using System.Buffers.Binary;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    private sealed partial class ThreadingProtocol
    {
        internal void RequireQuotaPthreads(ServiceQuotaPthreadInspection pthreads, ServiceQuotaMemoryInspection observation)
        {
            var children = history.Where(t => t.Tid != 1).ToArray();
            Need(serviceQuotaFailure && quotaAborted && children.All(t => t.Reaped) && children.Length == pthreads.Threads.Count && children.Zip(pthreads.Threads).All(pair => pair.First.Identity == pair.Second.ChildIdentity && pair.First.Tid == pair.Second.Tid && pair.First.Fs == pair.Second.Tls && pair.First.Bottom == pair.Second.StackStart && pair.First.Top == pair.Second.InitialStackPointer), "pthread_scheduler_join");
            var snapshots = pthreads.Snapshots.ToDictionary(s => s.RecordIndex);
            foreach (var sample in observation.Snapshots)
                Need(snapshots.TryGetValue(sample.RecordIndex + 2, out var s) && s.DynamicBackedBytes == sample.BackedPages * 4096 && s.PthreadBackedBytes <= s.DynamicBackedBytes - sample.GcBackedBytes, "pthread_native_backing_join");
            var gc = observation.GcArena;
            Need(pthreads.Threads.All(t => !HelloMemory.Overlap(t.MappingStart, t.MappingBytes, gc.ArenaStart, gc.ArenaBytes)), "pthread_gc_overlap");
        }
    }
}
