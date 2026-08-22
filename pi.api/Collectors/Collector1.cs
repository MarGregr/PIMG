using CollectData.Collectors.JsonModels;
using Npgsql;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;

namespace CollectData.Collectors;

/// <summary>
/// Wersja 1 - "naiwna"
/// </summary>
internal class Collector1
{
    public const string dynamicUrl = "https://eipa.udt.gov.pl/reader/export-data/dynamic/c4167d85dabe9e341470695c7180c128";
    public const string connString = "Host=localhost;Username=postgres;Password=123456;Database=pi";

    int statusCountInserted = 0;
    int statusCountAll = 0;

    int priceCountInserted = 0;
    int priceCountAll = 0;

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
        using var conn = new NpgsqlConnection(connString);
        conn.Open();
        foreach (var item in data.data)
        {
            await UpdateStatus(item, conn);
            await UpdatePrices(item, conn);
        }

        Console.WriteLine($"Wstawione rekordy statusów: {statusCountInserted}");
        Console.WriteLine($"Pominięte rekordy statusów: {statusCountAll - statusCountInserted}");
        Console.WriteLine($"Wstawione rekordy cen: {priceCountInserted}");
        Console.WriteLine($"Pominięte rekordy cen: {priceCountAll - priceCountInserted}");
        Console.WriteLine($"Czas działania: {stopwatch.ElapsedMilliseconds} ms");
    }

    public async Task UpdateStatus(PointData item, NpgsqlConnection conn)
    {
        if (item.status == null)
        {
            return;
        }
        DateTimeOffset dto = DateTimeOffset.Parse(item.status.ts);
        DateTime jsonTs = dto.DateTime;
        bool add = false;
        string sql = "SELECT ts FROM dynamic_status WHERE point_id = @point_id ORDER BY ts DESC LIMIT 1";

        using (var cmd = new NpgsqlCommand(sql, conn))
        {
            cmd.Parameters.AddWithValue("point_id", item.point_id);

            using (var reader = cmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    DateTime ts = reader.GetDateTime(0);
                    add = ts < jsonTs;
                }
                else
                {
                    add = true;
                }
            }

            if (add)
            {
                sql = "INSERT INTO dynamic_status (point_id, availability, status, ts) VALUES (@point_id, @availability, @status, @ts)";

                using (var insertCmd = new NpgsqlCommand(sql, conn))
                {
                    insertCmd.Parameters.AddWithValue("point_id", item.point_id);
                    insertCmd.Parameters.AddWithValue("availability", item.status.availability);
                    insertCmd.Parameters.AddWithValue("status", item.status.status);
                    insertCmd.Parameters.AddWithValue("ts", jsonTs);

                    await insertCmd.ExecuteNonQueryAsync();
                    statusCountInserted++;
                }
            }
            statusCountAll++;
        }
    }

    public async Task UpdatePrices(PointData item, NpgsqlConnection conn)
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
            string sql = "SELECT ts FROM dynamic_price WHERE point_id = @point_id AND unit = @unit ORDER BY ts DESC LIMIT 1";


            using (var cmd = new NpgsqlCommand(sql, conn))
            {
                //Dodawanie parametrów - chroni przed SQL Injection
                cmd.Parameters.AddWithValue("point_id", item.point_id);
                cmd.Parameters.AddWithValue("unit", priceItem.unit);


                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        DateTime ts = reader.GetDateTime(0);
                        add = ts < jsonTs;
                    }
                    else
                    {
                        add = true;
                    }
                }

                if (add)
                {
                    sql = "INSERT INTO dynamic_price (point_id, price, unit, literal, ts) VALUES (@point_id, @price, @unit, @literal, @ts)";

                    int priceValue = (int) (decimal.Parse(priceItem.price, CultureInfo.InvariantCulture) * 100);
                    using (var insertCmd = new NpgsqlCommand(sql, conn))
                    {
                        insertCmd.Parameters.AddWithValue("point_id", item.point_id);
                        insertCmd.Parameters.AddWithValue("price", priceValue);
                        insertCmd.Parameters.AddWithValue("unit", priceItem.unit);
                        insertCmd.Parameters.AddWithValue("literal", priceItem.literal);
                        insertCmd.Parameters.AddWithValue("ts", jsonTs);

                        await insertCmd.ExecuteNonQueryAsync();
                        priceCountInserted++;
                    }
                }
                priceCountAll++;
            }
        }
    }

}
