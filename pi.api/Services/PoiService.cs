using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace OSMApi;

public class PoiItem
{
    public string? Name { get; set; }
    public string? PoiType1 { get; set; }
    public string? PoiType2 { get; set; }
    public double Lat { get; set; }
    public double Lon { get; set; }
    public double Distance { get; set; }
}

public class PoiService
{
    private static readonly string[] TagPriorities = new[]
    {
        "railway", "amenity", "leisure", "office", "shop", "tourism"
    };

    //HttpClient powienien być statyczny i przeznaczony do wielokrotnego użytku
    private static readonly HttpClient HttpClient = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    public PoiService()
    {
        HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("pi/1.0.0");
        HttpClient.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    }

    public async Task<List<PoiItem>> GetPois(double lng, double lat, int radius)
    {
        var stopwatch = Stopwatch.StartNew();
        Console.WriteLine($"Pobieranie POI przez Overpass API...");

        try
        {
            var elements = await FetchOsmDataAsync(lng, lat, radius);
            Console.WriteLine($"Pobrano {elements.Count} obiektów z OSM w {stopwatch.ElapsedMilliseconds} ms. Przetwarzanie...");

            var poisList = new List<PoiItem>(elements.Count);

            foreach (var elem in elements)
            {
                double elemLat = elem.Lat ?? elem.Center?.Lat ?? 0;
                double elemLon = elem.Lon ?? elem.Center?.Lon ?? 0;

                if (elemLat == 0 || elemLon == 0) continue;

                //Szybkie obliczanie odległości (Haversine zamiast ProjNet)
                double distance = Math.Round(CalculateHaversineDistance(lat, lng, elemLat, elemLon));

                if (distance > radius) continue;

                string? poiType1 = null;
                string? poiType2 = null;

                if (elem.Tags != null)
                {
                    foreach (var key in TagPriorities)
                    {
                        if (elem.Tags.TryGetValue(key, out var val) && !string.IsNullOrEmpty(val))
                        {
                            poiType1 = key;
                            poiType2 = val;
                            break;
                        }
                    }
                }

                if (poiType1 == null) continue;

                elem.Tags.TryGetValue("name", out var name);

                poisList.Add(new PoiItem
                {
                    Name = name,
                    PoiType1 = poiType1,
                    PoiType2 = poiType2,
                    Lat = Math.Round(lat, 6),
                    Lon = Math.Round(lng, 6),
                    Distance = distance
                });
            }

            //var finalPois = poisList.OrderBy(p => p.Distance).ToList();
            stopwatch.Stop();

            return poisList;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Błąd podczas wykonywania: {ex.Message}");
            throw;
        }
    }

    private async Task<List<OsmElement>> FetchOsmDataAsync(double lng, double lat, int radius)
    {
        string latStr = lat.ToString(CultureInfo.InvariantCulture);
        string lngStr = lng.ToString(CultureInfo.InvariantCulture);
        string radStr = radius.ToString(CultureInfo.InvariantCulture);

        string query = $@"[out:json][timeout:30];
(
  nwr[""railway""=""station""](around:{radStr},{latStr},{lngStr});
  nwr[""amenity""](around:{radStr},{latStr},{lngStr});
  nwr[""leisure""~""^(stadium|beach_resort|bowling_alley|sports_centre|sports_hall)$""](around:{radStr},{latStr},{lngStr});
  nwr[""office""](around:{radStr},{latStr},{lngStr});
  nwr[""shop""](around:{radStr},{latStr},{lngStr});
  nwr[""tourism""](around:{radStr},{latStr},{lngStr});
);
out center;";

        var formData = new Dictionary<string, string> { { "data", query } };
        using var content = new FormUrlEncodedContent(formData);

        string[] endpoints = new[]
        {
            "https://maps.mail.ru/osm/tools/overpass/api/interpreter",
            "https://overpass-api.de/api/interpreter",
            "https://overpass.kumi.systems/api/interpreter",
        };


        var stopwatchApi = Stopwatch.StartNew();

        foreach (var endpoint in endpoints)
        {
            Console.WriteLine($"Endpoint: {endpoint}");
            try
            {
                var response = await HttpClient.PostAsync(endpoint, content);
                if (response.IsSuccessStatusCode)
                {
                    using var jsonStream = await response.Content.ReadAsStreamAsync();
                    Console.WriteLine($"Całkowity czas wykonania pobierania danych z API: {stopwatchApi.ElapsedMilliseconds / 1000.0:F2} s");
                    var result = await JsonSerializer.DeserializeAsync<OverpassResponse>(jsonStream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    return result?.Elements ?? new List<OsmElement>();
                }
                else
                {
                    Console.WriteLine($"response: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd: {ex.Message}");
                continue;
            }
        }
        

        throw new Exception("Nie udało się pobrać danych z żadnego serwera Overpass.");
    }

    //Szybka funkcja wyliczająca dystans w metrach z punktu A do B
    private static double CalculateHaversineDistance(double lat1, double lng1, double lat2, double lng2)
    {
        const double R = 6371000; //Promień Ziemi w metrach
        double dLat = ToRadians(lat2 - lat1);
        double dLng = ToRadians(lng2 - lng1);

        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                   Math.Sin(dLng / 2) * Math.Sin(dLng / 2);

        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }

    private static double ToRadians(double angle) => (Math.PI / 180) * angle;
}

public class OverpassResponse
{
    public List<OsmElement> Elements { get; set; } = new();
}

public class OsmElement
{
    public string Type { get; set; } = "";
    public long Id { get; set; }
    public double? Lat { get; set; }
    public double? Lon { get; set; }
    public OsmCenter? Center { get; set; }
    public Dictionary<string, string>? Tags { get; set; }
}

public class OsmCenter
{
    public double Lat { get; set; }
    public double Lon { get; set; }
}