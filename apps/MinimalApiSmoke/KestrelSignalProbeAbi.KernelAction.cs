using System.IO;
using System.Runtime.InteropServices;

internal static unsafe partial class KestrelSignalProbeAbi
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct KernelAction
    {
        internal ulong Handler, Flags, Restorer, Mask;
    }
}
