namespace OS.Pal;

public sealed class OSHostConfigEnvironmentFacade
{
    private readonly OSHostConfigEnvironmentTable? table;

    public OSHostConfigEnvironmentFacade(OSHostConfigEnvironmentTable? table)
    {
        this.table = table;
    }

    public OSHostConfigQueryResult GetConfigValue(
        OSHostConfigQuery query,
        ReadOnlySpan<byte> keyBytes,
        Span<byte> destination)
    {
        if (!TryGetTable(out OSHostConfigEnvironmentTable activeTable)
            || !QueryShapeMatches(query, keyBytes, destination))
        {
            return OSHostConfigQueryResult.FromStatus(OSHostStatus.InvalidArgument);
        }

        return activeTable.TryGetConfigValue(keyBytes, destination);
    }

    public OSHostConfigQueryResult GetEnvironmentValue(
        OSHostConfigQuery query,
        ReadOnlySpan<byte> keyBytes,
        Span<byte> destination)
    {
        if (!TryGetTable(out OSHostConfigEnvironmentTable activeTable)
            || !QueryShapeMatches(query, keyBytes, destination))
        {
            return OSHostConfigQueryResult.FromStatus(OSHostStatus.InvalidArgument);
        }

        return activeTable.TryGetEnvironmentValue(keyBytes, destination);
    }

    public OSHostEnvironmentEnumerationResult EnumerateConfig(
        OSHostEnvironmentEnumerationQuery query,
        Span<byte> keyDestination,
        Span<byte> valueDestination)
    {
        if (!TryGetTable(out OSHostConfigEnvironmentTable activeTable)
            || !EnumerationShapeMatches(query, keyDestination, valueDestination))
        {
            return OSHostEnvironmentEnumerationResult.FromStatus(OSHostStatus.InvalidArgument);
        }

        return activeTable.EnumerateConfig(
            query.Index,
            keyDestination,
            valueDestination);
    }

    public OSHostEnvironmentEnumerationResult EnumerateEnvironment(
        OSHostEnvironmentEnumerationQuery query,
        Span<byte> keyDestination,
        Span<byte> valueDestination)
    {
        if (!TryGetTable(out OSHostConfigEnvironmentTable activeTable)
            || !EnumerationShapeMatches(query, keyDestination, valueDestination))
        {
            return OSHostEnvironmentEnumerationResult.FromStatus(OSHostStatus.InvalidArgument);
        }

        return activeTable.EnumerateEnvironment(
            query.Index,
            keyDestination,
            valueDestination);
    }

    private static bool QueryShapeMatches(
        OSHostConfigQuery query,
        ReadOnlySpan<byte> keyBytes,
        Span<byte> destination)
    {
        return query.KeyUtf8.Length == (ulong)keyBytes.Length
            && query.Destination.Length == (ulong)destination.Length;
    }

    private static bool EnumerationShapeMatches(
        OSHostEnvironmentEnumerationQuery query,
        Span<byte> keyDestination,
        Span<byte> valueDestination)
    {
        return query.Reserved == 0
            && query.KeyDestination.Length == (ulong)keyDestination.Length
            && query.ValueDestination.Length == (ulong)valueDestination.Length;
    }

    private bool TryGetTable(out OSHostConfigEnvironmentTable activeTable)
    {
        if (table is null)
        {
            activeTable = null!;
            return false;
        }

        activeTable = table;
        return true;
    }
}
