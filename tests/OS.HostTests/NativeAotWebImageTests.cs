using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Manifest;
using OS.Tooling;

internal static unsafe class NativeAotWebImageTests
{
    private static byte[] Image()
    {
        byte[] original = LinuxElfPackageTests.CapturePackage();
        LinuxElfPackageView view = LinuxElfPackage.Validate(original);
        return original.AsSpan((int)view.PayloadOffset, (int)view.PayloadBytes).ToArray();
    }

    private static byte[] Package() => NativeAotWebPackage.Create(Image(), "{\"OS_SMOKE_EXPECTED_BODY\":\"OS_MINIMAL_API_OK\"}"u8);

    private static string Manifest(byte[] elf, string args = "--urls http://0.0.0.0:8080", string? environment = null) =>
        "format=clros-linux-elf\nversion=1\nprofile=linux-musl-x64-static-v1\n" +
        "app.path=apps/MinimalApiSmoke\nargs=" + args + "\ndigest=" + Convert.ToHexString(SHA256.HashData(elf)).ToLowerInvariant() +
        "\nmemory=67108864\nstack=1572864\nthreads=16\ndescriptors=16\nlisten=0.0.0.0:8080\n" +
        (environment ?? "env.CLROS_USER_IRQS=1\nenv.DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false\n");

    [HostTest("nativeaot.web configured selection preserves the default package byte for byte")]
    private static void DefaultSelection()
    {
        byte[] elf = Image(), settings = "{\"OS_SMOKE_EXPECTED_BODY\":\"OS_MINIMAL_API_OK\"}"u8.ToArray();
        byte[] expectedManifest = Encoding.ASCII.GetBytes(Manifest(elf));
        byte[] original = NativeAotWebPackage.Create(elf, settings);
        HostCheck.True(original.AsSpan(64, expectedManifest.Length).SequenceEqual(expectedManifest));
        HostCheck.True(original.SequenceEqual(NativeAotWebPackage.Create(elf, settings, expectedManifest)));
    }

    [HostTest("nativeaot.web configured selection reaches startup argv envp and immutable descriptor reads")]
    private static void ConfiguredStartup()
    {
        byte[] elf = Image(), settings = "{\"OS_SMOKE_EXPECTED_BODY\":\"SELECTED_CONFIG_VALUE\"}"u8.ToArray();
        byte[] manifest = Encoding.ASCII.GetBytes(Manifest(elf, "--urls http://0.0.0.0:8080 --contentRoot /app/apps",
            "env.CLROS_USER_IRQS=1\nenv.ASPNETCORE_URLS=http://0.0.0.0:8081\nenv.DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false\n"));
        byte[] package = NativeAotWebPackage.Create(elf, settings, manifest);
        var view = LinuxElfPackage.Validate(package); HostCheck.True(view.Succeeded);
        HostCheck.True(package.AsSpan((int)view.ManifestOffset, (int)view.ManifestBytes).SequenceEqual(manifest));
        HostCheck.True(package.AsSpan((int)view.PayloadOffset, (int)view.PayloadBytes).SequenceEqual(elf));
        const ulong start = 0x70000000;
        byte* stack = (byte*)NativeMemory.AlignedAlloc(view.Manifest.StackBytes, 4096);
        byte* entropy = stackalloc byte[32]; new Span<byte>(entropy, 32).Fill(0x39);
        LinuxDescriptorServices.View* views = stackalloc LinuxDescriptorServices.View[8];
        byte* paths = stackalloc byte[2048];
        new Span<byte>(views, 8 * sizeof(LinuxDescriptorServices.View)).Clear(); new Span<byte>(paths, 2048).Clear();
        try
        {
            fixed (byte* source = package)
            {
                HostCheck.Equal(LinuxElfStartupStatus.Ready, LinuxElfStartup.Build(source + view.ManifestOffset, view.ManifestBytes,
                    source + view.PayloadOffset, view.PayloadBytes, entropy, 32, stack, view.Manifest.StackBytes, start, 0x078bfbff, out var plan));
                ulong* frame = (ulong*)(stack + plan.StackPointer - start);
                HostCheck.Equal(5UL, frame[0]); HostCheck.Equal(5u, plan.ArgumentCount); HostCheck.Equal(3u, plan.EnvironmentCount);
                string[] args = ["apps/MinimalApiSmoke", "--urls", "http://0.0.0.0:8080", "--contentRoot", "/app/apps"];
                string[] env = ["CLROS_USER_IRQS=1", "ASPNETCORE_URLS=http://0.0.0.0:8081", "DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false"];
                for (int i = 0; i < args.Length; i++) HostCheck.Equal(args[i], Text(frame[1 + i]));
                HostCheck.Equal(0UL, frame[6]);
                for (int i = 0; i < env.Length; i++) HostCheck.Equal(env[i], Text(frame[7 + i]));
                HostCheck.Equal(0UL, frame[10]);
                HostCheck.True(LinuxDescriptorServices.CreatePackageViews(source, (uint)package.Length, views, 8, paths, 2048, out uint count));
                using var owner = new LinuxDescriptorServicesTests.Owner();
                var original = owner.Admission;
                try
                {
                    owner.Admission.Views = views; owner.Admission.ViewCount = count; owner.Admission.PackageProcessId = 23; owner.Begin();
                    uint fd = checked((uint)owner.Open("/app/apps/appsettings.json", 0x100));
                    const ulong output = 0x100006000;
                    HostCheck.Equal((long)settings.Length, owner.Call(0, fd, output, 4096));
                    HostCheck.True(new ReadOnlySpan<byte>(owner.Memory.At(output), settings.Length).SequenceEqual(settings));
                    HostCheck.Equal(0L, owner.Call(0, fd, output, 1)); HostCheck.Equal(0L, owner.Call(3, fd));
                    HostCheck.True(LinuxDescriptorServices.End(owner.State));
                }
                finally { owner.Admission = original; }
            }
        }
        finally { NativeMemory.AlignedFree(stack); }
        string Text(ulong address)
        {
            HostCheck.True(address >= start && address < start + view.Manifest.StackBytes);
            return Marshal.PtrToStringUTF8((nint)(stack + address - start))!;
        }
    }

