using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using OS.Drivers;
using OS.Kernel.Platform;
using Block = OS.Storage.ContinuingReadOnlyBlockService;
using Fs = OS.Storage.ReadOnlyWebRootFileSystem;
using OS.Storage;

internal static unsafe partial class ReadOnlyWebRootFileSystemTests
{
    [HostTest("webroot.fs immutable-file-chain certificates require exact disjoint pre-IO admission")]
    private static void FileChainCacheAdmission()
    {
        HostCheck.Equal(0U, Fs.ImmutableFileChainCacheBytes(0)); HostCheck.Equal(56U, Fs.ImmutableFileChainCacheBytes(1));
        HostCheck.Equal(112U, Fs.ImmutableFileChainCacheBytes(2)); HostCheck.Equal(0U, Fs.ImmutableFileChainCacheBytes(3));
        HostCheck.Equal(0U, Fs.ImmutableFileChainCacheBytes(uint.MaxValue));
        Type record = typeof(Fs).GetNestedType("FileChainCertificate", System.Reflection.BindingFlags.NonPublic)!;
        HostCheck.Equal(56, Marshal.SizeOf(record)); HostCheck.Equal(32, sizeof(Fs.Metadata));
        foreach (var item in new[] { ("Epoch", 0L), ("Info", 8L), ("Clusters", 40L), ("Count", 48L), ("Phase", 52L) })
            HostCheck.Equal(item.Item2, Marshal.OffsetOf(record, item.Item1).ToInt64());
        using var f = new Fixture(directoryCacheSlots: 5, chainCacheSlots: 2);
        byte[] untouched = new ReadOnlySpan<byte>(f.ChainCacheBacking, 112).ToArray();
        HostCheck.Equal(Fs.Status.InvalidAccess, Fs.EnableImmutableFileChainCache(f.Mount, f.ChainCacheBacking, 112, 2));
        HostCheck.Equal(Fs.Status.Pending, Fs.Start(f.Mount, f.Input));
        HostCheck.Equal(Fs.Status.Success, Fs.EnableImmutableDirectoryCache(f.Mount, f.DirectoryCacheBacking, 2800, 5));
        foreach (ulong address in new ulong[] { (ulong)f.Mount, (ulong)f.Files[0].Clusters, (ulong)f.Operations[1].Storage.Range,
            (ulong)f.Cache[0].Data, (ulong)f.Data, (ulong)f.DirectoryCacheBacking, (ulong)(f.ChainCacheBacking + 1) })
            HostCheck.Equal(Fs.Status.InvalidStorage, Fs.EnableImmutableFileChainCache(f.Mount, (byte*)address, 112, 2));
        HostCheck.Equal(Fs.Status.InvalidAccess, Fs.EnableImmutableFileChainCache(f.Mount, f.ChainCacheBacking, 111, 2));
        HostCheck.Equal(Fs.Status.InvalidAccess, Fs.EnableImmutableFileChainCache(f.Mount, f.ChainCacheBacking, 112, 3));
        HostCheck.True(new ReadOnlySpan<byte>(f.ChainCacheBacking, 112).SequenceEqual(untouched));
        HostCheck.Equal(Fs.Status.Success, Fs.EnableImmutableFileChainCache(f.Mount, f.ChainCacheBacking, 112, 2));
        HostCheck.Equal(0, ChainCertificates(f));
        byte[] enabled = new ReadOnlySpan<byte>(f.ChainCacheBacking, 112).ToArray();
        HostCheck.Equal(Fs.Status.InvalidAccess, Fs.EnableImmutableFileChainCache(f.Mount, f.ChainCacheBacking, 112, 2));
        HostCheck.True(new ReadOnlySpan<byte>(f.ChainCacheBacking, 112).SequenceEqual(enabled));
        using var late = new Fixture(chainCacheSlots: 2); HostCheck.Equal(Fs.Status.Pending, Fs.Start(late.Mount, late.Input));
        HostCheck.Equal(Fs.Status.Pending, Fs.PumpMount(late.Mount));
        HostCheck.Equal(Fs.Status.InvalidAccess, Fs.EnableImmutableFileChainCache(late.Mount, late.ChainCacheBacking, 112, 2));
        ChainCacheCanaries(f);
    }

    [HostTest("webroot.fs successful immutable chains reuse independent certificates after normal lookup and fresh generations")]
    private static void FileChainCacheReuse()
    {
        ulong coldWork = 0, warmWork = 0;
        foreach (uint certificates in new uint[] { 0, 2 })
        {
            using var f = new Fixture(cacheSlots: 1, chainCacheSlots: certificates); LongIndex(f, 200); f.MountReady();
            var a = f.Open("/app/wwwroot/index.html"); Fs.FileInspection facts = default;
            HostCheck.Equal(Fs.Status.Success, Fs.Inspect(f.Mount, a.File, &facts)); HostCheck.True(facts.AllocationKnown && facts.AllocatedBlocks == 200);
            uint[] map = new ReadOnlySpan<uint>(f.Files[0].Clusters, 200).ToArray();
            HostCheck.Equal(Fs.Status.Success, Fs.Close(f.Mount, a.File));
            ulong start = f.Disk.FatReads(); var b = f.Open("/app/wwwroot/index.html");
            ulong work = f.Disk.FatReads() - start; if (certificates == 0) coldWork = work; else warmWork = work;
            HostCheck.True(a.Info.Inode == b.Info.Inode && a.Info.Length == b.Info.Length && !a.File.Equals(b.File));
            HostCheck.True(Field<Fs.FileToken, ulong>(b.File, "Generation") > Field<Fs.FileToken, ulong>(a.File, "Generation"));
            HostCheck.True(new ReadOnlySpan<uint>(f.Files[0].Clusters, 200).SequenceEqual(map));
            Fs.OperationToken stale = default; ulong submitted = f.Device->Submitted;
            HostCheck.Equal(Fs.Status.StaleToken, Fs.BeginRead(f.Mount, a.File, 0, 1, &stale)); HostCheck.Equal(submitted, f.Device->Submitted);
            // A simultaneous open gets a separate map, never shared mutable slot authority.
            var independent = f.Open("/app/wwwroot/index.html");
            HostCheck.True(f.Files[0].Clusters != f.Files[1].Clusters && new ReadOnlySpan<uint>(f.Files[1].Clusters, 200).SequenceEqual(map));
            HostCheck.Equal(Fs.Status.Full, Fs.BeginOpen(f.Mount, "/app/wwwroot/empty.bin"u8, &stale));
            HostCheck.Equal(Fs.Status.Pending, Fs.BeginRead(f.Mount, b.File, 0, 1, &stale));
            HostCheck.Equal(Fs.Status.Busy, Fs.Close(f.Mount, b.File)); HostCheck.Equal(Fs.Status.Cancelled, Fs.Cancel(f.Mount, stale));
            HostCheck.Equal(Fs.Status.Success, Fs.Close(f.Mount, independent.File)); HostCheck.Equal(Fs.Status.Success, Fs.Close(f.Mount, b.File));
            if (certificates != 0) { HostCheck.True(ChainCertificates(f) != 0); ChainCacheCanaries(f); }
        }
        HostCheck.True(coldWork > warmWork); // Work count only, no guest timing claim.
        using var allocation = new Fixture(chainCacheSlots: 2); allocation.MountReady(); HostCheck.True(allocation.OneRead());
        long allocated = MeasureReadAllocations(allocation, out bool good);
        HostCheck.True(good); HostCheck.Equal(0L, allocated);
    }

    [HostTest("webroot.fs chain certificates revoke before map overwrite and cannot derive authority from closed bytes")]
    private static void FileChainCacheReplacement()
    {
        using var f = new Fixture(cacheSlots: 1, chainCacheSlots: 1); LongIndex(f, 200); f.MountReady();
        var a = f.Open("/app/wwwroot/index.html"); HostCheck.Equal(1, ChainCertificates(f));
        HostCheck.Equal(Fs.Status.Success, Fs.Close(f.Mount, a.File));
        var far = f.Open("/app/wwwroot/far.bin"); HostCheck.Equal(200U, f.Files[0].Clusters[0]);
        HostCheck.Equal(Fs.Status.Success, Fs.Close(f.Mount, far.File));
        var token = ChainLookup(f); var o = OperationFor(f, token);
        HostCheck.Equal(Fs.Status.Pending, Fs.Poll(f.Mount, token));
        HostCheck.True(ClusterCount(f, o) is > 0 and < 200); HostCheck.Equal(10U, f.Files[0].Clusters[0]); HostCheck.Equal(0, ChainCertificates(f));
        var cancellation = Fs.Cancel(f.Mount, token);
        if (cancellation == Fs.Status.Pending) HostCheck.Equal(Fs.Status.Cancelled, f.Drive(token));
        else HostCheck.Equal(Fs.Status.Cancelled, cancellation);
        HostCheck.Equal(0, ChainCertificates(f));
        HostCheck.Equal(Fs.Status.StaleToken, Fs.Poll(f.Mount, token));
        a = f.Open("/app/wwwroot/index.html"); HostCheck.Equal(1, ChainCertificates(f));
        HostCheck.Equal(Fs.Status.Success, Fs.Close(f.Mount, a.File));
        // A cancelled ready hit is not a new successful publication.
        token = ChainLookup(f); HostCheck.Equal(Fs.Status.Ready, Fs.Poll(f.Mount, token));
        HostCheck.Equal(Fs.Status.Cancelled, Fs.Cancel(f.Mount, token)); HostCheck.Equal(0, ChainCertificates(f));
        ChainCacheCanaries(f);
    }

    [HostTest("webroot.fs incomplete failed and cancelled chain validations never publish certificates")]
    private static void FileChainCacheColdFailures()
    {
        foreach (var item in new[] { (10U, Fs.Status.ChainLoop), (0x0fffffffU, Fs.Status.Truncated),
            (0x0ffffff7U, Fs.Status.BadCluster), (0U, Fs.Status.FreeCluster), (1U, Fs.Status.ReservedCluster), (65530U, Fs.Status.InvalidCluster) })
        {
            using var f = new Fixture(chainCacheSlots: 2); f.Disk.SetFat(10, item.Item1); f.MountReady();
            var token = BeginLookup(f, "/app/wwwroot/index.html"); HostCheck.Equal(item.Item2, f.Drive(token));
            HostCheck.Equal(0, ChainCertificates(f)); HostCheck.Equal(Fs.Status.Cancelled, Fs.Cancel(f.Mount, token)); ChainCacheCanaries(f);
        }
        foreach (bool mirror in new[] { false, true })
        {
            using var f = new Fixture(chainCacheSlots: 2);
            if (mirror) f.Disk.SetFat(10, 13, mirrorOnly: true); else f.Disk.SetFat(12, 14);
            f.MountReady(); var token = BeginLookup(f, "/app/wwwroot/index.html");
            HostCheck.Equal(mirror ? Fs.Status.MirrorMismatch : Fs.Status.Overlong, f.Drive(token)); HostCheck.Equal(0, ChainCertificates(f));
            HostCheck.Equal(Fs.Status.Cancelled, Fs.Cancel(f.Mount, token));
        }
        using var cancelled = new Fixture(cacheSlots: 1, chainCacheSlots: 2); LongIndex(cancelled, 200); cancelled.MountReady();
        var pending = ChainLookup(cancelled); HostCheck.Equal(Fs.Status.Pending, Fs.Poll(cancelled.Mount, pending));
        for (int i = 0; i < 1000 && !cancelled.Device->ReadPending; i++) HostCheck.Equal(Fs.Status.Pending, Fs.Poll(cancelled.Mount, pending));
        HostCheck.True(cancelled.Device->ReadPending); HostCheck.Equal(0, ChainCertificates(cancelled));
        HostCheck.Equal(Fs.Status.Pending, Fs.Cancel(cancelled.Mount, pending)); HostCheck.Equal(Fs.Status.Cancelled, cancelled.Drive(pending));
        HostCheck.Equal(0, ChainCertificates(cancelled)); HostCheck.Equal(Fs.Status.StaleToken, Fs.Poll(cancelled.Mount, pending));
        using var unpublished = new Fixture(chainCacheSlots: 2); unpublished.MountReady();
        pending = BeginLookup(unpublished, "/app/wwwroot/index.html"); HostCheck.Equal(Fs.Status.Ready, unpublished.Drive(pending));
        HostCheck.Equal(0, ChainCertificates(unpublished)); HostCheck.Equal(Fs.Status.Cancelled, Fs.Cancel(unpublished.Mount, pending));
        HostCheck.Equal(0, ChainCertificates(unpublished));
    }

