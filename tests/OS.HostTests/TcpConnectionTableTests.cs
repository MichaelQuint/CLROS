using System.Runtime.CompilerServices;
using OS.Net;
using Tcb = global::OS.Net.TcpConnectionTable;

internal static unsafe class TcpConnectionTableTests
{
    [HostTest("net.tcp.owned holds closed tuples and rejects stale recycled identities", HostLane.Fast, 20)]
    private static void OwnedLifetime()
    {
        using var f = new OwnedFixture(2);
        HostCheck.Equal(Tcb.OwnershipStatus.Success, Tcb.ReserveOwned(f.Owner, Key(RemotePort), new(0, 0), out var first));
        HostCheck.Equal(Tcb.OwnershipStatus.Success, Tcb.ReserveOwned(f.Owner, Key(RemotePort + 1), Sequence(), out var second));
        HostCheck.Equal(Tcb.OwnershipStatus.DuplicateEndpoint, Tcb.ReserveOwned(f.Owner, Key(RemotePort), Sequence(), out var rejected));
        HostCheck.True(!rejected.IsInitialized && f.Owner->Count == 2);
        HostCheck.Equal(Tcb.OwnershipStatus.Full, Tcb.ReserveOwned(f.Owner, Key(RemotePort + 2), Sequence(), out _));
        HostCheck.Equal(Tcb.OwnershipStatus.Success, Tcb.ReadOwned(f.Owner, first, out var pending));
        HostCheck.True(pending.State == TcpConnectionLifecycleState.SynReceived && pending.Sequence.HasValidMetadata && pending.Sequence.LocalNextSequenceNumber == 0);
        HostCheck.Equal(Tcb.OwnershipStatus.InvalidTransition, Tcb.ReleaseOwned(f.Owner, first));
        HostCheck.Equal(Tcb.OwnershipStatus.Success, Tcb.TransitionOwned(f.Owner, first, pending.State, TcpConnectionLifecycleState.Closed));
        HostCheck.Equal(Tcb.OwnershipStatus.DuplicateEndpoint, Tcb.ReserveOwned(f.Owner, pending.Key, Sequence(), out _));
        HostCheck.Equal(Tcb.OwnershipStatus.Success, Tcb.FindOwned(f.Owner, pending.Key, out var closed));
        HostCheck.Equal(first.SlotIndex, closed.SlotIndex);
        HostCheck.Equal(Tcb.OwnershipStatus.ConnectionsRemain, Tcb.RetireOwned(f.Owner));
        HostCheck.Equal(Tcb.OwnershipStatus.Success, Tcb.ReleaseOwned(f.Owner, first));
        HostCheck.True(f.Entries[0].State == TcpConnectionLifecycleState.Free && !f.Entries[0].Sequence.HasValidMetadata);
        HostCheck.Equal(Tcb.OwnershipStatus.StaleHandle, Tcb.ReadOwned(f.Owner, first, out _));
        HostCheck.Equal(Tcb.OwnershipStatus.Success, Tcb.ReserveOwned(f.Owner, pending.Key, new(0, uint.MaxValue), out var next));
        HostCheck.True(next.SlotIndex == first.SlotIndex && f.Generations[0] == 2);
        HostCheck.Equal(Tcb.OwnershipStatus.StaleHandle, Tcb.UpdateOwned(f.Owner, first, Sequence()));
        HostCheck.Equal(Tcb.OwnershipStatus.Success, Tcb.ReadOwned(f.Owner, second, out var unchanged));
        HostCheck.Equal(LocalNextSequence, unchanged.Sequence.LocalNextSequenceNumber);
        CloseAndRelease(f.Owner, second); CloseAndRelease(f.Owner, next);
        HostCheck.Equal(Tcb.OwnershipStatus.Success, Tcb.RetireOwned(f.Owner));
        HostCheck.True(f.Owner->Retired && f.Owner->Count == 0 && f.Generations[0] == 0 && f.Generations[1] == 0);
        HostCheck.Equal(Tcb.OwnershipStatus.InvalidState, Tcb.ReserveOwned(f.Owner, pending.Key, Sequence(), out _));
    }

