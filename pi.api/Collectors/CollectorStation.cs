using CollectData.Collectors.JsonModels;
using CollectData.Collectors.RowModels;
using Microsoft.CodeAnalysis;
using NetTopologySuite.Geometries;
using Npgsql;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;

namespace pi.api.Collectors;


internal class CollectorStation : CollectorBase
{
    int authenticationMethodCountInserted = 0;
    int authenticationMethodCountUpdated = 0;
    int authenticationMethodCountAll = 0;

    int paymentMethodCountInserted = 0;
    int paymentMethodCountUpdated = 0;
    int paymentMethodCountAll = 0;

    protected List<StationRow> newStations = [];
    protected List<AuthenticationMethodRow> newAuthenticationMethods = [];
    protected List<PaymentMethodRow> newPaymentMethods = [];
    protected Dictionary<long, DateTime> existingStations;

    public CollectorStation(NpgsqlDataSource dataSource) : base(dataSource)
    {
    }

    public async Task Collect(string stationUrl)
    {
        startedAt = DateTime.UtcNow;
        this.source = stationUrl;
        stopwatch = Stopwatch.StartNew();
        var data = await GetStationFromApi(stationUrl);
        apiReadTime = stopwatch.ElapsedMilliseconds;

        if (data?.data == null) return;
        await Collect(data);
    }

    public async Task CollectFromFile(string filePath)
    {
        startedAt = DateTime.UtcNow;
        this.source = filePath;
        stopwatch = Stopwatch.StartNew();
        var jsonContent = File.ReadAllText(filePath);
        var data = JsonSerializer.Deserialize<StationJson>(jsonContent);
        apiReadTime = stopwatch.ElapsedMilliseconds;

        if (data?.data == null) return;
        await Collect(data);
    }

    public async Task<StationJson> GetStationFromApi(string stationUrl)
    {
        using var client = new HttpClient();
        return await client.GetFromJsonAsync<StationJson>(stationUrl);
    }

