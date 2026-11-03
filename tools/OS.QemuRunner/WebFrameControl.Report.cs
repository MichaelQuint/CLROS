using System.Buffers.Binary;
using System.Globalization;
using System.Runtime.ExceptionServices;

namespace OS.Tools.QemuRunner;

public sealed partial class WebFrameControl
{
    public sealed record Report(int QemuOutPort, int QemuInPort, IReadOnlyList<string> Arguments, long ForwardedFrames, int VlanFrames, IReadOnlyList<DroppedFrame> DroppedFrames, bool GuestExitExpected, bool Stopped, bool Healthy);
}
