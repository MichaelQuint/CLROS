using OS.Pal;

namespace OS.Kernel.Threading;
/// <summary>
/// Bounded private TLS spans over one pinned cooperative lifecycle table. The
/// single BSP owner supplies freshly observed CPU/root/mapping state and keeps
/// IF clear throughout bind/dispatch/transfer/reap. Initialize saved FS/user-GS
/// before publishing a worker; Bind authenticates those values before dispatch.
/// Keep every declared region mapped and exclusive from other kernel resources.
/// Kernel-GS remains CPU-owned; this type never writes an MSR, switches a thread,
/// allocates storage or implements managed thread statics. CPL3/Linux adapters,
/// SWAPGS, asynchronous resumption, SMP and arbitrary pointers are unsupported.
/// </summary>

public unsafe partial struct ThreadLocalStorage
{
    public const uint Maximum = KernelThreadTable.MaximumSlots;
    private ulong _self, _key, _threadKey, _switchKey, _root, _kernelGs, _arena, _arenaBytes, _tlsStart, _tlsBytes;
    private KernelThreadTable* _threads;
    private Slot* _slots;
    private uint _slotBytes, _capacity;
    private bool _initialized;
    /// <summary>Validate all mapped metadata and the live owner before writing.
    /// Keep the table stable without key reuse until Release. Metadata and all
    /// TLS spans come from a dedicated eight-byte-aligned private subpool, disjoint from
    /// context/stack/control resources. Discovered TLS/context aliases reject;
    /// the caller admits remaining external resources and keeps mappings pinned.</summary>

    public Status Initialize(ulong key, ThreadContextSwitch.Owner owner, KernelThreadTable* threads, ulong threadKey, Slot* slots, uint slotBytes, uint capacity, ulong tlsStart, ulong tlsBytes, uint generation = 1)
    {
        if (_initialized)
            return Status.AlreadyInitialized;
        if (key == 0 || threadKey == 0)
            return Status.InvalidOwner;
        Status status = Owner(owner);
        if (status != Status.Ok)
            return status;
        if (capacity == 0 || capacity > Maximum || generation == 0)
            return Status.InvalidCapacity;
        ulong self;
        fixed (ThreadLocalStorage* p = &this)
            self = (ulong)p;
        if (!Buffer(self, (uint)sizeof(ThreadLocalStorage), (uint)sizeof(ThreadLocalStorage)) || !Buffer((ulong)threads, (uint)sizeof(KernelThreadTable), (uint)sizeof(KernelThreadTable)) || !Buffer((ulong)slots, slotBytes, capacity * (uint)sizeof(Slot)))
            return Status.InvalidStorage;
        if (Overlap(self, (uint)sizeof(ThreadLocalStorage), (ulong)threads, (uint)sizeof(KernelThreadTable)) || Overlap(self, (uint)sizeof(ThreadLocalStorage), (ulong)slots, slotBytes) || Overlap((ulong)threads, (uint)sizeof(KernelThreadTable), (ulong)slots, slotBytes))
            return Status.Overlap;
        if ((tlsStart & 7) != 0 || (tlsBytes & 7) != 0 || !Contains(owner.ContextArena, owner.ContextArenaBytes, tlsStart, tlsBytes))
            return Status.InvalidRegion;
        if (Overlap(tlsStart, tlsBytes, self, (uint)sizeof(ThreadLocalStorage)) || Overlap(tlsStart, tlsBytes, (ulong)threads, (uint)sizeof(KernelThreadTable)) || Overlap(tlsStart, tlsBytes, (ulong)slots, slotBytes) || owner.KernelGs != 0 && Includes(new(tlsStart, tlsBytes, tlsStart), owner.KernelGs))
            return Status.Overlap;
        if (threads->Inspect(threadKey, owner.Flags, default).Code != KernelThreadTable.Status.InvalidIdentity)
            return Status.ThreadFailure;
        for (uint i = 0; i < capacity; i++)
            slots[i] = new()
            {
                Generation = generation
            };
        _self = self;
        _key = key;
        _threadKey = threadKey;
        _switchKey = owner.Key;
        _root = owner.Root;
        _kernelGs = owner.KernelGs;
        _arena = owner.ContextArena;
        _arenaBytes = owner.ContextArenaBytes;
        _tlsStart = tlsStart;
        _tlsBytes = tlsBytes;
        _threads = threads;
        _slots = slots;
        _slotBytes = slotBytes;
        _capacity = capacity;
        _initialized = true;
        return Status.Ok;
    }

