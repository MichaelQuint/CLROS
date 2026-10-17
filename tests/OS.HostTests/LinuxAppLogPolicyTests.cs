using System.Text;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Manifest;
using OS.Tooling;

internal static unsafe class LinuxAppLogPolicyTests
{
    private static byte[] Manifest(string environment)
    {
        byte[] package = LinuxThreadProofFixture.CreatePackage("Threads");
        var view = LinuxElfPackage.Validate(package); HostCheck.True(view.Succeeded);
        return Encoding.ASCII.GetBytes(Encoding.ASCII.GetString(package, (int)view.ManifestOffset, (int)view.ManifestBytes) + environment);
    }

    [HostTest("m10-005 log policy selects only exact records64 and preserves parsed source bytes")]
    private static void Selector()
    {
        HostCheck.Equal(1048576U, LinuxAppLogBudget.DefaultByteLimit);
        HostCheck.Equal(4096U, LinuxAppLogBudget.DefaultRecordLimit);
        HostCheck.Equal(64U, LinuxAppLogBudget.QuotaRecordLimit);
        foreach (var (environment, valid, expected) in new[]
        {
            ("", true, 4096U), ("env.UNRELATED=anything\n", true, 4096U), ("env.UNRELATED=\n", true, 4096U),
            ("env.CLROS_LOG_BUDGET_CASE=records64\n", true, 64U),
            ("env.CLROS_LOG_BUDGET_CASE=records64", true, 64U), // Value ends at the exact readable extent.
            ("env.UNRELATED=value\nenv.CLROS_LOG_BUDGET_CASE=records64\nenv.OTHER=value\n", true, 64U),
            ("env.CLROS_LOG_BUDGET_CAS=records64\n", true, 4096U),
            ("env.CLROS_LOG_BUDGET_CASES=records64\n", true, 4096U),
            ("env.XCLROS_LOG_BUDGET_CASE=records64\n", true, 4096U),
            ("env.CLROS_LOG_BUDGET_CASE=records65\n", false, 0U),
            ("env.CLROS_LOG_BUDGET_CASE=records6\n", false, 0U),
            ("env.CLROS_LOG_BUDGET_CASE=records64x\n", false, 0U),
            ("env.CLROS_LOG_BUDGET_CASE=Records64\n", false, 0U),
            ("env.CLROS_LOG_BUDGET_CASE= records64\n", false, 0U),
            ("env.CLROS_LOG_BUDGET_CASE=records64 \n", false, 0U),
            ("env.CLROS_LOG_BUDGET_CASE=\n", false, 0U)
        })
        {
            byte[] bytes = Manifest(environment), before = (byte[])bytes.Clone();
            var parsed = NativeManifestParser.ParseLinuxElf(bytes); HostCheck.True(parsed.Succeeded, environment);
            fixed (byte* source = bytes)
            {
                uint records = uint.MaxValue;
                bool accepted = LinuxAppLogBudget.TryReadRecordLimit(source, (uint)bytes.Length, parsed, out records);
                HostCheck.True(accepted == valid && records == expected, environment);
            }
            HostCheck.True(bytes.AsSpan().SequenceEqual(before));
        }
    }

