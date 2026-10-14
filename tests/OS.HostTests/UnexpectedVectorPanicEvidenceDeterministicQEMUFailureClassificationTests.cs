using System.Buffers.Binary;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using OS.Kernel.Diagnostics;
using Leaf = OS.Kernel.Diagnostics.UnexpectedVectorPanicEvidenceDeterministicQEMUFailureClassification;

internal static unsafe class UnexpectedVectorPanicEvidenceDeterministicQEMUFailureClassificationTests
{
    [HostFixture(HostLane.Native)]
    internal static void RunAll()
    {
        Console.WriteLine("TEST_START m02.016");
        try
        {
            Group("layout and preparation", Preparation);
            Group("full extents and unchanged rejection", Rejections);
            Group("complete readback corruption", Corruption);
            Group("independent instruction and ABI model", Instructions);
            Group("full width capture routing", Routes);
            Group("captured state and exact output", Reporting);
            Group("two TEMT handoffs and finite polls", Drains);
            Group("THRE limits and no fallback", TransmitLimits);
            Group("zero allocation and canaries", NoAllocation);
            Group("actual no stdlib closure", CheckNoStdLibCompilation);
            Console.WriteLine("CLROS_M02_016_HOST_READY guestExecution=false softwareInterrupt=false currentState=synthetic nativeStackProven=false");
            Console.WriteLine("TEST_PASS m02.016");
        }
        catch (Exception error)
        {
            Console.WriteLine("TEST_FAIL m02.016 " + error.GetType().Name);
            throw;
        }
    }
    private static void Group(string suffix, Action action)
    {

        HostCheck.Run("m02.016 " + suffix, action);
    }
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
    private static ulong U(byte* p, int offset) => BinaryPrimitives.ReadUInt64LittleEndian(new ReadOnlySpan<byte>(p + offset, 8));
    private static void Q(byte* p, int offset, ulong value) => BinaryPrimitives.WriteUInt64LittleEndian(new Span<byte>(p + offset, 8), value);
    private static UnexpectedVectorPanicRequest Request(ulong c = 0x100000000, ulong d = 0x200000000,
        ulong image = 0x300000000, ulong bytes = 0x10000, ulong callback = 0x300001234,
        ulong cs = 0x38, ulong cr3 = 0x12345018, bool enabled = true) => new(c, d, image, bytes, callback, cs, cr3, enabled);

    private sealed class Fixture : IDisposable
    {
        private readonly byte* _code = (byte*)NativeMemory.AlignedAlloc(16384, 4096);
        private readonly byte* _data = (byte*)NativeMemory.AlignedAlloc(36864, 4096);
        public byte* Code => _code + 4096;
        public byte* Data => _data + 4096;
        public byte* Context => Data + 4096;
        public byte* Capture => Data + 28488;
        public UnexpectedVectorPanicRequest R;
        public Fixture(UnexpectedVectorPanicRequest? request = null)
        {
            new Span<byte>(_code, 16384).Fill(0xa5); new Span<byte>(_data, 36864).Fill(0xa5);
            R = request ?? Request(); Prepare();
        }
        public void Prepare()
        {
            Require(Leaf.PrepareContext(Code, 8192, Data, 28672, R).Succeeded, "prepare");
            Require(Leaf.EmitStream(Code, 8192, Data, 28672, R).Succeeded, "emit");
            Require(Leaf.ValidateStreamReadBack(Code, 8192, Data, 28672, R).Succeeded, "pristine readback");
        }
        public void Captured(ulong salt = 0)
        {
            Prepare();
            for (int i = 0; i < 15; i++) Q(Capture, i * 8, 0xfedcba9876500000UL + (ulong)i + salt);
            Q(Capture, 120, 32); Q(Capture, 128, 0); Q(Capture, 136, 0);
            Q(Capture, 144, R.CodeAddress + 4096 + 960 + 2); Q(Capture, 152, R.FirmwareCs);
            Q(Capture, 160, 0x446); Q(Capture, 168, R.DataAddress + 28672); Q(Capture, 176, 16);
            Context[10] = 39; Q(Context, 12, R.DataAddress + 4096 + 24); Context[20] = 24;
            Context[37] |= 1; Context[45] |= 1; Context[53] = 0x8b;
            Context[192] = 0xff; Context[193] = 15; Q(Context, 194, R.DataAddress);
            Q(Context, 288, R.DataAddress + 28488); Q(Context, 296, 0xfeedface12345678 + salt);
            Q(Context, 304, R.FirmwareCr3); Q(Context, 312, 8); Q(Context, 320, 16);
            Q(Context, 328, 16); Q(Context, 336, 16); Q(Context, 344, 24);
            Q(Context, 352, R.FirmwareCs); Q(Context, 360, 0x00a09b00); Q(Context, 368, R.FirmwareCr3);
            Require(Leaf.ValidateCapturedState(Code, 8192, Data, 28672, R, out _).Succeeded, "synthetic state");
        }
        public void Canaries()
        {
            for (int i = 0; i < 4096; i++) Require(_code[i] == 0xa5 && _code[12288 + i] == 0xa5 &&
                _data[i] == 0xa5 && _data[32768 + i] == 0xa5, "full-span canary");
        }
        public void Dispose() { Canaries(); NativeMemory.AlignedFree(_code); NativeMemory.AlignedFree(_data); }
    }

    private static void Preparation()
    {
        using var f = new Fixture();
        Require(!default(UnexpectedVectorPanicResult).Succeeded && !Leaf.EnabledByDefault, "default fails closed");
        Require(sizeof(UnexpectedVectorPanicRequest) == 64 && Leaf.CaptureBytes == 40 + 24 + 15 * 8 &&
            Leaf.CalleeDepthBytes == 184 + 32 + 8 + 8, "independent layout arithmetic");
        Require(Leaf.CodeBytes == 8192 && Leaf.DataBytes == 28672 && Leaf.ContextOffset == 4096 &&
            Leaf.CaptureOffset == 28488 && 28672 - Leaf.CalleeDepthBytes == 28440 &&
            Leaf.MetadataFieldCount == 24 && Leaf.MetadataByteCount == 650, "fixed C2/D7 offsets and unchanged metadata");
        Require(U(f.Context, 2) == f.R.DataAddress + 4120 && U(f.Context, 68) == f.R.DataAddress + 28672 &&
            U(f.Context, 100) == f.R.DataAddress + 12288 && U(f.Context, 108) == f.R.DataAddress + 16384 &&
            U(f.Context, 116) == f.R.DataAddress + 20480 && f.Context[166] == 104, "GDT/TSS layout");
        for (int vector = 0; vector < 256; vector++)
        {
            byte* gate = f.Data + 16 * vector; ulong address = (ulong)(ushort)(gate[0] | gate[1] << 8) |
                ((ulong)(ushort)(gate[6] | gate[7] << 8) << 16) | ((ulong)BinaryPrimitives.ReadUInt32LittleEndian(new ReadOnlySpan<byte>(gate + 8, 4)) << 32);
            Require(address == f.R.CodeAddress + (ulong)(16 * vector) && gate[2] == 8 && gate[3] == 0 &&
                gate[4] == (vector == 8 ? 1 : vector == 14 ? 2 : vector == 2 ? 3 : 0) && gate[5] == 0x8e &&
                BinaryPrimitives.ReadUInt32LittleEndian(new ReadOnlySpan<byte>(gate + 12, 4)) == 0, "independent all-present gates");
            byte* slot = f.Code + 16 * vector;
            bool hardware = vector is 8 or 10 or 11 or 12 or 13 or 14 or 17 or 21 or 30;
            int jump = hardware ? 7 : 9;
            Require(slot[0] == 0x6a && slot[1] == (hardware ? 1 : 0) &&
                (hardware || (slot[2] == 0x6a && slot[3] == 0)) && slot[jump] == 0xe9, "normalizer policy");
            int v = BinaryPrimitives.ReadInt32LittleEndian(new ReadOnlySpan<byte>(slot + (hardware ? 3 : 5), 4));
            int rel = BinaryPrimitives.ReadInt32LittleEndian(new ReadOnlySpan<byte>(slot + jump + 1, 4));
            Require(v == vector && (long)f.R.CodeAddress + 16 * vector + jump + 5 + rel == (long)f.R.CodeAddress + 4096, "normalizer full vector/target");
        }
        Require(Leaf.ValidateStreamReadBack(f.Code, 8192, f.Data, 28672, f.R).BytesCompared == 36864, "whole pristine readback");
    }

