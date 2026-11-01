namespace OS.Kernel.Memory;
/// <summary>
/// Classifies the modeled terminal outcome. A modeled return remains host
/// transcript evidence and is never live guest evidence.
/// </summary>

public enum X64ProtectionProbeTerminalKind : byte
{
    Uninitialized = 0,
    ModeledReturn = 1,
    Timeout = 2,
    Reset = 3,
    Fault = 4,
    UnexpectedReturnValue = 5,
    CanaryMismatch = 6
}
