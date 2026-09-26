using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using pi.api.Services;
using System.Net;

namespace pi.api.Functions;

public class ReportsFunctions
{
    private readonly ILogger<ReportsFunctions> _logger;
    private readonly NpgsqlDataSource _dataSource;
    private readonly ReportVehicleService _reportVehicleService;

    public ReportsFunctions(ILogger<ReportsFunctions> logger, NpgsqlDataSource dataSource, ReportVehicleService reportVehicleService)
    {
        _logger = logger;
        _dataSource = dataSource;
        _reportVehicleService = reportVehicleService;
    }

    [Function("GetReportsVehicles")]
    [Authorize]
    public async Task<HttpResponseData> GetReportsVehicles(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "reports/vehicles/{vehicleFuelType}/{voivodeship?}")] HttpRequestData req, VehicleFuelType vehicleFuelType, string? voivodeship)
    {
        if (!System.Enum.IsDefined(typeof(VehicleFuelType), vehicleFuelType))
        {
            return await CreateResponseAsync(req, HttpStatusCode.BadRequest, new { error = "Nieprawidłowy parametr." });
        }
        try
        {
            var result = await _reportVehicleService.GetReportsVehicles(vehicleFuelType, voivodeship);
            return await CreateResponseAsync(req, HttpStatusCode.OK, result);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Błąd: {ex.Message}");
            return await CreateResponseAsync(req, HttpStatusCode.InternalServerError, new { error = "Wystąpił błąd serwera." });
        }
    }

    [Function("GetReportsPowiaty")]
    [Authorize]
    public async Task<HttpResponseData> GetReportsPowiaty(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "reports/powiaty")] HttpRequestData req)
    {
        var result = new List<PowiatModel>();

        try
        {
            await using var conn = await _dataSource.OpenConnectionAsync();

            string query = @"
                    SELECT rejestracja_powiat as powiat, count(*) as liczba FROM public.pojazdy
                    WHERE rodzaj_paliwa='ENERGIA ELEKTRYCZNA' and active=true
                    GROUP BY rejestracja_powiat";

            await using var cmd = new NpgsqlCommand(query, conn);
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                result.Add(new PowiatModel
                {
                    powiat = reader.GetString(reader.GetOrdinal("powiat")).ToLower(),
                    vehicles = reader.GetInt32(reader.GetOrdinal("liczba")),
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Błąd: {ex.Message}");
            return await CreateResponseAsync(req, HttpStatusCode.InternalServerError, new { error = "Wystąpił błąd serwera." });
        }

        return await CreateResponseAsync(req, HttpStatusCode.OK, result);
    }

    private async Task<HttpResponseData> CreateResponseAsync(HttpRequestData req, HttpStatusCode statusCode, object responseBody)
    {
        var response = req.CreateResponse(statusCode);
        await response.WriteAsJsonAsync(responseBody);
        return response;
    }


    //TODO: przenieść do service'a
    [Function("GetPojazdyWojewodztwa")]
    [Authorize]
    public async Task<IActionResult> GetVehiclesVoivodeships(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "reports/vehicles/voivodeships")] HttpRequest req)
    {
        var result = new List<VoivodeshipResponse>();
        try
        {
            await using var conn = await _dataSource.OpenConnectionAsync();

            string query = @"SELECT DISTINCT rejestracja_wojewodztwo AS name FROM pojazdy ORDER BY rejestracja_wojewodztwo";
            await using var cmd = new NpgsqlCommand(query, conn);
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                result.Add(new VoivodeshipResponse
                {
                    Name = reader.GetString(reader.GetOrdinal("name")),
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Błąd: {ex.Message}");
            return new StatusCodeResult((int)HttpStatusCode.InternalServerError);
        }

        //(!) OkObjectResult zamienia w JSON pierwszą literę nazwy pola na małą (!)
        return new OkObjectResult(result);
    }


    [Function("GetReportsOperators")]
    [Authorize]
    public async Task<IActionResult> GetReportsOperators(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "reports/operators/{kodWojPowiat}")] HttpRequest req, int kodWojPowiat)
    {
        var result = new List<OperatorResponse>();
        try
        {
            await using var conn = await _dataSource.OpenConnectionAsync();

            string query = @"
                    SELECT o.name, SUM(COALESCE(p.pools, 0)) as pools FROM public.operators o
                    LEFT JOIN 
	                    (SELECT operator_id, COUNT(*) AS pools FROM pools 
	                    WHERE active=true and (@kod_woj_powiat = 0 or kod_woj_powiat / 100 = @kod_woj_powiat)
	                    GROUP BY operator_id) p ON p.operator_id = o.id
                    WHERE o.active=true 
                    GROUP BY o.name
                    ORDER BY SUM(COALESCE(p.pools, 0)) DESC";



            await using var cmd = new NpgsqlCommand(query, conn);

            cmd.Parameters.AddWithValue("kod_woj_powiat", kodWojPowiat);

            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                result.Add(new OperatorResponse
                {
                    Name = reader.GetString(reader.GetOrdinal("name")),
                    PoolsQuantity = reader.GetInt32(reader.GetOrdinal("pools")),
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Błąd podczas odczytu bazy danych: {ex.Message}");
            return new StatusCodeResult((int)HttpStatusCode.InternalServerError);
        }

        //(!) OkObjectResult zamienia w JSON pierwszą literę nazwy pola na małą (!)
        return new OkObjectResult(result);
    }

}


public enum VehicleFuelType
{
    All = 0,
    Bev = 1,
    Hybrid = 2,
}

public class VehicleStatReportModel
{
    public int rok { get; set; }
    public string rodzaj_pojazdu { get; set; }
    public long liczba { get; set; }
}

public class PowiatModel
{
    public string powiat { get; set; }
    public int vehicles { get; set; }
}

public class OperatorResponse
{
    public long OperatorId { get; set; }
    public string Name { get; set; }
    public long PoolsQuantity { get; set; }
}

public class VoivodeshipResponse
{
    public string Name { get; set; }
}