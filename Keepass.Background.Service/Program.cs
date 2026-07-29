using Keepass.Background.Service;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((ctx, services) =>
    {
        services.Configure<MergeOptions>(ctx.Configuration.GetSection("KeePassMerge"));
        services.AddSingleton<KeePassMergeService>();
        services.AddHostedService<MergeWorker>();
    })
    .Build();

await host.RunAsync();
