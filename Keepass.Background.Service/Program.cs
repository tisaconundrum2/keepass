using Keepass.Background.Service;
using Microsoft.Extensions.Logging;

using var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
var logger = loggerFactory.CreateLogger("Program");

// Parse CLI args: --base <path> --incoming <path> --output <path> --password <pwd>
string? basePath = null, incomingPath = null, outputPath = null, password = null;
for (int i = 0; i < args.Length - 1; i++)
{
    switch (args[i])
    {
        case "--base":     basePath     = args[++i]; break;
        case "--incoming": incomingPath = args[++i]; break;
        case "--output":   outputPath   = args[++i]; break;
        case "--password": password     = args[++i]; break;
    }
}

if (basePath is null || incomingPath is null || outputPath is null || password is null)
{
    Console.Error.WriteLine("Usage: keepass-merge --base <base.kdbx> --incoming <incoming.kdbx> --output <merged.kdbx> --password <pwd>");
    return 1;
}

var mergeService = new KeePassMergeService(loggerFactory.CreateLogger<KeePassMergeService>());
var worker = new MergeWorker(mergeService, loggerFactory.CreateLogger<MergeWorker>());
return worker.Run(basePath, incomingPath, outputPath, password);
