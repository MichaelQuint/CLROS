using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Net;
using OS.Std.Binary;

internal static partial class VirtioNetQueueNotificationPlanTests
{
    private sealed class FakeRegisterAccessor : IVirtioPciRegisterAccessor
    {
        private readonly byte[] _notifyBytes;
        public FakeRegisterAccessor(byte[] notifyBytes)
        {
            _notifyBytes = notifyBytes;
        }

        public VirtioPciRegisterWriteStatus FailureStatus { get; init; } = VirtioPciRegisterWriteStatus.Success;
        public VirtioPciRegisterWriteStatus LastStatus { get; private set; } = VirtioPciRegisterWriteStatus.NotReady;
        public int Write16Count
        {
            get; private set;
        }
        public VirtioPciBarAperture LastAperture
        {
            get; private set;
        }
        public uint LastRelativeOffset
        {
            get; private set;
        }
        public ushort LastValue
        {
            get; private set;
        }

        public VirtioPciRegisterRead16Result Read16(VirtioPciBarAperture aperture, uint relativeOffset)
        {
            _ = aperture;
            _ = relativeOffset;
            return new VirtioPciRegisterRead16Result(VirtioPciRegisterReadStatus.ReadFailed, value: 0);
        }

        public VirtioPciRegisterWriteStatus Write16(VirtioPciBarAperture aperture, uint relativeOffset, ushort value)
        {
            Write16Count++;
            LastAperture = aperture;
            LastRelativeOffset = relativeOffset;
            LastValue = value;
            if (FailureStatus != VirtioPciRegisterWriteStatus.Success)
            {
                LastStatus = FailureStatus;
                return LastStatus;
            }

            ulong endOffset = (ulong)relativeOffset + sizeof(ushort);
            if (relativeOffset > int.MaxValue || endOffset > (ulong)_notifyBytes.Length)
            {
                LastStatus = VirtioPciRegisterWriteStatus.RangeOutsideAperture;
                return LastStatus;
            }

            EndianPrimitives.WriteUInt16LittleEndian(_notifyBytes.AsSpan((int)relativeOffset, sizeof(ushort)), value);
            LastStatus = VirtioPciRegisterWriteStatus.Success;
            return LastStatus;
        }

        public VirtioPciRegisterWriteStatus Write64(VirtioPciBarAperture aperture, uint relativeOffset, ulong value)
        {
            _ = aperture;
            _ = relativeOffset;
            _ = value;
            return VirtioPciRegisterWriteStatus.UnsupportedWidth;
        }
    }
}
