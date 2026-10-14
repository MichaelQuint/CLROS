using System.Text.Json;
using System.Xml.Linq;
using OS.Tooling;

internal static class HostTestRunnerTests
{
    [HostTest("runner filters standard library cases without running sibling cases")]
    private static void StandardLibraryCaseFiltering()
    {
        Type[] owners = [typeof(StdArrayHelpersTests), typeof(StdAsciiByteTests), typeof(StdAsciiTests),
            typeof(StdMathTests), typeof(StdNativeIntTests), typeof(StdEndianPrimitivesTests), typeof(StdMemoryExtensionsTests)];
        foreach (Type owner in owners)
        {
            var fixture = HostTestRunner.Discover().Single(item => item.Method.DeclaringType == owner);
            string? originalFilter = HostCheck.Filter;
            TextWriter originalOutput = Console.Out;
            using StringWriter output = new();
            try
            {
                Console.SetOut(output);
                HostCheck.Filter = null;
                fixture.Method.Invoke(null, null);
                string[] cases = output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
                    .Where(line => line.StartsWith("TEST_START ", StringComparison.Ordinal)).Select(line => line["TEST_START ".Length..]).ToArray();
                HostCheck.True(cases.Length != 0, owner.Name);
                output.GetStringBuilder().Clear();
                HostCheck.Filter = cases[^1];
                int before = HostCheck.Executed;
                fixture.Method.Invoke(null, null);
                HostCheck.Equal(before + 1, HostCheck.Executed);
                HostCheck.Equal("TEST_START " + cases[^1] + Environment.NewLine + "TEST_PASS " + cases[^1] + Environment.NewLine, output.ToString());
                output.GetStringBuilder().Clear();
                HostCheck.Filter = "___unmatched_control___";
                before = HostCheck.Executed;
                fixture.Method.Invoke(null, null);
                HostCheck.Equal(before, HostCheck.Executed);
                HostCheck.Equal("", output.ToString());
            }
            finally
            {
                HostCheck.Filter = originalFilter;
                Console.SetOut(originalOutput);
            }
        }
    }

