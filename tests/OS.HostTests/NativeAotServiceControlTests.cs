using System.Text.Json;
using OS.Tooling;
using OS.Tools.QemuRunner;

internal static class NativeAotServiceControlTests
{
    [HostTest("independent full service capture checks pending resets lost reopen isolation literal HTTP and both closes")]
    private static async Task Complete()
    {
        var (control, frames, bytes) = await ServiceSocketControlTests.Exercise("none");
        var result = Inspect(bytes, JsonSerializer.Serialize(control), JsonSerializer.Serialize(frames));
        HostCheck.True(result.PendingResets == 2 && result.Http.Connections == 3 && result.Http.CompleteCloses == 2 &&
            result.Http.ServerActiveCloses == 2 && result.ControlledRecords == control.Observations.Count && result.Ports.Count == 3);
    }

    [HostTest("independent service capture refuses ledger substitutions trailing retries missing packets and false windows")]
    private static async Task Refusals()
    {
        var (control, frames, bytes) = await ServiceSocketControlTests.Exercise("none");
        string good = JsonSerializer.Serialize(control), writer = JsonSerializer.Serialize(frames);
        foreach (var changed in new[] { control with { FirstSynAckOrdinal = control.FirstSynAckOrdinal + 1 },
            control with { SecondProbeOrdinal = control.FirstProbeOrdinal }, control with { FirstClientSequence = 42 },
            control with { Observations = control.Observations.SkipLast(1).ToArray() },
            control with { Persist = control.Persist with { ResponseHex = control.Healthy.ResponseHex + "00" } },
            control with { Healthy = control.Healthy with { StartedUtc = control.Healthy.StartedUtc.AddHours(-2) } } })
            Reject(() => Inspect(bytes, JsonSerializer.Serialize(changed), writer));
        foreach(var unread in new[] {control.Unread with {ResetOrdinal=control.Unread.ResetOrdinal+1},
            control.Unread with {LateOrdinal=control.Unread.ResetOrdinal},
            control.Unread with {CloseRow=control.Unread.CloseRow.Replace("unread=0","unread=1",StringComparison.Ordinal)},
            control.Unread with {Http=control.Unread.Http with {ResponseHex=control.Unread.Http.ResponseHex+"00"}}})
            Reject(()=>Inspect(bytes,JsonSerializer.Serialize(control with {Unread=unread}),writer));
        foreach (var changed in new[] { frames with { ControlledPorts = frames.ControlledPorts.Reverse().ToArray() },
            frames with { Session = frames.Session with { GuestExitExpected = false } },
            frames with { Injections = frames.Injections.Where(row => row.Forwarded).ToArray() },
            frames with { Injections = [..frames.Injections.Where(row => row.Forwarded),frames.Injections.Single(row=>!row.Forwarded) with
                { AfterBackendOrdinal=frames.Injections[^1].AfterBackendOrdinal }] },
            frames with { Injections = frames.Injections.Where(row => row.Tag!="persist.zero.ack").ToArray() },
            frames with { Injections = frames.Injections.Where(row => row.Tag!="persist.reopen.fresh").ToArray() },
            frames with { Injections = frames.Injections.Select(row => row.Tag == "persist.reopen.lost" ? row with { Forwarded = true } : row).ToArray() } })
            Reject(() => Inspect(bytes, good, JsonSerializer.Serialize(changed)));
        // Change the original wire reset and its copied observation together.
        // This reaches the independent sequence/ACK check rather than merely
        // detecting an inconsistent driver ledger.
        var reset=control.Observations.Single(row=>row.Ordinal==control.Unread.ResetOrdinal);
        byte[] resetBytes=Convert.FromHexString(reset.Hex);
        uint sequence=System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(resetBytes.AsSpan(38));
        uint ack=System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(resetBytes.AsSpan(42));
        foreach(var wrong in new[]{(Sequence:unchecked(sequence+1),Ack:ack),(Sequence:sequence,Ack:unchecked(ack+1))})
        {
            byte[] replacement=NativeAotSocketIntegrationTests.Tcp(true,(ushort)control.PendingPort,wrong.Sequence,wrong.Ack,0x14,[]);
            Array.Resize(ref replacement,resetBytes.Length);
            byte[] changed=bytes.ToArray();int offset=24;
            for(long ordinal=1;ordinal<=reset.Ordinal;ordinal++)
            {
                int length=checked((int)System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(changed.AsSpan(offset+8)));
                if(ordinal==reset.Ordinal){HostCheck.Equal(length,replacement.Length);replacement.CopyTo(changed,offset+16);}
                offset+=16+length;
            }
            var copied=control with {Observations=control.Observations.Select(row=>row.Ordinal==reset.Ordinal?
                row with {Hex=Convert.ToHexString(replacement)}:row).ToArray()};
            Reject(()=>Inspect(changed,JsonSerializer.Serialize(copied),writer));
        }
        Reject(() => Inspect(bytes, "{\"Schema\":\"other\"," + good[1..], writer));
        Reject(() => Inspect(bytes, good[..^1] + ",\"Unknown\":1}", writer));
        Reject(() => Inspect(bytes, good, writer.Replace("\"Stopped\":true", "\"Stopped\":null", StringComparison.Ordinal)));
        Reject(() => Inspect(bytes[..^1], good, writer));
        // A late retry is independently visible even when absent from the driver ledger.
        byte[] retry = NativeAotSocketIntegrationTests.Tcp(true, (ushort)control.PendingPort, 123, control.SecondClientSequence + 1, 0x12, []);
        byte[] record = NativeAotSocketIntegrationTests.Capture([retry])[24..];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(4), 999999);
        Reject(() => Inspect([..bytes, ..record], good, writer));
    }

    private static QemuTranscripts.ServiceControlInspection Inspect(byte[] bytes, string control, string frames)
    {
        var fixture = NativeAotSocketIntegrationTests.Packets();
        return QemuTranscripts.InspectNativeAotServiceControlsForTests(bytes, fixture.Anchor, fixture.Arp, control, frames,
            DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(1));
    }
    private static void Reject(Action action)
    {
        bool refused = false; try { action(); } catch (Exception error) when (error is ToolFailure or InvalidDataException) { refused = true; }
        HostCheck.True(refused);
    }
}
