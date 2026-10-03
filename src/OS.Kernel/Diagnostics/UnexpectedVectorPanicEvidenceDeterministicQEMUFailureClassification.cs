namespace OS.Kernel.Diagnostics;

public enum UnexpectedVectorPanicStatus : byte
{
    Uninitialized, Success, InvalidRequest, InvalidStorage, StorageOverlap,
    ContextMismatch, NormalizerMismatch, IdtMismatch, StreamMismatch,
    InvalidCapture, ControlledVector, StateMismatch, BackendNotReady,
    FormatRejected, DrainRejected, DrainTimeout, ConfigurationRejected,
    MetadataReadRejected, MetadataTimeout, MetadataWriteRejected, PanicRejected
}

/// <summary>Scalar intent only. Neither ranges nor prior observations grant readable/executable authority.</summary>
public readonly struct UnexpectedVectorPanicRequest(
    ulong codeAddress, ulong dataAddress, ulong imageAddress, ulong imageBytes,
    ulong callbackAddress, ulong firmwareCs, ulong firmwareCr3, bool optInRequested)
{
    public ulong CodeAddress { get; } = codeAddress;
    public ulong DataAddress { get; } = dataAddress;
    public ulong ImageAddress { get; } = imageAddress;
    public ulong ImageBytes { get; } = imageBytes;
    public ulong CallbackAddress { get; } = callbackAddress;
    public ulong FirmwareCs { get; } = firmwareCs;
    public ulong FirmwareCr3 { get; } = firmwareCr3;
    public bool OptInRequested { get; } = optInRequested;
}

public readonly struct UnexpectedVectorPanicResult
{
    internal UnexpectedVectorPanicResult(UnexpectedVectorPanicStatus status,
        int bytesWritten = 0, int bytesCompared = 0, int firstMismatch = -1,
        int firstDrain = 0, int secondDrain = 0, int metadataPolls = 0,
        int metadataWritten = 0, Com1OwnedPortIoOperationResult lastOperation = default,
        PanicSafeOwnedCom1ReportResult panic = default)
    {
        Status = status; BytesWritten = bytesWritten; BytesCompared = bytesCompared;
        FirstMismatchOffset = firstMismatch; FirstDrainAttempts = firstDrain;
        SecondDrainAttempts = secondDrain; MetadataPolls = metadataPolls;
        MetadataBytesWritten = metadataWritten; LastOperation = lastOperation; Panic = panic;
    }
    public UnexpectedVectorPanicStatus Status { get; }
    public bool Succeeded => Status == UnexpectedVectorPanicStatus.Success;
    /// <summary>Distinct caller-buffer bytes written by preparation, emission or formatting; serial writes have separate counters.</summary>
    public int BytesWritten { get; }
    public bool BuffersMutated => BytesWritten != 0;
    public int BytesCompared { get; }
    public int FirstMismatchOffset { get; }
    public int FirstDrainAttempts { get; }
    public int SecondDrainAttempts { get; }
    public int MetadataPolls { get; }
    public int MetadataBytesWritten { get; }
    public Com1OwnedPortIoOperationResult LastOperation { get; }
    public PanicSafeOwnedCom1ReportResult Panic { get; }
}

/// <summary>
/// Nonexecuting C2/D7 preparation and ordinary-data capture checks. Reporting invokes
/// only the supplied checked port backend. Stable accessible exclusively owned storage
/// is a caller prerequisite; scalar checks cannot prove mappings, lifetime or live state.
/// The future adapter owns callback identity, publication and actual CPU/native-stack proof.
/// </summary>
public static unsafe class UnexpectedVectorPanicEvidenceDeterministicQEMUFailureClassification
{
    public const bool EnabledByDefault = false;
    public const int PageBytes = 4096;
    public const int CodeBytes = 8192;
    // D0-D4 keep their descriptor/IST roles; D5-D6 form the fixed transition stack.
    // This capacity is not proof of the future compiled callback's whole-stack usage.
    public const int DataBytes = 28672;
    public const int ContextOffset = 4096;
    public const int ContextBytes = 4096;
    public const int CommonOffset = 0;
    public const int EntryOffset = 512;
    public const int TriggerOffset = 960;
    public const int TerminalOffset = 1024;
    public const int StreamUsedBytes = 1028;
    public const int CaptureBytes = 184;
    public const int CalleeDepthBytes = 232;
    public const int CaptureOffset = DataBytes - CaptureBytes;
    public const int LoadIdtrOffset = 176;
    public const int ReadIdtrOffset = 192;
    public const int RequestOffset = 224;
    public const int CaptureRspOffset = 288;
    public const int Cr2Offset = 296;
    public const int Cr3Offset = 304;
    public const int CsOffset = 312;
    public const int SsOffset = 320;
    public const int DsOffset = 328;
    public const int EsOffset = 336;
    public const int TrOffset = 344;
    public const int FirmwareCsOffset = 352;
    public const int LarOffset = 360;
    public const int EntryCr3Offset = 368;
    public const int CallbackReturnOffset = 376;
    public const int MetadataOffset = 512;
    public const int PanicScratchOffset = 2048;
    public const int PanicScratchBytes = 2048;
    public const int MetadataFieldCount = 24;
    public const int DrainAttemptLimit = 65536;
    public const int TransmitAttemptLimit = 128;

