using System.Buffers.Binary;
using System.Globalization;
using System.Runtime.ExceptionServices;

namespace OS.Tools.QemuRunner;
/// <summary>Owns two finite loopback chardev sessions for a QEMU backend TX
/// filter-redirector. One worker preserves every forwarded frame byte-for-byte.
/// After the selected peer sends data, only its payload-free ACK/FIN/RST frames
/// are withheld. A single explicit VLAN injection shares that worker's writer.
/// Explicit service mode instead isolates three reserved raw-client tuples and
/// serializes their copied injections/loss ledger beside unchanged other traffic.
/// This host fault injector grants no guest timeout, drop or service authority.</summary>

public sealed partial class WebFrameControl : IDisposable
{
    private readonly QemuEthernetPeer backend, guest;
    private readonly CancellationTokenSource stop = new();
    private readonly TaskCompletionSource inject = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource injected = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly List<DroppedFrame> dropped = [];
    private readonly object lifecycle = new();
    private readonly bool continuingService;
    private readonly List<InjectedFrame> injections = [];
    private TaskCompletionSource serviceSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Injection? serviceRequest;
    private int[]? servicePorts;
    private int deadlinePort, armed, injectionRequested, vlanFrames, disposed, guestExitExpected, stopping;
    private long forwarded;
    private volatile Task<int>? pendingRead;
    private volatile Task? pendingWrite, pendingAccept;
    private Exception? priorIoFailure;
    /// <summary>Start exclusive framed I/O for 1 second through 5 minutes.
    /// Explicit continuingService permits up to18min and serialized bounded
    /// raw-frame injection, excluding the legacy deadline/VLAN operations.
    /// Start QEMU with Arguments after construction; no other reader/writer may
    /// use these sessions. Stop must join before accepting any evidence.</summary>

    public WebFrameControl(TimeSpan maximumLifetime, bool continuingService = false)
    {
        if (maximumLifetime < TimeSpan.FromSeconds(1) || maximumLifetime > TimeSpan.FromMinutes(continuingService ? 18 : 5))
            throw new ArgumentOutOfRangeException(nameof(maximumLifetime));
        this.continuingService = continuingService;
        backend = new(maximumLifetime, continuingService: continuingService);
        try
        {
            guest = new(maximumLifetime, allowTaggedDiagnostic: !continuingService, continuingService: continuingService);
        }
        catch
        {
            backend.Dispose();
            stop.Dispose();
            throw;
        }

        string Port(int value) => value.ToString(CultureInfo.InvariantCulture);
        Arguments = Array.AsReadOnly(new[] { "-chardev", "socket,id=clroswebout,host=127.0.0.1,port=" + Port(backend.Port), "-chardev", "socket,id=clroswebin,host=127.0.0.1,port=" + Port(guest.Port), "-object", "filter-redirector,id=clroswebfilter,netdev=n0,queue=tx,indev=clroswebin,outdev=clroswebout" });
        Completion = RunAsync();
    }

    public int QemuOutPort => backend.Port;
    public int QemuInPort => guest.Port;
    public IReadOnlyList<string> Arguments
    {
        get;
    }
    public Task Completion
    {
        get;
    }
    public int DeadlinePort => Volatile.Read(ref deadlinePort);
    public long ForwardedFrames => Interlocked.Read(ref forwarded);
    public int VlanFrames => Volatile.Read(ref vlanFrames);

    public IReadOnlyList<DroppedFrame> DroppedFrames
    {
        get
        {
            lock (dropped)
                return Array.AsReadOnly(dropped.ToArray());
        }
    }

    /// <summary>Select one actual loopback client's port before its request.
    /// The handshake remains intact; withholding begins after its first data
    /// frame. A second selection or retired owner throws without changing it.</summary>

    public void SelectDeadlineClient(int port)
    {
        if (continuingService)
            throw new InvalidOperationException("Service frame owner excludes legacy deadline control.");
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        if (Volatile.Read(ref stopping) != 0 || Completion.IsCompleted)
            throw new InvalidOperationException("Frame owner retired.");
        if (port is < 1024 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(port));
        if (Interlocked.CompareExchange(ref deadlinePort, port, 0) != 0)
            throw new InvalidOperationException("Deadline peer already selected.");
    }

