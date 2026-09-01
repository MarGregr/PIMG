////TU GETUSER I Z TEGO POBRAC OPERATORA USERA

//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Http;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.Azure.Functions.Worker;
//using Microsoft.Azure.Functions.Worker.Http;
//using Microsoft.Extensions.Logging;
//using Npgsql;
//using System.Net;
//using System.Security.Claims;

//namespace pi.api.Functions;

//public class OperatorsFunctions
//{
//    private readonly ILogger<OperatorsFunctions> _logger;
//    private readonly NpgsqlDataSource _dataSource;

//    public OperatorsFunctions(ILogger<OperatorsFunctions> logger, NpgsqlDataSource dataSource)
//    {
//        _logger = logger;
//        _dataSource = dataSource;
//    }

//    [Function("GetOperators")]
//    [Authorize]
//    public async Task<IActionResult> GetOperators(
//        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "operators")] HttpRequest req)
//    {
//        var operatorsList = new List<OperatorResponseModel>();
//        try
//        {
//            await using var conn = await _dataSource.OpenConnectionAsync();

//            string query = @"
//                    SELECT 
//                        o.id as id, 
//                        COALESCE(o.short_name, o.name) as name 
//                    FROM operators o
//                    where o.active = true
//                    ORDER by o.name ASC";

//            await using var cmd = new NpgsqlCommand(query, conn);
//            await using var reader = await cmd.ExecuteReaderAsync();

//            while (await reader.ReadAsync())
//            {

//                operatorsList.Add(new OperatorResponseModel
//                {
//                    Id = reader.GetInt32(reader.GetOrdinal("id")),
//                    Name = reader.GetString(reader.GetOrdinal("name"))
//                });
//            }
//        }
//        catch (Exception ex)
//        {
//            _logger.LogError($"Błąd podczas odczytu bazy danych: {ex.Message}");
//            return new StatusCodeResult((int)HttpStatusCode.InternalServerError);
//        }

//        //(!) OkObjectResult zamienia w JSON pierwszą literę nazwy pola na małą (!)
//        return new OkObjectResult(operatorsList);
//    }



//}

//public class OperatorResponseModel
//{
//    public int Id { get; set; }
//    public string Name { get; set; }
//}
