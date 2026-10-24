using System.Globalization;

namespace OS.Tooling;

/// <summary>Value-only inspection of selected M16_023_IO observations against
/// borrowed raw image bytes. This supplies no image, source, native, scheduler,
/// execution or guest qualification authority; the enclosing protocol owns those
/// checks and must select every disk observation in its original order.</summary>
public static class NativeDiskReadObservations
{
    public const int MaximumRows = 65536;
    public const int MaximumRowChars = 1536;
    public enum Kind : byte { Submitted, CompletionValidated, Copied }

    /// <summary>Separately authenticated owner/counter boundaries. Bootstrap is
    /// drained: its submitted and completed counts are BootstrapCompleted, with
    /// BootstrapCopied successful copies. Final Submitted must equal Completed.
    /// Counts include bootstrap; they are not deltas or execution authority.</summary>
    public readonly record struct Boundary(ulong Owner, ulong Epoch, ulong BootstrapCompleted,
        ulong BootstrapCopied, ulong Submitted, ulong Completed, ulong Copied);

    /// <summary>Copied immutable facts. DataHex contains exactly512 bytes only
    /// for Copied, whose native candidate was committed before serialization.
    /// Empty DataHex on the other stages grants no data-copy authority.</summary>
    public readonly record struct Observation(Kind Kind, ulong Owner, ulong Epoch, ulong Generation,
        ulong Sector, ulong Sequence, uint Bytes, ushort AvailableIndex, ushort ConsumedIndex,
        ulong Clock, string DataHex);

    /// <summary>Read-only copied observations and the inspected cumulative
    /// boundaries. Cancelled counts only zero-byte completions after bootstrap.
    /// No caller array, image alias or complete serial record is retained.</summary>
    public sealed class Inspection
    {
        internal Inspection(Boundary boundary, ulong cancelled, Observation[] rows)
        { Counters = boundary; Cancelled = cancelled; Rows = Array.AsReadOnly(rows); }
        public Boundary Counters { get; }
        public ulong Cancelled { get; }
        public IReadOnlyList<Observation> Rows { get; }
    }

