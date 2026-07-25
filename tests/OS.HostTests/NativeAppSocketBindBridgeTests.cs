using System.Runtime.CompilerServices;
using System.Text;
using OS.Boot;
using OS.Manifest;
using OS.Pal;

internal static class NativeAppSocketBindBridgeTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("appmodel.socket.bindbridge binds manifest-approved endpoint", TestBindsManifestApprovedEndpoint, ref failures);
        Run("appmodel.socket.bindbridge rejects policy mismatch", TestRejectsPolicyMismatch, ref failures);
        Run("appmodel.socket.bindbridge propagates mock bind conflict", TestPropagatesMockBindConflict, ref failures);
        Run("appmodel.socket.bindbridge rejects missing table", TestRejectsMissingTable, ref failures);
        Run("appmodel.socket.bindbridge preserves mock-only boundary", TestPreservesMockOnlyBoundary, ref failures);
        Run("appmodel.socket.bindbridge default result is not success", TestDefaultResultIsNotSuccess, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Native app socket bind bridge host tests failed: " + failures);
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

        return status == NativeAppSocketBindBridgeStatus.Success
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
            && entry.LocalEndpoint.Address3 == 0;
    }

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

        return status == NativeAppSocketBindBridgeStatus.PolicyRejected
            && !result.Succeeded
            && result.IsInitialized
            && result.PolicyStatus == NativeAppSocketBindPolicyStatus.PortMismatch
            && result.SocketStatus == OSHostStatus.AccessDenied
            && table.TryGetEntry(create.Socket, out OSHostSocketTableEntry entry) == OSHostStatus.Ok
            && entry.State == OSHostSocketLifecycleState.Created;
    }

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
            return false;
        }

        NativeAppSocketBindBridgeStatus status = NativeAppSocketBindBridge.BindManifestApprovedEndpoint(
            source,
            launch,
            table,
            second.Socket,
            endpoint,
            out NativeAppSocketBindBridgeResult result);

        return status == NativeAppSocketBindBridgeStatus.SocketBindFailed
            && !result.Succeeded
            && result.IsInitialized
            && result.PolicyStatus == NativeAppSocketBindPolicyStatus.Success
            && result.SocketStatus == OSHostStatus.AddressInUse
            && table.TryGetEntry(second.Socket, out OSHostSocketTableEntry entry) == OSHostStatus.Ok
            && entry.State == OSHostSocketLifecycleState.Created;
    }

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

        return status == NativeAppSocketBindBridgeStatus.MissingSocketTable
            && !result.Succeeded
            && result.IsInitialized
            && result.PolicyStatus == NativeAppSocketBindPolicyStatus.Success
            && result.SocketStatus == OSHostStatus.InvalidArgument;
    }

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

        return NativeAppSocketBindBridge.UsesMockSocketTable
            && NativeAppSocketBindBridge.CallsPolicyFirst
            && !NativeAppSocketBindBridge.ExportsPalFunctions
            && !NativeAppSocketBindBridge.OpensLiveSockets
            && !NativeAppSocketBindBridge.StartsKestrel
            && !RuntimeHelpers.IsReferenceOrContainsReferences<NativeAppSocketBindBridgeResult>()
            && status == NativeAppSocketBindBridgeStatus.PolicyRejected
            && result.IsInitialized
            && result.PolicyStatus == NativeAppSocketBindPolicyStatus.InvalidListenHost
            && table.TryGetEntry(create.Socket, out OSHostSocketTableEntry entry) == OSHostStatus.Ok
            && entry.State == OSHostSocketLifecycleState.Created;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        NativeAppSocketBindBridgeResult result = default;

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == NativeAppSocketBindBridgeStatus.Success
            && result.PolicyStatus == NativeAppSocketBindPolicyStatus.Success
            && result.LaunchStatus == NativeAppLaunchStatus.Success
            && result.SocketStatus == OSHostStatus.Ok
            && result.Socket.Value == 0
            && !RuntimeHelpers.IsReferenceOrContainsReferences<NativeAppSocketBindBridgeResult>();
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
