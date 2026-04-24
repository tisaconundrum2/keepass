using Keepass.Background.Service;
using LibGit2Sharp;

var builder = Host.CreateApplicationBuilder(args);

if (OperatingSystem.IsWindows())
{
    builder.Services.AddWindowsService(options =>
    {
        options.ServiceName = "Keepass Background Service";
    });
}

var repoPath = builder.Configuration.GetValue<string>("RepoPath");

if (string.IsNullOrEmpty(repoPath))
{
    var discovered = Repository.Discover(AppContext.BaseDirectory);
    if (!string.IsNullOrEmpty(discovered))
    {
        repoPath = Path.GetFullPath(Path.Combine(discovered, ".."));
        Console.WriteLine($"Auto-detected repository at: {repoPath}");
    }
}

if (string.IsNullOrEmpty(repoPath))
{
    if (!Environment.UserInteractive)
    {
        throw new InvalidOperationException("RepoPath is not set in the configuration. Cannot prompt when running as a service.");
    }

    Console.Write("Enter the path to the repository: ");
    repoPath = Console.ReadLine()?.Trim().Trim('\'', '"');

    if (string.IsNullOrEmpty(repoPath))
    {
        throw new ArgumentException("RepoPath cannot be empty.");
    }
}

if (!Directory.Exists(repoPath))
{
    throw new DirectoryNotFoundException($"The configured RepoPath '{repoPath}' does not exist.");
}

builder.Services.AddSingleton(sp =>
{
    var watcher = new FileSystemWatcher(repoPath)
    {
        Filter = "*.kdbx",
        IncludeSubdirectories = true
    };
    return watcher;
});
builder.Services.AddSingleton(sp => new Repository(repoPath));
builder.Services.AddSingleton<GitService>();
builder.Services.AddSingleton<KeePassMergeService>();

var mergeBranchIndex = Array.IndexOf(args, "--merge-branch");
var mergeIndex        = Array.IndexOf(args, "--merge");

if (mergeBranchIndex >= 0)
{
    // Cross-branch KeePass merge: --merge-branch <sourceBranch>
    // Example: --merge-branch origin/schultztechnology
    if (args.Length < mergeBranchIndex + 2)
    {
        Console.Error.WriteLine("Usage: --merge-branch <sourceBranch>  (e.g. origin/schultztechnology)");
        return 1;
    }

    var sourceBranch = args[mergeBranchIndex + 1];
    var mergeBranchHost = builder.Build();
    var mergeService = mergeBranchHost.Services.GetRequiredService<KeePassMergeService>();
    var gitService   = mergeBranchHost.Services.GetRequiredService<GitService>();

    mergeService.InitializeCredentials();
    gitService.MergeFromBranch(sourceBranch);
    return 0;
}
else if (mergeIndex >= 0)
{
    // One-off file merge: --merge <localKdbx> <remoteKdbx> [--branch <branchName>]
    if (args.Length < mergeIndex + 3)
    {
        Console.Error.WriteLine("Usage: --merge <localKdbxPath> <remoteKdbxPath> [--branch <branchName>]");
        return 1;
    }

    var localKdbx  = args[mergeIndex + 1];
    var remoteKdbx = args[mergeIndex + 2];

    var branchIndex = Array.IndexOf(args, "--branch");
    string? branchName = branchIndex >= 0 && args.Length > branchIndex + 1
        ? args[branchIndex + 1]
        : null;

    var mergeHost    = builder.Build();
    var mergeService = mergeHost.Services.GetRequiredService<KeePassMergeService>();

    if (!string.IsNullOrEmpty(branchName))
    {
        var repo = mergeHost.Services.GetRequiredService<Repository>();
        var branch = repo.Branches[branchName]
            ?? throw new ArgumentException($"Branch '{branchName}' not found in repository.");
        Commands.Checkout(repo, branch);
        Console.WriteLine($"Checked out branch: {branchName}");
    }

    mergeService.InitializeCredentials();
    var success = mergeService.MergeDatabase(localKdbx, remoteKdbx);
    return success ? 0 : 1;
}
else
{
    // Normal background service mode
    builder.Services.AddHostedService<Worker>();
    var host = builder.Build();

    if (Environment.UserInteractive)
    {
        host.Services.GetRequiredService<KeePassMergeService>().InitializeCredentials();
    }

    host.Run();
    return 0;
}
