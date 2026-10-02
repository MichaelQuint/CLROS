namespace OS.Kernel.Diagnostics;

public enum DoubleFaultIstOperation : byte
{
    None, PrepareContext, MaterializeArmedIdt, AuditArmedIdt, EmitStream, ValidateStreamReadBack, AuditSyntheticObservation
}

public enum DoubleFaultIstStatus : byte
{
    Uninitialized, Success, InvalidRequest, InvalidMode, InvalidPat,
    InvalidSnapshot, InvalidStorage, StorageOverlap, ContextMismatch,
    StreamMismatch, LayoutOverflow, IdtMismatch, SlotMismatch, ObservationMismatch
}

/// <summary>Supplied observations and image summary, not ownership or execution authority.</summary>
public readonly struct DoubleFaultIstRequest
{
    public DoubleFaultIstRequest(ulong code, ulong data, ulong tables,
        OS.Kernel.Memory.X64PagingModePreflightObservation observation, ulong pat,
        SeparatePageFaultPageTableResult image, bool enabledByDefault = false,
        bool executionRequested = false, bool executionAttempted = false, bool claimedGuestCapture = false)
    {
        IsInitialized = true; Code = code; Data = data; Tables = tables;
        Observation = observation; Pat = pat; Image = image;
        EnabledByDefault = enabledByDefault; ExecutionRequested = executionRequested;
        ExecutionAttempted = executionAttempted; ClaimedGuestCapture = claimedGuestCapture;
    }
    public bool EnabledByDefault { get; }
    public bool ExecutionRequested { get; }
    public bool ExecutionAttempted { get; }
    public bool ClaimedGuestCapture { get; }
    public bool IsInitialized { get; }
    public ulong Code { get; }
    public ulong Data { get; }
    public ulong Tables { get; }
    public OS.Kernel.Memory.X64PagingModePreflightObservation Observation { get; }
    public ulong Pat { get; }
    public SeparatePageFaultPageTableResult Image { get; }
}

public readonly struct DoubleFaultIstResult
{
    internal DoubleFaultIstResult(DoubleFaultIstOperation operation,
        DoubleFaultIstStatus status, int written, int compared, int mismatch,
        ulong contextFingerprint, ulong streamFingerprint, ulong tableFingerprint = 0, ulong slotFingerprint = 0)
    {
        Operation = operation; Status = status; BytesWritten = written;
        BytesCompared = compared; FirstMismatchOffset = mismatch;
        ContextFingerprint = contextFingerprint; StreamFingerprint = streamFingerprint;
        TableFingerprint = tableFingerprint; SlotFingerprint = slotFingerprint;
    }
    public DoubleFaultIstOperation Operation { get; }
    public DoubleFaultIstStatus Status { get; }
    public int BytesWritten { get; }
    /// <summary>Primary comparison extent; excludes prerequisite rechecks and repeated reads.</summary>
    public int BytesCompared { get; }
    public int FirstMismatchOffset { get; }
    public ulong TableFingerprint { get; }
    public ulong SlotFingerprint { get; }
    public ulong ContextFingerprint { get; }
    public ulong StreamFingerprint { get; }
    public bool Succeeded => Operation != DoubleFaultIstOperation.None &&
        Status == DoubleFaultIstStatus.Success && FirstMismatchOffset == -1;
    public bool ContextPrepared => Succeeded && Operation == DoubleFaultIstOperation.PrepareContext;
    public bool StreamEmitted => Succeeded && Operation == DoubleFaultIstOperation.EmitStream;
    public bool ReadBackValidated => Succeeded && Operation == DoubleFaultIstOperation.ValidateStreamReadBack;
    public bool GuestExecution => false;
    public bool HardwareStateCaptured => false;
    public bool FaultTriggered => false;
    public bool PostExitBootServices => false;
}

/// <summary>Four accessible, stable, disjoint caller buffers. No pointer is execution authority.</summary>
public readonly unsafe struct DoubleFaultIstBuffers
{
    public DoubleFaultIstBuffers(byte* context, ulong contextCapacity, byte* stream, ulong streamCapacity,
        byte* slots, ulong slotCapacity, byte* idt, ulong idtCapacity)
    { Context = context; ContextCapacity = contextCapacity; Stream = stream; StreamCapacity = streamCapacity;
      Slots = slots; SlotCapacity = slotCapacity; Idt = idt; IdtCapacity = idtCapacity; }
    public byte* Context { get; }
    public ulong ContextCapacity { get; }
    public byte* Stream { get; }
    public ulong StreamCapacity { get; }
    public byte* Slots { get; }
    public ulong SlotCapacity { get; }
    public byte* Idt { get; }
    public ulong IdtCapacity { get; }
}

/// <summary>Raw supplied walk, at most four reads. It does not authorize dereferencing any table.</summary>
public readonly struct DoubleFaultIstWalk
{
    public DoubleFaultIstWalk(ulong linear, ulong levelsRead, ulong pml4e, ulong pdpte, ulong pde, ulong pte)
    { Linear = linear; LevelsRead = levelsRead; Pml4e = pml4e; Pdpte = pdpte; Pde = pde; Pte = pte; }
    public ulong Linear { get; }
    public ulong LevelsRead { get; }
    public ulong Pml4e { get; }
    public ulong Pdpte { get; }
    public ulong Pde { get; }
    public ulong Pte { get; }
    internal ulong Entry(int level) => level == 0 ? Pml4e : level == 1 ? Pdpte : level == 2 ? Pde : Pte;
}

