using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Boot;
using OS.Kernel.Platform;
using Adapter = OS.Boot.TypedNoStdlibExitBootServicesCallAdapter;

internal static unsafe class TypedNoStdlibExitBootServicesCallAdapterTests
{
    private static int calls;
    private static ulong observedHandle, observedKey, returnedStatus;
    private static TypedExitBootServicesState* active;
    private static bool reentry, reentryPreserved;

    [HostFixture(HostLane.Native)]
    internal static void RunAll()
    {
        int failures = 0;
        Console.WriteLine("TEST_START m03.004");
        Run("default POD and measured layouts fail closed", Defaults, ref failures);
        Run("admission rejects missing and unavailable authority without writes", Admission, ref failures);
        Run("pre-call guard denial preserves ready state", GuardDenial, ref failures);
        Run("typed callback receives exact opaque handle and full map key", Forwarding, ref failures);
        Run("every firmware status is preserved without narrowing", Statuses, ref failures);
        Run("success and failure both consume one attempt", Reuse, ref failures);
        Run("in-flight callback cannot reenter or rearm", Reentry, ref failures);
        Run("malformed and incomplete state cannot issue a call", Malformed, ref failures);
        Run("adapter leaves firmware guard ownership unchanged", GuardIsolation, ref failures);
        Run("warmed complete attempts allocate zero managed bytes", Allocation, ref failures);
        Run("actual four-source no-stdlib closure has zero references", NoStdLib, ref failures);
        if (failures != 0)
        {
            Console.WriteLine("TEST_FAIL m03.004 failures=" + failures);
            throw new InvalidOperationException("m03.004 host adapter failures=" + failures);
        }
        Console.WriteLine("CLROS_M03_004_READY hostOnly=true controlledCallbacks=true oneAttempt=true fullKey64=true fullStatus64=true globalFirmwareModeUnchanged=true realExitBootServices=false");
        Console.WriteLine("TEST_PASS m03.004");
    }

