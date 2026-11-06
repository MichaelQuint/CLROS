using System.Buffers.Binary;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Pal;
using Fds = OS.Kernel.Compatibility.LinuxX64.LinuxDescriptorServices;
using Fs = OS.Storage.ReadOnlyWebRootFileSystem;
using Block = OS.Storage.ContinuingReadOnlyBlockService;

internal static unsafe partial class LinuxDiskDescriptorServicesTests
{
    private const ulong Key = LinuxThreadServicesTests.Key, Base = LinuxThreadServicesTests.UserBase;
    private const ulong Path = Base + 0x1200, Output = Base + 0x2000;
    private static byte Payload(int i) => unchecked((byte)(i * 31 ^ (i >> 8) ^ 0x5a));
    // Host-only metadata corruption controls need the actual managed field
    // layout. Marshal.OffsetOf describes interop layout (including four-byte
    // marshalled bools), which can name a different field in nested structs.
    private static int Offset<T>(string name)
        where T : unmanaged
    {
        FieldInfo field = typeof(T).GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
        HostCheck.True(field != null, "disk_test_field " + typeof(T).Name + "." + name);
        var method = new DynamicMethod("DiskManagedOffset_" + name, typeof(int), [typeof(T).MakeByRefType()], typeof(LinuxDiskDescriptorServicesTests), true);
        ILGenerator il = method.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldflda, field!);
        il.Emit(OpCodes.Conv_U);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Conv_U);
        il.Emit(OpCodes.Sub);
        il.Emit(OpCodes.Conv_I4);
        il.Emit(OpCodes.Ret);
        var read = (ManagedOffset<T>)method.CreateDelegate(typeof(ManagedOffset<T>));
        T value = default;
        int offset = read(ref value);
        HostCheck.True(offset >= 0 && offset < sizeof(T), "disk_test_managed_offset");
        return offset;
    }

    [HostTest("linux.disk structural audit rejects malformed attachment ranges alignment mount self and epochs")]
    private static void MalformedAttachment()
    {
        using Owner o = new();
        HostCheck.Equal((ulong)o.Disk, *(ulong*)((byte*)o.Files.State + Offset<Fds.State>("Disk")));
        ulong[] fields = [(ulong)((byte*)o.Files.State + Offset<Fds.State>("Disk")), (ulong)((byte*)o.Disk + Offset<Fds.DiskState>("Mount")), (ulong)((byte*)o.Disk + Offset<Fds.DiskState>("Bindings")), (ulong)((byte*)o.Disk + Offset<Fds.DiskState>("Requests"))];
        foreach (ulong address in fields)
        {
            ulong* field = (ulong*)address;
            ulong original = *field;
            foreach (ulong invalid in new[]
            {
                1UL,
                8UL,
                0x0000800000000000UL,
                0x00007ffffffffff8UL,
                ulong.MaxValue,
                original + 1
            }

            )
            {
                try
                {
                    *field = invalid;
                    HostCheck.True(!Fds.Audit(o.Files.State));
                    HostCheck.Equal(Fds.DiskProgress.Rejected, Fds.PumpDiskOne(o.Files.State, o.ThreadOwner).Progress);
                }
                finally
                {
                    *field = original;
                }
            }
        }

        foreach (int offset in new[]
        {
            Offset<Fs.Mount>("Self"),
            Offset<Fs.Mount>("Input") + Offset<Fs.Admission>("Epoch")
        }

        )
        {
            ulong* field = (ulong*)((byte*)o.Storage.Mount + offset);
            ulong original = *field;
            try
            {
                *field ^= 8;
                HostCheck.True(!Fds.Audit(o.Files.State));
            }
            finally
            {
                *field = original;
            }
        }

        HostCheck.True(Fds.Audit(o.Files.State));
    }

    [HostTest("linux.disk malformed ready read and binding fd or filesystem-token links reject before output")]
    private static void MalformedRecords()
    {
        using Owner o = new();
        o.PutPath("/app/wwwroot/index.html");
        var lookup = o.Begin(o.Call(2, Path));
        var token = lookup.Token;
        uint index = *(uint*)((byte*)&token + Offset<Fds.DiskToken>("Index"));
        Fds.DiskRequest* request = o.Requests + index;
        byte[] original = new ReadOnlySpan<byte>(request, sizeof(Fds.DiskRequest)).ToArray();
        try
        {
            // Forge from a real nonborrowed lookup, including a missing FS token.
            // The old audit admitted this shape and completion could index beyond
            // Descriptions when its zero-byte read attempted cursor publication.
            *((byte*)request + Offset<Fds.DiskRequest>("Kind")) = 3;
            *((byte*)request + Offset<Fds.DiskRequest>("Phase")) = 3;
            *(ulong*)((byte*)request + Offset<Fds.DiskRequest>("Number")) = 0;
            *(uint*)((byte*)request + Offset<Fds.DiskRequest>("Description")) = uint.MaxValue;
            new Span<byte>((byte*)request + Offset<Fds.DiskRequest>("Operation"), sizeof(Fs.OperationToken)).Clear();
            HostCheck.True(!Fds.Audit(o.Files.State));
            HostCheck.Equal(Fds.DiskProgress.Rejected, Fds.CompleteDisk(o.Files.State, o.ThreadOwner, o.Worker, token, o.Call(0, 0, Output, 0)).Progress);
        }
        finally
        {
            original.CopyTo(new Span<byte>(request, sizeof(Fds.DiskRequest)));
        }

        Fds.CancelDisk(o.Files.State, o.ThreadOwner, token);
        uint fd = o.Open();
        var call = o.Call(17, fd, Output, 5);
        var read = o.Begin(call);
        token = read.Token;
        index = *(uint*)((byte*)&token + Offset<Fds.DiskToken>("Index"));
        request = o.Requests + index;
        original = new ReadOnlySpan<byte>(request, sizeof(Fds.DiskRequest)).ToArray();
        foreach (int offset in new[]
        {
            Offset<Fds.DiskRequest>("Description"),
            Offset<Fds.DiskRequest>("Binding"),
            Offset<Fds.DiskRequest>("OriginalFd") + Offset<Fds.Token>("Number")
        }

        )
        {
            try
            {
                *(uint*)((byte*)request + offset) = uint.MaxValue;
                HostCheck.True(!Fds.Audit(o.Files.State));
                HostCheck.Equal(Fds.DiskProgress.Rejected, Fds.CompleteDisk(o.Files.State, o.ThreadOwner, o.Worker, token, call).Progress);
            }
            finally
            {
                original.CopyTo(new Span<byte>(request, sizeof(Fds.DiskRequest)));
            }
        }

        uint description = *(uint*)((byte*)o.Files.Admission.Slots + fd * (uint)sizeof(Fds.Slot)) - 1;
        uint binding = *(uint*)((byte*)(o.Files.Admission.Descriptions + description) + Offset<Fds.Description>("Pipe"));
        byte* file = (byte*)(o.Bindings + binding) + Offset<Fds.DiskBinding>("File");
        foreach (string field in new[]
        {
            "Owner",
            "Epoch",
            "Generation"
        }

        )
        {
            ulong* word = (ulong*)(file + Offset<Fs.FileToken>(field));
            ulong saved = *word;
            try
            {
                *word ^= 8;
                HostCheck.True(!Fds.Audit(o.Files.State));
            }
            finally
            {
                *word = saved;
            }
        }

        HostCheck.True(Fds.Audit(o.Files.State));
    }

    [HostTest("linux.disk forged mount retirement cannot discard pending ownership before actual block and FS receipts")]
    private static void SuspectRetirement()
    {
        using Owner o = new();
        uint fd = o.Open();
        var pending = o.Begin(o.Call(17, fd, Output, 5));
        Fds.PumpDiskOne(o.Files.State, o.ThreadOwner);
        Fds.PumpDiskOne(o.Files.State, o.ThreadOwner);
        byte* phase = (byte*)o.Storage.Mount + Offset<Fs.Mount>("Phase");
        byte saved = *phase;
        try
        {
            *phase = 4;
            HostCheck.True(!Fds.Audit(o.Files.State));
            HostCheck.Equal(Fds.DiskProgress.Rejected, Fds.CancelDisk(o.Files.State, o.ThreadOwner, pending.Token).Progress);
            HostCheck.True(!Fds.DetachDisk(o.Files.State));
        }
        finally
        {
            *phase = saved;
        }

        HostCheck.True(Fds.Audit(o.Files.State));
        HostCheck.Equal(Block.Status.Success, Block.Stop(o.Storage.Device));
        try
        {
            *phase = 4;
            HostCheck.True(!Fds.Audit(o.Files.State));
        }
        finally
        {
            *phase = saved;
        }

        HostCheck.True(Fds.Audit(o.Files.State));
        Fds.CancelDisk(o.Files.State, o.ThreadOwner, pending.Token);
        HostCheck.Equal(Fs.Status.Success, Fs.Stop(o.Storage.Mount));
        for (int i = 0; i < 16; i++)
            Fds.PumpDiskOne(o.Files.State, o.ThreadOwner);
        HostCheck.True(Fds.Audit(o.Files.State));
        HostCheck.Equal(0L, o.Core(3, fd));
    }

    [HostTest("linux.disk open and stat orphans block process detach without description borrows until actual drain")]
    private static void LookupDetachGuard()
    {
        foreach (ulong number in new[]
        {
            2UL,
            4UL
        }

        )
        {
            using Owner o = new();
            o.PutPath("/app/wwwroot/index.html");
            var pending = o.Begin(o.Call(number, Path, number == 2 ? 0UL : Output));
            Fds.PumpDiskOne(o.Files.State, o.ThreadOwner);
            Fds.PumpDiskOne(o.Files.State, o.ThreadOwner);
            HostCheck.Equal(0U, o.Files.State->RetainedPipeOperations);
            HostCheck.Equal(LinuxSyscallAction.ThreadExited, o.Thread.Call(60).Action);
            HostCheck.True(o.Thread.Service->CommitExit(Key, 2, o.Thread.Kernel.Capture(o.Worker), out long clear) && clear == 0);
            o.Thread.Kernel.Confirm();
            HostCheck.True(o.Thread.Service->Reap(Key, 2, o.Worker, o.Thread.Kernel.SupervisorRsp, out _));
            o.Reaped = true;
            HostCheck.True(!o.Thread.Service->Release(Key, 2));
            var cancelled = Fds.CancelDisk(o.Files.State, o.ThreadOwner, pending.Token);
            HostCheck.True(cancelled.Progress is Fds.DiskProgress.Draining or Fds.DiskProgress.Immediate);
            if (cancelled.Progress == Fds.DiskProgress.Draining)
                HostCheck.True(!o.Thread.Service->Release(Key, 2));
            for (int i = 0; i < 32; i++)
            {
                o.Storage.Tick();
                Fds.PumpDiskOne(o.Files.State, o.ThreadOwner);
            }

            HostCheck.Equal(Fds.DiskProgress.Rejected, Fds.PollDisk(o.Files.State, o.ThreadOwner, pending.Token).Progress);
            HostCheck.True(Fds.Audit(o.Files.State));
        }
    }

    [HostTest("linux.disk descriptor admission rejects aliases copies and late ownership without touching staged paths")]
    private static void Admission()
    {
        using Owner o = new(attach: false);
        o.PutPath("/app/wwwroot/index.html");
        uint reads = o.Thread.Reads;
        HostCheck.Equal(Fds.DiskProgress.NotHandled, o.Begin(o.Call(2, Path)).Progress);
        HostCheck.Equal(reads, o.Thread.Reads);
        foreach (ulong invalid in new[]
        {
            8UL,
            0x0000800000000000UL,
            0x00007ffffffffff8UL
        }

        )
        {
            var invalidAdmission = o.Admission;
            invalidAdmission.Mount = (Fs.Mount*)invalid;
            HostCheck.True(!Fds.AttachDisk(o.Files.State, o.Disk, invalidAdmission));
            invalidAdmission = o.Admission;
            invalidAdmission.Bindings = (Fds.DiskBinding*)invalid;
            HostCheck.True(!Fds.AttachDisk(o.Files.State, o.Disk, invalidAdmission));
            invalidAdmission = o.Admission;
            invalidAdmission.Requests = (Fds.DiskRequest*)invalid;
            HostCheck.True(!Fds.AttachDisk(o.Files.State, o.Disk, invalidAdmission));
            HostCheck.True(!Fds.AttachDisk(o.Files.State, (Fds.DiskState*)invalid, o.Admission));
        }

        var a = o.Admission;
        a.Bindings = (Fds.DiskBinding*)o.Files.Admission.Scratch;
        HostCheck.True(!Fds.AttachDisk(o.Files.State, o.Disk, a));
        a = o.Admission;
        a.Requests = (Fds.DiskRequest*)o.Storage.Data;
        HostCheck.True(!Fds.AttachDisk(o.Files.State, o.Disk, a));
        a = o.Admission;
        a.SingleOwnerValidated = false;
        HostCheck.True(!Fds.AttachDisk(o.Files.State, o.Disk, a));
        HostCheck.True(Fds.AttachDisk(o.Files.State, o.Disk, o.Admission));
        HostCheck.True(!Fds.AttachDisk(o.Files.State, o.Disk, o.Admission));
        Fds.DiskState copy = *o.Disk;
        HostCheck.True(!Fds.AttachDisk(o.Files.State, &copy, o.Admission));
        HostCheck.Equal(Fds.DiskProgress.Rejected, Fds.BeginDisk(o.Files.State, o.ThreadOwner + 8, o.Worker, o.Call(2, Path)).Progress);
        o.PutPath("/app/fixture.data");
        HostCheck.Equal(Fds.DiskProgress.NotHandled, o.Begin(o.Call(2, Path)).Progress);
    }

    [HostTest("linux.disk retained extents protect immutable directory backing from descriptor aliases")]
    private static void ImmutableDirectoryBacking()
    {
        var storage = new ReadOnlyWebRootFileSystemTests.Fixture(slots: 8, directoryCacheSlots: 5);
        storage.MountReady();
        using Owner o = new(attach: false, storage: storage);
        uint bytes = Fs.ImmutableDirectoryCacheBytes(storage.DirectoryCacheSlots);
        byte[] before = new ReadOnlySpan<byte>(storage.DirectoryCacheBacking, (int)bytes).ToArray();
        var invalid = o.Admission;
        invalid.Bindings = (Fds.DiskBinding*)storage.DirectoryCacheBacking;
        HostCheck.True(!Fds.AttachDisk(o.Files.State, o.Disk, invalid));
        invalid = o.Admission;
        invalid.Requests = (Fds.DiskRequest*)(storage.DirectoryCacheBacking + bytes - 8);
        HostCheck.True(!Fds.AttachDisk(o.Files.State, o.Disk, invalid));
        HostCheck.True(new ReadOnlySpan<byte>(storage.DirectoryCacheBacking, (int)bytes).SequenceEqual(before));
        HostCheck.True(Fds.AttachDisk(o.Files.State, o.Disk, o.Admission));
        uint matches = 0;
        for (uint i = 0; i < Fds.DiskExtentCount; i++)
        {
            Fds.DiskExtentAt(o.Files.State, i, out ulong start, out ulong length);
            if (start != (ulong)storage.DirectoryCacheBacking)
                continue;
            HostCheck.Equal((ulong)bytes, length);
            matches++;
        }

        HostCheck.Equal(1U, matches);
        uint fd = o.Open();
        HostCheck.Equal(32L, o.Run(o.Call(0, fd, Output, 32)));
        HostCheck.Equal(0L, o.Core(3, fd));
        ulong reads = storage.Mount->SectorReads;
        fd = o.Open();
        HostCheck.Equal(reads, storage.Mount->SectorReads);
        HostCheck.Equal(32L, o.Run(o.Call(0, fd, Output, 32)));
        HostCheck.Equal(0L, o.Core(3, fd));
        HostCheck.True(Fds.Audit(o.Files.State));
    }

    [HostTest("linux.disk retained extents protect immutable chain certificates and preserve metadata plus fresh reads")]
    private static void ImmutableFileChainBacking()
    {
        var storage = new ReadOnlyWebRootFileSystemTests.Fixture(slots: 8, directoryCacheSlots: 5, chainCacheSlots: 2);
        storage.MountReady();
        using Owner o = new(attach: false, storage: storage);
        uint bytes = Fs.ImmutableFileChainCacheBytes(storage.ChainCacheSlots);
        HostCheck.Equal(112U, bytes);
        HostCheck.Equal(78U, Fds.DiskExtentCount);
        byte[] before = new ReadOnlySpan<byte>(storage.ChainCacheBacking, (int)bytes).ToArray();
        var invalid = o.Admission;
        invalid.Bindings = (Fds.DiskBinding*)storage.ChainCacheBacking;
        HostCheck.True(!Fds.AttachDisk(o.Files.State, o.Disk, invalid));
        invalid = o.Admission;
        invalid.Requests = (Fds.DiskRequest*)(storage.ChainCacheBacking + bytes - 8);
        HostCheck.True(!Fds.AttachDisk(o.Files.State, o.Disk, invalid));
        HostCheck.True(new ReadOnlySpan<byte>(storage.ChainCacheBacking, (int)bytes).SequenceEqual(before));
        HostCheck.True(Fds.AttachDisk(o.Files.State, o.Disk, o.Admission));
        uint matches = 0;
        for (uint i = 0; i < Fds.DiskExtentCount; i++)
        {
            Fds.DiskExtentAt(o.Files.State, i, out ulong start, out ulong length);
            if (start != (ulong)storage.ChainCacheBacking)
                continue;
            HostCheck.Equal((ulong)bytes, length);
            matches++;
        }

        HostCheck.Equal(1U, matches);
        o.PutPath("/app/wwwroot/index.html");
        HostCheck.Equal(0L, o.Run(o.Call(4, Path, Output)));
        ulong inode = o.Thread.Read64(Output + 8), modified = o.Thread.Read64(Output + 88);
        HostCheck.Equal(0L, o.Run(o.Call(6, Path, Output)));
        HostCheck.Equal(inode, o.Thread.Read64(Output + 8));
        HostCheck.Equal(modified, o.Thread.Read64(Output + 88));
        o.PutPath("/app/wwwroot/index.html/");
        HostCheck.Equal(-20L, o.Run(o.Call(4, Path, Output)));
        for (int repeat = 0; repeat < 2; repeat++)
        {
            uint fd = o.Open();
            ulong reads = storage.Mount->SectorReads;
            HostCheck.Equal(32L, o.Run(o.Call(0, fd, Output, 32)));
            HostCheck.True(storage.Mount->SectorReads > reads, "chain certificate never supplies file data");
            HostCheck.Equal(0L, o.Core(3, fd));
        }

        HostCheck.True(Fds.Audit(o.Files.State));
    }

    [HostTest("linux.disk stat lstat fstat retain FAT inode mtime and checked block allocation")]
    private static void Metadata()
    {
        using Owner o = new();
        o.PutPath("/app/wwwroot/index.html");
        HostCheck.Equal(0L, o.Run(o.Call(4, Path, Output)));
        ulong inode = o.Thread.Read64(Output + 8), mtime = o.Thread.Read64(Output + 88);
        HostCheck.True(inode != 0 && mtime != 0);
        HostCheck.Equal(0x8124U, o.Thread.Read32(Output + 24));
        HostCheck.Equal(700UL, o.Thread.Read64(Output + 48));
        HostCheck.Equal(2UL, o.Thread.Read64(Output + 64));
        byte[] stat = new ReadOnlySpan<byte>(o.Thread.At(Output), 144).ToArray();
        HostCheck.Equal(0L, o.Run(o.Call(6, Path, Output)));
        HostCheck.True(new ReadOnlySpan<byte>(o.Thread.At(Output), 144).SequenceEqual(stat));
        uint fd = o.Open();
        ulong reads = o.Storage.Mount->SectorReads;
        HostCheck.Equal(0L, o.Run(o.Call(5, fd, Output)));
        HostCheck.Equal(reads, o.Storage.Mount->SectorReads);
        HostCheck.True(new ReadOnlySpan<byte>(o.Thread.At(Output), 144).SequenceEqual(stat));
        HostCheck.Equal(0L, o.Core(3, fd));
        o.PutPath("/app/wwwroot/");
        HostCheck.Equal(0L, o.Run(o.Call(4, Path, Output)));
        HostCheck.Equal(0x416dU, o.Thread.Read32(Output + 24));
        HostCheck.Equal(0UL, o.Thread.Read64(Output + 64));
    }

    private static uint DiskIndex(Fds.DiskToken token) => *(uint*)((byte*)&token + Offset<Fds.DiskToken>("Index"));
    private static Fs.OperationToken DiskOperation(Owner o, Fds.DiskToken token)
    {
        uint index = DiskIndex(token);
        HostCheck.True(index < Fds.DiskSlots);
        return *(Fs.OperationToken*)((byte*)(o.Requests + index) + Offset<Fds.DiskRequest>("Operation"));
    }

    private static void SameDeviceCounters(Owner o, ulong submitted, ulong completed, ulong copied)
    {
        HostCheck.Equal(submitted, o.Storage.Device->Submitted);
        HostCheck.Equal(completed, o.Storage.Device->Completed);
        HostCheck.Equal(copied, o.Storage.Device->Copied);
    }

    private static Fds.DiskResult[] PendingMetadata(Owner o, out LinuxSyscallFrame[] calls)
    {
        o.PutPath("/app/wwwroot/index.html");
        calls = new LinuxSyscallFrame[4];
        var requests = new Fds.DiskResult[4];
        new Span<byte>(o.Thread.At(Output), 4 * 256).Fill(0xa7);
        for (int i = 0; i < requests.Length; i++)
        {
            calls[i] = o.Call(i % 2 == 0 ? 4UL : 6UL, Path, Output + (ulong)i * 256);
            requests[i] = o.Begin(calls[i]);
            HostCheck.Equal(Fds.DiskProgress.Pending, requests[i].Progress);
        }

        return requests;
    }

    private static Fds.DiskResult PumpToPhysicalOwner(Owner o)
    {
        HostCheck.True(!o.Storage.Device->ReadPending);
        ulong submitted = o.Storage.Device->Submitted, completed = o.Storage.Device->Completed, copied = o.Storage.Device->Copied;
        for (int i = 0; i < 256; i++)
        {
            var selected = Fds.PumpDiskOne(o.Files.State, o.ThreadOwner);
            if (!o.Storage.Device->ReadPending)
                continue;
            HostCheck.True(selected.Token.IsActive);
            SameDeviceCounters(o, submitted + 1, completed, copied);
            return selected; // No Tick: this is a real submitted, uncompleted read.
        }

        HostCheck.True(false, "pending_owner_no_submit");
        return default;
    }

    [HostTest("linux.disk pending owner consumes the next ordinary metadata completion then resumes round robin")]
    private static void PendingOwnerCompletion()
    {
        using Owner o = new();
        var requests = PendingMetadata(o, out var calls);
        var owner = PumpToPhysicalOwner(o);
        int ownerAt = Array.FindIndex(requests, request => request.Token.Equals(owner.Token));
        HostCheck.True(ownerAt >= 0);
        ulong submitted = o.Storage.Device->Submitted, completed = o.Storage.Device->Completed, copied = o.Storage.Device->Copied;
        foreach (var request in requests)
        {
            if (request.Token.Equals(owner.Token))
                continue;
            // The actual FS operations are retained, but only the physical owner
            // can consume this read. Other lookups may do local metadata work.
            var status = Fs.Poll(o.Storage.Mount, DiskOperation(o, request.Token));
            HostCheck.True(status is Fs.Status.Pending or Fs.Status.Busy or Fs.Status.Ready);
            SameDeviceCounters(o, submitted, completed, copied);
        }

        o.Storage.Tick();
        var completion = Fds.PumpDiskOne(o.Files.State, o.ThreadOwner);
        HostCheck.True(completion.Token.Equals(owner.Token), "pending_owner_completion_turn");
        SameDeviceCounters(o, submitted, completed + 1, copied + 1);
        HostCheck.True(!o.Storage.Device->ReadPending);
        HostCheck.True(new ReadOnlySpan<byte>(o.Thread.At(Output), 4 * 256).IndexOfAnyExcept((byte)0xa7) < 0);
        var next = Fds.PumpDiskOne(o.Files.State, o.ThreadOwner);
        HostCheck.True(next.Token.Equals(requests[(ownerAt + 1) % requests.Length].Token), "pending_owner_round_robin_resume");
        for (int i = 0; i < requests.Length; i++)
        {
            HostCheck.Equal(Fds.DiskProgress.Ready, o.Ready(requests[i].Token).Progress);
            HostCheck.Equal(0L, Fds.CompleteDisk(o.Files.State, o.ThreadOwner, o.Worker, requests[i].Token, calls[i]).Value);
            HostCheck.Equal(700UL, o.Thread.Read64(calls[i].Argument1 + 48));
        }

        HostCheck.True(Fds.Audit(o.Files.State));
    }

    [HostTest("linux.disk pending owner cancellation drains before peers and stale reuse cannot copy")]
    private static void PendingOwnerCancellation()
    {
        using Owner o = new();
        var requests = PendingMetadata(o, out var calls);
        var owner = PumpToPhysicalOwner(o);
        int ownerAt = Array.FindIndex(requests, request => request.Token.Equals(owner.Token));
        HostCheck.True(ownerAt >= 0);
        var cancelled = Fds.CancelDisk(o.Files.State, o.ThreadOwner, owner.Token);
        HostCheck.Equal(Fds.DiskProgress.Draining, cancelled.Progress);
        HostCheck.True(!cancelled.Worker.IsValid);
        ulong submitted = o.Storage.Device->Submitted, completed = o.Storage.Device->Completed, copied = o.Storage.Device->Copied;
        o.Storage.Tick();
        var drain = Fds.PumpDiskOne(o.Files.State, o.ThreadOwner);
        SameDeviceCounters(o, submitted, completed + 1, copied);
        HostCheck.True(!o.Storage.Device->ReadPending && !drain.Worker.IsValid && !drain.Advanced);
        HostCheck.Equal(Fds.DiskProgress.Rejected, Fds.PollDisk(o.Files.State, o.ThreadOwner, owner.Token).Progress);
        HostCheck.True(new ReadOnlySpan<byte>(o.Thread.At(calls[ownerAt].Argument1), 144).IndexOfAnyExcept((byte)0xa7) < 0);
        o.PutPath("/app/wwwroot/empty.bin");
        var replacementCall = o.Call(4, Path, Output + 1024);
        var replacement = o.Begin(replacementCall);
        HostCheck.Equal(Fds.DiskProgress.Pending, replacement.Progress);
        HostCheck.Equal(DiskIndex(owner.Token), DiskIndex(replacement.Token));
        HostCheck.True(!replacement.Token.Equals(owner.Token));
        HostCheck.Equal(Fds.DiskProgress.Rejected, Fds.CancelDisk(o.Files.State, o.ThreadOwner, owner.Token).Progress);
        HostCheck.Equal(Fds.DiskProgress.Rejected, Fds.CompleteDisk(o.Files.State, o.ThreadOwner, o.Worker, owner.Token, calls[ownerAt]).Progress);
        SameDeviceCounters(o, submitted, completed + 1, copied);
        for (int i = 0; i < requests.Length; i++)
        {
            if (i == ownerAt)
                continue;
            HostCheck.Equal(Fds.DiskProgress.Ready, o.Ready(requests[i].Token).Progress);
            HostCheck.Equal(0L, Fds.CompleteDisk(o.Files.State, o.ThreadOwner, o.Worker, requests[i].Token, calls[i]).Value);
        }

        HostCheck.Equal(Fds.DiskProgress.Ready, o.Ready(replacement.Token).Progress);
        HostCheck.Equal(0L, Fds.CompleteDisk(o.Files.State, o.ThreadOwner, o.Worker, replacement.Token, replacementCall).Value);
        HostCheck.Equal(0UL, o.Thread.Read64(Output + 1024 + 48));
        HostCheck.True(Fds.Audit(o.Files.State));
    }

    [HostTest("linux.disk pending owner IO errors are selected without stale-token or nonowner progress")]
    private static void PendingOwnerFailure()
    {
        using Owner o = new();
        uint[] fds = [o.Open(), o.Open(), o.Open()];
        var calls = new LinuxSyscallFrame[fds.Length];
        var requests = new Fds.DiskResult[fds.Length];
        new Span<byte>(o.Thread.At(Output), fds.Length * 64).Fill(0x73);
        for (int i = 0; i < fds.Length; i++)
        {
            calls[i] = o.Call(17, fds[i], Output + (ulong)i * 64, 5, 0);
            requests[i] = o.Begin(calls[i]);
        }

        var owner = PumpToPhysicalOwner(o);
        var stale = owner.Token;
        // Corrupt only an unpublished value copy, never the live operation or mount.
        *(ulong*)((byte*)&stale + Offset<Fds.DiskToken>("Generation")) ^= 1;
        HostCheck.True(stale.IsActive);
        ulong submitted = o.Storage.Device->Submitted, completed = o.Storage.Device->Completed, copied = o.Storage.Device->Copied;
        HostCheck.Equal(Fds.DiskProgress.Rejected, Fds.PollDisk(o.Files.State, o.ThreadOwner, stale).Progress);
        HostCheck.Equal(Fds.DiskProgress.Rejected, Fds.CancelDisk(o.Files.State, o.ThreadOwner, stale).Progress);
        HostCheck.Equal(Fds.DiskProgress.Rejected, Fds.PumpDiskOne(o.Files.State, o.ThreadOwner + 8).Progress);
        SameDeviceCounters(o, submitted, completed, copied);
        o.Storage.Disk.FailLba = o.Storage.Disk.Data(10);
        o.Storage.Tick();
        var failure = Fds.PumpDiskOne(o.Files.State, o.ThreadOwner);
        HostCheck.True(failure.Token.Equals(owner.Token), "pending_owner_error_turn");
        HostCheck.True(failure.Progress is Fds.DiskProgress.Draining or Fds.DiskProgress.Ready);
        HostCheck.True(failure.Value == -5 && failure.Failure != Fs.Status.EndOfFile && !failure.Advanced);
        HostCheck.Equal(submitted, o.Storage.Device->Submitted);
        HostCheck.Equal(copied, o.Storage.Device->Copied);
        HostCheck.True(new ReadOnlySpan<byte>(o.Thread.At(Output), fds.Length * 64).IndexOfAnyExcept((byte)0x73) < 0);
        HostCheck.Equal(Block.Status.Success, Block.Stop(o.Storage.Device));
        HostCheck.Equal(Fs.Status.Success, Fs.Stop(o.Storage.Mount));
        for (int i = 0; i < requests.Length; i++)
        {
            HostCheck.Equal(Fds.DiskProgress.Ready, o.Ready(requests[i].Token).Progress);
            HostCheck.Equal(-5L, Fds.CompleteDisk(o.Files.State, o.ThreadOwner, o.Worker, requests[i].Token, calls[i]).Value);
        }

        HostCheck.True(new ReadOnlySpan<byte>(o.Thread.At(Output), fds.Length * 64).IndexOfAnyExcept((byte)0x73) < 0);
        HostCheck.True(Fds.Audit(o.Files.State));
    }

    [HostTest("linux.disk first queued read submits once without completing or copying until a later turn")]
    private static void FirstReadSubmission()
    {
        using Owner o = new();
        uint fd = o.Open();
        new Span<byte>(o.Thread.At(Output), 700).Fill(0xa5);
        ulong submitted = o.Storage.Device->Submitted, completed = o.Storage.Device->Completed, copied = o.Storage.Device->Copied;
        var call = o.Call(17, fd, Output, 700, 0);
        var request = o.Begin(call);
        HostCheck.Equal(Fds.DiskProgress.Pending, request.Progress);
        HostCheck.Equal(submitted, o.Storage.Device->Submitted);
        HostCheck.Equal(Fds.DiskProgress.Pending, Fds.PumpDiskOne(o.Files.State, o.ThreadOwner).Progress);
        HostCheck.Equal(submitted + 1, o.Storage.Device->Submitted);
        HostCheck.Equal(completed, o.Storage.Device->Completed);
        HostCheck.Equal(copied, o.Storage.Device->Copied);
        HostCheck.True(o.Storage.Device->ReadPending && new ReadOnlySpan<byte>(o.Thread.At(Output), 700).IndexOfAnyExcept((byte)0xa5) < 0);
        o.Storage.Tick();
        Fds.PumpDiskOne(o.Files.State, o.ThreadOwner);
        HostCheck.Equal(submitted + 1, o.Storage.Device->Submitted);
        HostCheck.Equal(completed + 1, o.Storage.Device->Completed);
        HostCheck.Equal(copied + 1, o.Storage.Device->Copied);
        HostCheck.Equal(Fds.DiskProgress.Pending, Fds.PollDisk(o.Files.State, o.ThreadOwner, request.Token).Progress);
        HostCheck.True(new ReadOnlySpan<byte>(o.Thread.At(Output), 700).IndexOfAnyExcept((byte)0xa5) < 0);
        HostCheck.Equal(Fds.DiskProgress.Ready, o.Ready(request.Token).Progress);
        HostCheck.Equal(700L, Fds.CompleteDisk(o.Files.State, o.ThreadOwner, o.Worker, request.Token, call).Value);
        for (int i = 0; i < 700; i++)
            HostCheck.Equal(Payload(i), o.Thread.At(Output)[i]);
        submitted = o.Storage.Device->Submitted;
        foreach (var empty in new[]
        {
            o.Call(17, fd, Output, 0, 0),
            o.Call(17, fd, Output, 700, 700)
        }

        )
        {
            var zero = o.Begin(empty);
            HostCheck.Equal(Fds.DiskProgress.Ready, Fds.PumpDiskOne(o.Files.State, o.ThreadOwner).Progress);
            HostCheck.Equal(0L, Fds.CompleteDisk(o.Files.State, o.ThreadOwner, o.Worker, zero.Token, empty).Value);
            HostCheck.Equal(submitted, o.Storage.Device->Submitted);
        }

        HostCheck.Equal(0L, o.Run(o.Call(8, fd, 0, 1)));
    }

    [HostTest("linux.disk measured16KiB pread requests return complete8KiB ranges and real EOF")]
    private static void ShortReads()
    {
        using Owner o = new(large: true);
        uint fd = o.Open();
        int at = 0;
        while (at < 20000)
        {
            long got = o.Run(o.Call(17, fd, Output, 16384, (ulong)at));
            HostCheck.Equal(Math.Min(8192, 20000 - at), (int)got);
            for (int i = 0; i < got; i++)
                HostCheck.Equal(Payload(at + i), o.Thread.At(Output)[i]);
            at += (int)got;
        }

        ulong reads = o.Storage.Mount->SectorReads;
        HostCheck.Equal(0L, o.Run(o.Call(17, fd, Output, 16384, 20000)));
        HostCheck.Equal(0L, o.Run(o.Call(17, fd, Output, 16384, 25000)));
        HostCheck.Equal(reads, o.Storage.Mount->SectorReads);
        HostCheck.Equal(0L, o.Run(o.Call(8, fd, 0, 1))); // pread did not advance the shared cursor.
    }

    [HostTest("linux.disk duplicated descriptors serialize read and seek while pread leaves shared position unchanged")]
    private static void CursorOrdering()
    {
        using Owner o = new();
        uint fd = o.Open(), duplicate = (uint)o.Core(72, fd, 0, 0);
        var first = o.Call(0, fd, Output, 20);
        var second = o.Call(0, duplicate, Output + 64, 20);
        var a = o.Begin(first);
        var b = o.Begin(second);
        HostCheck.Equal(Fds.DiskProgress.Ready, o.Ready(a.Token).Progress);
        HostCheck.Equal(Fds.DiskProgress.Pending, Fds.PollDisk(o.Files.State, o.ThreadOwner, b.Token).Progress);
        HostCheck.Equal(20L, Fds.CompleteDisk(o.Files.State, o.ThreadOwner, o.Worker, a.Token, first).Value);
        HostCheck.Equal(Fds.DiskProgress.Ready, o.Ready(b.Token).Progress);
        HostCheck.Equal(20L, Fds.CompleteDisk(o.Files.State, o.ThreadOwner, o.Worker, b.Token, second).Value);
        for (int i = 0; i < 20; i++)
            HostCheck.Equal(Payload(20 + i), o.Thread.At(Output + 64)[i]);
        HostCheck.Equal(40L, o.Run(o.Call(8, duplicate, 0, 1)));
        HostCheck.Equal(5L, o.Run(o.Call(17, fd, Output, 5, 500)));
        HostCheck.Equal(40L, o.Run(o.Call(8, fd, 0, 1)));
        HostCheck.Equal(900L, o.Run(o.Call(8, fd, 900, 0)));
        HostCheck.Equal(0L, o.Run(o.Call(0, duplicate, Output, 1)));
        HostCheck.Equal(-22L, o.Run(o.Call(8, fd, ulong.MaxValue, 0)));
        HostCheck.Equal(-75L, o.Run(o.Call(8, fd, (ulong)long.MaxValue, 1)));
    }

    [HostTest("linux.disk close and numeric reuse revoke ready copies and preserve the replacement descriptor")]
    private static void CloseReuse()
    {
        using Owner o = new();
        uint old = o.Open();
        var call = o.Call(17, old, Output, 5, 0);
        var begun = o.Begin(call);
        HostCheck.Equal(Fds.DiskProgress.Ready, o.Ready(begun.Token).Progress);
        HostCheck.Equal(0L, o.Core(3, old));
        uint replacement = o.Open("/app/wwwroot/empty.bin");
        HostCheck.Equal(old, replacement);
        new Span<byte>(o.Thread.At(Output), 5).Fill(0x69);
        var done = Fds.CompleteDisk(o.Files.State, o.ThreadOwner, o.Worker, begun.Token, call);
        HostCheck.Equal(-9L, done.Value);
        HostCheck.Equal(Fds.DiskProgress.Immediate, done.Progress);
        HostCheck.True(new ReadOnlySpan<byte>(o.Thread.At(Output), 5).IndexOfAnyExcept((byte)0x69) < 0);
        HostCheck.Equal(0L, o.Run(o.Call(17, replacement, Output, 5, 0)));
        HostCheck.Equal(Fds.DiskProgress.Rejected, Fds.PollDisk(o.Files.State, o.ThreadOwner, begun.Token).Progress);
    }

    [HostTest("linux.disk cancellation revokes pending and ready output while round-robin pumping drains orphan borrows")]
    private static void Cancellation()
    {
        foreach (bool ready in new[]
        {
            false,
            true
        }

        )
        {
            using Owner o = new();
            uint fd = o.Open();
            var call = o.Call(17, fd, Output, 5, 0);
            var request = o.Begin(call);
            if (ready)
                HostCheck.Equal(Fds.DiskProgress.Ready, o.Ready(request.Token).Progress);
            else
            {
                Fds.PumpDiskOne(o.Files.State, o.ThreadOwner);
                Fds.PumpDiskOne(o.Files.State, o.ThreadOwner);
            }

            new Span<byte>(o.Thread.At(Output), 5).Fill(0x71);
            var cancelled = Fds.CancelDisk(o.Files.State, o.ThreadOwner, request.Token);
            HostCheck.True(cancelled.Progress is Fds.DiskProgress.Immediate or Fds.DiskProgress.Draining);
            HostCheck.True(!cancelled.Worker.IsValid);
            HostCheck.Equal(Fds.DiskProgress.Rejected, Fds.CompleteDisk(o.Files.State, o.ThreadOwner, o.Worker, request.Token, call).Progress);
            for (int i = 0; i < 32; i++)
            {
                o.Storage.Tick();
                Fds.PumpDiskOne(o.Files.State, o.ThreadOwner);
            }

            HostCheck.Equal(Fds.DiskProgress.Rejected, Fds.PollDisk(o.Files.State, o.ThreadOwner, request.Token).Progress);
            HostCheck.True(new ReadOnlySpan<byte>(o.Thread.At(Output), 5).IndexOfAnyExcept((byte)0x71) < 0);
            HostCheck.Equal(5L, o.Run(call));
            HostCheck.True(Fds.Audit(o.Files.State));
        }
    }

    [HostTest("linux.disk user copy faults leave shared cursor and future data intact")]
    private static void CopyFault()
    {
        using Owner o = new();
        uint fd = o.Open();
        o.Thread.FailWrite(Output);
        HostCheck.Equal(-14L, o.Run(o.Call(0, fd, Output, 5)));
        o.Thread.FailWrite(ulong.MaxValue);
        HostCheck.Equal(0L, o.Run(o.Call(8, fd, 0, 1)));
        HostCheck.Equal(5L, o.Run(o.Call(0, fd, Output, 5)));
        for (int i = 0; i < 5; i++)
            HostCheck.Equal(Payload(i), o.Thread.At(Output)[i]);
        HostCheck.True(new ReadOnlySpan<byte>(o.Files.Admission.Scratch, 8192).IndexOfAnyExcept((byte)0) < 0);
    }

    [HostTest("linux.disk reset invalidates held-ready output but structural audit and real retired cleanup survive")]
    private static void ResetReady()
    {
        using Owner o = new();
        uint fd = o.Open();
        var call = o.Call(17, fd, Output, 5, 0);
        var request = o.Begin(call);
        HostCheck.Equal(Fds.DiskProgress.Ready, o.Ready(request.Token).Progress);
        HostCheck.Equal(Block.Status.Success, Block.Stop(o.Storage.Device));
        HostCheck.True(Fds.Audit(o.Files.State));
        new Span<byte>(o.Thread.At(Output), 5).Fill(0x77);
        var result = Fds.CompleteDisk(o.Files.State, o.ThreadOwner, o.Worker, request.Token, call);
        HostCheck.True(result.Value == -5 && result.Progress is Fds.DiskProgress.Immediate or Fds.DiskProgress.Draining);
        HostCheck.True(new ReadOnlySpan<byte>(o.Thread.At(Output), 5).IndexOfAnyExcept((byte)0x77) < 0);
        HostCheck.True(!Fds.DetachDisk(o.Files.State));
        HostCheck.Equal(Fs.Status.Success, Fs.Stop(o.Storage.Mount));
        for (int i = 0; i < 16; i++)
            Fds.PumpDiskOne(o.Files.State, o.ThreadOwner);
        HostCheck.True(Fds.Audit(o.Files.State));
        HostCheck.Equal(0L, o.Core(3, fd));
    }

    [HostTest("linux.disk eight pending slots and file handles report capacity distinctly from EOF and I/O")]
    private static void CapacityAndFailures()
    {
        using Owner o = new();
        var requests = new Fds.DiskResult[8];
        o.PutPath("/app/wwwroot/index.html");
        var call = o.Call(2, Path, 0x88100);
        for (int i = 0; i < 8; i++)
            requests[i] = o.Begin(call);
        HostCheck.Equal(-23L, o.Begin(call).Value);
        foreach (var r in requests)
        {
            HostCheck.Equal(Fds.DiskProgress.Ready, o.Ready(r.Token).Progress);
            HostCheck.True(Fds.CompleteDisk(o.Files.State, o.ThreadOwner, o.Worker, r.Token, call).Value >= 3);
        }

        HostCheck.Equal(-23L, o.Begin(call).Value);
        HostCheck.Equal(0L, o.Core(3, 3));
        o.PutPath("/app/wwwroot/missing.bin");
        HostCheck.Equal(-2L, o.Run(o.Call(2, Path)));
        o.PutPath("/app/wwwroot/../index.html");
        HostCheck.Equal(-13L, o.Run(o.Call(2, Path)));
    }

    [HostTest("linux.disk data I/O failure never becomes EOF and stale copied tokens never complete another range")]
    private static void IoAndTokens()
    {
        using Owner o = new();
        uint fd = o.Open();
        o.Storage.Disk.FailLba = o.Storage.Disk.Data(10);
        var call = o.Call(17, fd, Output, 5, 0);
        var request = o.Begin(call);
        Fds.DiskResult observation = default;
        for (int i = 0; i < 1000; i++)
        {
            o.Storage.Tick();
            Fds.PumpDiskOne(o.Files.State, o.ThreadOwner);
            observation = Fds.PollDisk(o.Files.State, o.ThreadOwner, request.Token);
            if (observation.Progress == Fds.DiskProgress.Draining)
                break;
        }

        HostCheck.True(observation.Progress == Fds.DiskProgress.Draining && observation.Value == -5 && observation.Failure != Fs.Status.EndOfFile);
        HostCheck.True(Fds.Audit(o.Files.State));
        HostCheck.Equal(Block.Status.Success, Block.Stop(o.Storage.Device));
        HostCheck.Equal(Fs.Status.Success, Fs.Stop(o.Storage.Mount));
        HostCheck.Equal(Fds.DiskProgress.Ready, o.Ready(request.Token).Progress);
        HostCheck.Equal(-5L, Fds.CompleteDisk(o.Files.State, o.ThreadOwner, o.Worker, request.Token, call).Value);
        HostCheck.Equal(Fds.DiskProgress.Rejected, Fds.CompleteDisk(o.Files.State, o.ThreadOwner, o.Worker, request.Token, call).Progress);
    }
}
