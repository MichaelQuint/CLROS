using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Tools.QemuRunner;

internal static class EvidenceManifestWriterTests
{
    private static readonly DateTimeOffset Started =
        new DateTimeOffset(2026, 7, 25, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Completed = Started.AddSeconds(15);

    [HostFixture(HostLane.Native)]
    internal static void Run()
    {
        int failures = 0;
        string tempRoot = Path.Combine(
            Path.GetTempPath(),
            "clros-evidence-manifest-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        Console.WriteLine("TEST_START evidence.manifest");
        try
        {
            RunTest("writes deterministic complete manifest", () => TestSuccess(tempRoot), ref failures);
            RunTest("command derives repository state", () => TestCommand(tempRoot), ref failures);
            RunTest("records failed and timed-out runs", () => TestFailedAndTimedOutRuns(tempRoot), ref failures);
            RunTest("rejects missing and malformed fields", () => TestInvalidFields(tempRoot), ref failures);
            RunTest("rejects missing unreadable and empty inputs", () => TestMissingUnreadableAndEmptyInputs(tempRoot), ref failures);
            RunTest("rejects malformed stale and mismatched hashes", () => TestHashes(tempRoot), ref failures);
            RunTest("rejects stale and dirty source mismatches", () => TestSourceState(tempRoot), ref failures);
            RunTest("binds marker claims to hashed serial bytes", () => TestMarkerBinding(tempRoot), ref failures);
            RunTest("rejects invalid timestamps and output overlap", () => TestTimeAndOutput(tempRoot), ref failures);
        }
        finally
        {
            try
            {
                Directory.Delete(tempRoot, recursive: true);
            }
            catch
            {
                // Cleanup failure must not replace evidence assertions.
            }
        }

        if (failures != 0)
        {
            Console.WriteLine("TEST_FAIL evidence.manifest failures=" + failures);
            throw new InvalidOperationException("Evidence manifest tests failed: " + failures);
        }

        Console.WriteLine("TEST_PASS evidence.manifest");
    }

    private static void RunTest(string name, Func<bool> test, ref int failures)
    {

        HostCheck.Run("evidence.manifest " + name, test, ref failures);
    }

    private static bool TestSuccess(string tempRoot)
    {
        EvidenceFixture fixture = CreateFixture(tempRoot, "success");
        string firstPath = Path.Combine(fixture.Root, "evidence-1.json");
        string secondPath = Path.Combine(fixture.Root, "evidence-2.json");

        EvidenceManifestWriteResult first =
            EvidenceManifestWriter.Write(fixture.PassingRequest, firstPath);
        EvidenceManifestWriteResult second =
            EvidenceManifestWriter.Write(fixture.PassingRequest, secondPath);

        byte[] firstBytes = File.ReadAllBytes(firstPath);
        byte[] secondBytes = File.ReadAllBytes(secondPath);
        if (!first.Succeeded ||
            !second.Succeeded ||
            first.ManifestSha256 != second.ManifestSha256 ||
            !firstBytes.AsSpan().SequenceEqual(secondBytes) ||
            firstBytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }))
        {
            return false;
        }

        string json = File.ReadAllText(firstPath);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        JsonElement artifacts = root.GetProperty("artifacts");
        JsonElement observed = root.GetProperty("observedMarkers");
        string computedManifestHash = Convert.ToHexString(
            SHA256.HashData(firstBytes)).ToLowerInvariant();

        int configPosition = json.IndexOf("\"role\": \"config\"", StringComparison.Ordinal);
        int efiPosition = json.IndexOf("\"role\": \"efi\"", StringComparison.Ordinal);
        int imagePosition = json.IndexOf("\"role\": \"image\"", StringComparison.Ordinal);
        int serialPosition = json.IndexOf("\"role\": \"serial-log\"", StringComparison.Ordinal);
        int qemuPosition = json.IndexOf("\"qemu\"", StringComparison.Ordinal);
        int sdkPosition = json.IndexOf("\"sdk\"", StringComparison.Ordinal);

        return root.GetProperty("schemaVersion").GetInt32() == 1
            && root.GetProperty("source").GetProperty("commit").GetString() == fixture.Commit
            && !root.GetProperty("source").GetProperty("dirty").GetBoolean()
            && root.GetProperty("run").GetProperty("classification").GetString() == "pass"
            && root.GetProperty("run").GetProperty("exitCode").GetInt32() == 0
            && artifacts.GetArrayLength() == 4
            && artifacts.EnumerateArray().All(
                artifact => artifact.GetProperty("bytes").GetInt64() > 0)
            && observed.EnumerateArray().Any(
                marker => marker.GetString() == "OS_SMOKE_PASS")
            && first.ManifestSha256 == computedManifestHash
            && configPosition >= 0
            && configPosition < efiPosition
            && efiPosition < imagePosition
            && imagePosition < serialPosition
            && qemuPosition >= 0
            && qemuPosition < sdkPosition;
    }

