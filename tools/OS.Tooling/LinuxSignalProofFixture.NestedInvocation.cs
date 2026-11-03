using System.Buffers.Binary;
using System.Text;
using Writer = OS.Tooling.LinuxThreadProofFixture.Writer;

namespace OS.Tooling;
public static partial class LinuxSignalProofFixture
{
    /// <summary>One actual syscall in nested execution order. Depth0 belongs to
    /// the parent. A restorer executes at that depth's handler RSP+8 after RET;
    /// all other handler calls retain its entry RSP. Inventories describe bytes,
    /// not observed delivery, restoration or native execution.</summary>

    public readonly record struct NestedInvocation(uint Number, ulong ReturnRip, uint Depth, bool Restorer);
}
