using Block = OS.Storage.ContinuingReadOnlyBlockService;
using Fs = OS.Storage.ReadOnlyWebRootFileSystem;

internal static unsafe partial class ReadOnlyWebRootFileSystemTests
{
    [HostTest("webroot.fs twelve-slot directory cache rejects oversized counts lengths and aliases without mutation")]
    private static void DirectoryCacheCoverageAdmission()
    {
        const uint bytes = 6720;
        using var f = new Fixture(directoryCacheSlots: 12);
        byte[] untouched = new ReadOnlySpan<byte>(f.DirectoryCacheBacking, (int)bytes).ToArray();
        HostCheck.Equal(Fs.Status.InvalidAccess, Fs.EnableImmutableDirectoryCache(f.Mount, f.DirectoryCacheBacking, bytes, 12));
        HostCheck.Equal(Fs.Status.Pending, Fs.Start(f.Mount, f.Input));
        byte[] mount = new ReadOnlySpan<byte>(f.Mount, sizeof(Fs.Mount)).ToArray();
        foreach (uint invalidBytes in new uint[] { bytes - 1, bytes + 1 })
            HostCheck.Equal(Fs.Status.InvalidAccess, Fs.EnableImmutableDirectoryCache(f.Mount, f.DirectoryCacheBacking, invalidBytes, 12));
        HostCheck.Equal(Fs.Status.InvalidAccess, Fs.EnableImmutableDirectoryCache(f.Mount, f.DirectoryCacheBacking, bytes, 13));
        foreach (ulong address in new ulong[] { (ulong)f.Operations[0].Storage.Sector, (ulong)f.Operations[1].Storage.Range,
            (ulong)f.Mount, (ulong)f.Cache[0].Data, (ulong)f.Data, (ulong)(f.DirectoryCacheBacking + 1) })
            HostCheck.Equal(Fs.Status.InvalidStorage, Fs.EnableImmutableDirectoryCache(f.Mount, (byte*)address, bytes, 12));
        HostCheck.True(new ReadOnlySpan<byte>(f.Mount, sizeof(Fs.Mount)).SequenceEqual(mount));
        HostCheck.True(new ReadOnlySpan<byte>(f.DirectoryCacheBacking, (int)bytes).SequenceEqual(untouched));
        HostCheck.Equal(0UL, f.Device->Submitted);
        HostCheck.Equal(Fs.Status.Success, Fs.EnableImmutableDirectoryCache(f.Mount, f.DirectoryCacheBacking, bytes, 12));
        HostCheck.Equal(12, DirectoryCachePhases(f, 0));
        HostCheck.Equal(0, DirectoryCachePhases(f, 2));
        DirectoryCoverageCanaries(f);
    }

    [HostTest("webroot.fs twelve-slot coverage removes warm directory reads while preserving complete parser work")]
    private static void DirectoryCacheCoverageWarmReads()
    {
        uint baselineWork = 0;
        foreach (uint slots in new uint[] { 5, 12 })
        {
            using var f = new Fixture(directoryCacheSlots: slots);
            uint[] clusters = TwelveSectorDirectory(f);
            f.MountReady();
            HostCheck.Equal(0UL, DirectoryCoverageReads(f, clusters));
            var cold = CacheOpen(f, "/app/wwwroot/index.html", out uint coldWork);
            HostCheck.Equal(12UL, DirectoryCoverageReads(f, clusters));
            HostCheck.Equal((int)slots, DirectoryCachePhases(f, 2));
            HostCheck.Equal(Fs.Status.Success, Fs.Close(f.Mount, cold.File));
            if (slots == 5)
                baselineWork = coldWork;
            else
                HostCheck.Equal(baselineWork, coldWork);
            for (int repeat = 0; repeat < 5; repeat++)
            {
                ulong reads = DirectoryCoverageReads(f, clusters);
                ulong submitted = f.Device->Submitted, completed = f.Device->Completed, copied = f.Device->Copied;
                var warm = CacheOpen(f, "/app/wwwroot/index.html", out uint warmWork);
                HostCheck.Equal(coldWork, warmWork);
                HostCheck.True(cold.Info.Equals(warm.Info));
                HostCheck.True(!cold.File.Equals(warm.File));
                HostCheck.Equal(slots == 12 ? 0UL : 7UL, DirectoryCoverageReads(f, clusters) - reads);
                if (slots == 12)
                    NoIo(f, submitted, completed, copied);
                HostCheck.Equal(Fs.Status.Success, Fs.Close(f.Mount, warm.File));
            }
            HostCheck.Equal(slots == 12 ? 12UL : 47UL, DirectoryCoverageReads(f, clusters));
            HostCheck.Equal(0U, f.Disk.Reads[f.Disk.Data(10)]);
            HostCheck.Equal(0U, f.Disk.Reads[f.Disk.Data(12)]);
            DirectoryCoverageCanaries(f);
            HostCheck.Equal(Fs.Status.Success, Fs.Stop(f.Mount));
            HostCheck.Equal(0, DirectoryCachePhases(f, 2));
        }
    }

