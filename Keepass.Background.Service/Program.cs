using Keepass.Background.Service;
using NReco.Logging.File;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton<FileSystemWatcher>();
builder.Services.AddHostedService<Worker>();
builder.Services.AddLogging(loggingBuilder => {
	var loggingSection = builder.Configuration.GetSection("Logging");
	loggingBuilder.AddFile(loggingSection);
});

var host = builder.Build();
host.Run();
