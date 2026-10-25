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
#if M16_014_PROBE
            bool serviceGcConfigured = state->LinuxServices.GcBudget.Succeeded;
#endif
            if (!CooperativeAuthority(state) || state->CooperativePhase != 0 || state->LinuxSyscallPhase != 4 ||
                state->LinuxMemoryPhase != 4 || state->LinuxDescriptorPhase != 4 || state->ApicPhase != 2 ||
                !state->LinuxSyscallPlan.ResumeUser || state->LinuxView.Manifest.Threads == 0 ||
                state->LinuxView.Manifest.Threads > LinuxNativeWorkers || !CooperativeAuditOriginalMappings(state) ||
                !CooperativeAuditPool(state, false)) return false;
#if M16_027_PROBE
            if (!RunNativeWritableDiskBootstrap(state)) return false;
#elif M16_025_PROBE
            if (!RunNativeDiskBootstrap(state))
            {
                _ = RetireNativeDisk(state);
                return false; // Failed retirement keeps the outer owners retained.
            }
#endif
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
                            !LinuxText(state, " code=") || !LinuxNumber(state, NativeProgram.Bytes) ||
                            !LinuxText(state, " metadata=") || !LinuxNumber(state, NativeProgram.MetadataPages * Page) ||
                            !LinuxText(state, " userImage=") || !LinuxNumber(state, X64LinuxUserTransition.UserImageBytes) ||
                            !LinuxText(state, c->User.UserInterrupts ?
                                " timer=user-irq-and-idle clock=hpet-nanoseconds reuse=off-stack\r\n" :
                                " timer=idle-only clock=hpet-nanoseconds reuse=off-stack\r\n")) c->Failed = 1;
#if M16_014_PROBE
                        if (!(LinuxText(state, "TEST_START m16.014\r\nM16_014_POLICY started=") && LinuxNumber(state, c->Budget.StartedNanoseconds) &&
                            LinuxText(state, " callsPerTurn=") && LinuxNumber(state, LinuxSyscallGateway.MaximumThreadCalls) &&
                            LinuxText(state, " progressNanos=") && LinuxNumber(state, LinuxThreadExecutionBudget.ServiceProgressNanoseconds) &&
                            LinuxText(state, " elapsedLimit=none counters=reject-overflow workers=individual cancellation=owned\r\n"))) c->Failed = 1;
#else
                        if (c->Budget.RequiresClock && !(LinuxText(state, "M16_008_BUDGET started=") && LinuxNumber(state, c->Budget.StartedNanoseconds) &&
                            LinuxText(state, " epochs=") && LinuxNumber(state, c->Budget.MaximumEpochs) &&
                            LinuxText(state, " steps=") && LinuxNumber(state, c->Budget.MaximumSteps) &&
                            LinuxText(state, " nanos=") && LinuxNumber(state, c->Budget.ElapsedLimitNanoseconds) && LinuxText(state, "\r\n"))) c->Failed = 1;
#endif
                        var plan = state->LinuxSyscallPlan;
                        LinuxSyscallFrame initial = new() { Rip = plan.UserRip, Rsp = plan.UserRsp,
                            Rflags = c->User.UserInterrupts ? 0x202UL : 2UL, FsBase = plan.UserFs, GsBase = plan.UserGs };
#if M16_027_PROBE
                        OSHostHandle first = default;
                        long startResult = -1;
                        bool startReady = c->Failed == 0 && plan.ResumeUser && X64LinuxUserTransition.InitializeUserImage(c->Initial,
                            X64LinuxUserTransition.UserImageBytes, initial, c->Plan.Owner.ObservedMxcsrMask, c->User);
                        if (startReady)
                        {
                            startReady = BeginNativeWritableSpan(state, 1, out ulong startClock);
                            if (startReady)
                            {
                                startResult = c->Process.Start(c->Key, 2, c->Initial, X64LinuxUserTransition.UserImageBytes, default, out first);
                                startReady = EndNativeWritableSpan(state, 1, startClock, startResult > 0);
                            }
                        }
                        if (startReady && startResult > 0 && first.Value == c->Plan.Bindings[0].Context.Identity)
