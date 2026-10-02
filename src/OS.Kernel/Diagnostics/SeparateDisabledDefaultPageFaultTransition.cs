namespace OS.Kernel.Diagnostics;

public enum SeparatePageFaultTransitionOperation : byte
{
    None, PrepareContext, EmitStream, ValidateStreamReadBack
}

public enum SeparatePageFaultTransitionStatus : byte
{
    Uninitialized, Success, InvalidRequest, InvalidMode, InvalidPat,
    InvalidSnapshot, InvalidStorage, StorageOverlap, ContextMismatch,
    StreamMismatch, LayoutOverflow
}

/// <summary>Supplied observations and image summary, not ownership or execution authority.</summary>
public readonly struct SeparatePageFaultTransitionRequest
{
    public SeparatePageFaultTransitionRequest(ulong code, ulong data, ulong tables,
        OS.Kernel.Memory.X64PagingModePreflightObservation observation, ulong pat,
        SeparatePageFaultPageTableResult image)
    {
        IsInitialized = true; Code = code; Data = data; Tables = tables;
        Observation = observation; Pat = pat; Image = image;
    }
    public bool IsInitialized { get; }
    public ulong Code { get; }
    public ulong Data { get; }
    public ulong Tables { get; }
    public OS.Kernel.Memory.X64PagingModePreflightObservation Observation { get; }
    public ulong Pat { get; }
    public SeparatePageFaultPageTableResult Image { get; }
}

public readonly struct SeparatePageFaultTransitionResult
{
    internal SeparatePageFaultTransitionResult(SeparatePageFaultTransitionOperation operation,
        SeparatePageFaultTransitionStatus status, int written, int compared, int mismatch,
        ulong contextFingerprint, ulong streamFingerprint)
    {
        Operation = operation; Status = status; BytesWritten = written;
        BytesCompared = compared; FirstMismatchOffset = mismatch;
        ContextFingerprint = contextFingerprint; StreamFingerprint = streamFingerprint;
    }
    public SeparatePageFaultTransitionOperation Operation { get; }
    public SeparatePageFaultTransitionStatus Status { get; }
    public int BytesWritten { get; }
    public int BytesCompared { get; }
    public int FirstMismatchOffset { get; }
    public ulong ContextFingerprint { get; }
    public ulong StreamFingerprint { get; }
    public bool Succeeded => Operation != SeparatePageFaultTransitionOperation.None &&
        Status == SeparatePageFaultTransitionStatus.Success && FirstMismatchOffset == -1;
    public bool ContextPrepared => Succeeded && Operation == SeparatePageFaultTransitionOperation.PrepareContext;
    public bool StreamEmitted => Succeeded && Operation == SeparatePageFaultTransitionOperation.EmitStream;
    public bool ReadBackValidated => Succeeded && Operation == SeparatePageFaultTransitionOperation.ValidateStreamReadBack;
    public bool GuestExecution => false;
    public bool HardwareStateCaptured => false;
    public bool FaultTriggered => false;
    public bool PostExitBootServices => false;
}