    /// <summary>Synchronously borrow unchanged selected rows and sector-aligned
    /// image bytes, at most MaximumRows rows of MaximumRowChars and the existing
    /// WebRootImageFixture.MaximumImageBytes image limit. Require one outstanding
    /// request, canonical decimal/lowercase hex, exact owner/epoch/generation/LBA,
    /// contiguous counters, ushort ring wrap and nondecreasing clocks. Compare
    /// every committed512-byte copy directly to its image sector. Zero-byte
    /// cancelled completions need no copy; positive completions require a copy
    /// before another submit or terminal success. A ready-result cancellation
    /// without a distinct zero-byte completion is outside this finite contract.
    /// Return bounded immutable copies; malformed, incomplete, extra or divergent
    /// observations throw ToolFailure. No I/O or owner lifetime is introduced.</summary>
    public static Inspection Inspect(IReadOnlyList<string> selectedRows, ReadOnlySpan<byte> rawImage, Boundary boundary, bool diskStaticFull = false)
    {
        int rowCount = selectedRows?.Count ?? 0;
        Need(rowCount>0 && rowCount<=(diskStaticFull?DiskStaticCapturePolicy.DeviceRows:MaximumRows) && rawImage.Length >= 512 &&
            rawImage.Length <= WebRootImageFixture.MaximumImageBytes && rawImage.Length % 512 == 0, "input");
        Need(boundary.Owner != 0 && boundary.Epoch != 0 && boundary.BootstrapCopied <= boundary.BootstrapCompleted &&
            boundary.Submitted > boundary.BootstrapCompleted && boundary.Completed == boundary.Submitted &&
            boundary.Copied >= boundary.BootstrapCopied && boundary.Copied <= boundary.Completed &&
            boundary.Copied - boundary.BootstrapCopied <= boundary.Completed - boundary.BootstrapCompleted &&
            boundary.Submitted - boundary.BootstrapCompleted <= (ulong)rowCount, "boundary");
        List<Observation> result = new(rowCount);
        ulong submitted = boundary.BootstrapCompleted, completed = boundary.BootstrapCompleted,
            copied = boundary.BootstrapCopied, cancelled = 0, generation = 0, sector = 0, clock = 0;
        ushort available = unchecked((ushort)submitted), consumed = unchecked((ushort)completed);
        byte stage = 0; //0 drained,1 submitted,2 positive validated completion awaiting its copy.
        Span<byte> bytes = stackalloc byte[512];
        for (int i = 0; i < rowCount; i++)
        {
            string? row = selectedRows![i];
            Need(row is { Length: > 0 and <= MaximumRowChars }, "row_bound");
            ReadOnlySpan<char> remaining = row.AsSpan();
            ulong kind = Number(ref remaining, "M16_023_IO kind=");
            ulong owner = Number(ref remaining, " owner="), epoch = Number(ref remaining, " epoch="),
                currentGeneration = Number(ref remaining, " generation="), currentSector = Number(ref remaining, " sector="),
                sequence = Number(ref remaining, " sequence="), length = Number(ref remaining, " bytes="),
                currentAvailable = Number(ref remaining, " available="), currentConsumed = Number(ref remaining, " consumed="),
                now = Number(ref remaining, " clock=");
            Need(kind <= 2 && owner == boundary.Owner && epoch == boundary.Epoch && currentGeneration != 0 &&
                currentAvailable <= ushort.MaxValue && currentConsumed <= ushort.MaxValue && now >= clock, "association");
            clock = now;
            if (kind == 0)
            {
                Need(stage == 0 && submitted != ulong.MaxValue, "submit_state");
                submitted++;
                Need(sequence == submitted && currentGeneration == submitted && submitted <= boundary.Submitted &&
                    length == 512 && currentSector < (ulong)rawImage.Length / 512, "submit");
                available = unchecked((ushort)(available + 1)); generation = currentGeneration; sector = currentSector; stage = 1;
            }
            else
            {
                Need(currentGeneration == generation && currentSector == sector, "request_identity");
                if (kind == 1)
                {
                    Need(stage == 1 && completed != ulong.MaxValue, "completion_state");
                    completed++;
                    Need(sequence == completed && completed <= boundary.Completed && length is 0 or 512, "completion");
                    consumed = unchecked((ushort)(consumed + 1));
                    if (length == 0) { cancelled++; stage = 0; }
                    else stage = 2;
                }
                else
                {
                    Need(stage == 2 && copied != ulong.MaxValue, "copy_state");
                    copied++;
                    Need(sequence == copied && copied <= boundary.Copied && length == 512, "copy");
                    stage = 0;
                }
            }
            Need(currentAvailable == available && currentConsumed == consumed, "ring");
            string data = "";
            if (kind == 2)
            {
                const string prefix = " copied=committed data=";
                Need(remaining.StartsWith(prefix, StringComparison.Ordinal), "copy_shape");
                remaining = remaining[prefix.Length..]; Need(remaining.Length == 1024, "copy_hex_length");
                for (int j = 0; j < 512; j++)
                    bytes[j] = (byte)((Hex(remaining[j * 2]) << 4) | Hex(remaining[j * 2 + 1]));
                // The submitted LBA was bounded before multiplication. The image
                // cap keeps this exact sector offset and length representable.
                Need(bytes.SequenceEqual(rawImage.Slice(checked((int)(sector * 512)), 512)), "image_bytes");
                data = new string(remaining); remaining = default;
            }
            Need(remaining.IsEmpty, "row_suffix");
            result.Add(new((Kind)kind, owner, epoch, currentGeneration, currentSector, sequence,
                (uint)length, (ushort)currentAvailable, (ushort)currentConsumed, now, data));
        }
        Need(stage == 0 && submitted == boundary.Submitted && completed == boundary.Completed && copied == boundary.Copied,
            "terminal");
        return new(boundary, cancelled, result.ToArray());
    }

    private static ulong Number(ref ReadOnlySpan<char> text, ReadOnlySpan<char> prefix)
    {
        Need(text.StartsWith(prefix, StringComparison.Ordinal), "row_shape"); text = text[prefix.Length..];
        int count = 0; while (count < text.Length && text[count] is >= '0' and <= '9') count++;
        ulong value = 0;
        Need(count != 0 && (count == 1 || text[0] != '0') &&
            ulong.TryParse(text[..count], NumberStyles.None, CultureInfo.InvariantCulture, out value), "number");
        text = text[count..]; return value;
    }
    private static int Hex(char value) => value is >= '0' and <= '9' ? value - '0' :
        value is >= 'a' and <= 'f' ? value - 'a' + 10 : throw new ToolFailure("disk_observation_hex");
    private static void Need(bool condition, string reason)
    { if (!condition) throw new ToolFailure("disk_observation_" + reason); }
}
