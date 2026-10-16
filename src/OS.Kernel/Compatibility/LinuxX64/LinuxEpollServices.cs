#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
using Fds = OS.Kernel.Compatibility.LinuxX64.LinuxDescriptorServices;

namespace OS.Kernel.Compatibility.LinuxX64;

/// <summary>Bounded Linux x64 epoll over this process's real passive TCP sockets.
/// One serialized IRQ-off dispatcher owns all borrowed supervisor storage and
/// refreshes readiness after every transport transition. Registrations retain
/// the original open-description identity, not authority over a reused fd.
/// No allocation, packet pumping, native switch, nested epoll, arbitrary pollable
/// files, EPOLLONESHOT or temporary signal masks are supplied. Negative errno
/// reports user errors; failed ownership checks retain storage for shutdown.</summary>
public static unsafe class LinuxEpollServices
{
    public const uint MaximumPorts = 4, MaximumRegistrations = 64, MaximumEvents = 1024;
    private const uint Edge = 0x80000000, Events = 1 | 4 | 8 | 16 | 0x2000;
    private const long Fault = -14, Invalid = -22, Unsupported = -38;

    /// <summary>Zeroed port metadata shared by dup fds and retained kernel waits.
    /// Cursor provides bounded fair delivery. Never copy or mutate a live port.</summary>
    public struct Port { internal uint Active, Cursor; }

    /// <summary>Zeroed private watch metadata. Opaque user Data is copied as
    /// exactly 64 bits and is never dereferenced. Last open-description close
    /// scrubs the entire registration, including queued readiness and Data.</summary>
    public struct Registration
    {
        internal uint Port, Fd, Description, Interest, Ready, Pending;
        internal ulong Inode, Data;
    }

    /// <summary>Noncopyable, one-use owner of caller-retained disjoint tables.
    /// Descriptors, ports and registrations remain mapped supervisor-only until
    /// Finish, after every fd and retained wait have drained. Counts are bounded
    /// capacities, not allocation requests or mapping authority.</summary>
    public struct State
    {
        internal ulong Self;
        internal Fds.State* Descriptors;
        internal Port* Ports;
        internal Registration* Registrations;
        internal uint PortCount, RegistrationCount, Initialized;
    }

    /// <summary>Stable thread-owned wait borrow of the original epoll description.
    /// Closing/reusing its fd cannot redirect completion. User output is an
    /// address only: each copy revalidates mappings. EndOperation releases the
    /// borrow only after genuine wait completion or captured thread exit.</summary>
    public struct Operation
    {
        internal State* Owner;
        internal ulong Self, Inode, Address;
        internal uint Description, Port, Maximum, Active;
        public readonly bool IsActive => Active == 1;
    }

    /// <summary>Attach before threads start or any TCP lifetime is enqueued.
    /// Reject dirty, overlapping or invalid
    /// extents before publication. Caller proves trusted mappings and exclusive
    /// lifetime; rejection leaves all storage and descriptors unchanged.</summary>
    public static bool Begin(State* state, Fds.State* descriptors, Port* ports, uint portCount,
        Registration* registrations, uint registrationCount)
    {
        if (portCount is 0 or > MaximumPorts || registrationCount is 0 or > MaximumRegistrations ||
            !Extent(state, (ulong)sizeof(State)) || !Extent(ports, portCount * (ulong)sizeof(Port)) ||
            !Extent(registrations, registrationCount * (ulong)sizeof(Registration)) ||
            !Fds.Audit(descriptors) || descriptors->ThreadOwner != 0 || descriptors->Epoll != null || descriptors->Sockets == null ||
            descriptors->Sockets->RetainedConnections != 0 ||
            !Zero(state, (ulong)sizeof(State)) || !Zero(ports, portCount * (ulong)sizeof(Port)) ||
            !Zero(registrations, registrationCount * (ulong)sizeof(Registration))) return false;
        ulong* extents = stackalloc ulong[6] { (ulong)state, (ulong)sizeof(State), (ulong)ports, portCount * (ulong)sizeof(Port),
            (ulong)registrations, registrationCount * (ulong)sizeof(Registration) };
        for (uint i = 0; i < 3; i++)
        {
            if (!Fds.SocketOutside(descriptors, (void*)extents[i * 2], extents[i * 2 + 1])) return false;
            for (uint j = 0; j < i; j++) if (Overlap(extents[i * 2], extents[i * 2 + 1], extents[j * 2], extents[j * 2 + 1])) return false;
        }
        *state = new() { Self = (ulong)state, Descriptors = descriptors, Ports = ports, PortCount = portCount,
            Registrations = registrations, RegistrationCount = registrationCount, Initialized = 1 };
        descriptors->Epoll = state; return true;
    }

