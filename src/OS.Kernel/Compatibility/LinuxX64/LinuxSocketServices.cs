#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
using System;
using OS.Net;

namespace OS.Kernel.Compatibility.LinuxX64;

/// <summary>Bounded IPv4 passive TCP sockets in one Linux descriptor namespace.
/// A serialized, IRQ-off kernel dispatcher retains all supervisor metadata and
/// transport backing. This adapter allocates nothing and never polls firmware or
/// the NIC. The continuing network owner supplies only genuinely established
/// TCP lifetimes, pumps packets/timers, and reaps closed ownership explicitly.
/// Negative Linux errno reports failure. Blocking socket waits, connect, UDP,
/// ancillary messages and arbitrary endpoint capabilities are unsupported.</summary>
public static unsafe class LinuxSocketServices
{
    public const uint MaximumSockets = 16;
    private const long Fault = -14, Invalid = -22, Again = -11, Unsupported = -38;
    private const long NotSocket = -88, NotConnected = -107, Reset = -104, BrokenPipe = -32;
    private const uint Created = 1, Bound = 2, Listening = 3, Pending = 4, Connected = 5, Closing = 6;

    /// <summary>Initially zero, private socket metadata. One open description
    /// owns a socket, including all dup fds. Accepted TCP pointers remain borrowed
    /// through ReapClosed, even after the last fd closes. Never copy a live slot.</summary>
    public struct Endpoint
    {
        internal uint Phase, Listener, Backlog, ReadShutdown, ErrorConsumed;
        internal uint ReuseAddress, NoDelay, SendBytes, ReceiveBytes;
        internal Ipv4Address Address;
        internal ushort Port;
        internal ulong Order;
        internal TcpReceiveWindow.Owner* Receiver;
        internal TcpSendWindow.Owner* Sender;
    }

    /// <summary>One-use, noncopyable owner with exclusive zeroed endpoint storage.
    /// InterfaceAddress is the actual configured unicast address. Only that
    /// address or INADDR_ANY and the exact AllowedPort may bind. Begin retains
    /// descriptors and slots; Finish detaches only after all TCP ownership drains.</summary>
    public struct State
    {
        internal ulong Self, NextOrder;
        internal LinuxDescriptorServices.State* Descriptors;
        internal Endpoint* Slots;
        internal uint Count, Initialized;
        internal LinuxSocketNetwork.State* Network;
        internal Ipv4Address InterfaceAddress;
        internal ushort AllowedPort;
        public readonly uint RetainedConnections
        {
            get
            {
                uint count = 0;
                if (Initialized == 1) for (uint i = 0; i < Count; i++) if (Slots[i].Phase >= Pending) count++;
                return count;
            }
        }
    }

    /// <summary>Attach before the descriptor owner is attached to threads.
    /// SIGPIPE must be explicitly ignored. Storage must be trusted, mapped,
    /// exclusive and retained. Check all extents
    /// and zero metadata before mutation; rejection leaves both owners unchanged.</summary>
    public static bool Begin(State* state, LinuxDescriptorServices.State* descriptors,
        Endpoint* slots, uint count, Ipv4Address interfaceAddress, ushort allowedPort)
    {
        if (!Extent(state, (ulong)sizeof(State)) || !Extent(slots, count * (ulong)sizeof(Endpoint)) ||
            count is 0 or > MaximumSockets || !Zero(state, (ulong)sizeof(State)) ||
            !LinuxDescriptorServices.Audit(descriptors) || !descriptors->Owner.SigpipeIgnored || descriptors->ThreadOwner != 0 || descriptors->Sockets != null ||
            interfaceAddress == default || interfaceAddress.Octet0 is 0 or >= 224 || allowedPort == 0 ||
            Overlap((ulong)state, (ulong)sizeof(State), (ulong)slots, count * (ulong)sizeof(Endpoint)) ||
            !LinuxDescriptorServices.SocketOutside(descriptors, state, (ulong)sizeof(State)) ||
            !LinuxDescriptorServices.SocketOutside(descriptors, slots, count * (ulong)sizeof(Endpoint)) ||
            !Zero(slots, count * (ulong)sizeof(Endpoint))) return false;
        *state = new() { Self = (ulong)state, Descriptors = descriptors, Slots = slots, Count = count,
            InterfaceAddress = interfaceAddress, AllowedPort = allowedPort, Initialized = 1 };
        descriptors->Sockets = state; return true;
    }

