using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using OS.Kernel.Compatibility.LinuxX64;

internal static unsafe partial class LinuxSyscallGatewayTests
{
    private struct Model
    {
        public byte* User;
        public byte* Output;
        public uint Copies, Writes, Bytes, Descriptor;
        public bool MissingSecond, CopyFails, SinkFails, High, RemovedCode;
        public uint ThreadCalls, ThreadMode;
        public long ThreadValue;
    }
}
