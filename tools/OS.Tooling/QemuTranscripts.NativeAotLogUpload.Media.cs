using B = OS.Storage.WritableBlockSession;
using Combined = OS.Tooling.CombinedWebDataImageFixture;
using Data = OS.Tooling.WritableDataImageFixture;
using F = OS.Tooling.LinuxWritableDiskProofFixture;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Check only copied write/verify media values for the exact pinned
    /// B8 package and its own unchanged ESP package entry. Synchronously borrow
    /// stable128MiB initial/final snapshots and independently extracted Combined
    /// inspections. Every IO/STATE row is selected at its original index and
    /// must lie in the supplied inclusive window; all other known027 and outer
    /// rows remain explicitly unjoined. Unknown027 families reject. Actual file
    /// holds, window provenance, firmware/process/packet/syscall inputs and a
    /// previous writer or cold reboot remain separate original-owner duties.
    /// Write requires empty targets, canonical660/5126 final bytes, successful
    /// observed FLUSH frontier coverage and guarded OUT payloads. Verify requires
    /// canonical initial files, identical entire images and no OUT publication.
    /// Every IN is compared with the serialized image view advanced only by
    /// successful OUT completion. Masks restrict media consistency, not dynamic
    /// path, fd or allocation authority; intermediate filesystem validity is not
    /// inferred. Errors, cancellation, dirty or unresolved work throw without a
    /// partial report. No buffer is retained or written, no callback/I/O occurs,
    /// and no execution, scheduler, sync-call or physical durability is granted.</summary>

    public static NativeAotLogUploadMediaReport InspectNativeAotLogUploadMediaValues(string[] originalRows, int firstRecordIndex, int lastRecordIndex, F.BlockBoundary before, F.BlockBoundary after, ReadOnlySpan<byte> canonicalPackage, string phase, ReadOnlySpan<byte> initialBytes, ReadOnlySpan<byte> finalBytes, Combined.Inspection initial, Combined.Inspection final)
    {
        var package = NativeAotLogUploadContract.InspectClrosPackage(canonicalPackage, phase);
        return InspectNativeAotLogUploadMediaCore(originalRows, firstRecordIndex, lastRecordIndex, before, after, package.PackageBytes, package.PackageSha256, phase, initialBytes, finalBytes, initial, final);
    }

    // Pure copied-media test seam, with no package parser override. The public
    // entrypoint always supplies the fixed-B8 canonical inspector's identity.
    internal static NativeAotLogUploadMediaReport InspectNativeAotLogUploadMediaCore(string[] originalRows, int firstRecordIndex, int lastRecordIndex, F.BlockBoundary before, F.BlockBoundary after, int packageBytes, string packageSha256, string phase, ReadOnlySpan<byte> initialBytes, ReadOnlySpan<byte> finalBytes, Combined.Inspection initial, Combined.Inspection final)
    {
        MediaNeed(phase is "write" or "verify", "phase");
        MediaNeed(originalRows is { Length: > 0 and <= MaximumWritableDiskRows } && firstRecordIndex >= 0 && firstRecordIndex <= lastRecordIndex && lastRecordIndex < originalRows.Length && packageBytes > 0 && packageSha256 is { Length: 64 } && packageSha256.All(char.IsAsciiHexDigit), "input");
        string[] rows = originalRows!.ToArray();
        MediaNeed(rows.All(row => row is { Length: > 0 and <= 2048 } && row.All(c => c is >= ' ' and <= '~')), "input");
        MediaNeed(initial is not null && final is not null && initialBytes.Length == Combined.ImageBytes && finalBytes.Length == Combined.ImageBytes, "images");
        var embedded = initial!.WebFiles.SingleOrDefault(file => file.Path == "/OS/APP.PKG");
        MediaNeed(embedded is not null && embedded.Bytes == packageBytes && embedded.Sha256.Equals(packageSha256, StringComparison.OrdinalIgnoreCase), "esp_package");
        List<WritableDiskRow> selected = [], residual = [];
        for (int i = 0; i < rows.Length; i++)
        {
            string tag = rows[i].Split(' ', 2)[0];
            if (tag is "M16_027_IO" or "M16_027_STATE")
            {
                MediaNeed(i >= firstRecordIndex && i <= lastRecordIndex, "window");
                selected.Add(new(i, rows[i]));
            }
            else
            {
                MediaNeed(!tag.StartsWith("M16_027", StringComparison.Ordinal) || tag is "M16_027_BOUND" or "M16_027_STOP" or "M16_027_END" or "M16_027_BEGIN" or "M16_027_WAKE" or "M16_027_COMPLETE" or "M16_027_REPARK", "unknown_writable_record");
                residual.Add(new(i, rows[i]));
            }
        }

        var block = InspectWritableBlockValues(selected, before, after);
        MediaNeed(before.Submitted == before.Completed && before.SubmittedBytes == before.CompletedBytes && before.CompletedBytes == before.FlushAcknowledgedBytes && after.Submitted == after.Completed && after.SubmittedBytes == after.CompletedBytes && after.CompletedBytes == after.FlushAcknowledgedBytes && !block.HasCancelled && !block.HasDeviceError && !block.HasStateFailure && !block.HasUncompletedPublication && !block.HasHeldRead && !block.Dirty, "clean_frontiers");
        if (phase == "verify")
        {
            MediaNeed(block.Io.All(row => row.Kind != B.Operation.Write), "verify_out");
            MediaNeed(initialBytes.SequenceEqual(finalBytes), "verify_image");
            MediaCanonical(initial.Data);
            MediaCanonical(final!.Data);
        }
        else
            MediaNeed(block.Io.Any(row => row.Kind == B.Operation.Write) && block.Io.Any(row => row.Kind == B.Operation.Flush && row.IsComplete && row.Status == B.Status.Success), "write_flush");
        // Reuse the serialized OUT/IN view, exact final raw equality, both image
        // hashes, GPT/ESP/BPB, full inventory and extracted guard/file checks.
        WdImages(block, initialBytes, finalBytes, initial, final!, phase == "write");
        MediaNeed(initial.Data.RootClusters.SequenceEqual(final!.Data.RootClusters), "root_chain");
        if (phase == "write")
            MediaOutMasks(block, initialBytes, finalBytes, initial, final);
        return new(phase, packageSha256.ToUpperInvariant(), rows.Length, firstRecordIndex, lastRecordIndex, block, selected, residual, initial, final);
    }

    private static void MediaCanonical(Data.Inspection image)
    {
        MediaNeed(image.Find("LOG.TXT") is { Name: "LOG.TXT" } log && log.CopyBytes().AsSpan().SequenceEqual(NativeAotLogUploadContract.ExpectedLog()) && image.Find("UPLOAD.BIN") is { Name: "UPLOAD.BIN" } upload && upload.CopyBytes().AsSpan().SequenceEqual(NativeAotLogUploadContract.ExpectedUpload()), "canonical_files");
    }

    private static void MediaOutMasks(WritableBlockValueReport block, ReadOnlySpan<byte> initialBytes, ReadOnlySpan<byte> finalBytes, Combined.Inspection initial, Combined.Inspection final)
    {
        Dictionary<ulong, MediaMask> masks = [];
        MediaMask Mask(ulong sector)
        {
            if (!masks.TryGetValue(sector, out var mask))
                masks.Add(sector, mask = new());
            return mask;
        }

        foreach (string name in new[]
        {
            "LOG.TXT",
            "UPLOAD.BIN"
        }

        )
        {
            var old = initial.Data.Find(name)!;
            var file = final.Data.Find(name)!;
            MediaNeed(old.Bytes == 0 && old.Clusters.Count == 0 && old.ShortEntryOffset == file.ShortEntryOffset, "target_entry");
            var root = Mask(old.ShortEntryOffset / 512);
            int entry = (int)(old.ShortEntryOffset % 512);
            foreach (int field in new[]
            {
                20,
                26,
                27,
                28,
                29,
                30,
                31
            }

            )
                root.Bits[entry + field] = 0xff;
            root.Bits[entry + 21] = 0x0f; // Cluster bits28..31 are reserved here too.
            int remaining = file.Bytes;
            foreach (uint cluster in file.Clusters)
            {
                foreach (ulong fat in new[]
                {
                    (ulong)initial.Data.FirstFatSector,
                    (ulong)initial.Data.FirstFatSector + Data.FatSectors
                }

                )
                {
                    var mask = Mask(fat + cluster / 128);
                    int offset = (int)(cluster % 128 * 4);
                    mask.Bits[offset] = mask.Bits[offset + 1] = mask.Bits[offset + 2] = 0xff;
                    mask.Bits[offset + 3] = 0x0f;
                }

                ulong sector = initial.Data.FirstDataSector + cluster - 2UL;
                var data = Mask(sector);
                int take = Math.Min(remaining, 512);
                data.Bits.AsSpan(0, take).Fill(0xff);
                data.ZeroTail = take;
                MediaNeed(finalBytes.Slice(checked((int)sector * 512 + take), 512 - take).IndexOfAnyExcept((byte)0) < 0, "target_tail");
                remaining -= take;
            }

            MediaNeed(remaining == 0, "target_chain");
        }

        foreach (ulong sector in new[]
        {
            initial.DataFirst + 1,
            initial.DataFirst + 7
        }

        )
            Mask(sector).Bits.AsSpan(488, 8).Fill(0xff);
        // Check every publication, not only the last payload for each sector.
        // A temporary protected-byte change cannot be hidden by a later restore.
        foreach (var row in block.Io.Where(row => row.Kind == B.Operation.Write && row.Flags == 1))
        {
            MediaNeed(masks.TryGetValue(row.Sector, out var mask), "out_nonowned");
            byte[] payload = row.CopyPayload();
            int offset = checked((int)row.Sector * 512);
            for (int i = 0; i < 512; i++)
                MediaNeed(i >= mask!.ZeroTail ? payload[i] == 0 : ((payload[i] ^ initialBytes[offset + i]) & ~mask.Bits[i]) == 0, "out_protected_bytes");
        }
    }

    private static void MediaNeed(bool value, string reason) => Need(value, "log_upload_media_" + reason);
}
