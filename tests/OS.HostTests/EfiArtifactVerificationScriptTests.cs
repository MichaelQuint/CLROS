using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

internal static class EfiArtifactVerificationScriptTests
{
    private const int PeOffset = 0x80;
    private const int OptionalHeaderOffset = PeOffset + 24;
    private const int SectionTableOffset = OptionalHeaderOffset + 0xf0;
    private const uint CoffTimestamp = 0x6a651aa5;
    private const ulong ImageBase = 0x0000000180000000;
    private const uint EntryPointRva = 0x1000;

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
            Run("efi artifact verifier accepts minimal import-free x64 EFI PE32+", () => TestValidArtifactPasses(tempRoot), ref failures);
            Run("efi artifact verifier rejects missing and truncated inputs", () => TestMissingAndTruncatedArtifactsFail(tempRoot), ref failures);
            Run("efi artifact verifier rejects non-x64 and non-PE32+ headers", () => TestMachineAndFormatFailures(tempRoot), ref failures);
            Run("efi artifact verifier rejects non-EFI and zero entrypoint headers", () => TestSubsystemAndEntrypointFailures(tempRoot), ref failures);
            Run("efi artifact verifier rejects non-executable and virtual-only entrypoints", () => TestEntrypointSectionFailures(tempRoot), ref failures);
            Run("efi artifact verifier rejects ordinary runtime import directories", () => TestImportDirectoriesFail(tempRoot), ref failures);
            Run("efi artifact verifier rejects missing EfiMain source contract", () => TestEfiMainContractFailure(tempRoot), ref failures);
            Run("efi artifact verifier rejects a synthetic RET-only entrypoint", () => TestTrivialEntrypointFails(tempRoot), ref failures);
            Run("efi artifact verifier rejects missing, stale, and mismatched link evidence", () => TestLinkEvidenceFailures(tempRoot), ref failures);
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
        }

        if (failures != 0)
        {
            throw new InvalidOperationException("EFI artifact verification script tests failed: " + failures);
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
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static bool TestValidArtifactPasses(string tempRoot)
    {
        string artifact = WriteFixture(tempRoot, "valid artifact", CreateValidPe());
        WriteLinkEvidence(artifact);
        ProcessResult result = RunVerifier(artifact);

        return result.ExitCode == 0
            && result.Output.Contains("TEST_START efi.artifact.verify", StringComparison.Ordinal)
            && result.Output.Contains(
                "EFI_ARTIFACT_PE format=PE32+ machine=0x8664 subsystem=10 entryRva=0x00001000 entrySection=.text",
                StringComparison.Ordinal)
            && result.Output.Contains(
                "EFI_ARTIFACT_IMPORTS import=absent bound-import=absent iat=absent delay-import=absent",
                StringComparison.Ordinal)
            && result.Output.Contains(
                "EFI_ARTIFACT_ENTRY_CONTRACT symbol=EfiMain managedSymbol=OS_OS_Boot_EfiEntry__EfiMain association=same-build-linker-map rva=0x00001000",
                StringComparison.Ordinal)
            && result.Output.Contains("EFI_ARTIFACT_LINK_MAP path=", StringComparison.Ordinal)
            && result.Output.Contains("EFI_ARTIFACT_LINK_RESPONSE path=", StringComparison.Ordinal)
            && result.Output.Contains("EFI_ARTIFACT_SHA256 ", StringComparison.Ordinal)
            && result.Output.Contains("TEST_PASS efi.artifact.verify", StringComparison.Ordinal)
            && !result.Output.Contains("TEST_FAIL efi.artifact.verify", StringComparison.Ordinal);
    }

    private static bool TestMissingAndTruncatedArtifactsFail(string tempRoot)
    {
        string missing = Path.Combine(tempRoot, "missing", "BOOTX64.EFI");
        ProcessResult missingResult = RunVerifier(missing);

        string truncated = WriteFixture(tempRoot, "truncated", new byte[] { 0x4d, 0x5a });
        ProcessResult truncatedResult = RunVerifier(truncated);

        return FailedWith(missingResult, "missing_artifact")
            && FailedWith(truncatedResult, "truncated_dos_header");
    }

    private static bool TestMachineAndFormatFailures(string tempRoot)
    {
        byte[] wrongMachine = CreateValidPe();
        WriteUInt16(wrongMachine, PeOffset + 4, 0x014c);
        ProcessResult machineResult = RunVerifier(
            WriteFixture(tempRoot, "wrong-machine", wrongMachine));

        byte[] pe32 = CreateValidPe();
        WriteUInt16(pe32, OptionalHeaderOffset, 0x010b);
        ProcessResult formatResult = RunVerifier(
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
        ProcessResult subsystemResult = RunVerifier(
            WriteFixture(tempRoot, "wrong-subsystem", wrongSubsystem));

        byte[] zeroEntrypoint = CreateValidPe();
        WriteUInt32(zeroEntrypoint, OptionalHeaderOffset + 16, 0);
        ProcessResult entrypointResult = RunVerifier(
            WriteFixture(tempRoot, "zero-entrypoint", zeroEntrypoint));

        return FailedWith(subsystemResult, "subsystem_not_efi_application")
            && FailedWith(entrypointResult, "entrypoint_zero");
    }

    private static bool TestEntrypointSectionFailures(string tempRoot)
    {
        byte[] notExecutable = CreateValidPe();
        WriteUInt32(notExecutable, SectionTableOffset + 36, 0x40000040);
        ProcessResult executableResult = RunVerifier(
            WriteFixture(tempRoot, "non-executable-entry", notExecutable));

        byte[] virtualOnly = CreateValidPe();
        WriteUInt32(virtualOnly, OptionalHeaderOffset + 16, 0x1200);
        WriteUInt32(virtualOnly, SectionTableOffset + 8, 0x300);
        ProcessResult fileBackedResult = RunVerifier(
            WriteFixture(tempRoot, "virtual-only-entry", virtualOnly));

        return FailedWith(executableResult, "entrypoint_section_not_executable")
            && FailedWith(fileBackedResult, "entrypoint_not_file_backed");
    }

    private static bool TestImportDirectoriesFail(string tempRoot)
    {
        byte[] importTable = CreateValidPe();
        WriteUInt32(importTable, OptionalHeaderOffset + 112 + 8, 0x1100);
        WriteUInt32(importTable, OptionalHeaderOffset + 112 + 12, 20);
        ProcessResult importResult = RunVerifier(
            WriteFixture(tempRoot, "ordinary-import", importTable));

        byte[] delayImport = CreateValidPe();
        WriteUInt32(delayImport, OptionalHeaderOffset + 112 + (13 * 8), 0x1140);
        WriteUInt32(delayImport, OptionalHeaderOffset + 112 + (13 * 8) + 4, 32);
        ProcessResult delayResult = RunVerifier(
            WriteFixture(tempRoot, "delay-import", delayImport));

        byte[] malformedImport = CreateValidPe();
        WriteUInt32(malformedImport, OptionalHeaderOffset + 112 + 8, 0x1100);
        ProcessResult malformedResult = RunVerifier(
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
        string sourcePath = Path.Combine(tempRoot, "NoEfiMain.cs");
        File.WriteAllText(sourcePath, "internal static class NoEfiMain { }", new UTF8Encoding(false));

        ProcessResult result = RunVerifier(artifact, entrySourcePath: sourcePath);
        return FailedWith(result, "efimain_source_contract_missing")
            && !result.Output.Contains("TEST_PASS efi.artifact.verify", StringComparison.Ordinal);
    }

    private static bool TestTrivialEntrypointFails(string tempRoot)
    {
        byte[] trivial = CreateValidPe();
        trivial[0x200] = 0xc3;
        string artifact = WriteFixture(tempRoot, "ret-only-entrypoint", trivial);

        ProcessResult result = RunVerifier(artifact);
        return FailedWith(result, "entrypoint_trivial_stub")
            && result.Output.Contains("byte=0xc3", StringComparison.Ordinal)
            && !result.Output.Contains("EFI_ARTIFACT_ENTRY_CONTRACT", StringComparison.Ordinal);
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

        string mismatchArtifact = WriteFixture(
            tempRoot,
            "entrypoint-map-mismatch",
            CreateValidPe());
        WriteLinkEvidence(mismatchArtifact, mappedEntryRva: EntryPointRva + 0x100);
        ProcessResult mismatchResult = RunVerifier(mismatchArtifact);

        return FailedWith(missingResult, "missing_linker_map")
            && FailedWith(staleResult, "link_evidence_hash_mismatch")
            && FailedWith(mismatchResult, "efimain_entrypoint_mismatch");
    }

    private static bool FailedWith(ProcessResult result, string reason)
    {
        return result.ExitCode == 1
            && result.Output.Contains(
                "TEST_FAIL efi.artifact.verify " + reason,
                StringComparison.Ordinal)
            && !result.Output.Contains("TEST_PASS efi.artifact.verify", StringComparison.Ordinal);
    }

    private static string WriteFixture(string tempRoot, string name, byte[] bytes)
    {
        string fixtureDirectory = Path.Combine(tempRoot, name);
        Directory.CreateDirectory(fixtureDirectory);
        string artifact = Path.Combine(fixtureDirectory, "BOOTX64.EFI");
        File.WriteAllBytes(artifact, bytes);
        return artifact;
    }

    private static byte[] CreateValidPe()
    {
        byte[] bytes = new byte[0x400];

        bytes[0] = (byte)'M';
        bytes[1] = (byte)'Z';
        WriteUInt32(bytes, 0x3c, PeOffset);

        bytes[PeOffset] = (byte)'P';
        bytes[PeOffset + 1] = (byte)'E';
        WriteUInt16(bytes, PeOffset + 4, 0x8664);
        WriteUInt16(bytes, PeOffset + 6, 1);
        WriteUInt32(bytes, PeOffset + 8, CoffTimestamp);
        WriteUInt16(bytes, PeOffset + 20, 0xf0);
        WriteUInt16(bytes, PeOffset + 22, 0x0022);

        WriteUInt16(bytes, OptionalHeaderOffset, 0x020b);
        WriteUInt32(bytes, OptionalHeaderOffset + 16, EntryPointRva);
        WriteUInt32(bytes, OptionalHeaderOffset + 20, 0x1000);
        WriteUInt64(bytes, OptionalHeaderOffset + 24, ImageBase);
        WriteUInt32(bytes, OptionalHeaderOffset + 32, 0x1000);
        WriteUInt32(bytes, OptionalHeaderOffset + 36, 0x200);
        WriteUInt32(bytes, OptionalHeaderOffset + 56, 0x2000);
        WriteUInt32(bytes, OptionalHeaderOffset + 60, 0x200);
        WriteUInt16(bytes, OptionalHeaderOffset + 68, 10);
        WriteUInt32(bytes, OptionalHeaderOffset + 108, 16);

        Encoding.ASCII.GetBytes(".text\0\0\0").CopyTo(bytes, SectionTableOffset);
        WriteUInt32(bytes, SectionTableOffset + 8, 0x200);
        WriteUInt32(bytes, SectionTableOffset + 12, 0x1000);
        WriteUInt32(bytes, SectionTableOffset + 16, 0x200);
        WriteUInt32(bytes, SectionTableOffset + 20, 0x200);
        WriteUInt32(bytes, SectionTableOffset + 36, 0x60000020);
        bytes[0x200] = 0x48;
        bytes[0x201] = 0x31;
        bytes[0x202] = 0xc0;
        bytes[0x203] = 0xc3;

        return bytes;
    }

    private static LinkEvidencePaths WriteLinkEvidence(
        string artifactPath,
        uint mappedEntryRva = EntryPointRva)
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string projectPath = Path.Combine(repositoryRoot, "src", "OS", "OS.csproj");
        string entrySourcePath = Path.Combine(repositoryRoot, "src", "OS", "Boot", "EfiEntry.cs");
        string mapPath = artifactPath + ".link.map";
        string responsePath = artifactPath + ".link.rsp";
        string evidencePath = artifactPath + ".link.json";
        string linkerPath = Path.Combine(
            Path.GetDirectoryName(artifactPath)!,
            "trusted-fixture-link.exe");

        File.WriteAllBytes(
            linkerPath,
            Encoding.ASCII.GetBytes("fixture linker identity"));

        ulong mappedVirtualAddress = ImageBase + mappedEntryRva;
        uint mapSegmentOffset = mappedEntryRva - EntryPointRva;
        string mapText = string.Join(
            "\r\n",
            [
                " OS",
                $" Timestamp is {CoffTimestamp:x8} (fixture)",
                $" Preferred load address is {ImageBase:x16}",
                $" 0001:{mapSegmentOffset:x8} EfiMain {mappedVirtualAddress:x16} OS.obj",
                $" 0001:{mapSegmentOffset:x8} OS_OS_Boot_EfiEntry__EfiMain {mappedVirtualAddress:x16} OS.obj",
                $" entry point at 0001:{mapSegmentOffset:x8}",
                ""
            ]);
        File.WriteAllText(mapPath, mapText, new UTF8Encoding(false));
        File.WriteAllText(responsePath, "/ENTRY:EfiMain\r\n", new UTF8Encoding(false));

        var evidence = new
        {
            schemaVersion = 1,
            artifactSha256 = GetSha256(artifactPath),
            artifactBytes = new FileInfo(artifactPath).Length,
            linkerMapSha256 = GetSha256(mapPath),
            linkResponseSha256 = GetSha256(responsePath),
            sourceProjectSha256 = GetSha256(projectPath),
            entrySourceSha256 = GetSha256(entrySourcePath),
            entryPointSymbol = "EfiMain",
            managedEntrySymbol = "OS_OS_Boot_EfiEntry__EfiMain",
            linkerPath,
            linkerVersion = "fixture-1.0",
            linkerSha256 = GetSha256(linkerPath)
        };
        File.WriteAllText(
            evidencePath,
            JsonSerializer.Serialize(evidence),
            new UTF8Encoding(false));

        return new LinkEvidencePaths(mapPath, responsePath, evidencePath);
    }

    private static string GetSha256(string path)
    {
        return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))
            .ToLowerInvariant();
    }

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

    private static ProcessResult RunVerifier(
        string artifactPath,
        string? projectPath = null,
        string? entrySourcePath = null)
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string scriptPath = Path.Combine(repositoryRoot, "scripts", "test-efi-artifact.ps1");

        ProcessStartInfo start = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
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

        using Process process = Process.Start(start)
            ?? throw new InvalidOperationException("Failed to start PowerShell.");
        string standardOutput = process.StandardOutput.ReadToEnd();
        string standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return new ProcessResult(process.ExitCode, standardOutput + standardError);
    }

    private readonly struct ProcessResult(int exitCode, string output)
    {
        public int ExitCode { get; } = exitCode;

        public string Output { get; } = output;
    }

    private readonly record struct LinkEvidencePaths(
        string MapPath,
        string ResponsePath,
        string EvidencePath);
}
