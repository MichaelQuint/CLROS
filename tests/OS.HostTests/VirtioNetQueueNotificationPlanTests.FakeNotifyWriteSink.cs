using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Net;
using OS.Std.Binary;

internal static partial class VirtioNetQueueNotificationPlanTests
{
    private sealed class FakeNotifyWriteSink : IVirtioPciNotifyWriteSink
    {
        private readonly byte[] _notifyBytes;
        public FakeNotifyWriteSink(byte[] notifyBytes)
        {
            _notifyBytes = notifyBytes;
        }

        public VirtioPciNotifyWriteStatus FailureStatus { get; init; } = VirtioPciNotifyWriteStatus.Success;
        public int WriteCount
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

        public VirtioPciNotifyWriteStatus Write16(VirtioPciBarAperture aperture, uint relativeOffset, ushort value)
        {
            WriteCount++;
            LastAperture = aperture;
            LastRelativeOffset = relativeOffset;
            LastValue = value;
            if (FailureStatus != VirtioPciNotifyWriteStatus.Success)
            {
                return FailureStatus;
            }

            ulong endOffset = (ulong)relativeOffset + sizeof(ushort);
            if (relativeOffset > int.MaxValue || endOffset > (ulong)_notifyBytes.Length)
            {
                return VirtioPciNotifyWriteStatus.RangeOutsideAperture;
            }

            EndianPrimitives.WriteUInt16LittleEndian(_notifyBytes.AsSpan((int)relativeOffset, sizeof(ushort)), value);
            return VirtioPciNotifyWriteStatus.Success;
        }
    }
}
