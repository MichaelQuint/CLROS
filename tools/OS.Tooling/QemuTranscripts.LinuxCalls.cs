using System.Globalization;
using System.Text.RegularExpressions;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>
    /// Checks actual gateway call ordinals independently for each complete
    /// generation identity, then returns fresh records without that suffix for
    /// the existing strict ABI parsers. Missing, duplicate, reordered or exhausted
    /// counters fail. This helper grants no package, transcript or guest authority.
    /// Caller-owned records are never mutated; new generations start at one.
    /// </summary>
    public static string[] ValidateLinuxCallOrdinals(string[] tail)
    {
        Need(tail is not null,"linux_call_ordinal_input");
        string[] result=tail!.ToArray(); Dictionary<ulong,ulong> counts=[];
        for(int i=0;i<result.Length;i++)
        {
            string line=result[i]; if(!line.StartsWith("M16_008_CALL ",StringComparison.Ordinal)) continue;
            var match=Regex.Match(line,@"\AM16_008_CALL identity=([1-9][0-9]*) .* ordinal=([1-9][0-9]*)\z",RegexOptions.CultureInvariant);
            Need(match.Success && ulong.TryParse(match.Groups[1].Value,NumberStyles.None,CultureInfo.InvariantCulture,out _) &&
                ulong.TryParse(match.Groups[2].Value,NumberStyles.None,CultureInfo.InvariantCulture,out _),"linux_call_ordinal_shape");
            ulong identity=Number(match.Groups[1].Value), ordinal=Number(match.Groups[2].Value);
            counts.TryGetValue(identity,out ulong previous);
            Need(ordinal==previous+1 && ordinal<=4096,"linux_call_ordinal_continuity"); counts[identity]=ordinal;
            result[i]=line[..line.LastIndexOf(" ordinal=",StringComparison.Ordinal)];
        }
        return result;
    }
}
