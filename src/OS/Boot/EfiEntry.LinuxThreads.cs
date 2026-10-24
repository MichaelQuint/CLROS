#if M16_008_PROBE
using OS.Boot;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Diagnostics;
using OS.Kernel.Memory;
using OS.Kernel.Platform;
using OS.Kernel.Threading;
using OS.Pal;
using NativeProgram = OS.Kernel.Compatibility.LinuxX64.LinuxThreadProgram;
using NativeContext = OS.Kernel.Threading.X64ThreadContext;
using NativeSwitch = OS.Kernel.Threading.ThreadContextSwitch;

namespace OS.Boot;

#pragma warning disable 649 // Caller-owned buffers are initialized through admitted native pointers.

public static unsafe partial class EfiEntry
{
    private static partial class M03FirstExitAdapter
    {
#if M16_014_PROBE
        // One serialized, supervisor-only negative lifetime. Each stage requires
        // its exact predecessor and independently checked owned restoration;
        // neither a log string nor an arbitrary false return can advance it.
        private enum LinuxQuotaDisposition : uint { None, Pending, Terminating, NativeCleaned, CpuRestored, NetworkCleaned }

        private static bool LinuxQuotaPolicy(Storage* state) => state != null &&
            state->LinuxServices.GcBudget.Succeeded && state->LinuxServices.GcBudget.TerminateOnPhysicalBackingExhaustion;
#endif

        private const uint LinuxNativeWorkers = NativeProgram.MaximumWorkers, LinuxNativeFutexes = 16;
        // Metadata lives only in the program's retained RW/NX arena. No user leaf
        // exposes this storage. One IRQ-off BSP owns the complete lifetime.
        private struct LinuxNativeControl
        {
            internal Storage* State;
            internal ulong Key, AliasSerial;
            internal uint Phase, Published, Capacity, Selected, Failed, Stopping, Reaped, Transfers, UserFaults;
#if M10_017_PROBE
            internal LinuxStaticFileObservation StaticApp;
            internal OSHostHandle StaticMain;
            internal LinuxDescriptorServices.Token StaticListener;
#endif
#if M16_014_PROBE
            internal ulong QuotaStackAcquired, QuotaStackLastReturned, QuotaStackPeakBytes, QuotaContextPeakBytes;
            internal uint QuotaStackPeakLive, QuotaStackStarted, QuotaStackEnded;
            internal fixed ulong QuotaStackGenerations[(int)LinuxNativeWorkers];
#endif
#if M10_004_PROBE
            internal uint WebFlags, WebBoundFd, WebStartMatch;
            internal ulong WebConfigBytes, WebStdoutBytes, WebStderrBytes;
            internal LinuxAppStartupObservation WebStartup;
            internal LinuxAppLogObservation WebLogs;
            internal LinuxAppLogQuotaObservation WebQuota;
#endif
#if M16_009_PROBE
            internal uint UserTimer, UserAcknowledged, UserDrain, UserPreempted;
            internal ulong TimingStart, TimingArm, TimingCapture, TimingReady;
            internal uint TimingValid;
#endif
            internal NativeProgram.Plan Plan;
            internal KernelThreadTable Threads;
            internal LinuxThreadServices Process;
            internal LinuxSignalServices.State* Signals;
            internal LinuxThreadServices.FutexCell* Cells;
            internal LinuxThreadServices.Slot* Slots;
            internal LinuxThreadServices.Worker* Workers;
            internal LinuxSyscallGateState* Gates;
            internal uint* Actions;
            internal uint* Entered;
            internal LinuxSyscallAccess User;
            internal KernelThreadSlot* KernelSlots;
            internal OSHostHandle* Ready;
            internal DeadlineQueue.Entry* Deadlines;
            internal WaitAddress.Slot* Addresses;
            internal WaitAddress.Registration* Registrations;
            internal X64LinuxUserTransition.UserImage* Initial;
            internal byte* CodeCopy;
            internal X64TimerInterrupt.Plan Handler;
            internal SchedulerTimer Timer;
            internal DeadlineQueue TimerDeadlines;
            internal DeadlineQueue.Entry TimerDeadline;
            internal ulong Epoch, OriginalIst;
            internal LinuxThreadExecutionBudget Budget;
#if M16_014_PROBE
            internal ulong ServiceProgress, ServicePeakCalls, ServicePeakIdentity, ServiceFinished;
            internal uint ServiceReady, ServiceShutdown;
#endif
            internal uint Installed, GuardAttached, ProcessAttached, TableAttached, SignalsInitialized;
            internal fixed ulong Cpu[(int)NativeSwitch.ObservationWords];
            internal fixed ulong GatewayCpu[(int)X64LinuxUserTransition.ObservationWords];
            internal fixed ulong OriginalIdtr[2], NewIdtr[2], ObservedIdtr[2];
            internal fixed byte OriginalTss[104];
        }

        private static LinuxNativeControl* LinuxNativeOwner(void* context)
        {
            Storage* state = (Storage*)context;
            fixed (Storage* retained = &s_storage) if (state != retained) return null;
            if (!LinuxSyscallAuthority(state) || !CooperativeAuthority(state)) return null;
            LinuxNativeControl* c = (LinuxNativeControl*)(state->CooperativePool + NativeProgram.MetadataPage * Page);
            if (c->State != state || c->Key != (state->CooperativePool ^ 0x4d31363030384f57UL) || c->Phase < 1 || c->Phase > 4) return null;
            ulong* cpu = stackalloc ulong[(int)NativeSwitch.ObservationWords];
            ((delegate* unmanaged<ulong*, void>)(state->Memory.Code + FirstExitCodeBundle.CooperativeObserveOffset))(cpu);
            byte* gateway = (byte*)state->LinuxSyscallData;
            if (*(ulong*)(gateway + X64LinuxUserTransition.PhaseOffset) == 1)
                return X64LinuxUserTransition.ValidateCallback(gateway, X64LinuxUserTransition.DataBytes,
                    state->LinuxDispatchRoot, cpu[1], cpu[3], cpu[4], cpu[5], cpu[6],
                    cpu[12], cpu[13], cpu[14], cpu[15], cpu[16]) ? c : null;
            // Outside the gateway, keep the original supervisor KGS authority.
            // IRQ callbacks use SS=0 on the owned IST; ordinary callbacks SS=16.
            return cpu[1] == state->LinuxDispatchRoot && (cpu[3] & 0x602) == 2 && cpu[4] == 8 &&
                (cpu[5] == 16 || cpu[5] == 0) && cpu[6] == 24 && cpu[14] == c->Plan.Owner.KernelGs &&
                cpu[15] == 0 && cpu[16] == 0 ? c : null;
        }

