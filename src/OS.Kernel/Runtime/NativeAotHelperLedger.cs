namespace OS.Kernel.Runtime;

[Flags]
public enum NativeAotCompletionGate : uint
{
    None = 0,
    CleanBuild = 1u << 0,
    Boot = 1u << 1,
    FaultDiagnostics = 1u << 2,
    StdRuntime = 1u << 3,
    UefiEncapsulation = 1u << 4,
    HostedDotNetKestrel = 1u << 5,
    ExtendedHostedTier = 1u << 6
}

public enum NativeAotHelperArea
{
    Unknown = 0,
    ObjectAllocation = 1,
    ArrayAllocation = 2,
    StringAllocation = 3,
    WriteBarriers = 4,
    InterfaceDispatch = 5,
    ClassConstructors = 6,
    ThreadStatics = 7,
    ExceptionUnwind = 8,
    ModuleInitialization = 9,
    TypeManagerRtrScans = 10,
    GcStackRoots = 11,
    EfiReversePInvoke = 12,
    EfiFailFastTypeCasts = 13,
    EfiStackProbe = 14
}

public enum NativeAotHelperStatus
{
    Unknown = 0,
    Deferred = 1,
    EfiOnlyLinkStub = 2,
    EfiOnlyFailFastStub = 3,
    Complete = 4,
    EfiOnlyStackProbe = 5
}

public readonly struct NativeAotHelperLedgerEntry
{
    public NativeAotHelperLedgerEntry(
        NativeAotHelperArea area,
        NativeAotHelperStatus status,
        NativeAotCompletionGate completionGates,
        bool isEfiOnly,
        bool satisfiesKernelRuntimeCompletion,
        string helperSymbols,
        string evidence,
        string requiredContract,
        string allowedMilestoneStub,
        string testCoverage,
        string ownerSubsystem,
        string nextStep)
    {
        Area = area;
        Status = status;
        CompletionGates = completionGates;
        IsEfiOnly = isEfiOnly;
        SatisfiesKernelRuntimeCompletion = satisfiesKernelRuntimeCompletion;
        HelperSymbols = helperSymbols;
        Evidence = evidence;
        RequiredContract = requiredContract;
        AllowedMilestoneStub = allowedMilestoneStub;
        TestCoverage = testCoverage;
        OwnerSubsystem = ownerSubsystem;
        NextStep = nextStep;
    }

    public NativeAotHelperArea Area { get; }

    public NativeAotHelperStatus Status { get; }

    public NativeAotCompletionGate CompletionGates { get; }

    public bool IsEfiOnly { get; }

    public bool SatisfiesKernelRuntimeCompletion { get; }

    public string HelperSymbols { get; }

    public string Evidence { get; }

    public string RequiredContract { get; }

    public string AllowedMilestoneStub { get; }

    public string TestCoverage { get; }

    public string OwnerSubsystem { get; }

    public string NextStep { get; }

    public bool IsComplete => Status == NativeAotHelperStatus.Complete;

    public bool AppliesTo(NativeAotCompletionGate gate)
    {
        return gate != NativeAotCompletionGate.None && (CompletionGates & gate) == gate;
    }
}

public static class NativeAotHelperLedger
{
    public const int EntryCount = 14;

    private const NativeAotCompletionGate RuntimeGates =
        NativeAotCompletionGate.StdRuntime |
        NativeAotCompletionGate.HostedDotNetKestrel |
        NativeAotCompletionGate.ExtendedHostedTier;

    private const NativeAotCompletionGate EfiOnlyGates =
        NativeAotCompletionGate.CleanBuild |
        NativeAotCompletionGate.Boot;

    public static bool TryGetEntry(int index, out NativeAotHelperLedgerEntry entry)
    {
        if (index < 0 || index >= EntryCount)
        {
            entry = InvalidEntry();
            return false;
        }

        entry = GetEntry(index);
        return true;
    }