    [HostTest("nativeaot.web configured selection rejects malformed bounds keys paths and stale digest")]
    private static void InvalidSelections()
    {
        byte[] elf = Image(); string original = Manifest(elf);
        string digest = Convert.ToHexString(SHA256.HashData(elf)).ToLowerInvariant();
        var cases = new (string Text, LinuxElfStatus Status)[]
        {
            (original.Replace(digest, new string('0', 64), StringComparison.Ordinal), LinuxElfStatus.DigestMismatch),
            (original + "env.CLROS_USER_IRQS=2\n", LinuxElfStatus.DuplicateField),
            (original + "env.bad=value\n", LinuxElfStatus.InvalidEnvironment),
            (original + "env.EXTRA=" + new string('x', 257) + "\n", LinuxElfStatus.InvalidEnvironment),
            (Manifest(elf, environment: string.Concat(Enumerable.Range(0, 17).Select(i => "env.KEY" + i + "=value\n"))), LinuxElfStatus.InvalidEnvironment),
            (Manifest(elf, new string('x', 257)), LinuxElfStatus.InvalidValue),
            (Manifest(elf, "\"unterminated"), LinuxElfStatus.InvalidValue),
            (original.Replace("apps/MinimalApiSmoke", "apps/../escape", StringComparison.Ordinal), LinuxElfStatus.InvalidPath),
            (original.Replace("apps/MinimalApiSmoke", "/absolute", StringComparison.Ordinal), LinuxElfStatus.InvalidPath),
            (original.Replace("version=1", "version=2", StringComparison.Ordinal), LinuxElfStatus.UnsupportedProfile)
        };
        foreach (var item in cases)
        {
            string? failure = null;
            try { NativeAotWebPackage.Create(elf, "{}"u8, Encoding.ASCII.GetBytes(item.Text)); }
            catch (InvalidDataException error) { failure = error.Message; }
            HostCheck.Equal("web_package_admission " + item.Status, failure!);
        }
        foreach (int length in new[] { 0, 4097 })
        {
            string? failure = null;
            try { NativeAotWebPackage.Create(elf, "{}"u8, new byte[length]); }
            catch (InvalidDataException error) { failure = error.Message; }
            HostCheck.Equal("web_package_input_length", failure!);
        }
    }

