using System.Runtime.CompilerServices;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Kernel.Runtime;
using OS.Manifest;
using OS.Storage;

internal static partial class BootSequenceAppEntryReadinessTests
{
    private readonly struct AppEntryInputs
    {
        public AppEntryInputs(NativeManifestLoadResult manifestLoad, OsPackageParseResult packageParse, NativeAppLaunchPlanResult launchPlan, NativeAppLaunchDescriptorResult descriptor, NativeAppEntryStagingPreflightResult stagingPreflight, NativeAppPayloadMappingPlanResult mappingPlan, NativeAppExecutableMemoryPlanResult memoryPlan, NativeAppInvocationPlanResult invocationPlan)
        {
            ManifestLoad = manifestLoad;
            PackageParse = packageParse;
            LaunchPlan = launchPlan;
            Descriptor = descriptor;
            StagingPreflight = stagingPreflight;
            MappingPlan = mappingPlan;
            MemoryPlan = memoryPlan;
            InvocationPlan = invocationPlan;
        }

        public NativeManifestLoadResult ManifestLoad { get; }
        public OsPackageParseResult PackageParse { get; }
        public NativeAppLaunchPlanResult LaunchPlan { get; }
        public NativeAppLaunchDescriptorResult Descriptor { get; }
        public NativeAppEntryStagingPreflightResult StagingPreflight { get; }
        public NativeAppPayloadMappingPlanResult MappingPlan { get; }
        public NativeAppExecutableMemoryPlanResult MemoryPlan { get; }
        public NativeAppInvocationPlanResult InvocationPlan { get; }
    }
}
