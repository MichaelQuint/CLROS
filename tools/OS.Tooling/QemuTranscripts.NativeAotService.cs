namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Inspect the successful service fixture's random metadata against
    /// every original syscall and its current generation/return suffix. Borrow
    /// bounded rows synchronously without mutation. Require complete seed/end
    /// records, exact counters and the fixture's large partial/error controls.
    /// Other records remain the full replay consumer's responsibility; this
    /// value-only seam grants no prepared-program or execution authority.</summary>

    public static ServiceRandomInspection InspectNativeAotServiceRandomForTests(string[] rows) => ReadServiceRandom(rows);
    private static ServiceRandomInspection ReadServiceRandom(string[] rows)
    {
        Need(rows is not null && rows.Length is> 0 and <= 1_000_000 && rows.All(row => row is not null && row.Length <= 4096) && rows.Sum(row => (long)row.Length + 2) <= 128L * 1024 * 1024, "service_random_input");
        var returned = ReadLinuxServiceCallReturns(rows!).ToDictionary(row => row.RecordIndex);
        string[] fields = "identity nr a0 a1 a2 a3 a4 a5 rip rsp flags fs gs clock valueBits action status".Split(' ').Select(field => field + ":dec").ToArray();
        const string seed = "M11_012_SEED source=firmware-rng acquisitions=2 entropyBytes=32 nonceBytes=16 domain=getrandom state=ready firmwareCallbacks=none";
        const string sequenceSeed = "M07_033_SEED source=firmware-rng acquisitions=1 secretBytes=32 domain=tcp-isn state=ready firmwareCallbacks=none";
        const string erased = " generator=zero seed=zero firmwareCallbacks=none";
        bool seeded = false, sequenceSeeded = false, ended = false;
        ulong requests = 0, bytes = 0, clock = 0;
        int partial = 0, zero = 0, invalid = 0, fault = 0;
        Dictionary<string, ulong>? pending = null;
        for (int i = 0; i < rows!.Length; i++)
        {
            string row = rows[i];
            if (row.StartsWith("M11_012_SEED", StringComparison.Ordinal))
            {
                Need(!seeded && !sequenceSeeded && !ended && row == seed, "service_random_seed");
                seeded = true;
            }
            else if (row.StartsWith("M07_033_SEED", StringComparison.Ordinal))
            {
                Need(seeded && !sequenceSeeded && !ended && row == sequenceSeed, "service_random_sequence_seed");
                sequenceSeeded = true;
            }
            else if (row.StartsWith("M11_012_RANDOM", StringComparison.Ordinal))
            {
                Need(seeded && sequenceSeeded && !ended && pending is null, "service_random_observation_order");
                pending = ReadRecord(row, "M11_012_RANDOM", "requested:dec", "flags:dec", "valueBits:dec", "requests:dec", "generatedBytes:dec");
            }
            else if (row.StartsWith("M16_008_CALL ", StringComparison.Ordinal))
            {
                Need(seeded && sequenceSeeded && !ended, "service_random_call_lifetime");
                var call = ReadRecord(row[..row.IndexOf(" ordinal=", StringComparison.Ordinal)], "M16_008_CALL", fields);
                var result = returned[i];
                Need(result.Identity == call["identity"] && result.ValueBits == call["valueBits"] && call["clock"] >= clock && call["clock"] <= long.MaxValue, "service_random_call_binding");
                clock = call["clock"];
                if (call["nr"] != 318)
                {
                    Need(pending is null, "service_random_intervening_call");
                    continue;
                }

                Need(pending is not null && call["a1"] == pending["requested"] && call["a2"] == pending["flags"] && call["valueBits"] == pending["valueBits"] && call["action"] == 1 && call["status"] == 1, "service_random_syscall_binding");
                ulong requested = call["a1"], flags = call["a2"], value = call["valueBits"];
                if ((flags & ~3UL) != 0)
                {
                    Need(value == unchecked((ulong)-22L), "service_random_invalid_flags");
                    if (flags == 8 && requested == 1 && call["a0"] != 0)
                        invalid++;
                }
                else if (requested == 0)
                {
                    Need(value == 0, "service_random_zero");
                    if (flags == 1 && call["a0"] != 0)
                        zero++;
                }
                else if (call["a0"] == 0)
                {
                    Need(value == unchecked((ulong)-14L), "service_random_fault");
                    if (flags == 1 && requested == 32)
                        fault++;
                }
                else
                {
                    ulong generated = Math.Min(requested, 8192UL);
                    Need(value == generated && requests < ulong.MaxValue && bytes <= ulong.MaxValue - generated, "service_random_result");
                    requests++;
                    bytes += generated;
                    if (requested == 16384 && flags == 1)
                        partial++;
                }

                Need(pending!["requests"] == requests && pending["generatedBytes"] == bytes, "service_random_accounting");
                pending = null;
            }
            else if (row.StartsWith("M11_012_END", StringComparison.Ordinal))
            {
                Need(seeded && sequenceSeeded && !ended && pending is null && row.EndsWith(erased, StringComparison.Ordinal), "service_random_end");
                var end = ReadRecord(row[..^erased.Length], "M11_012_END", "requests:dec", "generatedBytes:dec");
                Need(end["requests"] == requests && end["generatedBytes"] == bytes, "service_random_end_accounting");
                ended = true;
            }
            else
                Need(!row.StartsWith("M11_012_", StringComparison.Ordinal), "service_random_unknown_record");
        }

        Need(seeded && sequenceSeeded && ended && pending is null && requests > 32 && bytes > 4096 && partial >= 2 && zero > 0 && invalid > 0 && fault > 0, "service_random_fixture_controls");
        return new(requests, bytes, partial, zero, invalid, fault);
    }
}
