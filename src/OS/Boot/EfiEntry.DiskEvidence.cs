#if M16_025_PROBE
using OS.Storage;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Threading;
using Block = OS.Storage.ContinuingReadOnlyBlockService;

namespace OS.Boot;

#pragma warning disable 649 // Eight contiguous caller-owned records are written through admitted native pointers.

public static unsafe partial class EfiEntry
{
    private static partial class M03FirstExitAdapter
    {
        // Scalars only: never copy a live block, filesystem, descriptor or
        // thread owner. These observations are selected by the entire canonical
        // ExitPending package, not a path, fd, address or application marker.
        private struct NativeDiskExitObservation
        {
            internal ulong Main, Child, Ordinal, Rip, WaitSequence;
            internal ulong RequestOwner, RequestEpoch, RequestGeneration;
            internal ulong BindingGeneration, DescriptionPosition, FileGeneration, Inode;
            internal ulong FsOwner, FsEpoch, FsGeneration, BlockOwner, BlockEpoch, BlockGeneration, Sector;
            internal ulong Submitted, Completed, Copied, Cancelled, StopCompleted, StopCancelled;
            internal ulong CancelSubmitted, CancelCopied, Cursor;
            internal ulong RequestWorker, LastClock;
            internal uint RequestIndex, Fd, FdGeneration, Binding, Description, FsIndex, FileIndex, OutputBytes;
            internal uint SlotBytes;
            internal byte Phase, RequestPhase;
            internal bool SlotActive;
        }

        private static bool IsNativeDiskExitPackage(Storage* state)
        {
            if (state == null || state->LinuxBytes != 29002 || state->LinuxPackage == 0) return false;
            ulong* digest = stackalloc ulong[4];
            return OS.Std.Hash.Sha256.TryCompute((byte*)state->LinuxPackage, state->LinuxBytes, (byte*)digest, 32) &&
                digest[0] == 0xf1c4de52c3eb7a3b && digest[1] == 0x8e4cd7a7c9450031 &&
                digest[2] == 0x8c14b69e6c088bcd && digest[3] == 0x3988b80eabe968e3;
        }

