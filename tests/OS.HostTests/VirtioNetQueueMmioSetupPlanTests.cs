using System.Runtime.CompilerServices;
using System.Reflection;
using OS.Drivers;
using OS.Net;
using OS.Std.Binary;
using OS.Storage;

internal static partial class VirtioNetQueueMmioSetupPlanTests
{
    private const byte CommonOffset = 0x40;
    private const byte NotifyOffset = 0x60;
    private const byte IsrOffset = 0x80;
    private const byte DeviceOffset = 0xA0;
    private const ushort RxQueueSize = 8;
    private const ushort TxQueueSize = 4;
    private const ushort RxBufferCount = 3;
    private const ushort RxDeviceMaximumQueueSize = 16;
    private const ushort TxDeviceMaximumQueueSize = 8;
    private const ushort RxNotifyOffset = 3;
    private const ushort TxNotifyOffset = 5;
    private const uint FrameBufferBytes = VirtioNetQueuePlan.MaximumFrameBufferBytes;
    [HostTest("drivers.virtionet.queue.mmio creates setup command plan", HostLane.Fast, 0)]
    private static bool TestCreatesSetupCommandPlan()
    {
        VirtioNetDriverQueueBringUpPlanResult bringUp = CreateBringUpPlan();
        VirtioNetQueueMmioSetupCommand[] commands = new VirtioNetQueueMmioSetupCommand[VirtioNetQueueMmioSetupPlanner.RequiredCommandCount];
        VirtioNetQueueMmioSetupPlanResult result = VirtioNetQueueMmioSetupPlanner.Create(bringUp, CreateRxAddresses(), CreateTxAddresses(), CreateRxMetadata(), CreateTxMetadata(), commands);
        VirtioNetQueueMmioSetupPlan plan = result.Plan;
        return HostCheck.Result(result.Succeeded && result.Status == VirtioNetQueueMmioSetupPlanStatus.Success && result.BringUpStatus == VirtioNetDriverQueueBringUpPlanStatus.Success && result.RequiredCommandCount == VirtioNetQueueMmioSetupPlanner.RequiredCommandCount && result.AvailableCommandCount == VirtioNetQueueMmioSetupPlanner.RequiredCommandCount && result.CommandCount == VirtioNetQueueMmioSetupPlanner.RequiredCommandCount && plan.RxQueue.QueueIndex == VirtioNetQueuePlan.ReceiveQueueIndex && plan.RxQueue.PlannedQueueSize == RxQueueSize && plan.RxQueue.DeviceMaximumQueueSize == RxDeviceMaximumQueueSize && plan.RxQueue.NotifyOffset == RxNotifyOffset && plan.RxQueue.CommandStartIndex == 0 && plan.TxQueue.QueueIndex == VirtioNetQueuePlan.TransmitQueueIndex && plan.TxQueue.PlannedQueueSize == TxQueueSize && plan.TxQueue.DeviceMaximumQueueSize == TxDeviceMaximumQueueSize && plan.TxQueue.NotifyOffset == TxNotifyOffset && plan.TxQueue.CommandStartIndex == VirtioNetQueueMmioSetupPlanner.CommandsPerQueue && CommandsMatchQueue(commands.AsSpan(0, VirtioNetQueueMmioSetupPlanner.CommandsPerQueue), plan.RxQueue) && CommandsMatchQueue(commands.AsSpan(VirtioNetQueueMmioSetupPlanner.CommandsPerQueue, VirtioNetQueueMmioSetupPlanner.CommandsPerQueue), plan.TxQueue) && !VirtioNetQueueMmioSetupPlanner.InvokesRegisterAccessor && !VirtioNetQueueMmioSetupPlanner.WritesMmioRegisters && !VirtioNetQueueMmioSetupPlanner.PublishesDescriptors && !VirtioNetQueueMmioSetupPlanner.NotifiesDevice && !VirtioNetQueueMmioSetupPlanner.HandlesInterrupts && !VirtioNetQueueMmioSetupPlanner.SendsOrReceivesPackets && !VirtioNetQueueMmioSetupPlanner.SupportsNotificationData);
    }

    [HostTest("drivers.virtionet.queue.mmio rejects command destination", HostLane.Fast, 1)]
    private static bool TestRejectsCommandDestination()
    {
        VirtioNetQueueMmioSetupCommand[] commands = new VirtioNetQueueMmioSetupCommand[VirtioNetQueueMmioSetupPlanner.RequiredCommandCount - 1];
        VirtioNetQueueMmioSetupPlanResult result = VirtioNetQueueMmioSetupPlanner.Create(CreateBringUpPlan(), CreateRxAddresses(), CreateTxAddresses(), CreateRxMetadata(), CreateTxMetadata(), commands);
        return HostCheck.Result(!result.Succeeded && result.Status == VirtioNetQueueMmioSetupPlanStatus.CommandDestinationTooSmall && result.RequiredCommandCount == VirtioNetQueueMmioSetupPlanner.RequiredCommandCount && result.AvailableCommandCount == VirtioNetQueueMmioSetupPlanner.RequiredCommandCount - 1);
    }

    [HostTest("drivers.virtionet.queue.mmio rejects queue metadata", HostLane.Fast, 2)]
    private static bool TestRejectsQueueMetadata()
    {
        VirtioNetDriverQueueBringUpPlanResult bringUp = CreateBringUpPlan();
        VirtioNetQueueMmioSetupCommand[] commands = new VirtioNetQueueMmioSetupCommand[VirtioNetQueueMmioSetupPlanner.RequiredCommandCount];
        VirtioNetQueueMmioSetupPlanResult unavailable = VirtioNetQueueMmioSetupPlanner.Create(bringUp, CreateRxAddresses(), CreateTxAddresses(), new VirtioNetQueueMmioSetupQueueDeviceMetadata(0, true, RxNotifyOffset), CreateTxMetadata(), commands);
        VirtioNetQueueMmioSetupPlanResult rejectedSize = VirtioNetQueueMmioSetupPlanner.Create(bringUp, CreateRxAddresses(), CreateTxAddresses(), new VirtioNetQueueMmioSetupQueueDeviceMetadata(RxQueueSize - 1, true, RxNotifyOffset), CreateTxMetadata(), commands);
        VirtioNetQueueMmioSetupPlanResult missingNotify = VirtioNetQueueMmioSetupPlanner.Create(bringUp, CreateRxAddresses(), CreateTxAddresses(), new VirtioNetQueueMmioSetupQueueDeviceMetadata(RxDeviceMaximumQueueSize, false, 0), CreateTxMetadata(), commands);
        return HostCheck.Result(!unavailable.Succeeded && unavailable.Status == VirtioNetQueueMmioSetupPlanStatus.QueueUnavailable && unavailable.FailingQueueIndex == VirtioNetQueuePlan.ReceiveQueueIndex && !rejectedSize.Succeeded && rejectedSize.Status == VirtioNetQueueMmioSetupPlanStatus.QueueSizeRejected && rejectedSize.PlannedQueueSize == RxQueueSize && rejectedSize.DeviceMaximumQueueSize == RxQueueSize - 1 && !missingNotify.Succeeded && missingNotify.Status == VirtioNetQueueMmioSetupPlanStatus.MissingNotifyOffset && missingNotify.FailingQueueIndex == VirtioNetQueuePlan.ReceiveQueueIndex);
    }

