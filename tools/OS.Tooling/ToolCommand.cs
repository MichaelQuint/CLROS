namespace OS.Tooling;

public static class ToolCommand
{
    public static int Run(string[] args)
    {
        try
        {
            if (args.Length is 3 or 4 && args[0] == "linux" && args[1] == "thread-fixture")
            {
                string packagePath = args[2];
                LinuxThreadProofFixture.WritePackage(packagePath, args.Length == 4 ? args[3] : "Threads");
                Console.WriteLine("TEST_PASS linux.thread-fixture package=" + Path.GetFullPath(packagePath));
                return 0;
            }
            if (args.Length >= 2 && args[0] == "nativeaot" && args[1] == "payload")
                return NativeAotPayload.RunAsync(Repository.FindRoot(), args[2..], Console.Out).GetAwaiter().GetResult();
            if (args.Length >= 2 && args[0] == "efi" && args[1] == "build") return EfiBuild.Run(args[2..]);
            if (args.Length >= 2 && args[0] == "efi" && args[1] == "verify") return EfiVerify.Run(args[2..]);
            if (args.Length >= 2 && args[0] == "qemu" && args[1] == "smoke")
                return QemuSmoke.RunAsync(Repository.FindRoot(), QemuSmokeOptions.Parse(args[2..]), Console.Out).GetAwaiter().GetResult();
            if (args.Length >= 2 && args[0] == "qemu" && args[1] == "qcow2")
                return QemuQcow2.RunAsync(Repository.FindRoot(), QemuQcow2Options.Parse(args[2..]), Console.Out).GetAwaiter().GetResult();
            if (args.Length >= 2 && args[0] == "backlog" && args[1] == "sync")
                return BacklogSync.RunAsync(Repository.FindRoot(), args[2..], Console.Out).GetAwaiter().GetResult();
            if (args.Length >= 2 && args[0] == "ci" && args[1] == "smoke")
                return CiSmoke.RunAsync(Repository.FindRoot(), args[2..], Console.Out).GetAwaiter().GetResult();
            if (args.Length >= 2 && args[0] == "baseline" && args[1] == "evidence")
                return BaselineEvidence.RunAsync(Repository.FindRoot(), args[2..], Console.Out).GetAwaiter().GetResult();
            if (args.Length >= 2 && args[0] == "kestrel" && args[1] == "package")
                return MinimalApiPackage.RunAsync(Repository.FindRoot(), MinimalApiPackageOptions.Parse(args[2..]), Console.Out).GetAwaiter().GetResult();
            if (args.Length >= 2 && args[0] == "libvirt" && args[1] == "smoke")
                return LibvirtSmoke.RunAsync(Repository.FindRoot(), LibvirtSmokeOptions.Parse(args[2..]), Console.Out).GetAwaiter().GetResult();
            if (args.Length == 0 || args is ["--help"])
            {
                Console.WriteLine("CLROS .NET 11 tooling\ndone next|reserve [--root <repository>]\nrepo check [--root <repository>]\ncontracts check\nefi build|verify [options]\nlinux thread-fixture <fresh-package-path> [Threads|PipeReadClose|PipeWriteClose|PipeShutdown]\nqemu smoke|qcow2 [--dry-run] [options]\nlibvirt smoke [--dry-run|--plan-lifecycle|--check-serial-only] [options]\nkestrel package [options]\nnativeaot payload inspect <file> | qualify --output <fresh-directory>\nci smoke [options]\nbaseline evidence [options]\nbacklog sync [options]");
                return 0;
            }
            if (args.Length >= 2 && args[0] == "repo" && args[1] == "check")
            {
                string root = args.Length switch
                {
                    2 => Repository.FindRoot(),
                    4 when args[2] == "--root" => Path.GetFullPath(args[3]),
                    _ => throw new ArgumentException("usage: repo check [--root <repository>]")
                };
                var result = RepositoryChecks.Check(root);
                result.WriteTo(Console.Out);
                return result.Succeeded ? 0 : 1;
            }
            if (args is ["contracts", "check"])
            {
                Console.WriteLine("TEST_START contracts.audit");
                var failures = ContractAudit.Inspect(Repository.FindRoot());
                foreach (var failure in failures)
                    Console.WriteLine($"CONTRACT_MISMATCH {failure.Contract} expected={failure.Expected} actual={failure.Actual}");
                Console.WriteLine(failures.Count == 0 ? "TEST_PASS contracts.audit" : $"TEST_FAIL contracts.audit failures={failures.Count}");
                return failures.Count == 0 ? 0 : 1;
            }
            if (args.Length >= 2 && args[0] == "done" && args[1] is "next" or "reserve")
            {
                string root = args.Length switch
                {
                    2 => Repository.FindRoot(),
                    4 when args[2] == "--root" => Path.GetFullPath(args[3]),
                    _ => throw new ArgumentException("usage: done next|reserve [--root <repository>]")
                };
                Console.WriteLine("TEST_START done.step.allocate");
                string path = DoneSteps.Allocate(root, args[1] == "reserve");
                Console.WriteLine("NEXT_DONE_STEP " + path);
                Console.WriteLine("TEST_PASS done.step.allocate");
                return 0;
            }
            throw new ArgumentException("unknown_command");
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("TEST_FAIL " + (args.FirstOrDefault() == "done" ? "done.step.allocate" : "tooling") + " " + error.Message.ReplaceLineEndings(" "));
            return 1;
        }
    }
}
