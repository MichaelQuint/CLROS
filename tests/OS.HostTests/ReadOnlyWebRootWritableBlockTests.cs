using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using OS.Storage;
using B = OS.Storage.WritableBlockSession;
using C = OS.Storage.ContinuingFat32Mutation;
using Fs = OS.Storage.ReadOnlyWebRootFileSystem;
using P = OS.Storage.WritableBlockReadPort;
using Image = OS.Tooling.CombinedWebDataImageFixture;
using Data = OS.Tooling.WritableDataImageFixture;

// Actual queued fake media on one B owner. No native, Linux, browser or reboot proof.
internal static unsafe class ReadOnlyWebRootWritableBlockTests
{
    private delegate bool Retain(Fs.Mount* mount, ulong epoch, ulong address, ulong bytes);
    private static readonly Retain CanRetain = typeof(Fs).GetMethod("CanRetainStorage", BindingFlags.NonPublic | BindingFlags.Static)!.CreateDelegate<Retain>();

    [HostTest("borrowed webroot reads independently extracted nested fragmented empty and bounded ranges on the same writable owner", HostLane.Native)]
    private static void OriginalFiles() => WithImage((raw, receipt) =>
    {
        using var f = new Fixture(raw, receipt); f.MountReady();
        HostCheck.True(receipt.Image.WebLast < receipt.Image.DataFirst && f.Block->SectorCount == receipt.Image.CapacitySectors &&
            f.PortInput.Device == f.Block && f.PortInput.Engine == f.Engine);
        foreach (string path in new[] { "/app/wwwroot/index.html", "/app/wwwroot/fixture/nested/info.txt", "/app/wwwroot/fixture/fragmented.bin", "/app/wwwroot/fixture/empty.bin" })
        {
            var expected = receipt.Image.WebFiles.Single(x => x.Path == path); var file = f.Open(path);
            HostCheck.Equal(expected.Bytes, file.Info.Length); byte[] content = expected.CopyBytes();
            foreach ((ulong offset, uint requested) in new[] { (0UL, 65536U), (511UL, 1032U), ((ulong)content.Length, 1U) })
            {
                if (offset > (ulong)content.Length) continue;
                uint wanted = (uint)Math.Min(requested, (ulong)content.Length - offset); byte[] output = Enumerable.Repeat((byte)0x91, (int)requested).ToArray();
                f.Read(file.File, offset, requested, output, wanted);
                HostCheck.True(output.AsSpan(0, (int)wanted).SequenceEqual(content.AsSpan((int)offset, (int)wanted)) &&
                    output.AsSpan((int)wanted).IndexOfAnyExcept((byte)0x91) < 0);
            }
            Fs.FileInspection facts = default; HostCheck.Equal(Fs.Status.Success, Fs.Inspect(f.Mount, file.File, &facts));
            HostCheck.True(facts.AllocationKnown && facts.AllocatedBlocks == (expected.Bytes + 511UL) / 512);
            HostCheck.Equal(Fs.Status.Success, Fs.Close(f.Mount, file.File));
        }
        ulong submitted = f.Block->Submitted; Fs.OperationToken token = default;
        HostCheck.Equal(Fs.Status.OutsideRoot, Fs.BeginOpen(f.Mount, "/data/LOG.TXT"u8, &token));
        HostCheck.True(!token.IsInitialized && f.Block->Submitted == submitted && f.Block->SubmittedBytes == 0 &&
            f.Observations.Where(x => x.Kind == B.Operation.Read && x.Request.Generation > f.MountedEngineGeneration).All(x => x.Sector >= receipt.Image.WebFirst && x.Sector <= receipt.Image.WebLast));
        Console.WriteLine($"WEBROOT_WRITABLE_LAYOUT mount={sizeof(Fs.Mount)} port={sizeof(P.State)} engine={sizeof(C.State)} fsBacking={f.FsBackingBytes} engineBacking={f.EngineBackingBytes}");
    });

    [HostTest("borrowed webroot admission stays unchanged while an actual engine request or held result owns the device", HostLane.Native)]
    private static void BusyAdmission() => WithImage((raw, receipt) =>
    {
        using var f = new Fixture(raw, receipt, start: false); C.RequestToken request = default;
        HostCheck.Equal(C.Status.Pending, C.BeginOpenRoot(f.Engine, "LOG.TXT", &request));
        byte[] before = f.CopyFsBacking(); ulong submitted = f.Block->Submitted;
        HostCheck.Equal(Fs.Status.Busy, Fs.StartBorrowed(f.Mount, f.Input));
        HostCheck.True(before.AsSpan().SequenceEqual(f.CopyFsBacking()) && f.Block->Submitted == submitted);
        f.RunEngine(); HostCheck.Equal(Fs.Status.Busy, Fs.StartBorrowed(f.Mount, f.Input));
        HostCheck.True(before.AsSpan().SequenceEqual(f.CopyFsBacking()));
        C.FileToken file = default; HostCheck.Equal(C.Status.Success, C.CopyOpen(f.Engine, request, &file));
        HostCheck.Equal(Fs.Status.Pending, Fs.StartBorrowed(f.Mount, f.Input)); f.MountReady();
    });