    [HostTest("drivers.virtionet.queue.mmio rejects physical addresses", HostLane.Fast, 3)]
    private static bool TestRejectsPhysicalAddresses()
    {
        VirtioNetDriverQueueBringUpPlanResult bringUp = CreateBringUpPlan();
        VirtioNetQueueMmioSetupCommand[] commands = new VirtioNetQueueMmioSetupCommand[VirtioNetQueueMmioSetupPlanner.RequiredCommandCount];
        VirtioNetQueueMmioSetupPlanResult zeroDescriptor = VirtioNetQueueMmioSetupPlanner.Create(bringUp, new VirtioNetQueueMmioSetupQueueAddresses(0, CreateRxAddresses().DriverRingPhysicalAddress, CreateRxAddresses().DeviceRingPhysicalAddress), CreateTxAddresses(), CreateRxMetadata(), CreateTxMetadata(), commands);
        VirtioNetQueueMmioSetupPlanResult misalignedDriver = VirtioNetQueueMmioSetupPlanner.Create(bringUp, new VirtioNetQueueMmioSetupQueueAddresses(CreateRxAddresses().DescriptorTablePhysicalAddress, CreateRxAddresses().DriverRingPhysicalAddress + 1, CreateRxAddresses().DeviceRingPhysicalAddress), CreateTxAddresses(), CreateRxMetadata(), CreateTxMetadata(), commands);
        VirtioNetQueueMmioSetupPlanResult deviceOverflow = VirtioNetQueueMmioSetupPlanner.Create(bringUp, new VirtioNetQueueMmioSetupQueueAddresses(CreateRxAddresses().DescriptorTablePhysicalAddress, CreateRxAddresses().DriverRingPhysicalAddress, ulong.MaxValue - 3UL), CreateTxAddresses(), CreateRxMetadata(), CreateTxMetadata(), commands);
        return HostCheck.Result(!zeroDescriptor.Succeeded && zeroDescriptor.Status == VirtioNetQueueMmioSetupPlanStatus.InvalidPhysicalAddress && zeroDescriptor.FailingAddressKind == VirtioNetQueueMmioSetupAddressKind.DescriptorTable && zeroDescriptor.FailingPhysicalAddress == 0 && !misalignedDriver.Succeeded && misalignedDriver.Status == VirtioNetQueueMmioSetupPlanStatus.InvalidPhysicalAddress && misalignedDriver.FailingAddressKind == VirtioNetQueueMmioSetupAddressKind.DriverRing && !deviceOverflow.Succeeded && deviceOverflow.Status == VirtioNetQueueMmioSetupPlanStatus.PhysicalAddressOverflow && deviceOverflow.FailingAddressKind == VirtioNetQueueMmioSetupAddressKind.DeviceRing);
    }

    [HostTest("drivers.virtionet.queue.mmio rejects failed bringup", HostLane.Fast, 4)]
    private static bool TestRejectsFailedBringUp()
    {
        VirtioNetQueueMmioSetupCommand[] commands = new VirtioNetQueueMmioSetupCommand[VirtioNetQueueMmioSetupPlanner.RequiredCommandCount];
        VirtioNetQueueMmioSetupPlanResult result = VirtioNetQueueMmioSetupPlanner.Create(CreateBringUpPlan(rxQueueSize: 7), CreateRxAddresses(), CreateTxAddresses(), CreateRxMetadata(), CreateTxMetadata(), commands);
        VirtioNetQueueMmioSetupPlanResult defaultBringUp = VirtioNetQueueMmioSetupPlanner.Create(default, CreateRxAddresses(), CreateTxAddresses(), CreateRxMetadata(), CreateTxMetadata(), commands);
        return HostCheck.Result(!result.Succeeded && result.Status == VirtioNetQueueMmioSetupPlanStatus.BringUpPlanFailed && result.BringUpStatus == VirtioNetDriverQueueBringUpPlanStatus.QueuePlanFailed && result.AvailableCommandCount == VirtioNetQueueMmioSetupPlanner.RequiredCommandCount && !defaultBringUp.Succeeded && defaultBringUp.Status == VirtioNetQueueMmioSetupPlanStatus.BringUpPlanFailed && defaultBringUp.BringUpStatus == VirtioNetDriverQueueBringUpPlanStatus.Success && defaultBringUp.AvailableCommandCount == VirtioNetQueueMmioSetupPlanner.RequiredCommandCount && defaultBringUp.RequiredCommandCount == VirtioNetQueueMmioSetupPlanner.RequiredCommandCount);
    }

    [HostTest("drivers.virtionet.queue.mmio default results are not success", HostLane.Fast, 5)]
    private static bool TestDefaultResultsAreNotSuccess()
    {
        VirtioNetQueueMmioSetupPlanResult setup = default;
        VirtioNetQueueMmioSetupExecutionResult execution = default;
        VirtioNetQueueMmioSetupRegisterWindowPlanResult windowPlan = default;
        VirtioNetQueueMmioSetupRegisterWindowExecutionResult windowExecution = default;
        return HostCheck.Result(!setup.IsInitialized && !setup.Succeeded && setup.Status == VirtioNetQueueMmioSetupPlanStatus.Success && !execution.IsInitialized && !execution.Succeeded && execution.Status == VirtioNetQueueMmioSetupExecutionStatus.Success && !windowPlan.IsInitialized && !windowPlan.Succeeded && windowPlan.Status == VirtioNetQueueMmioSetupRegisterWindowPlanStatus.Success && !windowExecution.IsInitialized && !windowExecution.Succeeded && windowExecution.Status == VirtioNetQueueMmioSetupRegisterWindowExecutionStatus.Success && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioNetQueueMmioSetupPlanResult>() && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioNetQueueMmioSetupExecutionResult>() && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioNetQueueMmioSetupRegisterWindowPlanResult>() && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioNetQueueMmioSetupRegisterWindowExecutionResult>());
    }

    [HostTest("drivers.virtionet.queue.mmio.exec writes planned rx tx setup", HostLane.Fast, 7)]
    [HostTest("drivers.virtionet.queue.mmio.exec writes planned commands", HostLane.Fast, 6)]
    private static bool TestExecutesPlannedCommands()
    {
        VirtioNetQueueMmioSetupPlanResult setup = CreateSetupPlan(out VirtioNetQueueMmioSetupCommand[] commands, out VirtioPciBarAperture commonAperture);
        FakeRegisterAccessor accessor = new FakeRegisterAccessor();
        VirtioNetQueueMmioSetupExecutionResult result = VirtioNetQueueMmioSetupExecutor.Execute(setup, commands, commonAperture, accessor);
        return HostCheck.Result(setup.Succeeded && result.Succeeded && result.Status == VirtioNetQueueMmioSetupExecutionStatus.Success && result.SetupPlanStatus == VirtioNetQueueMmioSetupPlanStatus.Success && result.CommandsExecuted == VirtioNetQueueMmioSetupPlanner.RequiredCommandCount && accessor.OperationCount == VirtioNetQueueMmioSetupPlanner.RequiredCommandCount && !accessor.IsRead[0] && accessor.Widths[0] == sizeof(ushort) && accessor.Offsets[0] == VirtioPciModernRegisterMap.CommonConfiguration.QueueSelectOffset && accessor.Values[0] == VirtioNetQueuePlan.ReceiveQueueIndex && accessor.IsRead[1] && accessor.Offsets[1] == VirtioPciModernRegisterMap.CommonConfiguration.QueueSizeOffset && accessor.Values[1] == RxDeviceMaximumQueueSize && accessor.Values[3] == CreateRxAddresses().DescriptorTablePhysicalAddress && accessor.Values[4] == CreateRxAddresses().DriverRingPhysicalAddress && accessor.Values[5] == CreateRxAddresses().DeviceRingPhysicalAddress && accessor.IsRead[6] && accessor.Values[6] == RxNotifyOffset && accessor.Values[7] == 1 && accessor.Values[8] == VirtioNetQueuePlan.TransmitQueueIndex && accessor.IsRead[9] && accessor.Values[9] == TxDeviceMaximumQueueSize && accessor.Values[11] == CreateTxAddresses().DescriptorTablePhysicalAddress && accessor.Values[12] == CreateTxAddresses().DriverRingPhysicalAddress && accessor.Values[13] == CreateTxAddresses().DeviceRingPhysicalAddress && accessor.IsRead[14] && accessor.Values[14] == TxNotifyOffset && accessor.Values[15] == 1);
    }

    [HostTest("drivers.virtionet.queue.mmio.exec rejects failed setup plan", HostLane.Fast, 8)]
    private static bool TestExecutionRejectsFailedSetupPlan()
    {
        VirtioNetQueueMmioSetupCommand[] commands = new VirtioNetQueueMmioSetupCommand[VirtioNetQueueMmioSetupPlanner.RequiredCommandCount];
        VirtioNetQueueMmioSetupPlanResult failedSetup = VirtioNetQueueMmioSetupPlanner.Create(CreateBringUpPlan(rxQueueSize: 7), CreateRxAddresses(), CreateTxAddresses(), CreateRxMetadata(), CreateTxMetadata(), commands);
        FakeRegisterAccessor accessor = new FakeRegisterAccessor();
        VirtioNetQueueMmioSetupExecutionResult result = VirtioNetQueueMmioSetupExecutor.Execute(failedSetup, commands, CreateBindingPlan().Binding.Apertures.CommonConfiguration, accessor);
        return HostCheck.Result(!failedSetup.Succeeded && !result.Succeeded && result.Status == VirtioNetQueueMmioSetupExecutionStatus.SetupPlanFailed && result.SetupPlanStatus == VirtioNetQueueMmioSetupPlanStatus.BringUpPlanFailed && result.RegisterReadStatus == VirtioPciRegisterReadStatus.NotReady && result.RegisterWriteStatus == VirtioPciRegisterWriteStatus.NotReady && result.CommandIndex == -1 && result.CommandsExecuted == 0 && accessor.OperationCount == 0);
    }

