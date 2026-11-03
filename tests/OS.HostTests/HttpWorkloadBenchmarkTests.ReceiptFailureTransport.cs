using OS.Tooling;

internal static partial class HttpWorkloadBenchmarkTests
{
    private sealed class ReceiptFailureTransport(string directory, HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
        {
            Directory.CreateDirectory(Path.Combine(directory, "benchmark.json"));
            return base.SendAsync(request, cancellation);
        }
    }
}