    [HostTest("webroot.fs file-chain hits retain capacity path corruption and lookup error precedence")]
    private static void FileChainCacheOrdering()
    {
        using (var f = new Fixture(chainCacheSlots: 2))
        {
            f.Files[1].ClusterCapacity = 1; f.MountReady(); var a = f.Open("/app/wwwroot/index.html");
            var token = BeginLookup(f, "/app/wwwroot/index.html"); HostCheck.Equal(Fs.Status.TraversalBudgetExceeded, f.Drive(token));
            HostCheck.Equal(0U, Field<Fs.FileHandle, uint>(f.Files[1], "ClusterCount")); HostCheck.Equal(1, ChainCertificates(f));
            HostCheck.Equal(Fs.Status.Cancelled, Fs.Cancel(f.Mount, token));
            token = BeginLookup(f, "/app/wwwroot/index.html/child"); HostCheck.Equal(Fs.Status.NotDirectory, f.Drive(token));
            HostCheck.Equal(Fs.Status.Cancelled, Fs.Cancel(f.Mount, token));
            token = BeginLookup(f, "/app/wwwroot/missing.bin"); HostCheck.Equal(Fs.Status.NotFound, f.Drive(token));
            HostCheck.Equal(Fs.Status.Cancelled, Fs.Cancel(f.Mount, token)); HostCheck.Equal(Fs.Status.Success, Fs.Close(f.Mount, a.File));
        }
        foreach (int mode in new[] { 0, 1, 2 })
        {
            using var f = new Fixture(chainCacheSlots: 2, collision: mode == 1, workBudget: mode == 2 ? 7U : 65536U);
            if (mode == 0) f.Disk.Sector(f.Disk.Data(4))[13] ^= 1;
            f.MountReady(); var token = BeginLookup(f, "/app/wwwroot/index.html");
            HostCheck.Equal(mode == 0 ? Fs.Status.CorruptDirectory : mode == 1 ? Fs.Status.Ambiguous : Fs.Status.TraversalBudgetExceeded, f.Drive(token));
            HostCheck.Equal(0, ChainCertificates(f)); HostCheck.Equal(Fs.Status.Cancelled, Fs.Cancel(f.Mount, token));
        }
    }

    [HostTest("webroot.fs chain certificates preserve fresh data failure output exclusions and stop lifetime")]
    private static void FileChainCacheLifetime()
    {
        using (var f = new Fixture(chainCacheSlots: 2))
        {
            f.MountReady(); var a = f.Open("/app/wwwroot/index.html"); HostCheck.Equal(1, ChainCertificates(f));
            HostCheck.Equal(Fs.Status.Success, Fs.Close(f.Mount, a.File)); a = f.Open("/app/wwwroot/index.html");
            Fs.OperationToken token = default;
            foreach (uint offset in new uint[] { 0, 104 })
                HostCheck.Equal(Fs.Status.InvalidStorage, Fs.BeginOpen(f.Mount, "/app/wwwroot/index.html"u8, (Fs.OperationToken*)(f.ChainCacheBacking + offset)));
            HostCheck.Equal(Fs.Status.Pending, Fs.BeginRead(f.Mount, a.File, 0, 700, &token)); HostCheck.Equal(Fs.Status.Ready, f.Drive(token));
            uint bytes = 77; HostCheck.Equal(Fs.Status.InvalidStorage, Fs.CopyRead(f.Mount, token, new Span<byte>(f.ChainCacheBacking, 700), &bytes));
            byte[] output = new byte[700]; HostCheck.Equal(Fs.Status.Success, Fs.CopyRead(f.Mount, token, output, &bytes));
            for (int i = 0; i < output.Length; i++) HostCheck.Equal(Payload(i), output[i]);
            // Metadata reuse never suppresses a later real file-data failure.
            f.Disk.FailLba = f.Disk.Data(12); output.AsSpan().Fill(0xa5); bytes = 77;
            HostCheck.Equal(Fs.Status.Pending, Fs.BeginRead(f.Mount, a.File, 0, 700, &token)); HostCheck.Equal(Fs.Status.DeviceFailed, f.Drive(token));
            HostCheck.Equal(0, ChainCertificates(f)); HostCheck.Equal(Fs.Status.InvalidState, Fs.CopyRead(f.Mount, token, output, &bytes));
            HostCheck.True(bytes == 77 && output.All(v => v == 0xa5)); ChainCacheCanaries(f);
        }
        byte[] retiredCertificate;
        using (var f = new Fixture(chainCacheSlots: 2))
        {
            f.MountReady(); var a = f.Open("/app/wwwroot/index.html"); HostCheck.Equal(Fs.Status.Success, Fs.Close(f.Mount, a.File));
            HostCheck.Equal(1, ChainCertificates(f)); retiredCertificate = new ReadOnlySpan<byte>(f.ChainCacheBacking, 112).ToArray();
            HostCheck.Equal(Fs.Status.Success, Fs.Stop(f.Mount)); HostCheck.Equal(0, ChainCertificates(f));
            Fs.OperationToken token = default; HostCheck.Equal(Fs.Status.InvalidState, Fs.BeginOpen(f.Mount, "/app/wwwroot/index.html"u8, &token));
            HostCheck.Equal(Fs.Status.InvalidAccess, Fs.Start(f.Mount, f.Input)); ChainCacheCanaries(f);
        }
        using var fresh = new Fixture(chainCacheSlots: 2); retiredCertificate.CopyTo(new Span<byte>(fresh.ChainCacheBacking, 112));
        HostCheck.Equal(1, ChainCertificates(fresh)); fresh.Disk.SetFat(10, 0x0fffffff); fresh.MountReady();
        HostCheck.Equal(0, ChainCertificates(fresh)); var cold = BeginLookup(fresh, "/app/wwwroot/index.html");
        HostCheck.Equal(Fs.Status.Truncated, fresh.Drive(cold)); HostCheck.Equal(0, ChainCertificates(fresh));
        HostCheck.Equal(Fs.Status.Cancelled, Fs.Cancel(fresh.Mount, cold));
    }

    private static int ChainCertificates(Fixture f)
    {
        int count = 0; for (uint i = 0; i < f.ChainCacheSlots; i++) if (*(uint*)(f.ChainCacheBacking + i * 56 + 52) == 2) count++;
        return count;
    }
    private static void ChainCacheCanaries(Fixture f)
    {
        HostCheck.True(new ReadOnlySpan<byte>(f.ChainCacheBacking - 16, 16).IndexOfAnyExcept((byte)0xc7) < 0);
        HostCheck.True(new ReadOnlySpan<byte>(f.ChainCacheBacking + Fs.ImmutableFileChainCacheBytes(f.ChainCacheSlots), 16).IndexOfAnyExcept((byte)0xc7) < 0);
    }

    [HostTest("webroot.fs immutable-directory-cache admission is explicit disjoint and before all mount IO")]
    private static void ImmutableCacheAdmission()
    {
        HostCheck.Equal(0U, Fs.ImmutableDirectoryCacheBytes(0)); HostCheck.Equal(560U, Fs.ImmutableDirectoryCacheBytes(1));
        HostCheck.Equal(2800U, Fs.ImmutableDirectoryCacheBytes(5)); HostCheck.Equal(0U, Fs.ImmutableDirectoryCacheBytes(6));
        HostCheck.Equal(0U, Fs.ImmutableDirectoryCacheBytes(uint.MaxValue));
        Type record = typeof(Fs).GetNestedType("ImmutableDirectoryRecord", System.Reflection.BindingFlags.NonPublic)!;
        HostCheck.Equal(48, Marshal.SizeOf(record));
        var fields = record.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        HostCheck.Equal(6, fields.Length);
        foreach (var field in fields) HostCheck.True(field.FieldType == typeof(ulong));
        foreach (var item in new[] { ("MountOwner", 0L), ("MountEpoch", 8L), ("DeviceEpoch", 16L), ("PoolEpoch", 24L), ("Lba", 32L), ("Phase", 40L) })
            HostCheck.Equal(item.Item2, Marshal.OffsetOf(record, item.Item1).ToInt64());
        using var f = new Fixture(directoryCacheSlots: 5);
        byte[] untouched = new ReadOnlySpan<byte>(f.DirectoryCacheBacking, 2800).ToArray();
        HostCheck.Equal(Fs.Status.InvalidAccess, Fs.EnableImmutableDirectoryCache(f.Mount, f.DirectoryCacheBacking, 2800, 5));
        HostCheck.Equal(Fs.Status.Pending, Fs.Start(f.Mount, f.Input));
        foreach (ulong address in new ulong[] { (ulong)f.Operations[0].Storage.Sector, (ulong)f.Operations[1].Storage.Range,
            (ulong)f.Mount, (ulong)f.Cache[0].Data, (ulong)f.Data, (ulong)(f.DirectoryCacheBacking + 1) })
            HostCheck.Equal(Fs.Status.InvalidStorage, Fs.EnableImmutableDirectoryCache(f.Mount, (byte*)address, 2800, 5));
        HostCheck.Equal(Fs.Status.InvalidAccess, Fs.EnableImmutableDirectoryCache(f.Mount, f.DirectoryCacheBacking, 2799, 5));
        HostCheck.Equal(Fs.Status.InvalidAccess, Fs.EnableImmutableDirectoryCache(f.Mount, f.DirectoryCacheBacking, 2800, 6));
        HostCheck.True(new ReadOnlySpan<byte>(f.DirectoryCacheBacking, 2800).SequenceEqual(untouched));
        HostCheck.Equal(0UL, f.Mount->SectorReads);
        HostCheck.Equal(Fs.Status.Success, Fs.EnableImmutableDirectoryCache(f.Mount, f.DirectoryCacheBacking, 2800, 5));
        byte[] enabled = new ReadOnlySpan<byte>(f.DirectoryCacheBacking, 2800).ToArray();
        HostCheck.Equal(Fs.Status.InvalidAccess, Fs.EnableImmutableDirectoryCache(f.Mount, f.DirectoryCacheBacking, 2800, 5));
        HostCheck.True(new ReadOnlySpan<byte>(f.DirectoryCacheBacking, 2800).SequenceEqual(enabled));
        using var late = new Fixture(directoryCacheSlots: 5);
        HostCheck.Equal(Fs.Status.Pending, Fs.Start(late.Mount, late.Input));
        HostCheck.Equal(Fs.Status.Pending, Fs.PumpMount(late.Mount));
        HostCheck.Equal(Fs.Status.InvalidAccess, Fs.EnableImmutableDirectoryCache(late.Mount, late.DirectoryCacheBacking, 2800, 5));
    }

