namespace OS.Pal;

public sealed class OSHostRandomFacade
{
    private readonly OSHostRandomTable? table;

    public OSHostRandomFacade(OSHostRandomTable? table)
    {
        this.table = table;
    }

    public OSHostRandomBytesResult GetRandomBytes(
        OSHostRandomBytesRequest request,
        Span<byte> destination)
    {
        if (request.Reserved != 0 ||
            request.Destination.Address == 0 ||
            request.Destination.Length == 0 ||
            request.Destination.Length != (ulong)destination.Length)
        {
            return OSHostRandomBytesResult.FromStatus(OSHostStatus.InvalidArgument);
        }

        if (table is null)
        {
            return OSHostRandomBytesResult.FromStatus(OSHostStatus.InvalidArgument);
        }

        return table.FillRandomBytes(destination, request.Flags);
    }
}
