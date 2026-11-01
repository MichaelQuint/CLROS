namespace OS.Kernel.Memory;
/// <summary>
/// Identifies why CLROS owns a physical boot-time range.
/// Existing values are an append-only ABI and must never be renumbered or reused.
/// </summary>

public enum BootReservationKind : byte
{
    Uninitialized = 0,
    LoadedImage = 1,
    BootStackSwitchStub = 2,
    BootStackGuard = 3,
    BootStack = 4,
    OwnedMemoryMap = 5,
    AcpiSnapshot = 6,
    AcpiScratch = 7,
    DiagnosticState = 8,
    AllocatorArena = 9,
    PageTableArena = 10,
    Cr3TransitionStub = 11,
    BootHandoff = 12,
    FramebufferMetadata = 13,
    PageProtectionProbe = 14,
    LinuxElfPackage = 15,
    LinuxElfBacking = 16,
    LinuxElfUserTables = 17,
    LinuxSyscallCode = 18,
    LinuxSyscallData = 19
}
