using Keepass.Background.Service;
using LibGit2Sharp;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Logging.EventLog;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "Keepass Background Service";
});

LoggerProviderOptions.RegisterProviderOptions<
    EventLogSettings, EventLogLoggerProvider>(builder.Services);

var repoPath = builder.Configuration.GetValue<string>("RepoPath") ?? throw new ArgumentNullException("RepoPath is not set in the configuration.");

builder.Services.AddSingleton(sp => new FileSystemWatcher(repoPath));
builder.Services.AddSingleton(sp => new Repository(repoPath));
builder.Services.AddSingleton<GitService>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
