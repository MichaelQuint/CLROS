#if M10_017_PROBE || M10_018_PROBE
using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Boot;

public static unsafe partial class EfiEntry
{
    private static partial class M03FirstExitAdapter
    {
#if M10_018_PROBE
        private const string LinuxStaticTag = "M10_018";
        private const string LinuxStaticStart = "TEST_START m10.018\r\n";
        private const string LinuxStaticFinish = "M10_018_CLEANUP process=drained cpu=restored nic=retired dma=zero allocator=unchanged pci=restored canaries=valid reservation=retained\r\nCLROS_M10_018_READY\r\nTEST_PASS m10.018\r\n";





#else
        private const string LinuxStaticTag = "M10_017";
        private const string LinuxStaticStart = "TEST_START m10.017\r\n";
        private const string LinuxStaticFinish = "M10_017_CLEANUP process=drained cpu=restored nic=retired dma=zero allocator=unchanged pci=restored canaries=valid reservation=retained\r\nCLROS_M10_017_READY\r\nTEST_PASS m10.017\r\n";
#endif
















        private static bool LinuxStaticCaptureFile(LinuxNativeControl* c, ulong number, out LinuxStaticFileIdentity file)
        {
            file = default;
            var state = &c->State->LinuxDescriptors;
            var owner = state->Owner;
            if (number >= owner.DescriptorCount) return true;
            uint fd = (uint)number;
            var description = LinuxDescriptorServices.Find(state, fd);
            if (description == null || description->Type != (uint)LinuxDescriptorServices.Kind.Package &&
#if M10_018_PROBE
                description->Type != (uint)LinuxDescriptorServices.Kind.Disk &&
#endif
                description->Type != (uint)LinuxDescriptorServices.Kind.Snapshot) return true;
            if (!LinuxDescriptorServices.Capture(state, fd, out var token) || !LinuxDescriptorServices.IsCurrent(state, token)) return false;
            uint kind = description->Type == (uint)LinuxDescriptorServices.Kind.Package ? 1U : 2U;
#if M10_018_PROBE
            if (description->Type == (uint)LinuxDescriptorServices.Kind.Disk) kind = 5;
#endif
            uint view = uint.MaxValue;
            if (kind == 1)
            {
                for (uint i = 0; i < owner.ViewCount; i++)
                    if (owner.Views[i].Data == description->Data && owner.Views[i].Bytes == description->Bytes && owner.Views[i].Inode == description->Inode)
                    { if (view != uint.MaxValue) return false; view = i; }
                if (view == uint.MaxValue) return false;
            }
            else if (kind == 2)
            {
                uint index = owner.Slots[fd].Description - 1;
                if (index >= owner.DescriptionCount || description->Data != owner.Snapshots + index * LinuxDescriptorServices.SnapshotBytes ||
                    description->Bytes == 0 || description->Bytes > LinuxDescriptorServices.SnapshotBytes || description->Inode is not (17 or 18)) return false;
            }
            file = new() { Fd = fd, Generation = owner.Slots[fd].Generation, Kind = kind, View = view,
                Inode = description->Inode, Bytes = description->Bytes, Position = description->Position };
            return true;
        }

        private static bool LinuxStaticCaptureBefore(LinuxNativeControl* c, LinuxSyscallFrame call, LinuxStaticFileBefore* before)
        {
            *before = default;
            if (call.Number is 0 or 3 or 5 or 8 or 17 or 72 or 73 or 221)
                if (!LinuxStaticCaptureFile(c, call.Argument0, out before->File)) return false;
            if (call.Number is not (2 or 4 or 6 or 89 or 257)) return true;
            before->HasPath = true;
            ulong address = call.Number == 257 ? call.Argument1 : call.Argument0;
            byte* path = before->Path;
            if (!LinuxThreadKernelRange(c, path, LinuxDescriptorServices.MaximumPath, false)) return false;
            // Copy only within the current user page, then find its first NUL.
            // A valid terminator never requires reading a following unmapped page.
            uint copied = 0;
            while (copied < LinuxDescriptorServices.MaximumPath)
            {
                if (address > ulong.MaxValue - copied) return false;
                ulong current = address + copied;
                uint chunk = (uint)(Page - (current & (Page - 1)));
                if (chunk > LinuxDescriptorServices.MaximumPath - copied) chunk = LinuxDescriptorServices.MaximumPath - copied;
                if (CopyLinuxAlias(c->State, current, path + copied, chunk, false) != 1) return false;
                for (uint i = 0; i < chunk; i++)
                    if (path[copied + i] == 0) { before->PathBytes = copied + i; return true; }
                copied += chunk;
            }
            return false; // The measured fixture never supplies an overlong path.
        }

