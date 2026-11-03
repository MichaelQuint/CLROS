using System.Buffers.Binary;
using System.Text;

namespace OS.Tooling;

public static partial class LinuxDiskRaceProofFixture
{
    /// <summary>Fixed user-memory ownership for native sentinel/data joins.
    /// The child stack occupies the page below ChildStackTop, outside both
    /// output ranges. ClearTid starts at2 and must become0 on actual child exit.
    /// These addresses grant no permission to read an unvalidated user mapping.</summary>

    public readonly record struct MemoryLayout(ulong ParentBuffer, uint ParentBytes, ulong ChildBuffer, uint ChildBytes, byte Fill, ulong ChildStack, ulong ChildTlsBase, ulong ClearTidAddress, ulong ParentTidAddress, ulong PipePairAddress, ulong PayloadAddress, ulong PipeOutputAddress);
}
