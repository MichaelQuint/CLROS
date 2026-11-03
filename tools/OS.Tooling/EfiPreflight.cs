using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using OS.Tooling.Native;

namespace OS.Tooling;
/// <summary>Deterministic native tool discovery and bounded evidence collection.</summary>

public static class EfiPreflight
{
    public static string Quote(string? value) => "\"" + (value ?? "").Replace('\r', ' ').Replace('\n', ' ').Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    public static string SdkVersionFailure(string processFailure, IReadOnlyList<string> lines)
    {
        if (processFailure != "ok")
            return "dotnet_sdk_version_" + processFailure;
        if (lines.Count != 1)
            return "dotnet_sdk_version_output_invalid";
        string version = lines[0].Trim();
        return EfiContracts.IsPinnedSdkVersion(version) ? "ok" : "dotnet_sdk_version_unsupported";
    }

    public static Version NumericPathVersion(string path) => Regex.Matches(path, @"(?<!\d)\d+\.\d+(?:\.\d+){0,2}(?!\d)").Select(m => Version.TryParse(m.Value, out var v) ? v : new Version(0, 0)).DefaultIfEmpty(new Version(0, 0)).Max()!;
    public static string VersionFromPath(string path)
    {
        var matches = Regex.Matches(path, @"(?<!\d)\d+\.\d+(?:\.\d+){0,2}(?!\d)");
        return matches.Count == 0 ? "unknown" : matches[^1].Value;
    }

    public static string FileVersion(string path)
    {
        try
        {
            var info = FileVersionInfo.GetVersionInfo(path);
            foreach (string? value in new[]
            {
                info.FileVersion,
                info.ProductVersion
            }

            )
                if (!string.IsNullOrWhiteSpace(value))
                    return value.Trim();
        }
        catch (Exception error)when (error is IOException or ArgumentException)
        {
        }

        return VersionFromPath(path);
    }

