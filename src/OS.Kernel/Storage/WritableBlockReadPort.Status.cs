using B = OS.Storage.WritableBlockSession;
using C = OS.Storage.ContinuingFat32Mutation;

namespace OS.Storage;

public static unsafe partial class WritableBlockReadPort
{
    public enum Status : byte
    { Uninitialized, Success, Pending, Ready, Busy, Idle, Cancelled, InvalidState, InvalidStorage,
        InvalidRange, StaleToken, GenerationExhausted, DeviceFailed, Retired }
}
