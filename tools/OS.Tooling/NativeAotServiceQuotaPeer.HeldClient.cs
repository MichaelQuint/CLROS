using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using OS.Tools.QemuRunner;

namespace OS.Tooling;

public static partial class NativeAotServiceQuotaPeer
{
    private sealed class HeldClient(TcpClient client, Stream stream) : IDisposable
    {
        internal Socket Socket => client.Client;
        internal Stream Stream => stream;

        public void Dispose()
        {
            try
            {
                stream.Dispose();
            }
            finally
            {
                client.Dispose();
            }
        }
    }
}
