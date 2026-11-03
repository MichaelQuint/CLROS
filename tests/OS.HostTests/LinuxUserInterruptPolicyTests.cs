using System.Text;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Manifest;
using OS.Tooling;

internal static unsafe class LinuxUserInterruptPolicyTests
{
    private static byte[] Manifest(string environment)
    {
        byte[] package = LinuxThreadProofFixture.CreatePackage("Threads");
        var view = LinuxElfPackage.Validate(package);
        HostCheck.True(view.Succeeded);
        return Encoding.ASCII.GetBytes(Encoding.ASCII.GetString(package, (int)view.ManifestOffset, (int)view.ManifestBytes) + environment);
    }

    [HostTest("linux.user-irq policy requires exact explicit validated environment and preserves input")]
    private static void ExplicitPolicy()
    {
        foreach (var (environment, valid, expected) in new[]
        {
            ("", true, false), ("env.CLROS_USER_IRQS=0\n", true, false), ("env.CLROS_USER_IRQS=1\n", true, true),
            ("env.CLROS_USER_IRQ=1\n", true, false), ("env.CLROS_USER_IRQS_MORE=1\n", true, false),
            ("env.CLROS_USER_IRQS=01\n", false, false),
            ("env.CLROS_USER_IRQS=true\n", false, false), ("env.CLROS_USER_IRQS=2\n", false, false),
            ("env.CLROS_USER_IRQS=\n", false, false), ("env.UNRELATED=value\nenv.CLROS_USER_IRQS=1\n", true, true)
        })
        {
            byte[] bytes = Manifest(environment), original = bytes.ToArray();
            var parsed = NativeManifestParser.ParseLinuxElf(bytes);
            HostCheck.True(parsed.Succeeded);
            fixed (byte* p = bytes)
            {
                bool accepted = LinuxUserInterruptPolicy.TryRead(p, (uint)bytes.Length, parsed, out bool enabled);
                HostCheck.True(accepted == valid && enabled == expected, environment);
            }
            HostCheck.True(original.AsSpan().SequenceEqual(bytes));
        }
    }

    [HostTest("linux.user-irq policy rejects invalid offsets sizes duplicate identities and unvalidated metadata")]
    private static void Bounds()
    {
        byte[] bytes = Manifest("env.CLROS_USER_IRQS=1\n");
        var parsed = NativeManifestParser.ParseLinuxElf(bytes);
        HostCheck.True(parsed.Succeeded);
        fixed (byte* p = bytes)
        {
            HostCheck.True(!LinuxUserInterruptPolicy.TryRead(null, (uint)bytes.Length, parsed, out bool enabled) && !enabled);
            HostCheck.True(!LinuxUserInterruptPolicy.TryRead(p, 0, parsed, out enabled) && !enabled);
            HostCheck.True(!LinuxUserInterruptPolicy.TryRead(p, 4097, parsed, out enabled) && !enabled);
            HostCheck.True(!LinuxUserInterruptPolicy.TryRead(p, (uint)bytes.Length - 1, parsed, out enabled) && !enabled);
            for (int mutation = 0; mutation < 8; mutation++)
            {
                var bad = parsed;
                switch (mutation)
                {
                    case 0:
                        bad.Status = LinuxElfStatus.InvalidValue;
                        break;
                    case 1:
                        bad.EnvironmentCount = 17;
                        break;
                    case 2:
                        bad.EnvironmentRanges[0] = uint.MaxValue;
                        break;
                    case 3:
                        bad.EnvironmentRanges[1] = uint.MaxValue;
                        break;
                    case 4:
                        bad.EnvironmentRanges[2] = uint.MaxValue;
                        break;
                    case 5:
                        bad.EnvironmentRanges[3] = uint.MaxValue;
                        break;
                    case 6:
                        bad.EnvironmentRanges[1] = 0;
                        break;
                    case 7:
                        bad.EnvironmentCount = 2;
                        for (int i = 0; i < 4; i++)
                            bad.EnvironmentRanges[i + 4] = bad.EnvironmentRanges[i];
                        break;
                }
                HostCheck.True(!LinuxUserInterruptPolicy.TryRead(p, (uint)bytes.Length, bad, out enabled) && !enabled, "mutation=" + mutation);
            }
        }
        HostCheck.True(!NativeManifestParser.ParseLinuxElf(Manifest("env.CLROS_USER_IRQS=1\nenv.CLROS_USER_IRQS=0\n")).Succeeded);
        HostCheck.True(!NativeManifestParser.ParseLinuxElf(Manifest("env.clros_user_irqs=1\n")).Succeeded);
    }
}
