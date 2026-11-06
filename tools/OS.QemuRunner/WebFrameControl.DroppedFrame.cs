using System.Buffers.Binary;
using System.Globalization;
using System.Runtime.ExceptionServices;

namespace OS.Tools.QemuRunner;

public sealed partial class WebFrameControl
{
    /// <summary>Exact withheld frame, ordered within the original backend input.
    /// Immutable hex retains the bytes omitted from the guest-side PCAP.</summary>

    public readonly record struct DroppedFrame(long Ordinal, string Hex);
}
