using System.Buffers.Binary;
using System.Text;
using Block = OS.Storage.WritableBlockSession;

namespace OS.Tooling;
/// <summary>Finite C#-authored writable Linux disk fixture. One worker checks
/// exact syscall results and copied bytes, then exits42; any mismatch exits99.
/// Construction supplies package/instruction values only. Continuing create,
/// growth, delete, descriptor waits and real OUT/FLUSH joins remain requirements
/// of a separate original guest owner; no SQLite or durability claim follows.</summary>

public static partial class LinuxWritableDiskProofFixture
{
    public const string AppPath = "apps/m16writabledisk.elf", Variant = "WritableDisk";
    public const string Witness = "M16_WRITABLE_DISK_OK!", FilePath = "/data/m16-write.bin", RootPath = "/data";
    public const string OutsidePath = "/data/../private/sentinel";
    public const ulong ImageBase = 0x400000;
    public const int ElfBytes = 0x4000, EntryOffset = 0x200, FailureOffset = 0xf00, WitnessOffset = 0x180;
    public const ulong FileName = 0x401000, RootName = 0x401080, OutsideName = 0x4010c0;
    public const ulong Iovec = 0x401200, WriteLock = 0x401240, QueryLock = 0x401280, Unlock = 0x4012c0;
    public const ulong Zeroes = 0x401300, Payload = 0x402000, Output = 0x403000;
    public const uint PayloadBytes = 1032, FileOffset = 4094, ResultingFileBytes = FileOffset + PayloadBytes;
    public const uint RwfNoAppend = 0x20, CreateFlags = 2 | 0x40 | 0x80 | 0x20000 | 0x80000;
    public const uint ReadFlags = 0x8000 | 0x20000 | 0x80000, RootFlags = 0x8000 | 0x80000;
    public const long ReservedLockByte = 1073741825;
    public const byte Sentinel = 0xa5;
    public const string LogUploadVariant = "WritableLogUpload", LogUploadAppPath = "apps/m16logupload.elf", LogUploadWitness = "M16_LOG_UPLOAD_OK!";
    public const int LogUploadElfBytes = 0x6000;
    public const ulong LogName = 0x401000, UploadName = 0x401040, LogUploadIovecs = 0x401200;
    public const ulong ExpectedLogAddress = 0x402000, ExpectedUploadAddress = 0x402400, LogUploadOutput = 0x404000;
    public const uint LogUploadWriteFlags = 2 | 0x100 | 0x8000 | 0x20000 | 0x80000, LogUploadReadFlags = ReadFlags | 0x100;
    /// <summary>Return fresh deterministic bytes for the fixed static ELF and
    /// existing mechanical package envelope. The envelope permits two threads;
    /// this instruction stream never clones or requests user timer interrupts.</summary>

    public static byte[] CreatePackage() => LinuxThreadProofFixture.CreateMechanicalPackage(CreateElf(), Variant, AppPath, userInterrupts: false, socketListener: true, descriptorCount: 32);
    public static byte[] CreateElf() => Build().Elf;
    public static IReadOnlyList<CallSpec> Calls() => Array.AsReadOnly(Build().Calls);
    public static IReadOnlyList<ulong> InstructionRips() => Array.AsReadOnly(Build().Instructions);
    public static IReadOnlyList<ulong> ExpectedReturnRips() => Array.AsReadOnly(Build().Calls.Select(c => c.ReturnRip).ToArray());
    public static uint ExpectedCalls => checked((uint)Build().Calls.Length);
    public static ulong SuccessRip => Build().Calls[^1].ReturnRip;
    public static ulong MainEndRip => Build().MainEnd;

    public const ulong FailureExitRip = ImageBase + FailureOffset + 41;
    /// <summary>Return an independent payload copy. Its deterministic varying
    /// bytes cross both the 4096-byte boundary and multiple 512-byte sectors.</summary>

