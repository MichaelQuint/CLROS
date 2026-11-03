namespace OS.Std.NoRuntime;
public enum DotNetN10RuntimeExportBehavior : byte
{
    Unknown = 0,
    LinkOnlyNoOp = 1,
    FailFast = 2,
    AuditedStackProbe = 3,
    ProfileBoundFixedAllocation = 4,
    ProfileBoundSzArrayAllocation = 5,
    ProfileBoundStringAllocation = 6
}
