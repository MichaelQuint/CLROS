namespace OS.Boot;
/// <summary>
/// Selects whether the coordinator materializes the audited bytes before
/// readback or audits a caller-materialized page. The second mode permits an
/// exact malformed-readback failure without a test-only callback.
/// </summary>

public enum M01StageOneStubByteMode : byte
{
    Uninitialized = 0,
    EmitAndVerify = 1,
    VerifyExisting = 2
}
#if !NOSTDLIB_EFI
#endif
#if !NOSTDLIB_EFI
#endif
