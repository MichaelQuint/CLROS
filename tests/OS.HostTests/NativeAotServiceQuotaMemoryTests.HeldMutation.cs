using System.Text;
using OS.Tooling;

internal static partial class NativeAotServiceQuotaMemoryTests
{
    // Independent framed values only: no original ELF, call/write scheduler,
    // physical allocator, source pins, peer or actual guest is constructed.
    private enum HeldMutation
    {
        None,
        Advise,
        PartialUnmap,
        Shrink,
        GrowInto,
        FixedReplace,
        Protect
    }
}
