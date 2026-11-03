using System.IO;
using System.Runtime.InteropServices;

internal static unsafe partial class KestrelSignalProbeAbi
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct EpollEvent
    {
        internal uint Events;
        internal ulong Data;
    }
}
