namespace OS.Kernel.Diagnostics;

public static unsafe partial class SeparateDisabledDefaultPageFaultTransition
{
    private struct DescriptorScratch
    {
        public fixed ulong Words[23];
    }
}
