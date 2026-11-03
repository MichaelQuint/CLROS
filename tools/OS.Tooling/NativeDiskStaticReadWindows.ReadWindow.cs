namespace OS.Tooling;
public static partial class NativeDiskStaticReadWindows
{
    /// <summary>Copied positive read/pread facts from an independently checked
    /// original pending call. Begin/Complete are original serial record positions,
    /// not IO-list positions or clocks. Descriptor, descriptor-request and block
    /// request generations are separate identities and must not be equated here.
    /// OutputPrefix is the original admitted writable prefix, before consumption.</summary>

    public readonly record struct ReadWindow(ulong Identity, ulong Ordinal, ulong Number, ulong DescriptorGeneration, ulong RequestGeneration, int BeginRecordIndex, int CompleteRecordIndex, ulong Inode, ulong Offset, ulong Requested, uint OutputPrefix, uint ReturnedBytes)
    {
        public uint Descriptor { get; init; }
    }
}
