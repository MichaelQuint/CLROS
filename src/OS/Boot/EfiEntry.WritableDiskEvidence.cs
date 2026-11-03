#if M16_027_PROBE
using B = OS.Storage.WritableBlockSession;

namespace OS.Boot;

public static unsafe partial class EfiEntry
{
    private static partial class M03FirstExitAdapter
    {
        // Copied facts from the actual retained source5 request after CALL is
        // logged and before any later pump. No user alias or new borrow is kept.
        private static bool RecordNativeWritableWait(LinuxNativeControl* c, uint index,
            OS.Kernel.Compatibility.LinuxX64.LinuxSyscallFrame call,
            OS.Kernel.Compatibility.LinuxX64.LinuxSyscallResult result)
        {
            var slot = c->Slots + index;
            if (result.Action != OS.Kernel.Compatibility.LinuxX64.LinuxSyscallAction.Blocked || slot->WaitSource != 5)
                return true;
            var disk = c->State->LinuxDescriptors.WritableDisk;
            if (disk == null || !NativeWritableControl(c->State, c) || !slot->Wait.IsValid ||
                !slot->WritableDiskOperation.IsActive || slot->WritableDiskOperation.Owner != (ulong)disk ||
                slot->WritableDiskOperation.Epoch != disk->Epoch || slot->WritableDiskOperation.Generation != disk->Request.Generation ||
                disk->Request.Phase == 0 || disk->Request.Worker.Value != slot->Kernel.Value ||
                slot->WaitCall.Number != call.Number || slot->WaitCall.Rip != call.Rip)
                return false;
            var request = &disk->Request;
            Storage* state = c->State;
            bool written = LinuxText(state, "M16_027_BEGIN identity=") && LinuxNumber(state, slot->Kernel.Value) &&
                LinuxText(state, " ordinal=") && LinuxNumber(state, LinuxNativeCallOrdinal(c, index)) &&
                LinuxText(state, " sequence=") && LinuxNumber(state, slot->Wait.Sequence) &&
                LinuxText(state, " requestOwner=") && LinuxNumber(state, slot->WritableDiskOperation.Owner) &&
                LinuxText(state, " requestEpoch=") && LinuxNumber(state, slot->WritableDiskOperation.Epoch) &&
                LinuxText(state, " request=") && LinuxNumber(state, slot->WritableDiskOperation.Generation) &&
                LinuxText(state, " nr=") && LinuxNumber(state, call.Number) &&
                LinuxText(state, " rip=") && LinuxNumber(state, call.Rip) &&
                LinuxText(state, " outputPrefix=") && LinuxNumber(state, slot->WritableDiskOutputBytes) &&
                LinuxText(state, " fdOwner=") && LinuxNumber(state, (ulong)request->OriginalFd.Owner) &&
                LinuxText(state, " fd=") && LinuxNumber(state, request->OriginalFd.Number) &&
                LinuxText(state, " fdGeneration=") && LinuxNumber(state, request->OriginalFd.Generation) &&
                LinuxText(state, " description=") && LinuxNumber(state, request->Description) &&
                LinuxText(state, " bindingGeneration=") && LinuxNumber(state, request->BindingGeneration) &&
                LinuxText(state, " reservedFd=") && LinuxNumber(state, request->ReservedFd) &&
                LinuxText(state, " reservedDescription=") && LinuxNumber(state, request->ReservedDescription) &&
                LinuxText(state, " reservationGeneration=") && LinuxNumber(state, request->ReservationGeneration) &&
                LinuxText(state, " providerOwner=") && LinuxNumber(state, request->ProviderRequest.Owner) &&
                LinuxText(state, " providerEpoch=") && LinuxNumber(state, request->ProviderRequest.Epoch) &&
                LinuxText(state, " providerRequest=") && LinuxNumber(state, request->ProviderRequest.Generation) &&
                LinuxText(state, " operation=") && LinuxNumber(state, request->Operation) &&
                LinuxText(state, " phase=") && LinuxNumber(state, request->Phase) &&
                LinuxText(state, " clock=") && LinuxNumber(state, c->Threads.ObservedTicks) && LinuxText(state, "\r\n");
#if M16_027_TIMING_PROBE
            if (written)
            {
                fixed (NativeWritableOwner* o = &s_nativeWritable)
                {
                    if (o->Boot != state || o->Phase != 6 || o->Evidence == null)
                        return false;
                    if (o->Evidence->TimingPhase == 0)
                        o->Evidence->TimingPhase = 1;
                }
            }
#endif
            return written;
        }

#if M16_027_TIMING_PROBE

#endif

#if M16_027_TIMING_PROBE
        // The complete owner/evidence/mapping admission finishes BEFORE the
        // measured turn. Only the already admitted HPET register is sampled;
        // normal Clock and Threads.ObservedTicks are never changed by timing.
        private static bool BeginNativeWritableTiming(NativeWritableOwner* o, LinuxNativeControl* c, out bool started)
        {
            started = false;
            if (o->Boot != c->State || o->Phase != 6 || o->Evidence == null)
                return true;
            if (o->Evidence->TimingPhase != 1)
                return true;
            if (!NativeWritableControl(c->State, c) || !NativeWritableShape(o))
                return false;
            Storage* s = c->State;
            if (s->Clock.Source != OS.Kernel.Platform.OwnedClockSource.Hpet ||
                s->Clock.Status != OS.Kernel.Platform.OwnedClockStatus.Ready || s->Clock.CounterWidth is not (32 or 64) ||
                s->Clock.FrequencyHz == 0 || s->HpetDescription.Address != s->Memory.References.HpetDevicePage ||
                s->HpetDescription.Address > ulong.MaxValue - 0xf0 ||
                !OS.Kernel.Diagnostics.WritableStorageTiming.Begin(&o->Evidence->Timing, s->HpetDescription.Address + 0xf0, s->Clock.CounterWidth))
                return false;
            o->Evidence->TimingPhase = 2;
            started = true;
            return true;
        }

