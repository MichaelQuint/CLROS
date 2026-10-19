using System.Text;
using OS.Tooling;

internal static class NativeAotServiceStartupReplayTests
{
    private const ulong Writer = 0x100000001, Binder = 0x100000005;
    private const string Prefix = "CLROS_APP_ENTRY minimal.api.service\nCLROS_APP_CCTOR minimal.api.service\n" +
        "CLROS_APP_CTOR minimal.api.service\nCLROS_APP_BUILT minimal.api.service\n";
    private const string Listening = "CLROS_SERVICE_LISTEN contract=v1 endpoint=0.0.0.0:8080\n";
    private const string GcBegin = "CLROS_SERVICE_GC_BEGIN limit=16777216 baselineHeap=30920 baselineCommitted=151552 liveBytes=2097152 liveHeap=2129504 liveCommitted=4165632\n";
    private static readonly string[] Markers = ["TEST_START m16.014.startup", "M16_014_ENTER ", "M16_014_CONFIG ",
        "M16_014_STARTUP ", "M16_014_BIND ", "M16_014_LISTEN ", "M16_014_LIVE ", "CLROS_M16_014_LISTENING", "TEST_PASS m16.014.startup"];

    [HostTest("service v2 startup requires explicit mode and exact copied fd2 listening with three environment entries")]
    private static void LifecycleStartup()
    {
        foreach (bool segmented in new[] { false, true })
        {
            string raw = Raw(segmented, prefix: Prefix + GcBegin, listening: Listening.Replace("contract=v1", "contract=v2", StringComparison.Ordinal))
                .Replace("envc=2", "envc=3", StringComparison.Ordinal);
            var report = Read(raw, lifecycle: true);
            HostCheck.Equal(Writer, report.WriterIdentity); HostCheck.Equal(6, report.ListenerDescriptor);
            HostCheck.Equal((uint)Encoding.UTF8.GetByteCount(NativeAotServicePackage.LifecycleSettingsJson), report.SettingsBytes);
            Reject(raw); Reject(Raw(segmented), lifecycle: true);
        }
    }

    [HostTest("service v2 startup rejects a GC witness interleaved into the four required constructor lines")]
    private static void LifecyclePrefixOrder()
    {
        string prefix = Prefix.Replace("CLROS_APP_CCTOR", GcBegin + "CLROS_APP_CCTOR", StringComparison.Ordinal);
        string raw = Raw(prefix: prefix, listening: Listening.Replace("contract=v1", "contract=v2", StringComparison.Ordinal))
            .Replace("envc=2", "envc=3", StringComparison.Ordinal);
        Reject(raw, lifecycle: true);
    }

    [HostTest("service v2 startup rejects crossed or unknown listening versions and inconsistent environment metadata")]
    private static void LifecycleStartupControls()
    {
        string raw = Raw(listening: Listening.Replace("contract=v1", "contract=v2", StringComparison.Ordinal))
            .Replace("envc=2", "envc=3", StringComparison.Ordinal);
        foreach (string bad in new[] {
            raw.Replace("contract=v2", "contract=v1", StringComparison.Ordinal), raw.Replace("contract=v2", "contract=v3", StringComparison.Ordinal),
            Change(raw, "M16_014_ENTER ", r => r.Replace("envc=3", "envc=2", StringComparison.Ordinal)),
            Change(raw, "M16_014_LIVE ", r => r.Replace("envc=3", "envc=4", StringComparison.Ordinal)),
            Change(raw, "M16_014_ENTER ", r => r.Replace("envc=3", "envc=4294967299", StringComparison.Ordinal)) })
            Reject(bad, lifecycle: true);
    }

    [HostTest("service startup replay binds scalar and segmented vector prefixes configuration and fd2 listening", HostLane.Fast, 0)]
    private static void Startup()
    {
        foreach (bool segmented in new[] { false, true })
        {
            byte[] raw = Encoding.UTF8.GetBytes(Raw(segmented)), before = raw.ToArray();
            var report = QemuTranscripts.InspectNativeAotServiceStartupForTests(raw);
            HostCheck.Equal(Writer, report.WriterIdentity); HostCheck.Equal(6, report.ListenerDescriptor);
            HostCheck.Equal((uint)Encoding.UTF8.GetByteCount(NativeAotServicePackage.SettingsJson), report.SettingsBytes);
            HostCheck.Equal(3, report.SettingsDescriptor); HostCheck.Equal(Writer, report.SettingsIdentity);
            HostCheck.Equal(3UL, report.SettingsOrdinal); HostCheck.True(raw.AsSpan().SequenceEqual(before));
            Array.Fill(raw, (byte)0); HostCheck.Equal(Writer, report.WriterIdentity);
        }
    }

