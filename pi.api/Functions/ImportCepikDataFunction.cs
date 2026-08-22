using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using pi.api.Collectors;
using Npgsql;
using Microsoft.Extensions.Configuration;
using pi.api.Additional;

namespace pi.api.Functions;

public class ImportCepikDataFunction
{
    private readonly ILogger<ImportCepikDataFunction> _logger;
    private readonly NpgsqlDataSource _dataSource;
    private readonly IConfiguration _configuration;
    private readonly SummaryProcessor _summaryProcessor;

    public ImportCepikDataFunction(ILogger<ImportCepikDataFunction> logger, NpgsqlDataSource dataSource, IConfiguration configuration, SummaryProcessor summaryProcessor)
    {
        _logger = logger;
        _dataSource = dataSource;
        _configuration = configuration;
        _summaryProcessor = summaryProcessor;
    }

    //Uruchomienie o 3:00 UTC(w nocy)
    [Function("ImportCepikVehiclesData")]
    public async Task Run([TimerTrigger("0 8 18 * * *")] TimerInfo myTimer)
    //public async Task Run([TimerTrigger("0 */1 * * * *")] TimerInfo myTimer)
    {
        if (!(_configuration.GetValue<bool>("ImportEnabled"))) return;

        _logger.LogInformation($"[Timer] Funkcja ImportCepikVehiclesData uruchomiona pomyślnie o: {DateTime.Now}");

        try
        {
            string vehicleUrl = _configuration["CepikVehicleUrl"] ?? throw new InvalidOperationException("Brak konfiguracji CepikVehicleUrl");

            var opCollector = new CollectorVehicle(_dataSource);

            DateTime fromDate = DateTime.Today.AddDays(-18);
            DateTime toDate = fromDate;

            await opCollector.Collect(vehicleUrl, fromDate.ToString("yyyyMMdd"), toDate.ToString("yyyyMMdd"));
        }
        catch (Exception ex)
        {
            _logger.LogError($"[Timer] Błąd podczas wykonywania funkcji ImportCepikVehiclesData: {ex.Message}");
        }
        
        await _summaryProcessor.RefreshSummaryAsync();

        _logger.LogInformation($"[Timer] Następne uruchomienie ImportCepikVehiclesData: {myTimer.ScheduleStatus?.Next}");
    }
}