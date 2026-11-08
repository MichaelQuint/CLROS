namespace OS.Tooling;

public static partial class ToolCommand
{
    public static int Run(string[] args)
    {
        try
        {
            if (args is ["benchmark", "retained-serving", var retainedRoot, var retainedCloseout, var retainedImage, var retainedOutput, var retainedMode] && retainedMode is "--prepare" or "--run")
            {
                RetainedServingBenchmark.RunAsync(Repository.FindRoot(), retainedRoot, retainedCloseout, retainedImage,
                    retainedOutput, retainedMode == "--prepare", Console.Out).GetAwaiter().GetResult();
                return 0;
            }
            if (args is ["linux", "sqlite-reference", var sqliteRecipe, var sqliteSource, var sqliteOutput])
                return NativeAotSqliteContract.RunLinuxReferenceAsync(Repository.FindRoot(), sqliteRecipe,
                    sqliteSource, sqliteOutput, Console.Out).GetAwaiter().GetResult();
            if (args is ["linux", "sqlite-reference-with-archive", var dependencyRecipe, var dependencySource, var dependencyRoot, var dependencyOutput])
                return NativeAotSqliteContract.RunLinuxReferenceAsync(Repository.FindRoot(), dependencyRecipe,
                    dependencySource, dependencyOutput, Console.Out, dependencyRoot).GetAwaiter().GetResult();
            if (args is ["linux", "sqlite-recovery-reference", var recoveryRecipe, var recoverySource, var recoveryOutput])
                return NativeAotSqliteContract.RunLinuxRecoveryReferenceAsync(Repository.FindRoot(), recoveryRecipe,
                    recoverySource, recoveryOutput, Console.Out).GetAwaiter().GetResult();
            if (args is ["linux", "sqlite-recovery-reference-with-archive", var recoveryDependencyRecipe, var recoveryDependencySource, var recoveryDependencyRoot, var recoveryDependencyOutput])
                return NativeAotSqliteContract.RunLinuxRecoveryReferenceAsync(Repository.FindRoot(), recoveryDependencyRecipe,
                    recoveryDependencySource, recoveryDependencyOutput, Console.Out, recoveryDependencyRoot).GetAwaiter().GetResult();
            if (args is ["linux", "log-upload-reference", var logUploadRecipe, var logUploadOutput])
            {
                using var reference = NativeAotLogUploadContract.RunLinuxReferenceAsync(
                    Repository.FindRoot(), logUploadRecipe, logUploadOutput, Console.Out).GetAwaiter().GetResult();
                reference.VerifyUnchanged();
                Console.WriteLine($"TEST_PASS linux.log-upload.reference artifacts={reference.Artifacts.Count} completeTraceProjection={reference.CompleteTraceProjection} guest_lock_flush_and_persistence_authority=false");
                return 0;
            }
            if (args is ["linux", "log-upload-http-reference", var httpLogUploadRecipe, var httpLogUploadOutput])
            {
                using var reference = NativeAotLogUploadHttpContract.RunLinuxReferenceAsync(
                    Repository.FindRoot(), httpLogUploadRecipe, httpLogUploadOutput, Console.Out).GetAwaiter().GetResult();
                reference.VerifyUnchanged();
                Console.WriteLine($"TEST_PASS linux.log-upload.http-reference artifacts={reference.Artifacts.Count} guest_lock_flush_and_persistence_authority=false");
                return 0;
            }
            if (args is ["qemu", "inspect-disk-static-values", var staticDiskPackage, var staticDiskImage, var staticDiskIndex, var staticDiskSerial, ..])

                return InspectDiskStaticValues(args, staticDiskPackage, staticDiskImage, staticDiskIndex, staticDiskSerial);
            if (args is ["qemu", "inspect-webroot-inventory", var inventoryImage])

                return InspectWebRootInventory(inventoryImage);
            if (args is ["qemu", "inspect-disk-values", var capturePackage, var captureImage, var captureSerial])
                return InspectDiskValues(capturePackage, captureImage, captureSerial);
            if (args is ["qemu", "inspect-disk-leaf", var diskPackage, var diskSerial])
                return InspectDiskLeaf(diskPackage, diskSerial);
            if (args is ["linux", "disk-files-package", var diskFilePackage])
            {
                LinuxDiskFileProofFixture.WritePackage(Repository.FindRoot(), diskFilePackage);
                Console.WriteLine("TEST_PASS linux.disk-files.package fixture_only=true");
                return 0;
            }
            if (args is ["linux", "disk-race-package", var diskRaceVariant, var diskRacePackage])
            {
                WriteDiskRacePackage(Repository.FindRoot(), diskRaceVariant, diskRacePackage);
                Console.WriteLine("TEST_PASS linux.disk-race.package variant=" + diskRaceVariant + " fixture_only=true");
                return 0;
            }
            if (args is ["nativeaot", "static-files", "index", var staticElf, var staticDump])
                return InspectStaticIndex(staticElf, staticDump);
            if (args is ["nativeaot", "static-files", "program", var staticPackage, var staticProgramDump, var staticProgramMode] && staticProgramMode is "--staged" or "--disk")
            {
                var program = NativeAotStaticFileProgram.Read(Repository.FindRoot(), staticPackage, staticProgramDump, staticProgramMode == "--staged");
                Console.WriteLine("TEST_PASS nativeaot.static-files.program " + program.Metadata);
                return 0;
            }
            if (args is ["nativeaot", "log-upload", "program", var logUploadPackage, var logUploadDump, var logUploadMode] && logUploadMode is "--write" or "--verify")
            {
                Console.WriteLine("TEST_START nativeaot.log-upload.program");
                var program = NativeAotLogUploadProgram.Read(Repository.FindRoot(), logUploadPackage, logUploadDump, logUploadMode[2..]);
                Console.WriteLine($"TEST_PASS nativeaot.log-upload.program phase={program.Phase} instructions={program.Metadata.InstructionCount} syscalls={program.Metadata.SyscallCount} index={program.Metadata.Sha256} grants_execution=false");
                return 0;
            }
            if (args.Length >= 2 && args[0] == "nativeaot" && args[1] == "log-upload")
                throw new ArgumentException("usage: nativeaot log-upload program <canonical-package> <llvm-disassembly> --write|--verify");
            if (args is ["nativeaot", "sqlite", "program", var sqlitePackage, var sqliteDump, var sqliteMode] &&
                sqliteMode is "--database" or "--verify-database" or "--recovery-seed" or "--recovery-spill" or "--recovery-verify")
            {
                Console.WriteLine("TEST_START nativeaot.sqlite.program");
                var program = NativeAotSqliteProgram.Read(Repository.FindRoot(), sqlitePackage, sqliteDump, sqliteMode[2..]);
                Console.WriteLine($"TEST_PASS nativeaot.sqlite.program mode={program.Mode} instructions={program.Metadata.InstructionCount} syscalls={program.Metadata.SyscallCount} index={program.Metadata.Sha256} grants_execution=false");
                return 0;
            }
            if (args.Length >= 2 && args[0] == "nativeaot" && args[1] == "sqlite")
                throw new ArgumentException("usage: nativeaot sqlite program <canonical-package> <llvm-disassembly> --database|--verify-database|--recovery-seed|--recovery-spill|--recovery-verify");
            if (args is ["nativeaot", "static-files", "package", "--recipe-root", var staticRecipe, "--evidence", var staticEvidence,
                "--output", var staticOutput, var staticMode] && staticMode is "--staged" or "--disk")
            {
                string hash = NativeAotStaticFilePackage.Write(Repository.FindRoot(), staticRecipe, staticEvidence, staticOutput, staticMode == "--staged");
                Console.WriteLine("TEST_PASS nativeaot.static-files.package mode=" + staticMode + " sha256=" + hash);
                return 0;
            }
            if (args is ["nativeaot", "static-files", "image", var baseImage, var assetsRoot, var diskOutput, var diskVariant])
            {
                string repository = Repository.FindRoot();
                var snapshot = WebRootImageFixture.SnapshotPublishedRoot(assetsRoot);
                var receipt = WebRootImageFixture.Apply(repository, baseImage, assetsRoot, snapshot, diskOutput, diskVariant);
                WebRootImageFixture.Verify(diskOutput, receipt);
                using var receiptFile = new FileStream(Path.GetFullPath(diskOutput) + ".webroot.json", FileMode.CreateNew, FileAccess.Write, FileShare.None);
                System.Text.Json.JsonSerializer.Serialize(receiptFile, receipt);
                Console.WriteLine("TEST_PASS nativeaot.static-files.image variant=" + diskVariant + " sha256=" + receipt.FinalSha256);
                return 0;
            }
            if (args is ["nativeaot", "static-files", "prepare-client", var publishedRoot, var clientRoot])
            {
                BlazorStaticFileAssets.Prepare(Repository.FindRoot(), publishedRoot, clientRoot);
                Console.WriteLine("TEST_PASS nativeaot.static-files.prepare-client");
                return 0;
            }
            if (args is ["nativeaot", "static-files", "host-page", var publishedWebRoot])
            {
                BlazorStaticFileAssets.WriteHostPage(Repository.FindRoot(), publishedWebRoot);
                Console.WriteLine("TEST_PASS nativeaot.static-files.host-page");
                return 0;
            }
            if (args.Length >= 3 && args[0] == "nativeaot" && args[1] == "static-files")
                return NativeAotStaticFileContract.RunAsync(Repository.FindRoot(), args[2..], Console.Out).GetAwaiter().GetResult();
            if (args is ["linux", "web-index", var webElf, var webDump])
                return InspectWebIndex(webElf, webDump);
            if (args is ["linux", "web-program", var webPackage, var webProgramDump])
            {
                Console.WriteLine("TEST_START linux.web-program");
                var program = NativeAotWebProgram.Read(Repository.FindRoot(), webPackage, webProgramDump);
                Console.WriteLine("TEST_PASS linux.web-program " + program.Metadata);
                return 0;
            }
            if (args is ["qemu", "inspect-socket", var inspectMode, var inspectPackage, var inspectDump, var inspectSerial])
                return InspectSocketCapture(inspectMode, inspectPackage, inspectDump, inspectSerial);
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
                return InspectSocketIndex(socketElf, socketDump);
            if (args is ["linux", "socket-program", var programMode, var socketPackage, var programDump])
            {
                if (programMode is not ("sync" or "async"))
                    throw new ArgumentException("socket_program_mode");
                Console.WriteLine("TEST_START linux.socket-program");
                var program = NativeAotSocketProgram.Read(Repository.FindRoot(), programMode == "async", socketPackage, programDump);
                Console.WriteLine("TEST_PASS linux.socket-program mode=" + programMode + " " + program.Metadata);
                return 0;
            }
            if (args is ["linux", "socket-vectors-package", var vectorEvidence, var vectorDestination])
            {
                Console.WriteLine("TEST_START linux.socket-vectors-package");
                string digest = NativeAotSocketVectorsPackage.Write(Repository.FindRoot(), vectorEvidence, vectorDestination);
                Console.WriteLine("TEST_PASS linux.socket-vectors-package sha256=" + digest);
                return 0;
            }
            if (args is ["linux", "socket-vectors-program", var vectorPackage, var vectorDump])
            {
                Console.WriteLine("TEST_START linux.socket-vectors-program");
                var program = NativeAotSocketVectorsProgram.Read(Repository.FindRoot(), vectorPackage, vectorDump);
                Console.WriteLine("TEST_PASS linux.socket-vectors-program " + program.Metadata);
                return 0;
            }
            if (args is ["linux", "socket-package", var socketMode, var socketEvidence, var socketDestination])
            {
                if (socketMode is not ("sync" or "async"))
                    throw new ArgumentException("socket_package_mode");
                Console.WriteLine("TEST_START linux.socket-package");
                string digest = NativeAotSocketPackage.Write(Repository.FindRoot(), socketEvidence, socketMode == "async", socketDestination);
                Console.WriteLine("TEST_PASS linux.socket-package mode=" + socketMode + " sha256=" + digest);
                return 0;
            }
            if (args is ["linux", "runtime-package", var runtimeApp, var runtimeEvidence, var runtimeDestination])
            {
                Console.WriteLine("TEST_START linux.runtime-package");
                string digest = NativeAotRuntimePackage.Write(Repository.FindRoot(), runtimeEvidence, runtimeApp, runtimeDestination);
                Console.WriteLine("TEST_PASS linux.runtime-package app=" + runtimeApp + " sha256=" + digest);
                return 0;
            }
            if (args is ["linux", "runtime-program", var runtimeName, var runtimePackage, var runtimeDump])
            {
                Console.WriteLine("TEST_START linux.runtime-program");
                var program = NativeAotRuntimeProgram.Read(Repository.FindRoot(), runtimeName, runtimePackage, runtimeDump);
                Console.WriteLine("TEST_PASS linux.runtime-program app=" + program.Application + " " + program.Metadata);
                return 0;
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
            if (args is ["nativeaot", "socket", "guest-peer", "--port", var peerPort, "--serial", var peerSerial] &&
                int.TryParse(peerPort, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int peerPortNumber))
            {
                NativeAotSocketPeer.RunReceiveGatedPeerAsync(Repository.FindRoot(), peerPortNumber, peerSerial, Console.Out).GetAwaiter().GetResult();
                return 0;
            }
            if (args.Length >= 2 && args[0] == "nativeaot" && args[1] == "socket")
                return NativeAotSocketPayload.RunAsync(Repository.FindRoot(), args[2..], Console.Out).GetAwaiter().GetResult();
            if (args.Length >= 2 && args[0] == "nativeaot" && args[1] == "socket-containment")
                return NativeAotSocketContainmentPublisher.RunAsync(Repository.FindRoot(), args[2..], Console.Out).GetAwaiter().GetResult();
            if (args is ["nativeaot", "socket-vectors", "guest-peer", "--port", var vectorPort, "--serial", var vectorSerial] &&
                int.TryParse(vectorPort, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int vectorPortNumber))
            {
                NativeAotSocketVectorsPeer.RunGuestPeerAsync(Repository.FindRoot(), vectorPortNumber, vectorSerial, Console.Out).GetAwaiter().GetResult();
                return 0;
            }
            if (args.Length >= 2 && args[0] == "nativeaot" && args[1] == "socket-vectors")
                return NativeAotSocketVectorsPublisher.RunAsync(Repository.FindRoot(), args[2..], Console.Out).GetAwaiter().GetResult();
            if (args.Length >= 3 && args[0] == "nativeaot" && args[1] == "service" && args[2] == "quota-peer")
            {
                if (args.Length is not (5 or 7) || args[3] != "--port" || args.Length == 7 && (args[5] != "--wire-report" || args[6].Length == 0) ||
                    !int.TryParse(args[4], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int quotaPort) ||
                    quotaPort is < 1 or > 65535 || args[4] != quotaPort.ToString(System.Globalization.CultureInfo.InvariantCulture))
                    throw new ArgumentException("quota_peer_cli expected=nativeaot service quota-peer --port <1..65535> [--wire-report <fresh-absolute-artifact-file>]");
                var observation = (args.Length == 5 ? NativeAotServiceQuotaPeer.RunAsync(quotaPort) :
                    NativeAotServiceQuotaPeer.RunWithWireReportAsync(Repository.FindRoot(), quotaPort, args[6])).GetAwaiter().GetResult();
                Console.Out.Write(NativeAotServiceQuotaPeer.SerializeReport(observation) + "\r\n");
                return 0;
            }
            if (args.Length is 7 or 8 && args[0] == "nativeaot" && args[1] == "service" && args[2] == "guest-peer" &&
                args[3] == "--port" && int.TryParse(args[4], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int servicePort) &&
                args[5] == "--serial" && args[6].Length != 0 && (args.Length == 7 || args[7] == "--lifecycle"))
            {
                NativeAotServicePeer.RunGuestAsync(servicePort, Console.Out, lifecycle: args.Length == 8).GetAwaiter().GetResult();
                return 0;
            }
            if (args.Length >= 2 && args[0] == "nativeaot" && args[1] == "service")
                return NativeAotServicePublisher.RunAsync(Repository.FindRoot(), args[2..], Console.Out).GetAwaiter().GetResult();
            if (args.Length >= 3 && args[0] == "nativeaot" && args[1] == "web" && args[2] == "package" &&
                (args.Contains("--manifest", StringComparer.Ordinal) || args.Contains("--settings", StringComparer.Ordinal)))
                return NativeAotWebPackage.RunConfigured(Repository.FindRoot(), args[2..], Console.Out);
            if (args.Length >= 2 && args[0] == "nativeaot" && args[1] == "web")
                return NativeAotWebPayload.RunAsync(Repository.FindRoot(), args[2..], Console.Out).GetAwaiter().GetResult();
            if (args.Length >= 2 && args[0] == "nativeaot" && args[1] == "threading")
                return NativeAotThreadingPayload.RunAsync(Repository.FindRoot(), args[2..], Console.Out).GetAwaiter().GetResult();
            if (args.Length >= 2 && args[0] == "nativeaot" && args[1] == "runtime")
                return NativeAotRuntimePayload.RunAsync(Repository.FindRoot(), args[2..], Console.Out).GetAwaiter().GetResult();
            if (args is ["efi", "source-inputs", .. var sourceSelectors])
            {
                EfiSourceInputs.RunAsync(Repository.FindRoot(), sourceSelectors, Console.Out).GetAwaiter().GetResult();
                return 0;
            }
            if (args.Length >= 2 && args[0] == "efi" && args[1] == "build")
                return EfiBuild.Run(args[2..]);
            if (args.Length >= 2 && args[0] == "efi" && args[1] == "verify")
                return EfiVerify.Run(args[2..]);
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
                Console.WriteLine("linux log-upload-reference <original-socket-recipe-root> <fresh-artifacts-output> (Linux NativeAOT reference only; requires an exclusive builder lease)");
                Console.WriteLine("linux log-upload-http-reference <original-socket-recipe-root> <fresh-artifacts-output> (fresh HTTP and console NativeAOT reference only; requires an exclusive builder lease)");
                Console.WriteLine("linux sqlite-reference|sqlite-recovery-reference <original-socket-recipe-root> <pinned-source-zip> <fresh-artifacts-output> (Linux NativeAOT reference only; requires an exclusive builder lease)");
                Console.WriteLine("linux sqlite-reference-with-archive <original-socket-recipe-root> <pinned-source-zip> <reviewed-r03-artifacts-root> <fresh-artifacts-output> (explicit receipt-bound archive reuse; fresh app publication and traced processes)");
                Console.WriteLine("linux sqlite-recovery-reference-with-archive <original-socket-recipe-root> <pinned-source-zip> <reviewed-r03-artifacts-root> <fresh-recovery-output> (explicit archive reuse; outer-held originals through fresh process kill/join and reopen; no power-loss authority)");
                Console.WriteLine("nativeaot static-files package --recipe-root <original-candidate-root> --evidence <original-reference> --output <fresh-artifacts-package> --staged|--disk");
                Console.WriteLine("nativeaot static-files index <original-ELF> <llvm-disassembly> | program <package> <llvm-disassembly> --staged|--disk");
                Console.WriteLine("nativeaot log-upload program <canonical-package> <llvm-disassembly> --write|--verify (prepared index only; grants_execution=false)");
                Console.WriteLine("nativeaot sqlite program <canonical-package> <llvm-disassembly> --database|--verify-database|--recovery-seed|--recovery-spill|--recovery-verify (prepared index only; grants_execution=false)");
                Console.WriteLine("nativeaot static-files prepare-client <published-webroot> <fresh-artifacts-webroot>\nnativeaot static-files image <base-image> <prepared-webroot> <fresh-image> DiskA|DiskB\nnativeaot static-files qualify --recipe-root <original-socket-root> --output <fresh-directory> --webroot <prepared-webroot>\nnativeaot static-files validate <evidence-directory>");
                Console.WriteLine("qemu validate-network <serial-log> <pcap> <package> validates retained M07-026 capture inputs without launching a guest.");
                Console.WriteLine("qemu inspect-webroot-inventory <original-raw-image> requires its .webroot.json receipt; reports copied physical metadata only, without native/process/device/HTTP authority.");
                Console.WriteLine("qemu inspect-disk-static-values <package> <raw-image> <complete-instruction-index> <serial> [--full] reports scheduler/file values only; Full emits scalar diagnostics, failed originals stay failed.");
                Console.WriteLine("linux disk-race-package Progress|CloseReuse|InterruptWins|CompletionWins|ExitPending <fresh-artifacts-package>\nqemu smoke -EnableM16025Probe -LinuxDiskRaceVariant ExitPending [original disk options]; complete original ExitPending joins remain mandatory. Other variants are dry-run only. Absent variant retains the canonical59-call fixture.");
                Console.WriteLine("linux socket-vectors-package <original-publication-directory> <fresh-artifacts-package>\nlinux socket-vectors-program <package> <complete-original-llvm-disassembly>");
                Console.WriteLine("linux socket-index <original-elf> <llvm-disassembly> (report only)\nlinux socket-program sync|async <package> <reviewed-llvm-disassembly>\nlinux socket-package sync|async <extracted-socket-evidence> <fresh-artifacts-package>\nlinux runtime-package <app> <extracted-runtime-evidence> <fresh-artifacts-package>\nlinux runtime-program <app> <package> <reviewed-llvm-disassembly>");
                Console.WriteLine("CLROS .NET 11 tooling\ndone next|reserve [--root <repository>]\nrepo check [--root <repository>]\ncontracts check\nefi build|verify [options]\nlinux hello-package <extracted-evidence-r04> <fresh-artifacts-package-path>\nlinux hello-program <hello-package> <llvm-disassembly>\nlinux threading-package <app> <extracted-evidence-root> <fresh-artifacts-package>\nlinux threading-program <app> <package> <llvm-disassembly>\nlinux thread-fixture <fresh-package-path> [Threads|PipeReadClose|PipeWriteClose|PipeShutdown]\nlinux signal-fixture <fresh-package-path> [AlternateStack|UserStack|BadReturn|BadRestorer|FaultRead|FaultWrite|FaultExecute|FaultStack|FaultGp|FaultReturn|FaultFatal|Activation|SignalPipeEintr|SignalPipeRestart|SignalFutexEintr|SignalFutexRestart|SignalPipePartial|SignalPipeStolen|SignalEpollStolen|NestedAlternateStack|AlternateStackExhaustion]\nqemu inspect-disk-leaf <mechanical-package> <original-serial> (report only)\nqemu inspect-disk-values <mechanical-package> <raw-image> <original-serial> (report only)\nqemu smoke|qcow2 [--dry-run] [options]\nlibvirt smoke [--dry-run|--plan-lifecycle|--check-serial-only] [options]\nkestrel package [options]\nnativeaot threading validate-suspension|validate-threadpool <captured-stdout>\nnativeaot threading qualify --output <fresh-directory>\nnativeaot runtime qualify --output <fresh-directory>\nnativeaot payload inspect <file> | qualify --output <fresh-directory>\nci smoke [options]\nbaseline evidence [options]\nbacklog sync [options]");
                return 0;
            }
            if (args is ["repo", "style", "check"] or ["repo", "style", "check", "--root", _])
            {
                string root = args.Length == 5 ? Path.GetFullPath(args[4]) : Repository.FindRoot();
                var findings = RepositoryCodeStyle.Check(root);
                Console.WriteLine("TEST_START source.style");
                foreach (var finding in findings)
                    Console.WriteLine("TEST_FAIL source.style " + finding);
                if (findings.Count == 0)
                    Console.WriteLine("TEST_PASS source.style");
                return findings.Count == 0 ? 0 : 1;
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
                string root = Repository.FindRoot();
                IReadOnlyList<ContractMismatch> failures =
                    [.. ContractAudit.Inspect(root), .. NativeAotCurrentSources.InspectReviewedSources(root)];
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
            if (args.Length >= 3 && args[0] == "nativeaot" && args[1] == "service" && args[2] == "quota-peer")
                Console.Error.Write(NativeAotServiceQuotaPeer.DescribeFailure(error));
            Console.Error.WriteLine("TEST_FAIL " + (args.FirstOrDefault() == "done" ? "done.step.allocate" : "tooling") + " " + error.Message.ReplaceLineEndings(" "));
            return 1;
        }
    }

    /// <summary>Publish exact canonical race-package bytes to one fresh path
    /// below the supplied repository's artifacts directory. Validate the variant
    /// before filesystem changes, reject existing/redirected paths, and retain
    /// the readback through complete-byte identity verification. Relative output
    /// paths resolve against repositoryRoot. Failure never replaces an existing
    /// file; generated output supplies no native, device or guest authority.</summary>
    public static void WriteDiskRacePackage(string repositoryRoot, string variant, string outputPath)
    {
        byte[] package = LinuxDiskRaceProofFixture.CreatePackage(variant);
        string root = Path.GetFullPath(repositoryRoot), output = Path.GetFullPath(outputPath, root);
        string artifacts = Path.Combine(root, "artifacts") + Path.DirectorySeparatorChar;
        if (!output.StartsWith(artifacts, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new ToolFailure("disk_race_package_artifacts_required");
        EfiBuildFiles.InitializeDirectory(Path.GetDirectoryName(output)!, "disk_race_package_output");
        EfiBuildFiles.WriteCreateNew(root, output, package);
        using AuthorityFiles authority = new();
        var held = authority.Open(root, output, "disk race package", "disk_race_package_output", package.Length, singleLink: true);
        if (LinuxDiskRaceProofFixture.PackageVariant(held.ReadBytes(package.Length, "disk_race_package_output")) != variant)
            throw new ToolFailure("disk_race_package_identity");
        authority.VerifyUnchanged();
    }
}
