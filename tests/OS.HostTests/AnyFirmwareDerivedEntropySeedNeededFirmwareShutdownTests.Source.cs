using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Boot;
using OS.Kernel.Platform;
using CaptureSeed = OS.Boot.AnyFirmwareDerivedEntropySeedNeededFirmwareShutdown;

internal static partial class AnyFirmwareDerivedEntropySeedNeededFirmwareShutdownTests
{
    private sealed class Source : IPlatformRandomSource
    {
        internal Fixture? Owner;
        internal int Calls;
        internal int LastLength;
        internal int WriteLimit = int.MaxValue;
        internal int Reported = -1;
        internal int Failure;
        internal int Constant = -1;
        internal bool SawUnpublishedAndUntouched = true;
        public PlatformRandomSourceResult FillBytes(Span<byte> destination)
        {
            Calls++;
            LastLength = destination.Length;
            if (Owner is not null)
            {
                SawUnpublishedAndUntouched &= Owner.Publication.RetainedBytes == 0;
                for (int i = 0; i < Owner.Destination.Length; i++)
                    SawUnpublishedAndUntouched &= Owner.Destination[i] == PatternByte(i, 0xD3);
            }

            int count = Math.Min(WriteLimit, destination.Length);
            for (int i = 0; i < count; i++)
                destination[i] = Constant < 0 ? unchecked((byte)(0x31 + 7 * i)) : (byte)Constant;
            return Failure switch
            {
                1 => PlatformRandomSourceResult.Unavailable(),
                2 => PlatformRandomSourceResult.FirmwareError(0x8000000000000007UL),
                3 => default,
                _ => PlatformRandomSourceResult.Filled(Reported < 0 ? destination.Length : Reported)
            };
        }
    }
}
