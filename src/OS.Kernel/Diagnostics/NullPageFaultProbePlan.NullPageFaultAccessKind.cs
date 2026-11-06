namespace OS.Kernel.Diagnostics;
/// <summary>
/// The future architectural access modeled by the disabled plan.
/// </summary>

public enum NullPageFaultAccessKind : byte
{
    Uninitialized = 0,
    Read = 1
}