    public static NativeAotHelperLedgerEntry GetEntry(int index)
    {
        return index switch
        {
            0 => new NativeAotHelperLedgerEntry(
                NativeAotHelperArea.ObjectAllocation,
                NativeAotHelperStatus.Deferred,
                RuntimeGates,
                isEfiOnly: false,
                satisfiesKernelRuntimeCompletion: false,
                "contract:object-allocation",
                "No OS-owned NativeAOT object allocation helper or managed heap handoff is implemented. BootPageFreeListBuilder, BootPageAllocatorStateInitializer, BootPageAllocatorMutation, and NativeAotHeapBootstrapPlanner host-test prerequisite caller-owned free-list node construction, allocator state metadata, caller-owned free-list mutation, and future heap page-budget metadata only.",
                "Allocate managed objects with layout, alignment, method-table initialization, and GC registration semantics matching NativeAOT expectations.",
                "Fail-fast or deferred status only; no helper may report allocation success before heap and GC ownership exist.",
                "Host ledger tests, boot page free-list builder tests, boot page allocator state tests, boot page allocator mutation tests, and NativeAOT heap bootstrap plan tests cover prerequisite metadata; no allocation helper execution test exists.",
                "runtime-gc",
                "Reserve heap pages from the page allocator and define GC registration before marking object allocation complete."),

            1 => new NativeAotHelperLedgerEntry(
                NativeAotHelperArea.ArrayAllocation,
                NativeAotHelperStatus.Deferred,
                RuntimeGates,
                isEfiOnly: false,
                satisfiesKernelRuntimeCompletion: false,
                "contract:array-allocation",
                "No OS-owned NativeAOT array allocation helper exists.",
                "Allocate arrays with valid length, element-size, bounds, method-table, and GC metadata.",
                "Fail-fast or deferred status only; no stub may return a fabricated managed array.",
                "Host ledger tests only; no array helper execution test exists.",
                "runtime-gc",
                "Implement array allocation after object allocation and element layout metadata are defined."),

            2 => new NativeAotHelperLedgerEntry(
                NativeAotHelperArea.StringAllocation,
                NativeAotHelperStatus.Deferred,
                RuntimeGates,
                isEfiOnly: false,
                satisfiesKernelRuntimeCompletion: false,
                "contract:string-allocation",
                "No OS-owned NativeAOT string allocation helper exists.",
                "Allocate immutable strings with correct length, UTF-16 storage, object header, and GC visibility.",
                "Fail-fast or deferred status only; no stub may fabricate string references.",
                "Host ledger tests only; no string helper execution test exists.",
                "runtime-gc",
                "Implement string allocation after object allocation and string layout are defined."),

            3 => new NativeAotHelperLedgerEntry(
                NativeAotHelperArea.WriteBarriers,
                NativeAotHelperStatus.Deferred,
                RuntimeGates,
                isEfiOnly: false,
                satisfiesKernelRuntimeCompletion: false,
                "contract:write-barriers",
                "No kernel/runtime write barrier implementation exists.",
                "Maintain GC card-table or equivalent remembered-set semantics for every managed reference write required by the selected GC.",
                "Fail-fast or no-op only when no managed heap/reference mutation can occur in the proven milestone path.",
                "Host ledger tests only; no write-barrier mutation test exists.",
                "runtime-gc",
                "Design write barriers with the managed GC before enabling reference writes through helpers."),

            4 => new NativeAotHelperLedgerEntry(
                NativeAotHelperArea.InterfaceDispatch,
                NativeAotHelperStatus.Deferred,
                RuntimeGates,
                isEfiOnly: false,
                satisfiesKernelRuntimeCompletion: false,
                "contract:interface-dispatch;contract:type-cast",
                "EFI type-cast helpers fail fast; no kernel/runtime interface dispatch support is claimed by this repository.",
                "Resolve interface calls and type checks using NativeAOT-compatible metadata without reflection or dynamic code generation.",
                "EFI-only fail-fast stubs may halt; kernel/runtime paths must not depend on interface dispatch until implemented.",
                "Host ledger tests and EFI RuntimeExport symbol coverage only.",
                "runtime-dispatch",
                "Implement and test NativeAOT-compatible interface dispatch before relying on interface calls in kernel runtime paths."),

            5 => new NativeAotHelperLedgerEntry(
                NativeAotHelperArea.ClassConstructors,
                NativeAotHelperStatus.Deferred,
                RuntimeGates,
                isEfiOnly: false,
                satisfiesKernelRuntimeCompletion: false,
                "contract:class-constructor-runner",
                "Class constructor runner support is not implemented for kernel/runtime paths. NativeAotClassConstructorStateTable, NativeAotClassConstructorRunnerPreflight, and NativeAotClassConstructorWorkItemPlanner host-test caller-owned state transitions, ready/default/failed/pending metadata, target metadata counts, caller-owned work-item metadata, and caller-owned diagnostics only.",
                "Run type initializers once with deterministic ordering, recursion handling, failure diagnostics, and memory visibility.",
                "Deferred status only; avoid lazy static reference fields in kernel/runtime paths until implemented.",
                "Host ledger tests, NativeAOT cctor state/preflight tests, and work-item plan tests cover prerequisite metadata; no cctor execution test exists.",
                "runtime-initialization",
                "Implement metadata discovery and the cctor runner invocation path before using lazy static reference fields."),

            6 => new NativeAotHelperLedgerEntry(
                NativeAotHelperArea.ThreadStatics,
                NativeAotHelperStatus.Deferred,
                RuntimeGates,
                isEfiOnly: false,
                satisfiesKernelRuntimeCompletion: false,
                "contract:thread-statics",
                "Thread-static storage helpers are not implemented.",
                "Provide per-thread static bases, initialization, lifetime, and scheduler integration compatible with NativeAOT helper expectations.",
                "Deferred status only while the kernel remains effectively single-threaded for tested runtime paths.",
                "Host ledger tests only; no TLS/thread-static execution test exists.",
                "runtime-threading",
                "Define TLS bootstrap and NativeAOT thread-static base helpers with scheduler integration."),

            7 => new NativeAotHelperLedgerEntry(
                NativeAotHelperArea.ExceptionUnwind,
                NativeAotHelperStatus.Deferred,
                RuntimeGates | NativeAotCompletionGate.FaultDiagnostics,
                isEfiOnly: false,
                satisfiesKernelRuntimeCompletion: false,
                "contract:exception-unwind;contract:personality-function",
                "Throw helpers halt; catch, finally, stack unwinding, and personality functions are not implemented.",
                "Support throw, catch, finally, personality/unwind metadata, corrupt-unwind panic fallback, and stack diagnostics.",
                "Fail-fast throw helpers may halt in EFI-only paths; kernel boot/runtime code must not depend on catch/finally.",
                "Host ledger tests only; panic/fault diagnostics are separate and do not prove managed unwind.",
                "runtime-exceptions",
                "Implement unwind metadata scanning and managed exception dispatch before marking exception handling complete."),

            8 => new NativeAotHelperLedgerEntry(
                NativeAotHelperArea.ModuleInitialization,
                NativeAotHelperStatus.Deferred,
                RuntimeGates,
                isEfiOnly: false,
                satisfiesKernelRuntimeCompletion: false,
                "contract:module-initialization",
                "Kernel/runtime module initialization helper behavior is not implemented.",
                "Run module initializers deterministically before dependent code observes initialized module state.",
                "Deferred status only; modules that require initialization must be explicit and host-tested.",
                "Host ledger tests only; no module-initializer execution test exists.",
                "runtime-initialization",
                "Define module initializer ordering and failure diagnostics before enabling runtime module init support."),

            9 => new NativeAotHelperLedgerEntry(
                NativeAotHelperArea.TypeManagerRtrScans,
                NativeAotHelperStatus.Deferred,
                RuntimeGates | NativeAotCompletionGate.HostedDotNetKestrel,
                isEfiOnly: false,
                satisfiesKernelRuntimeCompletion: false,
                "contract:typemanager-rtr-scans",
                "TypeManager and runtime type representation scans are not implemented.",
                "Discover and validate NativeAOT TypeManager/RTR metadata required by dispatch, casts, reflection-adjacent hosted paths, and GC.",
                "Deferred status only; hosted runtime and reflection-adjacent paths remain blocked.",
                "Host ledger tests only; no metadata scan execution test exists.",
                "runtime-metadata",
                "Add metadata discovery and tests before enabling hosted runtime or reflection-adjacent paths."),

            10 => new NativeAotHelperLedgerEntry(
                NativeAotHelperArea.GcStackRoots,
                NativeAotHelperStatus.Deferred,
                RuntimeGates,
                isEfiOnly: false,
                satisfiesKernelRuntimeCompletion: false,
                "contract:gc-stack-roots",
                "GC stack-root registration and scanning helpers are not implemented.",
                "Enumerate live managed stack/register roots across scheduler-owned stacks with precise enough metadata for the selected GC.",
                "Deferred status only; no GC collection claim may depend on stack scanning.",
                "Host ledger tests only; no GC stack-root scan test exists.",
                "runtime-gc",
                "Implement stack root discovery with scheduler/stack ownership before GC stress can be claimed."),

            11 => new NativeAotHelperLedgerEntry(
                NativeAotHelperArea.EfiReversePInvoke,
                NativeAotHelperStatus.EfiOnlyLinkStub,
                EfiOnlyGates,
                isEfiOnly: true,
                satisfiesKernelRuntimeCompletion: false,
                "RhpReversePInvoke;RhpReversePInvokeReturn;RhpPInvoke;RhpPInvokeReturn",
                "RhpReversePInvoke/RhpReversePInvokeReturn/RhpPInvoke/RhpPInvokeReturn are EFI shell link stubs only.",
                "Track reverse P/Invoke transitions if a future hosted/native boundary requires managed/native transition bookkeeping.",
                "EFI link-only no-op stubs are allowed solely for the current no-stdlib EFI shell boundary.",
                "Host ledger tests verify every current EFI RuntimeExport symbol is represented exactly once.",
                "efi-runtime-shim",
                "Replace or isolate these stubs when a real kernel/runtime reverse-P/Invoke policy is needed."),

            12 => new NativeAotHelperLedgerEntry(
                NativeAotHelperArea.EfiFailFastTypeCasts,
                NativeAotHelperStatus.EfiOnlyFailFastStub,
                EfiOnlyGates,
                isEfiOnly: true,
                satisfiesKernelRuntimeCompletion: false,
                "RhpFallbackFailFast;RhTypeCast_IsInstanceOfClass;RhTypeCast_CheckCastClass;RhTypeCast_CheckCastClassSpecial;RhTypeCast_IsInstanceOfArray;RhTypeCast_CheckCastArray;RhTypeCast_IsInstanceOfInterface;RhTypeCast_CheckCastInterface;RhTypeCast_IsInstanceOfAny;RhTypeCast_CheckCastAny;RhTypeCast_CheckCastAny_NoCacheLookup;RhTypeCast_IsInstanceOfException",
                "RhTypeCast_* helpers in the EFI shell deterministically halt and do not implement type checks.",
                "Implement NativeAOT-compatible type checks and cast helpers over validated runtime type metadata.",
                "EFI fail-fast stubs are allowed only while tested EFI code never requires successful type checks.",
                "Host ledger tests verify every current EFI RuntimeExport symbol is represented exactly once.",
                "efi-runtime-shim",
                "Implement real type-cast and interface-dispatch helpers before using these paths outside the EFI shell."),

            13 => new NativeAotHelperLedgerEntry(
                NativeAotHelperArea.EfiStackProbe,
                NativeAotHelperStatus.EfiOnlyStackProbe,
                EfiOnlyGates,
                isEfiOnly: true,
                satisfiesKernelRuntimeCompletion: false,
                "RhpStackProbe",
                "M01-021 source/readback tests lock the exact 27-byte page-probe plan, and the emitted-object audit verifies the fail-closed bridge shape and pre-transfer frame budget; no guest execution is claimed.",
                "Honor the pinned .NET 10 x64 RhpStackProbe special ABI: consume the target in R11, touch each crossed page downward, preserve R11 and RSP until tail dispatch, and fail closed before an audited target is installed.",
                "An audited fail-closed bridge plus exact read-back-verified first-party byte plan is allowed for the EFI stage only; never use a no-op or ordinary-argument approximation.",
                "M01-021 focused source, mutation, readback, static-scratch, and generated-object frame/bridge audits cover the current build boundary.",
                "efi-runtime-shim",
                "M01-022 must prove the enabled stack-probe and owned-stack path in a real QEMU guest; re-audit the bridge and frame chain after any source or toolchain change."),

            _ => InvalidEntry()
        };
    }

    private static NativeAotHelperLedgerEntry InvalidEntry()
    {
        return new NativeAotHelperLedgerEntry(
            NativeAotHelperArea.Unknown,
            NativeAotHelperStatus.Unknown,
            NativeAotCompletionGate.None,
            isEfiOnly: false,
            satisfiesKernelRuntimeCompletion: false,
            "invalid",
            "Invalid helper ledger index.",
            "No helper contract.",
            "No stub allowed.",
            "TryGetEntry invalid-index host test.",
            "runtime-ledger",
            "Use TryGetEntry with an index from zero to EntryCount minus one.");
    }
}