#else
                        if (c->Failed == 0 && plan.ResumeUser && X64LinuxUserTransition.InitializeUserImage(c->Initial,
                            X64LinuxUserTransition.UserImageBytes, initial, c->Plan.Owner.ObservedMxcsrMask, c->User) &&
                            c->Process.Start(c->Key, 2, c->Initial, X64LinuxUserTransition.UserImageBytes, default, out var first) > 0 &&
                            first.Value == c->Plan.Bindings[0].Context.Identity)
#endif
                        {
#if M10_017_PROBE || M10_018_PROBE
                            c->StaticMain = first;
                            if (!c->StaticApp.Begin(first.Value, OS.Kernel.Diagnostics.Probes.EnableM10018FullProbe) || !LinuxText(state, LinuxStaticStart))
                                return LinuxNativeCleanupRejected(state, 12);
#endif
#if M10_018_FULL_PROBE
                            if (!(LinuxText(state, "M10_018_POLICY profile=DiskStaticFullV1 version=") && LinuxNumber(state, DiskStaticExecutionPolicy.Version) &&
                                LinuxText(state, " calls=") && LinuxNumber(state, DiskStaticExecutionPolicy.MaximumCalls) &&
                                LinuxText(state, " requestNanos=") && LinuxNumber(state, DiskStaticExecutionPolicy.RequestDelayNanoseconds) &&
                                LinuxText(state, " graceNanos=") && LinuxNumber(state, DiskStaticExecutionPolicy.GraceNanoseconds) &&
                                LinuxText(state, " runtimeNanos=") && LinuxNumber(state, DiskStaticExecutionPolicy.RuntimeNanoseconds) &&
                                LinuxText(state, " renewal=none\r\n"))) return LinuxNativeCleanupRejected(state, 12);
#endif
                            c->Phase = 3; ran = LinuxNativeRun(c);
                        }
                    }
                }
            }
#if M10_004_PROBE && !M16_014_PROBE && !M10_017_PROBE && !M10_018_PROBE
            // Keep this supervisor stack, original PTEs, mappings and live owners.
            // A continuing web driver has no successful guest return or teardown.
            return LinuxWebRetainFailure(state, c, 1);
#else
            // A returned but failed driver may still own a resumable worker.
            // Drain only through its genuine scheduled exit path. A failed
            // transfer with Current/Handoff retained never authorizes a guess.
            bool hardware = !published || LinuxNativeRestoreTimer(c);
            bool empty = c == null || c->Process.ActiveThreads == 0 && !c->Threads.Current.IsValid && !c->Threads.HandoffPending;
            if (!hardware || !empty) return LinuxNativeCleanupRejected(state, 1);
            uint reaped = c == null ? 0 : c->Reaped, transfers = c == null ? 0 : c->Transfers;
            ulong epochs = c == null ? 0 : c->Epoch;
            bool runtimeBudget = c != null && c->Budget.RequiresClock;
            ulong observedNanos = c == null ? 0 : c->Budget.ObservedNanoseconds;
#if M16_014_PROBE
            ulong serviceCalls = c == null ? 0 : c->ServicePeakCalls, serviceIdentity = c == null ? 0 : c->ServicePeakIdentity;
            ulong serviceFinished = c == null ? 0 : c->ServiceFinished, serviceStarted = c == null ? 0 : c->Budget.StartedNanoseconds;
            bool serviceStopped = c != null && c->ServiceReady == 1 && c->ServiceShutdown == 1 && c->Budget.CancellationRequested;
            bool quotaStopped = c != null && LinuxQuotaPolicy(state) && state->LinuxQuota == LinuxQuotaDisposition.Terminating &&
                c->Failed == 1 && c->Stopping == 1 && !c->Process.Failed && c->ServiceShutdown == 0 &&
                c->Budget.CancellationRequested && state->LinuxSyscallExitCode == 99 &&
                state->LinuxSyscallFailed == 0 && state->LinuxSyscallFaults == 0 && reaped != 0 &&
                serviceFinished >= serviceStarted;
#endif
            bool failed = c != null && (c->Failed != 0 || c->Process.Failed);
#if M10_017_PROBE || M10_018_PROBE
            var staticApp = c == null ? default : c->StaticApp;
            bool staticCompleted = staticApp.GracefulExit && state->LinuxSyscallExitCode == 0;
#endif
#if M16_027_PROBE
            if (!QuiesceNativeWritableDisk(state, c)) return LinuxNativeCleanupRejected(state, 14);