    /// <summary>Request exactly one 1518-byte 802.1Q frame. Completion means a
    /// complete framed write, never a guest drop. The owner lifetime bounds the
    /// wait; callers must also retain and observe Completion, then Stop.</summary>

    public Task InjectRejectedFrameAsync()
    {
        if (continuingService)
            throw new InvalidOperationException("Service frame owner excludes legacy VLAN control.");
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        if (Volatile.Read(ref stopping) != 0 || Completion.IsCompleted)
            throw new InvalidOperationException("Frame owner retired.");
        if (Interlocked.CompareExchange(ref injectionRequested, 1, 0) != 0)
            throw new InvalidOperationException("VLAN already requested.");
        inject.SetResult();
        return injected.Task;
    }

    /// <summary>Reserve exactly three distinct raw diagnostic client tuples.
    /// Drop backend-generated TCP on those ports (including slirp's automatic
    /// resets for injected raw clients) and retain its literal omission ledger.
    /// Caller must reserve the host ports to exclude ordinary peer collisions.
    /// Select once before injection. This isolation proves no guest behavior.</summary>

    public void SelectServiceClients(int pendingPort, int persistPort, int healthyPort)
    {
        if (!continuingService)
            throw new InvalidOperationException("Service frame owner required.");
        int[] ports = [pendingPort, persistPort, healthyPort];
        if (ports.Any(port => port is < 1024 or > 65535) || ports.Distinct().Count() != 3)
            throw new ArgumentException("Invalid service ports.");
        lock (lifecycle)
        {
            ObjectDisposedException.ThrowIf(disposed != 0, this);
            if (stopping != 0 || Completion.IsCompleted || servicePorts is not null)
                throw new InvalidOperationException("Service ports already selected or retired.");
            Volatile.Write(ref servicePorts, ports);
        }
    }

    /// <summary>Copy one54..1514-byte selected TCP diagnostic frame and serialize it beside
    /// unchanged backend traffic. Only service owners admit this operation;
    /// tags are unique printable identifiers, at most64 requests and one pending
    /// request. forward=false records a deliberate lost injection. Rejection
    /// queues nothing. Caller must own/stop/join this owner on cancellation;
    /// failure after a partial write closes the session and cannot be retried.</summary>