    private static void Run(string name, Action test, ref int failures)
    {

        HostCheck.Run("m03.004 " + name, test, ref failures);
    }
    private static void Check(bool condition, string detail)
    {
        if (!condition) throw new InvalidOperationException(detail);
    }
    private static void Reset(ulong status = 0)
    {
        calls = 0; observedHandle = 0; observedKey = 0; returnedStatus = status;
        active = null; reentry = false; reentryPreserved = false;
    }
    private static byte[] Bytes(ref TypedExitBootServicesState state) =>
        MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref state, 1)).ToArray();
    private static void Unchanged(ref TypedExitBootServicesState state, byte[] before) =>
        Check(MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref state, 1)).SequenceEqual(before), "retained state changed on refusal");
    private static void Prepare(ref TypedExitBootServicesState state, void* handle) =>
        Check(Adapter.Prepare(ref state, FirmwareServicesMode.BootServicesAvailable, &Callback, handle)
            == TypedExitBootServicesStatus.Prepared, "prepare");

    [UnmanagedCallersOnly]
    private static ulong Callback(void* handle, ulong key)
    {
        calls++; observedHandle = (ulong)handle; observedKey = key;
        if (reentry && active != null)
        {
            TypedExitBootServicesState before = *active;
            TypedExitBootServicesResult nested = Adapter.Attempt(ref *active,
                FirmwareServicesMode.BootServicesAvailable, 0xFEDCBA9876543210);
            TypedExitBootServicesStatus prepare = Adapter.Prepare(ref *active,
                FirmwareServicesMode.BootServicesAvailable, &Callback, handle);
            bool equal = true;
            for (int i = 0; i < sizeof(TypedExitBootServicesState); i++)
                equal &= ((byte*)&before)[i] == ((byte*)active)[i];
            reentryPreserved = before.Phase == TypedExitBootServicesPhase.InFlight
                && before.MapKey == key && nested.Status == TypedExitBootServicesStatus.ReentryRejected
                && !nested.CallIssued && !nested.Succeeded && nested.FirmwareStatus == 0
                && prepare == TypedExitBootServicesStatus.ReentryRejected && equal;
        }
        return returnedStatus;
    }

    private static void Defaults()
    {
        Reset();
        Check(sizeof(TypedExitBootServicesState) == 40 && sizeof(TypedExitBootServicesResult) == 16, "x64 layout");
        Check(!RuntimeHelpers.IsReferenceOrContainsReferences<TypedExitBootServicesState>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<TypedExitBootServicesResult>(), "POD fields");
        TypedExitBootServicesResult result = default;
        Check(!result.Succeeded && !result.CallIssued && !result.MapKeyRejected, "default result");
        TypedExitBootServicesState state = default; byte[] before = Bytes(ref state);
        result = Adapter.Attempt(ref state, default, 0);
        Check(result.Status == TypedExitBootServicesStatus.NotPrepared && !result.CallIssued && calls == 0,
            "default available mode does not admit default state");
        Unchanged(ref state, before);
        Console.WriteLine("M03_004_LAYOUT stateBytes=40 resultBytes=16 pointerBytes=" + sizeof(void*));
    }

    private static void Admission()
    {
        Reset(); byte handleByte = 0;
        TypedExitBootServicesState state = default; byte[] before = Bytes(ref state);
        Check(Adapter.Prepare(ref state, default, null, &handleByte) == TypedExitBootServicesStatus.MissingFunctionPointer, "null callback");
        Unchanged(ref state, before);
        Check(Adapter.Prepare(ref state, default, &Callback, null) == TypedExitBootServicesStatus.MissingImageHandle, "null handle");
        Unchanged(ref state, before);
        foreach (FirmwareServicesMode mode in new[] { FirmwareServicesMode.UefiGoneSimulated,
            FirmwareServicesMode.BootServicesExited, (FirmwareServicesMode)99 })
        {
            Check(Adapter.Prepare(ref state, mode, &Callback, &handleByte) == TypedExitBootServicesStatus.FirmwareServicesUnavailable, "guarded admission");
            Unchanged(ref state, before);
        }
        Check(calls == 0, "admission did not call");
    }

    private static void GuardDenial()
    {
        Reset(); byte handleByte = 0; TypedExitBootServicesState state = default;
        Prepare(ref state, &handleByte); byte[] before = Bytes(ref state);
        foreach (FirmwareServicesMode mode in new[] { FirmwareServicesMode.UefiGoneSimulated,
            FirmwareServicesMode.BootServicesExited, (FirmwareServicesMode)(-1) })
        {
            TypedExitBootServicesResult result = Adapter.Attempt(ref state, mode, ulong.MaxValue);
            Check(result.Status == TypedExitBootServicesStatus.FirmwareServicesUnavailable
                && result.Mode == mode && !result.CallIssued && result.FirmwareStatus == 0 && calls == 0, "pre-call denial");
            Unchanged(ref state, before);
        }
        Check(Adapter.Attempt(ref state, default, 0).Succeeded && calls == 1, "refusal did not consume ready state");
    }

    private static void Forwarding()
    {
        // Opaque tokens are only compared by the callback, never dereferenced.
        ulong[] handles = [1, 0x0000000100000001, 0xFEDCBA9876543210];
        ulong[] keys = [0, 1, 0x0000000100000000, 0x8000000000000000, 0xFEDCBA9876543210, ulong.MaxValue];
        foreach (ulong handle in handles) foreach (ulong key in keys)
        {
            Reset(); TypedExitBootServicesState state = default; Prepare(ref state, (void*)handle);
            TypedExitBootServicesResult result = Adapter.Attempt(ref state, default, key);
            Check(result.CallIssued && result.Succeeded && calls == 1 && observedHandle == handle && observedKey == key,
                "exact handle/key forwarding");
            Check(state.ImageHandle == handle && state.MapKey == key && state.Phase == TypedExitBootServicesPhase.Returned, "retained association");
        }
    }

    private static void Statuses()
    {
        ulong[] values = [0, 1, 2, 0x8000000000000002, 0x8000000000000005,
            0x8000000000000007, 0x1234567800000000, 0xFEDCBA9876543210, ulong.MaxValue];
        byte handleByte = 0;
        foreach (ulong status in values)
        {
            Reset(status); TypedExitBootServicesState state = default; Prepare(ref state, &handleByte);
            TypedExitBootServicesResult result = Adapter.Attempt(ref state, default, 0);
            Check(result.Status == TypedExitBootServicesStatus.Returned && result.CallIssued && calls == 1
                && result.FirmwareStatus == status && state.FirmwareStatus == status
                && result.Succeeded == (status == 0) && result.MapKeyRejected == (status == 0x8000000000000002), "full status");
        }
    }

    private static void Reuse()
    {
        byte handleByte = 0;
        foreach (ulong status in new[] { 0UL, 0x8000000000000002UL, ulong.MaxValue })
        {
            Reset(status); TypedExitBootServicesState state = default; Prepare(ref state, &handleByte);
            byte[] ready = Bytes(ref state);
            Check(Adapter.Prepare(ref state, default, &Callback, &handleByte) == TypedExitBootServicesStatus.AlreadyPrepared, "double prepare");
            Unchanged(ref state, ready);
            Adapter.Attempt(ref state, default, 0xFEEDFACE00000000);
            byte[] completed = Bytes(ref state);
            TypedExitBootServicesResult again = Adapter.Attempt(ref state, default, 1);
            Check(again.Status == TypedExitBootServicesStatus.AlreadyAttempted && !again.CallIssued
                && !again.Succeeded && again.FirmwareStatus == 0 && calls == 1, "reuse denied separately from firmware status");
            Unchanged(ref state, completed);
            Check(Adapter.Prepare(ref state, default, &Callback, &handleByte) == TypedExitBootServicesStatus.AlreadyAttempted, "no rearm");
            Unchanged(ref state, completed);
        }
    }

    private static void Reentry()
    {
        Reset(0xAABBCCDD12345678); byte handleByte = 0; TypedExitBootServicesState state = default;
        Prepare(ref state, &handleByte); active = &state; reentry = true;
        TypedExitBootServicesResult result = Adapter.Attempt(ref state, default, 0x1122334455667788);
        active = null; reentry = false;
        Check(reentryPreserved && calls == 1 && result.FirmwareStatus == 0xAABBCCDD12345678
            && state.Phase == TypedExitBootServicesPhase.Returned, "synchronous reentry refused before second callback");
    }

    private static void Malformed()
    {
        Reset(); byte handleByte = 0;
        int phase = (int)Marshal.OffsetOf<TypedExitBootServicesState>("PhaseValue");
        int function = (int)Marshal.OffsetOf<TypedExitBootServicesState>("Function");
        int handle = (int)Marshal.OffsetOf<TypedExitBootServicesState>("Handle");
        int key = (int)Marshal.OffsetOf<TypedExitBootServicesState>("Key");
        int status = (int)Marshal.OffsetOf<TypedExitBootServicesState>("ReturnedStatus");
        foreach (int offset in new[] { phase, function, handle, key, status })
        {
            TypedExitBootServicesState state = default; Prepare(ref state, &handleByte);
            byte* p = (byte*)&state;
            if (offset == phase) *(uint*)(p + offset) = 99;
            else *(ulong*)(p + offset) = offset == function || offset == handle ? 0UL : 1UL;
            byte[] before = Bytes(ref state);
            TypedExitBootServicesStatus expected = offset == function ? TypedExitBootServicesStatus.MissingFunctionPointer
                : offset == handle ? TypedExitBootServicesStatus.MissingImageHandle : TypedExitBootServicesStatus.InvalidState;
            Check(Adapter.Attempt(ref state, default, 0).Status == expected && calls == 0, "malformed authority");
            Unchanged(ref state, before);
        }
        TypedExitBootServicesState poison = default; *((byte*)&poison + key) = 1;
        byte[] original = Bytes(ref poison);
        Check(Adapter.Prepare(ref poison, default, &Callback, &handleByte) == TypedExitBootServicesStatus.InvalidState, "dirty empty admission");
        Unchanged(ref poison, original);
    }

    private static void GuardIsolation()
    {
        byte handleByte = 0;
        foreach (ulong status in new[] { 0UL, 0x8000000000000002UL })
        {
            Reset(status); FirmwareServicesGuard guard = new(); TypedExitBootServicesState state = default;
            Check(Adapter.Prepare(ref state, guard.Mode, &Callback, &handleByte) == TypedExitBootServicesStatus.Prepared, "guard available");
            Adapter.Attempt(ref state, guard.Mode, 0);
            Check(guard.Mode == FirmwareServicesMode.BootServicesAvailable, "adapter did not mark global exit");
        }
        Reset(); FirmwareServicesGuard denied = new(); denied.EnterUefiGoneSimulation();
        TypedExitBootServicesState unused = default;
        Check(Adapter.Prepare(ref unused, denied.Mode, &Callback, &handleByte) == TypedExitBootServicesStatus.FirmwareServicesUnavailable
            && denied.Mode == FirmwareServicesMode.UefiGoneSimulated && calls == 0, "guard refusal unchanged");
    }

    private static void Allocation()
    {
        Reset(); byte handleByte = 0; bool okay = true;
        for (int i = 0; i < 1024; i++)
        {
            TypedExitBootServicesState warm = default;
            Adapter.Prepare(ref warm, default, &Callback, &handleByte); Adapter.Attempt(ref warm, default, (ulong)i);
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 4096; i++)
        {
            TypedExitBootServicesState state = default;
            okay &= Adapter.Prepare(ref state, default, &Callback, &handleByte) == TypedExitBootServicesStatus.Prepared;
            okay &= Adapter.Attempt(ref state, default, (ulong)i).Succeeded;
            okay &= Adapter.Attempt(ref state, default, 0).Status == TypedExitBootServicesStatus.AlreadyAttempted;
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(okay && allocated == 0 && calls == 5120, "warmed allocation bytes=" + allocated);
    }

    private static void NoStdLib()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string directory = Path.Combine(root, "artifacts", "host-tests", "m03-004-nostdlib-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string[] sources = ["src/OS/Runtime/MethodTable.cs",
            "src/OS/Runtime/RuntimeFailFast.cs",
            "src/OS/Runtime/Object.cs",
            "src/OS/Runtime/Void.cs",
            "src/OS/Runtime/Boolean.cs",
            "src/OS/Runtime/Char.cs",
            "src/OS/Runtime/SByte.cs",
            "src/OS/Runtime/Byte.cs",
            "src/OS/Runtime/Int16.cs",
            "src/OS/Runtime/UInt16.cs",
            "src/OS/Runtime/Int32.cs",
            "src/OS/Runtime/UInt32.cs",
            "src/OS/Runtime/Int64.cs",
            "src/OS/Runtime/UInt64.cs",
            "src/OS/Runtime/IntPtr.cs",
            "src/OS/Runtime/UIntPtr.cs",
            "src/OS/Runtime/Single.cs",
            "src/OS/Runtime/Double.cs",
            "src/OS/Runtime/ValueType.cs",
            "src/OS/Runtime/Enum.cs",
            "src/OS/Runtime/Type.cs",
            "src/OS/Runtime/RuntimeType.cs",
            "src/OS/Runtime/Array.cs",
            "src/OS/Runtime/Delegate.cs",
            "src/OS/Runtime/MulticastDelegate.cs",
            "src/OS/Runtime/String.cs",
            "src/OS/Runtime/Attribute.cs",
            "src/OS/Runtime/Buffer.cs",
            "src/OS/Runtime/SpanHelpers.cs",
            "src/OS/Runtime/AttributeTargets.cs",
            "src/OS/Runtime/AttributeUsageAttribute.cs",
            "src/OS/Runtime/RuntimeTypeHandle.cs",
            "src/OS/Runtime/RuntimeMethodHandle.cs",
            "src/OS/Runtime/RuntimeFieldHandle.cs",
            "src/OS/Runtime/RuntimeExportAttribute.cs",
            "src/OS/Runtime/X64StackProbeRuntime.cs",
            "src/OS/Runtime/TypeCast.cs",
            "src/OS/Runtime/RuntimeFeature.cs",
            "src/OS/Runtime/RuntimeHelpers.cs",
            "src/OS/Runtime/InAttribute.cs",
            "src/OS/Runtime/UnmanagedType.cs",
            "src/OS/Runtime/UnmanagedCallersOnlyAttribute.cs",
            "src/OS/Runtime/IDynamicInterfaceCastable.cs",
            "src/OS/Runtime/ThrowHelpers.cs",
            "src/OS/Runtime/StartupCodeHelpers.cs",
            "src/OS/Runtime/IntrinsicAttribute.cs",
            "src/OS/Runtime/Unsafe.cs",
            "src/OS/Runtime/MinimalRuntime.Array.cs",
            "src/OS/Runtime/Nullable.cs",
            "src/OS/Runtime/RhpNewFastRuntime.cs",
            "src/OS/Runtime/DefaultMemberAttribute.cs",
            "src/OS/Runtime/IndexerNameAttribute.cs",
            "src/OS/Runtime/Exception.cs",
            "src/OS/Runtime/RuntimeImports.cs", "src/OS/Runtime/MethodImplAttribute.MethodImplOptions.cs",
            "src/OS/Runtime/MethodImplAttribute.cs",
            "src/OS.Kernel/Platform/FirmwareServicesMode.cs", "src/OS.Kernel/Boot/TypedNoStdlibExitBootServicesCallAdapter.TypedExitBootServicesPhase.cs",
            "src/OS.Kernel/Boot/TypedNoStdlibExitBootServicesCallAdapter.TypedExitBootServicesStatus.cs",
            "src/OS.Kernel/Boot/TypedNoStdlibExitBootServicesCallAdapter.TypedExitBootServicesState.cs",
            "src/OS.Kernel/Boot/TypedNoStdlibExitBootServicesCallAdapter.TypedExitBootServicesResult.cs",
            "src/OS.Kernel/Boot/TypedNoStdlibExitBootServicesCallAdapter.cs"];
        var identities = sources.Select(p => new { Path = p, Bytes = new FileInfo(Path.Combine(root, p)).Length,
            Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(root, p)))) }).ToArray();
        File.WriteAllText(Path.Combine(directory, "inputs.json"), JsonSerializer.Serialize(identities));
        string includes = string.Join(Environment.NewLine, sources.Select(p => "<Compile Include=\"" + System.Security.SecurityElement.Escape(Path.Combine(root, p)) + "\" />"));
        string project = $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net11.0</TargetFramework><LangVersion>14.0</LangVersion><OutputType>Library</OutputType>
                <AssemblyName>M03_004_NoStdLib</AssemblyName><EnableDefaultCompileItems>false</EnableDefaultCompileItems>
                <AllowUnsafeBlocks>true</AllowUnsafeBlocks><NoStdLib>true</NoStdLib><NoConfig>true</NoConfig>
                <DefineConstants>NOSTDLIB_EFI</DefineConstants><DisableImplicitFrameworkReferences>true</DisableImplicitFrameworkReferences>
                <GenerateAssemblyInfo>false</GenerateAssemblyInfo><GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute>
                <ProduceReferenceAssembly>false</ProduceReferenceAssembly><RuntimeMetadataVersion>v4.0.30319</RuntimeMetadataVersion>
                <EnableNETAnalyzers>false</EnableNETAnalyzers><RunAnalyzersDuringBuild>false</RunAnalyzersDuringBuild>
                <TreatWarningsAsErrors>true</TreatWarningsAsErrors><DebugType>none</DebugType><UseSharedCompilation>false</UseSharedCompilation>
              </PropertyGroup>
              <ItemGroup>{{includes}}</ItemGroup>
              <Target Name="RemoveFrameworkReferences" BeforeTargets="BeforeCompile" AfterTargets="FindReferenceAssembliesForReferences">
                <ItemGroup><ReferencePathWithRefAssemblies Remove="@(ReferencePathWithRefAssemblies)" /><ReferencePath Remove="@(ReferencePath)" /></ItemGroup>
              </Target>
            </Project>
            """;
        File.WriteAllText(Path.Combine(directory, "Closure.csproj"), project, new UTF8Encoding(false));
        string[] arguments = ["build", "Closure.csproj", "--nologo", "-m:1", "-c", "Release", "--disable-build-servers"];
        ProcessStartInfo start = new("dotnet") { WorkingDirectory = directory, UseShellExecute = false,
            CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string arg in arguments) start.ArgumentList.Add(arg);
        File.WriteAllText(Path.Combine(directory, "command.json"), JsonSerializer.Serialize(new { Executable = "dotnet", Arguments = arguments, WorkingDirectory = directory, StartedUtc = DateTimeOffset.UtcNow }));
        using Process child = Process.Start(start) ?? throw new InvalidOperationException("no-stdlib child start");
        Task<string> stdout = child.StandardOutput.ReadToEndAsync(), stderr = child.StandardError.ReadToEndAsync();
        bool exited = child.WaitForExit(90000); if (!exited) { child.Kill(true); child.WaitForExit(); }
        Task.WaitAll(stdout, stderr);
        File.WriteAllText(Path.Combine(directory, "stdout.log"), stdout.Result); File.WriteAllText(Path.Combine(directory, "stderr.log"), stderr.Result);
        File.WriteAllText(Path.Combine(directory, "result.json"), JsonSerializer.Serialize(new { Exited = exited, NativeExit = child.ExitCode, EndedUtc = DateTimeOffset.UtcNow }));
        Check(exited && child.ExitCode == 0, "actual no-stdlib child: " + directory + " " + stdout.Result + stderr.Result);
        string dll = Path.Combine(directory, "bin", "Release", "net11.0", "M03_004_NoStdLib.dll");
        using FileStream stream = File.OpenRead(dll); using PEReader pe = new(stream); MetadataReader metadata = pe.GetMetadataReader();
        Check(metadata.AssemblyReferences.Count == 0, "zero AssemblyRefs");
        TypeDefinition type = metadata.TypeDefinitions.Select(metadata.GetTypeDefinition).Single(t => metadata.GetString(t.Name) == nameof(TypedNoStdlibExitBootServicesCallAdapter));
        foreach (string name in new[] { "Prepare", "Attempt" })
        {
            MethodDefinition method = type.GetMethods().Select(metadata.GetMethodDefinition).Single(m => metadata.GetString(m.Name) == name);
            Check(method.RelativeVirtualAddress != 0 && pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes()!.Length > 0, "actual method " + name);
        }
        foreach (var row in identities) Check(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(root, row.Path)))) == row.Sha256, "closure input stable");
        File.WriteAllText(Path.Combine(directory, "metadata.json"), JsonSerializer.Serialize(new { SourceCount = sources.Length,
            AssemblyReferences = metadata.AssemblyReferences.Count, Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(dll))), Methods = new[] { "Prepare", "Attempt" }, TargetExecuted = false }));
        Console.WriteLine("M03_004_NOSTDLIB inputs=4 assemblyReferences=0 targetExecuted=false path=" + directory);
    }
}
