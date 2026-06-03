namespace JobPilot.Agent.Infrastructure;

public sealed class BrowserProfileLease : IDisposable
{
    private readonly FileStream _lockFile;
    private bool _disposed;

    private BrowserProfileLease(FileStream lockFile) => _lockFile = lockFile;

    public static BrowserProfileLease Acquire(string? profilePath = null)
    {
        var profileRoot = string.IsNullOrWhiteSpace(profilePath)
            ? AppContext.BaseDirectory
            : profilePath;
        Directory.CreateDirectory(profileRoot);
        var lockPath = Path.Combine(profileRoot, ".jobpilot-profile.lock");
        var deadline = DateTimeOffset.UtcNow.AddMinutes(5);
        Console.WriteLine("Waiting for browser profile lock...");
        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                var stream = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                Console.WriteLine("Browser profile lock acquired.");
                return new BrowserProfileLease(stream);
            }
            catch (IOException)
            {
                Thread.Sleep(500);
            }
        }
        throw new TimeoutException("Timed out waiting for the shared browser profile.");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _lockFile.Dispose();
        _disposed = true;
    }
}
