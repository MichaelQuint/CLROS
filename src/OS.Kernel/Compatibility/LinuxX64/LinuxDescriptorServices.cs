using System;
using OS.Kernel.Memory;
using OS.Std.Text;
#if !NOSTDLIB_EFI || M16_027_PROBE
using Writable = OS.Kernel.Compatibility.LinuxX64.LinuxContinuingWritableDescriptorServices;
#endif

namespace OS.Kernel.Compatibility.LinuxX64;

/// <summary>
/// Linux descriptors for one IRQ-off process owner on the kernel root. All
/// tables, buffers, package views and callbacks are borrowed, supervisor-only,
/// exclusive and retained until End. Optional LinuxSocketServices shares the
/// same fd/description lifetime. End retains faulted/closing TCP ownership until
/// explicit closed reap. An explicit bounded writable-disk attachment supplies
/// its separate continuing file/directory operations. No allocation, firmware,
/// blocking wait, signal delivery or process creation is supplied here.
/// Negative Linux errno reports rejection. File cursors and pipe
/// reads commit only after complete copy-out; sinks must accept an entire copy.
/// Linux fd integers may be reused. Internal tokens additionally bind the owner
/// and generation, without changing Linux's integer descriptor semantics.
/// </summary>
public static unsafe partial class LinuxDescriptorServices
{
    public const uint MaximumDescriptors = 32, MaximumViews = 8, MaximumPipes = 4;
    public const uint MaximumPath = 256, MaximumVectors = 16, MaximumIo = 65536, SnapshotBytes = 8192;
    public const uint MaximumViewBytes = OS.Manifest.LinuxElfPackage.MaximumBytes;
    public const uint NonBlock = 0x800, CloseOnExec = 0x80000;
    private const uint LargeFile = 0x8000, NoFollow = 0x20000;
    private const long Missing = -2, Io = -5, BadDescriptor = -9, Fault = -14, Denied = -13;
    private const long Invalid = -22, FileTableFull = -23, DescriptorTableFull = -24;
    private const long NotDirectory = -20, NotTerminal = -25, NotSeekable = -29, ReadOnly = -30;
    private const long NameTooLong = -36, Unsupported = -38, Overflow = -75, NoMemory = -12;
    internal enum Kind : uint { None, Input, Output, Package, Snapshot, PipeRead, PipeWrite, Socket, Epoll
#if !NOSTDLIB_EFI || M16_025_PROBE
        , Disk
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
        , WritableDisk
#endif
    }

    /// <summary>Retained immutable package data, with an exact absolute ASCII
    /// path below /app/ and a nonzero owner-assigned inode at least 256. Paths
    /// contain no dot segments. The package owner verifies provenance and denies mutation;
    /// this adapter neither acquires files nor opens executable images.</summary>
    public struct View
    {
        public byte* Path;
        public uint PathBytes;
        public byte* Data;
        public ulong Bytes, Inode;
    }

    /// <summary>Caller-zeroed descriptor metadata. Flags are per descriptor;
    /// generation survives close so tokens cannot name a reused integer.</summary>
    public struct Slot
    {
        internal uint Description, Flags, Generation;
    }

    /// <summary>Caller-zeroed open descriptions. Duplicates share position,
    /// status flags and endpoint ownership. Only the last close releases them.</summary>
    public struct Description
    {
        internal uint Type, References, Flags, Pipe, Borrowers;
        internal byte* Data;
        internal ulong Bytes, Position, Inode;
        internal uint Channel;
    }

    /// <summary>Kernel-only borrowed token. It is not an OSHost handle or a
    /// Linux user ABI value and confers no access without IsCurrent.</summary>
    public struct Token
    {
        internal State* Owner;
        internal uint Number, Generation;
    }

    /// <summary>All mutable extents must be disjoint, zeroed and inaccessible
    /// to the user. Scratch is 8..64KiB; each open description has one 8KiB proc
    /// snapshot slot. Pipe buffers have a common power-of-two capacity. Resource
    /// facts are actual owned snapshots; AvailableBytes includes the memory
    /// pool, whose current commitments are deducted. SIGPIPE policy is explicit
    /// mechanical admission only, never a fabricated sigaction implementation.</summary>
    public struct Admission
    {
        public Slot* Slots;
        public Description* Descriptions;
        public LinuxPipe* Pipes;
        public byte* PipeBuffers;
        public byte* Snapshots;
        public byte* Scratch;
        public uint DescriptorCount, DescriptionCount, PipeCount, PipeCapacity, ScratchBytes;
        public View* Views;
        public uint ViewCount;
        /// <summary>Nonzero enables bounded package path observations for this
        /// actual process ID. Views must be produced in package order, executable
        /// first with inodes256+i. The caller binds this ID to its thread owner;
        /// zero retains the earlier descriptor-only contract. CWD is fixed /.</summary>
        public uint PackageProcessId;
        public LinuxMemoryServices.State* Memory;
        public ulong PhysicalBytes, AvailableBytes;
        public LinuxSyscallAccess User;
        public delegate* unmanaged<void*, ulong, byte*, uint, byte> CopyToUser;
        public bool FirmwareExited, SingleOwnerValidated, SigpipeIgnored;
    }

    /// <summary>One-use owner state. Never copy or concurrently change a live
    /// instance. End closes every remaining descriptor and scrubs owned buffers;
    /// it does not free caller storage or erase immutable package data.</summary>
    public struct State
    {
        internal Admission Owner;
        internal uint Initialized, Active, NextInode;
        internal ulong ThreadOwner;
#if !NOSTDLIB_EFI || M16_025_PROBE
        internal DiskState* Disk;
#endif
#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
        internal LinuxSocketServices.State* Sockets;
        internal LinuxEpollServices.State* Epoll;
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
        internal WritableDiskState* WritableDisk;
#endif
        public readonly uint ActiveDescriptors => Initialized == 1 ? Active : 0;
        /// <summary>Retained kernel I/O borrows, including closed fds. This is
        /// diagnostic accounting, not a pin or release grant.</summary>
        public readonly uint RetainedPipeOperations
        {
            get
            {
                if (Initialized != 1) return 0;
                uint count = 0;
                for (uint i = 0; i < Owner.DescriptionCount; i++) count += Owner.Descriptions[i].Borrowers;
                return count;
            }
        }
    }

    /// <summary>Admit empty storage and install fd0 EOF input plus fd1/fd2
    /// owned output channels. Complete failure leaves storage unchanged. The
    /// caller proves mappings, provenance and exclusive callback identities.</summary>
    public static bool Begin(State* state, Admission admission)
    {
        if (!Storage(state, admission) || state->Initialized != 0 || state->Active != 0 || state->NextInode != 0 || state->ThreadOwner != 0)
            return false;
#if !NOSTDLIB_EFI || M16_025_PROBE
        if (state->Disk != null) return false;
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
        if (state->WritableDisk != null) return false;
#endif
#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
        if (state->Sockets != null || state->Epoll != null) return false;
#endif
        for (uint i = 0; i < admission.DescriptorCount; i++)
            if (admission.Slots[i].Description != 0 || admission.Slots[i].Flags != 0 || admission.Slots[i].Generation != 0) return false;
        for (uint i = 0; i < admission.DescriptionCount; i++)
        {
            byte* bytes = (byte*)(admission.Descriptions + i);
            for (uint j = 0; j < sizeof(Description); j++) if (bytes[j] != 0) return false;
        }
        for (uint i = 0; i < admission.PipeCount * sizeof(LinuxPipe); i++) if (((byte*)admission.Pipes)[i] != 0) return false;
        state->Owner = admission; state->ThreadOwner = 0; state->Initialized = 1; state->NextInode = 0x10000;
        Clear(admission.Scratch, admission.ScratchBytes);
        Clear(admission.Snapshots, admission.DescriptionCount * SnapshotBytes);
        Clear(admission.PipeBuffers, admission.PipeCount * admission.PipeCapacity);
        for (uint i = 0; i < 3; i++)
        {
            admission.Descriptions[i] = new() { Type = (uint)(i == 0 ? Kind.Input : Kind.Output), References = 1,
                Flags = i == 0 ? 0U : 1U, Inode = i + 1, Channel = i };
            Assign(state, i, i, 0);
        }
        return true;
    }

