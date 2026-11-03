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
    private readonly record struct PressureAbort(long Ticks, string Error);
}
