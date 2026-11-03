using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace OS.Tools.QemuRunner;
public static partial class WebSocketControls
{
    public sealed record Report(Exchange Deadline, Exchange Before, Exchange After, Events Observations);
}
