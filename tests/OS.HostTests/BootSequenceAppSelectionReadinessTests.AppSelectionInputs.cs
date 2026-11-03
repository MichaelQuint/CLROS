using System.Runtime.CompilerServices;
using System.Text;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Manifest;
using OS.Std.Binary;
using OS.Storage;

internal static partial class BootSequenceAppSelectionReadinessTests
{
    private readonly struct AppSelectionInputs
    {
        public AppSelectionInputs(NativeManifestLoadResult manifestLoad, OsPackageParseResult packageParse, NativeAppLaunchPlanResult launchPlan, NativeAppLaunchDescriptorResult descriptor)
        {
            ManifestLoad = manifestLoad;
            PackageParse = packageParse;
            LaunchPlan = launchPlan;
            Descriptor = descriptor;
        }

        public NativeManifestLoadResult ManifestLoad { get; }
        public OsPackageParseResult PackageParse { get; }
        public NativeAppLaunchPlanResult LaunchPlan { get; }
        public NativeAppLaunchDescriptorResult Descriptor { get; }
    }
}
