using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using OS.Tooling.Native;

namespace OS.Tooling;

public static partial class NativeAotWebPayload
{
    private sealed class RaceRoutingHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, HttpMessageInvoker> handlers = new(StringComparer.Ordinal);
        private readonly int port;
        internal RaceRoutingHandler(NativeAotServiceQuotaPeer.WireCapture wire, int port)
        {
            this.port = port;
            foreach (string role in new[]
            {
                "control",
                "churn",
                "race"
            }

            )
            {
                string ownedRole = role;
                var handler = new SocketsHttpHandler
                {
                    UseProxy = false,
                    AllowAutoRedirect = false,
                    UseCookies = false,
                    MaxConnectionsPerServer = 1,
                    ConnectTimeout = TimeSpan.FromSeconds(10),
                    MaxResponseHeadersLength = 8
                };
                handler.ConnectCallback = async (context, token) =>
                {
                    RaceNeed(context.DnsEndPoint.Host == "127.0.0.1" && context.DnsEndPoint.Port == port, "wire_connect_target");
                    return await wire.ConnectAsync(ownedRole, token);
                };
                handlers.Add(role, new(handler, disposeHandler: true));
            }
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            RaceNeed(request.RequestUri is { } uri && uri.Scheme == "http" && uri.Host == "127.0.0.1" && uri.Port == port && uri.UserInfo == "" && uri.Query == "" && uri.Fragment == "", "wire_request_target");
            request.Version = HttpVersion.Version11;
            request.VersionPolicy = HttpVersionPolicy.RequestVersionExact;
            string role = NativeAotServiceQuotaPeer.SignalRaceWireRole(request.Method.Method, request.RequestUri!.AbsolutePath);
            return handlers[role].SendAsync(request, token);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                foreach (var handler in handlers.Values)
                    handler.Dispose();
            base.Dispose(disposing);
        }
    }
}