    public async Task Collect(StationJson data)
    {
        try
        {
            await using (conn = await _dataSource.OpenConnectionAsync())
            {
                newStations.Clear();
                newAuthenticationMethods.Clear();
                newPaymentMethods.Clear();

                var ids = data.data.Select(o => o.id).ToArray();
                await ReadStations(ids);

                foreach (var item in data.data)
                {
                    await UpdateStation(item);
                }

                await BulkInsertStations();
                await BulkInsertAuthenticationMethods();
                await BulkInsertPaymentMethods();

                //Dezaktywacja brakujących w bazie danych
                await DeactivateMissingStations(ids);
            }

            //Console.WriteLine($"Wstawione rekordy stacji: {countInserted}");
            //Console.WriteLine($"Pominięte rekordy stacji: {countAll - countInserted}");
            //Console.WriteLine($"Zaktualizowane rekordy stacji: {countUpdated}");
            //Console.WriteLine($"Dezaktywowane rekordy stacji: {countDeactivated}");
            //Console.WriteLine("");
            //Console.WriteLine($"Wstawione rekordy AuthenticationMethod: {authenticationMethodCountInserted}");
            //Console.WriteLine($"Pominięte rekordy AuthenticationMethod: {authenticationMethodCountAll - authenticationMethodCountInserted}");
            //Console.WriteLine($"Zaktualizowane rekordy AuthenticationMethod: {authenticationMethodCountUpdated}");
            //Console.WriteLine("");
            //Console.WriteLine($"Wstawione rekordy PaymentMethod: {paymentMethodCountInserted}");
            //Console.WriteLine($"Pominięte rekordy PaymentMethod: {paymentMethodCountAll - paymentMethodCountInserted}");
            //Console.WriteLine($"Zaktualizowane rekordy PaymentMethod: {paymentMethodCountUpdated}");
            //Console.WriteLine($"Czas działania: {stopwatch.ElapsedMilliseconds} ms");
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

    public async Task UpdateStation(StationData item)
    {
        if (item.id == null)
        {
            return;
        }

        //Omijanie stacji o innym typie niż E (elektryczne)
        if (item.type != "E")
        {
            return;
        }

        countAll++;

        DateTimeOffset dto = DateTimeOffset.Parse(item.ts);
        DateTime jsonTs = dto.DateTime;

        if (existingStations.TryGetValue(item.id, out DateTime dbTs))
        {
            if (dbTs < jsonTs)
            {
                //Skoro stacja się zmieniła, czyszczone są jej dotychczasowe wpisy w bazie
                await ExecuteDeleteOldAuthenticationMethods(item.id);
                await ExecuteDeleteOldPaymentMethods(item.id);
                //Aktualizacja stacji
                await ExecuteUpdateStation(item, jsonTs);

                PrepareAuthenticationMethods(item);
                PreparePaymentMethods(item);

                existingStations[item.id] = jsonTs;
                countUpdated++;
            }
            else
            {
                //TODO: zastanowić się czy to potrzebne
                if (item.authentication_methods != null)
                {
                    authenticationMethodCountAll += item.authentication_methods.Count;
                }
                if (item.payment_methods != null)
                {
                    paymentMethodCountAll += item.payment_methods.Count;
                }
            }
            return;
        }

        existingStations[item.id] = jsonTs;

        newStations.Add(new StationRow
        {
            Id = item.id,
            PoolId = item.pool_id,
            Type = item.type,
            Latitude = item.latitude,
            Longitude = item.longitude,
            Province = item.location?.province,
            District = item.location?.district,
            Community = item.location?.community,
            City = item.location?.city,
            Ts = jsonTs,
        });

        PrepareAuthenticationMethods(item);
        PreparePaymentMethods(item);

        countInserted++;
    }

    public void PrepareAuthenticationMethods(StationData item)
    {
        if (item.authentication_methods == null) return;

        foreach (var auth in item.authentication_methods)
        {
            authenticationMethodCountAll++;

            newAuthenticationMethods.Add(new AuthenticationMethodRow
            {
                StationId = item.id,
                AuthenticationMethod = auth
            });
        }
    }

    public void PreparePaymentMethods(StationData item)
    {
        if (item.payment_methods == null) return;

        foreach (var pay in item.payment_methods)
        {
            paymentMethodCountAll++;

            newPaymentMethods.Add(new PaymentMethodRow
            {
                StationId = item.id,
                PaymentMethod = pay
            });
        }
    }

    protected async Task ReadStations(long[] ids)
    {
        existingStations = new Dictionary<long, DateTime>(ids.Length);
        string sql = "SELECT id, ts FROM stations WHERE id = ANY(@ids)";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("ids", ids);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            existingStations[reader.GetInt64(0)] = reader.GetDateTime(1);
        }
    }

    public async Task BulkInsertStations()
    {
        if (newStations.Count == 0)
        {
            return;
        }

        using var writer = await conn.BeginBinaryImportAsync(
            "COPY stations (id, pool_id, type, location, province, district, community, city, ts) FROM STDIN (FORMAT BINARY)"
            );

        var geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);

        foreach (var item in newStations)
        {
            await writer.StartRowAsync();
            await writer.WriteAsync(item.Id, NpgsqlTypes.NpgsqlDbType.Bigint);
            await writer.WriteAsync(item.PoolId, NpgsqlTypes.NpgsqlDbType.Bigint);
            await writer.WriteAsync(item.Type, NpgsqlTypes.NpgsqlDbType.Text);

            Point locationPoint = geometryFactory.CreatePoint(new Coordinate(item.Longitude, item.Latitude));
            await writer.WriteAsync(locationPoint);

            await writer.WriteAsync(item.Province, NpgsqlTypes.NpgsqlDbType.Text);
            await writer.WriteAsync(item.District, NpgsqlTypes.NpgsqlDbType.Text);
            await writer.WriteAsync(item.Community, NpgsqlTypes.NpgsqlDbType.Text);
            await writer.WriteAsync(item.City, NpgsqlTypes.NpgsqlDbType.Text);
            await writer.WriteAsync(item.Ts, NpgsqlTypes.NpgsqlDbType.Timestamp);
        }
        await writer.CompleteAsync();
    }

