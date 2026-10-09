using System.Diagnostics;
using System.Reflection;
using System.Text;

internal static class HostTestRunner
{
    internal sealed record Fixture(string Id, MethodInfo Method);

    internal static Fixture[] Discover() => typeof(HostTestRunner).Assembly.GetTypes()
        .SelectMany(type => type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
        .Where(method => method.IsDefined(typeof(HostFixtureAttribute), false))
        .Select(method => method.ReturnType == typeof(void) && method.GetParameters().Length == 0
            ? new Fixture(method.DeclaringType!.FullName + "." + method.Name, method)
            : throw new InvalidOperationException("Invalid fixture signature: " + method))
        .OrderBy(fixture => fixture.Id, StringComparer.Ordinal).ToArray();

    internal static int RunMain(string[] args)
    {
        try { return Run(args); }
        catch (Exception error)
        {
            Console.WriteLine("TEST_FAIL host.tests.runner " + error.Message.ReplaceLineEndings(" "));
            if (!args.Contains("--fixture") && !args.Contains("--contracts"))
                Console.WriteLine("TEST_FAIL host.tests failures=1");
            return 1;
        }
    }

    private static int Run(string[] args)
    {
        Fixture[] fixtures = Discover();
        if (args is ["--execute", string childId])
        {
            Fixture fixture = fixtures.Single(f => f.Id == childId);
            try { fixture.Method.Invoke(null, null); return 0; }
            catch (Exception error)
            {
                Console.Error.WriteLine(error is TargetInvocationException ? error.InnerException : error);
                return 1;
            }
        }
        if (args is ["--list"])
        {
            foreach (Fixture fixture in fixtures) Console.WriteLine(fixture.Id);
            return 0;
        }
        ValidateCompiledInputs();

        string? resume = null, results = null;
        bool contractsOnly = false;
        List<string> selected = [];
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--contracts": contractsOnly = true; break;
                case "--fixture" when i + 1 < args.Length: selected.Add(args[++i]); break;
                case "--resume" when i + 1 < args.Length: resume = args[++i]; break;
                case "--results" when i + 1 < args.Length: results = args[++i]; break;
                default: throw new ArgumentException("Unknown or incomplete option: " + args[i]);
            }
        }
        if (resume != null && results != null) throw new ArgumentException("Use --resume or --results, not both.");
        foreach (string id in selected)
            if (!fixtures.Any(f => f.Id == id)) throw new ArgumentException("Unknown fixture: " + id + "; use --list.");
        if (selected.Count != 0) fixtures = fixtures.Where(f => selected.Contains(f.Id, StringComparer.Ordinal)).ToArray();
        if (fixtures.Length == 0) throw new InvalidOperationException("No fixtures selected.");

        string root = HostToolProcess.FindRepositoryRoot();
        Directory.SetCurrentDirectory(root);
        // Source checks run on every invocation: they are cheap and expose all stale contracts together.
        int failures = Contracts(selected.Count == 0 ? null : fixtures.Select(f => f.Method.DeclaringType!.FullName!).ToHashSet());
        if (contractsOnly || failures != 0)
        {
            Console.WriteLine((failures == 0 ? "TEST_PASS" : "TEST_FAIL") + " host.tests.contracts failures=" + failures);
            if (!contractsOnly && selected.Count == 0) Console.WriteLine("TEST_FAIL host.tests failures=" + failures);
            return failures == 0 ? 0 : 1;
        }

