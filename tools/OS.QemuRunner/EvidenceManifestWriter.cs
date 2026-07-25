using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OS.Tools.QemuRunner;

public enum EvidenceRunClassification
{
    Passed = 1,
    Failed = 2,
    TimedOut = 3
}

public enum EvidenceManifestWriteStatus
{
    Success = 0,
    InvalidRequest = 1,
    MissingInput = 2,
    HashMismatch = 3,
    ContradictoryMarkers = 4,
    StaleSourceState = 5,
    IoError = 6
}

public sealed record EvidenceArtifactInput(
    string Name,
    string Role,
    string Path,
    string ExpectedSha256);

public sealed class EvidenceManifestRequest
{
    public string RepositoryPath { get; init; } = string.Empty;

    public string ExpectedSourceCommit { get; init; } = string.Empty;

    public bool ExpectedSourceDirty { get; init; }

    public DateTimeOffset StartedUtc { get; init; }

    public DateTimeOffset CompletedUtc { get; init; }

    public string Command { get; init; } = string.Empty;

    public int ExitCode { get; init; }

    public EvidenceRunClassification Classification { get; init; }

    public IReadOnlyDictionary<string, string> ToolVersions { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public IReadOnlyList<EvidenceArtifactInput> Artifacts { get; init; } =
        Array.Empty<EvidenceArtifactInput>();

    public IReadOnlyList<string> RequiredMarkers { get; init; } =
        Array.Empty<string>();

    public IReadOnlyList<string> ForbiddenMarkers { get; init; } =
        Array.Empty<string>();

    public IReadOnlyList<string> ObservedMarkers { get; init; } =
        Array.Empty<string>();
}

public readonly record struct EvidenceManifestWriteResult(
    EvidenceManifestWriteStatus Status,
    string ManifestPath,
    string ManifestSha256,
    string Reason)
{
    public bool Succeeded => Status == EvidenceManifestWriteStatus.Success;
}

public static class EvidenceManifestWriter
{
    private const int SchemaVersion = 1;
    private const int GitTimeoutMilliseconds = 10_000;
    private static readonly UTF8Encoding StrictUtf8 =
        new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true);
    private static readonly string[] RequiredArtifactRoles =
        ["config", "efi", "image", "serial-log"];