    [HostTest("drivers.virtionet.queue.mmio.exec rejects short command source", HostLane.Fast, 9)]
    private static bool TestExecutionRejectsShortCommandSource()
    {
        VirtioNetQueueMmioSetupPlanResult setup = CreateSetupPlan(out VirtioNetQueueMmioSetupCommand[] commands, out VirtioPciBarAperture commonAperture);
        FakeRegisterAccessor accessor = new FakeRegisterAccessor();
        VirtioNetQueueMmioSetupExecutionResult result = VirtioNetQueueMmioSetupExecutor.Execute(setup, commands.AsSpan(0, commands.Length - 1), commonAperture, accessor);
        return HostCheck.Result(setup.Succeeded && !result.Succeeded && result.Status == VirtioNetQueueMmioSetupExecutionStatus.CommandSourceTooSmall && result.SetupPlanStatus == VirtioNetQueueMmioSetupPlanStatus.Success && result.CommandIndex == commands.Length - 1 && result.CommandsExecuted == commands.Length - 1 && accessor.OperationCount == 0);
    }

    [HostTest("drivers.virtionet.queue.mmio.exec rejects read mismatch", HostLane.Fast, 10)]
    private static bool TestExecutionRejectsReadMismatch()
    {
        VirtioNetQueueMmioSetupPlanResult setup = CreateSetupPlan(out VirtioNetQueueMmioSetupCommand[] commands, out VirtioPciBarAperture commonAperture);
        FakeRegisterAccessor accessor = new FakeRegisterAccessor
        {
            MismatchReadOperationIndex = 1,
            MismatchReadValue = RxDeviceMaximumQueueSize - 1
        };
        VirtioNetQueueMmioSetupExecutionResult result = VirtioNetQueueMmioSetupExecutor.Execute(setup, commands, commonAperture, accessor);
        return HostCheck.Result(!result.Succeeded && result.Status == VirtioNetQueueMmioSetupExecutionStatus.RegisterReadMismatch && result.SetupPlanStatus == VirtioNetQueueMmioSetupPlanStatus.Success && result.RegisterReadStatus == VirtioPciRegisterReadStatus.Success && result.CommandKind == VirtioNetQueueMmioSetupCommandKind.ReadQueueSize && result.CommandIndex == 1 && result.CommandsExecuted == 1 && result.QueueIndex == VirtioNetQueuePlan.ReceiveQueueIndex && result.ExpectedValue == RxDeviceMaximumQueueSize && result.ObservedValue == RxDeviceMaximumQueueSize - 1 && accessor.OperationCount == 2);
    }

    [HostTest("drivers.virtionet.queue.mmio.exec propagates accessor failure", HostLane.Fast, 11)]
    private static bool TestExecutionPropagatesAccessorFailure()
    {
        VirtioNetQueueMmioSetupPlanResult setup = CreateSetupPlan(out VirtioNetQueueMmioSetupCommand[] commands, out VirtioPciBarAperture commonAperture);
        FakeRegisterAccessor writeFailure = new FakeRegisterAccessor
        {
            FailureOperationIndex = 3,
            WriteFailureStatus = VirtioPciRegisterWriteStatus.WriteFailed
        };
        FakeRegisterAccessor readFailure = new FakeRegisterAccessor
        {
            FailureOperationIndex = 6,
            ReadFailureStatus = VirtioPciRegisterReadStatus.ReadFailed
        };
        FakeRegisterAccessor notReady = new FakeRegisterAccessor
        {
            FailureOperationIndex = 1,
            ReadFailureStatus = VirtioPciRegisterReadStatus.NotReady
        };
        FakeRegisterAccessor writeNotReady = new FakeRegisterAccessor
        {
            FailureOperationIndex = 3,
            WriteFailureStatus = VirtioPciRegisterWriteStatus.NotReady
        };
        VirtioNetQueueMmioSetupExecutionResult writeResult = VirtioNetQueueMmioSetupExecutor.Execute(setup, commands, commonAperture, writeFailure);
        VirtioNetQueueMmioSetupExecutionResult readResult = VirtioNetQueueMmioSetupExecutor.Execute(setup, commands, commonAperture, readFailure);
        VirtioNetQueueMmioSetupExecutionResult notReadyResult = VirtioNetQueueMmioSetupExecutor.Execute(setup, commands, commonAperture, notReady);
        VirtioNetQueueMmioSetupExecutionResult writeNotReadyResult = VirtioNetQueueMmioSetupExecutor.Execute(setup, commands, commonAperture, writeNotReady);
        return HostCheck.Result(!writeResult.Succeeded && writeResult.Status == VirtioNetQueueMmioSetupExecutionStatus.RegisterWriteFailed && writeResult.RegisterWriteStatus == VirtioPciRegisterWriteStatus.WriteFailed && writeResult.CommandKind == VirtioNetQueueMmioSetupCommandKind.WriteQueueDescriptor && writeResult.CommandIndex == 3 && writeResult.CommandsExecuted == 3 && writeFailure.OperationCount == 4 && !readResult.Succeeded && readResult.Status == VirtioNetQueueMmioSetupExecutionStatus.RegisterReadFailed && readResult.RegisterReadStatus == VirtioPciRegisterReadStatus.ReadFailed && readResult.CommandKind == VirtioNetQueueMmioSetupCommandKind.ReadQueueNotifyOffset && readResult.CommandIndex == 6 && readResult.CommandsExecuted == 6 && readFailure.OperationCount == 7 && !notReadyResult.Succeeded && notReadyResult.Status == VirtioNetQueueMmioSetupExecutionStatus.AccessorNotReady && notReadyResult.RegisterReadStatus == VirtioPciRegisterReadStatus.NotReady && notReadyResult.CommandKind == VirtioNetQueueMmioSetupCommandKind.ReadQueueSize && notReadyResult.CommandIndex == 1 && notReadyResult.CommandsExecuted == 1 && notReady.OperationCount == 2 && !writeNotReadyResult.Succeeded && writeNotReadyResult.Status == VirtioNetQueueMmioSetupExecutionStatus.AccessorNotReady && writeNotReadyResult.RegisterWriteStatus == VirtioPciRegisterWriteStatus.NotReady && writeNotReadyResult.CommandKind == VirtioNetQueueMmioSetupCommandKind.WriteQueueDescriptor && writeNotReadyResult.CommandIndex == 3 && writeNotReadyResult.CommandsExecuted == 3 && writeNotReady.OperationCount == 4);
    }

    [HostTest("drivers.virtionet.queue.mmio.exec propagates read failure", HostLane.Fast, 12)]
    private static bool TestExecutionPropagatesReadFailure()
    {
        VirtioNetQueueMmioSetupPlanResult setup = CreateSetupPlan(out VirtioNetQueueMmioSetupCommand[] commands, out VirtioPciBarAperture commonAperture);
        FakeRegisterAccessor accessor = new FakeRegisterAccessor
        {
            FailureOperationIndex = 6,
            ReadFailureStatus = VirtioPciRegisterReadStatus.ReadFailed
        };
        VirtioNetQueueMmioSetupExecutionResult result = VirtioNetQueueMmioSetupExecutor.Execute(setup, commands, commonAperture, accessor);
        return HostCheck.Result(!result.Succeeded && result.Status == VirtioNetQueueMmioSetupExecutionStatus.RegisterReadFailed && result.RegisterReadStatus == VirtioPciRegisterReadStatus.ReadFailed && result.RegisterWriteStatus == VirtioPciRegisterWriteStatus.Success && result.CommandKind == VirtioNetQueueMmioSetupCommandKind.ReadQueueNotifyOffset && result.CommandIndex == 6 && result.CommandsExecuted == 6 && accessor.OperationCount == 7);
    }

