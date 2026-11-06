using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Runtime.InteropServices;
using OS.Std.Hash;
using OS.Storage;
using OS.Tooling;
using Image = OS.Tooling.WritableDataImageFixture;
using B = OS.Storage.WritableBlockSession;
using C = OS.Storage.ContinuingFat32Mutation;
using W = OS.Kernel.Compatibility.LinuxX64.LinuxContinuingWritableDescriptorServices;

// Host image/extraction controls only. No native/device/guest persistence claim.
internal static unsafe partial class WritableDataImageFixtureTests
{
    private sealed class MutationMedia : IDisposable
    {
        internal readonly WritableBlockSessionTests.Fixture Device;
        internal readonly C.State* State = (C.State*)NativeMemory.AllocZeroed((nuint)sizeof(C.State));
        private readonly byte* backing = (byte*)NativeMemory.AlignedAlloc(48 * 4096, 4096);
        internal uint Outs, Flushes;
        internal MutationMedia(byte[] image, uint workBudget = 100000, ulong epoch = 702)
        {
            Device = new((ulong)Image.ImageBytes / 512);
            Device.UseMedium(image);
            HostCheck.Equal(B.Status.Success, Device.Start());
            ulong cursor = (ulong)backing;
            ulong Take(ulong count)
            {
                ulong address = (cursor + 15) & ~15UL;
                cursor = address + count;
                return address;
            }

            C.Admission input = new()
            {
                Device = Device.State,
                Owner = 701,
                Epoch = epoch,
                PartitionFirst = Image.PartitionFirstLba,
                PartitionLast = Image.PartitionLastLba,
                ChangeCapacity = C.JournalChanges,
                ChainCapacity = C.MaximumClusters,
                InputCapacity = C.WriteBytes,
                WorkBudget = workBudget,
                Changes = (Fat32Mutation.Change*)Take(C.JournalChanges * (ulong)sizeof(Fat32Mutation.Change)),
                Before = (byte*)Take(C.JournalChanges * 512UL),
                After = (byte*)Take(C.JournalChanges * 512UL),
                DirectoryMap = (uint*)Take(C.MaximumClusters * 4UL),
                FileMap = (uint*)Take(C.MaximumClusters * 4UL),
                WalkMap = (uint*)Take(C.MaximumClusters * 4UL),
                Sector = (byte*)Take(512),
                Fat0 = (byte*)Take(512),
                Fat1 = (byte*)Take(512),
                Input = (byte*)Take(C.WriteBytes),
                Name = (ushort*)Take(C.NameWords * 2UL),
                LongName = (ushort*)Take(C.VfatWords * 2UL),
                EncodedEntries = (byte*)Take(C.EncodedEntryBytes)
            };
            HostCheck.True(cursor - (ulong)backing <= 48 * 4096UL);
            new Span<byte>(backing, 48 * 4096).Clear();
            HostCheck.Equal(C.Status.Pending, C.Start(State, input));
            Run();
        }

        internal void Run()
        {
            for (int i = 0; i < 100000; i++)
            {
                var status = C.Pump(State);
                CompleteHardware();
                if (status != C.Status.Pending)
                {
                    HostCheck.Equal(C.Status.Ready, status);
                    return;
                }
            }

            throw new InvalidOperationException("transition shared-media mutation did not terminate");
        }

        internal void CompleteHardware()
        {
            if (!Device.Hardware->Pending)
                return;
            if (Device.Hardware->Type == 1)
                Outs++;
            if (Device.Hardware->Type == 4)
                Flushes++;
            Device.Complete();
        }

        internal C.FileToken Open(string name)
        {
            C.RequestToken request = default;
            HostCheck.Equal(C.Status.Pending, C.BeginOpenRoot(State, name, &request));
            Run();
            C.FileToken file = default;
            HostCheck.Equal(C.Status.Success, C.CopyOpen(State, request, &file));
            return file;
        }

        internal byte[] Snapshot() => new ReadOnlySpan<byte>(Device.Hardware->Persistent, Image.ImageBytes).ToArray();
        public void Dispose()
        {
            HostCheck.Equal(B.Status.Success, B.Stop(Device.State));
            HostCheck.Equal(C.Status.Success, C.Stop(State));
            Device.CheckGuards();
            Device.Dispose();
            NativeMemory.AlignedFree(backing);
            NativeMemory.Free(State);
        }
    }
}
