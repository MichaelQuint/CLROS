using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Threading;
using OS.Pal;
using OS.Net;
using Fds = OS.Kernel.Compatibility.LinuxX64.LinuxDescriptorServices;
using Epoll = OS.Kernel.Compatibility.LinuxX64.LinuxEpollServices;
using Sockets = OS.Kernel.Compatibility.LinuxX64.LinuxSocketServices;
using Tx = OS.Net.TcpSendWindow;
using Wire = TcpReceiveWindowTests;
using Signals = OS.Kernel.Compatibility.LinuxX64.LinuxSignalServices;

internal static unsafe partial class LinuxEpollServicesTests
{
    private const ulong Key = LinuxThreadServicesTests.Key, User = LinuxThreadServicesTests.UserBase;
    private const ulong Event = User + 0x1200, Output = User + 0x2000, Data = User + 0x1800;
    private const uint Edge = 0x80000000;
    private const ulong SignalKey = 0x65706f6c6c736967;
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "ThreadOwner")]
    private static extern ref ulong DescriptorThreadOwner(ref Fds.State state);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "Self")]
    private static extern ref ulong EpollSelf(ref Epoll.State state);
    private static TcpSendWindowTests.Fixture Connection()
    {
        var connection = new TcpSendWindowTests.Fixture(capacity: 8, receiveCapacity: 8);
        var io = connection.Operations();
        HostCheck.Equal(Tx.Status.Success, Tx.ConfigureRetransmission(connection.Owner, io.Context, io.NowNanoseconds, 1_000_000_000_000));
        HostCheck.Equal(Tx.Status.Success, Tx.ConfigureClose(connection.Owner, 30_000_000_000));
        return connection;
    }

    [UnmanagedCallersOnly]
    private static byte ReadRefreshClock(void* context, ulong* value)
    {
        RefreshClock* clock = (RefreshClock*)context;
        clock->Calls++;
        clock->Refreshed = clock->Ready == null || *clock->Ready == 1;
        clock->Unwoken = clock->Table == null || clock->Table->Inspect(KernelThreadTableTests.Key, 2, clock->Parent).Completion == KernelThreadTable.Completion.None;
        *value = clock->Time;
        return clock->Result;
    }

    private static MeasuredReadiness BindMeasuredReadiness() => typeof(LinuxThreadServices).GetMethod("PollDescriptorReadinessMeasured", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.CreateDelegate<MeasuredReadiness>();
    [HostTest("timing readiness boundary follows actual refresh before any retained epoll wake and clock failure retains effects")]
    private static void MeasuredReadinessBoundary()
    {
        var poll = BindMeasuredReadiness();
        foreach (bool clockSucceeds in new[]
        {
            true,
            false
        }

        )
        {
            using Model m = new();
            var child = m.Thread.Clone();
            uint epoll = m.Create();
            var pipe = m.Pipe();
            HostCheck.Equal(0L, m.Control(epoll, 1, pipe.Read, 1, 77));
            HostCheck.Equal(LinuxSyscallAction.Blocked, m.Call(281, epoll, Output, 1, ulong.MaxValue).Action);
            m.Park(m.Parent);
            HostCheck.Equal(child.Value, m.Thread.Kernel.Dispatch().Value);
            uint* ready = (uint*)((byte*)m.Registrations + Marshal.OffsetOf<Epoll.Registration>("Ready"));
            HostCheck.Equal(0U, *ready);
            byte data = 0x63;
            HostCheck.Equal(1L, m.Files.Admission.Pipes[0].Write(&data, 1, true, true));
            RefreshClock clock = new()
            {
                Ready = ready,
                Table = m.Thread.Kernel.Table,
                Parent = m.Parent,
                Time = 1234,
                Result = clockSucceeds ? (byte)1 : (byte)0
            };
            HostCheck.Equal(clockSucceeds, poll(ref *m.Thread.Service, Key, 2, &clock, &ReadRefreshClock, out ulong boundary));
            HostCheck.Equal(clockSucceeds ? 1234UL : 0UL, boundary);
            HostCheck.Equal(1U, clock.Calls);
            HostCheck.True(clock.Refreshed && clock.Unwoken);
            HostCheck.Equal(1U, *ready); // A clock failure does not roll back real refresh effects.
            HostCheck.Equal(clockSucceeds ? KernelThreadTable.Completion.Woken : KernelThreadTable.Completion.None, m.Thread.Kernel.Inspect(m.Parent).Completion);
            if (clockSucceeds)
            {
                m.Thread.Yield(child);
                HostCheck.Equal(m.Parent.Value, m.Thread.Kernel.Dispatch().Value);
                HostCheck.Equal(1L, m.Complete().Value);
                HostCheck.Equal(77UL, m.Thread.Read64(Output + 4));
                HostCheck.Equal(1L, m.Value(0, pipe.Read, Data, 1));
            }

            m.Drain();
        }
    }

    [HostTest("timing readiness no epoll boundary accepts zero time and rejects foreign owner missing clock or failed refresh")]
    private static void MeasuredReadinessAdmission()
    {
        var poll = BindMeasuredReadiness();
        using Model m = new(epoll: false);
        RefreshClock clock = new()
        {
            Result = 1
        };
        HostCheck.True(!poll(ref *m.Thread.Service, Key + 1, 2, &clock, &ReadRefreshClock, out ulong boundary) && boundary == 0);
        HostCheck.True(!poll(ref *m.Thread.Service, Key, 0x202, &clock, &ReadRefreshClock, out boundary) && boundary == 0);
        HostCheck.True(!poll(ref *m.Thread.Service, Key, 2, null, &ReadRefreshClock, out boundary) && boundary == 0);
        HostCheck.True(!poll(ref *m.Thread.Service, Key, 2, &clock, null, out boundary) && boundary == 0);
        HostCheck.Equal(0U, clock.Calls);
        HostCheck.True(poll(ref *m.Thread.Service, Key, 2, &clock, &ReadRefreshClock, out boundary) && boundary == 0);
        HostCheck.Equal(1U, clock.Calls);
        HostCheck.True(clock.Refreshed && clock.Unwoken);
        ref ulong owner = ref DescriptorThreadOwner(ref *m.Files.State);
        ulong original = owner;
        try
        {
            owner ^= 2;
            HostCheck.True(!poll(ref *m.Thread.Service, Key, 2, &clock, &ReadRefreshClock, out boundary) && boundary == 0);
            HostCheck.Equal(1U, clock.Calls);
        }
        finally
        {
            owner = original;
        }

        m.Drain();
    }

    [HostTest("m16-012 epoll admission rejects dirty and aliased storage without publication", HostLane.Fast, 0)]
    private static void Admission()
    {
        using var files = new LinuxDescriptorServicesTests.Owner();
        files.Begin();
        Sockets.State sockets = default;
        Sockets.Endpoint* endpoints = stackalloc Sockets.Endpoint[2];
        new Span<byte>(endpoints, 2 * sizeof(Sockets.Endpoint)).Clear();
        HostCheck.True(Sockets.Begin(&sockets, files.State, endpoints, 2, new(10, 0, 2, 15), 8080));
        Epoll.State state = default;
        Epoll.Port* ports = stackalloc Epoll.Port[2];
        Epoll.Registration* registrations = stackalloc Epoll.Registration[2];
        new Span<byte>(ports, 2 * sizeof(Epoll.Port)).Clear();
        new Span<byte>(registrations, 2 * sizeof(Epoll.Registration)).Clear();
        HostCheck.True(!Epoll.Begin(&state, files.State, (Epoll.Port*)files.Admission.Scratch, 2, registrations, 2));
        HostCheck.True(!Epoll.Begin(&state, files.State, (Epoll.Port*)endpoints, 2, registrations, 2));
        ((byte*)registrations)[0] = 1;
        HostCheck.True(!Epoll.Begin(&state, files.State, ports, 2, registrations, 2));
        HostCheck.True(Fds.Audit(files.State));
        HostCheck.Equal(3U, files.State->ActiveDescriptors);
        HostCheck.True(Fds.End(files.State));
    }

    [HostTest("linux.epoll threaded refresh rejects changed owner mappings or port identity before readiness mutation")]
    private static void ThreadedRefreshAdmission()
    {
        for (int control = 0; control < 3; control++)
        {
            using Model m = new();
            uint epoll = m.Create();
            var pipe = m.Pipe();
            HostCheck.Equal(0L, m.Control(epoll, 1, pipe.Read, 1, 77));
            HostCheck.True(!m.Thread.Service->PollDescriptorReadiness(Key + 1, 2));
            HostCheck.True(m.Thread.Service->PollDescriptorReadiness(Key, 2));
            byte ready = 0x63;
            // Stage readiness without refreshing watches: an incorrectly early
            // refresh after the following corruption would now change the snapshot.
            HostCheck.Equal(1L, m.Files.Admission.Pipes[0].Write(&ready, 1, true, true));
            byte[] before = new ReadOnlySpan<byte>(m.Registrations, 8 * sizeof(Epoll.Registration)).ToArray();
            ref ulong changed = ref DescriptorThreadOwner(ref *m.Files.State);
            if (control == 1)
                changed = ref m.Files.Memory.Memory.Tables[1];
            else if (control == 2)
                changed = ref EpollSelf(ref *m.State);
            ulong original = changed;
            try
            {
                changed ^= 2;
                HostCheck.True(!m.Thread.Service->PollDescriptorReadiness(Key, 2));
                HostCheck.True(before.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(m.Registrations, before.Length)));
                HostCheck.Equal(1U, m.Files.Admission.Pipes[0].BufferedBytes);
            }
            finally
            {
                changed = original;
            }

            HostCheck.True(Fds.Audit(m.Files.State));
            HostCheck.True(Epoll.Refresh(m.State));
            HostCheck.True(!before.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(m.Registrations, before.Length)));
            m.Drain();
        }
    }

    [HostTest("m16-012 epoll validates ctl ABI and bounded capacity without consuming watches", HostLane.Fast, 1)]
    private static void CapacityAndErrors()
    {
        using Model m = new(ports: 1, registrations: 1);
        uint epoll = m.Create(), listener = m.Listen(), socket = m.Socket();
        HostCheck.Equal(1L, m.Value(72, epoll, 1));
        HostCheck.Equal(2L, m.Value(72, epoll, 3));
        HostCheck.Equal(-28L, m.Value(291, 0));
        HostCheck.Equal(-22L, m.Value(291, 1));
        HostCheck.Equal(-22L, m.Value(213, 0));
        HostCheck.Equal(-22L, m.Value(0, epoll, Output, 1));
        HostCheck.Equal(-1L, m.Control(epoll, 1, 1));
        "/app/fixture.data\0"u8.CopyTo(new Span<byte>(m.Thread.At(Data), 18));
        long immutable = m.Value(257, unchecked((uint)-100), Data);
        HostCheck.True(immutable >= 3);
        HostCheck.Equal(-1L, m.Control(epoll, 1, (uint)immutable, 1));
        HostCheck.Equal(-22L, m.Control(epoll, 1, epoll));
        HostCheck.Equal(-9L, m.Control(epoll, 1, 31));
        HostCheck.Equal(-22L, m.Control(epoll, 9, listener));
        HostCheck.Equal(-22L, m.Control(epoll, 1, listener, 0x40000000));
        HostCheck.Equal(0L, m.Control(epoll, 1, listener, 1 | Edge, 0xfedcba9876543210));
        HostCheck.Equal(-17L, m.Control(epoll, 1, listener, 1));
        HostCheck.Equal(-28L, m.Control(epoll, 1, socket, 1));
        HostCheck.Equal(-2L, m.Control(epoll, 3, socket, 1));
        HostCheck.Equal(-22L, m.Wait(epoll, 0));
        HostCheck.Equal(-22L, m.Wait(epoll, 1025));
        HostCheck.Equal(-14L, m.Value(232, epoll, 4096, 1, 0));
        m.Thread.FailRead(Event);
        HostCheck.Equal(-14L, m.Control(epoll, 3, listener, 4));
        m.Thread.FailRead(ulong.MaxValue);
        HostCheck.Equal(0L, m.Wait(epoll));
        HostCheck.Equal(0L, m.Value(233, epoll, 2, listener, 0));
        HostCheck.Equal(0L, m.Control(epoll, 1, socket, 1));
        m.Drain();
    }

    [HostTest("m16-012 actual TCP edges rearm after drain and copy failure preserves packed opaque data", HostLane.Fast, 2)]
    private static void TransportEdges()
    {
        using Model m = new();
        using var connection = Connection();
        uint epoll = m.Create(), listener = m.Listen();
        const ulong opaque = 0xfedcba9876543210;
        HostCheck.Equal(0L, m.Control(epoll, 1, listener, 1 | 4 | Edge, opaque));
        HostCheck.Equal(0L, m.Wait(epoll));
        m.Enqueue(listener, connection);
        m.Thread.FailWrite(Output);
        HostCheck.Equal(-14L, m.Wait(epoll));
        m.Thread.FailWrite(ulong.MaxValue);
        HostCheck.Equal(1L, m.Wait(epoll));
        HostCheck.Equal(1U, m.Thread.Read32(Output));
        HostCheck.Equal(opaque, m.Thread.Read64(Output + 4));
        HostCheck.Equal(0L, m.Wait(epoll));
        uint fd = m.Accept(listener);
        HostCheck.Equal(0L, m.Control(epoll, 1, fd, 1 | 4 | 0x2000 | Edge, 7));
        HostCheck.Equal(1L, m.Wait(epoll));
        HostCheck.Equal(4U, m.Thread.Read32(Output));
        HostCheck.Equal(0L, m.Wait(epoll));
        m.Thread.Put64(Data, 0x8877665544332211);
        HostCheck.Equal(8L, m.Value(44, fd, Data, 8));
        HostCheck.Equal(-11L, m.Value(44, fd, Data, 1));
        HostCheck.Equal(0L, m.Wait(epoll));
        HostCheck.Equal(Tx.Status.Success, Tx.Pump(connection.Owner, new byte[1514], out _));
        HostCheck.Equal(Tx.Status.Success, connection.Ack(704, out _));
        HostCheck.True(m.Thread.Service->PollDescriptorReadiness(Key, 2));
        HostCheck.Equal(1L, m.Wait(epoll));
        HostCheck.Equal(4U, m.Thread.Read32(Output));
        HostCheck.Equal(Tx.Status.Success, Tx.ReceiveFrame(connection.Owner, Wire.Packet(100, "abc"u8, ack: 704, flags: 0x18), out _));
        HostCheck.True(m.Thread.Service->PollDescriptorReadiness(Key, 2));
        HostCheck.Equal(1L, m.Wait(epoll));
        HostCheck.Equal(1U, m.Thread.Read32(Output));
        HostCheck.Equal(3L, m.Value(45, fd, Data, 8));
        HostCheck.Equal(-11L, m.Value(45, fd, Data, 1));
        HostCheck.Equal(0L, m.Wait(epoll));
        HostCheck.Equal(Tx.Status.PeerClosed, Tx.ReceiveFrame(connection.Owner, Wire.Packet(103, ReadOnlySpan<byte>.Empty, ack: 704, flags: 0x11), out _));
        HostCheck.True(m.Thread.Service->PollDescriptorReadiness(Key, 2));
        HostCheck.Equal(1L, m.Wait(epoll));
        HostCheck.Equal(1U | 0x2000, m.Thread.Read32(Output));
        HostCheck.Equal(0L, m.Value(45, fd, Data, 1));
        m.CloseAndReap(fd, connection);
        m.Drain();
    }

    [HostTest("m16-012 level watches and MOD rearm while dup close and fd reuse preserve original identities", HostLane.Fast, 3)]
    private static void DupAndReuse()
    {
        using Model m = new();
        using var connection = Connection();
        uint listener = m.Listen();
        m.Enqueue(listener, connection);
        uint fd = m.Accept(listener), epoll = m.Create();
        HostCheck.Equal(0L, m.Control(epoll, 1, fd, 4, 123));
        HostCheck.Equal(1L, m.Wait(epoll));
        HostCheck.Equal(1L, m.Wait(epoll));
        HostCheck.Equal(0L, m.Control(epoll, 3, fd, 4 | Edge, 456));
        HostCheck.Equal(1L, m.Wait(epoll));
        HostCheck.Equal(456UL, m.Thread.Read64(Output + 4));
        HostCheck.Equal(0L, m.Wait(epoll));
        uint duplicate = (uint)m.Value(72, fd, 0);
        HostCheck.Equal(0L, m.Control(epoll, 1, duplicate, 4, 789));
        HostCheck.Equal(0L, m.Value(3, fd));
        HostCheck.Equal(fd, m.Socket());
        HostCheck.Equal(-2L, m.Control(epoll, 2, fd));
        HostCheck.Equal(0L, m.Control(epoll, 1, fd, 1, 999));
        HostCheck.Equal(1L, m.Wait(epoll));
        HostCheck.Equal(789UL, m.Thread.Read64(Output + 4));
        m.CloseAndReap(duplicate, connection);
        HostCheck.Equal(0L, m.Wait(epoll));
        HostCheck.True(Fds.Audit(m.Files.State));
        m.Drain();
    }

    [HostTest("m16-012 genuine infinite wait survives epoll fd close and reuse then wakes original port", HostLane.Fast, 4)]
    private static void ParkCloseReuse()
    {
        using Model m = new();
        using var connection = Connection();
        var child = m.Thread.Clone();
        uint epoll = m.Create(), listener = m.Listen();
        HostCheck.Equal(0L, m.Control(epoll, 1, listener, 1 | Edge, 77));
        HostCheck.Equal(LinuxSyscallAction.Blocked, m.Call(281, epoll, Output, 1024, ulong.MaxValue).Action);
        HostCheck.Equal(1U, m.Files.State->RetainedPipeOperations);
        HostCheck.Equal(3U, m.Thread.View(m.Parent).WaitSource);
        m.Park(m.Parent);
        HostCheck.Equal(child.Value, m.Thread.Kernel.Dispatch().Value);
        HostCheck.Equal(0L, m.Value(3, epoll));
        HostCheck.Equal(epoll, m.Create());
        HostCheck.Equal(0L, m.Wait(epoll));
        m.Enqueue(listener, connection);
        HostCheck.Equal(KernelThreadTable.State.Ready, m.Thread.Kernel.Inspect(m.Parent).State);
        m.Thread.Yield(child);
        HostCheck.Equal(m.Parent.Value, m.Thread.Kernel.Dispatch().Value);
        var completed = m.Complete();
        HostCheck.Equal(LinuxSyscallAction.Return, completed.Action);
        HostCheck.Equal(1L, completed.Value);
        HostCheck.Equal(77UL, m.Thread.Read64(Output + 4));
        HostCheck.Equal(0U, m.Files.State->RetainedPipeOperations);
        HostCheck.Equal(0L, m.Wait(epoll));
        HostCheck.Equal(LinuxSyscallAction.Rejected, m.Complete().Action);
        uint fd = m.Accept(listener);
        m.CloseAndReap(fd, connection);
        m.Drain();
    }

    [HostTest("m16-012 real-clock timeout and interrupted completion release epoll borrows without restart", HostLane.Fast, 5)]
    private static void TimeoutAndInterrupt()
    {
        using Model m = new();
        var child = m.Thread.Clone();
        uint epoll = m.Create();
        HostCheck.Equal(LinuxSyscallAction.Blocked, m.Call(232, epoll, Output, 1, 5).Action);
        HostCheck.Equal(5_001_000UL, m.Thread.View(m.Parent).WaitDeadline);
        m.Park(m.Parent);
        HostCheck.Equal(child.Value, m.Thread.Kernel.Dispatch().Value);
        m.Thread.Now = 5_001_000;
        HostCheck.True(m.Thread.Service->AdvanceFutexClock(Key, 2));
        m.Thread.Yield(child);
        m.Thread.Kernel.Dispatch();
        HostCheck.Equal(0L, m.Complete().Value);
        HostCheck.True(!m.Thread.View(m.Parent).Wait.IsValid);
        HostCheck.Equal(LinuxSyscallAction.Blocked, m.Call(281, epoll, Output, 1, ulong.MaxValue).Action);
        m.Park(m.Parent);
        HostCheck.Equal(child.Value, m.Thread.Kernel.Dispatch().Value);
        HostCheck.True(m.Thread.Kernel.Table->Interrupt(KernelThreadTableTests.Key, 2, m.Thread.View(m.Parent).Wait).Succeeded);
        m.Thread.Yield(child);
        m.Thread.Kernel.Dispatch();
        HostCheck.Equal(-4L, m.Complete().Value);
        HostCheck.True(m.Thread.Service->CurrentImage(Key, 2, out _));
        HostCheck.Equal(0U, m.Files.State->RetainedPipeOperations);
        m.Drain();
    }

    [HostTest("m16-012 empty epoll shutdown retains Parking borrow until captured cancellation and exit", HostLane.Fast, 6)]
    private static void CancelParking()
    {
        using Model m = new();
        uint epoll = m.Create();
        HostCheck.Equal(LinuxSyscallAction.Blocked, m.Call(281, epoll, Output, 1, unchecked((ulong)-2L)).Action);
        HostCheck.True(m.Thread.Service->Shutdown(Key, 2, 9));
        HostCheck.Equal(1U, m.Files.State->RetainedPipeOperations);
        HostCheck.True(!m.Thread.Service->CommitExit(Key, 2, m.Thread.Kernel.Capture(m.Parent), out _));
        HostCheck.True(!m.Thread.Service->Release(Key, 2));
        HostCheck.True(!Fds.End(m.Files.State));
        m.Drain();
    }

    [HostTest("m16-012 immediate timeout expiry retains its actual deadline without staging a wait", HostLane.Fast, 12)]
    private static void ImmediateExpiry()
    {
        using Model m = new();
        uint epoll = m.Create();
        m.Thread.ClockStep = 1_000_000;
        var result = m.Call(232, epoll, Output, 1, 1);
        HostCheck.Equal(LinuxSyscallAction.Return, result.Action);
        HostCheck.Equal(0L, result.Value);
        HostCheck.True(!m.Thread.View(m.Parent).Wait.IsValid);
        HostCheck.True(m.Thread.Service->InspectEpollExpiry(Key, 2, out ulong deadline));
        HostCheck.Equal(1_001_000UL, deadline);
        HostCheck.Equal(0U, m.Thread.View(m.Parent).WaitSource);
        HostCheck.Equal(0L, m.Call(232, epoll, Output, 1, 0).Value);
        HostCheck.True(!m.Thread.Service->InspectEpollExpiry(Key, 2, out _));
        m.Drain();
    }

    [HostTest("m16-012 competing waiters retry stolen readiness with a new real wait token", HostLane.Fast, 7)]
    private static void CompetingWaiters()
    {
        using Model m = new();
        using var connection = Connection();
        var child = m.Thread.Clone();
        uint epoll = m.Create(), listener = m.Listen();
        HostCheck.Equal(0L, m.Control(epoll, 1, listener, 1 | Edge, 33));
        HostCheck.Equal(LinuxSyscallAction.Blocked, m.Call(281, epoll, Output, 1, ulong.MaxValue).Action);
        m.Park(m.Parent);
        m.Thread.Kernel.Dispatch();
        HostCheck.Equal(LinuxSyscallAction.Blocked, m.Call(281, epoll, Output, 1, ulong.MaxValue).Action);
        var first = m.Thread.View(child).Wait;
        m.Park(child);
        m.Enqueue(listener, connection);
        HostCheck.Equal(m.Parent.Value, m.Thread.Kernel.Dispatch().Value);
        HostCheck.Equal(1L, m.Complete().Value);
        uint fd = m.Accept(listener);
        m.Thread.Yield(m.Parent);
        HostCheck.Equal(child.Value, m.Thread.Kernel.Dispatch().Value);
        HostCheck.Equal(LinuxSyscallAction.Blocked, m.Complete().Action);
        HostCheck.True(m.Thread.View(child).Wait.Sequence > first.Sequence);
        HostCheck.Equal(1U, m.Files.State->RetainedPipeOperations);
        m.Park(child);
        HostCheck.Equal(m.Parent.Value, m.Thread.Kernel.Dispatch().Value);
        m.CloseAndReap(fd, connection);
        m.Drain();
    }

    [HostTest("linux.epoll retained readiness scan validates later operations without undoing earlier wakes")]
    private static void RetainedReadinessScanAdmission()
    {
        foreach (bool firstReady in new[]
        {
            false,
            true
        }

        )
            for (int damage = 0; damage < 2; damage++)
            {
                using Model m = new();
                var child = m.Thread.Clone();
                var actor = m.Thread.Clone();
                var firstPipe = m.Pipe();
                var secondPipe = m.Pipe();
                uint firstPort = m.Create(), secondPort = m.Create();
                HostCheck.Equal(0L, m.Control(firstPort, 1, firstPipe.Read, 1, 11));
                HostCheck.Equal(0L, m.Control(secondPort, 1, secondPipe.Read, 1, 22));
                HostCheck.Equal(LinuxSyscallAction.Blocked, m.Call(232, firstPort, Output, 1, ulong.MaxValue).Action);
                var firstWait = m.Thread.View(m.Parent).Wait;
                m.Park(m.Parent);
                HostCheck.Equal(child.Value, m.Thread.Kernel.Dispatch().Value);
                HostCheck.Equal(LinuxSyscallAction.Blocked, m.Call(232, secondPort, Output + 32, 1, ulong.MaxValue).Action);
                var secondWait = m.Thread.View(child).Wait;
                m.Park(child);
                HostCheck.Equal(actor.Value, m.Thread.Kernel.Dispatch().Value);
                HostCheck.Equal(2U, m.Files.State->RetainedPipeOperations);
                // Stage real readiness without polling, so the first wake, if any,
                // must occur in the same scan that rejects the second operation.
                byte ready = 0x63;
                if (firstReady)
                    HostCheck.Equal(1L, m.Files.Admission.Pipes[0].Write(&ready, 1, true, true));
                HostCheck.Equal(1L, m.Files.Admission.Pipes[1].Write(&ready, 1, true, true));
                HostCheck.Equal(KernelThreadTable.Completion.None, m.Thread.Kernel.Inspect(m.Parent).Completion);
                HostCheck.Equal(KernelThreadTable.Completion.None, m.Thread.Kernel.Inspect(child).Completion);
                uint childSlot = unchecked((uint)child.Value) - 1;
                byte* operation = (byte*)(m.Thread.Slots + childSlot) + Marshal.OffsetOf<LinuxThreadServices.Slot>("EpollOperation");
                ulong* changed = (ulong*)(operation + Marshal.OffsetOf<Epoll.Operation>(damage == 0 ? "Self" : "Owner"));
                ulong original = *changed;
                *changed = damage == 0 ? original ^ 8UL : 0;
                try
                {
                    HostCheck.True(!m.Thread.Service->PollDescriptorReadiness(Key, 2));
                    HostCheck.True(m.Thread.Service->Failed);
                    var first = m.Thread.Kernel.Inspect(m.Parent);
                    var second = m.Thread.Kernel.Inspect(child);
                    HostCheck.Equal(firstReady ? KernelThreadTable.Completion.Woken : KernelThreadTable.Completion.None, first.Completion);
                    HostCheck.Equal(firstReady ? KernelThreadTable.State.Ready : KernelThreadTable.State.Waiting, first.State);
                    HostCheck.Equal(firstWait.Sequence, first.Wait.Sequence);
                    HostCheck.Equal(KernelThreadTable.Completion.None, second.Completion);
                    HostCheck.Equal(KernelThreadTable.State.Waiting, second.State);
                    HostCheck.Equal(secondWait.Sequence, second.Wait.Sequence);
                    HostCheck.Equal(2U, m.Files.State->RetainedPipeOperations);
                    HostCheck.Equal(firstReady ? 1U : 0U, m.Files.Admission.Pipes[0].BufferedBytes);
                    HostCheck.Equal(1U, m.Files.Admission.Pipes[1].BufferedBytes);
                }
                finally
                {
                    *changed = original;
                }

                HostCheck.True(Fds.Audit(m.Files.State));
                m.Drain();
            }
    }

    [HostTest("linux.epoll fresh readiness scan rejects a null first retained owner without waking")]
    private static void FreshReadinessScanNullOwner()
    {
        using Model m = new();
        var actor = m.Thread.Clone();
        var pipe = m.Pipe();
        uint epoll = m.Create();
        HostCheck.Equal(0L, m.Control(epoll, 1, pipe.Read, 1, 33));
        HostCheck.Equal(LinuxSyscallAction.Blocked, m.Call(232, epoll, Output, 1, ulong.MaxValue).Action);
        var wait = m.Thread.View(m.Parent).Wait;
        m.Park(m.Parent);
        HostCheck.Equal(actor.Value, m.Thread.Kernel.Dispatch().Value);
        byte ready = 0x63;
        HostCheck.Equal(1L, m.Files.Admission.Pipes[0].Write(&ready, 1, true, true));
        HostCheck.Equal(KernelThreadTable.Completion.None, m.Thread.Kernel.Inspect(m.Parent).Completion);
        // This is the first active operation seen by a fresh default scan.
        // Its null owner must never compare equal to an admitted null cache.
        uint parentSlot = unchecked((uint)m.Parent.Value) - 1;
        byte* operation = (byte*)(m.Thread.Slots + parentSlot) + Marshal.OffsetOf<LinuxThreadServices.Slot>("EpollOperation");
        ulong* owner = (ulong*)(operation + Marshal.OffsetOf<Epoll.Operation>("Owner"));
        ulong original = *owner;
        HostCheck.True(original != 0);
        *owner = 0;
        try
        {
            HostCheck.True(!m.Thread.Service->PollDescriptorReadiness(Key, 2));
            HostCheck.True(m.Thread.Service->Failed);
            var state = m.Thread.Kernel.Inspect(m.Parent);
            HostCheck.Equal(KernelThreadTable.Completion.None, state.Completion);
            HostCheck.Equal(KernelThreadTable.State.Waiting, state.State);
            HostCheck.Equal(wait.Sequence, state.Wait.Sequence);
            HostCheck.Equal(1U, m.Files.State->RetainedPipeOperations);
            HostCheck.Equal(1U, m.Files.Admission.Pipes[0].BufferedBytes);
        }
        finally
        {
            *owner = original;
        }

        HostCheck.True(Fds.Audit(m.Files.State));
        m.Drain();
    }

    [HostTest("linux.epoll mixed pipe waits preserve unchanged readers and earlier wakes on later rejection")]
    private static void MixedPipeThreadRefresh()
    {
        foreach (bool writeFirst in new[]
        {
            false,
            true
        }

        )
            for (int damage = 0; damage < 3; damage++)
            {
                using Model m = new();
                var child = m.Thread.Clone();
                var actor = m.Thread.Clone();
                var firstPipe = m.Pipe(0);
                var secondPipe = m.Pipe();
                uint epoll = m.Create();
                HostCheck.Equal(0L, m.Control(epoll, 1, secondPipe.Read, 1, 44));
                HostCheck.Equal(LinuxSyscallAction.Blocked, m.Call(0, firstPipe.Read, Output, 1).Action);
                var firstView = m.Thread.View(m.Parent);
                var firstWait = firstView.Wait;
                HostCheck.True(firstView.PipePending && firstView.WaitSource == 2);
                HostCheck.True(!m.Thread.Service->CurrentImage(Key, 2, out _));
                HostCheck.True(m.Thread.Service->CommitPipeWait(Key, 2, m.Thread.Kernel.Capture(m.Parent), out bool handoff) && handoff);
                m.Thread.Kernel.Confirm();
                HostCheck.Equal(KernelThreadTable.State.Waiting, m.Thread.Kernel.Inspect(m.Parent).State);
                HostCheck.Equal(child.Value, m.Thread.Kernel.Dispatch().Value);
                HostCheck.Equal(LinuxSyscallAction.Blocked, m.Call(232, epoll, Output + 32, 1, ulong.MaxValue).Action);
                var secondView = m.Thread.View(child);
                var secondWait = secondView.Wait;
                HostCheck.Equal(3U, secondView.WaitSource);
                m.Park(child);
                HostCheck.Equal(actor.Value, m.Thread.Kernel.Dispatch().Value);
                HostCheck.Equal(2U, m.Files.State->RetainedPipeOperations);
                uint childSlot = unchecked((uint)child.Value) - 1;
                byte* operation = (byte*)(m.Thread.Slots + childSlot) + Marshal.OffsetOf<LinuxThreadServices.Slot>("EpollOperation");
                ulong* changed = (ulong*)(operation + Marshal.OffsetOf<Epoll.Operation>(damage == 1 ? "Self" : "Owner"));
                ulong original = *changed;
                if (damage != 0)
                    *changed = damage == 1 ? original ^ 8UL : 0;
                try
                {
                    // Writing B leaves the source2 reader unchanged. Writing A must
                    // retain its wake even if the subsequent source3 check rejects.
                    m.Thread.Put32(Data, 0x63);
                    var result = m.Call(1, writeFirst ? firstPipe.Write : secondPipe.Write, Data, 1);
                    HostCheck.Equal(damage == 0 ? LinuxSyscallAction.Return : LinuxSyscallAction.Rejected, result.Action);
                    HostCheck.Equal(damage != 0, m.Thread.Service->Failed);
                    if (damage == 0)
                        HostCheck.Equal(1L, result.Value);
                    var first = m.Thread.Kernel.Inspect(m.Parent);
                    var second = m.Thread.Kernel.Inspect(child);
                    bool secondReady = !writeFirst && damage == 0;
                    HostCheck.Equal(writeFirst ? KernelThreadTable.State.Ready : KernelThreadTable.State.Waiting, first.State);
                    HostCheck.Equal(writeFirst ? KernelThreadTable.Completion.Woken : KernelThreadTable.Completion.None, first.Completion);
                    HostCheck.Equal(firstWait.Sequence, first.Wait.Sequence);
                    HostCheck.Equal(firstWait.Thread.Value, first.Wait.Thread.Value);
                    HostCheck.Equal(secondReady ? KernelThreadTable.State.Ready : KernelThreadTable.State.Waiting, second.State);
                    HostCheck.Equal(secondReady ? KernelThreadTable.Completion.Woken : KernelThreadTable.Completion.None, second.Completion);
                    HostCheck.Equal(secondWait.Sequence, second.Wait.Sequence);
                    HostCheck.Equal(secondWait.Thread.Value, second.Wait.Thread.Value);
                    HostCheck.Equal(2U, m.Files.State->RetainedPipeOperations);
                    HostCheck.Equal(writeFirst ? 1U : 0U, m.Files.Admission.Pipes[0].BufferedBytes);
                    HostCheck.Equal(writeFirst ? 0U : 1U, m.Files.Admission.Pipes[1].BufferedBytes);
                    for (int pipe = 0; pipe < 2; pipe++)
                    {
                        HostCheck.Equal(1U, m.Files.Admission.Pipes[pipe].ReaderCount);
                        HostCheck.Equal(1U, m.Files.Admission.Pipes[pipe].WriterCount);
                    }

                    byte buffered = 0;
                    HostCheck.Equal(1L, m.Files.Admission.Pipes[writeFirst ? 0 : 1].Peek(&buffered, 1, true));
                    HostCheck.Equal((byte)0x63, buffered);
                }
                finally
                {
                    *changed = original;
                }

                HostCheck.True(Fds.Audit(m.Files.State));
                m.Drain();
            }
    }

    [HostTest("m16-012 checked syscall gateway retains and completes an actual epoll block", HostLane.Fast, 8)]
    private static void Gateway()
    {
        using Model m = new();
        uint epoll = m.Create();
        byte* scratch = stackalloc byte[64];
        LinuxSyscallGateState gate = default;
        HostCheck.Equal(LinuxSyscallGateStatus.Active, LinuxSyscallGateway.Begin(ref gate, new() { UserRoot = m.Thread.Kernel.Owner.Root, MaximumCalls = 64, MaximumWrite = 64, Scratch = scratch, ScratchBytes = 64, FirmwareExited = true, OwnedBoundaryValidated = true, EnableStartup = true, EnableDescriptors = true, EnableThreads = true }, m.Thread.GatewayAccess));
        var image = m.Thread.CurrentImage();
        var frame = image->Frame;
        frame.Number = 281;
        frame.Argument0 = epoll;
        frame.Argument1 = Output;
        frame.Argument2 = 1;
        frame.Argument3 = 1;
        image->Frame = frame;
        var blocked = LinuxSyscallGateway.Dispatch(ref gate, ref frame);
        image->Frame = frame;
        HostCheck.Equal(LinuxSyscallAction.Blocked, blocked.Action);
        HostCheck.Equal(LinuxSyscallGateStatus.Blocked, gate.Status);
        m.Park(m.Parent);
        m.Thread.Now = 1_001_000;
        HostCheck.True(m.Thread.Service->AdvanceFutexClock(Key, 2));
        m.Thread.Kernel.Dispatch();
        var completed = LinuxSyscallGateway.CompleteBlocked(ref gate, m.Complete());
        HostCheck.Equal(LinuxSyscallAction.Return, completed.Action);
        HostCheck.Equal(0L, completed.Value);
        HostCheck.Equal(LinuxSyscallGateStatus.Active, gate.Status);
        HostCheck.Equal(1U, gate.Calls);
        HostCheck.Equal(0U, gate.BytesWritten);
        m.Drain();
    }

    [HostTest("m16-012 older EFI profiles exclude epoll storage and ownership references", HostLane.Fast, 9)]
    private static void FeatureBoundary()
    {
        string directory = Path.Combine(HostToolProcess.FindRepositoryRoot(), "src/OS.Kernel/Compatibility/LinuxX64");
        foreach (string file in new[]
        {
            "LinuxDescriptorServices.cs",
            "LinuxThreadServices.cs",
            "LinuxThreadServices.Pipes.cs",
            "LinuxEpollServices.cs",
            "LinuxThreadServices.Epoll.cs"
        }

        )
        {
            string source = File.ReadAllText(Path.Combine(directory, file));
            var off = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(preprocessorSymbols: ["NOSTDLIB_EFI"])).GetCompilationUnitRoot();
            var on = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(preprocessorSymbols: ["NOSTDLIB_EFI", "CLROS_LINUX_SOCKETS"])).GetCompilationUnitRoot();
            HostCheck.True(!off.DescendantNodes().OfType<IdentifierNameSyntax>().Any(node => node.Identifier.ValueText == "LinuxEpollServices"));
            HostCheck.True(on.Members.Count > 0);
            if (file is "LinuxEpollServices.cs" or "LinuxThreadServices.Epoll.cs")
                HostCheck.Equal(0, off.Members.Count);
        }
    }

    [HostTest("m16-012 graceful close retains FIN readiness without ERR and consumed reset clears ERR", HostLane.Fast, 10)]
    private static void ClosedReadiness()
    {
        using (Model m = new())
        using (var connection = Connection())
        {
            uint listener = m.Listen();
            m.Enqueue(listener, connection);
            uint fd = m.Accept(listener), epoll = m.Create();
            HostCheck.Equal(0L, m.Control(epoll, 1, fd, 1 | 4 | 0x2000, 9));
            HostCheck.Equal(1L, m.Wait(epoll));
            HostCheck.Equal(Tx.Status.PeerClosed, Tx.ReceiveFrame(connection.Owner, Wire.Packet(100, "end"u8, ack: 700, flags: 0x19), out _));
            HostCheck.True(m.Thread.Service->PollDescriptorReadiness(Key, 2));
            HostCheck.Equal(1L, m.Wait(epoll));
            HostCheck.Equal(1U | 4 | 0x2000, m.Thread.Read32(Output));
            HostCheck.Equal(0L, m.Value(48, fd, 1));
            HostCheck.Equal(Tx.Status.Success, Tx.PumpClose(connection.Owner, new byte[1514], out _));
            HostCheck.Equal(Tx.Status.ConnectionClosed, Tx.ReceiveFrame(connection.Owner, Wire.Packet(104, ReadOnlySpan<byte>.Empty, ack: 701, flags: 0x10), out _));
            HostCheck.True(m.Thread.Service->PollDescriptorReadiness(Key, 2));
            HostCheck.Equal(1L, m.Wait(epoll));
            HostCheck.Equal(1U | 4 | 16 | 0x2000, m.Thread.Read32(Output));
            HostCheck.Equal(3L, m.Value(45, fd, Data, 8));
            HostCheck.Equal(0L, m.Value(45, fd, Data, 8));
            HostCheck.Equal(0L, m.Value(3, fd));
            HostCheck.True(Sockets.ReapClosed(m.SocketState, connection.Owner));
            m.Drain();
        }

        using (Model m = new())
        using (var connection = Connection())
        {
            uint listener = m.Listen();
            m.Enqueue(listener, connection);
            uint fd = m.Accept(listener), epoll = m.Create();
            HostCheck.Equal(0L, m.Control(epoll, 1, fd, 0, 7));
            HostCheck.Equal(Tx.Status.ConnectionReset, Tx.ReceiveFrame(connection.Owner, Wire.Packet(100, ReadOnlySpan<byte>.Empty, ack: 700, flags: 0x14), out _));
            HostCheck.True(m.Thread.Service->PollDescriptorReadiness(Key, 2));
            HostCheck.Equal(1L, m.Wait(epoll));
            HostCheck.Equal(8U | 16, m.Thread.Read32(Output));
            HostCheck.Equal(-104L, m.Value(45, fd, Data, 1));
            HostCheck.Equal(1L, m.Wait(epoll));
            HostCheck.Equal(16U, m.Thread.Read32(Output));
            HostCheck.Equal(0L, m.Value(3, fd));
            HostCheck.True(Sockets.ReapClosed(m.SocketState, connection.Owner));
            m.Drain();
        }
    }

    [HostTest("m16-020 anonymous pipe level and edge watches rearm read and write readiness after full drain")]
    private static void PipeReadiness()
    {
        foreach (bool edge in new[]
        {
            false,
            true
        }

        )
        {
            using Model m = new();
            var pipe = m.Pipe();
            uint epoll = m.Create(), mode = edge ? Edge : 0;
            HostCheck.Equal(0L, m.Control(epoll, 1, pipe.Read, 1 | mode, 11));
            HostCheck.Equal(0L, m.Control(epoll, 1, pipe.Write, 4 | mode, 22));
            HostCheck.Equal(1L, m.Wait(epoll));
            HostCheck.Equal(4U, m.Thread.Read32(Output));
            HostCheck.Equal(22UL, m.Thread.Read64(Output + 4));
            HostCheck.Equal(edge ? 0L : 1L, m.Wait(epoll));
            for (int cycle = 0; cycle < 2; cycle++)
            {
                new Span<byte>(m.Thread.At(Data), 4096).Fill(0x6b);
                HostCheck.Equal(4096L, m.Value(1, pipe.Write, Data, 4096));
                HostCheck.Equal(-11L, m.Value(1, pipe.Write, Data, 1));
                HostCheck.Equal(1L, m.Wait(epoll));
                HostCheck.Equal(1U, m.Thread.Read32(Output));
                HostCheck.Equal(11UL, m.Thread.Read64(Output + 4));
                HostCheck.Equal(edge ? 0L : 1L, m.Wait(epoll));
                HostCheck.Equal(4096L, m.Value(0, pipe.Read, Data, 4096));
                HostCheck.Equal(0x6b6b6b6bU, m.Thread.Read32(Data));
                HostCheck.Equal(-11L, m.Value(0, pipe.Read, Data, 1));
                HostCheck.Equal(1L, m.Wait(epoll));
                HostCheck.Equal(4U, m.Thread.Read32(Output));
                HostCheck.Equal(22UL, m.Thread.Read64(Output + 4));
                HostCheck.Equal(edge ? 0L : 1L, m.Wait(epoll));
            }

            HostCheck.True(Fds.Audit(m.Files.State) && Epoll.Audit(m.State, m.Files.State));
            m.Drain();
        }
    }

    [HostTest("m16-020 anonymous pipe opposite-end close exposes buffered IN HUP and unconditional ERR")]
    private static void PipePeerClose()
    {
        using (Model m = new())
        {
            var pipe = m.Pipe();
            uint epoll = m.Create();
            HostCheck.Equal(0L, m.Control(epoll, 1, pipe.Read, 1, 31));
            m.Thread.Put32(Data, 0x61);
            HostCheck.Equal(1L, m.Value(1, pipe.Write, Data, 1));
            HostCheck.Equal(0L, m.Value(3, pipe.Write));
            HostCheck.Equal(1L, m.Wait(epoll));
            HostCheck.Equal(1U | 16, m.Thread.Read32(Output));
            HostCheck.Equal(31UL, m.Thread.Read64(Output + 4));
            HostCheck.Equal(1L, m.Value(0, pipe.Read, Data, 1));
            HostCheck.Equal(1L, m.Wait(epoll));
            HostCheck.Equal(16U, m.Thread.Read32(Output));
            HostCheck.Equal(0L, m.Value(0, pipe.Read, Data, 1));
            HostCheck.Equal(0L, m.Control(epoll, 3, pipe.Read, 0, 32));
            HostCheck.Equal(1L, m.Wait(epoll));
            HostCheck.Equal(16U, m.Thread.Read32(Output));
            m.Drain();
        }

        using (Model m = new())
        {
            var pipe = m.Pipe();
            uint epoll = m.Create();
            HostCheck.Equal(0L, m.Control(epoll, 1, pipe.Write, 4, 41));
            HostCheck.Equal(0L, m.Value(3, pipe.Read));
            HostCheck.Equal(1L, m.Wait(epoll));
            HostCheck.Equal(4U | 8, m.Thread.Read32(Output));
            HostCheck.Equal(-32L, m.Value(1, pipe.Write, Data, 1));
            HostCheck.Equal(0L, m.Control(epoll, 3, pipe.Write, 0, 42));
            HostCheck.Equal(1L, m.Wait(epoll));
            HostCheck.Equal(8U, m.Thread.Read32(Output));
            m.Drain();
        }
    }

    [HostTest("m16-020 anonymous pipe duplicate watches survive fd reuse and last description close scrubs ownership")]
    private static void PipeDupAndReuse()
    {
        using Model m = new();
        var pipe = m.Pipe();
        uint epoll = m.Create();
        HostCheck.Equal(0L, m.Control(epoll, 1, pipe.Read, 1, 101));
        uint duplicate = (uint)m.Value(72, pipe.Read, 0);
        HostCheck.Equal(0L, m.Control(epoll, 1, duplicate, 1, 202));
        HostCheck.Equal(0L, m.Value(3, pipe.Read));
        var replacement = m.Pipe();
        HostCheck.Equal(pipe.Read, replacement.Read);
        HostCheck.Equal(-2L, m.Control(epoll, 2, replacement.Read));
        HostCheck.Equal(0L, m.Control(epoll, 1, replacement.Read, 1, 303));
        m.Thread.Put32(Data, 0x62);
        HostCheck.Equal(1L, m.Value(1, pipe.Write, Data, 1));
        HostCheck.Equal(2L, m.Wait(epoll));
        HostCheck.Equal(1U, m.Thread.Read32(Output));
        HostCheck.Equal(1U, m.Thread.Read32(Output + 12));
        HostCheck.True(new[] { m.Thread.Read64(Output + 4), m.Thread.Read64(Output + 16) }.Order().SequenceEqual(new ulong[] { 101, 202 }));
        HostCheck.Equal(0L, m.Value(3, duplicate));
        HostCheck.Equal(0L, m.Wait(epoll));
        HostCheck.True(Fds.Audit(m.Files.State) && Epoll.Audit(m.State, m.Files.State));
        HostCheck.Equal(1L, m.Value(1, replacement.Write, Data, 1));
        HostCheck.Equal(1L, m.Wait(epoll));
        HostCheck.Equal(303UL, m.Thread.Read64(Output + 4));
        m.Drain();
        HostCheck.True(new ReadOnlySpan<byte>(m.Files.Admission.PipeBuffers, 8192).IndexOfAnyExcept((byte)0) < 0);
        using Model borrowed = new();
        var child = borrowed.Thread.Clone();
        var pending = borrowed.Pipe(0);
        uint port = borrowed.Create();
        HostCheck.Equal(0L, borrowed.Control(port, 1, pending.Read, 1, 404));
        HostCheck.Equal(LinuxSyscallAction.Blocked, borrowed.Call(0, pending.Read, Output + 64, 1).Action);
        HostCheck.Equal(2U, borrowed.Thread.View(borrowed.Parent).WaitSource);
        HostCheck.True(borrowed.Thread.Service->CommitPipeWait(Key, 2, borrowed.Thread.Kernel.Capture(borrowed.Parent), out bool handoff) && handoff);
        borrowed.Thread.Kernel.Confirm();
        HostCheck.Equal(child.Value, borrowed.Thread.Kernel.Dispatch().Value);
        HostCheck.Equal(0L, borrowed.Value(3, pending.Read));
        HostCheck.Equal(pending.Read, borrowed.Socket());
        HostCheck.True(Fds.Audit(borrowed.Files.State) && Epoll.Audit(borrowed.State, borrowed.Files.State));
        HostCheck.Equal(1U, borrowed.Files.State->RetainedPipeOperations);
        HostCheck.Equal(-2L, borrowed.Control(port, 2, pending.Read));
        borrowed.Thread.Put32(Data, 0x64);
        HostCheck.Equal(1L, borrowed.Value(1, pending.Write, Data, 1));
        HostCheck.Equal(1L, borrowed.Wait(port));
        HostCheck.Equal(404UL, borrowed.Thread.Read64(Output + 4));
        borrowed.Thread.Yield(child);
        HostCheck.Equal(borrowed.Parent.Value, borrowed.Thread.Kernel.Dispatch().Value);
        var completed = borrowed.Thread.Service->CompletePipeWait(Key, 2);
        HostCheck.Equal(LinuxSyscallAction.Return, completed.Action);
        HostCheck.Equal(1L, completed.Value);
        HostCheck.Equal((byte)0x64, *borrowed.Thread.At(Output + 64));
        HostCheck.Equal(0U, borrowed.Files.State->RetainedPipeOperations);
        HostCheck.Equal(0L, borrowed.Wait(port));
        HostCheck.True(Fds.Audit(borrowed.Files.State) && Epoll.Audit(borrowed.State, borrowed.Files.State));
        borrowed.Drain();
    }

    [HostTest("m16-020 anonymous pipe stolen epoll readiness reparks before pending signal EINTR and fresh ready completion")]
    private static void PipeStolenReadinessSignal()
    {
        using Model m = new(signals: true);
        var child = m.Thread.Clone();
        var pipe = m.Pipe();
        uint epoll = m.Create();
        *(Signals.Disposition*)m.Thread.At(Data) = new()
        {
            Handler = User + 0x180,
            Flags = 0x14000004,
            Restorer = User + 0x200
        };
        HostCheck.Equal(0L, m.Thread.Call(13, 10, Data, 0, 8).Value);
        HostCheck.Equal(0L, m.Control(epoll, 1, pipe.Read, 1, 42));
        HostCheck.Equal(LinuxSyscallAction.Blocked, m.Call(232, epoll, Output, 1, ulong.MaxValue).Action);
        var first = m.Thread.View(m.Parent).Wait;
        HostCheck.Equal(3U, m.Thread.View(m.Parent).WaitSource);
        m.Park(m.Parent);
        HostCheck.Equal(child.Value, m.Thread.Kernel.Dispatch().Value);
        m.Thread.Put32(Data + 64, 0x63);
        HostCheck.Equal(1L, m.Value(1, pipe.Write, Data + 64, 1));
        HostCheck.Equal(KernelThreadTable.Completion.Woken, m.Thread.Kernel.Inspect(m.Parent).Completion);
        HostCheck.Equal(0L, m.Thread.Call(234, 101, 101, 10).Value);
        HostCheck.Equal(1L, m.Value(0, pipe.Read, Output + 32, 1));
        HostCheck.Equal((byte)0x63, *m.Thread.At(Output + 32));
        m.Thread.Yield(child);
        HostCheck.Equal(m.Parent.Value, m.Thread.Kernel.Dispatch().Value);
        HostCheck.Equal(LinuxSyscallAction.Blocked, m.Complete().Action);
        var replacement = m.Thread.View(m.Parent).Wait;
        HostCheck.True(replacement.Sequence > first.Sequence);
        HostCheck.Equal(KernelThreadTable.Completion.Interrupted, m.Thread.Kernel.Inspect(m.Parent).Completion);
        HostCheck.Equal(1U, m.Files.State->RetainedPipeOperations);
        HostCheck.True(m.Thread.Service->CommitEpollWait(Key, 2, m.Thread.Kernel.Capture(m.Parent), out bool handoff) && !handoff);
        var completed = m.Complete();
        HostCheck.Equal(LinuxSyscallAction.Return, completed.Action);
        HostCheck.Equal(-4L, completed.Value);
        HostCheck.Equal(0U, m.Files.State->RetainedPipeOperations);
        HostCheck.True(!m.Thread.View(m.Parent).Wait.IsValid);
        HostCheck.Equal(Signals.Delivery.Delivered, m.Thread.Service->DeliverPendingSignals(Key, 2));
        ulong frame = m.Thread.CurrentImage()->Frame.Rsp;
        m.Thread.CurrentImage()->Frame.Rsp = frame + 8;
        HostCheck.Equal(LinuxSyscallAction.RestoreUser, m.Thread.Call(15).Action);
        HostCheck.Equal(-4L, m.Thread.CurrentImage()->Value);
        HostCheck.Equal(1L, m.Value(1, pipe.Write, Data + 64, 1));
        HostCheck.Equal(1L, m.Value(232, epoll, Output, 1, ulong.MaxValue));
        HostCheck.Equal(1U, m.Thread.Read32(Output));
        HostCheck.Equal(42UL, m.Thread.Read64(Output + 4));
        HostCheck.Equal(1L, m.Value(0, pipe.Read, Output + 32, 1));
        m.Drain();
    }

    [HostTest("m16-020 initial pending epoll signals complete before Park while ready data wins with or without SA_RESTART")]
    private static void InitialPendingAndReadySignal()
    {
        foreach (bool restart in new[]
        {
            false,
            true
        }

        )
            foreach (bool ready in new[]
            {
                false,
                true
            }

            )
            {
                using Model m = new(signals: true);
                var pipe = m.Pipe();
                uint epoll = m.Create();
                *(Signals.Disposition*)m.Thread.At(Data) = new()
                {
                    Handler = User + 0x180,
                    Flags = 0x04000004UL | (restart ? 0x10000000UL : 0),
                    Restorer = User + 0x200
                };
                HostCheck.Equal(0L, m.Thread.Call(13, 10, Data, 0, 8).Value);
                HostCheck.Equal(0L, m.Control(epoll, 1, pipe.Read, 1, 51));
                if (ready)
                {
                    m.Thread.Put32(Data + 64, 0x65);
                    HostCheck.Equal(1L, m.Value(1, pipe.Write, Data + 64, 1));
                }

                HostCheck.Equal(0L, m.Thread.Call(234, 101, 101, 10).Value);
                ulong continuation = m.Thread.CurrentImage()->Frame.Rip;
                var result = m.Call(232, epoll, Output, 1, ulong.MaxValue);
                if (ready)
                {
                    HostCheck.Equal(LinuxSyscallAction.Return, result.Action);
                    HostCheck.Equal(1L, result.Value);
                    HostCheck.Equal(1U, m.Thread.Read32(Output));
                    HostCheck.Equal(51UL, m.Thread.Read64(Output + 4));
                }
                else
                {
                    HostCheck.Equal(LinuxSyscallAction.Blocked, result.Action);
                    HostCheck.Equal(3U, m.Thread.View(m.Parent).WaitSource);
                    HostCheck.Equal(1U, m.Files.State->RetainedPipeOperations);
                    HostCheck.Equal(KernelThreadTable.Completion.Interrupted, m.Thread.Kernel.Inspect(m.Parent).Completion);
                    HostCheck.True(m.Thread.Service->CommitEpollWait(Key, 2, m.Thread.Kernel.Capture(m.Parent), out bool handoff) && !handoff);
                    result = m.Complete();
                    HostCheck.Equal(LinuxSyscallAction.Return, result.Action);
                    HostCheck.Equal(-4L, result.Value);
                }

                HostCheck.True(!m.Thread.View(m.Parent).Wait.IsValid);
                HostCheck.Equal(0U, m.Files.State->RetainedPipeOperations);
                HostCheck.Equal(0U, m.Thread.View(m.Parent).WaitSource);
                HostCheck.Equal(Signals.Delivery.Delivered, m.Thread.Service->DeliverPendingSignals(Key, 2));
                ulong frame = m.Thread.CurrentImage()->Frame.Rsp;
                HostCheck.Equal(unchecked((ulong)(ready ? 1L : -4L)), m.Thread.Read64(frame + 152));
                HostCheck.Equal(continuation, m.Thread.Read64(frame + 176));
                m.Thread.CurrentImage()->Frame.Rsp = frame + 8;
                HostCheck.Equal(LinuxSyscallAction.RestoreUser, m.Thread.Call(15).Action);
                HostCheck.Equal(ready ? 1L : -4L, m.Thread.CurrentImage()->Value);
                HostCheck.Equal(continuation, m.Thread.CurrentImage()->Frame.Rip);
                if (ready)
                    HostCheck.Equal(1L, m.Value(0, pipe.Read, Output + 32, 1));
                m.Drain();
            }
    }

    [HostTest("m16-020 masked and ignored epoll signals preserve initial and replacement waits until genuine readiness")]
    private static void NonactionableWaitSignals()
    {
        foreach (bool ignored in new[]
        {
            false,
            true
        }

        )
            foreach (bool stolen in new[]
            {
                false,
                true
            }

            )
            {
                using Model m = new(signals: true);
                var child = m.Thread.Clone();
                var pipe = m.Pipe();
                uint epoll = m.Create();
                *(Signals.Disposition*)m.Thread.At(Data) = new()
                {
                    Handler = ignored ? 1UL : User + 0x180,
                    Flags = 0x14000004,
                    Restorer = User + 0x200
                };
                HostCheck.Equal(0L, m.Thread.Call(13, 10, Data, 0, 8).Value);
                HostCheck.Equal(0L, m.Control(epoll, 1, pipe.Read, 1, 61));
                if (!ignored)
                {
                    m.Thread.Put64(Data + 64, 512);
                    HostCheck.Equal(0L, m.Thread.Call(14, 0, Data + 64, 0, 8).Value);
                }

                if (!stolen)
                    HostCheck.Equal(0L, m.Thread.Call(234, 101, 101, 10).Value);
                HostCheck.Equal(LinuxSyscallAction.Blocked, m.Call(232, epoll, Output, 1, ulong.MaxValue).Action);
                HostCheck.Equal(KernelThreadTable.Completion.None, m.Thread.Kernel.Inspect(m.Parent).Completion);
                var first = m.Thread.View(m.Parent).Wait;
                m.Park(m.Parent);
                HostCheck.Equal(child.Value, m.Thread.Kernel.Dispatch().Value);
                m.Thread.Put32(Data + 80, 0x66);
                if (stolen)
                {
                    HostCheck.Equal(1L, m.Value(1, pipe.Write, Data + 80, 1));
                    HostCheck.Equal(KernelThreadTable.Completion.Woken, m.Thread.Kernel.Inspect(m.Parent).Completion);
                    HostCheck.Equal(0L, m.Thread.Call(234, 101, 101, 10).Value);
                    HostCheck.Equal(1L, m.Value(0, pipe.Read, Output + 32, 1));
                    m.Thread.Yield(child);
                    HostCheck.Equal(m.Parent.Value, m.Thread.Kernel.Dispatch().Value);
                    HostCheck.Equal(LinuxSyscallAction.Blocked, m.Complete().Action);
                    HostCheck.True(m.Thread.View(m.Parent).Wait.Sequence > first.Sequence);
                    HostCheck.Equal(3U, m.Thread.View(m.Parent).WaitSource);
                    HostCheck.Equal(KernelThreadTable.Completion.None, m.Thread.Kernel.Inspect(m.Parent).Completion);
                    HostCheck.Equal(1U, m.Files.State->RetainedPipeOperations);
                    m.Park(m.Parent);
                    HostCheck.Equal(child.Value, m.Thread.Kernel.Dispatch().Value);
                }

                HostCheck.Equal(1L, m.Value(1, pipe.Write, Data + 80, 1));
                HostCheck.Equal(KernelThreadTable.Completion.Woken, m.Thread.Kernel.Inspect(m.Parent).Completion);
                m.Thread.Yield(child);
                HostCheck.Equal(m.Parent.Value, m.Thread.Kernel.Dispatch().Value);
                var result = m.Complete();
                HostCheck.Equal(LinuxSyscallAction.Return, result.Action);
                HostCheck.Equal(1L, result.Value);
                HostCheck.Equal(1U, m.Thread.Read32(Output));
                HostCheck.Equal(61UL, m.Thread.Read64(Output + 4));
                HostCheck.True(!m.Thread.View(m.Parent).Wait.IsValid);
                HostCheck.Equal(0U, m.Files.State->RetainedPipeOperations);
                ulong continuation = m.Thread.CurrentImage()->Frame.Rip;
                HostCheck.Equal(Signals.Delivery.None, m.Thread.Service->DeliverPendingSignals(Key, 2));
                HostCheck.Equal(1L, m.Thread.CurrentImage()->Value);
                HostCheck.Equal(continuation, m.Thread.CurrentImage()->Frame.Rip);
                if (!ignored)
                {
                    m.Thread.Put64(Data + 64, 0);
                    HostCheck.Equal(0L, m.Thread.Call(14, 2, Data + 64, 0, 8).Value);
                    HostCheck.Equal(Signals.Delivery.Delivered, m.Thread.Service->DeliverPendingSignals(Key, 2));
                    ulong frame = m.Thread.CurrentImage()->Frame.Rsp;
                    m.Thread.CurrentImage()->Frame.Rsp = frame + 8;
                    HostCheck.Equal(LinuxSyscallAction.RestoreUser, m.Thread.Call(15).Action);
                    HostCheck.Equal(continuation, m.Thread.CurrentImage()->Frame.Rip);
                    HostCheck.Equal(0L, m.Thread.CurrentImage()->Value);
                }

                HostCheck.Equal(Signals.Delivery.None, m.Thread.Service->DeliverPendingSignals(Key, 2));
                HostCheck.Equal(1L, m.Value(0, pipe.Read, Output + 32, 1));
                m.Drain();
            }
    }

    [HostTest("m16-020 stolen epoll readiness interrupts the replacement wait for a pending handler")]
    private static void StolenReadinessSignal()
    {
        using Model m = new(signals: true);
        using var connection = Connection();
        var child = m.Thread.Clone();
        uint epoll = m.Create(), listener = m.Listen();
        m.Enqueue(listener, connection);
        uint fd = m.Accept(listener);
        *(Signals.Disposition*)m.Thread.At(Data) = new()
        {
            Handler = User + 0x180,
            Flags = 0x14000004,
            Restorer = User + 0x200
        };
        HostCheck.Equal(0L, m.Thread.Call(13, 10, Data, 0, 8).Value);
        HostCheck.Equal(0L, m.Control(epoll, 1, fd, 1, 42));
        HostCheck.Equal(LinuxSyscallAction.Blocked, m.Call(281, epoll, Output, 1, ulong.MaxValue).Action);
        m.Park(m.Parent);
        HostCheck.Equal(child.Value, m.Thread.Kernel.Dispatch().Value);
        HostCheck.Equal(Tx.Status.Success, Tx.ReceiveFrame(connection.Owner, Wire.Packet(100, "x"u8, ack: 700), out _));
        HostCheck.True(m.Thread.Service->PollDescriptorReadiness(Key, 2));
        HostCheck.Equal(0L, m.Thread.Call(234, 101, 101, 10).Value);
        HostCheck.Equal(1L, m.Value(45, fd, Data, 1));
        m.Thread.Yield(child);
        HostCheck.Equal(m.Parent.Value, m.Thread.Kernel.Dispatch().Value);
        HostCheck.Equal(LinuxSyscallAction.Blocked, m.Complete().Action);
        HostCheck.Equal(KernelThreadTable.Completion.Interrupted, m.Thread.Kernel.Inspect(m.Parent).Completion);
        HostCheck.True(m.Thread.Service->CommitEpollWait(Key, 2, m.Thread.Kernel.Capture(m.Parent), out bool handoff) && !handoff);
        HostCheck.Equal(-4L, m.Complete().Value);
        HostCheck.Equal(Signals.Delivery.Delivered, m.Thread.Service->DeliverPendingSignals(Key, 2));
        ulong frame = m.Thread.CurrentImage()->Frame.Rsp;
        m.Thread.CurrentImage()->Frame.Rsp = frame + 8;
        HostCheck.Equal(LinuxSyscallAction.RestoreUser, m.Thread.Call(15).Action);
        m.CloseAndReap(fd, connection);
        m.Drain();
    }

    [HostTest("m16-012 tgkill interrupts epoll without SA_RESTART and real deadline wins before signal", HostLane.Fast, 11)]
    private static void SignalInterruption()
    {
        foreach (bool timed in new[]
        {
            false,
            true
        }

        )
        {
            using Model m = new(signals: true);
            var child = m.Thread.Clone();
            uint epoll = m.Create();
            *(Signals.Disposition*)m.Thread.At(Data) = new()
            {
                Handler = User + 0x180,
                Flags = 0x14000004,
                Restorer = User + 0x200
            };
            HostCheck.Equal(0L, m.Thread.Call(13, 10, Data, 0, 8).Value);
            HostCheck.Equal(LinuxSyscallAction.Blocked, m.Call(281, epoll, Output, 1, timed ? 1UL : ulong.MaxValue).Action);
            m.Park(m.Parent);
            HostCheck.Equal(child.Value, m.Thread.Kernel.Dispatch().Value);
            if (timed)
                m.Thread.Now = 1_001_000; // Deliberately no prior timer advance.
            HostCheck.Equal(0L, m.Thread.Call(234, 101, 101, 10).Value);
            m.Thread.Yield(child);
            HostCheck.Equal(m.Parent.Value, m.Thread.Kernel.Dispatch().Value);
            HostCheck.Equal(timed ? 0L : -4L, m.Complete().Value);
            HostCheck.Equal(Signals.Delivery.Delivered, m.Thread.Service->DeliverPendingSignals(Key, 2));
            ulong signalFrame = m.Thread.CurrentImage()->Frame.Rsp;
            m.Thread.CurrentImage()->Frame.Rsp = signalFrame + 8;
            HostCheck.Equal(LinuxSyscallAction.RestoreUser, m.Thread.Call(15).Action);
            HostCheck.Equal(timed ? 0L : -4L, m.Thread.CurrentImage()->Value);
            HostCheck.Equal(0U, m.Files.State->RetainedPipeOperations);
            m.Drain();
        }
    }
}
