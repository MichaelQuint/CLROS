namespace OS.Drivers;
using OS.Std.Binary;

public enum EnaAdminOpcode : byte
{
    CreateSubmissionQueue = 1,
    DestroySubmissionQueue = 2,
    CreateCompletionQueue = 3,
    DestroyCompletionQueue = 4,
    GetFeature = 8,
    SetFeature = 9,
    GetStatistics = 11
}
