using KeePassLib;
using KeePassLib.Interfaces;
using KeePassLib.Keys;
using KeePassLib.Serialization;

namespace Keepass.Background.Service;

public class KeePassMergeService
{
    private readonly ILogger<KeePassMergeService> _logger;
    private CompositeKey? _compositeKey;

    public KeePassMergeService(ILogger<KeePassMergeService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Initializes credentials by prompting the user interactively.
    /// Must be called before any merge operations.
    /// </summary>
    public void InitializeCredentials()
    {
        if (_compositeKey != null) return;

        var key = new CompositeKey();

        Console.Write("Enter KeePass master password (leave blank if using key file only): ");
        var password = ReadPassword();
        if (!string.IsNullOrEmpty(password))
        {
            key.AddUserKey(new KcpPassword(password));
        }

        Console.Write("Enter path to key file (leave blank if using password only): ");
        var keyFilePath = Console.ReadLine()?.Trim().Trim('\'', '"');
        if (!string.IsNullOrEmpty(keyFilePath) && File.Exists(keyFilePath))
        {
            key.AddUserKey(new KcpKeyFile(keyFilePath));
        }

        if (key.UserKeyCount == 0)
        {
            throw new InvalidOperationException("At least one credential (password or key file) is required.");
        }

        _compositeKey = key;
        _logger.LogInformation("KeePass credentials initialized.");
    }

    private static string ReadPassword()
    {
        var password = new System.Text.StringBuilder();
        while (true)
        {
            var keyInfo = Console.ReadKey(intercept: true);
            if (keyInfo.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                break;
            }
            if (keyInfo.Key == ConsoleKey.Backspace && password.Length > 0)
            {
                password.Length--;
                Console.Write("\b \b");
            }
            else if (!char.IsControl(keyInfo.KeyChar))
            {
                password.Append(keyInfo.KeyChar);
                Console.Write('*');
            }
        }
        return password.ToString();
    }

    /// <summary>
    /// Merges a remote/updated kdbx file into the local kdbx file using KeePass synchronize mode.
    /// Both files must share the same master key (password/keyfile).
    /// </summary>
    public bool MergeDatabase(string localPath, string remotePath)
    {
        if (!File.Exists(localPath))
        {
            _logger.LogError("Local database not found: {Path}", localPath);
            return false;
        }

        if (!File.Exists(remotePath))
        {
            _logger.LogError("Remote database not found: {Path}", remotePath);
            return false;
        }

        try
        {
            if (_compositeKey == null)
                throw new InvalidOperationException("Credentials not initialized. Call InitializeCredentials first.");

            var compositeKey = _compositeKey;

            // Open the local database
            var localDb = new PwDatabase();
            var localIoc = IOConnectionInfo.FromPath(localPath);
            localDb.Open(localIoc, compositeKey, new NullStatusLogger());

            // Open the remote database
            var remoteDb = new PwDatabase();
            var remoteIoc = IOConnectionInfo.FromPath(remotePath);
            remoteDb.Open(remoteIoc, compositeKey, new NullStatusLogger());

            // Merge remote into local using Synchronize mode (bidirectional merge)
            localDb.MergeIn(remoteDb, PwMergeMethod.Synchronize);

            // Save the merged result
            localDb.Save(new NullStatusLogger());

            remoteDb.Close();
            localDb.Close();

            _logger.LogInformation("Successfully merged {Remote} into {Local}", remotePath, localPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to merge databases: {Local} <- {Remote}", localPath, remotePath);
            return false;
        }
    }

    private class NullStatusLogger : IStatusLogger
    {
        public bool ContinueWork() => true;
        public bool SetProgress(uint nPercent) => true;
        public bool SetText(string strNewText, LogStatusType lsType) => true;
        public void StartLogging(string strOperation, bool bWriteOperationToLog) { }
        public void EndLogging() { }
    }
}