        // Called before the child's real exit_group dispatch. A source4 wait
        // alone is insufficient: its original FS operation must own the actual
        // submitted block request. No progress, copy, wake or cancellation here.
        private static bool ObserveNativeDiskExitCall(LinuxNativeControl* c, uint index, LinuxSyscallFrame call)
        {
            if (call.Number != 231 || !IsNativeDiskExitPackage(c->State)) return true;
            fixed (NativeDiskOwner* o = &s_nativeDisk)
            {
                var parent = c->Slots;
                var token = parent->DiskOperation;
                var parked = c->Threads.Inspect(c->Key, 2, parent->Kernel);
                if (index != 1 || call.Argument0 != 42 || c->Capacity != 2 || o->Boot != c->State || o->Phase != 7 ||
                    o->ExitObservation.Phase != 0 || c->GuardAttached != 1 || c->ProcessAttached != 1 ||
                    parent->Kernel.Value != 0x100000001 || c->Threads.Current.Value != 0x100000002)
                    return RejectNativeDiskExitObservation(c, index, 1);
                if (parent->Exiting != 0 || parent->WaitSource != 4 || !parent->Wait.IsValid ||
                    !parked.Succeeded || parked.State != KernelThreadTable.State.Waiting ||
                    parked.Completion != KernelThreadTable.Completion.None || parked.Wait.Sequence != parent->Wait.Sequence ||
                    parent->WaitCall.Number != 0 || parent->WaitCall.Argument0 != 3 ||
                    parent->WaitCall.Argument1 != 0x404000 || parent->WaitCall.Argument2 != 8192 || parent->DiskOutputBytes != 8192)
                    return RejectNativeDiskExitObservation(c, index, 2);
                if (!token.IsActive || token.Owner != (ulong)(&o->Descriptors) || token.Epoch != o->Descriptors.Epoch ||
                    token.Index >= LinuxDescriptorServices.DiskSlots || !o->Device.ReadPending || !o->Mount.IoActive)
                    return RejectNativeDiskExitObservation(c, index, 3);
                var request = o->Requests + token.Index;
                var fs = request->Operation;
                if (request->Generation != token.Generation || request->Worker.Value != parent->Kernel.Value ||
                    !request->Borrowed || request->Positioned || request->Cancelling || request->Number != 0 ||
                    request->Requested != 8192 || request->Offset != 0 ||
                    request->OriginalFd.Owner != &c->State->LinuxDescriptors || request->OriginalFd.Number != 3 ||
                    !LinuxDescriptorServices.IsCurrent(&c->State->LinuxDescriptors, request->OriginalFd) ||
                    request->Binding >= LinuxDescriptorServices.DiskSlots || request->Description >= c->State->LinuxDescriptors.Owner.DescriptionCount ||
                    !fs.IsInitialized || fs.Owner != (ulong)(&o->Mount) || fs.Epoch != o->Mount.Input.Epoch ||
                    fs.Index >= o->Mount.Input.OperationCount || fs.Index != o->Mount.IoIndex || fs.Generation != o->Mount.IoGeneration ||
                    o->Operations[fs.Index].Generation != fs.Generation || o->Mount.Input.Device != &o->Device ||
                    o->Mount.IoToken.Owner != (ulong)(&o->Device) || o->Mount.IoToken.Epoch != o->Device.PoolEpoch ||
                    o->Mount.IoToken.Generation != o->Device.RequestGeneration || o->Device.Submitted != o->Device.Completed + 1)
                    return RejectNativeDiskExitObservation(c, index, 4);
                var binding = o->Bindings + request->Binding;
                var description = c->State->LinuxDescriptors.Owner.Descriptions + request->Description;
                if (binding->Generation != request->BindingGeneration || binding->Description != request->Description ||
                    !binding->File.IsInitialized || binding->File.Owner != (ulong)(&o->Mount) ||
                    binding->File.Epoch != fs.Epoch || binding->File.Index >= o->Mount.Input.FileCount ||
                    o->Files[binding->File.Index].Generation != binding->File.Generation || description->Position != request->Offset ||
                    !NativeDiskExitSentinelBytes(c->State)) return RejectNativeDiskExitObservation(c, index, 5);
                o->ExitObservation = new()
                {
                    Main = parent->Kernel.Value, Child = c->Threads.Current.Value, Ordinal = LinuxNativeCallOrdinal(c, 0),
                    Rip = parent->WaitCall.Rip, WaitSequence = parent->Wait.Sequence,
                    RequestOwner = token.Owner, RequestEpoch = token.Epoch, RequestGeneration = token.Generation, RequestIndex = token.Index,
                    Fd = request->OriginalFd.Number, FdGeneration = request->OriginalFd.Generation,
                    Binding = request->Binding, BindingGeneration = request->BindingGeneration, Description = request->Description,
                    DescriptionPosition = description->Position, FileGeneration = binding->File.Generation, FileIndex = binding->File.Index,
                    Inode = binding->Metadata.Info.Inode,
                    FsOwner = fs.Owner, FsEpoch = fs.Epoch, FsGeneration = fs.Generation, FsIndex = fs.Index,
                    BlockOwner = o->Mount.IoToken.Owner, BlockEpoch = o->Mount.IoToken.Epoch, BlockGeneration = o->Device.RequestGeneration,
                    Sector = o->Device.RequestSector, Submitted = o->Device.Submitted, Completed = o->Device.Completed,
                    Copied = o->Device.Copied, Cancelled = o->Device.Cancelled, OutputBytes = parent->DiskOutputBytes, Phase = 1,
                    RequestWorker = request->Worker.Value, RequestPhase = request->Phase,
                    Cursor = description->Position, SlotActive = true, SlotBytes = parent->DiskOutputBytes
                };
                return WriteNativeDiskExitObservation(o);
            }
        }

