using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using OS.Drivers;
using OS.Kernel.Platform;
using Block = OS.Storage.ContinuingReadOnlyBlockService;
using Fs = OS.Storage.ReadOnlyWebRootFileSystem;

internal static unsafe class ReadOnlyWebRootFileSystemTests
{
    [HostTest("webroot.fs mount metadata demand ranges and exact EOF", HostLane.Fast, 0)]
    private static bool DemandRanges()
    {
        using var f = new Fixture(); f.MountReady(); var file = f.Open("/app/wwwroot/index.html");
        HostCheck.Equal(700U, file.Info.Length); HostCheck.True(!file.Info.IsDirectory && file.Info.Inode > 256);
        HostCheck.Equal(315532800L, file.Info.ModifiedSeconds); HostCheck.Equal(0U, f.Disk.Reads[f.Disk.Data(10)]);
        HostCheck.Equal(0U, f.Disk.Reads[f.Disk.Data(12)]);
        Span<byte> output = stackalloc byte[800]; output.Fill(0xa5);
        f.Read(file.File, 511, 2, output, 2); HostCheck.Equal(Payload(511), output[0]); HostCheck.Equal(Payload(512), output[1]);
        HostCheck.True(output[2..].IndexOfAnyExcept((byte)0xa5) < 0);
        f.Read(file.File, 0, 800, output, 700); for (int i = 0; i < 700; i++) HostCheck.Equal(Payload(i), output[i]);
        Fs.OperationToken token = default; HostCheck.Equal(Fs.Status.InvalidOffset, Fs.BeginRead(f.Mount, file.File, 701, 1, &token));
        HostCheck.Equal(Fs.Status.InvalidOffset, Fs.BeginRead(f.Mount, file.File, ulong.MaxValue, 2, &token));
        HostCheck.Equal(Fs.Status.Pending, Fs.BeginRead(f.Mount, file.File, 700, 10, &token));
        uint bytes = 99; HostCheck.Equal(Fs.Status.EndOfFile, Fs.CopyRead(f.Mount, token, output, &bytes)); HostCheck.Equal(0U, bytes);
        HostCheck.Equal(Fs.Status.Success, Fs.Close(f.Mount, file.File));
        var empty = f.Open("/app/wwwroot/empty.bin"); HostCheck.Equal(0U, empty.Info.Length);
        HostCheck.Equal(Fs.Status.Pending, Fs.BeginRead(f.Mount, empty.File, 0, 1, &token));
        HostCheck.Equal(Fs.Status.EndOfFile, Fs.CopyRead(f.Mount, token, output, &bytes)); HostCheck.Equal(0U, bytes);
        HostCheck.Equal(Fs.Status.Success, Fs.Close(f.Mount, empty.File)); return true;
    }

    [HostTest("webroot.fs confined ASCII namespace rejects traversal prefix case and short aliases", HostLane.Fast, 1)]
    private static bool Namespace()
    {
        using var f = new Fixture(); f.MountReady(); Fs.OperationToken token = default;
        (string Path, Fs.Status Status)[] bad =
        [ ("/private/sentinel.txt", Fs.Status.OutsideRoot), ("/app/wwwroot-other/index.html", Fs.Status.OutsideRoot),
          ("/app/wwwroot/../private/sentinel.txt", Fs.Status.InvalidPath), ("/app/wwwroot/./index.html", Fs.Status.InvalidPath),
          ("/app/wwwroot//index.html", Fs.Status.InvalidPath), ("/app/wwwroot/index.html/", Fs.Status.InvalidPath),
          ("/app/wwwroot/index.html.", Fs.Status.InvalidPath), ("/app/wwwroot/a\\b", Fs.Status.InvalidPath) ];
        ulong before = f.Mount->SectorReads;
        foreach (var item in bad) HostCheck.Equal(item.Status, Fs.BeginOpen(f.Mount, Encoding.ASCII.GetBytes(item.Path), &token));
        HostCheck.Equal(before, f.Mount->SectorReads);
        byte[] utf = Encoding.UTF8.GetBytes("/app/wwwroot/é.txt"); HostCheck.Equal(Fs.Status.UnsupportedEncoding, Fs.BeginOpen(f.Mount, utf, &token));
        foreach (string path in new[] { "/app/wwwroot/INDEX.HTML", "/app/wwwroot/F0000004.DAT", "/app/wwwroot/missing.txt" })
        {
            HostCheck.Equal(Fs.Status.Pending, Fs.BeginOpen(f.Mount, Encoding.ASCII.GetBytes(path), &token));
            HostCheck.Equal(Fs.Status.NotFound, f.Drive(token)); HostCheck.Equal(Fs.Status.Cancelled, Fs.Cancel(f.Mount, token));
        }
        var nested = f.Open("/app/wwwroot/fixture/info.txt"); Span<byte> one = stackalloc byte[1]; f.Read(nested.File, 0, 1, one, 1);
        HostCheck.Equal((byte)0x6e, one[0]); HostCheck.Equal(Fs.Status.Success, Fs.Close(f.Mount, nested.File));
        HostCheck.Equal(0U, f.Disk.Reads[f.Disk.Data(999)]); return true;
    }

    [HostTest("webroot.fs interleaved handles retain independent offsets and identities", HostLane.Fast, 2)]
    private static bool IndependentHandles()
    {
        using var f = new Fixture(); f.MountReady(); Fs.OpenResult a = f.Open("/app/wwwroot/index.html"), b = f.Open("/app/wwwroot/" + Disk.LongName);
        HostCheck.True(a.Info.Inode != b.Info.Inode); Fs.OperationToken x = default, y = default;
        HostCheck.Equal(Fs.Status.Pending, Fs.BeginRead(f.Mount, a.File, 500, 20, &x));
        HostCheck.Equal(Fs.Status.Pending, Fs.BeginRead(f.Mount, b.File, 1, 2, &y));
        HostCheck.Equal(Fs.Status.Busy, Fs.Close(f.Mount, a.File));
        Fs.Status sx = Fs.Status.Pending, sy = Fs.Status.Pending;
        for (int i = 0; i < 2000 && (sx != Fs.Status.Ready || sy != Fs.Status.Ready); i++)
        { f.Tick(); sx = Fs.Poll(f.Mount, x); sy = Fs.Poll(f.Mount, y); }
        HostCheck.Equal(Fs.Status.Ready, sx); HostCheck.Equal(Fs.Status.Ready, sy);
        Span<byte> output = stackalloc byte[32]; uint bytes = 0;
        HostCheck.Equal(Fs.Status.Success, Fs.CopyRead(f.Mount, y, output, &bytes)); HostCheck.Equal(2U, bytes);
        HostCheck.Equal((byte)'B', output[0]); HostCheck.Equal((byte)'C', output[1]);
        HostCheck.Equal(Fs.Status.Success, Fs.CopyRead(f.Mount, x, output, &bytes)); HostCheck.Equal(20U, bytes);
        for (int i = 0; i < 20; i++) HostCheck.Equal(Payload(500 + i), output[i]);
        HostCheck.Equal(Fs.Status.StaleToken, Fs.CopyRead(f.Mount, x, output, &bytes));
        HostCheck.Equal(Fs.Status.Success, Fs.Close(f.Mount, a.File)); HostCheck.Equal(Fs.Status.Success, Fs.Close(f.Mount, b.File));
        var reopened = f.Open("/app/wwwroot/index.html"); HostCheck.Equal(a.Info.Inode, reopened.Info.Inode);
        HostCheck.Equal(Fs.Status.StaleToken, Fs.Close(f.Mount, a.File)); HostCheck.Equal(Fs.Status.Success, Fs.Close(f.Mount, reopened.File)); return true;
    }

