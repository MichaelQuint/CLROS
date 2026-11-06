namespace OS.Kernel.Memory;
/// <summary>
/// Controls whether a protection-only leaf rewrite retains the processor's
/// caller-supplied Accessed and Dirty observations.
/// </summary>

public enum X64HardwareStatePolicy : byte
{
    Uninitialized = 0,
    Clear = 1,
    Preserve = 2
}
