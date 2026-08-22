using CollectData.Collectors.JsonModels;
using CollectData.Collectors.RowModels;
using Npgsql;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;

namespace pi.api.Collectors;


internal class CollectorPoint : CollectorBase
{
    int chargingSolutionCountInserted = 0;
    int chargingSolutionCountUpdated = 0;
    int chargingSolutionCountAll = 0;

    int connectorsCountInserted = 0;
    int connectorsCountUpdated = 0;
    int connectorsCountAll = 0;

    int interfacesCountInserted = 0;
    int interfacesCountUpdated = 0;
    int interfacesCountAll = 0;

    protected Dictionary<long, DateTime> existingPoints;
    protected Dictionary<long, DateTime> existingStations;

    public CollectorPoint(NpgsqlDataSource dataSource) : base(dataSource)
    {
    }

    protected List<PointRow> newPoints = [];
    protected List<ChargingSolutionRow> newSolutions = [];
    protected List<ConnectorRow> newConnectors = [];
    //protected List<ConnectorInterfaceRow> newInterfaces = [];


    public async Task<PointJson> GetPointFromApi(string pointUrl)
    {
        using var client = new HttpClient();
        return await client.GetFromJsonAsync<PointJson>(pointUrl);
    }

    public async Task Collect(string pointUrl)
    {
        startedAt = DateTime.UtcNow;
        this.source = pointUrl;
        stopwatch = Stopwatch.StartNew();
        var data = await GetPointFromApi(pointUrl);
        apiReadTime = stopwatch.ElapsedMilliseconds;

        await Collect(data);
    }

    public async Task CollectFromFile(string filePath)
    {
        startedAt = DateTime.UtcNow;
        this.source = filePath;
        stopwatch = Stopwatch.StartNew();
        var jsonContent = File.ReadAllText(filePath);
        var data = JsonSerializer.Deserialize<PointJson>(jsonContent);
        apiReadTime = stopwatch.ElapsedMilliseconds;

        await Collect(data);
    }

