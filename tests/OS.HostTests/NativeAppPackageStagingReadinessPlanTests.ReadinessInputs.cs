using System.Runtime.CompilerServices;
using System.Text;
using OS.Boot;
using OS.Kernel.Runtime;
using OS.Manifest;
using OS.Std.Binary;
using OS.Storage;

internal static partial class NativeAppPackageStagingReadinessPlanTests
{
    private readonly struct ReadinessInputs(NativeManifestLoadResult manifestLoad, OsPackageParseResult packageParse, NativeAppLaunchPlanResult launchPlan, NativeAppLaunchDescriptorResult descriptor, OsPackageFileStagingResult packageFileStaging, NativeAppPayloadMappingPlanResult payloadMapping, NativeAppPayloadStagingResult payloadStaging, NativeAppExecutableMemoryPlanResult executableMemory, NativeAppInvocationPlanResult invocation)
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
