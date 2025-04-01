using Keepass.Background.Service;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton<FileSystemWatcher>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