    [HostTest("borrowed webroot Busy preserves ready open read directory EOF and metadata across actual dirty OUT FLUSH and held writer result", HostLane.Native)]
    private static void WriterExclusion() => WithImage((raw, receipt) =>
    {
        using var f = new Fixture(raw, receipt); f.MountReady(); var file = f.Open("/app/wwwroot/index.html"); var directory = f.Open("/app/wwwroot");
        var read = f.BeginRead(file.File, 0, 7); HostCheck.Equal(Fs.Status.Ready, f.Drive(read));
        var eof = f.BeginRead(file.File, file.Info.Length, 1); HostCheck.Equal(Fs.Status.Ready, f.Drive(eof));
        var open = f.BeginOpen("/app/wwwroot/fixture/nested/info.txt"); HostCheck.Equal(Fs.Status.Ready, f.Drive(open));
        Fs.OperationToken scan = default; HostCheck.Equal(Fs.Status.Pending, Fs.BeginDirectory(f.Mount, directory.File, &scan)); HostCheck.Equal(Fs.Status.Entry, f.Drive(scan));
        var target = f.OpenData("FRAG.BIN"); C.RequestToken write = default;
        HostCheck.Equal(C.Status.Pending, C.BeginOverwrite(f.Engine, target, 0, "changed data"u8, &write));
        bool dirty = false; var kinds = new HashSet<B.Operation>(); byte[] output = Enumerable.Repeat((byte)0xb7, 32).ToArray();
        for (int turn = 0; turn < 10000; turn++)
        {
            ulong submitted = f.Block->Submitted; uint calls = f.Device.Hardware->Calls; uint count = 81;
            Fs.OpenResult opened = default; Fs.FileInspection info = default; Fs.Metadata entry = default; Fs.OperationToken extra = default;
            HostCheck.Equal(Fs.Status.Busy, Fs.PumpMount(f.Mount)); HostCheck.Equal(Fs.Status.Busy, Fs.Poll(f.Mount, read));
            HostCheck.Equal(Fs.Status.Busy, Fs.CopyRead(f.Mount, read, output, &count)); HostCheck.Equal(Fs.Status.Busy, Fs.CopyRead(f.Mount, eof, output, &count));
            HostCheck.Equal(Fs.Status.Busy, Fs.CopyOpen(f.Mount, open, &opened)); HostCheck.Equal(Fs.Status.Busy, Fs.CopyEntry(f.Mount, scan, &entry, output));
            HostCheck.Equal(Fs.Status.Busy, Fs.Inspect(f.Mount, file.File, &info)); HostCheck.Equal(Fs.Status.Busy, Fs.Close(f.Mount, directory.File));
            HostCheck.Equal(Fs.Status.Busy, Fs.BeginOpen(f.Mount, "/app/wwwroot/index.html"u8, &extra));
            HostCheck.True(count == 81 && !extra.IsInitialized && !opened.File.IsInitialized && output.AsSpan().IndexOfAnyExcept((byte)0xb7) < 0 &&
                f.Block->Submitted == submitted && f.Device.Hardware->Calls == calls);
            var status = f.EngineTurn(); dirty |= f.Engine->Dirty;
            if (f.Block->Submitted != submitted) kinds.Add(f.Device.Observation->Kind);
            if (status == C.Status.Ready) break;
            HostCheck.Equal(C.Status.Pending, status); HostCheck.True(turn < 9999);
        }
        HostCheck.True(dirty && kinds.SetEquals(new[] { B.Operation.Read, B.Operation.Write, B.Operation.Flush }));
        uint untouched = 81; HostCheck.Equal(Fs.Status.Busy, Fs.CopyRead(f.Mount, read, output, &untouched));
        C.Result result = default; HostCheck.Equal(C.Status.Success, C.CopyResult(f.Engine, write, &result)); HostCheck.Equal(12U, result.Bytes);
        uint copied = 0; HostCheck.Equal(Fs.Status.Success, Fs.CopyRead(f.Mount, read, output, &copied)); HostCheck.Equal(7U, copied);
        HostCheck.Equal(Fs.Status.EndOfFile, Fs.CopyRead(f.Mount, eof, output, &copied)); HostCheck.Equal(0U, copied);
        Fs.OpenResult value = default; HostCheck.Equal(Fs.Status.Success, Fs.CopyOpen(f.Mount, open, &value));
        HostCheck.Equal(Fs.Status.Cancelled, Fs.Cancel(f.Mount, scan));
    });

    [HostTest("borrowed filesystem pending and held physical IN reject engine admission until the original range copy or cancellation drains", HostLane.Native)]
    private static void ReaderExclusion() => WithImage((raw, receipt) =>
    {
        foreach (bool held in new[] { false, true })
        {
            using var f = new Fixture(raw, receipt); f.MountReady(); var file = f.Open("/app/wwwroot/fixture/fragmented.bin"); var read = f.BeginRead(file.File, 509, 7);
            f.WaitForPort(read); if (held) f.HoldPort(); ulong submitted = f.Block->Submitted; uint calls = f.Device.Hardware->Calls; C.RequestToken request = default;
            HostCheck.Equal(C.Status.Busy, C.BeginFlush(f.Engine, &request)); HostCheck.Equal(C.Status.Busy, C.BeginInspectRoot(f.Engine, &request));
            HostCheck.Equal(C.Status.Busy, C.Stop(f.Engine)); HostCheck.Equal(C.Status.Idle, C.Pump(f.Engine));
            HostCheck.True(request.Owner == 0 && f.Block->Submitted == submitted && f.Device.Hardware->Calls == calls);
            byte[] output = new byte[7]; HostCheck.Equal(Fs.Status.Ready, f.Drive(read)); uint bytes = 0;
            HostCheck.Equal(Fs.Status.Success, Fs.CopyRead(f.Mount, read, output, &bytes));
            HostCheck.True(bytes == 7 && output.AsSpan().SequenceEqual(receipt.Image.WebFiles.Single(x => x.Path.EndsWith("/fragmented.bin", StringComparison.Ordinal)).CopyBytes().AsSpan(509, 7)));
            HostCheck.Equal(C.Status.Pending, C.BeginFlush(f.Engine, &request)); f.RunEngine(); C.Result flushed = default;
            HostCheck.Equal(C.Status.Success, C.CopyResult(f.Engine, request, &flushed));
        }
    });

