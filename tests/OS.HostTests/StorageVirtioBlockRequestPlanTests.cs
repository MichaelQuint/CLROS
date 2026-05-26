using System.Runtime.CompilerServices;
using OS.Std.Binary;
using OS.Storage;

internal static class StorageVirtioBlockRequestPlanTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("storage.virtioblk read request layout", TestReadRequestLayout, ref failures);
        Run("storage.virtioblk readiness accepts read-only queue", TestReadinessAcceptsReadOnlyQueue, ref failures);
        Run("storage.virtioblk readiness rejects missing features", TestReadinessRejectsMissingFeatures, ref failures);
        Run("storage.virtioblk rejects write request in read-only mode", TestRejectsWriteRequestInReadOnlyMode, ref failures);
        Run("storage.virtioblk request default result is not success", TestDefaultRequestResultIsNotSuccess, ref failures);
        Run("storage.virtioblk readiness default result is not success", TestDefaultReadinessResultIsNotSuccess, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Virtio block request-plan host tests failed: " + failures);
        }
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {
        Console.WriteLine("TEST_START " + name);

        try
        {
            if (test())
            {
                Console.WriteLine("TEST_PASS " + name);
                return;
            }

            failures++;
            Console.WriteLine("TEST_FAIL " + name);
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static bool TestReadRequestLayout()
    {
        VirtioBlockRequestPlanResult result = VirtioBlockRequestPlan.Create(
            VirtioBlockRequestType.Read,
            sector: 42,
            sectorCount: 4,
            readOnlyMode: true);
        if (!result.Succeeded || result.Status != VirtioBlockRequestPlanStatus.Success)
        {
            return false;
        }

        VirtioBlockRequestPlan plan = result.Plan;
        Span<byte> header = stackalloc byte[(int)VirtioBlockRequestPlan.RequestHeaderLength];
        VirtioBlockRequestPlanStatus headerStatus = plan.WriteHeader(header);

        return plan.RequestType == VirtioBlockRequestType.Read
            && plan.Sector == 42
            && plan.SectorCount == 4
            && plan.DataByteCount == 2048
            && plan.HeaderDescriptor.Index == VirtioBlockRequestPlan.HeaderDescriptorIndex
            && plan.HeaderDescriptor.Length == VirtioBlockRequestPlan.RequestHeaderLength
            && plan.HeaderDescriptor.DeviceReads
            && !plan.HeaderDescriptor.DeviceWrites
            && plan.HeaderDescriptor.HasNext
            && plan.HeaderDescriptor.NextIndex == VirtioBlockRequestPlan.DataDescriptorIndex
            && plan.HeaderDescriptor.Flags == VirtioBlockDescriptorMetadata.NextFlag
            && plan.DataDescriptor.Index == VirtioBlockRequestPlan.DataDescriptorIndex
            && plan.DataDescriptor.Length == 2048
            && plan.DataDescriptor.DeviceWrites
            && plan.DataDescriptor.HasNext
            && plan.DataDescriptor.NextIndex == VirtioBlockRequestPlan.StatusDescriptorIndex
            && plan.DataDescriptor.Flags == (VirtioBlockDescriptorMetadata.NextFlag | VirtioBlockDescriptorMetadata.DeviceWriteFlag)
            && plan.StatusDescriptor.Index == VirtioBlockRequestPlan.StatusDescriptorIndex
            && plan.StatusDescriptor.Length == VirtioBlockRequestPlan.StatusLength
            && plan.StatusDescriptor.DeviceWrites
            && !plan.StatusDescriptor.HasNext
            && plan.StatusDescriptor.Flags == VirtioBlockDescriptorMetadata.DeviceWriteFlag
            && headerStatus == VirtioBlockRequestPlanStatus.Success
            && EndianPrimitives.ReadUInt32LittleEndian(header.Slice(0, 4)) == (uint)VirtioBlockRequestType.Read
            && EndianPrimitives.ReadUInt32LittleEndian(header.Slice(4, 4)) == 0
            && EndianPrimitives.ReadUInt64LittleEndian(header.Slice(8, 8)) == 42;
    }

    private static bool TestReadinessAcceptsReadOnlyQueue()
    {
        VirtioBlockRequestPlanResult plan = VirtioBlockRequestPlan.Create(
            VirtioBlockRequestType.Read,
            sector: 7,
            sectorCount: 1,
            readOnlyMode: true);

        VirtioBlockQueueReadinessResult readiness = VirtioBlockQueueReadiness.ValidateReadOnlyQueue(
            VirtioBlockFeatureBits.ReadOnly,
            VirtioBlockRequestPlan.RequiredDescriptorCount,
            plan);

        return readiness.Succeeded
            && readiness.Status == VirtioBlockQueueReadinessStatus.Ready
            && readiness.RequestPlanStatus == VirtioBlockRequestPlanStatus.Success
            && readiness.QueueSize == VirtioBlockRequestPlan.RequiredDescriptorCount
            && readiness.RequestPlan.Sector == 7
            && readiness.RequestPlan.DataDescriptor.DeviceWrites;
    }

    private static bool TestReadinessRejectsMissingFeatures()
    {
        VirtioBlockRequestPlanResult plan = VirtioBlockRequestPlan.Create(
            VirtioBlockRequestType.Read,
            sector: 0,
            sectorCount: 1,
            readOnlyMode: true);

        VirtioBlockQueueReadinessResult readiness = VirtioBlockQueueReadiness.ValidateReadOnlyQueue(
            negotiatedFeatures: 0,
            queueSize: VirtioBlockRequestPlan.RequiredDescriptorCount,
            requestPlan: plan);

        return !readiness.Succeeded
            && readiness.Status == VirtioBlockQueueReadinessStatus.MissingReadOnlyFeature
            && readiness.RequestPlanStatus == VirtioBlockRequestPlanStatus.Success;
    }

    private static bool TestRejectsWriteRequestInReadOnlyMode()
    {
        VirtioBlockRequestPlanResult rejectedPlan = VirtioBlockRequestPlan.Create(
            VirtioBlockRequestType.Write,
            sector: 0,
            sectorCount: 1,
            readOnlyMode: true);
        if (rejectedPlan.Succeeded ||
            rejectedPlan.Status != VirtioBlockRequestPlanStatus.WriteRejectedReadOnlyMode)
        {
            return false;
        }

        VirtioBlockRequestPlanResult writePlan = VirtioBlockRequestPlan.Create(
            VirtioBlockRequestType.Write,
            sector: 0,
            sectorCount: 1,
            readOnlyMode: false);
        if (!writePlan.Succeeded ||
            !writePlan.Plan.DataDescriptor.DeviceReads ||
            writePlan.Plan.DataDescriptor.DeviceWrites)
        {
            return false;
        }

        VirtioBlockQueueReadinessResult readiness = VirtioBlockQueueReadiness.ValidateReadOnlyQueue(
            VirtioBlockFeatureBits.ReadOnly,
            VirtioBlockRequestPlan.RequiredDescriptorCount,
            writePlan);

        return !readiness.Succeeded
            && readiness.Status == VirtioBlockQueueReadinessStatus.WriteRejectedReadOnlyMode
            && readiness.RequestPlanStatus == VirtioBlockRequestPlanStatus.WriteRejectedReadOnlyMode;
    }

    private static bool TestDefaultRequestResultIsNotSuccess()
    {
        VirtioBlockRequestPlanResult result = default;
        VirtioBlockRequestPlanResult failed =
            VirtioBlockRequestPlanResult.Fail(VirtioBlockRequestPlanStatus.InvalidSectorCount);

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioBlockRequestPlanStatus.Success
            && result.Plan.Sector == 0
            && result.Plan.SectorCount == 0
            && result.Plan.DataByteCount == 0
            && failed.IsInitialized
            && !failed.Succeeded
            && failed.Status == VirtioBlockRequestPlanStatus.InvalidSectorCount
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioBlockRequestPlanResult>();
    }

    private static bool TestDefaultReadinessResultIsNotSuccess()
    {
        VirtioBlockQueueReadinessResult result = default;
        VirtioBlockQueueReadinessResult failed =
            VirtioBlockQueueReadinessResult.Fail(
                VirtioBlockQueueReadinessStatus.QueueTooSmall,
                VirtioBlockRequestPlanStatus.Success,
                queueSize: 1);

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioBlockQueueReadinessStatus.Ready
            && result.RequestPlanStatus == VirtioBlockRequestPlanStatus.Success
            && result.QueueSize == 0
            && result.RequestPlan.Sector == 0
            && failed.IsInitialized
            && !failed.Succeeded
            && failed.Status == VirtioBlockQueueReadinessStatus.QueueTooSmall
            && failed.RequestPlanStatus == VirtioBlockRequestPlanStatus.Success
            && failed.QueueSize == 1
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioBlockQueueReadinessResult>();
    }
}
