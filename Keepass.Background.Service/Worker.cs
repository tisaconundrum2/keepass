using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Keepass.Background.Service;

public class MergeWorker : IHostedService
{
    private readonly KeePassMergeService _mergeService;
    private readonly MergeOptions _options;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<MergeWorker> _logger;

    public MergeWorker(
        KeePassMergeService mergeService,
        IOptions<MergeOptions> options,
        IHostApplicationLifetime lifetime,
        ILogger<MergeWorker> logger)
    {
        _mergeService = mergeService;
        _options = options.Value;
        _lifetime = lifetime;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _lifetime.ApplicationStarted.Register(Run);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void Run()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_options.BasePath) ||
                string.IsNullOrWhiteSpace(_options.IncomingPath) ||
                string.IsNullOrWhiteSpace(_options.OutputPath) ||
                string.IsNullOrWhiteSpace(_options.Password))
            {
                _logger.LogError(
                    "Missing required configuration. Set KeePassMerge:BasePath, IncomingPath, OutputPath, and Password.");
                Environment.ExitCode = 1;
                _lifetime.StopApplication();
                return;
            }

            _logger.LogInformation(
                "Starting KeePass merge: base={Base} incoming={Incoming} output={Output}",
                _options.BasePath, _options.IncomingPath, _options.OutputPath);

            _mergeService.MergeDatabase(
                _options.BasePath, _options.IncomingPath, _options.OutputPath, _options.Password);

            _logger.LogInformation("Merge completed successfully. Output written to {Output}", _options.OutputPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "KeePass merge failed.");
            Environment.ExitCode = 1;
        }
        finally
        {
            _lifetime.StopApplication();
        }
    }
}