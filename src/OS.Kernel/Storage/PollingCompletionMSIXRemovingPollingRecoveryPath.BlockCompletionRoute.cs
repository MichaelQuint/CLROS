using OS.Drivers;

namespace OS.Storage;

public enum BlockCompletionRoute : byte
{
    Uninitialized, InvalidWait, Interrupt, PollingRecovery
}
