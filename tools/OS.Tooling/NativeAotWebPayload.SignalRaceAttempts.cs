using System.Text;
using System.Text.Json;

namespace OS.Tooling;

public static partial class NativeAotWebPayload
{
    private const int MaximumRaceAttempts=1024;
    private sealed class RaceHttpAttempt(int number,string verb,string target)
    {
        private int headerStatus,hasHeaders;
        public int id { get; }=number;
        public string method { get; }=verb;
        public string path { get; }=target;
        public int completion { get; set; }
        public bool headers { get=>Volatile.Read(ref hasHeaders)!=0; set=>Volatile.Write(ref hasHeaders,value?1:0); }
        public int status { get=>Volatile.Read(ref headerStatus); set=>Volatile.Write(ref headerStatus,value); }
        public string mediaType { get; set; }="";
        public bool mediaTypeComplete { get; set; }=true;
        public string bodyBase64 { get; set; }="";
        public bool bodyComplete { get; set; }
        public string outcome { get; set; }="pending";
        public string error { get; set; }="";
    }
    private sealed record RaceHttpReply(RaceHttpAttempt Attempt,string Body)
    { internal int Status=>Attempt.status; }
    private sealed record RaceSampleLink(int sample,int attempt,int related);

    // IDs and completion ordinals describe this owned API invocation only.
    // HttpClient exposes no actual connection, wire exchange or sent-byte ID.
    private sealed class RaceAttemptLog(string endpoint,NativeAotServiceQuotaPeer.WireCapture? wire)
    {
        private int completed;
        private readonly List<RaceHttpAttempt> attempts=[];
        private readonly List<RaceSampleLink> selections=[];
        internal RaceHttpAttempt LastStarted=>attempts[^1];
        internal async Task<RaceHttpReply> SendAsync(HttpClient client,HttpRequestMessage request,string path,CancellationToken token)
        {
            RaceNeed(attempts.Count<MaximumRaceAttempts,"attempt_limit");
            var attempt=new RaceHttpAttempt(attempts.Count+1,request.Method.Method,path);attempts.Add(attempt);
            using MemoryStream bytes=new();bool wireStarted=false;
            try
            {
                if(wire is not null)
                {
                    int exchange=wire.BeginExchange(NativeAotServiceQuotaPeer.SignalRaceWireRole(request.Method.Method,path),request.Method.Method,path);
                    wireStarted=true;RaceNeed(exchange==attempt.id,"wire_attempt_identity");
                }
                using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token);
                attempt.status=(int)response.StatusCode;attempt.headers=true;
                string media=response.Content.Headers.ContentType?.MediaType??"";
                attempt.mediaType=media[..Math.Min(media.Length,128)];attempt.mediaTypeComplete=media.Length<=128;
                RaceNeed(attempt.mediaTypeComplete&&media=="text/plain","content_type");
                using var stream=await response.Content.ReadAsStreamAsync(token);byte[] chunk=new byte[1024];
                for(;;)
                {
                    int count=await stream.ReadAsync(chunk,token);
                    if(count==0){attempt.bodyComplete=true;break;}
                    int retained=(int)Math.Min(count,4096-bytes.Length);if(retained>0)bytes.Write(chunk,0,retained);
                    RaceNeed(retained==count,"response_size");
                }
                string body=new UTF8Encoding(false,true).GetString(bytes.ToArray());
                attempt.outcome="response";return new(attempt,body);
            }
            catch(OperationCanceledException){attempt.outcome="cancelled";attempt.error="cancelled";throw;}
            catch(Exception error){attempt.outcome="faulted";attempt.error=RaceCaptureFailure(error);throw;}
            finally
            {
                byte[] body=bytes.ToArray();attempt.bodyBase64=Convert.ToBase64String(body);attempt.completion=Interlocked.Increment(ref completed);
                if(wireStarted)wire!.EndSignalRaceExchange(attempt.id,attempt.outcome,attempt.status,body);
            }
        }
        internal void Select(int sample,RaceHttpReply reply)=>selections.Add(new(sample,reply.Attempt.id,0));
        internal void SelectCancellation(int sample,int request)=>selections.Add(new(sample,0,request));
        internal string Json(bool semanticComplete,string failure)=>JsonSerializer.Serialize(new
        {schema="clros.web.signalrace.attempts.v1",endpoint,semanticComplete,failure,attempts,selections});
    }
    private static string RaceCaptureFailure(Exception error)=>error is InvalidDataException&&error.Message.StartsWith("web_signalrace_",StringComparison.Ordinal)
        ?error.Message:error.GetType().Name;
}
