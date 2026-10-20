namespace OS.Tooling;

public static partial class QemuTranscripts
{
    private sealed partial class ThreadingProtocol
    {
        // Socket event masks/data remain opaque. For the probe's read endpoint,
        // an empty live-writer pipe cannot supply any readable event; bytes or
        // writer closure supply a necessary readiness bound, not copied events.
        private int SocketReadyUpperBound(SocketDescriptor port)=>port.Watches.Count+
            port.ProbeReadWatches.Count(w=>ProbeReadWatchLive(w.Key,w.Value) &&
                (w.Value.Buffered!=0 || w.Value.Write==0));

        private bool ProbeReadWatchLive(int fd,WebProbePipe pipe)=>pipe.Read==fd && files.Contains(fd) &&
            descriptorGenerations[fd]==pipe.ReadGeneration;

        private WebProbePipe? ProbeReadTarget(ulong fd)=>web && !service && webProbePipe is { } pipe &&
            fd<=int.MaxValue && pipe.Read!=0 && fd==(ulong)pipe.Read && ProbeReadWatchLive((int)fd,pipe)?pipe:null;

        private void ProbeReadClosed(int fd,WebProbePipe pipe)
        {
            foreach(var port in socketDescriptions.Where(d=>d.Port))
                if(port.ProbeReadWatches.TryGetValue(fd,out var prior) && prior==pipe)port.ProbeReadWatches.Remove(fd);
        }
    }
}
