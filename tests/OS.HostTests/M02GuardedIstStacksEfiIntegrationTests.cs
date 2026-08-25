using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

internal static class M02GuardedIstStacksEfiIntegrationTests
{
    private const string TestName = "m02.008.efi.integration";
    private const string ReadyMarker =
        "CLROS_M02_008_READY stacks=3 guards=3 doubleFaultIst=1 " +
        "pageFaultIst=2 nmiIst=3 writableMetadata=true " +
        "executableMetadata=false allocationFree=true " +
        "guestArenaAllocated=true guestStackWrites=true " +
        "guestGuardMappings=false guestTss=false guestIdt=false " +
        "arenaFreed=true markerTransport=uefiSerial " +
        "postExitBootServices=false";
    private const string FailureMarker =
        "M02_GUARDED_IST_STACK_ALLOCATION_FAILED";
    private const string CompileClosureSha256 =
        "39395b3906e8875aad6c08386e807d0c9568780091fe2401b69d121ca2454b83";
    private const string ProjectFileSha256 =
        "1f089d541f1451560b1b0801bdf8dcdd05ba282a79a0e9711f6ef089c53284df";
    private const string CanonicalProjectShapeSha256 =
        "ab9195a9121f99096fd5bdde228e75d01a1c375303a1cc14379d23ef8487f7a7";

    private static readonly (string Path, string Sha256)[] ReviewedInputs =
    [
        (
            "src/OS.Kernel/Diagnostics/GuardedISTStacksDoubleFaultPageFaultNMI.cs",
            "cb3deb08256d85ec85cc017aa30938498705ae65956d69235993a411e4fb5703"),
        (
            "tests/OS.HostTests/GuardedISTStacksDoubleFaultPageFaultNMITests.cs",
            "dd82e2462b84effa3f3ec03fc3b64ee8916b0913c6cdb34bac2f3fe276f98f00"),
        (
            "docs/diagnostics.md",
            "bc1a9ffddfe8f71eaf6b729609366efffed7693b92e8851e784624470bfce76a"),
        (
            "docs/boot-order.md",
            "16d88fac99cbb273a4426841cb55e541ad9a2a4ffe151cf435cc02bedd59beac")
    ];

    [ModuleInitializer]
    internal static void RunAll()
    {
        Console.WriteLine("TEST_START " + TestName);
        int failures = 0;

        RunCase(
            "m02.008 EFI primitive and exact source closure are explicit",
            TestPrimitiveAndCompileClosure,
            ref failures);
        RunCase(
            "m02.008 EFI allocates validates touches releases and frees in order",
            TestGuestArenaSequence,
            ref failures);
        RunCase(
            "m02.008 EFI markers are exact mutually exclusive and pre-firmware-exit",
            TestMarkerEncodingAndFailureGate,
            ref failures);
        RunCase(
            "m02.008 QEMU profile is cumulative exact and fail closed",
            TestQemuProfileSource,
            ref failures);
        RunCase(
            "m02.008 reviewed inputs and documentation boundaries remain exact",
            TestReviewedInputsAndDocumentationBoundaries,
            ref failures);

        if (failures != 0)
        {
            Console.WriteLine(
                "TEST_FAIL " + TestName + " host-contract failures=" + failures);
            throw new InvalidOperationException(
                "M02-008 EFI integration host tests failed: " + failures);
        }

        Console.WriteLine("TEST_PASS " + TestName);
    }

    private static void RunCase(
        string name,
        Func<bool> test,
        ref int failures)
    {
        Console.WriteLine("TEST_START " + name);
        try
        {
            if (test())
            {
                Console.WriteLine("TEST_PASS " + name);
                return;
            }

            failures++;
            Console.WriteLine("TEST_FAIL " + name);
        }
        catch (Exception exception)
        {
            failures++;
            Console.WriteLine(
                "TEST_FAIL " + name + " " + exception.GetType().Name + ": " +
                exception.Message);
        }
    }

    private static bool TestPrimitiveAndCompileClosure()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string runtime = File.ReadAllText(
            Path.Combine(root, "src", "OS", "Runtime", "MinimalRuntime.cs"));
        MatchCollection maxValueDeclarations = Regex.Matches(
            runtime,
            @"public\s+const\s+ulong\s+MaxValue\s*=\s*0xffff_ffff_ffff_ffffUL\s*;",
            RegexOptions.CultureInvariant);

