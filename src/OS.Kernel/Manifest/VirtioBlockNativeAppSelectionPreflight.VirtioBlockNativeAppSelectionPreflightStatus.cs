using OS.Boot;
using OS.Storage;

namespace OS.Manifest;
public enum VirtioBlockNativeAppSelectionPreflightStatus
{
    Success = 0,
    IncompleteReadSession = 1,
    InvalidBlockDevice = 2,
    ScratchPlanFailed = 3,
    SelectionLoadFailed = 4
}