    [HostTest("webroot.fs independent cursors preserve split VFAT pending names and linear reads", HostLane.Fast, 3)]
    private static bool Cursors()
    {
        using var f = new Fixture(); f.MountReady(); Fs.OpenResult a = f.Open("/app/wwwroot"), b = f.Open("/app/wwwroot");
        Fs.OperationToken x = default, y = default;
        HostCheck.Equal(Fs.Status.Pending, Fs.BeginDirectory(f.Mount, a.File, &x)); HostCheck.Equal(Fs.Status.Pending, Fs.BeginDirectory(f.Mount, b.File, &y));
        var left = new List<string>(); var right = new List<string>(); Span<byte> name = stackalloc byte[256]; Fs.Metadata info = default;
        bool endedX = false, endedY = false; ulong before = f.Mount->SectorReads;
        for (int i = 0; i < 10000 && (!endedX || !endedY); i++)
        {
            f.Tick();
            foreach (bool first in new[] { true, false })
            {
                if (first ? endedX : endedY) continue;
                var token = first ? x : y; var status = Fs.Poll(f.Mount, token);
                if (status == Fs.Status.EndOfDirectory) { if (first) endedX = true; else endedY = true; continue; }
                if (status != Fs.Status.Entry) { HostCheck.True(status is Fs.Status.Pending or Fs.Status.Busy); continue; }
                ulong reads = f.Mount->SectorReads;
                HostCheck.Equal(Fs.Status.OutputFull, Fs.CopyEntry(f.Mount, token, &info, name[..0]));
                HostCheck.Equal(Fs.Status.Entry, Fs.Poll(f.Mount, token)); HostCheck.Equal(reads, f.Mount->SectorReads);
                HostCheck.Equal(Fs.Status.Success, Fs.CopyEntry(f.Mount, token, &info, name));
                (first ? left : right).Add(Encoding.ASCII.GetString(name[..info.NameBytes]));
            }
        }
        HostCheck.True(endedX && endedY); HostCheck.True(left.SequenceEqual(f.Disk.Names) && right.SequenceEqual(f.Disk.Names));
        HostCheck.True(f.Mount->SectorReads - before <= 10); // Four data sectors per cursor plus one shared FAT pair.
        HostCheck.Equal(Fs.Status.StaleToken, Fs.Poll(f.Mount, x));
        HostCheck.Equal(Fs.Status.Success, Fs.Close(f.Mount, a.File)); HostCheck.Equal(Fs.Status.Success, Fs.Close(f.Mount, b.File)); return true;
    }

    [HostTest("webroot.fs paired FAT cache cold warm eviction and failed fills", HostLane.Fast, 4)]
    private static bool Cache()
    {
        using (var f = new Fixture(cacheSlots: 1))
        {
            f.MountReady(); var a = f.Open("/app/wwwroot/index.html"); HostCheck.Equal(2U, f.Disk.FatReads());
            HostCheck.Equal(Fs.Status.Success, Fs.Close(f.Mount, a.File)); a = f.Open("/app/wwwroot/index.html");
            HostCheck.Equal(2U, f.Disk.FatReads()); HostCheck.Equal(Fs.Status.Success, Fs.Close(f.Mount, a.File));
            a = f.Open("/app/wwwroot/far.bin"); HostCheck.Equal(6U, f.Disk.FatReads()); HostCheck.Equal(Fs.Status.Success, Fs.Close(f.Mount, a.File));
            a = f.Open("/app/wwwroot/index.html"); HostCheck.Equal(8U, f.Disk.FatReads()); HostCheck.True(f.Mount->CacheHits != 0);
            HostCheck.Equal(Fs.Status.Success, Fs.Close(f.Mount, a.File));
        }
        using (var f = new Fixture())
        {
            f.Disk.SetFat(4, 99, mirrorOnly: true); f.MountReady(); Fs.OperationToken token = default;
            HostCheck.Equal(Fs.Status.Pending, Fs.BeginOpen(f.Mount, "/app/wwwroot/index.html"u8, &token));
            HostCheck.Equal(Fs.Status.MirrorMismatch, f.Drive(token)); HostCheck.Equal(Fs.Status.Cancelled, Fs.Cancel(f.Mount, token));
            uint before = f.Disk.FatReads(); f.Disk.SetFat(4, 6, mirrorOnly: true); var a = f.Open("/app/wwwroot/index.html");
            HostCheck.Equal(before + 2, f.Disk.FatReads()); HostCheck.Equal(Fs.Status.Success, Fs.Close(f.Mount, a.File));
        }
        return true;
    }

    [HostTest("webroot.fs chain corruption has distinct bounded outcomes", HostLane.Fast, 5)]
    private static bool Chains()
    {
        (uint Value, Fs.Status Status)[] cases =
        [ (10, Fs.Status.ChainLoop), (0x0fffffff, Fs.Status.Truncated), (0x0ffffff7, Fs.Status.BadCluster),
          (0, Fs.Status.FreeCluster), (1, Fs.Status.ReservedCluster), (0x0ffffff0, Fs.Status.ReservedCluster), (65530, Fs.Status.InvalidCluster) ];
        foreach (var item in cases)
        {
            using var f = new Fixture(); f.Disk.SetFat(10, item.Value); f.MountReady(); Fs.OperationToken token = default;
            HostCheck.Equal(Fs.Status.Pending, Fs.BeginOpen(f.Mount, "/app/wwwroot/index.html"u8, &token));
            HostCheck.Equal(item.Status, f.Drive(token)); HostCheck.True(f.Mount->SectorReads < 20);
            HostCheck.Equal(Fs.Status.Cancelled, Fs.Cancel(f.Mount, token));
        }
        using (var f = new Fixture())
        {
            f.Disk.SetFat(12, 14); f.MountReady(); Fs.OperationToken token = default;
            HostCheck.Equal(Fs.Status.Pending, Fs.BeginOpen(f.Mount, "/app/wwwroot/index.html"u8, &token));
            HostCheck.Equal(Fs.Status.Overlong, f.Drive(token)); HostCheck.Equal(Fs.Status.Cancelled, Fs.Cancel(f.Mount, token));
        }
        using (var f = new Fixture())
        {
            f.Disk.NoDirectoryEnd(); f.Disk.SetFat(8, 4); f.MountReady(); var root = f.Open("/app/wwwroot");
            HostCheck.Equal(Fs.Status.ChainLoop, f.ScanToEnd(root.File)); HostCheck.Equal(Fs.Status.Success, Fs.Close(f.Mount, root.File));
        }
        return true;
    }

