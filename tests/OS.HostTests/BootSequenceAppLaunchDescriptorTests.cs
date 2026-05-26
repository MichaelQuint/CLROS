using System.Runtime.CompilerServices;
using System.Text;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Manifest;
using OS.Std.Binary;
using OS.Storage;

internal static class BootSequenceAppLaunchDescriptorTests
{
    private const string AppPath = "apps/hello.bin";
    private const ushort FileCount = 1;
    private const byte AppContentSeed = 0x51;
    private const int AppContentLength = 40;

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run(
            "boot.sequence app launch descriptor ready",
            TestLaunchDescriptorReady,
            ref failures);
        Run(
            "boot.sequence app launch descriptor rejects missing binary",
            TestRejectsMissingBinary,
            ref failures);
        Run(
            "boot.sequence app launch descriptor rejects launch-plan failure",
            TestRejectsLaunchPlanFailure,
            ref failures);
        Run(
            "boot.sequence app payload ready",
            TestPayloadReady,
            ref failures);
        Run(
            "boot.sequence app payload rejects descriptor failure",
            TestPayloadRejectsDescriptorFailure,
            ref failures);
        Run(
            "boot.sequence app payload rejects invalid header",
            TestPayloadRejectsInvalidHeader,
            ref failures);
        Run(
            "boot.sequence app payload leaves default markers unchanged",
            TestPayloadLeavesDefaultMarkersUnchanged,
            ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Boot sequence app launch descriptor host tests failed: " + failures);
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

    private static bool TestLaunchDescriptorReady()
    {
        byte[] manifestSource = ValidManifest(AppPath);
        NativeAppLaunchPlanResult launchPlan = CreateLaunchPlan(manifestSource);
        byte[] package = CreatePackage(AppPath);
        NativeAppLaunchDescriptorResult descriptor =
            NativeAppLaunchDescriptorBuilder.Create(manifestSource, package, launchPlan);

        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(8);
        BootResult result = BootSequence.RunAppLaunchDescriptorReadiness(descriptor, serial, events);

        return descriptor.Succeeded
            && descriptor.Descriptor.AppBinaryLength == AppContentLength
            && descriptor.Descriptor.ArgumentByteLength == "--port=8080".Length
            && descriptor.Descriptor.NetworkMode == NativeManifestNetworkMode.Dhcp
            && descriptor.Descriptor.LogMode == NativeManifestLogMode.Serial
            && result.Succeeded
            && serial.Count == 1
            && serial.LineAt(0) == BootSequence.AppLaunchReady
            && events.Snapshot()[0] == BootSequence.AppLaunchReady;
    }

    private static bool TestRejectsMissingBinary()
    {
        byte[] manifestSource = ValidManifest(AppPath);
        NativeAppLaunchPlanResult launchPlan = CreateLaunchPlan(manifestSource);
        byte[] package = CreatePackage("apps/other.bin");
        NativeAppLaunchDescriptorResult descriptor =
            NativeAppLaunchDescriptorBuilder.Create(manifestSource, package, launchPlan);

        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(8);
        BootResult result = BootSequence.RunAppLaunchDescriptorReadiness(descriptor, serial, events);
        string expectedMarker = BootSequence.CreateAppLaunchNotReadyMarker(
            NativeAppLaunchDescriptorStatus.PackageFileMissing,
            NativeAppLaunchStatus.Success,
            OsPackageFileSystemStatus.MissingFile,
            OsPackageStatus.MissingFile);

        return !descriptor.Succeeded
            && descriptor.Status == NativeAppLaunchDescriptorStatus.PackageFileMissing
            && !result.Succeeded
            && result.Reason.Contains(
                nameof(NativeAppLaunchDescriptorStatus.PackageFileMissing),
                StringComparison.Ordinal)
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && events.Snapshot()[0] == expectedMarker;
    }

    private static bool TestRejectsLaunchPlanFailure()
    {
        NativeAppLaunchDescriptorResult descriptor = NativeAppLaunchDescriptorBuilder.Create(
            ValidManifest(AppPath),
            CreatePackage(AppPath),
            NativeAppLaunchPlanResult.Fail(
                NativeAppLaunchStatus.MissingRuntimeCapabilities,
                BootCapabilities.TextOutput,
                BootCapabilities.None,
                BootCapabilities.None,
                NativeAppLaunchRuntimeCapabilities.PackageStorage,
                NativeAppLaunchRuntimeCapabilities.PackageStorage));

        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(8);
        BootResult result = BootSequence.RunAppLaunchDescriptorReadiness(descriptor, serial, events);
        string expectedMarker = BootSequence.CreateAppLaunchNotReadyMarker(
            NativeAppLaunchDescriptorStatus.LaunchPlanFailed,
            NativeAppLaunchStatus.MissingRuntimeCapabilities,
            OsPackageFileSystemStatus.Success,
            OsPackageStatus.Success);

        return !descriptor.Succeeded
            && descriptor.Status == NativeAppLaunchDescriptorStatus.LaunchPlanFailed
            && !result.Succeeded
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && events.Snapshot()[0] == expectedMarker;
    }

    private static bool TestPayloadReady()
    {
        byte[] manifestSource = ValidManifest(AppPath);
        NativeAppLaunchDescriptorResult descriptor = NativeAppLaunchDescriptorBuilder.Create(
            manifestSource,
            CreatePackage(AppPath),
            CreateLaunchPlan(manifestSource));

        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(8);
        BootResult result = BootSequence.RunAppPayloadReadiness(descriptor, serial, events);

        return descriptor.Succeeded
            && descriptor.PayloadHeaderStatus == NativeAppPayloadHeaderStatus.Success
            && result.Succeeded
            && serial.Count == 1
            && serial.LineAt(0) == BootSequence.AppPayloadReady
            && events.Snapshot()[0] == BootSequence.AppPayloadReady;
    }

    private static bool TestPayloadRejectsDescriptorFailure()
    {
        byte[] manifestSource = ValidManifest(AppPath);
        NativeAppLaunchDescriptorResult descriptor = NativeAppLaunchDescriptorBuilder.Create(
            manifestSource,
            CreatePackage("apps/other.bin"),
            CreateLaunchPlan(manifestSource));

        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(8);
        BootResult result = BootSequence.RunAppPayloadReadiness(descriptor, serial, events);
        string expectedMarker = BootSequence.CreateAppPayloadNotReadyMarker(
            NativeAppLaunchDescriptorStatus.PackageFileMissing,
            NativeAppPayloadHeaderStatus.Success);

        return !descriptor.Succeeded
            && descriptor.Status == NativeAppLaunchDescriptorStatus.PackageFileMissing
            && !result.Succeeded
            && result.Reason.Contains(
                nameof(NativeAppLaunchDescriptorStatus.PackageFileMissing),
                StringComparison.Ordinal)
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && events.Snapshot()[0] == expectedMarker;
    }

    private static bool TestPayloadRejectsInvalidHeader()
    {
        byte[] manifestSource = ValidManifest(AppPath);
        NativeAppLaunchDescriptorResult descriptor = NativeAppLaunchDescriptorBuilder.Create(
            manifestSource,
            CreatePackage(AppPath, invalidPayloadHeader: true),
            CreateLaunchPlan(manifestSource));

        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(8);
        BootResult result = BootSequence.RunAppPayloadReadiness(descriptor, serial, events);
        string expectedMarker = BootSequence.CreateAppPayloadNotReadyMarker(
            NativeAppLaunchDescriptorStatus.InvalidPayloadHeader,
            NativeAppPayloadHeaderStatus.InvalidMagic);

        return !descriptor.Succeeded
            && descriptor.Status == NativeAppLaunchDescriptorStatus.InvalidPayloadHeader
            && descriptor.PayloadHeaderStatus == NativeAppPayloadHeaderStatus.InvalidMagic
            && !result.Succeeded
            && result.Reason.Contains(
                nameof(NativeAppPayloadHeaderStatus.InvalidMagic),
                StringComparison.Ordinal)
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && events.Snapshot()[0] == expectedMarker;
    }

    private static bool TestPayloadLeavesDefaultMarkersUnchanged()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);
        BootResult result = BootSequence.Run(serial, events);

