using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Memory;
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
internal static unsafe partial class LinuxBorrowedDiskDescriptorServicesTests
{
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
        internal TcpConnectionTable.Owner* FirstSharedTable => sharedConnections[0].Table;
        internal Tx.Owner* LastTransportSender => sharedConnections.Count == 0 ? Connection!.Owner : sharedConnections[^1].Sender(1);

        internal LinuxSocketNetworkTests.PacketCarrier? PacketNetwork;
        internal uint PacketLocal;
        private readonly List<TcpSendWindowTests.Fixture> connections = [];
        private readonly List<SharedTcp> sharedConnections = [];
        internal bool ReadAttached, WriteAttached;
        internal ulong OwnerKey => (ulong)Thread.Service;
        internal F.DiskAdmission Admission => new()
        {
            Mount = Storage.Mount,
            Bindings = bindings,
            Requests = Requests,
            Epoch = Storage.Input.Epoch,
            FirmwareExited = true,
            SingleOwnerValidated = true
        };

        internal Owner(byte[] raw, OS.Tooling.CombinedWebDataImageFixture.Receipt receipt, bool attach, Action<Owner>? beforeThreads = null, bool signals = false)
        {
            image = receipt;
            Storage = new(raw, receipt);
            Storage.MountReady();
            Files.Admission.User = Thread.DescriptorAccess;
            Files.Admission.CopyToUser = Thread.UserCopyOut;
            Files.Begin();
            if (attach)
                HostCheck.True(AttachReadOnly());
            beforeThreads?.Invoke(this);
            if (signals)
            {
                signalBytes = checked(((uint)sizeof(Signals.State) + 4095U) & ~4095U);
                signalState = (Signals.State*)NativeMemory.AlignedAlloc(signalBytes, 4096);
                new Span<byte>(signalState, checked((int)signalBytes)).Clear();
                HostCheck.True(Signals.Initialize(signalState, signalBytes, SignalKey, 2, new() { User = Thread.Access, CopyToUser = Thread.UserCopyOut, Capacity = Thread.Capacity, FirmwareExited = true, SingleOwnerValidated = true }));
                HostCheck.True(Thread.Service->EnableSignals(Key, 2, signalState));
            }

            HostCheck.True(Thread.Service->EnablePipes(Key, 2, Files.State));
            threadsAttached = true;
            worker = Thread.Start();
            HostCheck.Equal(worker.Value, Thread.Kernel.Dispatch().Value);
        }

        internal void PrepareTransport(Action<Owner>? beforeEpoll = null)
        {
            HostCheck.True(SocketState == null);
            SocketState = (Sockets.State*)NativeMemory.AllocZeroed((nuint)sizeof(Sockets.State));
            endpoints = (Sockets.Endpoint*)NativeMemory.AllocZeroed(8, (nuint)sizeof(Sockets.Endpoint));
            epollState = (Epoll.State*)NativeMemory.AllocZeroed((nuint)sizeof(Epoll.State));
            ports = (Epoll.Port*)NativeMemory.AllocZeroed(2, (nuint)sizeof(Epoll.Port));
            registrations = (Epoll.Registration*)NativeMemory.AllocZeroed(16, (nuint)sizeof(Epoll.Registration));
            HostCheck.True(Sockets.Begin(SocketState, Files.State, endpoints, 8, new(10, 0, 2, 15), 8080));
            beforeEpoll?.Invoke(this);
            HostCheck.True(Epoll.Begin(epollState, Files.State, ports, 2, registrations, 16));
            Listener = checked((uint)SCall(41, 2, 1 | F.NonBlock, 6));
            Thread.Put64(SocketData, 0x901f0002);
            Thread.Put64(SocketData + 8, 0);
            HostCheck.Equal(0L, SCall(49, Listener, SocketData, 16));
            HostCheck.Equal(0L, SCall(50, Listener, 4));
            EpollFd = checked((uint)SCall(291, F.CloseOnExec));
        }

        internal void PrepareConnectedTransport()
        {
            PrepareTransport();
            var connection = LinuxSocketServicesTests.Connection();
            connections.Add(connection);
            Connection = connection;
            F.Token token = default;
            HostCheck.True(F.Capture(Files.State, Listener, out token));
            HostCheck.True(Sockets.EnqueueAccepted(SocketState, token, Listener, connection.Receiver, connection.Owner, (uint)sizeof(TcpSendWindowTests.Hardware)));
            Connected = checked((uint)SCall(288, Listener, 0, 0, F.NonBlock));
            Thread.Put32(SocketEvent, 1 | 4 | 8 | 16);
            Thread.Put64(SocketEvent + 4, 0x1234567890abcdef);
            HostCheck.Equal(0L, SCall(233, EpollFd, 1, Connected, SocketEvent));
        }