#elif M16_025_PROBE
            ulong diskStarted = c == null ? 0 : c->Budget.StartedNanoseconds;
            if (!RetireNativeDiskAttachment(state, c) || !ObserveNativeDiskExitSentinel(state, c)) return LinuxNativeCleanupRejected(state, 13);
#endif
#if M16_027_PROBE
            bool released = !published || LinuxNativeRelease(c, LinuxNativeRsp(c));
            bool releaseSampled = SampleNativeWritableDisk(state);
            if (!released || !releaseSampled) return LinuxNativeCleanupRejected(state, 2);
#else
            if (published && !LinuxNativeRelease(c, LinuxNativeRsp(c))) return LinuxNativeCleanupRejected(state, 2);
#endif
#if M16_025_PROBE && !M16_027_PROBE
            if (!ObserveNativeDiskExitReleased(state, c)) return LinuxNativeCleanupRejected(state, 13);
#endif
            if (published && !LinuxNativeBeginTeardown(state, c)) return LinuxNativeCleanupRejected(state, 3);
            if (state->LinuxDescriptorPhase == 4)
            {
#if CLROS_LINUX_SOCKETS
                if (!DrainLinuxSocketNetwork(state, !ran || failed)) return LinuxNativeCleanupRejected(state, 11);
#endif
#if M16_027_PROBE
                bool descriptorAudit = LinuxDescriptorServices.Audit(&state->LinuxDescriptors);
                bool auditSampled = SampleNativeWritableDisk(state);
                if (!descriptorAudit || !auditSampled) return LinuxNativeCleanupRejected(state, 4);
                bool descriptorEnd = LinuxDescriptorServices.End(&state->LinuxDescriptors);
                bool endSampled = SampleNativeWritableDisk(state);
                if (!descriptorEnd || !endSampled || !LinuxDescriptorReleaseChecked(state)) return LinuxNativeCleanupRejected(state, 4);
#else
                if (!LinuxDescriptorServices.Audit(&state->LinuxDescriptors) || !LinuxDescriptorServices.End(&state->LinuxDescriptors) ||
                    !LinuxDescriptorReleaseChecked(state)) return LinuxNativeCleanupRejected(state, 4);
#endif
                state->LinuxDescriptorPhase = 6;
            }
#if M16_027_PROBE
            if (!RetireNativeWritableDiskAttachment(state, c)) return LinuxNativeCleanupRejected(state, 14);
#endif
            if (!LinuxNativeClearUserMemory(state)) return LinuxNativeCleanupRejected(state, 5);
#if M16_025_PROBE && !M16_027_PROBE
            if (!ObserveNativeDiskExitMemoryRestored(state)) return LinuxNativeCleanupRejected(state, 13);
#endif
            if (captured ? !CooperativeSetPoolMappings(state, originals, false, out _) : !CooperativeAuditPool(state, false)) return LinuxNativeCleanupRejected(state, 6);
            if (!CooperativeAuditOriginalMappings(state) || !CooperativeAuditPool(state, false)) return LinuxNativeCleanupRejected(state, 7);
            for (ulong i = 0; i < CooperativePoolPages * Page; i++) ((byte*)state->CooperativePool)[i] = 0;
            for (ulong i = 0; i < CooperativePoolPages * Page; i++) if (((byte*)state->CooperativePool)[i] != 0) return LinuxNativeCleanupRejected(state, 8);
            if (!ClearLinuxRandom(state)) return LinuxNativeCleanupRejected(state, 9);
            if (!LinuxMemoryServices.Audit(&state->LinuxMemory) || !FirstExitOwnedArena.ValidateCanaries(state->Arena)) return LinuxNativeCleanupRejected(state, 10);
            state->CooperativePhase = state->LinuxSyscallPhase = state->LinuxMemoryPhase = 6;
#if M16_025_PROBE && !M16_027_PROBE
            if (!ObserveNativeDiskExitCleanupEnd(state, diskStarted)) return LinuxNativeCleanupRejected(state, 13);
#endif
#if M16_014_PROBE
            if (serviceGcConfigured && (!LinuxText(state, "M16_016_MEMORY poolPages=") ||
                !LinuxNumber(state, LinuxMemoryPoolPages(state)) ||
                !LinuxText(state, " peakPages=") || !LinuxNumber(state, state->LinuxMemory.PeakBackedPages) ||
                !LinuxText(state, " endPages=") || !LinuxNumber(state, state->LinuxMemory.BackedPages) ||
                !LinuxText(state, "\r\n"))) return false;
