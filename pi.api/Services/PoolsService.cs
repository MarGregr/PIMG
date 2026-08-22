using Npgsql;
using System.Globalization;

namespace pi.api.Services;

public class PoolsService
{
    private readonly NpgsqlDataSource _dataSource;

    public PoolsService(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<int> PoolsInRange(double lng, double lat, int range, int myOperatorId)
    {
        string query = @$"
SELECT COUNT(*) FROM pools
WHERE active = true AND ST_Distance(location, ST_SetSRID(ST_MakePoint(@lng, @lat), 4326)::geography) <= @range AND operator_id <> @myOperatorId";

        await using var conn = await _dataSource.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = query;
        cmd.Parameters.AddWithValue("lng", lng);
        cmd.Parameters.AddWithValue("lat", lat);
        cmd.Parameters.AddWithValue("range", range);
        cmd.Parameters.AddWithValue("myOperatorId", myOperatorId);

        var favorites = new List<object>();
        await using var reader = await cmd.ExecuteReaderAsync();
        await reader.ReadAsync();
        return reader.GetInt16(0);
    }

    public async Task<double> NearestPoolDistance(double lng, double lat, int myOperatorId)
    {
        string query = @"SELECT ST_Distance(location, ST_SetSRID(ST_MakePoint(@lng, @lat), 4326)::geography) AS distance, name, operator_id 
FROM pools
WHERE active = true AND operator_id <> @myOperatorId
ORDER BY distance ASC LIMIT 1";

        await using var conn = await _dataSource.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = query;
        cmd.Parameters.AddWithValue("lng", lng);
        cmd.Parameters.AddWithValue("lat", lat);
        cmd.Parameters.AddWithValue("myOperatorId", myOperatorId);

        var favorites = new List<object>();
        await using var reader = await cmd.ExecuteReaderAsync();
        await reader.ReadAsync();
        return reader.GetDouble(0);
    }
}