    [HostTest("webroot.fs exact directory work history and file map limits", HostLane.Fast, 6)]
    private static bool Budgets()
    {
        using (var f = new Fixture(workBudget: 55, history: 4))
        {
            f.MountReady(); var root = f.Open("/app/wwwroot"); HostCheck.Equal(Fs.Status.EndOfDirectory, f.ScanToEnd(root.File));
            HostCheck.Equal(Fs.Status.Success, Fs.Close(f.Mount, root.File));
        }
        using (var f = new Fixture(workBudget: 54))
        {
            f.MountReady(); var root = f.Open("/app/wwwroot"); HostCheck.Equal(Fs.Status.TraversalBudgetExceeded, f.ScanToEnd(root.File));
            HostCheck.Equal(Fs.Status.Success, Fs.Close(f.Mount, root.File));
        }
        using (var f = new Fixture(history: 3))
        {
            f.MountReady(); var root = f.Open("/app/wwwroot"); HostCheck.Equal(Fs.Status.TraversalBudgetExceeded, f.ScanToEnd(root.File));
            HostCheck.Equal(Fs.Status.Success, Fs.Close(f.Mount, root.File));
        }
        using (var f = new Fixture(map: 1))
        {
            f.MountReady(); Fs.OperationToken token = default;
            HostCheck.Equal(Fs.Status.Pending, Fs.BeginOpen(f.Mount, "/app/wwwroot/index.html"u8, &token));
            HostCheck.Equal(Fs.Status.TraversalBudgetExceeded, f.Drive(token)); HostCheck.Equal(Fs.Status.Cancelled, Fs.Cancel(f.Mount, token));
        }
        return true;
    }

    [HostTest("webroot.fs malformed VFAT case collision and unsupported BPB reject", HostLane.Fast, 7)]
    private static bool Malformed()
    {
        using (var f = new Fixture())
        {
            f.Disk.Sectors[f.Disk.Data(4)][13] ^= 1; f.MountReady(); Fs.OperationToken token = default;
            HostCheck.Equal(Fs.Status.Pending, Fs.BeginOpen(f.Mount, "/app/wwwroot/index.html"u8, &token));
            HostCheck.Equal(Fs.Status.CorruptDirectory, f.Drive(token)); HostCheck.Equal(Fs.Status.Cancelled, Fs.Cancel(f.Mount, token));
        }
        using (var f = new Fixture(collision: true))
        {
            f.MountReady(); Fs.OperationToken token = default;
            HostCheck.Equal(Fs.Status.Pending, Fs.BeginOpen(f.Mount, "/app/wwwroot/index.html"u8, &token));
            HostCheck.Equal(Fs.Status.Ambiguous, f.Drive(token)); HostCheck.Equal(Fs.Status.Cancelled, Fs.Cancel(f.Mount, token));
        }
        foreach (int offset in new[] { 40, 42 })
        {
            using var f = new Fixture(); f.Disk.Sectors[Disk.Partition][offset] = 1;
            HostCheck.Equal(Fs.Status.UnsupportedGeometry, f.StartMount()); HostCheck.Equal(Fs.Status.Success, Fs.Stop(f.Mount));
        }
        return true;
    }

    [HostTest("webroot.fs cancellation drains owned request and stop preserves borrowed block owner", HostLane.Fast, 8)]
    private static bool Cancellation()
    {
        using var f = new Fixture(); f.MountReady(); var file = f.Open("/app/wwwroot/index.html"); Fs.OperationToken token = default;
        HostCheck.Equal(Fs.Status.Pending, Fs.BeginRead(f.Mount, file.File, 0, 10, &token)); HostCheck.Equal(Fs.Status.Pending, Fs.Poll(f.Mount, token));
        HostCheck.Equal(Fs.Status.Pending, Fs.Cancel(f.Mount, token)); HostCheck.Equal(Fs.Status.Busy, Fs.Close(f.Mount, file.File));
        HostCheck.Equal(Fs.Status.Pending, Fs.Poll(f.Mount, token)); f.Tick(); HostCheck.Equal(Fs.Status.Cancelled, Fs.Poll(f.Mount, token));
        HostCheck.Equal(Fs.Status.StaleToken, Fs.Poll(f.Mount, token)); HostCheck.Equal(Fs.Status.Success, Fs.Close(f.Mount, file.File));
        file = f.Open("/app/wwwroot/index.html"); HostCheck.Equal(Fs.Status.Pending, Fs.BeginRead(f.Mount, file.File, 0, 10, &token));
        HostCheck.Equal(Fs.Status.Pending, Fs.Poll(f.Mount, token)); f.Tick(); Block.Completion completion = default;
        HostCheck.Equal(Block.Status.Ready, Block.Poll(f.Device, &completion));
        HostCheck.Equal(Fs.Status.Cancelled, Fs.Cancel(f.Mount, token)); HostCheck.Equal(Fs.Status.Success, Fs.Close(f.Mount, file.File));
        file = f.Open("/app/wwwroot/index.html"); HostCheck.Equal(Fs.Status.Pending, Fs.BeginRead(f.Mount, file.File, 0, 10, &token));
        HostCheck.Equal(Fs.Status.Pending, Fs.Poll(f.Mount, token)); HostCheck.Equal(Fs.Status.Pending, Fs.Stop(f.Mount));
        HostCheck.True(!f.Mount->IsRetired && f.Device->Running); f.Tick(); HostCheck.Equal(Fs.Status.Success, Fs.Stop(f.Mount));
        HostCheck.True(f.Mount->IsRetired && f.Device->Running); HostCheck.Equal(1UL, f.Pool->AllocatedMask); return true;
    }

    [HostTest("webroot.fs I/O failure never publishes partial range or releases uncertain owner", HostLane.Fast, 9)]
    private static bool IoFailure()
    {
        using var f = new Fixture(); f.MountReady(); var file = f.Open("/app/wwwroot/index.html"); f.Disk.FailLba = f.Disk.Data(12);
        Fs.OperationToken token = default; HostCheck.Equal(Fs.Status.Pending, Fs.BeginRead(f.Mount, file.File, 0, 700, &token));
        HostCheck.Equal(Fs.Status.DeviceFailed, f.Drive(token)); Span<byte> output = stackalloc byte[700]; output.Fill(0xa5); uint bytes = 77;
        HostCheck.Equal(Fs.Status.InvalidState, Fs.CopyRead(f.Mount, token, output, &bytes)); HostCheck.Equal(77U, bytes);
        HostCheck.True(output.IndexOfAnyExcept((byte)0xa5) < 0); HostCheck.Equal(Fs.Status.DeviceFailed, Fs.Stop(f.Mount));
        HostCheck.Equal(1UL, f.Pool->AllocatedMask); HostCheck.Equal(Block.Status.Success, Block.Stop(f.Device));
        HostCheck.Equal(Fs.Status.Success, Fs.Stop(f.Mount)); return true;
    }