        if (resume != null && Path.GetFileName(resume) == "result.json") resume = Path.GetDirectoryName(Path.GetFullPath(resume));
        string directory = Path.GetFullPath(resume ?? results ?? Path.Combine(root, "artifacts", "host-suite", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..8]));
        using HostRunCheckpoint checkpoint = new(root, directory, resume != null);
        Console.WriteLine("HOST_RUN_RECEIPT " + Path.Combine(directory, "result.json"));
        int executed = 0, reused = 0;
        foreach (Fixture fixture in fixtures)
        {
            string source = fixture.Method.GetCustomAttribute<HostFixtureAttribute>()!.SourcePath;
            string fingerprint = checkpoint.Fingerprint(Path.GetFileNameWithoutExtension(source));
            if (checkpoint.TryReuse(fixture.Id, fingerprint, out string origin))
            {
                reused++;
                Console.WriteLine("HOST_FIXTURE_REUSED " + fixture.Id + " origin=" + origin);
                continue;
            }
            executed++;
            Console.WriteLine("HOST_FIXTURE_PENDING " + fixture.Id + " reason=" + origin);
            string log = checkpoint.Start(fixture.Id, fingerprint);
            Console.WriteLine("HOST_FIXTURE_START " + fixture.Id + " log=" + log);
            int exit = ExecuteChild(fixture.Id, log, process => checkpoint.ChildStarted(fixture.Id, process), out bool failureMarker, out bool cancelled);
            bool passed = exit == 0 && !failureMarker;
            checkpoint.Finish(fixture.Id, passed);
            if (!passed) failures++;
            Console.WriteLine("HOST_FIXTURE_" + (passed ? "PASS " : "FAIL ") + fixture.Id + " exit=" + exit);
            if (cancelled) return 130;
        }
        checkpoint.Complete(fixtures.Select(f => f.Id).ToArray(), selected.Count == 0, failures, executed, reused);
        Console.WriteLine($"HOST_RUN_SUMMARY selected={fixtures.Length} executed={executed} reused={reused} failures={failures}");
        string scope = selected.Count == 0 ? "host.tests" : "host.tests.selected";
        Console.WriteLine((failures == 0 ? "TEST_PASS " : "TEST_FAIL ") + scope + (failures == 0 ? "" : " failures=" + failures));
        return failures == 0 ? 0 : 1;
    }

    private static void ValidateCompiledInputs()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "OS.HostTests.inputs.sha256");
        if (!File.Exists(path)) throw new InvalidOperationException("Missing compiled-input manifest; rebuild OS.HostTests.");
        HashSet<string> compiled = new(StringComparer.OrdinalIgnoreCase);
        foreach (string line in File.ReadLines(path))
        {
            int separator = line.LastIndexOf('|');
            if (separator < 1 || !File.Exists(line[..separator]) || HostRunCheckpoint.HashFile(line[..separator]) != line[(separator + 1)..])
                throw new InvalidOperationException("Compiled inputs changed; rebuild OS.HostTests before running: " + (separator > 0 ? line[..separator] : path));
            compiled.Add(line[..separator]);
        }
        string root = HostToolProcess.FindRepositoryRoot();
        foreach (string relative in new[] { "tests/OS.HostTests", "src/OS.Kernel", "std/no-runtime/shared", "tools/OS.ImageBuilder", "tools/OS.LibvirtRunner", "tools/OS.ManifestTool", "tools/OS.QemuRunner" })
        foreach (string source in Directory.GetFiles(Path.GetFullPath(Path.Combine(root, relative)), "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) && !p.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)))
            if (!compiled.Contains(source)) throw new InvalidOperationException("New source is not compiled; rebuild OS.HostTests: " + source);
    }

    private static int Contracts(HashSet<string>? selected)
    {
        Assembly assembly = typeof(HostTestRunner).Assembly;
        using Stream stream = assembly.GetManifestResourceStream("OS.HostTests.HostSourceChecks.txt")
            ?? throw new InvalidOperationException("Missing source-check catalog.");
        using StreamReader reader = new(stream);
        int failures = 0, count = 0;
        foreach (string id in reader.ReadToEnd().Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            int separator = id.LastIndexOf('.');
            string typeName = id[..separator];
            if (selected != null && !selected.Contains(typeName)) continue;
            count++;
            Console.WriteLine("TEST_START host.contract." + id);
            try
            {
                MethodInfo method = assembly.GetType(typeName)?.GetMethod(id[(separator + 1)..], BindingFlags.Static | BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException("Missing catalog method: " + id);
                bool takesRoot = method.GetParameters() is [var parameter] && parameter.ParameterType == typeof(string);
                if ((!takesRoot && method.GetParameters().Length != 0) || (method.ReturnType != typeof(bool) && method.ReturnType != typeof(void)))
                    throw new InvalidOperationException("Invalid contract signature: " + id);
                object? result = method.Invoke(null, takesRoot ? [HostToolProcess.FindRepositoryRoot()] : null);
                if (result is false) throw new InvalidOperationException("predicate returned false");
                Console.WriteLine("TEST_PASS host.contract." + id);
            }
            catch (Exception error)
            {
                failures++;
                Exception cause = error is TargetInvocationException && error.InnerException != null ? error.InnerException : error;
                Console.WriteLine("TEST_FAIL host.contract." + id + " " + cause.Message.ReplaceLineEndings(" "));
            }
        }
        Console.WriteLine($"HOST_CONTRACT_SUMMARY selected={count} failures={failures}");
        return failures;
    }

    private static int ExecuteChild(string id, string logPath, Action<Process> started, out bool failureMarker, out bool cancelled)
    {
        ProcessStartInfo start = new("dotnet") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(typeof(HostTestRunner).Assembly.Location);
        start.ArgumentList.Add("--execute");
        start.ArgumentList.Add(id);
        using StreamWriter log = new(logPath, false, new UTF8Encoding(false)) { AutoFlush = true };
        object sync = new();
        bool failed = false;
        using Process process = new() { StartInfo = start };
        void Write(string? line)
        {
            if (line == null) return;
            lock (sync)
            {
                log.WriteLine(line);
                Console.WriteLine(line);
                if (line.StartsWith("TEST_FAIL ", StringComparison.Ordinal)) failed = true;
            }
        }
        process.OutputDataReceived += (_, e) => Write(e.Data);
        process.ErrorDataReceived += (_, e) => Write(e.Data);
        bool interrupted = false;
        ConsoleCancelEventHandler cancel = (_, e) =>
        {
            e.Cancel = true;
            interrupted = true;
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        };
        bool launched = false;
        try
        {
            process.Start();
            launched = true;
            started(process);
            Console.CancelKeyPress += cancel;
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();
        }
        finally
        {
            Console.CancelKeyPress -= cancel;
            // A failed checkpoint write or pipe setup must not leave a detached fixture behind.
            if (launched && !process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
            }
        }
        failureMarker = failed;
        cancelled = interrupted;
        return process.ExitCode;
    }
}