/// <summary>
/// Materializes ordinary data only. Never invokes generated instructions. The caller
/// owns accessible/stable backing spans, publication and lifetime. A fresh #510 Audit
/// of the exact derived six ranges is required by the future adapter: its snapshot
/// has no C/D identity or content fingerprint and cannot establish that association.
/// Entry expects RCX=D+4096; after mutation it has no return or external call path.
/// </summary>
public static unsafe class SeparateDisabledDefaultPageFaultTransition
{
    public const int StreamByteCount = 8192;
    public const int ContextByteCount = 4096;
    public const int CodePages = 3, DataPages = 10, TablePages = 43, MappedPages = 52;
    public const int CommonHandlerOffset = 0, EntryOffset = 768, WalkOffset = 2048;
    public const int SeedOffset = 2688, FaultOffset = 2841, FormatOffset = 2944;
    public const int SerialOffset = 3200, FailureOffset = 3328, HaltOffset = 3584;
    public const int ContextLiteralOffset = 3600, TranscriptOffset = 4096;
    public const int FieldTableOffset = 6160, HexDigitsOffset = 6400, FailureTextOffset = 6448;
    public const int TranscriptByteCount = 2053, NumericFieldCount = 57;
    public const int WalkRecordsOffset = 512, CapturedFrameOffset = 832, OutputOffset = 1152;
    public const int CapturedFrameByteCount = 192, MaximumStackBytes = 192;
    public const int SerialPollLimit = 65536;
    private const ulong LowEnd = 0x0000_8000_0000_0000UL;
    private const ulong Cr0Required = 0x80010001, Cr4Forbidden = 0x01821000;
    private const ulong SeedBase = 0x4D30323134000000UL;
    // Fixed ASCII schema as scalar words, following the accepted no-stdlib stub pattern.
    private const int TranscriptSchemaLength = 1084;
    private static ulong TranscriptWord(int index) => index switch     {
        0 => 0x4154535F54534554UL,
        1 => 0x302E32306D205452UL,
        2 => 0x5F32304D0A0D3431UL,
        3 => 0x5541465F45474150UL,
        4 => 0x554F59414C5F544CUL,
        5 => 0x403D65646F632054UL,
        6 => 0x20403D6174616420UL,
        7 => 0x403D73656C626174UL,
        8 => 0x6952746C75616620UL,
        9 => 0x65646F6320403D70UL,
        10 => 0x20333D7365676150UL,
        11 => 0x6567615061746164UL,
        12 => 0x6261742030313D73UL,
        13 => 0x3D7365676150656CUL,
        14 => 0x657070616D203334UL,
        15 => 0x353D736567615064UL,
        16 => 0x505F32304D0A0D32UL,
        17 => 0x4C5541465F454741UL,
        18 => 0x662045444F4D5F54UL,
        19 => 0x43657261776D7269UL,
        20 => 0x3D30726320403D73UL,
        21 => 0x61776D7269662040UL,
        22 => 0x20403D3372436572UL,
        23 => 0x726F666542347263UL,
        24 => 0x7265666520403D65UL,
        25 => 0x403D74617020403DUL,
        26 => 0x6163697379687020UL,
        27 => 0x0D403D737469426CUL,
        28 => 0x4741505F32304D0AUL,
        29 => 0x5F544C5541465F45UL,
        30 => 0x33726320544F4F52UL,
        31 => 0x636570786520403DUL,
        32 => 0x403D337243646574UL,
        33 => 0x6520403D34726320UL,
        34 => 0x4364657463657078UL,
        35 => 0x304D0A0D403D3472UL,
        36 => 0x465F454741505F32UL,
        37 => 0x5342415F544C5541UL,
        38 => 0x656C6F7220544E45UL,
        39 => 0x696C206F72657A3DUL,
        40 => 0x6C20403D7261656EUL,
        41 => 0x616552736C657665UL,
        42 => 0x346C6D7020403D64UL,
        43 => 0x7470647020403D65UL,
        44 => 0x3D65647020403D65UL,
        45 => 0x0D403D6574702040UL,
        46 => 0x4741505F32304D0AUL,
        47 => 0x5F544C5541465F45UL,
        48 => 0x7220544E45534241UL,
        49 => 0x6C2066643D656C6FUL,
        50 => 0x20403D7261656E69UL,
        51 => 0x6552736C6576656CUL,
        52 => 0x6C6D7020403D6461UL,
        53 => 0x70647020403D6534UL,
        54 => 0x65647020403D6574UL,
        55 => 0x403D65747020403DUL,
        56 => 0x41505F32304D0A0DUL,
        57 => 0x544C5541465F4547UL,
        58 => 0x20544E455342415FUL,
        59 => 0x2066703D656C6F72UL,
        60 => 0x403D7261656E696CUL,
        61 => 0x52736C6576656C20UL,
        62 => 0x6D7020403D646165UL,
        63 => 0x647020403D65346CUL,
        64 => 0x647020403D657470UL,
        65 => 0x3D65747020403D65UL,
        66 => 0x505F32304D0A0D40UL,
        67 => 0x4C5541465F454741UL,
        68 => 0x544E455342415F54UL,
        69 => 0x6D6E3D656C6F7220UL,
        70 => 0x7261656E696C2069UL,
        71 => 0x6C6576656C20403DUL,
        72 => 0x20403D6461655273UL,
        73 => 0x20403D65346C6D70UL,
        74 => 0x20403D6574706470UL,
        75 => 0x747020403D656470UL,
        76 => 0x32304D0A0D403D65UL,
        77 => 0x41465F454741505FUL,
        78 => 0x455342415F544C55UL,
        79 => 0x3D656C6F7220544EUL,
        80 => 0x697469736E617274UL,
        81 => 0x61656E696C206E6FUL,
        82 => 0x6576656C20403D72UL,
        83 => 0x403D64616552736CUL,
        84 => 0x403D65346C6D7020UL,
        85 => 0x403D657470647020UL,
        86 => 0x7020403D65647020UL,
        87 => 0x304D0A0D403D6574UL,
        88 => 0x465F454741505F32UL,
        89 => 0x4152465F544C5541UL,
        90 => 0x6F7463657620454DUL,
        91 => 0x4573616820403D72UL,
        92 => 0x65646F43726F7272UL,
        93 => 0x726F72726520403DUL,
        94 => 0x403D32726320403DUL,
        95 => 0x6320403D70697220UL,
        96 => 0x616C667220403D73UL,
        97 => 0x76617320403D7367UL,
        98 => 0x20403D7073526465UL,
        99 => 0x3D73536465766173UL,
        100 => 0x656C646E61682040UL,
        101 => 0x6820403D70735272UL,
        102 => 0x735372656C646E61UL,
        103 => 0x403D33726320403DUL,
        104 => 0x5F534F524C430A0DUL,
        105 => 0x5F3431305F32304DUL,
        106 => 0x6576205944414552UL,
        107 => 0x2034313D726F7463UL,
        108 => 0x6572617764726168UL,
        109 => 0x646F43726F727245UL,
        110 => 0x6520657572743D65UL,
        111 => 0x65646F43726F7272UL,
        112 => 0x303D32726320303DUL,
        113 => 0x72655A6567617020UL,
        114 => 0x3D746E657362416FUL,
        115 => 0x6175672065757274UL,
        116 => 0x4173656761506472UL,
        117 => 0x20343D746E657362UL,
        118 => 0x667220323D747369UL,
        119 => 0x776F20657572743DUL,
        120 => 0x743D33724364656EUL,
        121 => 0x6472616820657572UL,
        122 => 0x7461745365726177UL,
        123 => 0x6572757470614365UL,
        124 => 0x6620657572743D64UL,
        125 => 0x67697254746C7561UL,
        126 => 0x72743D6465726567UL,
        127 => 0x696D726574206575UL,
        128 => 0x3D746C61486C616EUL,
        129 => 0x736F702065757274UL,
        130 => 0x6F6F427469784574UL,
        131 => 0x6563697672655374UL,
        132 => 0x0D65736C61663D73UL,
        133 => 0x41505F545345540AUL,
        134 => 0x302E32306D205353UL,
        135 => 0x000000000A0D3431UL,
        _ => 0
    };

    public static SeparatePageFaultTransitionResult PrepareContext(byte* context, ulong capacity,
        SeparatePageFaultTransitionRequest request)
        => Process(SeparatePageFaultTransitionOperation.PrepareContext, null, 0, context, capacity, request);

