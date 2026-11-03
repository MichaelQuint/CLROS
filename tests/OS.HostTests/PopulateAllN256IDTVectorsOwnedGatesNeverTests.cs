using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using OS.Kernel.Diagnostics;

internal static unsafe class PopulateAllN256IDTVectorsOwnedGatesNeverTests
{
    private const string TestName = "m02.011";
    private const ulong ModeledTableAddress = 0x0010_0000;
    private const ulong OwnedStubPageAddress = 0x0020_0000;
    private const ulong HighModeledTableAddress =
        0xffff_8000_0010_0000;
    private const ulong HighOwnedStubPageAddress =
        0xffff_8000_0020_0000;
    private const ulong ExpectedEncodedFnv1A64 = 0x7f50_5c7d_1410_1415UL;
    private const string ExpectedEncodedSha256 =
        "0AD787A760168953C74020131BCAC6CC6FD09B9D68413D487678382846F95C38";
    private const byte Sentinel = 0xa5;
    private const string HostReadyMarker =
        "CLROS_M02_011_HOST_READY vectors=256 gates=256 gateBytes=16 " +
        "tableBytes=4096 selector=8 present=256 dpl0=256 " +
        "interruptGates=256 ownedHandlers=256 firmwareGatesCopied=0 " +
        "nmiIst=3 doubleFaultIst=1 pageFaultIst=2 " +
        "normalizedFnv1a64=8514F42EBDFC5D55 readBack=true " +
        "guestPopulation=false idtrLoaded=false stubExecution=false " +
        "faultTriggered=false postExitBootServices=false";

    [HostFixture(HostLane.Native)]
    internal static void RunAll()
    {
        Console.WriteLine("TEST_START " + TestName);
        int failures = 0;

        RunCase("value and ownership contracts are exact POD", TestValueContracts, ref failures);
        RunCase("all 256 gates match independent x64 descriptors", TestAllGates, ref failures);
        RunCase("canonical high-half handlers retain all 64 bits", TestCanonicalHighHalfHandlers, ref failures);
        RunCase("frozen fingerprints and poison tail are exact", TestFingerprintsAndPoisonTail, ref failures);
        RunCase("IST routing covers exactly NMI DF and PF", TestIstRouting, ref failures);
        RunCase("every table byte mutation is rejected", TestEveryByteMutation, ref failures);
        RunCase("span overloads preserve the pointer contract", TestSpanParity, ref failures);
        RunCase("all range failures are atomic", TestAtomicRangeFailures, ref failures);
        RunCase("pointer paths allocate no managed memory", TestZeroAllocation, ref failures);
        RunCase("exact pointer source compiles without the BCL", TestNoStdLibCompile, ref failures);
        RunCase("source boundary forbids firmware gate copying and live install", TestSourceBoundary, ref failures);

        if (failures != 0)
        {
            Console.WriteLine(
                "TEST_FAIL " + TestName + " host-tests failures=" + failures);
            throw new InvalidOperationException(
                "M02-011 owned IDT gate page host tests failed: " + failures);
        }

        Console.WriteLine(HostReadyMarker);
        Console.WriteLine("TEST_PASS " + TestName);
    }

    private static void RunCase(string name, Func<bool> test, ref int failures)
    {
        HostCheck.Run(TestName + " " + name, () =>
        {
            try
            {
                if (test())
                {
                    return true;
                }
                Console.WriteLine("M02_011_CASE_FAIL " + Normalize(name));
                return false;
            }
            catch (Exception exception)
            {
                Console.WriteLine(
                    "M02_011_CASE_FAIL " + Normalize(name) + " " +
                    exception.GetType().Name + ": " + exception.Message);
                throw;
            }
        }, ref failures);
    }

