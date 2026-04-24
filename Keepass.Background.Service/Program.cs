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
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