        // Failure-only scalars from the validated native control and retained
        // static owner. Never dereference a rejected request/binding token or
        // manufacture a successful stage. The caller still aborts and retains
        // every uncertain owner through the ordinary cleanup path.
        private static bool RejectNativeDiskExitObservation(LinuxNativeControl* c, uint index, uint reason)
        {
            fixed (NativeDiskOwner* o = &s_nativeDisk)
            {
                if (o->Boot != c->State) return false;
                _ = LinuxText(c->State, "M16_025_EXIT_REJECT reason=") && LinuxNumber(c->State, reason) &&
                    LinuxText(c->State, " index=") && LinuxNumber(c->State, index) &&
                    LinuxText(c->State, " phase=") && LinuxNumber(c->State, o->ExitObservation.Phase) &&
                    LinuxText(c->State, " readPending=") && LinuxNumber(c->State, o->Device.ReadPending ? 1UL : 0UL) &&
                    LinuxText(c->State, " ioActive=") && LinuxNumber(c->State, o->Mount.IoActive ? 1UL : 0UL) &&
                    LinuxText(c->State, " submitted=") && LinuxNumber(c->State, o->Device.Submitted) &&
                    LinuxText(c->State, " completed=") && LinuxNumber(c->State, o->Device.Completed) &&
                    LinuxText(c->State, " copied=") && LinuxNumber(c->State, o->Device.Copied) &&
                    LinuxText(c->State, " generation=") && LinuxNumber(c->State, o->Device.RequestGeneration) &&
                    LinuxText(c->State, " sector=") && LinuxNumber(c->State, o->Device.RequestSector) && LinuxText(c->State, "\r\n");
                return false;
            }
        }

        // Observe the genuine parent CommitExit before/after DropDiskOperation.
        // It must revoke the original slot's copy/wake capability. A remaining
        // orphan retains its exact request/FS owners until drain or real reset.
        private static bool ObserveNativeDiskExitCommit(LinuxNativeControl* c, uint index, bool committed)
        {
            fixed (NativeDiskOwner* o = &s_nativeDisk)
            {
                var e = &o->ExitObservation;
                if (e->Phase == 0 || index != 0) return true;
                var slot = c->Slots;
                if (o->Boot != c->State || slot->Kernel.Value != e->Main || slot->Exiting == 0 ||
                    o->Device.Submitted != e->Submitted || o->Device.Copied != e->Copied) return false;
                if (!committed)
                {
                    var request = o->Requests + e->RequestIndex;
                    var binding = o->Bindings + e->Binding;
                    if (e->Phase != 1 || !slot->DiskOperation.IsActive || slot->DiskOperation.Owner != e->RequestOwner ||
                        slot->DiskOperation.Epoch != e->RequestEpoch || slot->DiskOperation.Generation != e->RequestGeneration ||
                        slot->DiskOperation.Index != e->RequestIndex || slot->WaitSource != 4 || slot->DiskOutputBytes != e->OutputBytes ||
                        slot->Wait.Sequence != e->WaitSequence || slot->WaitCall.Rip != e->Rip ||
                        request->Generation != e->RequestGeneration || request->Worker.Value != e->Main || !request->Borrowed || request->Cancelling ||
                        request->OriginalFd.Number != e->Fd || request->OriginalFd.Generation != e->FdGeneration ||
                        !LinuxDescriptorServices.IsCurrent(&c->State->LinuxDescriptors, request->OriginalFd) ||
                        request->Binding != e->Binding || request->BindingGeneration != e->BindingGeneration || request->Description != e->Description ||
                        request->Operation.Owner != e->FsOwner || request->Operation.Epoch != e->FsEpoch ||
                        request->Operation.Generation != e->FsGeneration || request->Operation.Index != e->FsIndex ||
                        binding->Generation != e->BindingGeneration || binding->Description != e->Description ||
                        binding->File.Generation != e->FileGeneration || binding->File.Index != e->FileIndex ||
                        c->State->LinuxDescriptors.Owner.Descriptions[e->Description].Position != e->DescriptionPosition ||
                        o->Device.RequestGeneration != e->BlockGeneration || o->Device.RequestSector != e->Sector) return false;
                    e->Phase = 2;
                }
                else
                {
                    var request = o->Requests + e->RequestIndex;
                    if (e->Phase != 2 || slot->DiskOperation.IsActive || slot->DiskOutputBytes != 0 || slot->WaitSource != 0 ||
                        request->Generation != 0 && (request->Generation != e->RequestGeneration || request->Worker.IsValid ||
                            !request->Cancelling || !request->Borrowed || request->BindingGeneration != e->BindingGeneration ||
                            request->Operation.Owner != e->FsOwner || request->Operation.Epoch != e->FsEpoch ||
                            request->Operation.Generation != e->FsGeneration || request->Operation.Index != e->FsIndex)) return false;
                    e->CancelSubmitted = o->Device.Submitted; e->CancelCopied = o->Device.Copied;
                    e->Phase = 3;
                }
                var observedRequest = o->Requests + e->RequestIndex;
                e->RequestWorker = observedRequest->Worker.Value; e->RequestPhase = observedRequest->Phase;
                e->SlotActive = slot->DiskOperation.IsActive; e->SlotBytes = slot->DiskOutputBytes;
                e->Cursor = c->State->LinuxDescriptors.Owner.Descriptions[e->Description].Position;
                if (e->Cursor != e->DescriptionPosition) return false;
                return WriteNativeDiskExitObservation(o);
            }
        }

