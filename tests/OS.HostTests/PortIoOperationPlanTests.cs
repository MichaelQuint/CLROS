using System.Runtime.CompilerServices;
using OS.Kernel.Diagnostics;

internal static class PortIoOperationPlanTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("port io plan encodes out8 operation", TestEncodesOut8Operation, ref failures);
        Run("port io plan encodes in8 operation", TestEncodesIn8Operation, ref failures);
        Run("port io plan rejects invalid inputs", TestRejectsInvalidInputs, ref failures);
        Run("port io plan default result is not success", TestDefaultResultIsNotSuccess, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Port I/O operation plan host tests failed: " + failures);
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

    private static bool TestEncodesOut8Operation()
    {
        byte[] bytes = new byte[PortIoOperationPlan.RequiredByteLength];

        PortIoOperationPlanResult result = PortIoOperationPlan.Out8.Emit(bytes);

        return result.Succeeded
            && result.Status == PortIoOperationPlanStatus.Success
            && result.BytesWritten == PortIoOperationPlan.RequiredByteLength
            && result.RequiredBytes == PortIoOperationPlan.RequiredByteLength
            && PortIoOperationPlan.Out8.OperationKind == PortIoOperationKind.Out8
            && bytes[0] == PortIoOperationPlan.Out8Opcode
            && bytes[0] == 0xee;
    }

    private static bool TestEncodesIn8Operation()
    {
        byte[] bytes = new byte[PortIoOperationPlan.RequiredByteLength];

        PortIoOperationPlanResult result = PortIoOperationPlan.In8.Emit(bytes);

        return result.Succeeded
            && result.Status == PortIoOperationPlanStatus.Success
            && result.BytesWritten == PortIoOperationPlan.RequiredByteLength
            && result.RequiredBytes == PortIoOperationPlan.RequiredByteLength
            && PortIoOperationPlan.In8.OperationKind == PortIoOperationKind.In8
            && bytes[0] == PortIoOperationPlan.In8Opcode
            && bytes[0] == 0xec;
    }

    private static bool TestRejectsInvalidInputs()
    {
        byte[] bytes = new byte[PortIoOperationPlan.RequiredByteLength];
        byte[] shortBytes = Array.Empty<byte>();

        PortIoOperationPlanResult invalidDefault = default(PortIoOperationPlan).Emit(bytes);
        PortIoOperationPlanResult invalidUnknown =
            new PortIoOperationPlan((PortIoOperationKind)255).Emit(bytes);
        PortIoOperationPlanResult shortBuffer = PortIoOperationPlan.Out8.Emit(shortBytes);

        return !invalidDefault.Succeeded
            && invalidDefault.Status == PortIoOperationPlanStatus.InvalidOperationKind
            && invalidDefault.BytesWritten == 0
            && invalidDefault.RequiredBytes == PortIoOperationPlan.RequiredByteLength
            && !invalidUnknown.Succeeded
            && invalidUnknown.Status == PortIoOperationPlanStatus.InvalidOperationKind
            && invalidUnknown.BytesWritten == 0
            && invalidUnknown.RequiredBytes == PortIoOperationPlan.RequiredByteLength
            && !shortBuffer.Succeeded
            && shortBuffer.Status == PortIoOperationPlanStatus.OutputBufferTooSmall
            && shortBuffer.BytesWritten == 0
            && shortBuffer.RequiredBytes == PortIoOperationPlan.RequiredByteLength
            && bytes[0] == 0;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        PortIoOperationPlanResult result = default;
        PortIoOperationPlanResult failed =
            PortIoOperationPlanResult.Fail(PortIoOperationPlanStatus.InvalidOperationKind);

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == PortIoOperationPlanStatus.Success
            && result.BytesWritten == 0
            && result.RequiredBytes == 0
            && failed.IsInitialized
            && !failed.Succeeded
            && failed.Status == PortIoOperationPlanStatus.InvalidOperationKind
            && failed.BytesWritten == 0
            && failed.RequiredBytes == PortIoOperationPlan.RequiredByteLength
            && !PortIoOperationPlanResult.Success(0).Succeeded
            && !RuntimeHelpers.IsReferenceOrContainsReferences<PortIoOperationPlanResult>();
    }
}
