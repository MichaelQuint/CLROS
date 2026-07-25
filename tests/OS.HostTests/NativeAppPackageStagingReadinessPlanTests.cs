using System.Runtime.CompilerServices;
using System.Text;
using OS.Boot;
using OS.Kernel.Runtime;
using OS.Manifest;
using OS.Std.Binary;
using OS.Storage;

internal static class NativeAppPackageStagingReadinessPlanTests
{
    private const string AppName = "hello.app";
    private const string AppPath = "apps/hello.bin";
    private const ulong PayloadOffsetInPackage = 4096;
    private const ulong HeaderLength = NativeAppPayloadHeader.HeaderSize;
    private const ulong ExecutableLength = 64;
    private const ulong PayloadLength = HeaderLength + ExecutableLength;
    private const ulong EntryOffsetInExecutable = 8;

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("appmodel package staging readiness composes", TestComposesReadiness, ref failures);
        Run("appmodel package staging readiness rejects selection failures", TestRejectsSelectionFailures, ref failures);
        Run("appmodel package staging readiness rejects staging failures", TestRejectsStagingFailures, ref failures);
        Run("appmodel package staging readiness rejects memory invocation failures", TestRejectsMemoryInvocationFailures, ref failures);
        Run("appmodel package staging readiness rejects byte mismatches", TestRejectsByteMismatches, ref failures);
        Run("appmodel package staging readiness preserves no-launch boundary", TestPreservesNoLaunchBoundary, ref failures);
        Run("appmodel package staging readiness default result is not success", TestDefaultResultIsNotSuccess, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Native app package staging readiness host tests failed: " + failures);
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

    private static bool TestComposesReadiness()
    {
        ReadinessInputs inputs = CreateInputs();

        NativeAppPackageStagingReadinessResult result =
            NativeAppPackageStagingReadinessPlanner.Compose(
                inputs.ManifestLoad,
                inputs.PackageParse,
                inputs.LaunchPlan,
                inputs.Descriptor,
                inputs.PackageFileStaging,
                inputs.PayloadMapping,
                inputs.PayloadStaging,
                inputs.ExecutableMemory,
                inputs.Invocation);

        return result.Succeeded
            && result.IsInitialized
            && result.Status == NativeAppPackageStagingReadinessStatus.Success
            && result.ManifestStatus == NativeManifestLoadStatus.Success
            && result.PackageStatus == OsPackageStatus.Success
            && result.LaunchStatus == NativeAppLaunchStatus.Success
            && result.DescriptorStatus == NativeAppLaunchDescriptorStatus.Success
            && result.PackageFileStagingStatus == OsPackageFileStagingStatus.Success
            && result.PayloadMappingStatus == NativeAppPayloadMappingPlanStatus.Success
            && result.PayloadStagingStatus == NativeAppPayloadStagingStatus.Success
            && result.ExecutableMemoryStatus == NativeAppExecutableMemoryPlanStatus.Success
            && result.InvocationStatus == NativeAppInvocationPlanStatus.Success
            && result.AppBinaryFileIndex == 0
            && result.PackageFileBytesStaged == PayloadLength
            && result.PayloadOffsetInPackage == PayloadOffsetInPackage
            && result.ExecutableOffsetInPackage == PayloadOffsetInPackage + HeaderLength
            && result.PayloadExecutableBytesStaged == ExecutableLength
            && result.RequiredWritableStagingBytes == 4096
            && result.RequiredStackBytes == NativeAppInvocationPlanner.DefaultStackBytes
            && result.EntryOffsetInPackage == PayloadOffsetInPackage + HeaderLength + EntryOffsetInExecutable;
    }

    private static bool TestRejectsSelectionFailures()
    {
        ReadinessInputs manifestFailed = CreateInputs(
            manifestLoad: NativeManifestLoadResult.ManifestFail(
                NativeManifestParseStatus.MissingAppName,
                bytesRead: 12));

        NativeAppPackageStagingReadinessResult manifestResult =
            Compose(manifestFailed);

        ReadinessInputs packageFailed = CreateInputs(
            packageParse: OsPackageParseResult.Fail(OsPackageStatus.InvalidMagic));
        NativeAppPackageStagingReadinessResult packageResult =
            Compose(packageFailed);

        ReadinessInputs launchFailed = CreateInputs(
            launchPlan: NativeAppLaunchPlanResult.Fail(
                NativeAppLaunchStatus.MissingRuntimeCapabilities,
                BootCapabilities.TextOutput,
                BootCapabilities.None,
                BootCapabilities.None,
                NativeAppLaunchRuntimeCapabilities.PackageStorage,
                NativeAppLaunchRuntimeCapabilities.PackageStorage));
        NativeAppPackageStagingReadinessResult launchResult =
            Compose(launchFailed);

        ReadinessInputs descriptorFailed = CreateInputs(
            descriptor: NativeAppLaunchDescriptorResult.Fail(
                NativeAppLaunchDescriptorStatus.PackageFileMissing,
                NativeAppLaunchStatus.Success,
                OsPackageFileSystemStatus.MissingFile,
                OsPackageStatus.MissingFile));
        NativeAppPackageStagingReadinessResult descriptorResult =
            Compose(descriptorFailed);

        return !manifestResult.Succeeded
            && manifestResult.Status == NativeAppPackageStagingReadinessStatus.ManifestLoadFailed
            && manifestResult.ManifestStatus == NativeManifestLoadStatus.ManifestParseFailed
            && !packageResult.Succeeded
            && packageResult.Status == NativeAppPackageStagingReadinessStatus.PackageParseFailed
            && packageResult.PackageStatus == OsPackageStatus.InvalidMagic
            && !launchResult.Succeeded
            && launchResult.Status == NativeAppPackageStagingReadinessStatus.LaunchPlanFailed
            && launchResult.LaunchStatus == NativeAppLaunchStatus.MissingRuntimeCapabilities
            && !descriptorResult.Succeeded
            && descriptorResult.Status == NativeAppPackageStagingReadinessStatus.LaunchDescriptorFailed
            && descriptorResult.DescriptorStatus == NativeAppLaunchDescriptorStatus.PackageFileMissing;
    }

    private static bool TestRejectsStagingFailures()
    {
        ReadinessInputs packageStagingFailed = CreateInputs(
            packageFileStaging: OsPackageFileStagingResult.Create(
                OsPackageFileStagingStatus.DestinationTooSmall,
                OsPackageFileSystemStatus.Success,
                OsPackageStatus.Success,
                fileIndex: 0,
                packageOffset: PayloadOffsetInPackage,
                fileLength: PayloadLength,
                bytesWritten: 0,
                requiredBytes: PayloadLength,
                copyAttempted: false));
        NativeAppPackageStagingReadinessResult packageStagingResult =
            Compose(packageStagingFailed);

        ReadinessInputs payloadStagingFailed = CreateInputs(
            payloadStaging: NativeAppPayloadStagingResult.Fail(
                NativeAppPayloadStagingStatus.DestinationTooSmall,
                NativeAppPayloadMappingPlanStatus.Success,
                NativeAppLaunchDescriptorStatus.Success,
                NativeAppPayloadHeaderStatus.Success,
                PayloadOffsetInPackage,
                PayloadOffsetInPackage + HeaderLength,
                ExecutableLength,
                ExecutableLength));
        NativeAppPackageStagingReadinessResult payloadStagingResult =
            Compose(payloadStagingFailed);

        return !packageStagingResult.Succeeded
            && packageStagingResult.Status == NativeAppPackageStagingReadinessStatus.PackageFileStagingFailed
            && packageStagingResult.PackageFileStagingStatus == OsPackageFileStagingStatus.DestinationTooSmall
            && !payloadStagingResult.Succeeded
            && payloadStagingResult.Status == NativeAppPackageStagingReadinessStatus.PayloadStagingFailed
            && payloadStagingResult.PayloadStagingStatus == NativeAppPayloadStagingStatus.DestinationTooSmall;
    }

    private static bool TestRejectsMemoryInvocationFailures()
    {
        ReadinessInputs mappingFailed = CreateInputs(
            payloadMapping: NativeAppPayloadMappingPlanResult.Fail(
                NativeAppPayloadMappingPlanStatus.DescriptorFailed,
                NativeAppLaunchDescriptorStatus.InvalidPayloadHeader,
                NativeAppPayloadHeaderStatus.InvalidMagic));
        NativeAppPackageStagingReadinessResult mappingResult =
            Compose(mappingFailed);

        ReadinessInputs memoryFailed = CreateInputs(
            executableMemory: NativeAppExecutableMemoryPlanResult.Fail(
                NativeAppExecutableMemoryPlanStatus.InvalidPageSize,
                NativeAppPayloadMappingPlanStatus.Success,
                NativeAppLaunchDescriptorStatus.Success,
                NativeAppPayloadHeaderStatus.Success,
                2048,
                16,
                NativeAppExecutableMemoryProtection.Read | NativeAppExecutableMemoryProtection.Write,
                NativeAppExecutableMemoryProtection.Read | NativeAppExecutableMemoryProtection.Execute,
                NativeAppExecutableMemoryProtection.Read | NativeAppExecutableMemoryProtection.Execute));
        NativeAppPackageStagingReadinessResult memoryResult =
            Compose(memoryFailed);

        ReadinessInputs invocationFailed = CreateInputs(
            invocation: NativeAppInvocationPlanResult.Fail(
                NativeAppInvocationPlanStatus.InvalidStackSize,
                NativeAppExecutableMemoryPlanStatus.Success,
                NativeAppPayloadMappingPlanStatus.Success,
                NativeAppLaunchDescriptorStatus.Success,
                NativeAppPayloadHeaderStatus.Success,
                NativeAppLaunchStatus.Success));
        NativeAppPackageStagingReadinessResult invocationResult =
            Compose(invocationFailed);

        return !mappingResult.Succeeded
            && mappingResult.Status == NativeAppPackageStagingReadinessStatus.PayloadMappingFailed
            && mappingResult.PayloadMappingStatus == NativeAppPayloadMappingPlanStatus.DescriptorFailed
            && !memoryResult.Succeeded
            && memoryResult.Status == NativeAppPackageStagingReadinessStatus.ExecutableMemoryPlanFailed
            && memoryResult.ExecutableMemoryStatus == NativeAppExecutableMemoryPlanStatus.InvalidPageSize
            && !invocationResult.Succeeded
            && invocationResult.Status == NativeAppPackageStagingReadinessStatus.InvocationPlanFailed
            && invocationResult.InvocationStatus == NativeAppInvocationPlanStatus.InvalidStackSize;
    }

    private static bool TestRejectsByteMismatches()
    {
        ReadinessInputs packageMismatch = CreateInputs(
            packageFileStaging: OsPackageFileStagingResult.Create(
                OsPackageFileStagingStatus.Success,
                OsPackageFileSystemStatus.Success,
                OsPackageStatus.Success,
                fileIndex: 0,
                packageOffset: PayloadOffsetInPackage,
                fileLength: PayloadLength,
                bytesWritten: PayloadLength - 1,
                requiredBytes: PayloadLength - 1,
                copyAttempted: true));
        NativeAppPackageStagingReadinessResult packageResult =
            Compose(packageMismatch);

        ReadinessInputs payloadMismatch = CreateInputs(
            payloadStaging: NativeAppPayloadStagingResult.Success(
                CreateMappingPlanResult(),
                bytesWritten: ExecutableLength - 1));
        NativeAppPackageStagingReadinessResult payloadResult =
            Compose(payloadMismatch);

        ReadinessInputs writableMismatch = CreateInputs(
            invocation: NativeAppInvocationPlanResult.Success(
                NativeAppExecutableMemoryPlanStatus.Success,
                NativeAppPayloadMappingPlanStatus.Success,
                NativeAppLaunchDescriptorStatus.Success,
                NativeAppPayloadHeaderStatus.Success,
                NativeAppLaunchStatus.Success,
                CreateInvocationPlan(requiredWritableStagingBytes: 2048)));
        NativeAppPackageStagingReadinessResult writableResult =
            Compose(writableMismatch);

        ReadinessInputs entryMismatch = CreateInputs(
            invocation: NativeAppInvocationPlanResult.Success(
                NativeAppExecutableMemoryPlanStatus.Success,
                NativeAppPayloadMappingPlanStatus.Success,
                NativeAppLaunchDescriptorStatus.Success,
                NativeAppPayloadHeaderStatus.Success,
                NativeAppLaunchStatus.Success,
                CreateInvocationPlan(entryOffsetInPackage: PayloadOffsetInPackage + HeaderLength + 1)));
        NativeAppPackageStagingReadinessResult entryResult =
            Compose(entryMismatch);

        return !packageResult.Succeeded
            && packageResult.Status == NativeAppPackageStagingReadinessStatus.PackageStagingByteMismatch
            && !payloadResult.Succeeded
            && payloadResult.Status == NativeAppPackageStagingReadinessStatus.PayloadStagingByteMismatch
            && !writableResult.Succeeded
            && writableResult.Status == NativeAppPackageStagingReadinessStatus.WritableStagingByteMismatch
            && !entryResult.Succeeded
            && entryResult.Status == NativeAppPackageStagingReadinessStatus.EntryOffsetMismatch;
    }

    private static bool TestPreservesNoLaunchBoundary()
    {
        return NativeAppPackageStagingReadinessPlanner.RequiresManifestPackageAndPayloadResults
            && NativeAppPackageStagingReadinessPlanner.RequiresCallerOwnedPackageFileStaging
            && NativeAppPackageStagingReadinessPlanner.RequiresCallerOwnedPayloadStaging
            && !NativeAppPackageStagingReadinessPlanner.ReadsLiveStorage
            && !NativeAppPackageStagingReadinessPlanner.AllocatesMemory
            && !NativeAppPackageStagingReadinessPlanner.MapsExecutablePages
            && !NativeAppPackageStagingReadinessPlanner.ChangesPagePermissions
            && !NativeAppPackageStagingReadinessPlanner.RelocatesExecutable
            && !NativeAppPackageStagingReadinessPlanner.ExecutesAppCode
            && !RuntimeHelpers.IsReferenceOrContainsReferences<NativeAppPackageStagingReadinessResult>();
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        NativeAppPackageStagingReadinessResult result = default;

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == NativeAppPackageStagingReadinessStatus.Success
            && result.ManifestStatus == NativeManifestLoadStatus.Success
            && result.PackageStatus == OsPackageStatus.Success
            && result.LaunchStatus == NativeAppLaunchStatus.Success
            && result.DescriptorStatus == NativeAppLaunchDescriptorStatus.Success
            && result.PackageFileStagingStatus == OsPackageFileStagingStatus.Success
            && result.PayloadMappingStatus == NativeAppPayloadMappingPlanStatus.Success
            && result.PayloadStagingStatus == NativeAppPayloadStagingStatus.Success
            && result.ExecutableMemoryStatus == NativeAppExecutableMemoryPlanStatus.Success
            && result.InvocationStatus == NativeAppInvocationPlanStatus.Success
            && result.PackageFileBytesStaged == 0
            && result.PayloadExecutableBytesStaged == 0
            && result.RequiredWritableStagingBytes == 0
            && result.RequiredStackBytes == 0
            && !RuntimeHelpers.IsReferenceOrContainsReferences<NativeAppPackageStagingReadinessResult>();
    }

    private static NativeAppPackageStagingReadinessResult Compose(ReadinessInputs inputs)
    {
        return NativeAppPackageStagingReadinessPlanner.Compose(
            inputs.ManifestLoad,
            inputs.PackageParse,
            inputs.LaunchPlan,
            inputs.Descriptor,
            inputs.PackageFileStaging,
            inputs.PayloadMapping,
            inputs.PayloadStaging,
            inputs.ExecutableMemory,
            inputs.Invocation);
    }

    private static ReadinessInputs CreateInputs(
        NativeManifestLoadResult? manifestLoad = null,
        OsPackageParseResult? packageParse = null,
        NativeAppLaunchPlanResult? launchPlan = null,
        NativeAppLaunchDescriptorResult? descriptor = null,
        OsPackageFileStagingResult? packageFileStaging = null,
        NativeAppPayloadMappingPlanResult? payloadMapping = null,
        NativeAppPayloadStagingResult? payloadStaging = null,
        NativeAppExecutableMemoryPlanResult? executableMemory = null,
        NativeAppInvocationPlanResult? invocation = null)
    {
        byte[] manifestSource = ValidManifest();
        NativeManifestParseResult parsed = NativeManifestParser.Parse(manifestSource);
        if (!parsed.Succeeded)
        {
            throw new InvalidOperationException("Test manifest did not parse: " + parsed.Status);
        }

        NativeManifestLoadResult manifest = manifestLoad ??
            NativeManifestLoadResult.Success(parsed.Manifest, (uint)manifestSource.Length);
        OsPackageParseResult package = packageParse ??
            OsPackageParseResult.Success(new OsPackageHeader(
                OsPackageParser.SupportedVersion,
                OsPackageParser.HeaderSize,
                1,
                OsPackageParser.FileTableEntrySize,
                OsPackageParser.HeaderSize,
                OsPackageParser.FileTableEntrySize));
        NativeAppLaunchPlanResult launch = launchPlan ??
            NativeAppLaunchPlan.Create(
                manifestSource,
                parsed.Manifest,
                BootInfo.CreateHost(BootCapabilities.TextOutput),
                NativeAppLaunchRuntimeCapabilities.PackageStorage);

        NativeAppLaunchDescriptorResult selectedDescriptor = descriptor ??
            CreateDescriptor(launch);
        NativeAppPayloadMappingPlanResult mapping = payloadMapping ??
            NativeAppPayloadMappingPlanner.Create(selectedDescriptor);
        OsPackageFileStagingResult fileStaging = packageFileStaging ??
            OsPackageFileStagingResult.Create(
                OsPackageFileStagingStatus.Success,
                OsPackageFileSystemStatus.Success,
                OsPackageStatus.Success,
                fileIndex: 0,
                packageOffset: PayloadOffsetInPackage,
                fileLength: PayloadLength,
                bytesWritten: PayloadLength,
                requiredBytes: PayloadLength,
                copyAttempted: true);
        NativeAppPayloadStagingResult appStaging = payloadStaging ??
            NativeAppPayloadStagingResult.Success(mapping, ExecutableLength);
        NativeAppExecutableMemoryPlanResult memory = executableMemory ??
            NativeAppExecutableMemoryPlan.Create(mapping);
        NativeAppInvocationPlanResult invoke = invocation ??
            NativeAppInvocationPlanner.Create(memory, launch);

        return new ReadinessInputs(
            manifest,
            package,
            launch,
            selectedDescriptor,
            fileStaging,
            mapping,
            appStaging,
            memory,
            invoke);
    }

    private static NativeAppLaunchDescriptorResult CreateDescriptor(NativeAppLaunchPlanResult launchPlan)
    {
        NativeAppLaunchDescriptor descriptor = new(
            launchPlan.Plan,
            new OsPackageFileEntry(
                index: 0,
                pathOffset: 64,
                pathLength: (ushort)AppPath.Length,
                contentOffset: PayloadOffsetInPackage,
                contentLength: PayloadLength),
            PayloadLength,
            NativeManifestNetworkMode.None,
            NativeManifestLogMode.Serial,
            argumentByteLength: 0,
            CreatePayloadHeader());

        return NativeAppLaunchDescriptorResult.Success(descriptor);
    }

    private static NativeAppPayloadMappingPlanResult CreateMappingPlanResult()
    {
        return NativeAppPayloadMappingPlanner.Create(CreateDescriptor(CreateInputs().LaunchPlan));
    }

    private static NativeAppInvocationPlan CreateInvocationPlan(
        ulong? requiredWritableStagingBytes = null,
        ulong? entryOffsetInPackage = null)
    {
        return new NativeAppInvocationPlan(
            NativeAppInvocationPlanner.DefaultStackBytes,
            requiredWritableStagingBytes ?? 4096,
            PayloadOffsetInPackage,
            4096,
            PayloadOffsetInPackage + 4096,
            HeaderLength,
            ExecutableLength,
            EntryOffsetInExecutable,
            HeaderLength + EntryOffsetInExecutable,
            entryOffsetInPackage ?? PayloadOffsetInPackage + HeaderLength + EntryOffsetInExecutable,
            AppName.Length + AppPath.Length,
            NativeManifest.Version0,
            NativeManifestAppKind.Package,
            NativeManifestRuntimeKind.NativeAot,
            NativeManifestNetworkMode.None,
            NativeManifestLogMode.Serial,
            0,
            0,
            BootCapabilities.TextOutput,
            NativeAppLaunchRuntimeCapabilities.PackageStorage);
    }

    private static NativeAppPayloadHeader CreatePayloadHeader()
    {
        byte[] payload = new byte[(int)PayloadLength];
        payload[0] = (byte)'O';
        payload[1] = (byte)'S';
        payload[2] = (byte)'A';
        payload[3] = (byte)'P';
        payload[4] = (byte)'P';
        payload[5] = (byte)'v';
        payload[6] = (byte)'0';
        payload[7] = 0;
        EndianPrimitives.WriteUInt16LittleEndian(payload.AsSpan(8, 2), NativeAppPayloadHeader.SupportedVersion);
        EndianPrimitives.WriteUInt16LittleEndian(payload.AsSpan(10, 2), NativeAppPayloadHeader.HeaderSize);
        EndianPrimitives.WriteUInt32LittleEndian(payload.AsSpan(12, 4), 0);
        EndianPrimitives.WriteUInt64LittleEndian(
            payload.AsSpan(16, 8),
            HeaderLength + EntryOffsetInExecutable);
        EndianPrimitives.WriteUInt64LittleEndian(payload.AsSpan(24, 8), 0);

        NativeAppPayloadHeaderResult result = NativeAppPayloadHeader.Parse(payload);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Test payload header did not parse: " + result.Status);
        }

        return result.Header;
    }

