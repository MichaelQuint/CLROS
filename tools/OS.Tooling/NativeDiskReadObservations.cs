using System.Globalization;

namespace OS.Tooling;
/// <summary>Value-only inspection of selected M16_023_IO observations against
/// borrowed raw image bytes. This supplies no image, source, native, scheduler,
/// execution or guest qualification authority; the enclosing protocol owns those
/// checks and must select every disk observation in its original order.</summary>

public static partial class NativeDiskReadObservations
{
    public const int MaximumRows = 65536;
    public const int MaximumRowChars = 1536;
    /// <summary>Synchronously borrow unchanged selected rows and sector-aligned
    /// image bytes, at most MaximumRows rows of MaximumRowChars and the existing
    /// WebRootImageFixture.MaximumImageBytes image limit. Require one outstanding
    /// request, canonical decimal/lowercase hex, exact owner/epoch/generation/LBA,
    /// contiguous counters, ushort ring wrap and nondecreasing clocks. Compare
    /// every committed512-byte copy directly to its image sector. Zero-byte
    /// cancelled completions need no copy; positive completions require a copy
    /// before another submit or terminal success. A ready-result cancellation
    /// without a distinct zero-byte completion is outside this finite contract.
    /// Explicit allowMultiSector admits up to4 sectors: one Copied row followed
    /// by ordered CopyChunk rows with the same request/copy sequence, clock and exact
    /// chunk indices. The physical copied counter advances only for a complete
    /// group. Every row keeps MaximumRowChars; legacy callers admit512 only.
    /// Return bounded immutable copies; malformed, incomplete, extra or divergent
    /// observations throw ToolFailure. No I/O or owner lifetime is introduced.</summary>

    public static Inspection Inspect(IReadOnlyList<string> selectedRows, ReadOnlySpan<byte> rawImage, Boundary boundary, bool diskStaticFull = false, bool allowMultiSector = false) => InspectCore(selectedRows, rawImage, boundary, diskStaticFull, null, allowMultiSector);
    /// <summary>Inspect the separate pending-reset terminal shape. Preserve all
    /// preceding real submit/completion/copy/image joins from a fully copied
    /// bootstrap; the last observation
    /// must be the exact outstanding submit named by the supplied Stop receipt.
    /// Stop's software counters must be unchanged, with reset/readback,
    /// bus-master disable, DMA release and zero pool mask reported successful.
    /// No completion or bytes are synthesized. The ordinary/full readers retain
    /// their drained-only contract. Invalid or divergent values throw ToolFailure;
    /// original native/runner/receipt authentication remains the caller's duty.</summary>

