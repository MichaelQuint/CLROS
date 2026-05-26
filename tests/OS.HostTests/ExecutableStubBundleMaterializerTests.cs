using System.Runtime.CompilerServices;
using OS.Kernel.Diagnostics;
using OS.Kernel.Runtime;

internal static class ExecutableStubBundleMaterializerTests
{
    private const int ExpectedDiagnosticBytesCopied =
        PortIoStubPlan.Out8RequiredByteLength +
        PortIoStubPlan.In8RequiredByteLength +
        DescriptorTableInstallStubPlan.DescriptorStubRequiredByteLength +
        DescriptorTableInstallStubPlan.DescriptorStubRequiredByteLength +
        DescriptorTableInstallStubPlan.LtrStubRequiredByteLength +
        FaultProbeStubPlan.InvalidOpcodeRequiredByteLength +
        FaultProbeStubPlan.NullWriteRequiredByteLength;

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run(
            "executable stub bundle materializer copies planned diagnostic stubs",
            TestCopiesPlannedDiagnosticStubs,
            ref failures);
        Run(
            "executable stub bundle materializer applies payload base offset",
            TestAppliesPayloadBaseOffset,
            ref failures);
        Run(
            "executable stub bundle materializer rejects failed plans",
            TestRejectsFailedPlans,
            ref failures);
        Run(
            "executable stub bundle materializer rejects undersized destination or source",
            TestRejectsUndersizedDestinationOrSource,
            ref failures);
        Run(
            "executable stub bundle materializer default result is not success",
            TestDefaultResultIsNotSuccess,
            ref failures);
        Run(
            "executable stub bundle materializer uses shared buffer operations",
            TestUsesSharedBufferOperations,
            ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Executable stub bundle materializer host tests failed: " + failures);
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

    private static bool TestCopiesPlannedDiagnosticStubs()
    {
        Span<byte> com1Out8 = stackalloc byte[PortIoStubPlan.Out8RequiredByteLength];
        Span<byte> com1In8 = stackalloc byte[PortIoStubPlan.In8RequiredByteLength];
        Span<byte> installIdt = stackalloc byte[DescriptorTableInstallStubPlan.DescriptorStubRequiredByteLength];
        Span<byte> installGdt = stackalloc byte[DescriptorTableInstallStubPlan.DescriptorStubRequiredByteLength];
        Span<byte> loadTaskRegister = stackalloc byte[DescriptorTableInstallStubPlan.LtrStubRequiredByteLength];
        Span<byte> invalidOpcodeProbe = stackalloc byte[FaultProbeStubPlan.InvalidOpcodeRequiredByteLength];
        Span<byte> nullWriteProbe = stackalloc byte[FaultProbeStubPlan.NullWriteRequiredByteLength];

        if (!EmitDiagnosticSources(
            com1Out8,
            com1In8,
            installIdt,
            installGdt,
            loadTaskRegister,
            invalidOpcodeProbe,
            nullWriteProbe))
        {
            return false;
        }

        ExecutableStubMemoryPlanResult memoryPlan = ExecutableStubMemoryPlan.Create(512);
        ExecutableStubBundlePlanResult bundlePlan = CreateDiagnosticBundlePlan(memoryPlan);
        Span<byte> payloadDestination = stackalloc byte[(int)bundlePlan.TotalPayloadLength];
        payloadDestination.Fill(0xcc);

        ExecutableStubBundleMaterializerResult result =
            ExecutableStubBundleMaterializer.MaterializeToPayload(
                bundlePlan,
                new ExecutableStubBundleMaterializerSources(
                    com1Out8,
                    com1In8,
                    installIdt,
                    installGdt,
                    loadTaskRegister,
                    invalidOpcodeProbe,
                    nullWriteProbe),
                payloadDestination);

        return result.Succeeded
            && result.IsInitialized
            && result.Status == ExecutableStubBundleMaterializerStatus.Success
            && result.BytesCopied == (nuint)ExpectedDiagnosticBytesCopied
            && result.Com1Out8BytesCopied == PortIoStubPlan.Out8RequiredByteLength
            && result.Com1In8BytesCopied == PortIoStubPlan.In8RequiredByteLength
            && result.InstallIdtBytesCopied == DescriptorTableInstallStubPlan.DescriptorStubRequiredByteLength
            && result.InstallGdtBytesCopied == DescriptorTableInstallStubPlan.DescriptorStubRequiredByteLength
            && result.LoadTaskRegisterBytesCopied == DescriptorTableInstallStubPlan.LtrStubRequiredByteLength
            && result.InvalidOpcodeProbeBytesCopied == FaultProbeStubPlan.InvalidOpcodeRequiredByteLength
            && result.NullWriteProbeBytesCopied == FaultProbeStubPlan.NullWriteRequiredByteLength
            && payloadDestination.Slice((int)bundlePlan.Com1Out8Offset, com1Out8.Length).SequenceEqual(com1Out8)
            && payloadDestination.Slice((int)bundlePlan.Com1In8Offset, com1In8.Length).SequenceEqual(com1In8)
            && payloadDestination.Slice((int)bundlePlan.InstallIdtOffset, installIdt.Length).SequenceEqual(installIdt)
            && payloadDestination.Slice((int)bundlePlan.InstallGdtOffset, installGdt.Length).SequenceEqual(installGdt)
            && payloadDestination.Slice((int)bundlePlan.LoadTaskRegisterOffset, loadTaskRegister.Length).SequenceEqual(loadTaskRegister)
            && payloadDestination.Slice((int)bundlePlan.InvalidOpcodeProbeOffset, invalidOpcodeProbe.Length).SequenceEqual(invalidOpcodeProbe)
            && payloadDestination.Slice((int)bundlePlan.NullWriteProbeOffset, nullWriteProbe.Length).SequenceEqual(nullWriteProbe)
            && payloadDestination[PortIoStubPlan.Out8RequiredByteLength] == 0xcc;
    }

    private static bool TestAppliesPayloadBaseOffset()
    {
        Span<byte> com1Out8 = stackalloc byte[PortIoStubPlan.Out8RequiredByteLength];
        Span<byte> com1In8 = stackalloc byte[PortIoStubPlan.In8RequiredByteLength];
        Span<byte> installIdt = stackalloc byte[DescriptorTableInstallStubPlan.DescriptorStubRequiredByteLength];
        Span<byte> installGdt = stackalloc byte[DescriptorTableInstallStubPlan.DescriptorStubRequiredByteLength];
        Span<byte> loadTaskRegister = stackalloc byte[DescriptorTableInstallStubPlan.LtrStubRequiredByteLength];
        Span<byte> invalidOpcodeProbe = stackalloc byte[FaultProbeStubPlan.InvalidOpcodeRequiredByteLength];
        Span<byte> nullWriteProbe = stackalloc byte[FaultProbeStubPlan.NullWriteRequiredByteLength];

        if (!EmitDiagnosticSources(
            com1Out8,
            com1In8,
            installIdt,
            installGdt,
            loadTaskRegister,
            invalidOpcodeProbe,
            nullWriteProbe))
        {
            return false;
        }

        ExecutableStubMemoryPlanResult memoryPlan = ExecutableStubMemoryPlan.Create(512);
        ExecutableStubBundlePlanResult bundlePlan = CreateDiagnosticBundlePlan(memoryPlan);
        Span<byte> memoryDestination = stackalloc byte[(int)memoryPlan.RequiredByteLength];
        memoryDestination.Fill(0xcc);

        ExecutableStubBundleMaterializerResult result =
            ExecutableStubBundleMaterializer.MaterializeToMemoryRegion(
                memoryPlan,
                bundlePlan,
                new ExecutableStubBundleMaterializerSources(
                    com1Out8,
                    com1In8,
                    installIdt,
                    installGdt,
                    loadTaskRegister,
                    invalidOpcodeProbe,
                    nullWriteProbe),
                memoryDestination);

        int payloadBaseOffset = (int)memoryPlan.PayloadOffset;
        int com1Out8Offset = payloadBaseOffset + (int)bundlePlan.Com1Out8Offset;
        int nullWriteOffset = payloadBaseOffset + (int)bundlePlan.NullWriteProbeOffset;

        return result.Succeeded
            && memoryDestination.Slice(com1Out8Offset, com1Out8.Length).SequenceEqual(com1Out8)
            && memoryDestination.Slice(nullWriteOffset, nullWriteProbe.Length).SequenceEqual(nullWriteProbe)
            && memoryDestination[0] == 0xcc
            && memoryDestination[payloadBaseOffset - 1] == 0xcc
            && memoryDestination[payloadBaseOffset + PortIoStubPlan.Out8RequiredByteLength] == 0xcc;
    }

    private static bool TestRejectsFailedPlans()
    {
        Span<byte> com1Out8 = stackalloc byte[PortIoStubPlan.Out8RequiredByteLength];
        Span<byte> com1In8 = stackalloc byte[PortIoStubPlan.In8RequiredByteLength];
        Span<byte> installIdt = stackalloc byte[DescriptorTableInstallStubPlan.DescriptorStubRequiredByteLength];
        Span<byte> installGdt = stackalloc byte[DescriptorTableInstallStubPlan.DescriptorStubRequiredByteLength];
        Span<byte> loadTaskRegister = stackalloc byte[DescriptorTableInstallStubPlan.LtrStubRequiredByteLength];
        Span<byte> invalidOpcodeProbe = stackalloc byte[FaultProbeStubPlan.InvalidOpcodeRequiredByteLength];
        Span<byte> nullWriteProbe = stackalloc byte[FaultProbeStubPlan.NullWriteRequiredByteLength];

        if (!EmitDiagnosticSources(
            com1Out8,
            com1In8,
            installIdt,
            installGdt,
            loadTaskRegister,
            invalidOpcodeProbe,
            nullWriteProbe))
        {
            return false;
        }

        ExecutableStubBundleMaterializerSources sources =
            new ExecutableStubBundleMaterializerSources(
                com1Out8,
                com1In8,
                installIdt,
                installGdt,
                loadTaskRegister,
                invalidOpcodeProbe,
                nullWriteProbe);

        Span<ExecutableStubBundleSlot> oneSlot = stackalloc ExecutableStubBundleSlot[]
        {
            new ExecutableStubBundleSlot(ExecutableStubSlotKind.Com1Out8, PortIoStubPlan.Out8RequiredByteLength)
        };

        ExecutableStubMemoryPlanResult memoryPlan = ExecutableStubMemoryPlan.Create(512);
        ExecutableStubBundlePlanResult validBundlePlan = CreateDiagnosticBundlePlan(memoryPlan);
        ExecutableStubBundlePlanResult failedBundlePlan =
            CreateDiagnosticBundlePlan(ExecutableStubMemoryPlan.Create(0));
        ExecutableStubBundlePlanResult missingSlotBundlePlan =
            ExecutableStubBundlePlan.Create(oneSlot, memoryPlan);
        Span<byte> destination = stackalloc byte[(int)memoryPlan.RequiredByteLength];

        ExecutableStubBundleMaterializerResult failedBundleResult =
            ExecutableStubBundleMaterializer.MaterializeToPayload(
                failedBundlePlan,
                sources,
                destination);
        ExecutableStubBundleMaterializerResult failedMemoryResult =
            ExecutableStubBundleMaterializer.MaterializeToMemoryRegion(
                ExecutableStubMemoryPlan.Create(0),
                validBundlePlan,
                sources,
                destination);
        ExecutableStubBundleMaterializerResult missingSlotResult =
            ExecutableStubBundleMaterializer.MaterializeToPayload(
                missingSlotBundlePlan,
                sources,
                destination);

        return !failedBundleResult.Succeeded
            && failedBundleResult.IsInitialized
            && failedBundleResult.Status == ExecutableStubBundleMaterializerStatus.InvalidBundlePlan
            && failedBundleResult.BundlePlanStatus == ExecutableStubBundlePlanStatus.InvalidMemoryPlan
            && failedBundleResult.BytesCopied == 0
            && !failedMemoryResult.Succeeded
            && failedMemoryResult.Status == ExecutableStubBundleMaterializerStatus.InvalidMemoryPlan
            && failedMemoryResult.MemoryPlanStatus == ExecutableStubMemoryPlanStatus.InvalidPayloadLength
            && failedMemoryResult.BytesCopied == 0
            && !missingSlotResult.Succeeded
            && missingSlotResult.Status == ExecutableStubBundleMaterializerStatus.MissingSlot
            && missingSlotResult.BytesCopied == 0;
    }

    private static bool TestRejectsUndersizedDestinationOrSource()
    {
        Span<byte> com1Out8 = stackalloc byte[PortIoStubPlan.Out8RequiredByteLength];
        Span<byte> com1In8 = stackalloc byte[PortIoStubPlan.In8RequiredByteLength];
        Span<byte> installIdt = stackalloc byte[DescriptorTableInstallStubPlan.DescriptorStubRequiredByteLength];
        Span<byte> installGdt = stackalloc byte[DescriptorTableInstallStubPlan.DescriptorStubRequiredByteLength];
        Span<byte> loadTaskRegister = stackalloc byte[DescriptorTableInstallStubPlan.LtrStubRequiredByteLength];
        Span<byte> invalidOpcodeProbe = stackalloc byte[FaultProbeStubPlan.InvalidOpcodeRequiredByteLength];
        Span<byte> nullWriteProbe = stackalloc byte[FaultProbeStubPlan.NullWriteRequiredByteLength];

        if (!EmitDiagnosticSources(
            com1Out8,
            com1In8,
            installIdt,
            installGdt,
            loadTaskRegister,
            invalidOpcodeProbe,
            nullWriteProbe))
        {
            return false;
        }

        ExecutableStubMemoryPlanResult memoryPlan = ExecutableStubMemoryPlan.Create(512);
        ExecutableStubBundlePlanResult bundlePlan = CreateDiagnosticBundlePlan(memoryPlan);
        Span<byte> undersizedDestination = stackalloc byte[(int)bundlePlan.TotalPayloadLength - 1];
        Span<byte> destination = stackalloc byte[(int)bundlePlan.TotalPayloadLength];

        ExecutableStubBundleMaterializerResult undersizedDestinationResult =
            ExecutableStubBundleMaterializer.MaterializeToPayload(
                bundlePlan,
                new ExecutableStubBundleMaterializerSources(
                    com1Out8,
                    com1In8,
                    installIdt,
                    installGdt,
                    loadTaskRegister,
                    invalidOpcodeProbe,
                    nullWriteProbe),
                undersizedDestination);
        ExecutableStubBundleMaterializerResult undersizedSourceResult =
            ExecutableStubBundleMaterializer.MaterializeToPayload(
                bundlePlan,
                new ExecutableStubBundleMaterializerSources(
                    com1Out8.Slice(0, PortIoStubPlan.Out8RequiredByteLength - 1),
                    com1In8,
                    installIdt,
                    installGdt,
                    loadTaskRegister,
                    invalidOpcodeProbe,
                    nullWriteProbe),
                destination);

        return !undersizedDestinationResult.Succeeded
            && undersizedDestinationResult.Status == ExecutableStubBundleMaterializerStatus.DestinationTooSmall
            && undersizedDestinationResult.BytesCopied == 0
            && !undersizedSourceResult.Succeeded
            && undersizedSourceResult.Status == ExecutableStubBundleMaterializerStatus.SourceTooSmall
            && undersizedSourceResult.BytesCopied == 0;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        ExecutableStubBundleMaterializerResult result = default;

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == ExecutableStubBundleMaterializerStatus.Success
            && result.BundlePlanStatus == ExecutableStubBundlePlanStatus.Success
            && result.MemoryPlanStatus == ExecutableStubMemoryPlanStatus.Success
            && result.BytesCopied == 0
            && result.Com1Out8BytesCopied == 0
            && result.InvalidOpcodeProbeBytesCopied == 0;
    }

    private static bool TestUsesSharedBufferOperations()
    {
        string source = File.ReadAllText(Path.Combine(
            HostToolProcess.FindRepositoryRoot(),
            "src",
            "OS.Kernel",
            "Runtime",
            "ExecutableStubBundleMaterializer.cs"));

        return source.Contains("using OS.Std.Memory;", StringComparison.Ordinal)
            && source.Contains("BufferOperations.MemoryCopy(", StringComparison.Ordinal)
            && source.Contains("source.Slice(0, requiredBytes),", StringComparison.Ordinal)
            && source.Contains("destination.Slice(destinationStart, requiredBytes)", StringComparison.Ordinal)
            && !source.Contains(
                "source.Slice(0, requiredBytes).CopyTo(destination.Slice(destinationStart, requiredBytes))",
                StringComparison.Ordinal);
    }

    private static ExecutableStubBundlePlanResult CreateDiagnosticBundlePlan(
        ExecutableStubMemoryPlanResult memoryPlan)
    {
        Span<ExecutableStubBundleSlot> slots = stackalloc ExecutableStubBundleSlot[]
        {
            new ExecutableStubBundleSlot(ExecutableStubSlotKind.Com1Out8, PortIoStubPlan.Out8RequiredByteLength),
            new ExecutableStubBundleSlot(ExecutableStubSlotKind.Com1In8, PortIoStubPlan.In8RequiredByteLength),
            new ExecutableStubBundleSlot(ExecutableStubSlotKind.InstallIdt, DescriptorTableInstallStubPlan.DescriptorStubRequiredByteLength),
            new ExecutableStubBundleSlot(ExecutableStubSlotKind.InstallGdt, DescriptorTableInstallStubPlan.DescriptorStubRequiredByteLength),
            new ExecutableStubBundleSlot(ExecutableStubSlotKind.LoadTaskRegister, DescriptorTableInstallStubPlan.LtrStubRequiredByteLength),
            new ExecutableStubBundleSlot(ExecutableStubSlotKind.InvalidOpcodeProbe, FaultProbeStubPlan.InvalidOpcodeRequiredByteLength),
            new ExecutableStubBundleSlot(ExecutableStubSlotKind.NullWriteProbe, FaultProbeStubPlan.NullWriteRequiredByteLength)
        };

        return ExecutableStubBundlePlan.Create(slots, memoryPlan);
    }

    private static bool EmitDiagnosticSources(
        Span<byte> com1Out8,
        Span<byte> com1In8,
        Span<byte> installIdt,
        Span<byte> installGdt,
        Span<byte> loadTaskRegister,
        Span<byte> invalidOpcodeProbe,
        Span<byte> nullWriteProbe)
    {
        Span<byte> idtrPayload = stackalloc byte[DescriptorTableInstallStubPlan.DescriptorPayloadLength];
        Span<byte> gdtrPayload = stackalloc byte[DescriptorTableInstallStubPlan.DescriptorPayloadLength];
        FillDescriptorPayload(idtrPayload, 0xa0);
        FillDescriptorPayload(gdtrPayload, 0xb0);

        return PortIoStubPlan.Out8.Emit(com1Out8).Succeeded
            && PortIoStubPlan.In8.Emit(com1In8).Succeeded
            && DescriptorTableInstallStubPlan.EmitLidt(idtrPayload, installIdt).Succeeded
            && DescriptorTableInstallStubPlan.EmitLgdt(gdtrPayload, installGdt).Succeeded
            && DescriptorTableInstallStubPlan.EmitLtr(0x28, loadTaskRegister).Succeeded
            && FaultProbeStubPlan.InvalidOpcode.Emit(invalidOpcodeProbe).Succeeded
            && FaultProbeStubPlan.NullWrite.Emit(nullWriteProbe).Succeeded;
    }

    private static void FillDescriptorPayload(Span<byte> destination, byte firstValue)
    {
        for (int i = 0; i < destination.Length; i++)
        {
            destination[i] = (byte)(firstValue + i);
        }
    }
}