    private static byte[] ValidManifest()
    {
        string source =
            "format=os-native-manifest\n" +
            "version=0\n" +
            "app.name=" + AppName + "\n" +
            "app.path=" + AppPath + "\n" +
            "args=\n" +
            "app.kind=package\n" +
            "runtime.kind=native-aot\n" +
            "network=none\n" +
            "log=serial\n";

        return Encoding.ASCII.GetBytes(source);
    }

    private readonly struct ReadinessInputs(
        NativeManifestLoadResult manifestLoad,
        OsPackageParseResult packageParse,
        NativeAppLaunchPlanResult launchPlan,
        NativeAppLaunchDescriptorResult descriptor,
        OsPackageFileStagingResult packageFileStaging,
        NativeAppPayloadMappingPlanResult payloadMapping,
        NativeAppPayloadStagingResult payloadStaging,
        NativeAppExecutableMemoryPlanResult executableMemory,
        NativeAppInvocationPlanResult invocation)
    {
        public NativeManifestLoadResult ManifestLoad { get; } = manifestLoad;

        public OsPackageParseResult PackageParse { get; } = packageParse;

        public NativeAppLaunchPlanResult LaunchPlan { get; } = launchPlan;

        public NativeAppLaunchDescriptorResult Descriptor { get; } = descriptor;

        public OsPackageFileStagingResult PackageFileStaging { get; } = packageFileStaging;

        public NativeAppPayloadMappingPlanResult PayloadMapping { get; } = payloadMapping;

        public NativeAppPayloadStagingResult PayloadStaging { get; } = payloadStaging;

        public NativeAppExecutableMemoryPlanResult ExecutableMemory { get; } = executableMemory;

        public NativeAppInvocationPlanResult Invocation { get; } = invocation;
    }
}
