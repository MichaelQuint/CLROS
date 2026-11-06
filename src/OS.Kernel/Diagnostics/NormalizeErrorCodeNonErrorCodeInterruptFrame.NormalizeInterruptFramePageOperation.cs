namespace OS.Kernel.Diagnostics;

public enum NormalizeInterruptFramePageOperation : byte
{
    None = 0,
    Materialize = 1,
    ValidateReadBack = 2
}
