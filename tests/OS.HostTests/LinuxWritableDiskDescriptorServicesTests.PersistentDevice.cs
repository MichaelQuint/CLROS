using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Pal;
using OS.Storage;
using C = OS.Storage.ContinuingFat32Mutation;
using B = OS.Storage.WritableBlockSession;
using W = OS.Kernel.Compatibility.LinuxX64.LinuxContinuingWritableDescriptorServices;
using F = OS.Kernel.Compatibility.LinuxX64.LinuxDescriptorServices;

internal static unsafe partial class LinuxWritableDiskDescriptorServicesTests
{
    private sealed class PersistentDevice(WritableBlockSessionTests.Hardware* hardware) : IReadOnlyBlockDevice
    {
        public BlockDeviceInfo Info => BlockDeviceInfo.Create(512, hardware->MediumSectors).Info;

        public BlockReadResult ReadBlocks(ulong first, uint count, Span<byte> target)
        {
            if (first >= hardware->MediumSectors || count > hardware->MediumSectors - first || target.Length != count * 512)
                return BlockReadResult.Fail(BlockDeviceStatus.InvalidBlockRange);
            new ReadOnlySpan<byte>(hardware->Persistent + first * 512, checked((int)count * 512)).CopyTo(target);
            return BlockReadResult.Success(count, count * 512UL);
        }
    }
}
