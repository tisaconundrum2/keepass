using System.Diagnostics;
using System.IO;

namespace Keepass.Background.Service;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _repoPath;
    private FileSystemWatcher _fileWatcher;

    public Worker(ILogger<Worker> logger, IConfiguration configuration, FileSystemWatcher fileWatcher)
    {
        _fileWatcher = fileWatcher;
        _fileWatcher.EnableRaisingEvents = false;
        _configuration = configuration;

        _repoPath = _configuration.GetValue<string>("RepoPath") ?? throw new ArgumentNullException("RepoPath is not set in the configuration.");
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {

        InitializeFileWatcher();

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);

            try
            {
                ExecuteAutoCommit();
                _logger.LogInformation("Git operations completed successfully at: {time}", DateTimeOffset.Now);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while performing Git operations.");
            }

            await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
        }
    }

    private void InitializeFileWatcher()
    {
        _fileWatcher.NotifyFilter = NotifyFilters.Attributes
                             | NotifyFilters.CreationTime
                             | NotifyFilters.DirectoryName
                             | NotifyFilters.FileName
                             | NotifyFilters.LastAccess
                             | NotifyFilters.LastWrite
                             | NotifyFilters.Security
                             | NotifyFilters.Size;

        _fileWatcher.Changed += OnFileChanged;
        _fileWatcher.Created += OnFileChanged;
        _fileWatcher.Deleted += OnFileChanged;
        _fileWatcher.Renamed += OnFileChanged;

        _fileWatcher.Path = _repoPath;
        _fileWatcher.Filter = "*.kdbx";
        _fileWatcher.IncludeSubdirectories = true;
        _fileWatcher.EnableRaisingEvents = true;

        _logger.LogInformation("File watcher initialized for .kbdx files in {path}", _repoPath);
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        _logger.LogInformation("File change detected: {fileName}", e.FullPath);

        try
        {
            ExecuteAutoCommit();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while performing Git operations after file change.");
        }
    }

    private void ExecuteAutoCommit()
    {
        try
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "cmd",
                Arguments = "/c auto_commit.bat",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = _repoPath
            };

            using var process = new Process { StartInfo = processStartInfo };
            process.Start();

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();

            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"Command execution failed: {error}");
            }

            _logger.LogInformation("Command executed successfully: {output}", output);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while executing auto_commit.bat.");
        }
    }

}