        string projectPath = Path.Combine(root, "src", "OS", "OS.csproj");
        XDocument project = XDocument.Load(projectPath);
        XElement[] compileItems = project.Descendants("Compile").ToArray();
        string[] compileKeys = compileItems
            .Select(item =>
                (item.Attribute("Include")?.Value ?? string.Empty) + "|" +
                (item.Attribute("Link")?.Value ?? string.Empty))
            .ToArray();
        string compileHash = Convert.ToHexString(
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(string.Join("\n", compileKeys) + "\n")))
            .ToLowerInvariant();
        string projectHash = Convert.ToHexString(
                SHA256.HashData(File.ReadAllBytes(projectPath)))
            .ToLowerInvariant();

        const string GdtKey =
            @"..\OS.Kernel\Diagnostics\OwnedX64GDTContainingNullN64BitCode.cs|Diagnostics\OwnedX64GDTContainingNullN64BitCode.cs";
        const string GuardedIstKey =
            @"..\OS.Kernel\Diagnostics\GuardedISTStacksDoubleFaultPageFaultNMI.cs|Diagnostics\GuardedISTStacksDoubleFaultPageFaultNMI.cs";
        const string EndianKey =
            @"..\..\std\no-runtime\shared\IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMOSA.cs|Std\IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMOSA.cs";
        int gdtIndex = Array.IndexOf(compileKeys, GdtKey);
        int guardedIstIndex = Array.IndexOf(compileKeys, GuardedIstKey);
        int endianIndex = Array.IndexOf(compileKeys, EndianKey);

        XElement[] defaults = project
            .Descendants("EnableM02GuardedIstStackAllocationProbe")
            .Where(element => element.Parent?.Name.LocalName == "PropertyGroup")
            .ToArray();
        XElement[] symbolGroups = project
            .Descendants("PropertyGroup")
            .Where(element =>
                element.Attribute("Condition")?.Value ==
                    "'$(EnableM02GuardedIstStackAllocationProbe)' == 'true'")
            .ToArray();
        string[] dependencyConditions = project
            .Descendants("Error")
            .Select(element => element.Attribute("Condition")?.Value ?? string.Empty)
            .Where(value => value.Contains(
                "EnableM02GuardedIstStackAllocationProbe",
                StringComparison.Ordinal))
            .ToArray();

        string buildScript = File.ReadAllText(
            Path.Combine(root, "scripts", "build-efi-shell.ps1"));
        string normalizedBuildScript = buildScript.Replace(
            "\r\n",
            "\n",
            StringComparison.Ordinal);
        string verifierScript = File.ReadAllText(
            Path.Combine(root, "scripts", "test-efi-artifact.ps1"));

        return maxValueDeclarations.Count == 1
            && runtime.Contains("public struct UInt64", StringComparison.Ordinal)
            && compileItems.Length == 57
            && compileHash == CompileClosureSha256
            && projectHash == ProjectFileSha256
            && gdtIndex >= 0
            && guardedIstIndex == gdtIndex + 1
            && endianIndex == guardedIstIndex + 1
            && compileKeys.Count(key => key == GuardedIstKey) == 1
            && defaults.Length == 1
            && defaults[0].Value == "false"
            && defaults[0].Attribute("Condition")?.Value ==
                "'$(EnableM02GuardedIstStackAllocationProbe)' == ''"
            && symbolGroups.Length == 1
            && symbolGroups[0].Elements("DefineConstants").Single().Value ==
                "$(DefineConstants);M02_GUARDED_IST_STACK_ALLOCATION_PROBE"
            && dependencyConditions.Length == 1
            && dependencyConditions[0].Contains(
                "EnableM02OwnedX64GdtEncodingProbe",
                StringComparison.Ordinal)
            && buildScript.Contains(
                "[switch] $EnableM02GuardedIstStackAllocationProbe",
                StringComparison.Ordinal)
            && buildScript.Contains(
                "-p:EnableM02GuardedIstStackAllocationProbe=true",
                StringComparison.Ordinal)
            && buildScript.Contains(
                "guarded_ist_stack_allocation_requires_owned_x64_gdt_encoding",
                StringComparison.Ordinal)
            && buildScript.Contains(
                "EFI_PUBLISH_SOURCE_AUTHORITY status=ok inputs=70",
                StringComparison.Ordinal)
            && buildScript.Contains(
                CanonicalProjectShapeSha256,
                StringComparison.Ordinal)
            && buildScript.Contains(GuardedIstKey, StringComparison.Ordinal)
            && verifierScript.Contains(
                "$expectedSourceClosureInputCount = 70",
                StringComparison.Ordinal)
            && verifierScript.Contains(
                "$compileItems.Count -ne 57",
                StringComparison.Ordinal)
            && verifierScript.Contains(GuardedIstKey, StringComparison.Ordinal)
            && verifierScript.Contains(
                "[switch]$EnableM02GuardedIstStackAllocationProbe",
                StringComparison.Ordinal)
            && verifierScript.Contains(
                "-p:EnableM02GuardedIstStackAllocationProbe=true",
                StringComparison.Ordinal)
            && normalizedBuildScript.Contains(
                "$verificationArguments += @(\n" +
                "            \"-EnableM02GuardedIstStackAllocationProbe\",",
                StringComparison.Ordinal);
    }

    private static bool TestGuestArenaSequence()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(root, "src", "OS", "Boot", "EfiEntry.cs"));
        int priorMarker = source.IndexOf(
            "WriteM02OwnedX64GdtEncodingSuccessLine(",
            StringComparison.Ordinal);
        int switchStart = IndexOfAfter(
            source,
            "#if M02_GUARDED_IST_STACK_ALLOCATION_PROBE",
            priorMarker);
        int switchEnd = IndexOfAfter(source, "#endif", switchStart);
        if (switchStart < 0 || switchEnd <= switchStart)
        {
            return false;
        }

        string probe = source[switchStart..switchEnd];
        int bootServicesGate = probe.IndexOf(
            "bootServices == null ||",
            StringComparison.Ordinal);
        int allocatePages = IndexOfAfter(
            probe,
            "bootServices->AllocatePages(",
            bootServicesGate);
        int request = IndexOfAfter(
            probe,
            "new OS.Kernel.Diagnostics\n                            .GuardedIstStackArenaRequest(",
            allocatePages);
        if (request < 0)
        {
            request = IndexOfAfter(
                probe,
                ".GuardedIstStackArenaRequest(",
                allocatePages);
        }
        int initialize = IndexOfAfter(probe, ".Initialize(", request);
        int allocate = IndexOfAfter(probe, ".Allocate(ref m02GuardedState);", initialize);
        int layoutGate = IndexOfAfter(
            probe,
            "bool m02GuardedLayoutValid =",
            allocate);
        int firstStackWrite = IndexOfAfter(
            probe,
            "m02GuardedDoubleFaultBytes[0] = 0xD1;",
            layoutGate);
        int readBack = IndexOfAfter(
            probe,
            "m02GuardedStackReadBack =",
            firstStackWrite);
        int release = IndexOfAfter(
            probe,
            ".Release(",
            readBack);
        int freePages = IndexOfAfter(
            probe,
            "bootServices->FreePages(",
            release);
        int finalFailureGate = IndexOfAfter(
            probe,
            "if (!m02GuardedLayoutValid ||",
            freePages);
        int success = IndexOfAfter(
            probe,
            "WriteM02GuardedIstStackAllocationSuccessLine(",
            finalFailureGate);

        return source.Contains(
                "EfiBootServices* bootServices,\n            int lineIndex)",
                StringComparison.Ordinal) ||
            source.Contains(
                "EfiBootServices* bootServices,\r\n            int lineIndex)",
                StringComparison.Ordinal)
            ? bootServicesGate >= 0
                && allocatePages > bootServicesGate
                && request > allocatePages
                && initialize > request
                && allocate > initialize
                && layoutGate > allocate
                && firstStackWrite > layoutGate
                && readBack > firstStackWrite
                && release > readBack
                && freePages > release
                && finalFailureGate > freePages
                && success > finalFailureGate
                && probe.Contains(
                    "const ulong m02GuardedPageCount = 6;",
                    StringComparison.Ordinal)
                && probe.Contains(
                    "const ulong m02GuardedPageSize = 4096;",
                    StringComparison.Ordinal)
                && probe.Contains("EfiLoaderData", StringComparison.Ordinal)
                && probe.Contains(
                    "0x004D30322D303038UL",
                    StringComparison.Ordinal)
                && probe.Contains(
                    "stackWritable: true",
                    StringComparison.Ordinal)
                && probe.Contains(
                    "stackExecutable: false",
                    StringComparison.Ordinal)
                && probe.Contains(
                    "m02GuardedDoubleFault.IstIndex == 1",
                    StringComparison.Ordinal)
                && probe.Contains(
                    "m02GuardedPageFault.IstIndex == 2",
                    StringComparison.Ordinal)
                && probe.Contains(
                    "m02GuardedNmi.IstIndex == 3",
                    StringComparison.Ordinal)
                && probe.Contains(
                    "!m02GuardedDoubleFault.GuardPagePresent",
                    StringComparison.Ordinal)
                && probe.Contains(
                    "!m02GuardedPageFault.GuardPagePresent",
                    StringComparison.Ordinal)
                && probe.Contains(
                    "!m02GuardedNmi.GuardPagePresent",
                    StringComparison.Ordinal)
                && probe.Contains(
                    "m02GuardedDoubleFaultBytes[m02GuardedPageSize - 1]",
                    StringComparison.Ordinal)
                && probe.Contains(
                    "m02GuardedPageFaultBytes[m02GuardedPageSize - 1]",
                    StringComparison.Ordinal)
                && probe.Contains(
                    "m02GuardedNmiBytes[m02GuardedPageSize - 1]",
                    StringComparison.Ordinal)
                && !probe.Contains(
                    "(byte*)m02GuardedDoubleFault.GuardBaseAddress",
                    StringComparison.Ordinal)
                && !probe.Contains(
                    "(byte*)m02GuardedPageFault.GuardBaseAddress",
                    StringComparison.Ordinal)
                && !probe.Contains(
                    "(byte*)m02GuardedNmi.GuardBaseAddress",
                    StringComparison.Ordinal)
                && CountOccurrences(probe, ".Initialize(") == 1
                && CountOccurrences(probe, ".Allocate(ref m02GuardedState);") == 1
                && CountOccurrences(probe, ".Release(") == 1
                && CountOccurrences(probe, "m02GuardedStackReadBack =") == 2
            : false;
    }

    private static bool TestMarkerEncodingAndFailureGate()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(root, "src", "OS", "Boot", "EfiEntry.cs"));
        string normalizedSource = source.Replace(
            "\r\n",
            "\n",
            StringComparison.Ordinal);
        string reconstructedReady = ExtractAsciiMarker(
            normalizedSource,
            "private static void\n            WriteM02GuardedIstStackAllocationSuccessLine(");
        string reconstructedFailure = ExtractAsciiMarker(
            normalizedSource,
            "private static void\n            WriteM02GuardedIstStackAllocationFailureLine(");

        int finalGate = source.IndexOf(
            "if (!m02GuardedLayoutValid ||",
            StringComparison.Ordinal);
        int failureCall = IndexOfAfter(
            source,
            "WriteM02GuardedIstStackAllocationFailureLine(",
            finalGate);
        int abortReturn = IndexOfAfter(source, "return;", failureCall);
        int successCall = IndexOfAfter(
            source,
            "WriteM02GuardedIstStackAllocationSuccessLine(",
            abortReturn);

        return reconstructedReady == ReadyMarker
            && reconstructedFailure == FailureMarker
            && ReadyMarker.Contains(
                "guestGuardMappings=false",
                StringComparison.Ordinal)
            && ReadyMarker.Contains("guestTss=false", StringComparison.Ordinal)
            && ReadyMarker.Contains("guestIdt=false", StringComparison.Ordinal)
            && ReadyMarker.Contains("arenaFreed=true", StringComparison.Ordinal)
            && ReadyMarker.Contains(
                "postExitBootServices=false",
                StringComparison.Ordinal)
            && !ReadyMarker.Contains("CloudReady", StringComparison.OrdinalIgnoreCase)
            && !ReadyMarker.Contains("production", StringComparison.OrdinalIgnoreCase)
            && finalGate >= 0
            && failureCall > finalGate
            && abortReturn > failureCall
            && successCall > abortReturn
            && CountOccurrences(
                source,
                "WriteM02GuardedIstStackAllocationSuccessLine(") == 2
            && CountOccurrences(
                source,
                "WriteM02GuardedIstStackAllocationFailureLine(") == 4
            && !source.Contains("\"" + ReadyMarker + "\"", StringComparison.Ordinal)
            && !source.Contains("\"" + FailureMarker + "\"", StringComparison.Ordinal);
    }

    private static bool TestQemuProfileSource()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(root, "scripts", "run-qemu-smoke.ps1"));
        int guardedProfile = source.IndexOf(
            "if ($EnableM02GuardedIstStackAllocationProbe) {",
            StringComparison.Ordinal);
        int gdtProfile = IndexOfAfter(
            source,
            "elseif ($EnableM02OwnedX64GdtEncodingProbe) {",
            guardedProfile);
        string profile = guardedProfile >= 0 && gdtProfile > guardedProfile
            ? source[guardedProfile..gdtProfile]
            : string.Empty;

        return source.Contains(
                "[switch] $EnableM02GuardedIstStackAllocationProbe",
                StringComparison.Ordinal)
            && source.Contains(
                "$script:M02GuardedIstStackAllocationReadyMarker =",
                StringComparison.Ordinal)
            && source.Contains(ReadyMarker, StringComparison.Ordinal)
            && source.Contains(FailureMarker, StringComparison.Ordinal)
            && source.Contains(
                "$script:M02GuardedIstStackAllocationRequiredMarkers",
                StringComparison.Ordinal)
            && source.Contains(
                "$script:M02GuardedIstStackAllocationForbiddenMarkers",
                StringComparison.Ordinal)
            && source.Contains(
                "$marker -eq $script:M02OwnedX64GdtEncodingReadyMarker",
                StringComparison.Ordinal)
            && source.Contains(
                "m02_guarded_ist_stack_allocation_probe_requires_owned_x64_gdt_encoding",
                StringComparison.Ordinal)
            && source.Contains(
                "m02_guarded_ist_stack_allocation_probe_requires_build_efi",
                StringComparison.Ordinal)
            && source.Contains(
                "m02_guarded_ist_stack_allocation_probe_forbids_skip_image_build",
                StringComparison.Ordinal)
            && source.Contains(
                "-EnableM02GuardedIstStackAllocationProbe",
                StringComparison.Ordinal)
            && source.Contains(
                "profileRequired=56 profileForbidden=40",
                StringComparison.Ordinal)
            && source.Contains(
                "profileRequired=55 profileForbidden=39",
                StringComparison.Ordinal)
            && CountOccurrences(
                profile,
                "Merge-QemuSmokeMarkerList -Values @(") == 4
            && guardedProfile >= 0
            && gdtProfile > guardedProfile;
    }

    private static bool TestReviewedInputsAndDocumentationBoundaries()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        foreach ((string relativePath, string expectedSha256) in ReviewedInputs)
        {
            string path = Path.Combine(
                root,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
            {
                return false;
            }

            string actual = Convert.ToHexString(
                    SHA256.HashData(File.ReadAllBytes(path)))
                .ToLowerInvariant();
            if (actual != expectedSha256)
            {
                return false;
            }
        }

        string efiBuild = File.ReadAllText(
            Path.Combine(root, "docs", "efi-build.md"));
        string qemu = File.ReadAllText(
            Path.Combine(root, "docs", "qemu.md"));
        string readme = File.ReadAllText(Path.Combine(root, "README.md"));

        return efiBuild.Contains(
                "EnableM02GuardedIstStackAllocationProbe",
                StringComparison.Ordinal)
            && efiBuild.Contains("six", StringComparison.OrdinalIgnoreCase)
            && efiBuild.Contains("UEFI LoaderData", StringComparison.Ordinal)
            && efiBuild.Contains(
                "pre-`ExitBootServices`",
                StringComparison.Ordinal)
            && efiBuild.Contains(
                "does not make the guard-intent pages non-present",
                StringComparison.Ordinal)
            && qemu.Contains(
                "CLROS_M02_008_READY",
                StringComparison.Ordinal)
            && qemu.Contains(
                "guestGuardMappings=false",
                StringComparison.Ordinal)
            && qemu.Contains(
                "M02_GUARDED_IST_STACK_ALLOCATION_FAILED",
                StringComparison.Ordinal)
            && qemu.Contains(
                "postExitBootServices=false",
                StringComparison.Ordinal)
            && readme.Contains(
                "M02-008",
                StringComparison.Ordinal)
            && readme.Contains(
                "temporary six-page UEFI LoaderData arena",
                StringComparison.Ordinal)
            && readme.Contains(
                "does not install a TSS, IST, GDT, or IDT",
                StringComparison.Ordinal);
    }

    private static string ExtractAsciiMarker(string source, string methodToken)
    {
        int start = source.IndexOf(methodToken, StringComparison.Ordinal);
        int end = start < 0
            ? -1
            : source.IndexOf(
                "        private static ",
                start + methodToken.Length,
                StringComparison.Ordinal);
        if (start < 0 || end <= start)
        {
            return string.Empty;
        }

        MatchCollection characters = Regex.Matches(
            source[start..end],
            @"\(byte\)'(?<value>.)'",
            RegexOptions.CultureInvariant);
        return string.Concat(
            characters
                .Cast<Match>()
                .Select(match => match.Groups["value"].Value));
    }

    private static int CountOccurrences(string source, string value)
    {
        int count = 0;
        int index = 0;
        while ((index = source.IndexOf(
                   value,
                   index,
                   StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static int IndexOfAfter(
        string source,
        string value,
        int startIndex)
    {
        if (startIndex < 0 || startIndex > source.Length)
        {
            return -1;
        }

        return source.IndexOf(value, startIndex, StringComparison.Ordinal);
    }
}