    [HostTest("drivers.virtionet.queue.mmio.exec propagates write failure", HostLane.Fast, 13)]
    private static bool TestExecutionPropagatesWriteFailure()
    {
        VirtioNetQueueMmioSetupPlanResult setup = CreateSetupPlan(out VirtioNetQueueMmioSetupCommand[] commands, out VirtioPciBarAperture commonAperture);
        FakeRegisterAccessor accessor = new FakeRegisterAccessor
        {
            FailureOperationIndex = 3,
            WriteFailureStatus = VirtioPciRegisterWriteStatus.WriteFailed
        };
        VirtioNetQueueMmioSetupExecutionResult result = VirtioNetQueueMmioSetupExecutor.Execute(setup, commands, commonAperture, accessor);
        return HostCheck.Result(!result.Succeeded && result.Status == VirtioNetQueueMmioSetupExecutionStatus.RegisterWriteFailed && result.RegisterReadStatus == VirtioPciRegisterReadStatus.Success && result.RegisterWriteStatus == VirtioPciRegisterWriteStatus.WriteFailed && result.CommandKind == VirtioNetQueueMmioSetupCommandKind.WriteQueueDescriptor && result.CommandIndex == 3 && result.CommandsExecuted == 3 && accessor.OperationCount == 4);
    }

    [HostTest("drivers.virtionet.queue.mmio.exec rejects missing accessor", HostLane.Fast, 14)]
    private static bool TestExecutionRejectsMissingAccessor()
    {
        VirtioNetQueueMmioSetupPlanResult setup = CreateSetupPlan(out VirtioNetQueueMmioSetupCommand[] commands, out VirtioPciBarAperture commonAperture);
        VirtioNetQueueMmioSetupExecutionResult result = VirtioNetQueueMmioSetupExecutor.Execute(setup, commands, commonAperture, accessor: null);
        return HostCheck.Result(!result.Succeeded && result.Status == VirtioNetQueueMmioSetupExecutionStatus.AccessorNotReady && result.SetupPlanStatus == VirtioNetQueueMmioSetupPlanStatus.Success && result.RegisterReadStatus == VirtioPciRegisterReadStatus.NotReady && result.RegisterWriteStatus == VirtioPciRegisterWriteStatus.NotReady && result.CommandKind == VirtioNetQueueMmioSetupCommandKind.WriteQueueSelect && result.CommandIndex == 0 && result.CommandsExecuted == 0);
    }

    [HostTest("drivers.virtionet.queue.mmio.exec preserves no-live-mmio boundary", HostLane.Fast, 15)]
    private static bool TestExecutionPreservesNoLiveMmioBoundary()
    {
        VirtioNetQueueMmioSetupPlanResult setup = CreateSetupPlan(out VirtioNetQueueMmioSetupCommand[] commands, out VirtioPciBarAperture commonAperture);
        FakeRegisterAccessor accessor = new FakeRegisterAccessor();
        VirtioNetQueueMmioSetupExecutionResult result = VirtioNetQueueMmioSetupExecutor.Execute(setup, commands, commonAperture, accessor);
        return HostCheck.Result(result.Succeeded && VirtioNetQueueMmioSetupExecutor.RequiresInjectedAccessor && !VirtioNetQueueMmioSetupExecutor.PerformsLiveMmio && !VirtioNetQueueMmioSetupExecutor.MapsBars && !VirtioNetQueueMmioSetupExecutor.PublishesDescriptors && !VirtioNetQueueMmioSetupExecutor.NotifiesDevice && !VirtioNetQueueMmioSetupExecutor.HandlesInterrupts && !VirtioNetQueueMmioSetupExecutor.PerformsDmaMapping && !VirtioNetQueueMmioSetupExecutor.AllocatesDmaMemory && !VirtioNetQueueMmioSetupExecutor.PerformsPacketIo && !VirtioNetQueueMmioSetupExecutor.ReadsOrWritesPacketBuffers && !VirtioNetQueueMmioSetupPlanner.InvokesRegisterAccessor && !VirtioNetQueueMmioSetupPlanner.WritesMmioRegisters && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioNetQueueMmioSetupExecutionResult>() && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioPciRegisterRead16Result>());
    }

    [HostTest("drivers.virtionet.queue.mmio.window plans setup command accesses", HostLane.Fast, 16)]
    private static bool TestRegisterWindowPlansSetupCommandAccesses()
    {
        VirtioNetQueueMmioSetupPlanResult setup = CreateSetupPlan(out VirtioNetQueueMmioSetupCommand[] commands, out _);
        VirtioPciBarRegisterWindowBindingResult registerWindows = CreateRegisterWindows();
        VirtioPciBarRegisterWindowAccessPlan[] accessPlans = new VirtioPciBarRegisterWindowAccessPlan[VirtioNetQueueMmioSetupPlanner.RequiredCommandCount];
        VirtioNetQueueMmioSetupRegisterWindowPlanResult result = VirtioNetQueueMmioSetupRegisterWindowPlanner.PlanAccesses(setup, commands, registerWindows.Bindings.CommonConfiguration, accessPlans);
        return HostCheck.Result(setup.Succeeded && registerWindows.Succeeded && result.Succeeded && result.Status == VirtioNetQueueMmioSetupRegisterWindowPlanStatus.Success && result.SetupPlanStatus == VirtioNetQueueMmioSetupPlanStatus.Success && result.RegisterWindowStatus == VirtioPciBarRegisterWindowPlanStatus.Success && result.AccessPlansWritten == VirtioNetQueueMmioSetupPlanner.RequiredCommandCount && result.RequiredAccessPlanCount == VirtioNetQueueMmioSetupPlanner.RequiredCommandCount && result.AvailableAccessPlanCount == VirtioNetQueueMmioSetupPlanner.RequiredCommandCount && accessPlans[0].CapabilityType == VirtioPciCapabilityType.CommonConfiguration && accessPlans[0].BarIndex == 0 && accessPlans[0].RelativeOffset == VirtioPciModernRegisterMap.CommonConfiguration.QueueSelectOffset && accessPlans[0].Width == VirtioPciRegisterAccessWidth.Word && accessPlans[0].WidthBytes == sizeof(ushort) && accessPlans[0].BarRelativeOffset == 0x20UL + VirtioPciModernRegisterMap.CommonConfiguration.QueueSelectOffset && accessPlans[0].PhysicalAddress == 0x80000000UL + 0x20UL + VirtioPciModernRegisterMap.CommonConfiguration.QueueSelectOffset && accessPlans[0].VirtualAddress == 0xFFFF800000000000UL + 0x20UL + VirtioPciModernRegisterMap.CommonConfiguration.QueueSelectOffset && accessPlans[3].RelativeOffset == VirtioPciModernRegisterMap.CommonConfiguration.QueueDescOffset && accessPlans[3].Width == VirtioPciRegisterAccessWidth.QWord && accessPlans[3].WidthBytes == sizeof(ulong) && accessPlans[3].BarRelativeOffset == 0x20UL + VirtioPciModernRegisterMap.CommonConfiguration.QueueDescOffset && accessPlans[8].RelativeOffset == VirtioPciModernRegisterMap.CommonConfiguration.QueueSelectOffset && accessPlans[14].RelativeOffset == VirtioPciModernRegisterMap.CommonConfiguration.QueueNotifyOffOffset && accessPlans[15].RelativeOffset == VirtioPciModernRegisterMap.CommonConfiguration.QueueEnableOffset);
    }

    [HostTest("drivers.virtionet.queue.mmio.window rejects failed setup plan", HostLane.Fast, 17)]
    private static bool TestRegisterWindowRejectsFailedSetupPlan()
    {
        VirtioNetQueueMmioSetupCommand[] commands = new VirtioNetQueueMmioSetupCommand[VirtioNetQueueMmioSetupPlanner.RequiredCommandCount];
        VirtioNetQueueMmioSetupPlanResult failedSetup = VirtioNetQueueMmioSetupPlanner.Create(CreateBringUpPlan(rxQueueSize: 7), CreateRxAddresses(), CreateTxAddresses(), CreateRxMetadata(), CreateTxMetadata(), commands);
        VirtioPciBarRegisterWindowAccessPlan[] accessPlans = new VirtioPciBarRegisterWindowAccessPlan[VirtioNetQueueMmioSetupPlanner.RequiredCommandCount];
        VirtioNetQueueMmioSetupRegisterWindowPlanResult result = VirtioNetQueueMmioSetupRegisterWindowPlanner.PlanAccesses(failedSetup, commands, CreateRegisterWindows().Bindings.CommonConfiguration, accessPlans);
        return HostCheck.Result(!failedSetup.Succeeded && !result.Succeeded && result.Status == VirtioNetQueueMmioSetupRegisterWindowPlanStatus.SetupPlanFailed && result.SetupPlanStatus == VirtioNetQueueMmioSetupPlanStatus.BringUpPlanFailed && result.CommandIndex == -1 && result.AccessPlansWritten == 0);
    }