        private static bool ObserveNativeDiskExitBeforeStop(NativeDiskOwner* o)
        {
            var e = &o->ExitObservation;
            if (e->Phase == 0) return true;
            if (e->Phase != 3 || !o->Device.RequiresCleanup || o->Device.Retired ||
                o->Device.Submitted != e->CancelSubmitted || o->Device.Copied != e->CancelCopied ||
                o->Device.Completed < e->Completed || o->Device.Completed > e->Completed + 1 ||
                o->Device.Cancelled < e->Cancelled || o->Device.Cancelled > e->Cancelled + 1) return false;
            e->StopCompleted = o->Device.Completed; e->StopCancelled = o->Device.Cancelled; e->Phase = 4;
            return WriteNativeDiskExitObservation(o);
        }

        // A reset-aborted request is not a used-ring completion. Preserve its
        // counters exactly and report the actual Stop receipt before FS.Stop.
        private static bool ObserveNativeDiskExitStopped(NativeDiskOwner* o, Block.Status stopped)
        {
            var e = &o->ExitObservation;
            if (e->Phase == 0) return true;
            var receipt = o->Device.Receipt;
            if (e->Phase != 4 || stopped != Block.Status.Success || !o->Device.Retired ||
                !receipt.ResetComplete || receipt.ResetReads == 0 || !receipt.BusMasterDisabled || !receipt.DmaReleased ||
                receipt.CleanupStatus != VirtioBlkQueueStatus.Success || !o->Device.ReturnedToCpu || !o->Device.Scrubbed ||
                o->Pool.AllocatedMask != 0 || o->Device.Submitted != e->Submitted || o->Device.Completed != e->StopCompleted ||
                o->Device.Cancelled != e->StopCancelled || o->Device.Copied != e->Copied) return false;
            e->Phase = 5; return WriteNativeDiskExitObservation(o);
        }

        // Whole mapped output, using validated kernel aliases and a fixed
        // 128-byte stack buffer. No user pointer or copy authority is retained.
        private static bool NativeDiskExitSentinelBytes(Storage* state)
        {
            byte* chunk = stackalloc byte[128];
            for (uint offset = 0; offset < 8192; offset += 128)
            {
                if (CopyLinuxAlias(state, 0x404000UL + offset, chunk, 128, false) != 1) return false;
                for (uint i = 0; i < 128; i++) if (chunk[i] != 0xa5) return false;
            }
            return true;
        }

        private static bool ObserveNativeDiskExitSentinel(Storage* state, LinuxNativeControl* c)
        {
            fixed (NativeDiskOwner* o = &s_nativeDisk)
            {
                var e = &o->ExitObservation;
                if (e->Phase == 0) return !IsNativeDiskExitPackage(state);
                if (e->Phase != 5 || o->Boot != state || o->Phase != 9 || !o->Mount.IsRetired || c == null ||
                    c->GuardAttached != 1 || c->ProcessAttached != 1 || c->Reaped != 2 || c->Process.ActiveThreads != 0 ||
                    c->Threads.Current.IsValid || c->Threads.HandoffPending || o->Device.Copied != e->Copied ||
                    !LinuxDescriptorServices.CanDetachThreads(&state->LinuxDescriptors, (ulong)(&c->Process)) ||
                    !NativeDiskExitSentinelBytes(state)) return false;
                e->Phase = 6; return WriteNativeDiskExitObservation(o);
            }
        }

