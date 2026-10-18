using OS.Tools.QemuRunner;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Observed bad-peer transport identity and actual timeout/retire
    /// clocks, with the healthy peer live at both control events. Values grant
    /// no descriptor, scheduler, packet, process, native or guest authority.</summary>
    public readonly record struct WebSocketControlInspection(int Connections,ulong Slot,ulong Owner,ulong Epoch,
        ulong Generation,ulong Index,uint RemotePort,ulong Created,ulong Dropped,ulong TimedOut,ulong Retired);

    /// <summary>Inspect borrowed transport rows against explicit control
    /// expectations. Require exact live handle/tuple/generation, isolated drop,
    /// close timeout, later private retirement and healthy overlap. Existing
    /// generation/TIME-WAIT guards apply. Malformed/incomplete rows throw; no
    /// input is mutated or retained and no guest execution is established.</summary>
    public static WebSocketControlInspection InspectNativeAotWebTransportForTests(string[] rows,WebControlInspection controls,bool logQuota=false)
    {
        Need(rows is not null && rows.Length is >0 and <=100000 && controls is not null,"web_transport_inputs");
        var all=SocketLifetimes(rows!,true,logQuota?7:8,controls!.Sockets);
        return WebTransportDetails(all,controls.Sockets);
    }

    private static WebSocketControlInspection WebTransportDetails(List<SocketLifetime> all,WebSocketControls.Report controls)
    {
        var bad=all.Single(l=>l.RemotePort==controls.Deadline.SourcePort);var r=bad.First;
        return new(all.Count,r["slot"],r["owner"],r["epoch"],r["generation"],r["index"],bad.RemotePort,
            r["clock"],controls.Observations.DropClock,bad.Timeout,bad.Retired);
    }

    private static bool IsWebControlEvent(string row)=>row.StartsWith("M07_027_TIMEOUT",StringComparison.Ordinal) ||
        row.StartsWith("M07_028_DROP",StringComparison.Ordinal);

    private static Dictionary<string,ulong> WebControlEvent(string row)=>row.StartsWith("M07_027_TIMEOUT",StringComparison.Ordinal)?
        ReadRecord(row,"M07_027_TIMEOUT",[..SocketLifetimeFields[..10],"sharedFailed:dec"]):
        ReadRecord(row,"M07_028_DROP","before:dec","after:dec","availableBefore:dec","availableAfter:dec","delivered:dec","clock:dec","running:dec");

    private static void RequireWebControlEvents(WebSocketControls.Report controls)
    {
        try
        {
            var events=WebSocketControls.ObserveEvents([controls.Observations.DropRow,controls.Observations.TimeoutRow],controls.Deadline.SourcePort);
            Need(events.HasValue && events.Value==controls.Observations && controls.Before.SourcePort==controls.After.SourcePort &&
                controls.Before.SourcePort!=controls.Deadline.SourcePort,"web_transport_expected_events");
        }
        catch(InvalidDataException){throw new ToolFailure("web_transport_expected_events");}
    }
}
