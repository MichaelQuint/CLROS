#if M10_004_PROBE
using System;
using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Boot;

public static unsafe partial class EfiEntry
{
    private static partial class M03FirstExitAdapter
    {
        // Observations belong to the existing serialized native owner. They do
        // not parse HTTP, manufacture app output, authorize teardown or prove
        // application environment consumption. The external /config and HTTP
        // observations must join this exact admitted package and live process.
        private static bool LinuxWebObserveCall(LinuxNativeControl* c, LinuxSyscallFrame call, LinuxSyscallResult result)
        {
            if (c == null || c->Phase != 3 || c->Published != 1 || c->State->NetworkPhase != 2 ||
                !c->Threads.Current.IsValid || c->Process.ActiveThreads == 0) return false;
            Storage* state = c->State;
            var descriptors = state->LinuxDescriptors.Owner;
            if (descriptors.ViewCount != 2 || descriptors.Views[1].Inode != 257 ||
                descriptors.Views[1].Bytes == 0 || descriptors.Views[1].Bytes > 65536) return false;
            if ((c->WebFlags & 1) == 0)
            {
                if (!LinuxText(state, "TEST_START m10.006\r\nM10_004_ENTER identity=") || !LinuxNumber(state, c->Threads.Current.Value) ||
                    !LinuxText(state, " rip=") || !LinuxNumber(state, call.Rip) ||
                    !LinuxText(state, " argc=") || !LinuxNumber(state, state->LinuxInitialStack.ArgumentCount) ||
                    !LinuxText(state, " envc=") || !LinuxNumber(state, state->LinuxInitialStack.EnvironmentCount) ||
                    !LinuxText(state, " configBytes=") || !LinuxNumber(state, descriptors.Views[1].Bytes) ||
                    !LinuxText(state, "\r\n")) return false;
                c->WebFlags |= 1;
            }
            if (c->WebStartup.Failed)
            { LinuxText(state, "TEST_FAIL m10.006 startup-observation\r\n"); return false; }
            if (!c->WebLogs.RecordCall(c->Threads.Current.Value, call, result))
            { LinuxText(state, "TEST_FAIL m10.005 log-observation\r\n"); return false; }
            if (c->WebLogs.Complete && (c->WebFlags & 64) == 0)
            {
                if ((c->WebFlags & 48) != 48 || c->Failed != 0 || c->Stopping != 0 ||
                    c->Process.Failed || state->LinuxSyscallFailed != 0 || state->LinuxSyscallFaults != 0)
                { LinuxText(state, "TEST_FAIL m10.005 log-lifetime\r\n"); return false; }
                if (!LinuxText(state, "M10_005_LOGS stdout=") || !LinuxNumber(state, c->WebLogs.StdoutRecords) ||
                    !LinuxText(state, " stderr=") || !LinuxNumber(state, c->WebLogs.StderrRecords) ||
                    !LinuxText(state, " writers=8 writev=") || !LinuxNumber(state, c->WebLogs.WritevRecords) ||
                    !LinuxText(state, " invalidErrno=9\r\nCLROS_M10_005_READY\r\nTEST_PASS m10.005\r\n")) return false;
                c->WebFlags |= 64;
            }
            if (result.Action == LinuxSyscallAction.Return && result.Value == -12 && call.Number is 9 or 10 or 11 or 12 or 28)
            {
                // Snapshot only committed owner counters after an atomic errno
                // return. Distinguish physical and interval ceilings before any
                // policy change; this observation neither retries nor authorizes it.
                if (!LinuxText(state, "M10_004_MEMORY nr=") || !LinuxNumber(state, call.Number) ||
                    !LinuxText(state, " regions=") || !LinuxNumber(state, state->LinuxMemory.Count) ||
                    !LinuxText(state, " regionLimit=") || !LinuxNumber(state, LinuxMemoryServices.MaximumRegions) ||
                    !LinuxText(state, " backed=") || !LinuxNumber(state, state->LinuxMemory.BackedPages) ||
                    !LinuxText(state, " pool=") || !LinuxNumber(state, state->LinuxMemory.Memory.PoolPages) ||
                    !LinuxText(state, " reserved=") || !LinuxNumber(state, state->LinuxMemory.ReservedBytes) ||
                    !LinuxText(state, "\r\n")) return false;
            }
            // A rejected bind is still the first bind attempt. Observe it before
            // the general errno return so later output/retry cannot erase failure.
            if (call.Number == 49)
            {
                if (!c->WebStartup.RecordBind(c->Threads.Current.Value,
                    result.Action == LinuxSyscallAction.Return && result.Value == 0 &&
                    call.Argument0 < LinuxDescriptorServices.MaximumDescriptors))
                { LinuxText(state, "TEST_FAIL m10.006 bind-observation\r\n"); return false; }
                c->WebBoundFd = (uint)call.Argument0 + 1;
                if (!LinuxText(state, "M10_006_BIND identity=") || !LinuxNumber(state, c->Threads.Current.Value) ||
                    !LinuxText(state, " fd=") || !LinuxNumber(state, call.Argument0) || !LinuxText(state, "\r\n")) return false;
            }
            if (result.Action != LinuxSyscallAction.Return || result.Value < 0) return true;
            if ((c->WebFlags & 2) == 0 && result.Value > 0 && call.Number is 0 or 17 or 19 &&
                call.Argument0 < descriptors.DescriptorCount)
            {
                uint fd = (uint)call.Argument0, slot = descriptors.Slots[fd].Description;
                if (slot != 0 && slot <= descriptors.DescriptionCount)
                {
                    var file = descriptors.Descriptions[slot - 1]; var expected = descriptors.Views[1];
                    ulong count = (ulong)result.Value;
                    bool position = call.Number == 17 ? call.Argument3 == c->WebConfigBytes :
                        file.Position >= count && file.Position - count == c->WebConfigBytes;
                    if (file.References != 0 && file.Inode == expected.Inode && file.Data == expected.Data &&
                        file.Bytes == expected.Bytes && position && count <= expected.Bytes - c->WebConfigBytes)
                    {
                        c->WebConfigBytes += count;
                        if (c->WebConfigBytes == expected.Bytes)
                        {
                            c->WebFlags |= 2;
                            if (!LinuxText(state, "M10_004_CONFIG fd=") || !LinuxNumber(state, fd) ||
                                !LinuxText(state, " bytes=") || !LinuxNumber(state, c->WebConfigBytes) ||
                                !LinuxText(state, " source=immutable-package copied=user\r\n")) return false;
                        }
                    }
                }
            }
            if (call.Number == 50 && result.Value == 0 && c->WebBoundFd != 0 && call.Argument0 + 1 == c->WebBoundFd)
            {
                c->WebFlags |= 4;
                if (!LinuxText(state, "M10_004_LISTEN fd=") || !LinuxNumber(state, call.Argument0) ||
                    !LinuxText(state, " address=0 port=8080 owner=live\r\n")) return false;
            }
            if (call.Number == 3 && result.Value == 0 && c->WebBoundFd != 0 && call.Argument0 + 1 == c->WebBoundFd)
            {
                if ((c->WebFlags & 16) != 0)
                { _ = LinuxText(state, "TEST_FAIL m10.004 listener_closed\r\n"); return false; }
                c->WebBoundFd = 0; c->WebFlags &= ~4U;
            }
            return LinuxWebPublishLive(c);
        }

