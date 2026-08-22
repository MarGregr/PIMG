using CollectData.Collectors.JsonModels;
using Npgsql;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;

namespace pi.api.Collectors;

internal class CollectorVehicle : CollectorBase
{
    protected HashSet<long> existingVehicleIds;

    private string vehicleApiBaseUrl;

    public CollectorVehicle(NpgsqlDataSource dataSource) : base(dataSource)
    {
    }

    public async Task<VehicleJson> GetVehicles(string url, string district)
    {
        Stopwatch sw = Stopwatch.StartNew();
        using var client = new HttpClient();
        //client.DefaultRequestHeaders.Add("User-Agent", "PBŚ Bachelor's degree project Agent");
        //Udaje przeglądarkę Edge na Windows
        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/148.0.0.0 Safari/537.36 Edg/149.0.4022.52");

        while (true)
        {
            try
            {
                var response = await client.GetAsync(url);

                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    Console.WriteLine("Przekroczono limit żądań (429). Czekam 5 sekund...");
                    await Task.Delay(TimeSpan.FromSeconds(5));
                    continue;
                }
                response.EnsureSuccessStatusCode();

                var data = await response.Content.ReadFromJsonAsync<VehicleJson>();

                //Sprawdzenie czy Links.Next jest poprawny, bo API CEPIK potrafi zwrócić zepsute linki
                string? nextUrl = data.Links?.Next;
                if (nextUrl != null && nextUrl.StartsWith($"{vehicleApiBaseUrl}?") == false)
                {
                    Console.WriteLine($"Błędny NextUrl: {nextUrl}. Czekam 10 sekund...");
                    await Task.Delay(TimeSpan.FromSeconds(10));
                    continue;
                }

                sw.Stop();
                //Czas odczytu z api jest sumowany
                apiReadTime += sw.ElapsedMilliseconds;
                return data;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd API: {ex.Message}. Odczekam 10 sekund...");
                await Task.Delay(TimeSpan.FromSeconds(10));
            }
        }
    }

    public async Task Collect(string vehicleApiUrl, string dateFrom, string dateTo)
    {
        startedAt = DateTime.UtcNow;
        source = vehicleApiUrl;
        vehicleApiBaseUrl = vehicleApiUrl;

        stopwatch = Stopwatch.StartNew();
        apiReadTime = 0;

        try
        {
            await using (conn = await _dataSource.OpenConnectionAsync())
            {
                existingVehicleIds = await GetExistingVehicles();

                var apiIds = new List<long>();

                //Województwa
                List<string> districts = ["02", "04", "06", "08", "10", "12", "14", "16", "18", "20", "22", "24", "26", "28", "30", "32"];

                foreach (var district in districts)
                {
                    string currentUrl = $"{vehicleApiUrl}?typ-daty=2&pokaz-wszystkie-pola=true&tylko-zarejestrowane=false&limit=500&data-od={dateFrom}&data-do={dateTo}&wojewodztwo={district}";

                    int pageCounter = 1;

                    while (!string.IsNullOrEmpty(currentUrl))
                    {
                        Console.Write($"Pobieranie danych z API CEPiK (Data: {dateFrom}-{dateTo}, Woj: {district}, Strona {pageCounter})... ");

                        var response = await GetVehicles(currentUrl, district);

                        if (response == null || response.Data == null)
                        {
                            Console.WriteLine($"Brak danych lub błąd pobierania z API na stronie {pageCounter}. Przerywam paginację.");
                            break;
                        }

                        Console.WriteLine($"Pobrano {response.Data.Count} rekordów, strona {pageCounter}.");

                        foreach (var item in response.Data)
                        {

                            if (item.Attributes.RodzajPaliwa != "ENERGIA ELEKTRYCZNA" && item.Attributes.RodzajPierwszegoPaliwaAlternatywnego != "ENERGIA ELEKTRYCZNA"
                                && item.Attributes.RodzajDrugiegoPaliwaAlternatywnego != "ENERGIA ELEKTRYCZNA")
                            {
                                continue;
                            }

                            countAll++;
                            if (!long.TryParse(item.Id, out long vehicleId))
                            {
                                Console.WriteLine($"Błąd parsowania ID pojazdu: {item.Id}");
                                continue;
                            }

                            apiIds.Add(vehicleId);

                            if (existingVehicleIds.Contains(vehicleId))
                            {
                                await ExecuteUpdateVehicle(vehicleId, item);
                            }
                            else
                            {
                                if (string.IsNullOrEmpty(item.Attributes.DataWyrejestrowaniaPojazdu))
                                {
                                    await ExecuteInsertVehicle(vehicleId, item);
                                }
                            }
                        }

                        //Sprawdzenie, czy w obiekcie Links (w strukturze VehicleJson) jest link do następnej strony
                        if (response.Links != null && !string.IsNullOrEmpty(response.Links.Next) && response.Links.Next != currentUrl)
                        {
                            currentUrl = response.Links.Next;
                            pageCounter++;

                            //if (currentUrl.StartsWith("https://api.cepik.gov.pl/pojazdy?") == false)
                            //{
                            //    Console.WriteLine("NIEPOPRAWNY NEXT URL");
                            //}
                        }
                        else
                        {
                            //Brak następnej strony - koniec pętli
                            currentUrl = null;
                        }
                        //Console.WriteLine($"Next_url: {currentUrl ?? "null"}");
                    }
                    Console.WriteLine("Pobrano wszystkie strony.");
                }
            }

            //Console.WriteLine($"Wstawione rekordy pojazdów: {countInserted}");
            //Console.WriteLine($"Pominięte rekordy pojazdów: {countAll - countInserted}");
            //Console.WriteLine($"Zaktualizowane rekordy pojazdów: {countUpdated}");
            //Console.WriteLine($"Usunięte rekordy pojazdów: {countDeactivated}");
            //Console.WriteLine($"Czas działania: {sw.ElapsedMilliseconds} ms");
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

    private async Task<HashSet<long>> GetExistingVehicles()
    {
        var result = new HashSet<long>();
        string sql = "SELECT id, data_wprowadzania_danych FROM pojazdy WHERE active = true";

        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            long id = reader.GetInt64(0);
            DateTime? date = reader.IsDBNull(1) ? null : reader.GetDateTime(1);
            result.Add(id);
        }

        return result;
    }

    private async Task ExecuteInsertVehicle(long id, VehicleData item)
    {
        string sql = @"
                INSERT INTO pojazdy (
                    id, type, marka, kategoria_pojazdu, typ, model, wariant, wersja, rodzaj_pojazdu, podrodzaj_pojazdu, 
                    przeznaczenie_pojazdu, pochodzenie_pojazdu, rodzaj_tabliczki_znamionowej, sposob_produkcji, rok_produkcji, 
                    data_pierwszej_rejestracji_w_kraju, data_ostatniej_rejestracji_w_kraju, data_rejestracji_za_granica, 
                    pojemnosc_skokowa_silnika, stosunek_mocy_silnika_do_masy_wlasnej_motocykle, moc_netto_silnika, 
                    moc_netto_silnika_hybrydowego, masa_wlasna, masa_pojazdu_gotowego_do_jazdy, dopuszczalna_masa_calkowita, 
                    max_masa_calkowita, dopuszczalna_ladownosc, max_ladownosc, dopuszczalna_masa_calkowita_zespolu_pojazdow, 
                    liczba_osi, dopuszczalny_nacisk_osi, maksymalny_nacisk_osi, max_masa_calkowita_przyczepy_z_hamulcem, 
                    max_masa_calkowita_przyczepy_bez_hamulca, liczba_miejsc_ogolem, liczba_miejsc_siedzacych, liczba_miejsc_stojacych, 
                    rodzaj_paliwa, rodzaj_pierwszego_paliwa_alternatywnego, rodzaj_drugiego_paliwa_alternatywnego, 
                    srednie_zuzycie_paliwa, poziom_emisji_co2, rodzaj_zawieszenia, wyposazenie_i_rodzaj_urzadzenia_radarowego, 
                    hak, kierownica_po_prawej_stronie, kierownica_po_prawej_stronie_pierwotnie, katalizator_pochlaniacz, 
                    nazwa_producenta, kod_instytutu_transaportu_samochodowego, rozstaw_kol_osi_kierowanej_pozostalych_osi, 
                    max_rozstaw_kol, avg_rozstaw_kol, min_rozstaw_kol, redukcja_emisji_spalin, data_pierwszej_rejestracji, 
                    rodzaj_kodowania_rodzaj_podrodzaj_przeznaczenie, kod_rodzaj_podrodzaj_przeznaczenie, data_wyrejestrowania_pojazdu, 
                    przyczyna_wyrejestrowania_pojazdu, data_wprowadzania_danych, rejestracja_wojewodztwo, rejestracja_gmina, 
                    rejestracja_powiat, wlasciciel_wojewodztwo, wlasciciel_powiat, wlasciciel_gmina, wlasciciel_wojewodztwo_kod, 
                    wojewodztwo_kod, poziom_emisji_co2_paliwo_alternatywne_1, active
                ) VALUES (
                    @id, @type, @marka, @kategoria_pojazdu, @typ, @model, @wariant, @wersja, @rodzaj_pojazdu, @podrodzaj_pojazdu, 
                    @przeznaczenie_pojazdu, @pochodzenie_pojazdu, @rodzaj_tabliczki_znamionowej, @sposob_produkcji, @rok_produkcji, 
                    @data_pierwszej_rejestracji_w_kraju, @data_ostatniej_rejestracji_w_kraju, @data_rejestracji_za_granica, 
                    @pojemnosc_skokowa_silnika, @stosunek_mocy_silnika_do_masy_wlasnej_motocykle, @moc_netto_silnika, 
                    @moc_netto_silnika_hybrydowego, @masa_wlasna, @masa_pojazdu_gotowego_do_jazdy, @dopuszczalna_masa_calkowita, 
                    @max_masa_calkowita, @dopuszczalna_ladownosc, @max_ladownosc, @dopuszczalna_masa_calkowita_zespolu_pojazdow, 
                    @liczba_osi, @dopuszczalny_nacisk_osi, @maksymalny_nacisk_osi, @max_masa_calkowita_przyczepy_z_hamulcem, 
                    @max_masa_calkowita_przyczepy_bez_hamulca, @liczba_miejsc_ogolem, @liczba_miejsc_siedzacych, @liczba_miejsc_stojacych, 
                    @rodzaj_paliwa, @rodzaj_pierwszego_paliwa_alternatywnego, @rodzaj_drugiego_paliwa_alternatywnego, 
                    @srednie_zuzycie_paliwa, @poziom_emisji_co2, @rodzaj_zawieszenia, @wyposazenie_i_rodzaj_urzadzenia_radarowego, 
                    @hak, @kierownica_po_prawej_stronie, @kierownica_po_prawej_stronie_pierwotnie, @katalizator_pochlaniacz, 
                    @nazwa_producenta, @kod_instytutu_transaportu_samochodowego, @rozstaw_kol_osi_kierowanej_pozostalych_osi, 
                    @max_rozstaw_kol, @avg_rozstaw_kol, @min_rozstaw_kol, @redukcja_emisji_spalin, @data_pierwszej_rejestracji, 
                    @rodzaj_kodowania_rodzaj_podrodzaj_przeznaczenie, @kod_rodzaj_podrodzaj_przeznaczenie, @data_wyrejestrowania_pojazdu, 
                    @przyczyna_wyrejestrowania_pojazdu, @data_wprowadzania_danych, @rejestracja_wojewodztwo, @rejestracja_gmina, 
                    @rejestracja_powiat, @wlasciciel_wojewodztwo, @wlasciciel_powiat, @wlasciciel_gmina, @wlasciciel_wojewodztwo_kod, 
                    @wojewodztwo_kod, @poziom_emisji_co2_paliwo_alternatywne_1, true
                );";

        await using var cmd = new NpgsqlCommand(sql, conn);
        AddVehicleParameters(cmd, id, item);

        await cmd.ExecuteNonQueryAsync();
        countInserted++;
    }

    private async Task ExecuteUpdateVehicle(long id, VehicleData item)
    {
        string sql = @"
                UPDATE pojazdy SET 
                    type = @type, marka = @marka, kategoria_pojazdu = @kategoria_pojazdu, typ = @typ, model = @model, 
                    wariant = @wariant, wersja = @wersja, rodzaj_pojazdu = @rodzaj_pojazdu, podrodzaj_pojazdu = @podrodzaj_pojazdu, 
                    przeznaczenie_pojazdu = @przeznaczenie_pojazdu, pochodzenie_pojazdu = @pochodzenie_pojazdu, 
                    rodzaj_tabliczki_znamionowej = @rodzaj_tabliczki_znamionowej, sposob_produkcji = @sposob_produkcji, 
                    rok_produkcji = @rok_produkcji, data_pierwszej_rejestracji_w_kraju = @data_pierwszej_rejestracji_w_kraju, 
                    data_ostatniej_rejestracji_w_kraju = @data_ostatniej_rejestracji_w_kraju, data_rejestracji_za_granica = @data_rejestracji_za_granica, 
                    pojemnosc_skokowa_silnika = @pojemnosc_skokowa_silnika, stosunek_mocy_silnika_do_masy_wlasnej_motocykle = @stosunek_mocy_silnika_do_masy_wlasnej_motocykle, 
                    moc_netto_silnika = @moc_netto_silnika, moc_netto_silnika_hybrydowego = @moc_netto_silnika_hybrydowego, 
                    masa_wlasna = @masa_wlasna, masa_pojazdu_gotowego_do_jazdy = @masa_pojazdu_gotowego_do_jazdy, 
                    dopuszczalna_masa_calkowita = @dopuszczalna_masa_calkowita, max_masa_calkowita = @max_masa_calkowita, 
                    dopuszczalna_ladownosc = @dopuszczalna_ladownosc, max_ladownosc = @max_ladownosc, 
                    dopuszczalna_masa_calkowita_zespolu_pojazdow = @dopuszczalna_masa_calkowita_zespolu_pojazdow, liczba_osi = @liczba_osi, 
                    dopuszczalny_nacisk_osi = @dopuszczalny_nacisk_osi, maksymalny_nacisk_osi = @maksymalny_nacisk_osi, 
                    max_masa_calkowita_przyczepy_z_hamulcem = @max_masa_calkowita_przyczepy_z_hamulcem, 
                    max_masa_calkowita_przyczepy_bez_hamulca = @max_masa_calkowita_przyczepy_bez_hamulca, 
                    liczba_miejsc_ogolem = @liczba_miejsc_ogolem, liczba_miejsc_siedzacych = @liczba_miejsc_siedzacych, 
                    liczba_miejsc_stojacych = @liczba_miejsc_stojacych, rodzaj_paliwa = @rodzaj_paliwa, 
                    rodzaj_pierwszego_paliwa_alternatywnego = @rodzaj_pierwszego_paliwa_alternatywnego, 
                    rodzaj_drugiego_paliwa_alternatywnego = @rodzaj_drugiego_paliwa_alternatywnego, srednie_zuzycie_paliwa = @srednie_zuzycie_paliwa, 
                    poziom_emisji_co2 = @poziom_emisji_co2, rodzaj_zawieszenia = @rodzaj_zawieszenia, 
                    wyposazenie_i_rodzaj_urzadzenia_radarowego = @wyposazenie_i_rodzaj_urzadzenia_radarowego, hak = @hak, 
                    kierownica_po_prawej_stronie = @kierownica_po_prawej_stronie, kierownica_po_prawej_stronie_pierwotnie = @kierownica_po_prawej_stronie_pierwotnie, 
                    katalizator_pochlaniacz = @katalizator_pochlaniacz, nazwa_producenta = @nazwa_producenta, 
                    kod_instytutu_transaportu_samochodowego = @kod_instytutu_transaportu_samochodowego, 
                    rozstaw_kol_osi_kierowanej_pozostalych_osi = @rozstaw_kol_osi_kierowanej_pozostalych_osi, max_rozstaw_kol = @max_rozstaw_kol, 
                    avg_rozstaw_kol = @avg_rozstaw_kol, min_rozstaw_kol = @min_rozstaw_kol, redukcja_emisji_spalin = @redukcja_emisji_spalin, 
                    data_pierwszej_rejestracji = @data_pierwszej_rejestracji, rodzaj_kodowania_rodzaj_podrodzaj_przeznaczenie = @rodzaj_kodowania_rodzaj_podrodzaj_przeznaczenie, 
                    kod_rodzaj_podrodzaj_przeznaczenie = @kod_rodzaj_podrodzaj_przeznaczenie, data_wyrejestrowania_pojazdu = @data_wyrejestrowania_pojazdu, 
                    przyczyna_wyrejestrowania_pojazdu = @przyczyna_wyrejestrowania_pojazdu, data_wprowadzania_danych = @data_wprowadzania_danych, 
                    rejestracja_wojewodztwo = @rejestracja_wojewodztwo, rejestracja_gmina = @rejestracja_gmina, rejestracja_powiat = @rejestracja_powiat, 
                    wlasciciel_wojewodztwo = @wlasciciel_wojewodztwo, wlasciciel_powiat = @wlasciciel_powiat, wlasciciel_gmina = @wlasciciel_gmina, 
                    wlasciciel_wojewodztwo_kod = @wlasciciel_wojewodztwo_kod, wojewodztwo_kod = @wojewodztwo_kod, 
                    poziom_emisji_co2_paliwo_alternatywne_1 = @poziom_emisji_co2_paliwo_alternatywne_1, active = @active
                WHERE id = @id;";

        await using var cmd = new NpgsqlCommand(sql, conn);
        AddVehicleParameters(cmd, id, item);

        await cmd.ExecuteNonQueryAsync();

        countUpdated++;
    }

    private void AddVehicleParameters(NpgsqlCommand cmd, long id, VehicleData item)
    {
        var attr = item.Attributes ?? new VehicleAttributes();

        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("type", item.Type ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("marka", attr.Marka ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("kategoria_pojazdu", attr.KategoriaPojazdu ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("typ", attr.Typ ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("model", attr.Model ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("wariant", attr.Wariant ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("wersja", attr.Wersja ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("rodzaj_pojazdu", attr.RodzajPojazdu ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("podrodzaj_pojazdu", attr.PodrodzajPojazdu ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("przeznaczenie_pojazdu", attr.PrzeznaczeniePojazdu ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("pochodzenie_pojazdu", attr.PochodzeniePojazdu ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("rodzaj_tabliczki_znamionowej", attr.RodzajTabliczkiZnamionowej ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("sposob_produkcji", attr.SposobProdukcji ?? (object)DBNull.Value);

        cmd.Parameters.AddWithValue("rok_produkcji", int.TryParse(attr.RokProdukcji, out int rok) ? rok : (object)DBNull.Value);

        cmd.Parameters.AddWithValue("data_pierwszej_rejestracji_w_kraju", (object)ParseDate(attr.DataPierwszejRejestracjiWKraju) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("data_ostatniej_rejestracji_w_kraju", (object)ParseDate(attr.DataOstatniejRejestracjiWKraju) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("data_rejestracji_za_granica", (object)ParseDate(attr.DataRejestracjiZaGranica) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("data_pierwszej_rejestracji", (object)ParseDate(attr.DataPierwszejRejestracji) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("data_wyrejestrowania_pojazdu", (object)ParseDate(attr.DataWyrejestrowaniaPojazdu) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("data_wprowadzania_danych", (object)ParseDate(attr.DataWprowadzeniaDanych) ?? DBNull.Value);

        cmd.Parameters.AddWithValue("pojemnosc_skokowa_silnika", attr.PojemnoscSkokowaSilnika ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("stosunek_mocy_silnika_do_masy_wlasnej_motocykle", attr.StosunekMocySilnikaDoMasyWlasnejMotocykle ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("moc_netto_silnika", attr.MocNettoSilnika ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("moc_netto_silnika_hybrydowego", attr.MocNettoSilnikaHybrydowego ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("masa_wlasna", attr.MasaWlasna ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("masa_pojazdu_gotowego_do_jazdy", attr.MasaPojazduGotowegoDoJazdy ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("dopuszczalna_masa_calkowita", attr.DopuszczalnaMasaCalkowita ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("max_masa_calkowita", attr.MaxMasaCalkowita ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("dopuszczalna_ladownosc", attr.DopuszczalnaLadownosc ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("max_ladownosc", attr.MaxLadownosc ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("dopuszczalna_masa_calkowita_zespolu_pojazdow", attr.DopuszczalnaMasaCalkowitaZespoluPojazdow ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("liczba_osi", attr.LiczbaOsi ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("dopuszczalny_nacisk_osi", attr.DopuszczalnyNaciskOsi ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("maksymalny_nacisk_osi", attr.MaksymalnyNaciskOsi ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("max_masa_calkowita_przyczepy_z_hamulcem", attr.MaxMasaCalkowitaPrzyczepyZHamulcem ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("max_masa_calkowita_przyczepy_bez_hamulca", attr.MaxMasaCalkowitaPrzyczepyBezHamulca ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("liczba_miejsc_ogolem", attr.LiczbaMiejscOgolem ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("liczba_miejsc_siedzacych", attr.LiczbaMiejscSiedzacych ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("liczba_miejsc_stojacych", attr.LiczbaMiejscStojacych ?? (object)DBNull.Value);

        cmd.Parameters.AddWithValue("rodzaj_paliwa", attr.RodzajPaliwa ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("rodzaj_pierwszego_paliwa_alternatywnego", attr.RodzajPierwszegoPaliwaAlternatywnego ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("rodzaj_drugiego_paliwa_alternatywnego", attr.RodzajDrugiegoPaliwaAlternatywnego ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("srednie_zuzycie_paliwa", attr.SrednieZuzyciePaliwa ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("poziom_emisji_co2", attr.PoziomEmisjiCo2 ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("rodzaj_zawieszenia", attr.RodzajZawieszenia ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("wyposazenie_i_rodzaj_urzadzenia_radarowego", attr.WyposazenieIRodzajUrzadzeniaRadarowego ?? (object)DBNull.Value);

        cmd.Parameters.AddWithValue("hak", attr.Hak ?? false);
        cmd.Parameters.AddWithValue("kierownica_po_prawej_stronie", attr.KierownicaPoPrawejStronie ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("kierownica_po_prawej_stronie_pierwotnie", attr.KierownicaPoPrawejStroniePierwotnie ?? false);
        cmd.Parameters.AddWithValue("katalizator_pochlaniacz", attr.KatalizatorPochlaniacz ?? false);

        cmd.Parameters.AddWithValue("nazwa_producenta", attr.NazwaProducenta ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("kod_instytutu_transaportu_samochodowego", attr.KodInstytutuTransportuSamochodowego ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("rozstaw_kol_osi_kierowanej_pozostalych_osi", attr.RozstawKolOsiKierowanejPozostalychOsi ?? (object)DBNull.Value);

        cmd.Parameters.AddWithValue("max_rozstaw_kol", attr.MaxRozstawKol ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("avg_rozstaw_kol", attr.AvgRozstawKol ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("min_rozstaw_kol", attr.MinRozstawKol ?? (object)DBNull.Value);

        cmd.Parameters.AddWithValue("redukcja_emisji_spalin", attr.RedukcjaEmisjiSpalin != null ? attr.RedukcjaEmisjiSpalin.ToString() : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("rodzaj_kodowania_rodzaj_podrodzaj_przeznaczenie", attr.RodzajKodowaniaRodzajPodrodzajPrzeznaczenie ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("kod_rodzaj_podrodzaj_przeznaczenie", attr.KodRodzajPodrodzajPrzeznaczenie ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("przyczyna_wyrejestrowania_pojazdu", attr.PrzyczynaWyrejestrowaniaPojazdu ?? (object)DBNull.Value);

        cmd.Parameters.AddWithValue("rejestracja_wojewodztwo", attr.RejestracjaWojewodztwo ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("rejestracja_gmina", attr.RejestracjaGmina ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("rejestracja_powiat", attr.RejestracjaPowiat ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("wlasciciel_wojewodztwo", attr.WlascicielWojewodztwo ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("wlasciciel_powiat", attr.WlascicielPowiat ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("wlasciciel_gmina", attr.WlascicielGmina ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("wlasciciel_wojewodztwo_kod", attr.WlascicielWojewodztwoKod ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("wojewodztwo_kod", attr.WojewodztwoKod ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("poziom_emisji_co2_paliwo_alternatywne_1", attr.PoziomEmisjiCo2PaliwoAlternatywne1 ?? (object)DBNull.Value);

        bool active = string.IsNullOrEmpty(attr.DataWyrejestrowaniaPojazdu) ? true : false;
        if (active == false) { countDeactivated++; }

        cmd.Parameters.AddWithValue("active", active);
    }
    private DateTime? ParseDate(string dateStr)
    {
        if (string.IsNullOrWhiteSpace(dateStr) || dateStr == "---")
            return null;

        if (DateTime.TryParseExact(dateStr, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDate))
        {
            return parsedDate;
        }
        return null;
    }
}