    /// <summary>Verify the shared description/endpoint accounting. Caller owns
    /// the entire serialized lifetime; this does not establish page provenance.</summary>
    public static bool Audit(State* state, LinuxDescriptorServices.State* descriptors)
    {
        if (!Extent(state, (ulong)sizeof(State)) || !Extent(descriptors, (ulong)sizeof(LinuxDescriptorServices.State)) ||
            state->Self != (ulong)state || state->Initialized != 1 ||
            state->Descriptors != descriptors || descriptors->Sockets != state ||
            state->Count is 0 or > MaximumSockets || !Extent(state->Slots, state->Count * (ulong)sizeof(Endpoint))) return false;
        for (uint i = 0; i < state->Count; i++)
        {
            Endpoint* e = state->Slots + i;
            uint descriptions = LinuxDescriptorServices.SocketReferences(descriptors, i, out _);
            if (e->Phase == 0) { if (descriptions != 0 || !Zero(e, (ulong)sizeof(Endpoint))) return false; continue; }
            if (e->Phase > Closing || e->ReadShutdown > 1 || e->ErrorConsumed > 1 || e->ReuseAddress > 1 || e->NoDelay > 1 ||
                e->SendBytes is 0 or > 32768 || e->ReceiveBytes is 0 or > 32768 ||
                descriptions != (e->Phase is Pending or Closing ? 0U : 1U)) return false;
            if (e->Phase < Pending)
            {
                if (e->Receiver != null || e->Sender != null || e->Listener != 0 || e->Order != 0 ||
                    e->Phase != Created && (e->Port != state->AllowedPort ||
                        e->Address != default && e->Address != state->InterfaceAddress)) return false;
            }
            else
            {
                if (!Connection(e, out _)) return false;
                if (e->Phase == Pending && (e->Order == 0 || e->Listener == 0 || e->Listener > state->Count ||
                    state->Slots[e->Listener - 1].Phase != Listening)) return false;
                for (uint j = 0; j < i; j++)
                    if (state->Slots[j].Receiver == e->Receiver || state->Slots[j].Sender == e->Sender) return false;
            }
        }
        // Every socket description must name an owned endpoint, even when its
        // index is corrupted beyond this fixed slot inventory.
        for (uint i = 0; i < descriptors->Owner.DescriptorCount; i++)
            if (LinuxDescriptorServices.SocketDescription(descriptors, i, out uint endpoint, out _) && endpoint >= state->Count) return false;
        return true;
    }

