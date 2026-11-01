namespace OS.Boot;
/// <summary>
/// Identifies the caller-attested lifetime of one owned boot-stack layout.
/// Existing values are append-only and the zero-valued default is invalid.
/// </summary>

public enum OwnedBootStackLifetime : byte
{
    Uninitialized = 0,
    Reserved = 1,
    Ready = 2,
    Active = 3
}
