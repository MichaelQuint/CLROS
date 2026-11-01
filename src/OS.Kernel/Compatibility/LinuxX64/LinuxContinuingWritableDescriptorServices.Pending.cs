using C = OS.Storage.ContinuingFat32Mutation;

namespace OS.Kernel.Compatibility.LinuxX64;
public static unsafe partial class LinuxContinuingWritableDescriptorServices
{
    internal struct Pending
    {
        internal ulong Generation, DescriptionGeneration, Offset;
        internal DescriptorToken Original;
        internal C.RequestToken EngineRequest;
        internal C.FileToken File;
        internal C.OpenFacts StatFacts;
        internal C.RootFacts RootFacts;
        internal ProcessToken Process;
        internal FreeSearch Search;
        internal Operation Kind;
        internal Step Step;
        internal uint Description, Number, Flags, Bytes, Expected, AliasAttempts, NewLength, GrowthCount;
        internal ushort NameLength;
        internal bool Active, Ready, Cancelled, Discard, Positioned, Borrowed, RevisionAccepted, GrowAllowed, SearchRequested, SearchComplete, DirectoryRequired, EngineQueued;
        internal long Value;
        internal C.Status Failure;
        internal fixed byte Name[(int)PathBytes];
        internal fixed uint Growth[C.MaximumGrowthClusters];
    }
#if !NOSTDLIB_EFI || M16_027_TIMING_PROBE
#endif
}