        internal void Adopt(TcpSendWindowTests.Fixture connection) => connections.Add(connection);
        internal void PreparePacketNetwork()
        {
            HostCheck.True(PacketNetwork == null && Connection == null && SocketState != null);
            PacketNetwork = new(SocketState);
            HostCheck.True(PacketNetwork.Start());
            PacketLocal = PacketNetwork.Connect();
            Connected = checked((uint)SCall(288, Listener, 0, 0, F.NonBlock));
            Thread.Put32(SocketEvent, 1 | 4 | 8 | 16);
            Thread.Put64(SocketEvent + 4, 0x1234567890abcdef);
            HostCheck.Equal(0L, SCall(233, EpollFd, 1, Connected, SocketEvent));
        }

        internal void EnqueueShared()
        {
            var shared = new SharedTcp();
            sharedConnections.Add(shared);
            F.Token token = default;
            HostCheck.True(F.Capture(Files.State, Listener, out token));
            for (uint i = 0; i < 2; i++)
            {
                HostCheck.True(Sockets.EnqueueAccepted(SocketState, token, Listener, shared.Receiver(i), shared.Sender(i), (uint)sizeof(TcpSendWindowTests.Hardware)));
                HostCheck.True(SCall(288, Listener, 0, 0, F.NonBlock) >= 3);
            }

            HostCheck.Equal(2, shared.Table->Count);
        }

        internal long SCall(ulong number, ulong a0 = 0, ulong a1 = 0, ulong a2 = 0, ulong a3 = 0)
        {
            if (!threadsAttached)
                return F.Dispatch(Files.State, new() { Number = number, Argument0 = a0, Argument1 = a1, Argument2 = a2, Argument3 = a3 });
            var result = Thread.DescriptorCall(number, a0, a1, a2, a3);
            HostCheck.Equal(LinuxSyscallAction.Return, result.Action);
            return result.Value;
        }

        internal void SocketPoll()
        {
            HostCheck.True(Epoll.Refresh(epollState));
            HostCheck.True(SCall(281, EpollFd, SocketOutput, 1, 0) >= 0 && F.Audit(Files.State));
        }

        internal void SocketRoundtrip()
        {
            var connection = Connection!;
            "net"u8.CopyTo(new Span<byte>(Thread.At(SocketData), 3));
            HostCheck.True(F.Audit(Files.State));
            uint reads = Thread.Reads;
            long sent = SCall(44, Connected, SocketData, 3);
            if (sent != 3)
                Console.WriteLine($"SOCKET_SEND_FAILURE result={sent} reads={Thread.Reads - reads} global={F.Audit(Files.State)} socket={Sockets.Audit(SocketState, Files.State)} epoll={Epoll.Audit(epollState, Files.State)} disk={AuditDisk(Files.State)} writable={AuditWritable(Files.State)} pair={AuditPair(Files.State)}");
            HostCheck.Equal(3L, sent);
            HostCheck.Equal(Tx.Status.Success, Tx.Pump(connection.Owner, new byte[1514], out var packet));
            HostCheck.Equal(3, packet.PayloadBytes);
            HostCheck.Equal(Tx.Status.Success, connection.Ack(703, out int released));
            HostCheck.Equal(3, released);
            HostCheck.Equal(Tx.Status.Success, Tx.ReceiveFrame(connection.Owner, Wire.Packet(100, "wire"u8, ack: 703), out _));
            HostCheck.True(Epoll.Refresh(epollState));
            HostCheck.Equal(1L, SCall(281, EpollFd, SocketOutput, 1, 0));
            HostCheck.True((Thread.Read32(SocketOutput) & 1) != 0 && Thread.Read64(SocketOutput + 4) == 0x1234567890abcdef);
            HostCheck.Equal(4L, SCall(45, Connected, SocketData, 4));
            HostCheck.True(new ReadOnlySpan<byte>(Thread.At(SocketData), 4).SequenceEqual("wire"u8));
        }

