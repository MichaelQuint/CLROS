namespace OS.Kernel.Memory;

/// <summary>
/// Identifies the requested cache policy for a present mapping.
/// A policy is encoded only through an explicitly validated IA32_PAT mapping.
/// </summary>
public enum PageCachePolicy : byte
{
    Uninitialized = 0,
    WriteBack = 1,
    Uncached = 2,
    WriteCombining = 3
}
