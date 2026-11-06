namespace OS.Kernel.Memory;
/// <summary>
/// Identifies the single leaf mutation requested from a caller-owned x64
/// page-table image. Values are append-only.
/// </summary>

public enum X64PageMapOperation : byte
{
    Uninitialized = 0,
    Map = 1,
    Unmap = 2,
    Protect = 3
}
