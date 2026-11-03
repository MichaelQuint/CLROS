namespace OS.Kernel.Diagnostics;
#if !NOSTDLIB_EFI
public enum ProbeRisk
{
    Safe = 0,
    BoundarySimulation = 1,
    DeviceIo = 2,
    Destructive = 3
}
#endif