    /// <summary>Validate shared descriptor/watch identities without consuming
    /// readiness. This does not grant mapping or transport ownership.</summary>
    public static bool Audit(State* state, Fds.State* descriptors)
    {
        if (!Extent(state, (ulong)sizeof(State)) || state->Self != (ulong)state || state->Initialized != 1 ||
            descriptors == null || state->Descriptors != descriptors || descriptors->Epoll != state ||
            state->PortCount is 0 or > MaximumPorts || state->RegistrationCount is 0 or > MaximumRegistrations ||
            !Extent(state->Ports, state->PortCount * (ulong)sizeof(Port)) ||
            !Extent(state->Registrations, state->RegistrationCount * (ulong)sizeof(Registration))) return false;
        var a = descriptors->Owner;
        for (uint i = 0; i < state->PortCount; i++)
        {
            Port p = state->Ports[i]; uint descriptions = 0;
            for (uint j = 0; j < a.DescriptionCount; j++)
                if (a.Descriptions[j].Type == (uint)Fds.Kind.Epoll && a.Descriptions[j].Pipe == i) descriptions++;
            if (p.Active > 1 || descriptions != p.Active || p.Cursor >= state->RegistrationCount || p.Active == 0 && p.Cursor != 0) return false;
        }
        for (uint i = 0; i < a.DescriptionCount; i++)
            if (a.Descriptions[i].Type == (uint)Fds.Kind.Epoll && a.Descriptions[i].Pipe >= state->PortCount) return false;
        for (uint i = 0; i < state->RegistrationCount; i++)
        {
            Registration r = state->Registrations[i];
            if (r.Port == 0) { if (!Zero(state->Registrations + i, (ulong)sizeof(Registration))) return false; continue; }
            if (r.Port > state->PortCount || state->Ports[r.Port - 1].Active != 1 || r.Fd >= a.DescriptorCount ||
                r.Description >= a.DescriptionCount || r.Inode == 0 || (r.Interest & ~(Events | Edge)) != 0 ||
                (r.Ready & ~Events) != 0 || (r.Pending & ~r.Ready) != 0) return false;
            Fds.Description d = a.Descriptions[r.Description];
            if (d.Type != (uint)Fds.Kind.Socket || d.Inode != r.Inode || d.References == 0) return false;
            for (uint j = 0; j < i; j++)
            {
                Registration previous = state->Registrations[j];
                if (previous.Port == r.Port && previous.Fd == r.Fd && previous.Description == r.Description && previous.Inode == r.Inode) return false;
            }
        }
        return true;
    }

    /// <summary>Observe actual socket readiness after serialized descriptor or
    /// network mutations. Edge watches enqueue only newly ready bits; draining
    /// clears old readiness before a later arrival. No scheduler wake is performed
    /// here: the thread owner pairs this with its retained wait-token scan.</summary>
    public static bool Refresh(State* state)
    {
        if (!Current(state)) return false;
        for (uint i = 0; i < state->RegistrationCount; i++)
        {
            Registration* r = state->Registrations + i;
            if (r->Port == 0) continue;
            uint socket = state->Descriptors->Owner.Descriptions[r->Description].Pipe;
            if (!LinuxSocketServices.Readiness(state->Descriptors->Sockets, socket, out uint ready)) return false;
            ready &= r->Interest | 8 | 16;
            r->Pending = (r->Pending & ready) | (ready & ~r->Ready); r->Ready = ready;
        }
        return true;
    }

    internal static long Dispatch(State* state, LinuxSyscallFrame frame)
    {
        if (!Current(state)) return Fault;
        if (frame.Number is 213 or 291)
        {
            if (frame.Number == 213 ? unchecked((int)frame.Argument0) <= 0 : (frame.Argument0 & ~Fds.CloseOnExec) != 0) return Invalid;
            uint port = 0; for (; port < state->PortCount && state->Ports[port].Active != 0; port++) { }
            if (port == state->PortCount) return -28;
            long fd = Fds.InstallEndpoint(state->Descriptors, port, frame.Number == 291 ? (uint)frame.Argument0 : 0, Fds.Kind.Epoll);
            if (fd >= 0) state->Ports[port].Active = 1;
            return fd;
        }
        long error = FindPort(state, unchecked((uint)frame.Argument0), out var description);
        if (error != 0) return error;
        if (frame.Number == 233) return Control(state, description->Pipe, frame);
        error = ValidateWait(state, frame, out uint maximum);
        if (error != 0) return error;
        long count = Collect(state, description->Pipe, frame.Argument1, maximum);
        // Waiting requires the genuine thread owner. Never synthesize a zero
        // event return (or EAGAIN) for an infinite empty-port wait.
        return count != 0 || unchecked((int)frame.Argument3) == 0 ? count : Unsupported;
    }