    [HostTest("webroot.fs immutable-directory-cache protected hits preserve parsing and never evict or prefetch")]
    private static void ImmutableCacheReuse()
    {
        using var f = new Fixture(directoryCacheSlots: 5);
        f.Disk.NoDirectoryEnd(); f.Disk.SetFat(8, 20); f.Disk.SetFat(20, 0x0fffffff); f.Disk.Sector(f.Disk.Data(20)).AsSpan().Clear();
        f.MountReady(); HostCheck.Equal(0, DirectoryCachePhases(f, 2)); // MountKind never fills.
        var cold = CacheOpen(f, "/app/wwwroot/index.html", out uint coldWork);
        HostCheck.Equal(Fs.Status.Success, Fs.Close(f.Mount, cold.File)); HostCheck.Equal(5, DirectoryCachePhases(f, 2));
        ulong reads = f.Mount->SectorReads, submitted = f.Device->Submitted, completed = f.Device->Completed, copied = f.Device->Copied;
        var warm = CacheOpen(f, "/app/wwwroot/index.html", out uint warmWork);
        HostCheck.Equal(coldWork, warmWork); HostCheck.True(coldWork != 0 && cold.Info.Inode == warm.Info.Inode && cold.Info.Length == warm.Info.Length);
        HostCheck.Equal(reads, f.Mount->SectorReads); NoIo(f, submitted, completed, copied);
        HostCheck.Equal(Fs.Status.Success, Fs.Close(f.Mount, warm.File));
        byte[] protectedBytes = new ReadOnlySpan<byte>(f.DirectoryCacheBacking, 2800).ToArray();
        uint outside = f.Disk.Reads[f.Disk.Data(5)];
        for (int i = 0; i < 2; i++)
        {
            var nested = f.Open("/app/wwwroot/fixture/info.txt"); HostCheck.Equal(Fs.Status.Success, Fs.Close(f.Mount, nested.File));
        }
        HostCheck.Equal(outside + 2, f.Disk.Reads[f.Disk.Data(5)]); // Sixth distinct directory stays a physical miss.
        HostCheck.True(new ReadOnlySpan<byte>(f.DirectoryCacheBacking, 2800).SequenceEqual(protectedBytes));
        HostCheck.True(new ReadOnlySpan<byte>(f.DirectoryCacheBacking - 16, 16).IndexOfAnyExcept((byte)0xc7) < 0);
        HostCheck.True(new ReadOnlySpan<byte>(f.DirectoryCacheBacking + 2800, 16).IndexOfAnyExcept((byte)0xc7) < 0);
        HostCheck.Equal(Fs.Status.Success, Fs.Stop(f.Mount)); HostCheck.Equal(0, DirectoryCachePhases(f, 2));
    }

    [HostTest("webroot.fs immutable-directory-cache publishes only after accepted physical copy")]
    private static void ImmutableCacheCopyPublication()
    {
        for (int rejected = 0; rejected < 2; rejected++)
        {
            FsObservation sink = default;
            using var f = new Fixture(directoryCacheSlots: 5, observation: new() { Context = &sink, Observe = &ObserveSector });
            sink.Device = f.Device; new Span<byte>(f.DirectoryCacheBacking, 2800).Fill(0xa5); f.MountReady();
            var token = BeginLookup(f, "/app/wwwroot/index.html"); HostCheck.Equal(Fs.Status.Pending, Fs.Poll(f.Mount, token));
            HostCheck.Equal(0, DirectoryCachePhases(f, 1)); HostCheck.Equal(5, DirectoryCachePhases(f, 0)); HostCheck.Equal(0, DirectoryCachePhases(f, 2));
            sink.CacheBacking = f.DirectoryCacheBacking; sink.CacheSlots = 5; sink.CheckDirectoryCache = true; sink.Reject = rejected != 0;
            ulong before = f.Device->Copied; f.Tick(); Fs.Status status = Fs.Poll(f.Mount, token);
            HostCheck.True(sink.CheckedDirectoryCache && !sink.Bad);
            if (rejected == 0)
            {
                HostCheck.Equal(Fs.Status.Pending, status); HostCheck.Equal(before + 1, f.Device->Copied);
                HostCheck.Equal(1, DirectoryCachePhases(f, 2)); HostCheck.Equal(0, DirectoryCachePhases(f, 1));
                HostCheck.Equal(sink.Candidate.Sector, *(ulong*)(f.DirectoryCacheBacking + 32));
                HostCheck.True(new ReadOnlySpan<byte>(f.DirectoryCacheBacking + 5 * 48, 512).SequenceEqual(f.Disk.Sector(f.Disk.Data(4))));
                HostCheck.Equal(Fs.Status.Cancelled, Fs.Cancel(f.Mount, token));
            }
            else
            {
                HostCheck.Equal(Fs.Status.DeviceFailed, status); HostCheck.Equal(before, f.Device->Copied);
                HostCheck.Equal(0, DirectoryCachePhases(f, 1)); HostCheck.Equal(0, DirectoryCachePhases(f, 2));
                HostCheck.True(new ReadOnlySpan<byte>(f.DirectoryCacheBacking + 5 * 48, 5 * 512).IndexOfAnyExcept((byte)0xa5) < 0);
            }
        }
    }

    [HostTest("webroot.fs immutable-directory-cache replays VFAT collisions and budgets through original parser")]
    private static void ImmutableCacheParserErrors()
    {
        for (int mode = 0; mode < 3; mode++)
        {
            using var f = new Fixture(directoryCacheSlots: 5, collision: mode == 1, workBudget: mode == 2 ? 7U : 65536U);
            if (mode == 0) f.Disk.Sector(f.Disk.Data(4))[13] ^= 1;
            f.MountReady(); uint observed = 0;
            for (int repeat = 0; repeat < 2; repeat++)
            {
                var token = BeginLookup(f, "/app/wwwroot/index.html");
                HostCheck.Equal(mode == 0 ? Fs.Status.CorruptDirectory : mode == 1 ? Fs.Status.Ambiguous : Fs.Status.TraversalBudgetExceeded, f.Drive(token));
                HostCheck.Equal(Fs.Status.Cancelled, Fs.Cancel(f.Mount, token));
                if (repeat == 0) observed = f.Disk.Reads[f.Disk.Data(4)];
                else HostCheck.Equal(observed, f.Disk.Reads[f.Disk.Data(4)]);
            }
        }
    }

    [HostTest("webroot.fs immutable-directory-cache preserves End before a failing next sector and required cold failures")]
    private static void ImmutableCacheEndBoundary()
    {
        for (int removeEnd = 0; removeEnd < 2; removeEnd++)
        {
            using var f = new Fixture(directoryCacheSlots: 5);
            f.Disk.SetFat(8, 20); f.Disk.SetFat(20, 0x0fffffff); f.Disk.FailLba = f.Disk.Data(20);
            if (removeEnd != 0) f.Disk.NoDirectoryEnd();
            f.MountReady();
            if (removeEnd == 0)
            {
                for (int repeat = 0; repeat < 2; repeat++)
                {
                    var file = f.Open("/app/wwwroot/index.html"); HostCheck.Equal(Fs.Status.Success, Fs.Close(f.Mount, file.File));
                }
                HostCheck.Equal(0U, f.Disk.Reads[f.Disk.Data(20)]);
            }
            else
            {
                var token = BeginLookup(f, "/app/wwwroot/index.html"); HostCheck.Equal(Fs.Status.DeviceFailed, f.Drive(token));
                HostCheck.Equal(1U, f.Disk.Reads[f.Disk.Data(20)]); HostCheck.Equal(0, DirectoryCachePhases(f, 2));
            }
        }
    }

    [HostTest("webroot.fs immutable-directory-cache cold faults cancellation and stale tokens never publish")]
    private static void ImmutableCacheCancellation()
    {
        using (var f = new Fixture(directoryCacheSlots: 5))
        {
            new Span<byte>(f.DirectoryCacheBacking, 2800).Fill(0xa5); f.MountReady();
            var token = BeginLookup(f, "/app/wwwroot/index.html"); var other = BeginLookup(f, "/app/wwwroot/empty.bin");
            HostCheck.Equal(Fs.Status.Pending, Fs.Poll(f.Mount, token)); HostCheck.Equal(0, DirectoryCachePhases(f, 1)); HostCheck.Equal(5, DirectoryCachePhases(f, 0));
            HostCheck.Equal(Fs.Status.Cancelled, Fs.Cancel(f.Mount, other)); HostCheck.Equal(0, DirectoryCachePhases(f, 1)); HostCheck.Equal(5, DirectoryCachePhases(f, 0));
            HostCheck.Equal(Fs.Status.Pending, Fs.Cancel(f.Mount, token)); HostCheck.Equal(Fs.Status.Cancelled, f.Drive(token));
            HostCheck.Equal(1UL, f.Device->Cancelled);
            HostCheck.Equal(0, DirectoryCachePhases(f, 1)); HostCheck.Equal(0, DirectoryCachePhases(f, 2));
            HostCheck.True(new ReadOnlySpan<byte>(f.DirectoryCacheBacking + 5 * 48, 5 * 512).IndexOfAnyExcept((byte)0xa5) < 0);
            ulong submitted = f.Device->Submitted, completed = f.Device->Completed, copied = f.Device->Copied, cancelled = f.Device->Cancelled;
            HostCheck.Equal(Fs.Status.StaleToken, Fs.Poll(f.Mount, token)); NoIo(f, submitted, completed, copied, cancelled);
        }
        using (var f = new Fixture(directoryCacheSlots: 5))
        {
            f.MountReady(); var warm = f.Open("/app/wwwroot/index.html"); HostCheck.Equal(Fs.Status.Success, Fs.Close(f.Mount, warm.File));
            HostCheck.True(DirectoryCachePhases(f, 2) != 0); f.Disk.FailLba = f.Disk.Data(5);
            var token = BeginLookup(f, "/app/wwwroot/fixture/info.txt"); HostCheck.Equal(Fs.Status.DeviceFailed, f.Drive(token));
            HostCheck.Equal(0, DirectoryCachePhases(f, 1)); HostCheck.Equal(0, DirectoryCachePhases(f, 2));
        }
    }

