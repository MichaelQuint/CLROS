using System.Runtime.CompilerServices;
using System.Text;
using OS.Boot;
using OS.Manifest;
using OS.Pal;

internal static class NativeAppSocketBindBridgeTests
{

    [HostTest("appmodel.socket.bindbridge binds manifest-approved endpoint", HostLane.Fast, 0)]
    private static bool TestBindsManifestApprovedEndpoint()
    {
        byte[] source = ManifestWithListen("0.0.0.0:8080");
        NativeAppLaunchPlanResult launch = CreateLaunchPlan(source);
        OSHostSocketTable table = new(capacity: 2);
        OSHostSocketCreateResult create = table.Create(TcpCreateOptions());
        OSHostIpv4Endpoint endpoint = Endpoint(0, 0, 0, 0, 8080);

        NativeAppSocketBindBridgeStatus status = NativeAppSocketBindBridge.BindManifestApprovedEndpoint(
            source,
            launch,
            table,
            create.Socket,
            endpoint,
            out NativeAppSocketBindBridgeResult result);

        return HostCheck.Result(status == NativeAppSocketBindBridgeStatus.Success
            && result.Succeeded
            && result.IsInitialized
            && result.PolicyStatus == NativeAppSocketBindPolicyStatus.Success
            && result.SocketStatus == OSHostStatus.Ok
            && result.Socket.Value == create.Socket.Value
            && table.TryGetEntry(create.Socket, out OSHostSocketTableEntry entry) == OSHostStatus.Ok
            && entry.State == OSHostSocketLifecycleState.Bound
            && entry.LocalEndpoint.PortHostOrder == 8080
            && entry.LocalEndpoint.Address0 == 0
            && entry.LocalEndpoint.Address1 == 0
            && entry.LocalEndpoint.Address2 == 0
            && entry.LocalEndpoint.Address3 == 0);
    }

    [HostTest("appmodel.socket.bindbridge rejects policy mismatch", HostLane.Fast, 1)]
    private static bool TestRejectsPolicyMismatch()
    {
        byte[] source = ManifestWithListen("192.0.2.27:8080");
        NativeAppLaunchPlanResult launch = CreateLaunchPlan(source);
        OSHostSocketTable table = new(capacity: 1);
        OSHostSocketCreateResult create = table.Create(TcpCreateOptions());
        OSHostIpv4Endpoint wrongEndpoint = Endpoint(192, 0, 2, 27, 8081);

        NativeAppSocketBindBridgeStatus status = NativeAppSocketBindBridge.BindManifestApprovedEndpoint(
            source,
            launch,
            table,
            create.Socket,
            wrongEndpoint,
            out NativeAppSocketBindBridgeResult result);

        return HostCheck.Result(status == NativeAppSocketBindBridgeStatus.PolicyRejected
            && !result.Succeeded
            && result.IsInitialized
            && result.PolicyStatus == NativeAppSocketBindPolicyStatus.PortMismatch
            && result.SocketStatus == OSHostStatus.AccessDenied
            && table.TryGetEntry(create.Socket, out OSHostSocketTableEntry entry) == OSHostStatus.Ok
            && entry.State == OSHostSocketLifecycleState.Created);
    }

    [HostTest("appmodel.socket.bindbridge propagates mock bind conflict", HostLane.Fast, 2)]
    private static bool TestPropagatesMockBindConflict()
    {
        byte[] source = ManifestWithListen("0.0.0.0:8080");
        NativeAppLaunchPlanResult launch = CreateLaunchPlan(source);
        OSHostSocketTable table = new(capacity: 2);
        OSHostSocketCreateResult first = table.Create(TcpCreateOptions());
        OSHostSocketCreateResult second = table.Create(TcpCreateOptions());
        OSHostIpv4Endpoint endpoint = Endpoint(0, 0, 0, 0, 8080);
        if (table.Bind(first.Socket, endpoint) != OSHostStatus.Ok)
        {
            return HostCheck.Result(false);
        }

        NativeAppSocketBindBridgeStatus status = NativeAppSocketBindBridge.BindManifestApprovedEndpoint(
            source,
            launch,
            table,
            second.Socket,
            endpoint,
            out NativeAppSocketBindBridgeResult result);

        return HostCheck.Result(status == NativeAppSocketBindBridgeStatus.SocketBindFailed
            && !result.Succeeded
            && result.IsInitialized
            && result.PolicyStatus == NativeAppSocketBindPolicyStatus.Success
            && result.SocketStatus == OSHostStatus.AddressInUse
            && table.TryGetEntry(second.Socket, out OSHostSocketTableEntry entry) == OSHostStatus.Ok
            && entry.State == OSHostSocketLifecycleState.Created);
    }