/// <summary>Synthetic current state is separate from undefined diagnostic saved program state.</summary>
public readonly struct DoubleFaultIstObservation
{
    public DoubleFaultIstObservation(CpuFaultFrame frame, ulong savedCs, ulong savedSs,
        ulong currentRsp, ulong currentCs, ulong currentSs, ulong gdtrBase, ulong gdtrLimit,
        ulong tr, ulong idtrBase, ulong idtrLimit, ulong tssAccess, ulong ist1,
        ulong dfGateLow, ulong dfGateHigh, ulong pfGateLow, ulong pfGateHigh,
        DoubleFaultIstWalk zero, DoubleFaultIstWalk df, DoubleFaultIstWalk pf,
        DoubleFaultIstWalk nmi, DoubleFaultIstWalk transition, bool claimedGuestCapture = false)
    {
        IsInitialized = true; Frame = frame; SavedCs = savedCs; SavedSs = savedSs;
        CurrentRsp = currentRsp; CurrentCs = currentCs; CurrentSs = currentSs;
        GdtrBase = gdtrBase; GdtrLimit = gdtrLimit; Tr = tr; IdtrBase = idtrBase; IdtrLimit = idtrLimit;
        TssAccess = tssAccess; Ist1 = ist1; DfGateLow = dfGateLow; DfGateHigh = dfGateHigh;
        PfGateLow = pfGateLow; PfGateHigh = pfGateHigh; Zero = zero; Df = df; Pf = pf; Nmi = nmi;
        Transition = transition; ClaimedGuestCapture = claimedGuestCapture;
    }
    public bool IsInitialized { get; }
    public bool ClaimedGuestCapture { get; }
    public CpuFaultFrame Frame { get; }
    public ulong SavedCs { get; }
    public ulong SavedSs { get; }
    public ulong CurrentRsp { get; }
    public ulong CurrentCs { get; }
    public ulong CurrentSs { get; }
    public ulong GdtrBase { get; }
    public ulong GdtrLimit { get; }
    public ulong Tr { get; }
    public ulong IdtrBase { get; }
    public ulong IdtrLimit { get; }
    public ulong TssAccess { get; }
    public ulong Ist1 { get; }
    public ulong DfGateLow { get; }
    public ulong DfGateHigh { get; }
    public ulong PfGateLow { get; }
    public ulong PfGateHigh { get; }
    public DoubleFaultIstWalk Zero { get; }
    public DoubleFaultIstWalk Df { get; }
    public DoubleFaultIstWalk Pf { get; }
    public DoubleFaultIstWalk Nmi { get; }
    public DoubleFaultIstWalk Transition { get; }
    internal DoubleFaultIstWalk Walk(int role) => role == 0 ? Zero : role == 1 ? Df : role == 2 ? Pf : role == 3 ? Nmi : Transition;
}

/// <summary>
/// Materializes ordinary data only. Never invokes generated instructions. The caller
/// owns accessible/stable backing spans, publication and lifetime. A fresh #510 Audit
/// of the exact derived six ranges is required by the future adapter: its snapshot
/// has no C/D identity or content fingerprint and cannot establish that association.
/// Adapted from the first-party #513 emitter; its frozen source is not modified.
/// Intel SDM 253668 Vol.3A 6.14.2/6.14.5 and Interrupt 8/11:
/// https://cdrdv2-public.intel.com/812386/253668-sdm-vol-3a.pdf
/// The eventual terminal caller must be CPL0; these bytes do not provide ring-3 recovery.
/// Entry expects RCX=D+4096; after mutation it has no return or external call path.
/// </summary>
public static unsafe class DoubleFaultISTPathSilentRebootOrTriple
{
    public const int StreamByteCount = 8192;
    public const int ContextByteCount = 4096;
    public const int CodePages = 3, DataPages = 10, TablePages = 43, MappedPages = 52;
    public const int CommonHandlerOffset = 0, EntryOffset = 768, WalkOffset = 2048;
    public const int TriggerOffset = 2688, FaultOffset = 2698, FormatOffset = 2944;
    public const int SerialOffset = 3200, FailureOffset = 3328, HaltOffset = 3584;
    public const int ContextLiteralOffset = 3600, TranscriptOffset = 4096;
    public const int FieldTableOffset = 7104, HexDigitsOffset = 7488, FailureTextOffset = 7520;
    public const int TranscriptByteCount = 2643, NumericFieldCount = 84;
    public const int WalkRecordsOffset = 512, CapturedFrameOffset = 832, OutputOffset = 1152;
    public const int CapturedFrameByteCount = 192, MaximumStackBytes = 192;
    public const int SerialPollLimit = 65536;
    private const ulong LowEnd = 0x0000_8000_0000_0000UL;
    private const ulong Cr0Required = 0x80010001, Cr4Forbidden = 0x01821000;

