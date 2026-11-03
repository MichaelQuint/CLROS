using OS.Tooling;
using OS.Tools.QemuRunner;

internal static class NormalFinallyTranscriptTests
{
    [HostTest("m04.021.normal-finally numeric associations and strict framing", HostLane.Fast, 0)]
    private static void Transcript()
    {
        string[] original = M04GCStaticRootMaterializationEfiIntegrationTests.Lines;
        int first = Array.FindIndex(original, line => line.StartsWith("M04_GC_ROOT ", StringComparison.Ordinal)), insert = Array.IndexOf(original, "TEST_START boot.sequence");
        ulong[] image = EfiTranscriptControls.Fields(original[first]), module = EfiTranscriptControls.Fields(original[first + 1]),
            stack = EfiTranscriptControls.Fields(original[first + 2]), canaries = EfiTranscriptControls.Fields(original[first + 7]);
        ulong backing = EfiTranscriptControls.Fields(original[first + 4])[0] - 32, seed = (module[3] ^ backing) & 0xffff;
        ulong obj = backing + 184, type = image[0] + 0x5000, method = image[0] + 0x1000, thrower = method + 0x200, table = image[0] + 0x6001;
        ulong rsp = stack[2] - 0x1000, parentRsp = rsp + 96, bp = parentRsp + 64, local = bp - 8;
        string Row(int stage, params ulong[] values) => EfiTranscriptControls.Row("M04_NORMAL_FINALLY", stage, values);
        string[] child = ["TEST_START m04.021.normal-finally",Row(0,module[3],image[0]+0x10000,4000,300),Row(1,obj,type,9,3),
            Row(2,1,1,0,0),Row(3,obj,local,seed+2,1),Row(4,thrower+9,(thrower+9)^1,rsp,0),
            Row(5,thrower+9,rsp,method+115,rsp+48),Row(6,method+115,rsp+48,method+61,parentRsp),Row(7,method,173,table,11),
            Row(8,type+0x100,method+121,bp,0),Row(9,obj,seed,seed+2,1),Row(10,method+62,parentRsp,bp,1),
            Row(11,obj,seed,seed+1,1),Row(12,local,seed+2,1,1),Row(13,7,rsp-14592,canaries[1],canaries[2]),
            Row(14,obj,type,module[3],9),"TEST_PASS m04.021.normal-finally","CLROS_M04_021_NORMAL_FINALLY_READY"];
        string[] lines = [.. original[..insert], .. child, .. original[insert..]];
        HostCheck.True(lines.Length == 88, "independent whole inherited transcript");
        string folder = Path.Combine(HostToolProcess.FindRepositoryRoot(), "artifacts/normal-finally-transcript", Guid.NewGuid().ToString("N"));
        var controls = new EfiTranscriptControls(folder, path => QemuTranscripts.AssertM04ManagedHeapPageRangeTranscript(path, includeGcStatics: true, includeNormalFinally: true));
        controls.Check(lines, true, "independent synthetic vector; no guest proof");
        var markers = QemuContracts.M04NormalFinallyRequiredMarkers;
        var state = new SerialMarkerStateMachine(markers, QemuContracts.M04ManagedHeapPageRangeForbiddenMarkers, orderedMarkers: markers, uniqueMarkers: markers);
        HostCheck.True(state.Feed(System.Text.Encoding.ASCII.GetBytes(EfiTranscriptControls.Text(lines))).Succeeded && state.Complete(false).Passed, "complete exact marker streamer");
        foreach (int stage in Enumerable.Range(1, 12).Append(14))
            for (int field = 0; field < 4; field++)
            {
                if (stage == 7 && field == 2)
                    continue; // Exact table RVA is joined to original native evidence, not inferred from a numeric interval.
                string[] changed = [.. lines];
                string[] tokens = changed[insert + stage + 1].Split(' ');
                tokens[field + 2] = "0x" + (Convert.ToUInt64(tokens[field + 2][2..], 16) ^ 1).ToString("X16");
                changed[insert + stage + 1] = string.Join(' ', tokens);
                controls.Check(changed, false, "numeric association " + stage + ":" + field);
            }
        foreach (var (stage, field, value) in new[]{(0,0,0UL),(0,1,ulong.MaxValue),(0,2,0UL),(0,3,0UL),
            (5,0,ulong.MaxValue),(5,1,0UL),(6,3,ulong.MaxValue),(7,2,0UL),(7,2,ulong.MaxValue),(7,2,method),
            (7,2,image[0]+0x10000),(13,0,6UL),(13,1,0UL),(13,2,0UL),(13,3,0UL)})
        {
            string[] changed = [.. lines];
            string[] tokens = changed[insert + stage + 1].Split(' ');
            tokens[field + 2] = "0x" + value.ToString("X16");
            changed[insert + stage + 1] = string.Join(' ', tokens);
            controls.Check(changed, false, "range/alias " + stage + ":" + field);
        }
        controls.Structure(lines, 0, lines.Length);
        string text = EfiTranscriptControls.Text(lines);
        foreach (string suffix in new[] { "\r\n", "OS_SMOKE_PASS\r\n", "x", "\0" })
            controls.Check(text + suffix, false, "terminal suffix");
        controls.Check(text[..^1], false, "partial final CRLF");
        controls.Check(text.Replace("\r\n", "\n", StringComparison.Ordinal), false, "bare LF");
        controls.Check(text.Replace("M04_NORMAL_FINALLY 3", "M04_NORMAL_FINALLY\0 3", StringComparison.Ordinal), false, "control byte");
        controls.Check("unreviewed firmware\r\n" + text, false, "unreviewed preamble");
        string validPath = Path.Combine(folder, "valid.log");
        File.WriteAllText(validPath, text);
        foreach (string collision in new[] { "frames", "catalog", "nested", "filtered" })
        {
            bool rejected = false;
            try
            {
                QemuTranscripts.AssertM04ManagedHeapPageRangeTranscript(validPath, includeGcStatics: true, includeNormalFinally: true,
                includeFrameDispatch: collision == "frames", includeEhCatalog: collision == "catalog", includeNestedFinally: collision == "nested", includeFilteredCatch: collision == "filtered");
            }
            catch (ToolFailure e) { rejected = e.Reason == "m04_normal_finally_profile_collision"; }
            HostCheck.True(rejected, "profile collision " + collision);
        }
        controls.Complete("normal-finally.synthetic");
    }

    [HostTest("m04.021.normal-finally unselected records remain rejected", HostLane.Fast, 1)]
    private static void Unselected()
    {
        var ordinary = QemuProfiles.Create(QemuSmokeOptions.Parse(["--dry-run"]));
        foreach (string text in new[] { "M04_NORMAL_FINALLY 0", "CLROS_M04_021_NORMAL_FINALLY_READY", "TEST_START m04.021.normal-finally" })
        {
            bool rejected = false;
            try
            {
                QemuTranscripts.ValidateUnselectedRecords(text, ordinary);
            }
            catch (ToolFailure) { rejected = true; }
            HostCheck.True(rejected, "unselected normal fixture");
        }
        QemuTranscripts.AssertM04SimpleCatchSelection("TEST_START m04.021.normal-finally", false, normalFinallyEnabled: true);
    }
}
