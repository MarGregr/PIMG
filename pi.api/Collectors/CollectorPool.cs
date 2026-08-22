using CollectData.Collectors.JsonModels;
using CollectData.Collectors.RowModels;
using NetTopologySuite.Geometries;
using Npgsql;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace pi.api.Collectors;


public class CollectorPool : CollectorBase
{
    public CollectorPool(NpgsqlDataSource dataSource) : base(dataSource)
    {
    }

    int featureCountInserted = 0;
    int featureCountUpdated = 0;
    int featureCountAll = 0;
    int operatingHourCountInserted = 0;
    int operatingHourCountUpdated = 0;
    int operatingHourCountAll = 0;
    int closingHourCountInserted = 0;
    int closingHourCountUpdated = 0;
    int closingHourCountAll = 0;

    protected Dictionary<long, DateTime> existingPools;
    protected List<PoolRow> newPools = [];
    protected List<FeatureRow> newFeatures = [];
    protected List<OperatingHourRow> newOperatingHours = [];
    protected List<ClosingHourRow> newClosingHours = [];

    public async Task Collect(string poolUrl)
    {
        startedAt = DateTime.UtcNow;
        this.source = poolUrl;
        stopwatch = Stopwatch.StartNew();
        var data = await GetPoolFromApi(poolUrl);
        apiReadTime = stopwatch.ElapsedMilliseconds;

        await Collect(data);
    }

    public async Task CollectFromFile(string filePath)
    {
        startedAt = DateTime.UtcNow;
        this.source = filePath;
        stopwatch = Stopwatch.StartNew();
        var jsonContent = File.ReadAllText(filePath);
        var data = JsonSerializer.Deserialize<PoolJson>(jsonContent);
        apiReadTime = stopwatch.ElapsedMilliseconds;

        await Collect(data);
    }

    public async Task<PoolJson> GetPoolFromApi(string poolUrl)
    {
        using var client = new HttpClient();
        return await client.GetFromJsonAsync<PoolJson>(poolUrl);
    }

    public async Task Collect(PoolJson data)
    {
        try
        {
            newPools.Clear();
            newFeatures.Clear();
            newOperatingHours.Clear();
            newClosingHours.Clear();

            await using (conn = await _dataSource.OpenConnectionAsync())
            {
                var ids = data.data.Select(o => o.id).ToArray();
                await ReadPools(ids);

                foreach (var item in data.data)
                {
                    await UpdatePools(item);
                }

                await BulkInsertPools();
                await BulkInsertFeatures();
                await BulkInsertOperatingHours();
                await BulkInsertClosingHours();

                //Dezaktywacja brakujących w bazie danych
                await DeactivateMissingPools(ids);
            }

            //Console.WriteLine($"Wstawione rekordy pools: {countInserted}");
            //Console.WriteLine($"Pominięte rekordy pools: {countAll - countInserted}");
            //Console.WriteLine($"Zaktualizowane rekordy pools: {countUpdated}");
            //Console.WriteLine($"Dezaktywowane rekordy pools: {countDeactivated}");
            //Console.WriteLine("");
            //Console.WriteLine($"Wstawione rekordy PoolsFeature: {featureCountInserted}");
            //Console.WriteLine($"Pominięte rekordy PoolsFeature: {featureCountAll - featureCountInserted}");
            //Console.WriteLine($"Zaktualizowane rekordy PoolsFeature: {featureCountUpdated}");
            //Console.WriteLine("");
            //Console.WriteLine($"Wstawione rekordy PoolsOperatingHour: {operatingHourCountInserted}");
            //Console.WriteLine($"Pominięte rekordy PoolsOperatingHour: {operatingHourCountAll - operatingHourCountInserted}");
            //Console.WriteLine($"Zaktualizowane rekordy PoolsOperatingHour: {operatingHourCountUpdated}");
            //Console.WriteLine("");
            //Console.WriteLine($"Wstawione rekordy PoolsClosingHour: {closingHourCountInserted}");
            //Console.WriteLine($"Pominięte rekordy PoolsClosingHour: {closingHourCountAll - closingHourCountInserted}");
            //Console.WriteLine($"Zaktualizowane rekordy PoolsClosingHour: {closingHourCountUpdated}");
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            Console.WriteLine($"CollectorPool; Error: {ex.Message}");
        }
        finally
        {
            //Console.WriteLine($"Czas działania: {stopwatch.ElapsedMilliseconds} ms");

            await SaveStats();
        }
    }

