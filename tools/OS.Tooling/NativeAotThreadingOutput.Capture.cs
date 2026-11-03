using System.Text;

namespace OS.Tooling;
public static partial class NativeAotThreadingOutput
{
    /// <summary>Immutable derived text and original write byte lengths. No raw
    /// input or mutable array is exposed; this receipt grants no execution authority.</summary>

    public sealed class Capture
    {
        internal Capture(string stdout, string normalizedTail, int[] lengths, uint[] channels, string stderr)
        {
            Stdout = stdout;
            NormalizedTail = normalizedTail;
            WriteLengths = Array.AsReadOnly((int[])lengths.Clone());
            WriteChannels = Array.AsReadOnly((uint[])channels.Clone());
            Stderr = stderr;
        }

        public string Stdout { get; }
        public string NormalizedTail { get; }
        public IReadOnlyList<int> WriteLengths { get; }
        public IReadOnlyList<uint> WriteChannels { get; }
        public string Stderr { get; }
    }
}
