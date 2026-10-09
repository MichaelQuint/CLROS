using System.Runtime.CompilerServices;
using System.Text;
using OS.Boot;
using OS.Manifest;
using OS.Std.Binary;
using OS.Storage;

internal static class NativeAppLaunchDescriptorTests
{
    private const string AppPath = "apps/hello.bin";
    private const ushort FileCount = 1;
    private const int AppContentLength = 40;

    [HostTest("appmodel launch descriptor rejects invalid payload header", HostLane.Fast, 1)]
    private static bool TestRejectsInvalidPayloadHeader()
    {
        byte[] manifestSource = ValidManifest(AppPath);
        NativeAppLaunchPlanResult launchPlan = CreateLaunchPlan(manifestSource);
        byte[] package = CreatePackage(AppPath, validPayloadHeader: false);

        NativeAppLaunchDescriptorResult descriptor =
            NativeAppLaunchDescriptorBuilder.Create(manifestSource, package, launchPlan);

        return HostCheck.Result(!descriptor.Succeeded
            && descriptor.IsInitialized
            && descriptor.Status == NativeAppLaunchDescriptorStatus.InvalidPayloadHeader
            && descriptor.LaunchStatus == NativeAppLaunchStatus.Success
            && descriptor.PackageFileStatus == OsPackageFileSystemStatus.Success
            && descriptor.PackageStatus == OsPackageStatus.Success
            && descriptor.PayloadHeaderStatus == NativeAppPayloadHeaderStatus.InvalidMagic);
    }

    [HostTest("appmodel launch descriptor accepts package payload header", HostLane.Fast, 0)]
    private static bool TestAcceptsPackagePayloadHeader()
    {
        byte[] manifestSource = ValidManifest(AppPath);
        NativeAppLaunchPlanResult launchPlan = CreateLaunchPlan(manifestSource);
        byte[] package = CreatePackage(AppPath, validPayloadHeader: true);

        NativeAppLaunchDescriptorResult descriptor =
            NativeAppLaunchDescriptorBuilder.Create(manifestSource, package, launchPlan);

        return HostCheck.Result(descriptor.Succeeded
            && descriptor.IsInitialized
            && descriptor.Status == NativeAppLaunchDescriptorStatus.Success
            && descriptor.LaunchStatus == NativeAppLaunchStatus.Success
            && descriptor.PackageFileStatus == OsPackageFileSystemStatus.Success
            && descriptor.PackageStatus == OsPackageStatus.Success
            && descriptor.PayloadHeaderStatus == NativeAppPayloadHeaderStatus.Success
            && descriptor.Descriptor.AppBinaryLength == AppContentLength
            && descriptor.Descriptor.ArgumentByteLength == "--port=8080".Length
            && descriptor.Descriptor.PayloadHeader.EntryOffset == NativeAppPayloadHeader.HeaderSize);
    }

    [HostTest("appmodel launch descriptor default result is not success", HostLane.Fast, 2)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        NativeAppLaunchDescriptorResult descriptor = default;

        return HostCheck.Result(!descriptor.IsInitialized
            && !descriptor.Succeeded
            && descriptor.Status == NativeAppLaunchDescriptorStatus.Success
            && descriptor.LaunchStatus == NativeAppLaunchStatus.Success
            && descriptor.PackageFileStatus == OsPackageFileSystemStatus.Success
            && descriptor.PackageStatus == OsPackageStatus.Success
            && descriptor.PayloadHeaderStatus == NativeAppPayloadHeaderStatus.Success
            && !RuntimeHelpers.IsReferenceOrContainsReferences<NativeAppLaunchDescriptorResult>());
    }

    private static NativeAppLaunchPlanResult CreateLaunchPlan(byte[] manifestSource)
    {
        NativeManifestParseResult parsed = NativeManifestParser.Parse(manifestSource);
        if (!parsed.Succeeded)
        {
            throw new InvalidOperationException("Test manifest did not parse: " + parsed.Status);
        }

        return NativeAppLaunchPlan.Create(
            manifestSource,
            parsed.Manifest,
            BootInfo.CreateHost(BootCapabilities.TextOutput),
            NativeAppLaunchRuntimeCapabilities.PackageStorage | NativeAppLaunchRuntimeCapabilities.Network);
    }