    /// <summary>Dispatch measured read/write/open/close/stat/seek/ioctl/pread,
    /// writev, fcntl, pipe and attached socket calls. Unknown calls return ENOSYS. The gateway
    /// retains call limits, termination and user-return validation.</summary>
    public static long Dispatch(State* state, LinuxSyscallFrame frame)
        => state == null || ((ulong)state & 7) != 0 || state->ThreadOwner != 0 || !Audit(state) ? Fault : DispatchCore(state, frame);

    // Both entry points audit before this private body. DispatchThreads also
    // checks the exact thread owner, selects nonblocking/non-pipe calls and takes
    // a read-only pipe snapshot. Neither path mutates state or invokes a callback
    // between admission and this call; no audit survives a later call.
    private static long DispatchCore(State* state, LinuxSyscallFrame frame)
    {
        uint fd = unchecked((uint)frame.Argument0);
        long value;
        switch (frame.Number)
        {
            case 0: value = Read(state, fd, frame.Argument1, frame.Argument2, 0, false); break;
            case 1: value = Write(state, fd, frame.Argument1, frame.Argument2); break;
            case 2: value = Open(state, 0xffffff9c, frame.Argument0, frame.Argument1); break;
            case 3: value = Close(state, fd); break;
            case 4: case 6: value = PackagePathStat(state, frame.Argument0, frame.Argument1, frame.Number == 6); break;
            case 5: value = Stat(state, fd, frame.Argument1); break;
            case 8: value = Seek(state, fd, unchecked((long)frame.Argument1), unchecked((uint)frame.Argument2)); break;
            case 16: value = SocketIoctl(state, fd, frame.Argument1, frame.Argument2); break;
            case 17: value = Read(state, fd, frame.Argument1, frame.Argument2, frame.Argument3, true); break;
            case 20: value = WriteVector(state, fd, frame.Argument1, unchecked((uint)frame.Argument2)); break;
            case 22: value = Pipe(state, frame.Argument0, 0); break;
            case 72: value = Control(state, fd, unchecked((uint)frame.Argument1), frame.Argument2); break;
            case 79: value = PackageGetCwd(state, frame.Argument0, frame.Argument1); break;
            case 89: value = PackageReadLink(state, frame.Argument0, frame.Argument1, frame.Argument2); break;
            case 257: value = Open(state, fd, frame.Argument1, frame.Argument2); break;
            case 293: value = Pipe(state, frame.Argument0, frame.Argument1); break;
#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
            case 41: case 42: case 43: case 44: case 45: case 46: case 47: case 48: case 49: case 50: case 51: case 52: case 54: case 55: case 288:
                value = state->Sockets == null ? Unsupported : LinuxSocketServices.Dispatch(state->Sockets, frame); break;
            case 213: case 232: case 233: case 281: case 291:
                value = state->Epoll == null ? Unsupported : LinuxEpollServices.Dispatch(state->Epoll, frame); break;
#endif
            default: value = Unsupported; break;
        }
        Clear(state->Owner.Scratch, state->Owner.ScratchBytes);
#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
        if (state->Epoll != null && !LinuxEpollServices.Refresh(state->Epoll)) return Fault;
#endif
        return value;
    }

    /// <summary>Capture a kernel reference to a currently open fd. A failed
    /// capture returns an empty token. Linux integers themselves carry no token.</summary>
    public static bool Capture(State* state, uint fd, out Token token)
    {
        token = default;
        if (!Audit(state) || Find(state, fd) == null) return false;
        token = new() { Owner = state, Number = fd, Generation = state->Owner.Slots[fd].Generation };
        return true;
    }

    /// <summary>Validate owner, live descriptor and generation before later
    /// kernel use. Closing or reusing an fd invalidates its earlier token.</summary>
    public static bool IsCurrent(State* state, Token token) => Audit(state) && token.Owner == state &&
        Find(state, token.Number) != null && state->Owner.Slots[token.Number].Generation == token.Generation;

    /// <summary>Observe whether closing this exact live fd would drop its
    /// description's final reference, including pending kernel borrows. The
    /// serialized owner must keep the observation adjacent to its actual close;
    /// it grants no mutation, endpoint retirement or resource-release authority.
    /// Stale/foreign tokens, duplicates and retained I/O borrows return false.</summary>
    public static bool IsLastReference(State* state, Token token)
    {
        if (!IsCurrent(state, token)) return false;
        Description* description = Find(state, token.Number);
        return description->References == 1 && description->Borrowers == 0;
    }

    /// <summary>Release all live endpoints and scrub mutable buffers after
    /// user termination. A consumed/invalid owner fails; no process exit, signal
    /// delivery or deallocation is performed here.</summary>
    public static bool End(State* state)
    {
        if (!Audit(state) || state->ThreadOwner != 0) return false;
#if !NOSTDLIB_EFI || M16_027_PROBE
        if (!CanEndWritableDisk(state)) return false;
#endif
        for (uint i = 0; i < state->Owner.DescriptionCount; i++) if (state->Owner.Descriptions[i].Borrowers != 0) return false;
        for (uint i = 0; i < state->Owner.DescriptorCount; i++)
            if (state->Owner.Slots[i].Description != 0 && Close(state, i) != 0) return false;
#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
        if (state->Sockets != null && !LinuxSocketServices.Finish(state->Sockets)) return false;
        if (state->Epoll != null && !LinuxEpollServices.Finish(state->Epoll)) return false;
#endif
#if !NOSTDLIB_EFI || M16_025_PROBE
        if (state->Disk != null && !DetachDisk(state)) return false;
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
        if (state->WritableDisk != null && !DetachWritableDisk(state)) return false;
#endif
        Clear(state->Owner.Scratch, state->Owner.ScratchBytes);
        Clear(state->Owner.Snapshots, state->Owner.DescriptionCount * SnapshotBytes);
        Clear(state->Owner.PipeBuffers, state->Owner.PipeCount * state->Owner.PipeCapacity);
        state->Initialized = 2;
        return true;
    }

