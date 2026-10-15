namespace OS.Net;

public enum TcpConnectionLifecycleState
{
    Free = 0,
    Established = 1,
    Closing = 2,
    Closed = 3,
    SynReceived = 4
}

public enum TcpConnectionTableStatus
{
    Success = 0,
    InvalidEndpoint = 1,
    InvalidSequenceMetadata = 2,
    InvalidEntryState = 3,
    DuplicateEndpoint = 4,
    TableFull = 5,
    NotFound = 6,
    InvalidLifecycleState = 7
}

public readonly struct TcpConnectionKey
{
    public TcpConnectionKey(
        Ipv4Address localAddress,
        ushort localPort,
        Ipv4Address remoteAddress,
        ushort remotePort)
    {
        LocalAddress = localAddress;
        LocalPort = localPort;
        RemoteAddress = remoteAddress;
        RemotePort = remotePort;
    }

    public Ipv4Address LocalAddress { get; }

    public ushort LocalPort { get; }

    public Ipv4Address RemoteAddress { get; }

    public ushort RemotePort { get; }

    public bool HasValidEndpoint =>
        LocalAddress != default &&
        RemoteAddress != default &&
        LocalPort != 0 &&
        RemotePort != 0;

    public bool HasSameEndpoint(in TcpConnectionKey other)
    {
        return LocalAddress == other.LocalAddress &&
            LocalPort == other.LocalPort &&
            RemoteAddress == other.RemoteAddress &&
            RemotePort == other.RemotePort;
    }
}

public readonly struct TcpConnectionSequenceMetadata
{
    private readonly bool initialized;
    public TcpConnectionSequenceMetadata(
        uint localNextSequenceNumber,
        uint remoteNextSequenceNumber)
    {
        initialized = true;
        LocalNextSequenceNumber = localNextSequenceNumber;
        RemoteNextSequenceNumber = remoteNextSequenceNumber;
    }

    public uint LocalNextSequenceNumber { get; }

    public uint RemoteNextSequenceNumber { get; }

    // Every uint is a TCP sequence number, including zero after wrap. Only an
    // uninitialized value lacks sequence metadata.
    public bool HasValidMetadata => initialized;
}

public struct TcpConnectionTableEntry
{
    private TcpConnectionTableEntry(
        TcpConnectionLifecycleState state,
        TcpConnectionKey key,
        TcpConnectionSequenceMetadata sequence)
    {
        State = state;
        Key = key;
        Sequence = sequence;
    }

    public TcpConnectionLifecycleState State { get; }

    public TcpConnectionKey Key { get; }

    public TcpConnectionSequenceMetadata Sequence { get; }

    public bool IsActive =>
        State == TcpConnectionLifecycleState.SynReceived ||
        State == TcpConnectionLifecycleState.Established ||
        State == TcpConnectionLifecycleState.Closing;

    public static TcpConnectionTableEntry Create(
        TcpConnectionLifecycleState state,
        TcpConnectionKey key,
        TcpConnectionSequenceMetadata sequence)
    {
        return new TcpConnectionTableEntry(state, key, sequence);
    }

    internal static TcpConnectionTableEntry Established(
        TcpConnectionKey key,
        TcpConnectionSequenceMetadata sequence)
    {
        return new TcpConnectionTableEntry(
            TcpConnectionLifecycleState.Established,
            key,
            sequence);
    }

    internal static TcpConnectionTableEntry Closing(
        TcpConnectionKey key,
        TcpConnectionSequenceMetadata sequence)
    {
        return new TcpConnectionTableEntry(
            TcpConnectionLifecycleState.Closing,
            key,
            sequence);
    }

    internal static TcpConnectionTableEntry Closed(
        TcpConnectionKey key,
        TcpConnectionSequenceMetadata sequence)
    {
        return new TcpConnectionTableEntry(
            TcpConnectionLifecycleState.Closed,
            key,
            sequence);
    }
}

