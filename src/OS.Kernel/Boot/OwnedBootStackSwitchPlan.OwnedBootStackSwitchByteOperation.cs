namespace OS.Boot;
/// <summary>
/// Identifies whether byte-plan evidence came from emission or readback.
/// Only successful readback evidence can arm a switch plan.
/// </summary>

public enum OwnedBootStackSwitchByteOperation : byte
{
    Uninitialized = 0,
    Emit = 1,
    Verify = 2
}
