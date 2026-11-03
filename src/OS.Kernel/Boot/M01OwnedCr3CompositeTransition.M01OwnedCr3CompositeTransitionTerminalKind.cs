namespace OS.Boot;

public enum M01OwnedCr3CompositeTransitionTerminalKind : byte
{
    Uninitialized = 0,
    Returned = 1,
    Timeout = 2,
    Reset = 3,
    TripleFault = 4
}