    [HostTest("webroot.fs immutable-directory-cache backing is excluded from outputs and cleared for a new mount")]
    private static void ImmutableCacheOwnership()
    {
        byte* backing = (byte*)NativeMemory.AlignedAlloc(2800, 16);
        try
        {
            using (var f = new Fixture())
            {
                MountWithDirectoryCache(f, backing); var token = BeginLookup(f, "/app/wwwroot/index.html");
                HostCheck.Equal(Fs.Status.Ready, f.Drive(token)); byte[] ready = new ReadOnlySpan<byte>(backing, 2800).ToArray();
                HostCheck.Equal(Fs.Status.InvalidStorage, Fs.CopyOpen(f.Mount, token, (Fs.OpenResult*)backing));
                HostCheck.True(new ReadOnlySpan<byte>(backing, 2800).SequenceEqual(ready));
                Fs.OpenResult file = default; HostCheck.Equal(Fs.Status.Success, Fs.CopyOpen(f.Mount, token, &file));
                HostCheck.Equal(Fs.Status.Pending, Fs.BeginRead(f.Mount, file.File, 0, 1, &token)); HostCheck.Equal(Fs.Status.Ready, f.Drive(token));
                uint bytes = 99; HostCheck.Equal(Fs.Status.InvalidStorage, Fs.CopyRead(f.Mount, token, new Span<byte>(backing + 2799, 1), &bytes));
                HostCheck.Equal(99U, bytes); HostCheck.True(new ReadOnlySpan<byte>(backing, 2800).SequenceEqual(ready));
                HostCheck.Equal(Fs.Status.Cancelled, Fs.Cancel(f.Mount, token)); HostCheck.Equal(Fs.Status.Success, Fs.Close(f.Mount, file.File));
                HostCheck.Equal(Fs.Status.Success, Fs.Stop(f.Mount));
                ready.AsSpan().CopyTo(new Span<byte>(backing, 2800)); // Returned backing may contain an old ready generation.
            }
            using (var fresh = new Fixture())
            {
                MountWithDirectoryCache(fresh, backing); fresh.Disk.FailLba = fresh.Disk.Data(4);
                var token = BeginLookup(fresh, "/app/wwwroot/index.html");
                HostCheck.Equal(Fs.Status.DeviceFailed, fresh.Drive(token)); // Old mount payload cannot bypass a new cold request.
            }
        }
        finally { NativeMemory.AlignedFree(backing); }
    }

    [HostTest("webroot.fs immutable-directory-cache rejects the actual borrowed writable backend")]
    private static void ImmutableCacheBorrowed() => CombinedWebDataImageFixtureTests.WithImage((_, _, _, _, path, receipt) =>
    {
        using var f = new ReadOnlyWebRootWritableBlockTests.Fixture(System.IO.File.ReadAllBytes(path), receipt);
        byte* backing = (byte*)NativeMemory.AlignedAlloc(2800, 16);
        try
        {
            new Span<byte>(backing, 2800).Fill(0xa5);
            byte[] mount = new ReadOnlySpan<byte>(f.Mount, sizeof(Fs.Mount)).ToArray();
            HostCheck.Equal(Fs.Status.InvalidAccess, Fs.EnableImmutableDirectoryCache(f.Mount, backing, 2800, 5));
            HostCheck.Equal(Fs.Status.InvalidAccess, Fs.EnableImmutableFileChainCache(f.Mount, backing, 112, 2));
            HostCheck.True(new ReadOnlySpan<byte>(f.Mount, sizeof(Fs.Mount)).SequenceEqual(mount));
            HostCheck.True(new ReadOnlySpan<byte>(backing, 2800).IndexOfAnyExcept((byte)0xa5) < 0);
        }
        finally { NativeMemory.AlignedFree(backing); }
    });

    private static int DirectoryCachePhases(Fixture f, ulong phase)
    {
        int count = 0;
        for (uint i = 0; i < f.DirectoryCacheSlots; i++) if (*(ulong*)(f.DirectoryCacheBacking + i * 48 + 40) == phase) count++;
        return count;
    }

    private static Fs.OpenResult CacheOpen(Fixture f, string path, out uint work)
    {
        var token = BeginLookup(f, path); HostCheck.Equal(Fs.Status.Ready, f.Drive(token));
        work = Field<Fs.Operation, uint>(*OperationFor(f, token), "Work");
        Fs.OpenResult result = default; HostCheck.Equal(Fs.Status.Success, Fs.CopyOpen(f.Mount, token, &result)); return result;
    }

    private static void MountWithDirectoryCache(Fixture f, byte* backing)
    {
        HostCheck.Equal(Fs.Status.Pending, Fs.Start(f.Mount, f.Input));
        HostCheck.Equal(Fs.Status.Success, Fs.EnableImmutableDirectoryCache(f.Mount, backing, 2800, 5));
        Fs.Status status = Fs.Status.Pending;
        for (int i = 0; i < 10000 && status == Fs.Status.Pending; i++) { f.Tick(); status = Fs.PumpMount(f.Mount); }
        HostCheck.Equal(Fs.Status.Ready, status);
    }

    [HostTest("webroot.fs internal lookup selects at most16 loaded physical entries without IO or another operation progress")]
    private static void LoadedLookupBound()
    {
        using var f = new Fixture(); f.MountReady();
        var a = BeginLookup(f, "/app/wwwroot/missing-a.bin"); var b = BeginLookup(f, "/app/wwwroot/missing-b.bin");
        LoadedLookup(f, a); LoadedLookup(f, b); var left = OperationFor(f, a); var right = OperationFor(f, b);
        ulong submitted = f.Device->Submitted, completed = f.Device->Completed, copied = f.Device->Copied;
        HostCheck.Equal(16U, Fs.MaximumLoadedDirectorySteps); HostCheck.Equal(0U, Field<Fs.Operation, uint>(*left, "Work"));
        HostCheck.Equal(Fs.Status.Pending, Fs.Poll(f.Mount, a));
        HostCheck.True(Field<Fs.Operation, uint>(*left, "Work") == 16 && Cursor(left).EntryInSectorForTests() == 16);
        HostCheck.True(Field<Fs.Operation, uint>(*right, "Work") == 0 && Cursor(right).EntryInSectorForTests() == 0);
        NoIo(f, submitted, completed, copied);
        HostCheck.Equal(Fs.Status.Pending, Fs.Poll(f.Mount, b));
        HostCheck.True(Field<Fs.Operation, uint>(*right, "Work") == 16 && Field<Fs.Operation, uint>(*left, "Work") == 16);
        NoIo(f, submitted, completed, copied);
        HostCheck.Equal(Fs.Status.Cancelled, Fs.Cancel(f.Mount, a)); HostCheck.Equal(Fs.Status.Cancelled, Fs.Cancel(f.Mount, b));
        a = BeginLookup(f, "/app/wwwroot/missing-a.bin"); b = BeginLookup(f, "/app/wwwroot/missing-b.bin");
        LoadedLookup(f, a); LoadedLookup(f, b); right = OperationFor(f, b);
        HostCheck.Equal(Fs.Status.Pending, Fs.Poll(f.Mount, a)); HostCheck.Equal(Fs.Status.Pending, Fs.Poll(f.Mount, a));
        HostCheck.True(f.Device->ReadPending); submitted = f.Device->Submitted; completed = f.Device->Completed; copied = f.Device->Copied;
        HostCheck.Equal(Fs.Status.Pending, Fs.Poll(f.Mount, b));
        HostCheck.True(Field<Fs.Operation, uint>(*right, "Work") == 1 && Cursor(right).EntryInSectorForTests() == 1);
        NoIo(f, submitted, completed, copied); // Another IO owner preserves the old single-step behavior.
        HostCheck.Equal(Fs.Status.Cancelled, Fs.Cancel(f.Mount, b)); HostCheck.Equal(Fs.Status.Pending, Fs.Cancel(f.Mount, a));
        HostCheck.Equal(Fs.Status.Cancelled, f.Drive(a));
    }

    [HostTest("webroot.fs mount lookup batches a loaded sector but stops at component and IO boundaries")]
    private static void MountLookupBound()
    {
        using var f = new Fixture(); byte[] app = f.Disk.Sector(f.Disk.Data(2)).AsSpan(0, 64).ToArray();
        byte[] first = f.Disk.Sector(f.Disk.Data(2)); first.AsSpan().Clear();
        for (int i = 0; i < 16; i++) first[i * 32] = 0xe5;
        app.CopyTo(f.Disk.Sector(f.Disk.Data(20)), 0); f.Disk.SetFat(2, 20); f.Disk.SetFat(20, 0x0fffffff);
        HostCheck.Equal(Fs.Status.Pending, Fs.Start(f.Mount, f.Input)); var operation = f.Operations;
        MountLoaded(2); ulong submitted = f.Device->Submitted, completed = f.Device->Completed, copied = f.Device->Copied;
        HostCheck.Equal(Fs.Status.Pending, Fs.PumpMount(f.Mount));
        HostCheck.True(Field<Fs.Operation, uint>(*operation, "Work") == 16 && Cursor(operation).EntryInSectorForTests() == 16);
        NoIo(f, submitted, completed, copied);
        MountLoaded(20); submitted = f.Device->Submitted; completed = f.Device->Completed; copied = f.Device->Copied;
        uint work = Field<Fs.Operation, uint>(*operation, "Work"), component = Field<Fs.Operation, uint>(*operation, "Component");
        HostCheck.Equal(Fs.Status.Pending, Fs.PumpMount(f.Mount));
        HostCheck.True(Field<Fs.Operation, uint>(*operation, "Work") == work + 3 && Field<Fs.Operation, uint>(*operation, "Component") != component &&
            Field<Fs.DirectoryCursor, uint>(Cursor(operation), "Cluster") == 3 && !Field<Fs.DirectoryCursor, bool>(Cursor(operation), "SectorReady"));
        NoIo(f, submitted, completed, copied);
        Fs.Status status = Fs.Status.Pending;
        for (int i = 0; i < 1000 && status == Fs.Status.Pending; i++) { f.Tick(); status = Fs.PumpMount(f.Mount); }
        HostCheck.Equal(Fs.Status.Ready, status); HostCheck.True(f.Mount->Ready);
        void MountLoaded(uint cluster)
        {
            for (int i = 0; i < 1000; i++)
            {
                var cursor = Cursor(operation);
                if (Field<Fs.DirectoryCursor, uint>(cursor, "Cluster") == cluster && Loaded(operation)) return;
                f.Tick(); HostCheck.Equal(Fs.Status.Pending, Fs.PumpMount(f.Mount));
            }
            throw new InvalidOperationException("mount lookup did not reach loaded sector");
        }
    }

