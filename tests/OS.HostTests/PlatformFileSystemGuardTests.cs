using System.Runtime.CompilerServices;
using OS.Kernel.Platform;

internal static class PlatformFileSystemGuardTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("platform.filesystem allows read before UEFI gone", TestReadBeforeUefiGone, ref failures);
        Run(
            "platform.filesystem rejects read in UEFI-gone simulation",
            TestReadRejectedInUefiGoneSimulation,
            ref failures);
        Run(
            "platform.filesystem rejects read after ExitBootServices",
            TestReadRejectedAfterExitBootServices,
            ref failures);
        Run("platform.filesystem rejects unsafe paths", TestRejectsUnsafePaths, ref failures);
        Run(
            "platform.filesystem reports destination capacity",
            TestReportsDestinationCapacity,
            ref failures);
        Run(
            "platform.filesystem result contains no managed references",
            TestResultContainsNoManagedReferences,
            ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Platform filesystem guard host tests failed: " + failures);
        }
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {
        Console.WriteLine("TEST_START " + name);

        try
        {
            if (test())
            {
                Console.WriteLine("TEST_PASS " + name);
                return;
            }

            failures++;
            Console.WriteLine("TEST_FAIL " + name);
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static bool TestReadBeforeUefiGone()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        FileSystemSourceDouble source = new FileSystemSourceDouble("EFI/BOOT/BOOTX64.EFI"u8);
        IPlatformFileSystem fileSystem = new GuardedPlatformFileSystem(firmware, source);
        Span<byte> destination = stackalloc byte[4];

        PlatformFileSystemReadResult result = fileSystem.ReadFile(
            "EFI/BOOT/BOOTX64.EFI"u8,
            destination);

        return result.Succeeded
            && result.Status == PlatformFileSystemStatus.Success
            && result.Mode == FirmwareServicesMode.BootServicesAvailable
            && result.Operation == PlatformFileSystemOperations.ReadFile
            && result.PathLength == "EFI/BOOT/BOOTX64.EFI"u8.Length
            && result.BytesRead == 4
            && result.RequiredBytes == 4
            && result.AvailableBytes == 4
            && destination[0] == (byte)'O'
            && destination[1] == (byte)'S'
            && destination[2] == (byte)'F'
            && destination[3] == (byte)'S'
            && source.ReadCount == 1;
    }

    private static bool TestReadRejectedInUefiGoneSimulation()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        _ = firmware.EnterUefiGoneSimulation();
        FileSystemSourceDouble source = new FileSystemSourceDouble("EFI/BOOT/BOOTX64.EFI"u8);
        IPlatformFileSystem fileSystem = new GuardedPlatformFileSystem(firmware, source);
        Span<byte> destination = stackalloc byte[4];

        PlatformFileSystemReadResult result = fileSystem.ReadFile(
            "EFI/BOOT/BOOTX64.EFI"u8,
            destination);

        return !result.Succeeded
            && result.Status == PlatformFileSystemStatus.FirmwareServicesUnavailable
            && result.Mode == FirmwareServicesMode.UefiGoneSimulated
            && result.Operation == PlatformFileSystemOperations.ReadFile
            && result.BytesRead == 0
            && source.ReadCount == 0;
    }

    private static bool TestReadRejectedAfterExitBootServices()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        _ = firmware.MarkBootServicesExited();
        FileSystemSourceDouble source = new FileSystemSourceDouble("EFI/BOOT/BOOTX64.EFI"u8);
        IPlatformFileSystem fileSystem = new GuardedPlatformFileSystem(firmware, source);
        Span<byte> destination = stackalloc byte[4];

        PlatformFileSystemReadResult result = fileSystem.ReadFile(
            "EFI/BOOT/BOOTX64.EFI"u8,
            destination);

        return !result.Succeeded
            && result.Status == PlatformFileSystemStatus.FirmwareServicesUnavailable
            && result.Mode == FirmwareServicesMode.BootServicesExited
            && result.Operation == PlatformFileSystemOperations.ReadFile
            && result.BytesRead == 0
            && source.ReadCount == 0;
    }

    private static bool TestRejectsUnsafePaths()
    {
        byte[][] unsafePaths =
        [
            [],
            "/EFI/BOOT/BOOTX64.EFI"u8.ToArray(),
            "\\EFI\\BOOT\\BOOTX64.EFI"u8.ToArray(),
            "C:EFI/BOOT/BOOTX64.EFI"u8.ToArray(),
            "EFI//BOOT/BOOTX64.EFI"u8.ToArray(),
            "EFI/../BOOTX64.EFI"u8.ToArray(),
            "EFI/./BOOTX64.EFI"u8.ToArray(),
            "EFI/BOOT/BOOTX64.EFI "u8.ToArray(),
            "EFI/BOOT/"u8.ToArray(),
            "EFI/BOOT/BOOTX64.EFI\n"u8.ToArray(),
        ];

        for (int index = 0; index < unsafePaths.Length; index++)
        {
            FirmwareServicesGuard firmware = new FirmwareServicesGuard();
            FileSystemSourceDouble source = new FileSystemSourceDouble("EFI/BOOT/BOOTX64.EFI"u8);
            IPlatformFileSystem fileSystem = new GuardedPlatformFileSystem(firmware, source);
            byte[] destination = new byte[4];

            PlatformFileSystemReadResult result = fileSystem.ReadFile(
                unsafePaths[index],
                destination);

            if (result.Succeeded
                || result.Status != PlatformFileSystemStatus.InvalidPath
                || result.Mode != FirmwareServicesMode.BootServicesAvailable
                || source.ReadCount != 0)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TestReportsDestinationCapacity()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        FileSystemSourceDouble source = new FileSystemSourceDouble("EFI/BOOT/BOOTX64.EFI"u8);
        IPlatformFileSystem fileSystem = new GuardedPlatformFileSystem(firmware, source);
        Span<byte> destination = stackalloc byte[2];

        PlatformFileSystemReadResult result = fileSystem.ReadFile(
            "EFI/BOOT/BOOTX64.EFI"u8,
            destination);

        return !result.Succeeded
            && result.Status == PlatformFileSystemStatus.DestinationTooSmall
            && result.Mode == FirmwareServicesMode.BootServicesAvailable
            && result.BytesRead == 0
            && result.RequiredBytes == 4
            && result.AvailableBytes == 2
            && source.ReadCount == 1;
    }

    private static bool TestResultContainsNoManagedReferences()
    {
        return !RuntimeHelpers.IsReferenceOrContainsReferences<PlatformFileSystemReadResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<PlatformFileSystemSourceReadResult>();
    }

    private sealed class FileSystemSourceDouble : IPlatformFileSystemSource
    {
        private static readonly byte[] Payload = [(byte)'O', (byte)'S', (byte)'F', (byte)'S'];
        private readonly byte[] _expectedPath;

        public FileSystemSourceDouble(ReadOnlySpan<byte> expectedPath)
        {
            _expectedPath = expectedPath.ToArray();
        }

        public int ReadCount { get; private set; }

        public PlatformFileSystemSourceReadResult ReadFile(
            ReadOnlySpan<byte> relativePath,
            Span<byte> destination)
        {
            ReadCount++;

            if (!relativePath.SequenceEqual(_expectedPath))
            {
                return PlatformFileSystemSourceReadResult.Missing();
            }

            if (destination.Length < Payload.Length)
            {
                return PlatformFileSystemSourceReadResult.DestinationTooSmall(
                    Payload.Length,
                    destination.Length);
            }

            Payload.CopyTo(destination);
            return PlatformFileSystemSourceReadResult.Read(Payload.Length, destination.Length);
        }
    }
}
