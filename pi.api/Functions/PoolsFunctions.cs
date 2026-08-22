using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Net;
using System.Security.Claims;

namespace pi.api.Functions;

public class PoolsFunctions
{
    private readonly ILogger<PoolsFunctions> _logger;
    private readonly NpgsqlDataSource _dataSource;

    public PoolsFunctions(ILogger<PoolsFunctions> logger, NpgsqlDataSource dataSource)
    {
        _logger = logger;
        _dataSource = dataSource;
    }

    [Function("GetPools")]
    [Authorize]
    public async Task<IActionResult> GetPools(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "pools")] HttpRequest req)
    {
        var poolsList = new List<PoolResponseModel>();
        try
        {
            await using var conn = await _dataSource.OpenConnectionAsync();

            var (userId, email, username) = GetUserData(req);
            await using var cmdLog = conn.CreateCommand();
            cmdLog.CommandText = """
                INSERT INTO user_logs (created_at, user_oid, email, name)
                VALUES (@createdAt, @userOid, @email, @name)
                """;
            cmdLog.Parameters.AddWithValue("createdAt", DateTimeOffset.UtcNow);
            cmdLog.Parameters.AddWithValue("userOid", userId);
            cmdLog.Parameters.AddWithValue("email", email);
            cmdLog.Parameters.AddWithValue("name", username);

            await cmdLog.ExecuteNonQueryAsync();

            string query = @"
                    SELECT 
                        p.id as id, 
                        p.name as name, 
                        p.location AS location,
                        COALESCE(o.short_name, o.name) as operator
                    FROM public.pools p
                    LEFT JOIN public.operators o ON o.id = p.operator_id
                    WHERE p.active=true AND charging=true;";

            await using var cmd = new NpgsqlCommand(query, conn);
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {

                var point = reader.GetFieldValue<NetTopologySuite.Geometries.Point>(reader.GetOrdinal("location"));

                poolsList.Add(new PoolResponseModel
                {
                    Id = reader.GetInt32(reader.GetOrdinal("id")),
                    Name = reader.GetString(reader.GetOrdinal("name")),
                    Lng = point.X,
                    Lat = point.Y,
                    Operator = reader.GetString(reader.GetOrdinal("operator")),
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Błąd podczas odczytu bazy danych: {ex.Message}");
            return new StatusCodeResult((int)HttpStatusCode.InternalServerError);
        }

        //(!) OkObjectResult zamienia w JSON pierwszą literę nazwy pola na małą (!)
        return new OkObjectResult(poolsList);
    }


    public class PoolResponseModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public double Lng { get; set; }
        public double Lat { get; set; }
        public string Operator { get; set; }
    }

    [Function("GetPoolById")]
    [Authorize]
    public async Task<HttpResponseData> GetPoolById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "pools/{id}")] HttpRequestData req,
        long id)
    {
        try
        {
            await using var conn = await _dataSource.OpenConnectionAsync();

            string query = @"
                    SELECT p.id, p.location, p.name as name, o.name as operator_name, p.street, p.city, p.postal_code, p.house_number, COALESCE(p.house_number_addition,'') as house_number_addition
                    FROM public.pools p
                    left join operators o on p.operator_id=o.id where p.active=true and p.id=@id";

            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("id", id);
            await using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                string city = reader.GetFieldValue<string?>(reader.GetOrdinal("city")) ?? string.Empty;
                string street = reader.GetFieldValue<string?>(reader.GetOrdinal("street")) ?? string.Empty;
                string houseNumber = reader.GetFieldValue<string?>(reader.GetOrdinal("house_number")) ?? string.Empty;
                string houseNumberAdd = reader.GetFieldValue<string?>(reader.GetOrdinal("house_number_addition")) ?? string.Empty;
                string address = $"{city}, {street} {houseNumber}{(string.IsNullOrWhiteSpace(houseNumberAdd) ? string.Empty : $"/{houseNumberAdd}")}";

                var point = reader.GetFieldValue<NetTopologySuite.Geometries.Point>(reader.GetOrdinal("location"));

                var pool = new PoolFullResponseModel
                {
                    Id = reader.GetInt32(reader.GetOrdinal("id")),
                    Name = reader.GetString(reader.GetOrdinal("name")),
                    Lng = point.X,
                    Lat = point.Y,
                    OperatorName = reader.GetFieldValue<string?>(reader.GetOrdinal("operator_name")) ?? string.Empty,
                    Address = address,
                };

                await reader.CloseAsync();
                pool.Points = await GetPoints(conn, id);
                pool.OperationHours = await GetOperatingHours(conn, id);

                pool.ChargingStats = await GetChargingStats(conn, pool.Points);

                await SetChargingSolution(conn, pool.Points);

                return await CreateResponseAsync(req, HttpStatusCode.OK, pool);
            }
            else
            {
                //nie ma takiego punktu
                return await CreateResponseAsync(req, HttpStatusCode.NotFound, new { error = "not found" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Błąd podczas odczytu bazy danych: {ex.Message}");
            return await CreateResponseAsync(req, HttpStatusCode.InternalServerError, new { error = "Wystąpił błąd serwera." });
        }
    }

    private async Task<List<PointResponse>> GetPoints(NpgsqlConnection conn, long poolId)
    {
        string query = @"
                    SELECT p.Id, p.code,
                    (SELECT status FROM dynamic_status WHERE point_id=p.Id ORDER BY ts DESC LIMIT 1) AS status,
                    (SELECT availability FROM dynamic_status WHERE point_id=p.Id ORDER BY ts DESC LIMIT 1) AS availability,
                    (SELECT price FROM dynamic_price WHERE point_id=p.Id AND unit='kWh' ORDER BY ts DESC LIMIT 1) AS price
                    FROM points p
                    WHERE active=true AND station_id IN (
                        SELECT id FROM stations WHERE pool_id=@pool_id AND active=true AND type='E')";

        using var cmd = new NpgsqlCommand(query, conn);
        cmd.Parameters.AddWithValue("pool_id", poolId);
        using var reader = await cmd.ExecuteReaderAsync();

        var result = new List<PointResponse>();

        while (await reader.ReadAsync())
        {
            decimal price = 0;
            if (!reader.IsDBNull(reader.GetOrdinal("price")))
            {
                price = (decimal)reader.GetInt32(reader.GetOrdinal("price")) / 100;
            }
            var point = new PointResponse
            {
                Id = reader.GetInt32(reader.GetOrdinal("id")),
                Code = reader.GetString(reader.GetOrdinal("code")),
                Price = price,
                Status = reader.GetInt16(reader.GetOrdinal("status")),
                Availability = reader.GetInt16(reader.GetOrdinal("availability")),
            };

            result.Add(point);
        }
        return result;
    }

    private async Task<List<PoolOperationHoursResponde>> GetOperatingHours(NpgsqlConnection conn, long poolId)
    {
        string query = @"
            select pool_id, weekday, from_time, to_time from pools_operating_hours where pool_id=@pool_id order by weekday";

        using var cmd = new NpgsqlCommand(query, conn);
        cmd.Parameters.AddWithValue("pool_id", poolId);
        using var reader = await cmd.ExecuteReaderAsync();
        var result = new List<PoolOperationHoursResponde>();

        while (await reader.ReadAsync())
        {
            int dayId = reader.GetInt32(reader.GetOrdinal("weekday"));
            string day = dayId switch
            {
                1 => "Pon",
                2 => "Wto",
                3 => "Śro",
                4 => "Czw",
                5 => "Pią",
                6 => "Sob",
                7 => "Nie",
                _ => "???"
            };
            DateTime from = reader.GetDateTime(reader.GetOrdinal("from_time"));
            DateTime to = reader.GetDateTime(reader.GetOrdinal("to_time"));
            var dayInfo = new PoolOperationHoursResponde
            {
                DayId = dayId,
                Day = day,
                From = from.ToString("HH:mm"),
                To = to.ToString("HH:mm"),
            };

            result.Add(dayInfo);
        }
        return result;
    }

    private async Task SetChargingSolution(NpgsqlConnection conn, List<PointResponse> points)
    {
        long[] pointsIds = points.Select(o => o.Id).ToArray();

        string query = @"
            SELECT point_id, power, mode, name FROM public.points_charging_solutions pcs 
            LEFT JOIN charging_mode cm ON cm.id=pcs.mode
            WHERE point_id=ANY(@ids);";

        using var cmd = new NpgsqlCommand(query, conn);
        cmd.Parameters.AddWithValue("ids", pointsIds);
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            long pointId = reader.GetInt32(reader.GetOrdinal("point_id"));
            var cs = new PointChargingSolutionResponse
            {
                PointId = pointId,
                Power = reader.GetInt16(reader.GetOrdinal("power")),
                Mode = reader.GetInt16(reader.GetOrdinal("mode")),
                ModeName = reader.GetString(reader.GetOrdinal("name")),
            };

            var point = points.First(o => o.Id == pointId);
            point.Charging.Add(cs);
        }
    }

    private async Task<HttpResponseData> CreateResponseAsync(HttpRequestData req, HttpStatusCode statusCode, object responseBody)
    {
        var response = req.CreateResponse(statusCode);
        await response.WriteAsJsonAsync(responseBody);
        return response;
    }


    private async Task<List<ChargingDailyStat>> GetChargingStats(NpgsqlConnection conn, List<PointResponse> points)
    {
        //TODO: tymczasowo daty wbite na sztywno
        DateTime dataOd = new DateTime(2026, 06, 01, 0, 0, 0);
        DateTime dataDo = new DateTime(2026, 06, 08, 23, 59, 59);

        var rawData = await FetchTelemetryData(conn, points.Select(o => o.Id).ToArray(), dataOd, dataDo);

        var allPointIds = rawData.Select(r => r.PointId).Distinct().ToList();

        var detectedSessions = rawData
            .GroupBy(r => r.PointId)
            .SelectMany(pointGroup =>
            {
                var orderedRows = pointGroup.OrderBy(r => r.Timestamp).ToList();
                var detections = new List<(DateTime Date, int PointId)>();

                for (int i = 0; i < orderedRows.Count; i++)
                {
                    var currentRow = orderedRows[i];
                    if (currentRow.Availability == 0) continue;

                    if (currentRow.Status == 0 && (i == 0 || orderedRows[i - 1].Status == 1))
                    {
                        detections.Add((currentRow.Timestamp.Date, currentRow.PointId));
                    }
                }
                return detections;
            })
            .GroupBy(session => new { session.PointId, session.Date })
            .ToDictionary(
                g => (g.Key.PointId, g.Key.Date),
                g => g.Count()
            );

       
        int totalDays = (dataDo.Date - dataOd.Date).Days + 1;
        List<ChargingDailyStat> finalReport = Enumerable.Range(0, totalDays)
                .Select(offset => dataOd.Date.AddDays(offset))
                .Select(currentDate =>
                {
                    string dateStr = currentDate.ToString("yyyy-MM-dd");

                    var pointsForDay = allPointIds.Select(pointId =>
                    {
                        var key = (PointId: pointId, Date: currentDate);
                        int count = detectedSessions.ContainsKey(key) ? detectedSessions[key] : 0;
                        return new PointReport(pointId, count);
                    }).ToList();

                    //Suma wszystkich punktów razem dla TEGO dnia
                    int totalForDay = pointsForDay.Sum(p => p.ChargingCount);

                    return new ChargingDailyStat(dateStr, totalForDay);
                })
                .ToList();

        return finalReport;
    }


    private async Task<List<TelemetryRow>> FetchTelemetryData(NpgsqlConnection conn, long[] pointIds, DateTime fromDate, DateTime toDate)
    {
        var rows = new List<TelemetryRow>();

        string query = @"
                SELECT point_id, availability, status, ts 
                FROM dynamic_status 
                WHERE ts >= @data_od AND ts <= @data_do AND point_id = ANY(@ids)
                ORDER BY point_id, ts";

        using var command = new NpgsqlCommand(query, conn);
        command.Parameters.AddWithValue("data_od", fromDate);
        command.Parameters.AddWithValue("data_do", toDate);
        command.Parameters.AddWithValue("ids", pointIds);

        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            rows.Add(new TelemetryRow(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetInt32(2),
                reader.GetDateTime(3)
            ));
        }

        return rows;
    }

    private (string UserId, string Email, string Name) GetUserData(HttpRequest req)
    {
        var user = req.HttpContext.User;

        var userId = user.Claims.FirstOrDefault(c => c.Type == "http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value
                     ?? string.Empty;

        //Próba pobrania maila z preferred_username lub standardowego claimu email
        var email = user.Claims.FirstOrDefault(c => c.Type == "preferred_username")?.Value
                    ?? user.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value
                    ?? "unknown@domain.com";

        var name = user.Claims.FirstOrDefault(c => c.Type == "name")?.Value ?? string.Empty;

        return (userId, email, name);
    }
}