        private static bool EmitNativeWritableTiming(NativeWritableOwner* o, bool valid)
        {
            if (o->Evidence->TimingPhase != 2)
                return false;
            var r = &o->Evidence->Timing;
            Storage* s = o->Boot;
            bool written = LinuxText(s, "M16_027_TIMING") &&
                NativeWritableTimingValue(s, " width=", r->Width) &&
                NativeWritableTimingValue(s, " frequency=", s->Clock.FrequencyHz) &&
                NativeWritableTimingValue(s, " valid=", valid ? 1UL : 0UL) &&
                NativeWritableTimingValue(s, " outer=", r->OuterTicks) &&
                NativeWritableTimingValue(s, " auditCalls=", r->AuditCalls) &&
                NativeWritableTimingValue(s, " auditTicks=", r->AuditTicks) &&
                NativeWritableTimingValue(s, " outsideDeviceCalls=", r->OutsideDeviceCalls) &&
                NativeWritableTimingValue(s, " pairCalls=", r->PairCalls) &&
                NativeWritableTimingValue(s, " pairTicks=", r->PairTicks) &&
                NativeWritableTimingValue(s, " pairAuditTicks=", r->PairAuditTicks) &&
                NativeWritableTimingValue(s, " pumpCalls=", r->PumpCalls) &&
                NativeWritableTimingValue(s, " pumpTicks=", r->PumpTicks) &&
                NativeWritableTimingValue(s, " pumpAuditTicks=", r->PumpAuditTicks) &&
                NativeWritableTimingValue(s, " engineCalls=", r->EnginePumpCalls) &&
                NativeWritableTimingValue(s, " engineOwner=", r->EngineOwner) &&
                NativeWritableTimingValue(s, " beforePhase=", r->FirstEnginePhase) &&
                NativeWritableTimingValue(s, " beforeStep=", r->FirstEngineStep) &&
                NativeWritableTimingValue(s, " beforeWork=", r->FirstEngineWork) &&
                NativeWritableTimingValue(s, " beforeIo=", r->FirstEngineIo ? 1UL : 0UL) &&
                NativeWritableTimingValue(s, " afterPhase=", r->LastEnginePhase) &&
                NativeWritableTimingValue(s, " afterStep=", r->LastEngineStep) &&
                NativeWritableTimingValue(s, " afterWork=", r->LastEngineWork) &&
                NativeWritableTimingValue(s, " afterIo=", r->LastEngineIo ? 1UL : 0UL) &&
                NativeWritableTimingValue(s, " rawReads=", r->RawReads) &&
                NativeWritableTimingValue(s, " adjacentReadTicks=", r->AdjacentReadTicks) &&
                NativeWritableTimingValue(s, " auditOutside=", r->AuditOutsideEnclosingTicks) &&
                NativeWritableTimingValue(s, " unattributed=", r->UnattributedTicks) && LinuxText(s, "\r\n");
            if (written)
                o->Evidence->TimingPhase = 3;
            return written;
        }
        private static bool NativeWritableTimingValue(Storage* s, string label, ulong value) =>
            LinuxText(s, label) && LinuxNumber(s, value);
#endif

