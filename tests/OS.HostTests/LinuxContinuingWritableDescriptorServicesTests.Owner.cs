using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Text;
using OS.Storage;
using C = OS.Storage.ContinuingFat32Mutation;
using B = OS.Storage.WritableBlockSession;
using D = OS.Kernel.Compatibility.LinuxX64.LinuxContinuingWritableDescriptorServices;
using LogUpload = OS.Tooling.NativeAotLogUploadContract;
using P = OS.Storage.WritableBlockReadPort;

internal static unsafe partial class LinuxContinuingWritableDescriptorServicesTests
{
    private sealed class Owner : IDisposable
    {
        internal readonly ContinuingFat32MutationTests.Fixture Storage;
        internal readonly D.State* State = (D.State*)NativeMemory.AllocZeroed((nuint)sizeof(D.State));
        internal readonly D.Descriptor* Slots;
        internal readonly D.Description* Descriptions;
        internal readonly byte* InputBytes = (byte*)NativeMemory.AllocZeroed(8192);
        internal D.ProcessSlot* Processes;
        internal D.RangeLock* Locks, Scratch;
        internal D.LockAdmission LockInput;
        internal D.Admission Input;
        internal bool Attached;
        internal bool Record = true;
        internal bool AllowCleanRetainedFailure;
        internal uint? FailureType;
        internal ulong? FailureSector;
        internal uint AtAttach;
        internal WritableBlockSessionTests.Hardware* Device => Storage.Device.Hardware;
        internal List<(uint Type, ulong Sector)> Trace => Storage.Trace;
        internal ulong DataLba => Storage.Input.PartitionFirst + 32 + 2 * 512;

        internal Owner(bool attach = true, uint fdCount = 32, uint descriptionCount = 32, bool locks = false, bool growth = false, bool observe = false, byte sectorsPerCluster = 1, byte[]? restoredMedia = null, ulong engineEpoch = 321)
        {
            Storage = new(sectorsPerCluster, observe: observe);
            if (sectorsPerCluster != 1)
                Storage.Fat(4, 0x0fffffff);
            Slots = (D.Descriptor*)NativeMemory.AllocZeroed(fdCount, (nuint)sizeof(D.Descriptor));
            Descriptions = (D.Description*)NativeMemory.AllocZeroed(descriptionCount, (nuint)sizeof(D.Description));
            HostCheck.True(engineEpoch != 0);
            Storage.Input.Epoch = engineEpoch;
            if (restoredMedia is not null)
            {
                HostCheck.True(sectorsPerCluster == 1 && restoredMedia.Length == Storage.Volatile.Length && Device->MediumSectors * 512 == (ulong)restoredMedia.Length && !Storage.State->IsMounted && Storage.Device.State->Submitted == 0 && Storage.Device.State->Completed == 0 && !Storage.Device.State->RequestPending);
                // Initialize the new fake medium before its first actual IN.
                // This creates neither a device completion nor mount facts.
                restoredMedia.CopyTo(Storage.Volatile);
                restoredMedia.CopyTo(Storage.Persistent);
            }

            // Explicitly unknown FSInfo is supported by the pinned engine;
            // allocation still validates every supplied cluster in both FATs.
            if (growth && restoredMedia is null)
            {
                Storage.Volatile[(int)(Storage.Input.PartitionFirst * 512 + 48)] = 0xff;
                Storage.Volatile[(int)(Storage.Input.PartitionFirst * 512 + 49)] = 0xff;
            }

            Storage.Mount();
            AtAttach = Device->Notifies;
            Input = new()
            {
                Engine = Storage.State,
                Epoch = Storage.Input.Epoch,
                Descriptors = Slots,
                Descriptions = Descriptions,
                DescriptorCount = fdCount,
                DescriptionCount = descriptionCount,
                Input = InputBytes,
                InputBytes = 8192,
                ExclusiveOwnerValidated = true
            };
            if (attach)
            {
                HostCheck.True(D.Start(State, Input));
                Attached = true;
            }

            if (locks)
            {
                PrepareLocks();
                HostCheck.True(D.EnableLocks(State, LockInput));
            }
        }