        private static bool LinuxStaticObserveFile(LinuxNativeControl* c, uint index, LinuxSyscallFrame call,
            LinuxSyscallResult result, LinuxStaticFileBefore* before, ulong originalOrdinal = 0)
        {
            Storage* state = c->State;
            ulong ordinal = originalOrdinal == 0 ? LinuxNativeCallOrdinal(c, index) : originalOrdinal, identity = c->Threads.Current.Value;
            LinuxStaticFileIdentity file = before->File;
            bool opened = call.Number is 2 or 257 && result.Value >= 0;
            if (opened && (!LinuxStaticCaptureFile(c, (ulong)result.Value, out file) || file.Kind == 0)) return false;
            if (before->HasPath)
            {
                uint kind = 0, view = uint.MaxValue, mode = 0;
                ulong inode = 0, bytes = 0;
                if (opened)
                { kind = file.Kind; view = file.View; inode = file.Inode; bytes = file.Bytes; mode = 0x8124; }
                else if (result.Value == 0 && call.Number is 4 or 6)
                {
                    byte* wire = stackalloc byte[144];
                    if (!LinuxThreadKernelRange(c, wire, 144, false) || CopyLinuxAlias(state, call.Argument1, wire, 144, false) != 1) return false;
                    inode = *(ulong*)(wire + 8); mode = *(uint*)(wire + 24); bytes = *(ulong*)(wire + 48);
                    if (*(ulong*)(wire + 16) != 1 || *(ulong*)(wire + 56) != 4096) return false;
                    if (mode == 0x416d) kind = 3;
                    else if (mode == 0xa1ff) kind = 4;
                    else if (mode == 0x8124)
                    {
                        if (inode is 17 or 18) kind = 2;
                        else
                        {
                            var owner = state->LinuxDescriptors.Owner;
                            for (uint i = 0; i < owner.ViewCount; i++)
                                if (owner.Views[i].Inode == inode && owner.Views[i].Bytes == bytes)
                                { if (view != uint.MaxValue) return false; view = i; }
                            if (view != uint.MaxValue) kind = 1;
#if M10_018_PROBE
                            else if (LinuxStaticIsDiskPath(before)) kind = 5;
#endif
                            else return false;
                        }
                    }
                    else return false;
                }
                else if (call.Number == 89 && result.Value >= 0)
                { kind = 4; bytes = (ulong)result.Value; }
                if (!LinuxText(state, LinuxStaticTag + "_PATH identity=") || !LinuxNumber(state, identity) ||
                    !LinuxText(state, " ordinal=") || !LinuxNumber(state, ordinal) ||
                    !LinuxText(state, " nr=") || !LinuxNumber(state, call.Number) || !LinuxText(state, " pathHex=")) return false;
                for (uint i = 0; i < before->PathBytes; i++)
                {
                    uint high = (uint)before->Path[i] >> 4, low = (uint)before->Path[i] & 15;
                    if (!LinuxByte(state, (byte)(high < 10 ? '0' + high : 'a' + high - 10)) ||
                        !LinuxByte(state, (byte)(low < 10 ? '0' + low : 'a' + low - 10))) return false;
                }
                if (!LinuxText(state, " valueBits=") || !LinuxNumber(state, unchecked((ulong)result.Value)) ||
                    !LinuxText(state, " kind=") || !LinuxNumber(state, kind) || !LinuxText(state, " view=") || !LinuxNumber(state, view) ||
                    !LinuxText(state, " inode=") || !LinuxNumber(state, inode) || !LinuxText(state, " bytes=") || !LinuxNumber(state, bytes) ||
                    !LinuxText(state, " mode=") || !LinuxNumber(state, mode) || !LinuxText(state, "\r\n")) return false;
            }
            if (file.Kind == 0) return true;
#if M10_018_TIMING_PROBE
            if (file.Kind == 5 && call.Number is 0 or 17 && result.Value > 0 && !ObserveLinuxDiskTiming(c, (ulong)result.Value)) return false;
#endif
            ulong afterPosition = file.Position;
            if (call.Number == 3)
            { if (result.Value != 0 || LinuxDescriptorServices.Find(&state->LinuxDescriptors, file.Fd) != null) return false; }
            else
            {
                if (!LinuxStaticCaptureFile(c, file.Fd, out var after) || after.Kind != file.Kind || after.Generation != file.Generation ||
                    after.View != file.View || after.Inode != file.Inode || after.Bytes != file.Bytes) return false;
                afterPosition = after.Position;
            }
            return LinuxText(state, LinuxStaticTag + "_FILE identity=") && LinuxNumber(state, identity) &&
                LinuxText(state, " ordinal=") && LinuxNumber(state, ordinal) && LinuxText(state, " nr=") && LinuxNumber(state, call.Number) &&
                LinuxText(state, " fd=") && LinuxNumber(state, file.Fd) && LinuxText(state, " generation=") && LinuxNumber(state, file.Generation) &&
                LinuxText(state, " kind=") && LinuxNumber(state, file.Kind) && LinuxText(state, " view=") && LinuxNumber(state, file.View) &&
                LinuxText(state, " inode=") && LinuxNumber(state, file.Inode) && LinuxText(state, " bytes=") && LinuxNumber(state, file.Bytes) &&
                LinuxText(state, " position=") && LinuxNumber(state, file.Position) && LinuxText(state, " afterPosition=") &&
                LinuxNumber(state, afterPosition) && LinuxText(state, "\r\n");
        }

#if M10_018_PROBE
        private static bool LinuxStaticIsDiskPath(LinuxStaticFileBefore* before)
        {
            const string root = "/app/wwwroot";
            if (!before->HasPath || before->PathBytes < root.Length) return false;
            for (uint i = 0; i < root.Length; i++) if (before->Path[i] != root[(int)i]) return false;
            return before->PathBytes == root.Length || before->Path[root.Length] == '/';
        }

