using OS.Drivers;

namespace OS.Storage;

public static unsafe partial class WritableBlockSession
{
#if !NOSTDLIB_EFI
    [System.Flags]
#endif
    public enum ObservationFlags : byte
    {
        None, Published = 1, CompletionValidated = 2, ReadCopied = 4
    }
}
