using System;
using System.Threading.Tasks;
using api_scm.Contracts.Responses;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using ms_analytics.Models;
using ms_analytics.Services;

namespace ms_analytics.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AnalyticsController : ControllerBase
{
    private readonly IMongoDatabase _mongoDatabase;
    private readonly RedisCacheService _redisCache;
    private readonly AnalyticsCompilerService _compilerService;

    public AnalyticsController(
        IMongoDatabase mongoDatabase,
        RedisCacheService redisCache,
        AnalyticsCompilerService compilerService)
    {
        _mongoDatabase = mongoDatabase;
        _redisCache = redisCache;
        _compilerService = compilerService;
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<ApiResponse<DashboardStatsDocument>>> GetDashboardStats()
    {
        try
        {
            // 1. Check Redis Cache
            var cached = await _redisCache.GetAsync<DashboardStatsDocument>("analytics_dashboard_main");
            if (cached != null)
            {
                return Ok(ApiResponse<DashboardStatsDocument>.SuccessResponse(cached, "Dashboard analytics fetched from Redis cache"));
            }

            // 2. Tries to fetch from MongoDB
            try
            {
                var col = _mongoDatabase.GetCollection<DashboardStatsDocument>("DashboardStats");
                var doc = await col.Find(d => d.Id == "dashboard_main").FirstOrDefaultAsync();
                if (doc != null)
                {
                    await _redisCache.SetAsync("analytics_dashboard_main", doc, TimeSpan.FromSeconds(60));
                    return Ok(ApiResponse<DashboardStatsDocument>.SuccessResponse(doc, "Dashboard analytics fetched from MongoDB"));
                }
            }
            catch (Exception)
            {
                // Fallback log
            }

            // 3. Ultra-Resilient Fallback: Compute on-the-fly from PostgreSQL Read-Only
            var compiled = await _compilerService.CompileDashboardStatsAsync();
            return Ok(ApiResponse<DashboardStatsDocument>.SuccessResponse(compiled, "Dashboard analytics generated live from PostgreSQL Read-Only"));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<DashboardStatsDocument>.FailureResponse($"Error fetching dashboard analytics: {ex.Message}"));
        }
    }

    [HttpGet("ai")]
    public async Task<ActionResult<ApiResponse<AiRecommendationDocument>>> GetAiRecommendations()
    {
        try
        {
            // 1. Check Redis Cache
            var cached = await _redisCache.GetAsync<AiRecommendationDocument>("analytics_ai_recommendations");
            if (cached != null)
            {
                return Ok(ApiResponse<AiRecommendationDocument>.SuccessResponse(cached, "AI recommendations fetched from Redis cache"));
            }

            // 2. Fetch from MongoDB
            try
            {
                var col = _mongoDatabase.GetCollection<AiRecommendationDocument>("AiRecommendations");
                var doc = await col.Find(d => d.Id == "ai_recommendations").FirstOrDefaultAsync();
                if (doc != null)
                {
                    await _redisCache.SetAsync("analytics_ai_recommendations", doc, TimeSpan.FromSeconds(60));
                    return Ok(ApiResponse<AiRecommendationDocument>.SuccessResponse(doc, "AI recommendations fetched from MongoDB"));
                }
            }
            catch (Exception)
            {
                // Fallback log
            }

            // 3. Ultra-Resilient Fallback: Compute on-the-fly from ML.NET Engine
            var compiled = await _compilerService.CompileAiRecommendationsAsync();
            return Ok(ApiResponse<AiRecommendationDocument>.SuccessResponse(compiled, "AI recommendations generated live from ML.NET Engine"));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<AiRecommendationDocument>.FailureResponse($"Error fetching AI recommendations: {ex.Message}"));
        }
    }

    [HttpPost("compile")]
    public async Task<ActionResult<ApiResponse<string>>> TriggerCompilation()
    {
        try
        {
            await _compilerService.CompileAllAnalyticsAsync();
            return Ok(ApiResponse<string>.SuccessResponse("Phase 2 Analytics successfully compiled to MongoDB and cached in Redis.", "Compilation complete"));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<string>.FailureResponse($"Error compiling analytics: {ex.Message}"));
        }
    }
}
