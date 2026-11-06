using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Text;
using OS.Storage;
using C = OS.Storage.ContinuingFat32Mutation;
using B = OS.Storage.WritableBlockSession;
using D = OS.Kernel.Compatibility.LinuxX64.LinuxContinuingWritableDescriptorServices;
using LogUpload = OS.Tooling.NativeAotLogUploadContract;
using P = OS.Storage.WritableBlockReadPort;

internal static unsafe partial class LinuxContinuingWritableDescriptorServicesTests
{
    private sealed class CopiedMedium(byte[] raw) : IReadOnlyBlockDevice
    {
        public BlockDeviceInfo Info => BlockDeviceInfo.Create(512, (ulong)raw.Length / 512).Info;

        public BlockReadResult ReadBlocks(ulong first, uint count, Span<byte> output)
        {
            ulong capacity = (ulong)raw.Length / 512;
            if (first >= capacity || count == 0 || count > capacity - first || (ulong)output.Length != (ulong)count * 512)
                return BlockReadResult.Fail(BlockDeviceStatus.InvalidBlockRange);
            raw.AsSpan(checked((int)first * 512), output.Length).CopyTo(output);
            return BlockReadResult.Success(count, (ulong)count * 512);
        }
    }
}
