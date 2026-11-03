using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Observed policy socket lifetimes before the first normal accept.
    /// The caller still needs original app/native, copied sockaddr and full
    /// runtime/network proof. These counts alone qualify no guest execution.</summary>

    public readonly record struct SocketPolicyCallInspection(int BindDenials, int DuplicateListeners, int CapacitySockets, int ClosedSockets);
}
