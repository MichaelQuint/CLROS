using System.Runtime.InteropServices;
using OS.Boot;
using OS.Tooling;

internal static unsafe partial class WorkloadServiceLoopTests
{
    private static WorkloadServiceLoopRequest Request() => new()
    {
        FrequencyHz = 1000000,
        IntervalMilliseconds = 1,
        MinimumIterations = 3,
        MaximumIterations = 8,
        PollLimit = 4,
        FirmwareExited = true,
        RetainedPointersCleared = true,
        OwnedMappingsValidated = true
    };
    private static WorkloadServiceLoopCallbacks Callbacks(Context* context) => new()
    {
        Context = context,
        ReadClock = &Read,
        Service = &Service,
        Stop = &Stop
    };
    [UnmanagedCallersOnly]
    private static byte Read(void* pointer, ulong* value)
    {
        Context* c = (Context*)pointer;
        if (++c->ClockCalls == c->FailClockAt)
            return 0;
        if (c->ClockCalls != 1)
            c->Now = c->Regress ? c->Now - 1 : c->Now + c->Step;
        *value = c->Now;
        return 1;
    }

    [UnmanagedCallersOnly]
    private static byte Service(void* pointer, WorkloadServiceLoopState* state, uint iteration, ulong ticks)
    {
        Context* c = (Context*)pointer;
        c->Services++;
        if (state != c->Original || iteration != c->Services || ticks - c->LastService < 1000)
            return 0;
        c->LastService = ticks;
        if (iteration == 1)
            c->EarlyRefused = !WorkloadServiceLoop.RequestShutdown(ref *state);
        if (c->WrongState)
        {
            *state = default;
            return 1;
        }

        if (iteration == c->FailServiceAt)
            return 0;
        if (iteration == c->StopAt && !WorkloadServiceLoop.RequestShutdown(ref *state))
            return 0;
        return 1;
    }

    [UnmanagedCallersOnly]
    private static byte Stop(void* pointer, uint iterations, ulong ticks)
    {
        Context* c = (Context*)pointer;
        c->StopCalls++;
        if (c->StopChangesState)
        {
            *c->Original = default;
            return 1;
        }

        return !c->FailStop && iterations == c->StopAt && ticks == c->LastService && c->Original->Status == WorkloadServiceStatus.ShutdownRequested ? (byte)1 : (byte)0;
    }

    [HostTest("m16-003 cooperative timed steps stop only through the original controller", HostLane.Fast, 0)]
    private static void TimedStop()
    {
        WorkloadServiceLoopState state = default;
        Context context = new()
        {
            Step = 250,
            StopAt = 4,
            Original = &state
        };
        HostCheck.Equal(WorkloadServiceStatus.Stopped, WorkloadServiceLoop.Run(ref state, Request(), Callbacks(&context)));
        HostCheck.True(state.IsStopped && state.Iterations == 4 && context.Services == 4 && context.StopCalls == 1 && context.EarlyRefused);
        HostCheck.Equal(17U, state.ClockReads);
        HostCheck.Equal(4000UL, state.LastTicks);
        HostCheck.Equal(1000UL, state.IntervalTicks);
        uint calls = context.ClockCalls;
        HostCheck.Equal(WorkloadServiceStatus.AlreadyConsumed, WorkloadServiceLoop.Run(ref state, Request(), Callbacks(&context)));
        HostCheck.True(context.ClockCalls == calls && state.IsStopped && !WorkloadServiceLoop.RequestShutdown(ref state));
    }

    [HostTest("m16-003 admission rejects defaults unsafe authority and missing callbacks before mutation", HostLane.Fast, 1)]
    private static void Admission()
    {
        for (int variant = 0; variant < 17; variant++)
        {
            WorkloadServiceLoopState state = default;
            Context context = new()
            {
                Step = 1000,
                StopAt = 4,
                Original = &state
            };
            var request = Request();
            var callbacks = Callbacks(&context);
            switch (variant)
            {
                case 0:
                    request = default;
                    break;
                case 1:
                    request.FirmwareExited = false;
                    break;
                case 2:
                    request.RetainedPointersCleared = false;
                    break;
                case 3:
                    request.OwnedMappingsValidated = false;
                    break;
                case 4:
                    request.FrequencyHz = 999999;
                    break;
                case 5:
                    request.FrequencyHz = 10000000001;
                    break;
                case 6:
                    request.IntervalMilliseconds = 0;
                    break;
                case 7:
                    request.IntervalMilliseconds = 1001;
                    break;
                case 8:
                    request.MinimumIterations = 1;
                    break;
                case 9:
                    request.MaximumIterations = 2;
                    break;
                case 10:
                    request.MaximumIterations = 65;
                    break;
                case 11:
                    request.PollLimit = 0;
                    break;
                case 12:
                    request.PollLimit = 1000001;
                    break;
                case 13:
                    callbacks.ReadClock = null;
                    break;
                case 14:
                    callbacks.Context = null;
                    break;
                case 15:
                    callbacks.Service = null;
                    break;
                default:
                    callbacks.Stop = null;
                    break;
            }

            HostCheck.Equal(WorkloadServiceStatus.InvalidAdmission, WorkloadServiceLoop.Run(ref state, request, callbacks));
            HostCheck.True(state.Status == WorkloadServiceStatus.Uninitialized && state.ClockReads == 0 && context.ClockCalls == 0);
        }
    }

