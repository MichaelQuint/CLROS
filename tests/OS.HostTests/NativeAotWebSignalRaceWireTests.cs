using System.Text;
using System.Text.Json.Nodes;
using OS.Tooling;
using Peer=OS.Tooling.NativeAotServiceQuotaPeer;

// Independent copied values check profile separation and consistency only.
// They grant no actual stream, HTTP framing, packet or guest authority.
internal static class NativeAotWebSignalRaceWireTests
{
    private const int Port=23459;
    private const string Endpoint="http://127.0.0.1:23459",Health="OS_MINIMAL_API_OK";
    private static Peer.WireTrace Trace()
    {
        byte[] request="GET /__clros/churn/status HTTP/1.1\r\nHost: 127.0.0.1:23459\r\n\r\n"u8.ToArray();
        byte[] response="HTTP/1.1 503 Service Unavailable\r\nContent-Length: 3\r\nContent-Type: text/plain\r\n\r\nbad"u8.ToArray();
        return new(Port,"signalrace-partial",11,request.Length+response.Length,
            [new(1,"control","127.0.0.1",45678,"127.0.0.1",Port,1,10)],
            [new(1,"control","GET","/__clros/churn/status",0,8,"response",503,"YmFk",[1])],
            [new(1,1,"write",0,request.Length,request.Length,2,3,"complete",Convert.ToBase64String(request),1),
             new(2,1,"read",0,4096,response.Length,4,7,"complete",Convert.ToBase64String(response),0)],[]);
    }
    private static void Reject(Action action)
    {bool rejected=false;try{action();}catch(Exception error)when(error is ToolFailure or InvalidDataException){rejected=true;}HostCheck.True(rejected);}

    [HostTest("web race wire schema preserves raw values and rejects quota purpose and role confusion")]
    private static void Profiles()
    {
        var original=Trace();string json=NativeAotWebPayload.SerializeSignalRaceWire(original);
        var copy=NativeAotWebPayload.InspectSignalRaceWire(Encoding.UTF8.GetBytes(json));
        HostCheck.Equal("signalrace-partial",copy.Purpose);HostCheck.Equal(1,copy.Exchanges[0].ConnectionIds[0]);
        HostCheck.Equal("YmFk",copy.Exchanges[0].BodyBase64);HostCheck.Equal(0,copy.Operations[1].ExchangeId);
        HostCheck.True(copy.Operations[0].DataBase64==original.Operations[0].DataBase64&&copy.Operations[1].DataBase64==original.Operations[1].DataBase64);
        Reject(()=>Peer.SerializeWireReport(original));Reject(()=>Peer.InspectWireReport(Encoding.UTF8.GetBytes(json)));
        var quota=new Peer.WireTrace(Port,"stream-control",11,0,original.Connections,[],[],[]);
        string quotaJson=Peer.SerializeWireReport(quota);
        Reject(()=>NativeAotWebPayload.InspectSignalRaceWire(Encoding.UTF8.GetBytes(quotaJson)));
        Reject(()=>NativeAotWebPayload.SerializeSignalRaceWire(quota));
        foreach(int status in new[]{700,999})
        {
            var rejectedStatus=original with{Exchanges=[original.Exchanges[0] with{Status=status}]};
            var retained=NativeAotWebPayload.InspectSignalRaceWire(Encoding.UTF8.GetBytes(NativeAotWebPayload.SerializeSignalRaceWire(rejectedStatus)));
            HostCheck.Equal(status,retained.Exchanges[0].Status);
        }
    }

    [HostTest("web race wire copied profile rejects aliased endpoints impossible offsets outcomes and request joins")]
    private static void Refusals()
    {
        string json=NativeAotWebPayload.SerializeSignalRaceWire(Trace());
        foreach(Action<JsonObject> corrupt in new Action<JsonObject>[]
        {
            o=>o["schema"]="clros.web.signalrace.wire.v2",o=>o["purpose"]="quota-partial",o=>o["purpose"]="signalrace-run",o=>o["extra"]=true,
            o=>o["rawBytes"]=0,o=>o["snapshotTicks"]=0,o=>o["connections"]![0]!["role"]="pressure",
            o=>o["connections"]![0]!["remotePort"]=Port+1,o=>o["connections"]![0]!["localAddress"]="0.0.0.0",
            o=>o["connections"]![0]!["disposedTicks"]=0,o=>o["operations"]![0]!["offset"]=1,
            o=>o["operations"]![0]!["exchangeId"]=0,o=>o["operations"]![1]!["exchangeId"]=1,
            o=>o["operations"]![1]!["transferred"]=-1,o=>o["operations"]![1]!["dataBase64"]="!!!!",
            o=>o["exchanges"]![0]!["connectionIds"]=new JsonArray(),o=>o["exchanges"]![0]!["outcome"]="pending",
            o=>o["exchanges"]![0]!["role"]="churn",o=>o["exchanges"]![0]!["path"]="/__clros/service/status",
            o=>o["exchanges"]![0]!["status"]=0,o=>o["exchanges"]![0]!["endedTicks"]=2,
            o=>o["exchanges"]![0]!["status"]=1000
        })
        {var value=JsonNode.Parse(json)!.AsObject();corrupt(value);Reject(()=>NativeAotWebPayload.InspectSignalRaceWire(Encoding.UTF8.GetBytes(value.ToJsonString())));}
    }

    [HostTest("web race copied logical attempt join binds exact observed exchange body outcome route and identity")]
    private static void AttemptJoin()
    {
        string selected="{\"schema\":\"clros.web.signalrace.v1\",\"endpoint\":\""+Endpoint+"\",\"samples\":[]}";
        var attempt=new JsonObject{["id"]=1,["method"]="GET",["path"]="/__clros/churn/status",["completion"]=1,["headers"]=true,["status"]=503,
            ["mediaType"]="text/plain",["mediaTypeComplete"]=true,["bodyBase64"]="YmFk",["bodyComplete"]=true,["outcome"]="response",["error"]=""};
        string attempts=new JsonObject{["schema"]="clros.web.signalrace.attempts.v1",["endpoint"]=Endpoint,["semanticComplete"]=false,["failure"]="web_signalrace_churn_status",
            ["attempts"]=new JsonArray(attempt),["selections"]=new JsonArray()}.ToJsonString();
        var trace=Trace();byte[] wire=Encoding.UTF8.GetBytes(NativeAotWebPayload.SerializeSignalRaceWire(trace));
        NativeAotWebPayload.ValidateSignalRaceTransport(attempts,selected,wire,Endpoint,Health);
        foreach(Func<Peer.WireExchange,Peer.WireExchange> change in new Func<Peer.WireExchange,Peer.WireExchange>[]
        {e=>e with{Status=502},e=>e with{BodyBase64="bm9w"},e=>e with{Outcome="faulted"},e=>e with{Path="/healthz"}})
        {
            var altered=trace with{Exchanges=[change(trace.Exchanges[0])]};
            byte[] bytes=Encoding.UTF8.GetBytes(NativeAotWebPayload.SerializeSignalRaceWire(altered));
            Reject(()=>NativeAotWebPayload.ValidateSignalRaceTransport(attempts,selected,bytes,Endpoint,Health));
        }
    }
}
