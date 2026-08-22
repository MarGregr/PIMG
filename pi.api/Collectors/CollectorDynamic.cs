using CollectData.Collectors.JsonModels;
using CollectData.Collectors.RowModels;
using Npgsql;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;

namespace pi.api.Collectors;

/// <summary>
/// Wersja 3 - aktualne dane wczytane jednorazowo, zapis nowych danych przez bulk insert
/// </summary>
internal class CollectorDynamic : CollectorBase
{
    protected record ExistingStatusData
    {
        public DateTime ts { get; set; }
        public int avaliability { get; set; }
        public int status { get; set; }
    }

    protected List<StatusRow> newStatuses = [];

    protected Dictionary<long, ExistingStatusData> existingStatuses;
    protected Dictionary<(long PointId, string Unit), DateTime> existingPrices;

    public CollectorDynamic(NpgsqlDataSource dataSource) : base(dataSource)
    {
    }

    protected List<PriceRow> newPrices = [];

    public async Task<DynamicJson> GetDynamic(string dynamicUrl)
    {
        using var client = new HttpClient();
        return await client.GetFromJsonAsync<DynamicJson>(dynamicUrl);
    }

    public async Task Collect(string dynamicUrl)
    {
        startedAt = DateTime.UtcNow;
        this.source = dynamicUrl;
        stopwatch = Stopwatch.StartNew();
        var data = await GetDynamic(dynamicUrl);
        apiReadTime = stopwatch.ElapsedMilliseconds;

        await Collect(data);
    }

    public async Task CollectFromFile(string filePath)
    {
        startedAt = DateTime.UtcNow;
        this.source = filePath;
        stopwatch = Stopwatch.StartNew();
        var jsonContent = File.ReadAllText(filePath);
        var data = JsonSerializer.Deserialize<DynamicJson>(jsonContent);
        apiReadTime = stopwatch.ElapsedMilliseconds;

        await Collect(data);
    }

    public async Task Collect(DynamicJson data)
    {
        try
        {
            await using (conn = await _dataSource.OpenConnectionAsync())
            {
                newStatuses.Clear();
                newPrices.Clear();

                var pointsIds = data.data.Select(o => o.point_id).ToArray();
                await ReadStatuses(pointsIds);
                await ReadPrices(pointsIds);

                foreach (var item in data.data)
                {
                    await UpdateStatus(item);
                    await UpdatePrices(item);
                }

                await BulkInsertStatuses();
                await BulkInsertPrices();
            }
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
        }
        finally
        {
            await SaveStats();
        }
    }

    public async Task UpdateStatus(PointData item)
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
            var oldPointStatus = existingStatuses[item.point_id];
            add = (oldPointStatus.ts < jsonTs &&
                    (oldPointStatus.avaliability != item.status.availability || oldPointStatus.status != item.status.status)
                  );

            //W słowniku wczytanych punktów, dane dą uaktualniane zawsze
            existingStatuses[item.point_id] = new ExistingStatusData
            {
                status = item.status.status,
                avaliability = item.status.availability,
                ts = jsonTs
            };
        }
        else
        {
            add = true;

            existingStatuses[item.point_id] = new ExistingStatusData
            {
                status = item.status.status,
                avaliability = item.status.availability,
                ts = jsonTs
            };
        }

        if (add)
        {
            newStatuses.Add(new StatusRow
            {
                PointId = item.point_id,
                Availability = item.status.availability,
                Status = item.status.status,
                Ts = jsonTs,
            });
            statusCountInserted++;
        }
        statusCountAll++;
    }

    public async Task UpdatePrices(PointData item)
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

                int priceValue = (int)(decimal.Parse(priceItem.price, CultureInfo.InvariantCulture) * 100);
                newPrices.Add(new PriceRow
                {
                    PointId = item.point_id,
                    Unit = priceItem.unit,
                    Literal = priceItem.literal,
                    Price = priceValue,
                    Ts = jsonTs,
                });
                priceCountInserted++;
            }
            priceCountAll++;
        }
    }

    protected async Task ReadStatuses(long[] pointsIds)
    {
        existingStatuses = new Dictionary<long, ExistingStatusData>(pointsIds.Length);
        //string sql = "SELECT point_id, MAX(ts) FROM dynamic_status WHERE point_id = ANY(@ids) GROUP BY point_id";

        string sql = @"
            SELECT sub.point_id, sub.availability, sub.status, sub.ts
            FROM unnest(@ids) AS przekazane_id
            CROSS JOIN LATERAL (
                SELECT point_id, availability, status, ts
                FROM dynamic_status
                WHERE point_id = przekazane_id
                ORDER BY ts DESC
                LIMIT 1
            ) sub;";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("ids", pointsIds);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var record = new ExistingStatusData
            {
                avaliability = reader.GetInt16(1),
                status = reader.GetInt16(2),
                ts = reader.GetDateTime(3),
            };
            existingStatuses[reader.GetInt64(0)] = record;
        }
    }

    protected async Task ReadPrices(long[] pointsIds)
    {

        existingPrices = new Dictionary<(long PointId, string Unit), DateTime>();
        string sql = "SELECT point_id, unit, MAX(ts) FROM dynamic_price WHERE point_id = ANY(@ids) GROUP BY point_id, unit";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("ids", pointsIds);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            existingPrices[(reader.GetInt64(0), reader.GetString(1).ToLower())] = reader.GetDateTime(2);
        }
    }


    public async Task BulkInsertStatuses()
    {
        if (newStatuses.Count == 0)
        {
            return;
        }

        string tableName;
        var months = newStatuses.GroupBy(o => o.Ts.ToString("yyyyMM"));
        foreach (var month in months)
        {
            if (month.Key.CompareTo("202602") >= 0)
            {
                tableName = $"dynamic_status_{month.Key}";
            }
            else
            {
                //Starsze rekordy trafiają do tabeli partycji dynamic_status_2020
                tableName = "dynamic_status_2020";
            }

            using var writer = await conn.BeginBinaryImportAsync($"COPY {tableName} (point_id, availability, status, ts) FROM STDIN (FORMAT BINARY)");
            foreach (var item in month)
            {
                await writer.StartRowAsync();
                await writer.WriteAsync(item.PointId, NpgsqlTypes.NpgsqlDbType.Integer);
                await writer.WriteAsync(item.Availability, NpgsqlTypes.NpgsqlDbType.Smallint);
                await writer.WriteAsync(item.Status, NpgsqlTypes.NpgsqlDbType.Smallint);
                await writer.WriteAsync(item.Ts, NpgsqlTypes.NpgsqlDbType.Timestamp);
            }

            await writer.CompleteAsync();
        }
    }

    public async Task BulkInsertPrices()
    {
        if (newPrices.Count == 0)
        {
            return;
        }

        using var writer = await conn.BeginBinaryImportAsync("COPY dynamic_price (point_id, price, unit, literal, ts) FROM STDIN (FORMAT BINARY)");
        foreach (var item in newPrices)
        {
            await writer.StartRowAsync();
            await writer.WriteAsync(item.PointId, NpgsqlTypes.NpgsqlDbType.Integer);
            await writer.WriteAsync(item.Price, NpgsqlTypes.NpgsqlDbType.Integer);
            await writer.WriteAsync(item.Unit, NpgsqlTypes.NpgsqlDbType.Varchar);
            await writer.WriteAsync(item.Literal, NpgsqlTypes.NpgsqlDbType.Varchar);
            await writer.WriteAsync(item.Ts, NpgsqlTypes.NpgsqlDbType.Timestamp);
        }

        await writer.CompleteAsync();
    }
}


