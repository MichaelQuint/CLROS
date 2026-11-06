using System.IO;
using System.Runtime.InteropServices;

internal static unsafe partial class KestrelSignalProbeAbi
{
    internal readonly record struct Result(long Value, int Errno);
}
