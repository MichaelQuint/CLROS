#if M16_008_PROBE
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Memory;
using OS.Kernel.Platform;
using OS.Kernel.Threading;
using OS.Pal;
using NativeProgram = OS.Kernel.Compatibility.LinuxX64.LinuxThreadProgram;
using NativeContext = OS.Kernel.Threading.X64ThreadContext;
using NativeSwitch = OS.Kernel.Threading.ThreadContextSwitch;

namespace OS.Boot;

public static unsafe partial class EfiEntry
{
    private static partial class M03FirstExitAdapter
    {
        // Called only after common CPL3/memory/descriptor admission and APIC
        // setup. The original pool PTEs stay on the retained supervisor stack.
        // Failure with live workers, an unresolved native handoff or hardware
        // borrow retains the entire reservation; no cleanup success is inferred.
        private static bool RunLinuxNativeOwner(Storage* state)
        {
            fixed (Storage* retained = &s_storage) if (state != retained) return false;
            if (!CooperativeAuthority(state) || state->CooperativePhase != 0 || state->LinuxSyscallPhase != 4 ||
                state->LinuxMemoryPhase != 4 || state->LinuxDescriptorPhase != 4 || state->ApicPhase != 2 ||
                !state->LinuxSyscallPlan.ResumeUser || state->LinuxView.Manifest.Threads == 0 ||
                state->LinuxView.Manifest.Threads > LinuxNativeWorkers || !CooperativeAuditOriginalMappings(state) ||
                !CooperativeAuditPool(state, false)) return false;
            ulong* originals = stackalloc ulong[(int)CooperativePoolPages];
            for (uint i = 0; i < CooperativePoolPages; i++) originals[i] = 0;
            LinuxNativeControl* c = null;
            bool prepared = LinuxNativePrepare(state,
                (ulong)(delegate* unmanaged<void*, X64TimerInterrupt.Frame*, byte>)&LinuxNativeInterrupt, out c);
            bool captured = false, published = false, ran = false;
            if (prepared)
            {
                published = CooperativeSetPoolMappings(state, originals, true, out captured) && CooperativeAuditPool(state, true);
                if (published)
                {
                    c->Published = 1; state->CooperativePhase = 2;
                    ulong prior = ((delegate* unmanaged<ulong>)(state->CooperativePool + NativeProgram.IrqSaveDisable))();
                    if ((prior & 0x602) == 2 && LinuxNativeBind(c) && LinuxNativeInstallTimer(c))
                    {
                        if (!LinuxText(state, "M16_008_BOUND root=") || !LinuxNumber(state, c->Plan.Owner.Root) ||
                            !LinuxText(state, " pool=") || !LinuxNumber(state, state->CooperativePool) ||
                            !LinuxText(state, " workers=") || !LinuxNumber(state, c->Capacity) ||
                            !LinuxText(state, c->User.UserInterrupts ?
                                " code=16384 metadata=45056 userImage=728 timer=user-irq-and-idle clock=hpet-nanoseconds reuse=off-stack\r\n" :
                                " code=16384 metadata=45056 userImage=728 timer=idle-only clock=hpet-nanoseconds reuse=off-stack\r\n")) c->Failed = 1;
                        var plan = state->LinuxSyscallPlan;
                        LinuxSyscallFrame initial = new() { Rip = plan.UserRip, Rsp = plan.UserRsp,
                            Rflags = c->User.UserInterrupts ? 0x202UL : 2UL, FsBase = plan.UserFs, GsBase = plan.UserGs };
                        if (c->Failed == 0 && plan.ResumeUser && X64LinuxUserTransition.InitializeUserImage(c->Initial,
                            X64LinuxUserTransition.UserImageBytes, initial, c->Plan.Owner.ObservedMxcsrMask, c->User) &&
                            c->Process.Start(c->Key, 2, c->Initial, X64LinuxUserTransition.UserImageBytes, default, out var first) > 0 &&
                            first.Value == c->Plan.Bindings[0].Context.Identity)
                        { c->Phase = 3; ran = LinuxNativeRun(c); }
                    }
                }
            }
            // A returned but failed driver may still own a resumable worker.
            // Drain only through its genuine scheduled exit path. A failed
            // transfer with Current/Handoff retained never authorizes a guess.
            bool hardware = !published || LinuxNativeRestoreTimer(c);
            bool empty = c == null || c->Process.ActiveThreads == 0 && !c->Threads.Current.IsValid && !c->Threads.HandoffPending;
            if (!hardware || !empty) return false;
            uint reaped = c == null ? 0 : c->Reaped, transfers = c == null ? 0 : c->Transfers;
            ulong epochs = c == null ? 0 : c->Epoch;
            bool failed = c != null && (c->Failed != 0 || c->Process.Failed);
            if (published && !LinuxNativeRelease(c, LinuxNativeRsp(c))) return false;
            if (state->LinuxDescriptorPhase == 4)
            {
                if (!LinuxDescriptorServices.Audit(&state->LinuxDescriptors) || !LinuxDescriptorServices.End(&state->LinuxDescriptors) ||
                    !LinuxDescriptorReleaseChecked(state)) return false;
                state->LinuxDescriptorPhase = 6;
            }
            if (!LinuxNativeClearUserMemory(state)) return false;
            if (captured ? !CooperativeSetPoolMappings(state, originals, false, out _) : !CooperativeAuditPool(state, false)) return false;
            if (!CooperativeAuditOriginalMappings(state) || !CooperativeAuditPool(state, false)) return false;
            for (ulong i = 0; i < CooperativePoolPages * Page; i++) ((byte*)state->CooperativePool)[i] = 0;
            for (ulong i = 0; i < CooperativePoolPages * Page; i++) if (((byte*)state->CooperativePool)[i] != 0) return false;
            for (uint i = 0; i < 64; i++) state->LinuxRandomRetained.Data[i] = 0;
            if (!LinuxMemoryServices.Audit(&state->LinuxMemory) || !FirstExitOwnedArena.ValidateCanaries(state->Arena)) return false;
            state->CooperativePhase = state->LinuxSyscallPhase = state->LinuxMemoryPhase = 6;
            bool logged = LinuxText(state, "M16_008_OWNER_END reaped=") && LinuxNumber(state, reaped) &&
                LinuxText(state, " transfers=") && LinuxNumber(state, transfers) && LinuxText(state, " irqs=") && LinuxNumber(state, epochs) &&
                LinuxText(state, " exit=") && LinuxNumber(state, state->LinuxSyscallExitCode) &&
                LinuxText(state, " userReserved=") && LinuxNumber(state, state->LinuxMemory.ReservedBytes) &&
                LinuxText(state, " userBacked=") && LinuxNumber(state, state->LinuxMemory.BackedPages) &&
                LinuxText(state, " process=drained timer=restored descriptors=closed guard=detached pool=scrubbed initialStack=zero reservation=retained\r\n");
            return ran && logged && !failed && state->LinuxSyscallFailed == 0 && state->LinuxSyscallFaults == 0;
        }