        private static bool ObserveNativeDiskExitReleased(Storage* state, LinuxNativeControl* c)
        {
            fixed (NativeDiskOwner* o = &s_nativeDisk)
            {
                if (o->ExitObservation.Phase == 0) return true;
                if (o->ExitObservation.Phase != 6 || c == null || c->GuardAttached != 0 || c->ProcessAttached != 0) return false;
                o->ExitObservation.Phase = 7; return WriteNativeDiskExitObservation(o);
            }
        }

        private static bool ObserveNativeDiskExitMemoryRestored(Storage* state)
        {
            fixed (NativeDiskOwner* o = &s_nativeDisk)
            {
                if (o->ExitObservation.Phase == 0) return true;
                if (o->ExitObservation.Phase != 7 || o->Boot != state || o->Device.Copied != o->ExitObservation.Copied ||
                    state->LinuxMemory.ReservedBytes != 0 || state->LinuxMemory.BackedPages != 0 ||
                    state->LinuxMemory.HasImageProtections || state->LinuxMemory.HasMutationGuard) return false;
                o->ExitObservation.Phase = 8; return WriteNativeDiskExitObservation(o);
            }
        }

        // The socket drain clock predates memory/pool restoration. Sample a
        // separate terminal boundary from the still-owned HPET after all final
        // cleanup checks. Never dereference the released, scrubbed control.
        private static bool ObserveNativeDiskExitCleanupEnd(Storage* state, ulong started)
        {
            fixed (NativeDiskOwner* o = &s_nativeDisk)
            {
                var e = &o->ExitObservation;
                if (e->Phase == 0) return true;
                if (e->Phase != 8 || o->Boot != state || o->Phase != 9 || !o->Device.Retired || !o->Mount.IsRetired ||
                    o->Pool.AllocatedMask != 0 || o->Device.Copied != e->Copied || state->CooperativePhase != 6 ||
                    state->LinuxSyscallPhase != 6 || state->LinuxMemoryPhase != 6 || state->LinuxDescriptorPhase != 6 ||
                    !ReadNetworkTime(state, out ulong now) || now < e->LastClock || now < started ||
                    now - started >= LinuxThreadExecutionBudget.RuntimeNanoseconds) return false;
                return LinuxText(state, "M16_025_EXIT_CLEANUP_END owner=") && LinuxNumber(state, e->RequestOwner) &&
                    LinuxText(state, " epoch=") && LinuxNumber(state, e->RequestEpoch) &&
                    LinuxText(state, " clock=") && LinuxNumber(state, now) && LinuxText(state, "\r\n");
            }
        }