    [HostTest("drivers.virtionet.queue.mmio.window rejects short command source", HostLane.Fast, 18)]
    private static bool TestRegisterWindowRejectsShortCommandSource()
    {
        VirtioNetQueueMmioSetupPlanResult setup = CreateSetupPlan(out VirtioNetQueueMmioSetupCommand[] commands, out _);
        VirtioPciBarRegisterWindowAccessPlan[] accessPlans = new VirtioPciBarRegisterWindowAccessPlan[VirtioNetQueueMmioSetupPlanner.RequiredCommandCount];
        VirtioNetQueueMmioSetupRegisterWindowPlanResult result = VirtioNetQueueMmioSetupRegisterWindowPlanner.PlanAccesses(setup, commands.AsSpan(0, commands.Length - 1), CreateRegisterWindows().Bindings.CommonConfiguration, accessPlans);
        return HostCheck.Result(!result.Succeeded && result.Status == VirtioNetQueueMmioSetupRegisterWindowPlanStatus.CommandSourceTooSmall && result.SetupPlanStatus == VirtioNetQueueMmioSetupPlanStatus.Success && result.CommandIndex == commands.Length - 1 && result.RequiredAccessPlanCount == VirtioNetQueueMmioSetupPlanner.RequiredCommandCount && result.AccessPlansWritten == 0);
    }

    [HostTest("drivers.virtionet.queue.mmio.window rejects short destination", HostLane.Fast, 19)]
    private static bool TestRegisterWindowRejectsShortDestination()
    {
        VirtioNetQueueMmioSetupPlanResult setup = CreateSetupPlan(out VirtioNetQueueMmioSetupCommand[] commands, out _);
        VirtioPciBarRegisterWindowAccessPlan[] accessPlans = new VirtioPciBarRegisterWindowAccessPlan[VirtioNetQueueMmioSetupPlanner.RequiredCommandCount - 1];
        VirtioNetQueueMmioSetupRegisterWindowPlanResult result = VirtioNetQueueMmioSetupRegisterWindowPlanner.PlanAccesses(setup, commands, CreateRegisterWindows().Bindings.CommonConfiguration, accessPlans);
        return HostCheck.Result(!result.Succeeded && result.Status == VirtioNetQueueMmioSetupRegisterWindowPlanStatus.AccessPlanDestinationTooSmall && result.SetupPlanStatus == VirtioNetQueueMmioSetupPlanStatus.Success && result.CommandIndex == accessPlans.Length && result.RequiredAccessPlanCount == VirtioNetQueueMmioSetupPlanner.RequiredCommandCount && result.AvailableAccessPlanCount == accessPlans.Length && result.AccessPlansWritten == 0);
    }

    [HostTest("drivers.virtionet.queue.mmio.window rejects unsupported command width", HostLane.Fast, 20)]
    private static bool TestRegisterWindowRejectsUnsupportedCommandWidth()
    {
        VirtioNetQueueMmioSetupPlanResult setup = CreateSetupPlan(out VirtioNetQueueMmioSetupCommand[] commands, out _);
        commands[0] = CreateCommand(VirtioNetQueueMmioSetupCommandKind.WriteQueueSelect, VirtioNetQueuePlan.ReceiveQueueIndex, VirtioPciModernRegisterMap.CommonConfiguration.QueueSelectOffset, widthBytes: 3, value: VirtioNetQueuePlan.ReceiveQueueIndex);
        VirtioPciBarRegisterWindowAccessPlan[] accessPlans = new VirtioPciBarRegisterWindowAccessPlan[VirtioNetQueueMmioSetupPlanner.RequiredCommandCount];
        VirtioNetQueueMmioSetupRegisterWindowPlanResult result = VirtioNetQueueMmioSetupRegisterWindowPlanner.PlanAccesses(setup, commands, CreateRegisterWindows().Bindings.CommonConfiguration, accessPlans);
        return HostCheck.Result(!result.Succeeded && result.Status == VirtioNetQueueMmioSetupRegisterWindowPlanStatus.UnsupportedCommandWidth && result.RegisterWindowStatus == VirtioPciBarRegisterWindowPlanStatus.UnsupportedAccessWidth && result.CommandKind == VirtioNetQueueMmioSetupCommandKind.WriteQueueSelect && result.CommandIndex == 0 && result.WidthBytes == 3 && result.AccessPlansWritten == 0);
    }

    [HostTest("drivers.virtionet.queue.mmio.window propagates register window failure", HostLane.Fast, 21)]
    private static bool TestRegisterWindowPropagatesRegisterWindowFailure()
    {
        VirtioNetQueueMmioSetupPlanResult setup = CreateSetupPlan(out VirtioNetQueueMmioSetupCommand[] commands, out _);
        VirtioPciBarRegisterWindowAccessPlan[] accessPlans = new VirtioPciBarRegisterWindowAccessPlan[VirtioNetQueueMmioSetupPlanner.RequiredCommandCount];
        VirtioNetQueueMmioSetupRegisterWindowPlanResult result = VirtioNetQueueMmioSetupRegisterWindowPlanner.PlanAccesses(setup, commands, default, accessPlans);
        return HostCheck.Result(!result.Succeeded && result.Status == VirtioNetQueueMmioSetupRegisterWindowPlanStatus.RegisterWindowPlanFailed && result.RegisterWindowStatus == VirtioPciBarRegisterWindowPlanStatus.MissingWindow && result.CommandKind == VirtioNetQueueMmioSetupCommandKind.WriteQueueSelect && result.CommandIndex == 0 && result.AccessPlansWritten == 0);
    }

    [HostTest("drivers.virtionet.queue.mmio.window preserves no-live-mmio boundary", HostLane.Fast, 22)]
    private static bool TestRegisterWindowPreservesNoLiveMmioBoundary()
    {
        VirtioNetQueueMmioSetupPlanResult setup = CreateSetupPlan(out VirtioNetQueueMmioSetupCommand[] commands, out _);
        VirtioPciBarRegisterWindowAccessPlan[] accessPlans = new VirtioPciBarRegisterWindowAccessPlan[VirtioNetQueueMmioSetupPlanner.RequiredCommandCount];
        VirtioNetQueueMmioSetupRegisterWindowPlanResult result = VirtioNetQueueMmioSetupRegisterWindowPlanner.PlanAccesses(setup, commands, CreateRegisterWindows().Bindings.CommonConfiguration, accessPlans);
        return HostCheck.Result(result.Succeeded && VirtioNetQueueMmioSetupRegisterWindowPlanner.RequiresRegisterWindowBinding && VirtioNetQueueMmioSetupRegisterWindowPlanner.WritesAccessPlansToCallerStorage && !VirtioNetQueueMmioSetupRegisterWindowPlanner.MapsBars && !VirtioNetQueueMmioSetupRegisterWindowPlanner.PerformsLiveMmio && !VirtioNetQueueMmioSetupRegisterWindowPlanner.ContainsVolatileAccess && !VirtioNetQueueMmioSetupRegisterWindowPlanner.ReadsDeviceRegisters && !VirtioNetQueueMmioSetupRegisterWindowPlanner.WritesDeviceRegisters && !VirtioNetQueueMmioSetupRegisterWindowPlanner.ConfiguresQueues && !VirtioNetQueueMmioSetupRegisterWindowPlanner.NotifiesLiveDevice && !VirtioNetQueueMmioSetupRegisterWindowPlanner.PollsDevice && !VirtioNetQueueMmioSetupRegisterWindowPlanner.HandlesInterrupts && !VirtioNetQueueMmioSetupRegisterWindowPlanner.AllocatesDmaMemory && !VirtioNetQueueMmioSetupRegisterWindowPlanner.PerformsDmaMapping && !VirtioNetQueueMmioSetupRegisterWindowPlanner.PerformsPacketIo && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioNetQueueMmioSetupRegisterWindowPlanResult>());
    }

