#if CLROS_LINUX_SOCKETS
using System;
using OS.Kernel.Diagnostics;
using OS.Kernel.Memory;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Platform;
using OS.Net;
using NetDevice = OS.Drivers.ContinuingVirtioNetDevice;
using SocketNetwork = OS.Kernel.Compatibility.LinuxX64.LinuxSocketNetwork;
using ThreadTable = OS.Kernel.Threading.KernelThreadTable;

namespace OS.Boot;

public static unsafe partial class EfiEntry
{
    private static partial class M03FirstExitAdapter
    {
        // Called synchronously from the checked socket adapter after complete
        // metadata/range admission, before payload copies or TCP mutation. The
        // borrowed vector snapshot stays on the authenticated current stack.
        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte ObserveLinuxSocketMessage(void* context, ulong number, uint fd, ulong header,
            ulong flags, ulong table, ulong* vectors, uint count, uint total)
        {
            const uint snapshotBytes = LinuxSocketServices.MaximumMessageVectors * 16;
            Storage* state = (Storage*)context;
            LinuxNativeControl* c = LinuxNativeOwner(state);
            if (c == null || c->Phase != 3 || c->Published != 1 || state->NetworkPhase != 2 ||
                c->Selected == 0 || c->Selected > c->Capacity || !c->Threads.Current.IsValid ||
                c->Threads.Current.Value != c->Plan.Bindings[c->Selected - 1].Context.Identity ||
                number is not (46 or 47) || count > LinuxSocketServices.MaximumMessageVectors ||
                total > LinuxSocketServices.MaximumMessageBytes ||
                // Both message callers allocate the full fixed snapshot even
                // for fewer vectors. Authenticate all of it without reading
                // unused entries; normal calls exercise this exact capacity.
                !LinuxThreadKernelRange(c, (byte*)vectors, snapshotBytes, false, snapshotBytes) ||
                !LinuxDescriptorServices.Capture(&state->LinuxDescriptors, fd, out var token)) return 0;
            LinuxSyscallFrame* frame = (LinuxSyscallFrame*)(state->LinuxSyscallData + X64LinuxUserTransition.FrameOffset);
            if (frame->Number != number || (uint)frame->Argument0 != fd || frame->Argument1 != header ||
                frame->Argument2 != flags) return 0;
            ulong identity = c->Threads.Current.Value, ordinal = LinuxNativeCallOrdinal(c, c->Selected - 1);
            if (ordinal == 0 || !LinuxText(state, "M16_013_MESSAGE identity=") || !LinuxNumber(state, identity) ||
                !LinuxText(state, " ordinal=") || !LinuxNumber(state, ordinal) ||
                !LinuxText(state, " nr=") || !LinuxNumber(state, number) ||
                !LinuxText(state, " fd=") || !LinuxNumber(state, fd) ||
                !LinuxText(state, " generation=") || !LinuxNumber(state, token.Generation) ||
                !LinuxText(state, " header=") || !LinuxNumber(state, header) ||
                !LinuxText(state, " flags=") || !LinuxNumber(state, flags) ||
                !LinuxText(state, " table=") || !LinuxNumber(state, table) ||
                !LinuxText(state, " vectors=") || !LinuxNumber(state, count) ||
                !LinuxText(state, " total=") || !LinuxNumber(state, total) || !LinuxText(state, "\r\n")) return 0;
            for (uint i = 0; i < count; i++)
                if (!LinuxText(state, "M16_013_VECTOR identity=") || !LinuxNumber(state, identity) ||
                    !LinuxText(state, " ordinal=") || !LinuxNumber(state, ordinal) ||
                    !LinuxText(state, " index=") || !LinuxNumber(state, i) ||
                    !LinuxText(state, " address=") || !LinuxNumber(state, vectors[i * 2]) ||
                    !LinuxText(state, " bytes=") || !LinuxNumber(state, vectors[i * 2 + 1]) || !LinuxText(state, "\r\n")) return 0;
            return 1;
        }

#if M16_014_PROBE
        // Observe only committed quota-profile socket calls on this serialized
        // kernel turn. Counters belong to the actual TCP lifetime, not an fd,
        // sequence number or ring-buffer position. No I/O is retried here.
        private static bool ObserveLinuxQuotaSocketCall(LinuxNativeControl* c, uint index,
            LinuxSyscallFrame call, LinuxSyscallResult result)
        {
            bool accept = call.Number is 43 or 288, read = call.Number is 0 or 45 or 47,
                send = call.Number is 1 or 20 or 44 or 46;
            if ((!accept && !read && !send) || result.Action != LinuxSyscallAction.Return ||
                result.Value < 0 || (!accept && result.Value == 0)) return true;
            if (c == null || c->State == null || !LinuxQuotaPolicy(c->State) || c->Phase != 3 || c->Published != 1 ||
                c->State->NetworkPhase != 2 || c->Selected == 0 || c->Selected > c->Capacity ||
                index != c->Selected - 1 || !c->Threads.Current.IsValid ||
                c->Threads.Current.Value != c->Plan.Bindings[index].Context.Identity ||
                c->Gates[index].Status != LinuxSyscallGateStatus.Active ||
                (accept ? result.Value > uint.MaxValue : result.Value > LinuxDescriptorServices.MaximumIo)) return false;
            Storage* state = c->State;
            uint fd = accept ? (uint)result.Value : (uint)call.Argument0;
            fixed (LinuxSocketStorage* o = &s_linuxSocketStorage)
            {
                if (o->Boot != state || o->Sockets.Descriptors != &state->LinuxDescriptors ||
                    !LinuxDescriptorServices.Capture(&state->LinuxDescriptors, fd, out var token) ||
                    !LinuxDescriptorServices.IsCurrent(&state->LinuxDescriptors, token)) return false;
                if (!LinuxDescriptorServices.SocketDescription(&state->LinuxDescriptors, fd, out uint endpoint, out _))
                    return !accept && call.Number is 0 or 1 or 20; // Ordinary output/file/pipe calls are not sockets.
                uint description = state->LinuxDescriptors.Owner.Slots[fd].Description;
                if (description == 0 || description > state->LinuxDescriptors.Owner.DescriptionCount ||
                    endpoint >= o->Sockets.Count || o->Sockets.Slots != (LinuxSocketServices.Endpoint*)o->Endpoints ||
                    o->Sockets.Initialized != 1 || o->Network.Initialized != 1 || o->Network.Failed != 0 ||
                    o->Network.Count != LinuxSocketConnections || o->Network.Slots != (SocketNetwork.Slot*)o->Slots ||
                    o->Network.Table != &o->Table || o->Network.Sockets != &o->Sockets) return false;
                description--; // Descriptor storage names descriptions as index+1; the row records the actual zero-based index.
                var d = state->LinuxDescriptors.Owner.Descriptions + description;
                var e = o->Sockets.Slots + endpoint;
                if (d->Type != (uint)LinuxDescriptorServices.Kind.Socket || d->Pipe != endpoint ||
                    d->Inode == 0 || e->Phase != 5) return false; // Connected, including retained peer-reset reads.
                uint slotIndex = LinuxSocketConnections;
                for (uint i = 0; i < LinuxSocketConnections; i++)
                {
                    var candidate = o->Network.Slots + i;
                    if (&candidate->Sender != e->Sender || &candidate->Receiver != e->Receiver) continue;
                    if (slotIndex != LinuxSocketConnections) return false;
                    slotIndex = i;
                }
                if (slotIndex == LinuxSocketConnections) return false;
                var slot = o->Network.Slots + slotIndex;
                var sender = &slot->Sender; var receiver = &slot->Receiver;
                var handle = sender->Handle;
                var held = (LinuxSocketLifetime*)o->Lifetimes + slotIndex;
                if (slot->Phase != 3 || sender->Table != &o->Table || receiver->Table != &o->Table ||
                    sender->Receiver != receiver || !LinuxQuotaSocketHandleEquals(handle, receiver->Handle) ||
                    !LinuxQuotaSocketHandleEquals(handle, slot->Handshake.Connection) ||
                    held->Active != 1 || !LinuxQuotaSocketHandleEquals(handle, held->Handle) ||
                    TcpConnectionTable.ReadOwned(&o->Table, handle, out var entry) != TcpConnectionTable.OwnershipStatus.Success ||
                    !held->Key.HasSameEndpoint(entry.Key) ||
                    entry.ReceiveOwnerAddress != (ulong)receiver || entry.SendOwnerAddress != (ulong)sender ||
                    TcpSendWindow.Validate(sender, out _, entry.State == TcpConnectionLifecycleState.Closed, true) != TcpSendWindow.Status.Success ||
                    TcpReceiveWindow.Validate(receiver, out _, receiver->PeerClosed != 0 ||
                        sender->CloseState == TcpConnectionCloseState.Reset) != TcpReceiveWindow.Status.Success) return false;
                ulong bytes = accept ? 0 : (ulong)result.Value;
                ulong flags = call.Number is 44 or 45 ? call.Argument3 : call.Number is 46 or 47 ? call.Argument2 : 0;
                bool peek = read && (flags & 2) != 0;
                ulong offset = read ? held->AppReceived : send ? held->AppSent : 0;
                ulong sent = held->AppSent, received = held->AppReceived;
                if (accept && (sent != 0 || received != 0) || bytes > ~0UL - offset) return false;
                if (send) sent += bytes;
                else if (read && !peek) received += bytes;
                if (!LinuxText(state, "M16_016_SOCKET identity=") || !LinuxNumber(state, c->Threads.Current.Value) ||
                    !LinuxText(state, " ordinal=") || !LinuxNumber(state, LinuxNativeCallOrdinal(c, index)) ||
                    !LinuxText(state, " nr=") || !LinuxNumber(state, call.Number) ||
                    !LinuxText(state, " fd=") || !LinuxNumber(state, fd) ||
                    !LinuxText(state, " fdGeneration=") || !LinuxNumber(state, token.Generation) ||
                    !LinuxText(state, " description=") || !LinuxNumber(state, description) ||
                    !LinuxText(state, " inode=") || !LinuxNumber(state, d->Inode) ||
                    !LinuxText(state, " slot=") || !LinuxNumber(state, slotIndex) ||
                    !LinuxText(state, " owner=") || !LinuxNumber(state, handle.OwnerAddress) ||
                    !LinuxText(state, " epoch=") || !LinuxNumber(state, handle.Epoch) ||
                    !LinuxText(state, " generation=") || !LinuxNumber(state, handle.Generation) ||
                    !LinuxText(state, " index=") || !LinuxNumber(state, handle.Index) ||
                    !LinuxText(state, " local=") || !LinuxNumber(state, PackLinuxSocketAddress(entry.Key.LocalAddress)) ||
                    !LinuxText(state, " localPort=") || !LinuxNumber(state, entry.Key.LocalPort) ||
                    !LinuxText(state, " remote=") || !LinuxNumber(state, PackLinuxSocketAddress(entry.Key.RemoteAddress)) ||
                    !LinuxText(state, " remotePort=") || !LinuxNumber(state, entry.Key.RemotePort) ||
                    !LinuxText(state, " offset=") || !LinuxNumber(state, offset) ||
                    !LinuxText(state, " bytes=") || !LinuxNumber(state, bytes) ||
                    !LinuxText(state, " peek=") || !LinuxNumber(state, peek ? 1UL : 0UL) ||
                    !LinuxText(state, " sent=") || !LinuxNumber(state, sent) ||
                    !LinuxText(state, " received=") || !LinuxNumber(state, received) ||
                    !LinuxText(state, "\r\n")) return false;
                held->AppSent = sent; held->AppReceived = received;
                return true;
            }
        }

