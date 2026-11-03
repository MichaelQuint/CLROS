using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using OS.Tooling;

internal static class NativeAotPayloadTests
{
    [HostTest("nativeaot.payload SDK and application targets match the qualification policy")]
    private static void BuildPolicy()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        using JsonDocument global = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "global.json")));
        HostCheck.Equal(NativeAotPayload.SdkVersion, global.RootElement.GetProperty("sdk").GetProperty("version").GetString()!);
        HostCheck.True(global.RootElement.GetProperty("sdk").GetProperty("allowPrerelease").GetBoolean());
        foreach (string app in new[] { "DiagnosticApp", "FetchApp", "HelloSharpFs", "MinimalApiSmoke",
            "NativeAotHello", "NativeAotRuntimeSmoke", "TinyHttpServer" })
        {
            XDocument project = XDocument.Load(Path.Combine(root, "apps", app, app + ".csproj"));
            HostCheck.Equal(NativeAotPayload.TargetFramework, project.Descendants("TargetFramework").Single().Value);
            HostCheck.Equal("14.0", project.Descendants("LangVersion").Single().Value);
        }
    }

    [HostTest("nativeaot.payload hashes downloaded runtime archives and detects changed bytes")]
    private static void DownloadedRuntimeInputs()
    {
        WithPackages((assets, archive) =>
        {
            var before = CapturePackages(assets);
            HostCheck.Equal(4, before.Count);
            HostCheck.Equal(Convert.ToHexString(SHA256.HashData(new byte[] { 1, 2, 3, 4 })), before[archive]);
            DateTime timestamp = File.GetLastWriteTimeUtc(archive);
            File.WriteAllBytes(archive, [4, 3, 2, 1]);
            File.SetLastWriteTimeUtc(archive, timestamp);
            var after = CapturePackages(assets);
            HostCheck.True(before[archive] != after[archive]);
            HostCheck.True(before.Where(row => row.Key != archive).All(row => after[row.Key] == row.Value));
        });
    }

    [HostTest("nativeaot.payload rejects absent downloaded packs and mismatched package pins")]
    private static void PackagePinFailures()
    {
        WithPackages((assets, archive) =>
        {
            JsonObject changed = (JsonObject)assets.DeepClone();
            Downloads(changed).Clear();
            RejectPackages(changed, "native_runtime_pack_missing");
            changed = (JsonObject)assets.DeepClone();
            Downloads(changed)[0]!["version"] = "[10.0.12, 10.0.12]";
            RejectPackages(changed, "native_runtime_pin_mismatch");
            changed = (JsonObject)assets.DeepClone();
            Downloads(changed)[0]!["version"] = "[" + NativeAotPayload.RuntimeVersion + ", )";
            RejectPackages(changed, "native_runtime_pin_mismatch");
            changed = (JsonObject)assets.DeepClone();
            ((JsonObject)changed["libraries"]!).Remove("Microsoft.NET.ILLink.Tasks/" + NativeAotPayload.RuntimeVersion);
            RejectPackages(changed, "native_compiler_package_missing");
            changed = (JsonObject)assets.DeepClone();
            string library = "Microsoft.DotNet.ILCompiler/" + NativeAotPayload.RuntimeVersion;
            changed["libraries"]![library]!["path"] = "microsoft.dotnet.ilcompiler/10.0.12";
            RejectPackages(changed, "native_package_path");
            changed = (JsonObject)assets.DeepClone();
            string escaped = "..\\outside/" + NativeAotPayload.RuntimeVersion;
            changed["libraries"]![escaped] = new JsonObject { ["type"] = "package", ["path"] = escaped };
            RejectPackages(changed, "native_package_path");
            File.Delete(archive);
            RejectPackages(assets, "native_package_empty");
        });
    }

    private static void WithPackages(Action<JsonObject, string> check)
    {
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "clros-nativeaot-packages-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(root);
        try
        {
            string[] libraries = ["Microsoft.DotNet.ILCompiler", "Microsoft.NET.ILLink.Tasks",
                "runtime.linux-musl-x64.Microsoft.DotNet.ILCompiler"];
            var packageRows = new JsonObject();
            foreach (string name in libraries)
            {
                string relative = name.ToLowerInvariant() + "/" + NativeAotPayload.RuntimeVersion;
                Directory.CreateDirectory(Path.Combine(root, relative));
                File.WriteAllText(Path.Combine(root, relative, "compiler-input"), name);
                packageRows[name + "/" + NativeAotPayload.RuntimeVersion] = new JsonObject { ["type"] = "package", ["path"] = relative };
            }
            const string runtime = "Microsoft.NETCore.App.Runtime.NativeAOT.linux-musl-x64";
            string archive = Path.Combine(root, runtime.ToLowerInvariant(), NativeAotPayload.RuntimeVersion, "runtimes", "linux-musl-x64", "native", "libzstd.a");
            Directory.CreateDirectory(Path.GetDirectoryName(archive)!);
            File.WriteAllBytes(archive, [1, 2, 3, 4]);
            var assets = new JsonObject
            {
                ["libraries"] = packageRows,
                ["packageFolders"] = new JsonObject { [root] = new JsonObject() },
                ["project"] = new JsonObject
                {
                    ["frameworks"] = new JsonObject
                    {
                        [NativeAotPayload.TargetFramework] = new JsonObject
                        {
                            ["downloadDependencies"] = new JsonArray
                    {
                        new JsonObject { ["name"] = runtime, ["version"] = $"[{NativeAotPayload.RuntimeVersion}, {NativeAotPayload.RuntimeVersion}]" }
                    }
                        }
                    }
                }
            };
            check(assets, archive);
        }
        finally
        {
            HostCheck.True(root.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase));
            Directory.Delete(root, recursive: true);
        }
    }

    private static JsonArray Downloads(JsonObject assets) => (JsonArray)assets["project"]!["frameworks"]![NativeAotPayload.TargetFramework]!["downloadDependencies"]!;
    private static IReadOnlyDictionary<string, string> CapturePackages(JsonObject assets)
    {
        using JsonDocument document = JsonDocument.Parse(assets.ToJsonString());
        return NativeAotPayload.CapturePackageInputs(document.RootElement);
    }
    private static void RejectPackages(JsonObject assets, string reason)
    {
        try
        {
            CapturePackages(assets);
            throw new InvalidOperationException("invalid package closure accepted");
        }
        catch (InvalidDataException error) { HostCheck.Equal(reason, error.Message); }
    }

    [HostTest("nativeaot.payload archives warm managed and future-dated native caches after same-time source edits")]
    private static void WarmBuildCache()
    {
        WithBuildCache((root, app, destination) =>
        {
            string source = Path.Combine(root, "apps", app, "Program.cs");
            File.WriteAllText(source, "old source");
            DateTime timestamp = File.GetLastWriteTimeUtc(source);
            File.WriteAllText(source, "new source");
            File.SetLastWriteTimeUtc(source, timestamp);
            string bin = Path.Combine(root, "apps", app, "bin");
            string obj = Path.Combine(root, "apps", app, "obj");
            Directory.CreateDirectory(bin);
            Directory.CreateDirectory(Path.Combine(obj, "native"));
            File.WriteAllBytes(Path.Combine(bin, app + ".dll"), [1, 2, 3]);
            File.WriteAllBytes(Path.Combine(obj, "native", app + ".o"), [4, 5, 6]);
            DateTime future = DateTime.UtcNow.AddYears(5);
            File.SetLastWriteTimeUtc(Path.Combine(obj, "native", app + ".o"), future);
            using var lease = NativeAotPayload.OpenQualificationLease(root);
            NativeAotPayload.ArchiveBuildCache(root, app, destination);
            HostCheck.True(!Directory.Exists(bin));
            HostCheck.True(!Directory.Exists(obj));
            string archive = Path.Combine(destination, "retained-build-cache", app);
            HostCheck.True(File.ReadAllBytes(Path.Combine(archive, "bin", app + ".dll")).SequenceEqual(new byte[] { 1, 2, 3 }));
            HostCheck.True(File.ReadAllBytes(Path.Combine(archive, "obj", "native", app + ".o")).SequenceEqual(new byte[] { 4, 5, 6 }));
            HostCheck.Equal(future, File.GetLastWriteTimeUtc(Path.Combine(archive, "obj", "native", app + ".o")));
            HostCheck.Equal("new source", File.ReadAllText(source));
            HostCheck.Equal(timestamp, File.GetLastWriteTimeUtc(source));
            RejectCache(() => NativeAotPayload.ArchiveBuildCache(root, app, destination), "native_cache_archive_exists");
        });
    }

    [HostTest("nativeaot.payload serializes distinct qualification outputs and rejects redirected cache trees before moving")]
    private static void BuildCacheOwnership()
    {
        WithBuildCache((root, app, destination) =>
        {
            using (var lease = NativeAotPayload.OpenQualificationLease(root))
                RejectCache(() => NativeAotPayload.OpenQualificationLease(root).Dispose(), "native_qualification_busy");
            using var nextLease = NativeAotPayload.OpenQualificationLease(root);
            string appRoot = Path.Combine(root, "apps", app), bin = Path.Combine(appRoot, "bin"), obj = Path.Combine(appRoot, "obj");
            Directory.CreateDirectory(bin);
            Directory.CreateDirectory(obj);
            File.WriteAllText(Path.Combine(bin, "sentinel"), "retained");
            RejectCache(() => NativeAotPayload.ArchiveBuildCache(root, "../outside", destination), "native_payload_app");
            RejectCache(() => NativeAotPayload.ArchiveBuildCache(root, app, obj), "native_cache_output_overlap");
            string target = Path.Combine(root, "outside"), link = Path.Combine(obj, "redirect");
            Directory.CreateDirectory(target);
            File.WriteAllText(Path.Combine(target, "sentinel"), "outside");
            if (OperatingSystem.IsWindows())
                TestJunction.Create(link, target);
            else
                Directory.CreateSymbolicLink(link, target);
            try
            {
                RejectCache(() => NativeAotPayload.ArchiveBuildCache(root, app, destination), "native_cache_reparse_point");
                HostCheck.Equal("retained", File.ReadAllText(Path.Combine(bin, "sentinel")));
                HostCheck.Equal("outside", File.ReadAllText(Path.Combine(target, "sentinel")));
                HostCheck.True(!Directory.Exists(Path.Combine(destination, "retained-build-cache")));
            }
            finally { Directory.Delete(link); }
            NativeAotPayload.ArchiveBuildCache(root, app, destination);
            HostCheck.True(!Directory.Exists(bin));
            HostCheck.True(!Directory.Exists(obj));
        });
    }

    private static void WithBuildCache(Action<string, string, string> check)
    {
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "clros-nativeaot-build-" + Guid.NewGuid().ToString("N")));
        const string app = "MinimalApiSmoke";
        string destination = Path.Combine(root, "artifacts", "qualification");
        Directory.CreateDirectory(Path.Combine(root, "apps", app));
        Directory.CreateDirectory(destination);
        try
        {
            check(root, app, destination);
        }
        finally
        {
            string temporaryRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) + Path.DirectorySeparatorChar;
            if (!root.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("temporary_root_escape");
            Directory.Delete(root, recursive: true);
        }
    }

    private static void RejectCache(Action action, string reason)
    {
        try
        {
            action();
            throw new InvalidOperationException("unsafe build cache accepted");
        }
        catch (InvalidDataException error) { HostCheck.Equal(reason, error.Message); }
        catch (ToolFailure error) { HostCheck.Equal(reason, error.Reason); }
    }

    // Independent ELF byte vector: one RX load, one RW load and optional TLS.
    private static byte[] Image(bool tls = true)
    {
        byte[] bytes = new byte[4096];
        "\u007fELF"u8.CopyTo(bytes);
        bytes[4] = 2;
        bytes[5] = 1;
        bytes[6] = 1;
        W16(bytes, 16, 2);
        W16(bytes, 18, 62);
        W32(bytes, 20, 1);
        W64(bytes, 24, 0x400200);
        W64(bytes, 32, 64);
        W16(bytes, 52, 64);
        W16(bytes, 54, 56);
        W16(bytes, 56, (ushort)(tls ? 3 : 2));
        Row(bytes, 64, 1, 5, 0, 0x400000, 1024, 1024, 4096);
        Row(bytes, 120, 1, 6, 1024, 0x401400, 512, 1024, 4096);
        if (tls)
            Row(bytes, 176, 7, 4, 1024, 0x401400, 16, 64, 16);
        return bytes;
    }

    [HostTest("nativeaot.payload static ELF loads entry TLS and input preservation")]
    private static void StaticImage()
    {
        byte[] image = Image();
        byte[] original = image.ToArray();
        var inspected = NativeAotPayload.Inspect(image);
        HostCheck.Equal(0x400200UL, inspected.Entry);
        HostCheck.Equal(3, inspected.ProgramHeaderCount);
        HostCheck.Equal(2, inspected.Loads.Count);
        HostCheck.Equal(0x401400UL, inspected.Tls!.Address);
        HostCheck.Equal(64UL, inspected.Tls.MemorySize);
        HostCheck.Equal(Convert.ToHexString(SHA256.HashData(original)), inspected.Sha256);
        HostCheck.True(image.SequenceEqual(original));
        HostCheck.True(NativeAotPayload.Inspect(Image(false)).Tls is null);
    }

    [HostTest("nativeaot.payload rejects every truncated header and program table boundary")]
    private static void Truncation()
    {
        byte[] bytes = Image();
        for (int length = 0; length < 232; length++)
            Reject(bytes[..length]);
        Mutate(bytes, b => W64(b, 32, ulong.MaxValue));
        Mutate(bytes, b => W64(b, 32, 63));
        Mutate(bytes, b => W16(b, 56, 129));
        Mutate(bytes, b => W16(b, 56, 0));
        Mutate(bytes, b => W16(b, 54, 55));
    }

    [HostTest("nativeaot.payload rejects unsupported ABI architecture dynamic PIE and executable stack")]
    private static void Unsupported()
    {
        byte[] bytes = Image();
        foreach (int index in new[] { 0, 4, 5, 6, 7, 8, 9, 15 })
            Mutate(bytes, b => b[index] = 0xff);
        Mutate(bytes, b => W16(b, 16, 3)); // ET_DYN/PIE
        Mutate(bytes, b => W16(b, 18, 183)); // AArch64
        Mutate(bytes, b => W32(b, 20, 2));
        Mutate(bytes, b => W32(b, 48, 1));
        Mutate(bytes, b => W32(b, 176, 3)); // PT_INTERP
        Mutate(bytes, b => W32(b, 176, 2)); // PT_DYNAMIC
        Mutate(bytes, b => { W32(b, 176, 0x6474e551); W32(b, 180, 7); });
    }

    [HostTest("nativeaot.payload checks file memory alignment W^X overlap and file-backed entry")]
    private static void LoadBounds()
    {
        byte[] bytes = Image();
        Mutate(bytes, b => W64(b, 72, ulong.MaxValue));
        Mutate(bytes, b => W64(b, 96, 1025));
        Mutate(bytes, b => W64(b, 104, 0));
        Mutate(bytes, b => W64(b, 80, ulong.MaxValue - 100));
        Mutate(bytes, b => W64(b, 80, 0));
        Mutate(bytes, b => W64(b, 80, 0x800000000000UL));
        Mutate(bytes, b => W64(b, 104, NativeAotPayload.MaximumMappedBytes + 1));
        Mutate(bytes, b => W64(b, 112, 3));
        Mutate(bytes, b => W64(b, 112, 0x800000));
        Mutate(bytes, b => W32(b, 68, 7));
        Mutate(bytes, b => W32(b, 68, 8));
        Mutate(bytes, b => W32(b, 68, 1));
        Mutate(bytes, b => W64(b, 136, 0x400000));
        Mutate(bytes, b => W64(b, 24, 0x401400));
        Mutate(bytes, b => W64(b, 24, 0x400400));
        Mutate(bytes, b => W64(b, 96, 0));
        byte[] zeroAlignment = Image();
        W64(zeroAlignment, 112, 0);
        HostCheck.Equal(0UL, NativeAotPayload.Inspect(zeroAlignment).Loads[0].Alignment);
        byte[] pageOverlap = Image(false);
        W64(pageOverlap, 128, 2048);
        W64(pageOverlap, 136, 0x400800);
        W64(pageOverlap, 168, 1);
        Reject(pageOverlap); // Disjoint byte intervals still share an RX/RW page.
        byte[] incongruent = Image(false);
        W64(incongruent, 168, 1);
        W64(incongruent, 136, 0x401401);
        Reject(incongruent); // p_align=1 does not relax page-offset congruence.
    }

    [HostTest("nativeaot.payload TLS requires original load and initialized-byte association")]
    private static void TlsBounds()
    {
        byte[] bytes = Image();
        Mutate(bytes, b => W64(b, 192, 0x401900));
        Mutate(bytes, b => W64(b, 184, 1025));
        Mutate(bytes, b => W64(b, 216, 1025));
        Mutate(bytes, b => W64(b, 208, 65));
        Mutate(bytes, b => { W32(b, 120, 7); W32(b, 124, 4); });
    }

    [HostTest("nativeaot.payload CLI rejects malformed inputs and wrong builder without false success")]
    private static void CommandFailures()
    {
        using var output = new StringWriter();
        HostCheck.Equal(1, NativeAotPayload.RunAsync(".", ["unknown"], output).GetAwaiter().GetResult());
        HostCheck.True(output.ToString().Contains("TEST_FAIL m16-001") && !output.ToString().Contains("TEST_PASS m16-001"));
        if (!OperatingSystem.IsLinux())
        {
            output.GetStringBuilder().Clear();
            HostCheck.Equal(1, NativeAotPayload.RunAsync(".", ["qualify", "--output", "unused"], output).GetAwaiter().GetResult());
            HostCheck.True(output.ToString().Contains("linux_builder_required") && !Directory.Exists("unused"));
        }
    }

    private static void Mutate(byte[] source, Action<byte[]> edit)
    {
        byte[] bytes = source.ToArray();
        edit(bytes);
        Reject(bytes);
    }
    private static void Reject(byte[] bytes)
    {
        byte[] original = bytes.ToArray();
        try
        {
            NativeAotPayload.Inspect(bytes);
            throw new InvalidOperationException("malformed ELF accepted");
        }
        catch (InvalidDataException) { }
        HostCheck.True(bytes.SequenceEqual(original));
    }
    private static void Row(byte[] b, int at, uint type, uint flags, ulong offset, ulong address, ulong file, ulong memory, ulong align)
    {
        W32(b, at, type);
        W32(b, at + 4, flags);
        W64(b, at + 8, offset);
        W64(b, at + 16, address);
        W64(b, at + 32, file);
        W64(b, at + 40, memory);
        W64(b, at + 48, align);
    }
    private static void W16(byte[] b, int at, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(at), value);
    private static void W32(byte[] b, int at, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(at), value);
    private static void W64(byte[] b, int at, ulong value) => BinaryPrimitives.WriteUInt64LittleEndian(b.AsSpan(at), value);
}
