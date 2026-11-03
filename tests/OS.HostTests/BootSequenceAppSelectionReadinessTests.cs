using System.Runtime.CompilerServices;
using System.Text;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Manifest;
using OS.Std.Binary;
using OS.Storage;

internal static partial class BootSequenceAppSelectionReadinessTests
{
    private const string AppPath = "apps/hello.bin";
    private const ushort FileCount = 1;
    private const int AppContentLength = 40;
    [HostTest("boot.sequence app selection ready", HostLane.Fast, 0)]
    private static bool TestAppSelectionReady()
    {
        AppSelectionInputs inputs = CreateReadyInputs();
        SequenceTestSerialSink serial = new();
        BootEventRingBuffer events = new(8);
        BootResult result = BootSequence.RunAppSelectionReadiness(inputs.ManifestLoad, inputs.PackageParse, inputs.LaunchPlan, inputs.Descriptor, serial, events);
        return HostCheck.Result(result.Succeeded && serial.Count == 1 && serial.LineAt(0) == BootSequence.AppSelectionReady && events.Snapshot()[0] == BootSequence.AppSelectionReady);
    }

    [HostTest("boot.sequence app selection rejects manifest failure", HostLane.Fast, 1)]
    private static bool TestRejectsManifestFailure()
    {
        AppSelectionInputs inputs = CreateReadyInputs();
        NativeManifestLoadResult manifestLoad = NativeManifestLoadResult.ManifestFail(NativeManifestParseStatus.UnknownKey, 64);
        SequenceTestSerialSink serial = new();
        BootEventRingBuffer events = new(8);
        BootResult result = BootSequence.RunAppSelectionReadiness(manifestLoad, inputs.PackageParse, inputs.LaunchPlan, inputs.Descriptor, serial, events);
        string expectedMarker = BootSequence.CreateAppSelectionNotReadyMarker(AppSelectionReadinessStatus.ManifestFailed, NativeManifestLoadStatus.ManifestParseFailed, OsPackageStatus.Success, NativeAppLaunchStatus.Success, NativeAppLaunchDescriptorStatus.Success, OsPackageFileSystemStatus.Success, OsPackageStatus.Success);
        return HostCheck.Result(!result.Succeeded && result.Reason.Contains(nameof(AppSelectionReadinessStatus.ManifestFailed), StringComparison.Ordinal) && serial.Count == 1 && serial.LineAt(0) == expectedMarker && events.Snapshot()[0] == expectedMarker);
    }

    [HostTest("boot.sequence app selection rejects package failure", HostLane.Fast, 2)]
    private static bool TestRejectsPackageFailure()
    {
        AppSelectionInputs inputs = CreateReadyInputs();
        SequenceTestSerialSink serial = new();
        BootEventRingBuffer events = new(8);
        BootResult result = BootSequence.RunAppSelectionReadiness(inputs.ManifestLoad, OsPackageParseResult.Fail(OsPackageStatus.InvalidMagic), inputs.LaunchPlan, inputs.Descriptor, serial, events);
        string expectedMarker = BootSequence.CreateAppSelectionNotReadyMarker(AppSelectionReadinessStatus.PackageFailed, NativeManifestLoadStatus.Success, OsPackageStatus.InvalidMagic, NativeAppLaunchStatus.Success, NativeAppLaunchDescriptorStatus.Success, OsPackageFileSystemStatus.Success, OsPackageStatus.Success);
        return HostCheck.Result(!result.Succeeded && result.Reason.Contains(nameof(AppSelectionReadinessStatus.PackageFailed), StringComparison.Ordinal) && serial.Count == 1 && serial.LineAt(0) == expectedMarker && events.Snapshot()[0] == expectedMarker);
    }

    [HostTest("boot.sequence app selection rejects descriptor failure", HostLane.Fast, 3)]
    private static bool TestRejectsDescriptorFailure()
    {
        byte[] manifestSource = ValidManifest(AppPath);
        NativeManifestLoadResult manifestLoad = CreateManifestLoad(manifestSource);
        byte[] package = CreatePackage("apps/other.bin");
        NativeAppLaunchPlanResult launchPlan = CreateLaunchPlan(manifestSource);
        NativeAppLaunchDescriptorResult descriptor = NativeAppLaunchDescriptorBuilder.Create(manifestSource, package, launchPlan);
        SequenceTestSerialSink serial = new();
        BootEventRingBuffer events = new(8);
        BootResult result = BootSequence.RunAppSelectionReadiness(manifestLoad, OsPackageParser.Parse(package), launchPlan, descriptor, serial, events);
        string expectedMarker = BootSequence.CreateAppSelectionNotReadyMarker(AppSelectionReadinessStatus.LaunchDescriptorFailed, NativeManifestLoadStatus.Success, OsPackageStatus.Success, NativeAppLaunchStatus.Success, NativeAppLaunchDescriptorStatus.PackageFileMissing, OsPackageFileSystemStatus.MissingFile, OsPackageStatus.MissingFile);
        return HostCheck.Result(!descriptor.Succeeded && !result.Succeeded && result.Reason.Contains(nameof(AppSelectionReadinessStatus.LaunchDescriptorFailed), StringComparison.Ordinal) && serial.Count == 1 && serial.LineAt(0) == expectedMarker && events.Snapshot()[0] == expectedMarker);
    }