    [HostTest("m16-003 clock stall regression failure and deadline overflow consume the attempt", HostLane.Fast, 2)]
    private static void ClockFailures()
    {
        for (int variant = 0; variant < 5; variant++)
        {
            WorkloadServiceLoopState state = default;
            Context context = new()
            {
                Original = &state
            };
            WorkloadServiceStatus expected;
            if (variant == 0)
                expected = WorkloadServiceStatus.DeadlineExhausted;
            else if (variant == 1)
            {
                context.Now = 10000;
                context.Regress = true;
                expected = WorkloadServiceStatus.ClockRegressed;
            }
            else if (variant == 2)
            {
                context.FailClockAt = 2;
                expected = WorkloadServiceStatus.ClockFailed;
            }
            else if (variant == 3)
            {
                context.Now = ulong.MaxValue - 999;
                expected = WorkloadServiceStatus.DeadlineOverflow;
            }
            else
            {
                context.FailClockAt = 1;
                expected = WorkloadServiceStatus.ClockFailed;
            }

            HostCheck.Equal(expected, WorkloadServiceLoop.Run(ref state, Request(), Callbacks(&context)));
            HostCheck.True(!state.IsStopped && context.Services == 0 && context.StopCalls == 0 && state.ClockReads <= 5);
            HostCheck.Equal(WorkloadServiceStatus.AlreadyConsumed, WorkloadServiceLoop.Run(ref state, Request(), Callbacks(&context)));
        }
    }

    [HostTest("m16-003 no shutdown request or failing callbacks cannot report stopped", HostLane.Fast, 3)]
    private static void CallbackFailures()
    {
        for (int variant = 0; variant < 5; variant++)
        {
            WorkloadServiceLoopState state = default;
            Context context = new()
            {
                Step = 1000,
                Original = &state
            };
            WorkloadServiceStatus expected;
            if (variant == 0)
                expected = WorkloadServiceStatus.ShutdownMissing;
            else if (variant == 1)
            {
                context.FailServiceAt = 2;
                expected = WorkloadServiceStatus.ServiceFailed;
            }
            else if (variant == 2)
            {
                context.StopAt = 4;
                context.FailStop = true;
                expected = WorkloadServiceStatus.ShutdownFailed;
            }
            else if (variant == 3)
            {
                context.WrongState = true;
                expected = WorkloadServiceStatus.StateChanged;
            }
            else
            {
                context.StopAt = 4;
                context.StopChangesState = true;
                expected = WorkloadServiceStatus.StateChanged;
            }

            HostCheck.Equal(expected, WorkloadServiceLoop.Run(ref state, Request(), Callbacks(&context)));
            HostCheck.True(!state.IsStopped && state.Status == expected && context.StopCalls <= 1);
        }
    }

    [HostTest("m16-003 routing requires staged owned exit and rejects legacy profile combinations", HostLane.Fast, 5)]
    private static void Routing()
    {
        var plan = QemuProfiles.Create(QemuSmokeOptions.Parse("-BuildEfi", "-EnableM16003Probe", "-DryRun"));
        HostCheck.True(plan.BootPolicy == "Diagnostic" && plan.IsEnabled("EnableM16002Probe") && plan.IsEnabled("EnableM03FirstRealExitBootServicesProbe") && plan.IsEnabled("EnableM03OwnedSubsystemSurvivalProbe") && !plan.IsEnabled("EnableM05MonotonicClockProbe") && !plan.IsEnabled("EnableM03FirmwareAdapterRevocationProbe"));
        HostCheck.True(plan.Ordered.Contains("TEST_PASS m16-003") && plan.Unique.Contains("M16_003_ITERATION index=4"));
        foreach (string[] args in new string[][]
        {
            ["-EnableM16003Probe", "-EnableM16002Probe=false"],
            ["-EnableM16003Probe", "-EnableM05MonotonicClockProbe"],
            ["-EnableM16003Probe", "-EnableM03FirmwareAdapterRevocationProbe"],
            ["-EnableM16003Probe", "-BootPolicy", "VerifiedExit"],
            ["-EnableM16003Probe", "-LinuxElfImageVariant", "DigestFault"],
            ["-EnableM16003Probe", "-RequireMarker", "CLROS_M16_002_READY"]
        }

        )
        {
            bool rejected = false;
            try
            {
                _ = QemuProfiles.Create(QemuSmokeOptions.Parse(["-BuildEfi", "-DryRun", ..args]));
            }
            catch (ToolFailure)
            {
                rejected = true;
            }

            HostCheck.True(rejected);
        }

        var ordinary = EfiProfile.Create("VerifiedExit", new Dictionary<string, bool>());
        HostCheck.True(!ordinary.IsEnabled("EnableM16003Probe") && !ordinary.IsEnabled("EnableM16002Probe"));
    }

