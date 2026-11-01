using OS.Pal;

namespace OS.Kernel.Threading;
public unsafe partial struct WaitAddress
{
    /// <summary>Opaque generation plus stable owner and backing lifetime.
    /// Copying a binding does not pin its word or acquire wake/release authority.</summary>

    public readonly struct Binding
    {
        public readonly ulong Value, Lifetime;
        internal readonly ulong Owner, OwnerKey;
        public bool IsValid => Value != 0 && Lifetime != 0 && Owner != 0 && OwnerKey != 0;

        internal Binding(ulong value, ulong lifetime, ulong owner, ulong ownerKey)
        {
            Value = value;
            Lifetime = lifetime;
            Owner = owner;
            OwnerKey = ownerKey;
        }
    }
}