        // Called only after the actual owned serial write completes. Match a
        // complete standard lifetime message across fd1 fragments; fd2 is counted
        // separately. This is a startup observation, never external HTTP proof.
        private static bool LinuxWebObserveOutput(Storage* state, uint channel, byte* source, uint bytes)
        {
            LinuxNativeControl* c = LinuxNativeOwner(state);
            if (c == null || c->Phase != 3 || c->Published != 1 || source == null || bytes == 0 || bytes > 8192) return false;
            if (!c->WebLogs.Observe(channel, c->Threads.Current.Value, source, bytes)) return false;
            if ((c->WebFlags & 32) == 0 && c->WebLogs.StdoutRecords + c->WebLogs.StderrRecords != 0)
            {
                if (!LinuxText(state, "TEST_START m10.005\r\n")) return false;
                c->WebFlags |= 32;
            }
            if (channel == 2)
            {
                bool wasComplete = c->WebStartup.StartupComplete;
                if (c->WebStderrBytes > ulong.MaxValue - bytes ||
                    !c->WebStartup.Observe(c->Threads.Current.Value, source, bytes)) return false;
                c->WebStderrBytes += bytes;
                if (!wasComplete && c->WebStartup.StartupComplete &&
                    (!LinuxText(state, "M10_006_STARTUP writer=") || !LinuxNumber(state, c->WebStartup.WriterIdentity) ||
                    !LinuxText(state, " stages=4\r\n"))) return false;
                return true;
            }
            if (channel != 1 || c->WebStdoutBytes > ulong.MaxValue - bytes) return false;
            c->WebStdoutBytes += bytes;
            string started = "\n      Application started. Press Ctrl+C to shut down.\n";
            for (uint i = 0; i < bytes && (c->WebFlags & 8) == 0; i++)
            {
                byte value = source[i]; if (value == 13) continue;
                if (value == started[(int)c->WebStartMatch]) c->WebStartMatch++;
                else c->WebStartMatch = value == started[0] ? 1U : 0U;
                if (c->WebStartMatch == started.Length) { c->WebFlags |= 8; c->WebStartMatch = 0; }
            }
            return LinuxWebPublishLive(c);
        }

