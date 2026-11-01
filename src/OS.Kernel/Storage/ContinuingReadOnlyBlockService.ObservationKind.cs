using OS.Drivers;

namespace OS.Storage;

public static unsafe partial class ContinuingReadOnlyBlockService
{
    public enum ObservationKind : byte { Submitted, CompletionValidated, CopyReady }
}
