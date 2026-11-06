using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Threading;
using OS.Pal;
using OS.Storage;
using W = OS.Kernel.Compatibility.LinuxX64.LinuxContinuingWritableDescriptorServices;
using F = OS.Kernel.Compatibility.LinuxX64.LinuxDescriptorServices;

internal static unsafe partial class LinuxWritableDiskThreadServicesTests
{
    private sealed class Fixture : IDisposable
    {
        internal readonly LinuxThreadServicesTests.Model Thread = new(futex: true);
        internal readonly LinuxDescriptorServicesTests.Owner Files = new(descriptors: 32, descriptions: 32);
        internal readonly ContinuingFat32MutationTests.Fixture Storage = new();
        internal readonly W.State* Provider = (W.State*)NativeMemory.AllocZeroed((nuint)sizeof(W.State));
        private readonly W.Descriptor* descriptors = (W.Descriptor*)NativeMemory.AllocZeroed(32, (nuint)sizeof(W.Descriptor));
        private readonly W.Description* descriptions = (W.Description*)NativeMemory.AllocZeroed(32, (nuint)sizeof(W.Description));
        private readonly byte* input;
        private readonly W.ProcessSlot* processes = (W.ProcessSlot*)NativeMemory.AllocZeroed(4, (nuint)sizeof(W.ProcessSlot));
        private readonly W.RangeLock* locks = (W.RangeLock*)NativeMemory.AllocZeroed(32, (nuint)sizeof(W.RangeLock));
        private readonly W.RangeLock* scratch = (W.RangeLock*)NativeMemory.AllocZeroed(65, (nuint)sizeof(W.RangeLock));
        internal readonly F.WritableDiskState* Disk = (F.WritableDiskState*)NativeMemory.AllocZeroed((nuint)sizeof(F.WritableDiskState));
        private readonly F.WritableDiskBinding* bindings = (F.WritableDiskBinding*)NativeMemory.AllocZeroed(32, (nuint)sizeof(F.WritableDiskBinding));
        internal OSHostHandle Main;
        internal uint Parked;
        internal List<(uint Type, ulong Sector)>? Trace;
        internal bool RetainedFailure;
        internal readonly bool Rejected;
        private readonly bool borrowedInput;
        private readonly W.FreeSearch search;
        private ulong Owner => (ulong)Thread.Service;

        internal Fixture(bool aliasScheduler = false, uint primaryPid = 101, bool deferBinding = false, uint packagePid = 0, bool sqliteFootprints = false, uint workBudget = 100000, W.FreeSearch? search = null)
        {
            this.search = search ?? new(2, 64, 256);
            borrowedInput = aliasScheduler;
            input = aliasScheduler ? (byte*)Thread.Kernel.Ready : (byte*)NativeMemory.AllocZeroed(W.IoBytes);
            if (aliasScheduler)
                new Span<byte>(input, (int)W.IoBytes).Clear(); // No live workers; model of an invalid offered borrow.
            Storage.Volatile[(int)(Storage.Input.PartitionFirst * 512 + 48)] = 0xff;
            Storage.Volatile[(int)(Storage.Input.PartitionFirst * 512 + 49)] = 0xff;
            Storage.Input.WorkBudget = workBudget;
            if (sqliteFootprints)
                LinuxWritableDiskDescriptorServicesTests.SeedSqliteFootprints(Storage);
            Storage.Mount();
            HostCheck.True(W.Start(Provider, new()
            {
                Engine = Storage.State,
                Epoch = Storage.Input.Epoch,
                Descriptors = descriptors,
                Descriptions = descriptions,
                DescriptorCount = 32,
                DescriptionCount = 32,
                Input = input,
                InputBytes = W.IoBytes,
                ExclusiveOwnerValidated = true
            }));
            HostCheck.True(W.EnableLocks(Provider, new()
            {
                Processes = processes,
                ProcessCount = 4,
                Locks = locks,
                LockCount = 32,
                Scratch = scratch,
                ScratchCount = 65,
                PrimaryPid = primaryPid
            }));
            Files.Admission.User = Thread.DescriptorAccess;
            Files.Admission.CopyToUser = Thread.UserCopyOut;
            Files.Admission.PackageProcessId = packagePid;
            Files.Begin();
            if (deferBinding)
                return;
            HostCheck.True(AttachWritable());
            if (aliasScheduler)
            {
                uint calls = Storage.Device.Hardware->Calls;
                var before = Thread.Kernel.Snapshot();
                HostCheck.True(!Thread.Service->EnablePipes(Key, 2, Files.State));
                Thread.Kernel.Same(before);
                HostCheck.Equal(calls, Storage.Device.Hardware->Calls);
                Rejected = true;
                return;
            }

            HostCheck.True(EnableThreads());
            Start();
        }

