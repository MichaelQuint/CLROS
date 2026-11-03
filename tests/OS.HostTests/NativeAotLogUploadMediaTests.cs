using System.Buffers.Binary;
using System.Globalization;
using System.Reflection;
using OS.Tooling;
using B = OS.Storage.WritableBlockSession;
using Combined = OS.Tooling.CombinedWebDataImageFixture;
using Data = OS.Tooling.WritableDataImageFixture;
using F = OS.Tooling.LinuxWritableDiskProofFixture;
using Q = OS.Tooling.QemuTranscripts;

// Synthetic copied protocol/image vectors, not engine or SQLite execution.
// The internal pure seam cannot supply public pinned-B8 package authority.
internal static partial class NativeAotLogUploadMediaTests
{
    private static readonly Core Inspect = typeof(Q).GetMethod("InspectNativeAotLogUploadMediaCore", BindingFlags.Static | BindingFlags.NonPublic)!.CreateDelegate<Core>();
    [HostTest("log-upload media copied write replays all sectors and independently extracts canonical outputs without original authority")]
    private static void WriteValues() => WithImages(f =>
    {
        var w = f.Write();
        string[] rows = w.Rows;
        var result = f.Check(w);
        HostCheck.True(result.Phase == "write" && result.Block.After.FlushAcknowledgedBytes > 0 && !result.Block.Dirty && result.Block.Io.Any(row => row.Kind == B.Operation.Flush && row.IsComplete) && result.ObservedOutSectors.Count == 18 && result.Final.Data.Find("LOG.TXT")!.CopyBytes().AsSpan().SequenceEqual(NativeAotLogUploadContract.ExpectedLog()) && result.Final.Data.Find("UPLOAD.BIN")!.CopyBytes().AsSpan().SequenceEqual(NativeAotLogUploadContract.ExpectedUpload()));
        HostCheck.True(result.Initial.WebPartitionSha256 == result.Final.WebPartitionSha256 && result.Initial.Data.Find("GUARD.TXT")!.Sha256 == result.Final.Data.Find("GUARD.TXT")!.Sha256 && result.BlockRows.Select(row => row.RecordIndex).Concat(result.UnjoinedRows.Select(row => row.RecordIndex)).Order().SequenceEqual(Enumerable.Range(0, rows.Length)) && result.FirstRecordIndex == 1 && result.LastRecordIndex == rows.Length - 2);
        HostCheck.True(!result.SyscallInputQualified && !result.OriginalCreatorOrProcessQualified && !result.NativeOrGuestQualified && !result.RebootOrPhysicalPersistenceQualified && !result.WholeCaptureQualified);
        byte[] copy = result.Final.Data.Find("UPLOAD.BIN")!.CopyBytes();
        copy.AsSpan().Clear();
        rows[0] = "changed";
        HostCheck.True(result.UnjoinedRows[0].Text == "OUTER synthetic=unjoined" && result.Final.Data.Find("UPLOAD.BIN")!.Bytes == 5126);
        bool denied = false;
        try
        {
            ((IList<ulong>)result.ObservedOutSectors).Clear();
        }
        catch (NotSupportedException)
        {
            denied = true;
        }

        HostCheck.True(denied);
    });
    [HostTest("log-upload media verify requires canonical initial data identical entire image and zero OUT even for no-op metadata")]
    private static void VerifyValues() => WithImages(f =>
    {
        var w = new Wire(f.Final);
        foreach (ulong lba in f.TargetSectors)
            w.Read(lba);
        var result = f.Check(w, "verify");
        HostCheck.True(result.ObservedOutSectors.Count == 0 && result.Initial.RawSha256 == result.Final.RawSha256 && result.Block.Before.SubmittedBytes == result.Block.After.SubmittedBytes && result.Block.Io.All(row => row.Kind == B.Operation.Read));
        foreach (ulong lba in new[]
        {
            (ulong)f.Initial.Data.FirstFatSector,
            Combined.DataFirstLba + 1UL,
            f.TargetSectors[0]
        }

        )
        {
            var noop = new Wire(f.Final);
            noop.Write(lba, f.Sector(f.Final, lba));
            noop.Flush();
            Reject(() => f.Check(noop, "verify"), "log_upload_media_verify_out");
        }

        var empty = new Wire(f.InitialBytes);
        Reject(() => f.Check(empty, "verify", f.InitialBytes, f.Initial, f.InitialBytes, f.Initial), "log_upload_media_canonical_files");
        var noWrite = new Wire(f.Final);
        Reject(() => f.Check(noWrite, "write", f.Final, f.FinalInspection), "log_upload_media_write_flush");
        Reject(() => f.Check(w, "VERIFY"), "log_upload_media_phase");
        Reject(() => f.Check(w, "verify", packageHash: new string ('0', 64)), "log_upload_media_esp_package");
        Reject(() => f.Check(w, "verify", packageBytes: checked((int)f.Package.Bytes + 1)), "log_upload_media_esp_package");
    });
    [HostTest("log-upload media rejects transient protected FAT root FSInfo tail and nonowned OUT even after restoration")]
    private static void ProtectedWrites() => WithImages(f =>
    {
        ulong fat = f.Initial.Data.FirstFatSector, root = f.Initial.Data.FirstDataSector;
        foreach ((ulong sector, int offset)in new[]
        {
            (fat, 0),
            (fat, 100 * 4 + 3),
            (root, 21),
            (root, 128 + 11),
            (root, 5 * 32 + 1),
            (Combined.DataFirstLba + 1UL, 20),
            (f.TargetSectors[1], 200)
        }

        )
        {
            byte[] changed = f.Sector(f.InitialBytes, sector);
            changed[offset] ^= 0x80;
            var w = f.Write(prefix: wire =>
            {
                wire.Write(sector, changed);
                wire.Write(sector, f.Sector(f.InitialBytes, sector));
            });
            Reject(() => f.Check(w), "log_upload_media_out_protected_bytes");
        }

        // Only the low12 bits of the high word are mutable. This is a copied
        // field-mask control, not validity/allocation authority for that transient
        // cluster value or an assertion about intermediate filesystem states.
        byte[] low12 = f.Sector(f.InitialBytes, root);
        low12[20] = 0xff;
        low12[21] = 0x0f;
        var permitted = f.Write(prefix: wire =>
        {
            wire.Write(root, low12);
            wire.Write(root, f.Sector(f.InitialBytes, root));
        });
        HostCheck.True(!f.Check(permitted).WholeCaptureQualified);
        foreach (ulong sector in new[]
        {
            root + 900,
            fat + Data.FatSectors - 1,
            (ulong)Combined.WebFirstLba,
            f.Initial.Data.Find("GUARD.TXT")!.Extents[0].FirstLba,
            Combined.DataFirstLba + 2UL
        }

        )
        {
            var w = f.Write(prefix: wire => wire.Write(sector, f.Sector(f.InitialBytes, sector)));
            Reject(() => f.Check(w));
        }
    });
    [HostTest("log-upload media IN order payload and final full raw equality reject forged or unobserved copies")]
    private static void Replay() => WithImages(f =>
    {
        var correct = f.Write();
        string[] rows = correct.Rows;
        int read = Array.FindIndex(rows, row => row.StartsWith("M16_027_IO ", StringComparison.Ordinal) && Field(row, "flags") == 7);
        var forged = rows.ToArray();
        int data = forged[read].IndexOf(" data=", StringComparison.Ordinal) + 6;
        forged[read] = forged[read][..data] + (forged[read][data] == '0' ? '1' : '0') + forged[read][(data + 1)..];
        Reject(() => f.Check(correct, rows: forged), "writable_transcript_read_bytes");
        // This positive read precedes every OUT; future final bytes cannot be
        // substituted for the observed initial directory sector.
        forged = rows.ToArray();
        forged[read] = forged[read][..data] + Convert.ToHexString(f.Sector(f.Final, f.Initial.Data.FirstDataSector)).ToLowerInvariant();
        Reject(() => f.Check(correct, rows: forged), "writable_transcript_read_bytes");
        var omitted = f.Write(omit: f.TargetSectors[0]);
        Reject(() => f.Check(omitted), "writable_transcript_final_bytes");
        byte[] changed = f.Final.ToArray();
        changed[checked((int)f.TargetSectors[0] * 512)] ^= 1;
        Reject(() => f.Check(correct, finalBytes: changed), "writable_transcript_image_hash");
        changed = f.Final.ToArray();
        changed[512 + 16] ^= 1;
        Reject(() => f.Check(correct, finalBytes: changed), "writable_transcript_image_hash");
        var noFlush = f.Write(flush: false);
        Reject(() => f.Check(noFlush), "log_upload_media_clean_frontiers");
    });
    [HostTest("log-upload media refuses cancelled failed held unresolved dirty and unacknowledged FLUSH facts")]
    private static void Failures() => WithImages(f =>
    {
        var cancelled = new Wire(f.InitialBytes);
        cancelled.Read(f.Initial.Data.FirstDataSector, cancelled: true);
        Reject(() => f.Check(cancelled), "log_upload_media_clean_frontiers");
        var held = new Wire(f.InitialBytes);
        held.Read(f.Initial.Data.FirstDataSector, held: true);
        Reject(() => f.Check(held), "log_upload_media_clean_frontiers");
        var pending = new Wire(f.InitialBytes);
        pending.Write(f.TargetSectors[0], f.Sector(f.Final, f.TargetSectors[0]), pending: true);
        Reject(() => f.Check(pending), "log_upload_media_clean_frontiers");
        var failed = f.Write(flush: false);
        failed.Flush(error: true);
        Reject(() => f.Check(failed), "log_upload_media_clean_frontiers");
        var missing = f.Write(flush: false);
        missing.Flush(pending: true);
        Reject(() => f.Check(missing), "log_upload_media_clean_frontiers");
        var rollback = f.Write();
        string[] rows = rollback.Rows;
        int lastFlush = Array.FindLastIndex(rows, row => row.StartsWith("M16_027_IO ", StringComparison.Ordinal) && Field(row, "kind") == 1);
        rows[lastFlush] = Set(rows[lastFlush], "flushAcknowledgedBytes", 0);
        Reject(() => f.Check(rollback, rows: rows));
    });
    [HostTest("log-upload media public B8 gate and full indexed census reject synthetic packages omissions windows and unknown writable rows")]
    private static void PublicAndCensus() => WithImages(f =>
    {
        var w = f.Write();
        string[] rows = w.Rows;
        foreach (string phase in new[]
        {
            "write",
            "verify"
        }

        )
            Reject(() => Q.InspectNativeAotLogUploadMediaValues(rows, 1, rows.Length - 2, w.Before, w.After, f.Package.CopyBytes(), phase, f.InitialBytes, f.Final, f.Initial, f.FinalInspection), "log_upload_clros_package");
        Reject(() => f.Check(w, first: 2), "log_upload_media_window");
        Reject(() => f.Check(w, last: rows.Length - 3), "log_upload_media_window");
        Reject(() => f.Check(w, first: -1), "log_upload_media_input");
        Reject(() => f.Check(w, last: rows.Length), "log_upload_media_input");
        int io = Array.FindIndex(rows, row => row.StartsWith("M16_027_IO ", StringComparison.Ordinal));
        Reject(() => f.Check(w, rows: rows.Where((_, i) => i != io).ToArray()));
        Reject(() => f.Check(w, rows: rows.Take(io + 1).Concat([rows[io]]).Concat(rows.Skip(io + 1)).ToArray()));
        var reordered = rows.ToArray();
        (reordered[io], reordered[io + 1]) = (reordered[io + 1], reordered[io]);
        Reject(() => f.Check(w, rows: reordered));
        Reject(() => f.Check(w, rows: rows.Append("M16_027_NEW value=1").ToArray()), "log_upload_media_unknown_writable_record");
        string[] extra = rows.Append("M16_027_WAKE synthetic=requires-wait-consumer").Append("FUTURE_PARENT_ROW retained=1").ToArray();
        var report = f.Check(w, rows: extra);
        HostCheck.True(report.UnjoinedRows[^2].RecordIndex == rows.Length && report.UnjoinedRows[^1].Text == extra[^1]);
        Reject(() => f.Check(w, rows: [null!]), "log_upload_media_input");
        Reject(() => f.Check(w, rows: Enumerable.Repeat("outer", Q.MaximumWritableDiskRows + 1).ToArray()), "log_upload_media_input");
    });
    [HostTest("log-upload media requires zero new allocation tail independently of logical file extraction")]
    private static void ZeroTail() => WithImages(f =>
    {
        byte[] changed = f.Final.ToArray();
        ulong last = f.TargetSectors[1];
        changed[checked((int)last * 512 + 200)] = 0x5a;
        string path = Path.Combine(f.Folder, "nonzero-tail.raw");
        File.WriteAllBytes(path, changed);
        var actual = Combined.Inspect(path);
        HostCheck.True(actual.Data.Find("LOG.TXT")!.Sha256 == f.FinalInspection.Data.Find("LOG.TXT")!.Sha256);
        var w = f.Write(final: changed);
        Reject(() => f.Check(w, finalBytes: changed, finalInspection: actual), "log_upload_media_target_tail");
    });
    private static void WithImages(Action<Images> action) => CombinedWebDataImageFixtureTests.WithImage((_, folder, _, _, path, receipt) =>
    {
        byte[] before = File.ReadAllBytes(path), after = before.ToArray();
        uint[] log = [100, 102], upload = Enumerable.Range(110, 11).Select(i => (uint)i).ToArray();
        Put("LOG.TXT", log, NativeAotLogUploadContract.ExpectedLog());
        Put("UPLOAD.BIN", upload, NativeAotLogUploadContract.ExpectedUpload());
        foreach (ulong lba in new[]
        {
            receipt.Image.DataFirst + 1,
            receipt.Image.DataFirst + 7
        }

        )
            U32(after, checked((int)lba * 512 + 488), receipt.Image.Data.FreeClusters - 13);
        string final = Path.Combine(folder, "synthetic-written.raw");
        File.WriteAllBytes(final, after);
        var extracted = Combined.Inspect(final);
        action(new(folder, before, after, receipt.Image, extracted));
        void Put(string name, uint[] chain, byte[] bytes)
        {
            int entry = checked((int)receipt.Image.Data.Find(name)!.ShortEntryOffset);
            BinaryPrimitives.WriteUInt16LittleEndian(after.AsSpan(entry + 20), (ushort)(chain[0] >> 16));
            BinaryPrimitives.WriteUInt16LittleEndian(after.AsSpan(entry + 26), (ushort)chain[0]);
            U32(after, entry + 28, (uint)bytes.Length);
            int copied = 0;
            for (int i = 0; i < chain.Length; i++)
            {
                foreach (uint fat in new[]
                {
                    receipt.Image.Data.FirstFatSector,
                    receipt.Image.Data.FirstFatSector + Data.FatSectors
                }

                )
                    U32(after, checked((int)fat * 512 + (int)chain[i] * 4), i == chain.Length - 1 ? 0x0fffffffU : chain[i + 1]);
                int take = Math.Min(512, bytes.Length - copied), offset = checked((int)(receipt.Image.Data.FirstDataSector + chain[i] - 2) * 512);
                after.AsSpan(offset, 512).Clear();
                bytes.AsSpan(copied, take).CopyTo(after.AsSpan(offset));
                copied += take;
            }

            HostCheck.Equal(bytes.Length, copied);
        }
    });
    private static ulong Field(string row, string key) => ulong.Parse(row.Split(' ').Single(part => part.StartsWith(key + "=", StringComparison.Ordinal))[(key.Length + 1)..], CultureInfo.InvariantCulture);
    private static string Set(string row, string key, ulong value) => string.Join(' ', row.Split(' ').Select(part => part.StartsWith(key + "=", StringComparison.Ordinal) ? key + "=" + value.ToString(CultureInfo.InvariantCulture) : part));
    private static void U32(byte[] raw, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(offset), value);
    private static void Reject(Action action, string? reason = null)
    {
        try
        {
            action();
        }
        catch (ToolFailure failure)
        {
            HostCheck.True(reason is null || failure.Message == reason, "unexpected rejection: " + failure.Message);
            return;
        }

        HostCheck.True(false, "expected media rejection: " + reason);
    }
}
