namespace OS.Net;

public static unsafe partial class TcpConnectionTable
{
    /// <summary>Opaque borrowed identity, not a reference or ownership transfer.
    /// Owner address, lifetime epoch and slot generation all participate. Retain
    /// the owner backing; copied handles become stale after release/reuse.</summary>

    public readonly struct Handle
    {
        internal readonly ulong OwnerAddress, Epoch, Generation;
        internal readonly ushort Index;
        internal Handle(Owner* owner, ushort index)
        {
            OwnerAddress = (ulong)owner;
            Epoch = owner->Epoch;
            Index = index;
            Generation = owner->Generations[index];
        }

        public bool IsInitialized => OwnerAddress != 0 && Epoch != 0 && Generation != 0;
        public int SlotIndex => Index;
    }
}
