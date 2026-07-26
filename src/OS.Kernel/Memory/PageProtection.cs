namespace OS.Kernel.Memory;

/// <summary>
/// Describes the permissions of a present leaf mapping.
///
/// This is deliberately a plain explicit-valued enum rather than a
/// System.FlagsAttribute enum. Combined values are validated manually so the
/// contract remains usable by the no-stdlib EFI compile closure.
/// </summary>
public enum PageProtection : byte
{
    None = 0,
    Read = 1,
    Write = 2,
    Execute = 4
}
