using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using pi.api.Collectors;
using Npgsql;
using Microsoft.Extensions.Configuration;
using pi.api.Additional;

namespace pi.api.Functions;

public class AggregateFunctions
{
    private readonly ILogger<AggregateFunctions> _logger;
    private readonly NpgsqlDataSource _dataSource;
    private readonly IConfiguration _configuration;
    private readonly PoolsSummaryProcessor _summaryProcessor;

    public AggregateFunctions(ILogger<AggregateFunctions> logger, NpgsqlDataSource dataSource, IConfiguration configuration, PoolsSummaryProcessor summaryProcessor)
    {
        _logger = logger;
        _dataSource = dataSource;
        _configuration = configuration;
        _summaryProcessor = summaryProcessor;
    }

    //Uruchomienie o 4:00 UTC(w nocy) w każdą niedzielę
    [Function("PoolsAggregateFunction")]
    public async Task PoolsAggregateFunction([TimerTrigger("0 0 4 * * 0")] TimerInfo myTimer)
    {
        _logger.LogInformation($"[Timer] Funkcja PoolsAggregateFunction uruchomiona pomyślnie o: {DateTime.Now}");

        try
        {
            await _summaryProcessor.RefreshSummaryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError($"[Timer] Błąd podczas wykonywania funkcji PoolsAggregateFunction: {ex.Message}");
        }

        _logger.LogInformation($"[Timer] Następne uruchomienie PoolsAggregateFunction: {myTimer.ScheduleStatus?.Next}");
    }
}