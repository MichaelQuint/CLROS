namespace OS.Kernel.Compatibility.LinuxX64;
public struct X64LinuxUserTransitionPlan
{
    public ulong CodeAddress, ContextAddress, UserRoot, UserRip, UserRsp, UserFs, UserGs;
    public ulong Dispatcher, FaultReporter, CallbackContext, TssRsp0Address, OriginalGp, OriginalPf;
    public ulong KernelRoot; // Zero retains the byte-identical M16-005 dispatcher path.
    public bool ResumeUser; // Explicit M16-008 mode; requires the separate kernel dispatch root.
    public ulong UserFaultEntry; // Zero retains the original terminal GP/PF bytes.
    public bool UserInterrupts; // Explicit workload opt-in; requires full capture and owned IST4.
    public ulong UserSpuriousEntry; // Independently emitted/verified CPL0 IRETQ leaf, only in IRQ mode.
}
