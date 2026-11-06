namespace OS.Boot;
/// <summary>
/// Controls whether the coordinator writes the audited bytes or only checks
/// bytes already supplied by its caller. VerifyExisting is needed for exact
/// malformed-readback tests without test-only callbacks.
/// </summary>

public enum M01LivePagingByteMode : byte
{
    Uninitialized = 0,
    EmitAndVerify = 1,
    VerifyExisting = 2
}
