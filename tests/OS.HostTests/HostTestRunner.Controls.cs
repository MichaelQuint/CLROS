using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

internal static partial class HostTestRunner
{
    private static async Task<bool> AsyncFalseControl()
    {
        await Task.Delay(10);
        return false;
    }

    private static async Task AsyncThrowControl()
    {
        await Task.Delay(10);
        throw new InvalidOperationException("async_control_failure");
    }

    private static async Task AsyncPassControl()
    {
        await Task.Delay(10);
        Console.WriteLine("ASYNC_CONTROL_AFTER joined=true");
    }

    private static async Task<int> RunControlAsync(string[] args)
    {
        switch (args[1])
        {
            case "fingerprint":
                var fingerprintFixtures = Discover().Where(f => Owner(f.Method) == args[2]).ToArray();
                if (fingerprintFixtures.Length == 0)
                    return 2;
                Console.WriteLine(HostTestFingerprint.Create(HostToolProcess.FindRepositoryRoot()).For(fingerprintFixtures, null));
                return 0;
            case "failures":
                int failures = 0;
                HostCheck.Run("control.first", () => false, ref failures);
                HostCheck.Run("control.second", () => throw new InvalidOperationException("second_failure"), ref failures);
                HostCheck.Run("control.after", () => true, ref failures);
                return failures == 2 ? 1 : 2;
            case "async-failures":
                int asyncFailures = 0;
                foreach ((string name, string method) in new[]
                {
                    ("false", nameof(AsyncFalseControl)),
                    ("throw", nameof(AsyncThrowControl)),
                    ("after", nameof(AsyncPassControl))
                }

                )
                    HostCheck.Run("control.async." + name, () => Invoke(typeof(HostTestRunner).GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic)!), ref asyncFailures);
                return asyncFailures == 2 ? 1 : 2;
            case "timeout":
                await Task.Delay(Timeout.Infinite);
                return 2;
            case "wait-after-output":
                Console.WriteLine("control-ready");
                await Task.Delay(Timeout.Infinite);
                return 2;
            case "crash":
                return 23;
            case "output":
                Console.Write(new string('x', 100_000));
                return 0;
            case "echo":
                Console.Write(JsonSerializer.Serialize(args[2..]));
                return 0;
            case "environment":
                Console.Write(JsonSerializer.Serialize(args[2..].Select(Environment.GetEnvironmentVariable).ToArray()));
                return 0;
            case "streams":
            case "streams-wait":
                Console.Write("stdout-ok");
                Console.Error.Write("stderr-ok");
                Console.Out.Flush();
                Console.Error.Flush();
                if (args[1] == "streams-wait")
                    await Task.Delay(Timeout.Infinite);
                return 0;
            case "partial-object":
                File.WriteAllBytes(args[2], [1, 2, 3, 4, 5, 6, 7, 8]);
                Console.WriteLine("partial-object-written");
                return 0;
            case "partial-before-timeout":
                Console.WriteLine("partial-before-timeout");
                await Task.Delay(Timeout.Infinite);
                return 2;
            case "uppercase-output":
                Console.Write(new string('X', 4096));
                return 0;
            case "invalid-utf8":
                var bytes = Console.OpenStandardOutput();
                bytes.WriteByte(255);
                bytes.Flush();
                await Task.Delay(Timeout.Infinite);
                return 2;
            case "locked-child":
                using (var sentinel = File.Open(args[2], FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                    await Task.Delay(Timeout.Infinite);
                return 2;
            case "spawn-locked-child":
                var childStart = new ProcessStartInfo(Environment.ProcessPath!)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                foreach (string value in new[]
                {
                    typeof(HostTestRunner).Assembly.Location,
                    "--runner-control",
                    "locked-child",
                    args[2]
                }

                )
                    childStart.ArgumentList.Add(value);
                if (args.Length > 5 && args[5] == "detached")
                {
                    childStart.RedirectStandardOutput = true;
                    childStart.RedirectStandardError = true;
                }

                using (var child = Process.Start(childStart)!)
                {
                    var childDeadline = Stopwatch.StartNew();
                    while (!File.Exists(args[2]))
                    {
                        if (childDeadline.Elapsed > TimeSpan.FromSeconds(4))
                            return 3;
                        await Task.Delay(20);
                    }

                    if (args.Length > 3 && args[3] == "exit-parent")
                    {
                        Console.Write("parent-output");
                        Console.Error.Write("parent-error");
                        return args.Length > 4 ? int.Parse(args[4], System.Globalization.CultureInfo.InvariantCulture) : 0;
                    }

                    if (args.Length > 3 && args[3] == "signal-and-wait")
                    {
                        Console.Write("parent-output");
                        Console.Error.Write("parent-error");
                    }

                    if (args.Length > 3 && args[3] == "overflow")
                        Console.Write(new string('x', 100_000));
                    await Task.Delay(Timeout.Infinite);
                }

                return 2;
            default:
                return 2;
        }
    }
}