    [HostTest("net.tcp.owned rejects copied owners foreign and previous-epoch handles", HostLane.Fast, 21)]
    private static void OwnedIdentity()
    {
        using var a = new OwnedFixture(2); using var b = new OwnedFixture(2);
        HostCheck.Equal(Tcb.OwnershipStatus.Success, Tcb.ReserveOwned(a.Owner, Key(RemotePort), Sequence(), out var handle));
        HostCheck.Equal(Tcb.OwnershipStatus.StaleHandle, Tcb.ReadOwned(b.Owner, handle, out _));
        Tcb.Owner copy = *a.Owner;
        HostCheck.Equal(Tcb.OwnershipStatus.InvalidState, Tcb.ReadOwned(&copy, handle, out _));
        HostCheck.Equal(Tcb.OwnershipStatus.StaleHandle, Tcb.ReadOwned(a.Owner, default, out _));
        CloseAndRelease(a.Owner, handle);
        HostCheck.Equal(Tcb.OwnershipStatus.Success, Tcb.RetireOwned(a.Owner));
        System.Runtime.InteropServices.NativeMemory.Clear(a.Owner, (nuint)sizeof(Tcb.Owner));
        HostCheck.Equal(Tcb.OwnershipStatus.Success, Tcb.InitializeOwned(a.Owner, a.Entries, 2, a.Generations, 2, 2));
        HostCheck.Equal(Tcb.OwnershipStatus.Success, Tcb.ReserveOwned(a.Owner, Key(RemotePort), Sequence(), out var current));
        HostCheck.Equal(Tcb.OwnershipStatus.StaleHandle, Tcb.ReadOwned(a.Owner, handle, out _));
        HostCheck.Equal(Tcb.OwnershipStatus.Success, Tcb.ReadOwned(a.Owner, current, out _));
    }

    [HostTest("net.tcp.owned admits initialized zero sequences and guards lifecycle updates", HostLane.Fast, 22)]
    private static void OwnedSequence()
    {
        using var f = new OwnedFixture(1);
        HostCheck.Equal(Tcb.OwnershipStatus.InvalidSequence, Tcb.ReserveOwned(f.Owner, Key(RemotePort), default, out _));
        HostCheck.Equal(Tcb.OwnershipStatus.Success, Tcb.ReserveOwned(f.Owner, Key(RemotePort), new(uint.MaxValue, 0), out var handle));
        HostCheck.Equal(Tcb.OwnershipStatus.InvalidTransition, Tcb.TransitionOwned(f.Owner, handle, TcpConnectionLifecycleState.Established, TcpConnectionLifecycleState.Closing));
        HostCheck.Equal(Tcb.OwnershipStatus.InvalidTransition, Tcb.TransitionOwned(f.Owner, handle, TcpConnectionLifecycleState.SynReceived, TcpConnectionLifecycleState.Closing));
        HostCheck.Equal(Tcb.OwnershipStatus.Success, Tcb.TransitionOwned(f.Owner, handle, TcpConnectionLifecycleState.SynReceived, TcpConnectionLifecycleState.Established));
        HostCheck.Equal(Tcb.OwnershipStatus.Success, Tcb.UpdateOwned(f.Owner, handle, new(0, 0)));
        HostCheck.Equal(Tcb.OwnershipStatus.InvalidSequence, Tcb.UpdateOwned(f.Owner, handle, default));
        HostCheck.Equal(Tcb.OwnershipStatus.Success, Tcb.ReadOwned(f.Owner, handle, out var entry));
        HostCheck.True(entry.Sequence.LocalNextSequenceNumber == 0 && entry.Sequence.RemoteNextSequenceNumber == 0 && entry.Sequence.HasValidMetadata);
        HostCheck.Equal(Tcb.OwnershipStatus.Success, Tcb.TransitionOwned(f.Owner, handle, entry.State, TcpConnectionLifecycleState.Closing));
        HostCheck.Equal(Tcb.OwnershipStatus.Success, Tcb.UpdateOwned(f.Owner, handle, new(7, 0)));
        HostCheck.Equal(Tcb.OwnershipStatus.Success, Tcb.TransitionOwned(f.Owner, handle, TcpConnectionLifecycleState.Closing, TcpConnectionLifecycleState.Closed));
        HostCheck.Equal(Tcb.OwnershipStatus.InvalidTransition, Tcb.UpdateOwned(f.Owner, handle, Sequence()));
        HostCheck.Equal(Tcb.OwnershipStatus.Success, Tcb.ReleaseOwned(f.Owner, handle));
        Span<TcpConnectionTableEntry> legacy = stackalloc TcpConnectionTableEntry[1]; legacy.Clear();
        HostCheck.True(Tcb.AddEstablished(legacy, Key(RemotePort), new(0, 0)).Succeeded);
        HostCheck.True(!Tcb.UpdateSequenceMetadata(legacy, Key(RemotePort), default).Succeeded);
    }