    [HostTest("webroot.fs lookup preserves split LFN across loaded sector boundary and never combines new IO with completion")]
    private static void LookupSplitName()
    {
        using var f = new Fixture(); f.MountReady(); var token = BeginLookup(f, "/app/wwwroot/" + Disk.LongName); LoadedLookup(f, token);
        var operation = OperationFor(f, token); ulong submitted = f.Device->Submitted, completed = f.Device->Completed, copied = f.Device->Copied;
        HostCheck.Equal(Fs.Status.Pending, Fs.Poll(f.Mount, token));
        var cursor = Cursor(operation); var vfat = Field<Fs.DirectoryCursor, LiveVfatState>(cursor, "Vfat");
        HostCheck.True(cursor.EntryInSectorForTests() == 16 && vfat.Slots == (Disk.LongName.Length + 12) / 13 && vfat.Remaining == vfat.Slots - 1);
        NoIo(f, submitted, completed, copied); uint work = Field<Fs.Operation, uint>(*operation, "Work");
        HostCheck.Equal(Fs.Status.Pending, Fs.Poll(f.Mount, token)); HostCheck.Equal(work, Field<Fs.Operation, uint>(*operation, "Work"));
        Fs.Status status = Fs.Status.Pending;
        for (int i = 0; i < 4096 && status is Fs.Status.Pending or Fs.Status.Busy; i++)
        {
            submitted = f.Device->Submitted; completed = f.Device->Completed; copied = f.Device->Copied;
            f.Tick(); status = Fs.Poll(f.Mount, token);
            HostCheck.True(f.Device->Submitted - submitted <= 1 && f.Device->Completed - completed <= 1 && f.Device->Copied - copied <= 1);
            if (f.Device->Completed != completed) HostCheck.Equal(submitted, f.Device->Submitted);
        }
        HostCheck.Equal(Fs.Status.Ready, status); Fs.OpenResult result = default;
        HostCheck.Equal(Fs.Status.Success, Fs.CopyOpen(f.Mount, token, &result)); HostCheck.Equal(3U, result.Info.Length);
        HostCheck.Equal(1U, f.Disk.Reads[f.Disk.Data(6)]); HostCheck.Equal(0U, f.Disk.Reads[f.Disk.Data(13)]);
        Span<byte> output = stackalloc byte[3]; f.Read(result.File, 0, 3, output, 3); HostCheck.True(output.SequenceEqual("ABC"u8));
        HostCheck.Equal(Fs.Status.Success, Fs.Close(f.Mount, result.File));
    }

    [HostTest("webroot.fs batched lookup rejects late exact case and short alias collisions after complete directory validation")]
    private static void LookupLateCollisions()
    {
        Span<byte> entry = stackalloc byte[32];
        foreach (int variant in new[] { 0, 1, 2 })
        {
            using var f = new Fixture(); string name = variant == 0 ? "far.bin" : variant == 1 ? "FAR.BIN" : "elsewhere.bin";
            entry.Clear();
            (variant == 2 ? "FAR     BIN"u8 : "ZZZZZZZZBIN"u8).CopyTo(entry); entry[11] = 0x21;
            BinaryPrimitives.WriteUInt16LittleEndian(entry[24..], 33); BinaryPrimitives.WriteUInt16LittleEndian(entry[26..], 13);
            BinaryPrimitives.WriteUInt32LittleEndian(entry[28..], 3);
            List<byte[]> entries = Disk.Entries(name, entry);
            byte[] late = f.Disk.Sector(f.Disk.Data(8)); int slot = 0; while (slot < 16 && late[slot * 32] != 0) slot++;
            HostCheck.True(slot + entries.Count < 16);
            foreach (byte[] encoded in entries) { encoded.CopyTo(late, slot * 32); slot++; }
            f.MountReady(); var token = BeginLookup(f, "/app/wwwroot/far.bin");
            HostCheck.Equal(Fs.Status.Ambiguous, f.Drive(token)); Fs.OpenResult result = new() { Info = new() { Length = 77 } };
            HostCheck.Equal(Fs.Status.Ambiguous, Fs.CopyOpen(f.Mount, token, &result)); HostCheck.Equal(77U, result.Info.Length);
            HostCheck.True(f.Disk.Reads[f.Disk.Data(8)] == 1 && f.Disk.Reads[f.Disk.Data(13)] == 0 && f.Disk.Reads[f.Disk.Data(200)] == 0);
            HostCheck.Equal(Fs.Status.Cancelled, Fs.Cancel(f.Mount, token));
        }
    }

    [HostTest("webroot.fs loaded lookup consumes exact entry budget even after an early exact match")]
    private static void LookupBatchBudget()
    {
        using var f = new Fixture(workBudget: 7); f.MountReady(); var token = BeginLookup(f, "/app/wwwroot/index.html"); LoadedLookup(f, token);
        var operation = OperationFor(f, token); ulong submitted = f.Device->Submitted, completed = f.Device->Completed, copied = f.Device->Copied;
        HostCheck.Equal(Fs.Status.TraversalBudgetExceeded, Fs.Poll(f.Mount, token));
        HostCheck.True(Field<Fs.Operation, uint>(*operation, "Work") == 7 && Cursor(operation).EntryInSectorForTests() == 7);
        Fs.OpenResult result = new() { Info = new() { Length = 77 } };
        HostCheck.Equal(Fs.Status.TraversalBudgetExceeded, Fs.CopyOpen(f.Mount, token, &result)); HostCheck.Equal(77U, result.Info.Length);
        NoIo(f, submitted, completed, copied); HostCheck.Equal(Fs.Status.Cancelled, Fs.Cancel(f.Mount, token));
    }

    [HostTest("webroot.fs public directory entries stay retained and single step outside internal lookup batching")]
    private static void PublicEntryUnbatched()
    {
        using var f = new Fixture(); f.MountReady(); var directory = f.Open("/app/wwwroot"); Fs.OperationToken token = default;
        HostCheck.Equal(Fs.Status.Pending, Fs.BeginDirectory(f.Mount, directory.File, &token)); HostCheck.Equal(Fs.Status.Entry, f.Drive(token));
        var operation = OperationFor(f, token); uint work = Field<Fs.Operation, uint>(*operation, "Work");
        ulong submitted = f.Device->Submitted, completed = f.Device->Completed, copied = f.Device->Copied; Fs.Metadata metadata = new() { Length = 77 };
        for (int i = 0; i < 3; i++) HostCheck.Equal(Fs.Status.Entry, Fs.Poll(f.Mount, token));
        HostCheck.Equal(Fs.Status.OutputFull, Fs.CopyEntry(f.Mount, token, &metadata, [])); HostCheck.Equal(77U, metadata.Length);
        HostCheck.Equal(work, Field<Fs.Operation, uint>(*operation, "Work")); NoIo(f, submitted, completed, copied);
        Span<byte> name = stackalloc byte[256]; HostCheck.Equal(Fs.Status.Success, Fs.CopyEntry(f.Mount, token, &metadata, name));
        HostCheck.True(name[..metadata.NameBytes].SequenceEqual("index.html"u8));
        HostCheck.Equal(Fs.Status.Pending, Fs.Poll(f.Mount, token)); HostCheck.Equal(work + 1, Field<Fs.Operation, uint>(*operation, "Work"));
        NoIo(f, submitted, completed, copied); HostCheck.Equal(Fs.Status.Cancelled, Fs.Cancel(f.Mount, token));
        HostCheck.Equal(Fs.Status.Success, Fs.Close(f.Mount, directory.File));
    }

    [HostTest("webroot.fs batched lookup cancellation generation reuse and reset revoke loaded cursor progress")]
    private static void LookupBatchLifetime()
    {
        using var f = new Fixture(); f.MountReady(); var old = BeginLookup(f, "/app/wwwroot/missing.bin"); LoadedLookup(f, old);
        HostCheck.Equal(Fs.Status.Pending, Fs.Poll(f.Mount, old)); HostCheck.Equal(Fs.Status.Cancelled, Fs.Cancel(f.Mount, old));
        var fresh = BeginLookup(f, "/app/wwwroot/empty.bin"); LoadedLookup(f, fresh); var operation = OperationFor(f, fresh);
        HostCheck.True(Field<Fs.OperationToken, uint>(fresh, "Index") == Field<Fs.OperationToken, uint>(old, "Index") &&
            Field<Fs.OperationToken, ulong>(fresh, "Generation") > Field<Fs.OperationToken, ulong>(old, "Generation"));
        byte[] state = new ReadOnlySpan<byte>(operation, sizeof(Fs.Operation)).ToArray();
        ulong submitted = f.Device->Submitted, completed = f.Device->Completed, copied = f.Device->Copied;
        HostCheck.Equal(Fs.Status.StaleToken, Fs.Poll(f.Mount, old)); HostCheck.Equal(Fs.Status.StaleToken, Fs.Cancel(f.Mount, old));
        HostCheck.True(new ReadOnlySpan<byte>(operation, sizeof(Fs.Operation)).SequenceEqual(state)); NoIo(f, submitted, completed, copied);
        HostCheck.Equal(Fs.Status.Ready, f.Drive(fresh)); Fs.OpenResult result = default;
        HostCheck.Equal(Fs.Status.Success, Fs.CopyOpen(f.Mount, fresh, &result)); HostCheck.Equal(0U, result.Info.Length);
        HostCheck.Equal(Fs.Status.Success, Fs.Close(f.Mount, result.File));
        fresh = BeginLookup(f, "/app/wwwroot/index.html"); LoadedLookup(f, fresh); operation = OperationFor(f, fresh);
        uint work = Field<Fs.Operation, uint>(*operation, "Work"); HostCheck.Equal(Block.Status.Success, Block.Stop(f.Device));
        submitted = f.Device->Submitted; completed = f.Device->Completed; copied = f.Device->Copied;
        HostCheck.Equal(Fs.Status.DeviceFailed, Fs.Poll(f.Mount, fresh)); HostCheck.Equal(work, Field<Fs.Operation, uint>(*operation, "Work"));
        NoIo(f, submitted, completed, copied); HostCheck.Equal(Fs.Status.Success, Fs.Stop(f.Mount));
    }

    private static Fs.DirectoryCursor Cursor(Fs.Operation* operation) => Field<Fs.Operation, Fs.DirectoryCursor>(*operation, "Cursor");
    private static uint EntryInSectorForTests(this Fs.DirectoryCursor cursor) => Field<Fs.DirectoryCursor, uint>(cursor, "EntryInSector");
    private static bool Loaded(Fs.Operation* operation)
    {
        var cursor = Cursor(operation);
        return Field<Fs.DirectoryCursor, bool>(cursor, "Visited") && Field<Fs.DirectoryCursor, bool>(cursor, "SectorReady") && cursor.EntryInSectorForTests() == 0 &&
            !Field<Fs.DirectoryCursor, bool>(cursor, "HasPending") && !Field<Fs.Operation, bool>(*operation, "LookupDone") && Field<Fs.Operation, byte>(*operation, "Waiting") == 0;
    }
    private static Fs.OperationToken BeginLookup(Fixture f, string path)
    {
        Fs.OperationToken token = default; HostCheck.Equal(Fs.Status.Pending, Fs.BeginOpen(f.Mount, Encoding.ASCII.GetBytes(path), &token)); return token;
    }
    private static void LoadedLookup(Fixture f, Fs.OperationToken token)
    {
        for (int i = 0; i < 4096; i++)
        {
            if (Loaded(OperationFor(f, token))) return;
            f.Tick(); HostCheck.Equal(Fs.Status.Pending, Fs.Poll(f.Mount, token));
        }
        throw new InvalidOperationException("lookup did not reach loaded sector");
    }
    private static void NoIo(Fixture f, ulong submitted, ulong completed, ulong copied, ulong cancelled = 0)
    { HostCheck.Equal(submitted, f.Device->Submitted); HostCheck.Equal(completed, f.Device->Completed); HostCheck.Equal(copied, f.Device->Copied); HostCheck.Equal(cancelled, f.Device->Cancelled); }