    /// <summary>Transfer a genuine accepted TCP lifetime into the listener queue.
    /// The token must still name its live fd generation. Validate both upstream
    /// transport attachments and exact local tuple before taking ownership.
    /// The dispatcher proves these trusted transport extents/callbacks do not
    /// alias other scheduler/device ownership. False retains caller ownership;
    /// after success, do not release either window/table except through ReapClosed.
    /// No handshake or packet is synthesized.</summary>
    public static bool EnqueueAccepted(State* state, LinuxDescriptorServices.Token listener,
        uint listenerFd, TcpReceiveWindow.Owner* receiver, TcpSendWindow.Owner* sender)
    {
        if (!Current(state) || !LinuxDescriptorServices.IsCurrent(state->Descriptors, listener) ||
            listener.Number != listenerFd || !Find(state, listenerFd, out uint index, out _) ||
            state->Slots[index].Phase != Listening || state->NextOrder == ulong.MaxValue ||
            TcpReceiveWindow.Validate(receiver, out var entry) != TcpReceiveWindow.Status.Success ||
            TcpSendWindow.Validate(sender, out _) != TcpSendWindow.Status.Success || sender->Receiver != receiver ||
            sender->CloseEnabled != 1 || sender->WritesClosed || entry.Key.LocalAddress != state->InterfaceAddress ||
            entry.Key.LocalPort != state->Slots[index].Port || !TransportOutside(state, receiver, sender)) return false;
        uint pending = 0, free = state->Count;
        for (uint i = 0; i < state->Count; i++)
        {
            Endpoint e = state->Slots[i];
            if (e.Phase == 0 && free == state->Count) free = i;
            if (e.Phase == Pending && e.Listener == index + 1) pending++;
            if (e.Receiver == receiver || e.Sender == sender) return false;
        }
        if (pending >= state->Slots[index].Backlog || free == state->Count) return false;
        Endpoint inherited = state->Slots[index];
        state->Slots[free] = new() { Phase = Pending, Listener = index + 1, Order = ++state->NextOrder,
            Address = entry.Key.LocalAddress, Port = entry.Key.LocalPort, Receiver = receiver, Sender = sender,
            ReuseAddress = inherited.ReuseAddress, NoDelay = inherited.NoDelay,
            SendBytes = inherited.SendBytes < sender->CapacityValue ? inherited.SendBytes : (uint)sender->CapacityValue,
            ReceiveBytes = (uint)receiver->ReceiveCapacity };
        return true;
    }

    /// <summary>After the network owner stops packet delivery to a closed,
    /// fd-less endpoint, scrub and detach both windows, then release its exact
    /// table generation. No NIC quiescence is implied. Failures retain metadata
    /// for diagnosis; do not retry external release against these same owners.</summary>
    public static bool ReapClosed(State* state, TcpSendWindow.Owner* sender)
    {
        if (!Current(state) || sender == null) return false;
        uint index = 0; for (; index < state->Count && state->Slots[index].Sender != sender; index++) { }
        if (index == state->Count) return false;
        Endpoint* e = state->Slots + index;
        if (e->Phase != Closing || !Connection(e, out var entry) || entry.State != TcpConnectionLifecycleState.Closed) return false;
        var table = e->Sender->Table; var handle = e->Sender->Handle;
        if (TcpSendWindow.ReleaseClosed(e->Sender) != TcpSendWindow.Status.Success ||
            TcpReceiveWindow.ReleaseClosed(e->Receiver) != TcpReceiveWindow.Status.Success ||
            TcpConnectionTable.ReleaseOwned(table, handle) != TcpConnectionTable.OwnershipStatus.Success) return false;
        *e = default; return true;
    }

    internal static long Dispatch(State* state, LinuxSyscallFrame f)
    {
        if (f.Number == 41) return Create(state, f.Argument0, f.Argument1, f.Argument2);
        uint fd = unchecked((uint)f.Argument0);
        if (!Find(state, fd, out uint index, out uint status)) return Existing(state, fd) ? NotSocket : -9;
        Endpoint* e = state->Slots + index;
        switch (f.Number)
        {
            case 49: return Bind(state, e, f.Argument1, f.Argument2);
            case 50:
                if (e->Phase is not (Bound or Listening)) return Invalid;
                e->Backlog = unchecked((int)f.Argument1) <= 0 ? 1 : (uint)(f.Argument1 < state->Count ? f.Argument1 : state->Count);
                e->Phase = Listening; return 0;
            case 43: case 288:
                if (f.Number == 288 && (f.Argument3 & ~(LinuxDescriptorServices.NonBlock | LinuxDescriptorServices.CloseOnExec)) != 0) return Invalid;
                return Accept(state, index, status, f.Argument1, f.Argument2, f.Number == 288 ? (uint)f.Argument3 : 0);
            case 44:
                if (f.Argument4 != 0 || f.Argument5 != 0) return -106;
                return Send(state, index, f.Argument1, f.Argument2, f.Argument3);
            case 45:
                if (f.Argument4 != 0 || f.Argument5 != 0) return -95;
                return Receive(state, index, f.Argument1, f.Argument2, f.Argument3);
            case 48: return Shutdown(e, f.Argument1);
            case 51: case 52:
                if (f.Number == 52 && e->Phase != Connected) return NotConnected;
                return Name(state, e, f.Number == 52, f.Argument1, f.Argument2);
            case 54: return SetOption(state, e, f.Argument1, f.Argument2, f.Argument3, f.Argument4);
            case 55: return GetOption(state, e, f.Argument1, f.Argument2, f.Argument3, f.Argument4);
            default: return -95;
        }
    }

