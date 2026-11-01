using OS.Pal;

namespace OS.Kernel.Threading;
public unsafe partial struct ThreadLocalStorage
{
    /// <summary>Opaque TLS slot generation plus actual lifecycle thread identity.
    /// Copying it acquires no pin. Owner keys must never repeat across lifetimes.</summary>

    public readonly struct Binding
    {
        public readonly ulong Value;
        public readonly OSHostHandle Thread;
        internal readonly ulong Owner, Key;
        public bool IsValid => Value != 0 && Thread.IsValid && Owner != 0 && Key != 0;

        internal Binding(ulong value, OSHostHandle thread, ulong owner, ulong key)
        {
            Value = value;
            Thread = thread;
            Owner = owner;
            Key = key;
        }
    }
}