    // Fixed ASCII schema as scalar words, following the accepted no-stdlib stub pattern.
    private const int TranscriptSchemaLength = 1215;
    private static ulong TranscriptWord(int index) => index switch
    {
        0 => 0x4154535F54534554UL,
        1 => 0x302E32306D205452UL,
        2 => 0x5F32304D0A0D3531UL,
        3 => 0x554F59414C5F4644UL,
        4 => 0x403D65646F632054UL,
        5 => 0x20403D6174616420UL,
        6 => 0x403D73656C626174UL,
        7 => 0x6952746C75616620UL,
        8 => 0x65646F6320403D70UL,
        9 => 0x20333D7365676150UL,
        10 => 0x6567615061746164UL,
        11 => 0x6261742030313D73UL,
        12 => 0x3D7365676150656CUL,
        13 => 0x657070616D203334UL,
        14 => 0x353D736567615064UL,
        15 => 0x445F32304D0A0D32UL,
        16 => 0x662045444F4D5F46UL,
        17 => 0x43657261776D7269UL,
        18 => 0x3D30726320403D73UL,
        19 => 0x61776D7269662040UL,
        20 => 0x20403D3372436572UL,
        21 => 0x726F666542347263UL,
        22 => 0x7265666520403D65UL,
        23 => 0x403D74617020403DUL,
        24 => 0x6163697379687020UL,
        25 => 0x0D403D737469426CUL,
        26 => 0x5F46445F32304D0AUL,
        27 => 0x33726320544F4F52UL,
        28 => 0x636570786520403DUL,
        29 => 0x403D337243646574UL,
        30 => 0x6520403D34726320UL,
        31 => 0x4364657463657078UL,
        32 => 0x304D0A0D403D3472UL,
        33 => 0x5342415F46445F32UL,
        34 => 0x656C6F7220544E45UL,
        35 => 0x696C206F72657A3DUL,
        36 => 0x6C20403D7261656EUL,
        37 => 0x616552736C657665UL,
        38 => 0x346C6D7020403D64UL,
        39 => 0x7470647020403D65UL,
        40 => 0x3D65647020403D65UL,
        41 => 0x0D403D6574702040UL,
        42 => 0x5F46445F32304D0AUL,
        43 => 0x7220544E45534241UL,
        44 => 0x6C2066643D656C6FUL,
        45 => 0x20403D7261656E69UL,
        46 => 0x6552736C6576656CUL,
        47 => 0x6C6D7020403D6461UL,
        48 => 0x70647020403D6534UL,
        49 => 0x65647020403D6574UL,
        50 => 0x403D65747020403DUL,
        51 => 0x46445F32304D0A0DUL,
        52 => 0x20544E455342415FUL,
        53 => 0x2066703D656C6F72UL,
        54 => 0x403D7261656E696CUL,
        55 => 0x52736C6576656C20UL,
        56 => 0x6D7020403D646165UL,
        57 => 0x647020403D65346CUL,
        58 => 0x647020403D657470UL,
        59 => 0x3D65747020403D65UL,
        60 => 0x445F32304D0A0D40UL,
        61 => 0x544E455342415F46UL,
        62 => 0x6D6E3D656C6F7220UL,
        63 => 0x7261656E696C2069UL,
        64 => 0x6C6576656C20403DUL,
        65 => 0x20403D6461655273UL,
        66 => 0x20403D65346C6D70UL,
        67 => 0x20403D6574706470UL,
        68 => 0x747020403D656470UL,
        69 => 0x32304D0A0D403D65UL,
        70 => 0x455342415F46445FUL,
        71 => 0x3D656C6F7220544EUL,
        72 => 0x697469736E617274UL,
        73 => 0x61656E696C206E6FUL,
        74 => 0x6576656C20403D72UL,
        75 => 0x403D64616552736CUL,
        76 => 0x403D65346C6D7020UL,
        77 => 0x403D657470647020UL,
        78 => 0x7020403D65647020UL,
        79 => 0x304D0A0D403D6574UL,
        80 => 0x4152465F46445F32UL,
        81 => 0x6F7463657620454DUL,
        82 => 0x4573616820403D72UL,
        83 => 0x65646F43726F7272UL,
        84 => 0x726F72726520403DUL,
        85 => 0x403D32726320403DUL,
        86 => 0x6320403D70697220UL,
        87 => 0x616C667220403D73UL,
        88 => 0x76617320403D7367UL,
        89 => 0x20403D7073526465UL,
        90 => 0x3D73536465766173UL,
        91 => 0x656C646E61682040UL,
        92 => 0x6820403D70735272UL,
        93 => 0x735372656C646E61UL,
        94 => 0x403D33726320403DUL,
        95 => 0x72656C646E616820UL,
        96 => 0x304D0A0D403D7343UL,
        97 => 0x5345445F46445F32UL,
        98 => 0x53524F5450495243UL,
        99 => 0x6D694C7274646720UL,
        100 => 0x74646720403D7469UL,
        101 => 0x20403D6573614272UL,
        102 => 0x74646920403D7274UL,
        103 => 0x403D74696D694C72UL,
        104 => 0x7361427274646920UL,
        105 => 0x4173737420403D65UL,
        106 => 0x20403D7373656363UL,
        107 => 0x6420403D31747369UL,
        108 => 0x776F4C6574614766UL,
        109 => 0x746147666420403DUL,
        110 => 0x20403D6867694865UL,
        111 => 0x6F4C657461476670UL,
        112 => 0x6147667020403D77UL,
        113 => 0x403D686769486574UL,
        114 => 0x46445F32304D0A0DUL,
        115 => 0x617220535250475FUL,
        116 => 0x3D78627220403D78UL,
        117 => 0x20403D7863722040UL,
        118 => 0x627220403D786472UL,
        119 => 0x3D69737220403D70UL,
        120 => 0x20403D6964722040UL,
        121 => 0x3D397220403D3872UL,
        122 => 0x20403D3031722040UL,
        123 => 0x317220403D313172UL,
        124 => 0x3D33317220403D32UL,
        125 => 0x20403D3431722040UL,
        126 => 0x430A0D403D353172UL,
        127 => 0x32304D5F534F524CUL,
        128 => 0x4145525F3531305FUL,
        129 => 0x6F74636576205944UL,
        130 => 0x6472616820383D72UL,
        131 => 0x6F72724565726177UL,
        132 => 0x72743D65646F4372UL,
        133 => 0x726F727265206575UL,
        134 => 0x6920303D65646F43UL,
        135 => 0x67617020313D7473UL,
        136 => 0x7362416F72655A65UL,
        137 => 0x657572743D746E65UL,
        138 => 0x6150647261756720UL,
        139 => 0x6E65736241736567UL,
        140 => 0x6576617320343D74UL,
        141 => 0x643D657461745364UL,
        142 => 0x6974736F6E676169UL,
        143 => 0x657420796C6E4F63UL,
        144 => 0x61486C616E696D72UL,
        145 => 0x20657572743D746CUL,
        146 => 0x7469784574736F70UL,
        147 => 0x76726553746F6F42UL,
        148 => 0x6C61663D73656369UL,
        149 => 0x545345540A0D6573UL,
        150 => 0x306D20535341505FUL,
        151 => 0x000A0D3531302E32UL,
        _ => 0
    };