    internal static long WritePolicy(State* state, uint index, ulong flags)
    {
        if ((flags & ~0x4040UL) != 0) return -95; // MSG_DONTWAIT | MSG_NOSIGNAL.
        Endpoint* e = state->Slots + index;
        if (e->Phase != Connected) return NotConnected;
        if (!Connection(e, out var entry)) return -5;
        if (e->Sender->CloseState == TcpConnectionCloseState.Reset)
        { if (e->ErrorConsumed != 0) return BrokenPipe; e->ErrorConsumed = 1; return Reset; }
        if (e->Sender->WritesClosed || entry.State == TcpConnectionLifecycleState.Closed) return BrokenPipe;
        if (!Nonblocking(state, index) && (flags & 0x40) == 0) return Unsupported;
        return e->Sender->BufferedBytes >= e->SendBytes ? Again : 0;
    }

    internal static long SendCopied(State* state, uint index, uint bytes)
    {
        Endpoint* e = state->Slots + index;
        uint available = e->SendBytes > e->Sender->BufferedBytes ? e->SendBytes - (uint)e->Sender->BufferedBytes : 0;
        if (bytes > available) bytes = available;
        if (bytes == 0) return Again;
        var result = TcpSendWindow.Queue(e->Sender,
            new ReadOnlySpan<byte>(state->Descriptors->Owner.Scratch, (int)bytes), out int accepted);
        return result == TcpSendWindow.Status.Success ? accepted : result == TcpSendWindow.Status.WouldBlock ? Again : -5;
    }

    internal static long Receive(State* state, uint index, ulong address, ulong requested, ulong flags)
    {
        if ((flags & ~0x42UL) != 0) return -95; // MSG_PEEK | MSG_DONTWAIT.
        if (requested > LinuxDescriptorServices.MaximumIo) return Invalid;
        Endpoint* e = state->Slots + index;
        if (e->Phase != Connected) return NotConnected;
        if (!Connection(e, out var entry)) return -5;
        bool reset = e->Sender->CloseState == TcpConnectionCloseState.Reset;
        if (reset && e->Receiver->UnreadBytes == 0)
        { if (e->ErrorConsumed != 0) return 0; e->ErrorConsumed = 1; return Reset; }
        if (requested == 0 || e->ReadShutdown != 0) return 0;
        if (entry.State == TcpConnectionLifecycleState.Closed && e->Receiver->PeerClosed == 0 && !reset) return -5;
        var a = state->Descriptors->Owner; var receiver = e->Receiver;
        if (TcpReceiveWindow.Validate(receiver, out _, receiver->PeerClosed != 0 || reset) != TcpReceiveWindow.Status.Success) return -5;
        if (receiver->UnreadBytes == 0) return receiver->EndOfStream ? 0 :
            Nonblocking(state, index) || (flags & 0x40) != 0 ? Again : Unsupported;
        uint bytes = (uint)(requested < a.ScratchBytes ? requested : a.ScratchBytes);
        if (bytes > receiver->UnreadBytes) bytes = (uint)receiver->UnreadBytes;
        // Copy-out may fail even after page validation. Leave the TCP window
        // untouched until the entire borrowed prefix has reached user memory.
        // The native copy boundary admits the descriptor scratch extent, not
        // an arbitrary transport-owned receive buffer.
        for (uint i = 0; i < bytes; i++) a.Scratch[i] = receiver->Buffer[receiver->Offset + i];
        if (!LinuxDescriptorServices.Output(state->Descriptors, address, a.Scratch, bytes)) return Fault;
        if ((flags & 2) != 0) return bytes; // Peeking never advances the window or schedules an ACK.
        return TcpReceiveWindow.Read(receiver, new Span<byte>(a.Scratch, (int)bytes), out var read, reset) == TcpReceiveWindow.Status.Success &&
            read.BytesRead == bytes ? bytes : -5;
    }