        private static bool LinuxQuotaSocketHandleEquals(TcpConnectionTable.Handle first, TcpConnectionTable.Handle second) =>
            first.OwnerAddress == second.OwnerAddress && first.Epoch == second.Epoch &&
            first.Generation == second.Generation && first.Index == second.Index;
#endif

#if M10_004_PROBE && !M16_014_PROBE
        // Finite web controls retain seven TIME-WAIT tuples while concurrent
        // control/race/churn roles and a replacement connection remain live.
        private const uint LinuxSocketConnections = 16;
#else
        private const uint LinuxSocketConnections = 8;
#endif
        private const uint LinuxSocketReceive = 8192, LinuxSocketSend = 32768;
        // Borrowed observations only. A release is reported after the original
        // handle becomes stale and its network slot disappears or is reused.
        private struct LinuxSocketLifetime
        {
            internal TcpConnectionTable.Handle Handle;
            internal TcpConnectionKey Key;
            internal ulong MaximumDeadline, Duration;
            internal uint Active, WaitObserved, DrainObserved, TimeoutObserved;
#if M16_014_PROBE
            internal uint LastCloseObserved, QuotaLeaseObserved;
            internal ulong AppSent, AppReceived;
#endif
        }
        // Image-resident ownership outlives every borrowed callback, including a
        // rejected native transfer. Never reuse or free this storage after fault.
        private struct LinuxSocketStorage
        {
            internal Storage* Boot;
            internal NetDevice.State* Device;
            internal LinuxSocketServices.State Sockets;
            internal LinuxEpollServices.State Epoll;
            internal SocketNetwork.State Network;
            internal TcpConnectionTable.Owner Table;
            internal MacAddress Peer;
            internal uint Phase, Failed, Draining;
            internal ulong Polls, Received, Copied, Started;
            internal ulong ResolveSent, ResolveReceived, ResolveStarted, ResolveEnded;
            internal ulong ArpReceived, Accepted, Ignored, Capacity, ArpCopied;
            internal ulong Fresh, Retired, Ended, MaximumDeadline;
#if M16_014_PROBE
            internal ulong CompactTransfers, CompactFresh, CompactRetired;
            internal uint BulkHighWater, CompactHighWater;
            internal uint QuotaLeasePublished;
            internal ulong QuotaLeaseAcquired, QuotaLeaseReturned;
            internal ulong QuotaBindingAcquired, QuotaBindingReturned;
            internal fixed ulong CompactGenerations[64], CompactDeadlines[64];
#endif
            internal fixed byte Endpoints[2048], Ports[32], Registrations[2560];
            internal fixed byte Slots[1024 * (int)LinuxSocketConnections], Entries[128 * (int)LinuxSocketConnections];
            internal fixed ulong Generations[(int)LinuxSocketConnections];
            internal fixed ulong Lifetimes[16 * (int)LinuxSocketConnections];
            internal fixed ulong WakeIdentities[(int)LinuxNativeWorkers], WakeSequences[(int)LinuxNativeWorkers];
            internal fixed byte Buffers[((int)LinuxSocketReceive + (int)LinuxSocketSend) * (int)LinuxSocketConnections];
            internal fixed byte Input[1536], Output[1536];
        }
        private static LinuxSocketStorage s_linuxSocketStorage;
#if M16_027_PROBE
        // The whole direct IO carrier is separate from transport metadata and
        // the user-copy carrier. Original Boot/NIC and all opaque referents
        // retain their independent native mappings and lifetime obligations.
        private struct LinuxSocketIoCarrier
        {
            internal ulong Self, NetworkArena;
            internal Storage* Boot;
            internal LinuxSocketStorage* Owner;
            internal NetDevice.State* Device;
        }
        private static LinuxSocketIoCarrier s_linuxSocketIoCarrier;

        private static void* PrepareLinuxSocketIoCarrier(Storage* state, LinuxSocketStorage* owner)
        {
            fixed (Storage* original = &s_storage)
            fixed (LinuxSocketStorage* network = &s_linuxSocketStorage)
            fixed (LinuxSocketIoCarrier* carrier = &s_linuxSocketIoCarrier)
            {
                if (state != original || owner != network || network->Boot != state || network->Phase != 1 ||
                    network->Device == null || !network->Device->Running || state->NetworkArena < Page ||
                    state->Memory.References.NetworkArenaStart != state->NetworkArena ||
                    carrier->Self != 0 || carrier->Boot != null || carrier->Owner != null || carrier->Device != null ||
                    carrier->NetworkArena != 0) return null;
                carrier->Boot = state; carrier->Owner = network; carrier->Device = network->Device;
                carrier->NetworkArena = state->NetworkArena; carrier->Self = (ulong)carrier;
                return carrier;
            }
        }

        private static LinuxSocketStorage* ResolveLinuxSocketIo(void* context)
        {
            fixed (Storage* original = &s_storage)
            fixed (LinuxSocketStorage* network = &s_linuxSocketStorage)
            fixed (LinuxSocketIoCarrier* carrier = &s_linuxSocketIoCarrier)
            {
                if (context != carrier || carrier->Self != (ulong)carrier || carrier->Boot != original ||
                    carrier->Owner != network || carrier->Device == null || carrier->Device != network->Device ||
                    network->Boot != original || carrier->NetworkArena != original->NetworkArena ||
                    original->Memory.References.NetworkArenaStart != carrier->NetworkArena ||
                    network->Phase is not (1 or 2) || original->NetworkPhase != 2) return null;
                return network;
            }
        }
#endif

