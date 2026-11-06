using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Copied HTTP/packet facts only. Reset reception and recovery do
    /// not prove descriptor release, process exit, source or native authority.</summary>

    public sealed record StaticFileHttpPacketInspection(int Connections, int TcpRecords, int RequestBytes, int ResponseBytes, IReadOnlyList<StaticFileHttpResponseJoin> Responses, uint AbortedGuestPort, int AbortResetRecord, int AbortWireBytes, bool ConcurrentWireOverlap, IReadOnlyList<StaticFileHttpFlow> Flows);
}
