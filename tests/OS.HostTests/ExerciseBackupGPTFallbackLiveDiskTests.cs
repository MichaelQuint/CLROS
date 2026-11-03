using OS.Storage;

internal static class ExerciseBackupGPTFallbackLiveDiskTests
{
    private static LiveGptHeaderResult Header(byte[] bytes, bool backup = false) =>
        PrimaryGPTIndependentlyBoundsCRC.ValidateHeader(bytes, GptTestData.Capacity, BlockReadResult.Success(1, 512), backup);

    [HostTest("gpt.fallback retains valid primary without consuming a backup result")]
    private static void PrimaryWins()
    {
        var (header, entries) = GptTestData.Create();
        var primary = Header(header);
        var array = PrimaryGPTIndependentlyBoundsCRC.ValidateArray(primary, entries, out _);
        HostCheck.True(!ExerciseBackupGPTFallbackLiveDisk.NeedsBackup(primary, array));
        var result = ExerciseBackupGPTFallbackLiveDisk.Select(primary, array, default, LiveGptStatus.ArrayCrcMismatch);
        HostCheck.True(result.Succeeded && !result.UsedBackup && result.Header.CurrentLba == 1);
        HostCheck.Equal(LiveGptStatus.Uninitialized, result.BackupStatus);
        HostCheck.Equal(LiveGptStatus.Success, result.PrimaryStatus);
    }

    [HostTest("gpt.fallback requires independently valid backup header and complete array")]
    private static void BackupIsIndependent()
    {
        var (header, entries) = GptTestData.Create();
        header[16] ^= 1;
        var primary = Header(header);
        HostCheck.Equal(LiveGptStatus.HeaderCrcMismatch, primary.Status);
        var (backupBytes, backupEntries) = GptTestData.Create(true);
        var backup = Header(backupBytes, true);
        var array = PrimaryGPTIndependentlyBoundsCRC.ValidateArray(backup, backupEntries, out _);
        var result = ExerciseBackupGPTFallbackLiveDisk.Select(primary, LiveGptStatus.Uninitialized, backup, array);
        HostCheck.True(result.Succeeded && result.UsedBackup && result.Header.CurrentLba == GptTestData.Capacity - 1);
        HostCheck.Equal(LiveGptStatus.HeaderCrcMismatch, result.PrimaryStatus);
        HostCheck.Equal(LiveGptStatus.Success, result.BackupStatus);
        backupEntries[100] ^= 1;
        array = PrimaryGPTIndependentlyBoundsCRC.ValidateArray(backup, backupEntries, out _);
        result = ExerciseBackupGPTFallbackLiveDisk.Select(primary, LiveGptStatus.Uninitialized, backup, array);
        HostCheck.True(!result.Succeeded && !result.UsedBackup && !result.Header.Succeeded);
        HostCheck.Equal(LiveGptStatus.ArrayCrcMismatch, result.BackupStatus);
        backupBytes[16] ^= 1;
        result = ExerciseBackupGPTFallbackLiveDisk.Select(primary, LiveGptStatus.Uninitialized, Header(backupBytes, true), LiveGptStatus.Success);
        HostCheck.True(!result.Succeeded && !result.Header.Succeeded);
    }

    [HostTest("gpt.fallback rejects default receipts and accepts backup after primary array rejection")]
    private static void DefaultAndArrayFailure()
    {
        HostCheck.True(!default(LiveGptSelection).Succeeded);
        HostCheck.True(!ExerciseBackupGPTFallbackLiveDisk.Select(default, LiveGptStatus.Success, default, LiveGptStatus.Success).Succeeded);
        var (primaryBytes, _) = GptTestData.Create();
        var (backupBytes, backupEntries) = GptTestData.Create(true);
        var primary = Header(primaryBytes);
        var backup = Header(backupBytes, true);
        var array = PrimaryGPTIndependentlyBoundsCRC.ValidateArray(backup, backupEntries, out _);
        HostCheck.True(ExerciseBackupGPTFallbackLiveDisk.NeedsBackup(primary, LiveGptStatus.ArrayCrcMismatch));
        var result = ExerciseBackupGPTFallbackLiveDisk.Select(primary, LiveGptStatus.ArrayCrcMismatch, backup, array);
        HostCheck.True(result.Succeeded && result.UsedBackup);
        HostCheck.Equal(LiveGptStatus.ArrayCrcMismatch, result.PrimaryStatus);
    }
}
