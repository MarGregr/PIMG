using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace pi.api.Additional;

public class Predictor : IDisposable
{
    private readonly InferenceSession _session;

    public class ModelInput
    {
        /// <summary>
        /// Liczba planowanych punktów ładowania AC (podaje użytkownik)
        /// </summary>
        public double PoolPointACCount { get; set; }
        /// <summary>
        /// Liczba planowanych punktów ładowania DC (podaje użytkownik)
        /// </summary>
        public double PoolPointDCCount { get; set; }
        /// <summary>
        /// Liczba pojazdów BEV podzielona przez liczbę punktów ładowania
        /// </summary>
        public double BevCountPerPoint { get; set; }
        /// <summary>
        /// Współrzędne punktu
        /// </summary>
        public double PoolLon { get; set; }
        /// <summary>
        /// Współrzędne punktu
        /// </summary>
        public double PoolLat { get; set; }
        /// <summary>
        /// Suma mocy punktów ładowania w kW AC podzielona przez liczbę punktów ładowania (podaje użytkonwik)
        /// </summary>
        public double TotalPowerACPerPoint { get; set; }
        /// <summary>
        /// Suma mocy punktów ładowania w kW DC podzielona przez liczbę punktów ładowania (podaje użytkonwik)
        /// </summary>
        public double TotalPowerDCPerPoint { get; set; }
        /// <summary>
        /// Liczba POI w danej kategorii na punkt
        /// </summary>
        public double TourismPerPoint { get; set; }
        /// <summary>
        /// Liczba POI w danej kategorii na punkt
        /// </summary>
        public double ShopsPerPoint { get; set; }
        /// <summary>
        /// Liczba POI w danej kategorii na punkt
        /// </summary>
        public double OfficesPerPoint { get; set; }
        /// <summary>
        /// Liczba POI w danej kategorii na punkt
        /// </summary>
        public double AmenitiesPerPoint { get; set; }
        /// <summary>
        /// 1 - punkt MOP (Miejsce Obsługi Podróżnych) w odległości do 400 metrów
        /// </summary>
        public double HighwayMop { get; set; }
        /// <summary>
        /// TODO
        /// </summary>
        public double SmoothCompetitionIndex { get; set; }
        /// <summary>
        /// Śrfednia cena AC (podaje użytkownik)
        /// </summary>
        public double AvgSessionPriceAC { get; set; }
        /// <summary>
        /// Śrfednia cena DC (podaje użytkownik)
        /// </summary>
        public double AvgSessionPriceDC { get; set; }
    }

    public Predictor(string modelPath = "model_random_forest_mop.onnx")
    {
        if (!System.IO.File.Exists(modelPath))
        {
            throw new System.IO.FileNotFoundException($"Plik modelu ONNX nie został znaleziony: {modelPath}");
        }

        _session = new InferenceSession(modelPath);
    }
    private static float Log1p(float x)
    {
        return MathF.Log(1f + MathF.Max(0f, x));
    }

    public float PredictOccupancyRatio(ModelInput input)
    {
        float logBevPerPoint = Log1p((float)input.BevCountPerPoint);
        float logAmenitiesPerPoint = Log1p((float)input.AmenitiesPerPoint);
        float logShopsPerPoint = Log1p((float)input.ShopsPerPoint);
        float logOfficesPerPoint = Log1p((float)input.OfficesPerPoint);
        float logSmoothCompetitionIndex = Log1p((float)input.SmoothCompetitionIndex);
        float logTotalPowerAC = Log1p((float)input.TotalPowerACPerPoint);
        float logTotalPowerDC = Log1p((float)input.TotalPowerDCPerPoint);
        float logTourismPerPoint = Log1p((float)input.TourismPerPoint);
        float logAvgSessionPriceAC = Log1p((float)input.AvgSessionPriceAC);
        float logAvgSessionPriceDC = Log1p((float)input.AvgSessionPriceDC);
        float logHigwayMop = Log1p((float)input.HighwayMop);

        //Kolejność cech:
        //['pool_ac_point_count', 'pool_dc_point_count', 'bev_per_point', 'pool_lon', 'pool_lat', 
        //'power_ac_per_point', 'power_dc_per_point', 'tourism_per_point', 'shops_per_point', 'offices_per_point',
        //'amenities_per_point', 'highway_mop', smooth_competition_index', 'avg_ac_session_price', 'avg_dc_session_price']
        float[] inputFeatures = new float[]
        {
            (float)input.PoolPointACCount,
            (float)input.PoolPointDCCount,
            logBevPerPoint,
            (float)input.PoolLon,
            (float)input.PoolLat,
            logTotalPowerAC,
            logTotalPowerDC,
            logTourismPerPoint,
            logShopsPerPoint,
            logOfficesPerPoint,
            logAmenitiesPerPoint,
            logHigwayMop,
            logSmoothCompetitionIndex,
            logAvgSessionPriceAC,
            logAvgSessionPriceDC
        };

        var inputTensor = new DenseTensor<float>(inputFeatures, new int[] { 1, inputFeatures.Length });
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("float_input", inputTensor)
        };

        //Predykcja obłożenia
        using var results = _session.Run(inputs);
        float rawPrediction = results.First().AsTensor<float>().First();

        double predictedShare = Expm1(rawPrediction);
        predictedShare = Math.Clamp(predictedShare, 0.0f, 1.0f);
        return (float)predictedShare;
    }

    static double Expm1(double x)
    {
        if (Math.Abs(x) < 1e-5)
        {
            //Rozwinięcie Taylora: e^x - 1 = x + (x^2)/2 dla małych x
            return x + 0.5 * x * x;
        }
        return Math.Exp(x) - 1.0;
    }

    public void Dispose()
    {
        _session?.Dispose();
    }
}