using System.Text;

namespace OS.Tooling;

/// <summary>Immutable lookup for one exact step1276 runtime package and its
/// reviewed complete LLVM index. The factory borrows snapshots synchronously,
/// binds current relevant sources and retains only its private decoded index.
/// Reports, synthetic rows and counts alone cannot construct this type.
/// Missing, stale or substituted inputs throw; no app or guest executes.</summary>
public sealed class NativeAotRuntimeProgram
{
    private const int MaximumDisassemblyBytes=20*1024*1024;
    private readonly NativeAotInstructionIndex index;
    internal string SourceRoot{get;}
    public string Application{get;}
    /// <summary>Value-only complete index identity, without lookup authority.</summary>
    public Inspection Metadata=>Report(index);
    private NativeAotRuntimeProgram(string root,string app,NativeAotInstructionIndex index)
    {SourceRoot=Path.GetFullPath(root);Application=app;this.index=index;}
    /// <summary>Tests only a decoded instruction start, excluding operands/gaps.</summary>
    public bool IsInstruction(ulong address)=>index.IsInstruction(address);
    /// <summary>Tests the captured RIP following an actual decoded SYSCALL.</summary>
    public bool IsSyscallReturn(ulong address)=>index.IsSyscallReturn(address);
    /// <summary>Value-only report; partial/synthetic reports grant no admission.</summary>
    public readonly record struct Inspection(int InstructionCount,int SyscallCount,string Sha256);
    private static Inspection Report(NativeAotInstructionIndex value)=>new(value.Report.InstructionCount,value.Report.SyscallCount,value.Report.Sha256);
    private static NativeAotInstructionIndex Parse(ReadOnlySpan<byte> elf,string dump)=>
        NativeAotInstructionIndex.Parse(elf,dump,"runtime_instruction_","CLROS_RUNTIME_INSTRUCTIONS_V1\n",350_000,MaximumDisassemblyBytes);
    /// <summary>Parses bounded executable-byte rows for offline review/tests.
    /// The borrowed ELF/text are not retained. No lookup or execution authority
    /// is returned, even for a complete-looking report.</summary>
    public static Inspection Inspect(ReadOnlySpan<byte> elf,string dump)=>Report(Parse(elf,dump));

    /// <summary>Requires canonical package, current selected producer sources
    /// and exact reviewed full index digest with valid syscall continuations.
    /// Caller bytes/text are not retained. Any failed check throws.</summary>
    public static NativeAotRuntimeProgram Create(string root,string app,ReadOnlySpan<byte> package,string dump)
    {
        NativeAotRuntimePackage.RequireCurrentSources(root,app);NativeAotRuntimePackage.RequirePackage(app,package);
        var parsed=Parse(package[(64+Encoding.ASCII.GetByteCount(NativeAotRuntimePackage.Manifest(app)))..],dump);
        if(Report(parsed)!=Reviewed(app) || !parsed.SyscallContinuationsValid)throw new InvalidDataException("runtime_instruction_identity");
        NativeAotRuntimePackage.RequireCurrentSources(root,app);return new(root,app,parsed);
    }
    private static Inspection Reviewed(string app)=>app switch
    {
        "NativeAotSuspensionStress"=>new(301509,166,"C2021C3C8A003FEEE150A4974371640D101D374D34B014927502A5080C7601E4"),
        "NativeAotThreadPoolStress"=>new(307640,167,"4FBE5A570D727726C71123FBDBC222F4F7440B6E9A82B85E82A0E8C83212CEAB"),
        "NativeAotRuntimeMatrix"=>new(298538,167,"6496879D9908E67BF44C17988B1D899EDFEEB8024A0CE778594F11CF01EB8614"),
        _=>throw new ArgumentException("runtime_package_app",nameof(app))
    };

    /// <summary>Reads both bounded plain single-link snapshots under concurrent
    /// read-only file locks, then calls Create. Files remain caller-owned; this
    /// method executes nothing. Aliases, invalid UTF8 and changed reads throw.</summary>
    public static NativeAotRuntimeProgram Read(string root,string app,string packagePath,string disassemblyPath)
    {
        var p=NativeAotRuntimePackage.Specification(app);packagePath=Path.GetFullPath(packagePath);disassemblyPath=Path.GetFullPath(disassemblyPath);
        if(string.Equals(packagePath,disassemblyPath,OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal))
            throw new InvalidDataException("runtime_instruction_alias");
        using AuthorityFiles files=new();
        var package=files.Open(Path.GetDirectoryName(packagePath)!,packagePath,"runtime package","runtime_instruction_package",p.Bytes+4096,singleLink:true);
        var dump=files.Open(Path.GetDirectoryName(disassemblyPath)!,disassemblyPath,"runtime disassembly","runtime_instruction_dump",MaximumDisassemblyBytes,singleLink:true);
        AuthorityFiles.RequireDistinct(package,dump);
        byte[] bytes=package.ReadBytes(p.Bytes+4096,"runtime_instruction_package");string text=dump.ReadUtf8(MaximumDisassemblyBytes,"runtime_instruction_dump");
        var program=Create(root,app,bytes,text);files.VerifyUnchanged();return program;
    }
}