    public async Task UpdatePools(PoolData item)
    {
        if (item == null) return;
        countAll++;

        DateTimeOffset dto = DateTimeOffset.Parse(item.ts);
        DateTime jsonTs = dto.DateTime;

        if (existingPools.TryGetValue(item.id, out DateTime dbTs))
        {
            if (dbTs < jsonTs)
            {
                //Skoro pool ładowania się zmieniła, czyszczone są jej dotychczasowe powiązania w bazie
                await ExecuteDeleteOldFeatures(item.id);
                await ExecuteDeleteOldOperatingHours(item.id);
                await ExecuteDeleteOldClosingHours(item.id);

                //Aktualizacja głównego rekordu strefy
                await ExecuteUpdatePool(item, jsonTs);

                PrepareFeatures(item);
                PrepareOperatingHours(item);
                PrepareClosingHours(item);

                existingPools[item.id] = jsonTs;
                countUpdated++;
            }
            else
            {
                //if (item.features != null) featureCountAll += item.features.Count;
                if (item.operating_hours != null) operatingHourCountAll += item.operating_hours.Count;
                if (item.closing_hours != null) closingHourCountAll += item.closing_hours.Count;
            }
            return;
        }

        existingPools[item.id] = jsonTs;

        newPools.Add(new PoolRow
        {
            Id = item.id,
            OperatorId = item.operator_id,
            Code = item.code,
            Name = item.name,
            Accesibility = item.accesibility,
            Charging = item.charging,
            Filling = item.filling,
            Elevation = item.elevation,
            Street = item.street,
            HouseNumber = item.house_number,
            HouseNumberAddition = item.house_number_addition,
            PostalCode = item.postal_code,
            City = item.city,
            Latitude = item.latitude,
            Longitude = item.longitude,
            OperatorName = item.operator_name,
            OperatorPhone = item.operator_phone,
            OperatorWebsite = item.operator_website,
            OperatorEmail = item.operator_email,
            Ts = jsonTs,
            Teryt = item.teryt
        });

        PrepareFeatures(item);
        PrepareOperatingHours(item);
        PrepareClosingHours(item);

        countInserted++;
    }

    public void PrepareFeatures(PoolData item)
    {
        if (item.features == null) return;

        if (item.features is JsonArray)
        {
            string[] featureTable = item.features.Deserialize<string[]>();
            foreach (var featureName in featureTable)
            {
                featureCountAll++;

                newFeatures.Add(new FeatureRow
                {
                    PoolId = item.id,
                    Feature = featureName.ToString()
                });
            }
        }
    }

    public void PrepareOperatingHours(PoolData item)
    {
        if (item.operating_hours == null) return;

        foreach (var opHour in item.operating_hours)
        {
            operatingHourCountAll++;

            TimeSpan fromTimeSpan = TimeSpan.Parse(opHour.from_time);
            TimeSpan toTimeSpan = TimeSpan.Parse(opHour.to_time);

            DateTime baseDate = new DateTime(1900, 1, 1);

            newOperatingHours.Add(new OperatingHourRow
            {
                PoolId = item.id,
                Weekday = opHour.weekday,
                FromTime = baseDate.Add(fromTimeSpan),
                ToTime = baseDate.Add(toTimeSpan)
            });
        }
    }

    public void PrepareClosingHours(PoolData item)
    {
        if (item.closing_hours == null) return;

        foreach (var clHour in item.closing_hours)
        {
            closingHourCountAll++;

            DateTimeOffset fromTimeSpan = DateTimeOffset.Parse(clHour.from_time);
            DateTime fromTime = fromTimeSpan.DateTime;
            DateTimeOffset toTimeSpan = DateTimeOffset.Parse(clHour.to_time);
            DateTime toTime = toTimeSpan.DateTime;

            newClosingHours.Add(new ClosingHourRow
            {
                PoolId = item.id,
                FromTime = fromTime,
                ToTime = toTime
            });
        }
    }

