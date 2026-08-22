using CollectData.Collectors.JsonModels;
using Npgsql;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;

namespace CollectData.Collectors;

/// <summary>
/// Wersja 2 - aktualne dane wczytane jednorazowo, zapis nowych danych przez pojedyncze inserty
/// </summary>
internal class Collector2
{
    public const string dynamicUrl = "https://eipa.udt.gov.pl/reader/export-data/dynamic/c4167d85dabe9e341470695c7180c128";
    public const string connString = "Host=localhost;Username=postgres;Password=123456;Database=pi";

    int statusCountInserted = 0;
    int statusCountAll = 0;

    int priceCountInserted = 0;
    int priceCountAll = 0;

    protected NpgsqlConnection conn;
    protected Dictionary<long, DateTime> existingStatuses;
    protected Dictionary<(long PointId, string Unit), DateTime> existingPrices;

    public async Task<DynamicJson> GetDynamic()
    {
        using var client = new HttpClient();
        return await client.GetFromJsonAsync<DynamicJson>(dynamicUrl);
    }

    public async Task Collect()
    {

        Stopwatch stopwatch = Stopwatch.StartNew();
        var data = await GetDynamic();
        Console.WriteLine($"Czas pobrania z API: {stopwatch.ElapsedMilliseconds} ms");

        await Collect(data);
    }

    public async Task Collect(DynamicJson data)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        conn = new NpgsqlConnection(connString);
        conn.Open();

        var pointsIds = data.data.Select(o => o.point_id).ToArray();
        await ReadStatuses(pointsIds);
        await ReadPrices(pointsIds);

        await using var transaction = await conn.BeginTransactionAsync();

        foreach (var item in data.data)
        {
            await UpdateStatus(item, transaction);
            await UpdatePrices(item, transaction);
        }

        await transaction.CommitAsync();

        await conn.CloseAsync();
        await conn.DisposeAsync();

        Console.WriteLine($"Wstawione rekordy statusów: {statusCountInserted}");
        Console.WriteLine($"Pominięte rekordy statusów: {statusCountAll - statusCountInserted}");
        Console.WriteLine($"Wstawione rekordy cen: {priceCountInserted}");
        Console.WriteLine($"Pominięte rekordy cen: {priceCountAll - priceCountInserted}");
        Console.WriteLine($"Czas działania: {stopwatch.ElapsedMilliseconds} ms");
    }

    public async Task UpdateStatus(PointData item, NpgsqlTransaction transaction)
    {
        if (item.status == null)
        {
            return;
        }
        DateTimeOffset dto = DateTimeOffset.Parse(item.status.ts);
        DateTime jsonTs = dto.DateTime;
        bool add = false;

        if (existingStatuses.ContainsKey(item.point_id))
        {
            var indb = existingStatuses[item.point_id];
            add = existingStatuses[item.point_id] < jsonTs;
        }
        else
        {
            add = true;
        }

        if (add)
        {
            existingStatuses[item.point_id] = jsonTs;

            string sql = "INSERT INTO dynamic_status (point_id, availability, status, ts) VALUES (@point_id, @availability, @status, @ts)";
            using (var insertCmd = new NpgsqlCommand(sql, conn, transaction))
            {
                insertCmd.Parameters.AddWithValue("point_id", item.point_id);
                insertCmd.Parameters.AddWithValue("availability", item.status.availability);
                insertCmd.Parameters.AddWithValue("status", item.status.status);
                insertCmd.Parameters.AddWithValue("ts", jsonTs);

                await insertCmd.ExecuteNonQueryAsync();
            }

            statusCountInserted++;
        }
        statusCountAll++;
    }

    public async Task UpdatePrices(PointData item, NpgsqlTransaction transaction)
    {
        foreach (var priceItem in item.prices)
        {

            if (priceItem.price == null || priceItem.unit == null)
            {
                return;
            }

            DateTimeOffset dto = DateTimeOffset.Parse(priceItem.ts);
            DateTime jsonTs = dto.DateTime;
            bool add = false;

            string unitLower = priceItem.unit.ToLower();

            if (existingPrices.ContainsKey((item.point_id, unitLower)))
            {
                add = existingPrices[(item.point_id, unitLower)] < jsonTs;
            }
            else
            {
                add = true;
            }

            if (add)
            {
                existingPrices[(item.point_id, unitLower)] = jsonTs;

                string sql = "INSERT INTO dynamic_price (point_id, price, unit, literal, ts) VALUES (@point_id, @price, @unit, @literal, @ts)";
                int priceValue = (int)(decimal.Parse(priceItem.price, CultureInfo.InvariantCulture) * 100);
                using (var insertCmd = new NpgsqlCommand(sql, conn, transaction))
                {
                    insertCmd.Parameters.AddWithValue("point_id", item.point_id);
                    insertCmd.Parameters.AddWithValue("price", priceValue);
                    insertCmd.Parameters.AddWithValue("unit", priceItem.unit);
                    insertCmd.Parameters.AddWithValue("literal", priceItem.literal);
                    insertCmd.Parameters.AddWithValue("ts", jsonTs);

                    await insertCmd.ExecuteNonQueryAsync();
                }

                priceCountInserted++;
            }
            priceCountAll++;
        }
    }

    protected async Task ReadStatuses(long[] pointsIds)
    {
        existingStatuses = new Dictionary<long, DateTime>(pointsIds.Length);
        string sql = "SELECT point_id, MAX(ts) FROM dynamic_status WHERE point_id = ANY(@ids) GROUP BY point_id";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("ids", pointsIds);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            existingStatuses[reader.GetInt64(0)] = reader.GetDateTime(1);
        }
    }

    protected async Task ReadPrices(long[] pointsIds)
    {
        existingPrices = new Dictionary<(long PointId, string Unit), DateTime>(); // (uniquePairs.Length);
        string sql = "SELECT point_id, unit, MAX(ts) FROM dynamic_price WHERE point_id = ANY(@ids) GROUP BY point_id, unit";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("ids", pointsIds);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            existingPrices[(reader.GetInt64(0), reader.GetString(1).ToLower())] = reader.GetDateTime(2);
        }
    }

}
