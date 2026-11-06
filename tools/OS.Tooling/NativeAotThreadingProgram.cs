using System.Text;

namespace OS.Tooling;
/// <summary>Immutable instruction lookup for one exact qualified threading
/// package and its reviewed complete LLVM index. Construction binds current
/// relevant sources and consumes immutable snapshots; no app executes and no
/// mapping or guest-transcript authority is granted. Partial reports cannot
/// construct this type. Invalid/missing/changed inputs throw.</summary>

public sealed partial class NativeAotThreadingProgram
{
    private const int MaximumDisassemblyBytes = 20 * 1024 * 1024;
    private readonly NativeAotInstructionIndex index;
    internal string SourceRoot
    {
        get;
    }
    public string Application
    {
        get;
    }
    /// <summary>Value-only identity of the admitted complete instruction index.</summary>

    public Inspection Metadata => Report(index);

    private NativeAotThreadingProgram(string root, string app, NativeAotInstructionIndex index)
    {
        SourceRoot = Path.GetFullPath(root);
        Application = app;
        this.index = index;
    }

    /// <summary>Tests only a decoded instruction start, excluding operands/gaps.</summary>

    public bool IsInstruction(ulong address) => index.IsInstruction(address);
    /// <summary>Tests the captured RIP after an actual decoded SYSCALL.</summary>

    public bool IsSyscallReturn(ulong address) => index.IsSyscallReturn(address);
    private static Inspection Report(NativeAotInstructionIndex value) => new(value.Report.InstructionCount, value.Report.SyscallCount, value.Report.Sha256);
    private static NativeAotInstructionIndex Parse(ReadOnlySpan<byte> elf, string dump) => NativeAotInstructionIndex.Parse(elf, dump, "threading_instruction_", "CLROS_THREADING_INSTRUCTIONS_V1\n", 300_000, MaximumDisassemblyBytes);
    /// <summary>Parses bounded executable byte rows for offline review/tests.
    /// Reports may be partial or synthetic; no instruction lookup is returned.</summary>

    public static Inspection Inspect(ReadOnlySpan<byte> elf, string dump) => Report(Parse(elf, dump));
    /// <summary>Require canonical package, current selected producer sources and
    /// the exact complete reviewed index digest. Counts alone never admit a
    /// substituted decoder boundary. Caller spans/text are not retained.</summary>

    public static NativeAotThreadingProgram Create(string root, string app, ReadOnlySpan<byte> package, string dump)
    {
        NativeAotThreadingPackage.RequireCurrentSources(root, app);
        NativeAotThreadingPackage.RequirePackage(app, package);
        var parsed = Parse(package[(64 + Encoding.ASCII.GetByteCount(NativeAotThreadingPackage.Manifest(app)))..], dump);
        Inspection expected = Reviewed(app);
        if (Report(parsed) != expected || !parsed.SyscallContinuationsValid)
            throw new InvalidDataException("threading_instruction_identity");
        NativeAotThreadingPackage.RequireCurrentSources(root, app);
        return new(root, app, parsed);
    }

    private static Inspection Reviewed(string app) => app switch
    {
        "NativeAotSuspensionSmoke" => new(257_901, 166, "AA8A569696226AF82D110954FC819D1E79F15BA5A8014FE342FCEDA768B92E67"),
        "NativeAotThreadPoolSmoke" => new(299_149, 167, "D3CA7538C6E655C05CDEC05DB9FBBC942120DD862282F800CB98CB82F0A03D69"),
        _ => throw new ArgumentException("threading_package_app", nameof(app))
    };
    /// <summary>Reads both bounded file snapshots under concurrent read-only
    /// locks, then calls Create. Files remain caller-owned and nothing executes.
    /// Aliased paths, invalid UTF8, incomplete or changed reads throw.</summary>

    public static NativeAotThreadingProgram Read(string root, string app, string packagePath, string disassemblyPath)
    {
        var p = NativeAotThreadingPackage.Specification(app);
        packagePath = Path.GetFullPath(packagePath);
        disassemblyPath = Path.GetFullPath(disassemblyPath);
        if (string.Equals(packagePath, disassemblyPath, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new InvalidDataException("threading_instruction_alias");
        using AuthorityFiles files = new();
        var package = files.Open(Path.GetDirectoryName(packagePath)!, packagePath, "threading package", "threading_instruction_package", p.Bytes + 4096, singleLink: true);
        var dump = files.Open(Path.GetDirectoryName(disassemblyPath)!, disassemblyPath, "threading disassembly", "threading_instruction_dump", MaximumDisassemblyBytes, singleLink: true);
        AuthorityFiles.RequireDistinct(package, dump);
        byte[] bytes = package.ReadBytes(p.Bytes + 4096, "threading_instruction_package");
        string text = dump.ReadUtf8(MaximumDisassemblyBytes, "threading_instruction_dump");
        var program = Create(root, app, bytes, text);
        files.VerifyUnchanged();
        return program;
    }
}