    public static UnexpectedVectorPanicResult PrepareContext(byte* code, ulong codeCapacity,
        byte* data, ulong dataCapacity, UnexpectedVectorPanicRequest request)
    {
        UnexpectedVectorPanicStatus status = ValidateStorage(code, codeCapacity, data, dataCapacity, request);
        if (status != UnexpectedVectorPanicStatus.Success) return new(status);
        // All dependency preconditions follow from the full aligned, disjoint C2/D7 checks.
        for (int i = 0; i < CodeBytes; i++) code[i] = 0;
        for (int i = 0; i < DataBytes; i++) data[i] = 0;
        NormalizeErrorCodeNonErrorCodeInterruptFrame.Materialize(code, PageBytes,
            request.CodeAddress, request.CodeAddress + PageBytes + CommonOffset);
        PopulateAllN256IDTVectorsOwnedGatesNever.Materialize(data, PageBytes,
            request.DataAddress, request.CodeAddress);
        FillContext(data + ContextOffset, request);
        return new(UnexpectedVectorPanicStatus.Success, CodeBytes + DataBytes);
    }

    public static UnexpectedVectorPanicResult EmitStream(byte* code, ulong codeCapacity,
        byte* data, ulong dataCapacity, UnexpectedVectorPanicRequest request)
    {
        UnexpectedVectorPanicResult check = Check(code, codeCapacity, data, dataCapacity, request, false, false);
        if (!check.Succeeded) return check;
        var writer = new StreamWriter(code + PageBytes, false);
        WriteStream(ref writer, request);
        return new(UnexpectedVectorPanicStatus.Success, PageBytes);
    }

    public static UnexpectedVectorPanicResult ValidateStreamReadBack(byte* code, ulong codeCapacity,
        byte* data, ulong dataCapacity, UnexpectedVectorPanicRequest request) =>
        Check(code, codeCapacity, data, dataCapacity, request, true, false);

    private static UnexpectedVectorPanicStatus ValidateStorage(byte* code, ulong codeCapacity,
        byte* data, ulong dataCapacity, UnexpectedVectorPanicRequest r)
    {
        if (!r.OptInRequested || !Extent(r.CodeAddress, CodeBytes, PageBytes) ||
            !Extent(r.DataAddress, DataBytes, PageBytes) || !Extent(r.ImageAddress, r.ImageBytes, 1) ||
            r.CallbackAddress < r.ImageAddress || r.CallbackAddress - r.ImageAddress >= r.ImageBytes ||
            r.FirmwareCs == 0 || r.FirmwareCs > 65535 || (r.FirmwareCs & 7) != 0 ||
            (r.FirmwareCr3 & ~0x000f_ffff_ffff_f018UL) != 0 || (r.FirmwareCr3 & ~4095UL) == 0)
            return UnexpectedVectorPanicStatus.InvalidRequest;
        if (Overlap(r.CodeAddress, CodeBytes, r.DataAddress, DataBytes) ||
            Overlap(r.CodeAddress, CodeBytes, r.ImageAddress, r.ImageBytes) ||
            Overlap(r.DataAddress, DataBytes, r.ImageAddress, r.ImageBytes))
            return UnexpectedVectorPanicStatus.StorageOverlap;
        if (codeCapacity < CodeBytes || dataCapacity < DataBytes ||
            !Extent((ulong)code, codeCapacity, PageBytes) || !Extent((ulong)data, dataCapacity, PageBytes) ||
            !Extent(r.CodeAddress, codeCapacity, PageBytes) || !Extent(r.DataAddress, dataCapacity, PageBytes))
            return UnexpectedVectorPanicStatus.InvalidStorage;
        if (Overlap((ulong)code, codeCapacity, (ulong)data, dataCapacity) ||
            Overlap(r.CodeAddress, codeCapacity, r.DataAddress, dataCapacity) ||
            Overlap(r.CodeAddress, codeCapacity, r.ImageAddress, r.ImageBytes) ||
            Overlap(r.DataAddress, dataCapacity, r.ImageAddress, r.ImageBytes))
            return UnexpectedVectorPanicStatus.StorageOverlap;
        return UnexpectedVectorPanicStatus.Success;
    }

