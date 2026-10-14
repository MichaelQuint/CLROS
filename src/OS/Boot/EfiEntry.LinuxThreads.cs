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
        private const uint LinuxNativeWorkers = 4, LinuxNativeFutexes = 16;
        // Metadata lives only in the retained RW/NX pages 4..14. No user leaf
        // exposes this storage. One IRQ-off BSP owns the complete lifetime.
        private struct LinuxNativeControl
        {
            internal Storage* State;
            internal ulong Key, AliasSerial;
            internal uint Phase, Published, Capacity, Selected, Failed, Stopping, Reaped, Transfers;
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
            LinuxNativeControl* c = (LinuxNativeControl*)(state->CooperativePool + 4 * Page);
            if (c->State != state || c->Key != (state->CooperativePool ^ 0x4d31363030384f57UL) || c->Phase < 1 || c->Phase > 4) return null;
            ulong* cpu = stackalloc ulong[(int)NativeSwitch.ObservationWords];
            ((delegate* unmanaged<ulong*, void>)(state->Memory.Code + FirstExitCodeBundle.CooperativeObserveOffset))(cpu);
            // IRQ callbacks execute with SS=0 on the owned IST; ordinary native
            // callbacks use SS=16. Both require actual IF/DF clear and kernel CR3.
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
                !CooperativeAuditOriginalMappings(state) || !CooperativeAuditPool(state, false)) return false;
            state->CooperativePhase = 1; // One lifetime/key per boot, including failed preparation.
            ulong pool = state->CooperativePool, cursor = pool + 4 * Page, end = pool + 15 * Page;
            if (!LinuxNativeTake(ref cursor, end, (uint)sizeof(LinuxNativeControl), out byte* control)) return false;
            c = (LinuxNativeControl*)control;
            *c = default; c->State = state; c->Key = pool ^ 0x4d31363030384f57UL; c->Phase = 1; c->Capacity = state->LinuxView.Manifest.Threads;
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
                CopyFromUser = &LinuxThreadCopyInput, Write = &WriteDescriptorOutput };
            c->Plan = new() { Prepare = (ulong)(delegate* unmanaged<void*, uint, byte>)&LinuxNativeWorkerPrepare,
                Recover = (ulong)(delegate* unmanaged<void*, NativeContext*, byte>)&LinuxNativeRecover,
                TerminalCallback = (ulong)(delegate* unmanaged<void*, void>)&LinuxNativeTerminal,
                Stub = new() { AdmissionCallback = (ulong)(delegate* unmanaged<void*, NativeContext*, NativeContext*, byte>)&LinuxNativeTransfer } };
            c->Plan.Owner = new() { Initialized = true, Key = c->Key, CurrentKey = c->Key,
                Root = (ulong)state->Memory.Entries, ContextArena = pool + 4 * Page, ContextArenaBytes = 11 * Page,
                CodeStart = pool, CodeBytes = NativeProgram.Bytes, SwitchStackGuardStart = pool + 15 * Page, SwitchStackBytes = 18 * Page };
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
            c->Plan.Stub.CallbackContext = (ulong)c; c->Plan.Stub.CallbackBytes = (uint)sizeof(LinuxNativeControl);
            c->Plan.Stub.HelperFx = (ulong)helper; c->Plan.Stub.JumpSlot = (ulong)jump; c->Plan.Stub.RejectRip = pool + NativeProgram.Recovery;
            // Each entry starts at generation one, exactly as the fresh table.
            // Later reap/reuse must obtain its actual new generation before
            // rewriting that entry and publishing another Linux thread.
            for (uint i = 0; i < c->Capacity; i++)
            {
                ulong guard = pool + (33 + i * 6) * Page, entry = pool + NativeProgram.Entries + i * NativeProgram.WorkerStride;
                NativeContext* saved = (NativeContext*)(contexts + i * NativeContext.Bytes);
                if (NativeContext.Initialize(saved, NativeContext.Bytes, entry, guard + 5 * Page - 40, mask) != NativeContext.Status.Ok) return false;
                var d = new NativeSwitch.Descriptor { Identity = 0x100000001UL + i, CurrentIdentity = 0x100000001UL + i,
                    Context = saved, ContextBytes = NativeContext.Bytes, StackGuardStart = guard, StackBytes = 6 * Page,
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
                !X64TimerInterrupt.Emit((byte*)c->Handler.CodeAddress, X64TimerInterrupt.Bytes, c->Handler) || !LinuxNativeVerify(c)) return false;
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
            X64TimerInterrupt.Verify((byte*)c->Handler.CodeAddress, X64TimerInterrupt.Bytes, c->Handler);

        private static LinuxNativeControl* LinuxNativeControlOwner(void* context)
        {
            fixed (Storage* state = &s_storage)
            {
                if (state->CooperativePool == 0 || (ulong)context != state->CooperativePool + 4 * Page) return null;
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
                completion = view.PipePending ? c->Process.CompletePipeWait(c->Key, 2) : c->Process.CompleteFutexWait(c->Key, 2);
                completion = LinuxSyscallGateway.CompleteBlocked(ref c->Gates[index], completion);
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
                    FirmwareExited = true, OwnedBoundaryValidated = true, EnableStartup = true, EnableDescriptors = true, EnableThreads = true };
                LinuxSyscallAccess access = c->User;
                access.Startup = &DispatchLinuxStartup; access.Descriptors = &DispatchLinuxDescriptors; access.Threads = &DispatchLinuxNativeThreadService;
                if (LinuxSyscallGateway.Begin(ref c->Gates[index], admission, access) != LinuxSyscallGateStatus.Active) return LinuxNativeAbort(c, index);
            }
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
            c->Actions[index] = (uint)LinuxSyscallAction.Return; c->Entered[index] = 1; return 1;
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
                c->Selected == 0 || c->Selected > c->Capacity || !c->Process.CurrentImage(c->Key, 2, out var image) ||
                !X64LinuxUserTransition.CaptureUserImage((byte*)state->LinuxSyscallData, X64LinuxUserTransition.DataBytes,
                    image, X64LinuxUserTransition.UserImageBytes, c->Plan.Owner.ObservedMxcsrMask, c->User)) return 0;
            uint index = c->Selected - 1;
            if (c->Entered[index] != 1 || c->Threads.Current.Value != c->Plan.Bindings[index].Context.Identity) return 0;
            var result = LinuxSyscallGateway.Dispatch(ref c->Gates[index], ref *frame);
            image->Frame = *frame; image->Value = result.Value;
            // Keep the actual syscall record separate from a handler's new
            // register image. Full installation below aliases the gateway frame.
            LinuxSyscallFrame call = *frame;
            state->LinuxUserRoot = state->LinuxMemory.Root;
            byte* data = (byte*)state->LinuxSyscallData;
            var returning = result;
            if (result.Action is LinuxSyscallAction.Return or LinuxSyscallAction.RestoreUser)
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
                else { state->LinuxSyscallExitCode = result.ExitCode; c->Stopping = 1; }
            }
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
                LinuxText(state, " status=") && LinuxNumber(state, (uint)c->Gates[index].Status) && LinuxText(state, "\r\n");
            if (!logged || result.Action == LinuxSyscallAction.Rejected || returning.Action == LinuxSyscallAction.Rejected)
            { if (LinuxNativeAbort(c, index) != 2) return 0; }
            return logged && returning.Action == LinuxSyscallAction.RestoreUser ? (byte)1 : (byte)2;
        }

        private static byte LinuxNativeAbort(LinuxNativeControl* c, uint index)
        {
            // Gate rejection/call limits have not necessarily staged a process
            // exit. Use the real owner shutdown before CommitExit can run.
            c->Failed = c->Stopping = 1; c->State->LinuxSyscallExitCode = 99;
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
                var action = (LinuxSyscallAction)c->Actions[index];
                if (c->State->LinuxSyscallFaults != 0 || c->State->LinuxSyscallFailed != 0)
                {
                    if (LinuxNativeAbort(c, index) != 2) return 0;
                    action = (LinuxSyscallAction)c->Actions[index];
                }
                if (action == LinuxSyscallAction.Blocked)
                {
                    bool handoff;
                    bool committed = c->Slots[index].PipeOperation.IsActive
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
            if (c != null) _ = LinuxText(c->State, "TEST_FAIL m16-008 native-transfer\r\n");
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
            c->GuardAttached = 1; c->Phase = 2; return true;
        }

        // Valid only after real workers/IRQ/native references have drained.
        // Failure retains the pool and outstanding ownership for fail-closed
        // termination; it never authorizes restoring/unmapping a live resource.
        private static bool LinuxNativeRelease(LinuxNativeControl* c, ulong supervisorRsp)
        {
            if (c == null || c->Installed != 0 || c->Timer.IsPrepared || c->Process.ActiveThreads != 0 ||
                c->Threads.Current.IsValid || c->Threads.HandoffPending || !FirstExitOwnedArena.InMainStack(c->State->Arena, supervisorRsp, 8)) return false;
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