    public static byte[] ExpectedPayload()
    {
        byte[] result = new byte[PayloadBytes];
        for (uint i = 0; i < PayloadBytes; i++)
            result[i] = unchecked((byte)((i * 73 + 19) ^ (i >> 3) ^ (i >> 9)));
        return result;
    }

    /// <summary>Borrow complete package bytes synchronously and require the
    /// exact canonical envelope and ELF. Changed, foreign or truncated input
    /// throws; success supplies no native execution or persistence authority.</summary>

    public static void RequirePackage(ReadOnlySpan<byte> package)
    {
        if (!package.SequenceEqual(CreatePackage()))
            throw new ToolFailure("writable_disk_package_identity");
    }

    /// <summary>Construct a separate finite log/upload mechanical package.
    /// The original owner must independently authenticate initially empty
    /// /data/LOG.TXT and UPLOAD.BIN; this stream does not establish freshness.
    /// It checks the candidate's 660/5126 copied bytes, explicit file/directory
    /// sync return values, close/reopen and EOF. No BCL execution, append mode,
    /// original I/O, process, persistence or native admission is established.</summary>

    public static byte[] CreateLogUploadPackage() => LinuxThreadProofFixture.CreateMechanicalPackage(CreateLogUploadElf(), LogUploadVariant, LogUploadAppPath, userInterrupts: false, socketListener: true, descriptorCount: 32);
    public static byte[] CreateLogUploadElf() => BuildLogUpload().Elf;
    public static IReadOnlyList<CallSpec> LogUploadCalls() => Array.AsReadOnly(BuildLogUpload().Calls);
    public static IReadOnlyList<ulong> LogUploadInstructionRips() => Array.AsReadOnly(BuildLogUpload().Instructions);
    public static IReadOnlyList<ulong> LogUploadExpectedReturnRips() => Array.AsReadOnly(BuildLogUpload().Calls.Select(c => c.ReturnRip).ToArray());
    public static ulong LogUploadSuccessRip => BuildLogUpload().Calls[^1].ReturnRip;
    public static ulong LogUploadMainEndRip => BuildLogUpload().MainEnd;

    /// <summary>Borrow complete bytes only for this distinct package. Reject
    /// changed/foreign/default packages; return no execution/storage authority.</summary>

    public static void RequireLogUploadPackage(ReadOnlySpan<byte> package)
    {
        if (!package.SequenceEqual(CreateLogUploadPackage()))
            throw new ToolFailure("writable_log_upload_package_identity");
    }

    public const int MaximumBlockObservationSamples = 65536;
    /// <summary>Synchronously borrow bounded copied observation values, never
    /// live MMIO/state/DMA or pointers retained from their owner. The caller must
    /// prevent concurrent input mutation. Include each Published generation and
    /// its later completion/copy/cancel changes; unchanged repeats are allowed.
    /// One pump may combine completion and IN copy in one sample. Default empty
    /// observer slots are not selected rows. The initial boundary is drained
    /// and has no uncompleted write bytes, but may contain unflushed writes.
    /// Final unresolved/held/error requests remain explicit; Stop/timeout facts
    /// absent from these values are never inferred. Throws on malformed input,
    /// missing publication/completion/copy, conflicting accumulation, gaps,
    /// impossible counters/frontiers or capacity. All returned bytes are copied.
    /// No ELF/package bytes or original execution authority are involved.</summary>

