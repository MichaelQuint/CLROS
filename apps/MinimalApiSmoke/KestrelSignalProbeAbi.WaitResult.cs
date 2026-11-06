using System.IO;
using System.Runtime.InteropServices;

internal static unsafe partial class KestrelSignalProbeAbi
{
    internal readonly record struct WaitResult(long Value, int Errno, uint Events, ulong Data);
}
