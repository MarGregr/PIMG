using Npgsql;
using System.Diagnostics;

namespace pi.api.Collectors;

public class CollectorBase
{
    protected readonly NpgsqlDataSource _dataSource;

    public CollectorBase(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public int countInserted = 0;
    public int countUpdated = 0;
    public int countDeactivated = 0;
    public int countAll = 0;

    public int statusCountInserted = 0;
    public int statusCountAll = 0;

    public int priceCountInserted = 0;
    public int priceCountAll = 0;

    protected Stopwatch stopwatch = Stopwatch.StartNew();
    protected DateTime startedAt;
    protected long apiReadTime = 0;
    public string errorMessage = string.Empty;
    protected string source = string.Empty;

    protected NpgsqlConnection conn;

    protected async Task SaveStats()
    {
        try
        {
            stopwatch.Stop();
            long durationMs = stopwatch.ElapsedMilliseconds;
            bool success = string.IsNullOrEmpty(errorMessage);

            await using (var statsConn = await _dataSource.OpenConnectionAsync())
            {
                await using var cmd = statsConn.CreateCommand();
                cmd.CommandText = """
                    INSERT INTO collector_stats (
                        collector_name, source, started_at, finished_at, duration_api_ms, duration_ms,
                        read, inserted, updated, deactivated, status_read, status_inserted, price_read, price_inserted, success, error_message
                    ) VALUES (
                        @collectorName, @source, @startedAt, @finishedAt, @durationApiMs, @durationMs,
                        @read, @inserted, @updated, @deactivated, @status_read, @status_inserted, @price_read, @price_inserted, @success, @errorMessage
                    )
                    """;

                cmd.Parameters.AddWithValue("collectorName", GetType().Name);
                cmd.Parameters.AddWithValue("source", this.source);
                cmd.Parameters.AddWithValue("startedAt", startedAt);
                cmd.Parameters.AddWithValue("finishedAt", DateTime.UtcNow);
                cmd.Parameters.AddWithValue("durationApiMs", apiReadTime);
                cmd.Parameters.AddWithValue("durationMs", durationMs);
                cmd.Parameters.AddWithValue("read", countAll);
                cmd.Parameters.AddWithValue("inserted", countInserted);
                cmd.Parameters.AddWithValue("updated", countUpdated);
                cmd.Parameters.AddWithValue("deactivated", countDeactivated);
                cmd.Parameters.AddWithValue("status_read", statusCountAll);
                cmd.Parameters.AddWithValue("status_inserted", statusCountInserted);
                cmd.Parameters.AddWithValue("price_read", priceCountAll);
                cmd.Parameters.AddWithValue("price_inserted", priceCountInserted);
                cmd.Parameters.AddWithValue("success", success);
                cmd.Parameters.AddWithValue("errorMessage", (object?)errorMessage ?? DBNull.Value);

                await cmd.ExecuteNonQueryAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Błąd zapisu statystyk: {ex.Message}");
        }
    }
}