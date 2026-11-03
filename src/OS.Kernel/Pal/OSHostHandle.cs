namespace OS.Pal;

/// <summary>Eight-byte opaque ABI value with zero as the invalid sentinel.
/// No allocation, resource ownership or lifetime is acquired by constructing or
/// copying it. The supplying subsystem defines identity/generation semantics and
/// independently validates a live owner before use; IsValid checks only nonzero.
/// Natural sequential layout permits the same canonical source in the host and
/// explicit no-stdlib image without importing host interop attributes.</summary>
public readonly struct OSHostHandle
{
    public readonly ulong Value;
    public OSHostHandle(ulong value)
    {
        Value = value;
    }
    public bool IsValid => Value != 0;
}