    public static SeparatePageFaultTransitionResult EmitStream(byte* stream, ulong streamCapacity,
        byte* context, ulong contextCapacity, SeparatePageFaultTransitionRequest request)
        => Process(SeparatePageFaultTransitionOperation.EmitStream, stream, streamCapacity, context, contextCapacity, request);

    public static SeparatePageFaultTransitionResult ValidateStreamReadBack(byte* stream, ulong streamCapacity,
        byte* context, ulong contextCapacity, SeparatePageFaultTransitionRequest request)
        => Process(SeparatePageFaultTransitionOperation.ValidateStreamReadBack, stream, streamCapacity, context, contextCapacity, request);

    private static SeparatePageFaultTransitionResult Process(SeparatePageFaultTransitionOperation operation,
        byte* stream, ulong streamCapacity, byte* context, ulong contextCapacity,
        SeparatePageFaultTransitionRequest request)
    {
        SeparatePageFaultTransitionStatus status = Validate(request);
        if (status != SeparatePageFaultTransitionStatus.Success) return Fail(operation, status);
        if (!Storage(context, contextCapacity, ContextByteCount) ||
            (operation != SeparatePageFaultTransitionOperation.PrepareContext && !Storage(stream, streamCapacity, StreamByteCount)))
            return Fail(operation, SeparatePageFaultTransitionStatus.InvalidStorage);
        if (operation != SeparatePageFaultTransitionOperation.PrepareContext &&
            Overlap((ulong)stream, streamCapacity, (ulong)context, contextCapacity))
            return Fail(operation, SeparatePageFaultTransitionStatus.StorageOverlap);

        // Always measure every section before touching caller storage.
        Writer contextCount = new(null, 0);
        BuildContext(ref contextCount, request);
        Writer streamCount = new(null, 0);
        BuildStream(ref streamCount, request);
        if (!contextCount.Valid || !streamCount.Valid || contextCount.Position != ContextByteCount || streamCount.Position != StreamByteCount)
            return new(operation, SeparatePageFaultTransitionStatus.LayoutOverflow, 0, 0,
                !contextCount.Valid ? contextCount.Position : streamCount.Mismatch, 0, 0);
        if (operation == SeparatePageFaultTransitionOperation.PrepareContext)
        {
            Writer output = new(context, 1);
            BuildContext(ref output, request);
            return new(operation, SeparatePageFaultTransitionStatus.Success, output.Position, 0, -1, output.Hash, 0);
        }
        Writer contextReader = new(context, 2);
        BuildContext(ref contextReader, request);
        if (contextReader.Mismatch >= 0)
            return new(operation, SeparatePageFaultTransitionStatus.ContextMismatch, 0, ContextByteCount,
                contextReader.Mismatch, contextReader.Hash, 0);
        Writer streamIo = new(stream, operation == SeparatePageFaultTransitionOperation.EmitStream ? (byte)1 : (byte)2);
        BuildStream(ref streamIo, request);
        return new(operation, streamIo.Mismatch >= 0 ? SeparatePageFaultTransitionStatus.StreamMismatch : SeparatePageFaultTransitionStatus.Success,
            operation == SeparatePageFaultTransitionOperation.EmitStream ? StreamByteCount : 0,
            operation == SeparatePageFaultTransitionOperation.ValidateStreamReadBack ? StreamByteCount : 0,
            streamIo.Mismatch, contextReader.Hash, streamIo.Hash);
    }

    private static SeparatePageFaultTransitionResult Fail(SeparatePageFaultTransitionOperation operation, SeparatePageFaultTransitionStatus status)
        => new(operation, status, 0, 0, -1, 0, 0);

    private static SeparatePageFaultTransitionStatus Validate(SeparatePageFaultTransitionRequest request)
    {
        var mode = request.Observation;
        if (!request.IsInitialized) return SeparatePageFaultTransitionStatus.InvalidRequest;
        if (!mode.IsInitialized || mode.CpuidMaximumExtendedLeaf < 0x80000008 ||
            mode.PhysicalAddressWidth < 36 || mode.PhysicalAddressWidth > 52 ||
            (mode.Cr0 & Cr0Required) != Cr0Required || (mode.RawCr4 & 0x20) == 0 ||
            (mode.RawCr4 & Cr4Forbidden) != 0 || (mode.Ia32Efer & 0xd00) != 0xd00 ||
            (mode.Rflags & 0x502) != 2)
            return SeparatePageFaultTransitionStatus.InvalidMode;
        ulong limit = 1UL << mode.PhysicalAddressWidth;
        if (!Range(request.Code, 3 * 4096, limit) || !Range(request.Data, 10 * 4096, limit) ||
            !Range(request.Tables, 43 * 4096, limit) ||
            Overlap(request.Code, 3 * 4096, request.Data, 10 * 4096) ||
            Overlap(request.Code, 3 * 4096, request.Tables, 43 * 4096) ||
            Overlap(request.Data, 10 * 4096, request.Tables, 43 * 4096))
            return SeparatePageFaultTransitionStatus.InvalidRequest;
        if (OS.Kernel.Memory.X64PatMappingToken.TryCreate(request.Pat, 0, 255, 255, out var pat) != OS.Kernel.Memory.X64PageTableEntryStatus.Success)
            return SeparatePageFaultTransitionStatus.InvalidPat;
        var image = request.Image;
        var preconditions = image.Request.Preconditions;
        if (!image.Succeeded || image.RangeCount != 6 || image.MappedPages != 52 || image.Request.ArenaPageCount != 43 ||
            image.UsedTablePages < 4 || image.UsedTablePages > 43 ||
            image.Request.ArenaStart.TryGetRawValueForHardwareRegisterBoundary(out ulong root) != OS.Boot.AddressValueStatus.Success ||
            root != request.Tables || preconditions.PhysicalAddressWidth != mode.PhysicalAddressWidth ||
            !preconditions.NxeEnabled || !preconditions.WriteProtectEnabled || preconditions.FiveLevelPagingEnabled ||
            preconditions.PatMapping.RawIa32Pat != pat.RawIa32Pat || preconditions.PatMapping.WriteBackIndex != 0)
            return SeparatePageFaultTransitionStatus.InvalidSnapshot;
        return SeparatePageFaultTransitionStatus.Success;
    }

