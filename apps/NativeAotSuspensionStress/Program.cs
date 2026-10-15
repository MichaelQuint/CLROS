using System.Diagnostics;

// A separate upstream managed workload. This is neither a kernel collector nor
// proof of NativeAOT/CLROS execution. Original focused payloads remain unchanged.
internal static class Program
{
    private const int Lifetimes=3, Collections=3, Workers=5, TimeoutSeconds=20;
    [ThreadStatic] private static int s_tls;

    private sealed class WorkerState
    {
        internal int Ready, Sleeping, SleepStarts, Roots, Interior, Tls, Completed;
        internal long Progress;
        internal Exception? Failure;
    }

    private sealed class Lifetime
    {
        // Disable the managed spin phase so blocked contexts reach a real wait.
        internal readonly ManualResetEventSlim Release=new(false,0), Exit=new(false,0);
        internal int Stop;
    }

    private static int Main(string[] args)
    {
        Console.WriteLine("TEST_START nativeaot.gc-stress");
        if(args.Length!=0)
        {
            Console.WriteLine("TEST_FAIL nativeaot.gc-stress invalid_arguments"); return 1;
        }
        try
        {
            for(int lifetime=1;lifetime<=Lifetimes;lifetime++)Run(lifetime);
            Console.WriteLine("TEST_PASS nativeaot.gc-stress"); return 0;
        }
        catch(Exception error)
        {
            Console.WriteLine("TEST_FAIL nativeaot.gc-stress "+error.GetType().Name+" "+error.Message.ReplaceLineEndings(" "));
            return 1;
        }
    }

    private static void Run(int ordinal)
    {
        Lifetime owner=new();
        WorkerState[] states=Enumerable.Range(0,Workers).Select(_=>new WorkerState()).ToArray();
        Thread[] threads=new Thread[Workers];
        for(int i=0;i<threads.Length;i++)
        {
            int role=i;
            threads[i]=new Thread(()=>Work(role,states[role],owner,threads[1])){IsBackground=true};
        }
        int started=0,joined=0,exitRequests=0;
        s_tls=17+ordinal;
        byte[] mainRoots=new byte[8192];mainRoots[0]=53;mainRoots[^1]=59;
        try
        {
            foreach(Thread thread in threads){thread.Start();started++;}
            Wait(()=>states.All(s=>Volatile.Read(ref s.Ready)==1) && Volatile.Read(ref states[0].Progress)>0,states,"start");
            for(int epoch=1;epoch<=Collections;epoch++)
            {
                // These are runtime-observed managed wait states. Only native
                // traces/guest records can qualify actual kernel wait ownership.
                Wait(()=>Waiting(threads),states,"parked_contexts");
                if(epoch==1)Require(Volatile.Read(ref states[2].Sleeping)==1,"sleep_window");
                long progressBefore=Volatile.Read(ref states[0].Progress);
                int before=GC.CollectionCount(GC.MaxGeneration);
                // Request an exit concurrently with this collection. Whether
                // exit actually overlaps suspension must be measured externally.
                if(epoch==2){owner.Exit.Set();exitRequests++;}
                // GC itself cannot safely be cancelled here. The enclosing
                // publisher/guest runner must independently bound this process.
                GC.Collect(GC.MaxGeneration,GCCollectionMode.Forced,blocking:true,compacting:true);
                int after=GC.CollectionCount(GC.MaxGeneration);
                long returned=Volatile.Read(ref states[0].Progress);
                Wait(()=>Volatile.Read(ref states[0].Progress)>returned,states,"resume_progress");
                long progressAfter=Volatile.Read(ref states[0].Progress);
                Require(after>before && mainRoots[0]==53 && mainRoots[^1]==59 && s_tls==17+ordinal,"main_roots_tls");
                Console.WriteLine(FormattableString.Invariant($"NATIVEAOT_GC_STRESS_COLLECT lifetime={ordinal} epoch={epoch} before={before} after={after} progressBefore={progressBefore} progressReturned={returned} progressAfter={progressAfter} waiting=3"));
            }
            GC.KeepAlive(mainRoots);
        }
        finally
        {
            Volatile.Write(ref owner.Stop,1);owner.Release.Set();owner.Exit.Set();
            long cleanup=Stopwatch.GetTimestamp();
            foreach(Thread thread in threads.Take(started))
            {
                TimeSpan remaining=TimeSpan.FromSeconds(TimeoutSeconds)-Stopwatch.GetElapsedTime(cleanup);
                if(remaining>TimeSpan.Zero && thread.Join(remaining))joined++;
            }
            if(joined==started){owner.Release.Dispose();owner.Exit.Dispose();}
        }
        Require(started==Workers && joined==Workers && states.All(s=>s.Failure is null),"worker_join");
        Require(states.All(s=>s.Roots==1 && s.Interior==1 && s.Tls==1 && s.Completed==1) && s_tls==17+ordinal && exitRequests==1,"joined_roots_tls");
        Console.WriteLine(FormattableString.Invariant($"NATIVEAOT_GC_STRESS_JOIN lifetime={ordinal} started={started} joined={joined} completed={states.Sum(s=>s.Completed)} roots={states.Sum(s=>s.Roots)} interior={states.Sum(s=>s.Interior)} tls={states.Sum(s=>s.Tls)} sleepStarts={states.Sum(s=>s.SleepStarts)} exitRequests={exitRequests}"));
    }

