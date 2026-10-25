using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Pal;
using OS.Kernel.Threading;
using Memory = OS.Kernel.Compatibility.LinuxX64.LinuxMemoryServices;
using Signals = OS.Kernel.Compatibility.LinuxX64.LinuxSignalServices;
using OS.Net;
using B = OS.Storage.WritableBlockSession;
using C = OS.Storage.ContinuingFat32Mutation;
using Fs = OS.Storage.ReadOnlyWebRootFileSystem;
using P = OS.Storage.WritableBlockReadPort;
using F = OS.Kernel.Compatibility.LinuxX64.LinuxDescriptorServices;
using W = OS.Kernel.Compatibility.LinuxX64.LinuxContinuingWritableDescriptorServices;
using Sockets = OS.Kernel.Compatibility.LinuxX64.LinuxSocketServices;
using Epoll = OS.Kernel.Compatibility.LinuxX64.LinuxEpollServices;
using Network = OS.Kernel.Compatibility.LinuxX64.LinuxSocketNetwork;
using Tx = OS.Net.TcpSendWindow;
using Rx = OS.Net.TcpReceiveWindow;
using Wire = TcpReceiveWindowTests;

// Real queued fake media, with separate C-writer and paired-global controls.
// Every positive pair uses one ORIGINAL B/C. Thread controls use copied host
// contexts and actual scheduler contracts; none establishes native execution.
internal static unsafe class LinuxBorrowedDiskDescriptorServicesTests
{
    private const ulong Key = LinuxThreadServicesTests.Key, Base = LinuxThreadServicesTests.UserBase;
    private const ulong Path = Base + 0x1200, Output = Base + 0x2000;
    private const ulong Vector = Base + 0x1000, Payload = Base + 0x4000;
    private const ulong SocketData = Base + 0x2400, SocketOutput = Base + 0x2800, SocketEvent = Base + 0x2c00;
    private delegate bool Retain(F.State* state, ulong address, ulong bytes);
    private static readonly Retain CanRetain = typeof(F).GetMethod("DiskCanRetainStorage", BindingFlags.Static | BindingFlags.NonPublic)!.CreateDelegate<Retain>();
    private static readonly Retain WritableCanRetain = typeof(F).GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
        .Single(m => m.Name == "WritableDiskCanRetainStorage" && m.GetParameters().Length == 3).CreateDelegate<Retain>();
    private static readonly Retain SocketCanRetain = typeof(F).GetMethod("SocketCanRetainStorage", BindingFlags.Static | BindingFlags.NonPublic)!.CreateDelegate<Retain>();
    private delegate bool RetainBatch(F.State* state, ReadOnlySpan<ulong> ranges);
    private static readonly RetainBatch PairedBatch = typeof(F).GetMethod("PairedThreadStorageOutside", BindingFlags.Static | BindingFlags.NonPublic)!.CreateDelegate<RetainBatch>();
    private delegate bool Audit(F.State* state);
    private static readonly Audit AuditDisk = typeof(F).GetMethod("AuditDisk", BindingFlags.Static | BindingFlags.NonPublic)!.CreateDelegate<Audit>();
    private static readonly Audit AuditWritable = typeof(F).GetMethod("AuditWritableDisk", BindingFlags.Static | BindingFlags.NonPublic)!.CreateDelegate<Audit>();
    private static readonly Audit AuditPair = typeof(F).GetMethod("AuditDiskPair", BindingFlags.Static | BindingFlags.NonPublic)!.CreateDelegate<Audit>();
    private static readonly Audit ProcessCurrent = typeof(LinuxThreadServices).GetMethod("DescriptorProcessCurrent", BindingFlags.Static | BindingFlags.NonPublic)!.CreateDelegate<Audit>();
    private static readonly Retain CanPublish = typeof(LinuxThreadServices).GetMethod("CanPublishDescriptorStorage", BindingFlags.Static | BindingFlags.NonPublic)!.CreateDelegate<Retain>();
    private static readonly Retain ProcessOutside = typeof(LinuxThreadServices).GetMethod("DescriptorProcessOutsideStorage", BindingFlags.Static | BindingFlags.NonPublic)!.CreateDelegate<Retain>();

    [HostTest("outer process source4 deferred fstat pins original cross-page144 bytes through writer and held readiness", HostLane.Native)]
    private static void DeferredFstat() => WithPairedThread(o =>
    {
        uint fd = o.Open(); ulong output = Base + 0x2fc1; new Span<byte>(o.Thread.At(output - 1), 146).Fill(0xa9);
        o.StartWriter(); uint copies = o.Thread.Writes;
        HostCheck.Equal(LinuxSyscallAction.Blocked, o.Thread.DescriptorCall(5, fd, output).Action);
        HostCheck.True(!o.May(output, 144) && o.May(output - 1, 1) && o.May(output + 144, 1)); o.ParkSource4();
        HostCheck.True(o.Thread.Service->PollDiskReadiness(Key, 2)); HostCheck.True(!o.May(output, 144));
        o.WriterReady(() => HostCheck.True(!o.May(output, 144))); o.ConsumeWriter(); o.ReadySource4();
        HostCheck.True(!o.May(output, 144) && o.Thread.Writes == copies && new ReadOnlySpan<byte>(o.Thread.At(output - 1), 146).IndexOfAnyExcept((byte)0xa9) < 0);
        HostCheck.Equal(0L, o.ConsumeSource4()); HostCheck.True(o.May(output, 144) && o.Thread.Writes == copies + 1 && o.Position(fd) == 0);
        HostCheck.Equal((ulong)o.Expected("/app/wwwroot/index.html").Length, o.Thread.Read64(output + 48));
        HostCheck.True(*o.Thread.At(output - 1) == 0xa9 && *o.Thread.At(output + 144) == 0xa9);
    });

    [HostTest("outer process source4 fstat original short prefix cannot expand when a real mapped tail becomes writable", HostLane.Native)]
    private static void DeferredFstatPrefix() => WithPairedThread(o =>
    {
        uint fd = o.Open(); var memory = o.Files.Memory;
        const ulong page = Memory.DynamicStart + 0x20000, output = page + 4096 - 64;
        HostCheck.Equal((long)page, Memory.Map(memory.State, page, 4096, 3, 0x100022, ulong.MaxValue, 0));
        o.Thread.UseMemory(memory.Memory); new Span<byte>(memory.At(output), 64).Fill(0xa7);
        HostCheck.True(Memory.AttachMutationGuard(memory.State, o.Thread.Service, &LinuxFutexTests.MemoryGuard));
        try
        {
            o.StartWriter(); uint copies = o.Thread.Writes; HostCheck.Equal(LinuxSyscallAction.Blocked, o.Thread.DescriptorCall(5, fd, output).Action); o.ParkSource4();
            HostCheck.True(!o.May(output, 64) && o.May(page + 4096, 4096));
            HostCheck.Equal((long)(page + 4096), Memory.Map(memory.State, page + 4096, 4096, 3, 0x100022, ulong.MaxValue, 0));
            for (uint i = 64; i < 144; i++) *memory.At(output + i) = 0xa7;
            o.WriterReady(); o.ConsumeWriter(); o.ReadySource4(); HostCheck.Equal(-14L, o.ConsumeSource4());
            HostCheck.True(o.Thread.Writes == copies && o.May(output, 144));
            for (uint i = 0; i < 144; i++) HostCheck.Equal((byte)0xa7, *memory.At(output + i));
            // The SAME actual callback and now-mapped bytes can succeed on a
            // fresh request: the earlier failure was its captured ceiling.
            var fresh = o.Thread.DescriptorCall(5, fd, output); HostCheck.True(fresh.Action == LinuxSyscallAction.Return && fresh.Value == 0 && o.Thread.Writes == copies + 1);
            byte[] wire = Enumerable.Range(0, 144).Select(i => *memory.At(output + (uint)i)).ToArray();
            HostCheck.Equal((ulong)o.Expected("/app/wwwroot/index.html").Length, System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(wire.AsSpan(48)));
        }
        finally { HostCheck.True(Memory.DetachMutationGuard(memory.State, o.Thread.Service, &LinuxFutexTests.MemoryGuard)); }
    });

    [HostTest("outer process source4 fstat cancellation and fd reuse revoke original output without late metadata", HostLane.Native)]
    private static void DeferredFstatCancellation() => CombinedWebDataImageFixtureTests.WithImage((_, _, _, _, path, receipt) =>
    {
        foreach (bool close in new[] { false, true })
        {
            using var o = new Owner(File.ReadAllBytes(path), receipt, false, beforeThreads: p => { p.PrepareWritable(); p.AttachPair(false); }, signals: true);
            try
            {
                uint fd = o.Open(); OSHostHandle child = close ? o.Thread.Clone() : default; o.Fill(144, 0xb7); o.StartWriter(); uint copies = o.Thread.Writes;
                // The actual restart path validates the captured syscall opcode.
                // Default fixture bytes intentionally contain no executable syscall.
                *(ushort*)o.Thread.At(Base + 0xfe) = 0x050f;
                HostCheck.Equal(LinuxSyscallAction.Blocked, o.Thread.DescriptorCall(5, fd, Output).Action); o.ParkSource4();
                if (close)
                {
                    HostCheck.Equal(child.Value, o.Thread.Kernel.Dispatch().Value); HostCheck.Equal(0L, o.Core(3, fd));
                    o.PutPath("/app/fixture.data"); HostCheck.Equal((long)fd, o.Core(2, Path, 0x88100));
                    HostCheck.True(!o.May(Output, 144)); o.ReadySource4(); o.ExitChild(child);
                    // Actual child exit clears its user TID once; it must not be
                    // confused with publication of the revoked metadata output.
                    HostCheck.Equal(copies + 1, o.Thread.Writes); copies++;
                }
                else HostCheck.True(o.Thread.Kernel.Table->Interrupt(KernelThreadTableTests.Key, 2, o.Thread.View(o.Worker).Wait).Succeeded);
                HostCheck.Equal(close ? -9L : -4L, o.ConsumeSource4());
                if (!close) HostCheck.Equal(Signals.Delivery.None, o.Thread.Service->DeliverPendingSignals(Key, 2));
                HostCheck.True(o.May(Output, 144) && o.Thread.Writes == copies && o.Unchanged(144, 0xb7));
                o.WriterReady(); o.ConsumeWriter();
                for (int i = 0; i < 4; i++) HostCheck.True(o.Thread.Service->PollDiskReadiness(Key, 2));
                HostCheck.True(o.Thread.Writes == copies && o.Unchanged(144, 0xb7));
            }
            catch (Exception failure) { Console.WriteLine($"OUTER_CANCEL_BODY_FAILURE close={close} " + failure); throw; }
        }
    });

    [HostTest("outer process complete retained ranges reject late candidates while direct TCP and paired storage remain usable", HostLane.Native)]
    private static void OuterRanges() => CombinedWebDataImageFixtureTests.WithImage((_, _, _, _, path, receipt) =>
    {
        using var o = new Owner(File.ReadAllBytes(path), receipt, false, beforeThreads: p => { p.PrepareWritable(); p.AttachPair(false); p.PrepareConnectedTransport(); }, signals: true);
        var before = o.SocketAuditSnapshot();
        foreach ((ulong start, ulong bytes) in o.ProcessExtents())
            HostCheck.True(!CanPublish(o.Files.State, start, bytes) && !CanPublish(o.Files.State, start - 8, bytes + 16) && !CanPublish(o.Files.State, start + bytes - 1, 2));
        HostCheck.True(!CanPublish(o.Files.State, ulong.MaxValue - 7, 16) && ProcessCurrent(o.Files.State)); CheckSocketAuditSnapshot(before);
        o.SocketRoundtrip(); uint fd = o.Open(); HostCheck.Equal(7L, o.Run(o.Call(0, fd, Output, 7)));
    });

    [HostTest("outer process EnablePipes rejects prepared worker aliases into each private client before attachment", HostLane.Native)]
    private static void OuterAdmission() => CombinedWebDataImageFixtureTests.WithImage((_, _, _, _, path, receipt) =>
    {
        using var o = new Owner(File.ReadAllBytes(path), receipt, false, beforeThreads: p =>
        {
            p.PrepareWritable(); p.AttachPair(false); p.PrepareTransport(); p.PreparePacketNetwork();
            foreach (ulong alias in new[] { (ulong)p.WriteInput, (ulong)p.Storage.EngineInput.Before, (ulong)p.Storage.Operations[0].Storage.Range, (ulong)p.PacketNetwork!.Context })
            {
                var before = p.SocketAuditSnapshot(); var original = p.Thread.Workers[2];
                try
                {
                    p.Thread.Workers[2].Initial.Context = (X64ThreadContext*)alias;
                    HostCheck.True(!p.Thread.Service->EnablePipes(Key, 2, p.Files.State) && !p.Thread.Service->Failed);
                }
                finally { p.Thread.Workers[2] = original; }
                CheckSocketAuditSnapshot(before); HostCheck.True(F.Audit(p.Files.State));
            }
        });
        HostCheck.True(ProcessCurrent(o.Files.State)); o.PacketRoundtrip();
    });

    [HostTest("outer process ongoing prepared context alias rejects actual dispatch and packet callbacks without poisoning restored owner", HostLane.Native)]
    private static void OuterOngoing() => WithPacketOwner(o =>
    {
        var frame = o.Call(44, o.Connected, SocketData, 3); var before = o.SocketAuditSnapshot(); var original = o.Thread.Workers[2];
        uint sends = o.PacketNetwork!.Sends, clocks = o.PacketNetwork.Clocks;
        try
        {
            o.Thread.Workers[2].Initial.Context = (X64ThreadContext*)o.PacketNetwork.Context;
            HostCheck.True(F.Audit(o.Files.State) && !ProcessCurrent(o.Files.State));
            HostCheck.Equal(LinuxSyscallAction.Rejected, o.Thread.Service->DispatchDescriptor(Key, 2, frame).Action);
            HostCheck.Equal(Network.Status.InvalidState, o.PacketNetwork.Pump());
        }
        finally { o.Thread.Workers[2] = original; }
        CheckSocketAuditSnapshot(before); HostCheck.True(!o.Thread.Service->Failed && ProcessCurrent(o.Files.State) && sends == o.PacketNetwork.Sends && clocks == o.PacketNetwork.Clocks);
        o.PacketRoundtrip();
    });

    [HostTest("outer process late enqueue checks unpublished real receive ring against actual scheduler backing before publication", HostLane.Native)]
    private static void OuterLateEnqueue() => CombinedWebDataImageFixtureTests.WithImage((_, _, _, _, path, receipt) =>
    {
        using var o = new Owner(File.ReadAllBytes(path), receipt, false, beforeThreads: p => { p.PrepareWritable(); p.AttachPair(false); p.PrepareConnectedTransport(); });
        var candidate = LinuxSocketServicesTests.Connection(); bool adopted = false;
        try
        {
            HostCheck.True(F.Capture(o.Files.State, o.Listener, out var token)); var before = o.SocketAuditSnapshot(); uint callbacks = candidate.Hardware->Calls;
            byte* original = ReceiveBuffer(ref *candidate.Receiver);
            try
            {
                ReceiveBuffer(ref *candidate.Receiver) = (byte*)o.Thread.Kernel.Ready;
                HostCheck.True(!Sockets.EnqueueAccepted(o.SocketState, token, o.Listener, candidate.Receiver, candidate.Owner, (uint)sizeof(TcpSendWindowTests.Hardware)));
            }
            finally { ReceiveBuffer(ref *candidate.Receiver) = original; }
            CheckSocketAuditSnapshot(before); HostCheck.True(callbacks == candidate.Hardware->Calls && candidate.Table->Count == 1 && ProcessCurrent(o.Files.State));
            HostCheck.True(Sockets.EnqueueAccepted(o.SocketState, token, o.Listener, candidate.Receiver, candidate.Owner, (uint)sizeof(TcpSendWindowTests.Hardware)));
            o.Adopt(candidate); adopted = true; HostCheck.True(o.SCall(288, o.Listener, 0, 0, F.NonBlock) >= 3 && ProcessCurrent(o.Files.State));
        }
        finally { if (!adopted) candidate.Dispose(); }
    });

    [HostTest("outer process external packet frames reject actual scheduler and callback carrier aliases without effects", HostLane.Native)]
    private static void OuterPacketFrames() => WithPacketOwner(o =>
    {
        var network = o.PacketNetwork!; var before = o.SocketAuditSnapshot();
        uint clocks = network.Clocks, sends = network.Sends, writes = o.Thread.Writes;
        Span<byte> output = stackalloc byte[1514]; output.Fill(0xd6);
        foreach ((ulong start, ulong bytes) in o.ProcessExtents())
        {
            HostCheck.True(!ProcessOutside(o.Files.State, start, bytes) && !ProcessOutside(o.Files.State, start - 1, bytes + 2) &&
                !ProcessOutside(o.Files.State, start + bytes - 1, 2));
            // All these original process extents lie in the model's actual
            // mapped arena. Even the short queue/carrier ranges must reject a
            // larger output that starts there before its first possible write.
            HostCheck.Equal(Network.Status.InvalidStorage, Network.Pump(network.State, new Span<byte>((void*)start, 1514)));
            HostCheck.Equal(Network.Status.InvalidStorage, Network.Receive(network.State, new ReadOnlySpan<byte>((void*)(start + bytes - 1), 2), output));
        }
        HostCheck.True(output.IndexOfAnyExcept((byte)0xd6) < 0 && clocks == network.Clocks && sends == network.Sends && writes == o.Thread.Writes);
        CheckSocketAuditSnapshot(before); HostCheck.True(ProcessCurrent(o.Files.State) && !o.Thread.Service->Failed);
        o.PacketRoundtrip();
    });