        private static bool WriteNativeDiskExitObservation(NativeDiskOwner* o)
        {
            // Request/slot/cursor fields retain the last stage1..3 snapshot;
            // backend and mount fields are read afresh at each actual stage.
            var e = &o->ExitObservation; var state = o->Boot; var receipt = o->Device.Receipt;
            bool written = ReadNetworkTime(state, out ulong now) && LinuxText(state, "M16_025_EXIT_PENDING stage=") && LinuxNumber(state, e->Phase) &&
                LinuxText(state, " main=") && LinuxNumber(state, e->Main) && LinuxText(state, " child=") && LinuxNumber(state, e->Child) &&
                LinuxText(state, " ordinal=") && LinuxNumber(state, e->Ordinal) && LinuxText(state, " rip=") && LinuxNumber(state, e->Rip) &&
                LinuxText(state, " sequence=") && LinuxNumber(state, e->WaitSequence) &&
                LinuxText(state, " owner=") && LinuxNumber(state, e->RequestOwner) && LinuxText(state, " epoch=") && LinuxNumber(state, e->RequestEpoch) &&
                LinuxText(state, " generation=") && LinuxNumber(state, e->RequestGeneration) && LinuxText(state, " index=") && LinuxNumber(state, e->RequestIndex) &&
                LinuxText(state, " fd=") && LinuxNumber(state, e->Fd) && LinuxText(state, " fdGeneration=") && LinuxNumber(state, e->FdGeneration) &&
                LinuxText(state, " binding=") && LinuxNumber(state, e->Binding) && LinuxText(state, " bindingGeneration=") && LinuxNumber(state, e->BindingGeneration) &&
                LinuxText(state, " description=") && LinuxNumber(state, e->Description) && LinuxText(state, " position=") && LinuxNumber(state, e->DescriptionPosition) &&
                LinuxText(state, " fileGeneration=") && LinuxNumber(state, e->FileGeneration) && LinuxText(state, " fileIndex=") && LinuxNumber(state, e->FileIndex) &&
                LinuxText(state, " inode=") && LinuxNumber(state, e->Inode) &&
                LinuxText(state, " fsOwner=") && LinuxNumber(state, e->FsOwner) && LinuxText(state, " fsEpoch=") && LinuxNumber(state, e->FsEpoch) &&
                LinuxText(state, " fsGeneration=") && LinuxNumber(state, e->FsGeneration) && LinuxText(state, " fsIndex=") && LinuxNumber(state, e->FsIndex) &&
                LinuxText(state, " blockOwner=") && LinuxNumber(state, e->BlockOwner) && LinuxText(state, " blockEpoch=") && LinuxNumber(state, e->BlockEpoch) &&
                LinuxText(state, " blockGeneration=") && LinuxNumber(state, e->BlockGeneration) && LinuxText(state, " sector=") && LinuxNumber(state, e->Sector) &&
                LinuxText(state, " outputBytes=") && LinuxNumber(state, e->OutputBytes) &&
                LinuxText(state, " submitted=") && LinuxNumber(state, o->Device.Submitted) && LinuxText(state, " completed=") && LinuxNumber(state, o->Device.Completed) &&
                LinuxText(state, " copied=") && LinuxNumber(state, o->Device.Copied) && LinuxText(state, " cancelled=") && LinuxNumber(state, o->Device.Cancelled) &&
                LinuxText(state, " available=") && LinuxNumber(state, o->Device.PublishedIndex) &&
                LinuxText(state, " consumed=") && LinuxNumber(state, o->Device.ConsumedIndex) &&
                LinuxText(state, " resetReads=") && LinuxNumber(state, receipt.ResetReads) &&
                LinuxText(state, " reset=") && LinuxNumber(state, receipt.ResetComplete ? 1UL : 0UL) &&
                LinuxText(state, " busMasterOff=") && LinuxNumber(state, receipt.BusMasterDisabled ? 1UL : 0UL) &&
                LinuxText(state, " dmaReleased=") && LinuxNumber(state, receipt.DmaReleased ? 1UL : 0UL) &&
                LinuxText(state, " poolMask=") && LinuxNumber(state, o->Pool.AllocatedMask) &&
                LinuxText(state, " sentinelBytes=") && LinuxNumber(state, e->Phase is 1 or 6 ? 8192UL : 0UL) &&
                LinuxText(state, " userReserved=") && LinuxNumber(state, state->LinuxMemory.ReservedBytes) &&
                LinuxText(state, " userBacked=") && LinuxNumber(state, state->LinuxMemory.BackedPages) &&
                LinuxText(state, " ioGeneration=") && LinuxNumber(state, o->Device.RequestGeneration) &&
                LinuxText(state, " ioSector=") && LinuxNumber(state, o->Device.RequestSector) &&
                LinuxText(state, " blockPhase=") && LinuxNumber(state, o->Device.Phase) &&
                LinuxText(state, " fsPhase=") && LinuxNumber(state, o->Mount.Phase) &&
                LinuxText(state, " ioActive=") && LinuxNumber(state, o->Mount.IoActive ? 1UL : 0UL) &&
                LinuxText(state, " fsIoIndex=") && LinuxNumber(state, o->Mount.IoIndex) &&
                LinuxText(state, " fsIoGeneration=") && LinuxNumber(state, o->Mount.IoGeneration) &&
                LinuxText(state, " requestPhase=") && LinuxNumber(state, e->RequestPhase) &&
                LinuxText(state, " requestWorker=") && LinuxNumber(state, e->RequestWorker) &&
                LinuxText(state, " slotActive=") && LinuxNumber(state, e->SlotActive ? 1UL : 0UL) &&
                LinuxText(state, " slotBytes=") && LinuxNumber(state, e->SlotBytes) &&
                LinuxText(state, " cursor=") && LinuxNumber(state, e->Cursor) &&
                LinuxText(state, " clock=") && LinuxNumber(state, now) &&
                LinuxText(state, "\r\n");
            if (written) e->LastClock = now;
            return written;
        }

