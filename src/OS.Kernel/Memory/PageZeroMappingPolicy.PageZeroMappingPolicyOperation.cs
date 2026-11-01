namespace OS.Kernel.Memory;
/// <summary>
/// Identifies which page-zero policy boundary produced a result. Values are
/// append-only.
/// </summary>

public enum PageZeroMappingPolicyOperation : byte
{
    Uninitialized = 0,
    MappingIntent = 1,
    TableImageAudit = 2
}
