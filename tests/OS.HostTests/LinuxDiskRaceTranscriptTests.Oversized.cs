using System.Globalization;
using System.Runtime.InteropServices;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Manifest;
using OS.Tooling;
using F = OS.Tooling.LinuxDiskRaceProofFixture;

internal static partial class LinuxDiskRaceTranscriptTests
{
    private sealed class Oversized : IReadOnlyList<string>
    {
        public int Count => 32769;

        public string this[int index] => throw new InvalidOperationException("must reject bounds first");
        public IEnumerator<string> GetEnumerator() => throw new InvalidOperationException("must reject bounds first");
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
