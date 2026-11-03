using System.Security.Cryptography;
using OS.Manifest;

namespace OS.Tooling;
/// <summary>Immutable instruction lookup for the separately published static-file
/// server. Preparation holds its exact app/build/producer sources and admits only
/// canonical staged or disk envelopes. It grants no execution or storage proof.</summary>

public sealed partial class NativeAotStaticFileProgram
{
    public const int MaximumDisassemblyBytes = 128 * 1024 * 1024;
    internal const int ElfBytes = 8_641_616;
    private const string ElfSha256 = "43CD37EC60F8CDD192E0F3E28B799B778F038DEF85470542EEB3E1D8B61C6666";
    private readonly NativeAotInstructionIndex index;
    internal string SourceRoot
    {
        get;
    }
    public bool Staged
    {
        get;
    }

    private NativeAotStaticFileProgram(string root, NativeAotInstructionIndex value, bool staged)
    {
        SourceRoot = Path.GetFullPath(root);
        index = value;
        Staged = staged;
    }

    public Inspection Metadata => Report(index);

    public bool IsInstruction(ulong address) => index.IsInstruction(address);
    public bool IsSyscallReturn(ulong address) => index.IsSyscallReturn(address);
    private static Inspection Report(NativeAotInstructionIndex value) => new(value.Report.InstructionCount, value.Report.SyscallCount, value.Report.Sha256);
    private static NativeAotInstructionIndex Parse(ReadOnlySpan<byte> elf, string dump) => NativeAotInstructionIndex.Parse(elf, dump, "static_instruction_", "CLROS_STATIC_FILE_INSTRUCTIONS_V1\n", 2_000_000, MaximumDisassemblyBytes);
    /// <summary>Offline report over borrowed executable rows. A report has no
    /// prepared-program authority, even when its rows bind to valid ELF bytes.</summary>

    public static Inspection Inspect(ReadOnlySpan<byte> elf, string dump) => Report(Parse(elf, dump));
    /// <summary>Prepare only the exact published ELF and complete reviewed index.
    /// Inputs are synchronous borrows. Invalid envelopes, sources, instruction
    /// bytes or continuations fail before a lookup object is returned.</summary>

    public static NativeAotStaticFileProgram Create(string root, ReadOnlySpan<byte> package, string dump, bool staged)
    {
        var view = RequirePackage(package, staged);
        using AuthorityFiles sources = new();
        HoldSources(root, sources);
        var parsed = Parse(package.Slice((int)view.PayloadOffset, (int)view.PayloadBytes), dump);
        if (Report(parsed) != Reviewed || !parsed.SyscallContinuationsValid || !parsed.IsInstruction(0x408f77))
            throw new InvalidDataException("static_instruction_identity");
        HoldSources(root, sources);
        sources.VerifyUnchanged();
        return new(root, parsed, staged);
    }

    // Fresh RC3 LLVM22.1.3 covers every4,445,627 byte in five executable sections,
    // entry0x408f77 and all198 syscall continuations. An independent ELF-section
    // walk confirms complete contiguous coverage and the same domain digest.
    // Load padding has no starts. This is not a control-flow proof.
    private static Inspection Reviewed => new(1_389_175, 198, "4395C08AE044C0CAA09A7B96737B21CDE37E6CCDCFBECEC955306BD5E9383A4C");

    internal static LinuxElfPackageView RequirePackage(ReadOnlySpan<byte> package, bool staged)
    {
        var view = LinuxElfPackage.Validate(package);
        if (!view.Succeeded || view.PayloadBytes != ElfBytes)
            throw new InvalidDataException("static_package_identity");
        var elf = package.Slice((int)view.PayloadOffset, (int)view.PayloadBytes);
        if (Convert.ToHexString(SHA256.HashData(elf)) != ElfSha256)
            throw new InvalidDataException("static_package_identity");
        if (!package.SequenceEqual(NativeAotStaticFilePackage.Create(elf, staged)))
            throw new InvalidDataException("static_package_policy");
        return view;
    }

    /// <summary>Read distinct bounded plain single-link package/dump snapshots
    /// while held against mutation. Missing, aliased or changed inputs reject;
    /// the caller retains file lifetime and this starts no process.</summary>

    public static NativeAotStaticFileProgram Read(string root, string packagePath, string disassemblyPath, bool staged)
    {
        packagePath = Path.GetFullPath(packagePath);
        disassemblyPath = Path.GetFullPath(disassemblyPath);
        using AuthorityFiles files = new();
        var package = files.Open(Path.GetDirectoryName(packagePath)!, packagePath, "static package", "static_instruction_package", LinuxElfPackage.MaximumBytes, singleLink: true);
        var dump = files.Open(Path.GetDirectoryName(disassemblyPath)!, disassemblyPath, "static disassembly", "static_instruction_dump", MaximumDisassemblyBytes, singleLink: true);
        AuthorityFiles.RequireDistinct(package, dump);
        var program = Create(root, package.ReadBytes(LinuxElfPackage.MaximumBytes, "static_instruction_package"), dump.ReadUtf8(MaximumDisassemblyBytes, "static_instruction_dump"), staged);
        files.VerifyUnchanged();
        return program;
    }

    private static readonly (string Path, string Hash)[] ProducerSources = [("global.json", "BE8663081FD82196AC86E65E9021D58D7C03A13574CE8CD4DC803937FC3BA30E"), ("Directory.Build.props", "DF8A0D794DB73060124F5134EB930DCDBF6EE526C2410227F0C668AB6D10C5E5"), ("Directory.Build.targets", "85AFD90F9DD0F550B6A3C6AD71D28BF714A8C930158DB872F8DA5D316889931A"), ("tools/Directory.Build.props", "44B8268EA6934335DBC79FEF47C1CE1FDEE49393C929C92E322417B14AA321AE"), ("tools/OS.Tooling/NativeAotPayload.cs", "E3EC6BCE13C70C99F3D7306541D68104876FAF73593325107319719811694D2F"), ("tools/OS.Tooling/NativeAotWebPayload.cs", "129FD1AE91130FBFFC772D4A189A1FBB4EEEBF8236FF69B6B827A3A40B4C1FA3"), ("tools/OS.Tooling/NativeAotSocketFixturePublisher.cs", "A9029B6AFD17828D7182B7BFC2E971857BDF9469BA38D570AC6E6EBBCD197494"), ("tools/OS.Tooling/NativeAotStaticFilePublisher.cs", "8274BCDE33D30274F72C24BE0A2481AA8048CC0C97579C552271017EB1F89B87")];
    internal static void HoldSources(string root, AuthorityFiles files)
    {
        root = Path.GetFullPath(root);
        foreach (var input in CurrentSources())
            if (!files.Open(root, Path.Combine(root, input.Path), "static producer source", "static_source", 1024 * 1024).Sha256.Equals(input.Hash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("static_source_identity " + input.Path);
        NativeAotCurrentSources.RequireToolingOwnership(root, ProducerSources);
        string app = Path.Combine(root, "apps", NativeAotStaticFilePublisher.Application);
        foreach (string path in Directory.GetFiles(app))
            files.Open(root, path, "static application source", "static_source", 1024 * 1024);
        NativeAotStaticFilePublisher.RequireCurrentSources(root);
        files.VerifyUnchanged();
    }

    internal static IReadOnlyList<(string Path, string Hash)> CurrentSources() =>
        NativeAotCurrentSources.Expand(ProducerSources).ToArray();
}
