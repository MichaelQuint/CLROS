using System.Globalization;
using System.Text.RegularExpressions;
using OS.Tools.QemuRunner;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Require six ordered accepts, or eight with explicit transport
    /// controls, and the fifth descriptor's native
    /// scatter send and actual short return, with at least its complete entity's
    /// byte count before the later health accept. Borrowed rows are never executed or
    /// retained. Unsupported or inconsistent observations throw. This synthetic
    /// seam does not establish source, memory, packet, process or guest identity.</summary>

    public static WebVectorCallInspection InspectNativeAotWebVectorCallsForTests(string[] rows, bool transportControls = false) => WebVectorCalls(rows, transportControls);
    private static WebVectorCallInspection WebVectorCalls(string[] rows, bool transportControls = false)
    {
        int expectedAccepts = transportControls ? 8 : 6;
        var returns = ReadLinuxCallReturns(rows, allowEpoll: true);
        var messages = NativeAotSocketMessages.Read(rows, returns.ToDictionary(call => call.RecordIndex)).ToDictionary(message => message.CallRecordIndex);
        Dictionary<uint, int> accepted = [];
        int count = 0, sends = 0, scatter = 0, shorts = 0, blocked = 0;
        uint generation = 0;
        ulong bytes = 0;
        void Progress() => Need(scatter > 0 && shorts > 0 && bytes >= WebVectorResponse.Length, "web_vector_native_progress");
        foreach (var call in returns.OrderBy(call => call.RecordIndex))
        {
            var match = Regex.Match(rows[call.RecordIndex], @"^M16_008_CALL identity=\d+ nr=(\d+) a0=(\d+) ", RegexOptions.CultureInvariant);
            Need(match.Success, "web_vector_call_shape");
            ulong number = ulong.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture), fd = ulong.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            long result = unchecked((long)call.ValueBits);
            if (number is 43 or 288 && result >= 0)
            {
                Need(result <= uint.MaxValue && count < expectedAccepts && !accepted.ContainsKey((uint)result), "web_vector_accept");
                if (count == 5)
                    Progress();
                accepted.Add((uint)result, count++);
                continue;
            }

            if (fd > uint.MaxValue || !accepted.TryGetValue((uint)fd, out int index))
                continue;
            if (number == 3 && result == 0)
            {
                accepted.Remove((uint)fd);
                continue;
            }

            if (index != 4 || number is not (44 or 46))
                continue;
            Need(count == 5 && (result >= 0 || result == -11), "web_vector_send_result");
            sends++;
            if (number == 46)
            {
                Need(messages.TryGetValue(call.RecordIndex, out var message), "web_vector_message_required");
                var owner = message!;
                if (generation == 0)
                    generation = owner.Generation;
                Need(owner.Generation == generation && result <= (long)Math.Min(owner.Total, 8192u), "web_vector_generation_or_return");
                if (result > 0 && owner.Vectors.Count(vector => vector.Bytes > 0) >= 2 && (ulong)result > owner.Vectors.First(vector => vector.Bytes > 0).Bytes)
                {
                    scatter++;
                    if ((ulong)result < owner.Total)
                        shorts++;
                }
            }
            else
                Need(result <= 8192, "web_vector_send_result");
            if (result == -11)
                blocked++;
            else
                bytes += (ulong)result;
        }

        Need(count == expectedAccepts, "web_vector_accept_count");
        Progress();
        return new(sends, scatter, shorts, blocked, bytes);
    }
}
