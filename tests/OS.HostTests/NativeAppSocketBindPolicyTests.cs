using System.Runtime.CompilerServices;
using System.Text;
using OS.Boot;
using OS.Manifest;
using OS.Pal;

internal static class NativeAppSocketBindPolicyTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("appmodel.socket.policy accepts manifest listen endpoint", TestAcceptsManifestListenEndpoint, ref failures);
        Run("appmodel.socket.policy accepts wildcard manifest endpoint", TestAcceptsWildcardManifestEndpoint, ref failures);
        Run("appmodel.socket.policy rejects missing listen endpoint", TestRejectsMissingListenEndpoint, ref failures);
        Run("appmodel.socket.policy rejects wrong port", TestRejectsWrongPort, ref failures);
        Run("appmodel.socket.policy rejects wrong address", TestRejectsWrongAddress, ref failures);
        Run("appmodel.socket.policy preserves no-socket-io boundary", TestPreservesNoSocketIoBoundary, ref failures);
        Run("appmodel.socket.policy default result is not success", TestDefaultResultIsNotSuccess, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Native app socket bind policy host tests failed: " + failures);
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

    private static bool TestAcceptsManifestListenEndpoint()
    {
        byte[] source = ManifestWithListen("192.0.2.27:8080");
        NativeAppLaunchPlanResult launch = CreateLaunchPlan(source);
        OSHostIpv4Endpoint endpoint = Endpoint(192, 0, 2, 27, 8080);

        NativeAppSocketBindPolicyStatus status = NativeAppSocketBindPolicy.ValidateBindEndpoint(
            source,
            launch,
            endpoint,
            out NativeAppSocketBindPolicyResult result);

        return status == NativeAppSocketBindPolicyStatus.Success
            && result.Succeeded
            && result.IsInitialized
            && result.LaunchStatus == NativeAppLaunchStatus.Success
            && result.ExpectedPort == 8080
            && result.ActualPort == 8080
            && result.ExpectedAddress0 == 192
            && result.ExpectedAddress1 == 0
            && result.ExpectedAddress2 == 2
            && result.ExpectedAddress3 == 27
            && result.ActualAddress0 == 192
            && result.ActualAddress1 == 0
            && result.ActualAddress2 == 2
            && result.ActualAddress3 == 27
            && !result.Wildcard;
    }

    private static bool TestAcceptsWildcardManifestEndpoint()
    {
        byte[] source = ManifestWithListen("0.0.0.0:8080");
        NativeAppLaunchPlanResult launch = CreateLaunchPlan(source);
        OSHostIpv4Endpoint endpoint = Endpoint(0, 0, 0, 0, 8080);

        NativeAppSocketBindPolicyStatus status = NativeAppSocketBindPolicy.ValidateBindEndpoint(
            source,
            launch,
            endpoint,
            out NativeAppSocketBindPolicyResult result);

        return status == NativeAppSocketBindPolicyStatus.Success
            && result.Succeeded
            && result.IsInitialized
            && result.ExpectedPort == 8080
            && result.ActualPort == 8080
            && result.ExpectedAddress0 == 0
            && result.ExpectedAddress1 == 0
            && result.ExpectedAddress2 == 0
            && result.ExpectedAddress3 == 0
            && result.Wildcard;
    }

    private static bool TestRejectsMissingListenEndpoint()
    {
        byte[] source = ManifestWithoutListen();
        NativeAppLaunchPlanResult launch = CreateLaunchPlan(source);
        OSHostIpv4Endpoint endpoint = Endpoint(0, 0, 0, 0, 8080);

        NativeAppSocketBindPolicyStatus status = NativeAppSocketBindPolicy.ValidateBindEndpoint(
            source,
            launch,
            endpoint,
            out NativeAppSocketBindPolicyResult result);

        return status == NativeAppSocketBindPolicyStatus.MissingListenEndpoint
            && !result.Succeeded
            && result.IsInitialized
            && result.LaunchStatus == NativeAppLaunchStatus.Success
            && result.ExpectedPort == 0
            && result.ActualPort == 8080;
    }

    private static bool TestRejectsWrongPort()
    {
        byte[] source = ManifestWithListen("192.0.2.27:8080");
        NativeAppLaunchPlanResult launch = CreateLaunchPlan(source);
        OSHostIpv4Endpoint endpoint = Endpoint(192, 0, 2, 27, 8081);

        NativeAppSocketBindPolicyStatus status = NativeAppSocketBindPolicy.ValidateBindEndpoint(
            source,
            launch,
            endpoint,
            out NativeAppSocketBindPolicyResult result);

        return status == NativeAppSocketBindPolicyStatus.PortMismatch
            && !result.Succeeded
            && result.IsInitialized
            && result.ExpectedPort == 8080
            && result.ActualPort == 8081
            && result.ExpectedAddress0 == 192
            && result.ActualAddress0 == 192;
    }

    private static bool TestRejectsWrongAddress()
    {
        byte[] source = ManifestWithListen("192.0.2.27:8080");
        NativeAppLaunchPlanResult launch = CreateLaunchPlan(source);
        OSHostIpv4Endpoint endpoint = Endpoint(192, 0, 2, 28, 8080);

        NativeAppSocketBindPolicyStatus status = NativeAppSocketBindPolicy.ValidateBindEndpoint(
            source,
            launch,
            endpoint,
            out NativeAppSocketBindPolicyResult result);

        return status == NativeAppSocketBindPolicyStatus.AddressMismatch
            && !result.Succeeded
            && result.IsInitialized
            && result.ExpectedPort == 8080
            && result.ActualPort == 8080
            && result.ExpectedAddress3 == 27
            && result.ActualAddress3 == 28;
    }

    private static bool TestPreservesNoSocketIoBoundary()
    {
        byte[] hostSource = ManifestWithListen("localhost:8080");
        NativeAppLaunchPlanResult hostLaunch = CreateLaunchPlan(hostSource);
        NativeAppSocketBindPolicyStatus invalidHostStatus = NativeAppSocketBindPolicy.ValidateBindEndpoint(
            hostSource,
            hostLaunch,
            Endpoint(127, 0, 0, 1, 8080),
            out NativeAppSocketBindPolicyResult invalidHostResult);
        NativeAppSocketBindPolicyStatus invalidEndpointStatus = NativeAppSocketBindPolicy.ValidateBindEndpoint(
            hostSource,
            hostLaunch,
            default,
            out NativeAppSocketBindPolicyResult invalidEndpointResult);

        return NativeAppSocketBindPolicy.RequiresManifestListenEndpoint
            && !NativeAppSocketBindPolicy.OpensSockets
            && !NativeAppSocketBindPolicy.CallsPalSocketBind
            && !NativeAppSocketBindPolicy.ExportsPalFunctions
            && !NativeAppSocketBindPolicy.StartsKestrel
            && !RuntimeHelpers.IsReferenceOrContainsReferences<NativeAppSocketBindPolicyResult>()
            && invalidHostStatus == NativeAppSocketBindPolicyStatus.InvalidListenHost
            && !invalidHostResult.Succeeded
            && invalidHostResult.IsInitialized
            && invalidEndpointStatus == NativeAppSocketBindPolicyStatus.InvalidBindEndpoint
            && !invalidEndpointResult.Succeeded
            && invalidEndpointResult.IsInitialized;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        NativeAppSocketBindPolicyResult result = default;

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == NativeAppSocketBindPolicyStatus.Success
            && result.LaunchStatus == NativeAppLaunchStatus.Success
            && result.ExpectedPort == 0
            && result.ActualPort == 0
            && result.ExpectedAddress0 == 0
            && result.ExpectedAddress1 == 0
            && result.ExpectedAddress2 == 0
            && result.ExpectedAddress3 == 0
            && result.ActualAddress0 == 0
            && result.ActualAddress1 == 0
            && result.ActualAddress2 == 0
            && result.ActualAddress3 == 0
            && !result.Wildcard
            && !RuntimeHelpers.IsReferenceOrContainsReferences<NativeAppSocketBindPolicyResult>();
    }

    private static NativeAppLaunchPlanResult CreateLaunchPlan(byte[] source)
    {
        NativeManifestParseResult parse = NativeManifestParser.Parse(source);
        if (!parse.Succeeded)
        {
            throw new InvalidOperationException("Manifest fixture failed to parse.");
        }

        return NativeAppLaunchPlan.Create(
            source,
            parse.Manifest,
            BootInfo.CreateHost(BootCapabilities.TextOutput),
            NativeAppLaunchRuntimeCapabilities.PackageStorage | NativeAppLaunchRuntimeCapabilities.Network);
    }

    private static byte[] ManifestWithListen(string listen)
    {
        return Encoding.ASCII.GetBytes(
            "format=os-native-manifest\n" +
            "version=0\n" +
            "app.name=hello.app\n" +
            "app.path=apps/hello.bin\n" +
            "args=\n" +
            "app.kind=package\n" +
            "runtime.kind=native-aot\n" +
            "network=dhcp\n" +
            "listen=" + listen + "\n" +
            "log=serial\n");
    }

    private static byte[] ManifestWithoutListen()
    {
        return Encoding.ASCII.GetBytes(
            "format=os-native-manifest\n" +
            "version=0\n" +
            "app.name=hello.app\n" +
            "app.path=apps/hello.bin\n" +
            "args=\n" +
            "app.kind=package\n" +
            "runtime.kind=native-aot\n" +
            "network=dhcp\n" +
            "log=serial\n");
    }

    private static OSHostIpv4Endpoint Endpoint(
        byte address0,
        byte address1,
        byte address2,
        byte address3,
        ushort port)
    {
        OSHostStatus status = OSHostIpv4Endpoint.TryCreate(
            address0,
            address1,
            address2,
            address3,
            port,
            out OSHostIpv4Endpoint endpoint);
        if (status != OSHostStatus.Ok)
        {
            throw new InvalidOperationException("Invalid endpoint fixture.");
        }

        return endpoint;
    }
}
