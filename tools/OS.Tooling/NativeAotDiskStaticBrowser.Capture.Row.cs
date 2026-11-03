using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OS.Tooling;
public static partial class NativeAotDiskStaticBrowser
{
    private sealed partial class Capture
    {
        internal sealed class Row(string id)
        {
            public string Id = id;
            public JsonElement Request, RequestExtra, Response, ResponseExtra, Finished;
        }
    }
}