    /// <summary>Bind initialized entry-context bases to private regions before
    /// dispatch. Only a fresh Ready/Entry thread is accepted. No context or TLS
    /// byte is changed. Capacity, base and alias rejection leaves all state intact.
    /// Every thread using these TLS spans must be reaped through this owner.</summary>

    public Result Bind(ulong key, ThreadContextSwitch.Owner owner, OSHostHandle thread, Region fs, Region gs = default)
    {
        Status status = Access(key, owner);
        if (status != Status.Ok)
            return new(status);
        var state = _threads->Inspect(_threadKey, owner.Flags, thread);
        if (!state.Succeeded)
            return new(Status.ThreadFailure, threadStatus: state.Code);
        if (state.State != KernelThreadTable.State.Ready || state.Context.Kind != ThreadContextSwitch.ResumeKind.Entry || state.CancellationRequested)
            return new(Status.WrongState);
        var descriptor = state.Context;
        if (ThreadContextSwitch.AdmitResume(owner, descriptor) != ThreadContextSwitch.Status.Ok)
            return new(Status.InvalidContext);
        status = Regions(descriptor, fs, gs);
        if (status != Status.Ok)
            return new(status);
        uint free = _capacity;
        for (uint i = 0; i < _capacity; i++)
        {
            Slot slot = _slots[i];
            if (!slot.Thread.IsValid)
            {
                if (!slot.Retired && free == _capacity)
                    free = i;
                continue;
            }

            if (slot.Thread.Value == thread.Value)
                return new(Status.AlreadyBound);
            if (Alias(fs, slot.Fs) || Alias(fs, slot.Gs) || Alias(gs, slot.Fs) || Alias(gs, slot.Gs) || Hits(fs, (ulong)slot.Context, slot.ContextBytes) || Hits(gs, (ulong)slot.Context, slot.ContextBytes) || Hits(slot.Fs, (ulong)descriptor.Context, descriptor.ContextBytes) || Hits(slot.Gs, (ulong)descriptor.Context, descriptor.ContextBytes))
                return new(Status.Overlap);
        }

        if (free == _capacity)
            return new(Status.Full);
        _slots[free] = new()
        {
            Generation = _slots[free].Generation,
            Thread = thread,
            Context = descriptor.Context,
            ContextBytes = descriptor.ContextBytes,
            Fs = fs,
            Gs = gs
        };
        return new(Status.Ok, Id(free), descriptor, fs, gs);
    }

    /// <summary>Authenticate the actual current thread generation, saved bases,
    /// FP state and fresh CPU/root/kernel-GS observations. Caller still owns code,
    /// context/stack/TLS lifetimes and native observation/transfer authority.</summary>

    public Result Inspect(ulong key, ThreadContextSwitch.Owner owner, Binding identity)
    {
        Status status = Access(key, owner);
        if (status != Status.Ok)
            return new(status);
        status = Lookup(identity, out uint index);
        if (status != Status.Ok)
            return new(status);
        Slot slot = _slots[index];
        var state = _threads->Inspect(_threadKey, owner.Flags, slot.Thread);
        if (!state.Succeeded)
            return new(Status.ThreadFailure, threadStatus: state.Code);
        if (state.Context.Context != slot.Context || state.Context.ContextBytes != slot.ContextBytes || ThreadContextSwitch.AdmitResume(owner, state.Context) != ThreadContextSwitch.Status.Ok)
            return new(Status.InvalidContext);
        status = Regions(state.Context, slot.Fs, slot.Gs);
        return status == Status.Ok ? new(Status.Ok, identity, state.Context, slot.Fs, slot.Gs) : new(status);
    }