    private static bool TestValueContracts()
    {
        if (Enum.GetUnderlyingType(typeof(OwnedIdtGateTableOperation)) != typeof(byte)
            || Enum.GetUnderlyingType(typeof(OwnedIdtGateTableStatus)) != typeof(byte)
            || !EnumValuesAreSequential<OwnedIdtGateTableOperation>(3)
            || !EnumValuesAreSequential<OwnedIdtGateTableStatus>(20))
        {
            return false;
        }

        OwnedIdtGateTableResult empty = default;
        return !RuntimeHelpers.IsReferenceOrContainsReferences<OwnedIdtGateTableResult>()
            && PopulateAllN256IDTVectorsOwnedGatesNever.VectorCount == 256
            && PopulateAllN256IDTVectorsOwnedGatesNever.DescriptorByteCount == 16
            && PopulateAllN256IDTVectorsOwnedGatesNever.TableByteCount == 4096
            && PopulateAllN256IDTVectorsOwnedGatesNever.TableAlignment == 4096
            && PopulateAllN256IDTVectorsOwnedGatesNever.StubSlotByteCount == 16
            && PopulateAllN256IDTVectorsOwnedGatesNever.KernelCodeSelector == 8
            && PopulateAllN256IDTVectorsOwnedGatesNever.DefaultIstIndex == 0
            && PopulateAllN256IDTVectorsOwnedGatesNever.DoubleFaultIstIndex == 1
            && PopulateAllN256IDTVectorsOwnedGatesNever.PageFaultIstIndex == 2
            && PopulateAllN256IDTVectorsOwnedGatesNever.NmiIstIndex == 3
            && PopulateAllN256IDTVectorsOwnedGatesNever.InvalidIstIndex == 0xff
            && PopulateAllN256IDTVectorsOwnedGatesNever.PresentInterruptGateAttributes == 0x8e
            && PopulateAllN256IDTVectorsOwnedGatesNever.IstRoutedVectorCount == 3
            && PopulateAllN256IDTVectorsOwnedGatesNever.DefaultIstVectorCount == 253
            && PopulateAllN256IDTVectorsOwnedGatesNever.NormalizedPolicyFnv1A64 ==
                0x8514_f42e_bdfc_5d55UL
            && IdtTablePlan.EntryCount == 256
            && IdtTablePlan.EncodedByteLength == 4096
            && IdtGateDescriptor.EncodedLength == 16
            && IdtrDescriptor.RequiredTableByteLength == 4096
            && IdtrDescriptor.RequiredLimit == 4095
            && NormalizeErrorCodeNonErrorCodeInterruptFrame.VectorCount == 256
            && NormalizeErrorCodeNonErrorCodeInterruptFrame.SlotByteCount == 16
            && NormalizeErrorCodeNonErrorCodeInterruptFrame.PageByteCount == 4096
            && OwnedX64GdtContainingNullN64BitCode.KernelCodeSelector == 8
            && GuardedISTStacksDoubleFaultPageFaultNMI.DoubleFaultIstIndex == 1
            && GuardedISTStacksDoubleFaultPageFaultNMI.PageFaultIstIndex == 2
            && GuardedISTStacksDoubleFaultPageFaultNMI.NmiIstIndex == 3
            && !empty.IsInitialized
            && empty.Operation == OwnedIdtGateTableOperation.None
            && empty.Status == OwnedIdtGateTableStatus.Uninitialized
            && empty.RequiredBytes == 4096
            && empty.DescriptorCount == 0
            && empty.PresentDescriptorCount == 0
            && empty.IstRoutedDescriptorCount == 0
            && empty.DefaultIstDescriptorCount == 0
            && empty.FirmwareGateCopyCount == 0
            && empty.BytesWritten == 0
            && empty.BytesCompared == 0
            && empty.FirstMismatchOffset == -1
            && empty.FirstMismatchVector == -1
            && empty.Fingerprint == 0
            && empty.NormalizedPolicyFingerprint == 0
            && !empty.DestinationMutated
            && !empty.ReadBackPerformed
            && !empty.Materialized
            && !empty.ReadBackValidated
            && !empty.Succeeded;
    }

    private static bool TestAllGates()
    {
        byte* table = AllocateAligned(8192);
        try
        {
            Fill(table, 8192, Sentinel);
            OwnedIdtGateTableResult result =
                PopulateAllN256IDTVectorsOwnedGatesNever.Materialize(
                    table,
                    8192,
                    ModeledTableAddress,
                    OwnedStubPageAddress);
            if (!IsExactMaterializeResult(result, 8192))
            {
                return false;
            }

            Span<byte> expectedBytes = stackalloc byte[16];
            int presentCount = 0;
            int interruptGateCount = 0;
            int ownedHandlerCount = 0;
            int zeroReservedCount = 0;
            for (int vector = 0; vector < 256; vector++)
            {
                byte* descriptor = table + (vector * 16);
                ulong expectedHandler =
                    OwnedStubPageAddress + ((ulong)vector * 16UL);
                byte expectedIst = IndependentIstIndex(vector);

                if (ReadHandler(descriptor) != expectedHandler
                    || ReadU16(descriptor + 2) != 8
                    || descriptor[4] != expectedIst
                    || descriptor[5] != 0x8e
                    || ReadU32(descriptor + 12) != 0)
                {
                    return false;
                }

                IdtGateDescriptorResult oracle = IdtGateDescriptor.Create(
                    vector,
                    expectedHandler,
                    codeSelector: 8,
                    istIndex: expectedIst,
                    gateType: IdtGateType.InterruptGate,
                    descriptorPrivilegeLevel: 0,
                    present: true);
                expectedBytes.Clear();
                if (!oracle.Succeeded
                    || !oracle.Descriptor.TryWrite(expectedBytes)
                    || !new ReadOnlySpan<byte>(descriptor, 16)
                        .SequenceEqual(expectedBytes))
                {
                    return false;
                }

                presentCount += (descriptor[5] & 0x80) != 0 ? 1 : 0;
                interruptGateCount += (descriptor[5] & 0x0f) == 0x0e ? 1 : 0;
                ownedHandlerCount += expectedHandler >= OwnedStubPageAddress &&
                    expectedHandler < OwnedStubPageAddress + 4096
                        ? 1
                        : 0;
                zeroReservedCount += ReadU32(descriptor + 12) == 0 ? 1 : 0;
            }

            return presentCount == 256
                && interruptGateCount == 256
                && ownedHandlerCount == 256
                && zeroReservedCount == 256
                && ReadHandler(table) == OwnedStubPageAddress
                && ReadHandler(table + (128 * 16)) ==
                    OwnedStubPageAddress + 2048
                && ReadHandler(table + (255 * 16)) ==
                    OwnedStubPageAddress + 4080
                && IsAll(table + 4096, 4096, Sentinel);
        }
        finally
        {
            NativeMemory.AlignedFree(table);
        }
    }

