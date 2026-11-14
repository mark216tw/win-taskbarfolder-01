using System.Security.Cryptography;
using System.Text;

namespace Mark.TaskbarFolders.Services;

public static class FlyoutInstanceGuard
{
    private static readonly object Sync = new();

    private static Mutex? _mutex;

    public static bool TryAcquire(string groupId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);

        lock (Sync)
        {
            if (_mutex is not null)
            {
                return true;
            }

            var mutex = new Mutex(initiallyOwned: true, BuildName(groupId), out var createdNew);
            if (!createdNew)
            {
                mutex.Dispose();
                return false;
            }

            _mutex = mutex;
            return true;
        }
    }

    public static void Release()
    {
        lock (Sync)
        {
            if (_mutex is null)
            {
                return;
            }

            try
            {
                _mutex.ReleaseMutex();
            }
            catch
            {
                // The mutex may not be owned if startup failed early.
            }

            _mutex.Dispose();
            _mutex = null;
        }
    }

    internal static string BuildName(string groupId)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(groupId)));
        return $@"Local\TaskbarFolders.Flyout.{hash[..16]}";
    }
}
