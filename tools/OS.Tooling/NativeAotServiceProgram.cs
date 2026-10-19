using OS.Manifest;

namespace OS.Tooling;

/// <summary>Immutable lookup for the original continuing-service ELF and its
/// independently reviewed complete decoder index. Current app/build/publication
/// sources stay locked through preparation. This admits instruction boundaries;
/// it executes nothing and grants no service, shutdown or guest acceptance.</summary>
public sealed class NativeAotServiceProgram
{
    private const int MaximumDisassemblyBytes = 128 * 1024 * 1024;
    internal const int ElfBytes = 8_484_584;
    private const string ElfSha256 = "5020C22C3261DA626DC8CBDF8AD2EB5DBB296CE1E3A226E36C85549D4016A10B";
    private readonly NativeAotInstructionIndex index;
    internal string SourceRoot { get; }
    private NativeAotServiceProgram(string root, NativeAotInstructionIndex value, bool lifecycle)
    { SourceRoot = Path.GetFullPath(root); index = value; Lifecycle = lifecycle; }

    /// <summary>Explicit package/replay contract bound by the preparing factory.</summary>
    public bool Lifecycle { get; }

    /// <summary>Value-only index report; this alone conveys no lookup authority.</summary>
    public readonly record struct Inspection(int InstructionCount, int SyscallCount, string Sha256);
    /// <summary>Identity of the reviewed complete original index.</summary>
    public Inspection Metadata => Report(index);
    /// <summary>Accept only original decoded starts, excluding operands and padding.</summary>
    public bool IsInstruction(ulong address) => index.IsInstruction(address);
    /// <summary>Accept RIP immediately after an original decoded SYSCALL.</summary>
    public bool IsSyscallReturn(ulong address) => index.IsSyscallReturn(address);

    private static Inspection Report(NativeAotInstructionIndex value) => new(value.Report.InstructionCount, value.Report.SyscallCount, value.Report.Sha256);
    private static NativeAotInstructionIndex Parse(ReadOnlySpan<byte> elf, string dump) => NativeAotInstructionIndex.Parse(elf, dump,
        "service_instruction_", "CLROS_SERVICE_INSTRUCTIONS_V1\n", 2_000_000, MaximumDisassemblyBytes);

    /// <summary>Inspect bounded borrowed executable rows synchronously. Partial
    /// or synthetic reports confer no prepared-program authority.</summary>
    public static Inspection Inspect(ReadOnlySpan<byte> elf, string dump) => Report(Parse(elf, dump));

    /// <summary>Bind the exact original ELF, canonical service policy, current
    /// sources and reviewed complete index. Input bytes/text stay caller owned
    /// through return; missing, stale, aliased or substituted inputs reject.
    /// Explicit lifecycle mode selects the v2 envelope; the default retains
    /// v1. Both require this publication's exact original image and index.</summary>
    public static NativeAotServiceProgram Create(string root, ReadOnlySpan<byte> package, string dump, bool lifecycle = false)
    {
        var view = RequirePackage(package, lifecycle);
        using AuthorityFiles sources = new(); HoldSources(root, sources);
        var parsed = Parse(package.Slice((int)view.PayloadOffset, (int)view.PayloadBytes), dump);
        // LLVM22.1.3 covers all4,381,199 bytes in five original executable
        // sections, entry0x408f77 and all198 syscall continuations. Executable
        // load padding grants no starts. This is not a control-flow proof.
        if (Report(parsed) != new Inspection(1_368_900, 198,
            "33C0F2ED4A7DD510EB3DE4F8941E084C7AECD2F7E717D20F62E6A320375AF97D") ||
            !parsed.SyscallContinuationsValid || !parsed.IsInstruction(0x408f77))
            throw new InvalidDataException("service_instruction_identity");
        HoldSources(root, sources); sources.VerifyUnchanged(); return new(root, parsed, lifecycle);
    }

    internal static LinuxElfPackageView RequirePackage(ReadOnlySpan<byte> package, bool lifecycle = false)
    {
        var description = NativeAotServicePackage.Describe(package, lifecycle);
        var view = LinuxElfPackage.Validate(package);
        if (view.PayloadBytes != ElfBytes || description.ElfSha256 != ElfSha256)
            throw new InvalidDataException("service_package_identity");
        return view;
    }

