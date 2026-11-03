using System.Runtime.InteropServices;
using OS.Drivers;
using OS.Kernel.Platform;

internal static unsafe partial class QueueTests
{
    internal struct QueueRegisters
    {
        public ulong Descriptor, Available, Used;
        public ushort Maximum, Size, Vector, Enabled, NotifyOffset;
    }
}