        internal void PacketRoundtrip()
        {
            var packet = PacketNetwork!;
            "nic"u8.CopyTo(new Span<byte>(Thread.At(SocketData), 3));
            HostCheck.Equal(3L, SCall(44, Connected, SocketData, 3));
            HostCheck.Equal(Network.Status.Success, packet.Pump());
            HostCheck.Equal(PacketParseStatus.Success, EthernetHeader.TryParse(packet.Sent, out _, out var ipv4));
            HostCheck.Equal(PacketParseStatus.Success, Ipv4Header.TryParse(ipv4, out _, out var tcp));
            HostCheck.Equal(PacketParseStatus.Success, TcpHeader.TryParse(tcp, out _, out _, out var bytes));
            HostCheck.True(bytes.SequenceEqual("nic"u8));
            HostCheck.Equal(Network.Status.Success, packet.Receive(Wire.Packet(100, ReadOnlySpan<byte>.Empty, PacketLocal + 3, flags: 0x10)));
            HostCheck.Equal(Network.Status.Success, packet.Receive(Wire.Packet(100, "peer"u8, PacketLocal + 3, flags: 0x18)));
            SocketPoll();
            HostCheck.True((Thread.Read32(SocketOutput) & 1) != 0 && Thread.Read64(SocketOutput + 4) == 0x1234567890abcdef);
            HostCheck.Equal(4L, SCall(45, Connected, SocketData, 4));
            HostCheck.True(new ReadOnlySpan<byte>(Thread.At(SocketData), 4).SequenceEqual("peer"u8));
            HostCheck.Equal(Network.Status.Success, packet.Pump());
            PacketLocal += 3;
        }

        internal IEnumerable<(ulong, ulong)> TransportExtents()
        {
            if (PacketNetwork != null)
                return new[]
                {
                    ((ulong)SocketState, (ulong)sizeof(Sockets.State)),
                    ((ulong)endpoints, 8UL * (ulong)sizeof(Sockets.Endpoint)),
                    ((ulong)epollState, (ulong)sizeof(Epoll.State)),
                    ((ulong)ports, 2UL * (ulong)sizeof(Epoll.Port)),
                    ((ulong)registrations, 16UL * (ulong)sizeof(Epoll.Registration))
                }.Concat(PacketNetwork.Extents());
            var c = Connection!;
            return new[]
            {
                ((ulong)SocketState, (ulong)sizeof(Sockets.State)),
                ((ulong)endpoints, 8UL * (ulong)sizeof(Sockets.Endpoint)),
                ((ulong)epollState, (ulong)sizeof(Epoll.State)),
                ((ulong)ports, 2UL * (ulong)sizeof(Epoll.Port)),
                ((ulong)registrations, 16UL * (ulong)sizeof(Epoll.Registration)),
                ((ulong)c.Owner, (ulong)sizeof(Tx.Owner)),
                ((ulong)c.Receiver, (ulong)sizeof(Rx.Owner)),
                ((ulong)c.Buffer, 8UL),
                ((ulong)c.ReceiveBuffer, 8UL),
                ((ulong)c.Table, (ulong)sizeof(TcpConnectionTable.Owner)),
                ((ulong)TableEntries(ref *c.Table), (ulong)c.Table->Capacity * (ulong)sizeof(TcpConnectionTableEntry)),
                ((ulong)TableGenerations(ref *c.Table), (ulong)c.Table->Capacity * 8UL),
                ((ulong)c.Hardware, (ulong)sizeof(TcpSendWindowTests.Hardware))
            };
        }

        internal (ulong Address, byte[] Bytes)[] SocketAuditSnapshot()
        {
            // Copied host-test storage, including the actual contiguous engine
            // arena and all declared filesystem ranges. No producer authority.
            var a = Files.Admission;
            List<(ulong Address, ulong Bytes)> ranges = [..TransportExtents(), ..Storage.FsExtents(), ((ulong)Files.State, (ulong)sizeof(F.State)), ((ulong)a.Slots, a.DescriptorCount * (ulong)sizeof(F.Slot)), ((ulong)a.Descriptions, a.DescriptionCount * (ulong)sizeof(F.Description)), ((ulong)Disk, (ulong)sizeof(F.DiskState)), ((ulong)bindings, 8UL * (ulong)sizeof(F.DiskBinding)), ((ulong)Requests, 8UL * (ulong)sizeof(F.DiskRequest)), ((ulong)WriteDisk, (ulong)sizeof(F.WritableDiskState)), ((ulong)WriteBindings, 32UL * (ulong)sizeof(F.WritableDiskBinding)), ((ulong)Provider, (ulong)sizeof(W.State)), ((ulong)writeFds, 32UL * (ulong)sizeof(W.Descriptor)), ((ulong)writeDescriptions, 32UL * (ulong)sizeof(W.Description)), ((ulong)WriteInput, W.IoBytes), ((ulong)processes, 4UL * (ulong)sizeof(W.ProcessSlot)), ((ulong)locks, 32UL * (ulong)sizeof(W.RangeLock)), ((ulong)lockScratch, 65UL * (ulong)sizeof(W.RangeLock)), ((ulong)Storage.Port, (ulong)sizeof(P.State)), ((ulong)Storage.Engine, (ulong)sizeof(C.State)), ((ulong)Storage.EngineInput.Changes, Storage.EngineBackingBytes), ((ulong)Storage.Block, (ulong)sizeof(B.State)), ((ulong)Storage.Device.Observation, (ulong)sizeof(B.Observation)), ((ulong)Storage.Device.Data, 4096), ((ulong)Storage.Device.Hardware, (ulong)sizeof(WritableBlockSessionTests.Hardware)), ((ulong)Thread.Kernel.Memory, KernelThreadTableTests.Model.Bytes), ((ulong)Thread.Access.Context, Thread.UserContextBytes)];
            if (signalState != null)
                ranges.Add(((ulong)signalState, signalBytes));
            return ranges.Select(range => (range.Address, new ReadOnlySpan<byte>((void*)range.Address, checked((int)range.Bytes)).ToArray())).ToArray();
        }