    [HostTest("net.tcp.owned rejects dirty short aliased misaligned and unsupported storage before writes", HostLane.Fast, 23)]
    private static void OwnedAdmission()
    {
        using var f = new OwnedFixture(2, initialize:false);
        HostCheck.Equal(Tcb.OwnershipStatus.InvalidStorage, Tcb.InitializeOwned(f.Owner, f.Entries, 2, f.Generations, 1, 1));
        HostCheck.Equal(Tcb.OwnershipStatus.InvalidStorage, Tcb.InitializeOwned(f.Owner, f.Entries, 65, f.Generations, 65, 1));
        HostCheck.Equal(Tcb.OwnershipStatus.InvalidStorage, Tcb.InitializeOwned(f.Owner, f.Entries, 2, f.Generations, 2, 0));
        HostCheck.Equal(Tcb.OwnershipStatus.InvalidStorage, Tcb.InitializeOwned((Tcb.Owner*)((byte*)f.Owner + 1), f.Entries, 2, f.Generations, 2, 1));
        HostCheck.Equal(Tcb.OwnershipStatus.InvalidStorage, Tcb.InitializeOwned(f.Owner, (TcpConnectionTableEntry*)f.Owner, 2, f.Generations, 2, 1));
        HostCheck.Equal(Tcb.OwnershipStatus.InvalidStorage, Tcb.InitializeOwned(f.Owner, f.Entries, 2, (ulong*)f.Entries, 2, 1));
        f.Generations[1] = 7;
        HostCheck.Equal(Tcb.OwnershipStatus.InvalidStorage, Tcb.InitializeOwned(f.Owner, f.Entries, 2, f.Generations, 2, 1));
        HostCheck.True(new ReadOnlySpan<byte>(f.Owner, sizeof(Tcb.Owner)).IndexOfAnyExcept((byte)0) < 0 && f.Generations[1] == 7);
        f.Generations[1] = 0;
        HostCheck.Equal(Tcb.OwnershipStatus.Success, Tcb.InitializeOwned(f.Owner, f.Entries, 2, f.Generations, 2, 1));
        foreach (var key in new[] { default(TcpConnectionKey), new(default, 80, new(10,0,2,2), 81), new(new(10,0,2,15), 80, new(224,0,0,1), 81), new(new(10,0,2,15), 0, new(10,0,2,2), 81) })
            HostCheck.Equal(Tcb.OwnershipStatus.InvalidEndpoint, Tcb.ReserveOwned(f.Owner, key, Sequence(), out _));
        HostCheck.True(f.Owner->Count == 0 && f.Generations[0] == 0);
        HostCheck.Equal(Tcb.OwnershipStatus.InvalidStorage, Tcb.InitializeOwned(f.Owner, f.Entries, 2, f.Generations, 2, 2));
    }

    [HostTest("net.tcp.owned detects corrupt accounting duplicate tuples generations and metadata aliases", HostLane.Fast, 24)]
    private static void OwnedCorruption()
    {
        using var f = new OwnedFixture(2);
        Tcb.ReserveOwned(f.Owner, Key(RemotePort), Sequence(), out var first);
        Tcb.ReserveOwned(f.Owner, Key(RemotePort + 1), Sequence(), out _);
        *OwnerField<ushort>(f.Owner, "Held") = 1;
        HostCheck.Equal(Tcb.OwnershipStatus.CorruptState, Tcb.ReadOwned(f.Owner, first, out _)); *OwnerField<ushort>(f.Owner, "Held") = 2;
        var saved = f.Entries[1]; f.Entries[1] = f.Entries[0];
        HostCheck.Equal(Tcb.OwnershipStatus.CorruptState, Tcb.FindOwned(f.Owner, Key(RemotePort), out _)); f.Entries[1] = saved;
        ulong generation = f.Generations[0]; f.Generations[0] = 0;
        HostCheck.Equal(Tcb.OwnershipStatus.CorruptState, Tcb.ReadOwned(f.Owner, first, out _)); f.Generations[0] = generation;
        ulong* entryPointer = OwnerField<ulong>(f.Owner, "Entries"); ulong pointer = *entryPointer; *entryPointer = (ulong)f.Owner;
        HostCheck.Equal(Tcb.OwnershipStatus.CorruptState, Tcb.ReadOwned(f.Owner, first, out _)); *entryPointer = pointer;
        HostCheck.Equal(Tcb.OwnershipStatus.Success, Tcb.ReadOwned(f.Owner, first, out _));
    }

