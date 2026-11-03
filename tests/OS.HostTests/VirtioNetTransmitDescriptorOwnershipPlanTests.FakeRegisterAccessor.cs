using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Net;
using OS.Std.Binary;

internal static partial class VirtioNetTransmitDescriptorOwnershipPlanTests
{
    private sealed class FakeRegisterAccessor : IVirtioPciRegisterAccessor
    {
        private readonly byte[]? _notifyBytes;
        private ushort _selectedQueue;
        public FakeRegisterAccessor(byte[]? notifyBytes = null)
        {
            _notifyBytes = notifyBytes;
        }

        public VirtioPciRegisterWriteStatus NotifyFailureStatus { get; init; } = VirtioPciRegisterWriteStatus.Success;
        public VirtioPciRegisterWriteStatus LastStatus { get; private set; } = VirtioPciRegisterWriteStatus.NotReady;
        public int NotifyWrite16Count { get; private set; }
        public uint LastNotifyRelativeOffset { get; private set; }
        public ushort LastNotifyValue { get; private set; }

        public VirtioPciRegisterRead16Result Read16(VirtioPciBarAperture aperture, uint relativeOffset)
        {
            if (aperture.CapabilityType != VirtioPciCapabilityType.CommonConfiguration)
            {
                return new VirtioPciRegisterRead16Result(VirtioPciRegisterReadStatus.ReadFailed, value: 0);
            }

            if (relativeOffset == VirtioPciModernRegisterMap.CommonConfiguration.QueueSizeOffset)
            {
                ushort size = _selectedQueue == VirtioNetQueuePlan.ReceiveQueueIndex ? RxDeviceMaximumQueueSize : TxDeviceMaximumQueueSize;
                return new VirtioPciRegisterRead16Result(VirtioPciRegisterReadStatus.Success, size);
            }

            if (relativeOffset == VirtioPciModernRegisterMap.CommonConfiguration.QueueNotifyOffOffset)
            {
                ushort notifyOffset = _selectedQueue == VirtioNetQueuePlan.ReceiveQueueIndex ? RxNotifyOffset : TxNotifyOffset;
                return new VirtioPciRegisterRead16Result(VirtioPciRegisterReadStatus.Success, notifyOffset);
            }

            return new VirtioPciRegisterRead16Result(VirtioPciRegisterReadStatus.ReadFailed, value: 0);
        }

        public VirtioPciRegisterWriteStatus Write16(VirtioPciBarAperture aperture, uint relativeOffset, ushort value)
        {
            if (aperture.CapabilityType == VirtioPciCapabilityType.NotifyConfiguration)
            {
                NotifyWrite16Count++;
                LastNotifyRelativeOffset = relativeOffset;
                LastNotifyValue = value;
                if (NotifyFailureStatus != VirtioPciRegisterWriteStatus.Success)
                {
                    LastStatus = NotifyFailureStatus;
                    return LastStatus;
                }

                if (_notifyBytes is null || relativeOffset > int.MaxValue || (ulong)relativeOffset + sizeof(ushort) > (ulong)_notifyBytes.Length)
                {
                    LastStatus = VirtioPciRegisterWriteStatus.RangeOutsideAperture;
                    return LastStatus;
                }

                EndianPrimitives.WriteUInt16LittleEndian(_notifyBytes.AsSpan((int)relativeOffset, sizeof(ushort)), value);
                LastStatus = VirtioPciRegisterWriteStatus.Success;
                return LastStatus;
            }

            if (aperture.CapabilityType != VirtioPciCapabilityType.CommonConfiguration)
            {
                return VirtioPciRegisterWriteStatus.RangeOutsideAperture;
            }

            if (relativeOffset == VirtioPciModernRegisterMap.CommonConfiguration.QueueSelectOffset)
            {
                _selectedQueue = value;
            }

            return VirtioPciRegisterWriteStatus.Success;
        }

        public VirtioPciRegisterWriteStatus Write64(VirtioPciBarAperture aperture, uint relativeOffset, ulong value)
        {
            _ = relativeOffset;
            _ = value;
            return aperture.CapabilityType == VirtioPciCapabilityType.CommonConfiguration ? VirtioPciRegisterWriteStatus.Success : VirtioPciRegisterWriteStatus.UnsupportedWidth;
        }
    }
}