        internal IEnumerable<(ulong, ulong)> ProcessExtents()
        {
            List<(ulong, ulong)> ranges = [((ulong)Thread.Service, (ulong)sizeof(LinuxThreadServices)), ((ulong)Thread.Slots, Thread.Capacity * (ulong)sizeof(LinuxThreadServices.Slot)), ((ulong)Thread.Workers, Thread.Capacity * (ulong)sizeof(LinuxThreadServices.Worker)), ((ulong)Thread.Access.Context, Thread.UserContextBytes)];
            var futex = Thread.FutexPlan();
            ranges.Add(((ulong)futex.Cells, futex.CellBytes));
            ranges.Add(((ulong)futex.Addresses, futex.AddressBytes));
            ranges.Add(((ulong)futex.Registrations, futex.RegistrationBytes));
            if (signalState != null)
                ranges.Add(((ulong)signalState, (ulong)sizeof(Signals.State)));
            uint count = Thread.Kernel.Table->RetainedExtentCount(KernelThreadTableTests.Key, 2);
            HostCheck.True(count != 0);
            for (uint i = 0; i < count; i++)
            {
                HostCheck.Equal(KernelThreadTable.Status.Ok, Thread.Kernel.Table->InspectRetainedExtent(KernelThreadTableTests.Key, 2, i, out ulong start, out ulong bytes));
                if (bytes != 0)
                    ranges.Add((start, bytes));
            }

            for (uint i = 0; i < Thread.Capacity; i++)
            {
                var w = Thread.Workers[i].Initial;
                ranges.Add(((ulong)w.Context, w.ContextBytes));
                ranges.Add((w.StackGuardStart, w.StackBytes));
            }

            return ranges;
        }

        internal void CheckNetworkRejected()
        {
            HostCheck.Equal(0U, SocketState->RetainedConnections);
            Network.State network = default;
            Network.Slot slot = default;
            TcpConnectionTable.Owner table = default;
            TcpConnectionTableEntry entry = default;
            ulong generation = 0;
            byte* buffers = stackalloc byte[64];
            new Span<byte>(buffers, 64).Clear();
            HostCheck.Equal(TcpConnectionTable.OwnershipStatus.Success, TcpConnectionTable.InitializeOwned(&table, &entry, 1, &generation, 1, 73));
            NetworkProbe probe = new()
            {
                Now = 1000
            };
            var io = new TcpPassiveConnection.Operations
            {
                Context = &probe,
                NowNanoseconds = &ProbeClock,
                Send = &ProbeSend,
                LifetimeNanoseconds = 10000000000,
                RetryNanoseconds = 1000000000,
                MaximumAttempts = 3
            };
            HostCheck.True(!Network.Begin(&network, SocketState, &table, &slot, 1, buffers, 32, 32, new(2, 0, 0, 0, 0, 1), new(0x52, 0x55, 0x0a, 0, 2, 2), new(10, 0, 2, 2), 700, io, compactTimeWait: true));
            HostCheck.True(probe.Calls == 0 && new ReadOnlySpan<byte>(&network, sizeof(Network.State)).IndexOfAnyExcept((byte)0) < 0 && new ReadOnlySpan<byte>(&slot, sizeof(Network.Slot)).IndexOfAnyExcept((byte)0) < 0 && new ReadOnlySpan<byte>(buffers, 64).IndexOfAnyExcept((byte)0) < 0 && F.Audit(Files.State));
        }

