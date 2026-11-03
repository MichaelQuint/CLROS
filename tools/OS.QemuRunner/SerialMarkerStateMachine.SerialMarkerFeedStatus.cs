using System.Text;

namespace OS.Tools.QemuRunner;
public enum SerialMarkerFeedStatus
{
    Uninitialized = 0,
    Accepted = 1,
    AlreadyFinalized = 2,
    LineCapacityExceeded = 3,
    TotalCapacityExceeded = 4,
    InvalidByteRejected = 5
}