    /// <summary>Check descriptor references and endpoint accounting under the
    /// same exclusive ownership. This does not prove supervisor mapping or
    /// package provenance; those remain admission responsibilities.</summary>
    public static bool Audit(State* state)
    {
        if (state == null || ((ulong)state & 7) != 0 || state->Initialized != 1) return false;
        var a = state->Owner;
        if (!Storage(state, a) || state->Active > a.DescriptorCount) return false;
        uint active = 0;
        for (uint i = 0; i < a.DescriptorCount; i++)
        {
            Slot slot = a.Slots[i];
            if (slot.Description == 0) { if (slot.Flags != 0) return false; continue; }
            if (slot.Description > a.DescriptionCount || slot.Generation == 0 || (slot.Flags & ~1U) != 0) return false;
            active++;
        }
        if (active != state->Active) return false;
        for (uint i = 0; i < a.DescriptionCount; i++)
        {
            Description d = a.Descriptions[i]; uint references = 0;
            for (uint j = 0; j < a.DescriptorCount; j++) if (a.Slots[j].Description == i + 1) references++;
            if (d.References != references) return false;
            if (references == 0 && d.Borrowers == 0) { if (d.Type != 0) return false; continue; }
            bool borrowedKind = (Kind)d.Type is Kind.PipeRead or Kind.PipeWrite or Kind.Epoll;
            uint maximumKind = (uint)Kind.Epoll;
#if !NOSTDLIB_EFI || M16_025_PROBE
            borrowedKind |= (Kind)d.Type == Kind.Disk;
            maximumKind = (uint)Kind.Disk;
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
            borrowedKind |= (Kind)d.Type == Kind.WritableDisk;
            maximumKind = (uint)Kind.WritableDisk;
#endif
            if (d.Borrowers > MaximumDescriptors || d.Borrowers != 0 && (!borrowedKind || state->ThreadOwner == 0)) return false;
            if (d.Type == 0 || d.Type > maximumKind || d.Inode == 0 ||
                (d.Flags & ~(NonBlock | LargeFile | NoFollow | 3U)) != 0 || d.Position > long.MaxValue ||
                ((Kind)d.Type is Kind.Socket or Kind.Epoll ? (d.Flags & 3) != 2 :
#if !NOSTDLIB_EFI || M16_027_PROBE
                (Kind)d.Type == Kind.WritableDisk ? (d.Flags & 3) == 3 :
#endif
                (d.Flags & 2) != 0)) return false;
#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
            if ((Kind)d.Type == Kind.Socket && state->Sockets == null) return false;
            if ((Kind)d.Type == Kind.Epoll && state->Epoll == null) return false;
#else
            if ((Kind)d.Type is Kind.Socket or Kind.Epoll) return false;
#endif
#if !NOSTDLIB_EFI || M16_025_PROBE
            if ((Kind)d.Type == Kind.Disk && state->Disk == null) return false;
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
            if ((Kind)d.Type == Kind.WritableDisk && state->WritableDisk == null) return false;
#endif
            if ((Kind)d.Type == Kind.Package || (Kind)d.Type == Kind.Snapshot)
            {
                if ((d.Flags & 1) != 0 || d.Bytes > long.MaxValue || !Extent(d.Data, d.Bytes, true)) return false;
                if ((Kind)d.Type == Kind.Snapshot && (d.Data != a.Snapshots + i * SnapshotBytes || d.Bytes > SnapshotBytes)) return false;
            }
            if ((Kind)d.Type == Kind.PipeRead || (Kind)d.Type == Kind.PipeWrite)
                if (d.Pipe >= a.PipeCount || !a.Pipes[d.Pipe].Initialized ||
                    (d.Flags & 1) != ((Kind)d.Type == Kind.PipeWrite ? 1U : 0U)) return false;
        }
        for (uint i = 0; i < a.PipeCount; i++)
        {
            uint readers = 0, writers = 0;
            for (uint j = 0; j < a.DescriptionCount; j++)
            {
                Description d = a.Descriptions[j];
                if (d.References == 0 && d.Borrowers == 0 || d.Pipe != i) continue;
                if ((Kind)d.Type == Kind.PipeRead) readers++;
                if ((Kind)d.Type == Kind.PipeWrite) writers++;
            }
            LinuxPipe* p = a.Pipes + i;
            if (readers > 1 || writers > 1 || p->ReaderCount != readers || p->WriterCount != writers ||
                p->Initialized != (readers + writers != 0) ||
                (p->Initialized && (p->Capacity != a.PipeCapacity || p->BufferedBytes > p->Capacity))) return false;
        }
#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
        if (state->Sockets != null && !LinuxSocketServices.Audit(state->Sockets, state)) return false;
        if (state->Epoll != null && !LinuxEpollServices.Audit(state->Epoll, state)) return false;
#endif
#if !NOSTDLIB_EFI || M16_025_PROBE
        if (!AuditDisk(state)) return false;
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
        if (!AuditWritableDisk(state)) return false;
#endif
#if (!NOSTDLIB_EFI || M16_025_PROBE) && (!NOSTDLIB_EFI || M16_027_PROBE)
        if (!AuditDiskPair(state)) return false;
#endif
        return true;
    }

    private static long Read(State* state, uint fd, ulong address, ulong requested, ulong offset, bool positioned)
    {
        Description* d = Find(state, fd);
        if (d == null || (d->Flags & 1) != 0) return BadDescriptor;
#if !NOSTDLIB_EFI || M16_025_PROBE
        if ((Kind)d->Type == Kind.Disk) return Unsupported; // Requires an owned disk continuation.
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
        if ((Kind)d->Type == Kind.WritableDisk) return WritableUnsupported;
#endif
        if ((Kind)d->Type == Kind.Epoll) return Invalid;
        if (positioned && (Kind)d->Type != Kind.Package && (Kind)d->Type != Kind.Snapshot) return NotSeekable;
        if (requested > MaximumIo || (positioned && offset > long.MaxValue)) return Invalid;
        if (requested == 0) return 0;
#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
        if ((Kind)d->Type == Kind.Socket) return LinuxSocketServices.Receive(state->Sockets, d->Pipe, address, requested, 0);
#endif
        var a = state->Owner;
        if ((Kind)d->Type == Kind.Input) return 0; // Explicit appliance stdin EOF policy.
        uint bytes = (uint)(requested < a.ScratchBytes ? requested : a.ScratchBytes);
        if ((Kind)d->Type == Kind.PipeRead)
        {
            if ((d->Flags & NonBlock) == 0) return Unsupported;
            long peek = a.Pipes[d->Pipe].Peek(a.Scratch, bytes, true);
            if (peek <= 0) return peek;
            bytes = (uint)peek;
            if (!Output(state, address, a.Scratch, bytes)) return Fault;
            return a.Pipes[d->Pipe].Consume(bytes) ? bytes : Io;
        }
        ulong position = positioned ? offset : d->Position;
        if (position >= d->Bytes) return 0;
        if (bytes > d->Bytes - position) bytes = (uint)(d->Bytes - position);
        if (!Output(state, address, d->Data + position, bytes)) return Fault;
        if (!positioned) d->Position += bytes;
        return bytes;
    }

    private static long Write(State* state, uint fd, ulong address, ulong requested)
    {
        Description* d = Find(state, fd);
        if (d == null || (d->Flags & 3) == 0) return BadDescriptor;
#if !NOSTDLIB_EFI || M16_027_PROBE
        if ((Kind)d->Type == Kind.WritableDisk) return WritableUnsupported;
#endif
        if (requested > MaximumIo) return Invalid;
        if (requested == 0) return 0;
        long policy = WritePolicy(state, d);
        if (policy != 0) return policy;
        var a = state->Owner;
        uint bytes = (uint)(requested < a.ScratchBytes ? requested : a.ScratchBytes);
        if (!LinuxSyscallGateway.ValidateRange(address, requested, LinuxUserPageAccess.Read, a.User) ||
            a.User.CopyFromUser(a.User.Context, address, a.Scratch, bytes) != 1) return Fault;
        return CommitWrite(state, d, bytes);
    }