    [HostTest("runner applies case filters to every legacy leaf wrapper")]
    private static void LegacyCaseFiltering()
    {
        Type[] wrappers =
        [
            typeof(DisabledDefaultUd2VectorStateTests),
            typeof(DisabledDefaultUd2VectorStateTransitionTests),
            typeof(ExistingIn8Out8StubBytesRWPagesFinalizeTests),
            typeof(IDTRContinuedExecutionTests),
            typeof(NormalizeErrorCodeNonErrorCodeInterruptFrameTests),
            typeof(OwnedGDTTSSGeneratedAuditedCSharpStubBytesTests),
            typeof(PageZeroMappingPolicyTests),
            typeof(PopulateAllN256IDTVectorsOwnedGatesNeverTests),
            typeof(SerialMarkerStateMachineTests),
            typeof(X64Cr3LoadStubPlanTests),
            typeof(X64InvlpgStubPlanTests),
            typeof(X64PageMapTests),
            typeof(X64PagingModePreflightStubPlanTests),
            typeof(X64ProtectionProbeStubPlanTests)
        ];
        foreach (Type owner in wrappers)
        {
            var wrapper = owner.GetMethod("RunCase", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
            string? originalFilter = HostCheck.Filter;
            TextWriter originalOutput = Console.Out;
            using StringWriter output = new();
            int invoked = 0, before = HostCheck.Executed;
            object[] arguments = ["selected control", (Func<bool>)(() => { invoked++; return true; }), 0];
            string selectedOutput, omittedOutput;
            try
            {
                Console.SetOut(output);
                HostCheck.Filter = "SELECTED CONTROL";
                wrapper.Invoke(null, arguments);
                selectedOutput = output.ToString();
                output.GetStringBuilder().Clear();
                HostCheck.Filter = "___unmatched_control___";
                arguments[1] = (Func<bool>)(() => throw new InvalidOperationException("omitted case executed: " + owner.Name));
                wrapper.Invoke(null, arguments);
                omittedOutput = output.ToString();
            }
            finally
            {
                HostCheck.Filter = originalFilter;
                Console.SetOut(originalOutput);
            }
            HostCheck.Equal(1, invoked);
            HostCheck.Equal(before + 1, HostCheck.Executed);
            HostCheck.Equal(0, (int)arguments[2]);
            HostCheck.Equal("", omittedOutput);
            string[] markers = selectedOutput.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            HostCheck.Equal(2, markers.Length);
            HostCheck.True(markers[0].StartsWith("TEST_START ", StringComparison.Ordinal), owner.Name);
            HostCheck.Equal("TEST_PASS " + markers[0]["TEST_START ".Length..], markers[1]);
            HostCheck.True(markers[0].EndsWith(" selected control", StringComparison.Ordinal), owner.Name);
        }
    }

    [HostTest("runner selects a real aggregate leaf and rejects unmatched case filters")]
    private static void AggregateCaseFiltering()
    {
        const string fixture = nameof(X64Cr3LoadStubPlanTests) + "." + nameof(X64Cr3LoadStubPlanTests.RunAllM01013);
        const string selectedCase = "aggregate coordinator and marker ownership";
        var selected = Run(["--worker", fixture, "--case", selectedCase]);
        HostCheck.True(selected.Succeeded, selected.StandardOutput + selected.StandardError);
        string[] leafStarts = selected.StandardOutput.Split('\n')
            .Where(line => line.StartsWith("TEST_START m01.013.", StringComparison.Ordinal) && line.IndexOf(' ', "TEST_START m01.013.".Length) >= 0).ToArray();
        HostCheck.Equal("TEST_START m01.013.01.load " + selectedCase, leafStarts.Single().TrimEnd());

        var omitted = Run(["--worker", fixture, "--case", "___unmatched_control___"]);
        HostCheck.Equal(1, omitted.ExitCode);
        HostCheck.True(omitted.StandardOutput.Contains("case_filter_matched_no_tests", StringComparison.Ordinal));
        HostCheck.True(!omitted.StandardOutput.Split('\n').Any(line =>
            line.StartsWith("TEST_START m01.013.", StringComparison.Ordinal) && line.IndexOf(' ', "TEST_START m01.013.".Length) >= 0));
    }

    [HostTest("runner preserves unique aggregate registration and undiscovered coordinators")]
    private static void AggregateRegistration()
    {
        HostCheck.SingleFixture(typeof(DiagnosticsTests), nameof(DiagnosticsTests.RunAll));
        HostCheck.SingleFixture(typeof(M01OwnedCr3CompositeTransitionTests), nameof(M01OwnedCr3CompositeTransitionTests.RunAll));
        HostCheck.SingleFixture(typeof(X64Cr3LoadStubPlanTests), nameof(X64Cr3LoadStubPlanTests.RunAllM01013),
            typeof(X64PagingModePreflightStubPlanTests), typeof(X64ReversibleCr3TransitionStubPlanTests));
        foreach (Action invalid in new Action[]
        {
            () => HostCheck.SingleFixture(typeof(DiagnosticsTests), "missing aggregate"),
            () => HostCheck.SingleFixture(typeof(X64PagingModePreflightStubPlanTests), "unregistered coordinator"),
            () => HostCheck.SingleFixture(typeof(DiagnosticsTests), nameof(DiagnosticsTests.RunAll), typeof(M01OwnedCr3CompositeTransitionTests))
        })
        {
            bool rejected = false;
            try { invalid(); } catch (InvalidOperationException) { rejected = true; }
            HostCheck.True(rejected, "invalid compiled registration must be rejected");
        }
    }

    [HostTest("runner cancels later native work and drains active workers when cleanup is unconfirmed")]
    private static void UnconfirmedTermination() => UnconfirmedTerminationAsync().GetAwaiter().GetResult();

    private static async Task UnconfirmedTerminationAsync()
    {
        using CancellationTokenSource scheduling = new();
        TaskCompletionSource bothStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource cancellationObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseOther = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int started = 0, drained = 0;
        var confirmedFailure = new ProcessResult(-1, "", "", false, false, false, TimeSpan.Zero)
            { FailureReason = "CaptureFailed controlled" };
        HostCheck.Equal("CaptureFailed controlled", HostTestRunner.WorkerProcessFailure(confirmedFailure, scheduling));
        HostCheck.True(!scheduling.IsCancellationRequested, "ordinary confirmed failures remain independent");
        using var registration = scheduling.Token.Register(() => cancellationObserved.TrySetResult());
        Task run = HostWorkerScheduler.RunAsync(new[] { 0, 1, 2, 3 }, item => item == 2, 2, async (_, item) =>
        {
            if (Interlocked.Increment(ref started) == 2) bothStarted.TrySetResult();
            await bothStarted.Task;
            if (item == 0)
            {
                string? reason = HostTestRunner.WorkerProcessFailure(confirmedFailure with
                    { TerminationConfirmed = false, FailureReason = "TerminationUnconfirmed controlled" }, scheduling);
                HostCheck.Equal("termination_unconfirmed TerminationUnconfirmed controlled", reason);
            }
            else
            {
                await cancellationObserved.Task;
                await releaseOther.Task;
                Interlocked.Increment(ref drained);
            }
        }, scheduling.Token);
        await cancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(10));
        HostCheck.True(!run.IsCompleted, "active worker cleanup must finish before scheduling returns");
        releaseOther.TrySetResult();
        await run.WaitAsync(TimeSpan.FromSeconds(10));
        HostCheck.Equal(2, started);
        HostCheck.Equal(1, drained);
    }

