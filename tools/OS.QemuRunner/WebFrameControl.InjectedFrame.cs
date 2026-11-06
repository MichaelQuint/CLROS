using System.Buffers.Binary;
using System.Globalization;
using System.Runtime.ExceptionServices;

namespace OS.Tools.QemuRunner;

public sealed partial class WebFrameControl
{
    /// <summary>Literal bounded service injection, ordered after original
    /// processed backend input. Forwarded=false records an intentional loss without a
    /// write; a successful write supplies no guest-delivery authority.</summary>

    public readonly record struct InjectedFrame(long AfterBackendOrdinal, string Tag, string Hex, bool Forwarded);
}
