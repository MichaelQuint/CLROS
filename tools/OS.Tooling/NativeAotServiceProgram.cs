using OS.Manifest;

namespace OS.Tooling;

/// <summary>Immutable lookup for the original continuing-service ELF and its
/// independently reviewed complete decoder index. Current app/build/publication
/// sources stay locked through preparation. This admits instruction boundaries;
/// it executes nothing and grants no service, shutdown or guest acceptance.</summary>
public sealed class NativeAotServiceProgram
{
    private const int MaximumDisassemblyBytes = 128 * 1024 * 1024;
    private const int ElfBytes = 8_413_864;
    private const string ElfSha256 = "2BCD70ACBB9C843BC5AA18BB86EA7796B70D1AB527BD4DC59DDA8DA3BCBCDE6F";
    private readonly NativeAotInstructionIndex index;
    internal string SourceRoot { get; }
    private NativeAotServiceProgram(string root, NativeAotInstructionIndex value)
    { SourceRoot = Path.GetFullPath(root); index = value; }

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
    /// through return; missing, stale, aliased or substituted inputs reject.</summary>
    public static NativeAotServiceProgram Create(string root, ReadOnlySpan<byte> package, string dump)
    {
        var description = NativeAotServicePackage.Describe(package);
        var view = LinuxElfPackage.Validate(package);
        if (view.PayloadBytes != ElfBytes || description.ElfSha256 != ElfSha256)
            throw new InvalidDataException("service_package_identity");
        using AuthorityFiles sources = new(); HoldSources(root, sources);
        var parsed = Parse(package.Slice((int)view.PayloadOffset, (int)view.PayloadBytes), dump);
        // LLVM22.1.3 covers all4,340,143 bytes in five original executable
        // sections, entry0x408f77 and all198 syscall continuations. Executable
        // load padding grants no starts. This is not a control-flow proof.
        if (Report(parsed) != new Inspection(1_356_654, 198,
            "AAD837BA102E1DC42FDE54991DB3D82AA19DA467C208F9C0085C6DDFA985967C") ||
            !parsed.SyscallContinuationsValid || !parsed.IsInstruction(0x408f77))
            throw new InvalidDataException("service_instruction_identity");
        HoldSources(root, sources); sources.VerifyUnchanged(); return new(root, parsed);
    }

    /// <summary>Read distinct bounded plain single-link package/dump files under
    /// read-only locks. Consume owned snapshots through Create and verify their
    /// unchanged identities. Invalid text, aliases or changing inputs reject;
    /// files remain caller owned and no app/process is started.</summary>
    public static NativeAotServiceProgram Read(string root, string packagePath, string disassemblyPath)
    {
        packagePath = Path.GetFullPath(packagePath); disassemblyPath = Path.GetFullPath(disassemblyPath);
        using AuthorityFiles files = new();
        var package = files.Open(Path.GetDirectoryName(packagePath)!, packagePath, "service package", "service_instruction_package", LinuxElfPackage.MaximumBytes, singleLink: true);
        var dump = files.Open(Path.GetDirectoryName(disassemblyPath)!, disassemblyPath, "service disassembly", "service_instruction_dump", MaximumDisassemblyBytes, singleLink: true);
        AuthorityFiles.RequireDistinct(package, dump);
        var program = Create(root, package.ReadBytes(LinuxElfPackage.MaximumBytes, "service_instruction_package"),
            dump.ReadUtf8(MaximumDisassemblyBytes, "service_instruction_dump"));
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
        ("tools/OS.Tooling/NativeAotServicePublisher.cs", "473FD7C3BBA66126728B5645C2DC6896CA25E8D125B87943531995C1B376397D"),
        ("apps/MinimalApiService/MinimalApiService.csproj", "0B267A2A4EE1B931BF1CAD5DC025356345EE74AE8463D1A16B8C27A8D6C5B5D7"),
        ("apps/MinimalApiService/Program.cs", "DC48006F173CCD125EFDA43E361CEE73B8BBFB968ED0D1BCA20D04C4E78296A0"),
        ("apps/MinimalApiService/ServiceExercise.cs", "D6E48CB9EF1D58E6935BD6275B9DDCD7D46937A44B5FA04BDC200F8884F9F462"),
        ("apps/MinimalApiService/appsettings.json", "B2F356A9CD23F432F5E19AD6AD72844E5C5331587DAB81C67722016CE6447AB5")];

    private static void HoldSources(string root, AuthorityFiles files)
    {
        NativeAotServicePublisher.RequireCurrentSources(root);
        foreach (var source in Sources)
            if (!files.Open(root, Path.Combine(root, source.Path), "service source", "service_source", 1024 * 1024).Sha256.Equals(source.Hash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("service_source_identity " + source.Path);
    }
}
