namespace Keepass.Background.Service;

public class Worker(
    GitService gitService,
    ILogger<Worker> logger,
    IConfiguration configuration
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {

        gitService.InitializeFileWatcher();
        logger.LogWarning("File watcher initialized at: {time}", DateTimeOffset.Now);

        while (!stoppingToken.IsCancellationRequested)
        {
            logger.LogWarning("Worker running at: {time}", DateTimeOffset.Now);

            try
            {
                gitService.ExecuteAutoCommit();
                logger.LogWarning("Git operations completed successfully at: {time}", DateTimeOffset.Now);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while performing Git operations.");
            }

            await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
        }
    }
}