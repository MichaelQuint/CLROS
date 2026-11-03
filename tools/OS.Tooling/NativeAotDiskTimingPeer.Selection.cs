using System.Globalization;

namespace OS.Tooling;

public static partial class NativeAotDiskTimingPeer
{
    public const string SelectionMarker = "M10_018_SELECTED ";
    public const string SelectionEndMarker = "M10_018_SELECTED_END ";
    private const string SupervisorMarker = "M16_008_SUPERVISOR ";
    private const string InterruptTimingMarker = "M16_008_TIMING ";
    /// <summary>Validate the complete bounded selection stream and its adjacent
    /// supervisor/optional IRQ records. At least one IRQ join must witness the
    /// selected-to-arm discriminator. The end marker follows the last measured
    /// dispatch's physical return; later unmeasured turns add no samples.
    /// Malformed, missing or reordered records throw ToolFailure. Caller owns
    /// the copied serial text and separately proves original source/image/runner
    /// provenance. These checks grant no service cleanup or scheduling authority.</summary>

    public static IReadOnlyList<SelectionObservation> InspectSelections(string text)
    {
        Need(text.Length <= 32 * 1024 * 1024, "selection_extent");
        string[] lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        int[] ends = Enumerable.Range(0, lines.Length).Where(i => lines[i].StartsWith(SelectionEndMarker, StringComparison.Ordinal)).ToArray();
        Need(ends.Length == 1 && ends[0] < lines.Length - 1, "selection_end");
        int end = ends[0];
        ulong[] endValues = SelectionFields(lines[end], SelectionEndMarker, ["version", "lastSequence"]);
        Need(endValues[0] == 1 && endValues[1] is > 0 and <= 10_000_000, "selection_end_bounds");
        List<SelectionObservation> samples = [];
        ulong period = 0, previousTick = 0, previousEpoch = 0;
        bool irqJoined = false;
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (!line.StartsWith("M10_018_SELECTED", StringComparison.Ordinal))
                continue;
            if (i == end)
                continue;
            Need(i < end && line.StartsWith(SelectionMarker, StringComparison.Ordinal), "selection_order");
            ulong[] values = SelectionFields(line, SelectionMarker, ["version", "sequence", "identity", "selectionEpoch", "returnEpoch", "selected", "periodFs"]);
            ulong identity = values[2], selectedEpoch = values[3], returnedEpoch = values[4], tick = values[5];
            Need(values[0] == 1 && values[1] == (ulong)samples.Count + 1 && (uint)identity is > 0 and <= OS.Kernel.Compatibility.LinuxX64.LinuxThreadProgram.MaximumWorkers && identity >> 32 != 0 && selectedEpoch <= returnedEpoch && values[6] > 0, "selection_bounds");
            Need(samples.Count == 0 || selectedEpoch >= previousEpoch && tick >= previousTick && values[6] == period, "selection_monotonic");
            int supervisor = i - 1;
            if (supervisor >= 0 && lines[supervisor].StartsWith(InterruptTimingMarker, StringComparison.Ordinal))
            {
                ulong[] timing = SelectionFields(lines[supervisor], InterruptTimingMarker, ["identity", "epoch", "start", "arm", "capture", "ready", "periodFs"]);
                Need(timing[0] == identity && timing[1] == returnedEpoch && timing[6] == values[6] && timing[2] <= tick && tick <= timing[3] && timing[3] <= timing[4] && timing[4] <= timing[5], "selection_irq_join");
                irqJoined = true;
                supervisor--;
            }

            Need(supervisor >= 0 && lines[supervisor].StartsWith(SupervisorMarker, StringComparison.Ordinal), "selection_supervisor");
            ulong[] owner = SelectionFields(lines[supervisor], SupervisorMarker, ["identity", "action", "transfers", "ready", "active"]);
            Need(owner[0] == identity, "selection_identity");
            samples.Add(new(values[1], identity, selectedEpoch, returnedEpoch, tick, values[6]));
            period = values[6];
            previousTick = tick;
            previousEpoch = returnedEpoch;
        }

        Need((ulong)samples.Count == endValues[1] && irqJoined, "selection_complete");
        return samples.AsReadOnly();
    }

    private static ulong[] SelectionFields(string line, string marker, string[] names)
    {
        Need(line.Length <= 1024 && line.StartsWith(marker, StringComparison.Ordinal), "selection_record");
        string[] fields = line[marker.Length..].Split(' ');
        Need(fields.Length == names.Length, "selection_fields");
        ulong[] values = new ulong[names.Length];
        for (int i = 0; i < fields.Length; i++)
        {
            string prefix = names[i] + "=";
            Need(fields[i].StartsWith(prefix, StringComparison.Ordinal), "selection_field");
            string number = fields[i][prefix.Length..];
            Need(number.Length is > 0 and <= 20 && (number.Length == 1 || number[0] != '0') && number.All(c => c is >= '0' and <= '9') && ulong.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out values[i]), "selection_number");
        }

        return values;
    }
}
