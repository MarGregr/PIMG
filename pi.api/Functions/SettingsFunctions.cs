using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Net;
using System.Security.Claims;
using System.Text.Json;

namespace pi.api.Functions;

public class SettingsFunctions
{
    private readonly ILogger<SettingsFunctions> _logger;
    private readonly NpgsqlDataSource _dataSource;

    public SettingsFunctions(ILogger<SettingsFunctions> logger, NpgsqlDataSource dataSource)
    {
        _logger = logger;
        _dataSource = dataSource;
    }

    [Function("GetUserSettings")]
    [Authorize]
    public async Task<IActionResult> GetUserSettings(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "settings")] HttpRequest req)
    {
        var userOid = req.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? req.HttpContext.User.FindFirst("oid")?.Value;

        if (string.IsNullOrEmpty(userOid))
            return new UnauthorizedResult();

        try
        {
            await using var conn = await _dataSource.OpenConnectionAsync();
            string query = "SELECT operator_id FROM user_settings WHERE user_oid = @userOid";

            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("userOid", userOid);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var settings = new UserSettingsResponseModel
                {
                    Operator_id = reader.GetInt32(reader.GetOrdinal("operator_id"))
                };
                return new OkObjectResult(settings);
            }

            return new OkObjectResult(new { operator_id = (int?)null });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Błąd podczas odczytu ustawień: {ex.Message}");
            return new StatusCodeResult((int)HttpStatusCode.InternalServerError);
        }
    }

    [Function("UpdateUserSettings")]
    [Authorize]
    public async Task<IActionResult> UpdateUserSettings(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "settings")] HttpRequest req)
    {
        var userOid = req.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? req.HttpContext.User.FindFirst("oid")?.Value;

        if (string.IsNullOrEmpty(userOid))
            return new UnauthorizedResult();

        UpdateSettingsRequestModel request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<UpdateSettingsRequestModel>(
                req.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );

            if (request == null || request.OperatorId <= 0)
                return new BadRequestObjectResult("Nieprawidłowy operator_id.");
        }
        catch
        {
            return new BadRequestObjectResult("Błędny format JSON.");
        }

        try
        {
            await using var conn = await _dataSource.OpenConnectionAsync();

            //UPSERT
            string query = @"
                INSERT INTO user_settings (user_oid, operator_id) 
                VALUES (@userOid, @operatorId)
                ON CONFLICT (user_oid) 
                DO UPDATE SET operator_id = EXCLUDED.operator_id;";

            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("userOid", userOid);
            cmd.Parameters.AddWithValue("operatorId", request.OperatorId);

            await cmd.ExecuteNonQueryAsync();

            return new OkResult();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Błąd zapisu: {ex.Message}");
            return new ObjectResult(new { error = ex.Message, inner = ex.InnerException?.Message })
            {
                StatusCode = (int)HttpStatusCode.InternalServerError
            };
        }
    }
}

public class UserSettingsResponseModel
{
    public int Operator_id { get; set; }
}

public class UpdateSettingsRequestModel
{
    public int OperatorId { get; set; }
}