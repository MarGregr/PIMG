using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Net;

namespace pi.api.Functions;

public class ReportsFunctions
{
    private readonly ILogger<ReportsFunctions> _logger;
    private readonly NpgsqlDataSource _dataSource;

    public ReportsFunctions(ILogger<ReportsFunctions> logger, NpgsqlDataSource dataSource)
    {
        _logger = logger;
        _dataSource = dataSource;
    }

    [Function("GetReportsVehicles")]
    [Authorize]
    public async Task<HttpResponseData> GetReportsVehicles(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "reports/vehicles")] HttpRequestData req)
    {
        var result = new List<VehicleStatReportModel>();

        try
        {
            await using var conn = await _dataSource.OpenConnectionAsync();

            string query = @"
                    select 
extract(year from data_ostatniej_rejestracji_w_kraju) as rok, 
count(p.*) as liczba,
case p.rodzaj_pojazdu when 'CIĄGNIK SAMOCHODOWY' THEN 'SAMOCHÓD CIĘŻAROWY' when 'SAMOCHÓD SPECJALNY' then 'SAMOCHÓD CIĘŻAROWY' when 'SAMOCHODOWY INNY' THEN 'SAMOCHÓD CIĘŻAROWY' when 'SAM.CIĘŻ. UNIWERSALNY' then 'SAMOCHÓD CIĘŻAROWY'
else p.rodzaj_pojazdu end as rodzaj
from pojazdy p
where p.rodzaj_paliwa='ENERGIA ELEKTRYCZNA'
and rodzaj_pojazdu not in ('PRZYCZEPA CIĘŻAROWA', 'PRZYCZEPA CIĘŻAROWA ROLNICZA', 'PRZYCZEPA LEKKA', 'PRZYCZEPA SPECJALNA','POJAZD WOLNOBIEŻNY-KOLEJKA TURYSTYCZNA')
group by extract(year from data_ostatniej_rejestracji_w_kraju),
case p.rodzaj_pojazdu when 'CIĄGNIK SAMOCHODOWY' THEN 'SAMOCHÓD CIĘŻAROWY' when 'SAMOCHÓD SPECJALNY' then 'SAMOCHÓD CIĘŻAROWY' when 'SAMOCHODOWY INNY' THEN 'SAMOCHÓD CIĘŻAROWY' when 'SAM.CIĘŻ. UNIWERSALNY' then 'SAMOCHÓD CIĘŻAROWY'
else p.rodzaj_pojazdu end
ORDER by extract(year from data_ostatniej_rejestracji_w_kraju)";

            await using var cmd = new NpgsqlCommand(query, conn);
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                result.Add(new VehicleStatReportModel
                {
                    rok = reader.GetInt16(reader.GetOrdinal("rok")),
                    rodzaj_pojazdu = reader.GetString(reader.GetOrdinal("rodzaj")),
                    liczba = reader.GetInt32(reader.GetOrdinal("liczba")),
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


    [Function("GetReportsOperators")]
    [Authorize]
    public async Task<IActionResult> GetPoints(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "reports/operators")] HttpRequest req)
    {
        var result = new List<OperatorResponse>();
        try
        {
            await using var conn = await _dataSource.OpenConnectionAsync();

            string query = @"
                    SELECT o.id, o.name, COALESCE(p.pools,0) as pools FROM public.operators o
                    LEFT JOIN (SELECT operator_id, COUNT(*) AS pools FROM pools WHERE active=true GROUP BY operator_id) p ON p.operator_id = o.id
                    WHERE o.active=true ORDER BY COALESCE(p.pools,0) DESC";

            await using var cmd = new NpgsqlCommand(query, conn);
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                result.Add(new OperatorResponse
                {
                    OperatorId = reader.GetInt32(reader.GetOrdinal("id")),
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