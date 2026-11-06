using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace OS.Apps.NativeAotSocketVectors;

public static partial class SocketVectorsFixture
{
    private sealed class Budget
    {
        private readonly Stopwatch elapsed = Stopwatch.StartNew();
        private int operations;
        internal void Check()
        {
            if (++operations > MaximumOperations || elapsed.ElapsedMilliseconds >= DeadlineMilliseconds)
                throw new Failure(2);
        }
    }
}
