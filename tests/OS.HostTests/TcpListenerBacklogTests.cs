using System.Runtime.CompilerServices;
using OS.Net;

internal static class TcpListenerBacklogTests
{

    [HostTest("net.tcp.listener enqueues passive connection", HostLane.Fast, 0)]
    private static bool TestEnqueuesPassiveConnection()
    {
        TcpListenerBacklogEntry[] entries = new TcpListenerBacklogEntry[2];
        TcpListenerBacklogConnection connection = Connection(remotePort: 49152);

        TcpListenerBacklogResult result = TcpListenerBacklog.Enqueue(entries, connection);

        return HostCheck.Result(result.Succeeded
            && result.Status == TcpListenerBacklogStatus.Success
            && result.Count == 1
            && result.Capacity == entries.Length
            && result.RequiredCapacity == 1
            && result.EntryIndex == 0
            && result.Connection.HasSameEndpoint(connection)
            && result.Connection.LocalNextSequenceNumber == connection.LocalNextSequenceNumber
            && result.Connection.RemoteNextSequenceNumber == connection.RemoteNextSequenceNumber
            && entries[0].State == TcpListenerBacklogEntryState.PendingAccept
            && entries[0].Connection.HasSameEndpoint(connection)
            && entries[0].EnqueueOrder == 1
            && entries[1].State == TcpListenerBacklogEntryState.Free);
    }

    [HostTest("net.tcp.listener dequeues accepted connection", HostLane.Fast, 1)]
    private static bool TestDequeuesAcceptedConnection()
    {
        TcpListenerBacklogEntry[] entries = new TcpListenerBacklogEntry[2];
        TcpListenerBacklogConnection first = Connection(remotePort: 49152, remoteSequence: 10);
        TcpListenerBacklogConnection second = Connection(remotePort: 49153, remoteSequence: 20);
        TcpListenerBacklogConnection third = Connection(remotePort: 49154, remoteSequence: 30);

        TcpListenerBacklog.Enqueue(entries, first);
        TcpListenerBacklog.Enqueue(entries, second);
        TcpListenerBacklogResult acceptedFirst = TcpListenerBacklog.Accept(entries);
        TcpListenerBacklogResult enqueueThird = TcpListenerBacklog.Enqueue(entries, third);
        TcpListenerBacklogResult acceptedSecond = TcpListenerBacklog.Accept(entries);

        return HostCheck.Result(acceptedFirst.Succeeded
            && acceptedFirst.Connection.HasSameEndpoint(first)
            && acceptedFirst.Connection.RemoteNextSequenceNumber == first.RemoteNextSequenceNumber
            && acceptedFirst.Count == 1
            && entries[0].State == TcpListenerBacklogEntryState.PendingAccept
            && enqueueThird.Succeeded
            && enqueueThird.EntryIndex == 0
            && entries[0].Connection.HasSameEndpoint(third)
            && acceptedSecond.Succeeded
            && acceptedSecond.Connection.HasSameEndpoint(second)
            && acceptedSecond.Connection.RemoteNextSequenceNumber == second.RemoteNextSequenceNumber
            && acceptedSecond.EntryIndex == 1
            && acceptedSecond.Count == 1
            && entries[1].State == TcpListenerBacklogEntryState.Free);
    }

    [HostTest("net.tcp.listener rejects duplicate endpoint", HostLane.Fast, 2)]
    private static bool TestRejectsDuplicateEndpoint()
    {
        TcpListenerBacklogEntry[] entries = new TcpListenerBacklogEntry[2];
        TcpListenerBacklogConnection connection = Connection(remotePort: 49152);

        TcpListenerBacklogResult first = TcpListenerBacklog.Enqueue(entries, connection);
        TcpListenerBacklogResult duplicate = TcpListenerBacklog.Enqueue(entries, connection);

        return HostCheck.Result(first.Succeeded
            && !duplicate.Succeeded
            && duplicate.Status == TcpListenerBacklogStatus.DuplicateEndpoint
            && duplicate.EntryIndex == first.EntryIndex
            && duplicate.Count == 1
            && duplicate.Capacity == entries.Length
            && entries[0].Connection.HasSameEndpoint(connection)
            && entries[1].State == TcpListenerBacklogEntryState.Free);
    }

