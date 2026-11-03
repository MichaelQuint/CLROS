using System.Reflection;
using System.Runtime.Loader;
using System.Text;

internal static partial class NativeAotWebStartupTests
{
    private sealed class ConstructorFailureWriter(string rejectedLine) : TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
        internal StringBuilder Output { get; } = new();
        internal int Failures { get; private set; }

        public override void WriteLine(string? value)
        {
            if (value == rejectedLine)
            {
                Failures++;
                throw new IOException("controlled constructor logging failure");
            }

            Output.Append(value).Append('\n');
        }
    }
}