    private static long WriteVector(State* state, uint fd, ulong address, uint count)
    {
        Description* d = Find(state, fd);
        if (d == null || (d->Flags & 3) == 0) return BadDescriptor;
#if !NOSTDLIB_EFI || M16_027_PROBE
        if ((Kind)d->Type == Kind.WritableDisk) return WritableUnsupported;
#endif
        if (count > MaximumVectors) return Invalid;
        if (count == 0) return 0;
        var a = state->Owner;
        ulong* vectors = stackalloc ulong[(int)MaximumVectors * 2];
        if (!LinuxSyscallGateway.ValidateRange(address, count * 16UL, LinuxUserPageAccess.Read, a.User) ||
            a.User.CopyFromUser(a.User.Context, address, (byte*)vectors, count * 16) != 1) return Fault;
        ulong total = 0;
        for (uint i = 0; i < count; i++)
        {
            ulong bytes = vectors[i * 2 + 1];
            if (bytes > MaximumIo - total) return Invalid;
            total += bytes;
            if (bytes != 0 && !LinuxSyscallGateway.ValidateRange(vectors[i * 2], bytes, LinuxUserPageAccess.Read, a.User)) return Fault;
        }
        if (total == 0) return 0;
        long policy = WritePolicy(state, d);
        if (policy != 0) return policy;
        uint wanted = (uint)(total < a.ScratchBytes ? total : a.ScratchBytes), written = 0;
        for (uint i = 0; i < count && written < wanted; i++)
        {
            uint bytes = (uint)(vectors[i * 2 + 1] < wanted - written ? vectors[i * 2 + 1] : wanted - written);
            if (bytes != 0 && a.User.CopyFromUser(a.User.Context, vectors[i * 2], a.Scratch + written, bytes) != 1) return Fault;
            written += bytes;
        }
        return CommitWrite(state, d, written);
    }

    private static long WritePolicy(State* state, Description* d)
    {
#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
        if ((Kind)d->Type == Kind.Socket) return LinuxSocketServices.WritePolicy(state->Sockets, d->Pipe, 0);
#endif
        if ((Kind)d->Type != Kind.PipeWrite) return (Kind)d->Type == Kind.Output ? 0 : BadDescriptor;
        if ((d->Flags & NonBlock) == 0) return Unsupported;
        if (state->Owner.Pipes[d->Pipe].ReaderCount == 0) return state->Owner.SigpipeIgnored ? -32 : Unsupported;
        return 0;
    }

    private static long CommitWrite(State* state, Description* d, uint bytes) =>
#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
        (Kind)d->Type == Kind.Socket
        ? LinuxSocketServices.SendCopied(state->Sockets, d->Pipe, bytes)
        :
#endif
        (Kind)d->Type == Kind.PipeWrite
        ? state->Owner.Pipes[d->Pipe].Write(state->Owner.Scratch, bytes, true, state->Owner.SigpipeIgnored)
        : state->Owner.User.Write(state->Owner.User.Context, d->Channel, state->Owner.Scratch, bytes) == 1 ? bytes : Io;

    private static long Open(State* state, uint dirfd, ulong address, ulong flags)
    {
        var a = state->Owner; byte* path = stackalloc byte[(int)MaximumPath]; uint length = 0;
        for (; length < MaximumPath; length++)
        {
            if (address > ulong.MaxValue - length || !LinuxSyscallGateway.ValidateRange(address + length, 1, LinuxUserPageAccess.Read, a.User) ||
                a.User.CopyFromUser(a.User.Context, address + length, path + length, 1) != 1) return Fault;
            if (path[length] == 0) break;
        }
        if (length == MaximumPath) return NameTooLong;
        if (length == 0) return Missing;
        bool absolute = path[0] == '/';
        if (!absolute && dirfd != 0xffffff9c) return Find(state, dirfd) == null ? BadDescriptor : NotDirectory;
        uint start = absolute ? 1U : 0U;
        if (!AsciiPointerMatcher.IsRelativePath(path + start, length - start)) return Denied;
        if ((flags & (3UL | 0x40 | 0x200 | 0x400)) != 0) return ReadOnly;
        // System.Native adds O_NOCTTY even for immutable regular files. It
        // grants no terminal ownership and is not a retained file-status flag.
        if ((flags & ~(NonBlock | CloseOnExec | LargeFile | NoFollow | 0x100U)) != 0) return Invalid;
        uint view = a.ViewCount, special = 0;
        // /proc observations are snapshots of this owner's resources and VMAs.
        if (AsciiPointerMatcher.Matches(path + start, length - start, 12, 0x6d656d2f636f7270, 0x006f666e69)) special = 1;
        else if (AsciiPointerMatcher.Matches(path + start, length - start, 14, 0x6c65732f636f7270, 0x7370616d2f66)) special = 2;
        else for (uint i = 0; i < a.ViewCount; i++)
        {
            View v = a.Views[i];
            if (length - start != v.PathBytes - 1) continue;
            bool same = true;
            for (uint j = 0; j < length - start; j++) if (path[start + j] != v.Path[1 + j]) { same = false; break; }
            if (same) { view = i; break; }
        }
        if (special == 0 && view == a.ViewCount) return Missing;
        uint fd = FreeSlot(state, 0, uint.MaxValue), description = FreeDescription(state, uint.MaxValue);
        if (fd == a.DescriptorCount) return DescriptorTableFull;
        if (description == a.DescriptionCount) return FileTableFull;
        byte* data; ulong bytes, inode;
        if (special != 0)
        {
            data = a.Snapshots + description * SnapshotBytes;
            uint size = Snapshot(state, data, special);
            if (size == 0) { Clear(data, SnapshotBytes); return NoMemory; }
            bytes = size; inode = 16 + special;
        }
        else { View v = a.Views[view]; data = v.Data; bytes = v.Bytes; inode = v.Inode; }
        a.Descriptions[description] = new() { Type = (uint)(special != 0 ? Kind.Snapshot : Kind.Package), References = 1,
            // Native Linux x64 open/openat force O_LARGEFILE. O_NOFOLLOW is
            // retained in F_GETFL; this namespace has no symlink traversal.
            Flags = ((uint)flags & (NonBlock | NoFollow)) | LargeFile, Data = data, Bytes = bytes, Inode = inode };
        Assign(state, fd, description, (flags & CloseOnExec) != 0 ? 1U : 0U);
        return fd;
    }

    private static long Close(State* state, uint fd)
    {
        Description* d = Find(state, fd);
        if (d == null) return BadDescriptor;
#if !NOSTDLIB_EFI || M16_027_PROBE
        if ((Kind)d->Type == Kind.WritableDisk)
        { long closed = CloseWritableDiskFd(state, fd); if (closed != 0) return closed; }
#endif
        var a = state->Owner; uint index = a.Slots[fd].Description - 1;
        if (!DropDescription(state, index, false)) return Io;
        a.Slots[fd].Description = 0; a.Slots[fd].Flags = 0; state->Active--;
        return 0;
    }

