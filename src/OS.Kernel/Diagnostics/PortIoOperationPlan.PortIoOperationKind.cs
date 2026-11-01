namespace OS.Kernel.Diagnostics;
public enum PortIoOperationKind : byte
{
    None = 0,
    Out8 = 1,
    In8 = 2
}
#if !NOSTDLIB_EFI
#endif
