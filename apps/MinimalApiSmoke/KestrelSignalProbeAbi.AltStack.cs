using System.IO;
using System.Runtime.InteropServices;

internal static unsafe partial class KestrelSignalProbeAbi
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct AltStack
    {
        internal ulong Address;
        internal uint Flags, Padding;
        internal ulong Bytes;
    }
}
