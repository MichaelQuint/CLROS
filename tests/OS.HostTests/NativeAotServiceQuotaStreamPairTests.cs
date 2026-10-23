using System.Text;
using OS.Tooling;

internal static class NativeAotServiceQuotaStreamPairTests
{
    private const uint Port=43000;
    private static string Data(string value)=>Convert.ToBase64String(Encoding.ASCII.GetBytes(value));
    private static QemuTranscripts.ServiceQuotaHostConnection Host(string writes,string reads="",string? attempt=null)
    {
        var write=new List<QemuTranscripts.ServiceQuotaHostRange>();
        var read=new List<QemuTranscripts.ServiceQuotaHostRange>();
        if(writes.Length>0)write.Add(new(1,0,writes.Length,writes.Length,"complete"));
        if(reads.Length>0)read.Add(new(2,0,reads.Length,reads.Length,"complete"));
        QemuTranscripts.ServiceQuotaHostWriteAttempt? unknown=null;
        if(attempt is not null)
        {
            write.Add(new(3,writes.Length,attempt.Length,-1,"failed"));
            unknown=new(3,writes.Length,attempt.Length,"failed",Data(attempt));
        }
        return new(1,new(reads.Length,Data(reads),read,null),new(writes.Length,Data(writes),write,unknown));
    }
    private static QemuTranscripts.ServiceQuotaPacketDirection Direction(bool response,int extent,int contiguous,
        string prefix,params (int Offset,string Bytes)[] ranges) =>
        new(extent,contiguous,ranges.Sum(r=>r.Bytes.Length),Data(prefix),ranges.Select((r,i)=>
            new QemuTranscripts.ServiceQuotaPacketRange(new(Port,response,r.Offset,r.Bytes.Length,i+1,1,(uint)i),Data(r.Bytes))).ToArray());
    private static QemuTranscripts.ServiceQuotaPacketDirection Empty()=>Direction(false,0,0,"");
    private static QemuTranscripts.ServiceQuotaPacketStream Guest(QemuTranscripts.ServiceQuotaPacketDirection requests,
        QemuTranscripts.ServiceQuotaPacketDirection? responses=null)=>new(Port,requests,responses??Empty(),default);
    private static QemuTranscripts.ServiceQuotaStreamPairInspection Read(QemuTranscripts.ServiceQuotaHostConnection host,
        QemuTranscripts.ServiceQuotaPacketStream guest)=>QemuTranscripts.InspectNativeAotServiceQuotaStreamPairForTests(host,guest);

    [HostTest("quota stream pair compares independent request and response bytes without identifying empty or identical candidates")]
    private static void Bytes()
    {
        var host=Host("abcdef","XYZ");
        var guest=Guest(Direction(false,3,3,"abc",(0,"abc")),Direction(true,5,5,"XYZuv",(0,"XYZuv")));
        var result=Read(host,guest);
        HostCheck.True(result.Compatible && result.RequestBytesCompared==3 && result.ResponseBytesCompared==3 &&
            result.MinimumAttemptedWritePrefixBytes==0 && result.HasComparedBytes);
        HostCheck.True(Read(host with{ConnectionId=2},guest)==result); // Equal bytes cannot choose one candidate.
        HostCheck.True(!Read(Host("abXdef","XYZ"),guest).Compatible &&
            !Read(Host("abcdef","XYx"),guest).Compatible);
        var empty=Read(Host(""),Guest(Empty()));
        HostCheck.True(empty.Compatible && !empty.HasComparedBytes && empty.RequestBytesCompared==0 &&
            empty.ResponseBytesCompared==0 && empty.MinimumAttemptedWritePrefixBytes==0);
        HostCheck.True(Read(Host(""),Guest(Empty(),Direction(true,6,6,"unread",(0,"unread")))).Compatible);
    }