    public static DoubleFaultIstResult PrepareContext(DoubleFaultIstBuffers buffers, DoubleFaultIstRequest request)
        => Process(DoubleFaultIstOperation.PrepareContext, buffers, request);
    public static DoubleFaultIstResult MaterializeArmedIdt(DoubleFaultIstBuffers buffers, DoubleFaultIstRequest request)
        => Process(DoubleFaultIstOperation.MaterializeArmedIdt, buffers, request);
    public static DoubleFaultIstResult AuditArmedIdt(DoubleFaultIstBuffers buffers, DoubleFaultIstRequest request)
        => Process(DoubleFaultIstOperation.AuditArmedIdt, buffers, request);
    public static DoubleFaultIstResult EmitStream(DoubleFaultIstBuffers buffers, DoubleFaultIstRequest request)
        => Process(DoubleFaultIstOperation.EmitStream, buffers, request);
    public static DoubleFaultIstResult ValidateStreamReadBack(DoubleFaultIstBuffers buffers, DoubleFaultIstRequest request)
        => Process(DoubleFaultIstOperation.ValidateStreamReadBack, buffers, request);

    private static DoubleFaultIstResult Process(DoubleFaultIstOperation operation,
        DoubleFaultIstBuffers buffers, DoubleFaultIstRequest request)
    {
        DoubleFaultIstStatus status = Validate(request);
        if (status != DoubleFaultIstStatus.Success) return Fail(operation, status);
        for (int i = 0; i < 4; i++)
        {
            if (!Storage(Pointer(buffers, i), Capacity(buffers, i), (ulong)(i == 1 ? StreamByteCount : ContextByteCount)) ||
                (i >= 2 && ((ulong)Pointer(buffers, i) & 4095) != 0))
                return Fail(operation, DoubleFaultIstStatus.InvalidStorage);
        }
        DescriptorScratch scratch = default;
        byte* descriptor = (byte*)(((ulong)&scratch + 15) & ~15UL);
        for (int i = 0; i < 4; i++)
        {
            if (Overlap((ulong)Pointer(buffers, i), Capacity(buffers, i), (ulong)&scratch, (ulong)sizeof(DescriptorScratch)))
                return Fail(operation, DoubleFaultIstStatus.StorageOverlap);
            for (int j = i + 1; j < 4; j++)
                if (Overlap((ulong)Pointer(buffers, i), Capacity(buffers, i), (ulong)Pointer(buffers, j), Capacity(buffers, j)))
                    return Fail(operation, DoubleFaultIstStatus.StorageOverlap);
        }
        // Count the complete layout before any write to caller-owned storage.
        Writer contextCount = new(null, 0); BuildContext(ref contextCount, request, descriptor);
        Writer streamCount = new(null, 0); BuildStream(ref streamCount, request);
        if (!contextCount.Valid || !streamCount.Valid || contextCount.Position != ContextByteCount || streamCount.Position != StreamByteCount)
            return new(operation, DoubleFaultIstStatus.LayoutOverflow, 0, 0,
                !contextCount.Valid ? contextCount.Position : streamCount.Mismatch, 0, 0);
        if (operation == DoubleFaultIstOperation.PrepareContext)
        {
            Writer output = new(buffers.Context, 1); BuildContext(ref output, request, descriptor);
            return new(operation, DoubleFaultIstStatus.Success, output.Position, 0, -1, output.Hash, 0);
        }
        Writer contextReader = new(buffers.Context, 2); BuildContext(ref contextReader, request, descriptor);
        if (contextReader.Mismatch >= 0)
            return new(operation, DoubleFaultIstStatus.ContextMismatch, 0, ContextByteCount, contextReader.Mismatch, contextReader.Hash, 0);
        int written = 0;
        if (operation == DoubleFaultIstOperation.MaterializeArmedIdt)
        {
            var slots = NormalizeErrorCodeNonErrorCodeInterruptFrame.Materialize(buffers.Slots, 4096, request.Code, request.Code + 4096);
            written = slots.DestinationMutated ? 4096 : 0;
            if (!slots.Succeeded) return new(operation, DoubleFaultIstStatus.SlotMismatch, written, 0, -1, contextReader.Hash, 0);
            var allPresent = PopulateAllN256IDTVectorsOwnedGatesNever.Materialize(buffers.Idt, 4096, request.Data, request.Code);
            if (allPresent.DestinationMutated) written += 4096;
            if (!allPresent.Succeeded || !PopulateAllN256IDTVectorsOwnedGatesNever.ValidateReadBack(buffers.Idt, 4096, request.Data, request.Code).Succeeded)
                return new(operation, DoubleFaultIstStatus.IdtMismatch, written, 4096, -1, contextReader.Hash, 0);
            // The sole arm mutation: Present clear only for #PF. Never undo it for an audit.
            buffers.Idt[229] = 0x0e;
        }
        var slotRead = NormalizeErrorCodeNonErrorCodeInterruptFrame.ValidateReadBack(buffers.Slots, 4096, request.Code, request.Code + 4096);
        ulong slotHash = Fingerprint(buffers.Slots, 4096), idtHash = Fingerprint(buffers.Idt, 4096);
        if (!slotRead.Succeeded)
            return new(operation, DoubleFaultIstStatus.SlotMismatch, written, 4096, slotRead.FirstMismatchOffset, contextReader.Hash, 0, idtHash, slotHash);
        int mismatch = -1;
        for (int i = 0; i < 4096; i++)
            if (buffers.Idt[i] != GateByte(request.Code, i) && mismatch < 0) mismatch = i;
        if (mismatch >= 0)
            return new(operation, DoubleFaultIstStatus.IdtMismatch, written, 8192, mismatch, contextReader.Hash, 0, idtHash, slotHash);
        if (operation == DoubleFaultIstOperation.MaterializeArmedIdt || operation == DoubleFaultIstOperation.AuditArmedIdt)
            return new(operation, DoubleFaultIstStatus.Success, written, 8192, -1, contextReader.Hash, 0, idtHash, slotHash);
        Writer streamIo = new(buffers.Stream, operation == DoubleFaultIstOperation.EmitStream ? (byte)1 : (byte)2);
        BuildStream(ref streamIo, request);
        return new(operation, streamIo.Mismatch >= 0 ? DoubleFaultIstStatus.StreamMismatch : DoubleFaultIstStatus.Success,
            operation == DoubleFaultIstOperation.EmitStream ? StreamByteCount : 0,
            operation == DoubleFaultIstOperation.ValidateStreamReadBack ? StreamByteCount : 0,
            streamIo.Mismatch, contextReader.Hash, streamIo.Hash, idtHash, slotHash);
    }
    private static byte* Pointer(DoubleFaultIstBuffers b, int i) => i == 0 ? b.Context : i == 1 ? b.Stream : i == 2 ? b.Slots : b.Idt;
    private static ulong Capacity(DoubleFaultIstBuffers b, int i) => i == 0 ? b.ContextCapacity : i == 1 ? b.StreamCapacity : i == 2 ? b.SlotCapacity : b.IdtCapacity;
    private static ulong Fingerprint(byte* bytes, int count)
    { ulong hash = 14695981039346656037; for (int i = 0; i < count; i++) hash = (hash ^ bytes[i]) * 1099511628211; return hash; }
    private static byte GateByte(ulong code, int offset)
    {
        int vector = offset / 16, at = offset % 16; ulong target = code + (ulong)vector * 16;
        return at switch
        {
            0 => (byte)target, 1 => (byte)(target >> 8), 2 => 8,
            4 => vector == 8 ? (byte)1 : vector == 14 ? (byte)2 : vector == 2 ? (byte)3 : (byte)0,
            5 => vector == 14 ? (byte)0x0e : (byte)0x8e,
            6 => (byte)(target >> 16), 7 => (byte)(target >> 24), 8 => (byte)(target >> 32),
            9 => (byte)(target >> 40), 10 => (byte)(target >> 48), 11 => (byte)(target >> 56), _ => 0
        };
    }
    private static ulong GateWord(ulong code, int vector, bool high)
    { ulong value = 0; for (int i = 0; i < 8; i++) value |= (ulong)GateByte(code, vector * 16 + (high ? 8 : 0) + i) << (8 * i); return value; }
    private static DoubleFaultIstResult Fail(DoubleFaultIstOperation operation, DoubleFaultIstStatus status)
        => new(operation, status, 0, 0, -1, 0, 0);

