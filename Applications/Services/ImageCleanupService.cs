using Infrastructures.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Applications.Services;

public class ImageCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ImageCleanupService> _logger;
    private readonly string _uploadPath;

    public ImageCleanupService(IServiceProvider serviceProvider, ILogger<ImageCleanupService> logger, IWebHostEnvironment env)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _uploadPath = Path.Combine(env.WebRootPath ?? "wwwroot", "uploads", "production");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Image Cleanup Background Service is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupOldImagesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while cleaning up old images.");
            }

            // Run once a day
            await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
        }
    }

    private async Task CleanupOldImagesAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ScmDbContext>();

        var cutoffDate = DateTime.UtcNow.AddDays(-7);
        
        // Find batches older than 7 days that have an image URL
        var oldBatches = await context.ProductionBatches
            .Where(b => b.ProductionDate < cutoffDate && !string.IsNullOrEmpty(b.ImageUrl))
            .ToListAsync();

        if (Directory.Exists(_uploadPath))
        {
            foreach (var batch in oldBatches)
            {
                var fileName = Path.GetFileName(batch.ImageUrl);
                if (string.IsNullOrEmpty(fileName)) continue;

                var filePath = Path.Combine(_uploadPath, fileName);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    _logger.LogInformation($"Deleted old image: {filePath}");
                }

                // Clear the URL in the database
                batch.ImageUrl = string.Empty;
            }

            if (oldBatches.Any())
            {
                await context.SaveChangesAsync();
            }
        }
    }
}