    [HostTest("webroot.fs twelfth directory fill preserves cancellation stale tokens and fault invalidation")]
    private static void DirectoryCacheCoverageInterruptedFill()
    {
        for (int fault = 0; fault < 2; fault++)
        {
            using var f = new Fixture(directoryCacheSlots: 12);
            uint[] clusters = TwelveSectorDirectory(f);
            new Span<byte>(f.DirectoryCacheBacking, 6720).Fill(0xa5);
            f.MountReady();
            var token = BeginLookup(f, "/app/wwwroot/index.html");
            bool pendingLast = false;
            for (int turn = 0; turn < 100000; turn++)
            {
                if (f.Device->ReadPending && Field<Block.State, ulong>(*f.Device, "RequestSector") == f.Disk.Data(clusters[^1]))
                {
                    pendingLast = true;
                    break;
                }
                f.Tick();
                HostCheck.Equal(Fs.Status.Pending, Fs.Poll(f.Mount, token));
            }
            HostCheck.True(pendingLast);
            HostCheck.Equal(11, DirectoryCachePhases(f, 2));
            HostCheck.Equal(1, DirectoryCachePhases(f, 0));
            if (fault == 0)
            {
                HostCheck.Equal(Fs.Status.Pending, Fs.Cancel(f.Mount, token));
                HostCheck.Equal(Fs.Status.Cancelled, f.Drive(token));
                HostCheck.Equal(11, DirectoryCachePhases(f, 2));
                ulong submitted = f.Device->Submitted, completed = f.Device->Completed, copied = f.Device->Copied, cancelled = f.Device->Cancelled;
                HostCheck.Equal(Fs.Status.StaleToken, Fs.Poll(f.Mount, token));
                NoIo(f, submitted, completed, copied, cancelled);
            }
            else
            {
                f.Disk.FailLba = f.Disk.Data(clusters[^1]);
                HostCheck.Equal(Fs.Status.DeviceFailed, f.Drive(token));
                HostCheck.Equal(0, DirectoryCachePhases(f, 2));
            }
            HostCheck.True(new ReadOnlySpan<byte>(f.DirectoryCacheBacking + 12 * 48 + 11 * 512, 512).IndexOfAnyExcept((byte)0xa5) < 0);
            DirectoryCoverageCanaries(f);
            if (fault == 0)
            {
                var fresh = f.Open("/app/wwwroot/index.html");
                HostCheck.Equal(12, DirectoryCachePhases(f, 2));
                HostCheck.Equal(Fs.Status.Success, Fs.Close(f.Mount, fresh.File));
            }
        }
    }

    private static uint[] TwelveSectorDirectory(Fixture f)
    {
        uint[] clusters = [4, 6, 7, 8, 20, 21, 22, 23, 24, 25, 26, 27];
        f.Disk.NoDirectoryEnd();
        for (int i = 3; i < clusters.Length - 1; i++)
            f.Disk.SetFat(clusters[i], clusters[i + 1]);
        f.Disk.SetFat(clusters[^1], 0x0fffffff);
        for (int i = 4; i < clusters.Length; i++)
        {
            byte[] sector = f.Disk.Sector(f.Disk.Data(clusters[i]));
            sector.AsSpan().Clear();
            if (i != clusters.Length - 1)
                for (int entry = 0; entry < 16; entry++)
                    sector[entry * 32] = 0xe5;
        }
        return clusters;
    }

    private static ulong DirectoryCoverageReads(Fixture f, uint[] clusters)
    {
        ulong reads = 0;
        foreach (uint cluster in clusters)
            reads += f.Disk.Reads[f.Disk.Data(cluster)];
        return reads;
    }

    private static void DirectoryCoverageCanaries(Fixture f)
    {
        HostCheck.True(new ReadOnlySpan<byte>(f.DirectoryCacheBacking - 16, 16).IndexOfAnyExcept((byte)0xc7) < 0);
        HostCheck.True(new ReadOnlySpan<byte>(f.DirectoryCacheBacking + Fs.ImmutableDirectoryCacheBytes(f.DirectoryCacheSlots), 16).IndexOfAnyExcept((byte)0xc7) < 0);
    }
}
