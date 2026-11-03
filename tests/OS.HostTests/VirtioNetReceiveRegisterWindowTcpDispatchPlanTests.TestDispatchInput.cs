using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using OS.Boot;
using OS.Drivers;
using OS.Kernel.Diagnostics;
using OS.Net;
using OS.Std.Binary;

internal static partial class VirtioNetReceiveRegisterWindowTcpDispatchPlanTests
{
    private readonly struct TestDispatchInput
    {
        public TestDispatchInput(VirtioNetReceiveReadinessResult readiness, byte[] queueMemory, byte[] receiveBufferSlab, int receiveBufferLength)
        {
            Readiness = readiness;
            QueueMemory = queueMemory;
            ReceiveBufferSlab = receiveBufferSlab;
            ReceiveBufferLength = receiveBufferLength;
        }

        public VirtioNetReceiveReadinessResult Readiness { get; }
        public byte[] QueueMemory { get; }
        public byte[] ReceiveBufferSlab { get; }
        public int ReceiveBufferLength { get; }
    }
}