    [HostTest("service startup replay rejects missing duplicate unknown reordered and payload-forged native observations", HostLane.Fast, 1)]
    private static void MarkersAndPayloads()
    {
        string raw = Raw();
        foreach (string marker in Markers)
        {
            string row = raw.Split("\r\n").Single(line => line.StartsWith(marker, StringComparison.Ordinal));
            Reject(raw.Replace(row + "\r\n", "", StringComparison.Ordinal));
            Reject(raw.Replace(row + "\r\n", row + "\r\n" + row + "\r\n", StringComparison.Ordinal));
            // Identical marker text inside a genuine copied fd2 envelope cannot become native metadata.
            Reject(raw.Replace(row + "\r\n", Frame(row + "\n"), StringComparison.Ordinal));
        }
        Reject(raw + "M16_014_UNKNOWN value=1\r\n");
        Reject(raw + "M10_005_LOGS stdout=32 stderr=32 writers=8 writev=32 invalidErrno=9\r\n");
        Reject(raw + "TEST_FAIL m16.014.startup substituted\r\n");
        Reject(raw[..^1]);
        string progress = "M16_014_PROGRESS identity=4294967297 calls=4097 elapsed=300001\r\n";
        _ = Read(raw + progress); // Progress payload remains the shared replay consumer's responsibility.
        Reject(raw.Replace("TEST_PASS m16.014.startup\r\n", progress + "TEST_PASS m16.014.startup\r\n", StringComparison.Ordinal));
        Reject(Raw(prefix: Prefix.Replace("minimal.api.service", "minimal.api.smoke", StringComparison.Ordinal)));
        Reject(Raw(prefix: Prefix.Replace("CLROS_APP_CTOR minimal.api.service", "CLROS_APP_CTOR minimal.api.smoke", StringComparison.Ordinal)));
        Reject(Raw(listening: "CLROS_SERVICE_LISTEN\n"));
        Reject(Raw(listenChannel: 1));
    }

    [HostTest("service startup replay rejects substituted canonical policy identities descriptors and live counters", HostLane.Fast, 2)]
    private static void Substitutions()
    {
        string raw = Raw();
        var changes = new (string Row, string Before, string After)[]
        {
            ("M16_014_ENTER ", "identity=4294967297", "identity=4294967301"),
            ("M16_014_ENTER ", "rip=4198402", "rip=4198403"),
            ("M16_014_ENTER ", "argc=5", "argc=6"), ("M16_014_ENTER ", "envc=2", "envc=3"),
            ("M16_014_ENTER ", "argc=5", "argc=05"),
            ("M16_014_CONFIG ", "fd=3", "fd=4"),
            ("M16_014_CONFIG ", "source=immutable-package", "source=unverified-package"),
            ("M16_014_STARTUP ", "writer=4294967297", "writer=4294967301"),
            ("M16_014_STARTUP ", "stages=4", "stages=3"),
            ("M16_014_BIND ", "identity=4294967301", "identity=4294967297"),
            ("M16_014_BIND ", "fd=6", "fd=7"), ("M16_014_LISTEN ", "fd=6", "fd=7"),
            ("M16_014_LISTEN ", "address=0", "address=1"), ("M16_014_LISTEN ", "port=8080", "port=8081"),
            ("M16_014_LIVE ", "argc=5", "argc=6"), ("M16_014_LIVE ", "envc=2", "envc=3"),
            ("M16_014_LIVE ", "bindFd=6", "bindFd=7"), ("M16_014_LIVE ", "stdoutBytes=0", "stdoutBytes=1"),
            ("M16_014_LIVE ", "active=2", "active=0"), ("M16_014_LIVE ", "clock=60", "clock=59"),
            ("M16_014_LIVE ", "clock=60", "clock=71")
        };
        foreach (var change in changes) Reject(Change(raw, change.Row, row => row.Replace(change.Before, change.After, StringComparison.Ordinal)));
        uint settings = (uint)Encoding.UTF8.GetByteCount(NativeAotServicePackage.SettingsJson);
        foreach (string marker in new[] { "M16_014_ENTER ", "M16_014_CONFIG ", "M16_014_LIVE " })
            Reject(Change(raw, marker, row => row.Replace((marker == "M16_014_CONFIG " ? "bytes=" : "configBytes=") + settings,
                (marker == "M16_014_CONFIG " ? "bytes=" : "configBytes=") + (settings + 1), StringComparison.Ordinal)));
        Reject(Change(raw, "M16_014_LIVE ", row => row.Replace("stderrBytes=" + (Prefix.Length + Listening.Length),
            "stderrBytes=" + (Prefix.Length + Listening.Length - 1), StringComparison.Ordinal)));
    }

