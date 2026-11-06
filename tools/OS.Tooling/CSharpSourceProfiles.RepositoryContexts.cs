namespace OS.Tooling;

public static partial class CSharpSourceProfiles
{
    // Exact source family used by M04SingleFinallyCatchEfiIntegrationTests.GuardCore.
    // Its managed fixture defines SINGLE_FINALLY_CORE_TEST, while OS.csproj defines
    // NOSTDLIB_EFI. No other source receives this configuration exclusion.
    private static bool UsesSeparateFinallyEntryModes(string? repositoryPath) => repositoryPath?.Replace('\\', '/') is
        "src/OS/Runtime/ExceptionCatchProbe.cs" or
        "src/OS/Runtime/SimpleCatchBase.cs" or
        "src/OS/Runtime/SimpleCatchDerived.cs" or
        "src/OS/Runtime/ExceptionCatchProbe.RhpNewFastAllocation.cs" or
        "src/OS/Runtime/RhpNewFastAllocation.EhStorage.cs" or
        "src/OS/Runtime/RhpNewFastAllocation.EhTransfer.cs" or
        "src/OS/Runtime/RhpNewFastAllocation.EhControl.cs" or
        "src/OS/Runtime/SingleFinallyAdmission.cs" or
        "src/OS/Runtime/SingleFinallyAdmission.Control.cs";
}
