using System.Runtime.CompilerServices;
using OS.Std.Binary;
using OS.Storage;

internal static class StorageVirtioBlockReadTransactionPlanTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("storage.virtioblk read transaction plans sector range", TestPlansSectorRange, ref failures);
        Run("storage.virtioblk read transaction rejects destination too small", TestRejectsDestinationTooSmall, ref failures);
        Run("storage.virtioblk read transaction rejects invalid sector geometry", TestRejectsInvalidSectorGeometry, ref failures);
        Run("storage.virtioblk read transaction rejects queue too small", TestRejectsQueueTooSmall, ref failures);
        Run("storage.virtioblk read transaction default result is not success", TestDefaultResultIsNotSuccess, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Virtio block read transaction-plan host tests failed: " + failures);
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

    private static bool TestPlansSectorRange()
    {
        Span<byte> requestHeader = stackalloc byte[(int)VirtioBlockRequestPlan.RequestHeaderLength];
        byte[] destination = new byte[1024];
        Span<byte> status = stackalloc byte[(int)VirtioBlockRequestPlan.StatusLength];

        VirtioBlockReadTransactionPlanResult result = VirtioBlockReadTransactionPlan.Create(
            sectorSizeBytes: VirtioBlockReadTransactionPlan.RequiredSectorSizeBytes,
            deviceSectorCount: 128,
            firstSector: 5,
            sectorCount: 2,
            queueSize: VirtioBlockRequestPlan.RequiredDescriptorCount,
            requestHeader: requestHeader,
            destination: destination,
            status: status);
        if (!result.Succeeded || result.Status != VirtioBlockReadTransactionPlanStatus.Success)
        {
            return false;
        }

        VirtioBlockReadTransactionPlan plan = result.Plan;
        return plan.SectorSizeBytes == VirtioBlockReadTransactionPlan.RequiredSectorSizeBytes
            && plan.DeviceSectorCount == 128
            && plan.FirstSector == 5
            && plan.SectorCount == 2
            && plan.LastSectorInclusive == 6
            && plan.DataByteCount == 1024
            && plan.QueueSize == VirtioBlockRequestPlan.RequiredDescriptorCount
            && !VirtioBlockReadTransactionPlan.PerformsDmaMapping
            && !VirtioBlockReadTransactionPlan.PerformsMmio
            && !VirtioBlockReadTransactionPlan.PerformsInterrupts
            && !VirtioBlockReadTransactionPlan.PerformsDiskRead
            && plan.RequestPlan.RequestType == VirtioBlockRequestType.Read
            && plan.RequestPlan.DataDescriptor.DeviceWrites
            && plan.RequestPlan.StatusDescriptor.DeviceWrites
            && EndianPrimitives.ReadUInt32LittleEndian(requestHeader.Slice(0, 4)) == (uint)VirtioBlockRequestType.Read
            && EndianPrimitives.ReadUInt32LittleEndian(requestHeader.Slice(4, 4)) == 0
            && EndianPrimitives.ReadUInt64LittleEndian(requestHeader.Slice(8, 8)) == 5;
    }

    private static bool TestRejectsDestinationTooSmall()
    {
        Span<byte> requestHeader = stackalloc byte[(int)VirtioBlockRequestPlan.RequestHeaderLength];
        Fill(requestHeader, 0xA5);
        byte[] destination = new byte[1023];
        Span<byte> status = stackalloc byte[(int)VirtioBlockRequestPlan.StatusLength];

        VirtioBlockReadTransactionPlanResult result = VirtioBlockReadTransactionPlan.Create(
            sectorSizeBytes: VirtioBlockReadTransactionPlan.RequiredSectorSizeBytes,
            deviceSectorCount: 128,
            firstSector: 5,
            sectorCount: 2,
            queueSize: VirtioBlockRequestPlan.RequiredDescriptorCount,
            requestHeader: requestHeader,
            destination: destination,
            status: status);

        return !result.Succeeded
            && result.Status == VirtioBlockReadTransactionPlanStatus.DestinationTooSmall
            && requestHeader[0] == 0xA5;
    }

    private static bool TestRejectsInvalidSectorGeometry()
    {
        Span<byte> requestHeader = stackalloc byte[(int)VirtioBlockRequestPlan.RequestHeaderLength];
        byte[] destination = new byte[512];
        Span<byte> status = stackalloc byte[(int)VirtioBlockRequestPlan.StatusLength];

        VirtioBlockReadTransactionPlanResult invalidSize = VirtioBlockReadTransactionPlan.Create(
            sectorSizeBytes: 4096,
            deviceSectorCount: 128,
            firstSector: 0,
            sectorCount: 1,
            queueSize: VirtioBlockRequestPlan.RequiredDescriptorCount,
            requestHeader: requestHeader,
            destination: destination,
            status: status);
        VirtioBlockReadTransactionPlanResult zeroSectors = VirtioBlockReadTransactionPlan.Create(
            sectorSizeBytes: VirtioBlockReadTransactionPlan.RequiredSectorSizeBytes,
            deviceSectorCount: 0,
            firstSector: 0,
            sectorCount: 1,
            queueSize: VirtioBlockRequestPlan.RequiredDescriptorCount,
            requestHeader: requestHeader,
            destination: destination,
            status: status);

        return !invalidSize.Succeeded
            && invalidSize.Status == VirtioBlockReadTransactionPlanStatus.InvalidSectorGeometry
            && !zeroSectors.Succeeded
            && zeroSectors.Status == VirtioBlockReadTransactionPlanStatus.InvalidSectorGeometry;
    }

    private static bool TestRejectsQueueTooSmall()
    {
        Span<byte> requestHeader = stackalloc byte[(int)VirtioBlockRequestPlan.RequestHeaderLength];
        Fill(requestHeader, 0xA5);
        byte[] destination = new byte[512];
        Span<byte> status = stackalloc byte[(int)VirtioBlockRequestPlan.StatusLength];

        VirtioBlockReadTransactionPlanResult result = VirtioBlockReadTransactionPlan.Create(
            sectorSizeBytes: VirtioBlockReadTransactionPlan.RequiredSectorSizeBytes,
            deviceSectorCount: 128,
            firstSector: 0,
            sectorCount: 1,
            queueSize: VirtioBlockRequestPlan.RequiredDescriptorCount - 1,
            requestHeader: requestHeader,
            destination: destination,
            status: status);

        return !result.Succeeded
            && result.Status == VirtioBlockReadTransactionPlanStatus.QueueTooSmall
            && requestHeader[0] == 0xA5;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        VirtioBlockReadTransactionPlanResult result = default;
        VirtioBlockReadTransactionPlanResult failed =
            VirtioBlockReadTransactionPlanResult.Fail(
                VirtioBlockReadTransactionPlanStatus.InvalidSectorRange);

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioBlockReadTransactionPlanStatus.Success
            && result.RequestPlanStatus == VirtioBlockRequestPlanStatus.Success
            && result.Plan.SectorSizeBytes == 0
            && result.Plan.DeviceSectorCount == 0
            && result.Plan.FirstSector == 0
            && result.Plan.SectorCount == 0
            && result.Plan.DataByteCount == 0
            && result.Plan.QueueSize == 0
            && failed.IsInitialized
            && !failed.Succeeded
            && failed.Status == VirtioBlockReadTransactionPlanStatus.InvalidSectorRange
            && failed.RequestPlanStatus == VirtioBlockRequestPlanStatus.Success
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioBlockReadTransactionPlanResult>();
    }

    private static void Fill(Span<byte> destination, byte value)
    {
        for (int index = 0; index < destination.Length; index++)
        {
            destination[index] = value;
        }
    }
}
