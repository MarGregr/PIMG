using Npgsql;

namespace pi.api.Services;

public class PowiatyService
{
    private readonly NpgsqlDataSource _dataSource;

    public PowiatyService(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<int> GetBevByLocation(double lng, double lat)
    {
        string query = @"SELECT liczba_bev, pp.kod_woj_powiat 
FROM powiaty_polygons pp INNER JOIN pojazdy_powiaty_summary pps
ON pp.kod_woj_powiat = pps.kod_woj_powiat 
WHERE ST_Contains(polygon, ST_SetSRID(ST_MakePoint(@lng, @lat), 4326))";

        await using var conn = await _dataSource.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = query;
        cmd.Parameters.AddWithValue("lng", lng);
        cmd.Parameters.AddWithValue("lat", lat);

        var favorites = new List<object>();
        await using var reader = await cmd.ExecuteReaderAsync();

        if (!reader.HasRows)
        {
            return 0;
        }

        await reader.ReadAsync();
        return reader.GetInt32(0);
    }

}