    private static DoubleFaultIstStatus Validate(DoubleFaultIstRequest request)
    {
        var mode = request.Observation;
        if (!request.IsInitialized || request.EnabledByDefault || request.ExecutionRequested ||
            request.ExecutionAttempted || request.ClaimedGuestCapture) return DoubleFaultIstStatus.InvalidRequest;
        if (!mode.IsInitialized || mode.CpuidMaximumExtendedLeaf < 0x80000008 ||
            mode.PhysicalAddressWidth < 36 || mode.PhysicalAddressWidth > 52 ||
            (mode.Cr0 & Cr0Required) != Cr0Required || (mode.RawCr4 & 0x20) == 0 ||
            (mode.RawCr4 & Cr4Forbidden) != 0 || (mode.Ia32Efer & 0xd00) != 0xd00 ||
            (mode.Rflags & 0x502) != 2)
            return DoubleFaultIstStatus.InvalidMode;
        ulong limit = 1UL << mode.PhysicalAddressWidth;
        if ((mode.RawCr3 & ~0x18UL & 4095) != 0 || (mode.RawCr3 & ~4095UL) == 0 ||
            mode.RawCr3 >= limit) return DoubleFaultIstStatus.InvalidMode;
        if (!Range(request.Code, 3 * 4096, limit) || !Range(request.Data, 10 * 4096, limit) ||
            !Range(request.Tables, 43 * 4096, limit) ||
            Overlap(request.Code, 3 * 4096, request.Data, 10 * 4096) ||
            Overlap(request.Code, 3 * 4096, request.Tables, 43 * 4096) ||
            Overlap(request.Data, 10 * 4096, request.Tables, 43 * 4096))
            return DoubleFaultIstStatus.InvalidRequest;
        if (OS.Kernel.Memory.X64PatMappingToken.TryCreate(request.Pat, 0, 255, 255, out var pat) != OS.Kernel.Memory.X64PageTableEntryStatus.Success)
            return DoubleFaultIstStatus.InvalidPat;
        var image = request.Image;
        var preconditions = image.Request.Preconditions;
        if (!image.Succeeded || image.RangeCount != 6 || image.MappedPages != 52 || image.Request.ArenaPageCount != 43 ||
            image.UsedTablePages < 4 || image.UsedTablePages > 43 ||
            image.Request.ArenaStart.TryGetRawValueForHardwareRegisterBoundary(out ulong root) != OS.Boot.AddressValueStatus.Success ||
            root != request.Tables || preconditions.PhysicalAddressWidth != mode.PhysicalAddressWidth ||
            !preconditions.NxeEnabled || !preconditions.WriteProtectEnabled || preconditions.FiveLevelPagingEnabled ||
            preconditions.PatMapping.RawIa32Pat != pat.RawIa32Pat || preconditions.PatMapping.WriteBackIndex != 0)
            return DoubleFaultIstStatus.InvalidSnapshot;
        return DoubleFaultIstStatus.Success;
    }

