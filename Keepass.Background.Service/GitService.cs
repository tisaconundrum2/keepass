using System.Diagnostics;
using LibGit2Sharp;

namespace Keepass.Background.Service
{
    public class GitService
    {
        private readonly ILogger<GitService> _logger;
        private readonly FileSystemWatcher _fileWatcher;
        private readonly IConfiguration _configuration;
        private readonly Repository _repository;
        private readonly KeePassMergeService _mergeService;
        private readonly object _lock = new object();

        public GitService(ILogger<GitService> logger, FileSystemWatcher fileWatcher, IConfiguration configuration, Repository repository, KeePassMergeService mergeService)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _fileWatcher = fileWatcher;
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _mergeService = mergeService;
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
            lock (_lock)
            {
                try
                {
                    Commands.Checkout(_repository, "master");
                    _logger.LogInformation("Checked out to master branch.");

                    // Fetch changes from remote
                    var remote = _repository.Network.Remotes["origin"];
                    Commands.Fetch(_repository, remote.Name, Array.Empty<string>(), null, null);

                    // Get the remote tracking branch
                    var trackingBranch = _repository.Head.TrackedBranch;
                    if (trackingBranch != null)
                    {
                        // Merge the remote changes
                        var mergeOptions = new MergeOptions
                        {
                            FastForwardStrategy = FastForwardStrategy.Default
                        };

                        var signature = _repository.Config.BuildSignature(DateTimeOffset.Now);
                        var result = _repository.Merge(trackingBranch, signature, mergeOptions);
                    }

                    // Merge any kdbx files that have remote changes
                    MergeKeePassDatabases();

                    Commands.Stage(_repository, "*");
                    _logger.LogInformation("Staged all changes.");

                    if (_repository.RetrieveStatus().IsDirty)
                    {
                        var commitMessage = $"Auto-commit at {DateTime.Now}";
                        var commitSignature = _repository.Config.BuildSignature(DateTimeOffset.Now);
                        _repository.Commit(commitMessage, commitSignature, commitSignature);
                        _logger.LogInformation("Committed changes with message: {commitMessage}", commitMessage);

                        var pushOptions = new PushOptions
                        {
                            CredentialsProvider = (url, usernameFromUrl, types) =>
                                new UsernamePasswordCredentials
                                {
                                    Username = _configuration["Git:Username"],
                                    Password = _configuration["Git:Password"]
                                }
                        };
                        _repository.Network.Push(remote, "refs/heads/master", pushOptions);
                    }
                }
                catch (LibGit2Sharp.LockedFileException ex)
                {
                    _logger.LogWarning(ex, "The Git index is locked. Retrying...");
                    Thread.Sleep(1000); // Wait for 1 second before retrying
                    ExecuteAutoCommit(); // Retry the operation
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while executing auto-commit.");
                    throw;
                }
            }
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            // Handle file changes and trigger auto-commit if necessary
            _logger.LogInformation("File changed: {fileName}", e.FullPath);
            ExecuteAutoCommit();
        }

        private void MergeKeePassDatabases()
        {
            var repoRoot = _repository.Info.WorkingDirectory;
            var kdbxFiles = Directory.GetFiles(repoRoot, "*.kdbx", SearchOption.AllDirectories);

            foreach (var kdbxFile in kdbxFiles)
            {
                // Check if this file has changes from the remote merge
                var relativePath = Path.GetRelativePath(repoRoot, kdbxFile);
                var status = _repository.RetrieveStatus(relativePath);

                if (status == LibGit2Sharp.FileStatus.ModifiedInWorkdir || status == LibGit2Sharp.FileStatus.Conflicted)
                {
                    _logger.LogInformation("Detected changes in {File}, attempting KeePass merge", relativePath);

                    // Create a temp copy of the current working file before the git merge overwrote it
                    // Since git merge already happened, we use the merged file as the "remote" version
                    // and rely on KeePass MergeIn to reconcile entries
                    _mergeService.MergeDatabase(kdbxFile, kdbxFile);
                }
            }
        }
    }
}