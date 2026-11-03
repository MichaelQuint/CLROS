using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Runtime.InteropServices;
using OS.Kernel.Compatibility.LinuxX64;

internal static unsafe partial class LinuxAppConfigurationTests
{
    [HostTest("m10.004.configuration legacy executable remains an immutable borrowed view")]
    private static void Legacy()
    {
        byte[] package = LinuxElfPackageTests.CapturePackage();
        LinuxDescriptorServices.View* views = stackalloc LinuxDescriptorServices.View[8];
        byte* paths = stackalloc byte[2048];
        Clear(views, paths);
        fixed (byte* source = package)
        {
            HostCheck.True(LinuxDescriptorServices.CreatePackageViews(source, (uint)package.Length, views, 8, paths, 2048, out uint count));
            HostCheck.Equal(1u, count);
            HostCheck.Equal("/app/apps/hello.elf", Path(views[0]));
            HostCheck.Equal(256UL, views[0].Inode);
            HostCheck.Equal(4096UL, views[0].Bytes);
            HostCheck.True(views[0].Data >= source && views[0].Data + views[0].Bytes == source + package.Length);
            HostCheck.True(new ReadOnlySpan<byte>(views[0].Data, 4).SequenceEqual("\u007fELF"u8));
        }
    }

    [HostTest("m10.004.configuration data views retain exact paths bytes and independent identities")]
    private static void ImmutableData()
    {
        byte[] package = Package(("apps/appsettings.json", "{\"mode\":\"immutable\"}"), ("apps/site/banner.txt", "selected-value"));
        LinuxDescriptorServices.View* views = stackalloc LinuxDescriptorServices.View[8];
        byte* paths = stackalloc byte[2048];
        Clear(views, paths);
        fixed (byte* source = package)
        {
            HostCheck.True(LinuxDescriptorServices.CreatePackageViews(source, (uint)package.Length, views, 8, paths, 2048, out uint count));
            HostCheck.Equal(3u, count);
            HostCheck.Equal("/app/apps/appsettings.json", Path(views[1]));
            HostCheck.Equal("/app/apps/site/banner.txt", Path(views[2]));
            HostCheck.Equal("{\"mode\":\"immutable\"}", Data(views[1]));
            HostCheck.Equal("selected-value", Data(views[2]));
            for (uint i = 0; i < count; i++)
            {
                HostCheck.Equal(256UL + i, views[i].Inode);
                HostCheck.True(views[i].Path == paths + i * 256 && views[i].Path[views[i].PathBytes] == 0);
                HostCheck.True(views[i].Data >= source && views[i].Data + views[i].Bytes <= source + package.Length);
            }

            HostCheck.True(views[3].Path == null && paths[3 * 256] == 0);
        }
    }

