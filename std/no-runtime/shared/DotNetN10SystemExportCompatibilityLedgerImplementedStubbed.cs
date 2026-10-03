namespace OS.Std.NoRuntime;

public enum DotNetN10HelperSupportState : byte
{
    Unknown = 0,
    NarrowlyImplemented = 1,
    Stubbed = 2,
    Forbidden = 3
}

public enum DotNetN10SystemDeclarationCategory : byte
{
    Unknown = 0,
    RuntimeControl = 1,
    CoreType = 2,
    PrimitiveType = 3,
    MemoryHelper = 4,
    AttributeContract = 5,
    RuntimeHandle = 6,
    RuntimeExportInfrastructure = 7,
    CompilerServices = 8,
    InteropContract = 9
}

public enum DotNetN10SystemDeclaration : byte
{
    Unknown = 0,
    RuntimeFailFast = 1,
    Object = 2,
    Void = 3,
    Boolean = 4,
    Char = 5,
    SByte = 6,
    Byte = 7,
    Int16 = 8,
    UInt16 = 9,
    Int32 = 10,
    UInt32 = 11,
    Int64 = 12,
    UInt64 = 13,
    IntPtr = 14,
    UIntPtr = 15,
    Single = 16,
    Double = 17,
    ValueType = 18,
    Enum = 19,
    Type = 20,
    RuntimeType = 21,
    Array = 22,
    Delegate = 23,
    MulticastDelegate = 24,
    String = 25,
    Attribute = 26,
    Buffer = 27,
    SpanHelpers = 28,
    AttributeTargets = 29,
    AttributeUsageAttribute = 30,
    RuntimeTypeHandle = 31,
    RuntimeMethodHandle = 32,
    RuntimeFieldHandle = 33,
    RuntimeExportAttribute = 34,
    X64StackProbeRuntime = 35,
    TypeCast = 36,
    RuntimeFeature = 37,
    RuntimeHelpers = 38,
    UnmanagedCallersOnlyAttribute = 39,
    IDynamicInterfaceCastable = 40,
    IntrinsicAttribute = 41,
    Unsafe = 42,
    GenericArray = 43,
    Nullable = 44,
    RhpNewFastRuntime = 45
}

public readonly struct DotNetN10SystemDeclarationCompatibilityEntry
{
    internal DotNetN10SystemDeclarationCompatibilityEntry(
        DotNetN10SystemDeclaration declaration,
        DotNetN10SystemDeclarationCategory category,
        DotNetN10HelperSupportState supportState)
    {
        Declaration = declaration;
        Category = category;
        SupportState = supportState;
    }

    public DotNetN10SystemDeclaration Declaration { get; }

    public DotNetN10SystemDeclarationCategory Category { get; }

    public DotNetN10HelperSupportState SupportState { get; }

    public bool IsInitialized =>
        Declaration != DotNetN10SystemDeclaration.Unknown &&
        Category != DotNetN10SystemDeclarationCategory.Unknown &&
        SupportState != DotNetN10HelperSupportState.Unknown;

    public bool SatisfiesRuntimeCompletionGate4 => false;
}

public enum DotNetN10RuntimeExportCategory : byte
{
    Unknown = 0,
    StackProbe = 1,
    TypeCast = 2,
    NativeTransition = 3,
    FailFast = 4,
    FixedAllocation = 5,
    SzArrayAllocation = 6
}

public enum DotNetN10RuntimeExportBehavior : byte
{
    Unknown = 0,
    LinkOnlyNoOp = 1,
    FailFast = 2,
    AuditedStackProbe = 3,
    ProfileBoundFixedAllocation = 4,
    ProfileBoundSzArrayAllocation = 5
}

