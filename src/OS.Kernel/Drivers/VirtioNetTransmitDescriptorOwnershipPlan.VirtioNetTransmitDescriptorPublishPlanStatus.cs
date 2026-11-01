namespace OS.Drivers;
public enum VirtioNetTransmitDescriptorPublishPlanStatus
{
    Success = 0,
    FreeListFailed = 1,
    TransmitPublishFailed = 2,
    OwnershipMarkFailed = 3
}
