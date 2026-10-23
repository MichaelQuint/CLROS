using System.Buffers.Binary;
using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace OS.Tools.QemuRunner;

/// <summary>
/// Owns one finite, loopback-only QEMU socket-netdev connection. Frames use a four-byte
/// big-endian length followed by 14..1514 Ethernet bytes (without FCS). Call Accept once,
/// then allow at most one read and one write concurrently. Buffers remain borrowed until
/// their operation completes. Cancellation, truncation or invalid framing closes the session;
/// reconnect, framing recovery, jumbo frames and packet scheduling are unsupported.
/// This host transport alone provides no evidence that a guest handled a packet.
/// </summary>
public sealed class QemuEthernetPeer : IDisposable
{
    public const int MinimumFrameBytes = 14;
    public const int MaximumFrameBytes = 1514;
    public const int MaximumTaggedFrameBytes = 1518;
    private readonly int maximumFrameBytes;
    private readonly object gate = new();
    private readonly TcpListener listener;
    private readonly CancellationTokenSource lifetime;
    private readonly CancellationToken lifetimeToken;
    private readonly CancellationTokenRegistration expiry;
    private readonly byte[] readHeader = new byte[4], writeHeader = new byte[4];
    private TcpClient? client;
    private NetworkStream? stream;
    private bool accepting, faulted, disposed;
    private int reading, writing;
    private long framesRead, framesWritten;

    /// <summary>Starts an ephemeral IPv4 loopback listener for1ms..5min, including
    /// accept time. The explicit tagged diagnostic additionally admits exactly
    ///1518-byte802.1Q frames; ordinary sessions retain the1514-byte bound. Only
    /// explicit continuing-service sessions extend the lifetime to18min.</summary>
    public QemuEthernetPeer(TimeSpan maximumLifetime, bool allowTaggedDiagnostic = false, bool continuingService = false)
    {
        if (maximumLifetime < TimeSpan.FromMilliseconds(1) || maximumLifetime > TimeSpan.FromMinutes(continuingService ? 18 : 5))
            throw new ArgumentOutOfRangeException(nameof(maximumLifetime));
        maximumFrameBytes = allowTaggedDiagnostic ? MaximumTaggedFrameBytes : MaximumFrameBytes;
        listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start(1);
        Port = ((IPEndPoint)listener.LocalEndpoint).Port;
        lifetime = new CancellationTokenSource();
        lifetimeToken = lifetime.Token;
        expiry = lifetimeToken.Register(static state => ((QemuEthernetPeer)state!).Fail(), this);
        lifetime.CancelAfter(maximumLifetime);
    }

    public int Port { get; }
    public string NetdevArgument => "socket,id=n0,connect=127.0.0.1:" + Port.ToString(CultureInfo.InvariantCulture);
    public long FramesRead => Interlocked.Read(ref framesRead);
    public long FramesWritten => Interlocked.Read(ref framesWritten);

