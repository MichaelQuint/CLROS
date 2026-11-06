using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Pal;
using OS.Storage;
using C = OS.Storage.ContinuingFat32Mutation;
using B = OS.Storage.WritableBlockSession;
using W = OS.Kernel.Compatibility.LinuxX64.LinuxContinuingWritableDescriptorServices;
using F = OS.Kernel.Compatibility.LinuxX64.LinuxDescriptorServices;

internal static unsafe partial class LinuxWritableDiskDescriptorServicesTests
{
    private sealed class Owner : IDisposable
    {
        internal readonly LinuxThreadServicesTests.Model Thread = new(futex: true);
        internal readonly LinuxDescriptorServicesTests.Owner Files;
        internal readonly ContinuingFat32MutationTests.Fixture Storage;
        internal readonly W.State* Provider = (W.State*)NativeMemory.AllocZeroed((nuint)sizeof(W.State));
        internal readonly W.Descriptor* Slots = (W.Descriptor*)NativeMemory.AllocZeroed(32, (nuint)sizeof(W.Descriptor));
        internal readonly W.Description* Descriptions = (W.Description*)NativeMemory.AllocZeroed(32, (nuint)sizeof(W.Description));
        internal readonly byte* ProviderInput = (byte*)NativeMemory.AllocZeroed(W.IoBytes);
        internal readonly W.ProcessSlot* Processes = (W.ProcessSlot*)NativeMemory.AllocZeroed(4, (nuint)sizeof(W.ProcessSlot));
        internal readonly W.RangeLock* Locks = (W.RangeLock*)NativeMemory.AllocZeroed(32, (nuint)sizeof(W.RangeLock));
        internal readonly W.RangeLock* Scratch = (W.RangeLock*)NativeMemory.AllocZeroed(65, (nuint)sizeof(W.RangeLock));
        internal readonly F.WritableDiskState* Disk = (F.WritableDiskState*)NativeMemory.AllocZeroed((nuint)sizeof(F.WritableDiskState));
        internal readonly F.WritableDiskBinding* Bindings;
        internal readonly List<F.WritableDiskToken> Tokens = [];
        internal readonly List<(uint Type, ulong Sector)> Trace = [];
        internal readonly W.Admission ProviderAdmission;
        private readonly W.FreeSearch search;
        internal OSHostHandle Worker;
        internal bool Attached, AllowRetainedFailure;
        internal ulong ThreadOwner => (ulong)Thread.Service;
        internal WritableBlockSessionTests.Hardware* Hardware => Storage.Device.Hardware;
        internal F.WritableDiskAdmission Admission => new()
        {
            Provider = Provider,
            Process = W.PrimaryProcess(Provider),
            Bindings = Bindings,
            BindingCount = Files.Admission.DescriptionCount,
            Epoch = ProviderAdmission.Epoch,
            Search = search,
            FirmwareExited = true,
            SingleOwnerValidated = true
        };

