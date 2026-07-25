using System.Runtime.CompilerServices;
using OS.Kernel.Diagnostics;
using OS.Kernel.Runtime;

internal static class Com1PortIoBackendContractTests
{
    private const nuint PayloadBaseAddress = 0x0010_0000;

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run(
            "com1 port io backend contract resolves stub addresses",
            TestResolvesStubAddresses,
            ref failures);
        Run(
            "com1 port io backend contract rejects failed readiness",
            TestRejectsFailedReadiness,
            ref failures);
        Run(
            "com1 port io backend contract rejects missing stub slots",
            TestRejectsMissingStubSlots,
            ref failures);
        Run(
            "com1 port io backend contract rejects invalid payload base or overflow",
            TestRejectsInvalidPayloadBaseOrOverflow,
            ref failures);
        Run(
            "com1 port io backend contract default result is not success",
            TestDefaultResultIsNotSuccess,
            ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("COM1 port I/O backend contract host tests failed: " + failures);
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

    private static bool TestResolvesStubAddresses()
    {
        Com1PortIoBackendPlanResult backendReadiness = CreateReadyBackendReadiness();
        ExecutableStubBundlePlanResult stubBundlePlan = CreateCom1StubBundlePlan();

        Com1PortIoBackendContractResult result = Com1PortIoBackendContract.Resolve(
            backendReadiness,
            stubBundlePlan,
            PayloadBaseAddress);

        return result.Succeeded
            && result.IsInitialized
            && result.Status == Com1PortIoBackendContractStatus.Success
            && result.BackendPlanStatus == Com1PortIoBackendPlanStatus.Success
            && result.StubBundlePlanStatus == ExecutableStubBundlePlanStatus.Success
            && result.ConfiguredBasePort == SerialPortConfiguration.Com1BasePort
            && result.Out8StubOffset == 0
            && result.In8StubOffset == 16
            && result.Out8StubAddress == PayloadBaseAddress
            && result.In8StubAddress == PayloadBaseAddress + 16
            && Com1PortIoBackendContract.RequiredCom1BasePort == SerialPortConfiguration.Com1BasePort
            && Com1PortIoBackendContract.Out8PortParameterBits == 16
            && Com1PortIoBackendContract.Out8ValueParameterBits == 8
            && Com1PortIoBackendContract.In8PortParameterBits == 16
            && Com1PortIoBackendContract.In8ReturnValueBits == 8;
    }

    private static bool TestRejectsFailedReadiness()
    {
        Com1PortIoBackendPlan invalidPlan = new Com1PortIoBackendPlan(
            default,
            PortIoOperationPlan.Out8,
            PortIoOperationPlan.In8,
            PortIoStubPlan.Out8,
            PortIoStubPlan.In8,
            ExecutableStubMemoryPlan.Create(
                Com1PortIoBackendPlan.RequiredExecutableStubBytes,
                ExecutableStubMemoryPlan.DefaultPageSize,
                ExecutableStubMemoryPlan.DefaultAlignment));

        Com1PortIoBackendPlanResult backendReadiness = invalidPlan.Validate();
        ExecutableStubBundlePlanResult stubBundlePlan = CreateCom1StubBundlePlan();

        Com1PortIoBackendContractResult result = Com1PortIoBackendContract.Resolve(
            backendReadiness,
            stubBundlePlan,
            PayloadBaseAddress);

        return !result.Succeeded
            && result.IsInitialized
            && result.Status == Com1PortIoBackendContractStatus.BackendReadinessFailed
            && result.BackendPlanStatus == Com1PortIoBackendPlanStatus.InvalidSerialConfiguration
            && result.StubBundlePlanStatus == ExecutableStubBundlePlanStatus.Success
            && result.ConfiguredBasePort == SerialPortConfiguration.Com1BasePort
            && result.Out8StubAddress == 0
            && result.In8StubAddress == 0;
    }

    private static bool TestRejectsMissingStubSlots()
    {
        Com1PortIoBackendPlanResult backendReadiness = CreateReadyBackendReadiness();

        Span<ExecutableStubBundleSlot> missingOut8Slots = stackalloc ExecutableStubBundleSlot[]
        {
            new ExecutableStubBundleSlot(ExecutableStubSlotKind.Com1In8, PortIoStubPlan.In8RequiredByteLength)
        };
        Span<ExecutableStubBundleSlot> missingIn8Slots = stackalloc ExecutableStubBundleSlot[]
        {
            new ExecutableStubBundleSlot(ExecutableStubSlotKind.Com1Out8, PortIoStubPlan.Out8RequiredByteLength)
        };

        ExecutableStubBundlePlanResult missingOut8Bundle = ExecutableStubBundlePlan.Create(
            missingOut8Slots,
            ExecutableStubMemoryPlan.Create(64));
        ExecutableStubBundlePlanResult missingIn8Bundle = ExecutableStubBundlePlan.Create(
            missingIn8Slots,
            ExecutableStubMemoryPlan.Create(64));

        Com1PortIoBackendContractResult missingOut8Result = Com1PortIoBackendContract.Resolve(
            backendReadiness,
            missingOut8Bundle,
            PayloadBaseAddress);
        Com1PortIoBackendContractResult missingIn8Result = Com1PortIoBackendContract.Resolve(
            backendReadiness,
            missingIn8Bundle,
            PayloadBaseAddress);

        return missingOut8Bundle.Succeeded
            && missingIn8Bundle.Succeeded
            && !missingOut8Result.Succeeded
            && missingOut8Result.Status == Com1PortIoBackendContractStatus.MissingOut8StubSlot
            && missingOut8Result.Out8StubOffset == ExecutableStubBundlePlan.MissingSlotOffset
            && missingOut8Result.In8StubOffset == 0
            && !missingIn8Result.Succeeded
            && missingIn8Result.Status == Com1PortIoBackendContractStatus.MissingIn8StubSlot
            && missingIn8Result.Out8StubOffset == 0
            && missingIn8Result.In8StubOffset == ExecutableStubBundlePlan.MissingSlotOffset;
    }

    private static bool TestRejectsInvalidPayloadBaseOrOverflow()
    {
        Com1PortIoBackendPlanResult backendReadiness = CreateReadyBackendReadiness();
        ExecutableStubBundlePlanResult stubBundlePlan = CreateCom1StubBundlePlan();

        Com1PortIoBackendContractResult zeroBaseResult = Com1PortIoBackendContract.Resolve(
            backendReadiness,
            stubBundlePlan,
            0);

        ExecutableStubBundlePlanResult overflowBundlePlan =
            ExecutableStubBundlePlanResult.Success(
                slotCount: 2,
                totalPayloadLength: 16,
                requiredMemoryBytes: 4096,
                com1Out8Offset: nuint.MaxValue - 1,
                com1In8Offset: 0,
                installIdtOffset: ExecutableStubBundlePlan.MissingSlotOffset,
                installGdtOffset: ExecutableStubBundlePlan.MissingSlotOffset,
                loadTaskRegisterOffset: ExecutableStubBundlePlan.MissingSlotOffset,
                invalidOpcodeProbeOffset: ExecutableStubBundlePlan.MissingSlotOffset,
                nullWriteProbeOffset: ExecutableStubBundlePlan.MissingSlotOffset);

        Com1PortIoBackendContractResult overflowResult = Com1PortIoBackendContract.Resolve(
            backendReadiness,
            overflowBundlePlan,
            2);
        Com1PortIoBackendContractResult invalidBasePortResult = Com1PortIoBackendContract.Resolve(
            backendReadiness,
            stubBundlePlan,
            PayloadBaseAddress,
            configuredBasePort: 0x02f8);

        return !zeroBaseResult.Succeeded
            && zeroBaseResult.Status == Com1PortIoBackendContractStatus.InvalidPayloadBaseAddress
            && zeroBaseResult.Out8StubAddress == 0
            && zeroBaseResult.In8StubAddress == 0
            && !overflowResult.Succeeded
            && overflowResult.Status == Com1PortIoBackendContractStatus.AddressOverflow
            && overflowResult.Out8StubOffset == nuint.MaxValue - 1
            && overflowResult.In8StubOffset == 0
            && overflowResult.Out8StubAddress == 0
            && overflowResult.In8StubAddress == 0
            && !invalidBasePortResult.Succeeded
            && invalidBasePortResult.Status == Com1PortIoBackendContractStatus.InvalidCom1BasePort
            && invalidBasePortResult.ConfiguredBasePort == 0x02f8;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        Com1PortIoBackendContractResult result = default;

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == Com1PortIoBackendContractStatus.Success
            && result.BackendPlanStatus == Com1PortIoBackendPlanStatus.Success
            && result.StubBundlePlanStatus == ExecutableStubBundlePlanStatus.Success
            && result.ConfiguredBasePort == 0
            && result.Out8StubAddress == 0
            && result.In8StubAddress == 0;
    }

    private static Com1PortIoBackendPlanResult CreateReadyBackendReadiness()
    {
        return Com1PortIoBackendPlan.CreateReady().Validate();
    }

    private static ExecutableStubBundlePlanResult CreateCom1StubBundlePlan()
    {
        Span<ExecutableStubBundleSlot> slots = stackalloc ExecutableStubBundleSlot[]
        {
            new ExecutableStubBundleSlot(ExecutableStubSlotKind.Com1Out8, PortIoStubPlan.Out8RequiredByteLength),
            new ExecutableStubBundleSlot(ExecutableStubSlotKind.Com1In8, PortIoStubPlan.In8RequiredByteLength)
        };

        return ExecutableStubBundlePlan.Create(
            slots,
            ExecutableStubMemoryPlan.Create(64));
    }
}