        // The caller has restored hardware, confirmed every supervisor handoff,
        // released the empty process/table and closed descriptors. No user root
        // or borrowed word/buffer may remain live. Reuse the existing atomic
        // unmap/scrub path; keep the physical/table/firmware reservations owned.
        // Any failed admission/audit withholds the terminal cleanup witness.
        private static bool LinuxNativeClearUserMemory(Storage* state)
        {
            fixed (Storage* retained = &s_storage) if (state != retained) return false;
            if (state->LinuxSyscallPhase != 4 || state->LinuxMemoryPhase != 4 || state->LinuxDescriptorPhase != 6 ||
                state->LinuxMemory.HasMutationGuard || !LinuxMemoryServices.Audit(&state->LinuxMemory)) return false;
            var readRoot = (delegate* unmanaged<ulong>)(state->Memory.Code + FirstExitCodeBundle.LinuxReadBackOffset +
                X64LinuxElfReadBackBridge.ReadCr3Offset);
            if (state->LinuxDispatchRoot == 0 || readRoot() != state->LinuxDispatchRoot ||
                state->LinuxDispatchRoot == state->LinuxMemory.Root.Root) return false;
            ulong imageBytes = state->LinuxImagePlan.ImageBytes, stackBytes = state->LinuxInitialStack.StackBytes;
            ulong total = state->LinuxImagePages * Page, poolBytes = LinuxMemoryServices.MaximumPoolPages * Page;
            if (state->LinuxImagePages == 0 || state->LinuxImagePages > 4096 || imageBytes == 0 ||
                (imageBytes & (Page - 1)) != 0 || imageBytes >= total || stackBytes != total - imageBytes ||
                stackBytes != state->LinuxView.Manifest.StackBytes || state->LinuxImageBacking < Page ||
                state->LinuxImageBacking > 0x100000000UL - total || state->LinuxMemoryPool < Page ||
                state->LinuxMemoryPool > 0x100000000UL - poolBytes) return false;
            // Bind the scrub pointer to the audited static user-stack mapping,
            // rather than trusting only the independently retained image extent.
            var memory = state->LinuxMemory.Memory;
            if ((ulong)memory.Pool != state->LinuxMemoryPool || memory.PoolPhysical != state->LinuxMemoryPool ||
                memory.PoolPages != LinuxMemoryServices.MaximumPoolPages) return false;
            bool stackOwned = false;
            for (uint i = 0; i < memory.StaticCount; i++)
            {
                var range = memory.StaticRanges[i];
                if (range.User && range.VirtualStart == 0x70000000UL - stackBytes &&
                    range.PhysicalStart == state->LinuxImageBacking + imageBytes && range.Pages == stackBytes / Page &&
                    range.Protection == (PageProtection.Read | PageProtection.Write) && range.Cache == PageCachePolicy.WriteBack)
                    stackOwned = true;
            }
            if (!stackOwned) return false;
            if (LinuxMemoryServices.Unmap(&state->LinuxMemory, LinuxMemoryServices.DynamicStart,
                LinuxSyscallGateway.UserEnd - LinuxMemoryServices.DynamicStart) != 0 ||
                state->LinuxMemory.ReservedBytes != 0 || state->LinuxMemory.BackedPages != 0 ||
                !LinuxMemoryServices.Audit(&state->LinuxMemory)) return false;
            state->LinuxUserRoot = state->LinuxMemory.Root;
            byte* stack = (byte*)(state->LinuxImageBacking + imageBytes);
            for (ulong i = 0; i < stackBytes; i++) stack[i] = 0;
            for (ulong i = 0; i < stackBytes; i++) if (stack[i] != 0) return false;
            for (ulong i = 0; i < poolBytes; i++) if (((byte*)state->LinuxMemoryPool)[i] != 0) return false;
            return true;
        }