public readonly struct TcpConnectionTableResult
{
    private TcpConnectionTableResult(
        TcpConnectionTableStatus status,
        int count,
        int capacity,
        int requiredCapacity,
        int entryIndex,
        TcpConnectionTableEntry entry)
    {
        IsInitialized = true;
        Status = status;
        Count = count;
        Capacity = capacity;
        RequiredCapacity = requiredCapacity;
        EntryIndex = entryIndex;
        Entry = entry;
    }

    public TcpConnectionTableStatus Status { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized && Status == TcpConnectionTableStatus.Success;

    public int Count { get; }

    public int Capacity { get; }

    public int RequiredCapacity { get; }

    public int EntryIndex { get; }

    public TcpConnectionTableEntry Entry { get; }

    public static TcpConnectionTableResult Create(
        TcpConnectionTableStatus status,
        int count,
        int capacity,
        int requiredCapacity,
        int entryIndex,
        TcpConnectionTableEntry entry)
    {
        return new TcpConnectionTableResult(
            status,
            count,
            capacity,
            requiredCapacity,
            entryIndex,
            entry);
    }
}

public static unsafe class TcpConnectionTable
{
    public enum OwnershipStatus : byte
    {
        Uninitialized, Success, InvalidStorage, InvalidState, InvalidEndpoint,
        InvalidSequence, DuplicateEndpoint, Full, NotFound, StaleHandle,
        InvalidTransition, GenerationExhausted, CorruptState, ConnectionsRemain
    }

    /// <summary>Exclusive, noncopyable connection owner. One serialized kernel
    /// caller retains mapped state, entry and generation arrays through retirement.
    /// Count includes closed connections until explicit release. Observations
    /// grant no authority over packet buffers, timers, NICs or sockets.</summary>
    public struct Owner
    {
        internal ulong Self, Epoch;
        internal TcpConnectionTableEntry* Entries;
        internal ulong* Generations;
        internal ushort CapacityValue, Held;
        internal byte Phase;
        public readonly int Capacity => CapacityValue;
        public readonly int Count => Held;
        public readonly bool Retired => Phase == 2;
    }

    /// <summary>Opaque borrowed identity, not a reference or ownership transfer.
    /// Owner address, lifetime epoch and slot generation all participate. Retain
    /// the owner backing; copied handles become stale after release/reuse.</summary>
    public readonly struct Handle
    {
        internal readonly ulong OwnerAddress, Epoch, Generation;
        internal readonly ushort Index;
        internal Handle(Owner* owner, ushort index)
        { OwnerAddress = (ulong)owner; Epoch = owner->Epoch; Index = index; Generation = owner->Generations[index]; }
        public bool IsInitialized => OwnerAddress != 0 && Epoch != 0 && Generation != 0;
        public int SlotIndex => Index;
    }

    /// <summary>Bind zeroed, disjoint, aligned caller storage with1..64 entries
    /// and equally many generation words. Pointers must be trusted mapped kernel
    /// storage, never unchecked guest pointers. Epoch is unique for this lifetime.
    /// Invalid admission changes nothing; no allocation, callbacks or I/O occur.</summary>
    public static OwnershipStatus InitializeOwned(Owner* owner, TcpConnectionTableEntry* entries,
        uint capacity, ulong* generations, uint generationCount, ulong epoch)
    {
        if (capacity is 0 or > 64 || generationCount != capacity || epoch == 0 ||
            !OwnedStorage(owner, (ulong)sizeof(Owner)) ||
            !OwnedStorage(entries, capacity * (ulong)sizeof(TcpConnectionTableEntry)) || !OwnedStorage(generations, capacity * 8UL))
            return OwnershipStatus.InvalidStorage;
        ulong entryBytes = capacity * (ulong)sizeof(TcpConnectionTableEntry), generationBytes = capacity * 8UL;
        if (OwnedOverlap((ulong)owner, (ulong)sizeof(Owner), (ulong)entries, entryBytes) ||
            OwnedOverlap((ulong)owner, (ulong)sizeof(Owner), (ulong)generations, generationBytes) ||
            OwnedOverlap((ulong)entries, entryBytes, (ulong)generations, generationBytes) ||
            !OwnedZero(owner, (ulong)sizeof(Owner)) || !OwnedZero(entries, entryBytes) || !OwnedZero(generations, generationBytes))
            return OwnershipStatus.InvalidStorage;
        owner->Self = (ulong)owner; owner->Epoch = epoch; owner->Entries = entries; owner->Generations = generations;
        owner->CapacityValue = (ushort)capacity; owner->Phase = 1;
        return OwnershipStatus.Success;
    }

    /// <summary>Reserve a half-open connection and its unique endpoint tuple.
    /// Closed entries remain held until ReleaseOwned. Exhaustion/duplicate/bad
    /// inputs publish nothing; spent generations never wrap. Output is a distinct
    /// caller local outside all owner storage. No SYN or SYN-ACK is sent here.</summary>
    public static OwnershipStatus ReserveOwned(Owner* owner, TcpConnectionKey key,
        TcpConnectionSequenceMetadata sequence, out Handle handle)
    {
        handle = default;
        var status = ValidateOwned(owner); if (status != OwnershipStatus.Success) return status;
        if (!OwnedEndpoint(key)) return OwnershipStatus.InvalidEndpoint;
        if (!sequence.HasValidMetadata) return OwnershipStatus.InvalidSequence;
        ScanResult scan = Scan(OwnedEntries(owner), key, true);
        if (scan.MatchingEntryIndex != NoEntryIndex) return OwnershipStatus.DuplicateEndpoint;
        int free = NoEntryIndex; bool exhausted = false;
        for (int i = 0; i < owner->CapacityValue; i++)
            if (owner->Entries[i].State == TcpConnectionLifecycleState.Free)
            {
                if (owner->Generations[i] == ~0UL) exhausted = true;
                else { free = i; break; }
            }
        if (free == NoEntryIndex) return exhausted ? OwnershipStatus.GenerationExhausted : OwnershipStatus.Full;
        owner->Generations[free]++;
        owner->Entries[free] = TcpConnectionTableEntry.Create(TcpConnectionLifecycleState.SynReceived, key, sequence);
        owner->Held++; handle = new Handle(owner, (ushort)free); return OwnershipStatus.Success;
    }

    /// <summary>Find a held endpoint, including half-open and closed unreleased
    /// entries. Outputs are independent caller locals. Missing tuples expose no
    /// handle; this lookup neither accepts a connection nor transfers its lease.</summary>
    public static OwnershipStatus FindOwned(Owner* owner, TcpConnectionKey key, out Handle handle)
    {
        handle = default;
        var status = ValidateOwned(owner); if (status != OwnershipStatus.Success) return status;
        if (!OwnedEndpoint(key)) return OwnershipStatus.InvalidEndpoint;
        int index = Scan(OwnedEntries(owner), key, true).MatchingEntryIndex;
        if (index == NoEntryIndex) return OwnershipStatus.NotFound;
        handle = new Handle(owner, (ushort)index); return OwnershipStatus.Success;
    }

    /// <summary>Copy one checked metadata snapshot to a distinct caller local.
    /// Default, foreign, copied-owner and recycled handles never select a new
    /// connection. The snapshot is not a mutable entry or a socket authority.</summary>
    public static OwnershipStatus ReadOwned(Owner* owner, Handle handle, out TcpConnectionTableEntry entry)
    {
        entry = default;
        var status = OwnedHandle(owner, handle); if (status != OwnershipStatus.Success) return status;
        entry = owner->Entries[handle.Index]; return OwnershipStatus.Success;
    }

    /// <summary>Update initialized sequence metadata on a held nonclosed entry.
    /// Zero is valid. Packet/ACK/window validation belongs to the TCP owner; this
    /// method proves only identity/lifetime and never fabricates network progress.</summary>
    public static OwnershipStatus UpdateOwned(Owner* owner, Handle handle, TcpConnectionSequenceMetadata sequence)
    {
        var status = OwnedHandle(owner, handle); if (status != OwnershipStatus.Success) return status;
        if (!sequence.HasValidMetadata) return OwnershipStatus.InvalidSequence;
        var entry = owner->Entries[handle.Index];
        if (entry.State == TcpConnectionLifecycleState.Closed) return OwnershipStatus.InvalidTransition;
        owner->Entries[handle.Index] = TcpConnectionTableEntry.Create(entry.State, entry.Key, sequence);
        return OwnershipStatus.Success;
    }

    /// <summary>Apply an expected-state transition after the caller validates
    /// its actual handshake/close/reset event. Half-open may establish or close;
    /// established may begin closing or reset closed; closing may close. Invalid
    /// transitions preserve metadata. No packet, timer or teardown is synthesized.</summary>
    public static OwnershipStatus TransitionOwned(Owner* owner, Handle handle,
        TcpConnectionLifecycleState expected, TcpConnectionLifecycleState target)
    {
        var status = OwnedHandle(owner, handle); if (status != OwnershipStatus.Success) return status;
        var entry = owner->Entries[handle.Index];
        bool allowed = expected == TcpConnectionLifecycleState.SynReceived && target is TcpConnectionLifecycleState.Established or TcpConnectionLifecycleState.Closed ||
            expected == TcpConnectionLifecycleState.Established && target is TcpConnectionLifecycleState.Closing or TcpConnectionLifecycleState.Closed ||
            expected == TcpConnectionLifecycleState.Closing && target == TcpConnectionLifecycleState.Closed;
        if (entry.State != expected || !allowed) return OwnershipStatus.InvalidTransition;
        owner->Entries[handle.Index] = TcpConnectionTableEntry.Create(target, entry.Key, entry.Sequence);
        return OwnershipStatus.Success;
    }

    /// <summary>Release only a closed held entry after the caller has stopped
    /// its timers and returned all external packet/data leases. This table owns
    /// only metadata; it cannot certify that external cleanup. Clear endpoints
    /// and sequences, retain the spent generation, and invalidate every old handle.</summary>
    public static OwnershipStatus ReleaseOwned(Owner* owner, Handle handle)
    {
        var status = OwnedHandle(owner, handle); if (status != OwnershipStatus.Success) return status;
        if (owner->Entries[handle.Index].State != TcpConnectionLifecycleState.Closed) return OwnershipStatus.InvalidTransition;
        byte* bytes = (byte*)(owner->Entries + handle.Index);
        for (int i = 0; i < sizeof(TcpConnectionTableEntry); i++) bytes[i] = 0;
        owner->Held--; return OwnershipStatus.Success;
    }

    /// <summary>Retire an empty owner, scrubbing its generation words. Live or
    /// closed unreleased connections prevent retirement. Preserve backing until
    /// success; a retired or copied owner cannot be used or implicitly rebound.</summary>
    public static OwnershipStatus RetireOwned(Owner* owner)
    {
        var status = ValidateOwned(owner); if (status != OwnershipStatus.Success) return status;
        if (owner->Held != 0) return OwnershipStatus.ConnectionsRemain;
        for (int i = 0; i < owner->CapacityValue; i++) owner->Generations[i] = 0;
        owner->Phase = 2; return OwnershipStatus.Success;
    }

    private static OwnershipStatus OwnedHandle(Owner* owner, Handle handle)
    {
        var status = ValidateOwned(owner); if (status != OwnershipStatus.Success) return status;
        return handle.OwnerAddress == (ulong)owner && handle.Epoch == owner->Epoch && handle.Index < owner->CapacityValue &&
            handle.Generation != 0 && handle.Generation == owner->Generations[handle.Index] &&
            owner->Entries[handle.Index].State != TcpConnectionLifecycleState.Free ? OwnershipStatus.Success : OwnershipStatus.StaleHandle;
    }

    private static OwnershipStatus ValidateOwned(Owner* owner)
    {
        if (!OwnedStorage(owner, (ulong)sizeof(Owner)) || owner->Self != (ulong)owner || owner->Epoch == 0 || owner->Phase != 1 ||
            owner->CapacityValue is 0 or > 64 || owner->Held > owner->CapacityValue) return OwnershipStatus.InvalidState;
        ulong entriesBytes = owner->CapacityValue * (ulong)sizeof(TcpConnectionTableEntry), generationBytes = owner->CapacityValue * 8UL;
        if (!OwnedStorage(owner->Entries, entriesBytes) || !OwnedStorage(owner->Generations, generationBytes) ||
            OwnedOverlap((ulong)owner, (ulong)sizeof(Owner), (ulong)owner->Entries, entriesBytes) ||
            OwnedOverlap((ulong)owner, (ulong)sizeof(Owner), (ulong)owner->Generations, generationBytes) ||
            OwnedOverlap((ulong)owner->Entries, entriesBytes, (ulong)owner->Generations, generationBytes)) return OwnershipStatus.CorruptState;
        if (Scan(OwnedEntries(owner), default, true).Status != TcpConnectionTableStatus.Success) return OwnershipStatus.CorruptState;
        int held = 0;
        for (int i = 0; i < owner->CapacityValue; i++)
        {
            var entry = owner->Entries[i];
            if (entry.State == TcpConnectionLifecycleState.Free)
            {
                if (!OwnedZero(owner->Entries + i, (ulong)sizeof(TcpConnectionTableEntry))) return OwnershipStatus.CorruptState;
                continue;
            }
            if (!OwnedEndpoint(entry.Key) || owner->Generations[i] == 0) return OwnershipStatus.CorruptState;
            for (int j = 0; j < i; j++)
                if (owner->Entries[j].State != TcpConnectionLifecycleState.Free && owner->Entries[j].Key.HasSameEndpoint(entry.Key))
                    return OwnershipStatus.CorruptState;
            held++;
        }
        return held == owner->Held ? OwnershipStatus.Success : OwnershipStatus.CorruptState;
    }
    private static Span<TcpConnectionTableEntry> OwnedEntries(Owner* owner) => new(owner->Entries, owner->CapacityValue);
    private static bool OwnedEndpoint(TcpConnectionKey key) => key.HasValidEndpoint && key.LocalAddress.Octet0 is > 0 and < 224 && key.RemoteAddress.Octet0 is > 0 and < 224;
    private static bool OwnedStorage(void* address, ulong bytes) => address != null && ((ulong)address & 7) == 0 && bytes != 0 && bytes <= ~0UL - (ulong)address;
    private static bool OwnedOverlap(ulong a, ulong bytes, ulong b, ulong other) => a < b + other && b < a + bytes;
    private static bool OwnedZero(void* address, ulong bytes)
    { for (ulong i = 0; i < bytes; i++) if (((byte*)address)[i] != 0) return false; return true; }

    public const int NoEntryIndex = -1;

    public const bool RequiresCallerOwnedEntryStorage = true;
    public const bool AllocatesEntries = false;
    public const bool OwnsTimers = false;
    public const bool DispatchesSockets = false;
    public const bool PerformsNicIo = false;

    public static TcpConnectionTableResult AddEstablished(
        Span<TcpConnectionTableEntry> entries,
        TcpConnectionKey key,
        TcpConnectionSequenceMetadata sequence)
    {
        TcpConnectionTableStatus validationStatus = ValidateKeyAndSequence(key, sequence);
        if (validationStatus != TcpConnectionTableStatus.Success)
        {
            return Result(validationStatus, entries, count: 0, requiredCapacity: 1, NoEntryIndex, default);
        }

        ScanResult scan = Scan(entries, key);
        if (scan.Status != TcpConnectionTableStatus.Success)
        {
            return Result(
                scan.Status,
                entries,
                scan.Count,
                scan.Count + 1,
                scan.MatchingEntryIndex,
                default);
        }

        if (scan.MatchingEntryIndex != NoEntryIndex)
        {
            return Result(
                TcpConnectionTableStatus.DuplicateEndpoint,
                entries,
                scan.Count,
                scan.Count + 1,
                scan.MatchingEntryIndex,
                entries[scan.MatchingEntryIndex]);
        }

        if (scan.FirstReusableEntryIndex == NoEntryIndex)
        {
            return Result(
                TcpConnectionTableStatus.TableFull,
                entries,
                scan.Count,
                scan.Count + 1,
                NoEntryIndex,
                default);
        }

        int entryIndex = scan.FirstReusableEntryIndex;
        entries[entryIndex] = TcpConnectionTableEntry.Established(key, sequence);
        return Result(
            TcpConnectionTableStatus.Success,
            entries,
            scan.Count + 1,
            scan.Count + 1,
            entryIndex,
            entries[entryIndex]);
    }

    public static TcpConnectionTableResult Lookup(
        ReadOnlySpan<TcpConnectionTableEntry> entries,
        TcpConnectionKey key)
    {
        if (!key.HasValidEndpoint)
        {
            return Result(
                TcpConnectionTableStatus.InvalidEndpoint,
                entries,
                count: 0,
                requiredCapacity: 1,
                NoEntryIndex,
                default);
        }

        ScanResult scan = Scan(entries, key);
        if (scan.Status != TcpConnectionTableStatus.Success)
        {
            return Result(scan.Status, entries, scan.Count, scan.Count, scan.MatchingEntryIndex, default);
        }

        if (scan.MatchingEntryIndex == NoEntryIndex)
        {
            return Result(
                TcpConnectionTableStatus.NotFound,
                entries,
                scan.Count,
                requiredCapacity: 1,
                NoEntryIndex,
                default);
        }

        return Result(
            TcpConnectionTableStatus.Success,
            entries,
            scan.Count,
            scan.Count,
            scan.MatchingEntryIndex,
            entries[scan.MatchingEntryIndex]);
    }

    public static TcpConnectionTableResult UpdateSequenceMetadata(
        Span<TcpConnectionTableEntry> entries,
        TcpConnectionKey key,
        TcpConnectionSequenceMetadata sequence)
    {
        if (!sequence.HasValidMetadata)
        {
            return Result(
                TcpConnectionTableStatus.InvalidSequenceMetadata,
                entries,
                count: 0,
                requiredCapacity: 1,
                NoEntryIndex,
                default);
        }

        TcpConnectionTableResult lookup = Lookup(entries, key);
        if (!lookup.Succeeded)
        {
            return lookup;
        }

        TcpConnectionTableEntry current = lookup.Entry;
        entries[lookup.EntryIndex] = current.State == TcpConnectionLifecycleState.Closing
            ? TcpConnectionTableEntry.Closing(current.Key, sequence)
            : TcpConnectionTableEntry.Established(current.Key, sequence);
        return Result(
            TcpConnectionTableStatus.Success,
            entries,
            lookup.Count,
            lookup.RequiredCapacity,
            lookup.EntryIndex,
            entries[lookup.EntryIndex]);
    }

    public static TcpConnectionTableResult MarkClosing(
        Span<TcpConnectionTableEntry> entries,
        TcpConnectionKey key)
    {
        TcpConnectionTableResult lookup = Lookup(entries, key);
        if (!lookup.Succeeded)
        {
            return lookup;
        }

        if (lookup.Entry.State == TcpConnectionLifecycleState.Closing)
        {
            return lookup;
        }

        if (lookup.Entry.State != TcpConnectionLifecycleState.Established)
        {
            return Result(
                TcpConnectionTableStatus.InvalidLifecycleState,
                entries,
                lookup.Count,
                lookup.RequiredCapacity,
                lookup.EntryIndex,
                lookup.Entry);
        }

        entries[lookup.EntryIndex] = TcpConnectionTableEntry.Closing(
            lookup.Entry.Key,
            lookup.Entry.Sequence);
        return Result(
            TcpConnectionTableStatus.Success,
            entries,
            lookup.Count,
            lookup.RequiredCapacity,
            lookup.EntryIndex,
            entries[lookup.EntryIndex]);
    }

    public static TcpConnectionTableResult Remove(
        Span<TcpConnectionTableEntry> entries,
        TcpConnectionKey key)
    {
        TcpConnectionTableResult lookup = Lookup(entries, key);
        if (!lookup.Succeeded)
        {
            return lookup;
        }

        entries[lookup.EntryIndex] = TcpConnectionTableEntry.Closed(
            lookup.Entry.Key,
            lookup.Entry.Sequence);
        return Result(
            TcpConnectionTableStatus.Success,
            entries,
            lookup.Count - 1,
            lookup.Count,
            lookup.EntryIndex,
            entries[lookup.EntryIndex]);
    }

    public static TcpConnectionTableResult Inspect(ReadOnlySpan<TcpConnectionTableEntry> entries)
    {
        ScanResult scan = Scan(entries, default);
        return Result(scan.Status, entries, scan.Count, scan.Count, scan.MatchingEntryIndex, default);
    }

    private static TcpConnectionTableStatus ValidateKeyAndSequence(
        TcpConnectionKey key,
        TcpConnectionSequenceMetadata sequence)
    {
        if (!key.HasValidEndpoint)
        {
            return TcpConnectionTableStatus.InvalidEndpoint;
        }

        return sequence.HasValidMetadata
            ? TcpConnectionTableStatus.Success
            : TcpConnectionTableStatus.InvalidSequenceMetadata;
    }

    private static TcpConnectionTableResult Result(
        TcpConnectionTableStatus status,
        ReadOnlySpan<TcpConnectionTableEntry> entries,
        int count,
        int requiredCapacity,
        int entryIndex,
        TcpConnectionTableEntry entry)
    {
        return TcpConnectionTableResult.Create(
            status,
            count,
            entries.Length,
            requiredCapacity,
            entryIndex,
            entry);
    }

    private static ScanResult Scan(
        ReadOnlySpan<TcpConnectionTableEntry> entries,
        TcpConnectionKey match, bool retainClosed = false)
    {
        int count = 0;
        int firstReusableEntryIndex = NoEntryIndex;
        int matchingEntryIndex = NoEntryIndex;

        for (int index = 0; index < entries.Length; index++)
        {
            TcpConnectionTableEntry entry = entries[index];
            if (entry.State == TcpConnectionLifecycleState.Free ||
                !retainClosed && entry.State == TcpConnectionLifecycleState.Closed)
            {
                if (firstReusableEntryIndex == NoEntryIndex)
                {
                    firstReusableEntryIndex = index;
                }

                continue;
            }

            if (entry.State != TcpConnectionLifecycleState.Established &&
                entry.State != TcpConnectionLifecycleState.Closing &&
                entry.State != TcpConnectionLifecycleState.SynReceived &&
                (!retainClosed || entry.State != TcpConnectionLifecycleState.Closed))
            {
                return new ScanResult(
                    TcpConnectionTableStatus.InvalidEntryState,
                    count,
                    firstReusableEntryIndex,
                    matchingEntryIndex);
            }

            if (!entry.Key.HasValidEndpoint || !entry.Sequence.HasValidMetadata)
            {
                return new ScanResult(
                    TcpConnectionTableStatus.InvalidEntryState,
                    count,
                    firstReusableEntryIndex,
                    matchingEntryIndex);
            }

            count++;
            if (match.HasValidEndpoint && entry.Key.HasSameEndpoint(match))
            {
                matchingEntryIndex = index;
            }
        }

        return new ScanResult(
            TcpConnectionTableStatus.Success,
            count,
            firstReusableEntryIndex,
            matchingEntryIndex);
    }

    private readonly struct ScanResult
    {
        public ScanResult(
            TcpConnectionTableStatus status,
            int count,
            int firstReusableEntryIndex,
            int matchingEntryIndex)
        {
            Status = status;
            Count = count;
            FirstReusableEntryIndex = firstReusableEntryIndex;
            MatchingEntryIndex = matchingEntryIndex;
        }

        public TcpConnectionTableStatus Status { get; }

        public int Count { get; }

        public int FirstReusableEntryIndex { get; }

        public int MatchingEntryIndex { get; }
    }
}