    [HostTest("quota stream pair treats failed write bytes as possible prefix only with bounded observed tail minimum")]
    private static void UnknownTail()
    {
        var host=Host("ab",attempt:"cdef");
        var prefix=Read(host,Guest(Direction(false,5,5,"abcde",(0,"abcde"))));
        HostCheck.True(prefix.Compatible && prefix.RequestBytesCompared==5 && prefix.MinimumAttemptedWritePrefixBytes==3);
        var islands=Read(host,Guest(Direction(false,5,2,"ab",(4,"e"),(0,"ab"))));
        HostCheck.True(islands.Compatible && islands.RequestBytesCompared==3 && islands.MinimumAttemptedWritePrefixBytes==3);
        HostCheck.True(Read(host,Guest(Direction(false,2,2,"ab",(0,"ab")))).MinimumAttemptedWritePrefixBytes==0);
        HostCheck.True(!Read(host,Guest(Direction(false,5,5,"abcdX",(0,"abcdX")))).Compatible &&
            !Read(host,Guest(Direction(false,7,7,"abcdefg",(0,"abcdefg")))).Compatible &&
            !Read(Host("ab"),Guest(Direction(false,3,3,"abc",(0,"abc")))).Compatible);
        var canceled=host with{Writes=host.Writes with{UnknownWrite=host.Writes.UnknownWrite! with{Outcome="canceled"},
            Operations=[host.Writes.Operations[0],host.Writes.Operations[1] with{Outcome="canceled"}]}};
        HostCheck.True(Read(canceled,Guest(Direction(false,3,3,"abc",(0,"abc")))).Compatible);
        Reject(()=>Read(host with{Writes=host.Writes with{UnknownWrite=null}},Guest(Empty())),"quota_pair_host_attempt");
        Reject(()=>Read(host with{Writes=host.Writes with{UnknownWrite=host.Writes.UnknownWrite! with{Offset=1}}},Guest(Empty())),
            "quota_pair_host_attempt");
    }

    [HostTest("quota stream pair preserves request holes unread responses and strict supplied inventory bounds")]
    private static void HolesAndBounds()
    {
        var requests=Direction(false,6,2,"ab",(4,"ef"),(0,"ab"));
        var responses=Direction(true,9,9,"XYZunread",(0,"XYZunread"));
        var host=Host("abcdefghi","XYZ");var guest=Guest(requests,responses);
        var result=Read(host,guest);
        HostCheck.True(result.Compatible && result.RequestBytesCompared==4 && result.ResponseBytesCompared==3);
        // A correct island after a hole cannot cover a host read beyond the contiguous prefix.
        var hole=Direction(true,4,2,"XY",(0,"XY"),(3,"u"));
        HostCheck.True(!Read(host,Guest(requests,hole)).Compatible);
        var ranges=requests.FirstRanges.ToArray();
        ranges[1]=ranges[1] with{First=ranges[1].First with{StreamOffset=4}};
        Reject(()=>Read(host,Guest(requests with{FirstRanges=ranges,KnownPrefixBase64="",ContiguousBytes=0})),
            "quota_pair_packet_overlap");
        Reject(()=>Read(host,Guest(requests with{KnownPrefixBase64=Data("aX")})),"quota_pair_packet_prefix");
        Reject(()=>Read(host,Guest(requests with{KnownBytes=3})),"quota_pair_packet_count");
        Reject(()=>Read(host,Guest(requests with{Extent=4*1024*1024},responses)),"quota_pair_packet_bound");
        Reject(()=>Read(host with{Writes=host.Writes with{KnownBytes=4*1024*1024}},guest),"quota_pair_host_bound");
        Reject(()=>Read(host with{Reads=host.Reads with{Operations=[new(1,0,3,3,"complete")]}},guest),"quota_pair_host_range");
        Reject(()=>Read(host with{Writes=host.Writes with{KnownBase64="YWJjZGVmZ2hp "}},guest),"quota_pair_data");
        var mutable=requests.FirstRanges.ToArray();
        var copied=Read(host,Guest(requests with{FirstRanges=mutable},responses));
        mutable[0]=mutable[0] with{DataBase64=Data("XX")};
        HostCheck.True(copied==result); // The scalar result retains no borrowed list.
    }

    private static void Reject(Action action,string reason)
    {
        try{action();}catch(ToolFailure error){HostCheck.Equal(reason,error.Reason);return;}
        throw new InvalidOperationException("expected rejection "+reason);
    }
}
