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
                    var currentBranch = _repository.Head.FriendlyName;
                    _logger.LogInformation("Running auto-commit on branch: {Branch}", currentBranch);

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

                    // Merge any kdbx files that differ from the remote tracking branch
                    if (trackingBranch != null)
                    {
                        MergeKeePassDatabasesFromRef(trackingBranch.FriendlyName);
                    }

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
                        _repository.Network.Push(remote, $"refs/heads/{currentBranch}", pushOptions);
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

        /// <summary>
        /// For each .kdbx in the working tree, extracts the same file from the given
        /// source ref (e.g. "origin/master" or "origin/schultztechnology"), writes it to
        /// a temp file, and KeePass-merges it into the local copy.
        /// </summary>
        public void MergeKeePassDatabasesFromRef(string sourceRefName)
        {
            var repoRoot = _repository.Info.WorkingDirectory;

            // Resolve the source commit/tree
            var sourceCommit = _repository.Branches[sourceRefName]?.Tip
                ?? _repository.Tags[sourceRefName]?.PeeledTarget as Commit;

            if (sourceCommit == null)
            {
                _logger.LogWarning("Source ref '{Ref}' not found in repository — skipping KeePass merge.", sourceRefName);
                return;
            }

            var kdbxFiles = Directory.GetFiles(repoRoot, "*.kdbx", SearchOption.AllDirectories);

            foreach (var localPath in kdbxFiles)
            {
                var relativePath = Path.GetRelativePath(repoRoot, localPath)
                    .Replace('\\', '/');

                // Walk the source tree to find the matching blob
                var treeEntry = sourceCommit[relativePath];
                if (treeEntry?.Target is not Blob blob)
                {
                    _logger.LogInformation("'{File}' not found in ref '{Ref}' — skipping.", relativePath, sourceRefName);
                    continue;
                }

                // Write the blob to a temp file so KeePassMergeService can open it
                var tempPath = Path.Combine(Path.GetTempPath(), $"{Path.GetFileNameWithoutExtension(localPath)}__{sourceRefName.Replace('/', '_')}.kdbx");
                try
                {
                    using (var blobStream = blob.GetContentStream())
                    using (var tempFile = File.Create(tempPath))
                    {
                        blobStream.CopyTo(tempFile);
                    }

                    _logger.LogInformation("Merging '{Ref}:{File}' into local copy.", sourceRefName, relativePath);
                    _mergeService.MergeDatabase(localPath, tempPath);
                }
                finally
                {
                    if (File.Exists(tempPath)) File.Delete(tempPath);
                }
            }
        }

        /// <summary>
        /// Fetches the remote, then merges all .kdbx files from <paramref name="sourceBranch"/>
        /// (e.g. "origin/schultztechnology") into the current working branch, then commits
        /// and pushes the result.
        /// </summary>
        public void MergeFromBranch(string sourceBranch)
        {
            lock (_lock)
            {
                _logger.LogInformation("Starting cross-branch KeePass merge from '{Branch}'", sourceBranch);

                // Fetch so the ref is up to date
                var remote = _repository.Network.Remotes["origin"];
                Commands.Fetch(_repository, remote.Name, Array.Empty<string>(), null, null);
                _logger.LogInformation("Fetched origin.");

                MergeKeePassDatabasesFromRef(sourceBranch);

                Commands.Stage(_repository, "*.kdbx");

                if (_repository.RetrieveStatus().IsDirty)
                {
                    var sig = _repository.Config.BuildSignature(DateTimeOffset.Now);
                    var msg = $"KeePass merge from {sourceBranch} at {DateTime.Now}";
                    _repository.Commit(msg, sig, sig);
                    _logger.LogInformation("Committed: {Message}", msg);

                    var currentBranch = _repository.Head.FriendlyName;
                    var pushOptions = new PushOptions
                    {
                        CredentialsProvider = (url, usernameFromUrl, types) =>
                            new UsernamePasswordCredentials
                            {
                                Username = _configuration["Git:Username"],
                                Password = _configuration["Git:Password"]
                            }
                    };
                    _repository.Network.Push(remote, $"refs/heads/{currentBranch}", pushOptions);
                    _logger.LogInformation("Pushed to origin/{Branch}", currentBranch);
                }
                else
                {
                    _logger.LogInformation("No changes after KeePass merge — nothing to commit.");
                }
            }
        }
    }
}