using System.Buffers.Binary;
using System.Text;

namespace OS.Tooling;
public static partial class LinuxDiskRaceProofFixture
{
    /// <summary>Copied scalar syscall inventory. Child has no synthetic clone
    /// call: CloneReturnRip identifies its initial saved continuation. Output
    /// ceiling is the initially mapped writable range for a disk completion;
    /// result is the checked final value (Join additionally admits-EAGAIN).
    /// Role binds cross-worker requirements without exposing writer objects.</summary>

    public readonly record struct CallSpec(string Role, ulong Number, ulong A0, ulong A1, ulong A2, ulong A3, ulong A4, ulong A5, ulong ReturnRip, long Result, WaitPolicy Wait, uint OutputBytes);
}
