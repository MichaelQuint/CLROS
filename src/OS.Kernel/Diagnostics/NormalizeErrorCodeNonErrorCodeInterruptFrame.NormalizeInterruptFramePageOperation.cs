namespace OS.Kernel.Diagnostics;
public enum NormalizeInterruptFramePageOperation : byte
{
    None = 0,
    Materialize = 1,
    ValidateReadBack = 2
}
#if !NOSTDLIB_EFI
#endif
