using OS.Std.Binary;

namespace OS.Drivers;

public readonly struct VirtioNetQueueMmioSetupPlan
{
    internal VirtioNetQueueMmioSetupPlan(VirtioNetQueueMmioSetupQueuePlan rxQueue, VirtioNetQueueMmioSetupQueuePlan txQueue, int commandCount)
    {
        RxQueue = rxQueue;
        TxQueue = txQueue;
        CommandCount = commandCount;
    }

    public VirtioNetQueueMmioSetupQueuePlan RxQueue
    {
        get;
    }
    public VirtioNetQueueMmioSetupQueuePlan TxQueue
    {
        get;
    }
    public int CommandCount
    {
        get;
    }
}
