using C = OS.Storage.ContinuingFat32Mutation;

namespace OS.Kernel.Compatibility.LinuxX64;
public static unsafe partial class LinuxContinuingWritableDescriptorServices
{
    public struct Description
    {
        internal ulong Generation, Position;
        internal C.FileToken File;
        internal ProcessToken Process;
        internal uint References, Borrowers, Flags;
        internal ushort NameLength;
        internal bool Active, Reserved, Directory;
        internal byte FlockMode;
        internal fixed byte Name[(int)PathBytes];
    }
#if !NOSTDLIB_EFI || M16_027_TIMING_PROBE
#endif
}
