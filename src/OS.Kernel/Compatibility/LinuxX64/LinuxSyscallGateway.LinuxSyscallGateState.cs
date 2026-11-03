namespace OS.Kernel.Compatibility.LinuxX64;

public unsafe struct LinuxSyscallGateState
{
    internal LinuxSyscallGateStatus StatusValue;
    internal LinuxSyscallAdmission Admission;
    internal LinuxSyscallAccess Access;
    internal uint CallsValue, BytesValue;
    internal ulong TotalCallsValue, TotalBytesValue, TurnValue;
    internal LinuxSyscallFrame Pending;
    internal LinuxSyscallGateway.ExitTailContext ExitTail;
    public readonly LinuxSyscallGateStatus Status => StatusValue;
    public readonly uint Calls => CallsValue;
    public readonly uint BytesWritten => BytesValue;
    public readonly ulong TotalCalls => TotalCallsValue;
    public readonly ulong TotalBytesWritten => TotalBytesValue;
    public readonly ulong CurrentTurn => TurnValue;
    /// <summary>Non-owning observation of this generation's staged register-only
    /// self-unmap continuation. It grants no return or execution authority.</summary>

    public readonly bool HasExitTail => ExitTail.Rip != 0;
}
#if !NOSTDLIB_EFI || M16_025_PROBE || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
#endif
