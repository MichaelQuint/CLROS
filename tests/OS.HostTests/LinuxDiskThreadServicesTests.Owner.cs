using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Text;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Threading;
using OS.Pal;
using Fds = OS.Kernel.Compatibility.LinuxX64.LinuxDescriptorServices;
using Fs = OS.Storage.ReadOnlyWebRootFileSystem;
using Block = OS.Storage.ContinuingReadOnlyBlockService;
using Signals = OS.Kernel.Compatibility.LinuxX64.LinuxSignalServices;

internal static unsafe partial class LinuxDiskThreadServicesTests
{
    internal sealed class Owner : IDisposable
    {
        internal readonly LinuxThreadServicesTests.Model Thread = new(futex: true);
        internal readonly LinuxDescriptorServicesTests.Owner Files = new(descriptors: 32, descriptions: 32);
        internal readonly ReadOnlyWebRootFileSystemTests.Fixture Storage = new(slots: 8);
        internal readonly Fds.DiskState* Disk = (Fds.DiskState*)NativeMemory.AllocZeroed((nuint)sizeof(Fds.DiskState));
        private readonly Fds.DiskBinding* bindings = (Fds.DiskBinding*)NativeMemory.AllocZeroed(8, (nuint)sizeof(Fds.DiskBinding));
        private readonly Fds.DiskRequest* requests = (Fds.DiskRequest*)NativeMemory.AllocZeroed(8, (nuint)sizeof(Fds.DiskRequest));
        internal Fds.DiskRequest* Requests => requests;

        internal readonly OSHostHandle Main;
        private const ulong SignalKey = 0x64736b7369676e6c;
        private Signals.State* signalState;
        internal Owner(bool signals = false, bool large = false)
        {
            if (large)
            {
                System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(Storage.Disk.Sector(Storage.Disk.Data(4)).AsSpan(60), 8192);
                uint[] clusters = [10, 12, ..Enumerable.Range(1000, 14).Select(i => (uint)i)];
                for (int i = 0; i < clusters.Length; i++)
                {
                    Storage.Disk.SetFat(clusters[i], i + 1 == clusters.Length ? 0x0fffffff : clusters[i + 1]);
                    byte[] sector = Storage.Disk.Sector(Storage.Disk.Data(clusters[i]));
                    for (int n = 0; n < 512; n++)
                        sector[n] = unchecked((byte)((i * 512 + n) * 31 ^ ((i * 512 + n) >> 8) ^ 0x5a));
                }
            }

            Storage.MountReady();
            Files.Admission.User = Thread.DescriptorAccess;
            Files.Admission.CopyToUser = Thread.UserCopyOut;
            Files.Begin();
            HostCheck.True(Fds.AttachDisk(Files.State, Disk, new() { Mount = Storage.Mount, Bindings = bindings, Requests = requests, Epoch = Storage.Input.Epoch, FirmwareExited = true, SingleOwnerValidated = true }));
            HostCheck.True(Thread.Service->EnablePipes(Key, 2, Files.State));
            if (signals)
            {
                signalState = (Signals.State*)NativeMemory.AlignedAlloc(4096, 4096);
                new Span<byte>(signalState, 4096).Clear();
                HostCheck.True(Signals.Initialize(signalState, 4096, SignalKey, 2, new() { User = Thread.Access, CopyToUser = Thread.UserCopyOut, Capacity = 3, FirmwareExited = true, SingleOwnerValidated = true }));
                HostCheck.True(Thread.Service->EnableSignals(Key, 2, signalState));
            }

            Main = Thread.Start();
            HostCheck.Equal(Main.Value, Thread.Kernel.Dispatch().Value);
        }

        internal void PutPath(string value)
        {
            Encoding.ASCII.GetBytes(value + '\0').CopyTo(new Span<byte>(Thread.At(Path), value.Length + 1));
        }

        internal bool May(ulong start, ulong bytes) => Thread.Service->MayChangeUserRange(Key, 2, start, bytes);
        internal void Begin(ulong number, ulong a0, ulong a1 = 0, ulong a2 = 0, ulong a3 = 0) => HostCheck.Equal(LinuxSyscallAction.Blocked, Thread.DescriptorCall(number, a0, a1, a2, a3).Action);
        internal void Park()
        {
            HostCheck.True(Thread.Service->CommitDiskWait(Key, 2, Thread.Kernel.Capture(Main), out bool handoff));
            if (handoff)
                Thread.Kernel.Confirm();
        }

        internal void Ready()
        {
            for (int i = 0; i < 100000; i++)
            {
                Storage.Tick();
                HostCheck.True(Thread.Service->PollDiskReadiness(Key, 2));
                if (Thread.Kernel.Inspect(Main).Completion != KernelThreadTable.Completion.None)
                    return;
            }

            HostCheck.True(false, "draft_disk_wait_progress");
        }

        internal long Consume()
        {
            if (!Thread.Kernel.Table->Current.IsValid)
                HostCheck.Equal(Main.Value, Thread.Kernel.Dispatch().Value);
            HostCheck.Equal(Main.Value, Thread.Kernel.Table->Current.Value);
            var result = Thread.Service->CompleteDiskWait(Key, 2);
            HostCheck.Equal(LinuxSyscallAction.Return, result.Action);
            return result.Value;
        }

        internal long Run(ulong number, ulong a0, ulong a1 = 0, ulong a2 = 0, ulong a3 = 0)
        {
            var result = Thread.DescriptorCall(number, a0, a1, a2, a3);
            if (result.Action == LinuxSyscallAction.Return)
                return result.Value;
            HostCheck.Equal(LinuxSyscallAction.Blocked, result.Action);
            Park();
            Ready();
            return Consume();
        }

        internal uint Open(string value = "/app/wwwroot/index.html")
        {
            PutPath(value);
            long fd = Run(2, Path, 0x88100);
            HostCheck.True(fd >= 3);
            return (uint)fd;
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
                        HostCheck.True(Thread.Service->CommitDiskWait(Key, 2, Thread.Kernel.Capture(current), out bool handoff));
                        if (handoff)
                            Thread.Kernel.Confirm();
                        continue;
                    }

                    HostCheck.True(Thread.Service->CommitExit(Key, 2, Thread.Kernel.Capture(current), out _));
                    Thread.Kernel.Confirm();
                    HostCheck.True(Thread.Service->Reap(Key, 2, current, Thread.Kernel.SupervisorRsp, out _));
                }

                // Real block/FS retirement precedes orphan metadata scrub and
                // process detach. A failed reset must retain all native owners.
                HostCheck.Equal(Block.Status.Success, Block.Stop(Storage.Device));
                HostCheck.Equal(Fs.Status.Success, Fs.Stop(Storage.Mount));
                for (int i = 0; i < 64; i++)
                    HostCheck.True(Thread.Service->PollDiskReadiness(Key, 2));
                HostCheck.True(Thread.Service->Release(Key, 2));
                HostCheck.True(Fds.End(Files.State));
                if (signalState != null)
                    HostCheck.True(Signals.End(signalState, SignalKey, 2));
            }
            finally
            {
                if (signalState != null)
                    NativeMemory.AlignedFree(signalState);
                NativeMemory.Free(Disk);
                NativeMemory.Free(bindings);
                NativeMemory.Free(requests);
                Files.Dispose();
                Thread.Dispose();
                Storage.Dispose();
            }
        }
    }
}
