using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text.Json;

internal enum HostLane { Fast, Native }

[AttributeUsage(AttributeTargets.Method)]
internal sealed class HostFixtureAttribute(HostLane lane = HostLane.Fast) : Attribute
{
    public HostLane Lane { get; } = lane;
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
internal sealed class HostTestAttribute(string name, HostLane lane = HostLane.Fast, int order = 0) : Attribute
{
    public string Name { get; } = name;
    public HostLane Lane { get; } = lane;
    public int Order { get; } = order;
}

internal static class HostCheck
{
    public static int Failures { get; private set; }
    public static int Executed { get; private set; }
    public static string? Filter { get; set; }

    public static void Registered(Type fixture)
    {
        True(fixture.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Any(method => method.IsDefined(typeof(HostFixtureAttribute)) || method.IsDefined(typeof(HostTestAttribute))),
            "behavioral fixture must remain registered: " + fixture.Name);
    }

    public static void SingleFixture(Type owner, string expectedMethod, params Type[] coordinatorOnly)
    {
        var discovery = HostTestRunner.Discover();
        var entries = discovery.Where(item => item.Method.DeclaringType == owner).ToArray();
        True(entries.Length == 1, "explicit fixture registration must be unique: " + owner.Name);
        True(entries[0].Method.Name == expectedMethod && entries[0].Name is null,
            "explicit fixture entry must be the aggregate method: " + owner.Name + "." + expectedMethod);
        foreach (Type type in new[] { owner }.Concat(coordinatorOnly))
            True(!type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Any(method => method.IsDefined(typeof(ModuleInitializerAttribute))),
                "fixture must not run through a module initializer: " + type.Name);
        foreach (Type type in coordinatorOnly)
            True(!discovery.Any(item => item.Method.DeclaringType == type),
                "coordinator-only fixture must not be discovered independently: " + type.Name);
    }

    public static void Run(string name, Func<bool> test, ref int failures)
    {
        if (!Selected(name)) return;
        Executed++;
        Console.WriteLine("TEST_START " + name);
        try
        {
            if (!test()) throw new InvalidOperationException("assertion_returned_false method=" + test.Method.Name);
            Console.WriteLine("TEST_PASS " + name);
        }
        catch (Exception error)
        {
            failures++;
            Failures++;
            Console.WriteLine("TEST_FAIL " + name + " " + error.ToString().ReplaceLineEndings(" "));
        }
    }

    public static void Run(string name, Action test, ref int failures) =>
        Run(name, () => { test(); return true; }, ref failures);

    public static void Run(string name, Action test)
    {
        int failures = 0;
        Run(name, test, ref failures);
    }

    private static bool Selected(string name) => Filter is null || name.Contains(Filter, StringComparison.OrdinalIgnoreCase);

    public static void True(bool value, string? reason = null,
        [CallerArgumentExpression(nameof(value))] string expression = "",
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        if (!value) throw new InvalidOperationException($"{reason ?? expression} expected=True actual=False at {Path.GetFileName(file)}:{line}");
    }

    public static bool Result(bool value,
        [CallerArgumentExpression(nameof(value))] string expression = "",
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        True(value, null, expression, file, line);
        return true;
    }

    public static void Equal<T>(T expected, T actual,
        [CallerArgumentExpression(nameof(actual))] string expression = "",
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{expression} expected={expected} actual={actual} at {Path.GetFileName(file)}:{line}");
    }
}

internal static class HostTestRunner
{
    internal sealed record Fixture(string Id, HostLane Lane, MethodInfo Method, string? Name = null, int Order = 0);
    internal sealed record FixtureGroup(string Id, HostLane Lane, Fixture[] Fixtures);
    // Worker processes have no synchronization context. Join the returned
    // operation before deciding pass/fail or starting the next fixture. Reject
    // async void and unknown awaitables before they can escape worker ownership.
    internal static bool Invoke(MethodInfo method)
    {
        Type type = method.ReturnType;
        if (type == typeof(void) && method.GetCustomAttribute<AsyncStateMachineAttribute>() is not null ||
            type != typeof(void) && type != typeof(bool) && type != typeof(Task) && type != typeof(Task<bool>) &&
            type != typeof(ValueTask) && type != typeof(ValueTask<bool>))
            throw new InvalidOperationException("unsupported_fixture_return " + type.FullName);
        object? value;
        try { value = method.Invoke(null, null); }
        catch (TargetInvocationException error)
        {
            ExceptionDispatchInfo.Capture(error.InnerException ?? error).Throw();
            throw;
        }
        if (value is null && typeof(Task).IsAssignableFrom(type))
            throw new InvalidOperationException("null_fixture_task " + type.FullName);
        if (value is Task<bool> result) return result.GetAwaiter().GetResult();
        if (value is Task task) { task.GetAwaiter().GetResult(); return true; }
        if (value is ValueTask<bool> resultValue) return resultValue.AsTask().GetAwaiter().GetResult();
        if (value is ValueTask taskValue) { taskValue.AsTask().GetAwaiter().GetResult(); return true; }
        return value is not false;
    }

