using System.Runtime.CompilerServices;
using System.Reflection;
using OS.Drivers;
using OS.Net;
using OS.Std.Binary;
using OS.Storage;

internal static partial class VirtioNetQueueMmioSetupPlanTests
{
    private sealed class FakeRegisterAccessor : IVirtioPciRegisterAccessor
    {
        private ushort _selectedQueue;
        public bool[] IsRead { get; } = new bool[VirtioNetQueueMmioSetupPlanner.RequiredCommandCount];
        public uint[] Offsets { get; } = new uint[VirtioNetQueueMmioSetupPlanner.RequiredCommandCount];
        public uint[] Widths { get; } = new uint[VirtioNetQueueMmioSetupPlanner.RequiredCommandCount];
        public ulong[] Values { get; } = new ulong[VirtioNetQueueMmioSetupPlanner.RequiredCommandCount];
        public int OperationCount { get; private set; }
        public int FailureOperationIndex { get; init; } = -1;
        public VirtioPciRegisterReadStatus ReadFailureStatus { get; init; } = VirtioPciRegisterReadStatus.Success;
        public VirtioPciRegisterWriteStatus WriteFailureStatus { get; init; } = VirtioPciRegisterWriteStatus.Success;
        public int MismatchReadOperationIndex { get; init; } = -1;
        public ushort MismatchReadValue { get; init; }

        public VirtioPciRegisterRead16Result Read16(VirtioPciBarAperture aperture, uint relativeOffset)
        {
            _ = aperture;
            int index = OperationCount++;
            IsRead[index] = true;
            Offsets[index] = relativeOffset;
            Widths[index] = sizeof(ushort);
            if (index == FailureOperationIndex && ReadFailureStatus != VirtioPciRegisterReadStatus.Success)
            {
                return new VirtioPciRegisterRead16Result(ReadFailureStatus, value: 0);
            }

            ushort value = ReadValue(relativeOffset);
            if (index == MismatchReadOperationIndex)
            {
                value = MismatchReadValue;
            }

            Values[index] = value;
            return new VirtioPciRegisterRead16Result(VirtioPciRegisterReadStatus.Success, value);
        }

        public VirtioPciRegisterWriteStatus Write16(VirtioPciBarAperture aperture, uint relativeOffset, ushort value)
        {
            _ = aperture;
            int index = RecordWrite(relativeOffset, sizeof(ushort), value);
            if (relativeOffset == VirtioPciModernRegisterMap.CommonConfiguration.QueueSelectOffset)
            {
                _selectedQueue = value;
            }

            if (index == FailureOperationIndex && WriteFailureStatus != VirtioPciRegisterWriteStatus.Success)
            {
                return WriteFailureStatus;
            }

            return VirtioPciRegisterWriteStatus.Success;
        }

        public VirtioPciRegisterWriteStatus Write64(VirtioPciBarAperture aperture, uint relativeOffset, ulong value)
        {
            _ = aperture;
            int index = RecordWrite(relativeOffset, sizeof(ulong), value);
            if (index == FailureOperationIndex && WriteFailureStatus != VirtioPciRegisterWriteStatus.Success)
            {
                return WriteFailureStatus;
            }

            return VirtioPciRegisterWriteStatus.Success;
        }

        private int RecordWrite(uint relativeOffset, uint widthBytes, ulong value)
        {
            int index = OperationCount++;
            IsRead[index] = false;
            Offsets[index] = relativeOffset;
            Widths[index] = widthBytes;
            Values[index] = value;
            return index;
        }

        private ushort ReadValue(uint relativeOffset)
        {
            if (relativeOffset == VirtioPciModernRegisterMap.CommonConfiguration.QueueSizeOffset)
            {
                return _selectedQueue == VirtioNetQueuePlan.ReceiveQueueIndex ? RxDeviceMaximumQueueSize : TxDeviceMaximumQueueSize;
            }

            if (relativeOffset == VirtioPciModernRegisterMap.CommonConfiguration.QueueNotifyOffOffset)
            {
                return _selectedQueue == VirtioNetQueuePlan.ReceiveQueueIndex ? RxNotifyOffset : TxNotifyOffset;
            }

            return 0;
        }
    }
}
