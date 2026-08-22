using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace pi.api.Additional;

public class Predictor : IDisposable
{
    private readonly InferenceSession _session;

    public class ModelInput
    {
        /// <summary>
        /// Liczba planowanych punktów ładowania (podaje użytkownik)
        /// </summary>
        public double PoolPointCount { get; set; }
        /// <summary>
        /// Liczba pojazdów BEV w danych punkcie (powiecie)
        /// </summary>
        public double BevCount { get; set; }
        /// <summary>
        /// Współrzędne punktu
        /// </summary>
        public double PoolLon { get; set; }
        /// <summary>
        /// Współrzędne punktu
        /// </summary>
        public double PoolLat { get; set; }
        /// <summary>
        /// Suma mocy punktów ładowania w kW (podaje użytkonwik)
        /// </summary>
        public double TotalPower { get; set; }
        /// <summary>
        /// Liczba POI w dane kategorii
        /// </summary>
        public double Tourism { get; set; }
        public double Amenities { get; set; }
        /// <summary>
        /// Liczba konkurencyjnych Pools w promieniu
        /// </summary>
        public double ChargingPools { get; set; }
        /// <summary>
        /// Odległość do najbliższej stacji (Pool) konkurencji [m]
        /// </summary>
        public double NearestChargingDistance { get; set; }
        /// <summary>
        /// Śrfednia cena (podaje użytkownik)
        /// </summary>
        public double AvgSessionPrice { get; set; }
    }

    public Predictor(string modelPath = "model_random_forest.onnx")
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
        //Feature Engineering
        float bevPerPoint = (float)(input.BevCount / input.PoolPointCount);
        float amenitiesPerPoint = (float)(input.Amenities / input.PoolPointCount);
        float smoothCompetitionIndex = (float)((input.ChargingPools + 1.0) / ((input.NearestChargingDistance / 1000.0) + 0.1));

        float logBevPerPoint = Log1p(bevPerPoint);
        float logAmenitiesPerPoint = Log1p(amenitiesPerPoint);
        float logSmoothCompetitionIndex = Log1p(smoothCompetitionIndex);
        float logTotalPower = Log1p((float)input.TotalPower);
        float logTourism = Log1p((float)input.Tourism);
        float logAvgSessionPrice = Log1p((float)input.AvgSessionPrice);

        //Kolejność cech:
        //['pool_point_count', 'bev_per_point', 'pool_lon', 'pool_lat', 
        //'total_power', 'tourism', 'amenities_per_point', 'smooth_competition_index', 'avg_session_price']
        float[] inputFeatures = new float[]
        {
            (float)input.PoolPointCount,
            logBevPerPoint,
            (float)input.PoolLon,
            (float)input.PoolLat,
            logTotalPower,
            logTourism,
            logAmenitiesPerPoint,
            logSmoothCompetitionIndex,
            logAvgSessionPrice
        };

        var inputTensor = new DenseTensor<float>(inputFeatures, new int[] { 1, 9 });
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("float_input", inputTensor)
        };

        //Precykcja obłożenia
        using var results = _session.Run(inputs);
        float rawPrediction = results.First().AsTensor<float>().First();

        return Math.Clamp(rawPrediction, 0.0f, 1.0f);
    }

    public void Dispose()
    {
        _session?.Dispose();
    }
}