    public static bool IsPeX64(string path)
    {
        try
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var reader = new BinaryReader(file);
            if (file.Length < 256 || reader.ReadUInt16() != 0x5a4d)
                return false;
            file.Position = 0x3c;
            long pe = reader.ReadUInt32();
            if (pe < 64 || pe > file.Length - 24)
                return false;
            file.Position = pe;
            return reader.ReadUInt32() == 0x4550 && reader.ReadUInt16() == 0x8664;
        }
        catch (Exception error)when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }

    public static bool IsCoffArchive(string path)
    {
        try
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            Span<byte> signature = stackalloc byte[8];
            return file.Length >= 68 && file.Read(signature) == 8 && signature.SequenceEqual("!<arch>\n"u8);
        }
        catch (Exception error)when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }

    public static bool IsRuntimePack(string path)
    {
        try
        {
            string list = System.IO.Path.Combine(path, "data/RuntimeList.xml");
            return new FileInfo(list).Length > 0 && ContractAudit.ReadProject(list).Root?.Name.LocalName == "FileList";
        }
        catch (Exception error)when (error is IOException or UnauthorizedAccessException or System.Xml.XmlException or ArgumentException)
        {
            return false;
        }
    }

    public static EfiOvmfEvidence ReadOvmf(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return new("missing", "", 0, "");
        string full = System.IO.Path.GetFullPath(path);
        try
        {
            using var file = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read);
            byte[] header = new byte[(int)Math.Min(4096, file.Length)];
            file.ReadExactly(header);
            if (file.Length < 65536 || header.AsSpan().IndexOf("_FVH"u8) < 0)
                return new("invalid", full, file.Length, "");
            file.Position = 0;
            return new("ok", full, file.Length, Convert.ToHexString(SHA256.HashData(file)));
        }
        catch (Exception error)when (error is IOException or UnauthorizedAccessException)
        {
            return new("evidence_unavailable", full, 0, "");
        }
    }

    private static IEnumerable<string> Files(string root, string name)
    {
        if (!Directory.Exists(root))
            yield break;
        Stack<string> pending = new();
        pending.Push(System.IO.Path.GetFullPath(root));
        while (pending.TryPop(out string? directory))
        {
            string[] paths;
            try
            {
                paths = Directory.GetFileSystemEntries(directory);
            }
            catch (Exception error)when (error is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (string path in paths)
            {
                FileAttributes attributes;
                try
                {
                    attributes = File.GetAttributes(path);
                }
                catch (Exception error)when (error is IOException or UnauthorizedAccessException)
                {
                    continue;
                }

                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    continue;
                if ((attributes & FileAttributes.Directory) != 0)
                    pending.Push(path);
                else if (System.IO.Path.GetFileName(path).Equals(name, StringComparison.OrdinalIgnoreCase))
                    yield return path;
            }
        }
    }

    public static string? ResolveTool(IEnumerable<string> roots, string name, string pattern = "", Func<string, bool>? validator = null) => roots.Where(r => !string.IsNullOrWhiteSpace(r)).SelectMany(r => Files(r, name)).Distinct(StringComparer.OrdinalIgnoreCase).Where(p => pattern.Length == 0 || Regex.IsMatch(p, pattern, RegexOptions.IgnoreCase)).OrderByDescending(NumericPathVersion).ThenBy(p => p, StringComparer.OrdinalIgnoreCase).FirstOrDefault(p => validator is null || validator(p));
    public static string? ResolveExecutable(string requested, IReadOnlyList<string> roots, string name, IEnumerable<string> defaults, IEnumerable<string> commands, Func<string, bool>? validator = null)
    {
        bool Valid(string p) => !string.IsNullOrWhiteSpace(p) && File.Exists(p) && (validator is null || validator(p));
        if (!string.IsNullOrWhiteSpace(requested))
            return Valid(requested) ? System.IO.Path.GetFullPath(requested) : null;
        if (roots.Count != 0)
            return ResolveTool(roots, name, validator: validator);
        foreach (string path in defaults)
            if (Valid(path))
                return System.IO.Path.GetFullPath(path);
        foreach (string command in commands)
        {
            string? path = HostTools.FindExecutable(null, "", command);
            if (path is not null && Valid(path))
                return path;
        }

        return null;
    }

    public static string? ResolveRuntimePack(string requested, IReadOnlyList<string> roots, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(requested))
            return IsRuntimePack(requested) ? System.IO.Path.GetFullPath(requested) : null;
        if (roots.Count == 0)
            return IsRuntimePack(fallback) ? System.IO.Path.GetFullPath(fallback) : null;
        return roots.Where(Directory.Exists).SelectMany(root => new[] { System.IO.Path.GetFullPath(root) }.Concat(Files(root, "RuntimeList.xml").Where(p => System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(p)) == "data").Select(p => System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(p))!))).Distinct(StringComparer.OrdinalIgnoreCase).OrderByDescending(NumericPathVersion).ThenBy(p => p, StringComparer.OrdinalIgnoreCase).FirstOrDefault(IsRuntimePack);
    }

    public static EfiVersionEvidence ExecutableVersion(string path, int timeoutMilliseconds)
    {
        try
        {
            string executable = path;
            string[] arguments = ["--version"];
            if (System.IO.Path.GetExtension(path).Equals(".cmd", StringComparison.OrdinalIgnoreCase) || System.IO.Path.GetExtension(path).Equals(".bat", StringComparison.OrdinalIgnoreCase))
            {
                executable = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
                arguments = ["/d", "/c", path, "--version"];
            }

            ProcessResult result = ProcessRunner.RunAsync(new(executable, arguments, Environment.CurrentDirectory) { Timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds), OutputCharacterLimit = 65536 }).GetAwaiter().GetResult();
            if (result.TimedOut)
                return new("timeout", null);
            if (!result.Succeeded)
                return new("failed", null);
            string? line = (result.StandardOutput + "\n" + result.StandardError).Split('\n').FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));
            return line is null ? new("failed", null) : new("ok", line.Trim());
        }
        catch (Exception error)when (error is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return new("failed", null);
        }
    }

    public static IReadOnlyList<string> DefaultLinkerRoots(string vsWhere, Func<ProcessRequest, ProcessResult>? run = null)
    {
        List<string> roots = [];
        if (!string.IsNullOrWhiteSpace(vsWhere) && File.Exists(vsWhere))
        {
            try
            {
                ProcessRequest request = new(vsWhere, ["-latest", "-prerelease", "-products", "*", "-requires", "Microsoft.VisualStudio.Component.VC.Tools.x86.x64", "-property", "installationPath"], Environment.CurrentDirectory)
                {
                    Timeout = TimeSpan.FromSeconds(15),
                    OutputCharacterLimit = 65536
                };
                var result = run is null ? ProcessRunner.RunAsync(request).GetAwaiter().GetResult() : run(request);
                if (result.Succeeded)
                    roots.AddRange(result.StandardOutput.Split('\n').Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => System.IO.Path.Combine(p.Trim(), "VC/Tools/MSVC")));
            }
            catch (Exception error)when (error is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
            {
            }
        }

        if (Environment.GetEnvironmentVariable("ProgramFiles(x86)")is string basePath)
            roots.AddRange(new[] { "BuildTools", "Community", "Professional", "Enterprise" }.Select(e => System.IO.Path.Combine(basePath, "Microsoft Visual Studio/2022", e, "VC/Tools/MSVC")));
        return roots;
    }

    public static EfiProfile Profile(EfiOptions options, Action<string> emit)
    {
        EfiProfile profile;
        try
        {
            profile = options.Profile(line => emit(line.Replace("EFI_PROFILE_", "EFI_PREFLIGHT_", StringComparison.Ordinal)));
        }
        catch (ToolFailure failure)
        {
            string prefix = failure.Reason.Split('_')[0];
            string reason = failure.Reason;
            if (prefix is "m01" or "m02" or "m04")
            {
                // Profile-level failures already include their milestone; ordinary dependency failures historically do not.
                string shortReason = reason.Contains("_probe_", StringComparison.Ordinal) || reason.Contains("_profile_", StringComparison.Ordinal) || reason.StartsWith("m04_managed_heap", StringComparison.Ordinal) ? reason : reason[(prefix.Length + 1)..];
                emit("EFI_PREFLIGHT_" + prefix.ToUpperInvariant() + "_SWITCHES reason=" + shortReason + " invalid");
            }

            throw;
        }

        string Fields(params (string Label, string Probe)[] fields) => string.Join(" ", fields.Select(f => f.Label + "=" + profile.IsEnabled(f.Probe).ToString().ToLowerInvariant()));
        emit("EFI_PREFLIGHT_M01_SWITCHES status=ok " + Fields(("loadedImageStack", "M01LoadedImageStack"), ("liveAllocator", "M01LiveAllocator"), ("ownedPaging", "M01OwnedPaging"), ("m02SafeStub", "M02SafeStub")));
        emit("EFI_PREFLIGHT_M02_SWITCHES status=ok " + Fields(("enabled", "M02SafeStub"), ("loadedImageStack", "M01LoadedImageStack"), ("liveAllocator", "M01LiveAllocator"), ("ownedPaging", "M01OwnedPaging"), ("checkedPortIoBinding", "M02CheckedPortIoBinding"), ("com1OwnedPortIo", "M02Com1OwnedPortIo"), ("ownedCom1Ready", "M02OwnedCom1Ready"), ("panicSafeOwnedCom1", "M02PanicSafeOwnedCom1"), ("ownedX64GdtEncoding", "M02OwnedX64GdtEncoding"), ("guardedIstStackAllocation", "M02GuardedIstStackAllocation"), ("ownedGdtTssTerminal", "M02OwnedGdtTssTerminal"), ("normalizedInterruptFramePage", "M02NormalizedInterruptFramePage"), ("ownedIdtGatePopulation", "M02OwnedIdtGatePopulation"), ("idtrContinuedExecution", "M02IdtrContinuedExecution"), ("ud2VectorState", "M02Ud2VectorState")));
        emit("EFI_PREFLIGHT_M04_SWITCHES status=ok " + Fields(("independentEndianConformance", "M04IndependentEndianConformance"), ("spanOverlapMemoryCastUnsafeWidthConformance", "M04SpanOverlapMemoryCastUnsafeWidthConformance"), ("arithmeticConformance", "M04ArithmeticConformance"), ("layoutContract", "M04LayoutContract"), ("managedHeapPageRange", "M04ManagedHeapPageRange")));
        return profile;
    }

    public static EfiPreflightResult Inspect(EfiOptions options, Action<string> emit, EfiProfile? validatedProfile = null)
    {
        void Fail(string marker, string reason, string status = "missing")
        {
            emit(marker + " " + status);
            throw new ToolFailure(reason);
        }

        emit("EFI_PREFLIGHT_START");
        EfiProfile profile = validatedProfile ?? Profile(options, emit);
        string root = options.Root, configuration = options.Get("Configuration", "Release"), runtime = options.Get("Runtime", "win-x64");
        EfiPeImage.Require(configuration is "Release" or "Debug", "configuration_invalid");
        EfiPeImage.Require(runtime == "win-x64", "runtime_not_win_x64");
        string? dotnet = HostTools.FindExecutable(null, "", "dotnet");
        if (dotnet is null)
            Fail("EFI_PREFLIGHT_DOTNET", "dotnet_host_missing");
        var version = BoundedJobProcessRunner.Run(dotnet!, ["--version"], root, EfiBuildPolicy.BuildTimeout(options), EfiBuildPolicy.OutputLimit(options), EfiBuildCommands.ProcessEnvironment.ToDictionary(p => p.Key, p => p.Value!), EfiBuildCommands.TerminateDescendantsAfterRootExit);
        string[] lines = version.Output.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => l.Trim()).ToArray();
        string processFailure = version.Status == "Success" && version.TerminationConfirmed ? "ok" : version.Status switch
        {
            "TimedOut" => "timeout",
            "NonZeroExit" => "nonzero_exit",
            "OutputOverflow" => "output_overflow",
            _ => version.Status.ToLowerInvariant()};
        string failure = SdkVersionFailure(processFailure, lines);
        if (failure != "ok")
            Fail("EFI_PREFLIGHT_DOTNET", failure, "version_unavailable");
        string sdkVersion = lines[0], sdk = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(dotnet)!, "sdk", sdkVersion);
        if (!Directory.Exists(System.IO.Path.Combine(sdk, "Sdks")))
            Fail("EFI_PREFLIGHT_DOTNET", "dotnet_sdk_layout_invalid", "version_unavailable");
        InspectSource(options, profile, dotnet!, sdk, emit);
        emit("EFI_PREFLIGHT_DOTNET path=" + Quote(dotnet) + " sdkVersion=" + Quote(sdkVersion));
        string programFiles = Environment.GetEnvironmentVariable("ProgramFiles") ?? "", programFilesX86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)") ?? "";
        string vsWhere = options.Get("VsWherePath", System.IO.Path.Combine(programFilesX86, "Microsoft Visual Studio/Installer/vswhere.exe"));
        var linkerRoots = options.All("MsvcLinkerSearchRoot");
        if (linkerRoots.Count == 0)
            linkerRoots = DefaultLinkerRoots(vsWhere);
        string? linker = ResolveTool(linkerRoots, "link.exe", @"Hostx64\\x64", IsPeX64);
        if (linker is null)
            Fail("EFI_PREFLIGHT_MSVC_LINKER", "platform_linker_missing");
        string linkerVersion = FileVersion(linker!);
        if (linkerVersion == "unknown")
            Fail("EFI_PREFLIGHT_MSVC_LINKER", "platform_linker_version_unavailable", "version_unavailable");
        emit("EFI_PREFLIGHT_MSVC_LINKER path=" + Quote(linker) + " version=" + Quote(linkerVersion));
        var sdkRoots = options.All("WindowsSdkLibSearchRoot");
        if (sdkRoots.Count == 0)
            sdkRoots = new[]
            {
                programFilesX86,
                programFiles
            }.Where(p => p.Length != 0).Select(p => System.IO.Path.Combine(p, "Windows Kits/10/Lib")).ToArray();
        string? library = ResolveTool(sdkRoots, "kernel32.lib", @"\\um\\x64\\", IsCoffArchive);
        if (library is null)
            Fail("EFI_PREFLIGHT_WINDOWS_SDK", "windows_sdk_um_x64_missing");
        string windowsVersion = VersionFromPath(library!);
        if (windowsVersion == "unknown")
            Fail("EFI_PREFLIGHT_WINDOWS_SDK", "windows_sdk_version_unavailable", "version_unavailable");
        emit("EFI_PREFLIGHT_WINDOWS_SDK path=" + Quote(library) + " version=" + Quote(windowsVersion));
        var project = ContractAudit.ReadProject(System.IO.Path.Combine(root, "src/OS/OS.csproj"));
        string PackageVersion(string id) => project.Descendants("PackageReference").Where(n => string.Equals((string? )n.Attribute("Include"), id, StringComparison.OrdinalIgnoreCase)).Select(n => (string? )n.Attribute("Version") ?? "").FirstOrDefault() ?? "";
        string ilcVersion = PackageVersion("runtime.win-x64.Microsoft.DotNet.ILCompiler"), runtimeVersion = PackageVersion("Microsoft.NETCore.App.Runtime.NativeAOT.win-x64");
        if (runtimeVersion.Length == 0)
            runtimeVersion = PackageVersion("Microsoft.DotNet.ILCompiler");
        string global = Environment.GetEnvironmentVariable("NUGET_PACKAGES")is string custom && !string.IsNullOrWhiteSpace(custom) ? custom : System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget/packages");
        string? ilc = ResolveExecutable(options.Get("IlCompilerPath"), options.All("IlCompilerSearchRoot"), "ilc.exe", [System.IO.Path.Combine(global, "runtime.win-x64.microsoft.dotnet.ilcompiler", ilcVersion, "tools/ilc.exe")], [], IsPeX64);
        if (ilc is null)
            Fail("EFI_PREFLIGHT_ILCOMPILER", "ilcompiler_missing");
        string ilcFileVersion = FileVersion(ilc!);
        if (ilcVersion.Length == 0 || ilcFileVersion == "unknown")
            Fail("EFI_PREFLIGHT_ILCOMPILER", "ilcompiler_version_unavailable", "version_unavailable");
        emit("EFI_PREFLIGHT_ILCOMPILER path=" + Quote(ilc) + " packageVersion=" + Quote(ilcVersion) + " fileVersion=" + Quote(ilcFileVersion));
        string? pack = ResolveRuntimePack(options.Get("NativeAotRuntimePackPath"), options.All("NativeAotRuntimePackSearchRoot"), System.IO.Path.Combine(global, "microsoft.netcore.app.runtime.nativeaot.win-x64", runtimeVersion));
        if (pack is null)
            Fail("EFI_PREFLIGHT_NATIVEAOT_RUNTIME_PACK", "nativeaot_runtime_pack_missing");
        if (runtimeVersion.Length == 0)
            Fail("EFI_PREFLIGHT_NATIVEAOT_RUNTIME_PACK", "nativeaot_runtime_pack_version_unavailable", "version_unavailable");
        emit("EFI_PREFLIGHT_NATIVEAOT_RUNTIME_PACK path=" + Quote(pack) + " packageVersion=" + Quote(runtimeVersion) + " manifest=" + Quote(System.IO.Path.Combine(pack!, "data/RuntimeList.xml")));
        foreach ((string option, string environment, string filename, string marker, string reason, string pattern)in new[]
        {
            ("Qemu", "QEMU", "qemu-system-x86_64.exe", "EFI_PREFLIGHT_QEMU", "qemu", @"\bQEMU\b.*\bversion\b"),
            ("QemuImg", "QEMU_IMG", "qemu-img.exe", "EFI_PREFLIGHT_QEMU_IMG", "qemu_img", @"\bqemu-img\b.*\bversion\b")
        }

        )
        {
            string? path = ResolveExecutable(options.Get(option + "Path"), options.All(option + "SearchRoot"), filename, [Environment.GetEnvironmentVariable(environment) ?? "", System.IO.Path.Combine(programFiles, "qemu", filename)], [filename, System.IO.Path.GetFileNameWithoutExtension(filename)]);
            if (path is null)
                Fail(marker, reason + "_not_found");
            var value = ExecutableVersion(path!, EfiBuildPolicy.VersionTimeout(options));
            if (value.Status != "ok")
                Fail(marker, reason + (value.Status == "timeout" ? "_version_timeout" : "_version_unavailable"), value.Status);
            if (!Regex.IsMatch(value.Version!, pattern, RegexOptions.IgnoreCase))
                Fail(marker, reason + "_version_unrecognized", "version_unrecognized");
            emit(marker + " path=" + Quote(path) + " version=" + Quote(value.Version));
        }

        Dictionary<string, EfiOvmfEvidence> evidence = new(StringComparer.OrdinalIgnoreCase);
        EfiOvmfEvidence Read(string path)
        {
            string key = string.IsNullOrWhiteSpace(path) ? "" : System.IO.Path.GetFullPath(path);
            if (!evidence.TryGetValue(key, out var value))
                evidence[key] = value = ReadOvmf(path);
            return value;
        }

        EfiOvmfEvidence? ovmf = null;
        string requested = options.Get("OvmfCodePath");
        var roots = options.All("OvmfSearchRoot");
        if (!string.IsNullOrWhiteSpace(requested))
            ovmf = Read(requested);
        else if (roots.Count != 0)
        {
            string? selected = ResolveTool(roots, "edk2-x86_64-code.fd", validator: p => Read(p).Status == "ok") ?? ResolveTool(roots, "OVMF_CODE.fd", validator: p => Read(p).Status == "ok");
            if (selected is not null)
                ovmf = Read(selected);
        }
        else
            foreach (string candidate in new[]
            {
                Environment.GetEnvironmentVariable("OS_OVMF_CODE"),
                Environment.GetEnvironmentVariable("OVMF_CODE"),
                Environment.GetEnvironmentVariable("QEMU_OVMF_CODE"),
                System.IO.Path.Combine(programFiles, "qemu/share/edk2-x86_64-code.fd"),
                System.IO.Path.Combine(programFiles, "qemu/share/OVMF_CODE.fd")
            }.OfType<string>())
            {
                var value = Read(candidate);
                if (value.Status == "ok")
                {
                    ovmf = value;
                    break;
                }
            }

        if (ovmf?.Status != "ok")
        {
            string status = ovmf?.Status ?? (evidence.Values.Any(v => v.Status == "evidence_unavailable") ? "evidence_unavailable" : evidence.Values.Any(v => v.Status == "invalid") ? "invalid" : "missing");
            Fail("EFI_PREFLIGHT_OVMF", status switch
            {
                "evidence_unavailable" => "ovmf_evidence_unavailable",
                "invalid" => "ovmf_invalid",
                _ => "ovmf_not_found"
            }, status);
        }

        emit("EFI_PREFLIGHT_OVMF path=" + Quote(ovmf!.Path) + " bytes=" + ovmf.Length + " sha256=" + ovmf.Sha256);
        emit("EFI_PREFLIGHT_OK");
        return new(root, configuration, runtime, profile, dotnet!, sdkVersion, sdk, linker!, linkerVersion, library!, windowsVersion, ilc!, ilcVersion, ilcFileVersion, pack!, runtimeVersion);
    }

    public static void InspectSource(EfiOptions options, EfiProfile profile, string dotnet, string sdk, Action<string> emit) => EfiProjectAdmission.Inspect(options.Root, options.Get("Configuration", "Release"), profile, dotnet, sdk, emit, EfiBuildPolicy.BuildTimeout(options), EfiBuildPolicy.OutputLimit(options));
}
