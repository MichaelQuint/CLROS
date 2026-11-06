using System.Collections;
using OS.Tooling;
using Inventory = OS.Tooling.WebRootDiskInventory;
using Reads = OS.Tooling.NativeDiskReadObservations;
using ReadWindows = OS.Tooling.NativeDiskStaticReadWindows;

internal static partial class NativeDiskStaticReadWindowsTests
{
    private sealed class CountOnly<T>(int count) : IReadOnlyList<T>
    {
        public int Count => count;

        public T this[int index] => throw new InvalidOperationException("count_rejection_must_precede_indexing");
        public IEnumerator<T> GetEnumerator() => throw new InvalidOperationException("count_rejection_must_precede_enumeration");
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
