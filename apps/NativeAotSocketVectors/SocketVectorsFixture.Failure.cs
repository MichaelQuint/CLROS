using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace OS.Apps.NativeAotSocketVectors;

public static partial class SocketVectorsFixture
{
    private sealed class Failure(int code) : Exception
    {
        internal int Code => code;
    }
}