    [HostTest("net.tcp.owned never wraps generations and skips an exhausted free slot", HostLane.Fast, 25)]
    private static void OwnedExhaustion()
    {
        using var f = new OwnedFixture(2); f.Generations[0] = ulong.MaxValue;
        HostCheck.Equal(Tcb.OwnershipStatus.Success, Tcb.ReserveOwned(f.Owner, Key(RemotePort), Sequence(), out var handle));
        HostCheck.Equal(1, handle.SlotIndex); CloseAndRelease(f.Owner, handle);
        f.Generations[1] = ulong.MaxValue;
        HostCheck.Equal(Tcb.OwnershipStatus.GenerationExhausted, Tcb.ReserveOwned(f.Owner, Key(RemotePort), Sequence(), out _));
        HostCheck.Equal(0, f.Owner->Count);
        HostCheck.Equal(Tcb.OwnershipStatus.Success, Tcb.RetireOwned(f.Owner));
    }

    [HostTest("net.tcp.owned bounds sixty-four independent live connections", HostLane.Fast, 26)]
    private static void OwnedCapacity()
    {
        using var f = new OwnedFixture(64); Span<Tcb.Handle> handles = stackalloc Tcb.Handle[64];
        for (ushort i = 0; i < 64; i++)
            HostCheck.Equal(Tcb.OwnershipStatus.Success, Tcb.ReserveOwned(f.Owner, Key((ushort)(RemotePort + i)), new(i, 0), out handles[i]));
        HostCheck.Equal(64, f.Owner->Count);
        HostCheck.Equal(Tcb.OwnershipStatus.Full, Tcb.ReserveOwned(f.Owner, Key(RemotePort + 64), Sequence(), out _));
        for (int i = 63; i >= 0; i--) CloseAndRelease(f.Owner, handles[i]);
        HostCheck.Equal(Tcb.OwnershipStatus.Success, Tcb.RetireOwned(f.Owner));
    }

    [HostTest("net.tcp.owned warmed reserve lookup update release allocates no managed storage", HostLane.Fast, 27)]
    private static void OwnedAllocations()
    {
        using var f = new OwnedFixture(2); var key = Key(RemotePort); var sequence = new TcpConnectionSequenceMetadata(0, 0);
        bool Cycle()
        {
            return Tcb.ReserveOwned(f.Owner, key, sequence, out var handle) == Tcb.OwnershipStatus.Success &&
                Tcb.FindOwned(f.Owner, key, out var found) == Tcb.OwnershipStatus.Success && found.SlotIndex == handle.SlotIndex &&
                Tcb.UpdateOwned(f.Owner, found, sequence) == Tcb.OwnershipStatus.Success &&
                Tcb.TransitionOwned(f.Owner, found, TcpConnectionLifecycleState.SynReceived, TcpConnectionLifecycleState.Closed) == Tcb.OwnershipStatus.Success &&
                Tcb.ReleaseOwned(f.Owner, found) == Tcb.OwnershipStatus.Success;
        }
        for (int i = 0; i < 100; i++) HostCheck.True(Cycle());
        long before = GC.GetAllocatedBytesForCurrentThread(); bool success = true;
        for (int i = 0; i < 10000; i++) success &= Cycle();
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        HostCheck.True(success && bytes == 0 && f.Owner->Count == 0);
    }