        // Prepare unpublished metadata/code only. The runtime supplies its real
        // image-resident callbacks; this method invents no native continuation.
        // Nothing may invoke the emitted program before Publish/Bind succeed.
        private static bool LinuxNativePrepare(Storage* state, ulong interrupt,
            out LinuxNativeControl* c)
        {
            c = null;
            if (!CooperativeAuthority(state) || state->CooperativePhase != 0 || state->LinuxSyscallPhase != 4 || state->LinuxMemoryPhase != 4 ||
                state->LinuxDescriptorPhase != 4 || state->ApicPhase != 2 ||
                state->LinuxView.Manifest.Threads == 0 || state->LinuxView.Manifest.Threads > LinuxNativeWorkers ||
                (uint)sizeof(LinuxNativeControl) > 8192 ||
                !CooperativeAuditOriginalMappings(state) || !CooperativeAuditPool(state, false)) return false;
            state->CooperativePhase = 1; // One lifetime/key per boot, including failed preparation.
            ulong pool = state->CooperativePool, cursor = pool + NativeProgram.MetadataPage * Page, end = pool + NativeProgram.SwitchGuardPage * Page;
            if (!LinuxNativeTake(ref cursor, end, (uint)sizeof(LinuxNativeControl), out byte* control)) return false;
            c = (LinuxNativeControl*)control;
            *c = default; c->State = state; c->Key = pool ^ 0x4d31363030384f57UL; c->Phase = 1; c->Capacity = state->LinuxView.Manifest.Threads;
#if M16_014_PROBE
            c->WebStartup = LinuxAppStartupObservation.ForService();
#endif
            if (!LinuxNativeTake(ref cursor, end, NativeContext.Bytes, out byte* supervisor) ||
                !LinuxNativeTake(ref cursor, end, NativeContext.Bytes * LinuxNativeWorkers, out byte* contexts) ||
                !LinuxNativeTake(ref cursor, end, NativeContext.Bytes, out byte* witness) ||
                !LinuxNativeTake(ref cursor, end, 512, out byte* helper) ||
                !LinuxNativeTake(ref cursor, end, 16, out byte* jump) ||
                !LinuxNativeTake(ref cursor, end, (uint)sizeof(NativeProgram.Binding) * LinuxNativeWorkers, out byte* bindings) ||
                !LinuxNativeTake(ref cursor, end, (uint)sizeof(LinuxThreadServices.Slot) * LinuxNativeWorkers, out byte* slots) ||
                !LinuxNativeTake(ref cursor, end, (uint)sizeof(LinuxThreadServices.Worker) * LinuxNativeWorkers, out byte* workers) ||
                !LinuxNativeTake(ref cursor, end, (uint)sizeof(LinuxSyscallGateState) * LinuxNativeWorkers, out byte* gates) ||
                !LinuxNativeTake(ref cursor, end, 4 * LinuxNativeWorkers, out byte* actions) ||
                !LinuxNativeTake(ref cursor, end, 4 * LinuxNativeWorkers, out byte* entered) ||
                !LinuxNativeTake(ref cursor, end, (uint)sizeof(KernelThreadSlot) * LinuxNativeWorkers, out byte* kernelSlots) ||
                !LinuxNativeTake(ref cursor, end, (uint)sizeof(OSHostHandle) * LinuxNativeWorkers, out byte* ready) ||
                !LinuxNativeTake(ref cursor, end, (uint)sizeof(DeadlineQueue.Entry) * LinuxNativeWorkers, out byte* deadlines) ||
                !LinuxNativeTake(ref cursor, end, (uint)sizeof(LinuxThreadServices.FutexCell) * LinuxNativeFutexes, out byte* cells) ||
                !LinuxNativeTake(ref cursor, end, (uint)sizeof(WaitAddress.Slot) * LinuxNativeFutexes, out byte* addresses) ||
                !LinuxNativeTake(ref cursor, end, (uint)sizeof(WaitAddress.Registration) * LinuxNativeWorkers, out byte* registrations) ||
                !LinuxNativeTake(ref cursor, end, (uint)sizeof(LinuxSignalServices.State), out byte* signals) ||
                !LinuxNativeTake(ref cursor, end, X64LinuxUserTransition.UserImageBytes, out byte* initial) ||
                !LinuxNativeTake(ref cursor, end, NativeProgram.Bytes, out byte* codeCopy)) return false;
            c->Slots = (LinuxThreadServices.Slot*)slots; c->Workers = (LinuxThreadServices.Worker*)workers;
            c->Gates = (LinuxSyscallGateState*)gates; c->KernelSlots = (KernelThreadSlot*)kernelSlots;
            c->Actions = (uint*)actions; c->Entered = (uint*)entered;
            c->Ready = (OSHostHandle*)ready; c->Deadlines = (DeadlineQueue.Entry*)deadlines;
            c->Cells = (LinuxThreadServices.FutexCell*)cells; c->Addresses = (WaitAddress.Slot*)addresses;
            c->Registrations = (WaitAddress.Registration*)registrations; c->Initial = (X64LinuxUserTransition.UserImage*)initial;
            c->CodeCopy = codeCopy;
            c->Signals = (LinuxSignalServices.State*)signals;
            c->User = new() { Context = state, PageAccess = &LinuxUserPageAccess,
#if M16_009_PROBE
                UserInterrupts = state->LinuxSyscallPlan.UserInterrupts,
#endif
                CopyFromUser = &LinuxThreadCopyInput, Write = &WriteDescriptorOutput, ReturnPolicy = &LinuxNativeReturnPolicy };
            c->Plan = new() { Prepare = (ulong)(delegate* unmanaged<void*, uint, byte>)&LinuxNativeWorkerPrepare,
                Recover = (ulong)(delegate* unmanaged<void*, NativeContext*, byte>)&LinuxNativeRecover,
                TerminalCallback = (ulong)(delegate* unmanaged<void*, void>)&LinuxNativeTerminal,
                Stub = new() { AdmissionCallback = (ulong)(delegate* unmanaged<void*, NativeContext*, NativeContext*, byte>)&LinuxNativeTransfer } };
            c->Plan.Owner = new() { Initialized = true, Key = c->Key, CurrentKey = c->Key,
                Root = (ulong)state->Memory.Entries, ContextArena = pool + NativeProgram.MetadataPage * Page, ContextArenaBytes = NativeProgram.MetadataPages * Page,
                CodeStart = pool, CodeBytes = NativeProgram.Bytes, SwitchStackGuardStart = pool + NativeProgram.SwitchGuardPage * Page, SwitchStackBytes = NativeProgram.SwitchPages * Page };
            if (!LinuxNativeObserve(c)) return false;
            ((delegate* unmanaged<byte*, void>)(state->Memory.Code + FirstExitCodeBundle.CooperativeFxOffset))(helper);
            uint mask = *(uint*)(helper + 28); c->Plan.Owner.ObservedMxcsrMask = mask;
            for (uint i = 0; i < 512; i++) helper[i] = 0;
            *(ushort*)helper = 0x027f; *(uint*)(helper + 24) = 0x1f80; *(uint*)(helper + 28) = mask;
            ulong resume = pool + NativeProgram.DriverResume;
            if (NativeContext.Initialize((NativeContext*)supervisor, NativeContext.Bytes, resume,
                (state->Arena.StackTop - 64) & ~15UL, mask) != NativeContext.Status.Ok) return false;
            c->Plan.Supervisor = new() { Identity = 0x1ffffffff, CurrentIdentity = 0x1ffffffff,
                Context = (NativeContext*)supervisor, ContextBytes = NativeContext.Bytes,
                StackGuardStart = (ulong)state->Arena.Storage + 21 * Page, StackBytes = 66 * Page,
                ResumeRip = resume, Kind = NativeSwitch.ResumeKind.CapturedCall };
            c->Plan.Bindings = (NativeProgram.Binding*)bindings; c->Plan.Count = c->Capacity;
            c->Plan.BindingBytes = (uint)sizeof(NativeProgram.Binding) * LinuxNativeWorkers; c->Plan.Witness = (NativeContext*)witness;
            c->Plan.Gateway = state->LinuxSyscallCode;
#if M16_009_PROBE
            c->Plan.SeparateUserFaultCapture = true;
#endif
            c->Plan.Stub.CallbackContext = (ulong)c; c->Plan.Stub.CallbackBytes = (uint)sizeof(LinuxNativeControl);
            c->Plan.Stub.HelperFx = (ulong)helper; c->Plan.Stub.JumpSlot = (ulong)jump; c->Plan.Stub.RejectRip = pool + NativeProgram.Recovery;
            // Each entry starts at generation one, exactly as the fresh table.
            // Later reap/reuse must obtain its actual new generation before
            // rewriting that entry and publishing another Linux thread.
            for (uint i = 0; i < c->Capacity; i++)
            {
                ulong guard = pool + (NativeProgram.WorkerPage + i * NativeProgram.WorkerPages) * Page, entry = pool + NativeProgram.Entries + i * NativeProgram.WorkerStride;
                NativeContext* saved = (NativeContext*)(contexts + i * NativeContext.Bytes);
                ulong initialRsp = guard + (NativeProgram.WorkerPages - 1) * Page - 40;
                if (NativeContext.Initialize(saved, NativeContext.Bytes, entry, initialRsp, mask) != NativeContext.Status.Ok) return false;
                var d = new NativeSwitch.Descriptor { Identity = 0x100000001UL + i, CurrentIdentity = 0x100000001UL + i,
                    Context = saved, ContextBytes = NativeContext.Bytes, StackGuardStart = guard, StackBytes = NativeProgram.WorkerPages * Page,
                    ResumeRip = entry, Kind = NativeSwitch.ResumeKind.Entry };
                var e = new KernelThreadEntry.Plan { Owner = c->Plan.Owner, Initial = d,
                    CodeOffset = NativeProgram.Entries + i * NativeProgram.WorkerStride,
                    Entry = pool + NativeProgram.Workers + i * NativeProgram.WorkerStride, Argument = (ulong)(c->Slots + i),
                    Start = (ulong)(delegate* unmanaged<void*, ulong, byte>)&LinuxNativeStart,
                    Control = (ulong)c, ControlBytes = (uint)sizeof(LinuxNativeControl),
                    ExitStub = pool + NativeProgram.FromWorker + i * NativeSwitch.StubBytes, RejectRip = c->Plan.Stub.RejectRip };
                c->Plan.Bindings[i] = new() { Context = d, Entry = e, Argument = e.Argument };
                c->Workers[i] = new() { Initial = d, Entry = e.Entry };
            }
            c->Handler = new() { CodeAddress = pool + NativeProgram.TimerHandler, Callback = interrupt,
                CallbackContext = (ulong)c, CallbackBytes = (uint)sizeof(LinuxNativeControl), HelperFx = (ulong)helper };
            if (!LinuxNativeCallback(c, c->Plan.Prepare) || !LinuxNativeCallback(c, c->Plan.Stub.AdmissionCallback) ||
                !LinuxNativeCallback(c, c->Plan.Recover) || !LinuxNativeCallback(c, c->Plan.TerminalCallback) ||
                !LinuxNativeCallback(c, c->Plan.Bindings[0].Entry.Start) || !LinuxNativeCallback(c, interrupt)) return false;
            if (!NativeProgram.Emit((byte*)pool, NativeProgram.Bytes, c->Plan) ||
                !X64TimerInterrupt.Emit((byte*)c->Handler.CodeAddress, X64TimerInterrupt.Bytes, c->Handler)) return false;
#if M16_009_PROBE
            if (state->LinuxSyscallPlan.UserFaultEntry != pool + NativeProgram.UserFaultCapture ||
                !X64LinuxUserTransition.EmitFaultCapture((byte*)(pool + NativeProgram.UserFaultCapture),
                    X64LinuxUserTransition.FaultCaptureBytes, state->LinuxSyscallPlan)) return false;
#endif
            if (!LinuxNativeVerify(c)) return false;
            for (uint i = 0; i < NativeProgram.Bytes; i++) codeCopy[i] = ((byte*)pool)[i];
            return true;
        }

