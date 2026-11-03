using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Boot;
using OS.Kernel.Platform;
using OS.Std.Binary;

internal static partial class PlatformUefiGoneSimulationTests
{
    private sealed class ConfigurationTableSourceDouble : IPlatformConfigurationTableSource
    {
        private readonly GCHandle _tableHandle;
        private readonly ulong _tableCount;
        public ConfigurationTableSourceDouble(byte[] tableBytes, ulong tableCount)
        {
            _tableHandle = GCHandle.Alloc(tableBytes, GCHandleType.Pinned);
            _tableCount = tableCount;
        }

        public int ScanCount { get; private set; }

        public PlatformConfigurationTableMetadata GetMetadata()
        {
            ScanCount++;
            return new PlatformConfigurationTableMetadata(unchecked((ulong)_tableHandle.AddrOfPinnedObject().ToInt64()), _tableCount);
        }
    }
}
