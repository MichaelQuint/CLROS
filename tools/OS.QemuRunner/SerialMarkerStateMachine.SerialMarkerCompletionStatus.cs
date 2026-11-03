using System.Text;

namespace OS.Tools.QemuRunner;
public enum SerialMarkerCompletionStatus
{
    Uninitialized = 0,
    Passed = 1,
    CapacityExceeded = 2,
    InvalidByteSequence = 3,
    ForbiddenMarker = 4,
    ConflictingTerminalMarkers = 5,
    DuplicateTerminalMarker = 6,
    FailureTerminal = 7,
    Timeout = 8,
    PartialMarkerAtEndOfStream = 9,
    MissingRequiredMarker = 10,
    MissingSuccessTerminal = 11,
    OutOfOrderMarker = 12,
    DuplicateUniqueMarker = 13
}