    private static bool Extent(ulong address, ulong bytes, ulong alignment) =>
        address != 0 && bytes != 0 && (address & (alignment - 1)) == 0 &&
        address < 0x0000_8000_0000_0000UL && bytes <= 0x0000_8000_0000_0000UL - address;

    private static bool Overlap(ulong a, ulong an, ulong b, ulong bn) => a < b + bn && b < a + an;

    private static void FillContext(byte* context, UnexpectedVectorPanicRequest r)
    {
        for (int i = 0; i < MetadataOffset; i++) context[i] = 0;
        OwnedGDTTSSGeneratedAuditedCSharpStubBytes.PrepareContext(context, r.DataAddress + ContextOffset, 168);
        // RSP0 and three canonical ISTs; these pages are firmware-mapped, not guards.
        Put(context, 68, r.DataAddress + DataBytes);
        Put(context, 100, r.DataAddress + 3 * PageBytes);
        Put(context, 108, r.DataAddress + 4 * PageBytes);
        Put(context, 116, r.DataAddress + 5 * PageBytes);
        context[LoadIdtrOffset] = 0xff; context[LoadIdtrOffset + 1] = 0x0f;
        Put(context, LoadIdtrOffset + 2, r.DataAddress);
        Put(context, RequestOffset, r.CodeAddress); Put(context, RequestOffset + 8, r.DataAddress);
        Put(context, RequestOffset + 16, r.ImageAddress); Put(context, RequestOffset + 24, r.ImageBytes);
        Put(context, RequestOffset + 32, r.CallbackAddress); Put(context, RequestOffset + 40, r.FirmwareCs);
        Put(context, RequestOffset + 48, r.FirmwareCr3); Put(context, RequestOffset + 56, 1);
    }

    private static UnexpectedVectorPanicResult Check(byte* code, ulong codeCapacity,
        byte* data, ulong dataCapacity, UnexpectedVectorPanicRequest r, bool stream, bool captured)
    {
        UnexpectedVectorPanicStatus status = ValidateStorage(code, codeCapacity, data, dataCapacity, r);
        if (status != UnexpectedVectorPanicStatus.Success) return new(status);
        var normalizer = NormalizeErrorCodeNonErrorCodeInterruptFrame.ValidateReadBack(
            code, PageBytes, r.CodeAddress, r.CodeAddress + PageBytes + CommonOffset);
        if (!normalizer.Succeeded) return new(UnexpectedVectorPanicStatus.NormalizerMismatch,
            bytesCompared: normalizer.BytesCompared, firstMismatch: normalizer.FirstMismatchOffset);
        var idt = PopulateAllN256IDTVectorsOwnedGatesNever.ValidateReadBack(data, PageBytes,
            r.DataAddress, r.CodeAddress);
        if (!idt.Succeeded) return new(UnexpectedVectorPanicStatus.IdtMismatch,
            bytesCompared: idt.BytesCompared, firstMismatch: idt.FirstMismatchOffset);
        byte* expected = stackalloc byte[MetadataOffset + 16];
        expected = (byte*)(((ulong)expected + 15) & ~15UL);
        FillContext(expected, r);
        byte* ctx = data + ContextOffset;
        int compared = normalizer.BytesCompared + idt.BytesCompared;
        for (int i = 0; i < MetadataOffset; i++)
        {
            if (captured && ((i >= 10 && i < 22) || (i >= ReadIdtrOffset && i < ReadIdtrOffset + 10) ||
                (i >= CaptureRspOffset && i < CallbackReturnOffset + 4))) continue;
            compared++;
            byte value = expected[i];
            if (captured && (i == 37 || i == 45) && (ctx[i] & 0xfe) == value) continue;
            if (captured && i == 53) value = 0x8b;
            if (ctx[i] != value || (captured && (i == 37 || i == 45) && (ctx[i] & 0xfe) != expected[i]))
                return new(UnexpectedVectorPanicStatus.ContextMismatch, bytesCompared: i + 1, firstMismatch: i);
        }
        if (!captured)
            for (int i = ContextOffset + MetadataOffset; i < DataBytes; i++)
            {
                compared++;
                if (data[i] != 0) return new(UnexpectedVectorPanicStatus.ContextMismatch,
                    bytesCompared: i + 1, firstMismatch: i);
            }
        if (stream)
        {
            var writer = new StreamWriter(code + PageBytes, true);
            WriteStream(ref writer, r);
            if (writer.Mismatch >= 0) return new(UnexpectedVectorPanicStatus.StreamMismatch,
                bytesCompared: writer.Mismatch + 1, firstMismatch: writer.Mismatch);
            compared += writer.Position;
        }
        return new(UnexpectedVectorPanicStatus.Success, bytesCompared: compared);
    }

