using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;

internal static class EfiArtifactVerificationScriptTests
{
    private const int PeOffset = 0x80;
    private const int OptionalHeaderOffset = PeOffset + 24;
    private const int SectionTableOffset = OptionalHeaderOffset + 0xf0;
    private const int TextSectionHeaderOffset = SectionTableOffset;
    private const int PdataSectionHeaderOffset = SectionTableOffset + 40;
    private const int XdataSectionHeaderOffset = SectionTableOffset + 80;
    private const int TextRawOffset = 0x200;
    private const int PdataRawOffset = 0x400;
    private const int XdataRawOffset = 0x600;
    private const uint TextRva = 0x1000;
    private const uint PdataRva = 0x2000;
    private const uint XdataRva = 0x3000;
    private const uint CoffTimestamp = 0x6a651aa5;
    private const ulong ImageBase = 0x0000000180000000;
    private const uint EntryPointRva = TextRva;
    private const uint SerializerRva = TextRva + 0x100;
    private const uint SerializerEndRva = SerializerRva + 20;
    private const uint SerializerExpectedSuccessCookie = 0x53524c43;
    private const int MaximumRuntimeFunctionRecords = 2048;
    private const int MaximumLinkResponseTokens = 4096;
    private const int VerifierFixtureProcessTimeoutMilliseconds = 120_000;
    private const int ExpectedSourceClosureInputCount = 115;
    private const string SerializerSymbol =
        "CLROS_X64_SerializeInstructionPublication";
    private const string ToolDirectoryBuildPropsPath =
        "tools/Directory.Build.props";
    private const string SerializerBytesSha256 =
        "df76c8e39e0768d26ea27206ff102d7eadcc35d2c9bdf547b52b1171fd47bdaf";
    private const string SerializerObjectSha256 =
        "da97e50c95e479541b65eb3f96d9c6ded163735857650bee9416324086d55c7b";
    private const string SerializerObjectBase64 =
        "ZIYDAAAAAADSAAAAAgAAAAAAAAAvNAAAAAAAAAAAAAAAAAAAFAAAAIwAAAAAAAAAAAAAAAAAAAAgAFBgLzE4AAAAAAAAAAAAAAAAAAgAAACgAAAAAAAAAAAAAAAAAAAAQAAwQC8zMwAAAAAAAAAAAAAAAAAMAAAAqAAAALQAAAAAAAAAAwAAAEAAMEBTuAAAAAC5AAAAAA+iuENMUlNbwwEBAQABMAAAAAAAABQAAAAAAAAAAAAAAAAAAAADAAQAAAAAAAAAAwAIAAAAAQAAAAMAAAAAADAAAAAAAAAAAQAgAAIAAAAAABIAAAAAAAAAAgAAAAMAWgAAAC50ZXh0JGNscm9zc20ALnhkYXRhJGNscm9zc20ALnBkYXRhJGNscm9zc20AQ0xST1NfWDY0X1NlcmlhbGl6ZUluc3RydWN0aW9uUHVibGljYXRpb24A";
    private const string EmptySha256 =
        "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
    private const string CompileClosureSha256 =
        "cd11b6f11911097ee759222b6d14c97a9631c711ef5f655ab3fd192dd416eb65";
    private const string AnalyzerClosureSha256 =
        "bea3e700acbeec374acb02a39b8c154e03291cf380edf850fea2114193b7ea14";
    private const string ManagedFixtureConfiguration = "Release";
    private static readonly object SchemaThreeFixtureLock = new();
    private static readonly string SchemaThreeFixtureRepositoryRoot =
        Path.Combine(
            Path.GetTempPath(),
            "clros-efi-schema3-repository-" +
                Guid.NewGuid().ToString("N"));
    private static string? cachedDotnetHostPath;
    private static string? cachedDotnetSdkVersion;
    private static SchemaThreeFixture? cachedSchemaThreeFixture;

    private static readonly byte[] ExpectedSerializerBytes =
    [
        0x53,
        0xB8, 0x00, 0x00, 0x00, 0x00,
        0xB9, 0x00, 0x00, 0x00, 0x00,
        0x0F, 0xA2,
        0xB8, 0x43, 0x4C, 0x52, 0x53,
        0x5B,
        0xC3
    ];

    private static readonly byte[] ExpectedUnwindBytes =
    [
        0x01, 0x01, 0x01, 0x00,
        0x01, 0x30, 0x00, 0x00
    ];

    private static readonly RestoreArchiveDescriptor[] ExpectedRestoreArchives =
    [
        new(
            "microsoft.dotnet.ilcompiler",
            "10.0.8",
            "microsoft.dotnet.ilcompiler.10.0.8.nupkg",
            "ccdc381bbc7672c48bc755311fdf2f5907433933c295198172d2ad6958ed10b4"),
        new(
            "microsoft.net.illink.tasks",
            "10.0.10",
            "microsoft.net.illink.tasks.10.0.10.nupkg",
            "97a0eca2030901738f070f142aaf02b3b1aead2bc984c08c428847910b4be312"),
        new(
            "runtime.win-x64.microsoft.dotnet.ilcompiler",
            "10.0.8",
            "runtime.win-x64.microsoft.dotnet.ilcompiler.10.0.8.nupkg",
            "49f15032ef981172b8c25360f97b76d8abab75348f8e16da0a3fbb970c10bec8"),
        new(
            "runtime.win-x64.microsoft.dotnet.ilcompiler",
            "10.0.10",
            "runtime.win-x64.microsoft.dotnet.ilcompiler.10.0.10.nupkg",
            "0ae1c40ef28915e63c257999d4cd860db9086878c3a6f8e30388e00056ab350a"),
        new(
            "microsoft.netcore.app.runtime.nativeaot.win-x64",
            "10.0.10",
            "microsoft.netcore.app.runtime.nativeaot.win-x64.10.0.10.nupkg",
            "5fd7129af9eed4b5a2ed04219b636f7ba0dd965569275d3379fc4d02f623e626")
    ];

    private static readonly PackageDescriptor[] ExpectedPackages =
    [
        new(
            "microsoft.dotnet.ilcompiler",
            "10.0.8",
            "restore",
            "RJxitcN5CCyZDcPNXKLsecwKvACzmy8C1z8hGM9+hFcnPhv1jDysJFFIeUHIPWaZ6wDAfYtZcgKEtegvL2Nz8A==",
            20,
            404859,
            "b9c5a195c93e515f34066c9d8b0b3827cf1a32ce681454d284c489622a5bfcde"),
        new(
            "microsoft.net.illink.tasks",
            "10.0.10",
            "restore",
            "f5VCIE7AJpd5YvzNTeMGVzQIgyE9tX+AreTYwQF+REbu+DZo/2Ae+jNSwhPEYrVz6RRkd7y8ubXjk6Nn6Ka+Cg==",
            42,
            5337318,
            "0edc00810e9462f8d6a7468835861db6d2833f970539e6d4eb82cde5eb42ffb6"),
        new(
            "runtime.win-x64.microsoft.dotnet.ilcompiler",
            "10.0.8",
            "restore",
            "S+9omeowlFjRReD0VB/Q4u3DcN7+vueJSF37saFj0U2OSJIkfI7gGKQRsDxGIf0MwLha14qGrjRC3bDGhvF0Gg==",
            13,
            37461393,
            "c5aff04c7a4abcca691fc0e6a8539628cded3250a7777d6c407dc64c07ca858e"),
        new(
            "runtime.win-x64.microsoft.dotnet.ilcompiler",
            "10.0.10",
            "restore-only",
            "e5P00ObvdmAl49KkvGvqDLE5+EXu/IdXCDsLghgKFXKrNybyxMmH12HuGrsO7LTIGjXI58tyN3Hwxwr/MR0d8A==",
            13,
            37473738,
            "17a69cfcd4a3a770d27b7315313c85f68b3a956878af4b95699f4011572e63fb"),
        new(
            "microsoft.netcore.app.runtime.nativeaot.win-x64",
            "10.0.10",
            "restore-only",
            "NHkgRRE/Fbaw/z0eLdC7rNF7ZRoR+2KQGSPh2ZiKgXpuZOz3Cjhv0srLOaeYnvYMDtT+KzjyyZGgChXx1W18wA==",
            406,
            109133220,
            "389ade21d811efc6fc4d6f0f1aa492ba71f5fabfb39e4780b83c5a65e9a680c4"),
        new(
            "microsoft.netcore.app.runtime.nativeaot.win-x64",
            "10.0.8",
            "runtime",
            "4OPm4RaS1aAEf0GOcA/IsdD/ml9o6Hw2BaiHy/iceHxExoj+38QFAyNTxcNAz73SDPHXpUp4SL046vBq5RkuEQ==",
            406,
            109016717,
            "73972c0d17f29fa389de9c4a87051b372fa15afe5cdadf55bef731a3d8144fbc")
    ];

    private static readonly string[] PackageBuildClosurePaths =
    [
        "microsoft.dotnet.ilcompiler/10.0.8/build/Microsoft.DotNet.ILCompiler.SingleEntry.targets",
        "microsoft.dotnet.ilcompiler/10.0.8/build/Microsoft.NETCore.Native.Publish.targets",
        "microsoft.dotnet.ilcompiler/10.0.8/build/Microsoft.NETCore.Native.Windows.targets",
        "microsoft.dotnet.ilcompiler/10.0.8/build/Microsoft.NETCore.Native.targets",
        "microsoft.dotnet.ilcompiler/10.0.8/tools/netstandard/ILCompiler.Build.Tasks.dll",
        "microsoft.dotnet.ilcompiler/10.0.8/tools/netstandard/ILCompiler.Build.Tasks.deps.json",
        "microsoft.net.illink.tasks/10.0.10/build/Microsoft.NET.ILLink.targets",
        "microsoft.net.illink.tasks/10.0.10/tools/net/ILLink.Tasks.dll",
        "microsoft.net.illink.tasks/10.0.10/tools/net/ILLink.Tasks.deps.json"
    ];

    private static readonly string[] ActivePackageImportEdges =
    [
        "sdk|microsoft.dotnet.ilcompiler/10.0.8/build/Microsoft.DotNet.ILCompiler.SingleEntry.targets",
        "microsoft.dotnet.ilcompiler/10.0.8/build/Microsoft.DotNet.ILCompiler.SingleEntry.targets|microsoft.dotnet.ilcompiler/10.0.8/build/Microsoft.NETCore.Native.targets",
        "microsoft.dotnet.ilcompiler/10.0.8/build/Microsoft.NETCore.Native.targets|microsoft.dotnet.ilcompiler/10.0.8/build/Microsoft.NETCore.Native.Windows.targets",
        "microsoft.dotnet.ilcompiler/10.0.8/build/Microsoft.NETCore.Native.targets|microsoft.dotnet.ilcompiler/10.0.8/build/Microsoft.NETCore.Native.Publish.targets",
        "sdk|microsoft.net.illink.tasks/10.0.10/build/Microsoft.NET.ILLink.targets"
    ];

    private static readonly string[] PackageTaskBindings =
    [
        "ComputeManagedAssembliesToCompileToNative|microsoft.dotnet.ilcompiler/10.0.8/tools/netstandard/ILCompiler.Build.Tasks.dll",
        "ComputeManagedAssemblies|microsoft.net.illink.tasks/10.0.10/tools/net/ILLink.Tasks.dll"
    ];

