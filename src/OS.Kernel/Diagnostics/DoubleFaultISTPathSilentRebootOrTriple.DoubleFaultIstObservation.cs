namespace OS.Kernel.Diagnostics;
/// <summary>Synthetic current state is separate from undefined diagnostic saved program state.</summary>

public readonly struct DoubleFaultIstObservation
{
    public DoubleFaultIstObservation(CpuFaultFrame frame, ulong savedCs, ulong savedSs, ulong currentRsp, ulong currentCs, ulong currentSs, ulong gdtrBase, ulong gdtrLimit, ulong tr, ulong idtrBase, ulong idtrLimit, ulong tssAccess, ulong ist1, ulong dfGateLow, ulong dfGateHigh, ulong pfGateLow, ulong pfGateHigh, DoubleFaultIstWalk zero, DoubleFaultIstWalk df, DoubleFaultIstWalk pf, DoubleFaultIstWalk nmi, DoubleFaultIstWalk transition, bool claimedGuestCapture = false)
    {
        IsInitialized = true;
        Frame = frame;
        SavedCs = savedCs;
        SavedSs = savedSs;
        CurrentRsp = currentRsp;
        CurrentCs = currentCs;
        CurrentSs = currentSs;
        GdtrBase = gdtrBase;
        GdtrLimit = gdtrLimit;
        Tr = tr;
        IdtrBase = idtrBase;
        IdtrLimit = idtrLimit;
        TssAccess = tssAccess;
        Ist1 = ist1;
        DfGateLow = dfGateLow;
        DfGateHigh = dfGateHigh;
        PfGateLow = pfGateLow;
        PfGateHigh = pfGateHigh;
        Zero = zero;
        Df = df;
        Pf = pf;
        Nmi = nmi;
        Transition = transition;
        ClaimedGuestCapture = claimedGuestCapture;
    }

    public bool IsInitialized
    {
        get;
    }
    public bool ClaimedGuestCapture
    {
        get;
    }
    public CpuFaultFrame Frame
    {
        get;
    }
    public ulong SavedCs
    {
        get;
    }
    public ulong SavedSs
    {
        get;
    }
    public ulong CurrentRsp
    {
        get;
    }
    public ulong CurrentCs
    {
        get;
    }
    public ulong CurrentSs
    {
        get;
    }
    public ulong GdtrBase
    {
        get;
    }
    public ulong GdtrLimit
    {
        get;
    }
    public ulong Tr
    {
        get;
    }
    public ulong IdtrBase
    {
        get;
    }
    public ulong IdtrLimit
    {
        get;
    }
    public ulong TssAccess
    {
        get;
    }
    public ulong Ist1
    {
        get;
    }
    public ulong DfGateLow
    {
        get;
    }
    public ulong DfGateHigh
    {
        get;
    }
    public ulong PfGateLow
    {
        get;
    }
    public ulong PfGateHigh
    {
        get;
    }
    public DoubleFaultIstWalk Zero
    {
        get;
    }
    public DoubleFaultIstWalk Df
    {
        get;
    }
    public DoubleFaultIstWalk Pf
    {
        get;
    }
    public DoubleFaultIstWalk Nmi
    {
        get;
    }
    public DoubleFaultIstWalk Transition
    {
        get;
    }

    internal DoubleFaultIstWalk Walk(int role) => role == 0 ? Zero : role == 1 ? Df : role == 2 ? Pf : role == 3 ? Nmi : Transition;
}
