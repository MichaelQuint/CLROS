using System.Runtime.CompilerServices;
using OS.Kernel.Diagnostics;
using OS.Kernel.Runtime;

internal static class FaultProbeReadinessTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run(
            "fault probe readiness accepts disabled planned probes",
            TestAcceptsDisabledPlannedProbes,
            ref failures);
        Run(
            "fault probe readiness rejects enabled destructive defaults",
            TestRejectsEnabledDestructiveDefaults,
            ref failures);
        Run(
            "fault probe readiness rejects invalid stub readiness",
            TestRejectsInvalidStubReadiness,
            ref failures);
        Run(
            "fault probe readiness rejects insufficient executable memory plan",
            TestRejectsInsufficientExecutableMemoryPlan,
            ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Fault probe readiness host tests failed: " + failures);
        }
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {
        Console.WriteLine("TEST_START " + name);

        try
        {
            if (test())
            {
                Console.WriteLine("TEST_PASS " + name);
                return;
            }

            failures++;
            Console.WriteLine("TEST_FAIL " + name);
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static bool TestAcceptsDisabledPlannedProbes()
    {
        byte[] invalidOpcodeBuffer = new byte[FaultProbeStubPlan.InvalidOpcodeRequiredByteLength];
        byte[] nullWriteBuffer = new byte[FaultProbeStubPlan.NullWriteRequiredByteLength];
        ExecutableStubMemoryPlanResult memoryPlan =
            ExecutableStubMemoryPlan.Create((nuint)FaultProbeReadiness.RequiredPayloadBytes);

        FaultProbeReadinessResult result =
            FaultProbeReadiness.RequireReady(invalidOpcodeBuffer, nullWriteBuffer, memoryPlan);

        return result.Succeeded
            && result.Status == FaultProbeReadinessStatus.Ready
            && result.Operation == FaultProbeReadiness.OperationName
            && result.InvalidOpcodeStubStatus == FaultProbeStubPlanStatus.Success
            && result.NullWriteStubStatus == FaultProbeStubPlanStatus.Success
            && result.MemoryPlanStatus == ExecutableStubMemoryPlanStatus.Success
            && result.InvalidOpcodeBytesWritten == FaultProbeStubPlan.InvalidOpcodeRequiredByteLength
            && result.NullWriteBytesWritten == FaultProbeStubPlan.NullWriteRequiredByteLength
            && result.RequiredPayloadBytes == FaultProbeReadiness.RequiredPayloadBytes
            && result.AvailablePayloadBytes == (nuint)FaultProbeReadiness.RequiredPayloadBytes
            && memoryPlan.Succeeded
            && memoryPlan.RequestedPayloadLength >= (nuint)FaultProbeReadiness.RequiredPayloadBytes
            && invalidOpcodeBuffer[0] == FaultProbeStubPlan.TwoByteOpcodePrefix
            && invalidOpcodeBuffer[1] == FaultProbeStubPlan.Ud2Opcode
            && invalidOpcodeBuffer[2] == FaultProbeStubPlan.RetOpcode
            && nullWriteBuffer[0] == FaultProbeStubPlan.MovRm8Imm8Opcode
            && nullWriteBuffer[1] == FaultProbeStubPlan.AbsoluteAddressModRm
            && nullWriteBuffer[2] == FaultProbeStubPlan.AbsoluteAddressSib
            && nullWriteBuffer[7] == FaultProbeStubPlan.NullWriteValue
            && nullWriteBuffer[8] == FaultProbeStubPlan.RetOpcode;
    }

    private static bool TestRejectsEnabledDestructiveDefaults()
    {
        byte[] invalidOpcodeBuffer = new byte[FaultProbeStubPlan.InvalidOpcodeRequiredByteLength];
        byte[] nullWriteBuffer = new byte[FaultProbeStubPlan.NullWriteRequiredByteLength];
        ExecutableStubMemoryPlanResult memoryPlan =
            ExecutableStubMemoryPlan.Create((nuint)FaultProbeReadiness.RequiredPayloadBytes);

        FaultProbeReadinessResult invalidOpcodeEnabled =
            FaultProbeReadiness.RequireReady(
                invalidOpcodeEnabledByDefault: true,
                nullWriteEnabledByDefault: false,
                invalidOpcodeBuffer,
                nullWriteBuffer,
                memoryPlan);

        FaultProbeReadinessResult nullWriteEnabled =
            FaultProbeReadiness.RequireReady(
                invalidOpcodeEnabledByDefault: false,
                nullWriteEnabledByDefault: true,
                invalidOpcodeBuffer,
                nullWriteBuffer,
                memoryPlan);

        return !Probes.GetInvalidOpcodePanicProbe().EnabledByDefault
            && !Probes.GetNullPointerPanicProbe().EnabledByDefault
            && !invalidOpcodeEnabled.Succeeded
            && invalidOpcodeEnabled.Status == FaultProbeReadinessStatus.ProbeEnabledByDefault
            && !nullWriteEnabled.Succeeded
            && nullWriteEnabled.Status == FaultProbeReadinessStatus.ProbeEnabledByDefault;
    }

    private static bool TestRejectsInvalidStubReadiness()
    {
        byte[] validInvalidOpcodeBuffer = new byte[FaultProbeStubPlan.InvalidOpcodeRequiredByteLength];
        byte[] validNullWriteBuffer = new byte[FaultProbeStubPlan.NullWriteRequiredByteLength];
        byte[] shortInvalidOpcodeBuffer = new byte[FaultProbeStubPlan.InvalidOpcodeRequiredByteLength - 1];
        byte[] shortNullWriteBuffer = new byte[FaultProbeStubPlan.NullWriteRequiredByteLength - 1];
        ExecutableStubMemoryPlanResult memoryPlan =
            ExecutableStubMemoryPlan.Create((nuint)FaultProbeReadiness.RequiredPayloadBytes);

        FaultProbeReadinessResult invalidOpcodeFailure =
            FaultProbeReadiness.RequireReady(
                invalidOpcodeEnabledByDefault: false,
                nullWriteEnabledByDefault: false,
                shortInvalidOpcodeBuffer,
                validNullWriteBuffer,
                memoryPlan);

        FaultProbeReadinessResult nullWriteFailure =
            FaultProbeReadiness.RequireReady(
                invalidOpcodeEnabledByDefault: false,
                nullWriteEnabledByDefault: false,
                validInvalidOpcodeBuffer,
                shortNullWriteBuffer,
                memoryPlan);

        return !invalidOpcodeFailure.Succeeded
            && invalidOpcodeFailure.Status == FaultProbeReadinessStatus.InvalidOpcodeStubFailure
            && invalidOpcodeFailure.InvalidOpcodeStubStatus == FaultProbeStubPlanStatus.OutputBufferTooSmall
            && invalidOpcodeFailure.NullWriteStubStatus == FaultProbeStubPlanStatus.Success
            && !nullWriteFailure.Succeeded
            && nullWriteFailure.Status == FaultProbeReadinessStatus.NullWriteStubFailure
            && nullWriteFailure.InvalidOpcodeStubStatus == FaultProbeStubPlanStatus.Success
            && nullWriteFailure.NullWriteStubStatus == FaultProbeStubPlanStatus.OutputBufferTooSmall
            && nullWriteFailure.InvalidOpcodeBytesWritten == FaultProbeStubPlan.InvalidOpcodeRequiredByteLength;
    }

    private static bool TestRejectsInsufficientExecutableMemoryPlan()
    {
        byte[] invalidOpcodeBuffer = new byte[FaultProbeStubPlan.InvalidOpcodeRequiredByteLength];
        byte[] nullWriteBuffer = new byte[FaultProbeStubPlan.NullWriteRequiredByteLength];

        FaultProbeReadinessResult unavailable =
            FaultProbeReadiness.RequireReady(
                invalidOpcodeEnabledByDefault: false,
                nullWriteEnabledByDefault: false,
                invalidOpcodeBuffer,
                nullWriteBuffer,
                ExecutableStubMemoryPlan.Create(0));

        FaultProbeReadinessResult tooSmall =
            FaultProbeReadiness.RequireReady(
                invalidOpcodeEnabledByDefault: false,
                nullWriteEnabledByDefault: false,
                invalidOpcodeBuffer,
                nullWriteBuffer,
                ExecutableStubMemoryPlan.Create((nuint)(FaultProbeReadiness.RequiredPayloadBytes - 1)));

        return !unavailable.Succeeded
            && unavailable.Status == FaultProbeReadinessStatus.MemoryPlanUnavailable
            && unavailable.MemoryPlanStatus == ExecutableStubMemoryPlanStatus.InvalidPayloadLength
            && unavailable.InvalidOpcodeStubStatus == FaultProbeStubPlanStatus.Success
            && unavailable.NullWriteStubStatus == FaultProbeStubPlanStatus.Success
            && !tooSmall.Succeeded
            && tooSmall.Status == FaultProbeReadinessStatus.MemoryPlanTooSmall
            && tooSmall.MemoryPlanStatus == ExecutableStubMemoryPlanStatus.Success
            && tooSmall.AvailablePayloadBytes == (nuint)(FaultProbeReadiness.RequiredPayloadBytes - 1);
    }
}
