namespace OS.Kernel.Memory;
/// <summary>
/// Identifies whether a byte-plan result came from materialization or from
/// caller-requested readback validation.
/// </summary>

public enum X64PagingModePreflightOperation : byte
{
    Uninitialized = 0,
    Materialize = 1,
    Validate = 2
}
