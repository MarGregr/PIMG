using CollectData.Collectors.JsonModels;
using CollectData.Collectors.RowModels;
using Npgsql;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;

namespace pi.api.Collectors;

internal class CollectorOperator : CollectorBase
{
    protected Dictionary<long, OperatorRow> existingOperators = new();
    protected List<OperatorRow> newOperators = [];

    public CollectorOperator(NpgsqlDataSource dataSource) : base(dataSource)
    {
    }

    public async Task<OperatorJson> GetOperatorFromApi(string operatorUrl)
    {
        using var client = new HttpClient();
        return await client.GetFromJsonAsync<OperatorJson>(operatorUrl);
    }

    public async Task Collect(string operatorUrl)
    {
        startedAt = DateTime.UtcNow;
        this.source = operatorUrl;
        stopwatch = Stopwatch.StartNew();
        var data = await GetOperatorFromApi(operatorUrl);
        apiReadTime = stopwatch.ElapsedMilliseconds;

        await Collect(data);
    }

    public async Task CollectFromFile(string filePath)
    {
        startedAt = DateTime.UtcNow;
        this.source = filePath;
        stopwatch = Stopwatch.StartNew();
        var jsonContent = File.ReadAllText(filePath);
        var data = JsonSerializer.Deserialize<OperatorJson>(jsonContent);
        apiReadTime = stopwatch.ElapsedMilliseconds;

        await Collect(data);
    }

    public async Task Collect(OperatorJson data)
    {
        try
        {
            await using (conn = await _dataSource.OpenConnectionAsync())
            {
                newOperators.Clear();

                var ids = data.data.Select(o => o.id).ToArray();
                await ReadOperators(ids);

                foreach (var item in data.data)
                {
                    await UpdateOperator(item);
                }

                await BulkInsertOperators();

                await DeactivateMissingOperators(ids);
            }

            //Console.WriteLine($"Wstawione rekordy operatorów: {countInserted}");
            //Console.WriteLine($"Pominięte rekordy operatorów: {countAll - countInserted}");
            //Console.WriteLine($"Dezaktywowane rekordy operatorów: {countDeactivated}");
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

    public async Task UpdateOperator(OperatorData item)
    {
        countAll++;

        if (existingOperators.TryGetValue(item.id, out OperatorRow opRow))
        {
            if (!CompareOperators(item, opRow))
            {
                await ExecuteUpdateOperator(item);
            }
            return;
        }

        newOperators.Add(new OperatorRow
        {
            Id = item.id,
            Code = item.code,
            Name = item.name,
            ShortName = item.short_name,
            Email = item.email,
            Phone = item.phone,
            Website = item.website,
            Type = item.type,
            Country = item.country
        });
        countInserted++;

        existingOperators[item.id] = opRow;
    }

    protected async Task ReadOperators(long[] ids)
    {
        existingOperators.Clear();

        string sql = "SELECT id, code, name, short_name, email, phone, website, type, country" +
            " FROM operators WHERE id = ANY(@ids)";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("ids", ids);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            OperatorRow row = new OperatorRow();
            row.Id = reader.GetInt64(0);
            row.Code = reader.IsDBNull(1) ? null : reader.GetString(1);
            row.Name = reader.IsDBNull(2) ? null : reader.GetString(2);
            row.ShortName = reader.IsDBNull(3) ? null : reader.GetString(3);
            row.Email = reader.IsDBNull(4) ? null : reader.GetString(4);
            row.Phone = reader.IsDBNull(5) ? null : reader.GetString(5);
            row.Website = reader.IsDBNull(6) ? null : reader.GetString(6);
            row.Type = reader.GetInt16(7);
            row.Country = reader.IsDBNull(8) ? null : reader.GetString(8);

            existingOperators[row.Id] = row;
        }
    }

    public async Task BulkInsertOperators()
    {
        if (newOperators.Count == 0)
        {
            return;
        }

        using var writer = await conn.BeginBinaryImportAsync("COPY operators (id, code, name, " +
            "short_name, email, phone, website, type, country) FROM STDIN (FORMAT BINARY)");
        foreach (var item in newOperators)
        {
            await writer.StartRowAsync();
            await writer.WriteAsync(item.Id, NpgsqlTypes.NpgsqlDbType.Integer);
            await writer.WriteAsync(item.Code, NpgsqlTypes.NpgsqlDbType.Text);
            await writer.WriteAsync(item.Name, NpgsqlTypes.NpgsqlDbType.Text);
            await writer.WriteAsync(item.ShortName, NpgsqlTypes.NpgsqlDbType.Text);
            await writer.WriteAsync(item.Email, NpgsqlTypes.NpgsqlDbType.Text);
            await writer.WriteAsync(item.Phone, NpgsqlTypes.NpgsqlDbType.Text);
            await writer.WriteAsync(item.Website, NpgsqlTypes.NpgsqlDbType.Text);
            await writer.WriteAsync(item.Type, NpgsqlTypes.NpgsqlDbType.Smallint);
            await writer.WriteAsync(item.Country, NpgsqlTypes.NpgsqlDbType.Text);
        }

        await writer.CompleteAsync();
    }

    protected async Task DeactivateMissingOperators(long[] apiIds)
    {
        if (apiIds == null || apiIds.Length == 0) return;

        string sql = "UPDATE operators SET active = false WHERE active = true AND NOT (id = ANY(@apiIds))";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("apiIds", apiIds);

        countDeactivated = await cmd.ExecuteNonQueryAsync();
    }

    private async Task ExecuteUpdateOperator(OperatorData item)
    {
        string sql = "UPDATE operators SET name = @name, code = @code, phone = @phone, email = @email, " +
                     "website = @website, short_name = @short_name, type = @type, country = @country WHERE id = @id";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", item.id);
        cmd.Parameters.AddWithValue("name", item.name);
        cmd.Parameters.AddWithValue("code", item.code ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("phone", item.phone ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("email", item.email ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("website", item.website ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("short_name", item.short_name ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("type", item.type);
        cmd.Parameters.AddWithValue("country", item.country ?? (object)DBNull.Value);

        await cmd.ExecuteNonQueryAsync();

        countUpdated++;
    }

    protected bool CompareOperators(OperatorData jsonOp, OperatorRow dbOp)
    {
        if (jsonOp.name != dbOp.Name) return false;
        if (jsonOp.code != dbOp.Code) return false;
        if (jsonOp.phone != dbOp.Phone) return false;
        if (jsonOp.email != dbOp.Email) return false;
        if (jsonOp.website != dbOp.Website) return false;
        if (jsonOp.short_name != dbOp.ShortName) return false;
        if (jsonOp.type != dbOp.Type) return false;
        if (jsonOp.country != dbOp.Country) return false;

        return true;
    }
}


