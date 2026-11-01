namespace OS.Kernel.Threading;
public static unsafe partial class ThreadContextSwitch
{
    public enum ResumeKind : byte
    {
        Entry = 1,
        CapturedCall = 2
    }
}
