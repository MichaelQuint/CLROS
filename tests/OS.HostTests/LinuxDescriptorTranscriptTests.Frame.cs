using System.Buffers.Binary;
using OS.Manifest;
using OS.Tooling;

internal static partial class LinuxDescriptorTranscriptTests
{
    private readonly record struct Frame(ulong Number, ulong A0, ulong A1, ulong A2, ulong A3, ulong A4, ulong A5, ulong Rip);
}
