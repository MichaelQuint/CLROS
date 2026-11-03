namespace OS.Kernel.Diagnostics;

public enum OwnedIdtGateTableOperation : byte
{
    None = 0,
    Materialize = 1,
    ValidateReadBack = 2
}