    public Task InjectServiceFrameAsync(ReadOnlyMemory<byte> frame, string tag, bool forward = true)
    {
        if (!continuingService)
            throw new InvalidOperationException("Service frame owner required.");
        if (frame.Length is < 54 or > 1514 || string.IsNullOrEmpty(tag) || tag.Length > 48 || !tag.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_'))
            throw new ArgumentException("Invalid service injection.");
        lock (lifecycle)
        {
            ObjectDisposedException.ThrowIf(disposed != 0, this);
            if (stopping != 0 || Completion.IsCompleted)
                throw new InvalidOperationException("Frame owner retired.");
            if (!IsServiceTuple(frame.Span))
                throw new ArgumentException("Unselected service tuple.");
            if (serviceRequest is not null || injections.Count >= 64 || injections.Any(row => row.Tag == tag))
                throw new InvalidOperationException("Service injection already pending or exhausted.");
            serviceRequest = new(frame.ToArray(), tag, forward, new(TaskCreationOptions.RunContinuationsAsynchronously));
            serviceSignal.SetResult();
            return serviceRequest.Completion.Task;
        }
    }

    /// <summary>Stop/join and snapshot literal service writes/losses beside the
    /// unchanged transport report. Healthy describes host I/O only.</summary>

    public ServiceReport FinishService()
    {
        if (!continuingService)
            throw new InvalidOperationException("Service frame owner required.");
        Report session = Finish();
        lock (lifecycle)
            return new(session, Array.AsReadOnly((servicePorts ?? []).ToArray()), Array.AsReadOnly(injections.ToArray()));
    }

    private async Task RunAsync()
    {
        try
        {
            pendingAccept = Task.WhenAll(backend.AcceptConnectionAsync(stop.Token), guest.AcceptConnectionAsync(stop.Token));
            await pendingAccept.ConfigureAwait(false);
            pendingAccept = null;
            byte[] bytes = new byte[1518];
            long ordinal = 0;
            for (; ; )
            {
                pendingRead ??= backend.ReadFrameAsync(bytes, stop.Token).AsTask();
                if (continuingService)
                {
                    Task signal;
                    lock (lifecycle)
                        signal = serviceSignal.Task;
                    if (signal.IsCompleted || await Task.WhenAny(pendingRead, signal).ConfigureAwait(false) == signal)
                    {
                        Injection request;
                        lock (lifecycle)
                            request = serviceRequest ?? throw new InvalidOperationException("Missing service injection.");
                        stop.Token.ThrowIfCancellationRequested();
                        if (request.Forwarded)
                        {
                            pendingWrite = guest.WriteFrameAsync(request.Bytes, stop.Token).AsTask();
                            await pendingWrite.ConfigureAwait(false);
                            pendingWrite = null;
                        }

                        lock (lifecycle)
                        {
                            injections.Add(new(ordinal, request.Tag, Convert.ToHexString(request.Bytes), request.Forwarded));
                            serviceRequest = null;
                            serviceSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);
                            request.Completion.TrySetResult();
                        }

                        continue;
                    }
                }
                else if (VlanFrames == 0 && await Task.WhenAny(pendingRead, inject.Task).ConfigureAwait(false) == inject.Task)
                {
                    byte[] vlan = new byte[1518];
                    Convert.FromHexString("020000000001020000000002810000010800").CopyTo(vlan, 0);
                    pendingWrite = guest.WriteFrameAsync(vlan, stop.Token).AsTask();
                    await pendingWrite.ConfigureAwait(false);
                    pendingWrite = null;
                    Volatile.Write(ref vlanFrames, 1);
                    injected.TrySetResult();
                    continue;
                }

                int length = await pendingRead.ConfigureAwait(false);
                pendingRead = null;
                if (++ordinal > 100_000)
                    throw new InvalidDataException("Web frame budget exceeded.");
                if (Withhold(bytes.AsSpan(0, length)))
                {
                    lock (dropped)
                    {
                        if (dropped.Count == 128)
                            throw new InvalidDataException("Web withheld-frame budget exceeded.");
                        dropped.Add(new(ordinal, Convert.ToHexString(bytes.AsSpan(0, length))));
                    }
                }
                else
                {
                    pendingWrite = guest.WriteFrameAsync(bytes.AsMemory(0, length), stop.Token).AsTask();
                    await pendingWrite.ConfigureAwait(false);
                    pendingWrite = null;
                    Interlocked.Increment(ref forwarded);
                }
            }
        }
        catch (Exception failure) when (ExpectedClosure(failure))
        {
            injected.TrySetCanceled(stop.Token);
        }
        catch (Exception failure)
        {
            Exception? earlier = Volatile.Read(ref priorIoFailure);
            injected.TrySetException(earlier ?? failure);
            if (earlier is not null)
                ExceptionDispatchInfo.Capture(earlier).Throw();
            throw;
        }
        finally
        {
            lock (lifecycle)
                serviceRequest?.Completion.TrySetCanceled(stop.Token);
        }
    }

    /// <summary>Declare that the caller is now terminating its owned QEMU child.
    /// Only subsequent I/O closure becomes expected; earlier faults remain on
    /// Completion. The caller must independently confirm child death.</summary>

    public void ExpectGuestExit()
    {
        lock (lifecycle)
        {
            if (stopping != 0)
                throw new InvalidOperationException("Frame owner retired.");
            guestExitExpected = 1;
            PreserveCompletedFailure(); // QEMU termination has not yet been called.
        }
    }

    private bool ExpectedClosure(Exception failure)
    {
        lock (lifecycle)
            return priorIoFailure is null && (stopping != 0 && failure is OperationCanceledException or ObjectDisposedException or IOException || guestExitExpected != 0 && failure is IOException);
    }

    private void PreserveCompletedFailure(bool allowExpectedIo = false)
    {
        foreach (Task? operation in new Task?[]
        {
            pendingRead,
            pendingWrite,
            pendingAccept
        }

        )
            if (operation?.IsFaulted == true)
            {
                Exception failure = operation.Exception!.GetBaseException();
                if (!allowExpectedIo || failure is not IOException)
                    Interlocked.CompareExchange(ref priorIoFailure, failure, null);
            }
            else if (operation?.IsCanceled == true)
                Interlocked.CompareExchange(ref priorIoFailure, new TaskCanceledException(operation), null);
    }

