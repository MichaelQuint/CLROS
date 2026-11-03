using System.Text;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    private sealed partial class ThreadingProtocol
    {
        private void ContainmentClose(SocketDescriptor descriptor)
        {
            Need(descriptor.Connection is >= 0 and < 3 && descriptor.Reset == 0, "socket_containment_connection");
            Need(descriptor.Connection == 0 ? descriptor.Received == 1 && descriptor.Sent == containmentCapture!.Application.Queued && descriptor.SendBlocked && descriptor.TimedOut == 1 && descriptor.Eof == 1 && descriptor.ShutdownErrors == 1 && descriptor.WriteClosed : descriptor.Received == 16 && descriptor.Sent == 15 && descriptor.TimedOut == 0 && descriptor.Eof == 0 && descriptor.ShutdownErrors == 0 && !descriptor.WriteClosed, "socket_containment_connection_io");
        }

        private void ContainmentIo(SocketDescriptor descriptor, ulong nr, ulong requested, long result)
        {
            bool expiredPeer = descriptor.Connection == 0, send = nr == 44;
            Need(descriptor.Connection is >= 0 and < 3 && !descriptor.WriteClosed && (result == -11 || result >= 0 && result <= (long)Math.Min(requested, 8192) || expiredPeer && result is -110 or -32), "socket_containment_io_result");
            if (result > 0)
            {
                Need(descriptor.TimedOut == 0 && (send ? expiredPeer ? !descriptor.SendBlocked && descriptor.Sent + (ulong)result < 65536 : requested == 1 && result == 1 && descriptor.Sent < 15 && descriptor.Received == descriptor.Sent + 2 : requested == 1 && result == 1 && descriptor.Received < (expiredPeer ? 1UL : 16UL) && (expiredPeer || descriptor.Received <= descriptor.Sent + 1)), "socket_containment_io_bytes");
                if (send)
                {
                    descriptor.Sent += (ulong)result;
                    socketSent += (ulong)result;
                }
                else
                {
                    descriptor.Received += (ulong)result;
                    socketReceived += (ulong)result;
                }
            }
            else if (result == -11)
            {
                Need(descriptor.TimedOut == 0, "socket_containment_after_timeout");
                if (send && expiredPeer)
                {
                    Need(descriptor.Sent >= 32768, "socket_containment_saturation");
                    descriptor.SendBlocked = true;
                }
            }
            else if (result == -110)
            {
                Need(expiredPeer && !send && requested == 1 && descriptor.TimedOut == 0 && descriptor.SendBlocked && containmentTimeoutObserved && containmentDropObserved, "socket_containment_timeout_call");
                descriptor.TimedOut++;
            }
            else if (result == 0)
            {
                Need(expiredPeer && !send && requested == 1 && descriptor.TimedOut == 1 && descriptor.Eof == 0, "socket_containment_eof_call");
                descriptor.Eof++;
            }
            else
            {
                // The pinned Unix SocketErrorPal maps EPIPE to managed
                // SocketError.Shutdown; ESHUTDOWN maps to Disconnecting.
                Need(expiredPeer && send && requested == 1 && result == -32 && descriptor.TimedOut == 1 && descriptor.Eof == 1 && descriptor.ShutdownErrors == 0, "socket_containment_shutdown_call");
                descriptor.ShutdownErrors++;
                descriptor.WriteClosed = true;
            }
        }
    }
}
