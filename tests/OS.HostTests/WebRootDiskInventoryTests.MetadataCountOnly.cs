using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using OS.Tooling;
using Fds = OS.Kernel.Compatibility.LinuxX64.LinuxDescriptorServices;
using Fs = OS.Storage.ReadOnlyWebRootFileSystem;
using I = OS.Tooling.WebRootDiskInventory;

internal static partial class WebRootDiskInventoryTests
{
    private sealed class MetadataCountOnly(int count) : IReadOnlyList<ulong>
    {
        public int Count => count;

        public ulong this[int index] => throw new InvalidOperationException("metadata_bound_must_precede_indexing");
        public IEnumerator<ulong> GetEnumerator() => throw new InvalidOperationException("metadata_bound_must_precede_enumeration");
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