    public static unsafe BlockObservationInspection InspectBlockObservationValues(ReadOnlySpan<Block.Observation> samples, BlockBoundary before, BlockBoundary after)
    {
        if (samples.Length > MaximumBlockObservationSamples)
            throw new ToolFailure("writable_observation_capacity");
        RequireBoundary(before);
        RequireBoundary(after);
        if (before.Owner != after.Owner || before.Epoch != after.Epoch || before.SectorCount != after.SectorCount || before.Submitted != before.Completed || before.SubmittedBytes != before.CompletedBytes)
            throw new ToolFailure("writable_observation_boundary");
        List<BlockRequestFacts> requests = [];
        BlockBoundary baseline = before;
        Block.Observation previous = default;
        byte[] payload = [];
        int first = -1, completion = -1, copy = -1;
        for (int index = 0; index < samples.Length; index++)
        {
            Block.Observation current = samples[index];
            bool next = first < 0 || current.Request.Generation != previous.Request.Generation;
            if (next)
            {
                if (first >= 0)
                {
                    if (!Resolved(previous) || previous.DeviceResult != Block.Status.Success)
                        throw new ToolFailure("writable_observation_unresolved");
                    requests.Add(new(previous, first, completion, copy, index - 1, payload));
                    baseline = Boundary(previous, before);
                }

                if (current.Flags != Block.ObservationFlags.Published || current.Cancelled)
                    throw new ToolFailure("writable_observation_publication");
                first = index;
                completion = copy = -1;
                payload = [];
            }
            else
            {
                if (current.Request.Owner != previous.Request.Owner || current.Request.Epoch != previous.Request.Epoch || current.Kind != previous.Kind || current.Sector != previous.Sector || (current.Flags & previous.Flags) != previous.Flags || previous.Cancelled && !current.Cancelled || Completed(previous) && current.DeviceResult != previous.DeviceResult || !previous.Cancelled && current.Cancelled && (Copied(previous) || Completed(previous) && (previous.Kind != Block.Operation.Read || previous.DeviceResult != Block.Status.Success)))
                    throw new ToolFailure("writable_observation_accumulation");
            }

            RequireSnapshot(current, baseline);
            ReadOnlySpan<byte> data = new(current.Data, 512);
            if (current.PayloadBytes == 0)
            {
                if (data.IndexOfAnyExcept((byte)0) >= 0)
                    throw new ToolFailure("writable_observation_payload");
            }
            else if (payload.Length == 0)
                payload = data.ToArray();
            else if (!data.SequenceEqual(payload))
                throw new ToolFailure("writable_observation_payload");
            if (completion < 0 && Completed(current))
                completion = index;
            if (copy < 0 && Copied(current))
                copy = index;
            previous = current;
        }

        if (first >= 0)
            requests.Add(new(previous, first, completion, copy, samples.Length - 1, payload));
        if ((first < 0 ? before : Boundary(previous, before)) != after)
            throw new ToolFailure("writable_observation_final_boundary");
        return new(before, after, samples.Length, requests.ToArray());
    }

    private static void RequireBoundary(BlockBoundary value)
    {
        if (value.Owner == 0 || value.Epoch == 0 || value.SectorCount is 0 or > ulong.MaxValue / 512 || value.Completed > value.Submitted || value.Submitted - value.Completed > 1 || value.SubmittedBytes % 512 != 0 || value.CompletedBytes % 512 != 0 || value.FlushAcknowledgedBytes % 512 != 0 || value.CompletedBytes > value.SubmittedBytes || value.FlushAcknowledgedBytes > value.CompletedBytes || value.SubmittedBytes / 512 > value.Submitted || value.CompletedBytes / 512 > value.Completed)
            throw new ToolFailure("writable_observation_boundary");
    }