        internal void PrepareLocks()
        {
            Processes = (D.ProcessSlot*)NativeMemory.AllocZeroed(4, (nuint)sizeof(D.ProcessSlot));
            Locks = (D.RangeLock*)NativeMemory.AllocZeroed(32, (nuint)sizeof(D.RangeLock));
            Scratch = (D.RangeLock*)NativeMemory.AllocZeroed(65, (nuint)sizeof(D.RangeLock));
            LockInput = new()
            {
                Processes = Processes,
                Locks = Locks,
                Scratch = Scratch,
                ProcessCount = 4,
                LockCount = 32,
                ScratchCount = 65,
                PrimaryPid = 11
            };
        }

        internal byte[] LockBytes() => new ReadOnlySpan<byte>(Locks, 32 * sizeof(D.RangeLock)).ToArray();
        internal int ActiveLocks()
        {
            int result = 0, offset = WritableBlockSessionTests.ManagedOffset(Locks, "Active");
            for (int i = 0; i < 32; i++)
                if (*((byte*)(Locks + i) + offset) != 0)
                    result++;
            return result;
        }

        internal void CompleteHardware()
        {
            if (!Device->Pending)
                return;
            if (Record)
                Trace.Add((Device->Type, Device->Sector));
            bool failed = FailureType == Device->Type && (FailureSector is null || FailureSector == Device->Sector);
            if (failed)
            {
                FailureType = null;
                FailureSector = null;
            }

            Storage.Device.Complete(length: failed && Device->Type == 0 ? 1U : uint.MaxValue, status: failed ? (byte)1 : (byte)0);
        }

        internal D.Result Tick()
        {
            var result = D.Pump(State);
            CompleteHardware();
            return result;
        }

        internal D.Result Wait(D.Result begun)
        {
            if (begun.Phase != D.Progress.Pending)
                return begun;
            for (int i = 0; i < 100000; i++)
            {
                var result = Tick();
                if (result.Phase is D.Progress.Ready or D.Progress.RecoveryRequired or D.Progress.Rejected)
                    return result;
            }

            throw new InvalidOperationException("continuing descriptor did not complete");
        }

        internal D.Result Run(D.Result begun)
        {
            var result = Wait(begun);
            return result.Phase == D.Progress.Ready ? D.Complete(State, begun.Request) : result;
        }

        internal D.DescriptorToken Open(string path = "/data/DATA.BIN", uint flags = 0)
        {
            var result = Run(D.Open(State, Encoding.ASCII.GetBytes(path), flags));
            HostCheck.True(result.Phase == D.Progress.Immediate && result.Value >= 3);
            return result.Descriptor;
        }

        internal D.DescriptorToken OpenFor(D.ProcessToken process, string path = "/data/DATA.BIN", uint flags = 0)
        {
            var result = Run(D.OpenForProcess(State, process, Encoding.ASCII.GetBytes(path), flags));
            HostCheck.True(result.Phase == D.Progress.Immediate && result.Value >= 3);
            return result.Descriptor;
        }

        internal D.StatMetadata Stat(D.DescriptorToken file)
        {
            var request = D.BeginStat(State, file);
            HostCheck.Equal(D.Progress.Ready, Wait(request).Phase);
            D.StatMetadata facts = default;
            HostCheck.Equal(0L, D.CopyStat(State, request.Request, &facts, (uint)sizeof(D.StatMetadata)).Value);
            return facts;
        }

        internal byte[] Read(D.DescriptorToken file, uint bytes, ulong offset = 0, bool positioned = false)
        {
            var begun = D.BeginRead(State, file, bytes, offset, positioned);
            HostCheck.Equal(D.Progress.Ready, Wait(begun).Phase);
            byte[] output = new byte[bytes];
            var result = D.CopyRead(State, begun.Request, output);
            HostCheck.True(result.Phase == D.Progress.Immediate && result.Value >= 0);
            return output.AsSpan(0, (int)result.Value).ToArray();
        }