    /// <summary>Ordinary raw normalized data only; distinct from the hosted 200-byte CPU layout.</summary>
    public static UnexpectedVectorPanicStatus DecodeCapture(byte* capture, ulong capacity,
        ulong cr2, ulong cr3, out PanicSafeOwnedCom1FaultRecord fault)
    {
        fault = default;
        if (capacity < CaptureBytes || !Extent((ulong)capture, capacity, 8)) return UnexpectedVectorPanicStatus.InvalidCapture;
        ulong vector = Get(capture, 120), flag = Get(capture, 128), error = Get(capture, 136);
        if (vector > 255 || flag > 1 || flag != (NormalizeErrorCodeNonErrorCodeInterruptFrame.HasHardwareErrorCode((int)vector) ? 1UL : 0UL) ||
            (flag == 0 && error != 0) || ((vector == 8 || vector == 17) && error != 0))
            return UnexpectedVectorPanicStatus.InvalidCapture;
        if (vector == 6 || vector == 8 || vector == 14) return UnexpectedVectorPanicStatus.ControlledVector;
        fault = new((int)vector, flag != 0, error, Get(capture, 144), Get(capture, 168),
            Get(capture, 64), Get(capture, 160), cr2, cr3, Get(capture, 112), Get(capture, 104),
            Get(capture, 96), Get(capture, 88), Get(capture, 80), Get(capture, 72),
            Get(capture, 56), Get(capture, 48), Get(capture, 40), Get(capture, 32),
            Get(capture, 24), Get(capture, 16), Get(capture, 8), Get(capture, 0));
        return UnexpectedVectorPanicStatus.Success;
    }

    public static UnexpectedVectorPanicResult ValidateCapturedState(byte* code, ulong codeCapacity,
        byte* data, ulong dataCapacity, UnexpectedVectorPanicRequest r, out PanicSafeOwnedCom1FaultRecord fault)
    {
        fault = default;
        var check = Check(code, codeCapacity, data, dataCapacity, r, true, true);
        if (!check.Succeeded) return check;
        byte* ctx = data + ContextOffset;
        byte* capture = data + CaptureOffset;
        UnexpectedVectorPanicStatus status = DecodeCapture(capture, CaptureBytes, Get(ctx, Cr2Offset), Get(ctx, Cr3Offset), out fault);
        if (status != UnexpectedVectorPanicStatus.Success) return new(status);
        ulong flags = Get(capture, 160);
        if (fault.Vector != 32 || Get(capture, 144) != r.CodeAddress + PageBytes + TriggerOffset + 2 ||
            Get(capture, 152) != r.FirmwareCs || Get(capture, 168) != r.DataAddress + DataBytes ||
            Get(capture, 176) != 16 || (flags & 2) == 0 || (flags & 0x24200) != 0 ||
            Get(ctx, CaptureRspOffset) != r.DataAddress + CaptureOffset ||
            Get(ctx, Cr3Offset) != r.FirmwareCr3 || Get(ctx, EntryCr3Offset) != r.FirmwareCr3 ||
            Get(ctx, FirmwareCsOffset) != r.FirmwareCs || (Get(ctx, LarOffset) >> 32) != 0 || (Get(ctx, LarOffset) & 0x00f0fe00) != 0x00a09a00 ||
            Get(ctx, CsOffset) != 8 || Get(ctx, SsOffset) != 16 || Get(ctx, DsOffset) != 16 ||
            Get(ctx, EsOffset) != 16 || Get(ctx, TrOffset) != 24 ||
            ctx[10] != 39 || ctx[11] != 0 || Get(ctx, 12) != r.DataAddress + ContextOffset + 24 ||
            ctx[20] != 24 || ctx[21] != 0 || ctx[ReadIdtrOffset] != 0xff || ctx[ReadIdtrOffset + 1] != 0x0f ||
            Get(ctx, ReadIdtrOffset + 2) != r.DataAddress || Get(ctx, CallbackReturnOffset) != 0)
            return new(UnexpectedVectorPanicStatus.StateMismatch);
        return new(UnexpectedVectorPanicStatus.Success, bytesCompared: check.BytesCompared + CaptureBytes);
    }