        // Structural original-owner sampling also works after B.Stop returned
        // its DMA lease. It neither polls nor authorizes another I/O. Missing
        // stages/generations or serial failure stick; logging never recurses.
        private static bool SampleNativeWritableDisk(Storage* state)
        {
            fixed (NativeWritableOwner* o = &s_nativeWritable)
            {
                if (o->Boot != state || !NativeWritableShape(o))
                    return false;
                NativeWritableEvidence* e = o->Evidence;
                if (e->Failed)
                    return false;
                if (!SampleNativeWritableState(o))
                {
                    e->Failed = true;
                    return false;
                }
                for (uint i = 0; i < sizeof(B.Observation); i++)
                    ((byte*)(&e->Current))[i] = ((byte*)o->Observation)[i];
                B.Observation* current = &e->Current;
                B.Observation* prior = &e->Last;
                if (current->Flags == B.ObservationFlags.None)
                {
                    if (current->Request.Generation != 0 || o->Device->Submitted != 0 || prior->Request.Generation != 0)
                    {
                        e->Failed = true;
                        return false;
                    }
                    return true;
                }
                bool published = (current->Flags & B.ObservationFlags.Published) != 0;
                bool complete = (current->Flags & B.ObservationFlags.CompletionValidated) != 0;
                bool copied = (current->Flags & B.ObservationFlags.ReadCopied) != 0;
                bool valid = published && (byte)current->Flags is 1 or 3 or 7 &&
                    current->Request.Owner == (ulong)o->Device && current->Request.Epoch == NativeWritableEpoch &&
                    current->Request.Generation != 0 && current->Request.Generation == o->Device->Generation &&
                    current->SubmittedCount == o->Device->Submitted && current->CompletedCount == o->Device->Completed &&
                    current->SubmittedBytes == o->Device->SubmittedBytes && current->CompletedBytes == o->Device->CompletedBytes &&
                    current->FlushAcknowledgedBytes == o->Device->DurableBytes && current->CompletedCount <= current->SubmittedCount &&
                    current->CompletedBytes <= current->SubmittedBytes && current->FlushAcknowledgedBytes <= current->CompletedBytes &&
                    current->AvailableIndex == unchecked((ushort)current->SubmittedCount) && current->ConsumedIndex == unchecked((ushort)current->CompletedCount) &&
                    current->Kind is B.Operation.Read or B.Operation.Write or B.Operation.Flush &&
                    (current->Kind == B.Operation.Flush ? current->Sector == 0 : current->Sector < o->Capacity) &&
                    (current->Kind == B.Operation.Write ? current->PayloadBytes == 512 : current->Kind == B.Operation.Flush ? current->PayloadBytes == 0 : current->PayloadBytes == (copied ? 512U : 0U)) &&
                    (!copied || complete && current->Kind == B.Operation.Read && current->DeviceResult == B.Status.Success && !current->Cancelled) &&
                    (complete ? current->DeviceResult is B.Status.Success or B.Status.DeviceIoError or B.Status.UnsupportedRequest : current->DeviceResult == B.Status.Uninitialized) &&
                    (!complete || current->CompletionBytes == (current->DeviceResult == B.Status.Success && current->Kind != B.Operation.Flush ? 512U : 0U)) &&
                    (current->Kind == B.Operation.Flush ? current->FlushTargetBytes <= current->CompletedBytes : current->FlushTargetBytes == 0);
                if (current->Request.Generation != prior->Request.Generation)
                {
                    valid &= prior->Request.Generation != ulong.MaxValue && current->Request.Generation == prior->Request.Generation + 1 &&
                        current->SubmittedCount == prior->SubmittedCount + 1 && current->CompletedCount == prior->CompletedCount &&
                        current->SubmittedBytes == prior->SubmittedBytes + (current->Kind == B.Operation.Write ? 512UL : 0UL) &&
                        current->CompletedBytes == prior->CompletedBytes && current->FlushAcknowledgedBytes == prior->FlushAcknowledgedBytes &&
                        (current->Kind != B.Operation.Flush || current->FlushTargetBytes == prior->CompletedBytes) &&
                        current->Flags == B.ObservationFlags.Published &&
                        (prior->Request.Generation == 0 || (prior->Flags & B.ObservationFlags.CompletionValidated) != 0 &&
                            (prior->Kind != B.Operation.Read || prior->Cancelled || (prior->Flags & B.ObservationFlags.ReadCopied) != 0));
                }
                else
                {
                    valid &= current->Kind == prior->Kind && current->Sector == prior->Sector && current->SubmittedCount == prior->SubmittedCount &&
                        current->SubmittedBytes == prior->SubmittedBytes && ((byte)current->Flags & (byte)prior->Flags) == (byte)prior->Flags &&
                        current->FlushTargetBytes == prior->FlushTargetBytes &&
                        (!prior->Cancelled || current->Cancelled) && current->CompletedCount >= prior->CompletedCount &&
                        current->CompletedCount - prior->CompletedCount <= 1;
                    if ((prior->Flags & B.ObservationFlags.CompletionValidated) != 0)
                        valid &= current->DeviceResult == prior->DeviceResult && current->CompletionBytes == prior->CompletionBytes &&
                            current->CompletedCount == prior->CompletedCount && current->CompletedBytes == prior->CompletedBytes &&
                            current->FlushAcknowledgedBytes == prior->FlushAcknowledgedBytes;
                    if (prior->PayloadBytes == 512)
                        for (uint i = 0; i < 512; i++)
                            valid &= current->Data[i] == prior->Data[i];
                }
                if (current->PayloadBytes == 0)
                    for (uint i = 0; i < 512; i++)
                        valid &= current->Data[i] == 0;
                if (!valid)
                {
                    e->Failed = true;
                    return false;
                }
                bool changed = false;
                for (uint i = 0; i < sizeof(B.Observation); i++)
                    if (((byte*)current)[i] != ((byte*)prior)[i])
                    {
                        changed = true;
                        break;
                    }
                if (!changed)
                    return true;
                if (e->Rows >= 16384 || !ReadNetworkTime(state, out ulong now) || now < e->LastClock)
                {
                    e->Failed = true;
                    return false;
                }
                if (copied && current->Sector == 1 && o->Phase == 3 && !e->HeaderCopied)
                {
                    for (uint i = 0; i < 512; i++)
                        o->Header[i] = current->Data[i];
                    e->HeaderCopied = true;
                }
                if (!WriteNativeWritableObservation(o, current, now))
                {
                    e->Failed = true;
                    return false;
                }
                for (uint i = 0; i < sizeof(B.Observation); i++)
                    ((byte*)prior)[i] = ((byte*)current)[i];
                e->LastClock = now;
                e->Rows++;
                return true;
            }
        }