    /// <summary>
    /// Accepts the sole connection and stops listening. Cancellation or failure retires the
    /// entire session; a second accept is an invalid operation. Dispose interrupts a pending accept.
    /// </summary>
    public async Task AcceptConnectionAsync(CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            ThrowIfClosed();
            if (accepting) throw new InvalidOperationException("The sole accept has already started.");
            accepting = true;
        }
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetimeToken);
        TcpClient? accepted = null;
        try
        {
            accepted = await listener.AcceptTcpClientAsync(linked.Token).ConfigureAwait(false);
            accepted.NoDelay = true;
            lock (gate)
            {
                ThrowIfClosed();
                linked.Token.ThrowIfCancellationRequested();
                stream = accepted.GetStream();
                client = accepted;
                accepted = null;
                listener.Stop();
            }
        }
        catch
        {
            Fail();
            linked.Token.ThrowIfCancellationRequested();
            throw;
        }
        finally { accepted?.Dispose(); }
    }

    /// <summary>
    /// Reads exactly one frame into a buffer of at least MaximumFrameBytes, or
    /// MaximumTaggedFrameBytes in the explicit tagged diagnostic. Invalid buffer
    /// capacity or overlapping reads consume nothing. An incomplete frame closes both directions.
    /// The returned count and counter describe complete frames only.
    /// </summary>
    public async ValueTask<int> ReadFrameAsync(Memory<byte> destination, CancellationToken cancellationToken = default)
    {
        if (destination.Length < maximumFrameBytes) throw new ArgumentException("A full Ethernet frame buffer is required.", nameof(destination));
        if (Interlocked.CompareExchange(ref reading, 1, 0) != 0) throw new InvalidOperationException("A frame read is already pending.");
        try
        {
            NetworkStream connection = ConnectedStream();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetimeToken);
            try
            {
                await connection.ReadExactlyAsync(readHeader, linked.Token).ConfigureAwait(false);
                uint count = BinaryPrimitives.ReadUInt32BigEndian(readHeader);
                if (count < MinimumFrameBytes || count > maximumFrameBytes) throw new InvalidDataException("Invalid Ethernet frame length.");
                await connection.ReadExactlyAsync(destination[..(int)count], linked.Token).ConfigureAwait(false);
                if (!ValidExtendedFrame(destination.Span[..(int)count])) throw new InvalidDataException("Invalid diagnostic VLAN frame.");
                Interlocked.Increment(ref framesRead);
                return (int)count;
            }
            catch
            {
                Fail();
                linked.Token.ThrowIfCancellationRequested();
                throw;
            }
        }
        finally { Volatile.Write(ref reading, 0); }
    }

    /// <summary>
    /// Writes one complete frame with its length prefix. Invalid sizes or overlapping writes
    /// consume nothing. Cancellation/failure after partial output closes the session; do not retry
    /// that frame on this connection. The caller must retain the frame unchanged until completion.
    /// </summary>
    public async ValueTask WriteFrameAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken = default)
    {
        if (frame.Length < MinimumFrameBytes || frame.Length > maximumFrameBytes || !ValidExtendedFrame(frame.Span)) throw new ArgumentOutOfRangeException(nameof(frame));
        if (Interlocked.CompareExchange(ref writing, 1, 0) != 0) throw new InvalidOperationException("A frame write is already pending.");
        try
        {
            NetworkStream connection = ConnectedStream();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetimeToken);
            try
            {
                BinaryPrimitives.WriteUInt32BigEndian(writeHeader, (uint)frame.Length);
                await connection.WriteAsync(writeHeader, linked.Token).ConfigureAwait(false);
                await connection.WriteAsync(frame, linked.Token).ConfigureAwait(false);
                Interlocked.Increment(ref framesWritten);
            }
            catch
            {
                Fail();
                linked.Token.ThrowIfCancellationRequested();
                throw;
            }
        }
        finally { Volatile.Write(ref writing, 0); }
    }

    /// <summary>Closes listener/connection and interrupts pending I/O. Repeated disposal is harmless.</summary>
    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            listener.Stop();
            client?.Dispose();
        }
        expiry.Dispose();
        lifetime.Dispose();
    }

    private NetworkStream ConnectedStream()
    {
        lock (gate)
        {
            ThrowIfClosed();
            return stream ?? throw new InvalidOperationException("Accept the connection before transferring frames.");
        }
    }

    private static bool ValidExtendedFrame(ReadOnlySpan<byte> frame) => frame.Length <= MaximumFrameBytes ||
        frame.Length == MaximumTaggedFrameBytes && BinaryPrimitives.ReadUInt16BigEndian(frame[12..]) == 0x8100;

    private void ThrowIfClosed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (faulted) throw new IOException("The Ethernet peer session has ended.");
    }

    private void Fail()
    {
        lock (gate)
        {
            if (disposed || faulted) return;
            faulted = true;
            listener.Stop();
            client?.Dispose();
        }
    }
}
