using System.Buffers.Binary;
using System.Text.Json;
using OS.Tooling;
using OS.Tools.QemuRunner;

internal static partial class ServiceSocketControlTests
{
    [HostTest("service bounded flight control preserves reordering partial suffix loss and exact HTTP in owned mock IO")]
    private static async Task FlightComplete()
    {
        var (control, frames, bytes) = await Exercise("flight");
        HostCheck.True(control.Schema == "clros.nativeaot.service.controls.v3" && control.Flight is not null && frames.Injections.Count <= 64);
        var fixture = NativeAotSocketIntegrationTests.Packets();
        var inspected = QemuTranscripts.InspectNativeAotServiceControlsForTests(bytes, fixture.Anchor, fixture.Arp,
            JsonSerializer.Serialize(control), JsonSerializer.Serialize(frames), DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(1));
        HostCheck.True(inspected.BoundedFlight && inspected.Http.Connections == 3 && inspected.Http.CompleteCloses == 2);
        // Deliberately synthetic timestamps/control I/O: this checks the driver
        // and independent decoder, never an actual guest or native TCP result.
        foreach (var changed in new[]
        {
            control with { Schema = "clros.nativeaot.service.controls.v2" },
            control with { Flight = null },
            control with { Flight = control.Flight! with { Suffix = control.Flight.Fast } },
            control with { Flight = control.Flight! with { NextSecond = control.Flight.NextFirst } }
        })
        {
            bool refused = false;
            try
            {
                _ = QemuTranscripts.InspectNativeAotServiceControlsForTests(bytes, fixture.Anchor, fixture.Arp,
                    JsonSerializer.Serialize(changed), JsonSerializer.Serialize(frames), DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(1));
            }
            catch (ToolFailure)
            {
                refused = true;
            }
            HostCheck.True(refused);
        }
    }

    [HostTest("service bounded flight control rejects changed original timeout and partial retransmission copies")]
    private static async Task FlightRefusals()
    {
        foreach (string mode in new[] { "flight-timeout", "flight-suffix" })
        {
            bool refused = false;
            try
            {
                _ = await Exercise(mode);
            }
            catch (InvalidDataException error) when (error.Message == (mode == "flight-timeout" ? "control flight timeout copy" : "control flight exact retry"))
            {
                refused = true;
            }
            HostCheck.True(refused);
        }
    }

    [HostTest("independent flight capture rejects an early suffix even when the driver timestamp agrees")]
    private static async Task FlightEarlySuffix()
    {
        var (control, frames, original) = await Exercise("flight");
        byte[] changed = original.ToArray();
        ulong previous = 0, replacement = 0;
        int offset = 24;
        for (long ordinal = 1; offset < changed.Length; ordinal++)
        {
            int length = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(changed.AsSpan(offset + 8)));
            ulong time = (ulong)BinaryPrimitives.ReadUInt32LittleEndian(changed.AsSpan(offset)) * 1_000_000 + BinaryPrimitives.ReadUInt32LittleEndian(changed.AsSpan(offset + 4));
            if (ordinal == control.Flight!.Suffix)
            {
                replacement = previous + 1;
                BinaryPrimitives.WriteUInt32LittleEndian(changed.AsSpan(offset), (uint)(replacement / 1_000_000));
                BinaryPrimitives.WriteUInt32LittleEndian(changed.AsSpan(offset + 4), (uint)(replacement % 1_000_000));
                break;
            }
            previous = time;
            offset += 16 + length;
        }
        HostCheck.True(replacement != 0);
        var copied = control with
        {
            Observations = control.Observations.Select(row => row.Ordinal == control.Flight!.Suffix ? row with { Microseconds = replacement } : row).ToArray()
        };
        var fixture = NativeAotSocketIntegrationTests.Packets();
        string reason = "accepted";
        try
        {
            _ = QemuTranscripts.InspectNativeAotServiceControlsForTests(changed, fixture.Anchor, fixture.Arp,
                JsonSerializer.Serialize(copied), JsonSerializer.Serialize(frames), DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(1));
        }
        catch (ToolFailure error)
        {
            reason = error.Reason;
        }
        HostCheck.Equal("service_flight_suffix", reason);
    }
}