    protected async Task ReadPools(long[] ids)
    {
        existingPools = new Dictionary<long, DateTime>(ids.Length);
        string sql = "SELECT id, ts FROM pools WHERE id = ANY(@ids)";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("ids", ids);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            existingPools[reader.GetInt64(0)] = reader.GetDateTime(1);
        }
    }

    public async Task BulkInsertPools()
    {
        if (newPools.Count == 0) return;

        using var writer = await conn.BeginBinaryImportAsync(
            "COPY pools (id, operator_id, code, name, accesibility, charging, filling, elevation, street, house_number, house_number_addition, postal_code, city, location, operator_name, operator_phone, operator_website, operator_email, ts, teryt) FROM STDIN (FORMAT BINARY)"
        );

        var geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);

        foreach (var item in newPools)
        {
            await writer.StartRowAsync();
            await writer.WriteAsync(item.Id, NpgsqlTypes.NpgsqlDbType.Bigint);
            await writer.WriteAsync(item.OperatorId, NpgsqlTypes.NpgsqlDbType.Bigint);
            await writer.WriteAsync(item.Code, NpgsqlTypes.NpgsqlDbType.Text);
            await writer.WriteAsync(item.Name, NpgsqlTypes.NpgsqlDbType.Text);
            await writer.WriteAsync(item.Accesibility, NpgsqlTypes.NpgsqlDbType.Text);
            await writer.WriteAsync(item.Charging, NpgsqlTypes.NpgsqlDbType.Boolean);
            await writer.WriteAsync(item.Filling, NpgsqlTypes.NpgsqlDbType.Boolean);
            await writer.WriteAsync(item.Elevation, NpgsqlTypes.NpgsqlDbType.Integer);
            await writer.WriteAsync(item.Street, NpgsqlTypes.NpgsqlDbType.Text);
            await writer.WriteAsync(item.HouseNumber, NpgsqlTypes.NpgsqlDbType.Text);
            await writer.WriteAsync(item.HouseNumberAddition, NpgsqlTypes.NpgsqlDbType.Text);
            await writer.WriteAsync(item.PostalCode, NpgsqlTypes.NpgsqlDbType.Text);
            await writer.WriteAsync(item.City, NpgsqlTypes.NpgsqlDbType.Text);

            Point locationPoint = geometryFactory.CreatePoint(new Coordinate(item.Longitude, item.Latitude));
            await writer.WriteAsync(locationPoint);

            await writer.WriteAsync(item.OperatorName, NpgsqlTypes.NpgsqlDbType.Text);
            await writer.WriteAsync(item.OperatorPhone, NpgsqlTypes.NpgsqlDbType.Text);
            await writer.WriteAsync(item.OperatorWebsite, NpgsqlTypes.NpgsqlDbType.Text);
            await writer.WriteAsync(item.OperatorEmail, NpgsqlTypes.NpgsqlDbType.Text);
            await writer.WriteAsync(item.Ts, NpgsqlTypes.NpgsqlDbType.Timestamp);
            await writer.WriteAsync(item.Teryt, NpgsqlTypes.NpgsqlDbType.Text);
        }
        await writer.CompleteAsync();
    }

    public async Task BulkInsertFeatures()
    {
        if (newFeatures.Count == 0) return;

        using var writer = await conn.BeginBinaryImportAsync(
            "COPY pools_features (pool_id, feature) FROM STDIN (FORMAT BINARY)"
        );

        foreach (var item in newFeatures)
        {
            await writer.StartRowAsync();
            await writer.WriteAsync((int)item.PoolId, NpgsqlTypes.NpgsqlDbType.Integer); // Jawne rzutowanie z bigint JSON na int bazy danych
            await writer.WriteAsync(item.Feature, NpgsqlTypes.NpgsqlDbType.Text);

            featureCountInserted++;
        }
        await writer.CompleteAsync();
    }

    public async Task BulkInsertOperatingHours()
    {
        if (newOperatingHours.Count == 0) return;

        using var writer = await conn.BeginBinaryImportAsync(
            "COPY pools_operating_hours (pool_id, weekday, from_time, to_time) FROM STDIN (FORMAT BINARY)"
        );

        foreach (var item in newOperatingHours)
        {
            await writer.StartRowAsync();
            await writer.WriteAsync((int)item.PoolId, NpgsqlTypes.NpgsqlDbType.Integer);
            await writer.WriteAsync(item.Weekday, NpgsqlTypes.NpgsqlDbType.Integer);
            await writer.WriteAsync(item.FromTime, NpgsqlTypes.NpgsqlDbType.Timestamp);
            await writer.WriteAsync(item.ToTime, NpgsqlTypes.NpgsqlDbType.Timestamp);

            operatingHourCountInserted++;
        }
        await writer.CompleteAsync();
    }

    public async Task BulkInsertClosingHours()
    {
        if (newClosingHours.Count == 0) return;

        using var writer = await conn.BeginBinaryImportAsync(
            "COPY pools_closing_hours (pool_id, from_time, to_time) FROM STDIN (FORMAT BINARY)"
        );

        foreach (var item in newClosingHours)
        {
            await writer.StartRowAsync();
            await writer.WriteAsync((int)item.PoolId, NpgsqlTypes.NpgsqlDbType.Integer);
            await writer.WriteAsync(item.FromTime, NpgsqlTypes.NpgsqlDbType.Timestamp);
            await writer.WriteAsync(item.ToTime, NpgsqlTypes.NpgsqlDbType.Timestamp);

            closingHourCountInserted++;
        }
        await writer.CompleteAsync();
    }

    private async Task ExecuteUpdatePool(PoolData item, DateTime jsonTs)
    {
        string sql = "UPDATE pools SET operator_id = @operator_id, code = @code, name = @name, accesibility = @accesibility, " +
                     "charging = @charging, filling = @filling, elevation = @elevation, street = @street, house_number = @house_number, " +
                     "house_number_addition = @house_number_addition, postal_code = @postal_code, city = @city, " +
                     "location = @location, operator_name = @operator_name, operator_phone = @operator_phone, operator_website = @operator_website, " +
                     "operator_email = @operator_email, ts = @ts, teryt = @teryt WHERE id = @id";


        await using var cmd = new NpgsqlCommand(sql, conn);

        var geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);
        Point locationPoint = geometryFactory.CreatePoint(new Coordinate(item.longitude, item.latitude));

        cmd.Parameters.AddWithValue("id", item.id);
        cmd.Parameters.AddWithValue("operator_id", item.operator_id);
        cmd.Parameters.AddWithValue("code", item.code ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("name", item.name ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("accesibility", item.accesibility ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("charging", (object)item.charging ?? DBNull.Value);
        cmd.Parameters.AddWithValue("filling", (object)item.filling ?? DBNull.Value);
        cmd.Parameters.AddWithValue("elevation", (object)item.elevation ?? DBNull.Value);
        cmd.Parameters.AddWithValue("street", item.street ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("house_number", (object)item.house_number ?? DBNull.Value);
        cmd.Parameters.AddWithValue("house_number_addition", (object)item.house_number_addition ?? DBNull.Value);
        cmd.Parameters.AddWithValue("postal_code", item.postal_code ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("city", item.city ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("location", locationPoint);
        cmd.Parameters.AddWithValue("operator_name", item.operator_name ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("operator_phone", item.operator_phone ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("operator_website", item.operator_website ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("operator_email", item.operator_email ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("ts", jsonTs);
        cmd.Parameters.AddWithValue("teryt", item.teryt ?? (object)DBNull.Value);

        await cmd.ExecuteNonQueryAsync();
    }

    private async Task ExecuteDeleteOldFeatures(long poolId)
    {
        string sql = "DELETE FROM pools_features WHERE pool_id = @poolId";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("poolId", (int)poolId);

        featureCountUpdated += await cmd.ExecuteNonQueryAsync();
    }

    private async Task ExecuteDeleteOldOperatingHours(long poolId)
    {
        string sql = "DELETE FROM pools_operating_hours WHERE pool_id = @poolId";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("poolId", (int)poolId);

        operatingHourCountUpdated += await cmd.ExecuteNonQueryAsync();
    }

    private async Task ExecuteDeleteOldClosingHours(long poolId)
    {
        string sql = "DELETE FROM pools_closing_hours WHERE pool_id = @poolId";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("poolId", (int)poolId);

        closingHourCountUpdated += await cmd.ExecuteNonQueryAsync();
    }

    protected async Task DeactivateMissingPools(long[] apiIds)
    {
        if (apiIds == null || apiIds.Length == 0) return;

        string sql = "UPDATE pools SET active = false WHERE active = true AND NOT (id = ANY(@apiIds))";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("apiIds", apiIds);

        countDeactivated = await cmd.ExecuteNonQueryAsync();
    }
}