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
        private const uint LinuxSocketConnections = 8, LinuxSocketReceive = 8192, LinuxSocketSend = 32768;
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
            internal uint Phase, Failed;
            internal ulong Polls, Received, Copied, Started;
            internal fixed byte Endpoints[2048], Ports[32], Registrations[2560], Slots[8192], Entries[1024];
            internal fixed ulong Generations[8];
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
                if (!LinuxText(state, "TEST_START m16-011\r\n")) return false;
#if M16_012_PROBE
                if (!LinuxText(state, "TEST_START m16-012\r\n")) return false;
#endif
                bool ran = CompleteLinuxRandomLifetime(state, BootSequence.RunOwnedLinuxSockets(state->LinuxStartupPhase == 6,
                    state->ServiceLoop.IsStopped, state->Arena.Transition->Mode == FirmwareServicesMode.BootServicesExited &&
                    state->Arena.Transition->RetainedFirmwareCallbacksCleared && state->Arena.Binding->RetainedPointersCleared,
                    state->ApicPhase == 2, device->Running, state, &RunOwnedLinuxSyscalls));
                return ran && o->Phase == 3 && o->Failed == 0 &&
                    LinuxText(state, "M16_011_END polls=") && LinuxNumber(state, o->Polls) &&
                    LinuxText(state, " received=") && LinuxNumber(state, o->Received) &&
                    LinuxText(state, " copied=") && LinuxNumber(state, o->Copied) &&
                    LinuxText(state, " retained=0 buffers=scrubbed descriptors=closed workers=reaped\r\n");
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
                    sizeof(TcpConnectionTableEntry) * 8 > 1024 || !ReadNetworkTime(state, out ulong now)) return false;
                if (!LinuxSocketServices.Begin(&o->Sockets, &state->LinuxDescriptors, (LinuxSocketServices.Endpoint*)o->Endpoints,
                        16, new(10, 0, 2, 15), 8080) ||
                    !LinuxEpollServices.Begin(&o->Epoll, &state->LinuxDescriptors, (LinuxEpollServices.Port*)o->Ports, 4,
                        (LinuxEpollServices.Registration*)o->Registrations, 64) ||
                    TcpConnectionTable.InitializeOwned(&o->Table, (TcpConnectionTableEntry*)o->Entries, LinuxSocketConnections,
                        o->Generations, LinuxSocketConnections, state->NetworkArena ^ 0x4c534f434b455453UL) != TcpConnectionTable.OwnershipStatus.Success) return false;
                // A finite diagnostic seed from the admitted clock, not a
                // production unpredictable TCP initial-sequence generator.
                uint sequence = ((uint)now & 0x3fff0000) | 0x10000;
                var io = new TcpPassiveConnection.Operations { Context = o, NowNanoseconds = &LinuxSocketNow, Send = &LinuxSocketSendFrame,
                    LifetimeNanoseconds = 10_000_000_000, RetryNanoseconds = 1_000_000_000, MaximumAttempts = 3 };
                if (!SocketNetwork.Begin(&o->Network, &o->Sockets, &o->Table, (SocketNetwork.Slot*)o->Slots, LinuxSocketConnections,
                    o->Buffers, LinuxSocketReceive, LinuxSocketSend, new(2, 0, 0, 0, 0, 1), o->Peer, new(10, 0, 2, 2), sequence, io)) return false;
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
            ulong deadline = now + 2_000_000_000; bool sent = false;
            for (uint poll = 0; poll < 1_000_000; poll++)
            {
                if (NetDevice.Poll(o->Device, 8, out _) != NetDevice.Status.Success) return false;
                if (!sent)
                {
                    var copy = NetDevice.Send(o->Device, tx.Slice(0, 60));
                    if (copy is not (NetDevice.Status.Success or NetDevice.Status.Full)) return false;
                    sent = copy == NetDevice.Status.Success;
                }
                var received = NetDevice.Receive(o->Device, rx, out uint bytes);
                if (received == NetDevice.Status.Success && NetworkArpReply(rx.Slice(0, (int)bytes), localMac, local, new(10, 0, 2, 2), out o->Peer)) return true;
                if (received is not (NetDevice.Status.Success or NetDevice.Status.Empty) || !ReadNetworkTime(o->Boot, out now) || now >= deadline) return false;
            }
            return false;
        }

        private static bool PumpLinuxSocketFrames(LinuxSocketStorage* o)
        {
            if (o->Phase != 2 || o->Failed != 0 || !o->Device->Running || NetDevice.Poll(o->Device, 8, out _) != NetDevice.Status.Success) return false;
            Span<byte> input = new(o->Input, 1514), output = new(o->Output, 1514); o->Polls++;
            for (uint i = 0; i < 8; i++)
            {
                var received = NetDevice.Receive(o->Device, input, out uint bytes);
                if (received == NetDevice.Status.Empty) break;
                if (received != NetDevice.Status.Success) return false;
                o->Received++; var frame = input.Slice(0, (int)bytes);
                if (EthernetHeader.TryParse(frame, out var ethernet, out var payload) == PacketParseStatus.Success &&
                    ethernet.EtherType == EthernetHeader.EtherTypeArp)
                {
                    if (ArpPacket.TryParse(payload, out var arp) == PacketParseStatus.Success && arp.Operation == ArpPacket.OperationRequest &&
                        arp.SenderMac == o->Peer && arp.SenderAddress == new Ipv4Address(10, 0, 2, 2) && arp.TargetAddress == new Ipv4Address(10, 0, 2, 15))
                    {
                        output.Clear(); var mac = new MacAddress(2, 0, 0, 0, 0, 1);
                        if (EthernetHeader.TryWrite(output, new(o->Peer, mac, EthernetHeader.EtherTypeArp)) != PacketParseStatus.Success ||
                            ArpPacket.TryWrite(output.Slice(14), new(ArpPacket.OperationReply, mac, arp.TargetAddress, o->Peer, arp.SenderAddress), out _) != PacketParseStatus.Success) return false;
                        var copied = NetDevice.Send(o->Device, output.Slice(0, 60));
                        if (copied is not (NetDevice.Status.Success or NetDevice.Status.Full)) return false;
                    }
                    continue;
                }
                var accepted = SocketNetwork.Receive(&o->Network, frame, output);
                if (accepted is not (SocketNetwork.Status.Success or SocketNetwork.Status.Ignored or SocketNetwork.Status.Capacity)) return false;
            }
            return SocketNetwork.Pump(&o->Network, output) == SocketNetwork.Status.Success && o->Failed == 0;
        }

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
                }
                if (!SocketNetwork.End(&o->Network) || TcpConnectionTable.RetireOwned(&o->Table) != TcpConnectionTable.OwnershipStatus.Success) return false;
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