    private static void Work(int role,WorkerState state,Lifetime owner,Thread parked)
    {
        try
        {
            Require(s_tls==0,"worker_tls_initial");
            s_tls=29+role;
            byte[] rooted=new byte[8192];rooted[0]=(byte)(37+role);rooted[^1]=(byte)(43+role);
            ref byte interior=ref rooted[4096];interior=(byte)(59+role);
            if(role==2)Volatile.Write(ref state.Sleeping,1);
            Volatile.Write(ref state.Ready,1);
            switch(role)
            {
                case 0:
                    while(Volatile.Read(ref owner.Stop)==0)
                    {
                        Require(rooted[0]==37+role && rooted[^1]==43+role && interior==59+role && s_tls==29+role,"running_roots_tls");
                        Interlocked.Increment(ref state.Progress);
                    }
                    break;
                case 1: Require(owner.Release.Wait(TimeSpan.FromSeconds(TimeoutSeconds)),"park_timeout");break;
                case 2:
                    state.SleepStarts++;Thread.Sleep(1000);Volatile.Write(ref state.Sleeping,0);
                    Require(owner.Release.Wait(TimeSpan.FromSeconds(TimeoutSeconds)),"sleep_release_timeout");break;
                case 3: Require(parked.Join(TimeSpan.FromSeconds(TimeoutSeconds)),"join_timeout");break;
                case 4: Require(owner.Exit.Wait(TimeSpan.FromSeconds(TimeoutSeconds)),"exit_timeout");break;
            }
            state.Roots=rooted[0]==37+role && rooted[^1]==43+role?1:0;
            state.Interior=interior==59+role?1:0;state.Tls=s_tls==29+role?1:0;state.Completed++;
            GC.KeepAlive(rooted);
        }
        catch(Exception error){Volatile.Write(ref state.Failure,error);Volatile.Write(ref state.Ready,1);}
    }

    private static void Wait(Func<bool> ready,WorkerState[] states,string reason)
    {
        long start=Stopwatch.GetTimestamp();
        while(!ready())
        {
            Require(states.All(s=>Volatile.Read(ref s.Failure) is null),"worker_failure");
            Require(Stopwatch.GetElapsedTime(start)<TimeSpan.FromSeconds(TimeoutSeconds),reason+"_timeout");
            Thread.Yield();
        }
        Require(states.All(s=>Volatile.Read(ref s.Failure) is null),"worker_failure");
    }

    private static bool Waiting(Thread[] threads)
    {
        for(int i=1;i<4;i++)if((threads[i].ThreadState&System.Threading.ThreadState.WaitSleepJoin)==0)return false;
        return true;
    }

    private static void Require(bool condition,string reason)
    {if(!condition)throw new InvalidOperationException(reason);}
}
