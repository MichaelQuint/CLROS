using OS.Storage;

internal static class MalformedDiskPackageFailureProbesRemainDiagnosableEBSTests
{
    [HostTest("m06.failure reports only known failure metadata after asserted cleanup and canary checks")]
    private static void Classify()
    {
        HostCheck.True(!default(LiveStorageFailure).Succeeded);
        foreach (var item in new[] { (LiveStorageFailureKind.Manifest, (byte)LiveManifestStatus.InvalidValue),
            (LiveStorageFailureKind.Package, (byte)LivePackageStatus.ChecksumMismatch),
            (LiveStorageFailureKind.Payload, (byte)SelectedPayloadOwnedMemoryStatus.DestinationTooSmall) })
        {
            foreach (bool cleanup in new[] { false, true })
            foreach (bool canary in new[] { false, true })
            {
                var result = MalformedDiskPackageFailureProbesRemainDiagnosableEBS.Create(item.Item1, item.Item2, cleanup, canary);
                HostCheck.Equal(cleanup && canary, result.Succeeded);
                if (result.Succeeded) { HostCheck.Equal(item.Item1, result.Kind); HostCheck.Equal(item.Item2, result.Code); }
                else { HostCheck.Equal(LiveStorageFailureKind.Uninitialized, result.Kind); HostCheck.Equal((byte)0, result.Code); }
            }
        }
        foreach (var kind in new[] { LiveStorageFailureKind.Manifest, LiveStorageFailureKind.Package, LiveStorageFailureKind.Payload })
        foreach (byte code in new byte[] { 0, 1, 255 })
            HostCheck.True(!MalformedDiskPackageFailureProbesRemainDiagnosableEBS.Create(kind, code, true, true).Succeeded);
        HostCheck.True(!MalformedDiskPackageFailureProbesRemainDiagnosableEBS.Create((LiveStorageFailureKind)255, 2, true, true).Succeeded);
    }
}
