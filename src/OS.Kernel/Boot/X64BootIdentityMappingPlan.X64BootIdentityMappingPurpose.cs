namespace OS.Kernel.Boot;
/// <summary>
/// Identifies why one physical range must remain identity mapped during the
/// owned-page-table transition. Values are append-only.
/// </summary>

public enum X64BootIdentityMappingPurpose : byte
{
    Uninitialized = 0,
    ExecutingImage = 1,
    OwnedStack = 2,
    BootHandoff = 3,
    SerialStub = 4,
    AcpiScratch = 5,
    FramebufferMetadata = 6,
    BootStackGuard = 7,
    AdditionalRequired = 8,
    CompositeCodeAndCallGate = 9,
    CompositeOwnedStack = 10,
    ProtectionProbePage = 11,
    CompleteTableAndStateArena = 12,
    RuntimeStateAndCanaryPage = 13,
    OwnedRootPage = 14
}