        // Capture the pre-dispatch path and fd identity in retained supervisor
        // metadata. No resolved user pointer or stack borrow survives a park.
        private static bool LinuxStaticRefreshFile(LinuxNativeControl* c, uint index)
        {
            if (index >= c->Capacity || c->StaticPending == null) return false;
            LinuxStaticPending* pending = c->StaticPending + index;
            if (!LinuxThreadKernelRange(c, (byte*)pending, (uint)sizeof(LinuxStaticPending), false) ||
                pending->Identity != c->Threads.Current.Value || pending->Ordinal == 0) return false;
            LinuxStaticFileIdentity original = pending->Before.File;
            if (original.Kind == 0) return true;
            if (!LinuxStaticCaptureFile(c, original.Fd, out var current) || current.Kind != original.Kind ||
                current.Generation != original.Generation || current.View != original.View ||
                current.Inode != original.Inode || current.Bytes != original.Bytes) return false;
            // CursorDiskBusy serializes ordinary reads/seeks through actual
            // completion. The position now belongs to this operation, after
            // every older cursor owner committed. Preserve all other identity.
            pending->Before.File.Position = current.Position; return true;
        }

        private static bool LinuxStaticCompleteFile(LinuxNativeControl* c, uint index, LinuxSyscallFrame call, LinuxSyscallResult result)
        {
            if (index >= c->Capacity || c->StaticPending == null) return false;
            LinuxStaticPending* pending = c->StaticPending + index;
            if (!LinuxThreadKernelRange(c, (byte*)pending, (uint)sizeof(LinuxStaticPending), false) ||
                pending->Identity != c->Threads.Current.Value || pending->Ordinal == 0) return false;
            bool observed = LinuxStaticObserveFile(c, index, call, result, &pending->Before, pending->Ordinal);
            *pending = default; return observed;
        }
#endif