    internal static bool DropDescription(State* state, uint index, bool borrow)
    {
        var a = state->Owner; Description* d = a.Descriptions + index;
        if (borrow ? d->Borrowers == 0 : d->References == 0) return false;
        bool last = borrow ? d->Borrowers == 1 && d->References == 0 : d->References == 1 && d->Borrowers == 0;
#if !NOSTDLIB_EFI || M16_027_PROBE
        if (last && (Kind)d->Type == Kind.WritableDisk && !RetireWritableBinding(state, index)) return false;
#endif
#if !NOSTDLIB_EFI || M16_025_PROBE
        if (last && (Kind)d->Type == Kind.Disk && !CloseDiskDescription(state, index)) return false;
#endif
#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
        if (last && (Kind)d->Type == Kind.Socket && !LinuxSocketServices.Close(state->Sockets, d->Pipe)) return false;
        if (last && state->Epoll != null) LinuxEpollServices.DropDescription(state->Epoll, index, d->Inode, (Kind)d->Type == Kind.Epoll, d->Pipe);
#endif
        if (last && ((Kind)d->Type == Kind.PipeRead || (Kind)d->Type == Kind.PipeWrite))
        {
            LinuxPipe* pipe = a.Pipes + d->Pipe;
            bool closed = (Kind)d->Type == Kind.PipeRead ? pipe->CloseReader() : pipe->CloseWriter();
            if (!closed) return false;
            if (pipe->ReaderCount == 0 && pipe->WriterCount == 0 && !pipe->Release()) return false;
        }
        if (borrow) d->Borrowers--; else d->References--;
        if (d->References == 0 && d->Borrowers == 0)
        {
            if ((Kind)d->Type == Kind.Snapshot) Clear(a.Snapshots + index * SnapshotBytes, SnapshotBytes);
            *d = default;
        }
        return true;
    }

    private static long Control(State* state, uint fd, uint command, ulong argument)
    {
        Description* d = Find(state, fd);
        if (d == null) return BadDescriptor;
#if !NOSTDLIB_EFI || M16_027_PROBE
        if ((Kind)d->Type == Kind.WritableDisk && command is not (1 or 2 or 3)) return WritableUnsupported;
#endif
        var a = state->Owner;
        switch (command)
        {
            case 1: return a.Slots[fd].Flags;
            case 2:
                if ((argument & ~1UL) != 0) return Invalid;
                a.Slots[fd].Flags = (uint)argument; return 0;
            case 3: return d->Flags;
            case 4:
                if ((argument & ~(NonBlock | LargeFile | NoFollow | 3UL)) != 0) return Invalid;
                d->Flags = (d->Flags & ~NonBlock) | ((uint)argument & NonBlock); return 0;
            case 0: case 1030:
                int minimum = unchecked((int)argument);
                if (minimum < 0 || (uint)minimum >= a.DescriptorCount) return Invalid;
                uint target = FreeSlot(state, (uint)minimum, uint.MaxValue);
                if (target == a.DescriptorCount) return DescriptorTableFull;
                d->References++; Assign(state, target, a.Slots[fd].Description - 1, command == 1030 ? 1U : 0U);
                return target;
            default: return Invalid;
        }
    }

    private static long Seek(State* state, uint fd, long offset, uint whence)
    {
        Description* d = Find(state, fd);
        if (d == null) return BadDescriptor;
#if !NOSTDLIB_EFI || M16_025_PROBE
        if ((Kind)d->Type == Kind.Disk) return Unsupported;
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
        if ((Kind)d->Type == Kind.WritableDisk) return WritableUnsupported;
#endif
        if ((Kind)d->Type != Kind.Package && (Kind)d->Type != Kind.Snapshot) return NotSeekable;
        if (whence > 2) return Invalid;
        ulong origin = whence == 0 ? 0 : whence == 1 ? d->Position : d->Bytes, next;
        if (offset < 0)
        {
            ulong magnitude = (ulong)(-(offset + 1)) + 1;
            if (magnitude > origin) return Invalid;
            next = origin - magnitude;
        }
        else
        {
            if ((ulong)offset > (ulong)long.MaxValue - origin) return Overflow;
            next = origin + (ulong)offset;
        }
        d->Position = next; return (long)next;
    }

    private static long Stat(State* state, uint fd, ulong address)
    {
        Description* d = Find(state, fd);
        if (d == null) return BadDescriptor;
#if !NOSTDLIB_EFI || M16_025_PROBE
        if ((Kind)d->Type == Kind.Disk) return Unsupported;
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
        if ((Kind)d->Type == Kind.WritableDisk) return WritableUnsupported;
#endif
        byte* wire = stackalloc byte[144]; Clear(wire, 144);
        *(ulong*)(wire + 8) = d->Inode; *(ulong*)(wire + 16) = 1;
        Kind kind = (Kind)d->Type;
        *(uint*)(wire + 24) = kind == Kind.Package || kind == Kind.Snapshot ? 0x8124U :
            kind == Kind.PipeRead || kind == Kind.PipeWrite ? 0x1180U : kind == Kind.Socket ? 0xc180U : 0x2180U;
        *(ulong*)(wire + 48) = kind == Kind.Package || kind == Kind.Snapshot ? d->Bytes : 0;
        *(ulong*)(wire + 56) = 4096;
        // Package bytes are an immutable in-memory view, not allocated disk
        // sectors. No wall-clock/timestamps, host device IDs or disk blocks exist.
        return Output(state, address, wire, 144) ? 0 : Fault;
    }

    private static long Pipe(State* state, ulong address, ulong flags)
    {
        if ((flags & ~(NonBlock | CloseOnExec)) != 0) return Invalid;
        var a = state->Owner;
        if (!LinuxSyscallGateway.ValidateRange(address, 8, LinuxUserPageAccess.Write, a.User)) return Fault;
        uint read = FreeSlot(state, 0, uint.MaxValue), write = FreeSlot(state, 0, read);
        if (read == a.DescriptorCount || write == a.DescriptorCount) return DescriptorTableFull;
        uint rd = FreeDescription(state, uint.MaxValue), wr = FreeDescription(state, rd);
        if (rd == a.DescriptionCount || wr == a.DescriptionCount) return FileTableFull;
        uint pipe = a.PipeCount;
        for (uint i = 0; i < a.PipeCount; i++) if (!a.Pipes[i].Initialized) { pipe = i; break; }
        if (pipe == a.PipeCount || state->NextInode == uint.MaxValue) return FileTableFull;
        if (!a.Pipes[pipe].Initialize(a.PipeBuffers + pipe * a.PipeCapacity, a.PipeCapacity)) return Io;
        uint* output = stackalloc uint[2]; output[0] = read; output[1] = write;
        if (a.CopyToUser(a.User.Context, address, (byte*)output, 8) != 1)
        {
            a.Pipes[pipe].CloseReader(); a.Pipes[pipe].CloseWriter(); a.Pipes[pipe].Release();
            return Fault;
        }
        uint inode = state->NextInode++;
        a.Descriptions[rd] = new() { Type = (uint)Kind.PipeRead, References = 1, Flags = (uint)flags & NonBlock, Pipe = pipe, Inode = inode };
        a.Descriptions[wr] = new() { Type = (uint)Kind.PipeWrite, References = 1, Flags = ((uint)flags & NonBlock) | 1, Pipe = pipe, Inode = inode };
        Assign(state, read, rd, (flags & CloseOnExec) != 0 ? 1U : 0U);
        Assign(state, write, wr, (flags & CloseOnExec) != 0 ? 1U : 0U);
        return 0;
    }

