using OS.Manifest;

namespace OS.Tooling;

/// <summary>Immutable lookup for the original continuing-service ELF and its
/// independently reviewed complete decoder index. Current app/build/publication
/// sources stay locked through preparation. This admits instruction boundaries;
/// it executes nothing and grants no service, shutdown or guest acceptance.</summary>
public sealed class NativeAotServiceProgram
{
    private const int MaximumDisassemblyBytes = 128 * 1024 * 1024;
    internal const int ElfBytes = 8501192;
    private const string ElfSha256 = "5ED02AB93C4854B8E4C379811A0139AF2A905E160317BB83AA538D9F22FD117D";
    private readonly NativeAotInstructionIndex index;
    internal string SourceRoot { get; }
    private NativeAotServiceProgram(string root, NativeAotInstructionIndex value, NativeAotServiceContract contract)
    { SourceRoot = Path.GetFullPath(root); index = value; Contract = contract; }

    /// <summary>Immutable package/replay contract bound by the preparing factory.
    /// V1, V2 and QuotaFailure share the reviewed original executable; their
    /// immutable package policies remain distinct.</summary>
    public NativeAotServiceContract Contract { get; }
    /// <summary>Compatibility selector for the original positive v2 envelope.
    /// New contract dispatch must use Contract rather than infer policy here.</summary>
    public bool Lifecycle => Contract == NativeAotServiceContract.V2;

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
        => Create(root, package, dump, lifecycle ? NativeAotServiceContract.V2 : NativeAotServiceContract.V1);

    /// <summary>Prepare only an explicitly selected reviewed publication. Borrowed
    /// inputs remain caller owned through return. Unknown contracts reject;
    /// all three contracts require current sources, this exact original ELF and
    /// its complete index. A matching envelope alone grants no authority.</summary>
    public static NativeAotServiceProgram Create(string root, ReadOnlySpan<byte> package, string dump, NativeAotServiceContract contract)
    {
        var view = RequirePackage(package, contract);
        using AuthorityFiles sources = new(); HoldSources(root, sources);
        var parsed = Parse(package.Slice((int)view.PayloadOffset, (int)view.PayloadBytes), dump);
        // LLVM22.1.3 covers all4,390,791 bytes in five RC3 executable
        // sections, entry0x408f77 and all198 syscall continuations. Executable
        // load padding grants no starts. This is not a control-flow proof.
        if (Report(parsed) != new Inspection(1_371_556, 198,
            "A6BD90D9C25CB20EB880E7E84344A1A8B9A084D9649B3399DDE3D8743DC0E0C0") ||
            !parsed.SyscallContinuationsValid || !parsed.IsInstruction(0x408f77))
            throw new InvalidDataException("service_instruction_identity");
        HoldSources(root, sources); sources.VerifyUnchanged(); return new(root, parsed, contract);
    }

    internal static LinuxElfPackageView RequirePackage(ReadOnlySpan<byte> package, bool lifecycle = false)
        => RequirePackage(package, lifecycle ? NativeAotServiceContract.V2 : NativeAotServiceContract.V1);

    internal static LinuxElfPackageView RequirePackage(ReadOnlySpan<byte> package, NativeAotServiceContract contract)
    {
        RequireReviewedContract(contract);
        var description = NativeAotServicePackage.Describe(package, contract);
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
        => Read(root, packagePath, disassemblyPath, lifecycle ? NativeAotServiceContract.V2 : NativeAotServiceContract.V1);

    /// <summary>Read locked owned snapshots for the explicit reviewed contract.
    /// Unknown contracts reject before opening files;
    /// all original alias, source and instruction checks remain required.</summary>
    public static NativeAotServiceProgram Read(string root, string packagePath, string disassemblyPath, NativeAotServiceContract contract)
    {
        RequireReviewedContract(contract);
        packagePath = Path.GetFullPath(packagePath); disassemblyPath = Path.GetFullPath(disassemblyPath);
        using AuthorityFiles files = new();
        var package = files.Open(Path.GetDirectoryName(packagePath)!, packagePath, "service package", "service_instruction_package", LinuxElfPackage.MaximumBytes, singleLink: true);
        var dump = files.Open(Path.GetDirectoryName(disassemblyPath)!, disassemblyPath, "service disassembly", "service_instruction_dump", MaximumDisassemblyBytes, singleLink: true);
        AuthorityFiles.RequireDistinct(package, dump);
        var program = Create(root, package.ReadBytes(LinuxElfPackage.MaximumBytes, "service_instruction_package"),
            dump.ReadUtf8(MaximumDisassemblyBytes, "service_instruction_dump"), contract);
        files.VerifyUnchanged(); return program;
    }

    private static void RequireReviewedContract(NativeAotServiceContract contract)
    {
        if (contract is not (NativeAotServiceContract.V1 or NativeAotServiceContract.V2 or NativeAotServiceContract.QuotaFailure))
            throw new ArgumentOutOfRangeException(nameof(contract));
    }

    private static readonly (string Path, string Hash)[] Sources = [
        ("global.json", "BE8663081FD82196AC86E65E9021D58D7C03A13574CE8CD4DC803937FC3BA30E"),
        ("Directory.Build.props", "DF8A0D794DB73060124F5134EB930DCDBF6EE526C2410227F0C668AB6D10C5E5"),
        ("Directory.Build.targets", "85AFD90F9DD0F550B6A3C6AD71D28BF714A8C930158DB872F8DA5D316889931A"),
        ("tools/Directory.Build.props", "44B8268EA6934335DBC79FEF47C1CE1FDEE49393C929C92E322417B14AA321AE"),
        ("tools/OS.Tooling/NativeAotPayload.cs", "E3EC6BCE13C70C99F3D7306541D68104876FAF73593325107319719811694D2F"),
        // Current consumer pin; the original shared publisher helpers are unchanged.
        ("tools/OS.Tooling/NativeAotWebPayload.cs", "129FD1AE91130FBFFC772D4A189A1FBB4EEEBF8236FF69B6B827A3A40B4C1FA3"),
        ("tools/OS.Tooling/NativeAotSocketFixturePublisher.cs", "A9029B6AFD17828D7182B7BFC2E971857BDF9469BA38D570AC6E6EBBCD197494"),
        ("tools/OS.Tooling/NativeAotServicePublisher.cs", "D3DFC72743598FE90255B236CC253E2332FAE68CEE7F61F5DAC4D69D3F4AF4FD"),
        ("apps/MinimalApiService/MinimalApiService.csproj", "34B6A796F2F0B3F1A404A5CF13882FB57DF8BF248210096D5D65768FDCA740F3"),
        ("apps/MinimalApiService/Program.cs", "09D039670EEF4D7026EAD72655FFD7F936FB7E05FB473926B4CD519E5ED70ACD"),
        ("apps/MinimalApiService/ServiceQuotaExercise.cs", "31C19556890F5C100D83A63F462ACCF3F87374642F9B43FCE9075B73D66B2867"),
        ("apps/MinimalApiService/ServiceLifecycleExercise.cs", "86712B104BEACD7FDD13DA7F7BA25D94D24FF642C6581E0A34C98D8CCAB5123B"),
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