    internal static bool Close(State* state, uint index)
    {
        Endpoint* e = state->Slots + index;
        if (e->Phase == Listening)
            for (uint i = 0; i < state->Count; i++)
                if (state->Slots[i].Phase == Pending && state->Slots[i].Listener == index + 1 && !BeginClose(state->Slots + i)) return false;
        if (e->Phase < Pending) *e = default; else if (!BeginClose(e)) return false;
        return true;
    }

    internal static bool Finish(State* state)
    {
        if (state->Network != null) return false;
        for (uint i = 0; i < state->Count; i++) if (state->Slots[i].Phase != 0) return false;
        state->Descriptors->Sockets = null; state->Initialized = 2; return true;
    }

    internal static bool StorageOverlap(State* state, ulong start, ulong bytes) => state != null &&
        (Overlap(start, bytes, (ulong)state, (ulong)sizeof(State)) ||
         Overlap(start, bytes, (ulong)state->Slots, state->Count * (ulong)sizeof(Endpoint)));

    // Observe actual transport state without consuming bytes/errors or changing
    // flags. The serialized network owner refreshes epoll after each transition.
    internal static bool Readiness(State* state, uint index, out uint events)
    {
        events = 0;
        if (state == null || index >= state->Count) return false;
        Endpoint* e = state->Slots + index;
        if (e->Phase == Listening)
        {
            for (uint i = 0; i < state->Count; i++)
                if (state->Slots[i].Phase == Pending && state->Slots[i].Listener == index + 1) { events = 1; break; }
            return true;
        }
        if (e->Phase is Created or Bound) return true;
        if (e->Phase != Connected || !Connection(e, out var entry)) return false;
        if (e->Sender->CloseState == TcpConnectionCloseState.Reset)
        { events = 1 | 4 | 16 | (e->ErrorConsumed == 0 ? 8U : 0); return true; }
        if (e->Sender->RequiresCleanup && e->Sender->CloseState != TcpConnectionCloseState.Closed)
        { events = 1 | 4 | 8 | 16; return true; }
        if (e->Receiver->UnreadBytes != 0 || e->Receiver->EndOfStream || e->ReadShutdown != 0) events |= 1;
        if (e->Sender->WritesClosed || e->Sender->BufferedBytes < e->SendBytes) events |= 4;
        if (e->Receiver->PeerClosed != 0) events |= 0x2000;
        if (entry.State == TcpConnectionLifecycleState.Closed || e->ReadShutdown != 0 && e->Sender->WritesClosed) events |= 16;
        return true;
    }

    private static long Create(State* state, ulong family, ulong type, ulong protocol)
    {
        if (unchecked((int)family) != 2) return -97;
        if ((type & ~(LinuxDescriptorServices.NonBlock | LinuxDescriptorServices.CloseOnExec | 15UL)) != 0) return Invalid;
        // The upstream family capability probe uses SOCK_DGRAM. A supported
        // family with an unsupported type returns ESOCKTNOSUPPORT, not a false
        // UDP fd or EAFNOSUPPORT/EPROTONOSUPPORT that would disable IPv4 entirely.
        if ((type & 15) != 1) return -94;
        if (protocol is not (0 or 6)) return -93;
        uint index = 0; for (; index < state->Count && state->Slots[index].Phase != 0; index++) { }
        if (index == state->Count) return -105;
        long fd = LinuxDescriptorServices.InstallSocket(state->Descriptors, index, (uint)type);
        if (fd >= 0) state->Slots[index] = new() { Phase = Created, SendBytes = 32768, ReceiveBytes = 32768 };
        return fd;
    }

