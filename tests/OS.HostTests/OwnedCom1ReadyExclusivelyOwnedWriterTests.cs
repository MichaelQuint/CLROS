using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Kernel.Diagnostics;
using OS.Kernel.Runtime;

internal static unsafe class OwnedCom1ReadyExclusivelyOwnedWriterTests
{
    private const string ExactMarker = "OWNED_COM1_READY";
    private const string ReadyMarker = "CLROS_M02_005_READY marker=OWNED_COM1_READY markerBytes=16 lineEndingBytes=2 " + "writer=ownedCom1 hostCallbacks=true guestPortIo=false";
    private static readonly ushort[] CapturedWritePorts = new ushort[64];
    private static readonly byte[] CapturedWriteValues = new byte[64];
    private static int _capturedWriteCount;
    private static int _capturedReadCount;
    private static int _notReadyReadCount;
    private static ushort _lastReadPort;
    [HostFixture(HostLane.Fast)]
    internal static void RunAll()
    {
        Console.WriteLine("TEST_START m02.005");
        int failures = 0;
        Run("m02.005 writes exact marker only through owned com1", TestWritesExactMarker, ref failures);
        Run("m02.005 polls until transmit ready", TestPollsUntilTransmitReady, ref failures);
        Run("m02.005 fails closed on transmit timeout", TestFailsClosedOnTimeout, ref failures);
        Run("m02.005 rejects invalid poll limit before io", TestRejectsInvalidPollLimit, ref failures);
        Run("m02.005 rejects unready backend before io", TestRejectsUnreadyBackend, ref failures);
        Run("m02.005 exposes exact bounded marker bytes", TestExposesExactMarkerBytes, ref failures);
        Run("m02.005 result defaults fail closed without references", TestDefaultResult, ref failures);
        Run("m02.005 source has no firmware fallback", TestSourceExclusivity, ref failures);
        if (failures != 0)
        {
            Console.WriteLine("TEST_FAIL m02.005 host-tests failures=" + failures);
            throw new InvalidOperationException("OWNED_COM1_READY exclusively-owned-writer tests failed: " + failures);
        }

        Console.WriteLine(ReadyMarker);
        Console.WriteLine("TEST_PASS m02.005");
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {
        HostCheck.Run("" + name, test, ref failures);
    }

    private static bool TestWritesExactMarker()
    {
        ResetCapturedIo(notReadyReadCount: 0);
        Com1OwnedPortIoBackend backend = CreateReadyBackend();
        OwnedCom1ReadyWriterResult result = OwnedCom1ReadyExclusivelyOwnedWriter.Emit(backend, transmitReadyPollLimit: 4);
        if (!result.Succeeded || result.Status != OwnedCom1ReadyWriterStatus.Success || !result.Configuration.Succeeded || result.Configuration.WritesCompleted != Com1OwnedPortIoBackend.RequiredConfigurationWriteCount || !result.LastOperation.Succeeded || result.LastOperation.Port != Com1OwnedPortIoBackend.DataRegisterPort || result.LastOperation.Value != OwnedCom1ReadyExclusivelyOwnedWriter.LineFeed || result.MarkerBytesWritten != OwnedCom1ReadyExclusivelyOwnedWriter.MarkerByteCount || result.LineEndingBytesWritten != OwnedCom1ReadyExclusivelyOwnedWriter.LineEndingByteCount || result.SerialBytesWritten != OwnedCom1ReadyExclusivelyOwnedWriter.RequiredSerialByteCount || result.LineStatusPolls != result.SerialBytesWritten || result.FailedByteIndex != -1 || !result.OwnedBackendUsed || result.FirmwareFallbackUsed || _capturedWriteCount != Com1OwnedPortIoBackend.RequiredConfigurationWriteCount + OwnedCom1ReadyExclusivelyOwnedWriter.RequiredSerialByteCount || _capturedReadCount != OwnedCom1ReadyExclusivelyOwnedWriter.RequiredSerialByteCount)
        {
            return false;
        }

        int payloadOffset = Com1OwnedPortIoBackend.RequiredConfigurationWriteCount;
        string payload = ExactMarker + "\r\n";
        for (int index = 0; index < payload.Length; index++)
        {
            if (CapturedWritePorts[payloadOffset + index] != Com1OwnedPortIoBackend.DataRegisterPort || CapturedWriteValues[payloadOffset + index] != (byte)payload[index])
            {
                return false;
            }
        }

        return CapturedWritePorts[0] == Com1OwnedPortIoBackend.InterruptEnableRegisterPort && CapturedWriteValues[0] == Com1OwnedPortIoBackend.DisableInterruptsValue && CapturedWritePorts[6] == Com1OwnedPortIoBackend.ModemControlRegisterPort && CapturedWriteValues[6] == Com1OwnedPortIoBackend.ModemControlReadyValue && _lastReadPort == Com1OwnedPortIoBackend.LineStatusRegisterPort;
    }

    private static bool TestPollsUntilTransmitReady()
    {
        ResetCapturedIo(notReadyReadCount: 2);
        OwnedCom1ReadyWriterResult result = OwnedCom1ReadyExclusivelyOwnedWriter.Emit(CreateReadyBackend(), transmitReadyPollLimit: 4);
        return result.Succeeded && result.LineStatusPolls == OwnedCom1ReadyExclusivelyOwnedWriter.RequiredSerialByteCount + 2 && _capturedReadCount == result.LineStatusPolls && _capturedWriteCount == Com1OwnedPortIoBackend.RequiredConfigurationWriteCount + OwnedCom1ReadyExclusivelyOwnedWriter.RequiredSerialByteCount;
    }

    private static bool TestFailsClosedOnTimeout()
    {
        ResetCapturedIo(notReadyReadCount: int.MaxValue);
        OwnedCom1ReadyWriterResult result = OwnedCom1ReadyExclusivelyOwnedWriter.Emit(CreateReadyBackend(), transmitReadyPollLimit: 3);
        return !result.Succeeded && result.IsInitialized && result.Status == OwnedCom1ReadyWriterStatus.TransmitReadyTimeout && result.Configuration.Succeeded && result.MarkerBytesWritten == 0 && result.LineEndingBytesWritten == 0 && result.SerialBytesWritten == 0 && result.LineStatusPolls == 3 && result.FailedByteIndex == 0 && result.OwnedBackendUsed && !result.FirmwareFallbackUsed && _capturedWriteCount == Com1OwnedPortIoBackend.RequiredConfigurationWriteCount && _capturedReadCount == 3;
    }

    private static bool TestRejectsInvalidPollLimit()
    {
        ResetCapturedIo(notReadyReadCount: 0);
        OwnedCom1ReadyWriterResult result = OwnedCom1ReadyExclusivelyOwnedWriter.Emit(CreateReadyBackend(), 0);
        return !result.Succeeded && result.IsInitialized && result.Status == OwnedCom1ReadyWriterStatus.InvalidTransmitReadyPollLimit && !result.OwnedBackendUsed && !result.FirmwareFallbackUsed && result.FailedByteIndex == -1 && NoIoCaptured();
    }

    private static bool TestRejectsUnreadyBackend()
    {
        ResetCapturedIo(notReadyReadCount: 0);
        CheckedPortIoFunctionPointerBindingResult binding = InvokeIn8Out8CheckedUnmanagedFunctionPointers.Bind(default(CheckedPortIoFunctionPointerBindingRequest));
        Com1OwnedPortIoBackend backend = new Com1OwnedPortIoBackend(binding);
        OwnedCom1ReadyWriterResult result = OwnedCom1ReadyExclusivelyOwnedWriter.Emit(backend);
        return !result.Succeeded && result.IsInitialized && result.Status == OwnedCom1ReadyWriterStatus.BackendNotReady && !result.Configuration.IsInitialized && !result.OwnedBackendUsed && !result.FirmwareFallbackUsed && NoIoCaptured();
    }

    private static bool TestExposesExactMarkerBytes()
    {
        if (ExactMarker.Length != OwnedCom1ReadyExclusivelyOwnedWriter.MarkerByteCount)
        {
            return false;
        }

        for (int index = 0; index < ExactMarker.Length; index++)
        {
            if (!OwnedCom1ReadyExclusivelyOwnedWriter.TryGetMarkerByte(index, out byte value) || value != (byte)ExactMarker[index])
            {
                return false;
            }
        }

        return !OwnedCom1ReadyExclusivelyOwnedWriter.TryGetMarkerByte(-1, out byte below) && below == 0 && !OwnedCom1ReadyExclusivelyOwnedWriter.TryGetMarkerByte(OwnedCom1ReadyExclusivelyOwnedWriter.MarkerByteCount, out byte above) && above == 0;
    }

    private static bool TestDefaultResult()
    {
        OwnedCom1ReadyWriterResult result = default;
        return !result.IsInitialized && !result.Succeeded && result.Status == OwnedCom1ReadyWriterStatus.Success && !result.Configuration.IsInitialized && !result.LastOperation.IsInitialized && result.SerialBytesWritten == 0 && !result.OwnedBackendUsed && !result.FirmwareFallbackUsed && !RuntimeHelpers.IsReferenceOrContainsReferences<OwnedCom1ReadyWriterResult>();
    }

    private static bool TestSourceExclusivity()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(repositoryRoot, "src", "OS.Kernel", "Diagnostics", "OwnedCom1ReadyExclusivelyOwnedWriter.cs"));
        return source.Contains("Com1OwnedPortIoBackend", StringComparison.Ordinal) && source.Contains("LineStatusRegisterPort", StringComparison.Ordinal) && source.Contains("DataRegisterPort", StringComparison.Ordinal) && source.Contains("TryGetPayloadByte", StringComparison.Ordinal) && !source.Contains("IPlatformSerial", StringComparison.Ordinal) && !source.Contains("GuardedPlatformSerial", StringComparison.Ordinal) && !source.Contains("FirmwareBacked", StringComparison.Ordinal) && !source.Contains("WriteMarkerLine", StringComparison.Ordinal) && !source.Contains("Efi", StringComparison.Ordinal) && !source.Contains("Console", StringComparison.Ordinal) && !source.Contains("SharpOS", StringComparison.Ordinal);
    }

    private static Com1OwnedPortIoBackend CreateReadyBackend()
    {
        delegate* unmanaged<ushort, byte, void> out8Target = &CaptureOut8;
        delegate* unmanaged<ushort, byte> in8Target = &CaptureIn8;
        DiagnosticStubCallableBindingPlanResult bindingPlan = CreateReadyBindingPlan((nuint)out8Target, (nuint)in8Target);
        CheckedPortIoFunctionPointerBindingResult binding = InvokeIn8Out8CheckedUnmanagedFunctionPointers.Bind(bindingPlan);
        return new Com1OwnedPortIoBackend(binding);
    }

    private static DiagnosticStubCallableBindingPlanResult CreateReadyBindingPlan(nuint out8Address, nuint in8Address)
    {
        nuint lowestTarget = out8Address < in8Address ? out8Address : in8Address;
        nuint regionCandidate = lowestTarget - ExecutableStubMemoryPlan.MetadataPrefixByteLength;
        nuint pageMask = (nuint)ExecutableStubMemoryPlan.DefaultPageSize - 1;
        nuint regionBaseAddress = regionCandidate & ~pageMask;
        nuint payloadBaseAddress = regionBaseAddress + ExecutableStubMemoryPlan.MetadataPrefixByteLength;
        nuint out8EndAddress = out8Address + PortIoStubPlan.Out8RequiredByteLength;
        nuint in8EndAddress = in8Address + PortIoStubPlan.In8RequiredByteLength;
        nuint highestTargetEnd = out8EndAddress > in8EndAddress ? out8EndAddress : in8EndAddress;
        ExecutableStubBundleMaterializerResult materializer = ExecutableStubBundleMaterializerResult.Success();
        nuint payloadLength = highestTargetEnd - payloadBaseAddress;
        if (payloadLength < materializer.BytesCopied)
        {
            payloadLength = materializer.BytesCopied;
        }

        ExecutableStubMemoryPlanResult memoryPlan = ExecutableStubMemoryPlan.Create(payloadLength);
        ExecutableStubPageTransitionPlanResult pageTransition = ExecutableStubPageTransitionPlan.Create(memoryPlan, materializer, regionBaseAddress, memoryPlan.RequiredByteLength);
        Com1PortIoBackendContractResult backend = Com1PortIoBackendContractResult.Success(SerialPortConfiguration.Com1BasePort, out8Address - pageTransition.PayloadBaseAddress, in8Address - pageTransition.PayloadBaseAddress, out8Address, in8Address);
        DiagnosticStubInvocationReadinessResult readiness = DiagnosticStubInvocationReadiness.RequireReady(pageTransition, backend);
        return DiagnosticStubCallableBindingPlan.Create(readiness, backend, materializer);
    }

    [UnmanagedCallersOnly]
    private static void CaptureOut8(ushort port, byte value)
    {
        if (_capturedWriteCount < CapturedWritePorts.Length)
        {
            CapturedWritePorts[_capturedWriteCount] = port;
            CapturedWriteValues[_capturedWriteCount] = value;
        }

        _capturedWriteCount++;
    }

    [UnmanagedCallersOnly]
    private static byte CaptureIn8(ushort port)
    {
        _capturedReadCount++;
        _lastReadPort = port;
        if (port != Com1OwnedPortIoBackend.LineStatusRegisterPort)
        {
            return 0;
        }

        return _capturedReadCount <= _notReadyReadCount ? (byte)0 : OwnedCom1ReadyExclusivelyOwnedWriter.TransmitHoldingRegisterEmptyMask;
    }

    private static void ResetCapturedIo(int notReadyReadCount)
    {
        Array.Clear(CapturedWritePorts);
        Array.Clear(CapturedWriteValues);
        _capturedWriteCount = 0;
        _capturedReadCount = 0;
        _notReadyReadCount = notReadyReadCount < 0 ? 0 : notReadyReadCount;
        _lastReadPort = 0;
    }

    private static bool NoIoCaptured()
    {
        return _capturedWriteCount == 0 && _capturedReadCount == 0 && _lastReadPort == 0;
    }
}
