using System.Globalization;
using System.Text;
using System.Text.Json;
using OS.Tools.QemuRunner;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Borrowed selection of the actual executable, firmware and
    /// loopback forward and actual owned invocation UTC bounds. Production must
    /// supply both bounds; values alone confer no process or guest authority.</summary>

    public readonly record struct ServiceRunnerContext(int ForwardPort, string Qemu, string Ovmf, DateTimeOffset StartedUtc, DateTimeOffset CompletedUtc);
}
