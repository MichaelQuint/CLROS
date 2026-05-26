using System.Runtime.CompilerServices;
using OS.Kernel.Diagnostics;
using OS.Kernel.Runtime;

internal static class Com1PortIoBackendPlanTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run(
            "com1 port io backend plan accepts ready prerequisites",
            TestAcceptsReadyPrerequisites,
            ref failures);
        Run(
            "com1 port io backend plan rejects invalid serial config",
            TestRejectsInvalidSerialConfiguration,
            ref failures);
        Run(
            "com1 port io backend plan rejects missing stub readiness",
            TestRejectsMissingStubReadiness,
            ref failures);
        Run(
            "com1 port io backend plan rejects insufficient stub memory plan",
            TestRejectsInsufficientStubMemoryPlan,
            ref failures);
        Run(
            "com1 port io backend plan default result is not success",
            TestDefaultResultIsNotSuccess,
            ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("COM1 port I/O backend plan host tests failed: " + failures);
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

    private static bool TestAcceptsReadyPrerequisites()
    {
        Com1PortIoBackendPlanResult result =
            Com1PortIoBackendPlan.CreateReady().Validate();

        return result.Succeeded
            && result.IsInitialized
            && result.Status == Com1PortIoBackendPlanStatus.Success
            && result.SerialConfigurationStatus == SerialPortConfigurationStatus.Success
            && result.OperationPlanStatus == PortIoOperationPlanStatus.Success
            && result.StubPlanStatus == PortIoStubPlanStatus.Success
            && result.StubMemoryPlanStatus == ExecutableStubMemoryPlanStatus.Success
            && result.RequiredStubBytes == PortIoStubPlan.Out8RequiredByteLength + PortIoStubPlan.In8RequiredByteLength
            && result.RequiredStubBytes == Com1PortIoBackendPlan.RequiredExecutableStubBytes
            && result.AvailableStubBytes >= (nuint)Com1PortIoBackendPlan.RequiredExecutableStubBytes;
    }

    private static bool TestRejectsInvalidSerialConfiguration()
    {
        Com1PortIoBackendPlan plan = new Com1PortIoBackendPlan(
            default,
            PortIoOperationPlan.Out8,
            PortIoOperationPlan.In8,
            PortIoStubPlan.Out8,
            PortIoStubPlan.In8,
            ExecutableStubMemoryPlan.Create(
                Com1PortIoBackendPlan.RequiredExecutableStubBytes,
                ExecutableStubMemoryPlan.DefaultPageSize,
                ExecutableStubMemoryPlan.DefaultAlignment));

        Com1PortIoBackendPlanResult result = plan.Validate();

        return !result.Succeeded
            && result.IsInitialized
            && result.Status == Com1PortIoBackendPlanStatus.InvalidSerialConfiguration
            && result.SerialConfigurationStatus == SerialPortConfigurationStatus.InvalidBasePort
            && result.OperationPlanStatus == PortIoOperationPlanStatus.Success
            && result.StubPlanStatus == PortIoStubPlanStatus.Success
            && result.StubMemoryPlanStatus == ExecutableStubMemoryPlanStatus.Success
            && result.RequiredStubBytes == Com1PortIoBackendPlan.RequiredExecutableStubBytes
            && result.AvailableStubBytes >= (nuint)Com1PortIoBackendPlan.RequiredExecutableStubBytes;
    }

    private static bool TestRejectsMissingStubReadiness()
    {
        Com1PortIoBackendPlan missingOperationPlan = new Com1PortIoBackendPlan(
            SerialPortConfiguration.Com1Default,
            default,
            PortIoOperationPlan.In8,
            PortIoStubPlan.Out8,
            PortIoStubPlan.In8,
            ExecutableStubMemoryPlan.Create(
                Com1PortIoBackendPlan.RequiredExecutableStubBytes,
                ExecutableStubMemoryPlan.DefaultPageSize,
                ExecutableStubMemoryPlan.DefaultAlignment));
        Com1PortIoBackendPlan missingStubPlan = new Com1PortIoBackendPlan(
            SerialPortConfiguration.Com1Default,
            PortIoOperationPlan.Out8,
            PortIoOperationPlan.In8,
            default,
            PortIoStubPlan.In8,
            ExecutableStubMemoryPlan.Create(
                Com1PortIoBackendPlan.RequiredExecutableStubBytes,
                ExecutableStubMemoryPlan.DefaultPageSize,
                ExecutableStubMemoryPlan.DefaultAlignment));

        Com1PortIoBackendPlanResult operationResult = missingOperationPlan.Validate();
        Com1PortIoBackendPlanResult stubResult = missingStubPlan.Validate();

        return !operationResult.Succeeded
            && operationResult.IsInitialized
            && operationResult.Status == Com1PortIoBackendPlanStatus.MissingOperationPlan
            && operationResult.OperationPlanStatus == PortIoOperationPlanStatus.InvalidOperationKind
            && operationResult.StubPlanStatus == PortIoStubPlanStatus.Success
            && operationResult.StubMemoryPlanStatus == ExecutableStubMemoryPlanStatus.Success
            && !stubResult.Succeeded
            && stubResult.IsInitialized
            && stubResult.Status == Com1PortIoBackendPlanStatus.MissingStubPlan
            && stubResult.OperationPlanStatus == PortIoOperationPlanStatus.Success
            && stubResult.StubPlanStatus == PortIoStubPlanStatus.InvalidOperationKind
            && stubResult.StubMemoryPlanStatus == ExecutableStubMemoryPlanStatus.Success;
    }

    private static bool TestRejectsInsufficientStubMemoryPlan()
    {
        Com1PortIoBackendPlanResult unavailableResult =
            Com1PortIoBackendPlan.CreateReady(
                ExecutableStubMemoryPlan.Create(
                    0,
                    ExecutableStubMemoryPlan.DefaultPageSize,
                    ExecutableStubMemoryPlan.DefaultAlignment))
                    .Validate();
        ExecutableStubMemoryPlanResult tooSmallMemoryPlan =
            ExecutableStubMemoryPlanResult.Create(
                ExecutableStubMemoryPlanStatus.Success,
                Com1PortIoBackendPlan.RequiredExecutableStubBytes,
                ExecutableStubMemoryPlan.DefaultPageSize,
                ExecutableStubMemoryPlan.DefaultAlignment,
                ExecutableStubMemoryPlan.MetadataPrefixByteLength,
                ExecutableStubMemoryPlan.MetadataPrefixByteLength + (nuint)Com1PortIoBackendPlan.RequiredExecutableStubBytes - 1,
                ExecutableStubMemoryProtection.Read | ExecutableStubMemoryProtection.Write,
                ExecutableStubMemoryProtection.Read | ExecutableStubMemoryProtection.Execute,
                ExecutableStubMemoryProtection.Read | ExecutableStubMemoryProtection.Execute,
                ExecutableStubMemoryPlanFlags.None);
        Com1PortIoBackendPlanResult tooSmallResult =
            Com1PortIoBackendPlan.CreateReady(tooSmallMemoryPlan).Validate();

        return !unavailableResult.Succeeded
            && unavailableResult.IsInitialized
            && unavailableResult.Status == Com1PortIoBackendPlanStatus.StubMemoryPlanUnavailable
            && unavailableResult.StubMemoryPlanStatus == ExecutableStubMemoryPlanStatus.InvalidPayloadLength
            && unavailableResult.RequiredStubBytes == Com1PortIoBackendPlan.RequiredExecutableStubBytes
            && unavailableResult.AvailableStubBytes == 0
            && !tooSmallResult.Succeeded
            && tooSmallResult.IsInitialized
            && tooSmallResult.Status == Com1PortIoBackendPlanStatus.StubMemoryPlanTooSmall
            && tooSmallResult.StubMemoryPlanStatus == ExecutableStubMemoryPlanStatus.Success
            && tooSmallResult.RequiredStubBytes == Com1PortIoBackendPlan.RequiredExecutableStubBytes
            && tooSmallResult.AvailableStubBytes == (nuint)Com1PortIoBackendPlan.RequiredExecutableStubBytes - 1;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        Com1PortIoBackendPlanResult result = default;

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == Com1PortIoBackendPlanStatus.Success
            && result.SerialConfigurationStatus == SerialPortConfigurationStatus.Success
            && result.OperationPlanStatus == PortIoOperationPlanStatus.Success
            && result.StubPlanStatus == PortIoStubPlanStatus.Success
            && result.StubMemoryPlanStatus == ExecutableStubMemoryPlanStatus.Success
            && result.RequiredStubBytes == 0
            && result.AvailableStubBytes == 0;
    }
}