    private static byte[] ValidManifest(string appPath)
    {
        return Encoding.ASCII.GetBytes(
            "format=os-native-manifest\n" +
            "version=0\n" +
            "app.name=hello.app\n" +
            "app.path=" + appPath + "\n" +
            "args=--port=8080\n" +
            "network=dhcp\n" +
            "log=serial\n");
    }

    private static byte[] CreatePackage(string appPath, bool validPayloadHeader)
    {
        const int tableOffset = OsPackageParser.HeaderSize;
        const int tableLength = FileCount * OsPackageParser.FileTableEntrySize;
        int appPathOffset = tableOffset + tableLength;
        int appContentOffset = appPathOffset + appPath.Length;
        int packageLength = appContentOffset + AppContentLength;

        byte[] package = new byte[packageLength];
        WritePackageMagic(package);
        WriteUInt16(package, 8, OsPackageParser.SupportedVersion);
        WriteUInt16(package, 10, OsPackageParser.HeaderSize);
        WriteUInt16(package, 12, FileCount);
        WriteUInt16(package, 14, OsPackageParser.FileTableEntrySize);
        WriteUInt32(package, 16, tableOffset);
        WriteUInt32(package, 20, tableLength);
        WriteUInt32(package, 24, 0);
        WriteUInt32(package, 28, 0);
        WriteEntry(package, tableOffset, appPathOffset, appPath.Length, appContentOffset, AppContentLength);
        WriteAscii(package, appPathOffset, appPath);
        WritePayloadHeader(package, appContentOffset, validPayloadHeader);
        return package;
    }

    private static void WritePackageMagic(byte[] package)
    {
        package[0] = (byte)'O';
        package[1] = (byte)'S';
        package[2] = (byte)'P';
        package[3] = (byte)'K';
        package[4] = (byte)'G';
        package[5] = (byte)'v';
        package[6] = (byte)'0';
        package[7] = 0;
    }

    private static void WriteEntry(
        byte[] package,
        int offset,
        int pathOffset,
        int pathLength,
        int contentOffset,
        int contentLength)
    {
        WriteUInt32(package, offset, pathOffset);
        WriteUInt16(package, offset + 4, (ushort)pathLength);
        WriteUInt16(package, offset + 6, 0);
        WriteUInt64(package, offset + 8, contentOffset);
        WriteUInt64(package, offset + 16, contentLength);
        WriteUInt64(package, offset + 24, 0);
    }

    private static void WritePayloadHeader(byte[] package, int offset, bool validMagic)
    {
        package[offset] = validMagic ? (byte)'O' : (byte)'X';
        package[offset + 1] = (byte)'S';
        package[offset + 2] = (byte)'A';
        package[offset + 3] = (byte)'P';
        package[offset + 4] = (byte)'P';
        package[offset + 5] = (byte)'v';
        package[offset + 6] = (byte)'0';
        package[offset + 7] = 0;
        WriteUInt16(package, offset + 8, NativeAppPayloadHeader.SupportedVersion);
        WriteUInt16(package, offset + 10, NativeAppPayloadHeader.HeaderSize);
        WriteUInt32(package, offset + 12, 0);
        WriteUInt64(package, offset + 16, NativeAppPayloadHeader.HeaderSize);
        WriteUInt64(package, offset + 24, 0);
        package[offset + NativeAppPayloadHeader.HeaderSize] = 0xC3;
    }

    private static void WriteAscii(byte[] package, int offset, string value)
    {
        for (int index = 0; index < value.Length; index++)
        {
            package[offset + index] = (byte)value[index];
        }
    }

    private static void WriteUInt16(byte[] package, int offset, ushort value)
    {
        EndianPrimitives.WriteUInt16LittleEndian(package.AsSpan(offset, 2), value);
    }

    private static void WriteUInt32(byte[] package, int offset, uint value)
    {
        EndianPrimitives.WriteUInt32LittleEndian(package.AsSpan(offset, 4), value);
    }

    private static void WriteUInt32(byte[] package, int offset, int value)
    {
        WriteUInt32(package, offset, (uint)value);
    }

    private static void WriteUInt64(byte[] package, int offset, ulong value)
    {
        EndianPrimitives.WriteUInt64LittleEndian(package.AsSpan(offset, 8), value);
    }

    private static void WriteUInt64(byte[] package, int offset, int value)
    {
        WriteUInt64(package, offset, (ulong)value);
    }
}
