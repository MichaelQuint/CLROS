using System.Buffers.Binary;
using System.Globalization;
using System.Runtime.ExceptionServices;

namespace OS.Tools.QemuRunner;
public sealed partial class WebFrameControl
{
    public sealed record ServiceReport(Report Session, IReadOnlyList<int> ControlledPorts, IReadOnlyList<InjectedFrame> Injections);
}