    /// <summary>Supplied-buffer observations, not evidence of CPU execution. No fallback or reconfiguration on failure.</summary>
    public static UnexpectedVectorPanicResult ReportCaptured(Com1OwnedPortIoBackend backend,
        byte* code, ulong codeCapacity, byte* data, ulong dataCapacity, UnexpectedVectorPanicRequest r)
    {
        var check = ValidateCapturedState(code, codeCapacity, data, dataCapacity, r, out var fault);
        if (!check.Succeeded) return check;
        if (!backend.IsReady) return new(UnexpectedVectorPanicStatus.BackendNotReady);
        byte* context = data + ContextOffset;
        byte* metadata = context + MetadataOffset;
        byte* scratch = context + PanicScratchOffset;
        byte* subsystem = stackalloc byte[3]; subsystem[0] = 105; subsystem[1] = 100; subsystem[2] = 116;
        byte* reason = stackalloc byte[17];
        for (int i = 0; i < 17; i++) reason[i] = ReasonByte(i);
        // Format before any I/O. Both fixed buffers fit wholly within D1 and cannot alias the capture.
        var format = PanicSafeFormattingSerialReportingOwnedCom1.FormatFaultSummary(
            scratch, PanicScratchBytes, subsystem, 3, reason, 17, fault);
        if (!format.Succeeded) return new(UnexpectedVectorPanicStatus.FormatRejected);
        int length = WriteMetadata(metadata, data, code, r);
        int formattedBytes = length + format.BytesWritten;
        UnexpectedVectorPanicStatus status = Drain(backend, out int first, out var operation);
        if (status != UnexpectedVectorPanicStatus.Success)
            return new(status, bytesWritten: formattedBytes, firstDrain: first, lastOperation: operation);
        var configuration = backend.ConfigureDefault();
        if (!configuration.Succeeded) return new(UnexpectedVectorPanicStatus.ConfigurationRejected, bytesWritten: formattedBytes,
            firstDrain: first, lastOperation: configuration.LastOperation);
        int polls = 0, written = 0;
        for (int i = 0; i < length; i++)
        {
            bool ready = false;
            for (int poll = 0; poll < TransmitAttemptLimit; poll++)
            {
                operation = backend.ReadByte(Com1OwnedPortIoBackend.LineStatusRegisterPort); polls++;
                if (!operation.Succeeded) return new(UnexpectedVectorPanicStatus.MetadataReadRejected, bytesWritten: formattedBytes,
                    firstDrain: first, metadataPolls: polls, metadataWritten: written, lastOperation: operation);
                if ((operation.Value & 0x20) != 0) { ready = true; break; }
            }
            if (!ready) return new(UnexpectedVectorPanicStatus.MetadataTimeout, bytesWritten: formattedBytes,
                firstDrain: first, metadataPolls: polls, metadataWritten: written, lastOperation: operation);
            operation = backend.WriteByte(Com1OwnedPortIoBackend.DataRegisterPort, metadata[i]);
            if (!operation.Succeeded) return new(UnexpectedVectorPanicStatus.MetadataWriteRejected, bytesWritten: formattedBytes,
                firstDrain: first, metadataPolls: polls, metadataWritten: written, lastOperation: operation);
            written++;
        }
        status = Drain(backend, out int second, out operation);
        if (status != UnexpectedVectorPanicStatus.Success) return new(status, bytesWritten: formattedBytes, firstDrain: first,
            secondDrain: second, metadataPolls: polls, metadataWritten: written, lastOperation: operation);
        // No writer/configuration intervenes between the checked TEMT result and the unchanged reporter.
        var panic = PanicSafeFormattingSerialReportingOwnedCom1.ReportFaultSummary(backend,
            scratch, PanicScratchBytes, subsystem, 3, reason, 17, fault, TransmitAttemptLimit);
        return new(panic.Succeeded ? UnexpectedVectorPanicStatus.Success : UnexpectedVectorPanicStatus.PanicRejected, bytesWritten: formattedBytes,
            firstDrain: first, secondDrain: second, metadataPolls: polls, metadataWritten: written,
            lastOperation: panic.LastOperation, panic: panic);
    }

