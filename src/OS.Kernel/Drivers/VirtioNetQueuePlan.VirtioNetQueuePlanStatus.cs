namespace OS.Drivers;

public enum VirtioNetQueuePlanStatus
{
    Success = 0,
    InsufficientQueueCount = 1,
    InvalidRxQueueSize = 2,
    InvalidTxQueueSize = 3,
    InvalidRxBufferCount = 4,
    InvalidFrameBufferSize = 5,
    RxQueueTooSmall = 6,
    TxQueueTooSmall = 7,
    BufferByteCountOverflow = 8
}