        // One bounded pump can complete/copy and submit its next request. Keep
        // their original order without performing serial I/O inside callbacks.
        private struct NativeDiskRecord
        {
            internal Block.Observation Observation;
            internal fixed byte Bytes[512];
        }
        private struct NativeDiskEvidence
        {
            internal NativeDiskRecord A, B, C, D, E, F, G, H;
            internal ulong Generation, CopiedRecords;
            internal uint Count, Total;
            internal bool Enabled, Failed;
        }
        private const uint NativeDiskEvidenceCapacity = 8, NativeDiskEvidenceLimit = 65536;
        private static uint SelectedNativeDiskEvidenceLimit => OS.Kernel.Diagnostics.Probes.EnableM10018FullProbe ?
            OS.Kernel.Compatibility.LinuxX64.DiskStaticExecutionPolicy.MaximumDeviceRecords : NativeDiskEvidenceLimit;

        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte ObserveNativeDisk(void* context, Block.Observation* observation, byte* bytes)
        {
            NativeDiskOwner* o = (NativeDiskOwner*)context;
            if (o == null || observation == null || observation->Owner != (ulong)(&o->Device) ||
                observation->Epoch != NativeDiskEpoch || observation->Generation == 0 ||
                observation->Kind is not (Block.ObservationKind.Submitted or Block.ObservationKind.CompletionValidated or Block.ObservationKind.CopyReady)) return 0;
            NativeDiskEvidence* evidence = &o->Evidence;
            if (evidence->Failed) return 0;
            if (!evidence->Enabled) return 1;
            if (observation->Kind == Block.ObservationKind.Submitted) evidence->Generation = observation->Generation;
            // A read admitted before arming retains its earlier scope.
            if (evidence->Generation != observation->Generation) return 1;
            bool copy = observation->Kind == Block.ObservationKind.CopyReady;
            uint slots = copy ? observation->Bytes / 512 : 1;
            if (slots is 0 or > 4 || evidence->Count > NativeDiskEvidenceCapacity - slots ||
                evidence->Total > SelectedNativeDiskEvidenceLimit - slots ||
                (copy ? bytes == null || observation->Bytes != slots * 512 : bytes != null))
            { evidence->Failed = true; return 0; }
            // Reserve the complete group before accepting CopyReady. Each slot
            // holds one payload sector of the SAME physical transaction.
            for (uint chunk = 0; chunk < slots; chunk++)
            {
                NativeDiskRecord* record = &evidence->A + evidence->Count + chunk;
                record->Observation = *observation;
                if (copy) for (uint i = 0; i < 512; i++) record->Bytes[i] = bytes[chunk * 512 + i];
            }
            evidence->Count += slots; evidence->Total += slots; return 1;
        }

        private static bool ArmNativeDiskEvidence(NativeDiskOwner* o)
        {
            if (o == null || o->Phase != 7 || !o->Device.Running || !o->Mount.Ready || o->Evidence.Enabled ||
                o->Evidence.Failed || o->Evidence.Count != 0) return false;
            o->Evidence.Enabled = true; return true;
        }

#if M10_018_PROBE
        // The post-readiness boundary comes from the actual successful listen,
        // not process launch or the earlier mount. A pre-arming request keeps
        // its prior scope and is excluded by the generation observation rule.
        private static bool ArmLinuxStaticDisk(Storage* state, ulong now)
        {
            fixed (NativeDiskOwner* o = &s_nativeDisk)
                return o->Boot == state && ArmNativeDiskEvidence(o) &&
                    LinuxText(state, "M10_018_DISK_READY submitted=") && LinuxNumber(state, o->Device.Submitted) &&
                    LinuxText(state, " completed=") && LinuxNumber(state, o->Device.Completed) &&
                    LinuxText(state, " copied=") && LinuxNumber(state, o->Device.Copied) &&
                    LinuxText(state, " clock=") && LinuxNumber(state, now) && LinuxText(state, "\r\n");
        }
#endif

