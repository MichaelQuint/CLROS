using OS.Tooling;

internal static partial class NativeAotDiskStaticBrowserTests
{
    [HostTest("browser cleanup awaits its owner and preserves observation plus receipt IO failure")]
    private static async Task CleanupFailure()
    {
        string directory = Path.Combine(HostToolProcess.FindRepositoryRoot(), "artifacts", "tests",
            "browser-cleanup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(directory, "receipt.json"));
        var original = new ToolFailure("synthetic_browser_observation");
        bool joined = false;
        AggregateException? observed = null;
        try
        {
            await NativeAotDiskStaticBrowser.RunCleanupPreservingFailureAsync(original, async () =>
            {
                await Task.Yield();
                joined = true;
                await File.WriteAllTextAsync(Path.Combine(directory, "receipt.json"), "unwritable receipt");
            });
        }
        catch (AggregateException error)
        {
            observed = error;
        }
        HostCheck.True(joined && observed is not null && observed.InnerExceptions.Count == 2 &&
            ReferenceEquals(original, observed.InnerExceptions[0]) &&
            observed.InnerExceptions[1] is IOException or UnauthorizedAccessException);
        var cleanupOnly = new IOException("synthetic_cleanup_only");
        Exception? retained = null;
        try
        {
            await NativeAotDiskStaticBrowser.RunCleanupPreservingFailureAsync(null,
                () => Task.FromException(cleanupOnly));
        }
        catch (IOException error)
        {
            retained = error;
        }
        HostCheck.True(ReferenceEquals(cleanupOnly, retained));
        bool completed = false;
        await NativeAotDiskStaticBrowser.RunCleanupPreservingFailureAsync(original, async () =>
        {
            await Task.Yield();
            completed = true;
        });
        HostCheck.True(completed);
    }
}