    private static long Control(State* state, uint port, LinuxSyscallFrame frame)
    {
        uint command = unchecked((uint)frame.Argument1), fd = unchecked((uint)frame.Argument2);
        if (command is < 1 or > 3) return Invalid;
        var d = Fds.Find(state->Descriptors, fd);
        if (d == null) return -9;
        if (d->Type != (uint)Fds.Kind.Socket) return d->Type == (uint)Fds.Kind.Epoll && d->Pipe == port ? Invalid : -1;
        uint index = state->Descriptors->Owner.Slots[fd].Description - 1, found = state->RegistrationCount, free = found;
        for (uint i = 0; i < state->RegistrationCount; i++)
        {
            Registration r = state->Registrations[i];
            if (r.Port == 0 && free == state->RegistrationCount) free = i;
            if (r.Port == port + 1 && r.Fd == fd && r.Description == index && r.Inode == d->Inode) found = i;
        }
        if (command == 1 && found != state->RegistrationCount) return -17;
        if (command != 1 && found == state->RegistrationCount) return -2;
        if (command == 2) { state->Registrations[found] = default; return 0; }
        byte* wire = stackalloc byte[12]; var user = state->Descriptors->Owner.User;
        if (!LinuxSyscallGateway.ValidateRange(frame.Argument3, 12, LinuxUserPageAccess.Read, user) ||
            user.CopyFromUser(user.Context, frame.Argument3, wire, 12) != 1) return Fault;
        uint interest = *(uint*)wire;
        if ((interest & ~(Events | Edge)) != 0) return Invalid;
        if (command == 1 && free == state->RegistrationCount) return -28;
        uint target = command == 1 ? free : found;
        state->Registrations[target] = new() { Port = port + 1, Fd = fd, Description = index, Inode = d->Inode,
            Interest = interest, Data = *(ulong*)(wire + 4) };
        return 0;
    }

    internal static bool BeginOperation(Fds.State* descriptors, ulong owner, LinuxSyscallFrame frame, ref Operation operation, out long value)
    {
        value = Fault;
        if (!Fds.Attached(descriptors, owner)) return true;
        if (frame.Number is not (232 or 281) || descriptors->Epoll == null) return false;
        State* state = descriptors->Epoll;
        value = FindPort(state, unchecked((uint)frame.Argument0), out var d);
        if (value != 0) return true;
        value = ValidateWait(state, frame, out uint maximum);
        if (value != 0) return true;
        value = Collect(state, d->Pipe, frame.Argument1, maximum);
        if (value != 0 || unchecked((int)frame.Argument3) == 0) return true;
        if (operation.Active != 0 || operation.Self != 0 || d->Borrowers == Fds.MaximumDescriptors) { value = -23; return true; }
        ulong self; fixed (Operation* p = &operation) self = (ulong)p;
        operation = new() { Owner = state, Self = self, Inode = d->Inode, Address = frame.Argument1, Maximum = maximum,
            Description = descriptors->Owner.Slots[unchecked((uint)frame.Argument0)].Description - 1, Port = d->Pipe, Active = 1 };
        d->Borrowers++; return true;
    }

    internal static bool AttemptOperation(ulong owner, ref Operation operation, bool expired, out long value, out bool blocked)
    {
        value = Fault; blocked = false;
        if (!CurrentOperation(owner, ref operation)) return false;
        value = Collect(operation.Owner, operation.Port, operation.Address, operation.Maximum);
        blocked = value == 0 && !expired; return true;
    }
    internal static bool HasEvents(ulong owner, ref Operation operation, out bool ready)
    {
        ready = false;
        if (!CurrentOperation(owner, ref operation)) return false;
        var state = operation.Owner;
        for (uint i = 0; i < state->RegistrationCount; i++)
        {
            Registration r = state->Registrations[i];
            if (r.Port == operation.Port + 1 && ((r.Interest & Edge) != 0 ? r.Pending : r.Ready) != 0) ready = true;
        }
        return true;
    }
    internal static bool EndOperation(ulong owner, ref Operation operation)
    {
        if (!CurrentOperation(owner, ref operation) || !Fds.DropDescription(operation.Owner->Descriptors, operation.Description, true)) return false;
        operation = default; return true;
    }
    private static bool CurrentOperation(ulong owner, ref Operation operation)
    {
        if (operation.Active != 1 || !Current(operation.Owner) || !Fds.Attached(operation.Owner->Descriptors, owner) ||
            operation.Description >= operation.Owner->Descriptors->Owner.DescriptionCount) return false;
        fixed (Operation* p = &operation) if ((ulong)p != operation.Self) return false;
        var d = operation.Owner->Descriptors->Owner.Descriptions[operation.Description];
        return d.Type == (uint)Fds.Kind.Epoll && d.Inode == operation.Inode && d.Pipe == operation.Port && d.Borrowers != 0;
    }

