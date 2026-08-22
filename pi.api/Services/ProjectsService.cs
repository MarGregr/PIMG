using Microsoft.AspNetCore.Mvc;
using Npgsql;
using OSMApi;
using pi.api.Additional;
using static pi.api.Additional.Predictor;

namespace pi.api.Services;

public class ProjectDto
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public double Lat { get; set; }
    public double Lng { get; set; }
    public int Radius { get; set; }
    public string UserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ProjectsService
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly PoiService _poiService;
    private readonly PowiatyService _powiatyService;
    private readonly PoolsService _poolsService;

    public ProjectsService(NpgsqlDataSource dataSource, PoiService poiService, PowiatyService powiatyService, PoolsService poolsService)
    {
        _dataSource = dataSource;
        _poiService = poiService;
        _powiatyService = powiatyService;
        _poolsService = poolsService;
    }

    public async Task<ProjectDto?> GetProjectByGuid(Guid guid)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = """
            SELECT id, name, description, radius, ST_Y(location::geometry) as lat, ST_X(location::geometry) as lon, user_id, created_at, updated_at 
            FROM projects 
            WHERE id = @id
            """;
        cmd.Parameters.AddWithValue("id", guid);

        var projects = new List<ProjectDto>();
        await using var reader = await cmd.ExecuteReaderAsync();
        if (reader.HasRows)
        {
            await reader.ReadAsync();
            return new ProjectDto
            {
                Id = reader.GetGuid(0),
                Name = reader.GetString(1),
                Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                Radius = reader.GetInt32(3),
                Lat = reader.GetDouble(4),
                Lng = reader.GetDouble(5),
                UserId = reader.GetString(6),
                CreatedAt = reader.GetDateTime(7),
                UpdatedAt = reader.GetDateTime(8)
            };
        }

        return null;
    }

    public async Task<float> PredictProject(ProjectDto project)
    {
        using var predictor = new Predictor();

        double avgSessionPrice = 1.49;
        int myOperatorId = 0;
        int pointsCount = 1;
        int totalPower = 22;

        int radius = 850;
        var bevCount = await _powiatyService.GetBevByLocation(project.Lng, project.Lat);

        var chargingPools = await _poolsService.PoolsInRange(project.Lng, project.Lat, radius, myOperatorId);
        var nearestChargingDistance = await _poolsService.NearestPoolDistance(project.Lng, project.Lat, myOperatorId);

        var pois = await _poiService.GetPois(project.Lng, project.Lat, radius);
        var amenities = pois.Count(o => o.PoiType1 == "amenity");
        var tourism = pois.Count(o => o.PoiType1 == "tourism");

        var modelData = new ModelInput
        {
            BevCount = bevCount,
            AvgSessionPrice = avgSessionPrice,
            ChargingPools = chargingPools,
            NearestChargingDistance = nearestChargingDistance,
            PoolLat = project.Lat,
            PoolLon = project.Lng,
            PoolPointCount = pointsCount,
            TotalPower = totalPower,
            Amenities = amenities,
            Tourism = tourism,
        };

        var result = predictor.PredictOccupancyRatio(modelData);
        return result;
    }
}
