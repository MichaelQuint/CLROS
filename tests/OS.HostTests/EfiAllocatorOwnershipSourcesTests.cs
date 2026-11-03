using OS.Tooling;

internal static class EfiAllocatorOwnershipSourcesTests
{
    [HostTest("EFI allocator ownership spans moved partial files without admitting foreign calls")]
    private static void IndependentDocuments()
    {
        var snapshot = new EfiAllocatorOwnershipSources(EfiOptions.Parse(M06PackageReadEfiIntegrationTests.Flags, false).Profile());
        var movedFat = snapshot.MoveMethod("ReadFatSector", "M06_FAT_READ_PROBE");
        var movedPayload = snapshot.MoveMethod("StagePackagePayload", "M06_PACKAGE_READ_PROBE");
        var movedDiagnose = snapshot.MoveMethod("Diagnose", "M03_FIRST_REAL_EXIT_BOOT_SERVICES_PROBE");
        string moved = movedFat.Keys.Single(path => !snapshot.Sources.ContainsKey(path));
        var missing = new Dictionary<string, string>(movedFat, StringComparer.Ordinal);
        missing.Remove(moved);
        var wrongOwner = new Dictionary<string, string>(movedFat, StringComparer.Ordinal)
        {
            [moved] = movedFat[moved].Replace("class M03FirstExitAdapter", "class WrongAdapter", StringComparison.Ordinal)
        };
        var wrongCondition = new Dictionary<string, string>(movedFat, StringComparer.Ordinal)
        {
            [moved] = movedFat[moved].Replace("#if M06_FAT_READ_PROBE", "#if M06_PACKAGE_READ_PROBE", StringComparison.Ordinal)
        };
        var duplicate = new Dictionary<string, string>(snapshot.Sources, StringComparer.Ordinal) { [moved] = movedFat[moved] };
        var malformed = new Dictionary<string, string>(snapshot.Sources, StringComparer.Ordinal) { ["invalid-part.cs"] = "class Invalid {" };
        IReadOnlyDictionary<string, string> ForeignOwner(IReadOnlyDictionary<string, string> sources)
        {
            string changed = sources.Keys.Single(path => !snapshot.Sources.ContainsKey(path));
            return new Dictionary<string, string>(sources, StringComparer.Ordinal)
            {
                [changed] = sources[changed].Replace("class M03FirstExitAdapter", "class WrongAdapter", StringComparison.Ordinal)
            };
        }
        foreach (string call in new[] { "BootPageAllocatorMutator.AllocateOnePage", "BootPageAllocatorMutator.FreeOnePage" })
        {
            foreach (var sources in new[] { snapshot.Sources, movedFat, movedPayload, movedDiagnose })
            {
                HostCheck.True(EfiProfileTestControls.RemoveOwnedDmaCalls(sources, call, out var stripped, out string reason), reason);
                HostCheck.True(!stripped.Values.Any(text => text.Contains(call, StringComparison.Ordinal)), "every admitted allocator expression is removed");
            }
            var rogue = new Dictionary<string, string>(snapshot.Sources, StringComparer.Ordinal)
            {
                ["foreign-part.cs"] = "namespace Foreign; static class Rogue { static void Run() { " + call + "(); } }"
            };
            foreach (var rejected in new[] { missing, wrongOwner, wrongCondition, duplicate, malformed, rogue,
                ForeignOwner(movedPayload), ForeignOwner(movedDiagnose) })
                HostCheck.True(!EfiProfileTestControls.RemoveOwnedDmaCalls(rejected, call, out _, out _));
        }
        var cooperative = new EfiAllocatorOwnershipSources(EfiOptions.Parse(["--EnableM09004Probe"], false).Profile());
        HostCheck.True(!EfiProfileTestControls.RemoveOwnedDmaCalls(cooperative.ChangeCooperativeReceipt(),
            "BootPageAllocatorMutator.FreeOnePage", out _, out _), "the original allocation receipt remains required across documents");
    }

    [HostTest("EFI retained pool materializer follows moved partial owners and rejects missing or foreign owners")]
    private static void MaterializerDocuments()
    {
        // The admission control inspects both profiles. Keep the cooperative
        // member present in the timer profile when moving it between files.
        foreach (var (flag, method, condition) in new[]
        {
            ("EnableM09004Probe", "RunCooperativeSwitch", "M09_004_PROBE || M09_005_PROBE"),
            ("EnableM09005Probe", "RunSchedulerTimer", "M09_005_PROBE")
        })
        {
            var snapshot = new EfiAllocatorOwnershipSources(EfiOptions.Parse(["--" + flag], false).Profile());
            var moved = snapshot.MoveMethod(method, condition);
            string path = moved.Keys.Single(candidate => !snapshot.Sources.ContainsKey(candidate));
            foreach (var sources in new[] { snapshot.Sources, moved })
            {
                HostCheck.True(EfiProfileTestControls.RemoveOwnedCooperativeMaterializer(sources, out var stripped, out string reason), reason);
                HostCheck.True(!stripped.Values.Any(text => text.Contains("BootPageAllocatorStateMaterializer", StringComparison.Ordinal)));
            }
            var missing = new Dictionary<string, string>(moved, StringComparer.Ordinal);
            missing.Remove(path);
            var foreign = new Dictionary<string, string>(moved, StringComparer.Ordinal)
            {
                [path] = moved[path].Replace("class M03FirstExitAdapter", "class WrongAdapter", StringComparison.Ordinal)
            };
            var duplicate = new Dictionary<string, string>(snapshot.Sources, StringComparer.Ordinal) { [path] = moved[path] };
            var rogue = new Dictionary<string, string>(snapshot.Sources, StringComparer.Ordinal)
            {
                ["foreign-materializer.cs"] = "namespace Foreign; static class Rogue { static void Run() { BootPageAllocatorStateMaterializer.Materialize(); } }"
            };
            foreach (var rejected in new[] { missing, foreign, duplicate, rogue })
                HostCheck.True(!EfiProfileTestControls.RemoveOwnedCooperativeMaterializer(rejected, out _, out _));
        }
    }
}
