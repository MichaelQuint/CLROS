namespace OS.Pal;

using OS.Std.Text;

public enum OSHostExportSignature : int
{
    StatusOnly = 0,
    StatusHandle = 1,
    StatusMemoryRequestHandleOut = 2,
    StatusMemoryRange = 3,
    StatusHandleMemoryQueryOut = 4,
    StatusTimeSnapshotOut = 5,
    StatusWriteLogBytesRequest = 6,
    StatusRandomBytesRequest = 7,
    StatusPackagePathHandleOut = 8,
    StatusHandleReadAtRequest = 9,
    StatusHandleFileInfoOut = 10,
    StatusPackageEnumerateRequest = 11,
    StatusConfigQuery = 12,
    StatusEnvironmentEnumerationQuery = 13,
    StatusSocketCreateOptionsHandleOut = 14,
    StatusSocketRequest = 15,
    StatusSocketIoRequestTransferredOut = 16,
    StatusSocketPollEntries = 17,
    StatusTimerOptionsHandleOut = 18,
    StatusHandleTimerOptions = 19,
    StatusThreadCreateOptionsHandleOut = 20,
    StatusThreadExitCode = 21,
    StatusThreadIdOut = 22,
    StatusWaitOneRequestWaitResultOut = 23,
    StatusWaitManyRequestWaitResultOut = 24,
    StatusEventOptionsHandleOut = 25,
    StatusSemaphoreCreateHandleOut = 26,
    StatusMutexCreateHandleOut = 27,
    StatusMemoryBarrier = 28
}
