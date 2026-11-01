using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using B = OS.Storage.WritableBlockSession;
using C = OS.Storage.ContinuingFat32Mutation;
using P = OS.Storage.WritableBlockReadPort;

internal static unsafe partial class WritableBlockReadPortTests
{
    private sealed class Fixture : IDisposable
    {
        private static long epoch = 0x8090;
        internal readonly ContinuingFat32MutationTests.Fixture Media = new(observe: true);
        private readonly byte* backing = (byte*)NativeMemory.AlignedAlloc(12288, 4096);
        internal readonly byte* Bytes = (byte*)NativeMemory.AlignedAlloc(4096, 4096);
        internal P.State* State => (P.State*)(backing + 4096);
        internal C.State* Engine => Media.State;
        internal B.State* Block => Media.Device.State;
        internal WritableBlockSessionTests.Fixture Device => Media.Device;
        internal Span<byte> Output => new(Bytes, 512);
        internal P.Admission Input;
        internal bool AllowInvalidStop;
        internal Fixture(bool start = true)
        {
            new Span<byte>(backing, 12288).Fill(0xa9); new Span<byte>(State, sizeof(P.State)).Clear(); Output.Fill(0x63);
            for (int i = 512; i < 4096; i++) Media.Volatile[i] = unchecked((byte)(i * 37 + 9));
            Media.Mount(); Input = new() { Engine = Engine, Device = Block, Epoch = (ulong)Interlocked.Increment(ref epoch), EngineEpoch = Media.Input.Epoch,
                DeviceEpoch = Device.Input.Epoch, FirstLba = 1, LastLba = 7 };
            if (start) HostCheck.Equal(P.Status.Success, P.Start(State, Input));
        }
        internal B.Observation Sample() => *Device.Observation;
        internal P.Token Read(ulong lba) { P.Token token = default; HostCheck.Equal(P.Status.Success, P.Submit(State, lba, &token)); Sample(); return token; }
        internal void FinishRead(P.Token token)
        { Device.Complete(); P.Completion result = default; HostCheck.Equal(P.Status.Ready, P.Poll(State, token, &result)); Sample(); HostCheck.Equal(P.Status.Success, P.Copy(State, token, Output)); Sample(); }
        internal C.FileToken Open(out C.RequestToken consumed)
        {
            C.RequestToken request = default; HostCheck.Equal(C.Status.Pending, C.BeginOpenRoot(Engine, "DATA.BIN", &request));
            HostCheck.Equal(C.Status.Ready, RunEngine()); C.FileToken file = default; HostCheck.Equal(C.Status.Success, C.CopyOpen(Engine, request, &file)); consumed = request; return file;
        }
        internal C.Status RunEngine()
        {
            for (int i = 0; i < 100000; i++) { var status = Media.Turn(); Sample(); if (status != C.Status.Pending) return status; }
            throw new InvalidOperationException("borrowed port engine did not terminate");
        }
        internal ulong[] Owned() => new[] { (ulong)State, (ulong)State + (ulong)sizeof(P.State) - 8, (ulong)Engine,
            (ulong)Media.Input.Changes, (ulong)Media.Input.Before, (ulong)Media.Input.After, (ulong)Media.Input.DirectoryMap,
            (ulong)Media.Input.FileMap, (ulong)Media.Input.WalkMap, (ulong)Media.Input.Sector, (ulong)Media.Input.Fat0, (ulong)Media.Input.Fat1,
            (ulong)Media.Input.Input, (ulong)Media.Input.Name, (ulong)Media.Input.LongName, (ulong)Media.Input.EncodedEntries,
            (ulong)Block, (ulong)Device.Pool, (ulong)Device.Slot, (ulong)Device.Data, (ulong)Device.Observation, 0x400000UL, 0x402000UL, 0x403000UL };
        internal (ulong Start, ulong Bytes)[] OwnedExtents() => new (ulong, ulong)[]
        {
            ((ulong)State, (ulong)sizeof(P.State)), ((ulong)Engine, (ulong)sizeof(C.State)),
            ((ulong)Media.Input.Changes, C.JournalChanges * (ulong)sizeof(OS.Storage.Fat32Mutation.Change)),
            ((ulong)Media.Input.Before, C.JournalChanges * 512UL), ((ulong)Media.Input.After, C.JournalChanges * 512UL),
            ((ulong)Media.Input.DirectoryMap, C.MaximumClusters * 4UL), ((ulong)Media.Input.FileMap, C.MaximumClusters * 4UL),
            ((ulong)Media.Input.WalkMap, C.MaximumClusters * 4UL), ((ulong)Media.Input.Sector, 512),
            ((ulong)Media.Input.Fat0, 512), ((ulong)Media.Input.Fat1, 512), ((ulong)Media.Input.Input, C.WriteBytes),
            ((ulong)Media.Input.Name, C.NameWords * 2UL), ((ulong)Media.Input.LongName, C.VfatWords * 2UL),
            ((ulong)Media.Input.EncodedEntries, C.EncodedEntryBytes), ((ulong)Block, (ulong)sizeof(B.State)),
            ((ulong)Device.Pool, (ulong)sizeof(OS.Drivers.DmaBufferPool)), ((ulong)Device.Slot, (ulong)sizeof(OS.Drivers.DmaBufferSlot)),
            ((ulong)Device.Data, 4096), ((ulong)Device.Observation, (ulong)sizeof(B.Observation)),
            (Device.Input.Common.VirtualStart, Device.Input.Common.Bytes), (Device.Input.Notify.VirtualStart, Device.Input.Notify.Bytes),
            (Device.Input.Device.VirtualStart, Device.Input.Device.Bytes)
        };
        public void Dispose()
        {
            HostCheck.Equal(B.Status.Success, B.Stop(Block)); // External fixture owner, never the port.
            var stopped = P.Stop(State); if (!AllowInvalidStop) HostCheck.Equal(P.Status.Success, stopped);
            HostCheck.True(new ReadOnlySpan<byte>(backing, 4096).IndexOfAnyExcept((byte)0xa9) < 0 &&
                new ReadOnlySpan<byte>(backing + 4096 + sizeof(P.State), 8192 - sizeof(P.State)).IndexOfAnyExcept((byte)0xa9) < 0);
            Media.Dispose(); NativeMemory.AlignedFree(backing); NativeMemory.AlignedFree(Bytes);
        }
    }
}
