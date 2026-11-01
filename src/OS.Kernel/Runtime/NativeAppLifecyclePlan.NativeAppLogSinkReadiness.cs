using OS.Boot;
using OS.Manifest;
using OS.Pal;

namespace OS.Kernel.Runtime;
public readonly struct NativeAppLogSinkReadiness
{
    public NativeAppLogSinkReadiness(OSHostStatus status, int remainingRecordCapacity, int maxRecordBytes)
    {
        Status = status;
        RemainingRecordCapacity = remainingRecordCapacity;
        MaxRecordBytes = maxRecordBytes;
        IsInitialized = true;
    }

    public OSHostStatus Status { get; }
    public bool IsInitialized { get; }
    public int RemainingRecordCapacity { get; }
    public int MaxRecordBytes { get; }
    public bool Succeeded => IsInitialized && Status == OSHostStatus.Ok && RemainingRecordCapacity > 0 && MaxRecordBytes >= NativeAppExitStatusFormatter.MaximumMarkerBytes;

    public static NativeAppLogSinkReadiness FromTable(OSHostAppLogTable? table)
    {
        if (table is null)
        {
            return new NativeAppLogSinkReadiness(OSHostStatus.NotFound, remainingRecordCapacity: 0, maxRecordBytes: 0);
        }

        int remainingCapacity = table.Capacity - table.Count;
        if (table.Capacity <= 0 || remainingCapacity <= 0)
        {
            return new NativeAppLogSinkReadiness(OSHostStatus.OutOfMemory, remainingRecordCapacity: remainingCapacity < 0 ? 0 : remainingCapacity, table.MaxRecordBytes);
        }

        if (table.MaxRecordBytes < NativeAppExitStatusFormatter.MaximumMarkerBytes)
        {
            return new NativeAppLogSinkReadiness(OSHostStatus.BufferTooSmall, remainingCapacity, table.MaxRecordBytes);
        }

        return new NativeAppLogSinkReadiness(OSHostStatus.Ok, remainingCapacity, table.MaxRecordBytes);
    }
}