    /// <summary>Admit two current bindings for the existing cooperative ABI.
    /// Read-only success neither captures registers nor authorizes a switch.</summary>

    public Status Admit(ulong key, ThreadContextSwitch.Owner owner, Binding source, Binding destination)
    {
        Result from = Inspect(key, owner, source);
        if (!from.Succeeded)
            return from.Code;
        Result to = Inspect(key, owner, destination);
        if (!to.Succeeded)
            return to.Code;
        return ThreadContextSwitch.Admit(owner, from.Descriptor, to.Descriptor) == ThreadContextSwitch.Status.Ok ? Status.Ok : Status.InvalidContext;
    }

    /// <summary>Reap only through KernelThreadTable's actual supervisor/pin/wait
    /// guards, then scrub TLS and retire this binding. Supply freshly read RSP and
    /// FS/GS bases after revoking all native/IRQ references. Active segment bases
    /// into either target span reject before lifecycle or TLS mutation. A failed
    /// reap retains backing. This does not release pages or change MSRs.</summary>

    public Result Reap(ulong key, ThreadContextSwitch.Owner owner, Binding identity, ulong observedRsp, ulong observedFsBase, ulong observedGsBase)
    {
        Result current = Inspect(key, owner, identity);
        if (!current.Succeeded)
            return current;
        if (!Canonical(observedFsBase) || !Canonical(observedGsBase))
            return new(Status.InvalidBase);
        if (Includes(current.Fs, observedFsBase) || Includes(current.Gs, observedFsBase) || Includes(current.Fs, observedGsBase) || Includes(current.Gs, observedGsBase))
            return new(Status.Busy);
        var reaped = _threads->Reap(_threadKey, owner.Flags, identity.Thread, observedRsp);
        if (!reaped.Succeeded)
            return new(Status.ThreadFailure, threadStatus: reaped.Code);
        Scrub(current.Fs);
        Scrub(current.Gs);
        Lookup(identity, out uint index);
        uint generation = _slots[index].Generation;
        _slots[index] = generation == uint.MaxValue ? new()
        {
            Generation = generation,
            Retired = true
        }

        : new()
        {
            Generation = generation + 1
        };
        return new(Status.Ok, identity, reaped.Context, current.Fs, current.Gs, current.Fs.Bytes + current.Gs.Bytes);
    }

    /// <summary>Release metadata only after all bindings were reaped. External
    /// execution references must already be revoked; keys may never repeat.</summary>

    public Status Release(ulong key, ThreadContextSwitch.Owner owner)
    {
        Status status = Access(key, owner);
        if (status != Status.Ok)
            return status;
        for (uint i = 0; i < _capacity; i++)
            if (_slots[i].Thread.IsValid)
                return Status.Busy;
        for (uint i = 0; i < _capacity; i++)
            _slots[i] = default;
        this = default;
        return Status.Ok;
    }

    private readonly Status Regions(ThreadContextSwitch.Descriptor descriptor, Region fs, Region gs)
    {
        if (!RegionShape(fs) || !RegionShape(gs))
            return Status.InvalidRegion;
        if (fs.Base != descriptor.Context->FsBase || gs.Base != descriptor.Context->UserGsBase)
            return Status.InvalidBase;
        if (Overlap(_tlsStart, _tlsBytes, (ulong)descriptor.Context, descriptor.ContextBytes) || Alias(fs, gs) || Hits(fs, (ulong)descriptor.Context, descriptor.ContextBytes) || Hits(gs, (ulong)descriptor.Context, descriptor.ContextBytes) || BorrowOverlap(fs) || BorrowOverlap(gs) || Hits(fs, _kernelGs, _kernelGs == 0 ? 0UL : 8UL) || Hits(gs, _kernelGs, _kernelGs == 0 ? 0UL : 8UL))
            return Status.Overlap;
        return Status.Ok;
    }