    private static async Task<bool> AsyncFalseControl() { await Task.Delay(10); return false; }
    private static async Task AsyncThrowControl() { await Task.Delay(10); throw new InvalidOperationException("async_control_failure"); }
    private static async Task AsyncPassControl() { await Task.Delay(10); Console.WriteLine("ASYNC_CONTROL_AFTER joined=true"); }

    internal static FixtureGroup[] GroupByLane(Fixture[] fixtures) => fixtures
        .GroupBy(fixture => fixture.Method.DeclaringType!.Name)
        .SelectMany(owner => owner.GroupBy(fixture => fixture.Lane).Select(lane =>
            new FixtureGroup(owner.Key + (owner.Select(fixture => fixture.Lane).Distinct().Count() > 1 ? "." + lane.Key : ""),
                lane.Key, lane.ToArray())))
        .ToArray();

    internal sealed record FixtureResult(string Id, string Lane, int ExitCode, bool TimedOut,
        double Seconds, string Log, int Started, int Passed, int Failed, string? Error)
    {
        public bool Reused { get; init; }
        public string Origin { get; init; } = "";
        public int ProcessId { get; init; }
    }

    internal static Fixture[] Discover() => typeof(HostTestRunner).Assembly.GetTypes()
        .SelectMany(type => type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .SelectMany(method =>
            {
                List<Fixture> cases = [];
                if (method.GetCustomAttribute<HostFixtureAttribute>() is { } fixture)
                    cases.Add(new(type.Name + "." + method.Name, fixture.Lane, method));
                foreach (var test in method.GetCustomAttributes<HostTestAttribute>())
                    cases.Add(new(type.Name + "." + method.Name + "/" + test.Order, test.Lane, method, test.Name, test.Order));
                return cases;
            }))
        .OrderBy(f => f.Method.DeclaringType!.Name, StringComparer.Ordinal).ThenBy(f => f.Order).ThenBy(f => f.Id, StringComparer.Ordinal).ToArray();

    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length > 0 && args[0] == "--qemu-control") return await QemuTestControl.RunAsync(args[1..]);
        if (args.Length > 0 && args[0] == "--efi-control") return await EfiPreflightTestControl.RunAsync(args[1..]);
        // Deliberately failing worker controls are reachable only by this explicit internal command.
        // They are never registered as ordinary tests.
        if (args.Length >= 2 && args[0] == "--runner-control")
        {
            switch (args[1])
            {
                case "fingerprint":
                    var fingerprintFixtures = Discover().Where(f => f.Method.DeclaringType!.Name == args[2]).ToArray();
                    if (fingerprintFixtures.Length == 0) return 2;
                    Console.WriteLine(HostTestFingerprint.Create(HostToolProcess.FindRepositoryRoot()).For(fingerprintFixtures, null));
                    return 0;
                case "failures":
                    int failures = 0;
                    HostCheck.Run("control.first", () => false, ref failures);
                    HostCheck.Run("control.second", () => throw new InvalidOperationException("second_failure"), ref failures);
                    HostCheck.Run("control.after", () => true, ref failures);
                    return failures == 2 ? 1 : 2;
                case "async-failures":
                    int asyncFailures = 0;
                    foreach ((string name, string method) in new[] { ("false", nameof(AsyncFalseControl)), ("throw", nameof(AsyncThrowControl)), ("after", nameof(AsyncPassControl)) })
                        HostCheck.Run("control.async." + name, () => Invoke(typeof(HostTestRunner).GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic)!), ref asyncFailures);
                    return asyncFailures == 2 ? 1 : 2;
                case "timeout": await Task.Delay(Timeout.Infinite); return 2;
                case "wait-after-output": Console.WriteLine("control-ready"); await Task.Delay(Timeout.Infinite); return 2;
                case "crash": return 23;
                case "output": Console.Write(new string('x', 100_000)); return 0;
                case "echo": Console.Write(JsonSerializer.Serialize(args[2..])); return 0;
                case "environment": Console.Write(JsonSerializer.Serialize(args[2..].Select(Environment.GetEnvironmentVariable).ToArray())); return 0;
                case "streams":
                case "streams-wait":
                    Console.Write("stdout-ok"); Console.Error.Write("stderr-ok");
                    Console.Out.Flush(); Console.Error.Flush();
                    if (args[1] == "streams-wait") await Task.Delay(Timeout.Infinite);
                    return 0;
                case "partial-object": File.WriteAllBytes(args[2], [1, 2, 3, 4, 5, 6, 7, 8]); Console.WriteLine("partial-object-written"); return 0;
                case "partial-before-timeout": Console.WriteLine("partial-before-timeout"); await Task.Delay(Timeout.Infinite); return 2;
                case "uppercase-output": Console.Write(new string('X', 4096)); return 0;
                case "invalid-utf8":
                    var bytes = Console.OpenStandardOutput(); bytes.WriteByte(255); bytes.Flush();
                    await Task.Delay(Timeout.Infinite); return 2;
                case "locked-child":
                    using (var sentinel = File.Open(args[2], FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                        await Task.Delay(Timeout.Infinite);
                    return 2;
                case "spawn-locked-child":
                    var childStart = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
                    foreach (string value in new[] { typeof(HostTestRunner).Assembly.Location, "--runner-control", "locked-child", args[2] }) childStart.ArgumentList.Add(value);
                    if (args.Length > 5 && args[5] == "detached")
                    {
                        childStart.RedirectStandardOutput = true;
                        childStart.RedirectStandardError = true;
                    }
                    using (var child = Process.Start(childStart)!)
                    {
                        var childDeadline = Stopwatch.StartNew();
                        while (!File.Exists(args[2]))
                        {
                            if (childDeadline.Elapsed > TimeSpan.FromSeconds(4)) return 3;
                            await Task.Delay(20);
                        }
                        if (args.Length > 3 && args[3] == "exit-parent")
                        {
                            Console.Write("parent-output"); Console.Error.Write("parent-error");
                            return args.Length > 4 ? int.Parse(args[4], System.Globalization.CultureInfo.InvariantCulture) : 0;
                        }
                        if (args.Length > 3 && args[3] == "signal-and-wait")
                        {
                            Console.Write("parent-output"); Console.Error.Write("parent-error");
                        }
                        if (args.Length > 3 && args[3] == "overflow") Console.Write(new string('x', 100_000));
                        await Task.Delay(Timeout.Infinite);
                    }
                    return 2;
                default: return 2;
            }
        }
        string lane = "fast";
        string? filter = null, worker = null, caseFilter = null, resultsPath = null, resume = null, workerLease = null;
        int jobs = Math.Clamp(Environment.ProcessorCount, 1, 4);
        bool list = false;
        try
        {
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--lane": lane = args[++i]; break;
                    case "--filter": filter = args[++i]; break;
                    case "--case": caseFilter = args[++i]; break;
                    case "--worker": worker = args[++i]; break;
                    case "--worker-lease": workerLease = args[++i]; break;
                    case "--results": resultsPath = args[++i]; break;
                    case "--resume": resume = args[++i]; break;
                    case "--list": list = true; break;
                    case "--jobs": jobs = int.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
                    case "--help":
                        Console.WriteLine("CLROS host tests: [--lane fast|native|full] [--filter fixture] [--case name] [--jobs 1..64] [--list] [--results directory | --resume directory-or-results.json]");
                        return 0;
                    default: throw new ArgumentException("unknown_option " + args[i]);
                }
            }
            if (resume is not null)
            {
                if (resultsPath is not null || worker is not null || list) throw new ArgumentException("resume_options_conflict");
                resume = Path.GetFullPath(resume);
                if (File.Exists(resume)) resume = Path.GetDirectoryName(resume)!;
                var saved = HostRunCheckpoint.ReadSelection(resume);
                if (!args.Contains("--lane")) lane = saved.Lane;
                if (!args.Contains("--filter")) filter = saved.Filter;
                if (!args.Contains("--case")) caseFilter = saved.CaseFilter;
            }
            if (lane is not ("fast" or "native" or "full")) throw new ArgumentException("invalid_lane " + lane);
            if (jobs is < 1 or > 64) throw new ArgumentException("invalid_jobs " + jobs);
            if (filter is not null && string.IsNullOrWhiteSpace(filter)) throw new ArgumentException("empty_filter");
            if (caseFilter is not null && string.IsNullOrWhiteSpace(caseFilter)) throw new ArgumentException("empty_case_filter");
            Fixture[] all = Discover();
            if (all.Length == 0 || all.Select(f => f.Id).Distinct(StringComparer.Ordinal).Count() != all.Length)
                throw new InvalidOperationException("invalid_fixture_inventory");
            if (worker is not null)
            {
                using var workerLock = workerLease is null ? null : File.Open(workerLease, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
                if (workerLock is not null)
                {
                    workerLock.Write(System.Text.Encoding.UTF8.GetBytes(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                    workerLock.Flush();
                }
                string[] requested = worker.Split(';');
                Fixture[] fixtures = requested.Select(id => all.Single(f => f.Id == id)).ToArray();
                HostCheck.Filter = caseFilter;
                int fixtureFailures = 0;
                foreach (Fixture fixture in fixtures)
                {
                    if (fixture.Name is not null)
                    {
                        HostCheck.Run(fixture.Name, () => Invoke(fixture.Method), ref fixtureFailures);
                    }
                    else
                    {
                        try
                        {
                            if (!Invoke(fixture.Method))
                            { Console.WriteLine("TEST_FAIL " + fixture.Id + " returned_false"); fixtureFailures++; }
                        }
                        catch (Exception error)
                        {
                            Console.WriteLine("TEST_FAIL " + fixture.Id + " " + error.ToString().ReplaceLineEndings(" "));
                            fixtureFailures++;
                        }
                    }
                }
                if (caseFilter is not null && HostCheck.Executed == 0)
                {
                    Console.WriteLine("TEST_FAIL host.tests.worker case_filter_matched_no_tests");
                    return 1;
                }
                return HostCheck.Failures == 0 && fixtureFailures == 0 ? 0 : 1;
            }
            Fixture[] selected = all.Where(f =>
                (lane == "full" || (f.Lane == HostLane.Fast) == (lane == "fast")) &&
                (filter is null || f.Id.Contains(filter, StringComparison.OrdinalIgnoreCase) || f.Name?.Contains(filter, StringComparison.OrdinalIgnoreCase) == true) &&
                (caseFilter is null || f.Name is null || f.Name.Contains(caseFilter, StringComparison.OrdinalIgnoreCase))).ToArray();
            if (selected.Length == 0) throw new ArgumentException("filter_matched_no_fixtures");
            if (list)
            {
                foreach (Fixture fixture in selected) Console.WriteLine($"{fixture.Id}\t{fixture.Lane.ToString().ToLowerInvariant()}\t{fixture.Name ?? "fixture group"}");
                return 0;
            }
            var stale = HostBuildInputs.Validate();
            if (stale.Count != 0)
            {
                foreach (string failure in stale) Console.Error.WriteLine("TEST_FAIL host.tests.inputs " + failure);
                Console.Error.WriteLine("Rebuild with: dotnet build tests/OS.HostTests --nologo -m:1");
                return 1;
            }
            string root = HostToolProcess.FindRepositoryRoot();
            string directory = resume ?? (resultsPath is null ? Path.Combine(root, "artifacts", "logs", "host-tests", DateTime.UtcNow.ToString("yyyyMMddTHHmmss") + "-" + Guid.NewGuid().ToString("N")) : Path.GetFullPath(resultsPath));
            using var checkpoint = new HostRunCheckpoint(root, directory, resume is not null, new(lane, filter, caseFilter));
            var fingerprints = HostTestFingerprint.Create(root);
            string producer = HostRunCheckpoint.HashFile(typeof(HostTestRunner).Assembly.Location);
            List<FixtureResult> results = [];
            var groups = GroupByLane(selected);
            object receiptLock = new();
            Stopwatch elapsed = Stopwatch.StartNew();
            Console.WriteLine($"HOST_TEST_SELECTION lane={lane} fixtures={groups.Length} cases={selected.Length} inventory={all.Length} jobs={jobs} report={directory}");
            void SaveProgress() => SaveReceipt(directory, new
            {
                Lane = lane, Filter = filter, CaseFilter = caseFilter, Inventory = all.Length, Jobs = jobs,
                Run = checkpoint.Run, ProducerSha256 = producer,
                Executed = results.Count(r => !r.Reused), Reused = results.Count(r => r.Reused),
                WallSeconds = elapsed.Elapsed.TotalSeconds, WorkerSeconds = results.Where(r => !r.Reused).Sum(r => r.Seconds),
                Selected = selected.Select(f => new { f.Id, f.Name, Lane = f.Lane.ToString() }),
                Complete = false, Succeeded = false, Results = results.OrderBy(r => r.Id, StringComparer.Ordinal)
            });
            SaveProgress();
            using CancellationTokenSource cancellation = new();
            var inputs = new HostRunInputGuard(() => fingerprints.BoundaryInputsUnchanged(root), cancellation, failure =>
            {
                checkpoint.InvalidateRun();
                Console.Error.WriteLine("TEST_FAIL host.tests.inputs " + failure);
            });
            ConsoleCancelEventHandler cancel = (_, signal) => { signal.Cancel = true; cancellation.Cancel(); };
            Console.CancelKeyPress += cancel;
            try
            {
                bool phasesComplete = await HostWorkerScheduler.RunPhasesAsync(groups, group => group.Lane == HostLane.Native, jobs, async group =>
                {
                    Fixture[] fixtures = group.Fixtures;
                    string fingerprint = fingerprints.For(fixtures, caseFilter);
                    FixtureResult? result = await RunWithInputChecks(group.Id, inputs, async () =>
                    {
                        if (checkpoint.TryReuse(group.Id, fingerprint, out var previous) &&
                            HostNativeResumePolicy.Outputs(root, fixtures, previous!.Result).All(previous.ArtifactSources.ContainsKey))
                        {
                            Console.WriteLine($"HOST_FIXTURE_REUSED {group.Id} origin={previous.Origin}");
                            return previous!.Result with { Reused = true, Origin = previous.Origin };
                        }
                        string log = checkpoint.Start(group.Id);
                        Console.WriteLine($"HOST_FIXTURE_START {group.Id}");
                        return (await RunFixture(fixtures, root, log, caseFilter, cancellation)) with { Id = group.Id, Origin = checkpoint.Run };
                    }, completed => !HostRunCheckpoint.Passed(completed) || fingerprint.StartsWith("fresh:", StringComparison.Ordinal) ||
                        fingerprints.InputsUnchanged(root, fixtures));
                    if (result is null) return;
                    if (!result.Reused)
                    {
                        checkpoint.Finish(group.Id, fingerprint, result, HostNativeResumePolicy.Outputs(root, fixtures, result));
                        Console.WriteLine($"HOST_FIXTURE_RESULT {group.Id} exit={result.ExitCode} failures={result.Failed} seconds={result.Seconds:F3}");
                    }
                    // Persist progress after every fixture; only final input validation can complete a run.
                    lock (receiptLock)
                    {
                        results.Add(result);
                        SaveProgress();
                    }
                }, () => results.All(HostRunCheckpoint.Passed), cancellation.Token);
                if (!phasesComplete && !cancellation.IsCancellationRequested)
                    Console.WriteLine("HOST_NATIVE_BLOCKED reason=fast_failures");
            }
            finally { Console.CancelKeyPress -= cancel; }
            int failures = results.Count(r => r.ExitCode != 0 || r.TimedOut || r.Failed != 0 || r.Started == 0 || r.Passed < r.Started);
            List<string> changed = [];
            if (inputs.Failure is string inputFailure) changed.Add(inputFailure);
            try
            {
                if (!fingerprints.ExactRunInputsUnchanged(root)) changed.Add("run_snapshot_inputs_changed");
                changed.AddRange(HostBuildInputs.Validate());
                var finalFingerprints = HostTestFingerprint.Create(root);
                foreach (var group in groups)
                    if (fingerprints.For(group.Fixtures, caseFilter) != finalFingerprints.For(group.Fixtures, caseFilter))
                        changed.Add("checkpoint_inputs_changed " + group.Id);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                // A removed or temporarily malformed input must still produce a
                // final failed receipt after the scheduler has drained workers.
                changed.Add("final_input_validation_failed " + error.GetType().Name);
            }
            if (changed.Count != 0) checkpoint.InvalidateRun();
            failures += checkpoint.ValidateResults(results);
            foreach (string failure in changed) Console.Error.WriteLine("TEST_FAIL host.tests.inputs " + failure);
            failures += changed.Count;
            if (cancellation.IsCancellationRequested || results.Count != groups.Length) failures++;
            string scope = filter is not null || caseFilter is not null ? "host.tests.focused" : lane == "full" ? "host.tests" : "host.tests." + lane;
            SaveReceipt(directory, new
            {
                Scope = scope, Lane = lane, Filter = filter, CaseFilter = caseFilter, Inventory = all.Length, Jobs = jobs,
                Run = checkpoint.Run, ProducerSha256 = producer,
                Executed = results.Count(r => !r.Reused), Reused = results.Count(r => r.Reused),
                WallSeconds = elapsed.Elapsed.TotalSeconds, WorkerSeconds = results.Where(r => !r.Reused).Sum(r => r.Seconds),
                Selected = selected.Select(f => new { f.Id, f.Name, Lane = f.Lane.ToString() }),
                Complete = results.Count == groups.Length && !cancellation.IsCancellationRequested, Succeeded = failures == 0,
                Cancelled = cancellation.IsCancellationRequested,
                InputsUnchanged = changed.Count == 0, InputFailures = changed, Results = results.OrderBy(r => r.Id, StringComparer.Ordinal)
            });
            Console.WriteLine($"HOST_TEST_TIMING wallSeconds={elapsed.Elapsed.TotalSeconds:F3} workerSeconds={results.Where(r => !r.Reused).Sum(r => r.Seconds):F3} checks={results.Where(r => !r.Reused).Sum(r => r.Started)} reusedChecks={results.Where(r => r.Reused).Sum(r => r.Started)}");
            foreach (var result in results.Where(r => !r.Reused).OrderByDescending(r => r.Seconds).Take(5))
                Console.WriteLine($"HOST_TEST_SLOWEST {result.Id} seconds={result.Seconds:F3} checks={result.Started}");
            Console.WriteLine(failures == 0 ? "TEST_PASS " + scope : $"TEST_FAIL {scope} failures={failures}");
            return failures == 0 ? 0 : 1;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("TEST_FAIL host.tests.runner " + error.ToString().ReplaceLineEndings(" "));
            return 1;
        }
    }

    private static void SaveReceipt(string directory, object receipt)
        => OS.Tooling.AtomicReceipt.WriteJson(Path.Combine(directory, "results.json"), receipt);

    // Cache reuse, failed workers and fresh/native workers all cross the same
    // integrity boundary. The additional exact check retains the stronger byte
    // and toolchain validation already required for cacheable passing fixtures.
    internal static async Task<FixtureResult?> RunWithInputChecks(string id, HostRunInputGuard inputs,
        Func<Task<FixtureResult>> run, Func<FixtureResult, bool>? exact = null)
    {
        if (!inputs.Check("before_fixture " + id)) return null;
        FixtureResult? result = null;
        bool unchanged = false;
        try { result = await run(); }
        finally
        {
            unchanged = inputs.Check("after_fixture " + id, result is null || exact is null ? null : () => exact(result));
        }
        return unchanged ? result : result! with { ExitCode = 1, Error = "inputs_changed_during_fixture" };
    }

    internal static string? WorkerProcessFailure(OS.Tooling.ProcessResult process, CancellationTokenSource scheduling)
    {
        if (!process.TerminationConfirmed)
        {
            // No later native work may enter the shared publish tree without
            // proof this worker and all its descendants have stopped. The
            // scheduler's finally block drains other active workers.
            scheduling.Cancel();
            return "termination_unconfirmed" + (process.FailureReason.Length == 0 ? "" : " " + process.FailureReason);
        }
        if (process.FailureReason.Length != 0) return process.FailureReason;
        return process.Cancelled ? "fixture_cancelled" : process.TimedOut ? "fixture_timeout" :
            process.OutputLimitExceeded ? "output_limit_exceeded" : null;
    }

    private static async Task<FixtureResult> RunFixture(Fixture[] fixtures, string root, string log, string? filter, CancellationTokenSource scheduling)
    {
        string id = fixtures[0].Method.DeclaringType!.Name;
        HostLane lane = fixtures.Any(f => f.Lane == HostLane.Native) ? HostLane.Native : HostLane.Fast;
        List<string> arguments = [typeof(HostTestRunner).Assembly.Location, "--worker", string.Join(';', fixtures.Select(f => f.Id)), "--worker-lease", log + ".worker.lock"];
        if (filter is not null) arguments.AddRange(["--case", filter]);
        Stopwatch time = Stopwatch.StartNew();
        try
        {
            using StreamWriter liveLog = new(log) { AutoFlush = true };
            object outputLock = new();
            System.Text.StringBuilder[] partialLines = [new(), new()];
            var process = await OS.Tooling.ProcessRunner.RunAsync(new("dotnet", arguments, root)
            {
                Timeout = TimeSpan.FromMinutes(lane == HostLane.Fast ? 5 : 45), OutputCharacterLimit = 16 * 1024 * 1024,
                OutputReceived = (text, error) =>
                {
                    lock (outputLock)
                    {
                        liveLog.Write(text);
                        var partial = partialLines[error ? 1 : 0];
                        partial.Append(text);
                        string pending = partial.ToString();
                        int end = pending.LastIndexOf('\n');
                        if (end >= 0)
                        {
                            (error ? Console.Error : Console.Out).Write(pending[..(end + 1)]);
                            partial.Remove(0, end + 1);
                        }
                    }
                }
            }, scheduling.Token);
            string? processFailure = WorkerProcessFailure(process, scheduling);
            for (int i = 0; i < partialLines.Length; i++)
                if (partialLines[i].Length != 0) (i == 1 ? Console.Error : Console.Out).WriteLine(partialLines[i]);
            string output = process.StandardOutput;
            string error = process.StandardError;
            string[] lines = output.Split('\n').Select(s => s.TrimEnd('\r')).ToArray();
            int starts = lines.Count(l => l.StartsWith("TEST_START ", StringComparison.Ordinal));
            int passes = lines.Count(l => l.StartsWith("TEST_PASS ", StringComparison.Ordinal));
            int failed = lines.Count(l => l.StartsWith("TEST_FAIL ", StringComparison.Ordinal));
            var startedNames = lines.Where(l => l.StartsWith("TEST_START ", StringComparison.Ordinal)).Select(l => l[11..]).Order(StringComparer.Ordinal);
            var passedNames = lines.Where(l => l.StartsWith("TEST_PASS ", StringComparison.Ordinal)).Select(l => l[10..]).Order(StringComparer.Ordinal);
            bool paired = startedNames.SequenceEqual(passedNames, StringComparer.Ordinal);
            int exit = process.ExitCode;
            if (!process.Succeeded || starts == 0 || failed != 0 || !paired || error.Length != 0) exit = 1;
            return new(id, lane.ToString(), exit, process.TimedOut, time.Elapsed.TotalSeconds, log, starts, passes, failed,
                processFailure ?? (!paired ? "unpaired_case_markers" : null))
                { ProcessId = File.Exists(log + ".worker.lock") && int.TryParse(File.ReadAllText(log + ".worker.lock"), out int pid) ? pid : 0 };
        }
        catch (Exception error)
        {
            File.AppendAllText(log, Environment.NewLine + error);
            return new(id, lane.ToString(), 1, false, time.Elapsed.TotalSeconds, log, 0, 0, 1, error.Message);
        }
    }
}