    [HostTest("runner drains active workers after a scheduler callback fails")]
    private static void SchedulingFailure() => SchedulingFailureAsync().GetAwaiter().GetResult();

    private static async Task SchedulingFailureAsync()
    {
        TaskCompletionSource bothStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseSecond = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? firstWorker = null;
        int started = 0, finished = 0;
        Task run = HostWorkerScheduler.RunAsync(new[] { 0, 1, 2 }, _ => false, 2, (_, item) =>
        {
            Task worker = Worker(item);
            if (item == 0) firstWorker = worker;
            return worker;
        });
        async Task Worker(int item)
        {
            if (Interlocked.Increment(ref started) == 2) bothStarted.SetResult();
            await bothStarted.Task;
            if (item == 0) throw new IOException("controlled receipt failure");
            await releaseSecond.Task;
            Interlocked.Increment(ref finished);
        }
        // Observe the actual fault before releasing the sibling. A signal just
        // before throw lets that sibling finish first and legitimately admit #3.
        try { await firstWorker!.WaitAsync(TimeSpan.FromSeconds(10)); throw new InvalidOperationException("worker did not fail"); }
        catch (IOException error) when (error.Message == "controlled receipt failure") { }
        HostCheck.True(!run.IsCompleted, "scheduler must retain ownership of the second worker");
        releaseSecond.SetResult();
        try { await run; throw new InvalidOperationException("scheduler swallowed the failure"); }
        catch (IOException error) when (error.Message == "controlled receipt failure") { }
        HostCheck.Equal(2, started);
        HostCheck.Equal(1, finished);
    }

