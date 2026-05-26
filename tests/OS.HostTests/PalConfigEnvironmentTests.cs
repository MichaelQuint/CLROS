using System.Runtime.CompilerServices;
using System.Text;
using OS.Boot;
using OS.Manifest;
using OS.Pal;

internal static class PalConfigEnvironmentTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("pal.config returns manifest config value", TestReturnsConfigValue, ref failures);
        Run("pal.config reports buffer too small", TestConfigBufferTooSmall, ref failures);
        Run("pal.env returns manifest environment value", TestReturnsEnvironmentValue, ref failures);
        Run("pal.env enumerates environment entries", TestEnumeratesEnvironmentEntries, ref failures);
        Run("pal.env rejects invalid queries", TestRejectsInvalidQueries, ref failures);
        Run("pal.config table builds from native manifest", TestBuildsFromNativeManifest, ref failures);
        Run("pal.config table reports deterministic capacity", TestReportsDeterministicCapacity, ref failures);
        Run("pal.config table exposes launch fields", TestBuiltTableExposesLaunchFields, ref failures);
        Run("pal.config table exposes manifest listen endpoint", TestBuiltTableExposesManifestListenEndpoint, ref failures);
        Run("pal.config table exposes listen host and port", TestBuiltTableExposesListenHostAndPort, ref failures);
        Run("pal.config facade returns manifest listen endpoint", TestFacadeReturnsManifestListenEndpoint, ref failures);
        Run("pal.config table omits listen endpoint when absent", TestOmitsListenEndpointWhenAbsent, ref failures);
        Run("pal.config table preserves host-only boundary", TestPreservesHostOnlyBoundary, ref failures);
        Run("pal.env table exposes manifest environment", TestBuiltTableExposesEnvironment, ref failures);
        Run("pal.config table rejects invalid manifest slices", TestRejectsInvalidManifestSlices, ref failures);
        Run("pal.config table uses shared memory equality helper", TestUsesSharedMemoryEqualityHelper, ref failures);
        Run("pal.config table uses shared buffer operations", TestUsesSharedBufferOperations, ref failures);
        Run("pal.config table port formatter uses shared ascii text builder", TestPortFormatterUsesSharedAsciiTextBuilder, ref failures);
        Run("pal.config table default results are not success", TestDefaultResultsAreNotSuccess, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("PAL config/environment host tests failed: " + failures);
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

    private static bool TestReturnsConfigValue()
    {
        OSHostConfigEnvironmentTable table = CreateTable();
        byte[] destination = new byte[32];

        OSHostConfigQueryResult result = table.TryGetConfigValue(
            Bytes("app.name"),
            destination);

        return result.Succeeded
            && result.IsInitialized
            && result.Status == OSHostStatus.Ok
            && result.BytesWritten == (ulong)"minimal.api".Length
            && result.RequiredBytes == result.BytesWritten
            && BytesEqual(destination, (int)result.BytesWritten, "minimal.api");
    }

    private static bool TestConfigBufferTooSmall()
    {
        OSHostConfigEnvironmentTable table = CreateTable();
        byte[] destination = new byte[3];

        OSHostConfigQueryResult result = table.TryGetConfigValue(
            Bytes("runtime.assembly"),
            destination);

        return !result.Succeeded
            && result.IsInitialized
            && result.Status == OSHostStatus.BufferTooSmall
            && result.BytesWritten == 0
            && result.RequiredBytes == (ulong)"apps/api/MinimalApi.dll".Length;
    }

    private static bool TestReturnsEnvironmentValue()
    {
        OSHostConfigEnvironmentTable table = CreateTable();
        byte[] destination = new byte[32];

        OSHostConfigQueryResult result = table.TryGetEnvironmentValue(
            Bytes("ASPNETCORE_ENVIRONMENT"),
            destination);

        return result.Succeeded
            && result.IsInitialized
            && result.Status == OSHostStatus.Ok
            && result.BytesWritten == (ulong)"Production".Length
            && BytesEqual(destination, (int)result.BytesWritten, "Production");
    }

    private static bool TestEnumeratesEnvironmentEntries()
    {
        OSHostConfigEnvironmentTable table = CreateTable();
        byte[] keyDestination = new byte[32];
        byte[] valueDestination = new byte[32];

        OSHostEnvironmentEnumerationResult result = table.EnumerateEnvironment(
            1,
            keyDestination,
            valueDestination);

        return result.Succeeded
            && result.IsInitialized
            && result.Status == OSHostStatus.Ok
            && result.KeyBytesWritten == (ulong)"PORT".Length
            && result.ValueBytesWritten == (ulong)"8080".Length
            && result.RequiredKeyBytes == result.KeyBytesWritten
            && result.RequiredValueBytes == result.ValueBytesWritten
            && BytesEqual(keyDestination, (int)result.KeyBytesWritten, "PORT")
            && BytesEqual(valueDestination, (int)result.ValueBytesWritten, "8080");
    }

    private static bool TestRejectsInvalidQueries()
    {
        OSHostConfigEnvironmentTable table = CreateTable();
        byte[] destination = new byte[32];
        byte[] smallKeyDestination = new byte[3];
        byte[] valueDestination = new byte[32];

        OSHostConfigQueryResult emptyKey = table.TryGetConfigValue(
            ReadOnlySpan<byte>.Empty,
            destination);
        OSHostConfigQueryResult missingEnv = table.TryGetEnvironmentValue(
            Bytes("MISSING"),
            destination);
        OSHostEnvironmentEnumerationResult outOfRange = table.EnumerateEnvironment(
            9,
            destination,
            valueDestination);
        OSHostEnvironmentEnumerationResult tooSmall = table.EnumerateEnvironment(
            0,
            smallKeyDestination,
            valueDestination);

        return emptyKey.Status == OSHostStatus.InvalidArgument
            && emptyKey.IsInitialized
            && missingEnv.Status == OSHostStatus.NotFound
            && missingEnv.IsInitialized
            && outOfRange.Status == OSHostStatus.NotFound
            && outOfRange.IsInitialized
            && tooSmall.Status == OSHostStatus.BufferTooSmall
            && tooSmall.IsInitialized
            && tooSmall.RequiredKeyBytes == (ulong)"ASPNETCORE_ENVIRONMENT".Length
            && tooSmall.RequiredValueBytes == (ulong)"Production".Length;
    }

    private static bool TestBuildsFromNativeManifest()
    {
        byte[] source = NativeManifestBytes();
        if (!TryCreatePlan(source, out NativeAppLaunchPlan plan))
        {
            return false;
        }

        OSHostConfigEnvironmentTableBuildResult result =
            OSHostConfigEnvironmentTable.FromNativeLaunchPlan(source, plan);

        return result.Succeeded
            && result.Status == OSHostStatus.Ok
            && result.Table is not null
            && result.Table.ConfigCount == 7
            && result.Table.EnvironmentCount == 2;
    }

    private static bool TestReportsDeterministicCapacity()
    {
        OSHostConfigEnvironmentEntry[] config =
            CreateEntries("config.", OSHostConfigEnvironmentTable.MaxConfigEntries);
        OSHostConfigEnvironmentEntry[] environment =
            CreateEntries("ENV_", OSHostConfigEnvironmentTable.MaxEnvironmentEntries);

        OSHostConfigEnvironmentTableBuildResult exact =
            OSHostConfigEnvironmentTable.Create(config, environment);
        if (!exact.Succeeded || exact.Table is null)
        {
            return false;
        }

        OSHostConfigEnvironmentEntry[] tooManyConfig =
            CreateEntries("config.", OSHostConfigEnvironmentTable.MaxConfigEntries + 1);
        OSHostConfigEnvironmentEntry[] tooManyEnvironment =
            CreateEntries("ENV_", OSHostConfigEnvironmentTable.MaxEnvironmentEntries + 1);

        OSHostConfigEnvironmentTableBuildResult configOverflow =
            OSHostConfigEnvironmentTable.Create(tooManyConfig, environment);
        OSHostConfigEnvironmentTableBuildResult environmentOverflow =
            OSHostConfigEnvironmentTable.Create(config, tooManyEnvironment);

        return exact.Table.ConfigCount == OSHostConfigEnvironmentTable.MaxConfigEntries
            && exact.Table.EnvironmentCount == OSHostConfigEnvironmentTable.MaxEnvironmentEntries
            && configOverflow.Status == OSHostStatus.OutOfMemory
            && configOverflow.Table is null
            && !configOverflow.Succeeded
            && environmentOverflow.Status == OSHostStatus.OutOfMemory
            && environmentOverflow.Table is null
            && !environmentOverflow.Succeeded;
    }

    private static bool TestBuiltTableExposesLaunchFields()
    {
        byte[] source = NativeManifestBytes();
        if (!TryCreatePlan(source, out NativeAppLaunchPlan plan))
        {
            return false;
        }

        OSHostConfigEnvironmentTableBuildResult build =
            OSHostConfigEnvironmentTable.FromNativeLaunchPlan(source, plan);
        if (!build.Succeeded || build.Table is null)
        {
            return false;
        }

        byte[] destination = new byte[32];
        OSHostConfigQueryResult appPath = build.Table.TryGetConfigValue(
            Bytes("app.path"),
            destination);
        bool appPathOk = appPath.Succeeded
            && BytesEqual(destination, (int)appPath.BytesWritten, "apps/hello.bin");

        Array.Clear(destination);
        OSHostConfigQueryResult runtime = build.Table.TryGetConfigValue(
            Bytes("runtime.kind"),
            destination);
        bool runtimeOk = runtime.Succeeded
            && BytesEqual(destination, (int)runtime.BytesWritten, "native-aot");

        Array.Clear(destination);
        OSHostConfigQueryResult network = build.Table.TryGetConfigValue(
            Bytes("network"),
            destination);

        return appPathOk
            && runtimeOk
            && network.Succeeded
            && BytesEqual(destination, (int)network.BytesWritten, "dhcp");
    }

    private static bool TestBuiltTableExposesEnvironment()
    {
        byte[] source = NativeManifestBytes();
        if (!TryCreatePlan(source, out NativeAppLaunchPlan plan))
        {
            return false;
        }

        OSHostConfigEnvironmentTableBuildResult build =
            OSHostConfigEnvironmentTable.FromNativeLaunchPlan(source, plan);
        if (!build.Succeeded || build.Table is null)
        {
            return false;
        }

        byte[] destination = new byte[32];
        OSHostConfigQueryResult value = build.Table.TryGetEnvironmentValue(
            Bytes("PORT"),
            destination);

        return value.Succeeded
            && BytesEqual(destination, (int)value.BytesWritten, "8080");
    }

    private static bool TestBuiltTableExposesManifestListenEndpoint()
    {
        byte[] source = NativeManifestWithListenBytes("0.0.0.0:8080");
        if (!TryCreatePlan(source, out NativeAppLaunchPlan plan))
        {
            return false;
        }

        OSHostConfigEnvironmentTableBuildResult build =
            OSHostConfigEnvironmentTable.FromNativeLaunchPlan(source, plan);
        if (!build.Succeeded || build.Table is null)
        {
            return false;
        }

        byte[] destination = new byte[64];
        OSHostConfigQueryResult endpoint = build.Table.TryGetConfigValue(
            Bytes("kestrel.listen.0"),
            destination);

        return build.Table.ConfigCount == 10
            && endpoint.Succeeded
            && BytesEqual(destination, (int)endpoint.BytesWritten, "http://0.0.0.0:8080");
    }

    private static bool TestBuiltTableExposesListenHostAndPort()
    {
        byte[] source = NativeManifestWithListenBytes("192.0.2.42:9090");
        if (!TryCreatePlan(source, out NativeAppLaunchPlan plan))
        {
            return false;
        }

        OSHostConfigEnvironmentTableBuildResult build =
            OSHostConfigEnvironmentTable.FromNativeLaunchPlan(source, plan);
        if (!build.Succeeded || build.Table is null)
        {
            return false;
        }

        byte[] destination = new byte[64];
        OSHostConfigQueryResult host = build.Table.TryGetConfigValue(
            Bytes("kestrel.listen.0.host"),
            destination);
        bool hostOk = host.Succeeded
            && BytesEqual(destination, (int)host.BytesWritten, "192.0.2.42");

        Array.Clear(destination);
        OSHostConfigQueryResult port = build.Table.TryGetConfigValue(
            Bytes("kestrel.listen.0.port"),
            destination);

        return hostOk
            && port.Succeeded
            && BytesEqual(destination, (int)port.BytesWritten, "9090");
    }

    private static bool TestFacadeReturnsManifestListenEndpoint()
    {
        byte[] source = NativeManifestWithListenBytes("0.0.0.0:8080");
        if (!TryCreatePlan(source, out NativeAppLaunchPlan plan))
        {
            return false;
        }

        OSHostConfigEnvironmentTableBuildResult build =
            OSHostConfigEnvironmentTable.FromNativeLaunchPlan(source, plan);
        if (!build.Succeeded || build.Table is null)
        {
            return false;
        }

        OSHostConfigEnvironmentFacade facade = new(build.Table);
        byte[] key = Bytes("kestrel.listen.0");
        byte[] destination = new byte[64];
        OSHostConfigQueryResult endpoint = facade.GetConfigValue(
            Query(key, destination),
            key,
            destination);

        return endpoint.Succeeded
            && BytesEqual(destination, (int)endpoint.BytesWritten, "http://0.0.0.0:8080");
    }

    private static bool TestOmitsListenEndpointWhenAbsent()
    {
        byte[] source = NativeManifestBytes();
        if (!TryCreatePlan(source, out NativeAppLaunchPlan plan))
        {
            return false;
        }

        OSHostConfigEnvironmentTableBuildResult build =
            OSHostConfigEnvironmentTable.FromNativeLaunchPlan(source, plan);
        if (!build.Succeeded || build.Table is null)
        {
            return false;
        }

        byte[] destination = new byte[64];
        OSHostConfigQueryResult endpoint = build.Table.TryGetConfigValue(
            Bytes("kestrel.listen.0"),
            destination);

        return build.Table.ConfigCount == 7
            && endpoint.Status == OSHostStatus.NotFound;
    }

    private static bool TestPreservesHostOnlyBoundary()
    {
        return OSHostConfigEnvironmentTable.HostOnly
            && !OSHostConfigEnvironmentTable.ExportsPalFunctions
            && !OSHostConfigEnvironmentTable.ReadsGuestStorage
            && !OSHostConfigEnvironmentTable.OpensSockets
            && !OSHostConfigEnvironmentTable.StartsKestrel
            && !RuntimeHelpers.IsReferenceOrContainsReferences<OSHostConfigQueryResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<OSHostEnvironmentEnumerationResult>();
    }

    private static bool TestDefaultResultsAreNotSuccess()
    {
        OSHostConfigQueryResult query = default;
        OSHostEnvironmentEnumerationResult enumeration = default;

        return !query.IsInitialized
            && !query.Succeeded
            && query.Status == OSHostStatus.Ok
            && query.BytesWritten == 0
            && query.RequiredBytes == 0
            && !enumeration.IsInitialized
            && !enumeration.Succeeded
            && enumeration.Status == OSHostStatus.Ok
            && enumeration.KeyBytesWritten == 0
            && enumeration.ValueBytesWritten == 0
            && enumeration.RequiredKeyBytes == 0
            && enumeration.RequiredValueBytes == 0
            && !RuntimeHelpers.IsReferenceOrContainsReferences<OSHostConfigQueryResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<OSHostEnvironmentEnumerationResult>();
    }

    private static bool TestRejectsInvalidManifestSlices()
    {
        byte[] source = NativeManifestBytes();
        if (!TryCreatePlan(source, out NativeAppLaunchPlan plan))
        {
            return false;
        }

        OSHostConfigEnvironmentTableBuildResult result =
            OSHostConfigEnvironmentTable.FromNativeLaunchPlan(source.AsMemory(0, 8), plan);
        return !result.Succeeded
            && result.Status == OSHostStatus.InvalidArgument
            && result.Table is null;
    }

    private static bool TestUsesSharedMemoryEqualityHelper()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "OS.Kernel", "Pal", "OSHostConfigEnvironmentTable.cs"));

        return source.Contains(
                "OS.Std.MemoryExtensions.SequenceEqual(key, keyUtf8)",
                StringComparison.Ordinal)
            && !source.Contains(
                "private static bool ByteSequenceEqual",
                StringComparison.Ordinal);
    }

    private static bool TestPortFormatterUsesSharedAsciiTextBuilder()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "OS.Kernel", "Pal", "OSHostConfigEnvironmentTable.cs"));

        return source.Contains("using OS.Std.Text;", StringComparison.Ordinal)
            && source.Contains(
                "AsciiDecimalFormatter.CountUInt64Digits(port)",
                StringComparison.Ordinal)
            && source.Contains(
                "AsciiTextBuilder.AppendUInt64Decimal(bytes, 0, port)",
                StringComparison.Ordinal)
            && !source.Contains("Span<byte> reversed", StringComparison.Ordinal)
            && !source.Contains("value % 10", StringComparison.Ordinal)
            && !source.Contains("value /= 10", StringComparison.Ordinal)
            && !source.Contains("reversed[count - index - 1]", StringComparison.Ordinal);
    }

    private static bool TestUsesSharedBufferOperations()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "OS.Kernel", "Pal", "OSHostConfigEnvironmentTable.cs"));

        return source.Contains("using OS.Std.Memory;", StringComparison.Ordinal)
            && source.Contains(
                "BufferOperations.MemoryCopy(key, keyDestination)",
                StringComparison.Ordinal)
            && source.Contains(
                "BufferOperations.MemoryCopy(value, valueDestination)",
                StringComparison.Ordinal)
            && source.Contains(
                "BufferOperations.MemoryCopy(value, destination)",
                StringComparison.Ordinal)
            && source.Contains(
                "BufferOperations.MemoryCopy(host, value.AsSpan(PrefixLength, host.Length))",
                StringComparison.Ordinal)
            && source.Contains(
                "BufferOperations.MemoryCopy(port, value.AsSpan(PrefixLength + host.Length + 1, port.Length))",
                StringComparison.Ordinal)
            && !source.Contains("key.CopyTo(keyDestination)", StringComparison.Ordinal)
            && !source.Contains("value.CopyTo(valueDestination)", StringComparison.Ordinal)
            && !source.Contains("value.CopyTo(destination)", StringComparison.Ordinal)
            && !source.Contains("host.CopyTo(value.AsSpan(PrefixLength))", StringComparison.Ordinal)
            && !source.Contains(
                "port.CopyTo(value.AsSpan(PrefixLength + host.Length + 1))",
                StringComparison.Ordinal);
    }

    private static OSHostConfigEnvironmentTable CreateTable()
    {
        OSHostConfigEnvironmentEntry[] config =
        [
            Entry("app.name", "minimal.api"),
            Entry("runtime.assembly", "apps/api/MinimalApi.dll"),
            Entry("kestrel.listen.0", "http://0.0.0.0:8080")
        ];
        OSHostConfigEnvironmentEntry[] environment =
        [
            Entry("ASPNETCORE_ENVIRONMENT", "Production"),
            Entry("PORT", "8080")
        ];
        OSHostConfigEnvironmentTableBuildResult build =
            OSHostConfigEnvironmentTable.Create(config, environment);
        if (!build.Succeeded || build.Table is null)
        {
            throw new InvalidOperationException("Unable to create PAL config/environment test table.");
        }

        return build.Table;
    }

    private static bool TryCreatePlan(byte[] source, out NativeAppLaunchPlan plan)
    {
        NativeManifestParseResult parsed = NativeManifestParser.Parse(source);
        if (!parsed.Succeeded)
        {
            plan = default;
            return false;
        }

        NativeAppLaunchPlanResult result = NativeAppLaunchPlan.Create(
            source,
            parsed.Manifest,
            BootInfo.CreateHost(BootCapabilities.TextOutput),
            NativeAppLaunchRuntimeCapabilities.PackageStorage | NativeAppLaunchRuntimeCapabilities.Network);
        plan = result.Plan;
        return result.Succeeded;
    }

    private static byte[] NativeManifestBytes()
    {
        return Encoding.ASCII.GetBytes(
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
    }

    private static byte[] NativeManifestWithListenBytes(string listen)
    {
        return Encoding.ASCII.GetBytes(
            "format=os-native-manifest\n" +
            "version=0\n" +
            "app.name=hello.app\n" +
            "app.path=apps/hello.bin\n" +
            "args=--port=8080\n" +
            "app.kind=package\n" +
            "runtime.kind=native-aot\n" +
            "network=dhcp\n" +
            "listen=" + listen + "\n" +
            "log=serial\n" +
            "env.ASPNETCORE_ENVIRONMENT=Production\n" +
            "env.PORT=8080\n");
    }

    private static OSHostConfigQuery Query(byte[] key, byte[] destination)
    {
        return new OSHostConfigQuery(
            new OSHostByteSpan(0x1000, (ulong)key.Length),
            new OSHostByteSpan(0x2000, (ulong)destination.Length));
    }

    private static OSHostConfigEnvironmentEntry Entry(string key, string value)
    {
        return new OSHostConfigEnvironmentEntry(Bytes(key), Bytes(value));
    }

    private static OSHostConfigEnvironmentEntry[] CreateEntries(string keyPrefix, int count)
    {
        OSHostConfigEnvironmentEntry[] entries = new OSHostConfigEnvironmentEntry[count];
        for (int index = 0; index < entries.Length; index++)
        {
            entries[index] = Entry(keyPrefix + index, "value");
        }

        return entries;
    }

    private static byte[] Bytes(string value)
    {
        return Encoding.ASCII.GetBytes(value);
    }

    private static bool BytesEqual(byte[] source, int length, string expected)
    {
        if (length != expected.Length)
        {
            return false;
        }

        for (int index = 0; index < length; index++)
        {
            if (source[index] != (byte)expected[index])
            {
                return false;
            }
        }

        return true;
    }
}