        private static bool LinuxNativeTake(ref ulong cursor, ulong end, uint bytes, out byte* result)
        {
            result = null;
            if (bytes == 0 || cursor > end || cursor > ~0UL - 15) return false;
            ulong start = (cursor + 15) & ~15UL;
            if (start > end || bytes > end - start) return false;
            for (uint i = 0; i < bytes; i++) ((byte*)start)[i] = 0;
            result = (byte*)start; cursor = start + bytes; return true;
        }

        private static bool LinuxNativeCallback(LinuxNativeControl* c, ulong address)
        {
            var range = c->State->Memory.Capture.Range;
            return range.IsValid && range.ImageBase.TryGetRawValueForUefiBoundary(out ulong image) == AddressValueStatus.Success &&
                address >= image && address - image < range.ImageSize && LinuxSupervisorLeaf(c->State, address, true, false);
        }

        private static bool LinuxNativeObserve(LinuxNativeControl* c)
        {
            if (c == null || !CooperativeAuthority(c->State)) return false;
            ulong* cpu = c->Cpu;
            ((delegate* unmanaged<ulong*, void>)(c->State->Memory.Code + FirstExitCodeBundle.CooperativeObserveOffset))(cpu);
            if (cpu[9] < 0x80000001 || !CooperativeSingleCpu(c->State, cpu)) return false;
            NativeSwitch.Owner* o = &c->Plan.Owner;
            o->CurrentRoot = cpu[1]; o->CurrentKernelGs = cpu[14]; o->Cr0 = cpu[0]; o->Cr4 = cpu[2]; o->Flags = cpu[3]; o->Efer = cpu[11];
            o->Cs = (ushort)cpu[4]; o->Ss = (ushort)cpu[5]; o->Tr = (ushort)cpu[6]; o->BasicEdx = (uint)cpu[7]; o->ExtendedEdx = (uint)cpu[10];
            o->Fs = (ushort)cpu[15]; o->Gs = (ushort)cpu[16]; o->CpuCount = 1;
            if (c->Transfers == 0) o->KernelGs = cpu[14];
            if (c->Plan.Bindings != null) for (uint i = 0; i < c->Plan.Count; i++)
            { c->Plan.Bindings[i].Entry.Owner = *o; c->Plan.Bindings[i].Entry.Initial = c->Plan.Bindings[i].Context; }
            return NativeSwitch.AdmitPlan(*o) == NativeSwitch.Status.Ok;
        }

        private static bool LinuxNativeVerify(LinuxNativeControl* c) =>
            NativeProgram.Verify((byte*)c->State->CooperativePool, NativeProgram.Bytes, c->Plan) &&
#if M16_009_PROBE
            c->Plan.SeparateUserFaultCapture && c->State->LinuxSyscallPlan.UserFaultEntry == c->State->CooperativePool + NativeProgram.UserFaultCapture &&
            X64LinuxUserTransition.VerifyFaultCapture((byte*)c->State->LinuxSyscallPlan.UserFaultEntry,
                X64LinuxUserTransition.FaultCaptureBytes, c->State->LinuxSyscallPlan) &&
#endif
            X64TimerInterrupt.Verify((byte*)c->Handler.CodeAddress, X64TimerInterrupt.Bytes, c->Handler);

        private static LinuxNativeControl* LinuxNativeControlOwner(void* context)
        {
            fixed (Storage* state = &s_storage)
            {
                if (state->CooperativePool == 0 || (ulong)context != state->CooperativePool + NativeProgram.MetadataPage * Page) return null;
                return LinuxNativeOwner(state);
            }
        }

        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte LinuxNativeStart(void* context, ulong identity)
        {
            LinuxNativeControl* c = LinuxNativeControlOwner(context);
            if (c == null || c->Selected == 0 || c->Selected > c->Capacity || c->Published != 1 || c->Threads.Current.Value != identity) return 0;
            uint index = c->Selected - 1;
            var running = c->Threads.Inspect(c->Key, 2, new(identity)); var binding = c->Plan.Bindings[index];
            if (!running.Succeeded || running.State != KernelThreadTable.State.Running || binding.Context.Identity != identity ||
                running.Entry != binding.Entry.Entry || running.Argument != binding.Argument || binding.Argument != (ulong)(c->Slots + index) ||
                !c->Process.Inspect(c->Key, 2, new(identity), out var view)) return 0;
            if (running.CancellationRequested && !view.Exiting) return LinuxNativeAbort(c, index);
            if (view.Exiting || running.CancellationRequested)
            { c->Actions[index] = (uint)LinuxSyscallAction.ThreadExited; c->Entered[index] = 0; return 2; }
            return 1;
        }

        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte LinuxNativeWorkerPrepare(void* context, uint index)
        {
            LinuxNativeControl* c = LinuxNativeControlOwner(context);
            if (c == null || index >= c->Capacity || c->Selected != index + 1 || c->Published != 1 || !LinuxNativeObserve(c) ||
                !LinuxNativeVerify(c) || !c->Process.Inspect(c->Key, 2, c->Threads.Current, out var view)) return 0;
            c->Entered[index] = 0;
            if (view.Exiting)
            {
                if (c->Gates[index].Status == LinuxSyscallGateStatus.Blocked)
                    _ = LinuxSyscallGateway.CompleteBlocked(ref c->Gates[index], new(LinuxSyscallAction.ThreadExited, exitCode: view.ExitCode));
                c->Actions[index] = (uint)LinuxSyscallAction.ThreadExited; return 2;
            }
            LinuxSyscallResult completion = new(LinuxSyscallAction.Return);
            bool completed = c->Gates[index].Status == LinuxSyscallGateStatus.Blocked;
            if (completed)
            {
#if CLROS_LINUX_SOCKETS
                var epollWait = view.WaitSource == 3 ? c->Threads.Inspect(c->Key, 2, c->Threads.Current) : default;
                if (view.WaitSource == 3) completion = c->Process.CompleteEpollWait(c->Key, 2);
                else
#endif
                completion = view.PipePending ? c->Process.CompletePipeWait(c->Key, 2) : c->Process.CompleteFutexWait(c->Key, 2);
                completion = LinuxSyscallGateway.CompleteBlocked(ref c->Gates[index], completion);
#if CLROS_LINUX_SOCKETS
                if (view.WaitSource == 3 && completion.Action == LinuxSyscallAction.Return &&
                    (!epollWait.Succeeded || epollWait.Wait.Sequence != view.Wait.Sequence ||
                    epollWait.Completion == KernelThreadTable.Completion.None ||
                    !LinuxText(c->State, "M16_012_COMPLETE identity=") || !LinuxNumber(c->State, c->Threads.Current.Value) ||
                    !LinuxText(c->State, " sequence=") || !LinuxNumber(c->State, view.Wait.Sequence) ||
                    !LinuxText(c->State, " completion=") || !LinuxNumber(c->State, (uint)epollWait.Completion) ||
                    !LinuxText(c->State, " valueBits=") || !LinuxNumber(c->State, unchecked((ulong)completion.Value)) ||
                    !LinuxText(c->State, " clock=") || !LinuxNumber(c->State, c->Threads.ObservedTicks) || !LinuxText(c->State, "\r\n")))
                    return LinuxNativeAbort(c, index);
                if (view.WaitSource == 3 && completion.Action == LinuxSyscallAction.Blocked)
                {
                    if (!c->Process.Inspect(c->Key, 2, c->Threads.Current, out var again) || again.WaitSource != 3 ||
                        !again.Wait.IsValid || again.Wait.Sequence <= view.Wait.Sequence || again.WaitDeadline != view.WaitDeadline ||
                        !LinuxText(c->State, "M16_012_REPARK identity=") || !LinuxNumber(c->State, c->Threads.Current.Value) ||
                        !LinuxText(c->State, " previous=") || !LinuxNumber(c->State, view.Wait.Sequence) ||
                        !LinuxText(c->State, " sequence=") || !LinuxNumber(c->State, again.Wait.Sequence) ||
                        !LinuxText(c->State, " deadline=") || !LinuxNumber(c->State, again.WaitDeadline) ||
                        !LinuxText(c->State, " clock=") || !LinuxNumber(c->State, c->Threads.ObservedTicks) || !LinuxText(c->State, "\r\n"))
                        return LinuxNativeAbort(c, index);
                }
                if (!ObserveLinuxSocketWaits(c)) return LinuxNativeAbort(c, index);
#endif
                if (completion.Action != LinuxSyscallAction.Return)
                {
                    if (completion.Action is not (LinuxSyscallAction.Blocked or LinuxSyscallAction.ThreadExited)) return LinuxNativeAbort(c, index);
                    c->Actions[index] = (uint)completion.Action;
                    return 2;
                }
                if (!LinuxText(c->State, "M16_008_RESUME identity=") || !LinuxNumber(c->State, c->Threads.Current.Value) ||
                    !LinuxText(c->State, " valueBits=") || !LinuxNumber(c->State, unchecked((ulong)completion.Value)) ||
                    !LinuxText(c->State, " status=") || !LinuxNumber(c->State, (uint)c->Gates[index].Status) || !LinuxText(c->State, "\r\n")) return LinuxNativeAbort(c, index);
            }
            if (c->Gates[index].Status == LinuxSyscallGateStatus.Uninitialized)
            {
                LinuxSyscallAdmission admission = new() { UserRoot = c->State->LinuxUserRoot.Root,
                    MaximumCalls = LinuxSyscallGateway.MaximumThreadCalls, MaximumWrite = LinuxSyscallGateway.MaximumWrite,
                    Scratch = (byte*)c->State->LinuxSyscallData + X64LinuxUserTransition.ScratchOffset, ScratchBytes = 4096,
                    FirmwareExited = true, OwnedBoundaryValidated = true, EnableStartup = true, EnableDescriptors = true, EnableThreads = true,
                    ContinuingService = OS.Kernel.Diagnostics.Probes.EnableM16014Probe };
                LinuxSyscallAccess access = c->User;
                access.Startup = &DispatchLinuxStartup; access.Descriptors = &DispatchLinuxDescriptors; access.Threads = &DispatchLinuxNativeThreadService;
                if (LinuxSyscallGateway.Begin(ref c->Gates[index], admission, access) != LinuxSyscallGateStatus.Active) return LinuxNativeAbort(c, index);
            }
#if M16_014_PROBE
            if (c->Budget.IsContinuingService)
            {
                if (c->Epoch == ulong.MaxValue) return LinuxNativeAbort(c, index);
                ulong turn = c->Epoch + 1;
                if (c->Gates[index].CurrentTurn != turn && !LinuxSyscallGateway.RenewTurn(ref c->Gates[index], turn))
                    return LinuxNativeAbort(c, index);
            }
#endif
            if (c->Gates[index].Status != LinuxSyscallGateStatus.Active || !c->State->LinuxSyscallPlan.ResumeUser)
                return LinuxNativeAbort(c, index);
            // CompletePipe/FutexWait already stored the real result. Delivery
            // may replace RAX/RIP with a handler or checked syscall restart.
            var returning = LinuxNativeUserReturn(c, index, out var image);
            if (returning.Action == LinuxSyscallAction.Terminate) return 2;
            if (returning.Action != LinuxSyscallAction.RestoreUser) return LinuxNativeAbort(c, index);
            if (!X64LinuxUserTransition.InstallUserImage((byte*)c->State->LinuxSyscallData, X64LinuxUserTransition.DataBytes,
                image, X64LinuxUserTransition.UserImageBytes, c->Plan.Owner.ObservedMxcsrMask, c->User)) return LinuxNativeAbort(c, index);
            ((delegate* unmanaged<ulong*, void>)(c->State->LinuxSyscallCode + X64LinuxUserTransition.ObserveOffset))(c->GatewayCpu);
#if M16_009_PROBE
            if (c->User.UserInterrupts && !LinuxNativeArmUserTimer(c,index)) return LinuxNativeAbort(c,index);
#endif
            c->Actions[index] = (uint)LinuxSyscallAction.Return; c->Entered[index] = 1; return 1;
        }

        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte LinuxNativeReturnPolicy(void* context,LinuxSyscallFrame* frame)
        {
            Storage* state=(Storage*)context;
            fixed(Storage* retained=&s_storage) if(state!=retained || frame==null) return 2;
            if(!LinuxSyscallAuthority(state) || !CooperativeAuthority(state)) return 2;
            LinuxNativeControl* c=(LinuxNativeControl*)(state->CooperativePool+NativeProgram.MetadataPage*Page);
            if(c->State!=state || c->Key!=(state->CooperativePool^0x4d31363030384f57UL)) return 2;
            if(c->Selected==0 || c->Selected>c->Capacity) return 0;
            uint index=c->Selected-1;
            if(!c->Gates[index].HasExitTail) return 0;
            if(LinuxNativeOwner(context)!=c || c->Published!=1 || c->Threads.Current.Value!=c->Plan.Bindings[index].Context.Identity ||
                !c->Process.Inspect(c->Key,2,c->Threads.Current,out var view) || view.SignalDepth!=0 ||
                view.BlockedSignals!=~LinuxSignalMask.Unblockable) return 2;
            return LinuxSyscallGateway.ExitTailPolicy(ref c->Gates[index],*frame);
        }

        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static LinuxSyscallResult DispatchLinuxNativeThreadService(void* context, LinuxSyscallFrame* frame)
        {
            LinuxNativeControl* c = LinuxNativeOwner(context);
            return c == null || frame == null ? new(LinuxSyscallAction.Rejected) : c->Process.Dispatch(c->Key, 2, ref *frame);
        }

