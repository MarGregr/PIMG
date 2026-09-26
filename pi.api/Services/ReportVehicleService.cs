using Npgsql;
using pi.api.Functions;
using System.Data.Common;

namespace pi.api.Services;

public class ReportVehicleService
{
    private readonly NpgsqlDataSource _dataSource;

    public ReportVehicleService(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<List<VehicleStatReportModel>> GetReportsVehicles(VehicleFuelType vehicleFuelType, string? voivodeship)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        string fuelTypeWhere = vehicleFuelType switch
        {
            VehicleFuelType.All => "(p.rodzaj_paliwa = 'ENERGIA ELEKTRYCZNA' OR p.rodzaj_pierwszego_paliwa_alternatywnego = 'ENERGIA ELEKTRYCZNA' OR rodzaj_drugiego_paliwa_alternatywnego = 'ENERGIA ELEKTRYCZNA')",
            VehicleFuelType.Bev => "p.rodzaj_paliwa = 'ENERGIA ELEKTRYCZNA'",
            VehicleFuelType.Hybrid => "(p.rodzaj_pierwszego_paliwa_alternatywnego = 'ENERGIA ELEKTRYCZNA' OR rodzaj_drugiego_paliwa_alternatywnego = 'ENERGIA ELEKTRYCZNA')"
        };

        string voivodeshipWhere = string.IsNullOrEmpty(voivodeship) ? string.Empty : "p.rejestracja_wojewodztwo=@voivodeship AND ";

        string query;

        query = @"
SELECT
  extract(year from data_ostatniej_rejestracji_w_kraju) as rok, 
  count(p.*) as liczba,
  case p.rodzaj_pojazdu when 'CIĄGNIK SAMOCHODOWY' THEN 'SAMOCHÓD CIĘŻAROWY' when 'SAMOCHÓD SPECJALNY' then 'SAMOCHÓD CIĘŻAROWY' when 'SAMOCHODOWY INNY' THEN 'SAMOCHÓD CIĘŻAROWY' when 'SAM.CIĘŻ. UNIWERSALNY' then 'SAMOCHÓD CIĘŻAROWY'
  else p.rodzaj_pojazdu end as rodzaj
FROM pojazdy p
WHERE " + voivodeshipWhere + fuelTypeWhere +
@" and rodzaj_pojazdu not in ('PRZYCZEPA CIĘŻAROWA', 'PRZYCZEPA CIĘŻAROWA ROLNICZA', 'PRZYCZEPA LEKKA', 'PRZYCZEPA SPECJALNA','POJAZD WOLNOBIEŻNY-KOLEJKA TURYSTYCZNA')
GROUP BY extract(year from data_ostatniej_rejestracji_w_kraju),
  case p.rodzaj_pojazdu when 'CIĄGNIK SAMOCHODOWY' THEN 'SAMOCHÓD CIĘŻAROWY' when 'SAMOCHÓD SPECJALNY' then 'SAMOCHÓD CIĘŻAROWY' when 'SAMOCHODOWY INNY' THEN 'SAMOCHÓD CIĘŻAROWY' when 'SAM.CIĘŻ. UNIWERSALNY' then 'SAMOCHÓD CIĘŻAROWY'
  else p.rodzaj_pojazdu end
ORDER by extract(year from data_ostatniej_rejestracji_w_kraju)";


        await using var cmd = new NpgsqlCommand(query, conn);

        var result = new List<VehicleStatReportModel>();

        if (string.IsNullOrEmpty(voivodeship) == false)
        {
            cmd.Parameters.AddWithValue("voivodeship", voivodeship);
        }

        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            result.Add(new VehicleStatReportModel
            {
                rok = reader.GetInt16(reader.GetOrdinal("rok")),
                rodzaj_pojazdu = reader.GetString(reader.GetOrdinal("rodzaj")),
                liczba = reader.GetInt32(reader.GetOrdinal("liczba")),
            });
        }
        return result;
    }
}