    private struct Mapping { public ulong Start, End; public uint Protection; }
    private static uint Snapshot(State* state, byte* destination, uint special)
    {
        Span<byte> text = new(destination, (int)SnapshotBytes); int offset = 0;
        var a = state->Owner; var m = a.Memory->Memory;
        if (special == 1)
        {
            ulong free = (a.AvailableBytes - a.Memory->BackedPages * 4096UL) / 1024;
            if (!Packed(text, ref offset, 0x6c61746f546d654d, 0x203a, 10) || !Decimal(text, ref offset, a.PhysicalBytes / 1024) ||
                !Packed(text, ref offset, 0x0a426b20, 0, 4) || !Packed(text, ref offset, 0x3a656572466d654d, 0x20, 9) ||
                !Decimal(text, ref offset, free) || !Packed(text, ref offset, 0x0a426b20, 0, 4) ||
                !Packed(text, ref offset, 0x6c696176416d654d, 0x203a656c6261, 14) || !Decimal(text, ref offset, free) ||
                !Packed(text, ref offset, 0x0a426b20, 0, 4)) return 0;
            return (uint)offset;
        }
        Mapping* mappings = stackalloc Mapping[(int)(X64UserAddressSpace.MaximumRanges + LinuxMemoryServices.MaximumRegions)];
        uint count = 0;
        for (uint i = 0; i < m.StaticCount; i++) if (m.StaticRanges[i].User)
        {
            var range = m.StaticRanges[i];
            mappings[count++] = new() { Start = range.VirtualStart, End = range.VirtualStart + range.Pages * 4096, Protection = (uint)range.Protection };
        }
        for (uint i = 0; i < a.Memory->Count; i++)
            mappings[count++] = new() { Start = a.Memory->Regions[i * 4], End = a.Memory->Regions[i * 4] + a.Memory->Regions[i * 4 + 1],
                Protection = (uint)(a.Memory->Regions[i * 4 + 3] & 7) };
        for (uint i = 1; i < count; i++)
        {
            Mapping current = mappings[i]; uint j = i;
            while (j != 0 && mappings[j - 1].Start > current.Start) { mappings[j] = mappings[j - 1]; j--; }
            mappings[j] = current;
        }
        byte* permissions = stackalloc byte[6];
        for (uint i = 0; i < count; i++)
        {
            Mapping range = mappings[i];
            var first = AsciiTextBuilder.AppendUInt64Hex(text, offset, range.Start, 8, false);
            if (!first.Succeeded) return 0; offset = first.NextOffset;
            if (!Packed(text, ref offset, (byte)'-', 0, 1)) return 0;
            var last = AsciiTextBuilder.AppendUInt64Hex(text, offset, range.End, 8, false);
            if (!last.Succeeded) return 0; offset = last.NextOffset;
            permissions[0] = (byte)' ';
            permissions[1] = (byte)((range.Protection & 1) != 0 ? 'r' : '-');
            permissions[2] = (byte)((range.Protection & 2) != 0 ? 'w' : '-');
            permissions[3] = (byte)((range.Protection & 4) != 0 ? 'x' : '-');
            permissions[4] = (byte)'p'; permissions[5] = (byte)' ';
            var p = AsciiTextBuilder.AppendBytes(text, offset, new ReadOnlySpan<byte>(permissions, 6));
            if (!p.Succeeded) return 0; offset = p.NextOffset;
            if (!Packed(text, ref offset, 0x3030303030303030, 0x2030303a303020, 15) ||
                !Packed(text, ref offset, 0x0a30, 0, 2)) return 0;
        }
        return (uint)offset;
    }

    private static bool Packed(Span<byte> target, ref int offset, ulong first, ulong second, uint count)
    {
        byte* bytes = stackalloc byte[16]; *(ulong*)bytes = first; *(ulong*)(bytes + 8) = second;
        var result = AsciiTextBuilder.AppendBytes(target, offset, new ReadOnlySpan<byte>(bytes, (int)count));
        if (!result.Succeeded) return false;
        offset = result.NextOffset; return true;
    }
    private static bool Decimal(Span<byte> target, ref int offset, ulong value)
    {
        var result = AsciiTextBuilder.AppendUInt64Decimal(target, offset, value);
        if (!result.Succeeded) return false;
        offset = result.NextOffset; return true;
    }
    internal static bool Output(State* state, ulong address, byte* source, uint bytes) =>
        LinuxSyscallGateway.ValidateRange(address, bytes, LinuxUserPageAccess.Write, state->Owner.User) &&
        state->Owner.CopyToUser(state->Owner.User.Context, address, source, bytes) == 1;
    internal static Description* Find(State* state, uint fd) => fd < state->Owner.DescriptorCount && state->Owner.Slots[fd].Description != 0
        ? state->Owner.Descriptions + state->Owner.Slots[fd].Description - 1 : null;
    private static uint FreeSlot(State* state, uint start, uint exclude)
    {
        for (uint i = start; i < state->Owner.DescriptorCount; i++)
            if (i != exclude && state->Owner.Slots[i].Description == 0 && state->Owner.Slots[i].Generation != uint.MaxValue)
            {
#if !NOSTDLIB_EFI || M16_027_PROBE
                if (WritableSlotReserved(state, i)) continue;
#endif
                return i;
            }
        return state->Owner.DescriptorCount;
    }
    private static uint FreeDescription(State* state, uint exclude)
    {
        for (uint i = 0; i < state->Owner.DescriptionCount; i++)
            if (i != exclude && state->Owner.Descriptions[i].References == 0 && state->Owner.Descriptions[i].Borrowers == 0)
            {
#if !NOSTDLIB_EFI || M16_027_PROBE
                if (WritableDescriptionReserved(state, i)) continue;
#endif
                return i;
            }
        return state->Owner.DescriptionCount;
    }
    private static void Assign(State* state, uint fd, uint description, uint flags)
    {
        Slot* slot = state->Owner.Slots + fd;
        slot->Description = description + 1; slot->Flags = flags; slot->Generation++; state->Active++;
    }

    internal static bool SocketDescription(State* state, uint fd, out uint endpoint, out uint flags)
    {
        Description* d = Find(state, fd); endpoint = flags = 0;
        if (d == null || (Kind)d->Type != Kind.Socket) return false;
        endpoint = d->Pipe; flags = d->Flags; return true;
    }

    internal static long InstallSocket(State* state, uint endpoint, uint flags)
        => InstallEndpoint(state, endpoint, flags, Kind.Socket);

    internal static long InstallEndpoint(State* state, uint endpoint, uint flags, Kind kind)
    {
        uint fd = FreeSlot(state, 0, uint.MaxValue), index = FreeDescription(state, uint.MaxValue);
        if (fd == state->Owner.DescriptorCount) return DescriptorTableFull;
        if (index == state->Owner.DescriptionCount || state->NextInode == uint.MaxValue) return FileTableFull;
        state->Owner.Descriptions[index] = new() { Type = (uint)kind, References = 1,
            Flags = 2 | (flags & NonBlock), Pipe = endpoint, Inode = state->NextInode++ };
        Assign(state, fd, index, (flags & CloseOnExec) != 0 ? 1U : 0U); return fd;
    }

    internal static long SocketCapacity(State* state) => FreeSlot(state, 0, uint.MaxValue) == state->Owner.DescriptorCount
        ? DescriptorTableFull : FreeDescription(state, uint.MaxValue) == state->Owner.DescriptionCount || state->NextInode == uint.MaxValue ? FileTableFull : 0;

    internal static uint SocketReferences(State* state, uint endpoint, out uint flags)
    {
        uint count = 0; flags = 0;
        for (uint i = 0; i < state->Owner.DescriptionCount; i++)
        {
            Description d = state->Owner.Descriptions[i];
            if ((Kind)d.Type != Kind.Socket || d.Pipe != endpoint) continue;
            count++; flags = d.Flags;
        }
        return count;
    }