#endif
            bool logged = (!runtimeBudget || LinuxText(state, "M16_008_BUDGET_END nanos=") && LinuxNumber(state, observedNanos) && LinuxText(state, "\r\n")) &&
                LinuxText(state, "M16_008_OWNER_END reaped=") && LinuxNumber(state, reaped) &&
                LinuxText(state, " transfers=") && LinuxNumber(state, transfers) && LinuxText(state, " irqs=") && LinuxNumber(state, epochs) &&
                LinuxText(state, " exit=") && LinuxNumber(state, state->LinuxSyscallExitCode) &&
                LinuxText(state, " userReserved=") && LinuxNumber(state, state->LinuxMemory.ReservedBytes) &&
                LinuxText(state, " userBacked=") && LinuxNumber(state, state->LinuxMemory.BackedPages) &&
                LinuxText(state, " process=drained timer=restored descriptors=closed guard=detached pool=scrubbed initialStack=zero reservation=retained\r\n");
            bool success = ran && logged && !failed && state->LinuxSyscallFailed == 0 && state->LinuxSyscallFaults == 0;
#if M16_025_PROBE && !M10_018_PROBE
            success &= state->LinuxSyscallExitCode == 42;
#endif
#if M16_027_PROBE
            success = success && RecordNativeWritableUserCleanup(state, reaped, transfers, epochs);
#endif
#if M10_017_PROBE || M10_018_PROBE
            success &= staticCompleted;
            if (!LinuxText(state, LinuxStaticTag + "_OWNER_END main=") || !LinuxNumber(state, staticApp.MainGeneration) ||
                !LinuxText(state, " ready=") || !LinuxNumber(state, staticApp.ReadyNanoseconds) ||
                !LinuxText(state, " requested=") || !LinuxNumber(state, staticApp.RequestedNanoseconds) ||
                !LinuxText(state, " exited=") || !LinuxNumber(state, staticApp.ExitedNanoseconds) ||
                !LinuxText(state, " reaped=") || !LinuxNumber(state, reaped) ||
                !LinuxText(state, success ? " app=exit0 process=drained timer=restored descriptors=closed mappings=restored pool=scrubbed nic=pending\r\n" :
                    " app=failed cleanup=unqualified\r\n")) return false;
#endif
#if M16_014_PROBE
            success = success && serviceStopped && serviceCalls > LinuxSyscallGateway.MaximumThreadCalls &&
                serviceFinished >= serviceStarted && serviceFinished - serviceStarted > LinuxThreadExecutionBudget.RuntimeNanoseconds;
            if (!LinuxText(state, "M16_014_END identity=") || !LinuxNumber(state, serviceIdentity) ||
                !LinuxText(state, " calls=") || !LinuxNumber(state, serviceCalls) ||
                !LinuxText(state, " started=") || !LinuxNumber(state, serviceStarted) ||
                !LinuxText(state, " nanos=") || !LinuxNumber(state, serviceFinished) ||
                !LinuxText(state, " reaped=") || !LinuxNumber(state, reaped) ||
                !LinuxText(state, " process=drained mappings=restored pool=scrubbed random=zero reservation=retained\r\n") ||
                !LinuxText(state, quotaStopped ? "M16_016_QUOTA_PROCESS_FAILURE exit=99 cause=physical-backing\r\n" :
                    success ? "TEST_PASS m16.014\r\n" : "TEST_FAIL m16.014 process-or-policy\r\n")) return false;
            if (quotaStopped)
            {
                if (success || !logged || state->LinuxQuota != LinuxQuotaDisposition.Terminating ||
                    !LinuxText(state, "M16_016_QUOTA_NATIVE process=drained timer=restored descriptors=closed guard=detached mappings=restored pool=scrubbed initialStack=zero random=zero reservation=retained\r\n"))
                    return false;
                state->LinuxQuota = LinuxQuotaDisposition.NativeCleaned;
            }
#endif
            return success;