    private static bool Range(ulong address, ulong bytes, ulong physicalLimit)
        => address != 0 && (address & 4095) == 0 && address < LowEnd &&
            bytes < LowEnd - address && address < physicalLimit && bytes < physicalLimit - address;
    private static bool Storage(byte* pointer, ulong capacity, ulong required)
        => pointer != null && ((ulong)pointer & 7) == 0 && capacity == required &&
            (ulong)pointer < LowEnd && capacity < LowEnd - (ulong)pointer;
    private static bool Overlap(ulong a, ulong an, ulong b, ulong bn) => a < b + bn && b < a + an;

    private static void BuildContext(ref Writer writer, SeparatePageFaultTransitionRequest request)
    {
        DescriptorScratch scratch = default;
        byte* bytes = (byte*)(((ulong)&scratch + 15) & ~15UL);
        var prepared = OwnedGDTTSSGeneratedAuditedCSharpStubBytes.PrepareContext(bytes, request.Data + 4096, 168);
        if (!prepared.ContextPrepared) { writer.Valid = false; return; }
        Write64(bytes + 100, request.Data + 0x4000);
        Write64(bytes + 108, request.Data + 0x6000);
        Write64(bytes + 116, request.Data + 0x8000);
        for (int i = 0; i < 168; i++) writer.Byte(bytes[i]);
        writer.Pad(176, 0); writer.Word(4095); writer.Qword(request.Data);
        writer.Pad(256, 0);
        writer.Qword(request.Code); writer.Qword(request.Data); writer.Qword(request.Tables);
        writer.Qword(request.Code + 4096 + FaultOffset);
        writer.Qword(request.Observation.Cr0); writer.Qword(request.Observation.RawCr3);
        writer.Qword(request.Observation.RawCr4); writer.Qword(request.Observation.Ia32Efer);
        writer.Qword(request.Pat); writer.Qword(request.Observation.PhysicalAddressWidth);
        writer.Qword(((1UL << request.Observation.PhysicalAddressWidth) - 1) & ~4095UL);
        writer.Qword(request.Observation.RawCr4 & ~0x80UL);
        // Observations, frame, walk records, lifecycle and output start zero.
        writer.Pad(ContextByteCount, 0);
    }

    private struct DescriptorScratch { public fixed ulong Words[23]; }

    private static void Write64(byte* at, ulong value)
    {
        for (int i = 0; i < 8; i++) at[i] = (byte)(value >> (i * 8));
    }

    private struct Writer
    {
        private readonly byte* _pointer;
        private readonly byte _mode;
        public int Position, Mismatch;
        public ulong Hash;
        public bool Valid;
        public Writer(byte* pointer, byte mode)
        { _pointer = pointer; _mode = mode; Position = 0; Mismatch = -1; Hash = 14695981039346656037; Valid = true; }
        public void Byte(byte value)
        {
            if (_mode == 1) _pointer[Position] = value;
            byte actual = _mode == 2 ? _pointer[Position] : value;
            if (actual != value && Mismatch < 0) Mismatch = Position;
            Hash = (Hash ^ actual) * 1099511628211;
            Position++;
        }
        public void Bytes(byte a, byte b) { Byte(a); Byte(b); }
        public void Bytes(byte a, byte b, byte c) { Bytes(a, b); Byte(c); }
        public void Bytes(byte a, byte b, byte c, byte d) { Bytes(a, b, c); Byte(d); }
        public void Word(int value) { Byte((byte)value); Byte((byte)(value >> 8)); }
        public void Dword(uint value) { for (int i = 0; i < 4; i++) Byte((byte)(value >> (i * 8))); }
        public void Qword(ulong value) { for (int i = 0; i < 8; i++) Byte((byte)(value >> (i * 8))); }
        public void Pad(int target, byte fill = 0xf4)
        { if (Position > target) { Valid = false; if (Mismatch < 0) Mismatch = Position; } while (Position < target) Byte(fill); }
        public void Relative(int target) => Dword((uint)(target - Position - 4));
        public void Jump(int target) { Byte(0xe9); Relative(target); }
        public void Branch(byte condition, int target) { Bytes(0x0f, condition); Relative(target); }
        public void MovAbs(int register, ulong value) { Bytes((byte)(0x48 | (register >> 3)), (byte)(0xb8 | (register & 7))); Qword(value); }
        public void Load(int offset) { Bytes(0x49, 0x8b, 0x87); Dword((uint)offset); }
        public void Store(int offset) { Bytes(0x49, 0x89, 0x87); Dword((uint)offset); }
        public void Compare(int offset, int failure)
        { Bytes(0x49, 0x39, 0x87); Dword((uint)offset); Branch(0x85, failure); }
        public void Immediate(int offset, uint value, int failure)
        { Bytes(0x49, 0x81, 0xbf); Dword((uint)offset); Dword(value); Branch(0x85, failure); }
        public void Lea(int register, int target)
        { Bytes((byte)(0x48 | ((register >> 3) << 2)), 0x8d, (byte)(0x05 | ((register & 7) << 3))); Relative(target); }
    }

