using OS.Tooling;

internal static class ProcessRunnerLifecycleTests
{
    private static string Sentinel()
    {
        string directory = Path.Combine(HostToolProcess.FindRepositoryRoot(), "artifacts", "process-lifetime-controls", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "child.lock");
    }

    private static ProcessRequest Request(string sentinel, params string[] arguments) => new("dotnet",
        [typeof(HostTestRunner).Assembly.Location, "--runner-control", "spawn-locked-child", sentinel, .. arguments],
        HostToolProcess.FindRepositoryRoot()) { Timeout = TimeSpan.FromSeconds(10), OutputCharacterLimit = 4096 };

    private static void Reaped(string sentinel)
    {
        HostCheck.True(File.Exists(sentinel), "child actually acquired the sentinel");
        using FileStream unlocked = File.Open(sentinel, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    [HostTest("process runner reaps inherited-pipe and detached descendants after root exit")]
    private static void RootExit()
    {
        foreach (bool detached in new[] { false, true })
            foreach (int code in new[] { 0, 23 })
            {
                string sentinel = Sentinel();
                var result = ProcessRunner.RunAsync(Request(sentinel, "exit-parent", code.ToString(System.Globalization.CultureInfo.InvariantCulture), detached ? "detached" : "inherited")).GetAwaiter().GetResult();
                HostCheck.Equal(code, result.ExitCode);
                HostCheck.Equal(code == 0, result.Succeeded);
                HostCheck.True(result.TerminationConfirmed);
                HostCheck.True(!result.TimedOut && !result.Cancelled && !result.OutputLimitExceeded);
                HostCheck.Equal("parent-output", result.StandardOutput);
                HostCheck.Equal("parent-error", result.StandardError);
                Reaped(sentinel);
            }
    }

    [HostTest("process runner cancellation reaps descendants and retains separate streamed output")]
    private static void Cancellation() => CancellationAsync().GetAwaiter().GetResult();

    private static async Task CancellationAsync()
    {
        string sentinel = Sentinel();
        using CancellationTokenSource cancellation = new();
        object gate = new();
        string[] streamed = ["", ""];
        var request = Request(sentinel, "signal-and-wait") with
        {
            OutputReceived = (text, error) =>
            {
                lock (gate)
                {
                    streamed[error ? 1 : 0] += text;
                    if (streamed[0].Length != 0 && streamed[1].Length != 0) cancellation.Cancel();
                }
            }
        };
        var result = await ProcessRunner.RunAsync(request, cancellation.Token);
        HostCheck.True(result.Cancelled && !result.TimedOut && !result.Succeeded);
        HostCheck.True(result.TerminationConfirmed);
        HostCheck.Equal("parent-output", result.StandardOutput);
        HostCheck.Equal("parent-error", result.StandardError);
        HostCheck.Equal(result.StandardOutput, streamed[0]);
        HostCheck.Equal(result.StandardError, streamed[1]);
        Reaped(sentinel);
    }

    [HostTest("process runner output overflow reaps the complete descendant job")]
    private static void Overflow()
    {
        string sentinel = Sentinel();
        var result = ProcessRunner.RunAsync(Request(sentinel, "overflow")).GetAwaiter().GetResult();
        HostCheck.True(result.OutputLimitExceeded && !result.Succeeded && !result.TimedOut);
        HostCheck.True(result.TerminationConfirmed);
        HostCheck.True(result.StandardOutput.Length + result.StandardError.Length <= 4096);
        Reaped(sentinel);
    }

    [HostTest("process runner deadline reaps descendants before returning timeout")]
    private static void Deadline()
    {
        string sentinel = Sentinel();
        var result = ProcessRunner.RunAsync(Request(sentinel, "signal-and-wait") with { Timeout = TimeSpan.FromSeconds(2) }).GetAwaiter().GetResult();
        HostCheck.True(result.TimedOut && !result.Cancelled && !result.Succeeded && result.TerminationConfirmed);
        HostCheck.Equal("parent-output", result.StandardOutput);
        HostCheck.Equal("parent-error", result.StandardError);
        Reaped(sentinel);
    }

    [HostTest("process runner streaming callback failure reaps descendants before throwing")]
    private static void CallbackFailure()
    {
        string sentinel = Sentinel();
        bool rejected = false;
        try
        {
            ProcessRunner.RunAsync(Request(sentinel, "signal-and-wait") with
            {
                OutputReceived = (_, _) => throw new InvalidOperationException("controlled_stream_callback_failure")
            }).GetAwaiter().GetResult();
        }
        catch (InvalidOperationException error) when (error.Message == "controlled_stream_callback_failure") { rejected = true; }
        HostCheck.True(rejected, "callback failure cannot become success");
        Reaped(sentinel);
    }

    [HostTest("parallel process jobs do not inherit one another's pipe handles")]
    private static void IndependentPipes() => IndependentPipesAsync().GetAwaiter().GetResult();

    private static async Task IndependentPipesAsync()
    {
        string sentinel = Sentinel();
        using CancellationTokenSource cancellation = new();
        TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<ProcessResult> waiting = ProcessRunner.RunAsync(Request(sentinel, "signal-and-wait") with
        {
            OutputReceived = (_, _) => ready.TrySetResult()
        }, cancellation.Token);
        try
        {
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var quick = await ProcessRunner.RunAsync(new("dotnet",
                [typeof(HostTestRunner).Assembly.Location, "--runner-control", "echo", "separate-pipe"], HostToolProcess.FindRepositoryRoot())
                { Timeout = TimeSpan.FromSeconds(3) });
            HostCheck.True(quick.Succeeded && quick.TerminationConfirmed && !waiting.IsCompleted);
            HostCheck.True(quick.StandardOutput.Contains("separate-pipe", StringComparison.Ordinal));
        }
        finally
        {
            cancellation.Cancel();
            var stopped = await waiting;
            HostCheck.True(stopped.Cancelled && stopped.TerminationConfirmed);
            Reaped(sentinel);
        }
    }
}