        internal void CheckSocketAliases(ulong alias)
        {
            Sockets.State socket = default;
            uint callbacks = Storage.Device.Hardware->Calls;
            HostCheck.True(!Sockets.Begin(&socket, Files.State, (Sockets.Endpoint*)alias, 8, new(10, 0, 2, 15), 8080));
            HostCheck.True(new ReadOnlySpan<byte>(&socket, sizeof(Sockets.State)).IndexOfAnyExcept((byte)0) < 0 && callbacks == Storage.Device.Hardware->Calls && F.Audit(Files.State));
        }

        internal void CheckEpollAliases(ulong alias)
        {
            Epoll.State epoll = default;
            Epoll.Registration* rows = stackalloc Epoll.Registration[2];
            new Span<byte>(rows, 2 * sizeof(Epoll.Registration)).Clear();
            uint callbacks = Storage.Device.Hardware->Calls;
            byte[] before = new ReadOnlySpan<byte>((void*)alias, 16).ToArray();
            HostCheck.True(!Epoll.Begin(&epoll, Files.State, (Epoll.Port*)alias, 2, rows, 2));
            HostCheck.True(before.AsSpan().SequenceEqual(new ReadOnlySpan<byte>((void*)alias, 16)) && callbacks == Storage.Device.Hardware->Calls);
        }

        internal byte[] Expected(string path) => image.Image.WebFiles.Single(x => x.Path == path).CopyBytes();
        internal byte[] DataExpected(string name) => image.Image.Data.Files.Single(x => x.Name == name).CopyBytes();
        internal LinuxSyscallFrame Call(ulong number, ulong a0 = 0, ulong a1 = 0, ulong a2 = 0, ulong a3 = 0, ulong a4 = 0, ulong a5 = 0)
        {
            var call = Thread.CurrentImage()->Frame;
            call.Number = number;
            call.Argument0 = a0;
            call.Argument1 = a1;
            call.Argument2 = a2;
            call.Argument3 = a3;
            call.Argument4 = a4;
            call.Argument5 = a5;
            return call;
        }

        internal bool AttachReadOnly()
        {
            bool result = F.AttachDisk(Files.State, Disk, Admission);
            ReadAttached |= result;
            return result;
        }

        internal F.WritableDiskAdmission WriteAdmission => new()
        {
            Provider = Provider,
            Process = W.PrimaryProcess(Provider),
            Bindings = WriteBindings,
            BindingCount = 32,
            Epoch = Storage.EngineInput.Epoch,
            Search = new(2, 64, 256),
            FirmwareExited = true,
            SingleOwnerValidated = true
        };

        internal void PrepareWritable()
        {
            HostCheck.True(Provider == null);
            Provider = (W.State*)NativeMemory.AllocZeroed((nuint)sizeof(W.State));
            writeFds = (W.Descriptor*)NativeMemory.AllocZeroed(32, (nuint)sizeof(W.Descriptor));
            writeDescriptions = (W.Description*)NativeMemory.AllocZeroed(32, (nuint)sizeof(W.Description));
            WriteInput = (byte*)NativeMemory.AllocZeroed(W.IoBytes);
            processes = (W.ProcessSlot*)NativeMemory.AllocZeroed(4, (nuint)sizeof(W.ProcessSlot));
            locks = (W.RangeLock*)NativeMemory.AllocZeroed(32, (nuint)sizeof(W.RangeLock));
            lockScratch = (W.RangeLock*)NativeMemory.AllocZeroed(65, (nuint)sizeof(W.RangeLock));
            WriteDisk = (F.WritableDiskState*)NativeMemory.AllocZeroed((nuint)sizeof(F.WritableDiskState));
            WriteBindings = (F.WritableDiskBinding*)NativeMemory.AllocZeroed(32, (nuint)sizeof(F.WritableDiskBinding));
            HostCheck.True(W.Start(Provider, new() { Engine = Storage.Engine, Epoch = Storage.EngineInput.Epoch, Descriptors = writeFds, Descriptions = writeDescriptions, DescriptorCount = 32, DescriptionCount = 32, Input = WriteInput, InputBytes = W.IoBytes, ExclusiveOwnerValidated = true }));
            HostCheck.True(W.EnableLocks(Provider, new() { Processes = processes, ProcessCount = 4, Locks = locks, LockCount = 32, Scratch = lockScratch, ScratchCount = 65, PrimaryPid = 101 }));
        }

        internal bool AttachWritable()
        {
            bool result = F.AttachWritableDisk(Files.State, WriteDisk, WriteAdmission);
            WriteAttached |= result;
            return result;
        }

