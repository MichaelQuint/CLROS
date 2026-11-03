using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using OS.Tooling;
using Vector = NativeAotThreadingTranscriptTests.Vector;
using Thread = NativeAotThreadingTranscriptTests.Thread;

internal static partial class NativeAotRuntimeTranscriptTests
{
    // Independent complete observations, not guest captures or sealed authority.
    private sealed class BroadVector
    {
        internal readonly Vector Core = new(true);
        internal readonly StringBuilder Stdout = new();
        internal readonly string App;
        internal BroadVector(string app)
        {
            App = app;
            Core.Start();
            Core.Rows[1] = Core.Rows[1].Replace("entropyBytes=32", "entropyBytes=256", StringComparison.Ordinal);
            int size = app == "NativeAotThreadPoolStress" ? 1627408 : app == "NativeAotRuntimeMatrix" ? 1577616 : 1576912;
            Core.Rows[2] = Core.Rows[2].Replace("packageBytes=1577712", "packageBytes=" + size.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
            Core.Rows[4] = Core.Rows[4].Replace("workers=8", "workers=16", StringComparison.Ordinal);
            for (int i = 0; i < 7; i++)
                Core.Call(318, 16, [0x500600, 16, 0]); // Exercise the actual larger entropy contract.
        }

        internal void Write(string line) => WriteBytes(line + "\n");
        internal void WriteBytes(string text)
        {
            int bytes = Encoding.UTF8.GetByteCount(text);
            Stdout.Append(text);
            Core.Lengths.Add(bytes);
            string data = text.Replace("\r", "", StringComparison.Ordinal).Replace("\n", "", StringComparison.Ordinal);
            Core.Rows.Add($"M16_005_WRITE fd=1 bytes={bytes} data={data}");
            Core.Call(1, bytes, [1, 0x500400, (uint)bytes]);
        }

        internal QemuTranscripts.ThreadingProtocolReport Inspect(string[]? rows = null, string? text = null, ulong frequency = 100_000_000) => QemuTranscripts.InspectNativeAotRuntimeProtocolForTests(App, rows ?? Core.Rows.ToArray(), NativeAotThreadingTranscriptTests.Elf(), r => r == 0x400202, r => r == 0x400202, Core.Lengths, text ?? Stdout.ToString(), 8192, 12288, frequency);
    }
}
