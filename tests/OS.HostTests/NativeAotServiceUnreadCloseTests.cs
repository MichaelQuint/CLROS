using System.Globalization;
using System.Text.Json;
using OS.Tooling;
using OS.Tools.QemuRunner;

internal static class NativeAotServiceUnreadCloseTests
{
    [HostTest("service unread close joins exact third tuple lifetime retirement and subsequent generation reuse")]
    private static async Task Complete()
    {
        var (control, _, _) = await ServiceSocketControlTests.Exercise("none");
        QemuTranscripts.InspectNativeAotServiceUnreadOwnerForTests(Rows(control), JsonSerializer.Serialize(control));
    }

    [HostTest("service unread owner refuses missing substituted duplicate reordered and unreaped lifetime evidence")]
    private static async Task Refusals()
    {
        var (control, _, _) = await ServiceSocketControlTests.Exercise("none");
        string[] rows = Rows(control);
        string report = JsonSerializer.Serialize(control);
        foreach (var changed in new[]{rows.Where((_,at)=>at!=5).ToArray(),[..rows,rows[5]],
            rows.Select((row,at)=>at==5?row.Replace("owner=4096","owner=8192",StringComparison.Ordinal):row).ToArray(),
            rows.Where((_,at)=>at!=6).ToArray(),rows.Where((_,at)=>at!=7).ToArray(),
            new[]{rows[0],rows[1],rows[2],rows[3],rows[5],rows[4],rows[6],rows[7]},
            rows.Select((row,at)=>at==6?row.Replace("deadline=0","deadline=1",StringComparison.Ordinal):row).ToArray(),
            rows.Select((row,at)=>at==7?row.Replace("generation=4","generation=5",StringComparison.Ordinal):row).ToArray()})
            Reject(() => QemuTranscripts.InspectNativeAotServiceUnreadOwnerForTests(changed, report));
        Reject(() => QemuTranscripts.InspectNativeAotServiceUnreadOwnerForTests(rows,
            JsonSerializer.Serialize(control with
            {
                Unread = control.Unread with
                {
                    CloseRow = rows[5] + " "
                }
            })));
        // These synthetic rows check only the consumer's ownership joins.
    }

    private static string[] Rows(ServiceSocketControls.Report control)
    {
        var close = control.Unread.CloseRow.Split(' ');
        var values = close.Skip(1).Select(part => part.Split('=')).ToDictionary(pair => pair[0], pair => pair[1]);
        ulong clock = ulong.Parse(values["clock"], CultureInfo.InvariantCulture), generation = ulong.Parse(values["generation"], CultureInfo.InvariantCulture);
        string Slot(int kind, ulong time, ulong gen) => "M16_011_SLOT event=" + kind + " " +
            string.Join(' ', close.Skip(1).Take(10).Select(part => part.StartsWith("clock=", StringComparison.Ordinal) ? "clock=" + time :
                part.StartsWith("generation=", StringComparison.Ordinal) ? "generation=" + gen : part)) + " deadline=0 duration=0";
        return [Slot(0,clock-5,generation-2),Slot(1,clock-4,generation-2),Slot(0,clock-3,generation-1),
            Slot(1,clock-2,generation-1),Slot(0,clock-1,generation),control.Unread.CloseRow,
            Slot(1,clock+1,generation),Slot(0,clock+2,generation+1).Replace("remotePort="+control.PendingPort,
                "remotePort="+control.Persist.Port,StringComparison.Ordinal)];
    }

    private static void Reject(Action action)
    {
        bool refused = false;
        try
        {
            action();
        }
        catch (ToolFailure)
        {
            refused = true;
        }
        HostCheck.True(refused);
    }
}
