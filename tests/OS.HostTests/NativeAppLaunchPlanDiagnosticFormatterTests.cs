using System.Runtime.CompilerServices;
using System.Text;
using OS.Boot;
using OS.Manifest;

internal static class NativeAppLaunchPlanDiagnosticFormatterTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("appmodel launch diagnostic formats selected app", TestFormatsSelectedApp, ref failures);
        Run("appmodel launch diagnostic reports short destination", TestReportsShortDestination, ref failures);
        Run("appmodel launch diagnostic formats missing runtime", TestFormatsMissingRuntime, ref failures);
        Run("appmodel launch diagnostic rejects default result", TestRejectsDefaultResult, ref failures);
        Run(
            "appmodel launch plan formatter uses shared ascii text builder",
            TestFormatterUsesSharedAsciiTextBuilder,
            ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Native app launch diagnostic host tests failed: " + failures);
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

    private static bool TestFormatsSelectedApp()
    {
        byte[] source = ManifestBytes(
            "--port=8080",
            "dhcp",
            "serial",
            includeListen: true,
            includeEnvironment: true);
        if (!TryCreateLaunchPlan(
            source,
            NativeAppLaunchRuntimeCapabilities.PackageStorage |
            NativeAppLaunchRuntimeCapabilities.Network,
            out NativeAppLaunchPlanResult launch))
        {
            return false;
        }

        NativeAppLaunchPlanDiagnosticFormatResult query =
            NativeAppLaunchPlanDiagnosticFormatter.QueryRequiredBytes(launch, source);
        byte[] destination = new byte[query.RequiredBytes];
        NativeAppLaunchPlanDiagnosticFormatResult format =
            NativeAppLaunchPlanDiagnosticFormatter.FormatResult(launch, source, destination);

        string expected =
            "APP_LAUNCH_PLAN status=Success initialized=true succeeded=true " +
            "appNameBytes=9 appPathBytes=14 argumentBytes=11 environmentCount=1 " +
            "hasListenEndpoint=true listenPort=8080 requiresPackageStorage=true " +
            "requiresNetwork=true requiredBootCapabilities=1 missingBootCapabilities=0 " +
            "unknownBootCapabilities=0 requiredRuntimeCapabilities=3 " +
            "missingRuntimeCapabilities=0 appSelected=true appLoaded=false " +
            "appExecuted=false hostedDotNetStarted=false kestrelStarted=false";

        return launch.Succeeded
            && query.IsInitialized
            && !query.Succeeded
            && query.Status == NativeAppLaunchPlanDiagnosticFormatStatus.Success
            && query.LaunchStatus == NativeAppLaunchStatus.Success
            && query.RequiredBytes == expected.Length
            && format.Succeeded
            && format.Status == NativeAppLaunchPlanDiagnosticFormatStatus.Success
            && format.LaunchStatus == NativeAppLaunchStatus.Success
            && format.BytesWritten == expected.Length
            && format.RequiredBytes == expected.Length
            && format.AvailableBytes == destination.Length
            && Encoding.ASCII.GetString(destination) == expected
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                NativeAppLaunchPlanDiagnosticFormatResult>();
    }

    private static bool TestReportsShortDestination()
    {
        byte[] source = ManifestBytes(
            "--port=8080",
            "dhcp",
            "serial",
            includeListen: true,
            includeEnvironment: false);
        if (!TryCreateLaunchPlan(
            source,
            NativeAppLaunchRuntimeCapabilities.PackageStorage |
            NativeAppLaunchRuntimeCapabilities.Network,
            out NativeAppLaunchPlanResult launch))
        {
            return false;
        }

        NativeAppLaunchPlanDiagnosticFormatResult query =
            NativeAppLaunchPlanDiagnosticFormatter.QueryRequiredBytes(launch, source);
        byte[] destination = new byte[query.RequiredBytes - 1];
        Array.Fill(destination, (byte)0xCC);

        NativeAppLaunchPlanDiagnosticFormatResult format =
            NativeAppLaunchPlanDiagnosticFormatter.FormatResult(launch, source, destination);

        return launch.Succeeded
            && query.Status == NativeAppLaunchPlanDiagnosticFormatStatus.Success
            && format.IsInitialized
            && !format.Succeeded
            && format.Status == NativeAppLaunchPlanDiagnosticFormatStatus.DestinationTooSmall
            && format.LaunchStatus == NativeAppLaunchStatus.Success
            && format.BytesWritten == 0
            && format.RequiredBytes == query.RequiredBytes
            && format.AvailableBytes == destination.Length
            && AllBytesEqual(destination, 0xCC);
    }

    private static bool TestFormatsMissingRuntime()
    {
        byte[] source = ManifestBytes(
            "--port=8080",
            "dhcp",
            "serial",
            includeListen: false,
            includeEnvironment: false);
        if (!TryParse(source, out NativeManifest manifest))
        {
            return false;
        }

        NativeAppLaunchPlanResult launch = NativeAppLaunchPlan.Create(
            source,
            manifest,
            BootInfo.CreateHost(BootCapabilities.TextOutput),
            NativeAppLaunchRuntimeCapabilities.PackageStorage);

        NativeAppLaunchPlanDiagnosticFormatResult query =
            NativeAppLaunchPlanDiagnosticFormatter.QueryRequiredBytes(launch, source);
        byte[] destination = new byte[query.RequiredBytes];
        NativeAppLaunchPlanDiagnosticFormatResult format =
            NativeAppLaunchPlanDiagnosticFormatter.FormatResult(launch, source, destination);

        string expected =
            "APP_LAUNCH_PLAN status=MissingRuntimeCapabilities initialized=true " +
            "succeeded=false appNameBytes=0 appPathBytes=0 argumentBytes=0 " +
            "environmentCount=0 hasListenEndpoint=false listenPort=0 " +
            "requiresPackageStorage=false requiresNetwork=false " +
            "requiredBootCapabilities=1 missingBootCapabilities=0 " +
            "unknownBootCapabilities=0 requiredRuntimeCapabilities=3 " +
            "missingRuntimeCapabilities=2 appSelected=false appLoaded=false " +
            "appExecuted=false hostedDotNetStarted=false kestrelStarted=false";

        return !launch.Succeeded
            && launch.IsInitialized
            && launch.Status == NativeAppLaunchStatus.MissingRuntimeCapabilities
            && query.Status == NativeAppLaunchPlanDiagnosticFormatStatus.Success
            && query.LaunchStatus == NativeAppLaunchStatus.MissingRuntimeCapabilities
            && query.RequiredBytes == expected.Length
            && format.Succeeded
            && format.LaunchStatus == NativeAppLaunchStatus.MissingRuntimeCapabilities
            && Encoding.ASCII.GetString(destination) == expected;
    }

    private static bool TestRejectsDefaultResult()
    {
        NativeAppLaunchPlanResult defaultResult = default;
        byte[] source = ManifestBytes(
            string.Empty,
            "none",
            "none",
            includeListen: false,
            includeEnvironment: false);
        byte[] destination = new byte[128];
        Array.Fill(destination, (byte)0xA5);

        NativeAppLaunchPlanDiagnosticFormatResult format =
            NativeAppLaunchPlanDiagnosticFormatter.FormatResult(defaultResult, source, destination);
        NativeAppLaunchPlanDiagnosticFormatResult query =
            NativeAppLaunchPlanDiagnosticFormatter.QueryRequiredBytes(defaultResult, source);
        NativeAppLaunchPlanDiagnosticFormatResult defaultFormat = default;

        return !defaultResult.IsInitialized
            && !defaultResult.Succeeded
            && format.IsInitialized
            && !format.Succeeded
            && format.Status == NativeAppLaunchPlanDiagnosticFormatStatus.InvalidResult
            && format.LaunchStatus == NativeAppLaunchStatus.Success
            && format.BytesWritten == 0
            && format.RequiredBytes == 0
            && format.AvailableBytes == destination.Length
            && query.IsInitialized
            && !query.Succeeded
            && query.Status == NativeAppLaunchPlanDiagnosticFormatStatus.InvalidResult
            && query.RequiredBytes == 0
            && query.AvailableBytes == 0
            && !defaultFormat.IsInitialized
            && !defaultFormat.Succeeded
            && AllBytesEqual(destination, 0xA5);
    }

    private static bool TestFormatterUsesSharedAsciiTextBuilder()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(
                repositoryRoot,
                "src",
                "OS.Kernel",
                "Manifest",
                "NativeAppLaunchPlanDiagnosticFormatter.cs"));

        return source.Contains(
                "AsciiDecimalFormatter.CountInt32Digits(appNameBytes)",
                StringComparison.Ordinal)
            && source.Contains(
                "AsciiDecimalFormatter.CountInt32Digits((int)result.RequiredBootCapabilities)",
                StringComparison.Ordinal)
            && source.Contains(
                "AsciiTextBuilder.AppendBytes(destination, offset, value)",
                StringComparison.Ordinal)
            && source.Contains(
                "AsciiTextBuilder.AppendInt64Decimal(destination, offset, value)",
                StringComparison.Ordinal)
            && !source.Contains(
                "private static int GetInt32DecimalByteCount",
                StringComparison.Ordinal)
            && !source.Contains(
                "private static int GetUInt32DecimalByteCount",
                StringComparison.Ordinal)
            && !source.Contains(
                "private static void AppendInt32",
                StringComparison.Ordinal)
            && !source.Contains(
                "private static void AppendUInt32",
                StringComparison.Ordinal)
            && !source.Contains(
                "destination[offset + index] = (byte)('0' +",
                StringComparison.Ordinal)
            && !source.Contains(
                "destination[offset] = (byte)'-'",
                StringComparison.Ordinal);
    }

    private static bool TryCreateLaunchPlan(
        byte[] source,
        NativeAppLaunchRuntimeCapabilities availableRuntimeCapabilities,
        out NativeAppLaunchPlanResult launch)
    {
        launch = default;
        if (!TryParse(source, out NativeManifest manifest))
        {
            return false;
        }

        launch = NativeAppLaunchPlan.Create(
            source,
            manifest,
            BootInfo.CreateHost(BootCapabilities.TextOutput),
            availableRuntimeCapabilities);
        return launch.Succeeded;
    }

    private static bool TryParse(byte[] source, out NativeManifest manifest)
    {
        NativeManifestParseResult result = NativeManifestParser.Parse(source);
        manifest = result.Manifest;
        return result.Succeeded;
    }

    private static byte[] ManifestBytes(
        string args,
        string network,
        string log,
        bool includeListen,
        bool includeEnvironment)
    {
        string manifest =
            "format=os-native-manifest\n" +
            "version=0\n" +
            "app.name=hello.app\n" +
            "app.path=apps/hello.bin\n" +
            "args=" + args + "\n" +
            "app.kind=package\n" +
            "runtime.kind=native-aot\n" +
            "network=" + network + "\n";

        if (includeListen)
        {
            manifest += "listen=0.0.0.0:8080\n";
        }

        manifest += "log=" + log + "\n";

        if (includeEnvironment)
        {
            manifest += "env.PORT=8080\n";
        }

        return Encoding.ASCII.GetBytes(manifest);
    }

    private static bool AllBytesEqual(byte[] bytes, byte expected)
    {
        for (int index = 0; index < bytes.Length; index++)
        {
            if (bytes[index] != expected)
            {
                return false;
            }
        }

        return true;
    }
}