    private static long SocketIoctl(State* state, uint fd, ulong command, ulong address)
    {
        Description* d = Find(state, fd);
        if (d == null) return BadDescriptor;
        if ((Kind)d->Type != Kind.Socket) return NotTerminal;
        if (command != 0x5421) return NotTerminal; // FIONBIO, measured Linux x64 ABI.
        uint enabled;
        if (!LinuxSyscallGateway.ValidateRange(address, 4, LinuxUserPageAccess.Read, state->Owner.User) ||
            state->Owner.User.CopyFromUser(state->Owner.User.Context, address, (byte*)&enabled, 4) != 1) return Fault;
        d->Flags = (d->Flags & ~NonBlock) | (enabled != 0 ? NonBlock : 0); return 0;
    }

    // Socket metadata and transport backing are retained supervisor storage.
    // Check against descriptor, package and user-memory ownership before attachment.
    internal static bool SocketOutside(State* state, void* pointer, ulong bytes) =>
        SocketOutsideCore(state, pointer, bytes, writablePair: false);
    private static bool SocketOutsideCore(State* state, void* pointer, ulong bytes, bool writablePair)
    {
        if (!Extent(pointer, bytes)) return false;
#if !NOSTDLIB_EFI || M16_027_PROBE
        if (state->WritableDisk != null && !WritableDiskCanRetainStorage(state, (ulong)pointer, bytes)) return false;
#endif
        var a = state->Owner; ulong start = (ulong)pointer;
        ulong* extents = stackalloc ulong[24]
        {
            (ulong)state, (ulong)sizeof(State), (ulong)a.Slots, a.DescriptorCount * (ulong)sizeof(Slot),
            (ulong)a.Descriptions, a.DescriptionCount * (ulong)sizeof(Description), (ulong)a.Pipes, a.PipeCount * (ulong)sizeof(LinuxPipe),
            (ulong)a.PipeBuffers, a.PipeCount * a.PipeCapacity, (ulong)a.Snapshots, a.DescriptionCount * SnapshotBytes,
            (ulong)a.Scratch, a.ScratchBytes, (ulong)a.Memory, (ulong)sizeof(LinuxMemoryServices.State),
            (ulong)a.Memory->Memory.Pool, a.Memory->Memory.PoolPages * 4096UL,
            (ulong)a.Memory->Memory.Tables, a.Memory->Memory.TablePages * 4096UL,
            (ulong)a.Memory->Memory.CandidateTables, a.Memory->Memory.TablePages * 4096UL,
            (ulong)a.Memory->Memory.StaticRanges, a.Memory->Memory.StaticCount * (ulong)sizeof(X64UserMappingRange)
        };
        for (uint i = 0; i < 12; i++) if (Overlap(start, bytes, extents[i * 2], extents[i * 2 + 1])) return false;
        if (Overlap(start, bytes, (ulong)a.Views, a.ViewCount * (ulong)sizeof(View))) return false;
#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
        if (LinuxSocketServices.StorageOverlap(state->Sockets, start, bytes) || LinuxEpollServices.StorageOverlap(state->Epoll, start, bytes)) return false;
#if !NOSTDLIB_EFI || M16_027_PROBE
        if (writablePair && !SocketCanRetainStorage(state, start, bytes)) return false;
#endif
#endif
#if !NOSTDLIB_EFI || M16_025_PROBE
#if !NOSTDLIB_EFI || M16_027_PROBE
        if (state->Disk != null && BorrowedDiskMount(state->Disk->Mount) && !DiskCanRetainStorage(state, start, bytes)) return false;
#endif
        if (state->Disk != null)
            for (uint i = 0; i < DiskExtentCount; i++)
            {
                DiskExtentAt(state, i, out ulong held, out ulong heldBytes);
                if (heldBytes != 0 && Overlap(start, bytes, held, heldBytes)) return false;
            }
#endif
        for (uint i = 0; i < a.ViewCount; i++)
            if (Overlap(start, bytes, (ulong)a.Views[i].Path, a.Views[i].PathBytes) ||
                Overlap(start, bytes, (ulong)a.Views[i].Data, a.Views[i].Bytes)) return false;
        return true;
    }

#if (!NOSTDLIB_EFI || M16_027_PROBE) && (!NOSTDLIB_EFI || CLROS_LINUX_SOCKETS)
    // Finite direct-TCP mode needs actual shared callback-carrier bounds.
    // This is a shape requirement, never device/process/mapping authority.
    internal static bool SocketRequiresBoundContext(State* state)
    {
        if (state->WritableDisk != null) return true;
#if !NOSTDLIB_EFI || M16_025_PROBE
        if (state->Disk != null && BorrowedDiskMount(state->Disk->Mount)) return true;
#endif
        return false;
    }

    // Complete socket/epoll tree only. Outer buffer owners combine this with
    // their base/user policy and both disk retained-tree queries. No callbacks,
    // I/O or global Audit recursion; serialized shape proof precedes inventory.
    internal static bool SocketCanRetainStorage(State* state, ulong address, ulong bytes)
    {
        if (!Extent(state, (ulong)sizeof(State)) || !Extent((void*)address, bytes)) return false;
        if (state->Sockets == null) return state->Epoll == null;
        if (!TransportShape(state)) return false;
        for (uint i = 0; i < TransportExtentCount; i++)
        {
            TransportExtentAt(state, i, out ulong start, out ulong count);
            if (count != 0 && Overlap(address, bytes, start, count)) return false;
        }
        return true;
    }

    private const uint TransportExtentCount = LinuxSocketServices.RetainedExtentCount + 3;
    private static bool TransportShape(State* state) =>
        LinuxSocketServices.InspectRetainedStorage(state->Sockets, state) &&
        (state->Epoll == null || LinuxEpollServices.Audit(state->Epoll, state));

    private static void TransportExtentAt(State* state, uint index, out ulong start, out ulong bytes)
    {
        start = bytes = 0;
        if (index < LinuxSocketServices.RetainedExtentCount)
        { LinuxSocketServices.RetainedExtentAt(state->Sockets, index, out start, out bytes); return; }
        var epoll = state->Epoll; if (epoll == null) return;
        index -= LinuxSocketServices.RetainedExtentCount;
        if (index == 0) { start = (ulong)epoll; bytes = (ulong)sizeof(LinuxEpollServices.State); }
        else if (index == 1) { start = (ulong)epoll->Ports; bytes = epoll->PortCount * (ulong)sizeof(LinuxEpollServices.Port); }
        else if (index == 2) { start = (ulong)epoll->Registrations; bytes = epoll->RegistrationCount * (ulong)sizeof(LinuxEpollServices.Registration); }
    }

    // Candidate mount/provider pointers belong to the respective already
    // authenticated admission, including BEFORE their global publication.
    // Shared C/B backing is queried by its real owners; transport ranges never
    // confer ownership of either device or permission to perform user copies.
    private static bool TransportStorageOutside(State* state, void* mount, Writable.State* provider,
        ulong epoch, void* writableState, void* writableBindings, ulong bindingBytes)
    {
        if (state->Sockets == null) return state->Epoll == null;
        if (!TransportShape(state)) return false;
        for (uint i = 0; i < TransportExtentCount; i++)
        {
            TransportExtentAt(state, i, out ulong start, out ulong bytes); if (bytes == 0) continue;
            if (!Extent((void*)start, bytes) ||
                provider != null && !Writable.CanRetainStorage(provider, epoch, start, bytes) ||
                Overlap(start, bytes, (ulong)writableState, writableState == null ? 0UL : (ulong)sizeof(WritableDiskState)) ||
                Overlap(start, bytes, (ulong)writableBindings, bindingBytes)) return false;
#if !NOSTDLIB_EFI || M16_025_PROBE
            if (mount != null)
            {
                var m = (OS.Storage.ReadOnlyWebRootFileSystem.Mount*)mount;
                if (!OS.Storage.ReadOnlyWebRootFileSystem.CanRetainStorage(m, m->Input.Epoch, start, bytes)) return false;
                DiskState* d = state->Disk;
                if (d != null && (d->Mount != m || Overlap(start, bytes, (ulong)d, (ulong)sizeof(DiskState)) ||
                    Overlap(start, bytes, (ulong)d->Bindings, DiskSlots * (ulong)sizeof(DiskBinding)) ||
                    Overlap(start, bytes, (ulong)d->Requests, DiskSlots * (ulong)sizeof(DiskRequest)))) return false;
            }
#endif
        }
        return true;
    }
#endif