        private static ulong LinuxNativeRsp(LinuxNativeControl* c) =>
            ((delegate* unmanaged<ulong>)(c->State->CooperativePool + NativeProgram.StackPointer))();

        // Every iteration is a real dispatch, off-stack reap or bounded hardware
        // idle. Empty untimed pipe/futex waits cannot spin: shutdown wakes and
        // drains them. Explicit M16-009 user IRQs return through the restored
        // kernel CALL handoff before this driver can dispatch another worker.
        private static bool LinuxNativeRun(LinuxNativeControl* c)
        {
            for (uint step = 0; step < 512; step++)
            {
                if (!LinuxNativeObserve(c) || !LinuxNativeVerify(c) || !LinuxNativeSavedCode(c) || !CooperativeAuditPool(c->State, true)) return false;
                if (!LinuxNativeReapExited(c)) return false;
                if (c->Process.ActiveThreads == 0) return c->Failed == 0 && !c->Process.Failed;
                if ((c->Failed != 0 || c->Process.Failed || c->State->LinuxSyscallFaults != 0 || c->State->LinuxSyscallFailed != 0) &&
                    !LinuxNativeStop(c)) return false;
                if (c->Threads.Flush(c->Key, 2) != KernelThreadTable.Status.Ok) return false;
                if (c->Threads.ReadyCount != 0)
                { if (!LinuxNativeDispatch(c)) return false; }
                else if (c->Threads.DeadlineCount != 0 && c->Stopping == 0)
                {
                    if (!LinuxNativeWaitOne(c) && (!LinuxNativeRestoreTimer(c) || !LinuxNativeStop(c))) return false;
                }
                else if (!LinuxNativeStop(c)) return false;
            }
            // The bounded driver itself has made no native transfer here.
            // Cancellation still needs actual continuations, so use a separate
            // finite drain budget rather than freeing still-live resources.
            if (!LinuxNativeStop(c)) return false;
            for (uint step = 0; step < LinuxNativeWorkers * 3; step++)
            {
                if (!LinuxNativeReapExited(c)) return false;
                if (c->Process.ActiveThreads == 0) return false;
                if (c->Threads.Flush(c->Key, 2) != KernelThreadTable.Status.Ok || c->Threads.ReadyCount == 0 || !LinuxNativeDispatch(c)) return false;
            }
            return false;
        }

        private static bool LinuxNativeStop(LinuxNativeControl* c)
        {
            c->Failed = c->Stopping = 1; c->State->LinuxSyscallExitCode = 99;
            return !c->Threads.Current.IsValid && !c->Threads.HandoffPending && c->Process.Shutdown(c->Key, 2, 99);
        }

