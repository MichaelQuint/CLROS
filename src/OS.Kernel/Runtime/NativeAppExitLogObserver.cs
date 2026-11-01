using OS.Pal;

namespace OS.Kernel.Runtime;
public static class NativeAppExitLogObserver
{
    public static NativeAppExitLogObserverResult Observe(OSHostAppLogTable? logTable, Span<byte> recordBuffer)
    {
        if (logTable is null || logTable.Capacity <= 0 || logTable.Count < 0 || logTable.Count > logTable.Capacity)
        {
            return NativeAppExitLogObserverResult.InvalidLogTable();
        }

        int recordCount = logTable.Count;
        for (int index = 0; index < recordCount; index++)
        {
            uint recordIndex = (uint)index;
            int recordsScanned = index + 1;
            OSHostStatus entryStatus = logTable.TryGetEntry(recordIndex, out OSHostAppLogTableEntry entry);
            if (entryStatus != OSHostStatus.Ok)
            {
                return NativeAppExitLogObserverResult.FromEntryFailure(recordsScanned, recordIndex, entryStatus);
            }

            if (!IsMachineReadableStdout(entry))
            {
                continue;
            }

            OSHostAppLogReadResult read = logTable.TryCopyRecord(recordIndex, recordBuffer);
            if (read.Status == OSHostStatus.BufferTooSmall)
            {
                return NativeAppExitLogObserverResult.FromReadFailure(NativeAppExitLogObserverStatus.RecordBufferTooSmall, recordsScanned, recordIndex, read);
            }

            if (!read.Succeeded)
            {
                return NativeAppExitLogObserverResult.FromReadFailure(NativeAppExitLogObserverStatus.InvalidRecord, recordsScanned, recordIndex, read);
            }

            NativeAppExitParseResult parse = NativeAppExitStatusFormatter.TryParse(recordBuffer[..(int)read.BytesCopied]);
            if (parse.Succeeded)
            {
                return NativeAppExitLogObserverResult.Success(recordsScanned, recordIndex, read, parse);
            }

            if (parse.Status != NativeAppExitParseStatus.InvalidPrefix)
            {
                return NativeAppExitLogObserverResult.FromMalformedMarker(recordsScanned, recordIndex, read, parse);
            }
        }

        return NativeAppExitLogObserverResult.NoExitMarker(recordCount);
    }

    private static bool IsMachineReadableStdout(OSHostAppLogTableEntry entry)
    {
        return entry.StreamId == OSHostAppLogConstants.StreamStdout && (entry.Flags & OSHostAppLogConstants.FlagMachineReadable) != 0;
    }
}
