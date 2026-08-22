using System;
using System.Text.Json.Serialization;

namespace CollectData.Collectors.JsonModels
{
    public class VehicleData
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }
        [JsonPropertyName("type")]
        public string Type { get; set; }
        [JsonPropertyName("attributes")]
        public VehicleAttributes Attributes { get; set; }
    }

    public class VehicleAttributes
    {
        [JsonPropertyName("marka")]
        public string Marka { get; set; }
        [JsonPropertyName("kategoria-pojazdu")]
        public string KategoriaPojazdu { get; set; }
        [JsonPropertyName("typ")]
        public string Typ { get; set; }
        [JsonPropertyName("model")]
        public string Model { get; set; }
        [JsonPropertyName("wariant")]
        public string Wariant { get; set; }
        [JsonPropertyName("wersja")]
        public string Wersja { get; set; }
        [JsonPropertyName("rodzaj-pojazdu")]
        public string RodzajPojazdu { get; set; }
        [JsonPropertyName("podrodzaj-pojazdu")]
        public string PodrodzajPojazdu { get; set; }
        [JsonPropertyName("przeznaczenie-pojazdu")]
        public string PrzeznaczeniePojazdu { get; set; }
        [JsonPropertyName("pochodzenie-pojazdu")]
        public string PochodzeniePojazdu { get; set; }
        [JsonPropertyName("rodzaj-tabliczki-znamionowej")]
        public string RodzajTabliczkiZnamionowej { get; set; }
        [JsonPropertyName("sposob-produkcji")]
        public string SposobProdukcji { get; set; }
        [JsonPropertyName("rok-produkcji")]
        public string RokProdukcji { get; set; }
        [JsonPropertyName("data-pierwszej-rejestracji-w-kraju")]
        public string DataPierwszejRejestracjiWKraju { get; set; }
        [JsonPropertyName("data-ostatniej-rejestracji-w-kraju")]
        public string DataOstatniejRejestracjiWKraju { get; set; }
        [JsonPropertyName("data-rejestracji-za-granica")]
        public string DataRejestracjiZaGranica { get; set; }
        [JsonPropertyName("pojemnosc-skokowa-silnika")]
        public double? PojemnoscSkokowaSilnika { get; set; }
        [JsonPropertyName("stosunek-mocy-silnika-do-masy-wlasnej-motocykle")]
        public decimal? StosunekMocySilnikaDoMasyWlasnejMotocykle { get; set; }
        [JsonPropertyName("moc-netto-silnika")]
        public double? MocNettoSilnika { get; set; }
        [JsonPropertyName("moc-netto-silnika-hybrydowego")]
        public double? MocNettoSilnikaHybrydowego { get; set; }
        [JsonPropertyName("masa-wlasna")]
        public int? MasaWlasna { get; set; }
        [JsonPropertyName("masa-pojazdu-gotowego-do-jazdy")]
        public int? MasaPojazduGotowegoDoJazdy { get; set; }
        [JsonPropertyName("dopuszczalna-masa-calkowita")]
        public int? DopuszczalnaMasaCalkowita { get; set; }
        [JsonPropertyName("max-masa-calkowita")]
        public int? MaxMasaCalkowita { get; set; }
        [JsonPropertyName("dopuszczalna-ladownosc")]
        public int? DopuszczalnaLadownosc { get; set; }
        [JsonPropertyName("max-ladownosc")]
        public int? MaxLadownosc { get; set; }
        [JsonPropertyName("dopuszczalna-masa-calkowita-zespolu-pojazdow")]
        public int? DopuszczalnaMasaCalkowitaZespoluPojazdow { get; set; }
        [JsonPropertyName("liczba-osi")]
        public int? LiczbaOsi { get; set; }
        [JsonPropertyName("dopuszczalny-nacisk-osi")]
        public decimal? DopuszczalnyNaciskOsi { get; set; }
        [JsonPropertyName("maksymalny-nacisk-osi")]
        public decimal? MaksymalnyNaciskOsi { get; set; }
        [JsonPropertyName("max-masa-calkowita-przyczepy-z-hamulcem")]
        public int? MaxMasaCalkowitaPrzyczepyZHamulcem { get; set; }
        [JsonPropertyName("max-masa-calkowita-przyczepy-bez-hamulca")]
        public int? MaxMasaCalkowitaPrzyczepyBezHamulca { get; set; }
        [JsonPropertyName("liczba-miejsc-ogolem")]
        public int? LiczbaMiejscOgolem { get; set; }
        [JsonPropertyName("liczba-miejsc-siedzacych")]
        public int? LiczbaMiejscSiedzacych { get; set; }
        [JsonPropertyName("liczba-miejsc-stojacych")]
        public int? LiczbaMiejscStojacych { get; set; }
        [JsonPropertyName("rodzaj-paliwa")]
        public string RodzajPaliwa { get; set; }
        [JsonPropertyName("rodzaj-pierwszego-paliwa-alternatywnego")]
        public string RodzajPierwszegoPaliwaAlternatywnego { get; set; }
        [JsonPropertyName("rodzaj-drugiego-paliwa-alternatywnego")]
        public string RodzajDrugiegoPaliwaAlternatywnego { get; set; }
        [JsonPropertyName("srednie-zuzycie-paliwa")]
        public decimal? SrednieZuzyciePaliwa { get; set; }
        [JsonPropertyName("poziom-emisji-co2")]
        public decimal? PoziomEmisjiCo2 { get; set; }
        [JsonPropertyName("rodzaj-zawieszenia")]
        public string RodzajZawieszenia { get; set; }
        [JsonPropertyName("wyposazenie-i-rodzaj-urzadzenia-radarowego")]
        public string WyposazenieIRodzajUrzadzeniaRadarowego { get; set; }
        [JsonPropertyName("hak")]
        public bool? Hak { get; set; }
        [JsonPropertyName("kierownica-po-prawej-stronie")]
        public bool? KierownicaPoPrawejStronie { get; set; }
        [JsonPropertyName("kierownica-po-prawej-stronie-pierwotnie")]
        public bool? KierownicaPoPrawejStroniePierwotnie { get; set; }
        [JsonPropertyName("katalizator-pochlaniacz")]
        public bool? KatalizatorPochlaniacz { get; set; }
        [JsonPropertyName("nazwa-producenta")]
        public string NazwaProducenta { get; set; }
        [JsonPropertyName("kod-instytutu-transaportu-samochodowego")]
        public string KodInstytutuTransportuSamochodowego { get; set; }
        [JsonPropertyName("rozstaw-kol-osi-kierowanej-pozostalych-osi")]
        public string RozstawKolOsiKierowanejPozostalychOsi { get; set; }
        [JsonPropertyName("max-rozstaw-kol")]
        public int? MaxRozstawKol { get; set; }
        [JsonPropertyName("avg-rozstaw-kol")]
        public int? AvgRozstawKol { get; set; }
        [JsonPropertyName("min-rozstaw-kol")]
        public int? MinRozstawKol { get; set; }
        [JsonPropertyName("redukcja-emisji-spalin")]
        public int? RedukcjaEmisjiSpalin { get; set; }
        [JsonPropertyName("data-pierwszej-rejestracji")]
        public string DataPierwszejRejestracji { get; set; }
        [JsonPropertyName("rodzaj-kodowania-rodzaj-podrodzaj-przeznaczenie")]
        public string RodzajKodowaniaRodzajPodrodzajPrzeznaczenie { get; set; }
        [JsonPropertyName("kod-rodzaj-podrodzaj-przeznaczenie")]
        public string KodRodzajPodrodzajPrzeznaczenie { get; set; }
        [JsonPropertyName("data-wyrejestrowania-pojazdu")]
        public string DataWyrejestrowaniaPojazdu { get; set; }
        [JsonPropertyName("przyczyna-wyrejestrowania-pojazdu")]
        public string PrzyczynaWyrejestrowaniaPojazdu { get; set; }
        [JsonPropertyName("data-wprowadzenia-danych")]
        public string DataWprowadzeniaDanych { get; set; }
        [JsonPropertyName("rejestracja-wojewodztwo")]
        public string RejestracjaWojewodztwo { get; set; }
        [JsonPropertyName("rejestracja-gmina")]
        public string RejestracjaGmina { get; set; }
        [JsonPropertyName("rejestracja-powiat")]
        public string RejestracjaPowiat { get; set; }
        [JsonPropertyName("wlasciciel-wojewodztwo")]
        public string WlascicielWojewodztwo { get; set; }
        [JsonPropertyName("wlasciciel-powiat")]
        public string WlascicielPowiat { get; set; }
        [JsonPropertyName("wlasciciel-gmina")]
        public string WlascicielGmina { get; set; }
        [JsonPropertyName("wlasciciel-wojewodztwo-kod")]
        public string WlascicielWojewodztwoKod { get; set; }
        [JsonPropertyName("wojewodztwo-kod")]
        public string WojewodztwoKod { get; set; }
        [JsonPropertyName("poziom-emisji-co2-paliwo-alternatywne-1")]
        public double? PoziomEmisjiCo2PaliwoAlternatywne1 { get; set; }
    }
    public class VehicleJson
    {
        public List<VehicleData> Data { get; set; }
        public VehicleMeta Meta { get; set; }
        public VehicleLinks Links { get; set; }
    }

    public class VehicleLinks
    {
        public string First { get; set; }
        public string Self { get; set; }
        public string Next { get; set; }
        public string Last { get; set; }
    }

    public class VehicleMeta
    {
    }
}