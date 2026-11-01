using OS.Pal;

namespace OS.Kernel.Threading;
/// <summary>
/// Fixed caller-owned address registrations over one pinned KernelThreadTable.
/// One BSP owner saves/disables/restores IF across compare, registration, capture,
/// Park and native transfer. No other worker can write the compared word inside
/// that transaction. Supplied flags are observations, not serialization. The
/// caller admits and pins mapped, aligned address/storage ranges, exclusive from
/// all kernel resources, until Unbind/Release succeeds. Never reuse an owner key
/// across lifetimes, even at the same storage address. Keys detect mistakes,
/// not forgery. No allocation, hardware access, transfer or Linux ABI occurs here;
/// SMP, arbitrary address faults and asynchronous resumption are unsupported.
/// </summary>

public unsafe partial struct WaitAddress
{
    public const uint Maximum = 64;
    private ulong _key, _self, _threadKey, _order;
    private KernelThreadTable* _threads;
    private Slot* _slots;
    private Registration* _registrations;
    private uint _slotBytes, _registrationBytes, _capacity, _registrationCapacity;
    private bool _initialized;
    /// <summary>Validate all disjoint mapped borrows before any write. Keep the
    /// scheduler stable and exclusively borrowed, without reinitializing it or
    /// reusing its owner key, until Release. Seeds are nonwrapping identities in
    /// this fresh owner lifetime; a zero generation rejects without mutation.</summary>

    public Status Initialize(ulong key, ulong flags, KernelThreadTable* threads, ulong threadKey, Slot* slots, uint slotBytes, uint capacity, Registration* registrations, uint registrationBytes, uint registrationCapacity, uint generation = 1, ulong initialOrder = 0)
    {
        if (_initialized)
            return Status.AlreadyInitialized;
        if (key == 0 || threadKey == 0)
            return Status.WrongOwner;
        if (!IfClear(flags))
            return Status.InterruptsEnabled;
        if (capacity == 0 || capacity > Maximum || registrationCapacity == 0 || registrationCapacity > Maximum || generation == 0)
            return Status.InvalidCapacity;
        ulong self;
        fixed (WaitAddress* p = &this)
            self = (ulong)p;
        if (!Buffer(self, (uint)sizeof(WaitAddress), (uint)sizeof(WaitAddress)) || !Buffer((ulong)threads, (uint)sizeof(KernelThreadTable), (uint)sizeof(KernelThreadTable)) || !Buffer((ulong)slots, slotBytes, capacity * (uint)sizeof(Slot)) || !Buffer((ulong)registrations, registrationBytes, registrationCapacity * (uint)sizeof(Registration)))
            return Status.InvalidStorage;
        ulong a = (ulong)slots, b = (ulong)registrations, t = (ulong)threads;
        if (Overlap(self, (uint)sizeof(WaitAddress), a, slotBytes) || Overlap(self, (uint)sizeof(WaitAddress), b, registrationBytes) || Overlap(self, (uint)sizeof(WaitAddress), t, (uint)sizeof(KernelThreadTable)) || Overlap(a, slotBytes, b, registrationBytes) || Overlap(a, slotBytes, t, (uint)sizeof(KernelThreadTable)) || Overlap(b, registrationBytes, t, (uint)sizeof(KernelThreadTable)))
            return Status.Overlap;
        if (threads->Inspect(threadKey, flags, default).Code != KernelThreadTable.Status.InvalidIdentity)
            return Status.ThreadFailure;
        for (uint i = 0; i < capacity; i++)
            slots[i] = new()
            {
                Generation = generation
            };
        for (uint i = 0; i < registrationCapacity; i++)
            registrations[i] = default;
        _key = key;
        _self = self;
        _threadKey = threadKey;
        _threads = threads;
        _order = initialOrder;
        _slots = slots;
        _slotBytes = slotBytes;
        _capacity = capacity;
        _registrations = registrations;
        _registrationBytes = registrationBytes;
        _registrationCapacity = registrationCapacity;
        _initialized = true;
        return Status.Ok;
    }