    [HostTest("webroot.fs output aliases and copied or stale owners cannot mutate retained storage", HostLane.Fast, 10)]
    private static bool Aliases()
    {
        using var f = new Fixture(); f.MountReady(); byte[] before = new ReadOnlySpan<byte>(f.Arena, Fixture.ArenaBytes).ToArray();
        foreach (ulong address in new[] { 0UL, 1UL, 0x0000800000000000UL, (ulong)f.Mount, (ulong)f.Operations,
            (ulong)f.Files, (ulong)f.Cache, (ulong)f.Operations[0].Storage.Path, (ulong)f.Operations[1].Storage.Range,
            (ulong)f.Device, (ulong)f.Data + 128, Common })
            HostCheck.Equal(Fs.Status.InvalidStorage, Fs.BeginOpen(f.Mount, "/app/wwwroot/index.html"u8, (Fs.OperationToken*)address));
        HostCheck.True(new ReadOnlySpan<byte>(f.Arena, Fixture.ArenaBytes).SequenceEqual(before));
        Fs.Mount copy = *f.Mount; Fs.OperationToken token = default;
        HostCheck.Equal(Fs.Status.InvalidState, Fs.BeginOpen(&copy, "/app/wwwroot/index.html"u8, &token));
        var file = f.Open("/app/wwwroot/index.html"); HostCheck.Equal(Fs.Status.Pending, Fs.BeginRead(f.Mount, file.File, 0, 1, &token));
        HostCheck.Equal(Fs.Status.Ready, f.Drive(token)); uint bytes = 123;
        HostCheck.Equal(Fs.Status.InvalidStorage, Fs.CopyRead(f.Mount, token, new Span<byte>(f.Cache[0].Data, 1), &bytes));
        HostCheck.Equal(123U, bytes); Span<byte> output = stackalloc byte[8]; output.Fill(0xa5);
        fixed (byte* p = output) HostCheck.Equal(Fs.Status.InvalidStorage, Fs.CopyRead(f.Mount, token, output, (uint*)p));
        HostCheck.True(output.IndexOfAnyExcept((byte)0xa5) < 0); HostCheck.Equal(Fs.Status.Success, Fs.CopyRead(f.Mount, token, output, &bytes));
        HostCheck.Equal(Fs.Status.Success, Fs.Close(f.Mount, file.File)); HostCheck.Equal(Fs.Status.Success, Fs.Stop(f.Mount));
        HostCheck.Equal(Fs.Status.InvalidState, Fs.Poll(f.Mount, token)); return true;
    }

    [HostTest("webroot.fs warmed complete operations allocate no managed memory", HostLane.Fast, 11)]
    private static bool NoAllocation()
    {
        using var f = new Fixture(); f.MountReady(); HostCheck.True(f.OneRead()); long before = GC.GetAllocatedBytesForCurrentThread();
        bool good = true; for (int i = 0; i < 32; i++) good &= f.OneRead();
        long after = GC.GetAllocatedBytesForCurrentThread(); HostCheck.True(good); HostCheck.Equal(before, after); return true;
    }

    [HostTest("webroot.fs scratch admission is independent of advertised volume size", HostLane.Fast, 12)]
    private static bool VolumeBound()
    {
        Span<byte> data = stackalloc byte[1];
        foreach (uint clusters in new[] { 65525U, 1000000U })
        {
            using var f = new Fixture(clusters: clusters); f.MountReady(); var file = f.Open("/app/wwwroot/index.html");
            HostCheck.True(f.UsedArenaBytes < Fixture.ArenaBytes); f.Read(file.File, 699, 1, data, 1);
            HostCheck.Equal(Payload(699), data[0]); HostCheck.Equal(Fs.Status.Success, Fs.Close(f.Mount, file.File));
        }
        return true;
    }

    [HostTest("webroot.fs device reset revokes cached cursor ownership", HostLane.Fast, 13)]
    private static bool ResetGeneration()
    {
        using var f = new Fixture(); f.MountReady(); var file = f.Open("/app/wwwroot/index.html");
        HostCheck.Equal(Fs.Status.Success, Fs.Close(f.Mount, file.File)); var root = f.Open("/app/wwwroot"); Fs.OperationToken token = default;
        HostCheck.Equal(Fs.Status.Pending, Fs.BeginDirectory(f.Mount, root.File, &token)); ulong hits = f.Mount->CacheHits;
        HostCheck.Equal(Block.Status.Success, Block.Stop(f.Device)); HostCheck.Equal(Fs.Status.DeviceFailed, Fs.Poll(f.Mount, token));
        HostCheck.Equal(hits, f.Mount->CacheHits); HostCheck.Equal(Fs.Status.InvalidState, Fs.BeginOpen(f.Mount, "/app/wwwroot/index.html"u8, &token));
        HostCheck.Equal(Fs.Status.Success, Fs.Stop(f.Mount)); HostCheck.True(f.Mount->IsRetired); return true;
    }

    [HostTest("webroot.fs admission rejects overlapping scratch before mutation", HostLane.Fast, 14)]
    private static bool Admission()
    {
        using var f = new Fixture(); var input = f.Input; input.CacheCount = 0;
        HostCheck.Equal(Fs.Status.InvalidAccess, Fs.Start(f.Mount, input));
        byte* original = f.Cache[0].Data; f.Cache[0].Data = f.Operations[0].Storage.Sector;
        HostCheck.Equal(Fs.Status.InvalidAccess, Fs.Start(f.Mount, f.Input)); f.Cache[0].Data = original;
        uint* history = f.Operations[1].Storage.History; f.Operations[1].Storage.History = f.Operations[0].Storage.History;
        HostCheck.Equal(Fs.Status.InvalidAccess, Fs.Start(f.Mount, f.Input)); f.Operations[1].Storage.History = history;
        HostCheck.True(!f.Mount->Ready && !f.Mount->RequiresCleanup); HostCheck.Equal(1UL, f.Pool->AllocatedMask); f.MountReady(); return true;
    }

