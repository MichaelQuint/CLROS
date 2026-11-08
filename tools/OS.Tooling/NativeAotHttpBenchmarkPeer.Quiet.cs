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

    // Provisional stimulus gate only; the stopped original is checked again.
    // Quiet serving suppresses Full018 traces. Require its exact marker and
    // the application's subsequent listener message without enabling tracing.
    internal static bool IsQuietReady(string text)
    {
        Need(!text.Contains("CLROS_HTTP_BENCH_FAIL", StringComparison.Ordinal) &&
            !text.Contains("TEST_FAIL", StringComparison.Ordinal) &&
            !text.Contains("OS_SMOKE_FAIL", StringComparison.Ordinal), "quiet_guest_failure");
        const string marker = "CLROS_HTTP_BENCH_READY\r\n";
        int ready = text.IndexOf(marker, StringComparison.Ordinal);
        if (ready < 0 || ready > 0 && text[ready - 1] != '\n')
            return false;
        bool listening = false;
        using StringReader reader = new(text[(ready + marker.Length)..]);
        for (string? line; (line = reader.ReadLine()) is not null;)
        {
            Need(!line.StartsWith("M16_", StringComparison.Ordinal) &&
                !line.StartsWith("M10_018_", StringComparison.Ordinal), "quiet_trace_after_ready");
            if (line.TrimStart() == "Now listening on: http://0.0.0.0:8080")
                listening = true;
        }
        return listening;
    }
}
