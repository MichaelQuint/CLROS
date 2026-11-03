using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Pal;
using OS.Storage;
using C = OS.Storage.ContinuingFat32Mutation;
using B = OS.Storage.WritableBlockSession;
using W = OS.Kernel.Compatibility.LinuxX64.LinuxContinuingWritableDescriptorServices;
using F = OS.Kernel.Compatibility.LinuxX64.LinuxDescriptorServices;

internal static unsafe partial class LinuxWritableDiskDescriptorServicesTests
{
    private const ulong Key = LinuxThreadServicesTests.Key, User = LinuxThreadServicesTests.UserBase;
    private const ulong Path = User + 0x1200, Vector = User + 0x1400, Flock = User + 0x1500, Output = User + 0x2000, Payload = User + 0x3000;
    private static readonly RetainTree Tree = RetainMethod(typeof(F.State).MakePointerType(), typeof(ulong), typeof(ulong)).CreateDelegate<RetainTree>();
    private static readonly RetainTreeWithUserBacking TreeWithUserBacking = RetainMethod(typeof(F.State).MakePointerType(), typeof(ulong), typeof(ulong), typeof(bool)).CreateDelegate<RetainTreeWithUserBacking>();
    private static MethodInfo RetainMethod(params Type[] arguments) => typeof(F).GetMethod("WritableDiskCanRetainStorage", BindingFlags.Static | BindingFlags.NonPublic, binder: null, types: arguments, modifiers: null)!;
    private static byte Value(int i) => unchecked((byte)(i * 17 + 5));
    // Read the actual request POD without constructing an operation token or
    // treating a caller-supplied numeric ordinal as a genuine gateway witness.
    internal static (ulong Ordinal, ulong Rip) CopiedCall(F.WritableDiskState* disk)
    {
        object request = typeof(F.WritableDiskState).GetField("Request", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(*disk)!;
        return ((ulong)typeof(F.WritableDiskRequest).GetField("CallOrdinal", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(request)!, (ulong)typeof(F.WritableDiskRequest).GetField("OriginalRip", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(request)!);
    }

    [HostTest("writable copied ordinal retains its original RIP until completion and legacy zero keeps its old unobserved behavior")]
    private static void CopiedOrdinalCompletion()
    {
        foreach (ulong ordinal in new ulong[]
        {
            0,
            37,
            ulong.MaxValue
        }

        )
        {
            using var f = new Owner();
            uint fd = f.Open();
            var call = f.Call(0, fd, Output, 16);
            var begun = ordinal == 0 ? f.Begin(call) : F.BeginWritableDisk(f.Files.State, f.ThreadOwner, f.Worker, call, ordinal);
            HostCheck.Equal(F.WritableDiskProgress.Pending, begun.Progress);
            var expected = (ordinal, ordinal == 0 ? 0UL : call.Rip);
            HostCheck.Equal(expected, CopiedCall(f.Disk));
            byte[] held = new ReadOnlySpan<byte>(f.Disk, sizeof(F.WritableDiskState)).ToArray();
            uint copies = f.Thread.Writes;
            var wrong = call;
            wrong.Rip++;
            if (ordinal != 0)
            {
                HostCheck.Equal(F.WritableDiskProgress.Rejected, F.CompleteWritableDisk(f.Files.State, f.ThreadOwner, f.Worker, begun.Token, wrong).Progress);
                HostCheck.True(held.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(f.Disk, sizeof(F.WritableDiskState))) && f.Thread.Writes == copies);
            }

            HostCheck.Equal(F.WritableDiskProgress.Ready, f.Ready(begun.Token).Progress);
            HostCheck.Equal(expected, CopiedCall(f.Disk));
            if (ordinal != 0)
            {
                held = new ReadOnlySpan<byte>(f.Disk, sizeof(F.WritableDiskState)).ToArray();
                HostCheck.Equal(F.WritableDiskProgress.Rejected, F.CompleteWritableDisk(f.Files.State, f.ThreadOwner, f.Worker, begun.Token, wrong).Progress);
                HostCheck.True(held.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(f.Disk, sizeof(F.WritableDiskState))) && f.Provider->RequestActive && f.Thread.Writes == copies);
            }

            // Legacy completion deliberately never bound its RIP.
            HostCheck.Equal(16L, F.CompleteWritableDisk(f.Files.State, f.ThreadOwner, f.Worker, begun.Token, ordinal == 0 ? wrong : call).Value);
            HostCheck.True(f.Bytes(16).SequenceEqual(Enumerable.Range(0, 16).Select(Value)) && f.Thread.Writes == copies + 1);
            HostCheck.Equal((0UL, 0UL), CopiedCall(f.Disk));
            HostCheck.True(!f.Disk->RequestActive && F.Audit(f.Files.State));
            HostCheck.Equal(F.WritableDiskProgress.Rejected, F.CompleteWritableDisk(f.Files.State, f.ThreadOwner, f.Worker, begun.Token, call).Progress);
            HostCheck.Equal(16L, f.Run(f.Call(8, fd, 0, 1)));
            // The explicit zero overload also clears both fields, even with a real RIP.
            var zero = F.BeginWritableDisk(f.Files.State, f.ThreadOwner, f.Worker, call, 0);
            HostCheck.Equal((0UL, 0UL), CopiedCall(f.Disk));
            f.Ready(zero.Token);
            HostCheck.Equal(16L, F.CompleteWritableDisk(f.Files.State, f.ThreadOwner, f.Worker, zero.Token, call).Value);
        }

        Console.WriteLine($"HOST_COPIED_ORDINAL_LAYOUT request={sizeof(F.WritableDiskRequest)} attachment={sizeof(F.WritableDiskState)} admission={sizeof(F.WritableDiskAdmission)} global={sizeof(F.State)}");
    }

    [HostTest("writable copied ordinal rejects malformed RIP and inconsistent retained pairs before input copy or effects")]
    private static void CopiedOrdinalAdmission()
    {
        using var f = new Owner();
        f.PutPath("/data/DATA.BIN");
        var call = f.Call(2, Path);
        byte[] before = new ReadOnlySpan<byte>(f.Disk, sizeof(F.WritableDiskState)).ToArray();
        uint hardware = f.Hardware->Calls, copies = f.Thread.Writes;
        foreach (ulong rip in new ulong[]
        {
            0,
            4095,
            LinuxSyscallGateway.UserEnd,
            ulong.MaxValue
        }

        )
        {
            var malformed = call;
            malformed.Rip = rip;
            HostCheck.Equal(F.WritableDiskProgress.Rejected, F.BeginWritableDisk(f.Files.State, f.ThreadOwner, f.Worker, malformed, 1).Progress);
            HostCheck.True(before.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(f.Disk, sizeof(F.WritableDiskState))) && f.Hardware->Calls == hardware && f.Thread.Writes == copies && !f.Provider->RequestActive);
        }

        var begun = F.BeginWritableDisk(f.Files.State, f.ThreadOwner, f.Worker, call, 73);
        int request = (int)Marshal.OffsetOf<F.WritableDiskState>("Request");
        ulong* ordinal = (ulong*)((byte*)f.Disk + request + (int)Marshal.OffsetOf<F.WritableDiskRequest>("CallOrdinal"));
        ulong* ripField = (ulong*)((byte*)f.Disk + request + (int)Marshal.OffsetOf<F.WritableDiskRequest>("OriginalRip"));
        try
        {
            *ordinal = 0;
            HostCheck.True(!F.Audit(f.Files.State));
            *ordinal = 73;
            *ripField = 0;
            HostCheck.True(!F.Audit(f.Files.State));
            *ripField = LinuxSyscallGateway.UserEnd;
            HostCheck.True(!F.Audit(f.Files.State));
        }
        finally
        {
            *ordinal = 73;
            *ripField = call.Rip;
        }

        HostCheck.True(F.Audit(f.Files.State));
        f.Ready(begun.Token);
        HostCheck.Equal(3L, F.CompleteWritableDisk(f.Files.State, f.ThreadOwner, f.Worker, begun.Token, call).Value);
        try
        {
            *ordinal = 73;
            *ripField = call.Rip;
            HostCheck.True(!F.Audit(f.Files.State));
        }
        finally
        {
            *ordinal = 0;
            *ripField = 0;
        }

        HostCheck.Equal(0L, f.Core(3, 3));
        HostCheck.Equal(B.Status.Success, B.Stop(f.Storage.Device.State));
        HostCheck.Equal(-5L, F.BeginWritableDisk(f.Files.State, f.ThreadOwner, f.Worker, call, 74).Value);
        HostCheck.Equal((0UL, 0UL), CopiedCall(f.Disk));
    }

    [HostTest("writable copied ordinal survives orphan and revoked fd drain then clears before a fresh legacy request")]
    private static void CopiedOrdinalRevocation()
    {
        foreach (bool close in new[]
        {
            false,
            true
        }

        )
        {
            using var f = new Owner();
            uint fd = f.Open();
            var call = f.Call(0, fd, Output, 16);
            var begun = F.BeginWritableDisk(f.Files.State, f.ThreadOwner, f.Worker, call, 81);
            f.Pump();
            HostCheck.True(f.Hardware->Pending);
            uint copies = f.Thread.Writes;
            var expected = (81UL, call.Rip);
            if (close)
                HostCheck.Equal(0L, f.Core(3, fd));
            else
                HostCheck.Equal(F.WritableDiskProgress.Draining, F.CancelWritableDisk(f.Files.State, f.ThreadOwner, begun.Token, true).Progress);
            HostCheck.Equal(expected, CopiedCall(f.Disk));
            if (close)
            {
                f.PutPath("/app/fixture.data");
                HostCheck.Equal((long)fd, f.Core(2, Path, 0x88100));
                f.Ready(begun.Token);
                HostCheck.Equal(expected, CopiedCall(f.Disk));
                HostCheck.Equal(-9L, F.CompleteWritableDisk(f.Files.State, f.ThreadOwner, f.Worker, begun.Token, call).Value);
            }
            else
                f.Drain();
            HostCheck.True(f.Thread.Writes == copies && !f.Disk->RequestActive && F.Audit(f.Files.State));
            HostCheck.Equal((0UL, 0UL), CopiedCall(f.Disk));
            HostCheck.Equal(F.WritableDiskProgress.Rejected, F.CompleteWritableDisk(f.Files.State, f.ThreadOwner, f.Worker, begun.Token, call).Progress);
            uint fresh = f.Open();
            var next = f.Call(17, fresh, Output, 1);
            var legacy = f.Begin(next);
            HostCheck.Equal((0UL, 0UL), CopiedCall(f.Disk));
            f.Ready(legacy.Token);
            HostCheck.Equal(1L, F.CompleteWritableDisk(f.Files.State, f.ThreadOwner, f.Worker, legacy.Token, next).Value);
        }
    }

    [HostTest("writable copied ordinal remains with dirty recovery backing after cancellation revokes its worker")]
    private static void CopiedOrdinalDirtyRetention()
    {
        using var f = new Owner();
        uint fd = f.Open(flags: 2);
        var call = f.WriteCall(fd, new byte[] { 19, 23 }, 5);
        var begun = F.BeginWritableDisk(f.Files.State, f.ThreadOwner, f.Worker, call, 91);
        for (int i = 0; i < 100000 && !f.Storage.State->Dirty; i++)
        {
            f.Pump();
            f.CompleteHardware();
        }

        HostCheck.True(f.Storage.State->Dirty);
        f.AllowRetainedFailure = true;
        var cancelled = F.CancelWritableDisk(f.Files.State, f.ThreadOwner, begun.Token, true);
        HostCheck.True(cancelled.Progress == F.WritableDiskProgress.RecoveryRequired && !cancelled.Worker.IsValid && f.Disk->RequestActive);
        HostCheck.Equal((91UL, call.Rip), CopiedCall(f.Disk));
        HostCheck.True(F.Audit(f.Files.State) && !F.DetachWritableDisk(f.Files.State) && !F.End(f.Files.State));
        HostCheck.Equal(F.WritableDiskProgress.Rejected, F.CompleteWritableDisk(f.Files.State, f.ThreadOwner, f.Worker, begun.Token, call).Progress);
        HostCheck.Equal((91UL, call.Rip), CopiedCall(f.Disk));
    }

