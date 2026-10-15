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
        private readonly Dictionary<(ulong Identity,ulong Sequence),(ulong Before,ulong Woke)> wakes=[];
        private readonly List<(ulong Before,ulong Woke,ulong Resumed)> resumes=[];
        private readonly List<(ulong Before,ulong Woke,ulong Resumed,ulong After)> wakeIntervals=[];
        private readonly List<(ulong Lower,ulong Upper)> clockSamples=[];
        private readonly ulong hpetFrequencyHz;
        private ulong lastClockUpper;
        private ulong lastMonotonicClock;
        private bool hasMonotonicClock;
        private int position,normalReaps,totalReaps,matrixReapsDue,gc,joins,timers,written;
        private bool started,passed;
        internal ulong EarliestClock { get; private set; }=ulong.MaxValue;
        internal ulong LatestClock { get; private set; }

        internal RuntimePhases(string app,string stdout,IReadOnlyList<int> lengths,ulong hpetFrequencyHz)
        {
            if(app=="NativeAotRuntimeSmoke")NativeAotThreadingOutput.ValidateRuntimeSmokeOutput(stdout);
            else NativeAotRuntimePayload.ValidateOutput(app,stdout);
            this.app=app;
            this.hpetFrequencyHz=hpetFrequencyHz;
            Need(app!="NativeAotRuntimeMatrix" || hpetFrequencyHz is >=1_000_000 and <=10_000_000_000,"runtime_clock_domain");
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

        // A due managed timer need not produce a kernel timeout. Require an
        // explicit matched wake and exact generation/sequence resumption,
        // bracketed by successful clock reads with validated clock progress.
        // The untimestamped original additionally requires prior normal reap.
        // Clear-TID, requeue and stdout alone cannot mint this evidence.
        private bool ObserveWake=>app=="NativeAotRuntimeSmoke" && totalReaps>0;
        // The actual CLOCK_MONOTONIC read occurs between admitted and dispatched.
        // Convert both HPET bounds exactly as LinuxStartupServices.Clock does.
        // These windows bound returned timestamps, not timer/callback identity.
        internal void ClockRead(Dictionary<string,ulong> timing)
        {
            if(app!="NativeAotRuntimeMatrix")return;
            ulong period=timing["periodFs"];
            Need(1_000_000_000_000_000UL%period==0 && 1_000_000_000_000_000UL/period==hpetFrequencyHz,"runtime_clock_domain");
            ulong lower=Nanoseconds(timing["admitted"]),upper=Nanoseconds(timing["dispatched"]);
            Need(lower>=lastClockUpper && upper>=lower,"runtime_clock_sample_order");
            lastClockUpper=upper;clockSamples.Add((lower,upper));
        }
        private ulong Nanoseconds(ulong ticks)
        {
            UInt128 value=(UInt128)(ticks/hpetFrequencyHz)*1_000_000_000+
                (UInt128)(ticks%hpetFrequencyHz)*1_000_000_000/hpetFrequencyHz;
            Need(value<=long.MaxValue,"runtime_clock_sample_range");return (ulong)value;
        }
        private void ConsumeTimestamp(ulong value)
        {
            int at=clockSamples.FindIndex(s=>s.Lower<=value && value<=s.Upper);
            Need(at>=0,"runtime_clock_sample_missing");clockSamples.RemoveRange(0,at+1);
        }
        internal void MonotonicRead(ulong clock)
        {
            if(!ObserveWake)return;
            lastMonotonicClock=clock;hasMonotonicClock=true;
            for(int i=resumes.Count-1;i>=0;i--)
            {
                var proof=resumes[i];
                if(proof.Before<clock && proof.Resumed<=clock)
                {wakeIntervals.Add((proof.Before,proof.Woke,proof.Resumed,clock));resumes.RemoveAt(i);}
            }
        }
        internal void FutexWoken(ulong identity,ulong sequence,ulong clock)
        {
            if(ObserveWake && hasMonotonicClock)wakes.Add((identity,sequence),(lastMonotonicClock,clock));
        }
        internal void FutexResumed(ulong identity,ulong sequence,ulong clock)
        {
            if(wakes.Remove((identity,sequence),out var proof) && proof.Before<=proof.Woke && proof.Woke<=clock)
                resumes.Add((proof.Before,proof.Woke,clock));
        }
        private bool ConsumeWakeInterval(ulong before,ulong after)
        {
            int at=wakeIntervals.FindIndex(p=>p.Before>=before && p.After<=after);
            if(at<0)return false;
            var proof=wakeIntervals[at];Times(proof.Before,proof.After);wakeIntervals.RemoveAt(at);return true;
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
                {Frequency(fields);Times(fields["before"],fields["after"]);ConsumeTimer(fields["before"],fields["after"]);timers++;}
                else if(line.StartsWith("NATIVEAOT_MATRIX_ASYNC ",StringComparison.Ordinal))
                {
                    Frequency(fields);Times(fields["delayBefore"],fields["after"]);
                    foreach(string endpoint in new[]{"delayBefore","delayAfter","timerBefore","fired","drained","after"})
                        ConsumeTimestamp(fields[endpoint]);
                    timers++;
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
                    else Need(ConsumeWakeInterval(0,ulong.MaxValue),"runtime_smoke_timer_kernel_evidence");
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
        private void ConsumeTimer(ulong before,ulong after)
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
