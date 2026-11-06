using System.Text;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Manifest;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    private sealed partial class ThreadingProtocol
    {
        internal (int Accepts, ulong Sent, ulong Received) ServiceSocketCounts() => (socketAccepts, socketSent, socketReceived);
    }

    private sealed partial class ThreadingProtocol
    {
        private bool serviceReady;
        private ulong servicePeakIdentity, servicePeakCalls, serviceBudgetFinished;
        private void ServiceBegin()
        {
            Exact("TEST_START m16.014");
            const string policy = " elapsedLimit=none counters=reject-overflow workers=individual cancellation=owned";
            string line = Take();
            Need(line.EndsWith(policy, StringComparison.Ordinal), "service_budget_policy");
            var row = ReadRecord(line[..^policy.Length], "M16_014_POLICY", "started:dec", "callsPerTurn:dec", "progressNanos:dec");
            Need(row["callsPerTurn"] == LinuxSyscallGateway.MaximumThreadCalls && row["progressNanos"] == LinuxThreadExecutionBudget.ServiceProgressNanoseconds && budget.Begin((uint)capacity, row["started"], application: true, continuingWeb: true, continuingService: true), "service_budget_policy");
            // The initial network snapshot predates its first clock read. It
            // is an exact bootstrap sentinel, not a reading before this anchor.
            Exact("M16_012_POLL identity=0 clock=0 ready=1 idle=0");
        }

        private void ServiceClock(ulong now, ref LinuxThreadExecutionBudget observed)
        {
            // Cancellation ends operational admission. Actual exit/drain/reap
            // records remain mandatory and must not revive this watchdog.
            if (observed.CancellationRequested)
                return;
            bool Runnable(RuntimeThread thread) => !thread.Exiting && (ready.Contains(thread) || thread == socketSelected && !thread.Parking);
            if (!observed.ObserveProgress(now, (ulong)calls, history.Any(thread => !thread.Reaped && Runnable(thread))))
                throw new ToolFailure("service_progress_watchdog", $"position={position} now={now} calls={calls} ready={ready.Count} selected={socketSelected?.Identity ?? 0} row={rows[Math.Min(position, rows.Length - 1)]}");
            foreach (var thread in history.Where(thread => !thread.Reaped))
                Need(observed.ObserveWorker(now, (uint)thread.Identity - 1, thread.TotalCalls, Runnable(thread)), "service_worker_watchdog");
        }

        private void ServiceCall(RuntimeThread thread, LinuxCallWaitObservation witness)
        {
            Need(!budget.CancellationRequested && thread.TotalCalls < ulong.MaxValue && witness.Ordinal == thread.TotalCalls + 1, "service_worker_call_counter");
            ulong turn = (ulong)epochs + 1;
            if (thread.TurnEpoch != turn)
            {
                thread.TurnEpoch = turn;
                thread.TurnCalls = 0;
            }

            Need(thread.TurnCalls < LinuxSyscallGateway.MaximumThreadCalls, "service_turn_quota");
            thread.TurnCalls++;
            thread.TotalCalls = witness.Ordinal;
            if (thread.TotalCalls > servicePeakCalls)
            {
                servicePeakCalls = thread.TotalCalls;
                servicePeakIdentity = thread.Identity;
            }
        }

        private void ServiceProgress()
        {
            if (!Starts("M16_014_PROGRESS "))
                return;
            const string suffix = " owner=live cleanup=pending";
            string line = Take();
            Need(!serviceReady && line.EndsWith(suffix, StringComparison.Ordinal), "service_ready_once");
            var row = ReadRecord(line[..^suffix.Length], "M16_014_PROGRESS", "identity:dec", "calls:dec", "started:dec", "nanos:dec", "epochs:dec", "active:dec");
            Need(row["identity"] == servicePeakIdentity && row["calls"] == servicePeakCalls && servicePeakCalls > 4096 && history.Any(thread => !thread.Reaped && thread.Identity == servicePeakIdentity && thread.TotalCalls == servicePeakCalls) && row["started"] == budget.StartedNanoseconds && row["nanos"] >= clock && row["nanos"] >= row["started"] && row["nanos"] - row["started"] > LinuxThreadExecutionBudget.RuntimeNanoseconds && row["epochs"] == (ulong)epochs && epochs > 0 && row["active"] == (ulong)active, "service_ready_accounting");
            // This supervisor snapshot is emitted before the pending network
            // poll publishes its older clock sample. Validate it against a copy
            // of the same watchdog; do not reorder the actual poll/call stream.
            var observed = budget;
            ServiceClock(row["nanos"], ref observed);
            Exact("CLROS_M16_014_READY");
            serviceReady = true;
        }

        private void ServiceFinish()
        {
            const string suffix = " process=drained mappings=restored pool=scrubbed random=zero reservation=retained";
            string line = Take();
            Need(serviceReady && budget.CancellationRequested && line.EndsWith(suffix, StringComparison.Ordinal), "service_owner_cleanup");
            var row = ReadRecord(line[..^suffix.Length], "M16_014_END", "identity:dec", "calls:dec", "started:dec", "nanos:dec", "reaped:dec");
            Need(row["identity"] == servicePeakIdentity && row["calls"] == servicePeakCalls && servicePeakCalls > 4096 && row["started"] == budget.StartedNanoseconds && row["nanos"] >= Math.Max(clock, serviceBudgetFinished) && row["nanos"] <= long.MaxValue && row["nanos"] - row["started"] > LinuxThreadExecutionBudget.RuntimeNanoseconds && row["reaped"] == (ulong)reaped && history.All(thread => thread.Reaped), "service_owner_accounting");
            Exact("TEST_PASS m16.014");
        }
    }
}