    public async Task Collect(PointJson data)
    {
        try
        {
            await using (conn = await _dataSource.OpenConnectionAsync())
            {
                newPoints.Clear();
                newSolutions.Clear();
                newConnectors.Clear();
                //newInterfaces.Clear();

                var ids = data.data.Select(o => o.id).ToArray();
                await ReadPoints(ids);
                await ReadStations();

                foreach (var item in data.data)
                {
                    await UpdatePoints(item);
                }

                await BulkInsertPoints();
                await BulkInsertChargingSolutions();
                await BulkInsertConnectors();

                //Dezaktywacja brakujących w bazie danych
                await DeactivateMissingPoints(ids);
            }

            //Console.WriteLine($"Wstawione rekordy punktów: {pointCountInserted}");
            //Console.WriteLine($"Pominięte rekordy punktów: {pointCountAll - pointCountInserted}");
            //Console.WriteLine($"Zaktualizowane rekordy punktów: {pointCountUpdated}");
            //Console.WriteLine($"Dezaktywowane rekordy punktów: {pointCountDeactivated}");
            //Console.WriteLine("");
            //Console.WriteLine($"Wstawione rekordy ChargingSolution: {chargingSolutionCountInserted}");
            //Console.WriteLine($"Pominięte rekordy ChargingSolution: {chargingSolutionCountAll - chargingSolutionCountInserted}");
            //Console.WriteLine($"Zaktualizowane rekordy ChargingSolution: {chargingSolutionCountUpdated}");
            //Console.WriteLine("");
            //Console.WriteLine($"Wstawione rekordy Connector: {connectorsCountInserted}");
            //Console.WriteLine($"Pominięte rekordy Connector: {connectorsCountAll - connectorsCountInserted}");
            //Console.WriteLine($"Zaktualizowane rekordy Connector: {connectorsCountUpdated}");
            //Console.WriteLine("");
            //Console.WriteLine($"Wstawione rekordy Interface: {interfacesCountInserted}");
            //Console.WriteLine($"Pominięte rekordy Interface: {interfacesCountAll - interfacesCountInserted}");
            //Console.WriteLine($"Zaktualizowane rekordy Interface: {interfacesCountUpdated}");
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

    public async Task UpdatePoints(SinglePointData item)
    {
        if (item.id == null)
        {
            return;
        }

        //Ignorowanie punktów których nie ma w tablicy stations (np. stacji gazowych)
        if (!existingStations.ContainsKey(item.station_id))
        {
            return;
        }

        countAll++;

        DateTimeOffset dto = DateTimeOffset.Parse(item.ts);
        DateTime jsonTs = dto.DateTime;

        if (existingPoints.TryGetValue(item.id, out DateTime dbTs))
        {
            if (dbTs < jsonTs)
            {
                //Skoro punkt się zmienił, czyszczone są jego dotychczasowe powiązania w bazie
                await ExecuteDeleteOldSolutions(item.id);
                await ExecuteDeleteOldConnectors(item.id);

                //Aktualizacja punktu
                await ExecuteUpdatePoint(item, jsonTs);

                PrepareChargingSolutions(item);
                PrepareConnectors(item);

                existingPoints[item.id] = jsonTs;
                countUpdated++;
            }
            else
            {
                if (item.charging_solutions != null)
                {
                    chargingSolutionCountAll += item.charging_solutions.Count;
                }
                if (item.connectors != null)
                {
                    connectorsCountAll += item.connectors.Count;

                    //Zliczanie pominiętych interfejsów z poziomu złączy do statystyk
                    foreach (var connItem in item.connectors)
                    {
                        if (connItem.interfaces != null)
                        {
                            interfacesCountAll += connItem.interfaces.Count;
                        }
                    }
                }
            }
            return;
        }

        existingPoints[item.id] = jsonTs;

        newPoints.Add(new PointRow
        {
            Id = item.id,
            Code = item.code,
            StationId = item.station_id,
            Ts = jsonTs,
        });

        PrepareChargingSolutions(item);
        PrepareConnectors(item);

        countInserted++;
    }

    public void PrepareChargingSolutions(SinglePointData item)
    {
        if (item.charging_solutions == null) return;

        foreach (var sol in item.charging_solutions)
        {
            chargingSolutionCountAll++;

            newSolutions.Add(new ChargingSolutionRow
            {
                PointId = item.id,
                Mode = sol.mode,
                Power = sol.power
            });
        }
    }

    public void PrepareConnectors(SinglePointData item)
    {
        if (item.connectors == null) return;

        foreach (var connItem in item.connectors)
        {
            connectorsCountAll++;

            DateTime connectorTs = string.IsNullOrEmpty(connItem.ts)
                ? DateTimeOffset.Parse(item.ts).DateTime
                : DateTimeOffset.Parse(connItem.ts).DateTime;

            newConnectors.Add(new ConnectorRow
            {
                PointId = item.id,
                Power = connItem.power,
                CableAttached = connItem.cable_attached,
                Interfaces = connItem.interfaces == null ? [] : connItem.interfaces.ToArray(),
                Ts = connectorTs
            });
        }
    }

    protected async Task ReadPoints(long[] ids)
    {
        existingPoints = new Dictionary<long, DateTime>(ids.Length);
        string sql = "SELECT id, ts FROM points WHERE id = ANY(@ids)";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("ids", ids);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            existingPoints[reader.GetInt64(0)] = reader.GetDateTime(1);
        }
    }

    protected async Task ReadStations()
    {
        existingStations = new Dictionary<long, DateTime>();
        string sql = "SELECT id, ts FROM stations";

        await using var cmd = new NpgsqlCommand(sql, conn);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            existingStations[reader.GetInt64(0)] = reader.GetDateTime(1);
        }
    }