    [HostTest("borrowed web cache survives real data growth overwrite and FLUSH while independent extraction proves untouched whole ESP", HostLane.Native)]
    private static void DataMutation() => WithImage((raw, receipt) =>
    {
        using var f = new Fixture(raw, receipt); f.MountReady(); string path = "/app/wwwroot/fixture/fragmented.bin"; var file = f.Open(path);
        byte[] web = receipt.Image.WebFiles.Single(x => x.Path == path).CopyBytes(), readback = new byte[1032]; f.Read(file.File, 4094, 1032, readback, 1032);
        var log = f.OpenData("LOG.TXT"); uint required = 0; HostCheck.Equal(C.Status.Success, C.GrowthCandidatesRequired(f.Engine, log, 1032, &required));
        HostCheck.True(required is > 0 and <= 16); uint[] candidates = new uint[16]; C.FreeCursor cursor = default; C.RequestToken request = default; C.FreeResult free = default;
        HostCheck.Equal(C.Status.Success, C.CreateFreeCursor(f.Engine, 2, &cursor));
        HostCheck.Equal(C.Status.Pending, C.BeginFindFree(f.Engine, cursor, required, 4096, 128, &request)); f.RunEngine();
        HostCheck.Equal(C.Status.Success, C.CopyFree(f.Engine, request, candidates, &free)); HostCheck.Equal(required, free.Count);
        HostCheck.Equal(C.Status.Pending, C.BeginGrow(f.Engine, log, 1032, candidates.AsSpan(0, (int)required), &request)); f.RunEngine(); C.MutationResult grown = default;
        HostCheck.Equal(C.Status.Success, C.CopyMutationResult(f.Engine, request, &grown)); HostCheck.True(grown.NewFile.Length == 1032 && !grown.Deleted);
        byte[] payload = Enumerable.Range(0, 1032).Select(i => (byte)((i * 29) ^ (i >> 3))).ToArray();
        HostCheck.Equal(C.Status.Pending, C.BeginOverwrite(f.Engine, grown.NewFile, 0, payload, &request)); f.RunEngine(); C.Result changed = default;
        HostCheck.Equal(C.Status.Success, C.CopyResult(f.Engine, request, &changed)); HostCheck.Equal(1032U, changed.Bytes);
        HostCheck.Equal(C.Status.Pending, C.BeginFlush(f.Engine, &request)); f.RunEngine(); HostCheck.Equal(C.Status.Success, C.CopyResult(f.Engine, request, &changed));
        f.Read(file.File, 4094, 1032, readback, 1032); HostCheck.True(readback.AsSpan().SequenceEqual(web.AsSpan(4094, 1032)));
        var actual = Data.InspectPartition(f.Persistent, receipt.Image.DataFirst, receipt.Image.DataLast);
        HostCheck.True(actual.Find("LOG.TXT")!.CopyBytes().AsSpan().SequenceEqual(payload) &&
            Hash(f.Persistent.Slice((int)receipt.Image.WebFirst * 512, checked((int)(receipt.Image.WebLast - receipt.Image.WebFirst + 1) * 512))) == receipt.Image.WebPartitionSha256 &&
            f.Block->SubmittedBytes > 0 && f.Block->DurableBytes == f.Block->CompletedBytes);
        HostCheck.True(f.Observations.Where(x => x.Kind is B.Operation.Write or B.Operation.Flush).All(x => x.Kind == B.Operation.Flush || x.Sector >= receipt.Image.DataFirst && x.Sector <= receipt.Image.DataLast));
    });

    [HostTest("borrowed filesystem cancel and Stop revoke pending or held reads and release only local backing without reset", HostLane.Native)]
    private static void CancelAndStop() => WithImage((raw, receipt) =>
    {
        foreach (bool held in new[] { false, true }) foreach (bool stop in new[] { false, true })
        {
            using var f = new Fixture(raw, receipt); f.MountReady(); var file = f.Open("/app/wwwroot/index.html"); var read = f.BeginRead(file.File, 0, 7);
            f.WaitForPort(read); if (held) f.HoldPort(); uint resets = f.Device.Hardware->Resets; ulong submitted = f.Block->Submitted;
            var status = stop ? Fs.Stop(f.Mount) : Fs.Cancel(f.Mount, read); f.Sample();
            HostCheck.Equal(held ? stop ? Fs.Status.Success : Fs.Status.Cancelled : Fs.Status.Pending, status);
            if (!held)
            {
                f.Device.Complete(); status = stop ? Fs.Stop(f.Mount) : Fs.Poll(f.Mount, read); f.Sample();
                HostCheck.Equal(stop ? Fs.Status.Success : Fs.Status.Cancelled, status);
            }
            byte[] output = Enumerable.Repeat((byte)0xd4, 7).ToArray(); uint bytes = 91;
            HostCheck.Equal(stop ? Fs.Status.InvalidState : Fs.Status.StaleToken, Fs.CopyRead(f.Mount, read, output, &bytes));
            HostCheck.True(bytes == 91 && output.AsSpan().IndexOfAnyExcept((byte)0xd4) < 0 && !f.Port->RequestActive && !f.Port->Retired &&
                f.Engine->IsMounted && f.Block->Running && f.Block->Submitted == submitted && f.Device.Hardware->Resets == resets);
        }
    });

