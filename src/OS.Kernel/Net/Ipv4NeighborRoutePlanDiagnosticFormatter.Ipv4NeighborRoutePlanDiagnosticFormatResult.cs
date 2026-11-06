namespace OS.Net;

using OS.Std.Text;

public readonly struct Ipv4NeighborRoutePlanDiagnosticFormatResult
{
    private const uint InitializedFlag = 1;
    public Ipv4NeighborRoutePlanDiagnosticFormatResult(Ipv4NeighborRoutePlanDiagnosticFormatStatus status, Ipv4NeighborRoutePlanStatus routeStatus, int bytesWritten, int requiredBytes, int availableBytes, uint flags = InitializedFlag)
    {
        Status = status;
        RouteStatus = routeStatus;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
        AvailableBytes = availableBytes;
        Flags = flags;
    }

    public Ipv4NeighborRoutePlanDiagnosticFormatStatus Status
    {
        get;
    }
    public Ipv4NeighborRoutePlanStatus RouteStatus
    {
        get;
    }
    public int BytesWritten
    {
        get;
    }
    public int RequiredBytes
    {
        get;
    }
    public int AvailableBytes
    {
        get;
    }
    public uint Flags
    {
        get;
    }
    public bool IsInitialized => (Flags & InitializedFlag) != 0;
    public bool Succeeded => IsInitialized && Status == Ipv4NeighborRoutePlanDiagnosticFormatStatus.Success && RequiredBytes > 0 && BytesWritten == RequiredBytes && RequiredBytes <= AvailableBytes;

    public static Ipv4NeighborRoutePlanDiagnosticFormatResult Fail(Ipv4NeighborRoutePlanDiagnosticFormatStatus status, Ipv4NeighborRoutePlanStatus routeStatus, int requiredBytes, int availableBytes)
    {
        return new Ipv4NeighborRoutePlanDiagnosticFormatResult(status, routeStatus, bytesWritten: 0, requiredBytes, availableBytes);
    }
}
