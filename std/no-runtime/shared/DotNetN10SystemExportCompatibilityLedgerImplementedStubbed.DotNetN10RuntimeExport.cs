namespace OS.Std.NoRuntime;
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
    RhpNewPtrArrayFast = 20,
    RhNewString = 21
}
