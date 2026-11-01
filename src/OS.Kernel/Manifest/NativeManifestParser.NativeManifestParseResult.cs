using OS.Std;
using OS.Std.Text;

namespace OS.Manifest;
public readonly struct NativeManifestParseResult
{
    private NativeManifestParseResult(NativeManifestParseStatus status, bool isInitialized, NativeManifest manifest, int errorOffset)
    {
        Status = status;
        IsInitialized = isInitialized;
        Manifest = manifest;
        ErrorOffset = errorOffset;
    }

    public NativeManifestParseStatus Status { get; }
    public bool IsInitialized { get; }
    public NativeManifest Manifest { get; }
    public int ErrorOffset { get; }
    public bool Succeeded => IsInitialized && Status == NativeManifestParseStatus.Success;

    public static NativeManifestParseResult Success(NativeManifest manifest)
    {
        return new NativeManifestParseResult(NativeManifestParseStatus.Success, isInitialized: true, manifest, -1);
    }

    public static NativeManifestParseResult Fail(NativeManifestParseStatus status, int errorOffset)
    {
        return new NativeManifestParseResult(status, isInitialized: true, default, errorOffset);
    }
}
