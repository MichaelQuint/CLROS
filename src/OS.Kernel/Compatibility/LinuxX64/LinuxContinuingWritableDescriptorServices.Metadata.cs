using C = OS.Storage.ContinuingFat32Mutation;

namespace OS.Kernel.Compatibility.LinuxX64;

public static unsafe partial class LinuxContinuingWritableDescriptorServices
{
    public readonly struct Metadata
    {
        public readonly ulong EntryOffset, Length, DescriptionGeneration;
        public readonly bool Valid, Directory;
        internal Metadata(Description* d)
        {
            EntryOffset = d->File.EntryOffset;
            Length = d->File.Length;
            DescriptionGeneration = d->Generation;
            Directory = d->Directory;
            Valid = true;
        }
    }
}
