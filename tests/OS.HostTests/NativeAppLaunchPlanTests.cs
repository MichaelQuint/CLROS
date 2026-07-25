using System.Runtime.CompilerServices;
using System.Text;
using OS.Boot;
using OS.Manifest;

internal static class NativeAppLaunchPlanTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("appmodel launch plan accepts manifest", TestAcceptsManifest, ref failures);
        Run("appmodel launch plan accepts packaged native app", TestAcceptsPackagedNativeApp, ref failures);
        Run("appmodel launch plan carries environment fields", TestCarriesEnvironmentFields, ref failures);
        Run("appmodel launch plan records listen endpoint", TestRecordsListenEndpoint, ref failures);
        Run("appmodel launch plan requires network for listen endpoint", TestRequiresNetworkForListenEndpoint, ref failures);
        Run("appmodel launch plan rejects missing capabilities", TestRejectsMissingCapabilities, ref failures);
        Run("appmodel launch plan rejects malformed requirements", TestRejectsMalformedRequirements, ref failures);
        Run("appmodel launch plan uses shared memory dot segment helper", TestUsesSharedMemoryDotSegmentHelper, ref failures);
        Run("appmodel launch plan uses shared ascii listen-host helper", TestUsesSharedAsciiListenHostHelper, ref failures);
        Run("appmodel launch plan default result is not success", TestDefaultResultIsNotSuccess, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Native app launch plan host tests failed: " + failures);
        }
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {
        Console.WriteLine("TEST_START " + name);

        try
        {
            if (test())
            {
                Console.WriteLine("TEST_PASS " + name);
                return;
            }

            failures++;
            Console.WriteLine("TEST_FAIL " + name);
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static bool TestAcceptsManifest()
    {
        byte[] source = ValidManifest("apps/hello.bin", "--port=8080", "dhcp", "serial");
        if (!TryParse(source, out NativeManifest manifest))
        {
            return false;
        }

        NativeAppLaunchPlanResult result = NativeAppLaunchPlan.Create(
            source,
            manifest,
            BootInfo.CreateHost(BootCapabilities.TextOutput),
            NativeAppLaunchRuntimeCapabilities.PackageStorage | NativeAppLaunchRuntimeCapabilities.Network);

        return result.Succeeded
            && result.IsInitialized
            && result.Status == NativeAppLaunchStatus.Success
            && result.Plan.ManifestVersion == NativeManifest.Version0
            && result.Plan.StorageRequirement == NativeAppStorageRequirement.PackageFile
            && result.Plan.RequiresPackageStorage
            && result.Plan.RequiresNetwork
            && result.Plan.RequestedNetworkMode == NativeManifestNetworkMode.Dhcp
            && result.Plan.LogMode == NativeManifestLogMode.Serial
            && result.Plan.ArgumentByteLength == "--port=8080".Length
            && result.Plan.EnvironmentCount == 0
            && result.Plan.AppKind == NativeManifestAppKind.Package
            && result.Plan.RuntimeKind == NativeManifestRuntimeKind.NativeAot
            && result.Plan.RequiredBootCapabilities == BootCapabilities.TextOutput
            && result.Plan.RequiredRuntimeCapabilities == (NativeAppLaunchRuntimeCapabilities.PackageStorage | NativeAppLaunchRuntimeCapabilities.Network)
            && result.RequiredBootCapabilities == BootCapabilities.TextOutput
            && result.RequiredRuntimeCapabilities == (NativeAppLaunchRuntimeCapabilities.PackageStorage | NativeAppLaunchRuntimeCapabilities.Network)
            && FieldEquals(source, result.Plan.AppName, "hello.app")
            && FieldEquals(source, result.Plan.AppPath, "apps/hello.bin")
            && ArgumentEquals(source, result.Plan, "--port=8080");
    }

    private static bool TestAcceptsPackagedNativeApp()
    {
        byte[] source = ValidManifest("apps/hello.bin", string.Empty, "none", "serial");
        if (!TryParse(source, out NativeManifest manifest))
        {
            return false;
        }

        NativeAppLaunchPlanResult result = NativeAppLaunchPlan.Create(
            source,
            manifest,
            BootInfo.CreateHost(BootCapabilities.TextOutput),
            NativeAppLaunchRuntimeCapabilities.PackageStorage);

        return result.Succeeded
            && result.IsInitialized
            && result.Status == NativeAppLaunchStatus.Success
            && result.Plan.AppKind == NativeManifestAppKind.Package
            && result.Plan.RuntimeKind == NativeManifestRuntimeKind.NativeAot
            && result.Plan.StorageRequirement == NativeAppStorageRequirement.PackageFile
            && result.Plan.RequiresPackageStorage
            && !result.Plan.RequiresNetwork
            && result.Plan.RequiredRuntimeCapabilities == NativeAppLaunchRuntimeCapabilities.PackageStorage
            && FieldEquals(source, result.Plan.AppPath, "apps/hello.bin");
    }

    private static bool TestCarriesEnvironmentFields()
    {
        byte[] source = Encoding.ASCII.GetBytes(
            "format=os-native-manifest\n" +
            "version=0\n" +
            "app.name=hello.app\n" +
            "app.path=apps/hello.bin\n" +
            "args=--port=8080\n" +
            "app.kind=package\n" +
            "runtime.kind=native-aot\n" +
            "network=dhcp\n" +
            "log=serial\n" +
            "env.ASPNETCORE_ENVIRONMENT=Production\n" +
            "env.PORT=8080\n");
        if (!TryParse(source, out NativeManifest manifest))
        {
            return false;
        }

        NativeAppLaunchPlanResult result = NativeAppLaunchPlan.Create(
            source,
            manifest,
            BootInfo.CreateHost(BootCapabilities.TextOutput),
            NativeAppLaunchRuntimeCapabilities.PackageStorage | NativeAppLaunchRuntimeCapabilities.Network);

        return result.Succeeded
            && result.Plan.EnvironmentCount == 2
            && EnvironmentEntryEquals(source, result.Plan.GetEnvironmentEntry(0), "ASPNETCORE_ENVIRONMENT", "Production")
            && EnvironmentEntryEquals(source, result.Plan.GetEnvironmentEntry(1), "PORT", "8080");
    }

    private static bool TestRecordsListenEndpoint()
    {
        byte[] source = ValidManifestWithListen("apps/minimal-api/app.bin", string.Empty, "dhcp", "serial", "0.0.0.0:8080");
        if (!TryParse(source, out NativeManifest manifest))
        {
            return false;
        }

        NativeAppLaunchPlanResult result = NativeAppLaunchPlan.Create(
            source,
            manifest,
            BootInfo.CreateHost(BootCapabilities.TextOutput),
            NativeAppLaunchRuntimeCapabilities.PackageStorage | NativeAppLaunchRuntimeCapabilities.Network);

        return result.Succeeded
            && result.Plan.HasListenEndpoint
            && result.Plan.ListenEndpoint.IsPresent
            && result.Plan.ListenPort == 8080
            && result.Plan.RequiresNetwork
            && result.Plan.RequiredRuntimeCapabilities == (NativeAppLaunchRuntimeCapabilities.PackageStorage | NativeAppLaunchRuntimeCapabilities.Network)
            && ListenHostEquals(source, result.Plan, "0.0.0.0");
    }

    private static bool TestRequiresNetworkForListenEndpoint()
    {
        byte[] source = ValidManifestWithListen("apps/minimal-api/app.bin", string.Empty, "dhcp", "serial", "0.0.0.0:8080");
        if (!TryParse(source, out NativeManifest manifest))
        {
            return false;
        }

        NativeAppLaunchPlanResult missingRuntimeNetwork = NativeAppLaunchPlan.Create(
            source,
            manifest,
            BootInfo.CreateHost(BootCapabilities.TextOutput),
            NativeAppLaunchRuntimeCapabilities.PackageStorage);

        NativeManifest disabledNetworkManifest = new NativeManifest(
            NativeManifest.Version0,
            manifest.AppName,
            manifest.AppPath,
            manifest.Args,
            manifest.AppKind,
            manifest.RuntimeKind,
            NativeManifestNetworkMode.None,
            manifest.LogMode,
            manifest.Environment,
            manifest.ListenEndpoint);

        NativeAppLaunchPlanResult disabledNetwork = NativeAppLaunchPlan.Create(
            source,
            disabledNetworkManifest,
            BootInfo.CreateHost(BootCapabilities.TextOutput),
            NativeAppLaunchRuntimeCapabilities.PackageStorage | NativeAppLaunchRuntimeCapabilities.Network);

        return !missingRuntimeNetwork.Succeeded
            && missingRuntimeNetwork.IsInitialized
            && missingRuntimeNetwork.Status == NativeAppLaunchStatus.MissingRuntimeCapabilities
            && missingRuntimeNetwork.RequiredRuntimeCapabilities == (NativeAppLaunchRuntimeCapabilities.PackageStorage | NativeAppLaunchRuntimeCapabilities.Network)
            && missingRuntimeNetwork.MissingRuntimeCapabilities == NativeAppLaunchRuntimeCapabilities.Network
            && !disabledNetwork.Succeeded
            && disabledNetwork.IsInitialized
            && disabledNetwork.Status == NativeAppLaunchStatus.ListenRequiresNetwork;
    }


    private static bool TestRejectsMissingCapabilities()
    {
        byte[] source = ValidManifest("apps/hello.bin", string.Empty, "dhcp", "serial");
        if (!TryParse(source, out NativeManifest manifest))
        {
            return false;
        }

        NativeAppLaunchPlanResult missingBoot = NativeAppLaunchPlan.Create(
            source,
            manifest,
            BootInfo.CreateHost(BootCapabilities.None),
            NativeAppLaunchRuntimeCapabilities.PackageStorage | NativeAppLaunchRuntimeCapabilities.Network);

        NativeAppLaunchPlanResult missingNetwork = NativeAppLaunchPlan.Create(
            source,
            manifest,
            BootInfo.CreateHost(BootCapabilities.TextOutput),
            NativeAppLaunchRuntimeCapabilities.PackageStorage);

        byte[] storageOnlySource = ValidManifest("apps/hello.bin", string.Empty, "none", "none");
        if (!TryParse(storageOnlySource, out NativeManifest storageOnlyManifest))
        {
            return false;
        }

        NativeAppLaunchPlanResult missingStorage = NativeAppLaunchPlan.Create(
            storageOnlySource,
            storageOnlyManifest,
            BootInfo.CreateHost(BootCapabilities.None),
            NativeAppLaunchRuntimeCapabilities.None);

        return !missingBoot.Succeeded
            && missingBoot.Status == NativeAppLaunchStatus.MissingBootCapabilities
            && missingBoot.RequiredBootCapabilities == BootCapabilities.TextOutput
            && missingBoot.MissingBootCapabilities == BootCapabilities.TextOutput
            && !missingNetwork.Succeeded
            && missingNetwork.Status == NativeAppLaunchStatus.MissingRuntimeCapabilities
            && missingNetwork.RequiredRuntimeCapabilities == (NativeAppLaunchRuntimeCapabilities.PackageStorage | NativeAppLaunchRuntimeCapabilities.Network)
            && missingNetwork.MissingRuntimeCapabilities == NativeAppLaunchRuntimeCapabilities.Network
            && !missingStorage.Succeeded
            && missingStorage.Status == NativeAppLaunchStatus.MissingRuntimeCapabilities
            && missingStorage.RequiredRuntimeCapabilities == NativeAppLaunchRuntimeCapabilities.PackageStorage
            && missingStorage.MissingRuntimeCapabilities == NativeAppLaunchRuntimeCapabilities.PackageStorage;
    }

    private static bool TestRejectsMalformedRequirements()
    {
        byte[] source = ValidManifest("apps/hello.bin", string.Empty, "dhcp", "serial");
        if (!TryParse(source, out NativeManifest manifest))
        {
            return false;
        }

        NativeManifest missingPathManifest = new NativeManifest(
            NativeManifest.Version0,
            manifest.AppName,
            default,
            manifest.Args,
            manifest.AppKind,
            manifest.RuntimeKind,
            NativeManifestNetworkMode.Dhcp,
            NativeManifestLogMode.Serial);

        NativeAppLaunchPlanResult missingPath = NativeAppLaunchPlan.Create(
            source,
            missingPathManifest,
            BootInfo.CreateHost(BootCapabilities.TextOutput),
            NativeAppLaunchRuntimeCapabilities.PackageStorage | NativeAppLaunchRuntimeCapabilities.Network);

        byte[] staticSource = ValidManifest("apps/hello.bin", string.Empty, "static", "none");
        if (!TryParse(staticSource, out NativeManifest staticManifest))
        {
            return false;
        }

        NativeAppLaunchPlanResult unsupportedStaticNetwork = NativeAppLaunchPlan.Create(
            staticSource,
            staticManifest,
            BootInfo.CreateHost(BootCapabilities.None),
            NativeAppLaunchRuntimeCapabilities.PackageStorage | NativeAppLaunchRuntimeCapabilities.Network);

        byte[] unsafePathSource = ValidManifest("../hello.bin", string.Empty, "none", "none");
        if (!TryParse(unsafePathSource, out NativeManifest unsafePathManifest))
        {
            return false;
        }

        NativeAppLaunchPlanResult unsafePath = NativeAppLaunchPlan.Create(
            unsafePathSource,
            unsafePathManifest,
            BootInfo.CreateHost(BootCapabilities.None),
            NativeAppLaunchRuntimeCapabilities.PackageStorage);

        NativeAppLaunchPlanResult oversizedArguments = NativeAppLaunchPlan.Create(
            CreateOversizedArgumentSource(),
            CreateOversizedArgumentManifest(),
            BootInfo.CreateHost(BootCapabilities.None),
            NativeAppLaunchRuntimeCapabilities.PackageStorage);

        return !missingPath.Succeeded
            && missingPath.Status == NativeAppLaunchStatus.MissingAppPath
            && !unsupportedStaticNetwork.Succeeded
            && unsupportedStaticNetwork.Status == NativeAppLaunchStatus.UnsupportedNetworkMode
            && !unsafePath.Succeeded
            && unsafePath.Status == NativeAppLaunchStatus.AppPathNotPackageRelative
            && LaunchStatusForPathIs(string.Empty, NativeAppLaunchStatus.MissingAppPath)
            && LaunchStatusForPathIs("/apps/hello.bin", NativeAppLaunchStatus.AppPathNotPackageRelative)
            && LaunchStatusForPathIs("\\apps\\hello.bin", NativeAppLaunchStatus.AppPathNotPackageRelative)
            && LaunchStatusForPathIs("apps\\hello.bin", NativeAppLaunchStatus.AppPathNotPackageRelative)
            && LaunchStatusForPathIs("apps:hello.bin", NativeAppLaunchStatus.AppPathNotPackageRelative)
            && LaunchStatusForPathIs("apps//hello.bin", NativeAppLaunchStatus.AppPathNotPackageRelative)
            && LaunchStatusForPathIs("apps/", NativeAppLaunchStatus.AppPathNotPackageRelative)
            && LaunchStatusForPathIs("apps/./hello.bin", NativeAppLaunchStatus.AppPathNotPackageRelative)
            && LaunchStatusForPathIs("apps/../hello.bin", NativeAppLaunchStatus.AppPathNotPackageRelative)
            && !oversizedArguments.Succeeded
            && oversizedArguments.Status == NativeAppLaunchStatus.ArgumentsTooLong;
    }

    private static bool TestUsesSharedMemoryDotSegmentHelper()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "OS.Kernel", "Manifest", "NativeAppLaunchPlan.cs"));

        return source.Contains(
                "OS.Std.MemoryExtensions.SequenceEqual(segment, \".\"u8)",
                StringComparison.Ordinal)
            && source.Contains(
                "OS.Std.MemoryExtensions.SequenceEqual(segment, \"..\"u8)",
                StringComparison.Ordinal)
            && !source.Contains(
                "segment[0] == (byte)'.'",
                StringComparison.Ordinal)
            && !source.Contains(
                "segment[1] == (byte)'.'",
                StringComparison.Ordinal);
    }

    private static bool TestUsesSharedAsciiListenHostHelper()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "OS.Kernel", "Manifest", "NativeAppLaunchPlan.cs"));

        return source.Contains(
                "OS.Std.Ascii.IsLetterOrDigit(value)",
                StringComparison.Ordinal)
            && !source.Contains(
                "IsAsciiUpperAlpha",
                StringComparison.Ordinal)
            && !source.Contains(
                "IsAsciiLowerAlpha",
                StringComparison.Ordinal)
            && !source.Contains(
                "IsAsciiDigit",
                StringComparison.Ordinal);
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        NativeAppLaunchPlanResult result = default;

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == NativeAppLaunchStatus.Success
            && result.RequiredBootCapabilities == BootCapabilities.None
            && result.MissingBootCapabilities == BootCapabilities.None
            && result.UnknownBootCapabilities == BootCapabilities.None
            && result.RequiredRuntimeCapabilities == NativeAppLaunchRuntimeCapabilities.None
            && result.MissingRuntimeCapabilities == NativeAppLaunchRuntimeCapabilities.None
            && !RuntimeHelpers.IsReferenceOrContainsReferences<NativeAppLaunchPlanResult>();
    }

    private static bool TryParse(byte[] source, out NativeManifest manifest)
    {
        NativeManifestParseResult result = NativeManifestParser.Parse(source);
        manifest = result.Manifest;
        return result.Succeeded;
    }

    private static byte[] ValidManifest(string appPath, string args, string network, string log)
    {
        return Encoding.ASCII.GetBytes(
            "format=os-native-manifest\n" +
            "version=0\n" +
            "app.name=hello.app\n" +
            "app.path=" + appPath + "\n" +
            "args=" + args + "\n" +
            "app.kind=package\n" +
            "runtime.kind=native-aot\n" +
            "network=" + network + "\n" +
            "log=" + log + "\n");
    }

    private static byte[] CreateOversizedArgumentSource()
    {
        byte[] source = new byte[NativeManifestParser.MaximumArgsLength + 64];
        WriteAscii(source, 0, "hello.app");
        WriteAscii(source, 16, "apps/hello.bin");

        for (int index = 32; index < 32 + NativeManifestParser.MaximumArgsLength + 1; index++)
        {
            source[index] = (byte)'a';
        }

        return source;
    }

    private static NativeManifest CreateOversizedArgumentManifest()
    {
        return new NativeManifest(
            NativeManifest.Version0,
            new NativeManifestField(0, "hello.app".Length),
            new NativeManifestField(16, "apps/hello.bin".Length),
            new NativeManifestField(32, NativeManifestParser.MaximumArgsLength + 1),
            NativeManifestAppKind.Package,
            NativeManifestRuntimeKind.NativeAot,
            NativeManifestNetworkMode.None,
            NativeManifestLogMode.None);
    }

    private static bool LaunchStatusForPathIs(string appPath, NativeAppLaunchStatus expectedStatus)
    {
        const string appName = "hello.app";
        byte[] source = new byte[appName.Length + appPath.Length];
        WriteAscii(source, 0, appName);
        WriteAscii(source, appName.Length, appPath);

        NativeManifest manifest = new NativeManifest(
            NativeManifest.Version0,
            new NativeManifestField(0, appName.Length),
            new NativeManifestField(appName.Length, appPath.Length),
            new NativeManifestField(source.Length, 0),
            NativeManifestAppKind.Package,
            NativeManifestRuntimeKind.NativeAot,
            NativeManifestNetworkMode.None,
            NativeManifestLogMode.None);

        NativeAppLaunchPlanResult result = NativeAppLaunchPlan.Create(
            source,
            manifest,
            BootInfo.CreateHost(BootCapabilities.None),
            NativeAppLaunchRuntimeCapabilities.PackageStorage);

        return !result.Succeeded
            && result.IsInitialized
            && result.Status == expectedStatus;
    }

    private static byte[] ValidManifestWithListen(
        string appPath,
        string args,
        string network,
        string log,
        string listenEndpoint)
    {
        return Encoding.ASCII.GetBytes(
            "format=os-native-manifest\n" +
            "version=0\n" +
            "app.name=hello.app\n" +
            "app.path=" + appPath + "\n" +
            "args=" + args + "\n" +
            "app.kind=package\n" +
            "runtime.kind=native-aot\n" +
            "network=" + network + "\n" +
            "listen=" + listenEndpoint + "\n" +
            "log=" + log + "\n");
    }

    private static bool FieldEquals(byte[] source, NativeManifestField field, string expected)
    {
        if (!field.TryGetSlice(source, out ReadOnlySpan<byte> value) || value.Length != expected.Length)
        {
            return false;
        }

        for (int index = 0; index < value.Length; index++)
        {
            if (value[index] != (byte)expected[index])
            {
                return false;
            }
        }

        return true;
    }

    private static bool EnvironmentEntryEquals(
        byte[] source,
        NativeManifestEnvironmentEntry entry,
        string expectedKey,
        string expectedValue)
    {
        return entry.IsPresent
            && FieldEquals(source, entry.Key, expectedKey)
            && FieldEquals(source, entry.Value, expectedValue);
    }

    private static bool ArgumentEquals(byte[] source, NativeAppLaunchPlan plan, string expected)
    {
        if (!plan.TryGetArguments(source, out ReadOnlySpan<byte> value) || value.Length != expected.Length)
        {
            return false;
        }

        for (int index = 0; index < value.Length; index++)
        {
            if (value[index] != (byte)expected[index])
            {
                return false;
            }
        }

        return true;
    }

    private static bool ListenHostEquals(byte[] source, NativeAppLaunchPlan plan, string expected)
    {
        if (!plan.TryGetListenHost(source, out ReadOnlySpan<byte> value) || value.Length != expected.Length)
        {
            return false;
        }

        for (int index = 0; index < value.Length; index++)
        {
            if (value[index] != (byte)expected[index])
            {
                return false;
            }
        }

        return true;
    }

    private static void WriteAscii(byte[] source, int offset, string value)
    {
        for (int index = 0; index < value.Length; index++)
        {
            source[offset + index] = (byte)value[index];
        }
    }
}
