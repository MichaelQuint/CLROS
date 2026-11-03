using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Net;
using Network = OS.Kernel.Compatibility.LinuxX64.LinuxSocketNetwork;
using Fds = OS.Kernel.Compatibility.LinuxX64.LinuxDescriptorServices;
using Sockets = OS.Kernel.Compatibility.LinuxX64.LinuxSocketServices;
using Epoll = OS.Kernel.Compatibility.LinuxX64.LinuxEpollServices;
using Table = OS.Net.TcpConnectionTable;
using Wire = TcpReceiveWindowTests;
using Rx = OS.Net.TcpReceiveWindow;

internal static unsafe partial class LinuxSocketNetworkTests
{
    private sealed class Model : IDisposable
    {
        internal readonly LinuxDescriptorServicesTests.Owner Files = new(16, 16);
        internal readonly Sockets.State* Sockets;
        internal readonly Sockets.Endpoint* Endpoints;
        private readonly Epoll.State* Epoll;
        private readonly Epoll.Port* Ports;
        private readonly Epoll.Registration* Registrations;
        internal readonly Network.State* State;
        internal readonly Network.Slot* Slots;
        internal readonly Table.Owner* Table;
        private readonly TcpConnectionTableEntry* Entries;
        private readonly ulong* Generations;
        internal readonly byte* Buffers;
        internal readonly Hardware* Hardware;
        internal readonly TcpInitialSequence.Owner* Sequence;
        internal readonly byte[] Frame = new byte[1514];
        internal readonly uint Count;
        private readonly uint ReceiveBytes, SendBytes;
        internal Model(uint count = 4, uint receiveBytes = 8192, uint sendBytes = 32768, bool compactTimeWait = false, bool continuingSequence = false, bool measureLeases = false, uint endpointCount = 8, bool boundedContext = false, bool start = true)
        {
            Count = count;
            ReceiveBytes = receiveBytes;
            SendBytes = sendBytes;
            Files.Begin();
            Sockets = (Sockets.State*)NativeMemory.AllocZeroed((nuint)sizeof(Sockets.State));
            Endpoints = (Sockets.Endpoint*)NativeMemory.AllocZeroed(endpointCount, (nuint)sizeof(Sockets.Endpoint));
            Epoll = (Epoll.State*)NativeMemory.AllocZeroed((nuint)sizeof(Epoll.State));
            Ports = (Epoll.Port*)NativeMemory.AllocZeroed(2, (nuint)sizeof(Epoll.Port));
            Registrations = (Epoll.Registration*)NativeMemory.AllocZeroed(16, (nuint)sizeof(Epoll.Registration));
            State = (Network.State*)NativeMemory.AllocZeroed((nuint)sizeof(Network.State));
            Slots = (Network.Slot*)NativeMemory.AllocZeroed(count, (nuint)sizeof(Network.Slot));
            Table = (Table.Owner*)NativeMemory.AllocZeroed((nuint)sizeof(Table.Owner));
            Entries = (TcpConnectionTableEntry*)NativeMemory.AllocZeroed(count, (nuint)sizeof(TcpConnectionTableEntry));
            Generations = (ulong*)NativeMemory.AllocZeroed(count, 8);
            Buffers = (byte*)NativeMemory.AllocZeroed(count, receiveBytes + sendBytes);
            Hardware = (Hardware*)NativeMemory.AllocZeroed((nuint)sizeof(Hardware));
            Hardware->Now = 1000;
            Hardware->Result = 1;
            if (continuingSequence)
            {
                Sequence = (TcpInitialSequence.Owner*)NativeMemory.AllocZeroed((nuint)sizeof(TcpInitialSequence.Owner));
                byte* secret = stackalloc byte[32];
                for (uint i = 0; i < 32; i++)
                    secret[i] = (byte)i; // Fixed host-test key only.
                HostCheck.True(TcpInitialSequence.Initialize(Sequence, secret, 77));
            }

            HostCheck.True(LinuxSocketServices.Begin(Sockets, Files.State, Endpoints, endpointCount, new(10, 0, 2, 15), 8080));
            HostCheck.True(LinuxEpollServices.Begin(Epoll, Files.State, Ports, 2, Registrations, 16));
            HostCheck.Equal(TcpConnectionTable.OwnershipStatus.Success, TcpConnectionTable.InitializeOwned(Table, Entries, count, Generations, count, 0x1234));
            if (start)
                HostCheck.True(Network.Begin(State, Sockets, Table, Slots, count, Buffers, receiveBytes, sendBytes, new(2, 0, 0, 0, 0, 1), new(0x52, 0x55, 0x0a, 0, 2, 2), new(10, 0, 2, 2), continuingSequence ? 0U : 700U, Io, compactTimeWait, Sequence, measureLeases, ioContextBytes: boundedContext ? (uint)sizeof(Hardware) : 0));
        }