        internal Owner(bool attach = true, bool observe = false, uint descriptors = 32, uint descriptions = 32, byte sectorsPerCluster = 1, bool rootCollision = false, bool sqliteFootprints = false, uint workBudget = 100000, W.FreeSearch? search = null)
        {
            this.search = search ?? new(2, 64, 256);
            Files = new(descriptors, descriptions);
            Bindings = (F.WritableDiskBinding*)NativeMemory.AllocZeroed(descriptions, (nuint)sizeof(F.WritableDiskBinding));
            Storage = new(sectorsPerCluster, observe: observe);
            if (sectorsPerCluster != 1)
                Storage.Fat(4, 0x0fffffff);
            if (rootCollision)
            {
                HostCheck.Equal((byte)1, sectorsPerCluster);
                ulong data = Storage.Input.PartitionFirst + 32 + 2 * 512;
                byte[] entry = Storage.Volatile.Slice((int)(data * 512 + 32), 32).ToArray();
                System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(Storage.Volatile.Slice((int)(Storage.Input.PartitionFirst * 512 + 44)), 130);
                Storage.Fat(130, 129);
                Storage.Fat(129, 0xfffffff);
                Storage.Volatile.Slice((int)((data + 128) * 512), 512).Fill(0xe5);
                Storage.Volatile.Slice((int)((data + 127) * 512), 512).Fill(0xe5);
                entry.CopyTo(Storage.Volatile.Slice((int)((data + 127) * 512 + 15 * 32), 32));
            }

            Storage.Volatile[(int)(Storage.Input.PartitionFirst * 512 + 48)] = 0xff;
            Storage.Volatile[(int)(Storage.Input.PartitionFirst * 512 + 49)] = 0xff;
            Storage.Input.WorkBudget = workBudget;
            if (sqliteFootprints)
            {
                HostCheck.True(sectorsPerCluster == 1 && !rootCollision);
                SeedSqliteFootprints(Storage);
            }

            Storage.Mount();
            ProviderAdmission = new()
            {
                Engine = Storage.State,
                Epoch = Storage.Input.Epoch,
                Descriptors = Slots,
                Descriptions = Descriptions,
                DescriptorCount = 32,
                DescriptionCount = 32,
                Input = ProviderInput,
                InputBytes = W.IoBytes,
                ExclusiveOwnerValidated = true
            };
            HostCheck.True(W.Start(Provider, ProviderAdmission));
            HostCheck.True(W.EnableLocks(Provider, new()
            {
                Processes = Processes,
                ProcessCount = 4,
                Locks = Locks,
                LockCount = 32,
                Scratch = Scratch,
                ScratchCount = 65,
                PrimaryPid = 101
            }));
            Files.Admission.User = Thread.DescriptorAccess;
            Files.Admission.CopyToUser = Thread.UserCopyOut;
            Files.Begin();
            HostCheck.True(Thread.Service->EnablePipes(Key, 2, Files.State));
            if (attach)
            {
                HostCheck.True(F.AttachWritableDisk(Files.State, Disk, Admission));
                Attached = true;
            }

            Worker = Thread.Start();
            HostCheck.Equal(Worker.Value, Thread.Kernel.Dispatch().Value);
        }

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

        internal void PutPath(string path)
        {
            byte[] value = Encoding.ASCII.GetBytes(path + '\0');
            value.CopyTo(new Span<byte>(Thread.At(Path), value.Length));
        }

        internal Span<byte> DataEntry => Storage.Volatile.Slice((int)((Storage.Input.PartitionFirst + 32 + 2 * 512) * 512 + 32), 32);

        internal LinuxSyscallFrame WriteCall(uint fd, byte[] payload, ulong offset)
        {
            payload.CopyTo(new Span<byte>(Thread.At(Payload), payload.Length));
            *(ulong*)Thread.At(Vector) = Payload;
            *(ulong*)(Thread.At(Vector) + 8) = (ulong)payload.Length;
            return Call(328, fd, Vector, 1, offset, 0, W.RwfNoAppend);
        }

        internal void PutLock(short type, long start, long bytes, int pid = 0)
        {
            byte* wire = Thread.At(Flock);
            new Span<byte>(wire, 32).Fill(0xa5);
            *(short*)wire = type;
            *(short*)(wire + 2) = 0;
            *(long*)(wire + 8) = start;
            *(long*)(wire + 16) = bytes;
            *(int*)(wire + 24) = pid;
        }

        internal bool HasLocks()
        {
            FieldInfo active = typeof(W.RangeLock).GetField("Active", BindingFlags.Instance | BindingFlags.NonPublic)!;
            for (int i = 0; i < 32; i++)
                if ((bool)active.GetValue(Locks[i])!)
                    return true;
            return false;
        }

        internal void Independent(string name, byte[] expected)
        {
            byte[] actual = new byte[expected.Length];
            var read = Fat32Parser.ReadRootFile(new PersistentDevice(Hardware), Storage.Input.PartitionFirst, name, actual);
            HostCheck.True(read.Succeeded && read.BytesRead == (ulong)expected.Length && actual.SequenceEqual(expected));
        }

