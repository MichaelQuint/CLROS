using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Kernel.Platform;
using OS.Kernel.Runtime;

internal static class BootSequencePlanTests
{

    [HostTest("boot.sequence plan exposes ordered marker bytes", HostLane.Fast, 0)]
    private static bool TestOrderedMarkerBytes()
    {
        string[] expected =
        [
            "TEST_START boot.sequence",
            "BOOT_PHASE PhaseA.Audit START",
            "BOOT_PHASE PhaseA.Audit PASS",
            "BOOT_PHASE PhaseA.Diagnostics START",
            "BOOT_PHASE PhaseA.Diagnostics PASS",
            SerialSmokeMarkers.BootOk,
            SerialSmokeMarkers.TestPass + " boot.sequence",
            SerialSmokeMarkers.OsSmokePass
        ];

        if (BootSequencePlan.NonSmokeLineCount != 5 || BootSequencePlan.LineCount != expected.Length)
        {
            return HostCheck.Result(false);
        }

        for (int lineIndex = 0; lineIndex < expected.Length; lineIndex++)
        {
            if (BootSequence.CreatePlanLine(lineIndex) != expected[lineIndex])
            {
                return HostCheck.Result(false);
            }
        }

        return HostCheck.Result(BootSequence.CreatePlanLine(BootSequencePlan.LineCount).Length == 0);
    }

    [HostTest("boot.sequence M01 stage-one plan exposes exact success bytes", HostLane.Fast, 1)]
    private static bool TestM01StageOneSuccessMarkerBytes()
    {
        string[] expected =
        [
            "TEST_START m01.stage1",
            "M01_LOADED_IMAGE_CAPTURED",
            "M01_STACK_SWITCH_STUB_READY",
            "M01_STACK_ARENA_ALLOCATED",
            "M01_STACK_GUARD_RESERVED",
            "M01_STAGE1_MAP_RECAPTURED",
            "M01_STAGE1_RESERVATIONS_VALIDATED",
            "M01_OWNED_STACK_READY",
            "M01_OWNED_STACK_SWITCHED",
            "M01_RSP_CONTAINED",
            "TEST_PASS m01.stage1"
        ];

        if (BootSequencePlan.M01StageOneSuccessLineCount != expected.Length)
        {
            return HostCheck.Result(false);
        }

        for (int lineIndex = 0; lineIndex < expected.Length; lineIndex++)
        {
            string expectedLine = expected[lineIndex];
            if (BootSequencePlan.GetM01StageOneSuccessLineLength(lineIndex) != expectedLine.Length
                || BootSequencePlan.GetM01StageOneSuccessLineByte(lineIndex, -1) != 0)
            {
                return HostCheck.Result(false);
            }

            for (int offset = 0; offset < expectedLine.Length; offset++)
            {
                if (BootSequencePlan.GetM01StageOneSuccessLineByte(lineIndex, offset)
                    != (byte)expectedLine[offset])
                {
                    return HostCheck.Result(false);
                }
            }

            if (BootSequencePlan.GetM01StageOneSuccessLineByte(lineIndex, expectedLine.Length) != 0)
            {
                return HostCheck.Result(false);
            }
        }

        return HostCheck.Result(BootSequencePlan.GetM01StageOneSuccessLineLength(-1) == 0
            && BootSequencePlan.GetM01StageOneSuccessLineLength(expected.Length) == 0
            && BootSequencePlan.GetM01StageOneSuccessLineByte(-1, 0) == 0
            && BootSequencePlan.GetM01StageOneSuccessLineByte(expected.Length, 0) == 0);
    }

    [HostTest("boot.sequence M01 stage-two plan exposes exact success bytes", HostLane.Fast, 4)]
    private static bool TestM01StageTwoSuccessMarkerBytes()
    {
        string[] expected =
        [
            "TEST_START m01.stage2",
            "M01_ALLOCATOR_ARENA_ALLOCATED",
            "M01_STAGE2_MAP_RECAPTURED",
            "M01_RESERVATIONS_APPLIED",
            "M01_LIVE_ALLOCATED_4K",
            "M01_LIVE_PAGE_WITHIN_ARENA",
            "M01_LIVE_FREED_4K",
            "M01_DOUBLE_FREE_REJECTED",
            "TEST_PASS m01.stage2"
        ];

        if (BootSequencePlan.M01StageTwoSuccessLineCount != expected.Length)
        {
            return HostCheck.Result(false);
        }

        for (int lineIndex = 0; lineIndex < expected.Length; lineIndex++)
        {
            string expectedLine = expected[lineIndex];
            if (BootSequencePlan.GetM01StageTwoSuccessLineLength(lineIndex)
                    != expectedLine.Length
                || BootSequencePlan.GetM01StageTwoSuccessLineByte(
                    lineIndex,
                    -1) != 0
                || !string.Equals(
                    BootSequence.CreateM01StageTwoSuccessLine(lineIndex),
                    expectedLine,
                    StringComparison.Ordinal))
            {
                return HostCheck.Result(false);
            }

            for (int offset = 0; offset < expectedLine.Length; offset++)
            {
                if (BootSequencePlan.GetM01StageTwoSuccessLineByte(
                        lineIndex,
                        offset)
                    != (byte)expectedLine[offset])
                {
                    return HostCheck.Result(false);
                }
            }

            if (BootSequencePlan.GetM01StageTwoSuccessLineByte(
                    lineIndex,
                    expectedLine.Length) != 0)
            {
                return HostCheck.Result(false);
            }
        }

        return HostCheck.Result(BootSequencePlan.GetM01StageTwoSuccessLineLength(-1) == 0
            && BootSequencePlan.GetM01StageTwoSuccessLineLength(
                expected.Length) == 0
            && BootSequencePlan.GetM01StageTwoSuccessLineByte(-1, 0) == 0
            && BootSequencePlan.GetM01StageTwoSuccessLineByte(
                expected.Length,
                0) == 0
            && BootSequence.CreateM01StageTwoSuccessLine(-1).Length == 0
            && BootSequence.CreateM01StageTwoSuccessLine(
                expected.Length).Length == 0);
    }

    [HostTest("boot.sequence M01 stage-three plan exposes exact success bytes", HostLane.Fast, 7)]
    private static bool TestM01StageThreeSuccessMarkerBytes()
    {
        string[] expected =
        [
            "TEST_START m01.stage3",
            "M01_CR3_STUB_PAGE_ALLOCATED",
            "M01_PROTECTION_PROBE_PAGE_ALLOCATED",
            "M01_PAGE_TABLE_STATE_ARENA_ALLOCATED",
            "M01_STAGE3_MAP_RECAPTURED",
            "M01_CR3_STUB_MATERIALIZED",
            "M01_PAGE_TABLES_BUILT",
            "M01_PAGING_MODE_VALIDATED",
            "M01_PRE_CR3",
            "M01_FIRMWARE_CR3_RESTORED",
            "M01_RFLAGS_RESTORED",
            "M01_PGE_CYCLE_VERIFIED",
            "M01_OWNED_CR3_CANARY_VERIFIED",
            "M01_POST_CR3",
            "M01_RW_TO_RX_VERIFIED",
            "M01_TLB_INVALIDATION_COMPLETE",
            "M01_RX_PROBE_EXECUTED",
            "M01_PAGE_ZERO_ABSENT",
            "TEST_PASS m01.stage3",
            "CLROS_M01_LIVE_BOOT_MEMORY_READY"
        ];

        if (BootSequencePlan.M01StageThreeSuccessLineCount
            != expected.Length)
        {
            return HostCheck.Result(false);
        }

        for (int lineIndex = 0;
            lineIndex < expected.Length;
            lineIndex++)
        {
            string expectedLine = expected[lineIndex];
            if (BootSequencePlan.GetM01StageThreeSuccessLineLength(
                    lineIndex) != expectedLine.Length
                || BootSequencePlan.GetM01StageThreeSuccessLineByte(
                    lineIndex,
                    -1) != 0
                || !string.Equals(
                    BootSequence.CreateM01StageThreeSuccessLine(
                        lineIndex),
                    expectedLine,
                    StringComparison.Ordinal))
            {
                return HostCheck.Result(false);
            }

            for (int offset = 0;
                offset < expectedLine.Length;
                offset++)
            {
                if (BootSequencePlan.GetM01StageThreeSuccessLineByte(
                        lineIndex,
                        offset) != (byte)expectedLine[offset])
                {
                    return HostCheck.Result(false);
                }
            }

            if (BootSequencePlan.GetM01StageThreeSuccessLineByte(
                    lineIndex,
                    expectedLine.Length) != 0)
            {
                return HostCheck.Result(false);
            }
        }

        return HostCheck.Result(BootSequencePlan.GetM01StageThreeSuccessLineLength(-1)
                == 0
            && BootSequencePlan.GetM01StageThreeSuccessLineLength(
                expected.Length) == 0
            && BootSequencePlan.GetM01StageThreeSuccessLineByte(
                -1,
                0) == 0
            && BootSequencePlan.GetM01StageThreeSuccessLineByte(
                expected.Length,
                0) == 0
            && BootSequence.CreateM01StageThreeSuccessLine(-1).Length
                == 0
            && BootSequence.CreateM01StageThreeSuccessLine(
                expected.Length).Length == 0);
    }

    [HostTest("boot.sequence M01 stage-three presenter emits exact success transcript", HostLane.Fast, 8)]
    private static bool TestM01StageThreePresenterEmitsExactSuccessTranscript()
    {
        M01LivePagingIntegrationResult integration =
            M01LivePagingIntegrationTests
                .CreateCanonicalSuccessForPresenterTests();
        SequenceTestSerialSink serial =
            new SequenceTestSerialSink();
        BootEventRingBuffer events =
            new BootEventRingBuffer(32);
        BootResult result =
            BootSequence.RunM01LivePagingIntegration(
                integration,
                serial,
                events);
        string[] expected =
        [
            "TEST_START m01.stage3",
            "M01_CR3_STUB_PAGE_ALLOCATED",
            "M01_PROTECTION_PROBE_PAGE_ALLOCATED",
            "M01_PAGE_TABLE_STATE_ARENA_ALLOCATED",
            "M01_STAGE3_MAP_RECAPTURED",
            "M01_CR3_STUB_MATERIALIZED",
            "M01_PAGE_TABLES_BUILT",
            "M01_PAGING_MODE_VALIDATED",
            "M01_PRE_CR3",
            "M01_FIRMWARE_CR3_RESTORED",
            "M01_RFLAGS_RESTORED",
            "M01_PGE_CYCLE_VERIFIED",
            "M01_OWNED_CR3_CANARY_VERIFIED",
            "M01_POST_CR3",
            "M01_RW_TO_RX_VERIFIED",
            "M01_TLB_INVALIDATION_COMPLETE",
            "M01_RX_PROBE_EXECUTED",
            "M01_PAGE_ZERO_ABSENT",
            "TEST_PASS m01.stage3",
            "CLROS_M01_LIVE_BOOT_MEMORY_READY"
        ];

        if (!integration.Succeeded
            || !result.Succeeded
            || result.Reason.Length != 0
            || BootSequencePlan.M01StageThreeSuccessLineCount
                != expected.Length
            || serial.Count != expected.Length)
        {
            return HostCheck.Result(false);
        }

        string[] eventSnapshot = events.Snapshot();
        if (eventSnapshot.Length != expected.Length)
        {
            return HostCheck.Result(false);
        }

        for (int index = 0; index < expected.Length; index++)
        {
            if (!string.Equals(
                    serial.LineAt(index),
                    expected[index],
                    StringComparison.Ordinal)
                || !string.Equals(
                    eventSnapshot[index],
                    expected[index],
                    StringComparison.Ordinal))
            {
                return HostCheck.Result(false);
            }
        }

        return HostCheck.Result(true);
    }

