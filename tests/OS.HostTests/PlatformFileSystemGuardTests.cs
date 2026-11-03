using System.Runtime.CompilerServices;
using OS.Kernel.Platform;

internal static partial class PlatformFileSystemGuardTests
{
    [HostTest("platform.filesystem allows read before UEFI gone", HostLane.Fast, 0)]
    private static bool TestReadBeforeUefiGone()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        FileSystemSourceDouble source = new FileSystemSourceDouble("EFI/BOOT/BOOTX64.EFI"u8);
        IPlatformFileSystem fileSystem = new GuardedPlatformFileSystem(firmware, source);
        Span<byte> destination = stackalloc byte[4];
        PlatformFileSystemReadResult result = fileSystem.ReadFile("EFI/BOOT/BOOTX64.EFI"u8, destination);
        return HostCheck.Result(result.Succeeded && result.Status == PlatformFileSystemStatus.Success && result.Mode == FirmwareServicesMode.BootServicesAvailable && result.Operation == PlatformFileSystemOperations.ReadFile && result.PathLength == "EFI/BOOT/BOOTX64.EFI"u8.Length && result.BytesRead == 4 && result.RequiredBytes == 4 && result.AvailableBytes == 4 && destination[0] == (byte)'O' && destination[1] == (byte)'S' && destination[2] == (byte)'F' && destination[3] == (byte)'S' && source.ReadCount == 1);
    }

    [HostTest("platform.filesystem rejects read in UEFI-gone simulation", HostLane.Fast, 1)]
    private static bool TestReadRejectedInUefiGoneSimulation()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        _ = firmware.EnterUefiGoneSimulation();
        FileSystemSourceDouble source = new FileSystemSourceDouble("EFI/BOOT/BOOTX64.EFI"u8);
        IPlatformFileSystem fileSystem = new GuardedPlatformFileSystem(firmware, source);
        Span<byte> destination = stackalloc byte[4];
        PlatformFileSystemReadResult result = fileSystem.ReadFile("EFI/BOOT/BOOTX64.EFI"u8, destination);
        return HostCheck.Result(!result.Succeeded && result.Status == PlatformFileSystemStatus.FirmwareServicesUnavailable && result.Mode == FirmwareServicesMode.UefiGoneSimulated && result.Operation == PlatformFileSystemOperations.ReadFile && result.BytesRead == 0 && source.ReadCount == 0);
    }

    [HostTest("platform.filesystem rejects read after ExitBootServices", HostLane.Fast, 2)]
    private static bool TestReadRejectedAfterExitBootServices()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        _ = firmware.MarkBootServicesExited();
        FileSystemSourceDouble source = new FileSystemSourceDouble("EFI/BOOT/BOOTX64.EFI"u8);
        IPlatformFileSystem fileSystem = new GuardedPlatformFileSystem(firmware, source);
        Span<byte> destination = stackalloc byte[4];
        PlatformFileSystemReadResult result = fileSystem.ReadFile("EFI/BOOT/BOOTX64.EFI"u8, destination);
        return HostCheck.Result(!result.Succeeded && result.Status == PlatformFileSystemStatus.FirmwareServicesUnavailable && result.Mode == FirmwareServicesMode.BootServicesExited && result.Operation == PlatformFileSystemOperations.ReadFile && result.BytesRead == 0 && source.ReadCount == 0);
    }

    [HostTest("platform.filesystem rejects unsafe paths", HostLane.Fast, 3)]
    private static bool TestRejectsUnsafePaths()
    {
        byte[][] unsafePaths = [[], "/EFI/BOOT/BOOTX64.EFI"u8.ToArray(), "\\EFI\\BOOT\\BOOTX64.EFI"u8.ToArray(), "C:EFI/BOOT/BOOTX64.EFI"u8.ToArray(), "EFI//BOOT/BOOTX64.EFI"u8.ToArray(), "EFI/../BOOTX64.EFI"u8.ToArray(), "EFI/./BOOTX64.EFI"u8.ToArray(), "EFI/BOOT/BOOTX64.EFI "u8.ToArray(), "EFI/BOOT/"u8.ToArray(), "EFI/BOOT/BOOTX64.EFI\n"u8.ToArray(), ];
        for (int index = 0; index < unsafePaths.Length; index++)
        {
            FirmwareServicesGuard firmware = new FirmwareServicesGuard();
            FileSystemSourceDouble source = new FileSystemSourceDouble("EFI/BOOT/BOOTX64.EFI"u8);
            IPlatformFileSystem fileSystem = new GuardedPlatformFileSystem(firmware, source);
            byte[] destination = new byte[4];
            PlatformFileSystemReadResult result = fileSystem.ReadFile(unsafePaths[index], destination);
            if (result.Succeeded || result.Status != PlatformFileSystemStatus.InvalidPath || result.Mode != FirmwareServicesMode.BootServicesAvailable || source.ReadCount != 0)
            {
                return HostCheck.Result(false);
            }
        }

        return HostCheck.Result(true);
    }

    [HostTest("platform.filesystem reports destination capacity", HostLane.Fast, 4)]
    private static bool TestReportsDestinationCapacity()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        FileSystemSourceDouble source = new FileSystemSourceDouble("EFI/BOOT/BOOTX64.EFI"u8);
        IPlatformFileSystem fileSystem = new GuardedPlatformFileSystem(firmware, source);
        Span<byte> destination = stackalloc byte[2];
        PlatformFileSystemReadResult result = fileSystem.ReadFile("EFI/BOOT/BOOTX64.EFI"u8, destination);
        return HostCheck.Result(!result.Succeeded && result.Status == PlatformFileSystemStatus.DestinationTooSmall && result.Mode == FirmwareServicesMode.BootServicesAvailable && result.BytesRead == 0 && result.RequiredBytes == 4 && result.AvailableBytes == 2 && source.ReadCount == 1);
    }

    [HostTest("platform.filesystem result contains no managed references", HostLane.Fast, 5)]
    private static bool TestResultContainsNoManagedReferences()
    {
        return HostCheck.Result(!RuntimeHelpers.IsReferenceOrContainsReferences<PlatformFileSystemReadResult>() && !RuntimeHelpers.IsReferenceOrContainsReferences<PlatformFileSystemSourceReadResult>());
    }
}