    private readonly bool RegionShape(Region region) => region.Start == 0 && region.Bytes == 0 && region.Base == 0 || (region.Start & 7) == 0 && (region.Base & 7) == 0 && region.Bytes >= 8 && Contains(_tlsStart, _tlsBytes, region.Start, region.Bytes) && Contains(region.Start, region.Bytes, region.Base, 8);
    private readonly Binding Id(uint index) => new(((ulong)_slots[index].Generation << 32) | (index + 1UL), _slots[index].Thread, _self, _key);
    private readonly Status Lookup(Binding identity, out uint index)
    {
        uint slot = (uint)identity.Value;
        index = slot - 1;
        if (!identity.IsValid || identity.Owner != _self || identity.Key != _key || slot == 0 || slot > _capacity)
            return Status.InvalidBinding;
        return _slots[index].Generation != (uint)(identity.Value >> 32) || _slots[index].Thread.Value != identity.Thread.Value ? Status.StaleBinding : Status.Ok;
    }

    private Status Access(ulong key, ThreadContextSwitch.Owner owner)
    {
        if (!_initialized)
            return Status.Uninitialized;
        if (key != _key || key == 0)
            return Status.InvalidOwner;
        fixed (ThreadLocalStorage* p = &this)
            if ((ulong)p != _self)
                return Status.CopiedOwner;
        Status status = Owner(owner);
        if (status != Status.Ok)
            return status;
        if (owner.Key != _switchKey || owner.Root != _root || owner.KernelGs != _kernelGs || owner.ContextArena != _arena || owner.ContextArenaBytes != _arenaBytes)
            return Status.InvalidOwner;
        return _threads->Inspect(_threadKey, owner.Flags, default).Code == KernelThreadTable.Status.InvalidIdentity ? Status.Ok : Status.ThreadFailure;
    }

    private static Status Owner(ThreadContextSwitch.Owner owner) => (owner.Flags & 0x202) != 2 ? Status.InterruptsEnabled : !owner.GuardsVerified || !owner.MappingsVerified || ThreadContextSwitch.AdmitPlan(owner) != ThreadContextSwitch.Status.Ok ? Status.InvalidOwner : Status.Ok;
    private readonly bool BorrowOverlap(Region region) => Hits(region, _self, (uint)sizeof(ThreadLocalStorage)) || Hits(region, (ulong)_threads, (uint)sizeof(KernelThreadTable)) || Hits(region, (ulong)_slots, _slotBytes);
    private static void Scrub(Region region)
    {
        for (ulong i = 0; i < region.Bytes; i++)
            ((byte*)region.Start)[i] = 0;
    }

    private static bool Includes(Region region, ulong value) => region.Bytes != 0 && value >= region.Start && value - region.Start < region.Bytes;
    private static bool Alias(Region a, Region b) => Hits(a, b.Start, b.Bytes);
    private static bool Hits(Region region, ulong start, ulong bytes) => region.Bytes != 0 && bytes != 0 && Overlap(region.Start, region.Bytes, start, bytes);
    private static bool Buffer(ulong a, uint bytes, uint needed) => needed != 0 && bytes >= needed && (a & 7) == 0 && Range(a, bytes);
    private static bool Contains(ulong a, ulong n, ulong b, ulong m) => Range(b, m) && b >= a && m <= n && b - a <= n - m;
    private static bool Range(ulong a, ulong n) => a != 0 && n != 0 && n - 1 <= ulong.MaxValue - a && Canonical(a) && Canonical(a + n - 1) && (a >> 47) == ((a + n - 1) >> 47);
    private static bool Canonical(ulong a) => a >> 48 == ((a & (1UL << 47)) == 0 ? 0UL : 0xffffUL);
    private static bool Overlap(ulong a, ulong n, ulong b, ulong m) => a <= b + m - 1 && b <= a + n - 1;
}