    [HostTest("webroot.fs verified FAT fills remain pinned across competing opens", HostLane.Fast, 15)]
    private static bool PinnedCache()
    {
        using var f = new Fixture(cacheSlots: 1); f.MountReady(); Fs.OperationToken a = default, b = default;
        HostCheck.Equal(Fs.Status.Pending, Fs.BeginOpen(f.Mount, "/app/wwwroot/far.bin"u8, &a));
        HostCheck.Equal(Fs.Status.Pending, Fs.BeginOpen(f.Mount, "/app/wwwroot/index.html"u8, &b));
        Fs.Status sa = Fs.Status.Pending, sb = Fs.Status.Pending;
        for (int i = 0; i < 10000 && (sa != Fs.Status.Ready || sb != Fs.Status.Ready); i++)
        {
            f.Tick(); sa = Fs.Poll(f.Mount, a); sb = Fs.Poll(f.Mount, b);
            HostCheck.True(sa is Fs.Status.Pending or Fs.Status.Busy or Fs.Status.Ready);
            HostCheck.True(sb is Fs.Status.Pending or Fs.Status.Busy or Fs.Status.Ready);
        }
        HostCheck.Equal(Fs.Status.Ready, sa); HostCheck.Equal(Fs.Status.Ready, sb); Fs.OpenResult x = default, y = default;
        HostCheck.Equal(Fs.Status.Success, Fs.CopyOpen(f.Mount, a, &x)); HostCheck.Equal(Fs.Status.Success, Fs.CopyOpen(f.Mount, b, &y));
        HostCheck.Equal(1024U, x.Info.Length); HostCheck.Equal(700U, y.Info.Length);
        HostCheck.Equal(Fs.Status.Success, Fs.Close(f.Mount, x.File)); HostCheck.Equal(Fs.Status.Success, Fs.Close(f.Mount, y.File)); return true;
    }

    [HostTest("webroot.fs operation and file generations reject exhaustion without wrap", HostLane.Fast, 16)]
    private static bool GenerationExhaustion()
    {
        using var f = new Fixture(); f.MountReady();
        int opOffset = (int)Marshal.OffsetOf<Fs.Operation>("Generation"), fileOffset = (int)Marshal.OffsetOf<Fs.FileHandle>("Generation");
        for (int i = 0; i < 2; i++) *(ulong*)((byte*)(f.Operations + i) + opOffset) = ulong.MaxValue;
        Fs.OperationToken token = default; ulong reads = f.Mount->SectorReads;
        HostCheck.Equal(Fs.Status.GenerationExhausted, Fs.BeginOpen(f.Mount, "/app/wwwroot"u8, &token)); HostCheck.True(!token.IsInitialized);
        *(ulong*)((byte*)f.Operations + opOffset) = ulong.MaxValue - 1;
        HostCheck.Equal(Fs.Status.Pending, Fs.BeginOpen(f.Mount, "/app/wwwroot"u8, &token)); HostCheck.Equal(Fs.Status.Ready, f.Drive(token));
        Fs.OpenResult result = default; HostCheck.Equal(Fs.Status.Success, Fs.CopyOpen(f.Mount, token, &result));
        HostCheck.Equal(Fs.Status.Success, Fs.Close(f.Mount, result.File));
        HostCheck.Equal(Fs.Status.GenerationExhausted, Fs.BeginOpen(f.Mount, "/app/wwwroot"u8, &token));
        for (int i = 0; i < 2; i++)
        {
            *(ulong*)((byte*)(f.Operations + i) + opOffset) = ulong.MaxValue - 1;
            *(ulong*)((byte*)(f.Files + i) + fileOffset) = ulong.MaxValue;
        }
        HostCheck.Equal(Fs.Status.GenerationExhausted, Fs.BeginOpen(f.Mount, "/app/wwwroot"u8, &token));
        HostCheck.Equal(reads, f.Mount->SectorReads); return true;
    }

    [HostTest("webroot.fs reset revokes retained ready ranges and pending directory entries", HostLane.Fast, 17)]
    private static bool RetainedReset()
    {
        Span<byte> output = stackalloc byte[256];
        for (int mode = 0; mode < 2; mode++)
        {
            using var f = new Fixture(); f.MountReady(); var file = f.Open(mode == 0 ? "/app/wwwroot/index.html" : "/app/wwwroot");
            Fs.OperationToken token = default;
            HostCheck.Equal(Fs.Status.Pending, mode == 0 ? Fs.BeginRead(f.Mount, file.File, 0, 8, &token) : Fs.BeginDirectory(f.Mount, file.File, &token));
            HostCheck.Equal(mode == 0 ? Fs.Status.Ready : Fs.Status.Entry, f.Drive(token));
            HostCheck.Equal(Block.Status.Success, Block.Stop(f.Device)); ulong hits = f.Mount->CacheHits;
            HostCheck.Equal(Fs.Status.DeviceFailed, Fs.Poll(f.Mount, token)); HostCheck.Equal(hits, f.Mount->CacheHits);
            output.Fill(0xa5); uint bytes = 77; Fs.Metadata info = new() { Inode = 123 };
            HostCheck.Equal(Fs.Status.InvalidState, mode == 0 ? Fs.CopyRead(f.Mount, token, output, &bytes) : Fs.CopyEntry(f.Mount, token, &info, output));
            HostCheck.Equal(77U, bytes); HostCheck.Equal(123UL, info.Inode); HostCheck.True(output.IndexOfAnyExcept((byte)0xa5) < 0);
            HostCheck.Equal(Fs.Status.Success, Fs.Stop(f.Mount));
        }
        return true;
    }

    [HostTest("webroot.fs live inspection uses checked allocation and rejects stale reset and output aliases")]
    private static void Inspection()
    {
        using var f = new Fixture(); f.MountReady(); var file = f.Open("/app/wwwroot/index.html");
        Fs.FileInspection info = default;
        HostCheck.Equal(Fs.Status.Success, Fs.Inspect(f.Mount, file.File, &info));
        HostCheck.True(info.Info.Length == 700 && info.AllocationKnown && info.AllocatedBlocks == 2);
        byte[] before = new ReadOnlySpan<byte>(f.Mount, sizeof(Fs.Mount)).ToArray();
        HostCheck.Equal(Fs.Status.InvalidStorage, Fs.Inspect(f.Mount, file.File, (Fs.FileInspection*)f.Mount));
        HostCheck.True(new ReadOnlySpan<byte>(f.Mount, sizeof(Fs.Mount)).SequenceEqual(before));
        HostCheck.Equal(Fs.Status.InvalidStorage, Fs.Inspect(f.Mount, file.File, (Fs.FileInspection*)f.Data));
        HostCheck.Equal(Fs.Status.Success, Fs.Close(f.Mount, file.File));
        var saved = info;
        HostCheck.Equal(Fs.Status.StaleToken, Fs.Inspect(f.Mount, file.File, &info));
        HostCheck.True(new ReadOnlySpan<byte>(&saved, sizeof(Fs.FileInspection)).SequenceEqual(new ReadOnlySpan<byte>(&info, sizeof(Fs.FileInspection))));
        var directory = f.Open("/app/wwwroot");
        HostCheck.Equal(Fs.Status.Success, Fs.Inspect(f.Mount, directory.File, &info));
        HostCheck.True(info.Info.IsDirectory && !info.AllocationKnown && info.AllocatedBlocks == 0);
        saved = info; HostCheck.Equal(Block.Status.Success, Block.Stop(f.Device));
        HostCheck.Equal(Fs.Status.InvalidState, Fs.Inspect(f.Mount, directory.File, &info));
        HostCheck.True(new ReadOnlySpan<byte>(&saved, sizeof(Fs.FileInspection)).SequenceEqual(new ReadOnlySpan<byte>(&info, sizeof(Fs.FileInspection))));
    }

