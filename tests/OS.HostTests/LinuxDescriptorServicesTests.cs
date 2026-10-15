using System.Runtime.InteropServices;
using System.Text;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Memory;
using Services = OS.Kernel.Compatibility.LinuxX64.LinuxDescriptorServices;

internal static unsafe class LinuxDescriptorServicesTests
{
    private const ulong User = 0x100000000, Data = User + 4096, Vectors = User + 20480, Output = User + 24576;
    internal struct Model
    {
        public LinuxMemoryServices.Admission Memory;
        public uint InputCopies, OutputCopies, SinkCalls, SinkBytes, Channel, FailInputAt;
        public bool FailOutput, FailSink;
        public fixed byte Sink[8192];
    }

    internal sealed class Owner : IDisposable
    {
        public readonly LinuxMemoryServicesTests.Owner Memory = new();
        public readonly Services.State* State;
        public readonly Model* Context;
        public Services.Admission Admission;
        private readonly byte* _path;
        private readonly byte* _package;
        public Owner(uint descriptors = 12, uint descriptions = 12, bool ignoreSigpipe = true)
        {
            Memory.Begin();
            HostCheck.Equal((long)User, LinuxMemoryServices.Map(Memory.State, User, 32768, 3, 0x100022, ulong.MaxValue, 0));
            State = (Services.State*)NativeMemory.AlignedAlloc((nuint)sizeof(Services.State), 16); *State = default;
            Context = (Model*)NativeMemory.AlignedAlloc((nuint)sizeof(Model), 16); *Context = new() { Memory = Memory.Memory };
            _path = (byte*)NativeMemory.Alloc(17); Encoding.ASCII.GetBytes("/app/fixture.data").CopyTo(new Span<byte>(_path, 17));
            _package = (byte*)NativeMemory.Alloc(10); Encoding.ASCII.GetBytes("abcdefXYZ\n").CopyTo(new Span<byte>(_package, 10));
            Admission = new()
            {
                Slots = (Services.Slot*)NativeMemory.AllocZeroed(descriptors, (nuint)sizeof(Services.Slot)),
                Descriptions = (Services.Description*)NativeMemory.AllocZeroed(descriptions, (nuint)sizeof(Services.Description)),
                Pipes = (LinuxPipe*)NativeMemory.AllocZeroed(2, (nuint)sizeof(LinuxPipe)),
                PipeBuffers = (byte*)NativeMemory.Alloc(8192), Snapshots = (byte*)NativeMemory.Alloc(descriptions * Services.SnapshotBytes),
                Scratch = (byte*)NativeMemory.Alloc(8192), DescriptorCount = descriptors, DescriptionCount = descriptions,
                PipeCount = 2, PipeCapacity = 4096, ScratchBytes = 8192,
                Views = (Services.View*)NativeMemory.AllocZeroed(2, (nuint)sizeof(Services.View)), ViewCount = 1,
                Memory = Memory.State, PhysicalBytes = 256 * 1024 * 1024, AvailableBytes = 64 * 1024 * 1024,
                User = new() { Context = Context, PageAccess = &Permissions, CopyFromUser = &CopyIn, Write = &Sink },
                CopyToUser = &CopyOut, FirmwareExited = true, SingleOwnerValidated = true, SigpipeIgnored = ignoreSigpipe
            };
            Admission.Views[0] = new() { Path = _path, PathBytes = 17, Data = _package, Bytes = 10, Inode = 256 };
            new Span<byte>(Admission.PipeBuffers, 8192).Fill(0xa5);
            new Span<byte>(Admission.Snapshots, (int)(descriptions * Services.SnapshotBytes)).Fill(0xa5);
            new Span<byte>(Admission.Scratch, 8192).Fill(0xa5);
        }
        public void Begin() => HostCheck.True(Services.Begin(State, Admission));
        public long Call(ulong number, ulong a0 = 0, ulong a1 = 0, ulong a2 = 0, ulong a3 = 0) =>
            Services.Dispatch(State, new() { Number = number, Argument0 = a0, Argument1 = a1, Argument2 = a2, Argument3 = a3 });
        public void Text(ulong address, string text)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(text);
            bytes.CopyTo(new Span<byte>(Memory.At(address), bytes.Length));
            Memory.At(address)[bytes.Length] = 0;
        }
        public long Open(string path = "/app/fixture.data", ulong flags = 0)
        { Text(User, path); return Call(257, unchecked((uint)-100), User, flags); }
        public (uint Read, uint Write) Pipe(uint flags = Services.NonBlock | Services.CloseOnExec)
        {
            HostCheck.Equal(0L, Call(293, Output, flags));
            uint* fds = (uint*)Memory.At(Output); return (fds[0], fds[1]);
        }
        public string ReadText(uint fd)
        {
            long bytes = Call(0, fd, Output, 4096); HostCheck.True(bytes >= 0);
            return Encoding.ASCII.GetString(new ReadOnlySpan<byte>(Memory.At(Output), (int)bytes));
        }
        public void Dispose()
        {
            NativeMemory.Free(Admission.Slots); NativeMemory.Free(Admission.Descriptions); NativeMemory.Free(Admission.Pipes);
            NativeMemory.Free(Admission.PipeBuffers); NativeMemory.Free(Admission.Snapshots); NativeMemory.Free(Admission.Scratch);
            NativeMemory.Free(Admission.Views); NativeMemory.Free(_path); NativeMemory.Free(_package);
            NativeMemory.AlignedFree(State); NativeMemory.AlignedFree(Context); Memory.Dispose();
        }
    }

    [UnmanagedCallersOnly]
    private static byte Permissions(void* context, ulong page)
    {
        var m = ((Model*)context)->Memory;
        if (!X64UserAddressSpace.TryReadLeaf(m.Tables, m.RootPhysical, m.TablePages, page, out ulong leaf) || (leaf & 5) != 5) return 0;
        return (byte)(1 | ((leaf & 2) != 0 ? 2 : 0) | ((leaf & (1UL << 63)) == 0 ? 4 : 0));
    }
    private static byte* UserByte(Model* model, ulong address, bool write)
    {
        var m = model->Memory;
        if (!X64UserAddressSpace.TryReadLeaf(m.Tables, m.RootPhysical, m.TablePages, address, out ulong leaf) ||
            (leaf & (write ? 7UL : 5UL)) != (write ? 7UL : 5UL)) return null;
        ulong physical = (leaf & 0x000ffffffffff000) + (address & 4095);
        return physical >= m.PoolPhysical && physical < m.PoolPhysical + m.PoolPages * 4096UL ? m.Pool + physical - m.PoolPhysical : null;
    }
    [UnmanagedCallersOnly]
    private static byte CopyIn(void* context, ulong address, byte* destination, uint bytes)
    {
        Model* model = (Model*)context;
        if (++model->InputCopies == model->FailInputAt) return 0;
        for (uint i = 0; i < bytes; i++)
        { byte* source = UserByte(model, address + i, false); if (source == null) return 0; destination[i] = *source; }
        return 1;
    }
    [UnmanagedCallersOnly]
    private static byte CopyOut(void* context, ulong address, byte* source, uint bytes)
    {
        Model* model = (Model*)context; model->OutputCopies++;
        if (model->FailOutput) return 0;
        for (uint i = 0; i < bytes; i++)
        { byte* target = UserByte(model, address + i, true); if (target == null) return 0; *target = source[i]; }
        return 1;
    }
    [UnmanagedCallersOnly]
    private static byte Sink(void* context, uint fd, byte* source, uint bytes)
    {
        Model* model = (Model*)context; model->SinkCalls++;
        if (model->FailSink || bytes > 8192) return 0;
        model->Channel = fd; model->SinkBytes = bytes;
        for (uint i = 0; i < bytes; i++) model->Sink[i] = source[i];
        return 1;
    }

    [HostTest("m16-007 descriptor admission rejects malformed ownership without mutation", HostLane.Fast, 0)]
    private static void AdmissionFailures()
    {
        using var owner = new Owner(); var a = owner.Admission;
        HostCheck.True(!Services.Begin(null, a));
        a.SingleOwnerValidated = false; HostCheck.True(!Services.Begin(owner.State, a)); a = owner.Admission;
        a.FirmwareExited = false; HostCheck.True(!Services.Begin(owner.State, a)); a = owner.Admission;
        a.Scratch = a.PipeBuffers; HostCheck.True(!Services.Begin(owner.State, a)); a = owner.Admission;
        a.Scratch = owner.Memory.Memory.Pool; HostCheck.True(!Services.Begin(owner.State, a)); a = owner.Admission;
        a.Scratch = (byte*)(ulong.MaxValue - 7); HostCheck.True(!Services.Begin(owner.State, a)); a = owner.Admission;
        a.DescriptorCount = 33; HostCheck.True(!Services.Begin(owner.State, a)); a = owner.Admission;
        a.DescriptionCount = 2; HostCheck.True(!Services.Begin(owner.State, a)); a = owner.Admission;
        a.PipeCapacity = 4097; HostCheck.True(!Services.Begin(owner.State, a)); a = owner.Admission;
        a.ScratchBytes = 4096; HostCheck.True(!Services.Begin(owner.State, a)); a = owner.Admission;
        a.User.CopyFromUser = null; HostCheck.True(!Services.Begin(owner.State, a)); a = owner.Admission;
        a.CopyToUser = null; HostCheck.True(!Services.Begin(owner.State, a)); a = owner.Admission;
        a.AvailableBytes = 4096; HostCheck.True(!Services.Begin(owner.State, a));
        HostCheck.True(new ReadOnlySpan<byte>(owner.Admission.Scratch, 8192).IndexOfAnyExcept((byte)0xa5) == -1);
        HostCheck.True(new ReadOnlySpan<byte>(owner.Admission.PipeBuffers, 8192).IndexOfAnyExcept((byte)0xa5) == -1);
        HostCheck.Equal(0U, owner.State->ActiveDescriptors);
        owner.Begin(); HostCheck.True(Services.Audit(owner.State) && !Services.Begin(owner.State, owner.Admission));
        HostCheck.Equal(3U, owner.State->ActiveDescriptors);
    }

    [HostTest("m16-007 immutable view admission rejects alias duplicates and reserved proc namespace", HostLane.Fast, 1)]
    private static void ViewAdmission()
    {
        using var owner = new Owner(); Services.View original = owner.Admission.Views[0];
        owner.Admission.Views[0].Data = owner.Admission.Scratch;
        HostCheck.True(!Services.Begin(owner.State, owner.Admission)); owner.Admission.Views[0] = original;
        owner.Admission.Views[1] = original; owner.Admission.ViewCount = 2;
        HostCheck.True(!Services.Begin(owner.State, owner.Admission)); owner.Admission.ViewCount = 1;
        owner.Admission.Views[0].Inode = 1; HostCheck.True(!Services.Begin(owner.State, owner.Admission)); owner.Admission.Views[0] = original;
        byte* path = stackalloc byte[13]; Encoding.ASCII.GetBytes("/proc/meminfo").CopyTo(new Span<byte>(path, 13));
        owner.Admission.Views[0].Path = path; owner.Admission.Views[0].PathBytes = 13;
        HostCheck.True(!Services.Begin(owner.State, owner.Admission)); owner.Admission.Views[0] = original;
        owner.Admission.Views[0].PathBytes = 2; HostCheck.True(!Services.Begin(owner.State, owner.Admission));
        owner.Admission.Views[0] = original;
        owner.Begin(); HostCheck.True(Services.End(owner.State)); HostCheck.True(!Services.Begin(owner.State, owner.Admission));
    }

    [HostTest("m16-007 standard descriptors use explicit EOF serial channels and ENOTTY policy", HostLane.Fast, 2)]
    private static void StandardDescriptors()
    {
        using var owner = new Owner(); owner.Begin(); owner.Text(Data, "owned output");
        HostCheck.Equal(0L, owner.Call(0, 0, 0, 12)); HostCheck.Equal(-9L, owner.Call(0, 1, Output, 1));
        HostCheck.Equal(-9L, owner.Call(1, 0, Data, 12)); HostCheck.Equal(12L, owner.Call(1, 1, Data, 12));
        HostCheck.Equal(1U, owner.Context->Channel); HostCheck.Equal(12U, owner.Context->SinkBytes);
        HostCheck.Equal(12L, owner.Call(1, 2, Data, 12)); HostCheck.Equal(2U, owner.Context->Channel);
        HostCheck.Equal(-25L, owner.Call(16, 0, 0x5401, 0)); HostCheck.Equal(-25L, owner.Call(16, 1, 0x5413, 0));
        HostCheck.Equal(-9L, owner.Call(16, 99, 0x5401, Output)); HostCheck.Equal(-29L, owner.Call(8, 1));
        HostCheck.Equal(-29L, owner.Call(17, 0, Output, 1)); HostCheck.Equal(-38L, owner.Call(59));
        HostCheck.Equal(0L, owner.Call(3, 1)); HostCheck.Equal(-9L, owner.Call(1, 1, 0, 0));
        HostCheck.Equal(1L, owner.Call(72, 2, 0, 0)); // Lowest free fd, shared channel 2.
        HostCheck.Equal(1L, owner.Call(1, 1, Data, 1)); HostCheck.Equal(2U, owner.Context->Channel);
        owner.Context->FailSink = true; HostCheck.Equal(-5L, owner.Call(1, 2, Data, 1));
        HostCheck.True(Services.Audit(owner.State) && Services.End(owner.State));
        HostCheck.Equal(-14L, owner.Call(0)); HostCheck.True(!Services.End(owner.State));
    }

    [HostTest("m16-007 duplicates share file cursor and status while close-on-exec remains per fd", HostLane.Fast, 3)]
    private static void SharedDescription()
    {
        using var owner = new Owner(); owner.Begin(); uint fd = (uint)owner.Open(flags: Services.CloseOnExec | 0x20000);
        HostCheck.Equal(3U, fd); HostCheck.Equal(1L, owner.Call(72, fd, 1)); HostCheck.Equal(0x28000L, owner.Call(72, fd, 3));
        uint dup = (uint)owner.Call(72, fd, 0, 5), clo = (uint)owner.Call(72, fd, 1030, 6);
        HostCheck.Equal(5U, dup); HostCheck.Equal(6U, clo); HostCheck.Equal(0L, owner.Call(72, dup, 1));
        HostCheck.Equal(1L, owner.Call(72, clo, 1)); HostCheck.Equal(0L, owner.Call(72, dup, 2, 1));
        HostCheck.Equal(0L, owner.Call(72, fd, 2, 0)); HostCheck.Equal(1L, owner.Call(72, dup, 1));
        HostCheck.Equal(0L, owner.Call(72, dup, 4, 0x28800)); HostCheck.Equal(0x28800L, owner.Call(72, fd, 3));
        HostCheck.Equal(3L, owner.Call(0, fd, Output, 3)); HostCheck.Equal("abc", Encoding.ASCII.GetString(new ReadOnlySpan<byte>(owner.Memory.At(Output), 3)));
        HostCheck.Equal(2L, owner.Call(0, dup, Output, 2)); HostCheck.Equal("de", Encoding.ASCII.GetString(new ReadOnlySpan<byte>(owner.Memory.At(Output), 2)));
        HostCheck.Equal(3L, owner.Call(17, clo, Output, 3, 1)); HostCheck.Equal("bcd", Encoding.ASCII.GetString(new ReadOnlySpan<byte>(owner.Memory.At(Output), 3)));
        HostCheck.Equal(5L, owner.Call(8, fd, 0, 1)); HostCheck.Equal(9L, owner.Call(8, dup, ulong.MaxValue, 2));
        HostCheck.Equal(1L, owner.Call(0, clo, Output, 8)); HostCheck.Equal((byte)'\n', *owner.Memory.At(Output));
        HostCheck.Equal(0L, owner.Call(0, fd, 0, 8)); HostCheck.Equal(-22L, owner.Call(8, fd, unchecked((ulong)long.MinValue), 1));
        HostCheck.Equal(10L, owner.Call(8, fd, 0, 1)); HostCheck.Equal(long.MaxValue, owner.Call(8, fd, long.MaxValue, 0));
        HostCheck.Equal(-75L, owner.Call(8, dup, 1, 1)); HostCheck.Equal(-22L, owner.Call(8, fd, 0, 3));
        HostCheck.Equal(-22L, owner.Call(17, fd, Output, 1, ulong.MaxValue));
        HostCheck.Equal(0L, owner.Call(3, fd)); HostCheck.Equal(0L, owner.Call(3, dup)); HostCheck.Equal(0L, owner.Call(8, clo));
        HostCheck.True(Services.Audit(owner.State));
    }

    [HostTest("m16-007 immutable opens reject traversal absent files write flags and invalid bases", HostLane.Fast, 4)]
    private static void PathsAndReadTransactions()
    {
        using var owner = new Owner(); owner.Begin();
        HostCheck.Equal(-13L, owner.Open("/app/../fixture.data")); HostCheck.Equal(-13L, owner.Open("/app/./fixture.data"));
        HostCheck.Equal(-13L, owner.Open("/app//fixture.data")); HostCheck.Equal(-13L, owner.Open("/app\\fixture.data"));
        HostCheck.Equal(-2L, owner.Open("/sys/devices/system/cpu/possible")); HostCheck.Equal(-2L, owner.Open("/etc/terminfo/x/xterm"));
        HostCheck.Equal(-2L, owner.Open("")); HostCheck.Equal(-30L, owner.Open(flags: 1));
        HostCheck.Equal(-30L, owner.Open(flags: 0x40)); HostCheck.Equal(-22L, owner.Open(flags: 0x10000));
        owner.Text(User, "app/fixture.data"); HostCheck.Equal(-9L, owner.Call(257, 99, User));
        HostCheck.Equal(-20L, owner.Call(257, 1, User)); uint fd = (uint)owner.Call(257, unchecked((uint)-100), User);
        HostCheck.Equal(3U, fd); owner.Context->FailOutput = true;
        HostCheck.Equal(-14L, owner.Call(0, fd, Output, 3)); owner.Context->FailOutput = false;
        HostCheck.Equal(0L, owner.Call(8, fd, 0, 1)); HostCheck.Equal(-14L, owner.Call(0, fd, User + 32766, 4));
        HostCheck.Equal(0L, owner.Call(8, fd, 0, 1)); HostCheck.Equal(-9L, owner.Call(1, fd, Data, 1));
        HostCheck.Equal(10L, owner.Call(0, fd, Output, 4096)); HostCheck.Equal(-22L, owner.Call(0, fd, Output, 65537));
        new Span<byte>(owner.Memory.At(User), 256).Fill((byte)'a'); HostCheck.Equal(-36L, owner.Call(2, User));
        HostCheck.Equal(-14L, owner.Call(2, ulong.MaxValue)); HostCheck.Equal(4U, owner.State->ActiveDescriptors);
        HostCheck.True(Services.Audit(owner.State));
    }

    [HostTest("m16-007 fstat uses independent x64 layout without host device or timestamp fabrication", HostLane.Fast, 5)]
    private static void StatLayout()
    {
        using var owner = new Owner(); owner.Begin(); uint fd = (uint)owner.Open();
        HostCheck.Equal(0L, owner.Call(5, fd, Output));
        byte* wire = owner.Memory.At(Output);
        byte[] expected = new byte[144]; expected[9] = 1; expected[16] = 1; expected[24] = 0x24; expected[25] = 0x81;
        expected[48] = 10; expected[57] = 0x10;
        HostCheck.True(expected.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(wire, 144)));
        HostCheck.Equal(0L, owner.Call(5, 1, Output)); HostCheck.Equal(0x2180U, *(uint*)(wire + 24));
        HostCheck.Equal(0UL, *(ulong*)(wire + 48)); HostCheck.Equal(-14L, owner.Call(5, fd, User + 32760));
        HostCheck.Equal(-9L, owner.Call(5, 99, Output));
        var pipe = owner.Pipe(); HostCheck.Equal(0L, owner.Call(5, pipe.Read, Output));
        HostCheck.Equal(0x1180U, *(uint*)(wire + 24)); ulong inode = *(ulong*)(wire + 8);
        HostCheck.Equal(0L, owner.Call(5, pipe.Write, Output)); HostCheck.Equal(inode, *(ulong*)(wire + 8));
    }

    [HostTest("m16-007 proc snapshots use actual owned memory and user VMAs including sparse reservations", HostLane.Fast, 6)]
    private static void ProcSnapshots()
    {
        using var owner = new Owner(); owner.Begin(); uint info = (uint)owner.Open("/proc/meminfo");
        HostCheck.Equal("MemTotal: 262144 kB\nMemFree: 65504 kB\nMemAvailable: 65504 kB\n", owner.ReadText(info));
        uint maps = (uint)owner.Open("/proc/self/maps");
        string original = owner.ReadText(maps);
        HostCheck.Equal("00400000-00401000 r-xp 00000000 00:00 0\n100000000-100008000 rw-p 00000000 00:00 0\n", original);
        HostCheck.Equal(0L, owner.Call(8, maps));
        const ulong sparse = 0x500000000;
        HostCheck.Equal((long)sparse, LinuxMemoryServices.Map(owner.Memory.State, sparse, 12288, 0, 0x100022, ulong.MaxValue, 0));
        HostCheck.Equal(original, owner.ReadText(maps));
        uint newer = (uint)owner.Open("/proc/self/maps");
        HostCheck.Equal(original + "500000000-500003000 ---p 00000000 00:00 0\n", owner.ReadText(newer));
        HostCheck.Equal(0L, LinuxMemoryServices.Protect(owner.Memory.State, sparse, 4096, 3));
        uint liveInfo = (uint)owner.Open("/proc/meminfo");
        HostCheck.True(owner.ReadText(liveInfo).Contains("MemFree: 65500 kB\n", StringComparison.Ordinal));
        HostCheck.Equal(0L, owner.Call(8, info)); HostCheck.True(owner.ReadText(info).Contains("MemFree: 65504 kB\n", StringComparison.Ordinal));
        HostCheck.Equal(-2L, owner.Open("/proc/mounts")); HostCheck.True(Services.Audit(owner.State));
    }

    [HostTest("m16-007 writev preflights every vector and preserves byte ordering partial counts and scratch hygiene", HostLane.Fast, 7)]
    private static void VectorsAndPartialOutput()
    {
        using var owner = new Owner(); owner.Begin(); owner.Text(Data, "ABCDEF");
        ulong* vectors = (ulong*)owner.Memory.At(Vectors);
        vectors[0] = Data; vectors[1] = 3; vectors[2] = ulong.MaxValue; vectors[3] = 0; vectors[4] = Data + 3; vectors[5] = 3;
        HostCheck.Equal(6L, owner.Call(20, 1, Vectors, 3));
        HostCheck.Equal("ABCDEF", Encoding.ASCII.GetString(new ReadOnlySpan<byte>(owner.Context->Sink, 6)));
        uint before = owner.Context->SinkCalls;
        vectors[4] = User + 32768; HostCheck.Equal(-14L, owner.Call(20, 1, Vectors, 3)); HostCheck.Equal(before, owner.Context->SinkCalls);
        vectors[4] = Data + 3; owner.Context->FailInputAt = owner.Context->InputCopies + 3;
        HostCheck.Equal(-14L, owner.Call(20, 1, Vectors, 3)); HostCheck.Equal(before, owner.Context->SinkCalls);
        HostCheck.True(new ReadOnlySpan<byte>(owner.Admission.Scratch, 8192).IndexOfAnyExcept((byte)0) == -1);
        owner.Context->FailInputAt = 0;
        HostCheck.Equal(-22L, owner.Call(20, 1, Vectors, ulong.MaxValue)); HostCheck.Equal(-14L, owner.Call(20, 1, User + 32760, 1));
        vectors[1] = 65537; HostCheck.Equal(-22L, owner.Call(20, 1, Vectors, 1));
        HostCheck.Equal(0L, owner.Call(20, 1, ulong.MaxValue, 0)); HostCheck.Equal(-9L, owner.Call(20, 0, Vectors, 0));
        new Span<byte>(owner.Memory.At(Data), 16384).Fill(0x6d);
        HostCheck.Equal(8192L, owner.Call(1, 2, Data, 16384)); HostCheck.Equal(8192U, owner.Context->SinkBytes);
        vectors[0] = Data; vectors[1] = 4000; vectors[2] = Data + 4000; vectors[3] = 6000;
        HostCheck.Equal(8192L, owner.Call(20, 1, Vectors, 2));
        HostCheck.True(new ReadOnlySpan<byte>(owner.Context->Sink, 8192).IndexOfAnyExcept((byte)0x6d) == -1);
    }

    [HostTest("m16-007 pipe duplicates retain shared endpoints flags queue order and EOF", HostLane.Fast, 8)]
    private static void PipeOwnership()
    {
        using var owner = new Owner(); owner.Begin(); var p = owner.Pipe();
        HostCheck.Equal(1L, owner.Call(72, p.Read, 1)); HostCheck.Equal(1L, owner.Call(72, p.Write, 1));
        HostCheck.Equal(0x800L, owner.Call(72, p.Read, 3)); HostCheck.Equal(0x801L, owner.Call(72, p.Write, 3));
        HostCheck.Equal(-11L, owner.Call(0, p.Read, Output, 1)); owner.Text(Data, "abcd");
        HostCheck.Equal(4L, owner.Call(1, p.Write, Data, 4)); HostCheck.Equal(2L, owner.Call(0, p.Read, Output, 2));
        HostCheck.Equal("ab", Encoding.ASCII.GetString(new ReadOnlySpan<byte>(owner.Memory.At(Output), 2)));
        uint duplicate = (uint)owner.Call(72, p.Write, 1030, 0);
        HostCheck.Equal(1U, owner.Admission.Pipes[0].WriterCount);
        HostCheck.Equal(0L, owner.Call(3, p.Write)); HostCheck.Equal(1U, owner.Admission.Pipes[0].WriterCount);
        HostCheck.Equal(0L, owner.Call(3, duplicate)); HostCheck.Equal(0U, owner.Admission.Pipes[0].WriterCount);
        HostCheck.Equal("cd", owner.ReadText(p.Read)); HostCheck.Equal(0L, owner.Call(0, p.Read, 0, 1));
        HostCheck.Equal(0L, owner.Call(3, p.Read)); HostCheck.True(!owner.Admission.Pipes[0].Initialized);
        HostCheck.True(Services.Audit(owner.State));
    }

    [HostTest("m16-007 pipe partial atomic writes failed copy-out and explicit broken-pipe policy retain ownership", HostLane.Fast, 9)]
    private static void PipeErrorsAndPartialIo()
    {
        using var owner = new Owner(); owner.Begin(); var p = owner.Pipe();
        new Span<byte>(owner.Memory.At(Data), 8192).Fill(0x4d);
        HostCheck.Equal(4090L, owner.Call(1, p.Write, Data, 4090));
        HostCheck.Equal(-11L, owner.Call(1, p.Write, Data, 10)); HostCheck.Equal(4090U, owner.Admission.Pipes[0].BufferedBytes);
        HostCheck.Equal(6L, owner.Call(1, p.Write, Data, 8192)); HostCheck.Equal(-11L, owner.Call(1, p.Write, Data, 8192));
        owner.Context->FailOutput = true; HostCheck.Equal(-14L, owner.Call(0, p.Read, Output, 8));
        HostCheck.Equal(4096U, owner.Admission.Pipes[0].BufferedBytes); owner.Context->FailOutput = false;
        HostCheck.Equal(-14L, owner.Call(0, p.Read, User + 32766, 8)); HostCheck.Equal(4096U, owner.Admission.Pipes[0].BufferedBytes);
        HostCheck.Equal(4096L, owner.Call(0, p.Read, Output, 8192));
        HostCheck.True(new ReadOnlySpan<byte>(owner.Memory.At(Output), 4096).IndexOfAnyExcept((byte)0x4d) == -1);
        HostCheck.Equal(-14L, owner.Call(1, p.Write, User + 32766, 8)); HostCheck.Equal(0U, owner.Admission.Pipes[0].BufferedBytes);
        uint reader = (uint)owner.Call(72, p.Read, 0, 0); HostCheck.Equal(0L, owner.Call(3, p.Read));
        HostCheck.Equal(1L, owner.Call(1, p.Write, Data, 1)); HostCheck.Equal(0L, owner.Call(3, reader));
        HostCheck.Equal(-32L, owner.Call(1, p.Write, Data, 1)); HostCheck.Equal(0L, owner.Call(1, p.Write, 0, 0));
        HostCheck.Equal(0L, owner.Call(3, p.Write)); HostCheck.True(Services.Audit(owner.State));
        using var signals = new Owner(ignoreSigpipe: false); signals.Begin(); var q = signals.Pipe();
        HostCheck.Equal(0L, signals.Call(3, q.Read)); signals.Text(Data, "x");
        HostCheck.Equal(-38L, signals.Call(1, q.Write, Data, 1)); HostCheck.Equal(0U, signals.Admission.Pipes[0].BufferedBytes);
    }

    [HostTest("m16-007 blocking pipes stay unsupported and O_NONBLOCK changes propagate through duplicates", HostLane.Fast, 10)]
    private static void BlockingBoundary()
    {
        using var owner = new Owner(); owner.Begin(); var p = owner.Pipe(Services.CloseOnExec);
        owner.Text(Data, "x"); HostCheck.Equal(-38L, owner.Call(1, p.Write, Data, 1));
        HostCheck.Equal(-38L, owner.Call(0, p.Read, Output, 1)); HostCheck.Equal(0L, owner.Call(1, p.Write, 0, 0));
        HostCheck.Equal(0L, owner.Call(0, p.Read, 0, 0));
        uint duplicate = (uint)owner.Call(72, p.Write, 0, 0);
        HostCheck.Equal(0L, owner.Call(72, duplicate, 4, Services.NonBlock)); HostCheck.Equal(0x801L, owner.Call(72, p.Write, 3));
        HostCheck.Equal(1L, owner.Call(1, p.Write, Data, 1)); HostCheck.Equal(-38L, owner.Call(0, p.Read, Output, 1));
        HostCheck.Equal(0L, owner.Call(72, p.Read, 4, Services.NonBlock)); HostCheck.Equal(1L, owner.Call(0, p.Read, Output, 1));
        HostCheck.Equal(-22L, owner.Call(72, p.Write, 2, 2)); HostCheck.Equal(-22L, owner.Call(72, p.Write, 4, 0x400));
        HostCheck.Equal(-22L, owner.Call(72, p.Write, 0, ulong.MaxValue)); HostCheck.Equal(-22L, owner.Call(293, Output, 4));
        HostCheck.True(Services.Audit(owner.State));
    }

    [HostTest("m16-007 descriptor exhaustion pipe rollback reused integers and stale tokens do not leak", HostLane.Fast, 11)]
    private static void QuotasAndTokens()
    {
        using var owner = new Owner(); owner.Begin(); uint fd = (uint)owner.Open();
        HostCheck.True(Services.Capture(owner.State, fd, out Services.Token token) && Services.IsCurrent(owner.State, token));
        HostCheck.Equal(0L, owner.Call(3, fd)); HostCheck.True(!Services.IsCurrent(owner.State, token));
        HostCheck.Equal((long)fd, owner.Open()); HostCheck.True(!Services.IsCurrent(owner.State, token));
        HostCheck.Equal(1L, owner.Call(0, fd, Output, 1)); HostCheck.Equal((byte)'a', *owner.Memory.At(Output));
        using var another = new Owner(); another.Begin(); HostCheck.True(!Services.IsCurrent(another.State, token));
        for (uint i = 4; i < 12; i++) HostCheck.Equal((long)i, owner.Call(72, 1, 0, 0));
        HostCheck.Equal(-24L, owner.Call(72, 1, 0, 0)); HostCheck.Equal(-24L, owner.Call(293, Output, Services.NonBlock));
        HostCheck.Equal(0L, owner.Call(3, 8)); HostCheck.Equal(-24L, owner.Call(293, Output, Services.NonBlock));
        HostCheck.Equal(0L, owner.Call(3, 9)); owner.Context->FailOutput = true;
        HostCheck.Equal(-14L, owner.Call(293, Output, Services.NonBlock)); HostCheck.Equal(10U, owner.State->ActiveDescriptors);
        HostCheck.True(!owner.Admission.Pipes[0].Initialized && Services.Audit(owner.State)); owner.Context->FailOutput = false;
        var p = owner.Pipe(); HostCheck.Equal(8U, p.Read); HostCheck.Equal(9U, p.Write);
        HostCheck.True(Services.End(owner.State)); HostCheck.Equal(0U, owner.State->ActiveDescriptors);
        HostCheck.True(new ReadOnlySpan<byte>(owner.Admission.PipeBuffers, 8192).IndexOfAnyExcept((byte)0) == -1);
        using var descriptions = new Owner(descriptions: 3); descriptions.Begin();
        HostCheck.Equal(-23L, descriptions.Open()); HostCheck.Equal(-23L, descriptions.Call(293, Output, Services.NonBlock));
        HostCheck.Equal(3U, descriptions.State->ActiveDescriptors);
        using var pipes = new Owner(); pipes.Begin(); pipes.Pipe(); pipes.Pipe();
        HostCheck.Equal(-23L, pipes.Call(293, Output, Services.NonBlock)); HostCheck.Equal(7U, pipes.State->ActiveDescriptors);
        HostCheck.True(Services.Audit(pipes.State));
    }

    [HostTest("m16-007 descriptor dispatch allocates no managed memory and clears borrowed scratch", HostLane.Fast, 12)]
    private static void AllocationBoundary()
    {
        using var owner = new Owner(); owner.Begin(); uint fd = (uint)owner.Open();
        HostCheck.Equal(0L, owner.Call(72, fd, 1));
        long before = GC.GetAllocatedBytesForCurrentThread(), result = 0;
        for (uint i = 0; i < 32; i++) result += owner.Call(72, fd, 1);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        HostCheck.Equal(0L, result); HostCheck.Equal(0L, allocated);
        HostCheck.True(new ReadOnlySpan<byte>(owner.Admission.Scratch, 8192).IndexOfAnyExcept((byte)0) == -1);
        HostCheck.True(Services.End(owner.State));
    }

    [HostTest("m16-007 immutable payload views retain package size bounds beyond one MiB", HostLane.Fast, 13)]
    private static void LargePayloadView()
    {
        using var owner = new Owner(); const uint bytes = 2 * 1024 * 1024;
        byte* payload = (byte*)NativeMemory.Alloc(bytes);
        try
        {
            new Span<byte>(payload, (int)bytes).Clear(); payload[bytes - 1] = 0x6d;
            owner.Admission.Views[0].Data = payload; owner.Admission.Views[0].Bytes = bytes;
            owner.Begin(); uint fd = (uint)owner.Open();
            HostCheck.Equal(1L, owner.Call(17, fd, Output, 1, bytes - 1)); HostCheck.Equal((byte)0x6d, *owner.Memory.At(Output));
            HostCheck.Equal(0L, owner.Call(5, fd, Output)); HostCheck.Equal((ulong)bytes, *(ulong*)(owner.Memory.At(Output) + 48));
            HostCheck.Equal(0L, owner.Call(17, fd, Output, 1, bytes));
            HostCheck.True(Services.End(owner.State));
        }
        finally { NativeMemory.Free(payload); }
    }
}
