using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Kernel.Diagnostics;
using OS.Kernel.Runtime;

internal static unsafe class Com1OwnedPortIoBackendTests
{
    private const string ReadyMarker = "CLROS_M02_004_READY configurationWrites=7 hostCallbacks=true guestPortIo=false";
    private static readonly ushort[] CapturedWritePorts = new ushort[32];
    private static readonly byte[] CapturedWriteValues = new byte[32];
    private static int _capturedWriteCount;
    private static int _capturedReadCount;
    private static ushort _lastReadPort;
    [HostFixture(HostLane.Fast)]
    internal static void RunAll()
    {
        Console.WriteLine("TEST_START m02.004");
        int failures = 0;
        Run("m02.004 configures default com1 through the checked owned backend", TestConfiguresDefaultCom1, ref failures);
        Run("m02.004 adapter drives the existing serial writer", TestAdapterDrivesExistingSerialWriter, ref failures);
        Run("m02.004 reads line status through the checked owned backend", TestReadsThroughOwnedBackend, ref failures);
        Run("m02.004 rejects an unready binding before port io", TestRejectsUnreadyBinding, ref failures);
        Run("m02.004 rejects ports outside the com1 register window", TestRejectsPortsOutsideCom1Range, ref failures);
        Run("m02.004 default results fail closed without managed references", TestDefaultsFailClosedAndValueContractsContainNoReferences, ref failures);
        if (failures != 0)
        {
            Console.WriteLine("TEST_FAIL m02.004 host-tests failures=" + failures);
            throw new InvalidOperationException("COM1 owned port-I/O backend tests failed: " + failures);
        }

        Console.WriteLine(ReadyMarker);
        Console.WriteLine("TEST_PASS m02.004");
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {
        HostCheck.Run("" + name, test, ref failures);
    }

    private static bool TestConfiguresDefaultCom1()
    {
        ResetCapturedIo();
        Com1OwnedPortIoBackend backend = CreateReadyBackend();
        Com1OwnedPortIoConfigurationResult result = backend.ConfigureDefault();
        Span<SerialPortWrite> expected = stackalloc SerialPortWrite[SerialPortConfiguration.RequiredInitializationWriteCount];
        SerialPortInitializationPlanResult expectedPlan = SerialPortConfiguration.Com1Default.EmitInitializationSequence(expected);
        if (!result.Succeeded || result.Status != Com1OwnedPortIoConfigurationStatus.Success || result.WritesCompleted != Com1OwnedPortIoBackend.RequiredConfigurationWriteCount || result.RequiredWrites != SerialPortConfiguration.RequiredInitializationWriteCount || result.FailedWriteIndex != -1 || !result.LastOperation.Succeeded || !expectedPlan.Succeeded || _capturedWriteCount != expectedPlan.WritesEmitted || _capturedReadCount != 0)
        {
            return false;
        }

        for (int index = 0; index < expectedPlan.WritesEmitted; index++)
        {
            if (CapturedWritePorts[index] != expected[index].Port || CapturedWriteValues[index] != expected[index].Value)
            {
                return false;
            }
        }

        return CapturedWritePorts[0] == Com1OwnedPortIoBackend.InterruptEnableRegisterPort && CapturedWriteValues[0] == Com1OwnedPortIoBackend.DisableInterruptsValue && CapturedWritePorts[2] == Com1OwnedPortIoBackend.DataRegisterPort && CapturedWriteValues[2] == Com1OwnedPortIoBackend.DefaultDivisorLowValue && CapturedWritePorts[6] == Com1OwnedPortIoBackend.ModemControlRegisterPort && CapturedWriteValues[6] == Com1OwnedPortIoBackend.ModemControlReadyValue;
    }

    private static bool TestAdapterDrivesExistingSerialWriter()
    {
        ResetCapturedIo();
        Com1OwnedPortIoBackend backend = CreateReadyBackend();
        Com1OwnedPortIoByteBackend adapter = new Com1OwnedPortIoByteBackend(backend);
        Com1SerialWriter writer = new Com1SerialWriter(adapter, SerialPortConfiguration.Com1Default, transmitReadyPollLimit: 4);
        Com1SerialWriterResult initialized = writer.Initialize();
        Com1SerialWriterResult written = writer.WriteAscii("OK"u8);
        return backend.IsReady && initialized.Succeeded && initialized.PortWritesEmitted == SerialPortConfiguration.RequiredInitializationWriteCount && written.Succeeded && written.BytesWritten == 2 && written.PortWritesEmitted == 2 && written.LineStatusPolls == 2 && adapter.LastOperation.Succeeded && adapter.LastOperation.OperationKind == PortIoOperationKind.Out8 && adapter.LastOperation.Port == Com1OwnedPortIoBackend.DataRegisterPort && adapter.LastOperation.Value == (byte)'K' && _capturedWriteCount == SerialPortConfiguration.RequiredInitializationWriteCount + 2 && CapturedWritePorts[7] == Com1OwnedPortIoBackend.DataRegisterPort && CapturedWriteValues[7] == (byte)'O' && CapturedWritePorts[8] == Com1OwnedPortIoBackend.DataRegisterPort && CapturedWriteValues[8] == (byte)'K' && _capturedReadCount == 2 && _lastReadPort == Com1OwnedPortIoBackend.LineStatusRegisterPort;
    }

    private static bool TestReadsThroughOwnedBackend()
    {
        ResetCapturedIo();
        Com1OwnedPortIoBackend backend = CreateReadyBackend();
        Com1OwnedPortIoOperationResult result = backend.ReadByte(Com1OwnedPortIoBackend.LineStatusRegisterPort);
        return result.Succeeded && result.Status == Com1OwnedPortIoOperationStatus.Success && result.BindingStatus == CheckedPortIoFunctionPointerBindingStatus.Ready && result.InvocationStatus == CheckedPortIoFunctionPointerInvocationStatus.Success && result.OperationKind == PortIoOperationKind.In8 && result.Port == Com1OwnedPortIoBackend.LineStatusRegisterPort && result.Value == Com1SerialWriter.LineStatusTransmitEmptyMask && result.WasInvoked && _capturedReadCount == 1 && _lastReadPort == Com1OwnedPortIoBackend.LineStatusRegisterPort && _capturedWriteCount == 0;
    }

    private static bool TestRejectsUnreadyBinding()
    {
        ResetCapturedIo();
        CheckedPortIoFunctionPointerBindingResult binding = InvokeIn8Out8CheckedUnmanagedFunctionPointers.Bind(default(CheckedPortIoFunctionPointerBindingRequest));
        Com1OwnedPortIoBackend backend = new Com1OwnedPortIoBackend(binding);
        Com1OwnedPortIoConfigurationResult configuration = backend.ConfigureDefault();
        Com1OwnedPortIoOperationResult read = backend.ReadByte(Com1OwnedPortIoBackend.LineStatusRegisterPort);
        return !backend.IsReady && !configuration.Succeeded && configuration.Status == Com1OwnedPortIoConfigurationStatus.BackendNotReady && configuration.WritesCompleted == 0 && configuration.FailedWriteIndex == 0 && !configuration.LastOperation.Succeeded && configuration.LastOperation.Status == Com1OwnedPortIoOperationStatus.BindingNotReady && !configuration.LastOperation.WasInvoked && !read.Succeeded && read.Status == Com1OwnedPortIoOperationStatus.BindingNotReady && !read.WasInvoked && NoIoCaptured();
    }

    private static bool TestRejectsPortsOutsideCom1Range()
    {
        ResetCapturedIo();
        Com1OwnedPortIoBackend backend = CreateReadyBackend();
        Com1OwnedPortIoOperationResult below = backend.WriteByte((ushort)(Com1OwnedPortIoBackend.BasePort - 1), 0x5a);
        Com1OwnedPortIoOperationResult above = backend.ReadByte((ushort)(Com1OwnedPortIoBackend.EndPort + 1));
        return !below.Succeeded && below.Status == Com1OwnedPortIoOperationStatus.PortOutsideConfiguredRange && below.InvocationStatus == CheckedPortIoFunctionPointerInvocationStatus.PortOutsideConfiguredRange && !below.WasInvoked && !above.Succeeded && above.Status == Com1OwnedPortIoOperationStatus.PortOutsideConfiguredRange && above.InvocationStatus == CheckedPortIoFunctionPointerInvocationStatus.PortOutsideConfiguredRange && !above.WasInvoked && NoIoCaptured();
    }

    private static bool TestDefaultsFailClosedAndValueContractsContainNoReferences()
    {
        Com1OwnedPortIoBackend backend = default;
        Com1OwnedPortIoOperationResult operation = default;
        Com1OwnedPortIoConfigurationResult configuration = default;
        return !backend.IsReady && !operation.IsInitialized && !operation.Succeeded && operation.Status == Com1OwnedPortIoOperationStatus.Success && !operation.WasInvoked && !configuration.IsInitialized && !configuration.Succeeded && configuration.Status == Com1OwnedPortIoConfigurationStatus.Success && !RuntimeHelpers.IsReferenceOrContainsReferences<Com1OwnedPortIoBackend>() && !RuntimeHelpers.IsReferenceOrContainsReferences<Com1OwnedPortIoOperationResult>() && !RuntimeHelpers.IsReferenceOrContainsReferences<Com1OwnedPortIoConfigurationResult>();
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
        return port == Com1OwnedPortIoBackend.LineStatusRegisterPort ? Com1SerialWriter.LineStatusTransmitEmptyMask : (byte)0;
    }

    private static void ResetCapturedIo()
    {
        Array.Clear(CapturedWritePorts);
        Array.Clear(CapturedWriteValues);
        _capturedWriteCount = 0;
        _capturedReadCount = 0;
        _lastReadPort = 0;
    }

    private static bool NoIoCaptured()
    {
        return _capturedWriteCount == 0 && _capturedReadCount == 0 && _lastReadPort == 0;
    }
}