    private static byte Payload(int index) => unchecked((byte)(index * 31 ^ (index >> 8) ^ 0x5a));
    private const ulong Common = 0x400000, Notify = 0x403000, Config = 0x402000, Physical = 0x220000, DeviceBase = 0x123456780000;

    internal sealed class Disk
    {
        internal const ulong Partition = 2048;
        internal const string LongName = "a-long-vfat-name-crossing-sector-boundaries.bin";
        internal readonly Dictionary<ulong, byte[]> Sectors = [];
        internal readonly uint[] Reads;
        internal readonly string[] Names;
        internal readonly uint FatSectors;
        internal readonly ulong Capacity, Last, FatFirst, DataFirst;
        internal ulong FailLba = ulong.MaxValue;
        private int alias;
        internal Disk(uint clusters, bool collision)
        {
            FatSectors = (clusters + 2 + 127) / 128; FatFirst = Partition + 32; DataFirst = FatFirst + 2UL * FatSectors;
            uint total = 32 + FatSectors * 2 + clusters; Last = Partition + total - 1; Capacity = Last + 35; Reads = new uint[(int)Capacity];
            byte[] bpb = Sector(Partition); W16(bpb, 11, 512); bpb[13] = 1; W16(bpb, 14, 32); bpb[16] = 2;
            W32(bpb, 32, total); W32(bpb, 36, FatSectors); W32(bpb, 44, 2); bpb[510] = 0x55; bpb[511] = 0xaa;
            SetFat(0, 0x0ffffff8); SetFat(1, 0x0fffffff);
            foreach (uint c in new uint[] { 2, 3, 5, 8, 11, 12, 13, 400 }) SetFat(c, 0x0fffffff);
            SetFat(4, 6); SetFat(6, 7); SetFat(7, 8); SetFat(10, 12); SetFat(200, 400);
            WriteDirectory([2], Entries("app", 3, 0, true)); WriteDirectory([3], Entries("wwwroot", 4, 0, true));
            WriteDirectory([5], Entries("info.txt", 11, 1, false)); Sector(Data(11))[0] = 0x6e;
            var entries = new List<byte[]>(); var names = new List<string>();
            Add("index.html", 10, 700); Add("empty.bin", 0, 0); Add("fixture", 5, 0, true);
            while (entries.Count < 15) { byte[] deleted = new byte[32]; deleted[0] = 0xe5; entries.Add(deleted); }
            Add(LongName, 13, 3); Add("far.bin", 200, 1024);
            for (uint i = 0; i < 16; i++) { uint c = 100 + i; SetFat(c, 0x0fffffff); Add("f" + i.ToString("000") + ".bin", c, 1); Sector(Data(c))[0] = (byte)i; }
            if (collision) Add("INDEX.HTML", 13, 3);
            WriteDirectory([4, 6, 7, 8], entries); Names = names.ToArray();
            for (int i = 0; i < 700; i++) Sector(Data(i < 512 ? 10U : 12U))[i & 511] = Payload(i);
            "ABC"u8.CopyTo(Sector(Data(13)));
            void Add(string name, uint cluster, uint bytes, bool directory = false)
            { entries.AddRange(Entries(name, cluster, bytes, directory)); names.Add(name); }
        }
        internal ulong Data(uint cluster) => DataFirst + cluster - 2;
        internal byte[] Sector(ulong lba)
        { if (!Sectors.TryGetValue(lba, out var bytes)) Sectors.Add(lba, bytes = new byte[512]); return bytes; }
        internal void SetFat(uint cluster, uint value, bool mirrorOnly = false)
        {
            for (uint copy = mirrorOnly ? 1U : 0; copy < 2; copy++) W32(Sector(FatFirst + copy * FatSectors + cluster / 128), (int)(cluster % 128) * 4, value);
        }
        internal uint FatReads()
        { uint n = 0; for (ulong i = FatFirst; i < DataFirst; i++) n += Reads[i]; return n; }
        internal void NoDirectoryEnd()
        { for (int i = 0; i < 16; i++) { byte[] data = Sector(Data(8)); if (data[i * 32] == 0) data[i * 32] = 0xe5; } }
        private List<byte[]> Entries(string name, uint cluster, uint bytes, bool directory)
        {
            string shortName = "F" + (++alias).ToString("0000000") + "DAT"; byte[] shortEntry = new byte[32]; Encoding.ASCII.GetBytes(shortName).CopyTo(shortEntry, 0);
            shortEntry[11] = directory ? (byte)0x10 : (byte)0x21; W16(shortEntry, 24, 0x21); W16(shortEntry, 20, (ushort)(cluster >> 16)); W16(shortEntry, 26, (ushort)cluster); W32(shortEntry, 28, bytes);
            byte checksum = 0; foreach (byte b in shortEntry.AsSpan(0, 11)) checksum = unchecked((byte)(((checksum & 1) << 7) + (checksum >> 1) + b));
            int count = (name.Length + 12) / 13; List<byte[]> entries = [];
            int[] positions = [1, 3, 5, 7, 9, 14, 16, 18, 20, 22, 24, 28, 30];
            for (int ordinal = count; ordinal > 0; ordinal--)
            {
                byte[] entry = new byte[32]; entry[0] = (byte)(ordinal | (ordinal == count ? 0x40 : 0)); entry[11] = 15; entry[13] = checksum;
                for (int i = 0; i < 13; i++) { int at = (ordinal - 1) * 13 + i; W16(entry, positions[i], at < name.Length ? name[at] : at == name.Length ? (ushort)0 : ushort.MaxValue); }
                entries.Add(entry);
            }
            entries.Add(shortEntry); return entries;
        }
        private void WriteDirectory(uint[] clusters, List<byte[]> entries)
        { for (int i = 0; i < entries.Count; i++) entries[i].CopyTo(Sector(Data(clusters[i / 16])), i % 16 * 32); }
        private static void W16(byte[] b, int at, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(at), value);
        private static void W32(byte[] b, int at, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(at), value);
    }

