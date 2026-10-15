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
        private readonly List<(ulong Deadline,ulong Clock,int Reaps)> deadlines=[];
        private readonly Dictionary<(ulong Identity,ulong Sequence),ulong> smokeWakes=[];
        private readonly List<(ulong Clock,int Reads)> smokeResumes=[];
        private ulong smokeFirstClock=ulong.MaxValue,smokeLastClock;
        private int smokeClockReads;
        private int position,normalReaps,totalReaps,matrixReapsDue,gc,joins,timers,written;
        private bool started,passed;
        internal ulong EarliestClock { get; private set; }=ulong.MaxValue;
        internal ulong LatestClock { get; private set; }

        internal RuntimePhases(string app,string stdout,IReadOnlyList<int> lengths)
        {
            if(app=="NativeAotRuntimeSmoke")NativeAotThreadingOutput.ValidateRuntimeSmokeOutput(stdout);
            else NativeAotRuntimePayload.ValidateOutput(app,stdout);
            this.app=app;
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
        internal void Reaped(){normalReaps++;totalReaps++;}
        internal void Deadline(ulong deadline,ulong clock)=>deadlines.Add((deadline,clock,totalReaps));

        // The original app has no timer timestamps. An already-due managed timer
        // may use clock/wake activity without a kernel timeout. Only explicit
        // FUTEX_WAKE and the exact generation/sequence's successful resume mint
        // this alternative; clear-TID, requeue and output alone cannot do so.
        internal void MonotonicRead(ulong clock)
        {
            if(app!="NativeAotRuntimeSmoke" || totalReaps==0)return;
            smokeFirstClock=Math.Min(smokeFirstClock,clock);smokeLastClock=clock;smokeClockReads++;
        }
        internal void FutexWoken(ulong identity,ulong sequence,ulong clock)
        {
            if(app=="NativeAotRuntimeSmoke" && totalReaps>0 && smokeClockReads>0)
                smokeWakes.Add((identity,sequence),clock);
        }
        internal void FutexResumed(ulong identity,ulong sequence,ulong clock)
        {
            if(smokeWakes.Remove((identity,sequence),out ulong woke) && smokeFirstClock<=woke && woke<=clock)
                smokeResumes.Add((clock,smokeClockReads));
        }

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
                    // Managed Join may finish before its native wrapper exits.
                    // Require the real normal reaps by complete owner teardown.
                    ConsumeCollections(2,1);matrixReapsDue+=2;gc++;
                }
                else if(line.StartsWith("NATIVEAOT_TP_STRESS_TIMER ",StringComparison.Ordinal))
                {Frequency(fields);Times(fields["before"],fields["after"]);ConsumeDeadline(fields["before"],fields["after"]);timers++;}
                else if(line.StartsWith("NATIVEAOT_MATRIX_ASYNC ",StringComparison.Ordinal))
                {
                    Frequency(fields);Times(fields["delayBefore"],fields["after"]);ConsumeDeadline(fields["delayBefore"],fields["delayAfter"]);
                    ConsumeDeadline(fields["timerBefore"],fields["after"]);timers++;
                }
                else if(line=="NATIVEAOT_RUNTIME_GC_THREADS_TLS_OK")
                {
                    // This original fixture collects while its worker waits;
                    // it promises no CPU-bound activation/sigreturn. Its pinned
                    // ELF checks GC roots/TLS, and the leaf must witness reap.
                    Need(normalReaps>=1,"runtime_smoke_join_ownership");normalReaps--;joins++;
                }
                else if(line=="NATIVEAOT_RUNTIME_TASK_TIMER_OK")
                {
                    // Preserve real deadline evidence. If the managed timer was
                    // already due, require a matched wake/resume bracketed by
                    // successful monotonic reads and validated clock progress.
                    // Neither route identifies its internal registration or
                    // measures this untimestamped app's precise timer duration.
                    int at=deadlines.FindIndex(d=>d.Reaps>0);
                    if(at>=0)
                    {var timer=deadlines[at];Times(timer.Deadline,timer.Clock);deadlines.RemoveAt(at);}
                    else
                    {
                        at=smokeResumes.FindIndex(r=>r.Reads<smokeClockReads && r.Clock<=smokeLastClock && smokeFirstClock<smokeLastClock);
                        Need(at>=0,"runtime_smoke_timer_kernel_evidence");
                        Times(smokeFirstClock,smokeLastClock);smokeResumes.RemoveAt(at);
                    }
                    timers++;
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
            Need(app!="NativeAotRuntimeMatrix" || matrixReapsDue==6 && normalReaps>=matrixReapsDue,"runtime_matrix_join_ownership");
            Need(app=="NativeAotRuntimeSmoke"?joins==1 && timers==1:
                app=="NativeAotSuspensionStress"?gc==9 && joins==3:
                app=="NativeAotRuntimeMatrix"?gc==3 && timers==3:timers==3,"runtime_phase_inventory");
        }
    }
}