    [HostTest("m10.004.configuration descriptor reads use exact retained data copy boundary")]
    private static void DescriptorReads()
    {
        byte[] package = Package(("apps/config.json", "selected-value"));
        LinuxDescriptorServices.View* views = stackalloc LinuxDescriptorServices.View[8];
        byte* paths = stackalloc byte[2048];
        Clear(views, paths);
        using var owner = new LinuxDescriptorServicesTests.Owner();
        var original = owner.Admission;
        CopyBoundary boundary = new()
        {
            State = owner.State,
            User = original.User,
            Copy = original.CopyToUser
        };
        fixed (byte* source = package)
        {
            HostCheck.True(LinuxDescriptorServices.CreatePackageViews(source, (uint)package.Length, views, 8, paths, 2048, out uint count));
            owner.Admission.Views = views;
            owner.Admission.ViewCount = count;
            owner.Admission.User = new()
            {
                Context = &boundary,
                PageAccess = &PageAccess,
                CopyFromUser = &CopyInput,
                Write = &WriteOutput
            };
            owner.Admission.CopyToUser = &CopyPackageOutput;
            try
            {
                HostCheck.True(!LinuxDescriptorServices.ContainsPackageData(owner.State, views[1].Data, 1));
                owner.Begin();
                HostCheck.True(LinuxDescriptorServices.ContainsPackageData(owner.State, views[0].Data, 4));
                HostCheck.True(!LinuxDescriptorServices.ContainsPackageData(owner.State, source, 1));
                HostCheck.True(!LinuxDescriptorServices.ContainsPackageData(owner.State, views[1].Path, 1));
                HostCheck.True(!LinuxDescriptorServices.ContainsPackageData(owner.State, views[1].Data - 1, 2));
                HostCheck.True(!LinuxDescriptorServices.ContainsPackageData(owner.State, views[1].Data + views[1].Bytes - 1, 2));
                HostCheck.True(!LinuxDescriptorServices.ContainsPackageData(owner.State, views[1].Data, 0));
                HostCheck.True(!LinuxDescriptorServices.ContainsPackageData(owner.State, (byte*)(ulong.MaxValue - 1), 4));
                uint fd = checked((uint)owner.Open("/app/apps/config.json"));
                HostCheck.Equal(3u, fd);
                const ulong output = 0x100006000;
                owner.Context->FailOutput = true;
                HostCheck.Equal(-14L, owner.Call(0, fd, output, 8));
                HostCheck.Equal(0L, owner.Call(8, fd, 0, 1)); // Failed copy does not consume input.
                owner.Context->FailOutput = false;
                HostCheck.Equal(8L, owner.Call(0, fd, output, 8));
                HostCheck.Equal("selected", Encoding.ASCII.GetString(new ReadOnlySpan<byte>(owner.Memory.At(output), 8)));
                HostCheck.Equal(6L, owner.Call(0, fd, output, 8192));
                HostCheck.Equal("-value", Encoding.ASCII.GetString(new ReadOnlySpan<byte>(owner.Memory.At(output), 6)));
                HostCheck.Equal(0L, owner.Call(0, fd, output, 1));
                HostCheck.Equal(0L, owner.Call(3, fd));
                HostCheck.Equal(-30L, owner.Open("/app/apps/config.json", 1));
                HostCheck.Equal(-2L, owner.Open("/app/apps/undeclared.json"));
                HostCheck.True(LinuxDescriptorServices.End(owner.State));
                HostCheck.True(!LinuxDescriptorServices.ContainsPackageData(owner.State, views[1].Data, 1));
                HostCheck.Equal("selected-value", Data(views[1]));
            }
            finally
            {
                owner.Admission = original;
            }
        }
    }

    [UnmanagedCallersOnly]
    private static byte PageAccess(void* context, ulong page)
    {
        var c = (CopyBoundary*)context;
        return c->User.PageAccess(c->User.Context, page);
    }

    [UnmanagedCallersOnly]
    private static byte CopyInput(void* context, ulong address, byte* data, uint bytes)
    {
        var c = (CopyBoundary*)context;
        return c->User.CopyFromUser(c->User.Context, address, data, bytes);
    }

    [UnmanagedCallersOnly]
    private static byte WriteOutput(void* context, uint fd, byte* data, uint bytes)
    {
        var c = (CopyBoundary*)context;
        return c->User.Write(c->User.Context, fd, data, bytes);
    }

    [UnmanagedCallersOnly]
    private static byte CopyPackageOutput(void* context, ulong address, byte* data, uint bytes)
    {
        var c = (CopyBoundary*)context;
        return LinuxDescriptorServices.ContainsPackageData(c->State, data, bytes) ? c->Copy(c->User.Context, address, data, bytes) : (byte)0;
    }

