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
    /// The current wait suffix is validated when present; historical ordinal-only
    /// rows remain usable by existing consumers. Runtime consumers must separately
    /// require current observations through ReadLinuxCallWaits.
    /// </summary>
    public static string[] ValidateLinuxCallOrdinals(string[] tail)
        => ParseLinuxCalls(tail, false, out _);

    /// <summary>Immutable observations keyed to the original record position and
    /// complete generation/ordinal. Source0 has no retained wait, source1 is futex
    /// and source2 is pipe; deadline is absolute monotonic nanoseconds or zero.
    /// Values carry no scheduler, execution or prepared-program authority.</summary>
    public readonly record struct LinuxCallWaitObservation(int RecordIndex, ulong Identity,
        ulong Ordinal, uint Source, ulong Deadline, ulong Sequence);

    /// <summary>Require the complete current wait suffix on every call, validate
    /// independent contiguous generation ordinals, and return a read-only owned
    /// snapshot. Missing/noncanonical/inconsistent witnesses throw ToolFailure.
    /// Other records are left to the complete protocol consumer; no input is
    /// mutated or retained. This value-only parser cannot qualify execution.</summary>
    public static IReadOnlyList<LinuxCallWaitObservation> ReadLinuxCallWaits(string[] tail)
    {
        _ = ParseLinuxCalls(tail, true, out var waits);
        return Array.AsReadOnly(waits);
    }

    private static string[] ParseLinuxCalls(string[] tail, bool requireWaitState, out LinuxCallWaitObservation[] waits)
    {
        Need(tail is not null,"linux_call_ordinal_input");
        string[] result=tail!.ToArray(); Dictionary<ulong,ulong> counts=[];
        List<LinuxCallWaitObservation> observations=[];
        for(int i=0;i<result.Length;i++)
        {
            string line=result[i]; if(!line.StartsWith("M16_008_CALL ",StringComparison.Ordinal)) continue;
            var match=Regex.Match(line,@"\AM16_008_CALL identity=([1-9][0-9]*) .* ordinal=([1-9][0-9]*)(?: waitSource=([012]) waitDeadline=(0|[1-9][0-9]*) waitSequence=(0|[1-9][0-9]*))?\z",RegexOptions.CultureInvariant);
            Need(match.Success && ulong.TryParse(match.Groups[1].Value,NumberStyles.None,CultureInfo.InvariantCulture,out _) &&
                ulong.TryParse(match.Groups[2].Value,NumberStyles.None,CultureInfo.InvariantCulture,out _),"linux_call_ordinal_shape");
            int suffix=line.IndexOf(" ordinal=",StringComparison.Ordinal);
            Need(suffix==line.LastIndexOf(" ordinal=",StringComparison.Ordinal) &&
                !line[..suffix].Contains(" waitSource=",StringComparison.Ordinal) &&
                !line[..suffix].Contains(" waitDeadline=",StringComparison.Ordinal) &&
                !line[..suffix].Contains(" waitSequence=",StringComparison.Ordinal),"linux_call_ordinal_shape");
            ulong identity=Number(match.Groups[1].Value), ordinal=Number(match.Groups[2].Value);
            counts.TryGetValue(identity,out ulong previous);
            Need(ordinal==previous+1 && ordinal<=4096,"linux_call_ordinal_continuity"); counts[identity]=ordinal;
            bool present=match.Groups[3].Success;
            Need(!requireWaitState || present,"linux_call_wait_missing");
            if(present)
            {
                Need(ulong.TryParse(match.Groups[4].Value,NumberStyles.None,CultureInfo.InvariantCulture,out ulong deadline) &&
                    ulong.TryParse(match.Groups[5].Value,NumberStyles.None,CultureInfo.InvariantCulture,out _),"linux_call_wait_shape");
                uint source=(uint)(match.Groups[3].Value[0]-'0'); ulong sequence=Number(match.Groups[5].Value);
                Need(source==0 ? deadline==0 && sequence==0 : sequence!=0 && (source==1 || deadline==0),"linux_call_wait_state");
                Need(deadline<=long.MaxValue,"linux_call_wait_deadline");
                observations.Add(new(i,identity,ordinal,source,deadline,sequence));
            }
            result[i]=line[..suffix];
        }
        waits=observations.ToArray();
        return result;
    }
}
