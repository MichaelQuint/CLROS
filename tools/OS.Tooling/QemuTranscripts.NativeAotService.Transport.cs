namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Derived bulk/compact lifetime and secret-owner accounting. This
    /// checks serialized observations only; packets, secrets, native ownership,
    /// process and guest execution remain separate evidence requirements.</summary>
    public readonly record struct ServiceTransportInspection(int Connections,int Transfers,int Retired,
        int BulkHighWater,int CompactHighWater,ulong MaximumDeadline,ulong IssuedSequences);

    /// <summary>Inspect bounded synthetic service transport records without
    /// mutating inputs. Reject stale identities, tuple reuse before expiry,
    /// shortened deadlines, missing transfers/retirements and false high-water
    /// or erasure summaries. This value-only report grants no guest authority.</summary>
    public static ServiceTransportInspection InspectNativeAotServiceTransportForTests(string[] rows) => ReadServiceTransport(rows,out _);

    private sealed class ServiceBulk(Dictionary<string,ulong> identity)
    {
        internal readonly Dictionary<string,ulong> Identity=identity;
        internal ulong Deadline, Duration;
        internal bool Transferred, Retired, LastClosed;
    }
    private sealed record ServiceCompact(ServiceBulk Bulk,ulong Generation,ulong Original,ulong Deadline,ulong Duration,ulong Clock);

    private static ServiceTransportInspection ReadServiceTransport(string[] rows,out HashSet<int> metadata)
    {
        Need(rows is not null && rows.Length is >0 and <=1_000_000 && rows.All(row=>row is not null && row.Length<=4096) &&
            rows.Sum(row=>(long)row.Length+2)<=128L*1024*1024,"service_transport_input");
        metadata=[]; Dictionary<ulong,ServiceBulk> bulk=[]; Dictionary<ulong,ulong> generations=[];
        Dictionary<ulong,ServiceCompact> compact=[]; Dictionary<(ulong Index,ulong Generation),ServiceCompact> pending=[];
        Dictionary<ulong,ulong> compactGenerations=[];
        HashSet<ulong> issuedGenerations=[];
        ulong clock=0,owner=0,epoch=0,quiet=0,maximum=0,issued=0;
        int fresh=0,transfers=0,retired=0,bulkHigh=0,compactHigh=0,observedBulkHigh=0,observedCompactHigh=0;
        bool bound=false,ended=false,erased=false;
        string Tuple(ServiceBulk value)=>string.Join(":",new[]{"local","localPort","remote","remotePort"}.Select(key=>value.Identity[key]));
        void Time(ulong now)
        {
            Need(now>=clock && now<=long.MaxValue,"service_transport_clock");
            clock=now;
        }
        ServiceBulk Current(Dictionary<string,ulong> row)
        {
            Need(bulk.TryGetValue(row["slot"],out var value) &&
                SocketLifetimeFields[..9].All(field=>row[field[..^4]]==value!.Identity[field[..^4]]),"service_transport_lifetime");
            return value!;
        }
        for(int at=0;at<rows!.Length;at++)
        {
            string line=rows[at];
            if(line.StartsWith("M07_033_BOUND ",StringComparison.Ordinal))
            {
                const string policy=" tickNanos=4000 quietNanos=240000000000 secret=independent lease=network";
                Need(!bound && !ended && line.EndsWith(policy,StringComparison.Ordinal),"service_sequence_bound");
                var row=ReadRecord(line[..^policy.Length],"M07_033_BOUND","quietUntil:dec","clock:dec");
                Time(row["clock"]);quiet=row["quietUntil"];
                // BOUND logs the snapshot taken before network Begin; Acquire
                // takes a later actual clock inside Begin. Never shorten the
                // full quiet interval by treating those two reads as identical.
                Need(clock<=ulong.MaxValue-240_000_000_000 && quiet>=clock+240_000_000_000 && quiet<=long.MaxValue,"service_sequence_quiet");bound=true;
            }
            else if(IsSocketTransport(line) || line.StartsWith("M07_030_TRANSFER ",StringComparison.Ordinal))
            {
                Need(bound && !ended,"service_transport_owner");
                bool transfer=line.StartsWith("M07_030_TRANSFER ",StringComparison.Ordinal);
                var row=transfer?ReadRecord(line,"M07_030_TRANSFER",[..SocketLifetimeFields[..10],"compactIndex:dec","compactGeneration:dec",
                    "originalDeadline:dec","deadline:dec","duration:dec"]):SocketTransport(line);
                Time(row["clock"]);
                Need(row["slot"]<8 && row["index"]<8 && row["owner"]>=4096 && row["owner"]%8==0 &&
                    row["epoch"]!=0 && row["generation"]!=0 && row["local"]==0x0a00020f && row["localPort"]==8080 &&
                    row["remote"]==0x0a000202 && row["remotePort"] is >0 and <=65535,"service_transport_identity");
                if(owner==0){owner=row["owner"];epoch=row["epoch"];}
                Need(owner==row["owner"] && epoch==row["epoch"],"service_transport_owner_identity");
                bool slot=line.StartsWith("M16_011_SLOT ",StringComparison.Ordinal);
                if(slot && row["event"]==0)
                {
                    var value=new ServiceBulk(row);string tuple=Tuple(value);
                    Need(clock>=quiet && !bulk.ContainsKey(row["slot"]) && bulk.Values.All(other=>other.Identity["index"]!=row["index"] && Tuple(other)!=tuple) &&
                        compact.Values.All(other=>Tuple(other.Bulk)!=tuple || clock>=other.Deadline) &&
                        pending.Values.All(other=>Tuple(other.Bulk)!=tuple || clock>=other.Deadline) &&
                        generations.GetValueOrDefault(row["index"])<ulong.MaxValue &&
                        row["generation"]==generations.GetValueOrDefault(row["index"])+1 && row["deadline"]==0 && row["duration"]==0,
                        "service_transport_fresh");
                    bulk.Add(row["slot"],value);fresh++;observedBulkHigh=Math.Max(observedBulkHigh,bulk.Count);
                }
                else
                {
                    var value=Current(row);
                    if(transfer)
                    {
                        ulong index=row["compactIndex"],generation=row["compactGeneration"],original=row["originalDeadline"];
                        Need(!value.Transferred && index<64 && generation!=0 && !issuedGenerations.Contains(generation) && generation>compactGenerations.GetValueOrDefault(index) &&
                            (!compact.TryGetValue(index,out var old) || clock>=old.Deadline && generation>old.Generation) &&
                            pending.Keys.All(key=>key.Index!=index) && row["duration"]==240_000_000_000 &&
                            original>=row["duration"] && original-row["duration"]>=value.Identity["clock"] &&
                            original-row["duration"]<=clock && row["deadline"]>=Math.Max(original,value.Deadline) && row["deadline"]>clock,
                            "service_compact_transfer");
                        value.Transferred=true;value.Deadline=row["deadline"];value.Duration=row["duration"];
                        issuedGenerations.Add(generation);
                        pending.Add((index,generation),new(value,generation,original,row["deadline"],row["duration"],clock));transfers++;
                    }
                    else if(slot)
                    {
                        Need(row["event"]==1 && row["deadline"]==value.Deadline && row["duration"]==value.Duration &&
                            (value.Transferred || clock>=value.Deadline),"service_transport_retire");
                        value.Retired=true;bulk.Remove(row["slot"]);generations[row["index"]]=row["generation"];
                    }
                    else
                    {
                        Need(!value.Transferred && row["drain"]<=1 && row["duration"]==240_000_000_000 &&
                            row["deadline"]>=row["duration"] && row["deadline"]-row["duration"]<=clock &&
                            row["deadline"]-row["duration"]>=value.Identity["clock"] && row["deadline"]>=value.Deadline,"service_transport_timewait");
                        value.Deadline=row["deadline"];value.Duration=row["duration"];
                    }
                    maximum=Math.Max(maximum,value.Deadline);
                }
            }
            else if(line.StartsWith("M07_034_CLOSE ",StringComparison.Ordinal))
            {
                Need(bound && !ended,"service_unread_close_owner");
                var row=ReadRecord(line,"M07_034_CLOSE",[..SocketLifetimeFields[..10],"localNext:dec","remoteNext:dec","unread:dec"]);
                Time(row["clock"]);var value=Current(row);
                Need(!value.LastClosed && !value.Transferred && !value.Retired &&
                    row["localNext"]<=uint.MaxValue && row["remoteNext"]<=uint.MaxValue && row["unread"]<=8192,
                    "service_unread_close_state");
                value.LastClosed=true;
            }
            else if(line.StartsWith("M07_030_RECORD ",StringComparison.Ordinal))
            {
                Need(bound && !ended,"service_compact_owner");
                bool retirement=line.StartsWith("M07_030_RECORD event=1 ",StringComparison.Ordinal);
                var row=ReadRecord(line,"M07_030_RECORD",retirement?["event:dec","index:dec","generation:dec","deadline:dec","clock:dec"]:
                    ["event:dec","index:dec","generation:dec","originalDeadline:dec","deadline:dec","duration:dec","clock:dec"]);
                Time(row["clock"]);ulong index=row["index"],generation=row["generation"];
                Need(index<64 && generation!=0,"service_compact_identity");
                if(retirement)
                {
                    Need(compact.TryGetValue(index,out var value) && generation==value.Generation && row["deadline"]==value.Deadline && clock>=value.Deadline,
                        "service_compact_retire");
                    compact.Remove(index);compactGenerations[index]=generation;retired++;
                }
                else if(row["event"]==0)
                {
                    bool found=pending.Remove((index,generation),out var value);
                    Need(found && !compact.ContainsKey(index) && value!.Bulk.Retired &&
                        clock==value.Clock && row["originalDeadline"]==value.Original && row["deadline"]==value.Deadline && row["duration"]==value.Duration,
                        "service_compact_create");
                    compact.Add(index,value!);observedCompactHigh=Math.Max(observedCompactHigh,compact.Count);
                }
                else
                {
                    bool found=compact.TryGetValue(index,out var value);
                    Need(found && row["event"]==2 && generation==value!.Generation && row["originalDeadline"]==value.Original &&
                        row["duration"]==value.Duration && row["deadline"]>value.Deadline && row["deadline"]>=value.Duration &&
                        row["deadline"]-value.Duration<=clock && row["deadline"]-value.Duration<value.Deadline,"service_compact_extension");
                    compact[index]=value! with {Deadline=row["deadline"]};
                }
                maximum=Math.Max(maximum,row["deadline"]);
            }
            else if(line.StartsWith("M07_030_HIGHWATER ",StringComparison.Ordinal))
            {
                Need(bound && !ended && pending.Count==0,"service_transport_snapshot");
                var row=ReadRecord(line,"M07_030_HIGHWATER","bulk:dec","compact:dec","clock:dec");Time(row["clock"]);
                // Preserve snapshot admission while independently accounting for
                // peaks that disappeared before any reported high-water event.
                Need(row["bulk"]==(ulong)observedBulkHigh && row["compact"]==(ulong)observedCompactHigh &&
                    row["bulk"]==(ulong)Math.Max(bulkHigh,bulk.Count) && row["compact"]==(ulong)Math.Max(compactHigh,compact.Count) &&
                    (row["bulk"]>(ulong)bulkHigh || row["compact"]>(ulong)compactHigh) && row["bulk"]<=8 && row["compact"]<=64,
                    "service_transport_highwater");
                bulkHigh=(int)row["bulk"];compactHigh=(int)row["compact"];
            }
            else if(line.StartsWith("M07_030_END ",StringComparison.Ordinal))
            {
                const string suffix=" owner=retired";
                Need(bound && !ended && line.EndsWith(suffix,StringComparison.Ordinal),"service_transport_end");
                var row=ReadRecord(line[..^suffix.Length],"M07_030_END","transfers:dec","retired:dec","bulkHighWater:dec","compactHighWater:dec","bytesPerRecord:dec","clock:dec","deadline:dec");
                Time(row["clock"]);
                Need(fresh>8 && transfers>8 && transfers==retired && pending.Count==0 && bulk.Count==0 && compact.Count==0 &&
                    bulkHigh==observedBulkHigh && compactHigh==observedCompactHigh &&
                    row["transfers"]==(ulong)transfers && row["retired"]==(ulong)retired && row["bulkHighWater"]==(ulong)bulkHigh && bulkHigh is >0 and <=8 &&
                    row["compactHighWater"]==(ulong)compactHigh && compactHigh is >8 and <=64 &&
                    row["bytesPerRecord"]==(ulong)System.Runtime.CompilerServices.Unsafe.SizeOf<OS.Net.TcpTimeWaitTable.Entry>() && row["bytesPerRecord"] is >0 and <=64 &&
                    row["deadline"]==maximum && clock>=maximum,"service_transport_complete");ended=true;
            }
            else if(line.StartsWith("M07_033_END ",StringComparison.Ordinal))
            {
                const string suffix=" secret=zero seed=zero lease=released firmwareCallbacks=none";
                Need(ended && !erased && line.EndsWith(suffix,StringComparison.Ordinal),"service_sequence_erasure");
                var row=ReadRecord(line[..^suffix.Length],"M07_033_END","issued:dec");issued=row["issued"];
                Need(issued>=(ulong)fresh,"service_sequence_issued");erased=true;
            }
            else
            {
                Need(!line.StartsWith("M07_030_",StringComparison.Ordinal) && !line.StartsWith("M07_034_",StringComparison.Ordinal) &&
                    (!line.StartsWith("M07_033_",StringComparison.Ordinal) || line=="M07_033_SEED source=firmware-rng acquisitions=1 secretBytes=32 domain=tcp-isn state=ready firmwareCallbacks=none"),
                    "service_transport_unknown_record");continue;
            }
            metadata.Add(at);
        }
        Need(bound && ended && erased,"service_transport_incomplete");
        return new(fresh,transfers,retired,bulkHigh,compactHigh,maximum,issued);
    }
}