    // Instruction emission is below. Every direct target is a fixed local section.
    private static void BuildStream(ref Writer w, SeparatePageFaultTransitionRequest r)
    {
        EmitHandler(ref w, r);
        w.Pad(EntryOffset); EmitEntry(ref w, r);
        w.Pad(WalkOffset); EmitWalk(ref w, r);
        w.Pad(SeedOffset);
        for (int register = 0; register < 16; register++)
            if (register != 4) w.MovAbs(register, register == 0 ? 0 : SeedBase + (ulong)register);
        w.Bytes(0x6a, 0x46, 0x9d);
        if (w.Position != FaultOffset) w.Valid = false;
        w.Bytes(0x48, 0x8b, 0x10);
        w.Jump(FailureOffset + 5 * 32);
        w.Pad(FormatOffset); EmitFormat(ref w, r);
        w.Pad(SerialOffset); EmitSerial(ref w);
        w.Pad(FailureOffset);
        int textOffset = FailureTextOffset;
        for (int reason = 0; reason < 7; reason++)
        {
            w.Pad(FailureOffset + reason * 32);
            w.Lea(6, textOffset); w.Byte(0xb9); w.Dword((uint)FailureLength(reason));
            w.Jump(SerialOffset); textOffset += FailureLength(reason);
        }
        w.Pad(HaltOffset); w.Bytes(0xfa, 0xf4); w.Jump(HaltOffset);
        w.Pad(ContextLiteralOffset); w.Qword(r.Data + 4096);
        w.Pad(TranscriptOffset); EmitTranscript(ref w, false);
        if (w.Position != TranscriptOffset + TranscriptByteCount) w.Valid = false;
        w.Pad(FieldTableOffset); EmitTranscript(ref w, true);
        if (w.Position != FieldTableOffset + NumericFieldCount * 4) w.Valid = false;
        w.Pad(HexDigitsOffset); w.Qword(0x3736353433323130); w.Qword(0x4645444342413938);
        w.Pad(FailureTextOffset);
        for (int i = 0; i < FailureTextByteCount; i++) w.Byte((byte)(FailureWord(i / 8) >> (8 * (i % 8))));
        w.Pad(StreamByteCount);
    }

    private const int FailureTextByteCount = 514;
    private static int FailureLength(int reason) => reason switch
    {
        0 => 74,
        1 => 77,
        2 => 71,
        3 => 74,
        4 => 72,
        5 => 79,
        6 => 67,
        _ => 0
    };
    private static ulong FailureWord(int index) => index switch     {
        0 => 0x454741505F32304DUL,
        1 => 0x435F544C5541465FUL,
        2 => 0x524F5252455F3252UL,
        3 => 0x41465F45444F435FUL,
        4 => 0x45540A0D44454C49UL,
        5 => 0x204C4941465F5453UL,
        6 => 0x203431302E32306DUL,
        7 => 0x6867696C66657270UL,
        8 => 0x64656C6961665F74UL,
        9 => 0x41505F32304D0A0DUL,
        10 => 0x544C5541465F4547UL,
        11 => 0x5252455F3252435FUL,
        12 => 0x5F45444F435F524FUL,
        13 => 0x0A0D44454C494146UL,
        14 => 0x4941465F54534554UL,
        15 => 0x31302E32306D204CUL,
        16 => 0x6972637365642034UL,
        17 => 0x73696D5F726F7470UL,
        18 => 0x4D0A0D686374616DUL,
        19 => 0x5F454741505F3230UL,
        20 => 0x52435F544C554146UL,
        21 => 0x5F524F5252455F32UL,
        22 => 0x4941465F45444F43UL,
        23 => 0x5345540A0D44454CUL,
        24 => 0x6D204C4941465F54UL,
        25 => 0x72203431302E3230UL,
        26 => 0x6D73696D5F746F6FUL,
        27 => 0x304D0A0D68637461UL,
        28 => 0x465F454741505F32UL,
        29 => 0x3252435F544C5541UL,
        30 => 0x435F524F5252455FUL,
        31 => 0x4C4941465F45444FUL,
        32 => 0x545345540A0D4445UL,
        33 => 0x306D204C4941465FUL,
        34 => 0x6261203431302E32UL,
        35 => 0x696D5F65636E6573UL,
        36 => 0x0A0D686374616D73UL,
        37 => 0x454741505F32304DUL,
        38 => 0x435F544C5541465FUL,
        39 => 0x524F5252455F3252UL,
        40 => 0x41465F45444F435FUL,
        41 => 0x45540A0D44454C49UL,
        42 => 0x204C4941465F5453UL,
        43 => 0x203431302E32306DUL,
        44 => 0x696D5F656D617266UL,
        45 => 0x0A0D686374616D73UL,
        46 => 0x454741505F32304DUL,
        47 => 0x435F544C5541465FUL,
        48 => 0x524F5252455F3252UL,
        49 => 0x41465F45444F435FUL,
        50 => 0x45540A0D44454C49UL,
        51 => 0x204C4941465F5453UL,
        52 => 0x203431302E32306DUL,
        53 => 0x7463657078656E75UL,
        54 => 0x6C706D6F635F6465UL,
        55 => 0x4D0A0D6E6F697465UL,
        56 => 0x5F454741505F3230UL,
        57 => 0x52435F544C554146UL,
        58 => 0x5F524F5252455F32UL,
        59 => 0x4941465F45444F43UL,
        60 => 0x5345540A0D44454CUL,
        61 => 0x6D204C4941465F54UL,
        62 => 0x72203431302E3230UL,
        63 => 0x64657265746E6565UL,
        64 => 0x0000000000000A0DUL,
        _ => 0
    };

