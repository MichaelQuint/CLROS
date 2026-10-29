namespace OS.Tooling;

public static partial class NativeAotDiskTimingPeer
{
    public const string VerificationMarker = "M10_018_VERIFY ";
    public const string VerificationSchema = "clros-linux-native-verify-timing-v1";

    /// <summary>Inspect one frozen, nonnested verification count/time aggregate
    /// in the original phase. Elapsed includes boundary overhead and is not pure
    /// CPU time. Copied values grant no source, execution or performance claim;
    /// missing, duplicate, malformed or out-of-phase values throw ToolFailure.</summary>
    public static IReadOnlyDictionary<string, ulong> InspectVerificationMetrics(string text,
        IReadOnlyDictionary<string, ulong> dispatch)
    {
        Need(text.Length <= 32 * 1024 * 1024, "verification_extent");
        string[] lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n')
            .Where(line => line.StartsWith(VerificationMarker, StringComparison.Ordinal)).ToArray();
        Need(lines.Length == 1, "verification_count");
        string[] names = ["calls", "elapsedNs", "phaseNs", "phaseComplete"];
        ulong[] values = SelectionFields(lines[0], VerificationMarker, names);
        Need(dispatch.TryGetValue("phaseNs", out ulong phase) && phase > 0 &&
            values[0] > 0 && values[1] <= phase && values[2] == phase && values[3] == 1, "verification_phase");
        return new System.Collections.ObjectModel.ReadOnlyDictionary<string, ulong>(
            names.Select((name, i) => (name, values[i])).ToDictionary(x => x.name, x => x.Item2, StringComparer.Ordinal));
    }
}