    /// <summary>Validates supplied scalar observations only; never reads a table or saved stack pointer.</summary>
    public static DoubleFaultIstResult AuditSyntheticObservation(DoubleFaultIstRequest request, DoubleFaultIstObservation observation)
    {
        const DoubleFaultIstOperation operation = DoubleFaultIstOperation.AuditSyntheticObservation;
        var status = Validate(request);
        if (status != DoubleFaultIstStatus.Success) return Fail(operation, status);
        var frame = observation.Frame;
        ulong start = request.Data + 0x3000, end = request.Data + 0x4000;
        if (!observation.IsInitialized || observation.ClaimedGuestCapture || frame.Vector != 8 ||
            !frame.HasErrorCode || frame.ErrorCode != 0 || frame.Cr3 != request.Tables ||
            observation.CurrentRsp < start || observation.CurrentRsp > end - CapturedFrameByteCount ||
            (observation.CurrentRsp & 7) != 0 || observation.CurrentCs != 8 || observation.CurrentSs != 0 ||
            observation.GdtrBase != request.Data + 4120 || observation.GdtrLimit != 39 || observation.Tr != 24 ||
            observation.IdtrBase != request.Data || observation.IdtrLimit != 4095 || observation.TssAccess != 0x8b ||
            observation.Ist1 != end || observation.DfGateLow != GateWord(request.Code, 8, false) ||
            observation.DfGateHigh != GateWord(request.Code, 8, true) || observation.PfGateLow != GateWord(request.Code, 14, false) ||
            observation.PfGateHigh != GateWord(request.Code, 14, true) || !AuditWalks(request, observation))
            return Fail(operation, DoubleFaultIstStatus.ObservationMismatch);
        return new(operation, DoubleFaultIstStatus.Success, 0, 0, -1, 0, 0);
    }

    private static bool AuditWalks(DoubleFaultIstRequest request, DoubleFaultIstObservation observation)
    {
        ulong* parents = stackalloc ulong[20];
        ulong* slots = stackalloc ulong[20];
        ulong* entries = stackalloc ulong[20];
        int count = 0;
        ulong mask = ((1UL << request.Observation.PhysicalAddressWidth) - 1) & ~4095UL;
        for (int role = 0; role < 5; role++)
        {
            var walk = observation.Walk(role);
            ulong linear = role == 0 ? 0 : request.Data + (ulong)role * 8192;
            if (walk.Linear != linear || walk.LevelsRead < 1 || walk.LevelsRead > 4) return false;
            ulong table = request.Tables, visited = 1;
            for (int level = 0; level < 4; level++)
            {
                ulong entry = walk.Entry(level);
                if ((ulong)level >= walk.LevelsRead) { if (entry != 0) return false; continue; }
                ulong slot = (linear >> (39 - level * 9)) & 511;
                // Shared observations may differ only by the hardware accessed bit.
                for (int i = 0; i < count; i++)
                    if (parents[i] == table && slots[i] == slot && (entries[i] & ~0x20UL) != (entry & ~0x20UL)) return false;
                parents[count] = table; slots[count] = slot; entries[count] = entry; count++;
                if ((ulong)level + 1 == walk.LevelsRead)
                {
                    if (entry != 0) return false;
                    ulong size = 1UL << (39 - level * 9), absent = linear & ~(size - 1);
                    if (KnownMappingOverlap(request, absent, size)) return false;
                    continue;
                }
                if ((entry & 3) != 3 || (entry & ~(mask | 0x23UL)) != 0) return false;
                ulong child = entry & mask;
                if (child < request.Tables || child >= request.Tables + 43 * 4096) return false;
                ulong bit = 1UL << (int)((child - request.Tables) / 4096);
                if ((visited & bit) != 0) return false;
                visited |= bit;
                // No table page has two different incoming parent slots.
                for (int i = 0; i < count - 1; i++)
                    if (entries[i] != 0 && (entries[i] & mask) == child && (parents[i] != table || slots[i] != slot)) return false;
                table = child;
            }
        }
        return true;
    }
    private static bool KnownMappingOverlap(DoubleFaultIstRequest r, ulong start, ulong size)
    {
        if (Overlap(start, size, r.Code, 3 * 4096) || Overlap(start, size, r.Tables, 43 * 4096) ||
            Overlap(start, size, r.Data, 2 * 4096)) return true;
        for (ulong i = 0; i < 4; i++) if (Overlap(start, size, r.Data + (3 + i * 2) * 4096, 4096)) return true;
        return false;
    }


    private static bool Range(ulong address, ulong bytes, ulong physicalLimit)
        => address != 0 && (address & 4095) == 0 && address < LowEnd &&
            bytes < LowEnd - address && address < physicalLimit && bytes < physicalLimit - address;
    private static bool Storage(byte* pointer, ulong capacity, ulong required)
        => pointer != null && ((ulong)pointer & 7) == 0 && capacity == required &&
            (ulong)pointer < LowEnd && capacity < LowEnd - (ulong)pointer;
    private static bool Overlap(ulong a, ulong an, ulong b, ulong bn) => a < b + bn && b < a + an;

