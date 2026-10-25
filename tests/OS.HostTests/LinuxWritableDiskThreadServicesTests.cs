using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Threading;
using OS.Pal;
using OS.Storage;
using W = OS.Kernel.Compatibility.LinuxX64.LinuxContinuingWritableDescriptorServices;
using F = OS.Kernel.Compatibility.LinuxX64.LinuxDescriptorServices;

internal static unsafe class LinuxWritableDiskThreadServicesTests
{
    private const ulong Key = LinuxThreadServicesTests.Key, User = LinuxThreadServicesTests.UserBase;
    private const ulong Path = User + 0x1200, Vector = User + 0x1400, Output = User + 0x2000, Payload = User + 0x3000;

    [HostTest("writable source5 actual SQLite sized synthetic files use explicit engine work and search admissions through retained waits", HostLane.Native)]
    private static void SqliteFootprintWaitBudgets()
    {
        foreach (uint work in new uint[] { 100000, 1000000 })
        {
            using var f = new Fixture(sqliteFootprints: true, workBudget: work, search: work == 1000000 ? new W.FreeSearch(2, 4096, 8192) : null);
            f.Trace = []; uint initialParks = f.Parked;
            foreach (var file in LinuxWritableDiskDescriptorServicesTests.SqliteFootprints())
            {
                uint copies = f.Thread.Writes, hardware = f.Storage.Device.Hardware->Notifies; int trace = f.Trace.Count;
                f.PutPath("/data/" + file.Name); long opened = f.Run(2, Path);
                if (work == 100000 && file.Clusters > 80)
                {
                    HostCheck.Equal(-75L, opened); HostCheck.True(!F.Capture(f.Files.State, 3, out _) && copies == f.Thread.Writes &&
                        f.Storage.Device.Hardware->Notifies > hardware && f.Trace.Skip(trace).All(row => row.Type == 0));
                }
                else
                {
                    HostCheck.True(opened >= 3); uint fd = (uint)opened;
                    HostCheck.Equal(0L, f.Run(5, fd, Output));
                    HostCheck.True(BinaryPrimitives.ReadUInt64LittleEndian(new ReadOnlySpan<byte>(f.Thread.At(Output) + 48, 8)) == (ulong)file.Bytes &&
                        BinaryPrimitives.ReadUInt64LittleEndian(new ReadOnlySpan<byte>(f.Thread.At(Output) + 64, 8)) == file.Clusters);
                    ulong offset = (ulong)file.Bytes - 8192; HostCheck.Equal(8192L, f.Run(17, fd, Output, 8192, offset));
                    HostCheck.True(new ReadOnlySpan<byte>(f.Thread.At(Output), 8192).SequenceEqual(
                        LinuxWritableDiskDescriptorServicesTests.FootprintBytes(8192, file.Salt, (int)offset)));
                    HostCheck.Equal(0L, f.Run(8, fd, 0, 1)); HostCheck.Equal(0L, f.Run(3, fd));
                }
                HostCheck.True(!f.Thread.View(f.Main).Wait.IsValid && !f.Disk->RequestActive && !f.Provider->RequestActive &&
                    !f.Storage.State->RequestActive && !f.Storage.State->Dirty && F.Audit(f.Files.State));
            }
            HostCheck.True(f.Parked > initialParks && f.Storage.Device.State->SubmittedBytes == 0 && f.Trace.All(row => row.Type == 0));
            var ordinary = LinuxWritableDiskDescriptorServicesTests.SqliteFootprints()[0];
            uint writing = f.Open("/data/" + ordinary.Name, 2); byte[] payload = LinuxWritableDiskDescriptorServicesTests.FootprintBytes(8192, 83);
            f.PutWrite(payload); uint outputs = f.Thread.Writes;
            HostCheck.Equal(work == 1000000 ? 8192L : -11L, f.Run(328, writing, Vector, 1, 40960, 0, W.RwfNoAppend));
            HostCheck.True(outputs == f.Thread.Writes && f.Run(8, writing, 0, 1) == 0 && !f.Disk->RequestActive && !f.Provider->RequestActive && !f.Storage.State->Dirty);
            if (work == 1000000)
            {
                HostCheck.True(f.Trace.Any(row => row.Type == 1) && f.Trace.Any(row => row.Type == 4));
                ulong submitted = f.Storage.Device.State->Submitted; HostCheck.Equal(0L, f.Run(74, writing));
                HostCheck.True(f.Storage.Device.State->Submitted == submitted + 1 && f.Storage.Device.State->Completed == submitted + 1 &&
                    f.Storage.Device.State->DurableBytes == f.Storage.Device.State->SubmittedBytes);
            }
            else HostCheck.True(f.Storage.Device.State->SubmittedBytes == 0 && f.Trace.All(row => row.Type == 0));
            HostCheck.Equal(0L, f.Run(3, writing)); writing = f.Open("/data/" + ordinary.Name);
            byte[] expected = LinuxWritableDiskDescriptorServicesTests.FootprintBytes(ordinary.Bytes, ordinary.Salt);
            if (work == 1000000) expected = [.. expected, .. payload];
            byte[] extracted = new byte[expected.Length]; var read = Fat32Parser.ReadRootFile(new PersistentDevice(f.Storage.Device.Hardware),
                f.Storage.Input.PartitionFirst, ordinary.Name, extracted);
            HostCheck.True(read.Succeeded && read.BytesRead == (ulong)expected.Length && extracted.AsSpan().SequenceEqual(expected));
            HostCheck.Equal(0L, f.Run(3, writing));
        }
    }

    [HostTest("writable attachment before thread binding rejects a different registered primary PID without effects")]
    private static void WritableFirstPidBinding() => PidBinding(writableFirst: true);

    [HostTest("writable attachment after thread binding rejects a different registered primary PID without effects")]
    private static void ThreadFirstPidBinding() => PidBinding(writableFirst: false);

