using OS.Tooling;
using OS.Kernel.Runtime;
using OS.Tools.QemuRunner;

internal static class FrameDispatchTranscriptTests
{
    internal static string[] Arguments => [..RuntimeProfilePortTests.RethrowFlags[..^1].Select(n=>"--Enable"+n+"Probe"),
        "--EnableM04FrameDispatchProbe"];

    [HostTest("m04.021.frames transcript physical contexts and retained identity")]
    private static void Transcript()
    {
        string[] original = M04GCStaticRootMaterializationEfiIntegrationTests.Lines;
        int first = Array.FindIndex(original, l => l.StartsWith("M04_GC_ROOT ", StringComparison.Ordinal)), insert = Array.IndexOf(original, "TEST_START boot.sequence");
        ulong[] image = EfiTranscriptControls.Fields(original[first]), module = EfiTranscriptControls.Fields(original[first + 1]),
            stack = EfiTranscriptControls.Fields(original[first + 2]), canaries = EfiTranscriptControls.Fields(original[first + 7]);
        ulong backing = EfiTranscriptControls.Fields(original[first + 4])[0] - 32, seed = (module[3] ^ backing) & 0xffff;
        ulong obj = backing + 184, type = image[0] + 0x5000, method = image[0] + 0x1000, middle = method + 0x100, catcher = method + 0x200;
        ulong rsp = stack[2] - 0x1000, middleRsp = rsp + 48, catchRsp = rsp + 112;
        string R(int stage, params ulong[] values) => EfiTranscriptControls.Row("M04_FRAMES", stage, values);
        string[] child = ["TEST_START m04.021.frames",R(0,module[3],image[0]+0x10000,4000,300),R(1,obj,type,9,3),R(2,1,1,0,0),
            R(3,method+9,rsp,middle+25,middleRsp),R(4,middle+25,middleRsp,catcher+46,catchRsp),
            R(5,(ulong)BoundedExceptionFrameSearchStatus.InvalidFrame,(ulong)BoundedExceptionFrameSearchStatus.DispatchRejected,
                (ulong)BoundedExceptionDispatchStatus.InvalidClause,0),R(6,1,0,1,1),R(7,middle+49,catcher+96,middleRsp+40,catchRsp+40),
            R(8,obj,middleRsp+40,seed+4,1),R(9,middle+49,middleRsp,catchRsp,obj),R(10,obj,seed,seed+3,1),
            R(11,catcher+47,catchRsp,catchRsp+64,1),R(12,obj,seed+1,seed+3,1),R(13,10,rsp-15456,canaries[1],canaries[2]),
            R(14,obj,type,module[3],9),"TEST_PASS m04.021.frames","CLROS_M04_021_FRAMES_READY"];
        string[] lines = [.. original[..insert], .. child, .. original[insert..]];
        HostCheck.True(lines.Length == original.Length + 18, "whole inherited success transcript");
        string folder = Path.Combine(HostToolProcess.FindRepositoryRoot(), "artifacts/frame-transcripts", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string Text(string[] rows) => string.Join("\r\n", rows) + "\r\n";
        void Check(string name, string text, bool pass)
        {
            string path = Path.Combine(folder, name + ".log");
            File.WriteAllText(path, text);
            bool accepted = true;
            try
            {
                QemuTranscripts.AssertM04ManagedHeapPageRangeTranscript(path, includeGcStatics: true, includeFrameDispatch: true);
            }
            catch (ToolFailure)
            {
                accepted = false;
            }
            HostCheck.True(accepted == pass, name);
        }
        Check("valid", Text(lines), true);
        var plan = QemuProfiles.Create(QemuSmokeOptions.Parse(["--dry-run", "--build-efi", .. Arguments]));
        var state = new SerialMarkerStateMachine(plan.Required, plan.Forbidden, orderedMarkers: plan.Ordered, uniqueMarkers: plan.Unique);
        HostCheck.True(state.Feed(System.Text.Encoding.ASCII.GetBytes(Text(lines))).Succeeded && state.Complete(false).Passed);
        foreach (int stage in new[] { 1, 2, 4, 5, 6, 7, 8, 9, 10, 11, 12, 14 })
            for (int field = 0; field < 4; field++)
            {
                string[] changed = [.. lines], tokens = changed[insert + stage + 1].Split(' ');
                tokens[field + 2] = "0x" + (Convert.ToUInt64(tokens[field + 2][2..], 16) ^ 1).ToString("X16");
                changed[insert + stage + 1] = string.Join(' ', tokens);
                Check("field-" + stage + "-" + field, Text(changed), false);
            }
        foreach ((int stage, int field, ulong value) in new[]{(0,0,0UL),(0,1,ulong.MaxValue),(0,2,0UL),(0,3,0UL),
            (3,0,ulong.MaxValue),(3,1,0UL),(3,2,0UL),(3,3,ulong.MaxValue),(13,0,9UL),(13,1,0UL),(13,2,0UL),(13,3,0UL)})
        {
            string[] changed = [.. lines], tokens = changed[insert + stage + 1].Split(' ');
            tokens[field + 2] = "0x" + value.ToString("X16");
            changed[insert + stage + 1] = string.Join(' ', tokens);
            Check("range-" + stage + "-" + field, Text(changed), false);
        }
        for (int i = 0; i < lines.Length; i++)
        {
            Check("missing-" + i, Text([.. lines[..i], .. lines[(i + 1)..]]), false);
            Check("duplicate-" + i, Text([.. lines[..i], lines[i], .. lines[i..]]), false);
        }
        foreach (string ending in new[] { "\r\n", "OS_SMOKE_PASS\r\n", "TEST_START boot.sequence\r\n", "x", "\0" })
            Check("suffix-" + ending.Length, Text(lines) + ending, false);
        Check("truncated", Text(lines)[..^1], false);
        Check("lf", Text(lines).Replace("\r\n", "\n", StringComparison.Ordinal), false);
        Check("control", Text(lines).Replace("M04_FRAMES 10", "M04_FRAMES\0 10", StringComparison.Ordinal), false);
    }

    [HostTest("m04.021.frames profile isolation and ordinary completion")]
    private static void Profile()
    {
        var plan = QemuProfiles.Create(QemuSmokeOptions.Parse(["--dry-run", "--build-efi", .. Arguments]));
        HostCheck.True(plan.ReplaceDefaultRequiredMarkers && plan.Required.SequenceEqual(QemuContracts.M04FrameDispatchRequiredMarkers));
        HostCheck.True(plan.Required[^1] == "OS_SMOKE_PASS" && plan.Forbidden.Contains("OS_SMOKE_FAIL"));
        QemuTranscripts.ValidateUnselectedRecords("TEST_START m04.021.frames", plan);
        bool rejected = false;
        try
        {
            QemuTranscripts.ValidateUnselectedRecords("M04_FRAMES 0", QemuProfiles.Create(QemuSmokeOptions.Parse(["--dry-run"])));
        }
        catch (ToolFailure)
        {
            rejected = true;
        }
        HostCheck.True(rejected);
        foreach (string[] args in new string[][]{["--dry-run",..Arguments],["--dry-run","--build-efi","--skip-image-build",..Arguments],
            ["--dry-run","--build-efi",..Arguments,"--require-marker","x"],["--dry-run","--build-efi",..Arguments,"--EnableM04UnhandledExceptionProbe"],
            ["--dry-run","--build-efi","--boot-policy","VerifiedExit",..Arguments]})
        {
            rejected = false;
            try
            {
                _ = QemuProfiles.Create(QemuSmokeOptions.Parse(args));
            }
            catch (ToolFailure)
            {
                rejected = true;
            }
            HostCheck.True(rejected, "invalid frame selection");
        }
    }
}
