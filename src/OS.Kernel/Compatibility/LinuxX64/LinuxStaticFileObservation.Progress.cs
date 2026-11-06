namespace OS.Kernel.Compatibility.LinuxX64;

public unsafe partial struct LinuxStaticFileObservation
{
    public enum Progress : byte
    {
        Rejected,
        Waiting,
        RequestTermination,
        Draining,
        Exited,
        GraceExpired
    }
}
