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
        Run("permanent CR3 preparation and emitted continuation", PermanentContinuation);
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

    [UnmanagedCallersOnly]
    private static void Diagnostic(FirstExitTransitionState* state, FirstExitPermanentCr3Context* context) { }

    private static bool Prepare(ref FirstExitPermanentCr3Context context,
        ulong firmware = 0xF801000, ulong owned = 0x100000, ulong cr4 = 0x668,
        ulong bottom = 0x200008, ulong top = 0x240000, ulong depth = 16384)
        => FirstExitPermanentCr3Stub.Prepare(ref context, firmware, owned, cr4,
            bottom, top, depth, 0x1122334455667788, 0x8877665544332211, &Diagnostic, (void*)123);

    private static void PermanentContinuation()
    {
        Need(!RuntimeHelpers.IsReferenceOrContainsReferences<FirstExitPermanentCr3Context>() &&
            sizeof(FirstExitPermanentCr3Context) == FirstExitPermanentCr3Stub.ContextBytes, "POD context size");
        (string Field, int Offset)[] layout = [("FirmwareCr3", 0), ("OwnedCr3", 8), ("FirmwareCr4", 16),
            ("StackBottom", 24), ("StackTop", 32), ("MinimumRsp", 40), ("LowerCanary", 48), ("UpperCanary", 56),
            ("Diagnostic", 64), ("DiagnosticContext", 72), ("ObservedCr3", 80), ("ObservedCr4", 88),
            ("Phase", 96), ("ObservedRsp", 104)];
        foreach (var field in layout)
            Need((int)Marshal.OffsetOf<FirstExitPermanentCr3Context>(field.Field) == field.Offset, "native offset " + field.Field);
        Need((int)Marshal.OffsetOf<FirstExitTransitionState>("PhaseValue") == 0 &&
            (int)Marshal.OffsetOf<FirstExitTransitionState>("ModeValue") == 4 &&
            (uint)FirstExitPhase.OwnedContinuation == 3 && (uint)FirmwareServicesMode.BootServicesExited == 2,
            "native exit-state guard layout");
        FirstExitPermanentCr3Context context = default;
        Need(Prepare(ref context) && context.Phase == FirstExitAdoptionPhase.Prepared &&
            context.MinimumRsp == 0x204048 && context.DiagnosticContext == (void*)123 &&
            context.ObservedCr3 == 0 && context.ObservedCr4 == 0 && context.ObservedRsp == 0, "prepared bounds");
        Need(!Prepare(ref context), "cannot reset prepared context");
        foreach (ulong firmware in new[] { 0UL, 0x18UL, 0x1000_0000_0UL, 0xF801001UL })
        { context = default; Need(!Prepare(ref context, firmware: firmware) && context.Phase == 0, "invalid firmware root"); }
        foreach (ulong owned in new[] { 0UL, 0x100001UL, 0xF801000UL, 0x1000_0000_0UL })
        { context = default; Need(!Prepare(ref context, owned: owned) && context.Phase == 0, "invalid owned root"); }
        foreach (ulong cr4 in new[] { 0UL, 0x648UL, 0x1668UL, 0x20668UL, 0x800668UL })
        { context = default; Need(!Prepare(ref context, cr4: cr4) && context.Phase == 0, "unsupported processor mode"); }
        foreach (var stack in new[] { (0UL, 0x240000UL, 1UL), (0x200001UL, 0x240000UL, 1UL),
            (0x200008UL, 0x240001UL, 1UL), (0x200008UL, 0x200008UL, 1UL),
            (0x200008UL, 0x240000UL, 0UL), (0x200008UL, 0x240000UL, ulong.MaxValue),
            (0x200008UL, 0x240000UL, 0x3ffb8UL), (0x200008UL, 0x800000000000UL, 1UL) })
        { context = default; Need(!Prepare(ref context, bottom: stack.Item1, top: stack.Item2, depth: stack.Item3) &&
            context.Phase == 0, "invalid stack extent or headroom"); }
        context = default;
        ulong contextAddress = (ulong)&context;
        Need(!Prepare(ref context, bottom: contextAddress, top: contextAddress + 0x10000), "context overlaps live stack");
        Need(!FirstExitPermanentCr3Stub.Prepare(ref context, 0xF801000, 0x100000, 0x668,
            0x200008, 0x240000, 16384, 1, 2, null, null), "missing diagnostic");

        uint bytes = FirstExitPermanentCr3Stub.RequiredBytes;
        byte* output = stackalloc byte[(int)bytes + 2];
        new Span<byte>(output, (int)bytes + 2).Fill(0xa5);
        Need(!FirstExitPermanentCr3Stub.Emit(output + 1, bytes - 1, out uint written) && written == 0 &&
            new ReadOnlySpan<byte>(output, (int)bytes + 2).IndexOfAnyExcept((byte)0xa5) == -1,
            "short destination unchanged");
        Need(!FirstExitPermanentCr3Stub.Emit(null, bytes, out written) && written == 0, "null destination");
        Need(FirstExitPermanentCr3Stub.Emit(output + 1, bytes, out written) && written == bytes &&
            output[0] == 0xa5 && output[bytes + 1] == 0xa5, "exact bounded emission");
        byte[] raw = new ReadOnlySpan<byte>(output + 1, (int)bytes).ToArray();
        Need(raw.AsSpan(0, 2).SequenceEqual(new byte[] { 0xfa, 0xfc }) &&
            raw.AsSpan(raw.Length - 4).SequenceEqual(new byte[] { 0xfa, 0xf4, 0xeb, 0xfc }), "terminal CLI/HLT loop");
        // Preserve actual emitter output for independent decoding, never execute
        // privileged instructions in the host test process.
        string directory = Path.Combine(HostToolProcess.FindRepositoryRoot(), "artifacts/issue64/permanent-stub");
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, "continuation.bin"), raw);
    }

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