    private static UnexpectedVectorPanicStatus Drain(Com1OwnedPortIoBackend backend,
        out int attempts, out Com1OwnedPortIoOperationResult last)
    {
        attempts = 0; last = default;
        for (int i = 0; i < DrainAttemptLimit; i++)
        {
            last = backend.ReadByte(Com1OwnedPortIoBackend.LineStatusRegisterPort); attempts++;
            if (!last.Succeeded) return UnexpectedVectorPanicStatus.DrainRejected;
            if ((last.Value & 0x40) != 0) return UnexpectedVectorPanicStatus.Success;
        }
        return UnexpectedVectorPanicStatus.DrainTimeout;
    }

    private static byte ReasonByte(int i) => (byte)(i switch
    {
        0 => 'u', 1 => 'n', 2 => 'e', 3 => 'x', 4 => 'p', 5 => 'e', 6 => 'c', 7 => 't',
        8 => 'e', 9 => 'd', 10 => '-', 11 => 'v', 12 => 'e', 13 => 'c', 14 => 't', 15 => 'o', _ => 'r'
    });

    private static ulong Get(byte* p, int offset)
    {
        ulong value = 0; for (int i = 0; i < 8; i++) value |= (ulong)p[offset + i] << (i * 8); return value;
    }
    private static void Put(byte* p, int offset, ulong value)
    {
        for (int i = 0; i < 8; i++) p[offset + i] = (byte)(value >> (i * 8));
    }

