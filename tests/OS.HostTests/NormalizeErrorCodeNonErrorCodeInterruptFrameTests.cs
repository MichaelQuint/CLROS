using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using OS.Kernel.Diagnostics;

internal static unsafe class NormalizeErrorCodeNonErrorCodeInterruptFrameTests
{
    private const string TestName = "m02.010";
    private const string HostReadyMarker =
        "CLROS_M02_010_HOST_READY vectors=256 " +
        "hardwareErrorCodeVectors=9 syntheticErrorCodeVectors=247 " +
        "slotBytes=16 pageBytes=4096 normalizedSlots=3 readBack=true " +
        "guestMaterialization=false stubExecution=false idtInstalled=false " +
        "faultTriggered=false";
    private const ulong ModeledPageAddress = 0x0020_0000UL;
    private const ulong CommonTargetAddress = 0x0030_0000UL;
    private const ulong ExpectedFingerprint = 0xE1F6_3351_8434_546EUL;
    private const byte Sentinel = 0xcc;

    [HostFixture(HostLane.Native)]
    internal static void RunAll()
    {
        Console.WriteLine("TEST_START " + TestName);
        int failures = 0;

        RunCase("value contract and defaults are exact POD", TestValueContract, ref failures);
        RunCase("all 256 vector classifications match policy", TestVectorClassification, ref failures);
        RunCase("all 4096 bytes encode exact normalized slots", TestExactPage, ref failures);
        RunCase("rel32 first middle last and limits are exact", TestRelativeJumpBoundaries, ref failures);
        RunCase("every byte mutation reports its first mismatch", TestEveryByteMutation, ref failures);
        RunCase("pointer and span APIs are byte identical", TestPointerAndSpanEquality, ref failures);
        RunCase("range failures reject atomically", TestRangeFailuresAreAtomic, ref failures);
        RunCase("warmed pointer paths allocate zero managed bytes", TestZeroAllocation, ref failures);
        RunCase("exact source compiles with empty UIntPtr shim", TestNoStdLibEfiCompileClosure, ref failures);
        RunCase("source boundary remains host only", TestSourceBoundary, ref failures);

        if (failures != 0)
        {
            Console.WriteLine(
                "TEST_FAIL " + TestName + " host-tests failures=" + failures);
            throw new InvalidOperationException(
                "M02-010 interrupt-frame normalization host tests failed: " + failures);
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
                Console.WriteLine("M02_010_CASE_FAIL " + Normalize(name));
                return false;
            }
            catch (Exception exception)
            {
                Console.WriteLine(
                    "M02_010_CASE_FAIL " + Normalize(name) + " " +
                    exception.GetType().Name + ": " + exception.Message);
                throw;
            }
        }, ref failures);
    }

    private static bool TestValueContract()
    {
        NormalizeInterruptFramePageResult defaultResult = default;
        return Enum.GetUnderlyingType(
                typeof(NormalizeInterruptFramePageOperation)) == typeof(byte)
            && Enum.GetUnderlyingType(
                typeof(NormalizeInterruptFramePageStatus)) == typeof(byte)
            && !RuntimeHelpers
                .IsReferenceOrContainsReferences<NormalizeInterruptFramePageResult>()
            && !defaultResult.IsInitialized
            && defaultResult.Operation == NormalizeInterruptFramePageOperation.None
            && defaultResult.Status == NormalizeInterruptFramePageStatus.Uninitialized
            && defaultResult.RequiredBytes == 4096
            && defaultResult.BytesWritten == 0
            && defaultResult.BytesCompared == 0
            && defaultResult.FirstMismatchOffset == 0
            && defaultResult.HardwareErrorCodeVectorCount == 0
            && defaultResult.SyntheticErrorCodeVectorCount == 0
            && defaultResult.Fingerprint == 0
            && !defaultResult.DestinationMutated
            && !defaultResult.ReadBackPerformed
            && !defaultResult.Materialized
            && !defaultResult.ReadBackValidated
            && !defaultResult.Succeeded
            && NormalizeErrorCodeNonErrorCodeInterruptFrame.VectorCount == 256
            && NormalizeErrorCodeNonErrorCodeInterruptFrame.SlotByteCount == 16
            && NormalizeErrorCodeNonErrorCodeInterruptFrame.PageByteCount == 4096
            && NormalizeErrorCodeNonErrorCodeInterruptFrame.PageAlignment == 4096
            && NormalizeErrorCodeNonErrorCodeInterruptFrame
                .NormalizedSemanticSlotCount == 3
            && NormalizeErrorCodeNonErrorCodeInterruptFrame
                .HardwareErrorCodeVectorMask == 0x4022_7d00U
            && NormalizeErrorCodeNonErrorCodeInterruptFrame
                .HardwareErrorCodeVectorCount == 9
            && NormalizeErrorCodeNonErrorCodeInterruptFrame
                .SyntheticErrorCodeVectorCount == 247;
    }

    private static bool TestVectorClassification()
    {
        int hardwareCount = 0;
        int syntheticCount = 0;
        for (int vector = 0; vector < 256; vector++)
        {
            bool expected = vector == 8 ||
                vector == 10 ||
                vector == 11 ||
                vector == 12 ||
                vector == 13 ||
                vector == 14 ||
                vector == 17 ||
                vector == 21 ||
                vector == 30;
            bool actual = NormalizeErrorCodeNonErrorCodeInterruptFrame
                .HasHardwareErrorCode(vector);
            if (actual != expected)
            {
                return false;
            }

            if (actual)
            {
                hardwareCount++;
            }
            else
            {
                syntheticCount++;
            }

            if (vector < 32 &&
                FaultVectorCatalog.TryGet((byte)vector, out FaultVectorInfo info) &&
                info.PushesErrorCode != actual)
            {
                return false;
            }
        }

        return hardwareCount == 9
            && syntheticCount == 247
            && FaultVectorCatalog.TryGet(
                FaultVectorCatalog.SecurityException,
                out FaultVectorInfo securityException)
            && securityException.Vector == 30
            && securityException.Mnemonic == "#SX"
            && securityException.PushesErrorCode
            && !NormalizeErrorCodeNonErrorCodeInterruptFrame
                .HasHardwareErrorCode(-1)
            && !NormalizeErrorCodeNonErrorCodeInterruptFrame
                .HasHardwareErrorCode(32)
            && !NormalizeErrorCodeNonErrorCodeInterruptFrame
                .HasHardwareErrorCode(64)
            && !NormalizeErrorCodeNonErrorCodeInterruptFrame
                .HasHardwareErrorCode(255)
            && !NormalizeErrorCodeNonErrorCodeInterruptFrame
                .HasHardwareErrorCode(256);
    }

    private static bool TestExactPage()
    {
        const int tailLength = 37;
        byte* page = AllocateAlignedPage(
            NormalizeErrorCodeNonErrorCodeInterruptFrame.PageByteCount +
            tailLength);
        try
        {
            Fill(
                page,
                NormalizeErrorCodeNonErrorCodeInterruptFrame.PageByteCount +
                tailLength,
                Sentinel);
            NormalizeInterruptFramePageResult materialized =
                NormalizeErrorCodeNonErrorCodeInterruptFrame.Materialize(
                    page,
                    NormalizeErrorCodeNonErrorCodeInterruptFrame.PageByteCount +
                        tailLength,
                    ModeledPageAddress,
                    CommonTargetAddress);
            if (!IsExactMaterializeResult(materialized) ||
                materialized.Fingerprint != ExpectedFingerprint ||
                ComputeFingerprint(
                    page,
                    NormalizeErrorCodeNonErrorCodeInterruptFrame.PageByteCount) !=
                    ExpectedFingerprint ||
                !IsAll(
                    page + NormalizeErrorCodeNonErrorCodeInterruptFrame.PageByteCount,
                    tailLength,
                    Sentinel))
            {
                return false;
            }

            int auditedBytes = 0;
            for (int vector = 0; vector < 256; vector++)
            {
                if (!AuditSlot(page, vector, CommonTargetAddress))
                {
                    return false;
                }

                auditedBytes += 16;
            }

            byte* vector128 = page + (128 * 16);
            byte* vector255 = page + (255 * 16);
            return auditedBytes == 4096
                && vector128[4] == 0x68
                && ReadU32(vector128 + 5) == 128U
                && vector255[4] == 0x68
                && ReadU32(vector255 + 5) == 255U
                && NormalizeErrorCodeNonErrorCodeInterruptFrame
                    .ValidateReadBack(
                        page,
                        4096,
                        ModeledPageAddress,
                        CommonTargetAddress)
                    .ReadBackValidated;
        }
        finally
        {
            NativeMemory.AlignedFree(page);
        }
    }

    private static bool TestRelativeJumpBoundaries()
    {
        const ulong highPageAddress = 0x0000_0001_0000_0000UL;
        byte* page = AllocateAlignedPage(4096);
        try
        {
            NormalizeInterruptFramePageResult ordinary =
                NormalizeErrorCodeNonErrorCodeInterruptFrame.Materialize(
                    page,
                    4096,
                    ModeledPageAddress,
                    CommonTargetAddress);
            if (!ordinary.Materialized ||
                DecodeTarget(page, 0, ModeledPageAddress) != CommonTargetAddress ||
                DecodeTarget(page, 128, ModeledPageAddress) != CommonTargetAddress ||
                DecodeTarget(page, 255, ModeledPageAddress) != CommonTargetAddress)
            {
                return false;
            }

            ulong firstNext = GetIndependentNextInstructionAddress(
                highPageAddress,
                0);
            ulong lastNext = GetIndependentNextInstructionAddress(
                highPageAddress,
                255);
            ulong maximumForwardTarget = firstNext + 0x7fff_ffffUL;
            ulong maximumBackwardTarget = lastNext - 0x8000_0000UL;

            NormalizeInterruptFramePageResult forward =
                NormalizeErrorCodeNonErrorCodeInterruptFrame.Materialize(
                    page,
                    4096,
                    highPageAddress,
                    maximumForwardTarget);
            if (!forward.Materialized ||
                DecodeTarget(page, 0, highPageAddress) != maximumForwardTarget ||
                DecodeTarget(page, 128, highPageAddress) != maximumForwardTarget ||
                DecodeTarget(page, 255, highPageAddress) != maximumForwardTarget)
            {
                return false;
            }

            NormalizeInterruptFramePageResult backward =
                NormalizeErrorCodeNonErrorCodeInterruptFrame.Materialize(
                    page,
                    4096,
                    highPageAddress,
                    maximumBackwardTarget);
            if (!backward.Materialized ||
                DecodeTarget(page, 0, highPageAddress) != maximumBackwardTarget ||
                DecodeTarget(page, 128, highPageAddress) != maximumBackwardTarget ||
                DecodeTarget(page, 255, highPageAddress) != maximumBackwardTarget)
            {
                return false;
            }

            Fill(page, 4096, Sentinel);
            NormalizeInterruptFramePageResult forwardOutOfRange =
                NormalizeErrorCodeNonErrorCodeInterruptFrame.Materialize(
                    page,
                    4096,
                    highPageAddress,
                    firstNext + 0x8000_0000UL);
            if (!IsAtomicFailure(
                    forwardOutOfRange,
                    NormalizeInterruptFramePageStatus
                        .CommonTargetOutOfRel32Range) ||
                !IsAll(page, 4096, Sentinel))
            {
                return false;
            }

            NormalizeInterruptFramePageResult backwardOutOfRange =
                NormalizeErrorCodeNonErrorCodeInterruptFrame.Materialize(
                    page,
                    4096,
                    highPageAddress,
                    lastNext - 0x8000_0001UL);
            return IsAtomicFailure(
                    backwardOutOfRange,
                    NormalizeInterruptFramePageStatus
                        .CommonTargetOutOfRel32Range)
                && IsAll(page, 4096, Sentinel);
        }
        finally
        {
            NativeMemory.AlignedFree(page);
        }
    }

    private static bool TestEveryByteMutation()
    {
        byte* page = AllocateAlignedPage(4096);
        try
        {
            NormalizeInterruptFramePageResult materialized =
                NormalizeErrorCodeNonErrorCodeInterruptFrame.Materialize(
                    page,
                    4096,
                    ModeledPageAddress,
                    CommonTargetAddress);
            if (!materialized.Materialized)
            {
                return false;
            }

            for (int offset = 0; offset < 4096; offset++)
            {
                byte original = page[offset];
                page[offset] ^= 0x01;
                NormalizeInterruptFramePageResult mismatch =
                    NormalizeErrorCodeNonErrorCodeInterruptFrame
                        .ValidateReadBack(
                            page,
                            4096,
                            ModeledPageAddress,
                            CommonTargetAddress);
                if (!mismatch.IsInitialized ||
                    mismatch.Operation !=
                        NormalizeInterruptFramePageOperation.ValidateReadBack ||
                    mismatch.Status !=
                        NormalizeInterruptFramePageStatus.PageByteMismatch ||
                    mismatch.BytesWritten != 0 ||
                    mismatch.BytesCompared != offset + 1 ||
                    mismatch.FirstMismatchOffset != offset ||
                    mismatch.Fingerprint == ExpectedFingerprint ||
                    mismatch.DestinationMutated ||
                    !mismatch.ReadBackPerformed ||
                    mismatch.Succeeded)
                {
                    return false;
                }

                page[offset] = original;
            }

            NormalizeInterruptFramePageResult validated =
                NormalizeErrorCodeNonErrorCodeInterruptFrame.ValidateReadBack(
                    page,
                    4096,
                    ModeledPageAddress,
                    CommonTargetAddress);
            return IsExactReadBackResult(validated) &&
                validated.Fingerprint == ExpectedFingerprint;
        }
        finally
        {
            NativeMemory.AlignedFree(page);
        }
    }

    private static bool TestPointerAndSpanEquality()
    {
        const int capacity = 4113;
        byte* pointerPage = AllocateAlignedPage(capacity);
        byte* spanPage = AllocateAlignedPage(capacity);
        try
        {
            Fill(pointerPage, capacity, Sentinel);
            Fill(spanPage, capacity, Sentinel);
            NormalizeInterruptFramePageResult pointerResult =
                NormalizeErrorCodeNonErrorCodeInterruptFrame.Materialize(
                    pointerPage,
                    capacity,
                    ModeledPageAddress,
                    CommonTargetAddress);
            Span<byte> destination = new Span<byte>(spanPage, capacity);
            NormalizeInterruptFramePageResult spanResult =
                NormalizeErrorCodeNonErrorCodeInterruptFrame.Materialize(
                    destination,
                    ModeledPageAddress,
                    CommonTargetAddress);
            if (!SameResult(pointerResult, spanResult) ||
                !SameBytes(pointerPage, spanPage, capacity))
            {
                return false;
            }

            NormalizeInterruptFramePageResult pointerReadBack =
                NormalizeErrorCodeNonErrorCodeInterruptFrame.ValidateReadBack(
                    pointerPage,
                    capacity,
                    ModeledPageAddress,
                    CommonTargetAddress);
            ReadOnlySpan<byte> source = new ReadOnlySpan<byte>(spanPage, capacity);
            NormalizeInterruptFramePageResult spanReadBack =
                NormalizeErrorCodeNonErrorCodeInterruptFrame.ValidateReadBack(
                    source,
                    ModeledPageAddress,
                    CommonTargetAddress);
            return SameResult(pointerReadBack, spanReadBack) &&
                IsExactReadBackResult(pointerReadBack) &&
                IsAll(pointerPage + 4096, capacity - 4096, Sentinel) &&
                IsAll(spanPage + 4096, capacity - 4096, Sentinel);
        }
        finally
        {
            NativeMemory.AlignedFree(spanPage);
            NativeMemory.AlignedFree(pointerPage);
        }
    }

    private static bool TestRangeFailuresAreAtomic()
    {
        const ulong capacity = 8192;
        byte* page = AllocateAlignedPage((int)capacity);
        try
        {
            if (!ExpectAtomicFailure(
                    page,
                    4095,
                    ModeledPageAddress,
                    CommonTargetAddress,
                    NormalizeInterruptFramePageStatus.PageCapacityTooSmall) ||
                !ExpectStatus(
                    null,
                    4096,
                    ModeledPageAddress,
                    CommonTargetAddress,
                    NormalizeInterruptFramePageStatus.MissingPage) ||
                !ExpectAtomicFailure(
                    page + 1,
                    4096,
                    ModeledPageAddress,
                    CommonTargetAddress,
                    NormalizeInterruptFramePageStatus.MisalignedPagePointer) ||
                !ExpectStatus(
                    (byte*)0x0000_8000_0000_0000UL,
                    4096,
                    ModeledPageAddress,
                    CommonTargetAddress,
                    NormalizeInterruptFramePageStatus.NonCanonicalPagePointer) ||
                !ExpectStatus(
                    (byte*)0xffff_ffff_ffff_f000UL,
                    capacity,
                    ModeledPageAddress,
                    CommonTargetAddress,
                    NormalizeInterruptFramePageStatus.PagePointerSpanOverflow) ||
                !ExpectStatus(
                    (byte*)0x0000_7fff_ffff_f000UL,
                    capacity,
                    ModeledPageAddress,
                    CommonTargetAddress,
                    NormalizeInterruptFramePageStatus
                        .NonCanonicalPagePointerSpan))
            {
                return false;
            }

            (ulong ModeledAddress, ulong Target, NormalizeInterruptFramePageStatus Status)[]
                modeledFailures =
                [
                    (0, CommonTargetAddress,
                        NormalizeInterruptFramePageStatus.NullPageAddress),
                    (ModeledPageAddress + 1, CommonTargetAddress,
                        NormalizeInterruptFramePageStatus.MisalignedPageAddress),
                    (0x0000_8000_0000_0000UL, CommonTargetAddress,
                        NormalizeInterruptFramePageStatus.NonCanonicalPageAddress),
                    (0xffff_ffff_ffff_f000UL, CommonTargetAddress,
                        NormalizeInterruptFramePageStatus.PageAddressSpanOverflow),
                    (0x0000_7fff_ffff_f000UL, CommonTargetAddress,
                        NormalizeInterruptFramePageStatus
                            .NonCanonicalPageAddressSpan),
                    (ModeledPageAddress, 0,
                        NormalizeInterruptFramePageStatus.NullCommonTargetAddress),
                    (ModeledPageAddress, 0x0000_8000_0000_0000UL,
                        NormalizeInterruptFramePageStatus
                            .NonCanonicalCommonTargetAddress),
                    (ModeledPageAddress, ModeledPageAddress,
                        NormalizeInterruptFramePageStatus.CommonTargetOverlapsPage),
                    (ModeledPageAddress, ModeledPageAddress + 2048,
                        NormalizeInterruptFramePageStatus.CommonTargetOverlapsPage),
                    (ModeledPageAddress, ModeledPageAddress + 4095,
                        NormalizeInterruptFramePageStatus.CommonTargetOverlapsPage),
                    (ModeledPageAddress,
                        GetIndependentNextInstructionAddress(ModeledPageAddress, 0) +
                            0x8000_0000UL,
                        NormalizeInterruptFramePageStatus
                            .CommonTargetOutOfRel32Range)
                ];

            for (int index = 0; index < modeledFailures.Length; index++)
            {
                var failure = modeledFailures[index];
                if (!ExpectAtomicFailure(
                        page,
                        capacity,
                        failure.ModeledAddress,
                        failure.Target,
                        failure.Status))
                {
                    return false;
                }
            }

            Fill(page, (int)capacity, Sentinel);
            NormalizeInterruptFramePageResult adjacentTarget =
                NormalizeErrorCodeNonErrorCodeInterruptFrame.Materialize(
                    page,
                    capacity,
                    ModeledPageAddress,
                    ModeledPageAddress + 4096);
            if (!adjacentTarget.Materialized ||
                !IsAll(page + 4096, 4096, Sentinel))
            {
                return false;
            }

            Fill(page, (int)capacity, Sentinel);
            NormalizeInterruptFramePageResult beforePageTarget =
                NormalizeErrorCodeNonErrorCodeInterruptFrame.Materialize(
                    page,
                    capacity,
                    ModeledPageAddress,
                    ModeledPageAddress - 1);
            return beforePageTarget.Materialized &&
                IsAll(page + 4096, 4096, Sentinel);
        }
        finally
        {
            NativeMemory.AlignedFree(page);
        }
    }

    private static bool TestZeroAllocation()
    {
        byte* page = AllocateAlignedPage(4096);
        try
        {
            RunPointerPath(page);
            long before = GC.GetAllocatedBytesForCurrentThread();
            NormalizeInterruptFramePageResult result = default;
            for (int iteration = 0; iteration < 64; iteration++)
            {
                result = RunPointerPath(page);
            }

            long after = GC.GetAllocatedBytesForCurrentThread();
            return result.ReadBackValidated && after == before;
        }
        finally
        {
            NativeMemory.AlignedFree(page);
        }
    }

    private static bool TestNoStdLibEfiCompileClosure()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string workspace = Path.Combine(
            Path.GetTempPath(),
            "clros-m02-010-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);
        try
        {
            File.Copy(
                Path.Combine(repositoryRoot, "global.json"),
                Path.Combine(workspace, "global.json"));
            File.WriteAllText(
                Path.Combine(workspace, "NoStdLibShim.cs"),
                NoStdLibShimSource,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            string productionSource = Path.Combine(
                repositoryRoot,
                @"src\OS.Kernel\Diagnostics\NormalizeErrorCodeNonErrorCodeInterruptFrame.cs");
            string project = NoStdLibProjectSource.Replace(
                "__PRODUCTION_SOURCE__",
                EscapeXml(productionSource),
                StringComparison.Ordinal);
            File.WriteAllText(
                Path.Combine(workspace, "M02.010.NoStdLib.csproj"),
                project,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            ProcessStartInfo start = new ProcessStartInfo("dotnet")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = workspace
            };
            start.ArgumentList.Add("build");
            start.ArgumentList.Add("M02.010.NoStdLib.csproj");
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
                    "Failed to start focused M02-010 no-stdlib build.");
            var standardOutput = process.StandardOutput.ReadToEndAsync();
            var standardError = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(120_000))
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
                Console.WriteLine("M02_010_NOSTDLIB_FAIL timeout");
                return false;
            }

            string output = standardOutput.GetAwaiter().GetResult() +
                standardError.GetAwaiter().GetResult();
            if (process.ExitCode != 0)
            {
                Console.WriteLine(
                    "M02_010_NOSTDLIB_FAIL " + NormalizeCompilerOutput(output));
                return false;
            }

            return File.Exists(Path.Combine(
                workspace,
                "out",
                "CLROS.M02.InterruptFrameNormalization.dll"));
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
            @"src\OS.Kernel\Diagnostics\NormalizeErrorCodeNonErrorCodeInterruptFrame.cs"));
        string pointerCore = RemoveNoStdLibHostBlocks(source);
        int guardIndex = source.IndexOf("vector >= 32", StringComparison.Ordinal);
        int shiftIndex = source.IndexOf("1U << vector", StringComparison.Ordinal);
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
            "FaultVectorCatalog"
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
            && source.Contains(
                "HardwareErrorCodeVectorMask = 0x4022_7d00U",
                StringComparison.Ordinal)
            && guardIndex >= 0
            && shiftIndex > guardIndex
            && !source.Contains(HostReadyMarker, StringComparison.Ordinal)
            && !source.Contains("CLROS_M02_010_READY", StringComparison.Ordinal)
            && !source.Contains("TEST_PASS m02.010", StringComparison.Ordinal);
    }

    private static NormalizeInterruptFramePageResult RunPointerPath(byte* page)
    {
        NormalizeErrorCodeNonErrorCodeInterruptFrame.Materialize(
            page,
            4096,
            ModeledPageAddress,
            CommonTargetAddress);
        return NormalizeErrorCodeNonErrorCodeInterruptFrame.ValidateReadBack(
            page,
            4096,
            ModeledPageAddress,
            CommonTargetAddress);
    }

    private static bool AuditSlot(
        byte* page,
        int vector,
        ulong expectedTarget)
    {
        byte* slot = page + (vector * 16);
        bool hardware = vector == 8 ||
            vector == 10 ||
            vector == 11 ||
            vector == 12 ||
            vector == 13 ||
            vector == 14 ||
            vector == 17 ||
            vector == 21 ||
            vector == 30;
        if (hardware)
        {
            return slot[0] == 0x6a
                && slot[1] == 1
                && slot[2] == 0x68
                && ReadU32(slot + 3) == (uint)vector
                && slot[7] == 0xe9
                && DecodeTarget(page, vector, ModeledPageAddress) == expectedTarget
                && slot[12] == 0x90
                && slot[13] == 0x90
                && slot[14] == 0x90
                && slot[15] == 0x90;
        }

        return slot[0] == 0x6a
            && slot[1] == 0
            && slot[2] == 0x6a
            && slot[3] == 0
            && slot[4] == 0x68
            && ReadU32(slot + 5) == (uint)vector
            && slot[9] == 0xe9
            && DecodeTarget(page, vector, ModeledPageAddress) == expectedTarget
            && slot[14] == 0x90
            && slot[15] == 0x90;
    }

    private static ulong DecodeTarget(
        byte* page,
        int vector,
        ulong modeledPageAddress)
    {
        bool hardware = NormalizeErrorCodeNonErrorCodeInterruptFrame
            .HasHardwareErrorCode(vector);
        int jumpOffset = hardware ? 7 : 9;
        byte* jump = page + (vector * 16) + jumpOffset;
        int displacement = unchecked((int)ReadU32(jump + 1));
        ulong nextAddress = modeledPageAddress +
            ((ulong)vector * 16UL) +
            (ulong)jumpOffset +
            5UL;
        return displacement >= 0
            ? nextAddress + (uint)displacement
            : nextAddress - (uint)(-(long)displacement);
    }

    private static ulong GetIndependentNextInstructionAddress(
        ulong pageAddress,
        int vector)
    {
        bool hardware = vector == 8 ||
            vector == 10 ||
            vector == 11 ||
            vector == 12 ||
            vector == 13 ||
            vector == 14 ||
            vector == 17 ||
            vector == 21 ||
            vector == 30;
        int jumpOffset = hardware ? 7 : 9;
        return pageAddress + ((ulong)vector * 16UL) +
            (ulong)jumpOffset + 5UL;
    }

    private static bool ExpectAtomicFailure(
        byte* page,
        ulong capacity,
        ulong modeledPageAddress,
        ulong commonTargetAddress,
        NormalizeInterruptFramePageStatus expectedStatus)
    {
        int fillLength = capacity > 8192UL ? 8192 : (int)capacity;
        if (fillLength > 0)
        {
            Fill(page, fillLength, Sentinel);
        }

        NormalizeInterruptFramePageResult result =
            NormalizeErrorCodeNonErrorCodeInterruptFrame.Materialize(
                page,
                capacity,
                modeledPageAddress,
                commonTargetAddress);
        return IsAtomicFailure(result, expectedStatus)
            && (fillLength == 0 || IsAll(page, fillLength, Sentinel));
    }

    private static bool ExpectStatus(
        byte* page,
        ulong capacity,
        ulong modeledPageAddress,
        ulong commonTargetAddress,
        NormalizeInterruptFramePageStatus expectedStatus)
    {
        NormalizeInterruptFramePageResult result =
            NormalizeErrorCodeNonErrorCodeInterruptFrame.Materialize(
                page,
                capacity,
                modeledPageAddress,
                commonTargetAddress);
        return IsAtomicFailure(result, expectedStatus);
    }

    private static bool IsAtomicFailure(
        NormalizeInterruptFramePageResult result,
        NormalizeInterruptFramePageStatus expectedStatus)
    {
        return result.IsInitialized
            && result.Operation ==
                NormalizeInterruptFramePageOperation.Materialize
            && result.Status == expectedStatus
            && result.RequiredBytes == 4096
            && result.BytesWritten == 0
            && result.BytesCompared == 0
            && result.FirstMismatchOffset == -1
            && result.HardwareErrorCodeVectorCount == 9
            && result.SyntheticErrorCodeVectorCount == 247
            && result.Fingerprint == 0
            && !result.DestinationMutated
            && !result.ReadBackPerformed
            && !result.Succeeded;
    }

    private static bool IsExactMaterializeResult(
        NormalizeInterruptFramePageResult result)
    {
        return result.IsInitialized
            && result.Operation ==
                NormalizeInterruptFramePageOperation.Materialize
            && result.Status == NormalizeInterruptFramePageStatus.Success
            && result.ModeledPageAddress == ModeledPageAddress
            && result.CommonTargetAddress == CommonTargetAddress
            && result.PageCapacity >= 4096
            && result.RequiredBytes == 4096
            && result.BytesWritten == 4096
            && result.BytesCompared == 4096
            && result.FirstMismatchOffset == -1
            && result.HardwareErrorCodeVectorCount == 9
            && result.SyntheticErrorCodeVectorCount == 247
            && result.DestinationMutated
            && result.ReadBackPerformed
            && result.Materialized
            && !result.ReadBackValidated
            && result.Succeeded;
    }

    private static bool IsExactReadBackResult(
        NormalizeInterruptFramePageResult result)
    {
        return result.IsInitialized
            && result.Operation ==
                NormalizeInterruptFramePageOperation.ValidateReadBack
            && result.Status == NormalizeInterruptFramePageStatus.Success
            && result.ModeledPageAddress == ModeledPageAddress
            && result.CommonTargetAddress == CommonTargetAddress
            && result.PageCapacity >= 4096
            && result.RequiredBytes == 4096
            && result.BytesWritten == 0
            && result.BytesCompared == 4096
            && result.FirstMismatchOffset == -1
            && result.HardwareErrorCodeVectorCount == 9
            && result.SyntheticErrorCodeVectorCount == 247
            && !result.DestinationMutated
            && result.ReadBackPerformed
            && !result.Materialized
            && result.ReadBackValidated
            && result.Succeeded;
    }

    private static bool SameResult(
        NormalizeInterruptFramePageResult first,
        NormalizeInterruptFramePageResult second)
    {
        return first.IsInitialized == second.IsInitialized
            && first.Operation == second.Operation
            && first.Status == second.Status
            && first.ModeledPageAddress == second.ModeledPageAddress
            && first.CommonTargetAddress == second.CommonTargetAddress
            && first.PageCapacity == second.PageCapacity
            && first.RequiredBytes == second.RequiredBytes
            && first.BytesWritten == second.BytesWritten
            && first.BytesCompared == second.BytesCompared
            && first.FirstMismatchOffset == second.FirstMismatchOffset
            && first.HardwareErrorCodeVectorCount ==
                second.HardwareErrorCodeVectorCount
            && first.SyntheticErrorCodeVectorCount ==
                second.SyntheticErrorCodeVectorCount
            && first.Fingerprint == second.Fingerprint
            && first.DestinationMutated == second.DestinationMutated
            && first.ReadBackPerformed == second.ReadBackPerformed
            && first.Materialized == second.Materialized
            && first.ReadBackValidated == second.ReadBackValidated
            && first.Succeeded == second.Succeeded;
    }

    private static byte* AllocateAlignedPage(int byteCount)
    {
        void* allocation = NativeMemory.AlignedAlloc(
            (nuint)byteCount,
            (nuint)NormalizeErrorCodeNonErrorCodeInterruptFrame.PageAlignment);
        if (allocation == null)
        {
            throw new OutOfMemoryException(
                "Unable to allocate aligned M02-010 test page.");
        }

        return (byte*)allocation;
    }

    private static uint ReadU32(byte* source)
    {
        return (uint)source[0] |
            ((uint)source[1] << 8) |
            ((uint)source[2] << 16) |
            ((uint)source[3] << 24);
    }

    private static ulong ComputeFingerprint(byte* source, int length)
    {
        ulong fingerprint =
            NormalizeErrorCodeNonErrorCodeInterruptFrame.Fnv1A64OffsetBasis;
        for (int offset = 0; offset < length; offset++)
        {
            fingerprint ^= source[offset];
            fingerprint *=
                NormalizeErrorCodeNonErrorCodeInterruptFrame.Fnv1A64Prime;
        }

        return fingerprint;
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
            public struct UInt32 { }
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
            <AssemblyName>CLROS.M02.InterruptFrameNormalization</AssemblyName>
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
            <Compile Include="__PRODUCTION_SOURCE__"
                     Link="Diagnostics\NormalizeErrorCodeNonErrorCodeInterruptFrame.cs" />
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
