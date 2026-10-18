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
            Storage* state = (Storage*)context;
            LinuxNativeControl* c = LinuxNativeOwner(state);
            if (c == null || c->Phase != 3 || c->Published != 1 || state->NetworkPhase != 2 ||
                c->Selected == 0 || c->Selected > c->Capacity || !c->Threads.Current.IsValid ||
                c->Threads.Current.Value != c->Plan.Bindings[c->Selected - 1].Context.Identity ||
                number is not (46 or 47) || count > LinuxDescriptorServices.MaximumVectors ||
                total > LinuxDescriptorServices.MaximumIo ||
                count != 0 && !LinuxThreadKernelRange(c, (byte*)vectors, count * 16, false) ||
                !LinuxDescriptorServices.Capture(&state->LinuxDescriptors, fd, out var token)) return 0;
            LinuxSyscallFrame* frame = (LinuxSyscallFrame*)(state->LinuxSyscallData + X64LinuxUserTransition.FrameOffset);
            if (frame->Number != number || (uint)frame->Argument0 != fd || frame->Argument1 != header ||
                frame->Argument2 != flags) return 0;
            ulong identity = c->Threads.Current.Value, ordinal = c->Gates[c->Selected - 1].Calls;
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

        private const uint LinuxSocketConnections = 8, LinuxSocketReceive = 8192, LinuxSocketSend = 32768;
        // Borrowed observations only. A release is reported after the original
        // handle becomes stale and its network slot disappears or is reused.
        private struct LinuxSocketLifetime
        {
            internal TcpConnectionTable.Handle Handle;
            internal TcpConnectionKey Key;
            internal ulong MaximumDeadline, Duration;
            internal uint Active, WaitObserved, DrainObserved, TimeoutObserved;
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
            internal fixed ulong CompactGenerations[64], CompactDeadlines[64];
#endif
            internal fixed byte Endpoints[2048], Ports[32], Registrations[2560], Slots[8192], Entries[1024];
            internal fixed ulong Generations[8];
            internal fixed ulong Lifetimes[128];
            internal fixed ulong WakeIdentities[(int)LinuxNativeWorkers], WakeSequences[(int)LinuxNativeWorkers];
            internal fixed byte Buffers[327680], Input[1536], Output[1536];
        }
        private static LinuxSocketStorage s_linuxSocketStorage;

        private static bool LinuxSocketImageMapped(SeparatePageFaultMappingRange* ranges, ulong count)
        {
            fixed (LinuxSocketStorage* owner = &s_linuxSocketStorage)
                return FirstExitImageMappings.Contains(ranges, count, (ulong)owner, (ulong)sizeof(LinuxSocketStorage),
                    PageProtection.Read | PageProtection.Write);
        }

        private static bool RunLinuxSocketNetwork(Storage* state, NetDevice.State* device)
        {
            fixed (LinuxSocketStorage* o = &s_linuxSocketStorage)
            {
                if (o->Phase != 0 || !device->Running || state->CooperativePhase != 0 ||
                    !ReadNetworkTime(state, out o->Started)) return false;
                o->Boot = state; o->Device = device; o->Phase = 1;
                if (!ResolveLinuxSocketPeer(o)) return false;
#if M10_004_PROBE && !M16_014_PROBE
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
                bool ran = CompleteLinuxRandomLifetime(state, BootSequence.RunOwnedLinuxSockets(state->LinuxStartupPhase == 6,
                    state->ServiceLoop.IsStopped, state->Arena.Transition->Mode == FirmwareServicesMode.BootServicesExited &&
                    state->Arena.Transition->RetainedFirmwareCallbacksCleared && state->Arena.Binding->RetainedPointersCleared,
                    state->ApicPhase == 2, device->Running, state, &RunOwnedLinuxSyscalls));
                return ran && o->Phase == 3 && o->Failed == 0 &&
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
                    sizeof(LinuxEpollServices.Registration) * 64 > 2560 || sizeof(SocketNetwork.Slot) * 8 > 8192 ||
                    sizeof(TcpConnectionTableEntry) * 8 > 1024 || (sizeof(LinuxSocketLifetime) & 7) != 0 ||
                    sizeof(LinuxSocketLifetime) * 8 > 1024 ||
                    !ReadNetworkTime(state, out ulong now)) return false;
                if (!LinuxSocketServices.Begin(&o->Sockets, &state->LinuxDescriptors, (LinuxSocketServices.Endpoint*)o->Endpoints,
                        16, new(10, 0, 2, 15), 8080) ||
                    !LinuxEpollServices.Begin(&o->Epoll, &state->LinuxDescriptors, (LinuxEpollServices.Port*)o->Ports, 4,
                        (LinuxEpollServices.Registration*)o->Registrations, 64) ||
                    TcpConnectionTable.InitializeOwned(&o->Table, (TcpConnectionTableEntry*)o->Entries, LinuxSocketConnections,
                        o->Generations, LinuxSocketConnections, state->NetworkArena ^ 0x4c534f434b455453UL) != TcpConnectionTable.OwnershipStatus.Success) return false;
                if (!LinuxSocketServices.AttachMessageObserver(&o->Sockets, state, &ObserveLinuxSocketMessage)) return false;
                // A finite diagnostic seed from the admitted clock, not a
                // production unpredictable TCP initial-sequence generator.
#if M16_014_PROBE
                uint sequence = 0;
                TcpInitialSequence.Owner* sequenceOwner = &state->LinuxServiceSequence;
#else
                uint sequence = ((uint)now & 0x3fff0000) | 0x10000;
                TcpInitialSequence.Owner* sequenceOwner = null;
#endif
                var io = new TcpPassiveConnection.Operations { Context = o, NowNanoseconds = &LinuxSocketNow, Send = &LinuxSocketSendFrame,
                    LifetimeNanoseconds = 10_000_000_000, RetryNanoseconds = 1_000_000_000, MaximumAttempts = 3 };
                if (!SocketNetwork.Begin(&o->Network, &o->Sockets, &o->Table, (SocketNetwork.Slot*)o->Slots, LinuxSocketConnections,
                    o->Buffers, LinuxSocketReceive, LinuxSocketSend, new(2, 0, 0, 0, 0, 1), o->Peer, new(10, 0, 2, 2), sequence, io,
                    compactTimeWait: Probes.EnableM16014Probe, sequence: sequenceOwner)) return false;
#if M16_014_PROBE
                if (!LinuxText(state, "M07_033_BOUND quietUntil=") || !LinuxNumber(state, state->LinuxServiceSequence.QuietDeadline) ||
                    !LinuxText(state, " clock=") || !LinuxNumber(state, now) ||
                    !LinuxText(state, " tickNanos=4000 quietNanos=240000000000 secret=independent lease=network\r\n")) return false;
#endif
                o->Phase = 2;
                return LinuxText(state, "M16_011_BOUND connections=8 rx=8192 tx=32768 epoll=4 watches=64 nic=live\r\n");
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
                    if (TcpConnectionTable.ReadOwned(&o->Table, held->Handle, out _) != TcpConnectionTable.OwnershipStatus.StaleHandle ||
                        !WriteLinuxSocketLifetime(o, "M16_011_SLOT event=1", i, held, now) ||
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
                    held->Handle = handle; held->Key = entry.Key; held->Active = 1;
                    if (!WriteLinuxSocketLifetime(o, "M16_011_SLOT event=0", i, held, now) ||
                        !LinuxText(o->Boot, " deadline=0 duration=0\r\n")) return false;
                    o->Fresh++;
                }
                else if (!held->Key.HasSameEndpoint(entry.Key)) return false;
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
            return ObserveLinuxCompactTimeWait(o, now);
#else
            return true;
#endif
        }

