namespace OS.Storage;

public struct LiveGptSelection
{
    public LiveGptSelectionStatus Status;
    public LiveGptStatus PrimaryStatus, BackupStatus;
    public bool UsedBackup;
    public LiveGptHeaderResult Header;
    public readonly bool Succeeded => Status == LiveGptSelectionStatus.Success && Header.Succeeded;
}