        internal void AttachPair(bool writeFirst)
        {
            if (writeFirst)
            {
                HostCheck.True(AttachWritable());
                HostCheck.True(AttachReadOnly());
            }
            else
            {
                HostCheck.True(AttachReadOnly());
                HostCheck.True(AttachWritable());
            }
        }

        internal F.WritableDiskResult WBegin(LinuxSyscallFrame call)
        {
            var result = F.BeginWritableDisk(Files.State, OwnerKey, worker, call);
            if (result.Token.IsActive)
                writeTokens.Add(result.Token);
            return result;
        }

        internal F.WritableDiskResult WPump()
        {
            var result = F.PumpWritableDiskOne(Files.State, OwnerKey);
            Storage.Sample();
            return result;
        }

        internal void CompleteDevice()
        {
            if (Storage.Device.Hardware->Pending)
                Storage.Device.Complete();
            Storage.Sample();
        }

        internal F.WritableDiskResult WReady(F.WritableDiskToken token)
        {
            for (int i = 0; i < 100000; i++)
            {
                var result = WPump();
                CompleteDevice();
                if (result.Progress is F.WritableDiskProgress.Ready or F.WritableDiskProgress.RecoveryRequired or F.WritableDiskProgress.Rejected)
                    return result;
            }

            throw new InvalidOperationException("paired_writable_bound");
        }

        internal F.WritableDiskResult WComplete(F.WritableDiskToken token, LinuxSyscallFrame call) => F.CompleteWritableDisk(Files.State, OwnerKey, worker, token, call);
        internal long WRun(LinuxSyscallFrame call)
        {
            var begun = WBegin(call);
            if (begun.Progress == F.WritableDiskProgress.Immediate)
                return begun.Value;
            HostCheck.True(begun.Progress is F.WritableDiskProgress.Pending or F.WritableDiskProgress.Ready);
            HostCheck.Equal(F.WritableDiskProgress.Ready, WReady(begun.Token).Progress);
            var result = WComplete(begun.Token, call);
            HostCheck.Equal(F.WritableDiskProgress.Immediate, result.Progress);
            return result.Value;
        }

        internal uint WOpen()
        {
            PutPath("/data/FRAG.BIN");
            long value = WRun(Call(2, Path, 2));
            HostCheck.True(value >= 3);
            return (uint)value;
        }

        internal LinuxSyscallFrame WriteCall(uint fd, ReadOnlySpan<byte> bytes, ulong offset)
        {
            bytes.CopyTo(new Span<byte>(Thread.At(Payload), bytes.Length));
            *(ulong*)Thread.At(Vector) = Payload;
            *(ulong*)(Thread.At(Vector) + 8) = (ulong)bytes.Length;
            return Call(328, fd, Vector, 1, offset, 0, W.RwfNoAppend);
        }

        internal void WaitPort(F.DiskToken token)
        {
            for (int i = 0; i < 10000 && !Storage.Port->RequestActive; i++)
                HostCheck.Equal(F.DiskProgress.Pending, Pump(token).Progress);
            HostCheck.True(Storage.Port->RequestActive && Storage.Device.Hardware->Pending);
        }