    public static EvidenceManifestWriteResult Write(
        EvidenceManifestRequest? request,
        string? outputPath)
    {
        if (request is null || string.IsNullOrWhiteSpace(outputPath))
        {
            return Failure(EvidenceManifestWriteStatus.InvalidRequest, "missing_request_or_output");
        }

        if (!TryNormalizeCommit(request.ExpectedSourceCommit, out string expectedCommit))
        {
            return Failure(EvidenceManifestWriteStatus.InvalidRequest, "invalid_source_commit");
        }

        EvidenceManifestWriteResult sourceResult = TryReadRepositoryState(
            request.RepositoryPath,
            out RepositoryState initialSourceState);
        if (!sourceResult.Succeeded)
        {
            return sourceResult;
        }

        if (!string.Equals(
                initialSourceState.Commit,
                expectedCommit,
                StringComparison.Ordinal))
        {
            return Failure(EvidenceManifestWriteStatus.StaleSourceState, "source_commit_mismatch");
        }

        if (initialSourceState.Dirty != request.ExpectedSourceDirty)
        {
            return Failure(EvidenceManifestWriteStatus.StaleSourceState, "source_dirty_mismatch");
        }

        if (!IsUtcTimestamp(request.StartedUtc) ||
            !IsUtcTimestamp(request.CompletedUtc) ||
            request.CompletedUtc < request.StartedUtc)
        {
            return Failure(EvidenceManifestWriteStatus.InvalidRequest, "invalid_timestamps");
        }

        if (string.IsNullOrWhiteSpace(request.Command) ||
            !Enum.IsDefined(request.Classification) ||
            (request.Classification == EvidenceRunClassification.Passed && request.ExitCode != 0) ||
            (request.Classification != EvidenceRunClassification.Passed && request.ExitCode == 0))
        {
            return Failure(EvidenceManifestWriteStatus.InvalidRequest, "invalid_run_classification");
        }

        if (!TryNormalizeTools(request.ToolVersions, out KeyValuePair<string, string>[] tools))
        {
            return Failure(EvidenceManifestWriteStatus.InvalidRequest, "invalid_tool_versions");
        }

        if (!TryNormalizeMarkers(request.RequiredMarkers, out string[] requiredMarkers) ||
            !TryNormalizeMarkers(request.ForbiddenMarkers, out string[] forbiddenMarkers) ||
            !TryNormalizeMarkers(request.ObservedMarkers, out string[] claimedObservedMarkers))
        {
            return Failure(EvidenceManifestWriteStatus.InvalidRequest, "invalid_markers");
        }

        if (requiredMarkers.Intersect(forbiddenMarkers, StringComparer.Ordinal).Any())
        {
            return Failure(EvidenceManifestWriteStatus.ContradictoryMarkers, "marker_claim_overlap");
        }

        string fullOutputPath;
        try
        {
            fullOutputPath = Path.GetFullPath(outputPath);
        }
        catch (Exception)
        {
            return Failure(EvidenceManifestWriteStatus.InvalidRequest, "invalid_output_path");
        }

        EvidenceManifestWriteResult artifactResult = TryReadArtifacts(
            request.Artifacts,
            fullOutputPath,
            out EvidenceArtifactRecord[] artifacts,
            out byte[] serialLogBytes);
        if (!artifactResult.Succeeded)
        {
            return artifactResult;
        }

        EvidenceManifestWriteResult markerResult = TryBindMarkers(
            serialLogBytes,
            requiredMarkers,
            forbiddenMarkers,
            claimedObservedMarkers,
            request.Classification,
            out string[] observedMarkers);
        if (!markerResult.Succeeded)
        {
            return markerResult;
        }

        sourceResult = TryReadRepositoryState(
            request.RepositoryPath,
            out RepositoryState finalSourceState);
        if (!sourceResult.Succeeded)
        {
            return sourceResult;
        }

        if (initialSourceState != finalSourceState)
        {
            return Failure(
                EvidenceManifestWriteStatus.StaleSourceState,
                "source_state_changed_during_capture");
        }

        byte[] manifestBytes;
        try
        {
            using MemoryStream buffer = new MemoryStream();
            using (Utf8JsonWriter writer = new Utf8JsonWriter(
                buffer,
                new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();
                writer.WriteNumber("schemaVersion", SchemaVersion);

                writer.WriteStartObject("source");
                writer.WriteString("commit", initialSourceState.Commit);
                writer.WriteBoolean("dirty", initialSourceState.Dirty);
                writer.WriteEndObject();

                writer.WriteStartObject("run");
                writer.WriteString("startedUtc", request.StartedUtc.ToString("O"));
                writer.WriteString("completedUtc", request.CompletedUtc.ToString("O"));
                writer.WriteString("command", request.Command.Trim());
                writer.WriteNumber("exitCode", request.ExitCode);
                writer.WriteString("classification", FormatClassification(request.Classification));
                writer.WriteEndObject();

                writer.WriteStartObject("tools");
                foreach (KeyValuePair<string, string> tool in tools)
                {
                    writer.WriteString(tool.Key, tool.Value);
                }
                writer.WriteEndObject();

                writer.WriteStartArray("artifacts");
                foreach (EvidenceArtifactRecord artifact in artifacts)
                {
                    writer.WriteStartObject();
                    writer.WriteString("name", artifact.Name);
                    writer.WriteString("role", artifact.Role);
                    writer.WriteString("path", artifact.Path);
                    writer.WriteNumber("bytes", artifact.Length);
                    writer.WriteString("sha256", artifact.Sha256);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();

                WriteStringArray(writer, "requiredMarkers", requiredMarkers);
                WriteStringArray(writer, "forbiddenMarkers", forbiddenMarkers);
                WriteStringArray(writer, "observedMarkers", observedMarkers);
                writer.WriteEndObject();
            }

            manifestBytes = buffer.ToArray();
        }
        catch (Exception)
        {
            return Failure(EvidenceManifestWriteStatus.IoError, "manifest_serialization_failed");
        }

        try
        {
            string? directory = Path.GetDirectoryName(fullOutputPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string temporaryPath = fullOutputPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllBytes(temporaryPath, manifestBytes);
                File.Move(temporaryPath, fullOutputPath, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }

            string manifestHash = Convert.ToHexString(
                SHA256.HashData(manifestBytes)).ToLowerInvariant();
            return new EvidenceManifestWriteResult(
                EvidenceManifestWriteStatus.Success,
                fullOutputPath,
                manifestHash,
                string.Empty);
        }
        catch (Exception)
        {
            return Failure(EvidenceManifestWriteStatus.IoError, "manifest_write_failed");
        }
    }

    private static EvidenceManifestWriteResult TryReadRepositoryState(
        string? repositoryPath,
        out RepositoryState state)
    {
        state = default;
        if (string.IsNullOrWhiteSpace(repositoryPath))
        {
            return Failure(EvidenceManifestWriteStatus.InvalidRequest, "repository_path_missing");
        }

        string fullRepositoryPath;
        try
        {
            fullRepositoryPath = Path.GetFullPath(repositoryPath);
        }
        catch (Exception)
        {
            return Failure(EvidenceManifestWriteStatus.InvalidRequest, "repository_path_invalid");
        }

        if (!Directory.Exists(fullRepositoryPath))
        {
            return Failure(EvidenceManifestWriteStatus.MissingInput, "repository_missing");
        }

        if (!TryRunGit(
                fullRepositoryPath,
                ["rev-parse", "--show-toplevel"],
                out string repositoryRootText) ||
            string.IsNullOrWhiteSpace(repositoryRootText))
        {
            return Failure(
                EvidenceManifestWriteStatus.MissingInput,
                "repository_state_unavailable");
        }

        string repositoryRoot;
        try
        {
            repositoryRoot = Path.GetFullPath(repositoryRootText.Trim());
        }
        catch (Exception)
        {
            return Failure(
                EvidenceManifestWriteStatus.MissingInput,
                "repository_state_unavailable");
        }

        if (!TryRunGit(
                repositoryRoot,
                ["rev-parse", "--verify", "HEAD"],
                out string commitText) ||
            !TryNormalizeCommit(commitText.Trim(), out string commit))
        {
            return Failure(
                EvidenceManifestWriteStatus.MissingInput,
                "repository_state_unavailable");
        }

        if (!TryRunGit(
                repositoryRoot,
                ["status", "--porcelain=v1", "--untracked-files=all", "--ignore-submodules=none"],
                out string statusText))
        {
            return Failure(
                EvidenceManifestWriteStatus.MissingInput,
                "repository_state_unavailable");
        }

        state = new RepositoryState(commit, statusText.Length != 0);
        return Success();
    }

    private static bool TryRunGit(
        string repositoryPath,
        IReadOnlyList<string> arguments,
        out string standardOutput)
    {
        standardOutput = string.Empty;
        try
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
            startInfo.Environment["GIT_OPTIONAL_LOCKS"] = "0";
            startInfo.ArgumentList.Add("-C");
            startInfo.ArgumentList.Add(repositoryPath);
            foreach (string argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using Process process = new Process { StartInfo = startInfo };
            if (!process.Start())
            {
                return false;
            }

            Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
            Task<string> errorTask = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(GitTimeoutMilliseconds))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit();
                }
                catch
                {
                    // The process is already gone or cannot be terminated.
                }

                return false;
            }

            standardOutput = outputTask.GetAwaiter().GetResult();
            _ = errorTask.GetAwaiter().GetResult();
            return process.ExitCode == 0;
        }
        catch (Exception)
        {
            standardOutput = string.Empty;
            return false;
        }
    }