    private static void CloseAndRelease(Tcb.Owner* owner, Tcb.Handle handle)
    {
        HostCheck.Equal(Tcb.OwnershipStatus.Success, Tcb.ReadOwned(owner, handle, out var entry));
        HostCheck.Equal(Tcb.OwnershipStatus.Success, Tcb.TransitionOwned(owner, handle, entry.State, TcpConnectionLifecycleState.Closed));
        HostCheck.Equal(Tcb.OwnershipStatus.Success, Tcb.ReleaseOwned(owner, handle));
    }
    // Host-only fault injection locates private storage without widening the
    // production API or freezing its byte offsets. Never used by kernel code.
    private static T* OwnerField<T>(Tcb.Owner* owner, string name) where T : unmanaged =>
        (T*)((byte*)owner + System.Runtime.InteropServices.Marshal.OffsetOf<Tcb.Owner>(name).ToInt32());
    private sealed class OwnedFixture : IDisposable
    {
        private readonly void* allocation;
        internal readonly Tcb.Owner* Owner;
        internal readonly TcpConnectionTableEntry* Entries;
        internal readonly ulong* Generations;
        internal OwnedFixture(uint capacity, bool initialize = true)
        {
            HostCheck.True(capacity <= 64 && 128 + capacity * sizeof(TcpConnectionTableEntry) <= 4096);
            allocation = System.Runtime.InteropServices.NativeMemory.AlignedAlloc(8192, 4096);
            System.Runtime.InteropServices.NativeMemory.Clear(allocation, 8192);
            Owner = (Tcb.Owner*)allocation; Entries = (TcpConnectionTableEntry*)((byte*)allocation + 128); Generations = (ulong*)((byte*)allocation + 4096);
            if (initialize) HostCheck.Equal(Tcb.OwnershipStatus.Success, Tcb.InitializeOwned(Owner, Entries, capacity, Generations, capacity, 1));
        }
        public void Dispose() => System.Runtime.InteropServices.NativeMemory.AlignedFree(allocation);
    }

    private const ushort LocalPort = 8080;
    private const ushort RemotePort = 49152;
    private const uint LocalNextSequence = 0x01020304;
    private const uint RemoteNextSequence = 0x10203040;

    [HostTest("net.tcp.connection table adds established connection", HostLane.Fast, 0)]
    private static bool TestAddsEstablishedConnection()
    {
        TcpConnectionTableEntry[] entries = new TcpConnectionTableEntry[2];
        TcpConnectionKey key = Key(remotePort: RemotePort);
        TcpConnectionSequenceMetadata sequence = Sequence();

        TcpConnectionTableResult result = TcpConnectionTable.AddEstablished(entries, key, sequence);

        return HostCheck.Result(result.Succeeded
            && result.Status == TcpConnectionTableStatus.Success
            && result.Count == 1
            && result.Capacity == entries.Length
            && result.RequiredCapacity == 1
            && result.EntryIndex == 0
            && result.Entry.State == TcpConnectionLifecycleState.Established
            && result.Entry.Key.HasSameEndpoint(key)
            && result.Entry.Sequence.LocalNextSequenceNumber == LocalNextSequence
            && result.Entry.Sequence.RemoteNextSequenceNumber == RemoteNextSequence
            && entries[0].State == TcpConnectionLifecycleState.Established
            && entries[1].State == TcpConnectionLifecycleState.Free
            && TcpConnectionTable.RequiresCallerOwnedEntryStorage
            && !TcpConnectionTable.AllocatesEntries
            && !TcpConnectionTable.DispatchesSockets
            && !TcpConnectionTable.PerformsNicIo);
    }

    [HostTest("net.tcp.connection table rejects duplicate and capacity", HostLane.Fast, 1)]
    private static bool TestRejectsDuplicateAndCapacity()
    {
        TcpConnectionTableEntry[] entries = new TcpConnectionTableEntry[1];
        TcpConnectionKey firstKey = Key(remotePort: RemotePort);
        TcpConnectionKey secondKey = Key(remotePort: RemotePort + 1);

        TcpConnectionTableResult first = TcpConnectionTable.AddEstablished(
            entries,
            firstKey,
            Sequence());
        TcpConnectionTableResult duplicate = TcpConnectionTable.AddEstablished(
            entries,
            firstKey,
            Sequence(localNextSequence: LocalNextSequence + 1));
        TcpConnectionTableResult full = TcpConnectionTable.AddEstablished(
            entries,
            secondKey,
            Sequence(remoteNextSequence: RemoteNextSequence + 1));

        return HostCheck.Result(first.Succeeded
            && !duplicate.Succeeded
            && duplicate.Status == TcpConnectionTableStatus.DuplicateEndpoint
            && duplicate.EntryIndex == first.EntryIndex
            && duplicate.Count == 1
            && !full.Succeeded
            && full.Status == TcpConnectionTableStatus.TableFull
            && full.Count == 1
            && full.Capacity == entries.Length
            && full.RequiredCapacity == 2
            && full.EntryIndex == TcpConnectionTable.NoEntryIndex
            && entries[0].Key.HasSameEndpoint(firstKey));
    }