    // Literal, relocation-bound instructions only. No general assembler, execution API,
    // return/IRET, old-stack restoration or copied PF/DF transition. Intel SDM 3A 6.14;
    // Microsoft x64 ABI: 32 home bytes + 8 alignment + CALL's 8-byte return slot.
    private static void WriteStream(ref StreamWriter w, UnexpectedVectorPanicRequest r)
    {
        // RAX,RBX,RCX,RDX,RSI,RDI,RBP,R8..R15 -> reversed 120-byte capture.
        w.B(0x50); w.B(0x53); w.B(0x51); w.B(0x52); w.B(0x56); w.B(0x57); w.B(0x55);
        for (byte reg = 0x50; reg <= 0x57; reg++) { w.B(0x41); w.B(reg); }
        w.B(0x48); w.B(0x89); w.B(0xe1); // RCX=current capture RSP, before ABI stack adjustment.
        w.B(0x48); w.B(0xba); w.Q(r.DataAddress + ContextOffset);
        w.B(0x48); w.B(0x89); w.B(0x8a); w.D(CaptureRspOffset);
        w.B(0x0f); w.B(0x20); w.B(0xd0); w.StoreRax(Cr2Offset);
        w.B(0x0f); w.B(0x20); w.B(0xd8); w.StoreRax(Cr3Offset);
        w.Selector(0xc8, CsOffset); w.Selector(0xd0, SsOffset);
        w.Selector(0xd8, DsOffset); w.Selector(0xc0, EsOffset);
        w.B(0x31); w.B(0xc0); w.B(0x66); w.B(0x0f); w.B(0x00); w.B(0xc8); w.StoreRax(TrOffset);
        w.B(0x66); w.B(0x89); w.B(0x42); w.B(20);
        w.B(0x0f); w.B(0x01); w.B(0x42); w.B(10); // SGDT
        w.B(0x0f); w.B(0x01); w.B(0x8a); w.D(ReadIdtrOffset); // SIDT
        w.B(0xfc); // CLD only after GPRs/frame have been preserved.
        w.B(0x48); w.B(0x83); w.B(0xec); w.B(40);
        w.B(0x48); w.B(0xb8); w.Q(r.CallbackAddress);
        w.B(0xff); w.B(0xd0); // sole call: RCX=capture, RDX=context; scalar EAX return.
        w.B(0x48); w.B(0xba); w.Q(r.DataAddress + ContextOffset); // RDX is volatile across callback.
        w.B(0x89); w.B(0x82); w.D(CallbackReturnOffset);
        w.Jump(TerminalOffset);
        w.Pad(EntryOffset);
        w.B(0x48); w.B(0xba); w.Q(r.DataAddress + ContextOffset);
        w.Selector(0xc8, FirmwareCsOffset);
        w.B(0x3d); w.D((uint)r.FirmwareCs); w.NotEqual(TerminalOffset);
        w.B(0x0f); w.B(0x02); w.B(0xc8); // LAR ECX,EAX while firmware GDT is active.
        w.NotEqual(TerminalOffset);
        w.B(0x48); w.B(0x89); w.B(0xc8); w.StoreRax(LarOffset);
        w.B(0x25); w.D(0x00f0fe00); w.B(0x3d); w.D(0x00a09a00); w.NotEqual(TerminalOffset);
        w.B(0x0f); w.B(0x20); w.B(0xd8); w.StoreRax(EntryCr3Offset);
        w.B(0x49); w.B(0xb8); w.Q(r.FirmwareCr3);
        w.B(0x4c); w.B(0x39); w.B(0xc0); w.NotEqual(TerminalOffset);
        w.B(0x0f); w.B(0x20); w.B(0xc0); // CR0: PE+PG, EM/TS clear for later native callback.
        w.B(0x25); w.D(0x8000000d); w.B(0x3d); w.D(0x80000001); w.NotEqual(TerminalOffset);
        w.B(0x0f); w.B(0x20); w.B(0xe0);
        w.B(0xa9); w.D(0x00821000); w.NotEqual(TerminalOffset); // CET/PCIDE/LA57 excluded.
        w.B(0x25); w.D(0x600); w.B(0x3d); w.D(0x600); w.NotEqual(TerminalOffset);
        w.B(0xfa);
        w.B(0x48); w.B(0xbc); w.Q(r.DataAddress + DataBytes);
        w.B(0x0f); w.B(0x01); w.B(0x12); // LGDT
        w.B(0x66); w.B(0xb8); w.B(16); w.B(0);
        w.B(0x8e); w.B(0xd8); w.B(0x8e); w.B(0xc0); w.B(0x8e); w.B(0xd0);
        w.B(0x66); w.B(0xb8); w.B(24); w.B(0);
        w.B(0x0f); w.B(0x00); w.B(0xd8); // LTR (makes TSS busy)
        w.B(0x0f); w.B(0x01); w.B(0x9a); w.D(LoadIdtrOffset); // LIDT
        w.Jump(TriggerOffset);
        w.Pad(TriggerOffset); w.B(0xcd); w.B(0x20); w.Jump(TerminalOffset);
        w.Pad(TerminalOffset); w.B(0xfa); w.B(0xf4); w.B(0xeb); w.B(0xfd);
        w.Pad(PageBytes);
    }

    private struct StreamWriter(byte* bytes, bool compare)
    {
        private readonly byte* _bytes = bytes;
        private readonly bool _compare = compare;
        public int Position;
        public int Mismatch = -1;
        public void B(byte value)
        {
            if (_compare) { if (_bytes[Position] != value && Mismatch < 0) Mismatch = Position; }
            else _bytes[Position] = value;
            Position++;
        }
        public void D(uint value) { for (int i = 0; i < 4; i++) B((byte)(value >> (i * 8))); }
        public void Q(ulong value) { for (int i = 0; i < 8; i++) B((byte)(value >> (i * 8))); }
        public void Pad(int end) { while (Position < end) B(0x90); }
        public void Jump(int target) { B(0xe9); D(unchecked((uint)(target - Position - 4))); }
        public void NotEqual(int target) { B(0x0f); B(0x85); D(unchecked((uint)(target - Position - 4))); }
        public void StoreRax(int offset) { B(0x48); B(0x89); B(0x82); D((uint)offset); }
        public void Selector(byte modRm, int offset)
        { B(0x31); B(0xc0); B(0x66); B(0x8c); B(modRm); StoreRax(offset); }
    }

    public const int MetadataByteCount = 650;