    private static bool TestCommand(string tempRoot)
    {
        EvidenceFixture fixture = CreateFixture(tempRoot, "command");
        string output = Path.Combine(fixture.Root, "command-evidence.json");
        List<string> arguments =
        [
            "evidence-manifest",
            "--output", output,
            "--repository", fixture.RepositoryPath,
            "--expected-source-commit", fixture.Commit,
            "--expected-source-dirty", "false",
            "--started-utc", Started.ToString("O"),
            "--completed-utc", Completed.ToString("O"),
            "--command-file", fixture.CommandFile,
            "--exit-code", "0",
            "--classification", "pass",
            "--tool", "sdk=10.0.302",
            "--tool", "qemu=11.0.50"
        ];

        foreach (EvidenceArtifactInput artifact in fixture.PassingRequest.Artifacts)
        {
            arguments.Add("--artifact");
            arguments.Add(
                artifact.Name + "|" + artifact.Role + "|" +
                artifact.Path + "|" + artifact.ExpectedSha256);
        }

        foreach (string marker in fixture.PassingRequest.RequiredMarkers)
        {
            arguments.Add("--require-marker");
            arguments.Add(marker);
        }

        foreach (string marker in fixture.PassingRequest.ForbiddenMarkers)
        {
            arguments.Add("--forbid-marker");
            arguments.Add(marker);
        }

        foreach (string marker in fixture.PassingRequest.ObservedMarkers)
        {
            arguments.Add("--observed-marker");
            arguments.Add(marker);
        }

        bool handled = EvidenceManifestCommand.TryRun(arguments.ToArray(), out int exitCode);
        if (!handled || exitCode != 0 || !File.Exists(output))
        {
            return false;
        }

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(output));
        JsonElement source = document.RootElement.GetProperty("source");
        return source.GetProperty("commit").GetString() == fixture.Commit
            && !source.GetProperty("dirty").GetBoolean();
    }

    private static bool TestFailedAndTimedOutRuns(string tempRoot)
    {
        EvidenceFixture failedFixture = CreateFixture(tempRoot, "failed");
        EvidenceManifestRequest failed = WithSerial(
            failedFixture.PassingRequest,
            "OS_BOOT_OK\nTEST_FAIL boot.sequence reason=timeout\nOS_SMOKE_FAIL reason=timeout\n",
            ["OS_BOOT_OK", "TEST_FAIL boot.sequence reason=timeout", "OS_SMOKE_FAIL reason=timeout"],
            classification: EvidenceRunClassification.Failed,
            exitCode: 1);
        string failedOutput = Path.Combine(failedFixture.Root, "failed.json");
        EvidenceManifestWriteResult failedResult =
            EvidenceManifestWriter.Write(failed, failedOutput);

        EvidenceFixture timeoutFixture = CreateFixture(tempRoot, "timeout");
        EvidenceManifestRequest timedOut = WithSerial(
            timeoutFixture.PassingRequest,
            "OS_BOOT_OK\n",
            ["OS_BOOT_OK"],
            classification: EvidenceRunClassification.TimedOut,
            exitCode: 124);
        string timeoutOutput = Path.Combine(timeoutFixture.Root, "timeout.json");
        EvidenceManifestWriteResult timeoutResult =
            EvidenceManifestWriter.Write(timedOut, timeoutOutput);

        if (!failedResult.Succeeded || !timeoutResult.Succeeded)
        {
            return false;
        }

        using JsonDocument failedDocument =
            JsonDocument.Parse(File.ReadAllText(failedOutput));
        using JsonDocument timeoutDocument =
            JsonDocument.Parse(File.ReadAllText(timeoutOutput));
        return failedDocument.RootElement
                .GetProperty("run")
                .GetProperty("classification")
                .GetString() == "fail"
            && timeoutDocument.RootElement
                .GetProperty("run")
                .GetProperty("classification")
                .GetString() == "timeout";
    }

    private static bool TestInvalidFields(string tempRoot)
    {
        EvidenceFixture fixture = CreateFixture(tempRoot, "invalid");
        string output = Path.Combine(fixture.Root, "invalid.json");

        return EvidenceManifestWriter.Write(null, output).Status ==
                EvidenceManifestWriteStatus.InvalidRequest
            && EvidenceManifestWriter.Write(fixture.PassingRequest, null).Status ==
                EvidenceManifestWriteStatus.InvalidRequest
            && EvidenceManifestWriter.Write(
                Clone(fixture.PassingRequest, expectedCommit: "bad"),
                output).Status == EvidenceManifestWriteStatus.InvalidRequest
            && EvidenceManifestWriter.Write(
                Clone(fixture.PassingRequest, repositoryPath: " "),
                output).Status == EvidenceManifestWriteStatus.InvalidRequest
            && EvidenceManifestWriter.Write(
                Clone(fixture.PassingRequest, command: " "),
                output).Status == EvidenceManifestWriteStatus.InvalidRequest
            && EvidenceManifestWriter.Write(
                Clone(fixture.PassingRequest, exitCode: 9),
                output).Status == EvidenceManifestWriteStatus.InvalidRequest
            && EvidenceManifestWriter.Write(
                Clone(
                    fixture.PassingRequest,
                    classification: EvidenceRunClassification.Failed,
                    exitCode: 0),
                output).Status == EvidenceManifestWriteStatus.InvalidRequest
            && EvidenceManifestWriter.Write(
                Clone(fixture.PassingRequest, tools: new Dictionary<string, string>()),
                output).Status == EvidenceManifestWriteStatus.InvalidRequest
            && EvidenceManifestWriter.Write(
                Clone(fixture.PassingRequest, required: ["OS_SMOKE_PASS", "OS_SMOKE_PASS"]),
                output).Status == EvidenceManifestWriteStatus.InvalidRequest;
    }

    private static bool TestMissingUnreadableAndEmptyInputs(string tempRoot)
    {
        EvidenceFixture fixture = CreateFixture(tempRoot, "missing");
        string output = Path.Combine(fixture.Root, "missing.json");
        List<EvidenceArtifactInput> missing = fixture.PassingRequest.Artifacts.ToList();
        missing[0] = missing[0] with { Path = Path.Combine(fixture.Root, "absent.bin") };

        EvidenceManifestWriteResult missingResult = EvidenceManifestWriter.Write(
            Clone(fixture.PassingRequest, artifacts: missing),
            output);
        EvidenceManifestWriteResult noArtifacts = EvidenceManifestWriter.Write(
            Clone(fixture.PassingRequest, artifacts: []),
            output);
        EvidenceManifestWriteResult missingRole = EvidenceManifestWriter.Write(
            Clone(
                fixture.PassingRequest,
                artifacts: fixture.PassingRequest.Artifacts
                    .Where(value => value.Role != "serial-log")
                    .ToArray()),
            output);

        EvidenceArtifactInput serial = fixture.PassingRequest.Artifacts
            .Single(value => value.Role == "serial-log");
        EvidenceManifestWriteResult unreadableResult;
        using (FileStream locked = new FileStream(
            serial.Path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.None))
        {
            unreadableResult = EvidenceManifestWriter.Write(
                fixture.PassingRequest,
                output);
        }

        string missingRepository = Path.Combine(fixture.Root, "absent-repository");
        string nonRepository = Path.Combine(fixture.Root, "not-a-repository");
        Directory.CreateDirectory(nonRepository);
        EvidenceManifestWriteResult missingRepositoryResult = EvidenceManifestWriter.Write(
            Clone(fixture.PassingRequest, repositoryPath: missingRepository),
            output);
        EvidenceManifestWriteResult nonRepositoryResult = EvidenceManifestWriter.Write(
            Clone(fixture.PassingRequest, repositoryPath: nonRepository),
            output);

        bool emptyInputsRejected = true;
        foreach (string role in new[] { "config", "efi", "image", "serial-log" })
        {
            EvidenceFixture emptyFixture = CreateFixture(tempRoot, "empty-" + role);
            EvidenceArtifactInput artifact = emptyFixture.PassingRequest.Artifacts
                .Single(value => value.Role == role);
            File.WriteAllBytes(artifact.Path, []);
            EvidenceArtifactInput emptyArtifact = artifact with
            {
                ExpectedSha256 = HashBytes([])
            };
            IReadOnlyList<EvidenceArtifactInput> emptyArtifacts =
                emptyFixture.PassingRequest.Artifacts
                    .Select(value => value.Role == role ? emptyArtifact : value)
                    .ToArray();
            EvidenceManifestWriteResult emptyResult = EvidenceManifestWriter.Write(
                Clone(emptyFixture.PassingRequest, artifacts: emptyArtifacts),
                Path.Combine(emptyFixture.Root, "empty.json"));
            emptyInputsRejected &=
                emptyResult.Status == EvidenceManifestWriteStatus.MissingInput &&
                emptyResult.Reason == "artifact_empty";
        }

        List<EvidenceArtifactInput> ambiguous =
            fixture.PassingRequest.Artifacts.ToList();
        EvidenceArtifactInput duplicate = ambiguous[0] with
        {
            Name = "duplicate-" + ambiguous[0].Name
        };
        ambiguous.Add(duplicate);
        EvidenceManifestWriteResult ambiguousResult = EvidenceManifestWriter.Write(
            Clone(fixture.PassingRequest, artifacts: ambiguous),
            output);

        return missingResult.Status == EvidenceManifestWriteStatus.MissingInput
            && missingResult.Reason == "artifact_missing"
            && noArtifacts.Status == EvidenceManifestWriteStatus.InvalidRequest
            && missingRole.Status == EvidenceManifestWriteStatus.InvalidRequest
            && missingRole.Reason == "required_artifact_role_missing"
            && unreadableResult.Status == EvidenceManifestWriteStatus.MissingInput
            && unreadableResult.Reason == "artifact_unreadable"
            && missingRepositoryResult.Status == EvidenceManifestWriteStatus.MissingInput
            && missingRepositoryResult.Reason == "repository_missing"
            && nonRepositoryResult.Status == EvidenceManifestWriteStatus.MissingInput
            && nonRepositoryResult.Reason == "repository_state_unavailable"
            && ambiguousResult.Status == EvidenceManifestWriteStatus.InvalidRequest
            && ambiguousResult.Reason == "required_artifact_role_ambiguous"
            && emptyInputsRejected;
    }

    private static bool TestHashes(string tempRoot)
    {
        EvidenceFixture fixture = CreateFixture(tempRoot, "hashes");
        string output = Path.Combine(fixture.Root, "hashes.json");
        List<EvidenceArtifactInput> malformed = fixture.PassingRequest.Artifacts.ToList();
        malformed[0] = malformed[0] with { ExpectedSha256 = "not-a-hash" };
        List<EvidenceArtifactInput> mismatch = fixture.PassingRequest.Artifacts.ToList();
        mismatch[0] = mismatch[0] with { ExpectedSha256 = new string('0', 64) };

        EvidenceManifestWriteResult malformedResult = EvidenceManifestWriter.Write(
            Clone(fixture.PassingRequest, artifacts: malformed),
            output);
        EvidenceManifestWriteResult mismatchResult = EvidenceManifestWriter.Write(
            Clone(fixture.PassingRequest, artifacts: mismatch),
            output);

        EvidenceFixture changedSerialFixture = CreateFixture(tempRoot, "changed-serial");
        EvidenceArtifactInput changedSerial = changedSerialFixture.PassingRequest.Artifacts
            .Single(value => value.Role == "serial-log");
        File.AppendAllText(
            changedSerial.Path,
            "OS_SMOKE_FAIL forged-after-hash\n",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        EvidenceManifestWriteResult changedSerialResult = EvidenceManifestWriter.Write(
            changedSerialFixture.PassingRequest,
            Path.Combine(changedSerialFixture.Root, "changed-serial.json"));

        return malformedResult.Status == EvidenceManifestWriteStatus.InvalidRequest
            && mismatchResult.Status == EvidenceManifestWriteStatus.HashMismatch
            && changedSerialResult.Status == EvidenceManifestWriteStatus.HashMismatch
            && changedSerialResult.Reason == "artifact_hash_mismatch";
    }

    private static bool TestSourceState(string tempRoot)
    {
        EvidenceFixture staleFixture = CreateFixture(tempRoot, "stale-commit");
        string staleOutput = Path.Combine(staleFixture.Root, "stale.json");
        File.WriteAllText(
            Path.Combine(staleFixture.RepositoryPath, "second.txt"),
            "second",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        RunGit(staleFixture.RepositoryPath, "add", "second.txt");
        RunGit(staleFixture.RepositoryPath, "commit", "--quiet", "-m", "second");
        EvidenceManifestWriteResult staleResult = EvidenceManifestWriter.Write(
            staleFixture.PassingRequest,
            staleOutput);

        EvidenceFixture dirtyFixture = CreateFixture(tempRoot, "dirty");
        File.WriteAllText(
            Path.Combine(dirtyFixture.RepositoryPath, "untracked.txt"),
            "dirty",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        string dirtyOutput = Path.Combine(dirtyFixture.Root, "dirty.json");
        EvidenceManifestWriteResult dirtyMismatch = EvidenceManifestWriter.Write(
            dirtyFixture.PassingRequest,
            dirtyOutput);
        EvidenceManifestWriteResult verifiedDirty = EvidenceManifestWriter.Write(
            Clone(dirtyFixture.PassingRequest, expectedDirty: true),
            dirtyOutput);

        EvidenceFixture cleanFixture = CreateFixture(tempRoot, "expected-dirty");
        EvidenceManifestWriteResult expectedDirtyMismatch = EvidenceManifestWriter.Write(
            Clone(cleanFixture.PassingRequest, expectedDirty: true),
            Path.Combine(cleanFixture.Root, "expected-dirty.json"));

        bool recordedDirty = false;
        if (verifiedDirty.Succeeded)
        {
            using JsonDocument document =
                JsonDocument.Parse(File.ReadAllText(dirtyOutput));
            recordedDirty = document.RootElement
                .GetProperty("source")
                .GetProperty("dirty")
                .GetBoolean();
        }

        return staleResult.Status == EvidenceManifestWriteStatus.StaleSourceState
            && staleResult.Reason == "source_commit_mismatch"
            && !File.Exists(staleOutput)
            && dirtyMismatch.Status == EvidenceManifestWriteStatus.StaleSourceState
            && dirtyMismatch.Reason == "source_dirty_mismatch"
            && verifiedDirty.Succeeded
            && recordedDirty
            && expectedDirtyMismatch.Status == EvidenceManifestWriteStatus.StaleSourceState
            && expectedDirtyMismatch.Reason == "source_dirty_mismatch";
    }

    private static bool TestMarkerBinding(string tempRoot)
    {
        EvidenceFixture forgedFixture = CreateFixture(tempRoot, "forged-marker");
        EvidenceManifestRequest forgedClaim = Clone(
            forgedFixture.PassingRequest,
            observed:
            [
                "OS_BOOT_OK",
                "TEST_PASS boot.sequence",
                "OS_SMOKE_PASS",
                "UEFI_EBS_SUCCESS"
            ]);
        EvidenceManifestWriteResult forgedResult = EvidenceManifestWriter.Write(
            forgedClaim,
            Path.Combine(forgedFixture.Root, "forged.json"));

        EvidenceFixture missingFixture = CreateFixture(tempRoot, "missing-marker");
        EvidenceManifestRequest missingRequired = WithSerial(
            missingFixture.PassingRequest,
            "OS_BOOT_OK\nOS_SMOKE_PASS\n",
            ["OS_BOOT_OK", "OS_SMOKE_PASS"]);
        EvidenceManifestWriteResult missingResult = EvidenceManifestWriter.Write(
            missingRequired,
            Path.Combine(missingFixture.Root, "missing-marker.json"));

        EvidenceFixture forbiddenFixture = CreateFixture(tempRoot, "forbidden-marker");
        EvidenceManifestRequest forbidden = WithSerial(
            Clone(forbiddenFixture.PassingRequest, forbidden: ["PANIC"]),
            "OS_BOOT_OK\nTEST_PASS boot.sequence\nPANIC unexpected\nOS_SMOKE_PASS\n",
            ["OS_BOOT_OK", "TEST_PASS boot.sequence", "PANIC unexpected", "OS_SMOKE_PASS"]);
        EvidenceManifestWriteResult forbiddenResult = EvidenceManifestWriter.Write(
            forbidden,
            Path.Combine(forbiddenFixture.Root, "forbidden.json"));

        EvidenceFixture conflictFixture = CreateFixture(tempRoot, "terminal-conflict");
        EvidenceManifestRequest terminalConflict = WithSerial(
            conflictFixture.PassingRequest,
            "OS_BOOT_OK\nTEST_PASS boot.sequence\nOS_SMOKE_PASS\nOS_SMOKE_FAIL reason=late\n",
            [
                "OS_BOOT_OK",
                "TEST_PASS boot.sequence",
                "OS_SMOKE_PASS",
                "OS_SMOKE_FAIL reason=late"
            ]);
        EvidenceManifestWriteResult conflictResult = EvidenceManifestWriter.Write(
            terminalConflict,
            Path.Combine(conflictFixture.Root, "terminal-conflict.json"));

        EvidenceFixture noTerminalFixture = CreateFixture(tempRoot, "no-terminal");
        EvidenceManifestRequest noTerminal = WithSerial(
            Clone(
                noTerminalFixture.PassingRequest,
                required: ["OS_BOOT_OK", "TEST_PASS boot.sequence"]),
            "OS_BOOT_OK\nTEST_PASS boot.sequence\n",
            ["OS_BOOT_OK", "TEST_PASS boot.sequence"]);
        EvidenceManifestWriteResult noTerminalResult = EvidenceManifestWriter.Write(
            noTerminal,
            Path.Combine(noTerminalFixture.Root, "no-terminal.json"));

        EvidenceFixture testFailFixture = CreateFixture(tempRoot, "test-fail-pass");
        EvidenceManifestRequest testFail = WithSerial(
            Clone(testFailFixture.PassingRequest, forbidden: []),
            "OS_BOOT_OK\nTEST_PASS boot.sequence\nTEST_FAIL optional reason=bad\nOS_SMOKE_PASS\n",
            [
                "OS_BOOT_OK",
                "TEST_PASS boot.sequence",
                "TEST_FAIL optional reason=bad",
                "OS_SMOKE_PASS"
            ]);
        EvidenceManifestWriteResult testFailResult = EvidenceManifestWriter.Write(
            testFail,
            Path.Combine(testFailFixture.Root, "test-fail-pass.json"));

        EvidenceFixture invalidUtf8Fixture = CreateFixture(tempRoot, "invalid-utf8");
        EvidenceManifestRequest invalidUtf8 = WithSerial(
            invalidUtf8Fixture.PassingRequest,
            [0xFF, 0xFE, 0xFD],
            []);
        EvidenceManifestWriteResult invalidUtf8Result = EvidenceManifestWriter.Write(
            invalidUtf8,
            Path.Combine(invalidUtf8Fixture.Root, "invalid-utf8.json"));

        return forgedResult.Status == EvidenceManifestWriteStatus.ContradictoryMarkers
            && forgedResult.Reason == "observed_marker_claim_mismatch"
            && missingResult.Status == EvidenceManifestWriteStatus.ContradictoryMarkers
            && missingResult.Reason == "required_marker_missing"
            && forbiddenResult.Status == EvidenceManifestWriteStatus.ContradictoryMarkers
            && forbiddenResult.Reason == "forbidden_marker_observed"
            && conflictResult.Status == EvidenceManifestWriteStatus.ContradictoryMarkers
            && conflictResult.Reason == "terminal_marker_conflict"
            && noTerminalResult.Status == EvidenceManifestWriteStatus.ContradictoryMarkers
            && noTerminalResult.Reason == "pass_terminal_marker_missing"
            && testFailResult.Status == EvidenceManifestWriteStatus.ContradictoryMarkers
            && testFailResult.Reason == "pass_failure_marker_observed"
            && invalidUtf8Result.Status == EvidenceManifestWriteStatus.InvalidRequest
            && invalidUtf8Result.Reason == "serial_log_invalid_utf8";
    }

    private static bool TestTimeAndOutput(string tempRoot)
    {
        EvidenceFixture fixture = CreateFixture(tempRoot, "time");
        string output = Path.Combine(fixture.Root, "time.json");
        EvidenceManifestRequest reverse = Clone(
            fixture.PassingRequest,
            started: Completed,
            completed: Started);
        EvidenceManifestRequest localOffset = Clone(
            fixture.PassingRequest,
            started: Started.ToOffset(TimeSpan.FromHours(-4)));

        EvidenceManifestWriteResult overlap = EvidenceManifestWriter.Write(
            fixture.PassingRequest,
            fixture.PassingRequest.Artifacts[0].Path);

        return EvidenceManifestWriter.Write(reverse, output).Status ==
                EvidenceManifestWriteStatus.InvalidRequest
            && EvidenceManifestWriter.Write(localOffset, output).Status ==
                EvidenceManifestWriteStatus.InvalidRequest
            && overlap.Status == EvidenceManifestWriteStatus.InvalidRequest
            && overlap.Reason == "output_overlaps_input";
    }

    private static EvidenceFixture CreateFixture(string tempRoot, string name)
    {
        string root = Path.Combine(tempRoot, name);
        string repositoryPath = Path.Combine(root, "repository");
        string artifactRoot = Path.Combine(root, "artifacts");
        Directory.CreateDirectory(repositoryPath);
        Directory.CreateDirectory(artifactRoot);

        RunGit(repositoryPath, "init", "--quiet");
        RunGit(repositoryPath, "config", "user.name", "CLROS Test");
        RunGit(repositoryPath, "config", "user.email", "clros-test@example.invalid");
        RunGit(repositoryPath, "config", "commit.gpgsign", "false");
        File.WriteAllText(
            Path.Combine(repositoryPath, "seed.txt"),
            "seed",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        RunGit(repositoryPath, "add", "seed.txt");
        RunGit(repositoryPath, "commit", "--quiet", "-m", "seed");
        string commit = RunGit(repositoryPath, "rev-parse", "--verify", "HEAD").Trim();

        EvidenceArtifactInput efi =
            CreateArtifact(artifactRoot, "BOOTX64.EFI", "efi", "efi-bytes");
        EvidenceArtifactInput image =
            CreateArtifact(artifactRoot, "os.raw", "image", "image-bytes");
        EvidenceArtifactInput config =
            CreateArtifact(artifactRoot, "qemu.json", "config", "{\"memory\":256}");
        EvidenceArtifactInput serial = CreateArtifact(
            artifactRoot,
            "qemu-smoke.log",
            "serial-log",
            "OS_BOOT_OK\nTEST_PASS boot.sequence\nOS_SMOKE_PASS\n");
        string commandFile = Path.Combine(root, "command.txt");
        File.WriteAllText(
            commandFile,
            "qemu-system-x86_64 --machine q35",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        EvidenceManifestRequest request = new EvidenceManifestRequest
        {
            RepositoryPath = repositoryPath,
            ExpectedSourceCommit = commit.ToUpperInvariant(),
            ExpectedSourceDirty = false,
            StartedUtc = Started,
            CompletedUtc = Completed,
            Command = "qemu-system-x86_64 --machine q35",
            ExitCode = 0,
            Classification = EvidenceRunClassification.Passed,
            ToolVersions = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["sdk"] = "10.0.302",
                ["qemu"] = "11.0.50"
            },
            Artifacts = [serial, image, efi, config],
            RequiredMarkers = ["OS_SMOKE_PASS", "OS_BOOT_OK", "TEST_PASS boot.sequence"],
            ForbiddenMarkers = ["OS_SMOKE_FAIL"],
            ObservedMarkers = ["TEST_PASS boot.sequence", "OS_SMOKE_PASS", "OS_BOOT_OK"]
        };

        return new EvidenceFixture(root, repositoryPath, commit, commandFile, request);
    }

    private static EvidenceArtifactInput CreateArtifact(
        string root,
        string name,
        string role,
        string content)
    {
        string path = Path.Combine(root, name);
        File.WriteAllText(
            path,
            content,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return new EvidenceArtifactInput(name, role, path, HashFile(path));
    }

    private static EvidenceManifestRequest WithSerial(
        EvidenceManifestRequest source,
        string content,
        IReadOnlyList<string> observed,
        EvidenceRunClassification? classification = null,
        int? exitCode = null)
    {
        return WithSerial(
            source,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(content),
            observed,
            classification,
            exitCode);
    }

    private static EvidenceManifestRequest WithSerial(
        EvidenceManifestRequest source,
        byte[] content,
        IReadOnlyList<string> observed,
        EvidenceRunClassification? classification = null,
        int? exitCode = null)
    {
        EvidenceArtifactInput serial = source.Artifacts
            .Single(value => value.Role == "serial-log");
        File.WriteAllBytes(serial.Path, content);
        EvidenceArtifactInput updatedSerial = serial with
        {
            ExpectedSha256 = HashBytes(content)
        };
        IReadOnlyList<EvidenceArtifactInput> artifacts = source.Artifacts
            .Select(value => value.Role == "serial-log" ? updatedSerial : value)
            .ToArray();
        return Clone(
            source,
            artifacts: artifacts,
            observed: observed,
            classification: classification,
            exitCode: exitCode);
    }

    private static EvidenceManifestRequest Clone(
        EvidenceManifestRequest source,
        string? repositoryPath = null,
        string? expectedCommit = null,
        bool? expectedDirty = null,
        DateTimeOffset? started = null,
        DateTimeOffset? completed = null,
        string? command = null,
        int? exitCode = null,
        EvidenceRunClassification? classification = null,
        IReadOnlyDictionary<string, string>? tools = null,
        IReadOnlyList<EvidenceArtifactInput>? artifacts = null,
        IReadOnlyList<string>? required = null,
        IReadOnlyList<string>? forbidden = null,
        IReadOnlyList<string>? observed = null)
    {
        return new EvidenceManifestRequest
        {
            RepositoryPath = repositoryPath ?? source.RepositoryPath,
            ExpectedSourceCommit = expectedCommit ?? source.ExpectedSourceCommit,
            ExpectedSourceDirty = expectedDirty ?? source.ExpectedSourceDirty,
            StartedUtc = started ?? source.StartedUtc,
            CompletedUtc = completed ?? source.CompletedUtc,
            Command = command ?? source.Command,
            ExitCode = exitCode ?? source.ExitCode,
            Classification = classification ?? source.Classification,
            ToolVersions = tools ?? source.ToolVersions,
            Artifacts = artifacts ?? source.Artifacts,
            RequiredMarkers = required ?? source.RequiredMarkers,
            ForbiddenMarkers = forbidden ?? source.ForbiddenMarkers,
            ObservedMarkers = observed ?? source.ObservedMarkers
        };
    }

    private static string HashFile(string path)
    {
        return HashBytes(File.ReadAllBytes(path));
    }

    private static string HashBytes(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static string RunGit(string repositoryPath, params string[] arguments)
    {
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = "git",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = repositoryPath
        };
        startInfo.ArgumentList.Add("-C");
        startInfo.ArgumentList.Add(repositoryPath);
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Unable to start git.");
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                "git failed with exit " + process.ExitCode + ": " + error.Trim());
        }

        return output;
    }

    private sealed record EvidenceFixture(
        string Root,
        string RepositoryPath,
        string Commit,
        string CommandFile,
        EvidenceManifestRequest PassingRequest);
}
