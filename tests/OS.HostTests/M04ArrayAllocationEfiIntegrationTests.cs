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

#if ARRAY_CORE_TEST
using OS.Boot;
using OS.Kernel.Boot;
using OS.Kernel.Memory;
using OS.Kernel.Runtime;
using OS.Runtime;

#else
internal static partial class M04ArrayAllocationEfiIntegrationTests
{
    private const string Flag = "EnableM04SzArrayAllocationProbe";
    private static readonly string[] Chain = ["EnableM01LoadedImageStackProbe", "EnableM01LiveAllocatorProbe", "EnableM04IndependentEndianConformanceProbe", "EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe", "EnableM04ArithmeticConformanceProbe", "EnableM04LayoutContractProbe", "EnableM04ManagedHeapPageRangeProbe", "EnableM04RhpNewFastProbe"];
    [HostTest("m04.008.efi actual POD core and bounded PE", HostLane.Native, 1)]
    private static void TestCore()
    {
        string root = HostToolProcess.FindRepositoryRoot(), directory = Scratch(root, "core");
        string includes = string.Join('\n', AllocationSdkSourcePaths().Select(path => "<Compile Include=\"" + SecurityElement.Escape(path) + "\"/>"));
        File.WriteAllText(Path.Combine(directory, "Core.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net11.0</TargetFramework><LangVersion>14.0</LangVersion>
            <OutputType>Exe</OutputType><AllowUnsafeBlocks>true</AllowUnsafeBlocks><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable>
            <EnableDefaultCompileItems>false</EnableDefaultCompileItems><DefineConstants>ARRAY_CORE_TEST</DefineConstants><NoWarn>0649</NoWarn></PropertyGroup><ItemGroup>
            """ + includes + "<ProjectReference Include=\"" + SecurityElement.Escape(Path.Combine(root, "src/OS.Kernel/OS.Kernel.csproj")) + "\"/></ItemGroup></Project>");
        Result result = Process(root, "dotnet", ["run", "--project", Path.Combine(directory, "Core.csproj"), "-c", "Release", "--nologo"]);
        File.WriteAllText(Path.Combine(directory, "process.log"), result.Output);
        File.WriteAllText(Path.Combine(directory, "exit.txt"), result.Code.ToString());
        Need(result.Code == 0 && result.Output.Contains("TEST_PASS array.core.tests", StringComparison.Ordinal), result.Output);
    }

    [HostTest("m04.008.efi source profile and original handoff", HostLane.Fast, 0)]
    private static void TestSource()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        XDocument project = XDocument.Load(Path.Combine(root, "src/OS/OS.csproj"));
        var catalog = AllocationSources();
        Need(OS.Tooling.ContractAudit.Inspect(root).Count == 0, "complete independent compiler/link/profile identity");
        _ = HostRhpAllocationChecks.KernelFamily(root, catalog);
        foreach (var pair in new[]
        {
            (Flag, "M04_SZ_ARRAY_ALLOCATION_PROBE"),
            ("EnableM04SzArrayAllocationBeforeBindProbe", "M04_SZ_ARRAY_ALLOCATION_BEFORE_BIND_PROBE"),
            ("EnableM04SzArrayAllocationExhaustionProbe", "M04_SZ_ARRAY_ALLOCATION_EXHAUSTION_PROBE")
        }

        )
        {
            var value = project.Descendants(pair.Item1).Single();
            Need(value.Value == "false" && value.Attribute("Condition")?.Value == "'$(" + pair.Item1 + ")' == ''", "default false");
            Need(project.Descendants("PropertyGroup").Single(x => x.Attribute("Condition")?.Value == "'$(" + pair.Item1 + ")' == 'true'").Element("DefineConstants")?.Value == "$(DefineConstants);" + pair.Item2, "exact selected compiler symbol");
        }

        foreach (string required in Chain)
            Need(project.Descendants("Error").Any(x => x.Attribute("Condition")?.Value == "'$(" + Flag + ")' == 'true' and '$(" + required + ")' != 'true'"), "explicit prerequisite " + required);
        _ = HostRhpAllocationChecks.FrozenLeaf(catalog, "OS.Kernel.Runtime.GuestTestArrayAllocationLengthOverflowChecks", "3848704952c9b04052713ac11c4650a5b2238db80e48878ae4b80fb6ffd4549a");
        string shim = HostRhpAllocationChecks.ArrayShape(catalog);
        string adapter = string.Join('\n', HostRhpAllocationSources.RuntimeFamily(catalog).Select(document => document.Text));
        string allocationBody = HostSourceText.Active(HostRhpAllocationChecks.CoreAllocation(catalog));
        HostRhpAllocationChecks.BumpAndBinder(catalog);
        Need(HostSourceText.Contains(shim, "private int _numComponents;") && HostSourceText.Contains(shim, "public int Length => _numComponents;"), "minimal actual compiler Array contract");
        Need(HostSourceText.Contains(allocationBody, "GuestTestArrayAllocationLengthOverflowChecks.TryAllocate(*access, bump, 80,") && HostSourceText.Contains(allocationBody, "prefix.RelatedTypeToken != expectedElement") && HostSourceText.Contains(allocationBody, "token != expectedToken") && HostSourceText.Contains(allocationBody, "RejectedAllocationUnchanged(bump, bumpBefore, access->Backing, records->BeforeAttempt)") && !adapter.Contains("Serialize(", StringComparison.Ordinal), "actual shared allocator and metadata/rejection/data publication boundary");
        Need(Regex.Matches(adapter, @"private static RhpNewFastBumpState s_bump;").Count == 1 && Regex.Matches(adapter, @"GuestTestRhpNewFast\.InitializeOnce\(").Count == 1, "one state and binder");
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
            Need(!adapter.Contains(forbidden, StringComparison.Ordinal), "no runtime scope expansion");
    }

    [HostTest("m04.008.efi source admission and strict firewall", HostLane.Fast, 2)]
    private static void TestFirewall()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        var catalog = AllocationSources();
        var leaf = HostRhpAllocationChecks.FrozenLeaf(catalog, "OS.Kernel.Runtime.GuestTestArrayAllocationLengthOverflowChecks", "3848704952c9b04052713ac11c4650a5b2238db80e48878ae4b80fb6ffd4549a");
        string name = Path.GetRelativePath(Path.Combine(root, "src/OS"), leaf.Path).Replace('/', '\\');
        string text = leaf.Text.Replace("\r\n", "\n", StringComparison.Ordinal);
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
    }

    [HostTest("m04.008.efi strongest full transcript and negatives", HostLane.Fast, 3)]
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
        lines[45] = "M04_ARRAY_PREP image=0x0000000001000000 imageBytes=65536 access=0x0000000000103000 accessBytes=104 bump=0x0000000000103100 bumpBytes=80 control=0x0000000000103200 controlBytes=96 records=0x0000000000104000 recordsBytes=6672";
        lines[46] = "M04_ARRAY_BIND image=0x0000000001000000 imageBytes=65536 allocator=0x0000000000100000 nodes=0x0000000000100040 ledger=0x0000000000102000 ledgerBytes=200 parentIndex=4 backing=0x0000000000900000 backingBytes=8192 heap=0x0000000000101000 heapBytes=216 access=0x0000000000103000 accessBytes=104 bump=0x0000000000103100 bumpBytes=80 status=1 used=0 count=0";
        uint[] sizes = [24, 24, 48, 32, 32], lengths = [0, 0, 3, 0, 1], offsets = [8, 0, 24, 16, 16], widths = [8, 0, 8, 8, 1];
        uint[] flags = [0x50000000, 0xE0020001, 0xE0020008, 0x51000000, 0xE0020001];
        ulong[] scalars = [0x1111111111111111, 0, 0x3333333333333333, 0x2222222222222222, 0x5A];
        ulong used = 0;
        static string H(ulong x) => "0x" + x.ToString("X16");
        for (int id = 1; id <= 5; id++)
        {
            bool array = id is 2 or 3 or 5;
            uint size = sizes[id - 1];
            ulong mt = 0x1001100UL + (ulong)(id == 5 ? 1 : id - 1) * 32, allocation = 0x900000 + used;
            ulong element = array ? (id == 3 ? 0x1002100UL : 0x1002000UL) : 0;
            ulong element0 = array ? (24UL << 32) | (id == 3 ? 0x28000000UL : 0x14000000UL) : 0;
            lines[45 + id * 2] = $"M04_ARRAY_OBJECT id={id} route={(array ? id == 3 ? "compiled_long_array" : "compiled_byte_array" : "compiled_fixed")} mt={H(mt)} prefix0={H(((ulong)(id == 4 ? 32 : 24) << 32) | flags[id - 1])} prefix1={H(element)} prefix2={H(0x1234567800020001)} flags={H(flags[id - 1])} base={size} allocation={H(allocation)} reference={H(allocation + 8)} end={H(allocation + size)} usedBefore={used} usedAfter={used + size} count={id} sync={H(0)} observedMt={H(mt)} zeroBytes={size - (array ? 12 : 8)} zeroOr={H(0)} length={lengths[id - 1]} element={H(element)} element0={H(element0)} element1={H(0)} element2={H(0)}";
            lines[46 + id * 2] = $"M04_ARRAY_READBACK id={id} length={lengths[id - 1]} scalarOffset={offsets[id - 1]} scalarWidth={widths[id - 1]} scalar={H(scalars[id - 1])} referenceSlotBytes={size - 16 - (array ? 4 : 0) - widths[id - 1]} referenceSlotOr={H(0)} priorCount={id} priorMismatchBytes=0 canaryMismatchBytes=0";
            used += size;
        }

        lines[57] = "M04_ARRAY_RETAIN allocator=0x0000000000100000 nodes=0x0000000000100040 ledger=0x0000000000102000 parentIndex=4 backing=0x0000000000900000 heap=0x0000000000101000 bump=0x0000000000103100 heapStart=0x0000000000900000 heapEnd=0x0000000000901000 initialPages=2 freePages=1 allocatedPages=1 readBytes=4096 contentsZero=false used=160 count=5 status=1";
        Need(lines.Count == 68 && lines[44] == "TEST_START m04.008.sz_array" && lines[60] == "TEST_START boot.sequence", "full68 positions");
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
            "TEST_FAIL m04.008.sz_array exhausted\r\n"
        }

        )
            cases.Add((good + tail, false));
        cases.Add((good.Replace("\r\n", "\n", StringComparison.Ordinal), false));
        cases.Add((good[..^1], false));
        foreach ((string from, string to) in new[]
        {
            ("scalarWidth=1", "scalarWidth=8"),
            ("length=3", "length=2"),
            ("element=0x0000000001002000", "element=0x0000000001002100"),
            ("element0=0x0000001814000000", "element0=0x0000001828000000"),
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
            ("priorCount=5", "priorCount=4"),
            ("contentsZero=false", "contentsZero=true"),
            ("count=5 status=1", "count=6 status=1"),
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
                OS.Tooling.QemuTranscripts.AssertM04SzArrayAllocationTranscript(entry.Path);
            }
            catch (OS.Tooling.ToolFailure)
            {
                accepted = false;
            }

            Need(accepted == entry.Accept, "whole transcript control " + entry.Path + " expected=" + entry.Accept);
        }

        EfiTranscriptControls.RunnerExitCodes();
        Console.WriteLine("TRANSCRIPT_CONTROLS m04.008 cases=" + manifest.Length);
    }

    [HostTest("m04.008.efi explicit profile prerequisites and conflicts", HostLane.Native, 4)]
    private static void TestProfiles()
    {
        string root = HostToolProcess.FindRepositoryRoot(), directory = Scratch(root, "profiles");
        string before = "EnableM04SzArrayAllocationBeforeBindProbe", exhaust = "EnableM04SzArrayAllocationExhaustionProbe";
        string[] all = Chain.Append(Flag).ToArray();
        string[] conflicts = ["EnableM01OwnedPagingProbe", .. XDocument.Load(Path.Combine(root, "src/OS/OS.csproj")).Descendants().Where(x => x.Name.LocalName.StartsWith("EnableM02", StringComparison.Ordinal)).Select(x => x.Name.LocalName).Distinct(StringComparer.Ordinal), "EnableUefiGoneSimulationProbe", "EnableExitBootServicesFreshMapProbe", "EnableAcpiPhysicalDiscoveryProbe", "EnableM04RhpNewFastBeforeBindFailureProbe", "EnableM04RhpNewFastExhaustionFailureProbe", "EnableM03MemoryMapCaptureProbe", "EnableM04CheckedReferenceBarrierProbe", "EnableM03FirmwareEntropySeedProbe", "EnableM04TypeManagerModuleRegistrationProbe"];
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
                Need(r.Code != 0 && (r.Output.Contains("m04_", StringComparison.Ordinal) || (flags.Contains("EnableM03FirmwareEntropySeedProbe", StringComparer.Ordinal) && r.Output.Contains("m03_firmware_entropy_profile_conflict property=EnableM01LoadedImageStackProbe", StringComparison.Ordinal)) || (flags.Contains("EnableM03MemoryMapCaptureProbe", StringComparer.Ordinal) && r.Output.Contains("m03_memory_map_capture_profile_conflict property=EnableM01LoadedImageStackProbe", StringComparison.Ordinal))), "explicit profile rejection " + string.Join(',', flags) + " " + r.Output);
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
            Need(r.Output.Contains("TEST_FAIL m04.008.sz_array", StringComparison.Ordinal), "negative profile does not weaken generic failure inventory");
        }
    }

    [HostTest("m04.008.efi actual default and three array artifacts", HostLane.Native, 5)]
    private static void TestArtifacts()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string[] all = Chain.Append(Flag).ToArray();
        foreach (var lane in new[]
        {
            ("default", Array.Empty<string>()),
            ("positive", all),
            ("before-bind", all.Append("EnableM04SzArrayAllocationBeforeBindProbe").ToArray()),
            ("exhaustion", all.Append("EnableM04SzArrayAllocationExhaustionProbe").ToArray())
        }

        )
        {
            string directory = Scratch(root, "artifact-" + lane.Item1), artifact = Path.Combine(root, "artifacts/efi/issue523-tests-" + Guid.NewGuid().ToString("N") + ".EFI");
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
            TestMetadata(directory, lane.Item1 != "default");
            if (lane.Item1 != "default")
                TestLinkedRoutes(root, directory, lane.Item1 == "before-bind");
            File.WriteAllText(Path.Combine(directory, "identities.json"), System.Text.Json.JsonSerializer.Serialize(Directory.GetFiles(directory).Select(p => new { Path = Path.GetFileName(p), Sha256 = Hash(File.ReadAllBytes(p)) }), new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private static void TestMetadata(string directory, bool enabled)
    {
        using var pe = new PEReader(File.OpenRead(Path.Combine(directory, "OS.dll")));
        var md = pe.GetMetadataReader();
        Need(md.AssemblyReferences.Count == 0 && md.GetString(md.GetAssemblyDefinition().Name) == "OS", "actual zero-reference OS");
        string FullType(TypeDefinitionHandle h)
        {
            var t = md.GetTypeDefinition(h);
            return md.GetString(t.Namespace) + "." + md.GetString(t.Name);
        }

        string AttributeType(CustomAttribute attribute)
        {
            var parent = attribute.Constructor.Kind == HandleKind.MethodDefinition ? (EntityHandle)md.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor).GetDeclaringType() : md.GetMemberReference((MemberReferenceHandle)attribute.Constructor).Parent;
            if (parent.Kind == HandleKind.TypeDefinition)
                return FullType((TypeDefinitionHandle)parent);
            if (parent.Kind == HandleKind.TypeReference)
            {
                var t = md.GetTypeReference((TypeReferenceHandle)parent);
                return md.GetString(t.Namespace) + "." + md.GetString(t.Name);
            }

            return "";
        }

        var methods = md.TypeDefinitions.SelectMany(t => md.GetTypeDefinition(t).GetMethods()).ToArray();
        List<object> evidence = [];
        foreach (string name in new[]
        {
            "RhpNewArrayFast",
            "RhpNewPtrArrayFast"
        }

        )
        {
            var named = methods.Where(h => md.GetString(md.GetMethodDefinition(h).Name) == name).ToArray();
            Need(named.Length == 1, "sole named helper " + name);
            var method = md.GetMethodDefinition(named[0]);
            Need(FullType(method.GetDeclaringType()) == "System.Runtime.RhpNewFastRuntime", "exact export owner");
            Need((method.Attributes & System.Reflection.MethodAttributes.Static) != 0 && (method.Attributes & System.Reflection.MethodAttributes.MemberAccessMask) == System.Reflection.MethodAttributes.Assembly && method.GetGenericParameters().Count == 0, "internal static nongeneric helper");
            Need((method.ImplAttributes & System.Reflection.MethodImplAttributes.NoInlining) != 0 && method.RelativeVirtualAddress != 0, "real NoInlining body");
            var sig = md.GetBlobReader(method.Signature);
            Need(sig.ReadByte() == 0 && sig.ReadCompressedInteger() == 2 && sig.ReadByte() == 0x1C && sig.ReadByte() == 0x0F && sig.ReadByte() == 0x11, "object(pointer-to-valuetype,nint) signature");
            int coded = sig.ReadCompressedInteger();
            Need((coded & 3) == 0 && FullType(System.Reflection.Metadata.Ecma335.MetadataTokens.TypeDefinitionHandle(coded >> 2)) == "Internal.Runtime.MethodTable" && sig.ReadByte() == 0x18 && sig.RemainingBytes == 0, "actual MethodTable* and signed native-sized count");
            int exports = 0;
            foreach (var h in methods)
                foreach (var ah in md.GetMethodDefinition(h).GetCustomAttributes())
                {
                    var attr = md.GetCustomAttribute(ah);
                    if (AttributeType(attr) != "System.Runtime.RuntimeExportAttribute")
                        continue;
                    var blob = md.GetBlobReader(attr.Value);
                    Need(blob.ReadUInt16() == 1, "attribute prolog");
                    string? value = blob.ReadSerializedString();
                    Need(blob.ReadUInt16() == 0 && blob.RemainingBytes == 0, "finite export attribute payload");
                    if (value == name)
                    {
                        exports++;
                        Need(h == named[0], "sole payload on named export");
                    }
                }

            Need(exports == 1, "sole RuntimeExport payload " + name);
            byte[] il = pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes()!;
            Need(il.Length > 0, "initialized IL body");
            evidence.Add(new
            {
                Name = name,
                Rva = method.RelativeVirtualAddress,
                Signature = Convert.ToHexString(md.GetBlobBytes(method.Signature)),
                IL = Convert.ToHexString(il),
                ExportPayloads = exports
            });
        }

        var array = md.GetTypeDefinition(md.TypeDefinitions.Single(h => FullType(h) == "System.Array"));
        var fields = array.GetFields().ToArray();
        Need(fields.Length == 1 && md.GetString(md.GetFieldDefinition(fields[0]).Name) == "_numComponents" && md.GetBlobBytes(md.GetFieldDefinition(fields[0]).Signature).AsSpan().SequenceEqual(new byte[] { 6, 8 }), "exact first signed length field");
        Need(array.GetMethods().Any(h => md.GetString(md.GetMethodDefinition(h).Name) == "get_Length"), "length getter exists");
        Need(md.TypeDefinitions.Any(h => FullType(h) == "OS.Runtime.RhpNewFastAllocation") == enabled, "actual enabled adapter presence/default absence");
        if (enabled)
        {
            var adapter = md.TypeDefinitions.Single(h => FullType(h) == "OS.Runtime.RhpNewFastAllocation");
            foreach (string name in new[]
            {
                "NewBytes",
                "NewLongs",
                "ByteLength",
                "LongLength",
                "StoreByte",
                "ReadByte",
                "StoreLong",
                "ReadLong",
                "AllocateArray",
                "RunMixed"
            }

            )
            {
                var h = md.GetTypeDefinition(adapter).GetMethods().Single(h => md.GetString(md.GetMethodDefinition(h).Name) == name);
                var m = md.GetMethodDefinition(h);
                byte[] il = pe.GetMethodBody(m.RelativeVirtualAddress).GetILBytes()!;
                var instructions = DecodeIl(il);
                string expected = name.StartsWith("New", StringComparison.Ordinal) ? "newarr" : name.EndsWith("Length", StringComparison.Ordinal) ? "ldlen" : name == "StoreByte" ? "stelem.i1" : name == "StoreLong" ? "stelem.i8" : name == "ReadByte" ? "ldelem.u1" : name == "ReadLong" ? "ldelem.i8" : "";
                if (expected.Length != 0)
                    Need(instructions.Any(x => x.Name == expected), "genuine compiler IL " + name);
                evidence.Add(new
                {
                    Name = name,
                    Rva = m.RelativeVirtualAddress,
                    IL = Convert.ToHexString(il),
                    Instructions = instructions
                });
            }
        }

        File.WriteAllText(Path.Combine(directory, "managed-array-metadata.json"), System.Text.Json.JsonSerializer.Serialize(new
        {
            Assembly = "OS",
            AssemblyReferences = 0,
            Enabled = enabled,
            Mvid = md.GetGuid(md.GetModuleDefinition().Mvid),
            Methods = evidence,
            GuestAccepted = false
        }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }

    private static IlInstruction[] DecodeIl(byte[] bytes)
    {
        var codes = typeof(System.Reflection.Emit.OpCodes).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static).Where(f => f.FieldType == typeof(System.Reflection.Emit.OpCode)).Select(f => (System.Reflection.Emit.OpCode)f.GetValue(null)!).ToDictionary(o => (ushort)o.Value);
        List<IlInstruction> result = [];
        int p = 0;
        while (p < bytes.Length)
        {
            int start = p;
            ushort code = bytes[p++];
            if (code == 0xFE)
                code = (ushort)(0xFE00 | bytes[p++]);
            var op = codes[code];
            int operand = 0;
            int n = op.OperandType switch
            {
                System.Reflection.Emit.OperandType.InlineNone => 0,
                System.Reflection.Emit.OperandType.ShortInlineBrTarget or System.Reflection.Emit.OperandType.ShortInlineI or System.Reflection.Emit.OperandType.ShortInlineVar => 1,
                System.Reflection.Emit.OperandType.InlineVar => 2,
                System.Reflection.Emit.OperandType.InlineI8 or System.Reflection.Emit.OperandType.InlineR => 8,
                System.Reflection.Emit.OperandType.InlineSwitch => 4 + 4 * BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(p)),
                _ => 4
            };
            Need(n >= 0 && p <= bytes.Length - n, "bounded IL decode");
            if (n == 4)
                operand = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(p));
            result.Add(new(start, op.Name!, operand));
            p += n;
        }

        return result.ToArray();
    }

    private static void TestLinkedRoutes(string root, string directory, bool beforeBind)
    {
        byte[] image = File.ReadAllBytes(Path.Combine(directory, "image.EFI"));
        using var reader = new PEReader(new MemoryStream(image));
        var header = reader.PEHeaders.PEHeader!;
        Need(header.Magic == PEMagic.PE32Plus && reader.PEHeaders.SectionHeaders.Length is > 0 and <= 8, "bounded x64 image sections");
        byte[] At(ulong rva, int bytes)
        {
            var sections = reader.PEHeaders.SectionHeaders.Where(s => s.VirtualAddress >= 0 && s.VirtualSize >= 0 && s.SizeOfRawData >= 0 && rva >= (ulong)s.VirtualAddress && rva + (ulong)bytes <= (ulong)s.VirtualAddress + (ulong)s.VirtualSize && rva + (ulong)bytes <= (ulong)s.VirtualAddress + (ulong)s.SizeOfRawData).ToArray();
            Need(sections.Length == 1, "one initialized native extent");
            var s = sections[0];
            return image.AsSpan(checked(s.PointerToRawData + (int)(rva - (ulong)s.VirtualAddress)), bytes).ToArray();
        }

        var symbols = Regex.Matches(File.ReadAllText(Path.Combine(directory, "image.EFI.link.map")), @"^\s*[0-9A-Fa-f]{4}:[0-9A-Fa-f]{8}\s+(\S+)\s+([0-9A-Fa-f]{16})\s+", RegexOptions.Multiline).ToDictionary(m => m.Groups[1].Value, m => Convert.ToUInt64(m.Groups[2].Value, 16) - header.ImageBase, StringComparer.Ordinal);
        string llvm = HostToolProcess.FindLlvmObjdump();
        Result dis = Process(root, llvm, ["--disassemble", "--reloc", "--x86-asm-syntax=intel", Path.Combine(directory, "OS.obj")]);
        Save(Path.Combine(directory, "disassembly"), dis);
        Need(dis.Code == 0, dis.Output);
        File.WriteAllText(Path.Combine(directory, "disassembler.json"), System.Text.Json.JsonSerializer.Serialize(new
        {
            Path = llvm,
            Sha256 = Hash(File.ReadAllBytes(llvm))
        }));
        string Body(string name)
        {
            var m = Regex.Match(dis.Output, @"^[0-9a-f]+ <" + Regex.Escape(name) + @">:\r?\n(.*?)(?=^[0-9a-f]+ <|\z)", RegexOptions.Multiline | RegexOptions.Singleline);
            Need(m.Success, "decoded native method " + name);
            return m.Value;
        }

        const string prefix = "OS_OS_Runtime_RhpNewFastAllocation__";
        ulong Token(string name)
        {
            ulong rva = symbols[prefix + "Type" + name];
            byte[] code = At(rva, 8);
            Need(code.AsSpan(0, 3).SequenceEqual(new byte[] { 0x48, 0x8D, 0x05 }) && code[7] == 0xC3, "actual Of getter");
            return checked((ulong)((long)rva + 7 + BinaryPrimitives.ReadInt32LittleEndian(code.AsSpan(3))));
        }

        var dir64 = new List<ulong>();
        var relocation = header.BaseRelocationTableDirectory;
        byte[] blocks = At((ulong)relocation.RelativeVirtualAddress, relocation.Size);
        for (int p = 0; p < blocks.Length;)
        {
            uint page = BinaryPrimitives.ReadUInt32LittleEndian(blocks.AsSpan(p)), size = BinaryPrimitives.ReadUInt32LittleEndian(blocks.AsSpan(p + 4));
            Need(size >= 8 && size % 2 == 0 && size <= blocks.Length - p, "bounded reloc block");
            for (int j = 8; j < size; j += 2)
            {
                ushort e = BinaryPrimitives.ReadUInt16LittleEndian(blocks.AsSpan(p + j));
                if ((e >> 12) == 10)
                    dir64.Add(page + (ulong)(e & 4095));
                else
                    Need((e >> 12) == 0, "supported actual PE relocation");
            }

            p += (int)size;
        }

        byte[] coff = File.ReadAllBytes(Path.Combine(directory, "OS.obj"));
        uint table = BinaryPrimitives.ReadUInt32LittleEndian(coff.AsSpan(8)), count = BinaryPrimitives.ReadUInt32LittleEndian(coff.AsSpan(12));
        int strings = checked((int)(table + count * 18));
        string SymbolName(int entry)
        {
            if (BinaryPrimitives.ReadUInt32LittleEndian(coff.AsSpan(entry)) != 0)
                return Encoding.ASCII.GetString(coff, entry, 8).TrimEnd('\0');
            int p = checked(strings + (int)BinaryPrimitives.ReadUInt32LittleEndian(coff.AsSpan(entry + 4)));
            int end = Array.IndexOf(coff, (byte)0, p);
            Need(end >= p, "COFF string");
            return Encoding.ASCII.GetString(coff, p, end - p);
        }

        (uint Value, int Section) Symbol(string name)
        {
            for (uint i = 0; i < count;)
            {
                int p = checked((int)(table + i * 18));
                string n = SymbolName(p);
                if (n == name)
                    return (BinaryPrimitives.ReadUInt32LittleEndian(coff.AsSpan(p + 8)), BinaryPrimitives.ReadInt16LittleEndian(coff.AsSpan(p + 12)));
                i += (uint)(1 + coff[p + 17]);
            }

            throw new InvalidOperationException("COFF symbol missing " + name);
        }

        string RelocatedElement(string name, out byte[] raw)
        {
            var symbol = Symbol(name);
            Need(symbol.Section > 0, "defined array MT");
            int section = 20 + BinaryPrimitives.ReadUInt16LittleEndian(coff.AsSpan(16)) + (symbol.Section - 1) * 40;
            uint rawStart = BinaryPrimitives.ReadUInt32LittleEndian(coff.AsSpan(section + 20));
            raw = coff.AsSpan(checked((int)(rawStart + symbol.Value)), 24).ToArray();
            uint relocStart = BinaryPrimitives.ReadUInt32LittleEndian(coff.AsSpan(section + 24));
            int n = BinaryPrimitives.ReadUInt16LittleEndian(coff.AsSpan(section + 32));
            List<string> found = [];
            for (int i = 0; i < n; i++)
            {
                int p = checked((int)relocStart + i * 10);
                uint offset = BinaryPrimitives.ReadUInt32LittleEndian(coff.AsSpan(p));
                if (offset < symbol.Value || offset >= symbol.Value + 24)
                    continue;
                Need(offset == symbol.Value + 8 && BinaryPrimitives.ReadUInt16LittleEndian(coff.AsSpan(p + 8)) == 1, "exact array prefix ADDR64 field");
                uint target = BinaryPrimitives.ReadUInt32LittleEndian(coff.AsSpan(p + 4));
                Need(target < count, "relocation target");
                found.Add(SymbolName(checked((int)(table + target * 18))));
            }

            Need(found.Count == 1, "sole related-element prefix relocation");
            return found[0];
        }

        List<object> rows = [];
        foreach (var route in new[]
        {
            ("Bytes", "Byte", "NewBytes", "RhpNewArrayFast", 0xE0020001U),
            ("Longs", "Long", "NewLongs", "RhpNewPtrArrayFast", 0xE0020008U)
        }

        )
        {
            ulong mt = Token(route.Item1), element = Token(route.Item2);
            string mtName = symbols.Single(s => s.Value == mt && s.Key.StartsWith("??_7", StringComparison.Ordinal)).Key;
            string elementName = symbols.Single(s => s.Value == element && s.Key.StartsWith("??_7", StringComparison.Ordinal)).Key;
            byte[] raw = At(mt, 24), ep = At(element, 24);
            Need(BinaryPrimitives.ReadUInt32LittleEndian(raw) == route.Item5 && BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(4)) == 24 && BinaryPrimitives.ReadUInt64LittleEndian(raw.AsSpan(8)) == header.ImageBase + element, "actual array shape/element pointer");
            Need(RelocatedElement(mtName, out byte[] coffPrefix) == elementName && dir64.Count(x => x == mt + 8) == 1, "same array COFF ADDR64 to exact PE DIR64/element");
            BinaryPrimitives.WriteUInt64LittleEndian(coffPrefix.AsSpan(8), checked(BinaryPrimitives.ReadUInt64LittleEndian(coffPrefix.AsSpan(8)) + header.ImageBase + element));
            Need(raw.AsSpan().SequenceEqual(coffPrefix), "all24 relocated array prefix bytes");
            foreach (ulong token in new[]
            {
                mt,
                element
            }

            )
            {
                var section = reader.PEHeaders.SectionHeaders.Single(s => token >= (ulong)s.VirtualAddress && token + 24 <= (ulong)s.VirtualAddress + (ulong)s.VirtualSize);
                uint c = (uint)section.SectionCharacteristics;
                Need((c & 0x40000000) != 0 && (c & 0xA2000000) == 0, "bounded initialized readonly metadata section");
            }

            string caller = prefix + route.Item3;
            if (symbols.ContainsKey(caller))
            {
                string body = Body(caller);
                var operand = Regex.Matches(body, @"\blea\s+rcx, \[rip\][^\r\n]*\r?\n\s*[0-9a-f]+:\s+IMAGE_REL_AMD64_REL32\s+(\S+)");
                Need(operand.Count == 1 && operand[0].Groups[1].Value == mtName, "actual compiler token equals Of array token");
                var call = Regex.Matches(body, @"\bcall\s+0x[0-9a-f]+ <" + route.Item4 + ">");
                Need(call.Count == 1 && call[0].Index > operand[0].Index && Regex.IsMatch(body, @"\bmovsxd\s+rdx, ecx"), "genuine newarr ABI signed length/token/helper order");
            }
            else
                Need(beforeBind && route.Item1 == "Longs", "only unreachable beforebind long caller may be removed");
            string exportBody = Body(route.Item4);
            Need(exportBody.Contains(prefix + "AllocateArray", StringComparison.Ordinal), "real leaf-bound export native route");
            rows.Add(new
            {
                Kind = route.Item1,
                Caller = caller,
                CallerPresent = symbols.ContainsKey(caller),
                Export = route.Item4,
                ExportRva = symbols[route.Item4],
                MethodTable = mtName,
                MethodTableRva = mt,
                Element = elementName,
                ElementRva = element,
                Prefix = Convert.ToHexString(raw),
                ElementPrefix = Convert.ToHexString(ep),
                Dir64Rva = mt + 8
            });
        }

        File.WriteAllText(Path.Combine(directory, "linked-array-routes.json"), System.Text.Json.JsonSerializer.Serialize(new
        {
            PreferredBase = header.ImageBase,
            Rows = rows,
            BeforeBind = beforeBind,
            GuestAccepted = false
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
        string p = Path.Combine(root, "artifacts/issue523-tests", name, Guid.NewGuid().ToString("N"));
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