        // The fixed gateway dispatcher calls this only after its complete user
        // image is captured and the separately owned kernel CR3 is active.
        private static byte DispatchLinuxNativeCore(Storage* state, LinuxSyscallFrame* frame)
        {
            LinuxNativeControl* c = LinuxNativeOwner(state);
            if (c == null || frame != (LinuxSyscallFrame*)(state->LinuxSyscallData + X64LinuxUserTransition.FrameOffset) ||
                c->Selected == 0 || c->Selected > c->Capacity) return 0;
#if M16_009_PROBE
            // Sample only after owner and exact-frame admission. The initial
            // owner validation is outside these optional syscall intervals.
            ulong entered = 0, admitted = 0, dispatched = 0, prepared = 0;
            delegate* unmanaged<void*, ulong*, byte> timingRead = &ReadLinuxClock;
            bool timing = c->Budget.RequiresClock && timingRead(state, &entered) == 1;
#endif
            if (!c->Process.CurrentImage(c->Key, 2, out var image) ||
                !X64LinuxUserTransition.CaptureUserImage((byte*)state->LinuxSyscallData, X64LinuxUserTransition.DataBytes,
                    image, X64LinuxUserTransition.UserImageBytes, c->Plan.Owner.ObservedMxcsrMask, c->User)) return 0;
            uint index = c->Selected - 1;
            if (c->Entered[index] != 1 || c->Threads.Current.Value != c->Plan.Bindings[index].Context.Identity) return 0;
#if CLROS_LINUX_SOCKETS
            if (!PollLinuxSocketNetwork(c)) return LinuxNativeAbort(c, index);
#endif
            LinuxSyscallFrame call = *frame;
#if M10_017_PROBE
            LinuxStaticFileBefore staticFileBefore = default;
            if (!LinuxStaticCaptureBefore(c, call, &staticFileBefore)) return LinuxNativeAbort(c, index);
            bool staticListenerClosing = call.Number == 3 && LinuxDescriptorServices.IsCurrent(&state->LinuxDescriptors, c->StaticListener) &&
                unchecked((uint)call.Argument0) == c->StaticListener.Number;
            bool staticListenerLast = staticListenerClosing && LinuxDescriptorServices.IsLastReference(&state->LinuxDescriptors, c->StaticListener);
#endif
#if M16_009_PROBE
            timing = timing && timingRead(state, &admitted) == 1;
#endif
            var result = LinuxSyscallGateway.Dispatch(ref c->Gates[index], ref *frame);
#if M10_017_PROBE
            if (!LinuxStaticObserveFile(c, index, call, result, &staticFileBefore) ||
                !LinuxStaticObserveCall(c, call, result, staticListenerClosing, staticListenerLast)) return LinuxNativeAbort(c, index);
#elif M10_004_PROBE
            if (!LinuxWebObserveCall(c, call, result)) return LinuxNativeAbort(c, index);
#endif
#if M16_014_PROBE
            if (!LinuxServiceObserveCall(c, index, call, result)) return LinuxNativeAbort(c, index);
#endif
#if M16_009_PROBE
            timing = timing && timingRead(state, &dispatched) == 1;
#endif
            image->Frame = *frame; image->Value = result.Value;
            // Sigreturn's original restorer input remains in call; this is the
            // actual admitted output before pending delivery can replace it.
            LinuxSyscallFrame returned = *frame;
            if (!c->Process.Inspect(c->Key, 2, c->Threads.Current, out var dispatchedView))
            { if (LinuxNativeAbort(c, index) != 2) return 0; return 2; }
            // Keep the actual syscall record separate from a handler's new
            // register image. Full installation below aliases the gateway frame.
            // Sigreturn replaces the ref frame itself. Its serial record must
            // retain the real restorer SYSCALL, not a user-edited return RIP.
            if (call.Number != 15) call = *frame;
            state->LinuxUserRoot = state->LinuxMemory.Root;
            byte* data = (byte*)state->LinuxSyscallData;
            var returning = result;
#if M16_014_PROBE
            bool quotaPending = state->LinuxQuota == LinuxQuotaDisposition.Pending;
            if (!quotaPending && result.Action is LinuxSyscallAction.Return or LinuxSyscallAction.RestoreUser)
#else
            if (result.Action is LinuxSyscallAction.Return or LinuxSyscallAction.RestoreUser)
#endif
                returning = LinuxNativeUserReturn(c, index, out image);
            if (returning.Action == LinuxSyscallAction.RestoreUser &&
                !X64LinuxUserTransition.RestoreActiveUserImage(data, X64LinuxUserTransition.DataBytes, image,
                    X64LinuxUserTransition.UserImageBytes, c->Plan.Owner.ObservedMxcsrMask, c->User))
            { if (LinuxNativeAbort(c, index) != 2) return 0; return 2; }
            *(long*)(data + X64LinuxUserTransition.ValueOffset) = returning.Value;
            *(ulong*)(data + X64LinuxUserTransition.OutcomeOffset) = (ulong)returning.Action;
            c->Actions[index] = (uint)returning.Action;
            if (result.Action == LinuxSyscallAction.Terminate)
            {
                if (!c->Process.Inspect(c->Key, 2, c->Threads.Current, out var view) || !view.Exiting)
                { if (LinuxNativeAbort(c, index) != 2) return 0; }
                else
                {
                    state->LinuxSyscallExitCode = result.ExitCode;
#if M16_014_PROBE
                    c->Stopping = c->ServiceShutdown == 1 ? 2U : 1U;
#else
                    c->Stopping = 1;
#endif
                }
            }
            // Observe the actual retained wait after dispatch/return preparation.
            // No deadline is inferred from a user timespec or timer ready count.
            if (!c->Process.Inspect(c->Key, 2, c->Threads.Current, out var waitView))
            { if (LinuxNativeAbort(c, index) != 2) return 0; return 2; }
#if M16_009_PROBE
            timing = timing && timingRead(state, &prepared) == 1 && entered <= admitted && admitted <= dispatched && dispatched <= prepared;
#endif
            bool logged = LinuxText(state, "M16_008_CALL identity=") && LinuxNumber(state, c->Threads.Current.Value) &&
                LinuxText(state, " nr=") && LinuxNumber(state, call.Number) &&
                LinuxText(state, " a0=") && LinuxNumber(state, call.Argument0) && LinuxText(state, " a1=") && LinuxNumber(state, call.Argument1) &&
                LinuxText(state, " a2=") && LinuxNumber(state, call.Argument2) && LinuxText(state, " a3=") && LinuxNumber(state, call.Argument3) &&
                LinuxText(state, " a4=") && LinuxNumber(state, call.Argument4) && LinuxText(state, " a5=") && LinuxNumber(state, call.Argument5) &&
                LinuxText(state, " rip=") && LinuxNumber(state, call.Rip) && LinuxText(state, " rsp=") && LinuxNumber(state, call.Rsp) &&
                LinuxText(state, " flags=") && LinuxNumber(state, call.Rflags) && LinuxText(state, " fs=") && LinuxNumber(state, call.FsBase) &&
                LinuxText(state, " gs=") && LinuxNumber(state, call.GsBase) && LinuxText(state, " clock=") && LinuxNumber(state, c->Threads.ObservedTicks) &&
                LinuxText(state, " valueBits=") && LinuxNumber(state, unchecked((ulong)result.Value)) &&
                LinuxText(state, " action=") && LinuxNumber(state, (uint)result.Action) &&
                LinuxText(state, " status=") && LinuxNumber(state, (uint)c->Gates[index].Status) &&
                LinuxText(state, " ordinal=") && LinuxNumber(state, LinuxNativeCallOrdinal(c, index)) &&
                LinuxText(state, " waitSource=") && LinuxNumber(state, waitView.WaitSource) &&
                LinuxText(state, " waitDeadline=") && LinuxNumber(state, waitView.WaitDeadline) &&
                LinuxText(state, " waitSequence=") && LinuxNumber(state, waitView.Wait.IsValid ? waitView.Wait.Sequence : 0) &&
                LinuxText(state, " signalDepth=") && LinuxNumber(state, dispatchedView.SignalDepth) &&
                LinuxText(state, " signalFrame=") && LinuxNumber(state, dispatchedView.SignalFrame) &&
                LinuxText(state, " blockedSignals=") && LinuxNumber(state, dispatchedView.BlockedSignals) &&
                LinuxText(state, " returnRip=") && LinuxNumber(state, returned.Rip) &&
                LinuxText(state, " returnRsp=") && LinuxNumber(state, returned.Rsp) &&
                LinuxText(state, " returnFlags=") && LinuxNumber(state, returned.Rflags) &&
                LinuxText(state, " returnFs=") && LinuxNumber(state, returned.FsBase) &&
                LinuxText(state, " returnGs=") && LinuxNumber(state, returned.GsBase) &&
                LinuxText(state, " returnValueBits=") && LinuxNumber(state, unchecked((ulong)result.Value)) && LinuxText(state, "\r\n");
#if M16_014_PROBE && CLROS_LINUX_SOCKETS
            if (logged && LinuxQuotaPolicy(state)) logged = ObserveLinuxQuotaSocketCall(c, index, call, result);
#endif
#if M16_014_PROBE
            if (logged && LinuxQuotaPolicy(state)) logged = ObserveLinuxQuotaPthreadCall(c, index, call, result);
            if (logged && LinuxQuotaPolicy(state)) logged = ObserveLinuxQuotaStackLeases(c, false);
#endif
#if CLROS_LINUX_SOCKETS
            if (logged && call.Number is 232 or 281 && result.Action == LinuxSyscallAction.Return && result.Value == 0 &&
                unchecked((int)call.Argument3) > 0)
                logged = c->Process.InspectEpollExpiry(c->Key, 2, out ulong expired) &&
                    LinuxText(state, "M16_012_EXPIRE identity=") && LinuxNumber(state, c->Threads.Current.Value) &&
                    LinuxText(state, " ordinal=") && LinuxNumber(state, LinuxNativeCallOrdinal(c, index)) &&
                    LinuxText(state, " deadline=") && LinuxNumber(state, expired) &&
                    LinuxText(state, " clock=") && LinuxNumber(state, c->Threads.ObservedTicks) && LinuxText(state, "\r\n");
#endif
#if M16_009_PROBE
            if (logged && timing)
                logged = LinuxNativeSyscallTimingWrite(c, call.Number, entered, admitted, dispatched, prepared);
#endif
            // Capture descriptor/signal wakes before a later timer advance can
            // obscure their winner. CALL above introduces a newly staged wait.
#if CLROS_LINUX_SOCKETS
            logged = logged && ObserveLinuxSocketWaits(c);
#endif
#if M16_014_PROBE
            if (quotaPending && logged && result.Action == LinuxSyscallAction.Return &&
                c->Gates[index].Status == LinuxSyscallGateStatus.Active && call.Number is 9 or 10 or 12 or 25)
            {
                if (!LinuxQuotaPolicy(state) || c->Stopping != 0 || c->Failed != 0 || c->Process.Failed ||
                    c->ServiceShutdown != 0 || !c->Budget.RequestCancellation() ||
                    !LinuxText(state, "M16_016_QUOTA_ABORT identity=") || !LinuxNumber(state, c->Threads.Current.Value) ||
                    !LinuxText(state, " ordinal=") || !LinuxNumber(state, LinuxNativeCallOrdinal(c, index)) ||
                    !LinuxText(state, " nr=") || !LinuxNumber(state, call.Number) ||
                    !LinuxText(state, " backed=") || !LinuxNumber(state, state->LinuxMemory.BackedPages) ||
                    !LinuxText(state, " pool=") || !LinuxNumber(state, LinuxMemoryPoolPages(state)) ||
                    !LinuxText(state, " exit=99 policy=terminate cause=physical-backing\r\n")) return LinuxNativeAbort(c, index);
                state->LinuxQuota = LinuxQuotaDisposition.Terminating;
                return LinuxNativeAbort(c, index);
            }
#endif
            if (!logged || result.Action == LinuxSyscallAction.Rejected || returning.Action == LinuxSyscallAction.Rejected)
            { if (LinuxNativeAbort(c, index) != 2) return 0; }
            return logged && returning.Action == LinuxSyscallAction.RestoreUser ? (byte)1 : (byte)2;
        }