    [HostTest("service startup replay rejects orphan copies wrong calls returns generations and premature readiness", HostLane.Fast, 3)]
    private static void CallBindings()
    {
        string raw = Raw();
        string WriteCall(string row) => row.StartsWith("M16_008_CALL ", StringComparison.Ordinal) && row.Contains("ordinal=4 ", StringComparison.Ordinal) ? row : "";
        foreach (var change in new[] { ("nr=1", "nr=39"), ("a0=2", "a0=1"), ("action=1 status=1", "action=2 status=1"),
            ("action=1 status=1", "action=1 status=2") })
        {
            string modified = ChangeWhere(raw, row => WriteCall(row).Length != 0, row => row.Replace(change.Item1, change.Item2, StringComparison.Ordinal));
            Reject(modified);
        }
        Reject(ChangeWhere(raw, row => WriteCall(row).Length != 0, row => row.Replace("returnValueBits=" + Built().Length,
            "returnValueBits=" + (Built().Length - 1), StringComparison.Ordinal)));
        Reject(ChangeWhere(raw, row => WriteCall(row).Length != 0, row => row.Replace("a2=" + Built().Length, "a2=1", StringComparison.Ordinal)));
        Reject(ChangeWhere(Raw(segmented: true), row => WriteCall(row).Length != 0,
            row => row.Replace("a2=4", "a2=0", StringComparison.Ordinal)));
        Reject(ChangeWhere(raw, row => WriteCall(row).Length != 0, row => row.Replace("identity=" + Writer,
            "identity=" + (Writer + (1UL << 32)), StringComparison.Ordinal).Replace("ordinal=4 ", "ordinal=1 ", StringComparison.Ordinal)));
        Reject(ChangeWhere(raw, row => row.StartsWith("M16_008_CALL ", StringComparison.Ordinal) && row.Contains(" nr=17 ", StringComparison.Ordinal),
            row => row.Replace("a0=3", "a0=4", StringComparison.Ordinal)));
        Reject(ChangeWhere(raw, row => row.StartsWith("M16_008_CALL ", StringComparison.Ordinal) && row.Contains(" nr=49 ", StringComparison.Ordinal),
            row => row.Replace("valueBits=0 action=1", "valueBits=18446744073709551518 action=1", StringComparison.Ordinal)
                .Replace("returnValueBits=0", "returnValueBits=18446744073709551518", StringComparison.Ordinal)));
        Reject(ChangeWhere(raw, row => row.StartsWith("M16_008_CALL ", StringComparison.Ordinal) && row.Contains(" nr=50 ", StringComparison.Ordinal),
            row => row.Replace("a0=6", "a0=7", StringComparison.Ordinal)));
        string liveRow = raw.Split("\r\n").Single(row => row.StartsWith("M16_014_LIVE ", StringComparison.Ordinal));
        string reordered = raw.Replace(liveRow + "\r\n", "", StringComparison.Ordinal)
            .Replace("M16_014_LISTEN fd=6", liveRow + "\r\nM16_014_LISTEN fd=6", StringComparison.Ordinal);
        Reject(reordered);
    }

