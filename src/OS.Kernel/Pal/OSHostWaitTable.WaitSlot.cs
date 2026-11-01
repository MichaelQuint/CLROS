namespace OS.Pal;
public sealed partial class OSHostWaitTable
{
    private struct WaitSlot
    {
        public bool InUse;
        public uint Generation;
        public OSHostHandle Waitable;
        public OSHostWaitableKind Kind;
        public bool Signaled;
        public bool Canceled;
        public static WaitSlot Create(OSHostHandle waitable, uint generation, OSHostWaitableKind kind, bool signaled)
        {
            return new WaitSlot
            {
                InUse = true,
                Generation = generation,
                Waitable = waitable,
                Kind = kind,
                Signaled = signaled,
                Canceled = false
            };
        }
    }
}
