using System.Runtime.CompilerServices;
using System.Text;
using OS.Boot;
using OS.Manifest;
using OS.Pal;

internal static class NativeAppSocketListenBridgeTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("appmodel.socket.listenbridge creates manifest listener", TestCreatesManifestListener, ref failures);
        Run("appmodel.socket.listenbridge rejects policy mismatch", TestRejectsPolicyMismatch, ref failures);
        Run("appmodel.socket.listenbridge propagates create failure", TestPropagatesCreateFailure, ref failures);
        Run("appmodel.socket.listenbridge propagates bind failure", TestPropagatesBindFailure, ref failures);
        Run("appmodel.socket.listenbridge propagates listen failure", TestPropagatesListenFailure, ref failures);
        Run("appmodel.socket.listenbridge preserves mock-only boundary", TestPreservesMockOnlyBoundary, ref failures);
        Run("appmodel.socket.listenbridge default result is not success", TestDefaultResultIsNotSuccess, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Native app socket listen bridge host tests failed: " + failures);
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

    private static bool TestCreatesManifestListener()
    {
        byte[] source = ManifestWithListen("0.0.0.0:8080");
        NativeAppLaunchPlanResult launch = CreateLaunchPlan(source);
        OSHostSocketTable table = new(capacity: 2);
        OSHostIpv4Endpoint endpoint = Endpoint(0, 0, 0, 0, 8080);

        NativeAppSocketListenBridgeStatus status = NativeAppSocketListenBridge.CreateManifestApprovedListener(
            source,
            launch,
            table,
            endpoint,
            backlog: 8,
            out NativeAppSocketListenBridgeResult result);

        return status == NativeAppSocketListenBridgeStatus.Success
            && result.Succeeded
            && result.IsInitialized
            && result.PolicyStatus == NativeAppSocketBindPolicyStatus.Success
            && result.CreateAttempted
            && result.BindAttempted
            && result.ListenAttempted
            && result.CreateStatus == OSHostStatus.Ok
            && result.BindStatus == OSHostStatus.Ok
            && result.ListenStatus == OSHostStatus.Ok
            && result.Backlog == 8
            && result.Socket.IsValid
            && table.ActiveCount == 1
            && table.TryGetEntry(result.Socket, out OSHostSocketTableEntry entry) == OSHostStatus.Ok
            && entry.State == OSHostSocketLifecycleState.Listening
            && entry.ListenBacklog == 8
            && entry.LocalEndpoint.PortHostOrder == 8080
            && entry.LocalEndpoint.Address0 == 0
            && entry.LocalEndpoint.Address1 == 0
            && entry.LocalEndpoint.Address2 == 0
            && entry.LocalEndpoint.Address3 == 0;
    }

    private static bool TestRejectsPolicyMismatch()
    {
        byte[] source = ManifestWithListen("192.0.2.27:8080");
        NativeAppLaunchPlanResult launch = CreateLaunchPlan(source);
        OSHostSocketTable table = new(capacity: 1);

        NativeAppSocketListenBridgeStatus status = NativeAppSocketListenBridge.CreateManifestApprovedListener(
            source,
            launch,
            table,
            Endpoint(192, 0, 2, 27, 8081),
            backlog: 8,
            out NativeAppSocketListenBridgeResult result);

        return status == NativeAppSocketListenBridgeStatus.PolicyRejected
            && !result.Succeeded
            && result.IsInitialized
            && result.PolicyStatus == NativeAppSocketBindPolicyStatus.PortMismatch
            && !result.CreateAttempted
            && !result.BindAttempted
            && !result.ListenAttempted
            && result.CreateStatus == OSHostStatus.AccessDenied
            && table.ActiveCount == 0;
    }

    private static bool TestPropagatesCreateFailure()
    {
        byte[] source = ManifestWithListen("0.0.0.0:8080");
        NativeAppLaunchPlanResult launch = CreateLaunchPlan(source);
        OSHostSocketTable table = new(capacity: 0);

        NativeAppSocketListenBridgeStatus status = NativeAppSocketListenBridge.CreateManifestApprovedListener(
            source,
            launch,
            table,
            Endpoint(0, 0, 0, 0, 8080),
            backlog: 8,
            out NativeAppSocketListenBridgeResult result);

        return status == NativeAppSocketListenBridgeStatus.SocketCreateFailed
            && !result.Succeeded
            && result.IsInitialized
            && result.PolicyStatus == NativeAppSocketBindPolicyStatus.Success
            && result.CreateAttempted
            && !result.BindAttempted
            && !result.ListenAttempted
            && result.CreateStatus == OSHostStatus.OutOfMemory
            && !result.Socket.IsValid
            && table.ActiveCount == 0;
    }

    private static bool TestPropagatesBindFailure()
    {
        byte[] source = ManifestWithListen("0.0.0.0:8080");
        NativeAppLaunchPlanResult launch = CreateLaunchPlan(source);
        OSHostSocketTable table = new(capacity: 2);
        OSHostIpv4Endpoint endpoint = Endpoint(0, 0, 0, 0, 8080);
        OSHostSocketCreateResult existing = table.Create(TcpCreateOptions());
        if (!existing.Succeeded || table.Bind(existing.Socket, endpoint) != OSHostStatus.Ok)
        {
            return false;
        }

        NativeAppSocketListenBridgeStatus status = NativeAppSocketListenBridge.CreateManifestApprovedListener(
            source,
            launch,
            table,
            endpoint,
            backlog: 8,
            out NativeAppSocketListenBridgeResult result);

        return status == NativeAppSocketListenBridgeStatus.SocketBindFailed
            && !result.Succeeded
            && result.IsInitialized
            && result.PolicyStatus == NativeAppSocketBindPolicyStatus.Success
            && result.CreateAttempted
            && result.BindAttempted
            && !result.ListenAttempted
            && result.CreateStatus == OSHostStatus.Ok
            && result.BindStatus == OSHostStatus.AddressInUse
            && result.Socket.IsValid
            && table.TryGetEntry(result.Socket, out OSHostSocketTableEntry entry) == OSHostStatus.Ok
            && entry.State == OSHostSocketLifecycleState.Created;
    }

    private static bool TestPropagatesListenFailure()
    {
        byte[] source = ManifestWithListen("0.0.0.0:8080");
        NativeAppLaunchPlanResult launch = CreateLaunchPlan(source);
        OSHostSocketTable table = new(capacity: 1);
        OSHostIpv4Endpoint endpoint = Endpoint(0, 0, 0, 0, 8080);

        NativeAppSocketListenBridgeStatus status = NativeAppSocketListenBridge.CreateManifestApprovedListener(
            source,
            launch,
            table,
            endpoint,
            backlog: 0,
            out NativeAppSocketListenBridgeResult result);

        return status == NativeAppSocketListenBridgeStatus.SocketListenFailed
            && !result.Succeeded
            && result.IsInitialized
            && result.PolicyStatus == NativeAppSocketBindPolicyStatus.Success
            && result.CreateAttempted
            && result.BindAttempted
            && result.ListenAttempted
            && result.CreateStatus == OSHostStatus.Ok
            && result.BindStatus == OSHostStatus.Ok
            && result.ListenStatus == OSHostStatus.InvalidArgument
            && table.TryGetEntry(result.Socket, out OSHostSocketTableEntry entry) == OSHostStatus.Ok
            && entry.State == OSHostSocketLifecycleState.Bound
            && entry.LocalEndpoint.PortHostOrder == 8080;
    }

    private static bool TestPreservesMockOnlyBoundary()
    {
        byte[] source = ManifestWithListen("0.0.0.0:8080");
        NativeAppLaunchPlanResult launch = CreateLaunchPlan(source);

        NativeAppSocketListenBridgeStatus status = NativeAppSocketListenBridge.CreateManifestApprovedListener(
            source,
            launch,
            table: null,
            endpoint: Endpoint(0, 0, 0, 0, 8080),
            backlog: 8,
            out NativeAppSocketListenBridgeResult result);

        return NativeAppSocketListenBridge.UsesMockSocketTable
            && NativeAppSocketListenBridge.CallsPolicyFirst
            && NativeAppSocketListenBridge.CreatesSocket
            && !NativeAppSocketListenBridge.ExportsPalFunctions
            && !NativeAppSocketListenBridge.OpensLiveSockets
            && !NativeAppSocketListenBridge.WaitsOnScheduler
            && !NativeAppSocketListenBridge.StartsKestrel
            && !RuntimeHelpers.IsReferenceOrContainsReferences<NativeAppSocketListenBridgeResult>()
            && status == NativeAppSocketListenBridgeStatus.MissingSocketTable
            && result.IsInitialized
            && result.PolicyStatus == NativeAppSocketBindPolicyStatus.Success
            && !result.CreateAttempted
            && !result.BindAttempted
            && !result.ListenAttempted
            && result.CreateStatus == OSHostStatus.InvalidArgument;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        NativeAppSocketListenBridgeResult result = default;

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == NativeAppSocketListenBridgeStatus.Success
            && result.PolicyStatus == NativeAppSocketBindPolicyStatus.Success
            && result.LaunchStatus == NativeAppLaunchStatus.Success
            && result.Backlog == 0
            && !result.CreateAttempted
            && result.CreateStatus == OSHostStatus.Ok
            && !result.BindAttempted
            && result.BindStatus == OSHostStatus.Ok
            && !result.ListenAttempted
            && result.ListenStatus == OSHostStatus.Ok
            && result.Socket.Value == 0
            && !RuntimeHelpers.IsReferenceOrContainsReferences<NativeAppSocketListenBridgeResult>();
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

    private static OSHostSocketCreateOptions TcpCreateOptions()
    {
        return new OSHostSocketCreateOptions(
            OSHostSocketConstants.DomainIpv4,
            OSHostSocketConstants.TypeStream,
            OSHostSocketConstants.ProtocolTcp,
            flags: 0,
            OSHostSocketTable.CreateOptionsVersion,
            reserved: 0);
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