    private static bool TestFingerprintsAndPoisonTail()
    {
        byte* table = AllocateAligned(8192);
        try
        {
            Fill(table, 8192, Sentinel);
            OwnedIdtGateTableResult result =
                PopulateAllN256IDTVectorsOwnedGatesNever.Materialize(
                    table,
                    8192,
                    ModeledTableAddress,
                    OwnedStubPageAddress);
            ulong encodedFingerprint = ComputeFingerprint(table, 4096);
            ulong normalizedFingerprint = ComputeNormalizedPolicyFingerprint();
            string sha256 = Convert.ToHexString(
                SHA256.HashData(new ReadOnlySpan<byte>(table, 4096)));

            if (encodedFingerprint != ExpectedEncodedFnv1A64
                || normalizedFingerprint != 0x8514_f42e_bdfc_5d55UL
                || sha256 != ExpectedEncodedSha256)
            {
                Console.WriteLine(
                    "M02_011_FINGERPRINT_MISMATCH encoded=" +
                    encodedFingerprint.ToString("X16") + " normalized=" +
                    normalizedFingerprint.ToString("X16") + " sha256=" +
                    sha256);
                return false;
            }

            return result.Fingerprint == ExpectedEncodedFnv1A64
                && result.NormalizedPolicyFingerprint ==
                    normalizedFingerprint
                && IsAll(table + 4096, 4096, Sentinel);
        }
        finally
        {
            NativeMemory.AlignedFree(table);
        }
    }

    private static bool TestCanonicalHighHalfHandlers()
    {
        byte* table = AllocateAligned(8192);
        try
        {
            Fill(table, 8192, Sentinel);
            OwnedIdtGateTableResult materialized =
                PopulateAllN256IDTVectorsOwnedGatesNever.Materialize(
                    table,
                    8192,
                    HighModeledTableAddress,
                    HighOwnedStubPageAddress);
            if (!materialized.Materialized
                || materialized.ModeledTableAddress != HighModeledTableAddress
                || materialized.OwnedStubPageAddress !=
                    HighOwnedStubPageAddress)
            {
                return false;
            }

            Span<byte> expectedBytes = stackalloc byte[16];
            for (int vector = 0; vector < 256; vector++)
            {
                byte* descriptor = table + (vector * 16);
                ulong expectedHandler = HighOwnedStubPageAddress +
                    ((ulong)vector * 16UL);
                byte expectedIst = IndependentIstIndex(vector);
                if (ReadHandler(descriptor) != expectedHandler
                    || ReadU32(descriptor + 8) !=
                        (uint)(expectedHandler >> 32))
                {
                    return false;
                }

                IdtGateDescriptorResult oracle = IdtGateDescriptor.Create(
                    vector,
                    expectedHandler,
                    codeSelector: 8,
                    istIndex: expectedIst,
                    gateType: IdtGateType.InterruptGate,
                    descriptorPrivilegeLevel: 0,
                    present: true);
                expectedBytes.Clear();
                if (!oracle.Succeeded
                    || !oracle.Descriptor.TryWrite(expectedBytes)
                    || !new ReadOnlySpan<byte>(descriptor, 16)
                        .SequenceEqual(expectedBytes))
                {
                    return false;
                }
            }

            ulong independentFingerprint =
                ComputeIndependentEncodedFingerprint(
                    HighOwnedStubPageAddress);
            OwnedIdtGateTableResult readBack =
                PopulateAllN256IDTVectorsOwnedGatesNever.ValidateReadBack(
                    table,
                    8192,
                    HighModeledTableAddress,
                    HighOwnedStubPageAddress);
            return materialized.Fingerprint == independentFingerprint
                && readBack.Fingerprint == independentFingerprint
                && readBack.ReadBackValidated
                && ReadHandler(table) == HighOwnedStubPageAddress
                && ReadHandler(table + (128 * 16)) ==
                    HighOwnedStubPageAddress + 2048
                && ReadHandler(table + (255 * 16)) ==
                    HighOwnedStubPageAddress + 4080
                && IsAll(table + 4096, 4096, Sentinel);
        }
        finally
        {
            NativeMemory.AlignedFree(table);
        }
    }

    private static bool TestIstRouting()
    {
        int defaultCount = 0;
        int routedCount = 0;
        for (int vector = 0; vector < 256; vector++)
        {
            byte actual = PopulateAllN256IDTVectorsOwnedGatesNever
                .GetIstIndex(vector);
            byte expected = IndependentIstIndex(vector);
            if (actual != expected)
            {
                return false;
            }

            if (actual == 0)
            {
                defaultCount++;
            }
            else
            {
                routedCount++;
            }
        }

        return defaultCount == 253
            && routedCount == 3
            && PopulateAllN256IDTVectorsOwnedGatesNever.GetIstIndex(2) == 3
            && PopulateAllN256IDTVectorsOwnedGatesNever.GetIstIndex(8) == 1
            && PopulateAllN256IDTVectorsOwnedGatesNever.GetIstIndex(14) == 2
            && PopulateAllN256IDTVectorsOwnedGatesNever.GetIstIndex(-1) == 0xff
            && PopulateAllN256IDTVectorsOwnedGatesNever.GetIstIndex(256) == 0xff;
    }