        // A service renews its bounded per-turn quota; observation ordinals
        // must still increase throughout this exact worker generation.
        private static ulong LinuxNativeCallOrdinal(LinuxNativeControl* c, uint index)
        {
#if M16_014_PROBE
            return c->Gates[index].TotalCalls;
#else
            return c->Gates[index].Calls;
#endif
        }

#if M16_009_PROBE
        // Optional raw HPET observations exclude serial logging. Failed reads
        // suppress the sample; they never bypass the original syscall checks.
        private static bool LinuxNativeSyscallTimingWrite(LinuxNativeControl* c, ulong number,
            ulong entered, ulong admitted, ulong dispatched, ulong prepared) =>
            LinuxText(c->State, "M16_008_SYSCALL_TIME identity=") && LinuxNumber(c->State, c->Threads.Current.Value) &&
            LinuxText(c->State, " ordinal=") && LinuxNumber(c->State, LinuxNativeCallOrdinal(c, c->Selected - 1)) &&
            LinuxText(c->State, " nr=") && LinuxNumber(c->State, number) &&
            LinuxText(c->State, " entered=") && LinuxNumber(c->State, entered) &&
            LinuxText(c->State, " admitted=") && LinuxNumber(c->State, admitted) &&
            LinuxText(c->State, " dispatched=") && LinuxNumber(c->State, dispatched) &&
            LinuxText(c->State, " prepared=") && LinuxNumber(c->State, prepared) &&
            LinuxText(c->State, " periodFs=") && LinuxNumber(c->State, c->State->Clock.PeriodFemtoseconds) && LinuxText(c->State, "\r\n");
#endif

