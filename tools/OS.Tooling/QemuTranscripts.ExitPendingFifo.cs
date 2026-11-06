using System.Globalization;
using System.Text.RegularExpressions;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Threading;
using OS.Manifest;
using Race = OS.Tooling.LinuxDiskRaceProofFixture;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    // Deliberately finite: the two preparation waits complete before clone,
    // followed by one parked parent read and child group shutdown. This models
    // the existing table's FIFO/Cancel/Exit/Confirm/Reap semantics, not hardware
    // execution or a generic scheduler. Linux-slot Exiting and disk capability
    // are separate from the kernel slot's lifecycle state.
    private sealed class ExitPendingFifo
    {
        private readonly KernelThreadTable.State[] state = [KernelThreadTable.State.Ready, KernelThreadTable.State.Empty];
        private readonly bool[] exiting = new bool[2], cancelled = new bool[2];
        private readonly Queue<int> ready = new([0]);
        private int current = -1, handoff = -1;
        private bool childCreated, diskHeld, shutdown;
        internal void Running(int worker)
        {
            Need(worker is 0 or 1 && handoff < 0 && !state.Contains(KernelThreadTable.State.Exited), "disk_exit_fifo_dispatch");
            if (current < 0)
            {
                Need(ready.Count > 0 && ready.Dequeue() == worker && state[worker] == KernelThreadTable.State.Ready, "disk_exit_fifo_order");
                current = worker;
                state[worker] = KernelThreadTable.State.Running;
            }

            Need(current == worker && state[worker] == KernelThreadTable.State.Running, "disk_exit_fifo_running");
        }

        internal void Poll(int worker, ulong count)
        {
            Need(!shutdown, "disk_exit_fifo_post_shutdown_poll");
            // Idle readiness can be serialized after the sole preparation
            // wait wakes, before its eventual COMPLETE/RESUME. This uniquely
            // identifies worker0; after clone no such inferred wake is admitted.
            if (worker < 0 && count == 1 && !childCreated && state[0] == KernelThreadTable.State.Waiting)
                WakePreparation();
            if (worker >= 0)
                Running(worker);
            else
                Need(current < 0 && handoff < 0, "disk_exit_fifo_poll_owner");
            Need(count == (ulong)ready.Count, "disk_exit_fifo_poll_ready");
        }

        internal void CreateChild()
        {
            Need(current == 0 && !childCreated && state[1] == KernelThreadTable.State.Empty && ready.Count == 0, "disk_exit_fifo_clone");
            childCreated = true;
            state[1] = KernelThreadTable.State.Ready;
            ready.Enqueue(1);
        }

        internal void BeginWait(int worker, bool read)
        {
            Need(current == worker && state[worker] == KernelThreadTable.State.Running && !shutdown && worker == 0 && (read ? childCreated && !diskHeld : !childCreated), "disk_exit_fifo_wait");
            state[worker] = KernelThreadTable.State.Parking;
            if (read)
                diskHeld = true;
        }

        internal void PreparationCompleted(int worker)
        {
            // The only positive disk completions are the sole worker's two
            // pre-clone opens. Wake queues that same waiter; Flush/Dispatch is
            // uniquely determined before its emitted completion/RESUME pair.
            Need(worker == 0 && !childCreated && !shutdown && !diskHeld && current < 0 && handoff < 0, "disk_exit_fifo_prep_completion");
            if (state[0] == KernelThreadTable.State.Waiting)
                WakePreparation();
            Need(state[0] == KernelThreadTable.State.Ready && ready.Count == 1, "disk_exit_fifo_prep_ready");
            Running(0);
        }

        private void WakePreparation()
        {
            Need(!childCreated && !shutdown && !diskHeld && current < 0 && handoff < 0 && ready.Count == 0 && state[0] == KernelThreadTable.State.Waiting, "disk_exit_fifo_prep_wake");
            state[0] = KernelThreadTable.State.Ready;
            ready.Enqueue(0);
        }

        internal void BeforeShutdown()
        {
            Need(current == 1 && handoff < 0 && ready.Count == 0 && diskHeld && !shutdown && state[0] == KernelThreadTable.State.Waiting && state[1] == KernelThreadTable.State.Running && !exiting[0] && !exiting[1] && !cancelled[0] && !cancelled[1], "disk_exit_fifo_pending_stage");
        }

        internal void Shutdown()
        {
            BeforeShutdown();
            shutdown = true;
            exiting[0] = exiting[1] = cancelled[0] = cancelled[1] = true;
            state[0] = KernelThreadTable.State.Ready;
            ready.Enqueue(0);
        }

        internal void BeforeParentCommit()
        {
            Need(shutdown && state[1] == KernelThreadTable.State.Empty && exiting[0] && cancelled[0] && diskHeld, "disk_exit_fifo_parent_continuation");
            Running(0);
            Need(ready.Count == 0, "disk_exit_fifo_parent_queue");
        }

        internal void AfterParentCommit()
        {
            Need(diskHeld && current == 0 && shutdown && exiting[0] && cancelled[0], "disk_exit_fifo_revoke");
            CommitExit(0);
            diskHeld = false;
        }

        private void CommitExit(int worker)
        {
            Need(current == worker && handoff < 0 && state[worker] == KernelThreadTable.State.Running && exiting[worker] && cancelled[worker], "disk_exit_fifo_commit");
            state[worker] = KernelThreadTable.State.Exiting;
            current = -1;
            handoff = worker;
        }

        internal void ExitRecord(int worker)
        {
            if (worker == 1)
            {
                Need(diskHeld, "disk_exit_fifo_early_revoke");
                CommitExit(worker);
            }
            else
                Need(!diskHeld && current < 0 && handoff == 0 && state[0] == KernelThreadTable.State.Exiting, "disk_exit_fifo_parent_exit");
        }

        internal void Supervisor(int worker, uint action, ulong count, ulong active)
        {
            if (action == 3)
            {
                Need(current == worker && handoff < 0 && state[worker] == KernelThreadTable.State.Parking, "disk_exit_fifo_park");
                state[worker] = KernelThreadTable.State.Waiting;
                current = -1;
                handoff = worker;
            }

            Need(current < 0 && handoff == worker && count == (ulong)ready.Count && active == (ulong)state.Count(s => s != KernelThreadTable.State.Empty), "disk_exit_fifo_supervisor");
            if (action is 2 or 4)
            {
                Need(state[worker] == KernelThreadTable.State.Exiting, "disk_exit_fifo_exit_confirmation");
                state[worker] = KernelThreadTable.State.Exited;
            }
            else
                Need(action == 3 && state[worker] == KernelThreadTable.State.Waiting, "disk_exit_fifo_wait_confirmation");
            handoff = -1;
        }

        internal void Reap(int worker)
        {
            Need(current < 0 && handoff < 0 && state[worker] == KernelThreadTable.State.Exited && (worker == 1 ? diskHeld : !diskHeld), "disk_exit_fifo_reap");
            state[worker] = KernelThreadTable.State.Empty;
        }

        internal void Drained() => Need(shutdown && !diskHeld && current < 0 && handoff < 0 && ready.Count == 0 && state.All(s => s == KernelThreadTable.State.Empty), "disk_exit_fifo_drained");
    }
}
