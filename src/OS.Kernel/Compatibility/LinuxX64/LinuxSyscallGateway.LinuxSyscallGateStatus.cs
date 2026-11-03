namespace OS.Kernel.Compatibility.LinuxX64;

public enum LinuxSyscallGateStatus : byte
{
    Uninitialized,
    Active,
    Exited,
    InvalidAdmission,
    AlreadyConsumed,
    InvalidReturn,
    CallLimit,
    CopyFailed,
    SinkFailed,
    Blocked
}
