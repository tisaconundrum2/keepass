using KeePassLib;
using KeePassLib.Interfaces;
using KeePassLib.Keys;
using KeePassLib.Serialization;
using Microsoft.Extensions.Logging;

namespace Keepass.Background.Service;

public class KeePassMergeService(ILogger<KeePassMergeService> logger)
{
    /// <summary>
    /// Opens <paramref name="basePath"/> and <paramref name="incomingPath"/> using the
    /// shared <paramref name="password"/>, merges incoming into base using the
    /// Synchronize strategy, and saves the result to <paramref name="outputPath"/>.
    /// </summary>
    public void MergeDatabase(string basePath, string incomingPath, string outputPath, string password)
    {
        var key = BuildKey(password);

        logger.LogInformation("Opening base database: {Path}", basePath);
        var baseDb = OpenDatabase(basePath, key);

        logger.LogInformation("Opening incoming database: {Path}", incomingPath);
        var incomingDb = OpenDatabase(incomingPath, key);

        logger.LogInformation("Merging with PwMergeMethod.Synchronize");
        baseDb.MergeIn(incomingDb, PwMergeMethod.Synchronize);

        logger.LogInformation("Saving merged database to: {Path}", outputPath);
        var outInfo = new IOConnectionInfo { Path = outputPath };
        baseDb.SaveAs(outInfo, true, null);

        baseDb.Close();
        incomingDb.Close();
    }

    private static CompositeKey BuildKey(string password)
    {
        var key = new CompositeKey();
        key.AddUserKey(new KcpPassword(password));
        return key;
    }

    private static PwDatabase OpenDatabase(string path, CompositeKey key)
    {
        var ioInfo = new IOConnectionInfo { Path = path };
        var db = new PwDatabase();
        db.Open(ioInfo, key, new NullStatusLogger());
        return db;
    }
}
