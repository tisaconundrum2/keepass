using Microsoft.Extensions.Logging;

namespace Keepass.Background.Service;

public class MergeWorker(
    KeePassMergeService mergeService,
    ILogger<MergeWorker> logger
)
{
    /// <summary>
    /// Runs a one-shot KeePass merge: merges <paramref name="incomingPath"/> into
    /// <paramref name="basePath"/> and writes the result to <paramref name="outputPath"/>.
    /// Returns 0 on success, 1 on failure.
    /// </summary>
    public int Run(string basePath, string incomingPath, string outputPath, string password)
    {
        try
        {
            logger.LogInformation("Starting KeePass merge: base={Base} incoming={Incoming} output={Output}",
                basePath, incomingPath, outputPath);

            mergeService.MergeDatabase(basePath, incomingPath, outputPath, password);

            logger.LogInformation("Merge completed successfully. Output written to {Output}", outputPath);
            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "KeePass merge failed.");
            return 1;
        }
    }
}