    private static void RequireSnapshot(Block.Observation value, BlockBoundary before)
    {
        bool complete = Completed(value), copied = Copied(value);
        if (before.Submitted == ulong.MaxValue || value.Kind is not (Block.Operation.Write or Block.Operation.Flush or Block.Operation.Read) || (value.Flags & ~(Block.ObservationFlags.Published | Block.ObservationFlags.CompletionValidated | Block.ObservationFlags.ReadCopied)) != 0 || (value.Flags & Block.ObservationFlags.Published) == 0 || value.Request.Owner != before.Owner || value.Request.Epoch != before.Epoch || value.Request.Generation != before.Submitted + 1 || value.SubmittedCount != before.Submitted + 1 || (value.Kind == Block.Operation.Flush ? value.Sector != 0 : value.Sector >= before.SectorCount) || (complete ? value.DeviceResult is not (Block.Status.Success or Block.Status.DeviceIoError or Block.Status.UnsupportedRequest) : value.DeviceResult != Block.Status.Uninitialized) || copied && (value.Kind != Block.Operation.Read || !complete || value.DeviceResult != Block.Status.Success || value.Cancelled))
            throw new ToolFailure("writable_observation_request");
        bool success = complete && value.DeviceResult == Block.Status.Success;
        if (value.Kind == Block.Operation.Write && before.SubmittedBytes > ulong.MaxValue - 512)
            throw new ToolFailure("writable_observation_overflow");
        ulong submittedBytes = before.SubmittedBytes + (value.Kind == Block.Operation.Write ? 512UL : 0);
        ulong completedBytes = before.CompletedBytes + (success && value.Kind == Block.Operation.Write ? 512UL : 0);
        ulong completed = before.Completed + (complete ? 1UL : 0);
        uint completionBytes = success && value.Kind != Block.Operation.Flush ? 512U : 0;
        uint payloadBytes = value.Kind == Block.Operation.Write || copied ? 512U : 0;
        ulong target = value.Kind == Block.Operation.Flush ? before.CompletedBytes : 0;
        ulong acknowledged = success && value.Kind == Block.Operation.Flush ? target : before.FlushAcknowledgedBytes;
        if (value.CompletedCount != completed || value.SubmittedBytes != submittedBytes || value.CompletedBytes != completedBytes || value.AvailableIndex != unchecked((ushort)value.SubmittedCount) || value.ConsumedIndex != unchecked((ushort)completed) || value.CompletionBytes != completionBytes || value.PayloadBytes != payloadBytes || value.FlushTargetBytes != target || value.FlushAcknowledgedBytes != acknowledged)
            throw new ToolFailure("writable_observation_frontier");
    }

