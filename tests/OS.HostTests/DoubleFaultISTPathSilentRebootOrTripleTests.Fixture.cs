using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Kernel.Memory;
using Emitter = OS.Kernel.Diagnostics.DoubleFaultISTPathSilentRebootOrTriple;

internal static unsafe partial class DoubleFaultISTPathSilentRebootOrTripleTests
{
    private sealed class Fixture : IDisposable
    {
        private readonly byte* _stream, _context, _slots, _idt;
        public readonly ulong* Tables;
        public readonly ulong Code, Data, Root;
        public readonly SeparatePageFaultPageTableResult Image;
        public DoubleFaultIstRequest Request;
        public byte* Slots => _slots + 4096;
        public byte* Idt => _idt + 4096;
        public DoubleFaultIstBuffers Buffers => With();

        public DoubleFaultIstBuffers With(ulong? context = null, ulong contextCapacity = 4096, ulong? stream = null, ulong streamCapacity = 8192, ulong? slots = null, ulong slotCapacity = 4096, ulong? idt = null, ulong idtCapacity = 4096) => new((byte*)(context ?? (ulong)Context), contextCapacity, (byte*)(stream ?? (ulong)Stream), streamCapacity, (byte*)(slots ?? (ulong)Slots), slotCapacity, (byte*)(idt ?? (ulong)Idt), idtCapacity);
        public byte* Stream => _stream + 8;
        public byte* Context => _context + 8;
        public byte[] StreamBytes => new Span<byte>(Stream, 8192).ToArray();
        public byte[] ContextBytes => new Span<byte>(Context, 4096).ToArray();
        public byte[] TableBytes => new Span<byte>(Tables, 43 * 4096).ToArray();

        public Fixture(ulong code = C, ulong data = D, ulong root = T, byte width = 36)
        {
            Code = code;
            Data = data;
            Root = root;
            _stream = (byte*)NativeMemory.AlignedAlloc(8192 + 16, 8);
            _context = (byte*)NativeMemory.AlignedAlloc(4096 + 16, 8);
            _slots = (byte*)NativeMemory.AlignedAlloc(12288, 4096);
            _idt = (byte*)NativeMemory.AlignedAlloc(12288, 4096);
            new Span<byte>(_slots, 12288).Fill(0xa5);
            new Span<byte>(_idt, 12288).Fill(0xa5);
            Tables = (ulong*)NativeMemory.AlignedAlloc(43 * 4096, 4096);
            new Span<byte>(_stream, 8192 + 16).Fill(0xa5);
            new Span<byte>(_context, 4096 + 16).Fill(0xa5);
            new Span<byte>(Tables, 43 * 4096).Clear();
            X64PatMappingToken.TryCreate(Pat, 0, 255, 255, out var token);
            var conditions = X64PageTableEncodingPreconditions.CreateObserved(width, true, true, false, false, token);
            SeparatePageFaultMappingRange* ranges = stackalloc SeparatePageFaultMappingRange[6];
            ranges[0] = new(code, 3, PageProtection.Read | PageProtection.Execute);
            ranges[1] = new(data, 2, PageProtection.Read | PageProtection.Write);
            for (int i = 2; i < 6; i++)
                ranges[i] = new(data + (ulong)(i * 2 - 1) * 4096, 1, PageProtection.Read | PageProtection.Write);
            X64PageTableImagePage* pages = stackalloc X64PageTableImagePage[43];
            Image = SeparateDisabledDefaultPageFaultPageTables.Build(new(Address(root), 43, conditions), ranges, 6, 6, pages, 43, Tables, 43 * 512);
            Require(Image.Succeeded && Image.MappedPages == 52, "real six-range image");
            Request = new(code, data, root, Mode(width), Pat, Image);
        }

        public void Prepare()
        {
            var context = Emitter.PrepareContext(Buffers, Request);
            Require(context.ContextPrepared, "prepare context " + context.Status + " offset=" + context.FirstMismatchOffset);
            Require(Emitter.MaterializeArmedIdt(Buffers, Request).Succeeded, "armed materialization");
            var stream = Emitter.EmitStream(Buffers, Request);
            Require(stream.StreamEmitted, "emit stream " + stream.Status);
            Require(!stream.GuestExecution && !stream.HardwareStateCaptured && !stream.FaultTriggered && !stream.PostExitBootServices, "no hardware claims");
        }

        public void CheckCanaries()
        {
            for (int i = 0; i < 4096; i++)
                Require(_slots[i] == 0xa5 && _slots[8192 + i] == 0xa5 && _idt[i] == 0xa5 && _idt[8192 + i] == 0xa5, "slot/IDT canaries");
            for (int i = 0; i < 8; i++)
                Require(_stream[i] == 0xa5 && _stream[8192 + 8 + i] == 0xa5 && _context[i] == 0xa5 && _context[4096 + 8 + i] == 0xa5, "canaries");
        }

        public void Dispose()
        {
            NativeMemory.AlignedFree(_stream);
            NativeMemory.AlignedFree(_context);
            NativeMemory.AlignedFree(Tables);
            NativeMemory.AlignedFree(_slots);
            NativeMemory.AlignedFree(_idt);
        }
    }
}