    [HostTest("net.tcp.listener reports backlog capacity", HostLane.Fast, 3)]
    private static bool TestReportsBacklogCapacity()
    {
        TcpListenerBacklogEntry[] entries = new TcpListenerBacklogEntry[1];
        TcpListenerBacklogConnection first = Connection(remotePort: 49152);
        TcpListenerBacklogConnection second = Connection(remotePort: 49153);

        TcpListenerBacklogResult enqueue = TcpListenerBacklog.Enqueue(entries, first);
        TcpListenerBacklogResult full = TcpListenerBacklog.Enqueue(entries, second);
        TcpListenerBacklogResult inspect = TcpListenerBacklog.Inspect(entries);

        return HostCheck.Result(enqueue.Succeeded
            && !full.Succeeded
            && full.Status == TcpListenerBacklogStatus.BacklogFull
            && full.Count == 1
            && full.Capacity == 1
            && full.RequiredCapacity == 2
            && full.EntryIndex == TcpListenerBacklog.NoEntryIndex
            && inspect.Succeeded
            && inspect.Count == 1
            && inspect.Capacity == 1
            && inspect.RequiredCapacity == 1
            && inspect.Connection.HasSameEndpoint(first));
    }

    [HostTest("net.tcp.listener result contains no managed references", HostLane.Fast, 4)]
    private static bool TestResultContainsNoManagedReferences()
    {
        TcpListenerBacklogEntry[] entries = new TcpListenerBacklogEntry[1];
        TcpListenerBacklogResult empty = TcpListenerBacklog.Accept(entries);
        TcpListenerBacklogResult invalid = TcpListenerBacklog.Enqueue(entries, default);

        return HostCheck.Result(!RuntimeHelpers.IsReferenceOrContainsReferences<TcpListenerBacklogConnection>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<TcpListenerBacklogEntry>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<TcpListenerBacklogResult>()
            && !TcpListenerBacklog.ParsesPackets
            && !TcpListenerBacklog.AllocatesEntries
            && !TcpListenerBacklog.ExportsSocketHandles
            && TcpListenerBacklog.RequiresCallerOwnedEntryStorage
            && empty.Status == TcpListenerBacklogStatus.BacklogEmpty
            && empty.Count == 0
            && empty.Capacity == 1
            && invalid.Status == TcpListenerBacklogStatus.InvalidEndpoint
            && entries[0].State == TcpListenerBacklogEntryState.Free);
    }

    [HostTest("net.tcp.listener default result is not success", HostLane.Fast, 5)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        TcpListenerBacklogResult defaultResult = default;
        TcpListenerBacklogConnection connection = Connection(remotePort: 49152);
        TcpListenerBacklogResult failureResult = TcpListenerBacklogResult.Create(
            TcpListenerBacklogStatus.InvalidEndpoint,
            count: 0,
            capacity: 1,
            requiredCapacity: 1,
            TcpListenerBacklog.NoEntryIndex,
            connection);

        return HostCheck.Result(!defaultResult.IsInitialized
            && !defaultResult.Succeeded
            && defaultResult.Status == TcpListenerBacklogStatus.Success
            && failureResult.IsInitialized
            && !failureResult.Succeeded
            && failureResult.Status == TcpListenerBacklogStatus.InvalidEndpoint
            && failureResult.Count == 0
            && failureResult.Capacity == 1
            && failureResult.RequiredCapacity == 1
            && failureResult.EntryIndex == TcpListenerBacklog.NoEntryIndex
            && failureResult.Connection.HasSameEndpoint(connection)
            && !RuntimeHelpers.IsReferenceOrContainsReferences<TcpListenerBacklogResult>());
    }

    private static TcpListenerBacklogConnection Connection(
        ushort remotePort,
        uint remoteSequence = 0x10203041)
    {
        return new TcpListenerBacklogConnection(
            new Ipv4Address(192, 0, 2, 10),
            localPort: 8080,
            new Ipv4Address(198, 51, 100, (byte)(remotePort & 0xFF)),
            remotePort,
            localNextSequenceNumber: 0xA0B0C0D1,
            remoteNextSequenceNumber: remoteSequence);
    }
}