    private static void BuildContext(ref Writer writer, DoubleFaultIstRequest request, byte* bytes)
    {
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
    private static void BuildStream(ref Writer w, DoubleFaultIstRequest r)
    {
        EmitHandler(ref w, r);
        w.Pad(EntryOffset); EmitEntry(ref w, r);
        w.Pad(WalkOffset); EmitWalk(ref w, r);
        w.Pad(TriggerOffset);
        w.MovAbs(0, 0); // The only trigger is the actual null READ below.
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
        w.Pad(3616); EmitAbsenceConsistency(ref w, r);
        w.Pad(TranscriptOffset); EmitTranscript(ref w, false);
        if (w.Position != TranscriptOffset + TranscriptByteCount) w.Valid = false;
        w.Pad(FieldTableOffset); EmitTranscript(ref w, true);
        if (w.Position != FieldTableOffset + NumericFieldCount * 4) w.Valid = false;
        w.Pad(HexDigitsOffset); w.Qword(0x3736353433323130); w.Qword(0x4645444342413938);
        w.Pad(FailureTextOffset);
        for (int i = 0; i < FailureTextByteCount; i++) w.Byte((byte)(FailureWord(i / 8) >> (8 * (i % 8))));
        w.Pad(StreamByteCount);
    }

    private const int FailureTextByteCount = 423;
    private static int FailureLength(int reason) => reason switch
    {
        0 => 61,
        1 => 64,
        2 => 58,
        3 => 61,
        4 => 59,
        5 => 66,
        6 => 54,
        _ => 0
    };
    private static ulong FailureWord(int index) => index switch
    {
        0 => 0x42554F445F32304DUL,
        1 => 0x544C5541465F454CUL,
        2 => 0x0D44454C4941465FUL,
        3 => 0x41465F545345540AUL,
        4 => 0x302E32306D204C49UL,
        5 => 0x6C66657270203531UL,
        6 => 0x6961665F74686769UL,
        7 => 0x32304D0A0D64656CUL,
        8 => 0x5F454C42554F445FUL,
        9 => 0x41465F544C554146UL,
        10 => 0x45540A0D44454C49UL,
        11 => 0x204C4941465F5453UL,
        12 => 0x203531302E32306DUL,
        13 => 0x7470697263736564UL,
        14 => 0x616D73696D5F726FUL,
        15 => 0x32304D0A0D686374UL,
        16 => 0x5F454C42554F445FUL,
        17 => 0x41465F544C554146UL,
        18 => 0x45540A0D44454C49UL,
        19 => 0x204C4941465F5453UL,
        20 => 0x203531302E32306DUL,
        21 => 0x73696D5F746F6F72UL,
        22 => 0x4D0A0D686374616DUL,
        23 => 0x4C42554F445F3230UL,
        24 => 0x5F544C5541465F45UL,
        25 => 0x0A0D44454C494146UL,
        26 => 0x4941465F54534554UL,
        27 => 0x31302E32306D204CUL,
        28 => 0x636E657362612035UL,
        29 => 0x74616D73696D5F65UL,
        30 => 0x5F32304D0A0D6863UL,
        31 => 0x465F454C42554F44UL,
        32 => 0x4941465F544C5541UL,
        33 => 0x5345540A0D44454CUL,
        34 => 0x6D204C4941465F54UL,
        35 => 0x66203531302E3230UL,
        36 => 0x73696D5F656D6172UL,
        37 => 0x4D0A0D686374616DUL,
        38 => 0x4C42554F445F3230UL,
        39 => 0x5F544C5541465F45UL,
        40 => 0x0A0D44454C494146UL,
        41 => 0x4941465F54534554UL,
        42 => 0x31302E32306D204CUL,
        43 => 0x657078656E752035UL,
        44 => 0x6D6F635F64657463UL,
        45 => 0x0D6E6F6974656C70UL,
        46 => 0x554F445F32304D0AUL,
        47 => 0x4C5541465F454C42UL,
        48 => 0x44454C4941465F54UL,
        49 => 0x465F545345540A0DUL,
        50 => 0x2E32306D204C4941UL,
        51 => 0x6E65657220353130UL,
        52 => 0x000A0D6465726574UL,
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

    private static void EmitHandler(ref Writer w, DoubleFaultIstRequest r)
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
        w.Store(1048); // Current CS, never the undefined saved CS.
        w.Immediate(960, 8, failure); w.Immediate(968, 1, failure);
        w.Immediate(976, 0, failure);
        // Intel SDM Vol.3A Interrupt 8: saved program state is undefined.
        // Do not compare or dereference saved RIP/CS/RSP/RFLAGS/GPRs/CR2.
        w.MovAbs(0, r.Data + 0x4000 - CapturedFrameByteCount); w.Compare(1024, failure);
        w.Immediate(1032, 0, failure);
        w.MovAbs(0, r.Tables); w.Compare(1040, failure);
        EmitDescriptorObservations(ref w, r, failure);
        w.Jump(FormatOffset);
    }

    private static void EmitDescriptorObservations(ref Writer w, DoubleFaultIstRequest r, int failure)
    {
        // Fresh handler-side readbacks; the raw interrupted CS/SS/RIP are not used.
        w.Bytes(0x41, 0x0f, 0x01, 0x47); w.Byte(10);
        w.Bytes(0x41, 0x0f, 0x01, 0x8f); w.Dword(192);
        w.Bytes(0x41, 0x0f, 0x00, 0x4f); w.Byte(20);
        for (int i = 0; i < 7; i++)
        {
            int source = i == 0 ? 10 : i == 1 ? 12 : i == 2 ? 20 : i == 3 ? 192 : i == 4 ? 194 : i == 5 ? 53 : 100;
            if (i == 0 || i == 2 || i == 3)
            { w.Bytes(0x41, 0x0f, 0xb7, 0x87); w.Dword((uint)source); }
            else if (i == 5)
            { w.Bytes(0x31, 0xc0); w.Bytes(0x41, 0x8a, 0x87); w.Dword((uint)source); }
            else w.Load(source);
            w.Store(448 + 8 * i);
            ulong expected = i == 0 ? 39UL : i == 1 ? r.Data + 4120 : i == 2 ? 24UL : i == 3 ? 4095UL :
                i == 4 ? r.Data : i == 5 ? 0x8bUL : r.Data + 0x4000;
            w.MovAbs(2, expected); w.Bytes(0x48, 0x39, 0xd0); w.Branch(0x85, failure);
        }
        for (int i = 0; i < 4; i++)
        {
            int vector = i < 2 ? 8 : 14;
            w.MovAbs(2, r.Data); w.Bytes(0x48, 0x8b, 0x82); w.Dword((uint)(vector * 16 + (i % 2) * 8));
            w.Store(1056 + i * 8); w.MovAbs(2, GateWord(r.Code, vector, (i & 1) != 0));
            w.Bytes(0x48, 0x39, 0xd0); w.Branch(0x85, failure);
        }
    }

    private static void EmitEntry(ref Writer w, DoubleFaultIstRequest r)
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

    private static void EmitWalk(ref Writer w, DoubleFaultIstRequest r)
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
        w.Pad(absent); w.Jump(3616);
        w.Bytes(0x4d, 0x85, 0xd2);
        int nonzero = w.Position + 6 + 10 + 5;
        w.Branch(0x85, nonzero);
        w.MovAbs(10, r.Data); w.Jump(nonzero);
        w.Bytes(0x49, 0x81, 0xc2); w.Dword(0x2000);
        w.Bytes(0x49, 0x83, 0xc3, 64);
        w.Bytes(0x49, 0x39, 0xfb); w.Branch(0x82, role);
        w.Jump(TriggerOffset);
    }