        internal bool AttachWritable() => F.AttachWritableDisk(Files.State, Disk, new() { Provider = Provider, Process = W.PrimaryProcess(Provider), Bindings = bindings, BindingCount = 32, Epoch = Storage.Input.Epoch, Search = search, FirmwareExited = true, SingleOwnerValidated = true });
        internal bool EnableThreads() => Thread.Service->EnablePipes(Key, 2, Files.State);
        internal void Start()
        {
            Main = Thread.Start();
            HostCheck.Equal(Main.Value, Thread.Kernel.Dispatch().Value);
        }

        internal W.Result ProviderResult(W.Result begun)
        {
            if (begun.Phase != W.Progress.Pending)
                return begun;
            for (int i = 0; i < 100000; i++)
            {
                var result = W.Pump(Provider);
                CompleteHardware();
                if (result.Phase == W.Progress.Ready)
                    return W.Complete(Provider, begun.Request);
                HostCheck.Equal(W.Progress.Pending, result.Phase);
            }

            throw new InvalidOperationException("direct registered owner did not complete");
        }

        internal void PutPath(string path) => Encoding.ASCII.GetBytes(path + '\0').CopyTo(new Span<byte>(Thread.At(Path), path.Length + 1));
        internal void PutWrite(byte[] bytes)
        {
            bytes.CopyTo(new Span<byte>(Thread.At(Payload), bytes.Length));
            *(ulong*)Thread.At(Vector) = Payload;
            *(ulong*)(Thread.At(Vector) + 8) = (ulong)bytes.Length;
        }

        internal bool May(ulong start, ulong bytes) => Thread.Service->MayChangeUserRange(Key, 2, start, bytes);
        internal bool HasLocks()
        {
            FieldInfo active = typeof(W.RangeLock).GetField("Active", BindingFlags.Instance | BindingFlags.NonPublic)!;
            for (int i = 0; i < 32; i++)
                if ((bool)active.GetValue(locks[i])!)
                    return true;
            return false;
        }

        private LinuxSyscallResult Call(ulong number, ulong a0, ulong a1, ulong a2, ulong a3, ulong a4, ulong a5)
        {
            var image = Thread.CurrentImage();
            var frame = image->Frame;
            frame.Number = number;
            frame.Argument0 = a0;
            frame.Argument1 = a1;
            frame.Argument2 = a2;
            frame.Argument3 = a3;
            frame.Argument4 = a4;
            frame.Argument5 = a5;
            image->Frame = frame;
            var result = Thread.Service->DispatchDescriptor(Key, 2, frame);
            if (result.Action == LinuxSyscallAction.Return)
                image->Value = result.Value;
            return result;
        }

        internal void Begin(ulong number, ulong a0, ulong a1 = 0, ulong a2 = 0, ulong a3 = 0, ulong a4 = 0, ulong a5 = 0) => HostCheck.Equal(LinuxSyscallAction.Blocked, Call(number, a0, a1, a2, a3, a4, a5).Action);
        internal long Run(ulong number, ulong a0, ulong a1 = 0, ulong a2 = 0, ulong a3 = 0, ulong a4 = 0, ulong a5 = 0)
        {
            var result = Call(number, a0, a1, a2, a3, a4, a5);
            if (result.Action == LinuxSyscallAction.Return)
                return result.Value;
            HostCheck.Equal(LinuxSyscallAction.Blocked, result.Action);
            Park();
            Ready();
            return Consume();
        }

        internal uint Open(string path = "/data/DATA.BIN", uint flags = 0)
        {
            PutPath(path);
            long fd = Run(2, Path, flags, (flags & W.Create) != 0 ? 0x180UL : 0);
            HostCheck.True(fd >= 3);
            return (uint)fd;
        }

