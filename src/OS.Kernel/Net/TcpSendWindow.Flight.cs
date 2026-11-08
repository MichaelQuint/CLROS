namespace OS.Net;

public static unsafe partial class TcpSendWindow
{
    public const uint MaximumOutstandingSegments = 8;

    /// <summary>Enable a fixed eight-segment ring before queueing/transmission.
    /// The serialized owner retains its existing1..32768-byte payload buffer and
    /// embedded segment lengths until ACK or closed scrub. Sequences derive from
    /// SND.UNA plus preceding retained lengths; no user/frame pointer is kept.
    /// Require configured timing and an empty, writable sender. Invalid admission
    /// changes nothing. Initial/restart cwnd is one effective MSS, with RFC5681
    /// slow start, byte-counted congestion avoidance and Reno fast recovery.
    /// Legacy callers remain single flight. This grants no NIC/guest acceptance.</summary>
    public static Status ConfigureBoundedFlight(Owner* owner)
    {
        var status = Validate(owner, out _);
        if (status != Status.Success)
            return status;
        if (owner->Timed == 0 || owner->FlightEnabled != 0 || owner->Count != 0 || owner->Sent != 0 || owner->WriteShutdown != 0)
            return Status.InvalidState;
        owner->CongestionWindow = FlightMss(owner);
        owner->SlowStartThreshold = 32768;
        owner->FlightEnabled = 1;
        return Status.Success;
    }

    private static uint FlightMss(Owner* owner) => owner->PeerMss < owner->Mtu - 40 ? owner->PeerMss : (uint)(owner->Mtu - 40);

    private static bool ValidateFlight(Owner* owner)
    {
        if (owner->FlightEnabled > 1 || owner->FlightHead >= MaximumOutstandingSegments || owner->FlightCount > MaximumOutstandingSegments ||
            owner->FlightDuplicates > MaximumOutstandingSegments || owner->FastPending > 1 || owner->FastRecovery > 1 || owner->FlightAmbiguous > 1 || owner->FlightHasSent > 1)
            return false;
        if (owner->FlightEnabled == 0)
        {
            if (owner->FlightHead != 0 || owner->FlightCount != 0 || owner->FlightDuplicates != 0 || owner->FastPending != 0 ||
                owner->FastRecovery != 0 || owner->FlightAmbiguous != 0 || owner->CongestionWindow != 0 ||
                owner->SlowStartThreshold != 0 || owner->CongestionCredit != 0 || owner->LastDataTransmit != 0 || owner->FlightHasSent != 0 || owner->FlightAmbiguousEnd != 0)
                return false;
        }
        else if (owner->Timed == 0 || owner->CongestionWindow is 0 or > 32768 || owner->SlowStartThreshold is 0 or > 32768 ||
            owner->CongestionCredit > 65535 || owner->FlightCount == 0 && (owner->FlightHead != 0 || owner->Sent != 0 || owner->FastPending != 0 || owner->FastRecovery != 0) ||
            owner->FastPending != 0 && owner->FastRecovery != 0 ||
            owner->FlightAmbiguous == 0 && owner->FlightAmbiguousEnd != 0 ||
            owner->FlightAmbiguous != 0 && (owner->Sent == 0 || unchecked(owner->FlightAmbiguousEnd - owner->Una) == 0 || unchecked(owner->FlightAmbiguousEnd - owner->Una) > (uint)owner->Sent))
            return false;
        uint total = 0, mss = FlightMss(owner);
        for (uint i = 0; i < MaximumOutstandingSegments; i++)
        {
            uint length = owner->FlightLengths[i];
            bool used = (i + MaximumOutstandingSegments - owner->FlightHead) % MaximumOutstandingSegments < owner->FlightCount;
            if (used ? length == 0 || length > mss : length != 0)
                return false;
            total += length;
        }
        return owner->FlightEnabled == 0 || total == owner->Sent;
    }

    private static void FlightAppend(Owner* owner, int length)
    {
        owner->FlightLengths[(owner->FlightHead + owner->FlightCount) % MaximumOutstandingSegments] = (ushort)length;
        owner->FlightCount++;
    }

