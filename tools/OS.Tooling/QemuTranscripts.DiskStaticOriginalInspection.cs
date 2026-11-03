using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Tools.QemuRunner;
using Block = OS.Storage.ContinuingReadOnlyBlockService;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    internal sealed record DiskStaticOriginalInspection(DiskStaticReplayReport Replay, DiskStaticSectorValues Sectors, DiskStaticHttpInspection Http);
}
