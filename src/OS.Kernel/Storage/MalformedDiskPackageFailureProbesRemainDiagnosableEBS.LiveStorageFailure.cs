namespace OS.Storage;

public struct LiveStorageFailure
{
    public LiveStorageFailureKind Kind;
    public byte Code;
    public readonly bool Succeeded => Kind != LiveStorageFailureKind.Uninitialized && Code != 0;
}