    [HostTest("m10.004.configuration path observations require explicit valid package identity")]
    private static void PathAdmission()
    {
        using (var legacy = new LinuxDescriptorServicesTests.Owner())
        {
            legacy.Begin();
            foreach (ulong number in new ulong[]
            {
                4,
                6,
                79,
                89
            }

            )
                HostCheck.Equal(-38L, legacy.Call(number));
            HostCheck.True(LinuxDescriptorServices.End(legacy.State));
        }

        using var owner = new LinuxDescriptorServicesTests.Owner();
        var original = owner.Admission;
        owner.Admission.PackageProcessId = uint.MaxValue;
        HostCheck.True(!LinuxDescriptorServices.Begin(owner.State, owner.Admission));
        owner.Admission.PackageProcessId = 23;
        owner.Admission.ViewCount = 0;
        HostCheck.True(!LinuxDescriptorServices.Begin(owner.State, owner.Admission));
        owner.Admission.ViewCount = 1;
        var executable = owner.Admission.Views[0];
        owner.Admission.Views[0].Inode = 900;
        HostCheck.True(!LinuxDescriptorServices.Begin(owner.State, owner.Admission));
        owner.Admission.Views[0] = executable;
        byte* child = stackalloc byte[23];
        "/app/fixture.data/child"u8.CopyTo(new Span<byte>(child, 23));
        owner.Admission.Views[1] = new()
        {
            Path = child,
            PathBytes = 23,
            Inode = 257
        };
        owner.Admission.ViewCount = 2;
        HostCheck.True(!LinuxDescriptorServices.Begin(owner.State, owner.Admission));
        HostCheck.Equal(0u, owner.State->ActiveDescriptors);
        owner.Admission.Views[1] = default;
        owner.Admission = original;
    }

    [HostTest("m10.004.configuration stat paths and declared parent directories match retained metadata")]
    private static void PathMetadata()
    {
        using var owner = new LinuxDescriptorServicesTests.Owner();
        owner.Admission.PackageProcessId = 23;
        owner.Begin();
        const ulong path = 0x100000000, output = path + 24576;
        uint fd = checked((uint)owner.Open(flags: 0x100 | LinuxDescriptorServices.CloseOnExec));
        HostCheck.Equal(0x8000L, owner.Call(72, fd, 3)); // O_NOCTTY is not a file-status flag.
        HostCheck.Equal(0L, owner.Call(5, fd, output));
        byte[] expected = new ReadOnlySpan<byte>(owner.Memory.At(output), 144).ToArray();
        foreach (ulong number in new ulong[]
        {
            4,
            6
        }

        )
        {
            owner.Text(path, "/app/fixture.data");
            HostCheck.Equal(0L, owner.Call(number, path, output));
            HostCheck.True(new ReadOnlySpan<byte>(owner.Memory.At(output), 144).SequenceEqual(expected));
        }

        owner.Text(path, "app/fixture.data");
        HostCheck.Equal(0L, owner.Call(4, path, output));
        foreach (string directory in new[]
        {
            "/",
            "/app",
            "/app/",
            "/proc",
            "/proc/23",
            "/proc/self/"
        }

        )
        {
            owner.Text(path, directory);
            HostCheck.Equal(0L, owner.Call(4, path, output));
            HostCheck.Equal(0x416du, *(uint*)(owner.Memory.At(output) + 24));
        }

        owner.Text(path, "/app");
        HostCheck.Equal(0L, owner.Call(4, path, output));
        ulong inode = *(ulong*)(owner.Memory.At(output) + 8);
        HostCheck.Equal(0L, owner.Call(6, path, output));
        HostCheck.Equal(inode, *(ulong*)(owner.Memory.At(output) + 8));
        foreach (string missing in new[]
        {
            "/app/undeclared",
            "/app/wwwroot",
            "/proc/24/exe",
            "/etc/passwd"
        }

        )
        {
            owner.Text(path, missing);
            HostCheck.Equal(-2L, owner.Call(4, path, output));
        }

        owner.Text(path, "/app/fixture.data/");
        HostCheck.Equal(-20L, owner.Call(4, path, output));
        HostCheck.Equal(0L, owner.Call(3, fd));
        HostCheck.True(LinuxDescriptorServices.End(owner.State));
    }

