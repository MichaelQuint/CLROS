namespace OS.Kernel.Diagnostics;

public enum DisabledDefaultUd2VectorStatePreTerminalStage : byte
{
    None = 0,
    Services = 1,
    CodeAllocation = 2,
    DataAllocation = 3,
    Materialization = 4,
    ReadBack = 5,
    MarkerTransport = 6,
    Publication = 7
}
#if !NOSTDLIB_EFI
#endif