    [HostTest("nativeaot.web configured command rejects missing aliased and oversized inputs without publication")]
    private static void InvalidSelectionFiles()
    {
        string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "clros-web-selection-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string manifest = System.IO.Path.Combine(root, "selected.manifest"), settings = System.IO.Path.Combine(root, "settings.json");
            string destination = System.IO.Path.Combine(root, "artifacts", "selected.clrelf");
            File.WriteAllText(manifest, Manifest(Image())); File.WriteAllText(settings, "{}");
            string[] command = ["package", "--recipe-root", root, "--recipe-evidence", root, "--output", destination, "--manifest", manifest, "--settings", settings];
            Reject(command[..^2], "nativeaot web qualify");
            File.Delete(settings); Reject(command, "web_settings_reparse_point"); File.WriteAllText(settings, "{}");
            command[^1] = manifest; Reject(command, "authority_output_file_alias"); command[^1] = settings;
            File.WriteAllText(manifest, new string('x', 4097)); Reject(command, "web_manifest_size_out_of_range");
            File.WriteAllText(manifest, Manifest(Image())); File.WriteAllText(settings, new string('x', 65537));
            Reject(command, "web_settings_size_out_of_range");
            void Reject(string[] args, string reason)
            {
                using StringWriter output = new();
                HostCheck.Equal(1, NativeAotWebPayload.RunAsync(root, args, output).GetAwaiter().GetResult());
                string text = output.ToString();
                HostCheck.True(text.Contains("TEST_START nativeaot.web.package", StringComparison.Ordinal) &&
                    text.Contains("TEST_FAIL nativeaot.web.package " + reason, StringComparison.Ordinal));
                HostCheck.True(!text.Contains("TEST_PASS", StringComparison.Ordinal) && !text.Contains("NATIVEAOT_WEB_PACKAGE", StringComparison.Ordinal));
                HostCheck.True(!File.Exists(destination));
            }
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [HostTest("nativeaot.web image stages exact ELF manifest and immutable config bytes")]
    private static void Stage()
    {
        byte[] package = Package(), copied = new byte[package.Length], output = new byte[128];
        byte[] path = Encoding.ASCII.GetBytes("apps/appsettings.json");
        fixed (byte* source = package) fixed (byte* destination = copied) fixed (byte* name = path) fixed (byte* data = output)
        {
            HostCheck.Equal(LinuxElfStatus.Success, LinuxElfPackage.Copy(source, (uint)package.Length,
                destination, (uint)copied.Length, out var view));
            HostCheck.Equal(1u, view.DataFileCount); HostCheck.True(package.SequenceEqual(copied));
            HostCheck.Equal(LinuxElfStatus.Success, LinuxElfPackage.GetDataFile(destination, (uint)copied.Length, 0, out var file));
            HostCheck.Equal("apps/appsettings.json", Encoding.ASCII.GetString(copied.AsSpan((int)file.PathOffset, (int)file.PathBytes)));
            HostCheck.Equal(LinuxElfStatus.Success, LinuxElfPackage.ReadFile(destination, (uint)copied.Length,
                name, (uint)path.Length, 0, data, (uint)output.Length, out uint actual));
            HostCheck.Equal("{\"OS_SMOKE_EXPECTED_BODY\":\"OS_MINIMAL_API_OK\"}", Encoding.ASCII.GetString(output, 0, (int)actual));
            HostCheck.Equal(LinuxElfStatus.InvalidValue, LinuxElfPackage.GetDataFile(destination, (uint)copied.Length, 1, out _));
        }
    }

    [HostTest("nativeaot.web image rejects payload data digest overflow duplicate and legacy admission")]
    private static void RejectMalformed()
    {
        byte[] original = Package(); var view = LinuxElfPackage.Validate(original); int at = (int)view.DataTableOffset;
        void Reject(Action<byte[]> change, LinuxElfStatus expected)
        { byte[] bad = (byte[])original.Clone(); change(bad); HostCheck.Equal(expected, LinuxElfPackage.Validate(bad).Status); }
        Reject(bytes => bytes[(int)view.PayloadOffset + 300] ^= 1, LinuxElfStatus.DigestMismatch);
        Reject(bytes => bytes[^1] ^= 1, LinuxElfStatus.DigestMismatch);
        Reject(bytes => W32(bytes, 32, uint.MaxValue), LinuxElfStatus.InvalidLength);
        Reject(bytes => W32(bytes, at, uint.MaxValue), LinuxElfStatus.InvalidLength);
        Reject(bytes => W32(bytes, at + 4, uint.MaxValue), LinuxElfStatus.InvalidLength);
        Reject(bytes => W32(bytes, 36, 0), LinuxElfStatus.InvalidLength);
        Reject(bytes => bytes[0] = (byte)'O', LinuxElfStatus.InvalidHeader);
        Reject(bytes => W32(bytes, 8, 1), LinuxElfStatus.UnsupportedProfile);
        Reject(bytes => bytes[at + 48] = (byte)'/', LinuxElfStatus.InvalidPath);
        Reject(bytes => bytes[at + 12] = 1, LinuxElfStatus.InvalidHeader);
        int record = original.Length - at;
        byte[] duplicate = new byte[original.Length + record]; original.CopyTo(duplicate, 0);
        original.AsSpan(at).CopyTo(duplicate.AsSpan(original.Length));
        W32(duplicate, 32, (uint)(record * 2)); W32(duplicate, 36, 2);
        HostCheck.Equal(LinuxElfStatus.InvalidPath, LinuxElfPackage.Validate(duplicate).Status);
        foreach (byte[] missing in new[] { original[..at], original[..^1], Array.Empty<byte>() })
            HostCheck.Equal(LinuxElfStatus.InvalidLength, LinuxElfPackage.Validate(missing).Status);
    }

    [HostTest("nativeaot.web image builder rejects missing ELF malformed config and non-object config")]
    private static void BuilderFailures()
    {
        foreach ((byte[] elf, byte[] config) in new[] { (Array.Empty<byte>(), "{}"u8.ToArray()),
            (Image(), Array.Empty<byte>()), (Image(), "{"u8.ToArray()), (Image(), "[]"u8.ToArray()) })
        {
            bool rejected = false;
            try { NativeAotWebPackage.Create(elf, config); }
            catch (Exception error) when (error is InvalidDataException or System.Text.Json.JsonException) { rejected = true; }
            HostCheck.True(rejected);
        }
    }

    [HostTest("nativeaot.web image corrupt data cannot publish and aliased reads preserve package bytes")]
    private static void NoPublicationOnFailure()
    {
        byte[] valid = Package(), corrupted = (byte[])valid.Clone(); corrupted[^1] ^= 1;
        byte[] destination = Enumerable.Repeat((byte)0xa5, valid.Length).ToArray();
        fixed (byte* bad = corrupted) fixed (byte* output = destination)
        {
            HostCheck.Equal(LinuxElfStatus.DigestMismatch, LinuxElfPackage.Copy(bad, (uint)corrupted.Length,
                output, (uint)destination.Length, out var rejected));
            HostCheck.True(!rejected.Succeeded && destination.All(value => value == 0xa5));
        }
        byte[] path = Encoding.ASCII.GetBytes("apps/appsettings.json");
        fixed (byte* package = valid) fixed (byte* name = path)
        {
            HostCheck.Equal(LinuxElfStatus.Success, LinuxElfPackage.GetDataFile(package, (uint)valid.Length, 0, out var file));
            byte[] before = (byte[])valid.Clone();
            HostCheck.Equal(LinuxElfStatus.InvalidDestination, LinuxElfPackage.ReadFile(package, (uint)valid.Length,
                name, (uint)path.Length, 0, package + file.DataOffset, 1, out uint actual));
            HostCheck.Equal(0u, actual); HostCheck.True(valid.SequenceEqual(before));
        }
    }

    private static void W32(byte[] bytes, int at, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at), value);
}