        // A failed publication/poll can change State.LastFailure without a
        // validated completion. Preserve that fact separately, including a
        // stopped dirty owner; never label it as an observed device completion.
        private static bool SampleNativeWritableState(NativeWritableOwner* o)
        {
            B.State* b = o->Device;
            NativeWritableEvidence* e = o->Evidence;
            bool bound = b->Self == (ulong)b;
            if (e->HadState && e->LastBound == bound && e->LastBlockFailure == b->LastFailure && e->LastRunning == b->Running && e->LastRetired == b->Retired &&
                e->LastDirty == b->Dirty && e->LastRetry == b->RetryRequired && e->StateSubmitted == b->Submitted && e->StateCompleted == b->Completed &&
                e->StateSubmittedBytes == b->SubmittedBytes && e->StateCompletedBytes == b->CompletedBytes && e->StateFlushedBytes == b->DurableBytes)
                return true;
            if (e->Rows >= 16384 || !ReadNetworkTime(o->Boot, out ulong now) || now < e->LastClock ||
                !LinuxText(o->Boot, "M16_027_STATE owner=") || !LinuxNumber(o->Boot, (ulong)b) ||
                !LinuxText(o->Boot, " epoch=") || !LinuxNumber(o->Boot, NativeWritableEpoch) ||
                !LinuxText(o->Boot, " bound=") || !LinuxNumber(o->Boot, bound ? 1UL : 0UL) ||
                !LinuxText(o->Boot, " status=") || !LinuxNumber(o->Boot, (uint)b->LastFailure) ||
                !LinuxText(o->Boot, " running=") || !LinuxNumber(o->Boot, b->Running ? 1UL : 0UL) ||
                !LinuxText(o->Boot, " retired=") || !LinuxNumber(o->Boot, b->Retired ? 1UL : 0UL) ||
                !LinuxText(o->Boot, " dirty=") || !LinuxNumber(o->Boot, b->Dirty ? 1UL : 0UL) ||
                !LinuxText(o->Boot, " retry=") || !LinuxNumber(o->Boot, b->RetryRequired ? 1UL : 0UL) ||
                !LinuxText(o->Boot, " submitted=") || !LinuxNumber(o->Boot, b->Submitted) ||
                !LinuxText(o->Boot, " completed=") || !LinuxNumber(o->Boot, b->Completed) ||
                !LinuxText(o->Boot, " submittedBytes=") || !LinuxNumber(o->Boot, b->SubmittedBytes) ||
                !LinuxText(o->Boot, " completedBytes=") || !LinuxNumber(o->Boot, b->CompletedBytes) ||
                !LinuxText(o->Boot, " flushAcknowledgedBytes=") || !LinuxNumber(o->Boot, b->DurableBytes) ||
                !LinuxText(o->Boot, " clock=") || !LinuxNumber(o->Boot, now) || !LinuxText(o->Boot, "\r\n"))
                return false;
            e->HadState = true;
            e->LastBound = bound;
            e->LastBlockFailure = b->LastFailure;
            e->LastRunning = b->Running;
            e->LastRetired = b->Retired;
            e->LastDirty = b->Dirty;
            e->LastRetry = b->RetryRequired;
            e->StateSubmitted = b->Submitted;
            e->StateCompleted = b->Completed;
            e->StateSubmittedBytes = b->SubmittedBytes;
            e->StateCompletedBytes = b->CompletedBytes;
            e->StateFlushedBytes = b->DurableBytes;
            e->LastClock = now;
            e->Rows++;
            return true;
        }

