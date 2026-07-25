namespace OS.Pal;

public sealed class OSHostAppLogFacade
{
    private readonly OSHostAppLogTable table;

    public OSHostAppLogFacade(OSHostAppLogTable table)
    {
        this.table = table;
    }

    public OSHostAppLogWriteResult WriteLogBytes(
        OSHostWriteLogBytesRequest request,
        ReadOnlySpan<byte> source)
    {
        if (!OSHostAppLogTable.IsValidStream(request.StreamId)
            || request.Reserved != 0
            || request.Buffer.Length != (ulong)source.Length)
        {
            return OSHostAppLogWriteResult.FromStatus(OSHostStatus.InvalidArgument);
        }

        return table.Write(request.StreamId, source, request.Flags);
    }
}
