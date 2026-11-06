using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace OS.Tools.QemuRunner;

public static partial class WebSocketControls
{
    public readonly record struct Events(string TimeoutRow, string DropRow, ulong TimeoutClock, ulong DropClock);
}
