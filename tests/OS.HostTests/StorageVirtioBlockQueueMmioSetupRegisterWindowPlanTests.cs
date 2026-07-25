using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Std.Binary;
using OS.Storage;

internal static class StorageVirtioBlockQueueMmioSetupRegisterWindowPlanTests
{
    private const byte CommonOffset = 0x40;
    private const byte NotifyOffset = 0x60;
    private const byte IsrOffset = 0x80;
    private const byte DeviceOffset = 0xA0;
    private const ushort QueueSize = 8;
    private const ushort DeviceMaximumQueueSize = 16;
    private const ushort QueueNotifyOffset = 7;

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run(
            "storage.virtioblk.queuesetup.window plans setup command accesses",
            TestPlansSetupCommandAccesses,
            ref failures);
        Run(
            "storage.virtioblk.queuesetup.window rejects failed setup plan",
            TestRejectsFailedSetupPlan,
            ref failures);
        Run(
            "storage.virtioblk.queuesetup.window rejects short command source",
            TestRejectsShortCommandSource,
            ref failures);
        Run(
            "storage.virtioblk.queuesetup.window reports destination capacity",
            TestReportsDestinationCapacity,
            ref failures);
        Run(
            "storage.virtioblk.queuesetup.window propagates register window failure",
            TestPropagatesRegisterWindowFailure,
            ref failures);
        Run(
            "storage.virtioblk.queuesetup.window default result is not success",
            TestDefaultResultIsNotSuccess,
            ref failures);
        Run(
            "storage.virtioblk.queuesetup.windowexec writes planned setup accesses",
            TestRegisterWindowExecutionWritesPlannedSetupAccesses,
            ref failures);
        Run(
            "storage.virtioblk.queuesetup.windowexec rejects failed setup plan",
            TestRegisterWindowExecutionRejectsFailedSetupPlan,
            ref failures);
        Run(
            "storage.virtioblk.queuesetup.windowexec rejects short command source",
            TestRegisterWindowExecutionRejectsShortCommandSource,
            ref failures);
        Run(
            "storage.virtioblk.queuesetup.windowexec rejects read mismatch",
            TestRegisterWindowExecutionRejectsReadMismatch,
            ref failures);
        Run(
            "storage.virtioblk.queuesetup.windowexec propagates access failure",
            TestRegisterWindowExecutionPropagatesAccessFailure,
            ref failures);
        Run(
            "storage.virtioblk.queuesetup.windowexec preserves no-live-mmio boundary",
            TestRegisterWindowExecutionPreservesNoLiveMmioBoundary,
            ref failures);
        Run(
            "storage.virtioblk.queuesetup.windowexec default result is not success",
            TestDefaultExecutionResultIsNotSuccess,
            ref failures);
        Run(
            "storage.virtioblk.queuesetup.windowbarrier writes queue enable after barrier",
            TestBarrierExecutionWritesQueueEnableAfterBarrier,
            ref failures);
        Run(
            "storage.virtioblk.queuesetup.windowbarrier rejects missing barrier",
            TestBarrierExecutionRejectsMissingBarrier,
            ref failures);
        Run(
            "storage.virtioblk.queuesetup.windowbarrier rejects failed setup plan",
            TestBarrierExecutionRejectsFailedSetupPlan,
            ref failures);
        Run(
            "storage.virtioblk.queuesetup.windowbarrier rejects short command source",
            TestBarrierExecutionRejectsShortCommandSource,
            ref failures);
        Run(
            "storage.virtioblk.queuesetup.windowbarrier propagates access failure",
            TestBarrierExecutionPropagatesAccessFailure,
            ref failures);
        Run(
            "storage.virtioblk.queuesetup.windowbarrier preserves no-live-mmio boundary",
            TestBarrierExecutionPreservesNoLiveMmioBoundary,
            ref failures);
        Run(
            "storage.virtioblk.queuesetup.windowbarrier default result is not success",
            TestDefaultBarrierExecutionResultIsNotSuccess,
            ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException(
                "Virtio-blk queue MMIO setup register-window host tests failed: " + failures);
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

    private static bool TestPlansSetupCommandAccesses()
    {
        VirtioBlockQueueMmioSetupPlanResult setup = CreateSetupPlan(
            out VirtioBlockQueueMmioSetupCommand[] commands);
        VirtioPciBarRegisterWindowBindingResult registerWindows = CreateRegisterWindows();
        VirtioPciBarRegisterWindowAccessPlan[] accessPlans =
            new VirtioPciBarRegisterWindowAccessPlan[VirtioBlockQueueMmioSetupPlanner.RequiredCommandCount];

        VirtioBlockQueueMmioSetupRegisterWindowPlanResult result =
            VirtioBlockQueueMmioSetupRegisterWindowPlanner.PlanAccesses(
                setup,
                commands,
                registerWindows.Bindings.CommonConfiguration,
                accessPlans);

        return setup.Succeeded
            && registerWindows.Succeeded
            && result.Succeeded
            && result.Status == VirtioBlockQueueMmioSetupRegisterWindowPlanStatus.Success
            && result.SetupPlanStatus == VirtioBlockQueueMmioSetupPlanStatus.Success
            && result.RegisterWindowStatus == VirtioPciBarRegisterWindowPlanStatus.Success
            && result.AccessPlansWritten == VirtioBlockQueueMmioSetupPlanner.RequiredCommandCount
            && result.RequiredAccessPlanCount == VirtioBlockQueueMmioSetupPlanner.RequiredCommandCount
            && result.AvailableAccessPlanCount == VirtioBlockQueueMmioSetupPlanner.RequiredCommandCount
            && result.CommandKind == VirtioBlockQueueMmioSetupCommandKind.WriteQueueEnable
            && result.CommandIndex == VirtioBlockQueueMmioSetupPlanner.RequiredCommandCount - 1
            && result.QueueIndex == VirtioBlockQueueMmioSetupPlanner.QueueIndex
            && result.RelativeOffset == VirtioPciModernRegisterMap.CommonConfiguration.QueueEnableOffset
            && result.WidthBytes == VirtioPciModernRegisterMap.CommonConfiguration.QueueEnableWidth
            && accessPlans[0].CapabilityType == VirtioPciCapabilityType.CommonConfiguration
            && accessPlans[0].BarIndex == 0
            && accessPlans[0].RelativeOffset == VirtioPciModernRegisterMap.CommonConfiguration.QueueSelectOffset
            && accessPlans[0].Width == VirtioPciRegisterAccessWidth.Word
            && accessPlans[0].WidthBytes == sizeof(ushort)
            && accessPlans[0].BarRelativeOffset
                == 0x20UL + VirtioPciModernRegisterMap.CommonConfiguration.QueueSelectOffset
            && accessPlans[0].PhysicalAddress
                == 0x80000000UL + 0x20UL + VirtioPciModernRegisterMap.CommonConfiguration.QueueSelectOffset
            && accessPlans[0].VirtualAddress
                == 0xFFFF800000000000UL + 0x20UL + VirtioPciModernRegisterMap.CommonConfiguration.QueueSelectOffset
            && accessPlans[3].RelativeOffset == VirtioPciModernRegisterMap.CommonConfiguration.QueueDescOffset
            && accessPlans[3].Width == VirtioPciRegisterAccessWidth.QWord
            && accessPlans[3].WidthBytes == sizeof(ulong)
            && accessPlans[3].BarRelativeOffset
                == 0x20UL + VirtioPciModernRegisterMap.CommonConfiguration.QueueDescOffset
            && accessPlans[7].RelativeOffset == VirtioPciModernRegisterMap.CommonConfiguration.QueueEnableOffset
            && accessPlans[7].Width == VirtioPciRegisterAccessWidth.Word
            && VirtioBlockQueueMmioSetupRegisterWindowPlanner.RequiresRegisterWindowBinding
            && VirtioBlockQueueMmioSetupRegisterWindowPlanner.WritesAccessPlansToCallerStorage
            && VirtioBlockQueueMmioSetupRegisterWindowPlanner.PlansCommonConfigurationAccesses
            && !VirtioBlockQueueMmioSetupRegisterWindowPlanner.MapsBars
            && !VirtioBlockQueueMmioSetupRegisterWindowPlanner.PerformsLiveMmio
            && !VirtioBlockQueueMmioSetupRegisterWindowPlanner.ContainsVolatileAccess
            && !VirtioBlockQueueMmioSetupRegisterWindowPlanner.ReadsDeviceRegisters
            && !VirtioBlockQueueMmioSetupRegisterWindowPlanner.WritesDeviceRegisters
            && !VirtioBlockQueueMmioSetupRegisterWindowPlanner.ConfiguresLiveQueues
            && !VirtioBlockQueueMmioSetupRegisterWindowPlanner.NotifiesLiveDevice
            && !VirtioBlockQueueMmioSetupRegisterWindowPlanner.PollsDevice
            && !VirtioBlockQueueMmioSetupRegisterWindowPlanner.HandlesInterrupts
            && !VirtioBlockQueueMmioSetupRegisterWindowPlanner.AllocatesDmaMemory
            && !VirtioBlockQueueMmioSetupRegisterWindowPlanner.PerformsDmaMapping
            && !VirtioBlockQueueMmioSetupRegisterWindowPlanner.PerformsBlockIo
            && !VirtioBlockQueueMmioSetupRegisterWindowPlanner.PerformsDiskRead
            && !VirtioBlockQueueMmioSetupRegisterWindowPlanner.PerformsDiskWrite
            && !RuntimeHelpers.IsReferenceOrContainsReferences
                <VirtioBlockQueueMmioSetupRegisterWindowPlanResult>();
    }

    private static bool TestRejectsFailedSetupPlan()
    {
        VirtioBlockQueueMmioSetupCommand[] commands =
            new VirtioBlockQueueMmioSetupCommand[VirtioBlockQueueMmioSetupPlanner.RequiredCommandCount];
        VirtioBlockQueueMmioSetupPlanResult failedSetup = VirtioBlockQueueMmioSetupPlanner.Create(
            default,
            QueueSize,
            CreateAddresses(),
            CreateMetadata(),
            commands);
        VirtioPciBarRegisterWindowAccessPlan[] accessPlans =
            new VirtioPciBarRegisterWindowAccessPlan[VirtioBlockQueueMmioSetupPlanner.RequiredCommandCount];

        VirtioBlockQueueMmioSetupRegisterWindowPlanResult result =
            VirtioBlockQueueMmioSetupRegisterWindowPlanner.PlanAccesses(
                failedSetup,
                commands,
                CreateRegisterWindows().Bindings.CommonConfiguration,
                accessPlans);

        return !failedSetup.Succeeded
            && !result.Succeeded
            && result.Status == VirtioBlockQueueMmioSetupRegisterWindowPlanStatus.SetupPlanFailed
            && result.SetupPlanStatus == VirtioBlockQueueMmioSetupPlanStatus.DescriptorNotReady
            && result.CommandIndex == -1
            && result.AccessPlansWritten == 0
            && result.AvailableAccessPlanCount == VirtioBlockQueueMmioSetupPlanner.RequiredCommandCount;
    }

    private static bool TestRejectsShortCommandSource()
    {
        VirtioBlockQueueMmioSetupPlanResult setup = CreateSetupPlan(
            out VirtioBlockQueueMmioSetupCommand[] commands);
        VirtioPciBarRegisterWindowAccessPlan[] accessPlans =
            new VirtioPciBarRegisterWindowAccessPlan[VirtioBlockQueueMmioSetupPlanner.RequiredCommandCount];

        VirtioBlockQueueMmioSetupRegisterWindowPlanResult result =
            VirtioBlockQueueMmioSetupRegisterWindowPlanner.PlanAccesses(
                setup,
                commands.AsSpan(0, commands.Length - 1),
                CreateRegisterWindows().Bindings.CommonConfiguration,
                accessPlans);

        return setup.Succeeded
            && !result.Succeeded
            && result.Status == VirtioBlockQueueMmioSetupRegisterWindowPlanStatus.CommandSourceTooSmall
            && result.SetupPlanStatus == VirtioBlockQueueMmioSetupPlanStatus.Success
            && result.RegisterWindowStatus == VirtioPciBarRegisterWindowPlanStatus.Success
            && result.CommandIndex == commands.Length - 1
            && result.AccessPlansWritten == 0
            && result.RequiredAccessPlanCount == VirtioBlockQueueMmioSetupPlanner.RequiredCommandCount
            && result.AvailableAccessPlanCount == VirtioBlockQueueMmioSetupPlanner.RequiredCommandCount;
    }

    private static bool TestReportsDestinationCapacity()
    {
        VirtioBlockQueueMmioSetupPlanResult setup = CreateSetupPlan(
            out VirtioBlockQueueMmioSetupCommand[] commands);
        VirtioPciBarRegisterWindowAccessPlan[] accessPlans =
            new VirtioPciBarRegisterWindowAccessPlan[VirtioBlockQueueMmioSetupPlanner.RequiredCommandCount - 1];

        VirtioBlockQueueMmioSetupRegisterWindowPlanResult result =
            VirtioBlockQueueMmioSetupRegisterWindowPlanner.PlanAccesses(
                setup,
                commands,
                CreateRegisterWindows().Bindings.CommonConfiguration,
                accessPlans);

        return setup.Succeeded
            && !result.Succeeded
            && result.Status
                == VirtioBlockQueueMmioSetupRegisterWindowPlanStatus.AccessPlanDestinationTooSmall
            && result.SetupPlanStatus == VirtioBlockQueueMmioSetupPlanStatus.Success
            && result.CommandIndex == accessPlans.Length
            && result.RequiredAccessPlanCount == VirtioBlockQueueMmioSetupPlanner.RequiredCommandCount
            && result.AvailableAccessPlanCount == VirtioBlockQueueMmioSetupPlanner.RequiredCommandCount - 1
            && result.AccessPlansWritten == 0;
    }

    private static bool TestPropagatesRegisterWindowFailure()
    {
        VirtioBlockQueueMmioSetupPlanResult setup = CreateSetupPlan(
            out VirtioBlockQueueMmioSetupCommand[] commands);
        VirtioPciBarRegisterWindowAccessPlan[] accessPlans =
            new VirtioPciBarRegisterWindowAccessPlan[VirtioBlockQueueMmioSetupPlanner.RequiredCommandCount];

        VirtioBlockQueueMmioSetupRegisterWindowPlanResult result =
            VirtioBlockQueueMmioSetupRegisterWindowPlanner.PlanAccesses(
                setup,
                commands,
                default,
                accessPlans);

        return setup.Succeeded
            && !result.Succeeded
            && result.Status == VirtioBlockQueueMmioSetupRegisterWindowPlanStatus.RegisterWindowPlanFailed
            && result.SetupPlanStatus == VirtioBlockQueueMmioSetupPlanStatus.Success
            && result.RegisterWindowStatus == VirtioPciBarRegisterWindowPlanStatus.MissingWindow
            && result.CommandKind == VirtioBlockQueueMmioSetupCommandKind.WriteQueueSelect
            && result.CommandIndex == 0
            && result.AccessPlansWritten == 0
            && result.RequiredAccessPlanCount == VirtioBlockQueueMmioSetupPlanner.RequiredCommandCount
            && result.AvailableAccessPlanCount == VirtioBlockQueueMmioSetupPlanner.RequiredCommandCount
            && result.QueueIndex == VirtioBlockQueueMmioSetupPlanner.QueueIndex
            && result.RelativeOffset == VirtioPciModernRegisterMap.CommonConfiguration.QueueSelectOffset
            && result.WidthBytes == VirtioPciModernRegisterMap.CommonConfiguration.QueueSelectWidth;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        VirtioBlockQueueMmioSetupRegisterWindowPlanResult result = default;

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioBlockQueueMmioSetupRegisterWindowPlanStatus.Success
            && result.AccessPlansWritten == 0
            && result.RequiredAccessPlanCount == 0
            && result.AvailableAccessPlanCount == 0;
    }

    private static bool TestRegisterWindowExecutionWritesPlannedSetupAccesses()
    {
        VirtioBlockQueueMmioSetupPlanResult setup = CreateSetupPlan(
            out VirtioBlockQueueMmioSetupCommand[] commands);
        VirtioPciBarRegisterWindowBindingResult registerWindows = CreateRegisterWindows();
        byte[] commonBytes = CreateCommonWindowBytes();
        VirtioBlockQueueMmioSetupQueueAddresses addresses = CreateAddresses();

        VirtioBlockQueueMmioSetupRegisterWindowExecutionResult result =
            VirtioBlockQueueMmioSetupRegisterWindowExecutor.Execute(
                setup,
                commands,
                registerWindows.Bindings.CommonConfiguration,
                commonBytes);

        return setup.Succeeded
            && registerWindows.Succeeded
            && result.Succeeded
            && result.Status == VirtioBlockQueueMmioSetupRegisterWindowExecutionStatus.Success
            && result.SetupPlanStatus == VirtioBlockQueueMmioSetupPlanStatus.Success
            && result.RegisterWindowStatus == VirtioPciBarRegisterWindowPlanStatus.Success
            && result.AccessExecutionStatus == VirtioPciBarRegisterWindowAccessExecutionStatus.Success
            && result.CommandsExecuted == VirtioBlockQueueMmioSetupPlanner.RequiredCommandCount
            && result.CommandKind == VirtioBlockQueueMmioSetupCommandKind.WriteQueueEnable
            && result.CommandIndex == VirtioBlockQueueMmioSetupPlanner.RequiredCommandCount - 1
            && result.QueueIndex == VirtioBlockQueueMmioSetupPlanner.QueueIndex
            && ReadUInt16(commonBytes, VirtioPciModernRegisterMap.CommonConfiguration.QueueSelectOffset)
                == VirtioBlockQueueMmioSetupPlanner.QueueIndex
            && ReadUInt16(commonBytes, VirtioPciModernRegisterMap.CommonConfiguration.QueueSizeOffset)
                == QueueSize
            && ReadUInt64(commonBytes, VirtioPciModernRegisterMap.CommonConfiguration.QueueDescOffset)
                == addresses.DescriptorTablePhysicalAddress
            && ReadUInt64(commonBytes, VirtioPciModernRegisterMap.CommonConfiguration.QueueDriverOffset)
                == addresses.DriverRingPhysicalAddress
            && ReadUInt64(commonBytes, VirtioPciModernRegisterMap.CommonConfiguration.QueueDeviceOffset)
                == addresses.DeviceRingPhysicalAddress
            && ReadUInt16(commonBytes, VirtioPciModernRegisterMap.CommonConfiguration.QueueNotifyOffOffset)
                == QueueNotifyOffset
            && ReadUInt16(commonBytes, VirtioPciModernRegisterMap.CommonConfiguration.QueueEnableOffset)
                == 1;
    }

    private static bool TestRegisterWindowExecutionRejectsFailedSetupPlan()
    {
        VirtioBlockQueueMmioSetupCommand[] commands =
            new VirtioBlockQueueMmioSetupCommand[VirtioBlockQueueMmioSetupPlanner.RequiredCommandCount];
        VirtioBlockQueueMmioSetupPlanResult failedSetup = VirtioBlockQueueMmioSetupPlanner.Create(
            default,
            QueueSize,
            CreateAddresses(),
            CreateMetadata(),
            commands);
        byte[] commonBytes = CreateCommonWindowBytes();

        VirtioBlockQueueMmioSetupRegisterWindowExecutionResult result =
            VirtioBlockQueueMmioSetupRegisterWindowExecutor.Execute(
                failedSetup,
                commands,
                CreateRegisterWindows().Bindings.CommonConfiguration,
                commonBytes);

        return !failedSetup.Succeeded
            && !result.Succeeded
            && result.Status == VirtioBlockQueueMmioSetupRegisterWindowExecutionStatus.SetupPlanFailed
            && result.SetupPlanStatus == VirtioBlockQueueMmioSetupPlanStatus.DescriptorNotReady
            && result.CommandIndex == -1
            && result.CommandsExecuted == 0
            && AllBytesEqual(commonBytes, 0xCC);
    }

    private static bool TestRegisterWindowExecutionRejectsShortCommandSource()
    {
        VirtioBlockQueueMmioSetupPlanResult setup = CreateSetupPlan(
            out VirtioBlockQueueMmioSetupCommand[] commands);
        byte[] commonBytes = CreateCommonWindowBytes();

        VirtioBlockQueueMmioSetupRegisterWindowExecutionResult result =
            VirtioBlockQueueMmioSetupRegisterWindowExecutor.Execute(
                setup,
                commands.AsSpan(0, commands.Length - 1),
                CreateRegisterWindows().Bindings.CommonConfiguration,
                commonBytes);

        return setup.Succeeded
            && !result.Succeeded
            && result.Status == VirtioBlockQueueMmioSetupRegisterWindowExecutionStatus.CommandSourceTooSmall
            && result.SetupPlanStatus == VirtioBlockQueueMmioSetupPlanStatus.Success
            && result.CommandIndex == commands.Length - 1
            && result.CommandsExecuted == commands.Length - 1
            && AllBytesEqual(commonBytes, 0xCC);
    }

    private static bool TestRegisterWindowExecutionRejectsReadMismatch()
    {
        VirtioBlockQueueMmioSetupPlanResult setup = CreateSetupPlan(
            out _);
        VirtioBlockQueueMmioSetupCommand[] commands =
            new VirtioBlockQueueMmioSetupCommand[VirtioBlockQueueMmioSetupPlanner.RequiredCommandCount];
        VirtioBlockQueueMmioSetupPlanResult commandSource = VirtioBlockQueueMmioSetupPlanner.Create(
            CreateBlockDescriptor(),
            QueueSize,
            CreateAddresses(),
            new VirtioBlockQueueMmioSetupQueueDeviceMetadata(
                DeviceMaximumQueueSize - 1,
                hasNotifyOffset: true,
                QueueNotifyOffset),
            commands);
        byte[] commonBytes = CreateCommonWindowBytes();

        VirtioBlockQueueMmioSetupRegisterWindowExecutionResult result =
            VirtioBlockQueueMmioSetupRegisterWindowExecutor.Execute(
                setup,
                commands,
                CreateRegisterWindows().Bindings.CommonConfiguration,
                commonBytes);

        return setup.Succeeded
            && commandSource.Succeeded
            && !result.Succeeded
            && result.Status == VirtioBlockQueueMmioSetupRegisterWindowExecutionStatus.RegisterReadMismatch
            && result.SetupPlanStatus == VirtioBlockQueueMmioSetupPlanStatus.Success
            && result.AccessExecutionStatus == VirtioPciBarRegisterWindowAccessExecutionStatus.Success
            && result.CommandKind == VirtioBlockQueueMmioSetupCommandKind.ReadQueueSize
            && result.CommandIndex == 1
            && result.CommandsExecuted == 1
            && result.QueueIndex == VirtioBlockQueueMmioSetupPlanner.QueueIndex
            && result.ExpectedValue == DeviceMaximumQueueSize - 1UL
            && result.ObservedValue == DeviceMaximumQueueSize;
    }

    private static bool TestRegisterWindowExecutionPropagatesAccessFailure()
    {
        VirtioBlockQueueMmioSetupPlanResult setup = CreateSetupPlan(
            out VirtioBlockQueueMmioSetupCommand[] commands);
        ulong failingLength = 0x20UL +
            VirtioPciModernRegisterMap.CommonConfiguration.QueueSelectOffset +
            1UL;
        byte[] commonBytes = CreateCommonWindowBytes((int)failingLength);

        VirtioBlockQueueMmioSetupRegisterWindowExecutionResult result =
            VirtioBlockQueueMmioSetupRegisterWindowExecutor.Execute(
                setup,
                commands,
                CreateRegisterWindows().Bindings.CommonConfiguration,
                commonBytes);

        return setup.Succeeded
            && !result.Succeeded
            && result.Status == VirtioBlockQueueMmioSetupRegisterWindowExecutionStatus.AccessExecutionFailed
            && result.SetupPlanStatus == VirtioBlockQueueMmioSetupPlanStatus.Success
            && result.RegisterWindowStatus == VirtioPciBarRegisterWindowPlanStatus.Success
            && result.AccessExecutionStatus ==
                VirtioPciBarRegisterWindowAccessExecutionStatus.WindowBufferTooSmall
            && result.CommandKind == VirtioBlockQueueMmioSetupCommandKind.WriteQueueSelect
            && result.CommandIndex == 0
            && result.CommandsExecuted == 0
            && AllBytesEqual(commonBytes, 0xCC);
    }

    private static bool TestRegisterWindowExecutionPreservesNoLiveMmioBoundary()
    {
        VirtioBlockQueueMmioSetupPlanResult setup = CreateSetupPlan(
            out VirtioBlockQueueMmioSetupCommand[] commands);
        byte[] commonBytes = CreateCommonWindowBytes();

        VirtioBlockQueueMmioSetupRegisterWindowExecutionResult result =
            VirtioBlockQueueMmioSetupRegisterWindowExecutor.Execute(
                setup,
                commands,
                CreateRegisterWindows().Bindings.CommonConfiguration,
                commonBytes);

        return result.Succeeded
            && VirtioBlockQueueMmioSetupRegisterWindowExecutor.RequiresRegisterWindowBinding
            && VirtioBlockQueueMmioSetupRegisterWindowExecutor.UsesCallerOwnedWindowBuffer
            && VirtioBlockQueueMmioSetupRegisterWindowExecutor.StagesExpectedDeviceReadValues
            && VirtioBlockQueueMmioSetupRegisterWindowExecutor.InvokesRegisterWindowAccessExecutor
            && !VirtioBlockQueueMmioSetupRegisterWindowExecutor.MapsBars
            && !VirtioBlockQueueMmioSetupRegisterWindowExecutor.PerformsLiveMmio
            && !VirtioBlockQueueMmioSetupRegisterWindowExecutor.ContainsVolatileAccess
            && !VirtioBlockQueueMmioSetupRegisterWindowExecutor.AccessesPhysicalAddresses
            && !VirtioBlockQueueMmioSetupRegisterWindowExecutor.ExecutesMemoryBarriers
            && !VirtioBlockQueueMmioSetupRegisterWindowExecutor.ConfiguresLiveQueues
            && !VirtioBlockQueueMmioSetupRegisterWindowExecutor.NotifiesLiveDevice
            && !VirtioBlockQueueMmioSetupRegisterWindowExecutor.PollsDevice
            && !VirtioBlockQueueMmioSetupRegisterWindowExecutor.HandlesInterrupts
            && !VirtioBlockQueueMmioSetupRegisterWindowExecutor.AllocatesDmaMemory
            && !VirtioBlockQueueMmioSetupRegisterWindowExecutor.PerformsDmaMapping
            && !VirtioBlockQueueMmioSetupRegisterWindowExecutor.PerformsBlockIo
            && !VirtioBlockQueueMmioSetupRegisterWindowExecutor.PerformsDiskRead
            && !VirtioBlockQueueMmioSetupRegisterWindowExecutor.PerformsDiskWrite
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                VirtioBlockQueueMmioSetupRegisterWindowExecutionResult>();
    }

    private static bool TestDefaultExecutionResultIsNotSuccess()
    {
        VirtioBlockQueueMmioSetupRegisterWindowExecutionResult result = default;

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioBlockQueueMmioSetupRegisterWindowExecutionStatus.Success
            && result.CommandsExecuted == 0
            && result.CommandIndex == 0
            && result.ExpectedValue == 0
            && result.ObservedValue == 0;
    }

    private static bool TestBarrierExecutionWritesQueueEnableAfterBarrier()
    {
        VirtioBlockQueueMmioSetupPlanResult setup = CreateSetupPlan(
            out VirtioBlockQueueMmioSetupCommand[] commands);
        VirtioPciBarRegisterWindowBindingResult registerWindows = CreateRegisterWindows();
        byte[] commonBytes = CreateCommonWindowBytes();
        VirtioBlockQueueMmioSetupQueueAddresses addresses = CreateAddresses();

        VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionResult result =
            VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutor.Execute(
                setup,
                commands,
                registerWindows.Bindings.CommonConfiguration,
                commonBytes,
                VirtioBlockQueueMmioSetupBarrierReadiness.Ready(44));

        return setup.Succeeded
            && registerWindows.Succeeded
            && result.Succeeded
            && result.Status == VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionStatus.Success
            && result.SetupPlanStatus == VirtioBlockQueueMmioSetupPlanStatus.Success
            && result.RegisterWindowStatus == VirtioPciBarRegisterWindowPlanStatus.Success
            && result.AccessExecutionStatus == VirtioPciBarRegisterWindowAccessExecutionStatus.Success
            && result.CommandsExecuted == VirtioBlockQueueMmioSetupPlanner.RequiredCommandCount
            && result.CommandKind == VirtioBlockQueueMmioSetupCommandKind.WriteQueueEnable
            && result.CommandIndex == VirtioBlockQueueMmioSetupPlanner.RequiredCommandCount - 1
            && result.QueueIndex == VirtioBlockQueueMmioSetupPlanner.QueueIndex
            && result.QueueEnableAttempted
            && result.BarrierSucceeded
            && result.BarrierSequence == 44
            && result.QueueSelectVisible
            && result.QueueSizeVisible
            && result.QueueAddressWritesVisible
            && result.OrderedBeforeQueueEnable
            && ReadUInt16(commonBytes, VirtioPciModernRegisterMap.CommonConfiguration.QueueSelectOffset)
                == VirtioBlockQueueMmioSetupPlanner.QueueIndex
            && ReadUInt16(commonBytes, VirtioPciModernRegisterMap.CommonConfiguration.QueueSizeOffset)
                == QueueSize
            && ReadUInt64(commonBytes, VirtioPciModernRegisterMap.CommonConfiguration.QueueDescOffset)
                == addresses.DescriptorTablePhysicalAddress
            && ReadUInt64(commonBytes, VirtioPciModernRegisterMap.CommonConfiguration.QueueDriverOffset)
                == addresses.DriverRingPhysicalAddress
            && ReadUInt64(commonBytes, VirtioPciModernRegisterMap.CommonConfiguration.QueueDeviceOffset)
                == addresses.DeviceRingPhysicalAddress
            && ReadUInt16(commonBytes, VirtioPciModernRegisterMap.CommonConfiguration.QueueNotifyOffOffset)
                == QueueNotifyOffset
            && ReadUInt16(commonBytes, VirtioPciModernRegisterMap.CommonConfiguration.QueueEnableOffset)
                == 1;
    }

    private static bool TestBarrierExecutionRejectsMissingBarrier()
    {
        VirtioBlockQueueMmioSetupPlanResult setup = CreateSetupPlan(
            out VirtioBlockQueueMmioSetupCommand[] commands);
        byte[] commonBytes = CreateCommonWindowBytes();
        VirtioBlockQueueMmioSetupQueueAddresses addresses = CreateAddresses();
        VirtioBlockQueueMmioSetupBarrierReadiness missingBarrier =
            new VirtioBlockQueueMmioSetupBarrierReadiness(
                queueSelectVisible: true,
                queueSizeVisible: true,
                queueAddressWritesVisible: true,
                orderedBeforeQueueEnable: false,
                barrierSequence: 45);

        VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionResult result =
            VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutor.Execute(
                setup,
                commands,
                CreateRegisterWindows().Bindings.CommonConfiguration,
                commonBytes,
                missingBarrier);

        return setup.Succeeded
            && !result.Succeeded
            && result.Status ==
                VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionStatus.MemoryBarrierNotReady
            && result.SetupPlanStatus == VirtioBlockQueueMmioSetupPlanStatus.Success
            && result.AccessExecutionStatus == VirtioPciBarRegisterWindowAccessExecutionStatus.Success
            && result.CommandKind == VirtioBlockQueueMmioSetupCommandKind.WriteQueueEnable
            && result.CommandIndex == VirtioBlockQueueMmioSetupPlanner.RequiredCommandCount - 1
            && result.CommandsExecuted == VirtioBlockQueueMmioSetupPlanner.RequiredCommandCount - 1
            && !result.QueueEnableAttempted
            && !result.BarrierSucceeded
            && result.QueueSelectVisible
            && result.QueueSizeVisible
            && result.QueueAddressWritesVisible
            && !result.OrderedBeforeQueueEnable
            && result.BarrierSequence == 45
            && ReadUInt16(commonBytes, VirtioPciModernRegisterMap.CommonConfiguration.QueueSelectOffset)
                == VirtioBlockQueueMmioSetupPlanner.QueueIndex
            && ReadUInt16(commonBytes, VirtioPciModernRegisterMap.CommonConfiguration.QueueSizeOffset)
                == QueueSize
            && ReadUInt64(commonBytes, VirtioPciModernRegisterMap.CommonConfiguration.QueueDescOffset)
                == addresses.DescriptorTablePhysicalAddress
            && ReadUInt64(commonBytes, VirtioPciModernRegisterMap.CommonConfiguration.QueueDriverOffset)
                == addresses.DriverRingPhysicalAddress
            && ReadUInt64(commonBytes, VirtioPciModernRegisterMap.CommonConfiguration.QueueDeviceOffset)
                == addresses.DeviceRingPhysicalAddress
            && ReadUInt16(commonBytes, VirtioPciModernRegisterMap.CommonConfiguration.QueueNotifyOffOffset)
                == QueueNotifyOffset
            && ReadUInt16(commonBytes, VirtioPciModernRegisterMap.CommonConfiguration.QueueEnableOffset)
                == 0xCCCC;
    }

    private static bool TestBarrierExecutionRejectsFailedSetupPlan()
    {
        VirtioBlockQueueMmioSetupCommand[] commands =
            new VirtioBlockQueueMmioSetupCommand[VirtioBlockQueueMmioSetupPlanner.RequiredCommandCount];
        VirtioBlockQueueMmioSetupPlanResult failedSetup = VirtioBlockQueueMmioSetupPlanner.Create(
            default,
            QueueSize,
            CreateAddresses(),
            CreateMetadata(),
            commands);
        byte[] commonBytes = CreateCommonWindowBytes();

        VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionResult result =
            VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutor.Execute(
                failedSetup,
                commands,
                CreateRegisterWindows().Bindings.CommonConfiguration,
                commonBytes,
                VirtioBlockQueueMmioSetupBarrierReadiness.Ready(46));

        return !failedSetup.Succeeded
            && !result.Succeeded
            && result.Status ==
                VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionStatus.SetupPlanFailed
            && result.SetupPlanStatus == VirtioBlockQueueMmioSetupPlanStatus.DescriptorNotReady
            && result.CommandIndex == -1
            && result.CommandsExecuted == 0
            && result.BarrierSucceeded
            && result.BarrierSequence == 46
            && !result.QueueEnableAttempted
            && AllBytesEqual(commonBytes, 0xCC);
    }

    private static bool TestBarrierExecutionRejectsShortCommandSource()
    {
        VirtioBlockQueueMmioSetupPlanResult setup = CreateSetupPlan(
            out VirtioBlockQueueMmioSetupCommand[] commands);
        byte[] commonBytes = CreateCommonWindowBytes();

        VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionResult result =
            VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutor.Execute(
                setup,
                commands.AsSpan(0, commands.Length - 1),
                CreateRegisterWindows().Bindings.CommonConfiguration,
                commonBytes,
                VirtioBlockQueueMmioSetupBarrierReadiness.Ready(47));

        return setup.Succeeded
            && !result.Succeeded
            && result.Status ==
                VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionStatus.CommandSourceTooSmall
            && result.SetupPlanStatus == VirtioBlockQueueMmioSetupPlanStatus.Success
            && result.CommandIndex == commands.Length - 1
            && result.CommandsExecuted == 0
            && result.BarrierSucceeded
            && result.BarrierSequence == 47
            && !result.QueueEnableAttempted
            && AllBytesEqual(commonBytes, 0xCC);
    }

    private static bool TestBarrierExecutionPropagatesAccessFailure()
    {
        VirtioBlockQueueMmioSetupPlanResult setup = CreateSetupPlan(
            out VirtioBlockQueueMmioSetupCommand[] commands);
        ulong failingLength = 0x20UL +
            VirtioPciModernRegisterMap.CommonConfiguration.QueueSelectOffset +
            1UL;
        byte[] commonBytes = CreateCommonWindowBytes((int)failingLength);

        VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionResult result =
            VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutor.Execute(
                setup,
                commands,
                CreateRegisterWindows().Bindings.CommonConfiguration,
                commonBytes,
                VirtioBlockQueueMmioSetupBarrierReadiness.Ready(48));

        return setup.Succeeded
            && !result.Succeeded
            && result.Status ==
                VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionStatus.AccessExecutionFailed
            && result.SetupPlanStatus == VirtioBlockQueueMmioSetupPlanStatus.Success
            && result.RegisterWindowStatus == VirtioPciBarRegisterWindowPlanStatus.Success
            && result.AccessExecutionStatus ==
                VirtioPciBarRegisterWindowAccessExecutionStatus.WindowBufferTooSmall
            && result.CommandKind == VirtioBlockQueueMmioSetupCommandKind.WriteQueueSelect
            && result.CommandIndex == 0
            && result.CommandsExecuted == 0
            && result.BarrierSucceeded
            && result.BarrierSequence == 48
            && !result.QueueEnableAttempted
            && AllBytesEqual(commonBytes, 0xCC);
    }

    private static bool TestBarrierExecutionPreservesNoLiveMmioBoundary()
    {
        VirtioBlockQueueMmioSetupPlanResult setup = CreateSetupPlan(
            out VirtioBlockQueueMmioSetupCommand[] commands);
        byte[] commonBytes = CreateCommonWindowBytes();

        VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionResult result =
            VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutor.Execute(
                setup,
                commands,
                CreateRegisterWindows().Bindings.CommonConfiguration,
                commonBytes,
                VirtioBlockQueueMmioSetupBarrierReadiness.Ready(49));

        return result.Succeeded
            && VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutor.RequiresRegisterWindowBinding
            && VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutor.UsesCallerOwnedWindowBuffer
            && VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutor.StagesExpectedDeviceReadValues
            && VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutor.InvokesRegisterWindowAccessExecutor
            && VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutor.RequiresBarrierReadiness
            && VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutor.GatesQueueEnableOnBarrier
            && !VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutor.MapsBars
            && !VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutor.PerformsLiveMmio
            && !VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutor.ContainsVolatileAccess
            && !VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutor.AccessesPhysicalAddresses
            && !VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutor.ExecutesMemoryBarriers
            && !VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutor.ConfiguresLiveQueues
            && !VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutor.NotifiesLiveDevice
            && !VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutor.PollsDevice
            && !VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutor.HandlesInterrupts
            && !VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutor.AllocatesDmaMemory
            && !VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutor.PerformsDmaMapping
            && !VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutor.PerformsBlockIo
            && !VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutor.PerformsDiskRead
            && !VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutor.PerformsDiskWrite
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                VirtioBlockQueueMmioSetupBarrierReadiness>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionResult>();
    }

    private static bool TestDefaultBarrierExecutionResultIsNotSuccess()
    {
        VirtioBlockQueueMmioSetupBarrierReadiness barrier = default;
        VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionResult result = default;

        return !barrier.Succeeded
            && barrier.BarrierSequence == 0
            && !result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionStatus.Success
            && result.CommandsExecuted == 0
            && result.CommandIndex == 0
            && result.ExpectedValue == 0
            && result.ObservedValue == 0
            && !result.QueueEnableAttempted
            && !result.BarrierSucceeded;
    }

    private static VirtioBlockQueueMmioSetupPlanResult CreateSetupPlan(
        out VirtioBlockQueueMmioSetupCommand[] commands)
    {
        commands =
            new VirtioBlockQueueMmioSetupCommand[VirtioBlockQueueMmioSetupPlanner.RequiredCommandCount];

        return VirtioBlockQueueMmioSetupPlanner.Create(
            CreateBlockDescriptor(),
            QueueSize,
            CreateAddresses(),
            CreateMetadata(),
            commands);
    }

    private static VirtioPciBarRegisterWindowBindingResult CreateRegisterWindows()
    {
        return VirtioPciBarRegisterWindowPlanner.BindRequiredApertures(
            CreateBindingPlan(),
            CreateRegisterWindowSources());
    }

    private static VirtioPciMappedBarWindow[] CreateRegisterWindowSources()
    {
        return
        [
            CreateRegisterWindow(0, 0x80000000UL, 0xFFFF800000000000UL),
            CreateRegisterWindow(1, 0x80001000UL, 0xFFFF800000001000UL),
            CreateRegisterWindow(2, 0x80002000UL, 0xFFFF800000002000UL),
            CreateRegisterWindow(3, 0x80003000UL, 0xFFFF800000003000UL),
            default,
            default
        ];
    }

    private static VirtioPciMappedBarWindow CreateRegisterWindow(
        byte barIndex,
        ulong physicalBaseAddress,
        ulong virtualBaseAddress)
    {
        return new VirtioPciMappedBarWindow(
            barIndex,
            physicalBaseAddress,
            virtualBaseAddress,
            length: 0x1000,
            isMapped: true,
            isMmio: true,
            isDeviceMemory: true,
            isUncached: true);
    }

    private static VirtioBlockQueueMmioSetupQueueAddresses CreateAddresses()
    {
        return new VirtioBlockQueueMmioSetupQueueAddresses(
            descriptorTablePhysicalAddress: 0x100000,
            driverRingPhysicalAddress: 0x101000,
            deviceRingPhysicalAddress: 0x102000);
    }

    private static VirtioBlockQueueMmioSetupQueueDeviceMetadata CreateMetadata()
    {
        return new VirtioBlockQueueMmioSetupQueueDeviceMetadata(
            DeviceMaximumQueueSize,
            hasNotifyOffset: true,
            QueueNotifyOffset);
    }

    private static VirtioDeviceDescriptor CreateBlockDescriptor()
    {
        VirtioDeviceDescriptorSource[] sources =
        [
            new VirtioDeviceDescriptorSource(
                sourceIndex: 50,
                CreateBindingPlan(),
                VirtioDeviceInitializationProfile.BlockOptionalFeatureMask,
                VirtioDeviceInitializationProfile.BlockRequiredQueueCount,
                featuresOkAccepted: true)
        ];
        VirtioDeviceConfigurationSource[] configurations =
        [
            new VirtioDeviceConfigurationSource(
                sourceIndex: 50,
                CreateBlockConfiguration(capacitySectors: 64))
        ];
        VirtioDeviceDescriptor[] destination = new VirtioDeviceDescriptor[1];

        VirtioDeviceDescriptorCatalogResult result = VirtioDeviceDescriptorCatalog.Build(
            sources,
            configurations,
            destination);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Failed to create deterministic virtio-blk descriptor.");
        }

        return destination[0];
    }