    public async Task BulkInsertPoints()
    {
        if (newPoints.Count == 0)
        {
            return;
        }

        using var writer = await conn.BeginBinaryImportAsync(
            "COPY points (id, code, station_id, ts) FROM STDIN (FORMAT BINARY)"
            );
        foreach (var item in newPoints)
        {
            await writer.StartRowAsync();
            await writer.WriteAsync(item.Id, NpgsqlTypes.NpgsqlDbType.Bigint);
            await writer.WriteAsync(item.Code, NpgsqlTypes.NpgsqlDbType.Text);
            await writer.WriteAsync(item.StationId, NpgsqlTypes.NpgsqlDbType.Bigint);
            await writer.WriteAsync(item.Ts, NpgsqlTypes.NpgsqlDbType.Timestamp);
        }
        await writer.CompleteAsync();
    }

    public async Task BulkInsertChargingSolutions()
    {
        if (newSolutions.Count == 0) return;

        using var writer = await conn.BeginBinaryImportAsync(
            "COPY points_charging_solutions (point_id, mode, power) FROM STDIN (FORMAT BINARY)"
        );

        foreach (var item in newSolutions)
        {
            await writer.StartRowAsync();
            await writer.WriteAsync(item.PointId, NpgsqlTypes.NpgsqlDbType.Bigint);
            await writer.WriteAsync(item.Mode, NpgsqlTypes.NpgsqlDbType.Integer);
            await writer.WriteAsync(item.Power, NpgsqlTypes.NpgsqlDbType.Integer);

            chargingSolutionCountInserted++;
        }
        await writer.CompleteAsync();
    }

    public async Task BulkInsertConnectors()
    {
        if (newConnectors.Count == 0) return;

        using var writer = await conn.BeginBinaryImportAsync(
            "COPY points_connectors (point_id, power, cable_attached, interfaces, ts) FROM STDIN (FORMAT BINARY)"
        );

        foreach (var item in newConnectors)
        {
            await writer.StartRowAsync();
            await writer.WriteAsync(item.PointId, NpgsqlTypes.NpgsqlDbType.Bigint);
            await writer.WriteAsync(item.Power, NpgsqlTypes.NpgsqlDbType.Integer);
            await writer.WriteAsync(item.CableAttached, NpgsqlTypes.NpgsqlDbType.Boolean);
            await writer.WriteAsync(item.Interfaces, NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Integer);
            await writer.WriteAsync(item.Ts, NpgsqlTypes.NpgsqlDbType.Timestamp);

            connectorsCountInserted++;
            interfacesCountInserted += item.Interfaces.Count();
        }
        await writer.CompleteAsync();
    }

    private async Task ExecuteUpdatePoint(SinglePointData item, DateTime jsonTs)
    {
        string sql = "UPDATE points SET code = @code, station_id = @station_id, ts = @ts WHERE id = @id";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", item.id);
        cmd.Parameters.AddWithValue("code", item.code);
        cmd.Parameters.AddWithValue("station_id", item.station_id);
        cmd.Parameters.AddWithValue("ts", jsonTs);

        await cmd.ExecuteNonQueryAsync();
    }

    private async Task ExecuteDeleteOldSolutions(long pointId)
    {
        string sql = "DELETE FROM points_charging_solutions WHERE point_id = @pointId";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("pointId", pointId);

        chargingSolutionCountUpdated += await cmd.ExecuteNonQueryAsync();
    }

    private async Task ExecuteDeleteOldConnectors(long pointId)
    {
        string sql = "DELETE FROM points_connectors WHERE point_id = @pointId";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("pointId", pointId);

        connectorsCountUpdated += await cmd.ExecuteNonQueryAsync();
    }

    protected async Task DeactivateMissingPoints(long[] apiIds)
    {
        if (apiIds == null || apiIds.Length == 0) return;

        string sql = "UPDATE points SET active = false WHERE active = true AND NOT (id = ANY(@apiIds))";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("apiIds", apiIds);

        countDeactivated = await cmd.ExecuteNonQueryAsync();
    }
}