    [HostTest("net.tcp.connection table looks up endpoint tuple", HostLane.Fast, 2)]
    private static bool TestLooksUpEndpointTuple()
    {
        TcpConnectionTableEntry[] entries = new TcpConnectionTableEntry[2];
        TcpConnectionKey firstKey = Key(remotePort: RemotePort);
        TcpConnectionKey secondKey = Key(remotePort: RemotePort + 1);

        TcpConnectionTable.AddEstablished(entries, firstKey, Sequence());
        TcpConnectionTable.AddEstablished(
            entries,
            secondKey,
            Sequence(localNextSequence: LocalNextSequence + 10));
        TcpConnectionTableResult lookup = TcpConnectionTable.Lookup(entries, secondKey);
        TcpConnectionTableResult missing = TcpConnectionTable.Lookup(
            entries,
            Key(remotePort: RemotePort + 2));

        return HostCheck.Result(lookup.Succeeded
            && lookup.EntryIndex == 1
            && lookup.Count == 2
            && lookup.Entry.Key.HasSameEndpoint(secondKey)
            && lookup.Entry.Sequence.LocalNextSequenceNumber == LocalNextSequence + 10
            && !missing.Succeeded
            && missing.Status == TcpConnectionTableStatus.NotFound
            && missing.EntryIndex == TcpConnectionTable.NoEntryIndex
            && missing.Count == 2);
    }

    [HostTest("net.tcp.connection table updates sequence metadata", HostLane.Fast, 3)]
    private static bool TestUpdatesSequenceMetadata()
    {
        TcpConnectionTableEntry[] entries = new TcpConnectionTableEntry[1];
        TcpConnectionKey key = Key(remotePort: RemotePort);

        TcpConnectionTable.AddEstablished(entries, key, Sequence());
        TcpConnectionSequenceMetadata updatedSequence = Sequence(
            localNextSequence: LocalNextSequence + 5,
            remoteNextSequence: RemoteNextSequence + 7);
        TcpConnectionTableResult updated = TcpConnectionTable.UpdateSequenceMetadata(
            entries,
            key,
            updatedSequence);
        TcpConnectionTableResult invalidSequence = TcpConnectionTable.UpdateSequenceMetadata(
            entries,
            key,
            default);

        return HostCheck.Result(updated.Succeeded
            && updated.EntryIndex == 0
            && updated.Entry.State == TcpConnectionLifecycleState.Established
            && updated.Entry.Sequence.LocalNextSequenceNumber == LocalNextSequence + 5
            && updated.Entry.Sequence.RemoteNextSequenceNumber == RemoteNextSequence + 7
            && entries[0].Sequence.LocalNextSequenceNumber == LocalNextSequence + 5
            && !invalidSequence.Succeeded
            && invalidSequence.Status == TcpConnectionTableStatus.InvalidSequenceMetadata
            && entries[0].Sequence.RemoteNextSequenceNumber == RemoteNextSequence + 7);
    }

    [HostTest("net.tcp.connection table closes removes and reuses slot", HostLane.Fast, 4)]
    private static bool TestClosesRemovesAndReusesSlot()
    {
        TcpConnectionTableEntry[] entries = new TcpConnectionTableEntry[2];
        TcpConnectionKey firstKey = Key(remotePort: RemotePort);
        TcpConnectionKey secondKey = Key(remotePort: RemotePort + 1);

        TcpConnectionTable.AddEstablished(entries, firstKey, Sequence());
        TcpConnectionTable.AddEstablished(
            entries,
            secondKey,
            Sequence(localNextSequence: LocalNextSequence + 1));
        TcpConnectionTableResult closing = TcpConnectionTable.MarkClosing(entries, firstKey);
        TcpConnectionTableResult removed = TcpConnectionTable.Remove(entries, firstKey);
        TcpConnectionTableResult missing = TcpConnectionTable.Lookup(entries, firstKey);
        TcpConnectionTableResult reused = TcpConnectionTable.AddEstablished(
            entries,
            Key(remotePort: RemotePort + 2),
            Sequence(localNextSequence: LocalNextSequence + 2));

        bool passed = closing.Succeeded
            && closing.Entry.State == TcpConnectionLifecycleState.Closing
            && removed.Succeeded
            && removed.Count == 1
            && removed.Entry.State == TcpConnectionLifecycleState.Closed
            && !missing.Succeeded
            && missing.Status == TcpConnectionTableStatus.NotFound
            && reused.Succeeded
            && reused.EntryIndex == 0
            && reused.Count == 2
            && reused.Entry.Key.HasSameEndpoint(Key(remotePort: RemotePort + 2))
            && entries[0].State == TcpConnectionLifecycleState.Established
            && entries[1].State == TcpConnectionLifecycleState.Established;

        return HostCheck.Result(passed);
    }

