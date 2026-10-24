using System.Buffers.Binary;
using System.Text;
using OS.Manifest;
using OS.Tooling;
using F = OS.Tooling.LinuxDiskRaceProofFixture;

internal static class LinuxDiskRaceProofFixtureTests
{
    [HostTest("exit pending native selection authenticates the entire independently generated package")]
    private static void ExitPendingNativeIdentity()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(root, "src/OS/Boot/EfiEntry.DiskEvidence.cs"));
        var words = System.Text.RegularExpressions.Regex.Matches(source, @"digest\[(\d)\] == 0x([0-9a-f]{16})");
        HostCheck.Equal(4, words.Count);
        byte[] admitted = new byte[32];
        foreach (System.Text.RegularExpressions.Match word in words)
            BinaryPrimitives.WriteUInt64LittleEndian(admitted.AsSpan(int.Parse(word.Groups[1].Value) * 8),
                Convert.ToUInt64(word.Groups[2].Value, 16));
        byte[] canonical = F.CreatePackage("ExitPending");
        HostCheck.True(source.Contains("state->LinuxBytes != " + canonical.Length, StringComparison.Ordinal));
        HostCheck.True(admitted.AsSpan().SequenceEqual(System.Security.Cryptography.SHA256.HashData(canonical)));
        foreach (string variant in F.Variants.Where(v => v != "ExitPending"))
            HostCheck.True(!admitted.AsSpan().SequenceEqual(System.Security.Cryptography.SHA256.HashData(F.CreatePackage(variant))));
        canonical[^1] ^= 1;
        HostCheck.True(!admitted.AsSpan().SequenceEqual(System.Security.Cryptography.SHA256.HashData(canonical)));
    }

    [HostTest("exit pending native observations surround actual dispatch cancellation reset and release")]
    private static void ExitPendingNativeOrdering()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string Read(string leaf) => File.ReadAllText(Path.Combine(root, "src/OS/Boot", leaf));
        static void Ordered(string text, params string[] steps)
        {
            int at = 0;
            foreach (string step in steps)
            {
                int next = text.IndexOf(step, at, StringComparison.Ordinal);
                HostCheck.True(next >= at, step); at = next + step.Length;
            }
        }
        string threads = Read("EfiEntry.LinuxThreads.cs");
        Ordered(threads, "LinuxSyscallFrame call = *frame;",
            "if (call.Number != 231 && !PollLinuxSocketNetwork(c))",
            "ObserveNativeDiskExitCall(c, index, call)");
        Ordered(threads, "ObserveNativeDiskExitCall(c, index, call)", "LinuxSyscallGateway.Dispatch(ref c->Gates[index], ref *frame)");
        Ordered(threads, "ObserveNativeDiskExitCommit(c, index, false)", "c->Process.CommitExit(c->Key, 2, worker, out long clearTid)",
            "ObserveNativeDiskExitCommit(c, index, true)");
        Ordered(Read("EfiEntry.DiskNative.cs"), "ObserveNativeDiskExitBeforeStop(o)", "Block.Stop(&o->Device)",
            "ObserveNativeDiskExitStopped(o, stopped)", "Fs.Stop(&o->Mount)");
        Ordered(Read("EfiEntry.LinuxThreadRun.cs"), "RetireNativeDiskAttachment(state, c)", "ObserveNativeDiskExitSentinel(state, c)",
            "LinuxNativeRelease(c, LinuxNativeRsp(c))", "ObserveNativeDiskExitReleased(state, c)",
            "LinuxNativeClearUserMemory(state)", "ObserveNativeDiskExitMemoryRestored(state)",
            "ClearLinuxRandom(state)", "ObserveNativeDiskExitCleanupEnd(state, diskStarted)", "M16_008_BUDGET_END");
        string evidence = Read("EfiEntry.DiskEvidence.cs");
        HostCheck.True(evidence.Contains("!o->Device.ReadPending || !o->Mount.IoActive", StringComparison.Ordinal));
        HostCheck.True(evidence.Contains("CopyLinuxAlias(state, 0x404000UL + offset, chunk, 128, false)", StringComparison.Ordinal));
        HostCheck.True(evidence.Contains("offset < 8192; offset += 128", StringComparison.Ordinal));
        HostCheck.True(!evidence.Contains("CompletedCount++", StringComparison.Ordinal) && !evidence.Contains("Block.Poll(", StringComparison.Ordinal));
        HostCheck.True(evidence.Contains("M16_025_EXIT_REJECT reason=", StringComparison.Ordinal));
        HostCheck.True(evidence.Contains("now - started >= LinuxThreadExecutionBudget.RuntimeNanoseconds", StringComparison.Ordinal));
    }

    [HostTest("disk race packages are canonical deterministic two-worker cooperative children")]
    private static void Packages()
    {
        foreach (string variant in F.Variants)
        {
            byte[] package = F.CreatePackage(variant), elf = F.CreateElf(variant);
            var view = LinuxElfPackage.Validate(package);
            HostCheck.True(view.Succeeded && view.Manifest.Threads == 2 && view.Manifest.Descriptors == 16);
            HostCheck.Equal(variant, F.PackageVariant(package));
            HostCheck.True(package.SequenceEqual(F.CreatePackage(variant)) && elf.SequenceEqual(F.CreateElf(variant)));
            HostCheck.True(package.AsSpan((int)view.PayloadOffset, (int)view.PayloadBytes).SequenceEqual(elf));
            string ascii = Encoding.ASCII.GetString(package, (int)view.ManifestOffset, (int)view.ManifestBytes);
            HostCheck.True(ascii.Contains("app.path=apps/m16diskrace.elf\n", StringComparison.Ordinal) &&
                ascii.Contains("args=DiskRace" + variant + "\n", StringComparison.Ordinal) && !ascii.Contains("CLROS_USER_IRQS", StringComparison.Ordinal));
            byte[] changed = (byte[])package.Clone(); changed[^1] ^= 1; Reject(() => F.PackageVariant(changed));
            Reject(() => F.PackageVariant(package.AsSpan(0, package.Length - 1)));
        }
        HostCheck.True(!F.CreatePackage("Progress").SequenceEqual(F.CreatePackage("CloseReuse")));
        Reject(() => F.CreateElf("progress")); Reject(() => F.Calls("unknown"));
        Reject(() => F.PackageVariant(LinuxDiskFileProofFixture.CreatePackage()));
        HostCheck.Equal(59U, LinuxDiskFileProofFixture.ExpectedCalls);
    }

    [HostTest("disk race exact instruction vectors agree with independent finite x64 decoding")]
    private static void Instructions()
    {
        foreach (string variant in F.Variants)
        {
            byte[] elf = F.CreateElf(variant);
            var main = Decode(elf, F.EntryOffset, checked((int)(F.MainEndRip(variant) - F.ImageBase)));
            var child = Decode(elf, F.ChildOffset, checked((int)(F.ChildEndRip(variant) - F.ImageBase)));
            HostCheck.True(F.InstructionRips(variant).SequenceEqual(main.Starts));
            ulong clone = F.CloneReturnRip(variant);
            // Decode the actual child's inherited clone continuation through
            // its JE, rather than assuming every Writer.Op is an instruction.
            var inherited = Decode(elf, (int)(clone - F.ImageBase), (int)(clone - F.ImageBase + 21));
            HostCheck.Equal(4, inherited.Starts.Length);
            HostCheck.True(F.InstructionRips(variant, true).SequenceEqual(inherited.Starts.Concat(child.Starts)));
            HostCheck.True(F.InstructionRips(variant).All(r => r < F.ImageBase + F.ChildOffset));
            HostCheck.True(F.InstructionRips(variant, true).All(r => r < F.ImageBase + F.FailureOffset));
            HostCheck.True(F.InstructionRips(variant).Contains(clone) && F.InstructionRips(variant, true).Contains(clone));
            foreach (bool isChild in new[] { false, true })
            {
                var decoded = isChild ? child.Calls : main.Calls; var expected = F.Calls(variant, isChild);
                HostCheck.Equal(expected.Count, decoded.Length);
                for (int i = 0; i < decoded.Length; i++)
                {
                    var a = expected[i]; var b = decoded[i];
                    HostCheck.True(a.Number == b.Number && a.A0 == b.A0 && a.A1 == b.A1 && a.A2 == b.A2 &&
                        a.A3 == b.A3 && a.A4 == b.A4 && a.A5 == b.A5 && a.ReturnRip == b.Rip);
                    HostCheck.True(F.InstructionRips(variant, isChild).Contains(a.ReturnRip));
                    if (a.Number is not (56 or 60 or 202 or 231) && !(variant == "ExitPending" && a.Role == "main.read"))
                    {
                        int at = (int)(a.ReturnRip - F.ImageBase);
                        HostCheck.True(elf[at] == 0x48 && elf[at + 1] == 0x3d);
                        HostCheck.Equal(a.Result, (long)BinaryPrimitives.ReadInt32LittleEndian(elf.AsSpan(at + 2)));
                    }
                }
            }
            int cloneBranch = (int)(clone - F.ImageBase) + 15;
            HostCheck.True(elf[cloneBranch] == 0x0f && elf[cloneBranch + 1] == 0x84);
            HostCheck.Equal(F.ChildOffset, cloneBranch + 6 + BinaryPrimitives.ReadInt32LittleEndian(elf.AsSpan(cloneBranch + 2)));
            HostCheck.True(elf[(int)(F.FailureExitRip - F.ImageBase) - 2] == 0x0f && elf[(int)(F.FailureExitRip - F.ImageBase) - 1] == 5);
            HostCheck.True(!F.InstructionRips(variant).Contains(F.FailureExitRip));
        }
    }

    [HostTest("disk race metadata names disjoint sentinels stacks clone TIDs and exact progress or close operations")]
    private static void Ownership()
    {
        HostCheck.Equal(0x404000UL, F.Memory.ParentBuffer); HostCheck.Equal(8192U, F.Memory.ParentBytes);
        HostCheck.Equal(0x406000UL, F.Memory.ChildBuffer); HostCheck.Equal(4096U, F.Memory.ChildBytes);
        HostCheck.True(F.Memory.ChildStack <= F.Memory.ParentBuffer && F.ChildTls + 4096 <= F.Memory.ChildStack - 4096);
        foreach (string variant in F.OriginalVariants)
        {
            byte[] elf = F.CreateElf(variant);
            HostCheck.True(elf.AsSpan((int)(F.ParentOutput - F.ImageBase), 8192).IndexOfAnyExcept((byte)0xa5) < 0 &&
                elf.AsSpan((int)(F.ChildOutput - F.ImageBase), 4096).IndexOfAnyExcept((byte)0xa5) < 0);
            HostCheck.Equal(2U, BinaryPrimitives.ReadUInt32LittleEndian(elf.AsSpan((int)(F.ClearTid - F.ImageBase))));
            HostCheck.Equal(0x45434152U, BinaryPrimitives.ReadUInt32LittleEndian(elf.AsSpan((int)(F.Payload - F.ImageBase))));
            var main = F.Calls(variant); var child = F.Calls(variant, true); var clone = main.Single(c => c.Number == 56);
            HostCheck.True(clone.A0 == 0x7d0f00 && clone.A1 == F.ChildStackTop && clone.A2 == F.ParentTid && clone.A3 == F.ClearTid && clone.A4 == F.ChildTls && clone.Result == 2);
            HostCheck.True(main.Single(c => c.Role == "main.read") is { Number: 0, A0: 3, A2: 8192, OutputBytes: 8192 });
            HostCheck.True(child.Single(c => c.Role == "child.pin") is { Number: 10, A0: F.ParentOutput, A1: 4096, A2: 1, Result: -12 });
            HostCheck.True(child.Single(c => c.Role == "child.disjoint-writable") is { Number: 10, A0: F.ChildOutput, A2: 3, Result: 0 });
            HostCheck.True(child[^1] is { Number: 60, A0: 0 } && main[^1] is { Number: 231, A0: 42 });
            HostCheck.True(main.Single(c => c.Role == "main.join") is { Number: 202, A0: F.ClearTid, A2: 2, Wait: F.WaitPolicy.Join });
            if (variant == "CloseReuse")
            {
                HostCheck.Equal(-9L, main.Single(c => c.Role == "main.read").Result);
                HostCheck.True(child.Single(c => c.Role == "child.reopen") is { Number: 2, A0: F.EmptyPath, Result: 3 });
                HostCheck.Equal(0L, main.Single(c => c.Role == "main.replacement").Result);
            }
            else
            {
                HostCheck.Equal(8192L, main.Single(c => c.Role == "main.read").Result);
                HostCheck.True(child.Single(c => c.Role == "child.duplicate") is { Number: 0, A0: 7, A2: 8, Result: 8 });
                HostCheck.True(child.Single(c => c.Role == "child.pread") is { Number: 17, A3: 65535, Result: 512 });
                HostCheck.Equal(8200L, main.Single(c => c.Role == "main.cursor").Result);
            }
        }
    }

    [HostTest("disk lifecycle fixtures encode exact signal handlers EOF winner and nonreturning pending exit without timing loops")]
    private static void LifecycleVariants()
    {
        foreach (string variant in F.LifecycleVariants)
        {
            byte[] elf = F.CreateElf(variant); var main = F.Calls(variant); var child = F.Calls(variant, true);
            var read = main.Single(c => c.Role == "main.read");
            HostCheck.True(read.Number == 0 && read.A1 == F.ParentOutput && read.A2 == 8192 && read.OutputBytes == 8192 && read.Wait == F.WaitPolicy.Disk);
            if (variant == "ExitPending")
            {
                HostCheck.True(main[^1].Role == "main.read" && child[^1] is { Role: "child.group-exit", Number: 231, A0: 42 });
                HostCheck.True(F.HandlerCalls(variant).Count == 0 && F.HandlerInstructionRips(variant).Count == 0);
                HostCheck.Equal((byte)0xe9, elf[(int)(read.ReturnRip - F.ImageBase)]);
                continue;
            }
            HostCheck.True(child[0] is { Role: "child.signal", Number: 234, A0: 1, A1: 1, A2: 10 } && child.Count == 2 && child[^1].Number == 60);
            HostCheck.True(main.Single(c => c.Role == "prep.signal") is { Number: 13, A0: 10, A1: F.SignalAction, A3: 8 });
            HostCheck.Equal(0x04000004UL, BinaryPrimitives.ReadUInt64LittleEndian(elf.AsSpan((int)(F.SignalAction - F.ImageBase + 8))));
            HostCheck.Equal(F.ImageBase + F.HandlerOffset, BinaryPrimitives.ReadUInt64LittleEndian(elf.AsSpan((int)(F.SignalAction - F.ImageBase))));
            HostCheck.Equal(F.ImageBase + F.RestorerOffset, BinaryPrimitives.ReadUInt64LittleEndian(elf.AsSpan((int)(F.SignalAction - F.ImageBase + 16))));
            HostCheck.True(main.Single(c => c.Role == "main.drain-probe") is { Number: 17, A0: 3, A1: F.ChildOutput, A2: 1, A3: 0, Result: 1 });
            HostCheck.True(read.Result == (variant == "InterruptWins" ? -4 : 0) && read.A0 == (variant == "InterruptWins" ? 3UL : 4UL));
            HostCheck.Equal(variant == "CompletionWins" ? F.EmptyPath : F.Path, main.Single(c => c.Role == "prep.independent").A0);
            var h = Decode(elf, F.HandlerOffset, (int)(F.HandlerEndRip(variant) - F.ImageBase));
            var r = Decode(elf, F.RestorerOffset, (int)(F.RestorerEndRip(variant) - F.ImageBase));
            HostCheck.True(F.HandlerInstructionRips(variant).SequenceEqual(h.Starts.Concat(r.Starts)));
            var expected = F.HandlerCalls(variant); var decoded = h.Calls.Concat(r.Calls).ToArray(); HostCheck.Equal(3, expected.Count);
            HostCheck.True(expected.Select(c => c.Role).SequenceEqual(new[] { "handler.tid", "handler.mask", "handler.return" }));
            for (int i = 0; i < expected.Count; i++) HostCheck.True(expected[i].Number == decoded[i].Number && expected[i].ReturnRip == decoded[i].Rip &&
                expected[i].A0 == decoded[i].A0 && expected[i].A1 == decoded[i].A1 && expected[i].A2 == decoded[i].A2 && expected[i].A3 == decoded[i].A3);
            HostCheck.True(expected[^1].Result == read.Result && expected[^1].Number == 15);
            bool denied = false;
            try { ((IList<F.CallSpec>)expected)[0] = default; } catch (NotSupportedException) { denied = true; }
            HostCheck.True(denied);
        }
    }

    [HostTest("disk race copied metadata cannot mutate later package call or instruction identities")]
    private static void Immutable()
    {
        foreach (string variant in F.Variants)
        {
            var calls = F.Calls(variant); var rips = F.InstructionRips(variant, true);
            var savedCalls = calls.ToArray(); var savedRips = rips.ToArray();
            bool callsRejected = false, ripsRejected = false;
            try { ((IList<F.CallSpec>)calls)[0] = default; } catch (NotSupportedException) { callsRejected = true; }
            try { ((IList<ulong>)rips)[0] = 0; } catch (NotSupportedException) { ripsRejected = true; }
            HostCheck.True(callsRejected && ripsRejected);
            byte[] elf = F.CreateElf(variant), package = F.CreatePackage(variant); Array.Clear(elf); Array.Clear(package);
            HostCheck.True(calls.SequenceEqual(savedCalls) && rips.SequenceEqual(savedRips));
            HostCheck.True(F.Calls(variant).SequenceEqual(savedCalls) && F.InstructionRips(variant, true).SequenceEqual(savedRips));
            HostCheck.True(!ReferenceEquals(calls, F.Calls(variant)) && !ReferenceEquals(rips, F.InstructionRips(variant, true)));
        }
    }

    private static string[] SelectionArguments => ["-BuildEfi", "-EnableM16025Probe", "-LinuxElfPackage", "artifacts/disk-race.pkg",
        "-WebRootFixture", "DiskA", "-WebRootAssets", "artifacts/published-webroot"];

    [HostTest("disk race selection explicitly binds exact variants while preserving canonical and disk static defaults")]
    private static void Selection()
    {
        var original = QemuSmokeOptions.Parse(SelectionArguments); var originalPlan = QemuProfiles.Create(original);
        HostCheck.Equal("", original.LinuxDiskRaceVariant);
        HostCheck.Equal(59U, LinuxDiskFileProofFixture.ExpectedCalls);
        foreach (string variant in F.Variants)
        {
            var options = QemuSmokeOptions.Parse([..SelectionArguments, "-LinuxDiskRaceVariant", variant]);
            var plan = QemuProfiles.Create(options);
            HostCheck.Equal(variant, options.LinuxDiskRaceVariant);
            HostCheck.True(NativeAotDiskSelection.Selected(plan.Flags) && !NativeAotDiskSelection.StaticSelected(plan.Flags) &&
                !plan.IsEnabled("EnableM10017Probe") && options.TimeoutSeconds == 180 &&
                plan.Flags.OrderBy(p => p.Key, StringComparer.Ordinal).SequenceEqual(originalPlan.Flags.OrderBy(p => p.Key, StringComparer.Ordinal)) &&
                plan.Required.Where(m=>m!="M16_025_EXIT_CLEANUP_END").SequenceEqual(originalPlan.Required));
            HostCheck.Equal(variant=="ExitPending",plan.Required.Contains("M16_025_EXIT_CLEANUP_END"));
            HostCheck.Equal(variant=="ExitPending",plan.Unique.Contains("M16_025_EXIT_CLEANUP_END"));
        }
        string[] staticArguments = ["-BuildEfi", "-EnableM10018Probe", "-LinuxElfPackage", "artifacts/static.pkg",
            "-LinuxThreadingDisassembly", "artifacts/static.dump", "-WebRootFixture", "DiskA", "-WebRootAssets", "artifacts/webroot"];
        var diskStatic = QemuSmokeOptions.Parse(staticArguments); var staticPlan = QemuProfiles.Create(diskStatic);
        HostCheck.True(diskStatic.LinuxDiskRaceVariant == "" && NativeAotDiskSelection.StaticSelected(staticPlan.Flags) && diskStatic.TimeoutSeconds == 825);
        Reject(() => QemuProfiles.Create(QemuSmokeOptions.Parse([..staticArguments, "-LinuxDiskRaceVariant", "Progress"])));
        Reject(() => QemuProfiles.Create(QemuSmokeOptions.Parse(["-BuildEfi", "-LinuxDiskRaceVariant", "Progress"])));
        foreach (string[] collision in new[]
        {
            new[] { "-LinuxThreadingApplication", "NativeAotSocketSmoke" }, new[] { "-LinuxThreadingApplication", "" },
            new[] { "-LinuxThreadingDisassembly", "other.dump" }, new[] { "-LinuxThreadingDisassembly", "" },
            new[] { "-LinuxHelloDisassembly", "other.dump" }, new[] { "-LinuxHelloDisassembly", "" },
            new[] { "-SignalEpollRace" }, new[] { "-WebSignalRace" }, new[] { "-WebEventOwnedSignalRace" },
            new[] { "-ServiceLifecycle" }, new[] { "-ServiceQuotaFailure" }, new[] { "-EnableM10017Probe" },
            new[] { "-EnableM10018Probe" }, new[] { "-EnableM16013Probe" }, new[] { "-EnableM16014Probe" },
            new[] { "-GptImageVariant", "PrimaryHeaderCrcFault" }, new[] { "-PackageImageVariant", "DigestFault" },
            new[] { "-LinuxElfImageVariant", "Oversized" }, new[] { "-DiskFormat", "qcow2" },
            new[] { "-EnableM16025Probe:false" }
        }) Reject(() => QemuProfiles.Create(QemuSmokeOptions.Parse([..SelectionArguments, "-LinuxDiskRaceVariant", "Progress", ..collision])));
        var changed = QemuSmokeOptions.Parse(SelectionArguments); changed.LinuxDiskRaceVariant = "unknown";
        Reject(() => QemuProfiles.Create(changed));
        var empty = QemuSmokeOptions.Parse(SelectionArguments); empty.Explicit.Add("LinuxDiskRaceVariant");
        Reject(() => QemuProfiles.Create(empty));
    }

    [HostTest("disk race option parsing rejects unknown empty missing duplicate and misspelled selections")]
    private static void Options()
    {
        void Invalid(params string[] arguments)
        { bool rejected = false; try { QemuSmokeOptions.Parse(arguments); } catch (ArgumentException) { rejected = true; } HostCheck.True(rejected); }
        foreach (string variant in F.Variants)
            HostCheck.Equal(variant, QemuSmokeOptions.Parse("--linux-disk-race-variant=" + variant).LinuxDiskRaceVariant);
        foreach (string value in new[] { "", "progress", "Progress ", " Progress", "Unknown", "DiskFiles", "Progress,CloseReuse" })
            Invalid("-LinuxDiskRaceVariant=" + value);
        Invalid("-LinuxDiskRaceVariant");
        Invalid("-LinuxDiskRaceVariants", "Progress");
        Invalid("-LinuxDiskRaceVariant", "Progress", "--linux-disk-race-variant=CloseReuse");
        Invalid("-LinuxDiskRaceVariant=Progress", "-LinuxDiskRaceVariant:Progress");
    }

    [HostTest("unconnected disk race variants refuse original execution before any child launches")]
    private static async Task OriginalOwnerRequired()
    {
        foreach(string variant in F.Variants.Where(v=>v!="ExitPending"))
        {
            bool launched=false;using StringWriter output=new();
            var options=QemuSmokeOptions.Parse([..SelectionArguments,"-LinuxDiskRaceVariant",variant]);
            int exit=await QemuSmoke.RunAsync(HostToolProcess.FindRepositoryRoot(),options,output,(_,_)=>
            {launched=true;throw new InvalidOperationException("race owner must refuse before a child launches");});
            HostCheck.True(exit==1 && !launched && output.ToString().Contains(
                "TEST_FAIL qemu.smoke disk_race_complete_consumer_required",StringComparison.Ordinal));
        }
    }

    [HostTest("disk mechanical package selection requires exact canonical bytes for its separate original consumer")]
    private static void OriginalPackageSelection()
    {
        byte[] ordinary=LinuxDiskFileProofFixture.CreatePackage(),pending=F.CreatePackage("ExitPending");
        NativeAotDiskSelection.RequireMechanicalPackage(ordinary,"");
        NativeAotDiskSelection.RequireMechanicalPackage(pending,"ExitPending");
        void Deny(byte[] package,string variant)
        {
            bool denied=false;try{NativeAotDiskSelection.RequireMechanicalPackage(package,variant);}catch(ToolFailure){denied=true;}
            HostCheck.True(denied);
        }
        Deny(ordinary,"ExitPending");Deny(pending,"");
        foreach(string variant in F.Variants.Where(v=>v!="ExitPending"))
        {Deny(F.CreatePackage(variant),variant);Deny(F.CreatePackage(variant),"ExitPending");}
        byte[] altered=pending.ToArray();altered[^1]^=1;Deny(altered,"ExitPending");
        Deny(pending,"Unknown");
    }

    [HostTest("disk race package command publishes exact fresh artifact bytes and preserves rejected destinations")]
    private static void Publication()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string directory = Path.Combine(root, "artifacts", "disk-race-package-host-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        foreach (string variant in F.Variants)
        {
            string path = Path.Combine(directory, variant + ".pkg");
            if (variant == "Progress")
            {
                var original=Console.Out;using StringWriter command=new();int exit;
                try{Console.SetOut(command);exit=ToolCommand.Run(["linux","disk-race-package",variant,path]);}
                finally{Console.SetOut(original);}
                HostCheck.Equal(0,exit);
                HostCheck.True(command.ToString().Contains("TEST_PASS linux.disk-race.package variant=Progress fixture_only=true",StringComparison.Ordinal));
            }
            else ToolCommand.WriteDiskRacePackage(root, variant, Path.GetRelativePath(root, path));
            byte[] bytes = File.ReadAllBytes(path);
            HostCheck.True(bytes.SequenceEqual(F.CreatePackage(variant)) && F.PackageVariant(bytes) == variant);
            bool rejected = false;
            try { ToolCommand.WriteDiskRacePackage(root, "CloseReuse", path); } catch (IOException) { rejected = true; }
            HostCheck.True(rejected && File.ReadAllBytes(path).SequenceEqual(bytes));
        }
        string outside = Path.Combine(root, "artifacts", "..", "disk-race-outside-" + Guid.NewGuid().ToString("N") + ".pkg");
        Reject(() => ToolCommand.WriteDiskRacePackage(root, "Progress", outside));
        HostCheck.True(!Path.Exists(Path.GetFullPath(outside)));
        string invalidParent = Path.Combine(directory, "invalid-variant"), invalidOutput = Path.Combine(invalidParent, "bad.pkg");
        Reject(() => ToolCommand.WriteDiskRacePackage(root, "progress", invalidOutput));
        HostCheck.True(!Path.Exists(invalidParent));
    }

    internal readonly record struct DecodedCall(ulong Rip, ulong Number, ulong A0, ulong A1, ulong A2, ulong A3, ulong A4, ulong A5);
    internal static DecodedCall[] Calls(string variant, bool child = false)
    {
        byte[] elf = F.CreateElf(variant);
        return Decode(elf, child ? F.ChildOffset : F.EntryOffset,
            checked((int)((child ? F.ChildEndRip(variant) : F.MainEndRip(variant)) - F.ImageBase))).Calls;
    }
    private static (ulong[] Starts, DecodedCall[] Calls) Decode(byte[] elf, int start, int end)
    {
        List<ulong> starts = []; List<DecodedCall> calls = []; ulong[] registers = new ulong[16];
        for (int at = start; at < end;)
        {
            starts.Add(F.ImageBase + (uint)at); ReadOnlySpan<byte> code = elf.AsSpan(at); int bytes;
            if (code[0] is >= 0xb8 and <= 0xbf)
            { registers[code[0] - 0xb8] = BinaryPrimitives.ReadUInt32LittleEndian(code[1..]); bytes = 5; }
            else if (code[0] == 0x41 && code[1] is >= 0xb8 and <= 0xbf)
            { registers[8 + code[1] - 0xb8] = BinaryPrimitives.ReadUInt32LittleEndian(code[2..]); bytes = 6; }
            else if (code.StartsWith(new byte[] { 0x0f, 5 }))
            { calls.Add(new(F.ImageBase + (uint)at + 2, registers[0], registers[7], registers[6], registers[2], registers[10], registers[8], registers[9])); bytes = 2; }
            else if (code.StartsWith(new byte[] { 0x48, 0x3d })) bytes = 6;
            else if (code.StartsWith(new byte[] { 0x48, 0x85, 0xc0 })) bytes = 3;
            else if (code.StartsWith(new byte[] { 0x0f, 0xb6, 5 })) bytes = 7;
            else if (code.StartsWith(new byte[] { 0x8b, 5 })) bytes = 6;
            else if (code.StartsWith(new byte[] { 0x48, 0x8b, 5 })) bytes = 7;
            else if (code.StartsWith(new byte[] { 0xc7, 5 }))
            {
                bytes = 10; int target = at + bytes + BinaryPrimitives.ReadInt32LittleEndian(code[2..]);
                HostCheck.Equal((int)(F.SignalCount - F.ImageBase), target); HostCheck.Equal(1U, BinaryPrimitives.ReadUInt32LittleEndian(code[6..]));
            }
            else if (code.StartsWith(new byte[] { 0x48, 0x89, 0xf8 }) || code.StartsWith(new byte[] { 0x48, 0x8b, 2 }) || code.StartsWith(new byte[] { 0x8b, 0x46, 0x10 })) bytes = 3;
            else if (code.StartsWith(new byte[] { 0x8b, 6 })) bytes = 2;
            else if (code.StartsWith(new byte[] { 0x48, 0x63, 0x46, 8 })) bytes = 4;
            else if (code.StartsWith(new byte[] { 0x48, 0x8b, 0x82 })) bytes = 7;
            else if (code[0] == 0x0f && code[1] is 0x84 or 0x85)
            { bytes = 6; int target = at + 6 + BinaryPrimitives.ReadInt32LittleEndian(code[2..]); HostCheck.True(target == F.FailureOffset || target == F.ChildOffset || target > at && target < F.ChildOffset); }
            else if (code[0] == 0xe9)
            { bytes = 5; HostCheck.Equal(F.FailureOffset, at + 5 + BinaryPrimitives.ReadInt32LittleEndian(code[1..])); HostCheck.Equal(end, at + 5); }
            else if (code[0] == 0xfc) bytes = 1;
            else if (code[0] == 0xc3) bytes = 1;
            else if (code.StartsWith(new byte[] { 0x0f, 0x0b })) bytes = 2;
            else if (code.StartsWith(new byte[] { 0xf3, 0xae })) bytes = 2;
            else throw new InvalidOperationException("unsupported disk race opcode " + at.ToString("x"));
            HostCheck.True(bytes > 0 && at + bytes <= end); at += bytes;
        }
        return (starts.ToArray(), calls.ToArray());
    }
    private static void Reject(Action action)
    { bool rejected = false; try { action(); } catch (ToolFailure) { rejected = true; } HostCheck.True(rejected); }
}