    [HostTest("runner embedded inputs exclude unrelated generated trees and retain app configuration")]
    private static void EmbeddedSourceInventory()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        using Stream stream = typeof(HostBuildInputs).Assembly.GetManifestResourceStream("host-test-inputs.txt")
            ?? throw new InvalidOperationException("build input manifest missing");
        using StreamReader reader = new(stream);
        HashSet<string> paths = new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        while (reader.ReadLine() is { } line)
        {
            int delimiter = line.LastIndexOf('|');
            HostCheck.True(delimiter > 0, line);
            string path = Path.GetFullPath(line[..delimiter]);
            paths.Add(path);
            string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            bool generated = relative.Split('/').Any(part => part is "obj" or "bin" or "artifacts");
            if (!generated) continue;
            // Reference DLLs and this project's actual generated Compile inputs
            // remain build dependencies. Other projects' caches are not sources.
            bool compilerInput = relative.StartsWith("tests/OS.HostTests/obj/", StringComparison.Ordinal)
                && Path.GetExtension(path) == ".cs";
            HostCheck.True(compilerInput || Path.GetExtension(path).Equals(".dll", StringComparison.OrdinalIgnoreCase), path);
        }
        foreach (string path in HostBuildInputs.SourceInputs(root))
            HostCheck.True(paths.Contains(Path.GetFullPath(path)), "source omitted: " + path);
        foreach (string name in new[] { "Program.cs", "MinimalApiSmoke.csproj", "appsettings.json", "appsettings.Development.json" })
            HostCheck.True(paths.Contains(Path.Combine(root, "apps", "MinimalApiSmoke", name)), name);
    }

    [HostTest("runner refuses newly introduced linked source directories")]
    private static void LinkedSourceInput()
    {
        string root = Path.Combine(HostToolProcess.FindRepositoryRoot(), "artifacts", "host-linked-input", Guid.NewGuid().ToString("N"));
        foreach (string directory in new[] { "src", "std", "tools", "tests", "apps", "scripts", "target" }) Directory.CreateDirectory(Path.Combine(root, directory));
        string link = Path.Combine(root, "tests", "linked");
        File.WriteAllText(Path.Combine(root, "target", "Added.cs"), "// source behind a newly introduced link");
        TestJunction.Create(link, Path.Combine(root, "target"));
        try
        {
            try { _ = HostBuildInputs.SourceInputs(root).ToArray(); throw new InvalidOperationException("linked source escaped inventory"); }
            catch (InvalidOperationException error) when (error.Message.StartsWith("build_input_reparse_point ", StringComparison.Ordinal)) { }
        }
        finally { Directory.Delete(link); }
        HostCheck.True(File.Exists(Path.Combine(root, "target", "Added.cs")), "link cleanup preserves its target");
    }

    [HostTest("runner schedules isolated workers with bounded concurrency and exclusive native barriers")]
    private static void Scheduling() => SchedulingAsync().GetAwaiter().GetResult();

    private static async Task SchedulingAsync()
    {
        TaskCompletionSource firstStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource secondStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int active = 0, peak = 0;
        List<int> entered = [];
        Task run = HostWorkerScheduler.RunAsync(new[] { 0, 1, 2, 3 }, i => i == 2, 2, async (_, i) =>
        {
            int count = Interlocked.Increment(ref active);
            peak = Math.Max(peak, count);
            lock (entered) entered.Add(i);
            if (i == 0) firstStarted.SetResult();
            if (i == 1) secondStarted.SetResult();
            if (i < 2) await release.Task;
            if (i == 2) HostCheck.Equal(1, count);
            Interlocked.Decrement(ref active);
        });
        await Task.WhenAll(firstStarted.Task, secondStarted.Task).WaitAsync(TimeSpan.FromSeconds(10));
        HostCheck.Equal(2, active);
        lock (entered) HostCheck.True(entered.SequenceEqual(new[] { 0, 1 }));
        release.SetResult();
        await run.WaitAsync(TimeSpan.FromSeconds(10));
        HostCheck.Equal(2, peak);
        HostCheck.Equal(0, active);
        HostCheck.True(entered.SequenceEqual(new[] { 0, 1, 2, 3 }));
    }

    [HostTest("runner cancellation drains active workers without starting later fixtures")]
    private static void SchedulingCancellation()
    {
        using CancellationTokenSource cancellation = new();
        List<int> entered = [];
        HostWorkerScheduler.RunAsync(new[] { 0, 1, 2 }, _ => false, 1, (_, i) =>
        {
            entered.Add(i);
            cancellation.Cancel();
            return Task.CompletedTask;
        }, cancellation.Token).GetAwaiter().GetResult();
        HostCheck.True(entered.SequenceEqual(new[] { 0 }));
    }

    [HostTest("runner input changes cancel failed fresh and reused fixtures before another native launch")]
    private static void ChangedFixtureInputs() => ChangedFixtureInputsAsync().GetAwaiter().GetResult();

    private static async Task ChangedFixtureInputsAsync()
    {
        string directory = Path.Combine(HostToolProcess.FindRepositoryRoot(), "artifacts", "host-input-controls", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string source = Path.Combine(directory, "input.cs");
        foreach (var variant in new (bool Failed, bool Fresh, bool Reused)[] { (true, true, false), (false, true, false), (false, false, true) })
        {
            File.WriteAllText(source, "original");
            var files = new Dictionary<string, string> { [source] = HostRunCheckpoint.HashFile(source) };
            using CancellationTokenSource cancellation = new();
            int launched = 0, invalidated = 0;
            var guard = new HostRunInputGuard(() => HostTestFingerprint.FilesMatch(files), cancellation, _ => invalidated++);
            HostTestRunner.FixtureResult? changed = null;
            bool complete = await HostWorkerScheduler.RunPhasesAsync(new[] { 0, 1 }, _ => true, 1, async item =>
            {
                changed = await HostTestRunner.RunWithInputChecks("native-" + item, guard, () =>
                {
                    launched++;
                    File.WriteAllText(source, "modified");
                    return Task.FromResult(new HostTestRunner.FixtureResult("native-" + item, "Native", variant.Failed ? 1 : 0,
                        false, 0, "unused", 1, variant.Failed ? 0 : 1, variant.Failed ? 1 : 0, null) { Reused = variant.Reused });
                }, result => !HostRunCheckpoint.Passed(result) || variant.Fresh || HostTestFingerprint.FilesMatch(files));
            }, () => true, cancellation.Token);
            HostCheck.True(!complete && cancellation.IsCancellationRequested);
            HostCheck.Equal(1, launched);
            HostCheck.Equal(1, invalidated);
            HostCheck.True(changed is not null && changed.ExitCode == 1 && changed.Error == "inputs_changed_during_fixture");
            HostCheck.Equal(variant.Failed ? 1 : 0, changed!.Failed);
            File.WriteAllText(source, "original");
            HostCheck.True(!guard.Check("after_restoration"), "restoration cannot rehabilitate a changed run");
            HostCheck.Equal(1, invalidated);
        }
    }

    [HostTest("runner unchanged inputs preserve ordinary native failure continuation")]
    private static void OrdinaryNativeFailure() => OrdinaryNativeFailureAsync().GetAwaiter().GetResult();

    private static async Task OrdinaryNativeFailureAsync()
    {
        using CancellationTokenSource cancellation = new();
        var guard = new HostRunInputGuard(() => true, cancellation, _ => throw new InvalidOperationException("unexpected invalidation"));
        int launched = 0;
        bool complete = await HostWorkerScheduler.RunPhasesAsync(new[] { 0, 1 }, _ => true, 1, async item =>
        {
            var result = await HostTestRunner.RunWithInputChecks("native-" + item, guard, () =>
            {
                launched++;
                return Task.FromResult(new HostTestRunner.FixtureResult("native-" + item, "Native", 1, false, 0, "unused", 1, 0, 1, "ordinary_failure"));
            });
            HostCheck.Equal("ordinary_failure", result!.Error);
        }, () => true, cancellation.Token);
        HostCheck.True(complete && !cancellation.IsCancellationRequested && guard.Failure is null);
        HostCheck.Equal(2, launched);
    }

    [HostTest("runner input change before launch prevents both execution and cache reuse")]
    private static void ChangedBeforeLaunch()
    {
        foreach (bool reused in new[] { false, true })
        {
            using CancellationTokenSource cancellation = new();
            int entered = 0, invalidated = 0;
            var guard = new HostRunInputGuard(() => false, cancellation, _ => invalidated++);
            var result = HostTestRunner.RunWithInputChecks("before", guard, () =>
            {
                entered++;
                return Task.FromResult(new HostTestRunner.FixtureResult("before", "Native", 0, false, 0, "unused", 1, 1, 0, null) { Reused = reused });
            }).GetAwaiter().GetResult();
            HostCheck.True(result is null && cancellation.IsCancellationRequested);
            HostCheck.Equal(0, entered);
            HostCheck.Equal(1, invalidated);
            HostCheck.Equal("inputs_changed before_fixture before", guard.Failure);
        }
    }

    [HostTest("runner retains exact post-pass validation when the cheap boundary check passes")]
    private static void ExactInputValidation()
    {
        using CancellationTokenSource cancellation = new();
        int exactChecks = 0;
        var guard = new HostRunInputGuard(() => true, cancellation, _ => { });
        var result = HostTestRunner.RunWithInputChecks("exact", guard,
            () => Task.FromResult(new HostTestRunner.FixtureResult("exact", "Fast", 0, false, 0, "unused", 1, 1, 0, null)),
            _ => { exactChecks++; return false; }).GetAwaiter().GetResult();
        HostCheck.True(cancellation.IsCancellationRequested && result!.ExitCode == 1);
        HostCheck.Equal(1, exactChecks);
    }

    [HostTest("runner input cancellation drains active workers before releasing the native barrier")]
    private static void InputCancellationDrain() => InputCancellationDrainAsync().GetAwaiter().GetResult();

    private static async Task InputCancellationDrainAsync()
    {
        using CancellationTokenSource cancellation = new();
        TaskCompletionSource bothStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource cancelled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseOther = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = cancellation.Token.Register(() => cancelled.TrySetResult());
        int started = 0, drained = 0, changed = 0;
        var guard = new HostRunInputGuard(() => Volatile.Read(ref changed) == 0, cancellation, _ => { });
        Task run = HostWorkerScheduler.RunAsync(new[] { 0, 1, 2 }, item => item == 2, 2, async (_, item) =>
        {
            await HostTestRunner.RunWithInputChecks("worker-" + item, guard, async () =>
            {
                if (Interlocked.Increment(ref started) == 2) bothStarted.TrySetResult();
                await bothStarted.Task;
                if (item == 0) Interlocked.Exchange(ref changed, 1);
                else { await cancelled.Task; await releaseOther.Task; Interlocked.Increment(ref drained); }
                return new("worker-" + item, "Fast", 1, false, 0, "unused", 1, 0, 1, "controlled_failure");
            });
        }, cancellation.Token);
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10));
        HostCheck.True(!run.IsCompleted, "run must retain worker ownership through bounded cleanup");
        releaseOther.TrySetResult();
        await run.WaitAsync(TimeSpan.FromSeconds(10));
        HostCheck.Equal(2, started);
        HostCheck.Equal(1, drained);
    }

    [HostTest("runner rejects invalid parallel worker counts")]
    private static void InvalidJobs()
    {
        foreach (string value in new[] { "0", "65", "-1" })
        {
            var result = Run(["--jobs", value, "--list"]);
            HostCheck.Equal(1, result.ExitCode);
            HostCheck.True(result.StandardError.Contains("invalid_jobs", StringComparison.Ordinal));
        }
    }

    [HostTest("runner reports multiple failures and continues")]
    private static void MultipleFailures()
    {
        var result = Control("failures");
        HostCheck.Equal(1, result.ExitCode);
        HostCheck.True(result.StandardOutput.Contains("TEST_FAIL control.first", StringComparison.Ordinal));
        HostCheck.True(result.StandardOutput.Contains("TEST_FAIL control.second", StringComparison.Ordinal));
        HostCheck.True(result.StandardOutput.Contains("TEST_PASS control.after", StringComparison.Ordinal));
    }

    [HostTest("runner rejects an empty selection")]
    private static void EmptySelection()
    {
        var result = Run(["--filter", "___nonexistent_case___"]);
        HostCheck.Equal(1, result.ExitCode);
        HostCheck.True(result.StandardError.Contains("filter_matched_no_fixtures", StringComparison.Ordinal));
    }

    [HostTest("runner listing performs no test execution")]
    private static void Listing()
    {
        var result = Run(["--list", "--filter", "HostTestRunnerTests"]);
        HostCheck.Equal(0, result.ExitCode);
        HostCheck.True(result.StandardOutput.Contains(nameof(MultipleFailures), StringComparison.Ordinal));
        HostCheck.True(!result.StandardOutput.Contains("TEST_START", StringComparison.Ordinal));
    }

    [HostTest("runner bounds worker lifetime and later workers still run")]
    private static void TimeoutAndRecovery()
    {
        var timeout = Run(["--runner-control", "timeout"], TimeSpan.FromSeconds(2));
        HostCheck.True(timeout.TimedOut && !timeout.Succeeded);
        HostCheck.True(timeout.Duration < TimeSpan.FromSeconds(15));
        var later = Control("echo", "after timeout");
        HostCheck.True(later.Succeeded);
    }

    [HostTest("runner reports abnormal child exit and continues")]
    private static void AbnormalExit()
    {
        HostCheck.Equal(23, Control("crash").ExitCode);
        HostCheck.True(Control("echo", "after abnormal exit").Succeeded);
    }

    [HostTest("runner cancels a running child and retains streamed output")]
    private static void CancellationAndStreaming()
    {
        using CancellationTokenSource cancellation = new();
        var streamed = new System.Text.StringBuilder();
        var result = ProcessRunner.RunAsync(new("dotnet",
            [typeof(HostTestRunnerTests).Assembly.Location, "--runner-control", "wait-after-output"], HostToolProcess.FindRepositoryRoot())
        {
            Timeout = TimeSpan.FromSeconds(15),
            OutputReceived = (text, _) => { streamed.Append(text); cancellation.Cancel(); }
        }, cancellation.Token).GetAwaiter().GetResult();
        HostCheck.True(result.Cancelled && !result.TimedOut && !result.Succeeded);
        HostCheck.Equal("control-ready", streamed.ToString().Trim());
        HostCheck.Equal(streamed.ToString(), result.StandardOutput);
    }

    [HostTest("runner bounds captured output")]
    private static void OutputLimit()
    {
        var result = Run(["--runner-control", "output"], limit: 4096);
        HostCheck.True(result.OutputLimitExceeded && !result.Succeeded);
        HostCheck.True(result.StandardOutput.Length + result.StandardError.Length <= 4096);
    }

    [HostTest("runner preserves literal arguments without shell parsing")]
    private static void LiteralArguments()
    {
        string[] expected = ["two words", "quote\"here", "$(not-a-command)", "a&b", "", "trailing\\"];
        var result = Control("echo", expected);
        HostCheck.True(result.Succeeded);
        HostCheck.True(JsonSerializer.Deserialize<string[]>(result.StandardOutput)!.SequenceEqual(expected));
    }

    [HostTest("contracts reports every independent mismatch")]
    private static void AllContractFailures()
    {
        XDocument project = ContractAudit.ReadProject(Path.Combine(HostToolProcess.FindRepositoryRoot(), "src/OS/OS.csproj"));
        project.Descendants("Compile").First().SetAttributeValue("Include", "unauthorized.cs");
        project.Descendants("NoStdLib").Single().Value = "false";
        var errors = ContractAudit.InspectProject(project);
        HostCheck.True(errors.Any(error => error.Contract == "efi.compile[0]"));
        HostCheck.True(errors.Any(error => error.Contract == "efi.property.NoStdLib"));
    }

    [HostTest("contracts source identity accepts LF and CRLF but rejects content changes")]
    private static void SourceIdentity()
    {
        const string lf = "one\ntwo\n";
        HostCheck.Equal(ContractAudit.HashSource(lf), ContractAudit.HashSource("one\r\ntwo\r\n"));
        HostCheck.True(ContractAudit.HashSource(lf) != ContractAudit.HashSource("one\nthree\n"));
        HostCheck.True(ContractAudit.HashText(lf) != ContractAudit.HashText("one\r\ntwo\r\n"));
    }

    [HostTest("contracts current EFI inventory matches reviewed CSharp definitions")]
    private static void CurrentContracts()
    {
        var errors = ContractAudit.Inspect(HostToolProcess.FindRepositoryRoot());
        HostCheck.Equal("", string.Join(";", errors.Select(error => error.Contract + ":" + error.Actual)));
    }

    [HostTest("runner rejects stale build inputs without rewriting them")]
    private static void StaleBuildInputs()
    {
        string directory = Path.Combine(HostToolProcess.FindRepositoryRoot(), "artifacts", "test-input-controls", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "input.txt");
        File.WriteAllText(path, "original");
        string manifest = path + "|" + ContractAudit.Hash(File.ReadAllBytes(path));
        HostCheck.Equal(0, HostBuildInputs.Validate(new StringReader(manifest)).Count);
        string added = Path.Combine(directory, "added.cs");
        File.WriteAllText(added, "// new source after compilation");
        HostCheck.True(HostBuildInputs.Validate(new StringReader(manifest), [path, added]).Single().StartsWith("unrecorded_build_input", StringComparison.Ordinal));
        File.WriteAllText(path, "changed");
        HostCheck.True(HostBuildInputs.Validate(new StringReader(manifest)).Single().StartsWith("stale_build_input", StringComparison.Ordinal));
        HostCheck.Equal("changed", File.ReadAllText(path));
        File.Delete(path);
        HostCheck.True(HostBuildInputs.Validate(new StringReader(manifest)).Single().StartsWith("missing_build_input", StringComparison.Ordinal));
    }

    [HostTest("done allocator atomically reserves unique steps")]
    private static void ConcurrentReservations()
    {
        string directory = Path.Combine(HostToolProcess.FindRepositoryRoot(), "artifacts", "step-allocation-controls", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(directory, "done"));
        string[] paths = Enumerable.Range(0, 12).AsParallel().Select(_ => DoneSteps.Allocate(directory, true)).ToArray();
        HostCheck.Equal(paths.Length, paths.Distinct(StringComparer.Ordinal).Count());
        HostCheck.True(paths.All(path => File.ReadAllText(Path.Combine(directory, path)).Contains("## Evidence boundary", StringComparison.Ordinal)));
    }

    [HostTest("runner phase groups preserve every selected case exactly once")]
    private static void PhaseGroups()
    {
        var fixtures = HostTestRunner.Discover();
        var groups = HostTestRunner.GroupByLane(fixtures);
        HostCheck.True(groups.SelectMany(group => group.Fixtures).Select(fixture => fixture.Id).Order(StringComparer.Ordinal)
            .SequenceEqual(fixtures.Select(fixture => fixture.Id).Order(StringComparer.Ordinal)));
        HostCheck.Equal(groups.Length, groups.Select(group => group.Id).Distinct(StringComparer.Ordinal).Count());
        HostCheck.True(groups.All(group => group.Fixtures.All(fixture => fixture.Lane == group.Lane)));
        var mixed = fixtures.GroupBy(fixture => fixture.Method.DeclaringType!.Name)
            .Where(owner => owner.Select(fixture => fixture.Lane).Distinct().Count() == 2).ToArray();
        HostCheck.True(mixed.Length > 0, "exercise actual mixed fixtures");
        foreach (var owner in mixed)
            foreach (HostLane lane in new[] { HostLane.Fast, HostLane.Native })
                HostCheck.True(groups.Any(group => group.Id == owner.Key + "." + lane && group.Lane == lane));
        var fast = HostTestRunner.GroupByLane(fixtures.Where(fixture => fixture.Lane == HostLane.Fast).ToArray());
        HostCheck.True(fast.All(group => group.Id == group.Fixtures[0].Method.DeclaringType!.Name), "single-lane checkpoint identities stay stable");
    }

    [HostTest("runner phase gate collects all fast failures before allowing native work")]
    private static void PhaseGate() => PhaseGateAsync().GetAwaiter().GetResult();

    private static async Task PhaseGateAsync()
    {
        // Native items deliberately precede fast items in the input plan.
        var items = new[] { (Id: 0, Native: true), (Id: 1, Native: false), (Id: 2, Native: true), (Id: 3, Native: false) };
        foreach (bool failFast in new[] { true, false })
        {
            int fastCompleted = 0, failures = 0, nativeCompleted = 0, nativeActive = 0;
            bool complete = await HostWorkerScheduler.RunPhasesAsync(items, item => item.Native, 2, async item =>
            {
                if (item.Native)
                {
                    HostCheck.Equal(2, Volatile.Read(ref fastCompleted), "all fast cases must finish before native entry");
                    HostCheck.Equal(1, Interlocked.Increment(ref nativeActive), "native workers remain exclusive");
                    await Task.Yield();
                    Interlocked.Decrement(ref nativeActive);
                    nativeCompleted++;
                }
                else
                {
                    await Task.Yield();
                    if (failFast) Interlocked.Increment(ref failures);
                    Interlocked.Increment(ref fastCompleted);
                }
            }, () => failures == 0);
            HostCheck.Equal(2, fastCompleted, "one fast failure must not hide another");
            HostCheck.Equal(failFast ? 2 : 0, failures);
            HostCheck.Equal(failFast ? 0 : 2, nativeCompleted);
            HostCheck.Equal(!failFast, complete);
        }
    }

    private static ProcessResult Control(string name, params string[] arguments) => Run(["--runner-control", name, .. arguments]);

    private static ProcessResult Run(string[] arguments, TimeSpan? timeout = null, int limit = 64 * 1024) =>
        ProcessRunner.RunAsync(new("dotnet", [typeof(HostTestRunnerTests).Assembly.Location, .. arguments], HostToolProcess.FindRepositoryRoot())
        { Timeout = timeout ?? TimeSpan.FromSeconds(15), OutputCharacterLimit = limit }).GetAwaiter().GetResult();
}