    /// <summary>Bind one separately admitted four-byte word and nonzero backing
    /// lifetime. Exact address/tag pairs are compared, never hash buckets. Tags
    /// separate caller-defined namespaces for the same word; zero retains the
    /// original namespace. An already bound pair rejects even under another
    /// lifetime. Failures change nothing.</summary>

    public Result Bind(ulong key, ulong flags, uint* word, ulong lifetime, uint tag = 0)
    {
        Status status = Access(key, flags);
        if (status != Status.Ok)
            return new(status);
        ulong address = (ulong)word;
        if (lifetime == 0 || (address & 3) != 0 || !Range(address, 4))
            return new(Status.InvalidAddress);
        if (BorrowOverlap(address, 4))
            return new(Status.Overlap);
        uint free = _capacity;
        for (uint i = 0; i < _capacity; i++)
        {
            if (_slots[i].Word == word && _slots[i].Tag == tag)
                return new(Status.AlreadyBound);
            if (_slots[i].Word == null && !_slots[i].Retired && free == _capacity)
                free = i;
        }

        if (free == _capacity)
            return new(Status.Full);
        _slots[free].Word = word;
        _slots[free].Lifetime = lifetime;
        _slots[free].Tag = tag;
        return new(Status.Ok, Id(free));
    }

    /// <summary>Atomically compare and register while IF is clear. Caller then
    /// captures and Parks the returned wait within the same owner transaction;
    /// a wake before Park is retained by KernelThreadTable. A successful staged
    /// registration must be completed/parked before leaving that transaction.
    /// Mismatch, zero timed delay, overflow, stale identities and capacity failures
    /// publish nothing. A zero bitset rejects before comparison/registration;
    /// matching wake masks must share at least one bit. Timed waits use the
    /// scheduler's existing tick epoch.</summary>

    public Result Begin(ulong key, ulong flags, Binding address, OSHostHandle thread, uint expected, ulong now, bool timed = false, ulong delay = 0, uint bitset = uint.MaxValue)
    {
        Status status = Access(key, flags);
        if (status != Status.Ok)
            return new(status);
        if (bitset == 0)
            return new(Status.InvalidBitset);
        status = Lookup(address, out uint index);
        if (status != Status.Ok)
            return new(status);
        var current = _threads->Inspect(_threadKey, flags, thread);
        if (!current.Succeeded)
            return new(Status.ThreadFailure, threadStatus: current.Code);
        if (_threads->Current.Value != thread.Value || current.State != KernelThreadTable.State.Running)
            return new(Status.ThreadFailure, threadStatus: KernelThreadTable.Status.NotCurrent);
        if (now < _threads->ObservedTicks)
            return new(Status.ThreadFailure, threadStatus: KernelThreadTable.Status.ClockRegression);
        if (*_slots[index].Word != expected)
            return new(Status.ValueChanged);
        if (timed && delay == 0)
            return new(Status.TimedOut);
        if (_order == ulong.MaxValue)
            return new(Status.IdentityExhausted);
        uint free = _registrationCapacity;
        for (uint i = 0; i < _registrationCapacity; i++)
        {
            bool live = Live(_registrations[i]);
            if (live && _registrations[i].Wait.Thread.Value == thread.Value)
                return new(Status.Busy);
            if (!live && free == _registrationCapacity)
                free = i;
        }

        if (free == _registrationCapacity)
            return new(Status.Full);
        var begin = _threads->BeginWait(_threadKey, flags, thread, timed ? KernelThreadTable.WaitKind.Sleep : KernelThreadTable.WaitKind.Park, now, delay);
        if (!begin.Succeeded)
            return new(Status.ThreadFailure, threadStatus: begin.Code);
        _registrations[free] = new()
        {
            Address = address,
            Wait = begin.Wait,
            Order = ++_order,
            Bitset = bitset
        };
        return new(Status.Ok, address, begin.Wait);
    }

