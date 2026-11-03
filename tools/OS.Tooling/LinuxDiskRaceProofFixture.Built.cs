using System.Buffers.Binary;
using System.Text;

namespace OS.Tooling;

public static partial class LinuxDiskRaceProofFixture
{
    private sealed record Built(byte[] Elf, CallSpec[] MainCalls, CallSpec[] ChildCalls, ulong[] MainInstructions, ulong[] ChildInstructions, ulong CloneCall, ulong MainEnd, ulong ChildEnd, CallSpec[]? HandlerCalls = null, ulong[]? HandlerInstructions = null, ulong HandlerEnd = 0, ulong RestorerEnd = 0);
}
