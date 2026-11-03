using System.Buffers.Binary;
using System.Text;

namespace OS.Tooling;
public static partial class LinuxDiskRaceProofFixture
{
    /// <summary>None is immediate; Disk always owns a source4 wait. PipeRead
    /// may return4 immediately or park on source2. Join may return-EAGAIN after
    /// child exit or park on source1 and return0. These are exact finite fixture
    /// alternatives, not authority to invent a wait or a completion.</summary>

    public enum WaitPolicy : byte
    {
        None,
        Disk,
        PipeRead,
        Join
    }
}