    /// <summary>Stop/join and snapshot immutable frame/argument observations.
    /// Healthy describes this host transport only, without guest authority.</summary>

    public Report Finish()
    {
        bool joined = Stop();
        return new(QemuOutPort, QemuInPort, Arguments, ForwardedFrames, VlanFrames, DroppedFrames, Volatile.Read(ref guestExitExpected) != 0, joined, Completion.IsCompletedSuccessfully);
    }

    private bool Withhold(ReadOnlySpan<byte> frame)
    {
        if (continuingService)
            return IsServiceTuple(frame);
        int port = DeadlinePort;
        if (port == 0 || frame.Length < 54 || BinaryPrimitives.ReadUInt16BigEndian(frame[12..]) != 0x0800 || (frame[14] >> 4) != 4 || frame[23] != 6 || (BinaryPrimitives.ReadUInt16BigEndian(frame[20..]) & 0x3fff) != 0 || BinaryPrimitives.ReadUInt32BigEndian(frame[26..]) != 0x0a000202 || BinaryPrimitives.ReadUInt32BigEndian(frame[30..]) != 0x0a00020f)
            return false;
        int ip = (frame[14] & 15) * 4, total = BinaryPrimitives.ReadUInt16BigEndian(frame[16..]);
        if (ip < 20 || total < ip + 20 || total > frame.Length - 14)
            return false;
        var tcp = frame.Slice(14 + ip, total - ip);
        int header = (tcp[12] >> 4) * 4;
        if (header < 20 || header > tcp.Length || BinaryPrimitives.ReadUInt16BigEndian(tcp) != port || BinaryPrimitives.ReadUInt16BigEndian(tcp[2..]) != 8080)
            return false;
        // This is a transport fault injector, not a packet validator. Complete
        // PCAP/native joins still validate checksums, exact tuple and request.
        if (tcp.Length > header)
        {
            Volatile.Write(ref armed, 1);
            return false;
        }

        return Volatile.Read(ref armed) != 0 && (tcp[13] & 2) == 0 && (tcp[13] & 0x15) != 0;
    }

    private bool IsServiceTuple(ReadOnlySpan<byte> frame)
    {
        int[]? ports = Volatile.Read(ref servicePorts);
        if (ports is null || frame.Length < 54 || BinaryPrimitives.ReadUInt16BigEndian(frame[12..]) != 0x0800 || frame[14] >> 4 != 4 || frame[23] != 6 || (BinaryPrimitives.ReadUInt16BigEndian(frame[20..]) & 0x3fff) != 0 || BinaryPrimitives.ReadUInt32BigEndian(frame[26..]) != 0x0a000202 || BinaryPrimitives.ReadUInt32BigEndian(frame[30..]) != 0x0a00020f)
            return false;
        int ip = (frame[14] & 15) * 4, total = BinaryPrimitives.ReadUInt16BigEndian(frame[16..]);
        if (ip < 20 || total < ip + 20 || total > frame.Length - 14)
            return false;
        var tcp = frame.Slice(14 + ip, total - ip);
        int header = (tcp[12] >> 4) * 4;
        return header >= 20 && header <= tcp.Length && ports.Contains((int)BinaryPrimitives.ReadUInt16BigEndian(tcp)) && BinaryPrimitives.ReadUInt16BigEndian(tcp[2..]) == 8080;
    }

    /// <summary>Cancel both directions and join within two seconds. True proves
    /// a join only; Completion retains an earlier framing/protocol failure.</summary>

    public bool Stop()
    {
        bool retire = false;
        lock (lifecycle)
            if (disposed == 0 && stopping == 0)
            {
                PreserveCompletedFailure(allowExpectedIo: guestExitExpected != 0);
                stopping = 1;
                retire = true;
            }

        if (retire)
        {
            stop.Cancel();
            backend.Dispose();
            guest.Dispose();
        }

        try
        {
            return Completion.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            return Completion.IsCompleted;
        }
    }

    public void Dispose()
    {
        if (Volatile.Read(ref disposed) != 0)
            return;
        _ = Stop();
        Volatile.Write(ref disposed, 1);
        stop.Dispose();
    }
}
