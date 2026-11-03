using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OS.Tooling;

// Real in-process host Kestrel exercises peer ownership only. Synthetic
// readiness and runner values grant no CLROS, native, disk or guest authority.
internal static partial class NativeAotDiskStaticCaptureTests
{
    private enum Mode
    {
        Healthy,
        WrongStatus,
        CancelBody,
        ResetHeaders
    }
}