    [HostTest("boot.sequence M01 stage-three presenter rejects default and nonfinal results", HostLane.Fast, 9)]
    private static bool
        TestM01StageThreePresenterRejectsDefaultAndNonfinalResults()
    {
        SequenceTestSerialSink defaultSerial =
            new SequenceTestSerialSink();
        BootEventRingBuffer defaultEvents =
            new BootEventRingBuffer(8);
        BootResult defaultResult =
            BootSequence.RunM01LivePagingIntegration(
                default,
                defaultSerial,
                defaultEvents);
        string defaultDetail =
            (byte)M01LivePagingStatus.Uninitialized
            + " checkpoint="
            + (byte)M01LivePagingCheckpoint.Uninitialized;
        string[] defaultExpected =
        [
            "TEST_START m01.stage3",
            BootSequence.M01StageThreeTestFailurePrefix
                + defaultDetail,
            BootSequence.M01StageThreeFailurePrefix
                + defaultDetail
        ];

        if (!M01StageThreeFailureMatches(
                defaultResult,
                defaultSerial,
                defaultEvents,
                defaultExpected,
                "M01 stage three failed: status="
                    + defaultDetail))
        {
            return HostCheck.Result(false);
        }

        M01LivePagingIntegrationResult forgedNonfinalSuccess =
            WithByteAt(
                default(M01LivePagingIntegrationResult),
                0,
                (byte)M01LivePagingStatus.Success);
        SequenceTestSerialSink nonfinalSerial =
            new SequenceTestSerialSink();
        BootEventRingBuffer nonfinalEvents =
            new BootEventRingBuffer(8);
        BootResult nonfinalResult =
            BootSequence.RunM01LivePagingIntegration(
                forgedNonfinalSuccess,
                nonfinalSerial,
                nonfinalEvents);
        string illegalDetail =
            (byte)M01LivePagingStatus.IllegalCoordinatorState
            + " checkpoint="
            + (byte)M01LivePagingCheckpoint.Uninitialized;
        string[] nonfinalExpected =
        [
            "TEST_START m01.stage3",
            BootSequence.M01StageThreeTestFailurePrefix
                + illegalDetail,
            BootSequence.M01StageThreeFailurePrefix
                + illegalDetail
        ];

        return HostCheck.Result(forgedNonfinalSuccess.Status
                == M01LivePagingStatus.Success
            && !forgedNonfinalSuccess.Succeeded
            && M01StageThreeFailureMatches(
                nonfinalResult,
                nonfinalSerial,
                nonfinalEvents,
                nonfinalExpected,
                "M01 stage three failed: status="
                    + illegalDetail));
    }

    [HostTest("boot.sequence M01 stage-three presenter rejects null outputs", HostLane.Fast, 10)]
    private static bool TestM01StageThreePresenterRejectsNullOutputs()
    {
        BootEventRingBuffer nullSerialEvents =
            new BootEventRingBuffer(4);
        BootResult nullSerialResult =
            BootSequence.RunM01LivePagingIntegration(
                default,
                null!,
                nullSerialEvents);

        SequenceTestSerialSink nullEventSerial =
            new SequenceTestSerialSink();
        BootResult nullEventsResult =
            BootSequence.RunM01LivePagingIntegration(
                default,
                nullEventSerial,
                null!);

        return HostCheck.Result(!nullSerialResult.Succeeded
            && string.Equals(
                nullSerialResult.Reason,
                "serial sink is required",
                StringComparison.Ordinal)
            && nullSerialEvents.Count == 0
            && !nullEventsResult.Succeeded
            && string.Equals(
                nullEventsResult.Reason,
                "boot event ring buffer is required",
                StringComparison.Ordinal)
            && nullEventSerial.Count == 0);
    }

    private static bool M01StageThreeFailureMatches(
        BootResult result,
        SequenceTestSerialSink serial,
        BootEventRingBuffer events,
        string[] expected,
        string expectedReason)
    {
        if (result.Succeeded
            || !string.Equals(
                result.Reason,
                expectedReason,
                StringComparison.Ordinal)
            || serial.Count != expected.Length)
        {
            return false;
        }

        string[] eventSnapshot = events.Snapshot();
        if (eventSnapshot.Length != expected.Length)
        {
            return false;
        }

        for (int index = 0; index < expected.Length; index++)
        {
            if (!string.Equals(
                    serial.LineAt(index),
                    expected[index],
                    StringComparison.Ordinal)
                || !string.Equals(
                    eventSnapshot[index],
                    expected[index],
                    StringComparison.Ordinal))
            {
                return false;
            }
        }

        for (int index = 1;
            index < BootSequencePlan.M01StageThreeSuccessLineCount;
            index++)
        {
            if (serial.Contains(
                BootSequence.CreateM01StageThreeSuccessLine(index)))
            {
                return false;
            }
        }

        return true;
    }

    [HostTest("boot.sequence M01 stage-one plan preserves default bytes", HostLane.Fast, 2)]
    private static bool TestM01StageOnePlanPreservesDefaultBytes()
    {
        string[] expected =
        [
            "TEST_START boot.sequence",
            "BOOT_PHASE PhaseA.Audit START",
            "BOOT_PHASE PhaseA.Audit PASS",
            "BOOT_PHASE PhaseA.Diagnostics START",
            "BOOT_PHASE PhaseA.Diagnostics PASS",
            "OS_BOOT_OK",
            "TEST_PASS boot.sequence",
            "OS_SMOKE_PASS"
        ];

        if (BootSequencePlan.NonSmokeLineCount != 5
            || BootSequencePlan.LineCount != expected.Length)
        {
            return HostCheck.Result(false);
        }

        for (int lineIndex = 0; lineIndex < expected.Length; lineIndex++)
        {
            string expectedLine = expected[lineIndex];
            if (BootSequencePlan.GetLineLength(lineIndex) != expectedLine.Length)
            {
                return HostCheck.Result(false);
            }

            for (int offset = 0; offset < expectedLine.Length; offset++)
            {
                if (BootSequencePlan.GetLineByte(lineIndex, offset) != (byte)expectedLine[offset])
                {
                    return HostCheck.Result(false);
                }
            }
        }

        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);
        BootResult result = BootSequence.Run(serial, events);