public record PointReport(int PointId, int ChargingCount);
public record ChargingDailyStat(
        string Date,
        int Count
    );

public class PointResponse
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public int Status { get; set; }
    public int Availability { get; set; }
    public decimal Price { get; set; }

    public List<PointChargingSolutionResponse> Charging { get; set; } = [];
    public List<PointConnectorResponse> Connectors { get; set; } = [];
}

public class PointChargingSolutionResponse
{
    public long PointId { get; set; }
    public int Mode { get; set; }
    public string ModeName { get; set; }
    public int Power { get; set; }
}

public class PointConnectorResponse
{
    public long PointId { get; set; }
    public int Power { get; set; }
    public List<int> Interfaces { get; set; } = [];
}


public class PoolFullResponseModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string OperatorName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public double Lng { get; set; }
    public double Lat { get; set; }
    public List<PoolOperationHoursResponde> OperationHours { get; set; } = [];
    public List<PointResponse> Points { get; set; } = [];
    public List<ChargingDailyStat> ChargingStats { get; set; } = [];
}

public class PoolOperationHoursResponde
{
    public int DayId { get; set; }
    public string Day { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
}

public record TelemetryRow(int PointId, int Availability, int Status, DateTime Timestamp);

public class ChargingStat
{
    public long PointId { get; set; }
    public string Date { get; set; }
    public int Count { get; set; }
}