    internal static (string Name, string Alias, uint First, uint Clusters, int Bytes, byte Salt)[] SqliteFootprints() => [("DB80.BIN", "DB80    BIN", 8, 80, 40960, 31), ("DB592.BIN", "DB592   BIN", 128, 592, 303104, 47), ("JOURNAL.BIN", "JOURNAL BIN", 768, 608, 310816, 61)];
    internal static byte[] FootprintBytes(int bytes, byte salt, int offset = 0) => Enumerable.Range(offset, bytes).Select(i => unchecked((byte)(i * 29 + salt))).ToArray();
    // Independently authored initial fake-media bytes at measured lengths,
    // not SQLite pages or engine-issued writes. Every later open still walks
    // the actual mirrored FAT through the continuing engine and sole B owner.
    internal static void SeedSqliteFootprints(ContinuingFat32MutationTests.Fixture storage)
    {
        var parsed = Fat32Parser.ReadBpb(new PersistentDevice(storage.Device.Hardware), storage.Input.PartitionFirst);
        HostCheck.True(parsed.Succeeded && parsed.Bpb.SectorsPerCluster == 1 && parsed.Bpb.RootCluster == 2);
        var bpb = parsed.Bpb;
        Seed(storage.Volatile);
        Seed(storage.Persistent);
        void Seed(Span<byte> image)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(image.Slice((int)(storage.Input.PartitionFirst * 512 + 48)), 0xffff);
            int entry = (int)(bpb.FirstDataLba * 512 + 64);
            foreach (var file in SqliteFootprints())
            {
                HostCheck.True(file.Clusters == (file.Bytes + 511U) / 512 && file.First + file.Clusters < bpb.ClusterCount + 2);
                Span<byte> raw = image.Slice(entry, 32);
                raw.Clear();
                Encoding.ASCII.GetBytes(file.Alias).CopyTo(raw);
                raw[11] = 0x20;
                BinaryPrimitives.WriteUInt16LittleEndian(raw[20..], (ushort)(file.First >> 16));
                BinaryPrimitives.WriteUInt16LittleEndian(raw[26..], (ushort)file.First);
                BinaryPrimitives.WriteUInt32LittleEndian(raw[28..], (uint)file.Bytes);
                for (uint i = 0; i < file.Clusters; i++)
                {
                    uint cluster = file.First + i, next = i + 1 == file.Clusters ? 0x0fffffffU : cluster + 1;
                    for (uint fat = 0; fat < 2; fat++)
                        BinaryPrimitives.WriteUInt32LittleEndian(image.Slice((int)((bpb.FirstFatLba + fat * bpb.FatSizeSectors) * 512 + cluster * 4)), next);
                    Span<byte> sector = image.Slice((int)((bpb.FirstDataLba + cluster - 2) * 512), 512);
                    sector.Fill(0xd3);
                    for (int j = 0; j < sector.Length && (ulong)i * 512 + (uint)j < (uint)file.Bytes; j++)
                        sector[j] = unchecked((byte)(((int)i * 512 + j) * 29 + file.Salt));
                }

                entry += 32;
            }

            image[entry] = 0;
        }
    }

    [HostTest("writable global synthetic SQLite footprints distinguish explicit chain work from free search budgets without publishing failed opens")]
    private static void SqliteFootprintOpenBudgets()
    {
        foreach (uint work in new uint[]
        {
            100000,
            1000000
        }

        )
        {
            using var f = new Owner(sqliteFootprints: true, workBudget: work);
            ulong writes = f.Storage.Device.State->SubmittedBytes;
            foreach (var file in SqliteFootprints())
            {
                uint copies = f.Thread.Writes, hardware = f.Hardware->Notifies;
                int trace = f.Trace.Count;
                f.PutPath("/data/" + file.Name);
                long opened = f.Run(f.Call(2, Path));
                if (work == 100000 && file.Clusters > 80)
                {
                    HostCheck.Equal(-75L, opened); // Existing C.BudgetExceeded -> EOVERFLOW, not ENOSPC.
                    HostCheck.True(!F.Capture(f.Files.State, 3, out _) && f.Thread.Writes == copies && f.Hardware->Notifies > hardware && f.Trace.Skip(trace).All(row => row.Type == 0) && f.Storage.Device.State->SubmittedBytes == writes && !f.Disk->RequestActive && !f.Provider->RequestActive && !f.Storage.State->RequestActive && !f.Storage.State->Dirty && F.Audit(f.Files.State));
                    continue;
                }

                HostCheck.True(opened >= 3);
                uint fd = (uint)opened;
                HostCheck.Equal(17L, f.Run(f.Call(8, fd, 17)));
                HostCheck.Equal(0L, f.Run(f.Call(5, fd, Output)));
                byte[] facts = f.Bytes(144);
                HostCheck.True(BinaryPrimitives.ReadUInt64LittleEndian(facts.AsSpan(48)) == (ulong)file.Bytes && BinaryPrimitives.ReadUInt64LittleEndian(facts.AsSpan(56)) == 512 && BinaryPrimitives.ReadUInt64LittleEndian(facts.AsSpan(64)) == file.Clusters);
                HostCheck.Equal(8192L, f.Run(f.Call(17, fd, Output, 8192, (ulong)file.Bytes - 8192)));
                HostCheck.True(f.Bytes(8192).AsSpan().SequenceEqual(FootprintBytes(8192, file.Salt, file.Bytes - 8192)));
                HostCheck.Equal(17L, f.Run(f.Call(8, fd, 0, 1)));
                HostCheck.Equal(0L, f.Core(3, fd));
                uint reopened = f.Open("/data/" + file.Name);
                HostCheck.Equal(0L, f.Run(f.Call(8, reopened, 0, 1)));
                HostCheck.Equal(0L, f.Core(3, reopened));
                f.Independent(file.Name, FootprintBytes(file.Bytes, file.Salt));
            }

            HostCheck.True(f.Storage.Device.State->SubmittedBytes == writes && f.Trace.All(row => row.Type == 0));
        }
    }

    [HostTest("writable global explicit free search admits a sixteen cluster growth while the unchanged sixty four cluster default rejects without OUT")]
    private static void SqliteFootprintGrowthBudgets()
    {
        foreach (bool largeSearch in new[]
        {
            false,
            true
        }

        )
        {
            using var f = new Owner(sqliteFootprints: true, workBudget: 1000000, search: largeSearch ? new W.FreeSearch(2, 4096, 8192) : null);
            var geometry = Fat32Parser.ReadBpb(new PersistentDevice(f.Hardware), f.Storage.Input.PartitionFirst);
            HostCheck.True(geometry.Succeeded);
            var bpb = geometry.Bpb;
            byte[] guardedEntries = f.Storage.Persistent.Slice((int)(bpb.FirstDataLba * 512), 64).ToArray();
            var files = SqliteFootprints();
            uint fd = f.Open("/data/" + files[0].Name, 2);
            uint independent = f.Open("/data/" + files[1].Name);
            HostCheck.Equal(23L, f.Run(f.Call(8, fd, 23)));
            byte[] payload = FootprintBytes(8192, 83);
            uint copies = f.Thread.Writes;
            int trace = f.Trace.Count;
            ulong writeBytes = f.Storage.Device.State->SubmittedBytes;
            HostCheck.Equal(largeSearch ? 8192L : -11L, f.Run(f.WriteCall(fd, payload, 40960)));
            HostCheck.Equal(copies, f.Thread.Writes);
            HostCheck.Equal(23L, f.Run(f.Call(8, fd, 0, 1)));
            HostCheck.True(!f.Disk->RequestActive && !f.Provider->RequestActive && !f.Storage.State->RequestActive && !f.Storage.State->Dirty);
            if (!largeSearch)
                HostCheck.True(f.Trace.Skip(trace).All(row => row.Type == 0) && f.Storage.Device.State->SubmittedBytes == writeBytes);
            else
            {
                HostCheck.True(f.Trace.Skip(trace).Any(row => row.Type == 1) && f.Trace.Skip(trace).Any(row => row.Type == 4) && f.Storage.Device.State->SubmittedBytes > writeBytes);
                ulong submitted = f.Storage.Device.State->Submitted, bytes = f.Storage.Device.State->SubmittedBytes;
                HostCheck.Equal(0L, f.Run(f.Call(74, fd)));
                HostCheck.True(f.Storage.Device.State->Submitted == submitted + 1 && f.Storage.Device.State->Completed == f.Storage.Device.State->Submitted && f.Storage.Device.State->DurableBytes == bytes);
            }

            HostCheck.Equal(0L, f.Core(3, fd));
            fd = f.Open("/data/" + files[0].Name);
            HostCheck.Equal(0L, f.Run(f.Call(5, fd, Output)));
            HostCheck.Equal(largeSearch ? 49152UL : 40960UL, BinaryPrimitives.ReadUInt64LittleEndian(f.Bytes(144).AsSpan(48)));
            HostCheck.Equal(0L, f.Run(f.Call(8, independent, 0, 1)));
            HostCheck.Equal(0L, f.Core(3, independent));
            HostCheck.Equal(0L, f.Core(3, fd));
            byte[] expected = FootprintBytes(40960, files[0].Salt);
            if (largeSearch)
                expected = [..expected, ..payload];
            f.Independent(files[0].Name, expected);
            foreach (var file in files.Skip(1))
                f.Independent(file.Name, FootprintBytes(file.Bytes, file.Salt));
            f.Independent("KEEP.TXT", new byte[3]);
            f.Independent("DATA.BIN", Enumerable.Range(0, 900).Select(Value).ToArray());
            HostCheck.True(f.Storage.Persistent.Slice((int)(bpb.FirstDataLba * 512), 64).SequenceEqual(guardedEntries) && f.Storage.Persistent.Slice((int)(bpb.FirstFatLba * 512), (int)bpb.FatSizeSectors * 512).SequenceEqual(f.Storage.Persistent.Slice((int)((bpb.FirstFatLba + bpb.FatSizeSectors) * 512), (int)bpb.FatSizeSectors * 512)) && f.Storage.Persistent.Slice((int)((bpb.FirstDataLba + files[2].First + files[2].Clusters - 3) * 512 + 32), 480).IndexOfAnyExcept((byte)0xd3) < 0);
        }
    }

    [HostTest("writable measured create modes0600 and0644 use fixed mask077 and fresh effective metadata without persisted Unix permissions")]
    private static void CreateModePolicy()
    {
        foreach (ulong number in new ulong[]
        {
            2,
            257
        }

        )
            foreach (ulong mode in new ulong[]
            {
                0x180,
                0x1a4
            }

            )
            {
                using var f = new Owner();
                f.PutPath("/data/contract.db");
                const uint flags = 2 | W.Create | W.Exclusive | W.NoFollow | W.CloseOnExec | W.LargeFile;
                var call = number == 2 ? f.Call(2, Path, flags, mode) : f.Call(257, unchecked((ulong)-100L), Path, flags, mode);
                HostCheck.Equal(3L, f.Run(call));
                HostCheck.Equal(1L, f.Core(72, 3, 1));
                HostCheck.Equal(0L, f.Run(f.Call(5, 3, Output)));
                byte[] facts = f.Bytes(144);
                HostCheck.True(BitConverter.ToUInt32(facts, 24) == 0x8180 && BitConverter.ToUInt32(facts, 28) == 0 && BitConverter.ToUInt32(facts, 32) == 0 && BitConverter.ToUInt64(facts, 48) == 0 && BitConverter.ToUInt64(facts, 64) == 0);
                foreach (ulong stat in new ulong[]
                {
                    4,
                    6
                }

                )
                {
                    f.PutPath("/data/contract.db");
                    HostCheck.Equal(0L, f.Run(f.Call(stat, Path, Output)));
                    HostCheck.True(f.Bytes(144).SequenceEqual(facts));
                }

                ulong inode = BitConverter.ToUInt64(facts, 8);
                ulong entry = (inode - 1) * 32;
                HostCheck.Equal((byte)0x20, f.Storage.Persistent[(int)entry + 11]); // FAT archive, with no Unix permission storage.
                f.Independent("contract.db", []);
                HostCheck.True(F.Capture(f.Files.State, 3, out var original));
                HostCheck.Equal(0L, f.Core(3, 3));
                f.PutPath("/data/contract.db");
                var reopen = number == 2 ? f.Call(2, Path, 2 | W.Create, mode) : f.Call(257, unchecked((ulong)-100L), Path, 2 | W.Create, mode);
                ulong writes = f.Storage.Device.State->SubmittedBytes, revision = f.Storage.State->VolumeRevision;
                HostCheck.Equal(3L, f.Run(reopen));
                HostCheck.True(!F.IsCurrent(f.Files.State, original));
                HostCheck.Equal(0L, f.Run(f.Call(5, 3, Output)));
                HostCheck.Equal(inode, BitConverter.ToUInt64(f.Bytes(144), 8));
                HostCheck.True(f.Storage.Device.State->SubmittedBytes == writes && f.Storage.State->VolumeRevision == revision);
                HostCheck.Equal(-17L, f.Run(call));
                HostCheck.True(F.Audit(f.Files.State));
            }
    }

    [HostTest("writable create mode policy rejects every other requested permission before reservation or IO and ignores mode without create")]
    private static void CreateModeRejection()
    {
        using var f = new Owner();
        uint calls = f.Hardware->Calls;
        ulong submitted = f.Storage.Device.State->Submitted;
        foreach (ulong number in new ulong[]
        {
            2,
            257
        }

        )
            foreach (ulong mode in new ulong[]
            {
                0,
                0x100,
                0x1a0,
                0x1b6,
                0x1c0,
                0x1ff,
                0x8180,
                0x100000180,
                ulong.MaxValue
            }

            )
            {
                f.PutPath("/data/must-not-create.db");
                var call = number == 2 ? f.Call(2, Path, 2 | W.Create, mode) : f.Call(257, unchecked((ulong)-100L), Path, 2 | W.Create, mode);
                HostCheck.Equal(-95L, f.Run(call));
                HostCheck.True(!f.Disk->RequestActive && !f.Provider->RequestActive && f.Files.State->ActiveDescriptors == 3 && F.Audit(f.Files.State));
            }

        HostCheck.True(f.Hardware->Calls == calls && f.Storage.Device.State->Submitted == submitted && f.Storage.State->VolumeRevision == 1);
        f.PutPath("/data/DATA.BIN");
        HostCheck.Equal(3L, f.Run(f.Call(2, Path, 0, ulong.MaxValue)));
        HostCheck.Equal(-38L, f.Core(93, 3, 0, 0));
        HostCheck.Equal(-38L, f.Core(138, 3, Output));
    }

    [HostTest("writable0644 admission retains global capacity and exact reservation cancellation and ready winner generations")]
    private static void CreateModeCapacityAndCancellation()
    {
        foreach (bool fdFull in new[]
        {
            true,
            false
        }

        )
        {
            using var full = new Owner(descriptors: fdFull ? 3U : 32U, descriptions: 3);
            full.PutPath("/data/no-space.db");
            uint calls = full.Hardware->Calls;
            HostCheck.Equal(fdFull ? -24L : -23L, full.Run(full.Call(2, Path, 2 | W.Create, 0x1a4)));
            HostCheck.True(full.Hardware->Calls == calls && !full.Disk->RequestActive && !full.Provider->RequestActive && full.Storage.State->VolumeRevision == 1);
        }

        using (var f = new Owner())
        {
            f.PutPath("/data/cancelled.db");
            var call = f.Call(2, Path, 2 | W.Create | W.Exclusive, 0x1a4);
            var begun = f.Begin(call);
            HostCheck.Equal(F.WritableDiskProgress.Pending, begun.Progress);
            HostCheck.True(!F.Capture(f.Files.State, 3, out _));
            var cancelled = F.CancelWritableDisk(f.Files.State, f.ThreadOwner, begun.Token, true);
            HostCheck.True(cancelled.Progress == F.WritableDiskProgress.Draining && !cancelled.Worker.IsValid);
            f.PutPath("/app/fixture.data");
            HostCheck.Equal(4L, f.Core(2, Path, 0x88100));
            f.Drain();
            HostCheck.True(f.Storage.Device.State->SubmittedBytes == 0 && f.Storage.State->VolumeRevision == 1);
            HostCheck.Equal(3L, f.Core(2, Path, 0x88100));
            HostCheck.Equal(F.WritableDiskProgress.Rejected, F.CompleteWritableDisk(f.Files.State, f.ThreadOwner, f.Worker, begun.Token, call).Progress);
        }

        using (var f = new Owner())
        {
            f.PutPath("/data/ready.db");
            var call = f.Call(257, unchecked((ulong)-100L), Path, 2 | W.Create | W.Exclusive, 0x1a4);
            var begun = f.Begin(call);
            HostCheck.Equal(F.WritableDiskProgress.Ready, f.Ready(begun.Token).Progress);
            uint calls = f.Hardware->Calls;
            var winner = F.CancelWritableDisk(f.Files.State, f.ThreadOwner, begun.Token, false);
            HostCheck.True(winner.Progress == F.WritableDiskProgress.Ready && winner.Worker.Value == f.Worker.Value);
            HostCheck.Equal(3L, F.CompleteWritableDisk(f.Files.State, f.ThreadOwner, f.Worker, begun.Token, call).Value);
            HostCheck.Equal(calls, f.Hardware->Calls);
            HostCheck.True(F.Capture(f.Files.State, 3, out var original));
            HostCheck.Equal(0L, f.Core(3, 3));
            uint reused = f.Open();
            HostCheck.Equal(3U, reused);
            HostCheck.True(!F.IsCurrent(f.Files.State, original));
            HostCheck.Equal(F.WritableDiskProgress.Rejected, F.CompleteWritableDisk(f.Files.State, f.ThreadOwner, f.Worker, begun.Token, call).Progress);
            HostCheck.Equal(0L, f.Run(f.Call(8, reused, 0, 1)));
            HostCheck.True(F.Audit(f.Files.State));
        }
    }

    [HostTest("writable0644 creation reports actual lookup IO failure without fd publication or mutation")]
    private static void CreateModeIoFailure()
    {
        using var f = new Owner();
        f.PutPath("/data/failed.db");
        var call = f.Call(2, Path, 2 | W.Create, 0x1a4);
        var begun = f.Begin(call);
        f.Pump();
        HostCheck.True(f.Hardware->Pending && f.Hardware->Type == 0);
        f.Storage.Device.Complete(length: 1, status: 1);
        HostCheck.Equal(F.WritableDiskProgress.Ready, f.Ready(begun.Token).Progress);
        HostCheck.Equal(-5L, F.CompleteWritableDisk(f.Files.State, f.ThreadOwner, f.Worker, begun.Token, call).Value);
        HostCheck.True(f.Storage.Device.State->SubmittedBytes == 0 && f.Storage.State->VolumeRevision == 1 && f.Files.State->ActiveDescriptors == 3 && !F.Capture(f.Files.State, 3, out _) && F.Audit(f.Files.State));
    }

    [HostTest("writable global fresh stat and lstat match actual fstat bytes using copied path even when output aliases original path")]
    private static void FreshPathWire()
    {
        foreach (byte sectors in new byte[]
        {
            1,
            4
        }

        )
        {
            using var f = new Owner(sectorsPerCluster: sectors);
            uint fd = f.Open();
            f.Run(f.Call(8, fd, 29, 0));
            HostCheck.Equal(0L, f.Run(f.Call(5, fd, Output)));
            byte[] expected = f.Bytes(144);
            uint descriptors = f.Files.State->ActiveDescriptors;
            foreach (ulong nr in new ulong[]
            {
                4,
                6
            }

            )
            {
                f.PutPath("/data/DATA.BIN");
                var call = f.Call(nr, Path, Output);
                var request = f.Begin(call);
                f.PutPath("/proc/meminfo");
                HostCheck.Equal(F.WritableDiskProgress.Ready, f.Ready(request.Token).Progress);
                HostCheck.Equal(0L, F.CompleteWritableDisk(f.Files.State, f.ThreadOwner, f.Worker, request.Token, call, 144).Value);
                HostCheck.True(expected.SequenceEqual(f.Bytes(144)) && f.Files.State->ActiveDescriptors == descriptors);
                f.PutPath("/data/DATA.BIN");
                HostCheck.Equal(0L, f.Run(f.Call(nr, Path, Path)));
                HostCheck.True(expected.SequenceEqual(new ReadOnlySpan<byte>(f.Thread.At(Path), 144).ToArray()));
            }

            HostCheck.Equal(29L, f.Run(f.Call(8, fd, 0, 1)));
            HostCheck.True(F.Audit(f.Files.State));
        }
    }

    [HostTest("writable path root inode namespace is disjoint from a real preceding fragmented directory slot and reports actual allocation")]
    private static void RootInodeAndAllocation()
    {
        using var f = new Owner(rootCollision: true);
        ulong rootSector = f.Storage.Input.PartitionFirst + 32 + 2 * 512 + 128;
        byte[]? rootWire = null;
        foreach (ulong nr in new ulong[]
        {
            4,
            6
        }

        )
        {
            f.PutPath("/data");
            HostCheck.Equal(0L, f.Run(f.Call(nr, Path, Output)));
            byte[] actual = f.Bytes(144), expected = new byte[144];
            BitConverter.GetBytes(0x8000000000000000UL | rootSector).CopyTo(expected, 8);
            BitConverter.GetBytes(1UL).CopyTo(expected, 16);
            BitConverter.GetBytes(0x41c0U).CopyTo(expected, 24);
            BitConverter.GetBytes(1024UL).CopyTo(expected, 48);
            BitConverter.GetBytes(512UL).CopyTo(expected, 56);
            BitConverter.GetBytes(2UL).CopyTo(expected, 64);
            HostCheck.True(actual.SequenceEqual(expected));
            rootWire = actual;
        }

        f.PutPath("/data/DATA.BIN");
        HostCheck.Equal(0L, f.Run(f.Call(4, Path, Output)));
        ulong fileInode = BitConverter.ToUInt64(f.Bytes(144), 8), rootInode = BitConverter.ToUInt64(rootWire!, 8);
        HostCheck.Equal(rootSector * 16, fileInode); // This actual entry defeats an untagged root-sector encoding.
        HostCheck.True(rootInode != fileInode && fileInode < (1UL << 63) && rootInode >= (1UL << 63));
        HostCheck.True(rootInode > ulong.MaxValue / 32 + 1); // Any possible regular entry inode is below the namespace tag.
        HostCheck.True(f.Files.State->ActiveDescriptors == 3 && f.Trace.All(row => row.Type == 0));
    }

    [HostTest("writable path metadata uses real missing responses preserves syscall output ceiling and refuses failed user copies")]
    private static void PathMissingAndOutput()
    {
        using var f = new Owner();
        uint before = f.Files.State->ActiveDescriptors;
        foreach (ulong nr in new ulong[]
        {
            4,
            6
        }

        )
            foreach (string name in new[]
            {
                "/data/contract.db-journal",
                "/data/contract.db-wal",
                "/data/contract.db-shm"
            }

            )
            {
                f.PutPath(name);
                HostCheck.Equal(-2L, f.Run(f.Call(nr, Path, Output)));
            }

        foreach (string name in new[]
        {
            "/data",
            "/data/DATA.BIN"
        }

        )
        {
            f.PutPath(name);
            var call = f.Call(4, Path, Output);
            var request = f.Begin(call);
            f.Ready(request.Token);
            new Span<byte>(f.Thread.At(Output), 145).Fill(0xa5);
            uint copies = f.Thread.Writes;
            HostCheck.Equal(-14L, F.CompleteWritableDisk(f.Files.State, f.ThreadOwner, f.Worker, request.Token, call, 143).Value);
            HostCheck.True(f.Thread.Writes == copies && f.Bytes(145).All(value => value == 0xa5));
            f.Thread.FailWrite(Output);
            HostCheck.Equal(-14L, f.Run(call));
            HostCheck.True(f.Bytes(145).All(value => value == 0xa5));
            f.Thread.FailWrite(0);
            HostCheck.Equal(0L, f.Run(call));
            HostCheck.True(f.Thread.At(Output)[144] == 0xa5);
        }

        HostCheck.True(f.Files.State->ActiveDescriptors == before && !f.Provider->RequestActive && !f.Disk->RequestActive);
    }

    [HostTest("writable path stat cancellation revokes pending and held root or file metadata and drains orphan records without an fd borrow")]
    private static void PathCancellation()
    {
        foreach (bool held in new[]
        {
            false,
            true
        }

        )
            foreach (string path in new[]
            {
                "/data",
                "/data/DATA.BIN"
            }

            )
            {
                using var f = new Owner();
                f.PutPath(path);
                var call = f.Call(6, Path, Output);
                var request = f.Begin(call);
                if (held)
                    f.Ready(request.Token);
                else
                {
                    f.Pump();
                    HostCheck.True(f.Hardware->Pending);
                }

                new Span<byte>(f.Thread.At(Output), 144).Fill(0xa5);
                uint copies = f.Thread.Writes;
                var cancelled = F.CancelWritableDisk(f.Files.State, f.ThreadOwner, request.Token, false);
                HostCheck.True(!cancelled.Worker.IsValid);
                f.Drain();
                HostCheck.Equal(F.WritableDiskProgress.Rejected, F.PollWritableDisk(f.Files.State, f.ThreadOwner, request.Token).Progress);
                HostCheck.Equal(F.WritableDiskProgress.Rejected, F.CompleteWritableDisk(f.Files.State, f.ThreadOwner, f.Worker, request.Token, call).Progress);
                HostCheck.True(f.Thread.Writes == copies && f.Bytes(144).All(value => value == 0xa5) && f.Files.State->ActiveDescriptors == 3 && F.Audit(f.Files.State));
            }
    }

    [HostTest("writable path metadata actual IO failure and stopped readiness never copy guessed facts and leave unsupported policy routes explicit")]
    private static void PathFailureAndPolicy()
    {
        using (var f = new Owner())
        {
            f.PutPath("/data");
            var call = f.Call(4, Path, Output);
            var request = f.Begin(call);
            f.Pump();
            HostCheck.True(f.Hardware->Pending);
            f.Storage.Device.Complete(length: 1, status: 1);
            new Span<byte>(f.Thread.At(Output), 144).Fill(0xa5);
            uint copies = f.Thread.Writes;
            HostCheck.Equal(F.WritableDiskProgress.Ready, f.Ready(request.Token).Progress);
            HostCheck.Equal(-5L, F.CompleteWritableDisk(f.Files.State, f.ThreadOwner, f.Worker, request.Token, call).Value);
            HostCheck.True(f.Thread.Writes == copies && f.Bytes(144).All(value => value == 0xa5));
        }

        using (var f = new Owner())
        {
            f.PutPath("/data");
            var call = f.Call(6, Path, Output);
            var request = f.Begin(call);
            f.Ready(request.Token);
            new Span<byte>(f.Thread.At(Output), 144).Fill(0xa5);
            uint copies = f.Thread.Writes;
            B.Stop(f.Storage.Device.State);
            HostCheck.Equal(-5L, F.CompleteWritableDisk(f.Files.State, f.ThreadOwner, f.Worker, request.Token, call).Value);
            HostCheck.True(f.Thread.Writes == copies && f.Bytes(144).All(value => value == 0xa5));
        }

        using (var f = new Owner())
        {
            f.PutPath("/data/../private/sentinel");
            HostCheck.Equal(-13L, f.Run(f.Call(6, Path, Output)));
            f.PutPath("/proc/meminfo");
            HostCheck.Equal(F.WritableDiskProgress.NotHandled, f.Begin(f.Call(4, Path, Output)).Progress);
            f.PutPath("/data/not-created.db");
            HostCheck.Equal(-95L, f.Run(f.Call(2, Path, 2 | W.Create, 0x1a0)));
            HostCheck.Equal(-38L, f.Core(93, 3, 0, 0));
            HostCheck.Equal(-38L, f.Core(138, 3, Output));
            HostCheck.True(!f.Provider->RequestActive && !f.Disk->RequestActive && f.Storage.State->VolumeRevision == 1);
        }
    }

    [HostTest("writable global flock uses Linux operation values and regular open descriptions while filesystem stats remain unsupported")]
    private static void FlockWire()
    {
        using var f = new Owner();
        uint a = f.Open(), b = f.Open(flags: 1), root = f.Open("/data");
        uint calls = f.Hardware->Calls, copies = f.Thread.Writes;
        new Span<byte>(f.Thread.At(Output), 120).Fill(0xa5);
        HostCheck.Equal(0L, f.Run(f.Call(73, a, 5)));
        HostCheck.Equal(-11L, f.Run(f.Call(73, b, 6)));
        HostCheck.Equal(-38L, f.Run(f.Call(73, a, 1)));
        HostCheck.Equal(-22L, f.Run(f.Call(73, a, 7)));
        HostCheck.Equal(0L, f.Run(f.Call(73, a, 8)));
        HostCheck.Equal(0L, f.Run(f.Call(73, b, 6)));
        HostCheck.Equal(-95L, f.Run(f.Call(73, root, 5)));
        HostCheck.Equal(F.WritableDiskProgress.NotHandled, f.Begin(f.Call(138, a, Output)).Progress);
        HostCheck.Equal(-38L, f.Core(138, a, Output));
        HostCheck.True(f.Hardware->Calls == calls && f.Thread.Writes == copies && f.Bytes(120).All(value => value == 0xa5));
        HostCheck.True(F.Audit(f.Files.State));
    }

    [HostTest("writable global flock remains immediate during a clean pending read without inventing contention or advancing the cursor")]
    private static void FlockDuringRead()
    {
        using var f = new Owner();
        uint a = f.Open(), b = f.Open();
        HostCheck.Equal(0L, f.Run(f.Call(73, a, 6)));
        var call = f.Call(0, a, Output, 8);
        var read = f.Begin(call);
        f.Pump();
        HostCheck.True(f.Hardware->Pending);
        uint calls = f.Hardware->Calls;
        HostCheck.Equal(-11L, f.Run(f.Call(73, b, 5)));
        HostCheck.Equal(0L, f.Run(f.Call(73, a, 8)));
        HostCheck.Equal(0L, f.Run(f.Call(73, b, 5)));
        HostCheck.Equal(calls, f.Hardware->Calls);
        HostCheck.Equal(F.WritableDiskProgress.Ready, f.Ready(read.Token).Progress);
        HostCheck.Equal(8L, F.CompleteWritableDisk(f.Files.State, f.ThreadOwner, f.Worker, read.Token, call).Value);
        HostCheck.Equal(8L, f.Run(f.Call(8, a, 0, 1)));
    }

    [HostTest("writable global final close keeps flock until pending borrower drains and reused numeric fd gains no old lock authority")]
    private static void FlockCloseReuse()
    {
        using var f = new Owner();
        uint a = f.Open(), b = f.Open();
        HostCheck.Equal(0L, f.Run(f.Call(73, a, 6)));
        var call = f.Call(0, a, Output, 8);
        var read = f.Begin(call);
        f.Pump();
        HostCheck.True(f.Hardware->Pending);
        HostCheck.Equal(0L, f.Core(3, a));
        f.PutPath("/app/fixture.data");
        HostCheck.Equal((long)a, f.Core(2, Path, 0x88100));
        HostCheck.Equal(F.WritableDiskProgress.NotHandled, f.Begin(f.Call(73, a, 8)).Progress);
        HostCheck.Equal(-11L, f.Run(f.Call(73, b, 5)));
        HostCheck.Equal(F.WritableDiskProgress.Ready, f.Ready(read.Token).Progress);
        HostCheck.Equal(-9L, F.CompleteWritableDisk(f.Files.State, f.ThreadOwner, f.Worker, read.Token, call).Value);
        HostCheck.Equal(0L, f.Run(f.Call(73, b, 6)));
        HostCheck.True(F.Audit(f.Files.State));
    }

    [HostTest("writable global flock keeps BSD modes separate from fcntl process locks and refuses stopped or stale storage")]
    private static void FlockStorageAndFcntl()
    {
        using var f = new Owner();
        uint a = f.Open(flags: 2), b = f.Open(flags: 2);
        HostCheck.Equal(0L, f.Run(f.Call(73, a, 6)));
        f.PutLock(1, 0, 1);
        HostCheck.Equal(0L, f.Run(f.Call(72, b, 6, Flock))); // POSIX and BSD locks are independent.
        HostCheck.Equal(0L, f.Core(3, b));
        HostCheck.Equal(0L, f.Run(f.Call(73, a, 6)));
        HostCheck.Equal(B.Status.Success, B.Stop(f.Storage.Device.State));
        HostCheck.Equal(-5L, f.Run(f.Call(73, a, 8)));
        HostCheck.Equal(0L, f.Core(3, a));
    }

    [HostTest("writable fstat uses real continuing metadata and complete x64 layout with allocated clusters and independent read write cursors")]
    private static void FreshStatWire()
    {
        foreach (byte sectors in new byte[]
        {
            1,
            2,
            4,
            8
        }

        )
        {
            using var f = new Owner(sectorsPerCluster: sectors);
            PutTime(f.DataEntry, 2024, 2, 29, 23, 59, 58);
            uint first = f.Open(), second = f.Open(flags: 1 | W.NoControllingTerminal);
            HostCheck.Equal(7L, f.Run(f.Call(8, first, 7, 0)));
            HostCheck.Equal(29L, f.Run(f.Call(8, second, 29, 0)));
            ulong inode = 0;
            foreach (uint fd in new[]
            {
                first,
                second
            }

            )
            {
                int trace = f.Trace.Count;
                var call = f.Call(5, fd, Output);
                var begun = f.Begin(call);
                HostCheck.Equal(F.WritableDiskProgress.Pending, begun.Progress);
                HostCheck.Equal(F.WritableDiskProgress.Ready, f.Ready(begun.Token).Progress);
                new Span<byte>(f.Thread.At(Output), 145).Fill(0xa5);
                HostCheck.Equal(0L, F.CompleteWritableDisk(f.Files.State, f.ThreadOwner, f.Worker, begun.Token, call, 144).Value);
                byte[] actual = f.Bytes(144), expected = new byte[144];
                ulong physicalInode = (f.Storage.Input.PartitionFirst + 32 + 2 * 512) * 16 + 2;
                BitConverter.GetBytes(physicalInode).CopyTo(expected, 8);
                BitConverter.GetBytes(1UL).CopyTo(expected, 16);
                BitConverter.GetBytes(0x8180U).CopyTo(expected, 24);
                BitConverter.GetBytes(900UL).CopyTo(expected, 48);
                BitConverter.GetBytes((ulong)sectors * 512).CopyTo(expected, 56);
                BitConverter.GetBytes((ulong)(sectors == 1 ? 2 : sectors)).CopyTo(expected, 64);
                BitConverter.GetBytes(new DateTimeOffset(2024, 2, 29, 23, 59, 58, TimeSpan.Zero).ToUnixTimeSeconds()).CopyTo(expected, 88);
                HostCheck.True(actual.SequenceEqual(expected) && f.Thread.At(Output)[144] == 0xa5);
                HostCheck.True(inode == 0 || inode == BitConverter.ToUInt64(actual, 8));
                inode = BitConverter.ToUInt64(actual, 8);
                HostCheck.True(f.Trace.Skip(trace).Any(row => row.Type == 0) && !f.Trace.Skip(trace).Any(row => row.Type != 0));
            }

            HostCheck.Equal(7L, f.Run(f.Call(8, first, 0, 1)));
            HostCheck.Equal(29L, f.Run(f.Call(8, second, 0, 1)));
            HostCheck.True(F.Audit(f.Files.State));
        }
    }

    [HostTest("writable fstat sees authenticated empty create and actual growth with finite readonly mode and explicit directory rejection")]
    private static void StatMutationAndMode()
    {
        using var f = new Owner();
        uint fd = f.Open("/data/stat-new.bin", 2 | W.Create | W.Exclusive);
        HostCheck.Equal(0L, f.Run(f.Call(5, fd, Output)));
        HostCheck.True(BitConverter.ToUInt64(f.Bytes(144), 48) == 0 && BitConverter.ToUInt64(f.Bytes(144), 64) == 0);
        HostCheck.Equal(1032L, f.Run(f.WriteCall(fd, Enumerable.Range(0, 1032).Select(Value).ToArray(), 4094)));
        HostCheck.Equal(0L, f.Run(f.Call(5, fd, Output)));
        byte[] facts = f.Bytes(144);
        HostCheck.True(BitConverter.ToUInt64(facts, 48) == 5126 && BitConverter.ToUInt64(facts, 64) == 11 && BitConverter.ToUInt32(facts, 24) == 0x8180);
        uint directory = f.Open("/data", W.DirectoryOnly);
        uint writes = f.Thread.Writes;
        HostCheck.Equal(-95L, f.Run(f.Call(5, directory, Output)));
        HostCheck.Equal(writes, f.Thread.Writes);
        f.DataEntry[11] = 0x21;
        uint readOnly = f.Open();
        HostCheck.Equal(0L, f.Run(f.Call(5, readOnly, Output)));
        HostCheck.Equal(0x8100U, BitConverter.ToUInt32(f.Bytes(144), 24));
        HostCheck.Equal(-38L, f.Core(73, readOnly, 1));
        HostCheck.Equal(-38L, f.Core(138, readOnly, Output));
    }

    [HostTest("writable fstat enforces exact captured output prefix and user copy failures without moving cursors or writing a short prefix")]
    private static void StatCopyCeiling()
    {
        using var f = new Owner();
        uint fd = f.Open();
        HostCheck.Equal(17L, f.Run(f.Call(8, fd, 17, 0)));
        new Span<byte>(f.Thread.At(Output), 145).Fill(0xa5);
        var call = f.Call(5, fd, Output);
        var begun = f.Begin(call);
        f.Ready(begun.Token);
        uint writes = f.Thread.Writes;
        HostCheck.Equal(-14L, F.CompleteWritableDisk(f.Files.State, f.ThreadOwner, f.Worker, begun.Token, call, 143).Value);
        HostCheck.True(f.Thread.Writes == writes && f.Bytes(145).All(value => value == 0xa5) && !f.Provider->RequestActive);
        f.Thread.FailWrite(Output);
        HostCheck.Equal(-14L, f.Run(call));
        HostCheck.True(f.Bytes(145).All(value => value == 0xa5));
        f.Thread.FailWrite(0);
        HostCheck.Equal(0L, f.Run(call));
        HostCheck.True(f.Thread.At(Output)[144] == 0xa5);
        HostCheck.Equal(17L, f.Run(f.Call(8, fd, 0, 1)));
    }

    [HostTest("writable pending and held stat close revoke copy and wake only original live error waiter after fd reuse")]
    private static void StatCloseAndReuse()
    {
        foreach (bool held in new[]
        {
            false,
            true
        }

        )
        {
            using var f = new Owner();
            uint fd = f.Open();
            var call = f.Call(5, fd, Output);
            var begun = f.Begin(call);
            if (held)
                f.Ready(begun.Token);
            else
            {
                f.Pump();
                HostCheck.True(f.Hardware->Pending);
            }

            HostCheck.True(F.Capture(f.Files.State, fd, out var old));
            HostCheck.Equal(0L, f.Core(3, fd));
            f.PutPath("/app/fixture.data");
            HostCheck.Equal((long)fd, f.Core(2, Path, 0x88100));
            new Span<byte>(f.Thread.At(Output), 144).Fill(0xa5);
            uint writes = f.Thread.Writes;
            var ready = f.Ready(begun.Token);
            HostCheck.True(ready.Worker.Value == f.Worker.Value && ready.Value == -9);
            HostCheck.Equal(-9L, F.CompleteWritableDisk(f.Files.State, f.ThreadOwner, f.Worker, begun.Token, call).Value);
            HostCheck.True(f.Thread.Writes == writes && f.Bytes(144).All(value => value == 0xa5) && !F.IsCurrent(f.Files.State, old) && F.Audit(f.Files.State));
        }
    }

    [HostTest("writable stat cancellation revokes both pending and known ready readonly facts without converting them to mutation result authority")]
    private static void StatCancellation()
    {
        foreach (bool held in new[]
        {
            false,
            true
        }

        )
        {
            using var f = new Owner();
            uint fd = f.Open();
            var call = f.Call(5, fd, Output);
            var begun = f.Begin(call);
            if (held)
                f.Ready(begun.Token);
            else
            {
                f.Pump();
                HostCheck.True(f.Hardware->Pending);
            }

            uint writes = f.Thread.Writes;
            var cancelled = F.CancelWritableDisk(f.Files.State, f.ThreadOwner, begun.Token, false);
            HostCheck.True(!cancelled.Worker.IsValid && cancelled.Progress == F.WritableDiskProgress.Draining);
            f.Drain();
            HostCheck.Equal(F.WritableDiskProgress.Rejected, F.CompleteWritableDisk(f.Files.State, f.ThreadOwner, f.Worker, begun.Token, call).Progress);
            HostCheck.True(f.Thread.Writes == writes && !f.Provider->RequestActive);
            HostCheck.Equal(0L, f.Run(f.Call(8, fd, 0, 1)));
        }
    }

    [HostTest("writable fstat rejects invalid FAT civil timestamps explicitly and preserves zero unknown and Gregorian leap boundaries")]
    private static void StatTimeValidation()
    {
        foreach (var date in new[]
        {
            (1980, 1, 1, 0, 0, 0, true),
            (2000, 2, 29, 23, 59, 58, true),
            (2100, 2, 29, 0, 0, 0, false),
            (2024, 0, 1, 0, 0, 0, false),
            (2024, 1, 0, 0, 0, 0, false),
            (2024, 1, 1, 24, 0, 0, false),
            (2024, 1, 1, 0, 60, 0, false),
            (2024, 1, 1, 0, 0, 60, false),
            (1980, 0, 0, 0, 0, 2, false)
        }

        )
        {
            using var f = new Owner();
            PutTime(f.DataEntry, date.Item1, date.Item2, date.Item3, date.Item4, date.Item5, date.Item6);
            uint fd = f.Open();
            new Span<byte>(f.Thread.At(Output), 144).Fill(0xa5);
            uint writes = f.Thread.Writes;
            var call = f.Call(5, fd, Output);
            var begun = f.Begin(call);
            f.Ready(begun.Token);
            var result = F.CompleteWritableDisk(f.Files.State, f.ThreadOwner, f.Worker, begun.Token, call);
            HostCheck.Equal(date.Item7 ? 0L : -5L, result.Value);
            if (date.Item7)
                HostCheck.Equal(new DateTimeOffset(date.Item1, date.Item2, date.Item3, date.Item4, date.Item5, date.Item6, TimeSpan.Zero).ToUnixTimeSeconds(), BitConverter.ToInt64(f.Bytes(144), 88));
            else
                HostCheck.True(result.Failure == C.Status.CorruptDirectory && f.Thread.Writes == writes && f.Bytes(144).All(value => value == 0xa5));
        }

        using (var f = new Owner())
        {
            uint fd = f.Open();
            HostCheck.Equal(0L, f.Run(f.Call(5, fd, Output)));
            HostCheck.Equal(0L, BitConverter.ToInt64(f.Bytes(144), 88));
        }
    }

    private static void PutTime(Span<byte> entry, int year, int month, int day, int hour, int minute, int second)
    {
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(entry[22..], (ushort)((hour << 11) | (minute << 5) | second / 2));
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(entry[24..], (ushort)(((year - 1980) << 9) | (month << 5) | day));
    }

    [HostTest("writable canonical mechanical descriptor calls execute their exact copied wire values and checked read bytes over the actual shared media")]
    private static void MechanicalDescriptorCalls()
    {
        using var f = new Owner();
        byte[] elf = OS.Tooling.LinuxWritableDiskProofFixture.CreateElf();
        elf.CopyTo(new Span<byte>(f.Thread.At(User), elf.Length));
        byte[] payload = OS.Tooling.LinuxWritableDiskProofFixture.ExpectedPayload();
        var calls = OS.Tooling.LinuxWritableDiskProofFixture.Calls();
        int completed = 0;
        foreach (var spec in calls)
        {
            // Stdout and exit are existing thread/gateway obligations. This
            // host call driver never executes the ELF or grants source5 proof.
            if (spec.Number is 1 or 231)
                continue;
            var call = f.Call(spec.Number, spec.A0, spec.A1, spec.A2, spec.A3, spec.A4, spec.A5);
            var begun = f.Begin(call);
            long value;
            if (begun.Progress == F.WritableDiskProgress.NotHandled)
            {
                var core = f.Thread.DescriptorCall(spec.Number, spec.A0, spec.A1, spec.A2, spec.A3);
                HostCheck.Equal(LinuxSyscallAction.Return, core.Action);
                value = core.Value;
            }
            else if (begun.Progress == F.WritableDiskProgress.Immediate)
                value = begun.Value;
            else
            {
                HostCheck.Equal(F.WritableDiskProgress.Ready, f.Ready(begun.Token).Progress);
                var done = F.CompleteWritableDisk(f.Files.State, f.ThreadOwner, f.Worker, begun.Token, call, spec.OutputBytes);
                HostCheck.Equal(F.WritableDiskProgress.Immediate, done.Progress);
                value = done.Value;
            }

            HostCheck.Equal(spec.Result, value);
            completed++;
            const ulong output = OS.Tooling.LinuxWritableDiskProofFixture.Output;
            if (spec.Role == "read.hole-boundary")
                HostCheck.True(new ReadOnlySpan<byte>(f.Thread.At(output), 16).SequenceEqual(new byte[8].Concat(payload.Take(8)).ToArray()));
            if (spec.Role == "read.hole")
                HostCheck.True(new ReadOnlySpan<byte>(f.Thread.At(output), 16).IndexOfAnyExcept((byte)0) < 0);
            if (spec.Role == "read.payload")
                HostCheck.True(new ReadOnlySpan<byte>(f.Thread.At(output), 8).SequenceEqual(payload.AsSpan(0, 8)));
            if (spec.Role == "read.reopened")
            {
                HostCheck.True(new ReadOnlySpan<byte>(f.Thread.At(output), payload.Length).SequenceEqual(payload) && f.Thread.At(output)[payload.Length] == 0xa5);
                byte[] expected = new byte[5126];
                payload.CopyTo(expected, 4094);
                f.Independent("m16-write.bin", expected);
            }

            if (spec.Role == "lock.get-own")
                HostCheck.Equal(2U, *(uint*)f.Thread.At(OS.Tooling.LinuxWritableDiskProofFixture.QueryLock));
        }

        HostCheck.Equal(calls.Count - 2, completed);
        HostCheck.True(completed == 28 && !f.Disk->RequestActive && !f.Provider->RequestActive);
    }

    [HostTest("writable decoded log upload frames execute translated global wire inputs with exact bytes cursors flush barriers and independent FAT extraction")]
    private static void LogUploadDescriptorCalls()
    {
        // This drives copied syscall values, not x64 instructions or BCL code.
        // The ELF is24KiB but the existing model admits20KiB. Only bounded
        // paths/iovecs/payloads are translated; no pointer escapes that model.
        using var f = new Owner();
        var media = new PersistentDevice(f.Hardware);
        byte[] elf = OS.Tooling.LinuxWritableDiskProofFixture.CreateLogUploadElf();
        byte[] log = OS.Tooling.NativeAotLogUploadContract.ExpectedLog(), upload = OS.Tooling.NativeAotLogUploadContract.ExpectedUpload();
        var specs = OS.Tooling.LinuxWritableDiskProofFixture.LogUploadCalls();
        HostCheck.Equal(40, specs.Count);
        var geometry = Fat32Parser.ReadBpb(media, f.Storage.Input.PartitionFirst);
        HostCheck.True(geometry.Succeeded);
        var bpb = geometry.Bpb;
        HostCheck.Equal((byte)1, bpb.SectorsPerCluster);
        byte[] oldFat = f.Storage.Persistent.Slice((int)(bpb.FirstFatLba * 512), (int)bpb.FatSizeSectors * 512).ToArray();
        byte[] oldEntries = f.Storage.Persistent.Slice((int)(bpb.FirstDataLba * 512), 64).ToArray();
        uint[] guardClusters = [3, 4, 6];
        byte[][] guardSectors = guardClusters.Select(cluster => f.Storage.Persistent.Slice((int)((bpb.FirstDataLba + cluster - 2) * 512), 512).ToArray()).ToArray();
        byte[] preparedBoot = f.Storage.Persistent.Slice((int)(f.Storage.Input.PartitionFirst * 512), 512).ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(preparedBoot.AsSpan(48), 0xffff);
        HostCheck.True(f.Storage.Volatile.Slice((int)(f.Storage.Input.PartitionFirst * 512), 512).SequenceEqual(preparedBoot));
        foreach (string name in new[]
        {
            "LOG.TXT",
            "UPLOAD.BIN"
        }

        )
        {
            HostCheck.Equal(Fat32Status.MissingFile, Fat32Parser.FindRootDirectoryEntry(media, f.Storage.Input.PartitionFirst, name).Status);
            uint seed = f.Open("/data/" + name, 2 | W.Create | W.Exclusive);
            HostCheck.Equal(0L, f.Core(3, seed));
            var empty = Fat32Parser.FindRootDirectoryEntry(media, f.Storage.Input.PartitionFirst, name);
            HostCheck.True(empty.Succeeded && empty.Entry.FileSize == 0 && empty.Entry.FirstCluster == 0);
            f.Independent(name, []);
        }

        // Owner disables FSInfo in volatile fixture setup. Seed FLUSH commits
        // that setup, so the40-call BPB guard starts after authenticating seeds.
        byte[] boot = f.Storage.Persistent.Slice((int)(f.Storage.Input.PartitionFirst * 512), 512).ToArray();
        HostCheck.True(boot.SequenceEqual(preparedBoot));
        HostCheck.True(f.Files.State->ActiveDescriptors == 3 && !f.Disk->RequestActive && !f.Provider->RequestActive);
        int storageCalls = 0, writes = 0, barriers = 0, metadataOnly = 0;
        List<string> syncRoles = [];
        foreach (var spec in specs)
        {
            ulong[] wire = DecodeLogUploadFrame(elf, spec);
            if (wire[0] == 1)
            {
                HostCheck.True(spec.Role == "witness" && wire[1] == 1 && wire[3] == (ulong)OS.Tooling.LinuxWritableDiskProofFixture.LogUploadWitness.Length);
                HostCheck.True(ElfRange(elf, wire[2], wire[3]).SequenceEqual(Encoding.ASCII.GetBytes(OS.Tooling.LinuxWritableDiskProofFixture.LogUploadWitness)));
                metadataOnly++;
                continue;
            }

            if (wire[0] == 231)
            {
                HostCheck.True(spec.Role == "exit" && wire[1] == 42);
                metadataOnly++;
                continue;
            }

            var call = f.Call(wire[0], wire[1], wire[2], wire[3], wire[4], wire[5], wire[6]);
            if (wire[0] == 2)
            {
                ReadOnlySpan<byte> path = ElfRange(elf, wire[1], 128);
                int zero = path.IndexOf((byte)0);
                HostCheck.True(zero > 0);
                path[..(zero + 1)].CopyTo(UserRange(f, Path, (uint)zero + 1));
                call.Argument0 = Path;
            }
            else if (wire[0] == 328)
            {
                HostCheck.True(wire[2] == OS.Tooling.LinuxWritableDiskProofFixture.LogUploadIovecs + (uint)writes * 16 && wire[3] == 1 && wire[5] == 0 && wire[6] == W.RwfNoAppend);
                ReadOnlySpan<byte> vector = ElfRange(elf, wire[2], 16);
                ulong source = BinaryPrimitives.ReadUInt64LittleEndian(vector), count = BinaryPrimitives.ReadUInt64LittleEndian(vector[8..]);
                HostCheck.True(count == (ulong)spec.Result && count is> 0 and <= 4094);
                ReadOnlySpan<byte> payload = ElfRange(elf, source, count);
                ReadOnlySpan<byte> expected = writes < 12 ? log.AsSpan(writes * 55, 55) : writes == 12 ? upload.AsSpan(0, 4094) : upload.AsSpan(4094);
                HostCheck.True(payload.SequenceEqual(expected) && wire[4] == (writes < 12 ? (ulong)writes * 55 : writes == 12 ? 0UL : 4094UL));
                payload.CopyTo(UserRange(f, Payload, (uint)count));
                Span<byte> copiedVector = UserRange(f, Vector, 16);
                BinaryPrimitives.WriteUInt64LittleEndian(copiedVector, Payload);
                BinaryPrimitives.WriteUInt64LittleEndian(copiedVector[8..], count);
                call.Argument1 = Vector;
                writes++;
            }
            else if (wire[0] == 17)
            {
                HostCheck.True(wire[2] >= OS.Tooling.LinuxWritableDiskProofFixture.LogUploadOutput);
                ulong delta = wire[2] - OS.Tooling.LinuxWritableDiskProofFixture.LogUploadOutput;
                HostCheck.True(delta <= 8192 && wire[3] <= 8192 - delta && spec.OutputBytes == wire[3]);
                if (delta == 0)
                    UserRange(f, Output, 8192).Fill(OS.Tooling.LinuxWritableDiskProofFixture.Sentinel);
                call.Argument1 = Output + delta;
                UserRange(f, call.Argument1, (uint)wire[3]);
            }

            ulong submitted = f.Storage.Device.State->Submitted, completed = f.Storage.Device.State->Completed, writeBytes = f.Storage.Device.State->SubmittedBytes;
            int firstTrace = f.Trace.Count;
            var begun = f.Begin(call);
            long value;
            if (begun.Progress == F.WritableDiskProgress.NotHandled)
            {
                var core = f.Thread.DescriptorCall(call.Number, call.Argument0, call.Argument1, call.Argument2, call.Argument3);
                HostCheck.Equal(LinuxSyscallAction.Return, core.Action);
                value = core.Value;
            }
            else if (begun.Progress == F.WritableDiskProgress.Immediate)
                value = begun.Value;
            else
            {
                HostCheck.Equal(F.WritableDiskProgress.Ready, f.Ready(begun.Token).Progress);
                var done = F.CompleteWritableDisk(f.Files.State, f.ThreadOwner, f.Worker, begun.Token, call, spec.OutputBytes);
                HostCheck.Equal(F.WritableDiskProgress.Immediate, done.Progress);
                value = done.Value;
            }

            HostCheck.Equal(spec.Result, value);
            storageCalls++;
            HostCheck.True(!f.Disk->RequestActive && !f.Provider->RequestActive && !f.Storage.State->RequestActive && !f.Storage.Device.State->RequestPending);
            if (wire[0] == 328)
                HostCheck.True(f.Trace.Skip(firstTrace).Any(row => row.Type == 1) && f.Storage.Device.State->SubmittedBytes > writeBytes && !f.Storage.State->Dirty);
            if (wire[0] == 74)
            {
                HostCheck.True(f.Trace.Skip(firstTrace).Select(row => row.Type).SequenceEqual(new uint[] { 4 }) && f.Storage.Device.State->Submitted == submitted + 1 && f.Storage.Device.State->Completed == completed + 1 && f.Storage.Device.State->SubmittedBytes == writeBytes && f.Storage.Device.State->CompletedBytes == writeBytes && f.Storage.Device.State->DurableBytes == writeBytes);
                syncRoles.Add(spec.Role);
                barriers++;
                Console.WriteLine($"LOG_UPLOAD_GLOBAL_FLUSH_HOST role={spec.Role} submitted={submitted + 1} completed={completed + 1} writeBytes={writeBytes} flushAcknowledgedBytes={f.Storage.Device.State->DurableBytes}");
            }

            if (spec.Role == "log.read")
                HostCheck.True(f.Bytes(log.Length).SequenceEqual(log) && f.Thread.At(Output)[log.Length] == 0xa5);
            if (spec.Role == "upload.read")
                HostCheck.True(f.Bytes(upload.Length).SequenceEqual(upload) && f.Thread.At(Output)[upload.Length] == 0xa5);
            if (spec.Role is "log.eof" or "upload.eof")
                HostCheck.True(UserRange(f, call.Argument1, 1)[0] == 0xa5);
            if (wire[0] == 3)
                HostCheck.True(!F.Capture(f.Files.State, (uint)wire[1], out _));
        }

        HostCheck.True(storageCalls == 38 && metadataOnly == 2 && writes == 14 && barriers == 4 && syncRoles.SequenceEqual(new[] { "log.sync.first", "log.sync.second", "upload.sync", "parent.sync" }));
        f.Independent("LOG.TXT", log);
        f.Independent("UPLOAD.BIN", upload);
        f.Independent("DATA.BIN", Enumerable.Range(0, 900).Select(Value).ToArray());
        f.Independent("KEEP.TXT", new byte[3]);
        HashSet<uint> allocated = [];
        foreach (var item in new[]
        {
            (Name: "LOG.TXT", Bytes: log.Length),
            (Name: "UPLOAD.BIN", Bytes: upload.Length)
        }

        )
        {
            var entry = Fat32Parser.FindRootDirectoryEntry(media, f.Storage.Input.PartitionFirst, item.Name);
            HostCheck.True(entry.Succeeded && entry.Entry.FileSize == item.Bytes);
            uint cluster = entry.Entry.FirstCluster;
            int count = (item.Bytes + 511) / 512;
            for (int i = 0; i < count; i++)
            {
                HostCheck.True(cluster >= 2 && cluster < bpb.ClusterCount + 2 && allocated.Add(cluster));
                if (i == count - 1)
                    HostCheck.True(f.Storage.Persistent.Slice((int)((bpb.FirstDataLba + cluster - 2) * 512 + (uint)item.Bytes % 512), 512 - item.Bytes % 512).IndexOfAnyExcept((byte)0) < 0);
                cluster = BinaryPrimitives.ReadUInt32LittleEndian(f.Storage.Persistent.Slice((int)(bpb.FirstFatLba * 512 + cluster * 4), 4)) & 0x0fffffff;
            }

            HostCheck.True(cluster >= 0x0ffffff8);
        }

        ReadOnlySpan<byte> finalFat = f.Storage.Persistent.Slice((int)(bpb.FirstFatLba * 512), oldFat.Length);
        HostCheck.True(finalFat.SequenceEqual(f.Storage.Persistent.Slice((int)((bpb.FirstFatLba + bpb.FatSizeSectors) * 512), oldFat.Length)));
        for (int offset = 0; offset < oldFat.Length; offset += 4)
            if (!allocated.Contains((uint)offset / 4))
                HostCheck.True(finalFat.Slice(offset, 4).SequenceEqual(oldFat.AsSpan(offset, 4)));
        for (int i = 0; i < guardClusters.Length; i++)
            HostCheck.True(f.Storage.Persistent.Slice((int)((bpb.FirstDataLba + guardClusters[i] - 2) * 512), 512).SequenceEqual(guardSectors[i]));
        HostCheck.True(f.Storage.Persistent.Slice((int)(bpb.FirstDataLba * 512), 64).SequenceEqual(oldEntries));
        HostCheck.True(f.Storage.Persistent.Slice((int)(f.Storage.Input.PartitionFirst * 512), 512).SequenceEqual(boot));
        HostCheck.True(f.Files.State->ActiveDescriptors == 3 && F.Audit(f.Files.State) && !f.HasLocks() && !f.Disk->RequestActive && !f.Provider->RequestActive && !f.Storage.State->Dirty && f.Storage.Device.State->Submitted == f.Storage.Device.State->Completed && f.Storage.Device.State->SubmittedBytes == f.Storage.Device.State->CompletedBytes && f.Storage.Device.State->CompletedBytes == f.Storage.Device.State->DurableBytes);
        f.Storage.Device.CheckGuards();
        Console.WriteLine($"LOG_UPLOAD_GLOBAL_HOST frames=40 storageCalls={storageCalls} positionedWrites={writes} explicitFlushes={barriers} logBytes={log.Length} uploadBytes={upload.Length} submitted={f.Storage.Device.State->Submitted} completed={f.Storage.Device.State->Completed} writeBytes={f.Storage.Device.State->SubmittedBytes} flushAcknowledgedBytes={f.Storage.Device.State->DurableBytes}");
    }

    private static ReadOnlySpan<byte> ElfRange(byte[] elf, ulong address, ulong bytes)
    {
        const ulong image = OS.Tooling.LinuxWritableDiskProofFixture.ImageBase;
        HostCheck.True(address >= image && bytes <= (ulong)elf.Length && address - image <= (ulong)elf.Length - bytes);
        return elf.AsSpan((int)(address - image), (int)bytes);
    }

    private static Span<byte> UserRange(Owner f, ulong address, uint bytes)
    {
        HostCheck.True(bytes > 0 && address >= User + 4096 && bytes <= LinuxThreadServicesTests.UserBytes && address - User <= LinuxThreadServicesTests.UserBytes - bytes);
        return new Span<byte>(f.Thread.At(address), (int)bytes);
    }

    private static ulong[] DecodeLogUploadFrame(byte[] elf, OS.Tooling.LinuxWritableDiskProofFixture.CallSpec spec)
    {
        HostCheck.True(spec.ReturnRip >= OS.Tooling.LinuxWritableDiskProofFixture.ImageBase + 40);
        ReadOnlySpan<byte> code = ElfRange(elf, spec.ReturnRip - 40, 40);
        ulong[] actual = new ulong[7];
        int cursor = 0;
        byte[] registers = [0xb8, 0xbf, 0xbe, 0xba, 0xba, 0xb8, 0xb9];
        for (int i = 0; i < actual.Length; i++)
        {
            if (i >= 4)
                HostCheck.Equal((byte)0x41, code[cursor++]);
            HostCheck.Equal(registers[i], code[cursor++]);
            actual[i] = BinaryPrimitives.ReadUInt32LittleEndian(code[cursor..]);
            cursor += 4;
        }

        HostCheck.True(cursor == 38 && code[cursor] == 0x0f && code[cursor + 1] == 0x05 && actual.SequenceEqual(new[] { spec.Number, spec.A0, spec.A1, spec.A2, spec.A3, spec.A4, spec.A5 }));
        if (spec.Number != 231)
        {
            ReadOnlySpan<byte> check = ElfRange(elf, spec.ReturnRip, 12);
            HostCheck.True(check[0] == 0x48 && check[1] == 0x3d && BinaryPrimitives.ReadInt32LittleEndian(check[2..]) == spec.Result && check[6] == 0x0f && check[7] == 0x85 && (long)(spec.ReturnRip + 12) + BinaryPrimitives.ReadInt32LittleEndian(check[8..]) == (long)OS.Tooling.LinuxWritableDiskProofFixture.ImageBase + OS.Tooling.LinuxWritableDiskProofFixture.FailureOffset);
        }

        return actual;
    }

    [HostTest("writable x64 single iovec grows a hole from independently verified free FAT entries and preserves copied input and independent cursors")]
    private static void PositionedGrowth()
    {
        using var f = new Owner();
        uint fd = f.Open("/data/m16-write.bin", 2 | W.Create | W.Exclusive | W.CloseOnExec);
        byte[] payload = Enumerable.Range(0, 1032).Select(Value).ToArray(), expected = new byte[5126];
        payload.CopyTo(expected, 4094);
        var call = f.WriteCall(fd, payload, 4094);
        var begun = f.Begin(call);
        new Span<byte>(f.Thread.At(Payload), payload.Length).Fill(0xee);
        *(ulong*)f.Thread.At(Vector) = 0;
        HostCheck.Equal(F.WritableDiskProgress.Ready, f.Ready(begun.Token).Progress);
        HostCheck.Equal(1032L, F.CompleteWritableDisk(f.Files.State, f.ThreadOwner, f.Worker, begun.Token, call).Value);
        HostCheck.Equal(0L, f.Run(f.Call(8, fd, 0, 1)));
        HostCheck.Equal(5126L, f.Run(f.Call(8, fd, 0, 2)));
        HostCheck.Equal(16L, f.Run(f.Call(17, fd, Output, 16, 4086)));
        HostCheck.True(f.Bytes(16).SequenceEqual(new byte[8].Concat(payload.Take(8))));
        uint other = f.Open("/data/m16-write.bin");
        HostCheck.Equal(16L, f.Run(f.Call(0, other, Output, 16)));
        HostCheck.True(f.Bytes(16).All(value => value == 0));
        HostCheck.Equal(5126L, f.Run(f.Call(8, fd, 0, 1)));
        HostCheck.Equal(16L, f.Run(f.Call(8, other, 0, 1)));
        f.Independent("m16-write.bin", expected);
        HostCheck.True(f.Trace.Any(row => row.Type == 0) && f.Trace.Any(row => row.Type == 1) && f.Trace.Any(row => row.Type == 4) && f.Storage.Device.State->SubmittedBytes == f.Storage.Device.State->DurableBytes && !f.Storage.State->Dirty);
    }

    [HostTest("writable positioned wire rejects unsupported vectors flags compat high offset overflow and failed copies before media effects")]
    private static void PositionedInput()
    {
        using var f = new Owner();
        uint fd = f.Open(flags: 2);
        var good = f.WriteCall(fd, new byte[] { 9, 8, 7 }, 5);
        uint calls = f.Hardware->Calls;
        var bad = good;
        bad.Argument2 = 2;
        HostCheck.Equal(-95L, f.Run(bad));
        bad = good;
        bad.Argument4 = 1;
        HostCheck.Equal(-95L, f.Run(bad));
        bad = good;
        bad.Argument5 = 0;
        HostCheck.Equal(-95L, f.Run(bad));
        bad = good;
        bad.Argument3 = ulong.MaxValue;
        HostCheck.Equal(-22L, f.Run(bad));
        bad = good;
        bad.Argument3 = 0x100000001;
        HostCheck.Equal(-75L, f.Run(bad));
        *(ulong*)(f.Thread.At(Vector) + 8) = W.IoBytes + 1;
        HostCheck.Equal(-22L, f.Run(good));
        *(ulong*)(f.Thread.At(Vector) + 8) = 3;
        f.Thread.FailRead(Vector);
        HostCheck.Equal(-14L, f.Run(good));
        f.Thread.FailRead(Payload);
        HostCheck.Equal(-14L, f.Run(good));
        f.Thread.FailRead(0);
        HostCheck.True(!f.Disk->RequestActive && !f.Provider->RequestActive && f.Hardware->Calls == calls && f.Storage.State->VolumeRevision == 1);
        HostCheck.Equal(3L, f.Run(good));
        HostCheck.Equal(0L, f.Run(f.Call(8, fd, 0, 1)));
    }

    [HostTest("writable file and root fsync issue actual flush while closed unlink authenticates and removes the file before directory sync")]
    private static void SyncAndClosedUnlink()
    {
        using var f = new Owner();
        uint fd = f.Open("/data/m16-write.bin", 2 | W.Create | W.Exclusive);
        HostCheck.Equal(3L, f.Run(f.WriteCall(fd, new byte[] { 1, 2, 3 }, 1)));
        foreach (ulong sync in new ulong[]
        {
            74,
            75
        }

        )
        {
            int trace = f.Trace.Count;
            HostCheck.Equal(0L, f.Run(f.Call(sync, fd)));
            HostCheck.True(f.Trace.Skip(trace).Any(row => row.Type == 4));
        }

        uint root = f.Open("/data", W.DirectoryOnly | W.CloseOnExec | W.LargeFile);
        int before = f.Trace.Count;
        HostCheck.Equal(0L, f.Run(f.Call(74, root)));
        HostCheck.True(f.Trace.Skip(before).Any(row => row.Type == 4));
        before = f.Trace.Count;
        HostCheck.Equal(-22L, f.Run(f.Call(75, root)));
        HostCheck.Equal(before, f.Trace.Count);
        f.PutPath("/data/m16-write.bin");
        HostCheck.Equal(-16L, f.Run(f.Call(87, Path)));
        HostCheck.Equal(0L, f.Core(3, fd));
        HostCheck.Equal(0L, f.Run(f.Call(87, Path)));
        f.PutPath("/data/m16-write.bin");
        HostCheck.Equal(-2L, f.Run(f.Call(2, Path, 0)));
        before = f.Trace.Count;
        HostCheck.Equal(0L, f.Run(f.Call(74, root)));
        HostCheck.True(f.Trace.Skip(before).Any(row => row.Type == 4));
        HostCheck.True(f.Storage.Device.State->SubmittedBytes == f.Storage.Device.State->DurableBytes);
    }

    [HostTest("writable global copied SQLite journal ordering preserves live database handles locks and independent media across real flushes")]
    private static void SqliteJournalOrdering()
    {
        // Widths/offsets/lock ranges and journal->directory->header->DB order
        // come from original77B2 sqlite-trace.5255:343-364. Page bytes below
        // are independent host vectors, not a SQLite database/checksummed
        // recovery journal. No SQLite engine, user execution or physical
        // persistence is proved by this fake-device/global-wire control.
        using var f = new Owner();
        var media = new PersistentDevice(f.Hardware);
        var geometry = Fat32Parser.ReadBpb(media, f.Storage.Input.PartitionFirst);
        HostCheck.True(geometry.Succeeded);
        ulong data = geometry.Bpb.FirstDataLba;
        byte[] keepSector = f.Storage.Persistent.Slice((int)((data + 1) * 512), 512).ToArray();
        byte[] guard = Enumerable.Range(0, 900).Select(Value).ToArray();
        byte[] initial = Enumerable.Range(0, 8192).Select(i => unchecked((byte)(i * 29 + 7))).ToArray();
        byte[] updated = initial.Select(value => (byte)(value ^ 0x5a)).ToArray();
        uint Fat(uint cluster, uint copy = 0) => BinaryPrimitives.ReadUInt32LittleEndian(f.Storage.Persistent.Slice((int)((geometry.Bpb.FirstFatLba + copy * geometry.Bpb.FatSizeSectors) * 512 + cluster * 4), 4));
        uint[] preservedClusters = [0, 1, 2, 3, 4, 6];
        uint[] preservedFat = preservedClusters.Select(cluster => Fat(cluster)).ToArray();
        List<string> barriers = [];
        void Write(uint fd, byte[] bytes, ulong offset)
        {
            int first = f.Trace.Count;
            ulong submitted = f.Storage.Device.State->SubmittedBytes;
            HostCheck.Equal((long)bytes.Length, f.Run(f.WriteCall(fd, bytes, offset)));
            HostCheck.True(f.Trace.Skip(first).Any(row => row.Type == 1) && f.Trace.Skip(first).Any(row => row.Type == 4) && f.Storage.Device.State->SubmittedBytes > submitted && f.Storage.Device.State->SubmittedBytes == f.Storage.Device.State->CompletedBytes && f.Storage.Device.State->CompletedBytes == f.Storage.Device.State->DurableBytes && !f.Storage.State->Dirty);
        }

        void Sync(uint fd, string role)
        {
            B.State* block = f.Storage.Device.State;
            ulong submitted = block->Submitted, completed = block->Completed, bytes = block->SubmittedBytes;
            int first = f.Trace.Count;
            HostCheck.Equal(0L, f.Run(f.Call(74, fd)));
            HostCheck.True(f.Trace.Skip(first).Select(row => row.Type).SequenceEqual(new uint[] { 4 }) && block->Submitted == submitted + 1 && block->Completed == completed + 1 && block->SubmittedBytes == bytes && block->CompletedBytes == bytes && block->DurableBytes == bytes && !f.Storage.State->Dirty);
            barriers.Add(role); // Actual observed FLUSH frontiers, not application/power-loss durability.
            Console.WriteLine($"SQLITE_GLOBAL_FLUSH_HOST role={role} submitted={block->Submitted} completed={block->Completed} writeBytes={bytes} flushAcknowledgedBytes={block->DurableBytes}");
        }

        void Lock(uint fd, short type, long first, long bytes)
        {
            f.PutLock(type, first, bytes);
            HostCheck.Equal(0L, f.Run(f.Call(72, fd, 6, Flock)));
        }

        byte[] Stat(uint fd, string path, ulong length)
        {
            HostCheck.Equal(0L, f.Run(f.Call(5, fd, Output)));
            byte[] facts = f.Bytes(144);
            HostCheck.True(BitConverter.ToUInt32(facts, 24) == 0x8180 && BitConverter.ToUInt64(facts, 48) == length && BitConverter.ToUInt64(facts, 64) == (length + 511) / 512);
            foreach (ulong number in new ulong[]
            {
                4,
                6
            }

            )
            {
                f.PutPath(path);
                HostCheck.Equal(0L, f.Run(f.Call(number, Path, Output)));
                HostCheck.True(f.Bytes(144).SequenceEqual(facts));
            }

            return facts;
        }

        const uint flags = 2 | W.Create | W.NoFollow | W.CloseOnExec | W.LargeFile;
        f.PutPath("/data/contract.db");
        long opened = f.Run(f.Call(2, Path, flags, 0x1a4));
        HostCheck.True(opened >= 3);
        uint database = (uint)opened;
        HostCheck.Equal(0L, f.Core(72, database, 2, 1));
        HostCheck.True(F.Capture(f.Files.State, database, out var originalDatabase));
        Write(database, initial[..4096], 0);
        Write(database, initial[4096..], 4096);
        Sync(database, "seed-db");
        ulong inode = BitConverter.ToUInt64(Stat(database, "/data/contract.db", 8192), 8);
        uint independent = f.Open("/data/contract.db", W.LargeFile | W.CloseOnExec);
        HostCheck.Equal(17L, f.Run(f.Call(8, database, 17)));
        HostCheck.Equal(29L, f.Run(f.Call(8, independent, 29)));
        const long pending = 0x40000000, reserved = pending + 1, shared = pending + 2;
        Lock(database, 0, pending, 1);
        Lock(database, 0, shared, 510);
        Lock(database, 2, pending, 1);
        Lock(database, 1, reserved, 1);
        HostCheck.True(f.HasLocks());
        f.PutPath("/data/contract.db-journal");
        HostCheck.Equal(-2L, f.Run(f.Call(4, Path, Output)));
        uint journal = f.Open("/data/contract.db-journal", flags);
        HostCheck.Equal(0L, f.Core(72, journal, 2, 1));
        HostCheck.True(F.Capture(f.Files.State, journal, out var originalJournal));
        HostCheck.Equal(31L, f.Run(f.Call(8, journal, 31)));
        byte[] journalBytes = new byte[8720], header = new byte[512], checksum = [0x4b, 9, 0xca, 0x75];
        checksum.CopyTo(header, 12);
        header[19] = 2;
        header[22] = 2;
        header[26] = 0x10;
        (ulong Offset, byte[] Bytes)[] records = [(0, header), (512, [0, 0, 0, 1]), (516, initial[..4096]), (4612, checksum), (4616, [0, 0, 0, 2]), (4620, initial[4096..]), (8716, checksum)];
        foreach (var record in records)
        {
            Write(journal, record.Bytes, record.Offset);
            record.Bytes.CopyTo(journalBytes, (int)record.Offset);
        }

        byte[] journalFacts = Stat(journal, "/data/contract.db-journal", 8720);
        var located = Fat32Parser.FindRootDirectoryEntry(media, f.Storage.Input.PartitionFirst, "contract.db-journal");
        HostCheck.True(located.Succeeded && located.Entry.FileSize == 8720 && located.Entry.Attributes == 0x20);
        List<uint> journalClusters = [];
        uint next = located.Entry.FirstCluster;
        for (int i = 0; i < 18; i++)
        {
            HostCheck.True(next >= 2 && next < geometry.Bpb.ClusterCount + 2 && !journalClusters.Contains(next));
            journalClusters.Add(next);
            next = Fat(next) & 0x0fffffff;
        }

        HostCheck.True(next >= 0x0ffffff8 && BitConverter.ToUInt64(journalFacts, 64) == (ulong)journalClusters.Count);
        ulong entry = (BitConverter.ToUInt64(journalFacts, 8) - 1) * 32;
        var run = f.Storage.Persistent.Slice((int)entry - 64, 96);
        HostCheck.True(run[0] == 0x42 && run[11] == 15 && run[32] == 1 && run[43] == 15 && run[75] == 0x20);
        f.Independent("contract.db-journal", journalBytes);
        f.Independent("contract.db", initial);
        HostCheck.True(F.IsCurrent(f.Files.State, originalDatabase) && f.HasLocks());
        Lock(database, 1, pending, 1);
        Lock(database, 1, shared, 510);
        HostCheck.Equal(0L, f.Run(f.Call(17, journal, Output, 8, 9216))); // Genuine journal EOF before its explicit barrier.
        Sync(journal, "journal-body");
        uint parent = f.Open("/data", W.LargeFile | W.CloseOnExec);
        HostCheck.Equal(0L, f.Core(72, parent, 2, 1));
        Sync(parent, "parent");
        HostCheck.Equal(0L, f.Core(3, parent));
        HostCheck.True(f.HasLocks());
        byte[] committedHeader = [0xd9, 0xd5, 5, 0xf9, 0x20, 0xa1, 0x63, 0xd7, 0, 0, 0, 2];
        Write(journal, committedHeader, 0);
        committedHeader.CopyTo(journalBytes, 0);
        Sync(journal, "journal-header");
        f.Independent("contract.db-journal", journalBytes);
        f.Independent("contract.db", initial);
        Write(database, updated[..4096], 0);
        Write(database, updated[4096..], 4096);
        Sync(database, "database");
        HostCheck.Equal(inode, BitConverter.ToUInt64(Stat(independent, "/data/contract.db", 8192), 8));
        HostCheck.True(F.IsCurrent(f.Files.State, originalDatabase) && f.HasLocks());
        HostCheck.Equal(31L, f.Run(f.Call(8, journal, 0, 1)));
        HostCheck.Equal(0L, f.Core(3, journal));
        HostCheck.True(!F.IsCurrent(f.Files.State, originalJournal) && f.HasLocks());
        f.PutPath("/data/contract.db-journal");
        HostCheck.Equal(0L, f.Run(f.Call(87, Path)));
        HostCheck.True(f.HasLocks());
        HostCheck.Equal(-2L, f.Run(f.Call(4, Path, Output)));
        HostCheck.Equal(Fat32Status.MissingFile, Fat32Parser.FindRootDirectoryEntry(media, f.Storage.Input.PartitionFirst, "contract.db-journal").Status);
        HostCheck.True(journalClusters.All(cluster => (Fat(cluster) & 0x0fffffff) == 0 && (Fat(cluster, 1) & 0x0fffffff) == 0));
        for (int i = 0; i < 3; i++)
            HostCheck.True(f.Storage.Persistent[(int)entry - 64 + i * 32] == 0xe5 && f.Storage.Persistent.Slice((int)entry - 63 + i * 32, 31).IndexOfAnyExcept((byte)0) < 0);
        Lock(database, 0, shared, 510);
        Lock(database, 2, pending, 2);
        Lock(database, 2, 0, 0);
        HostCheck.True(!f.HasLocks());
        HostCheck.Equal(8192L, f.Run(f.Call(17, independent, Output, 8192, 0)));
        HostCheck.True(f.Bytes(8192).SequenceEqual(updated));
        HostCheck.Equal(17L, f.Run(f.Call(8, database, 0, 1)));
        HostCheck.Equal(29L, f.Run(f.Call(8, independent, 0, 1)));
        f.Independent("contract.db", updated);
        f.Independent("DATA.BIN", guard);
        f.Independent("KEEP.TXT", new byte[3]);
        HostCheck.True(f.Storage.Persistent.Slice((int)((data + 1) * 512), 512).SequenceEqual(keepSector));
        ulong fat = geometry.Bpb.FirstFatLba;
        HostCheck.True(f.Storage.Persistent.Slice((int)(fat * 512), (int)geometry.Bpb.FatSizeSectors * 512).SequenceEqual(f.Storage.Persistent.Slice((int)((fat + geometry.Bpb.FatSizeSectors) * 512), (int)geometry.Bpb.FatSizeSectors * 512)));
        HostCheck.True(preservedClusters.Select(cluster => Fat(cluster)).SequenceEqual(preservedFat));
        HostCheck.True(barriers.SequenceEqual(new[] { "seed-db", "journal-body", "parent", "journal-header", "database" }) && !f.Disk->RequestActive && !f.Provider->RequestActive && !f.Storage.State->Dirty && F.Audit(f.Files.State) && f.Storage.Device.State->Submitted == f.Storage.Device.State->Completed && f.Storage.Device.State->SubmittedBytes == f.Storage.Device.State->CompletedBytes && f.Storage.Device.State->CompletedBytes == f.Storage.Device.State->DurableBytes);
        Console.WriteLine($"SQLITE_GLOBAL_JOURNAL_HOST journalBytes=8720 databaseBytes=8192 explicitFlushes={barriers.Count} submitted={f.Storage.Device.State->Submitted} completed={f.Storage.Device.State->Completed} writeBytes={f.Storage.Device.State->SubmittedBytes} flushAcknowledgedBytes={f.Storage.Device.State->DurableBytes}");
    }

    [HostTest("writable copied x64 flock query preserves padding and PID data while any close releases actual process locks")]
    private static void CopiedLocks()
    {
        using var f = new Owner();
        uint first = f.Open(flags: 2), second = f.Open();
        f.PutLock(1, 1073741825, 1, 777);
        HostCheck.Equal(0L, f.Run(f.Call(72, first, 6, Flock)));
        HostCheck.True(f.HasLocks());
        byte[] original = new ReadOnlySpan<byte>(f.Thread.At(Flock), 32).ToArray();
        HostCheck.Equal(0L, f.Run(f.Call(72, second, 5, Flock)));
        original[0] = 2;
        HostCheck.True(new ReadOnlySpan<byte>(f.Thread.At(Flock), 32).SequenceEqual(original));
        f.PutLock(1, 0, 1);
        HostCheck.Equal(-9L, f.Run(f.Call(72, second, 6, Flock)));
        f.PutLock(2, 0, 1);
        HostCheck.Equal(-22L, f.Run(f.Call(72, first, 5, Flock)));
        f.PutLock(1, 0, 1);
        *(short*)(f.Thread.At(Flock) + 2) = 1;
        HostCheck.Equal(-22L, f.Run(f.Call(72, first, 6, Flock)));
        f.PutLock(1, 0, 1);
        f.Thread.FailRead(Flock);
        HostCheck.Equal(-14L, f.Run(f.Call(72, first, 6, Flock)));
        f.Thread.FailRead(0);
        f.Thread.FailWrite(Flock);
        HostCheck.Equal(-14L, f.Run(f.Call(72, first, 5, Flock)));
        f.Thread.FailWrite(0);
        HostCheck.True(f.HasLocks());
        HostCheck.Equal(0L, f.Core(3, second));
        HostCheck.True(!f.HasLocks());
        HostCheck.Equal(-95L, f.Core(72, first, 7, Flock));
        HostCheck.Equal(-95L, f.Core(72, first, 37, Flock));
    }

    [HostTest("writable already ready write sync and unlink results win interruption without replay or new hardware publication")]
    private static void ReadyMutations()
    {
        foreach (int operation in new[]
        {
            0,
            1,
            2
        }

        )
        {
            using var f = new Owner();
            uint fd = f.Open(flags: 2);
            LinuxSyscallFrame call;
            if (operation == 0)
                call = f.WriteCall(fd, new byte[] { 71, 72 }, 5);
            else if (operation == 1)
                call = f.Call(74, fd);
            else
            {
                HostCheck.Equal(0L, f.Core(3, fd));
                f.PutPath("/data/DATA.BIN");
                call = f.Call(87, Path);
            }

            var begun = f.Begin(call);
            HostCheck.Equal(F.WritableDiskProgress.Ready, f.Ready(begun.Token).Progress);
            uint calls = f.Hardware->Calls;
            var cancelled = F.CancelWritableDisk(f.Files.State, f.ThreadOwner, begun.Token, false);
            HostCheck.True(cancelled.Progress == F.WritableDiskProgress.Ready && cancelled.Worker.Value == f.Worker.Value);
            HostCheck.Equal(operation == 0 ? 2L : 0L, F.CompleteWritableDisk(f.Files.State, f.ThreadOwner, f.Worker, begun.Token, call).Value);
            HostCheck.Equal(calls, f.Hardware->Calls);
        }
    }

    [HostTest("writable dirty cancellation retains adapter and whole storage tree without live worker copy End or successful detach authority")]
    private static void DirtyRetention()
    {
        using var f = new Owner();
        uint fd = f.Open(flags: 2);
        var call = f.WriteCall(fd, new byte[] { 71, 72 }, 5);
        var begun = f.Begin(call);
        for (int i = 0; i < 100000 && !f.Storage.State->Dirty; i++)
        {
            f.Pump();
            f.CompleteHardware();
        }

        HostCheck.True(f.Storage.State->Dirty);
        f.AllowRetainedFailure = true;
        var cancelled = F.CancelWritableDisk(f.Files.State, f.ThreadOwner, begun.Token, true);
        HostCheck.True(cancelled.Progress == F.WritableDiskProgress.RecoveryRequired && !cancelled.Worker.IsValid && f.Disk->RequestActive);
        byte value = 19;
        HostCheck.True(Tree(f.Files.State, (ulong)&value, 1));
        HostCheck.True(!W.CanBorrowStorage(f.Provider, f.ProviderAdmission.Epoch, (ulong)&value, 1));
        HostCheck.True(!F.DetachWritableDisk(f.Files.State) && !F.End(f.Files.State));
        HostCheck.Equal(F.WritableDiskProgress.Rejected, F.CompleteWritableDisk(f.Files.State, f.ThreadOwner, f.Worker, begun.Token, call).Progress);
    }

    [HostTest("writable pending positioned write close cancels authentication before OUT and fd reuse cannot receive its completion or cursor")]
    private static void ClosedWrite()
    {
        using var f = new Owner();
        uint fd = f.Open(flags: 2);
        var call = f.WriteCall(fd, new byte[] { 71, 72 }, 5);
        var begun = f.Begin(call);
        f.Pump();
        HostCheck.True(f.Hardware->Pending && !f.Storage.State->Dirty);
        HostCheck.Equal(0L, f.Core(3, fd));
        f.PutPath("/app/fixture.data");
        HostCheck.Equal((long)fd, f.Core(2, Path, 0x88100));
        var result = f.Ready(begun.Token);
        HostCheck.True(result.Progress == F.WritableDiskProgress.Ready && result.Worker.Value == f.Worker.Value);
        HostCheck.Equal(-9L, F.CompleteWritableDisk(f.Files.State, f.ThreadOwner, f.Worker, begun.Token, call).Value);
        HostCheck.True(f.Storage.Device.State->SubmittedBytes == 0 && f.Storage.State->VolumeRevision == 1);
        f.Independent("DATA.BIN", Enumerable.Range(0, 900).Select(Value).ToArray());
        HostCheck.Equal(0L, f.Core(8, fd, 0, 1));
        HostCheck.Equal(8L, f.Core(0, fd, Output, 8));
    }

    [HostTest("writable failed actual flush keeps recovery request backing and revokes worker on exit without treating reset as successful sync")]
    private static void FailedFlush()
    {
        using var f = new Owner();
        uint fd = f.Open(flags: 2);
        var call = f.WriteCall(fd, new byte[] { 71, 72 }, 5);
        var begun = f.Begin(call);
        bool failed = false;
        for (int i = 0; i < 100000 && !failed; i++)
        {
            f.Pump();
            if (!f.Hardware->Pending)
                continue;
            if (f.Hardware->Type == 4)
            {
                f.Storage.Device.Complete(status: 1);
                failed = true;
            }
            else
                f.CompleteHardware();
        }

        HostCheck.True(failed);
        var result = f.Ready(begun.Token);
        f.AllowRetainedFailure = true;
        HostCheck.True(result.Progress == F.WritableDiskProgress.RecoveryRequired && result.Value == -5 && f.Storage.State->Dirty);
        result = F.CompleteWritableDisk(f.Files.State, f.ThreadOwner, f.Worker, begun.Token, call);
        HostCheck.True(result.Progress == F.WritableDiskProgress.RecoveryRequired && !result.Worker.IsValid);
        HostCheck.True(!F.DetachWritableDisk(f.Files.State) && f.Provider->RequestActive && f.Disk->RequestActive);
        HostCheck.True(f.Storage.Device.State->SubmittedBytes > f.Storage.Device.State->DurableBytes);
        byte outside = 7;
        HostCheck.True(Tree(f.Files.State, (ulong)&outside, 1));
    }

    [HostTest("writable global binding shares package fd namespace and commits only its independent common read cursor")]
    private static void NamespaceAndCursors()
    {
        using var f = new Owner();
        f.PutPath("/app/fixture.data");
        HostCheck.Equal(3L, f.Core(2, Path, 0x88100));
        uint first = f.Open(), second = f.Open();
        HostCheck.True(first == 4 && second == 5);
        HostCheck.Equal(16L, f.Run(f.Call(0, first, Output, 16)));
        HostCheck.True(f.Bytes(16).SequenceEqual(Enumerable.Range(0, 16).Select(Value)));
        HostCheck.Equal(16L, f.Run(f.Call(8, first, 0, 1)));
        HostCheck.Equal(8L, f.Run(f.Call(17, first, Output, 8, 500)));
        HostCheck.True(f.Bytes(8).SequenceEqual(Enumerable.Range(500, 8).Select(Value)));
        HostCheck.Equal(16L, f.Run(f.Call(8, first, 0, 1)));
        HostCheck.Equal(8L, f.Run(f.Call(0, second, Output, 8)));
        HostCheck.True(f.Bytes(8).SequenceEqual(Enumerable.Range(0, 8).Select(Value)));
        HostCheck.Equal(8L, f.Run(f.Call(8, second, 0, 1)));
        HostCheck.Equal(-95L, f.Core(72, first, 0, 0));
        HostCheck.Equal(-95L, f.Core(72, first, 1030, 0));
        HostCheck.Equal(8L, f.Run(f.Call(0, first, Output, 8)));
        HostCheck.True(f.Bytes(8).SequenceEqual(Enumerable.Range(16, 8).Select(Value)));
        HostCheck.Equal(24L, f.Run(f.Call(8, first, 0, 1)));
        HostCheck.Equal(8L, f.Run(f.Call(8, second, 0, 1)));
        var privateHandle = W.Capture(f.Provider, 3).Descriptor;
        HostCheck.Equal(0L, W.Seek(f.Provider, privateHandle, 0, 1).Value);
    }

    [HostTest("writable create reserves global fd and description before media effects while ordinary package and pipe allocations skip them")]
    private static void Reservations()
    {
        using (var f = new Owner())
        {
            f.PutPath("/data/new-global-file.bin");
            var call = f.Call(2, Path, 2 | W.Create | W.Exclusive | W.CloseOnExec, 0x180);
            var started = f.Begin(call);
            HostCheck.Equal(F.WritableDiskProgress.Pending, started.Progress);
            HostCheck.True(f.Disk->RequestActive && !F.Capture(f.Files.State, 3, out _) && f.Files.State->ActiveDescriptors == 3);
            f.PutPath("/app/fixture.data");
            HostCheck.Equal(4L, f.Core(2, Path, 0x88100));
            HostCheck.Equal(0L, f.Core(293, Output, F.NonBlock));
            HostCheck.True(f.Thread.Read32(Output) == 5 && f.Thread.Read32(Output + 4) == 6);
            HostCheck.Equal(F.WritableDiskProgress.Ready, f.Ready(started.Token).Progress);
            HostCheck.Equal(3L, F.CompleteWritableDisk(f.Files.State, f.ThreadOwner, f.Worker, started.Token, call).Value);
            HostCheck.True(F.Capture(f.Files.State, 3, out _) && !f.Disk->RequestActive && f.Storage.State->VolumeRevision > 1);
            HostCheck.Equal(0L, f.Run(f.Call(0, 3, Output, 1)));
        }

        foreach (bool fdFull in new[]
        {
            true,
            false
        }

        )
        {
            using var f = new Owner(descriptors: fdFull ? 3U : 32U, descriptions: 3);
            f.PutPath("/data/must-not-create.bin");
            uint calls = f.Hardware->Calls;
            HostCheck.Equal(fdFull ? -24L : -23L, f.Run(f.Call(2, Path, 2 | W.Create | W.Exclusive, 0x180)));
            HostCheck.True(!f.Provider->RequestActive && !f.Disk->RequestActive && f.Hardware->Calls == calls && f.Storage.State->VolumeRevision == 1);
        }
    }

    [HostTest("writable failed final user copy or captured output ceiling cannot advance the common cursor and a fresh read retries exact bytes")]
    private static void FailedCopy()
    {
        using var f = new Owner();
        uint fd = f.Open();
        new Span<byte>(f.Thread.At(Output), 64).Fill(0xa5);
        f.Thread.FailWrite(Output);
        HostCheck.Equal(-14L, f.Run(f.Call(0, fd, Output, 37)));
        HostCheck.True(f.Bytes(37).All(value => value == 0xa5));
        HostCheck.Equal(0L, f.Run(f.Call(8, fd, 0, 1)));
        f.Thread.FailWrite(0);
        var call = f.Call(0, fd, Output, 37);
        var begun = f.Begin(call);
        f.Ready(begun.Token);
        uint writes = f.Thread.Writes;
        HostCheck.Equal(-14L, F.CompleteWritableDisk(f.Files.State, f.ThreadOwner, f.Worker, begun.Token, call, 8).Value);
        HostCheck.Equal(writes, f.Thread.Writes);
        HostCheck.Equal(0L, f.Run(f.Call(8, fd, 0, 1)));
        HostCheck.Equal(37L, f.Run(f.Call(0, fd, Output, 37)));
        HostCheck.True(f.Bytes(37).SequenceEqual(Enumerable.Range(0, 37).Select(Value)) && f.Thread.At(Output)[37] == 0xa5);
        HostCheck.Equal(37L, f.Run(f.Call(8, fd, 0, 1)));
    }

    [HostTest("writable closed pending and held reads preserve exact live error waiter and reject late bytes after the global fd is reused")]
    private static void CloseAndReuse()
    {
        foreach (bool held in new[]
        {
            false,
            true
        }

        )
        {
            using var f = new Owner();
            uint fd = f.Open();
            var call = f.Call(0, fd, Output, 37);
            var begun = f.Begin(call);
            if (held)
                f.Ready(begun.Token);
            else
            {
                f.Pump();
                HostCheck.True(f.Hardware->Pending);
            }

            HostCheck.True(F.Capture(f.Files.State, fd, out var old));
            HostCheck.Equal(0L, f.Core(3, fd));
            f.PutPath("/app/fixture.data");
            HostCheck.Equal((long)fd, f.Core(2, Path, 0x88100));
            HostCheck.True(!F.IsCurrent(f.Files.State, old));
            new Span<byte>(f.Thread.At(Output), 37).Fill(0xa5);
            uint writes = f.Thread.Writes;
            var ready = f.Ready(begun.Token);
            HostCheck.True(ready.Worker.Value == f.Worker.Value && ready.Value == -9);
            HostCheck.Equal(-9L, F.CompleteWritableDisk(f.Files.State, f.ThreadOwner, f.Worker, begun.Token, call).Value);
            HostCheck.True(f.Bytes(37).All(value => value == 0xa5) && f.Thread.Writes == writes && F.Audit(f.Files.State));
            HostCheck.Equal(0L, f.Core(8, fd, 0, 1));
        }
    }

    [HostTest("writable explicit cancellation transfers a pending read to a no wake orphan while known ready open results win live interruption")]
    private static void CancelAndReady()
    {
        using var f = new Owner();
        uint fd = f.Open();
        var call = f.Call(0, fd, Output, 8);
        var begun = f.Begin(call);
        f.Pump();
        HostCheck.True(f.Hardware->Pending);
        uint writes = f.Thread.Writes;
        var cancelled = F.CancelWritableDisk(f.Files.State, f.ThreadOwner, begun.Token, true);
        HostCheck.True(cancelled.Progress == F.WritableDiskProgress.Draining && !cancelled.Worker.IsValid && f.Disk->RequestActive);
        HostCheck.Equal(F.WritableDiskProgress.Rejected, F.CompleteWritableDisk(f.Files.State, f.ThreadOwner, f.Worker, begun.Token, call).Progress);
        f.Drain();
        HostCheck.Equal(F.WritableDiskProgress.Rejected, F.PollWritableDisk(f.Files.State, f.ThreadOwner, begun.Token).Progress);
        HostCheck.True(f.Thread.Writes == writes);
        HostCheck.Equal(0L, f.Run(f.Call(8, fd, 0, 1)));
        f.PutPath("/data/DATA.BIN");
        call = f.Call(2, Path, 0);
        begun = f.Begin(call);
        f.Ready(begun.Token);
        uint calls = f.Hardware->Calls;
        var winner = F.CancelWritableDisk(f.Files.State, f.ThreadOwner, begun.Token, false);
        HostCheck.True(winner.Progress == F.WritableDiskProgress.Ready && winner.Worker.Value == f.Worker.Value);
        HostCheck.True(F.CompleteWritableDisk(f.Files.State, f.ThreadOwner, f.Worker, begun.Token, call).Value >= 3 && f.Hardware->Calls == calls);
        f.PutPath("/data/known-created.bin");
        call = f.Call(2, Path, 2 | W.Create | W.Exclusive, 0x180);
        begun = f.Begin(call);
        f.Ready(begun.Token);
        calls = f.Hardware->Calls;
        HostCheck.True(f.Storage.State->VolumeRevision > 1 && f.Storage.Device.State->SubmittedBytes > 0);
        winner = F.CancelWritableDisk(f.Files.State, f.ThreadOwner, begun.Token, false);
        HostCheck.True(winner.Progress == F.WritableDiskProgress.Ready && winner.Worker.Value == f.Worker.Value);
        HostCheck.True(F.CompleteWritableDisk(f.Files.State, f.ThreadOwner, f.Worker, begun.Token, call).Value >= 3 && f.Hardware->Calls == calls);
    }

    [HostTest("writable cancelled open reservation stays unavailable until drain and abandoned completed creation is neither published nor replayed")]
    private static void CancelOpenReservation()
    {
        using (var f = new Owner())
        {
            f.PutPath("/data/not-created.bin");
            var begun = f.Begin(f.Call(2, Path, 2 | W.Create | W.Exclusive, 0x180));
            var cancelled = F.CancelWritableDisk(f.Files.State, f.ThreadOwner, begun.Token, true);
            HostCheck.True(cancelled.Progress == F.WritableDiskProgress.Draining && !cancelled.Worker.IsValid && f.Disk->RequestActive);
            f.PutPath("/app/fixture.data");
            HostCheck.Equal(4L, f.Core(2, Path, 0x88100));
            f.Drain();
            HostCheck.Equal(3L, f.Core(2, Path, 0x88100));
            HostCheck.True(f.Storage.State->VolumeRevision == 1 && f.Storage.Device.State->SubmittedBytes == 0);
        }

        using (var f = new Owner())
        {
            f.PutPath("/data/abandoned-created.bin");
            var begun = f.Begin(f.Call(2, Path, 2 | W.Create | W.Exclusive, 0x180));
            f.Ready(begun.Token);
            ulong writes = f.Storage.Device.State->SubmittedBytes, revision = f.Storage.State->VolumeRevision;
            HostCheck.True(writes > 0 && revision > 1);
            var cancelled = F.CancelWritableDisk(f.Files.State, f.ThreadOwner, begun.Token, true);
            HostCheck.True(!cancelled.Worker.IsValid);
            f.Drain();
            HostCheck.True(!F.Capture(f.Files.State, 3, out _));
            HostCheck.Equal(3U, f.Open("/data/abandoned-created.bin"));
            HostCheck.True(f.Storage.Device.State->SubmittedBytes == writes && f.Storage.State->VolumeRevision == revision);
        }
    }

    [HostTest("writable root directory and unsupported paths flags mode and legacy stat return explicit errors before unowned effects")]
    private static void FiniteNamespace()
    {
        using var f = new Owner();
        uint root = f.Open("/data");
        uint calls = f.Hardware->Calls;
        HostCheck.Equal(-21L, f.Run(f.Call(0, root, Output, 1)));
        HostCheck.Equal(-29L, f.Run(f.Call(8, root, 0, 0)));
        f.PutPath("/data/../private/sentinel");
        HostCheck.Equal(-13L, f.Run(f.Call(2, Path, 0)));
        f.PutPath("/data/nested/file.bin");
        HostCheck.Equal(-95L, f.Run(f.Call(2, Path, 0)));
        f.PutPath("/data/mode.bin");
        HostCheck.Equal(-95L, f.Run(f.Call(2, Path, 2 | W.Create, 0x1ff)));
        HostCheck.Equal(-20L, f.Run(f.Call(2, Path, W.DirectoryOnly)));
        HostCheck.True(f.Hardware->Calls == calls && f.Storage.State->VolumeRevision == 1);
        HostCheck.Equal(-95L, f.Core(5, root, Output));
        HostCheck.Equal(-95L, f.Core(72, root, 4, F.NonBlock));
        uint file = f.Open(flags: W.NoFollow | W.CloseOnExec);
        HostCheck.Equal(1L, f.Core(72, file, 1));
        HostCheck.Equal((long)(W.NoFollow | W.LargeFile), f.Core(72, file, 3));
    }

    [HostTest("writable global measured non TTY FileStream opens preserve close flags actual media access and unsupported flag boundaries")]
    private static void NonTtyOpen()
    {
        using var f = new Owner();
        uint root = f.Open("/data", 0x98100);
        HostCheck.Equal(1L, f.Core(72, root, 1));
        HostCheck.Equal((long)W.LargeFile, f.Core(72, root, 3));
        uint read = f.Open(flags: 0x88100), write = f.Open(flags: 0x88101);
        HostCheck.Equal(1L, f.Core(72, read, 1));
        HostCheck.Equal((long)W.LargeFile, f.Core(72, read, 3));
        HostCheck.Equal((long)(W.LargeFile | 1), f.Core(72, write, 3));
        HostCheck.Equal(9L, f.Run(f.Call(17, read, Output, 9, 5)));
        HostCheck.True(f.Bytes(9).SequenceEqual(Enumerable.Range(5, 9).Select(Value)));
        HostCheck.Equal(-9L, f.Run(f.WriteCall(read, new byte[] { 71 }, 5)));
        HostCheck.Equal(1L, f.Run(f.WriteCall(write, new byte[] { 71 }, 5)));
        byte[] expected = Enumerable.Range(0, 900).Select(Value).ToArray();
        expected[5] = 71;
        f.Independent("DATA.BIN", expected);
        uint created = f.Open("/data/no-terminal-created.bin", 0x881c1);
        HostCheck.Equal((long)(W.LargeFile | 1), f.Core(72, created, 3));
        uint calls = f.Hardware->Calls;
        f.PutPath("/data/DATA.BIN");
        foreach (uint unsupported in new uint[]
        {
            0x200,
            0x400,
            0x800,
            3
        }

        )
            HostCheck.Equal(-95L, f.Run(f.Call(2, Path, W.NoControllingTerminal | unsupported)));
        HostCheck.Equal(calls, f.Hardware->Calls);
        f.PutPath("/app/fixture.data");
        HostCheck.True(f.Core(2, Path, 0x88100) >= 3);
    }

    [HostTest("writable attachment full tree query excludes adapter provider DMA and observer backing and survives original block stop without copy authority")]
    private static void TreeAliases()
    {
        using var f = new Owner(observe: true);
        uint fd = f.Open(flags: 2);
        byte external = 0x93;
        ulong address = (ulong)&external;
        HostCheck.True(Tree(f.Files.State, address, 1));
        uint calls = f.Hardware->Calls;
        var a = f.Storage.Input;
        var b = f.Storage.Device.Input;
        foreach (var extent in new (ulong Start, ulong Bytes)[]
        {
            ((ulong)f.Disk, (ulong)sizeof(F.WritableDiskState)),
            ((ulong)f.Bindings, f.Files.Admission.DescriptionCount * (ulong)sizeof(F.WritableDiskBinding)),
            ((ulong)f.Provider, (ulong)sizeof(W.State)),
            ((ulong)f.ProviderInput, W.IoBytes),
            ((ulong)f.Slots, 32UL * (ulong)sizeof(W.Descriptor)),
            ((ulong)f.Descriptions, 32UL * (ulong)sizeof(W.Description)),
            ((ulong)f.Processes, 4UL * (ulong)sizeof(W.ProcessSlot)),
            ((ulong)f.Locks, 32UL * (ulong)sizeof(W.RangeLock)),
            ((ulong)f.Scratch, 65UL * (ulong)sizeof(W.RangeLock)),
            ((ulong)f.Storage.State, (ulong)sizeof(C.State)),
            ((ulong)a.Before, C.JournalChanges * 512UL),
            ((ulong)a.After, C.JournalChanges * 512UL),
            ((ulong)f.Storage.Device.State, (ulong)sizeof(B.State)),
            ((ulong)f.Storage.Device.Data, 4096),
            ((ulong)f.Storage.Device.Observation, (ulong)sizeof(B.Observation)),
            (b.Common.VirtualStart, b.Common.Bytes),
            ((ulong)f.Files.State, (ulong)sizeof(F.State)),
            ((ulong)f.Files.Admission.Scratch, f.Files.Admission.ScratchBytes)
        }

        )
        {
            HostCheck.True(!Tree(f.Files.State, extent.Start, extent.Bytes) && !Tree(f.Files.State, extent.Start - 1, 2) && !Tree(f.Files.State, extent.Start + extent.Bytes - 1, 2) && !Tree(f.Files.State, extent.Start - 8, extent.Bytes + 16));
        }

        HostCheck.True(!Tree(f.Files.State, ulong.MaxValue - 1, 8) && f.Hardware->Calls == calls && external == 0x93);
        HostCheck.Equal(B.Status.Success, B.Stop(f.Storage.Device.State));
        HostCheck.True(Tree(f.Files.State, address, 1) && !W.CanBorrowStorage(f.Provider, f.ProviderAdmission.Epoch, address, 1));
        f.PutLock(1, 0, 1);
        uint writes = f.Thread.Writes;
        HostCheck.Equal(-5L, f.Run(f.Call(72, fd, 5, Flock)));
        HostCheck.Equal(writes, f.Thread.Writes);
        HostCheck.Equal(-5L, f.Run(f.Call(72, fd, 6, Flock)));
        HostCheck.True(!f.HasLocks());
        HostCheck.Equal(-5L, f.Run(f.Call(8, fd, 0, 0)));
    }

    [HostTest("writable retained tree deliberate futex pool alias preserves default exclusions and rejects crossing pool or supervisor backing")]
    private static void RetainedUserPool()
    {
        using var f = new Owner(observe: true);
        var memory = f.Files.Memory.Memory;
        ulong pool = (ulong)memory.Pool, bytes = memory.PoolPages * 4096UL;
        HostCheck.True(bytes >= 4096);
        foreach (var range in new (ulong Start, ulong Bytes)[]
        {
            (pool, 4),
            (pool + 128, 4),
            (pool + bytes - 4, 4),
            (pool, bytes)
        }

        )
        {
            HostCheck.True(!Tree(f.Files.State, range.Start, range.Bytes) && !TreeWithUserBacking(f.Files.State, range.Start, range.Bytes, true));
            HostCheck.True(TreeWithUserBacking(f.Files.State, range.Start, range.Bytes, false));
        }

        foreach (var range in new (ulong Start, ulong Bytes)[]
        {
            (pool - 1, 2),
            (pool + bytes - 1, 2),
            (pool - 8, bytes + 16)
        }

        )
            HostCheck.True(!TreeWithUserBacking(f.Files.State, range.Start, range.Bytes, false));
        var a = f.Storage.Input;
        var b = f.Storage.Device.Input;
        foreach (var range in new (ulong Start, ulong Bytes)[]
        {
            ((ulong)f.Files.State, (ulong)sizeof(F.State)),
            ((ulong)f.Files.Admission.Scratch, f.Files.Admission.ScratchBytes),
            ((ulong)f.Files.Admission.Slots, f.Files.Admission.DescriptorCount * (ulong)sizeof(F.Slot)),
            ((ulong)memory.Tables, memory.TablePages * 4096UL),
            ((ulong)memory.CandidateTables, memory.TablePages * 4096UL),
            ((ulong)f.Disk, (ulong)sizeof(F.WritableDiskState)),
            ((ulong)f.Bindings, f.Files.Admission.DescriptionCount * (ulong)sizeof(F.WritableDiskBinding)),
            ((ulong)f.Provider, (ulong)sizeof(W.State)),
            ((ulong)f.ProviderInput, W.IoBytes),
            ((ulong)f.Storage.State, (ulong)sizeof(C.State)),
            ((ulong)a.Before, C.JournalChanges * 512UL),
            ((ulong)f.Storage.Device.State, (ulong)sizeof(B.State)),
            ((ulong)f.Storage.Device.Data, 4096),
            ((ulong)f.Storage.Device.Observation, (ulong)sizeof(B.Observation)),
            (b.Common.VirtualStart, b.Common.Bytes)
        }

        )
        {
            HostCheck.True(!TreeWithUserBacking(f.Files.State, range.Start, range.Bytes, false) && !TreeWithUserBacking(f.Files.State, range.Start - 1, 2, false) && !TreeWithUserBacking(f.Files.State, range.Start + range.Bytes - 1, 2, false));
        }

        byte outside = 19;
        uint calls = f.Hardware->Calls;
        HostCheck.True(TreeWithUserBacking(f.Files.State, (ulong)&outside, 1, false) && !TreeWithUserBacking(f.Files.State, ulong.MaxValue - 1, 8, false));
        HostCheck.True(f.Hardware->Calls == calls && outside == 19);
    }

    [HostTest("writable global attachment validates original epoch primary and disjoint metadata before changing any caller storage")]
    private static void Admission()
    {
        HostCheck.True(sizeof(F.WritableDiskState) <= 4096 && sizeof(F.WritableDiskBinding) * F.MaximumDescriptors <= 4096);
        Console.WriteLine($"HOST_LAYOUT writable-descriptor globalState={sizeof(F.State)} attachment={sizeof(F.WritableDiskState)} request={sizeof(F.WritableDiskRequest)} binding={sizeof(F.WritableDiskBinding)} admission={sizeof(F.WritableDiskAdmission)}");
        using var f = new Owner(attach: false, observe: true);
        byte[] state = new ReadOnlySpan<byte>(f.Disk, sizeof(F.WritableDiskState)).ToArray();
        var a = f.Admission;
        a.Epoch++;
        HostCheck.True(!F.AttachWritableDisk(f.Files.State, f.Disk, a));
        a = f.Admission;
        a.Process = default;
        HostCheck.True(!F.AttachWritableDisk(f.Files.State, f.Disk, a));
        a = f.Admission;
        a.Bindings = (F.WritableDiskBinding*)f.ProviderInput;
        HostCheck.True(!F.AttachWritableDisk(f.Files.State, f.Disk, a));
        HostCheck.True(!F.AttachWritableDisk(f.Files.State, (F.WritableDiskState*)f.Storage.Input.Before, f.Admission));
        HostCheck.True(state.SequenceEqual(new ReadOnlySpan<byte>(f.Disk, sizeof(F.WritableDiskState)).ToArray()));
        HostCheck.True(F.AttachWritableDisk(f.Files.State, f.Disk, f.Admission));
        f.Attached = true;
        HostCheck.True(!F.AttachWritableDisk(f.Files.State, f.Disk, f.Admission) && F.Audit(f.Files.State));
    }
}
