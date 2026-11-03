using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    public sealed record DiskStaticHttpInspection(int Connections, int Responses, int TcpRecords, int RequestBytes, int ResponseBytes, IReadOnlyList<DiskStaticHttpDataJoin> Files, IReadOnlyList<StaticFileHttpFlow> Flows)
    {
        public string Scope { get; init; } = DiskStaticHttpContract.Scope;
        public bool GrantsOriginalNativeProcessOrBrowserAuthority => false;
    }
}
