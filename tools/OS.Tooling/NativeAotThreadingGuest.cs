using System.Globalization;
using System.Text;

namespace OS.Tooling;

/// <summary>
/// Strict observation checks for the separate upstream threading consumers.
/// Captured stdout alone grants no NativeAOT producer, signal, suspension or
/// CLROS guest authority. Those require separately bound native/guest receipts.
/// </summary>
public static class NativeAotThreadingGuest
{
    /// <summary>
    /// Checks the focused workload's entire stdout: two ordered explicit full
    /// collections, independently advancing worker progress and joined root/TLS
    /// witnesses. Counts may include unrelated runtime collections. Rejects
    /// missing, extra, failed or inconsistent rows; does not execute input.
    /// </summary>
    public static void ValidateSuspensionOutput(string text)
    {
        Need(text.Length is > 0 and <= 4096, "output_size");
        text = text.Replace("\r\n", "\n", StringComparison.Ordinal);
        if (text.EndsWith('\n')) text = text[..^1];
        string[] lines = text.Split('\n');
        Need(lines.Length == 5, "output_inventory");
        Need(lines[0] == "TEST_START nativeaot.suspension" && lines[4] == "TEST_PASS nativeaot.suspension", "output_markers");
        long lastCollection = 0, lastProgress = 0;
        for (int epoch = 1; epoch <= 2; epoch++)
        {
            string[] row = lines[epoch].Split(' ');
            Need(row.Length == 7 && row[0] == "NATIVEAOT_SUSPENSION_GC", "collection_record");
            long ordinal = Number(row[1], "epoch", int.MaxValue);
            long before = Number(row[2], "before", int.MaxValue), after = Number(row[3], "after", int.MaxValue);
            long progressBefore = Number(row[4], "progressBefore", long.MaxValue);
            long progressReturned = Number(row[5], "progressReturned", long.MaxValue), progressAfter = Number(row[6], "progressAfter", long.MaxValue);
            Need(ordinal == epoch && before >= lastCollection && after > before &&
                progressBefore > 0 && progressBefore >= lastProgress && progressReturned >= progressBefore &&
                progressAfter > progressReturned, "collection_progress");
            lastCollection = after; lastProgress = progressAfter;
        }
        Need(lines[3] == "NATIVEAOT_SUSPENSION_JOINED workers=1 collections=2 roots=true interior=true tls=true", "joined_witness");
    }

    /// <summary>Reads one bounded strict UTF8 capture and validates it. The file
    /// is read-only; unknown encodings, invalid observations and I/O failures throw.</summary>
    public static void ValidateSuspensionFile(string path)
    {
        using FileStream stream = new(Path.GetFullPath(path), FileMode.Open, FileAccess.Read, FileShare.Read);
        Need(stream.Length is > 0 and <= 4096, "output_size");
        byte[] bytes = new byte[(int)stream.Length]; stream.ReadExactly(bytes);
        Need(stream.ReadByte() == -1, "output_changed");
        ValidateSuspensionOutput(new UTF8Encoding(false, true).GetString(bytes));
    }

    private static long Number(string field, string name, long maximum)
    {
        string prefix = name + "=";
        Need(field.StartsWith(prefix, StringComparison.Ordinal), "collection_field");
        string value = field[prefix.Length..];
        Need(long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long number) &&
            number <= maximum && number.ToString(CultureInfo.InvariantCulture) == value, "collection_number");
        return number;
    }

    private static void Need(bool condition, string reason)
    {
        if (!condition) throw new ToolFailure("nativeaot_suspension_" + reason);
    }
}
