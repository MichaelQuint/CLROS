namespace OS.Tooling;
/// <summary>Immutable host instruction lookup for the exact 876D SQLite ELF
/// and one of five explicit app-mode packages. Preparation borrows snapshots and
/// holds reviewed source files until validation finishes; only copied numeric
/// boundaries and mode remain. This admits an index, not execution, mappings,
/// original publication, device, guest or persistence evidence.</summary>

public sealed partial class NativeAotSqliteProgram
{
    public const int MaximumDisassemblyBytes = 56 * 1024 * 1024;
    private readonly NativeAotInstructionIndex index;
    internal string SourceRoot { get; }
    public string Mode { get; }
    public Inspection Metadata => Report(index);
    public bool ExecutionAuthority => false;

    private NativeAotSqliteProgram(string root, string mode, NativeAotInstructionIndex index)
    {
        SourceRoot = Path.GetFullPath(root);
        Mode = mode;
        this.index = index;
    }

    /// <summary>Test an exact decoded instruction start, excluding operands and padding.</summary>

    public bool IsInstruction(ulong address) => index.IsInstruction(address);
    /// <summary>Test the RIP immediately following a genuine decoded SYSCALL.</summary>

    public bool IsSyscallReturn(ulong address) => index.IsSyscallReturn(address);
    private static Inspection Report(NativeAotInstructionIndex value) => new(value.Report.InstructionCount, value.Report.SyscallCount, value.Report.Sha256);
    private static NativeAotInstructionIndex Parse(ReadOnlySpan<byte> elf, string dump) => NativeAotInstructionIndex.Parse(elf, dump, "sqlite_instruction_", "CLROS_SQLITE_INSTRUCTIONS_V1\n", 800_000, MaximumDisassemblyBytes);
    /// <summary>Report bounded executable-byte rows for offline inspection. ELF
    /// bytes and text are synchronous borrows and are not retained. Partial or
    /// synthetic reports are permitted; they cannot construct a prepared lookup.</summary>

    public static Inspection Inspect(ReadOnlySpan<byte> elf, string llvmDisassembly) => Report(Parse(elf, llvmDisassembly));
    /// <summary>Require the fixed ELF, canonical mode/package, current reviewed
    /// producer/app sources and complete reviewed index. Input buffers must stay
    /// stable during this synchronous call. Invalid or changed inputs throw;
    /// no source/file lease is transferred to the returned immutable lookup.</summary>

    public static NativeAotSqliteProgram Create(string root, ReadOnlySpan<byte> package, string llvmDisassembly, string mode)
    {
        RequireMode(mode);
        _ = NativeAotSqliteContract.InspectClrosPackage(package, mode);
        using AuthorityFiles sources = new();
        HoldSources(root, sources);
        var parsed = Parse(package[^NativeAotSqliteContract.ClrosElfBytes..], llvmDisassembly);
        if (Report(parsed) != Reviewed || !parsed.IsInstruction(0x4092b7) || !parsed.SyscallContinuationsValid)
            throw new InvalidDataException("sqlite_instruction_identity");
        HoldSources(root, sources);
        sources.VerifyUnchanged();
        return new(root, mode, parsed);
    }

    // Independent LLVM22.1.3 section coverage binds all2633509 executable section
    // bytes of the current EC83 RC3 candidate. The75 load-padding bytes have no starts; entry and every193
    // syscall continuation are starts. This is not a control-flow/stack proof.
    private static Inspection Reviewed => new(761_857, 193, "4E0AF370EC915FFFC23F88FABE140402A2AFF599835026DDE272C1180C3A44B0");

    /// <summary>Read distinct bounded plain single-link package and UTF8 dump
    /// snapshots under read-sharing holds, then call Create. Files remain
    /// caller-owned and holds end before return. Missing, aliased, malformed or
    /// changed files throw. The eventual run owner must retain its own inputs.</summary>