    /// <summary>Move the oldest live source registration to the end of the
    /// destination FIFO under the same pinned, IRQ-off BSP owner transaction.
    /// Both distinct bindings must remain admitted through this call; success
    /// transfers the registration's backing borrow to destination. The exact
    /// scheduler wait, deadline and bitset are unchanged. No word access, wake,
    /// completion, transfer or allocation occurs. An empty source returns Count0;
    /// invalid bindings/ownership or exhausted order reject without mutation,
    /// including completed registrations, which are skipped rather than pruned.</summary>

    public Result RequeueOne(ulong key, ulong flags, Binding source, Binding destination)
    {
        Status status = Access(key, flags);
        if (status != Status.Ok)
            return new(status);
        status = Lookup(source, out _);
        if (status != Status.Ok)
            return new(status);
        status = Lookup(destination, out _);
        if (status != Status.Ok)
            return new(status);
        if (source.Value == destination.Value)
            return new(Status.InvalidBinding);
        uint first = _registrationCapacity;
        ulong order = ulong.MaxValue;
        for (uint i = 0; i < _registrationCapacity; i++)
            if (_registrations[i].Address.Value == source.Value && Live(_registrations[i]) && (first == _registrationCapacity || _registrations[i].Order < order))
            {
                first = i;
                order = _registrations[i].Order;
            }

        if (first == _registrationCapacity)
            return new(Status.Ok, destination);
        if (_order == ulong.MaxValue)
            return new(Status.IdentityExhausted);
        _registrations[first].Address = destination;
        _registrations[first].Order = ++_order;
        return new(Status.Ok, destination, _registrations[first].Wait, 1);
    }

    /// <summary>Complete at most maximum matching registrations in FIFO order;
    /// use 1 for wake-one or uint.MaxValue for wake-all. A spurious wake returns
    /// zero. Ready pressure retains the winner in the scheduler's Pending state.
    /// Stale/completed registrations are pruned only after argument admission.
    /// A later live-context failure reports earlier committed Count explicitly.
    /// Zero bitsets reject before pruning. Default matches every registration.</summary>

    public Result Wake(ulong key, ulong flags, Binding address, uint maximum, uint bitset = uint.MaxValue)
    {
        Status status = Access(key, flags);
        if (status != Status.Ok)
            return new(status);
        if (bitset == 0)
            return new(Status.InvalidBitset);
        status = Lookup(address, out _);
        if (status != Status.Ok)
            return new(status);
        uint count = 0;
        while (count < maximum)
        {
            uint first = _registrationCapacity;
            ulong order = ulong.MaxValue;
            for (uint i = 0; i < _registrationCapacity; i++)
            {
                if (!Live(_registrations[i]))
                {
                    _registrations[i] = default;
                    continue;
                }

                if (_registrations[i].Address.Value == address.Value && (_registrations[i].Bitset & bitset) != 0 && (first == _registrationCapacity || _registrations[i].Order < order))
                {
                    first = i;
                    order = _registrations[i].Order;
                }
            }

            if (first == _registrationCapacity)
                break;
            var wake = _threads->Wake(_threadKey, flags, _registrations[first].Wait);
            if (!wake.Succeeded)
                return new(Status.ThreadFailure, address, count: count, threadStatus: wake.Code);
            _registrations[first] = default;
            count++;
        }

        return new(Status.Ok, address, count: count);
    }

    /// <summary>Drop only completed/stale registrations after scheduler timeout,
    /// cancellation, exit or generation reuse. Never cancel/wake a live waiter.</summary>

    public Result Sweep(ulong key, ulong flags)
    {
        Status status = Access(key, flags);
        if (status != Status.Ok)
            return new(status);
        uint count = 0;
        for (uint i = 0; i < _registrationCapacity; i++)
            if (_registrations[i].Wait.IsValid && !Live(_registrations[i]))
            {
                _registrations[i] = default;
                count++;
            }

        return new(Status.Ok, count: count);
    }

    /// <summary>Relinquish the word only after all live registrations complete.
    /// Completed references are removed before advancing/retiring the generation.
    /// No target word is scrubbed or freed; its backing remains caller-owned.</summary>