    private struct Hardware
    {
        internal byte* Data;
        internal ulong Capacity, Descriptor, Available, Used, Sector;
        internal uint Produced;
        internal ushort Selected, Size, Enabled, ConfigVector, QueueVector;
        internal byte Status;
        internal bool Bus, Pending;
    }
    [UnmanagedCallersOnly] private static byte Read8(void* c, ulong address, byte* value)
    {
        var h = (Hardware*)c;
        if (address == Common + 20) *value = h->Status;
        else if (address == Common + 21) *value = 7;
        else if (address == (ulong)h->Data + 272) *value = h->Data[272];
        else return 0; return 1;
    }
    [UnmanagedCallersOnly] private static byte Write8(void* c, ulong address, byte value)
    { var h = (Hardware*)c; if (address != Common + 20 || value is not (0 or 15)) return 0; h->Status = value; if (value == 0) h->Enabled = 0; return 1; }
    [UnmanagedCallersOnly] private static byte Read16(void* c, ulong address, ushort* value)
    {
        var h = (Hardware*)c;
        if (address == (ulong)h->Data + 150) { *value = *(ushort*)(h->Data + 150); return 1; }
        switch (address - Common)
        {
            case 16: *value = h->ConfigVector; break; case 18: *value = 1; break; case 22: *value = h->Selected; break;
            case 24: *value = h->Size == 0 ? (ushort)8 : h->Size; break; case 26: *value = h->QueueVector; break;
            case 28: *value = h->Enabled; break; case 30: *value = 0; break; default: return 0;
        }
        return 1;
    }
    [UnmanagedCallersOnly] private static byte Write16(void* c, ulong address, uint value)
    {
        var h = (Hardware*)c;
        if (address == Notify) { if (h->Pending || h->Status != 15 || !h->Bus) return 0; h->Pending = true; h->Sector = *(ulong*)(h->Data + 264); return 1; }
        switch (address - Common)
        {
            case 16: h->ConfigVector = (ushort)value; break; case 22: h->Selected = (ushort)value; break; case 24: h->Size = (ushort)value; break;
            case 26: h->QueueVector = (ushort)value; break; case 28: h->Enabled = (ushort)value; break; default: return 0;
        }
        return 1;
    }
    [UnmanagedCallersOnly] private static byte Read32(void* c, ulong address, uint* value)
    {
        var h = (Hardware*)c;
        if (address == Config) { *value = (uint)h->Capacity; return 1; } if (address == Config + 4) { *value = (uint)(h->Capacity >> 32); return 1; }
        if (address >= (ulong)h->Data + 152 && address < (ulong)h->Data + 216) { *value = *(uint*)address; return 1; }
        ulong offset = address - Common; if (offset < 32 || offset > 52 || (offset & 3) != 0) return 0;
        ulong bits = offset < 40 ? h->Descriptor : offset < 48 ? h->Available : h->Used; *value = (uint)(bits >> ((offset & 4) == 0 ? 0 : 32)); return 1;
    }
    [UnmanagedCallersOnly] private static byte Write32(void* c, ulong address, uint value)
    {
        var h = (Hardware*)c; ulong offset = address - Common; if (offset < 32 || offset > 52 || (offset & 3) != 0) return 0;
        ulong* p = offset < 40 ? &h->Descriptor : offset < 48 ? &h->Available : &h->Used; int shift = (offset & 4) == 0 ? 0 : 32;
        *p = (*p & ~(0xffffffffUL << shift)) | ((ulong)value << shift); return 1;
    }
    [UnmanagedCallersOnly] private static byte Fence(void* c) => 1;
    [UnmanagedCallersOnly] private static byte BusOn(void* c) { ((Hardware*)c)->Bus = true; return 1; }
    [UnmanagedCallersOnly] private static byte BusOff(void* c) { ((Hardware*)c)->Bus = false; return 1; }
    [UnmanagedCallersOnly] private static byte Clock(void* c, ulong* value) { *value = 0; return 1; }

