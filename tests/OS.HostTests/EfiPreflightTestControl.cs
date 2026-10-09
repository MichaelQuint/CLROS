using System.Diagnostics;
using OS.Tooling;

// Explicit child-process controls. These are not registered as tests.
internal static class EfiPreflightTestControl
{
    internal static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0) return 2;
        switch (args[0])
        {
            case "mutex":
                try { using var owned = EfiBuildFiles.OpenPublishLock(args[1]); return args[2] == "acquire" ? 0 : 20; }
                catch (ToolFailure error) { return error.Reason == "publish_output_lock_failed" && args[2] == "blocked" ? 0 : 21; }
            case "child":
                using (File.Open(args[1], FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                {
                    File.WriteAllText(args[2], Environment.ProcessId.ToString());
                    await Task.Delay(Timeout.Infinite);
                }
                return 2;
            case "tree":
                File.WriteAllText(args[3], Environment.ProcessId.ToString());
                var start = new ProcessStartInfo(HostTools.FindExecutable(null, "", "dotnet")!) { UseShellExecute = false, CreateNoWindow = true };
                foreach (string value in new[] { typeof(EfiPreflightTestControl).Assembly.Location, "--efi-control", "child", args[1], args[2] }) start.ArgumentList.Add(value);
                using (var child = Process.Start(start)!)
                {
                    var time = Stopwatch.StartNew();
                    while (!File.Exists(args[2])) { if (time.Elapsed > TimeSpan.FromSeconds(4)) return 3; await Task.Delay(20); }
                    Console.WriteLine("EFI_CHILD_READY childPid=" + child.Id);
                    if (args.Length > 4 && args[4] == "exit-parent") return 0;
                    await Task.Delay(Timeout.Infinite);
                }
                return 2;
            default: return 2;
        }
    }
}
