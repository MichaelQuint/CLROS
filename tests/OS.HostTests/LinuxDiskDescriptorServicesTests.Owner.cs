using System.Buffers.Binary;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Pal;
using Fds = OS.Kernel.Compatibility.LinuxX64.LinuxDescriptorServices;
using Fs = OS.Storage.ReadOnlyWebRootFileSystem;
using Block = OS.Storage.ContinuingReadOnlyBlockService;

internal static unsafe partial class LinuxDiskDescriptorServicesTests
{
    internal sealed class Owner : IDisposable
    {
        internal readonly LinuxThreadServicesTests.Model Thread = new(futex: true);
        internal readonly LinuxDescriptorServicesTests.Owner Files = new(descriptors: 32, descriptions: 32);
        internal readonly ReadOnlyWebRootFileSystemTests.Fixture Storage;
        internal readonly Fds.DiskState* Disk = (Fds.DiskState*)NativeMemory.AllocZeroed((nuint)sizeof(Fds.DiskState));
        internal readonly Fds.DiskBinding* Bindings = (Fds.DiskBinding*)NativeMemory.AllocZeroed(8, (nuint)sizeof(Fds.DiskBinding));
        internal readonly Fds.DiskRequest* Requests = (Fds.DiskRequest*)NativeMemory.AllocZeroed(8, (nuint)sizeof(Fds.DiskRequest));
        internal readonly List<Fds.DiskToken> Tokens = [];
        internal OSHostHandle Worker;
        internal bool Reaped;
        internal ulong ThreadOwner => (ulong)Thread.Service;
        internal Fds.DiskAdmission Admission => new()
        {
            Mount = Storage.Mount,
            Bindings = Bindings,
            Requests = Requests,
            Epoch = Storage.Input.Epoch,
            FirmwareExited = true,
            SingleOwnerValidated = true
        };

        // A supplied ready fixture is adopted and disposed by this owner. The
        // default still constructs/mounts the same healthy eight-slot fixture.
        internal Owner(bool attach = true, bool large = false, ReadOnlyWebRootFileSystemTests.Fixture? storage = null)
        {
            HostCheck.True(storage is null || !large && storage.Mount->Ready);
            Storage = storage ?? new(slots: 8);
            if (large)
            {
                const int bytes = 20000;
                BinaryPrimitives.WriteUInt32LittleEndian(Storage.Disk.Sector(Storage.Disk.Data(4)).AsSpan(32 + 28), bytes);
                uint[] clusters = [10, 12, .. Enumerable.Range(1000, 38).Select(i => (uint)i)];
                for (int i = 0; i < clusters.Length; i++)
                {
                    Storage.Disk.SetFat(clusters[i], i + 1 == clusters.Length ? 0x0fffffff : clusters[i + 1]);
                    byte[] sector = Storage.Disk.Sector(Storage.Disk.Data(clusters[i]));
                    for (int n = 0; n < 512 && i * 512 + n < bytes; n++)
                        sector[n] = Payload(i * 512 + n);
                }
            }

            if (storage is null)
                Storage.MountReady();
            Files.Admission.User = Thread.DescriptorAccess;
            Files.Admission.CopyToUser = Thread.UserCopyOut;
            Files.Begin();
            HostCheck.True(Thread.Service->EnablePipes(Key, 2, Files.State));
            if (attach)
                HostCheck.True(Fds.AttachDisk(Files.State, Disk, Admission));
            Worker = Thread.Start();
            HostCheck.Equal(Worker.Value, Thread.Kernel.Dispatch().Value);
        }

        internal LinuxSyscallFrame Call(ulong number, ulong a0 = 0, ulong a1 = 0, ulong a2 = 0, ulong a3 = 0)
        {
            var call = Thread.CurrentImage()->Frame;
            call.Number = number;
            call.Argument0 = a0;
            call.Argument1 = a1;
            call.Argument2 = a2;
            call.Argument3 = a3;
            return call;
        }

        internal void PutPath(string value)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(value + '\0');
            bytes.CopyTo(new Span<byte>(Thread.At(Path), bytes.Length));
        }

        internal Fds.DiskResult Begin(LinuxSyscallFrame call)
        {
            var result = Fds.BeginDisk(Files.State, ThreadOwner, Worker, call);
            if (result.Token.IsActive)
                Tokens.Add(result.Token);
            return result;
        }

        internal Fds.DiskResult Ready(Fds.DiskToken token)
        {
            Fds.DiskResult result = default;
            for (int i = 0; i < 100000; i++)
            {
                Storage.Tick();
                Fds.PumpDiskOne(Files.State, ThreadOwner);
                result = Fds.PollDisk(Files.State, ThreadOwner, token);
                if (result.Progress is Fds.DiskProgress.Ready or Fds.DiskProgress.Rejected)
                    return result;
            }

            HostCheck.True(false, "disk_bridge_progress");
            return result;
        }

        internal long Run(LinuxSyscallFrame call)
        {
            var begun = Begin(call);
            if (begun.Progress == Fds.DiskProgress.Immediate)
                return begun.Value;
            HostCheck.Equal(Fds.DiskProgress.Pending, begun.Progress);
            HostCheck.Equal(Fds.DiskProgress.Ready, Ready(begun.Token).Progress);
            var result = Fds.CompleteDisk(Files.State, ThreadOwner, Worker, begun.Token, call);
            HostCheck.Equal(Fds.DiskProgress.Immediate, result.Progress);
            return result.Value;
        }

        internal uint Open(string path = "/app/wwwroot/index.html")
        {
            PutPath(path);
            long value = Run(Call(2, Path, 0x88100));
            HostCheck.True(value >= 3);
            return (uint)value;
        }

        internal long Core(ulong number, ulong a0 = 0, ulong a1 = 0, ulong a2 = 0)
        {
            var r = Thread.DescriptorCall(number, a0, a1, a2);
            HostCheck.Equal(LinuxSyscallAction.Return, r.Action);
            return r.Value;
        }

        public void Dispose()
        {
            try
            {
                foreach (var token in Tokens)
                    Fds.CancelDisk(Files.State, ThreadOwner, token);
                HostCheck.Equal(Block.Status.Success, Block.Stop(Storage.Device));
                HostCheck.Equal(Fs.Status.Success, Fs.Stop(Storage.Mount));
                for (int i = 0; i < 32; i++)
                    Fds.PumpDiskOne(Files.State, ThreadOwner);
                for (uint fd = 3; fd < 32; fd++)
                    if (Fds.Capture(Files.State, fd, out _))
                        HostCheck.Equal(0L, Core(3, fd));
                if (Fds.DetachDisk(Files.State))
                {
                }
                else
                    HostCheck.True(new ReadOnlySpan<byte>(Disk, sizeof(Fds.DiskState)).IndexOfAnyExcept((byte)0) < 0);
                if (!Reaped)
                {
                    HostCheck.True(Thread.Service->Shutdown(Key, 2, 7));
                    HostCheck.True(Thread.Service->CommitExit(Key, 2, Thread.Kernel.Capture(Worker), out long clear) && clear == 0);
                    Thread.Kernel.Confirm();
                    HostCheck.True(Thread.Service->Reap(Key, 2, Worker, Thread.Kernel.SupervisorRsp, out _));
                }

                HostCheck.True(Thread.Service->Release(Key, 2));
                HostCheck.True(Fds.End(Files.State));
            }
            finally
            {
                NativeMemory.Free(Disk);
                NativeMemory.Free(Bindings);
                NativeMemory.Free(Requests);
                Files.Dispose();
                Thread.Dispose();
                Storage.Dispose();
            }
        }
    }
}