    [HostTest("drivers.virtionet.queue.mmio.windowexec writes planned setup accesses", HostLane.Fast, 23)]
    private static bool TestRegisterWindowExecutionWritesPlannedSetupAccesses()
    {
        VirtioNetQueueMmioSetupPlanResult setup = CreateSetupPlan(out VirtioNetQueueMmioSetupCommand[] commands, out _);
        VirtioPciBarRegisterWindowBindingResult registerWindows = CreateRegisterWindows();
        byte[] commonBytes = CreateCommonWindowBytes();
        VirtioNetQueueMmioSetupRegisterWindowExecutionResult result = VirtioNetQueueMmioSetupRegisterWindowExecutor.Execute(setup, commands, registerWindows.Bindings.CommonConfiguration, commonBytes);
        return HostCheck.Result(setup.Succeeded && registerWindows.Succeeded && result.Succeeded && result.Status == VirtioNetQueueMmioSetupRegisterWindowExecutionStatus.Success && result.SetupPlanStatus == VirtioNetQueueMmioSetupPlanStatus.Success && result.RegisterWindowStatus == VirtioPciBarRegisterWindowPlanStatus.Success && result.AccessExecutionStatus == VirtioPciBarRegisterWindowAccessExecutionStatus.Success && result.CommandsExecuted == VirtioNetQueueMmioSetupPlanner.RequiredCommandCount && ReadUInt16(commonBytes, VirtioPciModernRegisterMap.CommonConfiguration.QueueSelectOffset) == VirtioNetQueuePlan.TransmitQueueIndex && ReadUInt16(commonBytes, VirtioPciModernRegisterMap.CommonConfiguration.QueueSizeOffset) == TxQueueSize && ReadUInt64(commonBytes, VirtioPciModernRegisterMap.CommonConfiguration.QueueDescOffset) == CreateTxAddresses().DescriptorTablePhysicalAddress && ReadUInt64(commonBytes, VirtioPciModernRegisterMap.CommonConfiguration.QueueDriverOffset) == CreateTxAddresses().DriverRingPhysicalAddress && ReadUInt64(commonBytes, VirtioPciModernRegisterMap.CommonConfiguration.QueueDeviceOffset) == CreateTxAddresses().DeviceRingPhysicalAddress && ReadUInt16(commonBytes, VirtioPciModernRegisterMap.CommonConfiguration.QueueNotifyOffOffset) == TxNotifyOffset && ReadUInt16(commonBytes, VirtioPciModernRegisterMap.CommonConfiguration.QueueEnableOffset) == 1);
    }

    [HostTest("drivers.virtionet.queue.mmio.windowexec rejects failed setup plan", HostLane.Fast, 24)]
    private static bool TestRegisterWindowExecutionRejectsFailedSetupPlan()
    {
        VirtioNetQueueMmioSetupCommand[] commands = new VirtioNetQueueMmioSetupCommand[VirtioNetQueueMmioSetupPlanner.RequiredCommandCount];
        VirtioNetQueueMmioSetupPlanResult failedSetup = VirtioNetQueueMmioSetupPlanner.Create(CreateBringUpPlan(rxQueueSize: 7), CreateRxAddresses(), CreateTxAddresses(), CreateRxMetadata(), CreateTxMetadata(), commands);
        byte[] commonBytes = CreateCommonWindowBytes();
        VirtioNetQueueMmioSetupRegisterWindowExecutionResult result = VirtioNetQueueMmioSetupRegisterWindowExecutor.Execute(failedSetup, commands, CreateRegisterWindows().Bindings.CommonConfiguration, commonBytes);
        return HostCheck.Result(!failedSetup.Succeeded && !result.Succeeded && result.Status == VirtioNetQueueMmioSetupRegisterWindowExecutionStatus.SetupPlanFailed && result.SetupPlanStatus == VirtioNetQueueMmioSetupPlanStatus.BringUpPlanFailed && result.CommandIndex == -1 && result.CommandsExecuted == 0 && AllBytesEqual(commonBytes, 0xCC));
    }

    [HostTest("drivers.virtionet.queue.mmio.windowexec rejects short command source", HostLane.Fast, 25)]
    private static bool TestRegisterWindowExecutionRejectsShortCommandSource()
    {
        VirtioNetQueueMmioSetupPlanResult setup = CreateSetupPlan(out VirtioNetQueueMmioSetupCommand[] commands, out _);
        byte[] commonBytes = CreateCommonWindowBytes();
        VirtioNetQueueMmioSetupRegisterWindowExecutionResult result = VirtioNetQueueMmioSetupRegisterWindowExecutor.Execute(setup, commands.AsSpan(0, commands.Length - 1), CreateRegisterWindows().Bindings.CommonConfiguration, commonBytes);
        return HostCheck.Result(!result.Succeeded && result.Status == VirtioNetQueueMmioSetupRegisterWindowExecutionStatus.CommandSourceTooSmall && result.SetupPlanStatus == VirtioNetQueueMmioSetupPlanStatus.Success && result.CommandIndex == commands.Length - 1 && result.CommandsExecuted == commands.Length - 1 && AllBytesEqual(commonBytes, 0xCC));
    }

    [HostTest("drivers.virtionet.queue.mmio.windowexec rejects read mismatch", HostLane.Fast, 26)]
    private static bool TestRegisterWindowExecutionRejectsReadMismatch()
    {
        VirtioNetQueueMmioSetupPlanResult setup = CreateSetupPlan(out VirtioNetQueueMmioSetupCommand[] commands, out _);
        commands[1] = CreateCommand(VirtioNetQueueMmioSetupCommandKind.ReadQueueSize, VirtioNetQueuePlan.ReceiveQueueIndex, VirtioPciModernRegisterMap.CommonConfiguration.QueueSizeOffset, VirtioPciModernRegisterMap.CommonConfiguration.QueueSizeWidth, value: RxDeviceMaximumQueueSize - 1);
        byte[] commonBytes = CreateCommonWindowBytes();
        VirtioNetQueueMmioSetupRegisterWindowExecutionResult result = VirtioNetQueueMmioSetupRegisterWindowExecutor.Execute(setup, commands, CreateRegisterWindows().Bindings.CommonConfiguration, commonBytes);
        return HostCheck.Result(!result.Succeeded && result.Status == VirtioNetQueueMmioSetupRegisterWindowExecutionStatus.RegisterReadMismatch && result.SetupPlanStatus == VirtioNetQueueMmioSetupPlanStatus.Success && result.AccessExecutionStatus == VirtioPciBarRegisterWindowAccessExecutionStatus.Success && result.CommandKind == VirtioNetQueueMmioSetupCommandKind.ReadQueueSize && result.CommandIndex == 1 && result.CommandsExecuted == 1 && result.QueueIndex == VirtioNetQueuePlan.ReceiveQueueIndex && result.ExpectedValue == RxDeviceMaximumQueueSize - 1 && result.ObservedValue == RxDeviceMaximumQueueSize);
    }

    [HostTest("drivers.virtionet.queue.mmio.windowexec propagates access failure", HostLane.Fast, 27)]
    private static bool TestRegisterWindowExecutionPropagatesAccessFailure()
    {
        VirtioNetQueueMmioSetupPlanResult setup = CreateSetupPlan(out VirtioNetQueueMmioSetupCommand[] commands, out _);
        ulong failingLength = 0x20UL + VirtioPciModernRegisterMap.CommonConfiguration.QueueSelectOffset + 1UL;
        byte[] commonBytes = CreateCommonWindowBytes((int)failingLength);
        VirtioNetQueueMmioSetupRegisterWindowExecutionResult result = VirtioNetQueueMmioSetupRegisterWindowExecutor.Execute(setup, commands, CreateRegisterWindows().Bindings.CommonConfiguration, commonBytes);
        return HostCheck.Result(!result.Succeeded && result.Status == VirtioNetQueueMmioSetupRegisterWindowExecutionStatus.AccessExecutionFailed && result.SetupPlanStatus == VirtioNetQueueMmioSetupPlanStatus.Success && result.RegisterWindowStatus == VirtioPciBarRegisterWindowPlanStatus.Success && result.AccessExecutionStatus == VirtioPciBarRegisterWindowAccessExecutionStatus.WindowBufferTooSmall && result.CommandKind == VirtioNetQueueMmioSetupCommandKind.WriteQueueSelect && result.CommandIndex == 0 && result.CommandsExecuted == 0 && AllBytesEqual(commonBytes, 0xCC));
    }

