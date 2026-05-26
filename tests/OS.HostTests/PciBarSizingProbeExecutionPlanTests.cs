using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Std.Binary;

internal static class PciBarSizingProbeExecutionPlanTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("drivers.pci.bar.sizeprobe.fakeexec captures 32bit readback", TestCaptures32BitReadback, ref failures);
        Run("drivers.pci.bar.sizeprobe.fakeexec captures 64bit readbacks", TestCaptures64BitReadbacks, ref failures);
        Run("drivers.pci.bar.sizeprobe.fakeexec rejects failed address plan", TestRejectsFailedAddressPlan, ref failures);
        Run("drivers.pci.bar.sizeprobe.fakeexec rejects short command source", TestRejectsShortCommandSource, ref failures);
        Run("drivers.pci.bar.sizeprobe.fakeexec propagates write failure", TestPropagatesWriteFailure, ref failures);
        Run("drivers.pci.bar.sizeprobe.fakeexec propagates read failure", TestPropagatesReadFailure, ref failures);
        Run("drivers.pci.bar.sizeprobe.fakeexec default result is not success", TestDefaultResultIsNotSuccess, ref failures);
        Run("drivers.pci.bar.sizeprobe.fakeexec preserves no-live-config-access boundary", TestPreservesNoLiveConfigAccessBoundary, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("PCI BAR sizing probe execution host tests failed: " + failures);
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

    private static bool TestCaptures32BitReadback()
    {
        PciBarSizingProbeAddressPlanResult addressPlan = CreateAddressPlan(
            bar0: 0xFE000008,
            bar2: 0,
            bar3: 0,
            out PciBarSizingProbeAddressCommand[] commands);
        Span<uint> readbacks = stackalloc uint[PciBarSizingProbeExecutor.RequiredReadbackCount];
        FakePciConfigDwordAccessor accessor = new FakePciConfigDwordAccessor();
        accessor.SetReadback(registerOffset: 0x10, value: 0xFFFFF008);

        PciBarSizingProbeExecutionResult result =
            PciBarSizingProbeExecutor.Execute(addressPlan, commands, accessor, readbacks);

        return result.Succeeded
            && result.IsInitialized
            && result.Status == PciBarSizingProbeExecutionStatus.Success
            && result.AddressPlanStatus == PciBarSizingProbeAddressPlanStatus.Success
            && result.ExecutedCommandCount == 3
            && result.ReadbackCount == 1
            && readbacks[0] == 0xFFFFF008
            && readbacks[1] == 0
            && accessor.WriteCount == 2
            && accessor.ReadCount == 1
            && accessor.OperationCount == 3
            && accessor.Operations[0].Kind == PciBarSizingProbeCommandKind.WriteAllOnes
            && accessor.Operations[0].RegisterOffset == 0x10
            && accessor.Operations[0].Value == uint.MaxValue
            && accessor.Operations[1].Kind == PciBarSizingProbeCommandKind.ReadSizingReadback
            && accessor.Operations[1].RegisterOffset == 0x10
            && accessor.Operations[1].Value == 0xFFFFF008
            && accessor.Operations[2].Kind == PciBarSizingProbeCommandKind.RestoreOriginal
            && accessor.Operations[2].RegisterOffset == 0x10
            && accessor.Operations[2].Value == 0xFE000008;
    }

    private static bool TestCaptures64BitReadbacks()
    {
        PciBarSizingProbeAddressPlanResult addressPlan = CreateAddressPlan(
            bar0: 0,
            bar2: 0x8000000C,
            bar3: 0x00000001,
            out PciBarSizingProbeAddressCommand[] commands);
        Span<uint> readbacks = stackalloc uint[PciBarSizingProbeExecutor.RequiredReadbackCount];
        FakePciConfigDwordAccessor accessor = new FakePciConfigDwordAccessor();
        accessor.SetReadback(registerOffset: 0x18, value: 0xFFFF000C);
        accessor.SetReadback(registerOffset: 0x1C, value: 0xFFFFFFFF);

        PciBarSizingProbeExecutionResult result =
            PciBarSizingProbeExecutor.Execute(addressPlan, commands, accessor, readbacks);

        return result.Succeeded
            && result.IsInitialized
            && result.ExecutedCommandCount == 6
            && result.ReadbackCount == 2
            && readbacks[2] == 0xFFFF000C
            && readbacks[3] == 0xFFFFFFFF
            && accessor.WriteCount == 4
            && accessor.ReadCount == 2
            && accessor.OperationCount == 6
            && IsOperation(accessor.Operations[0], PciBarSizingProbeCommandKind.WriteAllOnes, registerOffset: 0x18, value: uint.MaxValue)
            && IsOperation(accessor.Operations[1], PciBarSizingProbeCommandKind.WriteAllOnes, registerOffset: 0x1C, value: uint.MaxValue)
            && IsOperation(accessor.Operations[2], PciBarSizingProbeCommandKind.ReadSizingReadback, registerOffset: 0x18, value: 0xFFFF000C)
            && IsOperation(accessor.Operations[3], PciBarSizingProbeCommandKind.ReadSizingReadback, registerOffset: 0x1C, value: 0xFFFFFFFF)
            && IsOperation(accessor.Operations[4], PciBarSizingProbeCommandKind.RestoreOriginal, registerOffset: 0x1C, value: 0x00000001)
            && IsOperation(accessor.Operations[5], PciBarSizingProbeCommandKind.RestoreOriginal, registerOffset: 0x18, value: 0x8000000C);
    }

    private static bool TestRejectsFailedAddressPlan()
    {
        byte[] headerBytes = CreateBaseHeader();
        WriteUInt32(headerBytes, 0x10, 0xFE000000);
        PciBarDescriptorSet bars = DecodeBars(headerBytes);
        Span<PciBarSizingProbeAddressCommand> commands =
            stackalloc PciBarSizingProbeAddressCommand[PciBarSizingProbePlanner.MaximumCommandCount];
        PciBarSizingProbeAddressPlanResult addressPlan =
            PciBarSizingProbeAddressPlanner.Plan(
                new PciEcamSegment(0xE0000000, segmentGroup: 0, startBus: 8, endBus: 12),
                bus: 7,
                device: 0,
                function: 0,
                bars,
                commands);
        Span<uint> readbacks = stackalloc uint[PciBarSizingProbeExecutor.RequiredReadbackCount];
        FakePciConfigDwordAccessor accessor = new FakePciConfigDwordAccessor();

        PciBarSizingProbeExecutionResult result =
            PciBarSizingProbeExecutor.Execute(addressPlan, commands, accessor, readbacks);

        return !result.Succeeded
            && result.IsInitialized
            && result.Status == PciBarSizingProbeExecutionStatus.AddressPlanFailed
            && result.AddressPlanStatus == PciBarSizingProbeAddressPlanStatus.AddressPlanningFailed
            && result.RequiredCommandCount == 0
            && result.AvailableCommandCount == commands.Length
            && result.ExecutedCommandCount == 0
            && result.ReadbackCount == 0
            && result.FailingCommandIndex == PciBarSizingProbeExecutor.NoCommandIndex
            && accessor.OperationCount == 0;
    }

    private static bool TestRejectsShortCommandSource()
    {
        PciBarSizingProbeAddressPlanResult addressPlan = CreateAddressPlan(
            bar0: 0xFE000008,
            bar2: 0,
            bar3: 0,
            out PciBarSizingProbeAddressCommand[] commands);
        Span<uint> readbacks = stackalloc uint[PciBarSizingProbeExecutor.RequiredReadbackCount];
        FakePciConfigDwordAccessor accessor = new FakePciConfigDwordAccessor();

        PciBarSizingProbeExecutionResult result =
            PciBarSizingProbeExecutor.Execute(
                addressPlan,
                new ReadOnlySpan<PciBarSizingProbeAddressCommand>(commands, 0, 2),
                accessor,
                readbacks);

        return !result.Succeeded
            && result.IsInitialized
            && result.Status == PciBarSizingProbeExecutionStatus.CommandSourceTooShort
            && result.RequiredCommandCount == 3
            && result.AvailableCommandCount == 2
            && result.ExecutedCommandCount == 0
            && result.ReadbackCount == 0
            && accessor.OperationCount == 0;
    }

    private static bool TestPropagatesWriteFailure()
    {
        PciBarSizingProbeAddressPlanResult addressPlan = CreateAddressPlan(
            bar0: 0xFE000008,
            bar2: 0,
            bar3: 0,
            out PciBarSizingProbeAddressCommand[] commands);
        Span<uint> readbacks = stackalloc uint[PciBarSizingProbeExecutor.RequiredReadbackCount];
        FakePciConfigDwordAccessor accessor = new FakePciConfigDwordAccessor();
        accessor.FailWriteAtOperation = 2;

        PciBarSizingProbeExecutionResult result =
            PciBarSizingProbeExecutor.Execute(addressPlan, commands, accessor, readbacks);

        return !result.Succeeded
            && result.IsInitialized
            && result.Status == PciBarSizingProbeExecutionStatus.WriteFailed
            && result.AccessStatus == PciConfigDwordAccessStatus.AccessFailed
            && result.ExecutedCommandCount == 2
            && result.ReadbackCount == 1
            && result.FailingCommandIndex == 2
            && result.FailingBarIndex == 0
            && result.FailingRegisterOffset == 0x10
            && result.FailingCommandKind == PciBarSizingProbeCommandKind.RestoreOriginal
            && result.Value == 0xFE000008
            && readbacks[0] == 0
            && accessor.OperationCount == 3;
    }

    private static bool TestPropagatesReadFailure()
    {
        PciBarSizingProbeAddressPlanResult addressPlan = CreateAddressPlan(
            bar0: 0xFE000008,
            bar2: 0,
            bar3: 0,
            out PciBarSizingProbeAddressCommand[] commands);
        Span<uint> readbacks = stackalloc uint[PciBarSizingProbeExecutor.RequiredReadbackCount];
        FakePciConfigDwordAccessor accessor = new FakePciConfigDwordAccessor();
        accessor.FailReadAtOperation = 1;

        PciBarSizingProbeExecutionResult result =
            PciBarSizingProbeExecutor.Execute(addressPlan, commands, accessor, readbacks);

        return !result.Succeeded
            && result.IsInitialized
            && result.Status == PciBarSizingProbeExecutionStatus.ReadFailed
            && result.AccessStatus == PciConfigDwordAccessStatus.AccessFailed
            && result.ExecutedCommandCount == 1
            && result.ReadbackCount == 0
            && result.FailingCommandIndex == 1
            && result.FailingBarIndex == 0
            && result.FailingRegisterOffset == 0x10
            && result.FailingCommandKind == PciBarSizingProbeCommandKind.ReadSizingReadback
            && readbacks[0] == 0
            && accessor.OperationCount == 2;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        PciBarSizingProbeExecutionResult result = default;
        PciConfigDwordReadResult read = default;

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == PciBarSizingProbeExecutionStatus.Success
            && result.AddressPlanStatus == PciBarSizingProbeAddressPlanStatus.Success
            && !result.AddressPlan.IsInitialized
            && !result.AddressPlan.Succeeded
            && result.AccessStatus == PciConfigDwordAccessStatus.Success
            && result.RequiredCommandCount == 0
            && result.AvailableCommandCount == 0
            && result.ExecutedCommandCount == 0
            && result.RequiredReadbackCount == 0
            && result.AvailableReadbackCount == 0
            && result.ReadbackCount == 0
            && result.FailingCommandIndex == 0
            && result.FailingBarIndex == 0
            && result.FailingRegisterOffset == 0
            && result.FailingCommandKind == PciBarSizingProbeCommandKind.WriteAllOnes
            && result.FailingPhysicalAddress == 0
            && result.Value == 0
            && !read.IsInitialized
            && !read.Succeeded
            && read.Status == PciConfigDwordAccessStatus.Success
            && read.Value == 0;
    }

    private static bool TestPreservesNoLiveConfigAccessBoundary()
    {
        PciBarSizingProbeAddressPlanResult addressPlan = CreateAddressPlan(
            bar0: 0xFE000008,
            bar2: 0,
            bar3: 0,
            out PciBarSizingProbeAddressCommand[] commands);
        Span<uint> readbacks = stackalloc uint[PciBarSizingProbeExecutor.RequiredReadbackCount];
        FakePciConfigDwordAccessor accessor = new FakePciConfigDwordAccessor();
        accessor.SetReadback(registerOffset: 0x10, value: 0xFFFFF008);

        PciBarSizingProbeExecutionResult result =
            PciBarSizingProbeExecutor.Execute(addressPlan, commands, accessor, readbacks);

        return result.Succeeded
            && result.IsInitialized
            && PciBarSizingProbeExecutor.RequiresAddressPlan
            && PciBarSizingProbeExecutor.RequiresInjectedConfigAccessor
            && PciBarSizingProbeExecutor.UsesCallerOwnedReadbackBuffer
            && PciBarSizingProbeExecutor.InvokesInjectedConfigAccessor
            && PciBarSizingProbeExecutor.ExecutesRestoreCommandsAgainstInjectedAccessor
            && !PciBarSizingProbeExecutor.PerformsLiveConfigWrites
            && !PciBarSizingProbeExecutor.PerformsLiveConfigReads
            && !PciBarSizingProbeExecutor.RestoresHardwareBars
            && !PciBarSizingProbeExecutor.MapsEcam
            && !PciBarSizingProbeExecutor.MapsBars
            && !PciBarSizingProbeExecutor.PerformsMmio
            && !PciBarSizingProbeExecutor.ContainsVolatileAccess
            && !PciBarSizingProbeExecutor.AllocatesDmaMemory
            && !PciBarSizingProbeExecutor.PerformsDmaMapping
            && !PciBarSizingProbeExecutor.HandlesInterrupts
            && !PciBarSizingProbeExecutor.PerformsPacketIo
            && !PciBarSizingProbeExecutor.PerformsBlockIo
            && !RuntimeHelpers.IsReferenceOrContainsReferences<PciConfigDwordReadResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<PciBarSizingProbeExecutionResult>();
    }

    private static bool IsOperation(
        FakePciConfigDwordOperation operation,
        PciBarSizingProbeCommandKind kind,
        ushort registerOffset,
        uint value)
    {
        return operation.Kind == kind
            && operation.RegisterOffset == registerOffset
            && operation.Value == value;
    }

    private static PciBarSizingProbeAddressPlanResult CreateAddressPlan(
        uint bar0,
        uint bar2,
        uint bar3,
        out PciBarSizingProbeAddressCommand[] commands)
    {
        byte[] headerBytes = CreateBaseHeader();
        WriteUInt32(headerBytes, 0x10, bar0);
        WriteUInt32(headerBytes, 0x18, bar2);
        WriteUInt32(headerBytes, 0x1C, bar3);
        PciBarDescriptorSet bars = DecodeBars(headerBytes);
        commands = new PciBarSizingProbeAddressCommand[PciBarSizingProbePlanner.MaximumCommandCount];
        return PciBarSizingProbeAddressPlanner.Plan(
            new PciEcamSegment(0xE0000000, segmentGroup: 1, startBus: 0x20, endBus: 0x2F),
            bus: 0x24,
            device: 3,
            function: 5,
            bars,
            commands);
    }

    private static PciBarDescriptorSet DecodeBars(byte[] headerBytes)
    {
        PciConfigurationHeaderResult header = PciConfigurationHeader.ParseType0(headerBytes);
        if (!header.Succeeded)
        {
            throw new InvalidOperationException("test header parse failed: " + header.Status);
        }

        return PciBarDecoder.Decode(header.Header).Bars;
    }

    private static byte[] CreateBaseHeader()
    {
        byte[] headerBytes = new byte[PciConfigurationHeader.Type0HeaderLength];
        WriteUInt16(headerBytes, 0x00, VirtioPciDeviceDiscovery.VirtioVendorId);
        WriteUInt16(headerBytes, 0x02, 0x1041);
        headerBytes[0x0E] = PciConfigurationHeader.Type0HeaderType;
        return headerBytes;
    }

    private static void WriteUInt16(Span<byte> destination, int offset, ushort value)
    {
        EndianPrimitives.WriteUInt16LittleEndian(destination.Slice(offset, sizeof(ushort)), value);
    }

    private static void WriteUInt32(Span<byte> destination, int offset, uint value)
    {
        EndianPrimitives.WriteUInt32LittleEndian(destination.Slice(offset, sizeof(uint)), value);
    }

    private readonly struct FakePciConfigDwordOperation
    {
        public FakePciConfigDwordOperation(
            PciBarSizingProbeCommandKind kind,
            ushort registerOffset,
            ulong physicalAddress,
            uint value)
        {
            Kind = kind;
            RegisterOffset = registerOffset;
            PhysicalAddress = physicalAddress;
            Value = value;
        }

        public PciBarSizingProbeCommandKind Kind { get; }

        public ushort RegisterOffset { get; }

        public ulong PhysicalAddress { get; }

        public uint Value { get; }
    }

    private sealed class FakePciConfigDwordAccessor : IPciConfigDwordAccessor
    {
        private readonly uint[] _readbacks = new uint[PciBarSizingProbeExecutor.RequiredReadbackCount];

        public FakePciConfigDwordOperation[] Operations { get; } =
            new FakePciConfigDwordOperation[PciBarSizingProbePlanner.MaximumCommandCount];

        public int OperationCount { get; private set; }

        public int WriteCount { get; private set; }

        public int ReadCount { get; private set; }

        public int FailWriteAtOperation { get; set; } = -1;

        public int FailReadAtOperation { get; set; } = -1;

        public void SetReadback(
            ushort registerOffset,
            uint value)
        {
            byte barIndex = (byte)((registerOffset - PciBarSizingProbePlanner.FirstBarRegisterOffset) /
                PciBarSizingProbePlanner.BarRegisterStride);
            _readbacks[barIndex] = value;
        }

        public PciConfigDwordAccessStatus Write32(
            PciEcamConfigurationAddressResult address,
            uint value)
        {
            WriteCount++;
            PciBarSizingProbeCommandKind kind = value == uint.MaxValue
                ? PciBarSizingProbeCommandKind.WriteAllOnes
                : PciBarSizingProbeCommandKind.RestoreOriginal;
            Operations[OperationCount] = new FakePciConfigDwordOperation(
                kind,
                address.RegisterOffset,
                address.PhysicalAddress,
                value);
            OperationCount++;
            return OperationCount - 1 == FailWriteAtOperation
                ? PciConfigDwordAccessStatus.AccessFailed
                : PciConfigDwordAccessStatus.Success;
        }

        public PciConfigDwordReadResult Read32(
            PciEcamConfigurationAddressResult address)
        {
            ReadCount++;
            byte barIndex = (byte)((address.RegisterOffset - PciBarSizingProbePlanner.FirstBarRegisterOffset) /
                PciBarSizingProbePlanner.BarRegisterStride);
            uint value = _readbacks[barIndex];
            Operations[OperationCount] = new FakePciConfigDwordOperation(
                PciBarSizingProbeCommandKind.ReadSizingReadback,
                address.RegisterOffset,
                address.PhysicalAddress,
                value);
            OperationCount++;
            return OperationCount - 1 == FailReadAtOperation
                ? new PciConfigDwordReadResult(PciConfigDwordAccessStatus.AccessFailed, value: 0)
                : new PciConfigDwordReadResult(PciConfigDwordAccessStatus.Success, value);
        }
    }
}
