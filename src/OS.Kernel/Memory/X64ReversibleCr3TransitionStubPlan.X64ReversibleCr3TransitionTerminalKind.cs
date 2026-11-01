namespace OS.Kernel.Memory;
/// <summary>
/// Host- or runner-supplied terminal classification. The byte plan cannot
/// classify a machine that did not return from privileged execution.
/// </summary>

public enum X64ReversibleCr3TransitionTerminalKind : byte
{
    Uninitialized = 0,
    Returned = 1,
    Timeout = 2,
    Reset = 3,
    TripleFault = 4
}
