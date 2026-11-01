using OS.Boot;
using System.Runtime.CompilerServices;

namespace OS.Kernel.Runtime;
/// <summary>
/// Caller-attested readable, stable mapped bytes. This numeric window neither
/// establishes PE permissions nor probes whether memory is actually mapped.
/// </summary>

public readonly struct GCStaticReadableWindow
{
    public GCStaticReadableWindow(ulong start, ulong bytes)
    {
        Start = start;
        Bytes = bytes;
    }

    public ulong Start { get; }
    public ulong Bytes { get; }
}