    private static bool Storage(State* state, Admission a)
    {
        if (!Extent(state, (ulong)sizeof(State)) || ((ulong)state & 7) != 0 || !a.FirmwareExited || !a.SingleOwnerValidated ||
            a.DescriptorCount < 3 || a.DescriptorCount > MaximumDescriptors || a.DescriptionCount < 3 || a.DescriptionCount > a.DescriptorCount ||
            a.PipeCount == 0 || a.PipeCount > MaximumPipes || a.PipeCapacity < LinuxPipe.PipeBuf || a.PipeCapacity > LinuxPipe.MaximumCapacity ||
            (a.PipeCapacity & (a.PipeCapacity - 1)) != 0 || a.ScratchBytes < 8192 || a.ScratchBytes > MaximumIo ||
            a.ViewCount > MaximumViews || (a.ViewCount != 0 && !Extent(a.Views, a.ViewCount * (ulong)sizeof(View))) ||
            (a.PackageProcessId != 0 && (a.PackageProcessId > int.MaxValue || a.ViewCount == 0)) ||
            a.User.Context == null || a.User.PageAccess == null || a.User.CopyFromUser == null || a.User.Write == null || a.CopyToUser == null ||
            !Extent(a.Memory, (ulong)sizeof(LinuxMemoryServices.State)) || !LinuxMemoryServices.Audit(a.Memory) ||
            a.PhysicalBytes == 0 || a.PhysicalBytes > X64UserAddressSpace.PhysicalEnd ||
            a.AvailableBytes > a.PhysicalBytes || a.AvailableBytes < a.Memory->Memory.PoolPages * 4096UL ||
            ((a.PhysicalBytes | a.AvailableBytes) & 4095) != 0 || ((ulong)a.Slots & 3) != 0 ||
            ((ulong)a.Descriptions & 7) != 0 || ((ulong)a.Pipes & 7) != 0 || ((ulong)a.Views & 7) != 0) return false;
        // Validate every extent before any dereference or mutation. The memory
        // backend owns its own nonaliasing proof; descriptor buffers may not
        // alias its metadata, tables or backing pool either.
        ulong* extents = stackalloc ulong[24];
        extents[0] = (ulong)state; extents[1] = (ulong)sizeof(State);
        extents[2] = (ulong)a.Slots; extents[3] = a.DescriptorCount * (ulong)sizeof(Slot);
        extents[4] = (ulong)a.Descriptions; extents[5] = a.DescriptionCount * (ulong)sizeof(Description);
        extents[6] = (ulong)a.Pipes; extents[7] = a.PipeCount * (ulong)sizeof(LinuxPipe);
        extents[8] = (ulong)a.PipeBuffers; extents[9] = a.PipeCount * a.PipeCapacity;
        extents[10] = (ulong)a.Snapshots; extents[11] = a.DescriptionCount * SnapshotBytes;
        extents[12] = (ulong)a.Scratch; extents[13] = a.ScratchBytes;
        extents[14] = (ulong)a.Memory; extents[15] = (ulong)sizeof(LinuxMemoryServices.State);
        extents[16] = (ulong)a.Memory->Memory.Pool; extents[17] = a.Memory->Memory.PoolPages * 4096UL;
        extents[18] = (ulong)a.Memory->Memory.Tables; extents[19] = a.Memory->Memory.TablePages * 4096UL;
        extents[20] = (ulong)a.Memory->Memory.CandidateTables; extents[21] = a.Memory->Memory.TablePages * 4096UL;
        extents[22] = (ulong)a.Memory->Memory.StaticRanges; extents[23] = a.Memory->Memory.StaticCount * (ulong)sizeof(X64UserMappingRange);
        for (uint i = 0; i < 12; i++)
        {
            if (!Extent((void*)extents[i * 2], extents[i * 2 + 1])) return false;
            for (uint j = 0; j < i; j++)
                if (Overlap(extents[i * 2], extents[i * 2 + 1], extents[j * 2], extents[j * 2 + 1])) return false;
            if (a.ViewCount != 0 && Overlap(extents[i * 2], extents[i * 2 + 1], (ulong)a.Views, a.ViewCount * (ulong)sizeof(View))) return false;
        }
        for (uint i = 0; i < a.ViewCount; i++)
        {
            View v = a.Views[i];
            if (!Extent(v.Path, v.PathBytes) || v.PathBytes < 6 || v.PathBytes >= MaximumPath ||
                !AsciiPointerMatcher.Matches(v.Path, 5, 5, 0x2f7070612f) ||
                !AsciiPointerMatcher.IsRelativePath(v.Path + 1, v.PathBytes - 1) || v.Bytes > MaximumViewBytes ||
                !Extent(v.Data, v.Bytes, true) || v.Inode < 256) return false;
            if (a.PackageProcessId != 0 && v.Inode != 256 + i) return false;
            for (uint j = 0; j < 12; j++)
                if (Overlap((ulong)v.Path, v.PathBytes, extents[j * 2], extents[j * 2 + 1]) ||
                    (v.Bytes != 0 && Overlap((ulong)v.Data, v.Bytes, extents[j * 2], extents[j * 2 + 1]))) return false;
            if (Overlap((ulong)v.Path, v.PathBytes, (ulong)a.Views, a.ViewCount * (ulong)sizeof(View)) ||
                (v.Bytes != 0 && Overlap((ulong)v.Data, v.Bytes, (ulong)a.Views, a.ViewCount * (ulong)sizeof(View)))) return false;
            for (uint j = 0; j < i; j++)
            {
                View previous = a.Views[j];
                if (v.Inode == previous.Inode) return false;
                if (a.PackageProcessId != 0 &&
                    (PackagePathPrefix(v.Path, v.PathBytes, previous.Path, previous.PathBytes) ||
                    PackagePathPrefix(previous.Path, previous.PathBytes, v.Path, v.PathBytes))) return false;
                if (v.PathBytes != previous.PathBytes) continue;
                bool same = true;
                for (uint k = 0; k < v.PathBytes; k++) if (v.Path[k] != previous.Path[k]) { same = false; break; }
                if (same) return false;
            }
        }
        return true;
    }
    private static bool Extent(void* pointer, ulong bytes, bool empty = false) =>
        bytes == 0 ? empty : pointer != null && (ulong)pointer <= ulong.MaxValue - bytes;
    private static bool Overlap(ulong a, ulong an, ulong b, ulong bn) => an != 0 && bn != 0 && a < b + bn && b < a + an;
    private static void Clear(byte* bytes, uint count) { for (uint i = 0; i < count; i++) bytes[i] = 0; }
}