    public async Task BulkInsertAuthenticationMethods()
    {
        if (newAuthenticationMethods.Count == 0) return;

        using var writer = await conn.BeginBinaryImportAsync(
            "COPY stations_authentication_methods (station_id, authentication_method) FROM STDIN (FORMAT BINARY)"
        );

        foreach (var item in newAuthenticationMethods)
        {
            await writer.StartRowAsync();
            await writer.WriteAsync((int)item.StationId, NpgsqlTypes.NpgsqlDbType.Integer); // Rzutowanie na int zgodnie ze strukturą tabeli bazy danych
            await writer.WriteAsync(item.AuthenticationMethod, NpgsqlTypes.NpgsqlDbType.Integer);

            authenticationMethodCountInserted++;
        }
        await writer.CompleteAsync();
    }

    public async Task BulkInsertPaymentMethods()
    {
        if (newPaymentMethods.Count == 0) return;

        using var writer = await conn.BeginBinaryImportAsync(
            "COPY stations_payment_methods (station_id, payment_method) FROM STDIN (FORMAT BINARY)"
        );

        foreach (var item in newPaymentMethods)
        {
            await writer.StartRowAsync();
            await writer.WriteAsync((int)item.StationId, NpgsqlTypes.NpgsqlDbType.Integer); // Rzutowanie na int zgodnie ze strukturą tabeli bazy danych
            await writer.WriteAsync(item.PaymentMethod, NpgsqlTypes.NpgsqlDbType.Integer);

            paymentMethodCountInserted++;
        }
        await writer.CompleteAsync();
    }

    private async Task ExecuteUpdateStation(StationData item, DateTime jsonTs)
    {
        string sql = "UPDATE stations SET pool_id = @pool_id, type = @type, location = @location, " +
                     "province = @province, district = @district, community = @community, city = @city, ts = @ts WHERE id = @id";

        await using var cmd = new NpgsqlCommand(sql, conn);

        var geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);
        Point locationPoint = geometryFactory.CreatePoint(new Coordinate(item.longitude, item.latitude));

        cmd.Parameters.AddWithValue("id", item.id);
        cmd.Parameters.AddWithValue("pool_id", item.pool_id);
        cmd.Parameters.AddWithValue("type", item.type ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("location", locationPoint);
        cmd.Parameters.AddWithValue("province", item.location.province ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("district", item.location.district ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("community", item.location.community ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("city", item.location.city ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("ts", jsonTs);

        await cmd.ExecuteNonQueryAsync();
    }

    private async Task ExecuteDeleteOldAuthenticationMethods(long stationId)
    {
        string sql = "DELETE FROM stations_authentication_methods WHERE station_id = @stationId";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("stationId", (int)stationId);

        authenticationMethodCountUpdated += await cmd.ExecuteNonQueryAsync();
    }

    private async Task ExecuteDeleteOldPaymentMethods(long stationId)
    {
        string sql = "DELETE FROM stations_payment_methods WHERE station_id = @stationId";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("stationId", (int)stationId);

        paymentMethodCountUpdated += await cmd.ExecuteNonQueryAsync();
    }

    protected async Task DeactivateMissingStations(long[] apiIds)
    {
        if (apiIds == null || apiIds.Length == 0) return;

        string sql = "UPDATE stations SET active = false WHERE active = true AND NOT (id = ANY(@apiIds))";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("apiIds", apiIds);

        countDeactivated = await cmd.ExecuteNonQueryAsync();
    }
}