    [HostTest("drivers.virtionet.queue.mmio.windowexec preserves no-live-mmio boundary", HostLane.Fast, 28)]
    private static bool TestRegisterWindowExecutionPreservesNoLiveMmioBoundary()
    {
        VirtioNetQueueMmioSetupPlanResult setup = CreateSetupPlan(out VirtioNetQueueMmioSetupCommand[] commands, out _);
        byte[] commonBytes = CreateCommonWindowBytes();
        VirtioNetQueueMmioSetupRegisterWindowExecutionResult result = VirtioNetQueueMmioSetupRegisterWindowExecutor.Execute(setup, commands, CreateRegisterWindows().Bindings.CommonConfiguration, commonBytes);
        return HostCheck.Result(result.Succeeded && VirtioNetQueueMmioSetupRegisterWindowExecutor.RequiresRegisterWindowBinding && VirtioNetQueueMmioSetupRegisterWindowExecutor.UsesCallerOwnedWindowBuffer && VirtioNetQueueMmioSetupRegisterWindowExecutor.StagesExpectedDeviceReadValues && !VirtioNetQueueMmioSetupRegisterWindowExecutor.MapsBars && !VirtioNetQueueMmioSetupRegisterWindowExecutor.PerformsLiveMmio && !VirtioNetQueueMmioSetupRegisterWindowExecutor.ContainsVolatileAccess && !VirtioNetQueueMmioSetupRegisterWindowExecutor.AccessesPhysicalAddresses && !VirtioNetQueueMmioSetupRegisterWindowExecutor.ExecutesMemoryBarriers && !VirtioNetQueueMmioSetupRegisterWindowExecutor.ConfiguresLiveQueues && !VirtioNetQueueMmioSetupRegisterWindowExecutor.NotifiesLiveDevice && !VirtioNetQueueMmioSetupRegisterWindowExecutor.PollsDevice && !VirtioNetQueueMmioSetupRegisterWindowExecutor.HandlesInterrupts && !VirtioNetQueueMmioSetupRegisterWindowExecutor.AllocatesDmaMemory && !VirtioNetQueueMmioSetupRegisterWindowExecutor.PerformsDmaMapping && !VirtioNetQueueMmioSetupRegisterWindowExecutor.PerformsPacketIo && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioNetQueueMmioSetupRegisterWindowExecutionResult>());
    }

    private static bool CommandsMatchQueue(ReadOnlySpan<VirtioNetQueueMmioSetupCommand> commands, VirtioNetQueueMmioSetupQueuePlan queue)
    {
        return commands[0].Kind == VirtioNetQueueMmioSetupCommandKind.WriteQueueSelect && commands[0].QueueIndex == queue.QueueIndex && commands[0].RelativeOffset == VirtioPciModernRegisterMap.CommonConfiguration.QueueSelectOffset && commands[0].WidthBytes == VirtioPciModernRegisterMap.CommonConfiguration.QueueSelectWidth && commands[0].Value == queue.QueueIndex && commands[1].Kind == VirtioNetQueueMmioSetupCommandKind.ReadQueueSize && commands[1].QueueIndex == queue.QueueIndex && commands[1].Value == queue.DeviceMaximumQueueSize && commands[1].ReadsRegister && !commands[1].WritesRegister && commands[2].Kind == VirtioNetQueueMmioSetupCommandKind.WriteQueueSize && commands[2].Value == queue.PlannedQueueSize && commands[3].Kind == VirtioNetQueueMmioSetupCommandKind.WriteQueueDescriptor && commands[3].WidthBytes == VirtioPciModernRegisterMap.CommonConfiguration.QueueDescWidth && commands[3].Value == queue.Addresses.DescriptorTablePhysicalAddress && commands[4].Kind == VirtioNetQueueMmioSetupCommandKind.WriteQueueDriver && commands[4].Value == queue.Addresses.DriverRingPhysicalAddress && commands[5].Kind == VirtioNetQueueMmioSetupCommandKind.WriteQueueDevice && commands[5].Value == queue.Addresses.DeviceRingPhysicalAddress && commands[6].Kind == VirtioNetQueueMmioSetupCommandKind.ReadQueueNotifyOffset && commands[6].Value == queue.NotifyOffset && commands[6].ReadsRegister && commands[7].Kind == VirtioNetQueueMmioSetupCommandKind.WriteQueueEnable && commands[7].Value == 1;
    }

    private static VirtioNetDriverQueueBringUpPlanResult CreateBringUpPlan(ushort rxQueueSize = RxQueueSize)
    {
        VirtioNetQueuePlan queuePlan = VirtioNetQueuePlan.Create(VirtioNetQueuePlan.MinimumQueueCount, RxQueueSize, TxQueueSize, RxBufferCount, FrameBufferBytes).Plan;
        return VirtioNetDriverQueueBringUpPlan.Create(CreateNetworkDescriptor(), rxQueueSize, TxQueueSize, RxBufferCount, FrameBufferBytes, queuePlan.RxQueue.RequiredByteLength, queuePlan.TxQueue.RequiredByteLength, queuePlan.RxBuffers.RequiredBufferBytes, queuePlan.TxFrame.RequiredBufferBytes);
    }

    private static VirtioNetQueueMmioSetupPlanResult CreateSetupPlan(out VirtioNetQueueMmioSetupCommand[] commands, out VirtioPciBarAperture commonAperture)
    {
        commands = new VirtioNetQueueMmioSetupCommand[VirtioNetQueueMmioSetupPlanner.RequiredCommandCount];
        commonAperture = CreateBindingPlan().Binding.Apertures.CommonConfiguration;
        return VirtioNetQueueMmioSetupPlanner.Create(CreateBringUpPlan(), CreateRxAddresses(), CreateTxAddresses(), CreateRxMetadata(), CreateTxMetadata(), commands);
    }

    private static VirtioPciBarRegisterWindowBindingResult CreateRegisterWindows()
    {
        return VirtioPciBarRegisterWindowPlanner.BindRequiredApertures(CreateBindingPlan(), CreateRegisterWindowSources());
    }

    private static VirtioPciMappedBarWindow[] CreateRegisterWindowSources()
    {
        return [CreateRegisterWindow(0, 0x80000000UL, 0xFFFF800000000000UL), CreateRegisterWindow(1, 0x80001000UL, 0xFFFF800000001000UL), CreateRegisterWindow(2, 0x80002000UL, 0xFFFF800000002000UL), CreateRegisterWindow(3, 0x80003000UL, 0xFFFF800000003000UL), default, default];
    }

    private static VirtioPciMappedBarWindow CreateRegisterWindow(byte barIndex, ulong physicalBaseAddress, ulong virtualBaseAddress)
    {
        return new VirtioPciMappedBarWindow(barIndex, physicalBaseAddress, virtualBaseAddress, length: 0x1000, isMapped: true, isMmio: true, isDeviceMemory: true, isUncached: true);
    }

    private static VirtioNetQueueMmioSetupQueueAddresses CreateRxAddresses()
    {
        return new VirtioNetQueueMmioSetupQueueAddresses(descriptorTablePhysicalAddress: 0x100000, driverRingPhysicalAddress: 0x101000, deviceRingPhysicalAddress: 0x102000);
    }

    private static VirtioNetQueueMmioSetupQueueAddresses CreateTxAddresses()
    {
        return new VirtioNetQueueMmioSetupQueueAddresses(descriptorTablePhysicalAddress: 0x110000, driverRingPhysicalAddress: 0x111000, deviceRingPhysicalAddress: 0x112000);
    }

    private static VirtioNetQueueMmioSetupQueueDeviceMetadata CreateRxMetadata()
    {
        return new VirtioNetQueueMmioSetupQueueDeviceMetadata(RxDeviceMaximumQueueSize, hasNotifyOffset: true, RxNotifyOffset);
    }

    private static VirtioNetQueueMmioSetupQueueDeviceMetadata CreateTxMetadata()
    {
        return new VirtioNetQueueMmioSetupQueueDeviceMetadata(TxDeviceMaximumQueueSize, hasNotifyOffset: true, TxNotifyOffset);
    }