public enum DotNetN10RuntimeExport : byte
{
    Unknown = 0,
    RhpStackProbe = 1,
    RhTypeCast_IsInstanceOfClass = 2,
    RhTypeCast_CheckCastClass = 3,
    RhTypeCast_CheckCastClassSpecial = 4,
    RhTypeCast_IsInstanceOfArray = 5,
    RhTypeCast_CheckCastArray = 6,
    RhTypeCast_IsInstanceOfInterface = 7,
    RhTypeCast_CheckCastInterface = 8,
    RhTypeCast_IsInstanceOfAny = 9,
    RhTypeCast_CheckCastAny = 10,
    RhTypeCast_CheckCastAny_NoCacheLookup = 11,
    RhTypeCast_IsInstanceOfException = 12,
    RhpReversePInvoke = 13,
    RhpReversePInvokeReturn = 14,
    RhpPInvoke = 15,
    RhpPInvokeReturn = 16,
    RhpFallbackFailFast = 17,
    RhpNewFast = 18,
    RhpNewArrayFast = 19,
    RhpNewPtrArrayFast = 20
}

public readonly struct DotNetN10RuntimeExportCompatibilityEntry
{
    internal DotNetN10RuntimeExportCompatibilityEntry(
        DotNetN10RuntimeExport runtimeExport,
        DotNetN10RuntimeExportCategory category,
        DotNetN10RuntimeExportBehavior behavior,
        DotNetN10HelperSupportState supportState)
    {
        RuntimeExport = runtimeExport;
        Category = category;
        Behavior = behavior;
        SupportState = supportState;
    }

    public DotNetN10RuntimeExport RuntimeExport { get; }

    public DotNetN10RuntimeExportCategory Category { get; }

    public DotNetN10RuntimeExportBehavior Behavior { get; }

    public DotNetN10HelperSupportState SupportState { get; }

    public bool IsInitialized =>
        RuntimeExport != DotNetN10RuntimeExport.Unknown &&
        Category != DotNetN10RuntimeExportCategory.Unknown &&
        Behavior != DotNetN10RuntimeExportBehavior.Unknown &&
        SupportState != DotNetN10HelperSupportState.Unknown;

    public bool SatisfiesRuntimeCompletionGate4 => false;
}

public static class DotNetN10SystemExportCompatibilityLedgerImplementedStubbed
{
    public const int SystemDeclarationCount = 45;

    public const int RuntimeExportCount = 20;

    public const bool AddsCanonicalSystemNamespace = false;

    public const bool SatisfiesRuntimeCompletionGate4 = false;

    public static bool TryGetSystemDeclaration(
        int index,
        out DotNetN10SystemDeclarationCompatibilityEntry entry)
    {
        if (index < 0 || index >= SystemDeclarationCount)
        {
            entry = InvalidSystemDeclarationEntry();
            return false;
        }

        entry = GetSystemDeclaration(index);
        return entry.IsInitialized;
    }