    private static bool Completed(Block.Observation value) => (value.Flags & Block.ObservationFlags.CompletionValidated) != 0;
    private static bool Copied(Block.Observation value) => (value.Flags & Block.ObservationFlags.ReadCopied) != 0;
    private static bool Resolved(Block.Observation value) => Completed(value) && (value.Kind != Block.Operation.Read || value.DeviceResult != Block.Status.Success || value.Cancelled || Copied(value));
    private static BlockBoundary Boundary(Block.Observation value, BlockBoundary owner) => new(owner.Owner, owner.Epoch, owner.SectorCount, value.SubmittedCount, value.CompletedCount, value.SubmittedBytes, value.CompletedBytes, value.FlushAcknowledgedBytes);
    private static Built Build()
    {
        byte[] elf = new byte[ElfBytes];
        elf.AsSpan(EntryOffset, 4096 - EntryOffset).Fill(0xcc);
        "\u007fELF"u8.CopyTo(elf);
        elf[4] = 2;
        elf[5] = 1;
        elf[6] = 1;
        void U16(int at, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(elf.AsSpan(at), value);
        void U32(int at, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(elf.AsSpan(at), value);
        void U64(int at, ulong value) => BinaryPrimitives.WriteUInt64LittleEndian(elf.AsSpan(at), value);
        void Text(int at, string value) => Encoding.ASCII.GetBytes(value).CopyTo(elf, at);
        U16(16, 2);
        U16(18, 62);
        U32(20, 1);
        U64(24, ImageBase + EntryOffset);
        U64(32, 64);
        U16(52, 64);
        U16(54, 56);
        U16(56, 3);
        void Load(int at, uint type, uint flags, ulong offset, ulong address, ulong length, ulong alignment)
        {
            U32(at, type);
            U32(at + 4, flags);
            U64(at + 8, offset);
            U64(at + 16, address);
            U64(at + 24, address);
            U64(at + 32, length);
            U64(at + 40, length);
            U64(at + 48, alignment);
        }

        Load(64, 1, 5, 0, ImageBase, 4096, 4096);
        Load(120, 1, 6, 4096, ImageBase + 4096, ElfBytes - 4096, 4096);
        Load(176, 0x6474e551, 6, 0, 0, 0, 16);
        Text(WitnessOffset, Witness);
        Text((int)(FileName - ImageBase), FilePath + "\0");
        Text((int)(RootName - ImageBase), RootPath + "\0");
        Text((int)(OutsideName - ImageBase), OutsidePath + "\0");
        U64((int)(Iovec - ImageBase), Payload);
        U64((int)(Iovec - ImageBase + 8), PayloadBytes);
        void Flock(ulong address, ushort type)
        {
            int at = (int)(address - ImageBase);
            U16(at, type);
            U64(at + 8, (ulong)ReservedLockByte);
            U64(at + 16, 1);
        }

        Flock(WriteLock, 1);
        Flock(QueryLock, 1);
        Flock(Unlock, 2);
        ExpectedPayload().CopyTo(elf, (int)(Payload - ImageBase));
        elf.AsSpan((int)(Output - ImageBase), 4096).Fill(Sentinel);
        Writer w = new(elf, EntryOffset);
        w.Instruction(0xc031, 2);
        w.Instruction(0xc88c66, 3);
        w.Compare(0x3b);
        w.Call("create", 2, FileName, CreateFlags, 0x180, 3); // mode0600, with no POSIX-mode persistence claim.
        w.Call("write.positioned", 328, 3, Iovec, 1, PayloadBytes, a3: FileOffset, a4: 0, a5: RwfNoAppend);
        w.Call("write.cursor", 8, 3, 0, 1, 0);
        w.Call("read.hole-boundary", 17, 3, Output, 16, 16, 16, FileOffset - 8);
        w.CompareBytes(Output, Zeroes, 8);
        w.CompareBytes(Output + 8, Payload, 8);
        w.Call("open.independent", 2, FileName, ReadFlags, 0, 4);
        w.Call("read.hole", 0, 4, Output, 16, 16, 16);
        w.CompareBytes(Output, Zeroes, 16);
        w.Call("read.seek", 8, 4, FileOffset, 0, FileOffset);
        w.Call("read.payload", 0, 4, Output, 8, 8, 8);
        w.CompareBytes(Output, Payload, 8);
        w.Call("read.independent-cursor", 8, 3, 0, 1, 0);
        w.Call("lock.set", 72, 3, 6, WriteLock, 0);
        w.Call("lock.get-own", 72, 3, 5, QueryLock, 0, 32);
        w.MemoryDword(QueryLock, 2);
        w.Call("lock.release", 72, 3, 6, Unlock, 0);
        w.Call("sync.file", 74, 3, 0, 0, 0);
        w.Call("sync.parent-open", 2, RootName, RootFlags, 0, 5);
        w.Call("sync.parent", 74, 5, 0, 0, 0);
        w.Call("sync.parent-close", 3, 5, 0, 0, 0);
        w.Call("close.independent", 3, 4, 0, 0, 0);
        w.Call("close.original", 3, 3, 0, 0, 0);
        w.Call("reopen", 2, FileName, ReadFlags, 0, 3);
        w.Call("read.reopened", 17, 3, Output, PayloadBytes, PayloadBytes, PayloadBytes, FileOffset);
        w.CompareBytes(Output, Payload, PayloadBytes);
        w.MemoryByte(Output + PayloadBytes, Sentinel);
        w.Call("close.reopened", 3, 3, 0, 0, 0);
        w.Call("unlink", 87, FileName, 0, 0, 0);
        w.Call("open.deleted", 2, FileName, ReadFlags, 0, -2);
        w.Call("sync.deleted-parent-open", 2, RootName, RootFlags, 0, 3);
        w.Call("sync.deleted-parent", 74, 3, 0, 0, 0);
        w.Call("sync.deleted-parent-close", 3, 3, 0, 0, 0);
        w.Call("read.closed", 0, 3, Output, 1, -9);
        w.Call("open.outside", 2, OutsideName, ReadFlags, 0, -13);
        w.Call("witness", 1, 1, ImageBase + WitnessOffset, (uint)Witness.Length, Witness.Length);
        w.Syscall("exit", 231, 42, 0, 0, 0);
        w.JumpFailure();
        if (w.Position >= FailureOffset || w.Calls.Count > 64)
            throw new ToolFailure("writable_disk_code_capacity");
        Built result = new(elf, w.Calls.ToArray(), w.Instructions.ToArray(), ImageBase + (uint)w.Position);
        Writer failure = new(elf, FailureOffset);
        failure.Instruction(0xfc, 1);
        failure.Syscall("failure", 231, 99, 0, 0, 0);
        failure.Instruction(0xf4, 1);
        if (failure.Calls[0].ReturnRip != FailureExitRip)
            throw new ToolFailure("writable_disk_failure_rip");
        return result;
    }

    private static Built BuildLogUpload()
    {
        byte[] log = NativeAotLogUploadContract.ExpectedLog(), upload = NativeAotLogUploadContract.ExpectedUpload();
        if (log.Length != 660 || upload.Length != 5126)
            throw new ToolFailure("writable_log_upload_payload");
        byte[] elf = new byte[LogUploadElfBytes];
        elf.AsSpan(EntryOffset, 4096 - EntryOffset).Fill(0xcc);
        "\u007fELF"u8.CopyTo(elf);
        elf[4] = 2;
        elf[5] = 1;
        elf[6] = 1;
        void U16(int at, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(elf.AsSpan(at), value);
        void U32(int at, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(elf.AsSpan(at), value);
        void U64(int at, ulong value) => BinaryPrimitives.WriteUInt64LittleEndian(elf.AsSpan(at), value);
        void Text(ulong address, string value) => Encoding.ASCII.GetBytes(value).CopyTo(elf, (int)(address - ImageBase));
        U16(16, 2);
        U16(18, 62);
        U32(20, 1);
        U64(24, ImageBase + EntryOffset);
        U64(32, 64);
        U16(52, 64);
        U16(54, 56);
        U16(56, 3);
        void Load(int at, uint type, uint flags, ulong offset, ulong address, ulong length, ulong alignment)
        {
            U32(at, type);
            U32(at + 4, flags);
            U64(at + 8, offset);
            U64(at + 16, address);
            U64(at + 24, address);
            U64(at + 32, length);
            U64(at + 40, length);
            U64(at + 48, alignment);
        }

        Load(64, 1, 5, 0, ImageBase, 4096, 4096);
        Load(120, 1, 6, 4096, ImageBase + 4096, LogUploadElfBytes - 4096, 4096);
        Load(176, 0x6474e551, 6, 0, 0, 0, 16);
        Text(ImageBase + WitnessOffset, LogUploadWitness);
        Text(LogName, "/data/LOG.TXT\0");
        Text(UploadName, "/data/UPLOAD.BIN\0");
        Text(RootName, RootPath + "\0");
        Text(OutsideName, OutsidePath + "\0");
        log.CopyTo(elf, (int)(ExpectedLogAddress - ImageBase));
        upload.CopyTo(elf, (int)(ExpectedUploadAddress - ImageBase));
        elf.AsSpan((int)(LogUploadOutput - ImageBase), 8192).Fill(Sentinel);
        ulong Vector(int index) => LogUploadIovecs + (uint)index * 16;
        for (int i = 0; i < 14; i++)
        {
            ulong source = i < 12 ? ExpectedLogAddress + (uint)i * 55 : ExpectedUploadAddress + (i == 12 ? 0UL : 4094UL);
            U64((int)(Vector(i) - ImageBase), source);
            U64((int)(Vector(i) - ImageBase + 8), i < 12 ? 55UL : i == 12 ? 4094UL : 1032UL);
        }

        Writer w = new(elf, EntryOffset);
        w.Instruction(0xc031, 2);
        w.Instruction(0xc88c66, 3);
        w.Compare(0x3b);
        w.Call("log.open.first", 2, LogName, LogUploadWriteFlags, 0, 3);
        for (int i = 0; i < 6; i++)
            w.Call("log.row." + (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), 328, 3, Vector(i), 1, 55, a3: (uint)i * 55, a5: RwfNoAppend);
        w.Call("log.sync.first", 74, 3, 0, 0, 0);
        w.Call("log.close.first", 3, 3, 0, 0, 0);
        w.Call("log.open.second", 2, LogName, LogUploadWriteFlags, 0, 3);
        w.Call("log.seek.end", 8, 3, 0, 2, 330);
        for (int i = 6; i < 12; i++)
            w.Call("log.row." + (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), 328, 3, Vector(i), 1, 55, a3: (uint)i * 55, a5: RwfNoAppend);
        w.Call("log.cursor", 8, 3, 0, 1, 330);
        w.Call("log.sync.second", 74, 3, 0, 0, 0);
        w.Call("log.close.second", 3, 3, 0, 0, 0);
        w.Call("log.reopen", 2, LogName, LogUploadReadFlags, 0, 3);
        w.Call("log.read", 17, 3, LogUploadOutput, 660, 660, 660);
        w.CompareBytes(LogUploadOutput, ExpectedLogAddress, 660);
        w.MemoryByte(LogUploadOutput + 660, Sentinel);
        w.Call("log.eof", 17, 3, LogUploadOutput + 660, 1, 0, 1, 660);
        w.MemoryByte(LogUploadOutput + 660, Sentinel);
        w.Call("log.close.reopened", 3, 3, 0, 0, 0);
        w.Call("upload.open", 2, UploadName, LogUploadWriteFlags, 0, 3);
        w.Call("upload.first", 328, 3, Vector(12), 1, 4094, a5: RwfNoAppend);
        w.Call("upload.second", 328, 3, Vector(13), 1, 1032, a3: 4094, a5: RwfNoAppend);
        w.Call("upload.cursor", 8, 3, 0, 1, 0);
        w.Call("upload.sync", 74, 3, 0, 0, 0);
        w.Call("upload.close", 3, 3, 0, 0, 0);
        w.Call("upload.reopen", 2, UploadName, LogUploadReadFlags, 0, 3);
        w.Call("upload.read", 17, 3, LogUploadOutput, 5126, 5126, 5126);
        w.CompareBytes(LogUploadOutput, ExpectedUploadAddress, 5126);
        w.MemoryByte(LogUploadOutput + 5126, Sentinel);
        w.Call("upload.eof", 17, 3, LogUploadOutput + 5126, 1, 0, 1, 5126);
        w.MemoryByte(LogUploadOutput + 5126, Sentinel);
        w.Call("upload.close.reopened", 3, 3, 0, 0, 0);
        w.Call("parent.open", 2, RootName, RootFlags, 0, 3);
        w.Call("parent.sync", 74, 3, 0, 0, 0);
        w.Call("parent.close", 3, 3, 0, 0, 0);
        w.Call("open.outside", 2, OutsideName, LogUploadReadFlags, 0, -13);
        w.Call("witness", 1, 1, ImageBase + WitnessOffset, (uint)LogUploadWitness.Length, LogUploadWitness.Length);
        w.Syscall("exit", 231, 42, 0, 0, 0);
        w.JumpFailure();
        if (w.Position >= FailureOffset || w.Calls.Count != 40)
            throw new ToolFailure("writable_log_upload_code_capacity");
        Built result = new(elf, w.Calls.ToArray(), w.Instructions.ToArray(), ImageBase + (uint)w.Position);
        Writer failure = new(elf, FailureOffset);
        failure.Instruction(0xfc, 1);
        failure.Syscall("failure", 231, 99, 0, 0, 0);
        failure.Instruction(0xf4, 1);
        if (failure.Calls[0].ReturnRip != FailureExitRip)
            throw new ToolFailure("writable_log_upload_failure_rip");
        return result;
    }
}
