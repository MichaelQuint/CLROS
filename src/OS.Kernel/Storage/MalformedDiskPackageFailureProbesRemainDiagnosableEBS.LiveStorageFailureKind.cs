namespace OS.Storage;

public enum LiveStorageFailureKind : byte
{
    Uninitialized = 0, Manifest = 1, Package = 2, Payload = 3, Disk = 4
}