#endif
        }

        // Release ends live process authority. Authenticate that exact retained
        // control and restored hardware borrows before ending active pool checks;
        // the common code/CPU/leaf checks still govern every teardown mapping.
        private static bool LinuxNativeBeginTeardown(Storage* state, LinuxNativeControl* c)
        {
            if (c == null || c != (LinuxNativeControl*)(state->CooperativePool + NativeProgram.MetadataPage * Page) ||
                c->State != state || c->Phase != 5 || state->CooperativePhase != 2 || state->LinuxSyscallPhase != 4 ||
                c->Installed != 0 || c->Selected != 0 || c->GuardAttached != 0 || c->ProcessAttached != 0 ||
                c->TableAttached != 0 || c->SignalsInitialized != 0 || c->Timer.IsPrepared ||
                c->TimerDeadlines.IsInitialized || c->TimerDeadlines.HasPending || c->TimerDeadlines.Count != 0 ||
                c->Process.ActiveThreads != 0 || c->Threads.Current.IsValid || c->Threads.HandoffPending ||
                state->LinuxMemory.HasMutationGuard ||
                *(ulong*)(state->LinuxSyscallData + X64LinuxUserTransition.PhaseOffset) != 2 ||
                *(ulong*)(state->Arena.Descriptors->Tss + 4) != state->Arena.StackTop ||
                *(ulong*)(state->Arena.Descriptors->Tss + 60) != 0) return false;
#if M16_009_PROBE
            if (c->UserTimer != 0 || c->UserDrain != 0 || c->UserAcknowledged != 0 || c->UserPreempted != 0) return false;
#endif
            state->CooperativePhase = 5; return true;
        }

        private static bool LinuxNativeCleanupRejected(Storage* state, uint stage)
        {
            _ = LinuxText(state, "M16_008_CLEANUP_REJECT stage=") && LinuxNumber(state, stage) && LinuxText(state, "\r\n");
            return false;
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
            ulong total = state->LinuxImagePages * Page, poolBytes = LinuxMemoryPoolPages(state) * Page;
            if (state->LinuxImagePages == 0 || state->LinuxImagePages > 4096 || imageBytes == 0 ||
                (imageBytes & (Page - 1)) != 0 || imageBytes >= total || stackBytes != total - imageBytes ||
                stackBytes != state->LinuxView.Manifest.StackBytes || state->LinuxImageBacking < Page ||
                state->LinuxImageBacking > 0x100000000UL - total || state->LinuxMemoryPool < Page ||
                state->LinuxMemoryPool > 0x100000000UL - poolBytes) return false;
            // Bind the scrub pointer to the audited static user-stack mapping,
            // rather than trusting only the independently retained image extent.
            var memory = state->LinuxMemory.Memory;
            if ((ulong)memory.Pool != state->LinuxMemoryPool || memory.PoolPhysical != state->LinuxMemoryPool ||
                memory.PoolPages != LinuxMemoryPoolPages(state)) return false;
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
                LinuxMemoryServices.RestoreImageProtections(&state->LinuxMemory) != 0 || state->LinuxMemory.HasImageProtections ||
                state->LinuxMemory.ReservedBytes != 0 || state->LinuxMemory.BackedPages != 0 ||
                !LinuxMemoryServices.Audit(&state->LinuxMemory) ||
                !X64UserAddressSpace.Audit(memory.StaticRanges, memory.StaticCount, memory.Tables,
                    memory.RootPhysical, memory.TablePages, memory.Conditions, state->LinuxMemory.Root)) return false;
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
            for (uint step = 0; step < c->Budget.MaximumSteps; step++)
            {
#if M16_009_PROBE
                c->TimingValid = 0;
                if (c->Budget.RequiresClock && LinuxNativeTimingRead(c, &c->TimingStart)) c->TimingValid = 1;
#endif
                // ReapExited checks the independent code snapshot before any mutation.
                if (!LinuxNativeObserve(c) || !LinuxNativeVerify(c) || !CooperativeAuditPool(c->State, true)) return false;
                if (!LinuxNativeReapExited(c)) return false;
                if (c->Process.ActiveThreads == 0)
                {
#if M16_014_PROBE
                    ulong finished = 0;
                    delegate* unmanaged<void*, ulong*, byte> clock = &LinuxThreadReadNanoseconds;
                    if (c->State->LinuxQuota == LinuxQuotaDisposition.Terminating)
                    {
                        if (!LinuxQuotaPolicy(c->State) || c->Failed != 1 || c->Stopping != 1 || c->Process.Failed ||
                            c->ServiceShutdown != 0 || !c->Budget.CancellationRequested ||
                            clock(c->State, &finished) != 1 || finished < c->Budget.ObservedNanoseconds) return false;
                        c->ServiceFinished = finished; return false;
                    }
                    if (c->Failed != 0 || c->Process.Failed || c->ServiceReady != 1 || c->ServiceShutdown != 1 ||
                        !c->Budget.CancellationRequested || clock(c->State, &finished) != 1 || finished < c->Budget.ObservedNanoseconds) return false;
                    c->ServiceFinished = finished; return true;
#elif M10_017_PROBE || M10_018_PROBE
                    ulong finished = 0; delegate* unmanaged<void*, ulong*, byte> clock = &LinuxThreadReadNanoseconds;
                    return c->Failed == 0 && !c->Process.Failed && c->StaticApp.GracefulExit &&
                        c->State->LinuxSyscallExitCode == 0 && clock(c->State, &finished) == 1 && c->Budget.Observe(finished);
#elif M10_004_PROBE
                    return false; // Unexpected web termination, never finite completion.
#else
                    // Completion must not bypass the elapsed ceiling just
                    // because the final dispatch exited every remaining worker.
                    ulong finished = 0;
                    delegate* unmanaged<void*, ulong*, byte> clock = &LinuxThreadReadNanoseconds;
                    return c->Failed == 0 && !c->Process.Failed &&
                        (!c->Budget.RequiresClock || clock(c->State, &finished) == 1 && c->Budget.Observe(finished));
#endif
                }
                // Expiry is checked only on the restored supervisor. Previously
                // armed IRQs have already acknowledged/drained in Transfer.
                // Shutdown still uses the same real worker exit continuations.
                if (c->Stopping == 0)
                {
                    ulong now = 0;
                    delegate* unmanaged<void*, ulong*, byte> clock = &LinuxThreadReadNanoseconds;
                    bool clockRead = !c->Budget.RequiresClock || clock(c->State, &now) == 1;
#if M16_014_PROBE
                    bool progress = clockRead && LinuxServiceObserveWorkers(c, now);
                    if ((!progress || !c->Budget.Continue(now, c->Epoch, step)) &&
                        !LinuxNativeStop(c, !clockRead ? 6U : !progress ? 7U : 1U, step, now, clockRead)) return false;
#elif M10_017_PROBE || M10_018_PROBE
                    if ((!clockRead || !c->Budget.Continue(now, c->Epoch, step) || !LinuxStaticSupervisor(c, now)) &&
                        !LinuxNativeStop(c, clockRead ? 8U : 6U, step, now, clockRead)) return false;
#elif M10_004_PROBE
                    if (!clockRead || !c->Budget.Continue(now, c->Epoch, step)) return false;
#else
                    if ((!clockRead || !c->Budget.Continue(now, c->Epoch, step)) &&
                        !LinuxNativeStop(c, clockRead ? 1U : 6U, step, now, c->Budget.RequiresClock && clockRead)) return false;
#endif
                }
#if M10_004_PROBE && !M16_014_PROBE && !M10_017_PROBE && !M10_018_PROBE
                if (c->Stopping != 0 || c->Failed != 0 || c->Process.Failed || c->State->LinuxSyscallFaults != 0 ||
                    c->State->LinuxSyscallFailed != 0) return false;
#else
                if ((c->Failed != 0 || c->Process.Failed || c->State->LinuxSyscallFaults != 0 || c->State->LinuxSyscallFailed != 0) &&
                    !LinuxNativeStop(c, 2, step)) return false;
#endif
#if CLROS_LINUX_SOCKETS
                if (c->Stopping == 0 && !PollLinuxSocketNetwork(c
#if M10_018_TIMING_PROBE
                    , NativeDiskPollCause.Supervisor
#endif
                    ))
                {
#if M10_004_PROBE && !M16_014_PROBE && !M10_017_PROBE && !M10_018_PROBE
                    return false;
#else
                    if (!LinuxNativeStop(c, 2, step)) return false;
#endif
                }
#endif
                if (c->Threads.Flush(c->Key, 2) != KernelThreadTable.Status.Ok) return false;
                if (c->Threads.ReadyCount != 0)
                { if (!
#if M10_018_TIMING_PROBE
                    LinuxNativeMeasuredDispatch
#else
                    LinuxNativeDispatch
#endif
                    (c)) return false; }
#if CLROS_LINUX_SOCKETS
                else if (c->Stopping == 0)
                {
#if M10_004_PROBE && !M16_014_PROBE && !M10_017_PROBE && !M10_018_PROBE
                    if (!IdleLinuxSocketNetwork(c)) return false;
#else
                    if (!IdleLinuxSocketNetwork(c) && !LinuxNativeStop(c, 3, step)) return false;
#endif
                }
#else
                else if (c->Threads.DeadlineCount != 0 && c->Stopping == 0)
                {
                    if (!LinuxNativeWaitOne(c) && (!LinuxNativeRestoreTimer(c) || !LinuxNativeStop(c, 3, step))) return false;
                }
#endif
                else if (!LinuxNativeStop(c, 4, step)) return false;
            }
