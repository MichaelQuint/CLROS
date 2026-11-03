using System.Collections;
using System.Globalization;
using OS.Tooling;
using Reads = OS.Tooling.NativeDiskReadObservations;

internal static partial class NativeDiskReadObservationTests
{
    private sealed class CountOnly(int count) : IReadOnlyList<string>
    {
        public int Count => count;

        public string this[int index] => throw new InvalidOperationException("count_rejection_must_precede_indexing");
        public IEnumerator<string> GetEnumerator() => throw new InvalidOperationException("count_rejection_must_precede_enumeration");
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
