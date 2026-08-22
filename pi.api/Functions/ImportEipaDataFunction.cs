using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using pi.api.Collectors;
using Npgsql;
using Microsoft.Extensions.Configuration;

namespace pi.api.Functions;

public class ImportEipaDataFunction
{
    private readonly ILogger<ImportEipaDataFunction> _logger;
    private readonly NpgsqlDataSource _dataSource;
    private readonly IConfiguration _configuration;

    public ImportEipaDataFunction(ILogger<ImportEipaDataFunction> logger, NpgsqlDataSource dataSource, IConfiguration configuration)
    {
        _logger = logger;
        _dataSource = dataSource;
        _configuration = configuration;
    }

    //Uruchamiene o 2:00 UTC(w nocy)
    [Function("ImportEipaStaticData")]
    public async Task Run([TimerTrigger("0 45 16 * * *")] TimerInfo myTimer)
    //public async Task Run([TimerTrigger("0 */1 * * * *")] TimerInfo myTimer)
    {
        if (!(_configuration.GetValue<bool>("ImportEnabled"))) return;

        _logger.LogInformation($"[Timer] Funkcja ImportEipaStaticData uruchomiona pomyślnie o: {DateTime.Now}");

        try
        {
            string operatorUrl = _configuration["EipaOperatorUrl"] ?? throw new InvalidOperationException("Brak konfiguracji EipaOperatorUrl");
            string poolUrl = _configuration["EipaPoolUrl"] ?? throw new InvalidOperationException("Brak konfiguracji EipaPoolUrl");
            string stationUrl = _configuration["EipaStationUrl"] ?? throw new InvalidOperationException("Brak konfiguracji EipaStationUrl");
            string pointUrl = _configuration["EipaPointUrl"] ?? throw new InvalidOperationException("Brak konfiguracji EipaPointUrl");

            var opCollector = new CollectorOperator(_dataSource);
            await opCollector.Collect(operatorUrl);

            var poolCollector = new CollectorPool(_dataSource);
            await poolCollector.Collect(poolUrl);

            var stCollector = new CollectorStation(_dataSource);
            await stCollector.Collect(stationUrl);

            var pointCollector = new CollectorPoint(_dataSource);
            await pointCollector.Collect(pointUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[Timer] Błąd podczas wykonywania funkcji ImportEipaStaticData: {ex.Message}");
        }

        _logger.LogInformation($"[Timer] Następne uruchomienie ImportEipaStaticData: {myTimer.ScheduleStatus?.Next}");
    }
}