#if M10_004_PROBE && !M16_014_PROBE && !M10_017_PROBE && !M10_018_PROBE
            return false; // Counter ceiling reached; the outer owner retains everything.
#else
            // The bounded driver itself has made no native transfer here.
            // Cancellation still needs actual continuations, so use a separate
            // finite drain budget rather than freeing still-live resources.
            if (!LinuxNativeStop(c, 5, c->Budget.MaximumSteps)) return false;
            for (uint step = 0; step < LinuxNativeWorkers * 3; step++)
            {
                if (!LinuxNativeReapExited(c)) return false;
                if (c->Process.ActiveThreads == 0) return false;
                if (c->Threads.Flush(c->Key, 2) != KernelThreadTable.Status.Ok || c->Threads.ReadyCount == 0 || !
#if M10_018_TIMING_PROBE
                    LinuxNativeMeasuredDispatch
#else
                    LinuxNativeDispatch
#endif
                    (c)) return false;
            }
            return false;
#endif
        }

        // First-stop evidence distinguishes budget, failed-owner, idle,
        // empty untimed-wait and step-limit paths. Log before staged shutdown;
        // never emit TEST_FAIL here, because the actual exit/reap drain follows.
        private static bool LinuxNativeStop(LinuxNativeControl* c, uint reason, uint step, ulong now = 0, bool clockRead = false)
        {
            bool logged = c->Stopping != 0 ||
                LinuxText(c->State, "M16_008_STOP reason=") && LinuxNumber(c->State, reason) &&
                LinuxText(c->State, " step=") && LinuxNumber(c->State, step) &&
                LinuxText(c->State, " epochs=") && LinuxNumber(c->State, c->Epoch) &&
                LinuxText(c->State, " clockRead=") && LinuxNumber(c->State, clockRead ? 1UL : 0UL) &&
                LinuxText(c->State, " now=") && LinuxNumber(c->State, now) &&
                LinuxText(c->State, " started=") && LinuxNumber(c->State, c->Budget.StartedNanoseconds) &&
                LinuxText(c->State, " observed=") && LinuxNumber(c->State, c->Budget.ObservedNanoseconds) &&
                LinuxText(c->State, " ready=") && LinuxNumber(c->State, c->Threads.ReadyCount) &&
                LinuxText(c->State, " deadlines=") && LinuxNumber(c->State, c->Threads.DeadlineCount) &&
                LinuxText(c->State, " active=") && LinuxNumber(c->State, c->Process.ActiveThreads) &&
                LinuxText(c->State, " failed=") && LinuxNumber(c->State, c->Failed) &&
                LinuxText(c->State, " processFailed=") && LinuxNumber(c->State, c->Process.Failed ? 1UL : 0UL) &&
                LinuxText(c->State, " faults=") && LinuxNumber(c->State, c->State->LinuxSyscallFaults) &&
                LinuxText(c->State, " syscallFailed=") && LinuxNumber(c->State, c->State->LinuxSyscallFailed) && LinuxText(c->State, "\r\n");
            c->Failed = c->Stopping = 1; c->State->LinuxSyscallExitCode = 99;
            bool stopped = !c->Threads.Current.IsValid && !c->Threads.HandoffPending && c->Process.Shutdown(c->Key, 2, 99);
            return logged && stopped;
        }