        return HostCheck.Result(result.Succeeded
            && serial.Count == expected.Length
            && !serial.Contains("TEST_START m01.stage1")
            && !serial.Contains("TEST_PASS m01.stage1")
            && !serial.ContainsPrefix("M01_"));
    }

    [HostTest("boot.sequence M01 stage-two plan preserves default bytes", HostLane.Fast, 5)]
    private static bool TestM01StageTwoPlanPreservesDefaultBytes()
    {
        string[] expected =
        [
            "TEST_START boot.sequence",
            "BOOT_PHASE PhaseA.Audit START",
            "BOOT_PHASE PhaseA.Audit PASS",
            "BOOT_PHASE PhaseA.Diagnostics START",
            "BOOT_PHASE PhaseA.Diagnostics PASS",
            "OS_BOOT_OK",
            "TEST_PASS boot.sequence",
            "OS_SMOKE_PASS"
        ];

        if (BootSequencePlan.NonSmokeLineCount != 5
            || BootSequencePlan.LineCount != expected.Length)
        {
            return HostCheck.Result(false);
        }

        for (int lineIndex = 0; lineIndex < expected.Length; lineIndex++)
        {
            if (!string.Equals(
                    BootSequence.CreatePlanLine(lineIndex),
                    expected[lineIndex],
                    StringComparison.Ordinal))
            {
                return HostCheck.Result(false);
            }
        }

        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);
        BootResult result = BootSequence.Run(serial, events);
        string[] eventSnapshot = events.Snapshot();

        if (!result.Succeeded
            || serial.Count != expected.Length
            || eventSnapshot.Length != expected.Length
            || serial.Contains("TEST_START m01.stage2")
            || serial.Contains("TEST_PASS m01.stage2")
            || serial.ContainsPrefix("M01_"))
        {
            return HostCheck.Result(false);
        }

        for (int lineIndex = 0; lineIndex < expected.Length; lineIndex++)
        {
            if (!string.Equals(
                    serial.LineAt(lineIndex),
                    expected[lineIndex],
                    StringComparison.Ordinal)
                || !string.Equals(
                    eventSnapshot[lineIndex],
                    expected[lineIndex],
                    StringComparison.Ordinal))
            {
                return HostCheck.Result(false);
            }
        }

        return HostCheck.Result(true);
    }

    [HostTest("boot.sequence M01 stage-three plan preserves default bytes", HostLane.Fast, 11)]
    private static bool TestM01StageThreePlanPreservesDefaultBytes()
    {
        string[] expected =
        [
            "TEST_START boot.sequence",
            "BOOT_PHASE PhaseA.Audit START",
            "BOOT_PHASE PhaseA.Audit PASS",
            "BOOT_PHASE PhaseA.Diagnostics START",
            "BOOT_PHASE PhaseA.Diagnostics PASS",
            "OS_BOOT_OK",
            "TEST_PASS boot.sequence",
            "OS_SMOKE_PASS"
        ];

        if (BootSequencePlan.NonSmokeLineCount != 5
            || BootSequencePlan.LineCount != expected.Length)
        {
            return HostCheck.Result(false);
        }

        for (int lineIndex = 0;
            lineIndex < expected.Length;
            lineIndex++)
        {
            if (!string.Equals(
                    BootSequence.CreatePlanLine(lineIndex),
                    expected[lineIndex],
                    StringComparison.Ordinal))
            {
                return HostCheck.Result(false);
            }
        }

        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);
        BootResult result = BootSequence.Run(serial, events);
        string[] eventSnapshot = events.Snapshot();

        if (!result.Succeeded
            || serial.Count != expected.Length
            || eventSnapshot.Length != expected.Length
            || serial.Contains("TEST_START m01.stage3")
            || serial.Contains("TEST_PASS m01.stage3")
            || serial.Contains("CLROS_M01_LIVE_BOOT_MEMORY_READY")
            || serial.ContainsPrefix("M01_"))
        {
            return HostCheck.Result(false);
        }

        for (int lineIndex = 0;
            lineIndex < expected.Length;
            lineIndex++)
        {
            if (!string.Equals(
                    serial.LineAt(lineIndex),
                    expected[lineIndex],
                    StringComparison.Ordinal)
                || !string.Equals(
                    eventSnapshot[lineIndex],
                    expected[lineIndex],
                    StringComparison.Ordinal))
            {
                return HostCheck.Result(false);
            }
        }

        return HostCheck.Result(true);
    }

    internal static bool HasNoStdlibBootAuthorityBoundary()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        var project = System.Xml.Linq.XDocument.Load(Path.Combine(root, "src/OS/OS.csproj"));
        if (!BuildEfiShellPreflightTests.HasExactCanonicalEfiCompileItem(
                @"..\OS.Kernel\Boot\BootSequence.cs", @"Boot\BootSequence.cs")
            || !BuildEfiShellPreflightTests.HasExactCanonicalEfiCompileItem(
                @"..\OS.Kernel\Diagnostics\Probes.cs", @"Diagnostics\Probes.cs")
            || !project.Descendants("DefineConstants").Any(element =>
                element.Attribute("Condition") is null && element.Parent?.Attribute("Condition") is null
                && element.Value.Split(';').Contains("NOSTDLIB_EFI", StringComparer.Ordinal))
            || !project.Descendants("NoStdLib").Any(element => element.Value == "true")
            || project.Descendants("ProjectReference").Any())
            return false;

        // These files deliberately share a small EFI surface; their host-only
        // imports, catalogs and presenters must stay inside the existing guards.
        return HasGuardedSurface("src/OS.Kernel/Boot/BootSequence.cs", 3,
            ["public static class BootSequence",
             "public static unsafe bool StageLinuxElfPackage(bool firmwareAvailable, void* context, delegate*<void*, bool> stage) =>",
             "public static unsafe bool RunOwnedLinuxElfPackage(bool ownedChecksComplete, bool firmwareRevoked,",
             "public static bool IsWorkloadServiceLoopEnabled() =>",
             "public static unsafe bool RunOwnedWorkloadServiceLoop(bool packageReady, bool firmwareRevoked,",
             "public static unsafe bool PrepareLinuxElfStartup(bool firmwareAvailable, void* context, delegate*<void*, bool> prepare) =>",
             "public static unsafe bool RunOwnedLinuxElfStartup(bool packageReady, bool firmwareRevoked,",
             "public static unsafe bool PrepareLinuxUserBoundary(bool firmwareAvailable, void* context, delegate*<void*, bool> prepare) =>",
             "public static unsafe bool RunOwnedLinuxSyscalls(bool startupReady, bool serviceStopped, bool firmwareRevoked,",
             "public static unsafe bool PrepareLinuxMemory(bool firmwareAvailable, void* context, delegate*<void*, bool> prepare) =>",
             "public static unsafe bool RunOwnedLinuxMemory(bool startupReady, bool serviceStopped, bool firmwareRevoked,",
             "public static unsafe bool PrepareLinuxDescriptors(bool firmwareAvailable, void* context, delegate*<void*, bool> prepare) =>",
             "public static unsafe bool RunOwnedLinuxThreads(bool startupReady, bool serviceStopped, bool firmwareRevoked,",
             "public static unsafe bool RunOwnedLinuxSignals(bool startupReady, bool serviceStopped, bool firmwareRevoked,",
             "public static unsafe bool PrepareCooperativeSwitch(bool firmwareAvailable, void* context, delegate*<void*, bool> prepare) =>",
             "public static unsafe bool RunOwnedCooperativeSwitch(bool ownedChecksComplete, bool firmwareRevoked,",
             "public static unsafe bool RunOwnedSchedulerTimer(bool ownedChecksComplete, bool apicReady, bool firmwareRevoked,",
             "public static unsafe bool RunOwnedKernelThreads(bool ownedChecksComplete, bool apicReady, bool firmwareRevoked,",
             "public static unsafe bool RunOwnedLinuxDescriptors(bool startupReady, bool serviceStopped, bool firmwareRevoked,",
             "public static unsafe OS.Boot.FirstExitReturn RunFirstRealExitBootServicesTransition(",
             "private static bool IsFirstRealExitBootServicesEnabled() =>",
             "public static unsafe OS.Kernel.Runtime.CompilerClassConstructorStatus RunCompilerClassConstructors(",
             "public static bool IsFirmwareAdapterRevocationEnabled() =>",
             "public static bool IsSimpleThrowCatchEnabled() =>",
             "public static unsafe OS.Kernel.Runtime.X64FrameUnwindResult RunSimpleThrowCatchUnwind(",
             "public static bool IsSingleFinallyCatchEnabled() =>",
             "public static unsafe OS.Kernel.Runtime.X64FrameUnwindResult RunSingleFinallyCatchUnwind(",
             "public static bool IsRethrowEnabled() => OS.Kernel.Diagnostics.Probes.EnableM04RethrowProbe;",
             "public static bool IsNestedFinallyCatchEnabled() => OS.Kernel.Diagnostics.Probes.EnableM04NestedFinallyCatchProbe;",
             "public static unsafe OS.Kernel.Runtime.X64FrameUnwindResult RunNestedFinallyCatchUnwind(",
             "public static bool IsUnhandledExceptionEnabled() => OS.Kernel.Diagnostics.Probes.EnableM04UnhandledExceptionProbe;",
             "public static unsafe OS.Kernel.Runtime.X64FrameUnwindResult RunUnhandledExceptionUnwind(",
             "public static bool IsNormalFinallyEnabled() => OS.Kernel.Diagnostics.Probes.EnableM04NormalFinallyProbe;",
             "public static unsafe OS.Kernel.Runtime.X64FrameUnwindResult RunNormalFinallyUnwind(",
             "public static bool IsFrameDispatchEnabled() => OS.Kernel.Diagnostics.Probes.EnableM04FrameDispatchProbe;",
             "public static bool IsEhCatalogEnabled() =>",
             "public static unsafe OS.Kernel.Runtime.X64FrameUnwindResult RunFrameDispatchUnwind(",
             "public static bool IsConstructorFailureEnabled() => OS.Kernel.Diagnostics.Probes.EnableM04ConstructorFailureProbe;",
             "public static unsafe OS.Kernel.Runtime.X64FrameUnwindResult RunConstructorFailureUnwind(",
             "public static bool IsFilteredCatchEnabled() => OS.Kernel.Diagnostics.Probes.EnableM04FilteredCatchProbe;",
             "public static unsafe OS.Kernel.Runtime.X64FrameUnwindResult RunFilteredCatchUnwind(",
             "public static unsafe bool RunOwnedAcpiSelection(",
             "public static unsafe bool RunOwnedPciEnumeration(",
             "public static unsafe bool RunOwnedBarDma(",
             "public static unsafe bool RunOwnedApicSetup(",
             "public static unsafe bool RunOwnedVirtqueueInterrupt(",
             "public static unsafe bool RunOwnedVirtqueuePolling(",
             "public static unsafe bool RunOwnedVirtioNet(",
             "public static unsafe bool RunOwnedArpReceive(",
             "public static unsafe bool RunOwnedVirtioNegotiation(",
             "public static unsafe bool RunOwnedVirtioBlockQueueSetup(",
             "public static unsafe bool RunOwnedVirtioBlockRead(",
             "public static unsafe bool RunOwnedGptPartition(",
             "public static unsafe bool RunOwnedFatRead(",
             "public static unsafe bool RunOwnedPackageRead(",
             "public static unsafe bool RunOwnedBlockMsix(",
             "public static unsafe bool RunOwnedLiveBlockContract(",
             "public static unsafe bool RunOwnedMonotonicClock(",
             "public static unsafe bool RunPostFirmwarePhysicalMapping(",
             "public static unsafe OS.Kernel.Runtime.X64FrameUnwindResult RunRethrowUnwind(",
             "public static unsafe OS.Kernel.Runtime.X64UnwindMetadataResult RunX64UnwindMetadata(",
             "public static unsafe OS.Boot.OwnedAllocatorRoundTripStatus RunOwnedSubsystemAllocatorRoundTrip(",
             "public static unsafe bool ReportVerifiedExitCompletion(void* context,",
             "public static unsafe OS.Kernel.Runtime.CompilerInterfaceDispatchStatus RunInterfaceDispatch("])
            && HasGuardedSurface("src/OS.Kernel/Diagnostics/Probes.cs", 2,
            ["public static partial class Probes",
             "public const bool EnableM09010Probe = true;",
             "public const bool EnableM09010Probe = false;",
             "public const bool EnableM09009Probe = true;",
             "public const bool EnableM09009Probe = false;",
             "public const bool EnableM09006Probe = true;",
             "public const bool EnableM09006Probe = false;",
             "public const bool EnableM09005Probe = true;",
             "public const bool EnableM09005Probe = false;",
             "public const bool EnableM09004Probe = true;",
             "public const bool EnableM09004Probe = false;",
             "public const bool EnableM16009Probe = true;",
             "public const bool EnableM16009Probe = false;",
             "public const bool EnableM16008Probe = true;",
             "public const bool EnableM16008Probe = false;",
             "public const bool EnableM16007Probe = true;",
             "public const bool EnableM16007Probe = false;",
             "public const bool EnableM16006Probe = true;",
             "public const bool EnableM16006Probe = false;",
             "public const bool EnableM16005Probe = true;",
             "public const bool EnableM16005Probe = false;",
             "public const bool EnableM16004Probe = true;",
             "public const bool EnableM16004Probe = false;",
             "public const bool EnableM16003Probe = true;",
             "public const bool EnableM16003Probe = false;",
             "public const bool EnableM16002Probe = true;",
             "public const bool EnableM16002Probe = false;",
             "public const bool EnableM04SeededCollectorChurnProbe = true;",
             "public const bool EnableM04SeededCollectorChurnProbe = false;",
             "public const bool EnableM03FirstRealExitBootServicesProbe = true;",
             "public const bool EnableM03FirstRealExitBootServicesProbe = false;",
             "public const bool EnableM03FirmwareAdapterRevocationProbe = true;",
             "public const bool EnableM03FirmwareAdapterRevocationProbe = false;",
             "public const bool EnableM03OwnedSubsystemSurvivalProbe = true;",
             "public const bool EnableM03OwnedSubsystemSurvivalProbe = false;",
             "public const bool EnableM03OwnedSubsystemPanicProbe = true;",
             "public const bool EnableM03OwnedSubsystemPanicProbe = false;",
             "public const bool EnableM04ClassConstructorExecutionProbe = true;",
             "public const bool EnableM04ClassConstructorExecutionProbe = false;",
             "public const bool EnableM04InterfaceDispatchProbe = true;",
             "public const bool EnableM04InterfaceDispatchProbe = false;",
             "public const bool EnableM04X64UnwindMetadataProbe = true;",
             "public const bool EnableM04X64UnwindMetadataProbe = false;",
             "public const bool EnableM04SimpleThrowCatchProbe = true;",
             "public const bool EnableM04SimpleThrowCatchProbe = false;",
             "public const bool EnableM04SingleFinallyCatchProbe = true;",
             "public const bool EnableM04SingleFinallyCatchProbe = false;",
             "public const bool EnableM04RethrowProbe = true;",
             "public const bool EnableM04RethrowProbe = false;",
             "public const bool EnableM04NestedFinallyCatchProbe = true;",
             "public const bool EnableM04NestedFinallyCatchProbe = false;",
             "public const bool EnableM04UnhandledExceptionProbe = true;",
             "public const bool EnableM04UnhandledExceptionProbe = false;",
             "public const bool EnableM04FrameDispatchProbe = true;",
             "public const bool EnableM04FrameDispatchProbe = false;",
             "public const bool EnableM04NormalFinallyProbe = true;",
             "public const bool EnableM04NormalFinallyProbe = false;",
             "public const bool EnableM04EhCatalogProbe = true;",
             "public const bool EnableM04EhCatalogProbe = false;",
             "public const bool EnableM04ConstructorFailureProbe = true;",
             "public const bool EnableM04ConstructorFailureProbe = false;",
             "public const bool EnableM04FilteredCatchProbe = true;",
             "public const bool EnableM04FilteredCatchProbe = false;",
             "public const bool EnableM05OwnedAcpiSelectionProbe = true;",
             "public const bool EnableM05OwnedAcpiSelectionProbe = false;",
             "public const bool EnableM05PhysicalMappingProbe = true;",
             "public const bool EnableM05PhysicalMappingProbe = false;",
             "public const bool EnableM05MonotonicClockProbe = true;",
             "public const bool EnableM05MonotonicClockProbe = false;",
             "public const bool EnableM05PciEnumerationProbe = true;",
             "public const bool EnableM05PciEnumerationProbe = false;",
             "public const bool EnableM05BarDmaProbe = true;",
             "public const bool EnableM05BarDmaProbe = false;",
             "public const bool EnableM05ApicSetupProbe = true;",
             "public const bool EnableM05ApicSetupProbe = false;",
             "public const bool EnableM05VirtqueueInterruptProbe = true;",
             "public const bool EnableM05VirtqueueInterruptProbe = false;",
             "public const bool EnableM05VirtqueuePollingProbe = true;",
             "public const bool EnableM05VirtqueuePollingProbe = false;",
             "public const bool EnableM05VirtioNegotiationProbe = true;",
             "public const bool EnableM05VirtioNegotiationProbe = false;",
             "public const bool EnableM06VirtioBlockQueueSetupProbe = true;",
             "public const bool EnableM06VirtioBlockQueueSetupProbe = false;",
             "public const bool EnableM06VirtioBlockReadProbe = true;",
             "public const bool EnableM06VirtioBlockReadProbe = false;",
             "public const bool EnableM06FatReadProbe = true;",
             "public const bool EnableM06FatReadProbe = false;",
             "public const bool EnableM06PackageReadProbe = true;",
             "public const bool EnableM06PackageReadProbe = false;",
             "public const bool EnableM06BlockMsixProbe = true;",
             "public const bool EnableM06BlockMsixProbe = false;",
             "public const bool EnableM06BlockMsixPollingRecoveryProbe = true;",
             "public const bool EnableM06BlockMsixPollingRecoveryProbe = false;",
             "public const bool EnableM07VirtioNetProbe = true;",
             "public const bool EnableM07VirtioNetProbe = false;",
             "public const bool EnableM07ArpReceiveProbe = true;",
             "public const bool EnableM07ArpReceiveProbe = false;",
             "public const bool EnableM06GptPartitionProbe = true;",
             "public const bool EnableM06GptPartitionProbe = false;",
             "public const bool EnableM06LiveBlockContractProbe = true;",
             "public const bool EnableM06LiveBlockContractProbe = false;"]);

        bool HasGuardedSurface(string relativePath, int expectedRegions, string[] declarations)
        {
            string source = File.ReadAllText(Path.Combine(root, relativePath)).ReplaceLineEndings("\n");
            var guarded = new Regex(@"(?ms)^#if !NOSTDLIB_EFI\n.*?^#endif$");
            MatchCollection regions = guarded.Matches(source);
            if (regions.Count != expectedRegions || regions.Any(region =>
                Regex.IsMatch(region.Value["#if !NOSTDLIB_EFI\n".Length..], @"(?m)^#(?:if|elif|else)\b")))
                return false;
            string efi = guarded.Replace(source, string.Empty);
            return Regex.Matches(efi, @"(?m)^[ \t]*(?:public|private|internal|protected|using)\b[^\n]*")
                .Select(match => match.Value.Trim()).SequenceEqual(declarations, StringComparer.Ordinal)
                && !Regex.IsMatch(efi, @"\b(?:string|Span|ReadOnlySpan|ISerialSink|BootEventRingBuffer|ProbeDefinition)\b");
        }
    }

    [HostTest("boot.sequence M01 stage-one plan preserves EFI linkage boundary", HostLane.Fast, 3)]
    private static bool TestM01StageOnePlanPreservesEfiLinkageBoundary()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string projectPath = Path.Combine(repositoryRoot, "src", "OS", "OS.csproj");
        System.Xml.Linq.XDocument project = System.Xml.Linq.XDocument.Load(projectPath);

        bool defaultCompileItemsDisabled = project
            .Descendants("EnableDefaultCompileItems")
            .Any(element => string.Equals(element.Value.Trim(), "false", StringComparison.OrdinalIgnoreCase));
        bool planLinked = false;
        bool integrationLinked =
            BuildEfiShellPreflightTests.HasExactCanonicalEfiCompileItem(
                @"..\OS.Kernel\Boot\M01LoadedImageStackIntegration.cs",
                @"Boot\M01LoadedImageStackIntegration.cs");

        foreach (System.Xml.Linq.XElement compile in project.Descendants("Compile"))
        {
            string? include = compile.Attribute("Include")?.Value;
            if (string.IsNullOrWhiteSpace(include))
            {
                continue;
            }

            string normalized = include.Replace('/', '\\');
            if (normalized.Contains('*'))
            {
                return HostCheck.Result(false);
            }

            if (string.Equals(
                normalized,
                @"..\OS.Kernel\Boot\BootSequencePlan.cs",
                StringComparison.OrdinalIgnoreCase))
            {
                planLinked = true;
            }
        }

        string efiEntryPath = Path.Combine(
            repositoryRoot,
            "src",
            "OS",
            "Boot",
            "EfiEntry.cs");
        string efiEntrySource = File.ReadAllText(efiEntryPath);
        const string probeStart =
            "#if (M01_LOADED_IMAGE_STACK_PROBE || CLROS_NORMAL_VERIFIED_EXIT)";
        int probeStartIndex = efiEntrySource.IndexOf(
            probeStart,
            StringComparison.Ordinal);
        int probeElseIndex = probeStartIndex < 0
            ? -1
            : efiEntrySource.IndexOf(
                "#else",
                probeStartIndex + probeStart.Length,
                StringComparison.Ordinal);
        int probeEndIndex = probeElseIndex < 0
            ? -1
            : efiEntrySource.IndexOf(
                "#endif",
                probeElseIndex + "#else".Length,
                StringComparison.Ordinal);
        bool singleProbeRegion = probeStartIndex >= 0
            && probeElseIndex > probeStartIndex
            && probeEndIndex > probeElseIndex
            && efiEntrySource.IndexOf(
                probeStart,
                probeStartIndex + probeStart.Length,
                StringComparison.Ordinal) < 0;
        string probeImplementation = singleProbeRegion
            ? efiEntrySource[probeStartIndex..probeElseIndex]
            : string.Empty;
        string defaultImplementation = singleProbeRegion
            ? efiEntrySource[probeElseIndex..probeEndIndex]
            : string.Empty;
        string outsideProbe = singleProbeRegion
            ? string.Concat(
                efiEntrySource.AsSpan(0, probeStartIndex),
                efiEntrySource.AsSpan(probeEndIndex + "#endif".Length))
            : efiEntrySource;
        string compactProbe = Regex.Replace(
            probeImplementation,
            @"\s+",
            string.Empty,
            RegexOptions.CultureInvariant);
        string compactDefault = Regex.Replace(
            defaultImplementation,
            @"\s+",
            string.Empty,
            RegexOptions.CultureInvariant);
        string compactOutside = Regex.Replace(
            outsideProbe,
            @"\s+",
            string.Empty,
            RegexOptions.CultureInvariant);
        string[] requiredProbeCalls =
        [
            "M01LoadedImageStackIntegration.PrepareFromStaticScratch(",
            "RecordTransferFromStaticState",
            "ObserveContinuationRspFromStaticState",
            "ReportUnexpectedReturnFromStaticState"
        ];
        bool probeCallsConfined = singleProbeRegion;
        foreach (string requiredProbeCall in requiredProbeCalls)
        {
            probeCallsConfined &= compactProbe.Contains(
                    requiredProbeCall,
                    StringComparison.Ordinal)
                && !compactDefault.Contains(
                    requiredProbeCall,
                    StringComparison.Ordinal)
                && !compactOutside.Contains(
                    requiredProbeCall,
                    StringComparison.Ordinal);
        }

        return HostCheck.Result(defaultCompileItemsDisabled
            && planLinked
            && integrationLinked
            && HasNoStdlibBootAuthorityBoundary()
            && probeCallsConfined
            && !compactProbe.Contains(
                "M01LoadedImageStackIntegration.Prepare(",
                StringComparison.Ordinal)
            && !compactProbe.Contains(
                "M01LoadedImageStackIntegration.PrepareWithStubByteMode(",
                StringComparison.Ordinal)
            && !efiEntrySource.Contains(
                "RunM01LoadedImageStackIntegration",
                StringComparison.Ordinal)
            && !efiEntrySource.Contains(
                "RecordTransfer(",
                StringComparison.Ordinal)
            && !efiEntrySource.Contains(
                "ObserveContinuationRsp(",
                StringComparison.Ordinal));
    }

    [HostTest("boot.sequence M01 stage-two plan preserves EFI linkage boundary", HostLane.Fast, 6)]
    private static bool TestM01StageTwoPlanPreservesEfiLinkageBoundary()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string projectPath = Path.Combine(
            repositoryRoot,
            "src",
            "OS",
            "OS.csproj");
        System.Xml.Linq.XDocument project =
            System.Xml.Linq.XDocument.Load(projectPath);

        bool defaultCompileItemsDisabled = project
            .Descendants("EnableDefaultCompileItems")
            .Any(element => string.Equals(
                element.Value.Trim(),
                "false",
                StringComparison.OrdinalIgnoreCase));
        bool planLinked = false;
        bool integrationLinked =
            BuildEfiShellPreflightTests.HasExactCanonicalEfiCompileItem(
                @"..\OS.Kernel\Boot\M01LiveAllocatorIntegration.cs",
                @"Boot\M01LiveAllocatorIntegration.cs");

        foreach (System.Xml.Linq.XElement compile
            in project.Descendants("Compile"))
        {
            string? include = compile.Attribute("Include")?.Value;
            if (string.IsNullOrWhiteSpace(include))
            {
                continue;
            }

            string normalized = include.Replace('/', '\\');
            if (normalized.Contains('*'))
            {
                return HostCheck.Result(false);
            }

            if (string.Equals(
                normalized,
                @"..\OS.Kernel\Boot\BootSequencePlan.cs",
                StringComparison.OrdinalIgnoreCase))
            {
                planLinked = true;
            }
        }

        string efiEntryPath = Path.Combine(
            repositoryRoot,
            "src",
            "OS",
            "Boot",
            "EfiEntry.cs");
        string efiEntrySource = File.ReadAllText(efiEntryPath);

        return HostCheck.Result(defaultCompileItemsDisabled
            && planLinked
            && integrationLinked
            && HasNoStdlibBootAuthorityBoundary()
            && !efiEntrySource.Contains(
                "RunM01LiveAllocatorIntegration",
                StringComparison.Ordinal)
            && !efiEntrySource.Contains(
                "M01LiveAllocatorIntegration.Execute",
                StringComparison.Ordinal));
    }

    [HostTest("boot.sequence M01 stage-three plan preserves EFI linkage boundary", HostLane.Fast, 12)]
    private static bool TestM01StageThreePlanPreservesEfiLinkageBoundary()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string projectPath = Path.Combine(
            repositoryRoot,
            "src",
            "OS",
            "OS.csproj");
        System.Xml.Linq.XDocument project =
            System.Xml.Linq.XDocument.Load(projectPath);

        bool defaultCompileItemsDisabled = project
            .Descendants("EnableDefaultCompileItems")
            .Any(element => string.Equals(
                element.Value.Trim(),
                "false",
                StringComparison.OrdinalIgnoreCase));
        bool planLinked = false;
        bool integrationLinked =
            BuildEfiShellPreflightTests.HasExactCanonicalEfiCompileItem(
                @"..\OS.Kernel\Boot\M01LivePagingIntegration.cs",
                @"Boot\M01LivePagingIntegration.cs");

        foreach (System.Xml.Linq.XElement compile
            in project.Descendants("Compile"))
        {
            string? include = compile.Attribute("Include")?.Value;
            if (string.IsNullOrWhiteSpace(include))
            {
                continue;
            }

            string normalized = include.Replace('/', '\\');
            if (normalized.Contains('*'))
            {
                return HostCheck.Result(false);
            }

            if (string.Equals(
                normalized,
                @"..\OS.Kernel\Boot\BootSequencePlan.cs",
                StringComparison.OrdinalIgnoreCase))
            {
                planLinked = true;
            }
        }

        string efiEntryPath = Path.Combine(
            repositoryRoot,
            "src",
            "OS",
            "Boot",
            "EfiEntry.cs");
        string efiEntrySource = File.ReadAllText(efiEntryPath);

        return HostCheck.Result(defaultCompileItemsDisabled
            && planLinked
            && integrationLinked
            && HasNoStdlibBootAuthorityBoundary()
            && !efiEntrySource.Contains(
                "RunM01LivePagingIntegration",
                StringComparison.Ordinal)
            && !efiEntrySource.Contains(
                "M01LivePagingIntegration.Execute",
                StringComparison.Ordinal));
    }

    [HostTest("boot.sequence run emits plan lines in order", HostLane.Fast, 15)]
    private static bool TestBootSequenceUsesPlan()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);

        BootResult result = BootSequence.Run(serial, events);

        if (!result.Succeeded || serial.Count != BootSequencePlan.LineCount)
        {
            return HostCheck.Result(false);
        }

        for (int lineIndex = 0; lineIndex < BootSequencePlan.LineCount; lineIndex++)
        {
            if (serial.LineAt(lineIndex) != BootSequence.CreatePlanLine(lineIndex))
            {
                return HostCheck.Result(false);
            }
        }

        string[] eventSnapshot = events.Snapshot();
        return HostCheck.Result(eventSnapshot.Length == BootSequencePlan.LineCount
            && eventSnapshot[0] == BootSequence.CreatePlanLine(0)
            && eventSnapshot[^1] == BootSequence.CreatePlanLine(BootSequencePlan.LineCount - 1));
    }

    [HostTest("boot.sequence validates handoff and emits ready marker", HostLane.Fast, 16)]
    private static bool TestBootSequenceHandoffReadyMarker()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);

        BootResult result = BootSequence.Run(CreateValidOwnedHandoffRecord(), serial, events);

        if (!result.Succeeded || serial.Count != BootSequencePlan.LineCount + 1)
        {
            return HostCheck.Result(false);
        }

        string[] eventSnapshot = events.Snapshot();
        return HostCheck.Result(serial.LineAt(0) == BootSequence.CreatePlanLine(0)
            && serial.LineAt(1) == BootSequenceHandoffValidator.ReadyMarker
            && serial.LineAt(2) == BootSequence.CreatePlanLine(1)
            && serial.LineAt(serial.Count - 1) == SerialSmokeMarkers.OsSmokePass
            && eventSnapshot.Length == serial.Count
            && eventSnapshot[1] == BootSequenceHandoffValidator.ReadyMarker);
    }

    [HostTest("boot.sequence rejects invalid handoff before phases", HostLane.Fast, 17)]
    private static bool TestBootSequenceRejectsInvalidHandoff()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);

        BootHandoffRecord record = CreateValidOwnedHandoffRecord();
        record.MemoryMap.Flags = 0;

        BootResult result = BootSequence.Run(record, serial, events);
        string expectedFailMarker = BootSequenceHandoffValidator.CreateFailMarker(
            BootSequenceHandoffStatus.MemoryMapNotOwned);

        return HostCheck.Result(!result.Succeeded
            && result.Reason.Contains(nameof(BootSequenceHandoffStatus.MemoryMapNotOwned), StringComparison.Ordinal)
            && serial.Count == 2
            && serial.LineAt(0) == BootSequence.CreatePlanLine(0)
            && serial.LineAt(1) == expectedFailMarker);
    }

    [HostTest("boot.sequence handoff validator rejects source capability and count issues", HostLane.Fast, 18)]
    private static bool TestBootSequenceHandoffValidatorFailures()
    {
        BootHandoffRecord missingCapability = CreateValidOwnedHandoffRecord();
        missingCapability.Capabilities = BootHandoffRecord.CapabilityTextOutput;

        BootHandoffRecord sourceMatchesOwned = CreateValidOwnedHandoffRecord();
        sourceMatchesOwned.MemoryMap.SourceBufferAddress = sourceMatchesOwned.MemoryMap.BufferAddress;

        BootHandoffRecord tooManyDescriptors = CreateValidOwnedHandoffRecord();
        tooManyDescriptors.MemoryMap.DescriptorCount = BootSequenceHandoffValidator.MaximumDescriptorCount + 1;
        tooManyDescriptors.MemoryMap.MapSize =
            tooManyDescriptors.MemoryMap.DescriptorCount * tooManyDescriptors.MemoryMap.DescriptorSize;
        tooManyDescriptors.MemoryMap.BufferSize = tooManyDescriptors.MemoryMap.MapSize;
        tooManyDescriptors.MemoryMap.SourceBufferSize = tooManyDescriptors.MemoryMap.MapSize;

        BootSequenceHandoffValidationResult missingCapabilityResult =
            BootSequence.ValidateHandoff(missingCapability);
        BootSequenceHandoffValidationResult sourceMatchesOwnedResult =
            BootSequence.ValidateHandoff(sourceMatchesOwned);
        BootSequenceHandoffValidationResult tooManyDescriptorsResult =
            BootSequence.ValidateHandoff(tooManyDescriptors);

        return HostCheck.Result(!missingCapabilityResult.Succeeded
            && missingCapabilityResult.Status == BootSequenceHandoffStatus.MissingRequiredCapability
            && missingCapabilityResult.MissingCapabilities == BootCapabilities.MemoryMap
            && !sourceMatchesOwnedResult.Succeeded
            && sourceMatchesOwnedResult.Status == BootSequenceHandoffStatus.SourceMatchesOwnedBuffer
            && !tooManyDescriptorsResult.Succeeded
            && tooManyDescriptorsResult.Status == BootSequenceHandoffStatus.InvalidDescriptorCount);
    }

    [HostTest("boot.sequence handoff validator validates ACPI RSDP metadata", HostLane.Fast, 19)]
    private static bool TestBootSequenceHandoffValidatorAcpiRsdp()
    {
        BootHandoffRecord validAcpi = CreateValidOwnedHandoffRecord();
        validAcpi.SetAcpiRsdp(
            0x7fe00000,
            FirmwareAcpiRsdpRecord.TableKindAcpi20,
            FirmwareAcpiRsdpRecord.ScanStatusOk);

        BootHandoffRecord invalidAcpi = CreateValidOwnedHandoffRecord();
        invalidAcpi.Capabilities |= BootHandoffRecord.CapabilityAcpiRsdp;
        invalidAcpi.AcpiRsdp.Flags = FirmwareAcpiRsdpRecord.FlagPresent;
        invalidAcpi.AcpiRsdp.TableKind = FirmwareAcpiRsdpRecord.TableKindAcpi20;
        invalidAcpi.AcpiRsdp.ScanStatus = FirmwareAcpiRsdpRecord.ScanStatusOk;

        BootSequenceHandoffValidationResult validResult = BootSequence.ValidateHandoff(validAcpi);
        BootSequenceHandoffValidationResult invalidResult = BootSequence.ValidateHandoff(invalidAcpi);

        return HostCheck.Result(validResult.Succeeded
            && validResult.Status == BootSequenceHandoffStatus.Success
            && invalidResult.Status == BootSequenceHandoffStatus.InvalidAcpiRsdp);
    }

    [HostTest("boot.sequence memory map accepted", HostLane.Fast, 20)]
    private static bool TestBootSequenceMemoryMapAccepted()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);

        BootResult result = BootSequence.Run(
            CreateValidOwnedHandoffRecord(),
            CreateValidMemoryMapSummary(),
            serial,
            events);

        if (!result.Succeeded || serial.Count != BootSequencePlan.LineCount + 2)
        {
            return HostCheck.Result(false);
        }

        string[] eventSnapshot = events.Snapshot();
        return HostCheck.Result(serial.LineAt(0) == BootSequence.CreatePlanLine(0)
            && serial.LineAt(1) == BootSequenceHandoffValidator.ReadyMarker
            && serial.LineAt(2) == BootSequence.BootMemoryMapAccepted
            && serial.LineAt(3) == BootSequence.CreatePlanLine(1)
            && serial.LineAt(serial.Count - 1) == SerialSmokeMarkers.OsSmokePass
            && eventSnapshot.Length == serial.Count
            && eventSnapshot[2] == BootSequence.BootMemoryMapAccepted);
    }

    [HostTest("boot.sequence memory map missing rejected", HostLane.Fast, 21)]
    private static bool TestBootSequenceMemoryMapMissingRejected()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);

        BootResult result = BootSequence.Run(
            CreateValidOwnedHandoffRecord(),
            default,
            serial,
            events);

        string expectedFailMarker = BootSequence.CreateMemoryMapRejectedMarker(
            BootMemoryMapAcceptanceStatus.MissingSummary);

        return HostCheck.Result(!result.Succeeded
            && result.Reason.Contains(nameof(BootMemoryMapAcceptanceStatus.MissingSummary), StringComparison.Ordinal)
            && serial.Count == 3
            && serial.LineAt(0) == BootSequence.CreatePlanLine(0)
            && serial.LineAt(1) == BootSequenceHandoffValidator.ReadyMarker
            && serial.LineAt(2) == expectedFailMarker);
    }

    [HostTest("boot.sequence memory map invalid rejected", HostLane.Fast, 22)]
    private static bool TestBootSequenceMemoryMapInvalidRejected()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);
        EfiMemoryMapSummary invalidSummary = new EfiMemoryMapSummary(
            descriptorCount: 3,
            totalPages: 0,
            totalBytes: 0,
            conventionalPages: 0,
            lowestPhysicalAddress: 0x100000,
            highestPhysicalEndExclusive: 0x200000);

        BootResult result = BootSequence.Run(
            CreateValidOwnedHandoffRecord(),
            invalidSummary,
            serial,
            events);

        string expectedFailMarker = BootSequence.CreateMemoryMapRejectedMarker(
            BootMemoryMapAcceptanceStatus.ZeroPages);

        return HostCheck.Result(!result.Succeeded
            && result.Reason.Contains(nameof(BootMemoryMapAcceptanceStatus.ZeroPages), StringComparison.Ordinal)
            && serial.Count == 3
            && serial.LineAt(0) == BootSequence.CreatePlanLine(0)
            && serial.LineAt(1) == BootSequenceHandoffValidator.ReadyMarker
            && serial.LineAt(2) == expectedFailMarker);
    }

    [HostTest("boot.sequence fault infra readiness emits ready marker", HostLane.Fast, 23)]
    private static bool TestFaultInfrastructureReadyMarker()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);

        BootResult result = BootSequence.RunFaultInfrastructureReadiness(
            CreateReadyFaultInfrastructureResult(),
            serial,
            events);

        string[] eventSnapshot = events.Snapshot();
        return HostCheck.Result(result.Succeeded
            && serial.Count == 1
            && serial.LineAt(0) == BootSequence.FaultInfrastructureReady
            && eventSnapshot.Length == 1
            && eventSnapshot[0] == BootSequence.FaultInfrastructureReady);
    }

    [HostTest("boot.sequence fault infra readiness emits failure marker", HostLane.Fast, 24)]
    private static bool TestFaultInfrastructureFailureMarker()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);

        FaultInfrastructureReadinessResult readiness =
            FaultInfrastructureReadiness.RequireReadyForPrivilegedInstall(
                serialDiagnosticsReady: false,
                idtTableReady: true,
                IdtTablePlanStatus.Ok,
                idtrReady: true,
                IdtrDescriptorStatus.Ok,
                gdtReady: true,
                tssIstReady: true,
                doubleFaultIstReady: true);

        BootResult result = BootSequence.RunFaultInfrastructureReadiness(readiness, serial, events);
        string expectedMarker = BootSequence.CreateFaultInfrastructureNotReadyMarker(
            FaultInfrastructureReadinessStatus.SerialDiagnosticsNotReady);
        string[] eventSnapshot = events.Snapshot();

        return HostCheck.Result(!result.Succeeded
            && result.Reason.Contains(
                nameof(FaultInfrastructureReadinessStatus.SerialDiagnosticsNotReady),
                StringComparison.Ordinal)
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && eventSnapshot.Length == 1
            && eventSnapshot[0] == expectedMarker);
    }

    [HostTest("boot.sequence default markers unchanged by fault readiness helper", HostLane.Fast, 25)]
    private static bool TestDefaultMarkersUnchangedByFaultReadinessHelper()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);

        BootResult result = BootSequence.Run(serial, events);

        if (!result.Succeeded || serial.Count != BootSequencePlan.LineCount)
        {
            return HostCheck.Result(false);
        }

        for (int lineIndex = 0; lineIndex < BootSequencePlan.LineCount; lineIndex++)
        {
            if (serial.LineAt(lineIndex) != BootSequence.CreatePlanLine(lineIndex))
            {
                return HostCheck.Result(false);
            }
        }

        return HostCheck.Result(!serial.Contains(BootSequence.FaultInfrastructureReady)
            && !serial.ContainsPrefix(BootSequence.FaultInfrastructureNotReadyPrefix));
    }

    [HostTest("boot.sequence exitboot transition emits ready marker", HostLane.Fast, 26)]
    private static bool TestExitBootServicesTransitionReadyMarker()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();

        BootResult result = BootSequence.RunExitBootServicesTransition(
            CreateExitBootServicesPlan(UefiExitBootServicesPlanStatus.Success),
            firmware,
            serial,
            events);

        string[] eventSnapshot = events.Snapshot();
        return HostCheck.Result(result.Succeeded
            && firmware.Mode == FirmwareServicesMode.BootServicesExited
            && serial.Count == 1
            && serial.LineAt(0) == BootSequence.ExitBootServicesTransitionReady
            && eventSnapshot.Length == 1
            && eventSnapshot[0] == BootSequence.ExitBootServicesTransitionReady);
    }

    [HostTest("boot.sequence exitboot transition blocks non-success plan", HostLane.Fast, 27)]
    private static bool TestExitBootServicesTransitionBlockedMarker()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();

        BootResult result = BootSequence.RunExitBootServicesTransition(
            CreateExitBootServicesPlan(UefiExitBootServicesPlanStatus.RetryRequired),
            firmware,
            serial,
            events);
        string expectedMarker = BootSequence.CreateExitBootServicesTransitionBlockedMarker(
            UefiExitBootServicesPlanStatus.RetryRequired);
        string[] eventSnapshot = events.Snapshot();

        return HostCheck.Result(!result.Succeeded
            && result.Reason.Contains(nameof(UefiExitBootServicesPlanStatus.RetryRequired), StringComparison.Ordinal)
            && firmware.Mode == FirmwareServicesMode.BootServicesAvailable
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && eventSnapshot.Length == 1
            && eventSnapshot[0] == expectedMarker);
    }

    [HostTest("boot.sequence exitboot transition leaves default markers unchanged", HostLane.Fast, 28)]
    private static bool TestDefaultMarkersUnchangedByExitBootServicesHelper()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);

        BootResult result = BootSequence.Run(serial, events);

        if (!result.Succeeded || serial.Count != BootSequencePlan.LineCount)
        {
            return HostCheck.Result(false);
        }

        for (int lineIndex = 0; lineIndex < BootSequencePlan.LineCount; lineIndex++)
        {
            if (serial.LineAt(lineIndex) != BootSequence.CreatePlanLine(lineIndex))
            {
                return HostCheck.Result(false);
            }
        }

        return HostCheck.Result(!serial.Contains(BootSequence.ExitBootServicesTransitionReady)
            && !serial.ContainsPrefix(BootSequence.ExitBootServicesTransitionBlockedPrefix));
    }

    [HostTest("boot.sequence exitboot preflight emits ready marker", HostLane.Fast, 29)]
    private static bool TestExitBootServicesPreflightReadyMarker()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();

        BootResult result = BootSequence.RunExitBootServicesPreflight(
            CreateReadyPostFirmwareDiagnosticsResult(),
            CreateExitBootServicesPlan(UefiExitBootServicesPlanStatus.Success),
            firmware,
            serial,
            events);

        string[] eventSnapshot = events.Snapshot();
        return HostCheck.Result(result.Succeeded
            && firmware.Mode == FirmwareServicesMode.BootServicesExited
            && serial.Count == 1
            && serial.LineAt(0) == BootSequence.ExitBootServicesPreflightReady
            && eventSnapshot.Length == 1
            && eventSnapshot[0] == BootSequence.ExitBootServicesPreflightReady);
    }

    [HostTest("boot.sequence exitboot preflight blocks missing post-firmware diagnostics", HostLane.Fast, 30)]
    private static bool TestExitBootServicesPreflightBlocksMissingDiagnostics()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        SerialDiagnosticsGuard guard = SerialDiagnosticsGuard.CreateFirmwareConsoleOnly();
        SerialDiagnosticsGuardResult diagnostics =
            guard.RequirePostFirmwareDiagnostics("exitboot.preflight");

        BootResult result = BootSequence.RunExitBootServicesPreflight(
            diagnostics,
            CreateExitBootServicesPlan(UefiExitBootServicesPlanStatus.Success),
            firmware,
            serial,
            events);
        string expectedMarker = BootSequence.CreateExitBootServicesPreflightBlockedMarker(
            SerialDiagnosticsGuardStatus.FirmwareConsoleOnly,
            UefiExitBootServicesPlanStatus.Success);
        string[] eventSnapshot = events.Snapshot();

        return HostCheck.Result(!result.Succeeded
            && result.Reason.Contains(
                nameof(SerialDiagnosticsGuardStatus.FirmwareConsoleOnly),
                StringComparison.Ordinal)
            && firmware.Mode == FirmwareServicesMode.BootServicesAvailable
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && eventSnapshot.Length == 1
            && eventSnapshot[0] == expectedMarker);
    }

    [HostTest("boot.sequence exitboot preflight blocks non-success transition plan", HostLane.Fast, 31)]
    private static bool TestExitBootServicesPreflightBlocksNonSuccessPlan()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();

        BootResult result = BootSequence.RunExitBootServicesPreflight(
            CreateReadyPostFirmwareDiagnosticsResult(),
            CreateExitBootServicesPlan(UefiExitBootServicesPlanStatus.RetryRequired),
            firmware,
            serial,
            events);
        string expectedMarker = BootSequence.CreateExitBootServicesPreflightBlockedMarker(
            SerialDiagnosticsGuardStatus.OwnedSerialReady,
            UefiExitBootServicesPlanStatus.RetryRequired);
        string[] eventSnapshot = events.Snapshot();

        return HostCheck.Result(!result.Succeeded
            && result.Reason.Contains(
                nameof(UefiExitBootServicesPlanStatus.RetryRequired),
                StringComparison.Ordinal)
            && firmware.Mode == FirmwareServicesMode.BootServicesAvailable
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && eventSnapshot.Length == 1
            && eventSnapshot[0] == expectedMarker);
    }

    [HostTest("boot.sequence exitboot preflight leaves default markers unchanged", HostLane.Fast, 32)]
    private static bool TestDefaultMarkersUnchangedByExitBootServicesPreflight()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);

        BootResult result = BootSequence.Run(serial, events);

        if (!result.Succeeded || serial.Count != BootSequencePlan.LineCount)
        {
            return HostCheck.Result(false);
        }

        for (int lineIndex = 0; lineIndex < BootSequencePlan.LineCount; lineIndex++)
        {
            if (serial.LineAt(lineIndex) != BootSequence.CreatePlanLine(lineIndex))
            {
                return HostCheck.Result(false);
            }
        }

        return HostCheck.Result(!serial.Contains(BootSequence.ExitBootServicesPreflightReady)
            && !serial.ContainsPrefix(BootSequence.ExitBootServicesPreflightBlockedPrefix));
    }

    [HostTest("boot.sequence early diagnostics readiness emits ready marker", HostLane.Fast, 33)]
    private static bool TestEarlyDiagnosticsReadyMarker()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);

        BootResult result = BootSequence.RunEarlyDiagnosticsReadiness(
            CreateReadyCom1BackendResult(),
            CreateReadyDescriptorInstallResult(),
            serial,
            events);

        string[] eventSnapshot = events.Snapshot();
        return HostCheck.Result(result.Succeeded
            && serial.Count == 1
            && serial.LineAt(0) == BootSequence.EarlyDiagnosticsReady
            && eventSnapshot.Length == 1
            && eventSnapshot[0] == BootSequence.EarlyDiagnosticsReady);
    }

    [HostTest("boot.sequence early diagnostics readiness rejects serial backend failure", HostLane.Fast, 34)]
    private static bool TestEarlyDiagnosticsRejectsSerialBackendFailure()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);

        BootResult result = BootSequence.RunEarlyDiagnosticsReadiness(
            Com1PortIoBackendPlanResult.InvalidSerialConfiguration(
                SerialPortConfigurationStatus.InvalidBasePort,
                Com1PortIoBackendPlan.RequiredExecutableStubBytes,
                availableStubBytes: 0),
            CreateReadyDescriptorInstallResult(),
            serial,
            events);
        string expectedMarker = BootSequence.CreateEarlyDiagnosticsNotReadyMarker(
            EarlyDiagnosticsReadinessStatus.SerialBackendNotReady);

        return HostCheck.Result(!result.Succeeded
            && result.Reason.Contains(
                nameof(EarlyDiagnosticsReadinessStatus.SerialBackendNotReady),
                StringComparison.Ordinal)
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker);
    }

    [HostTest("boot.sequence early diagnostics readiness rejects descriptor readiness failure", HostLane.Fast, 35)]
    private static bool TestEarlyDiagnosticsRejectsDescriptorReadinessFailure()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);

        BootResult result = BootSequence.RunEarlyDiagnosticsReadiness(
            CreateReadyCom1BackendResult(),
            DescriptorTableInstallReadinessResult.Fail(
                DescriptorTableInstallReadinessStatus.IdtTableNotReady,
                IdtTablePlanStatus.MissingRequiredVector,
                IdtrDescriptorStatus.Ok,
                GdtDescriptorPlanStatus.Ok,
                TssIstPlanStatus.Ok),
            serial,
            events);
        string expectedMarker = BootSequence.CreateEarlyDiagnosticsNotReadyMarker(
            EarlyDiagnosticsReadinessStatus.DescriptorTablesNotReady);

        return HostCheck.Result(!result.Succeeded
            && result.Reason.Contains(
                nameof(EarlyDiagnosticsReadinessStatus.DescriptorTablesNotReady),
                StringComparison.Ordinal)
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker);
    }

    [HostTest("boot.sequence early diagnostics readiness leaves default markers unchanged", HostLane.Fast, 36)]
    private static bool TestDefaultMarkersUnchangedByEarlyDiagnosticsHelper()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);

        BootResult result = BootSequence.Run(serial, events);

        if (!result.Succeeded || serial.Count != BootSequencePlan.LineCount)
        {
            return HostCheck.Result(false);
        }

        for (int lineIndex = 0; lineIndex < BootSequencePlan.LineCount; lineIndex++)
        {
            if (serial.LineAt(lineIndex) != BootSequence.CreatePlanLine(lineIndex))
            {
                return HostCheck.Result(false);
            }
        }

        return HostCheck.Result(!serial.Contains(BootSequence.EarlyDiagnosticsReady)
            && !serial.ContainsPrefix(BootSequence.EarlyDiagnosticsNotReadyPrefix));
    }

    [HostTest("boot.sequence diagnostic stubs materialized emits ready marker", HostLane.Fast, 37)]
    private static bool TestDiagnosticStubMaterializationReadyMarker()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);

        BootResult result = BootSequence.RunDiagnosticStubMaterializationReadiness(
            ExecutableStubBundleMaterializerResult.Success(),
            serial,
            events);

        string[] eventSnapshot = events.Snapshot();
        return HostCheck.Result(result.Succeeded
            && serial.Count == 1
            && serial.LineAt(0) == BootSequence.DiagnosticStubsMaterialized
            && eventSnapshot.Length == 1
            && eventSnapshot[0] == BootSequence.DiagnosticStubsMaterialized);
    }

    [HostTest("boot.sequence diagnostic stubs materialized rejects materializer failure", HostLane.Fast, 38)]
    private static bool TestDiagnosticStubMaterializationRejectsMaterializerFailure()
    {
        SequenceTestSerialSink failedSerial = new SequenceTestSerialSink();
        BootEventRingBuffer failedEvents = new BootEventRingBuffer(16);

        ExecutableStubBundleMaterializerResult failedMaterializer =
            ExecutableStubBundleMaterializerResult.Fail(
                ExecutableStubBundleMaterializerStatus.InvalidBundlePlan,
                ExecutableStubBundlePlanStatus.InvalidMemoryPlan,
                ExecutableStubMemoryPlanStatus.Success);

        BootResult failedResult = BootSequence.RunDiagnosticStubMaterializationReadiness(
            failedMaterializer,
            failedSerial,
            failedEvents);
        string expectedFailedMarker = BootSequence.CreateDiagnosticStubsNotMaterializedMarker(
            DiagnosticStubMaterializationReadinessStatus.MaterializerFailed);
        string[] failedEventSnapshot = failedEvents.Snapshot();

        SequenceTestSerialSink defaultSerial = new SequenceTestSerialSink();
        BootEventRingBuffer defaultEvents = new BootEventRingBuffer(16);

        BootResult defaultResult = BootSequence.RunDiagnosticStubMaterializationReadiness(
            default(ExecutableStubBundleMaterializerResult),
            defaultSerial,
            defaultEvents);
        string expectedDefaultMarker = BootSequence.CreateDiagnosticStubsNotMaterializedMarker(
            DiagnosticStubMaterializationReadinessStatus.MaterializerFailed);
        string[] defaultEventSnapshot = defaultEvents.Snapshot();

        return HostCheck.Result(!failedResult.Succeeded
            && failedResult.Reason.Contains(
                nameof(DiagnosticStubMaterializationReadinessStatus.MaterializerFailed),
                StringComparison.Ordinal)
            && failedSerial.Count == 1
            && failedSerial.LineAt(0) == expectedFailedMarker
            && failedEventSnapshot.Length == 1
            && failedEventSnapshot[0] == expectedFailedMarker
            && !defaultResult.Succeeded
            && defaultResult.Reason.Contains(
                nameof(DiagnosticStubMaterializationReadinessStatus.MaterializerFailed),
                StringComparison.Ordinal)
            && defaultSerial.Count == 1
            && defaultSerial.LineAt(0) == expectedDefaultMarker
            && defaultEventSnapshot.Length == 1
            && defaultEventSnapshot[0] == expectedDefaultMarker);
    }

    [HostTest("boot.sequence diagnostic stubs materialized leaves default markers unchanged", HostLane.Fast, 39)]
    private static bool TestDefaultMarkersUnchangedByDiagnosticStubMaterializationHelper()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);

        BootResult result = BootSequence.Run(serial, events);

        if (!result.Succeeded || serial.Count != BootSequencePlan.LineCount)
        {
            return HostCheck.Result(false);
        }

        for (int lineIndex = 0; lineIndex < BootSequencePlan.LineCount; lineIndex++)
        {
            if (serial.LineAt(lineIndex) != BootSequence.CreatePlanLine(lineIndex))
            {
                return HostCheck.Result(false);
            }
        }

        return HostCheck.Result(!serial.Contains(BootSequence.DiagnosticStubsMaterialized)
            && !serial.ContainsPrefix(BootSequence.DiagnosticStubsNotMaterializedPrefix));
    }

    [HostTest("boot.sequence post-firmware diagnostics readiness emits ready marker", HostLane.Fast, 40)]
    private static bool TestPostFirmwareDiagnosticsReadyMarker()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);
        SerialDiagnosticsGuard guard = new SerialDiagnosticsGuard();
        guard.MarkOwnedSerialReady();
        SerialDiagnosticsGuardResult readiness =
            guard.RequirePostFirmwareDiagnostics("exitboot.diagnostics");

        BootResult result = BootSequence.RunPostFirmwareDiagnosticsReadiness(readiness, serial, events);
        string[] eventSnapshot = events.Snapshot();

        return HostCheck.Result(result.Succeeded
            && serial.Count == 1
            && serial.LineAt(0) == BootSequence.PostFirmwareDiagnosticsReady
            && eventSnapshot.Length == 1
            && eventSnapshot[0] == BootSequence.PostFirmwareDiagnosticsReady);
    }

    [HostTest("boot.sequence post-firmware diagnostics readiness blocks firmware-console-only path", HostLane.Fast, 41)]
    private static bool TestPostFirmwareDiagnosticsBlocksFirmwareConsoleOnly()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);
        SerialDiagnosticsGuard guard = SerialDiagnosticsGuard.CreateFirmwareConsoleOnly();
        SerialDiagnosticsGuardResult readiness =
            guard.RequirePostFirmwareDiagnostics("exitboot.diagnostics");

        BootResult result = BootSequence.RunPostFirmwareDiagnosticsReadiness(readiness, serial, events);
        string expectedMarker = BootSequence.CreatePostFirmwareDiagnosticsBlockedMarker(
            SerialDiagnosticsGuardStatus.FirmwareConsoleOnly);
        string[] eventSnapshot = events.Snapshot();

        return HostCheck.Result(!result.Succeeded
            && result.Reason.Contains(
                nameof(SerialDiagnosticsGuardStatus.FirmwareConsoleOnly),
                StringComparison.Ordinal)
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && eventSnapshot.Length == 1
            && eventSnapshot[0] == expectedMarker);
    }

    [HostTest("boot.sequence post-firmware diagnostics readiness blocks firmware-services-unavailable path", HostLane.Fast, 42)]
    private static bool TestPostFirmwareDiagnosticsBlocksFirmwareServicesUnavailable()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);
        SerialDiagnosticsGuard guard = new SerialDiagnosticsGuard();
        guard.MarkFirmwareServicesUnavailable();
        SerialDiagnosticsGuardResult readiness =
            guard.RequirePostFirmwareDiagnostics("exitboot.diagnostics");

        BootResult result = BootSequence.RunPostFirmwareDiagnosticsReadiness(readiness, serial, events);
        string expectedMarker = BootSequence.CreatePostFirmwareDiagnosticsBlockedMarker(
            SerialDiagnosticsGuardStatus.FirmwareServicesUnavailable);
        string[] eventSnapshot = events.Snapshot();

        return HostCheck.Result(!result.Succeeded
            && result.Reason.Contains(
                nameof(SerialDiagnosticsGuardStatus.FirmwareServicesUnavailable),
                StringComparison.Ordinal)
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && eventSnapshot.Length == 1
            && eventSnapshot[0] == expectedMarker);
    }

    [HostTest("boot.sequence post-firmware diagnostics readiness leaves default markers unchanged", HostLane.Fast, 43)]
    private static bool TestDefaultMarkersUnchangedByPostFirmwareDiagnosticsHelper()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);

        BootResult result = BootSequence.Run(serial, events);

        if (!result.Succeeded || serial.Count != BootSequencePlan.LineCount)
        {
            return HostCheck.Result(false);
        }

        for (int lineIndex = 0; lineIndex < BootSequencePlan.LineCount; lineIndex++)
        {
            if (serial.LineAt(lineIndex) != BootSequence.CreatePlanLine(lineIndex))
            {
                return HostCheck.Result(false);
            }
        }

        return HostCheck.Result(!serial.Contains(BootSequence.PostFirmwareDiagnosticsReady)
            && !serial.ContainsPrefix(BootSequence.PostFirmwareDiagnosticsBlockedPrefix));
    }

    [HostTest("boot.sequence controlled fault probes emit ready marker", HostLane.Fast, 44)]
    private static bool TestControlledFaultProbesReadyMarker()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);

        BootResult result = BootSequence.RunControlledFaultProbeReadiness(
            EarlyDiagnosticsReadinessStatus.Ready,
            CreateReadyFaultProbeReadinessResult(),
            serial,
            events);

        string[] eventSnapshot = events.Snapshot();
        return HostCheck.Result(result.Succeeded
            && serial.Count == 1
            && serial.LineAt(0) == BootSequence.ControlledFaultProbesReady
            && eventSnapshot.Length == 1
            && eventSnapshot[0] == BootSequence.ControlledFaultProbesReady);
    }

    [HostTest("boot.sequence controlled fault probes reject early diagnostics failure", HostLane.Fast, 45)]
    private static bool TestControlledFaultProbesRejectEarlyDiagnosticsFailure()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);

        BootResult result = BootSequence.RunControlledFaultProbeReadiness(
            EarlyDiagnosticsReadinessStatus.SerialBackendNotReady,
            CreateReadyFaultProbeReadinessResult(),
            serial,
            events);
        string expectedMarker = BootSequence.CreateControlledFaultProbesNotReadyMarker(
            ControlledFaultProbeReadinessStatus.EarlyDiagnosticsNotReady);

        return HostCheck.Result(!result.Succeeded
            && result.Reason.Contains(
                nameof(ControlledFaultProbeReadinessStatus.EarlyDiagnosticsNotReady),
                StringComparison.Ordinal)
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker);
    }

    [HostTest("boot.sequence controlled fault probes reject probe readiness failure", HostLane.Fast, 46)]
    private static bool TestControlledFaultProbesRejectProbeReadinessFailure()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);

        BootResult result = BootSequence.RunControlledFaultProbeReadiness(
            EarlyDiagnosticsReadinessStatus.Ready,
            FaultProbeReadinessResult.Fail(FaultProbeReadinessStatus.MemoryPlanTooSmall),
            serial,
            events);
        string expectedMarker = BootSequence.CreateControlledFaultProbesNotReadyMarker(
            ControlledFaultProbeReadinessStatus.FaultProbePlanNotReady);

        return HostCheck.Result(!result.Succeeded
            && result.Reason.Contains(
                nameof(ControlledFaultProbeReadinessStatus.FaultProbePlanNotReady),
                StringComparison.Ordinal)
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker);
    }

    [HostTest("boot.sequence controlled fault probes leave default markers unchanged", HostLane.Fast, 47)]
    private static bool TestDefaultMarkersUnchangedByControlledFaultProbeHelper()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);

        BootResult result = BootSequence.Run(serial, events);

        if (!result.Succeeded || serial.Count != BootSequencePlan.LineCount)
        {
            return HostCheck.Result(false);
        }

        for (int lineIndex = 0; lineIndex < BootSequencePlan.LineCount; lineIndex++)
        {
            if (serial.LineAt(lineIndex) != BootSequence.CreatePlanLine(lineIndex))
            {
                return HostCheck.Result(false);
            }
        }

        return HostCheck.Result(!serial.Contains(BootSequence.ControlledFaultProbesReady)
            && !serial.ContainsPrefix(BootSequence.ControlledFaultProbesNotReadyPrefix));
    }

    [HostTest("boot.phase catalog documents implemented phases", HostLane.Fast, 13)]
    private static bool TestBootPhaseCatalog()
    {
        BootPhaseInfo audit = BootPhaseCatalog.GetImplementedPhase(0);
        BootPhaseInfo diagnostics = BootPhaseCatalog.GetImplementedPhase(1);
        BootPhaseInfo futureRuntime = BootPhaseCatalog.Get(BootPhaseId.PhaseD_Runtime);

        return HostCheck.Result(BootPhaseCatalog.ImplementedPhaseCount == 2
            && BootPhaseCatalog.KnownPhaseCount == 8
            && audit.Id == BootPhaseId.PhaseA_Audit
            && audit.Name == "PhaseA.Audit"
            && audit.MarkerPrefix == BootPhaseCatalog.PhaseMarkerPrefix
            && audit.Precondition.Length > 0
            && audit.Postcondition.Length > 0
            && diagnostics.Id == BootPhaseId.PhaseA_Diagnostics
            && diagnostics.Name == "PhaseA.Diagnostics"
            && diagnostics.Precondition.Length > 0
            && diagnostics.Postcondition.Length > 0
            && futureRuntime.Id == BootPhaseId.PhaseD_Runtime
            && futureRuntime.Precondition.Length > 0
            && futureRuntime.Postcondition.Length > 0);
    }

    [HostTest("boot.phase catalog marker text matches plan", HostLane.Fast, 14)]
    private static bool TestBootPhaseMarkersMatchPlan()
    {
        BootPhaseInfo audit = BootPhaseCatalog.GetImplementedPhase(0);
        BootPhaseInfo diagnostics = BootPhaseCatalog.GetImplementedPhase(1);

        return HostCheck.Result(audit.StartMarker == BootSequence.CreatePlanLine(1)
            && audit.PassMarker == BootSequence.CreatePlanLine(2)
            && diagnostics.StartMarker == BootSequence.CreatePlanLine(3)
            && diagnostics.PassMarker == BootSequence.CreatePlanLine(4));
    }

    [HostTest("boot.sequence M06 block MSI-X defaults off and does not invoke callback", HostLane.Fast, 48)]
    private static unsafe bool TestOwnedBlockMsixDefaultsOff()
    {
        int calls = 0;
        return HostCheck.Result(!Probes.EnableM06BlockMsixProbe
            && !Probes.EnableM06BlockMsixPollingRecoveryProbe
            && !BootSequence.RunOwnedBlockMsix(true, &calls, &RecordBlockMsixCall)
            && !BootSequence.RunOwnedBlockMsix(false, &calls, &RecordBlockMsixCall)
            && !BootSequence.RunOwnedBlockMsix(true, null, &RecordBlockMsixCall)
            && !BootSequence.RunOwnedBlockMsix(true, &calls, null)
            && calls == 0);
    }

    private static unsafe bool RecordBlockMsixCall(void* context)
    {
        (*(int*)context)++;
        return true;
    }

    [HostTest("boot.sequence workload service defaults off and rejects incomplete authority", HostLane.Fast, 49)]
    private static unsafe bool TestWorkloadServiceLoopDefaultsOff()
    {
        int calls = 0;
        return HostCheck.Result(!Probes.EnableM16003Probe
            && !BootSequence.IsWorkloadServiceLoopEnabled()
            && !BootSequence.RunOwnedWorkloadServiceLoop(true, true, &calls, &RecordWorkloadServiceCall)
            && !BootSequence.RunOwnedWorkloadServiceLoop(false, true, &calls, &RecordWorkloadServiceCall)
            && !BootSequence.RunOwnedWorkloadServiceLoop(true, false, &calls, &RecordWorkloadServiceCall)
            && !BootSequence.RunOwnedWorkloadServiceLoop(true, true, null, &RecordWorkloadServiceCall)
            && !BootSequence.RunOwnedWorkloadServiceLoop(true, true, &calls, null)
            && calls == 0);
    }

    private static unsafe bool RecordWorkloadServiceCall(void* context)
    {
        (*(int*)context)++;
        return true;
    }

    [HostTest("boot.sequence Linux ELF startup defaults off and rejects incomplete authority", HostLane.Fast, 50)]
    private static unsafe bool TestLinuxElfStartupDefaultsOff()
    {
        int calls = 0;
        return HostCheck.Result(!Probes.EnableM16004Probe
            && !BootSequence.PrepareLinuxElfStartup(true, &calls, &RecordLinuxElfStartupCall)
            && !BootSequence.PrepareLinuxElfStartup(false, &calls, &RecordLinuxElfStartupCall)
            && !BootSequence.PrepareLinuxElfStartup(true, null, &RecordLinuxElfStartupCall)
            && !BootSequence.PrepareLinuxElfStartup(true, &calls, null)
            && !BootSequence.RunOwnedLinuxElfStartup(true, true, &calls, &RecordLinuxElfStartupCall)
            && !BootSequence.RunOwnedLinuxElfStartup(false, true, &calls, &RecordLinuxElfStartupCall)
            && !BootSequence.RunOwnedLinuxElfStartup(true, false, &calls, &RecordLinuxElfStartupCall)
            && !BootSequence.RunOwnedLinuxElfStartup(true, true, null, &RecordLinuxElfStartupCall)
            && !BootSequence.RunOwnedLinuxElfStartup(true, true, &calls, null)
            && calls == 0);
    }

    private static unsafe bool RecordLinuxElfStartupCall(void* context)
    {
        (*(int*)context)++;
        return true;
    }

    [HostTest("boot.sequence Linux syscalls default off and reject incomplete authority", HostLane.Fast, 51)]
    private static unsafe bool TestLinuxSyscallsDefaultsOff()
    {
        int calls = 0;
        return HostCheck.Result(!Probes.EnableM16005Probe
            && Probes.GetM16005Probe().Group == "app.linux-syscalls" && Probes.GetM16005Probe().Name == "EnableM16005Probe"
            && !Probes.GetM16005Probe().EnabledByDefault && Probes.GetM16005Probe().Risk == ProbeRisk.Destructive
            && !BootSequence.PrepareLinuxUserBoundary(true, &calls, &RecordLinuxSyscallCall)
            && !BootSequence.PrepareLinuxUserBoundary(false, &calls, &RecordLinuxSyscallCall)
            && !BootSequence.PrepareLinuxUserBoundary(true, null, &RecordLinuxSyscallCall)
            && !BootSequence.PrepareLinuxUserBoundary(true, &calls, null)
            && !BootSequence.RunOwnedLinuxSyscalls(true, true, true, &calls, &RecordLinuxSyscallCall)
            && !BootSequence.RunOwnedLinuxSyscalls(false, true, true, &calls, &RecordLinuxSyscallCall)
            && !BootSequence.RunOwnedLinuxSyscalls(true, false, true, &calls, &RecordLinuxSyscallCall)
            && !BootSequence.RunOwnedLinuxSyscalls(true, true, false, &calls, &RecordLinuxSyscallCall)
            && !BootSequence.RunOwnedLinuxSyscalls(true, true, true, null, &RecordLinuxSyscallCall)
            && !BootSequence.RunOwnedLinuxSyscalls(true, true, true, &calls, null)
            && calls == 0);
    }

    private static unsafe bool RecordLinuxSyscallCall(void* context)
    {
        (*(int*)context)++;
        return true;
    }

    [HostTest("boot.sequence Linux syscall source order follows startup and service stop", HostLane.Fast, 52)]
    private static bool TestLinuxSyscallSourceOrder()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string source = Regex.Replace(File.ReadAllText(Path.Combine(root, "src", "OS", "Boot", "EfiEntry.cs")),
            @"\s+", string.Empty, RegexOptions.CultureInvariant);
        string[] calls =
        [
            "BootSequence.StageLinuxElfPackage(", "BootSequence.PrepareLinuxElfStartup(",
            "BootSequence.PrepareLinuxUserBoundary(", "BootSequence.RunFirstRealExitBootServicesTransition(",
            "BootSequence.RunOwnedLinuxElfPackage(", "BootSequence.RunOwnedLinuxElfStartup(",
            "BootSequence.RunOwnedWorkloadServiceLoop(", "BootSequence.RunOwnedLinuxSyscalls("
        ];
        int previous = -1;
        foreach (string call in calls)
        {
            int at = source.IndexOf(call, StringComparison.Ordinal);
            if (at <= previous || source.IndexOf(call, at + call.Length, StringComparison.Ordinal) >= 0)
                return HostCheck.Result(false, "Linux syscall preparation/owned call order: " + call);
            previous = at;
        }
        return HostCheck.Result(true);
    }

    private static BootHandoffRecord CreateValidOwnedHandoffRecord()
    {
        return BootHandoffRecord.CreateUefiWithOwnedMemoryMap(
            0x100000,
            4096,
            0x900000,
            128 * 1024,
            48 * 3,
            0x77,
            48,
            1,
            3);
    }

    private static EfiMemoryMapSummary CreateValidMemoryMapSummary()
    {
        return new EfiMemoryMapSummary(
            descriptorCount: 3,
            totalPages: 48,
            totalBytes: 48 * EfiMemoryMapNormalizer.PageSize,
            conventionalPages: 32,
            lowestPhysicalAddress: 0x100000,
            highestPhysicalEndExclusive: 0x130000);
    }

    private static FaultInfrastructureReadinessResult CreateReadyFaultInfrastructureResult()
    {
        return FaultInfrastructureReadiness.RequireReadyForPrivilegedInstall(
            serialDiagnosticsReady: true,
            IdtTablePlanResult.Success(),
            IdtrDescriptor.Create(0xffff800000100000, IdtTablePlan.EncodedByteLength),
            gdtReady: true,
            tssIstReady: true,
            doubleFaultIstReady: true);
    }

    private static UefiExitBootServicesPlanResult CreateExitBootServicesPlan(
        UefiExitBootServicesPlanStatus status)
    {
        return UefiExitBootServicesPlanResult.Create(
            status,
            EfiBootHandoffStatus.Success,
            shouldRetry: status == UefiExitBootServicesPlanStatus.RetryRequired,
            attemptIndex: 1,
            maximumRetryCount: 3);
    }

    private static SerialDiagnosticsGuardResult CreateReadyPostFirmwareDiagnosticsResult()
    {
        SerialDiagnosticsGuard guard = new SerialDiagnosticsGuard();
        guard.MarkOwnedSerialReady();
        return guard.RequirePostFirmwareDiagnostics("exitboot.preflight");
    }

    private static Com1PortIoBackendPlanResult CreateReadyCom1BackendResult()
    {
        return Com1PortIoBackendPlanResult.Success(
            Com1PortIoBackendPlan.RequiredExecutableStubBytes,
            availableStubBytes: 4096);
    }

    private static DescriptorTableInstallReadinessResult CreateReadyDescriptorInstallResult()
    {
        return DescriptorTableInstallReadinessResult.Pass(
            IdtTablePlanStatus.Ok,
            IdtrDescriptorStatus.Ok,
            GdtDescriptorPlanStatus.Ok,
            TssIstPlanStatus.Ok);
    }

    private static FaultProbeReadinessResult CreateReadyFaultProbeReadinessResult()
    {
        byte[] invalidOpcodeBuffer = new byte[FaultProbeStubPlan.InvalidOpcodeRequiredByteLength];
        byte[] nullWriteBuffer = new byte[FaultProbeStubPlan.NullWriteRequiredByteLength];
        ExecutableStubMemoryPlanResult memoryPlan =
            ExecutableStubMemoryPlan.Create((nuint)FaultProbeReadiness.RequiredPayloadBytes);

        return FaultProbeReadiness.RequireReady(invalidOpcodeBuffer, nullWriteBuffer, memoryPlan);
    }

    private static unsafe T WithByteAt<T>(
        T value,
        int offset,
        byte replacement)
        where T : unmanaged
    {
        if (offset < 0 || offset >= sizeof(T))
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        *((byte*)&value + offset) = replacement;
        return value;
    }

    private sealed class SequenceTestSerialSink : ISerialSink
    {
        private readonly List<string> _lines = new List<string>();

        public int Count => _lines.Count;

        public void WriteLine(string line)
        {
            _lines.Add(line);
        }

        public string LineAt(int index)
        {
            return _lines[index];
        }

        public bool Contains(string value)
        {
            return _lines.Contains(value);
        }

        public bool ContainsPrefix(string value)
        {
            for (int index = 0; index < _lines.Count; index++)
            {
                if (_lines[index].StartsWith(value, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