    private static VirtioPciBindingPlanResult CreateBindingPlan()
    {
        ushort deviceId = (ushort)(VirtioPciDeviceDiscovery.ModernDeviceIdStart
            + VirtioPciDeviceDiscovery.BlockDeviceId);

        return VirtioPciBindingPlan.Create(
            CreateScannedDevice(deviceId),
            CreatePciConfiguration(deviceId),
            CreateBarSizes());
    }

    private static PciEcamScannedDevice CreateScannedDevice(ushort deviceId)
    {
        return new PciEcamScannedDevice(
            segmentIndex: 2,
            new PciEcamSegment(
                basePhysicalAddress: 0xE0000000UL,
                segmentGroup: 1,
                startBus: 0,
                endBus: 0x7F),
            new PciScannedDevice(
                bus: 3,
                device: 5,
                function: 0,
                vendorId: VirtioPciDeviceDiscovery.VirtioVendorId,
                deviceId: deviceId,
                revisionId: 0,
                classCode: 0,
                subclass: 0,
                programmingInterface: 0,
                headerType: PciConfigurationHeader.Type0HeaderType,
                isMultifunction: false,
                hasCapabilitiesList: true,
                capabilityPointer: CommonOffset));
    }

    private static byte[] CreatePciConfiguration(ushort deviceId)
    {
        byte[] config = new byte[256];
        EndianPrimitives.WriteUInt16LittleEndian(
            config.AsSpan(0x00, sizeof(ushort)),
            VirtioPciDeviceDiscovery.VirtioVendorId);
        EndianPrimitives.WriteUInt16LittleEndian(config.AsSpan(0x02, sizeof(ushort)), deviceId);
        EndianPrimitives.WriteUInt16LittleEndian(
            config.AsSpan(0x06, sizeof(ushort)),
            PciConfigurationHeader.StatusCapabilitiesListBit);
        config[0x0E] = PciConfigurationHeader.Type0HeaderType;
        WriteUInt32(config, 0x10, 0x80000000);
        WriteUInt32(config, 0x14, 0x80001000);
        WriteUInt32(config, 0x18, 0x80002000);
        WriteUInt32(config, 0x1C, 0x80003000);
        config[0x34] = CommonOffset;

        WriteVirtioCapability(
            config,
            CommonOffset,
            NotifyOffset,
            VirtioPciCapabilityType.CommonConfiguration,
            bar: 0,
            offset: 0x20,
            VirtioPciDeviceDiscovery.CommonConfigurationMinimumLength,
            notifyOffsetMultiplier: 0,
            capabilityLength: VirtioPciCapabilityListParser.ModernVirtioCapabilityLength);
        WriteVirtioCapability(
            config,
            NotifyOffset,
            IsrOffset,
            VirtioPciCapabilityType.NotifyConfiguration,
            bar: 1,
            offset: 0x80,
            length: 0x100,
            notifyOffsetMultiplier: 4,
            capabilityLength: VirtioPciCapabilityListParser.ModernVirtioNotifyCapabilityLength);
        WriteVirtioCapability(
            config,
            IsrOffset,
            DeviceOffset,
            VirtioPciCapabilityType.IsrStatus,
            bar: 2,
            offset: 0,
            VirtioPciDeviceDiscovery.IsrStatusMinimumLength,
            notifyOffsetMultiplier: 0,
            capabilityLength: VirtioPciCapabilityListParser.ModernVirtioCapabilityLength);
        WriteVirtioCapability(
            config,
            DeviceOffset,
            nextOffset: 0,
            VirtioPciCapabilityType.DeviceConfiguration,
            bar: 3,
            offset: 0x40,
            VirtioPciDeviceDiscovery.BlockDeviceConfigurationMinimumLength,
            notifyOffsetMultiplier: 0,
            capabilityLength: VirtioPciCapabilityListParser.ModernVirtioCapabilityLength);

        return config;
    }

    private static ulong[] CreateBarSizes()
    {
        return
        [
            0x1000,
            0x1000,
            0x1000,
            0x1000,
            0,
            0
        ];
    }

    private static byte[] CreateBlockConfiguration(ulong capacitySectors)
    {
        byte[] config = new byte[VirtioBlockDeviceConfig.CapacityLength];
        EndianPrimitives.WriteUInt64LittleEndian(config, capacitySectors);
        return config;
    }

    private static void WriteVirtioCapability(
        Span<byte> config,
        byte capabilityOffset,
        byte nextOffset,
        VirtioPciCapabilityType type,
        byte bar,
        uint offset,
        uint length,
        uint notifyOffsetMultiplier,
        byte capabilityLength)
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
        for (int index = 0; index < bytes.Length; index++)
        {
            if (bytes[index] != expected)
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
}