#if M10_018_TIMING_PROBE
        // Measure the complete unchanged dispatch invocation at its actual
        // supervisor boundary. The listen/last-copy hooks clip inside it.
        // No accounting result grants authority to omit any dispatch checks.
        private static bool LinuxNativeMeasuredDispatch(LinuxNativeControl* c)
        {
            fixed (NativeDiskOwner* disk = &s_nativeDisk)
            {
                if (disk->Boot != c->State) return false;
                if (disk->TimingPhase == 2)
                    return disk->TimingDispatch.IsComplete && !disk->TimingDispatch.IsPhysicalDispatchActive && LinuxNativeDispatch(c);
                if (!ReadNetworkTime(c->State, out ulong began) ||
                    !disk->TimingDispatch.BeginDispatch(began, disk->TimingNetworkTotal, out ulong sequence)) return false;
                bool returned = LinuxNativeDispatch(c);
                // A later bookkeeping fault fails the original owner even if
                // an immutable phase-complete line has already been printed.
                if (!ReadNetworkTime(c->State, out ulong ended) ||
                    !disk->TimingDispatch.EndDispatch(sequence, ended, disk->TimingNetworkTotal)) return false;
                return returned;
            }
        }
#endif

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
#if M16_009_PROBE
            LinuxNativeTimingFinish(c);