    private static VirtioDeviceDescriptor CreateNetworkDescriptor()
    {
        VirtioDeviceDescriptorSource[] sources = [new VirtioDeviceDescriptorSource(sourceIndex: 40, CreateBindingPlan(), VirtioDeviceInitializationProfile.NetworkOptionalFeatureMask, VirtioDeviceInitializationProfile.NetworkRequiredQueueCount, featuresOkAccepted: true)];
        VirtioDeviceConfigurationSource[] configurations = [new VirtioDeviceConfigurationSource(sourceIndex: 40, CreateNetworkConfiguration(new MacAddress(0x02, 0x33, 0x44, 0x55, 0x66, 0xCC), VirtioNetDeviceConfig.StatusLinkUp))];
        VirtioDeviceDescriptor[] destination = new VirtioDeviceDescriptor[1];
        VirtioDeviceDescriptorCatalogResult result = VirtioDeviceDescriptorCatalog.Build(sources, configurations, destination);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Failed to create deterministic virtio-net descriptor.");
        }

        return destination[0];
    }

    private static VirtioPciBindingPlanResult CreateBindingPlan()
    {
        ushort deviceId = (ushort)(VirtioPciDeviceDiscovery.ModernDeviceIdStart + VirtioPciDeviceDiscovery.NetworkDeviceId);
        return VirtioPciBindingPlan.Create(CreateScannedDevice(deviceId), CreatePciConfiguration(deviceId), CreateBarSizes());
    }

    private static PciEcamScannedDevice CreateScannedDevice(ushort deviceId)
    {
        return new PciEcamScannedDevice(segmentIndex: 2, new PciEcamSegment(basePhysicalAddress: 0xE0000000UL, segmentGroup: 1, startBus: 0, endBus: 0x7F), new PciScannedDevice(bus: 3, device: 5, function: 0, vendorId: VirtioPciDeviceDiscovery.VirtioVendorId, deviceId: deviceId, revisionId: 0, classCode: 0, subclass: 0, programmingInterface: 0, headerType: PciConfigurationHeader.Type0HeaderType, isMultifunction: false, hasCapabilitiesList: true, capabilityPointer: CommonOffset));
    }

    private static byte[] CreatePciConfiguration(ushort deviceId)
    {
        byte[] config = new byte[256];
        EndianPrimitives.WriteUInt16LittleEndian(config.AsSpan(0x00, sizeof(ushort)), VirtioPciDeviceDiscovery.VirtioVendorId);
        EndianPrimitives.WriteUInt16LittleEndian(config.AsSpan(0x02, sizeof(ushort)), deviceId);
        EndianPrimitives.WriteUInt16LittleEndian(config.AsSpan(0x06, sizeof(ushort)), PciConfigurationHeader.StatusCapabilitiesListBit);
        config[0x0E] = PciConfigurationHeader.Type0HeaderType;
        WriteUInt32(config, 0x10, 0x80000000);
        WriteUInt32(config, 0x14, 0x80001000);
        WriteUInt32(config, 0x18, 0x80002000);
        WriteUInt32(config, 0x1C, 0x80003000);
        config[0x34] = CommonOffset;
        WriteVirtioCapability(config, CommonOffset, NotifyOffset, VirtioPciCapabilityType.CommonConfiguration, bar: 0, offset: 0x20, VirtioPciDeviceDiscovery.CommonConfigurationMinimumLength, notifyOffsetMultiplier: 0, capabilityLength: VirtioPciCapabilityListParser.ModernVirtioCapabilityLength);
        WriteVirtioCapability(config, NotifyOffset, IsrOffset, VirtioPciCapabilityType.NotifyConfiguration, bar: 1, offset: 0x80, length: 0x100, notifyOffsetMultiplier: 4, capabilityLength: VirtioPciCapabilityListParser.ModernVirtioNotifyCapabilityLength);
        WriteVirtioCapability(config, IsrOffset, DeviceOffset, VirtioPciCapabilityType.IsrStatus, bar: 2, offset: 0, VirtioPciDeviceDiscovery.IsrStatusMinimumLength, notifyOffsetMultiplier: 0, capabilityLength: VirtioPciCapabilityListParser.ModernVirtioCapabilityLength);
        WriteVirtioCapability(config, DeviceOffset, nextOffset: 0, VirtioPciCapabilityType.DeviceConfiguration, bar: 3, offset: 0x40, VirtioPciDeviceDiscovery.NetworkDeviceConfigurationMinimumLength, notifyOffsetMultiplier: 0, capabilityLength: VirtioPciCapabilityListParser.ModernVirtioCapabilityLength);
        return config;
    }

    private static ulong[] CreateBarSizes()
    {
        return [0x1000, 0x1000, 0x1000, 0x1000, 0, 0];
    }

    private static byte[] CreateNetworkConfiguration(MacAddress macAddress, ushort statusBits)
    {
        byte[] config = new byte[VirtioNetDeviceConfig.StatusConfigLength];
        macAddress.TryWrite(config, VirtioNetDeviceConfig.MacAddressOffset);
        EndianPrimitives.WriteUInt16LittleEndian(config.AsSpan(VirtioNetDeviceConfig.StatusOffset, VirtioNetDeviceConfig.StatusLength), statusBits);
        return config;
    }

    private static void WriteVirtioCapability(Span<byte> config, byte capabilityOffset, byte nextOffset, VirtioPciCapabilityType type, byte bar, uint offset, uint length, uint notifyOffsetMultiplier, byte capabilityLength)
    {
        config[capabilityOffset] = VirtioPciCapabilityListParser.VendorSpecificCapabilityId;
        config[capabilityOffset + 1] = nextOffset;
        config[capabilityOffset + 2] = capabilityLength;
        config[capabilityOffset + 3] = (byte)type;
        config[capabilityOffset + 4] = bar;
        WriteUInt32(config, capabilityOffset + 8, offset);
        WriteUInt32(config, capabilityOffset + 12, length);
        if (capabilityLength >= VirtioPciCapabilityListParser.ModernVirtioNotifyCapabilityLength)
        {
            WriteUInt32(config, capabilityOffset + 16, notifyOffsetMultiplier);
        }
    }

    private static void WriteUInt32(Span<byte> destination, int offset, uint value)
    {
        EndianPrimitives.WriteUInt32LittleEndian(destination.Slice(offset, sizeof(uint)), value);
    }

    private static byte[] CreateCommonWindowBytes(int length = 0x1000)
    {
        byte[] bytes = new byte[length];
        Fill(bytes, 0xCC);
        return bytes;
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> bytes, uint relativeOffset)
    {
        int offset = checked((int)(0x20U + relativeOffset));
        return EndianPrimitives.ReadUInt16LittleEndian(bytes.Slice(offset, sizeof(ushort)));
    }

    private static ulong ReadUInt64(ReadOnlySpan<byte> bytes, uint relativeOffset)
    {
        int offset = checked((int)(0x20U + relativeOffset));
        return EndianPrimitives.ReadUInt64LittleEndian(bytes.Slice(offset, sizeof(ulong)));
    }

    private static bool AllBytesEqual(ReadOnlySpan<byte> bytes, byte expected)
    {
        return AllBytesEqual(bytes, 0, bytes.Length, expected);
    }

    private static bool AllBytesEqual(ReadOnlySpan<byte> bytes, int offset, int count, byte expected)
    {
        for (int index = 0; index < count; index++)
        {
            if (bytes[offset + index] != expected)
            {
                return false;
            }
        }

        return true;
    }

    private static void Fill(Span<byte> bytes, byte value)
    {
        for (int index = 0; index < bytes.Length; index++)
        {
            bytes[index] = value;
        }
    }

    private static VirtioNetQueueMmioSetupCommand CreateCommand(VirtioNetQueueMmioSetupCommandKind kind, ushort queueIndex, uint relativeOffset, uint widthBytes, ulong value)
    {
        ConstructorInfo? constructor = typeof(VirtioNetQueueMmioSetupCommand).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, binder: null, [typeof(VirtioNetQueueMmioSetupCommandKind), typeof(ushort), typeof(uint), typeof(uint), typeof(ulong)], modifiers: null);
        if (constructor is null)
        {
            throw new InvalidOperationException("Virtio-net queue MMIO setup command constructor missing.");
        }

        return (VirtioNetQueueMmioSetupCommand)constructor.Invoke([kind, queueIndex, relativeOffset, widthBytes, value]);
    }
}