    private static EvidenceManifestWriteResult TryReadArtifacts(
        IReadOnlyList<EvidenceArtifactInput>? inputs,
        string fullOutputPath,
        out EvidenceArtifactRecord[] artifacts,
        out byte[] serialLogBytes)
    {
        artifacts = [];
        serialLogBytes = [];
        if (inputs is null || inputs.Count == 0)
        {
            return Failure(EvidenceManifestWriteStatus.InvalidRequest, "missing_artifacts");
        }

        HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
        Dictionary<string, int> requiredRoleCounts =
            RequiredArtifactRoles.ToDictionary(
                role => role,
                _ => 0,
                StringComparer.Ordinal);
        List<EvidenceArtifactRecord> records = new List<EvidenceArtifactRecord>(inputs.Count);
        foreach (EvidenceArtifactInput? input in inputs)
        {
            if (input is null ||
                string.IsNullOrWhiteSpace(input.Name) ||
                string.IsNullOrWhiteSpace(input.Role) ||
                string.IsNullOrWhiteSpace(input.Path) ||
                !TryNormalizeSha256(input.ExpectedSha256, out string expectedHash) ||
                !names.Add(input.Name.Trim()))
            {
                return Failure(EvidenceManifestWriteStatus.InvalidRequest, "invalid_artifact");
            }

            string role = input.Role.Trim();
            if (requiredRoleCounts.TryGetValue(role, out int roleCount))
            {
                roleCount++;
                requiredRoleCounts[role] = roleCount;
                if (roleCount > 1)
                {
                    return Failure(
                        EvidenceManifestWriteStatus.InvalidRequest,
                        "required_artifact_role_ambiguous");
                }
            }

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(input.Path);
            }
            catch (Exception)
            {
                return Failure(EvidenceManifestWriteStatus.InvalidRequest, "invalid_artifact_path");
            }

            if (string.Equals(fullPath, fullOutputPath, StringComparison.OrdinalIgnoreCase))
            {
                return Failure(EvidenceManifestWriteStatus.InvalidRequest, "output_overlaps_input");
            }

            if (!File.Exists(fullPath))
            {
                return Failure(EvidenceManifestWriteStatus.MissingInput, "artifact_missing");
            }

            try
            {
                using FileStream stream = new FileStream(
                    fullPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read);
                if (stream.Length == 0)
                {
                    return Failure(EvidenceManifestWriteStatus.MissingInput, "artifact_empty");
                }

                string actualHash;
                if (string.Equals(role, "serial-log", StringComparison.Ordinal))
                {
                    if (stream.Length > int.MaxValue)
                    {
                        return Failure(
                            EvidenceManifestWriteStatus.InvalidRequest,
                            "serial_log_too_large");
                    }

                    serialLogBytes = new byte[(int)stream.Length];
                    stream.ReadExactly(serialLogBytes);
                    actualHash = Convert.ToHexString(
                        SHA256.HashData(serialLogBytes)).ToLowerInvariant();
                }
                else
                {
                    actualHash = Convert.ToHexString(
                        SHA256.HashData(stream)).ToLowerInvariant();
                }

                if (!string.Equals(actualHash, expectedHash, StringComparison.Ordinal))
                {
                    return Failure(EvidenceManifestWriteStatus.HashMismatch, "artifact_hash_mismatch");
                }

                records.Add(new EvidenceArtifactRecord(
                    input.Name.Trim(),
                    role,
                    NormalizeManifestPath(input.Path),
                    stream.Length,
                    actualHash));
            }
            catch (UnauthorizedAccessException)
            {
                return Failure(EvidenceManifestWriteStatus.MissingInput, "artifact_unreadable");
            }
            catch (IOException)
            {
                return Failure(EvidenceManifestWriteStatus.MissingInput, "artifact_unreadable");
            }
        }

