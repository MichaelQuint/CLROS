using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Boot;

internal static unsafe partial class EFIBUFFERTOOSMALLDescriptorGrowthSlackOverflowTests
{
    private struct Scenario
    {
        internal ulong InitialSize;
        internal ulong InitialStride;
        internal ulong FinalSize;
        internal ulong FinalStride;
        internal uint Version;
        internal ulong Slack;
        internal ulong QueryStatus;
        internal ulong AllocationStatus;
        internal ulong FinalStatus;
        internal bool NullAllocation;
        internal bool MissingMap;
        internal bool MissingAllocation;
        internal bool MissingDestination;
        internal bool DefaultSlack;
    }
}
