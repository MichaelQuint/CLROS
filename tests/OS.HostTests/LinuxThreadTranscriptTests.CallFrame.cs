using System.Buffers.Binary;
using System.Globalization;
using OS.Manifest;
using OS.Tooling;

internal static partial class LinuxThreadTranscriptTests
{
    private readonly record struct CallFrame(ulong Nr, ulong A0, ulong A1, ulong A2, ulong A3, ulong A4, ulong A5, ulong Rip, int FdOffset);
}
