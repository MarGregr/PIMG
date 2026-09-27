using Npgsql;
using OSMApi;
using pi.api.Additional;
using static pi.api.Additional.Predictor;

namespace pi.api.Services;

public enum ChargingPointMode
{
    AC = 0,
    DC = 1
};

public class ProjectChargingPointDto
{
    public Guid ProjectId { get; set; }
    public ChargingPointMode Mode { get; set; }
    public int Power { get; set; }
    public decimal Price { get; set; }
}

public class ProjectDto
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public double Lat { get; set; }
    public double Lng { get; set; }
    public int OperatorId { get; set; }
    public ICollection<ProjectChargingPointDto> ChargingPoints { get; set; } = [];
    public string UserId { get; set; }

    public double? Prediction { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int? ReversePercentile { get; set; }
    public List<ChartPoint> UsageChartData { get; set; }
}

public class ChartPoint
{
    public int X { get; set; }
    public int Y { get; set; }
}

public class ProjectPredictDto
{
    public double Prediction { get; set; }
    public int ReversePercentile { get; set; }
    public List<ChartPoint> UsageChartData { get; set; }

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
        ProjectDto? project = null;

        await using var conn = await _dataSource.OpenConnectionAsync();

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
            SELECT id, name, description, ST_Y(location::geometry) as lat, ST_X(location::geometry) as lon, operator_id, user_id, created_at, updated_at, prediction 
            FROM projects 
            WHERE id = @id
            """;
            cmd.Parameters.AddWithValue("id", guid);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                double? prediction = reader.IsDBNull(9) ? null : reader.GetDouble(9);
                project = new ProjectDto
                {
                    Id = reader.GetGuid(0),
                    Name = reader.GetString(1),
                    Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                    Lat = reader.GetDouble(3),
                    Lng = reader.GetDouble(4),
                    OperatorId = reader.GetInt32(5),
                    UserId = reader.GetString(6),
                    CreatedAt = reader.GetDateTime(7),
                    UpdatedAt = reader.GetDateTime(8),
                    Prediction = prediction,
                    ReversePercentile = prediction == null ? null : await CalcRevPercentile(prediction.Value),
                    UsageChartData = await PrepareChartData(),
                };
            }
        }

        if (project == null) return null;

        await using (var cpCmd = conn.CreateCommand())
        {
            cpCmd.CommandText = """
            SELECT project_id, mode, power, price
            FROM projects_points
            WHERE project_id = @projectId
            """;
            cpCmd.Parameters.AddWithValue("projectId", guid);

            await using var cpReader = await cpCmd.ExecuteReaderAsync();
            while (await cpReader.ReadAsync())
            {
                project.ChargingPoints.Add(new ProjectChargingPointDto
                {
                    ProjectId = cpReader.GetGuid(0),
                    Mode = (ChargingPointMode)cpReader.GetInt32(1),
                    Power = cpReader.GetInt32(2),
                    Price = (decimal)cpReader.GetInt32(3) / 100
                });
            }
        }

        return project;
    }

    protected async Task<int> CalcRevPercentile(double prediction)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        await using var cmd = conn.CreateCommand();

        cmd.CommandText = """
            SELECT (SELECT COUNT(*) FROM pools_summary WHERE usage_percentage >= @prediction) * 1.0
            /
            (SELECT COUNT(*) FROM pools_summary);
            """;
        cmd.Parameters.AddWithValue("prediction", prediction);

        await using var reader = await cmd.ExecuteReaderAsync();

        await reader.ReadAsync();
        return Convert.ToInt32(reader.GetDouble(0) * 100);

    }

    protected async Task<List<ChartPoint>> PrepareChartData()
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        await using var cmd = conn.CreateCommand();

        cmd.CommandText = """
            SELECT COUNT (*), round(usage_percentage) FROM pools_summary 
            WHERE availability_total > 0
            GROUP BY round(usage_percentage)
            ORDER BY round(usage_percentage) asc
            """;

        await using var reader = await cmd.ExecuteReaderAsync();

        List<ChartPoint> result = new List<ChartPoint>();

        while (await reader.ReadAsync())
        {
            result.Add(new ChartPoint
            {
                X = reader.GetInt32(1),
                Y = reader.GetInt32(0),
            });
        }
        return result;
    }



    public async Task<ProjectPredictDto> PredictProject(ProjectDto project)
    {
        using var predictor = new Predictor();

        //double avgSessionPrice = (double)project.ChargingPoints.Average(p => p.Price);
        var acPoints = project.ChargingPoints.Where(p => p.Mode == ChargingPointMode.AC);
        var dcPoints = project.ChargingPoints.Where(p => p.Mode == ChargingPointMode.DC);
        int myOperatorId = project.OperatorId;
        int pointsCount = project.ChargingPoints.Count();
        int pointsCountAC = acPoints.Count();
        int pointsCountDC = dcPoints.Count();
        int totalPower = project.ChargingPoints.Sum(p => p.Power);
        int totalPowerAC = acPoints.Sum(p => p.Power);
        int totalPowerDC = dcPoints.Sum(p => p.Power);
        double avgSessionPriceAC = acPoints.Select(p => (double)p.Price).DefaultIfEmpty(0).Average();
        double avgSessionPriceDC = dcPoints.Select(p => (double)p.Price).DefaultIfEmpty(0).Average();

        int radius = 850;
        var bevCount = await _powiatyService.GetBevByLocation(project.Lng, project.Lat);

        var chargingPools = await _poolsService.PoolsInRange(project.Lng, project.Lat, radius, myOperatorId);
        var nearestChargingDistance = await _poolsService.NearestPoolDistance(project.Lng, project.Lat, myOperatorId);

        var pois = await _poiService.GetPois(project.Lng, project.Lat, radius);
        var amenities = GetPoiValue(pois, "amenity");
        var tourism = GetPoiValue(pois, "tourism");
        var shops = GetPoiValue(pois, "shop");
        var offices = GetPoiValue(pois, "office");
        var highway = pois.Any(o => o.PoiType1 == "highway" && o.Name.Contains("mop", StringComparison.OrdinalIgnoreCase) && o.Distance <= 400) ? 1.0 : 0.0;

        var modelData = new ModelInput
        {
            PoolPointACCount = pointsCountAC,
            PoolPointDCCount = pointsCountDC,
            BevCountPerPoint = bevCount / pointsCount,
            PoolLat = project.Lat,
            PoolLon = project.Lng,
            TotalPowerACPerPoint = pointsCountAC > 0 ? totalPowerDC / pointsCountAC : 0,
            TotalPowerDCPerPoint = pointsCountDC > 0 ? totalPowerDC / pointsCountDC : 0,
            TourismPerPoint = tourism / pointsCount,
            ShopsPerPoint = shops / pointsCount,
            OfficesPerPoint = offices / pointsCount,
            AmenitiesPerPoint = amenities / pointsCount,
            HighwayMop = highway,
            SmoothCompetitionIndex = CalculateSmoothCompetitonIndex(chargingPools, nearestChargingDistance),
            AvgSessionPriceAC = avgSessionPriceAC * 100, // bo model był trenowany na cenie w groszach
            AvgSessionPriceDC = avgSessionPriceDC * 100, // bo model był trenowany na cenie w groszach
        };

        var resultPrediction = predictor.PredictOccupancyRatio(modelData) * 100;

        var result = new ProjectPredictDto
        {
            Prediction = resultPrediction,
            ReversePercentile = await CalcRevPercentile(resultPrediction),
            UsageChartData = await PrepareChartData(),
        };
        return result;

    }

    protected double CalculateSmoothCompetitonIndex(double chargingPools, double nearestChargingDistance)
    {
        return (chargingPools + 1.0) / ((nearestChargingDistance / 1000.0) + 0.1);
    }

    protected float GetPoiValue(List<PoiItem> pois, string poiType)
    {
        const float TAU = 300;
        double result = 0;
        var selectedPois = pois.Where(o => o.PoiType1 == poiType);
        foreach (var poi in selectedPois)
        {
            result += Math.Exp(-poi.Distance / TAU);
        }
        return (float)result;
    }
}