        private static bool LinuxStaticRecordFile(LinuxNativeControl* c, uint index, LinuxSyscallFrame call,
            LinuxSyscallResult result, LinuxStaticFileBefore* before)
        {
#if M10_018_PROBE
            if (index >= c->Capacity || c->StaticPending == null) return false;
            LinuxStaticPending* pending = c->StaticPending + index;
            if (!LinuxThreadKernelRange(c, (byte*)pending, (uint)sizeof(LinuxStaticPending), false) || pending->Identity != 0) return false;
            if (result.Action == LinuxSyscallAction.Blocked && c->Slots[index].WaitSource == 4)
            {
                *pending = new() { Before = *before, Identity = c->Threads.Current.Value,
                    Ordinal = LinuxNativeCallOrdinal(c, index) };
                return pending->Identity != 0 && pending->Ordinal != 0;
            }
#endif
            return LinuxStaticObserveFile(c, index, call, result, before);
        }

        // Static output has its own exact app identity. The accepted smoke's
        // constructor/log exercise and continuing-service markers are unrelated.
        private static bool LinuxStaticObserveOutput(Storage* state, uint channel, byte* source, uint bytes)
        {
            LinuxNativeControl* c = LinuxNativeOwner(state);
            return c != null && c->Phase == 3 && c->Published == 1 && state->NetworkPhase == 2 &&
                c->Threads.Current.IsValid && c->Process.ActiveThreads != 0 &&
                c->StaticMain.Value == c->StaticApp.MainGeneration &&
                c->StaticApp.ObserveOutput(c->Threads.Current.Value, channel, source, bytes);
        }