    private static bool TestEveryByteMutation()
    {
        byte* table = AllocateAligned(4096);
        try
        {
            if (!PopulateAllN256IDTVectorsOwnedGatesNever.Materialize(
                    table,
                    4096,
                    ModeledTableAddress,
                    OwnedStubPageAddress).Materialized)
            {
                return false;
            }

            for (int offset = 0; offset < 4096; offset++)
            {
                table[offset] ^= 0xff;
                OwnedIdtGateTableResult rejected =
                    PopulateAllN256IDTVectorsOwnedGatesNever.ValidateReadBack(
                        table,
                        4096,
                        ModeledTableAddress,
                        OwnedStubPageAddress);
                table[offset] ^= 0xff;

                if (rejected.Status != OwnedIdtGateTableStatus.TableByteMismatch
                    || rejected.Operation !=
                        OwnedIdtGateTableOperation.ValidateReadBack
                    || rejected.BytesWritten != 0
                    || rejected.BytesCompared != offset + 1
                    || rejected.FirstMismatchOffset != offset
                    || rejected.FirstMismatchVector != offset / 16
                    || rejected.DescriptorCount != 0
                    || rejected.PresentDescriptorCount != 0
                    || rejected.IstRoutedDescriptorCount != 0
                    || rejected.DefaultIstDescriptorCount != 0
                    || rejected.DestinationMutated
                    || !rejected.ReadBackPerformed
                    || rejected.NormalizedPolicyFingerprint != 0
                    || rejected.Succeeded)
                {
                    return false;
                }
            }

            return PopulateAllN256IDTVectorsOwnedGatesNever.ValidateReadBack(
                table,
                4096,
                ModeledTableAddress,
                OwnedStubPageAddress).ReadBackValidated;
        }
        finally
        {
            NativeMemory.AlignedFree(table);
        }
    }

    private static bool TestSpanParity()
    {
        byte* pointerTable = AllocateAligned(8192);
        byte* spanTable = AllocateAligned(8192);
        try
        {
            Fill(pointerTable, 8192, Sentinel);
            Fill(spanTable, 8192, Sentinel);
            OwnedIdtGateTableResult pointerResult =
                PopulateAllN256IDTVectorsOwnedGatesNever.Materialize(
                    pointerTable,
                    8192,
                    ModeledTableAddress,
                    OwnedStubPageAddress);
            Span<byte> span = new(spanTable, 8192);
            OwnedIdtGateTableResult spanResult =
                PopulateAllN256IDTVectorsOwnedGatesNever.Materialize(
                    span,
                    ModeledTableAddress,
                    OwnedStubPageAddress);
            OwnedIdtGateTableResult pointerReadBack =
                PopulateAllN256IDTVectorsOwnedGatesNever.ValidateReadBack(
                    pointerTable,
                    8192,
                    ModeledTableAddress,
                    OwnedStubPageAddress);
            OwnedIdtGateTableResult spanReadBack =
                PopulateAllN256IDTVectorsOwnedGatesNever.ValidateReadBack(
                    (ReadOnlySpan<byte>)span,
                    ModeledTableAddress,
                    OwnedStubPageAddress);

            return SameResult(pointerResult, spanResult)
                && SameResult(pointerReadBack, spanReadBack)
                && SameBytes(pointerTable, spanTable, 8192)
                && IsAll(pointerTable + 4096, 4096, Sentinel)
                && IsAll(spanTable + 4096, 4096, Sentinel);
        }
        finally
        {
            NativeMemory.AlignedFree(pointerTable);
            NativeMemory.AlignedFree(spanTable);
        }
    }