        private static bool LinuxNativeDispatch(LinuxNativeControl* c)
        {
            if (c->Selected != 0 || !LinuxNativeObserve(c) || !LinuxNativeVerify(c) || !LinuxNativeSavedCode(c) ||
                !CooperativeAuditPool(c->State, true) || c->Timer.IsPrepared) return false;
            var next = c->Threads.Dispatch(c->Key, 2);
            uint index = (uint)next.Identity.Value - 1;
            if (!next.Succeeded || next.State != KernelThreadTable.State.Running || index >= c->Capacity ||
                next.Identity.Value != c->Plan.Bindings[index].Context.Identity) return false;
            c->Selected = index + 1;
            bool returned = ((delegate* unmanaged<uint>)(c->State->CooperativePool + index * NativeProgram.DriverStride))() == 1;
            if (!returned || !CooperativeEqualContexts(c->Plan.Supervisor.Context, c->Plan.Witness) || !LinuxNativeObserve(c) ||
                !LinuxNativeVerify(c) || !LinuxNativeSavedCode(c) || !CooperativeAuditPool(c->State, true)) return false;
            // Confirm even a staged failure, after actual return/restoration;
            // its cancellation/exit resources still require off-stack cleanup.
            if (c->Threads.ConfirmSupervisor(c->Key, 2, LinuxNativeRsp(c)) != KernelThreadTable.Status.Ok) return false;
            c->Selected = 0;
            if (!LinuxText(c->State, "M16_008_SUPERVISOR identity=") || !LinuxNumber(c->State, next.Identity.Value) ||
                !LinuxText(c->State, " action=") || !LinuxNumber(c->State, c->Actions[index]) ||
                !LinuxText(c->State, " transfers=") || !LinuxNumber(c->State, c->Transfers) ||
                !LinuxText(c->State, " ready=") || !LinuxNumber(c->State, c->Threads.ReadyCount) ||
                !LinuxText(c->State, " active=") || !LinuxNumber(c->State, c->Process.ActiveThreads) || !LinuxText(c->State, "\r\n")) c->Failed = 1;
            return !c->Threads.Current.IsValid && !c->Threads.HandoffPending;
        }

        private static bool LinuxNativeSavedCode(LinuxNativeControl* c)
        {
            if (c->CodeCopy == null) return false;
            for (uint i = 0; i < NativeProgram.Bytes; i++) if (((byte*)c->State->CooperativePool)[i] != c->CodeCopy[i]) return false;
            return true;
        }

