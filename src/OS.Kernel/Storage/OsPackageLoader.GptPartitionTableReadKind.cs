namespace OS.Storage;

public static partial class OsPackageLoader
{
    private enum GptPartitionTableReadKind
    {
        Primary = 0,
        Backup = 1
    }
}
