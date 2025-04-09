using Keepass.Background.Service;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "Keepass Background Service";
});

builder.Services.AddSingleton<FileSystemWatcher>();
builder.Services.AddSingleton<GitService>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
