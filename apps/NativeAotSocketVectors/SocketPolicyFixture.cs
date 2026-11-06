using System.IO;
using System.Net;
using System.Net.Sockets;

namespace OS.Apps.NativeAotSocketVectors;

/// <summary>Finite guest-only socket policy check: IPv4 Any:8080 is admitted,
/// 10.0.2.2 is foreign, and the fd quota is 16. Standard I/O and the runtime's
/// wake pipe and epoll port retain six descriptors, leaving ten socket slots.
/// Run exclusively before creating the vector listener. Windows/Linux host
/// policy is not equivalent. Every owned socket is disposed before its case row;
/// unexpected socket outcomes return false with a bounded numeric diagnostic.</summary>
public static class SocketPolicyFixture
{
    /// <summary>Execute real Socket operations, retaining no socket on return.
    /// The caller owns output and must keep it writable. No peer, mock, native
    /// import or background work is used; only the guest --policy mode selects it.</summary>
    public static bool Run(TextWriter output)
    {
        int current = 0, expected = 10013;
        try
        {
            int actual = BindError(IPAddress.Any, 8081);
            if (actual != expected)
                return Fail(output, current, expected, actual);
            output.WriteLine("SOCKET_VECTOR_POLICY case=0 address=0 port=8081 socket_error=10013");
            current = 1;
            actual = BindError(new IPAddress(new byte[] { 10, 0, 2, 2 }), 8080);
            if (actual != expected)
                return Fail(output, current, expected, actual);
            output.WriteLine("SOCKET_VECTOR_POLICY case=1 address=167772674 port=8080 socket_error=10013");
            current = 2;
            expected = 10048;
            actual = DuplicateBindError();
            if (actual != expected)
                return Fail(output, current, expected, actual);
            output.WriteLine("SOCKET_VECTOR_POLICY case=2 address=0 port=8080 socket_error=10048");
            current = 3;
            expected = 10024;
            actual = CapacityError(out int opened);
            if (opened != 10)
                return Fail(output, current, 10, opened);
            if (actual != expected)
                return Fail(output, current, expected, actual);
            output.WriteLine("SOCKET_VECTOR_POLICY case=3 opened=10 limit=16 socket_error=10024");
            return true;
        }
        catch (SocketException failure)
        {
            return Fail(output, current, expected, (int)failure.SocketErrorCode);
        }
    }

    private static Socket Create() => new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

    private static int BindError(IPAddress address, int port)
    {
        using Socket socket = Create();
        try
        {
            socket.Bind(new IPEndPoint(address, port));
            return 0;
        }
        catch (SocketException failure)
        {
            return (int)failure.SocketErrorCode;
        }
    }

    private static int DuplicateBindError()
    {
        using Socket listener = Create();
        listener.Bind(new IPEndPoint(IPAddress.Any, 8080));
        listener.Listen(1);
        using Socket duplicate = Create();
        try
        {
            duplicate.Bind(new IPEndPoint(IPAddress.Any, 8080));
            return 0;
        }
        catch (SocketException failure)
        {
            return (int)failure.SocketErrorCode;
        }
    }

    private static int CapacityError(out int opened)
    {
        Socket?[] held = new Socket?[16];
        opened = 0;
        try
        {
            for (; opened < held.Length; opened++)
            {
                try
                {
                    held[opened] = Create();
                }
                catch (SocketException failure)
                {
                    return (int)failure.SocketErrorCode;
                }
            }
            return 0;
        }
        finally
        {
            for (int index = opened - 1; index >= 0; index--)
                held[index]!.Dispose();
        }
    }

    private static bool Fail(TextWriter output, int current, int expected, int actual)
    {
        output.WriteLine(FormattableString.Invariant($"SOCKET_VECTOR_POLICY_FAIL case={current} expected={expected} actual={actual}"));
        return false;
    }
}
