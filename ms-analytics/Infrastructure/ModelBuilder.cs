using System.IO;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace ms_analytics.Infrastructure;

// Generic input model for recommendations/predictions
public class ModelInput
{
    [LoadColumn(0)]
    public float Feature1 { get; set; }
    
    [LoadColumn(1)]
    public float Feature2 { get; set; }

    [LoadColumn(2), ColumnName("Label")]
    public float Target { get; set; }
}

public class ModelOutput
{
    [ColumnName("Score")]
    public float Score { get; set; }
}

public class ModelBuilder
{
    private readonly MLContext _mlContext;

    public ModelBuilder()
    {
        _mlContext = new MLContext(seed: 1);
    }

    public void TrainAndSaveModel(string modelSavePath)
    {
        // 1. Load Data (dummy data here for setup)
        var data = new[]
        {
            new ModelInput { Feature1 = 1.0f, Feature2 = 2.0f, Target = 3.0f },
            new ModelInput { Feature1 = 2.0f, Feature2 = 3.0f, Target = 5.0f },
            new ModelInput { Feature1 = 3.0f, Feature2 = 4.0f, Target = 7.0f }
        };
        var trainingDataView = _mlContext.Data.LoadFromEnumerable(data);

        // 2. Build Pipeline
        var pipeline = _mlContext.Transforms.Concatenate("Features", new[] { "Feature1", "Feature2" })
            .Append(_mlContext.Regression.Trainers.Sdca(labelColumnName: "Label", maximumNumberOfIterations: 100));

        // 3. Train Model
        var model = pipeline.Fit(trainingDataView);

        // 4. Save Model
        var directory = Path.GetDirectoryName(modelSavePath);
        if (directory != null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _mlContext.Model.Save(model, trainingDataView.Schema, modelSavePath);
    }
}