#endif
            if (!LinuxText(c->State, "M16_008_SUPERVISOR identity=") || !LinuxNumber(c->State, next.Identity.Value) ||
                !LinuxText(c->State, " action=") || !LinuxNumber(c->State, c->Actions[index]) ||
                !LinuxText(c->State, " transfers=") || !LinuxNumber(c->State, c->Transfers) ||
                !LinuxText(c->State, " ready=") || !LinuxNumber(c->State, c->Threads.ReadyCount) ||
                !LinuxText(c->State, " active=") || !LinuxNumber(c->State, c->Process.ActiveThreads) || !LinuxText(c->State, "\r\n")) c->Failed = 1;
#if M16_009_PROBE
            if (!LinuxNativeTimingWrite(c, next.Identity.Value)) c->Failed = 1;
#endif
            return !c->Threads.Current.IsValid && !c->Threads.HandoffPending;
        }

#if M16_009_PROBE
        // Raw HPET reads avoid the owner/CPU/pool observations being measured.
        // Samples never advance thread deadlines or authorize skipped checks.
        private static bool LinuxNativeTimingRead(LinuxNativeControl* c, ulong* ticks)
        {
            delegate* unmanaged<void*, ulong*, byte> read = &ReadLinuxClock;
            return read(c->State, ticks) == 1;
        }

        private static void LinuxNativeTimingFinish(LinuxNativeControl* c)
        {
            if (c->TimingValid != 7 || !LinuxNativeTimingRead(c, &c->TimingReady) ||
                c->TimingStart > c->TimingArm || c->TimingArm > c->TimingCapture || c->TimingCapture > c->TimingReady)
            { c->TimingValid = 0; return; }
            c->TimingValid = 15;
        }

        private static bool LinuxNativeTimingWrite(LinuxNativeControl* c, ulong identity) => c->TimingValid != 15 ||
            LinuxText(c->State, "M16_008_TIMING identity=") && LinuxNumber(c->State, identity) &&
            LinuxText(c->State, " epoch=") && LinuxNumber(c->State, c->Epoch) &&
            LinuxText(c->State, " start=") && LinuxNumber(c->State, c->TimingStart) &&
            LinuxText(c->State, " arm=") && LinuxNumber(c->State, c->TimingArm) &&
            LinuxText(c->State, " capture=") && LinuxNumber(c->State, c->TimingCapture) &&
            LinuxText(c->State, " ready=") && LinuxNumber(c->State, c->TimingReady) &&
            LinuxText(c->State, " periodFs=") && LinuxNumber(c->State, c->State->Clock.PeriodFemtoseconds) && LinuxText(c->State, "\r\n");
#endif

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
#if M16_014_PROBE
                if (!c->Budget.ForgetWorker(index)) return false;
#endif
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
                !CooperativeFindLeaf(state, state->CooperativePool +
                    ((NativeProgram.Entries + index * NativeProgram.WorkerStride) & ~(Page - 1)), out ulong* leaf)) return false;
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
