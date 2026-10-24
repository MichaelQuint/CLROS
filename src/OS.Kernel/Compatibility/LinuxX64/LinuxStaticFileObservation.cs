namespace OS.Kernel.Compatibility.LinuxX64;

/// <summary>Finite static-app lifecycle observations under one serialized native
/// owner. The caller authenticates thread generations, copied output, actual
/// syscall results and monotonic time. This value neither sends a signal nor
/// schedules, completes HTTP, cancels workers or authorizes resource release.</summary>
public unsafe struct LinuxStaticFileObservation
{
    public const ulong RequestDelayNanoseconds = 120_000_000_000;
    public const ulong GraceNanoseconds = 60_000_000_000;
    private ulong main, boundWriter, readyAt, requestedAt, exitedAt, last;
    private uint boundFd, position;
    private bool entered, bound, ready, requested, ended, exited, closed, failed;
    public enum Progress : byte { Rejected, Waiting, RequestTermination, Draining, Exited, GraceExpired }
    public readonly bool Ready => ready && !failed;
    public readonly bool Requested => requested;
    public readonly bool GracefulExit => entered && ready && requested && ended && exited && closed && !failed;
    public readonly bool Failed => failed;
    public readonly ulong MainGeneration => main;
    public readonly ulong BindGeneration => boundWriter;
    public readonly ulong ReadyNanoseconds => readyAt;
    public readonly ulong RequestedNanoseconds => requestedAt;
    public readonly ulong ExitedNanoseconds => exitedAt;

    /// <summary>Bind the actual handle returned by Process.Start, before user
    /// entry. A reused/default observer or empty handle rejects unchanged.</summary>
    public bool Begin(ulong generation)
    {
        if (main != 0 || generation == 0) return false;
        main = generation; return true;
    }

    /// <summary>Borrow one already copied stdout/stderr record. Exact stderr
    /// entry/end lines may split across calls, but both belong to the original
    /// main generation. End before the owned request, extra stderr or mismatch
    /// permanently rejects. Stdout is accounted by the separate log budget.</summary>
    public bool ObserveOutput(ulong writer, uint channel, byte* source, uint bytes)
    {
        if (failed || main == 0 || writer == 0 || channel is not (1 or 2) || source == null || bytes is 0 or > 8192)
            return Reject();
        if (channel == 1) return !exited || Reject();
        if (writer != main || ended) return Reject();
        for (uint i = 0; i < bytes; i++)
        {
            if (entered && !requested) return Reject();
            string expected = entered ? "CLROS_STATIC_FILES_APP_END v1\n" : "CLROS_STATIC_FILES_APP_ENTRY v1\n";
            if (source[i] != expected[(int)position]) return Reject();
            if (++position == expected.Length)
            {
                position = 0;
                if (entered) { ended = true; if (i + 1 != bytes) return Reject(); }
                else entered = true;
            }
        }
        return true;
    }

    /// <summary>Observe the actual first successful bind and its fd. Socket
    /// family/address/port policy stays with the separately admitted socket owner.</summary>
    public bool RecordBind(ulong writer, uint fd, bool succeeded)
    {
        if (failed || main == 0 || !entered || bound || ready || writer == 0 || fd >= LinuxDescriptorServices.MaximumDescriptors || !succeeded)
            return Reject();
        boundWriter = writer; boundFd = fd; bound = true; return true;
    }

    /// <summary>Arm the one-shot request deadline only after an actual listen
    /// on the bound descriptor and a nonregressing owned monotonic sample.</summary>
    public bool RecordListen(ulong writer, uint fd, bool succeeded, ulong now)
    {
        if (failed || !bound || ready || writer == 0 || fd != boundFd || !succeeded || !Time(now)) return Reject();
        readyAt = now; ready = true; return true;
    }

    /// <summary>Observe actual last listener close. This is independent of the
    /// later descriptor/transport/DMA cleanup receipts, which remain mandatory.</summary>
    public bool RecordClose(uint fd, bool succeeded)
    {
        if (failed || main == 0) return Reject();
        if (bound && fd == boundFd)
        { if (!requested || !succeeded || closed) return Reject(); closed = true; }
        return true;
    }

    /// <summary>Read finite policy at a restored supervisor turn. Request is
    /// only a proposed SIGTERM admission, never HTTP success or cancellation.
    /// Expiry is terminal failure; later app output cannot forgive it.</summary>
    public Progress Poll(ulong now)
    {
        if (failed || main == 0 || !Time(now)) { Reject(); return Progress.Rejected; }
        if (exited) return GracefulExit ? Progress.Exited : Progress.Rejected;
        if (!ready) return Progress.Waiting;
        if (!requested) return now - readyAt >= RequestDelayNanoseconds ? Progress.RequestTermination : Progress.Waiting;
        if (now - requestedAt >= GraceNanoseconds) { Reject(); return Progress.GraceExpired; }
        return Progress.Draining;
    }

    /// <summary>Record one successful owner-only signal request to the retained
    /// main generation. The caller has actually queued/woken it. Duplicate,
    /// early, mismatched or regressing observations reject permanently.</summary>
    public bool RecordRequest(ulong target, ulong now)
    {
        if (failed || !ready || requested || target != main || !Time(now) || now - readyAt < RequestDelayNanoseconds) return Reject();
        requestedAt = now; requested = true; return true;
    }

    /// <summary>Observe the real captured exit_group(0) after APP_END from the
    /// initial generation. This marks app exit only; native off-stack reap and
    /// cleanup are independently required before any success marker.</summary>
    public bool RecordExit(ulong writer, uint code, ulong now)
    {
        if (failed || writer != main || !ready || !requested || !ended || !closed || exited || code != 0 ||
            !Time(now) || now - requestedAt >= GraceNanoseconds) return Reject();
        exitedAt = now; exited = true; return true;
    }

    private bool Time(ulong now)
    {
        if (now > long.MaxValue || now < last || now > (ulong)long.MaxValue - RequestDelayNanoseconds - GraceNanoseconds) return false;
        last = now; return true;
    }
    private bool Reject() { failed = true; return false; }
}
