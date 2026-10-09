using System.Runtime.CompilerServices;
using System.Text;
using OS.Pal;

internal static class PalConfigEnvironmentFacadeTests
{

    [HostTest("pal.config facade returns config and env values", HostLane.Fast, 0)]
    private static bool TestReturnsConfigAndEnvironmentValues()
    {
        OSHostConfigEnvironmentFacade facade = CreateFacade();
        byte[] configKey = Bytes("kestrel.listen.0");
        byte[] envKey = Bytes("ASPNETCORE_ENVIRONMENT");
        byte[] destination = new byte[64];

        OSHostConfigQueryResult config = facade.GetConfigValue(
            Query(configKey, destination),
            configKey,
            destination);
        bool configOk = config.Succeeded
            && BytesEqual(destination, (int)config.BytesWritten, "http://0.0.0.0:8080");

        Array.Clear(destination);
        OSHostConfigQueryResult env = facade.GetEnvironmentValue(
            Query(envKey, destination),
            envKey,
            destination);

        return HostCheck.Result(configOk
            && env.Succeeded
            && BytesEqual(destination, (int)env.BytesWritten, "Production"));
    }

    [HostTest("pal.config facade enumerates config and env", HostLane.Fast, 1)]
    private static bool TestEnumeratesConfigAndEnvironment()
    {
        OSHostConfigEnvironmentFacade facade = CreateFacade();
        byte[] configKeyDestination = new byte[32];
        byte[] configValueDestination = new byte[64];
        byte[] envKeyDestination = new byte[32];
        byte[] envValueDestination = new byte[32];

        OSHostEnvironmentEnumerationResult config = facade.EnumerateConfig(
            EnumerationQuery(1, configKeyDestination, configValueDestination),
            configKeyDestination,
            configValueDestination);
        OSHostEnvironmentEnumerationResult env = facade.EnumerateEnvironment(
            EnumerationQuery(1, envKeyDestination, envValueDestination),
            envKeyDestination,
            envValueDestination);

        return HostCheck.Result(config.Succeeded
            && BytesEqual(configKeyDestination, (int)config.KeyBytesWritten, "kestrel.listen.0")
            && BytesEqual(configValueDestination, (int)config.ValueBytesWritten, "http://0.0.0.0:8080")
            && env.Succeeded
            && BytesEqual(envKeyDestination, (int)env.KeyBytesWritten, "PORT")
            && BytesEqual(envValueDestination, (int)env.ValueBytesWritten, "8080"));
    }

    [HostTest("pal.config facade reports buffer and missing keys", HostLane.Fast, 2)]
    private static bool TestReportsBufferAndMissingKeys()
    {
        OSHostConfigEnvironmentFacade facade = CreateFacade();
        byte[] key = Bytes("runtime.assembly");
        byte[] smallDestination = new byte[3];
        byte[] missingKey = Bytes("MISSING");
        byte[] destination = new byte[32];

        OSHostConfigQueryResult small = facade.GetConfigValue(
            Query(key, smallDestination),
            key,
            smallDestination);
        OSHostConfigQueryResult missing = facade.GetEnvironmentValue(
            Query(missingKey, destination),
            missingKey,
            destination);

        return HostCheck.Result(small.Status == OSHostStatus.BufferTooSmall
            && small.RequiredBytes == (ulong)"apps/api/MinimalApi.dll".Length
            && missing.Status == OSHostStatus.NotFound);
    }

    [HostTest("pal.config facade rejects malformed request shapes", HostLane.Fast, 3)]
    private static bool TestRejectsMalformedRequestShapes()
    {
        OSHostConfigEnvironmentFacade facade = CreateFacade();
        OSHostConfigEnvironmentFacade nullFacade = new(table: null);
        byte[] key = Bytes("app.name");
        byte[] destination = new byte[32];
        byte[] keyDestination = new byte[16];
        byte[] valueDestination = new byte[32];

        OSHostConfigQueryResult nullQuery = nullFacade.GetConfigValue(
            Query(key, destination),
            key,
            destination);
        OSHostConfigQueryResult keyMismatch = facade.GetConfigValue(
            new OSHostConfigQuery(new OSHostByteSpan(0x1000, 99), new OSHostByteSpan(0x2000, (ulong)destination.Length)),
            key,
            destination);
        OSHostConfigQueryResult destinationMismatch = facade.GetConfigValue(
            new OSHostConfigQuery(new OSHostByteSpan(0x1000, (ulong)key.Length), new OSHostByteSpan(0x2000, 99)),
            key,
            destination);
        OSHostEnvironmentEnumerationResult reserved = facade.EnumerateEnvironment(
            new OSHostEnvironmentEnumerationQuery(
                index: 0,
                reserved: 1,
                new OSHostByteSpan(0x3000, (ulong)keyDestination.Length),
                new OSHostByteSpan(0x4000, (ulong)valueDestination.Length)),
            keyDestination,
            valueDestination);
        OSHostEnvironmentEnumerationResult destinationShape = facade.EnumerateEnvironment(
            new OSHostEnvironmentEnumerationQuery(
                index: 0,
                reserved: 0,
                new OSHostByteSpan(0x3000, 99),
                new OSHostByteSpan(0x4000, (ulong)valueDestination.Length)),
            keyDestination,
            valueDestination);

        return HostCheck.Result(nullQuery.Status == OSHostStatus.InvalidArgument
            && keyMismatch.Status == OSHostStatus.InvalidArgument
            && destinationMismatch.Status == OSHostStatus.InvalidArgument
            && reserved.Status == OSHostStatus.InvalidArgument
            && destinationShape.Status == OSHostStatus.InvalidArgument);
    }

    [HostTest("pal.config facade result layouts contain no managed references", HostLane.Fast, 4)]
    private static bool TestResultLayoutsContainNoManagedReferences()
    {
        return HostCheck.Result(!RuntimeHelpers.IsReferenceOrContainsReferences<OSHostConfigQueryResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<OSHostEnvironmentEnumerationResult>());
    }

    private static OSHostConfigEnvironmentFacade CreateFacade()
    {
        OSHostConfigEnvironmentEntry[] config =
        [
            Entry("app.name", "minimal.api"),
            Entry("kestrel.listen.0", "http://0.0.0.0:8080"),
            Entry("runtime.assembly", "apps/api/MinimalApi.dll")
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
            throw new InvalidOperationException("Unable to create PAL config/environment facade test table.");
        }

        return new OSHostConfigEnvironmentFacade(build.Table);
    }

    private static OSHostConfigQuery Query(byte[] key, byte[] destination)
    {
        return new OSHostConfigQuery(
            new OSHostByteSpan(0x1000, (ulong)key.Length),
            new OSHostByteSpan(0x2000, (ulong)destination.Length));
    }

    private static OSHostEnvironmentEnumerationQuery EnumerationQuery(
        uint index,
        byte[] keyDestination,
        byte[] valueDestination)
    {
        return new OSHostEnvironmentEnumerationQuery(
            index,
            reserved: 0,
            new OSHostByteSpan(0x3000, (ulong)keyDestination.Length),
            new OSHostByteSpan(0x4000, (ulong)valueDestination.Length));
    }

    private static OSHostConfigEnvironmentEntry Entry(string key, string value)
    {
        return new OSHostConfigEnvironmentEntry(Bytes(key), Bytes(value));
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
