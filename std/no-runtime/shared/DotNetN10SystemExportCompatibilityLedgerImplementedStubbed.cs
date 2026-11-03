namespace OS.Std.NoRuntime;

public static class DotNetN10SystemExportCompatibilityLedgerImplementedStubbed
{
    public const int SystemDeclarationCount = (int)DotNetN10SystemDeclaration.InAttribute;
    public const int RuntimeExportCount = 21;
    public const bool AddsCanonicalSystemNamespace = false;
    public const bool SatisfiesRuntimeCompletionGate4 = false;
    public static bool TryGetSystemDeclaration(int index, out DotNetN10SystemDeclarationCompatibilityEntry entry)
    {
        if (index < 0 || index >= SystemDeclarationCount)
        {
            entry = InvalidSystemDeclarationEntry();
            return false;
        }

        entry = GetSystemDeclaration(index);
        return entry.IsInitialized;
    }

    public static DotNetN10SystemDeclarationCompatibilityEntry GetSystemDeclaration(int index)
    {
        return index switch
        {
            0 => SystemDeclaration(DotNetN10SystemDeclaration.RuntimeFailFast, DotNetN10SystemDeclarationCategory.RuntimeControl, DotNetN10HelperSupportState.NarrowlyImplemented),
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
            21 => SystemDeclaration(DotNetN10SystemDeclaration.Array, DotNetN10SystemDeclarationCategory.CoreType, DotNetN10HelperSupportState.NarrowlyImplemented),
            22 => StubbedCore(DotNetN10SystemDeclaration.Delegate),
            23 => StubbedCore(DotNetN10SystemDeclaration.MulticastDelegate),
            // Only the finite char/count constructor, UTF-16 storage, Length and Chars.
            24 => SystemDeclaration(DotNetN10SystemDeclaration.String, DotNetN10SystemDeclarationCategory.CoreType, DotNetN10HelperSupportState.NarrowlyImplemented),
            25 => SystemDeclaration(DotNetN10SystemDeclaration.Attribute, DotNetN10SystemDeclarationCategory.AttributeContract, DotNetN10HelperSupportState.Stubbed),
            26 => SystemDeclaration(DotNetN10SystemDeclaration.Buffer, DotNetN10SystemDeclarationCategory.MemoryHelper, DotNetN10HelperSupportState.NarrowlyImplemented),
            27 => SystemDeclaration(DotNetN10SystemDeclaration.SpanHelpers, DotNetN10SystemDeclarationCategory.MemoryHelper, DotNetN10HelperSupportState.NarrowlyImplemented),
            28 => StubbedAttribute(DotNetN10SystemDeclaration.AttributeTargets),
            29 => StubbedAttribute(DotNetN10SystemDeclaration.AttributeUsageAttribute),
            30 => StubbedRuntimeHandle(DotNetN10SystemDeclaration.RuntimeTypeHandle),
            31 => StubbedRuntimeHandle(DotNetN10SystemDeclaration.RuntimeMethodHandle),
            32 => StubbedRuntimeHandle(DotNetN10SystemDeclaration.RuntimeFieldHandle),
            33 => SystemDeclaration(DotNetN10SystemDeclaration.RuntimeExportAttribute, DotNetN10SystemDeclarationCategory.RuntimeExportInfrastructure, DotNetN10HelperSupportState.Stubbed),
            34 => SystemDeclaration(DotNetN10SystemDeclaration.X64StackProbeRuntime, DotNetN10SystemDeclarationCategory.RuntimeExportInfrastructure, DotNetN10HelperSupportState.NarrowlyImplemented),
            35 => SystemDeclaration(DotNetN10SystemDeclaration.TypeCast, DotNetN10SystemDeclarationCategory.RuntimeExportInfrastructure, DotNetN10HelperSupportState.Forbidden),
            36 => NarrowCompilerServices(DotNetN10SystemDeclaration.RuntimeFeature),
            37 => NarrowCompilerServices(DotNetN10SystemDeclaration.RuntimeHelpers),
            38 => SystemDeclaration(DotNetN10SystemDeclaration.UnmanagedCallersOnlyAttribute, DotNetN10SystemDeclarationCategory.InteropContract, DotNetN10HelperSupportState.Stubbed),
            39 => SystemDeclaration(DotNetN10SystemDeclaration.IDynamicInterfaceCastable, DotNetN10SystemDeclarationCategory.InteropContract, DotNetN10HelperSupportState.Forbidden),
            40 => StubbedAttribute(DotNetN10SystemDeclaration.IntrinsicAttribute),
            41 => NarrowCompilerServices(DotNetN10SystemDeclaration.Unsafe),
            42 => StubbedCore(DotNetN10SystemDeclaration.GenericArray),
            43 => StubbedCore(DotNetN10SystemDeclaration.Nullable),
            44 => SystemDeclaration(DotNetN10SystemDeclaration.RhpNewFastRuntime, DotNetN10SystemDeclarationCategory.RuntimeExportInfrastructure, DotNetN10HelperSupportState.NarrowlyImplemented),
            45 => StubbedAttribute(DotNetN10SystemDeclaration.DefaultMemberAttribute),
            46 => StubbedAttribute(DotNetN10SystemDeclaration.IndexerNameAttribute),
            47 => SystemDeclaration(DotNetN10SystemDeclaration.RuntimeImports, DotNetN10SystemDeclarationCategory.RuntimeExportInfrastructure, DotNetN10HelperSupportState.NarrowlyImplemented),
            // M04_SIMPLE_THROW_CATCH_PROBE supplies only an empty compiler identity;
            // messages, stack traces, nested exceptions and general exception services are unsupported.
            48 => StubbedCore(DotNetN10SystemDeclaration.Exception),
            // Compiler modreq identity only; no marshalling constants or API.
            49 => SystemDeclaration(DotNetN10SystemDeclaration.UnmanagedType, DotNetN10SystemDeclarationCategory.InteropContract, DotNetN10HelperSupportState.Stubbed),
            50 => StubbedAttribute(DotNetN10SystemDeclaration.InAttribute),
            _ => InvalidSystemDeclarationEntry()
        };
    }