    [HostTest("m10.004.configuration cwd and proc links report actual owner and exact declared executable")]
    private static void ProcessPaths()
    {
        using var owner = new LinuxDescriptorServicesTests.Owner();
        owner.Admission.PackageProcessId = 23;
        owner.Begin();
        const ulong path = 0x100000000, output = path + 24576;
        new Span<byte>(owner.Memory.At(output), 144).Fill(0xa5);
        HostCheck.Equal(-34L, owner.Call(79, output, 1));
        HostCheck.Equal((byte)0xa5, *owner.Memory.At(output));
        HostCheck.Equal(2L, owner.Call(79, output, 256));
        HostCheck.Equal((byte)'/', *owner.Memory.At(output));
        HostCheck.Equal((byte)0, owner.Memory.At(output)[1]);
        HostCheck.Equal((byte)0xa5, owner.Memory.At(output)[2]);
        foreach (var item in new[]
        {
            ("/proc/self", "23"),
            ("/proc/23/exe", "/app/fixture.data"),
            ("/proc/self/exe", "/app/fixture.data")
        }

        )
        {
            owner.Text(path, item.Item1);
            new Span<byte>(owner.Memory.At(output), 144).Fill(0xa5);
            HostCheck.Equal((long)item.Item2.Length, owner.Call(89, path, output, 4096));
            HostCheck.Equal(item.Item2, Encoding.ASCII.GetString(new ReadOnlySpan<byte>(owner.Memory.At(output), item.Item2.Length)));
            HostCheck.Equal((byte)0xa5, owner.Memory.At(output)[item.Item2.Length]);
            HostCheck.Equal(1L, owner.Call(89, path, output, 1));
            HostCheck.Equal((byte)item.Item2[0], *owner.Memory.At(output));
            HostCheck.Equal(0L, owner.Call(6, path, output));
            HostCheck.Equal(0xa1ffu, *(uint*)(owner.Memory.At(output) + 24));
            HostCheck.Equal((ulong)item.Item2.Length, *(ulong*)(owner.Memory.At(output) + 48));
            HostCheck.Equal(0L, owner.Call(4, path, output));
            HostCheck.Equal(item.Item1 == "/proc/self" ? 0x416du : 0x8124u, *(uint*)(owner.Memory.At(output) + 24));
        }

        foreach (string plain in new[]
        {
            "/",
            "/app",
            "/app/fixture.data",
            "/proc",
            "/proc/23"
        }

        )
        {
            owner.Text(path, plain);
            HostCheck.Equal(-22L, owner.Call(89, path, output, 256));
        }

        HostCheck.True(LinuxDescriptorServices.End(owner.State));
        HostCheck.Equal(-14L, owner.Call(79, output, 256));
    }

    [HostTest("m10.004.configuration malformed paths and copy failures preserve user bytes and owner")]
    private static void PathFailures()
    {
        using var owner = new LinuxDescriptorServicesTests.Owner();
        owner.Admission.PackageProcessId = 1;
        owner.Begin();
        const ulong path = 0x100000000, output = path + 24576;
        foreach (string invalid in new[]
        {
            "/app/../escape",
            "/app/./fixture.data",
            "/app//fixture.data",
            "/app\\fixture.data"
        }

        )
        {
            owner.Text(path, invalid);
            HostCheck.Equal(-13L, owner.Call(4, path, output));
        }

        foreach (string child in new[]
        {
            "/app/fixture.data/child",
            "/proc/self/exe/child",
            "/proc/1/exe/child",
            "/proc/meminfo/child",
            "/proc/self/maps/child"
        }

        )
        {
            owner.Text(path, child);
            foreach (ulong number in new ulong[]
            {
                4,
                6,
                89
            }

            )
                HostCheck.Equal(-20L, owner.Call(number, path, output, 256));
        }

        owner.Text(path, new string('a', 256));
        HostCheck.Equal(-36L, owner.Call(6, path, output));
        owner.Text(path, "");
        HostCheck.Equal(-2L, owner.Call(4, path, output));
        HostCheck.Equal(-14L, owner.Call(4, 0, output));
        HostCheck.Equal(-14L, owner.Call(79, 0, 256));
        owner.Text(path, "/proc/self/exe");
        HostCheck.Equal(-22L, owner.Call(89, path, output, 0));
        HostCheck.Equal(-22L, owner.Call(89, path, output, ulong.MaxValue));
        HostCheck.Equal(-14L, owner.Call(89, path, 0, 256));
        new Span<byte>(owner.Memory.At(output), 144).Fill(0xa5);
        owner.Context->FailOutput = true;
        HostCheck.Equal(-14L, owner.Call(4, path, output));
        HostCheck.Equal(-14L, owner.Call(89, path, output, 256));
        HostCheck.True(new ReadOnlySpan<byte>(owner.Memory.At(output), 144).IndexOfAnyExcept((byte)0xa5) < 0);
        HostCheck.Equal(3u, owner.State->ActiveDescriptors);
        HostCheck.True(LinuxDescriptorServices.Audit(owner.State) && LinuxDescriptorServices.End(owner.State));
    }

