using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;

namespace OS.Tooling;
/// <summary>Direct Microsoft.Data.Sqlite candidate and host requirement values.
/// No EF Core, native publication, guest storage or durability is admitted here.
/// Linux static linkage and original traced NativeAOT execution remain separate.</summary>

public static partial class NativeAotSqliteContract
{
    public const string Application = "NativeAotSqliteContract", ProviderVersion = "11.0.0-rc.3.26522.148", RawVersion = "3.0.5", EngineVersion = "3.53.4";
    public const string NativePackageSha256 = "5641EB58E88429FD73D09FC294BDAA610A6E99099F4141DE912A5E4AA6D773CA";
    public const string ArchiveDependencyReceiptSha256 = "77B2A06C28E919CDE82383839AA37CDC3A9AB3FC131FD781CA26559B07C4D21B";
    public const string SourceArchiveUrl = "https://www.sqlite.org/2026/sqlite-amalgamation-3530400.zip";
    public const string SourceArchiveSha256 = "1E71DDF93849C6A6ECF58B827C0692073D2DD7EE40196158068F7B29F422E87D";
    public const string SourceArchiveSha3 = "628A44CFE82C66AED1CCBBE85A562D2E33EBE64B3288981ED76285612227934E";
    public const string SourceCsha256 = "B1DD5D74EC7F29055A6684FA06FB3C2F6821C87DD38F9A458DFD2E8A1DB28189";
    public const string SourceHeaderSha256 = "919E7F2E8ED1D8F56AC17B412B8971C76AA5D1A879752CC6058F75E7D5910E1D";
    public const string SourceId = "2026-07-24 19:02:57 bf7c7f30031888f4e796e429ab3978879485813aaca6f641c7b33e4e09459bcc";
    public const int ClrosElfBytes = 9_049_224;
    public const string ClrosElfSha256 = "EC837A4DEE111980636C33173B7B48E387BD2922A887B390FCCC419EE6946C17";
    public const string ClrosApplicationPath = "apps/NativeAotSqliteContract";
    // Deliberately selected candidate envelope, not measured minima, backing
    // reservation, native stack fit or permission to execute a guest.
    public const uint ClrosMemoryBytes = 67_108_864, ClrosStackBytes = 1_572_864, ClrosThreads = 16, ClrosDescriptors = 32;
    /// <summary>Build a new V1 package from only the pinned successful Linux
    /// recovery-r03 ELF. Mode is exactly database, verify-database, recovery-seed,
    /// recovery-spill or recovery-verify; paths are confined contract/recovery
    /// database filenames under /data. Bytes are borrowed synchronously and not
    /// retained. Invalid identity, geometry or mode throws before output returns.
    /// This selects a finite envelope, without instruction/startup admission,
    /// execution, physical FLUSH, recovery or persistence authority.</summary>

    public static byte[] CreateClrosPackage(ReadOnlySpan<byte> elf, string mode) => CreateClrosPackageCore(elf, mode, ClrosElfSha256);
    /// <summary>Inspect exact canonical package bytes, mode, selected envelope
    /// and fixed four-load/TLS ELF. Input must stay stable during the synchronous
    /// borrow. Returned values own immutable metadata only; altered/unknown
    /// packages throw. Actual source/native/image/process holds stay external.</summary>

    public static ClrosPackageValues InspectClrosPackage(ReadOnlySpan<byte> package, string mode) => InspectClrosPackageCore(package, mode, ClrosElfSha256);
    // Portable independent geometry vectors may use an internal expected hash.
    // Public entrypoints always supply the one fixed original identity above.
    internal static byte[] CreateClrosPackageCore(ReadOnlySpan<byte> elf, string mode, string expectedElfSha256)
    {
        string path = ClrosDatabasePath(mode);
        _ = InspectClrosElf(elf, expectedElfSha256);
        byte[] manifest = Encoding.ASCII.GetBytes(ClrosManifest(mode, path, expectedElfSha256));
        byte[] package = new byte[64 + manifest.Length + elf.Length];
        ClrosHeader(manifest.Length).CopyTo(package, 0);
        manifest.CopyTo(package, 64);
        elf.CopyTo(package.AsSpan(64 + manifest.Length));
        return package;
    }

    internal static ClrosPackageValues InspectClrosPackageCore(ReadOnlySpan<byte> package, string mode, string expectedElfSha256)
    {
        string path = ClrosDatabasePath(mode), text = ClrosManifest(mode, path, expectedElfSha256);
        byte[] manifest = Encoding.ASCII.GetBytes(text);
        Need(package.Length == 64 + manifest.Length + ClrosElfBytes && package[..64].SequenceEqual(ClrosHeader(manifest.Length)) && package.Slice(64, manifest.Length).SequenceEqual(manifest), "clros_package");
        var image = InspectClrosElf(package[(64 + manifest.Length)..], expectedElfSha256);
        var view = OS.Manifest.LinuxElfPackage.Validate(package);
        Need(view.Succeeded && view.DataFileCount == 0 && view.PayloadBytes == ClrosElfBytes, "clros_package");
        return new(mode, path, text, package.Length, Convert.ToHexString(SHA256.HashData(package)), image);
    }

    private static unsafe NativeAotPayload.Inspection InspectClrosElf(ReadOnlySpan<byte> elf, string expectedElfSha256)
    {
        Need(elf.Length == ClrosElfBytes, "clros_elf_length");
        NativeAotPayload.Inspection image;
        try
        {
            image = NativeAotPayload.Inspect(elf);
        }
        catch (InvalidDataException)
        {
            throw new ToolFailure("sqlite_contract_clros_elf_geometry");
        }

        NativeAotPayload.Segment[] loads = [new(1, 4, 0, 4_194_304, 748, 748, 4096), new(1, 5, 4096, 4_198_400, 2_633_584, 2_633_584, 4096), new(1, 4, 2_637_824, 6_832_128, 1_338_200, 1_338_200, 4096), new(1, 6, 3_977_448, 8_171_752, 49_504, 603_016, 4096)];
        Need(image.Entry == 4_231_863 && image.ProgramHeaderCount == 11 && image.Loads.SequenceEqual(loads) && image.Tls == new NativeAotPayload.Segment(7, 4, 3_977_448, 8_171_752, 24, 264, 8), "clros_elf_geometry");
        fixed (byte* source = elf)
            Need(OS.Kernel.Compatibility.LinuxX64.LinuxElfLoader.Validate(source, (uint)elf.Length, ClrosMemoryBytes, ClrosStackBytes, out var plan) == OS.Kernel.Compatibility.LinuxX64.LinuxElfLoadStatus.Ready && plan.ImageBytes + ClrosStackBytes <= ClrosMemoryBytes, "clros_elf_envelope");
        Need(image.Sha256 == expectedElfSha256, "clros_elf_identity");
        return image;
    }

    private static string ClrosDatabasePath(string mode) => mode switch
    {
        "database" or "verify-database" => "/data/contract.db",
        "recovery-seed" or "recovery-spill" or "recovery-verify" => "/data/recovery.db",
        _ => throw new ToolFailure("sqlite_contract_clros_mode")
    };
    private static string ClrosManifest(string mode, string path, string digest)
    {
        Need(Hex(digest), "clros_elf_identity");
        // listen is a required V1 manifest field, not a measured SQLite socket
        // or a claim that any network endpoint was opened.
        return "format=clros-linux-elf\nversion=1\nprofile=linux-musl-x64-static-v1\napp.path=" + ClrosApplicationPath + "\nargs=--" + mode + " " + path + "\ndigest=" + digest.ToLowerInvariant() + "\nmemory=67108864\nstack=1572864\nthreads=16\ndescriptors=32\nlisten=0.0.0.0:8080\n";
    }

