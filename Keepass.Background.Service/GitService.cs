using System.Diagnostics;

namespace Keepass.Background.Service
{
    public class GitService
    {
        private readonly string _repoPath;
        private readonly ILogger<GitService> _logger;
        private readonly FileSystemWatcher _fileWatcher;

        public GitService(ILogger<GitService> logger, IConfiguration configuration, FileSystemWatcher fileWatcher)
        {
            _fileWatcher = fileWatcher;
            _repoPath = configuration.GetValue<string>("RepoPath") ?? throw new ArgumentNullException("RepoPath is not set in the configuration.");
            _logger = logger;
        }

        public void InitializeFileWatcher()
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
            _fileWatcher.EnableRaisingEvents = true;
        }

        public void ExecuteAutoCommit()
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

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            // Handle file changes and trigger auto-commit if necessary
            _logger.LogInformation("File changed: {fileName}", e.FullPath);
            ExecuteAutoCommit();
        }
    }
}