    [HostTest("borrowed filesystem complete retained tree rejects aliases and dynamic observer overlap before output or IO", HostLane.Native)]
    private static void StorageAliases() => WithImage((raw, receipt) =>
    {
        using var f = new Fixture(raw, receipt, start: false); byte[] before = f.CopyFsBacking();
        foreach (ulong address in new[] { (ulong)f.Port, (ulong)f.Engine, (ulong)f.EngineInput.Before, (ulong)f.Block,
            (ulong)f.Device.Pool, (ulong)f.Device.Slot, (ulong)f.Device.Data, (ulong)f.Device.Observation, 0x400000UL, 0x402000UL, 0x403000UL })
        {
            byte* saved = f.Operations[0].Storage.Range; f.Operations[0].Storage.Range = (byte*)address;
            HostCheck.Equal(Fs.Status.InvalidAccess, Fs.StartBorrowed(f.Mount, f.Input)); f.Operations[0].Storage.Range = saved;
            HostCheck.True(before.AsSpan().SequenceEqual(f.CopyFsBacking()));
        }
        HostCheck.Equal(Fs.Status.Pending, Fs.StartBorrowed(f.Mount, f.Input)); f.MountReady(); var file = f.Open("/app/wwwroot/index.html");
        var read = f.BeginRead(file.File, 0, 7); HostCheck.Equal(Fs.Status.Ready, f.Drive(read));
        foreach (var extent in f.FsExtents())
            HostCheck.True(!CanRetain(f.Mount, f.Input.Epoch, extent.Start, extent.Bytes) && !CanRetain(f.Mount, f.Input.Epoch, extent.Start - 8, extent.Bytes + 16) &&
                !CanRetain(f.Mount, f.Input.Epoch, extent.Start + extent.Bytes - 1, 2));
        HostCheck.True(!CanRetain(f.Mount, f.Input.Epoch, ulong.MaxValue - 7, 64) && !CanRetain(f.Mount, f.Input.Epoch + 1, (ulong)f.Output, 128) &&
            CanRetain(f.Mount, f.Input.Epoch, (ulong)f.Output, 128));
        B.Admission* blockInput = (B.Admission*)((byte*)f.Block + Offset<B.State>("Input")); B.Observation** slot = &blockInput->Observation; B.Observation* original = *slot;
        byte[] output = Enumerable.Repeat((byte)0xea, 7).ToArray(); uint bytes = 55; uint calls = f.Device.Hardware->Calls;
        try
        {
            *slot = (B.Observation*)f.Operations[0].Storage.Range;
            HostCheck.True(!CanRetain(f.Mount, f.Input.Epoch, (ulong)f.Output, 128));
            HostCheck.Equal(Fs.Status.DeviceFailed, Fs.CopyRead(f.Mount, read, output, &bytes));
            HostCheck.True(bytes == 55 && output.AsSpan().IndexOfAnyExcept((byte)0xea) < 0 && f.Device.Hardware->Calls == calls);
        }
        finally { *slot = original; }
        HostCheck.Equal(Fs.Status.Success, Fs.CopyRead(f.Mount, read, output, &bytes)); HostCheck.Equal(7U, bytes);
    });

    [HostTest("borrowed filesystem actual IN error prevents late output and retires locally only after original port cleanup", HostLane.Native)]
    private static void FailedRead() => WithImage((raw, receipt) =>
    {
        using var f = new Fixture(raw, receipt); f.MountReady(); var file = f.Open("/app/wwwroot/index.html"); var read = f.BeginRead(file.File, 0, 7);
        f.WaitForPort(read); f.Device.Complete(status: 1); HostCheck.Equal(Fs.Status.DeviceFailed, Fs.Poll(f.Mount, read)); f.Sample();
        byte[] output = Enumerable.Repeat((byte)0x78, 7).ToArray(); uint bytes = 19;
        HostCheck.Equal(Fs.Status.DeviceFailed, Fs.CopyRead(f.Mount, read, output, &bytes)); HostCheck.True(bytes == 19 && output.AsSpan().IndexOfAnyExcept((byte)0x78) < 0);
        HostCheck.Equal(Fs.Status.DeviceFailed, Fs.Stop(f.Mount)); HostCheck.True(f.Mount->RequiresCleanup && !f.Port->Retired);
        HostCheck.Equal(B.Status.Success, B.Stop(f.Block)); HostCheck.Equal(P.Status.Success, P.Stop(f.Port));
        HostCheck.Equal(Fs.Status.Success, Fs.Stop(f.Mount)); HostCheck.True(f.Mount->IsRetired && !CanRetain(f.Mount, f.Input.Epoch, (ulong)f.Output, 128));
    });

    [HostTest("borrowed filesystem retired port permits only local teardown after original engine release without dereferencing replacement backing", HostLane.Native)]
    private static void RetiredPort() => WithImage((raw, receipt) =>
    {
        using var f = new Fixture(raw, receipt); f.MountReady(); var file = f.Open("/app/wwwroot/index.html");
        HostCheck.Equal(P.Status.Success, P.Stop(f.Port)); HostCheck.Equal(C.Status.Success, C.Stop(f.Engine));
        byte* replacement = (byte*)NativeMemory.AlignedAlloc(4096, 4096); new Span<byte>(replacement, 4096).Clear();
        ref P.Admission input = ref PortAdmission(ref *f.Port); P.Admission original = input;
        try
        {
            // Mapped but unadmitted zero states stand in for released original
            // backing. Their fields cannot authenticate any active C/B owner.
            input.Engine = (C.State*)replacement; input.Device = (B.State*)(replacement + 1024);
            Fs.FileInspection facts = default; new Span<byte>(&facts, sizeof(Fs.FileInspection)).Fill(0x71);
            HostCheck.Equal(Fs.Status.DeviceFailed, Fs.Inspect(f.Mount, file.File, &facts));
            HostCheck.True(new ReadOnlySpan<byte>(&facts, sizeof(Fs.FileInspection)).IndexOfAnyExcept((byte)0x71) < 0 &&
                !CanRetain(f.Mount, f.Input.Epoch, (ulong)f.Output, 128));
            HostCheck.Equal(Fs.Status.Success, Fs.Stop(f.Mount)); HostCheck.Equal(Fs.Status.Success, Fs.Stop(f.Mount));
            HostCheck.True(f.Mount->IsRetired && new ReadOnlySpan<byte>(replacement, 4096).IndexOfAnyExcept((byte)0) < 0 && f.Block->Running);
        }
        finally { input = original; NativeMemory.AlignedFree(replacement); }
    });