    [HostTest("m10.004.configuration path calls traverse real thread and gateway ownership with matching process id")]
    private static void ThreadedPaths()
    {
        using var thread = new LinuxThreadServicesTests.Model(futex: true);
        const ulong key = LinuxThreadServicesTests.Key, path = 0x401000, output = 0x403400;
        using (var mismatch = new LinuxDescriptorServicesTests.Owner())
        {
            mismatch.Admission.PackageProcessId = 23;
            mismatch.Admission.User = thread.DescriptorAccess;
            mismatch.Admission.CopyToUser = thread.UserCopyOut;
            mismatch.Begin();
            HostCheck.True(!thread.Service->EnablePipes(key, 2, mismatch.State));
            HostCheck.True(LinuxDescriptorServices.End(mismatch.State));
        }

        using var files = new LinuxDescriptorServicesTests.Owner();
        files.Admission.PackageProcessId = 101;
        files.Admission.User = thread.DescriptorAccess;
        files.Admission.CopyToUser = thread.UserCopyOut;
        files.Begin();
        HostCheck.True(thread.Service->EnablePipes(key, 2, files.State));
        var identity = thread.Start();
        thread.Kernel.Dispatch();
        byte* scratch = stackalloc byte[64];
        LinuxSyscallGateState gate = default;
        HostCheck.Equal(LinuxSyscallGateStatus.Active, LinuxSyscallGateway.Begin(ref gate, new()
        {
            UserRoot = thread.Kernel.Owner.Root,
            MaximumCalls = 16,
            MaximumWrite = 64,
            Scratch = scratch,
            ScratchBytes = 64,
            FirmwareExited = true,
            OwnedBoundaryValidated = true,
            EnableStartup = true,
            EnableDescriptors = true,
            EnableThreads = true
        }, thread.GatewayAccess));
        foreach (ulong number in new ulong[]
        {
            4,
            6,
            79,
            89,
            39
        }

        )
        {
            string text = number == 89 ? "/proc/self" : "/app/fixture.data";
            byte[] input = Encoding.ASCII.GetBytes(text);
            input.CopyTo(new Span<byte>(thread.At(path), input.Length));
            thread.At(path)[input.Length] = 0;
            var image = thread.CurrentImage();
            var frame = image->Frame;
            frame.Number = number;
            frame.Argument0 = number == 79 ? output : path;
            frame.Argument1 = number == 79 ? 256u : output;
            frame.Argument2 = 256;
            var result = LinuxSyscallGateway.Dispatch(ref gate, ref frame);
            image->Frame = frame;
            HostCheck.Equal(LinuxSyscallAction.Return, result.Action);
            HostCheck.Equal(number == 79 ? 2L : number == 89 ? 3L : number == 39 ? 101L : 0L, result.Value);
            if (number == 89)
                HostCheck.Equal("101", Encoding.ASCII.GetString(new ReadOnlySpan<byte>(thread.At(output), 3)));
        }

        HostCheck.Equal(0u, gate.BytesWritten);
        HostCheck.Equal(3u, files.State->ActiveDescriptors);
        HostCheck.True(thread.Service->Shutdown(key, 2, 0));
        HostCheck.True(thread.Service->CommitExit(key, 2, thread.Kernel.Capture(identity), out _));
        thread.Kernel.Confirm();
        HostCheck.True(thread.Service->Reap(key, 2, identity, thread.Kernel.SupervisorRsp, out _));
        HostCheck.True(thread.Service->Release(key, 2));
        HostCheck.True(LinuxDescriptorServices.End(files.State));
    }

