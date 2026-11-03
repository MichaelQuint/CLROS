using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;

namespace OS.Tooling;

public static partial class NativeAotServiceQuotaPeer
{
    internal sealed partial class WireCapture
    {
        private sealed class Connected(WireConnection value)
        {
            internal WireConnection Value = value;
            internal long Read, Written;
            internal bool ReadUnknown, WriteUnknown, Reading, Writing;
        }
    }
}