        private static byte LinuxNativeAbort(LinuxNativeControl* c, uint index)
        {
            // Gate rejection/call limits have not necessarily staged a process
            // exit. Use the real owner shutdown before CommitExit can run.
            c->Failed = c->Stopping = 1; c->State->LinuxSyscallExitCode = 99;
#if M16_009_PROBE
            c->UserPreempted = 0;
#endif
            if (!c->Process.Shutdown(c->Key, 2, 99)) return 0;
            var current = c->Threads.Inspect(c->Key, 2, c->Threads.Current);
            if (!current.Succeeded) return 0;
            // BeginWait owns a Parking state until the genuine capture commits
            // Park, even if cancellation already won. Preserve that handoff;
            // the resumed exit path then drops its retained wait and backing.
            c->Actions[index] = (uint)(current.State == KernelThreadTable.State.Parking ? LinuxSyscallAction.Blocked : LinuxSyscallAction.ThreadExited);
            return 2;
        }

        private static bool LinuxNativeGatewayRestored(LinuxNativeControl* c, NativeContext* captured)
        {
            Storage* state = c->State; byte* data = (byte*)state->LinuxSyscallData;
            ulong* after = stackalloc ulong[(int)X64LinuxUserTransition.ObservationWords];
            ((delegate* unmanaged<ulong*, void>)(state->LinuxSyscallCode + X64LinuxUserTransition.ObserveOffset))(after);
            if ((captured->Rax & 255) != 1 || *(ulong*)(data + X64LinuxUserTransition.PhaseOffset) != 2 ||
                *(ulong*)(state->Arena.Descriptors->Tss + 4) != *(ulong*)(data + 0xd0) ||
                (after[3] & 0x74700) != (c->GatewayCpu[3] & 0x74700) || !FirstExitOwnedArena.ValidateCanaries(state->Arena)) return false;
            for (uint i = 0; i < X64LinuxUserTransition.ObservationWords; i++) if (i != 3 && after[i] != c->GatewayCpu[i]) return false;
            for (uint i = 0; i < 512; i++) if (data[X64LinuxUserTransition.KernelFxOffset + i] != data[X64LinuxUserTransition.UserFxOffset + i]) return false;
            return true;
        }

        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte LinuxNativeTransfer(void* context, NativeContext* source, NativeContext* destination)
        {
            LinuxNativeControl* c = LinuxNativeControlOwner(context);
            if (c == null || c->Selected == 0 || c->Selected > c->Capacity || !LinuxNativeObserve(c) || !CooperativeAuditPool(c->State, true)) return 0;
            uint index = c->Selected - 1; var worker = c->Plan.Bindings[index].Context;
            if (c->Threads.Current.Value != worker.Identity) return 0;
            if (source == c->Plan.Supervisor.Context && destination == worker.Context)
            {
                var running = c->Threads.Inspect(c->Key, 2, new(worker.Identity));
                if (!running.Succeeded || running.State != KernelThreadTable.State.Running) return 0;
                ulong expected = c->State->CooperativePool + index * NativeProgram.DriverStride + NativeProgram.DriverResume;
                if (source->Rip != expected) return 0;
                c->Plan.Supervisor.ResumeRip = expected;
                if (!LinuxNativeVerify(c) || NativeSwitch.Admit(c->Plan.Owner, c->Plan.Supervisor, worker) != NativeSwitch.Status.Ok) return 0;
            }
            else
            {
                ulong code = c->State->CooperativePool, body = code + NativeProgram.Workers + index * NativeProgram.WorkerStride;
                ulong entry = code + NativeProgram.Entries + index * NativeProgram.WorkerStride;
                if (source != worker.Context || destination != c->Plan.Supervisor.Context ||
                    source->Rip != body + NativeProgram.WorkerResume && source->Rip != entry + KernelThreadEntry.ExitResumeOffset ||
                    c->Entered[index] != 0 && !LinuxNativeGatewayRestored(c, source)) return 0;
                worker.Kind = NativeSwitch.ResumeKind.CapturedCall; worker.ResumeRip = source->Rip;
                c->Plan.Bindings[index].Context = c->Plan.Bindings[index].Entry.Initial = worker;
                if (!LinuxNativeVerify(c) || NativeSwitch.Admit(c->Plan.Owner, worker, c->Plan.Supervisor) != NativeSwitch.Status.Ok) return 0;
#if M16_009_PROBE
                // The gateway has restored the original kernel envelope and
                // this complete CALL image is stable. A known checkpoint can
                // acknowledge a pending one-shot before any table transition.
                if (c->User.UserInterrupts && !LinuxNativeStopUserTimer(c,index)) return 0;
#endif
                var action = (LinuxSyscallAction)c->Actions[index];
                if (c->State->LinuxSyscallFaults != 0 || c->State->LinuxSyscallFailed != 0)
                {
                    if (LinuxNativeAbort(c, index) != 2) return 0;
                    action = (LinuxSyscallAction)c->Actions[index];
                }
                if (action == LinuxSyscallAction.Yielded)
                {
#if M16_009_PROBE
                    if (c->UserPreempted != 0 && c->UserPreempted != index + 1) return 0;
#endif
                    if (!c->Threads.Yield(c->Key, 2, new(worker.Identity), worker).Succeeded) return 0;
#if M16_009_PROBE
                    c->UserPreempted = 0;
#endif
                }
                else
#if M16_009_PROBE
                if (c->UserPreempted!=0 && action==LinuxSyscallAction.Return)
                {
                    if (c->UserPreempted!=index+1 || !c->Threads.Yield(c->Key,2,new(worker.Identity),worker).Succeeded) return 0;
                    c->UserPreempted=0;
                }
                else
#endif
                if (action == LinuxSyscallAction.Blocked)
                {
                    bool handoff;
                    bool committed =
#if CLROS_LINUX_SOCKETS
                        c->Slots[index].WaitSource == 3 ? c->Process.CommitEpollWait(c->Key, 2, worker, out handoff) :
#endif
                        c->Slots[index].PipeOperation.IsActive
                        ? c->Process.CommitPipeWait(c->Key, 2, worker, out handoff) : c->Process.CommitFutexWait(c->Key, 2, worker, out handoff);
                    if (!committed || !handoff && !c->Threads.Yield(c->Key, 2, new(worker.Identity), worker).Succeeded) return 0;
                }
                else if (action is LinuxSyscallAction.ThreadExited or LinuxSyscallAction.Terminate)
                {
                    if (!c->Process.CommitExit(c->Key, 2, worker, out long clearTid)) return 0;
                    if (!LinuxText(c->State, "M16_008_EXIT identity=") || !LinuxNumber(c->State, worker.Identity) ||
                        !LinuxText(c->State, " clearTidResult=") || !LinuxNumber(c->State, unchecked((ulong)clearTid)) || !LinuxText(c->State, "\r\n")) return 0;
                }
                else return 0;
                if (c->Threads.Current.IsValid || !c->Threads.HandoffPending) return 0;
                c->Entered[index] = 0;
            }
            c->Transfers++; return 1;
        }

        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte LinuxNativeRecover(void* context, NativeContext* supervisor)
        {
            LinuxNativeControl* c = LinuxNativeControlOwner(context);
            if (c == null || supervisor != c->Plan.Supervisor.Context || !LinuxNativeObserve(c) ||
                NativeSwitch.AdmitResume(c->Plan.Owner, c->Plan.Supervisor) != NativeSwitch.Status.Ok) return 0;
            c->Failed = 1; return 1;
        }

        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static void LinuxNativeTerminal(void* context)
        {
            LinuxNativeControl* c = LinuxNativeControlOwner(context);
#if M16_009_PROBE
            if (c != null) _ = LinuxText(c->State, "TEST_FAIL m16-009 native-transfer\r\n");
#else
            if (c != null) _ = LinuxText(c->State, "TEST_FAIL m16-008 native-transfer\r\n");
#endif
            while (true) { }
        }

