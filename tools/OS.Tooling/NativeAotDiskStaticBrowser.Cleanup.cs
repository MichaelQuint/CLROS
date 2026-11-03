namespace OS.Tooling;

public static partial class NativeAotDiskStaticBrowser
{
    /// <summary>
    /// Await the existing owned browser cleanup. The caller retains its pending
    /// observation throw; if cleanup also fails, preserve both original exception
    /// instances. A cleanup-only failure retains its existing type and stack.
    /// </summary>
    internal static async Task RunCleanupPreservingFailureAsync(Exception? observation, Func<Task> cleanup)
    {
        try
        {
            await cleanup();
        }
        catch (Exception error) when (observation is not null)
        {
            throw new AggregateException("disk_browser_observation_and_cleanup", observation, error);
        }
    }
}