    [HostTest("borrowed local close releases only unreferenced handle metadata through actual writer dirty OUT FLUSH and held Ready", HostLane.Native)]
    private static void CloseDuringWriter() => WithImage((raw, receipt) =>
    {
        using var f = new Fixture(raw, receipt); f.MountReady(); Fs.FileToken[] handles = new Fs.FileToken[4];
        for (int i = 0; i < handles.Length; i++) handles[i] = f.Open("/app/wwwroot/index.html").File;
        var target = f.OpenData("FRAG.BIN"); C.RequestToken request = default;
        HostCheck.Equal(C.Status.Pending, C.BeginOverwrite(f.Engine, target, 0, "close only"u8, &request)); CloseOnly(f, handles[0]);
        bool dirty = false, flush = false;
        for (int i = 0; i < 10000; i++)
        {
            var status = f.EngineTurn();
            if (!dirty && f.Engine->Dirty && f.Block->LastOperation == B.Operation.Write) { CloseOnly(f, handles[1]); dirty = true; }
            if (!flush && f.Block->RequestPending && f.Block->LastOperation == B.Operation.Flush) { CloseOnly(f, handles[2]); flush = true; }
            if (status == C.Status.Ready) break;
            HostCheck.Equal(C.Status.Pending, status); HostCheck.True(i < 9999);
        }
        HostCheck.True(dirty && flush && f.Engine->IsReady && f.Engine->RequestActive); CloseOnly(f, handles[3]);
        C.Result result = default; HostCheck.Equal(C.Status.Success, C.CopyResult(f.Engine, request, &result)); HostCheck.Equal(10U, result.Bytes);
        var replacement = f.Open("/app/wwwroot/fixture/nested/info.txt");
        HostCheck.Equal(Fs.Status.StaleToken, Fs.Close(f.Mount, handles[0])); byte[] expected = receipt.Image.WebFiles.Single(x => x.Path.EndsWith("/nested/info.txt", StringComparison.Ordinal)).CopyBytes();
        byte[] output = new byte[expected.Length]; f.Read(replacement.File, 0, (uint)output.Length, output, (uint)output.Length); HostCheck.True(output.AsSpan().SequenceEqual(expected));
    });

    [HostTest("borrowed local close retains active pending held and ready range references while unrelated handles close without physical cancellation", HostLane.Native)]
    private static void ReferencedClose() => WithImage((raw, receipt) =>
    {
        foreach (bool held in new[] { false, true })
        {
            using var f = new Fixture(raw, receipt); f.MountReady(); var active = f.Open("/app/wwwroot/index.html"); var other = f.Open("/app/wwwroot/index.html");
            var read = f.BeginRead(active.File, 0, 7); f.WaitForPort(read); if (held) f.HoldPort();
            uint calls = f.Device.Hardware->Calls; ulong submitted = f.Block->Submitted;
            HostCheck.Equal(Fs.Status.Busy, Fs.Close(f.Mount, active.File)); CloseOnly(f, other.File);
            HostCheck.True(f.Port->RequestActive && f.Device.Hardware->Calls == calls && f.Block->Submitted == submitted);
            HostCheck.Equal(Fs.Status.Ready, f.Drive(read)); HostCheck.Equal(Fs.Status.Busy, Fs.Close(f.Mount, active.File));
            var data = f.OpenData("FRAG.BIN"); C.RequestToken writer = default; HostCheck.Equal(C.Status.Pending, C.BeginOverwrite(f.Engine, data, 0, "guard"u8, &writer));
            HostCheck.Equal(Fs.Status.Busy, Fs.Close(f.Mount, active.File)); HostCheck.Equal(Fs.Status.Cancelled, Fs.Cancel(f.Mount, read)); CloseOnly(f, active.File);
            f.RunEngine(); C.Result result = default; HostCheck.Equal(C.Status.Success, C.CopyResult(f.Engine, writer, &result));
        }
    });

