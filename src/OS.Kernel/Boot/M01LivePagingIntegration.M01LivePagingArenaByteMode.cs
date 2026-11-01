namespace OS.Boot;
/// <summary>
/// Controls whether the arena materializer writes the audited table bytes or
/// independently verifies table bytes already supplied by the caller.
/// </summary>

public enum M01LivePagingArenaByteMode : byte
{
    Uninitialized = 0,
    EmitAndVerify = 1,
    VerifyExisting = 2
}
