namespace OS.Storage;

public enum LiveGptSelectionStatus : byte { Uninitialized, Success, NoValidTable }

public struct LiveGptSelection
{
    public LiveGptSelectionStatus Status;
    public LiveGptStatus PrimaryStatus, BackupStatus;
    public bool UsedBackup;
    public LiveGptHeaderResult Header;
    public readonly bool Succeeded => Status == LiveGptSelectionStatus.Success && Header.Succeeded;
}

/// <summary>Selection policy for completed, independently validated live reads.
/// The caller must stop on uncertain transport cleanup before considering a
/// backup transaction. This policy performs no I/O, repair or disk writes.</summary>
public static class ExerciseBackupGPTFallbackLiveDisk
{
    public static bool NeedsBackup(LiveGptHeaderResult primary, LiveGptStatus arrayStatus) =>
        !primary.Succeeded || arrayStatus != LiveGptStatus.Success;

    public static LiveGptSelection Select(LiveGptHeaderResult primary, LiveGptStatus primaryArray,
        LiveGptHeaderResult backup, LiveGptStatus backupArray)
    {
        LiveGptSelection result = default;
        result.PrimaryStatus = primary.Succeeded ? primaryArray : primary.Status;
        if (!NeedsBackup(primary, primaryArray))
        {
            result.Status = LiveGptSelectionStatus.Success;
            result.Header = primary;
            return result;
        }
        result.BackupStatus = backup.Succeeded ? backupArray : backup.Status;
        if (backup.Succeeded && backupArray == LiveGptStatus.Success)
        {
            result.Status = LiveGptSelectionStatus.Success;
            result.UsedBackup = true;
            result.Header = backup;
            return result;
        }
        result.Status = LiveGptSelectionStatus.NoValidTable;
        return result;
    }
}
