namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Read-only derived transport observations. These values do not
    /// establish descriptor, syscall, scheduler, packet, package or guest authority.</summary>

    public sealed record SocketContainmentTransport(IReadOnlyList<SocketContainmentLifetime> Lifetimes, ulong TimeoutClock, ulong DropClock, ulong AvailableBefore, ulong AvailableAfter, ulong DeliveredAfterDrop);
}