#if M16_014_PROBE
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

        private static bool PollLinuxSocketNetwork(LinuxNativeControl* c, bool observe = true)
        {
            fixed (LinuxSocketStorage* o = &s_linuxSocketStorage)
                return o->Boot == c->State && (!observe || RecordLinuxSocketPoll(c)) &&
                    RecordLinuxSocketWaits(o, c) && PumpLinuxSocketFrames(o) && c->Process.AdvanceFutexClock(c->Key, 2) &&
                    (!observe || RecordLinuxSocketPoll(c)) &&
                    c->Process.PollDescriptorReadiness(c->Key, 2) && RecordLinuxSocketWaits(o, c);
        }

        private static bool ObserveLinuxSocketWaits(LinuxNativeControl* c)
        {
            fixed (LinuxSocketStorage* o = &s_linuxSocketStorage)
                return o->Boot == c->State && RecordLinuxSocketWaits(o, c);
        }

        // Observe only retained source3 waits after the real clock/readiness
        // mutation. This neither completes a wait nor changes scheduler state.
        private static bool RecordLinuxSocketWaits(LinuxSocketStorage* o, LinuxNativeControl* c)
        {
            if (c->Capacity > LinuxNativeWorkers) return false;
            for (uint i = 0; i < c->Capacity; i++)
            {
                var slot = c->Slots + i;
                if (slot->WaitSource != 3 || !slot->Wait.IsValid) continue;
                if (!c->Process.Inspect(c->Key, 2, slot->Kernel, out var view)) return false;
                if (view.Exiting) continue;
                var observed = c->Threads.Inspect(c->Key, 2, view.Kernel);
                if (!observed.Succeeded || !observed.Wait.IsValid || observed.Wait.Sequence != view.Wait.Sequence) return false;
                if (observed.Completion == ThreadTable.Completion.None ||
                    o->WakeIdentities[i] == view.Kernel.Value && o->WakeSequences[i] == view.Wait.Sequence) continue;
                if (!LinuxText(c->State, "M16_012_WAKE identity=") || !LinuxNumber(c->State, view.Kernel.Value) ||
                    !LinuxText(c->State, " sequence=") || !LinuxNumber(c->State, view.Wait.Sequence) ||
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
                if (!PollLinuxSocketNetwork(c, false) || !ReadNetworkTime(c->State, out now)) return false;
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
            var o = (LinuxSocketStorage*)context;
            if (!ReadNetworkTime(o->Boot, out ulong now)) { o->Failed = 1; return 0; }
            return now;
        }
        private static byte LinuxSocketSendFrame(void* context, byte* frame, uint bytes)
        {
            var o = (LinuxSocketStorage*)context;
            var sent = NetDevice.Send(o->Device, new ReadOnlySpan<byte>(frame, (int)bytes));
            if (sent == NetDevice.Status.Success) { o->Copied++; return 1; }
            if (sent == NetDevice.Status.Full) return 0;
            o->Failed = 1; return 2;
        }
    }
}
#endif