        private static bool WriteNativeWritableObservation(NativeWritableOwner* o, B.Observation* row, ulong now)
        {
            Storage* s = o->Boot;
            if (!LinuxText(s, "M16_027_IO owner=") || !LinuxNumber(s, row->Request.Owner) ||
                !LinuxText(s, " epoch=") || !LinuxNumber(s, row->Request.Epoch) ||
                !LinuxText(s, " generation=") || !LinuxNumber(s, row->Request.Generation) ||
                !LinuxText(s, " kind=") || !LinuxNumber(s, (uint)row->Kind) ||
                !LinuxText(s, " sector=") || !LinuxNumber(s, row->Sector) ||
                !LinuxText(s, " flags=") || !LinuxNumber(s, (uint)row->Flags) ||
                !LinuxText(s, " status=") || !LinuxNumber(s, (uint)row->DeviceResult) ||
                !LinuxText(s, " cancelled=") || !LinuxNumber(s, row->Cancelled ? 1UL : 0UL) ||
                !LinuxText(s, " submitted=") || !LinuxNumber(s, row->SubmittedCount) ||
                !LinuxText(s, " completed=") || !LinuxNumber(s, row->CompletedCount) ||
                !LinuxText(s, " submittedBytes=") || !LinuxNumber(s, row->SubmittedBytes) ||
                !LinuxText(s, " completedBytes=") || !LinuxNumber(s, row->CompletedBytes) ||
                !LinuxText(s, " flushTargetBytes=") || !LinuxNumber(s, row->FlushTargetBytes) ||
                !LinuxText(s, " flushAcknowledgedBytes=") || !LinuxNumber(s, row->FlushAcknowledgedBytes) ||
                !LinuxText(s, " available=") || !LinuxNumber(s, row->AvailableIndex) ||
                !LinuxText(s, " consumed=") || !LinuxNumber(s, row->ConsumedIndex) ||
                !LinuxText(s, " completionBytes=") || !LinuxNumber(s, row->CompletionBytes) ||
                !LinuxText(s, " payloadBytes=") || !LinuxNumber(s, row->PayloadBytes) ||
                !LinuxText(s, " clock=") || !LinuxNumber(s, now))
                return false;
            if (row->PayloadBytes == 512)
            {
                if (!LinuxText(s, " data="))
                    return false;
                const string hex = "0123456789abcdef";
                for (uint i = 0; i < 512; i++)
                    if (!LinuxByte(s, (byte)hex[row->Data[i] >> 4]) || !LinuxByte(s, (byte)hex[row->Data[i] & 15]))
                        return false;
            }
            return LinuxText(s, "\r\n");
        }
        private static bool WriteNativeWritableBinding(NativeWritableOwner* o) =>
            SampleNativeWritableDisk(o->Boot) && ReadNetworkTime(o->Boot, out ulong now) && now >= o->Evidence->LastClock &&
            LinuxText(o->Boot, "M16_027_BOUND owner=") && LinuxNumber(o->Boot, (ulong)o->Device) &&
            LinuxText(o->Boot, " epoch=") && LinuxNumber(o->Boot, NativeWritableEpoch) &&
            LinuxText(o->Boot, " arena=") && LinuxNumber(o->Boot, o->Arena) &&
            LinuxText(o->Boot, " pages=") && LinuxNumber(o->Boot, o->Pages) &&
            LinuxText(o->Boot, " bar=") && LinuxNumber(o->Boot, o->Bar) &&
            LinuxText(o->Boot, " capacity=") && LinuxNumber(o->Boot, o->Capacity) &&
            LinuxText(o->Boot, " nativeOwner=") && LinuxNumber(o->Boot, (ulong)o) &&
            LinuxText(o->Boot, " nativeOwnerBytes=") && LinuxNumber(o->Boot, (ulong)sizeof(NativeWritableOwner)) &&
            LinuxText(o->Boot, " boot=") && LinuxNumber(o->Boot, (ulong)o->Boot) &&
            LinuxText(o->Boot, " bootBytes=") && LinuxNumber(o->Boot, (ulong)sizeof(Storage)) &&
            LinuxText(o->Boot, " carrier=") && LinuxNumber(o->Boot, (ulong)o->Carrier) &&
            LinuxText(o->Boot, " carrierBytes=") && LinuxNumber(o->Boot, (ulong)sizeof(NativeWritableCarrier)) &&
            LinuxText(o->Boot, " observation=") && LinuxNumber(o->Boot, (ulong)o->Observation) &&
            LinuxText(o->Boot, " observationBytes=") && LinuxNumber(o->Boot, (ulong)sizeof(B.Observation)) &&
            LinuxText(o->Boot, " engine=") && LinuxNumber(o->Boot, (ulong)o->Engine) &&
            LinuxText(o->Boot, " engineEpoch=") && LinuxNumber(o->Boot, NativeWritableEpoch + 2) &&
            LinuxText(o->Boot, " port=") && LinuxNumber(o->Boot, (ulong)o->Port) &&
            LinuxText(o->Boot, " portEpoch=") && LinuxNumber(o->Boot, NativeWritableEpoch + 3) &&
            LinuxText(o->Boot, " mount=") && LinuxNumber(o->Boot, (ulong)o->Mount) &&
            LinuxText(o->Boot, " mountEpoch=") && LinuxNumber(o->Boot, NativeWritableEpoch + 4) &&
            LinuxText(o->Boot, " provider=") && LinuxNumber(o->Boot, (ulong)o->Provider) &&
            LinuxText(o->Boot, " providerEpoch=") && LinuxNumber(o->Boot, NativeWritableEpoch + 2) &&
            LinuxText(o->Boot, " readAttachment=") && LinuxNumber(o->Boot, (ulong)o->ReadDescriptors) &&
            LinuxText(o->Boot, " writeAttachment=") && LinuxNumber(o->Boot, (ulong)o->WriteDescriptors) &&
            LinuxText(o->Boot, " dataFirst=") && LinuxNumber(o->Boot, o->Selected->Volume.PartitionFirstLba) &&
            LinuxText(o->Boot, " dataLast=") && LinuxNumber(o->Boot, o->Selected->Volume.PartitionLastLba) &&
            LinuxText(o->Boot, " webFirst=") && LinuxNumber(o->Boot, o->Esp.FirstLba) &&
            LinuxText(o->Boot, " webLast=") && LinuxNumber(o->Boot, o->Esp.LastLba) &&
            LinuxText(o->Boot, " pid=") && LinuxNumber(o->Boot, o->PrimaryPid) &&
            LinuxText(o->Boot, " clock=") && LinuxNumber(o->Boot, now) &&
            LinuxText(o->Boot, " pci=0:7:0 queue=0 access=readwrite flush=negotiated reservation=retained\r\n");
        private static bool WriteNativeWritableRetired(NativeWritableOwner* o) =>
            !o->Evidence->Failed && ReadNetworkTime(o->Boot, out ulong now) && now >= o->Evidence->LastClock &&
            LinuxText(o->Boot, "M16_027_STOP submitted=") && LinuxNumber(o->Boot, o->Device->Submitted) &&
            LinuxText(o->Boot, " completed=") && LinuxNumber(o->Boot, o->Device->Completed) &&
            LinuxText(o->Boot, " submittedBytes=") && LinuxNumber(o->Boot, o->Device->SubmittedBytes) &&
            LinuxText(o->Boot, " completedBytes=") && LinuxNumber(o->Boot, o->Device->CompletedBytes) &&
            LinuxText(o->Boot, " flushAcknowledgedBytes=") && LinuxNumber(o->Boot, o->Device->DurableBytes) &&
            LinuxText(o->Boot, " rows=") && LinuxNumber(o->Boot, o->Evidence->Rows) &&
            LinuxText(o->Boot, " clock=") && LinuxNumber(o->Boot, now) &&
            LinuxText(o->Boot, " dma=zero reservation=retained\r\n");
    }
}
#endif
