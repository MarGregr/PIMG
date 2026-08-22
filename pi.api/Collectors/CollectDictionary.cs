using Npgsql;
using System.Text.Json;

namespace pi.api.Collectors;


public class CollectDictionary
{
    private const string filePath = @"C:\S\PI\data\dictionaries\dictionaries_20260510_200000.json";
    private readonly NpgsqlDataSource _dataSource;

    public CollectDictionary(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task LoadDictionariesAsync()
    {
        Console.WriteLine("Rozpoczynanie wczytywania słowników...");

        // 1. Odczyt i deserializacja pliku JSON
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"Błąd: Plik słownika nie istnieje pod ścieżką: {filePath}");
            return;
        }

        using var stream = File.OpenRead(filePath);
        var dicts = await JsonSerializer.DeserializeAsync<DictionaryData>(stream);
        if (dicts == null) return;

        await using var conn = await _dataSource.OpenConnectionAsync();

        //Wyczyszczenie tabel przed ponownym załadowaniem
        Console.WriteLine("Czyszczenie starych danych słownikowych...");
        string truncateSql = "TRUNCATE TABLE charging_mode, company_type, connector_interface, country, fuel_type, station_authentication_method, station_payment_method, weekday RESTART IDENTITY;";
        await using (var truncateCmd = new NpgsqlCommand(truncateSql, conn))
        {
            await truncateCmd.ExecuteNonQueryAsync();
        }

        if (dicts.charging_mode != null)
        {
            foreach (var item in dicts.charging_mode)
            {
                string sql = "INSERT INTO charging_mode (id, name) VALUES (@id, @name)";
                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("id", item.id);
                cmd.Parameters.AddWithValue("name", item.name ?? (object)DBNull.Value);
                await cmd.ExecuteNonQueryAsync();
            }
            Console.WriteLine($"Załadowano charging_mode: {dicts.charging_mode.Count} rekordów.");
        }

        if (dicts.company_type != null)
        {
            foreach (var item in dicts.company_type)
            {
                string sql = "INSERT INTO company_type (id, name) VALUES (@id, @name)";
                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("id", item.id);
                cmd.Parameters.AddWithValue("name", item.name ?? (object)DBNull.Value);
                await cmd.ExecuteNonQueryAsync();
            }
            Console.WriteLine($"Załadowano company_type: {dicts.company_type.Count} rekordów.");
        }

        if (dicts.connector_interface != null)
        {
            foreach (var item in dicts.connector_interface)
            {
                string sql = "INSERT INTO connector_interface (id, name, description) VALUES (@id, @name, @description)";
                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("id", item.id);
                cmd.Parameters.AddWithValue("name", item.name ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("description", item.description ?? (object)DBNull.Value);
                await cmd.ExecuteNonQueryAsync();
            }
            Console.WriteLine($"Załadowano connector_interface: {dicts.connector_interface.Count} rekordów.");
        }

        if (dicts.country != null)
        {
            foreach (var item in dicts.country)
            {
                string sql = "INSERT INTO country (id, name) VALUES (@id, @name)";
                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("id", item.id);
                cmd.Parameters.AddWithValue("name", item.name ?? (object)DBNull.Value);
                await cmd.ExecuteNonQueryAsync();
            }
            Console.WriteLine($"Załadowano country: {dicts.country.Count} rekordów.");
        }

        if (dicts.fuel_type != null)
        {
            foreach (var item in dicts.fuel_type)
            {
                string sql = "INSERT INTO fuel_type (id, name, description) VALUES (@id, @name, @description)";
                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("id", item.id);
                cmd.Parameters.AddWithValue("name", item.name ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("description", item.description ?? (object)DBNull.Value);
                await cmd.ExecuteNonQueryAsync();
            }
            Console.WriteLine($"Załadowano fuel_type: {dicts.fuel_type.Count} rekordów.");
        }

        if (dicts.station_authentication_method != null)
        {
            foreach (var item in dicts.station_authentication_method)
            {
                string sql = "INSERT INTO station_authentication_method (id, name) VALUES (@id, @name)";
                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("id", item.id);
                cmd.Parameters.AddWithValue("name", item.description ?? (object)DBNull.Value);
                await cmd.ExecuteNonQueryAsync();
            }
            Console.WriteLine($"Załadowano station_authentication_method: {dicts.station_authentication_method.Count} rekordów.");
        }

        if (dicts.station_payment_method != null)
        {
            foreach (var item in dicts.station_payment_method)
            {
                string sql = "INSERT INTO station_payment_method (id, name) VALUES (@id, @name)";
                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("id", item.id);
                cmd.Parameters.AddWithValue("name", item.description ?? (object)DBNull.Value);
                await cmd.ExecuteNonQueryAsync();
            }
            Console.WriteLine($"Załadowano station_payment_method: {dicts.station_payment_method.Count} rekordów.");
        }

        if (dicts.weekday != null)
        {
            foreach (var item in dicts.weekday)
            {
                string sql = "INSERT INTO weekday (id, name) VALUES (@id, @name)";
                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("id", item.id);
                cmd.Parameters.AddWithValue("name", item.name ?? (object)DBNull.Value);
                await cmd.ExecuteNonQueryAsync();
            }
            Console.WriteLine($"Załadowano weekday: {dicts.weekday.Count} rekordów.");
        }

        await conn.CloseAsync();
        Console.WriteLine("Słowniki zostały pomyślnie załadowane do bazy danych!");
    }
}