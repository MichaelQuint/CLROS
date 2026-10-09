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
        TaskCompletionSource failedFirst = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int started = 0, finished = 0;
        Task run = HostWorkerScheduler.RunAsync(new[] { 0, 1, 2 }, _ => false, 2, async (_, item) =>
        {
            if (Interlocked.Increment(ref started) == 2) bothStarted.SetResult();
            await bothStarted.Task;
            if (item == 0) { failedFirst.SetResult(); throw new IOException("controlled receipt failure"); }
            await releaseSecond.Task;
            Interlocked.Increment(ref finished);
        });
        await failedFirst.Task.WaitAsync(TimeSpan.FromSeconds(10));
        HostCheck.True(!run.IsCompleted, "scheduler must retain ownership of the second worker");
        releaseSecond.SetResult();
        try { await run; throw new InvalidOperationException("scheduler swallowed the failure"); }
        catch (IOException error) when (error.Message == "controlled receipt failure") { }
        HostCheck.Equal(2, started);
        HostCheck.Equal(1, finished);
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

    private static ProcessResult Control(string name, params string[] arguments) => Run(["--runner-control", name, .. arguments]);

    private static ProcessResult Run(string[] arguments, TimeSpan? timeout = null, int limit = 64 * 1024) =>
        ProcessRunner.RunAsync(new("dotnet", [typeof(HostTestRunnerTests).Assembly.Location, .. arguments], HostToolProcess.FindRepositoryRoot())
        { Timeout = timeout ?? TimeSpan.FromSeconds(15), OutputCharacterLimit = limit }).GetAwaiter().GetResult();
}