    private static bool TestAtomicRangeFailures()
    {
        byte* table = AllocateAligned(8192);
        try
        {
            if (!ExpectAtomicFailure(
                    table,
                    4095,
                    ModeledTableAddress,
                    OwnedStubPageAddress,
                    OwnedIdtGateTableStatus.TableCapacityTooSmall,
                    table)
                || !ExpectAtomicFailure(
                    null,
                    4096,
                    ModeledTableAddress,
                    OwnedStubPageAddress,
                    OwnedIdtGateTableStatus.MissingTable,
                    table)
                || !ExpectAtomicFailure(
                    table + 1,
                    4096,
                    ModeledTableAddress,
                    OwnedStubPageAddress,
                    OwnedIdtGateTableStatus.MisalignedTablePointer,
                    table)
                || !ExpectAtomicFailure(
                    (byte*)0x0000_8000_0000_0000UL,
                    4096,
                    ModeledTableAddress,
                    OwnedStubPageAddress,
                    OwnedIdtGateTableStatus.NonCanonicalTablePointer,
                    table)
                || !ExpectAtomicFailure(
                    (byte*)0xffff_ffff_ffff_f000UL,
                    4096,
                    ModeledTableAddress,
                    OwnedStubPageAddress,
                    OwnedIdtGateTableStatus.TablePointerSpanOverflow,
                    table)
                || !ExpectAtomicFailure(
                    (byte*)0x0000_7fff_ffff_f000UL,
                    8192,
                    ModeledTableAddress,
                    OwnedStubPageAddress,
                    OwnedIdtGateTableStatus.NonCanonicalTablePointerSpan,
                    table)
                || !ExpectAtomicFailure(
                    table,
                    4096,
                    0,
                    OwnedStubPageAddress,
                    OwnedIdtGateTableStatus.NullTableAddress,
                    table)
                || !ExpectAtomicFailure(
                    table,
                    4096,
                    ModeledTableAddress + 1,
                    OwnedStubPageAddress,
                    OwnedIdtGateTableStatus.MisalignedTableAddress,
                    table)
                || !ExpectAtomicFailure(
                    table,
                    4096,
                    0x0000_8000_0000_0000UL,
                    OwnedStubPageAddress,
                    OwnedIdtGateTableStatus.NonCanonicalTableAddress,
                    table)
                || !ExpectAtomicFailure(
                    table,
                    4096,
                    0xffff_ffff_ffff_f000UL,
                    OwnedStubPageAddress,
                    OwnedIdtGateTableStatus.TableAddressSpanOverflow,
                    table)
                || !ExpectAtomicFailure(
                    table,
                    4096,
                    ModeledTableAddress,
                    0,
                    OwnedIdtGateTableStatus.NullStubPageAddress,
                    table)
                || !ExpectAtomicFailure(
                    table,
                    4096,
                    ModeledTableAddress,
                    OwnedStubPageAddress + 1,
                    OwnedIdtGateTableStatus.MisalignedStubPageAddress,
                    table)
                || !ExpectAtomicFailure(
                    table,
                    4096,
                    ModeledTableAddress,
                    0x0000_8000_0000_0000UL,
                    OwnedIdtGateTableStatus.NonCanonicalStubPageAddress,
                    table)
                || !ExpectAtomicFailure(
                    table,
                    4096,
                    ModeledTableAddress,
                    0xffff_ffff_ffff_f000UL,
                    OwnedIdtGateTableStatus.StubPageAddressSpanOverflow,
                    table)
                || !ExpectAtomicFailure(
                    table,
                    4096,
                    ModeledTableAddress,
                    ModeledTableAddress,
                    OwnedIdtGateTableStatus.TableOverlapsStubPage,
                    table))
            {
                return false;
            }

            Fill(table, 8192, Sentinel);
            OwnedIdtGateTableResult forwardAdjacent =
                PopulateAllN256IDTVectorsOwnedGatesNever.Materialize(
                    table,
                    8192,
                    ModeledTableAddress,
                    ModeledTableAddress + 4096);
            Fill(table, 8192, Sentinel);
            OwnedIdtGateTableResult reverseAdjacent =
                PopulateAllN256IDTVectorsOwnedGatesNever.Materialize(
                    table,
                    8192,
                    ModeledTableAddress + 4096,
                    ModeledTableAddress);

            return forwardAdjacent.Materialized
                && reverseAdjacent.Materialized
                && IsAll(table + 4096, 4096, Sentinel);
        }
        finally
        {
            NativeMemory.AlignedFree(table);
        }
    }

    private static bool TestZeroAllocation()
    {
        byte* table = AllocateAligned(4096);
        try
        {
            PopulateAllN256IDTVectorsOwnedGatesNever.Materialize(
                table,
                4096,
                ModeledTableAddress,
                OwnedStubPageAddress);
            PopulateAllN256IDTVectorsOwnedGatesNever.ValidateReadBack(
                table,
                4096,
                ModeledTableAddress,
                OwnedStubPageAddress);

            long before = GC.GetAllocatedBytesForCurrentThread();
            OwnedIdtGateTableResult last = default;
            for (int iteration = 0; iteration < 64; iteration++)
            {
                last = PopulateAllN256IDTVectorsOwnedGatesNever.Materialize(
                    table,
                    4096,
                    ModeledTableAddress,
                    OwnedStubPageAddress);
                last = PopulateAllN256IDTVectorsOwnedGatesNever.ValidateReadBack(
                    table,
                    4096,
                    ModeledTableAddress,
                    OwnedStubPageAddress);
            }
            long after = GC.GetAllocatedBytesForCurrentThread();

            return last.ReadBackValidated && after == before;
        }
        finally
        {
            NativeMemory.AlignedFree(table);
        }
    }