    public static DotNetN10SystemDeclarationCompatibilityEntry
        GetSystemDeclaration(int index)
    {
        return index switch
        {
            0 => SystemDeclaration(
                DotNetN10SystemDeclaration.RuntimeFailFast,
                DotNetN10SystemDeclarationCategory.RuntimeControl,
                DotNetN10HelperSupportState.NarrowlyImplemented),
            1 => StubbedCore(DotNetN10SystemDeclaration.Object),
            2 => StubbedPrimitive(DotNetN10SystemDeclaration.Void),
            3 => StubbedPrimitive(DotNetN10SystemDeclaration.Boolean),
            4 => StubbedPrimitive(DotNetN10SystemDeclaration.Char),
            5 => StubbedPrimitive(DotNetN10SystemDeclaration.SByte),
            6 => StubbedPrimitive(DotNetN10SystemDeclaration.Byte),
            7 => StubbedPrimitive(DotNetN10SystemDeclaration.Int16),
            8 => StubbedPrimitive(DotNetN10SystemDeclaration.UInt16),
            9 => StubbedPrimitive(DotNetN10SystemDeclaration.Int32),
            10 => StubbedPrimitive(DotNetN10SystemDeclaration.UInt32),
            11 => StubbedPrimitive(DotNetN10SystemDeclaration.Int64),
            12 => StubbedPrimitive(DotNetN10SystemDeclaration.UInt64),
            13 => StubbedPrimitive(DotNetN10SystemDeclaration.IntPtr),
            14 => StubbedPrimitive(DotNetN10SystemDeclaration.UIntPtr),
            15 => StubbedPrimitive(DotNetN10SystemDeclaration.Single),
            16 => StubbedPrimitive(DotNetN10SystemDeclaration.Double),
            17 => StubbedCore(DotNetN10SystemDeclaration.ValueType),
            18 => StubbedCore(DotNetN10SystemDeclaration.Enum),
            19 => StubbedCore(DotNetN10SystemDeclaration.Type),
            20 => StubbedCore(DotNetN10SystemDeclaration.RuntimeType),
            // Only the compiler-required first length field and bounded int getter.
            21 => SystemDeclaration(DotNetN10SystemDeclaration.Array,
                DotNetN10SystemDeclarationCategory.CoreType,
                DotNetN10HelperSupportState.NarrowlyImplemented),
            22 => StubbedCore(DotNetN10SystemDeclaration.Delegate),
            23 => StubbedCore(DotNetN10SystemDeclaration.MulticastDelegate),
            24 => StubbedCore(DotNetN10SystemDeclaration.String),
            25 => SystemDeclaration(
                DotNetN10SystemDeclaration.Attribute,
                DotNetN10SystemDeclarationCategory.AttributeContract,
                DotNetN10HelperSupportState.Stubbed),
            26 => SystemDeclaration(
                DotNetN10SystemDeclaration.Buffer,
                DotNetN10SystemDeclarationCategory.MemoryHelper,
                DotNetN10HelperSupportState.NarrowlyImplemented),
            27 => SystemDeclaration(
                DotNetN10SystemDeclaration.SpanHelpers,
                DotNetN10SystemDeclarationCategory.MemoryHelper,
                DotNetN10HelperSupportState.NarrowlyImplemented),
            28 => StubbedAttribute(DotNetN10SystemDeclaration.AttributeTargets),
            29 => StubbedAttribute(
                DotNetN10SystemDeclaration.AttributeUsageAttribute),
            30 => StubbedRuntimeHandle(
                DotNetN10SystemDeclaration.RuntimeTypeHandle),
            31 => StubbedRuntimeHandle(
                DotNetN10SystemDeclaration.RuntimeMethodHandle),
            32 => StubbedRuntimeHandle(
                DotNetN10SystemDeclaration.RuntimeFieldHandle),
            33 => SystemDeclaration(
                DotNetN10SystemDeclaration.RuntimeExportAttribute,
                DotNetN10SystemDeclarationCategory.RuntimeExportInfrastructure,
                DotNetN10HelperSupportState.Stubbed),
            34 => SystemDeclaration(
                DotNetN10SystemDeclaration.X64StackProbeRuntime,
                DotNetN10SystemDeclarationCategory.RuntimeExportInfrastructure,
                DotNetN10HelperSupportState.NarrowlyImplemented),
            35 => SystemDeclaration(
                DotNetN10SystemDeclaration.TypeCast,
                DotNetN10SystemDeclarationCategory.RuntimeExportInfrastructure,
                DotNetN10HelperSupportState.Forbidden),
            36 => NarrowCompilerServices(
                DotNetN10SystemDeclaration.RuntimeFeature),
            37 => NarrowCompilerServices(
                DotNetN10SystemDeclaration.RuntimeHelpers),
            38 => SystemDeclaration(
                DotNetN10SystemDeclaration.UnmanagedCallersOnlyAttribute,
                DotNetN10SystemDeclarationCategory.InteropContract,
                DotNetN10HelperSupportState.Stubbed),
            39 => SystemDeclaration(
                DotNetN10SystemDeclaration.IDynamicInterfaceCastable,
                DotNetN10SystemDeclarationCategory.InteropContract,
                DotNetN10HelperSupportState.Forbidden),
            40 => StubbedAttribute(DotNetN10SystemDeclaration.IntrinsicAttribute),
            41 => NarrowCompilerServices(DotNetN10SystemDeclaration.Unsafe),
            42 => StubbedCore(DotNetN10SystemDeclaration.GenericArray),
            43 => StubbedCore(DotNetN10SystemDeclaration.Nullable),
            44 => SystemDeclaration(DotNetN10SystemDeclaration.RhpNewFastRuntime,
                DotNetN10SystemDeclarationCategory.RuntimeExportInfrastructure,
                DotNetN10HelperSupportState.NarrowlyImplemented),
            _ => InvalidSystemDeclarationEntry()
        };
    }