    public static bool TryGetRuntimeExport(int index, out DotNetN10RuntimeExportCompatibilityEntry entry)
    {
        if (index < 0 || index >= RuntimeExportCount)
        {
            entry = InvalidRuntimeExportEntry();
            return false;
        }

        entry = GetRuntimeExport(index);
        return entry.IsInitialized;
    }

    public static DotNetN10RuntimeExportCompatibilityEntry GetRuntimeExport(int index)
    {
        return index switch
        {
            0 => RuntimeExport(DotNetN10RuntimeExport.RhpStackProbe, DotNetN10RuntimeExportCategory.StackProbe, DotNetN10RuntimeExportBehavior.AuditedStackProbe, DotNetN10HelperSupportState.NarrowlyImplemented),
            1 => ForbiddenTypeCast(DotNetN10RuntimeExport.RhTypeCast_IsInstanceOfClass),
            2 => ForbiddenTypeCast(DotNetN10RuntimeExport.RhTypeCast_CheckCastClass),
            3 => ForbiddenTypeCast(DotNetN10RuntimeExport.RhTypeCast_CheckCastClassSpecial),
            4 => ForbiddenTypeCast(DotNetN10RuntimeExport.RhTypeCast_IsInstanceOfArray),
            5 => ForbiddenTypeCast(DotNetN10RuntimeExport.RhTypeCast_CheckCastArray),
            6 => ForbiddenTypeCast(DotNetN10RuntimeExport.RhTypeCast_IsInstanceOfInterface),
            7 => ForbiddenTypeCast(DotNetN10RuntimeExport.RhTypeCast_CheckCastInterface),
            8 => ForbiddenTypeCast(DotNetN10RuntimeExport.RhTypeCast_IsInstanceOfAny),
            9 => ForbiddenTypeCast(DotNetN10RuntimeExport.RhTypeCast_CheckCastAny),
            10 => ForbiddenTypeCast(DotNetN10RuntimeExport.RhTypeCast_CheckCastAny_NoCacheLookup),
            11 => ForbiddenTypeCast(DotNetN10RuntimeExport.RhTypeCast_IsInstanceOfException),
            12 => LinkOnlyTransition(DotNetN10RuntimeExport.RhpReversePInvoke),
            13 => LinkOnlyTransition(DotNetN10RuntimeExport.RhpReversePInvokeReturn),
            14 => LinkOnlyTransition(DotNetN10RuntimeExport.RhpPInvoke),
            15 => LinkOnlyTransition(DotNetN10RuntimeExport.RhpPInvokeReturn),
            16 => RuntimeExport(DotNetN10RuntimeExport.RhpFallbackFailFast, DotNetN10RuntimeExportCategory.FailFast, DotNetN10RuntimeExportBehavior.FailFast, DotNetN10HelperSupportState.Forbidden),
            17 => RuntimeExport(DotNetN10RuntimeExport.RhpNewFast, DotNetN10RuntimeExportCategory.FixedAllocation, DotNetN10RuntimeExportBehavior.ProfileBoundFixedAllocation, DotNetN10HelperSupportState.NarrowlyImplemented),
            18 => RuntimeExport(DotNetN10RuntimeExport.RhpNewArrayFast, DotNetN10RuntimeExportCategory.SzArrayAllocation, DotNetN10RuntimeExportBehavior.ProfileBoundSzArrayAllocation, DotNetN10HelperSupportState.NarrowlyImplemented),
            19 => RuntimeExport(DotNetN10RuntimeExport.RhpNewPtrArrayFast, DotNetN10RuntimeExportCategory.SzArrayAllocation, DotNetN10RuntimeExportBehavior.ProfileBoundSzArrayAllocation, DotNetN10HelperSupportState.NarrowlyImplemented),
            20 => RuntimeExport(DotNetN10RuntimeExport.RhNewString, DotNetN10RuntimeExportCategory.StringAllocation, DotNetN10RuntimeExportBehavior.ProfileBoundStringAllocation, DotNetN10HelperSupportState.NarrowlyImplemented),
            _ => InvalidRuntimeExportEntry()
        };
    }

