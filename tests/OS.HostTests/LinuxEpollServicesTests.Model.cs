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
    private sealed class Model : IDisposable
    {
        internal readonly LinuxThreadServicesTests.Model Thread = new(futex: true);
        internal readonly LinuxDescriptorServicesTests.Owner Files = new(16, 16);
        internal readonly Sockets.State* SocketState;
        internal readonly Sockets.Endpoint* Endpoints;
        internal readonly Epoll.State* State;
        internal readonly Epoll.Port* Ports;
        internal readonly Epoll.Registration* Registrations;
        private readonly uint RegistrationCapacity;
        private readonly Signals.State* SignalState;
        internal readonly OSHostHandle Parent;
        internal Model(uint ports = 4, uint registrations = 8, bool signals = false, bool epoll = true)
        {
            RegistrationCapacity = registrations;
            Files.Admission.User = Thread.DescriptorAccess;
            Files.Admission.CopyToUser = Thread.UserCopyOut;
            Files.Begin();
            SocketState = (Sockets.State*)NativeMemory.AllocZeroed((nuint)sizeof(Sockets.State));
            Endpoints = (Sockets.Endpoint*)NativeMemory.AllocZeroed(8, (nuint)sizeof(Sockets.Endpoint));
            State = (Epoll.State*)NativeMemory.AllocZeroed((nuint)sizeof(Epoll.State));
            Ports = (Epoll.Port*)NativeMemory.AllocZeroed(ports, (nuint)sizeof(Epoll.Port));
            Registrations = (Epoll.Registration*)NativeMemory.AllocZeroed(registrations, (nuint)sizeof(Epoll.Registration));
            HostCheck.True(Sockets.Begin(SocketState, Files.State, Endpoints, 8, new(10, 0, 2, 15), 8080));
            if (epoll)
                HostCheck.True(Epoll.Begin(State, Files.State, Ports, ports, Registrations, registrations));
            HostCheck.True(Thread.Service->EnablePipes(Key, 2, Files.State));
            if (signals)
            {
                SignalState = (Signals.State*)NativeMemory.AlignedAlloc(4096, 4096);
                new Span<byte>(SignalState, 4096).Clear();
                HostCheck.True(Signals.Initialize(SignalState, 4096, SignalKey, 2, new()
                {
                    User = Thread.Access,
                    CopyToUser = Thread.UserCopyOut,
                    Capacity = 3,
                    FirmwareExited = true,
                    SingleOwnerValidated = true
                }));
                HostCheck.True(Thread.Service->EnableSignals(Key, 2, SignalState));
            }

            Parent = Thread.Start();
            HostCheck.Equal(Parent.Value, Thread.Kernel.Dispatch().Value);
        }

        internal LinuxSyscallResult Call(ulong number, ulong a0 = 0, ulong a1 = 0, ulong a2 = 0, ulong a3 = 0) => Thread.DescriptorCall(number, a0, a1, a2, a3);
        internal long Value(ulong number, ulong a0 = 0, ulong a1 = 0, ulong a2 = 0, ulong a3 = 0)
        {
            var result = Call(number, a0, a1, a2, a3);
            HostCheck.Equal(LinuxSyscallAction.Return, result.Action);
            return result.Value;
        }

        internal uint Socket()
        {
            long fd = Value(41, 2, 1 | Fds.NonBlock, 6);
            HostCheck.True(fd >= 0);
            return (uint)fd;
        }

        internal uint Listen()
        {
            uint fd = Socket();
            Thread.Put64(Data, 0x901f0002);
            Thread.Put64(Data + 8, 0);
            HostCheck.Equal(0L, Value(49, fd, Data, 16));
            HostCheck.Equal(0L, Value(50, fd, 4));
            return fd;
        }

        internal uint Create()
        {
            long fd = Value(291, Fds.CloseOnExec);
            HostCheck.True(fd >= 0);
            return (uint)fd;
        }

        internal (uint Read, uint Write) Pipe(uint flags = Fds.NonBlock)
        {
            HostCheck.Equal(0L, Value(293, Data, flags));
            return (Thread.Read32(Data), Thread.Read32(Data + 4));
        }

        internal long Control(uint epoll, uint command, uint fd, uint events = 0, ulong data = 0)
        {
            Thread.Put32(Event, events);
            Thread.Put64(Event + 4, data);
            return Value(233, epoll, command, fd, Event);
        }

        internal long Wait(uint epoll, uint maximum = 1024) => Value(281, epoll, Output, maximum, 0);
        internal void Enqueue(uint listener, TcpSendWindowTests.Fixture connection)
        {
            HostCheck.True(Fds.Capture(Files.State, listener, out var token));
            HostCheck.True(Sockets.EnqueueAccepted(SocketState, token, listener, connection.Receiver, connection.Owner));
            HostCheck.True(Thread.Service->PollDescriptorReadiness(Key, 2));
        }

        internal uint Accept(uint listener)
        {
            long fd = Value(288, listener, 0, 0, Fds.NonBlock);
            HostCheck.True(fd >= 0);
            return (uint)fd;
        }

        internal void Park(OSHostHandle thread)
        {
            HostCheck.True(!Thread.Service->CurrentImage(Key, 2, out _));
            HostCheck.True(Thread.Service->CommitEpollWait(Key, 2, Thread.Kernel.Capture(thread), out bool handoff) && handoff);
            Thread.Kernel.Confirm();
            HostCheck.Equal(KernelThreadTable.State.Waiting, Thread.Kernel.Inspect(thread).State);
        }

        internal LinuxSyscallResult Complete() => Thread.Service->CompleteEpollWait(Key, 2);
        internal void CloseAndReap(uint fd, TcpSendWindowTests.Fixture connection)
        {
            HostCheck.Equal(0L, Value(3, fd));
            HostCheck.Equal(Tx.Status.Success, Tx.AbortConnection(connection.Owner));
            HostCheck.True(Thread.Service->PollDescriptorReadiness(Key, 2));
            HostCheck.True(Sockets.ReapClosed(SocketState, connection.Owner));
            HostCheck.Equal(0, connection.Table->Count);
        }

        internal void Drain()
        {
            HostCheck.True(Thread.Service->Shutdown(Key, 2, 9));
            while (Thread.Service->ActiveThreads != 0)
            {
                var thread = Thread.Kernel.Table->Current;
                if (!thread.IsValid)
                    thread = Thread.Kernel.Dispatch();
                if (Thread.Kernel.Inspect(thread).State == KernelThreadTable.State.Parking)
                {
                    HostCheck.Equal(3U, Thread.View(thread).WaitSource);
                    HostCheck.True(Thread.Service->CommitEpollWait(Key, 2, Thread.Kernel.Capture(thread), out bool handoff));
                    if (handoff)
                        Thread.Kernel.Confirm();
                    continue;
                }

                HostCheck.True(Thread.Service->CommitExit(Key, 2, Thread.Kernel.Capture(thread), out long clear) && clear == 0);
                Thread.Kernel.Confirm();
                HostCheck.True(Thread.Service->Reap(Key, 2, thread, Thread.Kernel.SupervisorRsp, out _));
            }

            HostCheck.True(Thread.Service->Release(Key, 2));
            HostCheck.True(Fds.End(Files.State));
            if (SignalState != null)
                HostCheck.True(Signals.End(SignalState, SignalKey, 2));
            HostCheck.True(new ReadOnlySpan<byte>(Registrations, (int)RegistrationCapacity * sizeof(Epoll.Registration)).IndexOfAnyExcept((byte)0) < 0);
            HostCheck.True(new ReadOnlySpan<byte>(Files.Admission.Scratch, (int)Files.Admission.ScratchBytes).IndexOfAnyExcept((byte)0) < 0);
        }

        public void Dispose()
        {
            NativeMemory.Free(Registrations);
            NativeMemory.Free(Ports);
            NativeMemory.Free(State);
            NativeMemory.Free(Endpoints);
            NativeMemory.Free(SocketState);
            Files.Dispose();
            Thread.Dispose();
            NativeMemory.AlignedFree(SignalState);
        }
    }
}
