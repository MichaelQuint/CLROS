using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Tools.QemuRunner;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Caller-selected runner inputs and actual invocation window.
    /// These values alone grant no execution authority. Production supplies
    /// them around its owned process call; synthetic inspections must provide
    /// explicit borrowed expectations and cannot qualify a guest.</summary>

    public readonly record struct WebRunnerContext(int ForwardPort, string Qemu, string Ovmf, DateTimeOffset StartedUtc, DateTimeOffset CompletedUtc);
}
