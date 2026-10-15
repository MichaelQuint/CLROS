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
        => ParseLinuxCalls(tail, 0, out _, out _);

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
        _ = ParseLinuxCalls(tail, 1, out var waits, out _);
        return Array.AsReadOnly(waits);
    }

    /// <summary>Value-only post-dispatch observation before pending user-return
    /// delivery. Signal nesting/top-frame come from the actual retained owner;
    /// the return envelope is the gateway's admitted output, including sigreturn
    /// user edits. This is neither frame access nor prepared-program authority.</summary>
    public readonly record struct LinuxCallReturnObservation(int RecordIndex, ulong Identity, ulong Ordinal,
        ulong SignalDepth, ulong SignalFrame, ulong BlockedSignals, ulong Rip, ulong Rsp, ulong Flags, ulong Fs, ulong Gs, ulong ValueBits);

    /// <summary>Require complete current wait and restored-return observations
    /// on every call. Validate canonical values/owner frame shape and return a
    /// read-only owned snapshot. Complete protocol consumers must separately
    /// bind instructions, mappings, generations, requests and signal lifetimes.
    /// Invalid/missing observations throw ToolFailure; no input is retained.</summary>
    public static IReadOnlyList<LinuxCallReturnObservation> ReadLinuxCallReturns(string[] tail)
    {
        _ = ParseLinuxCalls(tail, 2, out _, out var returns);
        return Array.AsReadOnly(returns);
    }

    private static string[] ParseLinuxCalls(string[] tail, int required, out LinuxCallWaitObservation[] waits,
        out LinuxCallReturnObservation[] returns)
    {
        Need(tail is not null,"linux_call_ordinal_input");
        string[] result=tail!.ToArray(); Dictionary<ulong,ulong> counts=[];
        List<LinuxCallWaitObservation> observations=[];
        List<LinuxCallReturnObservation> returned=[];
        for(int i=0;i<result.Length;i++)
        {
            string line=result[i]; if(!line.StartsWith("M16_008_CALL ",StringComparison.Ordinal)) continue;
            const string n="(0|[1-9][0-9]*)";
            var match=Regex.Match(line,@"\AM16_008_CALL identity=([1-9][0-9]*) .* ordinal=([1-9][0-9]*)(?: waitSource=([012]) waitDeadline="+n+
                " waitSequence="+n+"(?: signalDepth=([0-4]) signalFrame="+n+" blockedSignals="+n+" returnRip="+n+" returnRsp="+n+
                " returnFlags="+n+" returnFs="+n+" returnGs="+n+" returnValueBits="+n+@")?)?\z",RegexOptions.CultureInvariant);
            Need(match.Success && ulong.TryParse(match.Groups[1].Value,NumberStyles.None,CultureInfo.InvariantCulture,out _) &&
                ulong.TryParse(match.Groups[2].Value,NumberStyles.None,CultureInfo.InvariantCulture,out _),"linux_call_ordinal_shape");
            int suffix=line.IndexOf(" ordinal=",StringComparison.Ordinal);
            Need(suffix==line.LastIndexOf(" ordinal=",StringComparison.Ordinal) &&
                !line[..suffix].Contains(" waitSource=",StringComparison.Ordinal) &&
                !line[..suffix].Contains(" waitDeadline=",StringComparison.Ordinal) &&
                !line[..suffix].Contains(" waitSequence=",StringComparison.Ordinal) &&
                !line[..suffix].Contains(" signalDepth=",StringComparison.Ordinal) &&
                !line[..suffix].Contains(" returnRip=",StringComparison.Ordinal),"linux_call_ordinal_shape");
            ulong identity=Number(match.Groups[1].Value), ordinal=Number(match.Groups[2].Value);
            counts.TryGetValue(identity,out ulong previous);
            Need(ordinal==previous+1 && ordinal<=4096,"linux_call_ordinal_continuity"); counts[identity]=ordinal;
            bool present=match.Groups[3].Success;
            Need(required==0 || present,"linux_call_wait_missing");
            if(present)
            {
                Need(ulong.TryParse(match.Groups[4].Value,NumberStyles.None,CultureInfo.InvariantCulture,out ulong deadline) &&
                    ulong.TryParse(match.Groups[5].Value,NumberStyles.None,CultureInfo.InvariantCulture,out _),"linux_call_wait_shape");
                uint source=(uint)(match.Groups[3].Value[0]-'0'); ulong sequence=Number(match.Groups[5].Value);
                Need(source==0 ? deadline==0 && sequence==0 : sequence!=0 && (source==1 || deadline==0),"linux_call_wait_state");
                Need(deadline<=long.MaxValue,"linux_call_wait_deadline");
                observations.Add(new(i,identity,ordinal,source,deadline,sequence));
            }
            Need(required<2 || match.Groups[6].Success,"linux_call_return_missing");
            if(match.Groups[6].Success)
            {
                ulong[] values=new ulong[9];
                for(int j=0;j<values.Length;j++)
                    Need(ulong.TryParse(match.Groups[6+j].Value,NumberStyles.None,CultureInfo.InvariantCulture,out values[j]),"linux_call_return_shape");
                Need(values[0]==0 ? values[1]==0 : values[1]>=4096 && values[1]%16==8,"linux_call_signal_frame");
                returned.Add(new(i,identity,ordinal,values[0],values[1],values[2],values[3],values[4],values[5],values[6],values[7],values[8]));
            }
            result[i]=line[..suffix];
        }
        waits=observations.ToArray();
        returns=returned.ToArray();
        return result;
    }
}
