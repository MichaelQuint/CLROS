namespace OS.Drivers;
public readonly struct EnaAdminQueueDmaMemoryPlan
{
    internal EnaAdminQueueDmaMemoryPlan(EnaDmaPhysicalRegion contiguousRegion, EnaDmaPhysicalRegion adminSubmissionQueue, EnaDmaPhysicalRegion adminCompletionQueue, EnaAdminQueueLayoutPlan queueLayout)
    {
        ContiguousRegion = contiguousRegion;
        AdminSubmissionQueue = adminSubmissionQueue;
        AdminCompletionQueue = adminCompletionQueue;
        QueueLayout = queueLayout;
    }

    public EnaDmaPhysicalRegion ContiguousRegion { get; }
    public EnaDmaPhysicalRegion AdminSubmissionQueue { get; }
    public EnaDmaPhysicalRegion AdminCompletionQueue { get; }
    public EnaAdminQueueLayoutPlan QueueLayout { get; }
}
