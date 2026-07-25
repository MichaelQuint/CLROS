using System.Runtime.CompilerServices;
using OS.Kernel.Runtime;

internal static class NativeAppExecutableMemoryPlanTests
{
    private const ulong PayloadOffsetInPackage = 4096;
    private const ulong HeaderLength = 32;
    private const ulong ExecutableLength = 48;
    private const ulong PayloadLength = HeaderLength + ExecutableLength;

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("appmodel executable memory plan aligns payload mapping", TestAlignsPayloadMapping, ref failures);
        Run("appmodel executable memory plan rejects rwx policy", TestRejectsRwxPolicy, ref failures);
        Run("appmodel executable memory plan rejects invalid alignment", TestRejectsInvalidAlignment, ref failures);
        Run("appmodel executable memory plan rejects zero executable content", TestRejectsZeroExecutableContent, ref failures);
        Run("appmodel executable memory plan rejects overflow", TestRejectsOverflow, ref failures);
        Run("appmodel executable memory plan default result is not success", TestDefaultResultIsNotSuccess, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Native app executable memory plan host tests failed: " + failures);
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

    private static bool TestAlignsPayloadMapping()
    {
        NativeAppPayloadMappingPlanResult mappingPlan = CreateMappingPlanResult(
            PayloadOffsetInPackage,
            PayloadLength,
            entryOffsetInExecutable: 12);

        NativeAppExecutableMemoryPlanResult result =
            NativeAppExecutableMemoryPlan.Create(mappingPlan);

        return result.Succeeded
            && result.IsInitialized
            && result.Status == NativeAppExecutableMemoryPlanStatus.Success
            && result.PayloadMappingStatus == NativeAppPayloadMappingPlanStatus.Success
            && result.PageSize == NativeAppExecutableMemoryPlan.DefaultPageSize
            && result.ExecutableAlignment == NativeAppExecutableMemoryPlan.DefaultExecutableAlignment
            && result.RequiredWritableStagingBytes == 4096
            && result.ExecutableBytes == ExecutableLength
            && result.ExecutableMapOffsetInPackage == PayloadOffsetInPackage
            && result.ExecutableMapByteLength == 4096
            && result.ExecutableMapEndOffsetInPackage == PayloadOffsetInPackage + 4096
            && result.ExecutableContentOffsetInMapping == HeaderLength
            && result.EntryOffsetInExecutableContent == 12
            && result.EntryOffsetInExecutableMapping == HeaderLength + 12
            && result.WritableStagingProtection ==
                (NativeAppExecutableMemoryProtection.Read | NativeAppExecutableMemoryProtection.Write)
            && result.ExecutableProtection ==
                (NativeAppExecutableMemoryProtection.Read | NativeAppExecutableMemoryProtection.Execute)
            && result.FinalProtection ==
                (NativeAppExecutableMemoryProtection.Read | NativeAppExecutableMemoryProtection.Execute);
    }

    private static bool TestRejectsRwxPolicy()
    {
        NativeAppExecutableMemoryProtection rwx =
            NativeAppExecutableMemoryProtection.Read |
            NativeAppExecutableMemoryProtection.Write |
            NativeAppExecutableMemoryProtection.Execute;

        NativeAppExecutableMemoryPlanResult rejected = NativeAppExecutableMemoryPlan.Create(
            CreateMappingPlanResult(),
            pageSize: 4096,
            executableAlignment: 16,
            writableStagingProtection: rwx,
            executableProtection: NativeAppExecutableMemoryProtection.Read | NativeAppExecutableMemoryProtection.Execute,
            finalProtection: NativeAppExecutableMemoryProtection.Read | NativeAppExecutableMemoryProtection.Execute);

        return !rejected.Succeeded
            && rejected.IsInitialized
            && rejected.Status == NativeAppExecutableMemoryPlanStatus.AccidentalWritableExecutable
            && rejected.RequiredWritableStagingBytes == 0
            && NativeAppExecutableMemoryPlan.IsWritableExecutable(rwx);
    }

    private static bool TestRejectsInvalidAlignment()
    {
        NativeAppExecutableMemoryPlanResult smallPage = NativeAppExecutableMemoryPlan.Create(
            CreateMappingPlanResult(),
            pageSize: 2048,
            executableAlignment: 16);
        NativeAppExecutableMemoryPlanResult invalidAlignment = NativeAppExecutableMemoryPlan.Create(
            CreateMappingPlanResult(),
            pageSize: 4096,
            executableAlignment: 24);
        NativeAppExecutableMemoryPlanResult largeAlignment = NativeAppExecutableMemoryPlan.Create(
            CreateMappingPlanResult(),
            pageSize: 4096,
            executableAlignment: 8192);
        NativeAppExecutableMemoryPlanResult unalignedExecutable = NativeAppExecutableMemoryPlan.Create(
            CreateMappingPlanResult(payloadOffsetInPackage: 4098),
            pageSize: 4096,
            executableAlignment: 16);

        return !smallPage.Succeeded
            && smallPage.IsInitialized
            && smallPage.Status == NativeAppExecutableMemoryPlanStatus.InvalidPageSize
            && !invalidAlignment.Succeeded
            && invalidAlignment.IsInitialized
            && invalidAlignment.Status == NativeAppExecutableMemoryPlanStatus.InvalidAlignment
            && !largeAlignment.Succeeded
            && largeAlignment.IsInitialized
            && largeAlignment.Status == NativeAppExecutableMemoryPlanStatus.AlignmentGreaterThanPageSize
            && !unalignedExecutable.Succeeded
            && unalignedExecutable.IsInitialized
            && unalignedExecutable.Status == NativeAppExecutableMemoryPlanStatus.UnalignedExecutableOffset;
    }

    private static bool TestRejectsZeroExecutableContent()
    {
        NativeAppPayloadMappingPlan mappingPlan = CreateMappingPlan(
            payloadOffsetInPackage: 4096,
            payloadLength: HeaderLength,
            entryOffsetInExecutable: 0);

        NativeAppExecutableMemoryPlanResult result =
            NativeAppExecutableMemoryPlan.Create(mappingPlan);

        return !result.Succeeded
            && result.IsInitialized
            && result.Status == NativeAppExecutableMemoryPlanStatus.ZeroExecutableContent;
    }

    private static bool TestRejectsOverflow()
    {
        NativeAppPayloadMappingPlan mappingPlan = CreateMappingPlan(
            payloadOffsetInPackage: ulong.MaxValue - 47,
            payloadLength: 96,
            entryOffsetInExecutable: 0);

        NativeAppExecutableMemoryPlanResult result =
            NativeAppExecutableMemoryPlan.Create(mappingPlan);

        return !result.Succeeded
            && result.IsInitialized
            && result.Status == NativeAppExecutableMemoryPlanStatus.RangeOverflow;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        NativeAppExecutableMemoryPlanResult result = default;

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == NativeAppExecutableMemoryPlanStatus.Success
            && result.PayloadMappingStatus == NativeAppPayloadMappingPlanStatus.Success
            && result.PageSize == 0
            && result.ExecutableAlignment == 0
            && result.RequiredWritableStagingBytes == 0
            && result.ExecutableBytes == 0
            && result.ExecutableMapOffsetInPackage == 0
            && result.ExecutableMapByteLength == 0
            && result.ExecutableMapEndOffsetInPackage == 0
            && result.ExecutableContentOffsetInMapping == 0
            && result.EntryOffsetInExecutableContent == 0
            && result.EntryOffsetInExecutableMapping == 0
            && result.WritableStagingProtection == NativeAppExecutableMemoryProtection.None
            && result.ExecutableProtection == NativeAppExecutableMemoryProtection.None
            && result.FinalProtection == NativeAppExecutableMemoryProtection.None
            && !RuntimeHelpers.IsReferenceOrContainsReferences<NativeAppExecutableMemoryPlanResult>();
    }

    private static NativeAppPayloadMappingPlanResult CreateMappingPlanResult(
        ulong payloadOffsetInPackage = PayloadOffsetInPackage,
        ulong payloadLength = PayloadLength,
        ulong entryOffsetInExecutable = 4)
    {
        return NativeAppPayloadMappingPlanResult.Success(
            CreateMappingPlan(
                payloadOffsetInPackage,
                payloadLength,
                entryOffsetInExecutable));
    }

    private static NativeAppPayloadMappingPlan CreateMappingPlan(
        ulong payloadOffsetInPackage,
        ulong payloadLength,
        ulong entryOffsetInExecutable)
    {
        ulong executableOffsetInPackage = payloadOffsetInPackage + HeaderLength;
        ulong entryOffsetInPayload = HeaderLength + entryOffsetInExecutable;
        ulong entryOffsetInPackage = payloadOffsetInPackage + entryOffsetInPayload;
        ulong executableLength = payloadLength - HeaderLength;

        return new NativeAppPayloadMappingPlan(
            payloadOffsetInPackage,
            payloadLength,
            HeaderLength,
            HeaderLength,
            executableOffsetInPackage,
            executableLength,
            entryOffsetInPayload,
            entryOffsetInExecutable,
            entryOffsetInPackage);
    }
}