    private static void EmitAbsenceConsistency(ref Writer w, DoubleFaultIstRequest r)
    {
        // RCX is the current walk shift. Prove the absent entry's entire virtual
        // interval cannot cover a page needed by this fixed active C/D/T layout.
        w.MovAbs(0, 1); w.Bytes(0x48, 0xd3, 0xe0); // size = 1 << CL
        w.Bytes(0x48, 0x89, 0xc3); w.Bytes(0x48, 0xff, 0xcb); w.Bytes(0x48, 0xf7, 0xd3);
        w.Bytes(0x4c, 0x21, 0xd3); // RBX = linear & ~(size - 1)
        w.Bytes(0x48, 0x89, 0xda); w.Bytes(0x48, 0x01, 0xc2); // RDX = exclusive end
        for (int i = 0; i < 7; i++)
        {
            ulong start = i == 0 ? r.Code : i == 1 ? r.Tables : i == 2 ? r.Data : r.Data + (ulong)(i * 2 - 3) * 4096;
            ulong size = i == 0 ? 3UL * 4096 : i == 1 ? 43UL * 4096 : i == 2 ? 2UL * 4096 : 4096;
            w.MovAbs(0, start); w.Bytes(0x48, 0x39, 0xc2);
            w.Branch(0x86, w.Position + 6 + 19); // end <= mapped start: disjoint
            w.MovAbs(0, start + size); w.Bytes(0x48, 0x39, 0xc3);
            w.Branch(0x82, FailureOffset + 3 * 32); // absent start < mapped end: overlap
        }
        w.Jump(2533);
    }


    private static void EmitFormat(ref Writer w, DoubleFaultIstRequest r)
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

    private static int FieldSource(int field) => field switch
    {
        0 => 256,
        1 => 264,
        2 => 272,
        3 => 280,
        4 => 352,
        5 => 360,
        6 => 368,
        7 => 376,
        8 => 384,
        9 => 392,
        10 => 400,
        11 => 408,
        12 => 272,
        13 => 416,
        14 => 344,
        15 => 512,
        16 => 520,
        17 => 528,
        18 => 536,
        19 => 544,
        20 => 552,
        21 => 576,
        22 => 584,
        23 => 592,
        24 => 600,
        25 => 608,
        26 => 616,
        27 => 640,
        28 => 648,
        29 => 656,
        30 => 664,
        31 => 672,
        32 => 680,
        33 => 704,
        34 => 712,
        35 => 720,
        36 => 728,
        37 => 736,
        38 => 744,
        39 => 768,
        40 => 776,
        41 => 784,
        42 => 792,
        43 => 800,
        44 => 808,
        45 => 960,
        46 => 968,
        47 => 976,
        48 => 944,
        49 => 984,
        50 => 992,
        51 => 1000,
        52 => 1008,
        53 => 1016,
        54 => 1024,
        55 => 1032,
        56 => 1040,
        57 => 1048,
        58 => 448,
        59 => 456,
        60 => 464,
        61 => 472,
        62 => 480,
        63 => 488,
        64 => 496,
        65 => 1056,
        66 => 1064,
        67 => 1072,
        68 => 1080,
        69 => 952,
        70 => 920,
        71 => 936,
        72 => 928,
        73 => 912,
        74 => 904,
        75 => 896,
        76 => 888,
        77 => 880,
        78 => 872,
        79 => 864,
        80 => 856,
        81 => 848,
        82 => 840,
        83 => 832,
        _ => 0
    };
}
