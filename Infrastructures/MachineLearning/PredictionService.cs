using Microsoft.Extensions.ML;

namespace Infrastructures.MachineLearning;

public class PredictionService
{
    private readonly PredictionEnginePool<ModelInput, ModelOutput> _predictionEnginePool;

    public PredictionService(PredictionEnginePool<ModelInput, ModelOutput> predictionEnginePool)
    {
        _predictionEnginePool = predictionEnginePool;
    }

    public float Predict(ModelInput input)
    {
        // Use the pooled prediction engine to make thread-safe predictions
        var prediction = _predictionEnginePool.Predict(input);
        return prediction.Score;
    }
}