        private static bool LinuxStaticObserveCall(LinuxNativeControl* c, LinuxSyscallFrame call,
            LinuxSyscallResult result, bool listenerClosing, bool listenerLastReference)
        {
            if (c == null || c->Phase != 3 || c->Published != 1 || c->State->NetworkPhase != 2 ||
                !c->Threads.Current.IsValid || c->StaticApp.Failed || c->StaticMain.Value != c->StaticApp.MainGeneration)
                return false;
            if (call.Number == 228 && unchecked((uint)call.Argument0) == 0 && result.Value != 0) return false;
            ulong writer = c->Threads.Current.Value;
            bool succeeded = result.Action == LinuxSyscallAction.Return && result.Value == 0;
            if (call.Number == 49)
            {
                if (!c->StaticApp.RecordBind(writer, unchecked((uint)call.Argument0), succeeded) ||
                    !LinuxDescriptorServices.Capture(&c->State->LinuxDescriptors, unchecked((uint)call.Argument0), out c->StaticListener)) return false;
            }
            if (call.Number == 50)
            {
                ulong now = 0; delegate* unmanaged<void*, ulong*, byte> clock = &LinuxThreadReadNanoseconds;
                if (clock(c->State, &now) != 1 || !c->StaticApp.RecordListen(writer, unchecked((uint)call.Argument0), succeeded, now) ||
#if M10_018_PROBE
                    !ArmLinuxStaticDisk(c->State, now) ||
#endif
#if M10_018_TIMING_PROBE
                    !ObserveLinuxDiskTiming(c, 0) ||
#endif
                    !LinuxText(c->State, LinuxStaticTag + "_LISTENING main=") || !LinuxNumber(c->State, c->StaticMain.Value) ||
                    !LinuxText(c->State, " writer=") || !LinuxNumber(c->State, writer) ||
                    !LinuxText(c->State, " fd=") || !LinuxNumber(c->State, call.Argument0) ||
                    !LinuxText(c->State, " nanos=") || !LinuxNumber(c->State, now) ||
                    !LinuxText(c->State, " http=external cleanup=pending\r\n")) return false;
            }
            if (listenerClosing)
            {
                if (!c->StaticApp.RecordClose(unchecked((uint)call.Argument0), succeeded && listenerLastReference)) return false;
                c->StaticListener = default;
            }
            if (call.Number == 231)
            {
                ulong now = 0; delegate* unmanaged<void*, ulong*, byte> clock = &LinuxThreadReadNanoseconds;
                if (result.Action != LinuxSyscallAction.Terminate || clock(c->State, &now) != 1 ||
                    result.ExitCode != 0 || !c->StaticApp.RecordExit(writer, unchecked((uint)call.Argument0), now) ||
                    !LinuxText(c->State, LinuxStaticTag + "_APP_EXIT main=") || !LinuxNumber(c->State, writer) ||
                    !LinuxText(c->State, " nanos=") || !LinuxNumber(c->State, now) ||
                    !LinuxText(c->State, " exit=0 cleanup=pending\r\n")) return false;
            }
            return true;
        }

#if M10_018_TIMING_PROBE
        // Count only actual completed user reads after the real listen. Stop
        // measurements before printing one aggregate; existing per-step evidence
        // and scheduling remain intact. This diagnostic does not drive shutdown.
        private static bool ObserveLinuxDiskTiming(LinuxNativeControl* c, ulong bytes)
        {
            fixed (NativeDiskOwner* disk = &s_nativeDisk)
            {
                if (disk->Boot != c->State) return false;
                if (bytes == 0)
                {
                    if (disk->TimingPhase != 0) return false;
                    if (!ReadNetworkTime(c->State, out ulong armed) ||
                        !disk->TimingDispatch.Arm(armed, disk->TimingNetworkTotal) ||
                        !disk->TimingDriver.Arm(armed, disk->TimingNetworkTotal) ||
                        !disk->TimingVerification.Arm(armed)) return false;
                    disk->TimingPhase = 1; return true;
                }
                if (disk->TimingPhase == 2) return true;
                if (disk->TimingPhase != 1 || bytes > 65536 || disk->TimingReadBytes > 65536 - bytes) return false;
                disk->TimingReadBytes += bytes;
                if (disk->TimingReadBytes != 65536) return true;
                if (!ReadNetworkTime(c->State, out ulong frozen) ||
                    !disk->TimingDispatch.Freeze(frozen, disk->TimingNetworkTotal) ||
                    !disk->TimingDriver.Freeze(frozen, disk->TimingNetworkTotal) ||
                    !disk->TimingVerification.Freeze(frozen) ||
                    !disk->TimingVerification.Inspect(out ulong verificationCalls, out ulong verificationNanoseconds) ||
                    !disk->TimingDispatch.Inspect(out var dispatch) || !dispatch.PhysicalDispatchActiveAtFreeze ||
                    !disk->TimingDriver.Inspect(out var driver) || !driver.PhysicalDispatchActiveAtFreeze ||
                    !disk->TimingDispatch.InspectChild(in disk->TimingDriver, out ulong driverNonPoll, out ulong shellNonPoll)) return false;
                disk->TimingPhase = 2;
                var p = disk->TimingPump;
                if (disk->TimingPostReadiness > ~0UL - disk->TimingPostWaitRecord ||
                    disk->TimingPostReadiness + disk->TimingPostWaitRecord != disk->TimingPostNetwork ||
                    disk->TimingReadinessRefresh > ~0UL - disk->TimingReadinessWaitScan ||
                    disk->TimingReadinessRefresh + disk->TimingReadinessWaitScan != disk->TimingPostReadiness) return false;
                ulong calls = 0, total = 0, pre = 0, round = 0, post = 0;
                for (uint i = 0; i < 4; i++)
                {
                    ulong a = disk->TimingCausePre[i], b = disk->TimingCauseDisk[i], d = disk->TimingCausePost[i];
                    if (a > ~0UL - b || a + b > ~0UL - d || a + b + d != disk->TimingCauseTotal[i] ||
                        disk->TimingCauseCalls[i] == 0 && disk->TimingCauseTotal[i] != 0 ||
                        calls > ~0UL - disk->TimingCauseCalls[i] || total > ~0UL - disk->TimingCauseTotal[i] ||
                        pre > ~0UL - a || round > ~0UL - b || post > ~0UL - d) return false;
                    calls += disk->TimingCauseCalls[i]; total += disk->TimingCauseTotal[i]; pre += a; round += b; post += d;
                }
                if (calls != disk->TimingNetworkCalls || calls != p.Calls || total != disk->TimingNetworkTotal ||
                    pre != disk->TimingPreNetwork || round != disk->TimingDiskRound || post != disk->TimingPostNetwork) return false;
#if CLROS_DISK_EVIDENCE_TRACE || CLROS_DISK_EVIDENCE_SUMMARY
                if (!WriteNativeDiskDiagnosticBoundary(disk, frozen, false))
                    return false;
#endif
                if (!(LinuxText(c->State, "M10_018_TIMING bytes=") && LinuxNumber(c->State, disk->TimingReadBytes) &&
                    LinuxText(c->State, " calls=") && LinuxNumber(c->State, p.Calls) &&
                    LinuxText(c->State, " steps=") && LinuxNumber(c->State, p.Steps) &&
                    LinuxText(c->State, " pumpNs=") && LinuxNumber(c->State, p.PumpNanoseconds) &&
                    LinuxText(c->State, " scanNs=") && LinuxNumber(c->State, p.ScanNanoseconds) &&
                    LinuxText(c->State, " flushNs=") && LinuxNumber(c->State, p.FlushNanoseconds) &&
                    LinuxText(c->State, " preNetworkNs=") && LinuxNumber(c->State, disk->TimingPreNetwork) &&
                    LinuxText(c->State, " postNetworkNs=") && LinuxNumber(c->State, disk->TimingPostNetwork) &&
                    LinuxText(c->State, " postReadinessNs=") && LinuxNumber(c->State, disk->TimingPostReadiness) &&
                    LinuxText(c->State, " readinessRefreshNs=") && LinuxNumber(c->State, disk->TimingReadinessRefresh) &&
                    LinuxText(c->State, " readinessWaitScanNs=") && LinuxNumber(c->State, disk->TimingReadinessWaitScan) &&
                    LinuxText(c->State, " postWaitRecordNs=") && LinuxNumber(c->State, disk->TimingPostWaitRecord) &&
                    LinuxText(c->State, " networkCalls=") && LinuxNumber(c->State, disk->TimingNetworkCalls) &&
                    LinuxText(c->State, " diskReady=") && LinuxNumber(c->State, p.DiskReady) &&
                    LinuxText(c->State, " otherReady=") && LinuxNumber(c->State, p.OtherReady) &&
                    LinuxText(c->State, " noAdvance=") && LinuxNumber(c->State, p.NoAdvance) &&
                    LinuxText(c->State, " elapsed=") && LinuxNumber(c->State, p.Elapsed) &&
                    LinuxText(c->State, " stepLimit=") && LinuxNumber(c->State, p.StepLimit) &&
                    LinuxText(c->State, " failures=") && LinuxNumber(c->State, p.Failures) &&
                    LinuxText(c->State, " flushedPastBudget=") && LinuxNumber(c->State, p.FlushedPastBudget) &&
                    LinuxText(c->State, " pollNs=") && LinuxNumber(c->State, disk->TimingNetworkTotal) &&
                    LinuxText(c->State, " diskRoundNs=") && LinuxNumber(c->State, disk->TimingDiskRound))) return false;
                for (uint i = 0; i < 4; i++)
                    if (!LinuxText(c->State, i == 0 ? " clockCalls=" : i == 1 ? " syscallCalls=" : i == 2 ? " supervisorCalls=" : " idleCalls=") ||
                        !LinuxNumber(c->State, disk->TimingCauseCalls[i]) ||
                        !LinuxText(c->State, i == 0 ? " clockNs=" : i == 1 ? " syscallNs=" : i == 2 ? " supervisorNs=" : " idleNs=") ||
                        !LinuxNumber(c->State, disk->TimingCauseTotal[i]) ||
                        !LinuxText(c->State, i == 0 ? " clockPreNs=" : i == 1 ? " syscallPreNs=" : i == 2 ? " supervisorPreNs=" : " idlePreNs=") ||
                        !LinuxNumber(c->State, disk->TimingCausePre[i]) ||
                        !LinuxText(c->State, i == 0 ? " clockDiskNs=" : i == 1 ? " syscallDiskNs=" : i == 2 ? " supervisorDiskNs=" : " idleDiskNs=") ||
                        !LinuxNumber(c->State, disk->TimingCauseDisk[i]) ||
                        !LinuxText(c->State, i == 0 ? " clockPostNs=" : i == 1 ? " syscallPostNs=" : i == 2 ? " supervisorPostNs=" : " idlePostNs=") ||
                        !LinuxNumber(c->State, disk->TimingCausePost[i])) return false;
                // The original42-field v5 record stays byte-for-byte in its
                // own schema. This separate partition ends before both dumps.
                return LinuxText(c->State, "\r\nM10_018_DISPATCH bytes=") && LinuxNumber(c->State, disk->TimingReadBytes) &&
                    LinuxText(c->State, " phaseNs=") && LinuxNumber(c->State, dispatch.PhaseNanoseconds) &&
                    LinuxText(c->State, " pollNs=") && LinuxNumber(c->State, dispatch.PollNanoseconds) &&
                    LinuxText(c->State, " dispatchNs=") && LinuxNumber(c->State, dispatch.DispatchNanoseconds) &&
                    LinuxText(c->State, " nestedPollNs=") && LinuxNumber(c->State, dispatch.NestedPollNanoseconds) &&
                    LinuxText(c->State, " dispatchNonPollNs=") && LinuxNumber(c->State, dispatch.DispatchNonPollNanoseconds) &&
                    LinuxText(c->State, " supervisorNonPollNs=") && LinuxNumber(c->State, dispatch.SupervisorNonPollNanoseconds) &&
                    LinuxText(c->State, " completed=") && LinuxNumber(c->State, dispatch.CompletedEnvelopes) &&
                    LinuxText(c->State, " clipped=") && LinuxNumber(c->State, dispatch.ClippedEnvelopes) &&
                    LinuxText(c->State, " physicalActive=") && LinuxNumber(c->State, dispatch.PhysicalDispatchActiveAtFreeze ? 1UL : 0UL) &&
                    LinuxText(c->State, " phaseComplete=1\r\nM10_018_DRIVER bytes=") && LinuxNumber(c->State, disk->TimingReadBytes) &&
                    LinuxText(c->State, " phaseNs=") && LinuxNumber(c->State, driver.PhaseNanoseconds) &&
                    LinuxText(c->State, " pollNs=") && LinuxNumber(c->State, driver.PollNanoseconds) &&
                    LinuxText(c->State, " driverNs=") && LinuxNumber(c->State, driver.DispatchNanoseconds) &&
                    LinuxText(c->State, " nestedPollNs=") && LinuxNumber(c->State, driver.NestedPollNanoseconds) &&
                    LinuxText(c->State, " driverNonPollNs=") && LinuxNumber(c->State, driverNonPoll) &&
                    LinuxText(c->State, " dispatchShellNonPollNs=") && LinuxNumber(c->State, shellNonPoll) &&
                    LinuxText(c->State, " completed=") && LinuxNumber(c->State, driver.CompletedEnvelopes) &&
                    LinuxText(c->State, " clipped=") && LinuxNumber(c->State, driver.ClippedEnvelopes) &&
                    LinuxText(c->State, " physicalActive=") && LinuxNumber(c->State, driver.PhysicalDispatchActiveAtFreeze ? 1UL : 0UL) &&
                    LinuxText(c->State, " phaseComplete=1\r\nM10_018_VERIFY calls=") && LinuxNumber(c->State, verificationCalls) &&
                    LinuxText(c->State, " elapsedNs=") && LinuxNumber(c->State, verificationNanoseconds) &&
                    LinuxText(c->State, " phaseNs=") && LinuxNumber(c->State, dispatch.PhaseNanoseconds) &&
                    LinuxText(c->State, " phaseComplete=1\r\n");
            }
        }
#endif