        internal void Park()
        {
            HostCheck.True(Thread.Service->CommitWritableDiskWait(Key, 2, Thread.Kernel.Capture(Main), out bool handoff));
            Parked++;
            if (handoff)
                Thread.Kernel.Confirm();
        }

        private void CompleteHardware()
        {
            if (!Storage.Device.Hardware->Pending)
                return;
            Trace?.Add((Storage.Device.Hardware->Type, Storage.Device.Hardware->Sector));
            Storage.Device.Complete();
        }

        internal void Ready()
        {
            for (int i = 0; i < 100000; i++)
            {
                HostCheck.True(Thread.Service->PollWritableDiskReadiness(Key, 2));
                CompleteHardware();
                if (Thread.Kernel.Inspect(Main).Completion != KernelThreadTable.Completion.None)
                    return;
            }

            throw new InvalidOperationException("source5 did not complete");
        }

        internal void AdapterReady()
        {
            for (int i = 0; i < 100000; i++)
            {
                var result = F.PumpWritableDiskOne(Files.State, Owner);
                CompleteHardware();
                if (result.Progress == F.WritableDiskProgress.Ready)
                    return;
                HostCheck.True(result.Progress != F.WritableDiskProgress.Rejected);
            }

            throw new InvalidOperationException("adapter did not complete");
        }

        internal long Consume()
        {
            if (!Thread.Kernel.Table->Current.IsValid)
                HostCheck.Equal(Main.Value, Thread.Kernel.Dispatch().Value);
            var result = Thread.Service->CompleteWritableDiskWait(Key, 2);
            HostCheck.Equal(LinuxSyscallAction.Return, result.Action);
            return result.Value;
        }

        internal void Drain()
        {
            for (int i = 0; i < 100000 && Disk->RequestActive; i++)
            {
                HostCheck.True(Thread.Service->PollWritableDiskReadiness(Key, 2));
                CompleteHardware();
            }

            HostCheck.True(!Disk->RequestActive);
        }

        public void Dispose()
        {
            try
            {
                HostCheck.True(Thread.Service->Shutdown(Key, 2, 7));
                while (Thread.Service->ActiveThreads != 0)
                {
                    var current = Thread.Kernel.Table->Current;
                    if (!current.IsValid)
                        current = Thread.Kernel.Dispatch();
                    if (Thread.Kernel.Inspect(current).State == KernelThreadTable.State.Parking)
                    {
                        HostCheck.True(Thread.Service->CommitWritableDiskWait(Key, 2, Thread.Kernel.Capture(current), out bool handoff));
                        if (handoff)
                            Thread.Kernel.Confirm();
                        continue;
                    }

                    HostCheck.True(Thread.Service->CommitExit(Key, 2, Thread.Kernel.Capture(current), out _));
                    Thread.Kernel.Confirm();
                    HostCheck.True(Thread.Service->Reap(Key, 2, current, Thread.Kernel.SupervisorRsp, out _));
                }

                if (RetainedFailure)
                {
                    HostCheck.True(!Thread.Service->Release(Key, 2) && !F.DetachWritableDisk(Files.State) && Disk->RequestActive && Storage.State->Dirty);
                    // Discard this failed host model only after Storage.Dispose's
                    // real fake-device reset; this is not successful retirement.
                    return;
                }

                if (!Rejected)
                    Drain();
                HostCheck.True(Thread.Service->Release(Key, 2));
                HostCheck.True(F.End(Files.State));
                HostCheck.Equal(W.Progress.Retired, W.Detach(Provider).Phase);
            }
            finally
            {
                Storage.Dispose();
                Files.Dispose();
                Thread.Dispose();
                NativeMemory.Free(Disk);
                NativeMemory.Free(bindings);
                NativeMemory.Free(Provider);
                NativeMemory.Free(descriptors);
                NativeMemory.Free(descriptions);
                if (!borrowedInput)
                    NativeMemory.Free(input);
                NativeMemory.Free(processes);
                NativeMemory.Free(locks);
                NativeMemory.Free(scratch);
            }
        }
    }
}