    public static bool TryGetRuntimeExport(
        int index,
        out DotNetN10RuntimeExportCompatibilityEntry entry)
    {
        if (index < 0 || index >= RuntimeExportCount)
        {
            entry = InvalidRuntimeExportEntry();
            return false;
        }

        entry = GetRuntimeExport(index);
        return entry.IsInitialized;
    }

    public static DotNetN10RuntimeExportCompatibilityEntry GetRuntimeExport(
        int index)
    {
        return index switch
        {
            0 => RuntimeExport(
                DotNetN10RuntimeExport.RhpStackProbe,
                DotNetN10RuntimeExportCategory.StackProbe,
                DotNetN10RuntimeExportBehavior.AuditedStackProbe,
                DotNetN10HelperSupportState.NarrowlyImplemented),
            1 => ForbiddenTypeCast(
                DotNetN10RuntimeExport.RhTypeCast_IsInstanceOfClass),
            2 => ForbiddenTypeCast(
                DotNetN10RuntimeExport.RhTypeCast_CheckCastClass),
            3 => ForbiddenTypeCast(
                DotNetN10RuntimeExport.RhTypeCast_CheckCastClassSpecial),
            4 => ForbiddenTypeCast(
                DotNetN10RuntimeExport.RhTypeCast_IsInstanceOfArray),
            5 => ForbiddenTypeCast(
                DotNetN10RuntimeExport.RhTypeCast_CheckCastArray),
            6 => ForbiddenTypeCast(
                DotNetN10RuntimeExport.RhTypeCast_IsInstanceOfInterface),
            7 => ForbiddenTypeCast(
                DotNetN10RuntimeExport.RhTypeCast_CheckCastInterface),
            8 => ForbiddenTypeCast(
                DotNetN10RuntimeExport.RhTypeCast_IsInstanceOfAny),
            9 => ForbiddenTypeCast(
                DotNetN10RuntimeExport.RhTypeCast_CheckCastAny),
            10 => ForbiddenTypeCast(
                DotNetN10RuntimeExport.RhTypeCast_CheckCastAny_NoCacheLookup),
            11 => ForbiddenTypeCast(
                DotNetN10RuntimeExport.RhTypeCast_IsInstanceOfException),
            12 => LinkOnlyTransition(
                DotNetN10RuntimeExport.RhpReversePInvoke),
            13 => LinkOnlyTransition(
                DotNetN10RuntimeExport.RhpReversePInvokeReturn),
            14 => LinkOnlyTransition(DotNetN10RuntimeExport.RhpPInvoke),
            15 => LinkOnlyTransition(DotNetN10RuntimeExport.RhpPInvokeReturn),
            16 => RuntimeExport(
                DotNetN10RuntimeExport.RhpFallbackFailFast,
                DotNetN10RuntimeExportCategory.FailFast,
                DotNetN10RuntimeExportBehavior.FailFast,
                DotNetN10HelperSupportState.Forbidden),
            17 => RuntimeExport(DotNetN10RuntimeExport.RhpNewFast,
                DotNetN10RuntimeExportCategory.FixedAllocation,
                DotNetN10RuntimeExportBehavior.ProfileBoundFixedAllocation,
                DotNetN10HelperSupportState.NarrowlyImplemented),
            18 => RuntimeExport(DotNetN10RuntimeExport.RhpNewArrayFast,
                DotNetN10RuntimeExportCategory.SzArrayAllocation,
                DotNetN10RuntimeExportBehavior.ProfileBoundSzArrayAllocation,
                DotNetN10HelperSupportState.NarrowlyImplemented),
            19 => RuntimeExport(DotNetN10RuntimeExport.RhpNewPtrArrayFast,
                DotNetN10RuntimeExportCategory.SzArrayAllocation,
                DotNetN10RuntimeExportBehavior.ProfileBoundSzArrayAllocation,
                DotNetN10HelperSupportState.NarrowlyImplemented),
            _ => InvalidRuntimeExportEntry()
        };
    }