    [HostTest("webroot.fs CoreLib sized fragmented maps isolate metadata walks from retained independent 8KiB reads")]
    private static void LargeFragmentedReadWork()
    {
        // Exact measured asset length, synthetic bytes/layout. This isolates
        // host storage work, not CoreLib execution or the failed guest timeline.
        const uint length = 2009369, clusterCount = (length + 511) / 512;
        const string path = "/app/wwwroot/index.html";
        using var f = new Fixture(cacheSlots: 4);
        uint[] chain = Enumerable.Range(0, (int)clusterCount).Select(i => 1024U + (uint)i * 2).ToArray();
        HostCheck.True(clusterCount == 3925 && chain.Distinct().Count() == clusterCount && chain[^1] < 65525);
        byte[] shortSector = f.Disk.Sector(f.Disk.Data(4));
        BinaryPrimitives.WriteUInt16LittleEndian(shortSector.AsSpan(52), (ushort)(chain[0] >> 16));
        BinaryPrimitives.WriteUInt16LittleEndian(shortSector.AsSpan(58), (ushort)chain[0]);
        BinaryPrimitives.WriteUInt32LittleEndian(shortSector.AsSpan(60), length);
        for (int i = 0; i < chain.Length; i++)
        {
            f.Disk.SetFat(chain[i], i + 1 == chain.Length ? 0x0fffffffU : chain[i + 1]);
            byte[] sector = f.Disk.Sector(f.Disk.Data(chain[i]));
            for (int j = 0; j < 512; j++) sector[j] = LargePayload((ulong)i * 512 + (uint)j);
            if (i != 0) HostCheck.True(chain[i] != chain[i - 1] + 1); // Every adjacent logical cluster is fragmented.
        }
        ulong DataReads() { ulong value = 0; foreach (uint cluster in chain) value += f.Disk.Reads[f.Disk.Data(cluster)]; return value; }
        ulong DirectoryReads() { ulong value = 0; foreach (uint cluster in new uint[] { 2, 3, 4, 6, 7, 8 }) value += f.Disk.Reads[f.Disk.Data(cluster)]; return value; }
        var stages = new List<(string Name, ulong Submitted, ulong Fat, ulong Directory, ulong Data, ulong Polls)>();
        void Stage(string name)
        {
            ulong hardware = 0; foreach (uint reads in f.Disk.Reads) hardware += reads;
            HostCheck.True(f.Device->Submitted == hardware && f.Device->Completed == hardware && f.Device->Copied == hardware &&
                f.Mount->SectorReads == hardware && f.Device->Running && !f.Device->ReadPending && !f.Device->ReadReady);
            var prior = stages.Count == 0 ? default : stages[^1];
            stages.Add((name, hardware, f.Disk.FatReads(), DirectoryReads(), DataReads(), f.DrivePolls));
            var current = stages[^1];
            Console.WriteLine($"WEBROOT_LARGE_WORK phase={name} submitted={hardware - prior.Submitted} completed={hardware - prior.Submitted} copied={hardware - prior.Submitted} fat={current.Fat - prior.Fat} directory={current.Directory - prior.Directory} data={current.Data - prior.Data} drivePolls={current.Polls - prior.Polls} cacheHits={f.Mount->CacheHits} cacheMisses={f.Mount->CacheMisses}");
        }
        f.MountReady(); Stage("mount");
        // The current stat-like path opens/validates a complete map, inspects
        // its metadata, then closes. Inspect itself must issue no device I/O.
        var stat = f.Open(path); Fs.FileInspection inspection = default; ulong submitted = f.Device->Submitted;
        HostCheck.Equal(Fs.Status.Success, Fs.Inspect(f.Mount, stat.File, &inspection)); HostCheck.Equal(submitted, f.Device->Submitted);
        HostCheck.True(inspection.Info.Length == length && inspection.AllocationKnown && inspection.AllocatedBlocks == clusterCount);
        HostCheck.Equal(Fs.Status.Success, Fs.Close(f.Mount, stat.File)); Stage("cold-stat"); HostCheck.Equal(0UL, DataReads());
        var a = f.Open(path); Stage("first-open"); var b = f.Open(path); Stage("independent-open");
        HostCheck.True(a.Info.Inode == b.Info.Inode && a.Info.Length == length && b.Info.Length == length && !a.File.Equals(b.File));
        uint aIndex = Field<Fs.FileToken, uint>(a.File, "Index"), bIndex = Field<Fs.FileToken, uint>(b.File, "Index");
        HostCheck.True(aIndex != bIndex && f.Files[aIndex].Clusters != f.Files[bIndex].Clusters &&
            new ReadOnlySpan<uint>(f.Files[aIndex].Clusters, chain.Length).SequenceEqual(chain) &&
            new ReadOnlySpan<uint>(f.Files[bIndex].Clusters, chain.Length).SequenceEqual(chain));
        var closed = b.File; HostCheck.Equal(Fs.Status.Success, Fs.Close(f.Mount, closed));
        var far = f.Open("/app/wwwroot/far.bin"); Stage("cache-evict");
        Fs.OperationToken invalid = default; submitted = f.Device->Submitted;
        HostCheck.Equal(Fs.Status.StaleToken, Fs.BeginRead(f.Mount, closed, 0, 8192, &invalid)); HostCheck.Equal(submitted, f.Device->Submitted);
        HostCheck.Equal(Fs.Status.Success, Fs.Close(f.Mount, far.File)); b = f.Open(path); Stage("reopen");
        HostCheck.True(Field<Fs.FileToken, uint>(b.File, "Index") == bIndex &&
            Field<Fs.FileToken, ulong>(b.File, "Generation") > Field<Fs.FileToken, ulong>(closed, "Generation") &&
            new ReadOnlySpan<uint>(f.Files[aIndex].Clusters, chain.Length).SequenceEqual(chain) &&
            new ReadOnlySpan<uint>(f.Files[bIndex].Clusters, chain.Length).SequenceEqual(chain));
        HostCheck.Equal(0UL, DataReads());
        ulong readsBefore = f.Device->Submitted, fatBefore = f.Disk.FatReads(), directoryBefore = DirectoryReads(), hits = f.Mount->CacheHits, misses = f.Mount->CacheMisses;
        byte[] output = new byte[8192]; uint ranges = (length + 8191) / 8192;
        void ReadRange(Fs.FileToken file, ulong offset)
        {
            uint bytes = (uint)Math.Min(8192UL, length - offset), sectors = (bytes + 511) / 512;
            output.AsSpan().Fill(0xa5); ulong before = f.Device->Submitted;
            f.Read(file, offset, 8192, output, bytes);
            HostCheck.True(f.Device->Submitted == before + sectors && f.Device->Completed == before + sectors && f.Device->Copied == before + sectors);
            for (uint i = 0; i < bytes; i++) HostCheck.Equal(LargePayload(offset + i), output[i]);
            HostCheck.True(output.AsSpan((int)bytes).IndexOfAnyExcept((byte)0xa5) < 0);
        }
        for (uint i = 0; i < ranges; i++)
        {
            ReadRange(a.File, (ulong)i * 8192);
            ReadRange(b.File, (ulong)(ranges - 1 - i) * 8192); // Independent map/offset in reverse order.
        }
        HostCheck.True(f.Device->Submitted - readsBefore == 2UL * clusterCount && DataReads() == 2UL * clusterCount &&
            f.Disk.FatReads() == fatBefore && DirectoryReads() == directoryBefore && f.Mount->CacheHits == hits && f.Mount->CacheMisses == misses);
        foreach (uint cluster in chain) HostCheck.Equal(2U, f.Disk.Reads[f.Disk.Data(cluster)]);
        Stage("two-handle-8KiB-pass");
        HostCheck.Equal(Fs.Status.Success, Fs.Close(f.Mount, a.File)); HostCheck.Equal(Fs.Status.Success, Fs.Close(f.Mount, b.File));
        submitted = f.Device->Submitted;
        HostCheck.Equal(Fs.Status.StaleToken, Fs.BeginRead(f.Mount, a.File, 0, 8192, &invalid));
        HostCheck.Equal(Fs.Status.StaleToken, Fs.BeginRead(f.Mount, b.File, 0, 8192, &invalid)); HostCheck.Equal(submitted, f.Device->Submitted);
        HostCheck.True(f.UsedArenaBytes < Fixture.ArenaBytes && stages.Count == 7);
        Console.WriteLine($"WEBROOT_LARGE_SUMMARY bytes={length} clusters={clusterCount} fragmentedExtents={chain.Length} rangesPerHandle={ranges} requestedRange=8192 retainedHandleDataReads={2UL * clusterCount} retainedHandleFatReads={f.Disk.FatReads() - fatBefore} totalSubmitted={f.Device->Submitted} totalCompleted={f.Device->Completed} totalCopied={f.Device->Copied} arenaBytes={f.UsedArenaBytes} maxChainSteps={Fs.MaximumCachedChainSteps} syntheticPayload=true guestTiming=false");
    }

    private static byte LargePayload(ulong position) => unchecked((byte)((position * 73 + 19) ^ (position >> 9)));

    [HostTest("webroot.fs cached long chains advance a bounded selected batch without device progress")]
    private static void CachedChainBatch()
    {
        using var f = new Fixture(cacheSlots: 8, slots: 4);
        LongIndex(f, 200); f.MountReady();
        var warm = f.Open("/app/wwwroot/index.html");
        HostCheck.Equal(Fs.Status.Success, Fs.Close(f.Mount, warm.File));
        var a = ChainLookup(f); var b = ChainLookup(f);
        var operation = OperationFor(f, a); var other = OperationFor(f, b);
        uint before = ClusterCount(f, operation), otherBefore = ClusterCount(f, other);
        ulong submitted = f.Device->Submitted, completed = f.Device->Completed, copied = f.Device->Copied, cancelled = f.Device->Cancelled;
        HostCheck.Equal(Fs.Status.Pending, Fs.Poll(f.Mount, a));
        uint advanced = ClusterCount(f, operation) - before;
        HostCheck.True(advanced > 1 && advanced <= 64); HostCheck.Equal(64U, Fs.MaximumCachedChainSteps);
        HostCheck.Equal(otherBefore, ClusterCount(f, other));
        HostCheck.Equal(submitted, f.Device->Submitted); HostCheck.Equal(completed, f.Device->Completed);
        HostCheck.Equal(copied, f.Device->Copied); HostCheck.Equal(cancelled, f.Device->Cancelled);
        HostCheck.True(!Field<Fs.Mount, bool>(*f.Mount, "IoActive") && Field<Fs.Operation, byte>(*operation, "Waiting") == 0);
        HostCheck.Equal(Fs.Status.Pending, Fs.Poll(f.Mount, b));
        HostCheck.True(ClusterCount(f, other) - otherBefore is > 1 and <= 64);
        HostCheck.Equal(before + advanced, ClusterCount(f, operation));
        HostCheck.Equal(submitted, f.Device->Submitted); HostCheck.Equal(completed, f.Device->Completed);
        HostCheck.Equal(copied, f.Device->Copied); HostCheck.Equal(cancelled, f.Device->Cancelled);
        HostCheck.Equal(Fs.Status.Cancelled, Fs.Cancel(f.Mount, a));
        HostCheck.Equal(Fs.Status.Cancelled, Fs.Cancel(f.Mount, b));
        HostCheck.Equal(Fs.Status.StaleToken, Fs.Poll(f.Mount, a));
        HostCheck.Equal(Fs.Status.StaleToken, Fs.Poll(f.Mount, b));
    }

