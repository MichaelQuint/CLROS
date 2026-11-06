namespace OS.Tooling;
/// <summary>Immutable host instruction lookup for the exact B8E4 log/upload ELF
/// and an explicit write or verify package. Preparation borrows snapshots and
/// holds reviewed source files until validation finishes; only copied numeric
/// boundaries and phase remain. This admits an index, not execution, mappings,
/// original publication, device, guest or persistence evidence.</summary>

public sealed partial class NativeAotLogUploadProgram
{
    public const int MaximumDisassemblyBytes = 20 * 1024 * 1024;
    private readonly NativeAotInstructionIndex index;
    internal string SourceRoot
    {
        get;
    }
    public string Phase
    {
        get;
    }
    public Inspection Metadata => Report(index);

    private NativeAotLogUploadProgram(string root, string phase, NativeAotInstructionIndex index)
    {
        SourceRoot = Path.GetFullPath(root);
        Phase = phase;
        this.index = index;
    }

    /// <summary>Test an exact decoded instruction start, excluding operands and padding.</summary>

    public bool IsInstruction(ulong address) => index.IsInstruction(address);
    /// <summary>Test the RIP immediately following a genuine decoded SYSCALL.</summary>

    public bool IsSyscallReturn(ulong address) => index.IsSyscallReturn(address);
    private static Inspection Report(NativeAotInstructionIndex value) => new(value.Report.InstructionCount, value.Report.SyscallCount, value.Report.Sha256);
    private static NativeAotInstructionIndex Parse(ReadOnlySpan<byte> elf, string dump) => NativeAotInstructionIndex.Parse(elf, dump, "log_upload_instruction_", "CLROS_LOG_UPLOAD_INSTRUCTIONS_V1\n", 300_000, MaximumDisassemblyBytes);
    /// <summary>Report bounded executable-byte rows for offline inspection. ELF
    /// bytes and text are synchronous borrows and are not retained. Partial or
    /// synthetic reports are permitted; they cannot construct a prepared lookup.</summary>

    public static Inspection Inspect(ReadOnlySpan<byte> elf, string llvmDisassembly) => Report(Parse(elf, llvmDisassembly));
    /// <summary>Require the fixed ELF, canonical phase/package, current reviewed
    /// producer/app sources and complete reviewed index. Input buffers must stay
    /// stable during this synchronous call. Invalid or changed inputs throw;
    /// no source/file lease is transferred to the returned immutable lookup.</summary>

    public static NativeAotLogUploadProgram Create(string root, ReadOnlySpan<byte> package, string llvmDisassembly, string phase)
    {
        RequirePhase(phase);
        _ = NativeAotLogUploadContract.InspectClrosPackage(package, phase);
        using AuthorityFiles sources = new();
        HoldSources(root, sources);
        var parsed = Parse(package[^NativeAotLogUploadContract.ClrosElfBytes..], llvmDisassembly);
        if (Report(parsed) != Reviewed || !parsed.IsInstruction(0x408f77) || !parsed.SyscallContinuationsValid)
            throw new InvalidDataException("log_upload_instruction_identity");
        HoldSources(root, sources);
        sources.VerifyUnchanged();
        return new(root, phase, parsed);
    }

    // Independent LLVM22.1.3 section coverage binds all893491 executable section
    // bytes of the current CE55 RC3 candidate. The93 load-padding bytes have no starts; entry and every170
    // syscall continuation are starts. This is not a control-flow/stack proof.
    private static Inspection Reviewed => new(265_797, 170, "EBE1F9D31153BE28AAED85DE7DFDDBA6E6BEBD2BDA472AE0901E5AB14727A04F");

    /// <summary>Read distinct bounded plain single-link package and UTF8 dump
    /// snapshots under read-sharing holds, then call Create. Files remain
    /// caller-owned and holds end before return. Missing, aliased, malformed or
    /// changed files throw. The eventual run owner must retain its own inputs.</summary>

    public static NativeAotLogUploadProgram Read(string root, string packagePath, string disassemblyPath, string phase)
    {
        RequirePhase(phase);
        packagePath = Path.GetFullPath(packagePath);
        disassemblyPath = Path.GetFullPath(disassemblyPath);
        if (string.Equals(packagePath, disassemblyPath, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new InvalidDataException("log_upload_instruction_alias");
        using AuthorityFiles files = new();
        var package = files.Open(Path.GetDirectoryName(packagePath)!, packagePath, "log/upload package", "log_upload_instruction_package", NativeAotLogUploadContract.ClrosElfBytes + 4096, singleLink: true);
        var dump = files.Open(Path.GetDirectoryName(disassemblyPath)!, disassemblyPath, "log/upload disassembly", "log_upload_instruction_dump", MaximumDisassemblyBytes, singleLink: true);
        AuthorityFiles.RequireDistinct(package, dump);
        var program = Create(root, package.ReadBytes(NativeAotLogUploadContract.ClrosElfBytes + 4096, "log_upload_instruction_package"), dump.ReadUtf8(MaximumDisassemblyBytes, "log_upload_instruction_dump"), phase);
        files.VerifyUnchanged();
        return program;
    }

    private static readonly (string Path, string Hash)[] ProducerSources = [("global.json", "BE8663081FD82196AC86E65E9021D58D7C03A13574CE8CD4DC803937FC3BA30E"), ("Directory.Build.props", "DF8A0D794DB73060124F5134EB930DCDBF6EE526C2410227F0C668AB6D10C5E5"), ("Directory.Build.targets", "85AFD90F9DD0F550B6A3C6AD71D28BF714A8C930158DB872F8DA5D316889931A"), ("tools/Directory.Build.props", "44B8268EA6934335DBC79FEF47C1CE1FDEE49393C929C92E322417B14AA321AE"), ("tools/OS.Tooling/NativeAotPayload.cs", "E3EC6BCE13C70C99F3D7306541D68104876FAF73593325107319719811694D2F"), ("tools/OS.Tooling/NativeAotWebPayload.cs", "129FD1AE91130FBFFC772D4A189A1FBB4EEEBF8236FF69B6B827A3A40B4C1FA3"), ("tools/OS.Tooling/NativeAotSocketFixturePublisher.cs", "A9029B6AFD17828D7182B7BFC2E971857BDF9469BA38D570AC6E6EBBCD197494"), ("apps/NativeAotLogUploadContract/NativeAotLogUploadContract.csproj", "CFAFC841454C628232065A18FB17E6E294F63F8770488CC69DD7BCA80C5F29C3"), ("apps/NativeAotLogUploadContract/Program.cs", "701446082EC9C6DB152123BD9CC3B7F918827FFAED5DD915823DA5E82E3DBAA3")];
    internal static void HoldSources(string root, AuthorityFiles sources)
    {
        root = Path.GetFullPath(root);
        foreach (var input in CurrentSources())
            if (!sources.Open(root, Path.Combine(root, input.Path), "log/upload producer source", "log_upload_source", 1024 * 1024).Sha256.Equals(input.Hash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("log_upload_source_identity " + input.Path);
        NativeAotCurrentSources.RequireToolingOwnership(root, ProducerSources);
        NativeAotLogUploadContract.RequireCurrentSources(root);
        sources.VerifyUnchanged();
    }

    internal static IReadOnlyList<(string Path, string Hash)> CurrentSources() =>
        NativeAotCurrentSources.Expand(ProducerSources).ToArray();

    private static void RequirePhase(string phase)
    {
        if (phase is not ("write" or "verify"))
            throw new ArgumentException("log_upload_instruction_phase", nameof(phase));
    }
}
