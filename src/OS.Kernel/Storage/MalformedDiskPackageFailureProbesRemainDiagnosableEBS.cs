namespace OS.Storage;

/// <summary>Pure failure metadata for owned post-firmware diagnostics. A failure
/// is reportable only after the caller has checked transport cleanup and memory
/// canaries. This helper does not perform I/O or prove the caller's assertions.</summary>
public static class MalformedDiskPackageFailureProbesRemainDiagnosableEBS
{
    public static LiveStorageFailure Create(LiveStorageFailureKind kind, byte code,
        bool cleanupComplete, bool canariesValid)
    {
        if (!cleanupComplete || !canariesValid) return default;
        bool valid = kind == LiveStorageFailureKind.Manifest
            ? code > (byte)LiveManifestStatus.Success && code <= (byte)LiveManifestStatus.InvalidPath
            : kind == LiveStorageFailureKind.Package
                ? code > (byte)LivePackageStatus.Success && code <= (byte)LivePackageStatus.ChecksumMismatch
                : kind == LiveStorageFailureKind.Payload
                    ? code > (byte)SelectedPayloadOwnedMemoryStatus.Success && code <= (byte)SelectedPayloadOwnedMemoryStatus.OverlappingBuffers
                    : kind == LiveStorageFailureKind.Disk &&
                        (code == (byte)LiveFat32Status.Truncated || code == (byte)LiveFat32Status.Loop);
        return valid ? new LiveStorageFailure { Kind = kind, Code = code } : default;
    }
}
