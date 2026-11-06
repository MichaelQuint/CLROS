using System.Runtime.CompilerServices;
using OS.Kernel.Platform;

internal static partial class PlatformRandomGuardTests
{
    private sealed class RandomSourceDouble : IPlatformRandomSource
    {
        private readonly byte _firstByte;
        private readonly int _bytesToWrite;
        private readonly PlatformRandomSourceResult _result;
        private RandomSourceDouble(byte firstByte, int bytesToWrite, PlatformRandomSourceResult result)
        {
            _firstByte = firstByte;
            _bytesToWrite = bytesToWrite;
            _result = result;
        }

        public int FillCount
        {
            get; private set;
        }

        public static RandomSourceDouble Success(byte firstByte, int bytesToWrite)
        {
            return new RandomSourceDouble(firstByte, bytesToWrite, PlatformRandomSourceResult.Filled(bytesToWrite));
        }

        public static RandomSourceDouble FirmwareError(ulong firmwareStatus)
        {
            return new RandomSourceDouble(0, 0, PlatformRandomSourceResult.FirmwareError(firmwareStatus));
        }

        public PlatformRandomSourceResult FillBytes(Span<byte> destination)
        {
            FillCount++;
            int bytesToWrite = Math.Min(_bytesToWrite, destination.Length);
            for (int index = 0; index < bytesToWrite; index++)
            {
                destination[index] = (byte)(_firstByte + index);
            }

            return _result;
        }
    }
}