    [HostTest("m16-003 independent transcript requires elapsed ticks copied-handle denials and owned shutdown", HostLane.Fast, 6)]
    private static void Transcript()
    {
        string[] tail = ["TEST_START m16-003", "M16_003_CLOCK frequency=10000000 period=100000000 recoveries=0 source=owned-hpet", ..Enumerable.Range(1, 4).Select(i => $"M16_003_ITERATION index={i} ticks={i * 10000 + 50}"), ..Enumerable.Range(0, 8).Select(i => $"M16_003_FIRMWARE_DENIED index={i} status=ServicesUnavailable providers=1 outputs=unchanged"), "M16_003_STOP iterations=4 reads=65 first=50 last=40050 interval=10000 firmware=revoked providers=1 shutdown=owned-controller", "CLROS_M16_003_READY", "TEST_PASS m16-003"];
        string[] rows = [..Enumerable.Repeat("parent checked separately", QemuContracts.M03OwnedSubsystemMarkers.Count + 7), ..tail];
        HostCheck.Equal(rows.Length - 17, QemuTranscripts.ValidateServiceLoopTail(rows).Length);
        string[] recovered = rows.ToArray();
        recovered[recovered.Length - 16] = tail[1].Replace("recoveries=0", "recoveries=1", StringComparison.Ordinal);
        HostCheck.Equal(rows.Length - 17, QemuTranscripts.ValidateServiceLoopTail(recovered).Length);
        foreach (var mutation in new (int Index, string Text)[]
        {
            (1, tail[1].Replace("10000000", "9999999", StringComparison.Ordinal)),
            (1, tail[1].Replace("recoveries=0", "recoveries=2", StringComparison.Ordinal)),
            (2, "M16_003_ITERATION index=1 ticks=10049"),
            (3, "M16_003_ITERATION index=1 ticks=20050"),
            (6, tail[6].Replace("providers=1", "providers=2", StringComparison.Ordinal)),
            (7, tail[7].Replace("outputs=unchanged", "outputs=changed", StringComparison.Ordinal)),
            (14, tail[14].Replace("last=40050", "last=40049", StringComparison.Ordinal)),
            (14, tail[14].Replace("reads=65", "reads=16386", StringComparison.Ordinal)),
            (14, tail[14].Replace("owned-controller", "firmware", StringComparison.Ordinal)),
            (16, "TEST_PASS m16-002")
        }

        )
        {
            string[] bad = rows.ToArray();
            bad[bad.Length - 17 + mutation.Index] = mutation.Text;
            bool rejected = false;
            try
            {
                _ = QemuTranscripts.ValidateServiceLoopTail(bad);
            }
            catch (ToolFailure)
            {
                rejected = true;
            }

            HostCheck.True(rejected);
        }

        bool shortRejected = false;
        try
        {
            _ = QemuTranscripts.ValidateServiceLoopTail(rows[..^1]);
        }
        catch (ToolFailure)
        {
            shortRejected = true;
        }

        HostCheck.True(shortRejected);
    }

    [HostTest("m16-003 ceil tick conversion and zero allocation are independent of whole MHz", HostLane.Fast, 4)]
    private static void FractionalFrequency()
    {
        WorkloadServiceLoopState state = default;
        Context context = new()
        {
            Step = 1001,
            StopAt = 4,
            Original = &state
        };
        var request = Request();
        request.FrequencyHz = 1000001;
        long before = GC.GetAllocatedBytesForCurrentThread();
        var result = WorkloadServiceLoop.Run(ref state, request, Callbacks(&context));
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        HostCheck.Equal(WorkloadServiceStatus.Stopped, result);
        HostCheck.True(state.IntervalTicks == 1001 && state.LastTicks == 4004 && bytes == 0);
    }
}