    private static DotNetN10SystemDeclarationCompatibilityEntry
        StubbedCore(DotNetN10SystemDeclaration declaration)
    {
        return SystemDeclaration(
            declaration,
            DotNetN10SystemDeclarationCategory.CoreType,
            DotNetN10HelperSupportState.Stubbed);
    }

    private static DotNetN10SystemDeclarationCompatibilityEntry
        StubbedPrimitive(DotNetN10SystemDeclaration declaration)
    {
        return SystemDeclaration(
            declaration,
            DotNetN10SystemDeclarationCategory.PrimitiveType,
            DotNetN10HelperSupportState.Stubbed);
    }

    private static DotNetN10SystemDeclarationCompatibilityEntry
        StubbedAttribute(DotNetN10SystemDeclaration declaration)
    {
        return SystemDeclaration(
            declaration,
            DotNetN10SystemDeclarationCategory.AttributeContract,
            DotNetN10HelperSupportState.Stubbed);
    }

    private static DotNetN10SystemDeclarationCompatibilityEntry
        StubbedRuntimeHandle(DotNetN10SystemDeclaration declaration)
    {
        return SystemDeclaration(
            declaration,
            DotNetN10SystemDeclarationCategory.RuntimeHandle,
            DotNetN10HelperSupportState.Stubbed);
    }

    private static DotNetN10SystemDeclarationCompatibilityEntry
        NarrowCompilerServices(DotNetN10SystemDeclaration declaration)
    {
        return SystemDeclaration(
            declaration,
            DotNetN10SystemDeclarationCategory.CompilerServices,
            DotNetN10HelperSupportState.NarrowlyImplemented);
    }

    private static DotNetN10SystemDeclarationCompatibilityEntry
        SystemDeclaration(
            DotNetN10SystemDeclaration declaration,
            DotNetN10SystemDeclarationCategory category,
            DotNetN10HelperSupportState supportState)
    {
        return new DotNetN10SystemDeclarationCompatibilityEntry(
            declaration,
            category,
            supportState);
    }

    private static DotNetN10SystemDeclarationCompatibilityEntry
        InvalidSystemDeclarationEntry()
    {
        return new DotNetN10SystemDeclarationCompatibilityEntry(
            DotNetN10SystemDeclaration.Unknown,
            DotNetN10SystemDeclarationCategory.Unknown,
            DotNetN10HelperSupportState.Unknown);
    }

    private static DotNetN10RuntimeExportCompatibilityEntry ForbiddenTypeCast(
        DotNetN10RuntimeExport runtimeExport)
    {
        return RuntimeExport(
            runtimeExport,
            DotNetN10RuntimeExportCategory.TypeCast,
            DotNetN10RuntimeExportBehavior.FailFast,
            DotNetN10HelperSupportState.Forbidden);
    }

    private static DotNetN10RuntimeExportCompatibilityEntry LinkOnlyTransition(
        DotNetN10RuntimeExport runtimeExport)
    {
        return RuntimeExport(
            runtimeExport,
            DotNetN10RuntimeExportCategory.NativeTransition,
            DotNetN10RuntimeExportBehavior.LinkOnlyNoOp,
            DotNetN10HelperSupportState.Stubbed);
    }

    private static DotNetN10RuntimeExportCompatibilityEntry RuntimeExport(
        DotNetN10RuntimeExport runtimeExport,
        DotNetN10RuntimeExportCategory category,
        DotNetN10RuntimeExportBehavior behavior,
        DotNetN10HelperSupportState supportState)
    {
        return new DotNetN10RuntimeExportCompatibilityEntry(
            runtimeExport,
            category,
            behavior,
            supportState);
    }

    private static DotNetN10RuntimeExportCompatibilityEntry
        InvalidRuntimeExportEntry()
    {
        return new DotNetN10RuntimeExportCompatibilityEntry(
            DotNetN10RuntimeExport.Unknown,
            DotNetN10RuntimeExportCategory.Unknown,
            DotNetN10RuntimeExportBehavior.Unknown,
            DotNetN10HelperSupportState.Unknown);
    }
}