    private static long Bind(State* state, Endpoint* e, ulong address, ulong bytes)
    {
        if (e->Phase != Created || bytes != 16) return Invalid;
        byte* wire = stackalloc byte[16];
        if (!Input(state, address, wire, 16)) return Fault;
        if (*(ushort*)wire != 2) return -97;
        ushort port = (ushort)((wire[2] << 8) | wire[3]);
        Ipv4Address ip = new(wire[4], wire[5], wire[6], wire[7]);
        if (port != state->AllowedPort || ip != default && ip != state->InterfaceAddress) return -13;
        for (uint i = 0; i < state->Count; i++)
            if (state->Slots[i].Port == port && (state->Slots[i].Phase is Bound or Listening ||
                state->Slots[i].Phase >= Pending && e->ReuseAddress == 0)) return -98;
        e->Address = ip; e->Port = port; e->Phase = Bound; return 0;
    }

    private static long Accept(State* state, uint listener, uint status, ulong address, ulong length, uint flags)
    {
        if (state->Slots[listener].Phase != Listening) return Invalid;
        uint selected = state->Count; ulong order = ulong.MaxValue;
        for (uint i = 0; i < state->Count; i++)
            if (state->Slots[i].Phase == Pending && state->Slots[i].Listener == listener + 1 && state->Slots[i].Order < order)
            { selected = i; order = state->Slots[i].Order; }
        if (selected == state->Count) return (status & LinuxDescriptorServices.NonBlock) != 0 ? Again : Unsupported;
        // Reserve capacity before writing the peer address or consuming backlog.
        long capacity = LinuxDescriptorServices.SocketCapacity(state->Descriptors);
        if (capacity != 0) return capacity;
        Endpoint* e = state->Slots + selected;
        if (address != 0)
        {
            long result = Name(state, e, true, address, length);
            if (result != 0) return result;
        }
        long fd = LinuxDescriptorServices.InstallSocket(state->Descriptors, selected, flags);
        if (fd >= 0) { e->Phase = Connected; e->Listener = 0; e->Order = 0; }
        return fd;
    }

    private static long Send(State* state, uint index, ulong address, ulong bytes, ulong flags)
    {
        if (bytes > LinuxDescriptorServices.MaximumIo) return Invalid;
        long policy = WritePolicy(state, index, flags); if (policy != 0) return policy;
        if (bytes == 0) return 0;
        var a = state->Descriptors->Owner;
        uint wanted = (uint)(bytes < a.ScratchBytes ? bytes : a.ScratchBytes);
        if (!Input(state, address, a.Scratch, wanted)) return Fault;
        return SendCopied(state, index, wanted);
    }

    private static long Shutdown(Endpoint* e, ulong how)
    {
        if (how > 2) return Invalid;
        if (e->Phase != Connected) return NotConnected;
        if (how != 0 && !e->Sender->WritesClosed && TcpSendWindow.ShutdownWrite(e->Sender) != TcpSendWindow.Status.Success) return -5;
        if (how != 1) e->ReadShutdown = 1;
        return 0;
    }

    private static long SetOption(State* state, Endpoint* e, ulong level, ulong name, ulong address, ulong bytes)
    {
        if (bytes != 4) return Invalid;
        int value; if (!Input(state, address, (byte*)&value, 4)) return Fault;
        if (level == 1 && name == 2) { e->ReuseAddress = value != 0 ? 1U : 0; return 0; }
        if (level == 6 && name == 1) { e->NoDelay = value != 0 ? 1U : 0; return 0; }
        if (level == 1 && name == 7)
        {
            uint maximum = e->Phase >= Pending ? (uint)e->Sender->CapacityValue : 32768;
            if (value <= 0 || value > maximum) return -105;
            e->SendBytes = (uint)value;
            return 0;
        }
        return -92;
    }