    [HostTest("outer process stopping blocks new transport publication while real disjoint packet FIN cleanup remains usable", HostLane.Native)]
    private static void OuterPacketStopping() => WithPacketOwner(o =>
    {
        o.PacketRoundtrip();
        // Close through the actual live worker before stopping. Teardown never
        // dispatches a new user close after the process enters its stop phase.
        o.CloseUserDescriptors(); HostCheck.True(o.Thread.Service->Shutdown(Key, 2, 7));
        byte* frame = stackalloc byte[1514]; Span<byte> output = new(frame, 1514); output.Fill(0xa6);
        HostCheck.True(ProcessCurrent(o.Files.State) && ProcessOutside(o.Files.State, (ulong)frame, 1514) &&
            !CanPublish(o.Files.State, (ulong)frame, 1514));
        var network = o.PacketNetwork!; uint clocks = network.Clocks, sends = network.Sends;
        HostCheck.Equal(Network.Status.Success, Network.Pump(network.State, output));
        HostCheck.True(network.Clocks > clocks && network.Sends > sends && !o.Thread.Service->Failed);
        HostCheck.Equal(PacketParseStatus.Success, EthernetHeader.TryParse(network.Sent, out _, out var ip));
        HostCheck.Equal(PacketParseStatus.Success, Ipv4Header.TryParse(ip, out _, out var tcp));
        HostCheck.Equal(PacketParseStatus.Success, TcpHeader.TryParse(tcp, out var header, out _, out _)); HostCheck.True((header.Flags & 1) != 0);
    });

    private static void WithPairedThread(Action<Owner> action) => CombinedWebDataImageFixtureTests.WithImage((_, _, _, _, path, receipt) =>
    {
        using var o = new Owner(File.ReadAllBytes(path), receipt, false, beforeThreads: p => { p.PrepareWritable(); p.AttachPair(false); });
        try { action(o); } catch (Exception failure) { Console.WriteLine("OUTER_PROCESS_BODY_FAILURE " + failure); throw; }
    });

    [HostTest("borrowed retired filesystem actual source4 cancellation drains local thread ownership without borrowing retired backend", HostLane.Native)]
    private static void RetiredThreadCleanup() => CombinedWebDataImageFixtureTests.WithImage((_, _, _, _, path, receipt) =>
    {
        using var o = new Owner(File.ReadAllBytes(path), receipt, true, signals: true);
        try
        {
            uint fd = o.Open(); o.Fill(16, 0xc7); *(ushort*)o.Thread.At(Base + 0xfe) = 0x050f;
            // Retain the actual captured close frame before retirement. Local
            // teardown never asks to borrow a new image from a retired tree.
            var close = o.Call(3, fd); uint copies = o.Thread.Writes;
            HostCheck.Equal(LinuxSyscallAction.Blocked, o.Thread.DescriptorCall(17, fd, Output, 7, 0).Action);
            o.WaitSource4Port(); o.ParkSource4();
            HostCheck.True(o.Thread.Kernel.Table->Interrupt(KernelThreadTableTests.Key, 2, o.Thread.View(o.Worker).Wait).Succeeded);
            HostCheck.Equal(-4L, o.ConsumeSource4()); HostCheck.Equal(Signals.Delivery.None, o.Thread.Service->DeliverPendingSignals(Key, 2));
            HostCheck.True(o.Storage.Port->RequestActive && o.Storage.Block->RequestPending && o.Thread.Writes == copies && o.Unchanged(16, 0xc7));
            HostCheck.Equal(B.Status.Success, B.Stop(o.Storage.Block)); o.Storage.Sample();
            HostCheck.Equal(P.Status.Success, P.Stop(o.Storage.Port)); HostCheck.Equal(Fs.Status.Success, Fs.Stop(o.Storage.Mount));
            HostCheck.True(o.Storage.Mount->IsRetired && o.Storage.Port->Retired && o.Storage.Block->Retired &&
                !CanRetain(o.Files.State, (ulong)o.Thread.At(Output), 16));
            uint callbacks = o.Storage.Device.Hardware->Calls;
            byte* originalRange = o.Storage.Operations[0].Storage.Range;
            try
            {
                o.Storage.Operations[0].Storage.Range = (byte*)o.Thread.Service;
                HostCheck.True(!o.Thread.Service->PollDiskReadiness(Key, 2));
                HostCheck.True(callbacks == o.Storage.Device.Hardware->Calls && o.Thread.Writes == copies && o.Unchanged(16, 0xc7));
            }
            finally { o.Storage.Operations[0].Storage.Range = originalRange; }
            for (int i = 0; i < 4; i++) HostCheck.True(o.Thread.Service->PollDiskReadiness(Key, 2));
            for (uint i = 0; i < F.DiskSlots; i++) HostCheck.Equal((byte)0, RequestPhase(ref o.Requests[i]));
            HostCheck.True(o.Thread.Writes == copies && o.Unchanged(16, 0xc7));
            var rejected = o.Thread.DescriptorCall(17, fd, Output, 7, 0);
            HostCheck.Equal(LinuxSyscallAction.Return, rejected.Action); HostCheck.True(rejected.Value < 0);
            HostCheck.True(!CanRetain(o.Files.State, (ulong)o.Thread.At(Output), 16));
            var closed = o.Thread.Service->DispatchDescriptor(Key, 2, close); HostCheck.Equal(LinuxSyscallAction.Return, closed.Action); HostCheck.Equal(0L, closed.Value);
            HostCheck.True(F.DetachDisk(o.Files.State)); o.ReadAttached = false;
            HostCheck.True(o.Thread.Service->Shutdown(Key, 2, 7));
            HostCheck.True(callbacks == o.Storage.Device.Hardware->Calls && o.Thread.Writes == copies && o.Unchanged(16, 0xc7));
            // Owner.Dispose must still commit the actual current worker exit,
            // confirm/reap/release and retire its independently held engine.
        }
        catch (Exception failure) { Console.WriteLine("BORROWED_RETIRED_THREAD_BODY_FAILURE " + failure); throw; }
    });

    [HostTest("borrowed linux disk Busy open and path stat retain one copied path and token through actual writer and held result", HostLane.Native)]
    private static void CopiedPath() => WithOwner(o =>
    {
        o.StartWriter(); o.PutPath("/app/wwwroot/index.html"); var openCall = o.Call(2, Path, 0x88100); var opened = o.Begin(openCall);
        HostCheck.Equal(F.DiskProgress.Pending, opened.Progress); HostCheck.True(opened.Token.IsActive);
        o.PutPath("/app/wwwroot/fixture/nested/info.txt"); var statCall = o.Call(4, Path, Output); var stat = o.Begin(statCall);
        o.PutPath("/app/wwwroot/does-not-exist.bin"); o.Fill(144, 0x87); ulong submitted = o.Storage.Block->Submitted;
        for (int i = 0; i < 10; i++)
        {
            HostCheck.Equal(F.DiskProgress.Pending, o.Pump(opened.Token).Progress); HostCheck.Equal(F.DiskProgress.Pending, o.Observe(stat.Token).Progress);
            HostCheck.True(o.Files.State->ActiveDescriptors == 3 && o.Unchanged(144, 0x87) && o.Storage.Block->Submitted == submitted && F.Audit(o.Files.State));
        }
        o.WriterReady(); HostCheck.Equal(F.DiskProgress.Pending, o.Pump(opened.Token).Progress); HostCheck.Equal(F.DiskProgress.Pending, o.Observe(stat.Token).Progress);
        o.ConsumeWriter(); HostCheck.Equal(F.DiskProgress.Ready, o.Ready(opened.Token).Progress);
        long fd = o.Complete(opened.Token, openCall).Value; HostCheck.True(fd >= 3);
        HostCheck.Equal(F.DiskProgress.Ready, o.Ready(stat.Token).Progress); HostCheck.Equal(0L, o.Complete(stat.Token, statCall, 144).Value);
        HostCheck.Equal((ulong)o.Expected("/app/wwwroot/fixture/nested/info.txt").Length, o.Thread.Read64(Output + 48));
        HostCheck.Equal(7L, o.Run(o.Call(17, (ulong)fd, Output, 7, 0)));
        HostCheck.True(new ReadOnlySpan<byte>(o.Thread.At(Output), 7).SequenceEqual(o.Expected("/app/wwwroot/index.html").AsSpan(0, 7)));
    });

    [HostTest("borrowed linux disk paused ready read EOF and SEEK_CUR preserve original cursor and never publish during dirty or held writer", HostLane.Native)]
    private static void ReadyResults() => WithOwner(o =>
    {
        uint fd = o.Open(); ulong position = 0;
        foreach (int mode in new[] { 0, 1, 2 })
        {
            var call = mode == 0 ? o.Call(0, fd, Output, 7) : mode == 1 ? o.Call(8, fd, 5, 1) : o.Call(17, fd, Output, 7, (ulong)o.Expected("/app/wwwroot/index.html").Length);
            var request = o.Begin(call); HostCheck.Equal(F.DiskProgress.Ready, o.Ready(request.Token).Progress);
            o.StartWriter(); o.Fill(16, 0x79); HostCheck.Equal(F.DiskProgress.Pending, o.Observe(request.Token).Progress);
            HostCheck.Equal(F.DiskProgress.Pending, o.Complete(request.Token, call).Progress);
            HostCheck.True(o.Unchanged(16, 0x79) && o.Position(fd) == position && F.Audit(o.Files.State));
            bool dirty = false;
            o.WriterReady(() =>
            {
                dirty |= o.Storage.Engine->Dirty; ulong submitted = o.Storage.Block->Submitted; uint calls = o.Storage.Device.Hardware->Calls;
                HostCheck.Equal(F.DiskProgress.Pending, o.Pump(request.Token).Progress); HostCheck.Equal(F.DiskProgress.Pending, o.Complete(request.Token, call).Progress);
                HostCheck.True(o.Unchanged(16, 0x79) && o.Position(fd) == position && o.Storage.Block->Submitted == submitted && o.Storage.Device.Hardware->Calls == calls);
            });
            HostCheck.True(dirty); HostCheck.Equal(F.DiskProgress.Pending, o.Observe(request.Token).Progress);
            HostCheck.Equal(F.DiskProgress.Pending, o.Complete(request.Token, call).Progress); HostCheck.True(o.Unchanged(16, 0x79));
            o.ConsumeWriter(); HostCheck.Equal(F.DiskProgress.Ready, o.Ready(request.Token).Progress);
            var result = o.Complete(request.Token, call); HostCheck.Equal(F.DiskProgress.Immediate, result.Progress);
            if (mode == 0) { HostCheck.Equal(7L, result.Value); position = 7; }
            else if (mode == 1) { HostCheck.Equal(12L, result.Value); position = 12; }
            else { HostCheck.Equal(0L, result.Value); HostCheck.True(o.Unchanged(16, 0x79)); }
            HostCheck.Equal(position, o.Position(fd));
        }
    });

    [HostTest("borrowed linux disk ready open and path metadata cancellation revoke typed outputs without replay or descriptor publication", HostLane.Native)]
    private static void ReadyCancellation() => WithOwner(o =>
    {
        foreach (ulong number in new ulong[] { 2, 4, 6 })
        {
            o.PutPath("/app/wwwroot/index.html"); var call = number == 2 ? o.Call(2, Path, 0x88100) : o.Call(number, Path, Output);
            var request = o.Begin(call); HostCheck.Equal(F.DiskProgress.Ready, o.Ready(request.Token).Progress); o.StartWriter(); o.Fill(144, 0xa1);
            HostCheck.Equal(F.DiskProgress.Pending, o.Complete(request.Token, call).Progress);
            var cancelled = F.CancelDisk(o.Files.State, o.OwnerKey, request.Token);
            HostCheck.True(cancelled.Progress == F.DiskProgress.Immediate && cancelled.Value == -4 && !cancelled.Worker.IsValid);
            HostCheck.Equal(F.DiskProgress.Rejected, o.Complete(request.Token, call).Progress);
            HostCheck.True(o.Files.State->ActiveDescriptors == 3 && o.Unchanged(144, 0xa1) && F.Audit(o.Files.State));
            o.WriterReady(); o.ConsumeWriter();
        }
    });

    [HostTest("borrowed linux disk deferred fstat keeps exact file generation and144 byte output ceiling through writer Busy", HostLane.Native)]
    private static void Fstat() => WithOwner(o =>
    {
        uint fd = o.Open(); o.StartWriter(); var call = o.Call(5, fd, Output); var request = o.Begin(call); o.Fill(144, 0xb2);
        HostCheck.Equal(F.DiskProgress.Pending, request.Progress); HostCheck.Equal(F.DiskProgress.Pending, o.Pump(request.Token).Progress);
        HostCheck.Equal(F.DiskProgress.Pending, o.Complete(request.Token, call, 144).Progress); HostCheck.True(o.Unchanged(144, 0xb2));
        o.WriterReady(); HostCheck.Equal(F.DiskProgress.Pending, o.Pump(request.Token).Progress); o.ConsumeWriter();
        HostCheck.Equal(F.DiskProgress.Ready, o.Ready(request.Token).Progress);
        HostCheck.Equal(-14L, o.Complete(request.Token, call, 143).Value); HostCheck.True(o.Unchanged(144, 0xb2));
        HostCheck.Equal(0L, o.Run(call)); HostCheck.Equal((ulong)o.Expected("/app/wwwroot/index.html").Length, o.Thread.Read64(Output + 48)); HostCheck.Equal(0UL, o.Position(fd));
    });

    [HostTest("borrowed linux disk ready closed fd cancellation preserves original EINTR or EBADF and replacement while writer owns device", HostLane.Native)]
    private static void CloseOrphan() => WithOwner(o =>
    {
        foreach (bool cancelled in new[] { false, true })
        {
            uint fd = o.Open(); var call = o.Call(0, fd, Output, 7); var request = o.Begin(call); HostCheck.Equal(F.DiskProgress.Ready, o.Ready(request.Token).Progress);
            HostCheck.Equal(0L, o.Core(3, fd)); o.PutPath("/app/fixture.data"); HostCheck.Equal(fd, (uint)o.Core(2, Path));
            F.Token replacement = default; HostCheck.True(F.Capture(o.Files.State, fd, out replacement));
            o.StartWriter(); o.Fill(16, 0xc3);
            var result = cancelled ? F.CancelDisk(o.Files.State, o.OwnerKey, request.Token) : o.Complete(request.Token, call);
            HostCheck.True(result.Progress == F.DiskProgress.Immediate && result.Value == (cancelled ? -4 : -9) && !result.Worker.IsValid);
            for (int i = 0; i < 4; i++)
            { var held = o.Pump(request.Token); HostCheck.True(held.Progress == F.DiskProgress.Rejected && F.Audit(o.Files.State)); }
            o.WriterReady(); HostCheck.Equal(F.DiskProgress.Rejected, o.Observe(request.Token).Progress); o.ConsumeWriter();
            HostCheck.True(o.Observe(request.Token).Progress == F.DiskProgress.Rejected && F.IsCurrent(o.Files.State, replacement) && o.Unchanged(16, 0xc3) && F.Audit(o.Files.State));
            HostCheck.Equal(0L, o.Core(3, fd));
        }
    });

