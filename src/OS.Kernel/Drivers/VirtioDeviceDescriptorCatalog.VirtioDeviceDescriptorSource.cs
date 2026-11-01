namespace OS.Drivers;
using OS.Storage;

public readonly struct VirtioDeviceDescriptorSource
{
    public VirtioDeviceDescriptorSource(int sourceIndex, VirtioPciBindingPlanResult bindingPlan, ulong deviceFeatures, ushort availableQueueCount, bool featuresOkAccepted) : this(sourceIndex, bindingPlan, bindingPlan.Succeeded ? bindingPlan.Binding.DeviceKind : VirtioPciDeviceKind.Unknown, deviceFeatures, availableQueueCount, featuresOkAccepted)
    {
    }

    public VirtioDeviceDescriptorSource(int sourceIndex, VirtioPciBindingPlanResult bindingPlan, VirtioPciDeviceKind deviceKind, ulong deviceFeatures, ushort availableQueueCount, bool featuresOkAccepted)
    {
        SourceIndex = sourceIndex;
        BindingPlan = bindingPlan;
        DeviceKind = deviceKind;
        DeviceFeatures = deviceFeatures;
        AvailableQueueCount = availableQueueCount;
        FeaturesOkAccepted = featuresOkAccepted;
    }

    public int SourceIndex { get; }
    public VirtioPciBindingPlanResult BindingPlan { get; }
    public VirtioPciDeviceKind DeviceKind { get; }
    public ulong DeviceFeatures { get; }
    public ushort AvailableQueueCount { get; }
    public bool FeaturesOkAccepted { get; }
}
