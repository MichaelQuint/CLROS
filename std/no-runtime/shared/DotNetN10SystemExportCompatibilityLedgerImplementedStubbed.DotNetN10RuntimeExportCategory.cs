namespace OS.Std.NoRuntime;

public enum DotNetN10RuntimeExportCategory : byte
{
    Unknown = 0,
    StackProbe = 1,
    TypeCast = 2,
    NativeTransition = 3,
    FailFast = 4,
    FixedAllocation = 5,
    SzArrayAllocation = 6,
    StringAllocation = 7
}
