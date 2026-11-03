using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Net;
using OS.Std.Binary;
using OS.Storage;

internal static partial class VirtioNetBarLiveAccessorBoundaryTests
{
    private sealed class FakeRegisterAccessor : IVirtioPciRegisterAccessor
    {
        public bool[] IsRead { get; } = new bool[4];
        public uint[] RelativeOffsets { get; } = new uint[4];
        public uint[] Widths { get; } = new uint[4];
        public ulong[] Values { get; } = new ulong[4];
        public VirtioPciCapabilityType[] CapabilityTypes { get; } = new VirtioPciCapabilityType[4];
        public byte[] BarIndices { get; } = new byte[4];
        public int OperationCount { get; private set; }
        public ushort Read16Value { get; init; }
        public VirtioPciRegisterReadStatus ReadFailureStatus { get; init; } = VirtioPciRegisterReadStatus.Success;
        public VirtioPciRegisterWriteStatus WriteFailureStatus { get; init; } = VirtioPciRegisterWriteStatus.Success;

        public VirtioPciRegisterRead16Result Read16(VirtioPciBarAperture aperture, uint relativeOffset)
        {
            int index = Record(aperture, relativeOffset, sizeof(ushort), value: 0, isRead: true);
            if (ReadFailureStatus != VirtioPciRegisterReadStatus.Success)
            {
                return new VirtioPciRegisterRead16Result(ReadFailureStatus, value: 0);
            }

            Values[index] = Read16Value;
            return new VirtioPciRegisterRead16Result(VirtioPciRegisterReadStatus.Success, Read16Value);
        }

        public VirtioPciRegisterWriteStatus Write16(VirtioPciBarAperture aperture, uint relativeOffset, ushort value)
        {
            Record(aperture, relativeOffset, sizeof(ushort), value, isRead: false);
            return WriteFailureStatus;
        }

        public VirtioPciRegisterWriteStatus Write64(VirtioPciBarAperture aperture, uint relativeOffset, ulong value)
        {
            Record(aperture, relativeOffset, sizeof(ulong), value, isRead: false);
            return WriteFailureStatus;
        }

        private int Record(VirtioPciBarAperture aperture, uint relativeOffset, uint widthBytes, ulong value, bool isRead)
        {
            int index = OperationCount++;
            IsRead[index] = isRead;
            RelativeOffsets[index] = relativeOffset;
            Widths[index] = widthBytes;
            Values[index] = value;
            CapabilityTypes[index] = aperture.CapabilityType;
            BarIndices[index] = aperture.BarIndex;
            return index;
        }
    }
}