        if (!result.Succeeded || serial.Count != BootSequencePlan.LineCount)
        {
            return false;
        }

        for (int lineIndex = 0; lineIndex < BootSequencePlan.LineCount; lineIndex++)
        {
            if (serial.LineAt(lineIndex) != BootSequence.CreatePlanLine(lineIndex))
            {
                return false;
            }
        }

        return !serial.Contains(BootSequence.AppPayloadReady)
            && !serial.ContainsPrefix(BootSequence.AppPayloadNotReadyPrefix);
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

    private static byte[] CreatePackage(string appPath, bool invalidPayloadHeader = false)
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
        WriteEntry(
            package,
            OsPackageParser.HeaderSize,
            appPathOffset,
            appPath.Length,
            appContentOffset,
            AppContentLength);
        WriteAscii(package, appPathOffset, appPath);
        WriteGeneratedContent(package, appContentOffset, invalidPayloadHeader);
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

    private static void WriteEntry(
        byte[] package,
        int offset,
        int pathOffset,
        int pathLength,
        int contentOffset,
        int contentLength)
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

    private static void WriteGeneratedContent(byte[] package, int offset, bool invalidPayloadHeader)
    {
        WritePayloadHeader(package, offset, NativeAppPayloadHeader.HeaderSize);
        if (invalidPayloadHeader)
        {
            package[offset] = (byte)'X';
        }

        for (int index = NativeAppPayloadHeader.HeaderSize; index < AppContentLength; index++)
        {
            package[offset + index] = (byte)(AppContentSeed + (index * 3));
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

    private sealed class SequenceTestSerialSink : ISerialSink
    {
        private readonly List<string> _lines = new List<string>();

        public int Count => _lines.Count;

        public void WriteLine(string line)
        {
            _lines.Add(line);
        }

        public string LineAt(int index)
        {
            return _lines[index];
        }

        public bool Contains(string value)
        {
            return _lines.Contains(value);
        }

        public bool ContainsPrefix(string value)
        {
            for (int index = 0; index < _lines.Count; index++)
            {
                if (_lines[index].StartsWith(value, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