    public Status Unbind(ulong key, ulong flags, Binding address)
    {
        Status status = Access(key, flags);
        if (status != Status.Ok)
            return status;
        status = Lookup(address, out uint index);
        if (status != Status.Ok)
            return status;
        for (uint i = 0; i < _registrationCapacity; i++)
            if (_registrations[i].Address.Value == address.Value && Live(_registrations[i]))
                return Status.Busy;
        for (uint i = 0; i < _registrationCapacity; i++)
            if (_registrations[i].Address.Value == address.Value)
                _registrations[i] = default;
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
        return Status.Ok;
    }

    /// <summary>Clear metadata and release caller storage only with no bound
    /// address. Caller revokes any external/native references before reuse.</summary>

    public Status Release(ulong key, ulong flags)
    {
        Status status = Access(key, flags);
        if (status != Status.Ok)
            return status;
        for (uint i = 0; i < _capacity; i++)
            if (_slots[i].Word != null)
                return Status.Busy;
        for (uint i = 0; i < _capacity; i++)
            _slots[i] = default;
        for (uint i = 0; i < _registrationCapacity; i++)
            _registrations[i] = default;
        this = default;
        return Status.Ok;
    }

    private readonly bool Live(Registration registration)
    {
        if (!registration.Wait.IsValid)
            return false;
        var state = _threads->Inspect(_threadKey, 2, registration.Wait.Thread);
        return state.Succeeded && state.Wait.Sequence == registration.Wait.Sequence && state.Completion == KernelThreadTable.Completion.None && (state.State == KernelThreadTable.State.Parking || state.State == KernelThreadTable.State.Waiting);
    }

    private readonly Binding Id(uint index) => new(((ulong)_slots[index].Generation << 32) | (index + 1UL), _slots[index].Lifetime, _self, _key);
    private readonly Status Lookup(Binding address, out uint index)
    {
        uint slot = (uint)address.Value;
        index = slot - 1;
        if (!address.IsValid || address.Owner != _self || address.OwnerKey != _key || slot == 0 || slot > _capacity)
            return Status.InvalidBinding;
        return _slots[index].Word == null || _slots[index].Generation != (uint)(address.Value >> 32) || _slots[index].Lifetime != address.Lifetime ? Status.StaleBinding : Status.Ok;
    }

    private Status Access(ulong key, ulong flags)
    {
        if (!_initialized)
            return Status.Uninitialized;
        if (key == 0 || key != _key)
            return Status.WrongOwner;
        fixed (WaitAddress* p = &this)
            if ((ulong)p != _self)
                return Status.CopiedOwner;
        if (!IfClear(flags))
            return Status.InterruptsEnabled;
        return _threads->Inspect(_threadKey, flags, default).Code == KernelThreadTable.Status.InvalidIdentity ? Status.Ok : Status.ThreadFailure;
    }

    private readonly bool BorrowOverlap(ulong a, ulong n) => Overlap(a, n, _self, (uint)sizeof(WaitAddress)) || Overlap(a, n, (ulong)_threads, (uint)sizeof(KernelThreadTable)) || Overlap(a, n, (ulong)_slots, _slotBytes) || Overlap(a, n, (ulong)_registrations, _registrationBytes);
    private static bool IfClear(ulong flags) => (flags & 0x202) == 2;
    private static bool Buffer(ulong a, uint bytes, uint needed) => needed != 0 && bytes >= needed && (a & 7) == 0 && Range(a, bytes);
    private static bool Range(ulong a, ulong n) => a != 0 && n != 0 && n - 1 <= ulong.MaxValue - a && Canonical(a) && Canonical(a + n - 1) && a >> 47 == (a + n - 1) >> 47;
    private static bool Canonical(ulong a) => a >> 48 == ((a & (1UL << 47)) == 0 ? 0UL : 0xffffUL);
    private static bool Overlap(ulong a, ulong n, ulong b, ulong m) => a <= b + m - 1 && b <= a + n - 1;
}
