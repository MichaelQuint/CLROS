using OS.Kernel.Runtime;

namespace OS.Runtime;
#pragma warning disable CS0649
#pragma warning restore CS0649
internal struct RhpObjectObservation
{
    internal ulong Id, Route, MethodTable, Prefix0, Prefix1, Prefix2, Flags, BaseSize;
    internal ulong Allocation, Reference, End, UsedBefore, UsedAfter, Count;
    internal ulong Sync, ObservedMt, ZeroBytes, ZeroOr;
    internal ulong ScalarOffset, Scalar, UntouchedBytes, UntouchedOr;
    // Array-only observations stay in caller-owned POD storage, never another heap cursor.
    internal ulong Length, ElementTable, ElementPrefix0, ElementPrefix1, ElementPrefix2, ScalarWidth;
    // String's related Object is not an array element. All 24 bytes are retained separately.
    internal ulong RelatedObjectTable, RelatedObjectPrefix0, RelatedObjectPrefix1, RelatedObjectPrefix2;
}
#pragma warning disable CS0649
#pragma warning restore CS0649
#if NOSTDLIB_EFI && M04_RHP_NEW_FAST_PROBE











#pragma warning disable 649

#pragma warning restore 649
#endif
