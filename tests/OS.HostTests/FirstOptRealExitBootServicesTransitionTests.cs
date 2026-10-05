using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using OS.Boot;
using OS.Kernel.Platform;
using Transition = OS.Boot.FirstOptRealExitBootServicesTransition;

internal static unsafe class FirstOptRealExitBootServicesTransitionTests
{
    private static ulong captureStatus, exitStatus;
    private static int maps, exits, continuations, reentries;
    private static FirstExitTransitionState* active;

    [ModuleInitializer]
    internal static void RunAll()
    {
        Run("admission and failed exits cannot transfer", Failures);
        Run("successful exit transfers once and cannot return to its caller", TerminalTransfer);
    }

    private static void Run(string name, Action action)
    {
        Console.WriteLine("TEST_START m03.007.coordinator " + name);
        try { action(); Console.WriteLine("TEST_PASS m03.007.coordinator " + name); }
        catch (Exception e) { Console.WriteLine("TEST_FAIL m03.007.coordinator " + name + " " + e.Message); throw; }
    }
    private static void Need(bool value, string reason)
    { if (!value) throw new InvalidOperationException(reason); }

    [UnmanagedCallersOnly]
    private static ulong Map(ulong* bytes, void* buffer, ulong* key, ulong* width, uint* version)
    {
        maps++; *bytes = 40; *key = (ulong)maps; *width = 40; *version = 1;
        if (Transition.AttemptAndTransfer(ref *active, FirmwareServicesMode.BootServicesAvailable,
            null, null, null, null, 0, null, null, 0) == FirstExitReturn.ReentryRejected) reentries++;
        return captureStatus;
    }
    [UnmanagedCallersOnly]
    private static ulong Exit(void* image, ulong key)
    { exits++; return (ulong)image == 123 && key == (ulong)maps ? exitStatus : ulong.MaxValue; }
    [UnmanagedCallersOnly]
    private static void Continuation(FirstExitTransitionState* state, void* context)
    { continuations++; }

    private static FirstExitReturn Invoke(ref FirstExitTransitionState state, byte* buffer, void* context)
    {
        fixed (FirstExitTransitionState* pointer = &state)
        {
            active = pointer;
            return Transition.AttemptAndTransfer(ref state, FirmwareServicesMode.BootServicesAvailable,
                &Map, &Exit, (void*)123, buffer, 128, &Continuation, context, 8);
        }
    }

    private static void Failures()
    {
        Need(!RuntimeHelpers.IsReferenceOrContainsReferences<FirstExitTransitionState>(), "POD state");
        byte* buffer = stackalloc byte[128]; ulong context = 1;
        FirstExitTransitionState state = default;
        Need(Transition.AttemptAndTransfer(ref state, FirmwareServicesMode.BootServicesExited,
            &Map, &Exit, (void*)123, buffer, 128, &Continuation, &context, 8) == FirstExitReturn.FirmwareUnavailable,
            "firmware unavailable");
        Need(Transition.AttemptAndTransfer(ref state, FirmwareServicesMode.BootServicesAvailable,
            &Map, &Exit, (void*)123, buffer, 128, null, &context, 8) == FirstExitReturn.InvalidContinuation,
            "missing continuation");
        foreach (ulong badContext in new[] { 0UL, (ulong)buffer, (ulong)&state, ulong.MaxValue - 3 })
            Need(Invoke(ref state, buffer, (void*)badContext) == FirstExitReturn.InvalidStorage, "invalid/overlapping context");
        Need(Invoke(ref state, (byte*)&state, &context) == FirstExitReturn.InvalidStorage, "map overlaps state");
        Need(state.Phase == FirstExitPhase.Empty && maps == 0 && exits == 0 && continuations == 0,
            "admission is side-effect free");

        foreach (var scenario in new[] { (0x8000000000000005UL, 0UL, 0), (0UL, 7UL, 1), (0UL, 0x8000000000000002UL, 2) })
        {
            state = default; maps = exits = continuations = reentries = 0;
            captureStatus = scenario.Item1; exitStatus = scenario.Item2;
            Need(Invoke(ref state, buffer, &context) == FirstExitReturn.ExitNotCompleted, "failed exit returns failure");
            Need(state.Phase == FirstExitPhase.Failed && state.Mode == FirmwareServicesMode.BootServicesAvailable &&
                !state.Attempt.Succeeded && state.Attempt.ExitCalls == (uint)scenario.Item3 &&
                exits == scenario.Item3 && continuations == 0 && reentries == maps && state.RetainedFirmwareCallbacksCleared,
                "failed exit never adopts; captures/retries retained but callbacks cleared");
            int priorMaps = maps;
            Need(Invoke(ref state, buffer, &context) == FirstExitReturn.AlreadyConsumed && maps == priorMaps,
                "consumed attempt cannot call firmware again");
        }
    }

