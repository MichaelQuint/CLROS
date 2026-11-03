using System.Security.Cryptography;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Inspect a borrowed canonical rejection tail beginning at
    /// m03.007. Exact source-fault case, unavailable owner, pre-entry state,
    /// released startup backing/handles and terminal preparation96 failure are
    /// mandatory. Unsupported/malformed/extra/success rows throw. No execution
    /// or firmware/native/runner authority is established or input retained.</summary>

    public static RealtimeAdmissionRejection InspectRealtimeAdmissionRejectionForTests(string[] rows, string control)
    {
        Need(control is "Unavailable" or "Invalid", "realtime_rejection_control");
        Need(rows is not null && rows.Length == 9, "realtime_rejection_inventory");
        ExactInventory(rows!, ["TEST_START m03.007", "M16_005_PREPARE_REJECTED owned-backing=released", "M16_004_PREPARE_REJECTED owned-backing=released", "M16_002_STAGE_REJECTED executable=false backing=released", "M16_002_REJECTION_HANDLES closed=2", "TEST_START m16.019", "", "TEST_FAIL m16.019 admission", ""], 6, 8);
        var r = ReadRecord(rows![6], "M16_019_UTC_REJECT", "case:dec", "sourceStatus:dec", "month:dec", "available:dec", "firmware:dec", "entered:dec", "ready:dec");
        Need(r["case"] == (control == "Unavailable" ? 1UL : 2UL) && r["available"] == 0 && r["firmware"] == 1 && r["entered"] == 0 && r["ready"] == 0 && (control == "Unavailable" ? r["sourceStatus"] == 0x8000000000000003UL && r["month"] is >= 1 and <= 12 : r["sourceStatus"] == 0 && r["month"] == 0), "realtime_rejection_observation");
        const string prefix = "TEST_FAIL m03.007 preparation=96/";
        Need(rows[8].StartsWith(prefix, StringComparison.Ordinal), "realtime_rejection_checkpoint");
        string memory = rows[8][prefix.Length..];
        Need(memory.Length is> 0 and <= 2 && memory.All(c => c is >= '0' and <= '9') && uint.TryParse(memory, out uint kind) && kind <= 15 && memory == kind.ToString(System.Globalization.CultureInfo.InvariantCulture), "realtime_rejection_memory_type");
        return new((int)r["case"], r["sourceStatus"], (uint)r["month"], uint.Parse(memory, System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>Check an original bounded serial file's firmware prefix, exact
    /// pre-exit parent and dedicated UTC rejection tail through EOF. Locks the
    /// borrowed file while reading; stale/aliased/malformed captures throw.
    /// This is report-only: the caller must separately hold current original
    /// native/source/image and owned runner/cleanup receipts. It never grants
    /// ordinary web success, firmware exit or service readiness.</summary>

    public static RealtimeAdmissionRejection AssertRealtimeAdmissionRejectionCapture(string path, string control)
    {
        path = Path.GetFullPath(path);
        using AuthorityFiles files = new();
        var input = files.Open(Path.GetDirectoryName(path)!, path, "UTC rejection serial", "realtime_rejection_serial", 32768, singleLink: true);
        byte[] raw = input.ReadBytes(32768, "realtime_rejection_serial");
        Need(raw.Length > FirmwarePrefixBytes && Convert.ToHexString(SHA256.HashData(raw.AsSpan(0, FirmwarePrefixBytes))) == FirmwarePrefixSha256, "realtime_rejection_firmware_prefix");
        string text = DecodeUtf8(raw[FirmwarePrefixBytes..]);
        Need(text.EndsWith("\r\n", StringComparison.Ordinal) && !Controls.IsMatch(text.Replace("\r\n", "", StringComparison.Ordinal)), "realtime_rejection_framing");
        string[] rows = text[..^2].Split("\r\n", StringSplitOptions.None);
        string[] parent = QemuContracts.M03FirstExitRequiredMarkers.TakeWhile(r => r != "TEST_START m03.007").ToArray();
        Need(rows.Length == parent.Length + 9, "realtime_rejection_parent_inventory");
        string[] prefix = rows[..parent.Length];
        ExactInventory(prefix, parent, 3, 9, 10, 19, 20, 21, 22, 23, 24);
        ValidateAllocator(prefix);
        _ = AssertCopiedAcpiRecords(prefix);
        var result = InspectRealtimeAdmissionRejectionForTests(rows[parent.Length..], control);
        files.VerifyUnchanged();
        return result;
    }
}