        if (requiredRoleCounts.Any(pair => pair.Value == 0))
        {
            return Failure(EvidenceManifestWriteStatus.InvalidRequest, "required_artifact_role_missing");
        }

        artifacts = records
            .OrderBy(record => record.Role, StringComparer.Ordinal)
            .ThenBy(record => record.Name, StringComparer.Ordinal)
            .ToArray();
        return Success();
    }

    private static EvidenceManifestWriteResult TryBindMarkers(
        byte[] serialLogBytes,
        IReadOnlyList<string> requiredMarkers,
        IReadOnlyList<string> forbiddenMarkers,
        IReadOnlyList<string> claimedObservedMarkers,
        EvidenceRunClassification classification,
        out string[] observedMarkers)
    {
        observedMarkers = [];
        string serialText;
        try
        {
            serialText = StrictUtf8.GetString(serialLogBytes);
        }
        catch (DecoderFallbackException)
        {
            return Failure(EvidenceManifestWriteStatus.InvalidRequest, "serial_log_invalid_utf8");
        }

        SerialMarkerSummary summary = SerialMarkerSummary.Parse(
            serialText,
            requiredMarkers,
            forbiddenMarkers);
        string[] serialLines = ReadTrimmedLines(serialText);

        if (claimedObservedMarkers.Any(
                claim => !serialLines.Contains(claim, StringComparer.Ordinal)))
        {
            return Failure(
                EvidenceManifestWriteStatus.ContradictoryMarkers,
                "observed_marker_claim_mismatch");
        }

        if (HasConflictingTerminalMarkers(serialLines))
        {
            return Failure(
                EvidenceManifestWriteStatus.ContradictoryMarkers,
                "terminal_marker_conflict");
        }

        if (classification == EvidenceRunClassification.Passed)
        {
            if (!summary.HasAllRequiredMarkers)
            {
                return Failure(
                    EvidenceManifestWriteStatus.ContradictoryMarkers,
                    "required_marker_missing");
            }

            if (summary.HasForbiddenMarkers)
            {
                return Failure(
                    EvidenceManifestWriteStatus.ContradictoryMarkers,
                    "forbidden_marker_observed");
            }

            if (!MarkerObserved("OS_SMOKE_PASS", serialLines))
            {
                return Failure(
                    EvidenceManifestWriteStatus.ContradictoryMarkers,
                    "pass_terminal_marker_missing");
            }

            if (MarkerObserved("TEST_FAIL", serialLines) ||
                MarkerObserved("OS_SMOKE_FAIL", serialLines))
            {
                return Failure(
                    EvidenceManifestWriteStatus.ContradictoryMarkers,
                    "pass_failure_marker_observed");
            }
        }

        HashSet<string> derivedMarkers =
            new HashSet<string>(summary.ObservedMarkerLines, StringComparer.Ordinal);
        foreach (string line in serialLines)
        {
            if (requiredMarkers.Any(marker => LineMatchesMarker(line, marker)) ||
                forbiddenMarkers.Any(marker => LineMatchesMarker(line, marker)) ||
                claimedObservedMarkers.Contains(line, StringComparer.Ordinal))
            {
                derivedMarkers.Add(line);
            }
        }

        observedMarkers = derivedMarkers
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        return Success();
    }

    private static string[] ReadTrimmedLines(string serialText)
    {
        List<string> lines = new List<string>();
        using StringReader reader = new StringReader(serialText);
        while (reader.ReadLine() is { } line)
        {
            string trimmed = line.Trim();
            if (trimmed.Length != 0)
            {
                lines.Add(trimmed);
            }
        }

        return lines.ToArray();
    }

    private static bool TryNormalizeTools(
        IReadOnlyDictionary<string, string>? input,
        out KeyValuePair<string, string>[] tools)
    {
        tools = [];
        if (input is null || input.Count == 0)
        {
            return false;
        }

        List<KeyValuePair<string, string>> normalized =
            new List<KeyValuePair<string, string>>(input.Count);
        foreach (KeyValuePair<string, string> item in input)
        {
            if (string.IsNullOrWhiteSpace(item.Key) || string.IsNullOrWhiteSpace(item.Value))
            {
                return false;
            }

            normalized.Add(new KeyValuePair<string, string>(
                item.Key.Trim(),
                item.Value.Trim()));
        }

        tools = normalized
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .ToArray();
        return true;
    }

    private static bool TryNormalizeMarkers(
        IReadOnlyList<string>? input,
        out string[] markers)
    {
        markers = [];
        if (input is null)
        {
            return false;
        }

        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
        List<string> normalized = new List<string>(input.Count);
        foreach (string? marker in input)
        {
            if (string.IsNullOrWhiteSpace(marker))
            {
                return false;
            }

            string trimmed = marker.Trim();
            if (!seen.Add(trimmed))
            {
                return false;
            }

            normalized.Add(trimmed);
        }

        markers = normalized.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        return true;
    }

    private static bool MarkerObserved(
        string marker,
        IReadOnlyList<string> observedMarkers)
    {
        return observedMarkers.Any(line => LineMatchesMarker(line, marker));
    }

    private static bool LineMatchesMarker(string line, string marker)
    {
        return string.Equals(line, marker, StringComparison.Ordinal) ||
            line.StartsWith(marker + " ", StringComparison.Ordinal);
    }

    private static bool HasConflictingTerminalMarkers(IReadOnlyList<string> observedMarkers)
    {
        return MarkerObserved("OS_SMOKE_PASS", observedMarkers) &&
            MarkerObserved("OS_SMOKE_FAIL", observedMarkers);
    }

    private static bool TryNormalizeCommit(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (value is null ||
            (value.Length != 40 && value.Length != 64) ||
            !value.All(IsHexCharacter))
        {
            return false;
        }

        normalized = value.ToLowerInvariant();
        return true;
    }

    private static bool TryNormalizeSha256(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (value is null || value.Length != 64 || !value.All(IsHexCharacter))
        {
            return false;
        }

        normalized = value.ToLowerInvariant();
        return true;
    }

    private static bool IsHexCharacter(char value)
    {
        return (value >= '0' && value <= '9') ||
            (value >= 'a' && value <= 'f') ||
            (value >= 'A' && value <= 'F');
    }

    private static bool IsUtcTimestamp(DateTimeOffset value)
    {
        return value != default && value.Offset == TimeSpan.Zero;
    }

    private static string NormalizeManifestPath(string path)
    {
        return path.Replace('\\', '/');
    }

    private static string FormatClassification(EvidenceRunClassification classification)
    {
        return classification switch
        {
            EvidenceRunClassification.Passed => "pass",
            EvidenceRunClassification.Failed => "fail",
            EvidenceRunClassification.TimedOut => "timeout",
            _ => throw new ArgumentOutOfRangeException(nameof(classification))
        };
    }

    private static void WriteStringArray(
        Utf8JsonWriter writer,
        string propertyName,
        IReadOnlyList<string> values)
    {
        writer.WriteStartArray(propertyName);
        foreach (string value in values)
        {
            writer.WriteStringValue(value);
        }
        writer.WriteEndArray();
    }

    private static EvidenceManifestWriteResult Success()
    {
        return new EvidenceManifestWriteResult(
            EvidenceManifestWriteStatus.Success,
            string.Empty,
            string.Empty,
            string.Empty);
    }

    private static EvidenceManifestWriteResult Failure(
        EvidenceManifestWriteStatus status,
        string reason)
    {
        return new EvidenceManifestWriteResult(status, string.Empty, string.Empty, reason);
    }

    private readonly record struct RepositoryState(string Commit, bool Dirty);

    private sealed record EvidenceArtifactRecord(
        string Name,
        string Role,
        string Path,
        long Length,
        string Sha256);
}
