using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Copied selected sample binding. A host cancellation names its
    /// related logical exchange and MessageIndex=-1; it is never an HTTP response.
    /// Other samples name one complete FIFO request/response message.</summary>
    public readonly record struct WebSignalRaceHostHttpSample(int Sample,int ExchangeId,int MessageIndex,bool HostCancellation);

    /// <summary>Every known request, including polls/retries/incomplete suffixes,
    /// paired to its FIFO response on the same observed stream. Unknown attempted
    /// writes remain separate. Values grant no original I/O or guest authority.</summary>
    public sealed record WebSignalRaceHostHttpInspection(ServiceQuotaHostPrefixes Prefixes,
        IReadOnlyList<ServiceQuotaHostHttpMessage> Messages,IReadOnlyList<WebSignalRaceHostHttpSample> Samples);

    /// <summary>Borrow strict web-race wire/attempt/selected reports and frame
    /// only known successful stream bytes. Bind request-write IDs and pair each
    /// connection FIFO, including retries and partial suffixes. Response outcomes
    /// require a unique complete request/response with exact status/decoded bytes;
    /// faults/cancellations establish only consumed entity prefixes. Selected
    /// responses bind exact logical IDs; host cancellation remains a separate event.
    /// Uses existing32MiB JSON/4MiB raw/8KiB header bounds. Web-only diagnostics
    /// retain status700..999; quota grammar remains unchanged. Malformed framing,
    /// ambiguous/reordered responses or unknown-byte completion claims throw.
    /// Returns copied/read-only offsets and original op IDs. No process, file,
    /// native, PCAP/NAT tuple, cross-clock or guest admission is established.</summary>
    public static WebSignalRaceHostHttpInspection InspectNativeAotWebSignalRaceHostHttp(ReadOnlySpan<byte> wireJson,
        string attemptsJson,string selectedJson,string endpoint,string expectedHealthBody)
    {
        var trace=NativeAotWebPayload.InspectSignalRaceTransport(attemptsJson,selectedJson,wireJson,endpoint,expectedHealthBody);
        var prefixes=ReadServiceQuotaHostPrefixes(trace);List<ServiceQuotaHostHttpMessage> messages=[];HashSet<(int,int)> seen=[];
        foreach(var connection in prefixes.Connections)
        {
            var requests=ReadServiceQuotaHttpPrefixes(Convert.FromBase64String(connection.Writes.KnownBase64),false,webDiagnostics:true).Messages;
            var responses=ReadServiceQuotaHttpPrefixes(Convert.FromBase64String(connection.Reads.KnownBase64),true,webDiagnostics:true).Messages;
            Need(responses.Count<=requests.Count,"web_race_http_extra_response");
            ServiceQuotaHostHttpMessage? previous=null;
            for(int i=0;i<requests.Count;i++)
            {
                var request=requests[i];var response=i<responses.Count?responses[i]:null;
                int[] writes=Intersect(connection.Writes,request.Start,request.End),reads=response is null?[]:Intersect(connection.Reads,response.Start,response.End);
                Need(writes.Length>0,"web_race_http_missing_write");int exchangeId=trace.Operations[writes[0]-1].ExchangeId;
                Need(exchangeId>0&&writes.All(id=>trace.Operations[id-1].ExchangeId==exchangeId)&&seen.Add((connection.ConnectionId,exchangeId)),"web_race_http_write_identity");
                var exchange=trace.Exchanges[exchangeId-1];
                if(previous is not null)Need(previous.Response is {State:"complete"}&&previous.ResponseOperations.Count>0&&
                    previous.ResponseOperations.Max(id=>trace.Operations[id-1].EndedTicks)<=writes.Min(id=>trace.Operations[id-1].StartedTicks),"web_race_http_fifo");
                if(request.HeaderEnd>=0)
                {
                    Need(request.First==exchange.Method+" "+exchange.Path+" HTTP/1.1"&&request.Headers.GetValueOrDefault("Host")=="127.0.0.1:"+trace.Port.ToString(CultureInfo.InvariantCulture)&&
                        request.Headers.GetValueOrDefault("Connection") is null,"web_race_http_request");
                    Need(exchange.Method=="POST"?request.Framing=="length"&&request.DeclaredLength==0:request.Framing=="none","web_race_http_request_body");
                    if(exchange.Method=="POST")Need(request.Headers.GetValueOrDefault(exchange.Path=="/__clros/churn"?"X-CLROS-Churn":"X-CLROS-Signal")=="v1","web_race_http_request_header");
                }
                var message=new ServiceQuotaHostHttpMessage(connection.ConnectionId,exchangeId,request,response,Array.AsReadOnly(writes),Array.AsReadOnly(reads));
                messages.Add(message);previous=message;
            }
        }
        Dictionary<int,int> completed=[];
        foreach(var exchange in trace.Exchanges.Where(e=>e.Outcome=="response"))
        {
            int[] matches=Enumerable.Range(0,messages.Count).Where(i=>messages[i].ExchangeId==exchange.Id&&messages[i].Request.State=="complete"&&messages[i].Response?.State=="complete").ToArray();
            Need(matches.Length==1,"web_race_http_complete_exchange");var message=messages[matches[0]];var response=message.Response!;
            Need(int.Parse(response.First.AsSpan(9,3),NumberStyles.None,CultureInfo.InvariantCulture)==exchange.Status&&response.BodyBase64==exchange.BodyBase64&&
                MediaTypeHeaderValue.TryParse(response.Headers.GetValueOrDefault("Content-Type"),out var media)&&media.MediaType=="text/plain"&&
                message.ResponseOperations.Count>0&&message.ResponseOperations.All(id=>trace.Operations[id-1].EndedTicks>=message.RequestOperations.Min(write=>trace.Operations[write-1].StartedTicks)&&
                    trace.Operations[id-1].EndedTicks<=exchange.EndedTicks),"web_race_http_response");
            completed.Add(exchange.Id,matches[0]);
        }
        foreach(var exchange in trace.Exchanges.Where(e=>e.Outcome!="response"&&(e.Status>0||e.BodyBase64!="")))
        {
            Need(exchange.ConnectionIds.Count>0,"web_race_http_partial_connection");int last=exchange.ConnectionIds[^1];
            var matches=messages.Where(m=>m.ExchangeId==exchange.Id&&m.ConnectionId==last).ToArray();
            Need(matches.Length==1&&matches[0].Response is {HeaderEnd:>=0},"web_race_http_missing_headers");var response=matches[0].Response!;
            Need((exchange.Status==0||int.Parse(response.First.AsSpan(9,3),NumberStyles.None,CultureInfo.InvariantCulture)==exchange.Status)&&
                Convert.FromBase64String(response.BodyBase64).AsSpan().StartsWith(Convert.FromBase64String(exchange.BodyBase64)),"web_race_http_consumed_prefix");
        }
        // A cancelled/faulted attempt may never have connected or written bytes.
        // Complete response outcomes above can never use attempted unknown writes.
        using var document=JsonDocument.Parse(attemptsJson);List<WebSignalRaceHostHttpSample> samples=[];
        foreach(var link in document.RootElement.GetProperty("selections").EnumerateArray())
        {
            int sample=link.GetProperty("sample").GetInt32(),exchange=link.GetProperty("attempt").GetInt32();
            if(exchange==0)samples.Add(new(sample,link.GetProperty("related").GetInt32(),-1,true));
            else{Need(completed.TryGetValue(exchange,out int index),"web_race_http_sample");samples.Add(new(sample,exchange,index,false));}
        }
        return new(prefixes,Array.AsReadOnly(messages.ToArray()),Array.AsReadOnly(samples.ToArray()));
    }
}
