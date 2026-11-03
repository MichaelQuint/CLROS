namespace OS.Kernel.Diagnostics;

public enum DisabledDefaultUd2VectorStateTransitionOperation : byte
{
    None = 0,
    PrepareContext = 1,
    EmitStream = 2,
    ValidateStreamReadBack = 3
}
#if !NOSTDLIB_EFI
#endif
