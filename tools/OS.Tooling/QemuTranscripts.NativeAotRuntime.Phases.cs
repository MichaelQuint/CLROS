using System.Globalization;
using System.Text;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    // Complete stdout is already checked by the pinned app validator. This
    // additionally consumes independently observed kernel activity at the writes
    // that complete its rows. Segmentation/coalescing cannot invent evidence.
    private sealed class RuntimePhases
    {
        private readonly string app;
        private readonly (int End,string Text)[] rows;
        private readonly int bytes;
        private readonly List<CollectionSignal> collections=[];
        private readonly List<(ulong Deadline,ulong Clock)> deadlines=[];
        private int position,normalReaps,gc,joins,timers,written;
        private bool started,passed;
        internal ulong EarliestClock { get; private set; }=ulong.MaxValue;
        internal ulong LatestClock { get; private set; }

        internal RuntimePhases(string app,string stdout,IReadOnlyList<int> lengths)
        {
            NativeAotRuntimePayload.ValidateOutput(app,stdout);this.app=app;
            bytes=Encoding.UTF8.GetByteCount(stdout);
            Need(lengths.Sum()==bytes && bytes is >0 and <=4096,"runtime_stdout_lengths");
            int end=0;List<(int,string)> list=[];
            foreach(string raw in stdout.Split('\n')[..^1])
            {
                end+=Encoding.UTF8.GetByteCount(raw)+1;
                list.Add((end,raw.EndsWith('\r')?raw[..^1]:raw));
            }
            rows=list.ToArray();
        }
        internal void Collected(CollectionSignal proof)=>collections.Add(proof);
        internal void Reaped()=>normalReaps++;
        internal void Deadline(ulong deadline,ulong clock)=>deadlines.Add((deadline,clock));

        internal void Write(int total)
        {
            Need(total>written && total<=bytes,"runtime_stdout_progress");written=total;
            while(position<rows.Length && rows[position].End<=total)
            {
                string line=rows[position++].Text;
                if(line.StartsWith("TEST_START ",StringComparison.Ordinal))
                {Need(!started,"runtime_phase_start");started=true;continue;}
                Need(started && !passed,"runtime_phase_order");
                if(line.StartsWith("TEST_PASS ",StringComparison.Ordinal)){passed=true;continue;}
                var fields=Values(line);
                if(line.StartsWith("NATIVEAOT_GC_STRESS_COLLECT ",StringComparison.Ordinal))
                {ConsumeCollections(1,3);gc++;}
                else if(line.StartsWith("NATIVEAOT_GC_STRESS_JOIN ",StringComparison.Ordinal))
                {
                    Need(normalReaps>=5 && deadlines.Count!=0,"runtime_gc_join_ownership");
                    normalReaps-=5;deadlines.RemoveAt(0);joins++;
                }
                else if(line.StartsWith("NATIVEAOT_MATRIX_GC ",StringComparison.Ordinal))
                {
                    ConsumeCollections(2,1);Need(normalReaps>=2,"runtime_matrix_join_ownership");
                    normalReaps-=2;gc++;
                }
                else if(line.StartsWith("NATIVEAOT_TP_STRESS_TIMER ",StringComparison.Ordinal))
                {Frequency(fields);Times(fields["before"],fields["after"]);ConsumeDeadline(fields["before"],fields["after"]);timers++;}
                else if(line.StartsWith("NATIVEAOT_MATRIX_ASYNC ",StringComparison.Ordinal))
                {
                    Frequency(fields);Times(fields["delayBefore"],fields["after"]);ConsumeDeadline(fields["delayBefore"],fields["delayAfter"]);
                    ConsumeDeadline(fields["timerBefore"],fields["after"]);timers++;
                }
            }
        }
        private void Times(ulong before,ulong after)
        {EarliestClock=Math.Min(EarliestClock,before);LatestClock=Math.Max(LatestClock,after);}
        private void ConsumeCollections(int count,int parked)
        {
            for(int i=0;i<count;i++)
            {
                int at=collections.FindIndex(p=>p.Preempted && p.Parked>=parked);
                Need(at>=0,"runtime_collection_kernel_evidence");collections.RemoveAt(at);
            }
        }
        private void ConsumeDeadline(ulong before,ulong after)
        {
            int at=deadlines.FindIndex(d=>d.Deadline>=before && d.Clock>=d.Deadline && d.Clock<=after);
            Need(at>=0,"runtime_timer_kernel_evidence");deadlines.RemoveAt(at);
        }
        private static void Frequency(Dictionary<string,ulong> values)=>Need(values["frequency"]==1_000_000_000,"runtime_clock_frequency");
        private static Dictionary<string,ulong> Values(string row)
        {
            Dictionary<string,ulong> values=[];
            foreach(string field in row.Split(' ').Skip(1))
            {
                int at=field.IndexOf('=');
                if(at>0 && ulong.TryParse(field.AsSpan(at+1),NumberStyles.None,CultureInfo.InvariantCulture,out ulong value))values.Add(field[..at],value);
            }
            return values;
        }
        internal void Finish()
        {
            Need(started && passed && written==bytes && position==rows.Length,"runtime_phase_complete");
            Need(app=="NativeAotSuspensionStress"?gc==9 && joins==3:
                app=="NativeAotRuntimeMatrix"?gc==3 && timers==3:timers==3,"runtime_phase_inventory");
        }
    }
}
