using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Npgsql;
using pi.api.Services;
using System.Text.Json;

namespace pi.api.Functions;

public class ProjectsFunction
{
    private readonly ILogger<ProjectsFunction> _logger;
    private readonly NpgsqlDataSource _dataSource;
    private readonly ProjectsService _projectsService;

    public ProjectsFunction(ILogger<ProjectsFunction> logger, NpgsqlDataSource dataSource, ProjectsService projectsService)
    {
        _logger = logger;
        _dataSource = dataSource;
        _projectsService = projectsService;
    }



    // GET /api/projects
    [Function("GetProjects")]
    [Authorize]
    public async Task<IActionResult> GetProjects(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "projects")] HttpRequest req)
    {
        var userId = GetUserId(req);
        if (string.IsNullOrEmpty(userId)) return new UnauthorizedResult();

        await using var conn = await _dataSource.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();

        // ST_Y pobiera szerokość (lat), ST_X pobiera długość (lng) z typu GEOGRAPHY
        cmd.CommandText = """
            SELECT id, name, description, radius, ST_Y(location::geometry) as lat, ST_X(location::geometry) as lng, created_at, updated_at 
            FROM projects 
            WHERE user_id = @userId
            ORDER BY created_at DESC
            """;
        cmd.Parameters.AddWithValue("userId", userId);

        var projects = new List<ProjectDto>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            projects.Add(new ProjectDto
            {
                Id = reader.GetGuid(0),
                Name = reader.GetString(1),
                Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                Radius = reader.GetInt32(3),
                Lat = reader.GetDouble(4),
                Lng = reader.GetDouble(5),
                UserId = userId,
                CreatedAt = reader.GetDateTime(6),
                UpdatedAt = reader.GetDateTime(7)
            });
        }

        return new OkObjectResult(projects);
    }

    // POST /api/projects
    [Function("CreateProject")]
    [Authorize]
    public async Task<IActionResult> CreateProject(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "projects")] HttpRequest req)
    {
        var userId = GetUserId(req);
        if (string.IsNullOrEmpty(userId)) return new UnauthorizedResult();

        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        var data = JsonSerializer.Deserialize<ProjectDto>(requestBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (data == null || string.IsNullOrWhiteSpace(data.Name))
            return new BadRequestObjectResult(new { message = "Błędne dane wejściowe." });

        var projectId = data.Id == Guid.Empty ? Guid.NewGuid() : data.Id;
        var now = DateTime.UtcNow;

        await using var conn = await _dataSource.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = """
            INSERT INTO projects (id, name, description, radius, location, user_id, created_at, updated_at)
            VALUES (@id, @name, @description, @radius, ST_GeomFromText(@point, 4326), @userId, @createdAt, @updatedAt)
            """;

        cmd.Parameters.AddWithValue("id", projectId);
        cmd.Parameters.AddWithValue("name", data.Name.Trim());
        cmd.Parameters.AddWithValue("description", (object)data.Description?.Trim() ?? DBNull.Value);
        cmd.Parameters.AddWithValue("radius", data.Radius);
        cmd.Parameters.AddWithValue("point", $"POINT({data.Lng.ToString(System.Globalization.CultureInfo.InvariantCulture)} {data.Lat.ToString(System.Globalization.CultureInfo.InvariantCulture)})");
        cmd.Parameters.AddWithValue("userId", userId);
        cmd.Parameters.AddWithValue("createdAt", now);
        cmd.Parameters.AddWithValue("updatedAt", now);

        await cmd.ExecuteNonQueryAsync();

        data.Id = projectId;
        data.UserId = userId;
        data.CreatedAt = now;
        data.UpdatedAt = now;

        return new CreatedResult($"/api/projects/{projectId}", data);
    }

    // PUT /api/projects/{id}
    [Function("UpdateProject")]
    [Authorize]
    public async Task<IActionResult> UpdateProject(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "projects/{id:guid}")] HttpRequest req,
        Guid id)
    {
        var userId = GetUserId(req);
        if (string.IsNullOrEmpty(userId)) return new UnauthorizedResult();

        var project = await _projectsService.GetProjectByGuid(id);
        if(project == null)
        {
            return new NotFoundResult();
        }
        if(project.UserId != userId)
        {
            return new UnauthorizedResult();
        }

        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        var data = JsonSerializer.Deserialize<ProjectDto>(requestBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (data == null || string.IsNullOrWhiteSpace(data.Name))
            return new BadRequestObjectResult(new { message = "Błędne dane wejściowe." });

        var now = DateTime.UtcNow;

        await using var conn = await _dataSource.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();

        //Zabezpieczenie: filtrujemy po id projektu i po zalogowanym user_id
        cmd.CommandText = """
            UPDATE projects 
            SET name = @name, description = @description, radius = @radius, 
                location = ST_GeomFromText(@point, 4326), updated_at = @updatedAt
            WHERE id = @id AND user_id = @userId
            """;

        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("name", data.Name.Trim());
        cmd.Parameters.AddWithValue("description", (object)data.Description?.Trim() ?? DBNull.Value);
        cmd.Parameters.AddWithValue("radius", data.Radius);
        cmd.Parameters.AddWithValue("point", $"POINT({data.Lng.ToString(System.Globalization.CultureInfo.InvariantCulture)} {data.Lat.ToString(System.Globalization.CultureInfo.InvariantCulture)})");
        cmd.Parameters.AddWithValue("userId", userId);
        cmd.Parameters.AddWithValue("updatedAt", now);

        var affected = await cmd.ExecuteNonQueryAsync();

        //Jeśli żaden wiersz nie został zmodyfikowany, oznacza to brak uprawnień lub zły identyfikator projektu
        if (affected == 0)
            return new NotFoundObjectResult(new { message = "Projekt nie istnieje lub nie masz uprawnień do jego edycji." });

        data.Id = id;
        data.UserId = userId;
        data.UpdatedAt = now;

        return new OkObjectResult(data);
    }


    // GET /api/projects/{id}/predict
    [Function("PredictProject")]
    //[Authorize]
    [AllowAnonymous]
    public async Task<IActionResult> PredictProject(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "projects/{id:guid}/predict")] HttpRequest req,
        Guid id)
    {
        var userId = GetUserId(req);
        //if (string.IsNullOrEmpty(userId)) return new UnauthorizedResult();

        var project = await _projectsService.GetProjectByGuid(id);
        if (project == null)
        {
            return new NotFoundResult();
        }
        //if (project.UserId != userId)
        //{
        //    return new UnauthorizedResult();
        //}

        var value = await _projectsService.PredictProject(project);

        return new OkObjectResult(value);
    }


    private string GetUserId(HttpRequest req)
    {
        var user = req.HttpContext.User;
        return user.Claims.FirstOrDefault(c => c.Type == @"http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value ?? string.Empty;
    }
}