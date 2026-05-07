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

                        _repository.Network.Push(remote, $"refs/heads/{currentBranch}", BuildPushOptions());
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

        /// <summary>
        /// Returns push options that use explicit credentials from config when available,
        /// otherwise delegates to the system git credential helper (Keychain on macOS,
        /// Credential Manager on Windows, libsecret on Linux).
        /// </summary>
        private PushOptions BuildPushOptions()
        {
            var username = _configuration["Git:Username"];
            var password = _configuration["Git:Password"];

            return new PushOptions
            {
                CredentialsProvider = (url, usernameFromUrl, types) =>
                {
                    if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
                        return new UsernamePasswordCredentials { Username = username, Password = password };

                    return ResolveCredentialsFromHelper(url, usernameFromUrl);
                }
            };
        }

        /// <summary>
        /// Shells out to `git credential fill` to retrieve credentials from whatever
        /// credential helper is configured on the current platform.
        /// </summary>
        private static UsernamePasswordCredentials ResolveCredentialsFromHelper(string url, string usernameHint)
        {
            var uri = new Uri(url);
            var input = $"protocol={uri.Scheme}\nhost={uri.Host}\n";
            if (!string.IsNullOrEmpty(usernameHint))
                input += $"username={usernameHint}\n";
            input += "\n";

            var psi = new ProcessStartInfo("git", "credential fill")
            {
                RedirectStandardInput  = true,
                RedirectStandardOutput = true,
                UseShellExecute        = false,
                CreateNoWindow         = true
            };

            using var proc = Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to start git credential fill.");

            proc.StandardInput.Write(input);
            proc.StandardInput.Close();

            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();

            var creds = output.Split('\n')
                .Select(l => l.Split('=', 2))
                .Where(p => p.Length == 2)
                .ToDictionary(p => p[0].Trim(), p => p[1].Trim());

            if (!creds.TryGetValue("username", out var resolvedUser) ||
                !creds.TryGetValue("password", out var resolvedPass))
                throw new InvalidOperationException(
                    "git credential fill did not return username/password. " +
                    "Configure Git:Username and Git:Password in appsettings.json, or set up a git credential helper.");

            return new UsernamePasswordCredentials { Username = resolvedUser, Password = resolvedPass };
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
                    _repository.Network.Push(remote, $"refs/heads/{currentBranch}", BuildPushOptions());
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