        // Enter only on the restored supervisor. The deadline proposes one
        // real directed kernel SIGTERM; it never declares HTTP completion or
        // stages process cancellation. Runtime waits/pipe work keep progressing.
        private static bool LinuxStaticSupervisor(LinuxNativeControl* c, ulong now)
        {
            if (c->Selected != 0 || c->Threads.Current.IsValid || c->Threads.HandoffPending || c->Timer.IsPrepared ||
                c->StaticMain.Value != c->StaticApp.MainGeneration) return false;
            var progress = c->StaticApp.Poll(now);
            if (progress == LinuxStaticFileObservation.Progress.RequestTermination)
                return c->Process.RequestTermination(c->Key, 2, c->StaticMain) && c->StaticApp.RecordRequest(c->StaticMain.Value, now) &&
                    LinuxText(c->State, LinuxStaticTag + "_TERMINATE main=") && LinuxNumber(c->State, c->StaticMain.Value) &&
                    LinuxText(c->State, " signal=15 origin=128 nanos=") && LinuxNumber(c->State, now) &&
                    LinuxText(c->State, " tableClock=") && LinuxNumber(c->State, c->Threads.ObservedTicks) &&
                    LinuxText(c->State, " owner=supervisor completion=pending\r\n");
            return progress is LinuxStaticFileObservation.Progress.Waiting or LinuxStaticFileObservation.Progress.Draining or
                LinuxStaticFileObservation.Progress.Exited;
        }
    }
}
#endif
