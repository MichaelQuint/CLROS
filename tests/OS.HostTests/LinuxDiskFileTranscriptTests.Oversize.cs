using System.Buffers.Binary;
using System.Globalization;
using System.Runtime.InteropServices;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Manifest;
using OS.Tooling;
using F = OS.Tooling.LinuxDiskFileProofFixture;

internal static partial class LinuxDiskFileTranscriptTests
{
    private sealed class Oversize : IReadOnlyList<string>
    {
        public int Count => 131073;

        public string this[int index] => throw new InvalidOperationException("must reject count first");
        public IEnumerator<string> GetEnumerator() => throw new InvalidOperationException("must reject count first");
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
