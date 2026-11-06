using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Threading;
using OS.Pal;
using Descriptors = OS.Kernel.Compatibility.LinuxX64.LinuxDescriptorServices;
using ThreadModel = LinuxThreadServicesTests.Model;

internal static unsafe partial class LinuxBlockingPipeTests
{
    internal sealed class Owner : IDisposable
    {
        internal readonly ThreadModel Thread = new(futex: true);
        internal readonly LinuxDescriptorServicesTests.Owner Files = new();
        internal Owner(bool attach = true)
        {
            Files.Admission.User = Thread.DescriptorAccess;
            Files.Admission.CopyToUser = Thread.UserCopyOut;
            Files.Begin();
            if (attach)
                HostCheck.True(Thread.Service->EnablePipes(Key, 2, Files.State));
        }

        internal LinuxSyscallResult Call(ulong number, ulong a0 = 0, ulong a1 = 0, ulong a2 = 0) => Thread.DescriptorCall(number, a0, a1, a2);
        internal long Value(ulong number, ulong a0 = 0, ulong a1 = 0, ulong a2 = 0)
        {
            var r = Call(number, a0, a1, a2);
            HostCheck.Equal(LinuxSyscallAction.Return, r.Action);
            return r.Value;
        }

        internal (uint Read, uint Write) Pipe(uint flags = 0)
        {
            HostCheck.Equal(0L, Value(293, Fds, flags));
            return (Thread.Read32(Fds), Thread.Read32(Fds + 4));
        }

        internal void ScratchClean() => HostCheck.True(new ReadOnlySpan<byte>(Files.Admission.Scratch, (int)Files.Admission.ScratchBytes).IndexOfAnyExcept((byte)0) == -1);
        internal void Park(OSHostHandle thread)
        {
            ScratchClean();
            HostCheck.True(!Thread.Service->CurrentImage(Key, 2, out _));
            HostCheck.True(Thread.Service->CommitPipeWait(Key, 2, Thread.Kernel.Capture(thread), out bool handoff) && handoff);
            Thread.Kernel.Confirm();
            HostCheck.Equal(KernelThreadTable.State.Waiting, Thread.Kernel.Inspect(thread).State);
        }

        internal LinuxSyscallResult Complete()
        {
            var r = Thread.Service->CompletePipeWait(Key, 2);
            ScratchClean();
            return r;
        }

        internal void BeginGate(ref LinuxSyscallGateState gate, byte* scratch) => HostCheck.Equal(LinuxSyscallGateStatus.Active, LinuxSyscallGateway.Begin(ref gate, new() { UserRoot = Thread.Kernel.Owner.Root, MaximumCalls = 64, MaximumWrite = 64, Scratch = scratch, ScratchBytes = 64, FirmwareExited = true, OwnedBoundaryValidated = true, EnableStartup = true, EnableDescriptors = true, EnableThreads = true }, Thread.GatewayAccess));
        internal LinuxSyscallResult GateCall(ref LinuxSyscallGateState gate, ulong number, ulong fd, ulong address, ulong bytes)
        {
            var image = Thread.CurrentImage();
            var frame = image->Frame;
            frame.Number = number;
            frame.Argument0 = fd;
            frame.Argument1 = address;
            frame.Argument2 = bytes;
            image->Frame = frame;
            var result = LinuxSyscallGateway.Dispatch(ref gate, ref frame);
            image->Frame = frame;
            if (result.Action == LinuxSyscallAction.Return)
                image->Value = result.Value;
            return result;
        }

        internal void Drain()
        {
            HostCheck.True(Thread.Service->Shutdown(Key, 2, 7));
            while (Thread.Service->ActiveThreads != 0)
            {
                var thread = Thread.Kernel.Table->Current;
                if (!thread.IsValid)
                    thread = Thread.Kernel.Dispatch();
                if (Thread.Kernel.Inspect(thread).State == KernelThreadTable.State.Parking)
                {
                    bool ok = Thread.View(thread).PipePending ? Thread.Service->CommitPipeWait(Key, 2, Thread.Kernel.Capture(thread), out bool handoff) : Thread.Service->CommitFutexWait(Key, 2, Thread.Kernel.Capture(thread), out handoff);
                    HostCheck.True(ok);
                    if (handoff)
                        Thread.Kernel.Confirm();
                    continue;
                }

                HostCheck.True(Thread.Service->CommitExit(Key, 2, Thread.Kernel.Capture(thread), out _));
                Thread.Kernel.Confirm();
                HostCheck.True(Thread.Service->Reap(Key, 2, thread, Thread.Kernel.SupervisorRsp, out _));
            }

            HostCheck.True(Descriptors.Audit(Files.State));
            HostCheck.True(Thread.Service->Release(Key, 2));
            HostCheck.True(Descriptors.End(Files.State));
            ScratchClean();
            HostCheck.True(new ReadOnlySpan<byte>(Files.Admission.PipeBuffers, (int)(Files.Admission.PipeCount * Files.Admission.PipeCapacity)).IndexOfAnyExcept((byte)0) == -1);
        }

        public void Dispose()
        {
            Files.Dispose();
            Thread.Dispose();
        }
    }
}
