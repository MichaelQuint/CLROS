using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using OS.Boot;
using OS.Kernel.Boot;
using OS.Kernel.Memory;

internal static partial class M01LivePagingEfiEntryTests
{
    private readonly record struct AllocationCleanupScenario(string Name, AdapterAllocationOutcome Code, AdapterAllocationOutcome Probe, AdapterAllocationOutcome Table, bool FailAfterAllocations, string ExpectedCleanup);
}