    private static long ValidateWait(State* state, LinuxSyscallFrame frame, out uint maximum)
    {
        maximum = unchecked((uint)frame.Argument2);
        if (maximum is 0 or > MaximumEvents) return Invalid;
        if (frame.Number == 281 && frame.Argument4 != 0) return Unsupported;
        return LinuxSyscallGateway.ValidateRange(frame.Argument1, maximum * 12UL, LinuxUserPageAccess.Write, state->Descriptors->Owner.User) ? 0 : Fault;
    }
    private static long FindPort(State* state, uint fd, out Fds.Description* description)
    {
        description = Fds.Find(state->Descriptors, fd);
        return description == null ? -9 : description->Type != (uint)Fds.Kind.Epoll ? Invalid : 0;
    }
    private static long Collect(State* state, uint port, ulong address, uint maximum)
    {
        if (!Refresh(state)) return Fault;
        byte* output = stackalloc byte[(int)MaximumRegistrations * 12];
        uint* selected = stackalloc uint[(int)MaximumRegistrations]; uint count = 0, cursor = state->Ports[port].Cursor;
        for (uint step = 0; step < state->RegistrationCount && count < maximum; step++)
        {
            uint index = (cursor + step) % state->RegistrationCount; Registration r = state->Registrations[index];
            uint ready = (r.Interest & Edge) != 0 ? r.Pending : r.Ready;
            if (r.Port != port + 1 || ready == 0) continue;
            selected[count] = index; *(uint*)(output + count * 12) = ready; *(ulong*)(output + count * 12 + 4) = r.Data; count++;
        }
        if (count == 0) return 0;
        if (!Fds.Output(state->Descriptors, address, output, count * 12)) return Fault;
        for (uint i = 0; i < count; i++) state->Registrations[selected[i]].Pending = 0;
        state->Ports[port].Cursor = (selected[count - 1] + 1) % state->RegistrationCount; return count;
    }

    internal static void DropDescription(State* state, uint description, ulong inode, bool port, uint portIndex)
    {
        for (uint i = 0; i < state->RegistrationCount; i++)
        {
            Registration r = state->Registrations[i];
            if (port ? r.Port == portIndex + 1 : r.Description == description && r.Inode == inode) state->Registrations[i] = default;
        }
        if (port) state->Ports[portIndex] = default;
    }
    internal static bool Finish(State* state)
    {
        for (uint i = 0; i < state->PortCount; i++) if (state->Ports[i].Active != 0) return false;
        for (uint i = 0; i < state->RegistrationCount; i++) if (!Zero(state->Registrations + i, (ulong)sizeof(Registration))) return false;
        state->Descriptors->Epoll = null; state->Initialized = 2; return true;
    }
    internal static bool StorageOverlap(State* state, ulong start, ulong bytes) => state != null &&
        (Overlap(start, bytes, (ulong)state, (ulong)sizeof(State)) ||
         Overlap(start, bytes, (ulong)state->Ports, state->PortCount * (ulong)sizeof(Port)) ||
         Overlap(start, bytes, (ulong)state->Registrations, state->RegistrationCount * (ulong)sizeof(Registration)));
    private static bool Current(State* state) => Extent(state, (ulong)sizeof(State)) && state->Initialized == 1 &&
        state->Self == (ulong)state && Fds.Audit(state->Descriptors);
    private static bool Extent(void* pointer, ulong bytes) => pointer != null && ((ulong)pointer & 7) == 0 && bytes != 0 && bytes <= ~0UL - (ulong)pointer;
    private static bool Overlap(ulong a, ulong bytes, ulong b, ulong length) => bytes != 0 && length != 0 && a < b + length && b < a + bytes;
    private static bool Zero(void* pointer, ulong bytes) { for (ulong i = 0; i < bytes; i++) if (((byte*)pointer)[i] != 0) return false; return true; }
}
#endif
