namespace OS.Kernel.Diagnostics;

public static unsafe partial class DoubleFaultISTPathSilentRebootOrTriple
{
    private struct DescriptorScratch
    {
        public fixed ulong Words[23];
    }
}
