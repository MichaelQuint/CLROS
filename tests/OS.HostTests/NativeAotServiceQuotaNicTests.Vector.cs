using System.Text;
using OS.Tooling;

internal static partial class NativeAotServiceQuotaNicTests
{
    private sealed record Vector(List<byte[]> Frames, QemuTranscripts.WebParentReport Parent, QemuTranscripts.ServiceEnvelopeInspection Envelope);
}