    /// <summary>Read distinct bounded plain single-link package/dump files under
    /// read-only locks. Consume owned snapshots through Create and verify their
    /// unchanged identities. Invalid text, aliases or changing inputs reject;
    /// files remain caller owned and no app/process is started.</summary>
    public static NativeAotServiceProgram Read(string root, string packagePath, string disassemblyPath, bool lifecycle = false)
    {
        packagePath = Path.GetFullPath(packagePath); disassemblyPath = Path.GetFullPath(disassemblyPath);
        using AuthorityFiles files = new();
        var package = files.Open(Path.GetDirectoryName(packagePath)!, packagePath, "service package", "service_instruction_package", LinuxElfPackage.MaximumBytes, singleLink: true);
        var dump = files.Open(Path.GetDirectoryName(disassemblyPath)!, disassemblyPath, "service disassembly", "service_instruction_dump", MaximumDisassemblyBytes, singleLink: true);
        AuthorityFiles.RequireDistinct(package, dump);
        var program = Create(root, package.ReadBytes(LinuxElfPackage.MaximumBytes, "service_instruction_package"),
            dump.ReadUtf8(MaximumDisassemblyBytes, "service_instruction_dump"), lifecycle);
        files.VerifyUnchanged(); return program;
    }

    private static readonly (string Path, string Hash)[] Sources = [
        ("global.json", "FD3FDAAE1B349B01D6E6835EC111903F107A2643F3BCE313FAD4DC20180F5FB2"),
        ("Directory.Build.props", "DF8A0D794DB73060124F5134EB930DCDBF6EE526C2410227F0C668AB6D10C5E5"),
        ("Directory.Build.targets", "85AFD90F9DD0F550B6A3C6AD71D28BF714A8C930158DB872F8DA5D316889931A"),
        ("tools/Directory.Build.props", "44B8268EA6934335DBC79FEF47C1CE1FDEE49393C929C92E322417B14AA321AE"),
        ("tools/OS.Tooling/NativeAotPayload.cs", "44F16ABB380FE7F3CD42BA3C7E54710172F0320998CF2E85E296F8A7CE184AA5"),
        ("tools/OS.Tooling/NativeAotWebPayload.cs", "CEDB569E9AAF83AFE0377617A0440A08A8D7EDBE8145DFA1D7E8CF30B853A408"),
        ("tools/OS.Tooling/NativeAotSocketFixturePublisher.cs", "A9029B6AFD17828D7182B7BFC2E971857BDF9469BA38D570AC6E6EBBCD197494"),
        ("tools/OS.Tooling/NativeAotServicePublisher.cs", "1B9609B01600E43F37F642578D47033345B0EABE07C1FD4547BEEEB9A38C4639"),
        ("apps/MinimalApiService/MinimalApiService.csproj", "0B267A2A4EE1B931BF1CAD5DC025356345EE74AE8463D1A16B8C27A8D6C5B5D7"),
        ("apps/MinimalApiService/Program.cs", "EED32C37A92678411540591673389B30EF50F406BE7AD37AAC385D4801F00DBC"),
        ("apps/MinimalApiService/ServiceLifecycleExercise.cs", "FBBC8B89FCABFBDAD2D781C7E519680806933FD2B139EAA0FEBB75A496C3BE7E"),
        ("apps/MinimalApiService/ServiceExercise.cs", "971B8D504D3EA66A28576A89E221C9EB25E1D161BE2DAE4E150C06F15929EC80"),
        ("apps/MinimalApiService/appsettings.json", "B2F356A9CD23F432F5E19AD6AD72844E5C5331587DAB81C67722016CE6447AB5")];

    internal static void HoldSources(string root, AuthorityFiles files)
    {
        NativeAotServicePublisher.RequireCurrentSources(root);
        foreach (var source in Sources)
            if (!files.Open(root, Path.Combine(root, source.Path), "service source", "service_source", 1024 * 1024).Sha256.Equals(source.Hash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("service_source_identity " + source.Path);
    }
}
