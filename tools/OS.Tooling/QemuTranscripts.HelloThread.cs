using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    private sealed class HelloThread(int tid, ulong bottom, ulong top, ulong fs)
    {
        internal readonly int Tid = tid;
        internal readonly ulong Bottom = bottom, Top = top;
        internal ulong Fs = fs, WaitAddress;
        internal int Calls, Pending; // 1 futex, 2 signal-pipe read.
        internal bool ReadyCompletion, Reaped, StartupWake, MaskSet;
        internal ulong Identity => 0x100000000UL + (uint)Tid;
    }
}