        // Reap scrubs the actual context and usable stack only after supervisor
        // confirmation. Code bytes have an independent retained snapshot during
        // that invalid-descriptor interval. Reinitialize from the table's real
        // advanced generation before exposing another prepared clone worker.
        private static bool LinuxNativeReapExited(LinuxNativeControl* c)
        {
            if (c->Selected != 0 || c->Timer.IsPrepared || c->Threads.Current.IsValid || c->Threads.HandoffPending ||
                !LinuxNativeObserve(c) || !LinuxNativeVerify(c) || !LinuxNativeSavedCode(c)) return false;
            for (uint index = 0; index < c->Capacity; index++)
            {
                var old = c->Plan.Bindings[index].Context;
                var state = c->Threads.Inspect(c->Key, 2, new(old.Identity));
                if (state.Code == KernelThreadTable.Status.StaleIdentity)
                {
                    // Only this exact prepared vacant generation may be skipped.
                    if (c->KernelSlots[index].State != KernelThreadTable.State.Empty ||
                        c->KernelSlots[index].Generation != old.Identity >> 32 || c->Slots[index].Tid != 0 || c->Slots[index].Kernel.IsValid) return false;
                    continue;
                }
                if (!state.Succeeded) return false;
                if (state.State != KernelThreadTable.State.Exited) continue;
                if (!c->Process.Reap(c->Key, 2, new(old.Identity), LinuxNativeRsp(c), out var released) ||
                    released.Context != old.Context || released.ContextBytes != old.ContextBytes ||
                    released.StackGuardStart != old.StackGuardStart || released.StackBytes != old.StackBytes) return false;
                for (uint i = 0; i < released.ContextBytes; i++) if (((byte*)released.Context)[i] != 0) return false;
                for (ulong i = Page; i < released.StackBytes - Page; i++) if (((byte*)released.StackGuardStart)[i] != 0) return false;
                for (uint i = 0; i < sizeof(LinuxThreadServices.Slot); i++) if (((byte*)(c->Slots + index))[i] != 0) return false;
                if (!LinuxNativeSavedCode(c) || !CooperativeAuditPool(c->State, true) ||
                    c->KernelSlots[index].State != KernelThreadTable.State.Empty || c->KernelSlots[index].Generation == 0 ||
                    c->KernelSlots[index].Generation != (old.Identity >> 32) + 1) return false;
                ulong identity = ((ulong)c->KernelSlots[index].Generation << 32) | (index + 1UL);
                old.Identity = old.CurrentIdentity = identity; old.Kind = NativeSwitch.ResumeKind.Entry;
                old.ResumeRip = c->State->CooperativePool + NativeProgram.Entries + index * NativeProgram.WorkerStride;
                if (NativeContext.Initialize(old.Context, old.ContextBytes, old.ResumeRip,
                    old.StackGuardStart + old.StackBytes - Page - 40, c->Plan.Owner.ObservedMxcsrMask) != NativeContext.Status.Ok) return false;
                c->Plan.Bindings[index].Context = c->Plan.Bindings[index].Entry.Initial = old;
                c->Gates[index] = default; c->Actions[index] = c->Entered[index] = 0;
                if (!LinuxNativeObserve(c) || !LinuxNativeRewriteEntry(c, index)) return false;
                c->Workers[index] = new() { Initial = old, Entry = c->Plan.Bindings[index].Entry.Entry };
                c->Reaped++;
                if (!LinuxText(c->State, "M16_008_REAP old=") || !LinuxNumber(c->State, state.Identity.Value) ||
                    !LinuxText(c->State, " next=") || !LinuxNumber(c->State, identity) ||
                    !LinuxText(c->State, " context=zero stack=zero slot=zero\r\n")) c->Failed = 1;
            }
            return LinuxNativeVerify(c) && LinuxNativeSavedCode(c);
        }

        // No native code in page0 is active here. IF is clear, no timer/user/
        // worker transfer is live, and all other pages retain their exact PTEs.
        // Publish RW/NX, emit only the 256-byte entry, then restore RX even after
        // emission failure. Never introduce a writable executable mapping.
        private static bool LinuxNativeRewriteEntry(LinuxNativeControl* c, uint index)
        {
            Storage* state = c->State;
            if (index >= c->Capacity || c->Selected != 0 || c->Timer.IsPrepared || c->Threads.Current.IsValid || c->Threads.HandoffPending ||
                !LinuxNativeObserve(c) || !LinuxNativeSavedCode(c) || !CooperativeAuditPool(state, true) ||
                !CooperativeFindLeaf(state, state->CooperativePool, out ulong* leaf)) return false;
            ulong original = *leaf;
            var writable = X64PageTableEntry.TryReencodeLeafProtection(X64PageTableLevel.Pte, original, PageProtection.Read | PageProtection.Write,
                X64HardwareStatePolicy.Preserve, state->Memory.Tables.Preconditions);
            if (!writable.Succeeded) return false;
            var update = (delegate* unmanaged<ulong*, ulong, ulong, ulong, uint>)(state->Memory.Code + FirstExitCodeBundle.CooperativePublishOffset);
            bool writeReady = update(leaf, *leaf, writable.Entry.RawValue, (ulong)state->Memory.Entries) == 1 &&
                (*leaf & ~0x60UL) == (writable.Entry.RawValue & ~0x60UL);
            uint offset = NativeProgram.Entries + index * NativeProgram.WorkerStride;
            bool emitted = writeReady && KernelThreadEntry.Emit((byte*)state->CooperativePool + offset, KernelThreadEntry.Bytes, c->Plan.Bindings[index].Entry);
            bool restored = update(leaf, *leaf, original, (ulong)state->Memory.Entries) == 1 &&
                (*leaf & ~0x60UL) == (original & ~0x60UL) && CooperativeAuditPool(state, true);
            if (!emitted || !restored || !LinuxNativeVerify(c)) return false;
            for (uint i = 0; i < NativeProgram.Bytes; i++)
                if ((i < offset || i >= offset + KernelThreadEntry.Bytes) && ((byte*)state->CooperativePool)[i] != c->CodeCopy[i]) return false;
            for (uint i = offset; i < offset + KernelThreadEntry.Bytes; i++) c->CodeCopy[i] = ((byte*)state->CooperativePool)[i];
            return LinuxNativeSavedCode(c);
        }
    }
}
#endif