    private static byte[] ClrosHeader(int manifestBytes)
    {
        byte[] bytes = new byte[64];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes, OS.Manifest.LinuxElfPackage.Magic);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16), 64);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(20), (uint)manifestBytes);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(24), ClrosElfBytes);
        return bytes;
    }

    public static IReadOnlyDictionary<string, string> PackagePins { get; } = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Microsoft.Data.Sqlite"] = ProviderVersion, ["Microsoft.Data.Sqlite.Core"] = ProviderVersion, ["SQLitePCLRaw.bundle_e_sqlite3"] = RawVersion, ["SQLitePCLRaw.config.e_sqlite3"] = RawVersion, ["SQLitePCLRaw.provider.e_sqlite3"] = RawVersion, ["SQLitePCLRaw.core"] = RawVersion, ["SQLite"] = EngineVersion });
    // Exact signed archives independently fetched from NuGet's HTTPS flat
    // container. NuGet's project.assets sha512 is a different content identity;
    // it must not be compared with SHA512 over the complete signed ZIP bytes.
    public static IReadOnlyDictionary<string, string> PackageArchiveSha256 { get; } = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Microsoft.Data.Sqlite"] = "F6826764AF2C265F8BC2E0466A733DB32CED8836280A2FF03AAC07D4C85BA874", ["Microsoft.Data.Sqlite.Core"] = "E9D81166DFF2BC2725425702B31C7999F121A69DB5C61F83D0B58805D9B5F8F2", ["SQLitePCLRaw.bundle_e_sqlite3"] = "E325CE61A86BCA381FBD35487264481E1C6A39663546FEC82035FA661198C2A1", ["SQLitePCLRaw.config.e_sqlite3"] = "1C3908111A4A14E898839836BEA9C4DA390EDC669B297C404DE92422ABEE3AB1", ["SQLitePCLRaw.provider.e_sqlite3"] = "10C6D3DAAC432BB382DDA06C35E5F7FFCCE3618792141BD91656527F06EF5B25", ["SQLitePCLRaw.core"] = "7476B4B8E067D88C4CB598A31893B511037B88E05C28D524F4B4433FE9F69048", ["SQLite"] = NativePackageSha256 });

    /// <summary>Parse borrowed executable-byte rows with the existing complete-
    /// index decoder and a separate SQLite domain. This report may describe a
    /// partial instruction set and grants no lookup, package or guest authority.
    /// Independent executable-section coverage and a reviewed exact digest are
    /// required before a later prepared-program factory can admit this ELF.</summary>

    public static InstructionValues InspectInstructionValues(ReadOnlySpan<byte> elf, string disassembly)
    {
        var index = NativeAotInstructionIndex.Parse(elf, disassembly, "sqlite_instruction_", "CLROS_SQLITE_INSTRUCTIONS_V1\n", 2_000_000, 128 * 1024 * 1024);
        return new(index.Report.InstructionCount, index.Report.SyscallCount, index.Report.Sha256, index.SyscallContinuationsValid);
    }

    /// <summary>Inspect copied Linux x64 proc selectors only. Require one live
    /// traced child with the expected executable, parent and owned session.
    /// Actual proc handles, repeated start identities and a live pidfd must bind
    /// these values inside the original owner before any signal is issued.</summary>

    public static LinuxRecoveryIdentity InspectLinuxRecoveryIdentityValues(int tracer, string tracerStat, string children, string writerStat, string status, string executable, string expected)
    {
        Need(tracer > 0 && new[] { tracerStat, children, writerStat, status, executable, expected }.All(s => s.Length is > 0 and <= 65536), "recovery_proc_input");
        (int Pid, int Parent, int Group, int Session, ulong Start) Stat(string text)
        {
            int first = text.IndexOf('('), last = text.LastIndexOf(')');
            Need(first > 1 && last > first, "recovery_proc_stat");
            string[] fields = text[(last + 1)..].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            Need(fields.Length >= 20 && fields[0] is "R" or "S" or "D" or "T" or "t" && int.TryParse(text[..first].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out _) && fields.Skip(1).Take(3).All(f => int.TryParse(f, NumberStyles.None, CultureInfo.InvariantCulture, out _)) && ulong.TryParse(fields[19], NumberStyles.None, CultureInfo.InvariantCulture, out ulong start) && start > 0, "recovery_proc_stat");
            return (int.Parse(text[..first].Trim(), CultureInfo.InvariantCulture), int.Parse(fields[1], CultureInfo.InvariantCulture), int.Parse(fields[2], CultureInfo.InvariantCulture), int.Parse(fields[3], CultureInfo.InvariantCulture), ulong.Parse(fields[19], CultureInfo.InvariantCulture));
        }

        var parent = Stat(tracerStat);
        var writer = Stat(writerStat);
        string[] ids = children.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var tgids = Regex.Matches(status, @"(?m)^Tgid:[ \t]*(\d+)[ \t]*\r?$", RegexOptions.CultureInvariant);
        var tracers = Regex.Matches(status, @"(?m)^TracerPid:[ \t]*(\d+)[ \t]*\r?$", RegexOptions.CultureInvariant);
        Need(parent.Pid == tracer && parent.Group == tracer && parent.Session == tracer && ids.Length == 1 && ids[0] == writer.Pid.ToString(CultureInfo.InvariantCulture) && writer.Pid != tracer && writer.Parent == tracer && writer.Group == tracer && writer.Session == tracer && tgids.Count == 1 && tgids[0].Groups[1].Value == writer.Pid.ToString(CultureInfo.InvariantCulture) && tracers.Count == 1 && tracers[0].Groups[1].Value == tracer.ToString(CultureInfo.InvariantCulture) && expected.StartsWith('/') && executable == expected, "recovery_proc_owner");
        return new(tracer, writer.Pid, writer.Parent, writer.Group, writer.Session, parent.Start, writer.Start);
    }

    /// <summary>Interpret copied poll event values only. POLLIN identifies an
    /// exited process and POLLHUP a reaped process. This does not bind a pidfd
    /// or prove the death of any original owned process.</summary>

    public static bool InspectRecoveryPidfdPollValues(int count, short returned)
    {
        Need(count is 0 or 1 && (returned & ~(1 | 16)) == 0 && (count == 0) == (returned == 0), "recovery_pidfd_poll");
        return count == 1 && (returned & (1 | 16)) != 0;
    }

    /// <summary>Copy bounded journal-header and first-segment record values.
    /// Check SQLite's big-endian page numbers and sparse checksums against an
    /// independently retained committed baseline, and require a protected page
    /// already changed in the database. Later journal segments are not parsed.
    /// Borrowed bytes cannot establish an absent RESERVED lock, xSync calls,
    /// a hot journal, recovery execution or persistence after power loss.</summary>

    public static RecoveryJournal InspectRecoveryJournalValues(ReadOnlySpan<byte> baseline, ReadOnlySpan<byte> changed, ReadOnlySpan<byte> journal)
    {
        Need(baseline.Length is >= 4096 and <= 2 * 1024 * 1024 && baseline.Length % 4096 == 0 && changed.Length == baseline.Length && baseline[..16].SequenceEqual("SQLite format 3\0"u8) && changed[..16].SequenceEqual("SQLite format 3\0"u8) && BinaryPrimitives.ReadUInt16BigEndian(baseline[16..]) == 4096, "recovery_database_bytes");
        Need(journal.Length is > 512 and <= 8 * 1024 * 1024 && journal[..8].SequenceEqual(new byte[] { 0xd9, 0xd5, 5, 0xf9, 0x20, 0xa1, 0x63, 0xd7 }), "recovery_journal_header");
        uint records = BinaryPrimitives.ReadUInt32BigEndian(journal[8..]), nonce = BinaryPrimitives.ReadUInt32BigEndian(journal[12..]), pages = BinaryPrimitives.ReadUInt32BigEndian(journal[16..]), sector = BinaryPrimitives.ReadUInt32BigEndian(journal[20..]), size = BinaryPrimitives.ReadUInt32BigEndian(journal[24..]);
        Need(size == 4096 && pages == baseline.Length / 4096 && records is > 0 and <= 512 && records <= pages && sector is >= 512 and <= 65536 && (sector & (sector - 1)) == 0 && (ulong)sector + (ulong)records * 4104 <= (ulong)journal.Length, "recovery_journal_segment");
        HashSet<uint> seen = [];
        int dirty = 0;
        for (int index = 0; index < records; index++)
        {
            int at = checked((int)sector + index * 4104);
            uint page = BinaryPrimitives.ReadUInt32BigEndian(journal[at..]);
            Need(page > 0 && page <= pages && seen.Add(page), "recovery_journal_page");
            ReadOnlySpan<byte> data = journal.Slice(at + 4, 4096);
            uint checksum = nonce;
            for (int offset = 3896; offset >= 0; offset -= 200)
                checksum = unchecked(checksum + data[offset]);
            Need(checksum == BinaryPrimitives.ReadUInt32BigEndian(journal[(at + 4100)..]), "recovery_journal_checksum");
            int databaseAt = checked(((int)page - 1) * 4096);
            Need(data.SequenceEqual(baseline.Slice(databaseAt, 4096)), "recovery_journal_baseline");
            if (!data.SequenceEqual(changed.Slice(databaseAt, 4096)))
                dirty++;
        }

        Need(dirty > 0, "recovery_spill_not_observed");
        return new((int)records, dirty, (int)pages, (int)sector, Convert.ToHexString(SHA256.HashData(baseline)), Convert.ToHexString(SHA256.HashData(changed)), Convert.ToHexString(SHA256.HashData(journal)));
    }

    /// <summary>Independently walk copied modern 3.53.4 DELETE/FULL journal
    /// segments for this no-ATTACH, 4096-byte-page, fixed-size UPDATE fixture.
    /// Require finite positive valid-header counts, consistent header fields,
    /// sector alignment, complete checksummed original pages and no duplicates.
    /// A final full zero-magic/zero-count header may have complete original-page
    /// records; report that shape separately without playback eligibility.
    /// Bounds: 2 MiB database, 8 MiB journal, 512 pages/segments. Reject partial
    /// structures, legacy/no-sync/SAFE_APPEND counts, super-journal trailers,
    /// growth, WAL and conflicting originals. A structurally valid shorter
    /// journal needs original capture hashes to detect whole-segment deletion.
    /// Borrowed bytes grant no origin, lock, sync, ordering or recovery proof.</summary>

    public static RollbackSegments InspectRollbackJournalSegmentValues(ReadOnlySpan<byte> baseline, ReadOnlySpan<byte> changed, ReadOnlySpan<byte> journal)
    {
        Need(baseline.Length is >= 4096 and <= 2 * 1024 * 1024 && baseline.Length % 4096 == 0 && changed.Length == baseline.Length && baseline[..16].SequenceEqual("SQLite format 3\0"u8) && changed[..16].SequenceEqual("SQLite format 3\0"u8) && BinaryPrimitives.ReadUInt16BigEndian(baseline[16..]) == 4096 && BinaryPrimitives.ReadUInt16BigEndian(changed[16..]) == 4096 && baseline[18] == 1 && baseline[19] == 1 && changed[18] == 1 && changed[19] == 1, "segments_database");
        Need(journal.Length is > 512 and <= 8 * 1024 * 1024, "segments_capacity");
        int pages = baseline.Length / 4096;
        Need(BinaryPrimitives.ReadUInt32BigEndian(baseline[28..]) == pages, "segments_database_pages");
        ReadOnlySpan<byte> magic = [0xd9, 0xd5, 5, 0xf9, 0x20, 0xa1, 0x63, 0xd7];
        List<RollbackSegment> valid = [];
        RollbackSegment? tail = null;
        Dictionary<uint, int> seen = [];
        int at = 0, sector = 0;
        while (at < journal.Length)
        {
            Need(valid.Count < 512 && journal.Length - at >= 28, "segments_header_truncated");
            ReadOnlySpan<byte> header = journal.Slice(at, 28);
            uint records = BinaryPrimitives.ReadUInt32BigEndian(header[8..]), nonce = BinaryPrimitives.ReadUInt32BigEndian(header[12..]), original = BinaryPrimitives.ReadUInt32BigEndian(header[16..]), headerSector = BinaryPrimitives.ReadUInt32BigEndian(header[20..]), size = BinaryPrimitives.ReadUInt32BigEndian(header[24..]);
            Need(original == pages && size == 4096 && headerSector is >= 512 and <= 65536 && (headerSector & (headerSector - 1)) == 0 && (sector == 0 || headerSector == sector), "segments_header_consistency");
            sector = (int)headerSector;
            Need(journal.Length - at >= sector, "segments_header_truncated");
            bool initializedTail = header[..12].IndexOfAnyExcept((byte)0) < 0;
            Need(header[..8].SequenceEqual(magic) || initializedTail, "segments_header_magic");
            int count;
            if (initializedTail)
            {
                Need(valid.Count > 0, "segments_initial_tail");
                int remaining = journal.Length - at - sector;
                Need(remaining % 4104 == 0, "segments_tail_truncated");
                count = remaining / 4104;
            }
            else
            {
                Need(records is > 0 and <= 512 && records <= pages, "segments_record_count");
                count = (int)records;
                Need((long)at + sector + (long)count * 4104 <= journal.Length, "segments_records_truncated");
            }

            Need(count <= pages && seen.Count + count <= 512, "segments_record_capacity");
            List<RollbackPage> values = [];
            for (int record = 0; record < count; record++)
            {
                int offset = at + sector + record * 4104;
                uint page = BinaryPrimitives.ReadUInt32BigEndian(journal[offset..]);
                Need(page > 0 && page <= pages, "segments_page_number");
                ReadOnlySpan<byte> data = journal.Slice(offset + 4, 4096);
                uint checksum = nonce;
                for (int index = 4096 - 200; index > 0; index -= 200)
                    checksum = unchecked(checksum + data[index]);
                Need(checksum == BinaryPrimitives.ReadUInt32BigEndian(journal[(offset + 4100)..]), "segments_checksum");
                if (seen.TryGetValue(page, out int earlier))
                {
                    Need(data.SequenceEqual(journal.Slice(earlier + 4, 4096)), "segments_duplicate_conflict");
                    throw new ToolFailure("sqlite_contract_segments_duplicate_page");
                }

                int databaseAt = checked(((int)page - 1) * 4096);
                Need(data.SequenceEqual(baseline.Slice(databaseAt, 4096)), "segments_original_page");
                seen.Add(page, offset);
                values.Add(new(offset, page, Convert.ToHexString(SHA256.HashData(data)), !data.SequenceEqual(changed.Slice(databaseAt, 4096))));
            }

            var segment = new RollbackSegment(at, nonce, Array.AsReadOnly(values.ToArray()));
            if (initializedTail)
            {
                tail = segment;
                at = journal.Length;
                break;
            }

            valid.Add(segment);
            int end = at + sector + count * 4104;
            if (end == journal.Length)
            {
                at = end;
                break;
            }

            int next = checked((end + sector - 1) / sector * sector);
            Need(next <= journal.Length, "segments_alignment_truncated");
            Need(journal.Slice(end, next - end).IndexOfAnyExcept((byte)0) < 0, "segments_alignment_padding");
            at = next;
        }

        Need(valid.Count > 0, "segments_empty");
        int changes = 0;
        for (int page = 0; page < pages; page++)
            if (!baseline.Slice(page * 4096, 4096).SequenceEqual(changed.Slice(page * 4096, 4096)))
                changes++;
        return new(pages, sector, Array.AsReadOnly(valid.ToArray()), tail, changes, Convert.ToHexString(SHA256.HashData(baseline)), Convert.ToHexString(SHA256.HashData(changed)), Convert.ToHexString(SHA256.HashData(journal)));
    }

    /// <summary>Borrow the exact official release ZIP. Require its independently
    /// downloaded SHA256 and SQLite-published SHA3-256, then exact C/header
    /// identities and upstream source ID. Copy only those two named bounded
    /// members; no archive pathname is used as a destination or executed.
    /// Callers may write copies only to ignored external-build artifacts.</summary>

    public static UpstreamSource InspectSourceArchiveValues(ReadOnlySpan<byte> bytes)
    {
        Need(bytes.Length == 2946650 && Convert.ToHexString(SHA256.HashData(bytes)) == SourceArchiveSha256, "source_archive_hash");
        Need(SHA3_256.IsSupported && Convert.ToHexString(SHA3_256.HashData(bytes)) == SourceArchiveSha3, "source_archive_sha3");
        using var stream = new MemoryStream(bytes.ToArray(), false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        Need(archive.Entries.Count == 5, "source_archive_inventory");
        byte[] Read(string name, int size, string expected)
        {
            var matches = archive.Entries.Where(e => e.FullName == "sqlite-amalgamation-3530400/" + name).ToArray();
            Need(matches.Length == 1 && matches[0].Length == size, "source_member");
            byte[] result = new byte[size];
            using var input = matches[0].Open();
            input.ReadExactly(result);
            Need(input.ReadByte() == -1 && Convert.ToHexString(SHA256.HashData(result)) == expected, "source_member_hash");
            return result;
        }

        byte[] source = Read("sqlite3.c", 9515341, SourceCsha256), header = Read("sqlite3.h", 690838, SourceHeaderSha256);
        string text = Encoding.UTF8.GetString(header);
        Need(text.Contains("#define SQLITE_VERSION        \"3.53.4\"", StringComparison.Ordinal) && text.Contains("#define SQLITE_SOURCE_ID      \"" + SourceId + "\"", StringComparison.Ordinal), "source_version");
        return new(source, header);
    }

    /// <summary>Read P/Invoke metadata from the exact signed net8 provider ZIP,
    /// without loading or executing its assembly. Require all imported native
    /// modules to be e_sqlite3 and return copied distinct sqlite3 entry points.
    /// This measures the whole provider import surface, including methods the
    /// finite fixture may not invoke; linked code and symbol proof remain separate.</summary>

    public static IReadOnlyList<string> InspectProviderImports(ReadOnlySpan<byte> package)
    {
        Need(package.Length is > 0 and <= 1024 * 1024 && Convert.ToHexString(SHA256.HashData(package)) == PackageArchiveSha256["SQLitePCLRaw.provider.e_sqlite3"], "provider_package_hash");
        using var zipStream = new MemoryStream(package.ToArray(), false);
        using var zip = new ZipArchive(zipStream, ZipArchiveMode.Read);
        var entries = zip.Entries.Where(e => e.FullName == "lib/net8.0/SQLitePCLRaw.provider.e_sqlite3.dll").ToArray();
        Need(entries.Length == 1 && entries[0].Length is > 0 and <= 1024 * 1024, "provider_assembly");
        using var stream = new MemoryStream();
        using (var input = entries[0].Open())
            input.CopyTo(stream);
        stream.Position = 0;
        using var pe = new PEReader(stream);
        Need(pe.HasMetadata, "provider_metadata");
        var metadata = System.Reflection.Metadata.PEReaderExtensions.GetMetadataReader(pe);
        SortedSet<string> imports = new(StringComparer.Ordinal);
        foreach (var handle in metadata.MethodDefinitions)
        {
            var method = metadata.GetMethodDefinition(handle);
            if ((method.Attributes & MethodAttributes.PinvokeImpl) == 0)
                continue;
            var import = method.GetImport();
            string module = metadata.GetString(metadata.GetModuleReference(import.Module).Name), name = metadata.GetString(import.Name);
            Need(module == "e_sqlite3" && name.Length is > 8 and <= 128 && name.StartsWith("sqlite3", StringComparison.Ordinal) && name.All(c => char.IsAsciiLetterOrDigit(c) || c == '_') && imports.Count < 512, "provider_import");
            imports.Add(name);
        }

        Need(imports.Contains("sqlite3_open_v2") && imports.Contains("sqlite3_step") && imports.Contains("sqlite3_close_v2"), "provider_import_missing");
        return Array.AsReadOnly(imports.ToArray());
    }

    private static readonly string[] CompilerTools = ["dotnet", "gcc", "clang", "ld", "ar", "readelf", "nm", "strace", "setsid"];
    private static readonly string[] CompilerLibraries = ["libc.a", "libm.a", "libz.a", "libstdc++.a", "libgcc.a", "libgcc_eh.a", "crt1.o", "crti.o", "crtn.o"];
    private static readonly string[] DependencyArtifacts = ["commands.json", "round0/sqlite3.o", "round1/sqlite3.o", "libe_sqlite3.a", "libe_sqlite3-repeat.a"];
    /// <summary>Open the fixed nine dependency inputs in a caller-owned holder,
    /// checking a bounded successful receipt, its expected hash and each actual
    /// output hash. Return a read-only name map borrowing that holder's files.
    /// On success or failure the caller must retain/dispose the holder; a nested
    /// publisher has no ownership of these handles. VerifyUnchanged is still
    /// required through the outer consumer's last gate. This checks local file
    /// identity/content only, not pinned sources, recipes, compiler compatibility
    /// or execution authority; native reuse separately requires all those checks.</summary>

    public static IReadOnlyDictionary<string, AuthorityFile> HoldArchiveDependencyInputs(AuthorityFiles held, string directory, string expectedReceiptSha256)
    {
        Need(held is not null && expectedReceiptSha256 is not null && Hex(expectedReceiptSha256), "dependency_receipt_hash");
        directory = Path.GetFullPath(directory);
        Dictionary<string, AuthorityFile> files = new(StringComparer.Ordinal);
        var receipt = held.Open(directory, Path.Combine(directory, "receipt.json"), "original archive receipt", "sqlite_archive_dependency", 2 * 1024 * 1024, singleLink: true);
        files.Add("receipt.json", receipt);
        Need(receipt.Sha256.Equals(expectedReceiptSha256, StringComparison.OrdinalIgnoreCase), "dependency_receipt_hash");
        try
        {
            using var original = DependencyJson(receipt.ReadBytes(2 * 1024 * 1024, "sqlite_archive_dependency"));
            var value = original.RootElement;
            Need(value.GetProperty("Schema").GetString() is "clros-sqlite-linux-reference-v1" or "clros-sqlite-linux-reference-v2" && value.GetProperty("Succeeded").ValueKind == JsonValueKind.True && value.GetProperty("Failure").GetString() == "", "dependency_original_failed");
            var outputs = DependencyMap(value.GetProperty("Outputs"), 512);
            foreach (string relative in DependencyArtifacts.Concat(new[] { "upstream/sqlite3.c", "upstream/sqlite3.h", "upstream/sqlite-amalgamation-3530400.zip" }))
            {
                var file = held.Open(directory, Path.Combine(directory, relative), "original archive dependency", "sqlite_archive_dependency", relative == "commands.json" ? 2 * 1024 * 1024 : 16 * 1024 * 1024, singleLink: true);
                files.Add(relative, file);
                Need(outputs.TryGetValue(relative, out string? hash) && file.Sha256.Equals(hash, StringComparison.OrdinalIgnoreCase), "dependency_output_hash");
            }

            AuthorityFiles.RequireDistinct(files.Values.ToArray());
            held.VerifyUnchanged();
            return new ReadOnlyDictionary<string, AuthorityFile>(files);
        }
        catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException or FormatException or OverflowException)
        {
            throw new ToolFailure("sqlite_contract_dependency_json");
        }
    }

    /// <summary>Synchronously borrow bounded copied receipt/command/object/archive
    /// bytes. Authenticate their hash associations, two identical objects and
    /// archives, actual sole archive member, exact normalized gcc/ar recipes,
    /// pinned upstream source declarations, provider imports and current compiler
    /// input facts. Return immutable copied values, retaining no input buffers.
    /// This does not open files or execute anything. The native owner must hold
    /// and verify the actual upstream files and all dependencies through finish.</summary>

    public static ArchiveDependency InspectArchiveDependencyValues(ReadOnlySpan<byte> receipt, string expectedReceiptSha256, IReadOnlyDictionary<string, byte[]> artifacts, IReadOnlyList<string> imports, IReadOnlyDictionary<string, CompilerInput> compilerInputs)
    {
        try
        {
            return InspectArchiveDependency(receipt, expectedReceiptSha256, artifacts, imports, compilerInputs);
        }
        catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException or FormatException or OverflowException)
        {
            throw new ToolFailure("sqlite_contract_dependency_json");
        }
    }

    private static ArchiveDependency InspectArchiveDependency(ReadOnlySpan<byte> receipt, string expectedReceiptSha256, IReadOnlyDictionary<string, byte[]> artifacts, IReadOnlyList<string> imports, IReadOnlyDictionary<string, CompilerInput> compilerInputs)
    {
        Need(receipt.Length is > 0 and <= 2 * 1024 * 1024 && Hex(expectedReceiptSha256) && Convert.ToHexString(SHA256.HashData(receipt)) == expectedReceiptSha256, "dependency_receipt_hash");
        Need(artifacts is not null && artifacts.Count == DependencyArtifacts.Length && DependencyArtifacts.All(artifacts.ContainsKey), "dependency_inventory");
        Need(imports is { Count: > 0 and <= 512 } && imports.Distinct(StringComparer.Ordinal).Count() == imports.Count, "dependency_imports");
        string[] roles = CompilerTools.Concat(CompilerLibraries).ToArray();
        Need(compilerInputs is not null && compilerInputs.Count == roles.Length && roles.All(compilerInputs.ContainsKey), "dependency_compiler_inventory");
        foreach (var pair in artifacts)
            Need(pair.Value is not null && pair.Value.Length > 0 && pair.Value.Length <= (pair.Key == "commands.json" ? 2 * 1024 * 1024 : 16 * 1024 * 1024), "dependency_artifact_size");
        using var original = DependencyJson(receipt);
        var root = original.RootElement;
        Need(root.GetProperty("Schema").GetString() is "clros-sqlite-linux-reference-v1" or "clros-sqlite-linux-reference-v2" && root.GetProperty("Succeeded").ValueKind == JsonValueKind.True && root.GetProperty("Failure").GetString() == "", "dependency_original_failed");
        Need(root.GetProperty("SourceArchiveSha256").GetString() == SourceArchiveSha256 && root.GetProperty("SourceArchiveSha3").GetString() == SourceArchiveSha3 && root.GetProperty("SourceId").GetString() == SourceId, "dependency_source");
        var provider = root.GetProperty("Imports");
        Need(provider.ValueKind == JsonValueKind.Array && provider.GetArrayLength() == imports.Count && provider.EnumerateArray().Select(p => p.GetString()).SequenceEqual(imports), "dependency_imports");
        var outputs = DependencyMap(root.GetProperty("Outputs"), 512);
        var native = DependencyMap(root.GetProperty("Native"), 4096);
        foreach (var pair in artifacts)
            Need(outputs.TryGetValue(pair.Key, out string? hash) && hash == Convert.ToHexString(SHA256.HashData(pair.Value)), "dependency_output_hash");
        foreach (var pair in new[]
        {
            ("upstream/sqlite3.c", SourceCsha256),
            ("upstream/sqlite3.h", SourceHeaderSha256),
            ("upstream/sqlite-amalgamation-3530400.zip", SourceArchiveSha256)
        }

        )
            Need(outputs.TryGetValue(pair.Item1, out string? hash) && hash == pair.Item2, "dependency_source");
        Dictionary<string, CompilerInput> copiedInputs = new(StringComparer.Ordinal);
        foreach (string role in roles)
        {
            var input = compilerInputs[role];
            Need(input is not null && DependencyLinuxPath(input.Path) && Hex(input.Sha256) && native.TryGetValue(input.Path, out string? hash) && hash == input.Sha256, "dependency_compiler_changed");
            copiedInputs.Add(role, new(input!.Path, input.Sha256));
        }

        byte[] object0 = artifacts["round0/sqlite3.o"], object1 = artifacts["round1/sqlite3.o"], archive0 = artifacts["libe_sqlite3.a"], archive1 = artifacts["libe_sqlite3-repeat.a"];
        Need(object0.AsSpan().SequenceEqual(object1) && archive0.AsSpan().SequenceEqual(archive1), "dependency_repeat");
        var archive = InspectStaticArchiveValues(archive0);
        var declared = root.GetProperty("Archive");
        DependencyUnique(declared);
        Need(archive.ObjectFiles == 1 && declared.GetProperty("Sha256").GetString() == archive.Sha256 && declared.GetProperty("Bytes").GetInt32() == archive.Bytes && declared.GetProperty("ObjectFiles").GetInt32() == 1 && declared.GetProperty("DefinedSqliteSymbols").EnumerateArray().Select(p => p.GetString()).SequenceEqual(archive.DefinedSqliteSymbols), "dependency_archive");
        int at = 8;
        bool matched = false;
        while (at < archive0.Length)
        {
            string name = Encoding.ASCII.GetString(archive0.AsSpan(at, 16)).Trim();
            int size = int.Parse(Encoding.ASCII.GetString(archive0.AsSpan(at + 48, 10)).Trim(), CultureInfo.InvariantCulture);
            if (name is not ("/" or "//" or "/SYM64/"))
            {
                Need(!matched && archive0.AsSpan(at + 60, size).SequenceEqual(object0), "dependency_object_member");
                matched = true;
            }

            at += 60 + size + (size & 1);
        }

        Need(matched, "dependency_object_member");
        using var planned = DependencyJson(artifacts["commands.json"]);
        var commands = planned.RootElement;
        Need(commands.ValueKind == JsonValueKind.Array && commands.GetArrayLength() is > 0 and <= 128, "dependency_commands");
        Dictionary<string, (JsonElement Value, int Index)> selected = new(StringComparer.Ordinal);
        int index = 0;
        foreach (var command in commands.EnumerateArray())
        {
            DependencyUnique(command);
            string? log = command.GetProperty("log").GetString();
            Need(log is { Length: > 0 and <= 128 }, "dependency_commands");
            if (log!.StartsWith("compile-r", StringComparison.Ordinal) || log.StartsWith("archive-r", StringComparison.Ordinal))
                Need((log is "compile-r0" or "compile-r1" or "archive-r0" or "archive-r1") && selected.TryAdd(log, (command, index)), "dependency_commands");
            index++;
        }

        Need(selected.Count == 4 && selected["compile-r0"].Index < selected["archive-r0"].Index && selected["archive-r0"].Index < selected["compile-r1"].Index && selected["compile-r1"].Index < selected["archive-r1"].Index, "dependency_commands");
        string[] Arguments(JsonElement command)
        {
            var args = command.GetProperty("arguments");
            Need(args.ValueKind == JsonValueKind.Array && args.GetArrayLength() is > 0 and <= 128, "dependency_recipe");
            return args.EnumerateArray().Select(p =>
            {
                string? value = p.GetString();
                Need(value is { Length: > 0 and <= 8192 } && !value.Any(char.IsControl), "dependency_recipe");
                return value!;
            }).ToArray();
        }

        var first = selected["archive-r0"].Value;
        string[] firstArgs = Arguments(first);
        const string suffix = "/libe_sqlite3.a";
        Need(firstArgs.Length == 3 && firstArgs[1].EndsWith(suffix, StringComparison.Ordinal) && DependencyLinuxPath(firstArgs[1]), "dependency_recipe");
        string directory = firstArgs[1][..^suffix.Length], working = first.GetProperty("workingDirectory").GetString() ?? "";
        Need(DependencyLinuxPath(working) && directory.StartsWith(working + "/artifacts/", StringComparison.Ordinal), "dependency_recipe");
        Need(native.TryGetValue(directory + "/upstream/sqlite3.c", out string? sourceHash) && sourceHash == SourceCsha256 && native.TryGetValue(directory + "/upstream/sqlite3.h", out string? headerHash) && headerHash == SourceHeaderSha256, "dependency_source");
        string[] flags = StaticCompileArguments(Path.GetFullPath("sqlite-dependency-source.c"), Path.GetFullPath("sqlite-dependency-object.o"), imports).Skip(4).ToArray();
        for (int round = 0; round < 2; round++)
        {
            string obj = directory + "/round" + round + "/sqlite3.o", lib = directory + (round == 0 ? "/libe_sqlite3.a" : "/libe_sqlite3-repeat.a");
            var compile = selected["compile-r" + round].Value;
            var pack = selected["archive-r" + round].Value;
            Need(compile.GetProperty("executable").GetString() == "gcc" && pack.GetProperty("executable").GetString() == "ar" && compile.GetProperty("workingDirectory").GetString() == working && pack.GetProperty("workingDirectory").GetString() == working && Arguments(compile).SequenceEqual(new[] { "-c", directory + "/upstream/sqlite3.c", "-o", obj }.Concat(flags)) && Arguments(pack).SequenceEqual(new[] { "crD", lib, obj }), "dependency_recipe");
            Need(native.TryGetValue(obj, out string? objectHash) && objectHash == Convert.ToHexString(SHA256.HashData(object0)) && native.TryGetValue(lib, out string? archiveHash) && archiveHash == archive.Sha256, "dependency_native_output");
        }

        return new(archive, expectedReceiptSha256, Convert.ToHexString(SHA256.HashData(artifacts["commands.json"])), Convert.ToHexString(SHA256.HashData(object0)), new ReadOnlyDictionary<string, CompilerInput>(copiedInputs));
    }

    private static bool DependencyLinuxPath(string path) => path.Length is > 1 and <= 4096 && path.StartsWith('/') && !path.Any(char.IsControl) && path.Split('/').Skip(1).All(p => p.Length > 0 && p is not "." and not "..");
    private static void DependencyUnique(JsonElement value)
    {
        Need(value.ValueKind == JsonValueKind.Object && value.EnumerateObject().Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() == value.EnumerateObject().Count(), "dependency_json");
    }

    private static JsonDocument DependencyJson(ReadOnlySpan<byte> bytes)
    {
        JsonDocument? result = null;
        try
        {
            result = JsonDocument.Parse(bytes.ToArray(), new JsonDocumentOptions { MaxDepth = 32 });
            if (result.RootElement.ValueKind == JsonValueKind.Object)
                DependencyUnique(result.RootElement);
            return result;
        }
        catch (JsonException)
        {
            throw new ToolFailure("sqlite_contract_dependency_json");
        }
        catch
        {
            result?.Dispose();
            throw;
        }
    }

    private static Dictionary<string, string> DependencyMap(JsonElement value, int maximum)
    {
        DependencyUnique(value);
        Need(value.EnumerateObject().Count() <= maximum, "dependency_json");
        Dictionary<string, string> result = new(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
        {
            string? hash = property.Value.GetString();
            Need(property.Name.Length is > 0 and <= 8192 && hash is not null && Hex(hash), "dependency_json");
            result.Add(property.Name, hash!);
        }

        return result;
    }

    /// <summary>Plan one deterministic musl/x86-64-v2 object build. Preserve
    /// serialized SQLite and default upstream Unix VFS. Enable optional exports
    /// only when required by the measured provider imports; never replace locks,
    /// sync or VFS behavior with host-only stubs. Output is a value plan.</summary>

    public static IReadOnlyList<string> StaticCompileArguments(string source, string output, IReadOnlyList<string> imports)
    {
        Need(Path.IsPathFullyQualified(source) && Path.IsPathFullyQualified(output) && source != output && imports is { Count: > 0 and <= 512 }, "compile_input");
        List<string> args = ["-c", source, "-o", output, "-O2", "-fPIC", "-ffunction-sections", "-fdata-sections", "-march=x86-64-v2", "-mtune=generic", "-DSQLITE_THREADSAFE=1"];
        void Feature(string define, params string[] names)
        {
            if (names.Any(imports.Contains))
                args.Add("-D" + define + "=1");
        }

        Feature("SQLITE_ENABLE_COLUMN_METADATA", "sqlite3_column_database_name", "sqlite3_column_table_name", "sqlite3_column_origin_name");
        Feature("SQLITE_ENABLE_PREUPDATE_HOOK", "sqlite3_preupdate_hook", "sqlite3_preupdate_count", "sqlite3_preupdate_depth", "sqlite3_preupdate_old", "sqlite3_preupdate_new");
        Feature("SQLITE_ENABLE_SNAPSHOT", "sqlite3_snapshot_get", "sqlite3_snapshot_open", "sqlite3_snapshot_free", "sqlite3_snapshot_cmp", "sqlite3_snapshot_recover");
        Feature("SQLITE_ENABLE_STMT_SCANSTATUS", "sqlite3_stmt_scanstatus", "sqlite3_stmt_scanstatus_v2", "sqlite3_stmt_scanstatus_reset");
        Feature("SQLITE_ENABLE_UNLOCK_NOTIFY", "sqlite3_unlock_notify");
        if (imports.Any(n => n.StartsWith("sqlite3session_", StringComparison.Ordinal) || n.StartsWith("sqlite3changeset_", StringComparison.Ordinal)))
        {
            args.Add("-DSQLITE_ENABLE_SESSION=1");
            if (!args.Contains("-DSQLITE_ENABLE_PREUPDATE_HOOK=1"))
                args.Add("-DSQLITE_ENABLE_PREUPDATE_HOOK=1");
        }

        return Array.AsReadOnly(args.ToArray());
    }

    /// <summary>Require every supplied import to be defined by the admitted
    /// archive. Metadata may include unused SQLCipher methods; callers must not
    /// mistake that superset for the retained NativeAOT object's actual imports.
    /// This does not replace final ELF/link or execution checks.</summary>

    public static void RequireProviderSymbols(Archive archive, IReadOnlyList<string> imports)
    {
        Need(archive is not null && imports is { Count: > 0 and <= 512 }, "provider_symbols_input");
        string[] missing = imports.Where(n => !archive!.DefinedSqliteSymbols.Contains(n, StringComparer.Ordinal)).Distinct().ToArray();
        if (missing.Length != 0)
            throw new ToolFailure("sqlite_contract_provider_symbols_missing", string.Join(',', missing.Take(16)));
    }

    /// <summary>Copy actual undefined sqlite3 symbols from one original nm -u
    /// NativeAOT managed object. Require known provider entries and the finite
    /// fixture's core imports. Metadata-only optional APIs are not invented as
    /// retained imports. Supplied text grants no object or linker authority.</summary>

    public static IReadOnlyList<string> InspectRetainedImportValues(string nm, IReadOnlyList<string> providerImports)
    {
        Need(nm.Length is > 0 and <= 16 * 1024 * 1024 && providerImports is { Count: > 0 and <= 512 }, "retained_import_input");
        SortedSet<string> result = new(StringComparer.Ordinal);
        int rows = 0;
        foreach (string row in nm.Replace("\r\n", "\n").Split('\n'))
        {
            Need(++rows <= 200000 && row.Length <= 8192, "retained_import_capacity");
            var match = Regex.Match(row, @"^\s*U (sqlite3[A-Za-z0-9_]+)\s*$", RegexOptions.CultureInvariant);
            if (!match.Success)
                continue;
            string name = match.Groups[1].Value;
            Need(providerImports.Contains(name, StringComparer.Ordinal), "retained_import_unknown");
            result.Add(name);
        }

        Need(new[] { "sqlite3_open_v2", "sqlite3_close_v2", "sqlite3_prepare_v2", "sqlite3_step" }.All(result.Contains), "retained_import_missing");
        return Array.AsReadOnly(result.ToArray());
    }

    /// <summary>Classify final nm -u -P output. Allow explicitly reported weak
    /// toolchain references only; reject every strong undefined symbol and any
    /// unresolved SQLite reference, including weak ones. ELF loader/dynamic and
    /// relocation checks remain independent; supplied text is value-only.</summary>

    public static IReadOnlyList<WeakUndefined> InspectFinalUndefinedValues(string nm)
    {
        Need(nm.Length <= 1024 * 1024, "final_undefined_size");
        List<WeakUndefined> weak = [];
        foreach (string line in nm.Replace("\r\n", "\n").Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            string[] fields = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            Need(fields.Length == 2 && fields[0].Length is > 0 and <= 255 && fields[1] is "w" or "v" && weak.Count < 256, "final_undefined_strong");
            Need(!fields[0].StartsWith("sqlite3", StringComparison.Ordinal), "final_undefined_sqlite");
            weak.Add(new(fields[0], fields[1][0]));
        }

        return Array.AsReadOnly(weak.OrderBy(w => w.Name, StringComparer.Ordinal).ToArray());
    }

    /// <summary>Validate the unchanged original socket publication recipe through
    /// the existing publisher, changing only this app/output and adding the
    /// audited archive and no-restore after owned RID restore. Retain unstripped
    /// symbols and the ILC map explicitly for linkage review. DirectPInvoke is
    /// declared in the candidate project. No process is started.</summary>

    public static IReadOnlyList<string> StaticPublishArguments(string originalCommands, string destination, string archive)
    {
        Need(Path.IsPathFullyQualified(archive) && archive.EndsWith(".a", StringComparison.Ordinal), "publish_archive");
        string[] args = NativeAotStaticFilePublisher.PublishArguments(originalCommands, destination);
        args[1] = "apps/" + Application + "/" + Application + ".csproj";
        return Array.AsReadOnly([.. args, "--no-restore", "-p:SqliteNativeArchive=" + archive, "-p:StripSymbols=false", "-p:IlcGenerateMapFile=true"]);
    }

    /// <summary>Borrow bounded project.assets.json values and require the exact
    /// seven direct/provider/native package pins. Reject EF or unrecognized
    /// SQLite packages anywhere in the closure. Return copied musl native paths;
    /// paths and package hashes are observations, not held-file authority.</summary>

    public static Dependencies InspectDependencyValues(ReadOnlySpan<byte> assets)
    {
        Need(assets.Length is > 0 and <= 32 * 1024 * 1024, "assets_size");
        using var json = JsonDocument.Parse(assets.ToArray(), new()
        {
            MaxDepth = 32
        });
        var root = json.RootElement;
        List<Package> packages = [];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        SortedSet<string> native = new(StringComparer.Ordinal);
        int libraries = 0;
        foreach (var property in root.GetProperty("libraries").EnumerateObject())
        {
            Need(++libraries <= 1024, "library_count");
            string[] identity = property.Name.Split('/');
            Need(identity.Length == 2, "package_identity");
            string name = identity[0];
            Need(!name.Contains("EntityFramework", StringComparison.OrdinalIgnoreCase), "ef_core_forbidden");
            bool sqlite = name.Contains("sqlite", StringComparison.OrdinalIgnoreCase);
            if (!sqlite)
                continue;
            Need(PackagePins.TryGetValue(name, out string? version) && identity[1] == version && seen.Add(name) && property.Value.GetProperty("type").GetString() == "package", "package_pin");
            string hash = property.Value.GetProperty("sha512").GetString() ?? "";
            Need(Convert.TryFromBase64String(hash, new byte[64], out int written) && written == 64, "package_hash");
            packages.Add(new(name, identity[1], hash));
        }

        Need(seen.Count == PackagePins.Count, "package_missing");
        foreach (var target in root.GetProperty("targets").EnumerateObject())
            foreach (var package in target.Value.EnumerateObject())
                foreach (string section in new[]
                {
                    "native",
                    "runtimeTargets"
                }

                )
                    if (package.Value.TryGetProperty(section, out var values))
                        foreach (var file in values.EnumerateObject())
                        {
                            string path = file.Name;
                            Need(path.Length is > 0 and <= 512 && !path.StartsWith('/') && !path.Contains('\\') && path.Split('/').All(s => s.Length > 0 && s is not ("." or "..")), "native_path");
                            if (path.StartsWith("runtimes/linux-musl-x64/native/", StringComparison.Ordinal))
                            {
                                Need(native.Count < 64, "native_count");
                                native.Add(path);
                            }
                        }

        Need(native.Contains("runtimes/linux-musl-x64/native/libe_sqlite3.so"), "musl_native_missing");
        return new(Array.AsReadOnly(packages.OrderBy(p => p.Name, StringComparer.Ordinal).ToArray()), Array.AsReadOnly(native.ToArray()));
    }

    /// <summary>Borrow an explicit ordinary ar archive. Every object must be
    /// ELF64 little-endian x86-64 ET_REL with bounded valid symbol/string tables.
    /// Require defined global SQLite entry points, rejecting shared libraries,
    /// thin archives and incomplete substitutes. This structural check cannot
    /// prove complete provider imports, libc closure, compiler provenance or a
    /// linked static executable; original link/nm/readelf evidence is required.</summary>

    public static Archive InspectStaticArchiveValues(ReadOnlySpan<byte> bytes)
    {
        Need(bytes.Length is >= 68 and <= 64 * 1024 * 1024 && bytes[..8].SequenceEqual("!<arch>\n"u8), "static_archive_required");
        int at = 8, objects = 0, members = 0;
        SortedSet<string> symbols = new(StringComparer.Ordinal);
        while (at < bytes.Length)
        {
            Need(++members <= 2048 && bytes.Length - at >= 60 && bytes.Slice(at + 58, 2).SequenceEqual("`\n"u8), "archive_header");
            string name = Encoding.ASCII.GetString(bytes.Slice(at, 16)).Trim();
            Need(int.TryParse(Encoding.ASCII.GetString(bytes.Slice(at + 48, 10)).Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int size) && size >= 0 && size <= bytes.Length - at - 60, "archive_member");
            var member = bytes.Slice(at + 60, size);
            if (name is not ("/" or "//" or "/SYM64/"))
            {
                Need(++objects <= 1024, "archive_objects");
                ReadSymbols(member, symbols);
            }

            at = checked(at + 60 + size);
            if ((size & 1) != 0)
            {
                Need(at < bytes.Length && bytes[at] == '\n', "archive_padding");
                at++;
            }
        }

        string[] required = ["sqlite3_open_v2", "sqlite3_close_v2", "sqlite3_prepare_v2", "sqlite3_step", "sqlite3_finalize", "sqlite3_bind_text", "sqlite3_bind_blob", "sqlite3_libversion", "sqlite3_sourceid"];
        Need(at == bytes.Length && objects > 0 && required.All(symbols.Contains), "archive_symbols");
        return new(Convert.ToHexString(SHA256.HashData(bytes)), bytes.Length, objects, Array.AsReadOnly(symbols.ToArray()));
    }

    private static void ReadSymbols(ReadOnlySpan<byte> elf, SortedSet<string> symbols)
    {
        Need(elf.Length >= 64 && elf[..7].SequenceEqual(new byte[] { 0x7f, 69, 76, 70, 2, 1, 1 }) && U16(elf, 16) == 1 && U16(elf, 18) == 62 && U16(elf, 52) == 64 && U16(elf, 58) == 64, "archive_elf");
        ulong sectionAt = U64(elf, 40);
        uint count = U16(elf, 60);
        Need(count is > 0 and <= 16384 && sectionAt <= (ulong)elf.Length && count * 64UL <= (ulong)elf.Length - sectionAt, "archive_sections");
        for (uint i = 0; i < count; i++)
        {
            int header = checked((int)(sectionAt + i * 64UL));
            if (U32(elf, header + 4) != 2)
                continue;
            ulong offset = U64(elf, header + 24), size = U64(elf, header + 32);
            uint link = U32(elf, header + 40);
            Need(link < count && U64(elf, header + 56) == 24 && size % 24 == 0 && size / 24 <= 100000 && offset <= (ulong)elf.Length && size <= (ulong)elf.Length - offset, "archive_symtab");
            int strings = checked((int)(sectionAt + link * 64UL));
            ulong textOffset = U64(elf, strings + 24), textBytes = U64(elf, strings + 32);
            Need(U32(elf, strings + 4) == 3 && textOffset <= (ulong)elf.Length && textBytes <= (ulong)elf.Length - textOffset, "archive_strings");
            for (ulong symbol = 0; symbol < size; symbol += 24)
            {
                int row = checked((int)(offset + symbol));
                uint nameAt = U32(elf, row);
                byte info = elf[row + 4];
                Need(nameAt < textBytes, "archive_symbol_name");
                if ((info >> 4) is not (1 or 2) || (info & 15) != 2 || U16(elf, row + 6) == 0)
                    continue;
                var rest = elf.Slice(checked((int)(textOffset + nameAt)), checked((int)(textBytes - nameAt)));
                int end = rest.IndexOf((byte)0);
                Need(end is >= 0 and <= 255, "archive_symbol_text");
                string name = Encoding.ASCII.GetString(rest[..end]);
                if (name.StartsWith("sqlite3", StringComparison.Ordinal))
                {
                    uint defined = U16(elf, row + 6);
                    Need(defined < count, "archive_symbol_section");
                    int body = checked((int)(sectionAt + defined * 64UL));
                    ulong value = U64(elf, row + 8), length = U64(elf, row + 16), sectionBytes = U64(elf, body + 32), bodyOffset = U64(elf, body + 24);
                    Need(U32(elf, body + 4) == 1 && (U64(elf, body + 8) & 4) != 0 && value < sectionBytes && length <= sectionBytes - value && bodyOffset <= (ulong)elf.Length && sectionBytes <= (ulong)elf.Length - bodyOffset, "archive_symbol_body");
                    Need(symbols.Count < 4096, "archive_symbol_count");
                    symbols.Add(name);
                }
            }
        }
    }

    /// <summary>Validate the complete finite fixture protocol, parameterized
    /// UTF8/blob identities, commit/rollback/reopen, SQLITE_BUSY/READONLY/CANTOPEN
    /// controls and closed journal state. Supplied text is value-only evidence.</summary>

    public static Transcript InspectTranscriptValues(string output)
    {
        Need(output.Length is > 0 and <= 65536, "transcript_size");
        string[] lines = output.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
        Need(lines.Length == 10 && lines[0] == "TEST_START m10.020" && lines[9] == "TEST_PASS m10.020", "transcript_inventory");
        const string prefix = "SQLITE_ENGINE version=3.53.4 sourceBase64=";
        byte[] source = new byte[128];
        Need(lines[1].StartsWith(prefix, StringComparison.Ordinal) && lines[1].Length - prefix.Length <= 172 && Convert.TryFromBase64String(lines[1][prefix.Length..], source, out int sourceBytes) && source.AsSpan(0, sourceBytes).SequenceEqual(Encoding.UTF8.GetBytes(SourceId)), "transcript_engine");
        string[] expected = ["SQLITE_POLICY journal=delete synchronous=2 locking=normal pageSize=4096", "SQLITE_COMMIT rows=1 textBytes=22 blobBytes=34003 journal=true", "SQLITE_ROLLBACK rows=1 journal=true", "SQLITE_LOCK busy=5 rows=1", "SQLITE_REOPEN rows=1 integrity=ok", "SQLITE_FAILURE readonly=8 missingParent=14", "SQLITE_CLEANUP journal=false wal=false shm=false"];
        Need(lines.Skip(2).Take(7).SequenceEqual(expected), "transcript_contract");
        return ExpectedTranscriptValues();
    }

    /// <summary>Strict value-only protocol for the separate real read-only
    /// provider process. Expected payload hashes are computed here in the host,
    /// never by the native app. This supplies no process/flush/guest authority.</summary>

    public static Transcript InspectVerificationTranscriptValues(string output)
    {
        Need(output.Length is > 0 and <= 65536, "verify_transcript_size");
        string[] lines = output.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n').Split('\n');
        string[] expected = ["TEST_START m10.020.verify", "SQLITE_VERIFY_ENGINE version=" + EngineVersion + " sourceBase64=" + Convert.ToBase64String(Encoding.UTF8.GetBytes(SourceId)), "SQLITE_VERIFY rows=1 textBytes=22 blobBytes=34003 integrity=ok readonly=true", "SQLITE_VERIFY_CLEANUP journal=false wal=false shm=false", "TEST_PASS m10.020.verify"];
        Need(lines.SequenceEqual(expected), "verify_transcript_contract");
        return ExpectedTranscriptValues();
    }

    private static Transcript ExpectedTranscriptValues() => new(EngineVersion, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(SourceId))), Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("CLROS SQLite — café"))), Convert.ToHexString(SHA256.HashData(Enumerable.Range(0, 34003).Select(i => (byte)(i ^ (i >> 9))).ToArray())));
    /// <summary>Copy bounded complete strace -ff/-ttt rows, retaining original
    /// argument/result text and mapping observed storage operations to existing
    /// successor keys. Unfinished/resumed rows are counted, never invented into
    /// completed calls. A path mention is lexical, not descriptor/VFS causality.
    /// This does not qualify a trace, native process, syscall closure or durability.</summary>

    public static Trace InspectTraceValues(IReadOnlyDictionary<string, string> files, string databasePath)
    {
        Need(files is { Count: > 0 and <= 64 } && databasePath.Length is > 0 and <= 255 && databasePath.StartsWith('/') && !databasePath.Any(c => c is '"' or '\n' or '\r' or '\0'), "trace_input");
        List<TraceCall> calls = [];
        List<TraceRow> rows = [];
        int unjoined = 0, total = 0;
        foreach (var file in files.OrderBy(f => f.Key, StringComparer.Ordinal))
        {
            Need(file.Key.Length is > 0 and <= 255 && file.Value.Length <= 4 * 1024 * 1024 && (total += file.Value.Length) <= 16 * 1024 * 1024, "trace_capacity");
            int lineNumber = 0;
            foreach (string line in file.Value.Replace("\r\n", "\n").Split('\n'))
            {
                lineNumber++;
                Need(lineNumber <= 200000 && line.Length <= 8192, "trace_line");
                if (line.Length == 0)
                    continue;
                Need(rows.Count < 200000, "trace_row_capacity");
                if (line.Contains("<unfinished ...>", StringComparison.Ordinal) || line.Contains(" resumed>", StringComparison.Ordinal))
                {
                    unjoined++;
                    rows.Add(new(file.Key, lineNumber, line, line.Contains(" resumed>", StringComparison.Ordinal) ? TraceRowKind.Resumed : TraceRowKind.Unfinished));
                    continue;
                }

                if (Regex.IsMatch(line, @"^\d+\.\d+ (?:\+\+\+|---)", RegexOptions.CultureInvariant))
                {
                    rows.Add(new(file.Key, lineNumber, line, line.Contains("+++", StringComparison.Ordinal) ? TraceRowKind.ProcessTerminal : TraceRowKind.Signal));
                    continue;
                }

                var match = Regex.Match(line, @"^\d+\.\d+ ([a-zA-Z0-9_]+)\((.*)\) += (.+)$", RegexOptions.CultureInvariant);
                Need(match.Success && calls.Count < 200000, "trace_call");
                string syscall = match.Groups[1].Value;
                string successor = syscall switch
                {
                    "fsync" or "fdatasync" or "syncfs" => "M06-020/#798",
                    "truncate" or "ftruncate" or "unlink" or "unlinkat" or "rename" or "renameat" or "renameat2" or "mkdir" or "mkdirat" => "M06-021/#799",
                    "open" or "openat" or "close" or "stat" or "fstat" or "lstat" or "newfstatat" or "statx" or "access" or "faccessat" or "fchown" or "read" or "pread64" or "readv" or "preadv" or "preadv2" or "write" or "pwrite64" or "writev" or "pwritev" or "pwritev2" or "lseek" or "fcntl" or "flock" => "M16-027/#800",
                    _ => ""
                };
                string arguments = match.Groups[2].Value;
                calls.Add(new(file.Key, lineNumber, syscall, arguments, match.Groups[3].Value, successor, arguments.Contains(databasePath, StringComparison.Ordinal)));
                rows.Add(new(file.Key, lineNumber, line, TraceRowKind.Call));
            }
        }

        Need(calls.Count > 0, "trace_empty");
        return new(Array.AsReadOnly(calls.ToArray()), unjoined, Array.AsReadOnly(calls.Where(c => c.Successor.Length != 0).Select(c => c.Successor).Distinct().Order(StringComparer.Ordinal).ToArray()))
        {
            Rows = Array.AsReadOnly(rows.ToArray())
        };
    }

    /// <summary>Copy measured storage values from bounded strace -ff/-ttt/-yy
    /// text. Require one storage-owning trace file, explicit descriptor opens,
    /// matching path annotations and closed lifetimes; cross-thread storage,
    /// unfinished owner calls, descriptor duplication and unmeasured operation
    /// shapes are unsupported. Preserve unrelated unfinished rows as incomplete
    /// projection. Journal ordering describes successful observed syscalls only;
    /// truncated strace buffers do not prove payload bytes, physical flushes,
    /// native/guest execution, crash recovery or transactional durability.
    /// F_GETLK supports only the measured successful F_UNLCK/SEEK_SET returned
    /// shape; strace does not expose its original input query here.</summary>

    public static StorageTrace InspectStorageTraceValues(IReadOnlyDictionary<string, string> files, string databasePath) => InspectStorageTraceValuesCore(files, databasePath, killed: false);
    private static StorageTrace InspectStorageTraceValuesCore(IReadOnlyDictionary<string, string> files, string databasePath, bool killed)
    {
        var trace = InspectTraceValues(files, databasePath);
        Need(databasePath.Length <= 240 && !databasePath.Any(c => c is '\\' or '<' or '>' || char.IsControl(c)) && databasePath.Split('/').Skip(1).All(s => s.Length > 0 && s is not "." and not ".."), "storage_path");
        string parent = databasePath[..databasePath.LastIndexOf('/')], journal = databasePath + "-journal";
        Need(parent.Length > 0, "storage_path");
        string Kind(string path) => path == databasePath ? "database" : path == journal ? "journal" : path == parent ? "directory" : "";
        static string[] Parts(string text)
        {
            List<string> parts = [];
            int begin = 0, depth = 0;
            bool quoted = false, escaped = false;
            for (int at = 0; at < text.Length; at++)
            {
                char c = text[at];
                if (quoted)
                {
                    if (escaped)
                        escaped = false;
                    else if (c == '\\')
                        escaped = true;
                    else if (c == '"')
                        quoted = false;
                    continue;
                }

                if (c == '"')
                {
                    quoted = true;
                    continue;
                }

                if (c is '[' or '{' or '(')
                    depth++;
                if (c is ']' or '}' or ')')
                {
                    depth--;
                    Need(depth >= 0, "storage_arguments");
                }

                if (c == ',' && depth == 0)
                {
                    parts.Add(text[begin..at].Trim());
                    begin = at + 1;
                }
            }

            Need(!quoted && depth == 0, "storage_arguments");
            parts.Add(text[begin..].Trim());
            return parts.ToArray();
        }

        string PathValue(string value) => value.Length >= 2 && value[0] == '"' && value[^1] == '"' && !value[1..^1].Contains('\\') ? value[1..^1] : "";
        long Number(string value)
        {
            Need(long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long number), "storage_number");
            return number;
        }

        long Result(TraceCall call)
        {
            var result = Regex.Match(call.Result, @"^(-?\d+)(?:<.*>)?(?: .*)?$", RegexOptions.CultureInvariant);
            Need(result.Success && long.TryParse(result.Groups[1].Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long number) && number >= -1, "storage_result");
            return long.Parse(result.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        string OpenPath(TraceCall call, string[] parts) => call.Syscall == "open" ? PathValue(parts[0]) : call.Syscall == "openat" && parts.Length >= 2 && parts[0] == "AT_FDCWD" ? PathValue(parts[1]) : "";
        string AnnotatedPath(string descriptor)
        {
            var match = Regex.Match(descriptor, @"^\d+<([^>]*)>$", RegexOptions.CultureInvariant);
            return match.Success ? match.Groups[1].Value : "";
        }

        string CallKind(TraceCall call)
        {
            string[] parts = Parts(call.Arguments);
            if (call.Syscall is "open" or "openat")
                return Kind(OpenPath(call, parts));
            return Kind(PathValue(parts[0])) is { Length: > 0 } kind ? kind : parts.Select(AnnotatedPath).Select(Kind).FirstOrDefault(k => k.Length != 0) ?? "";
        }

        string[] owners = trace.Calls.Where(c => CallKind(c).Length != 0).Select(c => c.Source).Distinct(StringComparer.Ordinal).ToArray();
        Need(owners.Length == 1, "storage_single_owner");
        string owner = owners[0];
        foreach (string line in files[owner].Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').Where(l => l.Contains("<unfinished ...>", StringComparison.Ordinal) || l.Contains(" resumed>", StringComparison.Ordinal)))
            Need(killed && Regex.IsMatch(line, @"^\d+\.\d+ (?:(?:futex|futex_time64|clock_nanosleep|nanosleep)\(|<\.\.\. (?:futex|futex_time64|clock_nanosleep|nanosleep) resumed>)", RegexOptions.CultureInvariant), "storage_owner_unjoined");
        List<StorageLifetime> lifetimes = [];
        List<StorageEvent> events = [];
        Dictionary<int, int> active = [];
        foreach (var call in trace.Calls.Where(c => c.Source == owner))
        {
            string[] args = Parts(call.Arguments);
            string kind = CallKind(call);
            if (call.Syscall is "open" or "openat")
            {
                long returned = Result(call);
                int first = call.Syscall == "open" ? 0 : 1;
                if (returned >= 0)
                    Need(returned <= int.MaxValue && !active.ContainsKey((int)returned), "storage_descriptor_reused");
                if (kind.Length == 0)
                    continue;
                Need(args.Length == first + 2 || args.Length == first + 3, "storage_open");
                string path = OpenPath(call, args), flags = args[first + 1], mode = args.Length == first + 3 ? args[first + 2] : "";
                Need(Regex.IsMatch(flags, @"^O_[A-Z0-9_]+(?:\|O_[A-Z0-9_]+)*$", RegexOptions.CultureInvariant) && (mode.Length == 0 || Regex.IsMatch(mode, @"^0[0-7]{3}$", RegexOptions.CultureInvariant)), "storage_open");
                int id = 0;
                if (returned >= 0)
                {
                    Need(AnnotatedPath(call.Result) == path, "storage_annotation");
                    id = lifetimes.Count + 1;
                    Need(id <= 4096, "storage_capacity");
                    lifetimes.Add(new(id, (int)returned, kind, path, flags, mode, call.Line, null));
                    active.Add((int)returned, id);
                }

                events.Add(new(call, id, (int)returned, kind, null, null, returned, flags, null));
                continue;
            }

            var descriptor = Regex.Match(args[0], @"^(\d+)(?:<.*>)?$", RegexOptions.CultureInvariant);
            if (descriptor.Success)
            {
                Need(int.TryParse(descriptor.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int fd), "storage_descriptor");
                if (!active.TryGetValue(fd, out int id))
                {
                    Need(kind.Length == 0, "storage_stale_descriptor");
                    continue;
                }

                var lifetime = lifetimes[id - 1];
                Need(AnnotatedPath(args[0]) == lifetime.Path, "storage_annotation");
                kind = lifetime.Kind;
                long returned = Result(call);
                long? count = null, offset = null;
                string flags = "";
                StorageLock? region = null;
                StorageReturnedLock? returnedLock = null;
                switch (call.Syscall)
                {
                    case "close":
                        Need(args.Length == 1 && returned == 0, "storage_close");
                        lifetimes[id - 1] = lifetime with
                        {
                            CloseLine = call.Line
                        };
                        active.Remove(fd);
                        break;
                    case "pread64":
                    case "pwrite64":
                        Need(args.Length == 4, "storage_io");
                        count = Number(args[2]);
                        offset = Number(args[3]);
                        break;
                    case "pwritev2":
                        Need(args.Length == 5 && args[1].StartsWith('[') && args[1].EndsWith(']') && Number(args[2]) == 1, "storage_iovec");
                        string[] vectors = Parts(args[1][1..^1]);
                        Need(vectors.Length == 1 && vectors[0].StartsWith('{') && vectors[0].EndsWith('}'), "storage_iovec");
                        string[] fields = Parts(vectors[0][1..^1]);
                        Need(fields.Length == 2 && fields[0].StartsWith("iov_base=", StringComparison.Ordinal) && fields[1].StartsWith("iov_len=", StringComparison.Ordinal), "storage_iovec");
                        count = Number(fields[1][8..]);
                        offset = Number(args[3]);
                        flags = args[4];
                        Need(Regex.IsMatch(flags, @"^(?:0|RWF_[A-Z0-9_]+(?:\|RWF_[A-Z0-9_]+)*)$", RegexOptions.CultureInvariant), "storage_io_flags");
                        break;
                    case "fcntl":
                        Need(args.Length == 3, "storage_fcntl");
                        flags = args[1];
                        if (flags == "F_SETFD")
                            Need(args[2] == "FD_CLOEXEC" && returned == 0, "storage_fcntl");
                        else if (flags == "F_GETLK")
                        {
                            Need(returned == 0, "storage_fcntl");
                            var match = Regex.Match(args[2], @"^\{l_type=(F_UNLCK), l_whence=(SEEK_SET), l_start=(\d+), l_len=(\d+), l_pid=(-?\d+)\}$", RegexOptions.CultureInvariant);
                            Need(match.Success, "storage_lock");
                            Need(int.TryParse(match.Groups[5].Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int pid), "storage_lock");
                            long start = Number(match.Groups[3].Value), length = Number(match.Groups[4].Value);
                            Need(start <= long.MaxValue - length, "storage_lock");
                            returnedLock = new(match.Groups[1].Value, match.Groups[2].Value, start, length, pid);
                        }
                        else
                        {
                            Need(flags == "F_SETLK", "storage_fcntl");
                            var match = Regex.Match(args[2], @"^\{l_type=(F_RDLCK|F_WRLCK|F_UNLCK), l_whence=(SEEK_SET), l_start=(\d+), l_len=(\d+)\}$", RegexOptions.CultureInvariant);
                            Need(match.Success, "storage_lock");
                            region = new(match.Groups[1].Value, match.Groups[2].Value, Number(match.Groups[3].Value), Number(match.Groups[4].Value));
                            Need(region.Start <= long.MaxValue - region.Length, "storage_lock");
                        }

                        break;
                    case "flock":
                        Need(args.Length == 2 && returned == 0 && args[1] is "LOCK_SH|LOCK_NB" or "LOCK_UN", "storage_flock");
                        flags = args[1];
                        break;
                    case "fsync":
                    case "fdatasync":
                        Need(args.Length == 1, "storage_sync");
                        break;
                    case "fstat":
                        Need(args.Length == 2, "storage_metadata");
                        break;
                    case "fchown":
                        Need(args.Length == 3, "storage_metadata");
                        Number(args[1]);
                        Number(args[2]);
                        break;
                    default:
                        throw new ToolFailure("sqlite_contract_storage_operation_unsupported_" + call.Syscall);
                }

                if (count.HasValue)
                    Need(count <= 16 * 1024 * 1024 && offset <= long.MaxValue - count && returned <= count, "storage_io_result");
                events.Add(new(call, id, fd, kind, count, offset, returned, flags, region)
                {
                    ReturnedLock = returnedLock
                });
            }
            else if (kind.Length != 0)
            {
                Need(call.Syscall is "stat" or "lstat" or "access" or "unlink", "storage_path_operation");
                Need(args.Length == (call.Syscall == "unlink" ? 1 : 2), "storage_path_operation");
                events.Add(new(call, 0, -1, kind, null, null, Result(call), "", null));
            }

            Need(events.Count <= 200000, "storage_capacity");
        }

        Need(killed || active.Count == 0, "storage_lifetime_unclosed");
        List<JournalOrder> journals = [];
        foreach (var lifetime in lifetimes.Where(l => l.Kind == "journal"))
        {
            int end = lifetimes.Where(l => l.Kind == "journal" && l.OpenLine > lifetime.OpenLine).Select(l => l.OpenLine).DefaultIfEmpty(int.MaxValue).Min();
            var unlink = events.FirstOrDefault(e => e.Kind == "journal" && e.Call.Syscall == "unlink" && e.Returned == 0 && e.Call.Line > lifetime.OpenLine && e.Call.Line < end);
            int until = unlink?.Call.Line ?? end;
            var own = events.Where(e => e.LifetimeId == lifetime.Id).ToArray();
            var writes = events.Where(e => e.Kind == "database" && e.Count.HasValue && e.Call.Syscall is "pwrite64" or "pwritev2" && e.Returned > 0 && e.Call.Line > lifetime.OpenLine && e.Call.Line < until).ToArray();
            var syncs = own.Where(e => e.Call.Syscall == "fsync" && e.Returned == 0).ToArray();
            var first = syncs.FirstOrDefault();
            var last = syncs.LastOrDefault();
            var header = own.LastOrDefault(e => e.Call.Syscall is "pwrite64" or "pwritev2" && e.Count == 12 && e.Offset == 0 && e.Returned == 12);
            var parentSync = events.FirstOrDefault(e => e.Kind == "directory" && e.Call.Syscall == "fsync" && e.Returned == 0 && e.Call.Line > first?.Call.Line && e.Call.Line < header?.Call.Line);
            var databaseSync = events.FirstOrDefault(e => e.Kind == "database" && e.Call.Syscall == "fsync" && e.Returned == 0 && writes.Length > 0 && e.LifetimeId == writes[0].LifetimeId && e.Call.Line > writes[^1].Call.Line && e.Call.Line < until);
            bool closed = unlink is not null && lifetime.CloseLine < unlink.Call.Line;
            bool ordered = writes.Length > 0 && writes.Select(e => e.LifetimeId).Distinct().Count() == 1 && syncs.Length >= 2 && writes.All(e => e.Returned == e.Count) && own.Where(e => e.Count.HasValue && e.Call.Syscall is "pwrite64" or "pwritev2").All(e => e.Returned == e.Count) && own.Any(e => e.Count.HasValue && e.Returned > 0 && e.Call.Line < first?.Call.Line) && parentSync is not null && lifetimes[parentSync.LifetimeId - 1].CloseLine < header?.Call.Line && header?.Call.Line < last?.Call.Line && own.Where(e => e.Count.HasValue && e.Returned > 0).All(e => e.Call.Line < last?.Call.Line) && last?.Call.Line < writes[0].Call.Line && databaseSync is not null && databaseSync.Call.Line < lifetime.CloseLine && closed;
            journals.Add(new(lifetime.Id, writes.Length, first?.Call.Line, parentSync?.Call.Line, header?.Call.Line, last?.Call.Line, databaseSync?.Call.Line, unlink?.Call.Line, closed, ordered));
        }

        return new(owner, Array.AsReadOnly(lifetimes.ToArray()), Array.AsReadOnly(events.ToArray()), Array.AsReadOnly(journals.ToArray()), trace.UnjoinedLines);
    }

    /// <summary>Inspect copied storage before a separately proven SIGKILL. Keep
    /// open writer lifetimes and unfinished pure runtime waits; never invent
    /// closes or completed waits. Require the scoped read-only journal observer
    /// to SH|NB lock, buffer-read4096 at0 (at least28 returned bytes), obtain
    /// one to four matching fstats, unlock and close before the same writer's
    /// complete SPILL stdout write. Metadata may precede or follow the read.
    /// Match its first28 decoded bytes to that bounded marker; no full buffered
    /// payload is proved. The observer grants no writer/parent sync credit.
    /// Count protected DB writes, allowing empty next-segment headers after
    /// sync. No per-page flush, original-process or recovery authority follows.</summary>

    public static RecoveryTrace InspectKilledRecoveryTraceValues(IReadOnlyDictionary<string, string> files, string databasePath)
    {
        var storage = InspectStorageTraceValuesCore(files, databasePath, killed: true);
        var events = storage.Events;
        var live = storage.Lifetimes.Where(l => l.CloseLine is null).ToArray();
        Need(live.Count(l => l.Kind == "database") == 1 && live.Count(l => l.Kind == "journal") == 1 && live.Length == 2, "recovery_trace_live_files");
        Need(live.All(l => l.OpenFlags.Split('|').Contains("O_RDWR", StringComparer.Ordinal) && !l.OpenFlags.Split('|').Any(f => f is "O_RDONLY" or "O_WRONLY" or "O_TRUNC")), "recovery_trace_live_flags");
        var writes = events.Where(e => e.Kind == "database" && e.Call.Syscall is "pwrite64" or "pwritev2" && e.Returned > 0).ToArray();
        Need(writes.Length > 0, "recovery_trace_no_spill");
        int lastWrite = writes.Max(e => e.Call.Line);
        var trace = InspectTraceValues(files, databasePath);
        var markers = trace.Calls.Where(c => c.Arguments.Contains("SQLITE_RECOVERY_SPILL", StringComparison.Ordinal)).ToArray();
        Need(markers.Length == 1 && markers[0].Source == storage.Source && markers[0].Syscall == "write", "recovery_trace_spill_marker");
        var spill = markers[0];
        var marker = Regex.Match(spill.Arguments, @"^1<pipe:\[\d+\]>, ""(SQLITE_RECOVERY_SPILL rows=64 journalBytes=([1-9]\d{0,6}) headerBase64=([A-Za-z0-9+/]{38}==))\\n"", ([1-9]\d{0,2})$", RegexOptions.CultureInvariant);
        Need(marker.Success && int.TryParse(marker.Groups[4].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int markerBytes) && markerBytes == marker.Groups[1].Value.Length + 1 && spill.Result == marker.Groups[4].Value && spill.Line > lastWrite, "recovery_trace_spill_marker");
        long journalBytes = long.Parse(marker.Groups[2].Value, CultureInfo.InvariantCulture);
        string encoded = marker.Groups[3].Value;
        byte[] header = new byte[28];
        Need(journalBytes is > 512 and <= 8 * 1024 * 1024 && Convert.TryFromBase64String(encoded, header, out int headerBytes) && headerBytes == 28 && Convert.ToBase64String(header) == encoded && header.AsSpan(0, 8).SequenceEqual(new byte[] { 0xd9, 0xd5, 5, 0xf9, 0x20, 0xa1, 0x63, 0xd7 }), "recovery_trace_spill_header");
        var observers = storage.Lifetimes.Where(l => l.Kind == "journal" && l.OpenLine > lastWrite && l.OpenFlags.Split('|').Contains("O_RDONLY", StringComparer.Ordinal)).ToArray();
        Need(observers.Length == 1, "recovery_trace_observer");
        var observer = observers[0];
        string[] openFlags = observer.OpenFlags.Split('|');
        Need(observer.Mode.Length == 0 && openFlags.Length == 4 && openFlags.Distinct(StringComparer.Ordinal).Count() == 4 && openFlags.All(f => f is "O_RDONLY" or "O_NOCTTY" or "O_LARGEFILE" or "O_CLOEXEC") && observer.CloseLine is not null && observer.CloseLine < spill.Line, "recovery_trace_observer_lifetime");
        var own = events.Where(e => e.LifetimeId == observer.Id).ToArray();
        Need(own.All(e => e.Returned >= 0 && (e.Call.Syscall is "open" or "openat" or "close" or "fstat" or "pread64" or "flock" || e.Call.Syscall == "fcntl" && e.Flags == "F_SETFD" && e.Lock is null)) && own.Count(e => e.Call.Syscall == "fcntl") <= 1, "recovery_trace_observer_operation");
        var locks = own.Where(e => e.Call.Syscall == "flock").ToArray();
        var reads = own.Where(e => e.Call.Syscall == "pread64").ToArray();
        Need(locks.Length == 2 && locks[0].Flags == "LOCK_SH|LOCK_NB" && locks[1].Flags == "LOCK_UN" && locks.All(e => e.Returned == 0 && e.Lock is null) && reads.Length == 1 && reads[0].Count == 4096 && reads[0].Offset == 0 && reads[0].Returned is >= 28 and <= 4096 && locks[0].Call.Line < reads[0].Call.Line && reads[0].Call.Line < locks[1].Call.Line && locks[1].Call.Line < observer.CloseLine, "recovery_trace_observer_lock_read");
        Need(RecoveryReadHeader(reads[0].Call).AsSpan().SequenceEqual(header), "recovery_trace_observer_header");
        var metadata = own.Where(e => e.Call.Syscall == "fstat").ToArray();
        Need(metadata.Length is >= 1 and <= 4, "recovery_trace_observer_metadata");
        foreach (var stat in metadata)
        {
            var size = Regex.Match(stat.Call.Arguments, @", \{st_mode=S_IFREG\|0[0-7]{3}, st_size=([1-9]\d{0,6}), \.\.\.\}$", RegexOptions.CultureInvariant);
            Need(stat.Returned == 0 && size.Success && long.Parse(size.Groups[1].Value, CultureInfo.InvariantCulture) == journalBytes, "recovery_trace_observer_metadata");
        }

        Need(events.All(e => e.Call.Line < spill.Line), "recovery_trace_after_spill");
        int database = live.Single(l => l.Kind == "database").Id;
        var reserved = events.LastOrDefault(e => e.LifetimeId == database && e.Call.Syscall == "fcntl" && e.Flags == "F_SETLK" && e.Returned == 0 && e.Lock is not null && e.Lock.Start <= 1073741825 && (e.Lock.Length == 0 || 1073741825 - e.Lock.Start < e.Lock.Length));
        Need(reserved?.Lock?.Type == "F_WRLCK", "recovery_trace_reserved_lock");
        var journalSyncs = events.Where(e => e.Kind == "journal" && e.LifetimeId != observer.Id && e.Call.Syscall == "fsync" && e.Returned == 0).ToArray();
        var parentSyncs = events.Where(e => e.Kind == "directory" && e.Call.Syscall == "fsync" && e.Returned == 0).ToArray();
        int protectedWrites = 0;
        foreach (var write in writes)
        {
            var journals = storage.Lifetimes.Where(l => l.Kind == "journal" && l.Id != observer.Id && l.OpenLine < write.Call.Line && (l.CloseLine is null || l.CloseLine > write.Call.Line)).ToArray();
            Need(journals.Length == 1 && write.Returned == write.Count, "recovery_trace_write");
            int id = journals[0].Id;
            var headers = events.Where(e => e.LifetimeId == id && e.Count == 12 && e.Returned == 12 && e.Call.Line < write.Call.Line);
            Need(headers.Any(header => journalSyncs.Any(e => e.LifetimeId == id && e.Call.Line > header.Call.Line && e.Call.Line < write.Call.Line)) && parentSyncs.Any(e => e.Call.Line > journals[0].OpenLine && e.Call.Line < write.Call.Line), "recovery_trace_sync_order");
            protectedWrites++;
        }

        return new(storage, writes.Length, protectedWrites, journalSyncs.Length, parentSyncs.Length)
        {
            ObserverLifetimeId = observer.Id,
            SpillLine = spill.Line,
            JournalBytes = journalBytes,
            SpillHeaderBase64 = encoded
        };
    }

    // Decode only the copied28-byte prefix, not a general strace string or its
    // omitted payload. Octal escapes are one to three digits as emitted by -s.
    private static byte[] RecoveryReadHeader(TraceCall call)
    {
        int at = call.Arguments.IndexOf(", \"", StringComparison.Ordinal);
        Need(at >= 0, "recovery_trace_observer_header");
        at += 3;
        byte[] header = new byte[28];
        for (int i = 0; i < header.Length; i++)
        {
            Need(at < call.Arguments.Length, "recovery_trace_observer_header");
            char value = call.Arguments[at++];
            int decoded = value;
            Need(value != '"' && value is >= ' ' and <= '~', "recovery_trace_observer_header");
            if (value == '\\')
            {
                Need(at < call.Arguments.Length, "recovery_trace_observer_header");
                value = call.Arguments[at++];
                if (value is >= '0' and <= '7')
                {
                    decoded = value - '0';
                    int digits = 1;
                    while (digits < 3 && at < call.Arguments.Length && call.Arguments[at] is >= '0' and <= '7')
                    {
                        decoded = decoded * 8 + call.Arguments[at++] - '0';
                        digits++;
                    }
                }
                else
                    decoded = value switch
                    {
                        '\\' => 92,
                        '"' => 34,
                        'a' => 7,
                        'b' => 8,
                        'f' => 12,
                        'n' => 10,
                        'r' => 13,
                        't' => 9,
                        'v' => 11,
                        _ => -1
                    };
            }

            Need(decoded is >= 0 and <= 255, "recovery_trace_observer_header");
            header[i] = (byte)decoded;
        }

        return header;
    }

    /// <summary>Own a fresh artifacts directory and serialized managed restore,
    /// nonincremental build and execution. Hold original fixture/config inputs
    /// throughout, then hold exact assets and built outputs through joined child
    /// exit. Preserve all logs/failures and database bytes. This Windows job-owned
    /// host milestone is separate from Linux NativeAOT publication/tracing and
    /// CLROS file/lock/flush/transaction qualification.</summary>

    public static async Task<ManagedReference> RunManagedReferenceAsync(string root, string destination, CancellationToken cancellation = default)
    {
        Need(OperatingSystem.IsWindows(), "managed_owned_windows_required");
        root = Path.GetFullPath(root);
        destination = Path.GetFullPath(destination, root);
        Need(destination.StartsWith(Path.Combine(root, "artifacts") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && !Path.Exists(destination), "fresh_output_required");
        AuthorityFiles.RequirePlainPath(root, Path.GetDirectoryName(destination)!, "sqlite_output_parent");
        using var lease = NativeAotPayload.OpenQualificationLease(root);
        using AuthorityFiles held = new();
        string app = Path.Combine(root, "apps", Application), project = Path.Combine(app, Application + ".csproj");
        var sources = new[]
        {
            project,
            Path.Combine(app, "Program.cs"),
            Path.Combine(root, "global.json"),
            Path.Combine(root, "Directory.Build.props"),
            Path.Combine(root, "Directory.Build.targets")
        }.Select(path => held.Open(root, path, "sqlite source", "sqlite_source", 1024 * 1024, singleLink: true)).ToArray();
        Need(ToolingBuildInputs.Validate(root).Count == 0, "tooling_inputs_changed");
        Directory.CreateDirectory(destination);
        AuthorityFiles.RequirePlainPath(root, destination, "sqlite_output_path");
        using var ownership = new FileStream(Path.Combine(destination, "ownership.lock"), FileMode.CreateNew, FileAccess.Write, FileShare.None);
        bool succeeded = false;
        string failure = "";
        var commands = new List<object>();
        Transcript? transcript = null, verification = null;
        string databaseHash = "";
        ProcessResult? writer = null, verifier = null;
        async Task<ProcessResult> Run(string name, string[] arguments)
        {
            commands.Add(new
            {
                executable = "dotnet",
                arguments,
                workingDirectory = root
            });
            var result = await ProcessRunner.RunAsync(new("dotnet", arguments, root)
            {
                Timeout = TimeSpan.FromMinutes(5),
                OutputCharacterLimit = 4 * 1024 * 1024
            }, cancellation);
            await File.WriteAllTextAsync(Path.Combine(destination, name + ".stdout.log"), result.StandardOutput, CancellationToken.None);
            await File.WriteAllTextAsync(Path.Combine(destination, name + ".stderr.log"), result.StandardError, CancellationToken.None);
            QemuTranscripts.RequireCompleteProcess(result, "sqlite_" + name + "_incomplete");
            Need(result.Succeeded, "process_" + name);
            return result;
        }

        try
        {
            var sdk = await Run("sdk", ["--version"]);
            Need(sdk.StandardOutput.Trim() == NativeAotPayload.SdkVersion, "sdk_pin");
            await Run("restore", ["restore", project, "--nologo"]);
            await Run("build", ["build", project, "--no-restore", "--no-incremental", "--nologo", "-m:1", "-p:UseSharedCompilation=false"]);
            var assets = held.Open(root, Path.Combine(app, "obj/project.assets.json"), "sqlite assets", "sqlite_assets", 32 * 1024 * 1024, singleLink: true);
            byte[] assetBytes = assets.ReadBytes(32 * 1024 * 1024, "sqlite_assets");
            var dependencies = InspectDependencyValues(assetBytes);
            await File.WriteAllBytesAsync(Path.Combine(destination, "assets.json"), assetBytes, CancellationToken.None);
            List<AuthorityFile> archives = [];
            using (var parsed = JsonDocument.Parse(assetBytes))
                foreach (var pin in PackagePins)
                {
                    string relative = pin.Key.ToLowerInvariant() + "/" + pin.Value + "/" + pin.Key.ToLowerInvariant() + "." + pin.Value + ".nupkg";
                    string? path = parsed.RootElement.GetProperty("packageFolders").EnumerateObject().Select(f => Path.Combine(f.Name, relative)).FirstOrDefault(File.Exists);
                    Need(path is not null, "package_archive_missing");
                    var archive = held.Open(Path.GetPathRoot(path!)!, path!, "sqlite signed package", "sqlite_package", 64 * 1024 * 1024, singleLink: true);
                    Need(archive.Sha256.Equals(PackageArchiveSha256[pin.Key], StringComparison.OrdinalIgnoreCase), "package_archive_hash");
                    archives.Add(archive);
                }

            string bin = Path.Combine(app, "bin/Debug/net11.0");
            var built = System.IO.Directory.GetFiles(bin, "*", SearchOption.AllDirectories);
            Need(built.Length is > 0 and <= 256, "built_count");
            var outputs = built.Select(path => held.Open(root, path, "sqlite built output", "sqlite_built", 64 * 1024 * 1024, allowEmpty: true, singleLink: true)).ToArray();
            writer = await Run("fixture", [Path.Combine(bin, Application + ".dll"), "--database", Path.Combine(destination, "contract.db")]);
            Need(writer.StandardError.Length == 0, "fixture_stderr");
            transcript = InspectTranscriptValues(writer.StandardOutput);
            var database = held.Open(root, Path.Combine(destination, "contract.db"), "sqlite database", "sqlite_database", 1024 * 1024, singleLink: true);
            databaseHash = database.Sha256;
            verifier = await Run("verify", [Path.Combine(bin, Application + ".dll"), "--verify-database", database.Path]);
            Need(verifier.StandardError.Length == 0, "verify_stderr");
            verification = InspectVerificationTranscriptValues(verifier.StandardOutput);
            held.VerifyUnchanged();
            Need(ToolingBuildInputs.Validate(root).Count == 0, "tooling_inputs_changed");
            await File.WriteAllTextAsync(Path.Combine(destination, "dependencies.json"), JsonSerializer.Serialize(dependencies), CancellationToken.None);
            await File.WriteAllTextAsync(Path.Combine(destination, "inputs.json"), JsonSerializer.Serialize(sources.Concat(outputs).Append(assets).ToDictionary(f => Path.GetRelativePath(root, f.Path).Replace('\\', '/'), f => f.Sha256)), CancellationToken.None);
            foreach (var input in sources.Concat(outputs).Append(assets))
            {
                string relative = Path.GetRelativePath(root, input.Path), copy = Path.Combine(destination, "originals", relative);
                Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
                File.Copy(input.Path, copy);
                Need(NativeAotWebPayload.HashFile(copy).Equals(input.Sha256, StringComparison.OrdinalIgnoreCase), "retained_input_hash");
            }

            foreach (var archive in archives)
            {
                string copy = Path.Combine(destination, "packages", Path.GetFileName(archive.Path));
                Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
                File.Copy(archive.Path, copy);
                Need(NativeAotWebPayload.HashFile(copy).Equals(archive.Sha256, StringComparison.OrdinalIgnoreCase), "retained_package_hash");
            }

            held.VerifyUnchanged();
            succeeded = true;
        }
        catch (Exception error)
        {
            failure = error is ToolFailure tool ? tool.Reason : error.GetType().Name;
            throw;
        }
        finally
        {
            await File.WriteAllTextAsync(Path.Combine(destination, "commands.json"), JsonSerializer.Serialize(commands), CancellationToken.None);
            await File.WriteAllTextAsync(Path.Combine(destination, "receipt.json"), JsonSerializer.Serialize(new
            {
                Schema = "clros-sqlite-managed-reference-v3",
                Succeeded = succeeded,
                Failure = failure,
                Transcript = transcript,
                Verification = verification,
                Writer = writer,
                Verifier = verifier,
                DatabaseSha256 = databaseHash,
                LinuxNativeAotQualified = false,
                GuestQualified = false,
                DurabilityQualified = false
            }), CancellationToken.None);
        }

        return new(destination, transcript!, verification!, databaseHash);
    }

    /// <summary>Own fresh Windows managed seed/spill/reopen processes after a
    /// source-matched ordinary fixture build. Pause the writer at an actual
    /// journal/header and changed-page barrier, kill its entire owned job, then
    /// take exclusive file handles before independent recovery. Retain baseline,
    /// pre-kill and pre-reopen bytes, process results, pinned inputs and failures.
    /// No synthetic journal or host flush is used. FULL is configured policy,
    /// not an observed xSync/FlushFileBuffers trace; this process-kill candidate
    /// grants no Linux NativeAOT, guest, power-loss or durable-storage acceptance.</summary>

    public static async Task<ManagedRecovery> RunManagedRecoveryReferenceAsync(string root, string destination, CancellationToken cancellation = default)
    {
        Need(OperatingSystem.IsWindows(), "recovery_owned_windows_required");
        root = Path.GetFullPath(root);
        destination = Path.GetFullPath(destination, root);
        Need(destination.StartsWith(Path.Combine(root, "artifacts") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && !Path.Exists(destination), "fresh_output_required");
        AuthorityFiles.RequirePlainPath(root, Path.GetDirectoryName(destination)!, "sqlite_recovery_parent");
        Directory.CreateDirectory(destination);
        using var ownership = new FileStream(Path.Combine(destination, "ownership.lock"), FileMode.CreateNew, FileAccess.Write, FileShare.None);
        string database = Path.Combine(destination, "recovery.db"), baselinePath = Path.Combine(destination, "baseline.db");
        bool succeeded = false;
        string failure = "", writerFailure = "", recoveredHash = "";
        RecoveryJournal? barrier = null;
        ProcessResult? writer = null;
        List<object> commands = [];
        void SaveProcess(string name, ProcessResult result)
        {
            File.WriteAllText(Path.Combine(destination, name + ".stdout.log"), result.StandardOutput);
            File.WriteAllText(Path.Combine(destination, name + ".stderr.log"), result.StandardError);
            File.WriteAllText(Path.Combine(destination, name + ".process.json"), JsonSerializer.Serialize(result));
        }

        string[] Lines(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n').Split('\n');
        string engine = "SQLITE_RECOVERY_ENGINE version=3.53.4 sourceBase64=" + Convert.ToBase64String(Encoding.UTF8.GetBytes(SourceId));
        byte[] ReadDatabaseFile(string path, FileShare share)
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, share);
            Need(file.Length is > 0 and <= 8 * 1024 * 1024, "recovery_file_capacity");
            byte[] bytes = new byte[(int)file.Length];
            file.ReadExactly(bytes);
            Need(file.ReadByte() == -1, "recovery_file_changed");
            return bytes;
        }

        try
        {
            string ordinary = Path.Combine(destination, "ordinary");
            await RunManagedReferenceAsync(root, ordinary, cancellation);
            using var lease = NativeAotPayload.OpenQualificationLease(root);
            using AuthorityFiles held = new();
            var inventory = held.Open(root, Path.Combine(ordinary, "inputs.json"), "recovery build inputs", "sqlite_recovery_inputs", 1024 * 1024, singleLink: true);
            using (var inputs = JsonDocument.Parse(inventory.ReadBytes(1024 * 1024, "sqlite_recovery_inputs")))
            {
                var entries = inputs.RootElement.EnumerateObject().ToArray();
                Need(entries.Length is > 5 and <= 512, "recovery_input_inventory");
                foreach (var entry in entries)
                {
                    string path = Path.GetFullPath(entry.Name, root);
                    Need(path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "recovery_input_path");
                    var input = held.Open(root, path, "recovery exact build input", "sqlite_recovery_input", 64 * 1024 * 1024, allowEmpty: true, singleLink: true);
                    Need(input.Sha256.Equals(entry.Value.GetString(), StringComparison.OrdinalIgnoreCase), "recovery_input_changed");
                }
            }

            foreach (var pin in PackagePins)
            {
                var package = held.Open(root, Path.Combine(ordinary, "packages", pin.Key.ToLowerInvariant() + "." + pin.Value + ".nupkg"), "recovery pinned package", "sqlite_recovery_package", 64 * 1024 * 1024, singleLink: true);
                Need(package.Sha256.Equals(PackageArchiveSha256[pin.Key], StringComparison.OrdinalIgnoreCase), "recovery_package_changed");
            }

            string app = Path.Combine(root, "apps", Application, "bin/Debug/net11.0", Application + ".dll");
            async Task<ProcessResult> Run(string mode)
            {
                string[] arguments = [app, "--recovery-" + mode, database];
                commands.Add(new
                {
                    Executable = "dotnet",
                    Arguments = arguments,
                    WorkingDirectory = root
                });
                var result = await ProcessRunner.RunAsync(new("dotnet", arguments, root)
                {
                    Timeout = TimeSpan.FromMinutes(1),
                    OutputCharacterLimit = 64 * 1024
                }, cancellation);
                SaveProcess(mode, result);
                QemuTranscripts.RequireCompleteProcess(result, "sqlite_recovery_" + mode + "_cleanup");
                Need(result.Succeeded && result.StandardError.Length == 0, "recovery_" + mode + "_process");
                return result;
            }

            var seed = await Run("seed");
            Need(Lines(seed.StandardOutput).SequenceEqual(new[] { "TEST_START m10.020.recovery.seed", engine, "SQLITE_RECOVERY_SEED rows=64 pageSize=4096", "TEST_PASS m10.020.recovery.seed" }), "recovery_seed_transcript");
            byte[] baseline = ReadDatabaseFile(database, FileShare.None);
            await File.WriteAllBytesAsync(baselinePath, baseline, cancellation);
            held.Open(root, baselinePath, "recovery committed baseline", "sqlite_recovery_baseline", 2 * 1024 * 1024, singleLink: true);
            using (var kill = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
            {
                deadline.CancelAfter(TimeSpan.FromSeconds(45));
                var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                StringBuilder output = new();
                string[] arguments = [app, "--recovery-spill", database];
                commands.Add(new
                {
                    Executable = "dotnet",
                    Arguments = arguments,
                    WorkingDirectory = root
                });
                var running = ProcessRunner.RunAsync(new("dotnet", arguments, root)
                {
                    Timeout = TimeSpan.FromMinutes(1),
                    OutputCharacterLimit = 64 * 1024,
                    OutputReceived = (text, error) =>
                {
                    if (error)
                        return;
                    output.Append(text);
                    if (output.Length > 64 * 1024)
                        throw new ToolFailure("sqlite_contract_recovery_output_capacity");
                    if (output.ToString().Split('\n').SkipLast(1).Any(line => line.StartsWith("SQLITE_RECOVERY_SPILL ", StringComparison.Ordinal)))
                        ready.TrySetResult();
                }
                }, kill.Token);
                byte[]? changed = null, journal = null;
                try
                {
                    Need(await Task.WhenAny(running, ready.Task).WaitAsync(deadline.Token) == ready.Task && !running.IsCompleted, "recovery_spill_barrier_not_reached");
                    changed = ReadDatabaseFile(database, FileShare.ReadWrite);
                    journal = ReadDatabaseFile(database + "-journal", FileShare.ReadWrite);
                    await File.WriteAllBytesAsync(Path.Combine(destination, "before-kill.db"), changed, cancellation);
                    await File.WriteAllBytesAsync(Path.Combine(destination, "before-kill.journal"), journal, cancellation);
                    barrier = InspectRecoveryJournalValues(baseline, changed, journal);
                    held.VerifyUnchanged();
                    Need(!running.IsCompleted, "recovery_writer_exited_before_kill");
                }
                catch (Exception error)
                {
                    writerFailure = error is ToolFailure tool ? tool.Reason : error.GetType().Name;
                    throw;
                }
                finally
                {
                    kill.Cancel();
                    writer = await running;
                    SaveProcess("spill", writer);
                }

                Need(writer.Cancelled && !writer.TimedOut && !writer.OutputLimitExceeded && writer.TerminationConfirmed && writer.FailureReason.Length == 0 && writer.StandardError.Length == 0, "recovery_writer_owned_kill");
                Need(Lines(writer.StandardOutput).SequenceEqual(new[] { "TEST_START m10.020.recovery.spill", engine, "SQLITE_RECOVERY_SPILL rows=64 journalBytes=" + journal!.Length + " headerBase64=" + Convert.ToBase64String(journal.AsSpan(0, 28)) }), "recovery_spill_transcript");
                // Exclusive handles after joined job termination provide the
                // actual host absence-of-writer/lock barrier before reopen.
                using (var db = new FileStream(database, FileMode.Open, FileAccess.Read, FileShare.None))
                using (var log = new FileStream(database + "-journal", FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    Need(db.Length == changed!.Length && log.Length == journal.Length, "recovery_postkill_size");
                    byte[] dbBytes = new byte[changed.Length], logBytes = new byte[journal.Length];
                    db.ReadExactly(dbBytes);
                    log.ReadExactly(logBytes);
                    Need(dbBytes.AsSpan().SequenceEqual(changed) && logBytes.AsSpan().SequenceEqual(journal), "recovery_postkill_changed");
                    await File.WriteAllBytesAsync(Path.Combine(destination, "before-reopen.db"), dbBytes, cancellation);
                    await File.WriteAllBytesAsync(Path.Combine(destination, "before-reopen.journal"), logBytes, cancellation);
                }
            }

            foreach (string name in new[]
            {
                "before-kill.db",
                "before-kill.journal",
                "before-reopen.db",
                "before-reopen.journal"
            }

            )
                held.Open(root, Path.Combine(destination, name), "recovery retained crash bytes", "sqlite_recovery_crash", 8 * 1024 * 1024, singleLink: true);
            var verify = await Run("verify");
            Need(Lines(verify.StandardOutput).SequenceEqual(new[] { "TEST_START m10.020.recovery.verify", engine, "SQLITE_RECOVERY_VERIFY rows=64 generation=0 integrity=ok", "TEST_PASS m10.020.recovery.verify" }), "recovery_verify_transcript");
            byte[] recovered = ReadDatabaseFile(database, FileShare.None);
            Need(recovered.AsSpan().SequenceEqual(baseline), "recovery_baseline_not_restored");
            recoveredHash = Convert.ToHexString(SHA256.HashData(recovered));
            Need(!Path.Exists(database + "-journal") && !Path.Exists(database + "-wal") && !Path.Exists(database + "-shm"), "recovery_sidecars_remaining");
            held.VerifyUnchanged();
            Need(ToolingBuildInputs.Validate(root).Count == 0, "tooling_inputs_changed");
            succeeded = true;
        }
        catch (Exception error)
        {
            failure = error is ToolFailure tool ? tool.Reason : error.GetType().Name + " " + error.Message.ReplaceLineEndings(" ");
            throw;
        }
        finally
        {
            await File.WriteAllTextAsync(Path.Combine(destination, "commands.json"), JsonSerializer.Serialize(commands), CancellationToken.None);
            await File.WriteAllTextAsync(Path.Combine(destination, "receipt.json"), JsonSerializer.Serialize(new
            {
                Schema = "clros-sqlite-managed-process-kill-recovery-v1",
                Succeeded = succeeded,
                Failure = failure,
                WriterFailure = writerFailure,
                Journal = barrier,
                Writer = writer,
                RecoveredDatabaseSha256 = recoveredHash,
                FullPolicyConfigured = true,
                SqliteFlushCallsObserved = false,
                HostAssistedFlush = false,
                OriginalLinuxProducerSource = "f61648c223e9aad99e0bbb48a4be5ee19a0c1bce",
                RequiresDistinctLinuxPublication = true,
                LinuxNativeAotQualified = false,
                GuestQualified = false,
                PowerLossQualified = false,
                DurabilityQualified = false
            }), CancellationToken.None);
        }

        return new(destination, barrier!, writer!, recoveredHash);
    }

    /// <summary>Return copied direct-exec argv for one bounded recovery mode.
    /// Paths must be absolute Linux values without traversal or control bytes.
    /// No shell, publication, process ownership or execution is authorized here.</summary>

    public static IReadOnlyList<string> LinuxRecoveryTraceArguments(string executable, string database, string mode, string trace)
    {
        Need(mode is "seed" or "spill" or "verify", "recovery_mode");
        foreach (string path in new[]
        {
            executable,
            database,
            trace
        }

        )
            Need(path.Length is > 1 and <= 4096 && path.StartsWith('/') && !path.Any(char.IsControl) && path.Split('/').Skip(1).All(p => p.Length > 0 && p is not "." and not ".."), "recovery_linux_path");
        Need(executable != database && executable != trace && database != trace, "recovery_linux_path");
        return Array.AsReadOnly(new[] { "-ff", "-yy", "-ttt", "-s", "256", "-o", trace, executable, "--recovery-" + mode, database });
    }

    /// <summary>Copied bounded strace argv for one distinct ordinary writer or
    /// read-only verifier. Planning performs no execution or path admission.</summary>

    public static IReadOnlyList<string> LinuxReferenceTraceArguments(string executable, string database, bool verify, string trace)
    {
        foreach (string path in new[]
        {
            executable,
            database,
            trace
        }

        )
            Need(path.Length is > 1 and <= 4096 && path.StartsWith('/') && !path.Any(char.IsControl) && path.Split('/').Skip(1).All(p => p.Length > 0 && p is not "." and not ".."), "reference_linux_path");
        Need(executable != database && executable != trace && database != trace, "reference_linux_path");
        return Array.AsReadOnly(new[] { "-ff", "-yy", "-ttt", "-s", "256", "-o", trace, executable, verify ? "--verify-database" : "--database", database });
    }

    /// <summary>Fresh Linux x64 recovery-reference preparation. Reuse the pinned
    /// publisher in a new child namespace, never an old ordinary executable.
    /// Hold that publication's exact inputs/outputs; retain seed/spill/verify
    /// traces and crash bytes. Bind the actual sole tracer child to a pidfd,
    /// SIGKILL that writer, drain/reap strace and join its owned session before
    /// probing RESERVED-byte locks and reopening. This method must only be run
    /// under the caller's serialized native-builder lease. It supplies no guest
    /// or power-loss authority and does not alter original f616/r03 receipts.
    /// Optional archiveDependencyRoot explicitly reuses the reviewed archive,
    /// not an old application publication. This outer owner holds all nine
    /// originals independently of the nested fresh publisher through kill/join,
    /// RESERVED-lock verification and recovered reopen. Default builds twice.</summary>

    public static async Task<int> RunLinuxRecoveryReferenceAsync(string root, string recipeRoot, string sourceArchive, string destination, TextWriter output, string? archiveDependencyRoot = null)
    {
        const string marker = "nativeaot.sqlite-recovery-reference";
        output.WriteLine("TEST_START " + marker);
        Need(OperatingSystem.IsLinux() && RuntimeInformation.ProcessArchitecture == Architecture.X64, "linux_recovery_platform");
        root = Path.GetFullPath(root);
        recipeRoot = Path.GetFullPath(recipeRoot);
        destination = Path.GetFullPath(destination, root);
        Need(root != recipeRoot && destination.StartsWith(Path.Combine(root, "artifacts") + "/", StringComparison.Ordinal) && !Path.Exists(destination), "linux_recovery_fresh_output");
        AuthorityFiles.RequirePlainPath(root, Path.GetDirectoryName(destination)!, "sqlite_recovery_parent");
        Directory.CreateDirectory(destination);
        using var ownership = new FileStream(Path.Combine(destination, "ownership.lock"), FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using AuthorityFiles held = new();
        string publication = Path.Combine(destination, "publication"), database = Path.Combine(destination, "recovery.db"), publicationHash = "";
        List<object> commands = [];
        Dictionary<string, string> traceHashes = new(StringComparer.Ordinal);
        bool succeeded = false;
        string failure = "", recoveredHash = "";
        IReadOnlyDictionary<string, AuthorityFile>? dependencyInputs = null;
        string archiveBuild = "";
        int? freshCompilesCompleted = null;
        RecoveryJournal? barrier = null;
        RecoveryTrace? writerTrace = null;
        LinuxRecoveryIdentity? identity = null;
        bool pidfdDead = false, sessionJoined = false, lockClear = false;
        string engine = "SQLITE_RECOVERY_ENGINE version=3.53.4 sourceBase64=" + Convert.ToBase64String(Encoding.UTF8.GetBytes(SourceId));
        string[] Lines(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n').Split('\n');
        byte[] ReadBytes(string path, int maximum)
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            Need(file.Length > 0 && file.Length <= maximum, "recovery_file_capacity");
            byte[] bytes = new byte[(int)file.Length];
            file.ReadExactly(bytes);
            Need(file.ReadByte() == -1, "recovery_file_changed");
            return bytes;
        }

        async Task<Dictionary<string, string>> Traces(string mode, int? killedPid = null)
        {
            string[] paths = Directory.GetFiles(destination, mode + "-trace.*");
            Need(paths.Length is > 0 and <= 64, "recovery_trace_inventory");
            Dictionary<string, string> values = new(StringComparer.Ordinal);
            int total = 0;
            foreach (string path in paths.Order(StringComparer.Ordinal))
            {
                var originalTrace = held.Open(root, path, "original recovery trace", "sqlite_recovery_trace", 4 * 1024 * 1024, singleLink: true);
                byte[] bytes = originalTrace.ReadBytes(4 * 1024 * 1024, "sqlite_recovery_trace");
                Need((total += bytes.Length) <= 16 * 1024 * 1024, "recovery_trace_capacity");
                string text = new UTF8Encoding(false, true).GetString(bytes), name = Path.GetFileName(path), suffix = name[(name.LastIndexOf('.') + 1)..];
                Need(int.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out int pid) && pid > 0, "recovery_trace_pid");
                bool killed = text.TrimEnd('\r', '\n').EndsWith("+++ killed by SIGKILL +++", StringComparison.Ordinal);
                Need(killedPid == pid ? killed : text.TrimEnd('\r', '\n').EndsWith("+++ exited with 0 +++", StringComparison.Ordinal) || killedPid.HasValue && killed, "recovery_trace_terminal");
                values.Add(name, text);
                traceHashes.Add(name, Convert.ToHexString(SHA256.HashData(bytes)));
            }

            Need(!killedPid.HasValue || values.ContainsKey(mode + "-trace." + killedPid.Value.ToString(CultureInfo.InvariantCulture)), "recovery_trace_main");
            var projection = InspectTraceValues(values, database);
            await File.WriteAllTextAsync(Path.Combine(destination, mode + "-trace-values.json"), JsonSerializer.Serialize(projection));
            return values;
        }

        try
        {
            if (archiveDependencyRoot is not null)
            {
                archiveDependencyRoot = Path.GetFullPath(archiveDependencyRoot);
                Need(archiveDependencyRoot != root && archiveDependencyRoot != recipeRoot && archiveDependencyRoot != destination && !archiveDependencyRoot.StartsWith(destination + "/", StringComparison.Ordinal) && !destination.StartsWith(archiveDependencyRoot + "/", StringComparison.Ordinal), "dependency_path");
                dependencyInputs = HoldArchiveDependencyInputs(held, archiveDependencyRoot, ArchiveDependencyReceiptSha256);
            }

            Need(await RunLinuxReferenceAsync(root, recipeRoot, sourceArchive, publication, output, archiveDependencyRoot) == 0, "recovery_publication_failed");
            using var lease = NativeAotPayload.OpenQualificationLease(root);
            var producer = held.Open(root, Path.Combine(publication, "receipt.json"), "fresh recovery publication", "sqlite_recovery_publication", 4 * 1024 * 1024, singleLink: true);
            publicationHash = producer.Sha256;
            using (var receipt = JsonDocument.Parse(producer.ReadBytes(4 * 1024 * 1024, "sqlite_recovery_publication")))
            {
                Need(receipt.RootElement.GetProperty("Succeeded").GetBoolean(), "recovery_publication_failed");
                archiveBuild = receipt.RootElement.GetProperty("ArchiveBuild").GetString() ?? "";
                freshCompilesCompleted = receipt.RootElement.GetProperty("FreshCompilesCompleted").GetInt32();
                Need(archiveDependencyRoot is null ? archiveBuild == "fresh_two_compiles" && freshCompilesCompleted == 2 : archiveBuild == "reused_verified_dependency" && freshCompilesCompleted == 0 && receipt.RootElement.GetProperty("ArchiveDependency").GetProperty("ReceiptSha256").GetString() == ArchiveDependencyReceiptSha256, "recovery_publication_dependency");
                foreach (var entry in receipt.RootElement.GetProperty("Sources").EnumerateObject().Concat(receipt.RootElement.GetProperty("Outputs").EnumerateObject()))
                {
                    string path = Path.IsPathFullyQualified(entry.Name) ? entry.Name : Path.Combine(publication, entry.Name);
                    Need(path.StartsWith(root + "/", StringComparison.Ordinal), "recovery_publication_path");
                    var file = held.Open(root, path, "recovery publication input/output", "sqlite_recovery_publication_file", 64 * 1024 * 1024, allowEmpty: true, singleLink: true);
                    Need(file.Sha256.Equals(entry.Value.GetString(), StringComparison.OrdinalIgnoreCase), "recovery_publication_changed");
                }
            }

            string executable = Path.Combine(publication, Application, Application);
            async Task<string> Run(string mode)
            {
                string[] arguments = LinuxRecoveryTraceArguments(executable, database, mode, Path.Combine(destination, mode + "-trace")).ToArray();
                commands.Add(new
                {
                    Executable = "setsid",
                    Arguments = new[] { "strace" }.Concat(arguments),
                    WorkingDirectory = root
                });
                await NativeAotWebPayload.ExecuteAsync("strace", arguments, root, Path.Combine(destination, mode), TimeSpan.FromMinutes(2));
                await Traces(mode);
                var stdout = held.Open(root, Path.Combine(destination, mode + ".stdout.log"), "original recovery console", "sqlite_recovery_console", 65536, singleLink: true);
                var stderr = held.Open(root, Path.Combine(destination, mode + ".stderr.log"), "original recovery stderr", "sqlite_recovery_console", 65536, allowEmpty: true, singleLink: true);
                Need(stderr.ReadBytes(65536, "sqlite_recovery_console").Length == 0, "recovery_console");
                return new UTF8Encoding(false, true).GetString(stdout.ReadBytes(65536, "sqlite_recovery_console"));
            }

            Need(Lines(await Run("seed")).SequenceEqual(new[] { "TEST_START m10.020.recovery.seed", engine, "SQLITE_RECOVERY_SEED rows=64 pageSize=4096", "TEST_PASS m10.020.recovery.seed" }), "recovery_seed_transcript");
            byte[] baseline = ReadBytes(database, 2 * 1024 * 1024);
            await File.WriteAllBytesAsync(Path.Combine(destination, "baseline.db"), baseline);
            var baselineFile = held.Open(root, Path.Combine(destination, "baseline.db"), "recovery baseline", "sqlite_recovery_baseline", 2 * 1024 * 1024, singleLink: true);
            Need(baselineFile.Sha256.Equals(Convert.ToHexString(SHA256.HashData(baseline)), StringComparison.OrdinalIgnoreCase), "recovery_baseline_changed");
            string[] spill = LinuxRecoveryTraceArguments(executable, database, "spill", Path.Combine(destination, "spill-trace")).ToArray();
            commands.Add(new
            {
                Executable = "setsid",
                Arguments = new[] { "strace" }.Concat(spill),
                WorkingDirectory = root
            });
            using (var process = NativeAotWebPayload.Start("strace", spill, root, expectedBody: null))
            using (var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(2)))
            {
                var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                async Task Capture(StreamReader reader, string path, bool stdout)
                {
                    try
                    {
                        await using FileStream log = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                        char[] buffer = new char[4096];
                        StringBuilder text = new();
                        while (true)
                        {
                            int count = await reader.ReadAsync(buffer);
                            if (count == 0)
                                break;
                            text.Append(buffer, 0, count);
                            byte[] bytes = Encoding.UTF8.GetBytes(buffer.AsSpan(0, count).ToString());
                            Need(log.Length + bytes.Length <= 65536, "recovery_console_capacity");
                            await log.WriteAsync(bytes);
                            await log.FlushAsync();
                            if (stdout && text.ToString().Split('\n').SkipLast(1).Any(l => l.StartsWith("SQLITE_RECOVERY_SPILL ", StringComparison.Ordinal)))
                                ready.TrySetResult();
                        }
                    }
                    catch
                    {
                        deadline.Cancel();
                        throw;
                    }
                }

                Task stdout = Capture(process.StandardOutput, Path.Combine(destination, "spill.stdout.log"), true), stderr = Capture(process.StandardError, Path.Combine(destination, "spill.stderr.log"), false);
                Exception? runFailure = null;
                try
                {
                    Need(await Task.WhenAny(process.WaitForExitAsync(), ready.Task).WaitAsync(deadline.Token) == ready.Task && !process.HasExited, "recovery_spill_barrier_not_reached");
                    LinuxRecoveryIdentity Observe()
                    {
                        string children = ReadRecoveryProc($"/proc/{process.Id}/task/{process.Id}/children");
                        string[] ids = children.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                        Need(ids.Length == 1 && int.TryParse(ids[0], NumberStyles.None, CultureInfo.InvariantCulture, out _), "recovery_proc_children");
                        string child = ids[0];
                        return InspectLinuxRecoveryIdentityValues(process.Id, ReadRecoveryProc($"/proc/{process.Id}/stat"), children, ReadRecoveryProc($"/proc/{child}/stat"), ReadRecoveryProc($"/proc/{child}/status"), File.ResolveLinkTarget($"/proc/{child}/exe", true)?.FullName ?? "", executable);
                    }

                    identity = Observe();
                    nint opened = RecoveryPidfdOpen(434, identity.Writer, 0);
                    Need(opened >= 0 && opened <= int.MaxValue, "recovery_pidfd_open");
                    using var pidfd = new SafeFileHandle(opened, true);
                    Need(!pidfd.IsInvalid && Observe() == identity && !RecoveryPidfdDead(pidfd), "recovery_pidfd_identity");
                    byte[] changed = ReadBytes(database, 2 * 1024 * 1024), journal = ReadBytes(database + "-journal", 8 * 1024 * 1024);
                    await File.WriteAllBytesAsync(Path.Combine(destination, "before-kill.db"), changed);
                    await File.WriteAllBytesAsync(Path.Combine(destination, "before-kill.journal"), journal);
                    barrier = InspectRecoveryJournalValues(baseline, changed, journal);
                    held.VerifyUnchanged();
                    Need(Observe() == identity && !RecoveryPidfdDead(pidfd) && RecoveryPidfdSignal(424, pidfd, 9, 0, 0) == 0, "recovery_pidfd_signal");
                    await process.WaitForExitAsync(deadline.Token);
                    await Task.WhenAll(stdout, stderr).WaitAsync(deadline.Token);
                    Need(process.ExitCode == 137 && RecoveryPidfdDead(pidfd), "recovery_writer_death");
                    pidfdDead = true;
                    var originalOut = held.Open(root, Path.Combine(destination, "spill.stdout.log"), "original spill console", "sqlite_recovery_console", 65536, singleLink: true);
                    var originalError = held.Open(root, Path.Combine(destination, "spill.stderr.log"), "original spill stderr", "sqlite_recovery_console", 65536, allowEmpty: true, singleLink: true);
                    Need(Lines(new UTF8Encoding(false, true).GetString(originalOut.ReadBytes(65536, "sqlite_recovery_console"))).SequenceEqual(new[] { "TEST_START m10.020.recovery.spill", engine, "SQLITE_RECOVERY_SPILL rows=64 journalBytes=" + journal.Length + " headerBase64=" + Convert.ToBase64String(journal.AsSpan(0, 28)) }) && originalError.ReadBytes(65536, "sqlite_recovery_console").Length == 0, "recovery_spill_transcript");
                }
                catch (Exception error)
                {
                    runFailure = error;
                    throw;
                }
                finally
                {
                    Exception? cleanup = null;
                    try
                    {
                        await NativeAotWebPayload.StopSessionAsync(process);
                        sessionJoined = true;
                    }
                    catch (Exception error)
                    {
                        cleanup = error;
                    }

                    try
                    {
                        await Task.WhenAll(stdout, stderr).WaitAsync(TimeSpan.FromSeconds(10));
                    }
                    catch (Exception error)
                    {
                        cleanup = cleanup is null ? error : new AggregateException(cleanup, error);
                    }

                    await File.WriteAllTextAsync(Path.Combine(destination, "writer-owner.json"), JsonSerializer.Serialize(new
                    {
                        Identity = identity,
                        PidfdDeathObserved = pidfdDead,
                        SessionJoined = sessionJoined,
                        Signal = 9,
                        TracerExit = process.HasExited ? process.ExitCode : (int?)null
                    }));
                    if (cleanup is not null)
                        throw runFailure is null ? cleanup : new AggregateException(runFailure, cleanup);
                }
            }

            Need(identity is not null && pidfdDead && sessionJoined, "recovery_owner_incomplete");
            var selected = identity!;
            writerTrace = InspectKilledRecoveryTraceValues(await Traces("spill", selected.Writer), database);
            Need(writerTrace.Storage.Source == "spill-trace." + selected.Writer.ToString(CultureInfo.InvariantCulture), "recovery_trace_storage_owner");
            using (var db = new FileStream(database, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                RecoveryFlock region = new()
                {
                    Type = 1,
                    Whence = 0,
                    Start = 1073741825,
                    Length = 1
                };
                Need(RecoveryGetLock(db.SafeFileHandle, 5, ref region) == 0 && region.Type == 2, "recovery_reserved_lock_remains");
                lockClear = true;
                await File.WriteAllTextAsync(Path.Combine(destination, "reserved-lock.json"), JsonSerializer.Serialize(new
                {
                    Command = "F_GETLK",
                    RequestedType = "F_WRLCK",
                    Start = 1073741825,
                    Length = 1,
                    ReturnedType = region.Type,
                    region.Pid,
                    ReservedLockAbsent = true
                }));
                Need(db.Length is > 0 and <= 2 * 1024 * 1024, "recovery_file_capacity");
                byte[] bytes = new byte[(int)db.Length];
                db.ReadExactly(bytes);
                byte[] journal = ReadBytes(database + "-journal", 8 * 1024 * 1024);
                Need(InspectRecoveryJournalValues(baseline, bytes, journal) == barrier, "recovery_postkill_changed");
                await File.WriteAllBytesAsync(Path.Combine(destination, "before-reopen.db"), bytes);
                await File.WriteAllBytesAsync(Path.Combine(destination, "before-reopen.journal"), journal);
            }

            foreach (string name in new[]
            {
                "before-kill.db",
                "before-kill.journal",
                "before-reopen.db",
                "before-reopen.journal"
            }

            )
            {
                var snapshot = held.Open(root, Path.Combine(destination, name), "original recovery bytes", "sqlite_recovery_bytes", 8 * 1024 * 1024, singleLink: true);
                Need(snapshot.Sha256.Equals(name.EndsWith(".journal", StringComparison.Ordinal) ? barrier!.JournalSha256 : barrier!.ChangedDatabaseSha256, StringComparison.OrdinalIgnoreCase), "recovery_snapshot_changed");
            }

            Need(Lines(await Run("verify")).SequenceEqual(new[] { "TEST_START m10.020.recovery.verify", engine, "SQLITE_RECOVERY_VERIFY rows=64 generation=0 integrity=ok", "TEST_PASS m10.020.recovery.verify" }), "recovery_verify_transcript");
            byte[] recovered = ReadBytes(database, 2 * 1024 * 1024);
            Need(recovered.AsSpan().SequenceEqual(baseline), "recovery_baseline_not_restored");
            recoveredHash = Convert.ToHexString(SHA256.HashData(recovered));
            Need(!Path.Exists(database + "-journal") && !Path.Exists(database + "-wal") && !Path.Exists(database + "-shm"), "recovery_sidecars_remaining");
            var finalDatabase = held.Open(root, database, "recovered database", "sqlite_recovery_database", 2 * 1024 * 1024, singleLink: true);
            Need(finalDatabase.Sha256.Equals(recoveredHash, StringComparison.OrdinalIgnoreCase), "recovery_database_changed");
            held.VerifyUnchanged();
            Need(ToolingBuildInputs.Validate(root).Count == 0, "tooling_inputs_changed");
            succeeded = true;
        }
        catch (Exception error)
        {
            failure = error is ToolFailure tool ? tool.Reason : error.Message.ReplaceLineEndings(" ");
            output.WriteLine("TEST_FAIL " + marker + " " + failure);
            throw;
        }
        finally
        {
            await File.WriteAllTextAsync(Path.Combine(destination, "commands.json"), JsonSerializer.Serialize(commands));
            var files = Directory.GetFiles(destination, "*", SearchOption.AllDirectories).Where(p => Path.GetFileName(p) is not "ownership.lock" and not "qualification.lock" && p != Path.Combine(destination, "receipt.json")).Order(StringComparer.Ordinal);
            await File.WriteAllTextAsync(Path.Combine(destination, "receipt.json"), JsonSerializer.Serialize(new
            {
                Schema = "clros-sqlite-linux-process-kill-recovery-v1",
                Succeeded = succeeded,
                Failure = failure,
                FreshPublicationReceiptSha256 = publicationHash,
                Journal = barrier,
                ArchiveBuild = archiveBuild,
                FreshCompilesCompleted = freshCompilesCompleted,
                ArchiveDependencyRoot = archiveDependencyRoot,
                ArchiveDependencyReceiptSha256 = dependencyInputs?["receipt.json"].Sha256,
                ArchiveDependencyInputs = dependencyInputs?.ToDictionary(p => p.Key, p => p.Value.Sha256, StringComparer.Ordinal),
                WriterTrace = writerTrace,
                Identity = identity,
                PidfdDeathObserved = pidfdDead,
                SessionJoined = sessionJoined,
                ReservedLockAbsent = lockClear,
                RecoveredDatabaseSha256 = recoveredHash,
                TraceSha256 = traceHashes,
                Outputs = files.ToDictionary(p => Path.GetRelativePath(destination, p), NativeAotWebPayload.HashFile),
                CompleteWriterTraceProjection = false,
                OriginalLinuxProducerSource = "f61648c223e9aad99e0bbb48a4be5ee19a0c1bce",
                FreshPublicationRequired = true,
                GuestQualified = false,
                PowerLossQualified = false,
                DurabilityQualified = false
            }));
        }

        output.WriteLine("TEST_PASS " + marker);
        return 0;
    }

    private static string ReadRecoveryProc(string path)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        byte[] bytes = new byte[65537];
        int count = 0, read;
        while (count < bytes.Length && (read = file.Read(bytes.AsSpan(count))) > 0)
            count += read;
        Need(count < bytes.Length, "recovery_proc_capacity");
        return new UTF8Encoding(false, true).GetString(bytes, 0, count);
    }

    private static bool RecoveryPidfdDead(SafeFileHandle pidfd)
    {
        RecoveryPoll poll = new()
        {
            Descriptor = checked((int)pidfd.DangerousGetHandle()),
            Events = 1
        };
        int count = RecoveryPollCall(ref poll, 1, 0);
        return InspectRecoveryPidfdPollValues(count, poll.Returned);
    }

    [DllImport("libc", EntryPoint = "syscall", SetLastError = true)]
    private static extern nint RecoveryPidfdOpen(nint number, nint pid, nint flags);
    [DllImport("libc", EntryPoint = "syscall", SetLastError = true)]
    private static extern nint RecoveryPidfdSignal(nint number, SafeFileHandle pidfd, nint signal, nint info, nint flags);
    [DllImport("libc", EntryPoint = "poll", SetLastError = true)]
    private static extern int RecoveryPollCall(ref RecoveryPoll descriptor, nuint count, int timeout);
    [DllImport("libc", EntryPoint = "fcntl", SetLastError = true)]
    private static extern int RecoveryGetLock(SafeFileHandle descriptor, int command, ref RecoveryFlock region);
    private static bool Hex(string value) => value.Length == 64 && value.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F');
    private static ushort U16(ReadOnlySpan<byte> bytes, int at) => BinaryPrimitives.ReadUInt16LittleEndian(bytes[at..]);
    private static uint U32(ReadOnlySpan<byte> bytes, int at) => BinaryPrimitives.ReadUInt32LittleEndian(bytes[at..]);
    private static ulong U64(ReadOnlySpan<byte> bytes, int at) => BinaryPrimitives.ReadUInt64LittleEndian(bytes[at..]);
    private static void Need([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool condition, string reason)
    {
        if (!condition)
            throw new ToolFailure("sqlite_contract_" + reason);
    }
}
