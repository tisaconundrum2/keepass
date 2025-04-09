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

builder.Services.AddSingleton<FileSystemWatcher>();
builder.Services.AddSingleton<GitService>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