        internal void PutPath(string value)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(value + '\0');
            bytes.CopyTo(new Span<byte>(Thread.At(Path), bytes.Length));
        }

        internal F.DiskResult Begin(LinuxSyscallFrame call)
        {
            var result = F.BeginDisk(Files.State, OwnerKey, worker, call);
            if (result.Token.IsActive)
                tokens.Add(result.Token);
            return result;
        }

        internal F.DiskResult Observe(F.DiskToken token) => F.PollDisk(Files.State, OwnerKey, token);
        internal F.DiskResult Pump(F.DiskToken token)
        {
            if (Storage.Device.Hardware->Pending)
                Storage.Device.Complete();
            F.PumpDiskOne(Files.State, OwnerKey);
            Storage.Sample();
            return Observe(token);
        }

        internal F.DiskResult Ready(F.DiskToken token)
        {
            for (int i = 0; i < 100000; i++)
            {
                var result = Pump(token);
                if (result.Progress is F.DiskProgress.Ready or F.DiskProgress.Rejected)
                    return result;
            }

            throw new InvalidOperationException("borrowed_global_bound");
        }

        internal F.DiskResult Complete(F.DiskToken token, LinuxSyscallFrame call, uint ceiling = F.DiskCompletionBytes) => F.CompleteDisk(Files.State, OwnerKey, worker, token, call, ceiling);
        internal long Run(LinuxSyscallFrame call)
        {
            var begun = Begin(call);
            if (begun.Progress == F.DiskProgress.Immediate)
                return begun.Value;
            HostCheck.Equal(F.DiskProgress.Pending, begun.Progress);
            HostCheck.Equal(F.DiskProgress.Ready, Ready(begun.Token).Progress);
            var result = Complete(begun.Token, call);
            HostCheck.Equal(F.DiskProgress.Immediate, result.Progress);
            return result.Value;
        }

        internal uint Open()
        {
            PutPath("/app/wwwroot/index.html");
            long value = Run(Call(2, Path, 0x88100));
            HostCheck.True(value >= 3);
            return (uint)value;
        }

        internal long Core(ulong number, ulong a0 = 0, ulong a1 = 0, ulong a2 = 0)
        {
            var result = Thread.DescriptorCall(number, a0, a1, a2);
            HostCheck.Equal(LinuxSyscallAction.Return, result.Action);
            return result.Value;
        }

        internal void CloseUserDescriptors()
        {
            for (uint fd = 3; fd < 32; fd++)
                if (F.Capture(Files.State, fd, out _))
                    HostCheck.Equal(0L, Core(3, fd));
        }

        internal ulong Position(uint fd)
        {
            uint index = SlotDescription(ref Files.Admission.Slots[fd]) - 1;
            return DescriptionPosition(ref Files.Admission.Descriptions[index]);
        }

        internal void Fill(int bytes, byte value) => new Span<byte>(Thread.At(Output), bytes).Fill(value);
        internal bool Unchanged(int bytes, byte value) => new ReadOnlySpan<byte>(Thread.At(Output), bytes).IndexOfAnyExcept(value) < 0;
        internal bool May(ulong address, ulong bytes) => Thread.Service->MayChangeUserRange(Key, 2, address, bytes);
        internal void ParkSource4()
        {
            HostCheck.True(Thread.Service->CommitDiskWait(Key, 2, Thread.Kernel.Capture(worker), out bool handoff));
            if (handoff)
                Thread.Kernel.Confirm();
        }

        internal void WaitSource4Port()
        {
            for (int i = 0; i < 10000 && !Storage.Port->RequestActive; i++)
            {
                HostCheck.True(Thread.Service->PollDiskReadiness(Key, 2));
                Storage.Sample();
            }

            HostCheck.True(Storage.Port->RequestActive && Storage.Device.Hardware->Pending);
        }

        internal void ReadySource4()
        {
            // The C writer is a separate owner. This helper never polls its B
            // request or completes its device operation on the reader's behalf.
            for (int i = 0; i < 100000; i++)
            {
                HostCheck.True(Thread.Service->PollDiskReadiness(Key, 2));
                Storage.Sample();
                if (Thread.Kernel.Inspect(worker).Completion != KernelThreadTable.Completion.None)
                    return;
            }

            throw new InvalidOperationException("outer_source4_wait_bound");
        }

        internal long ConsumeSource4()
        {
            if (!Thread.Kernel.Table->Current.IsValid)
                HostCheck.Equal(worker.Value, Thread.Kernel.Dispatch().Value);
            HostCheck.Equal(worker.Value, Thread.Kernel.Table->Current.Value);
            var result = Thread.Service->CompleteDiskWait(Key, 2);
            HostCheck.Equal(LinuxSyscallAction.Return, result.Action);
            return result.Value;
        }

        internal void ExitChild(OSHostHandle child)
        {
            HostCheck.Equal(child.Value, Thread.Kernel.Table->Current.Value);
            HostCheck.Equal(LinuxSyscallAction.ThreadExited, Thread.Call(60).Action);
            HostCheck.True(Thread.Service->CommitExit(Key, 2, Thread.Kernel.Capture(child), out long clear) && clear == 0);
            Thread.Kernel.Confirm();
            HostCheck.True(Thread.Service->Reap(Key, 2, child, Thread.Kernel.SupervisorRsp, out _));
        }

        internal void StartWriter()
        {
            HostCheck.True(writer.Owner == 0);
            var file = Storage.OpenData("FRAG.BIN");
            C.RequestToken request = default;
            HostCheck.Equal(C.Status.Pending, C.BeginOverwrite(Storage.Engine, file, 0, "same-owner data"u8, &request));
            writer = request;
        }

        internal void WriterReady(Action? observe = null)
        {
            for (int i = 0; i < 100000; i++)
            {
                var status = Storage.EngineTurn();
                observe?.Invoke();
                if (status == C.Status.Ready)
                    break;
                HostCheck.Equal(C.Status.Pending, status);
                HostCheck.True(i < 99999);
            }

            HostCheck.True(Storage.Engine->IsReady && Storage.Engine->RequestActive && !Storage.Engine->Dirty);
        }

        internal void ConsumeWriter()
        {
            C.Result result = default;
            HostCheck.Equal(C.Status.Success, C.CopyResult(Storage.Engine, writer, &result));
            HostCheck.Equal(15U, result.Bytes);
            writer = default;
        }

        public void Dispose()
        {
            try
            {
                if (writer.Owner != 0)
                {
                    WriterReady();
                    ConsumeWriter();
                }

                if (WriteAttached)
                {
                    foreach (var token in writeTokens)
                        F.CancelWritableDisk(Files.State, OwnerKey, token, true);
                    for (int i = 0; i < 100000 && WriteDisk->RequestActive; i++)
                    {
                        var result = WPump();
                        CompleteDevice();
                        HostCheck.True(result.Progress != F.WritableDiskProgress.RecoveryRequired);
                    }

                    HostCheck.True(!WriteDisk->RequestActive);
                }

                foreach (var token in tokens)
                    F.CancelDisk(Files.State, OwnerKey, token);
                for (int i = 0; i < 1000; i++)
                {
                    if (tokens.All(t => Observe(t).Progress == F.DiskProgress.Rejected))
                        break;
                    if (Storage.Device.Hardware->Pending)
                        Storage.Device.Complete();
                    F.PumpDiskOne(Files.State, OwnerKey);
                    Storage.Sample();
                    HostCheck.True(i < 999);
                }

                CloseUserDescriptors();
                foreach (var connection in connections)
                    if (connection.Table->Count != 0)
                    {
                        if (connection.Owner->Active)
                            HostCheck.Equal(Tx.Status.Success, Tx.AbortConnection(connection.Owner));
                        HostCheck.True(Sockets.ReapClosed(SocketState, connection.Owner));
                    }

                foreach (var shared in sharedConnections)
                    for (uint i = 0; i < 2; i++)
                    {
                        if (shared.Sender(i)->Active)
                            HostCheck.Equal(Tx.Status.Success, Tx.AbortConnection(shared.Sender(i)));
                        HostCheck.True(Sockets.ReapClosed(SocketState, shared.Sender(i)));
                    }

                if (ReadAttached)
                    HostCheck.True(F.DetachDisk(Files.State));
                if (WriteAttached)
                    HostCheck.True(F.DetachWritableDisk(Files.State));
                HostCheck.True(Thread.Service->Shutdown(Key, 2, 7));
                HostCheck.True(Thread.Service->CommitExit(Key, 2, Thread.Kernel.Capture(worker), out long clear) && clear == 0);
                Thread.Kernel.Confirm();
                HostCheck.True(Thread.Service->Reap(Key, 2, worker, Thread.Kernel.SupervisorRsp, out _));
                HostCheck.True(Thread.Service->Release(Key, 2));
                if (signalState != null)
                    HostCheck.True(Signals.End(signalState, SignalKey, 2));
                if (PacketNetwork != null)
                {
                    // Network.Abort requires ALL descriptors closed. The
                    // process has released its wrapper; standalone ownership
                    // may now close the three retained standard descriptions.
                    for (uint fd = 0; fd < 3; fd++)
                        if (F.Capture(Files.State, fd, out _))
                            HostCheck.Equal(0L, F.Dispatch(Files.State, new() { Number = 3, Argument0 = fd }));
                    PacketNetwork.Retire();
                }

                HostCheck.True(F.End(Files.State));
                if (Provider != null)
                    HostCheck.Equal(W.Progress.Retired, W.Detach(Provider).Phase);
            }
            finally
            {
                if (signalState != null)
                    NativeMemory.AlignedFree(signalState);
                NativeMemory.Free(Disk);
                NativeMemory.Free(bindings);
                NativeMemory.Free(Requests);
                NativeMemory.Free(WriteDisk);
                NativeMemory.Free(WriteBindings);
                NativeMemory.Free(Provider);
                NativeMemory.Free(writeFds);
                NativeMemory.Free(writeDescriptions);
                NativeMemory.Free(WriteInput);
                NativeMemory.Free(processes);
                NativeMemory.Free(locks);
                NativeMemory.Free(lockScratch);
                foreach (var connection in connections)
                    connection.Dispose();
                foreach (var shared in sharedConnections)
                    shared.Dispose();
                PacketNetwork?.Dispose();
                NativeMemory.Free(SocketState);
                NativeMemory.Free(endpoints);
                NativeMemory.Free(epollState);
                NativeMemory.Free(ports);
                NativeMemory.Free(registrations);
                Files.Dispose();
                Thread.Dispose();
                Storage.Dispose();
            }
        }
    }
}
