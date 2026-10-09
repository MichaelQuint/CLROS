using System.Globalization;
using System.Text;

// Independent guest-shaped vectors stay in their owning fixtures. This helper
// shares only framing, mutation execution and evidence accounting.
internal sealed class EfiTranscriptControls
{
    private readonly string path;
    private readonly Action<string> validate;
    private int count;
    public EfiTranscriptControls(string directory, Action<string> validate)
    {
        Directory.CreateDirectory(directory);
        path = Path.Combine(directory, "case.serial");
        this.validate = validate;
    }
    public static string Text(IEnumerable<string> lines) => string.Join("\r\n", lines) + "\r\n";
    public static ulong[] Fields(string line) => line.Split(' ').Skip(2).Select(v => ulong.Parse(v.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToArray();
    public static string Row(string prefix, int stage, params ulong[] values) => prefix + " " + stage + " " + string.Join(' ', values.Select(v => "0x" + v.ToString("X16", CultureInfo.InvariantCulture)));
    public void Check(string text, bool accept, string reason)
    {
        File.WriteAllText(path, text, new UTF8Encoding(false));
        bool accepted = true;
        try { validate(path); }
        catch (OS.Tooling.ToolFailure) when (!accept) { accepted = false; }
        if (accepted != accept) throw new InvalidOperationException("transcript control " + reason + " expected=" + accept);
        count++;
    }
    public void Check(string[] lines, bool accept, string reason) => Check(Text(lines), accept, reason);
    public void Structure(string[] lines, int first, int length)
    {
        foreach (int i in Enumerable.Range(first, length))
        {
            Check(lines.Where((_, n) => n != i).ToArray(), false, "missing row " + i);
            Check(lines.Take(i + 1).Concat(lines.Skip(i)).ToArray(), false, "duplicate row " + i);
            string[] bad = [.. lines]; bad[i] += " "; Check(bad, false, "trailing space row " + i);
        }
    }
    public void Complete(string name) => Console.WriteLine("TRANSCRIPT_CONTROLS " + name + " cases=" + count);

    internal static void RunnerExitCodes()
    {
        string[] Records(string exit) => ["SERIAL_MARKER_DRAIN milliseconds=750", "QEMU_PROCESS_RESULT disposition=runner-terminated exitCode=" + exit];
        foreach (string exit in new[] { "0", "-1", "2147483647", "-2147483648" })
            OS.Tooling.QemuTranscripts.AssertM04ManagedHeapRunnerResult(Records(exit));
        foreach (string exit in new[] { "none", "2147483648", "-2147483649", "-0", "01", "+1" })
        {
            bool accepted = true;
            try { OS.Tooling.QemuTranscripts.AssertM04ManagedHeapRunnerResult(Records(exit)); }
            catch (OS.Tooling.ToolFailure) { accepted = false; }
            HostCheck.True(!accepted, "invalid runner exit: " + exit);
        }
    }
}