    private static bool TestNoStdLibCompile()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string workspace = Path.Combine(
            Path.GetTempPath(),
            "clros-m02-011-nostdlib-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);

        try
        {
            File.WriteAllText(
                Path.Combine(workspace, "NoStdLibShim.cs"),
                NoStdLibShimSource,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            string productionSource = Path.Combine(
                repositoryRoot,
                @"src\OS.Kernel\Diagnostics\PopulateAllN256IDTVectorsOwnedGatesNever.cs");
            string project = NoStdLibProjectSource.Replace(
                "__PRODUCTION_SOURCE__",
                EscapeXml(Path.GetDirectoryName(productionSource)!),
                StringComparison.Ordinal);
            File.WriteAllText(
                Path.Combine(workspace, "M02.011.NoStdLib.csproj"),
                project,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            ProcessStartInfo start = new("dotnet")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = workspace
            };
            start.ArgumentList.Add("build");
            start.ArgumentList.Add("M02.011.NoStdLib.csproj");
            start.ArgumentList.Add("--nologo");
            start.ArgumentList.Add("-m:1");
            start.ArgumentList.Add("--no-dependencies");
            start.ArgumentList.Add("--no-incremental");
            start.ArgumentList.Add("--disable-build-servers");
            start.ArgumentList.Add("-v:minimal");
            start.ArgumentList.Add("-p:RestoreIgnoreFailedSources=true");
            start.ArgumentList.Add("-p:NuGetAudit=false");
            start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
            start.Environment["DOTNET_NOLOGO"] = "1";
            start.Environment["DOTNET_CLI_UI_LANGUAGE"] = "en-US";
            start.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";

            using Process process = Process.Start(start) ??
                throw new InvalidOperationException(
                    "Failed to start focused M02-011 no-stdlib build.");
            Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
            Task<string> standardError = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(120_000))
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
                Console.WriteLine("M02_011_NOSTDLIB_FAIL timeout");
                return false;
            }

            string output = standardOutput.GetAwaiter().GetResult() +
                standardError.GetAwaiter().GetResult();
            if (process.ExitCode != 0)
            {
                Console.WriteLine(
                    "M02_011_NOSTDLIB_FAIL " +
                    NormalizeCompilerOutput(output));
                return false;
            }

            return File.Exists(Path.Combine(
                workspace,
                "out",
                "CLROS.M02.OwnedIdtGates.dll"));
        }
        finally
        {
            if (Directory.Exists(workspace))
            {
                Directory.Delete(workspace, recursive: true);
            }
        }
    }

    private static bool TestSourceBoundary()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(
            repositoryRoot,
            @"src\OS.Kernel\Diagnostics\PopulateAllN256IDTVectorsOwnedGatesNever.cs"));
        string pointerCore = RemoveNoStdLibHostBlocks(source);
        string[] forbidden =
        [
            "throw ",
            "new byte[",
            "stackalloc",
            "delegate*",
            "Marshal.",
            "VirtualProtect",
            "mprotect",
            "EfiEntry",
            "BootSequence",
            "Probes",
            "Console.",
            "DllImport",
            "sidt",
            "lidt",
            "IdtGateDescriptor",
            "FaultVectorCatalog",
            "GuardedISTStacks",
            "NormalizeErrorCodeNonErrorCodeInterruptFrame",
            "handlerAddresses",
            "sourceTable",
            "firmwareTable",
            "MemoryCopy",
            "BufferOperations"
        ];

        for (int index = 0; index < forbidden.Length; index++)
        {
            if (source.Contains(
                    forbidden[index],
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return source.Contains("#if !NOSTDLIB_EFI", StringComparison.Ordinal)
            && source.Contains("Span<byte>", StringComparison.Ordinal)
            && source.Contains("ReadOnlySpan<byte>", StringComparison.Ordinal)
            && !pointerCore.Contains("Span<", StringComparison.Ordinal)
            && source.Contains("FirmwareGateCopyCount => 0", StringComparison.Ordinal)
            && source.Contains(
                "NormalizedPolicyFnv1A64 = 0x8514_f42e_bdfc_5d55UL",
                StringComparison.Ordinal)
            && !source.Contains(HostReadyMarker, StringComparison.Ordinal)
            && !source.Contains("CLROS_M02_011_READY", StringComparison.Ordinal)
            && !source.Contains("TEST_PASS m02.011", StringComparison.Ordinal);
    }

    private static bool ExpectAtomicFailure(
        byte* callPointer,
        ulong capacity,
        ulong modeledTableAddress,
        ulong ownedStubPageAddress,
        OwnedIdtGateTableStatus expectedStatus,
        byte* poisonTable)
    {
        Fill(poisonTable, 8192, Sentinel);
        OwnedIdtGateTableResult result =
            PopulateAllN256IDTVectorsOwnedGatesNever.Materialize(
                callPointer,
                capacity,
                modeledTableAddress,
                ownedStubPageAddress);

        return IsAtomicFailure(result, expectedStatus)
            && IsAll(poisonTable, 8192, Sentinel);
    }

    private static bool IsAtomicFailure(
        OwnedIdtGateTableResult result,
        OwnedIdtGateTableStatus expectedStatus)
    {
        return result.IsInitialized
            && result.Operation == OwnedIdtGateTableOperation.Materialize
            && result.Status == expectedStatus
            && result.RequiredBytes == 4096
            && result.DescriptorCount == 0
            && result.PresentDescriptorCount == 0
            && result.IstRoutedDescriptorCount == 0
            && result.DefaultIstDescriptorCount == 0
            && result.FirmwareGateCopyCount == 0
            && result.BytesWritten == 0
            && result.BytesCompared == 0
            && result.FirstMismatchOffset == -1
            && result.FirstMismatchVector == -1
            && result.Fingerprint == 0
            && result.NormalizedPolicyFingerprint == 0
            && !result.DestinationMutated
            && !result.ReadBackPerformed
            && !result.Succeeded;
    }

    private static bool IsExactMaterializeResult(
        OwnedIdtGateTableResult result,
        ulong expectedCapacity)
    {
        return result.IsInitialized
            && result.Operation == OwnedIdtGateTableOperation.Materialize
            && result.Status == OwnedIdtGateTableStatus.Success
            && result.ModeledTableAddress == ModeledTableAddress
            && result.OwnedStubPageAddress == OwnedStubPageAddress
            && result.TableCapacity == expectedCapacity
            && result.RequiredBytes == 4096
            && result.DescriptorCount == 256
            && result.PresentDescriptorCount == 256
            && result.IstRoutedDescriptorCount == 3
            && result.DefaultIstDescriptorCount == 253
            && result.FirmwareGateCopyCount == 0
            && result.BytesWritten == 4096
            && result.BytesCompared == 4096
            && result.FirstMismatchOffset == -1
            && result.FirstMismatchVector == -1
            && result.Fingerprint == ExpectedEncodedFnv1A64
            && result.NormalizedPolicyFingerprint ==
                0x8514_f42e_bdfc_5d55UL
            && result.DestinationMutated
            && result.ReadBackPerformed
            && result.Materialized
            && !result.ReadBackValidated
            && result.Succeeded;
    }

    private static bool SameResult(
        OwnedIdtGateTableResult first,
        OwnedIdtGateTableResult second)
    {
        return first.IsInitialized == second.IsInitialized
            && first.Operation == second.Operation
            && first.Status == second.Status
            && first.ModeledTableAddress == second.ModeledTableAddress
            && first.OwnedStubPageAddress == second.OwnedStubPageAddress
            && first.TableCapacity == second.TableCapacity
            && first.RequiredBytes == second.RequiredBytes
            && first.DescriptorCount == second.DescriptorCount
            && first.PresentDescriptorCount == second.PresentDescriptorCount
            && first.IstRoutedDescriptorCount == second.IstRoutedDescriptorCount
            && first.DefaultIstDescriptorCount == second.DefaultIstDescriptorCount
            && first.FirmwareGateCopyCount == second.FirmwareGateCopyCount
            && first.BytesWritten == second.BytesWritten
            && first.BytesCompared == second.BytesCompared
            && first.FirstMismatchOffset == second.FirstMismatchOffset
            && first.FirstMismatchVector == second.FirstMismatchVector
            && first.Fingerprint == second.Fingerprint
            && first.NormalizedPolicyFingerprint ==
                second.NormalizedPolicyFingerprint
            && first.DestinationMutated == second.DestinationMutated
            && first.ReadBackPerformed == second.ReadBackPerformed
            && first.Materialized == second.Materialized
            && first.ReadBackValidated == second.ReadBackValidated
            && first.Succeeded == second.Succeeded;
    }

    private static bool EnumValuesAreSequential<T>(int expectedCount)
        where T : struct, Enum
    {
        Array values = Enum.GetValues(typeof(T));
        if (values.Length != expectedCount)
        {
            return false;
        }

        for (int index = 0; index < values.Length; index++)
        {
            if (Convert.ToByte(values.GetValue(index)) != index)
            {
                return false;
            }
        }

        return true;
    }

    private static byte IndependentIstIndex(int vector)
    {
        return vector switch
        {
            2 => 3,
            8 => 1,
            14 => 2,
            _ => 0
        };
    }

    private static ulong ReadHandler(byte* descriptor)
    {
        return ReadU16(descriptor)
            | ((ulong)ReadU16(descriptor + 6) << 16)
            | ((ulong)ReadU32(descriptor + 8) << 32);
    }

    private static ushort ReadU16(byte* source)
    {
        return (ushort)(source[0] | (source[1] << 8));
    }

    private static uint ReadU32(byte* source)
    {
        return (uint)source[0]
            | ((uint)source[1] << 8)
            | ((uint)source[2] << 16)
            | ((uint)source[3] << 24);
    }

    private static ulong ComputeFingerprint(byte* source, int length)
    {
        ulong fingerprint = 14_695_981_039_346_656_037UL;
        for (int offset = 0; offset < length; offset++)
        {
            fingerprint ^= source[offset];
            fingerprint *= 1_099_511_628_211UL;
        }

        return fingerprint;
    }

    private static ulong ComputeNormalizedPolicyFingerprint()
    {
        ulong fingerprint = 14_695_981_039_346_656_037UL;
        for (int offset = 0; offset < 4096; offset++)
        {
            int vector = offset / 16;
            int descriptorOffset = offset - (vector * 16);
            ulong relativeHandler = (ulong)vector * 16UL;
            fingerprint ^= IndependentExpectedByte(
                descriptorOffset,
                vector,
                relativeHandler);
            fingerprint *= 1_099_511_628_211UL;
        }

        return fingerprint;
    }

    private static ulong ComputeIndependentEncodedFingerprint(
        ulong ownedStubPageAddress)
    {
        ulong fingerprint = 14_695_981_039_346_656_037UL;
        for (int offset = 0; offset < 4096; offset++)
        {
            int vector = offset / 16;
            int descriptorOffset = offset - (vector * 16);
            ulong handler = ownedStubPageAddress +
                ((ulong)vector * 16UL);
            fingerprint ^= IndependentExpectedByte(
                descriptorOffset,
                vector,
                handler);
            fingerprint *= 1_099_511_628_211UL;
        }

        return fingerprint;
    }

    private static byte IndependentExpectedByte(
        int descriptorOffset,
        int vector,
        ulong handler)
    {
        return descriptorOffset switch
        {
            0 => (byte)handler,
            1 => (byte)(handler >> 8),
            2 => 8,
            3 => 0,
            4 => IndependentIstIndex(vector),
            5 => 0x8e,
            6 => (byte)(handler >> 16),
            7 => (byte)(handler >> 24),
            8 => (byte)(handler >> 32),
            9 => (byte)(handler >> 40),
            10 => (byte)(handler >> 48),
            11 => (byte)(handler >> 56),
            _ => 0
        };
    }

    private static byte* AllocateAligned(int byteCount)
    {
        void* allocation = NativeMemory.AlignedAlloc(
            (nuint)byteCount,
            4096);
        if (allocation == null)
        {
            throw new OutOfMemoryException(
                "Unable to allocate aligned M02-011 test storage.");
        }

        return (byte*)allocation;
    }

    private static void Fill(byte* destination, int length, byte value)
    {
        for (int offset = 0; offset < length; offset++)
        {
            destination[offset] = value;
        }
    }

    private static bool IsAll(byte* source, int length, byte value)
    {
        for (int offset = 0; offset < length; offset++)
        {
            if (source[offset] != value)
            {
                return false;
            }
        }

        return true;
    }

    private static bool SameBytes(byte* first, byte* second, int length)
    {
        for (int offset = 0; offset < length; offset++)
        {
            if (first[offset] != second[offset])
            {
                return false;
            }
        }

        return true;
    }

    private static string RemoveNoStdLibHostBlocks(string source)
    {
        const string open = "#if !NOSTDLIB_EFI";
        const string close = "#endif";
        while (true)
        {
            int start = source.IndexOf(open, StringComparison.Ordinal);
            if (start < 0)
            {
                return source;
            }

            int end = source.IndexOf(close, start, StringComparison.Ordinal);
            if (end < 0)
            {
                return source;
            }

            source = source.Remove(start, end + close.Length - start);
        }
    }

    private static string EscapeXml(string value)
    {
        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&apos;", StringComparison.Ordinal);
    }

    private static string NormalizeCompilerOutput(string output)
    {
        return output
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
    }

    private static string Normalize(string value)
    {
        return value.Replace(' ', '_').ToUpperInvariant();
    }

    private const string NoStdLibShimSource =
        """
        namespace System
        {
            public class Object { }
            public abstract class ValueType : Object { }
            public abstract class Enum : ValueType { }
            public abstract class Delegate : Object { }
            public abstract class MulticastDelegate : Delegate { }
            public class Attribute : Object { }
            public struct Void { }
            public struct Boolean { }
            public struct Byte { }
            public struct Int32 { }
            public struct UInt16 { }
            public struct UInt64 { }
            public struct UIntPtr { }
            public enum AttributeTargets { }
            public sealed class AttributeUsageAttribute : Attribute
            {
                public AttributeUsageAttribute(AttributeTargets validOn) { }
                public bool AllowMultiple { get; set; }
                public bool Inherited { get; set; }
            }
        }

        namespace System.Runtime.CompilerServices
        {
            public sealed class IsReadOnlyAttribute : System.Attribute { }
        }
        """;

    private const string NoStdLibProjectSource =
        """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <AssemblyName>CLROS.M02.OwnedIdtGates</AssemblyName>
            <TargetFramework>net11.0</TargetFramework>
            <LangVersion>14.0</LangVersion>
            <OutputType>Library</OutputType>
            <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
            <ImplicitUsings>disable</ImplicitUsings>
            <Nullable>disable</Nullable>
            <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
            <NoStdLib>true</NoStdLib>
            <NoConfig>true</NoConfig>
            <DisableImplicitFrameworkReferences>true</DisableImplicitFrameworkReferences>
            <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
            <GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute>
            <ComputeNETCoreBuildOutputFiles>false</ComputeNETCoreBuildOutputFiles>
            <RuntimeMetadataVersion>v4.0.30319</RuntimeMetadataVersion>
            <ProduceReferenceAssembly>false</ProduceReferenceAssembly>
            <EnableNETAnalyzers>false</EnableNETAnalyzers>
            <RunAnalyzersDuringBuild>false</RunAnalyzersDuringBuild>
            <DefineConstants>NOSTDLIB_EFI</DefineConstants>
            <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
            <Deterministic>true</Deterministic>
            <DebugType>none</DebugType>
            <DebugSymbols>false</DebugSymbols>
            <RestoreIgnoreFailedSources>true</RestoreIgnoreFailedSources>
            <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
            <OutputPath>$(MSBuildProjectDirectory)\out\</OutputPath>
          </PropertyGroup>
          <ItemGroup>
            <Compile Include="NoStdLibShim.cs" />
            <Compile Include="__PRODUCTION_SOURCE__/PopulateAllN256IDTVectorsOwnedGatesNever.OwnedIdtGateTableOperation.cs" Link="Diagnostics/PopulateAllN256IDTVectorsOwnedGatesNever.OwnedIdtGateTableOperation.cs" />
                <Compile Include="__PRODUCTION_SOURCE__/PopulateAllN256IDTVectorsOwnedGatesNever.OwnedIdtGateTableStatus.cs" Link="Diagnostics/PopulateAllN256IDTVectorsOwnedGatesNever.OwnedIdtGateTableStatus.cs" />
                <Compile Include="__PRODUCTION_SOURCE__/PopulateAllN256IDTVectorsOwnedGatesNever.OwnedIdtGateTableResult.cs" Link="Diagnostics/PopulateAllN256IDTVectorsOwnedGatesNever.OwnedIdtGateTableResult.cs" />
                <Compile Include="__PRODUCTION_SOURCE__/PopulateAllN256IDTVectorsOwnedGatesNever.cs" Link="Diagnostics/PopulateAllN256IDTVectorsOwnedGatesNever.cs" />
          </ItemGroup>
          <Target Name="CustomizeReferences"
                  BeforeTargets="BeforeCompile"
                  AfterTargets="FindReferenceAssembliesForReferences">
            <ItemGroup>
              <ReferencePathWithRefAssemblies Remove="@(ReferencePathWithRefAssemblies)" />
              <ReferencePath Remove="@(ReferencePath)" />
            </ItemGroup>
          </Target>
        </Project>
        """;
}
