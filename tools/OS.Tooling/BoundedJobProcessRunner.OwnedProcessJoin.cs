// Extracted from the reviewed EFI verifier helper; preserve native lifecycle semantics.
#nullable disable
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace OS.Tooling.Native
{
    public static partial class BoundedJobProcessRunner
    {
        // One synchronous owner remains inside RunCore until all of its original
        // handles signal. Incomplete census facts latch; an empty later job
        // cannot reconstruct an uncaptured process's lifetime.
        private sealed class OwnedProcessJoin : IDisposable
        {
            private readonly SafeKernelHandle job, root;
            private readonly Func<OwnershipJoinObservation, bool> pendingForTest;
            private readonly List<SafeKernelHandle> handles = new();
            private readonly HashSet<(uint Pid, ulong Created)> identities = new();
            private bool captureComplete = true, assigned, terminationRequested, joined;
            private uint active, total;
            private string failure = String.Empty;
            internal bool Uncertain { get; private set; }
            internal string Detail => failure;

            internal OwnedProcessJoin(SafeKernelHandle job, SafeKernelHandle root, Func<OwnershipJoinObservation, bool> pendingForTest)
            {
                this.job = job;
                this.root = root;
                this.pendingForTest = pendingForTest;
                handles.Add(root);
                if (TryIdentity(root, out var identity))
                    identities.Add(identity);
                else
                    Incomplete("root_identity_" + Win32Detail());
            }

            internal void Assigned()
            {
                assigned = true;
                Capture();
            }

            internal void Capture()
            {
                if (!assigned || joined)
                    return;
                if (captureComplete)
                {
                    var current = new List<SafeKernelHandle>();
                    bool captured;
                    string detail;
                    try
                    {
                        captured = CaptureJobProcesses(job, current, out detail);
                    }
                    catch (Exception error)
                    {
                        captured = false;
                        detail = "process_inventory_" + error.GetType().Name;
                    }

                    // Retain even the partial census before latching its error.
                    foreach (SafeKernelHandle member in current)
                    {
                        if (!TryIdentity(member, out var identity))
                        {
                            handles.Add(member);
                            Incomplete("member_identity_" + Win32Detail());
                        }
                        else if (identities.Add(identity))
                            handles.Add(member);
                        else
                            member.Dispose();
                    }

                    if (!captured)
                        Incomplete(detail);
                }

                ReadCounts();
                if (active == 0 && total != handles.Count)
                    Incomplete("uncaptured_job_process");
            }

            internal void Join()
            {
                if (joined)
                    return;
                if (!terminationRequested)
                {
                    Capture();
                    if (assigned && total != handles.Count)
                        Incomplete("incomplete_termination_census");
                    terminationRequested = true;
                    bool requested = assigned ? active == 0 || TerminateJobObject(job, TerminationExitCode) : WaitForSingleObject(root, 0) == WaitObject0 || TerminateProcess(root, TerminationExitCode);
                    if (!requested)
                        failure = "terminate_" + Win32Detail();
                }

                Stopwatch wait = Stopwatch.StartNew();
                while (true)
                {
                    if (assigned)
                        ReadCounts();
                    bool rootSignaled = Signaled(root), membersSignaled = true;
                    foreach (SafeKernelHandle member in handles)
                        if (!Signaled(member))
                            membersSignaled = false;
                    bool expired = wait.ElapsedMilliseconds >= TerminationConfirmationMilliseconds;
                    var observation = new OwnershipJoinObservation(active, total, handles.Count, captureComplete, rootSignaled, membersSignaled, assigned, expired, root.DangerousGetHandle(), handles.Count > 1 ? handles[1].DangerousGetHandle() : IntPtr.Zero);
                    bool forcedPending = false;
                    if (pendingForTest != null)
                    {
                        try
                        {
                            forcedPending = pendingForTest(observation);
                        }
                        catch (Exception error)
                        {
                            Incomplete("join_observer_" + error.GetType().Name);
                            forcedPending = true;
                        }
                    }

                    if (observation.HasCompleteHandleJoin && !forcedPending)
                    {
                        joined = true;
                        return;
                    }

                    if (expired)
                    {
                        Uncertain = true;
                        if (failure.Length == 0)
                            failure = "original_handle_join_pending";
                    }

                    // No cancellation token or detached task can release this
                    // owner. Only original handles plus the complete job census
                    // end this same pending call; kill attempts remain bounded.
                    Thread.Sleep(expired ? 250 : 10);
                }
            }

            internal void MarkUncertain(string detail)
            {
                Uncertain = true;
                if (failure.Length == 0)
                    failure = detail;
            }

            private void ReadCounts()
            {
                if (!QueryInformationJobObject(job, JobObjectBasicAccountingInformationClass, out JobObjectBasicAccountingInformation information, (uint)Marshal.SizeOf(typeof(JobObjectBasicAccountingInformation)), IntPtr.Zero))
                {
                    active = UInt32.MaxValue;
                    Incomplete("join_job_query_" + Win32Detail());
                    return;
                }

                if (information.TotalProcesses < total)
                    Incomplete("job_population_regressed");
                total = Math.Max(total, information.TotalProcesses);
                active = information.ActiveProcesses;
            }

            private bool Signaled(SafeKernelHandle handle)
            {
                uint result = WaitForSingleObject(handle, 0);
                if (result == WaitFailed)
                    Incomplete("original_handle_wait_" + Win32Detail());
                return result == WaitObject0;
            }

            private void Incomplete(string detail)
            {
                captureComplete = false;
                if (failure.Length == 0)
                    failure = detail;
            }

            private static bool TryIdentity(SafeKernelHandle handle, out (uint Pid, ulong Created) identity)
            {
                uint pid = GetProcessId(handle);
                bool valid = GetProcessTimes(handle, out ulong created, out _, out _, out _) && pid != 0;
                identity = (pid, created);
                return valid;
            }

            public void Dispose()
            {
                if (!joined)
                    throw new InvalidOperationException("original_process_join_pending");
                foreach (SafeKernelHandle handle in handles)
                    if (!ReferenceEquals(handle, root))
                        handle.Dispose();
            }
        }
    }
}
