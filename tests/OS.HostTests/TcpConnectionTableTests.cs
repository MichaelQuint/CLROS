using System.Runtime.CompilerServices;
using OS.Net;

internal static class TcpConnectionTableTests
{
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