    private static AppSelectionInputs CreateReadyInputs()
    {
        byte[] manifestSource = ValidManifest(AppPath);
        byte[] package = CreatePackage(AppPath);
        NativeAppLaunchPlanResult launchPlan = CreateLaunchPlan(manifestSource);
        NativeAppLaunchDescriptorResult descriptor = NativeAppLaunchDescriptorBuilder.Create(manifestSource, package, launchPlan);
        if (!descriptor.Succeeded)
        {
            throw new InvalidOperationException("Test descriptor failed: " + descriptor.Status);
        }

        return new AppSelectionInputs(CreateManifestLoad(manifestSource), OsPackageParser.Parse(package), launchPlan, descriptor);
    }

    private static NativeManifestLoadResult CreateManifestLoad(byte[] manifestSource)
    {
        NativeManifestParseResult parsed = NativeManifestParser.Parse(manifestSource);
        if (!parsed.Succeeded)
        {
            throw new InvalidOperationException("Test manifest did not parse: " + parsed.Status);
        }

        return NativeManifestLoadResult.Success(parsed.Manifest, (uint)manifestSource.Length);
    }

    private static NativeAppLaunchPlanResult CreateLaunchPlan(byte[] manifestSource)
    {
        NativeManifestParseResult parsed = NativeManifestParser.Parse(manifestSource);
        if (!parsed.Succeeded)
        {
            throw new InvalidOperationException("Test manifest did not parse: " + parsed.Status);
        }

        return NativeAppLaunchPlan.Create(manifestSource, parsed.Manifest, BootInfo.CreateHost(BootCapabilities.TextOutput), NativeAppLaunchRuntimeCapabilities.PackageStorage | NativeAppLaunchRuntimeCapabilities.Network);
    }

    private static byte[] ValidManifest(string appPath)
    {
        return Encoding.ASCII.GetBytes("format=os-native-manifest\n" + "version=0\n" + "app.name=hello.app\n" + "app.kind=package\n" + "app.path=" + appPath + "\n" + "runtime.kind=native-aot\n" + "args=--port=8080\n" + "network=dhcp\n" + "log=serial\n");
    }

    private static byte[] CreatePackage(string appPath)
    {
        const int tableOffset = OsPackageParser.HeaderSize;
        const int tableLength = FileCount * OsPackageParser.FileTableEntrySize;
        int appPathOffset = tableOffset + tableLength;
        int appContentOffset = appPathOffset + appPath.Length;
        int packageLength = appContentOffset + AppContentLength;
        byte[] package = new byte[packageLength];
        WriteMagic(package);
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
        WriteGeneratedContent(package, appContentOffset);
        return package;
    }

    private static void WriteMagic(byte[] package)
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

    private static void WriteEntry(byte[] package, int offset, int pathOffset, int pathLength, int contentOffset, int contentLength)
    {
        WriteUInt32(package, offset, (uint)pathOffset);
        WriteUInt16(package, offset + 4, (ushort)pathLength);
        WriteUInt16(package, offset + 6, 0);
        WriteUInt64(package, offset + 8, (ulong)contentOffset);
        WriteUInt64(package, offset + 16, (ulong)contentLength);
        WriteUInt64(package, offset + 24, 0);
    }

    private static void WriteAscii(byte[] package, int offset, string value)
    {
        for (int index = 0; index < value.Length; index++)
        {
            package[offset + index] = (byte)value[index];
        }
    }

    private static void WriteGeneratedContent(byte[] package, int offset)
    {
        WritePayloadHeader(package, offset, NativeAppPayloadHeader.HeaderSize);
        for (int index = NativeAppPayloadHeader.HeaderSize; index < AppContentLength; index++)
        {
            package[offset + index] = (byte)(0x61 + index);
        }
    }

    private static void WritePayloadHeader(byte[] package, int offset, ulong entryOffset)
    {
        package[offset] = (byte)'O';
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
        WriteUInt64(package, offset + 16, entryOffset);
        WriteUInt64(package, offset + 24, 0);
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