        internal void Drain()
        {
            for (int i = 0; i < 100000 && State->RequestActive; i++)
            {
                var result = Tick();
                if (result.Phase == D.Progress.RecoveryRequired)
                    break;
            }
        }

        internal void HoldMutationReady()
        {
            // Pump the original provider until it has accepted the mutation's
            // issued revision. Then finish only that retained engine request,
            // leaving its authenticated result unconsumed at the handoff.
            for (int i = 0; i < 100000 && !Storage.State->Dirty; i++)
                Tick();
            HostCheck.True(Storage.State->Dirty);
            for (int i = 0; i < 100000; i++)
            {
                var status = C.Pump(Storage.State);
                CompleteHardware();
                if (status == C.Status.Ready)
                    return;
                HostCheck.Equal(C.Status.Pending, status);
            }

            throw new InvalidOperationException("retained engine mutation did not become ready");
        }

        internal void HoldSearchStart()
        {
            for (int i = 0; i < 100000; i++)
            {
                object pending = typeof(D.State).GetField("Work", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(*State)!;
                if (pending.GetType().GetField("Step", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(pending)!.ToString() == "FindFree")
                {
                    HostCheck.True(Storage.State->RequestActive && !Device->Pending && !Storage.State->Dirty);
                    return;
                }

                HostCheck.Equal(D.Progress.Pending, Tick().Phase);
            }

            throw new InvalidOperationException("provider did not begin bounded free search");
        }

        internal void HoldSearchReady()
        {
            HoldSearchStart();
            for (int i = 0; i < 100000; i++)
            {
                var status = C.Pump(Storage.State);
                CompleteHardware();
                if (status == C.Status.Ready)
                    return;
                HostCheck.Equal(C.Status.Pending, status);
            }

            throw new InvalidOperationException("retained free search did not become ready");
        }

        internal void Independent(string name, byte[] expected)
        {
            var device = new PersistentDevice(Device);
            byte[] actual = new byte[expected.Length];
            var result = Fat32Parser.ReadRootFile(device, Storage.Input.PartitionFirst, name, actual);
            HostCheck.True(result.Succeeded && result.BytesRead == (ulong)expected.Length && actual.SequenceEqual(expected));
        }

        internal ulong Cursor(D.DescriptorToken file)
        {
            uint description = *(uint*)((byte*)(Slots + file.Number) + WritableBlockSessionTests.ManagedOffset(Slots + file.Number, "Description"));
            return *(ulong*)((byte*)(Descriptions + description - 1) + WritableBlockSessionTests.ManagedOffset(Descriptions + description - 1, "Position"));
        }

        internal C.FileToken CopiedFile(D.DescriptorToken file)
        {
            uint description = *(uint*)((byte*)(Slots + file.Number) + WritableBlockSessionTests.ManagedOffset(Slots + file.Number, "Description"));
            return *(C.FileToken*)((byte*)(Descriptions + description - 1) + WritableBlockSessionTests.ManagedOffset(Descriptions + description - 1, "File"));
        }

        public void Dispose()
        {
            // Fault cases end this simulated machine only after actual block
            // reset. They never count a refused dirty Detach/Stop as success.
            if (Attached && !State->Retired)
            {
                var result = D.Detach(State);
                if (result.Phase == D.Progress.Pending)
                {
                    Drain();
                    result = D.Detach(State);
                }

                HostCheck.True(result.Phase == D.Progress.Retired || result.Phase == D.Progress.RecoveryRequired && (Storage.State->Dirty || AllowCleanRetainedFailure && State->Quarantined && State->RequestActive));
            }

            Storage.Dispose();
            NativeMemory.Free(Scratch);
            NativeMemory.Free(Locks);
            NativeMemory.Free(Processes);
            NativeMemory.Free(InputBytes);
            NativeMemory.Free(Descriptions);
            NativeMemory.Free(Slots);
            NativeMemory.Free(State);
        }
    }
}
