using System.Text;
using OS.Tools.QemuRunner;

internal static class QemuStartupDeadlineTests
{
    [HostTest("qemu.startup.deadline.silent_preparation")]
    private static void SilentPreparation()
    {
        var markers = new SerialMarkerStateMachine([QemuStartupDeadline.ReadyMarker], []);
        var deadline = new QemuStartupDeadline(30);
        markers.Feed(Encoding.ASCII.GetBytes("OS_SMOKE_PASS\r\nTEST_START m03.007\r\nM03_EBS_RETURNED_SUCCESS"));
        HostCheck.True(!deadline.HasExpired(29999, markers));
        HostCheck.True(deadline.HasExpired(30000, markers));
        markers.Feed("\r\n"u8);
        HostCheck.True(markers.HasSeenRequiredMarker(QemuStartupDeadline.ReadyMarker));
        HostCheck.True(deadline.HasExpired(30001, markers));
    }

    [HostTest("qemu.startup.deadline.workload_budget")]
    private static void WorkloadBudget()
    {
        var markers = new SerialMarkerStateMachine([QemuStartupDeadline.ReadyMarker, "TEST_PASS workload"], []);
        var deadline = new QemuStartupDeadline(30);
        markers.Feed("M03_EBS_RETURNED_SUCCESS\r\n"u8);
        HostCheck.True(!deadline.HasExpired(2800, markers));
        HostCheck.True(!deadline.HasExpired(599999, markers));
        HostCheck.True(!markers.HasSeenRequiredMarker("TEST_PASS workload"));
        var late = new QemuStartupDeadline(30);
        HostCheck.True(late.HasExpired(30000, markers));
    }

    [HostTest("qemu.startup.deadline.options")]
    private static void Options()
    {
        HostCheck.Equal(30, OS.Tooling.QemuSmokeOptions.Parse().StartupTimeoutSeconds);
        HostCheck.Equal(45, OS.Tooling.QemuSmokeOptions.Parse("-StartupTimeoutSeconds", "45").StartupTimeoutSeconds);
        HostCheck.Equal(2, OS.Tooling.QemuSmokeOptions.Parse("--startup-timeout", "2").StartupTimeoutSeconds);
        foreach (string invalid in new[] { "0", "-1", "garbage" })
        {
            bool rejected = false;
            try { _ = OS.Tooling.QemuSmokeOptions.Parse("--startup-timeout", invalid); }
            catch (Exception e) when (e is ArgumentException or FormatException or OverflowException) { rejected = true; }
            HostCheck.True(rejected);
        }
    }
}