    [HostTest("borrowed linux disk full-tree query and attachment reject original backend aliases and preserve ordinary user futex backing", HostLane.Native)]
    private static void TreeAliases() => WithOwner(o =>
    {
        byte[] before = new ReadOnlySpan<byte>(o.Storage.EngineInput.Before, 65536).ToArray();
        var admission = o.Admission; admission.Requests = (F.DiskRequest*)o.Storage.EngineInput.Before;
        HostCheck.True(!F.AttachDisk(o.Files.State, o.Disk, admission) && before.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(o.Storage.EngineInput.Before, 65536)));
        ref F.Admission global = ref GlobalInput(ref *o.Files.State); byte* scratch = global.Scratch;
        try { global.Scratch = o.Storage.EngineInput.Before; HostCheck.True(!F.AttachDisk(o.Files.State, o.Disk, o.Admission)); }
        finally { global.Scratch = scratch; }
        HostCheck.True(o.AttachReadOnly());
        foreach ((ulong address, ulong bytes) in new[] { ((ulong)o.Disk, (ulong)sizeof(F.DiskState)), ((ulong)o.Requests, 8UL * (ulong)sizeof(F.DiskRequest)),
            ((ulong)o.Storage.Mount, (ulong)sizeof(Fs.Mount)), ((ulong)o.Storage.Port, (ulong)sizeof(P.State)), ((ulong)o.Storage.Engine, (ulong)sizeof(C.State)),
            ((ulong)o.Storage.EngineInput.Before, 65536UL), ((ulong)o.Storage.Device.Data, 4096UL), ((ulong)o.Storage.Device.Observation, (ulong)sizeof(B.Observation)) })
            HostCheck.True(!CanRetain(o.Files.State, address, bytes) && !CanRetain(o.Files.State, address - 8, bytes + 16) && !CanRetain(o.Files.State, address + bytes - 1, 2));
        HostCheck.True(!CanRetain(o.Files.State, ulong.MaxValue - 7, 64) && CanRetain(o.Files.State, (ulong)o.Thread.At(Output), 4));
        o.StartWriter(); HostCheck.True(CanRetain(o.Files.State, (ulong)o.Thread.At(Output), 4)); o.WriterReady(); o.ConsumeWriter();
        uint fd = o.Open(); var call = o.Call(17, fd, Output, 7, 0); var read = o.Begin(call); HostCheck.Equal(F.DiskProgress.Ready, o.Ready(read.Token).Progress); o.Fill(16, 0xd4);
        ref B.Admission input = ref BlockInput(ref *o.Storage.Block); B.Observation* original = input.Observation;
        try
        {
            input.Observation = (B.Observation*)o.Requests;
            HostCheck.True(!CanRetain(o.Files.State, (ulong)o.Thread.At(Output), 4));
            // The frame was captured while the original graph was valid. The
            // active thread image gate correctly rejects this malformed graph.
            HostCheck.Equal(F.DiskProgress.Rejected, o.Complete(read.Token, call).Progress); HostCheck.True(o.Unchanged(16, 0xd4));
        }
        finally { input.Observation = original; }
        HostCheck.Equal(7L, o.Complete(read.Token, call).Value);
        Console.WriteLine($"BORROWED_DISK_LAYOUT request={sizeof(F.DiskRequest)} state={sizeof(F.DiskState)} binding={sizeof(F.DiskBinding)}");
    }, attach: false);

    [HostTest("borrowed linux disk pending physical IN cancellation retains a genuine orphan until completion and never affects numeric replacement", HostLane.Native)]
    private static void PendingOrphan() => WithOwner(o =>
    {
        uint fd = o.Open(); var call = o.Call(17, fd, Output, 7, 0); var request = o.Begin(call);
        for (int i = 0; i < 100 && !o.Storage.Port->RequestActive; i++) HostCheck.Equal(F.DiskProgress.Pending, o.Pump(request.Token).Progress);
        HostCheck.True(o.Storage.Port->RequestActive && o.Storage.Device.Hardware->Pending);
        C.RequestToken writer = default; HostCheck.Equal(C.Status.Busy, C.BeginFlush(o.Storage.Engine, &writer));
        HostCheck.Equal(0L, o.Core(3, fd)); o.PutPath("/app/fixture.data"); HostCheck.Equal(fd, (uint)o.Core(2, Path)); o.Fill(16, 0xe5);
        F.Token replacement = default; HostCheck.True(F.Capture(o.Files.State, fd, out replacement));
        var cancelled = F.CancelDisk(o.Files.State, o.OwnerKey, request.Token);
        HostCheck.True(cancelled.Progress == F.DiskProgress.Draining && cancelled.Value == -4 && !cancelled.Worker.IsValid && o.Storage.Port->RequestActive && F.Audit(o.Files.State));
        HostCheck.Equal(F.DiskProgress.Rejected, o.Complete(request.Token, call).Progress); HostCheck.True(o.Unchanged(16, 0xe5));
        for (int i = 0; i < 32 && o.Observe(request.Token).Progress != F.DiskProgress.Rejected; i++) o.Pump(request.Token);
        HostCheck.True(o.Observe(request.Token).Progress == F.DiskProgress.Rejected && !o.Storage.Port->RequestActive && F.IsCurrent(o.Files.State, replacement) && o.Unchanged(16, 0xe5));
    });

    [HostTest("borrowed linux disk last close succeeds locally during writer Busy and numeric reuse preserves original writer", HostLane.Native)]
    private static void LastCloseBoundary() => WithOwner(o =>
    {
        uint fd = o.Open(); F.Token original = default; HostCheck.True(F.Capture(o.Files.State, fd, out original)); o.StartWriter();
        uint calls = o.Storage.Device.Hardware->Calls; ulong submitted = o.Storage.Block->Submitted;
        HostCheck.Equal(0L, o.Core(3, fd)); HostCheck.True(!F.IsCurrent(o.Files.State, original));
        o.PutPath("/app/fixture.data"); HostCheck.Equal(fd, (uint)o.Core(2, Path)); HostCheck.Equal(0L, o.Core(3, fd));
        HostCheck.True(o.Storage.Engine->RequestActive && o.Storage.Device.Hardware->Calls == calls && o.Storage.Block->Submitted == submitted && F.Audit(o.Files.State));
        o.WriterReady(); o.ConsumeWriter(); uint reopened = o.Open(); HostCheck.Equal(fd, reopened); HostCheck.True(!F.IsCurrent(o.Files.State, original));
    });

    [HostTest("paired global disks attach in both orders and keep one shared fd namespace with independent real read cursors", HostLane.Native)]
    private static void PairedNamespace() => CombinedWebDataImageFixtureTests.WithImage((_, _, _, _, path, receipt) =>
    {
        foreach (bool writeFirst in new[] { false, true })
        {
            using var o = new Owner(File.ReadAllBytes(path), receipt, false); o.PrepareWritable(); o.AttachPair(writeFirst);
            uint read = o.Open(), write = o.WOpen(), independent = o.Open();
            HostCheck.True(read != write && read != independent && write != independent && F.Audit(o.Files.State));
            HostCheck.Equal(7L, o.Run(o.Call(0, read, Output, 7))); HostCheck.True(new ReadOnlySpan<byte>(o.Thread.At(Output), 7).SequenceEqual(o.Expected("/app/wwwroot/index.html").AsSpan(0, 7)));
            HostCheck.True(o.Position(read) == 7 && o.Position(independent) == 0 && o.Position(write) == 0);
            HostCheck.Equal(9L, o.WRun(o.Call(0, write, Output, 9))); HostCheck.True(new ReadOnlySpan<byte>(o.Thread.At(Output), 9).SequenceEqual(o.DataExpected("FRAG.BIN").AsSpan(0, 9)));
            HostCheck.True(o.Position(write) == 9 && o.Position(read) == 7 && o.Position(independent) == 0 && F.Audit(o.Files.State));
            uint callbacks = o.Storage.Device.Hardware->Calls; ulong submitted = o.Storage.Block->Submitted;
            byte outside = 0; HostCheck.True(TransportOutside(o.Files.State, &outside, 1));
            HostCheck.True(callbacks == o.Storage.Device.Hardware->Calls && submitted == o.Storage.Block->Submitted);
        }
    });

    [HostTest("paired global writer retains copied bytes and one request behind genuine pending and held web IN", HostLane.Native)]
    private static void PairedQueuedWrite() => CombinedWebDataImageFixtureTests.WithImage((_, _, _, _, path, receipt) =>
    {
        foreach (bool held in new[] { false, true })
        {
            using var o = new Owner(File.ReadAllBytes(path), receipt, false); o.PrepareWritable(); o.AttachPair(held);
            uint read = o.Open(), write = o.WOpen(); var readCall = o.Call(0, read, Output, 7); var reading = o.Begin(readCall); o.WaitPort(reading.Token);
            if (held) o.Storage.HoldPort(); byte[] expected = "copied pair bytes"u8.ToArray(); var writeCall = o.WriteCall(write, expected, 13); var writing = o.WBegin(writeCall);
            HostCheck.Equal(F.WritableDiskProgress.Pending, writing.Progress); new Span<byte>(o.Thread.At(Payload), expected.Length).Fill(0xec);
            uint callbacks = o.Storage.Device.Hardware->Calls; ulong submitted = o.Storage.Block->Submitted, completed = o.Storage.Block->Completed;
            byte[] observation = new ReadOnlySpan<byte>(o.Storage.Device.Observation, sizeof(B.Observation)).ToArray();
            for (int i = 0; i < 8; i++)
            {
                var blocked = o.WPump(); HostCheck.True(blocked.Progress == F.WritableDiskProgress.Pending && F.Audit(o.Files.State));
                HostCheck.True(callbacks == o.Storage.Device.Hardware->Calls && submitted == o.Storage.Block->Submitted && completed == o.Storage.Block->Completed &&
                    observation.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(o.Storage.Device.Observation, sizeof(B.Observation))));
            }
            HostCheck.Equal(F.DiskProgress.Ready, o.Ready(reading.Token).Progress); HostCheck.Equal(7L, o.Complete(reading.Token, readCall).Value);
            HostCheck.Equal(F.WritableDiskProgress.Ready, o.WReady(writing.Token).Progress); HostCheck.Equal(expected.Length, (int)o.WComplete(writing.Token, writeCall).Value);
            HostCheck.Equal(expected.Length, (int)o.WRun(o.Call(17, write, Output, (ulong)expected.Length, 13)));
            HostCheck.True(new ReadOnlySpan<byte>(o.Thread.At(Output), expected.Length).SequenceEqual(expected) && o.Position(write) == 0 && o.Position(read) == 7 &&
                o.Storage.Block->SubmittedBytes != 0 && o.Storage.Block->DurableBytes == o.Storage.Block->CompletedBytes && F.Audit(o.Files.State));
        }
    });

    [HostTest("paired global web Ready remains typed and copy-free during dirty write FLUSH and held engine result", HostLane.Native)]
    private static void PairedReadyRead() => WithOwner(o =>
    {
        o.PrepareWritable(); HostCheck.True(o.AttachWritable()); uint read = o.Open(), write = o.WOpen();
        var readCall = o.Call(0, read, Output, 7); var reading = o.Begin(readCall); HostCheck.Equal(F.DiskProgress.Ready, o.Ready(reading.Token).Progress);
        var writeCall = o.WriteCall(write, "held writer"u8, 0); var writing = o.WBegin(writeCall);
        for (int i = 0; i < 100000 && !o.Storage.Engine->Dirty; i++) { o.WPump(); o.CompleteDevice(); }
        HostCheck.True(o.Storage.Engine->Dirty); o.Fill(16, 0xad); HostCheck.Equal(F.DiskProgress.Pending, o.Complete(reading.Token, readCall).Progress);
        HostCheck.True(o.Unchanged(16, 0xad) && o.Position(read) == 0 && F.Audit(o.Files.State));
        bool flushed = false;
        for (int i = 0; i < 100000; i++)
        {
            var status = C.Pump(o.Storage.Engine); o.Storage.Sample();
            if (o.Storage.Block->RequestPending && o.Storage.Block->LastOperation == B.Operation.Flush) flushed = true;
            o.CompleteDevice(); if (status == C.Status.Ready) break; HostCheck.Equal(C.Status.Pending, status); HostCheck.True(i < 99999);
        }
        HostCheck.True(flushed && o.Storage.Engine->IsReady && o.Storage.Engine->RequestActive && !o.Storage.Engine->Dirty);
        HostCheck.Equal(F.DiskProgress.Pending, o.Complete(reading.Token, readCall).Progress); HostCheck.True(o.Unchanged(16, 0xad) && o.Position(read) == 0 && F.Audit(o.Files.State));
        HostCheck.Equal(F.WritableDiskProgress.Ready, o.WReady(writing.Token).Progress);
        HostCheck.Equal(7L, o.Complete(reading.Token, readCall).Value); HostCheck.True(o.Position(read) == 7 && o.Position(write) == 0);
        HostCheck.Equal(11L, o.WComplete(writing.Token, writeCall).Value); HostCheck.True(F.Audit(o.Files.State));
    });

    [HostTest("paired global attachment rejects both directions of private aliases before changing admitted bytes or device callbacks", HostLane.Native)]
    private static void PairedAdmissionAliases() => WithOwner(o =>
    {
        o.PrepareWritable(); byte[] before = new ReadOnlySpan<byte>(o.Requests, 8 * sizeof(F.DiskRequest)).ToArray(); uint calls = o.Storage.Device.Hardware->Calls;
        var writable = o.WriteAdmission; writable.Bindings = (F.WritableDiskBinding*)o.Storage.Operations[0].Storage.Range;
        HostCheck.True(!F.AttachWritableDisk(o.Files.State, o.WriteDisk, writable));
        HostCheck.True(!F.AttachWritableDisk(o.Files.State, (F.WritableDiskState*)o.Requests, o.WriteAdmission));
        HostCheck.True(before.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(o.Requests, 8 * sizeof(F.DiskRequest))) && calls == o.Storage.Device.Hardware->Calls && F.Audit(o.Files.State));
        HostCheck.True(F.DetachDisk(o.Files.State)); o.ReadAttached = false; HostCheck.True(o.AttachWritable());
        var readonlyInput = o.Admission; readonlyInput.Requests = (F.DiskRequest*)o.WriteInput;
        HostCheck.True(!F.AttachDisk(o.Files.State, o.Disk, readonlyInput));
        readonlyInput = o.Admission; readonlyInput.Bindings = (F.DiskBinding*)o.WriteBindings;
        HostCheck.True(!F.AttachDisk(o.Files.State, o.Disk, readonlyInput)); HostCheck.True(o.AttachReadOnly() && F.Audit(o.Files.State));
    });

    [HostTest("paired global ongoing audit rejects forged cached engine identity private backing aliases and original epoch changes", HostLane.Native)]
    private static void PairedRetainedAudit() => WithOwner(o =>
    {
        o.PrepareWritable(); HostCheck.True(o.AttachWritable()); uint calls = o.Storage.Device.Hardware->Calls;
        foreach ((ulong address, ulong bytes) in new[] { ((ulong)o.Disk, (ulong)sizeof(F.DiskState)), ((ulong)o.Requests, 8UL * (ulong)sizeof(F.DiskRequest)),
            ((ulong)o.Storage.Port, (ulong)sizeof(P.State)), ((ulong)o.Storage.Operations[0].Storage.Range, (ulong)o.Storage.Operations[0].Storage.RangeCapacity),
            ((ulong)o.WriteDisk, (ulong)sizeof(F.WritableDiskState)), ((ulong)o.WriteBindings, 32UL * (ulong)sizeof(F.WritableDiskBinding)),
            ((ulong)o.Provider, (ulong)sizeof(W.State)), ((ulong)o.WriteInput, (ulong)W.IoBytes) })
            foreach (var query in new[] { CanRetain, WritableCanRetain })
                HostCheck.True(!query(o.Files.State, address, bytes) && !query(o.Files.State, address - 8, bytes + 16) && !query(o.Files.State, address + bytes - 1, 2));
        HostCheck.True(CanRetain(o.Files.State, (ulong)o.Thread.At(Output), 4) && WritableCanRetain(o.Files.State, (ulong)o.Thread.At(Output), 4) &&
            !CanRetain(o.Files.State, ulong.MaxValue - 7, 64) && !WritableCanRetain(o.Files.State, ulong.MaxValue - 7, 64));
        ref P.Inspection binding = ref PortBinding(ref *o.Storage.Mount); ref ulong engineOwner = ref BindingEngine(ref binding); ulong originalEngine = engineOwner;
        try { engineOwner += 8; HostCheck.True(!F.Audit(o.Files.State) && !CanRetain(o.Files.State, (ulong)o.Thread.At(Output), 4)); }
        finally { engineOwner = originalEngine; }
        ref W.Admission provider = ref ProviderInput(ref *o.Provider); byte* originalInput = provider.Input;
        try { provider.Input = o.Storage.Operations[0].Storage.Range; HostCheck.True(!F.Audit(o.Files.State) && !CanRetain(o.Files.State, (ulong)o.Thread.At(Output), 4)); }
        finally { provider.Input = originalInput; }
        ref B.Admission block = ref BlockInput(ref *o.Storage.Block); ulong originalEpoch = block.Epoch;
        try { block.Epoch++; HostCheck.True(!F.Audit(o.Files.State)); }
        finally { block.Epoch = originalEpoch; }
        HostCheck.True(calls == o.Storage.Device.Hardware->Calls && F.Audit(o.Files.State));
    });

    [HostTest("paired global queued write cancellation preserves foreign read ownership and retained original block stop grants cleanup only", HostLane.Native)]
    private static void PairedCancelAndStop() => WithOwner(o =>
    {
        o.PrepareWritable(); HostCheck.True(o.AttachWritable()); uint read = o.Open(), write = o.WOpen();
        var readCall = o.Call(0, read, Output, 7); var reading = o.Begin(readCall); o.WaitPort(reading.Token);
        var writing = o.WBegin(o.WriteCall(write, "cancelled"u8, 0)); uint calls = o.Storage.Device.Hardware->Calls; ulong submitted = o.Storage.Block->Submitted;
        var cancelled = F.CancelWritableDisk(o.Files.State, o.OwnerKey, writing.Token, true);
        // The provider owns an already-cancelled local Ready record until the
        // adapter consumes it. That drain must not poll the foreign read.
        HostCheck.True(cancelled.Progress == F.WritableDiskProgress.Draining && cancelled.Value == -4 && !cancelled.Worker.IsValid &&
            o.Storage.Port->RequestActive && submitted == o.Storage.Block->Submitted && calls == o.Storage.Device.Hardware->Calls && F.Audit(o.Files.State));
        var drained = o.WPump();
        HostCheck.True(drained.Progress == F.WritableDiskProgress.Immediate && drained.Value == -4 && !o.WriteDisk->RequestActive &&
            o.Storage.Port->RequestActive && submitted == o.Storage.Block->Submitted && calls == o.Storage.Device.Hardware->Calls && F.Audit(o.Files.State));
        HostCheck.Equal(F.DiskProgress.Ready, o.Ready(reading.Token).Progress); HostCheck.Equal(7L, o.Complete(reading.Token, readCall).Value);
        HostCheck.Equal(0L, o.Core(3, read)); HostCheck.Equal(0L, o.Core(3, write));
        HostCheck.Equal(B.Status.Success, B.Stop(o.Storage.Block)); HostCheck.True(F.Audit(o.Files.State)); calls = o.Storage.Device.Hardware->Calls;
        o.PutPath("/data/FRAG.BIN"); HostCheck.Equal(-5L, o.WBegin(o.Call(2, Path, 2)).Value);
        o.PutPath("/app/wwwroot/index.html"); HostCheck.Equal(-5L, o.Begin(o.Call(2, Path, 0)).Value);
        HostCheck.True(calls == o.Storage.Device.Hardware->Calls && F.Audit(o.Files.State));
    });

    [HostTest("paired global attachment refuses distinct actual retained engines in both orders without adopting the other device", HostLane.Native)]
    private static void PairedDifferentEngines() => CombinedWebDataImageFixtureTests.WithImage((_, _, _, _, path, receipt) =>
    {
        using var original = new Owner(File.ReadAllBytes(path), receipt, false);
        using var other = new Owner(File.ReadAllBytes(path), receipt, false);
        original.PrepareWritable(); other.PrepareWritable(); HostCheck.True(original.AttachReadOnly());
        byte[] otherDisk = new ReadOnlySpan<byte>(other.WriteDisk, sizeof(F.WritableDiskState)).ToArray();
        uint originalCalls = original.Storage.Device.Hardware->Calls, otherCalls = other.Storage.Device.Hardware->Calls;
        HostCheck.True(!F.AttachWritableDisk(original.Files.State, other.WriteDisk, other.WriteAdmission));
        HostCheck.True(otherDisk.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(other.WriteDisk, sizeof(F.WritableDiskState))));
        HostCheck.True(F.DetachDisk(original.Files.State)); original.ReadAttached = false; HostCheck.True(original.AttachWritable());
        HostCheck.True(!F.AttachDisk(original.Files.State, other.Disk, other.Admission));
        HostCheck.True(originalCalls == original.Storage.Device.Hardware->Calls && otherCalls == other.Storage.Device.Hardware->Calls &&
            F.Audit(original.Files.State) && F.Audit(other.Files.State));
        HostCheck.True(original.AttachReadOnly() && F.Audit(original.Files.State));
    });

    [HostTest("socket paired disks attach transport in either order and share real socket epoll file descriptors without sharing cursors", HostLane.Native)]
    private static void SocketPairOrders() => CombinedWebDataImageFixtureTests.WithImage((_, _, _, _, path, receipt) =>
    {
        foreach (bool transportFirst in new[] { false, true }) foreach (bool writeFirst in new[] { false, true })
        {
            using var o = new Owner(File.ReadAllBytes(path), receipt, false, beforeThreads: owner =>
            {
                owner.PrepareWritable();
                if (transportFirst) owner.PrepareConnectedTransport();
                owner.AttachPair(writeFirst);
                if (!transportFirst) owner.PrepareConnectedTransport();
            });
            uint read = o.Open(), write = o.WOpen(); HostCheck.True(new[] { read, write, o.Listener, o.Connected, o.EpollFd }.Distinct().Count() == 5);
            HostCheck.Equal(7L, o.Run(o.Call(0, read, Output, 7))); HostCheck.Equal(9L, o.WRun(o.Call(0, write, Output, 9)));
            ulong submitted = o.Storage.Block->Submitted; uint callbacks = o.Storage.Device.Hardware->Calls;
            o.SocketRoundtrip(); HostCheck.True(o.Position(read) == 7 && o.Position(write) == 9 && submitted == o.Storage.Block->Submitted &&
                callbacks == o.Storage.Device.Hardware->Calls && F.Audit(o.Files.State));
        }
    });

    [HostTest("socket paired disk traffic and epoll preserve pending and held original web reads while copied writer stays queued", HostLane.Native)]
    private static void SocketPendingRead() => CombinedWebDataImageFixtureTests.WithImage((_, _, _, _, path, receipt) =>
    {
        foreach (bool held in new[] { false, true })
        {
            using var o = new Owner(File.ReadAllBytes(path), receipt, false, beforeThreads: owner =>
            { owner.PrepareWritable(); owner.AttachPair(held); owner.PrepareConnectedTransport(); });
            uint read = o.Open(), write = o.WOpen(); var call = o.Call(0, read, Output, 7); var reading = o.Begin(call); o.WaitPort(reading.Token);
            if (held) o.Storage.HoldPort(); var writeCall = o.WriteCall(write, "queued network"u8, 0); var writing = o.WBegin(writeCall);
            ulong submitted = o.Storage.Block->Submitted, completed = o.Storage.Block->Completed; uint callbacks = o.Storage.Device.Hardware->Calls;
            o.SocketRoundtrip(); HostCheck.Equal(F.WritableDiskProgress.Pending, o.WPump().Progress);
            HostCheck.True(o.Storage.Port->RequestActive && submitted == o.Storage.Block->Submitted && completed == o.Storage.Block->Completed && callbacks == o.Storage.Device.Hardware->Calls &&
                o.Position(read) == 0 && o.Position(write) == 0 && F.Audit(o.Files.State));
            HostCheck.Equal(F.DiskProgress.Ready, o.Ready(reading.Token).Progress); HostCheck.Equal(7L, o.Complete(reading.Token, call).Value);
            HostCheck.Equal(F.WritableDiskProgress.Ready, o.WReady(writing.Token).Progress); HostCheck.Equal(14L, o.WComplete(writing.Token, writeCall).Value);
        }
    });

    [HostTest("socket paired disk readiness and copies stay independent through actual dirty OUT FLUSH and held writer result", HostLane.Native)]
    private static void SocketDirtyWriter() => WithSocketOwner(o =>
    {
        uint read = o.Open(), write = o.WOpen(); var call = o.Call(0, read, Output, 7); var reading = o.Begin(call);
        HostCheck.Equal(F.DiskProgress.Ready, o.Ready(reading.Token).Progress); var writeCall = o.WriteCall(write, "network flush"u8, 0); var writing = o.WBegin(writeCall);
        for (int i = 0; i < 100000 && !o.Storage.Engine->Dirty; i++) { o.WPump(); o.CompleteDevice(); }
        HostCheck.True(o.Storage.Engine->Dirty); o.Fill(16, 0xb8); ulong submitted = o.Storage.Block->Submitted; uint callbacks = o.Storage.Device.Hardware->Calls;
        o.SocketRoundtrip(); HostCheck.True(submitted == o.Storage.Block->Submitted && callbacks == o.Storage.Device.Hardware->Calls && o.Unchanged(16, 0xb8));
        HostCheck.Equal(F.DiskProgress.Pending, o.Complete(reading.Token, call).Progress); bool flush = false;
        for (int i = 0; i < 100000; i++)
        {
            var result = C.Pump(o.Storage.Engine); o.Storage.Sample();
            if (o.Storage.Block->RequestPending && o.Storage.Block->LastOperation == B.Operation.Flush)
            { flush = true; submitted = o.Storage.Block->Submitted; callbacks = o.Storage.Device.Hardware->Calls; o.SocketPoll(); HostCheck.True(submitted == o.Storage.Block->Submitted && callbacks == o.Storage.Device.Hardware->Calls); }
            o.CompleteDevice(); if (result == C.Status.Ready) break; HostCheck.Equal(C.Status.Pending, result); HostCheck.True(i < 99999);
        }
        HostCheck.True(flush && o.Storage.Engine->IsReady && o.Storage.Engine->RequestActive); o.SocketPoll();
        HostCheck.Equal(F.DiskProgress.Pending, o.Complete(reading.Token, call).Progress); HostCheck.True(o.Unchanged(16, 0xb8) && o.Position(read) == 0);
        HostCheck.Equal(F.WritableDiskProgress.Ready, o.WReady(writing.Token).Progress); HostCheck.Equal(7L, o.Complete(reading.Token, call).Value);
        HostCheck.Equal(13L, o.WComplete(writing.Token, writeCall).Value); HostCheck.True(o.Position(read) == 7 && o.Position(write) == 0 && F.Audit(o.Files.State));
    });

    [HostTest("socket paired disk full retained inventory rejects aliases and changed transport context without callbacks or output", HostLane.Native)]
    private static void SocketAliases() => WithSocketOwner(o =>
    {
        var connection = o.Connection!; uint callbacks = o.Storage.Device.Hardware->Calls, networkCalls = connection.Hardware->Calls;
        foreach ((ulong start, ulong bytes) in o.TransportExtents())
            HostCheck.True(!SocketCanRetain(o.Files.State, start, bytes) && !SocketCanRetain(o.Files.State, start - 8, bytes + 16) && !SocketCanRetain(o.Files.State, start + bytes - 1, 2));
        HostCheck.True(SocketCanRetain(o.Files.State, (ulong)o.Thread.At(SocketOutput), 12) && !SocketCanRetain(o.Files.State, ulong.MaxValue - 7, 64));
        ref byte* buffer = ref SendBuffer(ref *connection.Owner); byte* originalBuffer = buffer;
        new Span<byte>(o.Thread.At(SocketOutput), 16).Fill(0xd8); uint writes = o.Thread.Writes;
        var before = o.SocketAuditSnapshot();
        foreach (ulong alias in new[] { (ulong)o.WriteInput, (ulong)o.Storage.Operations[0].Storage.Range, (ulong)o.Storage.EngineInput.Before,
            (ulong)o.Storage.Device.Data, (ulong)o.Storage.Device.Observation })
        {
            // Deliberately invalid retained graphs are rejected by the global
            // audit. Do not poison an attached process with a syscall merely
            // to turn that rejection into a returned errno.
            try { buffer = (byte*)alias; HostCheck.True(!F.Audit(o.Files.State)); }
            finally { buffer = originalBuffer; }
            CheckSocketAuditSnapshot(before);
            HostCheck.True(F.Audit(o.Files.State) && !o.Thread.Service->Failed && callbacks == o.Storage.Device.Hardware->Calls &&
                networkCalls == connection.Hardware->Calls && writes == o.Thread.Writes);
        }
        ref void* clockContext = ref SendClockContext(ref *connection.Owner); void* originalClock = clockContext;
        try { clockContext = (byte*)connection.Hardware + 8; HostCheck.True(!F.Audit(o.Files.State)); }
        finally { clockContext = originalClock; }
        CheckSocketAuditSnapshot(before);
        HostCheck.True(callbacks == o.Storage.Device.Hardware->Calls && networkCalls == connection.Hardware->Calls && writes == o.Thread.Writes &&
            new ReadOnlySpan<byte>(o.Thread.At(SocketOutput), 16).IndexOfAnyExcept((byte)0xd8) < 0 && F.Audit(o.Files.State) && !o.Thread.Service->Failed);
        o.SocketPoll(); HostCheck.True(F.Audit(o.Files.State) && !o.Thread.Service->Failed);
    });

    private static void CheckSocketAuditSnapshot((ulong Address, byte[] Bytes)[] before)
    { foreach (var range in before) HostCheck.True(range.Bytes.AsSpan().SequenceEqual(new ReadOnlySpan<byte>((void*)range.Address, range.Bytes.Length))); }

    [HostTest("paired thread range batch preserves scalar exclusions malformed bounds and dirty orphan lifetimes without effects")]
    private static void PairedThreadBatch() => CombinedWebDataImageFixtureTests.WithImage((_, _, _, _, path, receipt) =>
    {
        using var o = new Owner(File.ReadAllBytes(path), receipt, false, beforeThreads: p =>
        { p.PrepareWritable(); p.AttachPair(false); p.PrepareConnectedTransport(); });
        string stage = "clean-vectors";
        try
        {
        ulong candidate = (ulong)o.Storage.Output; new Span<byte>((void*)candidate, 128).Fill(0xce);
        ulong[] valid = [candidate, 16, candidate + 32, 32, candidate + 80, 48];
        bool Scalar(ReadOnlySpan<ulong> ranges)
        {
            for (int i = 0; i < ranges.Length; i += 2)
                if (ranges[i + 1] != 0 && (!WritableCanRetain(o.Files.State, ranges[i], ranges[i + 1]) ||
                    !CanRetain(o.Files.State, ranges[i], ranges[i + 1]) || !SocketCanRetain(o.Files.State, ranges[i], ranges[i + 1]))) return false;
            return true;
        }
        void Equivalent(ReadOnlySpan<ulong> ranges, bool expected)
        { HostCheck.Equal(expected, Scalar(ranges)); HostCheck.Equal(expected, PairedBatch(o.Files.State, ranges)); }
        void QueriesOnly(Action queries)
        {
            var before = o.SocketAuditSnapshot(); uint calls = o.Storage.Device.Hardware->Calls, socketCalls = o.Connection!.Hardware->Calls, writes = o.Thread.Writes;
            ulong submitted = o.Storage.Block->Submitted, completed = o.Storage.Block->Completed;
            queries(); CheckSocketAuditSnapshot(before);
            HostCheck.True(calls == o.Storage.Device.Hardware->Calls && socketCalls == o.Connection.Hardware->Calls && writes == o.Thread.Writes &&
                submitted == o.Storage.Block->Submitted && completed == o.Storage.Block->Completed &&
                new ReadOnlySpan<byte>((void*)candidate, 128).IndexOfAnyExcept((byte)0xce) < 0);
        }
        QueriesOnly(() =>
        {
            Equivalent(valid, true);
            var aliases = new[] { (ulong)o.Disk, (ulong)o.Storage.Mount, (ulong)o.Storage.Operations[0].Storage.Range,
                o.Storage.FsExtents().Last().Start, (ulong)o.WriteInput, (ulong)o.Storage.Engine,
                (ulong)o.Storage.EngineInput.Before, (ulong)o.Storage.Block, (ulong)o.Storage.Device.Data,
                (ulong)o.Storage.Device.Observation, (ulong)o.Connection!.Hardware };
            foreach (ulong alias in aliases)
                for (int position = 0; position < 3; position++)
                {
                    ulong[] ranges = [.. valid]; ranges[position * 2] = alias; ranges[position * 2 + 1] = 1;
                    Equivalent(ranges, false);
                }
            Equivalent([0, 0, candidate, 128, ulong.MaxValue, 0], true);
            HostCheck.True(!PairedBatch(o.Files.State, []) && !PairedBatch(o.Files.State, [candidate]) &&
                !PairedBatch(o.Files.State, [0, 1]) && !PairedBatch(o.Files.State, [ulong.MaxValue - 7, 16]));
            int maximumPairs = checked((int)(8 + 9 + 2 * KernelThreadTable.MaximumSlots + 2 * LinuxThreadServices.MaximumThreads));
            ulong[] maximum = new ulong[maximumPairs * 2]; maximum[^2] = candidate; maximum[^1] = 128;
            HostCheck.True(PairedBatch(o.Files.State, maximum) && !PairedBatch(o.Files.State, new ulong[maximum.Length + 2]));
            ref B.Admission block = ref BlockInput(ref *o.Storage.Block); ulong epoch = block.Epoch;
            try { block.Epoch++; Equivalent(valid, false); }
            finally { block.Epoch = epoch; }
            Equivalent(valid, true);
            ref W.Admission provider = ref ProviderInput(ref *o.Provider); byte* input = provider.Input;
            try { provider.Input = o.Storage.Operations[0].Storage.Range; Equivalent(valid, false); }
            finally { provider.Input = input; }
            Equivalent(valid, true);
            ref byte phase = ref MountPhase(ref *o.Storage.Mount); byte originalPhase = phase;
            try { phase = 4; HostCheck.True(o.Storage.Mount->IsRetired); Equivalent(valid, false); }
            finally { phase = originalPhase; }
            Equivalent(valid, true);
        });

        stage = "dirty-submit";
        uint write = o.WOpen(); var call = o.WriteCall(write, "batchio"u8, 13); var writing = o.WBegin(call);
        HostCheck.Equal(F.WritableDiskProgress.Pending, writing.Progress);
        for (int i = 0; i < 100000 && !o.Storage.Engine->Dirty; i++) { o.WPump(); o.CompleteDevice(); }
        stage = "dirty-issued"; HostCheck.True(o.Storage.Engine->Dirty && o.Storage.Block->SubmittedBytes != 0);
        stage = "dirty-query";
        QueriesOnly(() => Equivalent(valid, true));
        HostCheck.Equal(F.WritableDiskProgress.Ready, o.WReady(writing.Token).Progress);
        HostCheck.True(o.Storage.Block->CompletedBytes != 0 && o.Storage.Block->DurableBytes == o.Storage.Block->CompletedBytes && !o.Storage.Engine->Dirty);
        HostCheck.Equal(7L, o.WComplete(writing.Token, call).Value);
        // Re-enter after an actual mutation/revision change; the earlier call
        // must not leave a reusable proof or suppress fresh stale-owner checks.
        stage = "post-mutation-query"; QueriesOnly(() => Equivalent(valid, true));

        stage = "orphan-submit";
        uint read = o.Open(); var readCall = o.Call(0, read, Output, 7); var reading = o.Begin(readCall); o.WaitPort(reading.Token);
        var queued = o.WBegin(o.WriteCall(write, "cancelled"u8, 0));
        var cancelled = F.CancelWritableDisk(o.Files.State, o.OwnerKey, queued.Token, true);
        HostCheck.True(cancelled.Progress == F.WritableDiskProgress.Draining && cancelled.Value == -4 && !cancelled.Worker.IsValid && o.Storage.Port->RequestActive);
        stage = "orphan-query"; QueriesOnly(() => Equivalent(valid, true));
        var drained = o.WPump(); HostCheck.True(drained.Progress == F.WritableDiskProgress.Immediate && drained.Value == -4 && o.Storage.Port->RequestActive);
        HostCheck.Equal(F.DiskProgress.Ready, o.Ready(reading.Token).Progress); HostCheck.Equal(7L, o.Complete(reading.Token, readCall).Value);
        QueriesOnly(() => Equivalent(valid, true)); HostCheck.True(F.Audit(o.Files.State) && !o.Thread.Service->Failed);
        }
        catch (Exception failure)
        {
            Console.WriteLine($"PAIRED_BATCH_BODY_FAILURE stage={stage} dirty={o.Storage.Engine->Dirty} submittedBytes={o.Storage.Block->SubmittedBytes} completedBytes={o.Storage.Block->CompletedBytes} " + failure);
            throw;
        }
    });

    [HostTest("socket paired disk candidate admission rejects every retained disk range and changed lifetime without effects")]
    private static void SocketCandidateDiskRanges() => CombinedWebDataImageFixtureTests.WithImage((_, _, _, _, path, receipt) =>
    {
        using var owner = new Owner(File.ReadAllBytes(path), receipt, false, beforeThreads: o =>
        {
            o.PrepareWritable(); o.AttachPair(false); o.PrepareConnectedTransport();
            byte* candidate = o.Storage.Output; new Span<byte>(candidate, 128).Fill(0xcd);
            HostCheck.True(F.Audit(o.Files.State) && TransportOutside(o.Files.State, candidate, 128));
            var before = o.SocketAuditSnapshot(); uint deviceCalls = o.Storage.Device.Hardware->Calls, socketCalls = o.Connection!.Hardware->Calls;
            ulong submitted = o.Storage.Block->Submitted, completed = o.Storage.Block->Completed;
            List<(ulong Start, ulong Bytes)> local = [];
            for (uint i = 0; i < F.DiskExtentCount; i++)
            {
                F.DiskExtentAt(o.Files.State, i, out ulong start, out ulong bytes);
                if (bytes != 0) local.Add((start, bytes));
            }
            // Independently require every actual FS allocation, not merely
            // whichever ranges the scalar adapter happened to enumerate.
            foreach (var range in o.Storage.FsExtents()) HostCheck.True(local.Contains(range));
            HostCheck.True(local.Contains(((ulong)o.Disk, (ulong)sizeof(F.DiskState))) &&
                local.Contains(((ulong)o.Requests, F.DiskSlots * (ulong)sizeof(F.DiskRequest))) &&
                local.Contains(((ulong)o.Storage.Port, (ulong)sizeof(P.State))));
            var engine = o.Storage.EngineInput; var device = o.Storage.Device.Input;
            List<(ulong Start, ulong Bytes)> shared =
            [
                ((ulong)o.Storage.Engine, (ulong)sizeof(C.State)), ((ulong)engine.Changes, o.Storage.EngineBackingBytes),
                ((ulong)o.Storage.Block, (ulong)sizeof(B.State)), ((ulong)device.Pool, (ulong)sizeof(OS.Drivers.DmaBufferPool)),
                ((ulong)o.Storage.Device.Slot, (ulong)sizeof(OS.Drivers.DmaBufferSlot)),
                ((ulong)o.Storage.Device.Data, 4096),
                ((ulong)device.Observation, (ulong)sizeof(B.Observation)),
                (device.Common.VirtualStart, device.Common.Bytes), (device.Notify.VirtualStart, device.Notify.Bytes),
                (device.Device.VirtualStart, device.Device.Bytes)
            ];
            foreach ((ulong start, ulong bytes) in local.Concat(shared))
                HostCheck.True(!TransportOutside(o.Files.State, (void*)start, bytes) &&
                    !TransportOutside(o.Files.State, (void*)(start - 8), bytes + 16) &&
                    !TransportOutside(o.Files.State, (void*)(start + bytes - 1), 2));

            ref var input = ref MountAdmission(ref *o.Storage.Mount); var originalInput = input;
            try { input.Epoch++; HostCheck.True(!TransportOutside(o.Files.State, candidate, 128)); }
            finally { input = originalInput; }
            CheckSocketAuditSnapshot(before);
            try { input.Operations = o.Storage.Operations + 1; HostCheck.True(!TransportOutside(o.Files.State, candidate, 128)); }
            finally { input = originalInput; }
            CheckSocketAuditSnapshot(before);
            ref byte phase = ref MountPhase(ref *o.Storage.Mount); byte originalPhase = phase;
            try { phase = 4; HostCheck.True(o.Storage.Mount->IsRetired && !TransportOutside(o.Files.State, candidate, 128)); }
            finally { phase = originalPhase; }
            CheckSocketAuditSnapshot(before);
            HostCheck.True(TransportOutside(o.Files.State, candidate, 128) && F.Audit(o.Files.State) &&
                new ReadOnlySpan<byte>(candidate, 128).IndexOfAnyExcept((byte)0xcd) < 0 &&
                deviceCalls == o.Storage.Device.Hardware->Calls && socketCalls == o.Connection.Hardware->Calls &&
                submitted == o.Storage.Block->Submitted && completed == o.Storage.Block->Completed && !o.Storage.Block->RequestPending);
        });
        HostCheck.True(F.Audit(owner.Files.State));
    });

    [HostTest("socket paired disk admission rejects socket and epoll metadata aliases into either filesystem and original block backing", HostLane.Native)]
    private static void SocketAdmissionAliases() => CombinedWebDataImageFixtureTests.WithImage((_, _, _, _, path, receipt) =>
    {
        using var owner = new Owner(File.ReadAllBytes(path), receipt, false, beforeThreads: o =>
        {
            o.PrepareWritable(); HostCheck.True(o.AttachReadOnly());
            // Authenticate the original C/B exclusion even before W publishes.
            o.CheckSocketAliases((ulong)o.Storage.EngineInput.Before);
            HostCheck.True(o.AttachWritable());
            foreach (ulong alias in new[] { (ulong)o.WriteInput, (ulong)o.Storage.Operations[0].Storage.Range, (ulong)o.Storage.EngineInput.Before,
                (ulong)o.Storage.Device.Data, (ulong)o.Storage.Device.Observation }) o.CheckSocketAliases(alias);
            o.PrepareTransport(beforeEpoll: current => current.CheckEpollAliases((ulong)current.Storage.EngineInput.Before));
        });
        HostCheck.True(F.Audit(owner.Files.State));
    });

    [HostTest("socket paired disk admission refuses unbounded context observer and NIC publication before changing original owners", HostLane.Native)]
    private static void SocketUnsupported() => WithSocketOwner(o =>
    {
        using var next = LinuxSocketServicesTests.Connection(); F.Token token = default; HostCheck.True(F.Capture(o.Files.State, o.Listener, out token));
        uint retained = o.SocketState->RetainedConnections, calls = next.Hardware->Calls;
        HostCheck.True(!Sockets.EnqueueAccepted(o.SocketState, token, o.Listener, next.Receiver, next.Owner));
        HostCheck.True(!Sockets.EnqueueAccepted(o.SocketState, token, o.Listener, next.Receiver, next.Owner, 1));
        HostCheck.True(!Sockets.EnqueueAccepted(o.SocketState, token, o.Listener, next.Receiver, next.Owner, Sockets.MaximumSharedContextBytes + 1));
        HostCheck.True(!Sockets.AttachMessageObserver(o.SocketState, next.Hardware, &ObserveSocket));
        HostCheck.True(calls == next.Hardware->Calls && retained == o.SocketState->RetainedConnections && F.Audit(o.Files.State));
    });

    [HostTest("socket paired disk rejects otherwise eligible NIC owner before publication or time callback", HostLane.Native)]
    private static void SocketNetworkAdmission() => CombinedWebDataImageFixtureTests.WithImage((_, _, _, _, path, receipt) =>
    {
        using var owner = new Owner(File.ReadAllBytes(path), receipt, false, beforeThreads: o =>
        { o.PrepareWritable(); o.AttachPair(true); o.PrepareTransport(); o.CheckNetworkRejected(); });
        HostCheck.True(F.Audit(owner.Files.State));
    });

    [HostTest("bounded packet network and paired disks attach in both orders with actual protocol traffic and independent file cursors", HostLane.Native)]
    private static void PacketOrders() => CombinedWebDataImageFixtureTests.WithImage((_, _, _, _, path, receipt) =>
    {
        foreach (bool networkFirst in new[] { false, true }) foreach (bool writeFirst in new[] { false, true })
        {
            using var o = new Owner(File.ReadAllBytes(path), receipt, false, beforeThreads: owner =>
            {
                owner.PrepareWritable();
                if (networkFirst) { owner.PrepareTransport(); owner.PreparePacketNetwork(); }
                owner.AttachPair(writeFirst);
                if (!networkFirst) { owner.PrepareTransport(); owner.PreparePacketNetwork(); }
            });
            try
            {
                uint read = o.Open(), write = o.WOpen(); HostCheck.True(new[] { read, write, o.Listener, o.Connected, o.EpollFd }.Distinct().Count() == 5);
                HostCheck.Equal(7L, o.Run(o.Call(0, read, Output, 7))); HostCheck.Equal(9L, o.WRun(o.Call(0, write, Output, 9)));
                ulong submitted = o.Storage.Block->Submitted, completed = o.Storage.Block->Completed; uint callbacks = o.Storage.Device.Hardware->Calls;
                o.PacketRoundtrip(); HostCheck.True(o.Position(read) == 7 && o.Position(write) == 9 && submitted == o.Storage.Block->Submitted &&
                    completed == o.Storage.Block->Completed && callbacks == o.Storage.Device.Hardware->Calls && F.Audit(o.Files.State));
            }
            catch (Exception failure) { Console.WriteLine($"PACKET_PAIR_BODY_FAILURE networkFirst={networkFirst} writeFirst={writeFirst} " + failure); throw; }
        }
    });

    [HostTest("bounded packet network leaves original pending and held web IN and queued copied writer untouched", HostLane.Native)]
    private static void PacketPending() => CombinedWebDataImageFixtureTests.WithImage((_, _, _, _, path, receipt) =>
    {
        foreach (bool held in new[] { false, true })
        {
            using var o = new Owner(File.ReadAllBytes(path), receipt, false, beforeThreads: owner =>
            { owner.PrepareWritable(); owner.AttachPair(held); owner.PrepareTransport(); owner.PreparePacketNetwork(); });
            uint read = o.Open(), write = o.WOpen(); var call = o.Call(0, read, Output, 7); var reading = o.Begin(call); o.WaitPort(reading.Token);
            if (held) o.Storage.HoldPort(); var writeCall = o.WriteCall(write, "queued packet"u8, 0); var writing = o.WBegin(writeCall);
            ulong submitted = o.Storage.Block->Submitted, completed = o.Storage.Block->Completed; uint callbacks = o.Storage.Device.Hardware->Calls;
            o.PacketRoundtrip(); HostCheck.Equal(F.WritableDiskProgress.Pending, o.WPump().Progress);
            HostCheck.True(o.Storage.Port->RequestActive && submitted == o.Storage.Block->Submitted && completed == o.Storage.Block->Completed &&
                callbacks == o.Storage.Device.Hardware->Calls && o.Position(read) == 0 && o.Position(write) == 0);
            HostCheck.Equal(F.DiskProgress.Ready, o.Ready(reading.Token).Progress); HostCheck.Equal(7L, o.Complete(reading.Token, call).Value);
            HostCheck.Equal(F.WritableDiskProgress.Ready, o.WReady(writing.Token).Progress); HostCheck.Equal(13L, o.WComplete(writing.Token, writeCall).Value);
        }
    });

    [HostTest("bounded packet network progresses through actual dirty OUT FLUSH and held write result without disk polling", HostLane.Native)]
    private static void PacketDirty() => WithPacketOwner(o =>
    {
        uint read = o.Open(), write = o.WOpen(); var call = o.Call(0, read, Output, 7); var reading = o.Begin(call);
        HostCheck.Equal(F.DiskProgress.Ready, o.Ready(reading.Token).Progress); var writeCall = o.WriteCall(write, "packet dirty"u8, 0); var writing = o.WBegin(writeCall);
        for (int i = 0; i < 100000 && !o.Storage.Engine->Dirty; i++) { o.WPump(); o.CompleteDevice(); }
        HostCheck.True(o.Storage.Engine->Dirty); o.Fill(16, 0xbb); ulong submitted = o.Storage.Block->Submitted; uint callbacks = o.Storage.Device.Hardware->Calls;
        o.PacketRoundtrip(); HostCheck.True(submitted == o.Storage.Block->Submitted && callbacks == o.Storage.Device.Hardware->Calls && o.Unchanged(16, 0xbb));
        HostCheck.Equal(F.DiskProgress.Pending, o.Complete(reading.Token, call).Progress); bool flush = false;
        for (int i = 0; i < 100000; i++)
        {
            var result = C.Pump(o.Storage.Engine); o.Storage.Sample();
            if (o.Storage.Block->RequestPending && o.Storage.Block->LastOperation == B.Operation.Flush)
            {
                flush = true; submitted = o.Storage.Block->Submitted; callbacks = o.Storage.Device.Hardware->Calls;
                HostCheck.Equal(Network.Status.Success, o.PacketNetwork!.Pump()); o.SocketPoll();
                HostCheck.True(submitted == o.Storage.Block->Submitted && callbacks == o.Storage.Device.Hardware->Calls);
            }
            o.CompleteDevice(); if (result == C.Status.Ready) break; HostCheck.Equal(C.Status.Pending, result); HostCheck.True(i < 99999);
        }
        HostCheck.True(flush && o.Storage.Engine->IsReady && o.Storage.Engine->RequestActive);
        HostCheck.Equal(Network.Status.Success, o.PacketNetwork!.Pump()); HostCheck.Equal(F.DiskProgress.Pending, o.Complete(reading.Token, call).Progress);
        HostCheck.True(o.Unchanged(16, 0xbb) && o.Position(read) == 0);
        HostCheck.Equal(F.WritableDiskProgress.Ready, o.WReady(writing.Token).Progress); HostCheck.Equal(7L, o.Complete(reading.Token, call).Value);
        HostCheck.Equal(12L, o.WComplete(writing.Token, writeCall).Value); HostCheck.True(F.Audit(o.Files.State));
    });

    [HostTest("bounded packet network rejects private carrier aliases and retains exact whole network ranges during ongoing audit", HostLane.Native)]
    private static void PacketAliases() => CombinedWebDataImageFixtureTests.WithImage((_, _, _, _, path, receipt) =>
    {
        using var o = new Owner(File.ReadAllBytes(path), receipt, false, beforeThreads: owner =>
        {
            owner.PrepareWritable(); owner.AttachPair(false); owner.PrepareTransport();
            using var candidate = new LinuxSocketNetworkTests.PacketCarrier(owner.SocketState);
            foreach (ulong alias in new[] { (ulong)owner.WriteInput, (ulong)owner.Storage.Operations[0].Storage.Range,
                (ulong)owner.Storage.EngineInput.Before, (ulong)owner.Storage.Device.Data, (ulong)owner.Storage.Device.Observation })
            {
                HostCheck.True(!candidate.Start((void*)alias, 8) && candidate.Clocks == 0 && candidate.Sends == 0 &&
                    new ReadOnlySpan<byte>(candidate.State, sizeof(Network.State)).IndexOfAnyExcept((byte)0) < 0 && F.Audit(owner.Files.State));
            }
            owner.PreparePacketNetwork();
        });
        foreach ((ulong address, ulong bytes) in o.PacketNetwork!.Extents())
            HostCheck.True(!SocketCanRetain(o.Files.State, address, bytes) && !SocketCanRetain(o.Files.State, address - 8, bytes + 16) &&
                !SocketCanRetain(o.Files.State, address + bytes - 1, 2));
        o.Fill(16, 0xdc); var before = o.SocketAuditSnapshot(); uint clocks = o.PacketNetwork.Clocks, sends = o.PacketNetwork.Sends, writes = o.Thread.Writes;
        var originalIo = PacketIo(ref *o.PacketNetwork.State);
        try { PacketIo(ref *o.PacketNetwork.State).Context = o.WriteInput; HostCheck.True(!F.Audit(o.Files.State)); }
        finally { PacketIo(ref *o.PacketNetwork.State) = originalIo; }
        CheckSocketAuditSnapshot(before); HostCheck.True(F.Audit(o.Files.State) && !o.Thread.Service->Failed);
        ref var admission = ref ProviderInput(ref *o.Provider); var originalAdmission = admission;
        try { admission.Input = (byte*)o.PacketNetwork.Context; HostCheck.True(!F.Audit(o.Files.State)); }
        finally { admission = originalAdmission; }
        CheckSocketAuditSnapshot(before); HostCheck.True(clocks == o.PacketNetwork.Clocks && sends == o.PacketNetwork.Sends && writes == o.Thread.Writes &&
            o.Unchanged(16, 0xdc) && F.Audit(o.Files.State) && !o.Thread.Service->Failed); o.PacketRoundtrip();
    });

    [HostTest("bounded packet network real reset and retired original block preserve physical read ownership and permit independent cleanup", HostLane.Native)]
    private static void PacketResetStop() => WithPacketOwner(o =>
    {
        uint read = o.Open(), write = o.WOpen(); var call = o.Call(0, read, Output, 7); var reading = o.Begin(call); o.WaitPort(reading.Token);
        ulong submitted = o.Storage.Block->Submitted; uint callbacks = o.Storage.Device.Hardware->Calls;
        HostCheck.Equal(Network.Status.Success, o.PacketNetwork!.Receive(Wire.Packet(100, ReadOnlySpan<byte>.Empty, o.PacketLocal, flags: 0x14)));
        o.SocketPoll(); HostCheck.Equal(-104L, o.SCall(45, o.Connected, SocketOutput, 7));
        HostCheck.Equal(0L, o.SCall(3, o.Connected)); HostCheck.Equal(Network.Status.Success, o.PacketNetwork.Pump());
        HostCheck.True(o.Storage.Port->RequestActive && submitted == o.Storage.Block->Submitted && callbacks == o.Storage.Device.Hardware->Calls);
        HostCheck.Equal(F.DiskProgress.Ready, o.Ready(reading.Token).Progress); HostCheck.Equal(7L, o.Complete(reading.Token, call).Value);
        HostCheck.Equal(0L, o.Core(3, read)); HostCheck.Equal(0L, o.Core(3, write)); HostCheck.Equal(B.Status.Success, B.Stop(o.Storage.Block));
        HostCheck.True(F.Audit(o.Files.State)); HostCheck.Equal(Network.Status.Success, o.PacketNetwork.Pump());
        o.PutPath("/app/wwwroot/index.html"); HostCheck.Equal(-5L, o.Begin(o.Call(2, Path)).Value);
    });

    private static void WithPacketOwner(Action<Owner> action) => CombinedWebDataImageFixtureTests.WithImage((_, _, _, _, path, receipt) =>
    {
        using var owner = new Owner(File.ReadAllBytes(path), receipt, false, beforeThreads: o =>
        { o.PrepareWritable(); o.AttachPair(false); o.PrepareTransport(); o.PreparePacketNetwork(); });
        try { action(owner); } catch (Exception failure) { Console.WriteLine("PACKET_PAIR_BODY_FAILURE " + failure); throw; }
    });

    [HostTest("socket paired disk complete inventory permits two genuine TCP lifetimes sharing their original table and callback carrier", HostLane.Native)]
    private static void SocketSharedTable() => CombinedWebDataImageFixtureTests.WithImage((_, _, _, _, path, receipt) =>
    {
        using var owner = new Owner(File.ReadAllBytes(path), receipt, false, beforeThreads: o =>
        { o.PrepareWritable(); o.AttachPair(false); o.PrepareTransport(); });
        owner.EnqueueShared(); HostCheck.True(F.Audit(owner.Files.State)); uint read = owner.Open(), write = owner.WOpen();
        HostCheck.Equal(7L, owner.Run(owner.Call(0, read, Output, 7))); HostCheck.Equal(5L, owner.WRun(owner.Call(0, write, Output, 5)));
        HostCheck.True(owner.Position(read) == 7 && owner.Position(write) == 5 && F.Audit(owner.Files.State));
    });

    [HostTest("socket paired disk conditional surface preserves native025 endpoint layout and original five argument enqueue", HostLane.Fast)]
    private static void SocketNativeBoundary()
    {
        string root = HostToolProcess.FindRepositoryRoot(); string source = File.ReadAllText(System.IO.Path.Combine(root, "src/OS.Kernel/Compatibility/LinuxX64/LinuxSocketServices.cs"));
        var legacy = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.CSharp14,
            preprocessorSymbols: ["NOSTDLIB_EFI", "CLROS_LINUX_SOCKETS", "M16_025_PROBE"])).GetRoot();
        var endpoint = legacy.DescendantNodes().OfType<StructDeclarationSyntax>().Single(x => x.Identifier.ValueText == "Endpoint");
        HostCheck.True(!endpoint.DescendantNodes().OfType<VariableDeclaratorSyntax>().Any(x => x.Identifier.ValueText.StartsWith("SharedContext", StringComparison.Ordinal)));
        var enqueue = legacy.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(x => x.Identifier.ValueText == "EnqueueAccepted").ToArray();
        HostCheck.True(enqueue.Length == 1 && enqueue[0].ParameterList.Parameters.Count == 5);
        var current = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.CSharp14)).GetRoot();
        HostCheck.True(current.DescendantNodes().OfType<MethodDeclarationSyntax>().Any(x => x.Identifier.ValueText == "EnqueueAccepted" && x.ParameterList.Parameters.Count == 6));
    }

    [HostTest("socket paired disk reset and closed reap do not steal a web IN and original block stop permits only disk cleanup", HostLane.Native)]
    private static void SocketResetStop() => WithSocketOwner(o =>
    {
        uint read = o.Open(), write = o.WOpen(); var call = o.Call(0, read, Output, 7); var reading = o.Begin(call); o.WaitPort(reading.Token);
        ulong submitted = o.Storage.Block->Submitted; uint callbacks = o.Storage.Device.Hardware->Calls;
        HostCheck.Equal(Tx.Status.ConnectionReset, Tx.ReceiveFrame(o.Connection!.Owner,
            Wire.Packet(100, ReadOnlySpan<byte>.Empty, ack: 700, flags: 0x14), out _));
        o.SocketPoll(); HostCheck.Equal(-104L, o.SCall(45, o.Connected, SocketOutput, 7));
        HostCheck.True(o.Storage.Port->RequestActive && submitted == o.Storage.Block->Submitted && callbacks == o.Storage.Device.Hardware->Calls);
        HostCheck.Equal(0L, o.SCall(3, o.Connected)); HostCheck.True(Sockets.ReapClosed(o.SocketState, o.Connection.Owner));
        HostCheck.Equal(F.DiskProgress.Ready, o.Ready(reading.Token).Progress); HostCheck.Equal(7L, o.Complete(reading.Token, call).Value);
        HostCheck.Equal(0L, o.Core(3, read)); HostCheck.Equal(0L, o.Core(3, write)); HostCheck.Equal(B.Status.Success, B.Stop(o.Storage.Block));
        HostCheck.True(F.Audit(o.Files.State)); o.PutPath("/data/FRAG.BIN"); HostCheck.Equal(-5L, o.WBegin(o.Call(2, Path, 2)).Value);
        o.PutPath("/app/wwwroot/index.html"); HostCheck.Equal(-5L, o.Begin(o.Call(2, Path)).Value); o.SocketPoll(); HostCheck.True(F.Audit(o.Files.State));
    });

    private static void WithSocketOwner(Action<Owner> action) => CombinedWebDataImageFixtureTests.WithImage((_, _, _, _, path, receipt) =>
    {
        using var owner = new Owner(File.ReadAllBytes(path), receipt, false, beforeThreads: o => { o.PrepareWritable(); o.AttachPair(false); o.PrepareConnectedTransport(); });
        try { action(owner); }
        catch (Exception failure) { Console.WriteLine("SOCKET_PAIR_BODY_FAILURE " + failure); throw; }
    });
    [UnmanagedCallersOnly]
    private static byte ObserveSocket(void* context, ulong number, uint fd, ulong header, ulong flags, ulong table, ulong* vectors, uint count, uint total) => 1;
    private struct NetworkProbe { internal ulong Now; internal uint Calls; }
    private static ulong ProbeClock(void* context) { var probe = (NetworkProbe*)context; probe->Calls++; return probe->Now; }
    private static byte ProbeSend(void* context, byte* bytes, uint length) { ((NetworkProbe*)context)->Calls++; return 1; }

    // Explicitly allocated test arena, not a copied kernel image: both handshakes
    // actually establish against this one table before windows are initialized.
    private sealed class SharedTcp : IDisposable
    {
        private readonly byte* arena = (byte*)NativeMemory.AllocZeroed(32768);
        internal readonly TcpSendWindowTests.Fixture Carrier = new(capacity: 8, receiveCapacity: 8, setup: false);
        internal TcpConnectionTable.Owner* Table => (TcpConnectionTable.Owner*)arena;
        internal Rx.Owner* Receiver(uint i) => (Rx.Owner*)(arena + 4096 + i * 512);
        internal Tx.Owner* Sender(uint i) => (Tx.Owner*)(arena + 8192 + i * 512);
        internal SharedTcp()
        {
            HostCheck.True(sizeof(Rx.Owner) <= 512 && sizeof(Tx.Owner) <= 512 && sizeof(TcpPassiveConnection.State) <= 512);
            HostCheck.Equal(TcpConnectionTable.OwnershipStatus.Success, TcpConnectionTable.InitializeOwned(Table, (TcpConnectionTableEntry*)(arena + 256), 2, (ulong*)(arena + 2048), 2, 91));
            TcpListenerBacklogEntry* backlog = stackalloc TcpListenerBacklogEntry[1];
            for (uint i = 0; i < 2; i++)
            {
                var handshake = (TcpPassiveConnection.State*)(arena + 12288 + i * 512); var io = Carrier.Operations(); uint iss = 699 + 100 * i; ushort port = (ushort)(40000 + i);
                var passiveInfo = new TcpPassiveIpv4FramePlannerInfo(new(2, 0, 0, 0, 0, 1), new(0x52, 0x55, 0x0a, 0, 2, 2),
                    new(10, 0, 2, 15), new(10, 0, 2, 2), 8080, port, iss, 8, 51, 0x272, maximumSegmentSize: 1460);
                HostCheck.Equal(TcpPassiveConnection.Status.Success, TcpPassiveConnection.Begin(handshake, Table, passiveInfo,
                    Wire.Packet(99, ReadOnlySpan<byte>.Empty, ack: 0, port: port, flags: 2, window: 32, options: new byte[] { 2, 4, 0, 4 }), new byte[64], io, out _));
                *backlog = default;
                HostCheck.Equal(TcpPassiveConnection.Status.Success, TcpPassiveConnection.ReceiveAck(handshake,
                    Wire.Packet(100, ReadOnlySpan<byte>.Empty, ack: iss + 1, port: port, flags: 0x10), new Span<TcpListenerBacklogEntry>(backlog, 1), out _, out _));
                var info = new TcpEstablishedStreamIpv4FramePlannerInfo(new(2, 0, 0, 0, 0, 1), new(0x52, 0x55, 0x0a, 0, 2, 2),
                    new(10, 0, 2, 15), new(10, 0, 2, 2), 1460, 8, 51, 0x272);
                HostCheck.Equal(Rx.Status.Success, Rx.Initialize(Receiver(i), Table, handshake->Connection, arena + 16384 + i * 2048, 8, info, io.Context, io.Send));
                HostCheck.Equal(Tx.Status.Success, Tx.Initialize(Sender(i), Receiver(i), handshake, arena + 24576 + i * 2048, 8, 1500));
                HostCheck.Equal(Tx.Status.Success, Tx.ConfigureRetransmission(Sender(i), io.Context, io.NowNanoseconds, 1000000000000));
                HostCheck.Equal(Tx.Status.Success, Tx.ConfigureClose(Sender(i), 30000000000));
            }
        }
        public void Dispose() { NativeMemory.Free(arena); Carrier.Dispose(); }
    }

    private static void WithOwner(Action<Owner> action, bool attach = true) => CombinedWebDataImageFixtureTests.WithImage((_, _, _, _, path, receipt) =>
    {
        using var owner = new Owner(File.ReadAllBytes(path), receipt, attach);
        try { action(owner); } catch (Exception failure) { Console.WriteLine("BORROWED_OWNER_BODY_FAILURE " + failure); throw; }
    });
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "Input")]
    private static extern ref B.Admission BlockInput(ref B.State state);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "Description")]
    private static extern ref uint SlotDescription(ref F.Slot slot);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "Phase")]
    private static extern ref byte RequestPhase(ref F.DiskRequest request);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "Position")]
    private static extern ref ulong DescriptionPosition(ref F.Description description);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "Owner")]
    private static extern ref F.Admission GlobalInput(ref F.State state);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "PortBinding")]
    private static extern ref P.Inspection PortBinding(ref Fs.Mount mount);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "Input")]
    private static extern ref Fs.Admission MountAdmission(ref Fs.Mount mount);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "Phase")]
    private static extern ref byte MountPhase(ref Fs.Mount mount);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "EngineOwner")]
    private static extern ref ulong BindingEngine(ref P.Inspection binding);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "Input")]
    private static extern ref W.Admission ProviderInput(ref W.State state);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "Buffer")]
    private static extern ref byte* SendBuffer(ref Tx.Owner owner);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "Buffer")]
    private static extern ref byte* ReceiveBuffer(ref Rx.Owner owner);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "ClockContext")]
    private static extern ref void* SendClockContext(ref Tx.Owner owner);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "Io")]
    private static extern ref TcpPassiveConnection.Operations PacketIo(ref Network.State state);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "Entries")]
    private static extern ref TcpConnectionTableEntry* TableEntries(ref TcpConnectionTable.Owner owner);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "Generations")]
    private static extern ref ulong* TableGenerations(ref TcpConnectionTable.Owner owner);
    private delegate bool Outside(F.State* state, void* pointer, ulong bytes);
    private static readonly Outside TransportOutside = typeof(F).GetMethod("SocketOutside", BindingFlags.Static | BindingFlags.NonPublic)!.CreateDelegate<Outside>();

    private sealed class Owner : IDisposable
    {
        internal readonly LinuxThreadServicesTests.Model Thread = new(futex: true);
        internal readonly LinuxDescriptorServicesTests.Owner Files = new(descriptors: 32, descriptions: 32);
        internal readonly ReadOnlyWebRootWritableBlockTests.Fixture Storage;
        internal readonly F.DiskState* Disk = (F.DiskState*)NativeMemory.AllocZeroed((nuint)sizeof(F.DiskState));
        private readonly F.DiskBinding* bindings = (F.DiskBinding*)NativeMemory.AllocZeroed(8, (nuint)sizeof(F.DiskBinding));
        internal readonly F.DiskRequest* Requests = (F.DiskRequest*)NativeMemory.AllocZeroed(8, (nuint)sizeof(F.DiskRequest));
        private readonly OS.Tooling.CombinedWebDataImageFixture.Receipt image;
        private readonly List<F.DiskToken> tokens = [];
        private readonly OSHostHandle worker;
        internal OSHostHandle Worker => worker;
        private bool threadsAttached;
        private const ulong SignalKey = 0x6f75746572736967;
        private Signals.State* signalState;
        private uint signalBytes;
        private C.RequestToken writer;
        internal W.State* Provider;
        private W.Descriptor* writeFds;
        private W.Description* writeDescriptions;
        internal byte* WriteInput;
        private W.ProcessSlot* processes;
        private W.RangeLock* locks, lockScratch;
        internal F.WritableDiskState* WriteDisk;
        internal F.WritableDiskBinding* WriteBindings;
        private readonly List<F.WritableDiskToken> writeTokens = [];
        internal Sockets.State* SocketState;
        private Sockets.Endpoint* endpoints;
        private Epoll.State* epollState;
        private Epoll.Port* ports;
        private Epoll.Registration* registrations;
        internal uint Listener, Connected, EpollFd;
        internal TcpSendWindowTests.Fixture? Connection;
        internal LinuxSocketNetworkTests.PacketCarrier? PacketNetwork;
        internal uint PacketLocal;
        private readonly List<TcpSendWindowTests.Fixture> connections = [];
        private readonly List<SharedTcp> sharedConnections = [];
        internal bool ReadAttached, WriteAttached;
        internal ulong OwnerKey => (ulong)Thread.Service;
        internal F.DiskAdmission Admission => new() { Mount = Storage.Mount, Bindings = bindings, Requests = Requests,
            Epoch = Storage.Input.Epoch, FirmwareExited = true, SingleOwnerValidated = true };
        internal Owner(byte[] raw, OS.Tooling.CombinedWebDataImageFixture.Receipt receipt, bool attach, Action<Owner>? beforeThreads = null, bool signals = false)
        {
            image = receipt; Storage = new(raw, receipt); Storage.MountReady();
            Files.Admission.User = Thread.DescriptorAccess; Files.Admission.CopyToUser = Thread.UserCopyOut; Files.Begin();
            if (attach) HostCheck.True(AttachReadOnly());
            beforeThreads?.Invoke(this);
            if (signals)
            {
                signalBytes = checked(((uint)sizeof(Signals.State) + 4095U) & ~4095U);
                signalState = (Signals.State*)NativeMemory.AlignedAlloc(signalBytes, 4096);
                new Span<byte>(signalState, checked((int)signalBytes)).Clear();
                HostCheck.True(Signals.Initialize(signalState, signalBytes, SignalKey, 2, new()
                { User = Thread.Access, CopyToUser = Thread.UserCopyOut, Capacity = Thread.Capacity, FirmwareExited = true, SingleOwnerValidated = true }));
                HostCheck.True(Thread.Service->EnableSignals(Key, 2, signalState));
            }
            HostCheck.True(Thread.Service->EnablePipes(Key, 2, Files.State));
            threadsAttached = true;
            worker = Thread.Start(); HostCheck.Equal(worker.Value, Thread.Kernel.Dispatch().Value);
        }
        internal void PrepareTransport(Action<Owner>? beforeEpoll = null)
        {
            HostCheck.True(SocketState == null); SocketState = (Sockets.State*)NativeMemory.AllocZeroed((nuint)sizeof(Sockets.State));
            endpoints = (Sockets.Endpoint*)NativeMemory.AllocZeroed(8, (nuint)sizeof(Sockets.Endpoint));
            epollState = (Epoll.State*)NativeMemory.AllocZeroed((nuint)sizeof(Epoll.State)); ports = (Epoll.Port*)NativeMemory.AllocZeroed(2, (nuint)sizeof(Epoll.Port));
            registrations = (Epoll.Registration*)NativeMemory.AllocZeroed(16, (nuint)sizeof(Epoll.Registration));
            HostCheck.True(Sockets.Begin(SocketState, Files.State, endpoints, 8, new(10, 0, 2, 15), 8080));
            beforeEpoll?.Invoke(this);
            HostCheck.True(Epoll.Begin(epollState, Files.State, ports, 2, registrations, 16));
            Listener = checked((uint)SCall(41, 2, 1 | F.NonBlock, 6)); Thread.Put64(SocketData, 0x901f0002); Thread.Put64(SocketData + 8, 0);
            HostCheck.Equal(0L, SCall(49, Listener, SocketData, 16)); HostCheck.Equal(0L, SCall(50, Listener, 4));
            EpollFd = checked((uint)SCall(291, F.CloseOnExec));
        }
        internal void PrepareConnectedTransport()
        {
            PrepareTransport(); var connection = LinuxSocketServicesTests.Connection(); connections.Add(connection); Connection = connection;
            F.Token token = default; HostCheck.True(F.Capture(Files.State, Listener, out token));
            HostCheck.True(Sockets.EnqueueAccepted(SocketState, token, Listener, connection.Receiver, connection.Owner, (uint)sizeof(TcpSendWindowTests.Hardware)));
            Connected = checked((uint)SCall(288, Listener, 0, 0, F.NonBlock));
            Thread.Put32(SocketEvent, 1 | 4 | 8 | 16); Thread.Put64(SocketEvent + 4, 0x1234567890abcdef);
            HostCheck.Equal(0L, SCall(233, EpollFd, 1, Connected, SocketEvent));
        }
        internal void Adopt(TcpSendWindowTests.Fixture connection) => connections.Add(connection);
        internal void PreparePacketNetwork()
        {
            HostCheck.True(PacketNetwork == null && Connection == null && SocketState != null);
            PacketNetwork = new(SocketState); HostCheck.True(PacketNetwork.Start()); PacketLocal = PacketNetwork.Connect();
            Connected = checked((uint)SCall(288, Listener, 0, 0, F.NonBlock));
            Thread.Put32(SocketEvent, 1 | 4 | 8 | 16); Thread.Put64(SocketEvent + 4, 0x1234567890abcdef);
            HostCheck.Equal(0L, SCall(233, EpollFd, 1, Connected, SocketEvent));
        }
        internal void EnqueueShared()
        {
            var shared = new SharedTcp(); sharedConnections.Add(shared); F.Token token = default; HostCheck.True(F.Capture(Files.State, Listener, out token));
            for (uint i = 0; i < 2; i++)
            {
                HostCheck.True(Sockets.EnqueueAccepted(SocketState, token, Listener, shared.Receiver(i), shared.Sender(i), (uint)sizeof(TcpSendWindowTests.Hardware)));
                HostCheck.True(SCall(288, Listener, 0, 0, F.NonBlock) >= 3);
            }
            HostCheck.Equal(2, shared.Table->Count);
        }
        internal long SCall(ulong number, ulong a0 = 0, ulong a1 = 0, ulong a2 = 0, ulong a3 = 0)
        {
            if (!threadsAttached) return F.Dispatch(Files.State,
                new() { Number = number, Argument0 = a0, Argument1 = a1, Argument2 = a2, Argument3 = a3 });
            var result = Thread.DescriptorCall(number, a0, a1, a2, a3);
            HostCheck.Equal(LinuxSyscallAction.Return, result.Action); return result.Value;
        }
        internal void SocketPoll()
        { HostCheck.True(Epoll.Refresh(epollState)); HostCheck.True(SCall(281, EpollFd, SocketOutput, 1, 0) >= 0 && F.Audit(Files.State)); }
        internal void SocketRoundtrip()
        {
            var connection = Connection!; "net"u8.CopyTo(new Span<byte>(Thread.At(SocketData), 3)); HostCheck.True(F.Audit(Files.State));
            uint reads = Thread.Reads; long sent = SCall(44, Connected, SocketData, 3);
            if (sent != 3) Console.WriteLine($"SOCKET_SEND_FAILURE result={sent} reads={Thread.Reads - reads} global={F.Audit(Files.State)} socket={Sockets.Audit(SocketState, Files.State)} epoll={Epoll.Audit(epollState, Files.State)} disk={AuditDisk(Files.State)} writable={AuditWritable(Files.State)} pair={AuditPair(Files.State)}");
            HostCheck.Equal(3L, sent);
            HostCheck.Equal(Tx.Status.Success, Tx.Pump(connection.Owner, new byte[1514], out var packet)); HostCheck.Equal(3, packet.PayloadBytes);
            HostCheck.Equal(Tx.Status.Success, connection.Ack(703, out int released)); HostCheck.Equal(3, released);
            HostCheck.Equal(Tx.Status.Success, Tx.ReceiveFrame(connection.Owner, Wire.Packet(100, "wire"u8, ack: 703), out _));
            HostCheck.True(Epoll.Refresh(epollState)); HostCheck.Equal(1L, SCall(281, EpollFd, SocketOutput, 1, 0));
            HostCheck.True((Thread.Read32(SocketOutput) & 1) != 0 && Thread.Read64(SocketOutput + 4) == 0x1234567890abcdef);
            HostCheck.Equal(4L, SCall(45, Connected, SocketData, 4)); HostCheck.True(new ReadOnlySpan<byte>(Thread.At(SocketData), 4).SequenceEqual("wire"u8));
        }
        internal void PacketRoundtrip()
        {
            var packet = PacketNetwork!; "nic"u8.CopyTo(new Span<byte>(Thread.At(SocketData), 3));
            HostCheck.Equal(3L, SCall(44, Connected, SocketData, 3)); HostCheck.Equal(Network.Status.Success, packet.Pump());
            HostCheck.Equal(PacketParseStatus.Success, EthernetHeader.TryParse(packet.Sent, out _, out var ipv4));
            HostCheck.Equal(PacketParseStatus.Success, Ipv4Header.TryParse(ipv4, out _, out var tcp));
            HostCheck.Equal(PacketParseStatus.Success, TcpHeader.TryParse(tcp, out _, out _, out var bytes)); HostCheck.True(bytes.SequenceEqual("nic"u8));
            HostCheck.Equal(Network.Status.Success, packet.Receive(Wire.Packet(100, ReadOnlySpan<byte>.Empty, PacketLocal + 3, flags: 0x10)));
            HostCheck.Equal(Network.Status.Success, packet.Receive(Wire.Packet(100, "peer"u8, PacketLocal + 3, flags: 0x18)));
            SocketPoll(); HostCheck.True((Thread.Read32(SocketOutput) & 1) != 0 && Thread.Read64(SocketOutput + 4) == 0x1234567890abcdef);
            HostCheck.Equal(4L, SCall(45, Connected, SocketData, 4)); HostCheck.True(new ReadOnlySpan<byte>(Thread.At(SocketData), 4).SequenceEqual("peer"u8));
            HostCheck.Equal(Network.Status.Success, packet.Pump()); PacketLocal += 3;
        }
        internal IEnumerable<(ulong, ulong)> TransportExtents()
        {
            if (PacketNetwork != null) return new[] { ((ulong)SocketState, (ulong)sizeof(Sockets.State)), ((ulong)endpoints, 8UL * (ulong)sizeof(Sockets.Endpoint)),
                ((ulong)epollState, (ulong)sizeof(Epoll.State)), ((ulong)ports, 2UL * (ulong)sizeof(Epoll.Port)),
                ((ulong)registrations, 16UL * (ulong)sizeof(Epoll.Registration)) }.Concat(PacketNetwork.Extents());
            var c = Connection!;
            return new[] { ((ulong)SocketState, (ulong)sizeof(Sockets.State)), ((ulong)endpoints, 8UL * (ulong)sizeof(Sockets.Endpoint)),
                ((ulong)epollState, (ulong)sizeof(Epoll.State)), ((ulong)ports, 2UL * (ulong)sizeof(Epoll.Port)), ((ulong)registrations, 16UL * (ulong)sizeof(Epoll.Registration)),
                ((ulong)c.Owner, (ulong)sizeof(Tx.Owner)), ((ulong)c.Receiver, (ulong)sizeof(Rx.Owner)), ((ulong)c.Buffer, 8UL), ((ulong)c.ReceiveBuffer, 8UL),
                ((ulong)c.Table, (ulong)sizeof(TcpConnectionTable.Owner)), ((ulong)TableEntries(ref *c.Table), (ulong)c.Table->Capacity * (ulong)sizeof(TcpConnectionTableEntry)),
                ((ulong)TableGenerations(ref *c.Table), (ulong)c.Table->Capacity * 8UL), ((ulong)c.Hardware, (ulong)sizeof(TcpSendWindowTests.Hardware)) };
        }
        internal (ulong Address, byte[] Bytes)[] SocketAuditSnapshot()
        {
            // Copied host-test storage, including the actual contiguous engine
            // arena and all declared filesystem ranges. No producer authority.
            var a = Files.Admission;
            List<(ulong Address, ulong Bytes)> ranges = [.. TransportExtents(), .. Storage.FsExtents(),
                ((ulong)Files.State, (ulong)sizeof(F.State)), ((ulong)a.Slots, a.DescriptorCount * (ulong)sizeof(F.Slot)),
                ((ulong)a.Descriptions, a.DescriptionCount * (ulong)sizeof(F.Description)),
                ((ulong)Disk, (ulong)sizeof(F.DiskState)), ((ulong)bindings, 8UL * (ulong)sizeof(F.DiskBinding)), ((ulong)Requests, 8UL * (ulong)sizeof(F.DiskRequest)),
                ((ulong)WriteDisk, (ulong)sizeof(F.WritableDiskState)), ((ulong)WriteBindings, 32UL * (ulong)sizeof(F.WritableDiskBinding)),
                ((ulong)Provider, (ulong)sizeof(W.State)), ((ulong)writeFds, 32UL * (ulong)sizeof(W.Descriptor)),
                ((ulong)writeDescriptions, 32UL * (ulong)sizeof(W.Description)), ((ulong)WriteInput, W.IoBytes),
                ((ulong)processes, 4UL * (ulong)sizeof(W.ProcessSlot)), ((ulong)locks, 32UL * (ulong)sizeof(W.RangeLock)), ((ulong)lockScratch, 65UL * (ulong)sizeof(W.RangeLock)),
                ((ulong)Storage.Port, (ulong)sizeof(P.State)), ((ulong)Storage.Engine, (ulong)sizeof(C.State)),
                ((ulong)Storage.EngineInput.Changes, Storage.EngineBackingBytes), ((ulong)Storage.Block, (ulong)sizeof(B.State)),
                ((ulong)Storage.Device.Observation, (ulong)sizeof(B.Observation)), ((ulong)Storage.Device.Data, 4096),
                ((ulong)Storage.Device.Hardware, (ulong)sizeof(WritableBlockSessionTests.Hardware)),
                ((ulong)Thread.Kernel.Memory, KernelThreadTableTests.Model.Bytes), ((ulong)Thread.Access.Context, Thread.UserContextBytes)];
            if (signalState != null) ranges.Add(((ulong)signalState, signalBytes));
            return ranges.Select(range => (range.Address, new ReadOnlySpan<byte>((void*)range.Address, checked((int)range.Bytes)).ToArray())).ToArray();
        }
        internal IEnumerable<(ulong, ulong)> ProcessExtents()
        {
            List<(ulong, ulong)> ranges = [((ulong)Thread.Service, (ulong)sizeof(LinuxThreadServices)),
                ((ulong)Thread.Slots, Thread.Capacity * (ulong)sizeof(LinuxThreadServices.Slot)),
                ((ulong)Thread.Workers, Thread.Capacity * (ulong)sizeof(LinuxThreadServices.Worker)),
                ((ulong)Thread.Access.Context, Thread.UserContextBytes)];
            var futex = Thread.FutexPlan();
            ranges.Add(((ulong)futex.Cells, futex.CellBytes)); ranges.Add(((ulong)futex.Addresses, futex.AddressBytes));
            ranges.Add(((ulong)futex.Registrations, futex.RegistrationBytes));
            if (signalState != null) ranges.Add(((ulong)signalState, (ulong)sizeof(Signals.State)));
            uint count = Thread.Kernel.Table->RetainedExtentCount(KernelThreadTableTests.Key, 2); HostCheck.True(count != 0);
            for (uint i = 0; i < count; i++)
            {
                HostCheck.Equal(KernelThreadTable.Status.Ok, Thread.Kernel.Table->InspectRetainedExtent(KernelThreadTableTests.Key, 2, i, out ulong start, out ulong bytes));
                if (bytes != 0) ranges.Add((start, bytes));
            }
            for (uint i = 0; i < Thread.Capacity; i++)
            {
                var w = Thread.Workers[i].Initial;
                ranges.Add(((ulong)w.Context, w.ContextBytes)); ranges.Add((w.StackGuardStart, w.StackBytes));
            }
            return ranges;
        }
        internal void CheckNetworkRejected()
        {
            HostCheck.Equal(0U, SocketState->RetainedConnections); Network.State network = default; Network.Slot slot = default;
            TcpConnectionTable.Owner table = default; TcpConnectionTableEntry entry = default; ulong generation = 0; byte* buffers = stackalloc byte[64]; new Span<byte>(buffers, 64).Clear();
            HostCheck.Equal(TcpConnectionTable.OwnershipStatus.Success, TcpConnectionTable.InitializeOwned(&table, &entry, 1, &generation, 1, 73));
            NetworkProbe probe = new() { Now = 1000 };
            var io = new TcpPassiveConnection.Operations { Context = &probe, NowNanoseconds = &ProbeClock, Send = &ProbeSend,
                LifetimeNanoseconds = 10000000000, RetryNanoseconds = 1000000000, MaximumAttempts = 3 };
            HostCheck.True(!Network.Begin(&network, SocketState, &table, &slot, 1, buffers, 32, 32,
                new(2, 0, 0, 0, 0, 1), new(0x52, 0x55, 0x0a, 0, 2, 2), new(10, 0, 2, 2), 700, io, compactTimeWait: true));
            HostCheck.True(probe.Calls == 0 && new ReadOnlySpan<byte>(&network, sizeof(Network.State)).IndexOfAnyExcept((byte)0) < 0 &&
                new ReadOnlySpan<byte>(&slot, sizeof(Network.Slot)).IndexOfAnyExcept((byte)0) < 0 && new ReadOnlySpan<byte>(buffers, 64).IndexOfAnyExcept((byte)0) < 0 && F.Audit(Files.State));
        }
        internal void CheckSocketAliases(ulong alias)
        {
            Sockets.State socket = default; uint callbacks = Storage.Device.Hardware->Calls;
            HostCheck.True(!Sockets.Begin(&socket, Files.State, (Sockets.Endpoint*)alias, 8, new(10, 0, 2, 15), 8080));
            HostCheck.True(new ReadOnlySpan<byte>(&socket, sizeof(Sockets.State)).IndexOfAnyExcept((byte)0) < 0 && callbacks == Storage.Device.Hardware->Calls && F.Audit(Files.State));
        }
        internal void CheckEpollAliases(ulong alias)
        {
            Epoll.State epoll = default; Epoll.Registration* rows = stackalloc Epoll.Registration[2]; new Span<byte>(rows, 2 * sizeof(Epoll.Registration)).Clear();
            uint callbacks = Storage.Device.Hardware->Calls; byte[] before = new ReadOnlySpan<byte>((void*)alias, 16).ToArray();
            HostCheck.True(!Epoll.Begin(&epoll, Files.State, (Epoll.Port*)alias, 2, rows, 2));
            HostCheck.True(before.AsSpan().SequenceEqual(new ReadOnlySpan<byte>((void*)alias, 16)) && callbacks == Storage.Device.Hardware->Calls);
        }
        internal byte[] Expected(string path) => image.Image.WebFiles.Single(x => x.Path == path).CopyBytes();
        internal byte[] DataExpected(string name) => image.Image.Data.Files.Single(x => x.Name == name).CopyBytes();
        internal LinuxSyscallFrame Call(ulong number, ulong a0 = 0, ulong a1 = 0, ulong a2 = 0, ulong a3 = 0, ulong a4 = 0, ulong a5 = 0)
        { var call = Thread.CurrentImage()->Frame; call.Number = number; call.Argument0 = a0; call.Argument1 = a1; call.Argument2 = a2; call.Argument3 = a3; call.Argument4 = a4; call.Argument5 = a5; return call; }
        internal bool AttachReadOnly() { bool result = F.AttachDisk(Files.State, Disk, Admission); ReadAttached |= result; return result; }
        internal F.WritableDiskAdmission WriteAdmission => new() { Provider = Provider, Process = W.PrimaryProcess(Provider), Bindings = WriteBindings,
            BindingCount = 32, Epoch = Storage.EngineInput.Epoch, Search = new(2, 64, 256), FirmwareExited = true, SingleOwnerValidated = true };
        internal void PrepareWritable()
        {
            HostCheck.True(Provider == null); Provider = (W.State*)NativeMemory.AllocZeroed((nuint)sizeof(W.State));
            writeFds = (W.Descriptor*)NativeMemory.AllocZeroed(32, (nuint)sizeof(W.Descriptor)); writeDescriptions = (W.Description*)NativeMemory.AllocZeroed(32, (nuint)sizeof(W.Description));
            WriteInput = (byte*)NativeMemory.AllocZeroed(W.IoBytes); processes = (W.ProcessSlot*)NativeMemory.AllocZeroed(4, (nuint)sizeof(W.ProcessSlot));
            locks = (W.RangeLock*)NativeMemory.AllocZeroed(32, (nuint)sizeof(W.RangeLock)); lockScratch = (W.RangeLock*)NativeMemory.AllocZeroed(65, (nuint)sizeof(W.RangeLock));
            WriteDisk = (F.WritableDiskState*)NativeMemory.AllocZeroed((nuint)sizeof(F.WritableDiskState)); WriteBindings = (F.WritableDiskBinding*)NativeMemory.AllocZeroed(32, (nuint)sizeof(F.WritableDiskBinding));
            HostCheck.True(W.Start(Provider, new() { Engine = Storage.Engine, Epoch = Storage.EngineInput.Epoch, Descriptors = writeFds, Descriptions = writeDescriptions,
                DescriptorCount = 32, DescriptionCount = 32, Input = WriteInput, InputBytes = W.IoBytes, ExclusiveOwnerValidated = true }));
            HostCheck.True(W.EnableLocks(Provider, new() { Processes = processes, ProcessCount = 4, Locks = locks, LockCount = 32, Scratch = lockScratch, ScratchCount = 65, PrimaryPid = 101 }));
        }
        internal bool AttachWritable() { bool result = F.AttachWritableDisk(Files.State, WriteDisk, WriteAdmission); WriteAttached |= result; return result; }
        internal void AttachPair(bool writeFirst)
        { if (writeFirst) { HostCheck.True(AttachWritable()); HostCheck.True(AttachReadOnly()); } else { HostCheck.True(AttachReadOnly()); HostCheck.True(AttachWritable()); } }
        internal F.WritableDiskResult WBegin(LinuxSyscallFrame call)
        { var result = F.BeginWritableDisk(Files.State, OwnerKey, worker, call); if (result.Token.IsActive) writeTokens.Add(result.Token); return result; }
        internal F.WritableDiskResult WPump() { var result = F.PumpWritableDiskOne(Files.State, OwnerKey); Storage.Sample(); return result; }
        internal void CompleteDevice() { if (Storage.Device.Hardware->Pending) Storage.Device.Complete(); Storage.Sample(); }
        internal F.WritableDiskResult WReady(F.WritableDiskToken token)
        {
            for (int i = 0; i < 100000; i++) { var result = WPump(); CompleteDevice(); if (result.Progress is F.WritableDiskProgress.Ready or F.WritableDiskProgress.RecoveryRequired or F.WritableDiskProgress.Rejected) return result; }
            throw new InvalidOperationException("paired_writable_bound");
        }
        internal F.WritableDiskResult WComplete(F.WritableDiskToken token, LinuxSyscallFrame call) => F.CompleteWritableDisk(Files.State, OwnerKey, worker, token, call);
        internal long WRun(LinuxSyscallFrame call)
        {
            var begun = WBegin(call); if (begun.Progress == F.WritableDiskProgress.Immediate) return begun.Value;
            HostCheck.True(begun.Progress is F.WritableDiskProgress.Pending or F.WritableDiskProgress.Ready); HostCheck.Equal(F.WritableDiskProgress.Ready, WReady(begun.Token).Progress);
            var result = WComplete(begun.Token, call); HostCheck.Equal(F.WritableDiskProgress.Immediate, result.Progress); return result.Value;
        }
        internal uint WOpen() { PutPath("/data/FRAG.BIN"); long value = WRun(Call(2, Path, 2)); HostCheck.True(value >= 3); return (uint)value; }
        internal LinuxSyscallFrame WriteCall(uint fd, ReadOnlySpan<byte> bytes, ulong offset)
        { bytes.CopyTo(new Span<byte>(Thread.At(Payload), bytes.Length)); *(ulong*)Thread.At(Vector) = Payload; *(ulong*)(Thread.At(Vector) + 8) = (ulong)bytes.Length; return Call(328, fd, Vector, 1, offset, 0, W.RwfNoAppend); }
        internal void WaitPort(F.DiskToken token)
        { for (int i = 0; i < 10000 && !Storage.Port->RequestActive; i++) HostCheck.Equal(F.DiskProgress.Pending, Pump(token).Progress); HostCheck.True(Storage.Port->RequestActive && Storage.Device.Hardware->Pending); }
        internal void PutPath(string value) { byte[] bytes = Encoding.ASCII.GetBytes(value + '\0'); bytes.CopyTo(new Span<byte>(Thread.At(Path), bytes.Length)); }
        internal F.DiskResult Begin(LinuxSyscallFrame call)
        { var result = F.BeginDisk(Files.State, OwnerKey, worker, call); if (result.Token.IsActive) tokens.Add(result.Token); return result; }
        internal F.DiskResult Observe(F.DiskToken token) => F.PollDisk(Files.State, OwnerKey, token);
        internal F.DiskResult Pump(F.DiskToken token)
        { if (Storage.Device.Hardware->Pending) Storage.Device.Complete(); F.PumpDiskOne(Files.State, OwnerKey); Storage.Sample(); return Observe(token); }
        internal F.DiskResult Ready(F.DiskToken token)
        { for (int i = 0; i < 100000; i++) { var result = Pump(token); if (result.Progress is F.DiskProgress.Ready or F.DiskProgress.Rejected) return result; } throw new InvalidOperationException("borrowed_global_bound"); }
        internal F.DiskResult Complete(F.DiskToken token, LinuxSyscallFrame call, uint ceiling = F.DiskCompletionBytes) => F.CompleteDisk(Files.State, OwnerKey, worker, token, call, ceiling);
        internal long Run(LinuxSyscallFrame call)
        { var begun = Begin(call); if (begun.Progress == F.DiskProgress.Immediate) return begun.Value; HostCheck.Equal(F.DiskProgress.Pending, begun.Progress); HostCheck.Equal(F.DiskProgress.Ready, Ready(begun.Token).Progress); var result = Complete(begun.Token, call); HostCheck.Equal(F.DiskProgress.Immediate, result.Progress); return result.Value; }
        internal uint Open() { PutPath("/app/wwwroot/index.html"); long value = Run(Call(2, Path, 0x88100)); HostCheck.True(value >= 3); return (uint)value; }
        internal long Core(ulong number, ulong a0 = 0, ulong a1 = 0, ulong a2 = 0)
        { var result = Thread.DescriptorCall(number, a0, a1, a2); HostCheck.Equal(LinuxSyscallAction.Return, result.Action); return result.Value; }
        internal void CloseUserDescriptors()
        { for (uint fd = 3; fd < 32; fd++) if (F.Capture(Files.State, fd, out _)) HostCheck.Equal(0L, Core(3, fd)); }
        internal ulong Position(uint fd)
        { uint index = SlotDescription(ref Files.Admission.Slots[fd]) - 1; return DescriptionPosition(ref Files.Admission.Descriptions[index]); }
        internal void Fill(int bytes, byte value) => new Span<byte>(Thread.At(Output), bytes).Fill(value);
        internal bool Unchanged(int bytes, byte value) => new ReadOnlySpan<byte>(Thread.At(Output), bytes).IndexOfAnyExcept(value) < 0;
        internal bool May(ulong address, ulong bytes) => Thread.Service->MayChangeUserRange(Key, 2, address, bytes);
        internal void ParkSource4()
        {
            HostCheck.True(Thread.Service->CommitDiskWait(Key, 2, Thread.Kernel.Capture(worker), out bool handoff));
            if (handoff) Thread.Kernel.Confirm();
        }
        internal void WaitSource4Port()
        {
            for (int i = 0; i < 10000 && !Storage.Port->RequestActive; i++)
            { HostCheck.True(Thread.Service->PollDiskReadiness(Key, 2)); Storage.Sample(); }
            HostCheck.True(Storage.Port->RequestActive && Storage.Device.Hardware->Pending);
        }
        internal void ReadySource4()
        {
            // The C writer is a separate owner. This helper never polls its B
            // request or completes its device operation on the reader's behalf.
            for (int i = 0; i < 100000; i++)
            {
                HostCheck.True(Thread.Service->PollDiskReadiness(Key, 2)); Storage.Sample();
                if (Thread.Kernel.Inspect(worker).Completion != KernelThreadTable.Completion.None) return;
            }
            throw new InvalidOperationException("outer_source4_wait_bound");
        }
        internal long ConsumeSource4()
        {
            if (!Thread.Kernel.Table->Current.IsValid) HostCheck.Equal(worker.Value, Thread.Kernel.Dispatch().Value);
            HostCheck.Equal(worker.Value, Thread.Kernel.Table->Current.Value);
            var result = Thread.Service->CompleteDiskWait(Key, 2); HostCheck.Equal(LinuxSyscallAction.Return, result.Action); return result.Value;
        }
        internal void ExitChild(OSHostHandle child)
        {
            HostCheck.Equal(child.Value, Thread.Kernel.Table->Current.Value);
            HostCheck.Equal(LinuxSyscallAction.ThreadExited, Thread.Call(60).Action);
            HostCheck.True(Thread.Service->CommitExit(Key, 2, Thread.Kernel.Capture(child), out long clear) && clear == 0);
            Thread.Kernel.Confirm(); HostCheck.True(Thread.Service->Reap(Key, 2, child, Thread.Kernel.SupervisorRsp, out _));
        }
        internal void StartWriter()
        { HostCheck.True(writer.Owner == 0); var file = Storage.OpenData("FRAG.BIN"); C.RequestToken request = default; HostCheck.Equal(C.Status.Pending, C.BeginOverwrite(Storage.Engine, file, 0, "same-owner data"u8, &request)); writer = request; }
        internal void WriterReady(Action? observe = null)
        {
            for (int i = 0; i < 100000; i++)
            { var status = Storage.EngineTurn(); observe?.Invoke(); if (status == C.Status.Ready) break; HostCheck.Equal(C.Status.Pending, status); HostCheck.True(i < 99999); }
            HostCheck.True(Storage.Engine->IsReady && Storage.Engine->RequestActive && !Storage.Engine->Dirty);
        }
        internal void ConsumeWriter()
        { C.Result result = default; HostCheck.Equal(C.Status.Success, C.CopyResult(Storage.Engine, writer, &result)); HostCheck.Equal(15U, result.Bytes); writer = default; }
        public void Dispose()
        {
            try
            {
                if (writer.Owner != 0) { WriterReady(); ConsumeWriter(); }
                if (WriteAttached)
                {
                    foreach (var token in writeTokens) F.CancelWritableDisk(Files.State, OwnerKey, token, true);
                    for (int i = 0; i < 100000 && WriteDisk->RequestActive; i++) { var result = WPump(); CompleteDevice(); HostCheck.True(result.Progress != F.WritableDiskProgress.RecoveryRequired); }
                    HostCheck.True(!WriteDisk->RequestActive);
                }
                foreach (var token in tokens) F.CancelDisk(Files.State, OwnerKey, token);
                for (int i = 0; i < 1000; i++)
                {
                    if (tokens.All(t => Observe(t).Progress == F.DiskProgress.Rejected)) break;
                    if (Storage.Device.Hardware->Pending) Storage.Device.Complete(); F.PumpDiskOne(Files.State, OwnerKey); Storage.Sample(); HostCheck.True(i < 999);
                }
                CloseUserDescriptors();
                foreach (var connection in connections)
                    if (connection.Table->Count != 0)
                    { if (connection.Owner->Active) HostCheck.Equal(Tx.Status.Success, Tx.AbortConnection(connection.Owner)); HostCheck.True(Sockets.ReapClosed(SocketState, connection.Owner)); }
                foreach (var shared in sharedConnections) for (uint i = 0; i < 2; i++)
                { if (shared.Sender(i)->Active) HostCheck.Equal(Tx.Status.Success, Tx.AbortConnection(shared.Sender(i))); HostCheck.True(Sockets.ReapClosed(SocketState, shared.Sender(i))); }
                if (ReadAttached) HostCheck.True(F.DetachDisk(Files.State));
                if (WriteAttached) HostCheck.True(F.DetachWritableDisk(Files.State));
                HostCheck.True(Thread.Service->Shutdown(Key, 2, 7)); HostCheck.True(Thread.Service->CommitExit(Key, 2, Thread.Kernel.Capture(worker), out long clear) && clear == 0);
                Thread.Kernel.Confirm(); HostCheck.True(Thread.Service->Reap(Key, 2, worker, Thread.Kernel.SupervisorRsp, out _));
                HostCheck.True(Thread.Service->Release(Key, 2));
                if (signalState != null) HostCheck.True(Signals.End(signalState, SignalKey, 2));
                if (PacketNetwork != null)
                {
                    // Network.Abort requires ALL descriptors closed. The
                    // process has released its wrapper; standalone ownership
                    // may now close the three retained standard descriptions.
                    for (uint fd = 0; fd < 3; fd++) if (F.Capture(Files.State, fd, out _)) HostCheck.Equal(0L,
                        F.Dispatch(Files.State, new() { Number = 3, Argument0 = fd }));
                    PacketNetwork.Retire();
                }
                HostCheck.True(F.End(Files.State));
                if (Provider != null) HostCheck.Equal(W.Progress.Retired, W.Detach(Provider).Phase);
            }
            finally
            { if (signalState != null) NativeMemory.AlignedFree(signalState);
                NativeMemory.Free(Disk); NativeMemory.Free(bindings); NativeMemory.Free(Requests); NativeMemory.Free(WriteDisk); NativeMemory.Free(WriteBindings);
                NativeMemory.Free(Provider); NativeMemory.Free(writeFds); NativeMemory.Free(writeDescriptions); NativeMemory.Free(WriteInput);
                NativeMemory.Free(processes); NativeMemory.Free(locks); NativeMemory.Free(lockScratch);
                foreach (var connection in connections) connection.Dispose(); foreach (var shared in sharedConnections) shared.Dispose(); PacketNetwork?.Dispose();
                NativeMemory.Free(SocketState); NativeMemory.Free(endpoints); NativeMemory.Free(epollState); NativeMemory.Free(ports); NativeMemory.Free(registrations);
                Files.Dispose(); Thread.Dispose(); Storage.Dispose(); }
        }
    }
}
