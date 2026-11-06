using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Reflection;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Net;
using Sockets = OS.Kernel.Compatibility.LinuxX64.LinuxSocketServices;
using Fds = OS.Kernel.Compatibility.LinuxX64.LinuxDescriptorServices;
using Tx = OS.Net.TcpSendWindow;
using Rx = OS.Net.TcpReceiveWindow;
using Wire = TcpReceiveWindowTests;

internal static unsafe partial class LinuxSocketServicesTests
{
    internal sealed class Model : IDisposable
    {
        internal readonly LinuxDescriptorServicesTests.Owner Files;
        internal readonly Sockets.State* State;
        internal readonly Sockets.Endpoint* Endpoints;
        internal Model(uint capacity = 4, uint descriptors = 12)
        {
            Files = new(descriptors, descriptors);
            Files.Begin();
            State = (Sockets.State*)NativeMemory.AlignedAlloc((nuint)sizeof(Sockets.State), 16);
            *State = default;
            Endpoints = (Sockets.Endpoint*)NativeMemory.AllocZeroed(capacity, (nuint)sizeof(Sockets.Endpoint));
            HostCheck.True(Sockets.Begin(State, Files.State, Endpoints, capacity, new(10, 0, 2, 15), 8080));
        }

        internal long Call(ulong number, ulong a0 = 0, ulong a1 = 0, ulong a2 = 0, ulong a3 = 0, ulong a4 = 0, ulong a5 = 0) => Fds.Dispatch(Files.State, new() { Number = number, Argument0 = a0, Argument1 = a1, Argument2 = a2, Argument3 = a3, Argument4 = a4, Argument5 = a5 });
        internal uint Socket(uint flags = Fds.NonBlock | Fds.CloseOnExec)
        {
            long fd = Call(41, 2, 1 | flags, 6);
            HostCheck.True(fd >= 0);
            return (uint)fd;
        }

        internal void Address(ushort port = 8080, byte last = 0)
        {
            var bytes = new Span<byte>(Files.Memory.At(User), 16);
            bytes.Clear();
            bytes[0] = 2;
            bytes[2] = (byte)(port >> 8);
            bytes[3] = (byte)port;
            if (last != 0)
            {
                bytes[4] = 10;
                bytes[6] = 2;
                bytes[7] = last;
            }
        }

        internal uint Listen(uint backlog = 4)
        {
            uint fd = Socket();
            Address();
            HostCheck.Equal(0L, Call(49, fd, User, 16));
            HostCheck.Equal(0L, Call(50, fd, backlog));
            return fd;
        }

        internal uint Accept(uint listener, TcpSendWindowTests.Fixture connection)
        {
            HostCheck.True(Fds.Capture(Files.State, listener, out var token));
            HostCheck.True(Sockets.EnqueueAccepted(State, token, listener, connection.Receiver, connection.Owner));
            long fd = Call(288, listener, 0, 0, Fds.NonBlock | Fds.CloseOnExec);
            HostCheck.True(fd >= 0);
            return (uint)fd;
        }

        internal void Option(uint fd, ulong level, ulong option, int value)
        {
            *(int*)Files.Memory.At(User) = value;
            HostCheck.Equal(0L, Call(54, fd, level, option, User, 4));
        }

        internal void End()
        {
            HostCheck.True(Fds.Audit(Files.State));
            HostCheck.True(Fds.End(Files.State));
        }

        public void Dispose()
        {
            NativeMemory.AlignedFree(State);
            NativeMemory.Free(Endpoints);
            Files.Dispose();
        }
    }
}