    [HostTest("appmodel.socket.bindbridge rejects missing table", HostLane.Fast, 3)]
    private static bool TestRejectsMissingTable()
    {
        byte[] source = ManifestWithListen("0.0.0.0:8080");
        NativeAppLaunchPlanResult launch = CreateLaunchPlan(source);
        OSHostIpv4Endpoint endpoint = Endpoint(0, 0, 0, 0, 8080);

        NativeAppSocketBindBridgeStatus status = NativeAppSocketBindBridge.BindManifestApprovedEndpoint(
            source,
            launch,
            table: null,
            socket: new OSHostSocketHandle(1),
            endpoint,
            out NativeAppSocketBindBridgeResult result);

        return HostCheck.Result(status == NativeAppSocketBindBridgeStatus.MissingSocketTable
            && !result.Succeeded
            && result.IsInitialized
            && result.PolicyStatus == NativeAppSocketBindPolicyStatus.Success
            && result.SocketStatus == OSHostStatus.InvalidArgument);
    }

    [HostTest("appmodel.socket.bindbridge preserves mock-only boundary", HostLane.Fast, 4)]
    private static bool TestPreservesMockOnlyBoundary()
    {
        byte[] source = ManifestWithListen("localhost:8080");
        NativeAppLaunchPlanResult launch = CreateLaunchPlan(source);
        OSHostSocketTable table = new(capacity: 1);
        OSHostSocketCreateResult create = table.Create(TcpCreateOptions());

        NativeAppSocketBindBridgeStatus status = NativeAppSocketBindBridge.BindManifestApprovedEndpoint(
            source,
            launch,
            table,
            create.Socket,
            Endpoint(127, 0, 0, 1, 8080),
            out NativeAppSocketBindBridgeResult result);

        return HostCheck.Result(NativeAppSocketBindBridge.UsesMockSocketTable
            && NativeAppSocketBindBridge.CallsPolicyFirst
            && !NativeAppSocketBindBridge.ExportsPalFunctions
            && !NativeAppSocketBindBridge.OpensLiveSockets
            && !NativeAppSocketBindBridge.StartsKestrel
            && !RuntimeHelpers.IsReferenceOrContainsReferences<NativeAppSocketBindBridgeResult>()
            && status == NativeAppSocketBindBridgeStatus.PolicyRejected
            && result.IsInitialized
            && result.PolicyStatus == NativeAppSocketBindPolicyStatus.InvalidListenHost
            && table.TryGetEntry(create.Socket, out OSHostSocketTableEntry entry) == OSHostStatus.Ok
            && entry.State == OSHostSocketLifecycleState.Created);
    }

    [HostTest("appmodel.socket.bindbridge default result is not success", HostLane.Fast, 5)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        NativeAppSocketBindBridgeResult result = default;

        return HostCheck.Result(!result.IsInitialized
            && !result.Succeeded
            && result.Status == NativeAppSocketBindBridgeStatus.Success
            && result.PolicyStatus == NativeAppSocketBindPolicyStatus.Success
            && result.LaunchStatus == NativeAppLaunchStatus.Success
            && result.SocketStatus == OSHostStatus.Ok
            && result.Socket.Value == 0
            && !RuntimeHelpers.IsReferenceOrContainsReferences<NativeAppSocketBindBridgeResult>());
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
