using System.Diagnostics;
using LibGit2Sharp;

namespace Keepass.Background.Service
{
    public class GitService
    {
        private readonly string _repoPath;
        private readonly ILogger<GitService> _logger;
        private readonly FileSystemWatcher _fileWatcher;
        private readonly Repository _repository;

        public GitService(ILogger<GitService> logger, IConfiguration configuration, FileSystemWatcher fileWatcher, Repository repository)
        {
            _fileWatcher = fileWatcher;
            _repoPath = configuration.GetValue<string>("RepoPath") ?? throw new ArgumentNullException("RepoPath is not set in the configuration.");
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _logger = logger;
        }

        public void InitializeFileWatcher()
        {
            _fileWatcher.Path = _repoPath;
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
                Commands.Checkout(_repository, "master");
                _logger.LogInformation("Checked out to master branch.");
                var PullOptions = new PullOptions
                {
                    MergeOptions = new MergeOptions
                    {
                        FastForwardStrategy = FastForwardStrategy.Default
                    }
                };

                var signature = _repository.Config.BuildSignature(DateTimeOffset.Now);
                Commands.Pull(_repository, signature, PullOptions);

                Commands.Stage(_repository, "*");
                _logger.LogInformation("Staged all changes.");

                if (_repository.RetrieveStatus().IsDirty)
                {
                    var commitMessage = $"Auto-commit at {DateTime.Now}";
                    var commitSignature = _repository.Config.BuildSignature(DateTimeOffset.Now);
                    _repository.Commit(commitMessage, commitSignature, commitSignature);
                    _logger.LogInformation("Committed changes with message: {commitMessage}", commitMessage);

                    var remote = _repository.Network.Remotes["origin"];
                    var pushOptions = new PushOptions
                    {
                        CredentialsProvider = (url, usernameFromUrl, types) =>
                            new DefaultCredentials()
                    };
                    _repository.Network.Push(remote, "refs/heads/master", pushOptions);
                }
            }
            catch (Exception)
            {
                _logger.LogError("An error occurred while executing auto-commit.");
                throw;
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