    private static void Rejections()
    {
        using var f = new Fixture();
        byte[] code = new ReadOnlySpan<byte>(f.Code, 8192).ToArray(), data = new ReadOnlySpan<byte>(f.Data, 28672).ToArray();
        UnexpectedVectorPanicRequest[] requests = [default, Request(enabled: false), Request(c: 0), Request(c: 0x100000001),
            Request(d: 0), Request(d: 0x100001000), Request(image: 0), Request(bytes: ulong.MaxValue),
            Request(callback: 0x300010000), Request(callback: 0x2ffffffff), Request(cs: 0), Request(cs: 0x3b),
            Request(cs: 0x10038), Request(cr3: 0), Request(cr3: 0x1001), Request(c: 0x7ffffffff000),
            Request(image: 0x100000000, callback: 0x100000100), Request(d: ulong.MaxValue - 4095)];
        foreach (var r in requests)
            Require(!Leaf.PrepareContext(f.Code, 8192, f.Data, 28672, r).Succeeded, "request reject");
        foreach (ulong n in new ulong[] { 0, 8191, ulong.MaxValue, 0x800000000000 })
            Require(!Leaf.PrepareContext(f.Code, n, f.Data, 28672, f.R).Succeeded, "code full extent");
        foreach (ulong n in new ulong[] { 0, 24576, 28671, ulong.MaxValue, 0x800000000000 })
            Require(!Leaf.PrepareContext(f.Code, 8192, f.Data, n, f.R).Succeeded, "data full extent");
        Require(!Leaf.PrepareContext(f.Code + 1, 8192, f.Data, 28672, f.R).Succeeded &&
            !Leaf.PrepareContext(null, 8192, f.Data, 28672, f.R).Succeeded &&
            !Leaf.PrepareContext(f.Code, 8192, f.Code + 4096, 28672, f.R).Succeeded, "actual aliases/alignment");
        // Declared capacity, not just used C2 bytes, reaches the modeled data arena.
        Require(!Leaf.PrepareContext(f.Code, 16384, f.Data, 28672, Request(d: 0x100003000)).Succeeded, "modeled full-capacity alias");
        Require(code.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(f.Code, 8192)) && data.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(f.Data, 28672)), "all rejection unchanged");
        f.Context[24] = 1; byte before = f.Code[4096];
        Require(!Leaf.EmitStream(f.Code, 8192, f.Data, 28672, f.R).Succeeded && f.Code[4096] == before, "bad context prevents stream write");
        f.Context[24] = 0;
        Require(!Leaf.EmitStream(f.Code, 8192, f.Data, 28672, Request(callback: 0x300002000)).Succeeded, "callback substitution");
        StackExtentBoundaries();
    }

    private static void StackExtentBoundaries()
    {
        using var f = new Fixture();
        byte[] code = new ReadOnlySpan<byte>(f.Code, 8192).ToArray();
        // Includes the accessible overprovisioned tail, not just the required D7 bytes.
        byte[] data = new ReadOnlySpan<byte>(f.Data, 32768).ToArray();
        ResetUart();
        void Reject(UnexpectedVectorPanicRequest request, ulong capacity, UnexpectedVectorPanicStatus status)
        {
            var result = Leaf.PrepareContext(f.Code, 8192, f.Data, capacity, request);
            Require(result.Status == status && !result.BuffersMutated && result.BytesWritten == 0 &&
                _reads == 0 && _writes == 0, "exact pre-mutation status and no I/O");
            Require(code.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(f.Code, 8192)) &&
                data.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(f.Data, 32768)), "complete declared data tail unchanged");
        }
        Reject(f.R, 24576, UnexpectedVectorPanicStatus.InvalidStorage);
        Reject(f.R, 28671, UnexpectedVectorPanicStatus.InvalidStorage);
        Reject(Request(c: 0), 24576, UnexpectedVectorPanicStatus.InvalidRequest);
        Reject(Request(c: f.R.DataAddress + 24576), 24576, UnexpectedVectorPanicStatus.StorageOverlap);
        Reject(Request(c: f.R.DataAddress + 24576), 28672, UnexpectedVectorPanicStatus.StorageOverlap);
        Reject(Request(image: f.R.DataAddress + 24576, bytes: 4096, callback: f.R.DataAddress + 24592),
            28672, UnexpectedVectorPanicStatus.StorageOverlap);
        Reject(Request(c: f.R.DataAddress + 28672), 32768, UnexpectedVectorPanicStatus.StorageOverlap);
        Reject(Request(image: f.R.DataAddress + 28672, bytes: 4096, callback: f.R.DataAddress + 28688),
            32768, UnexpectedVectorPanicStatus.StorageOverlap);
        const ulong limit = 0x0000800000000000;
        Reject(Request(d: limit - 24576), 28672, UnexpectedVectorPanicStatus.InvalidRequest);
        Reject(Request(d: limit - 28672), 32768, UnexpectedVectorPanicStatus.InvalidStorage);
        Reject(f.R, ulong.MaxValue, UnexpectedVectorPanicStatus.InvalidStorage);
        foreach (var request in new[] { Request(c: f.R.DataAddress + 28672),
            Request(image: f.R.DataAddress + 28672, bytes: 4096, callback: f.R.DataAddress + 28688),
            Request(d: limit - 28672) })
        {
            // Half-open modeled extent admission is not authority to execute its stack top.
            using var admitted = new Fixture(request);
            Require(Leaf.ValidateStreamReadBack(admitted.Code, 8192, admitted.Data, 28672, request).BytesCompared == 36864,
                "exact adjacency and canonical extent edge use full D7 readback");
        }

        byte* arena = (byte*)NativeMemory.AlignedAlloc(49152, 4096);
        try
        {
            new Span<byte>(arena, 49152).Fill(0x5a);
            byte[] before = new ReadOnlySpan<byte>(arena, 49152).ToArray();
            byte* backing = arena + 4096;
            foreach (var boundary in new[] { (CodeOffset: 24576, DataCapacity: 28672UL),
                (CodeOffset: 28672, DataCapacity: 32768UL) })
            {
                var result = Leaf.PrepareContext(backing + boundary.CodeOffset, 8192, backing, boundary.DataCapacity, f.R);
                Require(result.Status == UnexpectedVectorPanicStatus.StorageOverlap && !result.BuffersMutated &&
                    before.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(arena, 49152)), "actual new-page/full-capacity alias unchanged");
            }
            Require(Leaf.PrepareContext(backing + 28672, 8192, backing, 28672, f.R).Succeeded &&
                Leaf.EmitStream(backing + 28672, 8192, backing, 28672, f.R).Succeeded &&
                Leaf.ValidateStreamReadBack(backing + 28672, 8192, backing, 28672, f.R).Succeeded,
                "actual exact required-span adjacency admitted");
            Require(new ReadOnlySpan<byte>(arena, 4096).IndexOfAnyExcept((byte)0x5a) < 0 &&
                new ReadOnlySpan<byte>(arena + 40960, 8192).IndexOfAnyExcept((byte)0x5a) < 0 &&
                _reads == 0 && _writes == 0, "actual adjacency keeps both outside canaries and performs no I/O");
        }
        finally { NativeMemory.AlignedFree(arena); }
    }

    private static void Corruption()
    {
        using var f = new Fixture();
        for (int i = 0; i < 8192; i++)
        {
            f.Code[i] ^= 1; var result = Leaf.ValidateStreamReadBack(f.Code, 8192, f.Data, 28672, f.R);
            Require(!result.Succeeded && result.FirstMismatchOffset == (i < 4096 ? i : i - 4096), "every code byte " + i);
            f.Code[i] ^= 1;
        }
        for (int i = 0; i < 28672; i++)
        {
            f.Data[i] ^= 1;
            Require(!Leaf.ValidateStreamReadBack(f.Code, 8192, f.Data, 28672, f.R).Succeeded, "every data byte " + i);
            f.Data[i] ^= 1;
        }
        Require(Leaf.ValidateStreamReadBack(f.Code, 8192, f.Data, 28672, f.R).Succeeded, "restored bytes");
    }

    private static void Routes()
    {
        byte* raw = stackalloc byte[192]; new Span<byte>(raw, 192).Clear();
        foreach (int vector in new[] { 0, 3, 32, 33, 127, 128, 255, 10, 13, 17, 21, 30 })
        {
            Q(raw, 120, (ulong)vector); bool hardware = vector is 10 or 13 or 17 or 21 or 30;
            Q(raw, 128, hardware ? 1UL : 0); Q(raw, 136, hardware && vector != 17 ? 0xffffUL : 0UL);
            Require(Leaf.DecodeCapture(raw, 184, 0xffff, 0x1000, out var fault) == UnexpectedVectorPanicStatus.Success && fault.Vector == vector, "normalizer fallback domain");
            Q(raw, 128, hardware ? 0UL : 1);
            Require(Leaf.DecodeCapture(raw, 184, 0, 0, out _) == UnexpectedVectorPanicStatus.InvalidCapture, "error policy");
        }
        foreach (int vector in new[] { 6, 8, 14 })
        {
            Q(raw, 120, (ulong)vector); Q(raw, 128, vector == 6 ? 0UL : 1); Q(raw, 136, 0);
            Require(Leaf.DecodeCapture(raw, 184, 0, 0, out _) == UnexpectedVectorPanicStatus.ControlledVector, "controlled fault is distinct");
        }
        Q(raw, 128, 0); Q(raw, 136, 0);
        foreach (ulong v in new ulong[] { 256, 0x100000020, ulong.MaxValue })
        { Q(raw, 120, v); Require(Leaf.DecodeCapture(raw, 184, 0, 0, out _) == UnexpectedVectorPanicStatus.InvalidCapture, "high vector"); }
        Q(raw, 120, 32); Q(raw, 128, 0x100000000);
        Require(Leaf.DecodeCapture(raw, 184, 0, 0, out _) == UnexpectedVectorPanicStatus.InvalidCapture, "high error flag");
        Q(raw, 128, 0); Q(raw, 136, 1);
        Require(Leaf.DecodeCapture(raw, 184, 0, 0, out _) == UnexpectedVectorPanicStatus.InvalidCapture, "nonzero synthetic error");
        Require(Leaf.DecodeCapture(raw, 183, 0, 0, out _) == UnexpectedVectorPanicStatus.InvalidCapture &&
            Leaf.DecodeCapture(raw + 1, 184, 0, 0, out _) == UnexpectedVectorPanicStatus.InvalidCapture &&
            Leaf.DecodeCapture(raw, ulong.MaxValue, 0, 0, out _) == UnexpectedVectorPanicStatus.InvalidCapture, "capture capacity/alignment/wrap");
    }

    private static void Reporting()
    {
        using var f = new Fixture();
        foreach (ulong selector in new ulong[] { 8, 0x38, 0xfff8 })
        {
            f.R = Request(cs: selector); f.Captured(selector);
            string expected = ExpectedMetadata(f) + ExpectedPanic(f);
            ResetUart(prefix: true);
            var result = Leaf.ReportCaptured(Backend(), f.Code, 8192, f.Data, 28672, f.R);
            FlushForObservation();
            Require(result.Succeeded && result.Panic.Succeeded && result.MetadataBytesWritten == 650 &&
                result.FirstDrainAttempts == 1 && result.SecondDrainAttempts == 1 && result.BuffersMutated &&
                result.BytesWritten == 650 + result.Panic.BytesFormatted, "complete modeled report");
            Require(Output() == "inherited-prefix\r\n" + expected && _configurations == 2 && _lostBytes == 0, "exact metadata + unchanged panic");
        }
        f.R = Request(); f.Captured();
        foreach (var old in new[] {
            (Context: false, Offset: 168, Value: f.R.DataAddress + 24576, Status: UnexpectedVectorPanicStatus.StateMismatch),
            (Context: true, Offset: 288, Value: f.R.DataAddress + 24392, Status: UnexpectedVectorPanicStatus.StateMismatch),
            (Context: true, Offset: 68, Value: f.R.DataAddress + 24576, Status: UnexpectedVectorPanicStatus.ContextMismatch) })
        {
            f.Captured(); Q(old.Context ? f.Context : f.Capture, old.Offset, old.Value);
            byte[] unchanged = new ReadOnlySpan<byte>(f.Data, 28672).ToArray();
            ResetUart(); var rejected = Leaf.ReportCaptured(Backend(), f.Code, 8192, f.Data, 28672, f.R);
            Require(rejected.Status == old.Status && !rejected.BuffersMutated && _reads == 0 && _writes == 0 &&
                unchanged.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(f.Data, 28672)), "old stack/capture binding rejects before report");
        }
        f.Captured();
        new ReadOnlySpan<byte>(f.Capture, 184).CopyTo(new Span<byte>(f.Data + 24392, 184));
        new Span<byte>(f.Capture, 184).Clear();
        ResetUart();
        Require(Leaf.ReportCaptured(Backend(), f.Code, 8192, f.Data, 28672, f.R).Status == UnexpectedVectorPanicStatus.StateMismatch &&
            _reads == 0 && _writes == 0, "plausible capture in old D6 location is not current capture authority");
        f.Captured();
        foreach (int offset in new[] { 120,128,136,144,152,160,168,176 })
        {
            ulong previous = U(f.Capture, offset); Q(f.Capture, offset, offset == 160 ? 0x246UL : previous ^ 1);
            Require(!Leaf.ValidateCapturedState(f.Code, 8192, f.Data, 28672, f.R, out _).Succeeded, "bad captured field " + offset);
            Q(f.Capture, offset, previous);
        }
        foreach (int offset in new[] { 2,10,12,20,37,45,53,100,108,116,176,178,192,194,224,256,264,288,304,312,320,328,336,344,352,361,364,368,376 })
        {
            byte old = f.Context[offset]; f.Context[offset] ^= offset is 37 or 45 or 361 ? (byte)2 : (byte)1;
            Require(!Leaf.ValidateCapturedState(f.Code, 8192, f.Data, 28672, f.R, out _).Succeeded, "bad current/context field " + offset);
            f.Context[offset] = old;
        }
        ResetUart(); var invalid = Leaf.ReportCaptured(default, f.Code, 8192, f.Data, 28672, f.R);
        Require(invalid.Status == UnexpectedVectorPanicStatus.BackendNotReady && _reads == 0 && _writes == 0, "invalid backend zero callbacks");
        byte[] before = new ReadOnlySpan<byte>(f.Data, 28672).ToArray();
        Require(!Leaf.ReportCaptured(Backend(), f.Code, 8192, f.Data, 4096 + 2048, f.R).Succeeded && _reads == 0 && _writes == 0 &&
            before.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(f.Data, 28672)), "short scratch extent no mutation or I/O");
        Q(f.Capture, 120, 0x100000020);
        Require(!Leaf.ReportCaptured(Backend(), f.Code, 8192, f.Data, 28672, f.R).Succeeded && _reads == 0 && _writes == 0, "pre-format reject no I/O");
    }

    // Independent FIFO/shift model: FCR clears pending FIFO only. Reads advance
    // transmission unless deliberately held. Byte/void callbacks return no failure status.
    private static readonly byte[] Sent = new byte[4096];
    private static int _sent, _pending = -1, _shift = -1, _reads, _writes, _configurations, _lostBytes;
    private static int _firstReady, _secondReady, _drainReads, _phase, _dataWrites, _transmitReady, _pollsForByte;
    private static bool _dlab, _badPort, _drainJustObserved;
    private static void ResetUart(bool prefix = false, int first = 1, int second = 1, int transmit = 1)
    {
        _sent = _reads = _writes = _configurations = _lostBytes = _drainReads = _phase = _dataWrites = _pollsForByte = 0;
        _pending = _shift = -1; _dlab = _badPort = _drainJustObserved = false;
        _firstReady = first; _secondReady = second; _transmitReady = transmit;
        if (prefix)
        {
            foreach (byte value in "inherited-prefix"u8) Sent[_sent++] = value;
            _shift = 13; _pending = 10;
        }
    }
    private static void FlushForObservation()
    {
        if (_shift >= 0) { Sent[_sent++] = (byte)_shift; _shift = -1; }
        if (_pending >= 0) { Sent[_sent++] = (byte)_pending; _pending = -1; }
    }
    private static string Output() => Encoding.ASCII.GetString(Sent, 0, _sent);
    [UnmanagedCallersOnly]
    private static byte In(ushort port)
    {
        _reads++; if (port != 0x3fd) _badPort = true;
        if (_phase == 3 && _starvePanic) return 0;
        if (_phase is 0 or 2)
        {
            _drainReads++;
            if (_drainReads < (_phase == 0 ? _firstReady : _secondReady))
            {
                // One queued byte enters the shifter. THRE is now set while TEMT is clear.
                if (_drainReads == 1 && _pending >= 0)
                {
                    if (_shift >= 0) Sent[_sent++] = (byte)_shift;
                    _shift = _pending; _pending = -1;
                }
                return _pending >= 0 ? (byte)0 : (byte)0x20;
            }
            FlushForObservation(); _drainJustObserved = true; return 0x60;
        }
        _pollsForByte++;
        if (_pollsForByte < _transmitReady) return 0;
        FlushForObservation(); return 0x60;
    }
    [UnmanagedCallersOnly]
    private static void Out(ushort port, byte value)
    {
        _writes++;
        if (port == 0x3f9 && value == 0 && !_dlab)
        {
            if (!_drainJustObserved || _pending != -1 || _shift != -1) _badPort = true;
            _drainJustObserved = false; _configurations++;
            _phase = _configurations == 1 ? 1 : 3;
        }
        if (port == 0x3fb) _dlab = (value & 0x80) != 0;
        if (port == 0x3fa && (value & 4) != 0)
        { if (_pending >= 0) { _lostBytes++; _pending = -1; } }
        if (port == 0x3f8 && !_dlab)
        {
            if (_pending >= 0) _lostBytes++;
            _pending = value; _pollsForByte = 0; _dataWrites++;
            if (_phase == 1 && _dataWrites == 650) { _phase = 2; _drainReads = 0; }
        }
    }
    private static Com1OwnedPortIoBackend Backend()
    {
        nuint input = (nuint)(delegate* unmanaged<ushort, byte>)&In;
        nuint output = (nuint)(delegate* unmanaged<ushort, byte, void>)&Out;
        nuint start = (input < output ? input : output) - 16;
        nuint end = (input > output ? input : output) + 16;
        var request = CheckedPortIoFunctionPointerBindingRequest.Create(true, 0x3f8, start, end, end - start,
            output - start, input - start, output, input, 7, 5, 7, 5);
        var backend = new Com1OwnedPortIoBackend(InvokeIn8Out8CheckedUnmanagedFunctionPointers.Bind(request));
        Require(backend.IsReady, "real checked binding API with controlled host callbacks"); return backend;
    }

    private static void Drains()
    {
        using var f = new Fixture(); f.Captured(); var backend = Backend();
        ResetUart(true);
        Require(backend.WriteByte(0x3fa, 0xc7).Succeeded, "controlled FIFO-reset observation");
        FlushForObservation();
        Require(_lostBytes == 1 && Output() == "inherited-prefix\r", "FCR clears FIFO but preserves shift register");
        foreach (int count in new[] { 1, 2, 7, 65536 })
        {
            ResetUart(true, count, count);
            var result = Leaf.ReportCaptured(backend, f.Code, 8192, f.Data, 28672, f.R);
            FlushForObservation();
            Require(result.Succeeded && result.FirstDrainAttempts == count && result.SecondDrainAttempts == count &&
                _reads == count * 2 + result.MetadataPolls + result.Panic.LineStatusPolls && !_badPort && _lostBytes == 0,
                "TEMT exact boundary and callback counts " + count);
            Require(Output() == "inherited-prefix\r\n" + ExpectedMetadata(f) + ExpectedPanic(f), "queued prefix and metadata preserved");
        }
        ResetUart(true, 65537, 1);
        var first = Leaf.ReportCaptured(backend, f.Code, 8192, f.Data, 28672, f.R);
        Require(first.Status == UnexpectedVectorPanicStatus.DrainTimeout && first.FirstDrainAttempts == 65536 &&
            first.SecondDrainAttempts == 0 && first.BuffersMutated && _reads == 65536 && _writes == 0 && !first.Panic.IsInitialized, "no read65537 or config on first drain timeout");
        ResetUart(true, 1, 65537);
        var second = Leaf.ReportCaptured(backend, f.Code, 8192, f.Data, 28672, f.R);
        Require(second.Status == UnexpectedVectorPanicStatus.DrainTimeout && second.FirstDrainAttempts == 1 &&
            second.SecondDrainAttempts == 65536 && _configurations == 1 && _dataWrites == 650 &&
            _reads == 65537 + 650 && !second.Panic.IsInitialized && !_badPort, "no reporter/reconfigure/read65537 on second timeout");
    }

    private static void TransmitLimits()
    {
        using var f = new Fixture(); f.Captured(); var backend = Backend();
        ResetUart(transmit: 128);
        var ready = Leaf.ReportCaptured(backend, f.Code, 8192, f.Data, 28672, f.R);
        Require(ready.Succeeded && ready.MetadataPolls == 650 * 128 && ready.Panic.LineStatusPolls == ready.Panic.BytesWritten * 128, "last THRE attempt");
        ResetUart(transmit: 129);
        var timeout = Leaf.ReportCaptured(backend, f.Code, 8192, f.Data, 28672, f.R);
        Require(timeout.Status == UnexpectedVectorPanicStatus.MetadataTimeout && timeout.MetadataPolls == 128 &&
            timeout.MetadataBytesWritten == 0 && _configurations == 1 && !timeout.Panic.IsInitialized, "bounded metadata failure no fallback");
        // A separate controlled reporter-only starvation after the completed metadata handoff.
        ResetUart(); _starvePanic = true;
        var panic = Leaf.ReportCaptured(backend, f.Code, 8192, f.Data, 28672, f.R);
        _starvePanic = false;
        Require(panic.Status == UnexpectedVectorPanicStatus.PanicRejected && panic.Panic.Status == PanicSafeOwnedCom1ReportStatus.TransmitReadyTimeout &&
            panic.Panic.LineStatusPolls == 128 && panic.Panic.BytesWritten == 0 && _configurations == 2, "unchanged reporter timeout distinct");
    }
    private static bool _starvePanic;

    private static void NoAllocation()
    {
        using var f = new Fixture(); f.Captured(); var backend = Backend();
        ResetUart(); Leaf.ReportCaptured(backend, f.Code, 8192, f.Data, 28672, f.R);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 32; i++)
        {
            ResetUart(); var result = Leaf.ReportCaptured(backend, f.Code, 8192, f.Data, 28672, f.R);
            if (!result.Succeeded) throw new InvalidOperationException("allocation loop report");
            var read = Leaf.ValidateCapturedState(f.Code, 8192, f.Data, 28672, f.R, out _);
            if (!read.Succeeded) throw new InvalidOperationException("allocation loop state");
        }
        Require(GC.GetAllocatedBytesForCurrentThread() == before, "report and state zero allocations");
        before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 32; i++)
        {
            Leaf.PrepareContext(f.Code, 8192, f.Data, 28672, f.R);
            Leaf.EmitStream(f.Code, 8192, f.Data, 28672, f.R);
            if (!Leaf.ValidateStreamReadBack(f.Code, 8192, f.Data, 28672, f.R).Succeeded) throw new InvalidOperationException("allocation readback");
        }
        Require(GC.GetAllocatedBytesForCurrentThread() == before, "prepare emit full readback zero allocations");
    }

    private static string ExpectedMetadata(Fixture f)
    {
        string[] names = ["c","d","context","callback","captureRsp","firmwareCs","cs","ss","ds","es","tr","gdtrBase","gdtrLimit","idtrBase","idtrLimit","tssAccess","gateLow","gateHigh","normalizer0","normalizer1","trigger","cr3","entryCr3","lar"];
        ulong[] values = [f.R.CodeAddress,f.R.DataAddress,f.R.DataAddress+4096,f.R.CallbackAddress,f.R.DataAddress+28488,
            f.R.FirmwareCs,8,16,16,16,24,f.R.DataAddress+4120,39,f.R.DataAddress,4095,0x8b,U(f.Data,512),U(f.Data,520),
            U(f.Code,512),U(f.Code,520),f.R.CodeAddress+5056,f.R.FirmwareCr3,f.R.FirmwareCr3,0xa09b00];
        var b = new StringBuilder("M02_UNEXPECTED_STATE");
        for (int i = 0; i < names.Length; i++) b.Append(' ').Append(names[i]).Append("=0x").Append(values[i].ToString("X16"));
        return b.Append("\r\n").ToString();
    }
    private static string ExpectedPanic(Fixture f)
    {
        byte* p = f.Capture;
        return $"OS_SMOKE_FAIL subsystem=idt reason=unexpected-vector vector=32 errorCodePresent=false errorCode=0x0000000000000000 rip=0x{U(p,144):X16} rsp=0x{U(p,168):X16} rbp=0x{U(p,64):X16} rflags=0x{U(p,160):X16} cr2=0x{U(f.Context,296):X16} cr3=0x{f.R.FirmwareCr3:X16} rax=0x{U(p,112):X16} rbx=0x{U(p,104):X16} rcx=0x{U(p,96):X16} rdx=0x{U(p,88):X16} rsi=0x{U(p,80):X16} rdi=0x{U(p,72):X16} r8=0x{U(p,56):X16} r9=0x{U(p,48):X16} r10=0x{U(p,40):X16} r11=0x{U(p,32):X16} r12=0x{U(p,24):X16} r13=0x{U(p,16):X16} r14=0x{U(p,8):X16} r15=0x{U(p,0):X16}\r\n";
    }

    private readonly record struct Instruction(int At, int End, string Op, int Register = 0, ulong Value = 0);
    private static Instruction Decode(byte* code, int at)
    {
        int p = at;
        byte B() => code[p++];
        uint D() { uint n = BinaryPrimitives.ReadUInt32LittleEndian(new ReadOnlySpan<byte>(code + p, 4)); p += 4; return n; }
        ulong Qword() { ulong n = U(code, p); p += 8; return n; }
        Instruction I(string op, int reg = 0, ulong value = 0) => new(at, p, op, reg, value);
        byte b = B();
        if (b == 0x90) return I("nop");
        if (b is >= 0x50 and <= 0x57) return I("push", b - 0x50);
        if (b == 0x41) { byte v = B(); Require(v is >= 0x50 and <= 0x57, "REX push"); return I("push", 8 + v - 0x50); }
        if (b == 0x6a) return I("push-immediate", value: unchecked((ulong)(long)(sbyte)B()));
        if (b == 0x68) return I("push-immediate", value: unchecked((ulong)(long)(int)D()));
        if (b is 0xe9 or 0xeb) { long delta = b == 0xe9 ? (int)D() : (sbyte)B(); return I("jump", value: (ulong)(p + delta)); }
        if (b == 0xfa) return I("cli");
        if (b == 0xfc) return I("cld");
        if (b == 0xf4) return I("halt");
        if (b == 0xcd) return I("interrupt", value: B());
        if (b == 0x31) { Require(B() == 0xc0, "xor eax"); return I("zero-eax"); }
        if (b == 0x3d) return I("cmp-eax", value: D());
        if (b == 0x25) return I("and-eax", value: D());
        if (b == 0xa9) return I("test-eax", value: D());
        if (b == 0xff) { Require(B() == 0xd0, "only indirect CALL RAX admitted"); return I("call-rax"); }
        if (b == 0x89) { Require(B() == 0x82, "store eax only"); return I("store-eax", value: D()); }
        if (b == 0x8e) return I("load-selector", value: B());
        if (b == 0x66)
        {
            byte next = B();
            if (next == 0xb8) { ulong v = B(); v |= (ulong)B() << 8; return I("mov-ax", value: v); }
            if (next == 0x8c) return I("read-selector", value: B());
            if (next == 0x89) { Require(B() == 0x42, "store ax disp8"); return I("store-ax", value: B()); }
            Require(next == 0x0f && B() == 0x00 && B() == 0xc8, "STR ax"); return I("str");
        }
        if (b is 0x48 or 0x49 or 0x4c)
        {
            byte next = B();
            if (next is >= 0xb8 and <= 0xbf) return I("mov64", next - 0xb8 + (b == 0x49 ? 8 : 0), Qword());
            if (next == 0x83) { Require(b == 0x48 && B() == 0xec, "sub rsp"); return I("sub-rsp", value: B()); }
            if (next == 0x39) { Require(b == 0x4c && B() == 0xc0, "cmp rax r8"); return I("cmp-r8"); }
            Require(next == 0x89 && b == 0x48, "REX mov only");
            byte mod = B();
            if (mod == 0xe1) return I("rsp-to-rcx");
            if (mod == 0xc8) return I("rcx-to-rax");
            Require(mod is 0x82 or 0x8a, "context store"); return I("store64", mod == 0x82 ? 0 : 1, D());
        }
        if (b == 0x0f)
        {
            byte next = B();
            if (next == 0x85) { int delta = (int)D(); return I("jne", value: (ulong)(p + delta)); }
            if (next == 0x20) return I("read-control", value: B());
            if (next == 0x02) { Require(B() == 0xc8, "LAR ecx eax"); return I("lar"); }
            if (next == 0x00) { Require(B() == 0xd8, "LTR ax"); return I("ltr"); }
            Require(next == 0x01, "descriptor instruction"); byte mod = B();
            if (mod == 0x12) return I("lgdt");
            if (mod == 0x42) return I("sgdt", value: B());
            if (mod == 0x8a) return I("sidt", value: D());
            Require(mod == 0x9a, "LIDT disp32"); return I("lidt", value: D());
        }
        throw new InvalidOperationException($"Unknown byte {b:X2} at {at}");
    }

    private static void Instructions()
    {
        using var f = new Fixture();
        var decoded = new List<Instruction>();
        for (int at = 4096; at < 8192;)
        {
            var instruction = Decode(f.Code, at); Require(instruction.End > at && instruction.End <= 8192, "decoded span");
            decoded.Add(instruction); at = instruction.End;
        }
        var starts = decoded.Select(i => i.At).ToHashSet();
        foreach (var i in decoded.Where(i => i.Op is "jump" or "jne"))
            Require(starts.Contains((int)i.Value), "every branch lands at an instruction");
        Require(decoded.Count(i => i.Op == "call-rax") == 1 && decoded.Count(i => i.Op == "interrupt") == 1 &&
            decoded.Single(i => i.Op == "interrupt").At == 5056 && decoded.Single(i => i.Op == "interrupt").Value == 32 &&
            decoded.Single(i => i.Op == "sub-rsp").Value == 40, "one exact CD20/scalar callback/ABI reserve");
        Require(decoded.Where(i => i.Op == "jne").All(i => i.Value == 5120), "all reject branches local terminal");
        Require(decoded.TakeWhile(i => i.Op != "rsp-to-rcx").Select(i => i.Register).SequenceEqual(new[] { 0,3,1,2,6,7,5,8,9,10,11,12,13,14,15 }), "all fifteen original registers saved before repurpose");
        foreach (ulong cs in new ulong[] { 8, 0x38, 0xfff8 })
        {
            f.R = Request(cs: cs); f.Prepare(); var model = new Machine(f); model.Run();
            Require(model.Called && model.Halted && model.CallbackDepth == 232 && model.CaptureDepth == 184 &&
                U(f.Context, 376) == 0x12345678, "complete byte/ABI model and reloaded volatile context");
            Q(f.Context, 376, 0);
            Require(Leaf.ValidateCapturedState(f.Code, 8192, f.Data, 28672, f.R, out _).Succeeded, "byte-model capture is admitted ordinary data");
        }
        foreach (int invalid in new[] { 1,2,3,4,5,6,7,8,9,10,11,12 })
        {
            f.R = Request(); f.Prepare(); var model = new Machine(f, invalid); model.Run();
            Require(model.Halted && !model.Called && !model.DescriptorsLoaded, "pre-mutation reject " + invalid);
        }
        string directory = Path.Combine(FindRoot(), "artifacts", "issue56", "bytes", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory); f.Prepare();
        File.WriteAllBytes(Path.Combine(directory, "code.bin"), new ReadOnlySpan<byte>(f.Code, 8192).ToArray());
        File.WriteAllBytes(Path.Combine(directory, "prepared-data.bin"), new ReadOnlySpan<byte>(f.Data, 28672).ToArray());
        File.WriteAllText(Path.Combine(directory, "instructions.json"), System.Text.Json.JsonSerializer.Serialize(decoded));
        Console.WriteLine($"M02_UNEXPECTED_HOST_BYTES stream=4096 used=1028 metadata=650 capture=184 calleeDepth=232 instructions={decoded.Count(i => i.Op != "nop")} noPrivilegedExecution=true");
    }

    private sealed class Machine
    {
        private readonly Fixture _f;
        private readonly ulong[] _r = Enumerable.Range(0,16).Select(i => 0x7654321000000000UL + (ulong)i).ToArray();
        private readonly ulong _cr0, _cr4, _cr3;
        private readonly bool _badLar, _badLarValue;
        private ushort _cs, _ss = 0x30, _ds = 0x30, _es = 0x30, _tr;
        private ulong _gdt, _idt;
        private bool _zero, _df = true, _if = true;
        public bool Called, Halted, DescriptorsLoaded;
        public int CaptureDepth, CallbackDepth;
        public Machine(Fixture f, int invalid = 0)
        {
            _f=f; _cs=(ushort)(invalid==1 ? 0x40 : f.R.FirmwareCs); _badLar=invalid==2; _badLarValue=invalid==12;
            _cr0=invalid==3 ? 0x80000005UL : invalid==11 ? 0x80000009UL : invalid==8 ? 1UL : 0x80000011UL;
            _cr4=invalid==4 ? 0x800600UL : invalid==5 ? 0x20600UL : invalid==6 ? 0x600UL|0x1000UL : invalid==9 ? 0x200UL : invalid==10 ? 0x400UL : 0x600UL;
            _cr3=invalid==7 ? f.R.FirmwareCr3+0x1000 : f.R.FirmwareCr3;
        }
        private byte* Memory(ulong address, int count)
        {
            Require(address >= _f.R.DataAddress && address - _f.R.DataAddress <= 28672UL - (ulong)count, "modeled store within original D7");
            return _f.Data + (int)(address - _f.R.DataAddress);
        }
        private void Push(ulong value) { _r[4] -= 8; Q(Memory(_r[4],8),0,value); }
        public void Run()
        {
            int pc=4608; int budget=512;
            while (!Halted && budget-- > 0)
            {
                Instruction i=Decode(_f.Code,pc); pc=i.End;
                switch(i.Op)
                {
                    case "nop": break;
                    case "mov64": _r[i.Register]=i.Value; break;
                    case "zero-eax": _r[0]=0; _zero=true; break;
                    case "mov-ax": _r[0]=(_r[0]&~65535UL)|i.Value; break;
                    case "read-selector": _r[0]=(_r[0]&~65535UL)|(i.Value switch {0xc8=>_cs,0xd0=>_ss,0xd8=>_ds,0xc0=>_es,_=>throw new InvalidOperationException("selector")}); break;
                    case "load-selector":
                        Require(_r[0]==16 && DescriptorsLoaded, "owned data selector");
                        if(i.Value==0xd8)_ds=16; else if(i.Value==0xc0)_es=16; else if(i.Value==0xd0)_ss=16; else throw new InvalidOperationException("load selector");
                        _f.Context[45]|=1; break;
                    case "cmp-eax": _zero=(uint)_r[0]==(uint)i.Value; break;
                    case "and-eax": _r[0]=(uint)_r[0]&(uint)i.Value; _zero=_r[0]==0; break;
                    case "test-eax": _zero=((uint)_r[0]&(uint)i.Value)==0; break;
                    case "cmp-r8": _zero=_r[0]==_r[8]; break;
                    case "jne": if(!_zero)pc=(int)i.Value; break;
                    case "jump": pc=(int)i.Value; break;
                    case "lar": _zero=!_badLar; if(_zero)_r[1]=_badLarValue ? 0xc09b00UL : 0xa09b00UL; break;
                    case "read-control": _r[0]=i.Value switch {0xc0=>_cr0,0xe0=>_cr4,0xd8=>_cr3,0xd0=>0xfedcba9876543210,_=>throw new InvalidOperationException("control")}; break;
                    case "store64": Q(Memory(_r[2]+i.Value,8),0,_r[i.Register]); break;
                    case "store-eax": BinaryPrimitives.WriteUInt32LittleEndian(new Span<byte>(Memory(_r[2]+i.Value,4),4),(uint)_r[0]); break;
                    case "store-ax": BinaryPrimitives.WriteUInt16LittleEndian(new Span<byte>(Memory(_r[2]+i.Value,2),2),(ushort)_r[0]); break;
                    case "rcx-to-rax": _r[0]=_r[1]; break;
                    case "rsp-to-rcx": _r[1]=_r[4]; CaptureDepth=(int)(_f.R.DataAddress+28672-_r[4]); break;
                    case "push": Push(_r[i.Register]); break;
                    case "push-immediate": Push(i.Value); break;
                    case "cli": _if=false; break;
                    case "cld": _df=false; break;
                    case "sub-rsp": _r[4]-=i.Value; break;
                    case "lgdt": Require(!_if && _r[4]==_f.R.DataAddress+28672,"CLI and new stack precede LGDT"); _gdt=U(Memory(_r[2],10),2); DescriptorsLoaded=true; break;
                    case "lidt": _idt=U(Memory(_r[2]+i.Value,10),2); break;
                    case "ltr": Require(_r[0]==24,"LTR selector");_tr=24;_f.Context[53]=0x8b;break;
                    case "str": _r[0]=_tr;break;
                    case "sgdt": Memory(_r[2]+i.Value,10)[0]=39; Q(Memory(_r[2]+i.Value,10),2,_gdt);break;
                    case "sidt": Memory(_r[2]+i.Value,10)[0]=255;Memory(_r[2]+i.Value,10)[1]=15;Q(Memory(_r[2]+i.Value,10),2,_idt);break;
                    case "interrupt":
                        Require(i.Value==32 && _idt==_f.R.DataAddress && _ss==16 && (_r[4]&15)==0,"INT32 owned route");
                        ulong[] original=(ulong[])_r.Clone(); ulong top=_r[4];
                        Push(_ss);Push(top);Push(2|(_if?0x200UL:0)|(_df?0x400UL:0));Push(_cs);Push(_f.R.CodeAddress+(ulong)pc);
                        _cs=8;_f.Context[37]|=1;_if=false;pc=32*16;
                        // All saved registers are independently compared at the actual CALL below.
                        _interrupted=original;break;
                    case "call-rax":
                        Require(_r[0]==_f.R.CallbackAddress && _r[1]==_f.R.DataAddress+28488 && _r[2]==_f.R.DataAddress+4096 &&
                            !_df && !_if && (_r[4]&15)==0,"scalar callback register lifetime/alignment");
                        Push(_f.R.CodeAddress+(ulong)pc);CallbackDepth=(int)(_f.R.DataAddress+28672-_r[4]);
                        Require(CallbackDepth==232 && _r[4]==_f.R.DataAddress+28440 && _r[4]+8+32<=_r[1] && _r[1]-(_r[4]+8+32)==8,"return/home/padding/capture disjoint");
                        int[] registers=[15,14,13,12,11,10,9,8,5,7,6,2,1,3,0];
                        for(int n=0;n<15;n++)Require(U(_f.Capture,n*8)==_interrupted![registers[n]],"actual push register identity");
                        Require(Leaf.ValidateCapturedState(_f.Code,8192,_f.Data,28672,_f.R,out _).Succeeded,"model callback input valid");
                        Called=true;_r[4]+=8;_r[0]=0x12345678;_r[2]=0xdeadbeef;break;
                    case "halt": Halted=true;break;
                    default: throw new InvalidOperationException("unmodeled "+i.Op);
                }
            }
            Require(Halted && budget>0,"bounded model completion");
        }
        private ulong[]? _interrupted;
    }

    private static void CheckNoStdLibCompilation()
    {
        string root = FindRoot();
        string directory = Path.Combine(root, "artifacts", "issue56", "nostdlib", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        File.Copy(Path.Combine(root, "global.json"), Path.Combine(directory, "global.json"));
        string[] sources =
        [
            "src/OS/Runtime/MinimalRuntime.cs",
            "src/OS/Runtime/RuntimeImportAttribute.cs",
            "src/OS/Runtime/MethodImplAttribute.cs",
            "src/OS.Kernel/Diagnostics/PortIoOperationPlan.cs",
            "src/OS.Kernel/Diagnostics/PortIoStubPlan.cs",
            "src/OS.Kernel/Diagnostics/InvokeIn8Out8CheckedUnmanagedFunctionPointers.cs",
            "src/OS.Kernel/Diagnostics/COM1OwnedPortIOBackend.cs",
            "src/OS.Kernel/Diagnostics/PanicSafeFormattingSerialReportingOwnedCOM1.cs",
            "src/OS.Kernel/Diagnostics/OwnedGDTTSSGeneratedAuditedCSharpStubBytes.cs",
            "src/OS.Kernel/Diagnostics/NormalizeErrorCodeNonErrorCodeInterruptFrame.cs",
            "src/OS.Kernel/Diagnostics/PopulateAllN256IDTVectorsOwnedGatesNever.cs",
            "src/OS.Kernel/Diagnostics/UnexpectedVectorPanicEvidenceDeterministicQEMUFailureClassification.cs"
        ];
        string includes = string.Join(Environment.NewLine, sources.Select(p =>
            "<Compile Include=\"" + System.Security.SecurityElement.Escape(Path.Combine(root, p)) + "\" />"));
        File.WriteAllText(Path.Combine(directory, "Compile.csproj"), $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net11.0</TargetFramework><LangVersion>14.0</LangVersion>
                <OutputType>Library</OutputType><AssemblyName>CLROS.M02.UnexpectedVector.NoStdLib</AssemblyName>
                <EnableDefaultCompileItems>false</EnableDefaultCompileItems><AllowUnsafeBlocks>true</AllowUnsafeBlocks>
                <NoStdLib>true</NoStdLib><DefineConstants>NOSTDLIB_EFI</DefineConstants><NoConfig>true</NoConfig>
                <DisableImplicitFrameworkReferences>true</DisableImplicitFrameworkReferences>
                <GenerateAssemblyInfo>false</GenerateAssemblyInfo><GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute>
                <ComputeNETCoreBuildOutputFiles>false</ComputeNETCoreBuildOutputFiles><RuntimeMetadataVersion>v4.0.30319</RuntimeMetadataVersion>
                <ProduceReferenceAssembly>false</ProduceReferenceAssembly><EnableNETAnalyzers>false</EnableNETAnalyzers>
                <RunAnalyzersDuringBuild>false</RunAnalyzersDuringBuild><TreatWarningsAsErrors>true</TreatWarningsAsErrors>
                <DebugType>none</DebugType><RestoreIgnoreFailedSources>true</RestoreIgnoreFailedSources>
                <UseSharedCompilation>false</UseSharedCompilation>
              </PropertyGroup>
              <ItemGroup>{{includes}}</ItemGroup>
              <Target Name="RemoveFrameworkReferences" BeforeTargets="BeforeCompile" AfterTargets="FindReferenceAssembliesForReferences">
                <ItemGroup><ReferencePathWithRefAssemblies Remove="@(ReferencePathWithRefAssemblies)" />
                  <ReferencePath Remove="@(ReferencePath)" /></ItemGroup>
              </Target>
            </Project>
            """, new UTF8Encoding(false));
        var identities = sources.Select(p => new { Path = p, Sha256 = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(root, p)))) });
        File.WriteAllText(Path.Combine(directory, "source-identities.json"),
            System.Text.Json.JsonSerializer.Serialize(identities), new UTF8Encoding(false));
        ProcessStartInfo start = new("dotnet")
        {
            WorkingDirectory = directory, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (string arg in new[] { "build", "Compile.csproj", "--nologo", "-m:1", "-c", "Release", "-nodeReuse:false" }) start.ArgumentList.Add(arg);
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("no-stdlib compile start");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync(); Task<string> stderr = process.StandardError.ReadToEndAsync();
        bool exited = process.WaitForExit(90000);
        if (!exited) { process.Kill(entireProcessTree: true); process.WaitForExit(); }
        Task.WaitAll(stdout, stderr);
        File.WriteAllText(Path.Combine(directory, "build.stdout.log"), stdout.Result);
        File.WriteAllText(Path.Combine(directory, "build.stderr.log"), stderr.Result);
        File.WriteAllText(Path.Combine(directory, "build.exitcode.txt"), process.ExitCode.ToString(System.Globalization.CultureInfo.InvariantCulture));
        File.WriteAllText(Path.Combine(directory, "command.json"), System.Text.Json.JsonSerializer.Serialize(new
        {
            Executable = start.FileName, Arguments = start.ArgumentList.ToArray(), NativeExit = process.ExitCode,
            TimedOut = !exited, SourceCount = sources.Length,
            StdoutSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(stdout.Result))),
            StderrSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(stderr.Result)))
        }));
        Require(exited && process.ExitCode == 0, "actual-source no-stdlib compile: " + directory + " " + stdout.Result + stderr.Result);
        using var stream = File.OpenRead(Path.Combine(directory, "bin", "Release", "net11.0", "CLROS.M02.UnexpectedVector.NoStdLib.dll"));
        using PEReader image = new(stream); MetadataReader metadata = image.GetMetadataReader();
        Require(metadata.AssemblyReferences.Count == 0, "actual source closure has zero assembly references");
        Require(metadata.MethodDefinitions.Select(metadata.GetMethodDefinition).Any(m => metadata.GetString(m.Name) == "EmitStream" && m.RelativeVirtualAddress != 0),
            "actual emitter method is present");
        Console.WriteLine("M02_UNEXPECTED_HOST_NOSTDLIB inputs=12 assemblyReferences=0");
    }
    private static string FindRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "CLROS.slnx"))) return current.FullName;
            current = current.Parent;
        }
        throw new InvalidOperationException("CLROS root not found");
    }

}