        internal byte[] Bytes(int bytes) => new ReadOnlySpan<byte>(Thread.At(Output), bytes).ToArray();
        internal F.WritableDiskResult Begin(LinuxSyscallFrame call)
        {
            var begun = F.BeginWritableDisk(Files.State, ThreadOwner, Worker, call);
            if (begun.Token.IsActive)
                Tokens.Add(begun.Token);
            return begun;
        }

        internal F.WritableDiskResult Pump() => F.PumpWritableDiskOne(Files.State, ThreadOwner);
        internal void CompleteHardware()
        {
            if (Hardware->Pending)
            {
                Trace.Add((Hardware->Type, Hardware->Sector));
                Storage.Device.Complete();
            }
        }

        internal F.WritableDiskResult Ready(F.WritableDiskToken token)
        {
            for (int i = 0; i < 100000; i++)
            {
                var result = Pump();
                CompleteHardware();
                if (result.Progress is F.WritableDiskProgress.Ready or F.WritableDiskProgress.RecoveryRequired or F.WritableDiskProgress.Rejected)
                    return result;
            }

            throw new InvalidOperationException("writable global request did not complete");
        }

        internal long Run(LinuxSyscallFrame call)
        {
            var begun = Begin(call);
            if (begun.Progress == F.WritableDiskProgress.Immediate)
                return begun.Value;
            HostCheck.True(begun.Progress is F.WritableDiskProgress.Pending or F.WritableDiskProgress.Ready);
            HostCheck.Equal(F.WritableDiskProgress.Ready, Ready(begun.Token).Progress);
            var result = F.CompleteWritableDisk(Files.State, ThreadOwner, Worker, begun.Token, call);
            HostCheck.Equal(F.WritableDiskProgress.Immediate, result.Progress);
            return result.Value;
        }

        internal uint Open(string path = "/data/DATA.BIN", uint flags = 0)
        {
            PutPath(path);
            long value = Run(Call(2, Path, flags, (flags & W.Create) != 0 ? 0x180UL : 0));
            HostCheck.True(value >= 0);
            return (uint)value;
        }

        internal long Core(ulong number, ulong a0 = 0, ulong a1 = 0, ulong a2 = 0)
        {
            var result = Thread.DescriptorCall(number, a0, a1, a2);
            HostCheck.Equal(LinuxSyscallAction.Return, result.Action);
            return result.Value;
        }

        internal void Drain()
        {
            for (int i = 0; i < 100000 && Disk->RequestActive; i++)
            {
                var result = Pump();
                CompleteHardware();
                HostCheck.True(result.Progress != F.WritableDiskProgress.RecoveryRequired);
            }

            HostCheck.True(!Disk->RequestActive);
        }

        public void Dispose()
        {
            try
            {
                // A failed dirty simulated machine is discarded only after its
                // block reset below. Refused adapter/provider retirement is
                // preserved as the assertion, never reported as cleanup success.
                if (AllowRetainedFailure)
                {
                    HostCheck.True(Storage.State->Dirty && Disk->RequestActive && !F.DetachWritableDisk(Files.State));
                    return;
                }

                if (Attached)
                {
                    foreach (var token in Tokens)
                        F.CancelWritableDisk(Files.State, ThreadOwner, token, true);
                    Drain();
                    for (uint fd = 3; fd < Files.Admission.DescriptorCount; fd++)
                        if (F.Capture(Files.State, fd, out _))
                            HostCheck.Equal(0L, Core(3, fd));
                    HostCheck.True(F.DetachWritableDisk(Files.State));
                }

                HostCheck.True(Thread.Service->Shutdown(Key, 2, 7));
                HostCheck.True(Thread.Service->CommitExit(Key, 2, Thread.Kernel.Capture(Worker), out long clear) && clear == 0);
                Thread.Kernel.Confirm();
                HostCheck.True(Thread.Service->Reap(Key, 2, Worker, Thread.Kernel.SupervisorRsp, out _));
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
                NativeMemory.Free(Bindings);
                NativeMemory.Free(Provider);
                NativeMemory.Free(Slots);
                NativeMemory.Free(Descriptions);
                NativeMemory.Free(ProviderInput);
                NativeMemory.Free(Processes);
                NativeMemory.Free(Locks);
                NativeMemory.Free(Scratch);
            }
        }
    }
}