    [HostTest("webroot.fs cached chain batch stops at one submit and preserves separate completion turns")]
    private static void ChainBatchIoBoundary()
    {
        using var f = new Fixture(cacheSlots: 8); LongIndex(f, 200); f.MountReady();
        var token = ChainLookup(f); var operation = OperationFor(f, token);
        ulong submitted = f.Device->Submitted, completed = f.Device->Completed, copied = f.Device->Copied;
        HostCheck.Equal(Fs.Status.Pending, Fs.Poll(f.Mount, token));
        uint count = ClusterCount(f, operation);
        HostCheck.True(Field<Fs.Mount, bool>(*f.Mount, "IoActive") && Field<Fs.Operation, byte>(*operation, "Waiting") != 0 && f.Device->ReadPending);
        HostCheck.Equal(submitted + 1, f.Device->Submitted); HostCheck.Equal(completed, f.Device->Completed); HostCheck.Equal(copied, f.Device->Copied);
        f.Tick(); HostCheck.Equal(Fs.Status.Pending, Fs.Poll(f.Mount, token));
        HostCheck.Equal(submitted + 1, f.Device->Submitted); HostCheck.Equal(completed + 1, f.Device->Completed);
        HostCheck.Equal(copied + 1, f.Device->Copied); HostCheck.Equal(count, ClusterCount(f, operation));
        HostCheck.True(!Field<Fs.Mount, bool>(*f.Mount, "IoActive") && Field<Fs.Operation, byte>(*operation, "Waiting") == 0);
        HostCheck.Equal(Fs.Status.Pending, Fs.Poll(f.Mount, token));
        HostCheck.Equal(submitted + 2, f.Device->Submitted); HostCheck.Equal(completed + 1, f.Device->Completed);
        HostCheck.Equal(copied + 1, f.Device->Copied); HostCheck.Equal(count, ClusterCount(f, operation));
        HostCheck.Equal(Fs.Status.Pending, Fs.Cancel(f.Mount, token));
        HostCheck.Equal(Fs.Status.Cancelled, f.Drive(token)); HostCheck.Equal(Fs.Status.StaleToken, Fs.Poll(f.Mount, token));
    }

    [HostTest("webroot.fs another pending IO keeps cached chains to one step until actual copy completes")]
    private static void ChainBatchOtherOwner()
    {
        using var f = new Fixture(cacheSlots: 8, slots: 4); LongIndex(f, 200); f.MountReady();
        var file = f.Open("/app/wwwroot/index.html"); var chain = ChainLookup(f);
        var operation = OperationFor(f, chain); Fs.OperationToken read = default;
        HostCheck.Equal(Fs.Status.Pending, Fs.BeginRead(f.Mount, file.File, 0, 1, &read));
        HostCheck.Equal(Fs.Status.Pending, Fs.Poll(f.Mount, read));
        ulong submitted = f.Device->Submitted, completed = f.Device->Completed, copied = f.Device->Copied;
        uint before = ClusterCount(f, operation);
        HostCheck.Equal(Fs.Status.Pending, Fs.Poll(f.Mount, chain));
        HostCheck.Equal(before + 1, ClusterCount(f, operation));
        HostCheck.True(Field<Fs.Mount, bool>(*f.Mount, "IoActive") && Field<Fs.Mount, uint>(*f.Mount, "IoIndex") == Field<Fs.OperationToken, uint>(read, "Index"));
        HostCheck.Equal(submitted, f.Device->Submitted); HostCheck.Equal(completed, f.Device->Completed); HostCheck.Equal(copied, f.Device->Copied);
        f.Tick(); HostCheck.Equal(Fs.Status.Ready, Fs.Poll(f.Mount, read));
        Span<byte> output = stackalloc byte[1]; uint bytes = 0;
        HostCheck.Equal(Fs.Status.Success, Fs.CopyRead(f.Mount, read, output, &bytes)); HostCheck.Equal(1U, bytes);
        before = ClusterCount(f, operation);
        HostCheck.Equal(Fs.Status.Pending, Fs.Poll(f.Mount, chain));
        HostCheck.True(ClusterCount(f, operation) - before is > 1 and <= 64);
        HostCheck.Equal(submitted, f.Device->Submitted); HostCheck.Equal(completed + 1, f.Device->Completed); HostCheck.Equal(copied + 1, f.Device->Copied);
        HostCheck.Equal(Fs.Status.Cancelled, Fs.Cancel(f.Mount, chain)); HostCheck.Equal(Fs.Status.Success, Fs.Close(f.Mount, file.File));
    }

    [HostTest("webroot.fs selected data batching crosses contiguous clusters but stops at fragmentation and partial sectors")]
    private static void BatchedDataReads()
    {
        foreach (bool fragmented in new[] { false, true })
        {
            using var f = new Fixture(dataReadSectors: 4); LongIndex(f, 10);
            uint[] chain = [10, 1024, 1025, 1026, 1027, 1028, 1029, 1030, 1031, 1032];
            if (fragmented) chain[5] = 2000;
            for (int i = 0; i < chain.Length; i++)
            {
                f.Disk.SetFat(chain[i], i == chain.Length - 1 ? 0x0fffffffU : chain[i + 1]);
                byte[] sector = f.Disk.Sector(f.Disk.Data(chain[i]));
                for (int j = 0; j < 512; j++) sector[j] = Payload(i * 512 + j);
            }
            f.MountReady(); var file = f.Open("/app/wwwroot/index.html");
            foreach (var range in new[] { (Offset: 0UL, Bytes: 5120U), (Offset: 3UL, Bytes: 2600U), (Offset: 511UL, Bytes: 4609U) })
            {
                ulong before = f.Device->Submitted; byte[] output = new byte[range.Bytes];
                f.Read(file.File, range.Offset, range.Bytes, output, range.Bytes);
                for (int i = 0; i < output.Length; i++) HostCheck.Equal(Payload((int)range.Offset + i), output[i]);
                HostCheck.True(f.Device->Submitted - before is >= 3 and <= 5);
                if (!fragmented && range.Offset == 0) HostCheck.Equal(4UL, f.Device->Submitted - before);
            }
            HostCheck.Equal(Fs.Status.Success, Fs.Close(f.Mount, file.File));
        }
    }