    private static int WriteMetadata(byte* destination, byte* data, byte* code, UnexpectedVectorPanicRequest r)
    {
        byte* ctx = data + ContextOffset;
        var w = new MetadataWriter(destination);
        w.Ascii(0x58454e555f32304dUL, 8); w.Ascii(0x535f444554434550UL, 8); w.Ascii(0x45544154UL, 4);
        w.Ascii(0x78303d6320UL, 5); w.Hex(r.CodeAddress);
        w.Ascii(0x78303d6420UL, 5); w.Hex(r.DataAddress);
        w.Ascii(0x747865746e6f6320UL, 8); w.Ascii(0x78303dUL, 3); w.Hex(r.DataAddress + ContextOffset);
        w.Ascii(0x6361626c6c616320UL, 8); w.Ascii(0x78303d6bUL, 4); w.Hex(r.CallbackAddress);
        w.Ascii(0x6572757470616320UL, 8); w.Ascii(0x78303d707352UL, 6); w.Hex(Get(ctx, CaptureRspOffset));
        w.Ascii(0x7261776d72696620UL, 8); w.Ascii(0x78303d734365UL, 6); w.Hex(Get(ctx, FirmwareCsOffset));
        w.Ascii(0x78303d736320UL, 6); w.Hex(Get(ctx, CsOffset));
        w.Ascii(0x78303d737320UL, 6); w.Hex(Get(ctx, SsOffset));
        w.Ascii(0x78303d736420UL, 6); w.Hex(Get(ctx, DsOffset));
        w.Ascii(0x78303d736520UL, 6); w.Hex(Get(ctx, EsOffset));
        w.Ascii(0x78303d727420UL, 6); w.Hex(Get(ctx, TrOffset));
        w.Ascii(0x7361427274646720UL, 8); w.Ascii(0x78303d65UL, 4); w.Hex(Get(ctx, 12));
        w.Ascii(0x6d694c7274646720UL, 8); w.Ascii(0x78303d7469UL, 5); w.Hex((ulong)(ctx[10] | (ctx[11] << 8)));
        w.Ascii(0x7361427274646920UL, 8); w.Ascii(0x78303d65UL, 4); w.Hex(Get(ctx, ReadIdtrOffset + 2));
        w.Ascii(0x6d694c7274646920UL, 8); w.Ascii(0x78303d7469UL, 5); w.Hex((ulong)(ctx[ReadIdtrOffset] | (ctx[ReadIdtrOffset + 1] << 8)));
        w.Ascii(0x6563634173737420UL, 8); w.Ascii(0x78303d7373UL, 5); w.Hex(ctx[53]);
        w.Ascii(0x776f4c6574616720UL, 8); w.Ascii(0x78303dUL, 3); w.Hex(Get(data, 32 * 16));
        w.Ascii(0x6769486574616720UL, 8); w.Ascii(0x78303d68UL, 4); w.Hex(Get(data, 32 * 16 + 8));
        w.Ascii(0x696c616d726f6e20UL, 8); w.Ascii(0x78303d3072657aUL, 7); w.Hex(Get(code, 32 * 16));
        w.Ascii(0x696c616d726f6e20UL, 8); w.Ascii(0x78303d3172657aUL, 7); w.Hex(Get(code, 32 * 16 + 8));
        w.Ascii(0x7265676769727420UL, 8); w.Ascii(0x78303dUL, 3); w.Hex(r.CodeAddress + PageBytes + TriggerOffset);
        w.Ascii(0x78303d33726320UL, 7); w.Hex(Get(ctx, Cr3Offset));
        w.Ascii(0x72437972746e6520UL, 8); w.Ascii(0x78303d33UL, 4); w.Hex(Get(ctx, EntryCr3Offset));
        w.Ascii(0x78303d72616c20UL, 7); w.Hex(Get(ctx, LarOffset));
        w.Ascii(0x0a0d, 2);
        return w.Position;
    }

    private struct MetadataWriter(byte* destination)
    {
        private readonly byte* _destination = destination;
        public int Position;
        public void Ascii(ulong packed, int count)
        { for (int i = 0; i < count; i++) _destination[Position++] = (byte)(packed >> (i * 8)); }
        public void Hex(ulong value)
        {
            for (int shift = 60; shift >= 0; shift -= 4)
            {
                int digit = (int)((value >> shift) & 15);
                _destination[Position++] = (byte)(digit < 10 ? 48 + digit : 55 + digit);
            }
        }
    }
}
