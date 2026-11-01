using System.Runtime.InteropServices;
using OS.Kernel.Platform;

namespace OS.Boot;
// The bytes stay in caller-owned storage. This POD contains no seed or pointer.
public readonly struct FirmwareEntropySeedPublication
{
    internal FirmwareEntropySeedPublication(int retainedBytes) => RetainedBytes = retainedBytes;
    public int RetainedBytes { get; }
    public bool IsPublished => RetainedBytes is 32 or 64;
}