    private static DotNetN10SystemDeclarationCompatibilityEntry StubbedCore(DotNetN10SystemDeclaration declaration)
    {
        return SystemDeclaration(declaration, DotNetN10SystemDeclarationCategory.CoreType, DotNetN10HelperSupportState.Stubbed);
    }

    private static DotNetN10SystemDeclarationCompatibilityEntry StubbedPrimitive(DotNetN10SystemDeclaration declaration)
    {
        return SystemDeclaration(declaration, DotNetN10SystemDeclarationCategory.PrimitiveType, DotNetN10HelperSupportState.Stubbed);
    }

    private static DotNetN10SystemDeclarationCompatibilityEntry StubbedAttribute(DotNetN10SystemDeclaration declaration)
    {
        return SystemDeclaration(declaration, DotNetN10SystemDeclarationCategory.AttributeContract, DotNetN10HelperSupportState.Stubbed);
    }

    private static DotNetN10SystemDeclarationCompatibilityEntry StubbedRuntimeHandle(DotNetN10SystemDeclaration declaration)
    {
        return SystemDeclaration(declaration, DotNetN10SystemDeclarationCategory.RuntimeHandle, DotNetN10HelperSupportState.Stubbed);
    }

    private static DotNetN10SystemDeclarationCompatibilityEntry NarrowCompilerServices(DotNetN10SystemDeclaration declaration)
    {
        return SystemDeclaration(declaration, DotNetN10SystemDeclarationCategory.CompilerServices, DotNetN10HelperSupportState.NarrowlyImplemented);
    }

    private static DotNetN10SystemDeclarationCompatibilityEntry SystemDeclaration(DotNetN10SystemDeclaration declaration, DotNetN10SystemDeclarationCategory category, DotNetN10HelperSupportState supportState)
    {
        return new DotNetN10SystemDeclarationCompatibilityEntry(declaration, category, supportState);
    }

    private static DotNetN10SystemDeclarationCompatibilityEntry InvalidSystemDeclarationEntry()
    {
        return new DotNetN10SystemDeclarationCompatibilityEntry(DotNetN10SystemDeclaration.Unknown, DotNetN10SystemDeclarationCategory.Unknown, DotNetN10HelperSupportState.Unknown);
    }

    private static DotNetN10RuntimeExportCompatibilityEntry ForbiddenTypeCast(DotNetN10RuntimeExport runtimeExport)
    {
        return RuntimeExport(runtimeExport, DotNetN10RuntimeExportCategory.TypeCast, DotNetN10RuntimeExportBehavior.FailFast, DotNetN10HelperSupportState.Forbidden);
    }

    private static DotNetN10RuntimeExportCompatibilityEntry LinkOnlyTransition(DotNetN10RuntimeExport runtimeExport)
    {
        return RuntimeExport(runtimeExport, DotNetN10RuntimeExportCategory.NativeTransition, DotNetN10RuntimeExportBehavior.LinkOnlyNoOp, DotNetN10HelperSupportState.Stubbed);
    }

    private static DotNetN10RuntimeExportCompatibilityEntry RuntimeExport(DotNetN10RuntimeExport runtimeExport, DotNetN10RuntimeExportCategory category, DotNetN10RuntimeExportBehavior behavior, DotNetN10HelperSupportState supportState)
    {
        return new DotNetN10RuntimeExportCompatibilityEntry(runtimeExport, category, behavior, supportState);
    }

    private static DotNetN10RuntimeExportCompatibilityEntry InvalidRuntimeExportEntry()
    {
        return new DotNetN10RuntimeExportCompatibilityEntry(DotNetN10RuntimeExport.Unknown, DotNetN10RuntimeExportCategory.Unknown, DotNetN10RuntimeExportBehavior.Unknown, DotNetN10HelperSupportState.Unknown);
    }
}