    private static void EmitTranscript(ref Writer w, bool table)
    {
        int position = 0, field = 0;
        for (int i = 0; i < TranscriptSchemaLength; i++)
        {
            byte literal = (byte)(TranscriptWord(i / 8) >> (8 * (i % 8)));
            if (literal != (byte)'@')
            { if (!table) w.Byte(literal); position++; continue; }
            if (table) { w.Word(FieldSource(field)); w.Word(position + 2); }
            else { w.Bytes(0x30, 0x78); for (int digit = 0; digit < 16; digit++) w.Byte(0x30); }
            field++; position += 18;
        }
        if (field != NumericFieldCount || position != TranscriptByteCount) w.Valid = false;
    }

    private static void EmitHandler(ref Writer w, SeparatePageFaultTransitionRequest r)
    {
        int failure = FailureOffset + 4 * 32;
        // Original RAX and CR2 precede every context access, followed by all other GPRs.
        w.Byte(0x50); w.Bytes(0x0f, 0x20, 0xd0); w.Byte(0x50);
        for (int register = 1; register < 16; register++)
            if (register != 4) { if (register >= 8) w.Byte(0x41); w.Byte((byte)(0x50 | (register & 7))); }
        w.Bytes(0x4c, 0x8b, 0x3d); w.Relative(ContextLiteralOffset);
        w.Bytes(0x48, 0x89, 0xe0); w.Store(1024); // handler RSP
        w.Bytes(0x31, 0xc0); w.Bytes(0x66, 0x8c, 0xd0); w.Store(1032);
        w.Bytes(0x0f, 0x20, 0xd8); w.Store(1040);
        w.Bytes(0x48, 0x89, 0xe6); w.MovAbs(7, r.Data + 4096 + CapturedFrameOffset);
        w.Byte(0xb9); w.Dword(24); w.Byte(0xfc); w.Bytes(0xf3, 0x48, 0xa5);
        // Capture is complete before any comparison or lifecycle failure.
        w.Immediate(440, 0, FailureOffset + 6 * 32);
        w.MovAbs(0, 1); w.Store(440);
        w.Immediate(432, 1, failure);
        w.Bytes(0x31, 0xc0); w.Bytes(0x66, 0x8c, 0xc8);
        w.Bytes(0x48, 0x83, 0xf8, 8); w.Branch(0x85, failure);
        w.Immediate(960, 14, failure); w.Immediate(968, 1, failure);
        w.Immediate(976, 0, failure); w.Immediate(944, 0, failure);
        w.MovAbs(0, r.Code + 4096 + FaultOffset); w.Compare(984, failure);
        w.Load(352); w.Compare(992, failure);
        w.Immediate(1000, 0x10046, failure);
        w.MovAbs(0, r.Data + 0xa000); w.Compare(1008, failure);
        w.Immediate(1016, 0x10, failure);
        w.MovAbs(0, r.Data + 0x6000 - 192); w.Compare(1024, failure);
        w.Immediate(1032, 0, failure);
        w.MovAbs(0, r.Tables); w.Compare(1040, failure);
        w.Jump(FormatOffset);
    }

