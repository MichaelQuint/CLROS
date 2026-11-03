#if !NOSTDLIB_EFI || M16_027_PROBE
using OS.Kernel.Memory;
using OS.Pal;
using W = OS.Kernel.Compatibility.LinuxX64.LinuxContinuingWritableDescriptorServices;
using C = OS.Storage.ContinuingFat32Mutation;

namespace OS.Kernel.Compatibility.LinuxX64;

public static unsafe partial class LinuxDescriptorServices
{
    /// <summary>Stable one-request POD independent of a thread Slot. It owns
    /// reservations/borrows until real completion or cancellation drain. No user
    /// buffer address, resolved pointer, caller Span, path or iovec is retained.
    /// An optional copied call ordinal and original instruction address identify
    /// the request but grant no gateway, process or user-copy authority.</summary>

    public struct WritableDiskRequest
    {
        internal ulong Self, Generation, BindingGeneration, Number, Offset;
        internal ulong CallOrdinal, OriginalRip;
        internal Token OriginalFd;
        internal W.RequestToken ProviderRequest;
        internal W.DescriptorToken TemporaryOpened;
        internal OSHostHandle Worker;
        internal long Value;
        internal C.Status Failure;
        internal uint Description, ReservedFd, ReservedDescription, ReservationGeneration, Requested, Wanted, OpenFlags;
        internal byte Phase, Operation;
        internal bool Positioned, Borrowed, Reserved, Orphan, Cancelling, CopyRevoked, Closed;
    }
}
#endif