        // Actual RX/guard publication must precede this binding. It initializes
        // the existing table/process owners and attaches the concrete memory
        // guard; no worker is created or scheduled here. The caller retains all
        // ranges until Release and actual original-PTE restoration complete.
        private static bool LinuxNativeBind(LinuxNativeControl* c)
        {
            if (c == null || c->Phase != 1 || c->Published != 1 || !LinuxNativeObserve(c) ||
                !CooperativeAuditPool(c->State, true) || !LinuxNativeVerify(c)) return false;
            c->Plan.Owner.GuardsVerified = c->Plan.Owner.MappingsVerified = true;
            if (!LinuxNativeObserve(c) || c->Threads.Initialize(c->Key, 2, c->Plan.Owner, c->Plan.Supervisor,
                c->KernelSlots, (uint)sizeof(KernelThreadSlot) * LinuxNativeWorkers, c->Capacity,
                c->Ready, (uint)sizeof(OSHostHandle) * LinuxNativeWorkers, c->Capacity,
                c->Deadlines, (uint)sizeof(DeadlineQueue.Entry) * LinuxNativeWorkers, c->Capacity) != KernelThreadTable.Status.Ok) return false;
            c->TableAttached = 1;
            LinuxThreadServices.Admission owner = new() { Threads = &c->Threads, ThreadKey = c->Key,
                Slots = c->Slots, SlotBytes = (uint)sizeof(LinuxThreadServices.Slot) * LinuxNativeWorkers,
                Workers = c->Workers, WorkerBytes = (uint)sizeof(LinuxThreadServices.Worker) * LinuxNativeWorkers,
                Capacity = c->Capacity, ProcessId = 1, ObservedMxcsrMask = c->Plan.Owner.ObservedMxcsrMask,
                User = c->User, CopyToUser = &LinuxThreadCopyOutput, FirmwareExited = true, SingleOwnerValidated = true };
            if (!c->Process.Initialize(c->Key, 2, owner)) return false;
            c->ProcessAttached = 1;
            if (!LinuxNativeBindSignals(c)) return false;
            LinuxThreadServices.FutexAdmission futex = new() { OwnerKey = c->Key ^ 0x4655544558UL,
                Cells = c->Cells, CellBytes = (uint)sizeof(LinuxThreadServices.FutexCell) * LinuxNativeFutexes,
                Addresses = c->Addresses, AddressBytes = (uint)sizeof(WaitAddress.Slot) * LinuxNativeFutexes,
                Registrations = c->Registrations, RegistrationBytes = (uint)sizeof(WaitAddress.Registration) * LinuxNativeWorkers,
                Capacity = LinuxNativeFutexes, Resolve = &LinuxThreadResolveFutex, ReadMonotonic = &LinuxThreadReadNanoseconds };
            if (!c->Process.EnableFutex(c->Key, 2, futex) || !c->Process.EnablePipes(c->Key, 2, &c->State->LinuxDescriptors) ||
                !LinuxMemoryServices.AttachMutationGuard(&c->State->LinuxMemory, c->State, &LinuxThreadMemoryGuard)) return false;
            c->GuardAttached = 1; c->Phase = 2;
            ulong now = 0;
            delegate* unmanaged<void*, ulong*, byte> clock = &LinuxThreadReadNanoseconds;
            bool application = LinuxUsesApplicationLimits(c->State);
            return (!application || clock(c->State, &now) == 1) && c->Budget.Begin(c->Capacity, now, application,
                continuingWeb: OS.Kernel.Diagnostics.Probes.EnableM10004Probe,
                continuingService: OS.Kernel.Diagnostics.Probes.EnableM16014Probe);
        }

#if M16_014_PROBE
        private static bool LinuxServiceObserveCall(LinuxNativeControl* c, uint index, LinuxSyscallFrame call, LinuxSyscallResult result)
        {
            if (!c->Budget.IsContinuingService || c->ServiceProgress == ulong.MaxValue ||
                c->Gates[index].Status is not (LinuxSyscallGateStatus.Active or LinuxSyscallGateStatus.Blocked or LinuxSyscallGateStatus.Exited)) return false;
            c->ServiceProgress++;
            if (c->Gates[index].TotalCalls > c->ServicePeakCalls)
            { c->ServicePeakCalls = c->Gates[index].TotalCalls; c->ServicePeakIdentity = c->Plan.Bindings[index].Context.Identity; }
            if (call.Number == 231 && result.Action == LinuxSyscallAction.Terminate && result.ExitCode == 0)
            {
                ulong now = 0;
                delegate* unmanaged<void*, ulong*, byte> clock = &LinuxThreadReadNanoseconds;
                if (c->ServiceShutdown != 0 || clock(c->State, &now) != 1 ||
                    !c->Budget.ObserveProgress(now, c->ServiceProgress, false) || !c->Budget.RequestCancellation()) return false;
                c->ServiceShutdown = 1; c->Stopping = 2; // Explicit app shutdown; continue genuine exit/reap work.
            }
            return true;
        }

        private static bool LinuxServiceObserveWorkers(LinuxNativeControl* c, ulong now)
        {
            if (!c->Budget.ObserveProgress(now, c->ServiceProgress, c->Threads.ReadyCount != 0)) return false;
            for (uint i = 0; i < c->Capacity; i++)
            {
                if (c->KernelSlots[i].State == KernelThreadTable.State.Empty) continue;
                var worker = c->Threads.Inspect(c->Key, 2, new(c->Plan.Bindings[i].Context.Identity));
                if (!worker.Succeeded) return false;
                if (!c->Budget.ObserveWorker(now, i, c->Gates[i].TotalCalls,
                    worker.State is KernelThreadTable.State.Ready or KernelThreadTable.State.Running))
                {
                    _ = LinuxText(c->State, "M16_014_STALLED identity=") && LinuxNumber(c->State, worker.Identity.Value) &&
                        LinuxText(c->State, " calls=") && LinuxNumber(c->State, c->Gates[i].TotalCalls) &&
                        LinuxText(c->State, " nanos=") && LinuxNumber(c->State, now) && LinuxText(c->State, "\r\n");
                    return false;
                }
            }
            if (c->ServiceReady == 0 && c->ServicePeakCalls > LinuxSyscallGateway.MaximumThreadCalls &&
                now - c->Budget.StartedNanoseconds > LinuxThreadExecutionBudget.RuntimeNanoseconds && (c->WebFlags & 16) != 0)
            {
                if (!LinuxText(c->State, "M16_014_PROGRESS identity=") || !LinuxNumber(c->State, c->ServicePeakIdentity) ||
                    !LinuxText(c->State, " calls=") || !LinuxNumber(c->State, c->ServicePeakCalls) ||
                    !LinuxText(c->State, " started=") || !LinuxNumber(c->State, c->Budget.StartedNanoseconds) ||
                    !LinuxText(c->State, " nanos=") || !LinuxNumber(c->State, now) ||
                    !LinuxText(c->State, " epochs=") || !LinuxNumber(c->State, c->Epoch) ||
                    !LinuxText(c->State, " active=") || !LinuxNumber(c->State, c->Process.ActiveThreads) ||
                    !LinuxText(c->State, " owner=live cleanup=pending\r\nCLROS_M16_014_READY\r\n")) return false;
                c->ServiceReady = 1;
            }
            return true;
        }
#endif

#if M16_014_PROBE
        // Copy one newly published child's raw TCB prefix on the serialized
        // parent turn. The process/memory owners retain all backing; no guest
        // pointer or alias survives this call, and no libc layout is inferred.
        private static bool ObserveLinuxQuotaPthreadCall(LinuxNativeControl* c, uint index,
            LinuxSyscallFrame call, LinuxSyscallResult result)
        {
            if (call.Number != 56 || result.Action != LinuxSyscallAction.Return || result.Value <= 0) return true;
            if (c == null || c->State == null || LinuxNativeOwner(c->State) != c || !LinuxQuotaPolicy(c->State) ||
                c->Phase != 3 || c->Published != 1 || c->Stopping != 0 || c->Capacity == 0 || c->Capacity > LinuxNativeWorkers ||
                c->Slots == null || c->KernelSlots == null || c->Plan.Count != c->Capacity ||
                c->Selected == 0 || c->Selected > c->Capacity || index != c->Selected - 1 ||
                !c->Threads.Current.IsValid || c->Threads.Current.Value != c->Plan.Bindings[index].Context.Identity ||
                c->Gates[index].Status != LinuxSyscallGateStatus.Active || LinuxNativeCallOrdinal(c, index) == 0 ||
                result.Value > int.MaxValue || call.Argument0 != LinuxThreadServices.PthreadCloneFlags ||
                !c->Process.Inspect(c->Key, 2, c->Threads.Current, out var parent) || parent.Exiting ||
                parent.Kernel.Value != c->Slots[index].Kernel.Value || parent.Tid != c->Slots[index].Tid) return false;
            uint childIndex = c->Capacity, childTid = (uint)result.Value;
            for (uint i = 0; i < c->Capacity; i++)
            {
                if (c->Slots[i].Tid != childTid) continue;
                if (childIndex != c->Capacity) return false;
                childIndex = i;
            }
            if (childIndex == c->Capacity || childIndex == index) return false;
            var slot = c->Slots + childIndex; var kernel = c->KernelSlots + childIndex;
            ulong identity = ((ulong)kernel->Generation << 32) | (childIndex + 1UL);
            var binding = c->Plan.Bindings[childIndex];
            var created = c->Threads.Inspect(c->Key, 2, slot->Kernel);
            if (kernel->Generation == 0 || slot->Kernel.Value != identity || !created.Succeeded ||
                created.State != KernelThreadTable.State.Ready || created.Identity.Value != identity ||
                created.Context.Identity != identity || created.Context.Context != binding.Context.Context ||
                created.Entry != binding.Entry.Entry || created.Argument != (ulong)slot ||
                binding.Context.Identity != identity || binding.Argument != (ulong)slot ||
                !c->Process.Inspect(c->Key, 2, slot->Kernel, out var child) || child.Exiting || child.Tid != childTid ||
                child.ClearTid != call.Argument3 || child.Frame.FsBase != call.Argument4 || child.Frame.Rsp != call.Argument1 ||
                child.Frame.Rip != call.Rip) return false;
            byte* data = stackalloc byte[112];
            if (!LinuxThreadKernelRange(c, data, 112, false) || CopyLinuxAlias(c->State, call.Argument4, data, 112, false) != 1) return false;
            Storage* state = c->State;
            if (!LinuxText(state, "M16_016_PTHREAD identity=") || !LinuxNumber(state, c->Threads.Current.Value) ||
                !LinuxText(state, " ordinal=") || !LinuxNumber(state, LinuxNativeCallOrdinal(c, index)) ||
                !LinuxText(state, " child=") || !LinuxNumber(state, identity) ||
                !LinuxText(state, " childTid=") || !LinuxNumber(state, childTid) ||
                !LinuxText(state, " tls=") || !LinuxNumber(state, call.Argument4) ||
                !LinuxText(state, " sp=") || !LinuxNumber(state, call.Argument1) ||
                !LinuxText(state, " parentTid=") || !LinuxNumber(state, call.Argument2) ||
                !LinuxText(state, " clearTid=") || !LinuxNumber(state, call.Argument3) ||
                !LinuxText(state, " rip=") || !LinuxNumber(state, call.Rip) || !LinuxText(state, " bytes=112 data=")) return false;
            for (uint i = 0; i < 112; i++)
            {
                uint high = (uint)data[i] >> 4, low = (uint)data[i] & 15;
                if (!LinuxByte(state, (byte)(high < 10 ? '0' + high : 'a' + high - 10)) ||
                    !LinuxByte(state, (byte)(low < 10 ? '0' + low : 'a' + low - 10))) return false;
            }
            return LinuxText(state, "\r\n");
        }

