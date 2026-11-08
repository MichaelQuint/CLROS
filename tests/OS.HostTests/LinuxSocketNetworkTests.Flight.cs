using System.Runtime.InteropServices;
using OS.Net;
using Network = OS.Kernel.Compatibility.LinuxX64.LinuxSocketNetwork;
using Tx = OS.Net.TcpSendWindow;
using Wire = TcpReceiveWindowTests;

internal static unsafe partial class LinuxSocketNetworkTests
{
    private static Tx.Owner* FlightSender(Model m, uint index)
        => (Tx.Owner*)((byte*)(m.Slots + index) + (int)Marshal.OffsetOf<Network.Slot>("Sender"));

    private static void WarmFlight(Model m, uint index, ushort port)
    {
        Tx.Owner* sender = FlightSender(m, index);
        int mss = Math.Min(sender->PeerMaximumSegmentSize, (ushort)1460);
        byte[] bytes = new byte[mss];
        HostCheck.Equal(Tx.Status.WindowUpdated, Tx.Acknowledge(sender,
            Wire.Packet(100, ReadOnlySpan<byte>.Empty, sender->NextSequence, port, flags: 0x10, window: ushort.MaxValue), out _));
        for (int i = 0; i < 7; i++)
        {
            HostCheck.Equal(Tx.Status.Success, Tx.Queue(sender, bytes, out _));
            HostCheck.Equal(Tx.Status.Success, Tx.Pump(sender, m.Frame, out _));
            HostCheck.Equal(Tx.Status.Success, Tx.Acknowledge(sender,
                Wire.Packet(100, ReadOnlySpan<byte>.Empty, sender->NextSequence, port, flags: 0x10, window: ushort.MaxValue), out _));
        }
        HostCheck.Equal((uint)(8 * mss), sender->CongestionWindowBytes);
    }

    [HostTest("socket network lone bounded connection fills eight segments in one pump without waiting for ACK")]
    private static void FlightLoneConnection()
    {
        using var m = new Model(count: 1);
        HostCheck.True(Network.EnableBoundedFlight(m.State));
        uint listener = m.Listen();
        m.Connect();
        uint fd = m.Accept(listener);
        HostCheck.True(!Network.EnableBoundedFlight(m.State));
        WarmFlight(m, 0, 40000);
        new Span<byte>(m.Files.Memory.At(User), 32768).Fill(0x41);
        for (uint queued = 0; queued < 32768;)
        {
            long accepted = m.Call(44, fd, User + queued, 32768 - queued);
            HostCheck.True(accepted > 0 && accepted <= 8192);
            queued += (uint)accepted;
        }
        uint calls = m.Hardware->Calls;
        m.Pump();
        Tx.Owner* sender = FlightSender(m, 0);
        HostCheck.Equal(8U, m.Hardware->Calls - calls);
        HostCheck.Equal((byte)8, sender->OutstandingSegments);
        HostCheck.Equal(8 * Math.Min(sender->PeerMaximumSegmentSize, (ushort)1460), sender->InFlightBytes);
        HostCheck.Equal(32768, sender->BufferedBytes);
        m.Finish();
    }

    [HostTest("socket network bounded flight services competing connections round robin under one eight frame budget")]
    private static void FlightRoundRobin()
    {
        using var m = new Model(count: 2);
        HostCheck.True(Network.EnableBoundedFlight(m.State));
        uint listener = m.Listen();
        for (uint i = 0; i < 2; i++)
        {
            ushort port = (ushort)(40000 + i);
            m.Connect(port);
            m.Accept(listener);
            WarmFlight(m, i, port);
            HostCheck.Equal(Tx.Status.Success, Tx.Queue(FlightSender(m, i), new byte[32768], out _));
        }
        uint calls = m.Hardware->Calls;
        m.Pump();
        HostCheck.Equal(8U, m.Hardware->Calls - calls);
        for (uint i = 0; i < 2; i++)
            HostCheck.Equal((byte)4, FlightSender(m, i)->OutstandingSegments);
        calls = m.Hardware->Calls;
        m.Pump();
        HostCheck.Equal(8U, m.Hardware->Calls - calls);
        for (uint i = 0; i < 2; i++)
            HostCheck.Equal((byte)8, FlightSender(m, i)->OutstandingSegments);
        m.Finish();
    }

    [HostTest("socket network new zero window probe octets share the eight frame budget and later connections receive service")]
    private static void FlightPersistBudget()
    {
        using var m = new Model(count: 10, endpointCount: 16);
        HostCheck.True(Network.EnableBoundedFlight(m.State));
        uint listener = m.Listen();
        for (uint i = 0; i < 10; i++)
        {
            ushort port = (ushort)(40000 + i);
            m.Connect(port);
            m.Accept(listener);
            Tx.Owner* sender = FlightSender(m, i);
            HostCheck.Equal(Tx.Status.WindowUpdated, Tx.Acknowledge(sender,
                Wire.Packet(100, ReadOnlySpan<byte>.Empty, sender->NextSequence, port, flags: 0x10, window: 0), out _));
            HostCheck.Equal(Tx.Status.Success, Tx.Queue(sender, "x"u8, out _));
        }
        m.Pump();
        uint calls = m.Hardware->Calls;
        ulong due = FlightSender(m, 0)->PersistDeadlineNanoseconds;
        m.Pump(due);
        HostCheck.Equal(8U, m.Hardware->Calls - calls);
        HostCheck.Equal(8, Enumerable.Range(0, 10).Count(i => FlightSender(m, (uint)i)->InFlightBytes == 1));
        calls = m.Hardware->Calls;
        m.Pump();
        HostCheck.Equal(2U, m.Hardware->Calls - calls);
        for (uint i = 0; i < 10; i++)
        {
            HostCheck.Equal(1, FlightSender(m, i)->InFlightBytes);
            HostCheck.Equal((byte)1, FlightSender(m, i)->OutstandingSegments);
            HostCheck.Equal((byte)0, FlightSender(m, i)->Retransmissions);
        }
        m.Finish();
    }
}