    private static void PidBinding(bool writableFirst)
    {
        using (var wrong = new Fixture(primaryPid: 202, deferBinding: true))
        {
            HostCheck.Equal(0U, wrong.Files.Admission.PackageProcessId);
            HostCheck.True(writableFirst ? wrong.AttachWritable() : wrong.EnableThreads());
            byte[] files = new ReadOnlySpan<byte>(wrong.Files.State, sizeof(F.State)).ToArray();
            byte[] disk = new ReadOnlySpan<byte>(wrong.Disk, sizeof(F.WritableDiskState)).ToArray();
            var kernel = wrong.Thread.Kernel.Snapshot(); uint calls = wrong.Storage.Device.Hardware->Calls;
            HostCheck.True(!(writableFirst ? wrong.EnableThreads() : wrong.AttachWritable()), "copied PID202 must not bind to actual process101");
            wrong.Thread.Kernel.Same(kernel);
            HostCheck.True(files.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(wrong.Files.State, sizeof(F.State))) &&
                disk.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(wrong.Disk, sizeof(F.WritableDiskState))) &&
                calls == wrong.Storage.Device.Hardware->Calls);
        }
        foreach (uint packagePid in new uint[] { 0, 101 })
        {
            using var good = new Fixture(deferBinding: true, packagePid: packagePid);
            var secondary = W.RegisterProcess(good.Provider, 202);
            HostCheck.True(secondary.Value == 0 && secondary.Process.Pid == 202 && W.PrimaryProcess(good.Provider).Pid == 101);
            HostCheck.True(writableFirst ? good.AttachWritable() : good.EnableThreads());
            HostCheck.True(writableFirst ? good.EnableThreads() : good.AttachWritable());
            good.Start(); uint fd = good.Open(); HostCheck.Equal(0L, good.Run(8, fd, 0, 1)); HostCheck.Equal(0L, good.Run(3, fd));
            HostCheck.Equal(0L, W.ExitProcess(good.Provider, secondary.Process).Value);
        }
    }

    [HostTest("writable attachment rejects a mismatching explicit package PID before thread publication")]
    private static void PackagePidBinding()
    {
        using var f = new Fixture(primaryPid: 202, packagePid: 101, deferBinding: true);
        byte[] state = new ReadOnlySpan<byte>(f.Files.State, sizeof(F.State)).ToArray();
        uint calls = f.Storage.Device.Hardware->Calls;
        HostCheck.True(!f.AttachWritable() && state.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(f.Files.State, sizeof(F.State))) &&
            calls == f.Storage.Device.Hardware->Calls && !f.Disk->RequestActive && !f.Provider->RequestActive);
    }

    [HostTest("writable source5 copied GETLK returns a foreign registered owner with exact x64 fields beyond EOF without changing locks or padding")]
    private static void ForeignLockWire()
    {
        // The secondary owner is an explicit host registration, not a second
        // Linux process. Global calls below run on the actual modeled PID101.
        using var f = new Fixture(); uint first = f.Open(flags: 2), second = f.Open();
        var foreign = W.RegisterProcess(f.Provider, 202).Process;
        var opened = f.ProviderResult(W.OpenForProcess(f.Provider, foreign, "/data/DATA.BIN"u8, 2));
        HostCheck.True(opened.Phase == W.Progress.Immediate && opened.Value >= 3);
        const long reserved = 0x40000001; uint parks = f.Parked, calls = f.Storage.Device.Hardware->Calls;
        try
        {
            HostCheck.Equal(0L, W.FcntlLockForProcess(f.Provider, foreign, opened.Descriptor, 6, new(1, 0, reserved, 1)).Value);
            byte[] input = PutLock(); uint copies = f.Thread.Writes;
            HostCheck.Equal(0L, f.Run(72, first, 5, Output));
            byte[] expected = (byte[])input.Clone(); BinaryPrimitives.WriteInt64LittleEndian(expected.AsSpan(8), reserved);
            BinaryPrimitives.WriteInt64LittleEndian(expected.AsSpan(16), 1); BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(24), 202);
            HostCheck.True(Wire().SequenceEqual(expected)); HostCheck.Equal(copies + 1, f.Thread.Writes);
            PutLock(); HostCheck.Equal(-11L, f.Run(72, first, 6, Output)); HostCheck.True(Wire().SequenceEqual(input));
            f.Thread.FailWrite(Output);
            try { HostCheck.Equal(-14L, f.Run(72, first, 5, Output)); HostCheck.True(Wire().SequenceEqual(input)); }
            finally { f.Thread.FailWrite(ulong.MaxValue); }
            HostCheck.Equal(0L, f.Run(3, second)); // Closing PID101 cannot release PID202's lock.
            PutLock(); HostCheck.Equal(0L, f.Run(72, first, 5, Output)); HostCheck.True(Wire().SequenceEqual(expected));
            HostCheck.Equal(0L, W.ExitProcess(f.Provider, foreign).Value);
            var replacement = W.RegisterProcess(f.Provider, 202).Process;
            HostCheck.True(replacement.Index == foreign.Index && replacement.Generation != foreign.Generation);
            HostCheck.Equal(-9L, W.FcntlLockForProcess(f.Provider, foreign, opened.Descriptor, 6, new(1, 0, reserved, 1)).Value);
            PutLock(); HostCheck.Equal(0L, f.Run(72, first, 5, Output)); input[0] = 2;
            HostCheck.True(Wire().SequenceEqual(input)); // No conflict changes only l_type, retaining input PID9876.
            HostCheck.Equal(0L, W.ExitProcess(f.Provider, replacement).Value);
            HostCheck.True(F.Audit(f.Files.State) && !f.HasLocks() && f.Parked == parks && calls == f.Storage.Device.Hardware->Calls);
            HostCheck.Equal(0L, f.Run(3, first));
        }
        finally { W.ExitProcess(f.Provider, foreign); }

        byte[] PutLock()
        {
            Span<byte> wire = new(f.Thread.At(Output), 32); wire.Fill(0xa7);
            BinaryPrimitives.WriteInt16LittleEndian(wire, 1); BinaryPrimitives.WriteInt16LittleEndian(wire[2..], 0);
            BinaryPrimitives.WriteInt64LittleEndian(wire[8..], 0x40000000); BinaryPrimitives.WriteInt64LittleEndian(wire[16..], 512);
            BinaryPrimitives.WriteInt32LittleEndian(wire[24..], 9876); return wire.ToArray();
        }
        ReadOnlySpan<byte> Wire() => new(f.Thread.At(Output), 32);
    }

    [HostTest("writable source5 actual waits drive all28 measured descriptor calls with original wire values and independent file bytes")]
    private static void MechanicalCalls()
    {
        using var f = new Fixture(); byte[] elf = OS.Tooling.LinuxWritableDiskProofFixture.CreateElf();
        elf.CopyTo(new Span<byte>(f.Thread.At(User), elf.Length)); int completed = 0;
        foreach (var call in OS.Tooling.LinuxWritableDiskProofFixture.Calls())
        {
            if (call.Number is 1 or 231) continue;
            HostCheck.Equal(call.Result, f.Run(call.Number, call.A0, call.A1, call.A2, call.A3, call.A4, call.A5)); completed++;
            if (call.Role == "read.reopened")
            {
                byte[] expected = OS.Tooling.LinuxWritableDiskProofFixture.ExpectedPayload();
                HostCheck.True(new ReadOnlySpan<byte>(f.Thread.At(OS.Tooling.LinuxWritableDiskProofFixture.Output), expected.Length).SequenceEqual(expected));
            }
        }
        HostCheck.True(completed == 28 && f.Parked > 8 && !f.Disk->RequestActive && !f.Provider->RequestActive);
    }

    [HostTest("writable source5 decoded log upload frames park wake and consume exact translated inputs with four real flush barriers and no late user copy")]
    private static void LogUploadCalls()
    {
        // Copied instruction/data vectors drive the real host wait services;
        // this does not execute the ELF/BCL or establish physical persistence.
        using var f = new Fixture(); f.Trace = [];
        byte[] elf = OS.Tooling.LinuxWritableDiskProofFixture.CreateLogUploadElf();
        byte[] log = OS.Tooling.NativeAotLogUploadContract.ExpectedLog(), upload = OS.Tooling.NativeAotLogUploadContract.ExpectedUpload();
        var specs = OS.Tooling.LinuxWritableDiskProofFixture.LogUploadCalls(); HostCheck.Equal(40, specs.Count);
        var media = new PersistentDevice(f.Storage.Device.Hardware);
        var geometry = Fat32Parser.ReadBpb(media, f.Storage.Input.PartitionFirst); HostCheck.True(geometry.Succeeded);
        var bpb = geometry.Bpb; HostCheck.Equal((byte)1, bpb.SectorsPerCluster);
        byte[] oldFat = f.Storage.Persistent.Slice((int)(bpb.FirstFatLba * 512), (int)bpb.FatSizeSectors * 512).ToArray();
        byte[] oldEntries = f.Storage.Persistent.Slice((int)(bpb.FirstDataLba * 512), 64).ToArray();
        uint[] guards = [3, 4, 6]; byte[][] guardBytes = guards.Select(cluster =>
            f.Storage.Persistent.Slice((int)((bpb.FirstDataLba + cluster - 2) * 512), 512).ToArray()).ToArray();
        byte[] boot = f.Storage.Persistent.Slice((int)(f.Storage.Input.PartitionFirst * 512), 512).ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(boot.AsSpan(48), 0xffff); // Existing fixture setup, committed by seed FLUSH.
        HostCheck.True(f.Storage.Volatile.Slice((int)(f.Storage.Input.PartitionFirst * 512), 512).SequenceEqual(boot));
        foreach (string name in new[] { "LOG.TXT", "UPLOAD.BIN" })
        {
            HostCheck.Equal(Fat32Status.MissingFile, Fat32Parser.FindRootDirectoryEntry(media, f.Storage.Input.PartitionFirst, name).Status);
            uint fd = f.Open("/data/" + name, 2 | W.Create | W.Exclusive); HostCheck.Equal(0L, f.Run(3, fd));
            var empty = Fat32Parser.FindRootDirectoryEntry(media, f.Storage.Input.PartitionFirst, name);
            HostCheck.True(empty.Succeeded && empty.Entry.FileSize == 0 && empty.Entry.FirstCluster == 0); Independent(name, []);
        }
        HostCheck.True(f.Storage.Persistent.Slice((int)(f.Storage.Input.PartitionFirst * 512), 512).SequenceEqual(boot));
        uint startingParks = f.Parked; int storageCalls = 0, metadataOnly = 0, writes = 0, barriers = 0;
        List<string> barrierRoles = [];
        foreach (var spec in specs)
        {
            ulong[] wire = DecodeLogUploadFrame(elf, spec);
            if (wire[0] == 1)
            {
                HostCheck.True(spec.Role == "witness" && wire[1] == 1 && wire[3] == (ulong)OS.Tooling.LinuxWritableDiskProofFixture.LogUploadWitness.Length &&
                    ElfRange(elf, wire[2], wire[3]).SequenceEqual(Encoding.ASCII.GetBytes(OS.Tooling.LinuxWritableDiskProofFixture.LogUploadWitness)));
                metadataOnly++; continue;
            }
            if (wire[0] == 231) { HostCheck.True(spec.Role == "exit" && wire[1] == 42); metadataOnly++; continue; }
            if (wire[0] == 2)
            {
                ReadOnlySpan<byte> path = ElfRange(elf, wire[1], 128); int zero = path.IndexOf((byte)0); HostCheck.True(zero > 0);
                path[..(zero + 1)].CopyTo(UserRange(f, Path, (uint)zero + 1)); wire[1] = Path;
            }
            else if (wire[0] == 328)
            {
                HostCheck.True(wire[2] == OS.Tooling.LinuxWritableDiskProofFixture.LogUploadIovecs + (uint)writes * 16 &&
                    wire[3] == 1 && wire[5] == 0 && wire[6] == W.RwfNoAppend);
                ReadOnlySpan<byte> vector = ElfRange(elf, wire[2], 16); ulong source = BinaryPrimitives.ReadUInt64LittleEndian(vector), count = BinaryPrimitives.ReadUInt64LittleEndian(vector[8..]);
                HostCheck.True(count == (ulong)spec.Result && count is > 0 and <= 4094);
                ReadOnlySpan<byte> bytes = ElfRange(elf, source, count), expected = writes < 12 ? log.AsSpan(writes * 55, 55) : writes == 12 ? upload.AsSpan(0, 4094) : upload.AsSpan(4094);
                HostCheck.True(bytes.SequenceEqual(expected) && wire[4] == (writes < 12 ? (ulong)writes * 55 : writes == 12 ? 0UL : 4094UL));
                bytes.CopyTo(UserRange(f, Payload, (uint)count)); Span<byte> copied = UserRange(f, Vector, 16);
                BinaryPrimitives.WriteUInt64LittleEndian(copied, Payload); BinaryPrimitives.WriteUInt64LittleEndian(copied[8..], count); wire[2] = Vector; writes++;
            }
            else if (wire[0] == 17)
            {
                HostCheck.True(wire[2] >= OS.Tooling.LinuxWritableDiskProofFixture.LogUploadOutput);
                ulong delta = wire[2] - OS.Tooling.LinuxWritableDiskProofFixture.LogUploadOutput;
                HostCheck.True(delta <= 8192 && wire[3] <= 8192 - delta && spec.OutputBytes == wire[3]);
                if (delta == 0) UserRange(f, Output, 8192).Fill(OS.Tooling.LinuxWritableDiskProofFixture.Sentinel);
                wire[2] = Output + delta; UserRange(f, wire[2], (uint)wire[3]);
            }
            uint copies = f.Thread.Writes, parks = f.Parked; int firstTrace = f.Trace.Count;
            var block = f.Storage.Device.State; ulong submitted = block->Submitted, completed = block->Completed, beforeBytes = block->SubmittedBytes;
            HostCheck.Equal(spec.Result, f.Run(wire[0], wire[1], wire[2], wire[3], wire[4], wire[5], wire[6])); storageCalls++;
            // The authenticated root barrier open is immediate; only its
            // subsequent fsync owns a queued device request/source5 wait.
            bool waits = wire[0] is 17 or 74 or 328 || wire[0] == 2 && spec.Result >= 0 && spec.A0 != OS.Tooling.LinuxWritableDiskProofFixture.RootName;
            HostCheck.Equal(parks + (waits ? 1U : 0U), f.Parked);
            HostCheck.Equal(copies + (wire[0] == 17 && spec.Result > 0 ? 1U : 0U), f.Thread.Writes);
            HostCheck.True(f.Thread.View(f.Main).WaitSource == 0 && !f.Thread.View(f.Main).Wait.IsValid && f.May(Output, 8192) &&
                !f.Disk->RequestActive && !f.Provider->RequestActive && !f.Storage.State->RequestActive && !block->RequestPending);
            if (wire[0] == 328) HostCheck.True(f.Trace.Skip(firstTrace).Any(row => row.Type == 1) && block->SubmittedBytes > beforeBytes && !f.Storage.State->Dirty);
            if (wire[0] == 74)
            {
                HostCheck.True(f.Trace.Skip(firstTrace).Select(row => row.Type).SequenceEqual(new uint[] { 4 }) && block->Submitted == submitted + 1 &&
                    block->Completed == completed + 1 && block->SubmittedBytes == beforeBytes && block->CompletedBytes == beforeBytes && block->DurableBytes == beforeBytes);
                barriers++; barrierRoles.Add(spec.Role);
                Console.WriteLine($"LOG_UPLOAD_SOURCE5_FLUSH_HOST role={spec.Role} submitted={block->Submitted} completed={block->Completed} writeBytes={beforeBytes} flushAcknowledgedBytes={block->DurableBytes}");
            }
            if (spec.Role == "log.read") HostCheck.True(UserRange(f, Output, 660).SequenceEqual(log) && f.Thread.At(Output)[660] == 0xa5);
            if (spec.Role == "upload.read") HostCheck.True(UserRange(f, Output, 5126).SequenceEqual(upload) && f.Thread.At(Output)[5126] == 0xa5);
            if (spec.Role is "log.eof" or "upload.eof") HostCheck.True(UserRange(f, wire[2], 1)[0] == 0xa5);
            if (wire[0] == 3) HostCheck.True(!F.Capture(f.Files.State, (uint)wire[1], out _));
            copies = f.Thread.Writes; byte[] user = UserRange(f, User + 4096, (uint)(LinuxThreadServicesTests.UserBytes - 4096)).ToArray();
            HostCheck.True(f.Thread.Service->PollWritableDiskReadiness(Key, 2));
            HostCheck.Equal(LinuxSyscallAction.Rejected, f.Thread.Service->CompleteWritableDiskWait(Key, 2).Action);
            HostCheck.True(f.Thread.Writes == copies && UserRange(f, User + 4096, (uint)user.Length).SequenceEqual(user));
        }
        HostCheck.True(storageCalls == 38 && metadataOnly == 2 && writes == 14 && barriers == 4 && f.Parked - startingParks == 27 &&
            barrierRoles.SequenceEqual(new[] { "log.sync.first", "log.sync.second", "upload.sync", "parent.sync" }));
        Independent("LOG.TXT", log); Independent("UPLOAD.BIN", upload);
        Independent("DATA.BIN", Enumerable.Range(0, 900).Select(i => unchecked((byte)(i * 17 + 5))).ToArray()); Independent("KEEP.TXT", new byte[3]);
        var finalFat = f.Storage.Persistent.Slice((int)(bpb.FirstFatLba * 512), oldFat.Length);
        HostCheck.True(finalFat.SequenceEqual(f.Storage.Persistent.Slice((int)((bpb.FirstFatLba + bpb.FatSizeSectors) * 512), oldFat.Length)));
        HashSet<uint> allocated = [];
        foreach (var item in new[] { (Name: "LOG.TXT", Bytes: log.Length), (Name: "UPLOAD.BIN", Bytes: upload.Length) })
        {
            var entry = Fat32Parser.FindRootDirectoryEntry(media, f.Storage.Input.PartitionFirst, item.Name); HostCheck.True(entry.Succeeded && entry.Entry.FileSize == item.Bytes);
            uint cluster = entry.Entry.FirstCluster; int count = (item.Bytes + 511) / 512;
            for (int i = 0; i < count; i++)
            {
                HostCheck.True(cluster >= 2 && cluster < bpb.ClusterCount + 2 && allocated.Add(cluster));
                if (i == count - 1) HostCheck.True(f.Storage.Persistent.Slice((int)((bpb.FirstDataLba + cluster - 2) * 512 + (uint)item.Bytes % 512), 512 - item.Bytes % 512).IndexOfAnyExcept((byte)0) < 0);
                cluster = BinaryPrimitives.ReadUInt32LittleEndian(finalFat[(int)(cluster * 4)..]) & 0x0fffffff;
            }
            HostCheck.True(cluster >= 0x0ffffff8);
        }
        for (int offset = 0; offset < oldFat.Length; offset += 4)
            if (!allocated.Contains((uint)offset / 4)) HostCheck.True(finalFat.Slice(offset, 4).SequenceEqual(oldFat.AsSpan(offset, 4)));
        for (int i = 0; i < guards.Length; i++) HostCheck.True(f.Storage.Persistent.Slice((int)((bpb.FirstDataLba + guards[i] - 2) * 512), 512).SequenceEqual(guardBytes[i]));
        HostCheck.True(f.Storage.Persistent.Slice((int)(bpb.FirstDataLba * 512), 64).SequenceEqual(oldEntries) &&
            f.Storage.Persistent.Slice((int)(f.Storage.Input.PartitionFirst * 512), 512).SequenceEqual(boot));
        HostCheck.True(f.Files.State->ActiveDescriptors == 3 && f.Files.State->RetainedPipeOperations == 0 && F.Audit(f.Files.State) &&
            !f.Disk->RequestActive && !f.Provider->RequestActive && !f.Storage.State->RequestActive && !f.Storage.State->Dirty && !f.Storage.Device.State->RequestPending &&
            f.Storage.Device.State->Submitted == f.Storage.Device.State->Completed && f.Storage.Device.State->SubmittedBytes == f.Storage.Device.State->CompletedBytes &&
            f.Storage.Device.State->CompletedBytes == f.Storage.Device.State->DurableBytes);
        f.Storage.Device.CheckGuards();
        Console.WriteLine($"LOG_UPLOAD_SOURCE5_HOST frames=40 storageCalls={storageCalls} waits={f.Parked - startingParks} positionedWrites={writes} explicitFlushes={barriers} submitted={f.Storage.Device.State->Submitted} completed={f.Storage.Device.State->Completed} writeBytes={f.Storage.Device.State->SubmittedBytes} flushAcknowledgedBytes={f.Storage.Device.State->DurableBytes}");
        void Independent(string name, byte[] expected)
        {
            byte[] actual = new byte[expected.Length]; var read = Fat32Parser.ReadRootFile(media, f.Storage.Input.PartitionFirst, name, actual);
            HostCheck.True(read.Succeeded && read.BytesRead == (ulong)expected.Length && actual.SequenceEqual(expected));
        }
    }

    [HostTest("writable source5 copied SQLite journal lifecycle keeps exact waits locks metadata and independent bytes across actual file and parent barriers")]
    private static void SqliteJournalLifecycle() => SqliteJournalFlow(failHeaderFlush: false);

    [HostTest("writable source5 journal header OUT then failed FLUSH returns EIO once retains dirty backing and cannot replay or copy late")]
    private static void SqliteJournalHeaderFailure() => SqliteJournalFlow(failHeaderFlush: true);

    private static void SqliteJournalFlow(bool failHeaderFlush)
    {
        // Only widths, offsets, lock regions and ordering are copied from
        // original77B2 sqlite-trace.5255:343-364. These independent page bytes
        // are not a SQLite database or a valid recovery journal. Real host
        // services and fake-device IN/OUT/FLUSH run; no ELF/SQLite/guest or
        // physical-persistence authority follows.
        using var f = new Fixture(); f.Trace = [];
        var media = new PersistentDevice(f.Storage.Device.Hardware);
        var geometry = Fat32Parser.ReadBpb(media, f.Storage.Input.PartitionFirst); HostCheck.True(geometry.Succeeded);
        var bpb = geometry.Bpb; HostCheck.Equal((byte)1, bpb.SectorsPerCluster);
        byte[] oldEntries = f.Storage.Persistent.Slice((int)(bpb.FirstDataLba * 512), 64).ToArray();
        byte[] oldFat = f.Storage.Persistent.Slice((int)(bpb.FirstFatLba * 512), (int)bpb.FatSizeSectors * 512).ToArray();
        uint[] protectedClusters = [3, 4, 6];
        byte[][] protectedData = protectedClusters.Select(cluster => f.Storage.Persistent.Slice((int)((bpb.FirstDataLba + cluster - 2) * 512), 512).ToArray()).ToArray();
        byte[] initial = Enumerable.Range(0, 8192).Select(i => unchecked((byte)(i * 29 + 7))).ToArray();
        byte[] updated = initial.Select(value => (byte)(value ^ 0x5a)).ToArray(), journalBytes = new byte[8720];
        const ulong lockAddress = Vector + 64;
        const long pending = 0x40000000, reserved = pending + 1, shared = pending + 2;
        const uint flags = 2 | W.Create | W.NoFollow | W.CloseOnExec | W.LargeFile;
        List<string> barriers = []; uint startingParks = f.Parked; int storageCalls = 0, writes = 0;

        long Run(ulong nr, ulong a0, ulong a1 = 0, ulong a2 = 0, ulong a3 = 0, ulong a4 = 0, ulong a5 = 0, bool waits = false)
        {
            uint parks = f.Parked, copied = f.Thread.Writes;
            long value = f.Run(nr, a0, a1, a2, a3, a4, a5); storageCalls++;
            HostCheck.Equal(parks + (waits ? 1U : 0U), f.Parked);
            HostCheck.Equal(copied + (nr is 4 or 5 or 6 && value == 0 || nr == 17 && value > 0 ? 1U : 0U), f.Thread.Writes);
            HostCheck.True(!f.Thread.View(f.Main).Wait.IsValid && f.Thread.View(f.Main).WaitSource == 0 &&
                !f.Disk->RequestActive && !f.Provider->RequestActive && !f.Storage.State->RequestActive && !f.Storage.Device.State->RequestPending);
            return value;
        }
        uint Open(string path, uint openFlags, ulong mode = 0)
        {
            f.PutPath(path); long value = Run(2, Path, openFlags, mode, waits: path != "/data"); HostCheck.True(value >= 3); return (uint)value;
        }
        void Write(uint fd, byte[] bytes, ulong offset)
        {
            f.PutWrite(bytes); int first = f.Trace.Count; ulong before = f.Storage.Device.State->SubmittedBytes;
            HostCheck.Equal((long)bytes.Length, Run(328, fd, Vector, 1, offset, 0, W.RwfNoAppend, waits: true)); writes++;
            HostCheck.True(f.Trace.Skip(first).Any(row => row.Type == 1) && f.Trace.Skip(first).Any(row => row.Type == 4) &&
                f.Storage.Device.State->SubmittedBytes > before && f.Storage.Device.State->SubmittedBytes == f.Storage.Device.State->CompletedBytes &&
                f.Storage.Device.State->CompletedBytes == f.Storage.Device.State->DurableBytes && !f.Storage.State->Dirty);
        }
        void Sync(uint fd, string role)
        {
            var block = f.Storage.Device.State; ulong submitted = block->Submitted, completed = block->Completed, bytes = block->SubmittedBytes;
            int first = f.Trace.Count; HostCheck.Equal(0L, Run(74, fd, waits: true));
            HostCheck.True(f.Trace.Skip(first).Select(row => row.Type).SequenceEqual(new uint[] { 4 }) &&
                block->Submitted == submitted + 1 && block->Completed == completed + 1 && block->SubmittedBytes == bytes &&
                block->CompletedBytes == bytes && block->DurableBytes == bytes && !f.Storage.State->Dirty);
            barriers.Add(role);
            Console.WriteLine($"SQLITE_SOURCE5_FLUSH_HOST role={role} submitted={block->Submitted} completed={block->Completed} writeBytes={bytes} flushAcknowledgedBytes={block->DurableBytes}");
        }
        void Lock(uint fd, short type, long first, long length)
        {
            Span<byte> wire = UserRange(f, lockAddress, 32); wire.Clear(); BinaryPrimitives.WriteInt16LittleEndian(wire, type);
            BinaryPrimitives.WriteInt64LittleEndian(wire[8..], first); BinaryPrimitives.WriteInt64LittleEndian(wire[16..], length);
            HostCheck.Equal(0L, Run(72, fd, 6, lockAddress));
        }
        byte[] Stat(uint fd, string path, ulong length)
        {
            HostCheck.Equal(0L, Run(5, fd, Output, waits: true)); byte[] wire = UserRange(f, Output, 144).ToArray();
            HostCheck.True(BinaryPrimitives.ReadUInt32LittleEndian(wire.AsSpan(24)) == 0x8180 &&
                BinaryPrimitives.ReadUInt64LittleEndian(wire.AsSpan(48)) == length && BinaryPrimitives.ReadUInt64LittleEndian(wire.AsSpan(56)) == 512 &&
                BinaryPrimitives.ReadUInt64LittleEndian(wire.AsSpan(64)) == (length + 511) / 512);
            foreach (ulong nr in new ulong[] { 4, 6 })
            { f.PutPath(path); HostCheck.Equal(0L, Run(nr, Path, Output, waits: true)); HostCheck.True(UserRange(f, Output, 144).SequenceEqual(wire)); }
            return wire;
        }
        void Independent(string name, byte[] expected)
        {
            byte[] actual = new byte[expected.Length]; var read = Fat32Parser.ReadRootFile(media, f.Storage.Input.PartitionFirst, name, actual);
            HostCheck.True(read.Succeeded && read.BytesRead == (ulong)expected.Length && actual.SequenceEqual(expected));
        }
        uint Fat(uint cluster) => BinaryPrimitives.ReadUInt32LittleEndian(f.Storage.Persistent.Slice((int)(bpb.FirstFatLba * 512 + cluster * 4), 4)) & 0x0fffffff;

        uint database = Open("/data/contract.db", flags, 0x1a4); HostCheck.Equal(0L, Run(72, database, 2, 1));
        HostCheck.True(F.Capture(f.Files.State, database, out var databaseToken));
        Write(database, initial[..4096], 0); Write(database, initial[4096..], 4096); Sync(database, "seed-db");
        ulong inode = BinaryPrimitives.ReadUInt64LittleEndian(Stat(database, "/data/contract.db", 8192).AsSpan(8));
        uint reader = Open("/data/contract.db", W.LargeFile | W.CloseOnExec);
        HostCheck.Equal(17L, Run(8, database, 17)); HostCheck.Equal(29L, Run(8, reader, 29));
        Lock(database, 0, pending, 1); Lock(database, 0, shared, 510); Lock(database, 2, pending, 1); Lock(database, 1, reserved, 1);
        HostCheck.True(f.HasLocks()); f.PutPath("/data/contract.db-journal"); HostCheck.Equal(-2L, Run(4, Path, Output, waits: true));
        uint journal = Open("/data/contract.db-journal", flags, 0x180); HostCheck.Equal(0L, Run(72, journal, 2, 1));
        HostCheck.True(F.Capture(f.Files.State, journal, out var journalToken)); HostCheck.Equal(31L, Run(8, journal, 31));
        byte[] header = new byte[512], checksum = [0x4b, 9, 0xca, 0x75]; checksum.CopyTo(header, 12); header[19] = 2; header[22] = 2; header[26] = 0x10;
        (ulong Offset, byte[] Bytes)[] records = [(0, header), (512, [0, 0, 0, 1]), (516, initial[..4096]),
            (4612, checksum), (4616, [0, 0, 0, 2]), (4620, initial[4096..]), (8716, checksum)];
        foreach (var record in records) { Write(journal, record.Bytes, record.Offset); record.Bytes.CopyTo(journalBytes, (int)record.Offset); }
        byte[] facts = Stat(journal, "/data/contract.db-journal", 8720);
        var located = Fat32Parser.FindRootDirectoryEntry(media, f.Storage.Input.PartitionFirst, "contract.db-journal");
        HostCheck.True(located.Succeeded && located.Entry.FileSize == 8720 && located.Entry.Attributes == 0x20);
        List<uint> journalClusters = []; uint next = located.Entry.FirstCluster;
        for (int i = 0; i < 18; i++) { HostCheck.True(next >= 2 && next < bpb.ClusterCount + 2 && !journalClusters.Contains(next)); journalClusters.Add(next); next = Fat(next); }
        HostCheck.True(next >= 0x0ffffff8 && BinaryPrimitives.ReadUInt64LittleEndian(facts.AsSpan(64)) == (ulong)journalClusters.Count);
        HostCheck.True(f.Storage.Persistent.Slice((int)((bpb.FirstDataLba + journalClusters[^1] - 2) * 512 + 16), 496).IndexOfAnyExcept((byte)0) < 0);
        ulong entry = (BinaryPrimitives.ReadUInt64LittleEndian(facts.AsSpan(8)) - 1) * 32;
        var run = f.Storage.Persistent.Slice((int)entry - 64, 96);
        HostCheck.True(run[0] == 0x42 && run[11] == 15 && run[32] == 1 && run[43] == 15 && run[75] == 0x20);
        Independent("contract.db-journal", journalBytes); Independent("contract.db", initial);
        Lock(database, 1, pending, 1); Lock(database, 1, shared, 510);
        UserRange(f, Output, 8).Fill(0xa5); uint copies = f.Thread.Writes;
        HostCheck.Equal(0L, Run(17, journal, Output, 8, 9216, waits: true)); HostCheck.Equal(copies, f.Thread.Writes);
        HostCheck.True(UserRange(f, Output, 8).IndexOfAnyExcept((byte)0xa5) < 0);
        Sync(journal, "journal-body"); uint parent = Open("/data", W.LargeFile | W.CloseOnExec);
        Sync(parent, "parent"); HostCheck.Equal(0L, Run(3, parent)); HostCheck.True(f.HasLocks() && F.IsCurrent(f.Files.State, databaseToken));
        byte[] committedHeader = [0xd9, 0xd5, 5, 0xf9, 0x20, 0xa1, 0x63, 0xd7, 0, 0, 0, 2];
        if (failHeaderFlush)
        {
            byte[] priorSnapshotHash = SHA256.HashData(f.Storage.Persistent); ulong acknowledged = f.Storage.Device.State->DurableBytes;
            f.PutWrite(committedHeader); byte[] userBefore = UserRange(f, User + 4096, (uint)(LinuxThreadServicesTests.UserBytes - 4096)).ToArray();
            uint beforeCopies = f.Thread.Writes, beforeParks = f.Parked; int outCount = 0, flushCount = 0; bool failed = false;
            ulong firstSector = bpb.FirstDataLba + journalClusters[0] - 2;
            byte[] expectedSector = f.Storage.Persistent.Slice((int)(firstSector * 512), 512).ToArray(); committedHeader.CopyTo(expectedSector, 0);
            f.Begin(328, journal, Vector, 1, 0, 0, W.RwfNoAppend); HostCheck.True(f.Thread.View(f.Main).WaitSource == 5); f.Park();
            for (int i = 0; i < 100000 && !failed; i++)
            {
                HostCheck.True(f.Thread.Service->PollWritableDiskReadiness(Key, 2)); var device = f.Storage.Device.Hardware;
                if (!device->Pending) continue;
                f.Trace.Add((device->Type, device->Sector));
                if (device->Type == 1)
                {
                    outCount++; HostCheck.True(device->Sector == firstSector && outCount == 1 &&
                        new ReadOnlySpan<byte>(f.Storage.Device.Data + 512, 512).SequenceEqual(expectedSector));
                }
                if (device->Type == 4)
                { flushCount++; HostCheck.True(outCount == 1); f.Storage.Device.Complete(status: 1); failed = true; f.RetainedFailure = true; }
                else f.Storage.Device.Complete();
            }
            HostCheck.True(failed && outCount == 1 && flushCount == 1); f.Ready(); HostCheck.Equal(-5L, f.Consume());
            HostCheck.True(f.Parked == beforeParks + 1 && f.Thread.Writes == beforeCopies &&
                UserRange(f, User + 4096, (uint)userBefore.Length).SequenceEqual(userBefore) &&
                f.Storage.State->Dirty && f.Disk->RequestActive && f.Provider->RequestActive && !f.Storage.Device.State->RequestPending &&
                f.Storage.Device.State->DurableBytes == acknowledged && f.Storage.Device.State->CompletedBytes > acknowledged &&
                SHA256.HashData(f.Storage.Persistent).SequenceEqual(priorSnapshotHash));
            uint notifications = f.Storage.Device.Hardware->Notifies; ulong submitted = f.Storage.Device.State->Submitted;
            for (int i = 0; i < 3; i++)
            {
                HostCheck.True(f.Thread.Service->PollWritableDiskReadiness(Key, 2));
                HostCheck.Equal(LinuxSyscallAction.Rejected, f.Thread.Service->CompleteWritableDiskWait(Key, 2).Action);
            }
            HostCheck.True(f.Storage.Device.Hardware->Notifies == notifications && f.Storage.Device.State->Submitted == submitted &&
                f.Thread.Writes == beforeCopies && UserRange(f, User + 4096, (uint)userBefore.Length).SequenceEqual(userBefore) &&
                !F.DetachWritableDisk(f.Files.State) && ContinuingFat32Mutation.Stop(f.Storage.State) == ContinuingFat32Mutation.Status.RecoveryRequired);
            Independent("contract.db-journal", journalBytes); Independent("contract.db", initial);
            HostCheck.True(barriers.SequenceEqual(new[] { "seed-db", "journal-body", "parent" })); f.Storage.Device.CheckGuards();
            Console.WriteLine($"SQLITE_SOURCE5_HEADER_FAILURE_HOST result=-5 out={outCount} failedFlushes={flushCount} consumedOnce=true priorSnapshotUnchanged=true dirtyRetained=true submitted={submitted} flushAcknowledgedBytes={acknowledged}");
            return; // Fixture.Dispose verifies actual exit/reap refuses owner release, then resets only this failed fake machine.
        }
        Write(journal, committedHeader, 0); committedHeader.CopyTo(journalBytes, 0); Sync(journal, "journal-header");
        Independent("contract.db-journal", journalBytes); Independent("contract.db", initial);
        Write(database, updated[..4096], 0); Write(database, updated[4096..], 4096); Sync(database, "database");
        HostCheck.True(BinaryPrimitives.ReadUInt64LittleEndian(Stat(reader, "/data/contract.db", 8192).AsSpan(8)) == inode &&
            F.IsCurrent(f.Files.State, databaseToken) && f.HasLocks());
        HostCheck.Equal(31L, Run(8, journal, 0, 1)); HostCheck.Equal(0L, Run(3, journal)); HostCheck.True(!F.IsCurrent(f.Files.State, journalToken) && f.HasLocks());
        f.PutPath("/data/contract.db-journal"); HostCheck.Equal(0L, Run(87, Path, waits: true));
        f.PutPath("/data/contract.db-journal"); HostCheck.Equal(-2L, Run(6, Path, Output, waits: true));
        HostCheck.True(f.HasLocks() && journalClusters.All(cluster => Fat(cluster) == 0));
        for (int i = 0; i < 3; i++) HostCheck.True(f.Storage.Persistent[(int)entry - 64 + i * 32] == 0xe5 &&
            f.Storage.Persistent.Slice((int)entry - 63 + i * 32, 31).IndexOfAnyExcept((byte)0) < 0);
        // This additional requested host barrier is not counted as an
        // extra observed call in the original77B2 trace.
        parent = Open("/data", W.LargeFile | W.CloseOnExec); Sync(parent, "parent-after-unlink"); HostCheck.Equal(0L, Run(3, parent));
        Lock(database, 0, shared, 510); Lock(database, 2, pending, 2); Lock(database, 2, 0, 0); HostCheck.True(!f.HasLocks());
        UserRange(f, Output, 8192).Fill(0xa5); HostCheck.Equal(8192L, Run(17, reader, Output, 8192, waits: true));
        HostCheck.True(UserRange(f, Output, 8192).SequenceEqual(updated));
        HostCheck.Equal(17L, Run(8, database, 0, 1)); HostCheck.Equal(29L, Run(8, reader, 0, 1));
        Independent("contract.db", updated); Independent("DATA.BIN", Enumerable.Range(0, 900).Select(i => unchecked((byte)(i * 17 + 5))).ToArray()); Independent("KEEP.TXT", new byte[3]);
        HostCheck.Equal(Fat32Status.MissingFile, Fat32Parser.FindRootDirectoryEntry(media, f.Storage.Input.PartitionFirst, "contract.db-journal").Status);
        HostCheck.True(f.Storage.Persistent.Slice((int)(bpb.FirstDataLba * 512), 64).SequenceEqual(oldEntries));
        for (int i = 0; i < protectedClusters.Length; i++)
        {
            uint cluster = protectedClusters[i]; HostCheck.True(f.Storage.Persistent.Slice((int)((bpb.FirstDataLba + cluster - 2) * 512), 512).SequenceEqual(protectedData[i]) &&
                f.Storage.Persistent.Slice((int)(bpb.FirstFatLba * 512 + cluster * 4), 4).SequenceEqual(oldFat.AsSpan((int)cluster * 4, 4)));
        }
        HostCheck.True(f.Storage.Persistent.Slice((int)(bpb.FirstFatLba * 512), oldFat.Length).SequenceEqual(
            f.Storage.Persistent.Slice((int)((bpb.FirstFatLba + bpb.FatSizeSectors) * 512), oldFat.Length)));
        var databaseEntry = Fat32Parser.FindRootDirectoryEntry(media, f.Storage.Input.PartitionFirst, "contract.db"); HostCheck.True(databaseEntry.Succeeded);
        HashSet<uint> databaseClusters = []; next = databaseEntry.Entry.FirstCluster;
        for (int i = 0; i < 16; i++) { HostCheck.True(next >= 2 && next < bpb.ClusterCount + 2 && databaseClusters.Add(next)); next = Fat(next); }
        HostCheck.True(next >= 0x0ffffff8);
        for (int offset = 0; offset < oldFat.Length; offset += 4)
            if (!databaseClusters.Contains((uint)offset / 4)) HostCheck.True(f.Storage.Persistent.Slice((int)(bpb.FirstFatLba * 512) + offset, 4).SequenceEqual(oldFat.AsSpan(offset, 4)));
        HostCheck.Equal(0L, Run(3, reader)); HostCheck.Equal(0L, Run(3, database));
        byte[] userAfter = UserRange(f, User + 4096, (uint)(LinuxThreadServicesTests.UserBytes - 4096)).ToArray(); copies = f.Thread.Writes;
        HostCheck.True(f.Thread.Service->PollWritableDiskReadiness(Key, 2)); HostCheck.Equal(LinuxSyscallAction.Rejected, f.Thread.Service->CompleteWritableDiskWait(Key, 2).Action);
        HostCheck.True(f.Thread.Writes == copies && UserRange(f, User + 4096, (uint)userAfter.Length).SequenceEqual(userAfter) &&
            f.Files.State->ActiveDescriptors == 3 && f.Files.State->RetainedPipeOperations == 0 && F.Audit(f.Files.State) && !f.Storage.State->Dirty &&
            !f.Disk->RequestActive && !f.Provider->RequestActive && !f.Storage.State->RequestActive && !f.Storage.Device.State->RequestPending &&
            f.Storage.Device.State->Submitted == f.Storage.Device.State->Completed && f.Storage.Device.State->SubmittedBytes == f.Storage.Device.State->CompletedBytes &&
            f.Storage.Device.State->CompletedBytes == f.Storage.Device.State->DurableBytes && writes == 12 &&
            barriers.SequenceEqual(new[] { "seed-db", "journal-body", "parent", "journal-header", "database", "parent-after-unlink" }));
        f.Storage.Device.CheckGuards();
        Console.WriteLine($"SQLITE_SOURCE5_JOURNAL_HOST journalBytes=8720 databaseBytes=8192 storageCalls={storageCalls} waits={f.Parked - startingParks} positionedWrites={writes} explicitFlushes={barriers.Count} submitted={f.Storage.Device.State->Submitted} completed={f.Storage.Device.State->Completed} writeBytes={f.Storage.Device.State->SubmittedBytes} flushAcknowledgedBytes={f.Storage.Device.State->DurableBytes}");
    }

    private static ReadOnlySpan<byte> ElfRange(byte[] elf, ulong address, ulong bytes)
    {
        const ulong image = OS.Tooling.LinuxWritableDiskProofFixture.ImageBase;
        HostCheck.True(address >= image && bytes <= (ulong)elf.Length && address - image <= (ulong)elf.Length - bytes);
        return elf.AsSpan((int)(address - image), (int)bytes);
    }
    private static Span<byte> UserRange(Fixture f, ulong address, uint bytes)
    {
        HostCheck.True(bytes > 0 && address >= User + 4096 && bytes <= LinuxThreadServicesTests.UserBytes && address - User <= LinuxThreadServicesTests.UserBytes - bytes);
        return new Span<byte>(f.Thread.At(address), (int)bytes);
    }
    private static ulong[] DecodeLogUploadFrame(byte[] elf, OS.Tooling.LinuxWritableDiskProofFixture.CallSpec spec)
    {
        HostCheck.True(spec.ReturnRip >= OS.Tooling.LinuxWritableDiskProofFixture.ImageBase + 40);
        ReadOnlySpan<byte> code = ElfRange(elf, spec.ReturnRip - 40, 40); ulong[] actual = new ulong[7]; int cursor = 0;
        byte[] registers = [0xb8, 0xbf, 0xbe, 0xba, 0xba, 0xb8, 0xb9];
        for (int i = 0; i < actual.Length; i++)
        {
            if (i >= 4) HostCheck.Equal((byte)0x41, code[cursor++]);
            HostCheck.Equal(registers[i], code[cursor++]); actual[i] = BinaryPrimitives.ReadUInt32LittleEndian(code[cursor..]); cursor += 4;
        }
        HostCheck.True(cursor == 38 && code[cursor] == 0x0f && code[cursor + 1] == 0x05 && actual.SequenceEqual(new[] { spec.Number, spec.A0, spec.A1, spec.A2, spec.A3, spec.A4, spec.A5 }));
        if (spec.Number != 231)
        {
            ReadOnlySpan<byte> check = ElfRange(elf, spec.ReturnRip, 12);
            HostCheck.True(check[0] == 0x48 && check[1] == 0x3d && BinaryPrimitives.ReadInt32LittleEndian(check[2..]) == spec.Result &&
                check[6] == 0x0f && check[7] == 0x85 && (long)(spec.ReturnRip + 12) + BinaryPrimitives.ReadInt32LittleEndian(check[8..]) ==
                (long)OS.Tooling.LinuxWritableDiskProofFixture.ImageBase + OS.Tooling.LinuxWritableDiskProofFixture.FailureOffset);
        }
        return actual;
    }

    private sealed class PersistentDevice(WritableBlockSessionTests.Hardware* hardware) : IReadOnlyBlockDevice
    {
        public BlockDeviceInfo Info => BlockDeviceInfo.Create(512, hardware->MediumSectors).Info;
        public BlockReadResult ReadBlocks(ulong first, uint count, Span<byte> target)
        {
            if (first >= hardware->MediumSectors || count > hardware->MediumSectors - first || target.Length != count * 512)
                return BlockReadResult.Fail(BlockDeviceStatus.InvalidBlockRange);
            new ReadOnlySpan<byte>(hardware->Persistent + first * 512, checked((int)count * 512)).CopyTo(target);
            return BlockReadResult.Success(count, count * 512UL);
        }
    }

    [HostTest("writable source5 pins original read output through early readiness and consumes only after real Parking capture")]
    private static void EarlyRead()
    {
        using var f = new Fixture(); uint fd = f.Open(); f.Begin(17, fd, Output, 16);
        HostCheck.True(!f.May(Output, 16) && f.May(Output - 1, 1) && f.May(Output + 16, 1));
        f.Ready(); HostCheck.Equal(KernelThreadTable.State.Parking, f.Thread.Kernel.Inspect(f.Main).State);
        HostCheck.True(!f.May(Output, 16));
        HostCheck.True(f.Thread.Service->CommitWritableDiskWait(Key, 2, f.Thread.Kernel.Capture(f.Main), out bool handoff) && !handoff);
        HostCheck.Equal(16L, f.Consume()); HostCheck.True(f.May(Output, 16));
        HostCheck.Equal(0L, f.Run(8, fd, 0, 1));
    }

    [HostTest("writable source5 fresh fstat pins its exact cross page wire through early readiness without advancing the file cursor")]
    private static void StatEarlyReady()
    {
        using var f = new Fixture(); uint fd = f.Open("/data/stat5.bin", W.Create | W.Exclusive | 2);
        f.PutWrite(new byte[1000]); HostCheck.Equal(1000L, f.Run(328, fd, Vector, 1, 7, 0, W.RwfNoAppend));
        HostCheck.Equal(23L, f.Run(8, fd, 23));
        const ulong address = Output + 4096 - 80;
        var bytes = new Span<byte>(f.Thread.At(address - 1), 146); bytes.Fill(0xa5); uint copies = f.Thread.Writes;
        f.Begin(5, fd, address);
        HostCheck.True(!f.May(address, 144) && !f.May(address + 80, 1) &&
            f.May(address - 1, 1) && f.May(address + 144, 1));
        f.Ready(); HostCheck.Equal(KernelThreadTable.State.Parking, f.Thread.Kernel.Inspect(f.Main).State);
        HostCheck.Equal(copies, f.Thread.Writes); HostCheck.True(bytes.IndexOfAnyExcept((byte)0xa5) < 0);
        HostCheck.True(f.Thread.Service->CommitWritableDiskWait(Key, 2, f.Thread.Kernel.Capture(f.Main), out bool handoff) && !handoff);
        HostCheck.Equal(0L, f.Consume()); HostCheck.True(f.May(address, 144));
        var wire = bytes.Slice(1, 144);
        HostCheck.True(BinaryPrimitives.ReadUInt64LittleEndian(wire[8..]) != 0);
        HostCheck.Equal(0x8000U, BinaryPrimitives.ReadUInt32LittleEndian(wire[24..]) & 0xf000U);
        HostCheck.Equal(1007L, BinaryPrimitives.ReadInt64LittleEndian(wire[48..]));
        // This original fixture has one512B sector per allocated cluster.
        HostCheck.Equal(2L, BinaryPrimitives.ReadInt64LittleEndian(wire[64..]));
        HostCheck.True(bytes[0] == 0xa5 && bytes[145] == 0xa5 && f.Thread.Writes == copies + 1);
        HostCheck.Equal(23L, f.Run(8, fd, 0, 1));
    }

    [HostTest("writable source5 fstat cancellation and fd reuse revoke metadata output without late copy or cursor changes")]
    private static void StatCancellationAndReuse()
    {
        foreach (bool reuse in new[] { false, true })
        {
            using var f = new Fixture(); uint fd = f.Open(); var bytes = new Span<byte>(f.Thread.At(Output), 144); bytes.Fill(0xa5);
            OSHostHandle child = reuse ? f.Thread.Clone() : default; uint copies = f.Thread.Writes;
            f.Begin(5, fd, Output); f.Park(); HostCheck.True(!f.May(Output, 144));
            if (reuse)
            {
                HostCheck.Equal(child.Value, f.Thread.Kernel.Dispatch().Value); HostCheck.Equal(0L, f.Run(3, fd));
                f.PutPath("/app/fixture.data"); HostCheck.Equal((long)fd, f.Run(2, Path, 0x88100));
                f.Ready(); f.Thread.Yield(child);
            }
            else HostCheck.True(f.Thread.Kernel.Table->Interrupt(KernelThreadTableTests.Key, 2, f.Thread.View(f.Main).Wait).Succeeded);
            HostCheck.Equal(reuse ? -9L : -4L, f.Consume()); HostCheck.True(f.May(Output, 144)); f.Drain();
            HostCheck.True(f.Thread.Writes == copies && bytes.IndexOfAnyExcept((byte)0xa5) < 0);
            HostCheck.True(f.Thread.Service->CurrentImage(Key, 2, out _));
            if (!reuse) HostCheck.Equal(0L, f.Run(8, fd, 0, 1));
        }
    }

    [HostTest("writable gateway stat and lstat retain copied paths and exact metadata output until actual source5 completion")]
    private static void GatewayPathMetadata()
    {
        byte* scratch = stackalloc byte[64];
        foreach (ulong number in new ulong[] { 4, 6 })
        foreach (bool root in new[] { false, true })
        {
            using var f = new Fixture(); uint fd = f.Open(); HostCheck.Equal(23L, f.Run(8, fd, 23));
            f.PutPath(root ? "/data" : "/data/DATA.BIN");
            const ulong address = Output + 4096 - 80;
            var bytes = new Span<byte>(f.Thread.At(address - 1), 146); bytes.Fill(0xa5); uint copies = f.Thread.Writes;
            LinuxSyscallGateState gate = default;
            HostCheck.Equal(LinuxSyscallGateStatus.Active, LinuxSyscallGateway.Begin(ref gate, new()
            {
                UserRoot = 0x200000, MaximumCalls = 16, MaximumWrite = 64, Scratch = scratch, ScratchBytes = 64,
                FirmwareExited = true, OwnedBoundaryValidated = true, EnableStartup = true, EnableDescriptors = true, EnableThreads = true
            }, f.Thread.GatewayAccess));
            var image = f.Thread.CurrentImage(); var frame = image->Frame;
            frame.Number = number; frame.Argument0 = Path; frame.Argument1 = address;
            frame.Argument2 = frame.Argument3 = frame.Argument4 = frame.Argument5 = 0; image->Frame = frame;
            HostCheck.Equal(LinuxSyscallAction.Blocked, LinuxSyscallGateway.Dispatch(ref gate, ref frame).Action);
            HostCheck.True(f.Thread.View(f.Main).WaitSource == 5 && !f.May(address, 144) &&
                f.May(address - 1, 1) && f.May(address + 144, 1));
            f.PutPath("/data/missing.db-journal"); // Only the already-copied path owns this request.
            f.Ready(); HostCheck.True(bytes.IndexOfAnyExcept((byte)0xa5) < 0 && f.Thread.Writes == copies);
            HostCheck.True(f.Thread.Service->CommitWritableDiskWait(Key, 2, f.Thread.Kernel.Capture(f.Main), out bool handoff) && !handoff);
            var completion = f.Thread.Service->CompleteWritableDiskWait(Key, 2);
            var done = LinuxSyscallGateway.CompleteBlocked(ref gate, completion);
            HostCheck.True(done.Action == LinuxSyscallAction.Return && done.Value == 0 && gate.TotalCalls == 1);
            HostCheck.Equal(LinuxSyscallAction.Rejected, LinuxSyscallGateway.CompleteBlocked(ref gate, completion).Action);
            var wire = bytes.Slice(1, 144);
            HostCheck.Equal(root ? 0x4000U : 0x8000U, BinaryPrimitives.ReadUInt32LittleEndian(wire[24..]) & 0xf000U);
            HostCheck.True(BinaryPrimitives.ReadUInt64LittleEndian(wire[8..]) != 0 &&
                bytes[0] == 0xa5 && bytes[145] == 0xa5 && f.Thread.Writes == copies + 1 && f.May(address, 144));
            HostCheck.Equal(23L, f.Run(8, fd, 0, 1));
        }
    }

    [HostTest("writable source5 path metadata cancellation revokes output and partial original capacity never expands")]
    private static void PathMetadataCancellationAndCapacity()
    {
        foreach (ulong number in new ulong[] { 4, 6 })
        foreach (bool cancel in new[] { false, true })
        {
            using var f = new Fixture(); f.PutPath("/data");
            ulong address = cancel ? Output : User + LinuxThreadServicesTests.UserBytes - 80;
            var bytes = new Span<byte>(f.Thread.At(address), 80); bytes.Fill(0xa5); uint copies = f.Thread.Writes;
            f.Begin(number, Path, address); HostCheck.True(!f.May(address, 80)); f.Park();
            if (cancel) HostCheck.True(f.Thread.Kernel.Table->Interrupt(KernelThreadTableTests.Key, 2, f.Thread.View(f.Main).Wait).Succeeded);
            else f.Ready();
            HostCheck.Equal(cancel ? -4L : -14L, f.Consume()); f.Drain();
            HostCheck.True(f.May(address, 80) && f.Thread.Writes == copies && bytes.IndexOfAnyExcept((byte)0xa5) < 0);
        }
    }

    [HostTest("writable source5 interrupted pending read revokes original output without restart or late copy and drains independently")]
    private static void InterruptedRead()
    {
        using var f = new Fixture(); uint fd = f.Open(); uint copies = f.Thread.Writes;
        f.Begin(17, fd, Output, 16); f.Park();
        HostCheck.True(f.Thread.Kernel.Table->Interrupt(KernelThreadTableTests.Key, 2, f.Thread.View(f.Main).Wait).Succeeded);
        HostCheck.Equal(-4L, f.Consume()); HostCheck.True(f.May(Output, 16));
        HostCheck.True(f.Thread.Service->CurrentImage(Key, 2, out _)); // No restart decision may replay work.
        f.Drain(); HostCheck.Equal(copies, f.Thread.Writes); HostCheck.Equal(0L, f.Run(8, fd, 0, 1));
    }

    [HostTest("writable source5 already-ready write and flush win a prior signal completion without mutation replay")]
    private static void ReadyMutationWins()
    {
        using var f = new Fixture(); uint fd = f.Open(flags: 2); byte[] value = [7, 8, 9]; f.PutWrite(value);
        foreach (ulong number in new ulong[] { 328, 74 })
        {
            f.Begin(number, fd, number == 328 ? Vector : 0, number == 328 ? 1UL : 0, 0, 0, number == 328 ? W.RwfNoAppend : 0);
            f.Park(); f.AdapterReady(); ulong writes = f.Storage.Device.State->SubmittedBytes;
            HostCheck.True(f.Thread.Kernel.Table->Interrupt(KernelThreadTableTests.Key, 2, f.Thread.View(f.Main).Wait).Succeeded);
            HostCheck.Equal(number == 328 ? 3L : 0L, f.Consume());
            HostCheck.Equal(writes, f.Storage.Device.State->SubmittedBytes);
            HostCheck.True(f.Thread.Service->CurrentImage(Key, 2, out _));
        }
    }

    [HostTest("writable source5 peer close and reused fd retain original read pin until EBADF with no late output")]
    private static void CloseAndReuse()
    {
        using var f = new Fixture(); uint fd = f.Open(); var child = f.Thread.Clone(); uint copies = f.Thread.Writes;
        f.Begin(17, fd, Output, 16); f.Park(); HostCheck.Equal(child.Value, f.Thread.Kernel.Dispatch().Value);
        HostCheck.Equal(0L, f.Run(3, fd)); f.PutPath("/app/fixture.data"); HostCheck.Equal((long)fd, f.Run(2, Path, 0x88100));
        HostCheck.True(!f.May(Output, 16)); f.Ready(); f.Thread.Yield(child);
        HostCheck.Equal(-9L, f.Consume()); HostCheck.True(f.May(Output, 16)); HostCheck.Equal(copies, f.Thread.Writes);
    }

    [HostTest("writable source5 failed user copy leaves original global cursor unchanged and releases only its captured pin")]
    private static void CopyFailure()
    {
        using var f = new Fixture(); uint fd = f.Open(); f.Begin(0, fd, Output, 16); f.Park(); f.Ready();
        f.Thread.FailWrite(Output); HostCheck.Equal(-14L, f.Consume()); f.Thread.FailWrite(ulong.MaxValue);
        HostCheck.True(f.May(Output, 16)); HostCheck.Equal(0L, f.Run(8, fd, 0, 1));
        HostCheck.Equal(16L, f.Run(0, fd, Output, 16)); HostCheck.Equal(16L, f.Run(8, fd, 0, 1));
    }

    [HostTest("writable source5 process exit revokes copy only at actual exit commit and keeps orphan backing until drain")]
    private static void ExitPending()
    {
        using var f = new Fixture(); uint fd = f.Open(); uint copies = f.Thread.Writes;
        f.Begin(17, fd, Output, 16); HostCheck.True(f.Thread.Service->Shutdown(Key, 2, 7));
        HostCheck.True(!f.May(Output, 16)); f.Park();
        HostCheck.True(f.Thread.Service->CommitExit(Key, 2, f.Thread.Kernel.Capture(f.Main), out _));
        HostCheck.True(f.May(Output, 16)); f.Thread.Kernel.Confirm();
        HostCheck.True(f.Thread.Service->Reap(Key, 2, f.Main, f.Thread.Kernel.SupervisorRsp, out _));
        HostCheck.True(!f.Thread.Service->Release(Key, 2)); f.Drain(); HostCheck.Equal(copies, f.Thread.Writes);
    }

    [HostTest("writable source5 copied scheduler inventory authenticates retained queues supervisor and exact live worker backing without effects")]
    private static void SchedulerInventory()
    {
        using var f = new Fixture(); var table = f.Thread.Kernel.Table; var before = f.Thread.Kernel.Snapshot();
        HostCheck.Equal(15U, table->RetainedExtentCount(KernelThreadTableTests.Key, 2));
        var expected = new (ulong Start, ulong Bytes)[] {
            ((ulong)table, (ulong)sizeof(KernelThreadTable)), ((ulong)f.Thread.Kernel.Slots, 3UL * (ulong)sizeof(KernelThreadSlot)),
            ((ulong)f.Thread.Kernel.Ready, 24), ((ulong)f.Thread.Kernel.Deadlines, 3UL * (ulong)sizeof(DeadlineQueue.Entry)),
            (f.Thread.Kernel.Owner.ContextArena, f.Thread.Kernel.Owner.ContextArenaBytes), (f.Thread.Kernel.Code, 0x10000),
            (f.Thread.Kernel.Owner.SwitchStackGuardStart, f.Thread.Kernel.Owner.SwitchStackBytes),
            ((ulong)f.Thread.Kernel.Supervisor.Context, f.Thread.Kernel.Supervisor.ContextBytes),
            (f.Thread.Kernel.Supervisor.StackGuardStart, f.Thread.Kernel.Supervisor.StackBytes),
            ((ulong)f.Thread.Kernel.Context(0), 688), (f.Thread.Kernel.Stack(0), 0x3000) };
        for (uint i = 0; i < expected.Length; i++)
        {
            HostCheck.Equal(KernelThreadTable.Status.Ok, table->InspectRetainedExtent(KernelThreadTableTests.Key, 2, i, out ulong start, out ulong bytes));
            HostCheck.True(start == expected[i].Start && bytes == expected[i].Bytes);
        }
        for (uint i = 11; i < 15; i++)
        { HostCheck.Equal(KernelThreadTable.Status.Ok, table->InspectRetainedExtent(KernelThreadTableTests.Key, 2, i, out ulong start, out ulong bytes)); HostCheck.True(start == 0 && bytes == 0); }
        HostCheck.Equal(0U, table->RetainedExtentCount(1, 2)); HostCheck.Equal(0U, table->RetainedExtentCount(KernelThreadTableTests.Key, 0x202));
        var copied = *table; HostCheck.Equal(0U, copied.RetainedExtentCount(KernelThreadTableTests.Key, 2));
        HostCheck.Equal(KernelThreadTable.Status.InvalidStorage, table->InspectRetainedExtent(KernelThreadTableTests.Key, 2, 15, out ulong bad, out ulong length));
        HostCheck.True(bad == 0 && length == 0); f.Thread.Kernel.Same(before);
    }

    [HostTest("writable source5 admission rejects provider input over actual scheduler ready and deadline backing before thread attachment")]
    private static void SchedulerAlias()
    {
        using var f = new Fixture(aliasScheduler: true);
        HostCheck.True(f.Rejected && f.Thread.Service->ActiveThreads == 0 && !f.Disk->RequestActive);
    }

    [HostTest("writable attachment preserves genuine anonymous futex wait wake and completion on the actual admitted user pool")]
    private static void UserPoolFutex()
    {
        using var f = new Fixture(); var memory = f.Files.Memory; f.Thread.UseMemory(memory.Memory);
        const ulong address = LinuxMemoryServices.DynamicStart + 0x20000;
        HostCheck.True(LinuxMemoryServices.AttachMutationGuard(memory.State, f.Thread.Service, &LinuxFutexTests.MemoryGuard));
        try
        {
            HostCheck.Equal((long)address, LinuxMemoryServices.Map(memory.State, address, 4096, 3, 0x100022, ulong.MaxValue, 0));
            *(uint*)memory.At(address) = 17; var child = f.Thread.Clone();
            HostCheck.Equal(LinuxSyscallAction.Blocked, f.Thread.Call(202, address, 128, 17).Action);
            LinuxFutexTests.Park(f.Thread, f.Main); HostCheck.Equal(child.Value, f.Thread.Kernel.Dispatch().Value);
            HostCheck.Equal(1L, f.Thread.Call(202, address, 129, 1).Value); f.Thread.Yield(child);
            HostCheck.Equal(f.Main.Value, f.Thread.Kernel.Dispatch().Value);
            var result = f.Thread.Service->CompleteFutexWait(Key, 2);
            HostCheck.True(result.Action == LinuxSyscallAction.Return && result.Value == 0 && f.May(address, 4096));
        }
        finally { HostCheck.True(LinuxMemoryServices.DetachMutationGuard(memory.State, f.Thread.Service, &LinuxFutexTests.MemoryGuard)); }
    }

    [HostTest("writable gateway routes nonblocking flock through the actual current worker without parking or cursor changes")]
    private static void GatewayFlock()
    {
        using var f = new Fixture(); uint first = f.Open(), second = f.Open();
        byte* scratch = stackalloc byte[64]; LinuxSyscallGateState gate = default;
        HostCheck.Equal(LinuxSyscallGateStatus.Active, LinuxSyscallGateway.Begin(ref gate, new()
        {
            UserRoot = 0x200000, MaximumCalls = 16, MaximumWrite = 64, Scratch = scratch, ScratchBytes = 64,
            FirmwareExited = true, OwnedBoundaryValidated = true, EnableStartup = true, EnableDescriptors = true, EnableThreads = true
        }, f.Thread.GatewayAccess));
        uint parked = f.Parked;
        foreach (var item in new (uint Fd, uint Mode, long Value)[] { (first, 5, 0), (second, 6, -11), (first, 8, 0), (second, 6, 0), (first, 5, -11), (second, 12, 0) })
        {
            var image = f.Thread.CurrentImage(); var frame = image->Frame;
            frame.Number = 73; frame.Argument0 = item.Fd; frame.Argument1 = item.Mode;
            frame.Argument2 = frame.Argument3 = frame.Argument4 = frame.Argument5 = 0; image->Frame = frame;
            ulong calls = gate.TotalCalls; var result = LinuxSyscallGateway.Dispatch(ref gate, ref frame);
            HostCheck.Equal(LinuxSyscallAction.Return, result.Action); HostCheck.Equal(item.Value, result.Value);
            HostCheck.True(gate.Status == LinuxSyscallGateStatus.Active && gate.TotalCalls == calls + 1 &&
                !f.Thread.View(f.Main).Wait.IsValid && !f.Disk->RequestActive && !f.Provider->RequestActive);
        }
        HostCheck.Equal(parked, f.Parked); HostCheck.Equal(0L, f.Run(8, first, 0, 1)); HostCheck.Equal(0L, f.Run(8, second, 0, 1));
    }

    [HostTest("writable gateway admits measured positioned write sync and unlink through genuine source5 blocked completions")]
    private static void GatewayCalls()
    {
        using var f = new Fixture(); uint fd = f.Open("/data/gateway.bin", 2 | W.Create | W.Exclusive); f.PutWrite([31, 32, 33]);
        byte* scratch = stackalloc byte[64]; LinuxSyscallGateState gate = default;
        HostCheck.Equal(LinuxSyscallGateStatus.Active, LinuxSyscallGateway.Begin(ref gate, new()
        {
            UserRoot = 0x200000, MaximumCalls = 16, MaximumWrite = 64, Scratch = scratch, ScratchBytes = 64,
            FirmwareExited = true, OwnedBoundaryValidated = true, EnableStartup = true, EnableDescriptors = true, EnableThreads = true
        }, f.Thread.GatewayAccess));
        foreach (ulong number in new ulong[] { 328, 74, 75, 87 })
        {
            if (number == 87) { HostCheck.Equal(0L, f.Run(3, fd)); f.PutPath("/data/gateway.bin"); }
            var image = f.Thread.CurrentImage(); var frame = image->Frame;
            frame.Number = number; frame.Argument0 = number == 87 ? Path : fd; frame.Argument1 = number == 328 ? Vector : 0;
            frame.Argument2 = number == 328 ? 1UL : 0; frame.Argument3 = frame.Argument4 = 0; frame.Argument5 = number == 328 ? W.RwfNoAppend : 0;
            image->Frame = frame; ulong calls = gate.TotalCalls;
            HostCheck.Equal(LinuxSyscallAction.Blocked, LinuxSyscallGateway.Dispatch(ref gate, ref frame).Action);
            HostCheck.True(gate.Status == LinuxSyscallGateStatus.Blocked && gate.TotalCalls == calls + 1 && f.Thread.View(f.Main).WaitSource == 5);
            f.Park(); f.Ready();
            if (!f.Thread.Kernel.Table->Current.IsValid) HostCheck.Equal(f.Main.Value, f.Thread.Kernel.Dispatch().Value);
            var completion = f.Thread.Service->CompleteWritableDiskWait(Key, 2);
            var done = LinuxSyscallGateway.CompleteBlocked(ref gate, completion);
            HostCheck.True(done.Action == LinuxSyscallAction.Return && done.Value == (number == 328 ? 3 : 0) && gate.TotalCalls == calls + 1);
            HostCheck.Equal(LinuxSyscallAction.Rejected, LinuxSyscallGateway.CompleteBlocked(ref gate, completion).Action);
        }
    }

    [HostTest("writable source5 failed actual flush returns EIO then retains dirty provider tree after real exit and refuses release")]
    private static void DirtyFlush()
    {
        using var f = new Fixture(); uint fd = f.Open(flags: 2); f.PutWrite([71, 72]); f.Begin(328, fd, Vector, 1, 5, 0, W.RwfNoAppend); f.Park();
        bool failed = false;
        for (int i = 0; i < 100000 && !failed; i++)
        {
            HostCheck.True(f.Thread.Service->PollWritableDiskReadiness(Key, 2));
            if (!f.Storage.Device.Hardware->Pending) continue;
            if (f.Storage.Device.Hardware->Type == 4) { f.Storage.Device.Complete(status: 1); failed = true; }
            else f.Storage.Device.Complete();
        }
        HostCheck.True(failed); f.RetainedFailure = true; f.Ready(); HostCheck.Equal(-5L, f.Consume());
        HostCheck.True(f.Storage.State->Dirty && f.Disk->RequestActive && f.Provider->RequestActive && !F.DetachWritableDisk(f.Files.State));
        HostCheck.True(f.Thread.Service->CurrentImage(Key, 2, out _));
    }

    [HostTest("writable source5 actual mapping guard preserves a retained output lifetime and never expands a previously absent copy ceiling")]
    private static void MappingLifetime()
    {
        foreach (bool initial in new[] { true, false })
        {
            using var f = new Fixture(); uint fd = f.Open(); var memory = f.Files.Memory; f.Thread.UseMemory(memory.Memory);
            const ulong address = LinuxMemoryServices.DynamicStart + 0x20000;
            HostCheck.True(LinuxMemoryServices.AttachMutationGuard(memory.State, f.Thread.Service, &LinuxFutexTests.MemoryGuard));
            bool detached = false;
            try
            {
                if (initial) HostCheck.Equal((long)address, LinuxMemoryServices.Map(memory.State, address, 4096, 3, 0x100022, ulong.MaxValue, 0));
                var child = f.Thread.Clone(); uint copies = f.Thread.Writes; f.Begin(17, fd, address, 16); f.Park();
                HostCheck.Equal(child.Value, f.Thread.Kernel.Dispatch().Value);
                if (initial)
                {
                    HostCheck.Equal(-12L, LinuxMemoryServices.Unmap(memory.State, address, 4096));
                    HostCheck.Equal(-12L, LinuxMemoryServices.Protect(memory.State, address, 4096, 1));
                    HostCheck.Equal(-12L, LinuxMemoryServices.Map(memory.State, address, 4096, 3, 0x32, ulong.MaxValue, 0));
                }
                else
                {
                    HostCheck.True(f.May(address, 4096));
                    HostCheck.Equal((long)address, LinuxMemoryServices.Map(memory.State, address, 4096, 3, 0x100022, ulong.MaxValue, 0));
                }
                new Span<byte>(memory.At(address), 4096).Fill(0xa5); f.Ready();
                if (initial) HostCheck.Equal(-12L, LinuxMemoryServices.Protect(memory.State, address, 4096, 1));
                // The model now copies real mapped pool bytes. A mapping which
                // existed at Begin retains its original ceiling; a newly mapped
                // range still cannot enlarge the captured zero-byte ceiling.
                f.Thread.Yield(child); HostCheck.Equal(initial ? 16L : -14L, f.Consume());
                HostCheck.Equal(copies + (initial ? 1U : 0U), f.Thread.Writes);
                ReadOnlySpan<byte> output = new(memory.At(address), 4096);
                if (initial) HostCheck.True(output[..16].SequenceEqual(Enumerable.Range(0, 16).Select(i => unchecked((byte)(i * 17 + 5))).ToArray()));
                HostCheck.True(output[(initial ? 16 : 0)..].IndexOfAnyExcept((byte)0xa5) < 0);
                HostCheck.Equal(0L, f.Run(8, fd, 0, 1)); // Positioned copy leaves the shared cursor unchanged.
                HostCheck.True(f.May(address, 4096)); HostCheck.Equal(0L, LinuxMemoryServices.Protect(memory.State, address, 4096, 1));
            }
            finally { detached = LinuxMemoryServices.DetachMutationGuard(memory.State, f.Thread.Service, &LinuxFutexTests.MemoryGuard); }
            HostCheck.True(detached);
        }
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly LinuxThreadServicesTests.Model Thread = new(futex: true);
        internal readonly LinuxDescriptorServicesTests.Owner Files = new(descriptors: 32, descriptions: 32);
        internal readonly ContinuingFat32MutationTests.Fixture Storage = new();
        internal readonly W.State* Provider = (W.State*)NativeMemory.AllocZeroed((nuint)sizeof(W.State));
        private readonly W.Descriptor* descriptors = (W.Descriptor*)NativeMemory.AllocZeroed(32, (nuint)sizeof(W.Descriptor));
        private readonly W.Description* descriptions = (W.Description*)NativeMemory.AllocZeroed(32, (nuint)sizeof(W.Description));
        private readonly byte* input;
        private readonly W.ProcessSlot* processes = (W.ProcessSlot*)NativeMemory.AllocZeroed(4, (nuint)sizeof(W.ProcessSlot));
        private readonly W.RangeLock* locks = (W.RangeLock*)NativeMemory.AllocZeroed(32, (nuint)sizeof(W.RangeLock));
        private readonly W.RangeLock* scratch = (W.RangeLock*)NativeMemory.AllocZeroed(65, (nuint)sizeof(W.RangeLock));
        internal readonly F.WritableDiskState* Disk = (F.WritableDiskState*)NativeMemory.AllocZeroed((nuint)sizeof(F.WritableDiskState));
        private readonly F.WritableDiskBinding* bindings = (F.WritableDiskBinding*)NativeMemory.AllocZeroed(32, (nuint)sizeof(F.WritableDiskBinding));
        internal OSHostHandle Main;
        internal uint Parked;
        internal List<(uint Type, ulong Sector)>? Trace;
        internal bool RetainedFailure;
        internal readonly bool Rejected;
        private readonly bool borrowedInput;
        private readonly W.FreeSearch search;
        private ulong Owner => (ulong)Thread.Service;
        internal Fixture(bool aliasScheduler = false, uint primaryPid = 101, bool deferBinding = false, uint packagePid = 0,
            bool sqliteFootprints = false, uint workBudget = 100000, W.FreeSearch? search = null)
        {
            this.search = search ?? new(2, 64, 256);
            borrowedInput = aliasScheduler; input = aliasScheduler ? (byte*)Thread.Kernel.Ready : (byte*)NativeMemory.AllocZeroed(W.IoBytes);
            if (aliasScheduler) new Span<byte>(input, (int)W.IoBytes).Clear(); // No live workers; model of an invalid offered borrow.
            Storage.Volatile[(int)(Storage.Input.PartitionFirst * 512 + 48)] = 0xff;
            Storage.Volatile[(int)(Storage.Input.PartitionFirst * 512 + 49)] = 0xff;
            Storage.Input.WorkBudget = workBudget;
            if (sqliteFootprints) LinuxWritableDiskDescriptorServicesTests.SeedSqliteFootprints(Storage);
            Storage.Mount();
            HostCheck.True(W.Start(Provider, new() { Engine = Storage.State, Epoch = Storage.Input.Epoch, Descriptors = descriptors,
                Descriptions = descriptions, DescriptorCount = 32, DescriptionCount = 32, Input = input, InputBytes = W.IoBytes, ExclusiveOwnerValidated = true }));
            HostCheck.True(W.EnableLocks(Provider, new() { Processes = processes, ProcessCount = 4, Locks = locks, LockCount = 32,
                Scratch = scratch, ScratchCount = 65, PrimaryPid = primaryPid }));
            Files.Admission.User = Thread.DescriptorAccess; Files.Admission.CopyToUser = Thread.UserCopyOut;
            Files.Admission.PackageProcessId = packagePid; Files.Begin();
            if (deferBinding) return;
            HostCheck.True(AttachWritable());
            if (aliasScheduler)
            {
                uint calls = Storage.Device.Hardware->Calls; var before = Thread.Kernel.Snapshot();
                HostCheck.True(!Thread.Service->EnablePipes(Key, 2, Files.State)); Thread.Kernel.Same(before);
                HostCheck.Equal(calls, Storage.Device.Hardware->Calls); Rejected = true; return;
            }
            HostCheck.True(EnableThreads()); Start();
        }
        internal bool AttachWritable() => F.AttachWritableDisk(Files.State, Disk, new()
        {
            Provider = Provider, Process = W.PrimaryProcess(Provider), Bindings = bindings, BindingCount = 32,
            Epoch = Storage.Input.Epoch, Search = search, FirmwareExited = true, SingleOwnerValidated = true
        });
        internal bool EnableThreads() => Thread.Service->EnablePipes(Key, 2, Files.State);
        internal void Start()
        {
            Main = Thread.Start();
            HostCheck.Equal(Main.Value, Thread.Kernel.Dispatch().Value);
        }
        internal W.Result ProviderResult(W.Result begun)
        {
            if (begun.Phase != W.Progress.Pending) return begun;
            for (int i = 0; i < 100000; i++)
            {
                var result = W.Pump(Provider); CompleteHardware();
                if (result.Phase == W.Progress.Ready) return W.Complete(Provider, begun.Request);
                HostCheck.Equal(W.Progress.Pending, result.Phase);
            }
            throw new InvalidOperationException("direct registered owner did not complete");
        }
        internal void PutPath(string path) => Encoding.ASCII.GetBytes(path + '\0').CopyTo(new Span<byte>(Thread.At(Path), path.Length + 1));
        internal void PutWrite(byte[] bytes)
        { bytes.CopyTo(new Span<byte>(Thread.At(Payload), bytes.Length)); *(ulong*)Thread.At(Vector) = Payload; *(ulong*)(Thread.At(Vector) + 8) = (ulong)bytes.Length; }
        internal bool May(ulong start, ulong bytes) => Thread.Service->MayChangeUserRange(Key, 2, start, bytes);
        internal bool HasLocks()
        {
            FieldInfo active = typeof(W.RangeLock).GetField("Active", BindingFlags.Instance | BindingFlags.NonPublic)!;
            for (int i = 0; i < 32; i++) if ((bool)active.GetValue(locks[i])!) return true;
            return false;
        }
        private LinuxSyscallResult Call(ulong number, ulong a0, ulong a1, ulong a2, ulong a3, ulong a4, ulong a5)
        {
            var image = Thread.CurrentImage(); var frame = image->Frame;
            frame.Number = number; frame.Argument0 = a0; frame.Argument1 = a1; frame.Argument2 = a2;
            frame.Argument3 = a3; frame.Argument4 = a4; frame.Argument5 = a5; image->Frame = frame;
            var result = Thread.Service->DispatchDescriptor(Key, 2, frame);
            if (result.Action == LinuxSyscallAction.Return) image->Value = result.Value;
            return result;
        }
        internal void Begin(ulong number, ulong a0, ulong a1 = 0, ulong a2 = 0, ulong a3 = 0, ulong a4 = 0, ulong a5 = 0)
            => HostCheck.Equal(LinuxSyscallAction.Blocked, Call(number, a0, a1, a2, a3, a4, a5).Action);
        internal long Run(ulong number, ulong a0, ulong a1 = 0, ulong a2 = 0, ulong a3 = 0, ulong a4 = 0, ulong a5 = 0)
        {
            var result = Call(number, a0, a1, a2, a3, a4, a5);
            if (result.Action == LinuxSyscallAction.Return) return result.Value;
            HostCheck.Equal(LinuxSyscallAction.Blocked, result.Action); Park(); Ready(); return Consume();
        }
        internal uint Open(string path = "/data/DATA.BIN", uint flags = 0)
        { PutPath(path); long fd = Run(2, Path, flags, (flags & W.Create) != 0 ? 0x180UL : 0); HostCheck.True(fd >= 3); return (uint)fd; }
        internal void Park()
        {
            HostCheck.True(Thread.Service->CommitWritableDiskWait(Key, 2, Thread.Kernel.Capture(Main), out bool handoff)); Parked++;
            if (handoff) Thread.Kernel.Confirm();
        }
        private void CompleteHardware()
        {
            if (!Storage.Device.Hardware->Pending) return;
            Trace?.Add((Storage.Device.Hardware->Type, Storage.Device.Hardware->Sector)); Storage.Device.Complete();
        }
        internal void Ready()
        {
            for (int i = 0; i < 100000; i++)
            {
                HostCheck.True(Thread.Service->PollWritableDiskReadiness(Key, 2)); CompleteHardware();
                if (Thread.Kernel.Inspect(Main).Completion != KernelThreadTable.Completion.None) return;
            }
            throw new InvalidOperationException("source5 did not complete");
        }
        internal void AdapterReady()
        {
            for (int i = 0; i < 100000; i++)
            {
                var result = F.PumpWritableDiskOne(Files.State, Owner); CompleteHardware();
                if (result.Progress == F.WritableDiskProgress.Ready) return;
                HostCheck.True(result.Progress != F.WritableDiskProgress.Rejected);
            }
            throw new InvalidOperationException("adapter did not complete");
        }
        internal long Consume()
        {
            if (!Thread.Kernel.Table->Current.IsValid) HostCheck.Equal(Main.Value, Thread.Kernel.Dispatch().Value);
            var result = Thread.Service->CompleteWritableDiskWait(Key, 2); HostCheck.Equal(LinuxSyscallAction.Return, result.Action); return result.Value;
        }
        internal void Drain()
        {
            for (int i = 0; i < 100000 && Disk->RequestActive; i++)
            { HostCheck.True(Thread.Service->PollWritableDiskReadiness(Key, 2)); CompleteHardware(); }
            HostCheck.True(!Disk->RequestActive);
        }
        public void Dispose()
        {
            try
            {
                HostCheck.True(Thread.Service->Shutdown(Key, 2, 7));
                while (Thread.Service->ActiveThreads != 0)
                {
                    var current = Thread.Kernel.Table->Current;
                    if (!current.IsValid) current = Thread.Kernel.Dispatch();
                    if (Thread.Kernel.Inspect(current).State == KernelThreadTable.State.Parking)
                    {
                        HostCheck.True(Thread.Service->CommitWritableDiskWait(Key, 2, Thread.Kernel.Capture(current), out bool handoff));
                        if (handoff) Thread.Kernel.Confirm(); continue;
                    }
                    HostCheck.True(Thread.Service->CommitExit(Key, 2, Thread.Kernel.Capture(current), out _));
                    Thread.Kernel.Confirm(); HostCheck.True(Thread.Service->Reap(Key, 2, current, Thread.Kernel.SupervisorRsp, out _));
                }
                if (RetainedFailure)
                {
                    HostCheck.True(!Thread.Service->Release(Key, 2) && !F.DetachWritableDisk(Files.State) && Disk->RequestActive && Storage.State->Dirty);
                    // Discard this failed host model only after Storage.Dispose's
                    // real fake-device reset; this is not successful retirement.
                    return;
                }
                if (!Rejected) Drain(); HostCheck.True(Thread.Service->Release(Key, 2)); HostCheck.True(F.End(Files.State));
                HostCheck.Equal(W.Progress.Retired, W.Detach(Provider).Phase);
            }
            finally
            {
                Storage.Dispose(); Files.Dispose(); Thread.Dispose(); NativeMemory.Free(Disk); NativeMemory.Free(bindings);
                NativeMemory.Free(Provider); NativeMemory.Free(descriptors); NativeMemory.Free(descriptions); if (!borrowedInput) NativeMemory.Free(input);
                NativeMemory.Free(processes); NativeMemory.Free(locks); NativeMemory.Free(scratch);
            }
        }
    }
}
