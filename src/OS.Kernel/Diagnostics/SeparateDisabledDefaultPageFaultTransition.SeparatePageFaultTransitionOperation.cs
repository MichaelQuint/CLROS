namespace OS.Kernel.Diagnostics;

public enum SeparatePageFaultTransitionOperation : byte
{
    None,
    PrepareContext,
    EmitStream,
    ValidateStreamReadBack
}
