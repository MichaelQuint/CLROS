namespace OS.Tooling;

public static partial class NativeAotHttpBenchmarkPeer
{
    /// <summary>Observe the existing eight-case first/warm HTTP matrix against
    /// an explicitly selected quiet Full018 serving runner. Retain all source,
    /// native, package, image and device authorities through return; this helper
    /// grants none. The client retains its180-second header/body ceiling and
    /// the existing startup/matrix/outer budgets. Every path cancels and joins
    /// clients and runner; success checks stopped serial and endpoint refusal.
    /// No browser is invoked. This deliberate-stop observation qualifies neither
    /// natural cleanup, browser, sustained service nor production performance.
    /// Invalid controls reject before runner entry; failed originals remain.</summary>
    public static Task<Completion> RunQuietAsync(string root, string serial,
        Func<int, CancellationToken, Task<ProcessResult>> runRunner,
        WebRootDiskInventory.Inspection inventory, HttpWorkloadBenchmark.Options options,
        int runnerTimeoutSeconds, CancellationToken cancellation = default) =>
        RunCoreAsync(root, serial, runRunner, inventory, options, runnerTimeoutSeconds, null, cancellation);
}