        // Invoked only after the actual bounded block/filesystem pump returns.
        // CopyReady is merely a candidate; the committed counter must match
        // before its bytes may be identified as an actual owned-buffer copy.
        private static bool FlushNativeDiskEvidence(Storage* state)
        {
            fixed (NativeDiskOwner* o = &s_nativeDisk)
            {
                if (o->Boot != state || o->Evidence.Failed) return false;
                NativeDiskEvidence* evidence = &o->Evidence;
                for (uint i = 0; i < evidence->Count;)
                {
                    NativeDiskRecord* record = &evidence->A + i;
                    var value = record->Observation;
                    uint slots = value.Kind == Block.ObservationKind.CopyReady ? value.Bytes / 512 : 1;
                    if (slots is 0 or > 4 || slots > evidence->Count - i ||
                        value.Kind == Block.ObservationKind.CopyReady && value.Bytes != slots * 512)
                    { evidence->Failed = true; return false; }
                    for (uint chunk = 1; chunk < slots; chunk++)
                        if (!SameNativeDiskObservation(value, record[chunk].Observation))
                        { evidence->Failed = true; return false; }
                    if (value.Kind == Block.ObservationKind.CopyReady && o->Device.Copied != value.Sequence ||
                        !ReadNetworkTime(state, out ulong now))
                    { evidence->Failed = true; return false; }
                    for (uint chunk = 0; chunk < slots; chunk++)
                    {
                        if (!WriteNativeDiskRecord(state, record + chunk, now, chunk))
                        { evidence->Failed = true; return false; }
                        record[chunk] = default;
                    }
                    if (value.Kind == Block.ObservationKind.CopyReady) evidence->CopiedRecords++;
                    i += slots;
                }
                evidence->Count = 0; return true;
            }
        }

        private static bool SameNativeDiskObservation(Block.Observation a, Block.Observation b) =>
            a.Kind == b.Kind && a.Owner == b.Owner && a.Epoch == b.Epoch && a.Generation == b.Generation &&
            a.Sector == b.Sector && a.Sequence == b.Sequence && a.Bytes == b.Bytes &&
            a.AvailableIndex == b.AvailableIndex && a.ConsumedIndex == b.ConsumedIndex;

        private static bool WriteNativeDiskRecord(Storage* state, NativeDiskRecord* record, ulong now, uint chunk)
        {
            var value = record->Observation;
            uint kind = chunk == 0 ? (uint)value.Kind : 3U;
            if (!LinuxText(state, "M16_023_IO kind=") || !LinuxNumber(state, kind) ||
                !LinuxText(state, " owner=") || !LinuxNumber(state, value.Owner) ||
                !LinuxText(state, " epoch=") || !LinuxNumber(state, value.Epoch) ||
                !LinuxText(state, " generation=") || !LinuxNumber(state, value.Generation) ||
                !LinuxText(state, " sector=") || !LinuxNumber(state, value.Sector) ||
                !LinuxText(state, " sequence=") || !LinuxNumber(state, value.Sequence) ||
                !LinuxText(state, " bytes=") || !LinuxNumber(state, value.Bytes) ||
                !LinuxText(state, " available=") || !LinuxNumber(state, value.AvailableIndex) ||
                !LinuxText(state, " consumed=") || !LinuxNumber(state, value.ConsumedIndex) ||
                !LinuxText(state, " clock=") || !LinuxNumber(state, now)) return false;
            if (value.Kind == Block.ObservationKind.CopyReady)
            {
                if (!LinuxText(state, " copied=committed")) return false;
                if (value.Bytes != 512 && (!LinuxText(state, " chunk=") || !LinuxNumber(state, chunk))) return false;
                if (!LinuxText(state, " data=")) return false;
                const string hex = "0123456789abcdef";
                for (uint i = 0; i < 512; i++)
                    if (!LinuxByte(state, (byte)hex[record->Bytes[i] >> 4]) ||
                        !LinuxByte(state, (byte)hex[record->Bytes[i] & 15])) return false;
            }
            return LinuxText(state, "\r\n");
        }
    }
}
#pragma warning restore 649
#endif