    internal sealed class Fixture : IDisposable
    {
        internal const int ArenaBytes = 262144;
        private static long epoch;
        internal readonly byte* Arena;
        private readonly uint arenaCapacity;
        private readonly byte* blockBacking = (byte*)NativeMemory.AlignedAlloc(4096, 4096);
        internal byte* Data => blockBacking;
        internal readonly Block.State* Device = (Block.State*)NativeMemory.AllocZeroed((nuint)sizeof(Block.State));
        internal readonly DmaBufferPool* Pool = (DmaBufferPool*)NativeMemory.AllocZeroed((nuint)sizeof(DmaBufferPool));
        private readonly DmaBufferSlot* dmaSlot = (DmaBufferSlot*)NativeMemory.AllocZeroed((nuint)sizeof(DmaBufferSlot));
        private readonly Hardware* hardware = (Hardware*)NativeMemory.AllocZeroed((nuint)sizeof(Hardware));
        internal readonly Fs.Mount* Mount;
        internal readonly Fs.Operation* Operations;
        internal readonly Fs.FileHandle* Files;
        internal readonly Fs.CacheSlot* Cache;
        internal readonly Disk Disk;
        private readonly Fs.Admission input;
        internal Fs.Admission Input => input;
        private uint arenaAt;
        internal uint UsedArenaBytes => arenaAt;
        internal Fixture(uint cacheSlots = 2, uint workBudget = 65536, uint history = 4096, uint map = 4096, uint clusters = 65525, bool collision = false, uint slots = 2)
        {
            HostCheck.True(slots is >= 2 and <= 8);
            arenaCapacity = slots == 2 ? (uint)ArenaBytes : 1048576U;
            Arena = (byte*)NativeMemory.AlignedAlloc(arenaCapacity, 4096);
            new Span<byte>(Arena, (int)arenaCapacity).Clear(); new Span<byte>(Data, 4096).Clear(); Disk = new(clusters, collision);
            ulong lifetime = (ulong)Interlocked.Increment(ref epoch);
            HostCheck.Equal(DmaAddressStatus.Success, DmaAddressWindow.TryCreate((ulong)Data, Physical, DeviceBase, 4096, 64, out var window));
            HostCheck.Equal(DmaBufferStatus.Success, PageAlignedDmaBuffers.TryInitialize(Pool, window, dmaSlot, 1, lifetime));
            hardware->Data = Data; hardware->Capacity = Disk.Capacity; hardware->Status = 11;
            PciSizedBar* bars = stackalloc PciSizedBar[6]; new Span<PciSizedBar>(bars, 6).Clear();
            HostCheck.Equal(PciBarProbeStatus.Success, BARPrefetchTypeRegressionTestsDerivedMOSAFailures.Decode(0xc, 0xc0, 0xffffc00c, 0xffffffff, 4, out bars[4]));
            var caps = VirtioPciCapabilitySet.Create(new(VirtioPciCapabilityType.CommonConfiguration, 4, 0, 56, 0),
                new(VirtioPciCapabilityType.NotifyConfiguration, 4, 0x3000, 64, 4), new(VirtioPciCapabilityType.IsrStatus, 4, 0x1000, 1, 0),
                new(VirtioPciCapabilityType.DeviceConfiguration, 4, 0x2000, 8, 0));
            VirtioConfigurationRegion* regions = stackalloc VirtioConfigurationRegion[4];
            HostCheck.Equal(VirtioRegionStatus.Success, CommonNotifyISRDeviceConfigurationRegions.PlanBlock(caps, bars, 6, regions, 4));
            HostCheck.Equal(VirtioRegionStatus.Success, CommonNotifyISRDeviceConfigurationRegions.Bind(regions[0], Common, out var common));
            HostCheck.Equal(VirtioRegionStatus.Success, CommonNotifyISRDeviceConfigurationRegions.BindNotificationPage(regions[1], 64, Notify, out var notify));
            HostCheck.Equal(VirtioRegionStatus.Success, CommonNotifyISRDeviceConfigurationRegions.Bind(regions[3], Config, out var device));
            Block.Admission block = new() { Common = common, Notify = notify, Device = device, NotifyMultiplier = 4, ResetLimit = 3, Pool = Pool,
                Epoch = lifetime, NegotiatedFeatures = BoundedVirtioFeatures.Version1, ExpectedCapacity = Disk.Capacity, CompletionTimeoutNanos = 1000,
                Access = new() { Queue = new() { Context = hardware, Read8 = &Read8, Write8 = &Write8, Read16 = &Read16, Write16 = &Write16,
                    Read32 = &Read32, Write32 = &Write32, Fence = &Fence, BusMasterOff = &BusOff }, BusMasterOn = &BusOn }, Clock = new() { Context = hardware, Read = &Clock } };
            HostCheck.Equal(Block.Status.Success, Block.Start(Device, block));
            Mount = (Fs.Mount*)Take((uint)sizeof(Fs.Mount)); Operations = (Fs.Operation*)Take(slots * (uint)sizeof(Fs.Operation));
            Files = (Fs.FileHandle*)Take(slots * (uint)sizeof(Fs.FileHandle)); Cache = (Fs.CacheSlot*)Take(cacheSlots * (uint)sizeof(Fs.CacheSlot));
            for (uint i = 0; i < slots; i++)
            {
                Operations[i].Storage = new() { Path = Take(256), Sector = Take(512), Name = (ushort*)Take(520),
                    History = (uint*)Take(history * 4), HistoryCapacity = history, Range = Take(65536), RangeCapacity = 65536 };
                Files[i].Clusters = (uint*)Take(map * 4); Files[i].ClusterCapacity = map;
            }
            for (uint i = 0; i < cacheSlots; i++) Cache[i].Data = Take(512);
            input = new() { Device = Device, Epoch = lifetime, PartitionFirst = ReadOnlyWebRootFileSystemTests.Disk.Partition, PartitionLast = Disk.Last,
                Operations = Operations, OperationCount = slots, Files = Files, FileCount = slots, Cache = Cache, CacheCount = cacheSlots, DirectoryEntryBudget = workBudget };
        }
        private byte* Take(uint bytes) { byte* p = Arena + arenaAt; arenaAt = (arenaAt + bytes + 7) & ~7U; if (arenaAt > arenaCapacity) throw new InvalidOperationException("fixture_arena"); return p; }
        internal void Tick()
        {
            if (!hardware->Pending) return;
            ulong lba = hardware->Sector; Disk.Reads[lba]++;
            if (Disk.Sectors.TryGetValue(lba, out var bytes)) bytes.CopyTo(new Span<byte>(Data + 512, 512)); else new Span<byte>(Data + 512, 512).Clear();
            bool failure = lba == Disk.FailLba; Data[272] = failure ? (byte)1 : (byte)0;
            byte* used = Data + 152 + (hardware->Produced & 7) * 8; *(uint*)used = 0; *(uint*)(used + 4) = failure ? 1U : 513U;
            hardware->Produced++; *(ushort*)(Data + 150) = unchecked((ushort)hardware->Produced); hardware->Pending = false;
        }
        internal Fs.Status StartMount()
        {
            Fs.Status status = Fs.Start(Mount, input);
            for (int i = 0; i < 10000 && status == Fs.Status.Pending; i++) { Tick(); status = Fs.PumpMount(Mount); }
            return status;
        }
        internal void MountReady() => HostCheck.Equal(Fs.Status.Ready, StartMount());
        internal Fs.Status Drive(Fs.OperationToken token)
        {
            Fs.Status status = Fs.Status.Pending;
            for (int i = 0; i < 100000 && status is Fs.Status.Pending or Fs.Status.Busy; i++) { Tick(); status = Fs.Poll(Mount, token); }
            return status;
        }
        internal Fs.OpenResult Open(string path)
        {
            Fs.OperationToken token = default; HostCheck.Equal(Fs.Status.Pending, Fs.BeginOpen(Mount, Encoding.ASCII.GetBytes(path), &token));
            HostCheck.Equal(Fs.Status.Ready, Drive(token)); Fs.OpenResult result = default;
            HostCheck.Equal(Fs.Status.Success, Fs.CopyOpen(Mount, token, &result)); return result;
        }
        internal void Read(Fs.FileToken file, ulong offset, uint count, Span<byte> output, uint expected)
        {
            Fs.OperationToken token = default; HostCheck.Equal(Fs.Status.Pending, Fs.BeginRead(Mount, file, offset, count, &token));
            HostCheck.Equal(Fs.Status.Ready, Drive(token)); uint bytes = 0; HostCheck.Equal(Fs.Status.Success, Fs.CopyRead(Mount, token, output, &bytes)); HostCheck.Equal(expected, bytes);
        }
        internal Fs.Status ScanToEnd(Fs.FileToken file)
        {
            Fs.OperationToken token = default; HostCheck.Equal(Fs.Status.Pending, Fs.BeginDirectory(Mount, file, &token));
            Span<byte> name = stackalloc byte[256]; Fs.Metadata info = default;
            for (int i = 0; i < 10000; i++)
            {
                var status = Drive(token);
                if (status == Fs.Status.EndOfDirectory) return status;
                if (status != Fs.Status.Entry) { HostCheck.Equal(Fs.Status.Cancelled, Fs.Cancel(Mount, token)); return status; }
                HostCheck.Equal(Fs.Status.OutputFull, Fs.CopyEntry(Mount, token, &info, name[..0]));
                HostCheck.Equal(Fs.Status.Success, Fs.CopyEntry(Mount, token, &info, name));
            }
            return Fs.Status.InvalidState;
        }
        internal bool OneRead()
        {
            Fs.OperationToken token = default; Fs.OpenResult file = default;
            if (Fs.BeginOpen(Mount, "/app/wwwroot/index.html"u8, &token) != Fs.Status.Pending || Drive(token) != Fs.Status.Ready ||
                Fs.CopyOpen(Mount, token, &file) != Fs.Status.Success || Fs.BeginRead(Mount, file.File, 511, 2, &token) != Fs.Status.Pending || Drive(token) != Fs.Status.Ready) return false;
            Span<byte> bytes = stackalloc byte[2]; uint count = 0;
            return Fs.CopyRead(Mount, token, bytes, &count) == Fs.Status.Success && count == 2 && bytes[0] == Payload(511) && bytes[1] == Payload(512) && Fs.Close(Mount, file.File) == Fs.Status.Success;
        }
        public void Dispose()
        {
            if (Mount->RequiresCleanup) { _ = Fs.Stop(Mount); }
            if (!Device->Retired) _ = Block.Stop(Device);
            if (Mount->RequiresCleanup) _ = Fs.Stop(Mount);
            NativeMemory.AlignedFree(Arena); NativeMemory.AlignedFree(blockBacking); NativeMemory.Free(Device); NativeMemory.Free(Pool); NativeMemory.Free(dmaSlot); NativeMemory.Free(hardware);
        }
    }
}
