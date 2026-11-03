using System.Buffers.Binary;
using System.Globalization;
using System.Runtime.ExceptionServices;

namespace OS.Tools.QemuRunner;
public sealed partial class WebFrameControl
{
    private sealed record Injection(byte[] Bytes, string Tag, bool Forwarded, TaskCompletionSource Completion);
}
