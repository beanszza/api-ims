using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ms_analytics.Models;

public class AiRecommendationDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string Id { get; set; } = "ai_recommendations";

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime LastCompiledAt { get; set; } = DateTime.UtcNow;

    public string ModelType { get; set; } = "ML.NET Linear Regression & Velocity Optimization";
    public List<ProcurementAiRecommendationDto> ProcurementRecommendations { get; set; } = new();
    public List<ProductionAiRecommendationDto> ProductionRecommendations { get; set; } = new();
}

public class ProcurementAiRecommendationDto
{
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public double CurrentStock { get; set; }
    public double PredictedMonthlyUsage { get; set; }
    public double RecommendedReorderQty { get; set; }
    public string Confidence { get; set; } = "High";
    public string RecommendationBasis { get; set; } = string.Empty;
}

public class ProductionAiRecommendationDto
{
    public int RecipeId { get; set; }
    public string RecipeName { get; set; } = string.Empty;
    public int RecommendedBatchCount { get; set; }
    public int RecommendedOutputQty { get; set; }
    public string Confidence { get; set; } = "High";
    public string RecommendationBasis { get; set; } = string.Empty;
}