    private static string Built() => "CLROS_APP_BUILT minimal.api.service\n";
    private static string Frame(string text, uint channel = 2) => $"M16_005_WRITE fd={channel} bytes={Encoding.UTF8.GetByteCount(text)} data={text}\r\n";
    private static string Raw(bool segmented = false, string? prefix = null, string listening = Listening, uint listenChannel = 2)
    {
        prefix ??= Prefix; int last = prefix.LastIndexOf("CLROS_APP_BUILT", StringComparison.Ordinal);
        string beforeBuilt = prefix[..last], built = prefix[last..];
        uint settings = (uint)Encoding.UTF8.GetByteCount(NativeAotServicePackage.SettingsJson);
        List<string> rows = ["TEST_START m16.014\r\n", "TEST_START m16.014.startup\r\n",
            $"M16_014_ENTER identity={Writer} rip=4198402 argc=5 envc=2 configBytes={settings}\r\n"];
        Dictionary<ulong, ulong> ordinals = []; ulong clock = 0;
        void Call(ulong identity, ulong nr, ulong fd, ulong count, ulong value)
        {
            ordinals.TryGetValue(identity, out ulong ordinal); ordinals[identity] = ++ordinal; clock += 10;
            rows.Add($"M16_008_CALL identity={identity} nr={nr} a0={fd} a1=4096 a2={count} a3=0 a4=0 a5=0 rip=4198402 rsp=1879048184 flags=514 fs=0 gs=0 clock={clock} valueBits={value} action=1 status=1 ordinal={ordinal}" +
                $" waitSource=0 waitDeadline=0 waitSequence=0 signalDepth=0 signalFrame=0 blockedSignals=0 returnRip=4198402 returnRsp=1879048184 returnFlags=514 returnFs=0 returnGs=0 returnValueBits={value}\r\n");
        }
        void Write(ulong identity, string text, string metadata = "", uint channel = 2)
        {
            string[] parts = segmented ? [text[..1], text[1..9], text[9..17], text[17..]] : [text];
            foreach (string part in parts) rows.Add(Frame(part, channel));
            rows.Add(metadata); Call(identity, segmented ? 20UL : 1UL, channel, segmented ? (ulong)parts.Length : (ulong)text.Length, (ulong)text.Length);
        }
        Call(Writer, 12, 0, 0, 0);
        Write(Writer, beforeBuilt);
        rows.Add($"M16_014_CONFIG fd=3 bytes={settings} source=immutable-package copied=user\r\n");
        Call(Writer, 17, 3, 4096, settings);
        Write(Writer, built, $"M16_014_STARTUP writer={Writer} stages=4\r\n");
        rows.Add($"M16_014_BIND identity={Binder} fd=6\r\n"); Call(Binder, 49, 6, 16, 0);
        rows.Add("M16_014_LISTEN fd=6 address=0 port=8080 owner=live\r\n"); Call(Binder, 50, 6, 512, 0);
        string live = $"M16_014_LIVE argc=5 envc=2 configBytes={settings} bindFd=6 stdoutBytes={(listenChannel == 1 ? listening.Length : 0)} stderrBytes={prefix.Length + (listenChannel == 2 ? listening.Length : 0)} active=2 clock={clock} owner=retained externalHttp=required\r\n" +
            "CLROS_M16_014_LISTENING\r\nTEST_PASS m16.014.startup\r\n";
        Write(Binder, listening, live, listenChannel);
        return string.Concat(rows);
    }
    private static string Change(string raw, string prefix, Func<string, string> change) =>
        ChangeWhere(raw, row => row.StartsWith(prefix, StringComparison.Ordinal), change);
    private static string ChangeWhere(string raw, Func<string, bool> select, Func<string, string> change)
    {
        string[] rows = raw.Split("\r\n"); int at = Array.FindIndex(rows, row => select(row));
        HostCheck.True(at >= 0); string before = rows[at]; rows[at] = change(before); HostCheck.True(rows[at] != before);
        return string.Join("\r\n", rows);
    }
    private static QemuTranscripts.ServiceStartupInspection Read(string raw, bool lifecycle = false) =>
        QemuTranscripts.InspectNativeAotServiceStartupForTests(Encoding.UTF8.GetBytes(raw), lifecycle);
    private static void Reject(string raw, bool lifecycle = false)
    { bool rejected = false; try { _ = Read(raw, lifecycle); } catch (ToolFailure) { rejected = true; } HostCheck.True(rejected); }
}
