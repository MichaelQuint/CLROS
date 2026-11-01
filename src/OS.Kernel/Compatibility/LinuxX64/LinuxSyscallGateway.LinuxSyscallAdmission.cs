namespace OS.Kernel.Compatibility.LinuxX64;
public unsafe struct LinuxSyscallAdmission
{
    public ulong UserRoot;
    public uint MaximumCalls, MaximumWrite;
    public byte* Scratch;
    public uint ScratchBytes;
    public bool FirmwareExited, OwnedBoundaryValidated;
    public bool EnableStartup;
    public bool EnableDescriptors;
    public bool EnableThreads;
    // Explicit preemptible service policy. MaximumCalls is then a per-hardware-
    // turn quota; only the trusted supervisor may renew it after a new turn.
    public bool ContinuingService;
    // Explicit finite M10-018 full owner; never compatible with quota renewal.
    public bool DiskStaticFull;
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
