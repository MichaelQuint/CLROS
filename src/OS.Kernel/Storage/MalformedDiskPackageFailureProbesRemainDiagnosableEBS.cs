namespace OS.Storage;

public enum LiveStorageFailureKind : byte { Uninitialized = 0, Manifest = 1, Package = 2, Payload = 3 }

public struct LiveStorageFailure
{
    public LiveStorageFailureKind Kind;
    public byte Code;
    public readonly bool Succeeded => Kind != LiveStorageFailureKind.Uninitialized && Code != 0;
}

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
                : kind == LiveStorageFailureKind.Payload && code > (byte)SelectedPayloadOwnedMemoryStatus.Success &&
                    code <= (byte)SelectedPayloadOwnedMemoryStatus.OverlappingBuffers;
        return valid ? new LiveStorageFailure { Kind = kind, Code = code } : default;
    }
}
