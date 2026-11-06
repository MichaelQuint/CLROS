using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using OS.Apps.NativeAotSocketVectors;

internal static partial class NativeAotSocketVectorsTests
{
    private sealed class Markers : TextWriter
    {
        private readonly List<string> rows = [];
        private readonly AutoResetEvent changed = new(false);
        public override Encoding Encoding => Encoding.UTF8;

        internal string[] Lines
        {
            get
            {
                lock (rows)
                    return rows.ToArray();
            }
        }

        internal string Text => string.Join(Environment.NewLine, Lines);

        public override void WriteLine(string? value)
        {
            lock (rows)
                rows.Add(value ?? "");
            changed.Set();
        }

        internal void WaitFor(string prefix)
        {
            Stopwatch deadline = Stopwatch.StartNew();
            while (!Lines.Any(row => row.StartsWith(prefix, StringComparison.Ordinal)))
            {
                HostCheck.True(deadline.ElapsedMilliseconds < 30_000 && !Lines.Any(row => row.StartsWith("TEST_FAIL ", StringComparison.Ordinal)), Text);
                changed.WaitOne(50);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                changed.Dispose();
            base.Dispose(disposing);
        }
    }
}
