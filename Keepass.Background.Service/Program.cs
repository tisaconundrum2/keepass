using Keepass.Background.Service;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Logging.EventLog;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "Keepass Background Service";
});

LoggerProviderOptions.RegisterProviderOptions<
    EventLogSettings, EventLogLoggerProvider>(builder.Services);

builder.Services.AddSingleton(sp => 
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var repoPath = configuration.GetValue<string>("RepoPath") ?? throw new ArgumentNullException("RepoPath is not set in the configuration.");
    return new FileSystemWatcher(repoPath);
});
builder.Services.AddSingleton<GitService>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