    // Called only after complete ACK validation and original retained-byte
    // release. No callback intervenes; advance cannot exceed the audited ring.
    private static void FlightAcknowledged(Owner* owner, uint bytes)
    {
        uint left = bytes;
        while (left != 0)
        {
            uint length = owner->FlightLengths[owner->FlightHead];
            if (left < length)
            {
                owner->FlightLengths[owner->FlightHead] = (ushort)(length - left);
                break;
            }
            left -= length;
            owner->FlightLengths[owner->FlightHead] = 0;
            owner->FlightHead = (byte)((owner->FlightHead + 1) % MaximumOutstandingSegments);
            owner->FlightCount--;
        }
        if (owner->FlightCount == 0)
        {
            owner->FlightHead = 0;
        }
        // A continuous stream need not drain completely for Karn suppression
        // to end. Only bytes already in flight at the last retransmission are
        // ambiguous; later clean sends may provide a fresh RTT sample.
        if (owner->FlightAmbiguous != 0 && !After(owner->FlightAmbiguousEnd, unchecked(owner->Una + bytes)))
        {
            owner->FlightAmbiguous = 0;
            owner->FlightAmbiguousEnd = 0;
        }
        owner->FlightDuplicates = 0;
        owner->FastPending = 0;
        owner->RetryCount = 0;
        if (owner->FastRecovery != 0)
        {
            owner->CongestionWindow = owner->SlowStartThreshold;
            owner->FastRecovery = 0;
            owner->CongestionCredit = 0;
            return;
        }
        uint mss = FlightMss(owner);
        if (owner->CongestionWindow < owner->SlowStartThreshold)
            owner->CongestionWindow = FlightCap(owner->CongestionWindow + (bytes < mss ? bytes : mss));
        else
        {
            // RFC5681 byte counting avoids growth from ACK division. One
            // congestion window of newly ACKed bytes earns at most one MSS.
            owner->CongestionCredit += bytes;
            if (owner->CongestionCredit >= owner->CongestionWindow)
            {
                owner->CongestionCredit -= owner->CongestionWindow;
                owner->CongestionWindow = FlightCap(owner->CongestionWindow + mss);
            }
        }
    }

    private static void FlightDuplicate(Owner* owner)
    {
        if (owner->FlightDuplicates < MaximumOutstandingSegments)
            owner->FlightDuplicates++;
        if (owner->FastRecovery == 0)
        {
            if (owner->FlightDuplicates == 3)
                owner->FastPending = 1;
            return;
        }
        uint mss = FlightMss(owner);
        uint limit = owner->SlowStartThreshold + (owner->FlightCount > 3 ? owner->FlightCount : 3U) * mss;
        uint window = owner->CongestionWindow + mss;
        owner->CongestionWindow = FlightCap(window < limit ? window : limit);
    }

    private static void FlightRetransmitted(Owner* owner, bool fast)
    {
        uint mss = FlightMss(owner), threshold = (uint)owner->Sent / 2;
        if (threshold < 2 * mss)
            threshold = 2 * mss;
        if (fast || owner->RetryCount == 0)
            owner->SlowStartThreshold = threshold;
        owner->CongestionWindow = fast ? FlightCap(owner->SlowStartThreshold + 3 * mss) : mss;
        owner->CongestionCredit = 0;
        owner->FastPending = 0;
        owner->FastRecovery = fast ? (byte)1 : (byte)0;
        if (!fast)
            owner->FlightDuplicates = 0;
        owner->FlightAmbiguous = 1;
        owner->FlightAmbiguousEnd = owner->Next;
    }

    private static uint FlightCap(uint bytes) => bytes < 32768 ? bytes : 32768;

    private static void ClearFlight(Owner* owner)
    {
        for (uint i = 0; i < MaximumOutstandingSegments; i++)
            owner->FlightLengths[i] = 0;
        owner->CongestionWindow = owner->SlowStartThreshold = owner->CongestionCredit = 0;
        owner->FlightAmbiguousEnd = 0;
        owner->LastDataTransmit = 0;
        owner->FlightEnabled = owner->FlightHead = owner->FlightCount = owner->FlightDuplicates = 0;
        owner->FastPending = owner->FastRecovery = owner->FlightAmbiguous = 0;
        owner->FlightHasSent = 0;
    }
}