    private static long GetOption(State* state, Endpoint* e, ulong level, ulong name, ulong address, ulong length)
    {
        int value;
        if (level == 1 && name == 4) value = e->Sender != null && e->Sender->CloseState == TcpConnectionCloseState.Reset && e->ErrorConsumed == 0 ? 104 : 0;
        else if (level == 1 && name == 2) value = (int)e->ReuseAddress;
        else if (level == 6 && name == 1) value = (int)e->NoDelay;
        else if (level == 1 && name is 7 or 8) value = (int)(name == 7 ? e->SendBytes : e->ReceiveBytes);
        else return -92;
        uint bytes; if (!Input(state, length, (byte*)&bytes, 4)) return Fault;
        if (bytes < 4) return Invalid;
        uint size = 4;
        if (!Writable(state, address, 4) || !Writable(state, length, 4) ||
            Overlap(address, 4, length, 4) || !LinuxDescriptorServices.Output(state->Descriptors, address, (byte*)&value, 4) ||
            !LinuxDescriptorServices.Output(state->Descriptors, length, (byte*)&size, 4)) return Fault;
        if (level == 1 && name == 4 && value != 0) e->ErrorConsumed = 1;
        return 0;
    }

    private static long Name(State* state, Endpoint* e, bool peer, ulong address, ulong length)
    {
        uint capacity; if (!Input(state, length, (byte*)&capacity, 4)) return Fault;
        byte* wire = stackalloc byte[16]; for (uint i = 0; i < 16; i++) wire[i] = 0;
        *(ushort*)wire = 2; Ipv4Address ip = e->Address; ushort port = e->Port;
        if (peer)
        {
            if (!Connection(e, out var connection)) return NotConnected;
            ip = connection.Key.RemoteAddress; port = connection.Key.RemotePort;
        }
        wire[2] = (byte)(port >> 8); wire[3] = (byte)port;
        wire[4] = ip.Octet0; wire[5] = ip.Octet1; wire[6] = ip.Octet2; wire[7] = ip.Octet3;
        uint bytes = capacity < 16 ? capacity : 16, size = 16;
        if (!Writable(state, address, bytes) || !Writable(state, length, 4) || Overlap(address, bytes, length, 4) ||
            bytes != 0 && !LinuxDescriptorServices.Output(state->Descriptors, address, wire, bytes) ||
            !LinuxDescriptorServices.Output(state->Descriptors, length, (byte*)&size, 4)) return Fault;
        return 0;
    }

    private static bool BeginClose(Endpoint* e)
    {
        if (!Connection(e, out var entry)) return false;
        // A failed transport clock/budget cannot complete FIN pumping. Explicitly
        // close that exact TCP generation while retaining both buffers for reap.
        if (entry.State == TcpConnectionLifecycleState.Established &&
            (e->Sender->RequiresCleanup || !e->Sender->WritesClosed && TcpSendWindow.ShutdownWrite(e->Sender) != TcpSendWindow.Status.Success) &&
            TcpSendWindow.AbortConnection(e->Sender) != TcpSendWindow.Status.Success) return false;
        e->Phase = Closing; e->Listener = 0; e->Order = 0;
        return true;
    }
    private static bool Find(State* state, uint fd, out uint index, out uint flags) =>
        LinuxDescriptorServices.SocketDescription(state->Descriptors, fd, out index, out flags);