    [HostTest("m10-005 log policy rejects invalid views pointers extents and duplicate selectors with zero output")]
    private static void Bounds()
    {
        byte[] bytes = Manifest("env.UNRELATED=value\nenv.CLROS_LOG_BUDGET_CASE=records64\n");
        var parsed = NativeManifestParser.ParseLinuxElf(bytes); HostCheck.True(parsed.Succeeded);
        fixed (byte* source = bytes)
        {
            HostCheck.True(!LinuxAppLogBudget.TryReadRecordLimit(null, (uint)bytes.Length, parsed, out uint records) && records == 0);
            foreach (uint length in new[] { 0U, LinuxElfPackage.MaximumManifestBytes + 1, (uint)bytes.Length - 1, (uint)bytes.Length + 1 })
                HostCheck.True(!LinuxAppLogBudget.TryReadRecordLimit(source, length, parsed, out records) && records == 0);
            foreach (ulong address in new[] { 0x0000800000000000UL, 0x00007fffffffffffUL, ulong.MaxValue })
                HostCheck.True(!LinuxAppLogBudget.TryReadRecordLimit((byte*)address, (uint)bytes.Length, parsed, out records) && records == 0);
            for (int mutation = 0; mutation < 13; mutation++)
            {
                var bad = parsed;
                switch (mutation)
                {
                    case 0: bad = default; break;
                    case 1: bad.Status = LinuxElfStatus.InvalidValue; break;
                    case 2: bad.ByteCount--; break;
                    case 3: bad.EnvironmentCount = 17; break;
                    case 4: bad.EnvironmentRanges[0] = uint.MaxValue; break;
                    case 5: bad.EnvironmentRanges[1] = uint.MaxValue; break;
                    case 6: bad.EnvironmentRanges[2] = uint.MaxValue; break;
                    case 7: bad.EnvironmentRanges[3] = uint.MaxValue; break;
                    case 8: bad.EnvironmentRanges[1] = 0; break;
                    case 9:
                        bad.EnvironmentCount = 3;
                        for (int i = 0; i < 4; i++) bad.EnvironmentRanges[8 + i] = bad.EnvironmentRanges[4 + i];
                        break;
                    case 10: bad.EnvironmentRanges[0] = (uint)bytes.Length; bad.EnvironmentRanges[1] = 1; break;
                    case 11: bad.EnvironmentRanges[2] = (uint)bytes.Length; bad.EnvironmentRanges[3] = 1; break;
                    default: bad.EnvironmentRanges[7] = 0; break;
                }
                records = uint.MaxValue;
                HostCheck.True(!LinuxAppLogBudget.TryReadRecordLimit(source, (uint)bytes.Length, bad, out records) && records == 0,
                    "mutation=" + mutation);
            }
        }
    }

    [HostTest("m10-005 log policy admits sixteen parsed environment rows and rejects parser failures")]
    private static void EnvironmentLimit()
    {
        string unrelated = string.Concat(Enumerable.Range(0, 15).Select(i => $"env.UNRELATED_{i}=value\n"));
        byte[] bytes = Manifest(unrelated + "env.CLROS_LOG_BUDGET_CASE=records64\n");
        var parsed = NativeManifestParser.ParseLinuxElf(bytes);
        HostCheck.True(parsed.Succeeded && parsed.EnvironmentCount == 16);
        fixed (byte* source = bytes)
            HostCheck.True(LinuxAppLogBudget.TryReadRecordLimit(source, (uint)bytes.Length, parsed, out uint selected) && selected == 64);
        foreach (string environment in new[]
        {
            "env.CLROS_LOG_BUDGET_CASE=records64\nenv.CLROS_LOG_BUDGET_CASE=records64\n",
            "env.clros_log_budget_case=records64\n",
            unrelated + "env.EXTRA=value\nenv.CLROS_LOG_BUDGET_CASE=records64\n"
        })
        {
            bytes = Manifest(environment); parsed = NativeManifestParser.ParseLinuxElf(bytes);
            HostCheck.True(!parsed.Succeeded);
            fixed (byte* source = bytes)
                HostCheck.True(!LinuxAppLogBudget.TryReadRecordLimit(source, (uint)bytes.Length, parsed, out uint rejected) && rejected == 0);
        }
    }

    [HostTest("m10-005 log policy default and explicit selection allocate no managed memory")]
    private static void AllocationBoundary()
    {
        foreach (string environment in new[] { "", "env.CLROS_LOG_BUDGET_CASE=records64\n" })
        {
            byte[] bytes = Manifest(environment); var parsed = NativeManifestParser.ParseLinuxElf(bytes);
            uint expected = environment.Length == 0 ? 4096U : 64U;
            fixed (byte* source = bytes)
            {
                HostCheck.True(LinuxAppLogBudget.TryReadRecordLimit(source, (uint)bytes.Length, parsed, out uint warm) && warm == expected);
                bool accepted = true; long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 1000; i++)
                    accepted &= LinuxAppLogBudget.TryReadRecordLimit(source, (uint)bytes.Length, parsed, out uint records) && records == expected;
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                HostCheck.True(accepted); HostCheck.Equal(0L, allocated);
            }
        }
    }
}