    [HostTest("m10.004.configuration seven data files fit existing eight views and envelope path boundary")]
    private static void Capacity()
    {
        var entries = Enumerable.Range(0, 7).Select(i => ("apps/" + i + ".txt", "value-" + i)).ToArray();
        entries[6] = ("apps/" + new string('x', 123), "long-path-value");
        byte[] package = Package(entries);
        LinuxDescriptorServices.View* views = stackalloc LinuxDescriptorServices.View[8];
        byte* paths = stackalloc byte[2048];
        Clear(views, paths);
        fixed (byte* source = package)
        {
            HostCheck.True(LinuxDescriptorServices.CreatePackageViews(source, (uint)package.Length, views, 8, paths, 2048, out uint count));
            HostCheck.Equal(8u, count);
            HostCheck.Equal(133u, views[7].PathBytes);
            HostCheck.True(views[7].Path[133] == 0 && paths[2047] == 0);
            HostCheck.Equal("long-path-value", Data(views[7]));
        }

        Reject(package, 7);
        entries[6] = ("apps/" + new string('x', 124), "too-long");
        Reject(Package(entries));
    }

    [HostTest("m10.004.configuration malformed package and namespace inputs publish nothing")]
    private static void InvalidPackage()
    {
        byte[] digest = Package(("apps/config.json", "value"));
        digest[^1] ^= 1;
        Reject(digest);
        byte[] header = Package(("apps/config.json", "value"));
        header[40] = 1;
        Reject(header);
        Reject(Package(("apps/duplicate", "one"), ("apps/duplicate", "two")));
        Reject(Package(("apps/../escape", "value")));
        Reject(Package(("/apps/absolute", "value")));
        Reject(Package(("apps/hello.elf", "executable-collision")));
    }

    [HostTest("m10.004.configuration aliases invalid extents and occupied metadata preserve ownership")]
    private static void InvalidStorage()
    {
        byte[] package = Package(("apps/config", "value")), original = package.ToArray();
        LinuxDescriptorServices.View* views = stackalloc LinuxDescriptorServices.View[8];
        byte* paths = stackalloc byte[2048];
        Clear(views, paths);
        fixed (byte* source = package)
        {
            HostCheck.True(!LinuxDescriptorServices.CreatePackageViews(source, (uint)package.Length, views, 0, paths, 2048, out _));
            HostCheck.True(!LinuxDescriptorServices.CreatePackageViews(source, (uint)package.Length, views, 9, paths, 2048, out _));
            HostCheck.True(!LinuxDescriptorServices.CreatePackageViews(source, (uint)package.Length, views, 8, paths, 2047, out _));
            HostCheck.True(!LinuxDescriptorServices.CreatePackageViews(source, (uint)package.Length, views, 8, paths, 2049, out _));
            HostCheck.True(!LinuxDescriptorServices.CreatePackageViews(source, (uint)package.Length, (LinuxDescriptorServices.View*)((byte*)views + 1), 8, paths, 2048, out _));
            HostCheck.True(!LinuxDescriptorServices.CreatePackageViews(source, (uint)package.Length, views, 8, source, 2048, out _));
            HostCheck.True(!LinuxDescriptorServices.CreatePackageViews(source, (uint)package.Length, (LinuxDescriptorServices.View*)source, 8, paths, 2048, out _));
            HostCheck.True(!LinuxDescriptorServices.CreatePackageViews(source, (uint)package.Length, views, 8, (byte*)views, 2048, out _));
            HostCheck.True(!LinuxDescriptorServices.CreatePackageViews(source, (uint)package.Length, (LinuxDescriptorServices.View*)(ulong.MaxValue - 7), 8, paths, 2048, out _));
            HostCheck.True(package.SequenceEqual(original));
            views[0].Inode = 900;
            HostCheck.True(!LinuxDescriptorServices.CreatePackageViews(source, (uint)package.Length, views, 8, paths, 2048, out uint count));
            HostCheck.Equal(0u, count);
            HostCheck.Equal(900UL, views[0].Inode);
            HostCheck.True(paths[0] == 0);
            views[0] = default;
            paths[2047] = 42;
            HostCheck.True(!LinuxDescriptorServices.CreatePackageViews(source, (uint)package.Length, views, 8, paths, 2048, out count));
            HostCheck.Equal(0u, count);
            HostCheck.True(views[0].Path == null && paths[2047] == 42);
        }
    }

