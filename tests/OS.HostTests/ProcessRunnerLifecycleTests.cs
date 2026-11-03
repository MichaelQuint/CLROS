using OS.Tooling;
using OS.Tooling.Native;
using OS.Tools.QemuRunner;
using System.Runtime.CompilerServices;

internal static class ProcessRunnerLifecycleTests
{
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "<JoinPendingForTest>k__BackingField")]
    private static extern ref Func<BoundedJobProcessRunner.OwnershipJoinObservation, bool>? JoinProbe(ProcessRequest request);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "<AssignmentFailureForTest>k__BackingField")]
    private static extern ref bool AssignmentFailure(ProcessRequest request);

    private static string Sentinel()
    {
        string directory = Path.Combine(HostToolProcess.FindRepositoryRoot(), "artifacts", "process-lifetime-controls", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "child.lock");
    }

    private static ProcessRequest Request(string sentinel, params string[] arguments) => new("dotnet",
        [typeof(HostTestRunner).Assembly.Location, "--runner-control", "spawn-locked-child", sentinel, .. arguments],
        HostToolProcess.FindRepositoryRoot())
    {
        Timeout = TimeSpan.FromSeconds(10),
        OutputCharacterLimit = 4096
    };

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
                    if (streamed[0].Length != 0 && streamed[1].Length != 0)
                        cancellation.Cancel();
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
        var result = ProcessRunner.RunAsync(Request(sentinel, "signal-and-wait") with
        {
            Timeout = TimeSpan.FromSeconds(2)
        }).GetAwaiter().GetResult();
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
        catch (InvalidOperationException error) when (error.Message == "controlled_stream_callback_failure")
        {
            rejected = true;
        }
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
            {
                Timeout = TimeSpan.FromSeconds(3)
            });
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

    [HostTest("retained process cancellation keeps original member handles and image leases beyond empty job")]
    private static Task RetainedCancellation() => RetainedPending("cancel");

    [HostTest("retained process timeout preserves its failure until original handles join")]
    private static Task RetainedDeadline() => RetainedPending("deadline");

    [HostTest("retained process root exit cannot upgrade uncertain descendant cleanup to success")]
    private static Task RetainedRootExit() => RetainedPending("root-exit");

    [HostTest("retained suspended process assignment failure keeps the original root until join")]
    private static Task RetainedAssignmentFailure() => RetainedPending("assignment");

    private static async Task RetainedPending(string mode)
    {
        string sentinel = Sentinel(), directory = Path.GetDirectoryName(sentinel)!;
        string boot = Path.Combine(directory, "boot.raw"), working = Path.Combine(directory, "working.raw");
        File.WriteAllBytes(boot, new byte[512]);
        File.WriteAllBytes(working, new byte[1024]);
        var lease = WritableDataImageLease.Open(boot, working);
        using CancellationTokenSource cancellation = new();
        TaskCompletionSource<BoundedJobProcessRunner.OwnershipJoinObservation> pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource afterCancellation = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int allowJoin = 0, extractions = 0, released = 0, observations = 0;
        int repeatedCancellation = 0;
        nint originalRoot = 0, originalDescendant = 0;
        bool changedHandles = false;
        object gate = new();
        string[] output = ["", ""];
        var request = (mode == "root-exit" ? Request(sentinel, "exit-parent", "0", "detached") : Request(sentinel, "signal-and-wait")) with
        {
            RetainOwnershipUntilJoined = true,
            Timeout = TimeSpan.FromSeconds(mode == "deadline" ? 2 : 20),
            OutputReceived = (text, error) =>
            {
                lock (gate)
                {
                    output[error ? 1 : 0] += text;
                    if (mode == "cancel" && output.All(value => value.Length != 0))
                        cancellation.Cancel();
                }
            }
        };
        AssignmentFailure(request) = mode == "assignment";
        JoinProbe(request) = fact =>
        {
            if (fact.HasCompleteHandleJoin)
            {
                lock (gate)
                {
                    observations++;
                    if (originalRoot == 0)
                    {
                        originalRoot = fact.RootHandle;
                        originalDescendant = fact.FirstDescendantHandle;
                    }
                    else if (originalRoot != fact.RootHandle || originalDescendant != fact.FirstDescendantHandle)
                        changedHandles = true;
                }
                if (fact.InitialWaitExpired)
                {
                    pending.TrySetResult(fact);
                    if (Volatile.Read(ref repeatedCancellation) != 0)
                        afterCancellation.TrySetResult();
                }
                // Denial-only injection: real root/member waits and actual job
                // zero were observed first; this simulates a pending member wait.
                return Volatile.Read(ref allowJoin) == 0;
            }
            return false;
        };

        async Task<ProcessResult> OwnedCall()
        {
            try
            {
                ProcessResult result = await ProcessRunner.RunAsync(request, cancellation.Token);
                lease.VerifyOriginalAndWorkingIdentity();
                using var read = new FileStream(working, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                byte[] actual = new byte[1024];
                read.ReadExactly(actual);
                HostCheck.True(actual.All(value => value == 0) && read.ReadByte() == -1);
                Interlocked.Increment(ref extractions);
                return result;
            }
            finally
            {
                lease.Dispose();
                Interlocked.Exchange(ref released, 1);
            }
        }

        Task<ProcessResult> owner = OwnedCall();
        ProcessResult? completed = null;
        try
        {
            var held = await pending.Task.WaitAsync(TimeSpan.FromSeconds(15));
            Console.WriteLine("PROCESS_JOIN_HELD mode=" + mode + " " + held);
            HostCheck.True(held.ActiveProcesses == 0 && held.CaptureComplete && held.RootSignaled && held.MembersSignaled);
            HostCheck.Equal(mode != "assignment", held.Assigned);
            if (mode == "assignment")
                HostCheck.Equal(1, held.CapturedProcesses);
            else
                HostCheck.True(held.CapturedProcesses >= 2 && held.CapturedProcesses == held.TotalProcesses,
                "all actual cumulative job members, including the locked descendant, have retained handles");
            HostCheck.True(held.RootHandle != 0 && (held.FirstDescendantHandle != 0) == (mode != "assignment"));
            HostCheck.True(!owner.IsCompleted && Volatile.Read(ref released) == 0 && Volatile.Read(ref extractions) == 0);
            Held(boot);
            Held(working);
            for (int i = 0; i < 3; i++)
                cancellation.Cancel();
            Volatile.Write(ref repeatedCancellation, 1);
            await afterCancellation.Task.WaitAsync(TimeSpan.FromSeconds(2));
            HostCheck.True(!owner.IsCompleted && Volatile.Read(ref released) == 0 && Volatile.Read(ref extractions) == 0);
            lease.VerifyOriginalAndWorkingIdentity();
            Held(boot);
            Held(working);
        }
        finally
        {
            Volatile.Write(ref allowJoin, 1);
            completed = await owner.WaitAsync(TimeSpan.FromSeconds(12));
        }
        HostCheck.True(completed.TerminationConfirmed && !completed.Succeeded && !completed.OutputLimitExceeded);
        HostCheck.True(!changedHandles && observations > 1 && Volatile.Read(ref released) == 1 && Volatile.Read(ref extractions) == 1);
        if (mode is "cancel" or "deadline")
            HostCheck.True(completed.Cancelled == (mode == "cancel") && completed.TimedOut == (mode == "deadline"));
        else
            HostCheck.True(completed.FailureReason.StartsWith(mode == "assignment" ? "JobAssignmentFailed" : "TerminationUnconfirmed", StringComparison.Ordinal));
        using (File.Open(boot, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
        }
        using (File.Open(working, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
        }
        if (mode != "assignment")
            Reaped(sentinel);

        static void Held(string path)
        {
            bool denied = false;
            try
            {
                using var write = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException)
            {
                denied = true;
            }
            HostCheck.True(denied, "original image lease remains held");
        }
    }

    [HostTest("retained process join requires complete historical capture plus root member and job receipts")]
    private static void RetainedReceiptBounds()
    {
        var complete = new BoundedJobProcessRunner.OwnershipJoinObservation(0, 2, 2, true, true, true, true, false, 1, 2);
        HostCheck.True(complete.HasCompleteHandleJoin);
        foreach (var incomplete in new[] { complete with { CaptureComplete = false }, complete with { CapturedProcesses = 1 },
            complete with { TotalProcesses = 3 }, complete with { RootSignaled = false }, complete with { MembersSignaled = false },
            complete with { ActiveProcesses = 1 } })
            HostCheck.True(!incomplete.HasCompleteHandleJoin);
        // An empty later census cannot erase an earlier missing capture.
        HostCheck.True(!(complete with
        {
            CaptureComplete = false,
            TotalProcesses = 0,
            CapturedProcesses = 0
        }).HasCompleteHandleJoin);
        HostCheck.True(!new ProcessRequest("unused", [], ".").RetainOwnershipUntilJoined);
    }
}
