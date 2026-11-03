using System.Buffers.Binary;
using System.Text;
using Writer = OS.Tooling.LinuxThreadProofFixture.Writer;

namespace OS.Tooling;

public static partial class LinuxSignalProofFixture
{
    /// <summary>One emitted instruction boundary, with the completed syscall
    /// count for its actual worker. Rsp=0 is only the initial entry instruction,
    /// whose stack is separately derived from the canonical argc/argv/env/auxv
    /// input. Loop identifies the syscall-free parent activation JMP. These are
    /// construction inventories, not observations or native execution evidence.</summary>

    public readonly record struct IrqPoint(ulong Rip, int Calls, bool Child, bool Handler, ulong Rsp, ulong Fs, ulong Gs, bool Direction, bool Loop);
}