    private static void Reject(byte[] package, uint capacity = 8)
    {
        byte[] original = package.ToArray();
        LinuxDescriptorServices.View* views = stackalloc LinuxDescriptorServices.View[8];
        byte* paths = stackalloc byte[2048];
        Clear(views, paths);
        fixed (byte* source = package)
        {
            HostCheck.True(!LinuxDescriptorServices.CreatePackageViews(source, (uint)package.Length, views, capacity, paths, 2048, out uint count));
            HostCheck.Equal(0u, count);
            HostCheck.True(package.SequenceEqual(original));
            HostCheck.True(new ReadOnlySpan<byte>(views, 8 * sizeof(LinuxDescriptorServices.View)).IndexOfAnyExcept((byte)0) < 0);
            HostCheck.True(new ReadOnlySpan<byte>(paths, 2048).IndexOfAnyExcept((byte)0) < 0);
        }
    }

    private static void Clear(LinuxDescriptorServices.View* views, byte* paths)
    {
        new Span<byte>(views, 8 * sizeof(LinuxDescriptorServices.View)).Clear();
        new Span<byte>(paths, 2048).Clear();
    }

    private static string Path(LinuxDescriptorServices.View view) => Encoding.ASCII.GetString(new ReadOnlySpan<byte>(view.Path, (int)view.PathBytes));
    private static string Data(LinuxDescriptorServices.View view) => Encoding.ASCII.GetString(new ReadOnlySpan<byte>(view.Data, (int)view.Bytes));
    // Independent byte envelope: retain the existing small ELF vector, then
    // append literal V2 records using BCL hashing rather than the package writer.
    private static byte[] Package(params (string Path, string Data)[] entries)
    {
        byte[] v1 = LinuxElfPackageTests.CapturePackage();
        var records = entries.Select(x => (Path: Encoding.ASCII.GetBytes(x.Path), Data: Encoding.ASCII.GetBytes(x.Data))).ToArray();
        int total = records.Sum(x => 48 + x.Path.Length + x.Data.Length);
        byte[] result = new byte[v1.Length + total];
        v1.CopyTo(result, 0);
        W32(8, 2);
        W32(12, 2);
        W32(32, (uint)total);
        W32(36, (uint)records.Length);
        int at = v1.Length;
        foreach (var record in records)
        {
            W32(at, (uint)(48 + record.Path.Length + record.Data.Length));
            W32(at + 4, (uint)record.Path.Length);
            W32(at + 8, (uint)record.Data.Length);
            SHA256.HashData(record.Data).CopyTo(result, at + 16);
            record.Path.CopyTo(result, at + 48);
            record.Data.CopyTo(result, at + 48 + record.Path.Length);
            at += 48 + record.Path.Length + record.Data.Length;
        }

        return result;
        void W32(int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(offset), value);
    }
}