    private static void EmitEntry(ref Writer w, SeparatePageFaultTransitionRequest r)
    {
        int failure = FailureOffset;
        w.Bytes(0x4c, 0x8b, 0x3d); w.Relative(ContextLiteralOffset);
        w.Bytes(0x4c, 0x39, 0xf9); w.Branch(0x85, failure); // RCX is only entry argument.
        w.Immediate(432, 0, FailureOffset + 6 * 32);
        w.Bytes(0x9c, 0x58); w.Store(424);
        w.Bytes(0x48, 0x25); w.Dword(0x502);
        w.Bytes(0x48, 0x83, 0xf8, 2); w.Branch(0x85, failure);
        // Establish CPL0 before any privileged control-register or MSR query.
        w.Bytes(0x31, 0xc0); w.Bytes(0x66, 0x8c, 0xc8); w.Store(352);
        w.Bytes(0x48, 0xa9); w.Dword(7); w.Branch(0x85, failure);
        // LAR checks the current descriptor before replacing GDTR. Ignore undefined
        // 19:16 and accessed/AVL; require present DPL0 readable nonconforming L=1,D=0,G=1 code.
        w.Bytes(0x48, 0x0f, 0x02, 0xc0); w.Branch(0x85, failure);
        w.Bytes(0x48, 0x25); w.Dword(0x00e0fe00);
        w.Bytes(0x48, 0x3d); w.Dword(0x00a09a00); w.Branch(0x85, failure);
        w.Byte(0xb8); w.Dword(0x80000000); w.Bytes(0x0f, 0xa2);
        w.Byte(0x3d); w.Dword(0x80000008); w.Branch(0x82, failure);
        w.Byte(0xb8); w.Dword(0x80000008); w.Bytes(0x0f, 0xa2);
        w.Byte(0x25); w.Dword(255); w.Store(400); w.Compare(328, failure);
        w.Bytes(0x83, 0xf8, 36); w.Branch(0x82, failure);
        w.Bytes(0x83, 0xf8, 52); w.Branch(0x87, failure);
        w.Bytes(0x0f, 0x20, 0xc0); w.Store(360); w.Compare(288, failure);
        w.MovAbs(2, Cr0Required); w.Bytes(0x48, 0x21, 0xd0);
        w.Bytes(0x48, 0x39, 0xd0); w.Branch(0x85, failure);
        w.Bytes(0x0f, 0x20, 0xd8); w.Store(368); w.Compare(296, failure);
        w.Bytes(0x0f, 0x20, 0xe0); w.Store(376); w.Compare(304, failure);
        w.Bytes(0x48, 0xa9); w.Dword((uint)Cr4Forbidden); w.Branch(0x85, failure);
        w.Bytes(0x48, 0xa9); w.Dword(0x20); w.Branch(0x84, failure);
        EmitReadMsr(ref w, 0xc0000080); w.Store(384); w.Compare(312, failure);
        w.Bytes(0x48, 0x25); w.Dword(0xd00);
        w.Bytes(0x48, 0x3d); w.Dword(0xd00); w.Branch(0x85, failure);
        EmitReadMsr(ref w, 0x277); w.Store(392); w.MovAbs(2, r.Pat);
        w.Bytes(0x48, 0x39, 0xd0); w.Branch(0x85, failure);
        w.Byte(0xfa); w.MovAbs(0, 1); w.Store(432);
        failure = FailureOffset + 32;
        w.Bytes(0x41, 0x0f, 0x01, 0x17); // LGDT [R15]
        w.Bytes(0x41, 0x0f, 0x01, 0x47); w.Byte(10); // SGDT
        w.Load(0); w.Compare(10, failure);
        w.Bytes(0x41, 0x0f, 0xb7, 0x47); w.Byte(8);
        w.Bytes(0x41, 0x0f, 0xb7, 0x57); w.Byte(18);
        w.Bytes(0x39, 0xd0); w.Branch(0x85, failure);
        w.Bytes(0x66, 0xb8); w.Word(16);
        w.Bytes(0x8e, 0xd8); w.Bytes(0x8e, 0xc0); w.Bytes(0x8e, 0xd0);
        for (int segment = 0; segment < 3; segment++)
        {
            w.Bytes(0x66, 0x8c, segment == 0 ? (byte)0xd8 : segment == 1 ? (byte)0xc0 : (byte)0xd0);
            w.Bytes(0x66, 0x3d); w.Word(16); w.Branch(0x85, failure);
        }
        w.Bytes(0x66, 0xb8); w.Word(24); w.Bytes(0x0f, 0x00, 0xd8);
        w.Bytes(0x41, 0x0f, 0x00, 0x4f); w.Byte(20);
        w.Bytes(0x66, 0x41, 0x83, 0x7f); w.Bytes(20, 24); w.Branch(0x85, failure);
        w.Bytes(0x41, 0x80, 0x7f, 53); w.Byte(0x8b); w.Branch(0x85, failure);
        w.Bytes(0x41, 0x0f, 0x01, 0x9f); w.Dword(176);
        w.Bytes(0x41, 0x0f, 0x01, 0x8f); w.Dword(192);
        w.Load(176); w.Compare(192, failure);
        w.Bytes(0x41, 0x0f, 0xb7, 0x87); w.Dword(184);
        w.Bytes(0x41, 0x0f, 0xb7, 0x97); w.Dword(200);
        w.Bytes(0x39, 0xd0); w.Branch(0x85, failure);
        w.MovAbs(4, r.Data + 0xa000);
        failure = FailureOffset + 2 * 32;
        w.Bytes(0x0f, 0x20, 0xe0);
        w.Bytes(0x48, 0xa9); w.Dword(0x80);
        w.Branch(0x84, w.Position + 6 + 9); // No CR4 write when PGE is already clear.
        w.Bytes(0x48, 0x25); w.Dword(0xffffff7f); // sign-extended AND clears only PGE.
        w.Bytes(0x0f, 0x22, 0xe0);
        w.MovAbs(0, r.Tables); w.Bytes(0x0f, 0x22, 0xd8);
        w.Bytes(0x0f, 0x20, 0xd8); w.Store(408);
        w.MovAbs(2, r.Tables); w.Bytes(0x48, 0x39, 0xd0); w.Branch(0x85, failure);
        w.Bytes(0x0f, 0x20, 0xe0); w.Store(416);
        w.MovAbs(2, r.Observation.RawCr4 & ~0x80UL);
        w.Bytes(0x48, 0x39, 0xd0); w.Branch(0x85, failure);
        // Re-observe values which this transition must preserve.
        w.Bytes(0x0f, 0x20, 0xc0); w.Compare(360, failure);
        EmitReadMsr(ref w, 0xc0000080); w.Compare(384, failure);
        EmitReadMsr(ref w, 0x277); w.Compare(392, failure);
        w.Jump(WalkOffset);
    }

    private static void EmitReadMsr(ref Writer w, uint msr)
    {
        w.Byte(0xb9); w.Dword(msr); w.Bytes(0x0f, 0x32);
        w.Bytes(0x48, 0xc1, 0xe2, 32); w.Bytes(0x48, 0x09, 0xd0);
    }

