using System.Buffers.Binary;
using System.Text;
using Block = OS.Storage.WritableBlockSession;

namespace OS.Tooling;

public static partial class LinuxWritableDiskProofFixture
{
    private sealed record Built(byte[] Elf, CallSpec[] Calls, ulong[] Instructions, ulong MainEnd);
}