        internal TcpPassiveConnection.Operations Io => new()
        {
            Context = Hardware,
            NowNanoseconds = &Now,
            Send = &Send,
            LifetimeNanoseconds = 10_000_000_000,
            RetryNanoseconds = 1_000_000_000,
            MaximumAttempts = 3
        };

        internal long Call(ulong number, ulong a0 = 0, ulong a1 = 0, ulong a2 = 0, ulong a3 = 0) => Fds.Dispatch(Files.State, new() { Number = number, Argument0 = a0, Argument1 = a1, Argument2 = a2, Argument3 = a3 });
        internal uint Listen(uint backlog = 4)
        {
            long fd = Call(41, 2, 1 | Fds.NonBlock, 6);
            HostCheck.True(fd >= 0);
            *(ulong*)Files.Memory.At(User) = 0x901f0002;
            *(ulong*)Files.Memory.At(User + 8) = 0;
            HostCheck.Equal(0L, Call(49, (uint)fd, User, 16));
            HostCheck.Equal(0L, Call(50, (uint)fd, backlog));
            return (uint)fd;
        }

        internal Network.Status Receive(byte[] frame) => Network.Receive(State, frame, Frame);
        internal void Pump(ulong now = 0)
        {
            if (now != 0)
                Hardware->Now = now;
            HostCheck.Equal(Network.Status.Success, Network.Pump(State, Frame));
        }

        internal void Refresh() => HostCheck.True(LinuxEpollServices.Refresh(Epoll));
        internal uint Connect(ushort port = 40000, uint sequence = 99)
        {
            HostCheck.Equal(Network.Status.Success, Receive(Syn(port, sequence)));
            uint local = BinaryPrimitives.ReadUInt32BigEndian(new ReadOnlySpan<byte>(Hardware->Frame + 38, 4)) + 1;
            HostCheck.Equal(Network.Status.Success, Receive(Wire.Packet(sequence + 1, ReadOnlySpan<byte>.Empty, local, port, flags: 0x10)));
            return local;
        }

        internal uint Accept(uint listener)
        {
            long fd = Call(288, listener, 0, 0, Fds.NonBlock);
            HostCheck.True(fd >= 0);
            return (uint)fd;
        }

        internal void Finish(bool abort = true)
        {
            for (uint fd = 0; fd < 32; fd++)
                if (Fds.Capture(Files.State, fd, out _))
                    HostCheck.Equal(0L, Call(3, fd));
            if (abort)
                HostCheck.True(Network.Abort(State));
            HostCheck.Equal(0U, State->RetainedConnections);
            HostCheck.Equal(0, Table->Count);
            HostCheck.True(Network.End(State));
            HostCheck.Equal(TcpConnectionTable.OwnershipStatus.Success, TcpConnectionTable.RetireOwned(Table));
            if (Sequence != null)
            {
                HostCheck.True(!Sequence->IsBorrowed);
                HostCheck.True(TcpInitialSequence.Erase(Sequence));
            }

            HostCheck.True(Fds.End(Files.State));
            HostCheck.True(new ReadOnlySpan<byte>(Buffers, (int)(Count * (ReceiveBytes + SendBytes))).IndexOfAnyExcept((byte)0) < 0);
        }

        public void Dispose()
        {
            NativeMemory.Free(Sequence);
            NativeMemory.Free(Hardware);
            NativeMemory.Free(Buffers);
            NativeMemory.Free(Generations);
            NativeMemory.Free(Entries);
            NativeMemory.Free(Table);
            NativeMemory.Free(Slots);
            NativeMemory.Free(State);
            NativeMemory.Free(Registrations);
            NativeMemory.Free(Ports);
            NativeMemory.Free(Epoll);
            NativeMemory.Free(Endpoints);
            NativeMemory.Free(Sockets);
            Files.Dispose();
        }
    }
}
