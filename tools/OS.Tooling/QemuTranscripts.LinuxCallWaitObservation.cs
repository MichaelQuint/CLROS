using System.Globalization;
using System.Text.RegularExpressions;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Immutable observations keyed to the original record position and
    /// complete generation/ordinal. Source0 has no retained wait, source1 is futex
    /// source2 is pipe and explicitly admitted source3 is epoll; deadline is
    /// absolute monotonic nanoseconds or zero. Existing consumers reject source3.
    /// Values carry no scheduler, execution or prepared-program authority.</summary>

    public readonly record struct LinuxCallWaitObservation(int RecordIndex, ulong Identity, ulong Ordinal, uint Source, ulong Deadline, ulong Sequence);
}
