using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using OS.Tooling;

// Deterministic HTTP-handler controls exercise owned logical requests and
// retained bytes only. They establish no real socket, signal or guest result.
internal static partial class NativeAotWebSignalRaceAttemptTests
{
    private sealed class Peer(string mode, CancellationTokenSource stop) : HttpMessageHandler
    {
        internal int Calls;
        internal bool ChurnJoined;
        internal BodyStream? Stream;
        private bool churn;
        private static HttpResponseMessage Reply(string body) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "text/plain")
        };
        private HttpResponseMessage StreamReply(byte[] bytes, string terminal, string media = "text/plain")
        {
            Stream = new(bytes, terminal, stop);
            var content = new StreamContent(Stream);
            content.Headers.ContentType = new MediaTypeHeaderValue(media);
            return new(HttpStatusCode.OK)
            {
                Content = content
            };
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Calls++;
            string path = request.RequestUri!.AbsolutePath;
            if (mode is "headers-pending" or "join-fault")
            {
                if (request.Method == HttpMethod.Post)
                {
                    if (path == "/__clros/churn")
                    {
                        churn = true;
                        return mode == "headers-pending" ? Task.FromResult(StreamReply([65], "wait")) : Hold(token);
                    }

                    return Task.FromException<HttpResponseMessage>(new HttpRequestException("secret-rich request message"));
                }

                if (path == "/healthz")
                    return Task.FromResult(Reply(Health));
                if (path == "/__clros/signalrace/status")
                    return Task.FromResult(Reply("CLROS_SIGNAL_RACE_STATUS phase=0 pid=0 waiter=0 actor=0 callback=0 recoveryRequested=0"));
                int active = churn ? 1 : 0;
                return Task.FromResult(Reply($"CLROS_CHURN_STATUS active={active} waiting={active} peakActive={active} started={active} completed=0 canceled=0 failed=0 rejected=0 timers={active} workers={active} collections={active} heldBytes={active * 65536} peakHeldBytes={active * 65536}"));
            }

            if (mode == "before-headers")
                return Task.FromException<HttpResponseMessage>(new HttpRequestException("secret-rich request message"));
            if (mode == "status")
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("rejected status body", Encoding.UTF8, "text/plain") });
            return Task.FromResult(mode switch
            {
                "partial" => StreamReply([65, 255], "fault"),
                "utf8" => StreamReply([65, 255], "eof"),
                "cancel" => StreamReply([65, 255], "cancel"),
                "overflow" => StreamReply(new byte[4097], "eof"),
                "media" => StreamReply([65], "eof", "application/json"),
                _ => throw new InvalidOperationException("Unexpected mode")
            });
        }

        private async Task<HttpResponseMessage> Hold(CancellationToken token)
        {
            try
            {
                await Task.Delay(Timeout.Infinite, token);
                throw new InvalidOperationException("Unexpected completion");
            }
            finally
            {
                ChurnJoined = true;
            }
        }
    }
}