    [ModuleInitializer]
    internal static void Run()
    {
        int failures = 0;
        string tempRoot = Path.Combine(
            Path.GetTempPath(),
            "clros-efi-artifact-verifier-tests-" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(tempRoot);
            Run(
                "efi artifact verifier accepts linked serializer PE, map, object, and unwind evidence",
                () => TestValidArtifactPasses(tempRoot),
                ref failures);
            Run(
                "efi artifact verifier implies the M02-013 owned-COM1 chain from one switch",
                () => TestM02Ud2VectorStateSingleSwitchImplication(tempRoot),
                ref failures);
            Run(
                "efi artifact verifier rejects missing and truncated inputs",
                () => TestMissingAndTruncatedArtifactsFail(tempRoot),
                ref failures);
            Run(
                "efi artifact verifier rejects non-x64 and non-PE32+ headers",
                () => TestMachineAndFormatFailures(tempRoot),
                ref failures);
            Run(
                "efi artifact verifier rejects non-EFI and zero entrypoint headers",
                () => TestSubsystemAndEntrypointFailures(tempRoot),
                ref failures);
            Run(
                "efi artifact verifier rejects non-executable and virtual-only entrypoints",
                () => TestEntrypointSectionFailures(tempRoot),
                ref failures);
            Run(
                "efi artifact verifier rejects ordinary runtime import directories",
                () => TestImportDirectoriesFail(tempRoot),
                ref failures);
            Run(
                "efi artifact verifier rejects missing EfiMain source contract",
                () => TestEfiMainContractFailure(tempRoot),
                ref failures);
            Run(
                "efi artifact verifier rejects a synthetic RET-only entrypoint",
                () => TestTrivialEntrypointFails(tempRoot),
                ref failures);
            Run(
                "efi artifact verifier rejects overflowing and out-of-image PE ranges",
                () => TestPeLayoutBoundaryFailures(tempRoot),
                ref failures);
            Run(
                "efi artifact verifier rejects malformed exception-directory contracts",
                () => TestExceptionDirectoryFailures(tempRoot),
                ref failures);
            Run(
                "efi artifact verifier rejects invalid runtime-function tables",
                () => TestRuntimeFunctionFailures(tempRoot),
                ref failures);
            Run(
                "efi artifact verifier rejects mutable, misaligned, and non-file-backed serializer text",
                () => TestSerializerTextFailures(tempRoot),
                ref failures);
            Run(
                "efi artifact verifier rejects malformed or mutable serializer unwind data",
                () => TestSerializerUnwindFailures(tempRoot),
                ref failures);
            Run(
                "efi artifact verifier rejects missing, duplicate, and fallback serializer link bindings",
                () => TestSerializerLinkAssociationFailures(tempRoot),
                ref failures);
            Run(
                "efi artifact verifier rejects missing, stale, and malformed schema-three evidence",
                () => TestLinkEvidenceFailures(tempRoot),
                ref failures);
            Run(
                "efi artifact verifier rejects unauthorized restore and package authority",
                () => TestSchemaThreeAuthorityFailures(tempRoot),
                ref failures);
            Run(
                "efi artifact verifier pins project and entry source authority",
                () => TestPinnedRepositoryContractPaths(tempRoot),
                ref failures);
            Run(
                "efi artifact verifier isolates bounded external evaluation",
                () => TestBoundedExternalEvaluationRunner(tempRoot),
                ref failures);
            Run(
                "efi artifact verifier bounds response and runtime parsing",
                () => TestParserCapacityBounds(tempRoot),
                ref failures);
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, recursive: true);
                }
            }
            catch
            {
                // Cleanup failure must not conceal verifier contract results.
            }
            DeleteSchemaThreeFixture();
        }

        if (failures != 0)
        {
            throw new InvalidOperationException(
                "EFI artifact verification script tests failed: " + failures);
        }
    }

    private static void Run(string name, Func<bool> test, ref int failures)
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
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine(
                "TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static bool TestValidArtifactPasses(string tempRoot)
    {
        string artifact = WriteFixture(tempRoot, "valid artifact with spaces", CreateValidPe());
        LinkEvidencePaths paths = WriteLinkEvidence(artifact);
        ProcessResult result = RunVerifier(
            artifact,
            serializerObjectPath: paths.ObjectPath);

        bool passed = result.ExitCode == 0
            && !result.TimedOut
            && result.Output.Contains(
                "TEST_START efi.artifact.verify",
                StringComparison.Ordinal)
            && result.Output.Contains(
                "EFI_ARTIFACT_PE format=PE32+ machine=0x8664 subsystem=10 entryRva=0x00001000 entrySection=.text",
                StringComparison.Ordinal)
            && result.Output.Contains(
                "EFI_ARTIFACT_IMPORTS import=absent bound-import=absent iat=absent delay-import=absent",
                StringComparison.Ordinal)
            && result.Output.Contains(
                "EFI_ARTIFACT_ENTRY_CONTRACT symbol=EfiMain managedSymbol=OS_OS_Boot_EfiEntry__EfiMain association=same-build-linker-map rva=0x00001000",
                StringComparison.Ordinal)
            && result.Output.Contains(
                "EFI_ARTIFACT_EXCEPTION_DIRECTORY section=.pdata rva=0x00002000 size=12 records=1",
                StringComparison.Ordinal)
            && result.Output.Contains(
                "EFI_ARTIFACT_RUNTIME_SCAN records=1 distinctUnwind=1 elapsedMs=",
                StringComparison.Ordinal)
            && result.Output.Contains(
                "maximumRecords=2048",
                StringComparison.Ordinal)
            && result.Output.Contains(
                "EFI_ARTIFACT_SERIALIZER symbol=CLROS_X64_SerializeInstructionPublication section=.text rva=0x00001100 size=20 unwindRva=0x00003000 sha256=" +
                SerializerBytesSha256,
                StringComparison.Ordinal)
            && result.Output.Contains(
                "expectedCookie=0x53524c43",
                StringComparison.Ordinal)
            && !result.Output.Contains(
                " cookie=0x53524c43",
                StringComparison.Ordinal)
            && result.Output.Contains(
                "EFI_ARTIFACT_SERIALIZER_OBJECT path=",
                StringComparison.Ordinal)
            && result.Output.Contains(
                "bytes=336 sha256=" + SerializerObjectSha256,
                StringComparison.Ordinal)
            && result.Output.Contains("EFI_ARTIFACT_LINK_MAP path=", StringComparison.Ordinal)
            && result.Output.Contains("EFI_ARTIFACT_LINK_RESPONSE path=", StringComparison.Ordinal)
            && result.Output.Contains("EFI_ARTIFACT_SHA256 ", StringComparison.Ordinal)
            && result.Output.Contains("TEST_PASS efi.artifact.verify", StringComparison.Ordinal)
            && !result.Output.Contains("TEST_FAIL efi.artifact.verify", StringComparison.Ordinal);
        if (!passed)
        {
            WriteFixtureDiagnostic("valid", result);
        }

        return passed;
    }

    private static bool TestM02Ud2VectorStateSingleSwitchImplication(
        string tempRoot)
    {
        string artifact = WriteFixture(
            tempRoot,
            "m02 ud2 vector state artifact",
            CreateValidPe());
        LinkEvidencePaths paths = WriteLinkEvidence(artifact);
        string evidencePath = artifact + ".link.json";
        JsonObject evidence = JsonNode.Parse(File.ReadAllText(evidencePath))!
            .AsObject();
        JsonObject properties = evidence["postRestoreEvaluation"]!["properties"]!
            .AsObject();
        properties["EnableM02OwnedCom1ReadyProbe"] = "true";
        properties["EnableM02PanicSafeOwnedCom1Probe"] = "true";
        properties["EnableM02Ud2VectorStateProbe"] = "true";
        properties["EnableM01LoadedImageStackProbe"] = "true";
        properties["EnableM01LiveAllocatorProbe"] = "true";
        properties["EnableM01OwnedPagingProbe"] = "true";
        properties["EnableM02SafeStubProbe"] = "true";
        properties["EnableM02CheckedPortIoBindingProbe"] = "true";
        properties["EnableM02Com1OwnedPortIoProbe"] = "true";
        File.WriteAllText(
            evidencePath,
            evidence.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true
            }));

        ProcessResult enabled = RunVerifier(
            artifact,
            serializerObjectPath: paths.ObjectPath,
            additionalArguments: ["-EnableM02Ud2VectorStateProbe"]);
        ProcessResult ownedX64GdtConflict = RunVerifier(
            artifact,
            serializerObjectPath: paths.ObjectPath,
            additionalArguments:
            [
                "-EnableM02Ud2VectorStateProbe",
                "-EnableM02OwnedX64GdtEncodingProbe"
            ]);
        ProcessResult guardedIstConflict = RunVerifier(
            artifact,
            serializerObjectPath: paths.ObjectPath,
            additionalArguments:
            [
                "-EnableM02Ud2VectorStateProbe",
                "-EnableM02GuardedIstStackAllocationProbe"
            ]);
        ProcessResult m04Conflict = RunVerifier(
            artifact,
            serializerObjectPath: paths.ObjectPath,
            additionalArguments:
            [
                "-EnableM02Ud2VectorStateProbe",
                "-EnableM04IndependentEndianConformanceProbe",
                "-EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe"
            ]);
        string verifierScript = File.ReadAllText(Path.Combine(
            HostToolProcess.FindRepositoryRoot(),
            "scripts",
            "test-efi-artifact.ps1"));

        bool passed = enabled.ExitCode == 0
            && enabled.Output.Contains(
                "EFI_ARTIFACT_M02_UD2_VECTOR_STATE_IMPLICATION status=ok m01LoadedImageStack=true m01LiveAllocator=true ownedPaging=true m02SafeStub=true checkedPortIoBinding=true com1OwnedPortIo=true ownedCom1Ready=true panicSafeOwnedCom1=true",
                StringComparison.Ordinal)
            && enabled.Output.Contains(
                "TEST_PASS efi.artifact.verify",
                StringComparison.Ordinal)
            && FailedWith(
                ownedX64GdtConflict,
                "m02_ud2_vector_state_probe_forbids_owned_x64_gdt_encoding_probe")
            && FailedWith(
                guardedIstConflict,
                "m02_ud2_vector_state_probe_forbids_guarded_ist_stack_allocation_probe")
            && m04Conflict.ExitCode != 0
            && m04Conflict.Output.Contains(
                "m04_span_memory_conformance_forbids_m02_ud2_vector_state",
                StringComparison.Ordinal)
            && verifierScript.Contains(
                "m02_ud2_vector_state_probe_explicit_false_prerequisite",
                StringComparison.Ordinal);
        if (!passed)
        {
            Console.WriteLine(
                "M02-013 artifact implication outputs:\n"
                + enabled.Output
                + ownedX64GdtConflict.Output
                + guardedIstConflict.Output
                + m04Conflict.Output);
        }

        return passed;
    }

    private static bool TestMissingAndTruncatedArtifactsFail(string tempRoot)
    {
        string missing = Path.Combine(tempRoot, "missing", "BOOTX64.EFI");
        ProcessResult missingResult = RunVerifier(missing);

        string empty = WriteFixture(tempRoot, "empty", []);
        ProcessResult emptyResult = RunPeFixtureVerifier(empty);

        string truncated = WriteFixture(tempRoot, "truncated", [0x4d, 0x5a]);
        ProcessResult truncatedResult = RunPeFixtureVerifier(truncated);

        return FailedWith(missingResult, "missing_artifact")
            && FailedWith(emptyResult, "empty_artifact")
            && FailedWith(truncatedResult, "truncated_dos_header");
    }

    private static bool TestMachineAndFormatFailures(string tempRoot)
    {
        byte[] wrongMachine = CreateValidPe();
        WriteUInt16(wrongMachine, PeOffset + 4, 0x014c);
        ProcessResult machineResult = RunPeFixtureVerifier(
            WriteFixture(tempRoot, "wrong-machine", wrongMachine));

        byte[] pe32 = CreateValidPe();
        WriteUInt16(pe32, OptionalHeaderOffset, 0x010b);
        ProcessResult formatResult = RunPeFixtureVerifier(
            WriteFixture(tempRoot, "wrong-format", pe32));

        return FailedWith(machineResult, "machine_not_x64")
            && machineResult.Output.Contains("actual=0x014c", StringComparison.Ordinal)
            && FailedWith(formatResult, "format_not_pe32_plus")
            && formatResult.Output.Contains("actual=0x010b", StringComparison.Ordinal);
    }

    private static bool TestSubsystemAndEntrypointFailures(string tempRoot)
    {
        byte[] wrongSubsystem = CreateValidPe();
        WriteUInt16(wrongSubsystem, OptionalHeaderOffset + 68, 3);
        ProcessResult subsystemResult = RunPeFixtureVerifier(
            WriteFixture(tempRoot, "wrong-subsystem", wrongSubsystem));

        byte[] zeroEntrypoint = CreateValidPe();
        WriteUInt32(zeroEntrypoint, OptionalHeaderOffset + 16, 0);
        ProcessResult entrypointResult = RunPeFixtureVerifier(
            WriteFixture(tempRoot, "zero-entrypoint", zeroEntrypoint));

        byte[] zeroImage = CreateValidPe();
        WriteUInt32(zeroImage, OptionalHeaderOffset + 56, 0);
        ProcessResult imageResult = RunPeFixtureVerifier(
            WriteFixture(tempRoot, "zero-size-of-image", zeroImage));

        return FailedWith(subsystemResult, "subsystem_not_efi_application")
            && FailedWith(entrypointResult, "entrypoint_zero")
            && FailedWith(imageResult, "size_of_image_zero");
    }

    private static bool TestEntrypointSectionFailures(string tempRoot)
    {
        byte[] notExecutable = CreateValidPe();
        WriteUInt32(notExecutable, TextSectionHeaderOffset + 36, 0x40000020);
        ProcessResult executableResult = RunPeFixtureVerifier(
            WriteFixture(tempRoot, "non-executable-entry", notExecutable));

        byte[] virtualOnly = CreateValidPe();
        WriteUInt32(virtualOnly, OptionalHeaderOffset + 16, 0x1200);
        WriteUInt32(virtualOnly, TextSectionHeaderOffset + 8, 0x300);
        ProcessResult fileBackedResult = RunPeFixtureVerifier(
            WriteFixture(tempRoot, "virtual-only-entry", virtualOnly));

        return FailedWith(executableResult, "entrypoint_section_not_executable")
            && FailedWith(fileBackedResult, "entrypoint_not_file_backed");
    }

    private static bool TestImportDirectoriesFail(string tempRoot)
    {
        byte[] importTable = CreateValidPe();
        WriteDataDirectory(importTable, 1, 0x1180, 20);
        ProcessResult importResult = RunPeFixtureVerifier(
            WriteFixture(tempRoot, "ordinary-import", importTable));

        byte[] delayImport = CreateValidPe();
        WriteDataDirectory(delayImport, 13, 0x11a0, 32);
        ProcessResult delayResult = RunPeFixtureVerifier(
            WriteFixture(tempRoot, "delay-import", delayImport));

        byte[] malformedImport = CreateValidPe();
        WriteDataDirectory(malformedImport, 1, 0x1180, 0);
        ProcessResult malformedResult = RunPeFixtureVerifier(
            WriteFixture(tempRoot, "malformed-import", malformedImport));

        return FailedWith(importResult, "ordinary_runtime_imports_present")
            && importResult.Output.Contains("directory=import", StringComparison.Ordinal)
            && FailedWith(delayResult, "ordinary_runtime_imports_present")
            && delayResult.Output.Contains("directory=delay-import", StringComparison.Ordinal)
            && FailedWith(malformedResult, "malformed_import_directory");
    }

    private static bool TestEfiMainContractFailure(string tempRoot)
    {
        string artifact = WriteFixture(tempRoot, "contract-artifact", CreateValidPe());
        WriteLinkEvidence(artifact);
        SchemaThreeFixture fixture = cachedSchemaThreeFixture
            ?? throw new InvalidOperationException(
                "Schema-three fixture was not initialized.");
        string sourcePath = Path.Combine(
            fixture.RepositoryRoot,
            "src",
            "OS",
            "Boot",
            "EfiEntry.cs");
        byte[] originalSource = File.ReadAllBytes(sourcePath);
        ProcessResult result;
        try
        {
            File.WriteAllText(
                sourcePath,
                "internal static class NoEfiMain { }",
                new UTF8Encoding(false));
            result = RunVerifier(artifact);
        }
        finally
        {
            File.WriteAllBytes(sourcePath, originalSource);
        }

        return FailedWith(result, "efimain_source_contract_missing")
            && !result.Output.Contains(
                "EFI_ARTIFACT_SERIALIZER",
                StringComparison.Ordinal);
    }

    private static bool TestTrivialEntrypointFails(string tempRoot)
    {
        byte[] trivial = CreateValidPe();
        trivial[TextRawOffset] = 0xc3;
        string artifact = WriteFixture(tempRoot, "ret-only-entrypoint", trivial);

        ProcessResult result = RunPeFixtureVerifier(artifact);
        return FailedWith(result, "entrypoint_trivial_stub")
            && result.Output.Contains("byte=0xc3", StringComparison.Ordinal)
            && !result.Output.Contains(
                "EFI_ARTIFACT_ENTRY_CONTRACT",
                StringComparison.Ordinal);
    }

    private static bool TestPeLayoutBoundaryFailures(string tempRoot)
    {
        byte[] oversizedPeOffset = CreateValidPe();
        WriteUInt32(oversizedPeOffset, 0x3c, uint.MaxValue);
        ProcessResult oversizedPeOffsetResult = RunPeFixtureVerifier(
            WriteFixture(tempRoot, "pe-offset-overflow", oversizedPeOffset));

        byte[] undersizedHeaders = CreateValidPe();
        WriteUInt32(undersizedHeaders, OptionalHeaderOffset + 60, 0x180);
        ProcessResult undersizedHeadersResult = RunPeFixtureVerifier(
            WriteFixture(tempRoot, "section-table-outside-headers", undersizedHeaders));

        byte[] rawHeaderOverlap = CreateValidPe();
        WriteUInt32(rawHeaderOverlap, TextSectionHeaderOffset + 20, 0x100);
        ProcessResult rawHeaderOverlapResult = RunPeFixtureVerifier(
            WriteFixture(tempRoot, "section-raw-header-overlap", rawHeaderOverlap));

        byte[] rawOffsetOverflow = CreateValidPe();
        WriteUInt32(rawOffsetOverflow, TextSectionHeaderOffset + 20, uint.MaxValue);
        ProcessResult rawOffsetOverflowResult = RunPeFixtureVerifier(
            WriteFixture(tempRoot, "section-raw-offset-overflow", rawOffsetOverflow));

        byte[] virtualOverflow = CreateValidPe();
        WriteUInt32(
            virtualOverflow,
            TextSectionHeaderOffset + 12,
            0xfffffff0);
        ProcessResult virtualOverflowResult = RunPeFixtureVerifier(
            WriteFixture(tempRoot, "section-virtual-overflow", virtualOverflow));

        byte[] sectionOutOfImage = CreateValidPe();
        WriteUInt32(
            sectionOutOfImage,
            XdataSectionHeaderOffset + 12,
            0x4000);
        ProcessResult sectionOutOfImageResult = RunPeFixtureVerifier(
            WriteFixture(tempRoot, "section-out-of-image", sectionOutOfImage));

        byte[] imageBaseOverflow = CreateValidPe();
        WriteUInt64(
            imageBaseOverflow,
            OptionalHeaderOffset + 24,
            ulong.MaxValue - 0x100);
        ProcessResult imageBaseOverflowResult = RunPeFixtureVerifier(
            WriteFixture(tempRoot, "image-base-overflow", imageBaseOverflow));

        byte[] rawOnlyEntrypoint = CreateValidPe();
        WriteUInt32(
            rawOnlyEntrypoint,
            OptionalHeaderOffset + 16,
            TextRva + 0x180);
        WriteUInt32(
            rawOnlyEntrypoint,
            TextSectionHeaderOffset + 8,
            0x100);
        ProcessResult rawOnlyEntrypointResult = RunPeFixtureVerifier(
            WriteFixture(tempRoot, "entrypoint-raw-only", rawOnlyEntrypoint));

        byte[] exceptionOutOfImage = CreateValidPe();
        WriteDataDirectory(exceptionOutOfImage, 3, 0x3ffc, 12);
        ProcessResult exceptionOutOfImageResult = RunPeFixtureVerifier(
            WriteFixture(tempRoot, "exception-directory-out-of-image", exceptionOutOfImage));

        return FailedWith(oversizedPeOffsetResult, "truncated_pe_header")
            && FailedWith(
                undersizedHeadersResult,
                "section_table_outside_headers")
            && FailedWith(
                rawHeaderOverlapResult,
                "section_raw_range_overlaps_headers")
            && FailedWith(
                rawOffsetOverflowResult,
                "section_raw_data_out_of_range")
            && FailedWith(
                virtualOverflowResult,
                "section_virtual_range_overflow")
            && FailedWith(
                sectionOutOfImageResult,
                "section_virtual_range_out_of_image")
            && FailedWith(
                imageBaseOverflowResult,
                "image_virtual_range_overflow")
            && FailedWith(
                rawOnlyEntrypointResult,
                "entrypoint_outside_sections")
            && FailedWith(
                exceptionOutOfImageResult,
                "exception_directory_out_of_image");
    }

    private static bool TestExceptionDirectoryFailures(string tempRoot)
    {
        byte[] missingEntry = CreateValidPe();
        WriteUInt32(missingEntry, OptionalHeaderOffset + 108, 3);
        ProcessResult missingResult = RunFixtureWithEvidence(
            tempRoot,
            "exception-directory-missing",
            missingEntry);

        byte[] zeroRva = CreateValidPe();
        WriteDataDirectory(zeroRva, 3, 0, 12);
        ProcessResult zeroResult = RunFixtureWithEvidence(
            tempRoot,
            "exception-directory-zero",
            zeroRva);

        byte[] zeroSize = CreateValidPe();
        WriteDataDirectory(zeroSize, 3, PdataRva, 0);
        ProcessResult zeroSizeResult = RunFixtureWithEvidence(
            tempRoot,
            "exception-directory-zero-size",
            zeroSize);

        byte[] misaligned = CreateValidPe();
        WriteDataDirectory(misaligned, 3, PdataRva + 2, 12);
        ProcessResult misalignedResult = RunFixtureWithEvidence(
            tempRoot,
            "exception-directory-misaligned",
            misaligned);

        byte[] nonDivisible = CreateValidPe();
        WriteDataDirectory(nonDivisible, 3, PdataRva, 13);
        ProcessResult nonDivisibleResult = RunFixtureWithEvidence(
            tempRoot,
            "exception-directory-nondivisible",
            nonDivisible);

        byte[] excessiveRecords = CreateValidPe();
        WriteDataDirectory(
            excessiveRecords,
            3,
            PdataRva,
            checked((uint)(MaximumRuntimeFunctionRecords + 1) * 12u));
        ProcessResult excessiveRecordsResult = RunFixtureWithEvidence(
            tempRoot,
            "exception-directory-record-capacity",
            excessiveRecords);

        byte[] crossSection = CreateValidPe();
        WriteDataDirectory(crossSection, 3, PdataRva + 0x1fc, 12);
        ProcessResult crossSectionResult = RunFixtureWithEvidence(
            tempRoot,
            "exception-directory-cross-section",
            crossSection);

        byte[] outsideVirtualPdata = CreateValidPe();
        WriteUInt32(outsideVirtualPdata, PdataSectionHeaderOffset + 8, 8);
        ProcessResult outsideVirtualPdataResult = RunFixtureWithEvidence(
            tempRoot,
            "exception-directory-raw-only",
            outsideVirtualPdata);

        byte[] virtualOverlap = CreateValidPe();
        WriteUInt32(
            virtualOverlap,
            XdataSectionHeaderOffset + 12,
            PdataRva + 0x100);
        ProcessResult virtualOverlapResult = RunFixtureWithEvidence(
            tempRoot,
            "section-virtual-overlap",
            virtualOverlap);

        byte[] rawOverlap = CreateValidPe();
        WriteUInt32(
            rawOverlap,
            XdataSectionHeaderOffset + 20,
            PdataRawOffset + 0x100);
        ProcessResult rawOverlapResult = RunFixtureWithEvidence(
            tempRoot,
            "section-raw-overlap",
            rawOverlap);

        byte[] writablePdata = CreateValidPe();
        WriteUInt32(writablePdata, PdataSectionHeaderOffset + 36, 0xc0000040);
        ProcessResult writableResult = RunFixtureWithEvidence(
            tempRoot,
            "exception-directory-writable",
            writablePdata);

        byte[] executablePdata = CreateValidPe();
        WriteUInt32(executablePdata, PdataSectionHeaderOffset + 36, 0x60000040);
        ProcessResult executableResult = RunFixtureWithEvidence(
            tempRoot,
            "exception-directory-executable",
            executablePdata);

        byte[] discardablePdata = CreateValidPe();
        WriteUInt32(discardablePdata, PdataSectionHeaderOffset + 36, 0x42000040);
        ProcessResult discardableResult = RunFixtureWithEvidence(
            tempRoot,
            "exception-directory-discardable",
            discardablePdata);

        return FailedWith(missingResult, "exception_directory_missing")
            && FailedWith(zeroResult, "exception_directory_zero")
            && FailedWith(zeroSizeResult, "exception_directory_zero")
            && FailedWith(misalignedResult, "exception_directory_rva_misaligned")
            && FailedWith(
                nonDivisibleResult,
                "exception_directory_size_not_record_aligned")
            && FailedWith(
                excessiveRecordsResult,
                "exception_directory_record_count_out_of_range")
            && FailedWith(crossSectionResult, "exception_directory_not_file_backed")
            && FailedWith(
                outsideVirtualPdataResult,
                "exception_directory_not_file_backed")
            && FailedWith(virtualOverlapResult, "section_virtual_ranges_overlap")
            && FailedWith(rawOverlapResult, "section_raw_ranges_overlap")
            && FailedWith(
                writableResult,
                "exception_directory_section_characteristics_invalid")
            && FailedWith(
                executableResult,
                "exception_directory_section_characteristics_invalid")
            && FailedWith(
                discardableResult,
                "exception_directory_section_characteristics_invalid");
    }

    private static bool TestRuntimeFunctionFailures(string tempRoot)
    {
        byte[] emptyRange = CreateValidPe();
        WriteRuntimeFunction(emptyRange, 0, SerializerRva, SerializerRva, XdataRva);
        ProcessResult emptyResult = RunFixtureWithEvidence(
            tempRoot,
            "runtime-empty-range",
            emptyRange);

        byte[] outOfImage = CreateValidPe();
        WriteRuntimeFunction(outOfImage, 0, SerializerRva, 0x5000, XdataRva);
        ProcessResult outOfImageResult = RunFixtureWithEvidence(
            tempRoot,
            "runtime-range-out-of-image",
            outOfImage);

        byte[] zeroUnwind = CreateValidPe();
        WriteRuntimeFunction(zeroUnwind, 0, SerializerRva, SerializerEndRva, 0);
        ProcessResult zeroUnwindResult = RunFixtureWithEvidence(
            tempRoot,
            "runtime-zero-unwind",
            zeroUnwind);

        byte[] misalignedUnwind = CreateValidPe();
        WriteRuntimeFunction(
            misalignedUnwind,
            0,
            SerializerRva,
            SerializerEndRva,
            XdataRva + 1);
        ProcessResult misalignedUnwindResult = RunFixtureWithEvidence(
            tempRoot,
            "runtime-misaligned-unwind",
            misalignedUnwind);

        byte[] outOfImageUnwind = CreateValidPe();
        WriteRuntimeFunction(
            outOfImageUnwind,
            0,
            SerializerRva,
            SerializerEndRva,
            0x5000);
        ProcessResult outOfImageUnwindResult = RunFixtureWithEvidence(
            tempRoot,
            "runtime-unwind-out-of-image",
            outOfImageUnwind);

        byte[] missingSerializer = CreateValidPe();
        WriteRuntimeFunction(missingSerializer, 0, 0x1200, 0x1210, XdataRva);
        ProcessResult missingSerializerResult = RunFixtureWithEvidence(
            tempRoot,
            "runtime-serializer-missing",
            missingSerializer);

        byte[] wrongEnd = CreateValidPe();
        WriteRuntimeFunction(wrongEnd, 0, SerializerRva, SerializerEndRva + 4, XdataRva);
        ProcessResult wrongEndResult = RunFixtureWithEvidence(
            tempRoot,
            "runtime-serializer-wrong-end",
            wrongEnd);

        byte[] duplicate = CreateValidPe();
        WriteDataDirectory(duplicate, 3, PdataRva, 24);
        WriteRuntimeFunction(
            duplicate,
            1,
            SerializerRva,
            SerializerEndRva,
            XdataRva);
        ProcessResult duplicateResult = RunFixtureWithEvidence(
            tempRoot,
            "runtime-duplicate",
            duplicate);

        byte[] unsorted = CreateValidPe();
        WriteDataDirectory(unsorted, 3, PdataRva, 24);
        WriteRuntimeFunction(unsorted, 0, 0x1200, 0x1210, XdataRva + 0x10);
        WriteRuntimeFunction(
            unsorted,
            1,
            SerializerRva,
            SerializerEndRva,
            XdataRva);
        WriteMinimalUnwind(unsorted, XdataRva + 0x10);
        ProcessResult unsortedResult = RunFixtureWithEvidence(
            tempRoot,
            "runtime-unsorted",
            unsorted);

        byte[] overlapping = CreateValidPe();
        WriteDataDirectory(overlapping, 3, PdataRva, 24);
        WriteRuntimeFunction(overlapping, 1, SerializerRva + 0x10, 0x1120, XdataRva + 0x10);
        WriteMinimalUnwind(overlapping, XdataRva + 0x10);
        ProcessResult overlapResult = RunFixtureWithEvidence(
            tempRoot,
            "runtime-overlap",
            overlapping);

        return FailedWith(emptyResult, "runtime_function_range_invalid")
            && FailedWith(outOfImageResult, "runtime_function_range_out_of_image")
            && FailedWith(zeroUnwindResult, "runtime_function_unwind_zero")
            && FailedWith(
                misalignedUnwindResult,
                "runtime_function_unwind_misaligned")
            && FailedWith(
                outOfImageUnwindResult,
                "runtime_function_unwind_out_of_image")
            && FailedWith(
                missingSerializerResult,
                "serializer_runtime_function_missing")
            && FailedWith(
                wrongEndResult,
                "serializer_runtime_function_range_mismatch")
            && FailedWith(duplicateResult, "runtime_function_duplicate")
            && FailedWith(
                unsortedResult,
                "runtime_function_not_strictly_sorted")
            && FailedWith(overlapResult, "runtime_function_overlap");
    }

    private static bool TestSerializerTextFailures(string tempRoot)
    {
        byte[] mutated = CreateValidPe();
        mutated[GetSerializerFileOffset(SerializerRva) + 6] ^= 0x01;
        ProcessResult mutationResult = RunFixtureWithEvidence(
            tempRoot,
            "serializer-byte-mutated",
            mutated);

        byte[] writable = CreateValidPe();
        WriteUInt32(writable, TextSectionHeaderOffset + 36, 0xe0000020);
        ProcessResult writableResult = RunFixtureWithEvidence(
            tempRoot,
            "serializer-text-writable",
            writable);

        byte[] nonExecutable = CreateValidPe();
        WriteUInt32(nonExecutable, TextSectionHeaderOffset + 36, 0x40000020);
        ProcessResult nonExecutableResult = RunFixtureWithEvidence(
            tempRoot,
            "serializer-text-nonexecutable",
            nonExecutable);

        byte[] discardable = CreateValidPe();
        WriteUInt32(discardable, TextSectionHeaderOffset + 36, 0x62000020);
        ProcessResult discardableResult = RunFixtureWithEvidence(
            tempRoot,
            "serializer-text-discardable",
            discardable);

        byte[] virtualOnly = CreateValidPe();
        WriteUInt32(virtualOnly, TextSectionHeaderOffset + 16, 0x110);
        ProcessResult virtualOnlyResult = RunFixtureWithEvidence(
            tempRoot,
            "serializer-text-virtual-only",
            virtualOnly);

        byte[] outsideVirtualText = CreateValidPe();
        WriteUInt32(outsideVirtualText, TextSectionHeaderOffset + 8, 0x110);
        ProcessResult outsideVirtualTextResult = RunFixtureWithEvidence(
            tempRoot,
            "serializer-text-raw-only",
            outsideVirtualText);

        uint misalignedRva = SerializerRva + 1;
        byte[] misaligned = CreateValidPe(misalignedRva);
        ProcessResult misalignedResult = RunFixtureWithEvidence(
            tempRoot,
            "serializer-text-misaligned",
            misaligned,
            new LinkEvidenceOptions { SerializerRva = misalignedRva });

        ProcessResult mapOutOfImageResult = RunFixtureWithEvidence(
            tempRoot,
            "serializer-map-rva-out-of-image",
            CreateValidPe(),
            new LinkEvidenceOptions { SerializerRva = 0x5000 });

        return FailedWith(mutationResult, "serializer_bytes_mismatch")
            && FailedWith(
                writableResult,
                "serializer_section_characteristics_invalid")
            && FailedWith(
                nonExecutableResult,
                "entrypoint_section_not_executable")
            && FailedWith(
                discardableResult,
                "serializer_section_characteristics_invalid")
            && FailedWith(virtualOnlyResult, "serializer_not_file_backed")
            && FailedWith(outsideVirtualTextResult, "serializer_not_file_backed")
            && FailedWith(misalignedResult, "serializer_rva_misaligned")
            && FailedWith(mapOutOfImageResult, "serializer_rva_out_of_image");
    }

    private static bool TestSerializerUnwindFailures(string tempRoot)
    {
        byte[] mutatedOpcode = CreateValidPe();
        mutatedOpcode[XdataRawOffset + 5] ^= 0x01;
        ProcessResult opcodeResult = RunFixtureWithEvidence(
            tempRoot,
            "serializer-unwind-opcode-mutated",
            mutatedOpcode);

        byte[] mutatedCount = CreateValidPe();
        mutatedCount[XdataRawOffset + 2] = 2;
        ProcessResult countResult = RunFixtureWithEvidence(
            tempRoot,
            "serializer-unwind-count-mutated",
            mutatedCount);

        byte[] wrongVersion = CreateValidPe();
        wrongVersion[XdataRawOffset] = 2;
        ProcessResult versionResult = RunFixtureWithEvidence(
            tempRoot,
            "serializer-unwind-version",
            wrongVersion);

        byte[] wrongPrologLength = CreateValidPe();
        wrongPrologLength[XdataRawOffset + 1] = 2;
        ProcessResult prologLengthResult = RunFixtureWithEvidence(
            tempRoot,
            "serializer-unwind-prolog-length",
            wrongPrologLength);

        byte[] wrongSavedRegister = CreateValidPe();
        wrongSavedRegister[XdataRawOffset + 5] = 0x40;
        ProcessResult savedRegisterResult = RunFixtureWithEvidence(
            tempRoot,
            "serializer-unwind-saved-register",
            wrongSavedRegister);

        byte[] writable = CreateValidPe();
        WriteUInt32(writable, XdataSectionHeaderOffset + 36, 0xc0000040);
        ProcessResult writableResult = RunFixtureWithEvidence(
            tempRoot,
            "serializer-unwind-writable",
            writable);

        byte[] executable = CreateValidPe();
        WriteUInt32(executable, XdataSectionHeaderOffset + 36, 0x60000040);
        ProcessResult executableResult = RunFixtureWithEvidence(
            tempRoot,
            "serializer-unwind-executable",
            executable);

        byte[] discardable = CreateValidPe();
        WriteUInt32(discardable, XdataSectionHeaderOffset + 36, 0x42000040);
        ProcessResult discardableResult = RunFixtureWithEvidence(
            tempRoot,
            "serializer-unwind-discardable",
            discardable);

        byte[] truncated = CreateValidPe();
        WriteUInt32(truncated, XdataSectionHeaderOffset + 16, 4);
        ProcessResult truncatedResult = RunFixtureWithEvidence(
            tempRoot,
            "serializer-unwind-truncated",
            truncated);

        byte[] outsideVirtualXdata = CreateValidPe();
        WriteUInt32(outsideVirtualXdata, XdataSectionHeaderOffset + 8, 4);
        ProcessResult outsideVirtualXdataResult = RunFixtureWithEvidence(
            tempRoot,
            "serializer-unwind-raw-only",
            outsideVirtualXdata);

        byte[] aliased = CreateValidPe();
        WriteDataDirectory(aliased, 3, PdataRva, 24);
        WriteRuntimeFunction(aliased, 1, 0x1200, 0x1210, XdataRva);
        ProcessResult aliasResult = RunFixtureWithEvidence(
            tempRoot,
            "serializer-unwind-aliased",
            aliased);

        return FailedWith(opcodeResult, "serializer_unwind_bytes_mismatch")
            && FailedWith(countResult, "serializer_unwind_bytes_mismatch")
            && FailedWith(versionResult, "runtime_function_unwind_header_invalid")
            && FailedWith(
                prologLengthResult,
                "serializer_unwind_bytes_mismatch")
            && FailedWith(
                savedRegisterResult,
                "serializer_unwind_bytes_mismatch")
            && FailedWith(
                writableResult,
                "runtime_function_unwind_section_characteristics_invalid")
            && FailedWith(
                executableResult,
                "runtime_function_unwind_section_characteristics_invalid")
            && FailedWith(
                discardableResult,
                "runtime_function_unwind_section_characteristics_invalid")
            && FailedWith(
                truncatedResult,
                "runtime_function_unwind_not_file_backed")
            && FailedWith(
                outsideVirtualXdataResult,
                "runtime_function_unwind_not_file_backed")
            && FailedWith(aliasResult, "serializer_unwind_aliased");
    }

    private static bool TestSerializerLinkAssociationFailures(string tempRoot)
    {
        ProcessResult missingSymbolResult = RunFixtureWithEvidence(
            tempRoot,
            "serializer-map-symbol-missing",
            CreateValidPe(),
            new LinkEvidenceOptions { SerializerSymbolCopies = 0 });

        ProcessResult duplicateSymbolResult = RunFixtureWithEvidence(
            tempRoot,
            "serializer-map-symbol-duplicate",
            CreateValidPe(),
            new LinkEvidenceOptions { SerializerSymbolCopies = 2 });

        ProcessResult wrongOwnerResult = RunFixtureWithEvidence(
            tempRoot,
            "serializer-map-owner-wrong",
            CreateValidPe(),
            new LinkEvidenceOptions { MapOwner = "fallback.obj" });

        ProcessResult missingIncludeResult = RunFixtureWithEvidence(
            tempRoot,
            "serializer-include-missing",
            CreateValidPe(),
            new LinkEvidenceOptions { ForceIncludeCopies = 0 });

        ProcessResult duplicateIncludeResult = RunFixtureWithEvidence(
            tempRoot,
            "serializer-include-duplicate",
            CreateValidPe(),
            new LinkEvidenceOptions { ForceIncludeCopies = 2 });

        ProcessResult missingObjectResult = RunFixtureWithEvidence(
            tempRoot,
            "serializer-response-object-missing",
            CreateValidPe(),
            new LinkEvidenceOptions { ObjectTokenCopies = 0 });

        ProcessResult duplicateObjectResult = RunFixtureWithEvidence(
            tempRoot,
            "serializer-response-object-duplicate",
            CreateValidPe(),
            new LinkEvidenceOptions { ObjectTokenCopies = 2 });

        ProcessResult missingMapResult = RunFixtureWithEvidence(
            tempRoot,
            "serializer-response-map-missing",
            CreateValidPe(),
            new LinkEvidenceOptions { MapOptionCopies = 0 });

        ProcessResult duplicateMapResult = RunFixtureWithEvidence(
            tempRoot,
            "serializer-response-map-duplicate",
            CreateValidPe(),
            new LinkEvidenceOptions { MapOptionCopies = 2 });

        ProcessResult wrongMapResult = RunFixtureWithEvidence(
            tempRoot,
            "serializer-response-map-wrong",
            CreateValidPe(),
            new LinkEvidenceOptions
            {
                MapPathOverride = Path.Combine(tempRoot, "wrong", "OS.link.map")
            });

        ProcessResult missingMapInfoResult = RunFixtureWithEvidence(
            tempRoot,
            "serializer-response-mapinfo-missing",
            CreateValidPe(),
            new LinkEvidenceOptions { MapInfoCopies = 0 });

        ProcessResult duplicateMapInfoResult = RunFixtureWithEvidence(
            tempRoot,
            "serializer-response-mapinfo-duplicate",
            CreateValidPe(),
            new LinkEvidenceOptions { MapInfoCopies = 2 });

        ProcessResult defaultLibraryResult = RunFixtureWithEvidence(
            tempRoot,
            "serializer-default-library",
            CreateValidPe(),
            new LinkEvidenceOptions { AdditionalResponse = "/DEFAULTLIB:kernel32.lib" });

        ProcessResult fallbackResult = RunFixtureWithEvidence(
            tempRoot,
            "serializer-fallback-binding",
            CreateValidPe(),
            new LinkEvidenceOptions
            {
                AdditionalResponse =
                    "/ALTERNATENAME:CLROS_X64_SerializeInstructionPublication=fallback"
            });

        ProcessResult mixedCaseDuplicateIncludeResult = RunFixtureWithEvidence(
            tempRoot,
            "serializer-mixed-case-duplicate-include",
            CreateValidPe(),
            new LinkEvidenceOptions
            {
                AdditionalResponse =
                    "/include:CLROS_X64_SerializeInstructionPublication"
            });

        ProcessResult mixedCaseConflictingEntryResult = RunFixtureWithEvidence(
            tempRoot,
            "serializer-mixed-case-conflicting-entry",
            CreateValidPe(),
            new LinkEvidenceOptions
            {
                AdditionalResponse = "/entry:WrongEntry"
            });

        ProcessResult nestedResponseResult = RunFixtureWithEvidence(
            tempRoot,
            "serializer-nested-response",
            CreateValidPe(),
            new LinkEvidenceOptions
            {
                AdditionalResponse = "@nested.rsp"
            });

        ProcessResult forcePolicyControlResult = RunFixtureWithEvidence(
            tempRoot,
            "serializer-force-policy-control",
            CreateValidPe(),
            new LinkEvidenceOptions
            {
                AdditionalResponse = "/NOLOGO"
            });

        ProcessResult bareForceResult = RunFixtureWithEvidence(
            tempRoot,
            "serializer-force-bare",
            CreateValidPe(),
            new LinkEvidenceOptions
            {
                AdditionalResponse = "/fOrCe"
            });

        ProcessResult forceUnresolvedResult = RunFixtureWithEvidence(
            tempRoot,
            "serializer-force-unresolved",
            CreateValidPe(),
            new LinkEvidenceOptions
            {
                AdditionalResponse = "/FoRcE:UnReSoLvEd"
            });

        ProcessResult forceMultipleResult = RunFixtureWithEvidence(
            tempRoot,
            "serializer-force-multiple",
            CreateValidPe(),
            new LinkEvidenceOptions
            {
                AdditionalResponse = "/FORCE:MULTIPLE"
            });

        ProcessResult dashForceResult = RunFixtureWithEvidence(
            tempRoot,
            "serializer-dash-force",
            CreateValidPe(),
            new LinkEvidenceOptions
            {
                AdditionalResponse = "-FORCE:UNRESOLVED"
            });

        ProcessResult dashDefaultLibraryResult = RunFixtureWithEvidence(
            tempRoot,
            "serializer-dash-default-library",
            CreateValidPe(),
            new LinkEvidenceOptions
            {
                AdditionalResponse = "-DEFAULTLIB:kernel32.lib"
            });

        ProcessResult dashAlternateNameResult = RunFixtureWithEvidence(
            tempRoot,
            "serializer-dash-alternatename",
            CreateValidPe(),
            new LinkEvidenceOptions
            {
                AdditionalResponse =
                    "-ALTERNATENAME:CLROS_X64_SerializeInstructionPublication=fallback"
            });

        ProcessResult mixedCaseBreproControlResult = RunFixtureWithEvidence(
            tempRoot,
            "serializer-brepro-mixed-case-control",
            CreateValidPe(),
            new LinkEvidenceOptions
            {
                BreproToken = "/bRePrO"
            });

        ProcessResult missingBreproResult = RunFixtureWithEvidence(
            tempRoot,
            "serializer-brepro-missing",
            CreateValidPe(),
            new LinkEvidenceOptions
            {
                BreproCopies = 0
            });

        ProcessResult duplicateBreproResult = RunFixtureWithEvidence(
            tempRoot,
            "serializer-brepro-duplicate",
            CreateValidPe(),
            new LinkEvidenceOptions
            {
                BreproCopies = 2
            });

        ProcessResult malformedBreproResult = RunFixtureWithEvidence(
            tempRoot,
            "serializer-brepro-malformed",
            CreateValidPe(),
            new LinkEvidenceOptions
            {
                BreproToken = "/BREPRO:INVALID"
            });

        return PassedVerifier(forcePolicyControlResult)
            && PassedVerifier(mixedCaseBreproControlResult)
            && FailedWith(bareForceResult, "force_linkage_forbidden")
            && FailedWith(forceUnresolvedResult, "force_linkage_forbidden")
            && FailedWith(forceMultipleResult, "force_linkage_forbidden")
            && FailedWith(
                dashForceResult,
                "noncanonical_link_option_prefix")
            && FailedWith(
                dashDefaultLibraryResult,
                "noncanonical_link_option_prefix")
            && FailedWith(
                dashAlternateNameResult,
                "noncanonical_link_option_prefix")
            && FailedWith(
                missingBreproResult,
                "brepro_link_response_contract_invalid")
            && FailedWith(
                duplicateBreproResult,
                "brepro_link_response_contract_invalid")
            && FailedWith(
                malformedBreproResult,
                "brepro_link_response_contract_invalid")
            && FailedWith(
                missingSymbolResult,
                "serializer_linker_map_contract_missing")
            && FailedWith(
                duplicateSymbolResult,
                "serializer_linker_map_contract_missing")
            && FailedWith(
                wrongOwnerResult,
                "serializer_linker_map_object_mismatch")
            && FailedWith(
                missingIncludeResult,
                "serializer_force_include_contract_missing")
            && FailedWith(
                duplicateIncludeResult,
                "serializer_force_include_contract_missing")
            && FailedWith(
                missingObjectResult,
                "serializer_object_link_response_contract_missing")
            && FailedWith(
                duplicateObjectResult,
                "serializer_object_link_response_contract_missing")
            && FailedWith(
                missingMapResult,
                "serializer_link_map_response_contract_missing")
            && FailedWith(
                duplicateMapResult,
                "serializer_link_map_response_contract_missing")
            && FailedWith(
                wrongMapResult,
                "serializer_link_map_path_mismatch")
            && FailedWith(
                missingMapInfoResult,
                "serializer_link_mapinfo_contract_missing")
            && FailedWith(
                duplicateMapInfoResult,
                "serializer_link_mapinfo_contract_missing")
            && FailedWith(defaultLibraryResult, "default_library_linkage_present")
            && FailedWith(fallbackResult, "serializer_fallback_linkage_present")
            && FailedWith(
                mixedCaseDuplicateIncludeResult,
                "serializer_force_include_contract_missing")
            && FailedWith(
                mixedCaseConflictingEntryResult,
                "efimain_link_response_contract_missing")
            && FailedWith(
                nestedResponseResult,
                "nested_link_response_file_forbidden");
    }

    private static bool TestLinkEvidenceFailures(string tempRoot)
    {
        string missingArtifact = WriteFixture(
            tempRoot,
            "missing-link-evidence",
            CreateValidPe());
        ProcessResult missingResult = RunVerifier(missingArtifact);

        string staleArtifact = WriteFixture(
            tempRoot,
            "stale-link-evidence",
            CreateValidPe());
        LinkEvidencePaths stalePaths = WriteLinkEvidence(staleArtifact);
        File.AppendAllText(stalePaths.MapPath, "\r\nstale", new UTF8Encoding(false));
        ProcessResult staleResult = RunVerifier(staleArtifact);

        ProcessResult entryMismatchResult = RunFixtureWithEvidence(
            tempRoot,
            "entrypoint-map-mismatch",
            CreateValidPe(),
            new LinkEvidenceOptions { MappedEntryRva = EntryPointRva + 0x100 });

        string schemaArtifact = WriteFixture(
            tempRoot,
            "schema-version-two",
            CreateValidPe());
        LinkEvidencePaths schemaPaths = WriteLinkEvidence(schemaArtifact);
        MutateEvidence(schemaPaths.EvidencePath, evidence => evidence["schemaVersion"] = 2);
        ProcessResult schemaResult = RunVerifier(schemaArtifact);

        string cookieArtifact = WriteFixture(
            tempRoot,
            "serializer-cookie-wrong",
            CreateValidPe());
        LinkEvidencePaths cookiePaths = WriteLinkEvidence(cookieArtifact);
        MutateEvidence(
            cookiePaths.EvidencePath,
            evidence => evidence["serializerExpectedSuccessCookie"] = 0);
        ProcessResult cookieResult = RunVerifier(cookieArtifact);

        string missingPropertyArtifact = WriteFixture(
            tempRoot,
            "serializer-property-missing",
            CreateValidPe());
        LinkEvidencePaths missingPropertyPaths = WriteLinkEvidence(missingPropertyArtifact);
        MutateEvidence(
            missingPropertyPaths.EvidencePath,
            evidence => evidence.Remove("serializerTargetsSha256"));
        ProcessResult missingPropertyResult = RunVerifier(missingPropertyArtifact);

        string sourceHashArtifact = WriteFixture(
            tempRoot,
            "serializer-source-hash",
            CreateValidPe());
        LinkEvidencePaths sourceHashPaths = WriteLinkEvidence(sourceHashArtifact);
        MutateEvidence(
            sourceHashPaths.EvidencePath,
            evidence => evidence["serializerGeneratorProgramSha256"] = new string('0', 64));
        ProcessResult sourceHashResult = RunVerifier(sourceHashArtifact);

        string closureInputArtifact = WriteFixture(
            tempRoot,
            "source-closure-input-mutated",
            CreateValidPe());
        LinkEvidencePaths closureInputPaths = WriteLinkEvidence(closureInputArtifact);
        MutateEvidence(
            closureInputPaths.EvidencePath,
            CorruptFirstSourceClosureInput);
        ProcessResult closureInputResult = RunVerifier(closureInputArtifact);

        string missingToolPropsArtifact = WriteFixture(
            tempRoot,
            "source-closure-tools-directory-build-props-missing",
            CreateValidPe());
        LinkEvidencePaths missingToolPropsPaths = WriteLinkEvidence(
            missingToolPropsArtifact);
        MutateEvidence(
            missingToolPropsPaths.EvidencePath,
            evidence => RemoveSourceClosureInput(
                evidence,
                ToolDirectoryBuildPropsPath));
        ProcessResult missingToolPropsResult = RunVerifier(
            missingToolPropsArtifact);

        string missingGlobalJsonArtifact = WriteFixture(
            tempRoot,
            "source-closure-global-json-missing",
            CreateValidPe());
        LinkEvidencePaths missingGlobalJsonPaths = WriteLinkEvidence(
            missingGlobalJsonArtifact);
        MutateEvidence(
            missingGlobalJsonPaths.EvidencePath,
            evidence => RemoveSourceClosureInput(evidence, "global.json"));
        ProcessResult missingGlobalJsonResult = RunVerifier(
            missingGlobalJsonArtifact);

        string missingVerifierSourceArtifact = WriteFixture(
            tempRoot,
            "source-closure-verifier-script-missing",
            CreateValidPe());
        LinkEvidencePaths missingVerifierSourcePaths = WriteLinkEvidence(
            missingVerifierSourceArtifact);
        MutateEvidence(
            missingVerifierSourcePaths.EvidencePath,
            evidence => RemoveSourceClosureInput(
                evidence,
                "scripts/test-efi-artifact.ps1"));
        ProcessResult missingVerifierSourceResult = RunVerifier(
            missingVerifierSourceArtifact);

        string solutionHashArtifact = WriteFixture(
            tempRoot,
            "solution-hash-wrong",
            CreateValidPe());
        LinkEvidencePaths solutionHashPaths = WriteLinkEvidence(solutionHashArtifact);
        MutateEvidence(
            solutionHashPaths.EvidencePath,
            evidence => evidence["solutionSha256"] = new string('0', 64));
        ProcessResult solutionHashResult = RunVerifier(solutionHashArtifact);

        string malformedNumberArtifact = WriteFixture(
            tempRoot,
            "serializer-object-size-malformed",
            CreateValidPe());
        LinkEvidencePaths malformedNumberPaths = WriteLinkEvidence(malformedNumberArtifact);
        MutateEvidence(
            malformedNumberPaths.EvidencePath,
            evidence => evidence["serializerObjectBytes"] = "not-a-number");
        ProcessResult malformedNumberResult = RunVerifier(malformedNumberArtifact);

        string mutatedObjectArtifact = WriteFixture(
            tempRoot,
            "serializer-object-mutated",
            CreateValidPe());
        LinkEvidencePaths mutatedObjectPaths = WriteLinkEvidence(mutatedObjectArtifact);
        byte[] mutatedObject = File.ReadAllBytes(mutatedObjectPaths.ObjectPath);
        mutatedObject[0] ^= 0x01;
        File.WriteAllBytes(mutatedObjectPaths.ObjectPath, mutatedObject);
        ProcessResult mutatedObjectResult = RunVerifier(mutatedObjectArtifact);

        string mutatedLinkedObjectArtifact = WriteFixture(
            tempRoot,
            "serializer-linked-object-mutated",
            CreateValidPe());
        LinkEvidencePaths mutatedLinkedObjectPaths = WriteLinkEvidence(
            mutatedLinkedObjectArtifact);
        byte[] mutatedLinkedObject = File.ReadAllBytes(
            mutatedLinkedObjectPaths.LinkedObjectPath);
        mutatedLinkedObject[0] ^= 0x01;
        File.WriteAllBytes(
            mutatedLinkedObjectPaths.LinkedObjectPath,
            mutatedLinkedObject);
        ProcessResult mutatedLinkedObjectResult = RunVerifier(
            mutatedLinkedObjectArtifact);

        string aliasedObjectArtifact = WriteFixture(
            tempRoot,
            "serializer-linked-preserved-alias",
            CreateValidPe());
        LinkEvidencePaths aliasedObjectPaths = WriteLinkEvidence(
            aliasedObjectArtifact);
        MutateEvidence(
            aliasedObjectPaths.EvidencePath,
            evidence =>
                evidence["serializerLinkedObjectPath"] = aliasedObjectPaths.ObjectPath);
        ProcessResult aliasedObjectResult = RunVerifier(aliasedObjectArtifact);

        string mutatedLinkedMapArtifact = WriteFixture(
            tempRoot,
            "serializer-linked-map-mutated",
            CreateValidPe());
        LinkEvidencePaths mutatedLinkedMapPaths = WriteLinkEvidence(
            mutatedLinkedMapArtifact);
        File.AppendAllText(
            mutatedLinkedMapPaths.LinkedMapPath,
            "\r\nmutated",
            new UTF8Encoding(false));
        ProcessResult mutatedLinkedMapResult = RunVerifier(
            mutatedLinkedMapArtifact);

        string explicitMismatchArtifact = WriteFixture(
            tempRoot,
            "serializer-object-explicit-mismatch",
            CreateValidPe());
        LinkEvidencePaths explicitMismatchPaths = WriteLinkEvidence(explicitMismatchArtifact);
        string alternateObject = Path.Combine(
            Path.GetDirectoryName(explicitMismatchArtifact)!,
            "alternate.obj");
        File.Copy(explicitMismatchPaths.ObjectPath, alternateObject);
        ProcessResult explicitMismatchResult = RunVerifier(
            explicitMismatchArtifact,
            serializerObjectPath: alternateObject);

        string nonSiblingArtifact = WriteFixture(
            tempRoot,
            "serializer-object-not-sibling",
            CreateValidPe());
        LinkEvidencePaths nonSiblingPaths = WriteLinkEvidence(nonSiblingArtifact);
        string nonSiblingObject = Path.Combine(
            Path.GetDirectoryName(nonSiblingArtifact)!,
            "not-the-artifact-sibling.obj");
        File.Copy(nonSiblingPaths.ObjectPath, nonSiblingObject);
        MutateEvidence(
            nonSiblingPaths.EvidencePath,
            evidence => evidence["serializerObjectPath"] = nonSiblingObject);
        ProcessResult nonSiblingResult = RunVerifier(nonSiblingArtifact);

        string missingDotnetPropertyArtifact = WriteFixture(
            tempRoot,
            "dotnet-host-property-missing",
            CreateValidPe());
        LinkEvidencePaths missingDotnetPropertyPaths = WriteLinkEvidence(
            missingDotnetPropertyArtifact);
        MutateEvidence(
            missingDotnetPropertyPaths.EvidencePath,
            evidence => evidence.Remove("dotnetHostPath"));
        ProcessResult missingDotnetPropertyResult = RunVerifier(
            missingDotnetPropertyArtifact);

        string dotnetHashArtifact = WriteFixture(
            tempRoot,
            "dotnet-host-hash-wrong",
            CreateValidPe());
        LinkEvidencePaths dotnetHashPaths = WriteLinkEvidence(dotnetHashArtifact);
        MutateEvidence(
            dotnetHashPaths.EvidencePath,
            evidence => evidence["dotnetHostSha256"] = new string('0', 64));
        ProcessResult dotnetHashResult = RunVerifier(dotnetHashArtifact);

        string windowsSdkHashArtifact = WriteFixture(
            tempRoot,
            "windows-sdk-hash-wrong",
            CreateValidPe());
        LinkEvidencePaths windowsSdkHashPaths = WriteLinkEvidence(
            windowsSdkHashArtifact);
        MutateEvidence(
            windowsSdkHashPaths.EvidencePath,
            evidence =>
                evidence["windowsSdkLibrarySha256"] = new string('0', 64));
        ProcessResult windowsSdkHashResult = RunVerifier(
            windowsSdkHashArtifact);

        string ilCompilerVersionArtifact = WriteFixture(
            tempRoot,
            "ilcompiler-version-wrong",
            CreateValidPe());
        LinkEvidencePaths ilCompilerVersionPaths = WriteLinkEvidence(
            ilCompilerVersionArtifact);
        MutateEvidence(
            ilCompilerVersionPaths.EvidencePath,
            evidence => evidence["ilCompilerFileVersion"] = "0.0.0.0");
        ProcessResult ilCompilerVersionResult = RunVerifier(
            ilCompilerVersionArtifact);

        string runtimeListHashArtifact = WriteFixture(
            tempRoot,
            "runtime-list-hash-wrong",
            CreateValidPe());
        LinkEvidencePaths runtimeListHashPaths = WriteLinkEvidence(
            runtimeListHashArtifact);
        MutateEvidence(
            runtimeListHashPaths.EvidencePath,
            evidence => evidence["runtimeListSha256"] = new string('0', 64));
        ProcessResult runtimeListHashResult = RunVerifier(
            runtimeListHashArtifact);

        string generatedUtcArtifact = WriteFixture(
            tempRoot,
            "generated-utc-malformed",
            CreateValidPe());
        LinkEvidencePaths generatedUtcPaths = WriteLinkEvidence(
            generatedUtcArtifact);
        MutateEvidence(
            generatedUtcPaths.EvidencePath,
            evidence => evidence["generatedUtc"] = "not-a-timestamp");
        ProcessResult generatedUtcResult = RunVerifier(generatedUtcArtifact);

        string configurationArtifact = WriteFixture(
            tempRoot,
            "configuration-path-traversal",
            CreateValidPe());
        LinkEvidencePaths configurationPaths = WriteLinkEvidence(
            configurationArtifact);
        MutateEvidence(
            configurationPaths.EvidencePath,
            evidence => evidence["configuration"] = "..\\outside");
        ProcessResult configurationResult = RunVerifier(configurationArtifact);

        string nonCanonicalManagedArtifact = WriteFixture(
            tempRoot,
            "managed-assembly-noncanonical",
            CreateValidPe());
        LinkEvidencePaths nonCanonicalManagedPaths = WriteLinkEvidence(
            nonCanonicalManagedArtifact);
        string alternateManagedAssembly = Path.Combine(
            Path.GetDirectoryName(nonCanonicalManagedArtifact)!,
            "alternate-managed.dll");
        File.Copy(
            nonCanonicalManagedPaths.ManagedAssemblyPath,
            alternateManagedAssembly,
            overwrite: true);
        MutateEvidence(
            nonCanonicalManagedPaths.EvidencePath,
            evidence =>
            {
                evidence["managedAssemblyPath"] = alternateManagedAssembly;
                evidence["managedAssemblySha256"] =
                    GetSha256(alternateManagedAssembly);
            });
        ProcessResult nonCanonicalManagedResult = RunVerifier(
            nonCanonicalManagedArtifact);

        string malformedManagedArtifact = WriteFixture(
            tempRoot,
            "managed-assembly-malformed",
            CreateValidPe());
        LinkEvidencePaths malformedManagedPaths = WriteLinkEvidence(
            malformedManagedArtifact);
        byte[] originalManagedAssembly = File.ReadAllBytes(
            malformedManagedPaths.ManagedAssemblyPath);
        ProcessResult malformedManagedResult;
        try
        {
            File.WriteAllBytes(
                malformedManagedPaths.ManagedAssemblyPath,
                Encoding.ASCII.GetBytes("not a managed PE"));
            MutateEvidence(
                malformedManagedPaths.EvidencePath,
                evidence => evidence["managedAssemblySha256"] =
                    GetSha256(malformedManagedPaths.ManagedAssemblyPath));
            malformedManagedResult = RunVerifier(malformedManagedArtifact);
        }
        finally
        {
            File.WriteAllBytes(
                malformedManagedPaths.ManagedAssemblyPath,
                originalManagedAssembly);
        }

        string missingRestorePropertyArtifact = WriteFixture(
            tempRoot,
            "restore-closure-property-missing",
            CreateValidPe());
        LinkEvidencePaths missingRestorePropertyPaths = WriteLinkEvidence(
            missingRestorePropertyArtifact);
        MutateEvidence(
            missingRestorePropertyPaths.EvidencePath,
            evidence => evidence.Remove("restoreClosureSha256"));
        ProcessResult missingRestorePropertyResult = RunVerifier(
            missingRestorePropertyArtifact);

        string restoreInputHashArtifact = WriteFixture(
            tempRoot,
            "restore-closure-input-hash-wrong",
            CreateValidPe());
        LinkEvidencePaths restoreInputHashPaths = WriteLinkEvidence(
            restoreInputHashArtifact);
        MutateEvidence(
            restoreInputHashPaths.EvidencePath,
            CorruptFirstRestoreClosureInput);
        ProcessResult restoreInputHashResult = RunVerifier(
            restoreInputHashArtifact);

        string projectAssetsPathArtifact = WriteFixture(
            tempRoot,
            "project-assets-path-noncanonical",
            CreateValidPe());
        LinkEvidencePaths projectAssetsPathPaths = WriteLinkEvidence(
            projectAssetsPathArtifact);
        string alternateProjectAssetsPath = Path.Combine(
            Path.GetDirectoryName(projectAssetsPathArtifact)!,
            "alternate-project.assets.json");
        File.WriteAllText(
            alternateProjectAssetsPath,
            "{\"fixture\":\"alternate\"}",
            new UTF8Encoding(false));
        MutateEvidence(
            projectAssetsPathPaths.EvidencePath,
            evidence =>
            {
                evidence["projectAssetsPath"] = alternateProjectAssetsPath;
                evidence["projectAssetsSha256"] =
                    GetSha256(alternateProjectAssetsPath);
            });
        ProcessResult projectAssetsPathResult = RunVerifier(
            projectAssetsPathArtifact);

        string restoreExtraEntryArtifact = WriteFixture(
            tempRoot,
            "restore-closure-extra-entry",
            CreateValidPe());
        LinkEvidencePaths restoreExtraEntryPaths = WriteLinkEvidence(
            restoreExtraEntryArtifact);
        SchemaThreeFixture restoreExtraFixture = cachedSchemaThreeFixture
            ?? throw new InvalidOperationException(
                "Schema-three fixture was not initialized.");
        string restoreExtraEntryPath = Path.Combine(
            restoreExtraFixture.ProjectExtensionsPath,
            "injected.targets");
        File.WriteAllText(
            restoreExtraEntryPath,
            "<Project />",
            new UTF8Encoding(false));
        ProcessResult restoreExtraEntryResult;
        try
        {
            restoreExtraEntryResult = RunVerifier(restoreExtraEntryArtifact);
        }
        finally
        {
            File.Delete(restoreExtraEntryPath);
        }

        string missingAssemblyArtifact = WriteFixture(
            tempRoot,
            "managed-assembly-missing",
            CreateValidPe());
        LinkEvidencePaths missingAssemblyPaths = WriteLinkEvidence(missingAssemblyArtifact);
        File.Delete(missingAssemblyPaths.ManagedAssemblyPath);
        ProcessResult missingAssemblyResult = RunVerifier(missingAssemblyArtifact);

        return FailedWith(missingResult, "missing_linker_map")
            && FailedWith(staleResult, "link_evidence_hash_mismatch")
            && FailedWith(entryMismatchResult, "efimain_entrypoint_mismatch")
            && FailedWith(schemaResult, "link_evidence_contract_invalid")
            && FailedWith(cookieResult, "link_evidence_contract_invalid")
            && FailedWith(
                missingPropertyResult,
                "link_evidence_property_missing")
            && missingPropertyResult.Output.Contains(
                "property=serializerTargetsSha256",
                StringComparison.Ordinal)
            && FailedWith(sourceHashResult, "serializer_source_hash_mismatch")
            && FailedWith(
                closureInputResult,
                "source_closure_input_hash_mismatch")
            && FailedWith(
                missingToolPropsResult,
                "source_closure_input_count_mismatch")
            && FailedWith(
                missingGlobalJsonResult,
                "source_closure_input_count_mismatch")
            && FailedWith(
                missingVerifierSourceResult,
                "source_closure_input_count_mismatch")
            && FailedWith(solutionHashResult, "link_evidence_hash_mismatch")
            && FailedWith(
                malformedNumberResult,
                "link_evidence_number_malformed")
            && FailedWith(
                mutatedObjectResult,
                "serializer_object_identity_mismatch")
            && FailedWith(
                mutatedLinkedObjectResult,
                "serializer_linked_object_identity_mismatch")
            && FailedWith(
                aliasedObjectResult,
                "serializer_linked_and_preserved_object_paths_alias")
            && FailedWith(
                mutatedLinkedMapResult,
                "serializer_link_map_identity_mismatch")
            && FailedWith(
                explicitMismatchResult,
                "serializer_object_path_mismatch")
            && FailedWith(
                nonSiblingResult,
                "serializer_object_not_artifact_sibling")
            && FailedWith(
                missingDotnetPropertyResult,
                "link_evidence_property_missing")
            && FailedWith(
                dotnetHashResult,
                "dotnet_host_identity_mismatch")
            && FailedWith(
                windowsSdkHashResult,
                "windows_sdk_identity_mismatch")
            && FailedWith(
                ilCompilerVersionResult,
                "ilcompiler_version_mismatch")
            && FailedWith(
                runtimeListHashResult,
                "nativeaot_runtime_list_identity_mismatch")
            && FailedWith(generatedUtcResult, "generated_utc_invalid")
            && FailedWith(configurationResult, "configuration_invalid")
            && FailedWith(
                nonCanonicalManagedResult,
                "managed_assembly_path_not_canonical")
            && FailedWith(
                malformedManagedResult,
                "managed_pe_dos_header_truncated")
            && FailedWith(
                missingRestorePropertyResult,
                "link_evidence_property_missing")
            && missingRestorePropertyResult.Output.Contains(
                "property=restoreClosureSha256",
                StringComparison.Ordinal)
            && FailedWith(
                restoreInputHashResult,
                "restore_closure_input_hash_mismatch")
            && FailedWith(
                projectAssetsPathResult,
                "project_assets_path_not_canonical")
            && FailedWith(
                restoreExtraEntryResult,
                "restore_closure_entry_count_mismatch")
            && FailedWith(missingAssemblyResult, "managed_assembly_missing");
    }

    private static bool TestSchemaThreeAuthorityFailures(string tempRoot)
    {
        bool passed = true;
        passed &= EvidenceMutationFails(
            tempRoot,
            "schema-three-property-null",
            evidence => evidence["packageRootPath"] = null,
            "package_root_path_invalid");
        passed &= EvidenceMutationFails(
            tempRoot,
            "schema-three-property-empty",
            evidence => evidence["restoreArchiveSourceRootPath"] = "",
            "restore_archive_source_root_path_invalid");
        passed &= EvidenceMutationFails(
            tempRoot,
            "schema-three-property-extra",
            evidence => evidence["unexpectedAuthority"] = "forbidden",
            "link_evidence_property_set_invalid");
        passed &= EvidenceMutationFails(
            tempRoot,
            "restore-archive-count",
            evidence => GetEvidenceArray(
                evidence,
                "restoreArchiveInputs").RemoveAt(0),
            "restore_archive_input_count_mismatch");
        passed &= EvidenceMutationFails(
            tempRoot,
            "restore-archive-order",
            evidence => SwapArrayEntries(
                GetEvidenceArray(evidence, "restoreArchiveInputs"),
                0,
                1),
            "restore_archive_input_set_mismatch");
        passed &= EvidenceMutationFails(
            tempRoot,
            "restore-archive-case",
            evidence =>
            {
                JsonObject first = GetEvidenceArray(
                    evidence,
                    "restoreArchiveInputs")[0]!.AsObject();
                first["path"] = first["path"]!
                    .GetValue<string>()
                    .ToUpperInvariant();
            },
            "restore_archive_input_set_mismatch");
        passed &= EvidenceMutationFails(
            tempRoot,
            "restore-archive-duplicate",
            evidence =>
            {
                JsonArray inputs = GetEvidenceArray(
                    evidence,
                    "restoreArchiveInputs");
                inputs[1] = inputs[0]!.DeepClone();
            },
            "restore_archive_input_set_mismatch");
        passed &= EvidenceMutationFails(
            tempRoot,
            "restore-archive-hash",
            evidence =>
            {
                JsonObject first = GetEvidenceArray(
                    evidence,
                    "restoreArchiveInputs")[0]!.AsObject();
                first["sha256"] = new string('0', 64);
            },
            "restore_archive_input_hash_mismatch");
        passed &= EvidenceMutationFails(
            tempRoot,
            "pinned-restore-source-count",
            evidence => GetEvidenceArray(
                evidence,
                "pinnedRestoreSourceInputs").RemoveAt(0),
            "pinned_restore_source_input_count_mismatch");
        passed &= EvidenceMutationFails(
            tempRoot,
            "pinned-restore-source-order",
            evidence => SwapArrayEntries(
                GetEvidenceArray(evidence, "pinnedRestoreSourceInputs"),
                0,
                1),
            "pinned_restore_source_input_set_mismatch");
        passed &= EvidenceMutationFails(
            tempRoot,
            "pinned-restore-source-path",
            evidence =>
            {
                JsonObject first = GetEvidenceArray(
                    evidence,
                    "pinnedRestoreSourceInputs")[0]!.AsObject();
                first["path"] = first["path"]!
                    .GetValue<string>()
                    .ToUpperInvariant();
            },
            "pinned_restore_source_input_set_mismatch");
        passed &= EvidenceMutationFails(
            tempRoot,
            "pinned-restore-source-hash",
            evidence =>
            {
                JsonObject first = GetEvidenceArray(
                    evidence,
                    "pinnedRestoreSourceInputs")[0]!.AsObject();
                first["sha256"] = new string('0', 64);
            },
            "pinned_restore_source_input_hash_mismatch");
        passed &= EvidenceMutationFails(
            tempRoot,
            "generated-restore-order",
            evidence => SwapArrayEntries(
                GetEvidenceArray(evidence, "restoreClosureInputs"),
                0,
                1),
            "restore_closure_input_set_mismatch");
        passed &= EvidenceMutationFails(
            tempRoot,
            "restore-closure-path-traversal",
            evidence =>
            {
                JsonObject first = GetEvidenceArray(
                    evidence,
                    "restoreClosureInputs")[0]!.AsObject();
                first["path"] = "../outside";
            },
            "restore_closure_input_set_mismatch");
        passed &= TestPinnedRestoreArchiveMutation(tempRoot);
        passed &= TestGeneratedRestoreExecutableSurfaceMutation(tempRoot);
        passed &= EvidenceMutationFails(
            tempRoot,
            "package-count",
            evidence => GetEvidenceArray(evidence, "packages").RemoveAt(0),
            "package_evidence_count_mismatch");
        passed &= EvidenceMutationFails(
            tempRoot,
            "package-order",
            evidence => SwapArrayEntries(
                GetEvidenceArray(evidence, "packages"),
                0,
                1),
            "package_evidence_identity_mismatch");
        passed &= EvidenceMutationFails(
            tempRoot,
            "package-duplicate",
            evidence =>
            {
                JsonArray packages = GetEvidenceArray(evidence, "packages");
                packages[1] = packages[0]!.DeepClone();
            },
            "package_evidence_identity_mismatch");
        passed &= EvidenceMutationFails(
            tempRoot,
            "package-id-case",
            evidence =>
            {
                JsonObject first = GetEvidenceArray(
                    evidence,
                    "packages")[0]!.AsObject();
                first["id"] = first["id"]!
                    .GetValue<string>()
                    .ToUpperInvariant();
            },
            "package_evidence_identity_mismatch");
        passed &= EvidenceMutationFails(
            tempRoot,
            "package-root",
            evidence =>
            {
                JsonObject first = GetEvidenceArray(
                    evidence,
                    "packages")[0]!.AsObject();
                first["rootPath"] = Path.GetTempPath();
            },
            "package_evidence_root_path_mismatch");
        passed &= EvidenceMutationFails(
            tempRoot,
            "package-version",
            evidence =>
            {
                JsonObject first = GetEvidenceArray(
                    evidence,
                    "packages")[0]!.AsObject();
                first["version"] = "10.0.999";
            },
            "package_evidence_identity_mismatch");
        passed &= EvidenceMutationFails(
            tempRoot,
            "package-tree-hash",
            evidence =>
            {
                JsonObject first = GetEvidenceArray(
                    evidence,
                    "packages")[0]!.AsObject();
                first["aggregateSha256"] = new string('0', 64);
            },
            "package_tree_contract_mismatch");
        passed &= EvidenceMutationFails(
            tempRoot,
            "package-metadata-hash",
            evidence =>
            {
                JsonObject first = GetEvidenceArray(
                    evidence,
                    "packages")[0]!.AsObject();
                first["metadataSha256"] = new string('0', 64);
            },
            "package_metadata_hash_mismatch");
        passed &= TestPackageMetadataMutation(tempRoot);
        passed &= TestPackageFileMutation(tempRoot);
        passed &= TestExtraPackageVersion(tempRoot);
        passed &= EvidenceMutationFails(
            tempRoot,
            "package-build-input",
            evidence =>
            {
                JsonObject first = GetEvidenceArray(
                    evidence,
                    "packageBuildClosureInputs")[0]!.AsObject();
                first["sha256"] = new string('0', 64);
            },
            "package_build_closure_input_hash_mismatch");
        passed &= EvidenceMutationFails(
            tempRoot,
            "active-package-import-graph",
            evidence =>
            {
                JsonArray edges = GetEvidenceArray(
                    evidence,
                    "activePackageImportEdges");
                edges[0] = "sdk|outside.targets";
            },
            "active_package_import_edges_mismatch");
        passed &= EvidenceMutationFails(
            tempRoot,
            "package-task-binding",
            evidence =>
            {
                JsonArray bindings = GetEvidenceArray(
                    evidence,
                    "packageTaskBindings");
                bindings[0] = "ComputeManagedAssemblies|outside.dll";
            },
            "package_task_bindings_mismatch");
        passed &= EvidenceMutationFails(
            tempRoot,
            "post-restore-evaluation",
            evidence =>
            {
                JsonObject evaluation = evidence[
                    "postRestoreEvaluation"]!.AsObject();
                evaluation["compileCount"] = 43;
            },
            "post_restore_evidence_count_mismatch");
        passed &= EvidenceMutationFails(
            tempRoot,
            "post-restore-m02-owned-com1-property-missing",
            evidence =>
            {
                JsonObject properties = evidence[
                    "postRestoreEvaluation"]!["properties"]!.AsObject();
                properties.Remove("EnableM02OwnedCom1ReadyProbe");
            },
            "post_restore_evidence_property_set_invalid");
        passed &= EvidenceMutationFails(
            tempRoot,
            "post-restore-m02-owned-com1-property-enabled-without-switch",
            evidence =>
            {
                JsonObject properties = evidence[
                    "postRestoreEvaluation"]!["properties"]!.AsObject();
                properties["EnableM02OwnedCom1ReadyProbe"] = "true";
            },
            "post_restore_evidence_property_mismatch");
        passed &= EvidenceMutationFails(
            tempRoot,
            "post-restore-m02-panic-safe-owned-com1-property-missing",
            evidence =>
            {
                JsonObject properties = evidence[
                    "postRestoreEvaluation"]!["properties"]!.AsObject();
                properties.Remove("EnableM02PanicSafeOwnedCom1Probe");
            },
            "post_restore_evidence_property_set_invalid");
        passed &= EvidenceMutationFails(
            tempRoot,
            "post-restore-m02-panic-safe-owned-com1-property-enabled-without-switch",
            evidence =>
            {
                JsonObject properties = evidence[
                    "postRestoreEvaluation"]!["properties"]!.AsObject();
                properties["EnableM02PanicSafeOwnedCom1Probe"] = "true";
            },
            "post_restore_evidence_property_mismatch");
        passed &= EvidenceMutationFails(
            tempRoot,
            "post-restore-m02-owned-x64-gdt-property-missing",
            evidence =>
            {
                JsonObject properties = evidence[
                    "postRestoreEvaluation"]!["properties"]!.AsObject();
                properties.Remove("EnableM02OwnedX64GdtEncodingProbe");
            },
            "post_restore_evidence_property_set_invalid");
        passed &= EvidenceMutationFails(
            tempRoot,
            "post-restore-m02-owned-x64-gdt-property-enabled-without-switch",
            evidence =>
            {
                JsonObject properties = evidence[
                    "postRestoreEvaluation"]!["properties"]!.AsObject();
                properties["EnableM02OwnedX64GdtEncodingProbe"] = "true";
            },
            "post_restore_evidence_property_mismatch");
        passed &= EvidenceMutationFails(
            tempRoot,
            "post-restore-m02-idtr-continued-execution-property-missing",
            evidence =>
            {
                JsonObject properties = evidence[
                    "postRestoreEvaluation"]!["properties"]!.AsObject();
                properties.Remove("EnableM02IdtrContinuedExecutionProbe");
            },
            "post_restore_evidence_property_set_invalid");
        passed &= EvidenceMutationFails(
            tempRoot,
            "post-restore-m02-idtr-continued-execution-property-enabled-without-switch",
            evidence =>
            {
                JsonObject properties = evidence[
                    "postRestoreEvaluation"]!["properties"]!.AsObject();
                properties["EnableM02IdtrContinuedExecutionProbe"] = "true";
            },
            "post_restore_evidence_property_mismatch");
        passed &= EvidenceMutationFails(
            tempRoot,
            "post-restore-m02-ud2-vector-state-property-missing",
            evidence =>
            {
                JsonObject properties = evidence[
                    "postRestoreEvaluation"]!["properties"]!.AsObject();
                properties.Remove("EnableM02Ud2VectorStateProbe");
            },
            "post_restore_evidence_property_set_invalid");
        passed &= EvidenceMutationFails(
            tempRoot,
            "post-restore-m02-ud2-vector-state-property-enabled-without-switch",
            evidence =>
            {
                JsonObject properties = evidence[
                    "postRestoreEvaluation"]!["properties"]!.AsObject();
                properties["EnableM02Ud2VectorStateProbe"] = "true";
            },
            "post_restore_evidence_property_mismatch");
        passed &= EvidenceMutationFails(
            tempRoot,
            "post-restore-m04-property-missing",
            evidence =>
            {
                JsonObject properties = evidence[
                    "postRestoreEvaluation"]!["properties"]!.AsObject();
                properties.Remove("EnableM04IndependentEndianConformanceProbe");
            },
            "post_restore_evidence_property_set_invalid");
        passed &= EvidenceMutationFails(
            tempRoot,
            "post-restore-m04-property-enabled-without-switch",
            evidence =>
            {
                JsonObject properties = evidence[
                    "postRestoreEvaluation"]!["properties"]!.AsObject();
                properties["EnableM04IndependentEndianConformanceProbe"] = "true";
            },
            "post_restore_evidence_property_mismatch");
        passed &= EvidenceMutationFails(
            tempRoot,
            "post-restore-m04-span-memory-property-missing",
            evidence =>
            {
                JsonObject properties = evidence[
                    "postRestoreEvaluation"]!["properties"]!.AsObject();
                properties.Remove(
                    "EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe");
            },
            "post_restore_evidence_property_set_invalid");
        passed &= EvidenceMutationFails(
            tempRoot,
            "post-restore-m04-span-memory-property-enabled-without-switch",
            evidence =>
            {
                JsonObject properties = evidence[
                    "postRestoreEvaluation"]!["properties"]!.AsObject();
                properties[
                    "EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe"] =
                    "true";
            },
            "post_restore_evidence_property_mismatch");
        passed &= EvidenceMutationFails(
            tempRoot,
            "post-restore-sdk-path",
            evidence =>
            {
                JsonObject properties = evidence[
                    "postRestoreEvaluation"]!["properties"]!.AsObject();
                properties["MSBuildSDKsPath"] = Path.GetTempPath();
            },
            "post_restore_evidence_property_mismatch");
        passed &= EvidenceMutationFails(
            tempRoot,
            "post-restore-wildcard-enabled",
            evidence =>
            {
                JsonObject properties = evidence[
                    "postRestoreEvaluation"]!["properties"]!.AsObject();
                properties[
                    "ImportByWildcardBeforeMicrosoftCommonProps"] = "true";
            },
            "post_restore_evidence_property_mismatch");
        passed &= EvidenceMutationFails(
            tempRoot,
            "post-restore-shared-compilation-enabled",
            evidence =>
            {
                JsonObject properties = evidence[
                    "postRestoreEvaluation"]!["properties"]!.AsObject();
                properties["UseSharedCompilation"] = "true";
            },
            "post_restore_evidence_property_mismatch");
        passed &= TestPresentUserExtensionsDirectory(tempRoot);
        passed &= EvidenceMutationFails(
            tempRoot,
            "canonical-isolated-ilc",
            evidence =>
            {
                SchemaThreeFixture fixture = cachedSchemaThreeFixture
                    ?? throw new InvalidOperationException(
                        "Schema-three fixture was not initialized.");
                evidence["ilCompilerPath"] = fixture.IlcBuildTasksPath;
            },
            "ilcompiler_identity_mismatch");
        passed &= EvidenceMutationFails(
            tempRoot,
            "canonical-runtime-path",
            evidence =>
            {
                SchemaThreeFixture fixture = cachedSchemaThreeFixture
                    ?? throw new InvalidOperationException(
                        "Schema-three fixture was not initialized.");
                evidence["nativeAotRuntimePackPath"] = Path.Combine(
                    fixture.PackageRootPath,
                    "microsoft.netcore.app.runtime.nativeaot.win-x64",
                    "10.0.10");
            },
            "nativeaot_runtime_pack_identity_mismatch");
        passed &= EvidenceMutationFails(
            tempRoot,
            "external-package-root",
            evidence => evidence["packageRootPath"] = Path.GetTempPath(),
            "package_root_contract_invalid");
        passed &= EvidenceMutationFails(
            tempRoot,
            "configuration-dot",
            evidence => evidence["configuration"] = ".",
            "configuration_invalid");
        passed &= EvidenceMutationFails(
            tempRoot,
            "configuration-dot-dot",
            evidence => evidence["configuration"] = "..",
            "configuration_invalid");
        passed &= TestAuthorityOutputHardlink(tempRoot);
        passed &= TryTestAuthorityArtifactReparsePoint(tempRoot);
        passed &= TryTestAuthorityMapReparsePoint(tempRoot);
        passed &= TryTestAuthorityResponseReparsePoint(tempRoot);
        passed &= TryTestAuthorityEvidenceReparsePoint(tempRoot);
        passed &= TryTestSourceClosureReparsePoint(tempRoot);
        passed &= TryTestPackageReparsePoint(tempRoot);
        return passed;
    }

    private static bool TestPinnedRepositoryContractPaths(string tempRoot)
    {
        string artifact = WriteFixture(
            tempRoot,
            "pinned-project-entry-authority",
            CreateValidPe());
        WriteLinkEvidence(artifact);
        SchemaThreeFixture fixture = cachedSchemaThreeFixture
            ?? throw new InvalidOperationException(
                "Schema-three fixture was not initialized.");
        string canonicalProjectPath = Path.Combine(
            fixture.RepositoryRoot,
            "src",
            "OS",
            "OS.csproj");
        string canonicalEntryPath = Path.Combine(
            fixture.RepositoryRoot,
            "src",
            "OS",
            "Boot",
            "EfiEntry.cs");
        string alternateRoot = Path.Combine(
            fixture.RepositoryRoot,
            "src",
            "OS",
            "alternate-authority");
        Directory.CreateDirectory(alternateRoot);
        string alternateProjectPath = Path.Combine(
            alternateRoot,
            "OS.csproj");
        string alternateEntryPath = Path.Combine(
            alternateRoot,
            "EfiEntry.cs");
        File.Copy(
            canonicalProjectPath,
            alternateProjectPath,
            overwrite: true);
        File.Copy(
            canonicalEntryPath,
            alternateEntryPath,
            overwrite: true);

        ProcessResult alternateProjectResult;
        ProcessResult alternateEntryResult;
        ProcessResult traversedAbsoluteProjectResult;
        ProcessResult traversedRelativeProjectResult;
        ProcessResult traversedAbsoluteEntryResult;
        ProcessResult traversedRelativeEntryResult;
        try
        {
            alternateProjectResult = RunVerifier(
                artifact,
                projectPath: alternateProjectPath);
            alternateEntryResult = RunVerifier(
                artifact,
                entrySourcePath: alternateEntryPath);
            traversedAbsoluteProjectResult = RunVerifier(
                artifact,
                projectPath: Path.Combine(
                    fixture.RepositoryRoot,
                    "src",
                    "OS",
                    "..",
                    "OS",
                    "OS.csproj"));
            traversedRelativeProjectResult = RunVerifier(
                artifact,
                projectPath: "src/OS/../OS/OS.csproj");
            traversedAbsoluteEntryResult = RunVerifier(
                artifact,
                entrySourcePath: Path.Combine(
                    fixture.RepositoryRoot,
                    "src",
                    "OS",
                    "Boot",
                    "..",
                    "Boot",
                    "EfiEntry.cs"));
            traversedRelativeEntryResult = RunVerifier(
                artifact,
                entrySourcePath: "src/OS/Boot/../Boot/EfiEntry.cs");
        }
        finally
        {
            Directory.Delete(alternateRoot, recursive: true);
        }

        return FailedWith(
                alternateProjectResult,
                "project_path_not_canonical_repository_contract")
            && FailedWith(
                alternateEntryResult,
                "entry_source_path_not_canonical_repository_contract")
            && FailedWith(
                traversedAbsoluteProjectResult,
                "project_path_not_canonical_repository_contract")
            && FailedWith(
                traversedRelativeProjectResult,
                "project_path_not_canonical_repository_contract")
            && FailedWith(
                traversedAbsoluteEntryResult,
                "entry_source_path_not_canonical_repository_contract")
            && FailedWith(
                traversedRelativeEntryResult,
                "entry_source_path_not_canonical_repository_contract")
            && TryTestPinnedProjectHardlink(
                artifact,
                fixture,
                canonicalProjectPath);
    }

    private static bool TryTestPinnedProjectHardlink(
        string artifact,
        SchemaThreeFixture fixture,
        string canonicalProjectPath)
    {
        string aliasPath = Path.Combine(
            fixture.RepositoryRoot,
            "src",
            "OS",
            "project-authority-alias.csproj");
        if (!CreateHardLink(aliasPath, canonicalProjectPath, IntPtr.Zero))
        {
            Console.WriteLine(
                "EFI_ARTIFACT_PROJECT_HARDLINK_TEST skipped=win32-" +
                Marshal.GetLastWin32Error().ToString(
                    CultureInfo.InvariantCulture));
            return true;
        }

        try
        {
            return FailedWith(
                RunVerifier(artifact),
                "authority_output_hardlink_invalid");
        }
        finally
        {
            File.Delete(aliasPath);
        }
    }

    private static bool TestBoundedExternalEvaluationRunner(string tempRoot)
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string verifierPath = Path.Combine(
            repositoryRoot,
            "scripts",
            "test-efi-artifact.ps1");
        string verifierSource = File.ReadAllText(verifierPath);
        int createProcessCall = verifierSource.IndexOf(
            "if (!CreateProcessW(",
            StringComparison.Ordinal);
        int assignmentCall = verifierSource.IndexOf(
            "if (!AssignProcessToJobObject(",
            StringComparison.Ordinal);
        int resumeCall = verifierSource.IndexOf(
            "if (ResumeThread(thread)",
            StringComparison.Ordinal);
        string[] stableReasons =
        [
            "post_restore_evaluation_start_failed",
            "post_restore_evaluation_timeout",
            "post_restore_evaluation_output_overflow",
            "post_restore_evaluation_capture_failed",
            "post_restore_evaluation_nonzero_exit",
            "post_restore_evaluation_termination_unconfirmed"
        ];
        bool staticContract =
            !verifierSource.Contains(
                "& $DotnetHostPath",
                StringComparison.Ordinal)
            && verifierSource.Contains(
                "JobObjectLimitKillOnJobClose",
                StringComparison.Ordinal)
            && createProcessCall >= 0
            && assignmentCall > createProcessCall
            && resumeCall > assignmentCall
            && verifierSource.Contains(
                "-nodeReuse:false",
                StringComparison.Ordinal)
            && verifierSource.Contains(
                "-p:UseSharedCompilation=false",
                StringComparison.Ordinal)
            && verifierSource.Contains(
                "DOTNET_CLI_USE_MSBUILD_SERVER",
                StringComparison.Ordinal)
            && verifierSource.Contains(
                "MSBUILDDISABLENODEREUSE",
                StringComparison.Ordinal)
            && verifierSource.Contains(
                "$expectedSourceClosureInputCount = 115",
                StringComparison.Ordinal)
            && stableReasons.All(reason => verifierSource.Contains(
                reason,
                StringComparison.Ordinal));
        if (!staticContract)
        {
            Console.WriteLine(
                "EFI_ARTIFACT_RUNNER_STATIC_CONTRACT_FAIL " +
                "create=" + createProcessCall +
                " assign=" + assignmentCall +
                " resume=" + resumeCall);
            return false;
        }

        string probeRoot = Path.Combine(tempRoot, "bounded-runner-probe");
        Directory.CreateDirectory(probeRoot);
        string probePath = Path.Combine(
            probeRoot,
            "Invoke-BoundedRunnerProbe.ps1");
        File.WriteAllText(
            probePath,
            """
            param(
                [Parameter(Mandatory = $true)]
                [string]$VerifierPath,
                [Parameter(Mandatory = $true)]
                [string]$ProbeRoot
            )

            $ErrorActionPreference = "Stop"
            Set-StrictMode -Version Latest

            function Write-Failure {
                param(
                    [string]$Reason,
                    [string]$Detail = ""
                )

                throw "$Reason $Detail"
            }

            function Assert-Probe {
                param(
                    [bool]$Condition,
                    [string]$Reason
                )

                if (-not $Condition) {
                    throw $Reason
                }
            }

            $tokens = $null
            $parseErrors = $null
            $ast = [System.Management.Automation.Language.Parser]::ParseFile(
                $VerifierPath,
                [ref]$tokens,
                [ref]$parseErrors)
            Assert-Probe ($parseErrors.Count -eq 0) "verifier_parse_failed"
            $initializerAst = $ast.Find(
                {
                    param($node)
                    $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and
                        $node.Name -eq "Initialize-BoundedJobProcessRunner"
                },
                $true)
            Assert-Probe ($null -ne $initializerAst) "runner_initializer_missing"
            Invoke-Expression $initializerAst.Extent.Text
            Initialize-BoundedJobProcessRunner

            $workingDirectory = [System.IO.Path]::GetFullPath($ProbeRoot)
            $powershellPath = (Get-Command powershell.exe -CommandType Application |
                Select-Object -First 1).Source
            $missingExecutable = Join-Path $workingDirectory "missing-runner-host.exe"

            $positiveCommand =
                "[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding(`$false); " +
                "[Console]::Out.Write('stdout-ok'); " +
                "[Console]::Error.Write('stderr-ok')"
            $positive = [CLROS.Verifier.BoundedJobProcessRunner]::Run(
                $powershellPath,
                [string[]]@("-NoProfile", "-Command", $positiveCommand),
                $workingDirectory,
                5,
                4096)
            Assert-Probe ($positive.Status -eq "Success") "positive_status"
            Assert-Probe ($positive.ExitCode -eq 0) "positive_exit"
            Assert-Probe $positive.TerminationConfirmed "positive_termination"
            Assert-Probe (
                $positive.Output.Contains("stdout-ok") -and
                $positive.Output.Contains("stderr-ok")) "positive_output"

            $nonzero = [CLROS.Verifier.BoundedJobProcessRunner]::Run(
                $powershellPath,
                [string[]]@("-NoProfile", "-Command", "exit 7"),
                $workingDirectory,
                5,
                4096)
            Assert-Probe ($nonzero.Status -eq "NonZeroExit") "nonzero_status"
            Assert-Probe ($nonzero.ExitCode -eq 7) "nonzero_exit"
            Assert-Probe $nonzero.TerminationConfirmed "nonzero_termination"

            $overflowCommand =
                "[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding(`$false); " +
                "[Console]::Out.Write('x' * 8192)"
            $overflow = [CLROS.Verifier.BoundedJobProcessRunner]::Run(
                $powershellPath,
                [string[]]@("-NoProfile", "-Command", $overflowCommand),
                $workingDirectory,
                5,
                4096)
            Assert-Probe ($overflow.Status -eq "OutputOverflow") "overflow_status"
            Assert-Probe $overflow.TerminationConfirmed "overflow_termination"
            Assert-Probe ($overflow.Output.Length -le 4096) "overflow_hard_cap"
            Assert-Probe (
                $overflow.OutputCharacters -le 4096) "overflow_count_hard_cap"

            $captureFailureCommand =
                "`$stream = [Console]::OpenStandardOutput(); " +
                "`$bytes = [byte[]]@(255); " +
                "`$stream.Write(`$bytes, 0, `$bytes.Length); " +
                "`$stream.Flush(); Start-Sleep -Seconds 30"
            $captureFailure = [CLROS.Verifier.BoundedJobProcessRunner]::Run(
                $powershellPath,
                [string[]]@("-NoProfile", "-Command", $captureFailureCommand),
                $workingDirectory,
                5,
                4096)
            Assert-Probe (
                $captureFailure.Status -eq "CaptureFailed") "capture_status"
            Assert-Probe (
                $captureFailure.TerminationConfirmed) "capture_termination"

            $invalidTimeoutLow = [CLROS.Verifier.BoundedJobProcessRunner]::Run(
                $missingExecutable,
                [string[]]@(),
                $workingDirectory,
                4,
                4096)
            $invalidTimeoutHigh = [CLROS.Verifier.BoundedJobProcessRunner]::Run(
                $missingExecutable,
                [string[]]@(),
                $workingDirectory,
                3601,
                4096)
            $invalidLimitLow = [CLROS.Verifier.BoundedJobProcessRunner]::Run(
                $missingExecutable,
                [string[]]@(),
                $workingDirectory,
                5,
                4095)
            $invalidLimitHigh = [CLROS.Verifier.BoundedJobProcessRunner]::Run(
                $missingExecutable,
                [string[]]@(),
                $workingDirectory,
                5,
                67108865)
            $startFailure = [CLROS.Verifier.BoundedJobProcessRunner]::Run(
                $missingExecutable,
                [string[]]@(),
                $workingDirectory,
                5,
                4096)
            Assert-Probe (
                $invalidTimeoutLow.Status -eq "InvalidTimeout" -and
                $invalidTimeoutHigh.Status -eq "InvalidTimeout") "invalid_timeout"
            Assert-Probe (
                $invalidLimitLow.Status -eq "InvalidOutputLimit" -and
                $invalidLimitHigh.Status -eq "InvalidOutputLimit") "invalid_limit"
            Assert-Probe ($startFailure.Status -eq "StartFailed") "start_status"

            $childScriptPath = Join-Path $workingDirectory "lock-child.ps1"
            $parentScriptPath = Join-Path $workingDirectory "lock-parent.ps1"
            $sentinelPath = Join-Path $workingDirectory "descendant.lock"
            [System.IO.File]::WriteAllText(
                $childScriptPath,
                @'
            param([Parameter(Mandatory = $true)][string]$SentinelPath)
            $stream = [System.IO.File]::Open(
                $SentinelPath,
                [System.IO.FileMode]::OpenOrCreate,
                [System.IO.FileAccess]::ReadWrite,
                [System.IO.FileShare]::None)
            try {
                Start-Sleep -Seconds 60
            }
            finally {
                $stream.Dispose()
            }
            '@,
                (New-Object System.Text.UTF8Encoding($false)))
            [System.IO.File]::WriteAllText(
                $parentScriptPath,
                @'
            param(
                [Parameter(Mandatory = $true)][string]$ChildScriptPath,
                [Parameter(Mandatory = $true)][string]$SentinelPath
            )
            $arguments =
                '-NoProfile -ExecutionPolicy Bypass -File "' +
                $ChildScriptPath +
                '" "' +
                $SentinelPath +
                '"'
            $child = Start-Process `
                -FilePath powershell.exe `
                -ArgumentList $arguments `
                -NoNewWindow `
                -PassThru
            $deadline = [DateTime]::UtcNow.AddSeconds(4)
            while (-not (Test-Path -LiteralPath $SentinelPath -PathType Leaf)) {
                if ([DateTime]::UtcNow -ge $deadline) {
                    throw "descendant_lock_timeout"
                }
                Start-Sleep -Milliseconds 20
            }
            Start-Sleep -Seconds 60
            '@,
                (New-Object System.Text.UTF8Encoding($false)))
            $timeout = [CLROS.Verifier.BoundedJobProcessRunner]::Run(
                $powershellPath,
                [string[]]@(
                    "-NoProfile",
                    "-ExecutionPolicy",
                    "Bypass",
                    "-File",
                    $parentScriptPath,
                    $childScriptPath,
                    $sentinelPath),
                $workingDirectory,
                5,
                4096)
            Assert-Probe ($timeout.Status -eq "TimedOut") "timeout_status"
            Assert-Probe $timeout.TerminationConfirmed "timeout_termination"
            Assert-Probe (
                (Test-Path -LiteralPath $sentinelPath -PathType Leaf)) "sentinel_missing"
            $sentinelProbe = [System.IO.File]::Open(
                $sentinelPath,
                [System.IO.FileMode]::Open,
                [System.IO.FileAccess]::ReadWrite,
                [System.IO.FileShare]::None)
            $sentinelProbe.Dispose()
            Remove-Item -LiteralPath $sentinelPath -Force

            $handleBaseline = (Get-Process -Id $PID).HandleCount
            for ($index = 0; $index -lt 8; $index++) {
                $repeat = [CLROS.Verifier.BoundedJobProcessRunner]::Run(
                    $powershellPath,
                    [string[]]@("-NoProfile", "-Command", "exit 0"),
                    $workingDirectory,
                    5,
                    4096)
                Assert-Probe ($repeat.Status -eq "Success") "repeat_status"
            }
            [GC]::Collect()
            [GC]::WaitForPendingFinalizers()
            Start-Sleep -Milliseconds 200
            $handleFinal = (Get-Process -Id $PID).HandleCount
            Assert-Probe (
                $handleFinal -le ($handleBaseline + 4)) "runner_handle_leak"

            Write-Output (
                "BOUNDED_RUNNER_PROBE_PASS " +
                "baselineHandles=$handleBaseline finalHandles=$handleFinal")
            """,
            new UTF8Encoding(false));

        ProcessResult probeResult = RunBoundedProcess(
            "powershell.exe",
            [
                "-NoProfile",
                "-ExecutionPolicy",
                "Bypass",
                "-File",
                probePath,
                verifierPath,
                probeRoot
            ],
            timeoutMilliseconds: 45_000);
        bool passed = !probeResult.TimedOut
            && probeResult.ExitCode == 0
            && probeResult.Output.Contains(
                "BOUNDED_RUNNER_PROBE_PASS",
                StringComparison.Ordinal);
        if (!passed)
        {
            WriteFixtureDiagnostic("bounded-runner-probe", probeResult);
        }

        return passed;
    }

    private static bool EvidenceMutationFails(
        string tempRoot,
        string name,
        Action<JsonObject> mutation,
        string expectedReason)
    {
        string artifact = WriteFixture(
            tempRoot,
            name,
            CreateValidPe());
        LinkEvidencePaths paths = WriteLinkEvidence(artifact);
        MutateEvidence(paths.EvidencePath, mutation);
        return FailedWith(RunVerifier(artifact), expectedReason);
    }

    private static bool TestPinnedRestoreArchiveMutation(string tempRoot)
    {
        string artifact = WriteFixture(
            tempRoot,
            "pinned-restore-archive-mutation",
            CreateValidPe());
        LinkEvidencePaths paths = WriteLinkEvidence(artifact);
        SchemaThreeFixture fixture = cachedSchemaThreeFixture
            ?? throw new InvalidOperationException(
                "Schema-three fixture was not initialized.");
        string archivePath = Path.Combine(
            fixture.PinnedRestoreSourcePath,
            ExpectedRestoreArchives[0].FileName);
        long originalLength = new FileInfo(archivePath).Length;
        ProcessResult result;
        try
        {
            using (var stream = new FileStream(
                archivePath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.None))
            {
                stream.WriteByte(0);
            }

            result = RunVerifier(artifact);
        }
        finally
        {
            using var stream = new FileStream(
                archivePath,
                FileMode.Open,
                FileAccess.Write,
                FileShare.None);
            stream.SetLength(originalLength);
        }

        return FailedWith(result, "pinned_restore_source_input_hash_mismatch");
    }

    private static bool TestGeneratedRestoreExecutableSurfaceMutation(
        string tempRoot)
    {
        string artifact = WriteFixture(
            tempRoot,
            "generated-restore-using-task",
            CreateValidPe());
        LinkEvidencePaths paths = WriteLinkEvidence(artifact);
        SchemaThreeFixture fixture = cachedSchemaThreeFixture
            ?? throw new InvalidOperationException(
                "Schema-three fixture was not initialized.");
        string targetsPath = Path.Combine(
            fixture.ProjectExtensionsPath,
            "OS.csproj.nuget.g.targets");
        byte[] originalBytes = File.ReadAllBytes(targetsPath);
        ProcessResult result;
        try
        {
            File.WriteAllText(
                targetsPath,
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                "<Project ToolsVersion=\"14.0\" " +
                "xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">" +
                "<UsingTask TaskName=\"Injected\" AssemblyFile=\"outside.dll\" />" +
                "</Project>",
                new UTF8Encoding(false));
            MutateEvidence(
                paths.EvidencePath,
                evidence => RefreshClosureInputAndAggregate(
                    evidence,
                    "restoreClosureInputs",
                    "restoreClosureSha256",
                    Path.GetRelativePath(
                            fixture.RepositoryRoot,
                            targetsPath)
                        .Replace('\\', '/'),
                    targetsPath));
            result = RunVerifier(artifact);
        }
        finally
        {
            File.WriteAllBytes(targetsPath, originalBytes);
        }

        return FailedWith(result, "restore_generated_targets_not_empty");
    }

    private static bool TestPackageMetadataMutation(string tempRoot)
    {
        string artifact = WriteFixture(
            tempRoot,
            "package-metadata-source-mutation",
            CreateValidPe());
        WriteLinkEvidence(artifact);
        SchemaThreeFixture fixture = cachedSchemaThreeFixture
            ?? throw new InvalidOperationException(
                "Schema-three fixture was not initialized.");
        string metadataPath = Path.Combine(
            fixture.PackageRootPath,
            "microsoft.dotnet.ilcompiler",
            "10.0.8",
            ".nupkg.metadata");
        byte[] originalBytes = File.ReadAllBytes(metadataPath);
        ProcessResult result;
        try
        {
            JsonObject metadata = JsonNode.Parse(
                    File.ReadAllText(metadataPath))!.AsObject();
            metadata["source"] = Path.GetTempPath();
            File.WriteAllText(
                metadataPath,
                metadata.ToJsonString(),
                new UTF8Encoding(false));
            result = RunVerifier(artifact);
        }
        finally
        {
            File.WriteAllBytes(metadataPath, originalBytes);
        }

        return FailedWith(result, "package_metadata_source_mismatch");
    }

    private static bool TestPresentUserExtensionsDirectory(string tempRoot)
    {
        string artifact = WriteFixture(
            tempRoot,
            "post-restore-user-extensions-present",
            CreateValidPe());
        WriteLinkEvidence(artifact);
        SchemaThreeFixture fixture = cachedSchemaThreeFixture
            ?? throw new InvalidOperationException(
                "Schema-three fixture was not initialized.");
        string userExtensionsPath = Path.Combine(
            fixture.ProjectExtensionsPath,
            "disabled-user-extensions");
        Directory.CreateDirectory(userExtensionsPath);
        ProcessResult result;
        try
        {
            result = RunVerifier(artifact);
        }
        finally
        {
            Directory.Delete(userExtensionsPath, recursive: false);
        }

        return FailedWith(
            result,
            "post_restore_user_extensions_path_present");
    }

    private static bool TestAuthorityOutputHardlink(string tempRoot)
    {
        string artifact = WriteFixture(
            tempRoot,
            "authority-output-hardlink",
            CreateValidPe());
        WriteLinkEvidence(artifact);
        string aliasPath = artifact + ".unexpected-hardlink";
        if (!CreateHardLink(
                aliasPath,
                artifact,
                IntPtr.Zero))
        {
            throw new InvalidOperationException(
                "Fixture hardlink creation failed with Win32 error " +
                Marshal.GetLastWin32Error().ToString(
                    CultureInfo.InvariantCulture));
        }

        ProcessResult result;
        try
        {
            result = RunVerifier(artifact);
        }
        finally
        {
            File.Delete(aliasPath);
        }

        return FailedWith(result, "authority_output_hardlink_invalid");
    }

    private static bool TryTestAuthorityArtifactReparsePoint(string tempRoot)
    {
        string artifact = WriteFixture(
            tempRoot,
            "authority-artifact-reparse-point",
            CreateValidPe());
        WriteLinkEvidence(artifact);
        return TryReplaceFileWithSymbolicLinkAndVerify(
            artifact,
            artifact,
            "artifact_reparse_point",
            "artifact");
    }

    private static bool TryTestAuthorityMapReparsePoint(string tempRoot)
    {
        string artifact = WriteFixture(
            tempRoot,
            "authority-map-reparse-point",
            CreateValidPe());
        LinkEvidencePaths paths = WriteLinkEvidence(artifact);
        return TryReplaceFileWithSymbolicLinkAndVerify(
            paths.MapPath,
            artifact,
            "linker_map_reparse_point",
            "linker-map");
    }

    private static bool TryTestAuthorityResponseReparsePoint(string tempRoot)
    {
        string artifact = WriteFixture(
            tempRoot,
            "authority-response-reparse-point",
            CreateValidPe());
        LinkEvidencePaths paths = WriteLinkEvidence(artifact);
        return TryReplaceFileWithSymbolicLinkAndVerify(
            paths.ResponsePath,
            artifact,
            "link_response_reparse_point",
            "link-response");
    }

    private static bool TryTestAuthorityEvidenceReparsePoint(string tempRoot)
    {
        string artifact = WriteFixture(
            tempRoot,
            "authority-evidence-reparse-point",
            CreateValidPe());
        LinkEvidencePaths paths = WriteLinkEvidence(artifact);
        return TryReplaceFileWithSymbolicLinkAndVerify(
            paths.EvidencePath,
            artifact,
            "link_evidence_reparse_point",
            "link-evidence");
    }

    private static bool TryReplaceFileWithSymbolicLinkAndVerify(
        string authorityPath,
        string artifactPath,
        string expectedReason,
        string surface)
    {
        string backingPath = authorityPath + ".backing";
        File.Move(authorityPath, backingPath);
        bool linkCreated = false;
        try
        {
            try
            {
                File.CreateSymbolicLink(authorityPath, backingPath);
                linkCreated = true;
            }
            catch (Exception ex) when (
                ex is UnauthorizedAccessException ||
                ex is PlatformNotSupportedException ||
                ex is IOException)
            {
                Console.WriteLine(
                    "EFI_ARTIFACT_REPARSE_TEST skipped=" +
                    ex.GetType().Name +
                    " surface=" +
                    surface);
                return true;
            }

            return FailedWith(
                RunVerifier(artifactPath),
                expectedReason);
        }
        finally
        {
            if (linkCreated && File.Exists(authorityPath))
            {
                File.Delete(authorityPath);
            }

            if (File.Exists(backingPath))
            {
                File.Move(
                    backingPath,
                    authorityPath,
                    overwrite: true);
            }
        }
    }

    private static bool TryTestSourceClosureReparsePoint(string tempRoot)
    {
        string artifact = WriteFixture(
            tempRoot,
            "source-closure-reparse-point",
            CreateValidPe());
        WriteLinkEvidence(artifact);
        SchemaThreeFixture fixture = cachedSchemaThreeFixture
            ?? throw new InvalidOperationException(
                "Schema-three fixture was not initialized.");
        string sourcePath = Path.Combine(
            fixture.RepositoryRoot,
            "tools",
            "OS.X64TextSerializerGenerator",
            "Program.cs");
        string backingPath = sourcePath + ".backing";
        File.Move(sourcePath, backingPath);
        bool linkCreated = false;
        try
        {
            try
            {
                File.CreateSymbolicLink(sourcePath, backingPath);
                linkCreated = true;
            }
            catch (Exception ex) when (
                ex is UnauthorizedAccessException ||
                ex is PlatformNotSupportedException ||
                ex is IOException)
            {
                Console.WriteLine(
                    "EFI_ARTIFACT_REPARSE_TEST skipped=" +
                    ex.GetType().Name);
                return true;
            }

            return FailedWith(
                RunVerifier(artifact),
                "source_closure_reparse_point");
        }
        finally
        {
            if (linkCreated && File.Exists(sourcePath))
            {
                File.Delete(sourcePath);
            }

            if (File.Exists(backingPath))
            {
                File.Move(
                    backingPath,
                    sourcePath,
                    overwrite: true);
            }
        }
    }

    private static bool TestPackageFileMutation(string tempRoot)
    {
        string artifact = WriteFixture(
            tempRoot,
            "package-tree-file-mutation",
            CreateValidPe());
        WriteLinkEvidence(artifact);
        SchemaThreeFixture fixture = cachedSchemaThreeFixture
            ?? throw new InvalidOperationException(
                "Schema-three fixture was not initialized.");
        string targetPath = Path.Combine(
            fixture.PackageRootPath,
            "microsoft.dotnet.ilcompiler",
            "10.0.8",
            "build",
            "Microsoft.DotNet.ILCompiler.props");
        long originalLength = new FileInfo(targetPath).Length;
        ProcessResult result;
        try
        {
            using (var stream = new FileStream(
                targetPath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.None))
            {
                stream.WriteByte(0);
            }

            result = RunVerifier(artifact);
        }
        finally
        {
            using var stream = new FileStream(
                targetPath,
                FileMode.Open,
                FileAccess.Write,
                FileShare.None);
            stream.SetLength(originalLength);
        }

        return FailedWith(result, "package_tree_contract_mismatch");
    }

    private static bool TestExtraPackageVersion(string tempRoot)
    {
        string artifact = WriteFixture(
            tempRoot,
            "package-version-extra",
            CreateValidPe());
        WriteLinkEvidence(artifact);
        SchemaThreeFixture fixture = cachedSchemaThreeFixture
            ?? throw new InvalidOperationException(
                "Schema-three fixture was not initialized.");
        string extraVersionPath = Path.Combine(
            fixture.PackageRootPath,
            "microsoft.dotnet.ilcompiler",
            "10.0.999");
        Directory.CreateDirectory(extraVersionPath);
        ProcessResult result;
        try
        {
            result = RunVerifier(artifact);
        }
        finally
        {
            Directory.Delete(extraVersionPath, recursive: false);
        }

        return FailedWith(result, "isolated_package_version_count_invalid");
    }

    private static bool TryTestPackageReparsePoint(string tempRoot)
    {
        string artifact = WriteFixture(
            tempRoot,
            "package-tree-reparse-point",
            CreateValidPe());
        WriteLinkEvidence(artifact);
        SchemaThreeFixture fixture = cachedSchemaThreeFixture
            ?? throw new InvalidOperationException(
                "Schema-three fixture was not initialized.");
        string linkPath = Path.Combine(
            fixture.PackageRootPath,
            "microsoft.dotnet.ilcompiler",
            "10.0.8",
            "fixture-reparse");
        string targetPath = Path.Combine(
            Path.GetDirectoryName(artifact)!,
            "reparse-target");
        Directory.CreateDirectory(targetPath);
        try
        {
            try
            {
                Directory.CreateSymbolicLink(linkPath, targetPath);
            }
            catch (Exception ex) when (
                ex is UnauthorizedAccessException ||
                ex is PlatformNotSupportedException ||
                ex is IOException)
            {
                Console.WriteLine(
                    "EFI_ARTIFACT_REPARSE_TEST skipped=" +
                    ex.GetType().Name);
                return true;
            }

            return FailedWith(
                RunVerifier(artifact),
                "package_tree_reparse_point");
        }
        finally
        {
            if (Directory.Exists(linkPath) ||
                File.Exists(linkPath))
            {
                Directory.Delete(linkPath, recursive: false);
            }

            if (Directory.Exists(targetPath))
            {
                Directory.Delete(targetPath, recursive: false);
            }
        }
    }

    private static JsonArray GetEvidenceArray(
        JsonObject evidence,
        string propertyName)
    {
        return evidence[propertyName]?.AsArray()
            ?? throw new InvalidOperationException(
                "Fixture evidence array did not parse: " + propertyName);
    }

    private static void SwapArrayEntries(
        JsonArray array,
        int firstIndex,
        int secondIndex)
    {
        JsonNode first = array[firstIndex]?.DeepClone()
            ?? throw new InvalidOperationException(
                "Fixture array entry was null.");
        JsonNode second = array[secondIndex]?.DeepClone()
            ?? throw new InvalidOperationException(
                "Fixture array entry was null.");
        array[firstIndex] = second;
        array[secondIndex] = first;
    }

    private static void RefreshClosureInputAndAggregate(
        JsonObject evidence,
        string inputsProperty,
        string aggregateProperty,
        string relativePath,
        string fullPath)
    {
        JsonArray inputs = GetEvidenceArray(evidence, inputsProperty);
        bool found = false;
        var aggregate = new StringBuilder();
        foreach (JsonNode? node in inputs)
        {
            JsonObject input = node?.AsObject()
                ?? throw new InvalidOperationException(
                    "Fixture closure input did not parse.");
            string path = input["path"]?.GetValue<string>()
                ?? throw new InvalidOperationException(
                    "Fixture closure path did not parse.");
            if (string.Equals(path, relativePath, StringComparison.Ordinal))
            {
                input["sha256"] = GetSha256(fullPath);
                found = true;
            }

            aggregate.Append(path);
            aggregate.Append('|');
            aggregate.Append(input["sha256"]?.GetValue<string>());
            aggregate.Append('\n');
        }

        if (!found)
        {
            throw new InvalidOperationException(
                "Fixture closure path was not found: " + relativePath);
        }

        evidence[aggregateProperty] = GetSha256(
            Encoding.UTF8.GetBytes(aggregate.ToString()));
    }

    private static bool TestParserCapacityBounds(string tempRoot)
    {
        const long maximumVerifierProcessSafetyMilliseconds =
            VerifierFixtureProcessTimeoutMilliseconds;
        const long maximumLinkResponseTokenScanElapsedMilliseconds = 1_000;
        const long maximumRuntimeScanElapsedMilliseconds = 1_000;

        string atCapacityResponse = string.Join(
            "\r\n",
            Enumerable.Range(0, MaximumLinkResponseTokens - 7)
                .Select(index => "relative-object-" + index + ".obj"));
        ProcessResult responseAtCapacityResult = RunFixtureWithEvidence(
            tempRoot,
            "response-token-at-capacity",
            CreateValidPe(),
            new LinkEvidenceOptions
            {
                AdditionalResponse = atCapacityResponse
            });

        string overCapacityResponse = string.Join(
            "\r\n",
            Enumerable.Range(0, MaximumLinkResponseTokens - 6)
                .Select(index => "/NOOP" + index));
        ProcessResult responseOverCapacityResult = RunFixtureWithEvidence(
            tempRoot,
            "response-token-over-capacity",
            CreateValidPe(),
            new LinkEvidenceOptions
            {
                AdditionalResponse = overCapacityResponse
            });

        ProcessResult runtimeAtCapacityResult = RunFixtureWithEvidence(
            tempRoot,
            "runtime-function-distinct-unwind-at-capacity",
            CreateRuntimeCapacityPe(MaximumRuntimeFunctionRecords));

        byte[] runtimeOverCapacity = CreateValidPe();
        WriteDataDirectory(
            runtimeOverCapacity,
            3,
            PdataRva,
            checked((uint)(MaximumRuntimeFunctionRecords + 1) * 12u));
        ProcessResult runtimeOverCapacityResult = RunPeFixtureVerifier(
            WriteFixture(
                tempRoot,
                "runtime-function-over-capacity",
                runtimeOverCapacity));

        bool responseAtCapacityPassed = PassedVerifier(responseAtCapacityResult);
        bool responseAtCapacityBounded = CompletedWithin(
            "response-token-at-capacity",
            responseAtCapacityResult,
            maximumVerifierProcessSafetyMilliseconds);
        System.Text.RegularExpressions.MatchCollection responseTokenScanMatches =
            System.Text.RegularExpressions.Regex.Matches(
                responseAtCapacityResult.Output,
                @"^EFI_ARTIFACT_LINK_RESPONSE_SCAN tokens=4096 elapsedMs=([0-9]+) maximumTokens=4096\r?$",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant |
                    System.Text.RegularExpressions.RegexOptions.Multiline);
        bool responseAtCapacityModeledTokenScan =
            responseTokenScanMatches.Count == 1;
        System.Text.RegularExpressions.Match responseTokenScanMatch =
            responseAtCapacityModeledTokenScan
                ? responseTokenScanMatches[0]
                : System.Text.RegularExpressions.Match.Empty;
        if (!responseAtCapacityModeledTokenScan)
        {
            WriteFixtureDiagnostic(
                "response-token-scan-marker-missing",
                responseAtCapacityResult);
        }
        long responseTokenScanElapsedMilliseconds = long.MaxValue;
        bool responseTokenScanParsed = responseAtCapacityModeledTokenScan &&
            long.TryParse(
                responseTokenScanMatch.Groups[1].Value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out responseTokenScanElapsedMilliseconds);
        bool responseTokenScanBounded = responseTokenScanParsed &&
            responseTokenScanElapsedMilliseconds <=
                maximumLinkResponseTokenScanElapsedMilliseconds;
        if (!responseTokenScanBounded)
        {
            WriteFixtureDiagnostic(
                "response-token-scan-maximum-elapsed-" +
                    maximumLinkResponseTokenScanElapsedMilliseconds,
                responseAtCapacityResult);
        }
        bool responseOverCapacityFailed = FailedWith(
            responseOverCapacityResult,
            "link_response_token_count_out_of_range");
        bool responseOverCapacityBounded = CompletedWithin(
            "response-token-over-capacity",
            responseOverCapacityResult,
            maximumVerifierProcessSafetyMilliseconds);
        bool runtimeAtCapacityPassed = PassedVerifier(runtimeAtCapacityResult);
        System.Text.RegularExpressions.MatchCollection runtimeScanMatches =
            System.Text.RegularExpressions.Regex.Matches(
                runtimeAtCapacityResult.Output,
                @"^EFI_ARTIFACT_RUNTIME_SCAN records=2048 distinctUnwind=2048 elapsedMs=([0-9]+) maximumRecords=2048\r?$",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant |
                    System.Text.RegularExpressions.RegexOptions.Multiline);
        bool runtimeAtCapacityModeledDistinctUnwind =
            runtimeScanMatches.Count == 1;
        System.Text.RegularExpressions.Match runtimeScanMatch =
            runtimeAtCapacityModeledDistinctUnwind
                ? runtimeScanMatches[0]
                : System.Text.RegularExpressions.Match.Empty;
        if (!runtimeAtCapacityModeledDistinctUnwind)
        {
            WriteFixtureDiagnostic(
                "runtime-function-distinct-unwind-marker-missing",
                runtimeAtCapacityResult);
        }
        long runtimeScanElapsedMilliseconds = long.MaxValue;
        bool runtimeScanParsed = runtimeAtCapacityModeledDistinctUnwind &&
            long.TryParse(
                runtimeScanMatch.Groups[1].Value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out runtimeScanElapsedMilliseconds);
        bool runtimeScanBounded = runtimeScanParsed &&
            runtimeScanElapsedMilliseconds <=
                maximumRuntimeScanElapsedMilliseconds;
        if (!runtimeScanBounded)
        {
            WriteFixtureDiagnostic(
                "runtime-function-scan-maximum-elapsed-" +
                    maximumRuntimeScanElapsedMilliseconds,
                runtimeAtCapacityResult);
        }
        bool runtimeAtCapacityBounded = CompletedWithin(
            "runtime-function-distinct-unwind-at-capacity",
            runtimeAtCapacityResult,
            maximumVerifierProcessSafetyMilliseconds);
        Console.WriteLine(
            "EFI_ARTIFACT_PERFORMANCE responseTokens=" +
            MaximumLinkResponseTokens +
            " responseScanElapsedMs=" +
            (responseAtCapacityModeledTokenScan
                ? responseTokenScanMatch.Groups[1].Value
                : "missing") +
            " distinctUnwindRecords=" +
            MaximumRuntimeFunctionRecords +
            " processElapsedMs=" +
            runtimeAtCapacityResult.ElapsedMilliseconds +
            " scanElapsedMs=" +
            (runtimeAtCapacityModeledDistinctUnwind
                ? runtimeScanMatch.Groups[1].Value
                : "missing") +
            " processSafetyBudgetMs=" +
            maximumVerifierProcessSafetyMilliseconds +
            " responseScanBudgetMs=" +
            maximumLinkResponseTokenScanElapsedMilliseconds +
            " runtimeScanBudgetMs=" +
            maximumRuntimeScanElapsedMilliseconds);
        bool runtimeOverCapacityFailed = FailedWith(
            runtimeOverCapacityResult,
            "exception_directory_record_count_out_of_range");
        bool runtimeOverCapacityBounded = CompletedWithin(
            "runtime-function-over-capacity",
            runtimeOverCapacityResult,
            maximumVerifierProcessSafetyMilliseconds);

        return responseAtCapacityPassed
            && responseAtCapacityBounded
            && responseAtCapacityModeledTokenScan
            && responseTokenScanBounded
            && responseOverCapacityFailed
            && responseOverCapacityBounded
            && runtimeAtCapacityPassed
            && runtimeAtCapacityModeledDistinctUnwind
            && runtimeScanBounded
            && runtimeAtCapacityBounded
            && runtimeOverCapacityFailed
            && runtimeOverCapacityBounded;
    }

    private static bool CompletedWithin(
        string context,
        ProcessResult result,
        long maximumElapsedMilliseconds)
    {
        bool completedWithin =
            result.ElapsedMilliseconds <= maximumElapsedMilliseconds;
        if (!completedWithin)
        {
            WriteFixtureDiagnostic(
                context + "-maximum-elapsed-" + maximumElapsedMilliseconds,
                result);
        }

        return completedWithin;
    }

    private static bool PassedVerifier(ProcessResult result)
    {
        bool passed = !result.TimedOut
            && result.ExitCode == 0
            && result.Output.Contains(
                "TEST_PASS efi.artifact.verify",
                StringComparison.Ordinal)
            && !result.Output.Contains(
                "TEST_FAIL efi.artifact.verify",
                StringComparison.Ordinal);
        if (!passed)
        {
            WriteFixtureDiagnostic("expected-pass", result);
        }

        return passed;
    }

    private static ProcessResult RunFixtureWithEvidence(
        string tempRoot,
        string name,
        byte[] bytes,
        LinkEvidenceOptions? options = null)
    {
        string artifact = WriteFixture(tempRoot, name, bytes);
        WriteLinkEvidence(artifact, options);
        return RunVerifier(artifact);
    }

    private static bool FailedWith(ProcessResult result, string reason)
    {
        bool matched = !result.TimedOut
            && result.ExitCode == 1
            && result.Output.Contains(
                "TEST_FAIL efi.artifact.verify " + reason,
                StringComparison.Ordinal)
            && !result.Output.Contains(
                "TEST_PASS efi.artifact.verify",
                StringComparison.Ordinal);
        if (!matched)
        {
            WriteFixtureDiagnostic("expected-" + reason, result);
        }

        return matched;
    }

    private static void WriteFixtureDiagnostic(string context, ProcessResult result)
    {
        string output = result.Output
            .Replace("\r\n", " | ", StringComparison.Ordinal)
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
        Console.WriteLine(
            "EFI_ARTIFACT_FIXTURE_DIAGNOSTIC context=" +
            context +
            " exit=" +
            result.ExitCode +
            " timeout=" +
            result.TimedOut +
            " elapsedMs=" +
            result.ElapsedMilliseconds +
            " output=\"" +
            output.Replace("\"", "'", StringComparison.Ordinal) +
            "\"");
    }

    private static string WriteFixture(string tempRoot, string name, byte[] bytes)
    {
        string fixtureDirectory = Path.Combine(tempRoot, name);
        Directory.CreateDirectory(fixtureDirectory);
        string artifact = Path.Combine(fixtureDirectory, "BOOTX64.EFI");
        File.WriteAllBytes(artifact, bytes);
        return artifact;
    }

    private static byte[] CreateValidPe(uint serializerRva = SerializerRva)
    {
        byte[] bytes = new byte[0x800];

        bytes[0] = (byte)'M';
        bytes[1] = (byte)'Z';
        WriteUInt32(bytes, 0x3c, PeOffset);

        bytes[PeOffset] = (byte)'P';
        bytes[PeOffset + 1] = (byte)'E';
        WriteUInt16(bytes, PeOffset + 4, 0x8664);
        WriteUInt16(bytes, PeOffset + 6, 3);
        WriteUInt32(bytes, PeOffset + 8, CoffTimestamp);
        WriteUInt16(bytes, PeOffset + 20, 0xf0);
        WriteUInt16(bytes, PeOffset + 22, 0x0022);

        WriteUInt16(bytes, OptionalHeaderOffset, 0x020b);
        WriteUInt32(bytes, OptionalHeaderOffset + 16, EntryPointRva);
        WriteUInt32(bytes, OptionalHeaderOffset + 20, TextRva);
        WriteUInt64(bytes, OptionalHeaderOffset + 24, ImageBase);
        WriteUInt32(bytes, OptionalHeaderOffset + 32, 0x1000);
        WriteUInt32(bytes, OptionalHeaderOffset + 36, 0x200);
        WriteUInt32(bytes, OptionalHeaderOffset + 56, 0x4000);
        WriteUInt32(bytes, OptionalHeaderOffset + 60, 0x200);
        WriteUInt16(bytes, OptionalHeaderOffset + 68, 10);
        WriteUInt32(bytes, OptionalHeaderOffset + 108, 16);
        WriteDataDirectory(bytes, 3, PdataRva, 12);

        WriteSection(
            bytes,
            TextSectionHeaderOffset,
            ".text",
            virtualSize: 0x200,
            virtualAddress: TextRva,
            rawSize: 0x200,
            rawOffset: TextRawOffset,
            characteristics: 0x60000020);
        WriteSection(
            bytes,
            PdataSectionHeaderOffset,
            ".pdata",
            virtualSize: 0x200,
            virtualAddress: PdataRva,
            rawSize: 0x200,
            rawOffset: PdataRawOffset,
            characteristics: 0x40000040);
        WriteSection(
            bytes,
            XdataSectionHeaderOffset,
            ".xdata",
            virtualSize: 0x200,
            virtualAddress: XdataRva,
            rawSize: 0x200,
            rawOffset: XdataRawOffset,
            characteristics: 0x40000040);

        bytes[TextRawOffset] = 0x48;
        bytes[TextRawOffset + 1] = 0x31;
        bytes[TextRawOffset + 2] = 0xc0;
        bytes[TextRawOffset + 3] = 0xc3;
        ExpectedSerializerBytes.CopyTo(bytes, GetSerializerFileOffset(serializerRva));

        WriteRuntimeFunction(
            bytes,
            0,
            serializerRva,
            serializerRva + (uint)ExpectedSerializerBytes.Length,
            XdataRva);
        ExpectedUnwindBytes.CopyTo(bytes, XdataRawOffset);

        return bytes;
    }

    private static byte[] CreateRuntimeCapacityPe(int recordCount)
    {
        if (recordCount < 257 || recordCount > MaximumRuntimeFunctionRecords)
        {
            throw new ArgumentOutOfRangeException(nameof(recordCount));
        }

        const uint capacityPdataRva = 0x3000;
        int capacityPdataRawOffset = PdataRawOffset;
        int capacityPdataBytes = checked(recordCount * 12);
        int capacityXdataRawOffset = checked(
            capacityPdataRawOffset + capacityPdataBytes);
        int capacityXdataBytes = checked(recordCount * 8);
        uint capacityXdataRva = checked(
            capacityPdataRva + (uint)capacityPdataBytes);
        uint capacitySizeOfImage = checked(
            (capacityXdataRva + (uint)capacityXdataBytes + 0xfffu) &
            ~0xfffu);
        byte[] bytes = new byte[
            checked(capacityXdataRawOffset + capacityXdataBytes)];

        bytes[0] = (byte)'M';
        bytes[1] = (byte)'Z';
        WriteUInt32(bytes, 0x3c, PeOffset);

        bytes[PeOffset] = (byte)'P';
        bytes[PeOffset + 1] = (byte)'E';
        WriteUInt16(bytes, PeOffset + 4, 0x8664);
        WriteUInt16(bytes, PeOffset + 6, 3);
        WriteUInt32(bytes, PeOffset + 8, CoffTimestamp);
        WriteUInt16(bytes, PeOffset + 20, 0xf0);
        WriteUInt16(bytes, PeOffset + 22, 0x0022);

        WriteUInt16(bytes, OptionalHeaderOffset, 0x020b);
        WriteUInt32(bytes, OptionalHeaderOffset + 16, EntryPointRva);
        WriteUInt32(bytes, OptionalHeaderOffset + 20, TextRva);
        WriteUInt64(bytes, OptionalHeaderOffset + 24, ImageBase);
        WriteUInt32(bytes, OptionalHeaderOffset + 32, 0x1000);
        WriteUInt32(bytes, OptionalHeaderOffset + 36, 0x200);
        WriteUInt32(bytes, OptionalHeaderOffset + 56, capacitySizeOfImage);
        WriteUInt32(bytes, OptionalHeaderOffset + 60, 0x200);
        WriteUInt16(bytes, OptionalHeaderOffset + 68, 10);
        WriteUInt32(bytes, OptionalHeaderOffset + 108, 16);
        WriteDataDirectory(
            bytes,
            3,
            capacityPdataRva,
            checked((uint)capacityPdataBytes));

        WriteSection(
            bytes,
            TextSectionHeaderOffset,
            ".text",
            virtualSize: 0x200,
            virtualAddress: TextRva,
            rawSize: 0x200,
            rawOffset: TextRawOffset,
            characteristics: 0x60000020);
        WriteSection(
            bytes,
            PdataSectionHeaderOffset,
            ".pdata",
            virtualSize: checked((uint)capacityPdataBytes),
            virtualAddress: capacityPdataRva,
            rawSize: checked((uint)capacityPdataBytes),
            rawOffset: checked((uint)capacityPdataRawOffset),
            characteristics: 0x40000040);
        WriteSection(
            bytes,
            XdataSectionHeaderOffset,
            ".xdata",
            virtualSize: checked((uint)capacityXdataBytes),
            virtualAddress: capacityXdataRva,
            rawSize: checked((uint)capacityXdataBytes),
            rawOffset: checked((uint)capacityXdataRawOffset),
            characteristics: 0x40000040);

        bytes[TextRawOffset] = 0x48;
        bytes[TextRawOffset + 1] = 0x31;
        bytes[TextRawOffset + 2] = 0xc0;
        bytes[TextRawOffset + 3] = 0xc3;
        ExpectedSerializerBytes.CopyTo(
            bytes,
            GetSerializerFileOffset(SerializerRva));

        uint cursor = TextRva;
        bool serializerWritten = false;
        for (int index = 0; index < recordCount; index++)
        {
            uint begin = cursor;
            uint end;
            if (cursor == SerializerRva)
            {
                end = SerializerEndRva;
                serializerWritten = true;
            }
            else
            {
                end = checked(cursor + 1);
            }
            uint unwind = checked(capacityXdataRva + ((uint)index * 8u));

            int recordOffset = checked(
                capacityPdataRawOffset + (index * 12));
            WriteUInt32(bytes, recordOffset, begin);
            WriteUInt32(bytes, recordOffset + 4, end);
            WriteUInt32(bytes, recordOffset + 8, unwind);

            int unwindOffset = checked(
                capacityXdataRawOffset + (index * 8));
            if (begin == SerializerRva)
            {
                ExpectedUnwindBytes.CopyTo(bytes, unwindOffset);
            }
            else
            {
                bytes[unwindOffset] = 0x01;
            }

            cursor = end;
        }
        if (!serializerWritten)
        {
            throw new InvalidOperationException(
                "Capacity fixture did not contain the serializer range.");
        }

        return bytes;
    }

    private static int GetSerializerFileOffset(uint serializerRva)
    {
        return checked(TextRawOffset + (int)(serializerRva - TextRva));
    }

    private static void WriteSection(
        byte[] bytes,
        int offset,
        string name,
        uint virtualSize,
        uint virtualAddress,
        uint rawSize,
        uint rawOffset,
        uint characteristics)
    {
        byte[] encodedName = new byte[8];
        Encoding.ASCII.GetBytes(name).CopyTo(encodedName, 0);
        encodedName.CopyTo(bytes, offset);
        WriteUInt32(bytes, offset + 8, virtualSize);
        WriteUInt32(bytes, offset + 12, virtualAddress);
        WriteUInt32(bytes, offset + 16, rawSize);
        WriteUInt32(bytes, offset + 20, rawOffset);
        WriteUInt32(bytes, offset + 36, characteristics);
    }

    private static void WriteDataDirectory(
        byte[] bytes,
        int index,
        uint rva,
        uint size)
    {
        int offset = OptionalHeaderOffset + 112 + (index * 8);
        WriteUInt32(bytes, offset, rva);
        WriteUInt32(bytes, offset + 4, size);
    }

    private static void WriteRuntimeFunction(
        byte[] bytes,
        int index,
        uint begin,
        uint end,
        uint unwind)
    {
        int offset = PdataRawOffset + (index * 12);
        WriteUInt32(bytes, offset, begin);
        WriteUInt32(bytes, offset + 4, end);
        WriteUInt32(bytes, offset + 8, unwind);
    }

    private static void WriteMinimalUnwind(byte[] bytes, uint rva)
    {
        int offset = checked(XdataRawOffset + (int)(rva - XdataRva));
        bytes[offset] = 0x01;
        bytes[offset + 1] = 0x00;
        bytes[offset + 2] = 0x00;
        bytes[offset + 3] = 0x00;
    }

    private static LinkEvidencePaths WriteLinkEvidence(
        string artifactPath,
        LinkEvidenceOptions? options = null)
    {
        options ??= new LinkEvidenceOptions();

        string sourceRepositoryRoot = HostToolProcess.FindRepositoryRoot();
        string sourceProjectPath = Path.Combine(
            sourceRepositoryRoot,
            "src",
            "OS",
            "OS.csproj");
        string dotnetHostPath = ResolveDotnetHostPath();
        string dotnetSdkVersion = ResolveDotnetSdkVersion(dotnetHostPath);
        SchemaThreeFixture schemaFixture = EnsureSchemaThreeFixture(
            sourceRepositoryRoot,
            sourceProjectPath,
            dotnetHostPath,
            dotnetSdkVersion);
        string repositoryRoot = schemaFixture.RepositoryRoot;
        string projectPath = Path.Combine(
            repositoryRoot,
            "src",
            "OS",
            "OS.csproj");
        string entrySourcePath = Path.Combine(
            repositoryRoot,
            "src",
            "OS",
            "Boot",
            "EfiEntry.cs");
        string mapPath = artifactPath + ".link.map";
        string responsePath = artifactPath + ".link.rsp";
        string evidencePath = artifactPath + ".link.json";
        string linkerPath = Path.Combine(
            Path.GetDirectoryName(artifactPath)!,
            "trusted-fixture-link.exe");
        string objectPath = Path.Combine(
            Path.GetDirectoryName(artifactPath)!,
            Path.GetFileName(artifactPath) + ".serializer.obj");
        string linkedObjectDirectory = Path.Combine(
            repositoryRoot,
            "src",
            "OS",
            "obj",
            "artifact-verifier-fixtures",
            GetSha256(
                Encoding.UTF8.GetBytes(Path.GetFullPath(artifactPath)))
                .Substring(0, 16));
        Directory.CreateDirectory(linkedObjectDirectory);
        string linkedObjectPath = Path.Combine(
            linkedObjectDirectory,
            "instruction publication serializer.obj");
        string managedAssemblyPath = EnsureManagedAssemblyFixture(repositoryRoot);
        string fixtureExecutableSource = dotnetHostPath;
        string windowsSdkVersion = "10.0.99999.0";
        string windowsSdkLibraryPath = Path.Combine(
            Path.GetDirectoryName(artifactPath)!,
            "Windows Kits",
            "10",
            "Lib",
            windowsSdkVersion,
            "um",
            "x64",
            "kernel32.Lib");
        Directory.CreateDirectory(Path.GetDirectoryName(windowsSdkLibraryPath)!);
        File.WriteAllBytes(
            windowsSdkLibraryPath,
            Encoding.ASCII.GetBytes("fixture Windows SDK library"));
        string ilCompilerPackageVersion = "10.0.8";
        string ilCompilerPath = schemaFixture.IlCompilerPath;
        string ilCompilerFileVersion =
            FileVersionInfo.GetVersionInfo(ilCompilerPath).FileVersion
            ?? throw new InvalidOperationException(
                "Fixture ILCompiler file version is unavailable.");
        string nativeAotRuntimePackVersion = "10.0.8";
        string nativeAotRuntimePackPath = schemaFixture.NativeAotRuntimePackPath;
        string runtimeListPath = Path.Combine(
            nativeAotRuntimePackPath,
            "data",
            "RuntimeList.xml");

        File.Copy(fixtureExecutableSource, linkerPath, overwrite: true);
        string linkerVersion =
            FileVersionInfo.GetVersionInfo(linkerPath).FileVersion
            ?? throw new InvalidOperationException(
                "Fixture linker file version is unavailable.");
        byte[] objectBytes = Convert.FromBase64String(SerializerObjectBase64);
        if (objectBytes.Length != 336 ||
            GetSha256(objectBytes) != SerializerObjectSha256)
        {
            throw new InvalidOperationException("Embedded serializer object is invalid.");
        }

        File.WriteAllBytes(objectPath, objectBytes);
        File.WriteAllBytes(linkedObjectPath, objectBytes);

        ulong mappedVirtualAddress = ImageBase + options.MappedEntryRva;
        uint mapSegmentOffset = options.MappedEntryRva - EntryPointRva;
        var mapLines = new List<string>
        {
            " OS",
            $" Timestamp is {CoffTimestamp:x8} (fixture)",
            $" Preferred load address is {ImageBase:x16}",
            $" 0001:{mapSegmentOffset:x8} EfiMain {mappedVirtualAddress:x16} OS.obj",
            $" 0001:{mapSegmentOffset:x8} OS_OS_Boot_EfiEntry__EfiMain {mappedVirtualAddress:x16} OS.obj",
        };

        string owner = options.MapOwner ?? Path.GetFileName(linkedObjectPath);
        for (int copy = 0; copy < options.SerializerSymbolCopies; copy++)
        {
            mapLines.Add(
                $" 0001:{options.SerializerRva - TextRva:x8} {SerializerSymbol} " +
                $"{ImageBase + options.SerializerRva:x16} f {owner}");
        }

        mapLines.Add($" entry point at 0001:{mapSegmentOffset:x8}");
        mapLines.Add(string.Empty);
        File.WriteAllText(
            mapPath,
            string.Join("\r\n", mapLines),
            new UTF8Encoding(false));
        string linkedMapPath = Path.Combine(linkedObjectDirectory, "OS.link.map");
        File.Copy(mapPath, linkedMapPath, overwrite: true);

        var responseLines = new List<string>
        {
            "/ENTRY:EfiMain",
            "/NODEFAULTLIB"
        };
        for (int copy = 0; copy < options.BreproCopies; copy++)
        {
            responseLines.Add(options.BreproToken);
        }
        for (int copy = 0; copy < options.ForceIncludeCopies; copy++)
        {
            responseLines.Add("/INCLUDE:" + SerializerSymbol);
        }
        for (int copy = 0; copy < options.ObjectTokenCopies; copy++)
        {
            responseLines.Add("\"" + linkedObjectPath + "\"");
        }
        string responseMapPath = options.MapPathOverride ?? linkedMapPath;
        for (int copy = 0; copy < options.MapOptionCopies; copy++)
        {
            responseLines.Add("/MAP:\"" + responseMapPath + "\"");
        }
        for (int copy = 0; copy < options.MapInfoCopies; copy++)
        {
            responseLines.Add("/MAPINFO:EXPORTS");
        }
        if (!string.IsNullOrWhiteSpace(options.AdditionalResponse))
        {
            responseLines.Add(options.AdditionalResponse);
        }
        responseLines.Add(string.Empty);
        File.WriteAllText(
            responsePath,
            string.Join("\r\n", responseLines),
            new UTF8Encoding(false));

        string toolRoot = Path.Combine(
            repositoryRoot,
            "tools",
            "OS.X64TextSerializerGenerator");
        SourceClosureFixture sourceClosure = BuildSourceClosureFixture(
            repositoryRoot,
            projectPath);
        var evidence = new Dictionary<string, object?>
        {
            ["schemaVersion"] = 3,
            ["configuration"] = ManagedFixtureConfiguration,
            ["runtimeIdentifier"] = "win-x64",
            ["artifactSha256"] = GetSha256(artifactPath),
            ["artifactBytes"] = new FileInfo(artifactPath).Length,
            ["linkerMapSha256"] = GetSha256(mapPath),
            ["linkResponseSha256"] = GetSha256(responsePath),
            ["sourceProjectSha256"] = GetSha256(projectPath),
            ["entrySourceSha256"] = GetSha256(entrySourcePath),
            ["solutionSha256"] = GetSha256(
                Path.Combine(repositoryRoot, "CLROS.slnx")),
            ["sourceClosureSha256"] = sourceClosure.Sha256,
            ["sourceClosureInputs"] = sourceClosure.Inputs,
            ["restoreClosureSha256"] = schemaFixture.RestoreClosure.Sha256,
            ["restoreClosureInputs"] = schemaFixture.RestoreClosure.Inputs,
            ["restoreArchiveSourceRootPath"] =
                schemaFixture.RestoreArchiveSourceRootPath,
            ["restoreArchiveClosureSha256"] =
                schemaFixture.RestoreArchiveClosure.Sha256,
            ["restoreArchiveInputs"] =
                schemaFixture.RestoreArchiveClosure.Inputs,
            ["pinnedRestoreSourcePath"] =
                schemaFixture.PinnedRestoreSourcePath,
            ["pinnedRestoreSourceSha256"] =
                schemaFixture.PinnedRestoreSourceClosure.Sha256,
            ["pinnedRestoreSourceInputs"] =
                schemaFixture.PinnedRestoreSourceClosure.Inputs,
            ["restoreImportProjectExtensionProps"] = false,
            ["restoreImportProjectExtensionTargets"] = false,
            ["restoreExcludeRestorePackageImports"] = true,
            ["publishImportProjectExtensionProps"] = false,
            ["publishImportProjectExtensionTargets"] = false,
            ["publishExcludeRestorePackageImports"] = true,
            ["projectExtensionsPath"] = schemaFixture.ProjectExtensionsPath,
            ["projectAssetsPath"] = schemaFixture.ProjectAssetsPath,
            ["projectAssetsSha256"] = GetSha256(schemaFixture.ProjectAssetsPath),
            ["packageRootPath"] = schemaFixture.PackageRootPath,
            ["packageRootKind"] = "isolated",
            ["packageClosureSha256"] =
                schemaFixture.PackageClosureSha256,
            ["packages"] = schemaFixture.Packages,
            ["packageBuildClosureSha256"] =
                schemaFixture.PackageBuildClosure.Sha256,
            ["packageBuildClosureInputs"] =
                schemaFixture.PackageBuildClosure.Inputs,
            ["activePackageImportEdges"] =
                schemaFixture.ActivePackageImportEdges,
            ["activePackageImportGraphSha256"] =
                schemaFixture.ActivePackageImportGraphSha256,
            ["packageTaskBindings"] =
                schemaFixture.PackageTaskBindings,
            ["packageTaskBindingsSha256"] =
                schemaFixture.PackageTaskBindingsSha256,
            ["postRestoreEvaluation"] =
                schemaFixture.PostRestoreEvaluation,
            ["entryPointSymbol"] = "EfiMain",
            ["managedEntrySymbol"] = "OS_OS_Boot_EfiEntry__EfiMain",
            ["linkerPath"] = linkerPath,
            ["linkerVersion"] = linkerVersion,
            ["linkerSha256"] = GetSha256(linkerPath),
            ["serializerSymbol"] = SerializerSymbol,
            ["serializerExpectedSuccessCookie"] = SerializerExpectedSuccessCookie,
            ["serializerObjectPath"] = objectPath,
            ["serializerLinkedObjectPath"] = linkedObjectPath,
            ["serializerObjectBytes"] = objectBytes.Length,
            ["serializerObjectSha256"] = GetSha256(objectPath),
            ["serializerTargetsSha256"] = GetSha256(
                Path.Combine(
                    toolRoot,
                    "OS.X64TextSerializerGenerator.targets")),
            ["serializerGeneratorProjectSha256"] = GetSha256(
                Path.Combine(
                    toolRoot,
                    "OS.X64TextSerializerGenerator.csproj")),
            ["serializerGeneratorProgramSha256"] = GetSha256(
                Path.Combine(toolRoot, "Program.cs")),
            ["serializerGeneratorWriterSha256"] = GetSha256(
                Path.Combine(toolRoot, "Amd64CoffWriter.cs")),
            ["serializerGeneratorSourceSha256"] = GetSha256(
                Path.Combine(
                    toolRoot,
                    "InstructionPublicationSerializerGenerator.cs")),
            ["instructionPublicationSourceSha256"] = GetSha256(
                Path.Combine(
                    repositoryRoot,
                    "src",
                    "OS",
                    "Boot",
                    "X64InstructionPublication.cs")),
            ["runtimeImportAttributeSourceSha256"] = GetSha256(
                Path.Combine(
                    repositoryRoot,
                    "src",
                    "OS",
                    "Runtime",
                    "RuntimeImportAttribute.cs")),
            ["methodImplAttributeSourceSha256"] = GetSha256(
                Path.Combine(
                    repositoryRoot,
                    "src",
                    "OS",
                    "Runtime",
                    "MethodImplAttribute.cs")),
            ["managedAssemblyPath"] = managedAssemblyPath,
            ["managedAssemblySha256"] = GetSha256(managedAssemblyPath),
            ["dotnetHostPath"] = dotnetHostPath,
            ["dotnetHostSha256"] = GetSha256(dotnetHostPath),
            ["dotnetSdkVersion"] = dotnetSdkVersion,
            ["windowsSdkLibraryPath"] = windowsSdkLibraryPath,
            ["windowsSdkVersion"] = windowsSdkVersion,
            ["windowsSdkLibrarySha256"] = GetSha256(windowsSdkLibraryPath),
            ["ilCompilerPath"] = ilCompilerPath,
            ["ilCompilerDiscoverySourcePath"] =
                schemaFixture.IlCompilerDiscoverySourcePath,
            ["ilCompilerPackageVersion"] = ilCompilerPackageVersion,
            ["ilCompilerFileVersion"] = ilCompilerFileVersion,
            ["ilCompilerSha256"] = GetSha256(ilCompilerPath),
            ["nativeAotRuntimePackPath"] = nativeAotRuntimePackPath,
            ["nativeAotRuntimePackVersion"] = nativeAotRuntimePackVersion,
            ["nativeAotRuntimePackSha256"] =
                schemaFixture.NativeAotRuntimePackSha256,
            ["nativeAotRuntimePackFileCount"] =
                schemaFixture.NativeAotRuntimePackFileCount,
            ["nativeAotRuntimePackBytes"] =
                schemaFixture.NativeAotRuntimePackBytes,
            ["ilCompilerTargetsPath"] =
                schemaFixture.IlCompilerTargetsPath,
            ["ilLinkTargetsPath"] = schemaFixture.IlLinkTargetsPath,
            ["ilLinkTasksAssemblyPath"] =
                schemaFixture.IlLinkTasksAssemblyPath,
            ["ilcBuildTasksPath"] = schemaFixture.IlcBuildTasksPath,
            ["ilcHostPackagePath"] =
                schemaFixture.IlcHostPackagePath,
            ["ilcToolsPath"] = schemaFixture.IlcToolsPath,
            ["ilcFrameworkPath"] = schemaFixture.IlcFrameworkPath,
            ["ilcFrameworkNativePath"] =
                schemaFixture.IlcFrameworkNativePath,
            ["ilcSdkPath"] = schemaFixture.IlcSdkPath,
            ["ilcMibcPath"] = schemaFixture.IlcMibcPath,
            ["languageTargetsPath"] = schemaFixture.LanguageTargetsPath,
            ["csharpTargetsPath"] = schemaFixture.CSharpTargetsPath,
            ["commonTargetsPath"] = schemaFixture.CommonTargetsPath,
            ["runtimeListSha256"] = GetSha256(runtimeListPath),
            ["generatedUtc"] = DateTimeOffset.UtcNow.ToString(
                "O",
                CultureInfo.InvariantCulture)
        };
        File.WriteAllText(
            evidencePath,
            JsonSerializer.Serialize(evidence),
            new UTF8Encoding(false));

        return new LinkEvidencePaths(
            mapPath,
            responsePath,
            evidencePath,
            objectPath,
            linkedObjectPath,
            linkedMapPath,
            managedAssemblyPath);
    }

    private static string EnsureManagedAssemblyFixture(string repositoryRoot)
    {
        string configurationRoot = Path.GetFullPath(
            Path.Combine(
                repositoryRoot,
                "src",
                "OS",
                "bin",
                ManagedFixtureConfiguration));
        string managedAssemblyPath = Path.Combine(
            configurationRoot,
            "net10.0",
            "win-x64",
            "OS.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(managedAssemblyPath)!);
        if (!File.Exists(managedAssemblyPath))
        {
            File.Copy(
                typeof(EfiArtifactVerificationScriptTests).Assembly.Location,
                managedAssemblyPath);
        }

        return managedAssemblyPath;
    }

    private static SchemaThreeFixture EnsureSchemaThreeFixture(
        string sourceRepositoryRoot,
        string sourceProjectPath,
        string dotnetHostPath,
        string dotnetSdkVersion)
    {
        lock (SchemaThreeFixtureLock)
        {
            if (cachedSchemaThreeFixture is not null)
            {
                return cachedSchemaThreeFixture;
            }

            string repositoryRoot = Path.GetFullPath(
                SchemaThreeFixtureRepositoryRoot);
            if (Directory.Exists(repositoryRoot))
            {
                Directory.Delete(repositoryRoot, recursive: true);
            }

            CopySchemaThreeFixtureRepository(
                sourceRepositoryRoot,
                sourceProjectPath,
                repositoryRoot);
            string projectPath = Path.Combine(
                repositoryRoot,
                "src",
                "OS",
                "OS.csproj");
            string configurationRoot = Path.Combine(
                repositoryRoot,
                "src",
                "OS",
                "obj",
                ManagedFixtureConfiguration);
            if (Directory.Exists(configurationRoot))
            {
                Directory.Delete(configurationRoot, recursive: true);
            }

            string managedIntermediatePath = Path.Combine(
                configurationRoot,
                "net10.0",
                "win-x64");
            string projectExtensionsPath = Path.Combine(
                managedIntermediatePath,
                "nuget");
            string pinnedRestoreSourcePath = Path.Combine(
                managedIntermediatePath,
                "restore-source");
            string packageRootPath = Path.Combine(
                managedIntermediatePath,
                "packages");
            Directory.CreateDirectory(pinnedRestoreSourcePath);

            string globalPackageRoot = ResolveGlobalPackageRoot();
            foreach (RestoreArchiveDescriptor archive in ExpectedRestoreArchives)
            {
                string sourcePath = Path.Combine(
                    globalPackageRoot,
                    archive.Id,
                    archive.Version,
                    archive.FileName);
                if (!File.Exists(sourcePath) ||
                    !string.Equals(
                        GetSha256(sourcePath),
                        archive.Sha256,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Pinned restore archive is unavailable or changed: " +
                        archive.Id +
                        "/" +
                        archive.Version);
                }

                File.Copy(
                    sourcePath,
                    Path.Combine(pinnedRestoreSourcePath, archive.FileName),
                    overwrite: false);
            }

            string restoreConfigPath = Path.Combine(
                pinnedRestoreSourcePath,
                "NuGet.Config");
            string escapedRestoreSource = System.Security.SecurityElement.Escape(
                pinnedRestoreSourcePath)
                ?? throw new InvalidOperationException(
                    "Pinned restore source path could not be XML-escaped.");
            string restoreConfig =
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
                "<configuration>\r\n" +
                "  <packageSources>\r\n" +
                "    <clear />\r\n" +
                "    <add key=\"CLROS_PINNED\" value=\"" +
                escapedRestoreSource +
                "\" />\r\n" +
                "  </packageSources>\r\n" +
                "  <fallbackPackageFolders>\r\n" +
                "    <clear />\r\n" +
                "  </fallbackPackageFolders>\r\n" +
                "  <auditSources>\r\n" +
                "    <clear />\r\n" +
                "  </auditSources>\r\n" +
                "</configuration>\r\n";
            File.WriteAllText(
                restoreConfigPath,
                restoreConfig,
                new UTF8Encoding(false));

            RunCanonicalFixtureRestore(
                repositoryRoot,
                projectPath,
                dotnetHostPath,
                dotnetSdkVersion,
                restoreConfigPath,
                projectExtensionsPath,
                packageRootPath);

            string projectAssetsPath = Path.Combine(
                projectExtensionsPath,
                "project.assets.json");
            string nativeAotRuntimePackPath = Path.Combine(
                globalPackageRoot,
                "microsoft.netcore.app.runtime.nativeaot.win-x64",
                "10.0.8");
            string ilCompilerPath = Path.Combine(
                packageRootPath,
                "runtime.win-x64.microsoft.dotnet.ilcompiler",
                "10.0.8",
                "tools",
                "ilc.exe");
            string ilCompilerDiscoverySourcePath = Path.Combine(
                globalPackageRoot,
                "runtime.win-x64.microsoft.dotnet.ilcompiler",
                "10.0.8",
                "tools",
                "ilc.exe");
            string ilCompilerBuildPackageRoot = Path.Combine(
                packageRootPath,
                "microsoft.dotnet.ilcompiler",
                "10.0.8");
            string ilLinkPackageRoot = Path.Combine(
                packageRootPath,
                "microsoft.net.illink.tasks",
                "10.0.10");
            string ilCompilerHostPackageRoot = Path.Combine(
                packageRootPath,
                "runtime.win-x64.microsoft.dotnet.ilcompiler",
                "10.0.8");
            string dotnetSdkDirectory = Path.Combine(
                Path.GetDirectoryName(dotnetHostPath)!,
                "sdk",
                dotnetSdkVersion);
            string trailingSeparator =
                Path.DirectorySeparatorChar.ToString(CultureInfo.InvariantCulture);

            SourceClosureFixture restoreClosure = BuildExactFileClosure(
                repositoryRoot,
                [
                    Path.Combine(
                        projectExtensionsPath,
                        "OS.csproj.nuget.dgspec.json"),
                    Path.Combine(
                        projectExtensionsPath,
                        "OS.csproj.nuget.g.props"),
                    Path.Combine(
                        projectExtensionsPath,
                        "OS.csproj.nuget.g.targets"),
                    projectAssetsPath,
                    Path.Combine(
                        projectExtensionsPath,
                        "project.nuget.cache")
                ]);
            SourceClosureFixture pinnedRestoreSourceClosure =
                BuildExactFileClosure(
                    repositoryRoot,
                    Directory.EnumerateFiles(
                            pinnedRestoreSourcePath,
                            "*",
                            SearchOption.TopDirectoryOnly)
                        .ToArray());
            SourceClosureFixture restoreArchiveClosure =
                BuildRootRelativeFileClosure(
                    globalPackageRoot,
                    ExpectedRestoreArchives
                        .Select(archive => Path.Combine(
                            globalPackageRoot,
                            archive.Id,
                            archive.Version,
                            archive.FileName))
                        .ToArray(),
                    sortByPath: false);

            var packageEvidence = new Dictionary<string, object?>[
                ExpectedPackages.Length];
            var packageClosure = new StringBuilder();
            PackageTreeFixture? runtimePackage = null;
            for (int index = 0; index < ExpectedPackages.Length; index++)
            {
                PackageDescriptor package = ExpectedPackages[index];
                string packageRoot = string.Equals(
                    package.RootKind,
                    "runtime",
                    StringComparison.Ordinal)
                    ? nativeAotRuntimePackPath
                    : Path.Combine(
                        packageRootPath,
                        package.Id,
                        package.Version);
                PackageTreeFixture tree = BuildPackageTreeFixture(packageRoot);
                if (tree.FileCount != package.FileCount ||
                    tree.TotalBytes != package.TotalBytes ||
                    !string.Equals(
                        tree.AggregateSha256,
                        package.AggregateSha256,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Canonical package tree changed: " +
                        package.Id +
                        "/" +
                        package.Version);
                }

                string metadataPath = Path.Combine(
                    packageRoot,
                    ".nupkg.metadata");
                if (!File.Exists(metadataPath))
                {
                    throw new InvalidOperationException(
                        "Canonical package metadata is missing: " +
                        package.Id +
                        "/" +
                        package.Version);
                }

                packageEvidence[index] = new Dictionary<string, object?>
                {
                    ["id"] = package.Id,
                    ["version"] = package.Version,
                    ["rootKind"] = package.RootKind,
                    ["rootPath"] = packageRoot,
                    ["contentHashSha512"] = package.ContentHashSha512,
                    ["fileCount"] = tree.FileCount,
                    ["totalBytes"] = tree.TotalBytes,
                    ["aggregateSha256"] = tree.AggregateSha256,
                    ["metadataSha256"] = GetSha256(metadataPath),
                    ["inputs"] = tree.Inputs
                };
                packageClosure.Append(package.Id);
                packageClosure.Append('|');
                packageClosure.Append(package.Version);
                packageClosure.Append('|');
                packageClosure.Append(package.RootKind);
                packageClosure.Append('|');
                packageClosure.Append(tree.AggregateSha256);
                packageClosure.Append('\n');
                if (string.Equals(
                    package.RootKind,
                    "runtime",
                    StringComparison.Ordinal))
                {
                    runtimePackage = tree;
                }
            }

            SourceClosureFixture packageBuildClosure =
                BuildRootRelativeFileClosure(
                    packageRootPath,
                    PackageBuildClosurePaths
                        .Select(relativePath => Path.Combine(
                            packageRootPath,
                            relativePath.Replace(
                                '/',
                                Path.DirectorySeparatorChar)))
                        .ToArray(),
                    sortByPath: false);
            Dictionary<string, object?> postRestoreEvaluation =
                BuildPostRestoreEvaluationFixture(
                    packageRootPath,
                    nativeAotRuntimePackPath,
                    dotnetSdkDirectory,
                    projectExtensionsPath);
            PackageTreeFixture runtime = runtimePackage
                ?? throw new InvalidOperationException(
                    "Runtime package evidence was not materialized.");

            cachedSchemaThreeFixture = new SchemaThreeFixture(
                RepositoryRoot: repositoryRoot,
                ProjectExtensionsPath: projectExtensionsPath,
                ProjectAssetsPath: projectAssetsPath,
                PinnedRestoreSourcePath: pinnedRestoreSourcePath,
                PackageRootPath: packageRootPath,
                RestoreArchiveSourceRootPath: globalPackageRoot,
                RestoreClosure: restoreClosure,
                PinnedRestoreSourceClosure: pinnedRestoreSourceClosure,
                RestoreArchiveClosure: restoreArchiveClosure,
                PackageClosureSha256: GetSha256(
                    Encoding.UTF8.GetBytes(packageClosure.ToString())),
                Packages: packageEvidence,
                PackageBuildClosure: packageBuildClosure,
                ActivePackageImportEdges: ActivePackageImportEdges,
                ActivePackageImportGraphSha256: GetSequenceSha256(
                    ActivePackageImportEdges),
                PackageTaskBindings: PackageTaskBindings,
                PackageTaskBindingsSha256: GetSequenceSha256(
                    PackageTaskBindings),
                PostRestoreEvaluation: postRestoreEvaluation,
                IlCompilerPath: ilCompilerPath,
                IlCompilerDiscoverySourcePath:
                    ilCompilerDiscoverySourcePath,
                NativeAotRuntimePackPath: nativeAotRuntimePackPath,
                NativeAotRuntimePackSha256: runtime.AggregateSha256,
                NativeAotRuntimePackFileCount: runtime.FileCount,
                NativeAotRuntimePackBytes: runtime.TotalBytes,
                IlCompilerTargetsPath: Path.Combine(
                    ilCompilerBuildPackageRoot,
                    "build",
                    "Microsoft.DotNet.ILCompiler.SingleEntry.targets"),
                IlLinkTargetsPath: Path.Combine(
                    ilLinkPackageRoot,
                    "build",
                    "Microsoft.NET.ILLink.targets"),
                IlLinkTasksAssemblyPath: Path.Combine(
                    ilLinkPackageRoot,
                    "tools",
                    "net",
                    "ILLink.Tasks.dll"),
                IlcBuildTasksPath: Path.Combine(
                    ilCompilerBuildPackageRoot,
                    "tools",
                    "netstandard",
                    "ILCompiler.Build.Tasks.dll"),
                IlcHostPackagePath: ilCompilerHostPackageRoot,
                IlcToolsPath: Path.Combine(
                    ilCompilerHostPackageRoot,
                    "tools") + trailingSeparator,
                IlcFrameworkPath: Path.Combine(
                    nativeAotRuntimePackPath,
                    "runtimes",
                    "win-x64",
                    "lib") + trailingSeparator,
                IlcFrameworkNativePath: Path.Combine(
                    nativeAotRuntimePackPath,
                    "runtimes",
                    "win-x64",
                    "native") + trailingSeparator,
                IlcSdkPath: Path.Combine(
                    nativeAotRuntimePackPath,
                    "runtimes",
                    "win-x64",
                    "native") + trailingSeparator,
                IlcMibcPath: Path.Combine(
                    nativeAotRuntimePackPath,
                    "mibc") + trailingSeparator,
                LanguageTargetsPath: Path.Combine(
                    dotnetSdkDirectory,
                    "Microsoft.CSharp.targets"),
                CSharpTargetsPath: Path.Combine(
                    dotnetSdkDirectory,
                    "Microsoft.CSharp.CurrentVersion.targets"),
                CommonTargetsPath: Path.Combine(
                    dotnetSdkDirectory,
                    "Microsoft.Common.CurrentVersion.targets"));
            return cachedSchemaThreeFixture;
        }
    }

    private static void CopySchemaThreeFixtureRepository(
        string sourceRepositoryRoot,
        string sourceProjectPath,
        string destinationRepositoryRoot)
    {
        string normalizedSourceRoot = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(sourceRepositoryRoot));
        string sourcePrefix =
            normalizedSourceRoot + Path.DirectorySeparatorChar;
        string sourceProjectDirectory = Path.GetDirectoryName(
            sourceProjectPath)!;
        var sourcePaths = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        XDocument project = XDocument.Load(sourceProjectPath);
        foreach (XElement compile in project.Descendants("Compile"))
        {
            string include = compile.Attribute("Include")?.Value
                ?? throw new InvalidOperationException(
                    "Fixture project compile input has no Include.");
            sourcePaths.Add(
                Path.GetFullPath(
                    Path.Combine(sourceProjectDirectory, include)));
        }

        string[] explicitRelativePaths =
        [
            "CLROS.slnx",
            "global.json",
            "Directory.Build.props",
            "Directory.Build.targets",
            "src/OS/OS.csproj",
            "scripts/build-efi-shell.ps1",
            "scripts/test-efi-artifact.ps1",
            ToolDirectoryBuildPropsPath,
            "tools/OS.X64TextSerializerGenerator/OS.X64TextSerializerGenerator.targets",
            "tools/OS.X64TextSerializerGenerator/OS.X64TextSerializerGenerator.csproj",
            "tools/OS.X64TextSerializerGenerator/Program.cs",
            "tools/OS.X64TextSerializerGenerator/Amd64CoffWriter.cs",
            "tools/OS.X64TextSerializerGenerator/InstructionPublicationSerializerGenerator.cs",
            "tools/OS.X64TextSerializerGenerator/CheckedReferenceBarrierThunkGenerator.cs"
        ];
        foreach (string relativePath in explicitRelativePaths)
        {
            sourcePaths.Add(
                Path.GetFullPath(
                    Path.Combine(normalizedSourceRoot, relativePath)));
        }

        foreach (string sourcePath in sourcePaths)
        {
            if (!sourcePath.StartsWith(
                    sourcePrefix,
                    StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(sourcePath))
            {
                throw new InvalidOperationException(
                    "Schema-three fixture source is unavailable or external: " +
                    sourcePath);
            }

            string relativePath = Path.GetRelativePath(
                normalizedSourceRoot,
                sourcePath);
            string destinationPath = Path.Combine(
                destinationRepositoryRoot,
                relativePath);
            Directory.CreateDirectory(
                Path.GetDirectoryName(destinationPath)!);
            File.Copy(sourcePath, destinationPath, overwrite: false);
        }
    }

    private static void RunCanonicalFixtureRestore(
        string repositoryRoot,
        string projectPath,
        string dotnetHostPath,
        string dotnetSdkVersion,
        string restoreConfigPath,
        string projectExtensionsPath,
        string packageRootPath)
    {
        string canonicalPropsPath = Path.Combine(
            repositoryRoot,
            "Directory.Build.props");
        string canonicalTargetsPath = Path.Combine(
            repositoryRoot,
            "Directory.Build.targets");
        string dotnetSdkDirectory = Path.Combine(
            Path.GetDirectoryName(dotnetHostPath)!,
            "sdk",
            dotnetSdkVersion);
        string disabledUserExtensionsPath = Path.Combine(
            projectExtensionsPath,
            "disabled-user-extensions");
        if (Directory.Exists(disabledUserExtensionsPath) ||
            File.Exists(disabledUserExtensionsPath))
        {
            throw new InvalidOperationException(
                "Fixture user-extension quarantine path must be absent.");
        }

        string projectExtensionsArgument =
            projectExtensionsPath.Replace('\\', '/').TrimEnd('/') + "/";
        string projectAssetsArgument = Path.Combine(
            projectExtensionsPath,
            "project.assets.json").Replace('\\', '/');
        string[] arguments =
        [
            "restore",
            projectPath,
            "-noAutoResponse",
            "-r",
            "win-x64",
            "--nologo",
            "--force",
            "--no-cache",
            "-nodeReuse:false",
            "--configfile",
            restoreConfigPath,
            "--packages",
            packageRootPath,
            "-p:Configuration=" + ManagedFixtureConfiguration,
            "-p:DirectoryBuildPropsPath=" + canonicalPropsPath,
            "-p:DirectoryBuildTargetsPath=" + canonicalTargetsPath,
            "-p:ImportDirectoryBuildProps=true",
            "-p:ImportDirectoryBuildTargets=true",
            "-p:CustomBeforeMicrosoftCommonProps=",
            "-p:CustomAfterMicrosoftCommonProps=",
            "-p:CustomBeforeDirectoryBuildProps=",
            "-p:CustomAfterDirectoryBuildProps=",
            "-p:CustomBeforeDirectoryBuildTargets=",
            "-p:CustomAfterDirectoryBuildTargets=",
            "-p:CustomBeforeMicrosoftCommonTargets=",
            "-p:CustomAfterMicrosoftCommonTargets=",
            "-p:CustomBeforeMicrosoftCSharpTargets=",
            "-p:CustomAfterMicrosoftCSharpTargets=",
            "-p:CustomBeforeMicrosoftCommonCrossTargetingTargets=",
            "-p:CustomAfterMicrosoftCommonCrossTargetingTargets=",
            "-p:BeforeMicrosoftNETSdkTargets=",
            "-p:AfterMicrosoftNETSdkTargets=",
            "-p:MSBuildSDKsPath=" +
                Path.Combine(dotnetSdkDirectory, "Sdks"),
            "-p:MSBuildExtensionsPath=" +
                dotnetSdkDirectory +
                Path.DirectorySeparatorChar,
            "-p:MSBuildUserExtensionsPath=" +
                disabledUserExtensionsPath,
            "-p:UseSharedCompilation=false",
            "-p:ImportByWildcardBeforeMicrosoftCommonProps=false",
            "-p:ImportByWildcardAfterMicrosoftCommonProps=false",
            "-p:ImportUserLocationsByWildcardBeforeMicrosoftCommonProps=false",
            "-p:ImportUserLocationsByWildcardAfterMicrosoftCommonProps=false",
            "-p:ImportByWildcardBeforeMicrosoftCommonTargets=false",
            "-p:ImportByWildcardAfterMicrosoftCommonTargets=false",
            "-p:ImportUserLocationsByWildcardBeforeMicrosoftCommonTargets=false",
            "-p:ImportUserLocationsByWildcardAfterMicrosoftCommonTargets=false",
            "-p:ImportByWildcardBeforeMicrosoftCSharpTargets=false",
            "-p:ImportByWildcardAfterMicrosoftCSharpTargets=false",
            "-p:ImportUserLocationsByWildcardBeforeMicrosoftCSharpTargets=false",
            "-p:ImportUserLocationsByWildcardAfterMicrosoftCSharpTargets=false",
            "-p:ImportByWildcardBeforeMicrosoftCommonCrossTargetingTargets=false",
            "-p:ImportByWildcardAfterMicrosoftCommonCrossTargetingTargets=false",
            "-p:RestoreForceEvaluate=true",
            "-p:RestoreUseCustomAfterTargets=false",
            "-p:RestoreIgnoreFailedSources=false",
            "-p:MSBuildProjectExtensionsPath=" +
                projectExtensionsArgument,
            "-p:RestoreOutputPath=" + projectExtensionsArgument,
            "-p:ProjectAssetsFile=" + projectAssetsArgument,
            "-p:RestorePackagesPath=" + packageRootPath,
            "-p:NuGetPackageRoot=" + packageRootPath,
            "-p:ImportProjectExtensionProps=false",
            "-p:ImportProjectExtensionTargets=false",
            "-p:ExcludeRestorePackageImports=true"
        ];
        ProcessResult result = RunBoundedProcess(
            dotnetHostPath,
            arguments,
            timeoutMilliseconds: 120_000);
        if (result.TimedOut || result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                "Canonical isolated fixture restore failed: " +
                result.Output.Replace(
                    Environment.NewLine,
                    " ",
                    StringComparison.Ordinal));
        }
    }

    private static string ResolveGlobalPackageRoot()
    {
        string? configuredRoot = Environment.GetEnvironmentVariable(
            "NUGET_PACKAGES");
        if (!string.IsNullOrWhiteSpace(configuredRoot))
        {
            return Path.GetFullPath(configuredRoot);
        }

        return Path.GetFullPath(
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.UserProfile),
                ".nuget",
                "packages"));
    }

    private static SourceClosureFixture BuildExactFileClosure(
        string repositoryRoot,
        string[] fullPaths)
    {
        return BuildRootRelativeFileClosure(repositoryRoot, fullPaths);
    }

    private static SourceClosureFixture BuildRootRelativeFileClosure(
        string rootPath,
        string[] fullPaths,
        bool sortByPath = true)
    {
        string normalizedRoot = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(rootPath));
        string rootPrefix = normalizedRoot + Path.DirectorySeparatorChar;
        IEnumerable<Dictionary<string, string>> inputSequence = fullPaths
            .Select(Path.GetFullPath)
            .Select(fullPath =>
            {
                if (!fullPath.StartsWith(
                        rootPrefix,
                        StringComparison.OrdinalIgnoreCase) ||
                    !File.Exists(fullPath))
                {
                    throw new InvalidOperationException(
                        "Fixture closure input is not a file under its root: " +
                        fullPath);
                }

                return new Dictionary<string, string>
                {
                    ["path"] = Path.GetRelativePath(normalizedRoot, fullPath)
                        .Replace('\\', '/'),
                    ["sha256"] = GetSha256(fullPath)
                };
            });
        if (sortByPath)
        {
            inputSequence = inputSequence.OrderBy(
                input => input["path"],
                StringComparer.Ordinal);
        }

        Dictionary<string, string>[] inputs = inputSequence.ToArray();
        var aggregate = new StringBuilder();
        foreach (Dictionary<string, string> input in inputs)
        {
            aggregate.Append(input["path"]);
            aggregate.Append('|');
            aggregate.Append(input["sha256"]);
            aggregate.Append('\n');
        }

        return new SourceClosureFixture(
            GetSha256(Encoding.UTF8.GetBytes(aggregate.ToString())),
            inputs);
    }

    private static PackageTreeFixture BuildPackageTreeFixture(
        string packageRootPath)
    {
        string normalizedRoot = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(packageRootPath));
        string rootPrefix = normalizedRoot + Path.DirectorySeparatorChar;
        PackageInputFixture[] inputs = Directory
            .EnumerateFiles(
                normalizedRoot,
                "*",
                SearchOption.AllDirectories)
            .Where(path => !string.Equals(
                Path.GetFileName(path),
                ".nupkg.metadata",
                StringComparison.Ordinal))
            .Select(path =>
            {
                string fullPath = Path.GetFullPath(path);
                return new PackageInputFixture(
                    Path: fullPath.Substring(rootPrefix.Length)
                        .Replace('\\', '/'),
                    Bytes: new FileInfo(fullPath).Length,
                    Sha256: GetSha256(fullPath));
            })
            .OrderBy(input => input.Path, StringComparer.Ordinal)
            .ToArray();
        var aggregate = new StringBuilder();
        long totalBytes = 0;
        var serializedInputs = new Dictionary<string, object?>[inputs.Length];
        for (int index = 0; index < inputs.Length; index++)
        {
            PackageInputFixture input = inputs[index];
            totalBytes = checked(totalBytes + input.Bytes);
            aggregate.Append(input.Path);
            aggregate.Append('|');
            aggregate.Append(
                input.Bytes.ToString(CultureInfo.InvariantCulture));
            aggregate.Append('|');
            aggregate.Append(input.Sha256);
            aggregate.Append('\n');
            serializedInputs[index] = new Dictionary<string, object?>
            {
                ["path"] = input.Path,
                ["bytes"] = input.Bytes,
                ["sha256"] = input.Sha256
            };
        }

        return new PackageTreeFixture(
            FileCount: inputs.Length,
            TotalBytes: totalBytes,
            AggregateSha256: GetSha256(
                Encoding.UTF8.GetBytes(aggregate.ToString())),
            Inputs: serializedInputs);
    }

    private static Dictionary<string, object?>
        BuildPostRestoreEvaluationFixture(
            string packageRootPath,
            string nativeAotRuntimePackPath,
            string dotnetSdkDirectory,
            string projectExtensionsPath)
    {
        string ilCompilerBuildPackageRoot = Path.Combine(
            packageRootPath,
            "microsoft.dotnet.ilcompiler",
            "10.0.8");
        string ilCompilerHostPackageRoot = Path.Combine(
            packageRootPath,
            "runtime.win-x64.microsoft.dotnet.ilcompiler",
            "10.0.8");
        string ilLinkPackageRoot = Path.Combine(
            packageRootPath,
            "microsoft.net.illink.tasks",
            "10.0.10");
        string trailingSeparator =
            Path.DirectorySeparatorChar.ToString(CultureInfo.InvariantCulture);
        var properties = new Dictionary<string, string>
        {
            ["_targetOS"] = "win",
            ["_targetArchitecture"] = "x64",
            ["RuntimeIdentifier"] = "win-x64",
            ["TargetFramework"] = "net10.0",
            ["PublishAot"] = "true",
            ["SelfContained"] = "true",
            ["PublishTrimmed"] = "true",
            ["EnableM02OwnedCom1ReadyProbe"] = "false",
            ["EnableM02PanicSafeOwnedCom1Probe"] = "false",
            ["EnableM02OwnedX64GdtEncodingProbe"] = "false",
            ["EnableM02GuardedIstStackAllocationProbe"] = "false",
            ["EnableM02OwnedGdtTssTerminalProbe"] = "false",
            ["EnableM02NormalizedInterruptFramePageProbe"] = "false",
            ["EnableM02OwnedIdtGatePopulationProbe"] = "false",
            ["EnableM02IdtrContinuedExecutionProbe"] = "false",
            ["EnableM02Ud2VectorStateProbe"] = "false",
            ["EnableM02PageFaultCr2ErrorCodeProbe"] = "false",
            ["EnableM02DoubleFaultIstProbe"] = "false",
            ["EnableM02UnexpectedVectorPanicProbe"] = "false",
            ["EnableM04RhpNewFastProbe"] = "false",
            ["EnableM04RhpNewFastBeforeBindFailureProbe"] = "false",
            ["EnableM04RhpNewFastExhaustionFailureProbe"] = "false",
            ["EnableM04SzArrayAllocationProbe"] = "false",
            ["EnableM04SzArrayAllocationBeforeBindProbe"] = "false",
            ["EnableM04SzArrayAllocationExhaustionProbe"] = "false",
            ["EnableM04StringAllocationProbe"] = "false",
            ["EnableM04StringAllocationBeforeBindProbe"] = "false",
            ["EnableM04StringAllocationExhaustionProbe"] = "false",
            ["EnableM04StringAllocationInvalidLengthProbe"] = "false",
            ["EnableM03MemoryMapCaptureProbe"] = "false",
            ["EnableM03FirmwareEntropySeedProbe"] = "false",
            ["EnableM04TypeManagerModuleRegistrationProbe"] = "false",
            ["EnableM03TypedExitBootServicesCallProbe"] = "false",
            ["EnableM04GcStaticRootMaterializationProbe"] = "false",
            ["EnableM04CheckedReferenceBarrierProbe"] = "false",
            ["EnableM04IndependentEndianConformanceProbe"] = "false",
            ["EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe"] =
                "false",
            ["EnableM04ArithmeticConformanceProbe"] =
                "false",
            ["EnableM04LayoutContractProbe"] =
                "false",
            ["EnableM04ManagedHeapPageRangeProbe"] =
                "false",
            ["EnableM01LoadedImageStackProbe"] = "false",
            ["EnableM01LiveAllocatorProbe"] = "false",
            ["EnableM01OwnedPagingProbe"] = "false",
            ["EnableM02SafeStubProbe"] = "false",
            ["EnableM02CheckedPortIoBindingProbe"] = "false",
            ["EnableM02Com1OwnedPortIoProbe"] = "false",
            ["EnableUefiGoneSimulationProbe"] = "false",
            ["EnableExitBootServicesFreshMapProbe"] = "false",
            ["EnableAcpiPhysicalDiscoveryProbe"] = "false",
            ["ImportProjectExtensionProps"] = "false",
            ["ImportProjectExtensionTargets"] = "false",
            ["ExcludeRestorePackageImports"] = "true",
            ["ILCompilerTargetsPath"] = Path.Combine(
                ilCompilerBuildPackageRoot,
                "build",
                "Microsoft.DotNet.ILCompiler.SingleEntry.targets"),
            ["ILLinkTargetsPath"] = Path.Combine(
                ilLinkPackageRoot,
                "build",
                "Microsoft.NET.ILLink.targets"),
            ["ILLinkTasksAssembly"] = Path.Combine(
                ilLinkPackageRoot,
                "tools",
                "net",
                "ILLink.Tasks.dll"),
            ["IlcBuildTasksPath"] = Path.Combine(
                ilCompilerBuildPackageRoot,
                "tools",
                "netstandard",
                "ILCompiler.Build.Tasks.dll"),
            ["IlcHostPackagePath"] = ilCompilerHostPackageRoot,
            ["ClrosIlcHostPackagePath"] = ilCompilerHostPackageRoot,
            ["IlcToolsPath"] = Path.Combine(
                ilCompilerHostPackageRoot,
                "tools") + trailingSeparator,
            ["RuntimePackagePath"] = nativeAotRuntimePackPath,
            ["ClrosRuntimePackagePath"] = nativeAotRuntimePackPath,
            ["IlcFrameworkPath"] = Path.Combine(
                nativeAotRuntimePackPath,
                "runtimes",
                "win-x64",
                "lib") + trailingSeparator,
            ["IlcFrameworkNativePath"] = Path.Combine(
                nativeAotRuntimePackPath,
                "runtimes",
                "win-x64",
                "native") + trailingSeparator,
            ["IlcSdkPath"] = Path.Combine(
                nativeAotRuntimePackPath,
                "runtimes",
                "win-x64",
                "native") + trailingSeparator,
            ["IlcMibcPath"] = Path.Combine(
                nativeAotRuntimePackPath,
                "mibc") + trailingSeparator,
            ["RunILLink"] = "false",
            ["NativeCompilationDuringPublish"] = "true",
            ["IlcMultiModule"] = "false",
            ["DisableFrameworkLibGeneration"] = "true",
            ["IlcUseEnvironmentalTools"] = "true",
            ["PublishAotUsingRuntimePack"] = "false",
            ["IlcPgoOptimize"] = "false",
            ["ImportRuntimeIlcPackageTargetDependsOn"] = "",
            ["IlcSetupPropertiesDependsOn"] =
                "ImportRuntimeIlcPackageTarget",
            ["IlcDynamicBuildPropertyDependencies"] = "SetupProperties",
            ["ImportNuGetBuildTasksPackTargetsFromSdk"] = "false",
            ["NuGetBuildTasksPackTargets"] = "",
            ["LanguageTargets"] = Path.Combine(
                dotnetSdkDirectory,
                "Microsoft.CSharp.targets"),
            ["CSharpTargetsPath"] = Path.Combine(
                dotnetSdkDirectory,
                "Microsoft.CSharp.CurrentVersion.targets"),
            ["CommonTargetsPath"] = Path.Combine(
                dotnetSdkDirectory,
                "Microsoft.Common.CurrentVersion.targets"),
            ["MSBuildSDKsPath"] = Path.Combine(
                dotnetSdkDirectory,
                "Sdks"),
            ["MSBuildExtensionsPath"] =
                dotnetSdkDirectory + trailingSeparator,
            ["MSBuildUserExtensionsPath"] = Path.Combine(
                projectExtensionsPath,
                "disabled-user-extensions"),
            ["UseSharedCompilation"] = "false",
            ["ImportByWildcardBeforeMicrosoftCommonProps"] = "false",
            ["ImportByWildcardAfterMicrosoftCommonProps"] = "false",
            ["ImportUserLocationsByWildcardBeforeMicrosoftCommonProps"] =
                "false",
            ["ImportUserLocationsByWildcardAfterMicrosoftCommonProps"] =
                "false",
            ["ImportByWildcardBeforeMicrosoftCommonTargets"] = "false",
            ["ImportByWildcardAfterMicrosoftCommonTargets"] = "false",
            ["ImportUserLocationsByWildcardBeforeMicrosoftCommonTargets"] =
                "false",
            ["ImportUserLocationsByWildcardAfterMicrosoftCommonTargets"] =
                "false",
            ["ImportByWildcardBeforeMicrosoftCSharpTargets"] = "false",
            ["ImportByWildcardAfterMicrosoftCSharpTargets"] = "false",
            ["ImportUserLocationsByWildcardBeforeMicrosoftCSharpTargets"] =
                "false",
            ["ImportUserLocationsByWildcardAfterMicrosoftCSharpTargets"] =
                "false",
            ["ImportByWildcardBeforeMicrosoftCommonCrossTargetingTargets"] =
                "false",
            ["ImportByWildcardAfterMicrosoftCommonCrossTargetingTargets"] =
                "false"
        };
        return new Dictionary<string, object?>
        {
            ["compileCount"] = 101,
            ["compileSha256"] = CompileClosureSha256,
            ["projectReferenceCount"] = 0,
            ["projectReferenceSha256"] = EmptySha256,
            ["referenceCount"] = 0,
            ["referenceSha256"] = EmptySha256,
            ["analyzerCount"] = 2,
            ["analyzerSha256"] = AnalyzerClosureSha256,
            ["properties"] = properties
        };
    }

    private static string GetSequenceSha256(string[] values)
    {
        return GetSha256(
            Encoding.UTF8.GetBytes(string.Join('\n', values) + "\n"));
    }

    private static void DeleteSchemaThreeFixture()
    {
        try
        {
            cachedSchemaThreeFixture = null;
            string fixtureRoot = Path.GetFullPath(
                SchemaThreeFixtureRepositoryRoot);
            string temporaryRoot = Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(Path.GetTempPath()));
            if (!string.Equals(
                    Path.GetDirectoryName(fixtureRoot),
                    temporaryRoot,
                    StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(fixtureRoot).StartsWith(
                    "clros-efi-schema3-repository-",
                    StringComparison.Ordinal))
            {
                return;
            }

            if (Directory.Exists(fixtureRoot))
            {
                Directory.Delete(fixtureRoot, recursive: true);
            }
        }
        catch
        {
            // Generated fixture cleanup must not conceal verifier results.
        }
    }

    private static string ResolveDotnetHostPath()
    {
        if (cachedDotnetHostPath is not null)
        {
            return cachedDotnetHostPath;
        }

        string? searchPath = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(searchPath))
        {
            throw new InvalidOperationException("PATH is unavailable.");
        }

        foreach (string entry in searchPath.Split(Path.PathSeparator))
        {
            string directory = entry.Trim().Trim('"');
            if (directory.Length == 0)
            {
                continue;
            }

            string candidate = Path.Combine(directory, "dotnet.exe");
            if (File.Exists(candidate))
            {
                cachedDotnetHostPath = Path.GetFullPath(candidate);
                return cachedDotnetHostPath;
            }
        }

        throw new InvalidOperationException("dotnet.exe was not found on PATH.");
    }

    private static string ResolveDotnetSdkVersion(string dotnetHostPath)
    {
        if (cachedDotnetSdkVersion is not null)
        {
            return cachedDotnetSdkVersion;
        }

        var start = new ProcessStartInfo
        {
            FileName = dotnetHostPath,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        start.ArgumentList.Add("--version");
        using Process process = Process.Start(start)
            ?? throw new InvalidOperationException(
                "Failed to query the dotnet SDK version.");
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(15_000))
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // The timeout remains authoritative.
            }
            process.WaitForExit();
            throw new TimeoutException("dotnet --version timed out.");
        }

        Task.WaitAll(output, error);
        string version = output.Result.Trim();
        if (process.ExitCode != 0 ||
            !System.Text.RegularExpressions.Regex.IsMatch(
                version,
                "^[0-9]+\\.[0-9]+\\.[0-9]+(?:[-+][0-9A-Za-z.-]+)?$"))
        {
            throw new InvalidOperationException(
                "dotnet --version failed: " + error.Result.Trim());
        }

        cachedDotnetSdkVersion = version;
        return cachedDotnetSdkVersion;
    }

    private static SourceClosureFixture BuildSourceClosureFixture(
        string repositoryRoot,
        string projectPath)
    {
        string normalizedRoot = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(repositoryRoot));
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string projectDirectory = Path.GetDirectoryName(projectPath)!;
        XDocument project = XDocument.Load(projectPath);
        foreach (XElement compile in project.Descendants("Compile"))
        {
            string include = compile.Attribute("Include")?.Value
                ?? throw new InvalidOperationException(
                    "Fixture project compile input has no Include.");
            AddSourceClosurePath(
                paths,
                normalizedRoot,
                Path.GetFullPath(Path.Combine(projectDirectory, include)));
        }

        string[] explicitPaths =
        [
            "CLROS.slnx",
            "global.json",
            "src/OS/OS.csproj",
            "Directory.Build.props",
            "Directory.Build.targets",
            "scripts/build-efi-shell.ps1",
            "scripts/test-efi-artifact.ps1",
            ToolDirectoryBuildPropsPath,
            "tools/OS.X64TextSerializerGenerator/OS.X64TextSerializerGenerator.targets",
            "tools/OS.X64TextSerializerGenerator/OS.X64TextSerializerGenerator.csproj",
            "tools/OS.X64TextSerializerGenerator/Program.cs",
            "tools/OS.X64TextSerializerGenerator/Amd64CoffWriter.cs",
            "tools/OS.X64TextSerializerGenerator/InstructionPublicationSerializerGenerator.cs",
            "tools/OS.X64TextSerializerGenerator/CheckedReferenceBarrierThunkGenerator.cs"
        ];
        foreach (string relativePath in explicitPaths)
        {
            AddSourceClosurePath(
                paths,
                normalizedRoot,
                Path.GetFullPath(Path.Combine(normalizedRoot, relativePath)));
        }

        string[] orderedPaths = paths
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (orderedPaths.Length != ExpectedSourceClosureInputCount)
        {
            throw new InvalidOperationException(
                "Fixture source-closure input count changed: expected=" +
                ExpectedSourceClosureInputCount +
                " actual=" +
                orderedPaths.Length);
        }
        var inputs = new Dictionary<string, string>[orderedPaths.Length];
        var aggregate = new StringBuilder();
        for (int index = 0; index < orderedPaths.Length; index++)
        {
            string relativePath = orderedPaths[index];
            string fullPath = Path.GetFullPath(
                Path.Combine(
                    normalizedRoot,
                    relativePath.Replace('/', Path.DirectorySeparatorChar)));
            string sha256 = GetSha256(fullPath);
            inputs[index] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["path"] = relativePath,
                ["sha256"] = sha256
            };
            aggregate.Append(relativePath);
            aggregate.Append('|');
            aggregate.Append(sha256);
            aggregate.Append('\n');
        }

        string aggregateSha256 = GetSha256(
            Encoding.UTF8.GetBytes(aggregate.ToString()));
        return new SourceClosureFixture(aggregateSha256, inputs);
    }

    private static void AddSourceClosurePath(
        HashSet<string> paths,
        string repositoryRoot,
        string fullPath)
    {
        string rootPrefix = repositoryRoot + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Fixture source-closure path escaped the repository.");
        }

        string relativePath = Path.GetRelativePath(repositoryRoot, fullPath)
            .Replace('\\', '/');
        if (!paths.Add(relativePath))
        {
            throw new InvalidOperationException(
                "Fixture source-closure path is duplicated: " + relativePath);
        }
    }

    private static void MutateEvidence(
        string evidencePath,
        Action<JsonObject> mutation)
    {
        JsonObject evidence = JsonNode.Parse(File.ReadAllText(evidencePath))?.AsObject()
            ?? throw new InvalidOperationException("Fixture evidence did not parse.");
        mutation(evidence);
        File.WriteAllText(
            evidencePath,
            evidence.ToJsonString(),
            new UTF8Encoding(false));
    }

    private static void CorruptFirstSourceClosureInput(JsonObject evidence)
    {
        JsonArray inputs = evidence["sourceClosureInputs"]?.AsArray()
            ?? throw new InvalidOperationException(
                "Fixture sourceClosureInputs did not parse.");
        if (inputs.Count == 0)
        {
            throw new InvalidOperationException(
                "Fixture sourceClosureInputs was empty.");
        }

        JsonObject first = inputs[0]?.AsObject()
            ?? throw new InvalidOperationException(
                "Fixture source-closure entry did not parse.");
        first["sha256"] = new string('0', 64);

        UpdateSourceClosureAggregate(evidence, inputs);
    }

    private static void CorruptFirstRestoreClosureInput(JsonObject evidence)
    {
        JsonArray inputs = evidence["restoreClosureInputs"]?.AsArray()
            ?? throw new InvalidOperationException(
                "Fixture restoreClosureInputs did not parse.");
        if (inputs.Count == 0)
        {
            throw new InvalidOperationException(
                "Fixture restoreClosureInputs was empty.");
        }

        JsonObject first = inputs[0]?.AsObject()
            ?? throw new InvalidOperationException(
                "Fixture restore-closure entry did not parse.");
        first["sha256"] = new string('0', 64);
    }

    private static void RemoveSourceClosureInput(
        JsonObject evidence,
        string relativePath)
    {
        JsonArray inputs = evidence["sourceClosureInputs"]?.AsArray()
            ?? throw new InvalidOperationException(
                "Fixture sourceClosureInputs did not parse.");
        int matchIndex = -1;
        for (int index = 0; index < inputs.Count; index++)
        {
            JsonObject input = inputs[index]?.AsObject()
                ?? throw new InvalidOperationException(
                    "Fixture source-closure entry did not parse.");
            if (string.Equals(
                    input["path"]?.GetValue<string>(),
                    relativePath,
                    StringComparison.Ordinal))
            {
                matchIndex = index;
                break;
            }
        }

        if (matchIndex < 0)
        {
            throw new InvalidOperationException(
                "Fixture source-closure entry was not found: " + relativePath);
        }

        inputs.RemoveAt(matchIndex);
        UpdateSourceClosureAggregate(evidence, inputs);
    }

    private static void UpdateSourceClosureAggregate(
        JsonObject evidence,
        JsonArray inputs)
    {
        var aggregate = new StringBuilder();
        foreach (JsonNode? node in inputs)
        {
            JsonObject input = node?.AsObject()
                ?? throw new InvalidOperationException(
                    "Fixture source-closure entry did not parse.");
            aggregate.Append(input["path"]?.GetValue<string>());
            aggregate.Append('|');
            aggregate.Append(input["sha256"]?.GetValue<string>());
            aggregate.Append('\n');
        }

        evidence["sourceClosureSha256"] = GetSha256(
            Encoding.UTF8.GetBytes(aggregate.ToString()));
    }

    private static string GetSha256(string path)
    {
        return GetSha256(File.ReadAllBytes(path));
    }

    private static string GetSha256(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    [DllImport(
        "kernel32.dll",
        EntryPoint = "CreateHardLinkW",
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLink(
        string fileName,
        string existingFileName,
        IntPtr securityAttributes);

    private static void WriteUInt16(byte[] bytes, int offset, ushort value)
    {
        bytes[offset] = (byte)value;
        bytes[offset + 1] = (byte)(value >> 8);
    }

    private static void WriteUInt32(byte[] bytes, int offset, uint value)
    {
        bytes[offset] = (byte)value;
        bytes[offset + 1] = (byte)(value >> 8);
        bytes[offset + 2] = (byte)(value >> 16);
        bytes[offset + 3] = (byte)(value >> 24);
    }

    private static void WriteUInt64(byte[] bytes, int offset, ulong value)
    {
        for (int index = 0; index < 8; index++)
        {
            bytes[offset + index] = (byte)(value >> (index * 8));
        }
    }

    private static ProcessResult RunBoundedProcess(
        string fileName,
        IEnumerable<string> arguments,
        int timeoutMilliseconds)
    {
        var start = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        start.Environment.Remove("LINK");
        start.Environment.Remove("_LINK_");
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        var stopwatch = Stopwatch.StartNew();
        using Process process = Process.Start(start)
            ?? throw new InvalidOperationException(
                "Failed to start fixture process: " + fileName);
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();
        bool completed = process.WaitForExit(timeoutMilliseconds);
        if (!completed)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // A concurrent exit is harmless; TimedOut remains authoritative.
            }

            process.WaitForExit();
        }

        Task.WaitAll(standardOutput, standardError);
        stopwatch.Stop();
        return new ProcessResult(
            completed ? process.ExitCode : -1,
            standardOutput.Result + standardError.Result,
            timedOut: !completed,
            elapsedMilliseconds: stopwatch.ElapsedMilliseconds);
    }

    private static ProcessResult RunPeFixtureVerifier(string artifactPath)
    {
        WriteLinkEvidence(artifactPath);
        return RunVerifier(artifactPath);
    }

    private static ProcessResult RunVerifier(
        string artifactPath,
        string? projectPath = null,
        string? entrySourcePath = null,
        string? serializerObjectPath = null,
        string[]? additionalArguments = null)
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string canonicalScriptPath = Path.Combine(
            repositoryRoot,
            "scripts",
            "test-efi-artifact.ps1");
        string scriptPath =
            File.Exists(artifactPath + ".link.json") &&
            cachedSchemaThreeFixture is not null
                ? Path.Combine(
                    cachedSchemaThreeFixture.RepositoryRoot,
                    "scripts",
                    "test-efi-artifact.ps1")
                : canonicalScriptPath;

        ProcessStartInfo start = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-ExecutionPolicy");
        start.ArgumentList.Add("Bypass");
        start.ArgumentList.Add("-File");
        start.ArgumentList.Add(scriptPath);
        start.ArgumentList.Add("-EfiArtifact");
        start.ArgumentList.Add(artifactPath);

        if (projectPath is not null)
        {
            start.ArgumentList.Add("-ProjectPath");
            start.ArgumentList.Add(projectPath);
        }
        if (entrySourcePath is not null)
        {
            start.ArgumentList.Add("-EntrySourcePath");
            start.ArgumentList.Add(entrySourcePath);
        }
        if (serializerObjectPath is not null)
        {
            start.ArgumentList.Add("-SerializerObjectPath");
            start.ArgumentList.Add(serializerObjectPath);
        }
        if (additionalArguments is not null)
        {
            foreach (string argument in additionalArguments)
            {
                start.ArgumentList.Add(argument);
            }
        }

        var stopwatch = Stopwatch.StartNew();
        using Process process = Process.Start(start)
            ?? throw new InvalidOperationException("Failed to start PowerShell.");
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();
        bool completed = process.WaitForExit(
            VerifierFixtureProcessTimeoutMilliseconds);
        if (!completed)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // A concurrent exit is harmless; TimedOut remains authoritative.
            }

            process.WaitForExit();
        }

        Task.WaitAll(standardOutput, standardError);
        stopwatch.Stop();
        return new ProcessResult(
            completed ? process.ExitCode : -1,
            standardOutput.Result + standardError.Result,
            timedOut: !completed,
            elapsedMilliseconds: stopwatch.ElapsedMilliseconds);
    }

    private sealed class LinkEvidenceOptions
    {
        public uint MappedEntryRva { get; init; } = EntryPointRva;

        public uint SerializerRva { get; init; } = EfiArtifactVerificationScriptTests.SerializerRva;

        public int SerializerSymbolCopies { get; init; } = 1;

        public string? MapOwner { get; init; }

        public int ForceIncludeCopies { get; init; } = 1;

        public int BreproCopies { get; init; } = 1;

        public string BreproToken { get; init; } = "/BREPRO";

        public int ObjectTokenCopies { get; init; } = 1;

        public int MapOptionCopies { get; init; } = 1;

        public int MapInfoCopies { get; init; } = 1;

        public string? MapPathOverride { get; init; }

        public string? AdditionalResponse { get; init; }
    }

    private readonly struct ProcessResult(
        int exitCode,
        string output,
        bool timedOut,
        long elapsedMilliseconds)
    {
        public int ExitCode { get; } = exitCode;

        public string Output { get; } = output;

        public bool TimedOut { get; } = timedOut;

        public long ElapsedMilliseconds { get; } = elapsedMilliseconds;
    }

    private readonly record struct LinkEvidencePaths(
        string MapPath,
        string ResponsePath,
        string EvidencePath,
        string ObjectPath,
        string LinkedObjectPath,
        string LinkedMapPath,
        string ManagedAssemblyPath);

    private readonly record struct SourceClosureFixture(
        string Sha256,
        Dictionary<string, string>[] Inputs);

    private sealed record SchemaThreeFixture(
        string RepositoryRoot,
        string ProjectExtensionsPath,
        string ProjectAssetsPath,
        string PinnedRestoreSourcePath,
        string PackageRootPath,
        string RestoreArchiveSourceRootPath,
        SourceClosureFixture RestoreClosure,
        SourceClosureFixture PinnedRestoreSourceClosure,
        SourceClosureFixture RestoreArchiveClosure,
        string PackageClosureSha256,
        Dictionary<string, object?>[] Packages,
        SourceClosureFixture PackageBuildClosure,
        string[] ActivePackageImportEdges,
        string ActivePackageImportGraphSha256,
        string[] PackageTaskBindings,
        string PackageTaskBindingsSha256,
        Dictionary<string, object?> PostRestoreEvaluation,
        string IlCompilerPath,
        string IlCompilerDiscoverySourcePath,
        string NativeAotRuntimePackPath,
        string NativeAotRuntimePackSha256,
        int NativeAotRuntimePackFileCount,
        long NativeAotRuntimePackBytes,
        string IlCompilerTargetsPath,
        string IlLinkTargetsPath,
        string IlLinkTasksAssemblyPath,
        string IlcBuildTasksPath,
        string IlcHostPackagePath,
        string IlcToolsPath,
        string IlcFrameworkPath,
        string IlcFrameworkNativePath,
        string IlcSdkPath,
        string IlcMibcPath,
        string LanguageTargetsPath,
        string CSharpTargetsPath,
        string CommonTargetsPath);

    private readonly record struct RestoreArchiveDescriptor(
        string Id,
        string Version,
        string FileName,
        string Sha256);

    private readonly record struct PackageDescriptor(
        string Id,
        string Version,
        string RootKind,
        string ContentHashSha512,
        int FileCount,
        long TotalBytes,
        string AggregateSha256);

    private readonly record struct PackageInputFixture(
        string Path,
        long Bytes,
        string Sha256);

    private readonly record struct PackageTreeFixture(
        int FileCount,
        long TotalBytes,
        string AggregateSha256,
        Dictionary<string, object?>[] Inputs);
}
