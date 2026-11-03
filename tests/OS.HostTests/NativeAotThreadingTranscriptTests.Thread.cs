using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using OS.Tooling;

internal static partial class NativeAotThreadingTranscriptTests
{
    internal sealed class Thread(int slot, int tid, ulong generation = 1)
    {
        internal readonly ulong Id = (generation << 32) | (uint)slot;
        internal readonly int Tid = tid;
        internal ulong Bottom = 0x100100000 + (uint)tid * 0x30000UL, Top = 0x10011e000 + (uint)tid * 0x30000UL, Fs, Mask, Frame, Depth;
        internal ulong Rsp, Sequence, Ordinal;
    }
}
