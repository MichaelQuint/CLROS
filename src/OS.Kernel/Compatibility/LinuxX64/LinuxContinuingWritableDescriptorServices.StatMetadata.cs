using C = OS.Storage.ContinuingFat32Mutation;

namespace OS.Kernel.Compatibility.LinuxX64;

public static unsafe partial class LinuxContinuingWritableDescriptorServices
{
    /// <summary>Fresh copied regular-file or mount-root allocation facts.
    /// Facts is valid only when Directory is false; Root only when true. Path
    /// requests have DescriptionGeneration0 and never authenticate an fd.
    /// CopyStat rechecks the original process/request/revision and any borrowed
    /// descriptor. No user-copy or later-volume authority follows.</summary>

    public readonly struct StatMetadata
    {
        public readonly C.OpenFacts Facts;
        public readonly C.RootFacts Root;
        public readonly ulong DescriptionGeneration;
        public readonly bool Directory;
        internal StatMetadata(C.OpenFacts facts, C.RootFacts root, ulong generation, bool directory)
        {
            Facts = facts;
            Root = root;
            DescriptionGeneration = generation;
            Directory = directory;
        }
    }
#if !NOSTDLIB_EFI || M16_027_TIMING_PROBE
#endif
}