    internal static bool Listener(State* state, out uint fd, out LinuxDescriptorServices.Token token)
    {
        fd = 0; token = default;
        if (!Current(state)) return false;
        for (uint i = 0; i < state->Descriptors->Owner.DescriptorCount; i++)
            if (Find(state, i, out uint index, out _) && state->Slots[index].Phase == Listening)
            { fd = i; return LinuxDescriptorServices.Capture(state->Descriptors, i, out token); }
        return false;
    }
    internal static bool IsClosing(State* state, TcpSendWindow.Owner* sender)
    {
        for (uint i = 0; i < state->Count; i++) if (state->Slots[i].Sender == sender) return state->Slots[i].Phase == Closing;
        return false;
    }
    private static bool Nonblocking(State* state, uint index)
    { LinuxDescriptorServices.SocketReferences(state->Descriptors, index, out uint flags); return (flags & LinuxDescriptorServices.NonBlock) != 0; }
    private static bool Existing(State* state, uint fd) => fd < state->Descriptors->Owner.DescriptorCount && state->Descriptors->Owner.Slots[fd].Description != 0;
    private static bool Input(State* state, ulong address, byte* output, uint bytes) =>
        LinuxSyscallGateway.ValidateRange(address, bytes, LinuxUserPageAccess.Read, state->Descriptors->Owner.User) &&
        state->Descriptors->Owner.User.CopyFromUser(state->Descriptors->Owner.User.Context, address, output, bytes) == 1;
    private static bool Writable(State* state, ulong address, uint bytes) => bytes == 0 ||
        LinuxSyscallGateway.ValidateRange(address, bytes, LinuxUserPageAccess.Write, state->Descriptors->Owner.User);
    private static bool Current(State* state) => Extent(state, (ulong)sizeof(State)) && state->Initialized == 1 &&
        state->Self == (ulong)state && LinuxDescriptorServices.Audit(state->Descriptors);
    private static bool Connection(Endpoint* e, out TcpConnectionTableEntry entry)
    {
        entry = default;
        if (!Extent(e->Sender, (ulong)sizeof(TcpSendWindow.Owner)) || !Extent(e->Receiver, (ulong)sizeof(TcpReceiveWindow.Owner)) ||
            e->Sender->Receiver != e->Receiver || TcpConnectionTable.ReadOwned(e->Sender->Table, e->Sender->Handle, out entry) != TcpConnectionTable.OwnershipStatus.Success) return false;
        return TcpSendWindow.Validate(e->Sender, out _, entry.State == TcpConnectionLifecycleState.Closed, true) == TcpSendWindow.Status.Success;
    }
    private static bool TransportOutside(State* state, TcpReceiveWindow.Owner* receiver, TcpSendWindow.Owner* sender)
    {
        ulong* extents = stackalloc ulong[14]
        {
            (ulong)receiver, (ulong)sizeof(TcpReceiveWindow.Owner), (ulong)sender, (ulong)sizeof(TcpSendWindow.Owner),
            (ulong)receiver->Buffer, (ulong)receiver->ReceiveCapacity, (ulong)sender->Buffer, (ulong)sender->CapacityValue,
            (ulong)sender->Table, (ulong)sizeof(TcpConnectionTable.Owner),
            (ulong)sender->Table->Entries, (ulong)sender->Table->CapacityValue * (ulong)sizeof(TcpConnectionTableEntry),
            (ulong)sender->Table->Generations, (ulong)sender->Table->CapacityValue * 8
        };
        for (uint i = 0; i < 7; i++)
            if (StorageOverlap(state, extents[i * 2], extents[i * 2 + 1]) ||
                !LinuxDescriptorServices.SocketOutside(state->Descriptors, (void*)extents[i * 2], extents[i * 2 + 1])) return false;
        return true;
    }
    private static bool Extent(void* pointer, ulong bytes) => pointer != null && ((ulong)pointer & 7) == 0 && bytes != 0 && bytes <= ~0UL - (ulong)pointer;
    private static bool Overlap(ulong a, ulong size, ulong b, ulong bytes) => size != 0 && bytes != 0 && a < b + bytes && b < a + size;
    private static bool Zero(void* pointer, ulong bytes) { for (ulong i = 0; i < bytes; i++) if (((byte*)pointer)[i] != 0) return false; return true; }
}
#endif
