namespace OS.Tooling;

public static class ToolCommand
{
    public static int Run(string[] args)
    {
        try
        {
            if (args is ["linux", "web-index", var webElf, var webDump])
            {
                Console.WriteLine("TEST_START linux.web-index");
                webElf = Path.GetFullPath(webElf); webDump = Path.GetFullPath(webDump);
                using AuthorityFiles files = new();
                var elf = files.Open(Path.GetDirectoryName(webElf)!, webElf, "web ELF", "web_index_elf", 16 * 1024 * 1024, singleLink: true);
                var dump = files.Open(Path.GetDirectoryName(webDump)!, webDump, "web disassembly", "web_index_dump", NativeAotWebProgram.MaximumDisassemblyBytes, singleLink: true);
                AuthorityFiles.RequireDistinct(elf, dump);
                var report = NativeAotWebProgram.Inspect(elf.ReadBytes(16 * 1024 * 1024, "web_index_elf"), dump.ReadUtf8(NativeAotWebProgram.MaximumDisassemblyBytes, "web_index_dump"));
                files.VerifyUnchanged(); Console.WriteLine("TEST_PASS linux.web-index report_only=true " + report); return 0;
            }
            if (args is ["linux", "web-program", var webPackage, var webProgramDump])
            {
                Console.WriteLine("TEST_START linux.web-program");
                var program = NativeAotWebProgram.Read(Repository.FindRoot(), webPackage, webProgramDump);
                Console.WriteLine("TEST_PASS linux.web-program " + program.Metadata); return 0;
            }
            if(args is ["qemu","inspect-socket",var inspectMode,var inspectPackage,var inspectDump,var inspectSerial])
            {
                if(inspectMode is not ("sync" or "async"))throw new ArgumentException("socket_inspect_mode");
                Console.WriteLine("TEST_START qemu.socket.capture report_only=true");
                string root=Repository.FindRoot();inspectSerial=Path.GetFullPath(inspectSerial);inspectPackage=Path.GetFullPath(inspectPackage);inspectDump=Path.GetFullPath(inspectDump);
                using AuthorityFiles files=new();
                AuthorityFile Hold(string path,long maximum)=>files.Open(Path.GetDirectoryName(path)!,path,"socket capture inspection","socket_inspection",maximum,singleLink:true);
                var serial=Hold(inspectSerial,8*1024*1024);var pcap=Hold(inspectSerial+".pcap",4*1024*1024);
                var peer=Hold(inspectSerial+".peer.log",65536);var package=Hold(inspectPackage,16*1024*1024);var dump=Hold(inspectDump,NativeAotSocketProgram.MaximumDisassemblyBytes);
                AuthorityFiles.RequireDistinct(serial,pcap,peer,package,dump);
                var program=NativeAotSocketProgram.Read(root,inspectMode=="async",inspectPackage,inspectDump);
                NativeAotSocketPeer.InspectOutput(peer.ReadUtf8(65536,"socket_inspection_peer"));
                QemuTranscripts.AssertM03AcpiBootstrapTranscript(inspectSerial,firstRealExit:true,ownedSurvival:true,pciEnumeration:true,
                    virtioNegotiation:true,virtqueuePolling:true,apicSetup:true,virtioNet:true,arpReceive:true,linuxElfPackagePath:inspectPackage,
                    workloadServiceLoop:true,linuxStartup:true,linuxSyscalls:true,linuxMemory:true,linuxDescriptors:true,linuxThreads:true,linuxSignals:true,
                    networkService:true,socketProgram:program);
                files.VerifyUnchanged();
                Console.WriteLine("TEST_PASS qemu.socket.capture report_only=true own_process_and_native_provenance_required=true");return 0;
            }
            if (args is ["qemu", "validate-network", var networkSerial, var networkPcap, var networkPackage])
            {
                Console.WriteLine("TEST_START qemu.network.capture");
                using FileStream networkSerialLock = new(networkSerial, FileMode.Open, FileAccess.Read, FileShare.Read);
                using FileStream networkPacketLock = new(networkPcap, FileMode.Open, FileAccess.Read, FileShare.Read);
                using FileStream networkPackageLock = new(networkPackage, FileMode.Open, FileAccess.Read, FileShare.Read);
                QemuTranscripts.AssertM03AcpiBootstrapTranscript(networkSerial, firstRealExit: true, ownedSurvival: true,
                    pciEnumeration: true, virtioNegotiation: true, virtqueuePolling: true, virtioNet: true, arpReceive: true,
                    linuxElfPackagePath: networkPackage, workloadServiceLoop: true, networkService: true);
                QemuTranscripts.AssertM07026Capture(networkSerial, networkPcap);
                Console.WriteLine("TEST_PASS qemu.network.capture");
                return 0;
            }
            if (args is ["linux", "socket-index", var socketElf, var socketDump])
            {
                Console.WriteLine("TEST_START linux.socket-index");
                using AuthorityFiles files = new();
                socketElf = Path.GetFullPath(socketElf); socketDump = Path.GetFullPath(socketDump);
                var elf = files.Open(Path.GetDirectoryName(socketElf)!, socketElf, "socket ELF", "socket_index_elf", 16 * 1024 * 1024, singleLink: true);
                var dump = files.Open(Path.GetDirectoryName(socketDump)!, socketDump, "socket disassembly", "socket_index_dump", NativeAotSocketProgram.MaximumDisassemblyBytes, singleLink: true);
                AuthorityFiles.RequireDistinct(elf, dump);
                var report = NativeAotSocketProgram.Inspect(elf.ReadBytes(16 * 1024 * 1024, "socket_index_elf"), dump.ReadUtf8(NativeAotSocketProgram.MaximumDisassemblyBytes, "socket_index_dump"));
                files.VerifyUnchanged(); Console.WriteLine("TEST_PASS linux.socket-index report_only=true " + report); return 0;
            }
            if (args is ["linux", "socket-program", var programMode, var socketPackage, var programDump])
            {
                if (programMode is not ("sync" or "async")) throw new ArgumentException("socket_program_mode");
                Console.WriteLine("TEST_START linux.socket-program");
                var program = NativeAotSocketProgram.Read(Repository.FindRoot(), programMode == "async", socketPackage, programDump);
                Console.WriteLine("TEST_PASS linux.socket-program mode=" + programMode + " " + program.Metadata); return 0;
            }
            if (args is ["linux", "socket-package", var socketMode, var socketEvidence, var socketDestination])
            {
                if (socketMode is not ("sync" or "async")) throw new ArgumentException("socket_package_mode");
                Console.WriteLine("TEST_START linux.socket-package");
                string digest = NativeAotSocketPackage.Write(Repository.FindRoot(), socketEvidence, socketMode == "async", socketDestination);
                Console.WriteLine("TEST_PASS linux.socket-package mode=" + socketMode + " sha256=" + digest); return 0;
            }
            if(args is ["linux","runtime-package",var runtimeApp,var runtimeEvidence,var runtimeDestination])
            {
                Console.WriteLine("TEST_START linux.runtime-package");
                string digest=NativeAotRuntimePackage.Write(Repository.FindRoot(),runtimeEvidence,runtimeApp,runtimeDestination);
                Console.WriteLine("TEST_PASS linux.runtime-package app="+runtimeApp+" sha256="+digest);return 0;
            }
            if(args is ["linux","runtime-program",var runtimeName,var runtimePackage,var runtimeDump])
            {
                Console.WriteLine("TEST_START linux.runtime-program");
                var program=NativeAotRuntimeProgram.Read(Repository.FindRoot(),runtimeName,runtimePackage,runtimeDump);
                Console.WriteLine("TEST_PASS linux.runtime-program app="+program.Application+" "+program.Metadata);return 0;
            }
            if (args is ["linux", "threading-package", var threadingApp, var threadingEvidence, var threadingDestination])
            {
                Console.WriteLine("TEST_START linux.threading-package");
                string digest = NativeAotThreadingPackage.Write(Repository.FindRoot(), threadingEvidence, threadingApp, threadingDestination);
                Console.WriteLine("TEST_PASS linux.threading-package app=" + threadingApp + " sha256=" + digest);
                return 0;
            }
            if (args is ["linux", "threading-program", var appName, var packageInput, var dumpInput])
            {
                Console.WriteLine("TEST_START linux.threading-program");
                var program = NativeAotThreadingProgram.Read(Repository.FindRoot(), appName, packageInput, dumpInput);
                Console.WriteLine("TEST_PASS linux.threading-program app=" + program.Application + " " + program.Metadata);
                return 0;
            }
            if (args is ["nativeaot", "threading", "validate-threadpool", var threadPoolCapture])
            {
                Console.WriteLine("TEST_START nativeaot.threadpool-output");
                NativeAotThreadingGuest.ValidateThreadPoolFile(threadPoolCapture);
                Console.WriteLine("TEST_PASS nativeaot.threadpool-output");
                return 0;
            }
            if (args is ["nativeaot", "threading", "validate-suspension", var capture])
            {
                Console.WriteLine("TEST_START nativeaot.suspension-output");
                NativeAotThreadingGuest.ValidateSuspensionFile(capture);
                Console.WriteLine("TEST_PASS nativeaot.suspension-output");
                return 0;
            }
            if (args is ["linux", "hello-package", var evidenceRoot, var destination])
            {
                Console.WriteLine("TEST_START linux.hello-package");
                string hash = NativeAotHelloGuest.WritePackage(Repository.FindRoot(), evidenceRoot, destination);
                Console.WriteLine("TEST_PASS linux.hello-package sha256=" + hash + " elf=" + NativeAotHelloGuest.ElfSha256);
                return 0;
            }
            if (args is ["linux", "hello-program", var helloPackage, var disassembly])
            {
                Console.WriteLine("TEST_START linux.hello-program");
                _ = NativeAotHelloProgram.Read(Repository.FindRoot(), helloPackage, disassembly);
                Console.WriteLine($"TEST_PASS linux.hello-program instructions={NativeAotHelloProgram.InstructionCount} syscalls={NativeAotHelloProgram.SyscallCount} index={NativeAotHelloProgram.IndexSha256}");
                return 0;
            }
            if (args.Length is 3 or 4 && args[0] == "linux" && args[1] == "signal-fixture")
            {
                string packagePath = args[2];
                LinuxSignalProofFixture.WritePackage(packagePath, args.Length == 4 ? args[3] : "AlternateStack");
                Console.WriteLine("TEST_PASS linux.signal-fixture package=" + Path.GetFullPath(packagePath));
                return 0;
            }
            if (args.Length is 3 or 4 && args[0] == "linux" && args[1] == "thread-fixture")
            {
                string packagePath = args[2];
                LinuxThreadProofFixture.WritePackage(packagePath, args.Length == 4 ? args[3] : "Threads");
                Console.WriteLine("TEST_PASS linux.thread-fixture package=" + Path.GetFullPath(packagePath));
                return 0;
            }
            if (args.Length >= 2 && args[0] == "nativeaot" && args[1] == "payload")
                return NativeAotPayload.RunAsync(Repository.FindRoot(), args[2..], Console.Out).GetAwaiter().GetResult();
            if(args is ["nativeaot","socket","guest-peer","--port",var peerPort,"--serial",var peerSerial] &&
                int.TryParse(peerPort,System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out int peerPortNumber))
            {NativeAotSocketPeer.RunReceiveGatedPeerAsync(Repository.FindRoot(),peerPortNumber,peerSerial,Console.Out).GetAwaiter().GetResult();return 0;}
            if (args.Length >= 2 && args[0] == "nativeaot" && args[1] == "socket")
                return NativeAotSocketPayload.RunAsync(Repository.FindRoot(), args[2..], Console.Out).GetAwaiter().GetResult();
            if (args.Length >= 3 && args[0] == "nativeaot" && args[1] == "web" && args[2] == "package" &&
                (args.Contains("--manifest", StringComparer.Ordinal) || args.Contains("--settings", StringComparer.Ordinal)))
                return NativeAotWebPackage.RunConfigured(Repository.FindRoot(), args[2..], Console.Out);
            if (args.Length >= 2 && args[0] == "nativeaot" && args[1] == "web")
                return NativeAotWebPayload.RunAsync(Repository.FindRoot(), args[2..], Console.Out).GetAwaiter().GetResult();
            if (args.Length >= 2 && args[0] == "nativeaot" && args[1] == "threading")
                return NativeAotThreadingPayload.RunAsync(Repository.FindRoot(), args[2..], Console.Out).GetAwaiter().GetResult();
            if (args.Length >= 2 && args[0] == "nativeaot" && args[1] == "runtime")
                return NativeAotRuntimePayload.RunAsync(Repository.FindRoot(), args[2..], Console.Out).GetAwaiter().GetResult();
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
                Console.WriteLine("qemu validate-network <serial-log> <pcap> <package> validates retained M07-026 capture inputs without launching a guest.");
                Console.WriteLine("linux socket-index <original-elf> <llvm-disassembly> (report only)\nlinux socket-program sync|async <package> <reviewed-llvm-disassembly>\nlinux socket-package sync|async <extracted-socket-evidence> <fresh-artifacts-package>\nlinux runtime-package <app> <extracted-runtime-evidence> <fresh-artifacts-package>\nlinux runtime-program <app> <package> <reviewed-llvm-disassembly>");
                Console.WriteLine("CLROS .NET 11 tooling\ndone next|reserve [--root <repository>]\nrepo check [--root <repository>]\ncontracts check\nefi build|verify [options]\nlinux hello-package <extracted-evidence-r04> <fresh-artifacts-package-path>\nlinux hello-program <hello-package> <llvm-disassembly>\nlinux threading-package <app> <extracted-evidence-root> <fresh-artifacts-package>\nlinux threading-program <app> <package> <llvm-disassembly>\nlinux thread-fixture <fresh-package-path> [Threads|PipeReadClose|PipeWriteClose|PipeShutdown]\nlinux signal-fixture <fresh-package-path> [AlternateStack|UserStack|BadReturn|BadRestorer|FaultRead|FaultWrite|FaultExecute|FaultStack|FaultGp|FaultReturn|FaultFatal|Activation|SignalPipeEintr|SignalPipeRestart|SignalFutexEintr|SignalFutexRestart|SignalPipePartial|NestedAlternateStack|AlternateStackExhaustion]\nqemu smoke|qcow2 [--dry-run] [options]\nlibvirt smoke [--dry-run|--plan-lifecycle|--check-serial-only] [options]\nkestrel package [options]\nnativeaot threading validate-suspension|validate-threadpool <captured-stdout>\nnativeaot threading qualify --output <fresh-directory>\nnativeaot runtime qualify --output <fresh-directory>\nnativeaot payload inspect <file> | qualify --output <fresh-directory>\nci smoke [options]\nbaseline evidence [options]\nbacklog sync [options]");
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
