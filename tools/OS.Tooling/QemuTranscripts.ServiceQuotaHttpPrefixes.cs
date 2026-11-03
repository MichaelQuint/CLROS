using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Copied sequential framed messages and at most one unfinished
    /// final suffix. Responses selects direction, not connection/clock identity.
    /// Empty input contains no message and supplies no completion witness.</summary>

    public sealed record ServiceQuotaHttpPrefixes(int InputBytes, bool Responses, IReadOnlyList<ServiceQuotaHttpPrefix> Messages);
}
