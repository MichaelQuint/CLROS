namespace OS.Drivers;

public enum EnaAdminQueueLayoutPlanStatus
{
    Success = 0,
    InvalidDepth = 1,
    InvalidEntrySize = 2,
    InvalidAlignment = 3,
    InsufficientMemory = 4,
    LayoutOverflow = 5
}
