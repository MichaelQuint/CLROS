using System.Buffers.Binary;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

#if RHP_CORE_TEST
using OS.Boot;
using OS.Kernel.Boot;
using OS.Kernel.Memory;
using OS.Kernel.Runtime;
using OS.Runtime;

#else
internal static partial class M04RhpNewFastEfiIntegrationTests
{
    private const string Flag = "EnableM04RhpNewFastProbe";
    private static readonly string[] Chain = ["EnableM01LoadedImageStackProbe", "EnableM01LiveAllocatorProbe", "EnableM04IndependentEndianConformanceProbe", "EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe", "EnableM04ArithmeticConformanceProbe", "EnableM04LayoutContractProbe", "EnableM04ManagedHeapPageRangeProbe"];
    [HostTest("m04.007.efi actual POD core and bounded PE", HostLane.Native, 1)]
    private static void TestCore()
    {
        string root = HostToolProcess.FindRepositoryRoot(), directory = Scratch(root, "core");
        string includes = string.Join('\n', AllocationSdkSourcePaths().Select(path => "<Compile Include=\"" + SecurityElement.Escape(path) + "\"/>"));
        File.WriteAllText(Path.Combine(directory, "Core.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net11.0</TargetFramework><LangVersion>14.0</LangVersion>
            <OutputType>Exe</OutputType><AllowUnsafeBlocks>true</AllowUnsafeBlocks><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable>
            <EnableDefaultCompileItems>false</EnableDefaultCompileItems><DefineConstants>RHP_CORE_TEST</DefineConstants><NoWarn>0649</NoWarn></PropertyGroup><ItemGroup>
            """ + includes + "<ProjectReference Include=\"" + SecurityElement.Escape(Path.Combine(root, "src/OS.Kernel/OS.Kernel.csproj")) + "\"/></ItemGroup></Project>");
        Result result = Process(root, "dotnet", ["run", "--project", Path.Combine(directory, "Core.csproj"), "-c", "Release", "--nologo"]);
        File.WriteAllText(Path.Combine(directory, "process.log"), result.Output);
        File.WriteAllText(Path.Combine(directory, "exit.txt"), result.Code.ToString());
        Need(result.Code == 0 && result.Output.Contains("TEST_PASS rhp.core.tests", StringComparison.Ordinal), result.Output);
    }

    [HostTest("m04.007.efi source profile and original handoff", HostLane.Fast, 0)]
    private static void TestSource()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        XDocument p = XDocument.Load(Path.Combine(root, "src/OS/OS.csproj"));
        var catalog = AllocationSources();
        Need(OS.Tooling.ContractAudit.Inspect(root).Count == 0, "complete independent compiler/link/profile identity");
        _ = HostRhpAllocationChecks.KernelFamily(root, catalog);
        foreach (string flag in new[]
        {
            Flag,
            "EnableM04RhpNewFastBeforeBindFailureProbe",
            "EnableM04RhpNewFastExhaustionFailureProbe"
        }

        )
        {
            XElement value = p.Descendants(flag).Single();
            Need(value.Value == "false" && value.Attribute("Condition")?.Value == "'$(" + flag + ")' == ''", "default false " + flag);
        }

        foreach (string required in Chain)
            Need(p.Descendants("Error").Any(x => x.Attribute("Condition")?.Value == "'$(" + Flag + ")' == 'true' and '$(" + required + ")' != 'true'"), "explicit prerequisite " + required);

        string entry = HostRhpAllocationChecks.Handoff(catalog).ToFullString();
        int zero = entry.IndexOf("if (!M01StageTwoAdapter.ValidateManagedHeapBeforeBootPlan", StringComparison.Ordinal);
        int handoff = entry.IndexOf("M01StageTwoAdapter.RunRhpNewFastAfterZeroCheckpoint();", StringComparison.Ordinal);
        Need(zero >= 0 && handoff > zero && entry.IndexOf("EfiEntry.WriteBootSequencePlan(", zero, StringComparison.Ordinal) > handoff, "zero then allocation then boot sequence");
        string adapter = string.Join('\n', HostRhpAllocationSources.RuntimeFamily(catalog).Select(document => document.Text));
        string allocationBody = HostSourceText.Active(HostRhpAllocationChecks.CoreAllocation(catalog));
        HostRhpAllocationChecks.BumpAndBinder(catalog);
        Need(HostSourceText.Contains(allocationBody, "GuestTestRhpNewFast.TryAllocate(*access, bump, 80,") && HostSourceText.Contains(allocationBody, "if (token == 0 || token != expectedToken") && !adapter.Contains("Serialize(", StringComparison.Ordinal), "actual frozen call and no data-only publication");
        int snapshot = HostSourceText.IndexOf(allocationBody, "byte* bumpBefore = stackalloc byte[80];");
        int allocation = HostSourceText.IndexOf(allocationBody, "GuestTestRhpNewFast.TryAllocate(*access, bump, 80,");
        int rejection = HostSourceText.IndexOf(allocationBody, "!RejectedAllocationUnchanged(bump, bumpBefore, access->Backing, records->BeforeAttempt)");
        Need(snapshot >= 0 && snapshot < allocation && rejection > allocation && HostSourceText.Contains(allocationBody, "for (ulong i = 0; i < 80; i++) bumpBefore[i] = ((byte*)bump)[i];"), "all80-byte snapshot brackets the actual leaf");
        foreach (string forbidden in new[]
        {
            "FreeOnePage(",
            "FreePages(",
            "RhBox(",
            "new object[",
            "throw ",
            "System.Reflection",
            "System.Console"
        }

        )
            Need(!adapter.Contains(forbidden, StringComparison.Ordinal), "no expanded runtime " + forbidden);
    }

    [HostTest("m04.007.efi source admission and strict firewall", HostLane.Fast, 2)]
    private static void TestFirewall()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        var family = HostRhpAllocationChecks.KernelFamily(root, AllocationSources());
        foreach (var part in family)
            Need(OS.Tooling.EfiSourceAdmission.Inspect(part.Value.Text, part.Key).Succeeded, "reviewed split allocation family");
        var originalLeaf = family.Single(part => HostRhpAllocationChecks.Hash(part.Value.Text) == OS.Tooling.EfiContracts.AllocationSourceSha256);
        string name = originalLeaf.Key;
        string text = originalLeaf.Value.Text.Replace("\r\n", "\n", StringComparison.Ordinal);
        foreach (string good in new[]
        {
            text,
            text.Replace("\n", "\r\n", StringComparison.Ordinal)
        }

        )
            Need(OS.Tooling.EfiSourceAdmission.Inspect(good, name).Succeeded, "reviewed source admission");
        foreach (string bad in new[]
        {
            "using System; public class X {}",
            "public class X { object value = new object(); }"
        }

        )
            Need(!OS.Tooling.EfiSourceAdmission.Inspect(bad, name).Succeeded, "generic surface remains closed");
        foreach (string bad in new[]
        {
            text + " ",
            text + "\r",
            text + "\n// extra",
            text.Replace("4096", "4095", StringComparison.Ordinal)
        }

        )
            Need(!OS.Tooling.EfiSourceAdmission.Inspect(bad, name).Succeeded, "mutated reviewed source");
        foreach (string alias in new[]
        {
            name.ToLowerInvariant(),
            name + " ",
            name.Replace('\\', '/'),
            name.Replace("Runtime\\", "Runtime\\.\\", StringComparison.Ordinal),
            name.Insert(4, "\0"),
            @"..\OS.Kernel\Runtime\Other.cs"
        }

        )
            Need(!OS.Tooling.EfiSourceAdmission.Inspect(text, alias).Succeeded, "source alias refused");
        Need(!OS.Tooling.EfiSourceAdmission.Inspect("using System; public class X {}", @"..\OS.Kernel\Other.cs").Succeeded, "generic scanner unchanged");
    }

    [HostTest("m04.007.efi strongest full transcript and negatives", HostLane.Fast, 3)]
    private static void TestTranscript()
    {
        string root = HostToolProcess.FindRepositoryRoot(), directory = Scratch(root, "transcript");
        Result dry = Command(["qemu", "smoke", "-BootPolicy", "Diagnostic", "-DryRun", "-BuildEfi", .. Chain.Append(Flag).Select(x => "-" + x)]);
        Save(Path.Combine(directory, "dryrun"), dry);
        Need(dry.Code == 0, dry.Output);
        string[] chain = dry.Output.Replace("\r", "").Split('\n').Single(x => x.StartsWith("ORDERED_MARKERS ", StringComparison.Ordinal))[16..].Split(';');
        List<string> lines = ["TEST_START efi.main", .. chain.Take(6), "UEFI_MEMORY_MAP_DESCRIPTOR_COUNT 3", "UEFI_MEMORY_MAP_OWNED", "BOOT_HANDOFF_READY", "UEFI_MEMORY_MAP_NORMALIZED", "BOOT_MEMORY_MAP_ACCEPTED", "BOOT_MEMORY_REGIONS_READY", "BOOT_MEMORY_REGION_COUNT 3", "BOOT_PAGE_ALLOCATOR_READY status=Ready regions=3 conventionalPages=20 conventionalBytes=81920 freeNodes=2 freePages=20 freeBytes=81920 pageSize=4096 statusCode=0", "BOOT_ACPI_RSDP_HANDOFF_READY", "UEFI_ACPI_RSDP_READY", .. chain.Skip(6)];
        const string identity = "allocatorState=0x0000000000100000 nodes=0x0000000000100040 heapState=0x0000000000101000 ledger=0x0000000000102000 parentIndex=4 parentStart=0x0000000000900000 parentEnd=0x0000000000902000 backing=0x0000000000900000 retainedStart=0x0000000000900000 retainedEnd=0x0000000000901000";
        lines[lines.IndexOf("M04_HEAP_PAGE_BOUND")] += " " + identity;
        lines[lines.IndexOf("M04_HEAP_PAGE_RETAINED")] += " checkpoint=beforeBootPlan " + identity;
        lines[lines.IndexOf("M04_HEAP_PAGE_INITIALIZED")] += " pages=1 bytes=4096 freePagesBefore=2 freeBytesBefore=8192 allocatedPagesBefore=0 allocatedBytesBefore=0 freePagesAfter=1 freeBytesAfter=4096 allocatedPagesAfter=1 allocatedBytesAfter=4096 readBytes=4096 contentsZero=true";
        lines[lines.IndexOf("M04_HEAP_PAGE_CURRENT")] += " freePages=1 freeBytes=4096 allocatedPages=1 allocatedBytes=4096 readBytes=4096 contentsZero=true";
        lines[45] = "M04_RHP_PREP image=0x0000000001000000 imageBytes=65536 access=0x0000000000103000 accessBytes=104 bump=0x0000000000103100 bumpBytes=80 control=0x0000000000103200 controlBytes=96 records=0x0000000000104000 recordsBytes=6672";
        lines[46] = "M04_RHP_BIND image=0x0000000001000000 imageBytes=65536 allocator=0x0000000000100000 nodes=0x0000000000100040 ledger=0x0000000000102000 ledgerBytes=200 parentIndex=4 backing=0x0000000000900000 backingBytes=8192 heap=0x0000000000101000 heapBytes=216 access=0x0000000000103000 accessBytes=104 bump=0x0000000000103100 bumpBytes=80 status=1 used=0 count=0";
        uint[] sizes = [24, 32, 32, 32, 24, 24, 24, 32, 24, 24];
        uint[] flags = [0x50000000, 0x51000000, 0x53000000, 0x52000000, 0x20000080, 0x20000080, 0x40000000, 0x41000000, 0x43000000, 0x4A000200];
        ulong used = 0;
        static string H(ulong x) => "0x" + x.ToString("X16");
        for (int id = 1; id <= 10; id++)
        {
            uint size = sizes[id - 1];
            ulong mt = 0x1001100UL + (ulong)(id - 1) * 32, allocation = 0x900000 + used;
            lines[45 + id * 2] = $"M04_RHP_OBJECT id={id} route={(id <= 4 ? "new" : "direct")} mt={H(mt)} prefix0={H(((ulong)size << 32) | flags[id - 1])} prefix1={H(0x1002000)} prefix2={H(0x1234567800020001)} flags={H(flags[id - 1])} base={size} allocation={H(allocation)} reference={H(allocation + 8)} end={H(allocation + size)} usedBefore={used} usedAfter={used + size} count={id} sync={H(0)} observedMt={H(mt)} zeroBytes={size - 8} zeroOr={H(0)}";
            lines[46 + id * 2] = $"M04_RHP_READBACK id={id} scalarOffset={(id == 1 ? 8 : id <= 4 ? 16 : 0)} scalar={H(id <= 4 ? (ulong)id * 0x1111111111111111 : 0)} referenceSlotBytes={size - 16 - (id <= 4 ? 8 : 0)} referenceSlotOr={H(0)} priorCount={id} priorMismatchBytes=0 canaryMismatchBytes=0";
            used += size;
        }

        lines[67] = "M04_RHP_RETAIN allocator=0x0000000000100000 nodes=0x0000000000100040 ledger=0x0000000000102000 parentIndex=4 backing=0x0000000000900000 heap=0x0000000000101000 bump=0x0000000000103100 heapStart=0x0000000000900000 heapEnd=0x0000000000901000 initialPages=2 freePages=1 allocatedPages=1 readBytes=4096 contentsZero=false used=272 count=10 status=1";
        Need(lines.Count == 78 && lines[44] == "TEST_START m04.007.rhp_new_fast" && lines[70] == "TEST_START boot.sequence", "full78 positions");
        string good = string.Join("\r\n", lines) + "\r\n";
        List<(string Text, bool Accept)> cases = [(good, true), ("\u001b[2J\u001b[HUEFI Interactive Shell\r\n" + good, true), (good + "\r\n", true)];
        for (int i = 0; i < lines.Count; i++)
        {
            foreach (string replacement in new[]
            {
                "",
                lines[i] + " ",
                " " + lines[i],
                lines[i] + " diagnostic",
                lines[i] + "\0",
                lines[i].ToLowerInvariant(),
                lines[i] + "\r\n" + lines[i]
            }

            )
                cases.Add((good.Replace(lines[i] + "\r\n", replacement + "\r\n", StringComparison.Ordinal), false));
        }

        foreach (string bad in new[]
        {
            "TE\r\nST_FA\r\nIL firmware\r\n",
            "OS_SMO\r\nKE_FAIL\r\n",
            "M\0\uFEFF04_RHP_BIND\r\n"
        }

        )
            cases.Add((bad + good, false));
        foreach (string tail in new[]
        {
            "OVMF menu\r\n",
            "partial",
            "\r",
            "OS_SMOKE_PASS\r\n",
            "TEST_FAIL m04.007.rhp_new_fast exhausted\r\n"
        }

        )
            cases.Add((good + tail, false));
        cases.Add((good.Replace("\r\n", "\n", StringComparison.Ordinal), false));
        cases.Add((good[..^1], false));
        foreach ((string from, string to) in new[]
        {
            ("accessBytes=104", "accessBytes=103"),
            ("controlBytes=96", "controlBytes=95"),
            ("recordsBytes=6672", "recordsBytes=4096"),
            ("records=0x0000000000104000", "records=0x0000000000103100"),
            ("control=0x0000000000103200", "control=0x0000000000101000"),
            ("backingBytes=8192", "backingBytes=4096"),
            ("bump=0x0000000000103100", "bump=0x0000000000103000"),
            ("imageBytes=65536", "imageBytes=18446744073709551616"),
            ("zeroBytes=16", "zeroBytes=15"),
            ("usedAfter=24", "usedAfter=25"),
            ("scalarOffset=16", "scalarOffset=8"),
            ("priorCount=10", "priorCount=9"),
            ("contentsZero=false", "contentsZero=true"),
            ("count=10 status=1", "count=11 status=1"),
            ("flags=0x0000000050000000", "flags=0x0000000051000000")
        }

        )
            cases.Add((good.Replace(from, to, StringComparison.Ordinal), false));
        var manifest = cases.Select((c, i) => new { Path = Path.Combine(directory, i + ".log"), c.Accept }).ToArray();
        for (int i = 0; i < cases.Count; i++)
            File.WriteAllText(manifest[i].Path, cases[i].Text, new UTF8Encoding(false));
        string json = Path.Combine(directory, "cases.json");
        File.WriteAllText(json, System.Text.Json.JsonSerializer.Serialize(manifest));
        foreach (var entry in manifest)
        {
            bool accepted = true;
            try
            {
                OS.Tooling.QemuTranscripts.AssertM04RhpNewFastTranscript(entry.Path);
            }
            catch (OS.Tooling.ToolFailure)
            {
                accepted = false;
            }

            Need(accepted == entry.Accept, "whole transcript control " + entry.Path + " expected=" + entry.Accept);
        }

        EfiTranscriptControls.RunnerExitCodes();
        Console.WriteLine("TRANSCRIPT_CONTROLS m04.007 cases=" + manifest.Length);
    }

    [HostTest("m04.007.efi explicit profile prerequisites and conflicts", HostLane.Native, 4)]
    private static void TestProfiles()
    {
        string root = HostToolProcess.FindRepositoryRoot(), directory = Scratch(root, "profiles");
        string before = "EnableM04RhpNewFastBeforeBindFailureProbe", exhaust = "EnableM04RhpNewFastExhaustionFailureProbe";
        string[] all = Chain.Append(Flag).ToArray();
        string[] conflicts = ["EnableM01OwnedPagingProbe", .. XDocument.Load(Path.Combine(root, "src/OS/OS.csproj")).Descendants().Where(x => x.Name.LocalName.StartsWith("EnableM02", StringComparison.Ordinal)).Select(x => x.Name.LocalName).Distinct(StringComparer.Ordinal), "EnableUefiGoneSimulationProbe", "EnableExitBootServicesFreshMapProbe", "EnableAcpiPhysicalDiscoveryProbe", "EnableM03MemoryMapCaptureProbe", "EnableM03FirmwareEntropySeedProbe", "EnableM04TypeManagerModuleRegistrationProbe"];
        Need(OS.Tooling.ContractAudit.Inspect(root).Count == 0, "complete independent compiler/link/profile identity");
        Need(Chain.Length != 0 && Chain.Distinct(StringComparer.Ordinal).Count() == Chain.Length
            && conflicts.Length != 0 && conflicts.Distinct(StringComparer.Ordinal).Count() == conflicts.Length,
            "nonempty unique prerequisite and conflict vectors");
        List<string[]> invalid = [.. Chain.Select(missing => all.Where(x => x != missing).ToArray()), .. conflicts.Select(x => all.Append(x).ToArray()), [before], [exhaust], [.. all, before, exhaust]];
        int id = 0;
        foreach (string[] flags in invalid)
            foreach (bool runner in new[]
            {
                false,
                true
            }

            )
            {
                Result r = Command([.. (runner ? new[] { "qemu", "smoke" } : new[] { "efi", "build" }), .. (runner ? new[] { "-BootPolicy", "Diagnostic", "-DryRun", "-BuildEfi" } : new[] { "-PreflightOnly", "-LogPath", Path.Combine(directory, "preflight-" + id + ".log") }), .. flags.Select(x => "-" + x)]);
                Save(Path.Combine(directory, (id++).ToString()), r);
                Need(r.Code != 0 && (flags.Contains("EnableM04TypeManagerModuleRegistrationProbe", StringComparer.Ordinal) ? r.Output.Contains("m04_type_manager_profile_conflict property=EnableM01LiveAllocatorProbe", StringComparison.Ordinal) : flags.Contains("EnableM03FirmwareEntropySeedProbe", StringComparer.Ordinal) ? r.Output.Contains("m03_firmware_entropy_profile_conflict property=EnableM01LoadedImageStackProbe", StringComparison.Ordinal) : flags.Contains("EnableM03MemoryMapCaptureProbe", StringComparer.Ordinal) ? r.Output.Contains("m03_memory_map_capture_profile_conflict property=EnableM01LoadedImageStackProbe", StringComparison.Ordinal) : r.Output.Contains("m04_rhp_new_fast", StringComparison.Ordinal)), "explicit profile rejection " + string.Join(',', flags) + " " + r.Output);
            }

        foreach (string[] flags in new[]
        {
            all,
            all.Append(before).ToArray(),
            all.Append(exhaust).ToArray()
        }

        )
        {
            Result r = Command(["qemu", "smoke", "-BootPolicy", "Diagnostic", "-DryRun", "-BuildEfi", .. flags.Select(x => "-" + x)]);
            Save(Path.Combine(directory, (id++).ToString()), r);
            Need(r.Code == 0, r.Output);
            Need(r.Output.Contains("TEST_FAIL m04.007.rhp_new_fast", StringComparison.Ordinal), "negative profile does not weaken generic failure inventory");
        }
    }

    [HostTest("m04.007.efi actual default and three allocation artifacts", HostLane.Native, 5)]
    private static void TestArtifacts()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string[] all = Chain.Append(Flag).ToArray();
        foreach (var lane in new[]
        {
            ("default", Array.Empty<string>()),
            ("positive", all),
            ("before-bind", all.Append("EnableM04RhpNewFastBeforeBindFailureProbe").ToArray()),
            ("exhaustion", all.Append("EnableM04RhpNewFastExhaustionFailureProbe").ToArray())
        }

        )
        {
            string directory = Scratch(root, "artifact-" + lane.Item1), artifact = Path.Combine(root, "artifacts/efi/issue519-tests-" + Guid.NewGuid().ToString("N") + ".EFI");
            Result r = Command(["efi", "build", "-EfiOutputPath", artifact, "-LogPath", Path.Combine(directory, "publish.log"), .. lane.Item2.Select(x => "-" + x)]);
            Save(directory, r);
            Need(r.Code == 0 && r.Output.Contains("TEST_PASS efi.shell.publish", StringComparison.Ordinal) && r.Output.Contains("TEST_PASS efi.artifact.verify", StringComparison.Ordinal), r.Output);
            foreach (string suffix in new[]
            {
                "",
                ".link.json",
                ".link.map",
                ".link.rsp",
                ".serializer.obj"
            }

            )
                File.Copy(artifact + suffix, Path.Combine(directory, "image.EFI" + suffix));
            foreach (var file in new[]
            {
                (OS.Tooling.EfiContracts.ManagedAssemblyRelativePath("Release"), "OS.dll"),
                ("src/OS/obj/Release/net11.0/win-x64/native/OS.obj", "OS.obj"),
                ("src/OS/obj/Release/net11.0/win-x64/native/OS.map.xml", "OS.map.xml")
            }

            )
                File.Copy(Path.Combine(root, file.Item1), Path.Combine(directory, file.Item2));
            using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(artifact + ".link.json"));
            var e = document.RootElement;
            var inputs = e.GetProperty("sourceClosureInputs").EnumerateArray().ToArray();
            Need(inputs.Length == OS.Tooling.EfiEvidence.SourceClosurePaths(root).Count, "complete raw source inventory");
            foreach (var input in inputs)
                Need(Hash(File.ReadAllBytes(Path.Combine(root, input.GetProperty("path").GetString()!))) == input.GetProperty("sha256").GetString(), "actual raw source unchanged");
            string[] expectedCompileItems = OS.Tooling.EfiProjectAdmission.ExpectedCompileItems(OS.Tooling.EfiProfile.Create(null, lane.Item2.ToDictionary(flag => flag, _ => true, StringComparer.Ordinal)));
            var evaluation = e.GetProperty("postRestoreEvaluation");
            Need(evaluation.GetProperty("compileCount").GetInt32() == expectedCompileItems.Length, "complete evaluated compile inventory");
            Need(evaluation.GetProperty("compileSha256").GetString() == OS.Tooling.EfiEvidence.HashLines(expectedCompileItems), "ordered evaluated compile inventory");
            var flags = evaluation.GetProperty("properties").EnumerateObject().Where(x => x.Name.StartsWith("Enable", StringComparison.Ordinal)).ToArray();
            Need(flags.Length == OS.Tooling.EfiContracts.DiagnosticProbes.Count, "complete52 evaluated flags");
            foreach (var f in flags)
                Need(f.Value.GetString() == (lane.Item2.Contains(f.Name, StringComparer.Ordinal) ? "true" : "false"), "actual requested/evaluated profile " + f.Name);
            using (var pe = new PEReader(File.OpenRead(Path.Combine(directory, "OS.dll"))))
            {
                var md = pe.GetMetadataReader();
                Need(md.AssemblyReferences.Count == 0 && md.GetString(md.GetAssemblyDefinition().Name) == "OS", "actual no-stdlib assembly");
                var exports = md.TypeDefinitions.SelectMany(t => md.GetTypeDefinition(t).GetMethods()).Where(m => md.GetString(md.GetMethodDefinition(m).Name) == "RhpNewFast").ToArray();
                Need(exports.Length == 1, "sole RhpNewFast metadata method");
                var export = md.GetMethodDefinition(exports[0]);
                Need((export.ImplAttributes & System.Reflection.MethodImplAttributes.NoInlining) != 0, "actual export NoInlining");
                Need(export.RelativeVirtualAddress != 0, "actual export body");
            }

            if (lane.Item1 == "positive")
                TestLinkedRoutes(root, directory);
            File.WriteAllText(Path.Combine(directory, "identities.json"), System.Text.Json.JsonSerializer.Serialize(Directory.GetFiles(directory).Select(p => new { Path = Path.GetFileName(p), Sha256 = Hash(File.ReadAllBytes(p)) }), new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private static void TestLinkedRoutes(string root, string directory, int routeCount = 10)
    {
        byte[] image = File.ReadAllBytes(Path.Combine(directory, "image.EFI"));
        using var reader = new PEReader(new MemoryStream(image));
        var header = reader.PEHeaders.PEHeader!;
        Need(header.Magic == PEMagic.PE32Plus && reader.PEHeaders.SectionHeaders.Length is > 0 and <= 8, "bounded actual x64 PE sections");
        byte[] At(ulong rva, int bytes)
        {
            var sections = reader.PEHeaders.SectionHeaders.Where(s => rva >= (ulong)s.VirtualAddress && rva + (ulong)bytes <= (ulong)s.VirtualAddress + (ulong)s.VirtualSize && rva + (ulong)bytes <= (ulong)s.VirtualAddress + (ulong)s.SizeOfRawData).ToArray();
            Need(sections.Length == 1, "unique initialized extent");
            var s = sections[0];
            return image.AsSpan(checked(s.PointerToRawData + (int)(rva - (ulong)s.VirtualAddress)), bytes).ToArray();
        }

        var symbols = Regex.Matches(File.ReadAllText(Path.Combine(directory, "image.EFI.link.map")), @"^\s*[0-9A-Fa-f]{4}:[0-9A-Fa-f]{8}\s+(\S+)\s+([0-9A-Fa-f]{16})\s+", RegexOptions.Multiline).ToDictionary(m => m.Groups[1].Value, m => Convert.ToUInt64(m.Groups[2].Value, 16) - header.ImageBase, StringComparer.Ordinal);
        Need(symbols.ContainsKey("RhpNewFast"), "named actual native export");
        // LLVM provides decoded instructions/relocations. Never search arbitrary bytes for E8.
        string llvm = HostToolProcess.FindLlvmObjdump();
        Result dis = Process(root, llvm, ["--disassemble", "--reloc", "--x86-asm-syntax=intel", Path.Combine(directory, "OS.obj")]);
        Save(Path.Combine(directory, "disassembly"), dis);
        Need(dis.Code == 0, dis.Output);
        File.WriteAllText(Path.Combine(directory, "disassembler.json"), System.Text.Json.JsonSerializer.Serialize(new
        {
            Path = llvm,
            Sha256 = Hash(File.ReadAllBytes(llvm))
        }));
        const string prefix = "OS_OS_Runtime_RhpNewFastAllocation__";
        string[] types = ["Plain", "Reference", "GenericReference", "GenericValue", "Int32", "Enum", "Value", "ReferenceValue", "GenericStruct", "Nullable"];
        string[] calls = ["NewPlain", "NewReference", "NewGenericReference", "NewGenericValue", "AllocateInt32", "AllocateEnum", "AllocateValue", "AllocateReferenceValue", "AllocateGenericValue", "AllocateNullable"];
        uint[] sizes = [24, 32, 32, 32, 24, 24, 24, 32, 24, 24], flags = [0x50000000, 0x51000000, 0x53000000, 0x52000000, 0x20000080, 0x20000080, 0x40000000, 0x41000000, 0x43000000, 0x4A000200];
        List<object> rows = [];
        HashSet<ulong> tokens = [];
        Need(routeCount is 0 or 1 or 10, "bounded source-profile route inventory");
        for (int i = 0; i < routeCount; i++)
        {
            string getter = prefix + "Type" + types[i], caller = prefix + calls[i];
            ulong rva = symbols[getter];
            byte[] code = At(rva, 8);
            Need(code.AsSpan(0, 3).SequenceEqual(new byte[] { 0x48, 0x8D, 0x05 }) && code[7] == 0xC3, "actual intrinsic RIP-relative getter " + getter);
            ulong mt = checked((ulong)((long)rva + 7 + BinaryPrimitives.ReadInt32LittleEndian(code.AsSpan(3, 4))));
            Need(tokens.Add(mt), "ten distinct authentic identities");
            byte[] raw = At(mt, 24);
            Need(BinaryPrimitives.ReadUInt32LittleEndian(raw) == flags[i] && BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(4)) == sizes[i], "independent native prefix shape " + i);
            var section = reader.PEHeaders.SectionHeaders.Single(s => mt >= (ulong)s.VirtualAddress && mt + 24 <= (ulong)s.VirtualAddress + (ulong)s.VirtualSize);
            uint characteristics = (uint)section.SectionCharacteristics;
            Need((characteristics & 0x40000000) != 0 && (characteristics & 0xA2000000) == 0, "initialized readable nonwritable nondiscardable prefix");
            string mtName = symbols.Single(s => s.Value == mt && s.Key.StartsWith("??_7", StringComparison.Ordinal)).Key;
            var body = Regex.Match(dis.Output, @"^[0-9a-f]+ <" + Regex.Escape(caller) + @">:\r?\n(.*?)(?=^[0-9a-f]+ <|\z)", RegexOptions.Multiline | RegexOptions.Singleline);
            Need(body.Success, "decoded caller " + caller);
            var operand = Regex.Matches(body.Value, @"\blea\s+rcx, \[rip\][^\r\n]*\r?\n\s*[0-9a-f]+:\s+IMAGE_REL_AMD64_REL32\s+(\S+)");
            Need(operand.Count == 1 && string.Equals(operand[0].Groups[1].Value, mtName, StringComparison.Ordinal), "caller token relocation equals actual Of<T> identity " + caller);
            var exportCalls = Regex.Matches(body.Value, @"\bcall\s+0x[0-9a-f]+ <RhpNewFast>");
            Need(exportCalls.Count == 1 && exportCalls[0].Index > operand[0].Index, "actual token precedes exactly one export call " + caller);
            int offset = 0;
            string constructor = "";
            if (i < 4)
            {
                byte[] load = At(symbols[prefix + "Read" + types[i]], 5);
                offset = i == 0 ? 8 : 16;
                Need(load.AsSpan(0, 3).SequenceEqual(new byte[] { 0x48, 0x8B, 0x41 }) && load[3] == offset && load[4] == 0xC3, "independent scalar reader offset");
                var ctors = Regex.Matches(body.Value, @"\bcall\s+0x[0-9a-f]+ <(.+___ctor)>");
                Need(ctors.Count == 1, "one actual constructor after allocation");
                constructor = ctors[0].Groups[1].Value;
                Need(body.Value.IndexOf("<RhpNewFast>", StringComparison.Ordinal) < ctors[0].Index, "export before constructor");
                Need(symbols.ContainsKey(constructor), "linked constructor identity");
            }

            rows.Add(new
            {
                Id = i + 1,
                Getter = getter,
                GetterRva = rva,
                Caller = caller,
                CallerRva = symbols[caller],
                CallerTokenRelocation = operand[0].Groups[1].Value,
                MethodTable = mtName,
                MethodTableRva = mt,
                RawPrefix = Convert.ToHexString(raw),
                BaseSize = sizes[i],
                Flags = flags[i],
                ScalarOffset = offset,
                Constructor = constructor
            });
        }

        File.WriteAllText(Path.Combine(directory, "linked-ten-routes.json"), System.Text.Json.JsonSerializer.Serialize(new
        {
            PreferredBase = header.ImageBase,
            ExportRva = symbols["RhpNewFast"],
            Rows = rows
        }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }

    private static string Between(string text, string start, string end)
    {
        int i = text.IndexOf(start, StringComparison.Ordinal);
        Need(i >= 0, start);
        int j = text.IndexOf(end, i, StringComparison.Ordinal);
        Need(j >= i, end);
        return text[i..j];
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static void Save(string directory, Result r)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "process.log"), r.Output);
        File.WriteAllText(Path.Combine(directory, "exit.txt"), r.Code.ToString());
    }

    private static Result Process(string root, string executable, string[] arguments)
    {
        ProcessStartInfo start = new(executable)
        {
            WorkingDirectory = root,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (string a in arguments)
            start.ArgumentList.Add(a);
        using System.Diagnostics.Process p = System.Diagnostics.Process.Start(start)!;
        Task<string> stdout = p.StandardOutput.ReadToEndAsync(), stderr = p.StandardError.ReadToEndAsync();
        if (!p.WaitForExit(900000))
        {
            p.Kill(entireProcessTree: true);
            throw new TimeoutException(executable);
        }

        Task.WaitAll(stdout, stderr);
        return new(p.ExitCode, stdout.Result + stderr.Result);
    }

    private static string Scratch(string root, string name)
    {
        string p = Path.Combine(root, "artifacts/issue519-tests", name, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(p);
        return p;
    }

    private static void Need(bool x, string reason)
    {
        if (!x)
            throw new InvalidOperationException(reason);
    }

    private static Result Command(params string[] args)
    {
        var result = HostToolProcess.RunTool(args);
        return new(result.ExitCode, result.Output + result.Error);
    }
}
#endif
