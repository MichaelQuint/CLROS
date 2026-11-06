namespace OS.Kernel.Diagnostics;
/// <summary>
/// Exact caller-owned RCX context for the nonreturning M02-013 transition.
/// The first 168 bytes are the reviewed M02-009 owned GDT/TSS context. This
/// leaf prepares that frozen context first, then replaces only TSS IST1/2/3
/// with the caller's three validated stack tops. The remaining fields own the
/// IDTR image, execution expectations, and the 23-field captured observation.
/// </summary>

public unsafe struct DisabledDefaultUd2VectorStateTransitionContext
{
    public OwnedGdtTssTerminalContext OwnedGdtTss;
    public fixed byte AlignmentReserved[8];
    public fixed byte RequestedIdtr[10];
    public fixed byte RequestedIdtrReserved[6];
    public fixed byte ObservedIdtr[10];
    public fixed byte ObservedIdtrReserved[6];
    public ulong ExpectedIst1StackTop;
    public ulong ExpectedIst2StackTop;
    public ulong ExpectedIst3StackTop;
    public ulong PreparedContextFingerprint;
    public ulong ExpectedRip;
    public ulong ExpectedRsp;
    public ulong ExpectedRflags;
    public ulong ExpectedCr2;
    public ulong ExpectedCr3;
    public ulong ExpectedCs;
    public ulong ExpectedSs;
    public ulong ExpectedR15;
    public ulong CapturedVector;
    public ulong CapturedHasErrorCode;
    public ulong CapturedErrorCode;
    public ulong CapturedRip;
    public ulong CapturedRsp;
    public ulong CapturedRbp;
    public ulong CapturedRflags;
    public ulong CapturedCr2;
    public ulong CapturedCr3;
    public ulong CapturedRax;
    public ulong CapturedRbx;
    public ulong CapturedRcx;
    public ulong CapturedRdx;
    public ulong CapturedRsi;
    public ulong CapturedRdi;
    public ulong CapturedR8;
    public ulong CapturedR9;
    public ulong CapturedR10;
    public ulong CapturedR11;
    public ulong CapturedR12;
    public ulong CapturedR13;
    public ulong CapturedR14;
    public ulong CapturedR15;
    public ulong CaptureCompletion;
}
