using OS.Std.Hash;
using OS.Std.Memory;

namespace OS.Net;

public static unsafe partial class TcpInitialSequence
{
    /// <summary>Exact owner/epoch/generation/borrower identity; not authority to
    /// free backing. Copying a lease does not create another borrow. The borrower
    /// address must remain unique and mapped until exact Release.</summary>

    public readonly struct Lease
    {
        internal Lease(Owner* owner)
        {
            Address = (ulong)owner;
            Epoch = owner->Epoch;
            Generation = owner->Generation;
            Borrower = owner->Borrower;
        }

        internal readonly ulong Address, Epoch, Generation, Borrower;
        public bool IsValid => Address != 0 && Epoch != 0 && Generation != 0 && Borrower != 0;
    }
}
