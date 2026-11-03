using System.Globalization;
using System.Text.RegularExpressions;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Threading;
using OS.Manifest;
using Race = OS.Tooling.LinuxDiskRaceProofFixture;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Copied scalar checks of the exact two-worker mechanical leaf.
    /// Pending windows and all known auxiliary rows retain original indexes for
    /// parent/sector/timing joins. This inspector checks reported transaction
    /// ordering. Only the explicit eight-stage ExitPending entry point adds its
    /// finite FIFO replay. Neither entry point interprets arbitrary instructions
    /// or proves sentinel/data memory, original firmware/device or a whole guest.</summary>

    public sealed class LinuxDiskRaceLeafReport
    {
        public string Variant
        {
            get;
        }
        public int Transfers
        {
            get;
        }
        public ulong Epochs
        {
            get;
        }
        public bool CheckedCalls => true;
        public bool CheckedWaitCompletions => true;
        public bool CheckedLifecycleRecords => true;
        public bool CheckedCrossWorkerOrdering => true;
        public bool CheckedSchedulerReplay => ExitPendingStages is not null;
        public LinuxDiskExitPendingValues? ExitPendingStages
        {
            get;
        }
        public LinuxDiskExitPendingRecord? ExitCleanupEnd
        {
            get;
        }
        public bool CheckedUserMemoryOrAuxiliaryJoins => false;
        public bool CheckedSignalReturnEnvelopes => Variant is "InterruptWins" or "CompletionWins";
        public bool CheckedPostDeathSentinelOrOrphanDrain => false;
        public bool CompletionWinnerProvesBackendDataRace => false;
        public IReadOnlyList<LinuxDiskRaceCall> Calls
        {
            get;
        }
        public IReadOnlyList<LinuxDiskRaceWindow> Windows
        {
            get;
        }
        public IReadOnlyList<LinuxDiskAuxiliary> Auxiliary
        {
            get;
        }
        public IReadOnlyList<LinuxDiskRaceAbandoned> Abandoned
        {
            get;
        }
        public Race.MemoryLayout Memory => Race.Memory;
        public int MainReadCallIndex
        {
            get;
        }
        public int MainReadCompletionIndex
        {
            get;
        }
        public int ChildCloseIndex
        {
            get;
        }
        public int ChildSignalIndex
        {
            get;
        }

        internal LinuxDiskRaceLeafReport(string variant, int transfers, ulong epochs, List<LinuxDiskRaceCall> calls, List<LinuxDiskRaceWindow> windows, List<LinuxDiskAuxiliary> auxiliary, int read, int complete, int close, List<LinuxDiskRaceAbandoned> abandoned, int signal, LinuxDiskExitPendingValues? stages = null, LinuxDiskExitPendingRecord? cleanupEnd = null)
        {
            Variant = variant;
            Transfers = transfers;
            Epochs = epochs;
            Calls = Array.AsReadOnly(calls.ToArray());
            Windows = Array.AsReadOnly(windows.ToArray());
            Auxiliary = Array.AsReadOnly(auxiliary.ToArray());
            MainReadCallIndex = read;
            MainReadCompletionIndex = complete;
            ChildCloseIndex = close;
            Abandoned = Array.AsReadOnly(abandoned.ToArray());
            ChildSignalIndex = signal;
            ExitPendingStages = stages;
            ExitCleanupEnd = cleanupEnd;
        }
    }
}