        // These are kernel callback stack/context leases. The guest pthread
        // stack remains owned by user memory; clone RSP supplies no full bound.
        private static bool ObserveLinuxQuotaStackLeases(LinuxNativeControl* c, bool end)
        {
            if (c == null || !LinuxQuotaPolicy(c->State) || c->Phase != 3 || c->Published != 1 ||
                c->Capacity == 0 || c->Capacity > LinuxNativeWorkers || c->Plan.Count != c->Capacity ||
                c->QuotaStackEnded != 0 || !LinuxNativeVerify(c)) return false;
            Storage* state = c->State;
            if (c->QuotaStackStarted == 0 || end)
            {
                if (!CooperativeAuditPool(state, true) || !FirstExitOwnedArena.ValidateCanaries(state->Arena) ||
                    c->Plan.Supervisor.StackGuardStart != (ulong)state->Arena.Storage + 21 * Page ||
                    c->Plan.Supervisor.StackBytes != 66 * Page) return false;
                if (c->QuotaStackStarted == 0)
                {
                    ulong worker = 0;
                    for (uint page = NativeProgram.WorkerPage; page < NativeProgram.IrqGuardPage; page++)
                        if (!NativeProgram.IsGuardPage(page)) worker += Page;
                    if (!LinuxText(state, "M16_016_STACK_BACKING cooperativePages=") || !LinuxNumber(state, CooperativePoolPages) ||
                        !LinuxText(state, " workerMappedBytes=") || !LinuxNumber(state, worker) ||
                        !LinuxText(state, " switchMappedBytes=") || !LinuxNumber(state, (NativeProgram.SwitchPages - 2UL) * Page) ||
                        !LinuxText(state, " irqMappedBytes=") || !LinuxNumber(state, (NativeProgram.IrqPages - 2UL) * Page) ||
                        !LinuxText(state, " mainSupervisorUsableBound=") || !LinuxNumber(state, c->Plan.Supervisor.StackBytes - 2 * Page) ||
                        !LinuxText(state, " reservation=retained\r\n")) return false;
                }
            }
            ulong stacks = 0, contexts = 0; uint live = 0; bool changed = c->QuotaStackStarted == 0;
            for (uint i = 0; i < c->Capacity; i++)
            {
                var slot = c->KernelSlots + i;
                if (slot->State is KernelThreadTable.State.Empty or KernelThreadTable.State.Retired) continue;
                var d = slot->Context; ulong identity = ((ulong)slot->Generation << 32) | (i + 1UL);
                var held = c->Plan.Bindings[i].Context;
                if (slot->Generation == 0 || d.Identity != identity || held.Identity != identity || d.Context != held.Context ||
                    d.ContextBytes != NativeContext.Bytes || d.StackGuardStart != state->CooperativePool +
                        (NativeProgram.WorkerPage + i * NativeProgram.WorkerPages) * Page || d.StackBytes != NativeProgram.WorkerPages * Page ||
                    held.StackGuardStart != d.StackGuardStart || held.StackBytes != d.StackBytes ||
                    !c->Threads.Inspect(c->Key, 2, new(identity)).Succeeded) return false;
                ulong prior = c->QuotaStackGenerations[i];
                if (prior != slot->Generation)
                {
                    if (prior != 0 && slot->Generation != prior + 1 || c->QuotaStackAcquired == ~0UL ||
                        !LinuxText(state, "M16_016_STACK_LEASE identity=") || !LinuxNumber(state, identity) ||
                        !LinuxText(state, " index=") || !LinuxNumber(state, i) ||
                        !LinuxText(state, " stack=") || !LinuxNumber(state, d.StackGuardStart + Page) ||
                        !LinuxText(state, " stackBytes=") || !LinuxNumber(state, d.StackBytes - 2 * Page) ||
                        !LinuxText(state, " context=") || !LinuxNumber(state, (ulong)d.Context) ||
                        !LinuxText(state, " contextBytes=") || !LinuxNumber(state, d.ContextBytes) || !LinuxText(state, "\r\n")) return false;
                    c->QuotaStackGenerations[i] = slot->Generation; c->QuotaStackAcquired++; changed = true;
                }
                live++; stacks += d.StackBytes - 2 * Page; contexts += d.ContextBytes;
            }
            if (c->QuotaStackAcquired < c->Reaped || c->QuotaStackAcquired - c->Reaped != live ||
                live != c->Process.ActiveThreads || end && (live != 0 || c->Selected != 0 || c->Threads.HandoffPending)) return false;
            if (live > c->QuotaStackPeakLive) c->QuotaStackPeakLive = live;
            if (stacks > c->QuotaStackPeakBytes) c->QuotaStackPeakBytes = stacks;
            if (contexts > c->QuotaContextPeakBytes) c->QuotaContextPeakBytes = contexts;
            changed |= c->QuotaStackLastReturned != c->Reaped || end;
            if (changed && (!LinuxText(state, "M16_016_STACK_LEASES acquired=") || !LinuxNumber(state, c->QuotaStackAcquired) ||
                !LinuxText(state, " returned=") || !LinuxNumber(state, c->Reaped) ||
                !LinuxText(state, " live=") || !LinuxNumber(state, live) ||
                !LinuxText(state, " stackBytes=") || !LinuxNumber(state, stacks) ||
                !LinuxText(state, " contextBytes=") || !LinuxNumber(state, contexts) ||
                !LinuxText(state, " peakLive=") || !LinuxNumber(state, c->QuotaStackPeakLive) ||
                !LinuxText(state, " peakStackBytes=") || !LinuxNumber(state, c->QuotaStackPeakBytes) ||
                !LinuxText(state, " peakContextBytes=") || !LinuxNumber(state, c->QuotaContextPeakBytes) ||
                !LinuxText(state, " end=") || !LinuxNumber(state, end ? 1U : 0U) || !LinuxText(state, "\r\n"))) return false;
            c->QuotaStackStarted = 1; c->QuotaStackLastReturned = c->Reaped;
            if (end) c->QuotaStackEnded = 1;
            return true;
        }
#endif

        // Valid only after real workers/IRQ/native references have drained.
        // Failure retains the pool and outstanding ownership for fail-closed
        // termination; it never authorizes restoring/unmapping a live resource.
        private static bool LinuxNativeRelease(LinuxNativeControl* c, ulong supervisorRsp)
        {
            if (c == null || c->Installed != 0 || c->Timer.IsPrepared || c->Process.ActiveThreads != 0 ||
                c->Threads.Current.IsValid || c->Threads.HandoffPending || !FirstExitOwnedArena.InMainStack(c->State->Arena, supervisorRsp, 8)) return false;
#if M16_014_PROBE
            if (LinuxQuotaPolicy(c->State) && !ObserveLinuxQuotaStackLeases(c, true)) return false;
#endif
            if (c->GuardAttached != 0)
            {
                if (!LinuxMemoryServices.DetachMutationGuard(&c->State->LinuxMemory, c->State, &LinuxThreadMemoryGuard)) return false;
                c->GuardAttached = 0;
            }
            if (c->ProcessAttached != 0)
            { if (!c->Process.Release(c->Key, 2)) return false; c->ProcessAttached = 0; }
            if (!LinuxNativeReleaseSignals(c)) return false;
            if (c->TableAttached != 0)
            { if (c->Threads.Release(c->Key, 2, supervisorRsp) != KernelThreadTable.Status.Ok) return false; c->TableAttached = 0; }
            c->Phase = 5; return true;
        }
    }
}
#endif
