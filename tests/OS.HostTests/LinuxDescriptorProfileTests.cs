using System.Runtime.InteropServices;
using OS.Boot;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Diagnostics;
using OS.Tooling;

internal static unsafe partial class LinuxDescriptorProfileTests
{
    [HostTest("m16-007 default-off catalog boot authority and complete explicit-false prerequisite controls", HostLane.Fast, 0)]
    private static void DefaultAndParents()
    {
        HostCheck.True(!Probes.EnableM16007Probe && !Probes.GetM16007Probe().EnabledByDefault);
        HostCheck.True(!BootSequence.PrepareLinuxDescriptors(true, null, null) && !BootSequence.RunOwnedLinuxDescriptors(true, true, true, null, null));
        string[] parents = ["M16006", "M16005", "M16004", "M16003", "M16002", "M01LoadedImageStack", "M01LiveAllocator", "AcpiPhysicalDiscovery", "M03FirstRealExitBootServices", "M03OwnedSubsystemSurvival"];
        foreach (bool build in new[]
        {
            false,
            true
        }

        )
        {
            var profile = EfiOptions.Parse(["--EnableM16007Probe"], build).Profile();
            HostCheck.True(profile.BootPolicy == "Diagnostic" && profile.IsEnabled("M16007") && parents.All(profile.IsEnabled));
            HostCheck.Equal(parents.Length + 1, profile.Flags.Count(pair => pair.Value));
            foreach (string parent in parents)
                HostCheck.True(Rejected(() => EfiOptions.Parse(["--EnableM16007Probe", "--Enable" + parent + "Probe:false"], build).Profile()));
            HostCheck.True(Rejected(() => EfiOptions.Parse(["--BootPolicy", "VerifiedExit", "--EnableM16007Probe"], build).Profile()));
            HostCheck.True(Rejected(() => EfiOptions.Parse(["--EnableM16007Probe", "--EnableM05PhysicalMappingProbe"], build).Profile()));
        }

        var plan = QemuProfiles.Create(QemuSmokeOptions.Parse(["--DryRun", "--BuildEfi", "--EnableM16007Probe"]));
        HostCheck.True(plan.IsEnabled("EnableM16007Probe") && plan.Required.Contains("M16_007_POLICY") && plan.Required.Contains("CLROS_M16_007_READY") && !plan.Required.Contains("TEST_PASS m16-006"));
        HostCheck.True(Rejected(() => QemuTranscripts.AssertM03AcpiBootstrapTranscript("missing", linuxDescriptors: true)));
    }

    [UnmanagedCallersOnly]
    private static byte Permissions(void* context, ulong page) => page == 0x400000 ? ((Model*)context)->RemoveReturn ? (byte)0 : (byte)5 : page == 0x70000000 ? (byte)3 : (byte)0;
    [UnmanagedCallersOnly]
    private static byte Copy(void* context, ulong address, byte* output, uint bytes) => 0;
    [UnmanagedCallersOnly]
    private static byte Sink(void* context, uint fd, byte* input, uint bytes) => 0;
    [UnmanagedCallersOnly]
    private static long Startup(void* context, LinuxSyscallFrame* frame)
    {
        ((Model*)context)->StartupCalls++;
        return 123;
    }

    [UnmanagedCallersOnly]
    private static long Descriptors(void* context, LinuxSyscallFrame* frame)
    {
        ((Model*)context)->DescriptorCalls++;
        return ((Model*)context)->Value;
    }

    [HostTest("m16-007 gateway explicitly admits fd routing while retaining exit call bounds and return audit", HostLane.Fast, 1)]
    private static void GatewayRouting()
    {
        byte* scratch = stackalloc byte[64];
        Model model = new()
        {
            Value = 4
        };
        var admission = new LinuxSyscallAdmission
        {
            UserRoot = 0x200000,
            MaximumCalls = 4,
            MaximumWrite = 64,
            Scratch = scratch,
            ScratchBytes = 64,
            FirmwareExited = true,
            OwnedBoundaryValidated = true
        };
        var access = new LinuxSyscallAccess
        {
            Context = &model,
            PageAccess = &Permissions,
            CopyFromUser = &Copy,
            Write = &Sink,
            Startup = &Startup,
            Descriptors = &Descriptors
        };
        LinuxSyscallGateState invalid = default;
        HostCheck.Equal(LinuxSyscallGateStatus.InvalidAdmission, LinuxSyscallGateway.Begin(ref invalid, admission, access));
        admission.EnableDescriptors = true;
        HostCheck.Equal(LinuxSyscallGateStatus.InvalidAdmission, LinuxSyscallGateway.Begin(ref invalid, admission, access));
        admission.EnableStartup = true;
        LinuxSyscallGateState state = default;
        HostCheck.Equal(LinuxSyscallGateStatus.Active, LinuxSyscallGateway.Begin(ref state, admission, access));
        var frame = new LinuxSyscallFrame
        {
            Number = 20,
            Rip = 0x400200,
            Rsp = 0x70001000,
            Rflags = 2
        };
        HostCheck.Equal(4L, LinuxSyscallGateway.Dispatch(ref state, frame).Value);
        HostCheck.Equal(4U, state.BytesWritten);
        HostCheck.Equal(1U, model.DescriptorCalls);
        HostCheck.Equal(0U, model.StartupCalls);
        frame.Number = 9;
        HostCheck.Equal(123L, LinuxSyscallGateway.Dispatch(ref state, frame).Value);
        HostCheck.Equal(1U, model.StartupCalls);
        frame.Number = 293;
        model.Value = -11;
        HostCheck.Equal(-11L, LinuxSyscallGateway.Dispatch(ref state, frame).Value);
        HostCheck.Equal(4U, state.BytesWritten);
        frame.Number = 231;
        frame.Argument0 = 42;
        HostCheck.Equal(42U, LinuxSyscallGateway.Dispatch(ref state, frame).ExitCode);
        HostCheck.Equal(LinuxSyscallGateStatus.Exited, state.Status);
        HostCheck.Equal(LinuxSyscallAction.Rejected, LinuxSyscallGateway.Dispatch(ref state, frame).Action);
        state = default;
        model.RemoveReturn = false;
        HostCheck.Equal(LinuxSyscallGateStatus.Active, LinuxSyscallGateway.Begin(ref state, admission, access));
        model.RemoveReturn = true;
        frame.Number = 0;
        HostCheck.Equal(LinuxSyscallAction.Terminate, LinuxSyscallGateway.Dispatch(ref state, frame).Action);
        HostCheck.Equal(LinuxSyscallGateStatus.InvalidReturn, state.Status);
    }

    private static bool Rejected(Action action)
    {
        try
        {
            action();
            return false;
        }
        catch (ToolFailure)
        {
            return true;
        }
    }
}