    public static NativeAotSqliteProgram Read(string root, string packagePath, string disassemblyPath, string mode)
    {
        RequireMode(mode);
        packagePath = Path.GetFullPath(packagePath);
        disassemblyPath = Path.GetFullPath(disassemblyPath);
        if (string.Equals(packagePath, disassemblyPath, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new InvalidDataException("sqlite_instruction_alias");
        using AuthorityFiles files = new();
        var package = files.Open(Path.GetDirectoryName(packagePath)!, packagePath, "SQLite package", "sqlite_instruction_package", NativeAotSqliteContract.ClrosElfBytes + 4096, singleLink: true);
        var dump = files.Open(Path.GetDirectoryName(disassemblyPath)!, disassemblyPath, "SQLite disassembly", "sqlite_instruction_dump", MaximumDisassemblyBytes, singleLink: true);
        AuthorityFiles.RequireDistinct(package, dump);
        var program = Create(root, package.ReadBytes(NativeAotSqliteContract.ClrosElfBytes + 4096, "sqlite_instruction_package"), dump.ReadUtf8(MaximumDisassemblyBytes, "sqlite_instruction_dump"), mode);
        files.VerifyUnchanged();
        return program;
    }

    private static readonly (string Path, string Hash)[] ProducerSources = [("global.json", "BE8663081FD82196AC86E65E9021D58D7C03A13574CE8CD4DC803937FC3BA30E"), ("Directory.Build.props", "DF8A0D794DB73060124F5134EB930DCDBF6EE526C2410227F0C668AB6D10C5E5"), ("Directory.Build.targets", "85AFD90F9DD0F550B6A3C6AD71D28BF714A8C930158DB872F8DA5D316889931A"), ("tools/Directory.Build.props", "44B8268EA6934335DBC79FEF47C1CE1FDEE49393C929C92E322417B14AA321AE"), ("tools/OS.Tooling/NativeAotPayload.cs", "E3EC6BCE13C70C99F3D7306541D68104876FAF73593325107319719811694D2F"), ("tools/OS.Tooling/NativeAotWebPayload.cs", "129FD1AE91130FBFFC772D4A189A1FBB4EEEBF8236FF69B6B827A3A40B4C1FA3"), ("tools/OS.Tooling/NativeAotSocketFixturePublisher.cs", "A9029B6AFD17828D7182B7BFC2E971857BDF9469BA38D570AC6E6EBBCD197494"), ("tools/OS.Tooling/NativeAotSqliteContract.cs", "50ADE64A1AF557B57C9FBE44B773CBA9A92D9A6A1FC9268C7E109041254B9844"), ("apps/NativeAotSqliteContract/NativeAotSqliteContract.csproj", "FCCD459D604864E41480B5E2FA57AC477E821A19B6772BF6C3A564D30771509B"), ("apps/NativeAotSqliteContract/Program.cs", "29A852A3322D6919F909FFEDCD73279CDF5BDC084890DEF40EDDCC0367DCC45A")];
    private static readonly NativeAotSocketFixturePublisher AppSources = new(NativeAotSqliteContract.Application, "sqlite-program", [("Program.cs", "29A852A3322D6919F909FFEDCD73279CDF5BDC084890DEF40EDDCC0367DCC45A"), ("NativeAotSqliteContract.csproj", "FCCD459D604864E41480B5E2FA57AC477E821A19B6772BF6C3A564D30771509B")]);
    internal static void HoldSources(string root, AuthorityFiles sources)
    {
        root = Path.GetFullPath(root);
        foreach (var input in CurrentSources())
            if (!sources.Open(root, Path.Combine(root, input.Path), "SQLite producer source", "sqlite_source", 1024 * 1024).Sha256.Equals(input.Hash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("sqlite_source_identity " + input.Path);
        NativeAotCurrentSources.RequireToolingOwnership(root, ProducerSources);
        AppSources.RequireCurrentSources(root);
        sources.VerifyUnchanged();
    }

    internal static IReadOnlyList<(string Path, string Hash)> CurrentSources() =>
        NativeAotCurrentSources.Expand(ProducerSources).ToArray();

    private static void RequireMode(string mode)
    {
        if (mode is not ("database" or "verify-database" or "recovery-seed" or "recovery-spill" or "recovery-verify"))
            throw new ArgumentException("sqlite_instruction_mode", nameof(mode));
    }
}