        private static bool LinuxWebPublishLive(LinuxNativeControl* c)
        {
            if ((c->WebFlags & 16) != 0 || (c->WebFlags & 15) != 15) return true;
            if (c->Failed != 0 || c->Stopping != 0 || c->Process.Failed || c->Process.ActiveThreads == 0 ||
                c->State->LinuxSyscallFailed != 0 || c->State->LinuxSyscallFaults != 0 ||
                !c->WebStartup.StartupComplete || !c->WebStartup.SuccessfulBind) return false;
            Storage* state = c->State;
            if (!LinuxText(state, "M10_004_LIVE argc=") || !LinuxNumber(state, state->LinuxInitialStack.ArgumentCount) ||
                !LinuxText(state, " envc=") || !LinuxNumber(state, state->LinuxInitialStack.EnvironmentCount) ||
                !LinuxText(state, " configBytes=") || !LinuxNumber(state, c->WebConfigBytes) ||
                !LinuxText(state, " bindFd=") || !LinuxNumber(state, c->WebBoundFd - 1) ||
                !LinuxText(state, " stdoutBytes=") || !LinuxNumber(state, c->WebStdoutBytes) ||
                !LinuxText(state, " stderrBytes=") || !LinuxNumber(state, c->WebStderrBytes) ||
                !LinuxText(state, " active=") || !LinuxNumber(state, c->Process.ActiveThreads) ||
                !LinuxText(state, " clock=") || !LinuxNumber(state, c->Threads.ObservedTicks) ||
                !LinuxText(state, " owner=retained externalHttp=required\r\nCLROS_M10_004_READY\r\nTEST_PASS m10.004\r\n" +
                    "CLROS_M10_006_READY\r\nTEST_PASS m10.006\r\n")) return false;
            c->WebFlags |= 16; return true;
        }

        // Entered only on the owned supervisor after a rejected setup or native
        // return. Keep its stack, mappings, DMA and any unresolved handoff alive.
        // The same terminal spin is used by existing native failure callbacks;
        // no guessed restoration, detach, reclaim or successful exit is reported.
        private static bool LinuxWebRetainFailure(Storage* state, LinuxNativeControl* c, uint stage)
        {
            _ = LinuxText(state, "TEST_FAIL m10.004 retained_owner stage=") && LinuxNumber(state, stage) &&
                LinuxText(state, " active=") && LinuxNumber(state, c == null ? 0UL : c->Process.ActiveThreads) &&
                LinuxText(state, " current=") && LinuxNumber(state, c == null ? 0UL : c->Threads.Current.Value) &&
                LinuxText(state, " handoff=") && LinuxNumber(state, c != null && c->Threads.HandoffPending ? 1UL : 0UL) &&
                LinuxText(state, " cleanup=withheld\r\n");
            while (true) { }
        }
    }
}
#endif