    [HostTest("webroot.fs multi-sector cancellation publishes no result and invalid batching leaves admission untouched")]
    private static void BatchedCancellation()
    {
        using var f = new Fixture(dataReadSectors: 4); LongIndex(f, 10);
        var before = new ReadOnlySpan<byte>(f.Mount, sizeof(Fs.Mount)).ToArray();
        foreach (uint bound in new[] { 0U, 5U })
        {
            HostCheck.Equal(Fs.Status.InvalidAccess, Fs.Start(f.Mount, f.Input, bound));
            HostCheck.True(before.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(f.Mount, sizeof(Fs.Mount))));
        }
        f.MountReady(); var file = f.Open("/app/wwwroot/index.html"); Fs.OperationToken token = default;
        HostCheck.Equal(Fs.Status.Pending, Fs.BeginRead(f.Mount, file.File, 512, 2048, &token));
        HostCheck.Equal(Fs.Status.Pending, Fs.Poll(f.Mount, token));
        HostCheck.True(f.Device->ReadPending); ulong copied = f.Device->Copied;
        HostCheck.Equal(Fs.Status.Pending, Fs.Cancel(f.Mount, token));
        HostCheck.Equal(Fs.Status.Cancelled, f.Drive(token)); HostCheck.Equal(copied, f.Device->Copied);
        byte[] output = Enumerable.Repeat((byte)0xa5, 2048).ToArray(); uint bytes = 77;
        HostCheck.True(Fs.CopyRead(f.Mount, token, output, &bytes) != Fs.Status.Success);
        HostCheck.True(output.All(v => v == 0xa5) && bytes == 77);
        f.Read(file.File, 512, 2048, output, 2048);
        HostCheck.True(output.All(v => v == 0));
        HostCheck.Equal(Fs.Status.Success, Fs.Close(f.Mount, file.File));
    }

    private static void LongIndex(Fixture f, uint clusters)
    {
        byte[] directory = f.Disk.Sector(f.Disk.Data(4));
        HostCheck.Equal(700U, BinaryPrimitives.ReadUInt32LittleEndian(directory.AsSpan(60)));
        BinaryPrimitives.WriteUInt32LittleEndian(directory.AsSpan(60), clusters * 512);
        f.Disk.SetFat(10, 1024);
        for (uint i = 0; i < clusters - 1; i++) f.Disk.SetFat(1024 + i, i == clusters - 2 ? 0x0fffffffU : 1025 + i);
    }

    private static Fs.OperationToken ChainLookup(Fixture f)
    {
        Fs.OperationToken token = default;
        HostCheck.Equal(Fs.Status.Pending, Fs.BeginOpen(f.Mount, "/app/wwwroot/index.html"u8, &token));
        for (int i = 0; i < 4096 && !Field<Fs.Operation, bool>(*OperationFor(f, token), "LookupDone"); i++)
        { f.Tick(); HostCheck.Equal(Fs.Status.Pending, Fs.Poll(f.Mount, token)); }
        HostCheck.True(Field<Fs.Operation, bool>(*OperationFor(f, token), "LookupDone") && !Field<Fs.Operation, bool>(*OperationFor(f, token), "HaveCandidate"));
        return token;
    }

    // Host-only copied scalar observations for bounded-progress assertions.
    // Never write private slots, forge a token or expose a kernel test API.
    private static TValue Field<T, TValue>(T value, string name) where T : unmanaged =>
        (TValue)typeof(T).GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(value)!;
    private static Fs.Operation* OperationFor(Fixture f, Fs.OperationToken token) =>
        f.Operations + Field<Fs.OperationToken, uint>(token, "Index");
    private static uint ClusterCount(Fixture f, Fs.Operation* operation) =>
        Field<Fs.FileHandle, uint>(f.Files[Field<Fs.Operation, uint>(*operation, "FileIndex")], "ClusterCount");

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
        using var f = new Fixture(); f.MountReady(); HostCheck.True(f.OneRead());
        long allocated = MeasureReadAllocations(f, out bool good);
        HostCheck.True(good); HostCheck.Equal(0L, allocated); return true;
    }

    private static long MeasureReadAllocations(Fixture fixture, out bool good)
    {
        // Keep background-GC accounting outside this synchronous host measurement.
        // Allocations still count; admission or premature region exit must fail.
        HostCheck.True(GC.TryStartNoGCRegion(1024 * 1024, disallowFullBlockingGC: false));
        try
        {
            long before = GC.GetAllocatedBytesForCurrentThread(); good = true;
            for (int i = 0; i < 32; i++) good &= fixture.OneRead();
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }
        finally { GC.EndNoGCRegion(); }
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

    [HostTest("webroot.fs observer retains same-turn completion bytes and rejects before range publication")]
    private static bool SectorObservations()
    {
        Span<byte> output = stackalloc byte[16];
        for (int rejected = 0; rejected < 2; rejected++)
        {
            FsObservation sink = default;
            using var f = new Fixture(observation: new() { Context = &sink, Observe = &ObserveSector });
            sink.Device = f.Device; HostCheck.Equal(0UL, sink.Submitted);
            f.MountReady(); var file = f.Open("/app/wwwroot/index.html");
            Fs.OperationToken token = default;
            HostCheck.Equal(Fs.Status.Pending, Fs.BeginRead(f.Mount, file.File, 0, 7, &token));
            HostCheck.Equal(Fs.Status.Pending, Fs.Poll(f.Mount, token)); HostCheck.True(f.Device->ReadPending);
            ulong before = f.Device->Copied, events = sink.CopyCandidates; sink.Reject = rejected != 0;
            f.Tick(); Fs.Status status = Fs.Poll(f.Mount, token);
            HostCheck.Equal(events + 1, sink.CopyCandidates); HostCheck.Equal(before + 1, sink.Candidate.Sequence);
            HostCheck.Equal(f.Disk.Data(10), sink.Candidate.Sector);
            HostCheck.True(new ReadOnlySpan<byte>(sink.Payload, 512).SequenceEqual(f.Disk.Sector(f.Disk.Data(10))) && !sink.Bad);
            output.Fill(0xa5); uint bytes = 99;
            if (rejected == 0)
            {
                HostCheck.Equal(Fs.Status.Ready, status); HostCheck.Equal(sink.Candidate.Sequence, f.Device->Copied);
                HostCheck.True(!f.Device->ReadReady && f.Device->Running);
                HostCheck.Equal(Fs.Status.Success, Fs.CopyRead(f.Mount, token, output, &bytes)); HostCheck.Equal(7U, bytes);
                for (int i = 0; i < 7; i++) HostCheck.Equal(Payload(i), output[i]);
                HostCheck.True(output[7..].IndexOfAnyExcept((byte)0xa5) < 0); HostCheck.Equal(Fs.Status.Success, Fs.Close(f.Mount, file.File));
            }
            else
            {
                HostCheck.Equal(Fs.Status.DeviceFailed, status); HostCheck.Equal(before, f.Device->Copied);
                HostCheck.Equal(Block.Status.ObservationRejected, f.Device->LastFailure);
                HostCheck.Equal(Fs.Status.InvalidState, Fs.CopyRead(f.Mount, token, output, &bytes));
                HostCheck.Equal(99U, bytes); HostCheck.True(output.IndexOfAnyExcept((byte)0xa5) < 0);
                HostCheck.True(f.Device->RequiresCleanup && f.Pool->AllocatedMask == 1);
                ulong observed = sink.Submitted + sink.Completed + sink.CopyCandidates;
                HostCheck.Equal(Block.Status.Success, Block.Stop(f.Device)); HostCheck.Equal(Fs.Status.Success, Fs.Stop(f.Mount));
                HostCheck.Equal(observed, sink.Submitted + sink.Completed + sink.CopyCandidates);
            }
        }
        return true;
    }

    [HostTest("webroot.fs preserved boot directories allow unspecified zero FAT timestamps")]
    private static bool UnspecifiedModifiedTime()
    {
        using var f = new Fixture();
        byte[] root = f.Disk.Sector(f.Disk.Data(2));
        // Preserve the existing imported app LFN/short pair after the original
        // image writer's EFI and OS short entries, whose timestamp bytes are zero.
        root.AsSpan(0, 64).CopyTo(root.AsSpan(64)); root.AsSpan(0, 64).Clear();
        "EFI        "u8.CopyTo(root); "OS         "u8.CopyTo(root.AsSpan(32));
        root[11] = root[43] = 0x10;
        BinaryPrimitives.WriteUInt16LittleEndian(root.AsSpan(26), 20);
        BinaryPrimitives.WriteUInt16LittleEndian(root.AsSpan(58), 21);
        f.Disk.SetFat(20, 0x0fffffff); f.Disk.SetFat(21, 0x0fffffff);
        HostCheck.Equal((ushort)0x21, BinaryPrimitives.ReadUInt16LittleEndian(root.AsSpan(96 + 24)));
        f.Disk.Sector(f.Disk.Data(3)).AsSpan(32 + 22, 4).Clear();
        f.Disk.Sector(f.Disk.Data(4)).AsSpan(32 + 22, 4).Clear();

        f.MountReady();
        var directory = f.Open("/app/wwwroot");
        HostCheck.Equal(0L, directory.Info.ModifiedSeconds);
        Fs.FileInspection inspection = default;
        HostCheck.Equal(Fs.Status.Success, Fs.Inspect(f.Mount, directory.File, &inspection));
        HostCheck.True(inspection.Info.IsDirectory); HostCheck.Equal(0L, inspection.Info.ModifiedSeconds);
        Fs.OperationToken cursor = default;
        HostCheck.Equal(Fs.Status.Pending, Fs.BeginDirectory(f.Mount, directory.File, &cursor));
        HostCheck.Equal(Fs.Status.Entry, f.Drive(cursor));
        Span<byte> name = stackalloc byte[256]; Fs.Metadata info = default;
        HostCheck.Equal(Fs.Status.Success, Fs.CopyEntry(f.Mount, cursor, &info, name));
        HostCheck.True(name[..info.NameBytes].SequenceEqual("index.html"u8)); HostCheck.Equal(0L, info.ModifiedSeconds);
        HostCheck.Equal(Fs.Status.Cancelled, Fs.Cancel(f.Mount, cursor));
        HostCheck.Equal(Fs.Status.Success, Fs.Close(f.Mount, directory.File));

        var file = f.Open("/app/wwwroot/index.html");
        HostCheck.Equal(0L, file.Info.ModifiedSeconds);
        HostCheck.Equal(Fs.Status.Success, Fs.Inspect(f.Mount, file.File, &inspection));
        HostCheck.True(!inspection.Info.IsDirectory); HostCheck.Equal(0L, inspection.Info.ModifiedSeconds);
        Span<byte> one = stackalloc byte[1]; f.Read(file.File, 0, 1, one, 1); HostCheck.Equal(Payload(0), one[0]);
        HostCheck.Equal(Fs.Status.Success, Fs.Close(f.Mount, file.File));
        return true;
    }

    [HostTest("webroot.fs nonzero malformed FAT timestamps still reject during mount and open")]
    private static bool MalformedModifiedTime()
    {
        (ushort Date, ushort Time)[] cases =
        [ (0, 1), (1, 0), (0x20, 0), (0x21, 0xf800), (0x21, 0x07e0),
          (0x21, 0x001f), (0x005e, 0), (0x025d, 0) ];
        foreach (var item in cases)
        {
            using (var f = new Fixture())
            {
                byte[] root = f.Disk.Sector(f.Disk.Data(2));
                BinaryPrimitives.WriteUInt16LittleEndian(root.AsSpan(32 + 24), item.Date);
                BinaryPrimitives.WriteUInt16LittleEndian(root.AsSpan(32 + 22), item.Time);
                HostCheck.Equal(Fs.Status.CorruptDirectory, f.StartMount());
            }
            using (var f = new Fixture())
            {
                byte[] directory = f.Disk.Sector(f.Disk.Data(4));
                BinaryPrimitives.WriteUInt16LittleEndian(directory.AsSpan(32 + 24), item.Date);
                BinaryPrimitives.WriteUInt16LittleEndian(directory.AsSpan(32 + 22), item.Time);
                f.MountReady(); Fs.OperationToken operation = default;
                HostCheck.Equal(Fs.Status.Pending, Fs.BeginOpen(f.Mount, "/app/wwwroot/index.html"u8, &operation));
                HostCheck.Equal(Fs.Status.CorruptDirectory, f.Drive(operation));
                HostCheck.Equal(Fs.Status.Cancelled, Fs.Cancel(f.Mount, operation));
            }
        }
        return true;
    }

    [UnmanagedCallersOnly]
    private static byte ObserveSector(void* context, Block.Observation* observation, byte* payload)
    {
        var sink = (FsObservation*)context;
        sink->Bad |= observation->Owner != (ulong)sink->Device || observation->Generation == 0;
        if (observation->Kind == Block.ObservationKind.Submitted) sink->Submitted++;
        else if (observation->Kind == Block.ObservationKind.CompletionValidated) sink->Completed++;
        else
        {
            if (sink->CheckDirectoryCache)
            {
                sink->CheckedDirectoryCache = true;
                for (uint i = 0; i < sink->CacheSlots; i++) sink->Bad |= *(ulong*)(sink->CacheBacking + i * 48 + 40) != 0;
                sink->Bad |= new ReadOnlySpan<byte>(sink->CacheBacking + sink->CacheSlots * 48,
                    (int)(sink->CacheSlots * 512)).IndexOfAnyExcept((byte)0xa5) >= 0;
            }
            sink->CopyCandidates++; sink->Candidate = *observation;
            sink->Bad |= observation->Kind != Block.ObservationKind.CopyReady || payload == null || observation->Bytes != 512 ||
                observation->Sequence != sink->Device->Copied + 1;
            if (payload != null) for (int i = 0; i < 512; i++) sink->Payload[i] = payload[i];
            return sink->Reject ? (byte)0 : (byte)1;
        }
        sink->Bad |= payload != null; return 1;
    }

    private static byte Payload(int index) => unchecked((byte)(index * 31 ^ (index >> 8) ^ 0x5a));
    private const ulong Common = 0x400000, Notify = 0x403000, Config = 0x402000, Physical = 0x220000, DeviceBase = 0x123456780000;


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
        if (address == (ulong)h->Data + 150)
        {
            if (h->CompleteOnPoll) Complete(h);
            *value = *(ushort*)(h->Data + 150); return 1;
        }
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

    // A test device publishes a genuine used-ring completion. Immediate mode
    // runs at the device's next index read; it never edits block/FS ownership.
    private static void Complete(Hardware* h)
    {
        if (!h->Pending) return;
        var disk = (Disk)GCHandle.FromIntPtr(h->DiskHandle).Target!;
        uint length = *(uint*)(h->Data + 24);
        HostCheck.True(length is >= 512 and <= 2048 && length % 512 == 0);
        bool failure = false;
        for (uint offset = 0; offset < length; offset += 512)
        {
            ulong lba = h->Sector + offset / 512; disk.Reads[lba]++;
            if (disk.Sectors.TryGetValue(lba, out var bytes)) bytes.CopyTo(new Span<byte>(h->Data + 512 + offset, 512));
            else new Span<byte>(h->Data + 512 + offset, 512).Clear();
            failure |= lba == disk.FailLba;
        }
        h->Data[272] = failure ? (byte)1 : (byte)0;
        byte* used = h->Data + 152 + (h->Produced & 7) * 8; *(uint*)used = 0; *(uint*)(used + 4) = failure ? 1U : length + 1;
        h->Produced++; *(ushort*)(h->Data + 150) = unchecked((ushort)h->Produced); h->Pending = false;
    }

}
