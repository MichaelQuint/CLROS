using System.Globalization;
using System.Text.RegularExpressions;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Threading;
using OS.Manifest;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Immutable value inspection of the exact mechanical fixture.
    /// Checked flags cover only exact calls, wait/completion associations and
    /// reported exit/reap/restore accounting. Auxiliary rows remain mandatory
    /// independent joins; no firmware, native, device, disk bytes, arbitrary
    /// instruction execution or whole-guest acceptance is established.</summary>

    public sealed class LinuxDiskFileLeafReport
    {
        public bool CheckedCalls => true;
        public bool CheckedWaitCompletions => true;
        public bool CheckedLifecycleRecords => true;
        public bool CheckedAuxiliaryJoins => false;
        public int Calls { get; }
        public int Transfers { get; }
        public ulong Epochs { get; }
        public IReadOnlyList<LinuxDiskCompletion> Completions { get; }
        public IReadOnlyList<LinuxDiskAuxiliary> Auxiliary { get; }

        internal LinuxDiskFileLeafReport(int calls, int transfers, ulong epochs, List<LinuxDiskCompletion> completions, List<LinuxDiskAuxiliary> auxiliary)
        {
            Calls = calls;
            Transfers = transfers;
            Epochs = epochs;
            Completions = Array.AsReadOnly(completions.ToArray());
            Auxiliary = Array.AsReadOnly(auxiliary.ToArray());
        }
    }
}
