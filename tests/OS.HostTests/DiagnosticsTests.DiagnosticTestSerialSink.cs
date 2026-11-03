using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using OS.Kernel.Diagnostics;

internal static partial class DiagnosticsTests
{
    private sealed class DiagnosticTestSerialSink : ISerialSink
    {
        private readonly List<string> _lines = new List<string>();
        public int Count
        {
            get
            {
                return _lines.Count;
            }
        }

        public void WriteLine(string line)
        {
            _lines.Add(line);
        }

        public string LineAt(int index)
        {
            return _lines[index];
        }
    }
}
