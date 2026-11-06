using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using OS.Boot;
using OS.Kernel.Diagnostics;

internal static unsafe partial class VerifiedExitPathOptDefaultRepeatedCleanQEMUTests
{
    private struct Capture
    {
        public fixed byte Bytes[256];
        public int Count, Limit, Calls;
    }
}