        private static bool LinuxSocketImageMapped(SeparatePageFaultMappingRange* ranges, ulong count)
        {
            fixed (LinuxSocketStorage* owner = &s_linuxSocketStorage)
#if M16_027_PROBE
            fixed (LinuxSocketIoCarrier* io = &s_linuxSocketIoCarrier)
            fixed (LinuxUserCarrier* user = &s_linuxUserCarrier)
                return FirstExitImageMappings.Contains(ranges, count, (ulong)io, (ulong)sizeof(LinuxSocketIoCarrier),
                    PageProtection.Read | PageProtection.Write) &&
                    FirstExitImageMappings.Contains(ranges, count, (ulong)user, (ulong)sizeof(LinuxUserCarrier),
                    PageProtection.Read | PageProtection.Write) &&
                    FirstExitImageMappings.Contains(ranges, count, (ulong)owner, (ulong)sizeof(LinuxSocketStorage),
                    PageProtection.Read | PageProtection.Write);
#else
                return FirstExitImageMappings.Contains(ranges, count, (ulong)owner, (ulong)sizeof(LinuxSocketStorage),
                    PageProtection.Read | PageProtection.Write);
#endif
        }

        private static bool RunLinuxSocketNetwork(Storage* state, NetDevice.State* device)
        {
            fixed (LinuxSocketStorage* o = &s_linuxSocketStorage)
            {
                if (o->Phase != 0 || !device->Running || state->CooperativePhase != 0 ||
                    !ReadNetworkTime(state, out o->Started)) return false;
                o->Boot = state; o->Device = device; o->Phase = 1;
                if (!ResolveLinuxSocketPeer(o)) return false;
#if M10_004_PROBE && !M16_014_PROBE && !M10_017_PROBE && !M10_018_PROBE
                if (!LinuxText(state, "TEST_START m10.004\r\n")) return LinuxWebRetainFailure(state, null, 2);
                bool ran = BootSequence.RunOwnedLinuxSockets(state->LinuxStartupPhase == 6,
                    state->ServiceLoop.IsStopped, state->Arena.Transition->Mode == FirmwareServicesMode.BootServicesExited &&
                    state->Arena.Transition->RetainedFirmwareCallbacksCleared && state->Arena.Binding->RetainedPointersCleared,
                    state->ApicPhase == 2, device->Running, state, &RunOwnedLinuxSyscalls);
                return LinuxWebRetainFailure(state, null, ran ? 3U : 4U);
#else
                if (!LinuxText(state, "TEST_START m16-011\r\n")) return false;
#if M16_012_PROBE
                if (!LinuxText(state, "TEST_START m16-012\r\n")) return false;
#endif
#if M16_013_PROBE
                if (!LinuxText(state, "TEST_START m16-013\r\n")) return false;
#endif
#if M07_027_PROBE && M07_028_PROBE
                if (!LinuxText(state, "TEST_START m07.027\r\nTEST_START m07.028\r\n")) return false;
#endif
                bool ran = CompleteLinuxRandomLifetime(state,
#if M10_018_PROBE
                    BootSequence.RunOwnedDiskStaticFileApplication(state->LinuxStartupPhase == 6,
#elif M10_017_PROBE
                    BootSequence.RunOwnedStaticFileApplication(state->LinuxStartupPhase == 6,
#else
                    BootSequence.RunOwnedLinuxSockets(state->LinuxStartupPhase == 6,
#endif
                    state->ServiceLoop.IsStopped, state->Arena.Transition->Mode == FirmwareServicesMode.BootServicesExited &&
                    state->Arena.Transition->RetainedFirmwareCallbacksCleared && state->Arena.Binding->RetainedPointersCleared,
                    state->ApicPhase == 2, device->Running,
#if M10_018_PROBE
                    state->Memory.References.NativeDiskArenaStart != 0 && state->Memory.References.NativeDiskArenaPages != 0,
#endif
                    state, &RunOwnedLinuxSyscalls));
                bool completed = ran;
#if M16_014_PROBE
                if (!ran && LinuxQuotaPolicy(state) && state->LinuxQuota == LinuxQuotaDisposition.CpuRestored &&
                    state->LinuxSyscallExitCode == 99 && state->LinuxSyscallFailed == 0 && state->LinuxSyscallFaults == 0)
                    completed = true; // Continue only the verified negative owner's remaining NIC teardown.
#endif
                return completed && o->Phase == 3 && o->Failed == 0 &&
                    LinuxText(state, "M16_011_WIRE received=") && LinuxNumber(state, o->Received) &&
                    LinuxText(state, " arp=") && LinuxNumber(state, o->ArpReceived) &&
                    LinuxText(state, " accepted=") && LinuxNumber(state, o->Accepted) &&
                    LinuxText(state, " ignored=") && LinuxNumber(state, o->Ignored) &&
                    LinuxText(state, " capacity=") && LinuxNumber(state, o->Capacity) &&
                    LinuxText(state, " arpCopied=") && LinuxNumber(state, o->ArpCopied) &&
                    LinuxText(state, " tcpCopied=") && LinuxNumber(state, o->Copied) && LinuxText(state, "\r\n") &&
                    LinuxText(state, "M16_011_END polls=") && LinuxNumber(state, o->Polls) &&
                    LinuxText(state, " received=") && LinuxNumber(state, o->Received) &&
                    LinuxText(state, " copied=") && LinuxNumber(state, o->Copied) &&
                    LinuxText(state, " fresh=") && LinuxNumber(state, o->Fresh) &&
                    LinuxText(state, " retired=") && LinuxNumber(state, o->Retired) &&
                    LinuxText(state, " start=") && LinuxNumber(state, o->Started) &&
                    LinuxText(state, " end=") && LinuxNumber(state, o->Ended) &&
                    LinuxText(state, " maxDeadline=") && LinuxNumber(state, o->MaximumDeadline) &&
                    LinuxText(state, " retained=0 buffers=scrubbed descriptors=closed workers=reaped\r\n");
#endif
            }
        }

        private static bool InitializeLinuxSocketNetwork(Storage* state)
        {
            fixed (LinuxSocketStorage* o = &s_linuxSocketStorage)
            {
                if (o->Boot != state || o->Phase != 1 || o->Device == null || !o->Device->Running ||
                    state->LinuxView.Manifest.Ipv4 != 0 || state->LinuxView.Manifest.Port != 8080 ||
                    sizeof(LinuxSocketServices.Endpoint) * 16 > 2048 || sizeof(LinuxEpollServices.Port) * 4 > 32 ||
                    sizeof(LinuxEpollServices.Registration) * 64 > 2560 || sizeof(SocketNetwork.Slot) > 1024 ||
                    sizeof(TcpConnectionTableEntry) > 128 || (sizeof(LinuxSocketLifetime) & 7) != 0 ||
                    sizeof(LinuxSocketLifetime) > 16 * sizeof(ulong) ||
                    !ReadNetworkTime(state, out ulong now)) return false;
                if (!LinuxSocketServices.Begin(&o->Sockets, &state->LinuxDescriptors, (LinuxSocketServices.Endpoint*)o->Endpoints,
                        16, new(10, 0, 2, 15), 8080) ||
                    !LinuxEpollServices.Begin(&o->Epoll, &state->LinuxDescriptors, (LinuxEpollServices.Port*)o->Ports, 4,
                        (LinuxEpollServices.Registration*)o->Registrations, 64) ||
                    TcpConnectionTable.InitializeOwned(&o->Table, (TcpConnectionTableEntry*)o->Entries, LinuxSocketConnections,
                        o->Generations, LinuxSocketConnections, state->NetworkArena ^ 0x4c534f434b455453UL) != TcpConnectionTable.OwnershipStatus.Success) return false;
#if !M16_027_PROBE
                if (!LinuxSocketServices.AttachMessageObserver(&o->Sockets, state, &ObserveLinuxSocketMessage)) return false;
#endif
                // A finite diagnostic seed from the admitted clock, not a
                // production unpredictable TCP initial-sequence generator.
#if M16_014_PROBE
                uint sequence = 0;
                TcpInitialSequence.Owner* sequenceOwner = &state->LinuxServiceSequence;
#else
                uint sequence = ((uint)now & 0x3fff0000) | 0x10000;
                TcpInitialSequence.Owner* sequenceOwner = null;
#endif
                var io = new TcpPassiveConnection.Operations {
#if M16_027_PROBE
                    Context = PrepareLinuxSocketIoCarrier(state, o),
#else
                    Context = o,
#endif
                    NowNanoseconds = &LinuxSocketNow, Send = &LinuxSocketSendFrame,
                    LifetimeNanoseconds = 10_000_000_000, RetryNanoseconds = 1_000_000_000, MaximumAttempts = 3 };
                if (!SocketNetwork.Begin(&o->Network, &o->Sockets, &o->Table, (SocketNetwork.Slot*)o->Slots, LinuxSocketConnections,
                    o->Buffers, LinuxSocketReceive, LinuxSocketSend, new(2, 0, 0, 0, 0, 1), o->Peer, new(10, 0, 2, 2), sequence, io,
                    compactTimeWait: Probes.EnableM16014Probe, sequence: sequenceOwner
#if M16_014_PROBE
                    , measureLeases: LinuxQuotaPolicy(state)
#endif
#if M16_027_PROBE
                    , ioContextBytes: (uint)sizeof(LinuxSocketIoCarrier)
#endif
                    )) return false;
#if M16_014_PROBE
                if (!LinuxText(state, "M07_033_BOUND quietUntil=") || !LinuxNumber(state, state->LinuxServiceSequence.QuietDeadline) ||
                    !LinuxText(state, " clock=") || !LinuxNumber(state, now) ||
                    !LinuxText(state, " tickNanos=4000 quietNanos=240000000000 secret=independent lease=network\r\n")) return false;
#endif
                o->Phase = 2;
#if M10_004_PROBE && !M16_014_PROBE
                return LinuxText(state, "M16_011_BOUND connections=16 rx=8192 tx=32768 epoll=4 watches=64 nic=live\r\n");
#else
                return LinuxText(state, "M16_011_BOUND connections=8 rx=8192 tx=32768 epoll=4 watches=64 nic=live\r\n");
#endif
            }
        }

        private static bool ResolveLinuxSocketPeer(LinuxSocketStorage* o)
        {
            Span<byte> tx = new(o->Output, 1514), rx = new(o->Input, 1514); tx.Clear();
            var localMac = new MacAddress(2, 0, 0, 0, 0, 1); var local = new Ipv4Address(10, 0, 2, 15);
            var request = new ArpPacket(ArpPacket.OperationRequest, localMac, local, default, new(10, 0, 2, 2));
            if (EthernetHeader.TryWrite(tx, new(new(255, 255, 255, 255, 255, 255), localMac, EthernetHeader.EtherTypeArp)) != PacketParseStatus.Success ||
                ArpPacket.TryWrite(tx.Slice(14), request, out _) != PacketParseStatus.Success || !ReadNetworkTime(o->Boot, out ulong now) || now > ~0UL - 2_000_000_000) return false;
            o->ResolveStarted = now;
            ulong deadline = now + 2_000_000_000; bool sent = false;
            for (uint poll = 0; poll < 1_000_000; poll++)
            {
                if (NetDevice.Poll(o->Device, 8, out _) != NetDevice.Status.Success) return false;
                if (!sent)
                {
                    var copy = NetDevice.Send(o->Device, tx.Slice(0, 60));
                    if (copy is not (NetDevice.Status.Success or NetDevice.Status.Full)) return false;
                    sent = copy == NetDevice.Status.Success;
                    if (sent) o->ResolveSent++;
                }
                var received = NetDevice.Receive(o->Device, rx, out uint bytes);
                if (received == NetDevice.Status.Success)
                {
                    o->ResolveReceived++;
                    if (NetworkArpReply(rx.Slice(0, (int)bytes), localMac, local, new(10, 0, 2, 2), out o->Peer))
                        return ReadNetworkTime(o->Boot, out o->ResolveEnded) &&
                            LinuxText(o->Boot, "M16_011_PEER mac=") && LinuxNumber(o->Boot, PackNetworkMac(o->Peer)) &&
                            LinuxText(o->Boot, " sent=") && LinuxNumber(o->Boot, o->ResolveSent) &&
                            LinuxText(o->Boot, " received=") && LinuxNumber(o->Boot, o->ResolveReceived) &&
                            LinuxText(o->Boot, " start=") && LinuxNumber(o->Boot, o->ResolveStarted) &&
                            LinuxText(o->Boot, " end=") && LinuxNumber(o->Boot, o->ResolveEnded) && LinuxText(o->Boot, "\r\n");
                }
                if (received is not (NetDevice.Status.Success or NetDevice.Status.Empty) || !ReadNetworkTime(o->Boot, out now) || now >= deadline) return false;
            }
            return false;
        }

        private static bool PumpLinuxSocketFrames(LinuxSocketStorage* o)
        {
            if (o->Phase != 2 || o->Failed != 0 || !o->Device->Running ||
                NetDevice.Poll(o->Device, 8, out _) != NetDevice.Status.Success || !ObserveLinuxSocketLifetimes(o)) return false;
            Span<byte> input = new(o->Input, 1514), output = new(o->Output, 1514); o->Polls++;
            for (uint i = 0; i < 8; i++)
            {
                ulong dropped = o->Device->DroppedFrames;
                ushort available = o->Device->Receipt.Receive.AvailableIndex;
                var received = NetDevice.Receive(o->Device, input, out uint bytes);
                if (received is not (NetDevice.Status.Success or NetDevice.Status.Empty)) return false;
                if (o->Device->DroppedFrames != dropped)
                {
                    // Observe the real device transition after ownership/fences
                    // and queue notification. This does not manufacture a drop
                    // or change a completion, packet, timer or scheduler state.
                    ushort after = o->Device->Receipt.Receive.AvailableIndex;
                    if (!o->Device->Running || o->Device->DroppedFrames < dropped || after == available ||
                        !ReadNetworkTime(o->Boot, out ulong now) ||
                        !LinuxText(o->Boot, "M07_028_DROP before=") || !LinuxNumber(o->Boot, dropped) ||
                        !LinuxText(o->Boot, " after=") || !LinuxNumber(o->Boot, o->Device->DroppedFrames) ||
                        !LinuxText(o->Boot, " availableBefore=") || !LinuxNumber(o->Boot, available) ||
                        !LinuxText(o->Boot, " availableAfter=") || !LinuxNumber(o->Boot, after) ||
                        !LinuxText(o->Boot, " delivered=") || !LinuxNumber(o->Boot, received == NetDevice.Status.Success ? 1UL : 0UL) ||
                        !LinuxText(o->Boot, " clock=") || !LinuxNumber(o->Boot, now) ||
                        !LinuxText(o->Boot, " running=1\r\n")) return false;
                }
                if (received == NetDevice.Status.Empty) break;
                o->Received++; var frame = input.Slice(0, (int)bytes);
                if (EthernetHeader.TryParse(frame, out var ethernet, out var payload) == PacketParseStatus.Success &&
                    ethernet.EtherType == EthernetHeader.EtherTypeArp)
                {
                    o->ArpReceived++;
                    if (ArpPacket.TryParse(payload, out var arp) == PacketParseStatus.Success && arp.Operation == ArpPacket.OperationRequest &&
                        arp.SenderMac == o->Peer && arp.SenderAddress == new Ipv4Address(10, 0, 2, 2) && arp.TargetAddress == new Ipv4Address(10, 0, 2, 15))
                    {
                        output.Clear(); var mac = new MacAddress(2, 0, 0, 0, 0, 1);
                        if (EthernetHeader.TryWrite(output, new(o->Peer, mac, EthernetHeader.EtherTypeArp)) != PacketParseStatus.Success ||
                            ArpPacket.TryWrite(output.Slice(14), new(ArpPacket.OperationReply, mac, arp.TargetAddress, o->Peer, arp.SenderAddress), out _) != PacketParseStatus.Success) return false;
                        var copied = NetDevice.Send(o->Device, output.Slice(0, 60));
                        if (copied is not (NetDevice.Status.Success or NetDevice.Status.Full)) return false;
                        if (copied == NetDevice.Status.Success) o->ArpCopied++;
                    }
                    continue;
                }
                var accepted = SocketNetwork.Receive(&o->Network, frame, output);
                if (accepted == SocketNetwork.Status.Success) o->Accepted++;
                else if (accepted == SocketNetwork.Status.Ignored) o->Ignored++;
                else if (accepted == SocketNetwork.Status.Capacity) o->Capacity++;
                else return false;
                if (!ObserveLinuxSocketLifetimes(o)) return false;
            }
            return ObserveLinuxSocketLifetimes(o) && SocketNetwork.Pump(&o->Network, output) == SocketNetwork.Status.Success &&
                o->Failed == 0 && ObserveLinuxSocketLifetimes(o);
        }

        // Serialized snapshots surround each transport mutation. Fixed storage
        // tracks at most one current lifetime per network slot; no packet logs,
        // callbacks, table mutations or synthetic retirement are introduced.
        private static bool ObserveLinuxSocketLifetimes(LinuxSocketStorage* o)
        {
            if (o->Network.Count != LinuxSocketConnections || !ReadNetworkTime(o->Boot, out ulong now)) return false;
            var observations = (LinuxSocketLifetime*)o->Lifetimes;
#if M16_014_PROBE
            uint bulk = 0;
            uint priorBulk = o->BulkHighWater, priorCompact = o->CompactHighWater;
#endif
            for (uint i = 0; i < LinuxSocketConnections; i++)
            {
                var slot = o->Network.Slots + i; var held = observations + i;
                var handle = slot->Handshake.Connection;
                bool same = held->Active != 0 && handle.OwnerAddress == held->Handle.OwnerAddress &&
                    handle.Epoch == held->Handle.Epoch && handle.Generation == held->Handle.Generation && handle.Index == held->Handle.Index;
                if (held->Active != 0 && (slot->Phase == 0 || !same))
                {
#if M16_014_PROBE
                    // Read the actual retained tuple before recording bulk retirement.
                    // A compact transfer preserves its original MSL deadline.
                    var compact = TcpTimeWaitTable.Find(&o->Network.TimeWait, held->Key, out var retained, out var record);
                    if (compact == TcpTimeWaitTable.Status.Success)
                    {
                        if (record.DeadlineNanoseconds < held->MaximumDeadline ||
                            record.DurationNanoseconds < TcpTimeWaitTable.MinimumDurationNanoseconds ||
                            !WriteLinuxSocketLifetime(o, "M07_030_TRANSFER", i, held, now) ||
                            !LinuxText(o->Boot, " compactIndex=") || !LinuxNumber(o->Boot, retained.Index) ||
                            !LinuxText(o->Boot, " compactGeneration=") || !LinuxNumber(o->Boot, retained.Generation) ||
                            !LinuxText(o->Boot, " originalDeadline=") || !LinuxNumber(o->Boot, record.OriginalDeadlineNanoseconds) ||
                            !LinuxText(o->Boot, " deadline=") || !LinuxNumber(o->Boot, record.DeadlineNanoseconds) ||
                            !LinuxText(o->Boot, " duration=") || !LinuxNumber(o->Boot, record.DurationNanoseconds) ||
                            !LinuxText(o->Boot, "\r\n") || o->CompactTransfers == ~0UL) return false;
                        held->MaximumDeadline = record.DeadlineNanoseconds; held->Duration = record.DurationNanoseconds;
                        o->CompactTransfers++;
                    }
                    else if (compact != TcpTimeWaitTable.Status.NotFound) return false;
#endif
                    if (TcpConnectionTable.ReadOwned(&o->Table, held->Handle, out _) != TcpConnectionTable.OwnershipStatus.StaleHandle) return false;
#if M16_014_PROBE
                    if (LinuxQuotaPolicy(o->Boot) && held->QuotaLeaseObserved != 0 && !WriteLinuxQuotaSocketLease(o, i, held, false, false)) return false;
#endif
                    if (!WriteLinuxSocketLifetime(o, "M16_011_SLOT event=1", i, held, now) ||
                        !LinuxText(o->Boot, " deadline=") || !LinuxNumber(o->Boot, held->MaximumDeadline) ||
                        !LinuxText(o->Boot, " duration=") || !LinuxNumber(o->Boot, held->Duration) || !LinuxText(o->Boot, "\r\n")) return false;
                    o->Retired++; *held = default;
                }
                if (slot->Phase == 0) continue;
#if M16_014_PROBE
                bulk++;
#endif
                if (handle.OwnerAddress != (ulong)&o->Table ||
                    TcpConnectionTable.ReadOwned(&o->Table, handle, out var entry) != TcpConnectionTable.OwnershipStatus.Success) return false;
                if (held->Active == 0)
                {
#if M16_014_PROBE
                    if (held->AppSent != 0 || held->AppReceived != 0) return false;
#endif
                    held->Handle = handle; held->Key = entry.Key; held->Active = 1;
                    if (!WriteLinuxSocketLifetime(o, "M16_011_SLOT event=0", i, held, now) ||
                        !LinuxText(o->Boot, " deadline=0 duration=0\r\n")) return false;
                    o->Fresh++;
                }
                else if (!held->Key.HasSameEndpoint(entry.Key)) return false;
#if M16_014_PROBE
                // Pending SYN reservations have no RX/TX loan. The surrounding
                // Receive/Pump snapshots expose every successful establishment
                // before another transport mutation can release that generation.
                if (LinuxQuotaPolicy(o->Boot) && slot->Phase >= 2 && held->QuotaLeaseObserved == 0 &&
                    (slot->LeaseGeneration != handle.Generation || !WriteLinuxQuotaSocketLease(o, i, held, true, false))) return false;
                // Observe actual last-description closure before the next
                // receive/pump. SHUT_WR and live duplicate descriptors do not
                // satisfy IsClosing. The copied tuple/generation grants no
                // authority to mutate transport or descriptor ownership.
                if (slot->Phase == 3 && held->LastCloseObserved == 0 &&
                    LinuxSocketServices.IsClosing(&o->Sockets, &slot->Sender))
                {
                    if (!WriteLinuxSocketLifetime(o, "M07_034_CLOSE", i, held, now) ||
                        !LinuxText(o->Boot, " localNext=") || !LinuxNumber(o->Boot, slot->Sender.Next) ||
                        !LinuxText(o->Boot, " remoteNext=") || !LinuxNumber(o->Boot, slot->Receiver.RemoteNext) ||
                        !LinuxText(o->Boot, " unread=") || !LinuxNumber(o->Boot, (uint)slot->Receiver.UnreadBytes) ||
                        !LinuxText(o->Boot, "\r\n")) return false;
                    held->LastCloseObserved = 1;
                }
#endif
                if (slot->Phase >= 2 && slot->Sender.TimedOut && held->TimeoutObserved == 0)
                {
                    if (entry.State != TcpConnectionLifecycleState.Closed || slot->Sender.CloseState != TcpConnectionCloseState.Reset ||
                        o->Network.Failed != 0 || !WriteLinuxSocketLifetime(o, "M07_027_TIMEOUT", i, held, now) ||
                        !LinuxText(o->Boot, " sharedFailed=0\r\n")) return false;
                    held->TimeoutObserved = 1;
                }
                if (slot->Phase < 2 || slot->Sender.CloseState != TcpConnectionCloseState.TimeWait) continue;
                ulong deadline = slot->Sender.TimeWaitDeadlineNanoseconds, duration = slot->Sender.TimeWaitDuration;
                if (deadline == 0 || duration == 0) return false;
                if (deadline > held->MaximumDeadline) held->MaximumDeadline = deadline;
                if (deadline > o->MaximumDeadline) o->MaximumDeadline = deadline;
                held->Duration = duration;
                bool draining = o->Draining != 0;
                if (held->WaitObserved != 0 && (!draining || held->DrainObserved != 0)) continue;
                if (!WriteLinuxSocketLifetime(o, "M16_011_TIMEWAIT", i, held, now) ||
                    !LinuxText(o->Boot, " deadline=") || !LinuxNumber(o->Boot, deadline) ||
                    !LinuxText(o->Boot, " duration=") || !LinuxNumber(o->Boot, duration) ||
                    !LinuxText(o->Boot, " drain=") || !LinuxNumber(o->Boot, draining ? 1UL : 0UL) || !LinuxText(o->Boot, "\r\n")) return false;
                held->WaitObserved = 1; if (draining) held->DrainObserved = 1;
            }
#if M16_014_PROBE
            if (bulk > o->BulkHighWater) o->BulkHighWater = bulk;
            if (!ObserveLinuxCompactTimeWait(o, now)) return false;
            // Mark the completed serialized snapshot, only when a peak changes.
            // Equal clock reads cannot identify separate observation boundaries.
            // At most8+64 peak advances exist for this fixed owner lifetime.
            bool logged = priorBulk == o->BulkHighWater && priorCompact == o->CompactHighWater ||
                LinuxText(o->Boot, "M07_030_HIGHWATER bulk=") && LinuxNumber(o->Boot, o->BulkHighWater) &&
                LinuxText(o->Boot, " compact=") && LinuxNumber(o->Boot, o->CompactHighWater) &&
                LinuxText(o->Boot, " clock=") && LinuxNumber(o->Boot, now) && LinuxText(o->Boot, "\r\n");
            return logged && (!LinuxQuotaPolicy(o->Boot) || ObserveLinuxQuotaSocketLeases(o, false));
#else
            return true;
#endif
        }

#if M16_014_PROBE
        private static bool WriteLinuxQuotaSocketLease(LinuxSocketStorage* o, uint index, LinuxSocketLifetime* held, bool acquired, bool end)
        {
            if (index >= LinuxSocketConnections || held->Active != 1 || held->QuotaLeaseObserved != (acquired ? 0U : 1U) ||
                held->Handle.OwnerAddress != (ulong)&o->Table || held->Handle.Index >= LinuxSocketConnections ||
                held->Handle.Generation == 0 || acquired && end ||
                (acquired ? o->QuotaBindingAcquired : o->QuotaBindingReturned) == ~0UL) return false;
            if (!LinuxText(o->Boot, "M16_016_SOCKET_LEASE event=") || !LinuxNumber(o->Boot, acquired ? 0U : 1U) ||
                !LinuxText(o->Boot, " slot=") || !LinuxNumber(o->Boot, index) ||
                !LinuxText(o->Boot, " owner=") || !LinuxNumber(o->Boot, held->Handle.OwnerAddress) ||
                !LinuxText(o->Boot, " epoch=") || !LinuxNumber(o->Boot, held->Handle.Epoch) ||
                !LinuxText(o->Boot, " generation=") || !LinuxNumber(o->Boot, held->Handle.Generation) ||
                !LinuxText(o->Boot, " index=") || !LinuxNumber(o->Boot, held->Handle.Index) ||
                !LinuxText(o->Boot, " rxBytes=") || !LinuxNumber(o->Boot, LinuxSocketReceive) ||
                !LinuxText(o->Boot, " txBytes=") || !LinuxNumber(o->Boot, LinuxSocketSend) ||
                !LinuxText(o->Boot, " end=") || !LinuxNumber(o->Boot, end ? 1U : 0U) || !LinuxText(o->Boot, "\r\n")) return false;
            held->QuotaLeaseObserved = acquired ? 1U : 0U;
            if (acquired) o->QuotaBindingAcquired++; else o->QuotaBindingReturned++;
            return true;
        }

        // Static image backing is retained separately from measured borrower
        // peaks. Loans released between observations remain in the counters.
        private static bool ObserveLinuxQuotaSocketLeases(LinuxSocketStorage* o, bool end)
        {
            if (o == null || !LinuxQuotaPolicy(o->Boot) || o->Phase != 2 || o->Failed != 0 || o->QuotaLeasePublished > 1 ||
                o->Network.Buffers != o->Buffers || o->Network.Count != LinuxSocketConnections ||
                o->Network.ReceiveBytes != LinuxSocketReceive || o->Network.SendBytes != LinuxSocketSend ||
                !SocketNetwork.TryObserveLeases(&o->Network, out var lease) || lease.Ended != end ||
                lease.BackingBytes != 327680 || lease.Acquired < o->QuotaLeaseAcquired || lease.Returned < o->QuotaLeaseReturned) return false;
            if (end)
            {
                var c = (LinuxNativeControl*)(o->Boot->CooperativePool + LinuxThreadProgram.MetadataPage * Page);
                if (o->Boot->CooperativePhase != 5 || c->State != o->Boot || c->Phase != 5 ||
                    c->Key != (o->Boot->CooperativePool ^ 0x4d31363030384f57UL) || c->QuotaStackEnded != 1 ||
                    c->QuotaStackAcquired != c->Reaped || lease.Leases != 0 || lease.Acquired != lease.Returned) return false;
                var held = (LinuxSocketLifetime*)o->Lifetimes;
                for (uint i = 0; i < LinuxSocketConnections; i++)
                    if (held[i].QuotaLeaseObserved != 0 && !WriteLinuxQuotaSocketLease(o, i, held + i, false, true)) return false;
            }
            if (lease.Acquired != o->QuotaBindingAcquired || lease.Returned != o->QuotaBindingReturned) return false;
            if (!end && o->QuotaLeasePublished != 0 && lease.Acquired == o->QuotaLeaseAcquired && lease.Returned == o->QuotaLeaseReturned) return true;
            if (!LinuxText(o->Boot, "M16_016_SOCKET_LEASES backingBytes=") || !LinuxNumber(o->Boot, lease.BackingBytes) ||
                !LinuxText(o->Boot, " acquired=") || !LinuxNumber(o->Boot, lease.Acquired) ||
                !LinuxText(o->Boot, " returned=") || !LinuxNumber(o->Boot, lease.Returned) ||
                !LinuxText(o->Boot, " live=") || !LinuxNumber(o->Boot, lease.Leases) ||
                !LinuxText(o->Boot, " rxBytes=") || !LinuxNumber(o->Boot, lease.ReceiveBytes) ||
                !LinuxText(o->Boot, " txBytes=") || !LinuxNumber(o->Boot, lease.SendBytes) ||
                !LinuxText(o->Boot, " peakLive=") || !LinuxNumber(o->Boot, lease.PeakLeases) ||
                !LinuxText(o->Boot, " peakRxBytes=") || !LinuxNumber(o->Boot, lease.PeakReceiveBytes) ||
                !LinuxText(o->Boot, " peakTxBytes=") || !LinuxNumber(o->Boot, lease.PeakSendBytes) ||
                !LinuxText(o->Boot, " end=") || !LinuxNumber(o->Boot, end ? 1U : 0U) ||
                !LinuxText(o->Boot, " backing=retained\r\n")) return false;
            o->QuotaLeasePublished = end ? 2U : 1U; o->QuotaLeaseAcquired = lease.Acquired; o->QuotaLeaseReturned = lease.Returned;
            return true;
        }

        // Observe fixed metadata around each real transport mutation. Generation
        // changes require the preceding record's actual deadline to have passed;
        // duplicate FIN restarts remain visible after the bulk slot is reused.
        private static bool ObserveLinuxCompactTimeWait(LinuxSocketStorage* o, ulong now)
        {
            var owner = &o->Network.TimeWait;
            if (owner->Capacity != TcpTimeWaitTable.MaximumEntries) return false;
            uint active = 0;
            for (uint i = 0; i < owner->Capacity; i++)
            {
                ulong generation = owner->Entries[i].Generation, previous = o->CompactGenerations[i];
                if (previous != 0 && previous != generation)
                {
                    if (now < o->CompactDeadlines[i] || o->CompactRetired == ~0UL ||
                        !LinuxText(o->Boot, "M07_030_RECORD event=1 index=") || !LinuxNumber(o->Boot, i) ||
                        !LinuxText(o->Boot, " generation=") || !LinuxNumber(o->Boot, previous) ||
                        !LinuxText(o->Boot, " deadline=") || !LinuxNumber(o->Boot, o->CompactDeadlines[i]) ||
                        !LinuxText(o->Boot, " clock=") || !LinuxNumber(o->Boot, now) || !LinuxText(o->Boot, "\r\n")) return false;
                    o->CompactRetired++; o->CompactDeadlines[i] = 0;
                }
                if (generation != 0)
                {
                    var handle = new TcpTimeWaitTable.Handle(owner, (ushort)i, generation);
                    if (TcpTimeWaitTable.Read(owner, handle, out var record) != TcpTimeWaitTable.Status.Success ||
                        record.DeadlineNanoseconds < o->CompactDeadlines[i]) return false;
                    active++;
                    if (generation != previous || record.DeadlineNanoseconds != o->CompactDeadlines[i])
                    {
                        if (!LinuxText(o->Boot, "M07_030_RECORD event=") || !LinuxNumber(o->Boot, generation != previous ? 0UL : 2UL) ||
                            !LinuxText(o->Boot, " index=") || !LinuxNumber(o->Boot, i) ||
                            !LinuxText(o->Boot, " generation=") || !LinuxNumber(o->Boot, generation) ||
                            !LinuxText(o->Boot, " originalDeadline=") || !LinuxNumber(o->Boot, record.OriginalDeadlineNanoseconds) ||
                            !LinuxText(o->Boot, " deadline=") || !LinuxNumber(o->Boot, record.DeadlineNanoseconds) ||
                            !LinuxText(o->Boot, " duration=") || !LinuxNumber(o->Boot, record.DurationNanoseconds) ||
                            !LinuxText(o->Boot, " clock=") || !LinuxNumber(o->Boot, now) || !LinuxText(o->Boot, "\r\n")) return false;
                        if (generation != previous)
                        { if (o->CompactFresh == ~0UL) return false; o->CompactFresh++; }
                        o->CompactDeadlines[i] = record.DeadlineNanoseconds;
                    }
                    if (record.DeadlineNanoseconds > o->MaximumDeadline) o->MaximumDeadline = record.DeadlineNanoseconds;
                }
                o->CompactGenerations[i] = generation;
            }
            if (active > o->CompactHighWater) o->CompactHighWater = active;
            return active == owner->Count && o->CompactFresh == o->CompactTransfers;
        }
#endif

        private static bool WriteLinuxSocketLifetime(LinuxSocketStorage* o, string marker, uint index, LinuxSocketLifetime* held, ulong now) =>
            LinuxText(o->Boot, marker) && LinuxText(o->Boot, " slot=") && LinuxNumber(o->Boot, index) &&
            LinuxText(o->Boot, " owner=") && LinuxNumber(o->Boot, held->Handle.OwnerAddress) &&
            LinuxText(o->Boot, " epoch=") && LinuxNumber(o->Boot, held->Handle.Epoch) &&
            LinuxText(o->Boot, " generation=") && LinuxNumber(o->Boot, held->Handle.Generation) &&
            LinuxText(o->Boot, " index=") && LinuxNumber(o->Boot, held->Handle.Index) &&
            LinuxText(o->Boot, " local=") && LinuxNumber(o->Boot, PackLinuxSocketAddress(held->Key.LocalAddress)) &&
            LinuxText(o->Boot, " localPort=") && LinuxNumber(o->Boot, held->Key.LocalPort) &&
            LinuxText(o->Boot, " remote=") && LinuxNumber(o->Boot, PackLinuxSocketAddress(held->Key.RemoteAddress)) &&
            LinuxText(o->Boot, " remotePort=") && LinuxNumber(o->Boot, held->Key.RemotePort) &&
            LinuxText(o->Boot, " clock=") && LinuxNumber(o->Boot, now);

        private static uint PackLinuxSocketAddress(Ipv4Address address) => ((uint)address.Octet0 << 24) |
            ((uint)address.Octet1 << 16) | ((uint)address.Octet2 << 8) | address.Octet3;

        private static bool PollLinuxSocketNetwork(LinuxNativeControl* c,
#if M10_018_TIMING_PROBE
            NativeDiskPollCause cause,
#endif
            bool observe = true)
        {
#if M16_025_PROBE
            fixed (NativeDiskOwner* disk = &s_nativeDisk)
#endif
            fixed (LinuxSocketStorage* o = &s_linuxSocketStorage)
#if M10_018_TIMING_PROBE
            {
                bool timing = disk->TimingPhase == 1;
                bool pollTracked = disk->TimingPhase != 2;
                if (pollTracked && !disk->TimingDispatch.BeginPoll()) return false;
                uint bucket = (uint)cause;
                if (bucket >= 4) return false;
                ulong started = 0, afterPre = 0, afterDisk = 0, afterRefresh = 0, afterReadiness = 0, finished = 0;
                if (timing && !ReadNetworkTime(c->State, out started)) return false;
                if (o->Boot != c->State || observe && !RecordLinuxSocketPoll(c) ||
                    !RecordLinuxSocketWaits(o, c) || !PumpLinuxSocketFrames(o) || !c->Process.AdvanceFutexClock(c->Key, 2) ||
                    observe && !RecordLinuxSocketPoll(c)) return false;
                if (timing && (!ReadNetworkTime(c->State, out afterPre) || afterPre < started ||
                    disk->TimingPreNetwork > ~0UL - (afterPre - started) || disk->TimingNetworkCalls == ~0UL)) return false;
                if (disk->Boot != c->State || !c->Process.PollDiskReadiness(c->Key, 2,
                    new() { Context = disk, Clock = &NativeDiskClock, Flush = &NativeDiskQuantumFlush,
                        Measurements = timing ? &disk->TimingPump : null })) return false;
                if (timing && (!ReadNetworkTime(c->State, out afterDisk) || afterDisk < afterPre)) return false;
                if (timing)
                {
                    if (!c->Process.PollDescriptorReadinessMeasured(c->Key, 2, disk, &NativeDiskClock, out afterRefresh) ||
                        afterRefresh < afterDisk) return false;
                }
                else if (!c->Process.PollDescriptorReadiness(c->Key, 2)) return false;
                // One diagnostic-only clock partitions the existing parent
                // interval; its measurement overhead remains in that parent.
                if (timing && (!ReadNetworkTime(c->State, out afterReadiness) || afterReadiness < afterRefresh)) return false;
                if (!RecordLinuxSocketWaits(o, c)) return false;
                if (timing)
                {
                    if (!ReadNetworkTime(c->State, out finished) || finished < afterReadiness ||
                        disk->TimingPostNetwork > ~0UL - (finished - afterDisk) ||
                        disk->TimingPostReadiness > ~0UL - (afterReadiness - afterDisk) ||
                        disk->TimingReadinessRefresh > ~0UL - (afterRefresh - afterDisk) ||
                        disk->TimingReadinessWaitScan > ~0UL - (afterReadiness - afterRefresh) ||
                        disk->TimingPostWaitRecord > ~0UL - (finished - afterReadiness) ||
                        disk->TimingNetworkTotal > ~0UL - (finished - started) ||
                        disk->TimingDiskRound > ~0UL - (afterDisk - afterPre) ||
                        disk->TimingCauseCalls[bucket] == ~0UL ||
                        disk->TimingCauseTotal[bucket] > ~0UL - (finished - started) ||
                        disk->TimingCausePre[bucket] > ~0UL - (afterPre - started) ||
                        disk->TimingCauseDisk[bucket] > ~0UL - (afterDisk - afterPre) ||
                        disk->TimingCausePost[bucket] > ~0UL - (finished - afterDisk)) return false;
                    disk->TimingPreNetwork += afterPre - started;
                    disk->TimingPostNetwork += finished - afterDisk;
                    disk->TimingPostReadiness += afterReadiness - afterDisk;
                    disk->TimingReadinessRefresh += afterRefresh - afterDisk;
                    disk->TimingReadinessWaitScan += afterReadiness - afterRefresh;
                    disk->TimingPostWaitRecord += finished - afterReadiness;
                    disk->TimingNetworkCalls++;
                    disk->TimingNetworkTotal += finished - started;
                    disk->TimingDiskRound += afterDisk - afterPre;
                    disk->TimingCauseCalls[bucket]++;
                    disk->TimingCauseTotal[bucket] += finished - started;
                    disk->TimingCausePre[bucket] += afterPre - started;
                    disk->TimingCauseDisk[bucket] += afterDisk - afterPre;
                    disk->TimingCausePost[bucket] += finished - afterDisk;
                }
                return !pollTracked || disk->TimingDispatch.EndPoll();
            }
#else
                return o->Boot == c->State && (!observe || RecordLinuxSocketPoll(c)) &&
                    RecordLinuxSocketWaits(o, c) && PumpLinuxSocketFrames(o) && c->Process.AdvanceFutexClock(c->Key, 2) &&
                    (!observe || RecordLinuxSocketPoll(c)) &&
#if M16_025_PROBE
                    disk->Boot == c->State && c->Process.PollDiskReadiness(c->Key, 2,
                        new() { Context = disk, Clock = &NativeDiskClock, Flush = &NativeDiskQuantumFlush }) &&
#endif
                    c->Process.PollDescriptorReadiness(c->Key, 2) && RecordLinuxSocketWaits(o, c);
#endif
        }

        private static bool ObserveLinuxSocketWaits(LinuxNativeControl* c)
        {
            fixed (LinuxSocketStorage* o = &s_linuxSocketStorage)
                return o->Boot == c->State && RecordLinuxSocketWaits(o, c);
        }

        // Observe retained waits after the real clock/readiness
        // mutation. This neither completes a wait nor changes scheduler state.
        private static bool RecordLinuxSocketWaits(LinuxSocketStorage* o, LinuxNativeControl* c)
        {
            if (c->Capacity > LinuxNativeWorkers) return false;
            for (uint i = 0; i < c->Capacity; i++)
            {
                var slot = c->Slots + i;
#if M10_018_PROBE
                bool disk = slot->WaitSource == 4;
                if (slot->WaitSource != 3 && !disk || !slot->Wait.IsValid) continue;
                if (disk && !slot->DiskOperation.IsActive) return false;
#else
                if (slot->WaitSource != 3 || !slot->Wait.IsValid) continue;
#endif
                if (!c->Process.Inspect(c->Key, 2, slot->Kernel, out var view)) return false;
                if (view.Exiting) continue;
                var observed = c->Threads.Inspect(c->Key, 2, view.Kernel);
                if (!observed.Succeeded || !observed.Wait.IsValid || observed.Wait.Sequence != view.Wait.Sequence) return false;
                if (observed.Completion == ThreadTable.Completion.None ||
                    o->WakeIdentities[i] == view.Kernel.Value && o->WakeSequences[i] == view.Wait.Sequence) continue;
                if (!LinuxText(c->State,
#if M10_018_PROBE
                    disk ? "M16_025_WAKE identity=" :
#endif
                    "M16_012_WAKE identity=") || !LinuxNumber(c->State, view.Kernel.Value) ||
                    !LinuxText(c->State, " sequence=") || !LinuxNumber(c->State, view.Wait.Sequence) ||
#if M10_018_PROBE
                    disk && (!LinuxText(c->State, " request=") || !LinuxNumber(c->State, slot->DiskOperation.Generation)) ||
#endif
                    !LinuxText(c->State, " completion=") || !LinuxNumber(c->State, (uint)observed.Completion) ||
                    !LinuxText(c->State, " state=") || !LinuxNumber(c->State, (uint)observed.State) ||
                    !LinuxText(c->State, " clock=") || !LinuxNumber(c->State, c->Threads.ObservedTicks) || !LinuxText(c->State, "\r\n")) return false;
                o->WakeIdentities[i] = view.Kernel.Value; o->WakeSequences[i] = view.Wait.Sequence;
            }
            return true;
        }

        private static bool IdleLinuxSocketNetwork(LinuxNativeControl* c)
        {
            if (!ReadNetworkTime(c->State, out ulong now) || now > ~0UL - 5_000_000) return false;
            ulong until = now + 5_000_000;
            for (uint poll = 0; poll < 100_000; poll++)
            {
                if (!PollLinuxSocketNetwork(c,
#if M10_018_TIMING_PROBE
                    NativeDiskPollCause.Idle,
#endif
                    false) || !ReadNetworkTime(c->State, out now)) return false;
                if (c->Threads.ReadyCount != 0 || now >= until) return RecordLinuxSocketPoll(c, true);
            }
            return false;
        }

        // Preserve real deadline/FIFO changes even when no socket wait wins.
        // Idle pumps coalesce only empty iterations; every dispatch and syscall
        // boundary retains the table clock before descriptor-readiness wakes.
        private static bool RecordLinuxSocketPoll(LinuxNativeControl* c, bool idle = false) =>
            LinuxText(c->State, "M16_012_POLL identity=") && LinuxNumber(c->State, c->Threads.Current.Value) &&
            LinuxText(c->State, " clock=") && LinuxNumber(c->State, c->Threads.ObservedTicks) &&
            LinuxText(c->State, " ready=") && LinuxNumber(c->State, c->Threads.ReadyCount) &&
            LinuxText(c->State, " idle=") && LinuxNumber(c->State, idle ? 1UL : 0UL) && LinuxText(c->State, "\r\n");

        private static bool DrainLinuxSocketNetwork(Storage* state, bool abort)
        {
            fixed (LinuxSocketStorage* o = &s_linuxSocketStorage)
            {
                if (o->Boot != state || o->Phase != 2 || state->LinuxDescriptors.ThreadOwner != 0) return false;
                if (!abort)
                { o->Draining = 1; if (!ObserveLinuxSocketLifetimes(o)) return false; }
                for (uint fd = 0; fd < LinuxDescriptorServices.MaximumDescriptors; fd++)
                    if (LinuxDescriptorServices.Capture(&state->LinuxDescriptors, fd, out _))
                    {
                        long closed = LinuxDescriptorServices.Dispatch(&state->LinuxDescriptors, new() { Number = 3, Argument0 = fd });
                        if (!LinuxText(state, "M16_011_CLOSE fd=") || !LinuxNumber(state, fd) ||
                            !LinuxText(state, " valueBits=") || !LinuxNumber(state, unchecked((ulong)closed)) ||
                            !LinuxText(state, "\r\n") || closed != 0) return false;
                    }
#if M16_014_PROBE
                bool quotaAbort = abort && LinuxQuotaPolicy(state) && state->LinuxQuota == LinuxQuotaDisposition.Terminating;
                uint quotaRetained = 0, quotaCompact = 0;
                if (quotaAbort)
                {
                    // Descriptor closure can change retained owners. Publish
                    // their exact generations before the deliberate abort.
                    if (!ObserveLinuxSocketLifetimes(o)) return false;
                    quotaRetained = o->Network.RetainedConnections; quotaCompact = o->Network.CompactTimeWaitConnections;
                    if (quotaRetained < quotaCompact) return false;
                }
#endif
                if (abort)
                { if (!SocketNetwork.Abort(&o->Network)) return false; }
                else
                {
                    if (!ReadNetworkTime(state, out ulong now) || now > ~0UL - 270_000_000_000) return false;
                    ulong deadline = now + 270_000_000_000;
                    while (o->Network.RetainedConnections != 0)
                        if (!PumpLinuxSocketFrames(o) || !ReadNetworkTime(state, out now) || now >= deadline) return false;
                    if (!ObserveLinuxSocketLifetimes(o) || o->Fresh != o->Retired) return false;
                }
                if (!SocketNetwork.End(&o->Network) || TcpConnectionTable.RetireOwned(&o->Table) != TcpConnectionTable.OwnershipStatus.Success) return false;
#if M16_014_PROBE
                if (quotaAbort && !ObserveLinuxQuotaSocketLeases(o, true)) return false;
                if (quotaAbort && (!ReadNetworkTime(state, out o->Ended) || o->Ended < o->Started ||
                    !LinuxText(state, "M16_016_QUOTA_SOCKETS bulkAborted=") || !LinuxNumber(state, quotaRetained - quotaCompact) ||
                    !LinuxText(state, " compactDiscarded=") || !LinuxNumber(state, quotaCompact) ||
                    !LinuxText(state, " clock=") || !LinuxNumber(state, o->Ended) ||
                    !LinuxText(state, " retained=0 transport=aborted timewait=discarded buffers=scrubbed descriptors=closed table=retired\r\n"))) return false;
#endif
                if (!abort && (!ReadNetworkTime(state, out o->Ended) || o->Ended < o->MaximumDeadline)) return false;
#if M16_014_PROBE
                if (!abort && (o->CompactFresh != o->CompactRetired ||
                    !LinuxText(state, "M07_030_END transfers=") || !LinuxNumber(state, o->CompactTransfers) ||
                    !LinuxText(state, " retired=") || !LinuxNumber(state, o->CompactRetired) ||
                    !LinuxText(state, " bulkHighWater=") || !LinuxNumber(state, o->BulkHighWater) ||
                    !LinuxText(state, " compactHighWater=") || !LinuxNumber(state, o->CompactHighWater) ||
                    !LinuxText(state, " bytesPerRecord=") || !LinuxNumber(state, (ulong)sizeof(TcpTimeWaitTable.Entry)) ||
                    !LinuxText(state, " clock=") || !LinuxNumber(state, o->Ended) ||
                    !LinuxText(state, " deadline=") || !LinuxNumber(state, o->MaximumDeadline) ||
                    !LinuxText(state, " owner=retired\r\n"))) return false;
#endif
                o->Phase = 3; return true;
            }
        }

        private static ulong LinuxSocketNow(void* context)
        {
#if M16_027_PROBE
            var o = ResolveLinuxSocketIo(context);
            if (o == null) return 0;
#else
            var o = (LinuxSocketStorage*)context;
#endif
            if (!ReadNetworkTime(o->Boot, out ulong now)) { o->Failed = 1; return 0; }
            return now;
        }
        private static byte LinuxSocketSendFrame(void* context, byte* frame, uint bytes)
        {
#if M16_027_PROBE
            var o = ResolveLinuxSocketIo(context);
            if (o == null) return 2;
#else
            var o = (LinuxSocketStorage*)context;
#endif
            var sent = NetDevice.Send(o->Device, new ReadOnlySpan<byte>(frame, (int)bytes));
            if (sent == NetDevice.Status.Success) { o->Copied++; return 1; }
            if (sent == NetDevice.Status.Full) return 0;
            o->Failed = 1; return 2;
        }
    }
}
#endif