    private static void EmitWalk(ref Writer w, SeparatePageFaultTransitionRequest r)
    {
        const int role = 2104, step = 2168, absent = 2528;
        int failure = FailureOffset + 3 * 32;
        w.MovAbs(14, r.Tables); w.MovAbs(13, r.Tables + 43 * 4096);
        w.MovAbs(11, r.Data + 4096 + 512); w.MovAbs(7, r.Data + 4096 + 832);
        w.MovAbs(10, 0); w.Jump(role);
        w.Pad(role);
        w.Bytes(0x4d, 0x89, 0x13); // [R11]=R10, actual walk linear address.
        w.MovAbs(12, 1); w.Bytes(0x4d, 0x89, 0xf1); // R9=R14
        w.Bytes(0x45, 0x31, 0xc0); w.Byte(0xb9); w.Dword(39);
        w.Jump(step);
        w.Pad(step);
        w.Bytes(0x4c, 0x89, 0xd0); w.Bytes(0x48, 0xd3, 0xe8);
        w.Byte(0x25); w.Dword(511);
        w.Bytes(0x49, 0x8b, 0x04, 0xc1); // RAX=[R9+RAX*8], target was checked before reaching this instruction.
        w.Bytes(0x4b, 0x89, 0x44, 0xc3); w.Byte(16);
        w.Bytes(0x49, 0xff, 0xc0); w.Bytes(0x4d, 0x89, 0x43, 8);
        w.Bytes(0x48, 0x85, 0xc0); w.Branch(0x84, absent);
        w.Bytes(0x49, 0x83, 0xf8, 4); w.Branch(0x84, failure); // Any present/nonzero final leaf fails.
        w.Bytes(0x48, 0x89, 0xc2); w.Bytes(0x83, 0xe2, 3);
        w.Bytes(0x83, 0xfa, 3); w.Branch(0x85, failure);
        w.Bytes(0x49, 0x8b, 0x9f); w.Dword(336); // RBX=physical mask
        w.Bytes(0x49, 0x89, 0xc1); w.Bytes(0x49, 0x21, 0xd9); // R9=entry & physical mask
        w.Bytes(0x48, 0x83, 0xcb, 0x23); w.Bytes(0x48, 0xf7, 0xd3);
        w.Bytes(0x48, 0x85, 0xd8); w.Branch(0x85, failure); // P/RW/A only, never ancestor D/NX/PS/foreign flags.
        w.Bytes(0x4d, 0x39, 0xf1); w.Branch(0x82, failure);
        w.Bytes(0x4d, 0x39, 0xe9); w.Branch(0x83, failure);
        w.Bytes(0x4c, 0x89, 0xca); w.Bytes(0x4c, 0x29, 0xf2);
        w.Bytes(0x48, 0xc1, 0xea, 12);
        w.Bytes(0x49, 0x0f, 0xab, 0xd4); w.Branch(0x82, failure); // BTS R12,RDX: reject repeated table.
        w.Bytes(0x83, 0xe9, 9); w.Jump(step);
        w.Pad(absent);
        w.Bytes(0x4d, 0x85, 0xd2);
        int nonzero = w.Position + 6 + 10 + 5;
        w.Branch(0x85, nonzero);
        w.MovAbs(10, r.Data); w.Jump(nonzero);
        w.Bytes(0x49, 0x81, 0xc2); w.Dword(0x2000);
        w.Bytes(0x49, 0x83, 0xc3, 64);
        w.Bytes(0x49, 0x39, 0xfb); w.Branch(0x82, role);
        w.Jump(SeedOffset);
    }

    private static void EmitFormat(ref Writer w, SeparatePageFaultTransitionRequest r)
    {
        const int field = 3016, digit = 3064;
        w.Lea(6, TranscriptOffset); w.MovAbs(7, r.Data + 4096 + OutputOffset);
        w.Bytes(0x49, 0x89, 0xfc); // R12=output base, survives copy.
        w.Byte(0xb9); w.Dword(TranscriptByteCount); w.Byte(0xfc); w.Bytes(0xf3, 0xa4);
        w.Lea(3, FieldTableOffset); w.Lea(5, HexDigitsOffset);
        w.Bytes(0x41, 0xb8); w.Dword(NumericFieldCount); w.Jump(field);
        w.Pad(field);
        w.Bytes(0x0f, 0xb7, 0x03); w.Bytes(0x49, 0x8b, 0x04, 0x07);
        w.Bytes(0x0f, 0xb7, 0x7b, 2); w.Bytes(0x4c, 0x01, 0xe7);
        w.Byte(0xb9); w.Dword(16); w.Jump(digit);
        w.Pad(digit);
        w.Bytes(0x48, 0x89, 0xc2); w.Bytes(0x48, 0xc1, 0xea, 60);
        w.Bytes(0x8a, 0x54, 0x15, 0); w.Bytes(0x88, 0x17);
        w.Bytes(0x48, 0xff, 0xc7); w.Bytes(0x48, 0xc1, 0xe0, 4);
        w.Bytes(0xff, 0xc9); w.Branch(0x85, digit);
        w.Bytes(0x48, 0x83, 0xc3, 4); w.Bytes(0x49, 0xff, 0xc8); w.Branch(0x85, field);
        w.Bytes(0x4c, 0x89, 0xe6); w.Byte(0xb9); w.Dword(TranscriptByteCount); w.Jump(SerialOffset);
    }

    private static void EmitSerial(ref Writer w)
    {
        const int poll = 3232, ready = 3264;
        w.Byte(0xfc); w.Bytes(0x66, 0xba); w.Word(0x3fd);
        w.Bytes(0x41, 0xb8); w.Dword(SerialPollLimit); w.Jump(poll);
        w.Pad(poll); w.Byte(0xec); w.Bytes(0xa8, 0x20); w.Branch(0x85, ready);
        w.Bytes(0x49, 0xff, 0xc8); w.Branch(0x85, poll); w.Jump(HaltOffset);
        w.Pad(ready); w.Bytes(0x66, 0xba); w.Word(0x3f8); w.Bytes(0xac, 0xee);
        w.Bytes(0x48, 0xff, 0xc9); w.Branch(0x85, SerialOffset); w.Jump(HaltOffset);
    }

    private static int FieldSource(int field)
    {
        if (field < 4) return 256 + 8 * field;
        if (field < 11) return 352 + 8 * (field - 4);
        if (field < 15) return field switch { 11 => 408, 12 => 272, 13 => 416, _ => 344 };
        if (field < 45) return 512 + ((field - 15) / 6) * 64 + ((field - 15) % 6) * 8;
        return field switch
        {
            45 => 960, 46 => 968, 47 => 976, 48 => 944, 49 => 984, 50 => 992,
            51 => 1000, 52 => 1008, 53 => 1016, 54 => 1024, 55 => 1032, _ => 1040
        };
    }
}