    [HostTest("borrowed local Busy close rejects altered epochs aliases and actual stopped device before any handle mutation", HostLane.Native)]
    private static void CloseAuthority() => WithImage((raw, receipt) =>
    {
        using var f = new Fixture(raw, receipt); f.MountReady(); var file = f.Open("/app/wwwroot/index.html"); var data = f.OpenData("FRAG.BIN"); C.RequestToken writer = default;
        HostCheck.Equal(C.Status.Pending, C.BeginOverwrite(f.Engine, data, 0, "guard"u8, &writer));
        uint index = HandleIndex(ref file.File); byte[] original = new ReadOnlySpan<byte>(f.Input.Files + index, sizeof(Fs.FileHandle)).ToArray();
        ref P.Admission port = ref PortAdmission(ref *f.Port); ulong epoch = port.Epoch;
        try { port.Epoch++; HostCheck.Equal(Fs.Status.DeviceFailed, Fs.Close(f.Mount, file.File)); }
        finally { port.Epoch = epoch; }
        ref B.Admission block = ref BlockAdmission(ref *f.Block); B.Observation* observation = block.Observation;
        try { block.Observation = (B.Observation*)f.Input.Files; HostCheck.Equal(Fs.Status.DeviceFailed, Fs.Close(f.Mount, file.File)); }
        finally { block.Observation = observation; }
        HostCheck.True(original.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(f.Input.Files + index, sizeof(Fs.FileHandle))));
        f.RunEngine(); C.Result result = default; HostCheck.Equal(C.Status.Success, C.CopyResult(f.Engine, writer, &result));
        HostCheck.Equal(B.Status.Success, B.Stop(f.Block)); HostCheck.Equal(Fs.Status.DeviceFailed, Fs.Close(f.Mount, file.File));
        HostCheck.True(original.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(f.Input.Files + index, sizeof(Fs.FileHandle))));
        HostCheck.Equal(P.Status.Success, P.Stop(f.Port)); HostCheck.Equal(Fs.Status.Success, Fs.Stop(f.Mount));
    });

    [HostTest("borrowed local Busy close cannot adopt actual same-address restarted device and retains old metadata unchanged", HostLane.Native)]
    private static void CloseAba() => WithImage((raw, receipt) =>
    {
        using var f = new Fixture(raw, receipt); f.MountReady(); var file = f.Open("/app/wwwroot/index.html"); var data = f.OpenData("FRAG.BIN"); C.RequestToken writer = default;
        HostCheck.Equal(C.Status.Pending, C.BeginOverwrite(f.Engine, data, 0, "never issued"u8, &writer));
        byte[] handles = new ReadOnlySpan<byte>(f.Input.Files, 4 * sizeof(Fs.FileHandle)).ToArray();
        HostCheck.Equal(B.Status.Success, B.Stop(f.Block)); f.AllowStaleBacking = true; new Span<byte>(f.Block, sizeof(B.State)).Clear();
        var replacement = f.Device.Input; replacement.Epoch += 0x10000; replacement.Negotiation = f.Device.Negotiate();
        HostCheck.Equal(B.Status.Success, B.Start(f.Block, replacement));
        uint calls = f.Device.Hardware->Calls; HostCheck.Equal(Fs.Status.DeviceFailed, Fs.Close(f.Mount, file.File));
        HostCheck.True(handles.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(f.Input.Files, 4 * sizeof(Fs.FileHandle))) && f.Device.Hardware->Calls == calls &&
            f.Engine->RequestActive && !f.Engine->Dirty && !f.Mount->IsRetired && !f.Port->Retired);
        // Old backing remains held. Stopping the replacement is not a logical
        // retirement receipt for the violated original C/P/FS lifetime.
        HostCheck.Equal(B.Status.Success, B.Stop(f.Block));
    });

    private static void CloseOnly(Fixture f, Fs.FileToken token)
    {
        List<(ulong Start, ulong Bytes)> protectedRanges = [((ulong)f.Mount, (ulong)sizeof(Fs.Mount)), ((ulong)f.Port, (ulong)sizeof(P.State)),
            ((ulong)f.Engine, (ulong)sizeof(C.State)), ((ulong)f.Block, (ulong)sizeof(B.State)), ((ulong)f.Device.Pool, (ulong)sizeof(OS.Drivers.DmaBufferPool)),
            ((ulong)f.Device.Slot, (ulong)sizeof(OS.Drivers.DmaBufferSlot)), ((ulong)f.Device.Data, 4096), ((ulong)f.Device.Observation, (ulong)sizeof(B.Observation)),
            ((ulong)f.EngineInput.Changes, C.JournalChanges * (ulong)sizeof(Fat32Mutation.Change)), ((ulong)f.EngineInput.Before, 65536), ((ulong)f.EngineInput.After, 65536),
            ((ulong)f.EngineInput.DirectoryMap, 16384), ((ulong)f.EngineInput.FileMap, 16384), ((ulong)f.EngineInput.WalkMap, 16384),
            ((ulong)f.EngineInput.Sector, 512), ((ulong)f.EngineInput.Fat0, 512), ((ulong)f.EngineInput.Fat1, 512), ((ulong)f.EngineInput.Input, C.WriteBytes),
            ((ulong)f.EngineInput.Name, C.NameWords * 2UL), ((ulong)f.EngineInput.LongName, C.VfatWords * 2UL), ((ulong)f.EngineInput.EncodedEntries, C.EncodedEntryBytes),
            ((ulong)f.Input.Operations, 4UL * (ulong)sizeof(Fs.Operation)), ((ulong)f.Input.Cache, 2UL * (ulong)sizeof(Fs.CacheSlot))];
        for (int i = 0; i < 4; i++) protectedRanges.Add(((ulong)f.Input.Files[i].Clusters, 16384));
        for (int i = 0; i < 2; i++) protectedRanges.Add(((ulong)f.Input.Cache[i].Data, 512));
        byte[][] before = protectedRanges.Select(x => new ReadOnlySpan<byte>((void*)x.Start, (int)x.Bytes).ToArray()).ToArray();
        Fs.FileHandle* expected = stackalloc Fs.FileHandle[4]; new ReadOnlySpan<Fs.FileHandle>(f.Input.Files, 4).CopyTo(new Span<Fs.FileHandle>(expected, 4));
        uint index = HandleIndex(ref token); HandlePhase(ref expected[index]) = 0; HandleClusters(ref expected[index]) = 0;
        uint calls = f.Device.Hardware->Calls; HostCheck.Equal(Fs.Status.Success, Fs.Close(f.Mount, token));
        HostCheck.True(f.Device.Hardware->Calls == calls && new ReadOnlySpan<byte>(expected, 4 * sizeof(Fs.FileHandle)).SequenceEqual(new ReadOnlySpan<byte>(f.Input.Files, 4 * sizeof(Fs.FileHandle))));
        for (int i = 0; i < protectedRanges.Count; i++) HostCheck.True(before[i].AsSpan().SequenceEqual(new ReadOnlySpan<byte>((void*)protectedRanges[i].Start, (int)protectedRanges[i].Bytes)), "close changed protected extent " + i);
        HostCheck.Equal(Fs.Status.StaleToken, Fs.Close(f.Mount, token));
    }

    private static void WithImage(Action<byte[], Image.Receipt> action) => CombinedWebDataImageFixtureTests.WithImage((_, _, _, _, path, receipt) => action(File.ReadAllBytes(path), receipt));
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static int Offset<T>(string name) where T : struct => (int)Marshal.OffsetOf<T>(name);
    // Marshal.OffsetOf describes marshalled bool padding, not this POD's managed
    // field layout. Borrow the actual retained token rather than guessing its offset.
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "PortIo")]
    private static extern ref P.Token BorrowedToken(ref Fs.Mount mount);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "Input")]
    private static extern ref P.Admission PortAdmission(ref P.State state);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "Input")]
    private static extern ref B.Admission BlockAdmission(ref B.State state);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "Index")]
    private static extern ref uint HandleIndex(ref Fs.FileToken token);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "Phase")]
    private static extern ref byte HandlePhase(ref Fs.FileHandle handle);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "ClusterCount")]
    private static extern ref uint HandleClusters(ref Fs.FileHandle handle);

    internal sealed class Fixture : IDisposable
    {
        internal readonly WritableBlockSessionTests.Fixture Device;
        internal B.State* Block => Device.State;
        internal readonly C.State* Engine = (C.State*)NativeMemory.AllocZeroed((nuint)sizeof(C.State));
        internal readonly P.State* Port = (P.State*)NativeMemory.AllocZeroed((nuint)sizeof(P.State));
        private readonly byte* engineArena = (byte*)NativeMemory.AlignedAlloc(208896, 4096);
        private readonly byte* fsArena = (byte*)NativeMemory.AlignedAlloc(524288, 4096);
        internal readonly byte* Output = (byte*)NativeMemory.AllocZeroed(128);
        internal readonly Fs.Mount* Mount;
        internal readonly Fs.Operation* Operations;
        private readonly Fs.FileHandle* files;
        private readonly Fs.CacheSlot* cache;
        internal readonly C.Admission EngineInput;
        internal readonly P.Admission PortInput;
        internal readonly Fs.BorrowedAdmission Input;
        internal readonly ulong EngineBackingBytes, FsBackingBytes, MountedEngineGeneration;
        internal readonly List<B.Observation> Observations = [];
        internal bool AllowStaleBacking;
        internal Span<byte> Persistent => new(Device.Hardware->Persistent, 128 * 1024 * 1024);
        internal Fixture(byte[] raw, Image.Receipt receipt, bool start = true)
        {
            Device = new(receipt.Image.CapacitySectors, observe: true); Device.UseMedium(raw); HostCheck.Equal(B.Status.Success, Device.Start());
            new Span<byte>(engineArena, 208896).Fill(0xa7); ulong at = (ulong)engineArena + 4096;
            void* EngineTake(ulong bytes) { at = (at + 15) & ~15UL; ulong value = at; at += bytes; return (void*)value; }
            EngineInput = new() { Device = Block, Owner = 719, Epoch = 720, PartitionFirst = receipt.Image.DataFirst, PartitionLast = receipt.Image.DataLast,
                ChangeCapacity = C.JournalChanges, ChainCapacity = C.MaximumClusters, InputCapacity = C.WriteBytes, WorkBudget = 100000,
                Changes = (Fat32Mutation.Change*)EngineTake(C.JournalChanges * (ulong)sizeof(Fat32Mutation.Change)), Before = (byte*)EngineTake(C.JournalChanges * 512UL), After = (byte*)EngineTake(C.JournalChanges * 512UL),
                DirectoryMap = (uint*)EngineTake(C.MaximumClusters * 4UL), FileMap = (uint*)EngineTake(C.MaximumClusters * 4UL), WalkMap = (uint*)EngineTake(C.MaximumClusters * 4UL),
                Sector = (byte*)EngineTake(512), Fat0 = (byte*)EngineTake(512), Fat1 = (byte*)EngineTake(512), Input = (byte*)EngineTake(C.WriteBytes),
                Name = (ushort*)EngineTake(C.NameWords * 2UL), LongName = (ushort*)EngineTake(C.VfatWords * 2UL), EncodedEntries = (byte*)EngineTake(C.EncodedEntryBytes) };
            EngineBackingBytes = at - ((ulong)engineArena + 4096); HostCheck.True(EngineBackingBytes < 200704); new Span<byte>(engineArena + 4096, (int)EngineBackingBytes).Clear();
            HostCheck.Equal(C.Status.Pending, C.Start(Engine, EngineInput)); RunEngine(); MountedEngineGeneration = Block->Submitted;
            PortInput = new() { Engine = Engine, Device = Block, Epoch = 721, EngineEpoch = EngineInput.Epoch, DeviceEpoch = Device.Input.Epoch, FirstLba = receipt.Image.WebFirst, LastLba = receipt.Image.WebLast };
            HostCheck.Equal(P.Status.Success, P.Start(Port, PortInput));
            new Span<byte>(fsArena, 524288).Fill(0xb9); at = (ulong)fsArena + 4096;
            void* FsTake(ulong bytes) { at = (at + 15) & ~15UL; ulong value = at; at += bytes; new Span<byte>((void*)value, (int)bytes).Clear(); return (void*)value; }
            Mount = (Fs.Mount*)FsTake((ulong)sizeof(Fs.Mount)); Operations = (Fs.Operation*)FsTake(4UL * (ulong)sizeof(Fs.Operation));
            files = (Fs.FileHandle*)FsTake(4UL * (ulong)sizeof(Fs.FileHandle)); cache = (Fs.CacheSlot*)FsTake(2UL * (ulong)sizeof(Fs.CacheSlot));
            for (int i = 0; i < 4; i++)
            { Operations[i].Storage = new() { Path = (byte*)FsTake(256), Sector = (byte*)FsTake(512), Name = (ushort*)FsTake(520), History = (uint*)FsTake(4096 * 4), HistoryCapacity = 4096, Range = (byte*)FsTake(65536), RangeCapacity = 65536 };
                files[i].Clusters = (uint*)FsTake(4096 * 4); files[i].ClusterCapacity = 4096; }
            for (int i = 0; i < 2; i++) cache[i].Data = (byte*)FsTake(512);
            FsBackingBytes = at - ((ulong)fsArena + 4096); HostCheck.True(FsBackingBytes < 516096);
            Input = new() { ReadPort = Port, PortEpoch = PortInput.Epoch, Epoch = 722, Operations = Operations, Files = files, Cache = cache,
                OperationCount = 4, FileCount = 4, CacheCount = 2, DirectoryEntryBudget = 100000 };
            if (start) HostCheck.Equal(Fs.Status.Pending, Fs.StartBorrowed(Mount, Input));
        }
        internal void Sample() { if (Device.Observation->Request.Generation != 0) Observations.Add(*Device.Observation); }
        internal C.Status EngineTurn() { var status = C.Pump(Engine); Sample(); if (Device.Hardware->Pending) Device.Complete(); return status; }
        internal void RunEngine() { for (int i = 0; i < 100000; i++) { var status = EngineTurn(); if (status == C.Status.Ready) return; HostCheck.Equal(C.Status.Pending, status); } throw new InvalidOperationException("engine_bound"); }
        internal C.FileToken OpenData(string name)
        { C.RequestToken token = default; HostCheck.Equal(C.Status.Pending, C.BeginOpenRoot(Engine, name, &token)); RunEngine(); C.FileToken result = default; HostCheck.Equal(C.Status.Success, C.CopyOpen(Engine, token, &result)); return result; }
        internal void MountReady()
        { for (int i = 0; i < 100000; i++) { if (Device.Hardware->Pending) Device.Complete(); var status = Fs.PumpMount(Mount); Sample(); if (status == Fs.Status.Ready) return; HostCheck.Equal(Fs.Status.Pending, status); } throw new InvalidOperationException("mount_bound"); }
        internal Fs.OperationToken BeginOpen(string path) { Fs.OperationToken token = default; HostCheck.Equal(Fs.Status.Pending, Fs.BeginOpen(Mount, Encoding.ASCII.GetBytes(path), &token)); return token; }
        internal Fs.OpenResult Open(string path) { var token = BeginOpen(path); HostCheck.Equal(Fs.Status.Ready, Drive(token)); Fs.OpenResult result = default; HostCheck.Equal(Fs.Status.Success, Fs.CopyOpen(Mount, token, &result)); return result; }
        internal Fs.OperationToken BeginRead(Fs.FileToken file, ulong offset, uint bytes) { Fs.OperationToken token = default; HostCheck.Equal(Fs.Status.Pending, Fs.BeginRead(Mount, file, offset, bytes, &token)); return token; }
        internal Fs.Status Drive(Fs.OperationToken token)
        { for (int i = 0; i < 100000; i++) { if (Device.Hardware->Pending) Device.Complete(); var status = Fs.Poll(Mount, token); Sample(); if (status is not (Fs.Status.Pending or Fs.Status.Busy)) return status; } throw new InvalidOperationException("fs_bound"); }
        internal void Read(Fs.FileToken file, ulong offset, uint requested, Span<byte> output, uint wanted)
        { var token = BeginRead(file, offset, requested); HostCheck.Equal(Fs.Status.Ready, Drive(token)); uint bytes = 99; HostCheck.Equal(wanted == 0 ? Fs.Status.EndOfFile : Fs.Status.Success, Fs.CopyRead(Mount, token, output, &bytes)); HostCheck.Equal(wanted, bytes); }
        internal void WaitForPort(Fs.OperationToken token)
        { for (int i = 0; i < 10000 && !Port->RequestActive; i++) { var status = Fs.Poll(Mount, token); Sample(); HostCheck.Equal(Fs.Status.Pending, status); } HostCheck.True(Port->RequestActive && Device.Hardware->Pending); }
        internal void HoldPort() { Device.Complete(); P.Completion result = default; var token = BorrowedToken(ref *Mount); HostCheck.Equal(P.Status.Ready, P.Poll(Port, token, &result)); Sample(); }
        internal byte[] CopyFsBacking() => new ReadOnlySpan<byte>(fsArena + 4096, (int)FsBackingBytes).ToArray();
        internal IEnumerable<(ulong Start, ulong Bytes)> FsExtents()
        {
            List<(ulong, ulong)> result = [((ulong)Mount, (ulong)sizeof(Fs.Mount)), ((ulong)Operations, 4UL * (ulong)sizeof(Fs.Operation)), ((ulong)files, 4UL * (ulong)sizeof(Fs.FileHandle)), ((ulong)cache, 2UL * (ulong)sizeof(Fs.CacheSlot))];
            for (int i = 0; i < 4; i++) { var s = Operations[i].Storage; result.Add(((ulong)s.Path, 256)); result.Add(((ulong)s.Sector, 512)); result.Add(((ulong)s.Name, 520)); result.Add(((ulong)s.History, 16384)); result.Add(((ulong)s.Range, 65536)); result.Add(((ulong)files[i].Clusters, 16384)); }
            for (int i = 0; i < 2; i++) result.Add(((ulong)cache[i].Data, 512)); return result;
        }
        public void Dispose()
        {
            if (AllowStaleBacking)
            {
                // Negative supervisor-ABA test only: retain every old object to
                // this fake-owner boundary, never relabel it logically retired.
                if (Block->RequiresCleanup) HostCheck.Equal(B.Status.Success, B.Stop(Block));
                Device.CheckGuards(); Device.Dispose(); NativeMemory.Free(Engine); NativeMemory.Free(Port); NativeMemory.Free(Output); NativeMemory.AlignedFree(engineArena); NativeMemory.AlignedFree(fsArena); return;
            }
            if (Mount->RequiresCleanup) { var status = Fs.Stop(Mount); Sample(); if (status == Fs.Status.Pending) { if (Device.Hardware->Pending) Device.Complete(); HostCheck.Equal(Fs.Status.Success, Fs.Stop(Mount)); Sample(); } }
            if (!Port->Retired) { var status = P.Stop(Port); Sample(); if (status == P.Status.Pending) { if (Device.Hardware->Pending) Device.Complete(); P.Completion result = default; var token = BorrowedToken(ref *Mount); P.Poll(Port, token, &result); Sample(); status = P.Stop(Port); } if (status != P.Status.Success) { B.Stop(Block); HostCheck.Equal(P.Status.Success, P.Stop(Port)); } }
            if (Mount->RequiresCleanup) HostCheck.Equal(Fs.Status.Success, Fs.Stop(Mount));
            if (Engine->IsMounted && !Engine->Dirty) { var status = C.Stop(Engine); if (status != C.Status.Success) { B.Stop(Block); HostCheck.Equal(C.Status.Success, C.Stop(Engine)); } }
            if (Block->RequiresCleanup) HostCheck.Equal(B.Status.Success, B.Stop(Block));
            HostCheck.True(new ReadOnlySpan<byte>(engineArena, 4096).IndexOfAnyExcept((byte)0xa7) < 0 && new ReadOnlySpan<byte>(engineArena + 204800, 4096).IndexOfAnyExcept((byte)0xa7) < 0 &&
                new ReadOnlySpan<byte>(fsArena, 4096).IndexOfAnyExcept((byte)0xb9) < 0 && new ReadOnlySpan<byte>(fsArena + 520192, 4096).IndexOfAnyExcept((byte)0xb9) < 0);
            Device.CheckGuards(); Device.Dispose(); NativeMemory.Free(Engine); NativeMemory.Free(Port); NativeMemory.Free(Output); NativeMemory.AlignedFree(engineArena); NativeMemory.AlignedFree(fsArena);
        }
    }
}
