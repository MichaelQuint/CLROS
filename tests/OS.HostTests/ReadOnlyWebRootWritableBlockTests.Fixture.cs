using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using OS.Storage;
using B = OS.Storage.WritableBlockSession;
using C = OS.Storage.ContinuingFat32Mutation;
using Fs = OS.Storage.ReadOnlyWebRootFileSystem;
using P = OS.Storage.WritableBlockReadPort;
using Image = OS.Tooling.CombinedWebDataImageFixture;
using Data = OS.Tooling.WritableDataImageFixture;

internal static unsafe partial class ReadOnlyWebRootWritableBlockTests
{
    internal sealed class Fixture : IDisposable
    {
        internal readonly WritableBlockSessionTests.Fixture Device;
        internal B.State* Block => Device.State;
        internal readonly C.State* Engine = (C.State*)NativeMemory.AllocZeroed((nuint)sizeof(C.State));
        internal readonly P.State* Port = (P.State*)NativeMemory.AllocZeroed((nuint)sizeof(P.State));
        private readonly byte* engineArena = (byte*)NativeMemory.AlignedAlloc(208896, 4096);
        private readonly byte* fsArena = (byte*)NativeMemory.AlignedAlloc(524288, 4096);
        internal readonly byte* Output = (byte*)NativeMemory.AllocZeroed(128);
        internal readonly Fs.Mount* Mount;
        internal readonly Fs.Operation* Operations;
        private readonly Fs.FileHandle* files;
        private readonly Fs.CacheSlot* cache;
        internal readonly C.Admission EngineInput;
        internal readonly P.Admission PortInput;
        internal readonly Fs.BorrowedAdmission Input;
        internal readonly ulong EngineBackingBytes, FsBackingBytes, MountedEngineGeneration;
        internal readonly List<B.Observation> Observations = [];
        internal bool AllowStaleBacking;
        internal Span<byte> Persistent => new(Device.Hardware->Persistent, 128 * 1024 * 1024);
        internal Fixture(byte[] raw, Image.Receipt receipt, bool start = true)
        {
            Device = new(receipt.Image.CapacitySectors, observe: true); Device.UseMedium(raw); HostCheck.Equal(B.Status.Success, Device.Start());
            new Span<byte>(engineArena, 208896).Fill(0xa7); ulong at = (ulong)engineArena + 4096;
            void* EngineTake(ulong bytes) { at = (at + 15) & ~15UL; ulong value = at; at += bytes; return (void*)value; }
            EngineInput = new() { Device = Block, Owner = 719, Epoch = 720, PartitionFirst = receipt.Image.DataFirst, PartitionLast = receipt.Image.DataLast,
                ChangeCapacity = C.JournalChanges, ChainCapacity = C.MaximumClusters, InputCapacity = C.WriteBytes, WorkBudget = 100000,
                Changes = (Fat32Mutation.Change*)EngineTake(C.JournalChanges * (ulong)sizeof(Fat32Mutation.Change)), Before = (byte*)EngineTake(C.JournalChanges * 512UL), After = (byte*)EngineTake(C.JournalChanges * 512UL),
                DirectoryMap = (uint*)EngineTake(C.MaximumClusters * 4UL), FileMap = (uint*)EngineTake(C.MaximumClusters * 4UL), WalkMap = (uint*)EngineTake(C.MaximumClusters * 4UL),
                Sector = (byte*)EngineTake(512), Fat0 = (byte*)EngineTake(512), Fat1 = (byte*)EngineTake(512), Input = (byte*)EngineTake(C.WriteBytes),
                Name = (ushort*)EngineTake(C.NameWords * 2UL), LongName = (ushort*)EngineTake(C.VfatWords * 2UL), EncodedEntries = (byte*)EngineTake(C.EncodedEntryBytes) };
            EngineBackingBytes = at - ((ulong)engineArena + 4096); HostCheck.True(EngineBackingBytes < 200704); new Span<byte>(engineArena + 4096, (int)EngineBackingBytes).Clear();
            HostCheck.Equal(C.Status.Pending, C.Start(Engine, EngineInput)); RunEngine(); MountedEngineGeneration = Block->Submitted;
            PortInput = new() { Engine = Engine, Device = Block, Epoch = 721, EngineEpoch = EngineInput.Epoch, DeviceEpoch = Device.Input.Epoch, FirstLba = receipt.Image.WebFirst, LastLba = receipt.Image.WebLast };
            HostCheck.Equal(P.Status.Success, P.Start(Port, PortInput));
            new Span<byte>(fsArena, 524288).Fill(0xb9); at = (ulong)fsArena + 4096;
            void* FsTake(ulong bytes) { at = (at + 15) & ~15UL; ulong value = at; at += bytes; new Span<byte>((void*)value, (int)bytes).Clear(); return (void*)value; }
            Mount = (Fs.Mount*)FsTake((ulong)sizeof(Fs.Mount)); Operations = (Fs.Operation*)FsTake(4UL * (ulong)sizeof(Fs.Operation));
            files = (Fs.FileHandle*)FsTake(4UL * (ulong)sizeof(Fs.FileHandle)); cache = (Fs.CacheSlot*)FsTake(2UL * (ulong)sizeof(Fs.CacheSlot));
            for (int i = 0; i < 4; i++)
            { Operations[i].Storage = new() { Path = (byte*)FsTake(256), Sector = (byte*)FsTake(512), Name = (ushort*)FsTake(520), History = (uint*)FsTake(4096 * 4), HistoryCapacity = 4096, Range = (byte*)FsTake(65536), RangeCapacity = 65536 };
                files[i].Clusters = (uint*)FsTake(4096 * 4); files[i].ClusterCapacity = 4096; }
            for (int i = 0; i < 2; i++) cache[i].Data = (byte*)FsTake(512);
            FsBackingBytes = at - ((ulong)fsArena + 4096); HostCheck.True(FsBackingBytes < 516096);
            Input = new() { ReadPort = Port, PortEpoch = PortInput.Epoch, Epoch = 722, Operations = Operations, Files = files, Cache = cache,
                OperationCount = 4, FileCount = 4, CacheCount = 2, DirectoryEntryBudget = 100000 };
            if (start) HostCheck.Equal(Fs.Status.Pending, Fs.StartBorrowed(Mount, Input));
        }
        internal void Sample() { if (Device.Observation->Request.Generation != 0) Observations.Add(*Device.Observation); }
        internal C.Status EngineTurn() { var status = C.Pump(Engine); Sample(); if (Device.Hardware->Pending) Device.Complete(); return status; }
        internal void RunEngine() { for (int i = 0; i < 100000; i++) { var status = EngineTurn(); if (status == C.Status.Ready) return; HostCheck.Equal(C.Status.Pending, status); } throw new InvalidOperationException("engine_bound"); }
        internal C.FileToken OpenData(string name)
        { C.RequestToken token = default; HostCheck.Equal(C.Status.Pending, C.BeginOpenRoot(Engine, name, &token)); RunEngine(); C.FileToken result = default; HostCheck.Equal(C.Status.Success, C.CopyOpen(Engine, token, &result)); return result; }
        internal void MountReady()
        { for (int i = 0; i < 100000; i++) { if (Device.Hardware->Pending) Device.Complete(); var status = Fs.PumpMount(Mount); Sample(); if (status == Fs.Status.Ready) return; HostCheck.Equal(Fs.Status.Pending, status); } throw new InvalidOperationException("mount_bound"); }
        internal Fs.OperationToken BeginOpen(string path) { Fs.OperationToken token = default; HostCheck.Equal(Fs.Status.Pending, Fs.BeginOpen(Mount, Encoding.ASCII.GetBytes(path), &token)); return token; }
        internal Fs.OpenResult Open(string path) { var token = BeginOpen(path); HostCheck.Equal(Fs.Status.Ready, Drive(token)); Fs.OpenResult result = default; HostCheck.Equal(Fs.Status.Success, Fs.CopyOpen(Mount, token, &result)); return result; }
        internal Fs.OperationToken BeginRead(Fs.FileToken file, ulong offset, uint bytes) { Fs.OperationToken token = default; HostCheck.Equal(Fs.Status.Pending, Fs.BeginRead(Mount, file, offset, bytes, &token)); return token; }
        internal Fs.Status Drive(Fs.OperationToken token)
        { for (int i = 0; i < 100000; i++) { if (Device.Hardware->Pending) Device.Complete(); var status = Fs.Poll(Mount, token); Sample(); if (status is not (Fs.Status.Pending or Fs.Status.Busy)) return status; } throw new InvalidOperationException("fs_bound"); }
        internal void Read(Fs.FileToken file, ulong offset, uint requested, Span<byte> output, uint wanted)
        { var token = BeginRead(file, offset, requested); HostCheck.Equal(Fs.Status.Ready, Drive(token)); uint bytes = 99; HostCheck.Equal(wanted == 0 ? Fs.Status.EndOfFile : Fs.Status.Success, Fs.CopyRead(Mount, token, output, &bytes)); HostCheck.Equal(wanted, bytes); }
        internal void WaitForPort(Fs.OperationToken token)
        { for (int i = 0; i < 10000 && !Port->RequestActive; i++) { var status = Fs.Poll(Mount, token); Sample(); HostCheck.Equal(Fs.Status.Pending, status); } HostCheck.True(Port->RequestActive && Device.Hardware->Pending); }
        internal void HoldPort() { Device.Complete(); P.Completion result = default; var token = BorrowedToken(ref *Mount); HostCheck.Equal(P.Status.Ready, P.Poll(Port, token, &result)); Sample(); }
        internal byte[] CopyFsBacking() => new ReadOnlySpan<byte>(fsArena + 4096, (int)FsBackingBytes).ToArray();
        internal IEnumerable<(ulong Start, ulong Bytes)> FsExtents()
        {
            List<(ulong, ulong)> result = [((ulong)Mount, (ulong)sizeof(Fs.Mount)), ((ulong)Operations, 4UL * (ulong)sizeof(Fs.Operation)), ((ulong)files, 4UL * (ulong)sizeof(Fs.FileHandle)), ((ulong)cache, 2UL * (ulong)sizeof(Fs.CacheSlot))];
            for (int i = 0; i < 4; i++) { var s = Operations[i].Storage; result.Add(((ulong)s.Path, 256)); result.Add(((ulong)s.Sector, 512)); result.Add(((ulong)s.Name, 520)); result.Add(((ulong)s.History, 16384)); result.Add(((ulong)s.Range, 65536)); result.Add(((ulong)files[i].Clusters, 16384)); }
            for (int i = 0; i < 2; i++) result.Add(((ulong)cache[i].Data, 512)); return result;
        }
        public void Dispose()
        {
            if (AllowStaleBacking)
            {
                // Negative supervisor-ABA test only: retain every old object to
                // this fake-owner boundary, never relabel it logically retired.
                if (Block->RequiresCleanup) HostCheck.Equal(B.Status.Success, B.Stop(Block));
                Device.CheckGuards(); Device.Dispose(); NativeMemory.Free(Engine); NativeMemory.Free(Port); NativeMemory.Free(Output); NativeMemory.AlignedFree(engineArena); NativeMemory.AlignedFree(fsArena); return;
            }
            if (Mount->RequiresCleanup) { var status = Fs.Stop(Mount); Sample(); if (status == Fs.Status.Pending) { if (Device.Hardware->Pending) Device.Complete(); HostCheck.Equal(Fs.Status.Success, Fs.Stop(Mount)); Sample(); } }
            if (!Port->Retired) { var status = P.Stop(Port); Sample(); if (status == P.Status.Pending) { if (Device.Hardware->Pending) Device.Complete(); P.Completion result = default; var token = BorrowedToken(ref *Mount); P.Poll(Port, token, &result); Sample(); status = P.Stop(Port); } if (status != P.Status.Success) { B.Stop(Block); HostCheck.Equal(P.Status.Success, P.Stop(Port)); } }
            if (Mount->RequiresCleanup) HostCheck.Equal(Fs.Status.Success, Fs.Stop(Mount));
            if (Engine->IsMounted && !Engine->Dirty) { var status = C.Stop(Engine); if (status != C.Status.Success) { B.Stop(Block); HostCheck.Equal(C.Status.Success, C.Stop(Engine)); } }
            if (Block->RequiresCleanup) HostCheck.Equal(B.Status.Success, B.Stop(Block));
            HostCheck.True(new ReadOnlySpan<byte>(engineArena, 4096).IndexOfAnyExcept((byte)0xa7) < 0 && new ReadOnlySpan<byte>(engineArena + 204800, 4096).IndexOfAnyExcept((byte)0xa7) < 0 &&
                new ReadOnlySpan<byte>(fsArena, 4096).IndexOfAnyExcept((byte)0xb9) < 0 && new ReadOnlySpan<byte>(fsArena + 520192, 4096).IndexOfAnyExcept((byte)0xb9) < 0);
            Device.CheckGuards(); Device.Dispose(); NativeMemory.Free(Engine); NativeMemory.Free(Port); NativeMemory.Free(Output); NativeMemory.AlignedFree(engineArena); NativeMemory.AlignedFree(fsArena);
        }
    }
}
