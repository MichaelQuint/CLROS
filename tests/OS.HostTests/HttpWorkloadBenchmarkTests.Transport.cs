using System.Net;
using System.Net.Http.Headers;
using System.Text;
using OS.Tooling;

internal static partial class HttpWorkloadBenchmarkTests
{
    private sealed class Transport(WebRootDiskInventory.Inspection inventory, byte[] asset, string mode) : HttpMessageHandler
    {
        private readonly DiskStaticHttpContract.Asset metadata = new(
            inventory.Find(WebRootDiskInventory.Root + NativeAotDiskTimingPeer.Target)!, true);
        private TaskCompletionSource pairReady = NewSignal();
        private int requests;
        private int inFlight;
        private int pairCount;
        private int activeBodies;

        internal int Requests => requests;
        internal int ActiveBodies => activeBodies;
        internal int MaximumInFlight
        {
            get; private set;
        }
        internal bool Disposed
        {
            get; private set;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
        {
            int ordinal = Interlocked.Increment(ref requests) - 1;
            int current = Interlocked.Increment(ref inFlight);
            MaximumInFlight = Math.Max(MaximumInFlight, current);
            try
            {
                if (ordinal % 8 is 5 or 6)
                {
                    if (Interlocked.Increment(ref pairCount) == 2)
                        pairReady.TrySetResult();
                    await pairReady.Task.WaitAsync(cancellation);
                    if (ordinal % 8 == 6)
                    {
                        pairCount = 0;
                        pairReady = NewSignal();
                    }
                }

                string path = request.RequestUri!.AbsolutePath;
                bool head = request.Method == HttpMethod.Head;
                bool ranged = request.Headers.Range is not null;
                bool file = path == NativeAotDiskTimingPeer.Target;
                byte[] bytes = file ? head ? [] : ranged ? asset[..65536] : asset.ToArray() :
                    path == "/healthz" ? "OS_MINIMAL_API_OK"u8.ToArray() :
                    Encoding.UTF8.GetBytes("{\"profile\":\"v1\",\"ready\":true,\"message\":\"CLROS ready — café\"}");
                if (mode == "json" && ordinal == 0 || mode == "concurrent-failure" && ordinal == 6)
                    bytes = "{}"u8.ToArray();
                if (mode == "oversized-json" && ordinal == 0)
                    bytes = new byte[1025];
                if (mode == "health" && path == "/healthz" || mode == "range-body" && ranged ||
                    mode == "asset-body" && ordinal == 4)
                    bytes[0] ^= 1;
                string streamMode = mode == "body-timeout" && ordinal == 0 ||
                    mode == "concurrent-failure" && ordinal == 5 ? "blocked" :
                    mode == "interrupted" && ordinal == 0 ? "interrupted" : "good";
                Interlocked.Increment(ref activeBodies);
                var response = new HttpResponseMessage(mode == "status" ? HttpStatusCode.Found :
                    ranged ? HttpStatusCode.PartialContent : HttpStatusCode.OK)
                {
                    Content = new StreamContent(new BodyStream(bytes, streamMode,
                        () => Interlocked.Decrement(ref activeBodies)))
                };
                response.Content.Headers.ContentLength = file ? head ? asset.Length : bytes.Length : bytes.Length;
                response.Content.Headers.ContentType = new(file ? metadata.MediaType :
                    mode == "json-type" ? "text/plain" : "application/json");
                if (file)
                {
                    response.Headers.ETag = EntityTagHeaderValue.Parse(mode == "head-etag" && head ? "\"wrong\"" : metadata.ETag);
                    if (mode == "head-length" && head)
                        response.Content.Headers.ContentLength = asset.Length - 1;
                    if (ranged)
                        response.Content.Headers.ContentRange = new(mode == "range-header" ? 1 : 0, 65535, asset.Length);
                }
                if (mode == "headers")
                    response.Headers.TryAddWithoutValidation("X-Bounded", new string('x', 1025));
                return response;
            }
            finally
            {
                Interlocked.Decrement(ref inFlight);
            }
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }

        private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
