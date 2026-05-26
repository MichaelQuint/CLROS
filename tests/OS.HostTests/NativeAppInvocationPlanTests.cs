using System.Runtime.CompilerServices;
using System.Text;
using OS.Boot;
using OS.Kernel.Runtime;
using OS.Manifest;

internal static class NativeAppInvocationPlanTests
{
    private const string AppName = "hello.app";
    private const string AppPath = "apps/hello.bin";
    private const string Arguments = "--port=8080";
    private const ulong PayloadOffsetInPackage = 4096;
    private const ulong HeaderLength = 32;
    private const ulong ExecutableLength = 64;
    private const ulong PayloadLength = HeaderLength + ExecutableLength;

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("appmodel native invocation plan accepts executable memory", TestAcceptsExecutableMemory, ref failures);
        Run("appmodel native invocation plan rejects memory failure", TestRejectsMemoryFailure, ref failures);
        Run("appmodel native invocation plan rejects invalid stack", TestRejectsInvalidStack, ref failures);
        Run("appmodel native invocation plan preserves manifest metadata counts", TestPreservesManifestMetadataCounts, ref failures);
        Run("appmodel native invocation plan rejects invalid entry metadata", TestRejectsInvalidEntryMetadata, ref failures);
        Run("appmodel native invocation plan default result is not success", TestDefaultResultIsNotSuccess, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Native app invocation plan host tests failed: " + failures);
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

    private static bool TestAcceptsExecutableMemory()
    {
        NativeAppExecutableMemoryPlanResult executableMemoryPlan = CreateExecutableMemoryPlan();
        NativeAppLaunchPlanResult launchPlan = CreateLaunchPlan();

        NativeAppInvocationPlanResult result =
            NativeAppInvocationPlanner.Create(executableMemoryPlan, launchPlan);

        return result.Succeeded
            && result.IsInitialized
            && result.Status == NativeAppInvocationPlanStatus.Success
            && result.ExecutableMemoryStatus == NativeAppExecutableMemoryPlanStatus.Success
            && result.PayloadMappingStatus == NativeAppPayloadMappingPlanStatus.Success
            && result.LaunchStatus == NativeAppLaunchStatus.Success
            && result.Plan.RequiredStackBytes == NativeAppInvocationPlanner.DefaultStackBytes
            && result.Plan.RequiredWritableStagingBytes == executableMemoryPlan.RequiredWritableStagingBytes
            && result.Plan.ExecutableMapByteLength == executableMemoryPlan.ExecutableMapByteLength
            && result.Plan.EntryOffsetInExecutableMapping == executableMemoryPlan.EntryOffsetInExecutableMapping
            && result.Plan.EntryOffsetInPackage ==
                executableMemoryPlan.ExecutableMapOffsetInPackage +
                executableMemoryPlan.EntryOffsetInExecutableMapping
            && result.Plan.RequiredBootCapabilities == BootCapabilities.TextOutput
            && result.Plan.RequiredRuntimeCapabilities ==
                (NativeAppLaunchRuntimeCapabilities.PackageStorage | NativeAppLaunchRuntimeCapabilities.Network);
    }

    private static bool TestRejectsMemoryFailure()
    {
        NativeAppExecutableMemoryPlanResult executableMemoryPlan =
            NativeAppExecutableMemoryPlan.Create(
                NativeAppPayloadMappingPlanResult.Fail(
                    NativeAppPayloadMappingPlanStatus.DescriptorFailed,
                    NativeAppLaunchDescriptorStatus.InvalidPayloadHeader,
                    NativeAppPayloadHeaderStatus.InvalidMagic));

        NativeAppInvocationPlanResult result =
            NativeAppInvocationPlanner.Create(executableMemoryPlan, CreateLaunchPlan());

        return !result.Succeeded
            && result.IsInitialized
            && result.Status == NativeAppInvocationPlanStatus.ExecutableMemoryPlanFailed
            && result.ExecutableMemoryStatus == NativeAppExecutableMemoryPlanStatus.MappingPlanFailed
            && result.PayloadMappingStatus == NativeAppPayloadMappingPlanStatus.DescriptorFailed;
    }

    private static bool TestRejectsInvalidStack()
    {
        NativeAppInvocationPlanResult tooSmall =
            NativeAppInvocationPlanner.Create(
                CreateExecutableMemoryPlan(),
                CreateLaunchPlan(),
                stackSizeBytes: 8 * 1024);

        NativeAppInvocationPlanResult unaligned =
            NativeAppInvocationPlanner.Create(
                CreateExecutableMemoryPlan(),
                CreateLaunchPlan(),
                stackSizeBytes: NativeAppInvocationPlanner.DefaultStackBytes + 1);

        return !tooSmall.Succeeded
            && tooSmall.IsInitialized
            && tooSmall.Status == NativeAppInvocationPlanStatus.InvalidStackSize
            && !unaligned.Succeeded
            && unaligned.IsInitialized
            && unaligned.Status == NativeAppInvocationPlanStatus.InvalidStackSize;
    }

    private static bool TestPreservesManifestMetadataCounts()
    {
        NativeAppLaunchPlanResult launchPlan = CreateLaunchPlanWithEnvironment();
        NativeAppInvocationPlanResult result =
            NativeAppInvocationPlanner.Create(CreateExecutableMemoryPlan(), launchPlan);

        int expectedMetadataBytes =
            AppName.Length +
            AppPath.Length +
            Arguments.Length +
            "ASPNETCORE_ENVIRONMENT".Length +
            "Production".Length +
            "PORT".Length +
            "8080".Length;

        return result.Succeeded
            && result.IsInitialized
            && result.Plan.ManifestVersion == NativeManifest.Version0
            && result.Plan.AppKind == NativeManifestAppKind.Package
            && result.Plan.RuntimeKind == NativeManifestRuntimeKind.NativeAot
            && result.Plan.NetworkMode == NativeManifestNetworkMode.Dhcp
            && result.Plan.LogMode == NativeManifestLogMode.Serial
            && result.Plan.ArgumentByteLength == Arguments.Length
            && result.Plan.EnvironmentCount == 2
            && result.Plan.ManifestMetadataByteCount == expectedMetadataBytes;
    }

    private static bool TestRejectsInvalidEntryMetadata()
    {
        NativeAppExecutableMemoryPlanResult executableMemoryPlan =
            NativeAppExecutableMemoryPlanResult.Success(
                NativeAppPayloadMappingPlanStatus.Success,
                NativeAppLaunchDescriptorStatus.Success,
                NativeAppPayloadHeaderStatus.Success,
                pageSize: NativeAppExecutableMemoryPlan.DefaultPageSize,
                executableAlignment: NativeAppExecutableMemoryPlan.DefaultExecutableAlignment,
                requiredWritableStagingBytes: 4096,
                executableBytes: 32,
                executableMapOffsetInPackage: PayloadOffsetInPackage,
                executableMapByteLength: 4096,
                executableMapEndOffsetInPackage: PayloadOffsetInPackage + 4096,
                executableContentOffsetInMapping: HeaderLength,
                entryOffsetInExecutableContent: 8,
                entryOffsetInExecutableMapping: 4096,
                writableStagingProtection: NativeAppExecutableMemoryProtection.Read |
                    NativeAppExecutableMemoryProtection.Write,
                executableProtection: NativeAppExecutableMemoryProtection.Read |
                    NativeAppExecutableMemoryProtection.Execute,
                finalProtection: NativeAppExecutableMemoryProtection.Read |
                    NativeAppExecutableMemoryProtection.Execute);

        NativeAppInvocationPlanResult result =
            NativeAppInvocationPlanner.Create(executableMemoryPlan, CreateLaunchPlan());

        return !result.Succeeded
            && result.IsInitialized
            && result.Status == NativeAppInvocationPlanStatus.InvalidEntryOffset
            && result.ExecutableMemoryStatus == NativeAppExecutableMemoryPlanStatus.Success;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        NativeAppInvocationPlanResult result = default;

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == NativeAppInvocationPlanStatus.Success
            && result.ExecutableMemoryStatus == NativeAppExecutableMemoryPlanStatus.Success
            && result.PayloadMappingStatus == NativeAppPayloadMappingPlanStatus.Success
            && result.DescriptorStatus == NativeAppLaunchDescriptorStatus.Success
            && result.PayloadHeaderStatus == NativeAppPayloadHeaderStatus.Success
            && result.LaunchStatus == NativeAppLaunchStatus.Success
            && result.Plan.RequiredStackBytes == 0
            && result.Plan.RequiredWritableStagingBytes == 0
            && result.Plan.ExecutableMapByteLength == 0
            && result.Plan.ExecutableBytes == 0
            && result.Plan.EntryOffsetInExecutableMapping == 0
            && result.Plan.ManifestMetadataByteCount == 0
            && result.Plan.ArgumentByteLength == 0
            && result.Plan.EnvironmentCount == 0
            && !RuntimeHelpers.IsReferenceOrContainsReferences<NativeAppInvocationPlanResult>();
    }

    private static NativeAppExecutableMemoryPlanResult CreateExecutableMemoryPlan()
    {
        return NativeAppExecutableMemoryPlan.Create(
            NativeAppPayloadMappingPlanResult.Success(
                CreatePayloadMappingPlan(entryOffsetInExecutable: 8)));
    }

    private static NativeAppPayloadMappingPlan CreatePayloadMappingPlan(ulong entryOffsetInExecutable)
    {
        ulong executableOffsetInPackage = PayloadOffsetInPackage + HeaderLength;
        ulong entryOffsetInPayload = HeaderLength + entryOffsetInExecutable;
        ulong entryOffsetInPackage = PayloadOffsetInPackage + entryOffsetInPayload;

        return new NativeAppPayloadMappingPlan(
            PayloadOffsetInPackage,
            PayloadLength,
            HeaderLength,
            HeaderLength,
            executableOffsetInPackage,
            ExecutableLength,
            entryOffsetInPayload,
            entryOffsetInExecutable,
            entryOffsetInPackage);
    }

    private static NativeAppLaunchPlanResult CreateLaunchPlan()
    {
        return CreateLaunchPlan(ValidManifest(includeEnvironment: false));
    }

    private static NativeAppLaunchPlanResult CreateLaunchPlanWithEnvironment()
    {
        return CreateLaunchPlan(ValidManifest(includeEnvironment: true));
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

    private static byte[] ValidManifest(bool includeEnvironment)
    {
        string source =
            "format=os-native-manifest\n" +
            "version=0\n" +
            "app.name=" + AppName + "\n" +
            "app.path=" + AppPath + "\n" +
            "args=" + Arguments + "\n" +
            "app.kind=package\n" +
            "runtime.kind=native-aot\n" +
            "network=dhcp\n" +
            "log=serial\n";

        if (includeEnvironment)
        {
            source +=
                "env.ASPNETCORE_ENVIRONMENT=Production\n" +
                "env.PORT=8080\n";
        }

        return Encoding.ASCII.GetBytes(source);
    }
}