    public static Inspection InspectResetRetired(IReadOnlyList<string> selectedRows, ReadOnlySpan<byte> rawImage, Boundary boundary, PendingResetRetirement retirement) => InspectCore(selectedRows, rawImage, boundary, false, retirement, false);
    private static Inspection InspectCore(IReadOnlyList<string> selectedRows, ReadOnlySpan<byte> rawImage, Boundary boundary, bool diskStaticFull, PendingResetRetirement? reset, bool allowMultiSector,
        Representation representation = Representation.Original)
    {
        int rowCount = selectedRows?.Count ?? 0;
        Need(rowCount > 0 && rowCount <= (diskStaticFull ? DiskStaticCapturePolicy.DeviceRows : MaximumRows) && rawImage.Length >= 512 && rawImage.Length <= WebRootImageFixture.MaximumImageBytes && rawImage.Length % 512 == 0, "input");
        Need(boundary.Owner != 0 && boundary.Epoch != 0 && boundary.BootstrapCopied <= boundary.BootstrapCompleted && boundary.Submitted > boundary.BootstrapCompleted && (reset is null ? boundary.Completed == boundary.Submitted : boundary.Completed < ulong.MaxValue && boundary.Submitted == boundary.Completed + 1) && boundary.Completed >= boundary.BootstrapCompleted && boundary.Copied >= boundary.BootstrapCopied && boundary.Copied <= boundary.Completed && boundary.Copied - boundary.BootstrapCopied <= boundary.Completed - boundary.BootstrapCompleted && boundary.Submitted - boundary.BootstrapCompleted <= (ulong)rowCount, "boundary");
        Need(reset is null || boundary.BootstrapCopied == boundary.BootstrapCompleted, "reset_bootstrap");
        List<Observation> result = new(rowCount);
        ulong submitted = boundary.BootstrapCompleted, completed = boundary.BootstrapCompleted, copied = boundary.BootstrapCopied, cancelled = 0, generation = 0, sector = 0, clock = 0, copyClock = 0;
        ushort available = unchecked((ushort)submitted), consumed = unchecked((ushort)completed);
        byte stage = 0; //0 drained,1 submitted,2 awaiting first copy,3 awaiting its remaining chunks.
        uint requestBytes = 0, nextChunk = 0;
        Span<byte> bytes = stackalloc byte[512];
        for (int i = 0; i < rowCount; i++)
        {
            string? row = selectedRows![i];
            Need(row is { Length: > 0 and <= MaximumRowChars }, "row_bound");
            ReadOnlySpan<char> remaining = row.AsSpan();
            ulong kind = Number(ref remaining, representation == Representation.Original ? "M16_023_IO kind=" : "CLROS_DISK_IO_V1 kind=");
            ulong owner = Number(ref remaining, " owner="), epoch = Number(ref remaining, " epoch="), currentGeneration = Number(ref remaining, " generation="), currentSector = Number(ref remaining, " sector="), sequence = Number(ref remaining, " sequence="), length = Number(ref remaining, " bytes="), currentAvailable = Number(ref remaining, " available="), currentConsumed = Number(ref remaining, " consumed="), now = Number(ref remaining, " clock=");
            if (representation != Representation.Original)
                Need(Number(ref remaining, " payload=") == (representation == Representation.DiagnosticTrace ? 1UL : 0UL), "diagnostic_mode");
            Need(kind <= (allowMultiSector ? 3UL : 2UL) && owner == boundary.Owner && epoch == boundary.Epoch && currentGeneration != 0 && currentAvailable <= ushort.MaxValue && currentConsumed <= ushort.MaxValue && now >= clock, "association");
            clock = now;
            if (kind == 0)
            {
                Need(stage == 0 && submitted != ulong.MaxValue, "submit_state");
                submitted++;
                Need(sequence == submitted && currentGeneration == submitted && submitted <= boundary.Submitted && (length == 512 || allowMultiSector && length is >= 1024 and <= 2048 && length % 512 == 0) && currentSector < (ulong)rawImage.Length / 512 && length / 512 <= (ulong)rawImage.Length / 512 - currentSector, "submit");
                requestBytes = (uint)length;
                nextChunk = 0;
                available = unchecked((ushort)(available + 1));
                generation = currentGeneration;
                sector = currentSector;
                stage = 1;
            }
            else
            {
                Need(currentGeneration == generation && currentSector == sector, "request_identity");
                if (kind == 1)
                {
                    Need(stage == 1 && completed != ulong.MaxValue, "completion_state");
                    completed++;
                    Need(sequence == completed && completed <= boundary.Completed && (length == 0 || length == requestBytes), "completion");
                    consumed = unchecked((ushort)(consumed + 1));
                    if (length == 0)
                    {
                        cancelled++;
                        stage = 0;
                    }
                    else
                        stage = 2;
                }
                else
                {
                    Need((kind == 2 ? stage == 2 && nextChunk == 0 : stage == 3 && nextChunk > 0) && copied != ulong.MaxValue, "copy_state");
                    Need(sequence == copied + 1 && sequence <= boundary.Copied && length == requestBytes, "copy");
                }
            }

            Need(currentAvailable == available && currentConsumed == consumed, "ring");
            string data = "";
            uint chunk = nextChunk;
            if (kind is 2 or 3)
            {
                if (stage == 2)
                    copyClock = now;
                else
                    Need(now == copyClock, "copy_clock");
                const string prefix = " copied=committed";
                Need(remaining.StartsWith(prefix, StringComparison.Ordinal), "copy_shape");
                remaining = remaining[prefix.Length..];
                if (requestBytes != 512)
                    Need(Number(ref remaining, " chunk=") == chunk, "copy_chunk");
                if (representation != Representation.Original)
                    Need(Number(ref remaining, " crc32=") == SectorCrc(rawImage.Slice(checked((int)((sector + chunk) * 512)), 512)), "diagnostic_crc");
                if (representation != Representation.DiagnosticSummary)
                {
                    Need(remaining.StartsWith(" data=", StringComparison.Ordinal), "copy_shape");
                    remaining = remaining[6..];
                    Need(remaining.Length == 1024, "copy_hex_length");
                    for (int j = 0; j < 512; j++)
                        bytes[j] = (byte)((Hex(remaining[j * 2]) << 4) | Hex(remaining[j * 2 + 1]));
                    // The submitted LBA was bounded before multiplication. The image
                    // cap keeps this exact sector offset and length representable.
                    Need(bytes.SequenceEqual(rawImage.Slice(checked((int)((sector + chunk) * 512)), 512)), "image_bytes");
                    data = new string (remaining);
                    remaining = default;
                }
                nextChunk++;
                if (nextChunk == requestBytes / 512)
                {
                    copied++;
                    stage = 0;
                }
                else
                    stage = 3;
            }

            Need(remaining.IsEmpty, "row_suffix");
            result.Add(new((Kind)kind, owner, epoch, currentGeneration, currentSector, sequence, (uint)length, (ushort)currentAvailable, (ushort)currentConsumed, now, data) { ChunkIndex = kind is 2 or 3 ? chunk : 0 });
        }

        Need(submitted == boundary.Submitted && completed == boundary.Completed && copied == boundary.Copied, "terminal");
        if (reset is { } retired)
        {
            Need(stage == 1 && retired.Owner == boundary.Owner && retired.Epoch == boundary.Epoch && retired.Generation == generation && generation == submitted && retired.Sector == sector && retired.SubmittedBeforeStop == submitted && retired.CompletedBeforeStop == completed && retired.CopiedBeforeStop == copied && retired.CancelledBeforeStop == cancelled && retired.StopStatus == OS.Storage.ContinuingReadOnlyBlockService.Status.Success && retired.ResetReads is> 0 and <= 2048 && retired.ResetComplete && retired.BusMasterDisabled && retired.DmaReleased && retired.PoolMask == 0, "reset_retirement");
        }
        else
            Need(stage == 0, "terminal");
        return new(boundary, cancelled, result.ToArray(), reset);
    }

    private static ulong Number(ref ReadOnlySpan<char> text, ReadOnlySpan<char> prefix)
    {
        Need(text.StartsWith(prefix, StringComparison.Ordinal), "row_shape");
        text = text[prefix.Length..];
        int count = 0;
        while (count < text.Length && text[count] is >= '0' and <= '9')
            count++;
        ulong value = 0;
        Need(count != 0 && (count == 1 || text[0] != '0') && ulong.TryParse(text[..count], NumberStyles.None, CultureInfo.InvariantCulture, out value), "number");
        text = text[count..];
        return value;
    }

    private static int Hex(char value) => value is >= '0' and <= '9' ? value - '0' : value is >= 'a' and <= 'f' ? value - 'a' + 10 : throw new ToolFailure("disk_observation_hex");
    private static void Need(bool condition, string reason)
    {
        if (!condition)
            throw new ToolFailure("disk_observation_" + reason);
    }
}
