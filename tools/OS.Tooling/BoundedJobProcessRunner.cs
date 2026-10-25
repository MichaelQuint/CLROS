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
    public sealed class BoundedJobProcessResult
    {
        public string Status { get; private set; }
        public int ExitCode { get; private set; }
        public string Output { get; private set; }
        public string StandardOutput { get; private set; }
        public string StandardError { get; private set; }
        public int OutputCharacters { get; private set; }
        public bool TerminationConfirmed { get; private set; }
        public string Detail { get; private set; }

        public BoundedJobProcessResult(
            string status,
            int exitCode,
            string output,
            int outputCharacters,
            bool terminationConfirmed,
            string detail,
            string standardOutput = null,
            string standardError = null)
        {
            Status = status;
            ExitCode = exitCode;
            Output = output ?? String.Empty;
            StandardOutput = standardOutput ?? Output;
            StandardError = standardError ?? String.Empty;
            OutputCharacters = outputCharacters;
            TerminationConfirmed = terminationConfirmed;
            Detail = detail ?? String.Empty;
        }
    }

    public static class BoundedJobProcessRunner
    {
        /// <summary>Copied host join facts for a per-request test observer.
        /// These values grant no process or file authority. A control may only
        /// delay an otherwise proven join, never replace any native receipt.</summary>
        public readonly record struct OwnershipJoinObservation(uint ActiveProcesses, uint TotalProcesses,
            int CapturedProcesses, bool CaptureComplete, bool RootSignaled, bool MembersSignaled,
            bool Assigned, bool InitialWaitExpired, nint RootHandle, nint FirstDescendantHandle)
        {
            public bool HasCompleteHandleJoin => CaptureComplete && RootSignaled && MembersSignaled &&
                ActiveProcesses == 0 && (!Assigned || TotalProcesses == CapturedProcesses);
        }

        private const int MinimumTimeoutSeconds = 5;
        private const int MaximumTimeoutSeconds = 3600;
        private const int MinimumOutputCharacters = 4096;
        private const int MaximumOutputCharacters = 67108864;
        private const uint CreateSuspended = 0x00000004;
        private const uint CreateNoWindow = 0x08000000;
        private const uint CreateUnicodeEnvironment = 0x00000400;
        private const uint ExtendedStartupInfoPresent = 0x00080000;
        private const nuint ProcThreadAttributeHandleList = 0x00020002;
        private const uint StartfUseStdHandles = 0x00000100;
        private const uint HandleFlagInherit = 0x00000001;
        private const uint JobObjectLimitKillOnJobClose = 0x00002000;
        private const int JobObjectBasicAccountingInformationClass = 1;
        private const int JobObjectBasicProcessIdListClass = 3;
        private const int JobObjectExtendedLimitInformationClass = 9;
        private const uint WaitObject0 = 0x00000000;
        private const uint WaitFailed = 0xffffffff;
        private const uint InfiniteThreadResumeFailure = 0xffffffff;
        private const uint GenericRead = 0x80000000;
        private const uint FileShareRead = 0x00000001;
        private const uint FileShareWrite = 0x00000002;
        private const uint OpenExisting = 3;
        private const uint TerminationExitCode = 0xc1a00001;
        private const int TerminationConfirmationMilliseconds = 5000;
        private const int ReaderCompletionMilliseconds = 5000;

        [StructLayout(LayoutKind.Sequential)]
        private struct SecurityAttributes
        {
            public int Length;
            public IntPtr SecurityDescriptor;

            [MarshalAs(UnmanagedType.Bool)]
            public bool InheritHandle;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct StartupInfo
        {
            public int Size;
            public string Reserved;
            public string Desktop;
            public string Title;
            public uint X;
            public uint Y;
            public uint XSize;
            public uint YSize;
            public uint XCountChars;
            public uint YCountChars;
            public uint FillAttribute;
            public uint Flags;
            public short ShowWindow;
            public short ReservedByteCount;
            public IntPtr ReservedBytes;
            public IntPtr StandardInput;
            public IntPtr StandardOutput;
            public IntPtr StandardError;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ProcessInformation
        {
            public IntPtr Process;
            public IntPtr Thread;
            public uint ProcessId;
            public uint ThreadId;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct StartupInfoEx
        {
            public StartupInfo StartupInfo;
            public IntPtr AttributeList;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IoCounters
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JobObjectBasicLimitInformation
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JobObjectExtendedLimitInformation
        {
            public JobObjectBasicLimitInformation BasicLimitInformation;
            public IoCounters IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JobObjectBasicAccountingInformation
        {
            public long TotalUserTime;
            public long TotalKernelTime;
            public long ThisPeriodTotalUserTime;
            public long ThisPeriodTotalKernelTime;
            public uint TotalPageFaultCount;
            public uint TotalProcesses;
            public uint ActiveProcesses;
            public uint TotalTerminatedProcesses;
        }

        private sealed class SafeKernelHandle :
            SafeHandleZeroOrMinusOneIsInvalid
        {
            private SafeKernelHandle()
                : base(true)
            {
            }

            internal SafeKernelHandle(IntPtr value)
                : base(true)
            {
                SetHandle(value);
            }

            protected override bool ReleaseHandle()
            {
                return CloseHandle(handle);
            }
        }

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

            internal OwnedProcessJoin(SafeKernelHandle job, SafeKernelHandle root,
                Func<OwnershipJoinObservation, bool> pendingForTest)
            {
                this.job = job; this.root = root; this.pendingForTest = pendingForTest;
                handles.Add(root);
                if (TryIdentity(root, out var identity)) identities.Add(identity);
                else Incomplete("root_identity_" + Win32Detail());
            }

            internal void Assigned() { assigned = true; Capture(); }

            internal void Capture()
            {
                if (!assigned || joined) return;
                if (captureComplete)
                {
                    var current = new List<SafeKernelHandle>();
                    bool captured;
                    string detail;
                    try { captured = CaptureJobProcesses(job, current, out detail); }
                    catch (Exception error) { captured = false; detail = "process_inventory_" + error.GetType().Name; }
                    // Retain even the partial census before latching its error.
                    foreach (SafeKernelHandle member in current)
                    {
                        if (!TryIdentity(member, out var identity))
                        { handles.Add(member); Incomplete("member_identity_" + Win32Detail()); }
                        else if (identities.Add(identity)) handles.Add(member);
                        else member.Dispose();
                    }
                    if (!captured) Incomplete(detail);
                }
                ReadCounts();
                if (active == 0 && total != handles.Count) Incomplete("uncaptured_job_process");
            }

            internal void Join()
            {
                if (joined) return;
                if (!terminationRequested)
                {
                    Capture();
                    if (assigned && total != handles.Count) Incomplete("incomplete_termination_census");
                    terminationRequested = true;
                    bool requested = assigned ? active == 0 || TerminateJobObject(job, TerminationExitCode) :
                        WaitForSingleObject(root, 0) == WaitObject0 || TerminateProcess(root, TerminationExitCode);
                    if (!requested) failure = "terminate_" + Win32Detail();
                }
                Stopwatch wait = Stopwatch.StartNew();
                while (true)
                {
                    if (assigned) ReadCounts();
                    bool rootSignaled = Signaled(root), membersSignaled = true;
                    foreach (SafeKernelHandle member in handles)
                        if (!Signaled(member)) membersSignaled = false;
                    bool expired = wait.ElapsedMilliseconds >= TerminationConfirmationMilliseconds;
                    var observation = new OwnershipJoinObservation(active, total, handles.Count, captureComplete,
                        rootSignaled, membersSignaled, assigned, expired, root.DangerousGetHandle(),
                        handles.Count > 1 ? handles[1].DangerousGetHandle() : IntPtr.Zero);
                    bool forcedPending = false;
                    if (pendingForTest != null)
                    {
                        try { forcedPending = pendingForTest(observation); }
                        catch (Exception error) { Incomplete("join_observer_" + error.GetType().Name); forcedPending = true; }
                    }
                    if (observation.HasCompleteHandleJoin && !forcedPending) { joined = true; return; }
                    if (expired)
                    {
                        Uncertain = true;
                        if (failure.Length == 0) failure = "original_handle_join_pending";
                    }
                    // No cancellation token or detached task can release this
                    // owner. Only original handles plus the complete job census
                    // end this same pending call; kill attempts remain bounded.
                    Thread.Sleep(expired ? 250 : 10);
                }
            }

            internal void MarkUncertain(string detail)
            { Uncertain = true; if (failure.Length == 0) failure = detail; }

            private void ReadCounts()
            {
                if (!QueryInformationJobObject(job, JobObjectBasicAccountingInformationClass,
                    out JobObjectBasicAccountingInformation information,
                    (uint)Marshal.SizeOf(typeof(JobObjectBasicAccountingInformation)), IntPtr.Zero))
                { active = UInt32.MaxValue; Incomplete("join_job_query_" + Win32Detail()); return; }
                if (information.TotalProcesses < total) Incomplete("job_population_regressed");
                total = Math.Max(total, information.TotalProcesses); active = information.ActiveProcesses;
            }

            private bool Signaled(SafeKernelHandle handle)
            {
                uint result = WaitForSingleObject(handle, 0);
                if (result == WaitFailed) Incomplete("original_handle_wait_" + Win32Detail());
                return result == WaitObject0;
            }

            private void Incomplete(string detail)
            { captureComplete = false; if (failure.Length == 0) failure = detail; }

            private static bool TryIdentity(SafeKernelHandle handle, out (uint Pid, ulong Created) identity)
            {
                uint pid = GetProcessId(handle);
                bool valid = GetProcessTimes(handle, out ulong created, out _, out _, out _) && pid != 0;
                identity = (pid, created); return valid;
            }

            public void Dispose()
            {
                if (!joined) throw new InvalidOperationException("original_process_join_pending");
                foreach (SafeKernelHandle handle in handles) if (!ReferenceEquals(handle, root)) handle.Dispose();
            }
        }

        private sealed class BoundedCapture
        {
            private readonly object sync = new object();
            private readonly StringBuilder standardOutput =
                new StringBuilder();
            private readonly StringBuilder standardError =
                new StringBuilder();
            private readonly int limit;
            private int characters;
            private bool overflow;
            private string failureType = String.Empty;
            private readonly Action<string, bool> outputReceived;

            internal BoundedCapture(int limit, Action<string, bool> outputReceived = null)
            {
                this.limit = limit;
                this.outputReceived = outputReceived;
            }

            internal bool Append(bool isStandardError, char[] buffer, int count)
            {
                string notification = null;
                bool complete;
                lock (sync)
                {
                    if (overflow || failureType.Length != 0)
                    {
                        return false;
                    }

                    int remaining = limit - characters;
                    int accepted = Math.Min(remaining, count);
                    if (accepted > 0)
                    {
                        StringBuilder target = isStandardError
                            ? standardError
                            : standardOutput;
                        target.Append(buffer, 0, accepted);
                        characters += accepted;
                        if (outputReceived != null) notification = new string(buffer, 0, accepted);
                    }
                    if (accepted != count)
                    {
                        overflow = true;
                    }
                    complete = !overflow;
                }
                // A caller's callback must not hold the capture lock needed by
                // deadline/cancellation polling and termination diagnostics.
                if (notification != null) outputReceived(notification, isStandardError);
                return complete;
            }

            internal void Fail(Exception exception)
            {
                lock (sync)
                {
                    if (failureType.Length == 0)
                    {
                        failureType = exception.GetType().Name;
                    }
                }
            }

            internal bool Overflow
            {
                get
                {
                    lock (sync)
                    {
                        return overflow;
                    }
                }
            }

            internal string FailureType
            {
                get
                {
                    lock (sync)
                    {
                        return failureType;
                    }
                }
            }

            internal int CharacterCount
            {
                get
                {
                    lock (sync)
                    {
                        return characters;
                    }
                }
            }

            internal string GetOutput()
            {
                lock (sync)
                {
                    return standardOutput.ToString() +
                        standardError.ToString();
                }
            }
            internal string GetStandardOutput() { lock (sync) return standardOutput.ToString(); }
            internal string GetStandardError() { lock (sync) return standardError.ToString(); }
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateJobObjectW(
            IntPtr jobAttributes,
            string name);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetInformationJobObject(
            SafeKernelHandle job,
            int informationClass,
            ref JobObjectExtendedLimitInformation information,
            uint informationLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool QueryInformationJobObject(
            SafeKernelHandle job,
            int informationClass,
            out JobObjectBasicAccountingInformation information,
            uint informationLength,
            IntPtr returnLength);

        [DllImport("kernel32.dll", EntryPoint = "QueryInformationJobObject", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool QueryJobProcessIds(SafeKernelHandle job, int informationClass,
            IntPtr information, uint informationLength, out uint returnLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern SafeKernelHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsProcessInJob(SafeKernelHandle process, SafeKernelHandle job,
            [MarshalAs(UnmanagedType.Bool)] out bool inJob);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AssignProcessToJobObject(
            SafeKernelHandle job,
            SafeKernelHandle process);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool TerminateJobObject(
            SafeKernelHandle job,
            uint exitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CreatePipe(
            out SafeFileHandle readPipe,
            out SafeFileHandle writePipe,
            ref SecurityAttributes pipeAttributes,
            uint size);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetHandleInformation(
            SafeFileHandle handle,
            uint mask,
            uint flags);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern SafeFileHandle CreateFileW(
            string fileName,
            uint desiredAccess,
            uint shareMode,
            ref SecurityAttributes securityAttributes,
            uint creationDisposition,
            uint flagsAndAttributes,
            IntPtr templateFile);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CreateProcessW(
            string applicationName,
            StringBuilder commandLine,
            IntPtr processAttributes,
            IntPtr threadAttributes,
            [MarshalAs(UnmanagedType.Bool)] bool inheritHandles,
            uint creationFlags,
            IntPtr environment,
            string currentDirectory,
            ref StartupInfoEx startupInfo,
            out ProcessInformation processInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool InitializeProcThreadAttributeList(IntPtr attributeList, int count, uint flags, ref nuint size);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UpdateProcThreadAttribute(IntPtr attributeList, uint flags, nuint attribute,
            IntPtr value, nuint size, IntPtr previousValue, IntPtr returnSize);

        [DllImport("kernel32.dll")]
        private static extern void DeleteProcThreadAttributeList(IntPtr attributeList);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint ResumeThread(SafeKernelHandle thread);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint WaitForSingleObject(
            SafeKernelHandle handle,
            uint milliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetExitCodeProcess(
            SafeKernelHandle process,
            out uint exitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint GetProcessId(SafeKernelHandle process);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetProcessTimes(SafeKernelHandle process,
            out ulong creationTime, out ulong exitTime, out ulong kernelTime, out ulong userTime);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool TerminateProcess(
            SafeKernelHandle process,
            uint exitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr handle);

        public static BoundedJobProcessResult Run(
            string executablePath,
            string[] arguments,
            string workingDirectory,
            int timeoutSeconds,
            int outputCharacterLimit,
            IReadOnlyDictionary<string, string> environmentOverrides = null,
            bool terminateDescendantsAfterRootExit = false)
        {
            if (timeoutSeconds < MinimumTimeoutSeconds ||
                timeoutSeconds > MaximumTimeoutSeconds)
            {
                return Result(
                    "InvalidTimeout",
                    -1,
                    null,
                    true,
                    "minimum=5 maximum=3600");
            }
            if (outputCharacterLimit < MinimumOutputCharacters ||
                outputCharacterLimit > MaximumOutputCharacters)
            {
                return Result(
                    "InvalidOutputLimit",
                    -1,
                    null,
                    true,
                    "minimum=4096 maximum=67108864");
            }
            if (String.IsNullOrWhiteSpace(executablePath) ||
                String.IsNullOrWhiteSpace(workingDirectory) ||
                arguments == null)
            {
                return Result(
                    "StartFailed",
                    -1,
                    null,
                    true,
                    "invalid_process_contract");
            }

            return RunCore(executablePath, arguments, workingDirectory, TimeSpan.FromSeconds(timeoutSeconds), outputCharacterLimit,
                environmentOverrides, terminateDescendantsAfterRootExit, CancellationToken.None, null);
        }

        internal static BoundedJobProcessResult RunManaged(string executablePath, ProcessRequest request, CancellationToken cancellation)
        {
            return RunCore(executablePath, request.Arguments.ToArray(), request.WorkingDirectory, request.Timeout, request.OutputCharacterLimit,
                request.EnvironmentOverrides, true, cancellation, request.OutputReceived,
                request.RetainOwnershipUntilJoined, request.JoinPendingForTest, request.AssignmentFailureForTest);
        }

        private static BoundedJobProcessResult RunCore(string executablePath, string[] arguments, string workingDirectory,
            TimeSpan timeout, int outputCharacterLimit, IReadOnlyDictionary<string, string> environmentOverrides,
            bool terminateDescendantsAfterRootExit, CancellationToken cancellation, Action<string, bool> outputReceived,
            bool retainOwnershipUntilJoined = false, Func<OwnershipJoinObservation, bool> pendingForTest = null,
            bool assignmentFailureForTest = false)
        {
            if (cancellation.IsCancellationRequested) return BoundedJobProcessRunner.Result("Cancelled", -1, null, true, "cancellation_requested");

            SafeKernelHandle job = null;
            SafeKernelHandle process = null;
            SafeKernelHandle thread = null;
            SafeFileHandle standardOutputRead = null;
            SafeFileHandle standardOutputWrite = null;
            SafeFileHandle standardErrorRead = null;
            SafeFileHandle standardErrorWrite = null;
            SafeFileHandle standardInput = null;
            FileStream standardOutputStream = null;
            FileStream standardErrorStream = null;
            Task[] readerTasks = null;
            OwnedProcessJoin ownedJoin = null;
            BoundedCapture capture = new BoundedCapture(outputCharacterLimit, outputReceived);
            bool processCreated = false;
            bool processAssigned = false;
            bool retainedRootExitObserved = false;
            IntPtr environmentBlock = IntPtr.Zero;
            IntPtr attributeList = IntPtr.Zero;
            IntPtr inheritedHandles = IntPtr.Zero;
            bool attributeListInitialized = false;

            // All existing exits and cleanup calls use the same opt-in owner.
            // Default callers retain the reviewed bounded-return behavior.
            bool TerminateAssignedTree(SafeKernelHandle currentJob, out string detail)
            {
                if (ownedJoin == null) return BoundedJobProcessRunner.TerminateAssignedTree(currentJob, out detail);
                ownedJoin.Join(); detail = ownedJoin.Detail; return true;
            }
            bool TerminateSingleProcess(SafeKernelHandle currentProcess, out string detail)
            {
                if (ownedJoin == null) return BoundedJobProcessRunner.TerminateSingleProcess(currentProcess, out detail);
                ownedJoin.Join(); detail = ownedJoin.Detail; return true;
            }
            BoundedJobProcessResult Result(string status, int exitCode, BoundedCapture output, bool confirmed, string detail)
            {
                if (ownedJoin != null)
                {
                    ownedJoin.Join();
                    if (readerTasks != null && !WaitForReaders(readerTasks, capture))
                    {
                        ownedJoin.MarkUncertain("reader_completion_timeout");
                        // Keep streams and the same call while readers still own
                        // them. Later cancellation does not dispose active I/O.
                        while (readerTasks.Any(task => !task.IsCompleted)) Thread.Sleep(250);
                        WaitForReaders(readerTasks, capture);
                    }
                    if (ownedJoin.Uncertain)
                    {
                        if (status == "Success") { status = "TerminationUnconfirmed"; exitCode = -1; }
                        detail += (detail.Length == 0 ? "" : " ") + "retained_cleanup=" + ownedJoin.Detail;
                    }
                    confirmed = true;
                }
                return BoundedJobProcessRunner.Result(status, exitCode, output, confirmed, detail);
            }

            try
            {
                if (environmentOverrides != null)
                {
                    var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var pair in environmentOverrides)
                        if (String.IsNullOrEmpty(pair.Key) || pair.Key.IndexOfAny(['=', '\0']) >= 0 ||
                            pair.Value?.Contains('\0') == true || !names.Add(pair.Key))
                            return Result("InvalidEnvironment", -1, capture, true, "invalid_environment_contract");
                    var environment = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
                        environment[(string)entry.Key] = (string)entry.Value;
                    foreach (var pair in environmentOverrides)
                        if (pair.Value == null) environment.Remove(pair.Key);
                        else environment[pair.Key] = pair.Value;
                    // StringToHGlobalUni supplies the second terminating NUL.
                    environmentBlock = Marshal.StringToHGlobalUni(String.Join('\0', environment.Select(p => p.Key + "=" + p.Value)) + '\0');
                }
                IntPtr rawJob = CreateJobObjectW(IntPtr.Zero, null);
                if (rawJob == IntPtr.Zero || rawJob == new IntPtr(-1))
                {
                    return Result(
                        "JobConfigurationFailed",
                        -1,
                        capture,
                        true,
                        Win32Detail());
                }
                job = new SafeKernelHandle(rawJob);

                JobObjectExtendedLimitInformation limitInformation =
                    new JobObjectExtendedLimitInformation();
                limitInformation.BasicLimitInformation.LimitFlags =
                    JobObjectLimitKillOnJobClose;
                if (!SetInformationJobObject(
                        job,
                        JobObjectExtendedLimitInformationClass,
                        ref limitInformation,
                        (uint)Marshal.SizeOf(
                            typeof(JobObjectExtendedLimitInformation))))
                {
                    return Result(
                        "JobConfigurationFailed",
                        -1,
                        capture,
                        true,
                        Win32Detail());
                }

                SecurityAttributes inheritable = new SecurityAttributes();
                inheritable.Length = Marshal.SizeOf(
                    typeof(SecurityAttributes));
                inheritable.InheritHandle = true;
                if (!CreatePipe(
                        out standardOutputRead,
                        out standardOutputWrite,
                        ref inheritable,
                        0) ||
                    !SetHandleInformation(
                        standardOutputRead,
                        HandleFlagInherit,
                        0) ||
                    !CreatePipe(
                        out standardErrorRead,
                        out standardErrorWrite,
                        ref inheritable,
                        0) ||
                    !SetHandleInformation(
                        standardErrorRead,
                        HandleFlagInherit,
                        0))
                {
                    return Result(
                        "StartFailed",
                        -1,
                        capture,
                        true,
                        "pipe_" + Win32Detail());
                }

                standardInput = CreateFileW(
                    "NUL",
                    GenericRead,
                    FileShareRead | FileShareWrite,
                    ref inheritable,
                    OpenExisting,
                    0,
                    IntPtr.Zero);
                if (standardInput == null || standardInput.IsInvalid)
                {
                    return Result(
                        "StartFailed",
                        -1,
                        capture,
                        true,
                        "stdin_" + Win32Detail());
                }

                StringBuilder commandLine;
                try
                {
                    commandLine = BuildCommandLine(executablePath, arguments);
                }
                catch (Exception exception)
                {
                    return Result(
                        "StartFailed",
                        -1,
                        capture,
                        true,
                        "command_line_" + exception.GetType().Name);
                }
                if (commandLine.Length > 32767)
                {
                    return Result(
                        "StartFailed",
                        -1,
                        capture,
                        true,
                        "command_line_too_long");
                }

                // Explicitly inherit only this child's streams. Parallel
                // launches must not retain another process's pipe writers.
                nuint attributeListBytes = 0;
                InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref attributeListBytes);
                if (attributeListBytes == 0)
                    return Result("StartFailed", -1, capture, true, "handle_list_size_" + Win32Detail());
                attributeList = Marshal.AllocHGlobal(checked((nint)attributeListBytes));
                if (!InitializeProcThreadAttributeList(attributeList, 1, 0, ref attributeListBytes))
                    return Result("StartFailed", -1, capture, true, "handle_list_init_" + Win32Detail());
                attributeListInitialized = true;
                inheritedHandles = Marshal.AllocHGlobal(3 * IntPtr.Size);
                Marshal.WriteIntPtr(inheritedHandles, 0, standardInput.DangerousGetHandle());
                Marshal.WriteIntPtr(inheritedHandles, IntPtr.Size, standardOutputWrite.DangerousGetHandle());
                Marshal.WriteIntPtr(inheritedHandles, 2 * IntPtr.Size, standardErrorWrite.DangerousGetHandle());
                if (!UpdateProcThreadAttribute(attributeList, 0, ProcThreadAttributeHandleList,
                        inheritedHandles, (nuint)(3 * IntPtr.Size), IntPtr.Zero, IntPtr.Zero))
                    return Result("StartFailed", -1, capture, true, "handle_list_update_" + Win32Detail());
                StartupInfoEx startupInfo = new StartupInfoEx();
                startupInfo.StartupInfo.Size = Marshal.SizeOf(typeof(StartupInfoEx));
                startupInfo.StartupInfo.Flags = StartfUseStdHandles;
                startupInfo.AttributeList = attributeList;
                startupInfo.StartupInfo.StandardInput =
                    standardInput.DangerousGetHandle();
                startupInfo.StartupInfo.StandardOutput =
                    standardOutputWrite.DangerousGetHandle();
                startupInfo.StartupInfo.StandardError =
                    standardErrorWrite.DangerousGetHandle();
                ProcessInformation processInformation;
                if (!CreateProcessW(
                        executablePath,
                        commandLine,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        true,
                        CreateSuspended | CreateNoWindow | ExtendedStartupInfoPresent | (environmentBlock != IntPtr.Zero ? CreateUnicodeEnvironment : 0),
                        environmentBlock,
                        workingDirectory,
                        ref startupInfo,
                        out processInformation))
                {
                    return Result(
                        "StartFailed",
                        -1,
                        capture,
                        true,
                        Win32Detail());
                }

                processCreated = true;
                process = new SafeKernelHandle(processInformation.Process);
                thread = new SafeKernelHandle(processInformation.Thread);
                if (retainOwnershipUntilJoined) ownedJoin = new OwnedProcessJoin(job, process, pendingForTest);
                standardOutputWrite.Dispose();
                standardOutputWrite = null;
                standardErrorWrite.Dispose();
                standardErrorWrite = null;
                standardInput.Dispose();
                standardInput = null;

                if ((retainOwnershipUntilJoined && assignmentFailureForTest) || !AssignProcessToJobObject(job, process))
                {
                    string terminationDetail;
                    bool terminated = TerminateSingleProcess(
                        process,
                        out terminationDetail);
                    if (!terminated)
                    {
                        return Result(
                            "TerminationUnconfirmed",
                            -1,
                            capture,
                            false,
                            "trigger=JobAssignmentFailed " +
                                terminationDetail);
                    }
                    return Result(
                        "JobAssignmentFailed",
                        -1,
                        capture,
                        true,
                        retainOwnershipUntilJoined && assignmentFailureForTest ? "controlled_job_assignment_failure" : Win32Detail());
                }
                processAssigned = true;
                ownedJoin?.Assigned();

                try
                {
                    standardOutputStream = new FileStream(
                        standardOutputRead,
                        FileAccess.Read,
                        4096,
                        false);
                    standardErrorStream = new FileStream(
                        standardErrorRead,
                        FileAccess.Read,
                        4096,
                        false);
                    readerTasks = new Task[]
                    {
                        StartReader(
                            standardOutputStream,
                            false,
                            capture),
                        StartReader(
                            standardErrorStream,
                            true,
                            capture)
                    };
                }
                catch (Exception exception)
                {
                    string terminationDetail;
                    bool terminated = TerminateAssignedTree(
                        job,
                        out terminationDetail);
                    if (!terminated)
                    {
                        return Result(
                            "TerminationUnconfirmed",
                            -1,
                            capture,
                            false,
                            "trigger=CaptureFailed " +
                                terminationDetail);
                    }
                    capture.Fail(exception);
                    return Result(
                        "CaptureFailed",
                        -1,
                        capture,
                        true,
                        exception.GetType().Name);
                }

                if (ResumeThread(thread) ==
                    InfiniteThreadResumeFailure)
                {
                    string terminationDetail;
                    bool terminated = TerminateAssignedTree(
                        job,
                        out terminationDetail);
                    if (!terminated)
                    {
                        return Result(
                            "TerminationUnconfirmed",
                            -1,
                            capture,
                            false,
                            "trigger=ResumeFailed " +
                                terminationDetail);
                    }
                    WaitForReaders(readerTasks, capture);
                    return Result(
                        "ResumeFailed",
                        -1,
                        capture,
                        true,
                        Win32Detail());
                }

                Stopwatch stopwatch = Stopwatch.StartNew();
                string triggerStatus = String.Empty;
                string triggerDetail = String.Empty;
                while (true)
                {
                    ownedJoin?.Capture();
                    if (cancellation.IsCancellationRequested)
                    {
                        triggerStatus = "Cancelled";
                        triggerDetail = "cancellation_requested";
                        break;
                    }
                    if (capture.FailureType.Length != 0)
                    {
                        triggerStatus = "CaptureFailed";
                        triggerDetail = capture.FailureType;
                        break;
                    }
                    if (capture.Overflow)
                    {
                        triggerStatus = "OutputOverflow";
                        triggerDetail =
                            "limit=" + outputCharacterLimit.ToString(
                                System.Globalization.CultureInfo.InvariantCulture);
                        break;
                    }

                    uint waitResult = WaitForSingleObject(process, 0);
                    if (waitResult == WaitFailed)
                    {
                        triggerStatus = "JobQueryFailed";
                        triggerDetail = "process_wait_" + Win32Detail();
                        break;
                    }
                    uint activeProcesses;
                    if (!TryGetActiveProcessCount(
                            job,
                            out activeProcesses))
                    {
                        triggerStatus = "JobQueryFailed";
                        triggerDetail = Win32Detail();
                        break;
                    }
                    if (waitResult == WaitObject0 &&
                        (activeProcesses == 0 || terminateDescendantsAfterRootExit))
                    {
                        retainedRootExitObserved = retainOwnershipUntilJoined;
                        // EFI publishing historically completes at root exit,
                        // then reaps its entire assigned job and drains both
                        // streams. Other callers require natural tree exit.
                        if ((activeProcesses != 0 || retainOwnershipUntilJoined) && !TerminateAssignedTree(job, out string completionDetail))
                            return Result("TerminationUnconfirmed", -1, capture, false,
                                "trigger=RootExited " + completionDetail);
                        break;
                    }
                    if (stopwatch.Elapsed >= timeout)
                    {
                        triggerStatus = "TimedOut";
                        triggerDetail =
                            "timeoutSeconds=" + timeout.TotalSeconds.ToString(
                                System.Globalization.CultureInfo.InvariantCulture);
                        break;
                    }

                    Thread.Sleep(10);
                }

                if (triggerStatus.Length != 0)
                {
                    string terminationDetail;
                    bool terminated = TerminateAssignedTree(
                        job,
                        out terminationDetail);
                    if (!terminated)
                    {
                        return Result(
                            "TerminationUnconfirmed",
                            -1,
                            capture,
                            false,
                            "trigger=" + triggerStatus + " " +
                                terminationDetail);
                    }
                    if (!WaitForReaders(readerTasks, capture))
                    {
                        if (!retainOwnershipUntilJoined)
                        {
                            triggerStatus = "CaptureFailed";
                            triggerDetail = "reader_completion_timeout";
                        }
                        else
                        {
                            ownedJoin.MarkUncertain("reader_completion_timeout");
                            triggerDetail += " reader_completion_timeout";
                        }
                    }
                    return Result(
                        triggerStatus,
                        -1,
                        capture,
                        true,
                        triggerDetail);
                }

                if (!WaitForReaders(readerTasks, capture))
                {
                    return Result(
                        "CaptureFailed",
                        -1,
                        capture,
                        true,
                        "reader_completion_timeout");
                }
                if (capture.FailureType.Length != 0)
                {
                    return Result(
                        "CaptureFailed",
                        -1,
                        capture,
                        true,
                        capture.FailureType);
                }
                if (capture.Overflow)
                {
                    return Result(
                        "OutputOverflow",
                        -1,
                        capture,
                        true,
                        "limit=" + outputCharacterLimit.ToString(
                            System.Globalization.CultureInfo.InvariantCulture));
                }
                if (cancellation.IsCancellationRequested && !retainedRootExitObserved)
                    return Result("Cancelled", -1, capture, true, "cancellation_requested");

                uint unsignedExitCode;
                if (!GetExitCodeProcess(process, out unsignedExitCode))
                {
                    return Result(
                        "JobQueryFailed",
                        -1,
                        capture,
                        true,
                        "exit_code_" + Win32Detail());
                }
                int exitCode = unchecked((int)unsignedExitCode);
                return Result(
                    exitCode == 0 ? "Success" : "NonZeroExit",
                    exitCode,
                    capture,
                    true,
                    String.Empty);
            }
            catch (Exception exception)
            {
                if (processCreated)
                {
                    string ignored;
                    bool terminated = processAssigned
                        ? TerminateAssignedTree(job, out ignored)
                        : TerminateSingleProcess(process, out ignored);
                    if (!terminated)
                    {
                        return Result(
                            "TerminationUnconfirmed",
                            -1,
                            capture,
                            false,
                            "trigger=CaptureFailed " + ignored);
                    }
                }
                return Result(
                    "CaptureFailed",
                    -1,
                    capture,
                    true,
                    exception.GetType().Name);
            }
            finally
            {
                // This precedes every handle/stream unwind, including catches
                // and the suspended, not-yet-assigned process failure path.
                ownedJoin?.Join();
                if (environmentBlock != IntPtr.Zero) Marshal.FreeHGlobal(environmentBlock);
                if (attributeListInitialized) DeleteProcThreadAttributeList(attributeList);
                if (attributeList != IntPtr.Zero) Marshal.FreeHGlobal(attributeList);
                if (inheritedHandles != IntPtr.Zero) Marshal.FreeHGlobal(inheritedHandles);
                if (readerTasks != null)
                {
                    WaitForReaders(readerTasks, capture);
                }
                if (standardOutputStream != null)
                {
                    standardOutputStream.Dispose();
                }
                if (standardErrorStream != null)
                {
                    standardErrorStream.Dispose();
                }
                if (standardOutputRead != null)
                {
                    standardOutputRead.Dispose();
                }
                if (standardOutputWrite != null)
                {
                    standardOutputWrite.Dispose();
                }
                if (standardErrorRead != null)
                {
                    standardErrorRead.Dispose();
                }
                if (standardErrorWrite != null)
                {
                    standardErrorWrite.Dispose();
                }
                if (standardInput != null)
                {
                    standardInput.Dispose();
                }
                if (thread != null)
                {
                    thread.Dispose();
                }
                if (process != null)
                {
                    ownedJoin?.Dispose();
                    process.Dispose();
                }
                if (job != null)
                {
                    job.Dispose();
                }
            }
        }

        private static Task StartReader(
            Stream stream,
            bool isStandardError,
            BoundedCapture capture)
        {
            return Task.Factory.StartNew(
                delegate
                {
                    try
                    {
                        using (StreamReader reader = new StreamReader(
                            stream,
                            new UTF8Encoding(false, true),
                            false,
                            4096,
                            true))
                        {
                            char[] buffer = new char[1024];
                            while (true)
                            {
                                int read = reader.Read(
                                    buffer,
                                    0,
                                    buffer.Length);
                                if (read == 0)
                                {
                                    break;
                                }
                                if (!capture.Append(
                                        isStandardError,
                                        buffer,
                                        read))
                                {
                                    break;
                                }
                            }
                        }
                    }
                    catch (Exception exception)
                    {
                        capture.Fail(exception);
                    }
                },
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        private static bool WaitForReaders(
            Task[] tasks,
            BoundedCapture capture)
        {
            try
            {
                return Task.WaitAll(
                    tasks,
                    ReaderCompletionMilliseconds);
            }
            catch (Exception exception)
            {
                capture.Fail(exception);
                return true;
            }
        }

        private static bool TryGetActiveProcessCount(
            SafeKernelHandle job,
            out uint activeProcesses)
        {
            JobObjectBasicAccountingInformation information;
            bool success = QueryInformationJobObject(
                job,
                JobObjectBasicAccountingInformationClass,
                out information,
                (uint)Marshal.SizeOf(
                    typeof(JobObjectBasicAccountingInformation)),
                IntPtr.Zero);
            activeProcesses = success
                ? information.ActiveProcesses
                : UInt32.MaxValue;
            return success;
        }

        private static bool TerminateAssignedTree(
            SafeKernelHandle job,
            out string detail)
        {
            // Job accounting can reach zero while process teardown still owns
            // file handles. Retain real process handles before termination and
            // require their signaled state as well as the empty-job receipt.
            var processes = new List<SafeKernelHandle>();
            bool captured = CaptureJobProcesses(job, processes, out string captureDetail);
            try
            {
            uint activeProcesses;
            if (!TryGetActiveProcessCount(job, out activeProcesses))
            {
                detail = "initial_query_" + Win32Detail();
                return false;
            }
            bool terminateRequested = activeProcesses == 0 || TerminateJobObject(job, TerminationExitCode);
            int terminationError = terminateRequested
                ? 0
                : Marshal.GetLastWin32Error();
            Stopwatch stopwatch = Stopwatch.StartNew();
            while (stopwatch.ElapsedMilliseconds <
                TerminationConfirmationMilliseconds)
            {
                if (!TryGetActiveProcessCount(
                        job,
                        out activeProcesses))
                {
                    detail = "confirmation_query_" + Win32Detail();
                    return false;
                }
                bool processesSignaled = true;
                foreach (SafeKernelHandle member in processes)
                {
                    uint wait = WaitForSingleObject(member, 0);
                    if (wait == WaitFailed)
                    {
                        detail = "process_confirmation_" + Win32Detail();
                        return false;
                    }
                    if (wait != WaitObject0) processesSignaled = false;
                }
                if (activeProcesses == 0 && processesSignaled)
                {
                    detail = captured ? String.Empty : captureDetail;
                    return captured;
                }

                Thread.Sleep(10);
            }

            detail =
                "activeProcesses=" +
                activeProcesses.ToString(
                    System.Globalization.CultureInfo.InvariantCulture) +
                " terminateWin32=" +
                terminationError.ToString(
                    System.Globalization.CultureInfo.InvariantCulture);
            return false;
            }
            finally { foreach (SafeKernelHandle member in processes) member.Dispose(); }
        }

        private static bool CaptureJobProcesses(SafeKernelHandle job, List<SafeKernelHandle> processes, out string detail)
        {
            int capacity = 32;
            while (true)
            {
                int bytes = checked(8 + capacity * IntPtr.Size);
                IntPtr buffer = Marshal.AllocHGlobal(bytes);
                try
                {
                    if (!QueryJobProcessIds(job, JobObjectBasicProcessIdListClass, buffer, (uint)bytes, out uint required))
                    {
                        int error = Marshal.GetLastWin32Error();
                        if (error == 234) { capacity = checked(Math.Max(capacity * 2, ((int)required - 8) / IntPtr.Size)); continue; }
                        detail = "process_inventory_win32=" + error;
                        return false;
                    }
                    uint count = unchecked((uint)Marshal.ReadInt32(buffer, 4));
                    if (count > capacity) { detail = "process_inventory_size"; return false; }
                    for (int index = 0; index < count; index++)
                    {
                        uint id = checked((uint)(nuint)Marshal.ReadIntPtr(buffer, 8 + index * IntPtr.Size));
                        SafeKernelHandle member = OpenProcess(0x00100000 | 0x00001000, false, id); // SYNCHRONIZE | QUERY_LIMITED_INFORMATION
                        if (member.IsInvalid)
                        {
                            int error = Marshal.GetLastWin32Error();
                            member.Dispose();
                            if (error == 87) continue; // PID already ceased to exist; its resources are gone.
                            detail = "process_open_win32=" + error;
                            return false;
                        }
                        if (!IsProcessInJob(member, job, out bool assigned))
                        {
                            string error = Win32Detail(); member.Dispose();
                            detail = "process_membership_" + error; return false;
                        }
                        // An exited PID can be reused before OpenProcess. Never
                        // wait on or otherwise act on an unrelated process.
                        if (assigned) processes.Add(member);
                        else member.Dispose();
                    }
                    detail = String.Empty;
                    return true;
                }
                finally { Marshal.FreeHGlobal(buffer); }
            }
        }

        private static bool TerminateSingleProcess(
            SafeKernelHandle process,
            out string detail)
        {
            uint waitResult = WaitForSingleObject(process, 0);
            if (waitResult == WaitObject0)
            {
                detail = String.Empty;
                return true;
            }
            bool terminateRequested =
                TerminateProcess(process, TerminationExitCode);
            int terminationError = terminateRequested
                ? 0
                : Marshal.GetLastWin32Error();
            waitResult = WaitForSingleObject(
                process,
                TerminationConfirmationMilliseconds);
            if (waitResult == WaitObject0)
            {
                detail = String.Empty;
                return true;
            }

            detail =
                "waitResult=" +
                waitResult.ToString(
                    System.Globalization.CultureInfo.InvariantCulture) +
                " terminateWin32=" +
                terminationError.ToString(
                    System.Globalization.CultureInfo.InvariantCulture);
            return false;
        }

        private static StringBuilder BuildCommandLine(
            string executablePath,
            string[] arguments)
        {
            var commandLine = new StringBuilder();
            AppendQuotedArgument(commandLine, executablePath);
            for (int index = 0; index < arguments.Length; index++)
            {
                if (arguments[index] == null)
                {
                    throw new ArgumentException(
                        "Arguments cannot contain null.",
                        "arguments");
                }
                commandLine.Append(' ');
                AppendQuotedArgument(commandLine, arguments[index]);
            }
            return commandLine;
        }

        private static void AppendQuotedArgument(
            StringBuilder commandLine,
            string value)
        {
            if (value.IndexOf('\0') >= 0)
            {
                throw new ArgumentException(
                    "Arguments cannot contain null characters.",
                    "value");
            }
            if (value.Length != 0 &&
                value.IndexOfAny(
                    new char[] { ' ', '\t', '\n', '\v', '"' }) < 0)
            {
                commandLine.Append(value);
                return;
            }

            commandLine.Append('"');
            int backslashes = 0;
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (character == '\\')
                {
                    backslashes++;
                    continue;
                }
                if (character == '"')
                {
                    commandLine.Append('\\', (backslashes * 2) + 1);
                    commandLine.Append('"');
                    backslashes = 0;
                    continue;
                }
                if (backslashes != 0)
                {
                    commandLine.Append('\\', backslashes);
                    backslashes = 0;
                }
                commandLine.Append(character);
            }
            if (backslashes != 0)
            {
                commandLine.Append('\\', backslashes * 2);
            }
            commandLine.Append('"');
        }

        private static string Win32Detail()
        {
            return "win32=" +
                Marshal.GetLastWin32Error().ToString(
                    System.Globalization.CultureInfo.InvariantCulture);
        }

        private static BoundedJobProcessResult Result(
            string status,
            int exitCode,
            BoundedCapture capture,
            bool terminationConfirmed,
            string detail)
        {
            string output = capture == null
                ? String.Empty
                : capture.GetOutput();
            int outputCharacters = capture == null
                ? 0
                : capture.CharacterCount;
            return new BoundedJobProcessResult(
                status,
                exitCode,
                output,
                outputCharacters,
                terminationConfirmed,
                detail,
                capture?.GetStandardOutput(),
                capture?.GetStandardError());
        }
    }
}
