namespace OS.Drivers;
using OS.Storage;

public readonly struct VirtioDeviceDescriptorDerivedBindingSource
{
    public VirtioDeviceDescriptorDerivedBindingSource(int sourceIndex, VirtioPciBindingDerivedBarSizePlanResult derivedBindingPlan, ulong deviceFeatures, ushort availableQueueCount, bool featuresOkAccepted) : this(sourceIndex, derivedBindingPlan, derivedBindingPlan.Succeeded ? derivedBindingPlan.Binding.DeviceKind : VirtioPciDeviceKind.Unknown, deviceFeatures, availableQueueCount, featuresOkAccepted)
    {
    }

    public VirtioDeviceDescriptorDerivedBindingSource(int sourceIndex, VirtioPciBindingDerivedBarSizePlanResult derivedBindingPlan, VirtioPciDeviceKind deviceKind, ulong deviceFeatures, ushort availableQueueCount, bool featuresOkAccepted)
    {
        SourceIndex = sourceIndex;
        DerivedBindingPlan = derivedBindingPlan;
        DeviceKind = deviceKind;
        DeviceFeatures = deviceFeatures;
        AvailableQueueCount = availableQueueCount;
        FeaturesOkAccepted = featuresOkAccepted;
    }

    public int SourceIndex { get; }
    public VirtioPciBindingDerivedBarSizePlanResult DerivedBindingPlan { get; }
    public VirtioPciBindingPlanResult BindingPlan => DerivedBindingPlan.BindingPlan;
    public VirtioPciDeviceKind DeviceKind { get; }
    public ulong DeviceFeatures { get; }
    public ushort AvailableQueueCount { get; }
    public bool FeaturesOkAccepted { get; }
}
