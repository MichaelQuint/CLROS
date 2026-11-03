using System.Text;
using System.Text.Json;

namespace OS.Tooling;

public static partial class NativeAotWebPayload
{
    // IDs and completion ordinals describe this owned API invocation only.
    // HttpClient exposes no actual connection, wire exchange or sent-byte ID.
    private sealed class RaceAttemptLog(string endpoint, NativeAotServiceQuotaPeer.WireCapture? wire, bool startGate = false, bool eventOwned = false)
    {
        private int completed;
        private int churnRequests;
        private readonly List<RaceHttpAttempt> attempts = [];
        private readonly List<RaceSampleLink> selections = [];
        internal RaceHttpAttempt LastStarted => attempts[^1];

        internal async Task<RaceHttpReply> SendAsync(HttpClient client, HttpRequestMessage request, string path, CancellationToken token)
        {
            RaceNeed(attempts.Count < MaximumRaceAttempts, "attempt_limit");
            var attempt = new RaceHttpAttempt(attempts.Count + 1, request.Method.Method, path);
            attempts.Add(attempt);
            using MemoryStream bytes = new();
            bool wireStarted = false;
            try
            {
                if (eventOwned)
                {
                    attempt.SignalVersion = request.Headers.TryGetValues("X-CLROS-Signal", out var versions) ? string.Join(',', versions) : "";
                    RaceNeed(attempt.SignalVersion == EventSignalVersion(request.Method.Method, path), "attempt_signal_version");
                    attempt.ChurnVersion = request.Headers.TryGetValues("X-CLROS-Churn", out var churnVersions) ? string.Join(',', churnVersions) : "";
                    bool initial = request.Method == HttpMethod.Post && path == "/__clros/churn" && ++churnRequests == 1;
                    RaceNeed(attempt.ChurnVersion == EventChurnVersion(request.Method.Method, path, initial), "attempt_churn_version");
                }

                if (wire is not null)
                {
                    int exchange = wire.BeginExchange(NativeAotServiceQuotaPeer.SignalRaceWireRole(request.Method.Method, path), request.Method.Method, path);
                    wireStarted = true;
                    RaceNeed(exchange == attempt.id, "wire_attempt_identity");
                }

                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
                attempt.status = (int)response.StatusCode;
                attempt.headers = true;
                string media = response.Content.Headers.ContentType?.MediaType ?? "";
                attempt.mediaType = media[..Math.Min(media.Length, 128)];
                attempt.mediaTypeComplete = media.Length <= 128;
                RaceNeed(attempt.mediaTypeComplete && media == "text/plain", "content_type");
                using var stream = await response.Content.ReadAsStreamAsync(token);
                byte[] chunk = new byte[1024];
                for (; ; )
                {
                    int count = await stream.ReadAsync(chunk, token);
                    if (count == 0)
                    {
                        attempt.bodyComplete = true;
                        break;
                    }

                    int retained = (int)Math.Min(count, 4096 - bytes.Length);
                    if (retained > 0)
                        bytes.Write(chunk, 0, retained);
                    RaceNeed(retained == count, "response_size");
                }

                string body = new UTF8Encoding(false, true).GetString(bytes.ToArray());
                attempt.outcome = "response";
                return new(attempt, body);
            }
            catch (OperationCanceledException)
            {
                attempt.outcome = "cancelled";
                attempt.error = "cancelled";
                throw;
            }
            catch (Exception error)
            {
                attempt.outcome = "faulted";
                attempt.error = RaceCaptureFailure(error);
                throw;
            }
            finally
            {
                byte[] body = bytes.ToArray();
                attempt.bodyBase64 = Convert.ToBase64String(body);
                attempt.completion = Interlocked.Increment(ref completed);
                if (wireStarted)
                    wire!.EndSignalRaceExchange(attempt.id, attempt.outcome, attempt.status, body);
            }
        }

        internal void Select(int sample, RaceHttpReply reply) => selections.Add(new(sample, reply.Attempt.id, 0));
        internal void SelectCancellation(int sample, int request) => selections.Add(new(sample, 0, request));
        internal string Json(bool semanticComplete, string failure)
        {
            object rows = eventOwned ? attempts.Select(a => new { a.id, a.method, a.path, a.completion, a.headers, a.status, a.mediaType, a.mediaTypeComplete, a.bodyBase64, a.bodyComplete, a.outcome, a.error, signalVersion = a.SignalVersion, churnVersion = a.ChurnVersion }).ToArray() : attempts;
            return JsonSerializer.Serialize(new
            {
                schema = eventOwned ? "clros.web.signalrace.attempts.v3" : startGate ? "clros.web.signalrace.attempts.v2" : "clros.web.signalrace.attempts.v1",
                endpoint,
                semanticComplete,
                failure,
                attempts = rows,
                selections
            });
        }
    }
}
