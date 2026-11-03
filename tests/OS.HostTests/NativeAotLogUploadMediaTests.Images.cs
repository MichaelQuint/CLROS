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
    private sealed class Images(string folder, byte[] initial, byte[] final, Combined.Inspection before, Combined.Inspection after)
    {
        internal string Folder = folder;
        internal byte[] InitialBytes = initial, Final = final;
        internal Combined.Inspection Initial = before, FinalInspection = after;
        internal Combined.WebFile Package => Initial.WebFiles.Single(file => file.Path == "/OS/APP.PKG");
        internal ulong[] TargetSectors => FinalInspection.Data.Find("LOG.TXT")!.Extents.Concat(FinalInspection.Data.Find("UPLOAD.BIN")!.Extents).SelectMany(extent => Enumerable.Range(0, (int)extent.Clusters).Select(i => extent.FirstLba + (uint)i)).ToArray();

        internal byte[] Sector(byte[] raw, ulong lba) => raw.AsSpan(checked((int)lba * 512), 512).ToArray();
        internal Wire Write(Action<Wire>? prefix = null, ulong? omit = null, bool flush = true, byte[]? final = null)
        {
            final ??= Final;
            var wire = new Wire(InitialBytes);
            wire.Read(Initial.Data.FirstDataSector);
            prefix?.Invoke(wire);
            for (ulong sector = Combined.DataFirstLba; sector <= Combined.DataLastLba; sector++)
                if (sector != omit && !InitialBytes.AsSpan(checked((int)sector * 512), 512).SequenceEqual(final.AsSpan(checked((int)sector * 512), 512)))
                    wire.Write(sector, Sector(final, sector));
            if (flush)
                wire.Flush();
            foreach (ulong sector in TargetSectors)
                wire.Read(sector);
            return wire;
        }

        internal Q.NativeAotLogUploadMediaReport Check(Wire w, string phase = "write", byte[]? initialBytes = null, Combined.Inspection? initialInspection = null, byte[]? finalBytes = null, Combined.Inspection? finalInspection = null, string[]? rows = null, int? first = null, int? last = null, string? packageHash = null, int? packageBytes = null)
        {
            rows ??= w.Rows;
            bool verify = phase == "verify";
            return Inspect(rows, first ?? 1, last ?? (rows.Length - 2), w.Before, w.After, packageBytes ?? (int)Package.Bytes, packageHash ?? Package.Sha256, phase, initialBytes ?? (verify ? Final : InitialBytes), finalBytes ?? Final, initialInspection ?? (verify ? FinalInspection : Initial), finalInspection ?? FinalInspection);
        }
    }
}
