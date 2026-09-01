using Npgsql;

namespace pi.api.Additional;

public class SummaryProcessor
{
    private readonly NpgsqlDataSource _dataSource;

    private class CountHolder
    {
        public string NazwaPowiat { get; set; }
        public int KodWojPowiat { get; set; }
        public int LiczbaBev { get; set; }
        public int LiczbaWszystkich { get; set; }
    }

    public SummaryProcessor(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<int> RefreshSummaryAsync()
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        await using var transaction = await conn.BeginTransactionAsync();

        try
        {
            var powiatyInfo = await LoadPowiatyInfoAsync(conn, transaction);
            var summaryDict = new Dictionary<int, CountHolder>();

            //Zliczanie BEV
            string sqlBevCounts = @"
                SELECT rejestracja_powiat, wojewodztwo_kod, COUNT(*)::integer 
                FROM pojazdy 
                WHERE rodzaj_paliwa = 'ENERGIA ELEKTRYCZNA' AND rejestracja_powiat IS NOT NULL
                GROUP BY rejestracja_powiat, wojewodztwo_kod;";

            await ProcessCountsAsync(
                conn,
                transaction,
                sqlBevCounts,
                powiatyInfo,
                summaryDict,
                (holder, val) => holder.LiczbaBev += val
            );

            //Zliczanie Wszystkich (BEVy oraz inne chociaż w części napędzane prądem)
            string sqlAllCounts = @"
                SELECT rejestracja_powiat, wojewodztwo_kod, COUNT(*)::integer 
                FROM pojazdy 
                WHERE (rodzaj_paliwa = 'ENERGIA ELEKTRYCZNA' 
                   OR rodzaj_pierwszego_paliwa_alternatywnego = 'ENERGIA ELEKTRYCZNA'
                   OR rodzaj_drugiego_paliwa_alternatywnego = 'ENERGIA ELEKTRYCZNA')
                   AND rejestracja_powiat IS NOT NULL
                GROUP BY rejestracja_powiat, wojewodztwo_kod;";

            await ProcessCountsAsync(
                conn,
                transaction,
                sqlAllCounts,
                powiatyInfo,
                summaryDict,
                (holder, val) => holder.LiczbaWszystkich += val
            );

            //Opróżnianie tabeli i zapis do bazy
            await new NpgsqlCommand("TRUNCATE TABLE pojazdy_powiaty_summary;", conn, transaction)
                .ExecuteNonQueryAsync();

            int insertedRows = 0;
            string insertSql = @"
                INSERT INTO pojazdy_powiaty_summary (nazwa_powiatu, kod_woj_powiat, liczba_bev, liczba_wszystkich) 
                VALUES (@nazwa, @kod, @bev, @wszystkich);";

            foreach (var kvp in summaryDict)
            {
                await using var insertCmd = new NpgsqlCommand(insertSql, conn, transaction);
                insertCmd.Parameters.AddWithValue("nazwa", kvp.Value.NazwaPowiat);

                insertCmd.Parameters.AddWithValue("kod", kvp.Value.KodWojPowiat);

                insertCmd.Parameters.AddWithValue("bev", kvp.Value.LiczbaBev);
                insertCmd.Parameters.AddWithValue("wszystkich", kvp.Value.LiczbaWszystkich);

                await insertCmd.ExecuteNonQueryAsync();
                insertedRows++;
            }

            await transaction.CommitAsync();
            return insertedRows;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private async Task ProcessCountsAsync(
        NpgsqlConnection conn,
        NpgsqlTransaction transaction,
        string sqlQuery,
        Dictionary<string, (int KodWojPowiat, string NazwaPowiat, int? KodPodrzedny, decimal Proporcja)> powiatyInfo,
        Dictionary<int, CountHolder> summaryDict,
        Action<CountHolder, int> applyValueAction)
    {
        await using var cmd = new NpgsqlCommand(sqlQuery, conn, transaction);
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            string nazwaPowiatu = reader.GetString(0).Trim();
            string wojKod = reader.GetString(1).Trim();
            int count = reader.GetInt32(2);

            if (powiatyInfo.TryGetValue(wojKod + nazwaPowiatu, out var info))
            {
                if (info.KodPodrzedny != null)
                {
                    int cntMain = (int)Math.Round(count * info.Proporcja);
                    int cntSub = count - cntMain;

                    //Szukanie nazwy powiatu podrzędnego na podstawie jego kodu
                    string? nazwaSub = powiatyInfo.FirstOrDefault(x => x.Value.KodWojPowiat == info.KodPodrzedny).Value.NazwaPowiat;

                    ApplyValue(summaryDict, nazwaPowiatu, info.KodWojPowiat, cntMain, applyValueAction);

                    if (nazwaSub != null)
                    {
                        ApplyValue(summaryDict, nazwaSub, info.KodPodrzedny.Value, cntSub, applyValueAction);
                    }
                }
                else
                {
                    ApplyValue(summaryDict, nazwaPowiatu, info.KodWojPowiat, count, applyValueAction);
                }
            }
        }
    }

    private static void ApplyValue(
        Dictionary<int, CountHolder> dict,
        string nazwaPowiat,
        int kodWojPowiat,
        int value,
        Action<CountHolder, int> applyValueAction)
    {
        if (!dict.TryGetValue(kodWojPowiat, out var holder))
        {
            holder = new CountHolder { KodWojPowiat = kodWojPowiat, NazwaPowiat = nazwaPowiat };
            dict[kodWojPowiat] = holder;
        }
        applyValueAction(holder, value);
    }

    private async Task<Dictionary<string, (int KodWojPowiat, string NazwaPowiat, int? KodPodrzedny, decimal Proporcja)>> LoadPowiatyInfoAsync(
        NpgsqlConnection conn, NpgsqlTransaction transaction)
    {
        var dict = new Dictionary<string, (int, string, int?, decimal)>(StringComparer.OrdinalIgnoreCase);

        string sql = "SELECT nazwa_powiatu, kod_woj_powiat, kod_powiat_podrzedny, proporcja FROM powiaty;";

        await using var cmd = new NpgsqlCommand(sql, conn, transaction);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            string nazwa = reader.GetString(0).Trim();
            int kodWojPowiat = reader.GetInt32(1);
            string kodWoj = kodWojPowiat.ToString("0000").Substring(0, 2);
            int? kodPodrzedny = reader.IsDBNull(2) ? null : reader.GetInt32(2);

            string propStr = reader.GetValue(3).ToString()!.Replace(',', '.');
            decimal prop = decimal.Parse(propStr, System.Globalization.CultureInfo.InvariantCulture);

            dict[kodWoj + nazwa] = (kodWojPowiat, nazwa, kodPodrzedny, prop);
        }
        return dict;
    }
}