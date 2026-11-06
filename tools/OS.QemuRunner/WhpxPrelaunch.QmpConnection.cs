using System.IO;
using System.Net.Sockets;

namespace OS.Tools.QemuRunner;

internal sealed partial class WhpxPrelaunch
{
    /// <summary>Owns the accepted monitor socket beyond the prelaunch handshake.</summary>
    /// <remarks>
    /// The caller transfers the accepted client and borrows its stream without
    /// disposing it. Handshake writers must leave that stream open. Only the
    /// owning runner may close the connection after its child has joined; false
    /// admission rejects without closing it. Disposal is idempotent and introduces
    /// no protocol commands, guest resources or changed stderr/deadline policy.
    /// </remarks>
    private sealed class QmpConnection
    {
        private readonly TcpClient client;

        internal QmpConnection(TcpClient client) => this.client = client;

        internal NetworkStream Stream => client.GetStream();

        internal void CloseAfterChildJoin(bool joined)
        {
            if (!joined)
                throw new IOException("whpx_prelaunch_qmp_child_unjoined");
            client.Dispose();
        }
    }
}
