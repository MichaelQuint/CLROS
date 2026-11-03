namespace OS.Tooling;

public static partial class QemuTranscripts
{
    // Smoke validation consumes real serial rows. Synthetic rows exercise admission
    // only; they prove no interrupt delivery, state preservation or release.
    public static string[] ValidateSchedulerTimerTail(string[] lines)
    {
        void Need(bool value, string reason)
        {
            if (!value)
                throw new ToolFailure("m09_005_transcript_" + reason);
        }
        int start = Array.IndexOf(lines, "TEST_START m09.005");
        Need(start >= 0 && Array.LastIndexOf(lines, "TEST_START m09.005") == start, "start");
        Need(!lines.Take(start).Any(line => line.StartsWith("M09_005_", StringComparison.Ordinal) ||
            line == "CLROS_M09_005_READY" || line.StartsWith("TEST_PASS m09.005", StringComparison.Ordinal)), "premature_record");
        int ticks = lines.Length - start - 7;
        Need(ticks is >= 4 and <= 9, "record_count");
        ulong[] Row(int index, string marker)
        {
            string[] fields = lines[index].Split(' ', StringSplitOptions.None);
            Need(fields.Length == 5 && fields[0] == marker, "record");
            ulong[] values = new ulong[4];
            for (int i = 0; i < values.Length; i++)
                Need(fields[i + 1].Length == 18 && fields[i + 1].StartsWith("0x", StringComparison.Ordinal) &&
                    ulong.TryParse(fields[i + 1].AsSpan(2), System.Globalization.NumberStyles.HexNumber,
                        System.Globalization.CultureInfo.InvariantCulture, out values[i]), "hex");
            return values;
        }
        ulong[] prepared = Row(start + 1, "M09_005_PREPARED");
        ulong pool = prepared[0];
        Need(pool >= 4096 && (pool & 4095) == 0 && pool <= 0x100000000UL - 32 * 4096 &&
            prepared[1] == 22 && prepared[2] == pool + 13 * 4096 && prepared[3] == pool + 0xc00, "preparation");
        ulong[] armed = Row(start + 2, "M09_005_ARMED");
        Need(armed[0] == (pool ^ 0x4d30393030354f57UL) && armed[1] == 2000000 && armed[2] == 0xe1 && armed[3] == 16, "arming");
        ulong previousPhase = 1, progress = 0, mask = 0;
        for (int i = 0; i < ticks; i++)
        {
            ulong[] tick = Row(start + 3 + i, "M09_005_TICK");
            ulong phase = tick[1];
            Need(tick[0] == (ulong)i + 1 && phase <= 3 && tick[3] > 0 && tick[3] >= progress, "tick");
            Need(i == ticks - 1 ? phase == 0 && previousPhase == 3 && tick[2] == 0 :
                phase != 0 && (i == 0 ? phase == 1 : phase == previousPhase || phase == previousPhase + 1) &&
                tick[2] == (ulong)i + 1, "critical_phase");
            mask |= 1UL << (int)phase;
            previousPhase = phase;
            progress = tick[3];
        }
        Need(mask == 15, "publication_boundaries");
        ulong[] wake = Row(start + 3 + ticks, "M09_005_WAKE");
        Need(wake[0] == (ulong)ticks && wake[1] == 0x100000001 && wake[2] == 1 && wake[0] >= wake[2] &&
            wake[3] >= pool + 0x202 && wake[3] < pool + 0x298, "wake");
        ulong[] restored = Row(start + 4 + ticks, "M09_005_RESTORED");
        Need(restored[0] == (ulong)ticks && restored[1] == 22 && restored[2] == 32 && restored[3] == 1, "restoration");
        Need(lines[start + 5 + ticks] == "TEST_PASS m09.005" && lines[start + 6 + ticks] == "CLROS_M09_005_READY", "terminal");
        return lines[..start];
    }
}
