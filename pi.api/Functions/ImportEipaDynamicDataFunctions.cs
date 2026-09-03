using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using pi.api.Collectors;

namespace pi.api.Functions;

public class ImportEipaDynamicDataFunctions
{
    private readonly ILogger<ImportEipaDynamicDataFunctions> _logger;
    private readonly NpgsqlDataSource _dataSource;
    private readonly IConfiguration _configuration;

    public ImportEipaDynamicDataFunctions(ILogger<ImportEipaDynamicDataFunctions> logger, NpgsqlDataSource dataSource, IConfiguration configuration)
    {
        _logger = logger;
        _dataSource = dataSource;
        _configuration = configuration;
    }

    [Function("ImportEipaDynamicData")]
    public async Task Run([TimerTrigger("0 */1 * * * *")] TimerInfo myTimer)
    {
        if (!(_configuration.GetValue<bool>("ImportEnabled"))) return;

        _logger.LogInformation($"[Timer] Funkcja ImportEipaDynamicData uruchomiona pomyślnie o: {DateTime.Now}");

        try
        {
            string url = _configuration["EipaDynamicUrl"] ?? throw new InvalidOperationException("Brak konfiguracji EipaDynamicUrl");
            var collector = new CollectorDynamic(_dataSource);
            await collector.Collect(url);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[Timer] Błąd podczas wykonywania funkcji ImportEipaDynamicData: {ex.Message}");
        }
        //TODO: Następne uruchomienie jest o tej samej godzinie co obecne
        _logger.LogInformation($"[Timer] Następne uruchomienie ImportEipaDynamicData: {myTimer.ScheduleStatus?.Next}");
    }
}