    [HostTest("net.tcp.connection table default result is not success", HostLane.Fast, 5)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        TcpConnectionTableResult defaultResult = default;
        TcpConnectionTableResult failureResult = TcpConnectionTableResult.Create(
            TcpConnectionTableStatus.InvalidEndpoint,
            count: 0,
            capacity: 1,
            requiredCapacity: 1,
            TcpConnectionTable.NoEntryIndex,
            default);
        TcpConnectionTableEntry[] entries = new TcpConnectionTableEntry[1];
        TcpConnectionTableResult invalidEndpoint = TcpConnectionTable.AddEstablished(
            entries,
            default,
            Sequence());
        TcpConnectionTableResult invalidSequence = TcpConnectionTable.AddEstablished(
            entries,
            Key(remotePort: RemotePort),
            default);

        return HostCheck.Result(!defaultResult.IsInitialized
            && !defaultResult.Succeeded
            && defaultResult.Status == TcpConnectionTableStatus.Success
            && failureResult.IsInitialized
            && !failureResult.Succeeded
            && failureResult.Status == TcpConnectionTableStatus.InvalidEndpoint
            && !invalidEndpoint.Succeeded
            && invalidEndpoint.Status == TcpConnectionTableStatus.InvalidEndpoint
            && !invalidSequence.Succeeded
            && invalidSequence.Status == TcpConnectionTableStatus.InvalidSequenceMetadata
            && entries[0].State == TcpConnectionLifecycleState.Free);
    }

    [HostTest("net.tcp.connection table layouts contain no managed references", HostLane.Fast, 6)]
    private static bool TestLayoutsContainNoManagedReferences()
    {
        TcpConnectionTableEntry[] entries = new TcpConnectionTableEntry[2];
        TcpConnectionTable.AddEstablished(entries, Key(remotePort: RemotePort), Sequence());
        TcpConnectionTable.AddEstablished(
            entries,
            Key(remotePort: RemotePort + 1),
            Sequence(localNextSequence: LocalNextSequence + 1));
        TcpConnectionTable.MarkClosing(entries, Key(remotePort: RemotePort + 1));

        TcpConnectionTableResult inspect = TcpConnectionTable.Inspect(entries);
        entries[1] = TcpConnectionTableEntry.Create(
            (TcpConnectionLifecycleState)99,
            Key(remotePort: RemotePort + 1),
            Sequence(localNextSequence: LocalNextSequence + 1));
        TcpConnectionTableResult invalid = TcpConnectionTable.Inspect(entries);

        return HostCheck.Result(!RuntimeHelpers.IsReferenceOrContainsReferences<TcpConnectionKey>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<TcpConnectionSequenceMetadata>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<TcpConnectionTableEntry>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<TcpConnectionTableResult>()
            && inspect.Succeeded
            && inspect.Count == 2
            && !invalid.Succeeded
            && invalid.Status == TcpConnectionTableStatus.InvalidEntryState
            && !TcpConnectionTable.OwnsTimers
            && !TcpConnectionTable.DispatchesSockets
            && !TcpConnectionTable.PerformsNicIo);
    }

    private static TcpConnectionKey Key(ushort remotePort)
    {
        return new TcpConnectionKey(
            new Ipv4Address(192, 0, 2, 10),
            LocalPort,
            new Ipv4Address(198, 51, 100, (byte)(remotePort & 0xFF)),
            remotePort);
    }

    private static TcpConnectionSequenceMetadata Sequence(
        uint localNextSequence = LocalNextSequence,
        uint remoteNextSequence = RemoteNextSequence)
    {
        return new TcpConnectionSequenceMetadata(localNextSequence, remoteNextSequence);
    }

}
