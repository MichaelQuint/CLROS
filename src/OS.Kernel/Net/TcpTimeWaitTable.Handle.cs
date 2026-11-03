namespace OS.Net;

public static unsafe partial class TcpTimeWaitTable
{
    /// <summary>Opaque address/epoch/generation identity, valid only while the
    /// corresponding record remains held. Slot reuse cannot revive old handles.</summary>

    public readonly struct Handle
    {
        internal readonly ulong Address, Epoch, Generation;
        internal Handle(Owner* owner, ushort index, ulong generation)
        {
            Address = (ulong)owner;
            Epoch = owner->Epoch;
            Generation = generation;
            Index = index;
        }

        public ushort Index
        {
            get;
        }
        public bool IsInitialized => Address != 0 && Epoch != 0 && Generation != 0;
    }
}