    private static void TerminalTransfer()
    {
        string directory = Path.Combine(HostToolProcess.FindRepositoryRoot(), "artifacts/issue64-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string kernel = SecurityElement.Escape(typeof(Transition).Assembly.Location)!;
        File.WriteAllText(Path.Combine(directory, "Worker.csproj"), $$"""
            <Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>
            <TargetFramework>net10.0</TargetFramework><LangVersion>14.0</LangVersion>
            <AllowUnsafeBlocks>true</AllowUnsafeBlocks><ImplicitUsings>enable</ImplicitUsings>
            </PropertyGroup><ItemGroup><Reference Include="OS.Kernel"><HintPath>{{kernel}}</HintPath>
            </Reference></ItemGroup></Project>
            """, new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(directory, "Program.cs"), Worker, new UTF8Encoding(false));
        Execute(directory, "build", ["build", "Worker.csproj", "--nologo"], "Build succeeded.");
        foreach (string scenario in new[] { "one", "retry", "return" })
            Execute(directory, scenario, ["bin/Debug/net10.0/Worker.dll", scenario], "TEST_PASS transfer." + scenario);
    }

    private static void Execute(string directory, string name, string[] arguments, string marker)
    {
        var start = new ProcessStartInfo("dotnet") { WorkingDirectory = directory, UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (string arg in arguments) start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        bool ended = process.WaitForExit(60_000);
        if (!ended) { process.Kill(true); process.WaitForExit(); }
        string stdout = output.GetAwaiter().GetResult(), stderr = error.GetAwaiter().GetResult();
        File.WriteAllText(Path.Combine(directory, name + ".stdout.log"), stdout);
        File.WriteAllText(Path.Combine(directory, name + ".stderr.log"), stderr);
        Need(ended && process.ExitCode == 0 && stderr.Length == 0 && stdout.Contains(marker, StringComparison.Ordinal),
            name + ": " + stdout + stderr);
    }

    // Controlled host callbacks only. Environment.Exit terminates the owned test
    // process, not firmware; the return case observes the coordinator's terminal
    // fallback from a host thread. No privileged instruction runs in these tests.
    private const string Worker = """
        using System.Runtime.InteropServices;
        using OS.Boot;
        using OS.Kernel.Platform;
        unsafe class Program
        {
            static string scenario = "";
            static int maps, exits;
            static FirstExitTransitionState* active;
            static int Main(string[] args)
            {
                scenario = args[0];
                FirstExitTransitionState state = default; active = &state;
                byte* buffer = stackalloc byte[128]; ulong context = 0xC1A05;
                FirstOptRealExitBootServicesTransition.AttemptAndTransfer(ref state,
                    FirmwareServicesMode.BootServicesAvailable, &Map, &Exit, (void*)123,
                    buffer, 128, &Owned, &context, 8);
                Console.WriteLine("TEST_FAIL returned_to_efi_caller");
                return 91;
            }
            [UnmanagedCallersOnly]
            static ulong Map(ulong* bytes, void* buffer, ulong* key, ulong* width, uint* version)
            { maps++; *bytes=40; *key=(ulong)maps; *width=40; *version=1; return 0; }
            [UnmanagedCallersOnly]
            static ulong Exit(void* handle, ulong key)
            { exits++; return scenario=="retry" && exits==1 ? 0x8000000000000002UL : 0; }
            [UnmanagedCallersOnly]
            static void Owned(FirstExitTransitionState* state, void* context)
            {
                int expected=scenario=="retry" ? 2 : 1;
                if(state!=active || *(ulong*)context!=0xC1A05 || maps!=expected || exits!=expected ||
                    state->Phase!=FirstExitPhase.OwnedContinuation || state->Mode!=FirmwareServicesMode.BootServicesExited ||
                    !state->Attempt.Succeeded || state->Attempt.ExitCalls!=(uint)expected || !state->RetainedFirmwareCallbacksCleared ||
                    FirstOptRealExitBootServicesTransition.AttemptAndTransfer(ref *state, state->Mode,
                        null,null,null,null,0,null,null,0)!=FirstExitReturn.AlreadyConsumed)
                    Environment.Exit(92);
                if(scenario=="return")
                {
                    new Thread(() => {
                        while(true) { Thread.MemoryBarrier(); if(active->Phase==FirstExitPhase.UnexpectedContinuationReturn) break; Thread.Yield(); }
                        Console.WriteLine("TEST_PASS transfer.return"); Environment.Exit(0);
                    }) { IsBackground=true }.Start();
                    return;
                }
                Console.WriteLine("TEST_PASS transfer."+scenario); Environment.Exit(0);
            }
        }
        """;
}
