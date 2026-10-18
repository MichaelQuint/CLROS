using OS.Tooling;

internal static class NativeAotWebVectorEvidenceTests
{
    private const string Receipt="{\"status\":200,\"contentType\":\"application/octet-stream\",\"contentLength\":49152,\"bodyBytes\":49152,\"sha256\":\"664d1e34fe80e8713fefa2b9c30df7d7877bbffd850411ffda14f54bd1ce847c\"}";

    [HostTest("native web vector evidence binds all binary bytes and observed HTTP status type length hash")]
    private static void HttpEvidence()
    {
        byte[] body=Enumerable.Range(0,49152).Select(index=>(byte)(index%251)).ToArray();
        NativeAotWebPayload.ValidateVectorEvidence(Receipt,body);
        foreach((string from,string to) in new[]{("200","201"),("application/octet-stream","text/plain"),("49152","49151"),
            ("664d1e34","00000000"),("\"status\":200,","\"extra\":0,\"status\":200,")})
            Reject(()=>NativeAotWebPayload.ValidateVectorEvidence(Receipt.Replace(from,to,StringComparison.Ordinal),body));
        var wrong=(byte[])body.Clone();wrong[32769]^=1;Reject(()=>NativeAotWebPayload.ValidateVectorEvidence(Receipt,wrong));
        Reject(()=>NativeAotWebPayload.ValidateVectorEvidence(Receipt,body.AsSpan(0,49151)));
        Reject(()=>NativeAotWebPayload.ValidateVectorEvidence(Receipt,[..body,0]));
    }

    private static void Reject(Action action)
    {bool rejected=false;try{action();}catch(InvalidDataException){rejected=true;}HostCheck.True(rejected);}
}
