[CmdletBinding()]
param(
    [ValidateSet("Release", "Debug")]
    [string] $Configuration = "Release",
    [string] $Runtime = "win-x64",
    [string] $LogPath = "artifacts/logs/efi-shell-publish.log",
    [string] $EfiOutputPath = "artifacts/efi/BOOTX64.EFI",
    [string] $VsWherePath = "",
    [string[]] $MsvcLinkerSearchRoot = @(),
    [string[]] $WindowsSdkLibSearchRoot = @(),
    [string] $IlCompilerPath = "",
    [string[]] $IlCompilerSearchRoot = @(),
    [string] $NativeAotRuntimePackPath = "",
    [string[]] $NativeAotRuntimePackSearchRoot = @(),
    [string] $QemuPath = "",
    [string[]] $QemuSearchRoot = @(),
    [string] $QemuImgPath = "",
    [string[]] $QemuImgSearchRoot = @(),
    [string] $OvmfCodePath = "",
    [string[]] $OvmfSearchRoot = @(),
    [ValidateRange(100, 60000)]
    [int] $VersionProbeTimeoutMilliseconds = 5000,
    [ValidateRange(5, 300)]
    [int] $ArtifactVerifierTimeoutSeconds = 60,
    [ValidateRange(5, 3600)]
    [int] $BuildProcessTimeoutSeconds = 300,
    [ValidateRange(4096, 67108864)]
    [int] $BuildProcessMaximumOutputCharacters = 4194304,
    [switch] $PreflightOnly,
    [switch] $EnableUefiGoneSimulationProbe,
    [switch] $EnableExitBootServicesFreshMapProbe,
    [switch] $EnableAcpiPhysicalDiscoveryProbe,
    [switch] $EnableM01LoadedImageStackProbe,
    [switch] $EnableM01LiveAllocatorProbe,
    [switch] $EnableM01OwnedPagingProbe,
    [switch] $EnableM02SafeStubProbe,
    [switch] $EnableM02CheckedPortIoBindingProbe,
    [switch] $EnableM02Com1OwnedPortIoProbe,
    [switch] $EnableM02OwnedCom1ReadyProbe,
    [switch] $EnableM02PanicSafeOwnedCom1Probe,
    [switch] $EnableM02OwnedX64GdtEncodingProbe,
    [switch] $EnableM02GuardedIstStackAllocationProbe,
    [switch] $EnableM02OwnedGdtTssTerminalProbe,
    [switch] $EnableM02NormalizedInterruptFramePageProbe,
    [switch] $EnableM02OwnedIdtGatePopulationProbe,
    [switch] $EnableM02IdtrContinuedExecutionProbe,
    [switch] $EnableM02Ud2VectorStateProbe,
    [switch] $EnableM02PageFaultCr2ErrorCodeProbe,
    [switch] $EnableM02DoubleFaultIstProbe,
    [switch] $EnableM02UnexpectedVectorPanicProbe,
    [switch] $EnableM04IndependentEndianConformanceProbe,
    [switch] $EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe,
    [switch] $EnableM04ArithmeticConformanceProbe,
    [switch] $EnableM04LayoutContractProbe,
    [switch] $EnableM04ManagedHeapPageRangeProbe,
    [switch] $EnableM04RhpNewFastProbe,
    [switch] $EnableM04RhpNewFastBeforeBindFailureProbe,
    [switch] $EnableM04RhpNewFastExhaustionFailureProbe,
    [switch] $EnableM04SzArrayAllocationProbe,
    [switch] $EnableM04SzArrayAllocationBeforeBindProbe,
    [switch] $EnableM04SzArrayAllocationExhaustionProbe,
    [switch] $EnableM04StringAllocationProbe,
    [switch] $EnableM04StringAllocationBeforeBindProbe,
    [switch] $EnableM04StringAllocationExhaustionProbe,
    [switch] $EnableM04StringAllocationInvalidLengthProbe,
    [switch] $EnableM03MemoryMapCaptureProbe,
    [switch] $EnableM03FirmwareEntropySeedProbe,
    [switch] $EnableM04TypeManagerModuleRegistrationProbe,
    [switch] $EnableM04X64UnwindMetadataProbe,
    [switch] $EnableM04SimpleThrowCatchProbe,
    [switch] $EnableM04GcStaticRootMaterializationProbe,
    [switch] $EnableM04SeededCollectorChurnProbe,
    [switch] $EnableM03TypedExitBootServicesCallProbe,
    [switch] $EnableM03FirstRealExitBootServicesProbe,
    [switch] $EnableM03FirmwareAdapterRevocationProbe,
    [switch] $EnableM03OwnedSubsystemSurvivalProbe,
    [switch] $EnableM03OwnedSubsystemPanicProbe,
    [switch] $EnableM04ClassConstructorExecutionProbe,
    [switch] $EnableM04InterfaceDispatchProbe,
    [switch] $EnableM04CheckedReferenceBarrierProbe
)

$ErrorActionPreference = "Stop"

$script:EfiPreflightLines = New-Object System.Collections.Generic.List[string]

$script:ExpectedEfiCompileItems = @(
    '..\OS.Kernel\Boot\BootHandoffRecord.cs|Boot\BootHandoffRecord.cs',
    '..\OS.Kernel\Boot\BootMemoryRegionMaterializer.cs|Boot\BootMemoryRegionMaterializer.cs',
    '..\OS.Kernel\Boot\BootMemoryMapAcceptance.cs|Boot\BootMemoryMapAcceptance.cs',
    '..\OS.Kernel\Boot\EfiConfigurationTableScanner.cs|Boot\EfiConfigurationTableScanner.cs',
    '..\OS.Kernel\Boot\EfiBootHandoffValidator.cs|Boot\EfiBootHandoffValidator.cs',
    '..\OS.Kernel\Boot\EfiMemoryMapNormalizer.cs|Boot\EfiMemoryMapNormalizer.cs',
    '..\OS.Kernel\Boot\UefiExitBootServicesPlan.cs|Boot\UefiExitBootServicesPlan.cs',
    '..\OS.Kernel\Boot\UefiMemoryMapCapture.cs|Boot\UefiMemoryMapCapture.cs',
    '..\OS.Kernel\Boot\UefiMemoryMapCopyPlan.cs|Boot\UefiMemoryMapCopyPlan.cs',
    '..\OS.Kernel\Boot\BootSequencePlan.cs|Boot\BootSequencePlan.cs',
    '..\OS.Kernel\Boot\TypedPhysicalVirtualDeviceAddressPODWrappers.cs|Boot\TypedPhysicalVirtualDeviceAddressPODWrappers.cs',
    '..\OS.Kernel\Memory\BootReservedRange.cs|Memory\BootReservedRange.cs',
    '..\OS.Kernel\Memory\BootReservationTableValidator.cs|Memory\BootReservationTableValidator.cs',
    '..\OS.Kernel\Boot\EfiLoadedImageRange.cs|Boot\EfiLoadedImageRange.cs',
    '..\OS.Kernel\Boot\EfiLoadedImageRangeCapturePlan.cs|Boot\EfiLoadedImageRangeCapturePlan.cs',
    '..\OS.Kernel\Boot\OwnedBootStackLayout.cs|Boot\OwnedBootStackLayout.cs',
    '..\OS.Kernel\Boot\UefiMemoryType.cs|Boot\UefiMemoryType.cs',
    '..\OS.Kernel\Boot\OwnedBootStackSwitchPlan.cs|Boot\OwnedBootStackSwitchPlan.cs',
    '..\OS.Kernel\Memory\BootPageAllocatorCoreContracts.cs|Memory\BootPageAllocatorCoreContracts.cs',
    '..\OS.Kernel\Memory\BootPageAllocatorStateMaterializer.cs|Memory\BootPageAllocatorStateMaterializer.cs',
    '..\OS.Kernel\Memory\BootReservationSubtractor.cs|Memory\BootReservationSubtractor.cs',
    '..\OS.Kernel\Memory\BootPageAllocatorMutation.cs|Memory\BootPageAllocatorMutation.cs',
    '..\OS.Kernel\Memory\PageProtection.cs|Memory\PageProtection.cs',
    '..\OS.Kernel\Memory\PageCachePolicy.cs|Memory\PageCachePolicy.cs',
    '..\OS.Kernel\Memory\X64PageTableEntry.cs|Memory\X64PageTableEntry.cs',
    '..\OS.Kernel\Boot\X64BootIdentityMappingPlan.cs|Boot\X64BootIdentityMappingPlan.cs',
    '..\OS.Kernel\Memory\X64Cr3LoadStubPlan.cs|Memory\X64Cr3LoadStubPlan.cs',
    '..\OS.Kernel\Memory\X64PagingModePreflightStubPlan.cs|Memory\X64PagingModePreflightStubPlan.cs',
    '..\OS.Kernel\Memory\X64ReversibleCr3TransitionStubPlan.cs|Memory\X64ReversibleCr3TransitionStubPlan.cs',
    '..\OS.Kernel\Memory\X64PageMap.cs|Memory\X64PageMap.cs',
    '..\OS.Kernel\Memory\X64TlbInvalidationPlan.cs|Memory\X64TlbInvalidationPlan.cs',
    '..\OS.Kernel\Memory\X64InvlpgStubPlan.cs|Memory\X64InvlpgStubPlan.cs',
    '..\OS.Kernel\Memory\X64ProtectionProbeStubPlan.cs|Memory\X64ProtectionProbeStubPlan.cs',
    '..\OS.Kernel\Memory\PageZeroMappingPolicy.cs|Memory\PageZeroMappingPolicy.cs',
    '..\OS.Kernel\Diagnostics\PortIoOperationPlan.cs|Diagnostics\PortIoOperationPlan.cs',
    '..\OS.Kernel\Diagnostics\PortIoStubPlan.cs|Diagnostics\PortIoStubPlan.cs',
    '..\OS.Kernel\Diagnostics\ExistingIn8Out8StubBytesRWPagesFinalize.cs|Diagnostics\ExistingIn8Out8StubBytesRWPagesFinalize.cs',
    '..\OS.Kernel\Diagnostics\InvokeIn8Out8CheckedUnmanagedFunctionPointers.cs|Diagnostics\InvokeIn8Out8CheckedUnmanagedFunctionPointers.cs',
    '..\OS.Kernel\Diagnostics\COM1OwnedPortIOBackend.cs|Diagnostics\COM1OwnedPortIOBackend.cs',
    '..\OS.Kernel\Diagnostics\OWNEDCOM1READYExclusivelyOwnedWriter.cs|Diagnostics\OWNEDCOM1READYExclusivelyOwnedWriter.cs',
    '..\OS.Kernel\Diagnostics\PanicSafeFormattingSerialReportingOwnedCOM1.cs|Diagnostics\PanicSafeFormattingSerialReportingOwnedCOM1.cs',
    '..\OS.Kernel\Diagnostics\OwnedX64GDTContainingNullN64BitCode.cs|Diagnostics\OwnedX64GDTContainingNullN64BitCode.cs',
    '..\OS.Kernel\Diagnostics\GuardedISTStacksDoubleFaultPageFaultNMI.cs|Diagnostics\GuardedISTStacksDoubleFaultPageFaultNMI.cs',
    '..\OS.Kernel\Diagnostics\OwnedGDTTSSGeneratedAuditedCSharpStubBytes.cs|Diagnostics\OwnedGDTTSSGeneratedAuditedCSharpStubBytes.cs',
    '..\OS.Kernel\Diagnostics\NormalizeErrorCodeNonErrorCodeInterruptFrame.cs|Diagnostics\NormalizeErrorCodeNonErrorCodeInterruptFrame.cs',
    '..\OS.Kernel\Diagnostics\PopulateAllN256IDTVectorsOwnedGatesNever.cs|Diagnostics\PopulateAllN256IDTVectorsOwnedGatesNever.cs',
    '..\OS.Kernel\Diagnostics\IDTRContinuedExecution.cs|Diagnostics\IDTRContinuedExecution.cs',
    '..\OS.Kernel\Diagnostics\InvalidOpcodeProbeAuthority.cs|Diagnostics\InvalidOpcodeProbeAuthority.cs',
    '..\OS.Kernel\Diagnostics\NullPointerProbeAuthority.cs|Diagnostics\NullPointerProbeAuthority.cs',
    '..\OS.Kernel\Diagnostics\NullPageFaultProbePlan.cs|Diagnostics\NullPageFaultProbePlan.cs',
    '..\OS.Kernel\Diagnostics\SeparateDisabledDefaultPageFaultCR2ErrorCode.cs|Diagnostics\SeparateDisabledDefaultPageFaultCR2ErrorCode.cs',
    '..\OS.Kernel\Diagnostics\SeparateDisabledDefaultPageFaultPageTables.cs|Diagnostics\SeparateDisabledDefaultPageFaultPageTables.cs',
    '..\OS.Kernel\Diagnostics\SeparateDisabledDefaultPageFaultTransition.cs|Diagnostics\SeparateDisabledDefaultPageFaultTransition.cs',
    '..\OS.Kernel\Diagnostics\DoubleFaultIstProbeAuthority.cs|Diagnostics\DoubleFaultIstProbeAuthority.cs',
    '..\OS.Kernel\Diagnostics\DoubleFaultISTPathSilentRebootOrTriple.cs|Diagnostics\DoubleFaultISTPathSilentRebootOrTriple.cs',
    '..\OS.Kernel\Diagnostics\UnexpectedVectorPanicProbeAuthority.cs|Diagnostics\UnexpectedVectorPanicProbeAuthority.cs',
    '..\OS.Kernel\Diagnostics\UnexpectedVectorPanicEvidenceDeterministicQEMUFailureClassification.cs|Diagnostics\UnexpectedVectorPanicEvidenceDeterministicQEMUFailureClassification.cs',
    '..\OS.Kernel\Diagnostics\InvalidOpcodeVectorAuthority.cs|Diagnostics\InvalidOpcodeVectorAuthority.cs',
    '..\OS.Kernel\Diagnostics\CpuFaultFrame.cs|Diagnostics\CpuFaultFrame.cs',
    '..\OS.Kernel\Diagnostics\DisabledDefaultUd2VectorState.cs|Diagnostics\DisabledDefaultUd2VectorState.cs',
    '..\OS.Kernel\Diagnostics\DisabledDefaultUd2VectorStateTransition.cs|Diagnostics\DisabledDefaultUd2VectorStateTransition.cs',
    '..\..\std\no-runtime\shared\Binary\IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMOSA.cs|Std\Binary\IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMOSA.cs',
    '..\..\std\no-runtime\shared\Memory\SpanOverlapMemoryCastUnsafeWidthConformanceVectors.cs|Std\Memory\SpanOverlapMemoryCastUnsafeWidthConformanceVectors.cs',
    '..\..\std\no-runtime\shared\Numerics\IndependentArithmeticShiftCastN64BitDivisionRemainder.cs|Std\Numerics\IndependentArithmeticShiftCastN64BitDivisionRemainder.cs',
    '..\OS.Kernel\Runtime\ObjectArrayStringEETypeMethodTableLayoutContracts.cs|Runtime\ObjectArrayStringEETypeMethodTableLayoutContracts.cs',
    '..\OS.Kernel\Runtime\ManagedHeapPageRangeLiveAllocator.cs|Runtime\ManagedHeapPageRangeLiveAllocator.cs',
    '..\OS.Kernel\Boot\M01LoadedImageStackIntegration.cs|Boot\M01LoadedImageStackIntegration.cs',
    '..\OS.Kernel\Boot\M01LiveAllocatorIntegration.cs|Boot\M01LiveAllocatorIntegration.cs',
    '..\OS.Kernel\Boot\M01LivePagingIntegration.cs|Boot\M01LivePagingIntegration.cs',
    '..\OS.Kernel\Boot\M01OwnedCr3CompositeTransition.cs|Boot\M01OwnedCr3CompositeTransition.cs',
    'Boot\EfiConsoleAdapter.cs|',
    'Boot\EfiEntry.cs|',
    'Boot\M02CheckedPortIoFunctionPointerIntegration.cs|',
    'Boot\M02Com1OwnedPortIoBackendIntegration.cs|',
    'Boot\M02SafeStubPageIntegration.cs|',
    'Boot\X64InstructionPublication.cs|',
    'Runtime\RuntimeImportAttribute.cs|',
    'Runtime\MethodImplAttribute.cs|',
    'Runtime\MinimalRuntime.cs|',
    '..\OS.Kernel\Runtime\GuestTestRhpNewFast.cs|Runtime\GuestTestRhpNewFast.cs',
    '..\OS.Kernel\Runtime\GuestTestArrayAllocationLengthOverflowChecks.cs|Runtime\GuestTestArrayAllocationLengthOverflowChecks.cs',
    '..\OS.Kernel\Runtime\GuestTestStringAllocationCharacterStorage.cs|Runtime\GuestTestStringAllocationCharacterStorage.cs',
    '..\OS.Kernel\Diagnostics\StringAllocationProbeAuthority.cs|Diagnostics\StringAllocationProbeAuthority.cs',
    'Runtime\RhpNewFastAllocation.cs|',
    '..\OS.Kernel\Diagnostics\MemoryMapCaptureProbeAuthority.cs|Diagnostics\MemoryMapCaptureProbeAuthority.cs',
    'Runtime\CheckedReferenceBarrierAdapter.cs|',
    '..\OS.Kernel\Runtime\CheckedReferenceBulkBarriersIncludingDotNetN10Bulk.cs|Runtime\CheckedReferenceBulkBarriersIncludingDotNetN10Bulk.cs',
    '..\OS.Kernel\Diagnostics\CheckedReferenceBarrierProbeAuthority.cs|Diagnostics\CheckedReferenceBarrierProbeAuthority.cs',
    '..\OS.Kernel\Diagnostics\FirmwareEntropySeedCaptureProbeAuthority.cs|Diagnostics\FirmwareEntropySeedCaptureProbeAuthority.cs',
    '..\OS.Kernel\Runtime\TypeManagerModuleMetadataLiveNativeAOTImage.cs|Runtime\TypeManagerModuleMetadataLiveNativeAOTImage.cs',
    'Runtime\NativeAotModuleRegistrationProbe.cs|',
    '..\OS.Kernel\Diagnostics\TypeManagerModuleRegistrationProbeAuthority.cs|Diagnostics\TypeManagerModuleRegistrationProbeAuthority.cs',
    '..\OS.Kernel\Boot\TypedNoStdlibExitBootServicesCallAdapter.cs|Boot\TypedNoStdlibExitBootServicesCallAdapter.cs',
    '..\OS.Kernel\Boot\NoAllocationOrUnrelatedFirmwareCallOccursBetween.cs|Boot\NoAllocationOrUnrelatedFirmwareCallOccursBetween.cs',
    '..\OS.Kernel\Boot\BoundedRetryStaleKeyStatus.cs|Boot\BoundedRetryStaleKeyStatus.cs',
    '..\OS.Kernel\Boot\FirstOptRealExitBootServicesTransition.cs|Boot\FirstOptRealExitBootServicesTransition.cs',
    '..\OS.Kernel\Boot\OwnedAllocatorDescriptorTablesACPISnapshotPanicOutput.cs|Boot\OwnedAllocatorDescriptorTablesACPISnapshotPanicOutput.cs',
    '..\OS.Kernel\Boot\BootSequence.cs|Boot\BootSequence.cs',
    '..\OS.Kernel\Diagnostics\Probes.cs|Diagnostics\Probes.cs',
    '..\..\std\no-runtime\shared\Hash\Crc32.cs|Std\Hash\Crc32.cs',
    '..\OS.Kernel\Platform\FirmwareServicesMode.cs|Platform\FirmwareServicesMode.cs',
    '..\OS.Kernel\Diagnostics\TypedExitBootServicesCallProbeAuthority.cs|Diagnostics\TypedExitBootServicesCallProbeAuthority.cs',
    '..\OS.Kernel\Runtime\GCStaticRootsMalformedMetadata.cs|Runtime\GCStaticRootsMalformedMetadata.cs',
    '..\OS.Kernel\Runtime\BoundedNonMovingCollectorExplicitRootEnumeration.cs|Runtime\BoundedNonMovingCollectorExplicitRootEnumeration.cs',
    'Runtime\GCStaticRootMaterializationProbe.cs|',
    '..\OS.Kernel\Diagnostics\GCStaticRootMaterializationProbeAuthority.cs|Diagnostics\GCStaticRootMaterializationProbeAuthority.cs',
    '..\..\std\no-runtime\shared\DeterministicRandom32.cs|Std\DeterministicRandom32.cs',
    '..\OS.Kernel\Runtime\SeededAllocationChurnCollectionRootRetentionGuestProbes.cs|Runtime\SeededAllocationChurnCollectionRootRetentionGuestProbes.cs',
    'Runtime\SeededCollectorChurnProbe.cs|',
    '..\OS.Kernel\Boot\ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.cs|Boot\ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.cs',
    '..\OS.Kernel\Runtime\CompilerClassConstructorExecution.cs|Runtime\CompilerClassConstructorExecution.cs',
    'Runtime\ClassConstructorRunner.cs|',
    'Runtime\CompilerClassConstructorExecutionProbe.cs|',
    '..\OS.Kernel\Runtime\CompilerInterfaceDispatch.cs|Runtime\CompilerInterfaceDispatch.cs',
    'Runtime\InterfaceDispatchProbe.cs|',
    '..\OS.Kernel\Runtime\X64PERuntimeFunctionUnwindMetadataHostileInput.cs|Runtime\X64PERuntimeFunctionUnwindMetadataHostileInput.cs',
    'Runtime\X64UnwindMetadataProbe.cs|',
    '..\OS.Kernel\Runtime\ExceptionTypeMatch.cs|Runtime\ExceptionTypeMatch.cs',
    '..\OS.Kernel\Runtime\X64FrameUnwind.cs|Runtime\X64FrameUnwind.cs',
    '..\OS.Kernel\Runtime\NativeAotEhClauseMetadata.cs|Runtime\NativeAotEhClauseMetadata.cs',
    'Runtime\ExceptionCatchProbe.cs|'
)

$script:ExpectedSerializerTargetsImport =
    '..\..\tools\OS.X64TextSerializerGenerator\OS.X64TextSerializerGenerator.targets'
$script:ExpectedSerializerObjectBytes = 336
$script:ExpectedSerializerObjectSha256 =
    'DA97E50C95E479541B65EB3F96D9C6DED163735857650BEE9416324086D55C7B'
$script:ExpectedSerializerSymbol =
    'CLROS_X64_SerializeInstructionPublication'
$script:ExpectedSerializerSuccessCookie = 1397902403

$script:ExpectedPackageBuildClosure = @(
    [ordered] @{
        Path = 'microsoft.dotnet.ilcompiler/10.0.8/build/Microsoft.DotNet.ILCompiler.SingleEntry.targets'
        Sha256 = '345c9448182507befa430e80622f68c7ab09debe9c419d98b418f7f1cb79c34e'
    },
    [ordered] @{
        Path = 'microsoft.dotnet.ilcompiler/10.0.8/build/Microsoft.NETCore.Native.Publish.targets'
        Sha256 = '6fcb3f0491a79fd5a67281b44439e0685c17fe4c848a07477ab2a57a10a68c60'
    },
    [ordered] @{
        Path = 'microsoft.dotnet.ilcompiler/10.0.8/build/Microsoft.NETCore.Native.Windows.targets'
        Sha256 = '547266fbf1e772518edda8194413ce99906d3deb8bef074bbcca1033c31bc54c'
    },
    [ordered] @{
        Path = 'microsoft.dotnet.ilcompiler/10.0.8/build/Microsoft.NETCore.Native.targets'
        Sha256 = '22516102af0e4bdafa004795d3bbc30d617efe5bb7b809005d1ea3a00c436f6d'
    },
    [ordered] @{
        Path = 'microsoft.dotnet.ilcompiler/10.0.8/tools/netstandard/ILCompiler.Build.Tasks.dll'
        Sha256 = '4c8cd85bcf8e2d51d69c0fce3eaea55c2c8b23765da747c0c6cbfc38f9cccd88'
    },
    [ordered] @{
        Path = 'microsoft.dotnet.ilcompiler/10.0.8/tools/netstandard/ILCompiler.Build.Tasks.deps.json'
        Sha256 = '893364a48ebfd6ca97a440e32fc2252f53dbc4f4c64d63f62ac9a583365ec7c6'
    },
    [ordered] @{
        Path = 'microsoft.net.illink.tasks/10.0.10/build/Microsoft.NET.ILLink.targets'
        Sha256 = 'f1bbe7e9e79badb900a594691fe4c6738e906d911ee3e50975434d09035a2568'
    },
    [ordered] @{
        Path = 'microsoft.net.illink.tasks/10.0.10/tools/net/ILLink.Tasks.dll'
        Sha256 = '9c5ffc1e594b75c1c716173c12f501ff4a7aa0cfb7a84019c7176f2a3f4c6120'
    },
    [ordered] @{
        Path = 'microsoft.net.illink.tasks/10.0.10/tools/net/ILLink.Tasks.deps.json'
        Sha256 = '4bd1d4ac7c249fa1911c53106c757fd390fb40aad0d690368bcda0941ff8bbae'
    }
)

$script:ExpectedRestoreLibraries = [ordered] @{
    'Microsoft.DotNet.ILCompiler/10.0.8' = [ordered] @{
        Path = 'microsoft.dotnet.ilcompiler/10.0.8'
        Sha512 = 'RJxitcN5CCyZDcPNXKLsecwKvACzmy8C1z8hGM9+hFcnPhv1jDysJFFIeUHIPWaZ6wDAfYtZcgKEtegvL2Nz8A=='
        FileCount = 20
        TotalBytes = 404859
        AggregateSha256 = 'b9c5a195c93e515f34066c9d8b0b3827cf1a32ce681454d284c489622a5bfcde'
    }
    'Microsoft.NET.ILLink.Tasks/10.0.10' = [ordered] @{
        Path = 'microsoft.net.illink.tasks/10.0.10'
        Sha512 = 'f5VCIE7AJpd5YvzNTeMGVzQIgyE9tX+AreTYwQF+REbu+DZo/2Ae+jNSwhPEYrVz6RRkd7y8ubXjk6Nn6Ka+Cg=='
        FileCount = 42
        TotalBytes = 5337318
        AggregateSha256 = '0edc00810e9462f8d6a7468835861db6d2833f970539e6d4eb82cde5eb42ffb6'
    }
    'runtime.win-x64.Microsoft.DotNet.ILCompiler/10.0.8' = [ordered] @{
        Path = 'runtime.win-x64.microsoft.dotnet.ilcompiler/10.0.8'
        Sha512 = 'S+9omeowlFjRReD0VB/Q4u3DcN7+vueJSF37saFj0U2OSJIkfI7gGKQRsDxGIf0MwLha14qGrjRC3bDGhvF0Gg=='
        FileCount = 13
        TotalBytes = 37461393
        AggregateSha256 = 'c5aff04c7a4abcca691fc0e6a8539628cded3250a7777d6c407dc64c07ca858e'
    }
}

$script:ExpectedConsumedPackageTrees = @(
    [ordered] @{
        Id = 'microsoft.dotnet.ilcompiler'
        Version = '10.0.8'
        ContentHashSha512 = 'RJxitcN5CCyZDcPNXKLsecwKvACzmy8C1z8hGM9+hFcnPhv1jDysJFFIeUHIPWaZ6wDAfYtZcgKEtegvL2Nz8A=='
        FileCount = 20
        TotalBytes = 404859
        AggregateSha256 = 'b9c5a195c93e515f34066c9d8b0b3827cf1a32ce681454d284c489622a5bfcde'
        RootKind = 'restore'
    },
    [ordered] @{
        Id = 'microsoft.net.illink.tasks'
        Version = '10.0.10'
        ContentHashSha512 = 'f5VCIE7AJpd5YvzNTeMGVzQIgyE9tX+AreTYwQF+REbu+DZo/2Ae+jNSwhPEYrVz6RRkd7y8ubXjk6Nn6Ka+Cg=='
        FileCount = 42
        TotalBytes = 5337318
        AggregateSha256 = '0edc00810e9462f8d6a7468835861db6d2833f970539e6d4eb82cde5eb42ffb6'
        RootKind = 'restore'
    },
    [ordered] @{
        Id = 'runtime.win-x64.microsoft.dotnet.ilcompiler'
        Version = '10.0.8'
        ContentHashSha512 = 'S+9omeowlFjRReD0VB/Q4u3DcN7+vueJSF37saFj0U2OSJIkfI7gGKQRsDxGIf0MwLha14qGrjRC3bDGhvF0Gg=='
        FileCount = 13
        TotalBytes = 37461393
        AggregateSha256 = 'c5aff04c7a4abcca691fc0e6a8539628cded3250a7777d6c407dc64c07ca858e'
        RootKind = 'restore'
    },
    [ordered] @{
        Id = 'runtime.win-x64.microsoft.dotnet.ilcompiler'
        Version = '10.0.10'
        ContentHashSha512 = 'e5P00ObvdmAl49KkvGvqDLE5+EXu/IdXCDsLghgKFXKrNybyxMmH12HuGrsO7LTIGjXI58tyN3Hwxwr/MR0d8A=='
        FileCount = 13
        TotalBytes = 37473738
        AggregateSha256 = '17a69cfcd4a3a770d27b7315313c85f68b3a956878af4b95699f4011572e63fb'
        RootKind = 'restore-only'
    },
    [ordered] @{
        Id = 'microsoft.netcore.app.runtime.nativeaot.win-x64'
        Version = '10.0.10'
        ContentHashSha512 = 'NHkgRRE/Fbaw/z0eLdC7rNF7ZRoR+2KQGSPh2ZiKgXpuZOz3Cjhv0srLOaeYnvYMDtT+KzjyyZGgChXx1W18wA=='
        FileCount = 406
        TotalBytes = 109133220
        AggregateSha256 = '389ade21d811efc6fc4d6f0f1aa492ba71f5fabfb39e4780b83c5a65e9a680c4'
        RootKind = 'restore-only'
    },
    [ordered] @{
        Id = 'microsoft.netcore.app.runtime.nativeaot.win-x64'
        Version = '10.0.8'
        ContentHashSha512 = '4OPm4RaS1aAEf0GOcA/IsdD/ml9o6Hw2BaiHy/iceHxExoj+38QFAyNTxcNAz73SDPHXpUp4SL046vBq5RkuEQ=='
        FileCount = 406
        TotalBytes = 109016717
        AggregateSha256 = '73972c0d17f29fa389de9c4a87051b372fa15afe5cdadf55bef731a3d8144fbc'
        RootKind = 'runtime'
    }
)

$script:ExpectedActivePackageImportEdges = @(
    'sdk|microsoft.dotnet.ilcompiler/10.0.8/build/Microsoft.DotNet.ILCompiler.SingleEntry.targets',
    'microsoft.dotnet.ilcompiler/10.0.8/build/Microsoft.DotNet.ILCompiler.SingleEntry.targets|microsoft.dotnet.ilcompiler/10.0.8/build/Microsoft.NETCore.Native.targets',
    'microsoft.dotnet.ilcompiler/10.0.8/build/Microsoft.NETCore.Native.targets|microsoft.dotnet.ilcompiler/10.0.8/build/Microsoft.NETCore.Native.Windows.targets',
    'microsoft.dotnet.ilcompiler/10.0.8/build/Microsoft.NETCore.Native.targets|microsoft.dotnet.ilcompiler/10.0.8/build/Microsoft.NETCore.Native.Publish.targets',
    'sdk|microsoft.net.illink.tasks/10.0.10/build/Microsoft.NET.ILLink.targets'
)

$script:ExpectedPackageTaskBindings = @(
    'ComputeManagedAssembliesToCompileToNative|microsoft.dotnet.ilcompiler/10.0.8/tools/netstandard/ILCompiler.Build.Tasks.dll',
    'ComputeManagedAssemblies|microsoft.net.illink.tasks/10.0.10/tools/net/ILLink.Tasks.dll'
)

$script:ExpectedRestoreArchives = @(
    [ordered] @{
        Id = 'microsoft.dotnet.ilcompiler'
        Version = '10.0.8'
        FileName = 'microsoft.dotnet.ilcompiler.10.0.8.nupkg'
        Sha256 = 'ccdc381bbc7672c48bc755311fdf2f5907433933c295198172d2ad6958ed10b4'
    },
    [ordered] @{
        Id = 'microsoft.net.illink.tasks'
        Version = '10.0.10'
        FileName = 'microsoft.net.illink.tasks.10.0.10.nupkg'
        Sha256 = '97a0eca2030901738f070f142aaf02b3b1aead2bc984c08c428847910b4be312'
    },
    [ordered] @{
        Id = 'runtime.win-x64.microsoft.dotnet.ilcompiler'
        Version = '10.0.8'
        FileName = 'runtime.win-x64.microsoft.dotnet.ilcompiler.10.0.8.nupkg'
        Sha256 = '49f15032ef981172b8c25360f97b76d8abab75348f8e16da0a3fbb970c10bec8'
    },
    [ordered] @{
        Id = 'runtime.win-x64.microsoft.dotnet.ilcompiler'
        Version = '10.0.10'
        FileName = 'runtime.win-x64.microsoft.dotnet.ilcompiler.10.0.10.nupkg'
        Sha256 = '0ae1c40ef28915e63c257999d4cd860db9086878c3a6f8e30388e00056ab350a'
    },
    [ordered] @{
        Id = 'microsoft.netcore.app.runtime.nativeaot.win-x64'
        Version = '10.0.10'
        FileName = 'microsoft.netcore.app.runtime.nativeaot.win-x64.10.0.10.nupkg'
        Sha256 = '5fd7129af9eed4b5a2ed04219b636f7ba0dd965569275d3379fc4d02f623e626'
    }
)

$script:ExpectedM01ReviewedSourceIncludes = @(
    '..\OS.Kernel\Boot\TypedPhysicalVirtualDeviceAddressPODWrappers.cs',
    '..\OS.Kernel\Memory\BootReservedRange.cs',
    '..\OS.Kernel\Memory\BootReservationTableValidator.cs',
    '..\OS.Kernel\Boot\EfiLoadedImageRange.cs',
    '..\OS.Kernel\Boot\EfiLoadedImageRangeCapturePlan.cs',
    '..\OS.Kernel\Boot\OwnedBootStackLayout.cs',
    '..\OS.Kernel\Boot\OwnedBootStackSwitchPlan.cs',
    '..\OS.Kernel\Memory\BootPageAllocatorCoreContracts.cs',
    '..\OS.Kernel\Memory\BootPageAllocatorStateMaterializer.cs',
    '..\OS.Kernel\Memory\BootReservationSubtractor.cs',
    '..\OS.Kernel\Memory\BootPageAllocatorMutation.cs',
    '..\OS.Kernel\Memory\PageProtection.cs',
    '..\OS.Kernel\Memory\PageCachePolicy.cs',
    '..\OS.Kernel\Memory\X64PageTableEntry.cs',
    '..\OS.Kernel\Boot\X64BootIdentityMappingPlan.cs',
    '..\OS.Kernel\Memory\X64Cr3LoadStubPlan.cs',
    '..\OS.Kernel\Memory\X64PagingModePreflightStubPlan.cs',
    '..\OS.Kernel\Memory\X64ReversibleCr3TransitionStubPlan.cs',
    '..\OS.Kernel\Memory\X64PageMap.cs',
    '..\OS.Kernel\Memory\X64TlbInvalidationPlan.cs',
    '..\OS.Kernel\Memory\X64InvlpgStubPlan.cs',
    '..\OS.Kernel\Memory\X64ProtectionProbeStubPlan.cs',
    '..\OS.Kernel\Memory\PageZeroMappingPolicy.cs',
    '..\OS.Kernel\Diagnostics\PortIoOperationPlan.cs',
    '..\OS.Kernel\Diagnostics\PortIoStubPlan.cs',
    '..\OS.Kernel\Diagnostics\ExistingIn8Out8StubBytesRWPagesFinalize.cs',
    '..\OS.Kernel\Boot\M01LoadedImageStackIntegration.cs',
    '..\OS.Kernel\Boot\M01LiveAllocatorIntegration.cs',
    '..\OS.Kernel\Boot\M01LivePagingIntegration.cs',
    '..\OS.Kernel\Boot\M01OwnedCr3CompositeTransition.cs',
    '..\OS.Kernel\Boot\BootSequencePlan.cs'
)

$script:ExpectedM01PrerequisiteSourceIncludes = @(
    '..\OS.Kernel\Boot\BootHandoffRecord.cs',
    '..\OS.Kernel\Boot\BootMemoryRegionMaterializer.cs',
    '..\OS.Kernel\Boot\UefiMemoryType.cs'
)

$script:M01NostdlibExclusionCounts = @{
    '..\OS.Kernel\Boot\BootMemoryRegionMaterializer.cs' = 1
    '..\OS.Kernel\Boot\EfiConfigurationTableScanner.cs' = 1
    '..\OS.Kernel\Memory\BootPageAllocatorMutation.cs' = 2
    '..\OS.Kernel\Boot\M01LoadedImageStackIntegration.cs' = 2
    '..\OS.Kernel\Boot\M01OwnedCr3CompositeTransition.cs' = 1
}

$script:M01SourceContractTokens = @{
    '..\OS.Kernel\Boot\M01LivePagingIntegration.cs' = @(
        'public const ulong RequiredAllocationRecordCount = 3;',
        'public const ulong Cr3CodeAllocationRecordIndex = 0;',
        'public const ulong ProtectionProbeAllocationRecordIndex = 1;',
        'public const ulong TableStateAllocationRecordIndex = 2;',
        'public const ulong RequiredNonImageMappingRequirementCount = 10;',
        'M01LivePagingMapLedgerEvidence',
        'BootReservationKind.AllocatorArena',
        'BootReservationKind.PageTableArena',
        '_mapCaptureOrdinal > _lastAllocationOrdinal',
        '_ledgerBuildOrdinal > _mapCaptureOrdinal',
        'AreDisjoint(',
        'ProtectionMutationRejected',
        'ProtectionProbeRejected',
        'CallerAttestedPostAllocationOwnedMap',
        'public const bool CallsFirmwarePageAllocator = false;',
        'public const bool InvokesFirmware = false;',
        'public const bool InvokesFirmwareCallbacks = false;',
        'public const bool RestoresModeledProbeLeafToInitialProtection = true;'
    )
    '..\OS.Kernel\Boot\M01OwnedCr3CompositeTransition.cs' = @(
        'public const ulong RequiredRangeBindingCount = 7;',
        'public const bool CallsFirmware = false;',
        'public const bool DefinesSingleCompositeOwnedCr3Interval = true;',
        'public const bool InvokesFirmwareCallbacks = false;',
        'public const bool InvokesArbitraryCallbacks = false;',
        'public const bool ExactProbeIsSoleInternalTransfer = true;',
        'StateFirmwareCallbackCountOffset',
        'StateExpectedCanaryOffset',
        'StateReplacementCanaryOffset',
        'StateOriginalCr3RawOffset',
        'StateOriginalCr4RawOffset',
        'StateOriginalRflagsOffset',
        'StateFirmwareCr3ReadbackOffset',
        'StatePostRestoreCanaryOffset',
        'FirmwareCallbackForbidden',
        'FirmwareCallbackObserved',
        'RestoreCr3ThenCr4ThenRflags',
        'RestoreFirmwareCr3',
        'Cr4RestoreReadbackMismatch',
        'RflagsRestoreReadbackMismatch',
        'PostRestoreCanaryMismatch',
        'CompleteTableArena',
        'RuntimeStateAndCanary',
        'StateTableStateArenaStartOffset',
        'StateCanaryAddressOffset',
        'PostRestoreCanaryReadback'
    )
    '..\OS.Kernel\Diagnostics\ExistingIn8Out8StubBytesRWPagesFinalize.cs' = @(
        'public const ulong PageSize = 4096;',
        'public const ulong Out8Offset = 16;',
        'public const ulong In8Offset = 32;',
        'public const ulong ExpectedFnv1a64 = 0x982e_5b7e_3ee5_8e56;',
        'public const bool PerformsLiveExecution = false;',
        'public const bool InvokesSafeStub = false;',
        'public const bool InvokesPortIo = false;',
        'public static ExistingIn8Out8StubPageResult Materialize(',
        'public static ExistingIn8Out8StubPageResult ValidateReadback(',
        'public static ExistingIn8Out8StubPageResult AuditFinalization('
    )
}

function Write-EfiPreflightLine {
    param([string] $Line)

    Write-Host $Line
    $script:EfiPreflightLines.Add($Line)
}

function Save-EfiPreflightLog {
    param([string] $Path)

    if ($null -eq $script:M01LogAuthority) {
        throw "log authority is not initialized: $Path"
    }
    Write-M01LogLines `
        -Authority $script:M01LogAuthority `
        -Lines $script:EfiPreflightLines.ToArray() `
        -Replace
}

function Resolve-ToolPath {
    param(
        [string[]] $SearchRoot,
        [string] $FileName,
        [string] $PathPattern,
        [scriptblock] $Validator = $null
    )

    $candidateFiles = New-Object System.Collections.Generic.List[System.IO.FileInfo]
    $seenPaths = New-Object 'System.Collections.Generic.HashSet[string]' (
        [System.StringComparer]::OrdinalIgnoreCase)
    foreach ($candidateRoot in $SearchRoot) {
        if ([string]::IsNullOrWhiteSpace($candidateRoot)) {
            continue
        }

        if (-not (Test-Path -LiteralPath $candidateRoot)) {
            continue
        }

        $rootMatches = Get-ChildItem -LiteralPath $candidateRoot -Recurse -File -Filter $FileName -ErrorAction SilentlyContinue |
            Where-Object {
                [string]::IsNullOrWhiteSpace($PathPattern) -or
                $_.FullName -match $PathPattern
            }
        foreach ($match in $rootMatches) {
            if ($seenPaths.Add($match.FullName)) {
                $candidateFiles.Add($match)
            }
        }
    }

    $orderedMatches = $candidateFiles |
        Sort-Object `
            @{ Expression = { Get-NumericPathVersion -Path $_.FullName }; Descending = $true },
            @{ Expression = { $_.FullName }; Descending = $false }
    foreach ($match in $orderedMatches) {
        if ($null -eq $Validator -or (& $Validator $match.FullName)) {
            return $match.FullName
        }
    }

    return $null
}

function Get-NumericPathVersion {
    param([string] $Path)

    $best = [version] "0.0"
    $matches = [System.Text.RegularExpressions.Regex]::Matches(
        $Path,
        '(?<!\d)\d+\.\d+(?:\.\d+){0,2}(?!\d)')
    foreach ($match in $matches) {
        try {
            $parsed = [version] $match.Value
            if ($parsed -gt $best) {
                $best = $parsed
            }
        }
        catch {
            # Ignore path components that resemble, but are not, valid versions.
        }
    }

    return $best
}

function Test-PeX64Executable {
    param([string] $Path)

    try {
        $stream = [System.IO.File]::Open(
            $Path,
            [System.IO.FileMode]::Open,
            [System.IO.FileAccess]::Read,
            [System.IO.FileShare]::Read)
        try {
            if ($stream.Length -lt 256) {
                return $false
            }

            $reader = New-Object System.IO.BinaryReader($stream)
            if ($reader.ReadUInt16() -ne 0x5a4d) {
                return $false
            }

            $stream.Position = 0x3c
            $peOffset = [long] $reader.ReadUInt32()
            if ($peOffset -lt 64 -or $peOffset -gt ($stream.Length - 24)) {
                return $false
            }

            $stream.Position = $peOffset
            if ($reader.ReadUInt32() -ne 0x00004550) {
                return $false
            }

            return $reader.ReadUInt16() -eq 0x8664
        }
        finally {
            $stream.Dispose()
        }
    }
    catch {
        return $false
    }
}

function Test-CoffArchive {
    param([string] $Path)

    try {
        $stream = [System.IO.File]::Open(
            $Path,
            [System.IO.FileMode]::Open,
            [System.IO.FileAccess]::Read,
            [System.IO.FileShare]::Read)
        try {
            if ($stream.Length -lt 68) {
                return $false
            }

            $signature = New-Object byte[] 8
            if ($stream.Read($signature, 0, $signature.Length) -ne $signature.Length) {
                return $false
            }

            return [System.Text.Encoding]::ASCII.GetString($signature) -eq "!<arch>`n"
        }
        finally {
            $stream.Dispose()
        }
    }
    catch {
        return $false
    }
}

function Read-M01SecureXmlDocument {
    param([string] $Path)

    $settings = New-Object System.Xml.XmlReaderSettings
    $settings.DtdProcessing = [System.Xml.DtdProcessing]::Prohibit
    $settings.XmlResolver = $null
    $reader = $null
    try {
        $reader = [System.Xml.XmlReader]::Create($Path, $settings)
        $document = New-Object System.Xml.XmlDocument
        $document.XmlResolver = $null
        $document.Load($reader)
        return $document
    }
    finally {
        if ($null -ne $reader) {
            $reader.Dispose()
        }
    }
}

function Initialize-M01NativeFileIdentity {
    if ($null -ne ("CLROS.Build.AuthorityNativeFile" -as [type])) {
        return
    }

    Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace CLROS.Build
{
    public sealed class AuthorityFileIdentity
    {
        public string Key { get; private set; }
        public uint LinkCount { get; private set; }
        public ulong Length { get; private set; }

        internal AuthorityFileIdentity(string key, uint linkCount, ulong length)
        {
            Key = key;
            LinkCount = linkCount;
            Length = length;
        }
    }

    public static class AuthorityNativeFile
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct FILETIME
        {
            public uint Low;
            public uint High;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BY_HANDLE_FILE_INFORMATION
        {
            public uint FileAttributes;
            public FILETIME CreationTime;
            public FILETIME LastAccessTime;
            public FILETIME LastWriteTime;
            public uint VolumeSerialNumber;
            public uint FileSizeHigh;
            public uint FileSizeLow;
            public uint NumberOfLinks;
            public uint FileIndexHigh;
            public uint FileIndexLow;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetFileInformationByHandle(
            SafeFileHandle handle,
            out BY_HANDLE_FILE_INFORMATION information);

        public static AuthorityFileIdentity GetIdentity(SafeFileHandle handle)
        {
            if (handle == null || handle.IsInvalid || handle.IsClosed)
            {
                throw new ArgumentException("A live file handle is required.", "handle");
            }

            BY_HANDLE_FILE_INFORMATION information;
            if (!GetFileInformationByHandle(handle, out information))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            string key = String.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "{0:x8}:{1:x8}{2:x8}",
                information.VolumeSerialNumber,
                information.FileIndexHigh,
                information.FileIndexLow);
            ulong length =
                ((ulong)information.FileSizeHigh << 32) |
                information.FileSizeLow;
            return new AuthorityFileIdentity(
                key,
                information.NumberOfLinks,
                length);
        }
    }
}
'@
}

function Get-M01StreamSha256 {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.Stream] $Stream
    )

    $originalPosition = $Stream.Position
    $algorithm = [System.Security.Cryptography.SHA256]::Create()
    try {
        $Stream.Position = 0
        return ([System.BitConverter]::ToString(
                $algorithm.ComputeHash($Stream))).Replace(
            "-",
            "").ToLowerInvariant()
    }
    finally {
        $Stream.Position = $originalPosition
        $algorithm.Dispose()
    }
}

function Assert-M01NoReparsePoints {
    param(
        [Parameter(Mandatory = $true)]
        [string] $RootPath,
        [Parameter(Mandatory = $true)]
        [string] $Path,
        [Parameter(Mandatory = $true)]
        [string] $Reason,
        [switch] $AllowMissingLeaf
    )

    $rootCandidate = [System.IO.Path]::GetFullPath($RootPath)
    $rootVolume = [System.IO.Path]::GetPathRoot($rootCandidate)
    if ([string]::IsNullOrWhiteSpace($rootVolume)) {
        throw "$Reason root_missing path=$rootCandidate"
    }
    $rootFullPath = if ([string]::Equals(
            $rootCandidate,
            $rootVolume,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        $rootVolume
    }
    else {
        $rootCandidate.TrimEnd(
            [System.IO.Path]::DirectorySeparatorChar,
            [System.IO.Path]::AltDirectorySeparatorChar)
    }
    $pathFullPath = [System.IO.Path]::GetFullPath($Path)
    $rootPrefix = if ($rootFullPath.EndsWith(
            [string][System.IO.Path]::DirectorySeparatorChar,
            [System.StringComparison]::Ordinal)) {
        $rootFullPath
    }
    else {
        $rootFullPath + [System.IO.Path]::DirectorySeparatorChar
    }
    if (-not [string]::Equals(
            $pathFullPath,
            $rootFullPath,
            [System.StringComparison]::OrdinalIgnoreCase) -and
        -not $pathFullPath.StartsWith(
            $rootPrefix,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "$Reason outside_root path=$pathFullPath root=$rootFullPath"
    }

    $volumeRoot = [System.IO.Path]::GetPathRoot($pathFullPath)
    if ([string]::IsNullOrWhiteSpace($volumeRoot)) {
        throw "$Reason volume_root_missing path=$pathFullPath"
    }
    $segments = $pathFullPath.Substring($volumeRoot.Length).Split(
        @(
            [System.IO.Path]::DirectorySeparatorChar,
            [System.IO.Path]::AltDirectorySeparatorChar
        ),
        [System.StringSplitOptions]::RemoveEmptyEntries)
    $currentPath = $volumeRoot
    for ($index = 0; $index -lt $segments.Length; $index++) {
        $currentPath = Join-Path $currentPath $segments[$index]
        if (-not (Test-Path -LiteralPath $currentPath)) {
            if ($AllowMissingLeaf -and $index -eq ($segments.Length - 1)) {
                return
            }
            throw "$Reason missing_component path=$currentPath"
        }
        $item = Get-Item -LiteralPath $currentPath -Force -ErrorAction Stop
        if (($item.Attributes -band
                [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "$Reason reparse_point path=$currentPath"
        }
    }
}

function Open-M01AuthorityReadRecord {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,
        [Parameter(Mandatory = $true)]
        [string] $RootPath,
        [Parameter(Mandatory = $true)]
        [string] $Purpose,
        [Parameter(Mandatory = $true)]
        [string] $FailurePrefix,
        [string] $ExpectedSha256,
        [switch] $AllowEmpty,
        [switch] $RequireSingleLink
    )

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    Assert-M01NoReparsePoints `
        -RootPath $RootPath `
        -Path $fullPath `
        -Reason ($FailurePrefix + "_reparse")
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        throw "$FailurePrefix missing path=$fullPath"
    }

    Initialize-M01NativeFileIdentity
    $stream = $null
    try {
        $stream = New-Object System.IO.FileStream(
            $fullPath,
            [System.IO.FileMode]::Open,
            [System.IO.FileAccess]::Read,
            [System.IO.FileShare]::Read,
            65536,
            [System.IO.FileOptions]::SequentialScan)
        Assert-M01NoReparsePoints `
            -RootPath $RootPath `
            -Path $fullPath `
            -Reason ($FailurePrefix + "_reparse")
        $identity = [CLROS.Build.AuthorityNativeFile]::GetIdentity(
            $stream.SafeFileHandle)
        $length = [long] $stream.Length
        if ($length -lt 0 -or
            (-not $AllowEmpty -and $length -eq 0) -or
            [uint64] $length -ne [uint64] $identity.Length) {
            throw "$FailurePrefix size_invalid purpose=$Purpose path=$fullPath"
        }
        if ($RequireSingleLink -and [uint32] $identity.LinkCount -ne 1) {
            throw (
                "$FailurePrefix hardlink_invalid purpose=$Purpose path=$fullPath " +
                "links=$($identity.LinkCount)")
        }
        $sha256 = Get-M01StreamSha256 -Stream $stream
        if (-not [string]::IsNullOrWhiteSpace($ExpectedSha256) -and
            $sha256 -cne $ExpectedSha256.ToLowerInvariant()) {
            throw "$FailurePrefix hash_mismatch purpose=$Purpose path=$fullPath"
        }

        $probe = New-Object System.IO.FileStream(
            $fullPath,
            [System.IO.FileMode]::Open,
            [System.IO.FileAccess]::Read,
            [System.IO.FileShare]::Read,
            4096,
            [System.IO.FileOptions]::SequentialScan)
        try {
            $probeIdentity = [CLROS.Build.AuthorityNativeFile]::GetIdentity(
                $probe.SafeFileHandle)
        }
        finally {
            $probe.Dispose()
        }
        if ([string] $probeIdentity.Key -cne [string] $identity.Key) {
            throw "$FailurePrefix path_identity_mismatch purpose=$Purpose path=$fullPath"
        }

        $record = [pscustomobject] @{
            Path = $fullPath
            RootPath = [System.IO.Path]::GetFullPath($RootPath)
            Purpose = $Purpose
            FailurePrefix = $FailurePrefix
            Stream = $stream
            InitialLength = $length
            InitialSha256 = $sha256
            InitialIdentityKey = [string] $identity.Key
            InitialLinkCount = [uint32] $identity.LinkCount
            RequireSingleLink = [bool] $RequireSingleLink
        }
        $stream = $null
        return $record
    }
    finally {
        if ($null -ne $stream) {
            $stream.Dispose()
        }
    }
}

function Assert-M01AuthorityRecordUnchanged {
    param(
        [Parameter(Mandatory = $true)]
        [object] $Record
    )

    Assert-M01NoReparsePoints `
        -RootPath $Record.RootPath `
        -Path $Record.Path `
        -Reason ($Record.FailurePrefix + "_reparse")
    $identity = [CLROS.Build.AuthorityNativeFile]::GetIdentity(
        $Record.Stream.SafeFileHandle)
    $finalLength = [long] $Record.Stream.Length
    $finalSha256 = Get-M01StreamSha256 -Stream $Record.Stream
    $probe = New-Object System.IO.FileStream(
        $Record.Path,
        [System.IO.FileMode]::Open,
        [System.IO.FileAccess]::Read,
        [System.IO.FileShare]::Read,
        4096,
        [System.IO.FileOptions]::SequentialScan)
    try {
        $probeIdentity = [CLROS.Build.AuthorityNativeFile]::GetIdentity(
            $probe.SafeFileHandle)
    }
    finally {
        $probe.Dispose()
    }
    if ($finalLength -ne [long] $Record.InitialLength -or
        $finalSha256 -cne [string] $Record.InitialSha256 -or
        [string] $identity.Key -cne [string] $Record.InitialIdentityKey -or
        [string] $probeIdentity.Key -cne [string] $Record.InitialIdentityKey -or
        [uint32] $identity.LinkCount -ne [uint32] $Record.InitialLinkCount -or
        ($Record.RequireSingleLink -and [uint32] $identity.LinkCount -ne 1)) {
        throw (
            "$($Record.FailurePrefix) changed purpose=$($Record.Purpose) " +
            "path=$($Record.Path)")
    }
}

function Assert-M01DistinctAuthorityRecords {
    param(
        [Parameter(Mandatory = $true)]
        [object[]] $Records
    )

    $identityPaths =
        New-Object 'System.Collections.Generic.Dictionary[string,string]' (
            [System.StringComparer]::Ordinal)
    foreach ($record in $Records) {
        if ([uint32] $record.InitialLinkCount -ne 1) {
            throw (
                "authority_file_hardlink_invalid purpose=$($record.Purpose) " +
                "path=$($record.Path) links=$($record.InitialLinkCount)")
        }
        if ($identityPaths.ContainsKey(
                [string] $record.InitialIdentityKey)) {
            throw (
                "authority_file_alias first=" +
                $identityPaths[[string] $record.InitialIdentityKey] +
                " second=$($record.Path)")
        }
        $identityPaths.Add(
            [string] $record.InitialIdentityKey,
            [string] $record.Path)
    }
}

function Close-M01AuthorityRecords {
    param(
        [AllowNull()]
        [object] $Records
    )

    if ($null -eq $Records) {
        return
    }
    for ($index = $Records.Count - 1; $index -ge 0; $index--) {
        try {
            $Records[$index].Stream.Dispose()
        }
        catch {
            # Process exit remains the final handle-cleanup boundary.
        }
    }
    $Records.Clear()
}

function Initialize-M01PlainDirectoryPath {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,
        [Parameter(Mandatory = $true)]
        [string] $Reason
    )

    $pathCandidate = [System.IO.Path]::GetFullPath($Path)
    $volumeRoot = [System.IO.Path]::GetPathRoot($pathCandidate)
    if ([string]::IsNullOrWhiteSpace($volumeRoot)) {
        throw "$Reason volume_root_missing path=$pathCandidate"
    }
    $fullPath = if ([string]::Equals(
            $pathCandidate,
            $volumeRoot,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        $volumeRoot
    }
    else {
        $pathCandidate.TrimEnd(
            [System.IO.Path]::DirectorySeparatorChar,
            [System.IO.Path]::AltDirectorySeparatorChar)
    }
    $currentPath = $volumeRoot
    $segments = $fullPath.Substring($volumeRoot.Length).Split(
        @(
            [System.IO.Path]::DirectorySeparatorChar,
            [System.IO.Path]::AltDirectorySeparatorChar
        ),
        [System.StringSplitOptions]::RemoveEmptyEntries)
    foreach ($segment in $segments) {
        $currentPath = Join-Path $currentPath $segment
        if (-not (Test-Path -LiteralPath $currentPath)) {
            New-Item `
                -ItemType Directory `
                -Path $currentPath `
                -ErrorAction Stop | Out-Null
        }
        $item = Get-Item -LiteralPath $currentPath -Force -ErrorAction Stop
        if (-not $item.PSIsContainer -or
            ($item.Attributes -band
                [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "$Reason not_plain_directory path=$currentPath"
        }
    }
    Assert-M01NoReparsePoints `
        -RootPath $volumeRoot `
        -Path $fullPath `
        -Reason $Reason
    return $fullPath
}

function Assert-M01WritableAuthorityIdentity {
    param(
        [Parameter(Mandatory = $true)]
        [object] $Record
    )

    Assert-M01NoReparsePoints `
        -RootPath $Record.RootPath `
        -Path $Record.Path `
        -Reason ($Record.FailurePrefix + "_reparse")
    $identity = [CLROS.Build.AuthorityNativeFile]::GetIdentity(
        $Record.Stream.SafeFileHandle)
    $probe = New-Object System.IO.FileStream(
        $Record.Path,
        [System.IO.FileMode]::Open,
        [System.IO.FileAccess]::Read,
        [System.IO.FileShare]::ReadWrite,
        4096,
        [System.IO.FileOptions]::SequentialScan)
    try {
        $probeIdentity = [CLROS.Build.AuthorityNativeFile]::GetIdentity(
            $probe.SafeFileHandle)
    }
    finally {
        $probe.Dispose()
    }
    if ([string] $identity.Key -cne [string] $Record.InitialIdentityKey -or
        [string] $probeIdentity.Key -cne [string] $Record.InitialIdentityKey -or
        [uint32] $identity.LinkCount -ne 1) {
        throw (
            "$($Record.FailurePrefix) identity_changed path=$($Record.Path)")
    }
}

function Open-M01LogAuthority {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    Initialize-M01NativeFileIdentity
    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $volumeRoot = [System.IO.Path]::GetPathRoot($fullPath)
    $directory = Split-Path -Parent $fullPath
    [void] (Initialize-M01PlainDirectoryPath `
            -Path $directory `
            -Reason "log_authority_ancestor_invalid")
    Assert-M01NoReparsePoints `
        -RootPath $volumeRoot `
        -Path $fullPath `
        -Reason "log_authority_leaf_invalid" `
        -AllowMissingLeaf
    $mode = if (Test-Path -LiteralPath $fullPath) {
        if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
            throw "log_authority_leaf_invalid path=$fullPath"
        }
        [System.IO.FileMode]::Open
    }
    else {
        [System.IO.FileMode]::CreateNew
    }
    $stream = $null
    try {
        $stream = New-Object System.IO.FileStream(
            $fullPath,
            $mode,
            [System.IO.FileAccess]::ReadWrite,
            [System.IO.FileShare]::Read,
            4096,
            [System.IO.FileOptions]::SequentialScan)
        Assert-M01NoReparsePoints `
            -RootPath $volumeRoot `
            -Path $fullPath `
            -Reason "log_authority_leaf_invalid"
        $identity = [CLROS.Build.AuthorityNativeFile]::GetIdentity(
            $stream.SafeFileHandle)
        if ([uint32] $identity.LinkCount -ne 1) {
            throw (
                "log_authority_hardlink_invalid path=$fullPath " +
                "links=$($identity.LinkCount)")
        }
        $record = [pscustomobject] @{
            Path = $fullPath
            RootPath = $volumeRoot
            Purpose = "publish-log"
            FailurePrefix = "log_authority"
            Stream = $stream
            InitialIdentityKey = [string] $identity.Key
            InitialLinkCount = [uint32] $identity.LinkCount
        }
        Assert-M01WritableAuthorityIdentity -Record $record
        $record.Stream.SetLength(0)
        $record.Stream.Position = 0
        $record.Stream.Flush($true)
        $stream = $null
        return $record
    }
    finally {
        if ($null -ne $stream) {
            $stream.Dispose()
        }
    }
}

function Write-M01LogLines {
    param(
        [Parameter(Mandatory = $true)]
        [object] $Authority,
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [string[]] $Lines,
        [switch] $Replace
    )

    Assert-M01WritableAuthorityIdentity -Record $Authority
    if ($Replace) {
        $Authority.Stream.SetLength(0)
        $Authority.Stream.Position = 0
    }
    else {
        $Authority.Stream.Position = $Authority.Stream.Length
    }
    if ($Lines.Count -gt 0) {
        $text = [string]::Join([Environment]::NewLine, $Lines) +
            [Environment]::NewLine
        $bytes = (New-Object System.Text.UTF8Encoding($false)).GetBytes($text)
        $Authority.Stream.Write($bytes, 0, $bytes.Length)
    }
    $Authority.Stream.Flush($true)
    Assert-M01WritableAuthorityIdentity -Record $Authority
}

function Read-M01LogAuthorityText {
    param(
        [Parameter(Mandatory = $true)]
        [object] $Authority
    )

    Assert-M01WritableAuthorityIdentity -Record $Authority
    $originalPosition = $Authority.Stream.Position
    try {
        $Authority.Stream.Position = 0
        $reader = New-Object System.IO.StreamReader(
            $Authority.Stream,
            (New-Object System.Text.UTF8Encoding($false, $true)),
            $true,
            4096,
            $true)
        try {
            return $reader.ReadToEnd()
        }
        finally {
            $reader.Dispose()
        }
    }
    finally {
        $Authority.Stream.Position = $originalPosition
    }
}

function Get-M01OutputPublicationPlan {
    param(
        [Parameter(Mandatory = $true)]
        [string] $RepositoryRoot,
        [Parameter(Mandatory = $true)]
        [string] $EfiOutputPath,
        [Parameter(Mandatory = $true)]
        [string] $LogFullPath
    )

    $repositoryFullPath = [System.IO.Path]::GetFullPath($RepositoryRoot)
    $outputDirectory = [System.IO.Path]::GetFullPath(
        (Join-Path $repositoryFullPath "artifacts\efi"))
    [void] (Initialize-M01PlainDirectoryPath `
            -Path $outputDirectory `
            -Reason "output_authority_ancestor_invalid")
    $efiFullPath = if ([System.IO.Path]::IsPathRooted($EfiOutputPath)) {
        [System.IO.Path]::GetFullPath($EfiOutputPath)
    }
    else {
        [System.IO.Path]::GetFullPath(
            (Join-Path $repositoryFullPath $EfiOutputPath))
    }
    if (-not [string]::Equals(
            (Split-Path -Parent $efiFullPath),
            $outputDirectory,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw (
            "output_authority_path_invalid path=$efiFullPath " +
            "expectedDirectory=$outputDirectory")
    }
    $paths = @(
        $efiFullPath,
        ($efiFullPath + ".link.map"),
        ($efiFullPath + ".link.rsp"),
        ($efiFullPath + ".link.json"),
        ($efiFullPath + ".serializer.obj")
    )
    if ($EnableM04CheckedReferenceBarrierProbe -or $EnableM04GcStaticRootMaterializationProbe) { $paths += $efiFullPath + ".references.obj" }
    if ($EnableM04InterfaceDispatchProbe) { $paths += $efiFullPath + ".dispatch.obj" }
    if ($EnableM04SimpleThrowCatchProbe) { $paths += $efiFullPath + ".catch.obj" }
    $seenPaths = New-Object 'System.Collections.Generic.HashSet[string]' (
        [System.StringComparer]::OrdinalIgnoreCase)
    $volumeRoot = [System.IO.Path]::GetPathRoot($outputDirectory)
    foreach ($path in $paths) {
        if (-not $seenPaths.Add($path)) {
            throw "output_authority_duplicate_path path=$path"
        }
        if ([string]::Equals(
                $path,
                [System.IO.Path]::GetFullPath($LogFullPath),
                [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "output_authority_log_path_collision path=$path"
        }
        Assert-M01NoReparsePoints `
            -RootPath $volumeRoot `
            -Path $path `
            -Reason "output_authority_leaf_invalid" `
            -AllowMissingLeaf
    }
    return [pscustomobject] @{
        Directory = $outputDirectory
        EfiPath = $paths[0]
        MapPath = $paths[1]
        ResponsePath = $paths[2]
        EvidencePath = $paths[3]
        SerializerObjectPath = $paths[4]
        Paths = $paths
    }
}

function Clear-M01OutputPublicationLeaves {
    param(
        [Parameter(Mandatory = $true)]
        [object] $Plan
    )

    $existingRecords = New-Object System.Collections.Generic.List[object]
    try {
        foreach ($path in $Plan.Paths) {
            if (-not (Test-Path -LiteralPath $path)) {
                continue
            }
            $existingRecords.Add((Open-M01AuthorityReadRecord `
                    -Path $path `
                    -RootPath $Plan.Directory `
                    -Purpose "stale-publish-output" `
                    -FailurePrefix "output_cleanup_authority" `
                    -AllowEmpty `
                    -RequireSingleLink))
        }
        if ($existingRecords.Count -gt 0) {
            Assert-M01DistinctAuthorityRecords -Records $existingRecords.ToArray()
        }
    }
    finally {
        Close-M01AuthorityRecords -Records $existingRecords
    }
    foreach ($path in $Plan.Paths) {
        Assert-M01NoReparsePoints `
            -RootPath $Plan.Directory `
            -Path $path `
            -Reason "output_cleanup_authority" `
            -AllowMissingLeaf
        if (Test-Path -LiteralPath $path -PathType Leaf) {
            [System.IO.File]::Delete($path)
        }
        if (Test-Path -LiteralPath $path) {
            throw "output_cleanup_failed path=$path"
        }
    }
}

function Copy-M01FileCreateNew {
    param(
        [Parameter(Mandatory = $true)]
        [string] $SourcePath,
        [Parameter(Mandatory = $true)]
        [string] $SourceRootPath,
        [Parameter(Mandatory = $true)]
        [string] $DestinationPath,
        [Parameter(Mandatory = $true)]
        [string] $DestinationRootPath,
        [Parameter(Mandatory = $true)]
        [string] $Purpose
    )

    $source = Open-M01AuthorityReadRecord `
        -Path $SourcePath `
        -RootPath $SourceRootPath `
        -Purpose $Purpose `
        -FailurePrefix "output_source_authority" `
        -RequireSingleLink
    $destination = $null
    try {
        Assert-M01NoReparsePoints `
            -RootPath $DestinationRootPath `
            -Path $DestinationPath `
            -Reason "output_destination_authority" `
            -AllowMissingLeaf
        $destination = New-Object System.IO.FileStream(
            $DestinationPath,
            [System.IO.FileMode]::CreateNew,
            [System.IO.FileAccess]::Write,
            [System.IO.FileShare]::None,
            65536,
            [System.IO.FileOptions]::SequentialScan)
        $source.Stream.Position = 0
        $source.Stream.CopyTo($destination, 65536)
        $destination.Flush($true)
        $destination.Dispose()
        $destination = $null
        Assert-M01AuthorityRecordUnchanged -Record $source
        Assert-M01NoReparsePoints `
            -RootPath $DestinationRootPath `
            -Path $DestinationPath `
            -Reason "output_destination_authority"
    }
    finally {
        if ($null -ne $destination) {
            $destination.Dispose()
        }
        $source.Stream.Dispose()
    }
}

function Write-M01Utf8CreateNew {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,
        [Parameter(Mandatory = $true)]
        [string] $RootPath,
        [Parameter(Mandatory = $true)]
        [string] $Text
    )

    Assert-M01NoReparsePoints `
        -RootPath $RootPath `
        -Path $Path `
        -Reason "output_evidence_authority" `
        -AllowMissingLeaf
    $stream = New-Object System.IO.FileStream(
        $Path,
        [System.IO.FileMode]::CreateNew,
        [System.IO.FileAccess]::Write,
        [System.IO.FileShare]::None)
    try {
        $bytes = (New-Object System.Text.UTF8Encoding($false)).GetBytes($Text)
        $stream.Write($bytes, 0, $bytes.Length)
        $stream.Flush($true)
    }
    finally {
        $stream.Dispose()
    }
}

function Open-M01VerifierInputLocks {
    param(
        [Parameter(Mandatory = $true)]
        [object[]] $Inputs
    )

    $records = New-Object System.Collections.Generic.List[object]
    try {
        foreach ($input in $Inputs) {
            $records.Add((Open-M01AuthorityReadRecord `
                    -Path ([string] $input.Path) `
                    -RootPath ([string] $input.RootPath) `
                    -Purpose ([string] $input.Purpose) `
                    -FailurePrefix "verifier_input_authority" `
                    -RequireSingleLink))
        }
        Assert-M01DistinctAuthorityRecords -Records $records.ToArray()
        return ,$records
    }
    catch {
        Close-M01AuthorityRecords -Records $records
        throw
    }
}

function Assert-M01VerifierInputLocks {
    param(
        [Parameter(Mandatory = $true)]
        [object] $Locks
    )

    foreach ($record in $Locks) {
        Assert-M01AuthorityRecordUnchanged -Record $record
    }
    Assert-M01DistinctAuthorityRecords -Records $Locks.ToArray()
}

function Get-M01PublishSourceSnapshot {
    param(
        [string] $RepositoryRoot,
        [string] $ProjectPath
    )

    $repositoryFullPath = [System.IO.Path]::GetFullPath($RepositoryRoot)
    $repositoryPrefix = $repositoryFullPath.TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar) +
        [System.IO.Path]::DirectorySeparatorChar
    $projectDirectory = Split-Path -Parent $ProjectPath
    $candidatePaths = New-Object System.Collections.Generic.List[string]
    foreach ($relativePath in @(
            "CLROS.slnx",
            "global.json",
            "Directory.Build.props",
            "Directory.Build.targets",
            "scripts\build-efi-shell.ps1",
            "scripts\test-efi-artifact.ps1",
            "src\OS\OS.csproj",
            "tools\Directory.Build.props",
            "tools\OS.X64TextSerializerGenerator\OS.X64TextSerializerGenerator.targets",
            "tools\OS.X64TextSerializerGenerator\OS.X64TextSerializerGenerator.csproj",
            "tools\OS.X64TextSerializerGenerator\Program.cs",
            "tools\OS.X64TextSerializerGenerator\Amd64CoffWriter.cs",
            "tools\OS.X64TextSerializerGenerator\CheckedReferenceBarrierThunkGenerator.cs",
            "tools\OS.X64TextSerializerGenerator\InstructionPublicationSerializerGenerator.cs",
            "tools\OS.X64TextSerializerGenerator\InterfaceDispatchThunkGenerator.cs",
            "tools\OS.X64TextSerializerGenerator\ExceptionHandlingThunkGenerator.cs"
        )) {
        $candidatePaths.Add(
            [System.IO.Path]::GetFullPath(
                (Join-Path $repositoryFullPath $relativePath)))
    }
    foreach ($compileKey in $script:ExpectedEfiCompileItems) {
        $separatorIndex = $compileKey.IndexOf(
            "|",
            [System.StringComparison]::Ordinal)
        if ($separatorIndex -le 0) {
            throw "invalid expected EFI compile key: $compileKey"
        }
        $compileInclude = $compileKey.Substring(0, $separatorIndex)
        $candidatePaths.Add(
            [System.IO.Path]::GetFullPath(
                (Join-Path $projectDirectory $compileInclude)))
    }

    $relativePaths = New-Object System.Collections.Generic.List[string]
    $seenFullPaths = New-Object 'System.Collections.Generic.HashSet[string]' (
        [System.StringComparer]::OrdinalIgnoreCase)
    foreach ($candidatePath in $candidatePaths) {
        if (-not $seenFullPaths.Add($candidatePath)) {
            continue
        }
        if (-not (Test-Path -LiteralPath $candidatePath -PathType Leaf)) {
            throw "source snapshot input missing: $candidatePath"
        }

        if (-not $candidatePath.StartsWith(
                $repositoryPrefix,
                [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "source snapshot input outside repository: $candidatePath"
        }
        $relativePath = $candidatePath.Substring(
            $repositoryPrefix.Length).Replace("\", "/")
        $relativePaths.Add($relativePath)
    }

    $relativePathArray = $relativePaths.ToArray()
    [System.Array]::Sort(
        $relativePathArray,
        [System.StringComparer]::Ordinal)
    $inputs = New-Object System.Collections.Generic.List[object]
    $aggregateBuilder = New-Object System.Text.StringBuilder
    $authorityBuilder = New-Object System.Text.StringBuilder
    $authorityRecords = New-Object System.Collections.Generic.List[object]
    try {
        foreach ($relativePath in $relativePathArray) {
            $fullPath = [System.IO.Path]::GetFullPath(
                (Join-Path $repositoryFullPath $relativePath))
            $record = Open-M01AuthorityReadRecord `
                -Path $fullPath `
                -RootPath $repositoryFullPath `
                -Purpose ("source:" + $relativePath) `
                -FailurePrefix "source_authority" `
                -RequireSingleLink
            $authorityRecords.Add($record)
            $hash = [string] $record.InitialSha256
            $inputs.Add([ordered] @{
                    path = $relativePath
                    sha256 = $hash
                })
            [void] $aggregateBuilder.Append($relativePath)
            [void] $aggregateBuilder.Append("|")
            [void] $aggregateBuilder.Append($hash)
            [void] $aggregateBuilder.Append("`n")
            [void] $authorityBuilder.Append($relativePath)
            [void] $authorityBuilder.Append("|")
            [void] $authorityBuilder.Append($hash)
            [void] $authorityBuilder.Append("|")
            [void] $authorityBuilder.Append($record.InitialIdentityKey)
            [void] $authorityBuilder.Append("|")
            [void] $authorityBuilder.Append($record.InitialLinkCount)
            [void] $authorityBuilder.Append("`n")
        }
        Assert-M01DistinctAuthorityRecords -Records $authorityRecords.ToArray()
    }
    finally {
        Close-M01AuthorityRecords -Records $authorityRecords
    }

    $aggregateBytes = [System.Text.Encoding]::UTF8.GetBytes(
        $aggregateBuilder.ToString())
    $aggregateAlgorithm = [System.Security.Cryptography.SHA256]::Create()
    try {
        $aggregateHash = ([System.BitConverter]::ToString(
                $aggregateAlgorithm.ComputeHash($aggregateBytes))).Replace(
            "-",
            "").ToLowerInvariant()
    }
    finally {
        $aggregateAlgorithm.Dispose()
    }
    $authorityAlgorithm = [System.Security.Cryptography.SHA256]::Create()
    try {
        $authorityHash = ([System.BitConverter]::ToString(
                $authorityAlgorithm.ComputeHash(
                    [System.Text.Encoding]::UTF8.GetBytes(
                        $authorityBuilder.ToString())))).Replace(
            "-",
            "").ToLowerInvariant()
    }
    finally {
        $authorityAlgorithm.Dispose()
    }

    return [pscustomobject] @{
        Inputs = $inputs.ToArray()
        AggregateSha256 = $aggregateHash
        AuthoritySha256 = $authorityHash
        InputCount = $inputs.Count
    }
}

function Get-M01ExactFileSnapshot {
    param(
        [string] $RepositoryRoot,
        [string[]] $RelativePaths
    )

    $repositoryFullPath = [System.IO.Path]::GetFullPath($RepositoryRoot)
    $repositoryPrefix = $repositoryFullPath.TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar) +
        [System.IO.Path]::DirectorySeparatorChar
    $normalizedPaths = New-Object System.Collections.Generic.List[string]
    $seenPaths = New-Object 'System.Collections.Generic.HashSet[string]' (
        [System.StringComparer]::OrdinalIgnoreCase)
    foreach ($relativePath in $RelativePaths) {
        if ([string]::IsNullOrWhiteSpace($relativePath) -or
            [System.IO.Path]::IsPathRooted($relativePath)) {
            throw "exact snapshot relative path invalid"
        }
        $fullPath = [System.IO.Path]::GetFullPath(
            (Join-Path $repositoryFullPath $relativePath))
        if (-not $fullPath.StartsWith(
                $repositoryPrefix,
                [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "exact snapshot input outside repository: $fullPath"
        }
        if (-not $seenPaths.Add($fullPath)) {
            throw "exact snapshot input duplicated: $fullPath"
        }
        if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
            throw "exact snapshot input missing: $fullPath"
        }
        $normalizedPaths.Add(
            $fullPath.Substring($repositoryPrefix.Length).Replace("\", "/"))
    }

    $pathArray = $normalizedPaths.ToArray()
    [System.Array]::Sort($pathArray, [System.StringComparer]::Ordinal)
    $inputs = New-Object System.Collections.Generic.List[object]
    $aggregateBuilder = New-Object System.Text.StringBuilder
    foreach ($relativePath in $pathArray) {
        $fullPath = [System.IO.Path]::GetFullPath(
            (Join-Path $repositoryFullPath $relativePath))
        $hash = (
            Get-FileHash -LiteralPath $fullPath -Algorithm SHA256
        ).Hash.ToLowerInvariant()
        $inputs.Add([ordered] @{
                path = $relativePath
                sha256 = $hash
            })
        [void] $aggregateBuilder.Append($relativePath)
        [void] $aggregateBuilder.Append("|")
        [void] $aggregateBuilder.Append($hash)
        [void] $aggregateBuilder.Append("`n")
    }

    $aggregateAlgorithm = [System.Security.Cryptography.SHA256]::Create()
    try {
        $aggregateHash = ([System.BitConverter]::ToString(
                $aggregateAlgorithm.ComputeHash(
                    [System.Text.Encoding]::UTF8.GetBytes(
                        $aggregateBuilder.ToString())))).Replace(
            "-",
            "").ToLowerInvariant()
    }
    finally {
        $aggregateAlgorithm.Dispose()
    }

    return [pscustomobject] @{
        Inputs = $inputs.ToArray()
        AggregateSha256 = $aggregateHash
    }
}

function Get-M01ExactRestoreClosureSnapshot {
    param(
        [string] $RepositoryRoot,
        [string] $ProjectExtensionsDirectory,
        [string[]] $ExpectedRelativePaths
    )

    $repositoryFullPath = [System.IO.Path]::GetFullPath($RepositoryRoot)
    $repositoryPrefix = $repositoryFullPath.TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar) +
        [System.IO.Path]::DirectorySeparatorChar
    $extensionsFullPath = [System.IO.Path]::GetFullPath(
        $ProjectExtensionsDirectory)
    if (-not $extensionsFullPath.StartsWith(
            $repositoryPrefix,
            [System.StringComparison]::OrdinalIgnoreCase) -or
        -not (Test-Path -LiteralPath $extensionsFullPath -PathType Container)) {
        throw "restore project-extensions directory is not canonical"
    }
    $extensionsItem = Get-Item -LiteralPath $extensionsFullPath -Force
    if (($extensionsItem.Attributes -band
            [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "restore project-extensions directory is a reparse point"
    }

    $actualEntries = @(
        Get-ChildItem -LiteralPath $extensionsFullPath -Force)
    if ($actualEntries.Count -ne $ExpectedRelativePaths.Count -or
        @($actualEntries | Where-Object { $_.PSIsContainer }).Count -ne 0) {
        throw (
            "restore closure entry count mismatch: expected=" +
            $ExpectedRelativePaths.Count +
            " actual=" +
            $actualEntries.Count)
    }

    $expectedPaths = @($ExpectedRelativePaths)
    [System.Array]::Sort($expectedPaths, [System.StringComparer]::Ordinal)
    $actualPaths = @(
        $actualEntries |
            ForEach-Object {
                if (($_.Attributes -band
                        [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                    throw "restore closure contains a reparse point: $($_.FullName)"
                }
                $_.FullName.Substring($repositoryPrefix.Length).Replace("\", "/")
            })
    [System.Array]::Sort($actualPaths, [System.StringComparer]::Ordinal)
    for ($index = 0; $index -lt $expectedPaths.Count; $index++) {
        if ($actualPaths[$index] -cne $expectedPaths[$index]) {
            throw (
                "restore closure input mismatch: expected=" +
                $expectedPaths[$index] +
                " actual=" +
                $actualPaths[$index])
        }
    }

    return Get-M01ExactFileSnapshot `
        -RepositoryRoot $repositoryFullPath `
        -RelativePaths $expectedPaths
}

function Test-M01ExactPropertyNameSet {
    param(
        [AllowNull()]
        [object] $Value,
        [string[]] $ExpectedNames
    )

    if ($null -eq $Value) {
        return $false
    }
    $actualNames = @(
        $Value.PSObject.Properties |
            ForEach-Object { [string] $_.Name })
    $expected = @($ExpectedNames)
    [System.Array]::Sort($actualNames, [System.StringComparer]::Ordinal)
    [System.Array]::Sort($expected, [System.StringComparer]::Ordinal)
    if ($actualNames.Count -ne $expected.Count) {
        return $false
    }
    for ($index = 0; $index -lt $expected.Count; $index++) {
        if ($actualNames[$index] -cne $expected[$index]) {
            return $false
        }
    }
    return $true
}

function Get-M01Utf8Sha256 {
    param([AllowEmptyString()][string] $Text)

    $algorithm = [System.Security.Cryptography.SHA256]::Create()
    try {
        return ([System.BitConverter]::ToString(
                $algorithm.ComputeHash(
                    [System.Text.Encoding]::UTF8.GetBytes($Text)))).Replace(
            "-",
            "").ToLowerInvariant()
    }
    finally {
        $algorithm.Dispose()
    }
}

function Get-M01MsbuildIsolationContract {
    param(
        [Parameter(Mandatory = $true)]
        [string] $ProjectExtensionsDirectory
    )

    if ([string]::IsNullOrWhiteSpace($script:VerifiedDotnetSdkDirectory) -or
        [string]::IsNullOrWhiteSpace($script:VerifiedMsbuildSdksPath) -or
        [string]::IsNullOrWhiteSpace($script:VerifiedMsbuildExtensionsPath)) {
        throw "verified MSBuild SDK isolation paths are unavailable"
    }

    $projectExtensionsFullPath = [System.IO.Path]::GetFullPath(
        $ProjectExtensionsDirectory.TrimEnd(
            [System.IO.Path]::DirectorySeparatorChar,
            [System.IO.Path]::AltDirectorySeparatorChar))
    $disabledUserExtensionsPath = Join-Path (
        $projectExtensionsFullPath) "disabled-user-extensions"
    if (Test-Path -LiteralPath $disabledUserExtensionsPath) {
        throw (
            "disabled MSBuild user extensions path must remain absent: " +
            $disabledUserExtensionsPath)
    }

    $properties = [ordered] @{
        MSBuildSDKsPath = $script:VerifiedMsbuildSdksPath
        MSBuildExtensionsPath = $script:VerifiedMsbuildExtensionsPath
        MSBuildUserExtensionsPath = $disabledUserExtensionsPath
        ImportByWildcardBeforeMicrosoftCommonProps = "false"
        ImportByWildcardAfterMicrosoftCommonProps = "false"
        ImportUserLocationsByWildcardBeforeMicrosoftCommonProps = "false"
        ImportUserLocationsByWildcardAfterMicrosoftCommonProps = "false"
        ImportByWildcardBeforeMicrosoftCommonTargets = "false"
        ImportByWildcardAfterMicrosoftCommonTargets = "false"
        ImportUserLocationsByWildcardBeforeMicrosoftCommonTargets = "false"
        ImportUserLocationsByWildcardAfterMicrosoftCommonTargets = "false"
        ImportByWildcardBeforeMicrosoftCSharpTargets = "false"
        ImportByWildcardAfterMicrosoftCSharpTargets = "false"
        ImportUserLocationsByWildcardBeforeMicrosoftCSharpTargets = "false"
        ImportUserLocationsByWildcardAfterMicrosoftCSharpTargets = "false"
        ImportByWildcardBeforeMicrosoftCommonCrossTargetingTargets = "false"
        ImportByWildcardAfterMicrosoftCommonCrossTargetingTargets = "false"
        UseSharedCompilation = "false"
    }
    $arguments = New-Object System.Collections.Generic.List[string]
    $arguments.Add("-nodeReuse:false")
    foreach ($entry in $properties.GetEnumerator()) {
        if ($entry.Key -ceq "MSBuildExtensionsPath") {
            # A quoted Windows native-process argument ending in "\" can
            # consume the following argument. Preserve the required canonical
            # trailing separator through the child environment instead.
            continue
        }
        $arguments.Add("-p:" + $entry.Key + "=" + [string] $entry.Value)
    }

    $environment = @{
        MSBuildSDKsPath = $script:VerifiedMsbuildSdksPath
        MSBuildExtensionsPath = $script:VerifiedMsbuildExtensionsPath
        MSBuildUserExtensionsPath = $disabledUserExtensionsPath
    }

    return [pscustomobject] @{
        ProjectExtensionsDirectory = $projectExtensionsFullPath
        DisabledUserExtensionsPath = $disabledUserExtensionsPath
        Properties = $properties
        Arguments = $arguments.ToArray()
        Environment = $environment
    }
}

function Invoke-M01PostRestoreEvaluation {
    param(
        [string] $RepositoryRoot,
        [string] $ProjectPath,
        [string] $Configuration,
        [string] $Runtime,
        [string] $ProjectExtensionsDirectory,
        [string] $ProjectAssetsPath,
        [string] $RestorePackagesRoot,
        [string] $IlCompilerTargetsPath,
        [string] $IlLinkTargetsPath,
        [string] $IlLinkTasksAssembly,
        [string] $IlcBuildTasksPath,
        [string] $IlcHostPackagePath,
        [string] $IlcToolsPath,
        [string] $RuntimePackagePath,
        [string] $IlcFrameworkPath,
        [string] $IlcFrameworkNativePath,
        [string] $IlcSdkPath,
        [string] $IlcMibcPath,
        [string] $LanguageTargetsPath,
        [string] $CSharpTargetsPath,
        [string] $CommonTargetsPath,
        [bool] $M02OwnedCom1ReadyEnabled,
        [bool] $M02PanicSafeOwnedCom1Enabled,
        [bool] $M02OwnedX64GdtEncodingEnabled,
        [bool] $M02GuardedIstStackAllocationEnabled,
        [bool] $M02OwnedGdtTssTerminalEnabled,
        [bool] $M02NormalizedInterruptFramePageEnabled,
        [bool] $M02OwnedIdtGatePopulationEnabled,
        [bool] $M02IdtrContinuedExecutionEnabled,
        [bool] $M02Ud2VectorStateEnabled,
        [bool] $M02PageFaultCr2ErrorCodeEnabled,
        [bool] $M02DoubleFaultIstEnabled,
        [bool] $M02UnexpectedVectorPanicEnabled,
        [bool] $M04IndependentEndianConformanceEnabled,
        [bool] $M04SpanMemoryConformanceEnabled,
        [bool] $M04ArithmeticConformanceEnabled,
        [bool] $M04LayoutContractEnabled,
        [bool] $M04ManagedHeapPageRangeEnabled,
        [bool] $M04RhpNewFastEnabled,
        [bool] $M04RhpNewFastBeforeBindFailureEnabled,
        [bool] $M04RhpNewFastExhaustionFailureEnabled,
        [bool] $M04SzArrayAllocationEnabled,
        [bool] $M04SzArrayAllocationBeforeBindEnabled,
        [bool] $M04SzArrayAllocationExhaustionEnabled,
        [bool] $M04StringAllocationEnabled,
        [bool] $M04StringAllocationBeforeBindEnabled,
        [bool] $M04StringAllocationExhaustionEnabled,
        [bool] $M04StringAllocationInvalidLengthEnabled,
        [bool] $M03MemoryMapCaptureEnabled,
        [bool] $M03FirmwareEntropySeedEnabled,
        [bool] $M04TypeManagerModuleRegistrationEnabled,
        [bool] $M04X64UnwindMetadataEnabled,
        [bool] $M04SimpleThrowCatchEnabled,
        [bool] $M04GcStaticRootMaterializationEnabled,
        [bool] $M04SeededCollectorChurnEnabled,
        [bool] $M03TypedExitBootServicesCallEnabled,
        [bool] $M03FirstRealExitBootServicesEnabled,
        [bool] $M03FirmwareAdapterRevocationEnabled,
        [bool] $M03OwnedSubsystemSurvivalEnabled,
        [bool] $M03OwnedSubsystemPanicEnabled,
        [bool] $M04ClassConstructorExecutionEnabled,
        [bool] $M04InterfaceDispatchEnabled,
        [bool] $M04CheckedReferenceBarrierEnabled,
        [bool] $LoadedImageStackEnabled,
        [bool] $LiveAllocatorEnabled,
        [bool] $OwnedPagingEnabled,
        [bool] $M02SafeStubEnabled,
        [bool] $M02CheckedPortIoBindingEnabled,
        [bool] $M02Com1OwnedPortIoEnabled,
        [bool] $UefiGoneSimulationEnabled,
        [bool] $ExitBootServicesFreshMapEnabled,
        [bool] $AcpiPhysicalDiscoveryEnabled
    )

    $canonicalRootPropsPath = Join-Path $RepositoryRoot "Directory.Build.props"
    $canonicalRootTargetsPath = Join-Path $RepositoryRoot "Directory.Build.targets"
    $msbuildIsolation = Get-M01MsbuildIsolationContract `
        -ProjectExtensionsDirectory $ProjectExtensionsDirectory
    $arguments = @(
        "msbuild",
        $ProjectPath,
        "-noAutoResponse",
        "-nologo",
        "-getItem:Compile,ProjectReference,Reference,Analyzer",
        (
            "-getProperty:" +
            "_targetOS,_targetArchitecture,RuntimeIdentifier," +
            "TargetFramework,PublishAot,SelfContained,PublishTrimmed," +
            "EnableM02OwnedCom1ReadyProbe," +
            "EnableM02PanicSafeOwnedCom1Probe," +
            "EnableM02OwnedX64GdtEncodingProbe," +
            "EnableM02GuardedIstStackAllocationProbe," +
            "EnableM02OwnedGdtTssTerminalProbe," +
            "EnableM02NormalizedInterruptFramePageProbe," +
            "EnableM02OwnedIdtGatePopulationProbe," +
            "EnableM02IdtrContinuedExecutionProbe," +
            "EnableM02Ud2VectorStateProbe," +
            "EnableM02PageFaultCr2ErrorCodeProbe," +
            "EnableM02DoubleFaultIstProbe," +
            "EnableM02UnexpectedVectorPanicProbe," +
            "EnableM04IndependentEndianConformanceProbe," +
            "EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe," +
            "EnableM04ArithmeticConformanceProbe," +
            "EnableM04LayoutContractProbe," +
            "EnableM04ManagedHeapPageRangeProbe," +
            "EnableM04RhpNewFastProbe," +
            "EnableM04RhpNewFastBeforeBindFailureProbe," +
            "EnableM04RhpNewFastExhaustionFailureProbe," +
            "EnableM04SzArrayAllocationProbe," +
            "EnableM04SzArrayAllocationBeforeBindProbe," +
            "EnableM04SzArrayAllocationExhaustionProbe," +
            "EnableM04StringAllocationProbe," +
            "EnableM04StringAllocationBeforeBindProbe," +
            "EnableM04StringAllocationExhaustionProbe," +
            "EnableM04StringAllocationInvalidLengthProbe," +
            "EnableM04CheckedReferenceBarrierProbe," +
            "EnableM03MemoryMapCaptureProbe," +
            "EnableM03FirmwareEntropySeedProbe," +
            "EnableM04TypeManagerModuleRegistrationProbe," +
            "EnableM04X64UnwindMetadataProbe," +
            "EnableM04SimpleThrowCatchProbe," +
            "EnableM04GcStaticRootMaterializationProbe," +
            "EnableM04SeededCollectorChurnProbe," +
            "EnableM03TypedExitBootServicesCallProbe," +
            "EnableM03FirstRealExitBootServicesProbe," +
            "EnableM03FirmwareAdapterRevocationProbe," +
            "EnableM03OwnedSubsystemSurvivalProbe," +
            "EnableM03OwnedSubsystemPanicProbe," +
            "EnableM04ClassConstructorExecutionProbe," +
            "EnableM04InterfaceDispatchProbe," +
            "EnableM01LoadedImageStackProbe," +
            "EnableM01LiveAllocatorProbe," +
            "EnableM01OwnedPagingProbe," +
            "EnableM02SafeStubProbe," +
            "EnableM02CheckedPortIoBindingProbe," +
            "EnableM02Com1OwnedPortIoProbe," +
            "EnableUefiGoneSimulationProbe," +
            "EnableExitBootServicesFreshMapProbe," +
            "EnableAcpiPhysicalDiscoveryProbe," +
            "ImportProjectExtensionProps,ImportProjectExtensionTargets," +
            "ExcludeRestorePackageImports,ILCompilerTargetsPath," +
            "ILLinkTargetsPath,ILLinkTasksAssembly,IlcBuildTasksPath," +
            "IlcHostPackagePath,IlcToolsPath,RuntimePackagePath," +
            "IlcFrameworkPath,IlcFrameworkNativePath,IlcSdkPath," +
            "IlcMibcPath,RunILLink,NativeCompilationDuringPublish," +
            "IlcMultiModule,DisableFrameworkLibGeneration," +
            "IlcUseEnvironmentalTools,PublishAotUsingRuntimePack," +
            "IlcPgoOptimize,ClrosIlcHostPackagePath," +
            "ClrosRuntimePackagePath,ImportRuntimeIlcPackageTargetDependsOn," +
            "IlcSetupPropertiesDependsOn,IlcDynamicBuildPropertyDependencies," +
            "ImportNuGetBuildTasksPackTargetsFromSdk," +
            "NuGetBuildTasksPackTargets,LanguageTargets,CSharpTargetsPath," +
            "CommonTargetsPath,MSBuildSDKsPath,MSBuildExtensionsPath," +
            "MSBuildUserExtensionsPath," +
            "ImportByWildcardBeforeMicrosoftCommonProps," +
            "ImportByWildcardAfterMicrosoftCommonProps," +
            "ImportUserLocationsByWildcardBeforeMicrosoftCommonProps," +
            "ImportUserLocationsByWildcardAfterMicrosoftCommonProps," +
            "ImportByWildcardBeforeMicrosoftCommonTargets," +
            "ImportByWildcardAfterMicrosoftCommonTargets," +
            "ImportUserLocationsByWildcardBeforeMicrosoftCommonTargets," +
            "ImportUserLocationsByWildcardAfterMicrosoftCommonTargets," +
            "ImportByWildcardBeforeMicrosoftCSharpTargets," +
            "ImportByWildcardAfterMicrosoftCSharpTargets," +
            "ImportUserLocationsByWildcardBeforeMicrosoftCSharpTargets," +
            "ImportUserLocationsByWildcardAfterMicrosoftCSharpTargets," +
            "ImportByWildcardBeforeMicrosoftCommonCrossTargetingTargets," +
            "ImportByWildcardAfterMicrosoftCommonCrossTargetingTargets," +
            "UseSharedCompilation"
        ),
        ("-p:Configuration=" + $Configuration),
        ("-p:RuntimeIdentifier=" + $Runtime),
        ("-p:DirectoryBuildPropsPath=" + $canonicalRootPropsPath),
        ("-p:DirectoryBuildTargetsPath=" + $canonicalRootTargetsPath),
        ("-p:MSBuildProjectExtensionsPath=" +
            $ProjectExtensionsDirectory.Replace("\", "/").TrimEnd("/") + "/"),
        ("-p:ProjectAssetsFile=" + $ProjectAssetsPath.Replace("\", "/")),
        ("-p:RestorePackagesPath=" + $RestorePackagesRoot),
        ("-p:NuGetPackageRoot=" + $RestorePackagesRoot),
        "-p:ImportDirectoryBuildProps=true",
        "-p:ImportDirectoryBuildTargets=true",
        "-p:ImportProjectExtensionProps=false",
        "-p:ImportProjectExtensionTargets=false",
        "-p:ExcludeRestorePackageImports=true",
        "-p:CustomBeforeMicrosoftCommonProps=",
        "-p:CustomAfterMicrosoftCommonProps=",
        "-p:CustomBeforeDirectoryBuildProps=",
        "-p:CustomAfterDirectoryBuildProps=",
        "-p:CustomBeforeDirectoryBuildTargets=",
        "-p:CustomAfterDirectoryBuildTargets=",
        "-p:CustomBeforeMicrosoftCommonTargets=",
        "-p:CustomAfterMicrosoftCommonTargets=",
        "-p:CustomBeforeMicrosoftCSharpTargets=",
        "-p:CustomAfterMicrosoftCSharpTargets=",
        "-p:CustomBeforeMicrosoftCommonCrossTargetingTargets=",
        "-p:CustomAfterMicrosoftCommonCrossTargetingTargets=",
        "-p:BeforeMicrosoftNETSdkTargets=",
        "-p:AfterMicrosoftNETSdkTargets=",
        ("-p:ILCompilerTargetsPath=" + $IlCompilerTargetsPath),
        ("-p:ILLinkTargetsPath=" + $IlLinkTargetsPath),
        ("-p:ILLinkTasksAssembly=" + $IlLinkTasksAssembly),
        ("-p:IlcBuildTasksPath=" + $IlcBuildTasksPath),
        ("-p:IlcHostPackagePath=" + $IlcHostPackagePath),
        ("-p:ClrosIlcHostPackagePath=" + $IlcHostPackagePath),
        ("-p:IlcToolsPath=" + $IlcToolsPath),
        ("-p:RuntimePackagePath=" + $RuntimePackagePath),
        ("-p:ClrosRuntimePackagePath=" + $RuntimePackagePath),
        ("-p:IlcFrameworkPath=" + $IlcFrameworkPath),
        ("-p:IlcFrameworkNativePath=" + $IlcFrameworkNativePath),
        ("-p:IlcSdkPath=" + $IlcSdkPath),
        ("-p:IlcMibcPath=" + $IlcMibcPath),
        "-p:RunILLink=false",
        "-p:NativeCompilationDuringPublish=true",
        "-p:IlcMultiModule=false",
        "-p:DisableFrameworkLibGeneration=true",
        "-p:IlcUseEnvironmentalTools=true",
        "-p:PublishAotUsingRuntimePack=false",
        "-p:IlcPgoOptimize=false",
        "-p:PublishTrimmed=true",
        "-p:ImportRuntimeIlcPackageTargetDependsOn=",
        "-p:IlcSetupPropertiesDependsOn=ImportRuntimeIlcPackageTarget",
        "-p:IlcDynamicBuildPropertyDependencies=SetupProperties",
        "-p:ImportNuGetBuildTasksPackTargetsFromSdk=false",
        "-p:NuGetBuildTasksPackTargets=",
        ("-p:LanguageTargets=" + $LanguageTargetsPath),
        ("-p:CSharpTargetsPath=" + $CSharpTargetsPath),
        ("-p:CommonTargetsPath=" + $CommonTargetsPath)
    )
    $arguments += (
        "-p:EnableM02OwnedCom1ReadyProbe=" +
        $M02OwnedCom1ReadyEnabled.ToString().ToLowerInvariant())
    $arguments += (
        "-p:EnableM02PanicSafeOwnedCom1Probe=" +
        $M02PanicSafeOwnedCom1Enabled.ToString().ToLowerInvariant())
    $arguments += (
        "-p:EnableM02OwnedX64GdtEncodingProbe=" +
        $M02OwnedX64GdtEncodingEnabled.ToString().ToLowerInvariant())
    $arguments += (
        "-p:EnableM02GuardedIstStackAllocationProbe=" +
        $M02GuardedIstStackAllocationEnabled.ToString().ToLowerInvariant())
    $arguments += (
        "-p:EnableM02OwnedGdtTssTerminalProbe=" +
        $M02OwnedGdtTssTerminalEnabled.ToString().ToLowerInvariant())
    $arguments += (
        "-p:EnableM02NormalizedInterruptFramePageProbe=" +
        $M02NormalizedInterruptFramePageEnabled.ToString().ToLowerInvariant())
    $arguments += (
        "-p:EnableM02OwnedIdtGatePopulationProbe=" +
        $M02OwnedIdtGatePopulationEnabled.ToString().ToLowerInvariant())
    $arguments += (
        "-p:EnableM02IdtrContinuedExecutionProbe=" +
        $M02IdtrContinuedExecutionEnabled.ToString().ToLowerInvariant())
    $arguments += (
        "-p:EnableM02Ud2VectorStateProbe=" +
        $M02Ud2VectorStateEnabled.ToString().ToLowerInvariant())
    $arguments += (
        "-p:EnableM02PageFaultCr2ErrorCodeProbe=" +
        $M02PageFaultCr2ErrorCodeEnabled.ToString().ToLowerInvariant())
    $arguments += (
        "-p:EnableM02DoubleFaultIstProbe=" +
        $M02DoubleFaultIstEnabled.ToString().ToLowerInvariant())
    $arguments += (
        "-p:EnableM02UnexpectedVectorPanicProbe=" +
        $M02UnexpectedVectorPanicEnabled.ToString().ToLowerInvariant())
    $arguments += (
        "-p:EnableM04IndependentEndianConformanceProbe=" +
        $M04IndependentEndianConformanceEnabled.ToString().ToLowerInvariant())
    $arguments += (
        "-p:EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe=" +
        $M04SpanMemoryConformanceEnabled.ToString().ToLowerInvariant())
    $arguments += (
        "-p:EnableM04ArithmeticConformanceProbe=" +
        $M04ArithmeticConformanceEnabled.ToString().ToLowerInvariant())
    $arguments += (
        "-p:EnableM04LayoutContractProbe=" +
        $M04LayoutContractEnabled.ToString().ToLowerInvariant())
    $arguments += ("-p:EnableM04ManagedHeapPageRangeProbe=" +
        $M04ManagedHeapPageRangeEnabled.ToString().ToLowerInvariant())
    $arguments += ("-p:EnableM04RhpNewFastProbe=" +
        $M04RhpNewFastEnabled.ToString().ToLowerInvariant())
    $arguments += ("-p:EnableM04RhpNewFastBeforeBindFailureProbe=" +
        $M04RhpNewFastBeforeBindFailureEnabled.ToString().ToLowerInvariant())
    $arguments += ("-p:EnableM04RhpNewFastExhaustionFailureProbe=" +
        $M04RhpNewFastExhaustionFailureEnabled.ToString().ToLowerInvariant())
    $arguments += ("-p:EnableM04SzArrayAllocationProbe=" +
        $M04SzArrayAllocationEnabled.ToString().ToLowerInvariant())
    $arguments += ("-p:EnableM04SzArrayAllocationBeforeBindProbe=" +
        $M04SzArrayAllocationBeforeBindEnabled.ToString().ToLowerInvariant())
    $arguments += ("-p:EnableM04SzArrayAllocationExhaustionProbe=" +
        $M04SzArrayAllocationExhaustionEnabled.ToString().ToLowerInvariant())
    $arguments += ("-p:EnableM04StringAllocationProbe=" +
        $M04StringAllocationEnabled.ToString().ToLowerInvariant())
    $arguments += ("-p:EnableM04StringAllocationBeforeBindProbe=" +
        $M04StringAllocationBeforeBindEnabled.ToString().ToLowerInvariant())
    $arguments += ("-p:EnableM04StringAllocationExhaustionProbe=" +
        $M04StringAllocationExhaustionEnabled.ToString().ToLowerInvariant())
    $arguments += ("-p:EnableM04StringAllocationInvalidLengthProbe=" +
        $M04StringAllocationInvalidLengthEnabled.ToString().ToLowerInvariant())
    $arguments += ("-p:EnableM03MemoryMapCaptureProbe=" +
        $M03MemoryMapCaptureEnabled.ToString().ToLowerInvariant())
    $arguments += ("-p:EnableM03FirmwareEntropySeedProbe=" +
        $M03FirmwareEntropySeedEnabled.ToString().ToLowerInvariant())
    $arguments += ("-p:EnableM04TypeManagerModuleRegistrationProbe=" +
        $M04TypeManagerModuleRegistrationEnabled.ToString().ToLowerInvariant())
    $arguments += ("-p:EnableM04X64UnwindMetadataProbe=" +
        $M04X64UnwindMetadataEnabled.ToString().ToLowerInvariant())
    $arguments += ("-p:EnableM04SimpleThrowCatchProbe=" +
        $M04SimpleThrowCatchEnabled.ToString().ToLowerInvariant())
    $arguments += ("-p:EnableM04GcStaticRootMaterializationProbe=" +
        $M04GcStaticRootMaterializationEnabled.ToString().ToLowerInvariant())
    $arguments += ("-p:EnableM04SeededCollectorChurnProbe=" +
        $M04SeededCollectorChurnEnabled.ToString().ToLowerInvariant())
    $arguments += ("-p:EnableM03TypedExitBootServicesCallProbe=" +
        $M03TypedExitBootServicesCallEnabled.ToString().ToLowerInvariant())
    $arguments += ("-p:EnableM03FirstRealExitBootServicesProbe=" +
        $M03FirstRealExitBootServicesEnabled.ToString().ToLowerInvariant())
    $arguments += ("-p:EnableM03FirmwareAdapterRevocationProbe=" +
        $M03FirmwareAdapterRevocationEnabled.ToString().ToLowerInvariant())
    $arguments += ("-p:EnableM03OwnedSubsystemSurvivalProbe=" +
        $M03OwnedSubsystemSurvivalEnabled.ToString().ToLowerInvariant())
    $arguments += ("-p:EnableM03OwnedSubsystemPanicProbe=" +
        $M03OwnedSubsystemPanicEnabled.ToString().ToLowerInvariant())
    $arguments += ("-p:EnableM04ClassConstructorExecutionProbe=" +
        $M04ClassConstructorExecutionEnabled.ToString().ToLowerInvariant())
    $arguments += ("-p:EnableM04InterfaceDispatchProbe=" +
        $M04InterfaceDispatchEnabled.ToString().ToLowerInvariant())
    $arguments += ("-p:EnableM04CheckedReferenceBarrierProbe=" +
        $M04CheckedReferenceBarrierEnabled.ToString().ToLowerInvariant())
    $arguments += ("-p:EnableM01LoadedImageStackProbe=" + $LoadedImageStackEnabled.ToString().ToLowerInvariant())
    $arguments += ("-p:EnableM01LiveAllocatorProbe=" + $LiveAllocatorEnabled.ToString().ToLowerInvariant())
    $arguments += ("-p:EnableM01OwnedPagingProbe=" + $OwnedPagingEnabled.ToString().ToLowerInvariant())
    $arguments += ("-p:EnableM02SafeStubProbe=" + $M02SafeStubEnabled.ToString().ToLowerInvariant())
    $arguments += ("-p:EnableM02CheckedPortIoBindingProbe=" + $M02CheckedPortIoBindingEnabled.ToString().ToLowerInvariant())
    $arguments += ("-p:EnableM02Com1OwnedPortIoProbe=" + $M02Com1OwnedPortIoEnabled.ToString().ToLowerInvariant())
    $arguments += ("-p:EnableUefiGoneSimulationProbe=" + $UefiGoneSimulationEnabled.ToString().ToLowerInvariant())
    $arguments += ("-p:EnableExitBootServicesFreshMapProbe=" + $ExitBootServicesFreshMapEnabled.ToString().ToLowerInvariant())
    $arguments += ("-p:EnableAcpiPhysicalDiscoveryProbe=" + $AcpiPhysicalDiscoveryEnabled.ToString().ToLowerInvariant())
    $arguments += $msbuildIsolation.Arguments
    $processResult = Invoke-M01BoundedProcess `
        -FilePath $script:DotnetHostPath `
        -Arguments $arguments `
        -WorkingDirectory $RepositoryRoot `
        -Operation "post-restore-evaluation" `
        -TimeoutSeconds $BuildProcessTimeoutSeconds `
        -MaximumOutputCharacters $BuildProcessMaximumOutputCharacters `
        -EnvironmentOverrides $msbuildIsolation.Environment
    $processFailure = Get-M01BoundedProcessFailureReason -Result $processResult
    if ($processFailure -cne "ok") {
        throw (
            "post-restore project evaluation failed: " +
            $processFailure +
            ";output=" +
            $processResult.OutputText)
    }
    $outputText = $processResult.OutputText
    $jsonStart = $outputText.IndexOf(
        "{",
        [System.StringComparison]::Ordinal)
    $jsonEnd = $outputText.LastIndexOf(
        "}",
        [System.StringComparison]::Ordinal)
    if ($jsonStart -lt 0 -or $jsonEnd -le $jsonStart) {
        throw "post-restore project evaluation malformed"
    }
    try {
        $evaluation = $outputText.Substring(
            $jsonStart,
            $jsonEnd - $jsonStart + 1) | ConvertFrom-Json
    }
    catch {
        throw "post-restore project evaluation JSON invalid"
    }

    $expectedProperties = [ordered] @{
        _targetOS = "win"
        _targetArchitecture = "x64"
        RuntimeIdentifier = $Runtime
        TargetFramework = "net10.0"
        PublishAot = "true"
        SelfContained = "true"
        PublishTrimmed = "true"
        EnableM02OwnedCom1ReadyProbe =
            $M02OwnedCom1ReadyEnabled.ToString().ToLowerInvariant()
        EnableM02PanicSafeOwnedCom1Probe =
            $M02PanicSafeOwnedCom1Enabled.ToString().ToLowerInvariant()
        EnableM02OwnedX64GdtEncodingProbe =
            $M02OwnedX64GdtEncodingEnabled.ToString().ToLowerInvariant()
        EnableM02GuardedIstStackAllocationProbe =
            $M02GuardedIstStackAllocationEnabled.ToString().ToLowerInvariant()
        EnableM02OwnedGdtTssTerminalProbe =
            $M02OwnedGdtTssTerminalEnabled.ToString().ToLowerInvariant()
        EnableM02NormalizedInterruptFramePageProbe =
            $M02NormalizedInterruptFramePageEnabled.ToString().ToLowerInvariant()
        EnableM02OwnedIdtGatePopulationProbe =
            $M02OwnedIdtGatePopulationEnabled.ToString().ToLowerInvariant()
        EnableM02IdtrContinuedExecutionProbe =
            $M02IdtrContinuedExecutionEnabled.ToString().ToLowerInvariant()
        EnableM02Ud2VectorStateProbe =
            $M02Ud2VectorStateEnabled.ToString().ToLowerInvariant()
        EnableM02PageFaultCr2ErrorCodeProbe =
            $M02PageFaultCr2ErrorCodeEnabled.ToString().ToLowerInvariant()
        EnableM02DoubleFaultIstProbe =
            $M02DoubleFaultIstEnabled.ToString().ToLowerInvariant()
        EnableM02UnexpectedVectorPanicProbe =
            $M02UnexpectedVectorPanicEnabled.ToString().ToLowerInvariant()
        EnableM04IndependentEndianConformanceProbe =
            $M04IndependentEndianConformanceEnabled.ToString().ToLowerInvariant()
        EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe =
            $M04SpanMemoryConformanceEnabled.ToString().ToLowerInvariant()
        EnableM04ArithmeticConformanceProbe =
            $M04ArithmeticConformanceEnabled.ToString().ToLowerInvariant()
        EnableM04LayoutContractProbe =
            $M04LayoutContractEnabled.ToString().ToLowerInvariant()
        EnableM04ManagedHeapPageRangeProbe =
            $M04ManagedHeapPageRangeEnabled.ToString().ToLowerInvariant()
        EnableM04RhpNewFastProbe =
            $M04RhpNewFastEnabled.ToString().ToLowerInvariant()
        EnableM04RhpNewFastBeforeBindFailureProbe =
            $M04RhpNewFastBeforeBindFailureEnabled.ToString().ToLowerInvariant()
        EnableM04RhpNewFastExhaustionFailureProbe =
            $M04RhpNewFastExhaustionFailureEnabled.ToString().ToLowerInvariant()
        EnableM04SzArrayAllocationProbe =
            $M04SzArrayAllocationEnabled.ToString().ToLowerInvariant()
        EnableM04SzArrayAllocationBeforeBindProbe =
            $M04SzArrayAllocationBeforeBindEnabled.ToString().ToLowerInvariant()
        EnableM04SzArrayAllocationExhaustionProbe =
            $M04SzArrayAllocationExhaustionEnabled.ToString().ToLowerInvariant()
        EnableM04StringAllocationProbe =
            $M04StringAllocationEnabled.ToString().ToLowerInvariant()
        EnableM04StringAllocationBeforeBindProbe =
            $M04StringAllocationBeforeBindEnabled.ToString().ToLowerInvariant()
        EnableM04StringAllocationExhaustionProbe =
            $M04StringAllocationExhaustionEnabled.ToString().ToLowerInvariant()
        EnableM04StringAllocationInvalidLengthProbe =
            $M04StringAllocationInvalidLengthEnabled.ToString().ToLowerInvariant()
        EnableM03MemoryMapCaptureProbe =
            $M03MemoryMapCaptureEnabled.ToString().ToLowerInvariant()
        EnableM03FirmwareEntropySeedProbe =
            $M03FirmwareEntropySeedEnabled.ToString().ToLowerInvariant()
        EnableM04TypeManagerModuleRegistrationProbe =
            $M04TypeManagerModuleRegistrationEnabled.ToString().ToLowerInvariant()
        EnableM04X64UnwindMetadataProbe =
            $M04X64UnwindMetadataEnabled.ToString().ToLowerInvariant()
        EnableM04SimpleThrowCatchProbe =
            $M04SimpleThrowCatchEnabled.ToString().ToLowerInvariant()
        EnableM04GcStaticRootMaterializationProbe =
            $M04GcStaticRootMaterializationEnabled.ToString().ToLowerInvariant()
        EnableM04SeededCollectorChurnProbe =
            $M04SeededCollectorChurnEnabled.ToString().ToLowerInvariant()
        EnableM03TypedExitBootServicesCallProbe =
            $M03TypedExitBootServicesCallEnabled.ToString().ToLowerInvariant()
        EnableM03FirstRealExitBootServicesProbe =
            $M03FirstRealExitBootServicesEnabled.ToString().ToLowerInvariant()
        EnableM03FirmwareAdapterRevocationProbe =
            $M03FirmwareAdapterRevocationEnabled.ToString().ToLowerInvariant()
        EnableM03OwnedSubsystemSurvivalProbe =
            $M03OwnedSubsystemSurvivalEnabled.ToString().ToLowerInvariant()
        EnableM03OwnedSubsystemPanicProbe =
            $M03OwnedSubsystemPanicEnabled.ToString().ToLowerInvariant()
        EnableM04ClassConstructorExecutionProbe =
            $M04ClassConstructorExecutionEnabled.ToString().ToLowerInvariant()
        EnableM04InterfaceDispatchProbe =
            $M04InterfaceDispatchEnabled.ToString().ToLowerInvariant()
        EnableM04CheckedReferenceBarrierProbe =
            $M04CheckedReferenceBarrierEnabled.ToString().ToLowerInvariant()
        ImportProjectExtensionProps = "false"
        ImportProjectExtensionTargets = "false"
        ExcludeRestorePackageImports = "true"
        ILCompilerTargetsPath = $IlCompilerTargetsPath
        ILLinkTargetsPath = $IlLinkTargetsPath
        ILLinkTasksAssembly = $IlLinkTasksAssembly
        IlcBuildTasksPath = $IlcBuildTasksPath
        IlcHostPackagePath = $IlcHostPackagePath
        ClrosIlcHostPackagePath = $IlcHostPackagePath
        IlcToolsPath = $IlcToolsPath
        RuntimePackagePath = $RuntimePackagePath
        ClrosRuntimePackagePath = $RuntimePackagePath
        IlcFrameworkPath = $IlcFrameworkPath
        IlcFrameworkNativePath = $IlcFrameworkNativePath
        IlcSdkPath = $IlcSdkPath
        IlcMibcPath = $IlcMibcPath
        RunILLink = "false"
        NativeCompilationDuringPublish = "true"
        IlcMultiModule = "false"
        DisableFrameworkLibGeneration = "true"
        IlcUseEnvironmentalTools = "true"
        PublishAotUsingRuntimePack = "false"
        IlcPgoOptimize = "false"
        ImportRuntimeIlcPackageTargetDependsOn = ""
        IlcSetupPropertiesDependsOn = "ImportRuntimeIlcPackageTarget"
        IlcDynamicBuildPropertyDependencies = "SetupProperties"
        ImportNuGetBuildTasksPackTargetsFromSdk = "false"
        NuGetBuildTasksPackTargets = ""
        LanguageTargets = $LanguageTargetsPath
        CSharpTargetsPath = $CSharpTargetsPath
        CommonTargetsPath = $CommonTargetsPath
    }
    $expectedProperties["EnableM01LoadedImageStackProbe"] = $LoadedImageStackEnabled.ToString().ToLowerInvariant()
    $expectedProperties["EnableM01LiveAllocatorProbe"] = $LiveAllocatorEnabled.ToString().ToLowerInvariant()
    $expectedProperties["EnableM01OwnedPagingProbe"] = $OwnedPagingEnabled.ToString().ToLowerInvariant()
    $expectedProperties["EnableM02SafeStubProbe"] = $M02SafeStubEnabled.ToString().ToLowerInvariant()
    $expectedProperties["EnableM02CheckedPortIoBindingProbe"] = $M02CheckedPortIoBindingEnabled.ToString().ToLowerInvariant()
    $expectedProperties["EnableM02Com1OwnedPortIoProbe"] = $M02Com1OwnedPortIoEnabled.ToString().ToLowerInvariant()
    $expectedProperties["EnableUefiGoneSimulationProbe"] = $UefiGoneSimulationEnabled.ToString().ToLowerInvariant()
    $expectedProperties["EnableExitBootServicesFreshMapProbe"] = $ExitBootServicesFreshMapEnabled.ToString().ToLowerInvariant()
    $expectedProperties["EnableAcpiPhysicalDiscoveryProbe"] = $AcpiPhysicalDiscoveryEnabled.ToString().ToLowerInvariant()

    foreach ($entry in $msbuildIsolation.Properties.GetEnumerator()) {
        $expectedProperties[$entry.Key] = [string] $entry.Value
    }
    foreach ($entry in $expectedProperties.GetEnumerator()) {
        if ($entry.Key.StartsWith('Enable', [StringComparison]::Ordinal) -and
            -not [string]::Equals([string]$evaluation.Properties.($entry.Key),
                [string]$entry.Value, [StringComparison]::Ordinal)) {
            throw "post-restore property mismatch: $($entry.Key)"
        }
        if ([string] $evaluation.Properties.($entry.Key) -cne
            [string] $entry.Value) {
            throw "post-restore property mismatch: $($entry.Key)"
        }
    }

    $compileItems = @($evaluation.Items.Compile)
    $projectReferences = @($evaluation.Items.ProjectReference)
    $references = @($evaluation.Items.Reference)
    $analyzers = @($evaluation.Items.Analyzer)
    if ($compileItems.Count -ne $script:ExpectedEfiCompileItems.Count -or
        $projectReferences.Count -ne 0 -or
        $references.Count -ne 0 -or
        $analyzers.Count -ne 2) {
        throw (
            "post-restore item count mismatch: compile=" +
            $compileItems.Count +
            ";project=" +
            $projectReferences.Count +
            ";reference=" +
            $references.Count +
            ";analyzer=" +
            $analyzers.Count)
    }

    $compileBuilder = New-Object System.Text.StringBuilder
    for ($index = 0; $index -lt $compileItems.Count; $index++) {
        $item = $compileItems[$index]
        $link = if ($null -eq $item.Link) { "" } else { [string] $item.Link }
        $key = [string] $item.Identity + "|" + $link
        if ($key -cne $script:ExpectedEfiCompileItems[$index]) {
            throw "post-restore compile item mismatch: $index"
        }
        [void] $compileBuilder.Append($key)
        [void] $compileBuilder.Append("`n")
    }
    $expectedAnalyzerNames = @(
        "Microsoft.CodeAnalysis.CSharp.NetAnalyzers.dll",
        "Microsoft.CodeAnalysis.NetAnalyzers.dll")
    $analyzerRecords = New-Object System.Collections.Generic.List[string]
    foreach ($analyzer in $analyzers) {
        $analyzerName = [System.IO.Path]::GetFileName(
            [string] $analyzer.FullPath)
        $definingProjectName = [System.IO.Path]::GetFileName(
            [string] $analyzer.DefiningProjectFullPath)
        if ($expectedAnalyzerNames -cnotcontains $analyzerName -or
            $definingProjectName -cne
                "Microsoft.NET.Sdk.Analyzers.targets") {
            throw "post-restore analyzer item mismatch"
        }
        $analyzerRecords.Add(
            $analyzerName + "|" + $definingProjectName)
    }
    $analyzerArray = [string[]] $analyzerRecords.ToArray()
    [System.Array]::Sort(
        $analyzerArray,
        [System.StringComparer]::Ordinal)
    return [ordered] @{
        compileCount = $compileItems.Count
        compileSha256 = Get-M01Utf8Sha256 -Text $compileBuilder.ToString()
        projectReferenceCount = $projectReferences.Count
        projectReferenceSha256 = Get-M01Utf8Sha256 -Text ""
        referenceCount = $references.Count
        referenceSha256 = Get-M01Utf8Sha256 -Text ""
        analyzerCount = $analyzers.Count
        analyzerSha256 = Get-M01Utf8Sha256 -Text (
            ($analyzerArray -join "`n") + "`n")
        properties = $expectedProperties
    }
}

function Test-M01RestoreSemanticClosure {
    param(
        [string] $ProjectPath,
        [string] $ProjectExtensionsDirectory,
        [string] $PackageCacheRoot
    )

    $projectFullPath = [System.IO.Path]::GetFullPath($ProjectPath)
    $extensionsFullPath = [System.IO.Path]::GetFullPath(
        $ProjectExtensionsDirectory)
    $packageCacheFullPath = [System.IO.Path]::GetFullPath(
        $PackageCacheRoot).TrimEnd(
            [System.IO.Path]::DirectorySeparatorChar,
            [System.IO.Path]::AltDirectorySeparatorChar)
    $propsPath = Join-Path $extensionsFullPath "OS.csproj.nuget.g.props"
    $targetsPath = Join-Path $extensionsFullPath "OS.csproj.nuget.g.targets"
    $assetsPath = Join-Path $extensionsFullPath "project.assets.json"

    $props = Read-M01SecureXmlDocument -Path $propsPath
    $propsRoot = $props.DocumentElement
    if ($null -eq $propsRoot -or
        $propsRoot.LocalName -cne "Project" -or
        $propsRoot.NamespaceURI -cne
            "http://schemas.microsoft.com/developer/msbuild/2003" -or
        $propsRoot.Attributes.Count -ne 2 -or
        $propsRoot.GetAttribute("ToolsVersion") -cne "14.0" -or
        $propsRoot.GetAttribute("xmlns") -cne
            "http://schemas.microsoft.com/developer/msbuild/2003") {
        throw "restore generated props root invalid"
    }
    $propsChildren = @(
        $propsRoot.ChildNodes |
            Where-Object { $_.NodeType -eq [System.Xml.XmlNodeType]::Element })
    $expectedChildNames = @(
        "PropertyGroup",
        "ItemGroup",
        "ImportGroup",
        "PropertyGroup")
    if ($propsChildren.Count -ne $expectedChildNames.Count) {
        throw "restore generated props child count invalid"
    }
    for ($index = 0; $index -lt $expectedChildNames.Count; $index++) {
        if ($propsChildren[$index].LocalName -cne $expectedChildNames[$index]) {
            throw "restore generated props child order invalid"
        }
        if ($propsChildren[$index].Attributes.Count -ne 1 -or
            $propsChildren[$index].GetAttribute("Condition") -cne
                " '`$(ExcludeRestorePackageImports)' != 'true' ") {
            throw "restore generated props group attributes invalid"
        }
    }

    $forbiddenPropsNodes = @($props.SelectNodes(
        "//*[local-name()='Target' or local-name()='UsingTask' or " +
        "local-name()='Choose' or local-name()='When' or " +
        "local-name()='Otherwise' or local-name()='ItemDefinitionGroup' or " +
        "local-name()='PropertyGroup']/*[contains(local-name(), 'Custom') or " +
        "contains(local-name(), 'Before') or contains(local-name(), 'After')]"))
    if ($forbiddenPropsNodes.Count -ne 0) {
        throw "restore generated props executable surface invalid"
    }

    $imports = @($props.SelectNodes("//*[local-name()='Import']"))
    $expectedImports = [ordered] @{
        '$(NuGetPackageRoot)\microsoft.net.illink.tasks\10.0.10\build\Microsoft.NET.ILLink.Tasks.props' =
            "Exists('`$(NuGetPackageRoot)\microsoft.net.illink.tasks\10.0.10\build\Microsoft.NET.ILLink.Tasks.props')"
        '$(NuGetPackageRoot)\microsoft.dotnet.ilcompiler\10.0.8\build\Microsoft.DotNet.ILCompiler.props' =
            "Exists('`$(NuGetPackageRoot)\microsoft.dotnet.ilcompiler\10.0.8\build\Microsoft.DotNet.ILCompiler.props')"
    }
    if ($imports.Count -ne $expectedImports.Count) {
        throw "restore generated props import count invalid"
    }
    $seenImports = New-Object 'System.Collections.Generic.HashSet[string]' (
        [System.StringComparer]::Ordinal)
    foreach ($import in $imports) {
        $project = $import.GetAttribute("Project")
        if (-not $expectedImports.Contains($project) -or
            -not $seenImports.Add($project) -or
            $import.GetAttribute("Condition") -cne $expectedImports[$project] -or
            $import.Attributes.Count -ne 2 -or
            $import.ParentNode.LocalName -cne "ImportGroup" -or
            $import.ParentNode.GetAttribute("Condition") -cne
                " '`$(ExcludeRestorePackageImports)' != 'true' ") {
            throw "restore generated props import invalid"
        }
    }

    $allowedPropertyNames = @(
        "RestoreSuccess",
        "RestoreTool",
        "ProjectAssetsFile",
        "NuGetPackageRoot",
        "NuGetPackageFolders",
        "NuGetProjectStyle",
        "NuGetToolVersion",
        "Pkgruntime_win-x64_Microsoft_DotNet_ILCompiler",
        "PkgMicrosoft_NET_ILLink_Tasks",
        "PkgMicrosoft_DotNet_ILCompiler")
    $propertyNodes = @(
        $props.SelectNodes("//*[local-name()='PropertyGroup']/*"))
    if ($propertyNodes.Count -ne $allowedPropertyNames.Count) {
        throw "restore generated props property count invalid"
    }
    $seenPropertyNames = New-Object 'System.Collections.Generic.HashSet[string]' (
        [System.StringComparer]::Ordinal)
    foreach ($propertyNode in $propertyNodes) {
        if ($allowedPropertyNames -cnotcontains $propertyNode.LocalName -or
            -not $seenPropertyNames.Add($propertyNode.LocalName) -or
            $propertyNode.Attributes.Count -ne 1 -or
            $propertyNode.GetAttribute("Condition") -cne
                (" '`$(" + $propertyNode.LocalName + ")' == '' ")) {
            throw "restore generated props property invalid"
        }
    }
    $userProfileFullPath = [System.IO.Path]::GetFullPath(
        [Environment]::GetFolderPath(
            [Environment+SpecialFolder]::UserProfile)).TrimEnd(
                [System.IO.Path]::DirectorySeparatorChar,
                [System.IO.Path]::AltDirectorySeparatorChar)
    $userProfilePrefix = $userProfileFullPath +
        [System.IO.Path]::DirectorySeparatorChar
    $generatedPackageRoot = if ($packageCacheFullPath.StartsWith(
            $userProfilePrefix,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        '$(UserProfile)\' +
            $packageCacheFullPath.Substring($userProfilePrefix.Length)
    }
    else {
        $packageCacheFullPath
    }
    $expectedPropertyValues = [ordered] @{
        RestoreSuccess = "True"
        RestoreTool = "NuGet"
        ProjectAssetsFile = '$(MSBuildThisFileDirectory)project.assets.json'
        NuGetPackageRoot = $generatedPackageRoot
        NuGetProjectStyle = "PackageReference"
        NuGetToolVersion = "7.0.0"
        'Pkgruntime_win-x64_Microsoft_DotNet_ILCompiler' =
            (Join-Path $packageCacheFullPath (
                "runtime.win-x64.microsoft.dotnet.ilcompiler\10.0.8"))
        'PkgMicrosoft_NET_ILLink_Tasks' =
            (Join-Path $packageCacheFullPath (
                "microsoft.net.illink.tasks\10.0.10"))
        'PkgMicrosoft_DotNet_ILCompiler' =
            (Join-Path $packageCacheFullPath (
                "microsoft.dotnet.ilcompiler\10.0.8"))
    }
    foreach ($propertyName in $expectedPropertyValues.Keys) {
        $propertyNode = @(
            $propertyNodes |
                Where-Object { $_.LocalName -ceq $propertyName })[0]
        if ($null -eq $propertyNode -or
            $propertyNode.InnerText -cne $expectedPropertyValues[$propertyName]) {
            throw "restore generated props property value invalid: $propertyName"
        }
    }

    $generatedTargets = Read-M01SecureXmlDocument -Path $targetsPath
    if ($null -eq $generatedTargets.DocumentElement -or
        $generatedTargets.DocumentElement.LocalName -cne "Project" -or
        $generatedTargets.DocumentElement.NamespaceURI -cne
            "http://schemas.microsoft.com/developer/msbuild/2003" -or
        $generatedTargets.DocumentElement.Attributes.Count -ne 2 -or
        $generatedTargets.DocumentElement.GetAttribute("ToolsVersion") -cne
            "14.0" -or
        $generatedTargets.DocumentElement.GetAttribute("xmlns") -cne
            "http://schemas.microsoft.com/developer/msbuild/2003" -or
        @($generatedTargets.DocumentElement.ChildNodes |
            Where-Object {
                $_.NodeType -eq [System.Xml.XmlNodeType]::Element
            }).Count -ne 0) {
        throw "restore generated targets must be empty"
    }

    try {
        $assets = [System.IO.File]::ReadAllText($assetsPath) |
            ConvertFrom-Json
    }
    catch {
        throw "restore project assets invalid"
    }
    if (-not (Test-M01ExactPropertyNameSet `
            -Value $assets `
            -ExpectedNames @(
                "version",
                "targets",
                "libraries",
                "projectFileDependencyGroups",
                "packageFolders",
                "project")) -or
        [int64] $assets.version -ne 4) {
        throw "restore project assets root invalid"
    }

    $libraryProperties = @($assets.libraries.PSObject.Properties)
    if ($libraryProperties.Count -ne $script:ExpectedRestoreLibraries.Count) {
        throw "restore project assets library count invalid"
    }
    foreach ($libraryProperty in $libraryProperties) {
        $libraryName = [string] $libraryProperty.Name
        if (-not $script:ExpectedRestoreLibraries.Contains($libraryName)) {
            throw "restore project assets library unexpected: $libraryName"
        }
        $expectedLibrary = $script:ExpectedRestoreLibraries[$libraryName]
        $library = $libraryProperty.Value
        if (-not (Test-M01ExactPropertyNameSet `
                -Value $library `
                -ExpectedNames @("sha512", "type", "path", "hasTools", "files")) -or
            [string] $library.sha512 -cne [string] $expectedLibrary.Sha512 -or
            [string] $library.type -cne "package" -or
            [string] $library.path -cne [string] $expectedLibrary.Path -or
            $library.hasTools -isnot [bool] -or
            -not [bool] $library.hasTools -or
            @($library.files).Count -ne [int] $expectedLibrary.FileCount) {
            throw "restore project assets library contract invalid: $libraryName"
        }
        $seenLibraryFiles = New-Object 'System.Collections.Generic.HashSet[string]' (
            [System.StringComparer]::Ordinal)
        foreach ($libraryFile in @($library.files)) {
            $libraryFilePath = [string] $libraryFile
            if ([string]::IsNullOrWhiteSpace($libraryFilePath) -or
                [System.IO.Path]::IsPathRooted($libraryFilePath) -or
                $libraryFilePath.IndexOf("..", [System.StringComparison]::Ordinal) -ge 0 -or
                $libraryFilePath.IndexOf(":", [System.StringComparison]::Ordinal) -ge 0 -or
                -not $seenLibraryFiles.Add($libraryFilePath)) {
                throw "restore project assets library file invalid: $libraryName"
            }
        }
    }

    $targetProperties = @($assets.targets.PSObject.Properties)
    $expectedTargetNames = @("net10.0", "net10.0/win-x64")
    if ($targetProperties.Count -ne $expectedTargetNames.Count) {
        throw "restore project assets target count invalid"
    }
    foreach ($targetName in $expectedTargetNames) {
        $targetProperty = @(
            $targetProperties |
                Where-Object { $_.Name -ceq $targetName })[0]
        if ($null -eq $targetProperty) {
            throw "restore project assets target missing: $targetName"
        }
        $targetLibraries = @($targetProperty.Value.PSObject.Properties)
        if ($targetLibraries.Count -ne $script:ExpectedRestoreLibraries.Count) {
            throw "restore project assets target library count invalid: $targetName"
        }
        foreach ($targetLibraryProperty in $targetLibraries) {
            $libraryName = [string] $targetLibraryProperty.Name
            if (-not $script:ExpectedRestoreLibraries.Contains($libraryName)) {
                throw "restore project assets target library unexpected: $libraryName"
            }
            $targetLibrary = $targetLibraryProperty.Value
            $isCompiler = $libraryName -ceq
                "Microsoft.DotNet.ILCompiler/10.0.8"
            $isLinker = $libraryName -ceq
                "Microsoft.NET.ILLink.Tasks/10.0.10"
            $isRidTarget = $targetName -ceq "net10.0/win-x64"
            $expectedNames = if ($isCompiler -and $isRidTarget) {
                @("type", "dependencies", "build")
            }
            elseif ($isCompiler -or $isLinker) {
                @("type", "build")
            }
            else {
                @("type")
            }
            if (-not (Test-M01ExactPropertyNameSet `
                    -Value $targetLibrary `
                    -ExpectedNames $expectedNames) -or
                [string] $targetLibrary.type -cne "package") {
                throw "restore project assets target library contract invalid: $targetName/$libraryName"
            }
            if ($isCompiler -or $isLinker) {
                $expectedBuildPath = if ($isCompiler) {
                    "build/Microsoft.DotNet.ILCompiler.props"
                }
                else {
                    "build/Microsoft.NET.ILLink.Tasks.props"
                }
                $buildProperties = @($targetLibrary.build.PSObject.Properties)
                if ($buildProperties.Count -ne 1 -or
                    $buildProperties[0].Name -cne $expectedBuildPath -or
                    @($buildProperties[0].Value.PSObject.Properties).Count -ne 0) {
                    throw "restore project assets build import invalid: $targetName/$libraryName"
                }
            }
            if ($isCompiler -and $isRidTarget) {
                $dependencies = @(
                    $targetLibrary.dependencies.PSObject.Properties)
                if ($dependencies.Count -ne 1 -or
                    $dependencies[0].Name -cne
                        "runtime.win-x64.Microsoft.DotNet.ILCompiler" -or
                    [string] $dependencies[0].Value -cne "10.0.8") {
                    throw "restore project assets compiler dependency invalid"
                }
            }
        }
    }

    $packageFolderProperties = @($assets.packageFolders.PSObject.Properties)
    if ($packageFolderProperties.Count -ne 1) {
        throw "restore package folder count invalid"
    }
    $expectedPrimaryFolder = $packageCacheFullPath
    if (-not [string]::Equals(
            [string] $packageFolderProperties[0].Name,
            $expectedPrimaryFolder,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "restore primary package folder invalid"
    }
    $nuGetPackageFolders = @(
        $packageFolderProperties |
            ForEach-Object { [string] $_.Name }) -join ";"
    $nuGetPackageFoldersNode = @(
        $propertyNodes |
            Where-Object { $_.LocalName -ceq "NuGetPackageFolders" })[0]
    if ($null -eq $nuGetPackageFoldersNode -or
        $nuGetPackageFoldersNode.InnerText -cne $nuGetPackageFolders) {
        throw "restore generated package-folder property invalid"
    }
    $sourceRoots = @($props.SelectNodes("//*[local-name()='SourceRoot']"))
    if ($sourceRoots.Count -ne $packageFolderProperties.Count) {
        throw "restore generated source-root count invalid"
    }
    for ($index = 0; $index -lt $sourceRoots.Count; $index++) {
        if ($sourceRoots[$index].Attributes.Count -ne 1 -or
            $sourceRoots[$index].GetAttribute("Include") -cne
                ([string] $packageFolderProperties[$index].Name +
                    [System.IO.Path]::DirectorySeparatorChar) -or
            $sourceRoots[$index].ParentNode.LocalName -cne "ItemGroup" -or
            $sourceRoots[$index].ParentNode.GetAttribute("Condition") -cne
                " '`$(ExcludeRestorePackageImports)' != 'true' ") {
            throw "restore generated source-root invalid"
        }
    }

    $restore = $assets.project.restore
    if ($null -eq $restore -or
        -not [string]::Equals(
            [System.IO.Path]::GetFullPath([string] $restore.projectPath),
            $projectFullPath,
            [System.StringComparison]::OrdinalIgnoreCase) -or
        -not [string]::Equals(
            [System.IO.Path]::GetFullPath([string] $restore.packagesPath),
            $packageCacheFullPath,
            [System.StringComparison]::OrdinalIgnoreCase) -or
        -not [string]::Equals(
            [System.IO.Path]::GetFullPath([string] $restore.outputPath),
            $extensionsFullPath +
                [System.IO.Path]::DirectorySeparatorChar,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "restore project path contract invalid"
    }
}

function Get-M01PackageBuildClosureSnapshot {
    param([string] $PackageCacheRoot)

    $packageCacheFullPath = [System.IO.Path]::GetFullPath(
        $PackageCacheRoot).TrimEnd(
            [System.IO.Path]::DirectorySeparatorChar,
            [System.IO.Path]::AltDirectorySeparatorChar)
    if (-not (Test-Path -LiteralPath $packageCacheFullPath -PathType Container)) {
        throw "package cache root missing"
    }
    $packageCacheItem = Get-Item -LiteralPath $packageCacheFullPath -Force
    if (($packageCacheItem.Attributes -band
            [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "package cache root is a reparse point"
    }

    $inputs = New-Object System.Collections.Generic.List[object]
    $aggregateBuilder = New-Object System.Text.StringBuilder
    foreach ($expectedInput in $script:ExpectedPackageBuildClosure) {
        $relativePath = [string] $expectedInput.Path
        $expectedHash = [string] $expectedInput.Sha256
        $fullPath = [System.IO.Path]::GetFullPath(
            (Join-Path $packageCacheFullPath $relativePath))
        $packageCachePrefix = $packageCacheFullPath +
            [System.IO.Path]::DirectorySeparatorChar
        if (-not $fullPath.StartsWith(
                $packageCachePrefix,
                [System.StringComparison]::OrdinalIgnoreCase) -or
            -not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
            throw "package build closure input missing: $relativePath"
        }
        $relativePlatformPath = $relativePath.Replace(
            "/",
            [System.IO.Path]::DirectorySeparatorChar)
        $currentPath = $packageCacheFullPath
        foreach ($segment in $relativePlatformPath.Split(
                [System.IO.Path]::DirectorySeparatorChar)) {
            $currentPath = Join-Path $currentPath $segment
            $item = Get-Item -LiteralPath $currentPath -Force
            if (($item.Attributes -band
                    [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "package build closure contains a reparse point: $relativePath"
            }
        }
        $actualHash = (
            Get-FileHash -LiteralPath $fullPath -Algorithm SHA256
        ).Hash.ToLowerInvariant()
        if ($actualHash -cne $expectedHash) {
            throw "package build closure hash mismatch: $relativePath"
        }
        $inputs.Add([ordered] @{
                path = $relativePath
                sha256 = $actualHash
            })
        [void] $aggregateBuilder.Append($relativePath)
        [void] $aggregateBuilder.Append("|")
        [void] $aggregateBuilder.Append($actualHash)
        [void] $aggregateBuilder.Append("`n")
    }

    $algorithm = [System.Security.Cryptography.SHA256]::Create()
    try {
        $aggregateHash = ([System.BitConverter]::ToString(
                $algorithm.ComputeHash(
                    [System.Text.Encoding]::UTF8.GetBytes(
                        $aggregateBuilder.ToString())))).Replace(
            "-",
            "").ToLowerInvariant()
    }
    finally {
        $algorithm.Dispose()
    }
    return [pscustomobject] @{
        PackageCacheRoot = $packageCacheFullPath
        Inputs = $inputs.ToArray()
        AggregateSha256 = $aggregateHash
    }
}

function Get-M01PinnedRestoreArchiveSnapshot {
    param([string] $GlobalPackageCacheRoot)

    $cacheFullPath = [System.IO.Path]::GetFullPath(
        $GlobalPackageCacheRoot).TrimEnd(
            [System.IO.Path]::DirectorySeparatorChar,
            [System.IO.Path]::AltDirectorySeparatorChar)
    if (-not (Test-Path -LiteralPath $cacheFullPath -PathType Container)) {
        throw "global package cache root missing"
    }
    $cacheItem = Get-Item -LiteralPath $cacheFullPath -Force
    if (($cacheItem.Attributes -band
            [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "global package cache root is a reparse point"
    }
    $cachePrefix = $cacheFullPath +
        [System.IO.Path]::DirectorySeparatorChar
    $inputs = New-Object System.Collections.Generic.List[object]
    $aggregateBuilder = New-Object System.Text.StringBuilder
    foreach ($archive in $script:ExpectedRestoreArchives) {
        $relativePath = (
            [string] $archive.Id +
            "/" +
            [string] $archive.Version +
            "/" +
            [string] $archive.FileName)
        $fullPath = [System.IO.Path]::GetFullPath(
            (Join-Path $cacheFullPath $relativePath))
        if (-not $fullPath.StartsWith(
                $cachePrefix,
                [System.StringComparison]::OrdinalIgnoreCase) -or
            -not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
            throw "pinned restore archive missing: $relativePath"
        }
        $currentPath = $cacheFullPath
        foreach ($segment in $relativePath.Replace(
                "/",
                [System.IO.Path]::DirectorySeparatorChar).Split(
                    [System.IO.Path]::DirectorySeparatorChar)) {
            $currentPath = Join-Path $currentPath $segment
            $item = Get-Item -LiteralPath $currentPath -Force
            if (($item.Attributes -band
                    [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "pinned restore archive path contains a reparse point"
            }
        }
        $actualHash = (
            Get-FileHash -LiteralPath $fullPath -Algorithm SHA256
        ).Hash.ToLowerInvariant()
        if ($actualHash -cne [string] $archive.Sha256) {
            throw "pinned restore archive hash mismatch: $relativePath"
        }
        $inputs.Add([ordered] @{
                path = $relativePath
                sha256 = $actualHash
            })
        [void] $aggregateBuilder.Append($relativePath)
        [void] $aggregateBuilder.Append("|")
        [void] $aggregateBuilder.Append($actualHash)
        [void] $aggregateBuilder.Append("`n")
    }
    $algorithm = [System.Security.Cryptography.SHA256]::Create()
    try {
        $aggregateHash = ([System.BitConverter]::ToString(
                $algorithm.ComputeHash(
                    [System.Text.Encoding]::UTF8.GetBytes(
                        $aggregateBuilder.ToString())))).Replace(
            "-",
            "").ToLowerInvariant()
    }
    finally {
        $algorithm.Dispose()
    }
    return [pscustomobject] @{
        RootPath = $cacheFullPath
        Inputs = $inputs.ToArray()
        AggregateSha256 = $aggregateHash
    }
}

function New-M01PinnedRestoreSource {
    param(
        [string] $GlobalPackageCacheRoot,
        [string] $RestoreSourceDirectory,
        [object[]] $ArchiveInputs
    )

    $cacheFullPath = [System.IO.Path]::GetFullPath(
        $GlobalPackageCacheRoot).TrimEnd(
            [System.IO.Path]::DirectorySeparatorChar,
            [System.IO.Path]::AltDirectorySeparatorChar)
    $sourceFullPath = [System.IO.Path]::GetFullPath($RestoreSourceDirectory)
    [System.IO.Directory]::CreateDirectory($sourceFullPath) | Out-Null
    $sourceItem = Get-Item -LiteralPath $sourceFullPath -Force
    if (($sourceItem.Attributes -band
            [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "pinned restore source is a reparse point"
    }
    foreach ($input in $ArchiveInputs) {
        $relativePath = [string] $input.path
        $sourcePath = [System.IO.Path]::GetFullPath(
            (Join-Path $cacheFullPath $relativePath))
        $destinationPath = Join-Path $sourceFullPath (
            [System.IO.Path]::GetFileName($relativePath))
        [System.IO.File]::Copy($sourcePath, $destinationPath, $false)
        $destinationHash = (
            Get-FileHash -LiteralPath $destinationPath -Algorithm SHA256
        ).Hash.ToLowerInvariant()
        if ($destinationHash -cne [string] $input.sha256) {
            throw "pinned restore archive copy mismatch: $relativePath"
        }
    }
    $escapedSourcePath = [System.Security.SecurityElement]::Escape(
        $sourceFullPath)
    $configPath = Join-Path $sourceFullPath "NuGet.Config"
    $configText = (
        "<?xml version=`"1.0`" encoding=`"utf-8`"?>`n" +
        "<configuration>`n" +
        "  <packageSources>`n" +
        "    <clear />`n" +
        "    <add key=`"CLROS_PINNED`" value=`"" +
        $escapedSourcePath +
        "`" />`n" +
        "  </packageSources>`n" +
        "  <fallbackPackageFolders>`n" +
        "    <clear />`n" +
        "  </fallbackPackageFolders>`n" +
        "  <auditSources>`n" +
        "    <clear />`n" +
        "  </auditSources>`n" +
        "</configuration>`n")
    [System.IO.File]::WriteAllText(
        $configPath,
        $configText,
        (New-Object System.Text.UTF8Encoding($false)))
    return $configPath
}

function Get-M01ConsumedPackageClosureSnapshot {
    param(
        [string] $RestorePackagesRoot,
        [string] $RuntimePackageRoot,
        [string] $RestoreSourceDirectory
    )

    $restoreRoot = [System.IO.Path]::GetFullPath(
        $RestorePackagesRoot).TrimEnd(
            [System.IO.Path]::DirectorySeparatorChar,
            [System.IO.Path]::AltDirectorySeparatorChar)
    $runtimeRoot = [System.IO.Path]::GetFullPath(
        $RuntimePackageRoot).TrimEnd(
            [System.IO.Path]::DirectorySeparatorChar,
            [System.IO.Path]::AltDirectorySeparatorChar)
    $restoreSourceFullPath = [System.IO.Path]::GetFullPath(
        $RestoreSourceDirectory).TrimEnd(
            [System.IO.Path]::DirectorySeparatorChar,
            [System.IO.Path]::AltDirectorySeparatorChar)
    $packages = New-Object System.Collections.Generic.List[object]
    $lockInputs = New-Object System.Collections.Generic.List[object]
    $closureBuilder = New-Object System.Text.StringBuilder

    if (-not (Test-Path -LiteralPath $restoreRoot -PathType Container)) {
        throw "isolated restore package root missing"
    }
    $restoreRootItem = Get-Item -LiteralPath $restoreRoot -Force
    if (($restoreRootItem.Attributes -band
            [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "isolated restore package root is a reparse point"
    }
    $expectedRestoreVersions = @(
        $script:ExpectedConsumedPackageTrees |
            Where-Object { [string] $_.RootKind -cne "runtime" } |
            ForEach-Object {
                [string] $_.Id + "/" + [string] $_.Version
            })
    $actualRestoreVersions = New-Object System.Collections.Generic.List[string]
    foreach ($packageIdDirectory in @(
            Get-ChildItem -LiteralPath $restoreRoot -Force)) {
        if (-not $packageIdDirectory.PSIsContainer -or
            ($packageIdDirectory.Attributes -band
                [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "isolated restore package root shape invalid"
        }
        $versionEntries = @(
            Get-ChildItem -LiteralPath $packageIdDirectory.FullName -Force)
        foreach ($versionEntry in $versionEntries) {
            if (-not $versionEntry.PSIsContainer -or
                ($versionEntry.Attributes -band
                    [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "isolated restore package version shape invalid"
            }
            $actualRestoreVersions.Add(
                $packageIdDirectory.Name + "/" + $versionEntry.Name)
        }
    }
    $expectedRestoreVersions = [string[]] $expectedRestoreVersions
    $actualRestoreVersionArray = [string[]] $actualRestoreVersions.ToArray()
    [System.Array]::Sort(
        $expectedRestoreVersions,
        [System.StringComparer]::Ordinal)
    [System.Array]::Sort(
        $actualRestoreVersionArray,
        [System.StringComparer]::Ordinal)
    if ($actualRestoreVersionArray.Count -ne
        $expectedRestoreVersions.Count) {
        throw "isolated restore package version count invalid"
    }
    for ($versionIndex = 0;
        $versionIndex -lt $expectedRestoreVersions.Count;
        $versionIndex++) {
        if ($actualRestoreVersionArray[$versionIndex] -cne
            $expectedRestoreVersions[$versionIndex]) {
            throw "isolated restore package version set invalid"
        }
    }

    foreach ($expectedPackage in $script:ExpectedConsumedPackageTrees) {
        if ([string] $expectedPackage.RootKind -cnotin @(
                "restore",
                "restore-only",
                "runtime")) {
            throw "consumed package root kind invalid"
        }
        $packageRoot = if ([string] $expectedPackage.RootKind -cne "runtime") {
            Join-Path $restoreRoot (
                [string] $expectedPackage.Id +
                "\" +
                [string] $expectedPackage.Version)
        }
        else {
            $runtimeRoot
        }
        $packageRoot = [System.IO.Path]::GetFullPath($packageRoot).TrimEnd(
            [System.IO.Path]::DirectorySeparatorChar,
            [System.IO.Path]::AltDirectorySeparatorChar)
        $expectedSuffix = (
            [string] $expectedPackage.Id +
            "\" +
            [string] $expectedPackage.Version)
        if (-not $packageRoot.EndsWith(
                $expectedSuffix,
                [System.StringComparison]::OrdinalIgnoreCase) -or
            -not (Test-Path -LiteralPath $packageRoot -PathType Container)) {
            throw "consumed package root invalid: $($expectedPackage.Id)"
        }
        $packageRootItem = Get-Item -LiteralPath $packageRoot -Force
        if (($packageRootItem.Attributes -band
                [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "consumed package root is a reparse point: $($expectedPackage.Id)"
        }
        $treeItems = @(
            Get-ChildItem -LiteralPath $packageRoot -Recurse -Force)
        foreach ($treeItem in $treeItems) {
            if (($treeItem.Attributes -band
                    [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "consumed package contains a reparse point: $($expectedPackage.Id)"
            }
        }
        $metadataPath = Join-Path $packageRoot ".nupkg.metadata"
        if (-not (Test-Path -LiteralPath $metadataPath -PathType Leaf)) {
            throw "consumed package metadata missing: $($expectedPackage.Id)"
        }
        try {
            $metadata = [System.IO.File]::ReadAllText($metadataPath) |
                ConvertFrom-Json
        }
        catch {
            throw "consumed package metadata invalid: $($expectedPackage.Id)"
        }
        if (-not (Test-M01ExactPropertyNameSet `
                -Value $metadata `
                -ExpectedNames @("version", "contentHash", "source")) -or
            [int64] $metadata.version -ne 2 -or
            [string] $metadata.contentHash -cne
                [string] $expectedPackage.ContentHashSha512 -or
            [string]::IsNullOrWhiteSpace([string] $metadata.source)) {
            throw "consumed package metadata contract invalid: $($expectedPackage.Id)"
        }
        if ([string] $expectedPackage.RootKind -cne "runtime" -and
            -not [string]::Equals(
                [System.IO.Path]::GetFullPath([string] $metadata.source),
                $restoreSourceFullPath,
                [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "consumed package source invalid: $($expectedPackage.Id)"
        }

        $fileRecords = New-Object System.Collections.Generic.List[object]
        $treeBuilder = New-Object System.Text.StringBuilder
        $totalBytes = [int64] 0
        $fileByRelativePath = New-Object `
            'System.Collections.Generic.Dictionary[string,object]' (
                [System.StringComparer]::Ordinal)
        foreach ($file in @(
                $treeItems |
                    Where-Object {
                        -not $_.PSIsContainer -and
                        $_.Name -cne ".nupkg.metadata"
                    })) {
            $relativePath = $file.FullName.Substring(
                $packageRoot.Length + 1).Replace("\", "/")
            if ($fileByRelativePath.ContainsKey($relativePath)) {
                throw "consumed package contains duplicate path: $relativePath"
            }
            $fileByRelativePath.Add($relativePath, $file)
        }
        $relativePaths = [string[]] @($fileByRelativePath.Keys)
        [System.Array]::Sort(
            $relativePaths,
            [System.StringComparer]::Ordinal)
        foreach ($relativePath in $relativePaths) {
            $file = $fileByRelativePath[$relativePath]
            $hash = (
                Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256
            ).Hash.ToLowerInvariant()
            $length = [int64] $file.Length
            $totalBytes += $length
            $fileRecords.Add([ordered] @{
                    path = $relativePath
                    bytes = $length
                    sha256 = $hash
                })
            $lockInputs.Add([pscustomobject] @{ fullPath = $file.FullName })
            [void] $treeBuilder.Append($relativePath)
            [void] $treeBuilder.Append("|")
            [void] $treeBuilder.Append($length)
            [void] $treeBuilder.Append("|")
            [void] $treeBuilder.Append($hash)
            [void] $treeBuilder.Append("`n")
        }
        $lockInputs.Add([pscustomobject] @{ fullPath = $metadataPath })
        $treeAlgorithm = [System.Security.Cryptography.SHA256]::Create()
        try {
            $treeHash = ([System.BitConverter]::ToString(
                    $treeAlgorithm.ComputeHash(
                        [System.Text.Encoding]::UTF8.GetBytes(
                            $treeBuilder.ToString())))).Replace(
                "-",
                "").ToLowerInvariant()
        }
        finally {
            $treeAlgorithm.Dispose()
        }
        if ($fileRecords.Count -ne [int] $expectedPackage.FileCount -or
            $totalBytes -ne [int64] $expectedPackage.TotalBytes -or
            $treeHash -cne [string] $expectedPackage.AggregateSha256) {
            throw (
                "consumed package tree mismatch: " +
                $expectedPackage.Id +
                "/" +
                $expectedPackage.Version +
                ";count=" +
                $fileRecords.Count +
                ";bytes=" +
                $totalBytes +
                ";sha256=" +
                $treeHash)
        }
        $metadataHash = (
            Get-FileHash -LiteralPath $metadataPath -Algorithm SHA256
        ).Hash.ToLowerInvariant()
        $packageEvidence = [ordered] @{
            id = [string] $expectedPackage.Id
            version = [string] $expectedPackage.Version
            rootKind = [string] $expectedPackage.RootKind
            rootPath = $packageRoot
            contentHashSha512 =
                [string] $expectedPackage.ContentHashSha512
            fileCount = $fileRecords.Count
            totalBytes = $totalBytes
            aggregateSha256 = $treeHash
            metadataSha256 = $metadataHash
            inputs = $fileRecords.ToArray()
        }
        $packages.Add($packageEvidence)
        [void] $closureBuilder.Append($packageEvidence.id)
        [void] $closureBuilder.Append("|")
        [void] $closureBuilder.Append($packageEvidence.version)
        [void] $closureBuilder.Append("|")
        [void] $closureBuilder.Append($packageEvidence.rootKind)
        [void] $closureBuilder.Append("|")
        [void] $closureBuilder.Append($packageEvidence.aggregateSha256)
        [void] $closureBuilder.Append("`n")
    }
    $closureAlgorithm = [System.Security.Cryptography.SHA256]::Create()
    try {
        $closureHash = ([System.BitConverter]::ToString(
                $closureAlgorithm.ComputeHash(
                    [System.Text.Encoding]::UTF8.GetBytes(
                        $closureBuilder.ToString())))).Replace(
            "-",
            "").ToLowerInvariant()
    }
    finally {
        $closureAlgorithm.Dispose()
    }
    return [pscustomobject] @{
        Packages = $packages.ToArray()
        LockInputs = $lockInputs.ToArray()
        AggregateSha256 = $closureHash
    }
}

function Open-M01AbsoluteInputLocks {
    param([object[]] $Inputs)

    $locks = New-Object System.Collections.Generic.List[System.IO.FileStream]
    try {
        foreach ($input in $Inputs) {
            $fullPath = [System.IO.Path]::GetFullPath(
                [string] $input.fullPath)
            $locks.Add([System.IO.File]::Open(
                    $fullPath,
                    [System.IO.FileMode]::Open,
                    [System.IO.FileAccess]::Read,
                    [System.IO.FileShare]::Read))
        }
        return ,$locks
    }
    catch {
        foreach ($lock in $locks) {
            $lock.Dispose()
        }
        throw
    }
}

function Open-M01ExternalInputLocks {
    param(
        [string] $RootPath,
        [object[]] $Inputs
    )

    $rootFullPath = [System.IO.Path]::GetFullPath($RootPath).TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar)
    $rootPrefix = $rootFullPath + [System.IO.Path]::DirectorySeparatorChar
    $locks = New-Object System.Collections.Generic.List[System.IO.FileStream]
    try {
        foreach ($input in $Inputs) {
            $relativePath = [string] $input.path
            if ([string]::IsNullOrWhiteSpace($relativePath) -or
                [System.IO.Path]::IsPathRooted($relativePath)) {
                throw "external lock input path invalid"
            }
            $fullPath = [System.IO.Path]::GetFullPath(
                (Join-Path $rootFullPath $relativePath))
            if (-not $fullPath.StartsWith(
                    $rootPrefix,
                    [System.StringComparison]::OrdinalIgnoreCase)) {
                throw "external lock input outside root"
            }
            $locks.Add([System.IO.File]::Open(
                    $fullPath,
                    [System.IO.FileMode]::Open,
                    [System.IO.FileAccess]::Read,
                    [System.IO.FileShare]::Read))
        }
        return ,$locks
    }
    catch {
        foreach ($lock in $locks) {
            $lock.Dispose()
        }
        throw
    }
}

function Open-M01PublishSourceLocks {
    param(
        [string] $RepositoryRoot,
        [object[]] $Inputs
    )

    $repositoryFullPath = [System.IO.Path]::GetFullPath($RepositoryRoot)
    $repositoryPrefix = $repositoryFullPath.TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar) +
        [System.IO.Path]::DirectorySeparatorChar
    $locks = New-Object System.Collections.Generic.List[object]
    try {
        foreach ($input in $Inputs) {
            $relativePath = [string] $input.path
            if ([string]::IsNullOrWhiteSpace($relativePath) -or
                [System.IO.Path]::IsPathRooted($relativePath)) {
                throw "source lock input path missing"
            }

            $fullPath = [System.IO.Path]::GetFullPath(
                (Join-Path $repositoryFullPath $relativePath))
            if (-not $fullPath.StartsWith(
                    $repositoryPrefix,
                    [System.StringComparison]::OrdinalIgnoreCase)) {
                throw "source lock input outside repository: $fullPath"
            }
            $locks.Add((Open-M01AuthorityReadRecord `
                    -Path $fullPath `
                    -RootPath $repositoryFullPath `
                    -Purpose ("source:" + $relativePath) `
                    -FailurePrefix "source_lock_authority" `
                    -ExpectedSha256 ([string] $input.sha256) `
                    -RequireSingleLink))
        }
        Assert-M01DistinctAuthorityRecords -Records $locks.ToArray()
        return ,$locks
    }
    catch {
        Close-M01AuthorityRecords -Records $locks
        throw
    }
}

function Assert-M01PublishSourceLocks {
    param(
        [Parameter(Mandatory = $true)]
        [string] $RepositoryRoot,
        [Parameter(Mandatory = $true)]
        [object[]] $Inputs,
        [Parameter(Mandatory = $true)]
        [object] $Locks
    )

    if ($Locks.Count -ne $Inputs.Count) {
        throw "source authority lock count mismatch"
    }
    $repositoryFullPath = [System.IO.Path]::GetFullPath($RepositoryRoot)
    for ($index = 0; $index -lt $Inputs.Count; $index++) {
        $record = $Locks[$index]
        $relativePath = [string] $Inputs[$index].path
        $expectedPath = [System.IO.Path]::GetFullPath(
            (Join-Path $repositoryFullPath $relativePath))
        if (-not [string]::Equals(
                $record.Path,
                $expectedPath,
                [System.StringComparison]::OrdinalIgnoreCase) -or
            [string] $record.InitialSha256 -cne
                ([string] $Inputs[$index].sha256).ToLowerInvariant()) {
            throw "source authority lock input mismatch: $relativePath"
        }
        Assert-M01AuthorityRecordUnchanged -Record $record
    }
    Assert-M01DistinctAuthorityRecords -Records $Locks.ToArray()
}

function Close-M01PublishSourceLocks {
    param(
        [AllowNull()]
        [object] $Locks
    )

    if ($null -eq $Locks) {
        return
    }

    for ($index = $Locks.Count - 1; $index -ge 0; $index--) {
        $lock = $Locks[$index]
        if ($null -ne $lock.PSObject.Properties["Stream"]) {
            $lock.Stream.Dispose()
        }
        else {
            $lock.Dispose()
        }
    }
    $Locks.Clear()
}

function Open-M01PublishOutputLock {
    param(
        [string] $RepositoryRoot,
        [string] $Configuration,
        [string] $Runtime
    )

    if ($Configuration -notmatch '^(Release|Debug)$' -or
        $Runtime -notmatch '^[A-Za-z0-9-]+$') {
        throw "publish output lock identity invalid"
    }
    $repositoryFullPath = [System.IO.Path]::GetFullPath($RepositoryRoot)
    $artifactsPath = Join-Path $repositoryFullPath "artifacts"
    $lockDirectory = Join-Path $artifactsPath ".locks"
    if (-not (Test-Path -LiteralPath $repositoryFullPath -PathType Container)) {
        throw "publish output lock repository missing: $repositoryFullPath"
    }
    foreach ($directory in @($repositoryFullPath, $artifactsPath, $lockDirectory)) {
        if (-not (Test-Path -LiteralPath $directory)) {
            New-Item -ItemType Directory -Path $directory -Force | Out-Null
        }
        $directoryItem = Get-Item -LiteralPath $directory -Force
        if (-not $directoryItem.PSIsContainer -or
            ($directoryItem.Attributes -band
                [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "publish output lock path is not a plain directory: $directory"
        }
    }

    $lockPath = Join-Path $lockDirectory "efi-publish.lock"
    try {
        return [System.IO.File]::Open(
            $lockPath,
            [System.IO.FileMode]::OpenOrCreate,
            [System.IO.FileAccess]::ReadWrite,
            [System.IO.FileShare]::None)
    }
    catch [System.IO.IOException] {
        throw "another EFI publish owns the canonical output paths"
    }
}

function Close-M01PublishOutputLock {
    param(
        [AllowNull()]
        [System.IO.FileStream] $Lock
    )

    if ($null -eq $Lock) {
        return
    }

    $Lock.Dispose()
}

function Remove-M01FreshPublishDirectory {
    param(
        [string] $RepositoryRoot,
        [string] $CandidatePath,
        [string] $ExpectedRelativePath
    )

    $repositoryFullPath = [System.IO.Path]::GetFullPath($RepositoryRoot)
    $candidateFullPath = [System.IO.Path]::GetFullPath($CandidatePath)
    $expectedFullPath = [System.IO.Path]::GetFullPath(
        (Join-Path $repositoryFullPath $ExpectedRelativePath))
    $repositoryPrefix = $repositoryFullPath.TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar) +
        [System.IO.Path]::DirectorySeparatorChar
    if (-not [string]::Equals(
            $candidateFullPath,
            $expectedFullPath,
            [System.StringComparison]::OrdinalIgnoreCase) -or
        -not $candidateFullPath.StartsWith(
            $repositoryPrefix,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "fresh publish path mismatch: $candidateFullPath"
    }

    $relativePath = $candidateFullPath.Substring($repositoryPrefix.Length)
    $currentPath = $repositoryFullPath
    foreach ($segment in $relativePath.Split(
            @(
                [System.IO.Path]::DirectorySeparatorChar,
                [System.IO.Path]::AltDirectorySeparatorChar
            ),
            [System.StringSplitOptions]::RemoveEmptyEntries)) {
        $currentPath = Join-Path $currentPath $segment
        if (-not (Test-Path -LiteralPath $currentPath)) {
            continue
        }

        $item = Get-Item -LiteralPath $currentPath -Force
        if (($item.Attributes -band
                [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "fresh publish path contains reparse point: $currentPath"
        }
    }

    if (Test-Path -LiteralPath $candidateFullPath -PathType Leaf) {
        throw "fresh publish path is not a directory: $candidateFullPath"
    }
    if (Test-Path -LiteralPath $candidateFullPath -PathType Container) {
        Remove-Item `
            -LiteralPath $candidateFullPath `
            -Recurse `
            -Force `
            -ErrorAction Stop
    }
    if (Test-Path -LiteralPath $candidateFullPath) {
        throw "fresh publish path cleanup failed: $candidateFullPath"
    }
}

function New-M01ProcessJob {
    if ($null -eq ("CLROS.Build.ProcessJob" -as [type])) {
        Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CLROS.Build
{
    public sealed class ProcessJob : IDisposable
    {
        private const uint JobObjectBasicAccountingInformation = 1;
        private const uint JobObjectExtendedLimitInformation = 9;
        private const uint JobObjectLimitKillOnJobClose = 0x00002000;
        private IntPtr handle;

        private ProcessJob(IntPtr handle)
        {
            this.handle = handle;
        }

        public static ProcessJob Create()
        {
            IntPtr handle = CreateJobObject(IntPtr.Zero, null);
            if (handle == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            try
            {
                JOBOBJECT_EXTENDED_LIMIT_INFORMATION information =
                    new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
                information.BasicLimitInformation.LimitFlags =
                    JobObjectLimitKillOnJobClose;
                int length = Marshal.SizeOf(
                    typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
                IntPtr buffer = Marshal.AllocHGlobal(length);
                try
                {
                    Marshal.StructureToPtr(information, buffer, false);
                    if (!SetInformationJobObject(
                            handle,
                            JobObjectExtendedLimitInformation,
                            buffer,
                            (uint)length))
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }

                return new ProcessJob(handle);
            }
            catch
            {
                CloseHandle(handle);
                throw;
            }
        }

        public void Assign(Process process)
        {
            if (process == null)
            {
                throw new ArgumentNullException("process");
            }
            if (handle == IntPtr.Zero)
            {
                throw new ObjectDisposedException("ProcessJob");
            }
            if (!AssignProcessToJobObject(handle, process.Handle))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        public void Terminate()
        {
            if (handle == IntPtr.Zero)
            {
                throw new ObjectDisposedException("ProcessJob");
            }
            if (!TerminateJobObject(handle, 1))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        public uint ActiveProcessCount
        {
            get
            {
                if (handle == IntPtr.Zero)
                {
                    throw new ObjectDisposedException("ProcessJob");
                }

                JOBOBJECT_BASIC_ACCOUNTING_INFORMATION information =
                    new JOBOBJECT_BASIC_ACCOUNTING_INFORMATION();
                int length = Marshal.SizeOf(
                    typeof(JOBOBJECT_BASIC_ACCOUNTING_INFORMATION));
                IntPtr buffer = Marshal.AllocHGlobal(length);
                try
                {
                    if (!QueryInformationJobObject(
                            handle,
                            JobObjectBasicAccountingInformation,
                            buffer,
                            (uint)length,
                            IntPtr.Zero))
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    }

                    information =
                        (JOBOBJECT_BASIC_ACCOUNTING_INFORMATION)
                        Marshal.PtrToStructure(
                            buffer,
                            typeof(JOBOBJECT_BASIC_ACCOUNTING_INFORMATION));
                    return information.ActiveProcesses;
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
        }

        public bool WaitForEmpty(int milliseconds)
        {
            if (milliseconds < 0)
            {
                throw new ArgumentOutOfRangeException("milliseconds");
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            do
            {
                if (ActiveProcessCount == 0)
                {
                    return true;
                }
                Thread.Sleep(10);
            }
            while (stopwatch.ElapsedMilliseconds < milliseconds);

            return ActiveProcessCount == 0;
        }

        public void Dispose()
        {
            if (handle == IntPtr.Zero)
            {
                return;
            }

            CloseHandle(handle);
            handle = IntPtr.Zero;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IO_COUNTERS
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
            public IO_COUNTERS IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_BASIC_ACCOUNTING_INFORMATION
        {
            public long TotalUserTime;
            public long TotalKernelTime;
            public long ThisPeriodTotalUserTime;
            public long ThisPeriodTotalKernelTime;
            public uint TotalPageFaultCount;
            public uint TotalProcesses;
            public uint ActiveProcesses;
            public uint TotalTerminatedProcesses;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateJobObject(
            IntPtr jobAttributes,
            string name);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetInformationJobObject(
            IntPtr job,
            uint informationClass,
            IntPtr information,
            uint informationLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AssignProcessToJobObject(
            IntPtr job,
            IntPtr process);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool TerminateJobObject(
            IntPtr job,
            uint exitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool QueryInformationJobObject(
            IntPtr job,
            uint informationClass,
            IntPtr information,
            uint informationLength,
            IntPtr returnLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);
    }

    public sealed class BoundedProcessOutput : IDisposable
    {
        private readonly object sync = new object();
        private readonly int maximumCharacters;
        private readonly StringBuilder captured = new StringBuilder();
        private bool disposed;
        private bool limitExceeded;

        public BoundedProcessOutput(
            TextReader standardOutput,
            TextReader standardError,
            int maximumCharacters)
        {
            if (standardOutput == null)
            {
                throw new ArgumentNullException("standardOutput");
            }
            if (standardError == null)
            {
                throw new ArgumentNullException("standardError");
            }
            if (maximumCharacters <= 0)
            {
                throw new ArgumentOutOfRangeException("maximumCharacters");
            }

            this.maximumCharacters = maximumCharacters;
            Tasks = new Task[]
            {
                StartDrain(standardOutput),
                StartDrain(standardError)
            };
        }

        public Task[] Tasks { get; private set; }

        public bool LimitExceeded
        {
            get
            {
                lock (sync)
                {
                    return limitExceeded;
                }
            }
        }

        public int CapturedCharacters
        {
            get
            {
                lock (sync)
                {
                    return captured.Length;
                }
            }
        }

        public string GetText()
        {
            lock (sync)
            {
                return captured.ToString();
            }
        }

        public void Dispose()
        {
            lock (sync)
            {
                disposed = true;
            }
        }

        private Task StartDrain(TextReader reader)
        {
            return Task.Factory.StartNew(
                delegate
                {
                    char[] buffer = new char[4096];
                    while (true)
                    {
                        int count = reader.Read(buffer, 0, buffer.Length);
                        if (count == 0)
                        {
                            return;
                        }

                        lock (sync)
                        {
                            if (disposed)
                            {
                                return;
                            }

                            int remaining =
                                maximumCharacters - captured.Length;
                            if (remaining > 0)
                            {
                                captured.Append(
                                    buffer,
                                    0,
                                    Math.Min(remaining, count));
                            }
                            if (count > remaining)
                            {
                                limitExceeded = true;
                            }
                        }
                    }
                },
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }
    }
}
'@
    }

    return [CLROS.Build.ProcessJob]::Create()
}

function Stop-M01ProcessTree {
    param(
        [System.Diagnostics.Process] $Process,
        [object] $ProcessJob
    )

    $wrapperExited = $false
    $jobEmpty = $false
    try {
        $ProcessJob.Terminate()
        $wrapperExited = $Process.WaitForExit(5000)
        $jobEmpty = $ProcessJob.WaitForEmpty(5000)
    }
    catch {
        try {
            if (-not $Process.HasExited) {
                $Process.Kill()
            }
            $wrapperExited = $Process.WaitForExit(5000)
            $jobEmpty = $ProcessJob.WaitForEmpty(5000)
        }
        catch {
            # The false result below is authoritative.
        }
    }

    return $wrapperExited -and $jobEmpty
}

function Get-M01BoundedProcessFailureReason {
    param([object] $Result)

    if ($Result.TimedOut) {
        return "timeout"
    }
    if ($Result.OutputLimitExceeded) {
        return "output_limit_exceeded"
    }
    if (-not $Result.TerminationConfirmed) {
        return "process_tree_termination_unconfirmed"
    }
    if (-not $Result.OutputCaptureSucceeded) {
        return "output_capture_failed"
    }
    if ($Result.ExitCode -ne 0) {
        return "exit_code_" + $Result.ExitCode
    }
    return "ok"
}

function Write-M01BoundedProcessOutput {
    param(
        [object] $Result,
        [string] $LogFullPath
    )

    foreach ($line in @($Result.OutputLines)) {
        Write-Host $line
        if (-not [string]::IsNullOrWhiteSpace($LogFullPath)) {
            if ($null -eq $script:M01LogAuthority) {
                throw "log authority is not initialized: $LogFullPath"
            }
            Write-M01LogLines `
                -Authority $script:M01LogAuthority `
                -Lines @([string] $line)
        }
    }
}

function Invoke-M01BoundedProcess {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string] $FilePath,
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [string[]] $Arguments,
        [Parameter(Mandatory = $true)]
        [string] $WorkingDirectory,
        [Parameter(Mandatory = $true)]
        [ValidatePattern('^[a-z0-9][a-z0-9._-]*$')]
        [string] $Operation,
        [Parameter(Mandatory = $true)]
        [ValidateRange(1, 3600)]
        [int] $TimeoutSeconds,
        [Parameter(Mandatory = $true)]
        [ValidateRange(256, 67108864)]
        [int] $MaximumOutputCharacters,
        [System.Collections.IDictionary] $EnvironmentOverrides = @{}
    )

    $resolvedFilePath = (Resolve-Path -LiteralPath $FilePath).ProviderPath
    $resolvedWorkingDirectory = (
        Resolve-Path -LiteralPath $WorkingDirectory).ProviderPath
    if (-not (Test-Path -LiteralPath $resolvedFilePath -PathType Leaf)) {
        throw "bounded process executable is not a file: $resolvedFilePath"
    }
    if (-not (Test-Path -LiteralPath $resolvedWorkingDirectory -PathType Container)) {
        throw (
            "bounded process working directory is not a directory: " +
            $resolvedWorkingDirectory)
    }

    $powershellPath = Join-Path $env:SystemRoot (
        "System32\WindowsPowerShell\v1.0\powershell.exe")
    if (-not (Test-Path -LiteralPath $powershellPath -PathType Leaf)) {
        throw "Windows PowerShell executable missing: $powershellPath"
    }

    $argumentJson = ConvertTo-Json -InputObject @($Arguments) -Compress
    $encodedArguments = [System.Convert]::ToBase64String(
        [System.Text.Encoding]::UTF8.GetBytes($argumentJson))
    $encodedFilePath = [System.Convert]::ToBase64String(
        [System.Text.Encoding]::UTF8.GetBytes($resolvedFilePath))
    if ($encodedArguments.Length -gt 16384 -or
        $encodedFilePath.Length -gt 4096) {
        throw "bounded process launch payload exceeds the bounded contract"
    }

    $startupGateName =
        "Local\CLROS-M01-BuildProcess-" +
        [System.Guid]::NewGuid().ToString("N")
    $startupGateCreated = $false
    $startupGate = New-Object System.Threading.EventWaitHandle(
        $false,
        [System.Threading.EventResetMode]::ManualReset,
        $startupGateName,
        [ref] $startupGateCreated)
    if (-not $startupGateCreated) {
        $startupGate.Dispose()
        throw "bounded process startup gate name collision"
    }
$startupBootstrap = @'
$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"
$gate = [System.Threading.EventWaitHandle]::OpenExisting(
    $env:CLROS_M01_BUILD_STARTUP_GATE)
try {
    if (-not $gate.WaitOne(30000)) {
        exit 125
    }
}
finally {
    $gate.Dispose()
}
$targetPath = [System.Text.Encoding]::UTF8.GetString(
    [System.Convert]::FromBase64String(
        $env:CLROS_M01_BUILD_FILE_PATH))
$argumentJson = [System.Text.Encoding]::UTF8.GetString(
    [System.Convert]::FromBase64String(
        $env:CLROS_M01_BUILD_ARGUMENTS))
$targetArguments = @($argumentJson | ConvertFrom-Json)
$env:CLROS_M01_BUILD_STARTUP_GATE = $null
$env:CLROS_M01_BUILD_FILE_PATH = $null
$env:CLROS_M01_BUILD_ARGUMENTS = $null
& $targetPath @targetArguments
if ($null -eq $LASTEXITCODE) {
    exit 0
}
exit [int] $LASTEXITCODE
'@
    $encodedBootstrap = [System.Convert]::ToBase64String(
        [System.Text.Encoding]::Unicode.GetBytes($startupBootstrap))

    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $powershellPath
    $startInfo.Arguments =
        "-NoProfile -ExecutionPolicy Bypass -EncodedCommand " +
        $encodedBootstrap
    $startInfo.WorkingDirectory = $resolvedWorkingDirectory
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.EnvironmentVariables["CLROS_M01_BUILD_STARTUP_GATE"] =
        $startupGateName
    $startInfo.EnvironmentVariables["CLROS_M01_BUILD_FILE_PATH"] =
        $encodedFilePath
    $startInfo.EnvironmentVariables["CLROS_M01_BUILD_ARGUMENTS"] =
        $encodedArguments
    $startInfo.EnvironmentVariables["MSBUILDDISABLENODEREUSE"] = "1"
    $startInfo.EnvironmentVariables["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0"
    $startInfo.EnvironmentVariables["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1"
    $startInfo.EnvironmentVariables["DOTNET_NOLOGO"] = "1"
    foreach ($key in @($EnvironmentOverrides.Keys)) {
        $name = [string] $key
        $value = $EnvironmentOverrides[$key]
        if ($null -eq $value) {
            [void] $startInfo.EnvironmentVariables.Remove($name)
        }
        else {
            $startInfo.EnvironmentVariables[$name] = [string] $value
        }
    }

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $startInfo
    $processJob = $null
    $capture = $null
    $processStarted = $false
    $processAssigned = $false
    try {
        $processJob = New-M01ProcessJob
        if (-not $process.Start()) {
            throw "bounded process did not start: $Operation"
        }
        $processStarted = $true
        $processJob.Assign($process)
        $processAssigned = $true
        $capture = New-Object CLROS.Build.BoundedProcessOutput(
            $process.StandardOutput,
            $process.StandardError,
            $MaximumOutputCharacters)
        if (-not $startupGate.Set()) {
            throw "bounded process startup gate release failed: $Operation"
        }

        $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
        $completed = $false
        $timedOut = $false
        $outputLimitExceeded = $false
        while (-not $completed) {
            $completed = $process.WaitForExit(25)
            if ($capture.LimitExceeded) {
                $outputLimitExceeded = $true
                break
            }
            if ($stopwatch.Elapsed.TotalSeconds -ge $TimeoutSeconds) {
                $timedOut = $true
                break
            }
        }
        $stopwatch.Stop()
        $exitCode = if ($completed -and -not $outputLimitExceeded) {
            $process.ExitCode
        }
        else {
            -1
        }
        $terminationConfirmed = Stop-M01ProcessTree `
            -Process $process `
            -ProcessJob $processJob

        $outputDrainTask = [System.Threading.Tasks.Task]::WhenAll(
            [System.Threading.Tasks.Task[]] $capture.Tasks)
        $outputDrainWaitCompleted = $false
        try {
            $outputDrainWaitCompleted = $outputDrainTask.Wait(2000)
        }
        catch {
            $outputDrainWaitCompleted = $true
        }
        $outputCaptureSucceeded =
            $outputDrainWaitCompleted -and
            ($capture.Tasks | Where-Object {
                $_.Status -ne
                    [System.Threading.Tasks.TaskStatus]::RanToCompletion
            }).Count -eq 0
        $outputText = $capture.GetText()
        $outputLines = New-Object System.Collections.Generic.List[string]
        if (-not [string]::IsNullOrEmpty($outputText)) {
            foreach ($line in [System.Text.RegularExpressions.Regex]::Split(
                    $outputText.TrimEnd("`r", "`n"),
                    "\r\n|\n|\r")) {
                $outputLines.Add($line)
            }
        }

        return [pscustomobject] @{
            Operation = $Operation
            TimedOut = $timedOut
            OutputLimitExceeded =
                $outputLimitExceeded -or $capture.LimitExceeded
            ExitCode = $exitCode
            TerminationConfirmed = $terminationConfirmed
            OutputCaptureSucceeded = $outputCaptureSucceeded
            OutputDrainTimedOut = -not $outputDrainWaitCompleted
            OutputCharacters = $capture.CapturedCharacters
            OutputText = $outputText
            OutputLines = $outputLines.ToArray()
            ElapsedMilliseconds = $stopwatch.ElapsedMilliseconds
        }
    }
    finally {
        $startupGate.Dispose()
        if ($null -ne $processJob) {
            $processJob.Dispose()
        }
        if ($processStarted -and -not $processAssigned) {
            try {
                if (-not $process.HasExited) {
                    $process.Kill()
                }
                [void] $process.WaitForExit(1000)
            }
            catch {
                # Launch failure remains authoritative.
            }
        }
        if ($null -ne $capture) {
            $capture.Dispose()
        }
        $process.Dispose()
    }
}

function Invoke-M01ArtifactVerifier {
    param(
        [string[]] $Arguments,
        [int] $TimeoutSeconds
    )

    $powershellPath = Join-Path $env:SystemRoot (
        "System32\WindowsPowerShell\v1.0\powershell.exe")
    if (-not (Test-Path -LiteralPath $powershellPath -PathType Leaf)) {
        throw "Windows PowerShell executable missing: $powershellPath"
    }

    $argumentJson = ConvertTo-Json -InputObject @($Arguments) -Compress
    $encodedArguments = [System.Convert]::ToBase64String(
        [System.Text.Encoding]::UTF8.GetBytes($argumentJson))
    if ($encodedArguments.Length -gt 16384) {
        throw "artifact verifier argument payload exceeds the bounded launch contract"
    }
    $startupGateName =
        "Local\CLROS-M01-ArtifactVerifier-" +
        [System.Guid]::NewGuid().ToString("N")
    $startupGateCreated = $false
    $startupGate = New-Object System.Threading.EventWaitHandle(
        $false,
        [System.Threading.EventResetMode]::ManualReset,
        $startupGateName,
        [ref] $startupGateCreated)
    if (-not $startupGateCreated) {
        $startupGate.Dispose()
        throw "artifact verifier startup gate name collision"
    }
$startupBootstrap = @'
$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"
$gate = [System.Threading.EventWaitHandle]::OpenExisting(
    $env:CLROS_M01_VERIFIER_STARTUP_GATE)
try {
    if (-not $gate.WaitOne(30000)) {
        exit 125
    }
}
finally {
    $gate.Dispose()
}
$argumentBytes = [System.Convert]::FromBase64String(
    $env:CLROS_M01_VERIFIER_ARGUMENTS)
$argumentJson = [System.Text.Encoding]::UTF8.GetString($argumentBytes)
$parsedArguments = $argumentJson | ConvertFrom-Json
$verifierArguments = @($parsedArguments)
if ($verifierArguments.Count -lt 5 -or
    [string]$verifierArguments[0] -cne "-NoProfile" -or
    [string]$verifierArguments[1] -cne "-ExecutionPolicy" -or
    [string]$verifierArguments[2] -cne "Bypass" -or
    [string]$verifierArguments[3] -cne "-File") {
    exit 126
}
$verifierScript = [string]$verifierArguments[4]
if (($verifierArguments.Count - 5) % 2 -ne 0) {
    exit 127
}
$verifierParameters = @{}
for ($index = 5; $index -lt $verifierArguments.Count; $index += 2) {
    $parameterToken = [string]$verifierArguments[$index]
    if ($parameterToken -cnotmatch '^-[A-Za-z][A-Za-z0-9]*$') {
        exit 127
    }
    $parameterName = $parameterToken.Substring(1)
    if ($verifierParameters.ContainsKey($parameterName)) {
        exit 127
    }
    $parameterValue = [string]$verifierArguments[$index + 1]
    if ($parameterName -ceq "EnableM04IndependentEndianConformanceProbe" -or
        $parameterName -ceq "EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe" -or
        $parameterName -ceq "EnableM04ArithmeticConformanceProbe" -or
        $parameterName -ceq "EnableM04LayoutContractProbe" -or
        $parameterName -ceq "EnableM04ManagedHeapPageRangeProbe" -or
        $parameterName -ceq "EnableM04RhpNewFastProbe" -or
        $parameterName -ceq "EnableM04RhpNewFastBeforeBindFailureProbe" -or
        $parameterName -ceq "EnableM04RhpNewFastExhaustionFailureProbe" -or
        $parameterName -ceq "EnableM04SzArrayAllocationProbe" -or
        $parameterName -ceq "EnableM04SzArrayAllocationBeforeBindProbe" -or
        $parameterName -ceq "EnableM04SzArrayAllocationExhaustionProbe" -or
        $parameterName -ceq "EnableM04StringAllocationProbe" -or
        $parameterName -ceq "EnableM04StringAllocationBeforeBindProbe" -or
        $parameterName -ceq "EnableM04StringAllocationExhaustionProbe" -or
        $parameterName -ceq "EnableM04StringAllocationInvalidLengthProbe" -or
        $parameterName -ceq "EnableM03MemoryMapCaptureProbe" -or
        $parameterName -ceq "EnableM03FirmwareEntropySeedProbe" -or
        $parameterName -ceq "EnableM04TypeManagerModuleRegistrationProbe" -or
        $parameterName -ceq "EnableM04X64UnwindMetadataProbe" -or
        $parameterName -ceq "EnableM04SimpleThrowCatchProbe" -or
        $parameterName -ceq "EnableM04GcStaticRootMaterializationProbe" -or
        $parameterName -ceq "EnableM04SeededCollectorChurnProbe" -or
        $parameterName -ceq "EnableM03TypedExitBootServicesCallProbe" -or
        $parameterName -ceq "EnableM03FirstRealExitBootServicesProbe" -or
        $parameterName -ceq "EnableM03FirmwareAdapterRevocationProbe" -or
        $parameterName -ceq "EnableM03OwnedSubsystemSurvivalProbe" -or
        $parameterName -ceq "EnableM03OwnedSubsystemPanicProbe" -or
        $parameterName -ceq "EnableM04ClassConstructorExecutionProbe" -or
        $parameterName -ceq "EnableM04InterfaceDispatchProbe" -or
        $parameterName -ceq "EnableM04CheckedReferenceBarrierProbe" -or
        $parameterName -ceq "EnableM01LoadedImageStackProbe" -or
        $parameterName -ceq "EnableM01LiveAllocatorProbe" -or
        $parameterName -ceq "EnableM01OwnedPagingProbe" -or
        $parameterName -ceq "EnableM02SafeStubProbe" -or
        $parameterName -ceq "EnableM02CheckedPortIoBindingProbe" -or
        $parameterName -ceq "EnableM02Com1OwnedPortIoProbe" -or
        $parameterName -ceq "EnableUefiGoneSimulationProbe" -or
        $parameterName -ceq "EnableExitBootServicesFreshMapProbe" -or
        $parameterName -ceq "EnableAcpiPhysicalDiscoveryProbe" -or
        $parameterName -ceq "EnableM02NormalizedInterruptFramePageProbe" -or
        $parameterName -ceq "EnableM02OwnedIdtGatePopulationProbe" -or
        $parameterName -ceq "EnableM02IdtrContinuedExecutionProbe" -or
        $parameterName -ceq "EnableM02Ud2VectorStateProbe" -or
        $parameterName -ceq "EnableM02PageFaultCr2ErrorCodeProbe" -or
        $parameterName -ceq "EnableM02DoubleFaultIstProbe" -or
        $parameterName -ceq "EnableM02UnexpectedVectorPanicProbe" -or
        $parameterName -ceq "EnableM02OwnedCom1ReadyProbe" -or
        $parameterName -ceq "EnableM02PanicSafeOwnedCom1Probe" -or
        $parameterName -ceq "EnableM02OwnedX64GdtEncodingProbe" -or
        $parameterName -ceq "EnableM02GuardedIstStackAllocationProbe" -or
        $parameterName -ceq "EnableM02OwnedGdtTssTerminalProbe") {
        if ($parameterValue -cne "true" -and $parameterValue -cne "false") {
            exit 127
        }
        $verifierParameters.Add(
            $parameterName,
            [System.Boolean]::Parse($parameterValue))
    }
    else {
        $verifierParameters.Add($parameterName, $parameterValue)
    }
}
& $verifierScript @verifierParameters
exit $LASTEXITCODE
'@
    $encodedBootstrap = [System.Convert]::ToBase64String(
        [System.Text.Encoding]::Unicode.GetBytes($startupBootstrap))

    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $powershellPath
    $startInfo.Arguments =
        "-NoProfile -ExecutionPolicy Bypass -EncodedCommand " +
        $encodedBootstrap
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.EnvironmentVariables["CLROS_M01_VERIFIER_STARTUP_GATE"] =
        $startupGateName
    $startInfo.EnvironmentVariables["CLROS_M01_VERIFIER_ARGUMENTS"] =
        $encodedArguments

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $startInfo
    $standardOutputReader = $null
    $standardErrorReader = $null
    $standardOutputTask = $null
    $standardErrorTask = $null
    $processJob = $null
    $processStarted = $false
    $processAssigned = $false
    try {
        $processJob = New-M01ProcessJob
        if (-not $process.Start()) {
            throw "artifact verifier process did not start"
        }
        $processStarted = $true
        $processJob.Assign($process)
        $processAssigned = $true
        $standardOutputReader = $process.StandardOutput
        $standardErrorReader = $process.StandardError
        $standardOutputTask = $standardOutputReader.ReadToEndAsync()
        $standardErrorTask = $standardErrorReader.ReadToEndAsync()
        if (-not $startupGate.Set()) {
            throw "artifact verifier startup gate release failed"
        }
        $completed = $process.WaitForExit($TimeoutSeconds * 1000)
        $exitCode = if ($completed) { $process.ExitCode } else { -1 }
        $terminationConfirmed = Stop-M01ProcessTree `
            -Process $process `
            -ProcessJob $processJob

        $outputDrainTask = [System.Threading.Tasks.Task]::WhenAll(
            [System.Threading.Tasks.Task[]] @(
                $standardOutputTask,
                $standardErrorTask
            ))
        $outputDrainWaitCompleted = $false
        $outputDrainDeadlineExceeded = $false
        try {
            $outputDrainWaitCompleted = $outputDrainTask.Wait(2000)
        }
        catch {
            $outputDrainWaitCompleted = $true
        }
        if (-not $outputDrainWaitCompleted) {
            $outputDrainDeadlineExceeded = $true
            $terminationConfirmed =
                (Stop-M01ProcessTree `
                    -Process $process `
                    -ProcessJob $processJob) -and
                $terminationConfirmed
            try {
                $outputDrainWaitCompleted = $outputDrainTask.Wait(1000)
            }
            catch {
                $outputDrainWaitCompleted = $true
            }
        }

        $outputCaptureSucceeded =
            $outputDrainWaitCompleted -and
            $standardOutputTask.Status -eq
                [System.Threading.Tasks.TaskStatus]::RanToCompletion -and
            $standardErrorTask.Status -eq
                [System.Threading.Tasks.TaskStatus]::RanToCompletion
        $standardOutput = if ($outputCaptureSucceeded) {
            $standardOutputTask.GetAwaiter().GetResult()
        }
        else {
            ""
        }
        $standardError = if ($outputCaptureSucceeded) {
            $standardErrorTask.GetAwaiter().GetResult()
        }
        else {
            ""
        }
        $outputLines = New-Object System.Collections.Generic.List[string]
        foreach ($text in @($standardOutput, $standardError)) {
            if ([string]::IsNullOrEmpty($text)) {
                continue
            }
            foreach ($line in [System.Text.RegularExpressions.Regex]::Split(
                    $text.TrimEnd("`r", "`n"),
                    "\r\n|\n|\r")) {
                $outputLines.Add($line)
            }
        }

        return [pscustomobject] @{
            TimedOut = -not $completed
            ExitCode = $exitCode
            TerminationConfirmed = $terminationConfirmed
            OutputCaptureSucceeded = $outputCaptureSucceeded
            OutputDrainTimedOut = $outputDrainDeadlineExceeded
            OutputLines = $outputLines.ToArray()
        }
    }
    finally {
        $startupGate.Dispose()
        if ($null -ne $processJob) {
            $processJob.Dispose()
        }
        if ($processStarted -and -not $processAssigned) {
            try {
                if (-not $process.HasExited) {
                    $process.Kill()
                    [void] $process.WaitForExit(1000)
                }
            }
            catch {
                # Launch failure remains authoritative.
            }
        }
        if ($null -ne $standardOutputReader -and
            ($null -eq $standardOutputTask -or
                -not $standardOutputTask.IsCompleted)) {
            $standardOutputReader.Dispose()
        }
        if ($null -ne $standardErrorReader -and
            ($null -eq $standardErrorTask -or
                -not $standardErrorTask.IsCompleted)) {
            $standardErrorReader.Dispose()
        }
        $process.Dispose()
    }
}

function Test-NativeAotRuntimePack {
    param([string] $Path)

    try {
        $runtimeListPath = Join-Path $Path "data\RuntimeList.xml"
        if (-not (Test-Path -LiteralPath $runtimeListPath -PathType Leaf) -or
            (Get-Item -LiteralPath $runtimeListPath).Length -eq 0) {
            return $false
        }

        $runtimeList = Read-M01SecureXmlDocument -Path $runtimeListPath
        return $null -ne $runtimeList.DocumentElement -and
            $runtimeList.DocumentElement.LocalName -eq "FileList"
    }
    catch {
        return $false
    }
}

function Get-OvmfCodeImageEvidence {
    param([string] $Path)

    $resolvedPath = Resolve-ExactFilePath -Candidate $Path
    if ($null -eq $resolvedPath) {
        return [pscustomobject] @{
            Status = "missing"
            Path = ""
            Length = 0L
            Sha256 = ""
        }
    }

    try {
        $stream = [System.IO.File]::Open(
            $resolvedPath,
            [System.IO.FileMode]::Open,
            [System.IO.FileAccess]::Read,
            [System.IO.FileShare]::Read)
        try {
            $length = $stream.Length
            $signatureFound = $false

            $probeLength = [int] [System.Math]::Min(4096L, $length)
            $probe = New-Object byte[] $probeLength
            $probeBytesRead = 0
            while ($probeBytesRead -lt $probe.Length) {
                $read = $stream.Read(
                    $probe,
                    $probeBytesRead,
                    $probe.Length - $probeBytesRead)
                if ($read -eq 0) {
                    break
                }
                $probeBytesRead += $read
            }

            for ($index = 0; $index -le $probeBytesRead - 4; $index++) {
                if ($probe[$index] -eq 0x5f -and
                    $probe[$index + 1] -eq 0x46 -and
                    $probe[$index + 2] -eq 0x56 -and
                    $probe[$index + 3] -eq 0x48) {
                    $signatureFound = $true
                    break
                }
            }

            if ($length -lt 65536 -or -not $signatureFound) {
                return [pscustomobject] @{
                    Status = "invalid"
                    Path = $resolvedPath
                    Length = $length
                    Sha256 = ""
                }
            }

            [void] $stream.Seek(0L, [System.IO.SeekOrigin]::Begin)
            $sha256 = [System.Security.Cryptography.SHA256]::Create()
            try {
                $hashBytes = $sha256.ComputeHash($stream)
            }
            finally {
                $sha256.Dispose()
            }

            return [pscustomobject] @{
                Status = "ok"
                Path = $resolvedPath
                Length = $length
                Sha256 = [System.BitConverter]::ToString($hashBytes).Replace("-", "")
            }
        }
        finally {
            $stream.Dispose()
        }
    }
    catch {
        return [pscustomobject] @{
            Status = "evidence_unavailable"
            Path = $resolvedPath
            Length = 0L
            Sha256 = ""
        }
    }
}

$script:OvmfCodeImageEvidenceByPath = @{}
$script:OvmfCodeImageEvidenceAttempts =
    New-Object System.Collections.Generic.List[object]

function Get-CachedOvmfCodeImageEvidence {
    param([string] $Path)

    $resolvedPath = Resolve-ExactFilePath -Candidate $Path
    if ($null -ne $resolvedPath -and
        $script:OvmfCodeImageEvidenceByPath.ContainsKey($resolvedPath)) {
        return $script:OvmfCodeImageEvidenceByPath[$resolvedPath]
    }

    $evidence = Get-OvmfCodeImageEvidence -Path $Path
    if (-not [string]::IsNullOrWhiteSpace($evidence.Path)) {
        $script:OvmfCodeImageEvidenceByPath[$evidence.Path] = $evidence
    }
    $script:OvmfCodeImageEvidenceAttempts.Add($evidence)
    return $evidence
}

function Test-OvmfCodeImage {
    param([string] $Path)

    return (Get-CachedOvmfCodeImageEvidence -Path $Path).Status -eq "ok"
}

function Resolve-ExactFilePath {
    param([string] $Candidate)

    if ([string]::IsNullOrWhiteSpace($Candidate) -or
        -not (Test-Path -LiteralPath $Candidate -PathType Leaf)) {
        return $null
    }

    return (Resolve-Path -LiteralPath $Candidate).ProviderPath
}

function Resolve-ExactDirectoryPath {
    param([string] $Candidate)

    if ([string]::IsNullOrWhiteSpace($Candidate) -or
        -not (Test-Path -LiteralPath $Candidate -PathType Container)) {
        return $null
    }

    return (Resolve-Path -LiteralPath $Candidate).ProviderPath
}

function Resolve-ExecutableToolPath {
    param(
        [string] $RequestedPath,
        [string[]] $SearchRoot,
        [string] $FileName,
        [string[]] $DefaultPath,
        [string[]] $CommandName,
        [scriptblock] $Validator = $null
    )

    if (-not [string]::IsNullOrWhiteSpace($RequestedPath)) {
        $resolvedRequested = Resolve-ExactFilePath -Candidate $RequestedPath
        if ($null -eq $resolvedRequested -or
            ($null -ne $Validator -and -not (& $Validator $resolvedRequested))) {
            return $null
        }

        return $resolvedRequested
    }

    if ($SearchRoot.Count -gt 0) {
        return Resolve-ToolPath `
            -SearchRoot $SearchRoot `
            -FileName $FileName `
            -PathPattern "" `
            -Validator $Validator
    }

    foreach ($candidate in $DefaultPath) {
        $resolved = Resolve-ExactFilePath -Candidate $candidate
        if ($null -ne $resolved -and
            ($null -eq $Validator -or (& $Validator $resolved))) {
            return $resolved
        }
    }

    foreach ($command in $CommandName) {
        $resolvedCommand = Get-Command $command -CommandType Application -ErrorAction SilentlyContinue |
            Select-Object -First 1
        if ($null -ne $resolvedCommand -and
            ($null -eq $Validator -or (& $Validator $resolvedCommand.Source))) {
            return $resolvedCommand.Source
        }
    }

    return $null
}

function Resolve-NativeAotRuntimePackPath {
    param(
        [string] $RequestedPath,
        [string[]] $SearchRoot,
        [string] $DefaultPath
    )

    if (-not [string]::IsNullOrWhiteSpace($RequestedPath)) {
        $resolvedRequested = Resolve-ExactDirectoryPath -Candidate $RequestedPath
        if ($null -eq $resolvedRequested -or
            -not (Test-NativeAotRuntimePack -Path $resolvedRequested)) {
            return $null
        }

        return $resolvedRequested
    }

    if ($SearchRoot.Count -gt 0) {
        $candidatePacks = New-Object System.Collections.Generic.List[string]
        $seenPacks = New-Object 'System.Collections.Generic.HashSet[string]' (
            [System.StringComparer]::OrdinalIgnoreCase)
        foreach ($candidateRoot in $SearchRoot) {
            $resolvedRoot = Resolve-ExactDirectoryPath -Candidate $candidateRoot
            if ($null -eq $resolvedRoot) {
                continue
            }

            if ($seenPacks.Add($resolvedRoot) -and
                (Test-Path -LiteralPath (Join-Path $resolvedRoot "data\RuntimeList.xml") -PathType Leaf)) {
                $candidatePacks.Add($resolvedRoot)
            }

            $runtimeLists = Get-ChildItem -LiteralPath $resolvedRoot -Recurse -File -Filter "RuntimeList.xml" -ErrorAction SilentlyContinue |
                Where-Object { $_.Directory.Name -eq "data" }
            foreach ($runtimeList in $runtimeLists) {
                $packPath = $runtimeList.Directory.Parent.FullName
                if ($seenPacks.Add($packPath)) {
                    $candidatePacks.Add($packPath)
                }
            }
        }

        $orderedPacks = $candidatePacks |
            Sort-Object `
                @{ Expression = { Get-NumericPathVersion -Path $_ }; Descending = $true },
                @{ Expression = { $_ }; Descending = $false }
        foreach ($candidatePack in $orderedPacks) {
            if (Test-NativeAotRuntimePack -Path $candidatePack) {
                return $candidatePack
            }
        }

        return $null
    }

    $resolvedDefault = Resolve-ExactDirectoryPath -Candidate $DefaultPath
    if ($null -ne $resolvedDefault -and
        (Test-NativeAotRuntimePack -Path $resolvedDefault)) {
        return $resolvedDefault
    }

    return $null
}

function Get-PackageReferenceVersion {
    param(
        [string] $ProjectPath,
        [string] $PackageId
    )

    try {
        $project = Read-M01SecureXmlDocument -Path $ProjectPath
        $packageReference = $project.Project.ItemGroup.PackageReference |
            Where-Object { $_.Include -eq $PackageId } |
            Select-Object -First 1
        if ($null -ne $packageReference -and
            -not [string]::IsNullOrWhiteSpace($packageReference.Version)) {
            return [string] $packageReference.Version
        }
    }
    catch {
        return $null
    }

    return $null
}

function Get-VersionFromPath {
    param([string] $Path)

    $matches = [System.Text.RegularExpressions.Regex]::Matches(
        $Path,
        '(?<!\d)\d+\.\d+(?:\.\d+){0,2}(?!\d)')
    if ($matches.Count -eq 0) {
        return "unknown"
    }

    return $matches[$matches.Count - 1].Value
}

function Get-FileVersionEvidence {
    param([string] $Path)

    try {
        $versionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($Path)
        foreach ($candidate in @($versionInfo.FileVersion, $versionInfo.ProductVersion)) {
            if (-not [string]::IsNullOrWhiteSpace($candidate)) {
                return $candidate.Trim()
            }
        }
    }
    catch {
        # Version metadata is optional for injected test fixtures.
    }

    return Get-VersionFromPath -Path $Path
}

function Stop-VersionProbeProcess {
    param([System.Diagnostics.Process] $Process)

    try {
        if (-not $Process.HasExited) {
            $taskKill = Get-Command "taskkill.exe" -CommandType Application -ErrorAction SilentlyContinue
            if ($null -ne $taskKill) {
                & $taskKill.Source /PID $Process.Id /T /F 2>$null | Out-Null
            }
            elseif (-not $Process.HasExited) {
                $Process.Kill()
            }
        }
    }
    catch {
        # The bounded caller reports timeout even if cleanup races process exit.
    }
}

function Get-ExecutableVersionEvidence {
    param(
        [string] $Path,
        [int] $TimeoutMilliseconds
    )

    try {
        $startInfo = New-Object System.Diagnostics.ProcessStartInfo
        $extension = [System.IO.Path]::GetExtension($Path)
        if ($extension -eq ".cmd" -or $extension -eq ".bat") {
            $startInfo.FileName = $env:ComSpec
            $startInfo.Arguments = '/d /s /c ""' + $Path + '" --version"'
        }
        else {
            $startInfo.FileName = $Path
            $startInfo.Arguments = "--version"
        }

        $startInfo.UseShellExecute = $false
        $startInfo.CreateNoWindow = $true
        $startInfo.RedirectStandardOutput = $true
        $startInfo.RedirectStandardError = $true

        $process = New-Object System.Diagnostics.Process
        $process.StartInfo = $startInfo
        if (-not $process.Start()) {
            return [pscustomobject] @{
                Status = "failed"
                Version = $null
            }
        }

        $standardOutput = $process.StandardOutput.ReadToEndAsync()
        $standardError = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit($TimeoutMilliseconds)) {
            Stop-VersionProbeProcess -Process $process
            return [pscustomobject] @{
                Status = "timeout"
                Version = $null
            }
        }

        $process.WaitForExit()
        if ($process.ExitCode -ne 0) {
            return [pscustomobject] @{
                Status = "failed"
                Version = $null
            }
        }

        $standardOutputText = $standardOutput.GetAwaiter().GetResult()
        $standardErrorText = $standardError.GetAwaiter().GetResult()
        $versionLines =
            @($standardOutputText -split "`r?`n") +
            @($standardErrorText -split "`r?`n")
        foreach ($line in $versionLines) {
            if (-not [string]::IsNullOrWhiteSpace($line)) {
                return [pscustomobject] @{
                    Status = "ok"
                    Version = $line.Trim()
                }
            }
        }
    }
    catch {
        return [pscustomobject] @{
            Status = "failed"
            Version = $null
        }
    }

    return [pscustomobject] @{
        Status = "failed"
        Version = $null
    }
}

function New-M01SourceClosureResult {
    param(
        [bool] $Succeeded,
        [string] $Reason,
        [string] $Detail,
        [int] $CompileItemCount = 0,
        [int] $ReviewedSourceCount = 0,
        [int] $PrerequisiteSourceCount = 0
    )

    return [pscustomobject] @{
        Succeeded = $Succeeded
        Reason = $Reason
        Detail = $Detail
        CompileItemCount = $CompileItemCount
        ReviewedSourceCount = $ReviewedSourceCount
        PrerequisiteSourceCount = $PrerequisiteSourceCount
    }
}

function Get-NostdlibEfiActiveSource {
    param(
        [string] $SourceText,
        [string] $SourceName
    )

    $activeText = New-Object System.Text.StringBuilder
    $frames = New-Object System.Collections.ArrayList
    $currentActive = $true
    $lines = [System.Text.RegularExpressions.Regex]::Split(
        $SourceText,
        "\r\n|\n|\r")

    foreach ($line in $lines) {
        $ifMatch = [System.Text.RegularExpressions.Regex]::Match(
            $line,
            '^\s*#if\s+(.+?)\s*$')
        if ($ifMatch.Success) {
            $conditionText = $ifMatch.Groups[1].Value
            if (($conditionText -ne "!NOSTDLIB_EFI") -or
                ($frames.Count -ne 0)) {
                return [pscustomobject] @{
                    Succeeded = $false
                    Reason = "ambiguous_preprocessor_condition"
                    Detail = $SourceName + ":" + $conditionText
                    ActiveText = ""
                }
            }

            [void] $frames.Add([pscustomobject] @{
                ParentActive = $currentActive
                ConditionActive = $false
            })
            $currentActive = $false
            continue
        }

        if ($line -match '^\s*#(?:elif|else)\b') {
            return [pscustomobject] @{
                Succeeded = $false
                Reason = "ambiguous_preprocessor_condition"
                Detail = $SourceName + ":alternate-branch"
                ActiveText = ""
            }
            continue
        }

        if ($line -match '^\s*#endif\s*$') {
            if ($frames.Count -eq 0) {
                return [pscustomobject] @{
                    Succeeded = $false
                    Reason = "unbalanced_preprocessor_condition"
                    Detail = $SourceName + ":#endif"
                    ActiveText = ""
                }
            }

            $frame = $frames[$frames.Count - 1]
            $frames.RemoveAt($frames.Count - 1)
            $currentActive = $frame.ParentActive
            continue
        }

        if ($line -match '^\s*#if\b') {
            return [pscustomobject] @{
                Succeeded = $false
                Reason = "ambiguous_preprocessor_condition"
                Detail = $SourceName + ":malformed-if"
                ActiveText = ""
            }
        }

        if ($line -match '^\s*#') {
            return [pscustomobject] @{
                Succeeded = $false
                Reason = "unsupported_preprocessor_directive"
                Detail = $SourceName + ":" + $line.Trim()
                ActiveText = ""
            }
        }

        if ($currentActive) {
            [void] $activeText.AppendLine($line)
        }
    }

    if ($frames.Count -ne 0) {
        return [pscustomobject] @{
            Succeeded = $false
            Reason = "unbalanced_preprocessor_condition"
            Detail = $SourceName + ":missing-endif"
            ActiveText = ""
        }
    }

    return [pscustomobject] @{
        Succeeded = $true
        Reason = "ok"
        Detail = ""
        ActiveText = $activeText.ToString()
    }
}

function Test-M01ActiveSourceSurface {
    param(
        [string] $SourceText,
        [string] $SourceName
    )

    # The reviewed scalar layout leaf uses compiler attributes, const version
    # strings and an enum member named String. Admit only that complete frozen
    # source, not those constructs in arbitrary kernel input. Normalize CRLF
    # solely for repository-text identity; publish-authority hashes stay raw.
    if ([string]::Equals($SourceName,
            '..\OS.Kernel\Runtime\ObjectArrayStringEETypeMethodTableLayoutContracts.cs',
            [System.StringComparison]::Ordinal)) {
        $identity = Get-M01Utf8Sha256 -Text ($SourceText.Replace("`r`n", "`n"))
        if (-not [string]::Equals($identity,
                '44f8a34e2f1911aae4fd5b028adb88bf1d8bf06ce915625e9f13d11964f579aa',
                [System.StringComparison]::Ordinal)) {
            return [pscustomobject] @{
                Succeeded = $false
                Reason = "forbidden_active_source"
                Detail = $SourceName + ":reviewed_layout_source_identity"
                ActiveText = ""
            }
        }
        return [pscustomobject] @{
            Succeeded = $true
            Reason = "ok"
            Detail = ""
            ActiveText = $SourceText
        }
    }

    # Frozen heap leaf: imports POD memory contracts and the existing NoInlining shim.
    # Admission is exact path plus complete text identity; raw authority hashes stay raw.
    if ([string]::Equals($SourceName,
            '..\OS.Kernel\Runtime\ManagedHeapPageRangeLiveAllocator.cs',
            [System.StringComparison]::Ordinal)) {
        $identity = Get-M01Utf8Sha256 -Text ($SourceText.Replace("`r`n", "`n"))
        if (-not [string]::Equals($identity,
                '43cdf5af449f7963f07b200ec494da3d8538f1ceb2f01fb3dbe9c29edebcb070',
                [System.StringComparison]::Ordinal)) {
            return [pscustomobject] @{
                Succeeded = $false
                Reason = "forbidden_active_source"
                Detail = $SourceName + ":reviewed_heap_source_identity"
                ActiveText = ""
            }
        }
        return [pscustomobject] @{
            Succeeded = $true
            Reason = "ok"
            Detail = ""
            ActiveText = $SourceText
        }
    }

    # Reviewed #76/#82 allocator: original POD authority, bounded allocation records and same-owner reuse.
    # Only this complete reviewed source is admitted; publish hashes remain raw.
    if ([string]::Equals($SourceName,
            '..\OS.Kernel\Runtime\GuestTestRhpNewFast.cs',
            [System.StringComparison]::Ordinal)) {
        $identity = Get-M01Utf8Sha256 -Text ($SourceText.Replace("`r`n", "`n"))
        if (-not [string]::Equals($identity,
                'e7cd46a4c35de92cb9085f198737916697f7479dd0947fd591e4874aa347617e',
                [System.StringComparison]::Ordinal)) {
            return [pscustomobject] @{ Succeeded=$false; Reason="forbidden_active_source"; Detail=$SourceName + ":reviewed_rhp_new_fast_source_identity"; ActiveText="" }
        }
        return [pscustomobject] @{ Succeeded=$true; Reason="ok"; Detail=""; ActiveText=$SourceText }
    }

    # The two new retained authority leaves are admitted by complete source identity.
    if ([string]::Equals($SourceName, '..\OS.Kernel\Boot\ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.cs', [StringComparison]::Ordinal)) {
        $identity = Get-M01Utf8Sha256 -Text ($SourceText.Replace("`r`n", "`n"))
        if ($identity -cne '9c42726c9c0b182dabafd06003d56ef6242469e8d412ea248f1918f9311d1434') { return [pscustomobject]@{ Succeeded=$false; Reason='forbidden_active_source'; Detail=$SourceName+':reviewed_retained_authority_source_identity'; ActiveText='' } }
        return [pscustomobject]@{ Succeeded=$true; Reason='ok'; Detail=''; ActiveText=$SourceText }
    }
    if ([string]::Equals($SourceName, '..\OS.Kernel\Runtime\CompilerClassConstructorExecution.cs', [StringComparison]::Ordinal)) {
        $identity = Get-M01Utf8Sha256 -Text ($SourceText.Replace("`r`n", "`n"))
        if ($identity -cne '1c57cb96a890c9bfdb551d770826ce40a7f2ba0f86bbb305edb20eb386bd5d7f') { return [pscustomobject]@{ Succeeded=$false; Reason='forbidden_active_source'; Detail=$SourceName+':reviewed_retained_authority_source_identity'; ActiveText='' } }
        return [pscustomobject]@{ Succeeded=$true; Reason='ok'; Detail=''; ActiveText=$SourceText }
    }

    # Reviewed bounded dispatch core: original module/allocator authority and exact native extents.
    if ([string]::Equals($SourceName, '..\OS.Kernel\Runtime\CompilerInterfaceDispatch.cs', [StringComparison]::Ordinal)) {
        $identity = Get-M01Utf8Sha256 -Text ($SourceText.Replace("`r`n", "`n"))
        if ($identity -cne '6c95c98406305e84e9653aaaedcee7812a6dc1c4a516bba3db04256d3f068a64') { return [pscustomobject]@{ Succeeded=$false; Reason='forbidden_active_source'; Detail=$SourceName+':reviewed_dispatch_source_identity'; ActiveText='' } }
        return [pscustomobject]@{ Succeeded=$true; Reason='ok'; Detail=''; ActiveText=$SourceText }
    }

    if ([string]::Equals($SourceName, '..\OS.Kernel\Runtime\ExceptionTypeMatch.cs', [StringComparison]::Ordinal)) {
        $identity = Get-M01Utf8Sha256 -Text ($SourceText.Replace("`r`n", "`n"))
        if ($identity -cne 'e45c75ad8e6286c25d02346555a54b85f93dc6123755821a355012569f3138d5') { return [pscustomobject]@{ Succeeded=$false; Reason='forbidden_active_source'; Detail=$SourceName+':reviewed_exception_leaf_source_identity'; ActiveText='' } }
        return [pscustomobject]@{ Succeeded=$true; Reason='ok'; Detail=''; ActiveText=$SourceText }
    }
    if ([string]::Equals($SourceName, '..\OS.Kernel\Runtime\X64FrameUnwind.cs', [StringComparison]::Ordinal)) {
        $identity = Get-M01Utf8Sha256 -Text ($SourceText.Replace("`r`n", "`n"))
        if ($identity -cne 'e8ef25590186dbcf872deed818ffc508c5db7a2f092592e3c09a1127a8a122ce') { return [pscustomobject]@{ Succeeded=$false; Reason='forbidden_active_source'; Detail=$SourceName+':reviewed_exception_leaf_source_identity'; ActiveText='' } }
        return [pscustomobject]@{ Succeeded=$true; Reason='ok'; Detail=''; ActiveText=$SourceText }
    }
    if ([string]::Equals($SourceName, '..\OS.Kernel\Runtime\NativeAotEhClauseMetadata.cs', [StringComparison]::Ordinal)) {
        $identity = Get-M01Utf8Sha256 -Text ($SourceText.Replace("`r`n", "`n"))
        if ($identity -cne '7f770a669630c568dbf782bf89a4e1bdc990ed2c966c0a1e1efb55b1b47d5f62') { return [pscustomobject]@{ Succeeded=$false; Reason='forbidden_active_source'; Detail=$SourceName+':reviewed_exception_leaf_source_identity'; ActiveText='' } }
        return [pscustomobject]@{ Succeeded=$true; Reason='ok'; Detail=''; ActiveText=$SourceText }
    }

    # Reviewed metadata-only core; retain its exact bounded version-one decoder.
    if ([string]::Equals($SourceName, '..\OS.Kernel\Runtime\X64PERuntimeFunctionUnwindMetadataHostileInput.cs', [StringComparison]::Ordinal)) {
        $identity = Get-M01Utf8Sha256 -Text ($SourceText.Replace("`r`n", "`n"))
        if ($identity -cne '12fac853b9dc2357d25315e4bedd0e43df0af76854079b7ae030bca36aed5253') { return [pscustomobject]@{ Succeeded=$false; Reason='forbidden_active_source'; Detail=$SourceName+':reviewed_x64_unwind_metadata_source_identity'; ActiveText='' } }
        return [pscustomobject]@{ Succeeded=$true; Reason='ok'; Detail=''; ActiveText=$SourceText }
    }

    # Only the two compile-time branches of the reviewed probe catalog are admitted.
    # Native compilation excludes the managed catalog and retains the explicit toggle.
    if ([string]::Equals($SourceName, '..\OS.Kernel\Diagnostics\Probes.cs', [StringComparison]::Ordinal)) {
        $identity = Get-M01Utf8Sha256 -Text ($SourceText.Replace("`r`n", "`n"))
        if ($identity -cne '0055406938d41636ae14e616eebcd6da6941406f02bdd8cdc53d65237bccd408') { return [pscustomobject]@{ Succeeded=$false; Reason='forbidden_active_source'; Detail=$SourceName+':reviewed_first_exit_probe_identity'; ActiveText='' } }
        return [pscustomobject]@{ Succeeded=$true; Reason='ok'; Detail=''; ActiveText=$SourceText }
    }

    # Exact reviewed allocator operation; never admit a changed leaf by name alone.
    if ([string]::Equals($SourceName, '..\OS.Kernel\Boot\OwnedAllocatorDescriptorTablesACPISnapshotPanicOutput.cs', [StringComparison]::Ordinal)) {
        $identity = Get-M01Utf8Sha256 -Text ($SourceText.Replace("`r`n", "`n"))
        if ($identity -cne '88053c5a7a7c46aae39e0c68d03d3dd6ab83047640e3a22455148e53023a44fe') { return [pscustomobject]@{ Succeeded=$false; Reason='forbidden_active_source'; Detail=$SourceName+':reviewed_owned_allocator_identity'; ActiveText='' } }
        return [pscustomobject]@{ Succeeded=$true; Reason='ok'; Detail=''; ActiveText=$SourceText }
    }

    # First-exit source and shared CRC closure reviewed in steps1029..1031.
    if ([string]::Equals($SourceName, '..\OS.Kernel\Boot\FirstOptRealExitBootServicesTransition.cs', [StringComparison]::Ordinal)) {
        $identity = Get-M01Utf8Sha256 -Text ($SourceText.Replace("`r`n", "`n"))
        if ($identity -cne '543e6d7f22eaf23cc491b9b330374eb39dffde038e82c666f1ba916249326808') { return [pscustomobject]@{ Succeeded=$false; Reason='forbidden_active_source'; Detail=$SourceName+':reviewed_first_exit_source_identity'; ActiveText='' } }
        return [pscustomobject]@{ Succeeded=$true; Reason='ok'; Detail=''; ActiveText=$SourceText }
    }
    if ([string]::Equals($SourceName, '..\..\std\no-runtime\shared\Hash\Crc32.cs', [StringComparison]::Ordinal)) {
        $identity = Get-M01Utf8Sha256 -Text ($SourceText.Replace("`r`n", "`n"))
        if ($identity -cne '228540b7c150ea26100ea9f509772ee89bbe10a45ef2c4d8e95a4b250c56333e') { return [pscustomobject]@{ Succeeded=$false; Reason='forbidden_active_source'; Detail=$SourceName+':reviewed_first_exit_source_identity'; ActiveText='' } }
        return [pscustomobject]@{ Succeeded=$true; Reason='ok'; Detail=''; ActiveText=$SourceText }
    }

    # Seeded churn adds no general runtime support; admit only the reviewed leaves.
    if ([string]::Equals($SourceName, '..\..\std\no-runtime\shared\DeterministicRandom32.cs', [StringComparison]::Ordinal)) {
        $identity = Get-M01Utf8Sha256 -Text ($SourceText.Replace("`r`n", "`n"))
        if ($identity -cne '6ca5e3e3e839017ca27fb73884b7665315b3e2df3c1f49a5fe012a3e56c2ae33') { return [pscustomobject]@{ Succeeded=$false; Reason='forbidden_active_source'; Detail=$SourceName+':reviewed_seeded_churn_source_identity'; ActiveText='' } }
        return [pscustomobject]@{ Succeeded=$true; Reason='ok'; Detail=''; ActiveText=$SourceText }
    }
    if ([string]::Equals($SourceName, '..\OS.Kernel\Runtime\SeededAllocationChurnCollectionRootRetentionGuestProbes.cs', [StringComparison]::Ordinal)) {
        $identity = Get-M01Utf8Sha256 -Text ($SourceText.Replace("`r`n", "`n"))
        if ($identity -cne '43cbaad55c19a90b4f763792cf974ab9c9dfe3b5ce76dd1ebffa716de12241a9') { return [pscustomobject]@{ Succeeded=$false; Reason='forbidden_active_source'; Detail=$SourceName+':reviewed_seeded_churn_source_identity'; ActiveText='' } }
        return [pscustomobject]@{ Succeeded=$true; Reason='ok'; Detail=''; ActiveText=$SourceText }
    }
    if ([string]::Equals($SourceName, 'Runtime\SeededCollectorChurnProbe.cs', [StringComparison]::Ordinal)) {
        $identity = Get-M01Utf8Sha256 -Text ($SourceText.Replace("`r`n", "`n"))
        if ($identity -cne '69aeb22b1c23f17f7748b07b68629698216da53a3d9b39fa6f6aac6bc17e6768') { return [pscustomobject]@{ Succeeded=$false; Reason='forbidden_active_source'; Detail=$SourceName+':reviewed_seeded_churn_source_identity'; ActiveText='' } }
        return [pscustomobject]@{ Succeeded=$true; Reason='ok'; Detail=''; ActiveText=$SourceText }
    }

    # Frozen #510 image leaf imports only the already included POD boot/memory contracts.
    # Admission is exact path plus complete text identity; raw authority hashes stay raw.
    if ([string]::Equals($SourceName,
            '..\OS.Kernel\Diagnostics\SeparateDisabledDefaultPageFaultPageTables.cs',
            [System.StringComparison]::Ordinal)) {
        $identity = Get-M01Utf8Sha256 -Text ($SourceText.Replace("`r`n", "`n"))
        if (-not [string]::Equals($identity,
                '4ec0cfb1cd2a07f0834463c5fd2ea1d6882e3fc76e85b3d9e49607b47abbb3e2',
                [System.StringComparison]::Ordinal)) {
            return [pscustomobject] @{
                Succeeded = $false
                Reason = "forbidden_active_source"
                Detail = $SourceName + ":reviewed_page_fault_tables_source_identity"
                ActiveText = ""
            }
        }
        return [pscustomobject] @{
            Succeeded = $true
            Reason = "ok"
            Detail = ""
            ActiveText = $SourceText
        }
    }

    # Frozen TypeManager leaf imports only the already included POD boot contracts.
    # Admit this exact path and complete text; raw publish authority stays raw.
    if ([string]::Equals($SourceName,
            '..\OS.Kernel\Runtime\TypeManagerModuleMetadataLiveNativeAOTImage.cs',
            [System.StringComparison]::Ordinal)) {
        $identity = Get-M01Utf8Sha256 -Text ($SourceText.Replace("`r`n", "`n"))
        if (-not [string]::Equals($identity,
                '3cfa1d3a627a6c9f7230fa368b3c7b6f025cd744784e75e506cf9b8cb5f51421',
                [System.StringComparison]::Ordinal)) {
            return [pscustomobject] @{ Succeeded=$false; Reason="forbidden_active_source"; Detail=$SourceName + ":reviewed_type_manager_source_identity"; ActiveText="" }
        }
        return [pscustomobject] @{ Succeeded=$true; Reason="ok"; Detail=""; ActiveText=$SourceText }
    }

    # Reviewed GC-static leaf imports only the already included POD boot contracts.
    # Admit this exact path and complete text; raw publish authority stays raw.
    if ([string]::Equals($SourceName,
            '..\OS.Kernel\Runtime\GCStaticRootsMalformedMetadata.cs',
            [System.StringComparison]::Ordinal)) {
        $identity = Get-M01Utf8Sha256 -Text ($SourceText.Replace("`r`n", "`n"))
        if (-not [string]::Equals($identity,
                '16c927badb80ebcdea56485463d0bd043ff985253a60621cee12ea7e7c80b519',
                [System.StringComparison]::Ordinal)) {
            return [pscustomobject] @{ Succeeded=$false; Reason="forbidden_active_source"; Detail=$SourceName + ":reviewed_gc_static_source_identity"; ActiveText="" }
        }
        return [pscustomobject] @{ Succeeded=$true; Reason="ok"; Detail=""; ActiveText=$SourceText }
    }

    # Reviewed bounded collector leaf imports only the already included POD boot contracts.
    # Admit this exact path and complete text; raw publish authority stays raw.
    if ([string]::Equals($SourceName,
            '..\OS.Kernel\Runtime\BoundedNonMovingCollectorExplicitRootEnumeration.cs',
            [System.StringComparison]::Ordinal)) {
        $identity = Get-M01Utf8Sha256 -Text ($SourceText.Replace("`r`n", "`n"))
        if (-not [string]::Equals($identity,
                '48b0007e97704b6337793682311b6257498e21d8adacaaf44b287fac5b975461',
                [System.StringComparison]::Ordinal)) {
            return [pscustomobject] @{ Succeeded=$false; Reason="forbidden_active_source"; Detail=$SourceName + ":reviewed_bounded_collector_source_identity"; ActiveText="" }
        }
        return [pscustomobject] @{ Succeeded=$true; Reason="ok"; Detail=""; ActiveText=$SourceText }
    }

    # Frozen typed EBS adapter imports only the included FirmwareServicesMode enum.
    # Admit this exact path and complete text; raw publish authority stays raw.
    if ([string]::Equals($SourceName,
            '..\OS.Kernel\Boot\TypedNoStdlibExitBootServicesCallAdapter.cs',
            [System.StringComparison]::Ordinal)) {
        $identity = Get-M01Utf8Sha256 -Text ($SourceText.Replace("`r`n", "`n"))
        if (-not [string]::Equals($identity,
                '7227e860b5e4f97d47c9c269b889c975e30f21c506f9ecb51424f5417e2945db',
                [System.StringComparison]::Ordinal)) {
            return [pscustomobject] @{ Succeeded=$false; Reason="forbidden_active_source"; Detail=$SourceName + ":reviewed_typed_ebs_adapter_source_identity"; ActiveText="" }
        }
        return [pscustomobject] @{ Succeeded=$true; Reason="ok"; Detail=""; ActiveText=$SourceText }
    }

    # Reviewed final-map/retry core imports only the included FirmwareServicesMode enum.
    # Admit this exact path and complete text; raw publish authority stays raw.
    if ([string]::Equals($SourceName,
            '..\OS.Kernel\Boot\NoAllocationOrUnrelatedFirmwareCallOccursBetween.cs',
            [System.StringComparison]::Ordinal)) {
        $identity = Get-M01Utf8Sha256 -Text ($SourceText.Replace("`r`n", "`n"))
        if (-not [string]::Equals($identity,
                '039407d33d94265898dcd6ac9239a3d011e93594bb3631e14f76eb7529af8315',
                [System.StringComparison]::Ordinal)) {
            return [pscustomobject] @{ Succeeded=$false; Reason="forbidden_active_source"; Detail=$SourceName + ":reviewed_final_map_window_source_identity"; ActiveText="" }
        }
        return [pscustomobject] @{ Succeeded=$true; Reason="ok"; Detail=""; ActiveText=$SourceText }
    }

    # Reviewed final-map/retry core imports only the included FirmwareServicesMode enum.
    # Admit this exact path and complete text; raw publish authority stays raw.
    if ([string]::Equals($SourceName,
            '..\OS.Kernel\Boot\BoundedRetryStaleKeyStatus.cs',
            [System.StringComparison]::Ordinal)) {
        $identity = Get-M01Utf8Sha256 -Text ($SourceText.Replace("`r`n", "`n"))
        if (-not [string]::Equals($identity,
                'efe748c857a600081bb4db7f54c4823113f8d334c5cf81a1d8ff8760191e6633',
                [System.StringComparison]::Ordinal)) {
            return [pscustomobject] @{ Succeeded=$false; Reason="forbidden_active_source"; Detail=$SourceName + ":reviewed_bounded_ebs_retry_source_identity"; ActiveText="" }
        }
        return [pscustomobject] @{ Succeeded=$true; Reason="ok"; Detail=""; ActiveText=$SourceText }
    }

    $active = Get-NostdlibEfiActiveSource `
        -SourceText $SourceText `
        -SourceName $SourceName
    if (-not $active.Succeeded) {
        return $active
    }

    $withoutBlockComments = [System.Text.RegularExpressions.Regex]::Replace(
        $active.ActiveText,
        '/\*.*?\*/',
        "",
        [System.Text.RegularExpressions.RegexOptions]::Singleline)
    $code = [System.Text.RegularExpressions.Regex]::Replace(
        $withoutBlockComments,
        '//.*$',
        "",
        [System.Text.RegularExpressions.RegexOptions]::Multiline)

    $forbiddenPatterns = [ordered] @{
        "unicode_identifier_escape" = '\\(?:u[0-9A-Fa-f]{4}|U[0-9A-Fa-f]{8})'
        "verbatim_identifier" = '@[A-Za-z_]'
        "span_api" = '\b(?:ReadOnly)?Span\s*<'
        "flags_attribute" = 'System\.FlagsAttribute|\[\s*(?:System\.)?Flags\s*\]'
        "system_api" = '\bSystem\s*\.'
        "system_namespace" = '\bnamespace\s+(?:global\s*::\s*)?System\b'
        "using_directive" = '\busing\b'
        "managed_string" = '\b(?:string|String)\b|"|\bnameof\s*\('
        "managed_object" = '\b(?:object|Object)\b'
        "managed_array" = '\[\s*(?:,\s*)*\]|\bnew\s+[A-Za-z_][A-Za-z0-9_.<>]*\s*\['
        "managed_root_reference" = '\b(?:Array|Type)\b'
        "managed_record_type" = '\brecord\s+(?:(?:class|struct)\s+)?[A-Za-z_][A-Za-z0-9_]*\s*(?:[<(:;{])'
        "generic_type_declaration" = '\b(?:class|struct|interface|record)\s+[A-Za-z_][A-Za-z0-9_]*\s*<'
        "generic_method_shape" = '\b[A-Za-z_][A-Za-z0-9_]*\s*<\s*(?:[A-Za-z_][A-Za-z0-9_]*\s*,\s*)*[A-Za-z_][A-Za-z0-9_]*\s*>\s*\('
        "generic_constraint" = '\bwhere\s+[A-Za-z_][A-Za-z0-9_]*\s*:'
        "managed_anonymous_object" = '\bnew\s*\{'
        "managed_type_shape" = '\b(?:interface|event|dynamic)\b'
        "source_attribute" = '(?m)(?:^|[;{}(,])\s*\[\s*[A-Za-z_]'
        "exception_control_flow" = '\b(?:Exception|throw|try|catch|finally)\b'
        "reflection_api" = '\b(?:typeof|GetType|Activator)\b|System\.Reflection'
        "managed_delegate" = '\bdelegate\b(?!\s*\*)|\b(?:Func|Action|Delegate|MulticastDelegate)\b'
        "managed_runtime_api" = '\b(?:Console|Task|Thread|GC|Marshal)\b'
    }

    foreach ($entry in $forbiddenPatterns.GetEnumerator()) {
        if ([System.Text.RegularExpressions.Regex]::IsMatch(
                $code,
                $entry.Value,
                [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)) {
            return [pscustomobject] @{
                Succeeded = $false
                Reason = "forbidden_active_source"
                Detail = $SourceName + ":" + $entry.Key
                ActiveText = ""
            }
        }
    }

    $bclConstantMatches = [System.Text.RegularExpressions.Regex]::Matches(
        $code,
        '\b[A-Za-z_][A-Za-z0-9_.<>]*\s*\.\s*(?:MaxValue|MinValue|Empty)\b',
        [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)
    foreach ($bclConstantMatch in $bclConstantMatches) {
        $constant = [System.Text.RegularExpressions.Regex]::Replace(
            $bclConstantMatch.Value,
            '\s+',
            "")
        if ($constant -cne "ulong.MaxValue") {
            return [pscustomobject] @{
                Succeeded = $false
                Reason = "forbidden_active_source"
                Detail = $SourceName + ":unavailable_bcl_constant"
                ActiveText = ""
            }
        }
    }

    $classMatches = [System.Text.RegularExpressions.Regex]::Matches(
        $code,
        '(?<![@A-Za-z0-9_])class\b',
        [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)
    foreach ($classMatch in $classMatches) {
        $prefixStart = -1
        if ($classMatch.Index -gt 0) {
            foreach ($delimiter in @("`r", "`n", ";", "{", "}")) {
                $candidate = $code.LastIndexOf(
                    $delimiter,
                    $classMatch.Index - 1,
                    [System.StringComparison]::Ordinal)
                if ($candidate -gt $prefixStart) {
                    $prefixStart = $candidate
                }
            }
        }

        $prefix = $code.Substring(
            $prefixStart + 1,
            $classMatch.Index - $prefixStart - 1).Trim()
        $modifierTokens = @(
            $prefix.Split(
                [char[]] " `t`r`n",
                [System.StringSplitOptions]::RemoveEmptyEntries)
        )
        $allowedClassModifiers = @(
            "public",
            "internal",
            "private",
            "protected",
            "file",
            "new",
            "unsafe",
            "sealed",
            "abstract",
            "partial",
            "static"
        )
        $hasStaticModifier = $false
        $invalidModifier = $false
        foreach ($modifier in $modifierTokens) {
            if ($modifier -eq "static") {
                $hasStaticModifier = $true
            }
            elseif ($allowedClassModifiers -notcontains $modifier) {
                $invalidModifier = $true
            }
        }
        if (-not $hasStaticModifier -or $invalidModifier) {
            return [pscustomobject] @{
                Succeeded = $false
                Reason = "forbidden_active_source"
                Detail = $SourceName + ":non_static_class"
                ActiveText = ""
            }
        }
    }

    return [pscustomobject] @{
        Succeeded = $true
        Reason = "ok"
        Detail = ""
        ActiveText = $code
    }
}

function Get-M01CanonicalXmlSha256 {
    param([System.Xml.XmlDocument] $Document)

    $bytes = [System.Text.Encoding]::UTF8.GetBytes($Document.OuterXml)
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        return ([System.BitConverter]::ToString(
                $sha256.ComputeHash($bytes))).Replace("-", "").ToLowerInvariant()
    }
    finally {
        $sha256.Dispose()
    }
}

function Test-M01SerializerTargetsImport {
    param(
        [string] $RepositoryRoot,
        [string] $ProjectPath,
        [System.Xml.XmlDocument] $Project
    )

    $projectReferences = @($Project.SelectNodes("//ProjectReference"))
    $importGroups = @($Project.SelectNodes("//ImportGroup"))
    $chooseNodes = @($Project.SelectNodes("//Choose"))
    if ($projectReferences.Count -ne 0 -or
        $importGroups.Count -ne 0 -or
        $chooseNodes.Count -ne 0) {
        return [pscustomobject] @{
            Succeeded = $false
            Reason = "forbidden_project_input"
            Detail = "reference-importgroup-or-choose"
        }
    }

    $allImports = @($Project.SelectNodes("//Import"))
    $rootImports = @($Project.SelectNodes("/Project/Import"))
    if ($allImports.Count -ne 1 -or $rootImports.Count -ne 1) {
        return [pscustomobject] @{
            Succeeded = $false
            Reason = "serializer_import_count_mismatch"
            Detail = $allImports.Count.ToString()
        }
    }

    $import = $rootImports[0]
    $importValue = $import.GetAttribute("Project")
    if ($import.Attributes.Count -ne 1 -or
        -not $import.HasAttribute("Project") -or
        $import.HasAttribute("Condition") -or
        $import.SelectNodes("*").Count -ne 0 -or
        $importValue -ne $script:ExpectedSerializerTargetsImport -or
        $importValue -match '[*?\[\];]' -or
        $importValue -match '\$\(' -or
        $importValue -match '@\(' -or
        [System.IO.Path]::IsPathRooted($importValue)) {
        return [pscustomobject] @{
            Succeeded = $false
            Reason = "serializer_import_shape_invalid"
            Detail = $importValue
        }
    }

    $projectDirectory = Split-Path -Parent $ProjectPath
    $expectedTargetsPath = [System.IO.Path]::GetFullPath(
        (Join-Path $RepositoryRoot (
            "tools\OS.X64TextSerializerGenerator\" +
            "OS.X64TextSerializerGenerator.targets")))
    try {
        $resolvedTargetsPath = [System.IO.Path]::GetFullPath(
            (Join-Path $projectDirectory $importValue))
    }
    catch {
        return [pscustomobject] @{
            Succeeded = $false
            Reason = "serializer_import_path_invalid"
            Detail = $importValue
        }
    }

    if (-not [string]::Equals(
            $resolvedTargetsPath,
            $expectedTargetsPath,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        return [pscustomobject] @{
            Succeeded = $false
            Reason = "serializer_import_path_invalid"
            Detail = $resolvedTargetsPath
        }
    }

    if (-not (Test-Path -LiteralPath $resolvedTargetsPath -PathType Leaf)) {
        return [pscustomobject] @{
            Succeeded = $false
            Reason = "serializer_targets_missing"
            Detail = $resolvedTargetsPath
        }
    }

    try {
        $targets = Read-M01SecureXmlDocument -Path $resolvedTargetsPath
    }
    catch {
        return [pscustomobject] @{
            Succeeded = $false
            Reason = "serializer_targets_malformed"
            Detail = $resolvedTargetsPath
        }
    }

    if ($targets.Project.Attributes.Count -ne 0) {
        return [pscustomobject] @{
            Succeeded = $false
            Reason = "serializer_targets_shape_invalid"
            Detail = "project-attributes"
        }
    }

    $injectedInputs = @($targets.SelectNodes(
        "//Compile|//ProjectReference|//Reference|//PackageReference|" +
        "//Analyzer|//AdditionalFiles|//UsingTask|//Import|//ImportGroup|" +
        "//Choose|//Csc|//WriteLinesToFile|//Copy|//Delete"))
    if ($injectedInputs.Count -ne 0) {
        return [pscustomobject] @{
            Succeeded = $false
            Reason = "serializer_targets_input_injection"
            Detail = $injectedInputs[0].Name
        }
    }

    $cleanupNodes = @($targets.SelectNodes("//RemoveDir"))
    $expectedCleanupDirectory =
        '$(_ClrosX64SerializerTrackedToolRoot)'
    $expectedCleanupCondition =
        'Exists(''$(_ClrosX64SerializerTrackedToolRoot)'')'
    $expectedCleanupTargetCondition =
        "'`$(NativeIntermediateOutputPath)' != ''"
    $cleanupTarget = if ($cleanupNodes.Count -eq 1) {
        $cleanupNodes[0].ParentNode
    }
    else {
        $null
    }
    if ($cleanupNodes.Count -ne 1 -or
        $null -eq $cleanupTarget -or
        $cleanupTarget.LocalName -ne "Target" -or
        $cleanupTarget.Attributes.Count -ne 4 -or
        $cleanupTarget.GetAttribute("Name") -ne
            "RemoveClrosX64InstructionPublicationSerializerToolOutput" -or
        $cleanupTarget.GetAttribute("AfterTargets") -ne "CoreClean" -or
        $cleanupTarget.GetAttribute("DependsOnTargets") -ne
            "TrackClrosX64InstructionPublicationSerializerOutput" -or
        $cleanupTarget.GetAttribute("Condition") -ne
            $expectedCleanupTargetCondition -or
        $cleanupNodes[0].Attributes.Count -ne 2 -or
        $cleanupNodes[0].GetAttribute("Directories") -ne
            $expectedCleanupDirectory -or
        $cleanupNodes[0].GetAttribute("Condition") -ne
            $expectedCleanupCondition -or
        $cleanupNodes[0].SelectNodes("*").Count -ne 0) {
        return [pscustomobject] @{
            Succeeded = $false
            Reason = "serializer_targets_cleanup_invalid"
            Detail = $cleanupNodes.Count.ToString()
        }
    }

    $expectedHashOutputs = @(
        "_ClrosX64SerializerGeneratorProjectSha256",
        "_ClrosX64SerializerToolsPropsSha256",
        "_ClrosX64SerializerRootPropsSha256",
        "_ClrosX64SerializerRootTargetsSha256",
        "_ClrosX64SerializerActualSha256",
        "_ClrosCheckedReferenceActualSha256",
        "_ClrosInterfaceDispatchActualSha256",
        "_ClrosSimpleCatchActualSha256"
    )
    $outputNodes = @($targets.SelectNodes("//Output"))
    if ($outputNodes.Count -ne $expectedHashOutputs.Count) {
        return [pscustomobject] @{
            Succeeded = $false
            Reason = "serializer_targets_output_injection"
            Detail = $outputNodes.Count.ToString()
        }
    }
    foreach ($expectedHashOutput in $expectedHashOutputs) {
        $matchingOutputs = @($outputNodes | Where-Object {
            $_.ParentNode.Name -eq "GetFileHash" -and
            $_.Attributes.Count -eq 2 -and
            $_.GetAttribute("TaskParameter") -eq "Hash" -and
            $_.GetAttribute("PropertyName") -eq $expectedHashOutput -and
            $_.SelectNodes("*").Count -eq 0
        })
        if ($matchingOutputs.Count -ne 1) {
            return [pscustomobject] @{
                Succeeded = $false
                Reason = "serializer_targets_output_injection"
                Detail = $expectedHashOutput
            }
        }
    }

    $serializerDirectory = Split-Path -Parent $resolvedTargetsPath
    $generatorProjectPath = Join-Path $serializerDirectory (
        "OS.X64TextSerializerGenerator.csproj")
    $toolsPropsPath = Join-Path (
        Split-Path -Parent $serializerDirectory) (
        "Directory.Build.props")
    try {
        $generatorProject =
            Read-M01SecureXmlDocument -Path $generatorProjectPath
        $toolsProps = Read-M01SecureXmlDocument -Path $toolsPropsPath
    }
    catch {
        return [pscustomobject] @{
            Succeeded = $false
            Reason = "serializer_build_input_malformed"
            Detail = $_.Exception.Message
        }
    }
    if ((Get-M01CanonicalXmlSha256 -Document $generatorProject) -ne
            "0c76867058268dffc8790f47468a742a4cc6baa8fc7d062062c3df9b7654591d" -or
        (Get-M01CanonicalXmlSha256 -Document $toolsProps) -ne
            "b268302d013a229bfc4c3be37fd22a59daaa699a2bc53e439804cbd3a9bfa778") {
        return [pscustomobject] @{
            Succeeded = $false
            Reason = "serializer_build_input_shape_invalid"
            Detail = $generatorProjectPath
        }
    }

    $expectedTargetsHash =
        "8a4dbf71b127b68c2b4ce7ab36ea6b73f42d0265c8dc9918d833475ac2013324"
    $targetsHash = Get-M01CanonicalXmlSha256 -Document $targets
    if ($targetsHash -ne $expectedTargetsHash) {
        return [pscustomobject] @{
            Succeeded = $false
            Reason = "serializer_targets_shape_invalid"
            Detail = $targetsHash
        }
    }

    return [pscustomobject] @{
        Succeeded = $true
        Reason = "ok"
        Detail = $resolvedTargetsPath
    }
}

function Test-M01BuildCustomizationSurface {
    param(
        [string] $RepositoryRoot,
        [string] $ProjectPath
    )

    $projectDirectory = Split-Path -Parent $ProjectPath
    $repositoryFullPath = [System.IO.Path]::GetFullPath($RepositoryRoot)
    $cursor = $projectDirectory
    while (-not [string]::Equals(
            [System.IO.Path]::GetFullPath($cursor),
            $repositoryFullPath,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        foreach ($fileName in @("Directory.Build.props", "Directory.Build.targets")) {
            $unexpectedPath = Join-Path $cursor $fileName
            if (Test-Path -LiteralPath $unexpectedPath -PathType Leaf) {
                return [pscustomobject] @{
                    Succeeded = $false
                    Reason = "unexpected_build_customization"
                    Detail = $unexpectedPath
                }
            }
        }

        $parent = Split-Path -Parent $cursor
        if ([string]::IsNullOrWhiteSpace($parent) -or
            [string]::Equals(
                $parent,
                $cursor,
                [System.StringComparison]::OrdinalIgnoreCase)) {
            return [pscustomobject] @{
                Succeeded = $false
                Reason = "repository_root_unreachable"
                Detail = $cursor
            }
        }
        $cursor = $parent
    }

    foreach ($fileName in @("Directory.Build.props", "Directory.Build.targets")) {
        $customizationPath = Join-Path $repositoryFullPath $fileName
        if (-not (Test-Path -LiteralPath $customizationPath -PathType Leaf)) {
            return [pscustomobject] @{
                Succeeded = $false
                Reason = "build_customization_missing"
                Detail = $customizationPath
            }
        }

        try {
            $customization =
                Read-M01SecureXmlDocument -Path $customizationPath
        }
        catch {
            return [pscustomobject] @{
                Succeeded = $false
                Reason = "build_customization_malformed"
                Detail = $customizationPath
            }
        }

        $expectedCanonicalHash = if ($fileName -eq "Directory.Build.props") {
            "0743a5f401f40744dc0a8c8a4d2d8467d9716a7040df32865974d1f6693115fa"
        }
        else {
            "de2b50f646cebd9b29f23f2a74b2ad5ff9d56f80680d6cf5ba826e40dfc959cf"
        }
        $canonicalHash = Get-M01CanonicalXmlSha256 -Document $customization
        if ($canonicalHash -ne $expectedCanonicalHash) {
            return [pscustomobject] @{
                Succeeded = $false
                Reason = "build_customization_shape_invalid"
                Detail = $customizationPath
            }
        }

        $forbiddenNodes = @($customization.SelectNodes(
            "//Compile|//ProjectReference|//Reference|//PackageReference|" +
            "//Analyzer|//AdditionalFiles|//UsingTask|//Import|//Choose|" +
            "//Csc"))
        $compileOutputs = @($customization.SelectNodes("//Output"))
        $nonValidationTargetChildren = @($customization.SelectNodes("//Target/*") |
            Where-Object { $_.Name -ne "Error" })
        if ($forbiddenNodes.Count -ne 0 -or
            $compileOutputs.Count -ne 0 -or
            $nonValidationTargetChildren.Count -ne 0) {
            return [pscustomobject] @{
                Succeeded = $false
                Reason = "build_customization_source_injection"
                Detail = $customizationPath
            }
        }
    }
    $rootResponsePath = Join-Path $repositoryFullPath "Directory.Build.rsp"
    if (Test-Path -LiteralPath $rootResponsePath -PathType Leaf) {
        return [pscustomobject] @{
            Succeeded = $false
            Reason = "unexpected_build_customization"
            Detail = $rootResponsePath
        }
    }

    $toolsDirectory = Join-Path $repositoryFullPath "tools"
    $generatorDirectory = Join-Path $toolsDirectory (
        "OS.X64TextSerializerGenerator")
    $unexpectedGeneratorCustomizations = @(
        (Join-Path $generatorDirectory "Directory.Build.props"),
        (Join-Path $generatorDirectory "Directory.Build.targets"),
        (Join-Path $generatorDirectory "Directory.Build.rsp"),
        (Join-Path $toolsDirectory "Directory.Build.targets"),
        (Join-Path $toolsDirectory "Directory.Build.rsp")
    )
    foreach ($unexpectedPath in $unexpectedGeneratorCustomizations) {
        if (Test-Path -LiteralPath $unexpectedPath -PathType Leaf) {
            return [pscustomobject] @{
                Succeeded = $false
                Reason = "unexpected_build_customization"
                Detail = $unexpectedPath
            }
        }
    }

    $canonicalToolsPropsPath = Join-Path $toolsDirectory "Directory.Build.props"
    if (-not (Test-Path -LiteralPath $canonicalToolsPropsPath -PathType Leaf)) {
        return [pscustomobject] @{
            Succeeded = $false
            Reason = "build_customization_missing"
            Detail = $canonicalToolsPropsPath
        }
    }

    return [pscustomobject] @{
        Succeeded = $true
        Reason = "ok"
        Detail = ""
    }
}

function Test-M01EvaluatedSourceClosure {
    param(
        [string] $RepositoryRoot,
        [string] $ProjectPath,
        [string] $Configuration,
        [bool] $LoadedImageStackEnabled,
        [bool] $LiveAllocatorEnabled,
        [bool] $OwnedPagingEnabled,
        [bool] $M02SafeStubEnabled,
        [bool] $M02CheckedPortIoBindingEnabled,
        [bool] $M02Com1OwnedPortIoEnabled,
        [bool] $M02OwnedCom1ReadyEnabled,
        [bool] $M02PanicSafeOwnedCom1Enabled,
        [bool] $M02OwnedX64GdtEncodingEnabled,
        [bool] $M02GuardedIstStackAllocationEnabled,
        [bool] $M02OwnedGdtTssTerminalEnabled,
        [bool] $M02NormalizedInterruptFramePageEnabled,
        [bool] $M02OwnedIdtGatePopulationEnabled,
        [bool] $M02IdtrContinuedExecutionEnabled,
        [bool] $M02Ud2VectorStateEnabled,
        [bool] $M02PageFaultCr2ErrorCodeEnabled,
        [bool] $M02DoubleFaultIstEnabled,
        [bool] $M02UnexpectedVectorPanicEnabled,
        [bool] $M04IndependentEndianConformanceEnabled,
        [bool] $M04SpanMemoryConformanceEnabled,
        [bool] $M04ArithmeticConformanceEnabled,
        [bool] $M04LayoutContractEnabled,
        [bool] $M04ManagedHeapPageRangeEnabled,
        [bool] $M04RhpNewFastEnabled,
        [bool] $M04RhpNewFastBeforeBindFailureEnabled,
        [bool] $M04RhpNewFastExhaustionFailureEnabled,
        [bool] $M04SzArrayAllocationEnabled,
        [bool] $M04SzArrayAllocationBeforeBindEnabled,
        [bool] $M04SzArrayAllocationExhaustionEnabled,
        [bool] $M04StringAllocationEnabled,
        [bool] $M04StringAllocationBeforeBindEnabled,
        [bool] $M04StringAllocationExhaustionEnabled,
        [bool] $M04StringAllocationInvalidLengthEnabled,
        [bool] $M03MemoryMapCaptureEnabled,
        [bool] $M03FirmwareEntropySeedEnabled,
        [bool] $M04TypeManagerModuleRegistrationEnabled,
        [bool] $M04X64UnwindMetadataEnabled,
        [bool] $M04SimpleThrowCatchEnabled,
        [bool] $M04GcStaticRootMaterializationEnabled,
        [bool] $M04SeededCollectorChurnEnabled,
        [bool] $M03TypedExitBootServicesCallEnabled,
        [bool] $M03FirstRealExitBootServicesEnabled,
        [bool] $M03FirmwareAdapterRevocationEnabled,
        [bool] $M03OwnedSubsystemSurvivalEnabled,
        [bool] $M03OwnedSubsystemPanicEnabled,
        [bool] $M04ClassConstructorExecutionEnabled,
        [bool] $M04InterfaceDispatchEnabled,
        [bool] $M04CheckedReferenceBarrierEnabled,
        [bool] $UefiGoneSimulationEnabled,
        [bool] $ExitBootServicesFreshMapEnabled,
        [bool] $AcpiPhysicalDiscoveryEnabled
    )

    $projectDirectory = Split-Path -Parent $ProjectPath
    $canonicalRootPropsPath = Join-Path $RepositoryRoot "Directory.Build.props"
    $canonicalRootTargetsPath = Join-Path $RepositoryRoot "Directory.Build.targets"
    $evaluationExtensionsPath = (
        Join-Path (
            [System.IO.Path]::GetTempPath()) (
            "clros-m01-source-evaluation-" +
            [System.Guid]::NewGuid().ToString("N"))
    ).Replace("\", "/").TrimEnd("/") + "/"
    $evaluationAssetsPath = Join-Path (
        $evaluationExtensionsPath) (
        "project.assets.json")
    $evaluationIsolation = Get-M01MsbuildIsolationContract `
        -ProjectExtensionsDirectory $evaluationExtensionsPath
    $expectedSwitchValues = [ordered] @{
        "EnableM01LoadedImageStackProbe" =
            $LoadedImageStackEnabled.ToString().ToLowerInvariant()
        "EnableM01LiveAllocatorProbe" =
            $LiveAllocatorEnabled.ToString().ToLowerInvariant()
        "EnableM01OwnedPagingProbe" =
            $OwnedPagingEnabled.ToString().ToLowerInvariant()
        "EnableM02SafeStubProbe" =
            $M02SafeStubEnabled.ToString().ToLowerInvariant()
        "EnableM02CheckedPortIoBindingProbe" =
            $M02CheckedPortIoBindingEnabled.ToString().ToLowerInvariant()
        "EnableM02Com1OwnedPortIoProbe" =
            $M02Com1OwnedPortIoEnabled.ToString().ToLowerInvariant()
        "EnableM02OwnedCom1ReadyProbe" =
            $M02OwnedCom1ReadyEnabled.ToString().ToLowerInvariant()
        "EnableM02PanicSafeOwnedCom1Probe" =
            $M02PanicSafeOwnedCom1Enabled.ToString().ToLowerInvariant()
        "EnableM02OwnedX64GdtEncodingProbe" =
            $M02OwnedX64GdtEncodingEnabled.ToString().ToLowerInvariant()
        "EnableM02GuardedIstStackAllocationProbe" =
            $M02GuardedIstStackAllocationEnabled.ToString().ToLowerInvariant()
        "EnableM02OwnedGdtTssTerminalProbe" =
            $M02OwnedGdtTssTerminalEnabled.ToString().ToLowerInvariant()
        "EnableM02NormalizedInterruptFramePageProbe" =
            $M02NormalizedInterruptFramePageEnabled.ToString().ToLowerInvariant()
        "EnableM02OwnedIdtGatePopulationProbe" =
            $M02OwnedIdtGatePopulationEnabled.ToString().ToLowerInvariant()
        "EnableM02IdtrContinuedExecutionProbe" =
            $M02IdtrContinuedExecutionEnabled.ToString().ToLowerInvariant()
        "EnableM02Ud2VectorStateProbe" =
            $M02Ud2VectorStateEnabled.ToString().ToLowerInvariant()
        "EnableM02PageFaultCr2ErrorCodeProbe" =
            $M02PageFaultCr2ErrorCodeEnabled.ToString().ToLowerInvariant()
        "EnableM02DoubleFaultIstProbe" =
            $M02DoubleFaultIstEnabled.ToString().ToLowerInvariant()
        "EnableM02UnexpectedVectorPanicProbe" =
            $M02UnexpectedVectorPanicEnabled.ToString().ToLowerInvariant()
        "EnableM04IndependentEndianConformanceProbe" =
            $M04IndependentEndianConformanceEnabled.ToString().ToLowerInvariant()
        "EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe" =
            $M04SpanMemoryConformanceEnabled.ToString().ToLowerInvariant()
        "EnableM04ArithmeticConformanceProbe" =
            $M04ArithmeticConformanceEnabled.ToString().ToLowerInvariant()
        "EnableM04LayoutContractProbe" =
            $M04LayoutContractEnabled.ToString().ToLowerInvariant()
        "EnableM04ManagedHeapPageRangeProbe" =
            $M04ManagedHeapPageRangeEnabled.ToString().ToLowerInvariant()
        "EnableM04RhpNewFastProbe" =
            $M04RhpNewFastEnabled.ToString().ToLowerInvariant()
        "EnableM04RhpNewFastBeforeBindFailureProbe" =
            $M04RhpNewFastBeforeBindFailureEnabled.ToString().ToLowerInvariant()
        "EnableM04RhpNewFastExhaustionFailureProbe" =
            $M04RhpNewFastExhaustionFailureEnabled.ToString().ToLowerInvariant()
        "EnableM04SzArrayAllocationProbe" =
            $M04SzArrayAllocationEnabled.ToString().ToLowerInvariant()
        "EnableM04SzArrayAllocationBeforeBindProbe" =
            $M04SzArrayAllocationBeforeBindEnabled.ToString().ToLowerInvariant()
        "EnableM04SzArrayAllocationExhaustionProbe" =
            $M04SzArrayAllocationExhaustionEnabled.ToString().ToLowerInvariant()
        "EnableM04StringAllocationProbe" =
            $M04StringAllocationEnabled.ToString().ToLowerInvariant()
        "EnableM04StringAllocationBeforeBindProbe" =
            $M04StringAllocationBeforeBindEnabled.ToString().ToLowerInvariant()
        "EnableM04StringAllocationExhaustionProbe" =
            $M04StringAllocationExhaustionEnabled.ToString().ToLowerInvariant()
        "EnableM04StringAllocationInvalidLengthProbe" =
            $M04StringAllocationInvalidLengthEnabled.ToString().ToLowerInvariant()
        "EnableM03MemoryMapCaptureProbe" =
            $M03MemoryMapCaptureEnabled.ToString().ToLowerInvariant()
        "EnableM03FirmwareEntropySeedProbe" =
            $M03FirmwareEntropySeedEnabled.ToString().ToLowerInvariant()
        "EnableM04TypeManagerModuleRegistrationProbe" =
            $M04TypeManagerModuleRegistrationEnabled.ToString().ToLowerInvariant()
        "EnableM04X64UnwindMetadataProbe" =
            $M04X64UnwindMetadataEnabled.ToString().ToLowerInvariant()
        "EnableM04SimpleThrowCatchProbe" =
            $M04SimpleThrowCatchEnabled.ToString().ToLowerInvariant()
        "EnableM04GcStaticRootMaterializationProbe" =
            $M04GcStaticRootMaterializationEnabled.ToString().ToLowerInvariant()
        "EnableM04SeededCollectorChurnProbe" =
            $M04SeededCollectorChurnEnabled.ToString().ToLowerInvariant()
        "EnableM03TypedExitBootServicesCallProbe" =
            $M03TypedExitBootServicesCallEnabled.ToString().ToLowerInvariant()
        "EnableM03FirstRealExitBootServicesProbe" =
            $M03FirstRealExitBootServicesEnabled.ToString().ToLowerInvariant()
        "EnableM03FirmwareAdapterRevocationProbe" =
            $M03FirmwareAdapterRevocationEnabled.ToString().ToLowerInvariant()
        "EnableM03OwnedSubsystemSurvivalProbe" =
            $M03OwnedSubsystemSurvivalEnabled.ToString().ToLowerInvariant()
        "EnableM03OwnedSubsystemPanicProbe" =
            $M03OwnedSubsystemPanicEnabled.ToString().ToLowerInvariant()
        "EnableM04ClassConstructorExecutionProbe" =
            $M04ClassConstructorExecutionEnabled.ToString().ToLowerInvariant()
        "EnableM04InterfaceDispatchProbe" =
            $M04InterfaceDispatchEnabled.ToString().ToLowerInvariant()
        "EnableM04CheckedReferenceBarrierProbe" =
            $M04CheckedReferenceBarrierEnabled.ToString().ToLowerInvariant()
    }
    $expectedSwitchSymbols = [ordered] @{
        "M01_LOADED_IMAGE_STACK_PROBE" = $LoadedImageStackEnabled
        "M01_LIVE_ALLOCATOR_PROBE" = $LiveAllocatorEnabled
        "M01_OWNED_PAGING_PROBE" = $OwnedPagingEnabled
        "M02_SAFE_STUB_PROBE" = $M02SafeStubEnabled
        "M02_CHECKED_PORT_IO_BINDING_PROBE" =
            $M02CheckedPortIoBindingEnabled
        "M02_COM1_OWNED_PORT_IO_PROBE" = $M02Com1OwnedPortIoEnabled
        "M02_OWNED_COM1_READY_PROBE" = $M02OwnedCom1ReadyEnabled
        "M02_PANIC_SAFE_OWNED_COM1_PROBE" = $M02PanicSafeOwnedCom1Enabled
        "M02_OWNED_X64_GDT_ENCODING_PROBE" = $M02OwnedX64GdtEncodingEnabled
        "M02_GUARDED_IST_STACK_ALLOCATION_PROBE" =
            $M02GuardedIstStackAllocationEnabled
        "M02_OWNED_GDT_TSS_TERMINAL_PROBE" =
            $M02OwnedGdtTssTerminalEnabled
        "M02_NORMALIZED_INTERRUPT_FRAME_PAGE_PROBE" =
            $M02NormalizedInterruptFramePageEnabled
        "M02_OWNED_IDT_GATE_POPULATION_PROBE" =
            $M02OwnedIdtGatePopulationEnabled
        "M02_IDTR_CONTINUED_EXECUTION_PROBE" =
            $M02IdtrContinuedExecutionEnabled
        "M02_UD2_VECTOR_STATE_PROBE" = $M02Ud2VectorStateEnabled
        "M02_PAGE_FAULT_CR2_ERROR_CODE_PROBE" = $M02PageFaultCr2ErrorCodeEnabled
        "M02_DOUBLE_FAULT_IST_PROBE" = $M02DoubleFaultIstEnabled
        "M02_UNEXPECTED_VECTOR_PANIC_PROBE" = $M02UnexpectedVectorPanicEnabled
        "M04_INDEPENDENT_ENDIAN_CONFORMANCE_PROBE" =
            $M04IndependentEndianConformanceEnabled
        "M04_SPAN_OVERLAP_MEMORY_CAST_UNSAFE_WIDTH_CONFORMANCE_PROBE" =
            $M04SpanMemoryConformanceEnabled
        "M04_ARITHMETIC_CONFORMANCE_PROBE" =
            $M04ArithmeticConformanceEnabled
        "M04_LAYOUT_CONTRACT_PROBE" =
            $M04LayoutContractEnabled
        "M04_MANAGED_HEAP_PAGE_RANGE_PROBE" =
            $M04ManagedHeapPageRangeEnabled
        "M04_RHP_NEW_FAST_PROBE" =
            $M04RhpNewFastEnabled
        "M04_RHP_NEW_FAST_BEFORE_BIND_FAILURE_PROBE" =
            $M04RhpNewFastBeforeBindFailureEnabled
        "M04_RHP_NEW_FAST_EXHAUSTION_FAILURE_PROBE" =
            $M04RhpNewFastExhaustionFailureEnabled
        "M04_SZ_ARRAY_ALLOCATION_PROBE" =
            $M04SzArrayAllocationEnabled
        "M04_SZ_ARRAY_ALLOCATION_BEFORE_BIND_PROBE" =
            $M04SzArrayAllocationBeforeBindEnabled
        "M04_SZ_ARRAY_ALLOCATION_EXHAUSTION_PROBE" =
            $M04SzArrayAllocationExhaustionEnabled
        "M04_STRING_ALLOCATION_PROBE" =
            $M04StringAllocationEnabled
        "M04_STRING_ALLOCATION_BEFORE_BIND_PROBE" =
            $M04StringAllocationBeforeBindEnabled
        "M04_STRING_ALLOCATION_EXHAUSTION_PROBE" =
            $M04StringAllocationExhaustionEnabled
        "M04_STRING_ALLOCATION_INVALID_LENGTH_PROBE" =
            $M04StringAllocationInvalidLengthEnabled
        "M03_MEMORY_MAP_CAPTURE_PROBE" =
            $M03MemoryMapCaptureEnabled
        "M03_FIRMWARE_ENTROPY_SEED_PROBE" =
            $M03FirmwareEntropySeedEnabled
        "M04_TYPE_MANAGER_MODULE_REGISTRATION_PROBE" =
            $M04TypeManagerModuleRegistrationEnabled
        "M04_X64_UNWIND_METADATA_PROBE" =
            $M04X64UnwindMetadataEnabled
        "M04_SIMPLE_THROW_CATCH_PROBE" =
            $M04SimpleThrowCatchEnabled
        "M04_GC_STATIC_ROOT_MATERIALIZATION_PROBE" =
            $M04GcStaticRootMaterializationEnabled
        "M04_SEEDED_COLLECTOR_CHURN_PROBE" =
            $M04SeededCollectorChurnEnabled
        "M03_TYPED_EXIT_BOOT_SERVICES_CALL_PROBE" =
            $M03TypedExitBootServicesCallEnabled
        "M03_FIRST_REAL_EXIT_BOOT_SERVICES_PROBE" =
            $M03FirstRealExitBootServicesEnabled
        "M03_FIRMWARE_ADAPTER_REVOCATION_PROBE" =
            $M03FirmwareAdapterRevocationEnabled
        "M03_OWNED_SUBSYSTEM_SURVIVAL_PROBE" =
            $M03OwnedSubsystemSurvivalEnabled
        "M03_OWNED_SUBSYSTEM_PANIC_PROBE" =
            $M03OwnedSubsystemPanicEnabled
        "M04_CLASS_CONSTRUCTOR_EXECUTION_PROBE" =
            $M04ClassConstructorExecutionEnabled
        "M04_INTERFACE_DISPATCH_PROBE" =
            $M04InterfaceDispatchEnabled
        "M04_CHECKED_REFERENCE_BARRIER_PROBE" =
            $M04CheckedReferenceBarrierEnabled
        "UEFI_GONE_SIMULATION_PROBE" = $UefiGoneSimulationEnabled
        "EXIT_BOOT_SERVICES_FRESH_MAP_PROBE" =
            $ExitBootServicesFreshMapEnabled
        "ACPI_PHYSICAL_DISCOVERY_PROBE" = $AcpiPhysicalDiscoveryEnabled
    }

    foreach ($profile in @("SingleMinimalApi", "MultiMinimalApi")) {
        $arguments = @(
            "msbuild",
            $ProjectPath,
            "-noAutoResponse",
            "-nologo",
            (
                "-getItem:" +
                "Compile,ProjectReference,Reference,Analyzer," +
                "_ClrosX64SerializerGeneratorInput"
            ),
            (
                "-getProperty:" +
                "Configuration," +
                "EnableM01LoadedImageStackProbe," +
                "EnableM01LiveAllocatorProbe," +
                "EnableM01OwnedPagingProbe," +
                "EnableM02SafeStubProbe," +
                "EnableM02CheckedPortIoBindingProbe," +
                "EnableM02Com1OwnedPortIoProbe," +
                "EnableM02OwnedCom1ReadyProbe," +
                "EnableM02PanicSafeOwnedCom1Probe," +
                "EnableM02OwnedX64GdtEncodingProbe," +
                "EnableM02GuardedIstStackAllocationProbe," +
                "EnableM02OwnedGdtTssTerminalProbe," +
                "EnableM02NormalizedInterruptFramePageProbe," +
                "EnableM02OwnedIdtGatePopulationProbe," +
                "EnableM02IdtrContinuedExecutionProbe," +
                "EnableM02Ud2VectorStateProbe," +
                "EnableM02PageFaultCr2ErrorCodeProbe," +
                "EnableM02DoubleFaultIstProbe," +
                "EnableM02UnexpectedVectorPanicProbe," +
                "EnableM04IndependentEndianConformanceProbe," +
                "EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe," +
                "EnableM04ArithmeticConformanceProbe," +
                "EnableM04LayoutContractProbe," +
                "EnableM04ManagedHeapPageRangeProbe," +
                "EnableM04RhpNewFastProbe," +
                "EnableM04RhpNewFastBeforeBindFailureProbe," +
                "EnableM04RhpNewFastExhaustionFailureProbe," +
                "EnableM04SzArrayAllocationProbe," +
                "EnableM04SzArrayAllocationBeforeBindProbe," +
                "EnableM04SzArrayAllocationExhaustionProbe," +
                "EnableM04StringAllocationProbe," +
                "EnableM04StringAllocationBeforeBindProbe," +
                "EnableM04StringAllocationExhaustionProbe," +
                "EnableM04StringAllocationInvalidLengthProbe," +
                "EnableM04CheckedReferenceBarrierProbe," +
            "EnableM03MemoryMapCaptureProbe," +
            "EnableM03FirmwareEntropySeedProbe," +
            "EnableM04TypeManagerModuleRegistrationProbe," +
            "EnableM04X64UnwindMetadataProbe," +
            "EnableM04SimpleThrowCatchProbe," +
            "EnableM04GcStaticRootMaterializationProbe," +
            "EnableM04SeededCollectorChurnProbe," +
            "EnableM03TypedExitBootServicesCallProbe," +
            "EnableM03FirstRealExitBootServicesProbe," +
            "EnableM03FirmwareAdapterRevocationProbe," +
            "EnableM03OwnedSubsystemSurvivalProbe," +
            "EnableM03OwnedSubsystemPanicProbe," +
            "EnableM04ClassConstructorExecutionProbe," +
            "EnableM04InterfaceDispatchProbe," +
                "DefineConstants," +
                "EnableDefaultCompileItems," +
                "NoStdLib," +
                "DisableImplicitFrameworkReferences," +
                "MSBuildSDKsPath,MSBuildExtensionsPath," +
                "MSBuildUserExtensionsPath," +
                "ImportByWildcardBeforeMicrosoftCommonProps," +
                "ImportByWildcardAfterMicrosoftCommonProps," +
                "ImportUserLocationsByWildcardBeforeMicrosoftCommonProps," +
                "ImportUserLocationsByWildcardAfterMicrosoftCommonProps," +
                "ImportByWildcardBeforeMicrosoftCommonTargets," +
                "ImportByWildcardAfterMicrosoftCommonTargets," +
                "ImportUserLocationsByWildcardBeforeMicrosoftCommonTargets," +
                "ImportUserLocationsByWildcardAfterMicrosoftCommonTargets," +
                "ImportByWildcardBeforeMicrosoftCSharpTargets," +
                "ImportByWildcardAfterMicrosoftCSharpTargets," +
                "ImportUserLocationsByWildcardBeforeMicrosoftCSharpTargets," +
                "ImportUserLocationsByWildcardAfterMicrosoftCSharpTargets," +
                "ImportByWildcardBeforeMicrosoftCommonCrossTargetingTargets," +
                "ImportByWildcardAfterMicrosoftCommonCrossTargetingTargets," +
                "UseSharedCompilation"
            ),
            ("-p:Configuration=" + $Configuration),
            ("-p:OSWorkloadProfile=" + $profile),
            ("-p:DirectoryBuildPropsPath=" + $canonicalRootPropsPath),
            ("-p:DirectoryBuildTargetsPath=" + $canonicalRootTargetsPath),
            ("-p:MSBuildProjectExtensionsPath=" + $evaluationExtensionsPath),
            ("-p:BaseIntermediateOutputPath=" + $evaluationExtensionsPath),
            ("-p:IntermediateOutputPath=" + $evaluationExtensionsPath),
            ("-p:ProjectAssetsFile=" + $evaluationAssetsPath),
            "-p:ImportDirectoryBuildProps=true",
            "-p:ImportDirectoryBuildTargets=true",
            "-p:ImportProjectExtensionProps=false",
            "-p:ImportProjectExtensionTargets=false",
            "-p:ExcludeRestorePackageImports=true",
            "-p:CustomBeforeMicrosoftCommonProps=",
            "-p:CustomAfterMicrosoftCommonProps=",
            "-p:CustomBeforeDirectoryBuildProps=",
            "-p:CustomAfterDirectoryBuildProps=",
            "-p:CustomBeforeDirectoryBuildTargets=",
            "-p:CustomAfterDirectoryBuildTargets=",
            "-p:CustomBeforeMicrosoftCommonTargets=",
            "-p:CustomAfterMicrosoftCommonTargets=",
            "-p:CustomBeforeMicrosoftCSharpTargets=",
            "-p:CustomAfterMicrosoftCSharpTargets=",
            "-p:CustomBeforeMicrosoftCommonCrossTargetingTargets=",
            "-p:CustomAfterMicrosoftCommonCrossTargetingTargets=",
            "-p:BeforeMicrosoftNETSdkTargets=",
            "-p:AfterMicrosoftNETSdkTargets=",
            "-p:RestoreIgnoreFailedSources=true"
        )
        $arguments += $evaluationIsolation.Arguments
        if ($LoadedImageStackEnabled) {
            $arguments += "-p:EnableM01LoadedImageStackProbe=true"
        }
        if ($LiveAllocatorEnabled) {
            $arguments += "-p:EnableM01LiveAllocatorProbe=true"
        }
        if ($OwnedPagingEnabled) {
            $arguments += "-p:EnableM01OwnedPagingProbe=true"
        }
        if ($M02SafeStubEnabled) {
            $arguments += "-p:EnableM02SafeStubProbe=true"
        }
        if ($M02CheckedPortIoBindingEnabled) {
            $arguments += "-p:EnableM02CheckedPortIoBindingProbe=true"
        }
        if ($M02Com1OwnedPortIoEnabled) {
            $arguments += "-p:EnableM02Com1OwnedPortIoProbe=true"
        }
        if ($M02OwnedCom1ReadyEnabled) {
            $arguments += "-p:EnableM02OwnedCom1ReadyProbe=true"
        }
        if ($M02PanicSafeOwnedCom1Enabled) {
            $arguments += "-p:EnableM02PanicSafeOwnedCom1Probe=true"
        }
        if ($M02OwnedX64GdtEncodingEnabled) {
            $arguments += "-p:EnableM02OwnedX64GdtEncodingProbe=true"
        }
        if ($M02GuardedIstStackAllocationEnabled) {
            $arguments +=
                "-p:EnableM02GuardedIstStackAllocationProbe=true"
        }
        if ($M02OwnedGdtTssTerminalEnabled) {
            $arguments +=
                "-p:EnableM02OwnedGdtTssTerminalProbe=true"
        }
        if ($M02NormalizedInterruptFramePageEnabled) {
            $arguments +=
                "-p:EnableM02NormalizedInterruptFramePageProbe=true"
        }
        if ($M02OwnedIdtGatePopulationEnabled) {
            $arguments +=
                "-p:EnableM02OwnedIdtGatePopulationProbe=true"
        }
        if ($M02IdtrContinuedExecutionEnabled) {
            $arguments +=
                "-p:EnableM02IdtrContinuedExecutionProbe=true"
        }
        if ($M02Ud2VectorStateEnabled) {
            $arguments += "-p:EnableM02Ud2VectorStateProbe=true"
        }
        if ($M02PageFaultCr2ErrorCodeEnabled) {
            $arguments += "-p:EnableM02PageFaultCr2ErrorCodeProbe=true"
        }

        if ($M02DoubleFaultIstEnabled) {
            $arguments += "-p:EnableM02DoubleFaultIstProbe=true"
        }
        if ($M02UnexpectedVectorPanicEnabled) {
            $arguments += "-p:EnableM02UnexpectedVectorPanicProbe=true"
        }
        if ($M04IndependentEndianConformanceEnabled) {
            $arguments +=
                "-p:EnableM04IndependentEndianConformanceProbe=true"
        }
        if ($M04SpanMemoryConformanceEnabled) {
            $arguments +=
                "-p:EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe=true"
        }
        if ($M04ArithmeticConformanceEnabled) {
            $arguments +=
                "-p:EnableM04ArithmeticConformanceProbe=true"
        }
        if ($M04LayoutContractEnabled) {
            $arguments +=
                "-p:EnableM04LayoutContractProbe=true"
        }
        if ($M04ManagedHeapPageRangeEnabled) {
            $arguments +=
                "-p:EnableM04ManagedHeapPageRangeProbe=true"
        }
        if ($M04RhpNewFastEnabled) {
            $arguments +=
                "-p:EnableM04RhpNewFastProbe=true"
        }
        if ($M04RhpNewFastBeforeBindFailureEnabled) {
            $arguments +=
                "-p:EnableM04RhpNewFastBeforeBindFailureProbe=true"
        }
        if ($M04RhpNewFastExhaustionFailureEnabled) {
            $arguments +=
                "-p:EnableM04RhpNewFastExhaustionFailureProbe=true"
        }
        if ($M04SzArrayAllocationEnabled) {
            $arguments +=
                "-p:EnableM04SzArrayAllocationProbe=true"
        }
        if ($M04SzArrayAllocationBeforeBindEnabled) {
            $arguments +=
                "-p:EnableM04SzArrayAllocationBeforeBindProbe=true"
        }
        if ($M04SzArrayAllocationExhaustionEnabled) {
            $arguments +=
                "-p:EnableM04SzArrayAllocationExhaustionProbe=true"
        }
        if ($M04StringAllocationEnabled) {
            $arguments +=
                "-p:EnableM04StringAllocationProbe=true"
        }
        if ($M04StringAllocationBeforeBindEnabled) {
            $arguments +=
                "-p:EnableM04StringAllocationBeforeBindProbe=true"
        }
        if ($M04StringAllocationExhaustionEnabled) {
            $arguments +=
                "-p:EnableM04StringAllocationExhaustionProbe=true"
        }
        if ($M04StringAllocationInvalidLengthEnabled) {
            $arguments +=
                "-p:EnableM04StringAllocationInvalidLengthProbe=true"
        }
        if ($M03MemoryMapCaptureEnabled) {
            $arguments +=
                "-p:EnableM03MemoryMapCaptureProbe=true"
        }
        if ($M03FirmwareEntropySeedEnabled) {
            $arguments +=
                "-p:EnableM03FirmwareEntropySeedProbe=true"
        }
        if ($M04TypeManagerModuleRegistrationEnabled) {
            $arguments +=
                "-p:EnableM04TypeManagerModuleRegistrationProbe=true"
        }
        if ($M04X64UnwindMetadataEnabled) {
            $arguments +=
                "-p:EnableM04X64UnwindMetadataProbe=true"
        }
        if ($M04SimpleThrowCatchEnabled) {
            $arguments +=
                "-p:EnableM04SimpleThrowCatchProbe=true"
        }
        if ($M04GcStaticRootMaterializationEnabled) {
            $arguments +=
                "-p:EnableM04GcStaticRootMaterializationProbe=true"
        }
        if ($M04SeededCollectorChurnEnabled) {
            $arguments +=
                "-p:EnableM04SeededCollectorChurnProbe=true"
        }
        if ($M03TypedExitBootServicesCallEnabled) {
            $arguments +=
                "-p:EnableM03TypedExitBootServicesCallProbe=true"
        }
        if ($M03FirstRealExitBootServicesEnabled) {
            $arguments +=
                "-p:EnableM03FirstRealExitBootServicesProbe=true"
        }
        if ($M03FirmwareAdapterRevocationEnabled) {
            $arguments +=
                "-p:EnableM03FirmwareAdapterRevocationProbe=true"
        }
        if ($M03OwnedSubsystemSurvivalEnabled) {
            $arguments +=
                "-p:EnableM03OwnedSubsystemSurvivalProbe=true"
        }
        if ($M03OwnedSubsystemPanicEnabled) {
            $arguments +=
                "-p:EnableM03OwnedSubsystemPanicProbe=true"
        }
        if ($M04ClassConstructorExecutionEnabled) {
            $arguments +=
                "-p:EnableM04ClassConstructorExecutionProbe=true"
        }
        if ($M04InterfaceDispatchEnabled) {
            $arguments +=
                "-p:EnableM04InterfaceDispatchProbe=true"
        }
        if ($M04CheckedReferenceBarrierEnabled) {
            $arguments +=
                "-p:EnableM04CheckedReferenceBarrierProbe=true"
        }
        if ($UefiGoneSimulationEnabled) {
            $arguments += "-p:EnableUefiGoneSimulationProbe=true"
        }
        if ($ExitBootServicesFreshMapEnabled) {
            $arguments += "-p:EnableExitBootServicesFreshMapProbe=true"
        }
        if ($AcpiPhysicalDiscoveryEnabled) {
            $arguments += "-p:EnableAcpiPhysicalDiscoveryProbe=true"
        }

        $processResult = Invoke-M01BoundedProcess `
            -FilePath $script:DotnetHostPath `
            -Arguments $arguments `
            -WorkingDirectory $RepositoryRoot `
            -Operation ("source-evaluation-" + $profile.ToLowerInvariant()) `
            -TimeoutSeconds $BuildProcessTimeoutSeconds `
            -MaximumOutputCharacters $BuildProcessMaximumOutputCharacters `
            -EnvironmentOverrides $evaluationIsolation.Environment
        $processFailure = Get-M01BoundedProcessFailureReason `
            -Result $processResult
        $outputText = $processResult.OutputText
        if ($processFailure -cne "ok") {
            return New-M01SourceClosureResult `
                -Succeeded $false `
                -Reason (
                    "evaluated_project_process_" + $processFailure) `
                -Detail ($profile + ":" + $processResult.OutputText)
        }

        $jsonStart = $outputText.IndexOf(
            "{",
            [System.StringComparison]::Ordinal)
        $jsonEnd = $outputText.LastIndexOf(
            "}",
            [System.StringComparison]::Ordinal)
        if ($jsonStart -lt 0 -or $jsonEnd -le $jsonStart) {
            return New-M01SourceClosureResult `
                -Succeeded $false `
                -Reason "evaluated_project_malformed" `
                -Detail ($profile + ":json")
        }

        try {
            $evaluation = $outputText.Substring(
                $jsonStart,
                $jsonEnd - $jsonStart + 1) | ConvertFrom-Json
        }
        catch {
            return New-M01SourceClosureResult `
                -Succeeded $false `
                -Reason "evaluated_project_malformed" `
                -Detail ($profile + ":parse")
        }

        if ($evaluation.Properties.Configuration -ne $Configuration -or
            $evaluation.Properties.EnableDefaultCompileItems -ne "false" -or
            $evaluation.Properties.NoStdLib -ne "true" -or
            $evaluation.Properties.DisableImplicitFrameworkReferences -ne "true") {
            return New-M01SourceClosureResult `
                -Succeeded $false `
                -Reason "evaluated_property_mismatch" `
                -Detail ($profile + ":nostdlib")
        }
        foreach ($entry in $evaluationIsolation.Properties.GetEnumerator()) {
            if ([string] $evaluation.Properties.($entry.Key) -cne
                [string] $entry.Value) {
                return New-M01SourceClosureResult `
                    -Succeeded $false `
                    -Reason "evaluated_property_mismatch" `
                    -Detail ($profile + ":" + $entry.Key)
            }
        }
        foreach ($entry in $expectedSwitchValues.GetEnumerator()) {
            if ($evaluation.Properties.($entry.Key) -ne $entry.Value) {
                return New-M01SourceClosureResult `
                    -Succeeded $false `
                    -Reason "evaluated_property_mismatch" `
                    -Detail ($profile + ":" + $entry.Key)
            }
        }

        $symbols = @(
            $evaluation.Properties.DefineConstants.Split(";") |
                Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
                ForEach-Object { $_.Trim() }
        )
        $expectedProfileSymbol = if ($profile -eq "SingleMinimalApi") {
            "OS_PROFILE_SINGLE_MINIMAL_API"
        }
        else {
            "OS_PROFILE_MULTI_MINIMAL_API"
        }
        $unexpectedProfileSymbol = if ($profile -eq "SingleMinimalApi") {
            "OS_PROFILE_MULTI_MINIMAL_API"
        }
        else {
            "OS_PROFILE_SINGLE_MINIMAL_API"
        }
        if ($symbols -notcontains "NOSTDLIB_EFI" -or
            $symbols -contains "NOSTDLIB" -or
            $symbols -notcontains $expectedProfileSymbol -or
            $symbols -contains $unexpectedProfileSymbol) {
            return New-M01SourceClosureResult `
                -Succeeded $false `
                -Reason "evaluated_define_constants_mismatch" `
                -Detail $profile
        }
        foreach ($entry in $expectedSwitchSymbols.GetEnumerator()) {
            $isPresent = $symbols -contains $entry.Key
            if ($isPresent -ne [bool] $entry.Value) {
                return New-M01SourceClosureResult `
                    -Succeeded $false `
                    -Reason "evaluated_define_constants_mismatch" `
                    -Detail ($profile + ":" + $entry.Key)
            }
        }
        $expectedSymbols = New-Object 'System.Collections.Generic.HashSet[string]' (
            [System.StringComparer]::Ordinal)
        foreach ($symbol in @(
                "TRACE",
                "NOSTDLIB_EFI",
                $expectedProfileSymbol)) {
            [void] $expectedSymbols.Add($symbol)
        }
        [void] $expectedSymbols.Add(
            $(if ($Configuration -eq "Debug") { "DEBUG" } else { "RELEASE" }))
        foreach ($entry in $expectedSwitchSymbols.GetEnumerator()) {
            if ([bool] $entry.Value) {
                [void] $expectedSymbols.Add([string] $entry.Key)
            }
        }
        $actualSymbols = New-Object 'System.Collections.Generic.HashSet[string]' (
            [System.StringComparer]::Ordinal)
        foreach ($symbol in $symbols) {
            [void] $actualSymbols.Add($symbol)
        }
        if ($actualSymbols.Count -ne $expectedSymbols.Count) {
            return New-M01SourceClosureResult `
                -Succeeded $false `
                -Reason "evaluated_define_constants_mismatch" `
                -Detail ($profile + ":count=" + $actualSymbols.Count)
        }
        foreach ($symbol in $expectedSymbols) {
            if (-not $actualSymbols.Contains($symbol)) {
                return New-M01SourceClosureResult `
                    -Succeeded $false `
                    -Reason "evaluated_define_constants_mismatch" `
                    -Detail ($profile + ":missing=" + $symbol)
            }
        }

        $compileItems = @($evaluation.Items.Compile)
        $projectReferences = @($evaluation.Items.ProjectReference)
        $references = @($evaluation.Items.Reference)
        $analyzers = @($evaluation.Items.Analyzer)
        $serializerInputs = @(
            $evaluation.Items._ClrosX64SerializerGeneratorInput)
        if ($compileItems.Count -ne $script:ExpectedEfiCompileItems.Count -or
            $projectReferences.Count -ne 0 -or
            $references.Count -ne 0 -or
            $analyzers.Count -ne 2 -or
            $serializerInputs.Count -ne 8) {
            return New-M01SourceClosureResult `
                -Succeeded $false `
                -Reason "evaluated_item_count_mismatch" `
                -Detail (
                    $profile +
                    ":compile=" + $compileItems.Count +
                    ";project=" + $projectReferences.Count +
                    ";reference=" + $references.Count +
                    ";analyzer=" + $analyzers.Count +
                    ";serializer=" + $serializerInputs.Count)
        }

        $serializerTargetsPath = [System.IO.Path]::GetFullPath(
            (Join-Path $projectDirectory $script:ExpectedSerializerTargetsImport))
        $serializerDirectory = Split-Path -Parent $serializerTargetsPath
        $expectedSerializerInputPaths = @(
            (Join-Path $serializerDirectory (
                "OS.X64TextSerializerGenerator.csproj")),
            (Join-Path $serializerDirectory "Program.cs"),
            (Join-Path $serializerDirectory "Amd64CoffWriter.cs"),
            (Join-Path $serializerDirectory "CheckedReferenceBarrierThunkGenerator.cs"),
            (Join-Path $serializerDirectory (
                "InstructionPublicationSerializerGenerator.cs")),
            (Join-Path $serializerDirectory "InterfaceDispatchThunkGenerator.cs"),
            (Join-Path $serializerDirectory "ExceptionHandlingThunkGenerator.cs"),
            $serializerTargetsPath
        )
        for ($serializerIndex = 0;
            $serializerIndex -lt $expectedSerializerInputPaths.Count;
            $serializerIndex++) {
            $expectedSerializerInputPath = [System.IO.Path]::GetFullPath(
                $expectedSerializerInputPaths[$serializerIndex])
            $serializerInput = $serializerInputs[$serializerIndex]
            if (-not [string]::Equals(
                    [string] $serializerInput.Identity,
                    $expectedSerializerInputPath,
                    [System.StringComparison]::OrdinalIgnoreCase) -or
                -not [string]::Equals(
                    [string] $serializerInput.FullPath,
                    $expectedSerializerInputPath,
                    [System.StringComparison]::OrdinalIgnoreCase) -or
                -not [string]::Equals(
                    [string] $serializerInput.DefiningProjectFullPath,
                    $serializerTargetsPath,
                    [System.StringComparison]::OrdinalIgnoreCase)) {
                return New-M01SourceClosureResult `
                    -Succeeded $false `
                    -Reason "evaluated_serializer_input_mismatch" `
                    -Detail ($profile + ":" + $serializerIndex)
            }
        }

        $expectedAnalyzerNames = @(
            "Microsoft.CodeAnalysis.CSharp.NetAnalyzers.dll",
            "Microsoft.CodeAnalysis.NetAnalyzers.dll"
        )
        $actualAnalyzerNames = New-Object `
            'System.Collections.Generic.HashSet[string]' (
                [System.StringComparer]::Ordinal)
        foreach ($analyzer in $analyzers) {
            $analyzerName = [System.IO.Path]::GetFileName(
                [string] $analyzer.FullPath)
            $definingProjectName = [System.IO.Path]::GetFileName(
                [string] $analyzer.DefiningProjectFullPath)
            if ($expectedAnalyzerNames -notcontains $analyzerName -or
                $definingProjectName -ne "Microsoft.NET.Sdk.Analyzers.targets") {
                return New-M01SourceClosureResult `
                    -Succeeded $false `
                    -Reason "evaluated_analyzer_mismatch" `
                    -Detail ($profile + ":" + $analyzerName)
            }
            [void] $actualAnalyzerNames.Add($analyzerName)
        }
        if ($actualAnalyzerNames.Count -ne $expectedAnalyzerNames.Count) {
            return New-M01SourceClosureResult `
                -Succeeded $false `
                -Reason "evaluated_analyzer_mismatch" `
                -Detail ($profile + ":duplicate")
        }

        for ($index = 0; $index -lt $compileItems.Count; $index++) {
            $item = $compileItems[$index]
            $link = if ($null -eq $item.Link) { "" } else { [string] $item.Link }
            $actualKey = [string] $item.Identity + "|" + $link
            $expectedKey = $script:ExpectedEfiCompileItems[$index]
            $expectedInclude = $expectedKey.Substring(
                0,
                $expectedKey.IndexOf(
                    "|",
                    [System.StringComparison]::Ordinal))
            $expectedFullPath = [System.IO.Path]::GetFullPath(
                (Join-Path $projectDirectory $expectedInclude))
            if (-not [string]::Equals(
                    $actualKey,
                    $expectedKey,
                    [System.StringComparison]::Ordinal) -or
                -not [string]::Equals(
                    [string] $item.FullPath,
                    $expectedFullPath,
                    [System.StringComparison]::OrdinalIgnoreCase) -or
                -not [string]::Equals(
                    [string] $item.DefiningProjectFullPath,
                    [System.IO.Path]::GetFullPath($ProjectPath),
                    [System.StringComparison]::OrdinalIgnoreCase)) {
                return New-M01SourceClosureResult `
                    -Succeeded $false `
                    -Reason "evaluated_compile_item_mismatch" `
                    -Detail ($profile + ":" + $index + ":" + $actualKey)
            }
        }
    }

    $serializerTargetsPath = [System.IO.Path]::GetFullPath(
        (Join-Path $projectDirectory $script:ExpectedSerializerTargetsImport))
    $serializerGeneratorProjectPath = Join-Path (
        Split-Path -Parent $serializerTargetsPath) (
        "OS.X64TextSerializerGenerator.csproj")
    $serializerGeneratorDirectory =
        Split-Path -Parent $serializerGeneratorProjectPath
    $serializerToolsPropsPath = Join-Path (
        Split-Path -Parent $serializerGeneratorDirectory) (
        "Directory.Build.props")
    $serializerRootTargetsPath = Join-Path $RepositoryRoot (
        "Directory.Build.targets")
    $generatorEvaluationRoot = Join-Path (
        $serializerGeneratorDirectory) (
        "obj\clros-m01-034-source-closure")
    $generatorEvaluationOutputPath = (
        Join-Path $generatorEvaluationRoot "bin").Replace(
            [System.IO.Path]::DirectorySeparatorChar,
            [System.IO.Path]::AltDirectorySeparatorChar) + "/"
    $generatorEvaluationIntermediatePath = (
        Join-Path $generatorEvaluationRoot "obj").Replace(
            [System.IO.Path]::DirectorySeparatorChar,
            [System.IO.Path]::AltDirectorySeparatorChar) + "/"
    $generatorProjectExtensionsPath = (
        Join-Path (
            [System.IO.Path]::GetTempPath()) (
            "clros-m01-generator-evaluation-" +
            [System.Guid]::NewGuid().ToString("N"))
    ).Replace("\", "/").TrimEnd("/") + "/"
    $generatorAssetsPath = Join-Path (
        $generatorProjectExtensionsPath) (
        "project.assets.json")
    $generatorIsolation = Get-M01MsbuildIsolationContract `
        -ProjectExtensionsDirectory $generatorProjectExtensionsPath
    $generatorArguments = @(
        "msbuild",
        $serializerGeneratorProjectPath,
        "-noAutoResponse",
        "-nologo",
        (
            "-getItem:" +
            "Compile,ProjectReference,Reference,PackageReference,Analyzer"
        ),
        (
            "-getProperty:EnableDefaultCompileItems,MSBuildSDKsPath," +
            "MSBuildExtensionsPath,MSBuildUserExtensionsPath," +
            "ImportByWildcardBeforeMicrosoftCommonProps," +
            "ImportByWildcardAfterMicrosoftCommonProps," +
            "ImportUserLocationsByWildcardBeforeMicrosoftCommonProps," +
            "ImportUserLocationsByWildcardAfterMicrosoftCommonProps," +
            "ImportByWildcardBeforeMicrosoftCommonTargets," +
            "ImportByWildcardAfterMicrosoftCommonTargets," +
            "ImportUserLocationsByWildcardBeforeMicrosoftCommonTargets," +
            "ImportUserLocationsByWildcardAfterMicrosoftCommonTargets," +
            "ImportByWildcardBeforeMicrosoftCSharpTargets," +
            "ImportByWildcardAfterMicrosoftCSharpTargets," +
            "ImportUserLocationsByWildcardBeforeMicrosoftCSharpTargets," +
            "ImportUserLocationsByWildcardAfterMicrosoftCSharpTargets," +
            "ImportByWildcardBeforeMicrosoftCommonCrossTargetingTargets," +
            "ImportByWildcardAfterMicrosoftCommonCrossTargetingTargets," +
            "UseSharedCompilation"
        ),
        ("-p:Configuration=" + $Configuration),
        (
            "-p:BaseOutputPath=" +
            $generatorEvaluationOutputPath),
        (
            "-p:BaseIntermediateOutputPath=" +
            $generatorEvaluationIntermediatePath),
        (
            "-p:MSBuildProjectExtensionsPath=" +
            $generatorProjectExtensionsPath),
        ("-p:ProjectAssetsFile=" + $generatorAssetsPath),
        "-p:ImportProjectExtensionProps=false",
        "-p:ImportProjectExtensionTargets=false",
        "-p:ExcludeRestorePackageImports=true",
        "-p:DefaultItemExcludes=obj/**",
        "-p:DefaultExcludesInProjectFolder=bin/**",
        (
            "-p:CustomBeforeMicrosoftCommonTargets=" +
            $serializerTargetsPath),
        ("-p:DirectoryBuildPropsPath=" + $serializerToolsPropsPath),
        ("-p:DirectoryBuildTargetsPath=" + $serializerRootTargetsPath),
        "-p:ImportDirectoryBuildProps=true",
        "-p:ImportDirectoryBuildTargets=true",
        "-p:CustomBeforeMicrosoftCommonProps=",
        "-p:CustomAfterMicrosoftCommonProps=",
        "-p:CustomBeforeDirectoryBuildProps=",
        "-p:CustomAfterDirectoryBuildProps=",
        "-p:CustomAfterMicrosoftCommonTargets=",
        "-p:CustomBeforeDirectoryBuildTargets=",
        "-p:CustomAfterDirectoryBuildTargets=",
        "-p:CustomBeforeMicrosoftCSharpTargets=",
        "-p:CustomAfterMicrosoftCSharpTargets=",
        "-p:CustomBeforeMicrosoftCommonCrossTargetingTargets=",
        "-p:CustomAfterMicrosoftCommonCrossTargetingTargets=",
        "-p:BeforeMicrosoftNETSdkTargets=",
        "-p:AfterMicrosoftNETSdkTargets=",
        "-p:RestoreIgnoreFailedSources=true"
    )
    $generatorArguments += $generatorIsolation.Arguments
    $generatorProcessResult = Invoke-M01BoundedProcess `
        -FilePath $script:DotnetHostPath `
        -Arguments $generatorArguments `
        -WorkingDirectory $RepositoryRoot `
        -Operation "serializer-generator-evaluation" `
        -TimeoutSeconds $BuildProcessTimeoutSeconds `
        -MaximumOutputCharacters $BuildProcessMaximumOutputCharacters `
        -EnvironmentOverrides $generatorIsolation.Environment
    $generatorProcessFailure = Get-M01BoundedProcessFailureReason `
        -Result $generatorProcessResult
    $generatorOutputText = $generatorProcessResult.OutputText
    if ($generatorProcessFailure -cne "ok") {
        return New-M01SourceClosureResult `
            -Succeeded $false `
            -Reason (
                "serializer_generator_evaluation_process_" +
                $generatorProcessFailure) `
            -Detail (
                $serializerGeneratorProjectPath +
                ":" +
                $generatorProcessResult.OutputText)
    }

    $generatorJsonStart = $generatorOutputText.IndexOf(
        "{",
        [System.StringComparison]::Ordinal)
    $generatorJsonEnd = $generatorOutputText.LastIndexOf(
        "}",
        [System.StringComparison]::Ordinal)
    if ($generatorJsonStart -lt 0 -or
        $generatorJsonEnd -le $generatorJsonStart) {
        return New-M01SourceClosureResult `
            -Succeeded $false `
            -Reason "serializer_generator_evaluation_malformed" `
            -Detail "json"
    }

    try {
        $generatorEvaluation = $generatorOutputText.Substring(
            $generatorJsonStart,
            $generatorJsonEnd - $generatorJsonStart + 1) | ConvertFrom-Json
    }
    catch {
        return New-M01SourceClosureResult `
            -Succeeded $false `
            -Reason "serializer_generator_evaluation_malformed" `
            -Detail "parse"
    }

    $generatorCompileItems = @($generatorEvaluation.Items.Compile)
    $generatorProjectReferences = @(
        $generatorEvaluation.Items.ProjectReference)
    $generatorReferences = @($generatorEvaluation.Items.Reference)
    $generatorPackageReferences = @(
        $generatorEvaluation.Items.PackageReference)
    $generatorAnalyzers = @($generatorEvaluation.Items.Analyzer)
    foreach ($entry in $generatorIsolation.Properties.GetEnumerator()) {
        if ([string] $generatorEvaluation.Properties.($entry.Key) -cne
            [string] $entry.Value) {
            return New-M01SourceClosureResult `
                -Succeeded $false `
                -Reason "serializer_generator_property_mismatch" `
                -Detail $entry.Key
        }
    }
    $expectedGeneratorCompileItems = @(
        "Amd64CoffWriter.cs",
        "CheckedReferenceBarrierThunkGenerator.cs",
        "ExceptionHandlingThunkGenerator.cs",
        "InstructionPublicationSerializerGenerator.cs",
        "InterfaceDispatchThunkGenerator.cs",
        "Program.cs"
    )
    if ($generatorEvaluation.Properties.EnableDefaultCompileItems -ne "true" -or
        $generatorCompileItems.Count -ne $expectedGeneratorCompileItems.Count -or
        $generatorProjectReferences.Count -ne 0 -or
        $generatorReferences.Count -ne 0 -or
        $generatorPackageReferences.Count -ne 0 -or
        $generatorAnalyzers.Count -ne 2) {
        return New-M01SourceClosureResult `
            -Succeeded $false `
            -Reason "serializer_generator_compile_count_mismatch" `
            -Detail (
                "compile=" + $generatorCompileItems.Count +
                ";project=" + $generatorProjectReferences.Count +
                ";reference=" + $generatorReferences.Count +
                ";package=" + $generatorPackageReferences.Count +
                ";analyzer=" + $generatorAnalyzers.Count)
    }

    for ($generatorIndex = 0;
        $generatorIndex -lt $expectedGeneratorCompileItems.Count;
        $generatorIndex++) {
        $generatorCompileItem = $generatorCompileItems[$generatorIndex]
        $expectedGeneratorIdentity =
            $expectedGeneratorCompileItems[$generatorIndex]
        $expectedGeneratorFullPath = [System.IO.Path]::GetFullPath(
            (Join-Path $serializerGeneratorDirectory $expectedGeneratorIdentity))
        $definingProjectName = [System.IO.Path]::GetFileName(
            [string] $generatorCompileItem.DefiningProjectFullPath)
        if ([string] $generatorCompileItem.Identity -cne
                $expectedGeneratorIdentity -or
            -not [string]::Equals(
                [string] $generatorCompileItem.FullPath,
                $expectedGeneratorFullPath,
                [System.StringComparison]::OrdinalIgnoreCase) -or
            $definingProjectName -ne "Microsoft.NET.Sdk.DefaultItems.props") {
            return New-M01SourceClosureResult `
                -Succeeded $false `
                -Reason "serializer_generator_compile_item_mismatch" `
                -Detail $generatorIndex
        }
    }

    $expectedGeneratorAnalyzerNames = @(
        "Microsoft.CodeAnalysis.CSharp.NetAnalyzers.dll",
        "Microsoft.CodeAnalysis.NetAnalyzers.dll"
    )
    for ($analyzerIndex = 0;
        $analyzerIndex -lt $expectedGeneratorAnalyzerNames.Count;
        $analyzerIndex++) {
        $analyzer = $generatorAnalyzers[$analyzerIndex]
        if ([System.IO.Path]::GetFileName([string] $analyzer.FullPath) -ne
                $expectedGeneratorAnalyzerNames[$analyzerIndex] -or
            [System.IO.Path]::GetFileName(
                [string] $analyzer.DefiningProjectFullPath) -ne
                "Microsoft.NET.Sdk.Analyzers.targets") {
            return New-M01SourceClosureResult `
                -Succeeded $false `
                -Reason "serializer_generator_analyzer_mismatch" `
                -Detail $analyzerIndex
        }
    }

    return New-M01SourceClosureResult `
        -Succeeded $true `
        -Reason "ok" `
        -Detail "" `
        -CompileItemCount $script:ExpectedEfiCompileItems.Count
}

function Test-M01SourceClosure {
    param(
        [string] $RepositoryRoot,
        [string] $ProjectPath,
        [string] $Configuration,
        [bool] $LoadedImageStackEnabled,
        [bool] $LiveAllocatorEnabled,
        [bool] $OwnedPagingEnabled,
        [bool] $M02SafeStubEnabled,
        [bool] $M02CheckedPortIoBindingEnabled,
        [bool] $M02Com1OwnedPortIoEnabled,
        [bool] $M02OwnedCom1ReadyEnabled,
        [bool] $M02PanicSafeOwnedCom1Enabled,
        [bool] $M02OwnedX64GdtEncodingEnabled,
        [bool] $M02GuardedIstStackAllocationEnabled,
        [bool] $M02OwnedGdtTssTerminalEnabled,
        [bool] $M02NormalizedInterruptFramePageEnabled,
        [bool] $M02OwnedIdtGatePopulationEnabled,
        [bool] $M02IdtrContinuedExecutionEnabled,
        [bool] $M02Ud2VectorStateEnabled,
        [bool] $M02PageFaultCr2ErrorCodeEnabled,
        [bool] $M02DoubleFaultIstEnabled,
        [bool] $M02UnexpectedVectorPanicEnabled,
        [bool] $M04IndependentEndianConformanceEnabled,
        [bool] $M04SpanMemoryConformanceEnabled,
        [bool] $M04ArithmeticConformanceEnabled,
        [bool] $M04LayoutContractEnabled,
        [bool] $M04ManagedHeapPageRangeEnabled,
        [bool] $M04RhpNewFastEnabled,
        [bool] $M04RhpNewFastBeforeBindFailureEnabled,
        [bool] $M04RhpNewFastExhaustionFailureEnabled,
        [bool] $M04SzArrayAllocationEnabled,
        [bool] $M04SzArrayAllocationBeforeBindEnabled,
        [bool] $M04SzArrayAllocationExhaustionEnabled,
        [bool] $M04StringAllocationEnabled,
        [bool] $M04StringAllocationBeforeBindEnabled,
        [bool] $M04StringAllocationExhaustionEnabled,
        [bool] $M04StringAllocationInvalidLengthEnabled,
        [bool] $M03MemoryMapCaptureEnabled,
        [bool] $M03FirmwareEntropySeedEnabled,
        [bool] $M04TypeManagerModuleRegistrationEnabled,
        [bool] $M04X64UnwindMetadataEnabled,
        [bool] $M04SimpleThrowCatchEnabled,
        [bool] $M04GcStaticRootMaterializationEnabled,
        [bool] $M04SeededCollectorChurnEnabled,
        [bool] $M03TypedExitBootServicesCallEnabled,
        [bool] $M03FirstRealExitBootServicesEnabled,
        [bool] $M03FirmwareAdapterRevocationEnabled,
        [bool] $M03OwnedSubsystemSurvivalEnabled,
        [bool] $M03OwnedSubsystemPanicEnabled,
        [bool] $M04ClassConstructorExecutionEnabled,
        [bool] $M04InterfaceDispatchEnabled,
        [bool] $M04CheckedReferenceBarrierEnabled,
        [bool] $UefiGoneSimulationEnabled,
        [bool] $ExitBootServicesFreshMapEnabled,
        [bool] $AcpiPhysicalDiscoveryEnabled
    )

    $canonicalProjectPath = [System.IO.Path]::GetFullPath(
        (Join-Path $RepositoryRoot "src\OS\OS.csproj"))
    $evaluatedProjectPath = [System.IO.Path]::GetFullPath($ProjectPath)
    if (-not [string]::Equals(
            $canonicalProjectPath,
            $evaluatedProjectPath,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        return New-M01SourceClosureResult `
            -Succeeded $false `
            -Reason "noncanonical_project" `
            -Detail $evaluatedProjectPath
    }

    if (-not (Test-Path -LiteralPath $canonicalProjectPath -PathType Leaf)) {
        return New-M01SourceClosureResult `
            -Succeeded $false `
            -Reason "project_missing" `
            -Detail $canonicalProjectPath
    }

    try {
        $projectText = Get-Content -LiteralPath $canonicalProjectPath -Raw
        $project = Read-M01SecureXmlDocument -Path $canonicalProjectPath
    }
    catch {
        return New-M01SourceClosureResult `
            -Succeeded $false `
            -Reason "project_malformed" `
            -Detail $canonicalProjectPath
    }

    $serializerImport = Test-M01SerializerTargetsImport `
        -RepositoryRoot $RepositoryRoot `
        -ProjectPath $canonicalProjectPath `
        -Project $project
    if (-not $serializerImport.Succeeded) {
        return New-M01SourceClosureResult `
            -Succeeded $false `
            -Reason $serializerImport.Reason `
            -Detail $serializerImport.Detail
    }

    $canonicalProjectHash = Get-M01CanonicalXmlSha256 -Document $project
    if ($canonicalProjectHash -ne
        "52b4d0078a27607575f02e2b1c21c750281d746badf5d354db5b40ccb27d7c5a") {
        return New-M01SourceClosureResult `
            -Succeeded $false `
            -Reason "noncanonical_project_shape" `
            -Detail $canonicalProjectHash
    }

    if ($project.Project.Sdk -ne "Microsoft.NET.Sdk") {
        return New-M01SourceClosureResult `
            -Succeeded $false `
            -Reason "noncanonical_project" `
            -Detail "sdk"
    }

    $requiredSingleProperties = [ordered] @{
        "AssemblyName" = "OS"
        "RootNamespace" = "OS"
        "TargetFramework" = "net10.0"
        "LangVersion" = "14.0"
        "RuntimeIdentifier" = "win-x64"
        "EnableDefaultCompileItems" = "false"
        "NoStdLib" = "true"
        "DisableImplicitFrameworkReferences" = "true"
    }
    foreach ($entry in $requiredSingleProperties.GetEnumerator()) {
        $nodes = @($project.SelectNodes("/Project/PropertyGroup/" + $entry.Key))
        if ($nodes.Count -ne 1 -or
            $nodes[0].InnerText -ne $entry.Value -or
            $nodes[0].Attributes.Count -ne 0 -or
            $nodes[0].SelectNodes("*").Count -ne 0 -or
            $nodes[0].ParentNode.Name -ne "PropertyGroup" -or
            $nodes[0].ParentNode.Attributes.Count -ne 0) {
            return New-M01SourceClosureResult `
                -Succeeded $false `
                -Reason "noncanonical_project" `
                -Detail ("property:" + $entry.Key)
        }
    }

    $baseDefineValue = '$' + '(DefineConstants);NOSTDLIB_EFI'
    $defineNodes = @($project.SelectNodes("//DefineConstants"))
    $baseDefineNodes = @($defineNodes | Where-Object {
        -not $_.ParentNode.HasAttribute("Condition")
    })
    if ($baseDefineNodes.Count -ne 1 -or
        $baseDefineNodes[0].HasAttribute("Condition") -or
        $baseDefineNodes[0].InnerText -ne $baseDefineValue) {
        return New-M01SourceClosureResult `
            -Succeeded $false `
            -Reason "nostdlib_efi_symbol_missing" `
            -Detail "NOSTDLIB_EFI"
    }

    $allowedConditionalSymbols = [ordered] @{
        "EnableM01LoadedImageStackProbe" = "M01_LOADED_IMAGE_STACK_PROBE"
        "EnableM01LiveAllocatorProbe" = "M01_LIVE_ALLOCATOR_PROBE"
        "EnableM01OwnedPagingProbe" = "M01_OWNED_PAGING_PROBE"
        "EnableM02SafeStubProbe" = "M02_SAFE_STUB_PROBE"
        "EnableM02CheckedPortIoBindingProbe" = "M02_CHECKED_PORT_IO_BINDING_PROBE"
        "EnableM02Com1OwnedPortIoProbe" = "M02_COM1_OWNED_PORT_IO_PROBE"
        "EnableM02OwnedCom1ReadyProbe" = "M02_OWNED_COM1_READY_PROBE"
        "EnableM02PanicSafeOwnedCom1Probe" = "M02_PANIC_SAFE_OWNED_COM1_PROBE"
        "EnableM02OwnedX64GdtEncodingProbe" = "M02_OWNED_X64_GDT_ENCODING_PROBE"
        "EnableM02GuardedIstStackAllocationProbe" =
            "M02_GUARDED_IST_STACK_ALLOCATION_PROBE"
        "EnableM02OwnedGdtTssTerminalProbe" =
            "M02_OWNED_GDT_TSS_TERMINAL_PROBE"
        "EnableM02NormalizedInterruptFramePageProbe" =
            "M02_NORMALIZED_INTERRUPT_FRAME_PAGE_PROBE"
        "EnableM02OwnedIdtGatePopulationProbe" =
            "M02_OWNED_IDT_GATE_POPULATION_PROBE"
        "EnableM02IdtrContinuedExecutionProbe" =
            "M02_IDTR_CONTINUED_EXECUTION_PROBE"
        "EnableM02Ud2VectorStateProbe" = "M02_UD2_VECTOR_STATE_PROBE"
        "EnableM02PageFaultCr2ErrorCodeProbe" = "M02_PAGE_FAULT_CR2_ERROR_CODE_PROBE"
        "EnableM02DoubleFaultIstProbe" = "M02_DOUBLE_FAULT_IST_PROBE"
        "EnableM02UnexpectedVectorPanicProbe" = "M02_UNEXPECTED_VECTOR_PANIC_PROBE"
        "EnableM04IndependentEndianConformanceProbe" =
            "M04_INDEPENDENT_ENDIAN_CONFORMANCE_PROBE"
        "EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe" =
            "M04_SPAN_OVERLAP_MEMORY_CAST_UNSAFE_WIDTH_CONFORMANCE_PROBE"
        "EnableM04ArithmeticConformanceProbe" =
            "M04_ARITHMETIC_CONFORMANCE_PROBE"
        "EnableM04LayoutContractProbe" =
            "M04_LAYOUT_CONTRACT_PROBE"
        "EnableM04ManagedHeapPageRangeProbe" =
            "M04_MANAGED_HEAP_PAGE_RANGE_PROBE"
        "EnableM04RhpNewFastProbe" =
            "M04_RHP_NEW_FAST_PROBE"
        "EnableM04RhpNewFastBeforeBindFailureProbe" =
            "M04_RHP_NEW_FAST_BEFORE_BIND_FAILURE_PROBE"
        "EnableM04RhpNewFastExhaustionFailureProbe" =
            "M04_RHP_NEW_FAST_EXHAUSTION_FAILURE_PROBE"
        "EnableM04SzArrayAllocationProbe" =
            "M04_SZ_ARRAY_ALLOCATION_PROBE"
        "EnableM04SzArrayAllocationBeforeBindProbe" =
            "M04_SZ_ARRAY_ALLOCATION_BEFORE_BIND_PROBE"
        "EnableM04SzArrayAllocationExhaustionProbe" =
            "M04_SZ_ARRAY_ALLOCATION_EXHAUSTION_PROBE"
        "EnableM04StringAllocationProbe" =
            "M04_STRING_ALLOCATION_PROBE"
        "EnableM04StringAllocationBeforeBindProbe" =
            "M04_STRING_ALLOCATION_BEFORE_BIND_PROBE"
        "EnableM04StringAllocationExhaustionProbe" =
            "M04_STRING_ALLOCATION_EXHAUSTION_PROBE"
        "EnableM04StringAllocationInvalidLengthProbe" =
            "M04_STRING_ALLOCATION_INVALID_LENGTH_PROBE"
        "EnableM03MemoryMapCaptureProbe" =
            "M03_MEMORY_MAP_CAPTURE_PROBE"
        "EnableM03FirmwareEntropySeedProbe" =
            "M03_FIRMWARE_ENTROPY_SEED_PROBE"
        "EnableM04TypeManagerModuleRegistrationProbe" =
            "M04_TYPE_MANAGER_MODULE_REGISTRATION_PROBE"
        "EnableM04X64UnwindMetadataProbe" =
            "M04_X64_UNWIND_METADATA_PROBE"
        "EnableM04SimpleThrowCatchProbe" =
            "M04_SIMPLE_THROW_CATCH_PROBE"
        "EnableM04GcStaticRootMaterializationProbe" =
            "M04_GC_STATIC_ROOT_MATERIALIZATION_PROBE"
        "EnableM04SeededCollectorChurnProbe" =
            "M04_SEEDED_COLLECTOR_CHURN_PROBE"
        "EnableM03TypedExitBootServicesCallProbe" =
            "M03_TYPED_EXIT_BOOT_SERVICES_CALL_PROBE"
        "EnableM03FirstRealExitBootServicesProbe" =
            "M03_FIRST_REAL_EXIT_BOOT_SERVICES_PROBE"
        "EnableM03FirmwareAdapterRevocationProbe" =
            "M03_FIRMWARE_ADAPTER_REVOCATION_PROBE"
        "EnableM03OwnedSubsystemSurvivalProbe" =
            "M03_OWNED_SUBSYSTEM_SURVIVAL_PROBE"
        "EnableM03OwnedSubsystemPanicProbe" =
            "M03_OWNED_SUBSYSTEM_PANIC_PROBE"
        "EnableM04ClassConstructorExecutionProbe" =
            "M04_CLASS_CONSTRUCTOR_EXECUTION_PROBE"
        "EnableM04InterfaceDispatchProbe" =
            "M04_INTERFACE_DISPATCH_PROBE"
        "EnableM04CheckedReferenceBarrierProbe" =
            "M04_CHECKED_REFERENCE_BARRIER_PROBE"
        "EnableUefiGoneSimulationProbe" = "UEFI_GONE_SIMULATION_PROBE"
        "EnableExitBootServicesFreshMapProbe" = "EXIT_BOOT_SERVICES_FRESH_MAP_PROBE"
        "EnableAcpiPhysicalDiscoveryProbe" = "ACPI_PHYSICAL_DISCOVERY_PROBE"
    }
    if ($defineNodes.Count -ne (1 + $allowedConditionalSymbols.Count)) {
        return New-M01SourceClosureResult `
            -Succeeded $false `
            -Reason "define_constants_shape_invalid" `
            -Detail $defineNodes.Count
    }

    foreach ($entry in $allowedConditionalSymbols.GetEnumerator()) {
        $condition = "'" + '$' + '(' + $entry.Key + ")' == 'true'"
        $groups = @($project.SelectNodes("/Project/PropertyGroup") |
            Where-Object { $_.GetAttribute("Condition") -eq $condition })
        if ($entry.Key -ceq "EnableM02Ud2VectorStateProbe" -or $entry.Key -ceq "EnableM02PageFaultCr2ErrorCodeProbe" -or $entry.Key -ceq "EnableM02DoubleFaultIstProbe" -or $entry.Key -ceq "EnableM02UnexpectedVectorPanicProbe") {
            $symbolGroups = @($groups | Where-Object {
                $_.SelectNodes("DefineConstants").Count -eq 1
            })
            if ($groups.Count -ne 2 -or
                $symbolGroups.Count -ne 1 -or
                $symbolGroups[0].SelectNodes("*").Count -ne 1) {
                return New-M01SourceClosureResult `
                    -Succeeded $false `
                    -Reason "switch_symbol_invalid" `
                    -Detail $entry.Key
            }
            $symbolGroup = $symbolGroups[0]
        }
        else {
            if ($groups.Count -ne 1 -or
                $groups[0].SelectNodes("*").Count -ne 1) {
                return New-M01SourceClosureResult `
                    -Succeeded $false `
                    -Reason "switch_symbol_invalid" `
                    -Detail $entry.Key
            }
            $symbolGroup = $groups[0]
        }

        $defines = @($symbolGroup.SelectNodes("DefineConstants"))
        $expectedDefineConstants =
            '$' + '(DefineConstants);' + $entry.Value
        if ($defines.Count -ne 1 -or
            $defines[0].HasAttribute("Condition") -or
            $defines[0].InnerText -ne $expectedDefineConstants) {
            return New-M01SourceClosureResult `
                -Succeeded $false `
                -Reason "switch_symbol_invalid" `
            -Detail $entry.Key
        }
    }

    $m02Ud2VectorStateImplications = [ordered] @{
        "EnableM01LoadedImageStackProbe" = "true"
        "EnableM01LiveAllocatorProbe" = "true"
        "EnableM01OwnedPagingProbe" = "true"
        "EnableM02SafeStubProbe" = "true"
        "EnableM02CheckedPortIoBindingProbe" = "true"
        "EnableM02Com1OwnedPortIoProbe" = "true"
        "EnableM02OwnedCom1ReadyProbe" = "true"
        "EnableM02PanicSafeOwnedCom1Probe" = "true"
    }
    $m02Ud2VectorStateCondition =
        "'" + '$' + "(EnableM02Ud2VectorStateProbe)' == 'true'"
    $m02Ud2VectorStateGroups = @(
        $project.SelectNodes("/Project/PropertyGroup") |
            Where-Object {
                $_.GetAttribute("Condition") -eq
                    $m02Ud2VectorStateCondition
            })
    $m02Ud2VectorStateImplicationGroups = @(
        $m02Ud2VectorStateGroups | Where-Object {
            $_.SelectNodes("DefineConstants").Count -eq 0
        })
    if ($m02Ud2VectorStateGroups.Count -ne 2 -or
        $m02Ud2VectorStateImplicationGroups.Count -ne 1 -or
        $m02Ud2VectorStateImplicationGroups[0].SelectNodes("*").Count -ne 8) {
        return New-M01SourceClosureResult `
            -Succeeded $false `
            -Reason "switch_implication_invalid" `
            -Detail "EnableM02Ud2VectorStateProbe:group"
    }
    foreach ($implication in
        $m02Ud2VectorStateImplications.GetEnumerator()) {
        $nodes = @($m02Ud2VectorStateImplicationGroups[0].SelectNodes(
            $implication.Key))
        if ($nodes.Count -ne 1 -or
            $nodes[0].InnerText -cne $implication.Value -or
            $nodes[0].Attributes.Count -ne 0 -or
            $nodes[0].SelectNodes("*").Count -ne 0) {
            return New-M01SourceClosureResult `
                -Succeeded $false `
                -Reason "switch_implication_invalid" `
                -Detail $implication.Key
        }
    }

    $m02PageFaultCr2ErrorCodeImplications = [ordered] @{
        "EnableM01LoadedImageStackProbe" = "true"
        "EnableM01LiveAllocatorProbe" = "true"
        "EnableM01OwnedPagingProbe" = "true"
        "EnableM02SafeStubProbe" = "true"
        "EnableM02CheckedPortIoBindingProbe" = "true"
        "EnableM02Com1OwnedPortIoProbe" = "true"
        "EnableM02OwnedCom1ReadyProbe" = "true"
        "EnableM02PanicSafeOwnedCom1Probe" = "true"
    }
    $m02PageFaultCr2ErrorCodeCondition =
        "'" + '$' + "(EnableM02PageFaultCr2ErrorCodeProbe)' == 'true'"
    $m02PageFaultCr2ErrorCodeGroups = @(
        $project.SelectNodes("/Project/PropertyGroup") |
            Where-Object {
                $_.GetAttribute("Condition") -eq
                    $m02PageFaultCr2ErrorCodeCondition
            })
    $m02PageFaultCr2ErrorCodeImplicationGroups = @(
        $m02PageFaultCr2ErrorCodeGroups | Where-Object {
            $_.SelectNodes("DefineConstants").Count -eq 0
        })
    if ($m02PageFaultCr2ErrorCodeGroups.Count -ne 2 -or
        $m02PageFaultCr2ErrorCodeImplicationGroups.Count -ne 1 -or
        $m02PageFaultCr2ErrorCodeImplicationGroups[0].SelectNodes("*").Count -ne 8) {
        return New-M01SourceClosureResult `
            -Succeeded $false `
            -Reason "switch_implication_invalid" `
            -Detail "EnableM02PageFaultCr2ErrorCodeProbe:group"
    }
    foreach ($implication in
        $m02PageFaultCr2ErrorCodeImplications.GetEnumerator()) {
        $nodes = @($m02PageFaultCr2ErrorCodeImplicationGroups[0].SelectNodes(
            $implication.Key))
        if ($nodes.Count -ne 1 -or
            $nodes[0].InnerText -cne $implication.Value -or
            $nodes[0].Attributes.Count -ne 0 -or
            $nodes[0].SelectNodes("*").Count -ne 0) {
            return New-M01SourceClosureResult `
                -Succeeded $false `
                -Reason "switch_implication_invalid" `
                -Detail $implication.Key
        }
    }

    $m02DoubleFaultIstImplications = [ordered] @{
        "EnableM01LoadedImageStackProbe" = "true"
        "EnableM01LiveAllocatorProbe" = "true"
        "EnableM01OwnedPagingProbe" = "true"
        "EnableM02SafeStubProbe" = "true"
        "EnableM02CheckedPortIoBindingProbe" = "true"
        "EnableM02Com1OwnedPortIoProbe" = "true"
        "EnableM02OwnedCom1ReadyProbe" = "true"
        "EnableM02PanicSafeOwnedCom1Probe" = "true"
    }
    $m02DoubleFaultIstCondition =
        "'" + '$' + "(EnableM02DoubleFaultIstProbe)' == 'true'"
    $m02DoubleFaultIstGroups = @(
        $project.SelectNodes("/Project/PropertyGroup") |
            Where-Object {
                $_.GetAttribute("Condition") -eq
                    $m02DoubleFaultIstCondition
            })
    $m02DoubleFaultIstImplicationGroups = @(
        $m02DoubleFaultIstGroups | Where-Object {
            $_.SelectNodes("DefineConstants").Count -eq 0
        })
    if ($m02DoubleFaultIstGroups.Count -ne 2 -or
        $m02DoubleFaultIstImplicationGroups.Count -ne 1 -or
        $m02DoubleFaultIstImplicationGroups[0].SelectNodes("*").Count -ne 8) {
        return New-M01SourceClosureResult `
            -Succeeded $false `
            -Reason "switch_implication_invalid" `
            -Detail "EnableM02DoubleFaultIstProbe:group"
    }
    foreach ($implication in
        $m02DoubleFaultIstImplications.GetEnumerator()) {
        $nodes = @($m02DoubleFaultIstImplicationGroups[0].SelectNodes(
            $implication.Key))
        if ($nodes.Count -ne 1 -or
            $nodes[0].InnerText -cne $implication.Value -or
            $nodes[0].Attributes.Count -ne 0 -or
            $nodes[0].SelectNodes("*").Count -ne 0) {
            return New-M01SourceClosureResult `
                -Succeeded $false `
                -Reason "switch_implication_invalid" `
                -Detail $implication.Key
        }
    }

    $m02UnexpectedVectorPanicImplications = [ordered] @{
        "EnableM01LoadedImageStackProbe" = "true"
        "EnableM01LiveAllocatorProbe" = "true"
        "EnableM01OwnedPagingProbe" = "true"
        "EnableM02SafeStubProbe" = "true"
        "EnableM02CheckedPortIoBindingProbe" = "true"
        "EnableM02Com1OwnedPortIoProbe" = "true"
        "EnableM02OwnedCom1ReadyProbe" = "true"
        "EnableM02PanicSafeOwnedCom1Probe" = "true"
    }
    $m02UnexpectedVectorPanicCondition =
        "'" + '$' + "(EnableM02UnexpectedVectorPanicProbe)' == 'true'"
    $m02UnexpectedVectorPanicGroups = @(
        $project.SelectNodes("/Project/PropertyGroup") |
            Where-Object {
                $_.GetAttribute("Condition") -eq
                    $m02UnexpectedVectorPanicCondition
            })
    $m02UnexpectedVectorPanicImplicationGroups = @(
        $m02UnexpectedVectorPanicGroups | Where-Object {
            $_.SelectNodes("DefineConstants").Count -eq 0
        })
    if ($m02UnexpectedVectorPanicGroups.Count -ne 2 -or
        $m02UnexpectedVectorPanicImplicationGroups.Count -ne 1 -or
        $m02UnexpectedVectorPanicImplicationGroups[0].SelectNodes("*").Count -ne 8) {
        return New-M01SourceClosureResult `
            -Succeeded $false `
            -Reason "switch_implication_invalid" `
            -Detail "EnableM02UnexpectedVectorPanicProbe:group"
    }
    foreach ($implication in
        $m02UnexpectedVectorPanicImplications.GetEnumerator()) {
        $nodes = @($m02UnexpectedVectorPanicImplicationGroups[0].SelectNodes(
            $implication.Key))
        if ($nodes.Count -ne 1 -or
            $nodes[0].InnerText -cne $implication.Value -or
            $nodes[0].Attributes.Count -ne 0 -or
            $nodes[0].SelectNodes("*").Count -ne 0) {
            return New-M01SourceClosureResult `
                -Succeeded $false `
                -Reason "switch_implication_invalid" `
                -Detail $implication.Key
        }
    }

    $switchSymbols = [ordered] @{
        "EnableM01LoadedImageStackProbe" = "M01_LOADED_IMAGE_STACK_PROBE"
        "EnableM01LiveAllocatorProbe" = "M01_LIVE_ALLOCATOR_PROBE"
        "EnableM01OwnedPagingProbe" = "M01_OWNED_PAGING_PROBE"
        "EnableM02SafeStubProbe" = "M02_SAFE_STUB_PROBE"
        "EnableM02CheckedPortIoBindingProbe" = "M02_CHECKED_PORT_IO_BINDING_PROBE"
        "EnableM02Com1OwnedPortIoProbe" = "M02_COM1_OWNED_PORT_IO_PROBE"
        "EnableM02OwnedCom1ReadyProbe" = "M02_OWNED_COM1_READY_PROBE"
        "EnableM02PanicSafeOwnedCom1Probe" = "M02_PANIC_SAFE_OWNED_COM1_PROBE"
        "EnableM02OwnedX64GdtEncodingProbe" = "M02_OWNED_X64_GDT_ENCODING_PROBE"
        "EnableM02GuardedIstStackAllocationProbe" =
            "M02_GUARDED_IST_STACK_ALLOCATION_PROBE"
        "EnableM02OwnedGdtTssTerminalProbe" =
            "M02_OWNED_GDT_TSS_TERMINAL_PROBE"
        "EnableM02NormalizedInterruptFramePageProbe" =
            "M02_NORMALIZED_INTERRUPT_FRAME_PAGE_PROBE"
        "EnableM02OwnedIdtGatePopulationProbe" =
            "M02_OWNED_IDT_GATE_POPULATION_PROBE"
        "EnableM02IdtrContinuedExecutionProbe" =
            "M02_IDTR_CONTINUED_EXECUTION_PROBE"
        "EnableM02Ud2VectorStateProbe" = "M02_UD2_VECTOR_STATE_PROBE"
        "EnableM02PageFaultCr2ErrorCodeProbe" = "M02_PAGE_FAULT_CR2_ERROR_CODE_PROBE"
        "EnableM02DoubleFaultIstProbe" = "M02_DOUBLE_FAULT_IST_PROBE"
        "EnableM02UnexpectedVectorPanicProbe" = "M02_UNEXPECTED_VECTOR_PANIC_PROBE"
        "EnableM04IndependentEndianConformanceProbe" =
            "M04_INDEPENDENT_ENDIAN_CONFORMANCE_PROBE"
        "EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe" =
            "M04_SPAN_OVERLAP_MEMORY_CAST_UNSAFE_WIDTH_CONFORMANCE_PROBE"
        "EnableM04ArithmeticConformanceProbe" =
            "M04_ARITHMETIC_CONFORMANCE_PROBE"
        "EnableM04LayoutContractProbe" =
            "M04_LAYOUT_CONTRACT_PROBE"
        "EnableM04ManagedHeapPageRangeProbe" =
            "M04_MANAGED_HEAP_PAGE_RANGE_PROBE"
        "EnableM04RhpNewFastProbe" =
            "M04_RHP_NEW_FAST_PROBE"
        "EnableM04RhpNewFastBeforeBindFailureProbe" =
            "M04_RHP_NEW_FAST_BEFORE_BIND_FAILURE_PROBE"
        "EnableM04RhpNewFastExhaustionFailureProbe" =
            "M04_RHP_NEW_FAST_EXHAUSTION_FAILURE_PROBE"
        "EnableM04SzArrayAllocationProbe" =
            "M04_SZ_ARRAY_ALLOCATION_PROBE"
        "EnableM04SzArrayAllocationBeforeBindProbe" =
            "M04_SZ_ARRAY_ALLOCATION_BEFORE_BIND_PROBE"
        "EnableM04SzArrayAllocationExhaustionProbe" =
            "M04_SZ_ARRAY_ALLOCATION_EXHAUSTION_PROBE"
        "EnableM04StringAllocationProbe" =
            "M04_STRING_ALLOCATION_PROBE"
        "EnableM04StringAllocationBeforeBindProbe" =
            "M04_STRING_ALLOCATION_BEFORE_BIND_PROBE"
        "EnableM04StringAllocationExhaustionProbe" =
            "M04_STRING_ALLOCATION_EXHAUSTION_PROBE"
        "EnableM04StringAllocationInvalidLengthProbe" =
            "M04_STRING_ALLOCATION_INVALID_LENGTH_PROBE"
        "EnableM03MemoryMapCaptureProbe" =
            "M03_MEMORY_MAP_CAPTURE_PROBE"
        "EnableM03FirmwareEntropySeedProbe" =
            "M03_FIRMWARE_ENTROPY_SEED_PROBE"
        "EnableM04TypeManagerModuleRegistrationProbe" =
            "M04_TYPE_MANAGER_MODULE_REGISTRATION_PROBE"
        "EnableM04X64UnwindMetadataProbe" =
            "M04_X64_UNWIND_METADATA_PROBE"
        "EnableM04SimpleThrowCatchProbe" =
            "M04_SIMPLE_THROW_CATCH_PROBE"
        "EnableM04GcStaticRootMaterializationProbe" =
            "M04_GC_STATIC_ROOT_MATERIALIZATION_PROBE"
        "EnableM04SeededCollectorChurnProbe" =
            "M04_SEEDED_COLLECTOR_CHURN_PROBE"
        "EnableM03TypedExitBootServicesCallProbe" =
            "M03_TYPED_EXIT_BOOT_SERVICES_CALL_PROBE"
        "EnableM03FirstRealExitBootServicesProbe" =
            "M03_FIRST_REAL_EXIT_BOOT_SERVICES_PROBE"
        "EnableM03FirmwareAdapterRevocationProbe" =
            "M03_FIRMWARE_ADAPTER_REVOCATION_PROBE"
        "EnableM03OwnedSubsystemSurvivalProbe" =
            "M03_OWNED_SUBSYSTEM_SURVIVAL_PROBE"
        "EnableM03OwnedSubsystemPanicProbe" =
            "M03_OWNED_SUBSYSTEM_PANIC_PROBE"
        "EnableM04ClassConstructorExecutionProbe" =
            "M04_CLASS_CONSTRUCTOR_EXECUTION_PROBE"
        "EnableM04InterfaceDispatchProbe" =
            "M04_INTERFACE_DISPATCH_PROBE"
        "EnableM04CheckedReferenceBarrierProbe" =
            "M04_CHECKED_REFERENCE_BARRIER_PROBE"
    }
    foreach ($entry in $switchSymbols.GetEnumerator()) {
        $defaultNodes = @($project.SelectNodes(
            "/Project/PropertyGroup/" + $entry.Key) |
                Where-Object { $_.HasAttribute("Condition") })
        if ($defaultNodes.Count -ne 1) {
            return New-M01SourceClosureResult `
                -Succeeded $false `
                -Reason "switch_default_invalid" `
                -Detail ($entry.Key + ":count")
        }

        $expectedDefaultCondition =
            "'" + '$' + '(' + $entry.Key + ")' == ''"
        $actualDefaultCondition =
            $defaultNodes[0].GetAttribute("Condition")
        if (($defaultNodes[0].InnerText -ne "false") -or
            ($actualDefaultCondition -ne $expectedDefaultCondition)) {
            return New-M01SourceClosureResult `
                -Succeeded $false `
                -Reason "switch_default_invalid" `
                -Detail $entry.Key
        }

    }

    $validationTargets = @($project.SelectNodes(
        "/Project/Target[@Name='ValidateM01ProbeSwitches']"))
    if ($validationTargets.Count -ne 1 -or
        $validationTargets[0].GetAttribute("BeforeTargets") -ne "BeforeCompile" -or
        $validationTargets[0].HasAttribute("Condition") -or
        $validationTargets[0].Attributes.Count -ne 2) {
        return New-M01SourceClosureResult `
            -Succeeded $false `
            -Reason "switch_dependency_validation_missing" `
            -Detail "ValidateM01ProbeSwitches"
    }
    $errorNodes = @($validationTargets[0].SelectNodes("Error"))
    $errorConditions = @($errorNodes |
        ForEach-Object { $_.GetAttribute("Condition") })
    $allocatorDependencyCondition =
        "'" + '$' + "(EnableM01LiveAllocatorProbe)' == 'true' and '" +
        '$' + "(EnableM01LoadedImageStackProbe)' != 'true'"
    $pagingDependencyCondition =
        "'" + '$' + "(EnableM01OwnedPagingProbe)' == 'true' and '" +
        '$' + "(EnableM01LiveAllocatorProbe)' != 'true'"
    $m02LoadedImageDependencyCondition =
        "'" + '$' + "(EnableM02SafeStubProbe)' == 'true' and '" +
        '$' + "(EnableM01LoadedImageStackProbe)' != 'true'"
    $m02AllocatorDependencyCondition =
        "'" + '$' + "(EnableM02SafeStubProbe)' == 'true' and '" +
        '$' + "(EnableM01LiveAllocatorProbe)' != 'true'"
    $m02PagingDependencyCondition =
        "'" + '$' + "(EnableM02SafeStubProbe)' == 'true' and '" +
        '$' + "(EnableM01OwnedPagingProbe)' != 'true'"
    $m02CheckedBindingDependencyCondition =
        "'" + '$' + "(EnableM02CheckedPortIoBindingProbe)' == 'true' and '" +
        '$' + "(EnableM02SafeStubProbe)' != 'true'"
    $m02Com1OwnedPortIoDependencyCondition =
        "'" + '$' + "(EnableM02Com1OwnedPortIoProbe)' == 'true' and '" +
        '$' + "(EnableM02CheckedPortIoBindingProbe)' != 'true'"
    $m02OwnedCom1ReadyDependencyCondition =
        "'" + '$' + "(EnableM02OwnedCom1ReadyProbe)' == 'true' and '" +
        '$' + "(EnableM02Com1OwnedPortIoProbe)' != 'true'"
    $m02PanicSafeOwnedCom1DependencyCondition =
        "'" + '$' + "(EnableM02PanicSafeOwnedCom1Probe)' == 'true' and '" +
        '$' + "(EnableM02OwnedCom1ReadyProbe)' != 'true'"
    $m02OwnedX64GdtEncodingDependencyCondition =
        "'" + '$' + "(EnableM02OwnedX64GdtEncodingProbe)' == 'true' and '" +
        '$' + "(EnableM02PanicSafeOwnedCom1Probe)' != 'true'"
    $m02GuardedIstStackAllocationDependencyCondition =
        "'" + '$' + "(EnableM02GuardedIstStackAllocationProbe)' == 'true' and '" +
        '$' + "(EnableM02OwnedX64GdtEncodingProbe)' != 'true'"
    $m02OwnedGdtTssTerminalDependencyCondition =
        "'" + '$' + "(EnableM02OwnedGdtTssTerminalProbe)' == 'true' and '" +
        '$' + "(EnableM02GuardedIstStackAllocationProbe)' != 'true'"
    $m02NormalizedInterruptFramePageTerminalExclusionCondition =
        "'" + '$' + "(EnableM02NormalizedInterruptFramePageProbe)' == 'true' and '" +
        '$' + "(EnableM02OwnedGdtTssTerminalProbe)' == 'true'"
    $m02OwnedIdtGatePopulationTerminalExclusionCondition =
        "'" + '$' + "(EnableM02OwnedIdtGatePopulationProbe)' == 'true' and '" +
        '$' + "(EnableM02OwnedGdtTssTerminalProbe)' == 'true'"
    $m02OwnedIdtGatePopulationNormalizedExclusionCondition =
        "'" + '$' + "(EnableM02OwnedIdtGatePopulationProbe)' == 'true' and '" +
        '$' + "(EnableM02NormalizedInterruptFramePageProbe)' == 'true'"
    $m02IdtrContinuedExecutionTerminalExclusionCondition =
        "'" + '$' + "(EnableM02IdtrContinuedExecutionProbe)' == 'true' and '" +
        '$' + "(EnableM02OwnedGdtTssTerminalProbe)' == 'true'"
    $m02IdtrContinuedExecutionNormalizedExclusionCondition =
        "'" + '$' + "(EnableM02IdtrContinuedExecutionProbe)' == 'true' and '" +
        '$' + "(EnableM02NormalizedInterruptFramePageProbe)' == 'true'"
    $m02IdtrContinuedExecutionIdtExclusionCondition =
        "'" + '$' + "(EnableM02IdtrContinuedExecutionProbe)' == 'true' and '" +
        '$' + "(EnableM02OwnedIdtGatePopulationProbe)' == 'true'"
    $m02Ud2VectorStateDependencyCondition =
        "'" + '$' + "(EnableM02Ud2VectorStateProbe)' == 'true' and '" +
        '$' + "(EnableM02PanicSafeOwnedCom1Probe)' != 'true'"
    $m02Ud2VectorStateOwnedX64GdtExclusionCondition =
        "'" + '$' + "(EnableM02Ud2VectorStateProbe)' == 'true' and '" +
        '$' + "(EnableM02OwnedX64GdtEncodingProbe)' == 'true'"
    $m02Ud2VectorStateGuardedIstExclusionCondition =
        "'" + '$' + "(EnableM02Ud2VectorStateProbe)' == 'true' and '" +
        '$' + "(EnableM02GuardedIstStackAllocationProbe)' == 'true'"
    $m02Ud2VectorStateTerminalExclusionCondition =
        "'" + '$' + "(EnableM02Ud2VectorStateProbe)' == 'true' and '" +
        '$' + "(EnableM02OwnedGdtTssTerminalProbe)' == 'true'"
    $m02Ud2VectorStateNormalizedExclusionCondition =
        "'" + '$' + "(EnableM02Ud2VectorStateProbe)' == 'true' and '" +
        '$' + "(EnableM02NormalizedInterruptFramePageProbe)' == 'true'"
    $m02Ud2VectorStateIdtExclusionCondition =
        "'" + '$' + "(EnableM02Ud2VectorStateProbe)' == 'true' and '" +
        '$' + "(EnableM02OwnedIdtGatePopulationProbe)' == 'true'"
    $m02Ud2VectorStateIdtrExclusionCondition =
        "'" + '$' + "(EnableM02Ud2VectorStateProbe)' == 'true' and '" +
        '$' + "(EnableM02IdtrContinuedExecutionProbe)' == 'true'"
    $m04SpanMemoryDependencyCondition =
        "'" + '$' + "(EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe)' == 'true' and '" +
        '$' + "(EnableM04IndependentEndianConformanceProbe)' != 'true'"
    $m04SpanMemoryTerminalExclusionCondition =
        "'" + '$' + "(EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe)' == 'true' and '" +
        '$' + "(EnableM02OwnedGdtTssTerminalProbe)' == 'true'"
    $m04SpanMemoryUd2ExclusionCondition =
        "'" + '$' + "(EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe)' == 'true' and '" +
        '$' + "(EnableM02Ud2VectorStateProbe)' == 'true'"
    $m04ArithmeticDependencyCondition =
        "'" + '$' + "(EnableM04ArithmeticConformanceProbe)' == 'true' and '" +
        '$' + "(EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe)' != 'true'"
    $m04ArithmeticTerminalExclusionCondition =
        "'" + '$' + "(EnableM04ArithmeticConformanceProbe)' == 'true' and '" +
        '$' + "(EnableM02OwnedGdtTssTerminalProbe)' == 'true'"
    $m04ArithmeticUd2ExclusionCondition =
        "'" + '$' + "(EnableM04ArithmeticConformanceProbe)' == 'true' and '" +
        '$' + "(EnableM02Ud2VectorStateProbe)' == 'true'"
    $m04LayoutArithmeticDependencyCondition =
        "'" + '$' + "(EnableM04LayoutContractProbe)' == 'true' and '" +
        '$' + "(EnableM04ArithmeticConformanceProbe)' != 'true'"
    $m04LayoutSpanDependencyCondition =
        "'" + '$' + "(EnableM04LayoutContractProbe)' == 'true' and '" +
        '$' + "(EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe)' != 'true'"
    $m04LayoutEndianDependencyCondition =
        "'" + '$' + "(EnableM04LayoutContractProbe)' == 'true' and '" +
        '$' + "(EnableM04IndependentEndianConformanceProbe)' != 'true'"
    $m04LayoutTerminalExclusionCondition =
        "'" + '$' + "(EnableM04LayoutContractProbe)' == 'true' and '" +
        '$' + "(EnableM02OwnedGdtTssTerminalProbe)' == 'true'"
    $m04LayoutUd2ExclusionCondition =
        "'" + '$' + "(EnableM04LayoutContractProbe)' == 'true' and '" +
        '$' + "(EnableM02Ud2VectorStateProbe)' == 'true'"
    $requiredErrorConditions = @(
        "'`$(EnableM04GcStaticRootMaterializationProbe)' != '' and '`$(EnableM04GcStaticRootMaterializationProbe)' != 'true' and '`$(EnableM04GcStaticRootMaterializationProbe)' != 'false'",
        "'`$(EnableM04GcStaticRootMaterializationProbe)' == 'true' and '`$(EnableM01LoadedImageStackProbe)' != 'true'",
        "'`$(EnableM04GcStaticRootMaterializationProbe)' == 'true' and '`$(EnableM01LiveAllocatorProbe)' != 'true'",
        "'`$(EnableM04GcStaticRootMaterializationProbe)' == 'true' and '`$(EnableM04IndependentEndianConformanceProbe)' != 'true'",
        "'`$(EnableM04GcStaticRootMaterializationProbe)' == 'true' and '`$(EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe)' != 'true'",
        "'`$(EnableM04GcStaticRootMaterializationProbe)' == 'true' and '`$(EnableM04ArithmeticConformanceProbe)' != 'true'",
        "'`$(EnableM04GcStaticRootMaterializationProbe)' == 'true' and '`$(EnableM04LayoutContractProbe)' != 'true'",
        "'`$(EnableM04GcStaticRootMaterializationProbe)' == 'true' and '`$(EnableM04ManagedHeapPageRangeProbe)' != 'true'",
        "'`$(EnableM04GcStaticRootMaterializationProbe)' == 'true' and '`$(EnableM04RhpNewFastProbe)' != 'true'",
        "'`$(EnableM04GcStaticRootMaterializationProbe)' == 'true' and '`$(EnableM01OwnedPagingProbe)' == 'true'",
        "'`$(EnableM04GcStaticRootMaterializationProbe)' == 'true' and '`$(EnableM02SafeStubProbe)' == 'true'",
        "'`$(EnableM04GcStaticRootMaterializationProbe)' == 'true' and '`$(EnableM02CheckedPortIoBindingProbe)' == 'true'",
        "'`$(EnableM04GcStaticRootMaterializationProbe)' == 'true' and '`$(EnableM02Com1OwnedPortIoProbe)' == 'true'",
        "'`$(EnableM04GcStaticRootMaterializationProbe)' == 'true' and '`$(EnableM02OwnedCom1ReadyProbe)' == 'true'",
        "'`$(EnableM04GcStaticRootMaterializationProbe)' == 'true' and '`$(EnableM02PanicSafeOwnedCom1Probe)' == 'true'",
        "'`$(EnableM04GcStaticRootMaterializationProbe)' == 'true' and '`$(EnableM02OwnedX64GdtEncodingProbe)' == 'true'",
        "'`$(EnableM04GcStaticRootMaterializationProbe)' == 'true' and '`$(EnableM02GuardedIstStackAllocationProbe)' == 'true'",
        "'`$(EnableM04GcStaticRootMaterializationProbe)' == 'true' and '`$(EnableM02OwnedGdtTssTerminalProbe)' == 'true'",
        "'`$(EnableM04GcStaticRootMaterializationProbe)' == 'true' and '`$(EnableM02NormalizedInterruptFramePageProbe)' == 'true'",
        "'`$(EnableM04GcStaticRootMaterializationProbe)' == 'true' and '`$(EnableM02OwnedIdtGatePopulationProbe)' == 'true'",
        "'`$(EnableM04GcStaticRootMaterializationProbe)' == 'true' and '`$(EnableM02IdtrContinuedExecutionProbe)' == 'true'",
        "'`$(EnableM04GcStaticRootMaterializationProbe)' == 'true' and '`$(EnableM02Ud2VectorStateProbe)' == 'true'",
        "'`$(EnableM04GcStaticRootMaterializationProbe)' == 'true' and '`$(EnableM02PageFaultCr2ErrorCodeProbe)' == 'true'",
        "'`$(EnableM04GcStaticRootMaterializationProbe)' == 'true' and '`$(EnableM02DoubleFaultIstProbe)' == 'true'",
        "'`$(EnableM04GcStaticRootMaterializationProbe)' == 'true' and '`$(EnableM02UnexpectedVectorPanicProbe)' == 'true'",
        "'`$(EnableM04GcStaticRootMaterializationProbe)' == 'true' and '`$(EnableM04RhpNewFastBeforeBindFailureProbe)' == 'true'",
        "'`$(EnableM04GcStaticRootMaterializationProbe)' == 'true' and '`$(EnableM04RhpNewFastExhaustionFailureProbe)' == 'true'",
        "'`$(EnableM04GcStaticRootMaterializationProbe)' == 'true' and '`$(EnableM04SzArrayAllocationProbe)' == 'true'",
        "'`$(EnableM04GcStaticRootMaterializationProbe)' == 'true' and '`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true'",
        "'`$(EnableM04GcStaticRootMaterializationProbe)' == 'true' and '`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM04GcStaticRootMaterializationProbe)' == 'true' and '`$(EnableM04StringAllocationProbe)' == 'true'",
        "'`$(EnableM04GcStaticRootMaterializationProbe)' == 'true' and '`$(EnableM04StringAllocationBeforeBindProbe)' == 'true'",
        "'`$(EnableM04GcStaticRootMaterializationProbe)' == 'true' and '`$(EnableM04StringAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM04GcStaticRootMaterializationProbe)' == 'true' and '`$(EnableM04StringAllocationInvalidLengthProbe)' == 'true'",
        "'`$(EnableM04GcStaticRootMaterializationProbe)' == 'true' and '`$(EnableM03MemoryMapCaptureProbe)' == 'true'",
        "'`$(EnableM04GcStaticRootMaterializationProbe)' == 'true' and '`$(EnableM04CheckedReferenceBarrierProbe)' == 'true'",
        "'`$(EnableM04GcStaticRootMaterializationProbe)' == 'true' and '`$(EnableM03FirmwareEntropySeedProbe)' == 'true'",
        "'`$(EnableM04GcStaticRootMaterializationProbe)' == 'true' and '`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true'",
        "'`$(EnableM04GcStaticRootMaterializationProbe)' == 'true' and '`$(EnableM03TypedExitBootServicesCallProbe)' == 'true'",
        "'`$(EnableM04GcStaticRootMaterializationProbe)' == 'true' and '`$(EnableUefiGoneSimulationProbe)' == 'true'",
        "'`$(EnableM04GcStaticRootMaterializationProbe)' == 'true' and '`$(EnableExitBootServicesFreshMapProbe)' == 'true'",
        "'`$(EnableM04GcStaticRootMaterializationProbe)' == 'true' and '`$(EnableAcpiPhysicalDiscoveryProbe)' == 'true'",
        "'`$(EnableM04CheckedReferenceBarrierProbe)' == 'true' and '`$(EnableM01LoadedImageStackProbe)' != 'true'",
        "'`$(EnableM04CheckedReferenceBarrierProbe)' == 'true' and '`$(EnableM01LiveAllocatorProbe)' != 'true'",
        "'`$(EnableM04CheckedReferenceBarrierProbe)' == 'true' and '`$(EnableM04IndependentEndianConformanceProbe)' != 'true'",
        "'`$(EnableM04CheckedReferenceBarrierProbe)' == 'true' and '`$(EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe)' != 'true'",
        "'`$(EnableM04CheckedReferenceBarrierProbe)' == 'true' and '`$(EnableM04ArithmeticConformanceProbe)' != 'true'",
        "'`$(EnableM04CheckedReferenceBarrierProbe)' == 'true' and '`$(EnableM04LayoutContractProbe)' != 'true'",
        "'`$(EnableM04CheckedReferenceBarrierProbe)' == 'true' and '`$(EnableM04ManagedHeapPageRangeProbe)' != 'true'",
        "'`$(EnableM04CheckedReferenceBarrierProbe)' == 'true' and '`$(EnableM04RhpNewFastProbe)' != 'true'",
        "'`$(EnableM04CheckedReferenceBarrierProbe)' == 'true' and '`$(EnableM01OwnedPagingProbe)' == 'true'",
        "'`$(EnableM01OwnedPagingProbe)' == 'true' and '`$(EnableM04CheckedReferenceBarrierProbe)' == 'true'",
        "'`$(EnableM04CheckedReferenceBarrierProbe)' == 'true' and '`$(EnableM02SafeStubProbe)' == 'true'",
        "'`$(EnableM02SafeStubProbe)' == 'true' and '`$(EnableM04CheckedReferenceBarrierProbe)' == 'true'",
        "'`$(EnableM04CheckedReferenceBarrierProbe)' == 'true' and '`$(EnableM02CheckedPortIoBindingProbe)' == 'true'",
        "'`$(EnableM02CheckedPortIoBindingProbe)' == 'true' and '`$(EnableM04CheckedReferenceBarrierProbe)' == 'true'",
        "'`$(EnableM04CheckedReferenceBarrierProbe)' == 'true' and '`$(EnableM02Com1OwnedPortIoProbe)' == 'true'",
        "'`$(EnableM02Com1OwnedPortIoProbe)' == 'true' and '`$(EnableM04CheckedReferenceBarrierProbe)' == 'true'",
        "'`$(EnableM04CheckedReferenceBarrierProbe)' == 'true' and '`$(EnableM02OwnedCom1ReadyProbe)' == 'true'",
        "'`$(EnableM02OwnedCom1ReadyProbe)' == 'true' and '`$(EnableM04CheckedReferenceBarrierProbe)' == 'true'",
        "'`$(EnableM04CheckedReferenceBarrierProbe)' == 'true' and '`$(EnableM02PanicSafeOwnedCom1Probe)' == 'true'",
        "'`$(EnableM02PanicSafeOwnedCom1Probe)' == 'true' and '`$(EnableM04CheckedReferenceBarrierProbe)' == 'true'",
        "'`$(EnableM04CheckedReferenceBarrierProbe)' == 'true' and '`$(EnableM02OwnedX64GdtEncodingProbe)' == 'true'",
        "'`$(EnableM02OwnedX64GdtEncodingProbe)' == 'true' and '`$(EnableM04CheckedReferenceBarrierProbe)' == 'true'",
        "'`$(EnableM04CheckedReferenceBarrierProbe)' == 'true' and '`$(EnableM02GuardedIstStackAllocationProbe)' == 'true'",
        "'`$(EnableM02GuardedIstStackAllocationProbe)' == 'true' and '`$(EnableM04CheckedReferenceBarrierProbe)' == 'true'",
        "'`$(EnableM04CheckedReferenceBarrierProbe)' == 'true' and '`$(EnableM02OwnedGdtTssTerminalProbe)' == 'true'",
        "'`$(EnableM02OwnedGdtTssTerminalProbe)' == 'true' and '`$(EnableM04CheckedReferenceBarrierProbe)' == 'true'",
        "'`$(EnableM04CheckedReferenceBarrierProbe)' == 'true' and '`$(EnableM02NormalizedInterruptFramePageProbe)' == 'true'",
        "'`$(EnableM02NormalizedInterruptFramePageProbe)' == 'true' and '`$(EnableM04CheckedReferenceBarrierProbe)' == 'true'",
        "'`$(EnableM04CheckedReferenceBarrierProbe)' == 'true' and '`$(EnableM02OwnedIdtGatePopulationProbe)' == 'true'",
        "'`$(EnableM02OwnedIdtGatePopulationProbe)' == 'true' and '`$(EnableM04CheckedReferenceBarrierProbe)' == 'true'",
        "'`$(EnableM04CheckedReferenceBarrierProbe)' == 'true' and '`$(EnableM02IdtrContinuedExecutionProbe)' == 'true'",
        "'`$(EnableM02IdtrContinuedExecutionProbe)' == 'true' and '`$(EnableM04CheckedReferenceBarrierProbe)' == 'true'",
        "'`$(EnableM04CheckedReferenceBarrierProbe)' == 'true' and '`$(EnableM02Ud2VectorStateProbe)' == 'true'",
        "'`$(EnableM02Ud2VectorStateProbe)' == 'true' and '`$(EnableM04CheckedReferenceBarrierProbe)' == 'true'",
        "'`$(EnableM04CheckedReferenceBarrierProbe)' == 'true' and '`$(EnableM02PageFaultCr2ErrorCodeProbe)' == 'true'",
        "'`$(EnableM02PageFaultCr2ErrorCodeProbe)' == 'true' and '`$(EnableM04CheckedReferenceBarrierProbe)' == 'true'",
        "'`$(EnableM04CheckedReferenceBarrierProbe)' == 'true' and '`$(EnableM02DoubleFaultIstProbe)' == 'true'",
        "'`$(EnableM02DoubleFaultIstProbe)' == 'true' and '`$(EnableM04CheckedReferenceBarrierProbe)' == 'true'",
        "'`$(EnableM04CheckedReferenceBarrierProbe)' == 'true' and '`$(EnableM02UnexpectedVectorPanicProbe)' == 'true'",
        "'`$(EnableM02UnexpectedVectorPanicProbe)' == 'true' and '`$(EnableM04CheckedReferenceBarrierProbe)' == 'true'",
        "'`$(EnableM04CheckedReferenceBarrierProbe)' == 'true' and '`$(EnableM04RhpNewFastBeforeBindFailureProbe)' == 'true'",
        "'`$(EnableM04RhpNewFastBeforeBindFailureProbe)' == 'true' and '`$(EnableM04CheckedReferenceBarrierProbe)' == 'true'",
        "'`$(EnableM04CheckedReferenceBarrierProbe)' == 'true' and '`$(EnableM04RhpNewFastExhaustionFailureProbe)' == 'true'",
        "'`$(EnableM04RhpNewFastExhaustionFailureProbe)' == 'true' and '`$(EnableM04CheckedReferenceBarrierProbe)' == 'true'",
        "'`$(EnableM04CheckedReferenceBarrierProbe)' == 'true' and '`$(EnableM04SzArrayAllocationProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationProbe)' == 'true' and '`$(EnableM04CheckedReferenceBarrierProbe)' == 'true'",
        "'`$(EnableM04CheckedReferenceBarrierProbe)' == 'true' and '`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true' and '`$(EnableM04CheckedReferenceBarrierProbe)' == 'true'",
        "'`$(EnableM04CheckedReferenceBarrierProbe)' == 'true' and '`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true' and '`$(EnableM04CheckedReferenceBarrierProbe)' == 'true'",
        "'`$(EnableM04CheckedReferenceBarrierProbe)' == 'true' and '`$(EnableM04StringAllocationProbe)' == 'true'",
        "'`$(EnableM04StringAllocationProbe)' == 'true' and '`$(EnableM04CheckedReferenceBarrierProbe)' == 'true'",
        "'`$(EnableM04CheckedReferenceBarrierProbe)' == 'true' and '`$(EnableM04StringAllocationBeforeBindProbe)' == 'true'",
        "'`$(EnableM04StringAllocationBeforeBindProbe)' == 'true' and '`$(EnableM04CheckedReferenceBarrierProbe)' == 'true'",
        "'`$(EnableM04CheckedReferenceBarrierProbe)' == 'true' and '`$(EnableM04StringAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM04StringAllocationExhaustionProbe)' == 'true' and '`$(EnableM04CheckedReferenceBarrierProbe)' == 'true'",
        "'`$(EnableM04CheckedReferenceBarrierProbe)' == 'true' and '`$(EnableM04StringAllocationInvalidLengthProbe)' == 'true'",
        "'`$(EnableM04StringAllocationInvalidLengthProbe)' == 'true' and '`$(EnableM04CheckedReferenceBarrierProbe)' == 'true'",
        "'`$(EnableM04CheckedReferenceBarrierProbe)' == 'true' and '`$(EnableM03MemoryMapCaptureProbe)' == 'true'",
        "'`$(EnableM03MemoryMapCaptureProbe)' == 'true' and '`$(EnableM04CheckedReferenceBarrierProbe)' == 'true'",
        "'`$(EnableM04CheckedReferenceBarrierProbe)' == 'true' and '`$(EnableUefiGoneSimulationProbe)' == 'true'",
        "'`$(EnableUefiGoneSimulationProbe)' == 'true' and '`$(EnableM04CheckedReferenceBarrierProbe)' == 'true'",
        "'`$(EnableM04CheckedReferenceBarrierProbe)' == 'true' and '`$(EnableExitBootServicesFreshMapProbe)' == 'true'",
        "'`$(EnableExitBootServicesFreshMapProbe)' == 'true' and '`$(EnableM04CheckedReferenceBarrierProbe)' == 'true'",
        "'`$(EnableM04CheckedReferenceBarrierProbe)' == 'true' and '`$(EnableAcpiPhysicalDiscoveryProbe)' == 'true'",
        "'`$(EnableAcpiPhysicalDiscoveryProbe)' == 'true' and '`$(EnableM04CheckedReferenceBarrierProbe)' == 'true'",
        "'`$(EnableM02UnexpectedVectorPanicProbe)' == 'true' and '`$(EnableM01LoadedImageStackProbe)' != 'true'",
        "'`$(EnableM02UnexpectedVectorPanicProbe)' == 'true' and '`$(EnableM01LiveAllocatorProbe)' != 'true'",
        "'`$(EnableM02UnexpectedVectorPanicProbe)' == 'true' and '`$(EnableM01OwnedPagingProbe)' != 'true'",
        "'`$(EnableM02UnexpectedVectorPanicProbe)' == 'true' and '`$(EnableM02SafeStubProbe)' != 'true'",
        "'`$(EnableM02UnexpectedVectorPanicProbe)' == 'true' and '`$(EnableM02CheckedPortIoBindingProbe)' != 'true'",
        "'`$(EnableM02UnexpectedVectorPanicProbe)' == 'true' and '`$(EnableM02Com1OwnedPortIoProbe)' != 'true'",
        "'`$(EnableM02UnexpectedVectorPanicProbe)' == 'true' and '`$(EnableM02OwnedCom1ReadyProbe)' != 'true'",
        "'`$(EnableM02UnexpectedVectorPanicProbe)' == 'true' and '`$(EnableM02PanicSafeOwnedCom1Probe)' != 'true'",
        "'`$(EnableM02UnexpectedVectorPanicProbe)' == 'true' and '`$(EnableM02OwnedX64GdtEncodingProbe)' == 'true'",
        "'`$(EnableM02OwnedX64GdtEncodingProbe)' == 'true' and '`$(EnableM02UnexpectedVectorPanicProbe)' == 'true'",
        "'`$(EnableM02UnexpectedVectorPanicProbe)' == 'true' and '`$(EnableM02GuardedIstStackAllocationProbe)' == 'true'",
        "'`$(EnableM02GuardedIstStackAllocationProbe)' == 'true' and '`$(EnableM02UnexpectedVectorPanicProbe)' == 'true'",
        "'`$(EnableM02UnexpectedVectorPanicProbe)' == 'true' and '`$(EnableM02OwnedGdtTssTerminalProbe)' == 'true'",
        "'`$(EnableM02OwnedGdtTssTerminalProbe)' == 'true' and '`$(EnableM02UnexpectedVectorPanicProbe)' == 'true'",
        "'`$(EnableM02UnexpectedVectorPanicProbe)' == 'true' and '`$(EnableM02NormalizedInterruptFramePageProbe)' == 'true'",
        "'`$(EnableM02NormalizedInterruptFramePageProbe)' == 'true' and '`$(EnableM02UnexpectedVectorPanicProbe)' == 'true'",
        "'`$(EnableM02UnexpectedVectorPanicProbe)' == 'true' and '`$(EnableM02OwnedIdtGatePopulationProbe)' == 'true'",
        "'`$(EnableM02OwnedIdtGatePopulationProbe)' == 'true' and '`$(EnableM02UnexpectedVectorPanicProbe)' == 'true'",
        "'`$(EnableM02UnexpectedVectorPanicProbe)' == 'true' and '`$(EnableM02IdtrContinuedExecutionProbe)' == 'true'",
        "'`$(EnableM02IdtrContinuedExecutionProbe)' == 'true' and '`$(EnableM02UnexpectedVectorPanicProbe)' == 'true'",
        "'`$(EnableM02UnexpectedVectorPanicProbe)' == 'true' and '`$(EnableM02Ud2VectorStateProbe)' == 'true'",
        "'`$(EnableM02Ud2VectorStateProbe)' == 'true' and '`$(EnableM02UnexpectedVectorPanicProbe)' == 'true'",
        "'`$(EnableM02UnexpectedVectorPanicProbe)' == 'true' and '`$(EnableM04IndependentEndianConformanceProbe)' == 'true'",
        "'`$(EnableM04IndependentEndianConformanceProbe)' == 'true' and '`$(EnableM02UnexpectedVectorPanicProbe)' == 'true'",
        "'`$(EnableM02UnexpectedVectorPanicProbe)' == 'true' and '`$(EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe)' == 'true'",
        "'`$(EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe)' == 'true' and '`$(EnableM02UnexpectedVectorPanicProbe)' == 'true'",
        "'`$(EnableM02UnexpectedVectorPanicProbe)' == 'true' and '`$(EnableM04ArithmeticConformanceProbe)' == 'true'",
        "'`$(EnableM04ArithmeticConformanceProbe)' == 'true' and '`$(EnableM02UnexpectedVectorPanicProbe)' == 'true'",
        "'`$(EnableM02UnexpectedVectorPanicProbe)' == 'true' and '`$(EnableM04LayoutContractProbe)' == 'true'",
        "'`$(EnableM04LayoutContractProbe)' == 'true' and '`$(EnableM02UnexpectedVectorPanicProbe)' == 'true'",
        "'`$(EnableM02UnexpectedVectorPanicProbe)' == 'true' and '`$(EnableM04ManagedHeapPageRangeProbe)' == 'true'",
        "'`$(EnableM04ManagedHeapPageRangeProbe)' == 'true' and '`$(EnableM02UnexpectedVectorPanicProbe)' == 'true'",
        "'`$(EnableM02UnexpectedVectorPanicProbe)' == 'true' and '`$(EnableM02PageFaultCr2ErrorCodeProbe)' == 'true'",
        "'`$(EnableM02PageFaultCr2ErrorCodeProbe)' == 'true' and '`$(EnableM02UnexpectedVectorPanicProbe)' == 'true'",
        "'`$(EnableM02UnexpectedVectorPanicProbe)' == 'true' and '`$(EnableM02DoubleFaultIstProbe)' == 'true'",
        "'`$(EnableM02DoubleFaultIstProbe)' == 'true' and '`$(EnableM02UnexpectedVectorPanicProbe)' == 'true'",
        "'`$(EnableM02UnexpectedVectorPanicProbe)' == 'true' and '`$(EnableM04RhpNewFastProbe)' == 'true'",
        "'`$(EnableM04RhpNewFastProbe)' == 'true' and '`$(EnableM02UnexpectedVectorPanicProbe)' == 'true'",
        "'`$(EnableM02UnexpectedVectorPanicProbe)' == 'true' and '`$(EnableM04RhpNewFastBeforeBindFailureProbe)' == 'true'",
        "'`$(EnableM04RhpNewFastBeforeBindFailureProbe)' == 'true' and '`$(EnableM02UnexpectedVectorPanicProbe)' == 'true'",
        "'`$(EnableM02UnexpectedVectorPanicProbe)' == 'true' and '`$(EnableM04RhpNewFastExhaustionFailureProbe)' == 'true'",
        "'`$(EnableM04RhpNewFastExhaustionFailureProbe)' == 'true' and '`$(EnableM02UnexpectedVectorPanicProbe)' == 'true'",
        "'`$(EnableM02UnexpectedVectorPanicProbe)' == 'true' and '`$(EnableM04SzArrayAllocationProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationProbe)' == 'true' and '`$(EnableM02UnexpectedVectorPanicProbe)' == 'true'",
        "'`$(EnableM02UnexpectedVectorPanicProbe)' == 'true' and '`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true' and '`$(EnableM02UnexpectedVectorPanicProbe)' == 'true'",
        "'`$(EnableM02UnexpectedVectorPanicProbe)' == 'true' and '`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true' and '`$(EnableM02UnexpectedVectorPanicProbe)' == 'true'",
        "'`$(EnableM02UnexpectedVectorPanicProbe)' == 'true' and '`$(EnableUefiGoneSimulationProbe)' == 'true'",
        "'`$(EnableUefiGoneSimulationProbe)' == 'true' and '`$(EnableM02UnexpectedVectorPanicProbe)' == 'true'",
        "'`$(EnableM02UnexpectedVectorPanicProbe)' == 'true' and '`$(EnableExitBootServicesFreshMapProbe)' == 'true'",
        "'`$(EnableExitBootServicesFreshMapProbe)' == 'true' and '`$(EnableM02UnexpectedVectorPanicProbe)' == 'true'",
        "'`$(EnableM02UnexpectedVectorPanicProbe)' == 'true' and '`$(EnableAcpiPhysicalDiscoveryProbe)' == 'true'",
        "'`$(EnableAcpiPhysicalDiscoveryProbe)' == 'true' and '`$(EnableM02UnexpectedVectorPanicProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationProbe)' == 'true' and '`$(EnableM04RhpNewFastProbe)' != 'true'",
        "'`$(EnableM04SzArrayAllocationProbe)' == 'true' and '`$(EnableM01LoadedImageStackProbe)' != 'true'",
        "'`$(EnableM04SzArrayAllocationProbe)' == 'true' and '`$(EnableM01LiveAllocatorProbe)' != 'true'",
        "'`$(EnableM04SzArrayAllocationProbe)' == 'true' and '`$(EnableM04IndependentEndianConformanceProbe)' != 'true'",
        "'`$(EnableM04SzArrayAllocationProbe)' == 'true' and '`$(EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe)' != 'true'",
        "'`$(EnableM04SzArrayAllocationProbe)' == 'true' and '`$(EnableM04ArithmeticConformanceProbe)' != 'true'",
        "'`$(EnableM04SzArrayAllocationProbe)' == 'true' and '`$(EnableM04LayoutContractProbe)' != 'true'",
        "'`$(EnableM04SzArrayAllocationProbe)' == 'true' and '`$(EnableM04ManagedHeapPageRangeProbe)' != 'true'",
        "'`$(EnableM04SzArrayAllocationProbe)' == 'true' and '`$(EnableM02SafeStubProbe)' == 'true'",
        "'`$(EnableM02SafeStubProbe)' == 'true' and '`$(EnableM04SzArrayAllocationProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true' and '`$(EnableM02SafeStubProbe)' == 'true'",
        "'`$(EnableM02SafeStubProbe)' == 'true' and '`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true' and '`$(EnableM02SafeStubProbe)' == 'true'",
        "'`$(EnableM02SafeStubProbe)' == 'true' and '`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationProbe)' == 'true' and '`$(EnableM02CheckedPortIoBindingProbe)' == 'true'",
        "'`$(EnableM02CheckedPortIoBindingProbe)' == 'true' and '`$(EnableM04SzArrayAllocationProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true' and '`$(EnableM02CheckedPortIoBindingProbe)' == 'true'",
        "'`$(EnableM02CheckedPortIoBindingProbe)' == 'true' and '`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true' and '`$(EnableM02CheckedPortIoBindingProbe)' == 'true'",
        "'`$(EnableM02CheckedPortIoBindingProbe)' == 'true' and '`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationProbe)' == 'true' and '`$(EnableM02Com1OwnedPortIoProbe)' == 'true'",
        "'`$(EnableM02Com1OwnedPortIoProbe)' == 'true' and '`$(EnableM04SzArrayAllocationProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true' and '`$(EnableM02Com1OwnedPortIoProbe)' == 'true'",
        "'`$(EnableM02Com1OwnedPortIoProbe)' == 'true' and '`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true' and '`$(EnableM02Com1OwnedPortIoProbe)' == 'true'",
        "'`$(EnableM02Com1OwnedPortIoProbe)' == 'true' and '`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationProbe)' == 'true' and '`$(EnableM02OwnedCom1ReadyProbe)' == 'true'",
        "'`$(EnableM02OwnedCom1ReadyProbe)' == 'true' and '`$(EnableM04SzArrayAllocationProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true' and '`$(EnableM02OwnedCom1ReadyProbe)' == 'true'",
        "'`$(EnableM02OwnedCom1ReadyProbe)' == 'true' and '`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true' and '`$(EnableM02OwnedCom1ReadyProbe)' == 'true'",
        "'`$(EnableM02OwnedCom1ReadyProbe)' == 'true' and '`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationProbe)' == 'true' and '`$(EnableM02PanicSafeOwnedCom1Probe)' == 'true'",
        "'`$(EnableM02PanicSafeOwnedCom1Probe)' == 'true' and '`$(EnableM04SzArrayAllocationProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true' and '`$(EnableM02PanicSafeOwnedCom1Probe)' == 'true'",
        "'`$(EnableM02PanicSafeOwnedCom1Probe)' == 'true' and '`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true' and '`$(EnableM02PanicSafeOwnedCom1Probe)' == 'true'",
        "'`$(EnableM02PanicSafeOwnedCom1Probe)' == 'true' and '`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationProbe)' == 'true' and '`$(EnableM02OwnedX64GdtEncodingProbe)' == 'true'",
        "'`$(EnableM02OwnedX64GdtEncodingProbe)' == 'true' and '`$(EnableM04SzArrayAllocationProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true' and '`$(EnableM02OwnedX64GdtEncodingProbe)' == 'true'",
        "'`$(EnableM02OwnedX64GdtEncodingProbe)' == 'true' and '`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true' and '`$(EnableM02OwnedX64GdtEncodingProbe)' == 'true'",
        "'`$(EnableM02OwnedX64GdtEncodingProbe)' == 'true' and '`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationProbe)' == 'true' and '`$(EnableM02GuardedIstStackAllocationProbe)' == 'true'",
        "'`$(EnableM02GuardedIstStackAllocationProbe)' == 'true' and '`$(EnableM04SzArrayAllocationProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true' and '`$(EnableM02GuardedIstStackAllocationProbe)' == 'true'",
        "'`$(EnableM02GuardedIstStackAllocationProbe)' == 'true' and '`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true' and '`$(EnableM02GuardedIstStackAllocationProbe)' == 'true'",
        "'`$(EnableM02GuardedIstStackAllocationProbe)' == 'true' and '`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationProbe)' == 'true' and '`$(EnableM02OwnedGdtTssTerminalProbe)' == 'true'",
        "'`$(EnableM02OwnedGdtTssTerminalProbe)' == 'true' and '`$(EnableM04SzArrayAllocationProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true' and '`$(EnableM02OwnedGdtTssTerminalProbe)' == 'true'",
        "'`$(EnableM02OwnedGdtTssTerminalProbe)' == 'true' and '`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true' and '`$(EnableM02OwnedGdtTssTerminalProbe)' == 'true'",
        "'`$(EnableM02OwnedGdtTssTerminalProbe)' == 'true' and '`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationProbe)' == 'true' and '`$(EnableM02NormalizedInterruptFramePageProbe)' == 'true'",
        "'`$(EnableM02NormalizedInterruptFramePageProbe)' == 'true' and '`$(EnableM04SzArrayAllocationProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true' and '`$(EnableM02NormalizedInterruptFramePageProbe)' == 'true'",
        "'`$(EnableM02NormalizedInterruptFramePageProbe)' == 'true' and '`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true' and '`$(EnableM02NormalizedInterruptFramePageProbe)' == 'true'",
        "'`$(EnableM02NormalizedInterruptFramePageProbe)' == 'true' and '`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationProbe)' == 'true' and '`$(EnableM02OwnedIdtGatePopulationProbe)' == 'true'",
        "'`$(EnableM02OwnedIdtGatePopulationProbe)' == 'true' and '`$(EnableM04SzArrayAllocationProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true' and '`$(EnableM02OwnedIdtGatePopulationProbe)' == 'true'",
        "'`$(EnableM02OwnedIdtGatePopulationProbe)' == 'true' and '`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true' and '`$(EnableM02OwnedIdtGatePopulationProbe)' == 'true'",
        "'`$(EnableM02OwnedIdtGatePopulationProbe)' == 'true' and '`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationProbe)' == 'true' and '`$(EnableM02IdtrContinuedExecutionProbe)' == 'true'",
        "'`$(EnableM02IdtrContinuedExecutionProbe)' == 'true' and '`$(EnableM04SzArrayAllocationProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true' and '`$(EnableM02IdtrContinuedExecutionProbe)' == 'true'",
        "'`$(EnableM02IdtrContinuedExecutionProbe)' == 'true' and '`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true' and '`$(EnableM02IdtrContinuedExecutionProbe)' == 'true'",
        "'`$(EnableM02IdtrContinuedExecutionProbe)' == 'true' and '`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationProbe)' == 'true' and '`$(EnableM02Ud2VectorStateProbe)' == 'true'",
        "'`$(EnableM02Ud2VectorStateProbe)' == 'true' and '`$(EnableM04SzArrayAllocationProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true' and '`$(EnableM02Ud2VectorStateProbe)' == 'true'",
        "'`$(EnableM02Ud2VectorStateProbe)' == 'true' and '`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true' and '`$(EnableM02Ud2VectorStateProbe)' == 'true'",
        "'`$(EnableM02Ud2VectorStateProbe)' == 'true' and '`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationProbe)' == 'true' and '`$(EnableM02PageFaultCr2ErrorCodeProbe)' == 'true'",
        "'`$(EnableM02PageFaultCr2ErrorCodeProbe)' == 'true' and '`$(EnableM04SzArrayAllocationProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true' and '`$(EnableM02PageFaultCr2ErrorCodeProbe)' == 'true'",
        "'`$(EnableM02PageFaultCr2ErrorCodeProbe)' == 'true' and '`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true' and '`$(EnableM02PageFaultCr2ErrorCodeProbe)' == 'true'",
        "'`$(EnableM02PageFaultCr2ErrorCodeProbe)' == 'true' and '`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationProbe)' == 'true' and '`$(EnableM02DoubleFaultIstProbe)' == 'true'",
        "'`$(EnableM02DoubleFaultIstProbe)' == 'true' and '`$(EnableM04SzArrayAllocationProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true' and '`$(EnableM02DoubleFaultIstProbe)' == 'true'",
        "'`$(EnableM02DoubleFaultIstProbe)' == 'true' and '`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true' and '`$(EnableM02DoubleFaultIstProbe)' == 'true'",
        "'`$(EnableM02DoubleFaultIstProbe)' == 'true' and '`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationProbe)' == 'true' and '`$(EnableM01OwnedPagingProbe)' == 'true'",
        "'`$(EnableM01OwnedPagingProbe)' == 'true' and '`$(EnableM04SzArrayAllocationProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true' and '`$(EnableM01OwnedPagingProbe)' == 'true'",
        "'`$(EnableM01OwnedPagingProbe)' == 'true' and '`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true' and '`$(EnableM01OwnedPagingProbe)' == 'true'",
        "'`$(EnableM01OwnedPagingProbe)' == 'true' and '`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationProbe)' == 'true' and '`$(EnableUefiGoneSimulationProbe)' == 'true'",
        "'`$(EnableUefiGoneSimulationProbe)' == 'true' and '`$(EnableM04SzArrayAllocationProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true' and '`$(EnableUefiGoneSimulationProbe)' == 'true'",
        "'`$(EnableUefiGoneSimulationProbe)' == 'true' and '`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true' and '`$(EnableUefiGoneSimulationProbe)' == 'true'",
        "'`$(EnableUefiGoneSimulationProbe)' == 'true' and '`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationProbe)' == 'true' and '`$(EnableExitBootServicesFreshMapProbe)' == 'true'",
        "'`$(EnableExitBootServicesFreshMapProbe)' == 'true' and '`$(EnableM04SzArrayAllocationProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true' and '`$(EnableExitBootServicesFreshMapProbe)' == 'true'",
        "'`$(EnableExitBootServicesFreshMapProbe)' == 'true' and '`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true' and '`$(EnableExitBootServicesFreshMapProbe)' == 'true'",
        "'`$(EnableExitBootServicesFreshMapProbe)' == 'true' and '`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationProbe)' == 'true' and '`$(EnableAcpiPhysicalDiscoveryProbe)' == 'true'",
        "'`$(EnableAcpiPhysicalDiscoveryProbe)' == 'true' and '`$(EnableM04SzArrayAllocationProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true' and '`$(EnableAcpiPhysicalDiscoveryProbe)' == 'true'",
        "'`$(EnableAcpiPhysicalDiscoveryProbe)' == 'true' and '`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true' and '`$(EnableAcpiPhysicalDiscoveryProbe)' == 'true'",
        "'`$(EnableAcpiPhysicalDiscoveryProbe)' == 'true' and '`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationProbe)' == 'true' and '`$(EnableM04RhpNewFastBeforeBindFailureProbe)' == 'true'",
        "'`$(EnableM04RhpNewFastBeforeBindFailureProbe)' == 'true' and '`$(EnableM04SzArrayAllocationProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true' and '`$(EnableM04RhpNewFastBeforeBindFailureProbe)' == 'true'",
        "'`$(EnableM04RhpNewFastBeforeBindFailureProbe)' == 'true' and '`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true' and '`$(EnableM04RhpNewFastBeforeBindFailureProbe)' == 'true'",
        "'`$(EnableM04RhpNewFastBeforeBindFailureProbe)' == 'true' and '`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationProbe)' == 'true' and '`$(EnableM04RhpNewFastExhaustionFailureProbe)' == 'true'",
        "'`$(EnableM04RhpNewFastExhaustionFailureProbe)' == 'true' and '`$(EnableM04SzArrayAllocationProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true' and '`$(EnableM04RhpNewFastExhaustionFailureProbe)' == 'true'",
        "'`$(EnableM04RhpNewFastExhaustionFailureProbe)' == 'true' and '`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true' and '`$(EnableM04RhpNewFastExhaustionFailureProbe)' == 'true'",
        "'`$(EnableM04RhpNewFastExhaustionFailureProbe)' == 'true' and '`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true' and '`$(EnableM04SzArrayAllocationProbe)' != 'true'",
        "'`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true' and '`$(EnableM04SzArrayAllocationProbe)' != 'true'",
        "'`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true' and '`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM04RhpNewFastProbe)' == 'true' and '`$(EnableM01LoadedImageStackProbe)' != 'true'",
        "'`$(EnableM04RhpNewFastProbe)' == 'true' and '`$(EnableM01LiveAllocatorProbe)' != 'true'",
        "'`$(EnableM04RhpNewFastProbe)' == 'true' and '`$(EnableM04IndependentEndianConformanceProbe)' != 'true'",
        "'`$(EnableM04RhpNewFastProbe)' == 'true' and '`$(EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe)' != 'true'",
        "'`$(EnableM04RhpNewFastProbe)' == 'true' and '`$(EnableM04ArithmeticConformanceProbe)' != 'true'",
        "'`$(EnableM04RhpNewFastProbe)' == 'true' and '`$(EnableM04LayoutContractProbe)' != 'true'",
        "'`$(EnableM04RhpNewFastProbe)' == 'true' and '`$(EnableM04ManagedHeapPageRangeProbe)' != 'true'",
        "'`$(EnableM04RhpNewFastProbe)' == 'true' and '`$(EnableM02SafeStubProbe)' == 'true'",
        "'`$(EnableM04RhpNewFastProbe)' == 'true' and '`$(EnableM02CheckedPortIoBindingProbe)' == 'true'",
        "'`$(EnableM04RhpNewFastProbe)' == 'true' and '`$(EnableM02Com1OwnedPortIoProbe)' == 'true'",
        "'`$(EnableM04RhpNewFastProbe)' == 'true' and '`$(EnableM02OwnedCom1ReadyProbe)' == 'true'",
        "'`$(EnableM04RhpNewFastProbe)' == 'true' and '`$(EnableM02PanicSafeOwnedCom1Probe)' == 'true'",
        "'`$(EnableM04RhpNewFastProbe)' == 'true' and '`$(EnableM02OwnedX64GdtEncodingProbe)' == 'true'",
        "'`$(EnableM04RhpNewFastProbe)' == 'true' and '`$(EnableM02GuardedIstStackAllocationProbe)' == 'true'",
        "'`$(EnableM04RhpNewFastProbe)' == 'true' and '`$(EnableM02OwnedGdtTssTerminalProbe)' == 'true'",
        "'`$(EnableM04RhpNewFastProbe)' == 'true' and '`$(EnableM02NormalizedInterruptFramePageProbe)' == 'true'",
        "'`$(EnableM04RhpNewFastProbe)' == 'true' and '`$(EnableM02OwnedIdtGatePopulationProbe)' == 'true'",
        "'`$(EnableM04RhpNewFastProbe)' == 'true' and '`$(EnableM02IdtrContinuedExecutionProbe)' == 'true'",
        "'`$(EnableM04RhpNewFastProbe)' == 'true' and '`$(EnableM02Ud2VectorStateProbe)' == 'true'",
        "'`$(EnableM04RhpNewFastProbe)' == 'true' and '`$(EnableM02PageFaultCr2ErrorCodeProbe)' == 'true'",
        "'`$(EnableM04RhpNewFastProbe)' == 'true' and '`$(EnableM02DoubleFaultIstProbe)' == 'true'",
        "'`$(EnableM04RhpNewFastProbe)' == 'true' and '`$(EnableM01OwnedPagingProbe)' == 'true'",
        "'`$(EnableM04RhpNewFastProbe)' == 'true' and '`$(EnableUefiGoneSimulationProbe)' == 'true'",
        "'`$(EnableM04RhpNewFastProbe)' == 'true' and '`$(EnableExitBootServicesFreshMapProbe)' == 'true'",
        "'`$(EnableM04RhpNewFastProbe)' == 'true' and '`$(EnableAcpiPhysicalDiscoveryProbe)' == 'true'",
        "'`$(EnableM04RhpNewFastBeforeBindFailureProbe)' == 'true' and '`$(EnableM04RhpNewFastProbe)' != 'true'",
        "'`$(EnableM04RhpNewFastExhaustionFailureProbe)' == 'true' and '`$(EnableM04RhpNewFastProbe)' != 'true'",
        "'`$(EnableM04RhpNewFastBeforeBindFailureProbe)' == 'true' and '`$(EnableM04RhpNewFastExhaustionFailureProbe)' == 'true'",

        "'`$(EnableM02DoubleFaultIstProbe)' == 'true' and '`$(EnableM01LoadedImageStackProbe)' != 'true'",
        "'`$(EnableM02DoubleFaultIstProbe)' == 'true' and '`$(EnableM01LiveAllocatorProbe)' != 'true'",
        "'`$(EnableM02DoubleFaultIstProbe)' == 'true' and '`$(EnableM01OwnedPagingProbe)' != 'true'",
        "'`$(EnableM02DoubleFaultIstProbe)' == 'true' and '`$(EnableM02SafeStubProbe)' != 'true'",
        "'`$(EnableM02DoubleFaultIstProbe)' == 'true' and '`$(EnableM02CheckedPortIoBindingProbe)' != 'true'",
        "'`$(EnableM02DoubleFaultIstProbe)' == 'true' and '`$(EnableM02Com1OwnedPortIoProbe)' != 'true'",
        "'`$(EnableM02DoubleFaultIstProbe)' == 'true' and '`$(EnableM02OwnedCom1ReadyProbe)' != 'true'",
        "'`$(EnableM02DoubleFaultIstProbe)' == 'true' and '`$(EnableM02PanicSafeOwnedCom1Probe)' != 'true'",
        "'`$(EnableM02DoubleFaultIstProbe)' == 'true' and '`$(EnableUefiGoneSimulationProbe)' == 'true'",
        "'`$(EnableM02DoubleFaultIstProbe)' == 'true' and '`$(EnableExitBootServicesFreshMapProbe)' == 'true'",
        "'`$(EnableM02DoubleFaultIstProbe)' == 'true' and '`$(EnableAcpiPhysicalDiscoveryProbe)' == 'true'",
        "'`$(EnableM02DoubleFaultIstProbe)' == 'true' and '`$(EnableM02OwnedX64GdtEncodingProbe)' == 'true'",
        "'`$(EnableM02DoubleFaultIstProbe)' == 'true' and '`$(EnableM02GuardedIstStackAllocationProbe)' == 'true'",
        "'`$(EnableM02DoubleFaultIstProbe)' == 'true' and '`$(EnableM02OwnedGdtTssTerminalProbe)' == 'true'",
        "'`$(EnableM02DoubleFaultIstProbe)' == 'true' and '`$(EnableM02NormalizedInterruptFramePageProbe)' == 'true'",
        "'`$(EnableM02DoubleFaultIstProbe)' == 'true' and '`$(EnableM02OwnedIdtGatePopulationProbe)' == 'true'",
        "'`$(EnableM02DoubleFaultIstProbe)' == 'true' and '`$(EnableM02IdtrContinuedExecutionProbe)' == 'true'",
        "'`$(EnableM02DoubleFaultIstProbe)' == 'true' and '`$(EnableM02Ud2VectorStateProbe)' == 'true'",
        "'`$(EnableM02DoubleFaultIstProbe)' == 'true' and '`$(EnableM02PageFaultCr2ErrorCodeProbe)' == 'true'",
        "'`$(EnableM02DoubleFaultIstProbe)' == 'true' and '`$(EnableM04IndependentEndianConformanceProbe)' == 'true'",
        "'`$(EnableM02DoubleFaultIstProbe)' == 'true' and '`$(EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe)' == 'true'",
        "'`$(EnableM02DoubleFaultIstProbe)' == 'true' and '`$(EnableM04ArithmeticConformanceProbe)' == 'true'",
        "'`$(EnableM02DoubleFaultIstProbe)' == 'true' and '`$(EnableM04LayoutContractProbe)' == 'true'",
        "'`$(EnableM02DoubleFaultIstProbe)' == 'true' and '`$(EnableM04ManagedHeapPageRangeProbe)' == 'true'",
        "'`$(EnableM02DoubleFaultIstProbe)' == 'true' and '`$(EnableM04RhpNewFastProbe)' == 'true'",
        "'`$(EnableM02DoubleFaultIstProbe)' == 'true' and '`$(EnableM04RhpNewFastBeforeBindFailureProbe)' == 'true'",
        "'`$(EnableM02DoubleFaultIstProbe)' == 'true' and '`$(EnableM04RhpNewFastExhaustionFailureProbe)' == 'true'",
        "'`$(EnableM02PageFaultCr2ErrorCodeProbe)' == 'true' and '`$(EnableM02DoubleFaultIstProbe)' == 'true'",
        "'`$(EnableM02PageFaultCr2ErrorCodeProbe)' == 'true' and '`$(EnableM01LoadedImageStackProbe)' != 'true'",
        "'`$(EnableM02PageFaultCr2ErrorCodeProbe)' == 'true' and '`$(EnableM01LiveAllocatorProbe)' != 'true'",
        "'`$(EnableM02PageFaultCr2ErrorCodeProbe)' == 'true' and '`$(EnableM01OwnedPagingProbe)' != 'true'",
        "'`$(EnableM02PageFaultCr2ErrorCodeProbe)' == 'true' and '`$(EnableM02SafeStubProbe)' != 'true'",
        "'`$(EnableM02PageFaultCr2ErrorCodeProbe)' == 'true' and '`$(EnableM02CheckedPortIoBindingProbe)' != 'true'",
        "'`$(EnableM02PageFaultCr2ErrorCodeProbe)' == 'true' and '`$(EnableM02Com1OwnedPortIoProbe)' != 'true'",
        "'`$(EnableM02PageFaultCr2ErrorCodeProbe)' == 'true' and '`$(EnableM02OwnedCom1ReadyProbe)' != 'true'",
        "'`$(EnableM02PageFaultCr2ErrorCodeProbe)' == 'true' and '`$(EnableM02PanicSafeOwnedCom1Probe)' != 'true'",
        "'`$(EnableM02PageFaultCr2ErrorCodeProbe)' == 'true' and '`$(EnableM02OwnedX64GdtEncodingProbe)' == 'true'",
        "'`$(EnableM02PageFaultCr2ErrorCodeProbe)' == 'true' and '`$(EnableM02GuardedIstStackAllocationProbe)' == 'true'",
        "'`$(EnableM02PageFaultCr2ErrorCodeProbe)' == 'true' and '`$(EnableM02OwnedGdtTssTerminalProbe)' == 'true'",
        "'`$(EnableM02PageFaultCr2ErrorCodeProbe)' == 'true' and '`$(EnableM02NormalizedInterruptFramePageProbe)' == 'true'",
        "'`$(EnableM02PageFaultCr2ErrorCodeProbe)' == 'true' and '`$(EnableM02OwnedIdtGatePopulationProbe)' == 'true'",
        "'`$(EnableM02PageFaultCr2ErrorCodeProbe)' == 'true' and '`$(EnableM02IdtrContinuedExecutionProbe)' == 'true'",
        "'`$(EnableM02PageFaultCr2ErrorCodeProbe)' == 'true' and '`$(EnableM02Ud2VectorStateProbe)' == 'true'",
        "'`$(EnableM02PageFaultCr2ErrorCodeProbe)' == 'true' and '`$(EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe)' == 'true'",
        "'`$(EnableM02PageFaultCr2ErrorCodeProbe)' == 'true' and '`$(EnableM04ArithmeticConformanceProbe)' == 'true'",
        "'`$(EnableM02PageFaultCr2ErrorCodeProbe)' == 'true' and '`$(EnableM04LayoutContractProbe)' == 'true'",
        "'`$(EnableM02PageFaultCr2ErrorCodeProbe)' == 'true' and '`$(EnableM04ManagedHeapPageRangeProbe)' == 'true'",
        "'`$(EnableM04ManagedHeapPageRangeProbe)' == 'true' and '`$(EnableM01LoadedImageStackProbe)' != 'true'",
        "'`$(EnableM04ManagedHeapPageRangeProbe)' == 'true' and '`$(EnableM01LiveAllocatorProbe)' != 'true'",
        "'`$(EnableM04ManagedHeapPageRangeProbe)' == 'true' and '`$(EnableM04IndependentEndianConformanceProbe)' != 'true'",
        "'`$(EnableM04ManagedHeapPageRangeProbe)' == 'true' and '`$(EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe)' != 'true'",
        "'`$(EnableM04ManagedHeapPageRangeProbe)' == 'true' and '`$(EnableM04ArithmeticConformanceProbe)' != 'true'",
        "'`$(EnableM04ManagedHeapPageRangeProbe)' == 'true' and '`$(EnableM04LayoutContractProbe)' != 'true'",
        "'`$(EnableM04ManagedHeapPageRangeProbe)' == 'true' and '`$(EnableM01OwnedPagingProbe)' == 'true'",
        "'`$(EnableM04ManagedHeapPageRangeProbe)' == 'true' and '`$(EnableM02SafeStubProbe)' == 'true'",
        "'`$(EnableM04ManagedHeapPageRangeProbe)' == 'true' and '`$(EnableM02CheckedPortIoBindingProbe)' == 'true'",
        "'`$(EnableM04ManagedHeapPageRangeProbe)' == 'true' and '`$(EnableM02Com1OwnedPortIoProbe)' == 'true'",
        "'`$(EnableM04ManagedHeapPageRangeProbe)' == 'true' and '`$(EnableM02OwnedCom1ReadyProbe)' == 'true'",
        "'`$(EnableM04ManagedHeapPageRangeProbe)' == 'true' and '`$(EnableM02PanicSafeOwnedCom1Probe)' == 'true'",
        "'`$(EnableM04ManagedHeapPageRangeProbe)' == 'true' and '`$(EnableM02OwnedX64GdtEncodingProbe)' == 'true'",
        "'`$(EnableM04ManagedHeapPageRangeProbe)' == 'true' and '`$(EnableM02GuardedIstStackAllocationProbe)' == 'true'",
        "'`$(EnableM04ManagedHeapPageRangeProbe)' == 'true' and '`$(EnableM02OwnedGdtTssTerminalProbe)' == 'true'",
        "'`$(EnableM04ManagedHeapPageRangeProbe)' == 'true' and '`$(EnableM02NormalizedInterruptFramePageProbe)' == 'true'",
        "'`$(EnableM04ManagedHeapPageRangeProbe)' == 'true' and '`$(EnableM02OwnedIdtGatePopulationProbe)' == 'true'",
        "'`$(EnableM04ManagedHeapPageRangeProbe)' == 'true' and '`$(EnableM02IdtrContinuedExecutionProbe)' == 'true'",
        "'`$(EnableM04ManagedHeapPageRangeProbe)' == 'true' and '`$(EnableM02Ud2VectorStateProbe)' == 'true'",
        "'`$(EnableM04ManagedHeapPageRangeProbe)' == 'true' and '`$(EnableUefiGoneSimulationProbe)' == 'true'",
        "'`$(EnableM04ManagedHeapPageRangeProbe)' == 'true' and '`$(EnableExitBootServicesFreshMapProbe)' == 'true'",
        "'`$(EnableM04ManagedHeapPageRangeProbe)' == 'true' and '`$(EnableAcpiPhysicalDiscoveryProbe)' == 'true'",
        $allocatorDependencyCondition,
        $pagingDependencyCondition,
        $m02LoadedImageDependencyCondition,
        $m02AllocatorDependencyCondition,
        $m02PagingDependencyCondition,
        $m02CheckedBindingDependencyCondition,
        $m02Com1OwnedPortIoDependencyCondition,
        $m02OwnedCom1ReadyDependencyCondition,
        $m02PanicSafeOwnedCom1DependencyCondition,
        $m02OwnedX64GdtEncodingDependencyCondition,
        $m02GuardedIstStackAllocationDependencyCondition,
        $m02OwnedGdtTssTerminalDependencyCondition,
        $m02NormalizedInterruptFramePageTerminalExclusionCondition,
        $m02OwnedIdtGatePopulationTerminalExclusionCondition,
        $m02OwnedIdtGatePopulationNormalizedExclusionCondition,
        $m02IdtrContinuedExecutionTerminalExclusionCondition,
        $m02IdtrContinuedExecutionNormalizedExclusionCondition,
        $m02IdtrContinuedExecutionIdtExclusionCondition,
        $m02Ud2VectorStateDependencyCondition,
        $m02Ud2VectorStateOwnedX64GdtExclusionCondition,
        $m02Ud2VectorStateGuardedIstExclusionCondition,
        $m02Ud2VectorStateTerminalExclusionCondition,
        $m02Ud2VectorStateNormalizedExclusionCondition,
        $m02Ud2VectorStateIdtExclusionCondition,
        $m02Ud2VectorStateIdtrExclusionCondition,
        $m04SpanMemoryDependencyCondition,
        $m04SpanMemoryTerminalExclusionCondition,
        $m04SpanMemoryUd2ExclusionCondition,
        $m04ArithmeticDependencyCondition,
        $m04ArithmeticTerminalExclusionCondition,
        $m04ArithmeticUd2ExclusionCondition,
        $m04LayoutArithmeticDependencyCondition,
        $m04LayoutSpanDependencyCondition,
        $m04LayoutEndianDependencyCondition,
        $m04LayoutTerminalExclusionCondition,
        $m04LayoutUd2ExclusionCondition,
        "'`$(EnableM04StringAllocationProbe)' == 'true' and '`$(EnableM01LoadedImageStackProbe)' != 'true'",
        "'`$(EnableM04StringAllocationProbe)' == 'true' and '`$(EnableM01LiveAllocatorProbe)' != 'true'",
        "'`$(EnableM04StringAllocationProbe)' == 'true' and '`$(EnableM04IndependentEndianConformanceProbe)' != 'true'",
        "'`$(EnableM04StringAllocationProbe)' == 'true' and '`$(EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe)' != 'true'",
        "'`$(EnableM04StringAllocationProbe)' == 'true' and '`$(EnableM04ArithmeticConformanceProbe)' != 'true'",
        "'`$(EnableM04StringAllocationProbe)' == 'true' and '`$(EnableM04LayoutContractProbe)' != 'true'",
        "'`$(EnableM04StringAllocationProbe)' == 'true' and '`$(EnableM04ManagedHeapPageRangeProbe)' != 'true'",
        "'`$(EnableM04StringAllocationProbe)' == 'true' and '`$(EnableM04RhpNewFastProbe)' != 'true'",
        "'`$(EnableM04StringAllocationProbe)' == 'true' and '`$(EnableM04SzArrayAllocationProbe)' != 'true'",
        "'`$(EnableM04StringAllocationProbe)' == 'true' and '`$(EnableM01OwnedPagingProbe)' == 'true'",
        "'`$(EnableM01OwnedPagingProbe)' == 'true' and '`$(EnableM04StringAllocationProbe)' == 'true'",
        "'`$(EnableM04StringAllocationProbe)' == 'true' and '`$(EnableM02SafeStubProbe)' == 'true'",
        "'`$(EnableM02SafeStubProbe)' == 'true' and '`$(EnableM04StringAllocationProbe)' == 'true'",
        "'`$(EnableM04StringAllocationProbe)' == 'true' and '`$(EnableM02CheckedPortIoBindingProbe)' == 'true'",
        "'`$(EnableM02CheckedPortIoBindingProbe)' == 'true' and '`$(EnableM04StringAllocationProbe)' == 'true'",
        "'`$(EnableM04StringAllocationProbe)' == 'true' and '`$(EnableM02Com1OwnedPortIoProbe)' == 'true'",
        "'`$(EnableM02Com1OwnedPortIoProbe)' == 'true' and '`$(EnableM04StringAllocationProbe)' == 'true'",
        "'`$(EnableM04StringAllocationProbe)' == 'true' and '`$(EnableM02OwnedCom1ReadyProbe)' == 'true'",
        "'`$(EnableM02OwnedCom1ReadyProbe)' == 'true' and '`$(EnableM04StringAllocationProbe)' == 'true'",
        "'`$(EnableM04StringAllocationProbe)' == 'true' and '`$(EnableM02PanicSafeOwnedCom1Probe)' == 'true'",
        "'`$(EnableM02PanicSafeOwnedCom1Probe)' == 'true' and '`$(EnableM04StringAllocationProbe)' == 'true'",
        "'`$(EnableM04StringAllocationProbe)' == 'true' and '`$(EnableM02OwnedX64GdtEncodingProbe)' == 'true'",
        "'`$(EnableM02OwnedX64GdtEncodingProbe)' == 'true' and '`$(EnableM04StringAllocationProbe)' == 'true'",
        "'`$(EnableM04StringAllocationProbe)' == 'true' and '`$(EnableM02GuardedIstStackAllocationProbe)' == 'true'",
        "'`$(EnableM02GuardedIstStackAllocationProbe)' == 'true' and '`$(EnableM04StringAllocationProbe)' == 'true'",
        "'`$(EnableM04StringAllocationProbe)' == 'true' and '`$(EnableM02OwnedGdtTssTerminalProbe)' == 'true'",
        "'`$(EnableM02OwnedGdtTssTerminalProbe)' == 'true' and '`$(EnableM04StringAllocationProbe)' == 'true'",
        "'`$(EnableM04StringAllocationProbe)' == 'true' and '`$(EnableM02NormalizedInterruptFramePageProbe)' == 'true'",
        "'`$(EnableM02NormalizedInterruptFramePageProbe)' == 'true' and '`$(EnableM04StringAllocationProbe)' == 'true'",
        "'`$(EnableM04StringAllocationProbe)' == 'true' and '`$(EnableM02OwnedIdtGatePopulationProbe)' == 'true'",
        "'`$(EnableM02OwnedIdtGatePopulationProbe)' == 'true' and '`$(EnableM04StringAllocationProbe)' == 'true'",
        "'`$(EnableM04StringAllocationProbe)' == 'true' and '`$(EnableM02IdtrContinuedExecutionProbe)' == 'true'",
        "'`$(EnableM02IdtrContinuedExecutionProbe)' == 'true' and '`$(EnableM04StringAllocationProbe)' == 'true'",
        "'`$(EnableM04StringAllocationProbe)' == 'true' and '`$(EnableM02Ud2VectorStateProbe)' == 'true'",
        "'`$(EnableM02Ud2VectorStateProbe)' == 'true' and '`$(EnableM04StringAllocationProbe)' == 'true'",
        "'`$(EnableM04StringAllocationProbe)' == 'true' and '`$(EnableM02PageFaultCr2ErrorCodeProbe)' == 'true'",
        "'`$(EnableM02PageFaultCr2ErrorCodeProbe)' == 'true' and '`$(EnableM04StringAllocationProbe)' == 'true'",
        "'`$(EnableM04StringAllocationProbe)' == 'true' and '`$(EnableM02DoubleFaultIstProbe)' == 'true'",
        "'`$(EnableM02DoubleFaultIstProbe)' == 'true' and '`$(EnableM04StringAllocationProbe)' == 'true'",
        "'`$(EnableM04StringAllocationProbe)' == 'true' and '`$(EnableM02UnexpectedVectorPanicProbe)' == 'true'",
        "'`$(EnableM02UnexpectedVectorPanicProbe)' == 'true' and '`$(EnableM04StringAllocationProbe)' == 'true'",
        "'`$(EnableM04StringAllocationProbe)' == 'true' and '`$(EnableUefiGoneSimulationProbe)' == 'true'",
        "'`$(EnableUefiGoneSimulationProbe)' == 'true' and '`$(EnableM04StringAllocationProbe)' == 'true'",
        "'`$(EnableM04StringAllocationProbe)' == 'true' and '`$(EnableExitBootServicesFreshMapProbe)' == 'true'",
        "'`$(EnableExitBootServicesFreshMapProbe)' == 'true' and '`$(EnableM04StringAllocationProbe)' == 'true'",
        "'`$(EnableM04StringAllocationProbe)' == 'true' and '`$(EnableAcpiPhysicalDiscoveryProbe)' == 'true'",
        "'`$(EnableAcpiPhysicalDiscoveryProbe)' == 'true' and '`$(EnableM04StringAllocationProbe)' == 'true'",
        "'`$(EnableM04StringAllocationProbe)' == 'true' and '`$(EnableM04RhpNewFastBeforeBindFailureProbe)' == 'true'",
        "'`$(EnableM04RhpNewFastBeforeBindFailureProbe)' == 'true' and '`$(EnableM04StringAllocationProbe)' == 'true'",
        "'`$(EnableM04StringAllocationProbe)' == 'true' and '`$(EnableM04RhpNewFastExhaustionFailureProbe)' == 'true'",
        "'`$(EnableM04RhpNewFastExhaustionFailureProbe)' == 'true' and '`$(EnableM04StringAllocationProbe)' == 'true'",
        "'`$(EnableM04StringAllocationProbe)' == 'true' and '`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true' and '`$(EnableM04StringAllocationProbe)' == 'true'",
        "'`$(EnableM04StringAllocationProbe)' == 'true' and '`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true' and '`$(EnableM04StringAllocationProbe)' == 'true'",
        "'`$(EnableM04StringAllocationBeforeBindProbe)' == 'true' and '`$(EnableM01OwnedPagingProbe)' == 'true'",
        "'`$(EnableM01OwnedPagingProbe)' == 'true' and '`$(EnableM04StringAllocationBeforeBindProbe)' == 'true'",
        "'`$(EnableM04StringAllocationBeforeBindProbe)' == 'true' and '`$(EnableM02SafeStubProbe)' == 'true'",
        "'`$(EnableM02SafeStubProbe)' == 'true' and '`$(EnableM04StringAllocationBeforeBindProbe)' == 'true'",
        "'`$(EnableM04StringAllocationBeforeBindProbe)' == 'true' and '`$(EnableM02CheckedPortIoBindingProbe)' == 'true'",
        "'`$(EnableM02CheckedPortIoBindingProbe)' == 'true' and '`$(EnableM04StringAllocationBeforeBindProbe)' == 'true'",
        "'`$(EnableM04StringAllocationBeforeBindProbe)' == 'true' and '`$(EnableM02Com1OwnedPortIoProbe)' == 'true'",
        "'`$(EnableM02Com1OwnedPortIoProbe)' == 'true' and '`$(EnableM04StringAllocationBeforeBindProbe)' == 'true'",
        "'`$(EnableM04StringAllocationBeforeBindProbe)' == 'true' and '`$(EnableM02OwnedCom1ReadyProbe)' == 'true'",
        "'`$(EnableM02OwnedCom1ReadyProbe)' == 'true' and '`$(EnableM04StringAllocationBeforeBindProbe)' == 'true'",
        "'`$(EnableM04StringAllocationBeforeBindProbe)' == 'true' and '`$(EnableM02PanicSafeOwnedCom1Probe)' == 'true'",
        "'`$(EnableM02PanicSafeOwnedCom1Probe)' == 'true' and '`$(EnableM04StringAllocationBeforeBindProbe)' == 'true'",
        "'`$(EnableM04StringAllocationBeforeBindProbe)' == 'true' and '`$(EnableM02OwnedX64GdtEncodingProbe)' == 'true'",
        "'`$(EnableM02OwnedX64GdtEncodingProbe)' == 'true' and '`$(EnableM04StringAllocationBeforeBindProbe)' == 'true'",
        "'`$(EnableM04StringAllocationBeforeBindProbe)' == 'true' and '`$(EnableM02GuardedIstStackAllocationProbe)' == 'true'",
        "'`$(EnableM02GuardedIstStackAllocationProbe)' == 'true' and '`$(EnableM04StringAllocationBeforeBindProbe)' == 'true'",
        "'`$(EnableM04StringAllocationBeforeBindProbe)' == 'true' and '`$(EnableM02OwnedGdtTssTerminalProbe)' == 'true'",
        "'`$(EnableM02OwnedGdtTssTerminalProbe)' == 'true' and '`$(EnableM04StringAllocationBeforeBindProbe)' == 'true'",
        "'`$(EnableM04StringAllocationBeforeBindProbe)' == 'true' and '`$(EnableM02NormalizedInterruptFramePageProbe)' == 'true'",
        "'`$(EnableM02NormalizedInterruptFramePageProbe)' == 'true' and '`$(EnableM04StringAllocationBeforeBindProbe)' == 'true'",
        "'`$(EnableM04StringAllocationBeforeBindProbe)' == 'true' and '`$(EnableM02OwnedIdtGatePopulationProbe)' == 'true'",
        "'`$(EnableM02OwnedIdtGatePopulationProbe)' == 'true' and '`$(EnableM04StringAllocationBeforeBindProbe)' == 'true'",
        "'`$(EnableM04StringAllocationBeforeBindProbe)' == 'true' and '`$(EnableM02IdtrContinuedExecutionProbe)' == 'true'",
        "'`$(EnableM02IdtrContinuedExecutionProbe)' == 'true' and '`$(EnableM04StringAllocationBeforeBindProbe)' == 'true'",
        "'`$(EnableM04StringAllocationBeforeBindProbe)' == 'true' and '`$(EnableM02Ud2VectorStateProbe)' == 'true'",
        "'`$(EnableM02Ud2VectorStateProbe)' == 'true' and '`$(EnableM04StringAllocationBeforeBindProbe)' == 'true'",
        "'`$(EnableM04StringAllocationBeforeBindProbe)' == 'true' and '`$(EnableM02PageFaultCr2ErrorCodeProbe)' == 'true'",
        "'`$(EnableM02PageFaultCr2ErrorCodeProbe)' == 'true' and '`$(EnableM04StringAllocationBeforeBindProbe)' == 'true'",
        "'`$(EnableM04StringAllocationBeforeBindProbe)' == 'true' and '`$(EnableM02DoubleFaultIstProbe)' == 'true'",
        "'`$(EnableM02DoubleFaultIstProbe)' == 'true' and '`$(EnableM04StringAllocationBeforeBindProbe)' == 'true'",
        "'`$(EnableM04StringAllocationBeforeBindProbe)' == 'true' and '`$(EnableM02UnexpectedVectorPanicProbe)' == 'true'",
        "'`$(EnableM02UnexpectedVectorPanicProbe)' == 'true' and '`$(EnableM04StringAllocationBeforeBindProbe)' == 'true'",
        "'`$(EnableM04StringAllocationBeforeBindProbe)' == 'true' and '`$(EnableUefiGoneSimulationProbe)' == 'true'",
        "'`$(EnableUefiGoneSimulationProbe)' == 'true' and '`$(EnableM04StringAllocationBeforeBindProbe)' == 'true'",
        "'`$(EnableM04StringAllocationBeforeBindProbe)' == 'true' and '`$(EnableExitBootServicesFreshMapProbe)' == 'true'",
        "'`$(EnableExitBootServicesFreshMapProbe)' == 'true' and '`$(EnableM04StringAllocationBeforeBindProbe)' == 'true'",
        "'`$(EnableM04StringAllocationBeforeBindProbe)' == 'true' and '`$(EnableAcpiPhysicalDiscoveryProbe)' == 'true'",
        "'`$(EnableAcpiPhysicalDiscoveryProbe)' == 'true' and '`$(EnableM04StringAllocationBeforeBindProbe)' == 'true'",
        "'`$(EnableM04StringAllocationBeforeBindProbe)' == 'true' and '`$(EnableM04RhpNewFastBeforeBindFailureProbe)' == 'true'",
        "'`$(EnableM04RhpNewFastBeforeBindFailureProbe)' == 'true' and '`$(EnableM04StringAllocationBeforeBindProbe)' == 'true'",
        "'`$(EnableM04StringAllocationBeforeBindProbe)' == 'true' and '`$(EnableM04RhpNewFastExhaustionFailureProbe)' == 'true'",
        "'`$(EnableM04RhpNewFastExhaustionFailureProbe)' == 'true' and '`$(EnableM04StringAllocationBeforeBindProbe)' == 'true'",
        "'`$(EnableM04StringAllocationBeforeBindProbe)' == 'true' and '`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true' and '`$(EnableM04StringAllocationBeforeBindProbe)' == 'true'",
        "'`$(EnableM04StringAllocationBeforeBindProbe)' == 'true' and '`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true' and '`$(EnableM04StringAllocationBeforeBindProbe)' == 'true'",
        "'`$(EnableM04StringAllocationExhaustionProbe)' == 'true' and '`$(EnableM01OwnedPagingProbe)' == 'true'",
        "'`$(EnableM01OwnedPagingProbe)' == 'true' and '`$(EnableM04StringAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM04StringAllocationExhaustionProbe)' == 'true' and '`$(EnableM02SafeStubProbe)' == 'true'",
        "'`$(EnableM02SafeStubProbe)' == 'true' and '`$(EnableM04StringAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM04StringAllocationExhaustionProbe)' == 'true' and '`$(EnableM02CheckedPortIoBindingProbe)' == 'true'",
        "'`$(EnableM02CheckedPortIoBindingProbe)' == 'true' and '`$(EnableM04StringAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM04StringAllocationExhaustionProbe)' == 'true' and '`$(EnableM02Com1OwnedPortIoProbe)' == 'true'",
        "'`$(EnableM02Com1OwnedPortIoProbe)' == 'true' and '`$(EnableM04StringAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM04StringAllocationExhaustionProbe)' == 'true' and '`$(EnableM02OwnedCom1ReadyProbe)' == 'true'",
        "'`$(EnableM02OwnedCom1ReadyProbe)' == 'true' and '`$(EnableM04StringAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM04StringAllocationExhaustionProbe)' == 'true' and '`$(EnableM02PanicSafeOwnedCom1Probe)' == 'true'",
        "'`$(EnableM02PanicSafeOwnedCom1Probe)' == 'true' and '`$(EnableM04StringAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM04StringAllocationExhaustionProbe)' == 'true' and '`$(EnableM02OwnedX64GdtEncodingProbe)' == 'true'",
        "'`$(EnableM02OwnedX64GdtEncodingProbe)' == 'true' and '`$(EnableM04StringAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM04StringAllocationExhaustionProbe)' == 'true' and '`$(EnableM02GuardedIstStackAllocationProbe)' == 'true'",
        "'`$(EnableM02GuardedIstStackAllocationProbe)' == 'true' and '`$(EnableM04StringAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM04StringAllocationExhaustionProbe)' == 'true' and '`$(EnableM02OwnedGdtTssTerminalProbe)' == 'true'",
        "'`$(EnableM02OwnedGdtTssTerminalProbe)' == 'true' and '`$(EnableM04StringAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM04StringAllocationExhaustionProbe)' == 'true' and '`$(EnableM02NormalizedInterruptFramePageProbe)' == 'true'",
        "'`$(EnableM02NormalizedInterruptFramePageProbe)' == 'true' and '`$(EnableM04StringAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM04StringAllocationExhaustionProbe)' == 'true' and '`$(EnableM02OwnedIdtGatePopulationProbe)' == 'true'",
        "'`$(EnableM02OwnedIdtGatePopulationProbe)' == 'true' and '`$(EnableM04StringAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM04StringAllocationExhaustionProbe)' == 'true' and '`$(EnableM02IdtrContinuedExecutionProbe)' == 'true'",
        "'`$(EnableM02IdtrContinuedExecutionProbe)' == 'true' and '`$(EnableM04StringAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM04StringAllocationExhaustionProbe)' == 'true' and '`$(EnableM02Ud2VectorStateProbe)' == 'true'",
        "'`$(EnableM02Ud2VectorStateProbe)' == 'true' and '`$(EnableM04StringAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM04StringAllocationExhaustionProbe)' == 'true' and '`$(EnableM02PageFaultCr2ErrorCodeProbe)' == 'true'",
        "'`$(EnableM02PageFaultCr2ErrorCodeProbe)' == 'true' and '`$(EnableM04StringAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM04StringAllocationExhaustionProbe)' == 'true' and '`$(EnableM02DoubleFaultIstProbe)' == 'true'",
        "'`$(EnableM02DoubleFaultIstProbe)' == 'true' and '`$(EnableM04StringAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM04StringAllocationExhaustionProbe)' == 'true' and '`$(EnableM02UnexpectedVectorPanicProbe)' == 'true'",
        "'`$(EnableM02UnexpectedVectorPanicProbe)' == 'true' and '`$(EnableM04StringAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM04StringAllocationExhaustionProbe)' == 'true' and '`$(EnableUefiGoneSimulationProbe)' == 'true'",
        "'`$(EnableUefiGoneSimulationProbe)' == 'true' and '`$(EnableM04StringAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM04StringAllocationExhaustionProbe)' == 'true' and '`$(EnableExitBootServicesFreshMapProbe)' == 'true'",
        "'`$(EnableExitBootServicesFreshMapProbe)' == 'true' and '`$(EnableM04StringAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM04StringAllocationExhaustionProbe)' == 'true' and '`$(EnableAcpiPhysicalDiscoveryProbe)' == 'true'",
        "'`$(EnableAcpiPhysicalDiscoveryProbe)' == 'true' and '`$(EnableM04StringAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM04StringAllocationExhaustionProbe)' == 'true' and '`$(EnableM04RhpNewFastBeforeBindFailureProbe)' == 'true'",
        "'`$(EnableM04RhpNewFastBeforeBindFailureProbe)' == 'true' and '`$(EnableM04StringAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM04StringAllocationExhaustionProbe)' == 'true' and '`$(EnableM04RhpNewFastExhaustionFailureProbe)' == 'true'",
        "'`$(EnableM04RhpNewFastExhaustionFailureProbe)' == 'true' and '`$(EnableM04StringAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM04StringAllocationExhaustionProbe)' == 'true' and '`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true' and '`$(EnableM04StringAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM04StringAllocationExhaustionProbe)' == 'true' and '`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true' and '`$(EnableM04StringAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM04StringAllocationInvalidLengthProbe)' == 'true' and '`$(EnableM01OwnedPagingProbe)' == 'true'",
        "'`$(EnableM01OwnedPagingProbe)' == 'true' and '`$(EnableM04StringAllocationInvalidLengthProbe)' == 'true'",
        "'`$(EnableM04StringAllocationInvalidLengthProbe)' == 'true' and '`$(EnableM02SafeStubProbe)' == 'true'",
        "'`$(EnableM02SafeStubProbe)' == 'true' and '`$(EnableM04StringAllocationInvalidLengthProbe)' == 'true'",
        "'`$(EnableM04StringAllocationInvalidLengthProbe)' == 'true' and '`$(EnableM02CheckedPortIoBindingProbe)' == 'true'",
        "'`$(EnableM02CheckedPortIoBindingProbe)' == 'true' and '`$(EnableM04StringAllocationInvalidLengthProbe)' == 'true'",
        "'`$(EnableM04StringAllocationInvalidLengthProbe)' == 'true' and '`$(EnableM02Com1OwnedPortIoProbe)' == 'true'",
        "'`$(EnableM02Com1OwnedPortIoProbe)' == 'true' and '`$(EnableM04StringAllocationInvalidLengthProbe)' == 'true'",
        "'`$(EnableM04StringAllocationInvalidLengthProbe)' == 'true' and '`$(EnableM02OwnedCom1ReadyProbe)' == 'true'",
        "'`$(EnableM02OwnedCom1ReadyProbe)' == 'true' and '`$(EnableM04StringAllocationInvalidLengthProbe)' == 'true'",
        "'`$(EnableM04StringAllocationInvalidLengthProbe)' == 'true' and '`$(EnableM02PanicSafeOwnedCom1Probe)' == 'true'",
        "'`$(EnableM02PanicSafeOwnedCom1Probe)' == 'true' and '`$(EnableM04StringAllocationInvalidLengthProbe)' == 'true'",
        "'`$(EnableM04StringAllocationInvalidLengthProbe)' == 'true' and '`$(EnableM02OwnedX64GdtEncodingProbe)' == 'true'",
        "'`$(EnableM02OwnedX64GdtEncodingProbe)' == 'true' and '`$(EnableM04StringAllocationInvalidLengthProbe)' == 'true'",
        "'`$(EnableM04StringAllocationInvalidLengthProbe)' == 'true' and '`$(EnableM02GuardedIstStackAllocationProbe)' == 'true'",
        "'`$(EnableM02GuardedIstStackAllocationProbe)' == 'true' and '`$(EnableM04StringAllocationInvalidLengthProbe)' == 'true'",
        "'`$(EnableM04StringAllocationInvalidLengthProbe)' == 'true' and '`$(EnableM02OwnedGdtTssTerminalProbe)' == 'true'",
        "'`$(EnableM02OwnedGdtTssTerminalProbe)' == 'true' and '`$(EnableM04StringAllocationInvalidLengthProbe)' == 'true'",
        "'`$(EnableM04StringAllocationInvalidLengthProbe)' == 'true' and '`$(EnableM02NormalizedInterruptFramePageProbe)' == 'true'",
        "'`$(EnableM02NormalizedInterruptFramePageProbe)' == 'true' and '`$(EnableM04StringAllocationInvalidLengthProbe)' == 'true'",
        "'`$(EnableM04StringAllocationInvalidLengthProbe)' == 'true' and '`$(EnableM02OwnedIdtGatePopulationProbe)' == 'true'",
        "'`$(EnableM02OwnedIdtGatePopulationProbe)' == 'true' and '`$(EnableM04StringAllocationInvalidLengthProbe)' == 'true'",
        "'`$(EnableM04StringAllocationInvalidLengthProbe)' == 'true' and '`$(EnableM02IdtrContinuedExecutionProbe)' == 'true'",
        "'`$(EnableM02IdtrContinuedExecutionProbe)' == 'true' and '`$(EnableM04StringAllocationInvalidLengthProbe)' == 'true'",
        "'`$(EnableM04StringAllocationInvalidLengthProbe)' == 'true' and '`$(EnableM02Ud2VectorStateProbe)' == 'true'",
        "'`$(EnableM02Ud2VectorStateProbe)' == 'true' and '`$(EnableM04StringAllocationInvalidLengthProbe)' == 'true'",
        "'`$(EnableM04StringAllocationInvalidLengthProbe)' == 'true' and '`$(EnableM02PageFaultCr2ErrorCodeProbe)' == 'true'",
        "'`$(EnableM02PageFaultCr2ErrorCodeProbe)' == 'true' and '`$(EnableM04StringAllocationInvalidLengthProbe)' == 'true'",
        "'`$(EnableM04StringAllocationInvalidLengthProbe)' == 'true' and '`$(EnableM02DoubleFaultIstProbe)' == 'true'",
        "'`$(EnableM02DoubleFaultIstProbe)' == 'true' and '`$(EnableM04StringAllocationInvalidLengthProbe)' == 'true'",
        "'`$(EnableM04StringAllocationInvalidLengthProbe)' == 'true' and '`$(EnableM02UnexpectedVectorPanicProbe)' == 'true'",
        "'`$(EnableM02UnexpectedVectorPanicProbe)' == 'true' and '`$(EnableM04StringAllocationInvalidLengthProbe)' == 'true'",
        "'`$(EnableM04StringAllocationInvalidLengthProbe)' == 'true' and '`$(EnableUefiGoneSimulationProbe)' == 'true'",
        "'`$(EnableUefiGoneSimulationProbe)' == 'true' and '`$(EnableM04StringAllocationInvalidLengthProbe)' == 'true'",
        "'`$(EnableM04StringAllocationInvalidLengthProbe)' == 'true' and '`$(EnableExitBootServicesFreshMapProbe)' == 'true'",
        "'`$(EnableExitBootServicesFreshMapProbe)' == 'true' and '`$(EnableM04StringAllocationInvalidLengthProbe)' == 'true'",
        "'`$(EnableM04StringAllocationInvalidLengthProbe)' == 'true' and '`$(EnableAcpiPhysicalDiscoveryProbe)' == 'true'",
        "'`$(EnableAcpiPhysicalDiscoveryProbe)' == 'true' and '`$(EnableM04StringAllocationInvalidLengthProbe)' == 'true'",
        "'`$(EnableM04StringAllocationInvalidLengthProbe)' == 'true' and '`$(EnableM04RhpNewFastBeforeBindFailureProbe)' == 'true'",
        "'`$(EnableM04RhpNewFastBeforeBindFailureProbe)' == 'true' and '`$(EnableM04StringAllocationInvalidLengthProbe)' == 'true'",
        "'`$(EnableM04StringAllocationInvalidLengthProbe)' == 'true' and '`$(EnableM04RhpNewFastExhaustionFailureProbe)' == 'true'",
        "'`$(EnableM04RhpNewFastExhaustionFailureProbe)' == 'true' and '`$(EnableM04StringAllocationInvalidLengthProbe)' == 'true'",
        "'`$(EnableM04StringAllocationInvalidLengthProbe)' == 'true' and '`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true' and '`$(EnableM04StringAllocationInvalidLengthProbe)' == 'true'",
        "'`$(EnableM04StringAllocationInvalidLengthProbe)' == 'true' and '`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true' and '`$(EnableM04StringAllocationInvalidLengthProbe)' == 'true'",
        "'`$(EnableM04StringAllocationBeforeBindProbe)' == 'true' and '`$(EnableM04StringAllocationProbe)' != 'true'",
        "'`$(EnableM04StringAllocationExhaustionProbe)' == 'true' and '`$(EnableM04StringAllocationProbe)' != 'true'",
        "'`$(EnableM04StringAllocationInvalidLengthProbe)' == 'true' and '`$(EnableM04StringAllocationProbe)' != 'true'",
        "'`$(EnableM04StringAllocationBeforeBindProbe)' == 'true' and '`$(EnableM04StringAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM04StringAllocationBeforeBindProbe)' == 'true' and '`$(EnableM04StringAllocationInvalidLengthProbe)' == 'true'",
        "'`$(EnableM04StringAllocationExhaustionProbe)' == 'true' and '`$(EnableM04StringAllocationBeforeBindProbe)' == 'true'",
        "'`$(EnableM04StringAllocationExhaustionProbe)' == 'true' and '`$(EnableM04StringAllocationInvalidLengthProbe)' == 'true'",
        "'`$(EnableM04StringAllocationInvalidLengthProbe)' == 'true' and '`$(EnableM04StringAllocationBeforeBindProbe)' == 'true'",
        "'`$(EnableM04StringAllocationInvalidLengthProbe)' == 'true' and '`$(EnableM04StringAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM03MemoryMapCaptureProbe)' == 'true' and '`$(EnableM01LoadedImageStackProbe)' == 'true'",
        "'`$(EnableM01LoadedImageStackProbe)' == 'true' and '`$(EnableM03MemoryMapCaptureProbe)' == 'true'",
        "'`$(EnableM03MemoryMapCaptureProbe)' == 'true' and '`$(EnableM01LiveAllocatorProbe)' == 'true'",
        "'`$(EnableM01LiveAllocatorProbe)' == 'true' and '`$(EnableM03MemoryMapCaptureProbe)' == 'true'",
        "'`$(EnableM03MemoryMapCaptureProbe)' == 'true' and '`$(EnableM01OwnedPagingProbe)' == 'true'",
        "'`$(EnableM01OwnedPagingProbe)' == 'true' and '`$(EnableM03MemoryMapCaptureProbe)' == 'true'",
        "'`$(EnableM03MemoryMapCaptureProbe)' == 'true' and '`$(EnableM02SafeStubProbe)' == 'true'",
        "'`$(EnableM02SafeStubProbe)' == 'true' and '`$(EnableM03MemoryMapCaptureProbe)' == 'true'",
        "'`$(EnableM03MemoryMapCaptureProbe)' == 'true' and '`$(EnableM02CheckedPortIoBindingProbe)' == 'true'",
        "'`$(EnableM02CheckedPortIoBindingProbe)' == 'true' and '`$(EnableM03MemoryMapCaptureProbe)' == 'true'",
        "'`$(EnableM03MemoryMapCaptureProbe)' == 'true' and '`$(EnableM02Com1OwnedPortIoProbe)' == 'true'",
        "'`$(EnableM02Com1OwnedPortIoProbe)' == 'true' and '`$(EnableM03MemoryMapCaptureProbe)' == 'true'",
        "'`$(EnableM03MemoryMapCaptureProbe)' == 'true' and '`$(EnableM02OwnedCom1ReadyProbe)' == 'true'",
        "'`$(EnableM02OwnedCom1ReadyProbe)' == 'true' and '`$(EnableM03MemoryMapCaptureProbe)' == 'true'",
        "'`$(EnableM03MemoryMapCaptureProbe)' == 'true' and '`$(EnableM02PanicSafeOwnedCom1Probe)' == 'true'",
        "'`$(EnableM02PanicSafeOwnedCom1Probe)' == 'true' and '`$(EnableM03MemoryMapCaptureProbe)' == 'true'",
        "'`$(EnableM03MemoryMapCaptureProbe)' == 'true' and '`$(EnableM02OwnedX64GdtEncodingProbe)' == 'true'",
        "'`$(EnableM02OwnedX64GdtEncodingProbe)' == 'true' and '`$(EnableM03MemoryMapCaptureProbe)' == 'true'",
        "'`$(EnableM03MemoryMapCaptureProbe)' == 'true' and '`$(EnableM02GuardedIstStackAllocationProbe)' == 'true'",
        "'`$(EnableM02GuardedIstStackAllocationProbe)' == 'true' and '`$(EnableM03MemoryMapCaptureProbe)' == 'true'",
        "'`$(EnableM03MemoryMapCaptureProbe)' == 'true' and '`$(EnableM02OwnedGdtTssTerminalProbe)' == 'true'",
        "'`$(EnableM02OwnedGdtTssTerminalProbe)' == 'true' and '`$(EnableM03MemoryMapCaptureProbe)' == 'true'",
        "'`$(EnableM03MemoryMapCaptureProbe)' == 'true' and '`$(EnableM02NormalizedInterruptFramePageProbe)' == 'true'",
        "'`$(EnableM02NormalizedInterruptFramePageProbe)' == 'true' and '`$(EnableM03MemoryMapCaptureProbe)' == 'true'",
        "'`$(EnableM03MemoryMapCaptureProbe)' == 'true' and '`$(EnableM02OwnedIdtGatePopulationProbe)' == 'true'",
        "'`$(EnableM02OwnedIdtGatePopulationProbe)' == 'true' and '`$(EnableM03MemoryMapCaptureProbe)' == 'true'",
        "'`$(EnableM03MemoryMapCaptureProbe)' == 'true' and '`$(EnableM02IdtrContinuedExecutionProbe)' == 'true'",
        "'`$(EnableM02IdtrContinuedExecutionProbe)' == 'true' and '`$(EnableM03MemoryMapCaptureProbe)' == 'true'",
        "'`$(EnableM03MemoryMapCaptureProbe)' == 'true' and '`$(EnableM02Ud2VectorStateProbe)' == 'true'",
        "'`$(EnableM02Ud2VectorStateProbe)' == 'true' and '`$(EnableM03MemoryMapCaptureProbe)' == 'true'",
        "'`$(EnableM03MemoryMapCaptureProbe)' == 'true' and '`$(EnableM04IndependentEndianConformanceProbe)' == 'true'",
        "'`$(EnableM04IndependentEndianConformanceProbe)' == 'true' and '`$(EnableM03MemoryMapCaptureProbe)' == 'true'",
        "'`$(EnableM03MemoryMapCaptureProbe)' == 'true' and '`$(EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe)' == 'true'",
        "'`$(EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe)' == 'true' and '`$(EnableM03MemoryMapCaptureProbe)' == 'true'",
        "'`$(EnableM03MemoryMapCaptureProbe)' == 'true' and '`$(EnableM04ArithmeticConformanceProbe)' == 'true'",
        "'`$(EnableM04ArithmeticConformanceProbe)' == 'true' and '`$(EnableM03MemoryMapCaptureProbe)' == 'true'",
        "'`$(EnableM03MemoryMapCaptureProbe)' == 'true' and '`$(EnableM04LayoutContractProbe)' == 'true'",
        "'`$(EnableM04LayoutContractProbe)' == 'true' and '`$(EnableM03MemoryMapCaptureProbe)' == 'true'",
        "'`$(EnableM03MemoryMapCaptureProbe)' == 'true' and '`$(EnableM04ManagedHeapPageRangeProbe)' == 'true'",
        "'`$(EnableM04ManagedHeapPageRangeProbe)' == 'true' and '`$(EnableM03MemoryMapCaptureProbe)' == 'true'",
        "'`$(EnableM03MemoryMapCaptureProbe)' == 'true' and '`$(EnableM02PageFaultCr2ErrorCodeProbe)' == 'true'",
        "'`$(EnableM02PageFaultCr2ErrorCodeProbe)' == 'true' and '`$(EnableM03MemoryMapCaptureProbe)' == 'true'",
        "'`$(EnableM03MemoryMapCaptureProbe)' == 'true' and '`$(EnableM02DoubleFaultIstProbe)' == 'true'",
        "'`$(EnableM02DoubleFaultIstProbe)' == 'true' and '`$(EnableM03MemoryMapCaptureProbe)' == 'true'",
        "'`$(EnableM03MemoryMapCaptureProbe)' == 'true' and '`$(EnableM02UnexpectedVectorPanicProbe)' == 'true'",
        "'`$(EnableM02UnexpectedVectorPanicProbe)' == 'true' and '`$(EnableM03MemoryMapCaptureProbe)' == 'true'",
        "'`$(EnableM03MemoryMapCaptureProbe)' == 'true' and '`$(EnableM04RhpNewFastProbe)' == 'true'",
        "'`$(EnableM04RhpNewFastProbe)' == 'true' and '`$(EnableM03MemoryMapCaptureProbe)' == 'true'",
        "'`$(EnableM03MemoryMapCaptureProbe)' == 'true' and '`$(EnableM04RhpNewFastBeforeBindFailureProbe)' == 'true'",
        "'`$(EnableM04RhpNewFastBeforeBindFailureProbe)' == 'true' and '`$(EnableM03MemoryMapCaptureProbe)' == 'true'",
        "'`$(EnableM03MemoryMapCaptureProbe)' == 'true' and '`$(EnableM04RhpNewFastExhaustionFailureProbe)' == 'true'",
        "'`$(EnableM04RhpNewFastExhaustionFailureProbe)' == 'true' and '`$(EnableM03MemoryMapCaptureProbe)' == 'true'",
        "'`$(EnableM03MemoryMapCaptureProbe)' == 'true' and '`$(EnableM04SzArrayAllocationProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationProbe)' == 'true' and '`$(EnableM03MemoryMapCaptureProbe)' == 'true'",
        "'`$(EnableM03MemoryMapCaptureProbe)' == 'true' and '`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true' and '`$(EnableM03MemoryMapCaptureProbe)' == 'true'",
        "'`$(EnableM03MemoryMapCaptureProbe)' == 'true' and '`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true' and '`$(EnableM03MemoryMapCaptureProbe)' == 'true'",
        "'`$(EnableM03MemoryMapCaptureProbe)' == 'true' and '`$(EnableM04StringAllocationProbe)' == 'true'",
        "'`$(EnableM04StringAllocationProbe)' == 'true' and '`$(EnableM03MemoryMapCaptureProbe)' == 'true'",
        "'`$(EnableM03MemoryMapCaptureProbe)' == 'true' and '`$(EnableM04StringAllocationBeforeBindProbe)' == 'true'",
        "'`$(EnableM04StringAllocationBeforeBindProbe)' == 'true' and '`$(EnableM03MemoryMapCaptureProbe)' == 'true'",
        "'`$(EnableM03MemoryMapCaptureProbe)' == 'true' and '`$(EnableM04StringAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM04StringAllocationExhaustionProbe)' == 'true' and '`$(EnableM03MemoryMapCaptureProbe)' == 'true'",
        "'`$(EnableM03MemoryMapCaptureProbe)' == 'true' and '`$(EnableM04StringAllocationInvalidLengthProbe)' == 'true'",
        "'`$(EnableM04StringAllocationInvalidLengthProbe)' == 'true' and '`$(EnableM03MemoryMapCaptureProbe)' == 'true'",
        "'`$(EnableM03MemoryMapCaptureProbe)' == 'true' and '`$(EnableUefiGoneSimulationProbe)' == 'true'",
        "'`$(EnableUefiGoneSimulationProbe)' == 'true' and '`$(EnableM03MemoryMapCaptureProbe)' == 'true'",
        "'`$(EnableM03MemoryMapCaptureProbe)' == 'true' and '`$(EnableExitBootServicesFreshMapProbe)' == 'true'",
        "'`$(EnableExitBootServicesFreshMapProbe)' == 'true' and '`$(EnableM03MemoryMapCaptureProbe)' == 'true'",
        "'`$(EnableM03MemoryMapCaptureProbe)' == 'true' and '`$(EnableAcpiPhysicalDiscoveryProbe)' == 'true'",
        "'`$(EnableAcpiPhysicalDiscoveryProbe)' == 'true' and '`$(EnableM03MemoryMapCaptureProbe)' == 'true'",
        "'`$(EnableM03FirmwareEntropySeedProbe)' == 'true' and '`$(EnableAcpiPhysicalDiscoveryProbe)' == 'true'",
        "'`$(EnableAcpiPhysicalDiscoveryProbe)' == 'true' and '`$(EnableM03FirmwareEntropySeedProbe)' == 'true'",
        "'`$(EnableM03FirmwareEntropySeedProbe)' == 'true' and '`$(EnableExitBootServicesFreshMapProbe)' == 'true'",
        "'`$(EnableExitBootServicesFreshMapProbe)' == 'true' and '`$(EnableM03FirmwareEntropySeedProbe)' == 'true'",
        "'`$(EnableM03FirmwareEntropySeedProbe)' == 'true' and '`$(EnableM01LiveAllocatorProbe)' == 'true'",
        "'`$(EnableM01LiveAllocatorProbe)' == 'true' and '`$(EnableM03FirmwareEntropySeedProbe)' == 'true'",
        "'`$(EnableM03FirmwareEntropySeedProbe)' == 'true' and '`$(EnableM01LoadedImageStackProbe)' == 'true'",
        "'`$(EnableM01LoadedImageStackProbe)' == 'true' and '`$(EnableM03FirmwareEntropySeedProbe)' == 'true'",
        "'`$(EnableM03FirmwareEntropySeedProbe)' == 'true' and '`$(EnableM01OwnedPagingProbe)' == 'true'",
        "'`$(EnableM01OwnedPagingProbe)' == 'true' and '`$(EnableM03FirmwareEntropySeedProbe)' == 'true'",
        "'`$(EnableM03FirmwareEntropySeedProbe)' == 'true' and '`$(EnableM02CheckedPortIoBindingProbe)' == 'true'",
        "'`$(EnableM02CheckedPortIoBindingProbe)' == 'true' and '`$(EnableM03FirmwareEntropySeedProbe)' == 'true'",
        "'`$(EnableM03FirmwareEntropySeedProbe)' == 'true' and '`$(EnableM02Com1OwnedPortIoProbe)' == 'true'",
        "'`$(EnableM02Com1OwnedPortIoProbe)' == 'true' and '`$(EnableM03FirmwareEntropySeedProbe)' == 'true'",
        "'`$(EnableM03FirmwareEntropySeedProbe)' == 'true' and '`$(EnableM02DoubleFaultIstProbe)' == 'true'",
        "'`$(EnableM02DoubleFaultIstProbe)' == 'true' and '`$(EnableM03FirmwareEntropySeedProbe)' == 'true'",
        "'`$(EnableM03FirmwareEntropySeedProbe)' == 'true' and '`$(EnableM02GuardedIstStackAllocationProbe)' == 'true'",
        "'`$(EnableM02GuardedIstStackAllocationProbe)' == 'true' and '`$(EnableM03FirmwareEntropySeedProbe)' == 'true'",
        "'`$(EnableM03FirmwareEntropySeedProbe)' == 'true' and '`$(EnableM02IdtrContinuedExecutionProbe)' == 'true'",
        "'`$(EnableM02IdtrContinuedExecutionProbe)' == 'true' and '`$(EnableM03FirmwareEntropySeedProbe)' == 'true'",
        "'`$(EnableM03FirmwareEntropySeedProbe)' == 'true' and '`$(EnableM02NormalizedInterruptFramePageProbe)' == 'true'",
        "'`$(EnableM02NormalizedInterruptFramePageProbe)' == 'true' and '`$(EnableM03FirmwareEntropySeedProbe)' == 'true'",
        "'`$(EnableM03FirmwareEntropySeedProbe)' == 'true' and '`$(EnableM02OwnedCom1ReadyProbe)' == 'true'",
        "'`$(EnableM02OwnedCom1ReadyProbe)' == 'true' and '`$(EnableM03FirmwareEntropySeedProbe)' == 'true'",
        "'`$(EnableM03FirmwareEntropySeedProbe)' == 'true' and '`$(EnableM02OwnedGdtTssTerminalProbe)' == 'true'",
        "'`$(EnableM02OwnedGdtTssTerminalProbe)' == 'true' and '`$(EnableM03FirmwareEntropySeedProbe)' == 'true'",
        "'`$(EnableM03FirmwareEntropySeedProbe)' == 'true' and '`$(EnableM02OwnedIdtGatePopulationProbe)' == 'true'",
        "'`$(EnableM02OwnedIdtGatePopulationProbe)' == 'true' and '`$(EnableM03FirmwareEntropySeedProbe)' == 'true'",
        "'`$(EnableM03FirmwareEntropySeedProbe)' == 'true' and '`$(EnableM02OwnedX64GdtEncodingProbe)' == 'true'",
        "'`$(EnableM02OwnedX64GdtEncodingProbe)' == 'true' and '`$(EnableM03FirmwareEntropySeedProbe)' == 'true'",
        "'`$(EnableM03FirmwareEntropySeedProbe)' == 'true' and '`$(EnableM02PageFaultCr2ErrorCodeProbe)' == 'true'",
        "'`$(EnableM02PageFaultCr2ErrorCodeProbe)' == 'true' and '`$(EnableM03FirmwareEntropySeedProbe)' == 'true'",
        "'`$(EnableM03FirmwareEntropySeedProbe)' == 'true' and '`$(EnableM02PanicSafeOwnedCom1Probe)' == 'true'",
        "'`$(EnableM02PanicSafeOwnedCom1Probe)' == 'true' and '`$(EnableM03FirmwareEntropySeedProbe)' == 'true'",
        "'`$(EnableM03FirmwareEntropySeedProbe)' == 'true' and '`$(EnableM02SafeStubProbe)' == 'true'",
        "'`$(EnableM02SafeStubProbe)' == 'true' and '`$(EnableM03FirmwareEntropySeedProbe)' == 'true'",
        "'`$(EnableM03FirmwareEntropySeedProbe)' == 'true' and '`$(EnableM02Ud2VectorStateProbe)' == 'true'",
        "'`$(EnableM02Ud2VectorStateProbe)' == 'true' and '`$(EnableM03FirmwareEntropySeedProbe)' == 'true'",
        "'`$(EnableM03FirmwareEntropySeedProbe)' == 'true' and '`$(EnableM02UnexpectedVectorPanicProbe)' == 'true'",
        "'`$(EnableM02UnexpectedVectorPanicProbe)' == 'true' and '`$(EnableM03FirmwareEntropySeedProbe)' == 'true'",
        "'`$(EnableM03FirmwareEntropySeedProbe)' == 'true' and '`$(EnableM03MemoryMapCaptureProbe)' == 'true'",
        "'`$(EnableM03MemoryMapCaptureProbe)' == 'true' and '`$(EnableM03FirmwareEntropySeedProbe)' == 'true'",
        "'`$(EnableM03FirmwareEntropySeedProbe)' == 'true' and '`$(EnableM04ArithmeticConformanceProbe)' == 'true'",
        "'`$(EnableM04ArithmeticConformanceProbe)' == 'true' and '`$(EnableM03FirmwareEntropySeedProbe)' == 'true'",
        "'`$(EnableM03FirmwareEntropySeedProbe)' == 'true' and '`$(EnableM04CheckedReferenceBarrierProbe)' == 'true'",
        "'`$(EnableM04CheckedReferenceBarrierProbe)' == 'true' and '`$(EnableM03FirmwareEntropySeedProbe)' == 'true'",
        "'`$(EnableM03FirmwareEntropySeedProbe)' == 'true' and '`$(EnableM04IndependentEndianConformanceProbe)' == 'true'",
        "'`$(EnableM04IndependentEndianConformanceProbe)' == 'true' and '`$(EnableM03FirmwareEntropySeedProbe)' == 'true'",
        "'`$(EnableM03FirmwareEntropySeedProbe)' == 'true' and '`$(EnableM04LayoutContractProbe)' == 'true'",
        "'`$(EnableM04LayoutContractProbe)' == 'true' and '`$(EnableM03FirmwareEntropySeedProbe)' == 'true'",
        "'`$(EnableM03FirmwareEntropySeedProbe)' == 'true' and '`$(EnableM04ManagedHeapPageRangeProbe)' == 'true'",
        "'`$(EnableM04ManagedHeapPageRangeProbe)' == 'true' and '`$(EnableM03FirmwareEntropySeedProbe)' == 'true'",
        "'`$(EnableM03FirmwareEntropySeedProbe)' == 'true' and '`$(EnableM04RhpNewFastBeforeBindFailureProbe)' == 'true'",
        "'`$(EnableM04RhpNewFastBeforeBindFailureProbe)' == 'true' and '`$(EnableM03FirmwareEntropySeedProbe)' == 'true'",
        "'`$(EnableM03FirmwareEntropySeedProbe)' == 'true' and '`$(EnableM04RhpNewFastExhaustionFailureProbe)' == 'true'",
        "'`$(EnableM04RhpNewFastExhaustionFailureProbe)' == 'true' and '`$(EnableM03FirmwareEntropySeedProbe)' == 'true'",
        "'`$(EnableM03FirmwareEntropySeedProbe)' == 'true' and '`$(EnableM04RhpNewFastProbe)' == 'true'",
        "'`$(EnableM04RhpNewFastProbe)' == 'true' and '`$(EnableM03FirmwareEntropySeedProbe)' == 'true'",
        "'`$(EnableM03FirmwareEntropySeedProbe)' == 'true' and '`$(EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe)' == 'true'",
        "'`$(EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe)' == 'true' and '`$(EnableM03FirmwareEntropySeedProbe)' == 'true'",
        "'`$(EnableM03FirmwareEntropySeedProbe)' == 'true' and '`$(EnableM04StringAllocationBeforeBindProbe)' == 'true'",
        "'`$(EnableM04StringAllocationBeforeBindProbe)' == 'true' and '`$(EnableM03FirmwareEntropySeedProbe)' == 'true'",
        "'`$(EnableM03FirmwareEntropySeedProbe)' == 'true' and '`$(EnableM04StringAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM04StringAllocationExhaustionProbe)' == 'true' and '`$(EnableM03FirmwareEntropySeedProbe)' == 'true'",
        "'`$(EnableM03FirmwareEntropySeedProbe)' == 'true' and '`$(EnableM04StringAllocationInvalidLengthProbe)' == 'true'",
        "'`$(EnableM04StringAllocationInvalidLengthProbe)' == 'true' and '`$(EnableM03FirmwareEntropySeedProbe)' == 'true'",
        "'`$(EnableM03FirmwareEntropySeedProbe)' == 'true' and '`$(EnableM04StringAllocationProbe)' == 'true'",
        "'`$(EnableM04StringAllocationProbe)' == 'true' and '`$(EnableM03FirmwareEntropySeedProbe)' == 'true'",
        "'`$(EnableM03FirmwareEntropySeedProbe)' == 'true' and '`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true' and '`$(EnableM03FirmwareEntropySeedProbe)' == 'true'",
        "'`$(EnableM03FirmwareEntropySeedProbe)' == 'true' and '`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true' and '`$(EnableM03FirmwareEntropySeedProbe)' == 'true'",
        "'`$(EnableM03FirmwareEntropySeedProbe)' == 'true' and '`$(EnableM04SzArrayAllocationProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationProbe)' == 'true' and '`$(EnableM03FirmwareEntropySeedProbe)' == 'true'",
        "'`$(EnableM03FirmwareEntropySeedProbe)' == 'true' and '`$(EnableUefiGoneSimulationProbe)' == 'true'",
        "'`$(EnableUefiGoneSimulationProbe)' == 'true' and '`$(EnableM03FirmwareEntropySeedProbe)' == 'true'",
        "'`$(EnableM04TypeManagerModuleRegistrationProbe)' != '' and '`$(EnableM04TypeManagerModuleRegistrationProbe)' != 'true' and '`$(EnableM04TypeManagerModuleRegistrationProbe)' != 'false'",
        "'`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true' and '`$(EnableM01LoadedImageStackProbe)' != 'true'",
        "'`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true' and '`$(EnableM01LiveAllocatorProbe)' == 'true'",
        "'`$(EnableM01LiveAllocatorProbe)' == 'true' and '`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true'",
        "'`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true' and '`$(EnableM01OwnedPagingProbe)' == 'true'",
        "'`$(EnableM01OwnedPagingProbe)' == 'true' and '`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true'",
        "'`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true' and '`$(EnableM02SafeStubProbe)' == 'true'",
        "'`$(EnableM02SafeStubProbe)' == 'true' and '`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true'",
        "'`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true' and '`$(EnableM02CheckedPortIoBindingProbe)' == 'true'",
        "'`$(EnableM02CheckedPortIoBindingProbe)' == 'true' and '`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true'",
        "'`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true' and '`$(EnableM02Com1OwnedPortIoProbe)' == 'true'",
        "'`$(EnableM02Com1OwnedPortIoProbe)' == 'true' and '`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true'",
        "'`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true' and '`$(EnableM02OwnedCom1ReadyProbe)' == 'true'",
        "'`$(EnableM02OwnedCom1ReadyProbe)' == 'true' and '`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true'",
        "'`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true' and '`$(EnableM02PanicSafeOwnedCom1Probe)' == 'true'",
        "'`$(EnableM02PanicSafeOwnedCom1Probe)' == 'true' and '`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true'",
        "'`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true' and '`$(EnableM02OwnedX64GdtEncodingProbe)' == 'true'",
        "'`$(EnableM02OwnedX64GdtEncodingProbe)' == 'true' and '`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true'",
        "'`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true' and '`$(EnableM02GuardedIstStackAllocationProbe)' == 'true'",
        "'`$(EnableM02GuardedIstStackAllocationProbe)' == 'true' and '`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true'",
        "'`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true' and '`$(EnableM02OwnedGdtTssTerminalProbe)' == 'true'",
        "'`$(EnableM02OwnedGdtTssTerminalProbe)' == 'true' and '`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true'",
        "'`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true' and '`$(EnableM02NormalizedInterruptFramePageProbe)' == 'true'",
        "'`$(EnableM02NormalizedInterruptFramePageProbe)' == 'true' and '`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true'",
        "'`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true' and '`$(EnableM02OwnedIdtGatePopulationProbe)' == 'true'",
        "'`$(EnableM02OwnedIdtGatePopulationProbe)' == 'true' and '`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true'",
        "'`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true' and '`$(EnableM02IdtrContinuedExecutionProbe)' == 'true'",
        "'`$(EnableM02IdtrContinuedExecutionProbe)' == 'true' and '`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true'",
        "'`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true' and '`$(EnableM02Ud2VectorStateProbe)' == 'true'",
        "'`$(EnableM02Ud2VectorStateProbe)' == 'true' and '`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true'",
        "'`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true' and '`$(EnableM04IndependentEndianConformanceProbe)' == 'true'",
        "'`$(EnableM04IndependentEndianConformanceProbe)' == 'true' and '`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true'",
        "'`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true' and '`$(EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe)' == 'true'",
        "'`$(EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe)' == 'true' and '`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true'",
        "'`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true' and '`$(EnableM04ArithmeticConformanceProbe)' == 'true'",
        "'`$(EnableM04ArithmeticConformanceProbe)' == 'true' and '`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true'",
        "'`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true' and '`$(EnableM04LayoutContractProbe)' == 'true'",
        "'`$(EnableM04LayoutContractProbe)' == 'true' and '`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true'",
        "'`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true' and '`$(EnableM04ManagedHeapPageRangeProbe)' == 'true'",
        "'`$(EnableM04ManagedHeapPageRangeProbe)' == 'true' and '`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true'",
        "'`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true' and '`$(EnableUefiGoneSimulationProbe)' == 'true'",
        "'`$(EnableUefiGoneSimulationProbe)' == 'true' and '`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true'",
        "'`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true' and '`$(EnableExitBootServicesFreshMapProbe)' == 'true'",
        "'`$(EnableExitBootServicesFreshMapProbe)' == 'true' and '`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true'",
        "'`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true' and '`$(EnableAcpiPhysicalDiscoveryProbe)' == 'true'",
        "'`$(EnableAcpiPhysicalDiscoveryProbe)' == 'true' and '`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true'",
        "'`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true' and '`$(EnableM02PageFaultCr2ErrorCodeProbe)' == 'true'",
        "'`$(EnableM02PageFaultCr2ErrorCodeProbe)' == 'true' and '`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true'",
        "'`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true' and '`$(EnableM02DoubleFaultIstProbe)' == 'true'",
        "'`$(EnableM02DoubleFaultIstProbe)' == 'true' and '`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true'",
        "'`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true' and '`$(EnableM02UnexpectedVectorPanicProbe)' == 'true'",
        "'`$(EnableM02UnexpectedVectorPanicProbe)' == 'true' and '`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true'",
        "'`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true' and '`$(EnableM04RhpNewFastProbe)' == 'true'",
        "'`$(EnableM04RhpNewFastProbe)' == 'true' and '`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true'",
        "'`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true' and '`$(EnableM04RhpNewFastBeforeBindFailureProbe)' == 'true'",
        "'`$(EnableM04RhpNewFastBeforeBindFailureProbe)' == 'true' and '`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true'",
        "'`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true' and '`$(EnableM04RhpNewFastExhaustionFailureProbe)' == 'true'",
        "'`$(EnableM04RhpNewFastExhaustionFailureProbe)' == 'true' and '`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true'",
        "'`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true' and '`$(EnableM04SzArrayAllocationProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationProbe)' == 'true' and '`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true'",
        "'`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true' and '`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true' and '`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true'",
        "'`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true' and '`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true' and '`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true'",
        "'`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true' and '`$(EnableM04StringAllocationProbe)' == 'true'",
        "'`$(EnableM04StringAllocationProbe)' == 'true' and '`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true'",
        "'`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true' and '`$(EnableM04StringAllocationBeforeBindProbe)' == 'true'",
        "'`$(EnableM04StringAllocationBeforeBindProbe)' == 'true' and '`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true'",
        "'`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true' and '`$(EnableM04StringAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM04StringAllocationExhaustionProbe)' == 'true' and '`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true'",
        "'`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true' and '`$(EnableM04StringAllocationInvalidLengthProbe)' == 'true'",
        "'`$(EnableM04StringAllocationInvalidLengthProbe)' == 'true' and '`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true'",
        "'`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true' and '`$(EnableM03MemoryMapCaptureProbe)' == 'true'",
        "'`$(EnableM03MemoryMapCaptureProbe)' == 'true' and '`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true'",
        "'`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true' and '`$(EnableM04CheckedReferenceBarrierProbe)' == 'true'",
        "'`$(EnableM04CheckedReferenceBarrierProbe)' == 'true' and '`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true'",
        "'`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true' and '`$(EnableM03FirmwareEntropySeedProbe)' == 'true'",
        "'`$(EnableM03FirmwareEntropySeedProbe)' == 'true' and '`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true'",
        "'`$(EnableM03TypedExitBootServicesCallProbe)' != '' and '`$(EnableM03TypedExitBootServicesCallProbe)' != 'true' and '`$(EnableM03TypedExitBootServicesCallProbe)' != 'false'",
        "'`$(EnableM03TypedExitBootServicesCallProbe)' == 'true' and '`$(EnableM01LoadedImageStackProbe)' == 'true'",
        "'`$(EnableM01LoadedImageStackProbe)' == 'true' and '`$(EnableM03TypedExitBootServicesCallProbe)' == 'true'",
        "'`$(EnableM03TypedExitBootServicesCallProbe)' == 'true' and '`$(EnableM01LiveAllocatorProbe)' == 'true'",
        "'`$(EnableM01LiveAllocatorProbe)' == 'true' and '`$(EnableM03TypedExitBootServicesCallProbe)' == 'true'",
        "'`$(EnableM03TypedExitBootServicesCallProbe)' == 'true' and '`$(EnableM01OwnedPagingProbe)' == 'true'",
        "'`$(EnableM01OwnedPagingProbe)' == 'true' and '`$(EnableM03TypedExitBootServicesCallProbe)' == 'true'",
        "'`$(EnableM03TypedExitBootServicesCallProbe)' == 'true' and '`$(EnableM02SafeStubProbe)' == 'true'",
        "'`$(EnableM02SafeStubProbe)' == 'true' and '`$(EnableM03TypedExitBootServicesCallProbe)' == 'true'",
        "'`$(EnableM03TypedExitBootServicesCallProbe)' == 'true' and '`$(EnableM02CheckedPortIoBindingProbe)' == 'true'",
        "'`$(EnableM02CheckedPortIoBindingProbe)' == 'true' and '`$(EnableM03TypedExitBootServicesCallProbe)' == 'true'",
        "'`$(EnableM03TypedExitBootServicesCallProbe)' == 'true' and '`$(EnableM02Com1OwnedPortIoProbe)' == 'true'",
        "'`$(EnableM02Com1OwnedPortIoProbe)' == 'true' and '`$(EnableM03TypedExitBootServicesCallProbe)' == 'true'",
        "'`$(EnableM03TypedExitBootServicesCallProbe)' == 'true' and '`$(EnableM02OwnedCom1ReadyProbe)' == 'true'",
        "'`$(EnableM02OwnedCom1ReadyProbe)' == 'true' and '`$(EnableM03TypedExitBootServicesCallProbe)' == 'true'",
        "'`$(EnableM03TypedExitBootServicesCallProbe)' == 'true' and '`$(EnableM02PanicSafeOwnedCom1Probe)' == 'true'",
        "'`$(EnableM02PanicSafeOwnedCom1Probe)' == 'true' and '`$(EnableM03TypedExitBootServicesCallProbe)' == 'true'",
        "'`$(EnableM03TypedExitBootServicesCallProbe)' == 'true' and '`$(EnableM02OwnedX64GdtEncodingProbe)' == 'true'",
        "'`$(EnableM02OwnedX64GdtEncodingProbe)' == 'true' and '`$(EnableM03TypedExitBootServicesCallProbe)' == 'true'",
        "'`$(EnableM03TypedExitBootServicesCallProbe)' == 'true' and '`$(EnableM02GuardedIstStackAllocationProbe)' == 'true'",
        "'`$(EnableM02GuardedIstStackAllocationProbe)' == 'true' and '`$(EnableM03TypedExitBootServicesCallProbe)' == 'true'",
        "'`$(EnableM03TypedExitBootServicesCallProbe)' == 'true' and '`$(EnableM02OwnedGdtTssTerminalProbe)' == 'true'",
        "'`$(EnableM02OwnedGdtTssTerminalProbe)' == 'true' and '`$(EnableM03TypedExitBootServicesCallProbe)' == 'true'",
        "'`$(EnableM03TypedExitBootServicesCallProbe)' == 'true' and '`$(EnableM02NormalizedInterruptFramePageProbe)' == 'true'",
        "'`$(EnableM02NormalizedInterruptFramePageProbe)' == 'true' and '`$(EnableM03TypedExitBootServicesCallProbe)' == 'true'",
        "'`$(EnableM03TypedExitBootServicesCallProbe)' == 'true' and '`$(EnableM02OwnedIdtGatePopulationProbe)' == 'true'",
        "'`$(EnableM02OwnedIdtGatePopulationProbe)' == 'true' and '`$(EnableM03TypedExitBootServicesCallProbe)' == 'true'",
        "'`$(EnableM03TypedExitBootServicesCallProbe)' == 'true' and '`$(EnableM02IdtrContinuedExecutionProbe)' == 'true'",
        "'`$(EnableM02IdtrContinuedExecutionProbe)' == 'true' and '`$(EnableM03TypedExitBootServicesCallProbe)' == 'true'",
        "'`$(EnableM03TypedExitBootServicesCallProbe)' == 'true' and '`$(EnableM02Ud2VectorStateProbe)' == 'true'",
        "'`$(EnableM02Ud2VectorStateProbe)' == 'true' and '`$(EnableM03TypedExitBootServicesCallProbe)' == 'true'",
        "'`$(EnableM03TypedExitBootServicesCallProbe)' == 'true' and '`$(EnableM04IndependentEndianConformanceProbe)' == 'true'",
        "'`$(EnableM04IndependentEndianConformanceProbe)' == 'true' and '`$(EnableM03TypedExitBootServicesCallProbe)' == 'true'",
        "'`$(EnableM03TypedExitBootServicesCallProbe)' == 'true' and '`$(EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe)' == 'true'",
        "'`$(EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe)' == 'true' and '`$(EnableM03TypedExitBootServicesCallProbe)' == 'true'",
        "'`$(EnableM03TypedExitBootServicesCallProbe)' == 'true' and '`$(EnableM04ArithmeticConformanceProbe)' == 'true'",
        "'`$(EnableM04ArithmeticConformanceProbe)' == 'true' and '`$(EnableM03TypedExitBootServicesCallProbe)' == 'true'",
        "'`$(EnableM03TypedExitBootServicesCallProbe)' == 'true' and '`$(EnableM04LayoutContractProbe)' == 'true'",
        "'`$(EnableM04LayoutContractProbe)' == 'true' and '`$(EnableM03TypedExitBootServicesCallProbe)' == 'true'",
        "'`$(EnableM03TypedExitBootServicesCallProbe)' == 'true' and '`$(EnableM04ManagedHeapPageRangeProbe)' == 'true'",
        "'`$(EnableM04ManagedHeapPageRangeProbe)' == 'true' and '`$(EnableM03TypedExitBootServicesCallProbe)' == 'true'",
        "'`$(EnableM03TypedExitBootServicesCallProbe)' == 'true' and '`$(EnableM02PageFaultCr2ErrorCodeProbe)' == 'true'",
        "'`$(EnableM02PageFaultCr2ErrorCodeProbe)' == 'true' and '`$(EnableM03TypedExitBootServicesCallProbe)' == 'true'",
        "'`$(EnableM03TypedExitBootServicesCallProbe)' == 'true' and '`$(EnableM02DoubleFaultIstProbe)' == 'true'",
        "'`$(EnableM02DoubleFaultIstProbe)' == 'true' and '`$(EnableM03TypedExitBootServicesCallProbe)' == 'true'",
        "'`$(EnableM03TypedExitBootServicesCallProbe)' == 'true' and '`$(EnableM02UnexpectedVectorPanicProbe)' == 'true'",
        "'`$(EnableM02UnexpectedVectorPanicProbe)' == 'true' and '`$(EnableM03TypedExitBootServicesCallProbe)' == 'true'",
        "'`$(EnableM03TypedExitBootServicesCallProbe)' == 'true' and '`$(EnableM04RhpNewFastProbe)' == 'true'",
        "'`$(EnableM04RhpNewFastProbe)' == 'true' and '`$(EnableM03TypedExitBootServicesCallProbe)' == 'true'",
        "'`$(EnableM03TypedExitBootServicesCallProbe)' == 'true' and '`$(EnableM04RhpNewFastBeforeBindFailureProbe)' == 'true'",
        "'`$(EnableM04RhpNewFastBeforeBindFailureProbe)' == 'true' and '`$(EnableM03TypedExitBootServicesCallProbe)' == 'true'",
        "'`$(EnableM03TypedExitBootServicesCallProbe)' == 'true' and '`$(EnableM04RhpNewFastExhaustionFailureProbe)' == 'true'",
        "'`$(EnableM04RhpNewFastExhaustionFailureProbe)' == 'true' and '`$(EnableM03TypedExitBootServicesCallProbe)' == 'true'",
        "'`$(EnableM03TypedExitBootServicesCallProbe)' == 'true' and '`$(EnableM04SzArrayAllocationProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationProbe)' == 'true' and '`$(EnableM03TypedExitBootServicesCallProbe)' == 'true'",
        "'`$(EnableM03TypedExitBootServicesCallProbe)' == 'true' and '`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true' and '`$(EnableM03TypedExitBootServicesCallProbe)' == 'true'",
        "'`$(EnableM03TypedExitBootServicesCallProbe)' == 'true' and '`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true' and '`$(EnableM03TypedExitBootServicesCallProbe)' == 'true'",
        "'`$(EnableM03TypedExitBootServicesCallProbe)' == 'true' and '`$(EnableM04StringAllocationProbe)' == 'true'",
        "'`$(EnableM04StringAllocationProbe)' == 'true' and '`$(EnableM03TypedExitBootServicesCallProbe)' == 'true'",
        "'`$(EnableM03TypedExitBootServicesCallProbe)' == 'true' and '`$(EnableM04StringAllocationBeforeBindProbe)' == 'true'",
        "'`$(EnableM04StringAllocationBeforeBindProbe)' == 'true' and '`$(EnableM03TypedExitBootServicesCallProbe)' == 'true'",
        "'`$(EnableM03TypedExitBootServicesCallProbe)' == 'true' and '`$(EnableM04StringAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM04StringAllocationExhaustionProbe)' == 'true' and '`$(EnableM03TypedExitBootServicesCallProbe)' == 'true'",
        "'`$(EnableM03TypedExitBootServicesCallProbe)' == 'true' and '`$(EnableM04StringAllocationInvalidLengthProbe)' == 'true'",
        "'`$(EnableM04StringAllocationInvalidLengthProbe)' == 'true' and '`$(EnableM03TypedExitBootServicesCallProbe)' == 'true'",
        "'`$(EnableM03TypedExitBootServicesCallProbe)' == 'true' and '`$(EnableM03MemoryMapCaptureProbe)' == 'true'",
        "'`$(EnableM03MemoryMapCaptureProbe)' == 'true' and '`$(EnableM03TypedExitBootServicesCallProbe)' == 'true'",
        "'`$(EnableM03TypedExitBootServicesCallProbe)' == 'true' and '`$(EnableM04CheckedReferenceBarrierProbe)' == 'true'",
        "'`$(EnableM04CheckedReferenceBarrierProbe)' == 'true' and '`$(EnableM03TypedExitBootServicesCallProbe)' == 'true'",
        "'`$(EnableM03TypedExitBootServicesCallProbe)' == 'true' and '`$(EnableM03FirmwareEntropySeedProbe)' == 'true'",
        "'`$(EnableM03FirmwareEntropySeedProbe)' == 'true' and '`$(EnableM03TypedExitBootServicesCallProbe)' == 'true'",
        "'`$(EnableM03TypedExitBootServicesCallProbe)' == 'true' and '`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true'",
        "'`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true' and '`$(EnableM03TypedExitBootServicesCallProbe)' == 'true'",
        "'`$(EnableM03TypedExitBootServicesCallProbe)' == 'true' and '`$(EnableUefiGoneSimulationProbe)' == 'true'",
        "'`$(EnableUefiGoneSimulationProbe)' == 'true' and '`$(EnableM03TypedExitBootServicesCallProbe)' == 'true'",
        "'`$(EnableM03TypedExitBootServicesCallProbe)' == 'true' and '`$(EnableExitBootServicesFreshMapProbe)' == 'true'",
        "'`$(EnableExitBootServicesFreshMapProbe)' == 'true' and '`$(EnableM03TypedExitBootServicesCallProbe)' == 'true'",
        "'`$(EnableM03TypedExitBootServicesCallProbe)' == 'true' and '`$(EnableAcpiPhysicalDiscoveryProbe)' == 'true'",
        "'`$(EnableAcpiPhysicalDiscoveryProbe)' == 'true' and '`$(EnableM03TypedExitBootServicesCallProbe)' == 'true'"
    )
    $requiredErrorConditions += @(
        "'`$(EnableM01OwnedPagingProbe)' == 'true' and '`$(EnableM04GcStaticRootMaterializationProbe)' == 'true'",
        "'`$(EnableM02SafeStubProbe)' == 'true' and '`$(EnableM04GcStaticRootMaterializationProbe)' == 'true'",
        "'`$(EnableM02CheckedPortIoBindingProbe)' == 'true' and '`$(EnableM04GcStaticRootMaterializationProbe)' == 'true'",
        "'`$(EnableM02Com1OwnedPortIoProbe)' == 'true' and '`$(EnableM04GcStaticRootMaterializationProbe)' == 'true'",
        "'`$(EnableM02OwnedCom1ReadyProbe)' == 'true' and '`$(EnableM04GcStaticRootMaterializationProbe)' == 'true'",
        "'`$(EnableM02PanicSafeOwnedCom1Probe)' == 'true' and '`$(EnableM04GcStaticRootMaterializationProbe)' == 'true'",
        "'`$(EnableM02OwnedX64GdtEncodingProbe)' == 'true' and '`$(EnableM04GcStaticRootMaterializationProbe)' == 'true'",
        "'`$(EnableM02GuardedIstStackAllocationProbe)' == 'true' and '`$(EnableM04GcStaticRootMaterializationProbe)' == 'true'",
        "'`$(EnableM02OwnedGdtTssTerminalProbe)' == 'true' and '`$(EnableM04GcStaticRootMaterializationProbe)' == 'true'",
        "'`$(EnableM02NormalizedInterruptFramePageProbe)' == 'true' and '`$(EnableM04GcStaticRootMaterializationProbe)' == 'true'",
        "'`$(EnableM02OwnedIdtGatePopulationProbe)' == 'true' and '`$(EnableM04GcStaticRootMaterializationProbe)' == 'true'",
        "'`$(EnableM02IdtrContinuedExecutionProbe)' == 'true' and '`$(EnableM04GcStaticRootMaterializationProbe)' == 'true'",
        "'`$(EnableM02Ud2VectorStateProbe)' == 'true' and '`$(EnableM04GcStaticRootMaterializationProbe)' == 'true'",
        "'`$(EnableM02PageFaultCr2ErrorCodeProbe)' == 'true' and '`$(EnableM04GcStaticRootMaterializationProbe)' == 'true'",
        "'`$(EnableM02DoubleFaultIstProbe)' == 'true' and '`$(EnableM04GcStaticRootMaterializationProbe)' == 'true'",
        "'`$(EnableM02UnexpectedVectorPanicProbe)' == 'true' and '`$(EnableM04GcStaticRootMaterializationProbe)' == 'true'",
        "'`$(EnableM04RhpNewFastBeforeBindFailureProbe)' == 'true' and '`$(EnableM04GcStaticRootMaterializationProbe)' == 'true'",
        "'`$(EnableM04RhpNewFastExhaustionFailureProbe)' == 'true' and '`$(EnableM04GcStaticRootMaterializationProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationProbe)' == 'true' and '`$(EnableM04GcStaticRootMaterializationProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true' and '`$(EnableM04GcStaticRootMaterializationProbe)' == 'true'",
        "'`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true' and '`$(EnableM04GcStaticRootMaterializationProbe)' == 'true'",
        "'`$(EnableM04StringAllocationProbe)' == 'true' and '`$(EnableM04GcStaticRootMaterializationProbe)' == 'true'",
        "'`$(EnableM04StringAllocationBeforeBindProbe)' == 'true' and '`$(EnableM04GcStaticRootMaterializationProbe)' == 'true'",
        "'`$(EnableM04StringAllocationExhaustionProbe)' == 'true' and '`$(EnableM04GcStaticRootMaterializationProbe)' == 'true'",
        "'`$(EnableM04StringAllocationInvalidLengthProbe)' == 'true' and '`$(EnableM04GcStaticRootMaterializationProbe)' == 'true'",
        "'`$(EnableM03MemoryMapCaptureProbe)' == 'true' and '`$(EnableM04GcStaticRootMaterializationProbe)' == 'true'",
        "'`$(EnableM04CheckedReferenceBarrierProbe)' == 'true' and '`$(EnableM04GcStaticRootMaterializationProbe)' == 'true'",
        "'`$(EnableM03FirmwareEntropySeedProbe)' == 'true' and '`$(EnableM04GcStaticRootMaterializationProbe)' == 'true'",
        "'`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true' and '`$(EnableM04GcStaticRootMaterializationProbe)' == 'true'",
        "'`$(EnableM03TypedExitBootServicesCallProbe)' == 'true' and '`$(EnableM04GcStaticRootMaterializationProbe)' == 'true'",
        "'`$(EnableUefiGoneSimulationProbe)' == 'true' and '`$(EnableM04GcStaticRootMaterializationProbe)' == 'true'",
        "'`$(EnableExitBootServicesFreshMapProbe)' == 'true' and '`$(EnableM04GcStaticRootMaterializationProbe)' == 'true'",
        "'`$(EnableAcpiPhysicalDiscoveryProbe)' == 'true' and '`$(EnableM04GcStaticRootMaterializationProbe)' == 'true'"
    )
    $requiredErrorConditions += @(
        "'`$(EnableM03FirstRealExitBootServicesProbe)' != '' and '`$(EnableM03FirstRealExitBootServicesProbe)' != 'true' and '`$(EnableM03FirstRealExitBootServicesProbe)' != 'false'",
        "'`$(EnableM03FirstRealExitBootServicesProbe)' == 'true' and '`$(EnableM01LoadedImageStackProbe)' != 'true'",
        "'`$(EnableM03FirstRealExitBootServicesProbe)' == 'true' and '`$(EnableM01LiveAllocatorProbe)' != 'true'",
        "'`$(EnableM03FirstRealExitBootServicesProbe)' == 'true' and '`$(EnableAcpiPhysicalDiscoveryProbe)' != 'true'",
        "'`$(EnableM03FirstRealExitBootServicesProbe)' == 'true' and '`$(EnableM01OwnedPagingProbe)' == 'true'",
        "'`$(EnableM03FirstRealExitBootServicesProbe)' == 'true' and '`$(EnableM02SafeStubProbe)' == 'true'",
        "'`$(EnableM03FirstRealExitBootServicesProbe)' == 'true' and '`$(EnableM02CheckedPortIoBindingProbe)' == 'true'",
        "'`$(EnableM03FirstRealExitBootServicesProbe)' == 'true' and '`$(EnableM02Com1OwnedPortIoProbe)' == 'true'",
        "'`$(EnableM03FirstRealExitBootServicesProbe)' == 'true' and '`$(EnableM02OwnedCom1ReadyProbe)' == 'true'",
        "'`$(EnableM03FirstRealExitBootServicesProbe)' == 'true' and '`$(EnableM02PanicSafeOwnedCom1Probe)' == 'true'",
        "'`$(EnableM03FirstRealExitBootServicesProbe)' == 'true' and '`$(EnableM02OwnedX64GdtEncodingProbe)' == 'true'",
        "'`$(EnableM03FirstRealExitBootServicesProbe)' == 'true' and '`$(EnableM02GuardedIstStackAllocationProbe)' == 'true'",
        "'`$(EnableM03FirstRealExitBootServicesProbe)' == 'true' and '`$(EnableM02OwnedGdtTssTerminalProbe)' == 'true'",
        "'`$(EnableM03FirstRealExitBootServicesProbe)' == 'true' and '`$(EnableM02NormalizedInterruptFramePageProbe)' == 'true'",
        "'`$(EnableM03FirstRealExitBootServicesProbe)' == 'true' and '`$(EnableM02OwnedIdtGatePopulationProbe)' == 'true'",
        "'`$(EnableM03FirstRealExitBootServicesProbe)' == 'true' and '`$(EnableM02IdtrContinuedExecutionProbe)' == 'true'",
        "'`$(EnableM03FirstRealExitBootServicesProbe)' == 'true' and '`$(EnableM02Ud2VectorStateProbe)' == 'true'",
        "'`$(EnableM03FirstRealExitBootServicesProbe)' == 'true' and '`$(EnableM04IndependentEndianConformanceProbe)' == 'true'",
        "'`$(EnableM03FirstRealExitBootServicesProbe)' == 'true' and '`$(EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe)' == 'true'",
        "'`$(EnableM03FirstRealExitBootServicesProbe)' == 'true' and '`$(EnableM04ArithmeticConformanceProbe)' == 'true'",
        "'`$(EnableM03FirstRealExitBootServicesProbe)' == 'true' and '`$(EnableM04LayoutContractProbe)' == 'true'",
        "'`$(EnableM03FirstRealExitBootServicesProbe)' == 'true' and '`$(EnableM04ManagedHeapPageRangeProbe)' == 'true'",
        "'`$(EnableM03FirstRealExitBootServicesProbe)' == 'true' and '`$(EnableM02PageFaultCr2ErrorCodeProbe)' == 'true'",
        "'`$(EnableM03FirstRealExitBootServicesProbe)' == 'true' and '`$(EnableM02DoubleFaultIstProbe)' == 'true'",
        "'`$(EnableM03FirstRealExitBootServicesProbe)' == 'true' and '`$(EnableM02UnexpectedVectorPanicProbe)' == 'true'",
        "'`$(EnableM03FirstRealExitBootServicesProbe)' == 'true' and '`$(EnableM04RhpNewFastProbe)' == 'true'",
        "'`$(EnableM03FirstRealExitBootServicesProbe)' == 'true' and '`$(EnableM04RhpNewFastBeforeBindFailureProbe)' == 'true'",
        "'`$(EnableM03FirstRealExitBootServicesProbe)' == 'true' and '`$(EnableM04RhpNewFastExhaustionFailureProbe)' == 'true'",
        "'`$(EnableM03FirstRealExitBootServicesProbe)' == 'true' and '`$(EnableM04SzArrayAllocationProbe)' == 'true'",
        "'`$(EnableM03FirstRealExitBootServicesProbe)' == 'true' and '`$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true'",
        "'`$(EnableM03FirstRealExitBootServicesProbe)' == 'true' and '`$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM03FirstRealExitBootServicesProbe)' == 'true' and '`$(EnableM04StringAllocationProbe)' == 'true'",
        "'`$(EnableM03FirstRealExitBootServicesProbe)' == 'true' and '`$(EnableM04StringAllocationBeforeBindProbe)' == 'true'",
        "'`$(EnableM03FirstRealExitBootServicesProbe)' == 'true' and '`$(EnableM04StringAllocationExhaustionProbe)' == 'true'",
        "'`$(EnableM03FirstRealExitBootServicesProbe)' == 'true' and '`$(EnableM04StringAllocationInvalidLengthProbe)' == 'true'",
        "'`$(EnableM03FirstRealExitBootServicesProbe)' == 'true' and '`$(EnableM03MemoryMapCaptureProbe)' == 'true'",
        "'`$(EnableM03FirstRealExitBootServicesProbe)' == 'true' and '`$(EnableM04CheckedReferenceBarrierProbe)' == 'true'",
        "'`$(EnableM03FirstRealExitBootServicesProbe)' == 'true' and '`$(EnableM03FirmwareEntropySeedProbe)' == 'true'",
        "'`$(EnableM03FirstRealExitBootServicesProbe)' == 'true' and '`$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true'",
        "'`$(EnableM03FirstRealExitBootServicesProbe)' == 'true' and '`$(EnableM03TypedExitBootServicesCallProbe)' == 'true'",
        "'`$(EnableM03FirstRealExitBootServicesProbe)' == 'true' and '`$(EnableM04GcStaticRootMaterializationProbe)' == 'true'",
        "'`$(EnableM03FirstRealExitBootServicesProbe)' == 'true' and '`$(EnableUefiGoneSimulationProbe)' == 'true'",
        "'`$(EnableM03FirstRealExitBootServicesProbe)' == 'true' and '`$(EnableExitBootServicesFreshMapProbe)' == 'true'"
    )
    $requiredErrorConditions += @(
        "'`$(EnableM04SeededCollectorChurnProbe)' != '' and '`$(EnableM04SeededCollectorChurnProbe)' != 'true' and '`$(EnableM04SeededCollectorChurnProbe)' != 'false'",
        "'`$(EnableM04SeededCollectorChurnProbe)' == 'true' and '`$(EnableM04GcStaticRootMaterializationProbe)' != 'true'"
    )
    $requiredErrorConditions += @(
        "'`$(EnableM03FirmwareAdapterRevocationProbe)' != '' and '`$(EnableM03FirmwareAdapterRevocationProbe)' != 'true' and '`$(EnableM03FirmwareAdapterRevocationProbe)' != 'false'",
        "'`$(EnableM04ClassConstructorExecutionProbe)' != '' and '`$(EnableM04ClassConstructorExecutionProbe)' != 'true' and '`$(EnableM04ClassConstructorExecutionProbe)' != 'false'",
        "'`$(EnableM03FirmwareAdapterRevocationProbe)' == 'true' and '`$(EnableM03FirstRealExitBootServicesProbe)' != 'true'",
        "'`$(EnableM03OwnedSubsystemSurvivalProbe)' != '' and '`$(EnableM03OwnedSubsystemSurvivalProbe)' != 'true' and '`$(EnableM03OwnedSubsystemSurvivalProbe)' != 'false'",
        "'`$(EnableM03OwnedSubsystemPanicProbe)' != '' and '`$(EnableM03OwnedSubsystemPanicProbe)' != 'true' and '`$(EnableM03OwnedSubsystemPanicProbe)' != 'false'",
        "'`$(EnableM03OwnedSubsystemSurvivalProbe)' == 'true' and '`$(EnableM03FirstRealExitBootServicesProbe)' != 'true'",
        "'`$(EnableM03OwnedSubsystemPanicProbe)' == 'true' and '`$(EnableM03OwnedSubsystemSurvivalProbe)' != 'true'",
        "'`$(EnableM03OwnedSubsystemSurvivalProbe)' == 'true' and '`$(EnableM03FirmwareAdapterRevocationProbe)' == 'true'",
        "'`$(EnableM04ClassConstructorExecutionProbe)' == 'true' and '`$(EnableM04GcStaticRootMaterializationProbe)' != 'true'",
        "'`$(EnableM04ClassConstructorExecutionProbe)' == 'true' and '`$(EnableM04SeededCollectorChurnProbe)' == 'true'",
        "'`$(EnableM04InterfaceDispatchProbe)' != '' and '`$(EnableM04InterfaceDispatchProbe)' != 'true' and '`$(EnableM04InterfaceDispatchProbe)' != 'false'",
        "'`$(EnableM04InterfaceDispatchProbe)' == 'true' and '`$(EnableM04ClassConstructorExecutionProbe)' != 'true'"
    )
    $requiredErrorConditions += @(
        "'`$(EnableM04X64UnwindMetadataProbe)' != '' and '`$(EnableM04X64UnwindMetadataProbe)' != 'true' and '`$(EnableM04X64UnwindMetadataProbe)' != 'false'",
        "'`$(EnableM04X64UnwindMetadataProbe)' == 'true' and '`$(EnableM04TypeManagerModuleRegistrationProbe)' != 'true'"
    )
    $requiredErrorConditions += @(
        "'`$(EnableM04SimpleThrowCatchProbe)' != '' and '`$(EnableM04SimpleThrowCatchProbe)' != 'true' and '`$(EnableM04SimpleThrowCatchProbe)' != 'false'",
        "'`$(EnableM04SimpleThrowCatchProbe)' == 'true' and '`$(EnableM04GcStaticRootMaterializationProbe)' != 'true'",
        "'`$(EnableM04SimpleThrowCatchProbe)' == 'true' and ('`$(EnableM04SeededCollectorChurnProbe)' == 'true' or '`$(EnableM04ClassConstructorExecutionProbe)' == 'true' or '`$(EnableM04InterfaceDispatchProbe)' == 'true' or '`$(EnableM04X64UnwindMetadataProbe)' == 'true')"
    )
    if ($errorConditions.Count -ne $requiredErrorConditions.Count -or
        $validationTargets[0].SelectNodes("*").Count -ne $errorNodes.Count) {
        return New-M01SourceClosureResult `
            -Succeeded $false `
            -Reason "switch_dependency_validation_missing" `
            -Detail "count"
    }
    foreach ($condition in $requiredErrorConditions) {
        if ($errorConditions -notcontains $condition) {
            return New-M01SourceClosureResult `
                -Succeeded $false `
                -Reason "switch_dependency_validation_missing" `
                -Detail $condition
        }
    }
    foreach ($errorNode in $errorNodes) {
        if ($errorNode.Attributes.Count -ne 2 -or
            -not $errorNode.HasAttribute("Condition") -or
            -not $errorNode.HasAttribute("Text") -or
            $errorNode.SelectNodes("*").Count -ne 0) {
            return New-M01SourceClosureResult `
                -Succeeded $false `
                -Reason "switch_dependency_validation_missing" `
                -Detail "error-shape"
        }
    }

    $sourceInjectionNodes = @($project.SelectNodes(
        "//UsingTask|//Analyzer|//AdditionalFiles|//Csc|//Output"))
    if ($sourceInjectionNodes.Count -ne 0) {
        return New-M01SourceClosureResult `
            -Succeeded $false `
            -Reason "forbidden_project_input" `
            -Detail "task-or-analyzer"
    }

    $packageReferences = @($project.SelectNodes(
        "/Project/ItemGroup/PackageReference"))
    $expectedPackages = [ordered] @{
        "Microsoft.DotNet.ILCompiler" = "10.0.8"
        "runtime.win-x64.Microsoft.DotNet.ILCompiler" = "10.0.8"
    }
    if ($packageReferences.Count -ne $expectedPackages.Count) {
        return New-M01SourceClosureResult `
            -Succeeded $false `
            -Reason "package_reference_shape_invalid" `
            -Detail $packageReferences.Count
    }
    $seenPackageIds = New-Object 'System.Collections.Generic.HashSet[string]' (
        [System.StringComparer]::Ordinal)
    foreach ($packageReference in $packageReferences) {
        $packageId = $packageReference.GetAttribute("Include")
        if (-not $expectedPackages.Contains($packageId) -or
            -not $seenPackageIds.Add($packageId) -or
            $packageReference.GetAttribute("Version") -ne
                $expectedPackages[$packageId] -or
            $packageReference.GetAttribute("PrivateAssets") -ne "all" -or
            $packageReference.Attributes.Count -ne 3 -or
            $packageReference.SelectNodes("*").Count -ne 0) {
            return New-M01SourceClosureResult `
                -Succeeded $false `
                -Reason "package_reference_shape_invalid" `
                -Detail $packageId
        }
    }

    if (($project.SelectNodes("//ProjectReference").Count -ne 0) -or
        ($project.SelectNodes("//ImportGroup").Count -ne 0) -or
        ($project.SelectNodes("//Choose").Count -ne 0)) {
        return New-M01SourceClosureResult `
            -Succeeded $false `
            -Reason "forbidden_project_input" `
            -Detail "reference-importgroup-or-choose"
    }

    $compileItems = @($project.SelectNodes("/Project/ItemGroup/Compile"))
    $allCompileItems = @($project.SelectNodes("//Compile"))
    if ($allCompileItems.Count -ne $compileItems.Count) {
        return New-M01SourceClosureResult `
            -Succeeded $false `
            -Reason "forbidden_generated_compile_item" `
            -Detail ($allCompileItems.Count.ToString()) `
            -CompileItemCount $compileItems.Count
    }

    if ($compileItems.Count -ne $script:ExpectedEfiCompileItems.Count) {
        return New-M01SourceClosureResult `
            -Succeeded $false `
            -Reason "compile_item_count_mismatch" `
            -Detail ($compileItems.Count.ToString()) `
            -CompileItemCount $compileItems.Count
    }

    $seenItems = New-Object 'System.Collections.Generic.HashSet[string]' (
        [System.StringComparer]::OrdinalIgnoreCase)
    $seenIncludes = New-Object 'System.Collections.Generic.HashSet[string]' (
        [System.StringComparer]::OrdinalIgnoreCase)
    $seenLinks = New-Object 'System.Collections.Generic.HashSet[string]' (
        [System.StringComparer]::OrdinalIgnoreCase)
    $projectDirectory = Split-Path -Parent $canonicalProjectPath
    $repositoryPrefix = [System.IO.Path]::GetFullPath($RepositoryRoot)
    if (-not $repositoryPrefix.EndsWith(
            [System.IO.Path]::DirectorySeparatorChar.ToString(),
            [System.StringComparison]::Ordinal)) {
        $repositoryPrefix += [System.IO.Path]::DirectorySeparatorChar
    }
    $sourceTextByInclude = @{}
    $activeCodeByInclude = @{}

    for ($index = 0; $index -lt $compileItems.Count; $index++) {
        $compile = $compileItems[$index]
        $include = $compile.GetAttribute("Include")
        $link = $compile.GetAttribute("Link")
        $actualKey = $include + "|" + $link
        $expectedKey = $script:ExpectedEfiCompileItems[$index]
        $expectedHasLink = -not $expectedKey.EndsWith(
            "|",
            [System.StringComparison]::Ordinal)
        $expectedAttributeCount = if ($expectedHasLink) { 2 } else { 1 }
        if ($compile.Attributes.Count -ne $expectedAttributeCount -or
            -not $compile.HasAttribute("Include") -or
            ($expectedHasLink -and -not $compile.HasAttribute("Link")) -or
            (-not $expectedHasLink -and $compile.HasAttribute("Link")) -or
            $compile.SelectNodes("*").Count -ne 0) {
            return New-M01SourceClosureResult `
                -Succeeded $false `
                -Reason "compile_item_shape_invalid" `
                -Detail $actualKey `
                -CompileItemCount $compileItems.Count
        }

        $hasExistsCondition = $projectText.IndexOf(
                "Exists(",
                [System.StringComparison]::OrdinalIgnoreCase) -ge 0
        if (($include -match '[*?\[\];]') -or
            ($include -match '\$\(') -or
            ($include -match '@\(') -or
            $compile.HasAttribute("Remove") -or
            $compile.HasAttribute("Update") -or
            $compile.HasAttribute("Condition") -or
            $compile.ParentNode.HasAttribute("Condition") -or
            $hasExistsCondition) {
            return New-M01SourceClosureResult `
                -Succeeded $false `
                -Reason "conditional_or_glob_compile_item" `
                -Detail $include `
                -CompileItemCount $compileItems.Count
        }

        if (-not $seenItems.Add($actualKey)) {
            return New-M01SourceClosureResult `
                -Succeeded $false `
                -Reason "duplicate_compile_item" `
                -Detail $actualKey `
                -CompileItemCount $compileItems.Count
        }
        if (-not $seenIncludes.Add($include)) {
            return New-M01SourceClosureResult `
                -Succeeded $false `
                -Reason "duplicate_compile_include" `
                -Detail $include `
                -CompileItemCount $compileItems.Count
        }
        if (-not [string]::IsNullOrWhiteSpace($link) -and
            -not $seenLinks.Add($link)) {
            return New-M01SourceClosureResult `
                -Succeeded $false `
                -Reason "duplicate_compile_link" `
                -Detail $link `
                -CompileItemCount $compileItems.Count
        }

        if (-not [string]::Equals(
                $actualKey,
                $expectedKey,
                [System.StringComparison]::Ordinal)) {
            return New-M01SourceClosureResult `
                -Succeeded $false `
                -Reason "compile_item_mismatch" `
                -Detail ($index.ToString() + ":" + $actualKey) `
                -CompileItemCount $compileItems.Count
        }

        $sourcePath = [System.IO.Path]::GetFullPath(
            (Join-Path $projectDirectory $include))
        if (-not $sourcePath.StartsWith(
                $repositoryPrefix,
                [System.StringComparison]::OrdinalIgnoreCase)) {
            return New-M01SourceClosureResult `
                -Succeeded $false `
                -Reason "source_outside_repository" `
                -Detail $include `
                -CompileItemCount $compileItems.Count
        }
        if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
            return New-M01SourceClosureResult `
                -Succeeded $false `
                -Reason "source_missing" `
                -Detail $include `
                -CompileItemCount $compileItems.Count
        }

        if ($include.StartsWith(
                "..\OS.Kernel\",
                [System.StringComparison]::Ordinal)) {
            $sourceText = Get-Content -LiteralPath $sourcePath -Raw
            $sourceTextByInclude[$include] = $sourceText
            $surface = Test-M01ActiveSourceSurface `
                -SourceText $sourceText `
                -SourceName $include
            if (-not $surface.Succeeded) {
                return New-M01SourceClosureResult `
                    -Succeeded $false `
                    -Reason $surface.Reason `
                    -Detail $surface.Detail `
                    -CompileItemCount $compileItems.Count
            }
            $activeCodeByInclude[$include] = $surface.ActiveText
        }
    }

    foreach ($entry in $script:M01NostdlibExclusionCounts.GetEnumerator()) {
        if (-not $sourceTextByInclude.ContainsKey($entry.Key)) {
            return New-M01SourceClosureResult `
                -Succeeded $false `
                -Reason "source_missing" `
                -Detail $entry.Key `
                -CompileItemCount $compileItems.Count
        }

        $guardCount = [System.Text.RegularExpressions.Regex]::Matches(
            $sourceTextByInclude[$entry.Key],
            '(?m)^\s*#if\s+!NOSTDLIB_EFI\s*$').Count
        if ($guardCount -ne [int] $entry.Value) {
            return New-M01SourceClosureResult `
                -Succeeded $false `
                -Reason "nostdlib_efi_exclusion_mismatch" `
                -Detail ($entry.Key + ":" + $guardCount) `
                -CompileItemCount $compileItems.Count
        }
    }

    foreach ($entry in $script:M01SourceContractTokens.GetEnumerator()) {
        if (-not $activeCodeByInclude.ContainsKey($entry.Key)) {
            return New-M01SourceClosureResult `
                -Succeeded $false `
                -Reason "source_missing" `
                -Detail $entry.Key `
                -CompileItemCount $compileItems.Count
        }

        foreach ($token in $entry.Value) {
            if ($activeCodeByInclude[$entry.Key].IndexOf(
                    $token,
                    [System.StringComparison]::Ordinal) -lt 0) {
                return New-M01SourceClosureResult `
                    -Succeeded $false `
                    -Reason "source_contract_missing" `
                    -Detail ($entry.Key + ":" + $token) `
                    -CompileItemCount $compileItems.Count
            }
        }
    }

    foreach ($include in $script:ExpectedM01ReviewedSourceIncludes) {
        if (-not $activeCodeByInclude.ContainsKey($include)) {
            return New-M01SourceClosureResult `
                -Succeeded $false `
                -Reason "reviewed_source_missing" `
                -Detail $include `
                -CompileItemCount $compileItems.Count
        }
    }

    foreach ($include in $script:ExpectedM01PrerequisiteSourceIncludes) {
        if (-not $activeCodeByInclude.ContainsKey($include)) {
            return New-M01SourceClosureResult `
                -Succeeded $false `
                -Reason "prerequisite_source_missing" `
                -Detail $include `
                -CompileItemCount $compileItems.Count
        }
    }

    $customizationSurface = Test-M01BuildCustomizationSurface `
        -RepositoryRoot $RepositoryRoot `
        -ProjectPath $canonicalProjectPath
    if (-not $customizationSurface.Succeeded) {
        return New-M01SourceClosureResult `
            -Succeeded $false `
            -Reason $customizationSurface.Reason `
            -Detail $customizationSurface.Detail `
            -CompileItemCount $compileItems.Count
    }

    $evaluatedClosure = Test-M01EvaluatedSourceClosure `
        -RepositoryRoot $RepositoryRoot `
        -ProjectPath $canonicalProjectPath `
        -Configuration $Configuration `
        -LoadedImageStackEnabled $LoadedImageStackEnabled `
        -LiveAllocatorEnabled $LiveAllocatorEnabled `
        -OwnedPagingEnabled $OwnedPagingEnabled `
        -M02SafeStubEnabled $M02SafeStubEnabled `
        -M02CheckedPortIoBindingEnabled $M02CheckedPortIoBindingEnabled `
        -M02Com1OwnedPortIoEnabled $M02Com1OwnedPortIoEnabled `
        -M02OwnedCom1ReadyEnabled $M02OwnedCom1ReadyEnabled `
        -M02PanicSafeOwnedCom1Enabled $M02PanicSafeOwnedCom1Enabled `
        -M02OwnedX64GdtEncodingEnabled $M02OwnedX64GdtEncodingEnabled `
        -M02GuardedIstStackAllocationEnabled $M02GuardedIstStackAllocationEnabled `
        -M02OwnedGdtTssTerminalEnabled $M02OwnedGdtTssTerminalEnabled `
        -M02NormalizedInterruptFramePageEnabled $M02NormalizedInterruptFramePageEnabled `
        -M02OwnedIdtGatePopulationEnabled $M02OwnedIdtGatePopulationEnabled `
        -M02IdtrContinuedExecutionEnabled $M02IdtrContinuedExecutionEnabled `
        -M02Ud2VectorStateEnabled $M02Ud2VectorStateEnabled `
        -M02PageFaultCr2ErrorCodeEnabled $M02PageFaultCr2ErrorCodeEnabled `
        -M02DoubleFaultIstEnabled $M02DoubleFaultIstEnabled `
        -M02UnexpectedVectorPanicEnabled $M02UnexpectedVectorPanicEnabled `
        -M04IndependentEndianConformanceEnabled $M04IndependentEndianConformanceEnabled `
        -M04SpanMemoryConformanceEnabled $M04SpanMemoryConformanceEnabled `
        -M04ArithmeticConformanceEnabled $M04ArithmeticConformanceEnabled `
        -M04LayoutContractEnabled $M04LayoutContractEnabled `
        -M04ManagedHeapPageRangeEnabled $M04ManagedHeapPageRangeEnabled `
        -M04RhpNewFastEnabled $M04RhpNewFastEnabled `
        -M04RhpNewFastBeforeBindFailureEnabled $M04RhpNewFastBeforeBindFailureEnabled `
        -M04RhpNewFastExhaustionFailureEnabled $M04RhpNewFastExhaustionFailureEnabled `
        -M04SzArrayAllocationEnabled $M04SzArrayAllocationEnabled `
        -M04SzArrayAllocationBeforeBindEnabled $M04SzArrayAllocationBeforeBindEnabled `
        -M04SzArrayAllocationExhaustionEnabled $M04SzArrayAllocationExhaustionEnabled `
        -M04StringAllocationEnabled $M04StringAllocationEnabled `
        -M04StringAllocationBeforeBindEnabled $M04StringAllocationBeforeBindEnabled `
        -M04StringAllocationExhaustionEnabled $M04StringAllocationExhaustionEnabled `
        -M04StringAllocationInvalidLengthEnabled $M04StringAllocationInvalidLengthEnabled `
        -M03MemoryMapCaptureEnabled $M03MemoryMapCaptureEnabled `
        -M03FirmwareEntropySeedEnabled $M03FirmwareEntropySeedEnabled `
        -M04TypeManagerModuleRegistrationEnabled $M04TypeManagerModuleRegistrationEnabled `
        -M04X64UnwindMetadataEnabled $M04X64UnwindMetadataEnabled `
        -M04SimpleThrowCatchEnabled $M04SimpleThrowCatchEnabled `
        -M04GcStaticRootMaterializationEnabled $M04GcStaticRootMaterializationEnabled `
        -M04SeededCollectorChurnEnabled $M04SeededCollectorChurnEnabled `
        -M03TypedExitBootServicesCallEnabled $M03TypedExitBootServicesCallEnabled `
        -M03FirstRealExitBootServicesEnabled $M03FirstRealExitBootServicesEnabled `
        -M03FirmwareAdapterRevocationEnabled $M03FirmwareAdapterRevocationEnabled `
        -M03OwnedSubsystemSurvivalEnabled $M03OwnedSubsystemSurvivalEnabled `
        -M03OwnedSubsystemPanicEnabled $M03OwnedSubsystemPanicEnabled `
        -M04ClassConstructorExecutionEnabled $M04ClassConstructorExecutionEnabled `
        -M04InterfaceDispatchEnabled $M04InterfaceDispatchEnabled `
        -M04CheckedReferenceBarrierEnabled $M04CheckedReferenceBarrierEnabled `
        -UefiGoneSimulationEnabled $UefiGoneSimulationEnabled `
        -ExitBootServicesFreshMapEnabled $ExitBootServicesFreshMapEnabled `
        -AcpiPhysicalDiscoveryEnabled $AcpiPhysicalDiscoveryEnabled
    if (-not $evaluatedClosure.Succeeded) {
        return $evaluatedClosure
    }

    return New-M01SourceClosureResult `
        -Succeeded $true `
        -Reason "ok" `
        -Detail "" `
        -CompileItemCount $compileItems.Count `
        -ReviewedSourceCount $script:ExpectedM01ReviewedSourceIncludes.Count `
        -PrerequisiteSourceCount $script:ExpectedM01PrerequisiteSourceIncludes.Count
}

function Format-EvidenceValue {
    param([string] $Value)

    if ($null -eq $Value) {
        return '""'
    }

    $safeValue = $Value.Replace("`r", " ").Replace("`n", " ").Replace('"', '""')
    return '"' + $safeValue + '"'
}

function Stop-EfiPreflight {
    param(
        [string] $Marker,
        [string] $Reason,
        [string] $LogFullPath,
        [string] $ReportedLogPath,
        [string] $Status = "missing"
    )

    Write-EfiPreflightLine ($Marker + " " + $Status)
    Save-EfiPreflightLog -Path $LogFullPath
    Write-Host ("TEST_FAIL efi.shell.publish " + $Reason + " log=" + $ReportedLogPath)
    exit 1
}

function Get-DefaultMsvcLinkerSearchRoot {
    param([string] $VsWhere)

    $roots = New-Object System.Collections.Generic.List[string]

    if (-not [string]::IsNullOrWhiteSpace($VsWhere) -and (Test-Path -LiteralPath $VsWhere)) {
        try {
            $installationPaths = & $VsWhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath 2>$null
            foreach ($installationPath in $installationPaths) {
                if (-not [string]::IsNullOrWhiteSpace($installationPath)) {
                    $roots.Add((Join-Path $installationPath.Trim() "VC\Tools\MSVC"))
                }
            }
        }
        catch {
            # vswhere discovery is best-effort; fall back to well-known install roots below.
        }
    }

    $programFilesX86 = ${env:ProgramFiles(x86)}
    if (-not [string]::IsNullOrWhiteSpace($programFilesX86)) {
        foreach ($edition in @("BuildTools", "Community", "Professional", "Enterprise")) {
            $roots.Add((Join-Path $programFilesX86 "Microsoft Visual Studio\2022\$edition\VC\Tools\MSVC"))
        }
    }

    return $roots
}

function Get-DefaultWindowsSdkLibSearchRoot {
    $roots = New-Object System.Collections.Generic.List[string]

    foreach ($base in @(${env:ProgramFiles(x86)}, $env:ProgramFiles)) {
        if (-not [string]::IsNullOrWhiteSpace($base)) {
            $roots.Add((Join-Path $base "Windows Kits\10\Lib"))
        }
    }

    return $roots
}

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).ProviderPath
$efiProjectPath = Join-Path $repositoryRoot "src\OS\OS.csproj"

if ([System.IO.Path]::IsPathRooted($LogPath)) {
    $logFullPath = [System.IO.Path]::GetFullPath($LogPath)
}
else {
    $logFullPath = [System.IO.Path]::GetFullPath(
        (Join-Path $repositoryRoot $LogPath))
}
$outputPublicationPlan = $null
$script:M01LogAuthority = $null
try {
    $outputPublicationPlan = Get-M01OutputPublicationPlan `
        -RepositoryRoot $repositoryRoot `
        -EfiOutputPath $EfiOutputPath `
        -LogFullPath $logFullPath
    $script:M01LogAuthority = Open-M01LogAuthority -Path $logFullPath
}
catch {
    Write-Host (
        "TEST_FAIL efi.shell.publish authority_initialization_failed detail=" +
        $_.Exception.Message.Replace("`r", " ").Replace("`n", " "))
    exit 1
}

$publishOutputLock = $null
$sourceLocks = $null
$restoreArchiveSourceLocks = $null
$restoreSourceLocks = $null
$restoreLocks = $null
$packageLocks = $null
$verifierInputLocks = $null
try {
    Write-Host "TEST_START efi.shell.publish"

    try {
        $publishOutputLock = Open-M01PublishOutputLock `
            -RepositoryRoot $repositoryRoot `
            -Configuration $Configuration `
            -Runtime $Runtime
    }
    catch {
        Write-Host (
            "TEST_FAIL efi.shell.publish publish_output_lock_failed detail=" +
            (Format-EvidenceValue $_.Exception.Message))
        exit 1
    }
    Write-Host "EFI_PUBLISH_OUTPUT_LOCK status=ok"

    try {
        $sourceSnapshotBeforePublish = Get-M01PublishSourceSnapshot `
            -RepositoryRoot $repositoryRoot `
            -ProjectPath $efiProjectPath
    }
    catch {
        Write-Host (
            "TEST_FAIL efi.shell.publish source_snapshot_failed detail=" +
            (Format-EvidenceValue $_.Exception.Message))
        exit 1
    }

    try {
        $sourceLocks = Open-M01PublishSourceLocks `
            -RepositoryRoot $repositoryRoot `
            -Inputs $sourceSnapshotBeforePublish.Inputs
        Assert-M01PublishSourceLocks `
            -RepositoryRoot $repositoryRoot `
            -Inputs $sourceSnapshotBeforePublish.Inputs `
            -Locks $sourceLocks
    }
    catch {
        Write-Host (
            "TEST_FAIL efi.shell.publish source_lock_failed detail=" +
            (Format-EvidenceValue $_.Exception.Message))
        exit 1
    }

    try {
        $sourceSnapshotAfterLock = Get-M01PublishSourceSnapshot `
            -RepositoryRoot $repositoryRoot `
            -ProjectPath $efiProjectPath
    }
    catch {
        Write-Host (
            "TEST_FAIL efi.shell.publish source_snapshot_failed detail=" +
            (Format-EvidenceValue $_.Exception.Message))
        exit 1
    }
    if ($sourceSnapshotBeforePublish.InputCount -ne 137 -or
        $sourceSnapshotAfterLock.InputCount -ne 137 -or
        $sourceSnapshotBeforePublish.AggregateSha256 -cne
            $sourceSnapshotAfterLock.AggregateSha256 -or
        $sourceSnapshotBeforePublish.AuthoritySha256 -cne
            $sourceSnapshotAfterLock.AuthoritySha256) {
        Write-Host (
            "TEST_FAIL efi.shell.publish source_changed_before_lock before=" +
            $sourceSnapshotBeforePublish.AggregateSha256 +
            " locked=" +
            $sourceSnapshotAfterLock.AggregateSha256)
        exit 1
    }
    Write-Host "EFI_PUBLISH_SOURCE_LOCK status=ok"
    Write-Host "EFI_PUBLISH_SOURCE_AUTHORITY status=ok inputs=137"

Write-EfiPreflightLine "EFI_PREFLIGHT_START"
if ($EnableM03FirmwareAdapterRevocationProbe -and -not $EnableM03FirstRealExitBootServicesProbe) { Write-Output "TEST_FAIL efi.shell.publish m03_revocation_requires_first_exit"; exit 1 }
if ($EnableM03OwnedSubsystemSurvivalProbe -and -not $EnableM03FirstRealExitBootServicesProbe) { Write-Output "TEST_FAIL efi.shell.publish m03_owned_requires_first_exit"; exit 1 }
if ($EnableM03OwnedSubsystemPanicProbe -and -not $EnableM03OwnedSubsystemSurvivalProbe) { Write-Output "TEST_FAIL efi.shell.publish m03_owned_panic_requires_survival"; exit 1 }
if ($EnableM03OwnedSubsystemSurvivalProbe -and $EnableM03FirmwareAdapterRevocationProbe) { Write-Output "TEST_FAIL efi.shell.publish m03_owned_revocation_conflict"; exit 1 }
if ($EnableM04ClassConstructorExecutionProbe -and -not $EnableM04GcStaticRootMaterializationProbe) { Write-Output "TEST_FAIL efi.shell.publish m04_cctor_requires_gc_profile"; exit 1 }
if ($EnableM04ClassConstructorExecutionProbe -and $EnableM04SeededCollectorChurnProbe) { Write-Output "TEST_FAIL efi.shell.publish m04_cctor_churn_conflict"; exit 1 }
if ($EnableM04InterfaceDispatchProbe -and -not $EnableM04ClassConstructorExecutionProbe) { Write-Output "TEST_FAIL efi.shell.publish m04_dispatch_requires_cctor_profile"; exit 1 }
if ($EnableM04X64UnwindMetadataProbe -and -not $EnableM04TypeManagerModuleRegistrationProbe) { Write-Output "TEST_FAIL efi.shell.publish m04_x64_unwind_metadata_requires_original_module"; exit 1 }
if ($EnableM04SimpleThrowCatchProbe -and -not $EnableM04GcStaticRootMaterializationProbe) { Write-Output "TEST_FAIL efi.shell.publish m04_simple_catch_requires_gc_profile"; exit 1 }
if ($EnableM04SimpleThrowCatchProbe -and ($EnableM04SeededCollectorChurnProbe -or $EnableM04ClassConstructorExecutionProbe -or $EnableM04InterfaceDispatchProbe -or $EnableM04X64UnwindMetadataProbe)) { Write-Output "TEST_FAIL efi.shell.publish m04_simple_catch_profile_conflict"; exit 1 }
if ($EnableM04SeededCollectorChurnProbe -and -not $EnableM04GcStaticRootMaterializationProbe) {
    Write-Output "TEST_FAIL efi.shell.publish m04_churn_requires_gc_profile"; exit 1
}
if ($EnableM04GcStaticRootMaterializationProbe) {
    foreach ($gcFlag in @(
        'EnableM01LoadedImageStackProbe',
        'EnableM01LiveAllocatorProbe',
        'EnableM04IndependentEndianConformanceProbe',
        'EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe',
        'EnableM04ArithmeticConformanceProbe',
        'EnableM04LayoutContractProbe',
        'EnableM04ManagedHeapPageRangeProbe',
        'EnableM04RhpNewFastProbe'
    )) {
        if (-not [bool](Get-Variable -Name $gcFlag -ValueOnly)) { Write-Output ("TEST_FAIL efi.shell.publish m04_gc_static_profile_invalid property="+$gcFlag); exit 1 }
    }
    foreach ($gcFlag in @(
        'EnableM01OwnedPagingProbe',
        'EnableM02SafeStubProbe',
        'EnableM02CheckedPortIoBindingProbe',
        'EnableM02Com1OwnedPortIoProbe',
        'EnableM02OwnedCom1ReadyProbe',
        'EnableM02PanicSafeOwnedCom1Probe',
        'EnableM02OwnedX64GdtEncodingProbe',
        'EnableM02GuardedIstStackAllocationProbe',
        'EnableM02OwnedGdtTssTerminalProbe',
        'EnableM02NormalizedInterruptFramePageProbe',
        'EnableM02OwnedIdtGatePopulationProbe',
        'EnableM02IdtrContinuedExecutionProbe',
        'EnableM02Ud2VectorStateProbe',
        'EnableM02PageFaultCr2ErrorCodeProbe',
        'EnableM02DoubleFaultIstProbe',
        'EnableM02UnexpectedVectorPanicProbe',
        'EnableM04RhpNewFastBeforeBindFailureProbe',
        'EnableM04RhpNewFastExhaustionFailureProbe',
        'EnableM04SzArrayAllocationProbe',
        'EnableM04SzArrayAllocationBeforeBindProbe',
        'EnableM04SzArrayAllocationExhaustionProbe',
        'EnableM04StringAllocationProbe',
        'EnableM04StringAllocationBeforeBindProbe',
        'EnableM04StringAllocationExhaustionProbe',
        'EnableM04StringAllocationInvalidLengthProbe',
        'EnableM03MemoryMapCaptureProbe',
        'EnableM04CheckedReferenceBarrierProbe',
        'EnableM03FirmwareEntropySeedProbe',
        'EnableM04TypeManagerModuleRegistrationProbe',
        'EnableM03TypedExitBootServicesCallProbe',
        'EnableUefiGoneSimulationProbe',
        'EnableExitBootServicesFreshMapProbe',
        'EnableAcpiPhysicalDiscoveryProbe'
    )) {
        if ([bool](Get-Variable -Name $gcFlag -ValueOnly)) { Write-Output ("TEST_FAIL efi.shell.publish m04_gc_static_profile_conflict property="+$gcFlag); exit 1 }
    }
}

if ($EnableM03FirstRealExitBootServicesProbe) {
    foreach ($dependency in @(
        'EnableM01LoadedImageStackProbe',
        'EnableM01LiveAllocatorProbe',
        'EnableAcpiPhysicalDiscoveryProbe'
    )) {
        if (-not [bool](Get-Variable -Name $dependency -ValueOnly)) { Write-Output ("TEST_FAIL efi.shell.publish m03_first_exit_requires property="+$dependency); exit 1 }
    }
    foreach ($conflict in @(
        'EnableM01OwnedPagingProbe',
        'EnableM02SafeStubProbe',
        'EnableM02CheckedPortIoBindingProbe',
        'EnableM02Com1OwnedPortIoProbe',
        'EnableM02OwnedCom1ReadyProbe',
        'EnableM02PanicSafeOwnedCom1Probe',
        'EnableM02OwnedX64GdtEncodingProbe',
        'EnableM02GuardedIstStackAllocationProbe',
        'EnableM02OwnedGdtTssTerminalProbe',
        'EnableM02NormalizedInterruptFramePageProbe',
        'EnableM02OwnedIdtGatePopulationProbe',
        'EnableM02IdtrContinuedExecutionProbe',
        'EnableM02Ud2VectorStateProbe',
        'EnableM04IndependentEndianConformanceProbe',
        'EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe',
        'EnableM04ArithmeticConformanceProbe',
        'EnableM04LayoutContractProbe',
        'EnableM04ManagedHeapPageRangeProbe',
        'EnableM02PageFaultCr2ErrorCodeProbe',
        'EnableM02DoubleFaultIstProbe',
        'EnableM02UnexpectedVectorPanicProbe',
        'EnableM04RhpNewFastProbe',
        'EnableM04RhpNewFastBeforeBindFailureProbe',
        'EnableM04RhpNewFastExhaustionFailureProbe',
        'EnableM04SzArrayAllocationProbe',
        'EnableM04SzArrayAllocationBeforeBindProbe',
        'EnableM04SzArrayAllocationExhaustionProbe',
        'EnableM04StringAllocationProbe',
        'EnableM04StringAllocationBeforeBindProbe',
        'EnableM04StringAllocationExhaustionProbe',
        'EnableM04StringAllocationInvalidLengthProbe',
        'EnableM03MemoryMapCaptureProbe',
        'EnableM04CheckedReferenceBarrierProbe',
        'EnableM03FirmwareEntropySeedProbe',
        'EnableM04TypeManagerModuleRegistrationProbe',
        'EnableM03TypedExitBootServicesCallProbe',
        'EnableM04GcStaticRootMaterializationProbe',
        'EnableUefiGoneSimulationProbe',
        'EnableExitBootServicesFreshMapProbe'
    )) {
        if ([bool](Get-Variable -Name $conflict -ValueOnly)) { Write-Output ("TEST_FAIL efi.shell.publish m03_first_exit_profile_conflict property="+$conflict); exit 1 }
    }
}

if ($EnableM03TypedExitBootServicesCallProbe) {
    foreach ($typedEbsConflict in @(
        'EnableM01LoadedImageStackProbe',
        'EnableM01LiveAllocatorProbe',
        'EnableM01OwnedPagingProbe',
        'EnableM02SafeStubProbe',
        'EnableM02CheckedPortIoBindingProbe',
        'EnableM02Com1OwnedPortIoProbe',
        'EnableM02OwnedCom1ReadyProbe',
        'EnableM02PanicSafeOwnedCom1Probe',
        'EnableM02OwnedX64GdtEncodingProbe',
        'EnableM02GuardedIstStackAllocationProbe',
        'EnableM02OwnedGdtTssTerminalProbe',
        'EnableM02NormalizedInterruptFramePageProbe',
        'EnableM02OwnedIdtGatePopulationProbe',
        'EnableM02IdtrContinuedExecutionProbe',
        'EnableM02Ud2VectorStateProbe',
        'EnableM04IndependentEndianConformanceProbe',
        'EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe',
        'EnableM04ArithmeticConformanceProbe',
        'EnableM04LayoutContractProbe',
        'EnableM04ManagedHeapPageRangeProbe',
        'EnableM02PageFaultCr2ErrorCodeProbe',
        'EnableM02DoubleFaultIstProbe',
        'EnableM02UnexpectedVectorPanicProbe',
        'EnableM04RhpNewFastProbe',
        'EnableM04RhpNewFastBeforeBindFailureProbe',
        'EnableM04RhpNewFastExhaustionFailureProbe',
        'EnableM04SzArrayAllocationProbe',
        'EnableM04SzArrayAllocationBeforeBindProbe',
        'EnableM04SzArrayAllocationExhaustionProbe',
        'EnableM04StringAllocationProbe',
        'EnableM04StringAllocationBeforeBindProbe',
        'EnableM04StringAllocationExhaustionProbe',
        'EnableM04StringAllocationInvalidLengthProbe',
        'EnableM03MemoryMapCaptureProbe',
        'EnableM04CheckedReferenceBarrierProbe',
        'EnableM03FirmwareEntropySeedProbe',
        'EnableM04TypeManagerModuleRegistrationProbe',
        'EnableM04GcStaticRootMaterializationProbe',
        'EnableUefiGoneSimulationProbe',
        'EnableExitBootServicesFreshMapProbe',
        'EnableAcpiPhysicalDiscoveryProbe'
    )) {
        if ([bool](Get-Variable -Name $typedEbsConflict -ValueOnly)) { Write-Output ('TEST_FAIL efi.shell.publish m03_typed_ebs_profile_conflict property='+$typedEbsConflict); exit 1 }
    }
}

if ($EnableM03FirmwareEntropySeedProbe) {
    foreach ($entropyConflict in @(
        "EnableAcpiPhysicalDiscoveryProbe",
        "EnableExitBootServicesFreshMapProbe",
        "EnableM01LiveAllocatorProbe",
        "EnableM01LoadedImageStackProbe",
        "EnableM01OwnedPagingProbe",
        "EnableM02CheckedPortIoBindingProbe",
        "EnableM02Com1OwnedPortIoProbe",
        "EnableM02DoubleFaultIstProbe",
        "EnableM02GuardedIstStackAllocationProbe",
        "EnableM02IdtrContinuedExecutionProbe",
        "EnableM02NormalizedInterruptFramePageProbe",
        "EnableM02OwnedCom1ReadyProbe",
        "EnableM02OwnedGdtTssTerminalProbe",
        "EnableM02OwnedIdtGatePopulationProbe",
        "EnableM02OwnedX64GdtEncodingProbe",
        "EnableM02PageFaultCr2ErrorCodeProbe",
        "EnableM02PanicSafeOwnedCom1Probe",
        "EnableM02SafeStubProbe",
        "EnableM02Ud2VectorStateProbe",
        "EnableM02UnexpectedVectorPanicProbe",
        "EnableM03MemoryMapCaptureProbe",
        "EnableM04ArithmeticConformanceProbe",
        "EnableM04CheckedReferenceBarrierProbe",
        "EnableM04IndependentEndianConformanceProbe",
        "EnableM04LayoutContractProbe",
        "EnableM04ManagedHeapPageRangeProbe",
        "EnableM04RhpNewFastBeforeBindFailureProbe",
        "EnableM04RhpNewFastExhaustionFailureProbe",
        "EnableM04RhpNewFastProbe",
        "EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe",
        "EnableM04StringAllocationBeforeBindProbe",
        "EnableM04StringAllocationExhaustionProbe",
        "EnableM04StringAllocationInvalidLengthProbe",
        "EnableM04StringAllocationProbe",
        "EnableM04SzArrayAllocationBeforeBindProbe",
        "EnableM04SzArrayAllocationExhaustionProbe",
        "EnableM04SzArrayAllocationProbe",
        "EnableUefiGoneSimulationProbe"
    )) {
        if ([bool](Get-Variable -Name $entropyConflict -ValueOnly)) {
            Write-Output ("TEST_FAIL efi.shell.publish m03_firmware_entropy_profile_conflict property=" + $entropyConflict)
            exit 1
        }
    }
}


if ($EnableM04TypeManagerModuleRegistrationProbe) {
    if (-not $EnableM01LoadedImageStackProbe) { Write-Output "TEST_FAIL efi.shell.publish m04_type_manager_requires_original_loaded_image"; exit 1 }
    foreach ($typeManagerConflict in @(
        'EnableM01LiveAllocatorProbe',
        'EnableM01OwnedPagingProbe',
        'EnableM02SafeStubProbe',
        'EnableM02CheckedPortIoBindingProbe',
        'EnableM02Com1OwnedPortIoProbe',
        'EnableM02OwnedCom1ReadyProbe',
        'EnableM02PanicSafeOwnedCom1Probe',
        'EnableM02OwnedX64GdtEncodingProbe',
        'EnableM02GuardedIstStackAllocationProbe',
        'EnableM02OwnedGdtTssTerminalProbe',
        'EnableM02NormalizedInterruptFramePageProbe',
        'EnableM02OwnedIdtGatePopulationProbe',
        'EnableM02IdtrContinuedExecutionProbe',
        'EnableM02Ud2VectorStateProbe',
        'EnableM04IndependentEndianConformanceProbe',
        'EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe',
        'EnableM04ArithmeticConformanceProbe',
        'EnableM04LayoutContractProbe',
        'EnableM04ManagedHeapPageRangeProbe',
        'EnableUefiGoneSimulationProbe',
        'EnableExitBootServicesFreshMapProbe',
        'EnableAcpiPhysicalDiscoveryProbe',
        'EnableM02PageFaultCr2ErrorCodeProbe',
        'EnableM02DoubleFaultIstProbe',
        'EnableM02UnexpectedVectorPanicProbe',
        'EnableM04RhpNewFastProbe',
        'EnableM04RhpNewFastBeforeBindFailureProbe',
        'EnableM04RhpNewFastExhaustionFailureProbe',
        'EnableM04SzArrayAllocationProbe',
        'EnableM04SzArrayAllocationBeforeBindProbe',
        'EnableM04SzArrayAllocationExhaustionProbe',
        'EnableM04StringAllocationProbe',
        'EnableM04StringAllocationBeforeBindProbe',
        'EnableM04StringAllocationExhaustionProbe',
        'EnableM04StringAllocationInvalidLengthProbe',
        'EnableM03MemoryMapCaptureProbe',
        'EnableM04CheckedReferenceBarrierProbe',
        'EnableM03FirmwareEntropySeedProbe'
    )) {
        if ([bool](Get-Variable -Name $typeManagerConflict -ValueOnly)) { Write-Output ("TEST_FAIL efi.shell.publish m04_type_manager_profile_conflict property="+$typeManagerConflict); exit 1 }
    }
}

if ($EnableM04CheckedReferenceBarrierProbe) {
    foreach ($referenceFlag in @(
        "EnableM01LoadedImageStackProbe",
        "EnableM01LiveAllocatorProbe",
        "EnableM04IndependentEndianConformanceProbe",
        "EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe",
        "EnableM04ArithmeticConformanceProbe",
        "EnableM04LayoutContractProbe",
        "EnableM04ManagedHeapPageRangeProbe",
        "EnableM04RhpNewFastProbe"
    )) {
        if ( -not [bool](Get-Variable -Name $referenceFlag -ValueOnly)) {
            Write-Output ("TEST_FAIL efi.shell.publish m04_checked_reference_profile_invalid property=" + $referenceFlag)
            exit 1
        }
    }
    foreach ($referenceFlag in @(
        "EnableM01OwnedPagingProbe",
        "EnableM02SafeStubProbe",
        "EnableM02CheckedPortIoBindingProbe",
        "EnableM02Com1OwnedPortIoProbe",
        "EnableM02OwnedCom1ReadyProbe",
        "EnableM02PanicSafeOwnedCom1Probe",
        "EnableM02OwnedX64GdtEncodingProbe",
        "EnableM02GuardedIstStackAllocationProbe",
        "EnableM02OwnedGdtTssTerminalProbe",
        "EnableM02NormalizedInterruptFramePageProbe",
        "EnableM02OwnedIdtGatePopulationProbe",
        "EnableM02IdtrContinuedExecutionProbe",
        "EnableM02Ud2VectorStateProbe",
        "EnableM02PageFaultCr2ErrorCodeProbe",
        "EnableM02DoubleFaultIstProbe",
        "EnableM02UnexpectedVectorPanicProbe",
        "EnableM04RhpNewFastBeforeBindFailureProbe",
        "EnableM04RhpNewFastExhaustionFailureProbe",
        "EnableM04SzArrayAllocationProbe",
        "EnableM04SzArrayAllocationBeforeBindProbe",
        "EnableM04SzArrayAllocationExhaustionProbe",
        "EnableM04StringAllocationProbe",
        "EnableM04StringAllocationBeforeBindProbe",
        "EnableM04StringAllocationExhaustionProbe",
        "EnableM04StringAllocationInvalidLengthProbe",
        "EnableM03MemoryMapCaptureProbe",
        "EnableM03FirmwareEntropySeedProbe",
        "EnableM04TypeManagerModuleRegistrationProbe",
        "EnableM04GcStaticRootMaterializationProbe",
        "EnableM03TypedExitBootServicesCallProbe",
        "EnableUefiGoneSimulationProbe",
        "EnableExitBootServicesFreshMapProbe",
        "EnableAcpiPhysicalDiscoveryProbe"
    )) {
        if ([bool](Get-Variable -Name $referenceFlag -ValueOnly)) {
            Write-Output ("TEST_FAIL efi.shell.publish m04_checked_reference_profile_conflict property=" + $referenceFlag)
            exit 1
        }
    }
}

if ($EnableM03MemoryMapCaptureProbe) {
    foreach ($memoryMapConflict in @(
        "EnableM01LoadedImageStackProbe",
        "EnableM01LiveAllocatorProbe",
        "EnableM01OwnedPagingProbe",
        "EnableM02SafeStubProbe",
        "EnableM02CheckedPortIoBindingProbe",
        "EnableM02Com1OwnedPortIoProbe",
        "EnableM02OwnedCom1ReadyProbe",
        "EnableM02PanicSafeOwnedCom1Probe",
        "EnableM02OwnedX64GdtEncodingProbe",
        "EnableM02GuardedIstStackAllocationProbe",
        "EnableM02OwnedGdtTssTerminalProbe",
        "EnableM02NormalizedInterruptFramePageProbe",
        "EnableM02OwnedIdtGatePopulationProbe",
        "EnableM02IdtrContinuedExecutionProbe",
        "EnableM02Ud2VectorStateProbe",
        "EnableM04IndependentEndianConformanceProbe",
        "EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe",
        "EnableM04ArithmeticConformanceProbe",
        "EnableM04LayoutContractProbe",
        "EnableM04ManagedHeapPageRangeProbe",
        "EnableM02PageFaultCr2ErrorCodeProbe",
        "EnableM02DoubleFaultIstProbe",
        "EnableM02UnexpectedVectorPanicProbe",
        "EnableM04RhpNewFastProbe",
        "EnableM04RhpNewFastBeforeBindFailureProbe",
        "EnableM04RhpNewFastExhaustionFailureProbe",
        "EnableM04SzArrayAllocationProbe",
        "EnableM04SzArrayAllocationBeforeBindProbe",
        "EnableM04SzArrayAllocationExhaustionProbe",
        "EnableM04StringAllocationProbe",
        "EnableM04StringAllocationBeforeBindProbe",
        "EnableM04StringAllocationExhaustionProbe",
        "EnableM04StringAllocationInvalidLengthProbe",
        "EnableUefiGoneSimulationProbe",
        "EnableExitBootServicesFreshMapProbe",
        "EnableAcpiPhysicalDiscoveryProbe"
    )) {
        if ([bool](Get-Variable -Name $memoryMapConflict -ValueOnly)) {
            Stop-EfiPreflight -Marker "EFI_PREFLIGHT_M03_MEMORY_MAP_CAPTURE status=invalid" -Reason ("m03_memory_map_capture_profile_conflict property=" + $memoryMapConflict) -LogFullPath $logFullPath -ReportedLogPath $LogPath -Status "invalid"
        }
    }
}

$stringReason = ""
if (($EnableM04StringAllocationBeforeBindProbe -or $EnableM04StringAllocationExhaustionProbe -or $EnableM04StringAllocationInvalidLengthProbe) -and -not $EnableM04StringAllocationProbe) { $stringReason = "m04_string_negative_requires_main" }
if (([int][bool]$EnableM04StringAllocationBeforeBindProbe + [int][bool]$EnableM04StringAllocationExhaustionProbe + [int][bool]$EnableM04StringAllocationInvalidLengthProbe) -gt 1) { $stringReason = "m04_string_negative_conflict" }
if ($EnableM04StringAllocationProbe) {
    foreach ($stringRequired in @("EnableM01LoadedImageStackProbe", "EnableM01LiveAllocatorProbe", "EnableM04IndependentEndianConformanceProbe", "EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe", "EnableM04ArithmeticConformanceProbe", "EnableM04LayoutContractProbe", "EnableM04ManagedHeapPageRangeProbe", "EnableM04RhpNewFastProbe", "EnableM04SzArrayAllocationProbe")) {
        if (-not [bool](Get-Variable -Name $stringRequired -ValueOnly)) { $stringReason = "m04_string_requires_explicit_chain_" + $stringRequired }
    }
}
if ($EnableM04StringAllocationProbe -or $EnableM04StringAllocationBeforeBindProbe -or $EnableM04StringAllocationExhaustionProbe -or $EnableM04StringAllocationInvalidLengthProbe) {
    foreach ($stringConflict in @("EnableM01OwnedPagingProbe", "EnableM02SafeStubProbe", "EnableM02CheckedPortIoBindingProbe", "EnableM02Com1OwnedPortIoProbe", "EnableM02OwnedCom1ReadyProbe", "EnableM02PanicSafeOwnedCom1Probe", "EnableM02OwnedX64GdtEncodingProbe", "EnableM02GuardedIstStackAllocationProbe", "EnableM02OwnedGdtTssTerminalProbe", "EnableM02NormalizedInterruptFramePageProbe", "EnableM02OwnedIdtGatePopulationProbe", "EnableM02IdtrContinuedExecutionProbe", "EnableM02Ud2VectorStateProbe", "EnableM02PageFaultCr2ErrorCodeProbe", "EnableM02DoubleFaultIstProbe", "EnableM02UnexpectedVectorPanicProbe", "EnableUefiGoneSimulationProbe", "EnableExitBootServicesFreshMapProbe", "EnableAcpiPhysicalDiscoveryProbe", "EnableM04RhpNewFastBeforeBindFailureProbe", "EnableM04RhpNewFastExhaustionFailureProbe", "EnableM04SzArrayAllocationBeforeBindProbe", "EnableM04SzArrayAllocationExhaustionProbe")) {
        if ([bool](Get-Variable -Name $stringConflict -ValueOnly)) { $stringReason = "m04_string_conflicting_profile_" + $stringConflict }
    }
}
$arrayReason = $stringReason
if (($EnableM04SzArrayAllocationBeforeBindProbe -or $EnableM04SzArrayAllocationExhaustionProbe) -and -not $EnableM04SzArrayAllocationProbe) { $arrayReason = "m04_sz_array_negative_requires_main" }
if ($EnableM04SzArrayAllocationBeforeBindProbe -and $EnableM04SzArrayAllocationExhaustionProbe) { $arrayReason = "m04_sz_array_negative_conflict" }
if ($EnableM04SzArrayAllocationProbe) {
    foreach ($arrayRequired in @("EnableM04RhpNewFastProbe", "EnableM01LoadedImageStackProbe", "EnableM01LiveAllocatorProbe", "EnableM04IndependentEndianConformanceProbe", "EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe", "EnableM04ArithmeticConformanceProbe", "EnableM04LayoutContractProbe", "EnableM04ManagedHeapPageRangeProbe")) {
        if (-not [bool](Get-Variable -Name $arrayRequired -ValueOnly)) { $arrayReason = "m04_sz_array_requires_explicit_chain_" + $arrayRequired }
    }
}
if ($EnableM04SzArrayAllocationProbe -or $EnableM04SzArrayAllocationBeforeBindProbe -or $EnableM04SzArrayAllocationExhaustionProbe) {
    foreach ($arrayConflict in @("EnableM02SafeStubProbe", "EnableM02CheckedPortIoBindingProbe", "EnableM02Com1OwnedPortIoProbe", "EnableM02OwnedCom1ReadyProbe", "EnableM02PanicSafeOwnedCom1Probe", "EnableM02OwnedX64GdtEncodingProbe", "EnableM02GuardedIstStackAllocationProbe", "EnableM02OwnedGdtTssTerminalProbe", "EnableM02NormalizedInterruptFramePageProbe", "EnableM02OwnedIdtGatePopulationProbe", "EnableM02IdtrContinuedExecutionProbe", "EnableM02Ud2VectorStateProbe", "EnableM02PageFaultCr2ErrorCodeProbe", "EnableM02DoubleFaultIstProbe", "EnableM02UnexpectedVectorPanicProbe", "EnableM01OwnedPagingProbe", "EnableUefiGoneSimulationProbe", "EnableExitBootServicesFreshMapProbe", "EnableAcpiPhysicalDiscoveryProbe", "EnableM04RhpNewFastBeforeBindFailureProbe", "EnableM04RhpNewFastExhaustionFailureProbe")) {
        if ([bool](Get-Variable -Name $arrayConflict -ValueOnly)) { $arrayReason = "m04_sz_array_conflicting_profile_" + $arrayConflict }
    }
}
$rhpReason = $arrayReason
if (($EnableM04RhpNewFastBeforeBindFailureProbe -or $EnableM04RhpNewFastExhaustionFailureProbe) -and -not $EnableM04RhpNewFastProbe) { $rhpReason = "m04_rhp_new_fast_negative_requires_main" }
if ($EnableM04RhpNewFastBeforeBindFailureProbe -and $EnableM04RhpNewFastExhaustionFailureProbe) { $rhpReason = "m04_rhp_new_fast_negative_conflict" }
if ($EnableM04RhpNewFastProbe) {
    foreach ($rhpRequired in @("EnableM01LoadedImageStackProbe", "EnableM01LiveAllocatorProbe", "EnableM04IndependentEndianConformanceProbe", "EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe", "EnableM04ArithmeticConformanceProbe", "EnableM04LayoutContractProbe", "EnableM04ManagedHeapPageRangeProbe")) {
        if (-not [bool](Get-Variable -Name $rhpRequired -ValueOnly)) { $rhpReason = "m04_rhp_new_fast_requires_explicit_chain_" + $rhpRequired }
    }
    foreach ($rhpConflict in @("EnableM02SafeStubProbe", "EnableM02CheckedPortIoBindingProbe", "EnableM02Com1OwnedPortIoProbe", "EnableM02OwnedCom1ReadyProbe", "EnableM02PanicSafeOwnedCom1Probe", "EnableM02OwnedX64GdtEncodingProbe", "EnableM02GuardedIstStackAllocationProbe", "EnableM02OwnedGdtTssTerminalProbe", "EnableM02NormalizedInterruptFramePageProbe", "EnableM02OwnedIdtGatePopulationProbe", "EnableM02IdtrContinuedExecutionProbe", "EnableM02Ud2VectorStateProbe", "EnableM02PageFaultCr2ErrorCodeProbe", "EnableM02DoubleFaultIstProbe", "EnableM02UnexpectedVectorPanicProbe", "EnableM01OwnedPagingProbe", "EnableUefiGoneSimulationProbe", "EnableExitBootServicesFreshMapProbe", "EnableAcpiPhysicalDiscoveryProbe")) {
        if ([bool](Get-Variable -Name $rhpConflict -ValueOnly)) { $rhpReason = "m04_rhp_new_fast_conflicting_profile_" + $rhpConflict }
    }
}
if ($rhpReason.Length -ne 0) { Stop-EfiPreflight -Marker ("EFI_PREFLIGHT_M04_RHP_NEW_FAST status=invalid reason=" + $rhpReason) -Reason $rhpReason -LogFullPath $logFullPath -ReportedLogPath $LogPath -Status "invalid" }

if ($EnableM02Ud2VectorStateProbe) {
    $m02Ud2VectorStatePrerequisites = @(
        "EnableM01LoadedImageStackProbe",
        "EnableM01LiveAllocatorProbe",
        "EnableM01OwnedPagingProbe",
        "EnableM02SafeStubProbe",
        "EnableM02CheckedPortIoBindingProbe",
        "EnableM02Com1OwnedPortIoProbe",
        "EnableM02OwnedCom1ReadyProbe",
        "EnableM02PanicSafeOwnedCom1Probe"
    )
    $explicitFalsePrerequisite = $null
    foreach ($prerequisiteName in $m02Ud2VectorStatePrerequisites) {
        if ($PSBoundParameters.ContainsKey($prerequisiteName) -and
            -not [bool]$PSBoundParameters[$prerequisiteName]) {
            $explicitFalsePrerequisite = $prerequisiteName
            break
        }
    }
    if ($null -ne $explicitFalsePrerequisite) {
        Stop-EfiPreflight `
            -Marker (
                "EFI_PREFLIGHT_M02_UD2_VECTOR_STATE_IMPLICATION status=invalid " +
                "explicitFalse=" + $explicitFalsePrerequisite) `
            -Reason "m02_ud2_vector_state_probe_explicit_false_prerequisite" `
            -LogFullPath $logFullPath `
            -ReportedLogPath $LogPath `
            -Status "invalid"
    }

    $EnableM01LoadedImageStackProbe =
        [System.Management.Automation.SwitchParameter]::new($true)
    $EnableM01LiveAllocatorProbe =
        [System.Management.Automation.SwitchParameter]::new($true)
    $EnableM01OwnedPagingProbe =
        [System.Management.Automation.SwitchParameter]::new($true)
    $EnableM02SafeStubProbe =
        [System.Management.Automation.SwitchParameter]::new($true)
    $EnableM02CheckedPortIoBindingProbe =
        [System.Management.Automation.SwitchParameter]::new($true)
    $EnableM02Com1OwnedPortIoProbe =
        [System.Management.Automation.SwitchParameter]::new($true)
    $EnableM02OwnedCom1ReadyProbe =
        [System.Management.Automation.SwitchParameter]::new($true)
    $EnableM02PanicSafeOwnedCom1Probe =
        [System.Management.Automation.SwitchParameter]::new($true)
    Write-EfiPreflightLine (
        "EFI_PREFLIGHT_M02_UD2_VECTOR_STATE_IMPLICATION status=ok " +
        "m01LoadedImageStack=true m01LiveAllocator=true ownedPaging=true " +
        "m02SafeStub=true checkedPortIoBinding=true com1OwnedPortIo=true " +
        "ownedCom1Ready=true panicSafeOwnedCom1=true")
}
if ($EnableM02PageFaultCr2ErrorCodeProbe) {
    foreach ($conflict in @(
        "EnableM02DoubleFaultIstProbe"
        "EnableM02UnexpectedVectorPanicProbe"
        "EnableM02OwnedX64GdtEncodingProbe"
        "EnableM02GuardedIstStackAllocationProbe"
        "EnableM02OwnedGdtTssTerminalProbe"
        "EnableM02NormalizedInterruptFramePageProbe"
        "EnableM02OwnedIdtGatePopulationProbe"
        "EnableM02IdtrContinuedExecutionProbe"
        "EnableM02Ud2VectorStateProbe"
        "EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe"
        "EnableM04ArithmeticConformanceProbe"
        "EnableM04LayoutContractProbe"
        "EnableM04ManagedHeapPageRangeProbe"
    )) {
        if ([bool](Get-Variable -Name $conflict -ValueOnly)) {
            Stop-EfiPreflight -Marker "EFI_PREFLIGHT_M02_PAGE_FAULT_CR2_ERROR_CODE status=invalid" -Reason ("m02_page_fault_cr2_error_code_profile_conflict_" + $conflict) -LogFullPath $logFullPath -ReportedLogPath $LogPath -Status "invalid"
        }
    }
}

if ($EnableM02DoubleFaultIstProbe) {
    foreach ($conflict in @(
        "EnableUefiGoneSimulationProbe"
        "EnableExitBootServicesFreshMapProbe"
        "EnableAcpiPhysicalDiscoveryProbe"
        "EnableM02OwnedX64GdtEncodingProbe"
        "EnableM02GuardedIstStackAllocationProbe"
        "EnableM02OwnedGdtTssTerminalProbe"
        "EnableM02NormalizedInterruptFramePageProbe"
        "EnableM02OwnedIdtGatePopulationProbe"
        "EnableM02IdtrContinuedExecutionProbe"
        "EnableM02Ud2VectorStateProbe"
        "EnableM02PageFaultCr2ErrorCodeProbe"
        "EnableM04IndependentEndianConformanceProbe"
        "EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe"
        "EnableM04ArithmeticConformanceProbe"
        "EnableM04LayoutContractProbe"
        "EnableM04ManagedHeapPageRangeProbe"
        "EnableM04RhpNewFastProbe"
        "EnableM04RhpNewFastBeforeBindFailureProbe"
        "EnableM04RhpNewFastExhaustionFailureProbe"
        "EnableM04SzArrayAllocationProbe"
        "EnableM04SzArrayAllocationBeforeBindProbe"
        "EnableM04SzArrayAllocationExhaustionProbe"
        "EnableM04StringAllocationProbe"
        "EnableM04StringAllocationBeforeBindProbe"
        "EnableM04StringAllocationExhaustionProbe"
        "EnableM04StringAllocationInvalidLengthProbe"
    )) {
        if ([bool](Get-Variable -Name $conflict -ValueOnly)) {
            Stop-EfiPreflight -Marker "EFI_PREFLIGHT_M02_DOUBLE_FAULT_IST status=invalid" -Reason ("m02_double_fault_ist_profile_conflict_" + $conflict) -LogFullPath $logFullPath -ReportedLogPath $LogPath -Status "invalid"
        }
    }
}

if ($EnableM02UnexpectedVectorPanicProbe) {
    foreach ($conflict in @(
        "EnableM02OwnedX64GdtEncodingProbe"
        "EnableM02GuardedIstStackAllocationProbe"
        "EnableM02OwnedGdtTssTerminalProbe"
        "EnableM02NormalizedInterruptFramePageProbe"
        "EnableM02OwnedIdtGatePopulationProbe"
        "EnableM02IdtrContinuedExecutionProbe"
        "EnableM02Ud2VectorStateProbe"
        "EnableM04IndependentEndianConformanceProbe"
        "EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe"
        "EnableM04ArithmeticConformanceProbe"
        "EnableM04LayoutContractProbe"
        "EnableM04ManagedHeapPageRangeProbe"
        "EnableM02PageFaultCr2ErrorCodeProbe"
        "EnableM02DoubleFaultIstProbe"
        "EnableM04RhpNewFastProbe"
        "EnableM04RhpNewFastBeforeBindFailureProbe"
        "EnableM04RhpNewFastExhaustionFailureProbe"
        "EnableM04SzArrayAllocationProbe"
        "EnableM04SzArrayAllocationBeforeBindProbe"
        "EnableM04SzArrayAllocationExhaustionProbe"
        "EnableM04StringAllocationProbe"
        "EnableM04StringAllocationBeforeBindProbe"
        "EnableM04StringAllocationExhaustionProbe"
        "EnableM04StringAllocationInvalidLengthProbe"
        "EnableUefiGoneSimulationProbe"
        "EnableExitBootServicesFreshMapProbe"
        "EnableAcpiPhysicalDiscoveryProbe"
    )) {
        if ([bool](Get-Variable -Name $conflict -ValueOnly)) {
            Stop-EfiPreflight -Marker "EFI_PREFLIGHT_M02_UNEXPECTED_VECTOR_PANIC status=invalid" -Reason ("m02_unexpected_vector_panic_profile_conflict_" + $conflict) -LogFullPath $logFullPath -ReportedLogPath $LogPath -Status "invalid"
        }
    }
}

if ($EnableM02PageFaultCr2ErrorCodeProbe) {
    $m02PageFaultCr2ErrorCodePrerequisites = @(
        "EnableM01LoadedImageStackProbe",
        "EnableM01LiveAllocatorProbe",
        "EnableM01OwnedPagingProbe",
        "EnableM02SafeStubProbe",
        "EnableM02CheckedPortIoBindingProbe",
        "EnableM02Com1OwnedPortIoProbe",
        "EnableM02OwnedCom1ReadyProbe",
        "EnableM02PanicSafeOwnedCom1Probe"
    )
    $explicitFalsePrerequisite = $null
    foreach ($prerequisiteName in $m02PageFaultCr2ErrorCodePrerequisites) {
        if ($PSBoundParameters.ContainsKey($prerequisiteName) -and
            -not [bool]$PSBoundParameters[$prerequisiteName]) {
            $explicitFalsePrerequisite = $prerequisiteName
            break
        }
    }
    if ($null -ne $explicitFalsePrerequisite) {
        Stop-EfiPreflight `
            -Marker (
                "EFI_PREFLIGHT_M02_PAGE_FAULT_CR2_ERROR_CODE_IMPLICATION status=invalid " +
                "explicitFalse=" + $explicitFalsePrerequisite) `
            -Reason "m02_page_fault_cr2_error_code_probe_explicit_false_prerequisite" `
            -LogFullPath $logFullPath `
            -ReportedLogPath $LogPath `
            -Status "invalid"
    }

    $EnableM01LoadedImageStackProbe =
        [System.Management.Automation.SwitchParameter]::new($true)
    $EnableM01LiveAllocatorProbe =
        [System.Management.Automation.SwitchParameter]::new($true)
    $EnableM01OwnedPagingProbe =
        [System.Management.Automation.SwitchParameter]::new($true)
    $EnableM02SafeStubProbe =
        [System.Management.Automation.SwitchParameter]::new($true)
    $EnableM02CheckedPortIoBindingProbe =
        [System.Management.Automation.SwitchParameter]::new($true)
    $EnableM02Com1OwnedPortIoProbe =
        [System.Management.Automation.SwitchParameter]::new($true)
    $EnableM02OwnedCom1ReadyProbe =
        [System.Management.Automation.SwitchParameter]::new($true)
    $EnableM02PanicSafeOwnedCom1Probe =
        [System.Management.Automation.SwitchParameter]::new($true)
    Write-EfiPreflightLine (
        "EFI_PREFLIGHT_M02_PAGE_FAULT_CR2_ERROR_CODE_IMPLICATION status=ok " +
        "m01LoadedImageStack=true m01LiveAllocator=true ownedPaging=true " +
        "m02SafeStub=true checkedPortIoBinding=true com1OwnedPortIo=true " +
        "ownedCom1Ready=true panicSafeOwnedCom1=true")
}

if ($EnableM02DoubleFaultIstProbe) {
    $m02DoubleFaultIstPrerequisites = @(
        "EnableM01LoadedImageStackProbe",
        "EnableM01LiveAllocatorProbe",
        "EnableM01OwnedPagingProbe",
        "EnableM02SafeStubProbe",
        "EnableM02CheckedPortIoBindingProbe",
        "EnableM02Com1OwnedPortIoProbe",
        "EnableM02OwnedCom1ReadyProbe",
        "EnableM02PanicSafeOwnedCom1Probe"
    )
    $explicitFalsePrerequisite = $null
    foreach ($prerequisiteName in $m02DoubleFaultIstPrerequisites) {
        if ($PSBoundParameters.ContainsKey($prerequisiteName) -and
            -not [bool]$PSBoundParameters[$prerequisiteName]) {
            $explicitFalsePrerequisite = $prerequisiteName
            break
        }
    }
    if ($null -ne $explicitFalsePrerequisite) {
        Stop-EfiPreflight `
            -Marker (
                "EFI_PREFLIGHT_M02_DOUBLE_FAULT_IST_IMPLICATION status=invalid " +
                "explicitFalse=" + $explicitFalsePrerequisite) `
            -Reason "m02_double_fault_ist_probe_explicit_false_prerequisite" `
            -LogFullPath $logFullPath `
            -ReportedLogPath $LogPath `
            -Status "invalid"
    }

    $EnableM01LoadedImageStackProbe =
        [System.Management.Automation.SwitchParameter]::new($true)
    $EnableM01LiveAllocatorProbe =
        [System.Management.Automation.SwitchParameter]::new($true)
    $EnableM01OwnedPagingProbe =
        [System.Management.Automation.SwitchParameter]::new($true)
    $EnableM02SafeStubProbe =
        [System.Management.Automation.SwitchParameter]::new($true)
    $EnableM02CheckedPortIoBindingProbe =
        [System.Management.Automation.SwitchParameter]::new($true)
    $EnableM02Com1OwnedPortIoProbe =
        [System.Management.Automation.SwitchParameter]::new($true)
    $EnableM02OwnedCom1ReadyProbe =
        [System.Management.Automation.SwitchParameter]::new($true)
    $EnableM02PanicSafeOwnedCom1Probe =
        [System.Management.Automation.SwitchParameter]::new($true)
    Write-EfiPreflightLine (
        "EFI_PREFLIGHT_M02_DOUBLE_FAULT_IST_IMPLICATION status=ok " +
        "m01LoadedImageStack=true m01LiveAllocator=true ownedPaging=true " +
        "m02SafeStub=true checkedPortIoBinding=true com1OwnedPortIo=true " +
        "ownedCom1Ready=true panicSafeOwnedCom1=true")
}

if ($EnableM02UnexpectedVectorPanicProbe) {
    $m02UnexpectedVectorPanicPrerequisites = @(
        "EnableM01LoadedImageStackProbe",
        "EnableM01LiveAllocatorProbe",
        "EnableM01OwnedPagingProbe",
        "EnableM02SafeStubProbe",
        "EnableM02CheckedPortIoBindingProbe",
        "EnableM02Com1OwnedPortIoProbe",
        "EnableM02OwnedCom1ReadyProbe",
        "EnableM02PanicSafeOwnedCom1Probe"
    )
    $explicitFalsePrerequisite = $null
    foreach ($prerequisiteName in $m02UnexpectedVectorPanicPrerequisites) {
        if ($PSBoundParameters.ContainsKey($prerequisiteName) -and
            -not [bool]$PSBoundParameters[$prerequisiteName]) {
            $explicitFalsePrerequisite = $prerequisiteName
            break
        }
    }
    if ($null -ne $explicitFalsePrerequisite) {
        Stop-EfiPreflight `
            -Marker (
                "EFI_PREFLIGHT_M02_UNEXPECTED_VECTOR_PANIC_IMPLICATION status=invalid " +
                "explicitFalse=" + $explicitFalsePrerequisite) `
            -Reason "m02_unexpected_vector_panic_probe_explicit_false_prerequisite" `
            -LogFullPath $logFullPath `
            -ReportedLogPath $LogPath `
            -Status "invalid"
    }

    $EnableM01LoadedImageStackProbe =
        [System.Management.Automation.SwitchParameter]::new($true)
    $EnableM01LiveAllocatorProbe =
        [System.Management.Automation.SwitchParameter]::new($true)
    $EnableM01OwnedPagingProbe =
        [System.Management.Automation.SwitchParameter]::new($true)
    $EnableM02SafeStubProbe =
        [System.Management.Automation.SwitchParameter]::new($true)
    $EnableM02CheckedPortIoBindingProbe =
        [System.Management.Automation.SwitchParameter]::new($true)
    $EnableM02Com1OwnedPortIoProbe =
        [System.Management.Automation.SwitchParameter]::new($true)
    $EnableM02OwnedCom1ReadyProbe =
        [System.Management.Automation.SwitchParameter]::new($true)
    $EnableM02PanicSafeOwnedCom1Probe =
        [System.Management.Automation.SwitchParameter]::new($true)
    Write-EfiPreflightLine (
        "EFI_PREFLIGHT_M02_UNEXPECTED_VECTOR_PANIC_IMPLICATION status=ok " +
        "m01LoadedImageStack=true m01LiveAllocator=true ownedPaging=true " +
        "m02SafeStub=true checkedPortIoBinding=true com1OwnedPortIo=true " +
        "ownedCom1Ready=true panicSafeOwnedCom1=true")
}

if (-not [string]::Equals($Runtime, "win-x64", [System.StringComparison]::OrdinalIgnoreCase)) {
    Stop-EfiPreflight `
        -Marker "EFI_PREFLIGHT_RUNTIME" `
        -Reason "runtime_not_win_x64" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath `
        -Status "unsupported"
}

$loadedImageStackProbeEnabled =
    $EnableM01LoadedImageStackProbe.IsPresent.ToString().ToLowerInvariant()
$liveAllocatorProbeEnabled =
    $EnableM01LiveAllocatorProbe.IsPresent.ToString().ToLowerInvariant()
$ownedPagingProbeEnabled =
    $EnableM01OwnedPagingProbe.IsPresent.ToString().ToLowerInvariant()
$m02SafeStubProbeEnabled =
    $EnableM02SafeStubProbe.IsPresent.ToString().ToLowerInvariant()
$m02CheckedPortIoBindingProbeEnabled =
    $EnableM02CheckedPortIoBindingProbe.IsPresent.ToString().ToLowerInvariant()
$m02Com1OwnedPortIoProbeEnabled =
    $EnableM02Com1OwnedPortIoProbe.IsPresent.ToString().ToLowerInvariant()
$m02OwnedCom1ReadyProbeEnabled =
    $EnableM02OwnedCom1ReadyProbe.IsPresent.ToString().ToLowerInvariant()
$m02PanicSafeOwnedCom1ProbeEnabled =
    $EnableM02PanicSafeOwnedCom1Probe.IsPresent.ToString().ToLowerInvariant()
$m02OwnedX64GdtEncodingProbeEnabled =
    $EnableM02OwnedX64GdtEncodingProbe.IsPresent.ToString().ToLowerInvariant()
$m02GuardedIstStackAllocationProbeEnabled =
    $EnableM02GuardedIstStackAllocationProbe.IsPresent.ToString().ToLowerInvariant()
$m02OwnedGdtTssTerminalProbeEnabled =
    $EnableM02OwnedGdtTssTerminalProbe.IsPresent.ToString().ToLowerInvariant()
$m02NormalizedInterruptFramePageProbeEnabled =
    $EnableM02NormalizedInterruptFramePageProbe.IsPresent.ToString().ToLowerInvariant()
$m02OwnedIdtGatePopulationProbeEnabled =
    $EnableM02OwnedIdtGatePopulationProbe.IsPresent.ToString().ToLowerInvariant()
$m02IdtrContinuedExecutionProbeEnabled =
    $EnableM02IdtrContinuedExecutionProbe.IsPresent.ToString().ToLowerInvariant()
$m02Ud2VectorStateProbeEnabled =
    $EnableM02Ud2VectorStateProbe.IsPresent.ToString().ToLowerInvariant()
$m02PageFaultCr2ErrorCodeProbeEnabled =
    $EnableM02PageFaultCr2ErrorCodeProbe.IsPresent.ToString().ToLowerInvariant()
$m02DoubleFaultIstProbeEnabled =
    $EnableM02DoubleFaultIstProbe.IsPresent.ToString().ToLowerInvariant()
$m02UnexpectedVectorPanicProbeEnabled =
    $EnableM02UnexpectedVectorPanicProbe.IsPresent.ToString().ToLowerInvariant()
$m04IndependentEndianConformanceProbeEnabled =
    $EnableM04IndependentEndianConformanceProbe.IsPresent.ToString().ToLowerInvariant()
$m04SpanMemoryConformanceProbeEnabled =
    $EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe.IsPresent.ToString().ToLowerInvariant()
$m04ArithmeticConformanceProbeEnabled =
    $EnableM04ArithmeticConformanceProbe.IsPresent.ToString().ToLowerInvariant()
$m04LayoutContractProbeEnabled =
    $EnableM04LayoutContractProbe.IsPresent.ToString().ToLowerInvariant()
$m01SwitchEvidence =
    "loadedImageStack=" + $loadedImageStackProbeEnabled +
    " liveAllocator=" + $liveAllocatorProbeEnabled +
    " ownedPaging=" + $ownedPagingProbeEnabled +
    " m02SafeStub=" + $m02SafeStubProbeEnabled
$m02SwitchEvidence =
    "enabled=" + $m02SafeStubProbeEnabled +
    " loadedImageStack=" + $loadedImageStackProbeEnabled +
    " liveAllocator=" + $liveAllocatorProbeEnabled +
    " ownedPaging=" + $ownedPagingProbeEnabled +
    " checkedPortIoBinding=" + $m02CheckedPortIoBindingProbeEnabled +
    " com1OwnedPortIo=" + $m02Com1OwnedPortIoProbeEnabled +
    " ownedCom1Ready=" + $m02OwnedCom1ReadyProbeEnabled +
    " panicSafeOwnedCom1=" + $m02PanicSafeOwnedCom1ProbeEnabled +
    " ownedX64GdtEncoding=" + $m02OwnedX64GdtEncodingProbeEnabled +
    " guardedIstStackAllocation=" +
    $m02GuardedIstStackAllocationProbeEnabled +
    " ownedGdtTssTerminal=" + $m02OwnedGdtTssTerminalProbeEnabled +
    " normalizedInterruptFramePage=" +
    $m02NormalizedInterruptFramePageProbeEnabled +
    " ownedIdtGatePopulation=" +
    $m02OwnedIdtGatePopulationProbeEnabled +
    " idtrContinuedExecution=" +
    $m02IdtrContinuedExecutionProbeEnabled +
    " ud2VectorState=" +
    $m02Ud2VectorStateProbeEnabled
$m04SwitchEvidence =
    "independentEndianConformance=" +
    $m04IndependentEndianConformanceProbeEnabled +
    " spanOverlapMemoryCastUnsafeWidthConformance=" +
    $m04SpanMemoryConformanceProbeEnabled +
    " arithmeticConformance=" + $m04ArithmeticConformanceProbeEnabled +
    " layoutContract=" + $m04LayoutContractProbeEnabled +
    " managedHeapPageRange=" + $EnableM04ManagedHeapPageRangeProbe.IsPresent.ToString().ToLowerInvariant()

if ($EnableM04ManagedHeapPageRangeProbe -and
    (-not $EnableM01LoadedImageStackProbe -or
    -not $EnableM01LiveAllocatorProbe -or
    -not $EnableM04IndependentEndianConformanceProbe -or
    -not $EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe -or
    -not $EnableM04ArithmeticConformanceProbe -or
    -not $EnableM04LayoutContractProbe)) {
    Stop-EfiPreflight -Marker "EFI_PREFLIGHT_M04_SWITCHES reason=m04_managed_heap_page_range_requires_explicit_chain" -Reason "m04_managed_heap_page_range_requires_explicit_chain" -LogFullPath $logFullPath -ReportedLogPath $LogPath -Status "invalid"
}
if ($EnableM04ManagedHeapPageRangeProbe -and
    ($EnableM01OwnedPagingProbe -or
    $EnableM02SafeStubProbe -or
    $EnableM02CheckedPortIoBindingProbe -or
    $EnableM02Com1OwnedPortIoProbe -or
    $EnableM02OwnedCom1ReadyProbe -or
    $EnableM02PanicSafeOwnedCom1Probe -or
    $EnableM02OwnedX64GdtEncodingProbe -or
    $EnableM02GuardedIstStackAllocationProbe -or
    $EnableM02OwnedGdtTssTerminalProbe -or
    $EnableM02NormalizedInterruptFramePageProbe -or
    $EnableM02OwnedIdtGatePopulationProbe -or
    $EnableM02IdtrContinuedExecutionProbe -or
    $EnableM02Ud2VectorStateProbe -or
    $EnableUefiGoneSimulationProbe -or
    $EnableExitBootServicesFreshMapProbe -or
    $EnableAcpiPhysicalDiscoveryProbe)) {
    Stop-EfiPreflight -Marker "EFI_PREFLIGHT_M04_SWITCHES reason=m04_managed_heap_page_range_forbids_optional_profiles" -Reason "m04_managed_heap_page_range_forbids_optional_profiles" -LogFullPath $logFullPath -ReportedLogPath $LogPath -Status "invalid"
}

if ($EnableM04LayoutContractProbe -and
    (-not $EnableM04ArithmeticConformanceProbe -or
    -not $EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe -or
    -not $EnableM04IndependentEndianConformanceProbe)) {
    Stop-EfiPreflight `
        -Marker ("EFI_PREFLIGHT_M04_SWITCHES " + $m04SwitchEvidence + " reason=layout_contract_requires_explicit_m04_chain") `
        -Reason "m04_layout_contract_requires_explicit_m04_chain" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath `
        -Status "invalid"
}

if ($EnableM04LayoutContractProbe -and
    $EnableM02OwnedGdtTssTerminalProbe) {
    Stop-EfiPreflight `
        -Marker ("EFI_PREFLIGHT_M04_SWITCHES " + $m04SwitchEvidence + " reason=layout_contract_forbids_owned_gdt_tss_terminal") `
        -Reason "m04_layout_contract_forbids_owned_gdt_tss_terminal" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath `
        -Status "invalid"
}

if ($EnableM04LayoutContractProbe -and
    $EnableM02Ud2VectorStateProbe) {
    Stop-EfiPreflight `
        -Marker ("EFI_PREFLIGHT_M04_SWITCHES " + $m04SwitchEvidence + " reason=layout_contract_forbids_m02_ud2_vector_state") `
        -Reason "m04_layout_contract_forbids_m02_ud2_vector_state" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath `
        -Status "invalid"
}

if ($EnableM04ArithmeticConformanceProbe -and
    -not $EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe) {
    Stop-EfiPreflight `
        -Marker (
            "EFI_PREFLIGHT_M04_SWITCHES " + $m04SwitchEvidence +
            " reason=arithmetic_conformance_requires_span_memory_conformance") `
        -Reason "m04_arithmetic_conformance_requires_span_memory_conformance" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath `
        -Status "invalid"
}

if ($EnableM04ArithmeticConformanceProbe -and
    $EnableM02OwnedGdtTssTerminalProbe) {
    Stop-EfiPreflight `
        -Marker (
            "EFI_PREFLIGHT_M04_SWITCHES " + $m04SwitchEvidence +
            " reason=arithmetic_conformance_forbids_owned_gdt_tss_terminal") `
        -Reason "m04_arithmetic_conformance_forbids_owned_gdt_tss_terminal" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath `
        -Status "invalid"
}

if ($EnableM04ArithmeticConformanceProbe -and
    $EnableM02Ud2VectorStateProbe) {
    Stop-EfiPreflight `
        -Marker (
            "EFI_PREFLIGHT_M04_SWITCHES " + $m04SwitchEvidence +
            " reason=arithmetic_conformance_forbids_m02_ud2_vector_state") `
        -Reason "m04_arithmetic_conformance_forbids_m02_ud2_vector_state" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath `
        -Status "invalid"
}

if ($EnableM02IdtrContinuedExecutionProbe -and
    $EnableM02OwnedGdtTssTerminalProbe) {
    Stop-EfiPreflight `
        -Marker (
            "EFI_PREFLIGHT_M02_SWITCHES " + $m02SwitchEvidence +
            " reason=m02_idtr_continued_execution_probe_forbids_owned_gdt_tss_terminal") `
        -Reason "m02_idtr_continued_execution_probe_forbids_owned_gdt_tss_terminal" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath `
        -Status "invalid"
}

if ($EnableM02IdtrContinuedExecutionProbe -and
    $EnableM02NormalizedInterruptFramePageProbe) {
    Stop-EfiPreflight `
        -Marker (
            "EFI_PREFLIGHT_M02_SWITCHES " + $m02SwitchEvidence +
            " reason=m02_idtr_continued_execution_probe_forbids_normalized_interrupt_frame_page_probe") `
        -Reason "m02_idtr_continued_execution_probe_forbids_normalized_interrupt_frame_page_probe" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath `
        -Status "invalid"
}

if ($EnableM02IdtrContinuedExecutionProbe -and
    $EnableM02OwnedIdtGatePopulationProbe) {
    Stop-EfiPreflight `
        -Marker (
            "EFI_PREFLIGHT_M02_SWITCHES " + $m02SwitchEvidence +
            " reason=m02_idtr_continued_execution_probe_forbids_owned_idt_gate_population_probe") `
        -Reason "m02_idtr_continued_execution_probe_forbids_owned_idt_gate_population_probe" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath `
        -Status "invalid"
}

if ($EnableM02Ud2VectorStateProbe -and
    -not $EnableM02PanicSafeOwnedCom1Probe) {
    Stop-EfiPreflight `
        -Marker (
            "EFI_PREFLIGHT_M02_SWITCHES " + $m02SwitchEvidence +
            " reason=m02_ud2_vector_state_probe_requires_panic_safe_owned_com1") `
        -Reason "m02_ud2_vector_state_probe_requires_panic_safe_owned_com1" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath `
        -Status "invalid"
}

if ($EnableM02Ud2VectorStateProbe -and
    $EnableM02OwnedX64GdtEncodingProbe) {
    Stop-EfiPreflight `
        -Marker (
            "EFI_PREFLIGHT_M02_SWITCHES " + $m02SwitchEvidence +
            " reason=m02_ud2_vector_state_probe_forbids_owned_x64_gdt_encoding_probe") `
        -Reason "m02_ud2_vector_state_probe_forbids_owned_x64_gdt_encoding_probe" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath `
        -Status "invalid"
}

if ($EnableM02Ud2VectorStateProbe -and
    $EnableM02GuardedIstStackAllocationProbe) {
    Stop-EfiPreflight `
        -Marker (
            "EFI_PREFLIGHT_M02_SWITCHES " + $m02SwitchEvidence +
            " reason=m02_ud2_vector_state_probe_forbids_guarded_ist_stack_allocation_probe") `
        -Reason "m02_ud2_vector_state_probe_forbids_guarded_ist_stack_allocation_probe" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath `
        -Status "invalid"
}

if ($EnableM02Ud2VectorStateProbe -and
    $EnableM02OwnedGdtTssTerminalProbe) {
    Stop-EfiPreflight `
        -Marker (
            "EFI_PREFLIGHT_M02_SWITCHES " + $m02SwitchEvidence +
            " reason=m02_ud2_vector_state_probe_forbids_owned_gdt_tss_terminal") `
        -Reason "m02_ud2_vector_state_probe_forbids_owned_gdt_tss_terminal" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath `
        -Status "invalid"
}

if ($EnableM02Ud2VectorStateProbe -and
    $EnableM02NormalizedInterruptFramePageProbe) {
    Stop-EfiPreflight `
        -Marker (
            "EFI_PREFLIGHT_M02_SWITCHES " + $m02SwitchEvidence +
            " reason=m02_ud2_vector_state_probe_forbids_normalized_interrupt_frame_page_probe") `
        -Reason "m02_ud2_vector_state_probe_forbids_normalized_interrupt_frame_page_probe" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath `
        -Status "invalid"
}

if ($EnableM02Ud2VectorStateProbe -and
    $EnableM02OwnedIdtGatePopulationProbe) {
    Stop-EfiPreflight `
        -Marker (
            "EFI_PREFLIGHT_M02_SWITCHES " + $m02SwitchEvidence +
            " reason=m02_ud2_vector_state_probe_forbids_owned_idt_gate_population_probe") `
        -Reason "m02_ud2_vector_state_probe_forbids_owned_idt_gate_population_probe" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath `
        -Status "invalid"
}

if ($EnableM02Ud2VectorStateProbe -and
    $EnableM02IdtrContinuedExecutionProbe) {
    Stop-EfiPreflight `
        -Marker (
            "EFI_PREFLIGHT_M02_SWITCHES " + $m02SwitchEvidence +
            " reason=m02_ud2_vector_state_probe_forbids_idtr_continued_execution_probe") `
        -Reason "m02_ud2_vector_state_probe_forbids_idtr_continued_execution_probe" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath `
        -Status "invalid"
}

if ($EnableM02SafeStubProbe -and -not $EnableM01LoadedImageStackProbe) {
    Stop-EfiPreflight `
        -Marker (
            "EFI_PREFLIGHT_M02_SWITCHES " + $m02SwitchEvidence +
            " reason=safe_stub_requires_loaded_image_stack") `
        -Reason "m02_safe_stub_requires_loaded_image_stack" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath `
        -Status "invalid"
}

if ($EnableM02SafeStubProbe -and -not $EnableM01LiveAllocatorProbe) {
    Stop-EfiPreflight `
        -Marker (
            "EFI_PREFLIGHT_M02_SWITCHES " + $m02SwitchEvidence +
            " reason=safe_stub_requires_live_allocator") `
        -Reason "m02_safe_stub_requires_live_allocator" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath `
        -Status "invalid"
}

if ($EnableM02SafeStubProbe -and -not $EnableM01OwnedPagingProbe) {
    Stop-EfiPreflight `
        -Marker (
            "EFI_PREFLIGHT_M02_SWITCHES " + $m02SwitchEvidence +
            " reason=safe_stub_requires_owned_paging") `
        -Reason "m02_safe_stub_requires_owned_paging" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath `
        -Status "invalid"
}

if ($EnableM02CheckedPortIoBindingProbe -and -not $EnableM02SafeStubProbe) {
    Stop-EfiPreflight `
        -Marker (
            "EFI_PREFLIGHT_M02_SWITCHES " + $m02SwitchEvidence +
            " reason=checked_binding_requires_safe_stub") `
        -Reason "m02_checked_binding_requires_safe_stub" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath `
        -Status "invalid"
}

if ($EnableM02Com1OwnedPortIoProbe -and
    -not $EnableM02CheckedPortIoBindingProbe) {
    Stop-EfiPreflight `
        -Marker (
            "EFI_PREFLIGHT_M02_SWITCHES " + $m02SwitchEvidence +
            " reason=com1_owned_port_io_requires_checked_binding") `
        -Reason "m02_com1_owned_port_io_requires_checked_binding" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath `
        -Status "invalid"
}

if ($EnableM02OwnedCom1ReadyProbe -and
    -not $EnableM02Com1OwnedPortIoProbe) {
    Stop-EfiPreflight `
        -Marker (
            "EFI_PREFLIGHT_M02_SWITCHES " + $m02SwitchEvidence +
            " reason=owned_com1_ready_requires_com1_owned_port_io") `
        -Reason "m02_owned_com1_ready_requires_com1_owned_port_io" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath `
        -Status "invalid"
}

if ($EnableM02PanicSafeOwnedCom1Probe -and
    -not $EnableM02OwnedCom1ReadyProbe) {
    Stop-EfiPreflight `
        -Marker (
            "EFI_PREFLIGHT_M02_SWITCHES " + $m02SwitchEvidence +
            " reason=panic_safe_owned_com1_requires_owned_com1_ready") `
        -Reason "m02_panic_safe_owned_com1_requires_owned_com1_ready" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath `
        -Status "invalid"
}

if ($EnableM02OwnedX64GdtEncodingProbe -and
    -not $EnableM02PanicSafeOwnedCom1Probe) {
    Stop-EfiPreflight `
        -Marker (
            "EFI_PREFLIGHT_M02_SWITCHES " + $m02SwitchEvidence +
            " reason=owned_x64_gdt_encoding_requires_panic_safe_owned_com1") `
        -Reason "m02_owned_x64_gdt_encoding_requires_panic_safe_owned_com1" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath `
        -Status "invalid"
}

if ($EnableM02GuardedIstStackAllocationProbe -and
    -not $EnableM02OwnedX64GdtEncodingProbe) {
    Stop-EfiPreflight `
        -Marker (
            "EFI_PREFLIGHT_M02_SWITCHES " + $m02SwitchEvidence +
            " reason=guarded_ist_stack_allocation_requires_owned_x64_gdt_encoding") `
        -Reason "m02_guarded_ist_stack_allocation_requires_owned_x64_gdt_encoding" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath `
        -Status "invalid"
}

if ($EnableM02OwnedGdtTssTerminalProbe -and
    -not $EnableM02GuardedIstStackAllocationProbe) {
    Stop-EfiPreflight `
        -Marker (
            "EFI_PREFLIGHT_M02_SWITCHES " + $m02SwitchEvidence +
            " reason=owned_gdt_tss_terminal_requires_guarded_ist_stack_allocation") `
        -Reason "m02_owned_gdt_tss_terminal_requires_guarded_ist_stack_allocation" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath `
        -Status "invalid"
}

if ($EnableM02NormalizedInterruptFramePageProbe -and
    $EnableM02OwnedGdtTssTerminalProbe) {
    Stop-EfiPreflight `
        -Marker (
            "EFI_PREFLIGHT_M02_SWITCHES " + $m02SwitchEvidence +
            " reason=m02_normalized_interrupt_frame_page_probe_forbids_owned_gdt_tss_terminal") `
        -Reason "m02_normalized_interrupt_frame_page_probe_forbids_owned_gdt_tss_terminal" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath `
        -Status "invalid"
}

if ($EnableM02OwnedIdtGatePopulationProbe -and
    $EnableM02OwnedGdtTssTerminalProbe) {
    Stop-EfiPreflight `
        -Marker (
            "EFI_PREFLIGHT_M02_SWITCHES " + $m02SwitchEvidence +
            " reason=m02_owned_idt_gate_population_probe_forbids_owned_gdt_tss_terminal") `
        -Reason "m02_owned_idt_gate_population_probe_forbids_owned_gdt_tss_terminal" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath `
        -Status "invalid"
}

if ($EnableM02OwnedIdtGatePopulationProbe -and
    $EnableM02NormalizedInterruptFramePageProbe) {
    Stop-EfiPreflight `
        -Marker (
            "EFI_PREFLIGHT_M02_SWITCHES " + $m02SwitchEvidence +
            " reason=m02_owned_idt_gate_population_probe_forbids_normalized_interrupt_frame_page_probe") `
        -Reason "m02_owned_idt_gate_population_probe_forbids_normalized_interrupt_frame_page_probe" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath `
        -Status "invalid"
}

if ($EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe -and
    -not $EnableM04IndependentEndianConformanceProbe) {
    Stop-EfiPreflight `
        -Marker (
            "EFI_PREFLIGHT_M04_SWITCHES " + $m04SwitchEvidence +
            " reason=span_memory_conformance_requires_independent_endian_conformance") `
        -Reason "m04_span_memory_conformance_requires_independent_endian_conformance" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath `
        -Status "invalid"
}

if ($EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe -and
    $EnableM02OwnedGdtTssTerminalProbe) {
    Stop-EfiPreflight `
        -Marker (
            "EFI_PREFLIGHT_M04_SWITCHES " + $m04SwitchEvidence +
            " reason=span_memory_conformance_forbids_owned_gdt_tss_terminal") `
        -Reason "m04_span_memory_conformance_forbids_owned_gdt_tss_terminal" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath `
        -Status "invalid"
}

if ($EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe -and
    $EnableM02Ud2VectorStateProbe) {
    Stop-EfiPreflight `
        -Marker (
            "EFI_PREFLIGHT_M04_SWITCHES " + $m04SwitchEvidence +
            " reason=span_memory_conformance_forbids_m02_ud2_vector_state") `
        -Reason "m04_span_memory_conformance_forbids_m02_ud2_vector_state" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath `
        -Status "invalid"
}

if ($EnableM01LiveAllocatorProbe -and -not $EnableM01LoadedImageStackProbe) {
    Stop-EfiPreflight `
        -Marker (
            "EFI_PREFLIGHT_M01_SWITCHES " + $m01SwitchEvidence +
            " reason=live_allocator_requires_loaded_image_stack") `
        -Reason "m01_live_allocator_requires_loaded_image_stack" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath `
        -Status "invalid"
}

if ($EnableM01OwnedPagingProbe -and -not $EnableM01LiveAllocatorProbe) {
    Stop-EfiPreflight `
        -Marker (
            "EFI_PREFLIGHT_M01_SWITCHES " + $m01SwitchEvidence +
            " reason=owned_paging_requires_live_allocator") `
        -Reason "m01_owned_paging_requires_live_allocator" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath `
        -Status "invalid"
}

$dotnetCommand = Get-Command "dotnet" -CommandType Application -ErrorAction SilentlyContinue |
    Select-Object -First 1
if ($null -eq $dotnetCommand) {
    Stop-EfiPreflight `
        -Marker "EFI_PREFLIGHT_DOTNET" `
        -Reason "dotnet_host_missing" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath
}
$script:DotnetHostPath =
    (Resolve-Path -LiteralPath $dotnetCommand.Source).ProviderPath
$dotnetVersionResult = Invoke-M01BoundedProcess `
    -FilePath $script:DotnetHostPath `
    -Arguments @("--version") `
    -WorkingDirectory $repositoryRoot `
    -Operation "dotnet-sdk-version" `
    -TimeoutSeconds $BuildProcessTimeoutSeconds `
    -MaximumOutputCharacters $BuildProcessMaximumOutputCharacters
$dotnetVersionFailure = Get-M01BoundedProcessFailureReason `
    -Result $dotnetVersionResult
$dotnetVersionLines = @(
    @($dotnetVersionResult.OutputLines) |
        Where-Object { -not [string]::IsNullOrWhiteSpace([string] $_) } |
        ForEach-Object { [string] $_ }
)
if ($dotnetVersionFailure -cne "ok" -or
    $dotnetVersionLines.Count -ne 1 -or
    $dotnetVersionLines[0].Trim() -cnotmatch '^10\.0\.3[0-9]{2}$') {
    Stop-EfiPreflight `
        -Marker "EFI_PREFLIGHT_DOTNET" `
        -Reason ("dotnet_sdk_version_" + $dotnetVersionFailure) `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath `
        -Status "version_unavailable"
}
$dotnetSdkVersion = $dotnetVersionLines[0].Trim()
$dotnetSdkCandidate = Join-Path (
    Split-Path -Parent $script:DotnetHostPath) (
        "sdk\" + $dotnetSdkVersion)
$dotnetSdksCandidate = Join-Path $dotnetSdkCandidate "Sdks"
if (-not (Test-Path -LiteralPath $dotnetSdkCandidate -PathType Container) -or
    -not (Test-Path -LiteralPath $dotnetSdksCandidate -PathType Container)) {
    Stop-EfiPreflight `
        -Marker "EFI_PREFLIGHT_DOTNET" `
        -Reason "dotnet_sdk_layout_invalid" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath `
        -Status "version_unavailable"
}
$script:VerifiedDotnetSdkDirectory = (
    Resolve-Path -LiteralPath $dotnetSdkCandidate).ProviderPath.TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar)
$script:VerifiedMsbuildSdksPath = (
    Resolve-Path -LiteralPath $dotnetSdksCandidate).ProviderPath.TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar)
$script:VerifiedMsbuildExtensionsPath =
    $script:VerifiedDotnetSdkDirectory +
    [System.IO.Path]::DirectorySeparatorChar

Write-EfiPreflightLine (
    "EFI_PREFLIGHT_M01_SWITCHES status=ok " + $m01SwitchEvidence)
Write-EfiPreflightLine (
    "EFI_PREFLIGHT_M02_SWITCHES status=ok " + $m02SwitchEvidence)
Write-EfiPreflightLine (
    "EFI_PREFLIGHT_M04_SWITCHES status=ok " + $m04SwitchEvidence)

$m01SourceClosure = Test-M01SourceClosure `
    -RepositoryRoot $repositoryRoot `
    -ProjectPath $efiProjectPath `
    -Configuration $Configuration `
    -LoadedImageStackEnabled $EnableM01LoadedImageStackProbe.IsPresent `
    -LiveAllocatorEnabled $EnableM01LiveAllocatorProbe.IsPresent `
    -OwnedPagingEnabled $EnableM01OwnedPagingProbe.IsPresent `
    -M02SafeStubEnabled $EnableM02SafeStubProbe.IsPresent `
    -M02CheckedPortIoBindingEnabled $EnableM02CheckedPortIoBindingProbe.IsPresent `
    -M02Com1OwnedPortIoEnabled $EnableM02Com1OwnedPortIoProbe.IsPresent `
    -M02OwnedCom1ReadyEnabled $EnableM02OwnedCom1ReadyProbe.IsPresent `
    -M02PanicSafeOwnedCom1Enabled $EnableM02PanicSafeOwnedCom1Probe.IsPresent `
    -M02OwnedX64GdtEncodingEnabled $EnableM02OwnedX64GdtEncodingProbe.IsPresent `
    -M02GuardedIstStackAllocationEnabled $EnableM02GuardedIstStackAllocationProbe.IsPresent `
    -M02OwnedGdtTssTerminalEnabled $EnableM02OwnedGdtTssTerminalProbe.IsPresent `
    -M02NormalizedInterruptFramePageEnabled $EnableM02NormalizedInterruptFramePageProbe.IsPresent `
    -M02OwnedIdtGatePopulationEnabled $EnableM02OwnedIdtGatePopulationProbe.IsPresent `
    -M02IdtrContinuedExecutionEnabled $EnableM02IdtrContinuedExecutionProbe.IsPresent `
    -M02Ud2VectorStateEnabled $EnableM02Ud2VectorStateProbe.IsPresent `
    -M02PageFaultCr2ErrorCodeEnabled $EnableM02PageFaultCr2ErrorCodeProbe.IsPresent `
    -M02DoubleFaultIstEnabled $EnableM02DoubleFaultIstProbe.IsPresent `
    -M02UnexpectedVectorPanicEnabled $EnableM02UnexpectedVectorPanicProbe.IsPresent `
    -M04IndependentEndianConformanceEnabled $EnableM04IndependentEndianConformanceProbe.IsPresent `
    -M04SpanMemoryConformanceEnabled $EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe.IsPresent `
    -M04ArithmeticConformanceEnabled $EnableM04ArithmeticConformanceProbe.IsPresent `
    -M04LayoutContractEnabled $EnableM04LayoutContractProbe.IsPresent `
    -M04ManagedHeapPageRangeEnabled $EnableM04ManagedHeapPageRangeProbe.IsPresent `
    -M04RhpNewFastEnabled $EnableM04RhpNewFastProbe.IsPresent `
    -M04RhpNewFastBeforeBindFailureEnabled $EnableM04RhpNewFastBeforeBindFailureProbe.IsPresent `
    -M04RhpNewFastExhaustionFailureEnabled $EnableM04RhpNewFastExhaustionFailureProbe.IsPresent `
    -M04SzArrayAllocationEnabled $EnableM04SzArrayAllocationProbe.IsPresent `
    -M04SzArrayAllocationBeforeBindEnabled $EnableM04SzArrayAllocationBeforeBindProbe.IsPresent `
    -M04SzArrayAllocationExhaustionEnabled $EnableM04SzArrayAllocationExhaustionProbe.IsPresent `
    -M04StringAllocationEnabled $EnableM04StringAllocationProbe.IsPresent `
    -M04StringAllocationBeforeBindEnabled $EnableM04StringAllocationBeforeBindProbe.IsPresent `
    -M04StringAllocationExhaustionEnabled $EnableM04StringAllocationExhaustionProbe.IsPresent `
    -M04StringAllocationInvalidLengthEnabled $EnableM04StringAllocationInvalidLengthProbe.IsPresent `
    -M03MemoryMapCaptureEnabled $EnableM03MemoryMapCaptureProbe.IsPresent `
    -M03FirmwareEntropySeedEnabled $EnableM03FirmwareEntropySeedProbe.IsPresent `
    -M04TypeManagerModuleRegistrationEnabled $EnableM04TypeManagerModuleRegistrationProbe.IsPresent `
    -M04X64UnwindMetadataEnabled $EnableM04X64UnwindMetadataProbe.IsPresent `
    -M04SimpleThrowCatchEnabled $EnableM04SimpleThrowCatchProbe.IsPresent `
    -M04GcStaticRootMaterializationEnabled $EnableM04GcStaticRootMaterializationProbe.IsPresent `
    -M04SeededCollectorChurnEnabled $EnableM04SeededCollectorChurnProbe.IsPresent `
    -M03TypedExitBootServicesCallEnabled $EnableM03TypedExitBootServicesCallProbe.IsPresent `
    -M03FirstRealExitBootServicesEnabled $EnableM03FirstRealExitBootServicesProbe.IsPresent `
    -M03FirmwareAdapterRevocationEnabled $EnableM03FirmwareAdapterRevocationProbe.IsPresent `
    -M03OwnedSubsystemSurvivalEnabled $EnableM03OwnedSubsystemSurvivalProbe.IsPresent `
    -M03OwnedSubsystemPanicEnabled $EnableM03OwnedSubsystemPanicProbe.IsPresent `
    -M04ClassConstructorExecutionEnabled $EnableM04ClassConstructorExecutionProbe.IsPresent `
    -M04InterfaceDispatchEnabled $EnableM04InterfaceDispatchProbe.IsPresent `
    -M04CheckedReferenceBarrierEnabled $EnableM04CheckedReferenceBarrierProbe.IsPresent `
    -UefiGoneSimulationEnabled $EnableUefiGoneSimulationProbe.IsPresent `
    -ExitBootServicesFreshMapEnabled $EnableExitBootServicesFreshMapProbe.IsPresent `
    -AcpiPhysicalDiscoveryEnabled $EnableAcpiPhysicalDiscoveryProbe.IsPresent
if (-not $m01SourceClosure.Succeeded) {
    Stop-EfiPreflight `
        -Marker (
            "EFI_PREFLIGHT_M01_SOURCE_CLOSURE" +
            " reason=" + $m01SourceClosure.Reason +
            " detail=" + (Format-EvidenceValue $m01SourceClosure.Detail)) `
        -Reason ("m01_source_closure_" + $m01SourceClosure.Reason) `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath `
        -Status "invalid"
}

Write-EfiPreflightLine (
    "EFI_PREFLIGHT_M01_SOURCE_CLOSURE status=ok" +
    " reviewed=" + $m01SourceClosure.ReviewedSourceCount +
    " prerequisites=" + $m01SourceClosure.PrerequisiteSourceCount +
    " compileItems=" + $m01SourceClosure.CompileItemCount +
    " prerequisiteGap=UefiMemoryType")

Write-EfiPreflightLine (
    "EFI_PREFLIGHT_DOTNET path=" +
    (Format-EvidenceValue $script:DotnetHostPath) +
    " sdkVersion=" +
    (Format-EvidenceValue $dotnetSdkVersion))

if ([string]::IsNullOrWhiteSpace($VsWherePath)) {
    $programFilesX86 = ${env:ProgramFiles(x86)}
    if (-not [string]::IsNullOrWhiteSpace($programFilesX86)) {
        $VsWherePath = Join-Path $programFilesX86 "Microsoft Visual Studio\Installer\vswhere.exe"
    }
}

if ($MsvcLinkerSearchRoot.Count -gt 0) {
    $linkerRoots = $MsvcLinkerSearchRoot
}
else {
    $linkerRoots = Get-DefaultMsvcLinkerSearchRoot -VsWhere $VsWherePath
}

$linkerPath = Resolve-ToolPath `
    -SearchRoot $linkerRoots `
    -FileName "link.exe" `
    -PathPattern "Hostx64\\x64" `
    -Validator ${function:Test-PeX64Executable}
if ($null -eq $linkerPath) {
    Stop-EfiPreflight `
        -Marker "EFI_PREFLIGHT_MSVC_LINKER" `
        -Reason "platform_linker_missing" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath
}

$linkerVersion = Get-FileVersionEvidence -Path $linkerPath
if ($linkerVersion -eq "unknown") {
    Stop-EfiPreflight `
        -Marker "EFI_PREFLIGHT_MSVC_LINKER" `
        -Reason "platform_linker_version_unavailable" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath `
        -Status "version_unavailable"
}
Write-EfiPreflightLine (
    "EFI_PREFLIGHT_MSVC_LINKER path=" + (Format-EvidenceValue $linkerPath) +
    " version=" + (Format-EvidenceValue $linkerVersion))

if ($WindowsSdkLibSearchRoot.Count -gt 0) {
    $sdkRoots = $WindowsSdkLibSearchRoot
}
else {
    $sdkRoots = Get-DefaultWindowsSdkLibSearchRoot
}

$sdkLibPath = Resolve-ToolPath `
    -SearchRoot $sdkRoots `
    -FileName "kernel32.lib" `
    -PathPattern "\\um\\x64\\" `
    -Validator ${function:Test-CoffArchive}
if ($null -eq $sdkLibPath) {
    Stop-EfiPreflight `
        -Marker "EFI_PREFLIGHT_WINDOWS_SDK" `
        -Reason "windows_sdk_um_x64_missing" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath
}

$sdkVersion = Get-VersionFromPath -Path $sdkLibPath
if ($sdkVersion -eq "unknown") {
    Stop-EfiPreflight `
        -Marker "EFI_PREFLIGHT_WINDOWS_SDK" `
        -Reason "windows_sdk_version_unavailable" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath `
        -Status "version_unavailable"
}
Write-EfiPreflightLine (
    "EFI_PREFLIGHT_WINDOWS_SDK path=" + (Format-EvidenceValue $sdkLibPath) +
    " version=" + (Format-EvidenceValue $sdkVersion))

$ilCompilerPackageId = "runtime.$Runtime.Microsoft.DotNet.ILCompiler"
$nativeAotRuntimePackId = "Microsoft.NETCore.App.Runtime.NativeAOT.$Runtime"
$ilCompilerPackageVersion = Get-PackageReferenceVersion `
    -ProjectPath $efiProjectPath `
    -PackageId $ilCompilerPackageId
$nativeAotRuntimePackVersion = Get-PackageReferenceVersion `
    -ProjectPath $efiProjectPath `
    -PackageId $nativeAotRuntimePackId
if ([string]::IsNullOrWhiteSpace($nativeAotRuntimePackVersion)) {
    $nativeAotRuntimePackVersion = Get-PackageReferenceVersion `
        -ProjectPath $efiProjectPath `
        -PackageId "Microsoft.DotNet.ILCompiler"
}

if (-not [string]::IsNullOrWhiteSpace($env:NUGET_PACKAGES)) {
    $nugetPackageRoot = $env:NUGET_PACKAGES
}
else {
    $nugetPackageRoot = Join-Path $env:USERPROFILE ".nuget\packages"
}

$defaultIlCompilerPath = ""
if (-not [string]::IsNullOrWhiteSpace($ilCompilerPackageVersion)) {
    $defaultIlCompilerPath = Join-Path $nugetPackageRoot (
        $ilCompilerPackageId.ToLowerInvariant() +
        "\" +
        $ilCompilerPackageVersion +
        "\tools\ilc.exe")
}

$resolvedIlCompilerPath = Resolve-ExecutableToolPath `
    -RequestedPath $IlCompilerPath `
    -SearchRoot $IlCompilerSearchRoot `
    -FileName "ilc.exe" `
    -DefaultPath @($defaultIlCompilerPath) `
    -CommandName @() `
    -Validator ${function:Test-PeX64Executable}
if ($null -eq $resolvedIlCompilerPath) {
    Stop-EfiPreflight `
        -Marker "EFI_PREFLIGHT_ILCOMPILER" `
        -Reason "ilcompiler_missing" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath
}

$ilCompilerFileVersion = Get-FileVersionEvidence -Path $resolvedIlCompilerPath
if ([string]::IsNullOrWhiteSpace($ilCompilerPackageVersion) -or
    $ilCompilerFileVersion -eq "unknown") {
    Stop-EfiPreflight `
        -Marker "EFI_PREFLIGHT_ILCOMPILER" `
        -Reason "ilcompiler_version_unavailable" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath `
        -Status "version_unavailable"
}
Write-EfiPreflightLine (
    "EFI_PREFLIGHT_ILCOMPILER path=" + (Format-EvidenceValue $resolvedIlCompilerPath) +
    " packageVersion=" + (Format-EvidenceValue $ilCompilerPackageVersion) +
    " fileVersion=" + (Format-EvidenceValue $ilCompilerFileVersion))

$defaultNativeAotRuntimePackPath = ""
if (-not [string]::IsNullOrWhiteSpace($nativeAotRuntimePackVersion)) {
    $defaultNativeAotRuntimePackPath = Join-Path $nugetPackageRoot (
        $nativeAotRuntimePackId.ToLowerInvariant() +
        "\" +
        $nativeAotRuntimePackVersion)
}

$resolvedNativeAotRuntimePackPath = Resolve-NativeAotRuntimePackPath `
    -RequestedPath $NativeAotRuntimePackPath `
    -SearchRoot $NativeAotRuntimePackSearchRoot `
    -DefaultPath $defaultNativeAotRuntimePackPath
if ($null -eq $resolvedNativeAotRuntimePackPath) {
    Stop-EfiPreflight `
        -Marker "EFI_PREFLIGHT_NATIVEAOT_RUNTIME_PACK" `
        -Reason "nativeaot_runtime_pack_missing" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath
}
if ([string]::IsNullOrWhiteSpace($nativeAotRuntimePackVersion)) {
    Stop-EfiPreflight `
        -Marker "EFI_PREFLIGHT_NATIVEAOT_RUNTIME_PACK" `
        -Reason "nativeaot_runtime_pack_version_unavailable" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath `
        -Status "version_unavailable"
}

$runtimeListPath = Join-Path $resolvedNativeAotRuntimePackPath "data\RuntimeList.xml"
Write-EfiPreflightLine (
    "EFI_PREFLIGHT_NATIVEAOT_RUNTIME_PACK path=" +
    (Format-EvidenceValue $resolvedNativeAotRuntimePackPath) +
    " packageVersion=" + (Format-EvidenceValue $nativeAotRuntimePackVersion) +
    " manifest=" + (Format-EvidenceValue $runtimeListPath))

$defaultQemuPaths = New-Object System.Collections.Generic.List[string]
$defaultQemuImgPaths = New-Object System.Collections.Generic.List[string]
$defaultOvmfPaths = New-Object System.Collections.Generic.List[string]
if (-not [string]::IsNullOrWhiteSpace($env:QEMU)) {
    $defaultQemuPaths.Add($env:QEMU)
}
if (-not [string]::IsNullOrWhiteSpace($env:QEMU_IMG)) {
    $defaultQemuImgPaths.Add($env:QEMU_IMG)
}
foreach ($candidate in @($env:OS_OVMF_CODE, $env:OVMF_CODE, $env:QEMU_OVMF_CODE)) {
    if (-not [string]::IsNullOrWhiteSpace($candidate)) {
        $defaultOvmfPaths.Add($candidate)
    }
}
if (-not [string]::IsNullOrWhiteSpace($env:ProgramFiles)) {
    $qemuInstallRoot = Join-Path $env:ProgramFiles "qemu"
    $defaultQemuPaths.Add((Join-Path $qemuInstallRoot "qemu-system-x86_64.exe"))
    $defaultQemuImgPaths.Add((Join-Path $qemuInstallRoot "qemu-img.exe"))
    $defaultOvmfPaths.Add((Join-Path $qemuInstallRoot "share\edk2-x86_64-code.fd"))
    $defaultOvmfPaths.Add((Join-Path $qemuInstallRoot "share\OVMF_CODE.fd"))
}

$resolvedQemuPath = Resolve-ExecutableToolPath `
    -RequestedPath $QemuPath `
    -SearchRoot $QemuSearchRoot `
    -FileName "qemu-system-x86_64.exe" `
    -DefaultPath $defaultQemuPaths `
    -CommandName @("qemu-system-x86_64.exe", "qemu-system-x86_64")
if ($null -eq $resolvedQemuPath) {
    Stop-EfiPreflight `
        -Marker "EFI_PREFLIGHT_QEMU" `
        -Reason "qemu_not_found" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath
}

$qemuVersionEvidence = Get-ExecutableVersionEvidence `
    -Path $resolvedQemuPath `
    -TimeoutMilliseconds $VersionProbeTimeoutMilliseconds
if ($qemuVersionEvidence.Status -ne "ok") {
    $reason = if ($qemuVersionEvidence.Status -eq "timeout") {
        "qemu_version_timeout"
    }
    else {
        "qemu_version_unavailable"
    }
    Stop-EfiPreflight `
        -Marker "EFI_PREFLIGHT_QEMU" `
        -Reason $reason `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath `
        -Status $qemuVersionEvidence.Status
}
$qemuVersion = $qemuVersionEvidence.Version
if ($qemuVersion -notmatch '(?i)\bQEMU\b.*\bversion\b') {
    Stop-EfiPreflight `
        -Marker "EFI_PREFLIGHT_QEMU" `
        -Reason "qemu_version_unrecognized" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath `
        -Status "version_unrecognized"
}
Write-EfiPreflightLine (
    "EFI_PREFLIGHT_QEMU path=" + (Format-EvidenceValue $resolvedQemuPath) +
    " version=" + (Format-EvidenceValue $qemuVersion))

$resolvedQemuImgPath = Resolve-ExecutableToolPath `
    -RequestedPath $QemuImgPath `
    -SearchRoot $QemuImgSearchRoot `
    -FileName "qemu-img.exe" `
    -DefaultPath $defaultQemuImgPaths `
    -CommandName @("qemu-img.exe", "qemu-img")
if ($null -eq $resolvedQemuImgPath) {
    Stop-EfiPreflight `
        -Marker "EFI_PREFLIGHT_QEMU_IMG" `
        -Reason "qemu_img_not_found" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath
}

$qemuImgVersionEvidence = Get-ExecutableVersionEvidence `
    -Path $resolvedQemuImgPath `
    -TimeoutMilliseconds $VersionProbeTimeoutMilliseconds
if ($qemuImgVersionEvidence.Status -ne "ok") {
    $reason = if ($qemuImgVersionEvidence.Status -eq "timeout") {
        "qemu_img_version_timeout"
    }
    else {
        "qemu_img_version_unavailable"
    }
    Stop-EfiPreflight `
        -Marker "EFI_PREFLIGHT_QEMU_IMG" `
        -Reason $reason `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath `
        -Status $qemuImgVersionEvidence.Status
}
$qemuImgVersion = $qemuImgVersionEvidence.Version
if ($qemuImgVersion -notmatch '(?i)\bqemu-img\b.*\bversion\b') {
    Stop-EfiPreflight `
        -Marker "EFI_PREFLIGHT_QEMU_IMG" `
        -Reason "qemu_img_version_unrecognized" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath `
        -Status "version_unrecognized"
}
Write-EfiPreflightLine (
    "EFI_PREFLIGHT_QEMU_IMG path=" + (Format-EvidenceValue $resolvedQemuImgPath) +
    " version=" + (Format-EvidenceValue $qemuImgVersion))

$ovmfEvidence = $null
if (-not [string]::IsNullOrWhiteSpace($OvmfCodePath)) {
    $ovmfEvidence = Get-CachedOvmfCodeImageEvidence -Path $OvmfCodePath
    $resolvedOvmfCodePath = if ($ovmfEvidence.Status -eq "ok") {
        $ovmfEvidence.Path
    }
    else {
        $null
    }
}
elseif ($OvmfSearchRoot.Count -gt 0) {
    $resolvedOvmfCodePath = Resolve-ToolPath `
        -SearchRoot $OvmfSearchRoot `
        -FileName "edk2-x86_64-code.fd" `
        -PathPattern "" `
        -Validator ${function:Test-OvmfCodeImage}
    if ($null -eq $resolvedOvmfCodePath) {
        $resolvedOvmfCodePath = Resolve-ToolPath `
            -SearchRoot $OvmfSearchRoot `
            -FileName "OVMF_CODE.fd" `
            -PathPattern "" `
            -Validator ${function:Test-OvmfCodeImage}
    }
    if ($null -ne $resolvedOvmfCodePath) {
        $ovmfEvidence = Get-CachedOvmfCodeImageEvidence `
            -Path $resolvedOvmfCodePath
    }
}
else {
    $resolvedOvmfCodePath = $null
    foreach ($candidate in $defaultOvmfPaths) {
        $candidateEvidence = Get-CachedOvmfCodeImageEvidence -Path $candidate
        if ($candidateEvidence.Status -eq "ok") {
            $ovmfEvidence = $candidateEvidence
            $resolvedOvmfCodePath = $candidateEvidence.Path
            break
        }
    }
}

if ($null -eq $resolvedOvmfCodePath) {
    $ovmfFailureStatus = "missing"
    if ($null -ne $ovmfEvidence -and $ovmfEvidence.Status -ne "ok") {
        $ovmfFailureStatus = $ovmfEvidence.Status
    }
    elseif (@($script:OvmfCodeImageEvidenceAttempts | Where-Object {
                $_.Status -eq "evidence_unavailable"
            }).Count -gt 0) {
        $ovmfFailureStatus = "evidence_unavailable"
    }
    elseif (@($script:OvmfCodeImageEvidenceAttempts | Where-Object {
                $_.Status -eq "invalid"
            }).Count -gt 0) {
        $ovmfFailureStatus = "invalid"
    }

    if ($ovmfFailureStatus -eq "evidence_unavailable") {
        Stop-EfiPreflight `
            -Marker "EFI_PREFLIGHT_OVMF" `
            -Reason "ovmf_evidence_unavailable" `
            -LogFullPath $logFullPath `
            -ReportedLogPath $LogPath `
            -Status "evidence_unavailable"
    }
    if ($ovmfFailureStatus -eq "invalid") {
        Stop-EfiPreflight `
            -Marker "EFI_PREFLIGHT_OVMF" `
            -Reason "ovmf_invalid" `
            -LogFullPath $logFullPath `
            -ReportedLogPath $LogPath `
            -Status "invalid"
    }
    Stop-EfiPreflight `
        -Marker "EFI_PREFLIGHT_OVMF" `
        -Reason "ovmf_not_found" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath
}

$ovmfHash = $ovmfEvidence.Sha256
$ovmfLength = $ovmfEvidence.Length
Write-EfiPreflightLine (
    "EFI_PREFLIGHT_OVMF path=" + (Format-EvidenceValue $ovmfEvidence.Path) +
    " bytes=" + $ovmfLength +
    " sha256=" + $ovmfHash)

Write-EfiPreflightLine "EFI_PREFLIGHT_OK"
Save-EfiPreflightLog -Path $logFullPath

if ($PreflightOnly) {
    exit 0
}

$canonicalRootPropsPath = Join-Path $repositoryRoot "Directory.Build.props"
$canonicalRootTargetsPath = Join-Path $repositoryRoot "Directory.Build.targets"
$dotnetSdkDirectory = $script:VerifiedDotnetSdkDirectory
$languageTargetsPath = Join-Path (
    $dotnetSdkDirectory) "Microsoft.CSharp.targets"
$csharpTargetsPath = Join-Path (
    $dotnetSdkDirectory) "Microsoft.CSharp.CurrentVersion.targets"
$commonTargetsPath = Join-Path (
    $dotnetSdkDirectory) "Microsoft.Common.CurrentVersion.targets"
foreach ($sdkTargetPath in @(
        $languageTargetsPath,
        $csharpTargetsPath,
        $commonTargetsPath)) {
    if (-not (Test-Path -LiteralPath $sdkTargetPath -PathType Leaf)) {
        Write-Host (
            "TEST_FAIL efi.shell.publish sdk_target_missing path=" +
            (Format-EvidenceValue $sdkTargetPath))
        exit 1
    }
}
$nativeIntermediateDirectory = Join-Path $repositoryRoot (
    "src\OS\obj\$Configuration\net10.0\$Runtime\native")
$managedIntermediateDirectory = Join-Path $repositoryRoot (
    "src\OS\obj\$Configuration\net10.0\$Runtime")
$managedOutputDirectory = Join-Path $repositoryRoot (
    "src\OS\bin\$Configuration\net10.0\$Runtime")
$projectExtensionsDirectory = Join-Path $managedIntermediateDirectory "nuget"
$restoreSourceDirectory = Join-Path (
    $managedIntermediateDirectory) "restore-source"
$restorePackagesDirectory = Join-Path (
    $managedIntermediateDirectory) "packages"
$restoreConfigPath = Join-Path $restoreSourceDirectory "NuGet.Config"
$globalPackageCacheRoot = [System.IO.Path]::GetFullPath($nugetPackageRoot)
$projectExtensionsArgumentPath = $projectExtensionsDirectory.Replace(
    "\",
    "/").TrimEnd("/") + "/"
$projectAssetsPath = Join-Path $projectExtensionsDirectory "project.assets.json"
$projectAssetsArgumentPath = $projectAssetsPath.Replace("\", "/")
$producerMsbuildIsolation = Get-M01MsbuildIsolationContract `
    -ProjectExtensionsDirectory $projectExtensionsDirectory
$projectExtensionsRelativePath = (
    "src/OS/obj/" +
    $Configuration +
    "/net10.0/" +
    $Runtime +
    "/nuget")
$restoreSourceRelativePath = (
    "src/OS/obj/" +
    $Configuration +
    "/net10.0/" +
    $Runtime +
    "/restore-source")
$expectedRestoreClosureRelativePaths = @(
    ($projectExtensionsRelativePath + "/OS.csproj.nuget.dgspec.json")
    ($projectExtensionsRelativePath + "/OS.csproj.nuget.g.props")
    ($projectExtensionsRelativePath + "/OS.csproj.nuget.g.targets")
    ($projectExtensionsRelativePath + "/project.assets.json")
    ($projectExtensionsRelativePath + "/project.nuget.cache")
)
$expectedRestoreSourceRelativePaths = @(
    ($restoreSourceRelativePath + "/NuGet.Config")
)
foreach ($archive in $script:ExpectedRestoreArchives) {
    $expectedRestoreSourceRelativePaths += (
        $restoreSourceRelativePath +
        "/" +
        [string] $archive.FileName)
}

$ilCompilerBuildPackageRoot = Join-Path (
    $restorePackagesDirectory) "microsoft.dotnet.ilcompiler\10.0.8"
$ilCompilerHostPackageRoot = Join-Path (
    $restorePackagesDirectory) (
        "runtime.win-x64.microsoft.dotnet.ilcompiler\10.0.8")
$ilLinkPackageRoot = Join-Path (
    $restorePackagesDirectory) "microsoft.net.illink.tasks\10.0.10"
$actualIlCompilerPath = Join-Path (
    $ilCompilerHostPackageRoot) "tools\ilc.exe"
$ilCompilerTargetsPath = Join-Path (
    $ilCompilerBuildPackageRoot) (
        "build\Microsoft.DotNet.ILCompiler.SingleEntry.targets")
$ilLinkTargetsPath = Join-Path (
    $ilLinkPackageRoot) "build\Microsoft.NET.ILLink.targets"
$ilLinkTasksAssemblyPath = Join-Path (
    $ilLinkPackageRoot) "tools\net\ILLink.Tasks.dll"
$ilcBuildTasksPath = Join-Path (
    $ilCompilerBuildPackageRoot) (
        "tools\netstandard\ILCompiler.Build.Tasks.dll")
$ilcToolsPath = (
    Join-Path $ilCompilerHostPackageRoot "tools") +
    [System.IO.Path]::DirectorySeparatorChar
$ilcFrameworkPath = (
    Join-Path $resolvedNativeAotRuntimePackPath (
        "runtimes\$Runtime\lib")) +
    [System.IO.Path]::DirectorySeparatorChar
$ilcFrameworkNativePath = (
    Join-Path $resolvedNativeAotRuntimePackPath (
        "runtimes\$Runtime\native")) +
    [System.IO.Path]::DirectorySeparatorChar
$ilcSdkPath = $ilcFrameworkNativePath
$ilcMibcPath = (
    Join-Path $resolvedNativeAotRuntimePackPath "mibc") +
    [System.IO.Path]::DirectorySeparatorChar

$projectExtensionArguments = @(
    ("-p:MSBuildProjectExtensionsPath=" + $projectExtensionsArgumentPath),
    ("-p:RestoreOutputPath=" + $projectExtensionsArgumentPath),
    ("-p:ProjectAssetsFile=" + $projectAssetsArgumentPath),
    ("-p:RestorePackagesPath=" + $restorePackagesDirectory),
    ("-p:NuGetPackageRoot=" + $restorePackagesDirectory),
    "-p:ImportProjectExtensionProps=false",
    "-p:ImportProjectExtensionTargets=false",
    "-p:ExcludeRestorePackageImports=true"
)
$projectExtensionArguments += $producerMsbuildIsolation.Arguments
$pinnedPackageArguments = @(
    ("-p:ILCompilerTargetsPath=" + $ilCompilerTargetsPath),
    ("-p:ILLinkTargetsPath=" + $ilLinkTargetsPath),
    ("-p:ILLinkTasksAssembly=" + $ilLinkTasksAssemblyPath),
    ("-p:IlcBuildTasksPath=" + $ilcBuildTasksPath),
    ("-p:IlcHostPackagePath=" + $ilCompilerHostPackageRoot),
    ("-p:ClrosIlcHostPackagePath=" + $ilCompilerHostPackageRoot),
    ("-p:IlcToolsPath=" + $ilcToolsPath),
    ("-p:RuntimePackagePath=" + $resolvedNativeAotRuntimePackPath),
    ("-p:ClrosRuntimePackagePath=" + $resolvedNativeAotRuntimePackPath),
    ("-p:IlcFrameworkPath=" + $ilcFrameworkPath),
    ("-p:IlcFrameworkNativePath=" + $ilcFrameworkNativePath),
    ("-p:IlcSdkPath=" + $ilcSdkPath),
    ("-p:IlcMibcPath=" + $ilcMibcPath),
    "-p:RunILLink=false",
    "-p:NativeCompilationDuringPublish=true",
    "-p:IlcMultiModule=false",
    "-p:DisableFrameworkLibGeneration=true",
    "-p:IlcUseEnvironmentalTools=true",
    "-p:PublishAotUsingRuntimePack=false",
    "-p:IlcPgoOptimize=false",
    "-p:PublishTrimmed=true",
    "-p:ImportRuntimeIlcPackageTargetDependsOn=",
    "-p:IlcSetupPropertiesDependsOn=ImportRuntimeIlcPackageTarget",
    "-p:IlcDynamicBuildPropertyDependencies=SetupProperties",
    "-p:ImportNuGetBuildTasksPackTargetsFromSdk=false",
    "-p:NuGetBuildTasksPackTargets=",
    ("-p:LanguageTargets=" + $languageTargetsPath),
    ("-p:CSharpTargetsPath=" + $csharpTargetsPath),
    ("-p:CommonTargetsPath=" + $commonTargetsPath)
)
$arguments = @(
    "publish",
    $efiProjectPath,
    "-noAutoResponse",
    "-c", $Configuration,
    "-r", $Runtime,
    "--nologo",
    ("-p:DirectoryBuildPropsPath=" + $canonicalRootPropsPath),
    ("-p:DirectoryBuildTargetsPath=" + $canonicalRootTargetsPath),
    "-p:ImportDirectoryBuildProps=true",
    "-p:ImportDirectoryBuildTargets=true",
    "-p:CustomBeforeMicrosoftCommonProps=",
    "-p:CustomAfterMicrosoftCommonProps=",
    "-p:CustomBeforeDirectoryBuildProps=",
    "-p:CustomAfterDirectoryBuildProps=",
    "-p:CustomBeforeDirectoryBuildTargets=",
    "-p:CustomAfterDirectoryBuildTargets=",
    "-p:CustomBeforeMicrosoftCommonTargets=",
    "-p:CustomAfterMicrosoftCommonTargets=",
    "-p:CustomBeforeMicrosoftCSharpTargets=",
    "-p:CustomAfterMicrosoftCSharpTargets=",
    "-p:CustomBeforeMicrosoftCommonCrossTargetingTargets=",
    "-p:CustomAfterMicrosoftCommonCrossTargetingTargets=",
    "-p:BeforeMicrosoftNETSdkTargets=",
    "-p:AfterMicrosoftNETSdkTargets=",
    "-p:CppLinker=$linkerPath",
    "-p:IlcGenerateMapFile=true",
    "-p:NoIncremental=true"
)
$arguments += $projectExtensionArguments
$arguments += $pinnedPackageArguments
$arguments += "--no-restore"

if ($EnableM01LoadedImageStackProbe) {
    $arguments += "-p:EnableM01LoadedImageStackProbe=true"
    Write-Host "EFI_PROBE M01_LOADED_IMAGE_STACK"
}

if ($EnableM01LiveAllocatorProbe) {
    $arguments += "-p:EnableM01LiveAllocatorProbe=true"
    Write-Host "EFI_PROBE M01_LIVE_ALLOCATOR"
}

if ($EnableM01OwnedPagingProbe) {
    $arguments += "-p:EnableM01OwnedPagingProbe=true"
    Write-Host "EFI_PROBE M01_OWNED_PAGING"
}

if ($EnableM02SafeStubProbe) {
    $arguments += "-p:EnableM02SafeStubProbe=true"
    Write-Host "EFI_PROBE M02_SAFE_STUB"
}

if ($EnableM02CheckedPortIoBindingProbe) {
    $arguments += "-p:EnableM02CheckedPortIoBindingProbe=true"
    Write-Host "EFI_PROBE M02_CHECKED_PORT_IO_BINDING"
}

if ($EnableM02Com1OwnedPortIoProbe) {
    $arguments += "-p:EnableM02Com1OwnedPortIoProbe=true"
    Write-Host "EFI_PROBE M02_COM1_OWNED_PORT_IO"
}

if ($EnableM02OwnedCom1ReadyProbe) {
    $arguments += "-p:EnableM02OwnedCom1ReadyProbe=true"
    Write-Host "EFI_PROBE M02_OWNED_COM1_READY"
}

if ($EnableM02PanicSafeOwnedCom1Probe) {
    $arguments += "-p:EnableM02PanicSafeOwnedCom1Probe=true"
    Write-Host "EFI_PROBE M02_PANIC_SAFE_OWNED_COM1"
}

if ($EnableM02OwnedX64GdtEncodingProbe) {
    $arguments += "-p:EnableM02OwnedX64GdtEncodingProbe=true"
    Write-Host "EFI_PROBE M02_OWNED_X64_GDT_ENCODING"
}

if ($EnableM02GuardedIstStackAllocationProbe) {
    $arguments += "-p:EnableM02GuardedIstStackAllocationProbe=true"
    Write-Host "EFI_PROBE M02_GUARDED_IST_STACK_ALLOCATION"
}

if ($EnableM02OwnedGdtTssTerminalProbe) {
    $arguments += "-p:EnableM02OwnedGdtTssTerminalProbe=true"
    Write-Host "EFI_PROBE M02_OWNED_GDT_TSS_TERMINAL"
}

if ($EnableM02NormalizedInterruptFramePageProbe) {
    $arguments += "-p:EnableM02NormalizedInterruptFramePageProbe=true"
    Write-Host "EFI_PROBE M02_NORMALIZED_INTERRUPT_FRAME_PAGE"
}

if ($EnableM02OwnedIdtGatePopulationProbe) {
    $arguments += "-p:EnableM02OwnedIdtGatePopulationProbe=true"
    Write-Host "EFI_PROBE M02_OWNED_IDT_GATE_POPULATION"
}

if ($EnableM02IdtrContinuedExecutionProbe) {
    $arguments += "-p:EnableM02IdtrContinuedExecutionProbe=true"
    Write-Host "EFI_PROBE M02_IDTR_CONTINUED_EXECUTION"
}

if ($EnableM02Ud2VectorStateProbe) {
    $arguments += "-p:EnableM02Ud2VectorStateProbe=true"
    Write-Host "EFI_PROBE M02_UD2_VECTOR_STATE"
}
if ($EnableM02PageFaultCr2ErrorCodeProbe) {
    $arguments += "-p:EnableM02PageFaultCr2ErrorCodeProbe=true"
    Write-Host "EFI_PROBE M02_PAGE_FAULT_CR2_ERROR_CODE"
}

if ($EnableM02DoubleFaultIstProbe) {
    $arguments += "-p:EnableM02DoubleFaultIstProbe=true"
    Write-Host "EFI_PROBE M02_DOUBLE_FAULT_IST"
}
if ($EnableM02UnexpectedVectorPanicProbe) {
    $arguments += "-p:EnableM02UnexpectedVectorPanicProbe=true"
    Write-Host "EFI_PROBE M02_UNEXPECTED_VECTOR_PANIC"
}

if ($EnableM04IndependentEndianConformanceProbe) {
    $arguments +=
        "-p:EnableM04IndependentEndianConformanceProbe=true"
    Write-Host "EFI_PROBE M04_INDEPENDENT_ENDIAN_CONFORMANCE"
}

if ($EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe) {
    $arguments +=
        "-p:EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe=true"
    Write-Host "EFI_PROBE M04_SPAN_OVERLAP_MEMORY_CAST_UNSAFE_WIDTH_CONFORMANCE"
}
if ($EnableM04ArithmeticConformanceProbe) {
    $arguments +=
        "-p:EnableM04ArithmeticConformanceProbe=true"
    Write-Host "EFI_PROBE M04_ARITHMETIC_CONFORMANCE"
}
if ($EnableM04LayoutContractProbe) {
    $arguments +=
        "-p:EnableM04LayoutContractProbe=true"
    Write-Host "EFI_PROBE M04_LAYOUT_CONTRACT"
}
if ($EnableM04ManagedHeapPageRangeProbe) {
    $arguments +=
        "-p:EnableM04ManagedHeapPageRangeProbe=true"
    $arguments += "-p:EnableUefiGoneSimulationProbe=false"
    $arguments += "-p:EnableExitBootServicesFreshMapProbe=false"
    $arguments += "-p:EnableAcpiPhysicalDiscoveryProbe=false"
    Write-Host "EFI_PROBE M04_MANAGED_HEAP_PAGE_RANGE"
}

if ($EnableM04RhpNewFastProbe) {
    $arguments += "-p:EnableM04RhpNewFastProbe=true"
    Write-Host "EFI_PROBE M04_RHP_NEW_FAST"
}
if ($EnableM04RhpNewFastBeforeBindFailureProbe) {
    $arguments += "-p:EnableM04RhpNewFastBeforeBindFailureProbe=true"
    Write-Host "EFI_PROBE M04_RHP_NEW_FAST_BEFORE_BIND_FAILURE"
}
if ($EnableM04RhpNewFastExhaustionFailureProbe) {
    $arguments += "-p:EnableM04RhpNewFastExhaustionFailureProbe=true"
    Write-Host "EFI_PROBE M04_RHP_NEW_FAST_EXHAUSTION_FAILURE"
}
if ($EnableM04SzArrayAllocationProbe) {
    $arguments += "-p:EnableM04SzArrayAllocationProbe=true"
    Write-Host "EFI_PROBE M04_SZ_ARRAY_ALLOCATION"
}
if ($EnableM04SzArrayAllocationBeforeBindProbe) {
    $arguments += "-p:EnableM04SzArrayAllocationBeforeBindProbe=true"
    Write-Host "EFI_PROBE M04_SZ_ARRAY_ALLOCATION_BEFORE_BIND"
}
if ($EnableM04SzArrayAllocationExhaustionProbe) {
    $arguments += "-p:EnableM04SzArrayAllocationExhaustionProbe=true"
    Write-Host "EFI_PROBE M04_SZ_ARRAY_ALLOCATION_EXHAUSTION"
}
if ($EnableM04StringAllocationProbe) {
    $arguments += "-p:EnableM04StringAllocationProbe=true"
    Write-Host "EFI_PROBE M04_STRING_ALLOCATION"
}
if ($EnableM04StringAllocationBeforeBindProbe) {
    $arguments += "-p:EnableM04StringAllocationBeforeBindProbe=true"
    Write-Host "EFI_PROBE M04_STRING_ALLOCATION_BEFORE_BIND"
}
if ($EnableM04StringAllocationExhaustionProbe) {
    $arguments += "-p:EnableM04StringAllocationExhaustionProbe=true"
    Write-Host "EFI_PROBE M04_STRING_ALLOCATION_EXHAUSTION"
}
if ($EnableM04StringAllocationInvalidLengthProbe) {
    $arguments += "-p:EnableM04StringAllocationInvalidLengthProbe=true"
    Write-Host "EFI_PROBE M04_STRING_ALLOCATION_INVALID_LENGTH"
}
if ($EnableM03MemoryMapCaptureProbe) {
    $arguments += "-p:EnableM03MemoryMapCaptureProbe=true"
    Write-Host "EFI_PROBE M03_MEMORY_MAP_CAPTURE"
}
if ($EnableM03FirmwareEntropySeedProbe) {
    $arguments += "-p:EnableM03FirmwareEntropySeedProbe=true"
    Write-Host "EFI_PROBE M03_FIRMWARE_ENTROPY_SEED"
}
if ($EnableM04TypeManagerModuleRegistrationProbe) {
    $arguments += "-p:EnableM04TypeManagerModuleRegistrationProbe=true"
    Write-Host "EFI_PROBE M04_TYPE_MANAGER_MODULE_REGISTRATION"
}
if ($EnableM04X64UnwindMetadataProbe) {
    $arguments += "-p:EnableM04X64UnwindMetadataProbe=true"
    Write-Host "EFI_PROBE M04_X64_UNWIND_METADATA"
}
if ($EnableM04SimpleThrowCatchProbe) {
    $arguments += "-p:EnableM04SimpleThrowCatchProbe=true"
    Write-Host "EFI_PROBE M04_SIMPLE_THROW_CATCH"
}
if ($EnableM04GcStaticRootMaterializationProbe) {
    $arguments += "-p:EnableM04GcStaticRootMaterializationProbe=true"
    Write-Host "EFI_PROBE M04_GC_STATIC_ROOT_MATERIALIZATION"
}
if ($EnableM04SeededCollectorChurnProbe) {
    $arguments += "-p:EnableM04SeededCollectorChurnProbe=true"
    Write-Host "EFI_PROBE M04_SEEDED_COLLECTOR_CHURN"
}
if ($EnableM03TypedExitBootServicesCallProbe) {
    $arguments += "-p:EnableM03TypedExitBootServicesCallProbe=true"
    Write-Host "EFI_PROBE M03_TYPED_EXIT_BOOT_SERVICES_CALL"
}
if ($EnableM03FirstRealExitBootServicesProbe) {
    $arguments += "-p:EnableM03FirstRealExitBootServicesProbe=true"
    Write-Host "EFI_PROBE M03_FIRST_REAL_EXIT_BOOT_SERVICES"
}
if ($EnableM03FirmwareAdapterRevocationProbe) {
    $arguments += "-p:EnableM03FirmwareAdapterRevocationProbe=true"
    Write-Host "EFI_PROBE M03_FIRMWARE_ADAPTER_REVOCATION"
}
if ($EnableM03OwnedSubsystemSurvivalProbe) {
    $arguments += "-p:EnableM03OwnedSubsystemSurvivalProbe=true"
    Write-Host "EFI_PROBE M03_OWNED_SUBSYSTEM_SURVIVAL"
}
if ($EnableM03OwnedSubsystemPanicProbe) {
    $arguments += "-p:EnableM03OwnedSubsystemPanicProbe=true"
    Write-Host "EFI_PROBE M03_OWNED_SUBSYSTEM_PANIC"
}
if ($EnableM04ClassConstructorExecutionProbe) {
    $arguments += "-p:EnableM04ClassConstructorExecutionProbe=true"
    Write-Host "EFI_PROBE M04_CLASS_CONSTRUCTOR_EXECUTION"
}
if ($EnableM04InterfaceDispatchProbe) {
    $arguments += "-p:EnableM04InterfaceDispatchProbe=true"
    Write-Host "EFI_PROBE M04_INTERFACE_DISPATCH"
}
if ($EnableM04CheckedReferenceBarrierProbe) {
    $arguments += "-p:EnableM04CheckedReferenceBarrierProbe=true"
    Write-Host "EFI_PROBE M03_MEMORY_MAP_CAPTURE"
}

if ($EnableUefiGoneSimulationProbe) {
    $arguments += "-p:EnableUefiGoneSimulationProbe=true"
    Write-Host "EFI_PROBE UEFI_GONE_SIMULATION"
}

if ($EnableExitBootServicesFreshMapProbe) {
    $arguments += "-p:EnableExitBootServicesFreshMapProbe=true"
    Write-Host "EFI_PROBE EXIT_BOOT_SERVICES_FRESH_MAP"
}

if ($EnableAcpiPhysicalDiscoveryProbe) {
    $arguments += "-p:EnableAcpiPhysicalDiscoveryProbe=true"
    Write-Host "EFI_PROBE ACPI_PHYSICAL_DISCOVERY"
}

$restoreArguments = @(
    "restore",
    $efiProjectPath,
    "-noAutoResponse",
    "-r", $Runtime,
    "--nologo",
    "--force",
    "--no-cache",
    "--configfile", $restoreConfigPath,
    "--packages", $restorePackagesDirectory,
    ("-p:Configuration=" + $Configuration),
    ("-p:DirectoryBuildPropsPath=" + $canonicalRootPropsPath),
    ("-p:DirectoryBuildTargetsPath=" + $canonicalRootTargetsPath),
    "-p:ImportDirectoryBuildProps=true",
    "-p:ImportDirectoryBuildTargets=true",
    "-p:CustomBeforeMicrosoftCommonProps=",
    "-p:CustomAfterMicrosoftCommonProps=",
    "-p:CustomBeforeDirectoryBuildProps=",
    "-p:CustomAfterDirectoryBuildProps=",
    "-p:CustomBeforeDirectoryBuildTargets=",
    "-p:CustomAfterDirectoryBuildTargets=",
    "-p:CustomBeforeMicrosoftCommonTargets=",
    "-p:CustomAfterMicrosoftCommonTargets=",
    "-p:CustomBeforeMicrosoftCSharpTargets=",
    "-p:CustomAfterMicrosoftCSharpTargets=",
    "-p:CustomBeforeMicrosoftCommonCrossTargetingTargets=",
    "-p:CustomAfterMicrosoftCommonCrossTargetingTargets=",
    "-p:BeforeMicrosoftNETSdkTargets=",
    "-p:AfterMicrosoftNETSdkTargets=",
    "-p:RestoreForceEvaluate=true",
    "-p:RestoreUseCustomAfterTargets=false",
    "-p:RestoreIgnoreFailedSources=false"
) + $projectExtensionArguments
$nativeBinaryPath = Join-Path $repositoryRoot (
    "src\OS\bin\$Configuration\net10.0\$Runtime\native\OS.exe")
$linkerMapWorkingPath = Join-Path $nativeIntermediateDirectory "OS.link.map"
$linkResponseWorkingPath = Join-Path $nativeIntermediateDirectory "link.rsp"
    $serializerObjectPath = Join-Path $nativeIntermediateDirectory (
        "CLROS.X64.InstructionPublication.obj")
    $referenceObjectPath = Join-Path $nativeIntermediateDirectory "CLROS.X64.CheckedReferenceBarriers.obj"
    $dispatchObjectPath = Join-Path $nativeIntermediateDirectory "CLROS.X64.InterfaceDispatch.obj"
    $catchObjectPath = Join-Path $nativeIntermediateDirectory "CLROS.X64.SimpleThrowCatch.obj"
    $solutionPath = Join-Path $repositoryRoot "CLROS.slnx"
$managedAssemblyPath = Join-Path $repositoryRoot (
    "src\OS\bin\$Configuration\net10.0\$Runtime\OS.dll")
$serializerToolRoot = Join-Path $repositoryRoot (
    "tools\OS.X64TextSerializerGenerator")
$serializerTargetsPath = Join-Path $serializerToolRoot (
    "OS.X64TextSerializerGenerator.targets")
$serializerGeneratorProjectPath = Join-Path $serializerToolRoot (
    "OS.X64TextSerializerGenerator.csproj")
$serializerGeneratorProgramPath = Join-Path $serializerToolRoot "Program.cs"
$serializerGeneratorWriterPath = Join-Path $serializerToolRoot "Amd64CoffWriter.cs"
$serializerGeneratorSourcePath = Join-Path $serializerToolRoot (
    "InstructionPublicationSerializerGenerator.cs")
$instructionPublicationSourcePath = Join-Path $repositoryRoot (
    "src\OS\Boot\X64InstructionPublication.cs")
$runtimeImportAttributeSourcePath = Join-Path $repositoryRoot (
    "src\OS\Runtime\RuntimeImportAttribute.cs")
$methodImplAttributeSourcePath = Join-Path $repositoryRoot (
    "src\OS\Runtime\MethodImplAttribute.cs")

    $lockedDotnetVersionResult = Invoke-M01BoundedProcess `
        -FilePath $script:DotnetHostPath `
        -Arguments @("--version") `
        -WorkingDirectory $repositoryRoot `
        -Operation "dotnet-sdk-recheck" `
        -TimeoutSeconds $BuildProcessTimeoutSeconds `
        -MaximumOutputCharacters $BuildProcessMaximumOutputCharacters
    $lockedDotnetVersionFailure = Get-M01BoundedProcessFailureReason `
        -Result $lockedDotnetVersionResult
    $lockedDotnetVersionLines = @(
        @($lockedDotnetVersionResult.OutputLines) |
            Where-Object {
                -not [string]::IsNullOrWhiteSpace([string] $_)
            } |
            ForEach-Object { [string] $_ }
    )
    if ($lockedDotnetVersionFailure -cne "ok" -or
        $lockedDotnetVersionLines.Count -ne 1 -or
        $lockedDotnetVersionLines[0].Trim() -cne $dotnetSdkVersion) {
        Write-Host (
            "TEST_FAIL efi.shell.publish dotnet_sdk_changed_before_publish reason=" +
            $lockedDotnetVersionFailure +
            " expected=" +
            (Format-EvidenceValue $dotnetSdkVersion))
        exit 1
    }

    $m01LockedSourceClosure = Test-M01SourceClosure `
        -RepositoryRoot $repositoryRoot `
        -ProjectPath $efiProjectPath `
        -Configuration $Configuration `
        -LoadedImageStackEnabled $EnableM01LoadedImageStackProbe.IsPresent `
        -LiveAllocatorEnabled $EnableM01LiveAllocatorProbe.IsPresent `
        -OwnedPagingEnabled $EnableM01OwnedPagingProbe.IsPresent `
        -M02SafeStubEnabled $EnableM02SafeStubProbe.IsPresent `
        -M02CheckedPortIoBindingEnabled $EnableM02CheckedPortIoBindingProbe.IsPresent `
        -M02Com1OwnedPortIoEnabled $EnableM02Com1OwnedPortIoProbe.IsPresent `
        -M02OwnedCom1ReadyEnabled $EnableM02OwnedCom1ReadyProbe.IsPresent `
        -M02PanicSafeOwnedCom1Enabled $EnableM02PanicSafeOwnedCom1Probe.IsPresent `
        -M02OwnedX64GdtEncodingEnabled $EnableM02OwnedX64GdtEncodingProbe.IsPresent `
        -M02GuardedIstStackAllocationEnabled $EnableM02GuardedIstStackAllocationProbe.IsPresent `
        -M02OwnedGdtTssTerminalEnabled $EnableM02OwnedGdtTssTerminalProbe.IsPresent `
        -M02NormalizedInterruptFramePageEnabled $EnableM02NormalizedInterruptFramePageProbe.IsPresent `
        -M02OwnedIdtGatePopulationEnabled $EnableM02OwnedIdtGatePopulationProbe.IsPresent `
        -M02IdtrContinuedExecutionEnabled $EnableM02IdtrContinuedExecutionProbe.IsPresent `
        -M02Ud2VectorStateEnabled $EnableM02Ud2VectorStateProbe.IsPresent `
        -M02PageFaultCr2ErrorCodeEnabled $EnableM02PageFaultCr2ErrorCodeProbe.IsPresent `
        -M02DoubleFaultIstEnabled $EnableM02DoubleFaultIstProbe.IsPresent `
        -M02UnexpectedVectorPanicEnabled $EnableM02UnexpectedVectorPanicProbe.IsPresent `
        -M04IndependentEndianConformanceEnabled $EnableM04IndependentEndianConformanceProbe.IsPresent `
        -M04SpanMemoryConformanceEnabled $EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe.IsPresent `
        -M04ArithmeticConformanceEnabled $EnableM04ArithmeticConformanceProbe.IsPresent `
        -M04LayoutContractEnabled $EnableM04LayoutContractProbe.IsPresent `
        -M04ManagedHeapPageRangeEnabled $EnableM04ManagedHeapPageRangeProbe.IsPresent `
        -M04RhpNewFastEnabled $EnableM04RhpNewFastProbe.IsPresent `
        -M04RhpNewFastBeforeBindFailureEnabled $EnableM04RhpNewFastBeforeBindFailureProbe.IsPresent `
        -M04RhpNewFastExhaustionFailureEnabled $EnableM04RhpNewFastExhaustionFailureProbe.IsPresent `
        -M04SzArrayAllocationEnabled $EnableM04SzArrayAllocationProbe.IsPresent `
        -M04SzArrayAllocationBeforeBindEnabled $EnableM04SzArrayAllocationBeforeBindProbe.IsPresent `
        -M04SzArrayAllocationExhaustionEnabled $EnableM04SzArrayAllocationExhaustionProbe.IsPresent `
        -M04StringAllocationEnabled $EnableM04StringAllocationProbe.IsPresent `
        -M04StringAllocationBeforeBindEnabled $EnableM04StringAllocationBeforeBindProbe.IsPresent `
        -M04StringAllocationExhaustionEnabled $EnableM04StringAllocationExhaustionProbe.IsPresent `
        -M04StringAllocationInvalidLengthEnabled $EnableM04StringAllocationInvalidLengthProbe.IsPresent `
        -M03MemoryMapCaptureEnabled $EnableM03MemoryMapCaptureProbe.IsPresent `
        -M03FirmwareEntropySeedEnabled $EnableM03FirmwareEntropySeedProbe.IsPresent `
        -M04TypeManagerModuleRegistrationEnabled $EnableM04TypeManagerModuleRegistrationProbe.IsPresent `
        -M04X64UnwindMetadataEnabled $EnableM04X64UnwindMetadataProbe.IsPresent `
        -M04SimpleThrowCatchEnabled $EnableM04SimpleThrowCatchProbe.IsPresent `
        -M04GcStaticRootMaterializationEnabled $EnableM04GcStaticRootMaterializationProbe.IsPresent `
        -M04SeededCollectorChurnEnabled $EnableM04SeededCollectorChurnProbe.IsPresent `
        -M03TypedExitBootServicesCallEnabled $EnableM03TypedExitBootServicesCallProbe.IsPresent `
        -M03FirstRealExitBootServicesEnabled $EnableM03FirstRealExitBootServicesProbe.IsPresent `
        -M03FirmwareAdapterRevocationEnabled $EnableM03FirmwareAdapterRevocationProbe.IsPresent `
        -M03OwnedSubsystemSurvivalEnabled $EnableM03OwnedSubsystemSurvivalProbe.IsPresent `
        -M03OwnedSubsystemPanicEnabled $EnableM03OwnedSubsystemPanicProbe.IsPresent `
        -M04ClassConstructorExecutionEnabled $EnableM04ClassConstructorExecutionProbe.IsPresent `
        -M04InterfaceDispatchEnabled $EnableM04InterfaceDispatchProbe.IsPresent `
        -M04CheckedReferenceBarrierEnabled $EnableM04CheckedReferenceBarrierProbe.IsPresent `
        -UefiGoneSimulationEnabled $EnableUefiGoneSimulationProbe.IsPresent `
        -ExitBootServicesFreshMapEnabled $EnableExitBootServicesFreshMapProbe.IsPresent `
        -AcpiPhysicalDiscoveryEnabled $EnableAcpiPhysicalDiscoveryProbe.IsPresent
    if (-not $m01LockedSourceClosure.Succeeded) {
        Write-Host (
            "TEST_FAIL efi.shell.publish m01_locked_source_closure_" +
            $m01LockedSourceClosure.Reason +
            " detail=" +
            (Format-EvidenceValue $m01LockedSourceClosure.Detail))
        exit 1
    }
    Write-Host (
        "EFI_PUBLISH_M01_SOURCE_CLOSURE_LOCKED status=ok" +
        " reviewed=" + $m01LockedSourceClosure.ReviewedSourceCount +
        " prerequisites=" + $m01LockedSourceClosure.PrerequisiteSourceCount +
        " compileItems=" + $m01LockedSourceClosure.CompileItemCount)

    try {
        Remove-M01FreshPublishDirectory `
            -RepositoryRoot $repositoryRoot `
            -CandidatePath $managedIntermediateDirectory `
            -ExpectedRelativePath (
                "src\OS\obj\$Configuration\net10.0\$Runtime")
        Remove-M01FreshPublishDirectory `
            -RepositoryRoot $repositoryRoot `
            -CandidatePath $managedOutputDirectory `
            -ExpectedRelativePath (
                "src\OS\bin\$Configuration\net10.0\$Runtime")
    }
    catch {
        Write-Host (
            "TEST_FAIL efi.shell.publish fresh_output_cleanup_failed detail=" +
            (Format-EvidenceValue $_.Exception.Message))
        exit 1
    }
    Write-Host (
        "EFI_PUBLISH_FRESH_OUTPUTS status=ok directories=2 " +
        "noIncremental=true")

    try {
        $restoreArchiveSnapshotBeforeLock =
            Get-M01PinnedRestoreArchiveSnapshot `
                -GlobalPackageCacheRoot $globalPackageCacheRoot
        $restoreArchiveSourceLocks = Open-M01ExternalInputLocks `
            -RootPath $globalPackageCacheRoot `
            -Inputs $restoreArchiveSnapshotBeforeLock.Inputs
        $restoreArchiveSnapshotAfterLock =
            Get-M01PinnedRestoreArchiveSnapshot `
                -GlobalPackageCacheRoot $globalPackageCacheRoot
        if ($restoreArchiveSnapshotBeforeLock.AggregateSha256 -cne
            $restoreArchiveSnapshotAfterLock.AggregateSha256) {
            throw "pinned restore archives changed before lock"
        }
        $createdRestoreConfigPath = New-M01PinnedRestoreSource `
            -GlobalPackageCacheRoot $globalPackageCacheRoot `
            -RestoreSourceDirectory $restoreSourceDirectory `
            -ArchiveInputs $restoreArchiveSnapshotBeforeLock.Inputs
        if (-not [string]::Equals(
                [System.IO.Path]::GetFullPath($createdRestoreConfigPath),
                [System.IO.Path]::GetFullPath($restoreConfigPath),
                [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "pinned restore configuration path mismatch"
        }
        $restoreSourceSnapshotBeforeLock = Get-M01ExactFileSnapshot `
            -RepositoryRoot $repositoryRoot `
            -RelativePaths $expectedRestoreSourceRelativePaths
        $restoreSourceLocks = Open-M01PublishSourceLocks `
            -RepositoryRoot $repositoryRoot `
            -Inputs $restoreSourceSnapshotBeforeLock.Inputs
        $restoreSourceSnapshotAfterLock = Get-M01ExactFileSnapshot `
            -RepositoryRoot $repositoryRoot `
            -RelativePaths $expectedRestoreSourceRelativePaths
        if ($restoreSourceSnapshotBeforeLock.AggregateSha256 -cne
            $restoreSourceSnapshotAfterLock.AggregateSha256) {
            throw "pinned restore source changed before lock"
        }
    }
    catch {
        Write-Host (
            "TEST_FAIL efi.shell.publish pinned_restore_source_failed detail=" +
            (Format-EvidenceValue $_.Exception.Message))
        exit 1
    }
    Write-Host (
        "EFI_PUBLISH_PINNED_RESTORE_SOURCE status=ok archives=" +
        $restoreArchiveSnapshotBeforeLock.Inputs.Count +
        " sourceInputs=" +
        $restoreSourceSnapshotBeforeLock.Inputs.Count +
        " sha256=" +
        $restoreSourceSnapshotBeforeLock.AggregateSha256)

$restoreEnvironment = @{}
foreach ($entry in $producerMsbuildIsolation.Environment.GetEnumerator()) {
    $restoreEnvironment[$entry.Key] = $entry.Value
}
$restoreEnvironment["LINK"] = $null
$restoreEnvironment["_LINK_"] = $null
$restoreResult = Invoke-M01BoundedProcess `
    -FilePath $script:DotnetHostPath `
    -Arguments $restoreArguments `
    -WorkingDirectory $repositoryRoot `
    -Operation "isolated-restore" `
    -TimeoutSeconds $BuildProcessTimeoutSeconds `
    -MaximumOutputCharacters $BuildProcessMaximumOutputCharacters `
    -EnvironmentOverrides $restoreEnvironment
Write-M01BoundedProcessOutput `
    -Result $restoreResult `
    -LogFullPath $logFullPath
$restoreFailure = Get-M01BoundedProcessFailureReason -Result $restoreResult
if ($restoreFailure -cne "ok") {
    Write-Host (
        "TEST_FAIL efi.shell.publish isolated_restore_failed reason=" +
        $restoreFailure +
        " exit_code=" +
        $restoreResult.ExitCode +
        " log=" +
        $LogPath)
    exit 1
}

try {
    $restoreArchiveSnapshotAfterRestore =
        Get-M01PinnedRestoreArchiveSnapshot `
            -GlobalPackageCacheRoot $globalPackageCacheRoot
    if ($restoreArchiveSnapshotBeforeLock.AggregateSha256 -cne
        $restoreArchiveSnapshotAfterRestore.AggregateSha256) {
        throw "pinned restore archives changed during restore"
    }
    $restoreSourceSnapshotAfterRestore = Get-M01ExactFileSnapshot `
        -RepositoryRoot $repositoryRoot `
        -RelativePaths $expectedRestoreSourceRelativePaths
    if ($restoreSourceSnapshotBeforeLock.AggregateSha256 -cne
        $restoreSourceSnapshotAfterRestore.AggregateSha256) {
        throw "pinned restore source changed during restore"
    }
    $restoreSnapshotBeforeLock = Get-M01ExactRestoreClosureSnapshot `
        -RepositoryRoot $repositoryRoot `
        -ProjectExtensionsDirectory $projectExtensionsDirectory `
        -ExpectedRelativePaths $expectedRestoreClosureRelativePaths
    $restoreLocks = Open-M01PublishSourceLocks `
        -RepositoryRoot $repositoryRoot `
        -Inputs $restoreSnapshotBeforeLock.Inputs
    $restoreSnapshotAfterLock = Get-M01ExactRestoreClosureSnapshot `
        -RepositoryRoot $repositoryRoot `
        -ProjectExtensionsDirectory $projectExtensionsDirectory `
        -ExpectedRelativePaths $expectedRestoreClosureRelativePaths
    Test-M01RestoreSemanticClosure `
        -ProjectPath $efiProjectPath `
        -ProjectExtensionsDirectory $projectExtensionsDirectory `
        -PackageCacheRoot $restorePackagesDirectory
}
catch {
    Write-Host (
        "TEST_FAIL efi.shell.publish restore_closure_lock_failed detail=" +
        (Format-EvidenceValue $_.Exception.Message))
    exit 1
}
if ($restoreSnapshotBeforeLock.AggregateSha256 -cne
    $restoreSnapshotAfterLock.AggregateSha256) {
    Write-Host (
        "TEST_FAIL efi.shell.publish restore_closure_changed_before_lock before=" +
        $restoreSnapshotBeforeLock.AggregateSha256 +
        " locked=" +
        $restoreSnapshotAfterLock.AggregateSha256)
    exit 1
}
Write-Host (
    "EFI_PUBLISH_RESTORE_CLOSURE_LOCKED status=ok inputs=" +
    $restoreSnapshotBeforeLock.Inputs.Count +
    " sha256=" +
    $restoreSnapshotBeforeLock.AggregateSha256)

try {
    $packageBuildSnapshotBeforeLock =
        Get-M01PackageBuildClosureSnapshot `
            -PackageCacheRoot $restorePackagesDirectory
    $packageClosureSnapshotBeforeLock =
        Get-M01ConsumedPackageClosureSnapshot `
            -RestorePackagesRoot $restorePackagesDirectory `
            -RuntimePackageRoot $resolvedNativeAotRuntimePackPath `
            -RestoreSourceDirectory $restoreSourceDirectory
    $packageLocks = Open-M01AbsoluteInputLocks `
        -Inputs $packageClosureSnapshotBeforeLock.LockInputs
    $packageBuildSnapshotAfterLock =
        Get-M01PackageBuildClosureSnapshot `
            -PackageCacheRoot $restorePackagesDirectory
    $packageClosureSnapshotAfterLock =
        Get-M01ConsumedPackageClosureSnapshot `
            -RestorePackagesRoot $restorePackagesDirectory `
            -RuntimePackageRoot $resolvedNativeAotRuntimePackPath `
            -RestoreSourceDirectory $restoreSourceDirectory
    if ($packageBuildSnapshotBeforeLock.AggregateSha256 -cne
            $packageBuildSnapshotAfterLock.AggregateSha256 -or
        $packageClosureSnapshotBeforeLock.AggregateSha256 -cne
            $packageClosureSnapshotAfterLock.AggregateSha256) {
        throw "package closure changed before lock"
    }
    $postRestoreEvaluation = Invoke-M01PostRestoreEvaluation `
        -RepositoryRoot $repositoryRoot `
        -ProjectPath $efiProjectPath `
        -Configuration $Configuration `
        -Runtime $Runtime `
        -ProjectExtensionsDirectory $projectExtensionsDirectory `
        -ProjectAssetsPath $projectAssetsPath `
        -RestorePackagesRoot $restorePackagesDirectory `
        -IlCompilerTargetsPath $ilCompilerTargetsPath `
        -IlLinkTargetsPath $ilLinkTargetsPath `
        -IlLinkTasksAssembly $ilLinkTasksAssemblyPath `
        -IlcBuildTasksPath $ilcBuildTasksPath `
        -IlcHostPackagePath $ilCompilerHostPackageRoot `
        -IlcToolsPath $ilcToolsPath `
        -RuntimePackagePath $resolvedNativeAotRuntimePackPath `
        -IlcFrameworkPath $ilcFrameworkPath `
        -IlcFrameworkNativePath $ilcFrameworkNativePath `
        -IlcSdkPath $ilcSdkPath `
        -IlcMibcPath $ilcMibcPath `
        -LanguageTargetsPath $languageTargetsPath `
        -CSharpTargetsPath $csharpTargetsPath `
        -CommonTargetsPath $commonTargetsPath `
        -M02OwnedCom1ReadyEnabled $EnableM02OwnedCom1ReadyProbe.IsPresent `
        -M02PanicSafeOwnedCom1Enabled $EnableM02PanicSafeOwnedCom1Probe.IsPresent `
        -M02OwnedX64GdtEncodingEnabled $EnableM02OwnedX64GdtEncodingProbe.IsPresent `
        -M02GuardedIstStackAllocationEnabled $EnableM02GuardedIstStackAllocationProbe.IsPresent `
        -M02OwnedGdtTssTerminalEnabled $EnableM02OwnedGdtTssTerminalProbe.IsPresent `
        -M02NormalizedInterruptFramePageEnabled `
            $EnableM02NormalizedInterruptFramePageProbe.IsPresent `
        -M02OwnedIdtGatePopulationEnabled `
            $EnableM02OwnedIdtGatePopulationProbe.IsPresent `
        -M02IdtrContinuedExecutionEnabled `
            $EnableM02IdtrContinuedExecutionProbe.IsPresent `
        -M02Ud2VectorStateEnabled $EnableM02Ud2VectorStateProbe.IsPresent `
        -M02PageFaultCr2ErrorCodeEnabled $EnableM02PageFaultCr2ErrorCodeProbe.IsPresent `
        -M02DoubleFaultIstEnabled $EnableM02DoubleFaultIstProbe.IsPresent `
        -M02UnexpectedVectorPanicEnabled $EnableM02UnexpectedVectorPanicProbe.IsPresent `
        -M04IndependentEndianConformanceEnabled `
            $EnableM04IndependentEndianConformanceProbe.IsPresent `
        -M04SpanMemoryConformanceEnabled `
            $EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe.IsPresent `
        -M04ArithmeticConformanceEnabled `
            $EnableM04ArithmeticConformanceProbe.IsPresent `
        -M04LayoutContractEnabled $EnableM04LayoutContractProbe.IsPresent `
        -M04ManagedHeapPageRangeEnabled $EnableM04ManagedHeapPageRangeProbe.IsPresent `
        -M04RhpNewFastEnabled $EnableM04RhpNewFastProbe.IsPresent `
        -M04RhpNewFastBeforeBindFailureEnabled $EnableM04RhpNewFastBeforeBindFailureProbe.IsPresent `
        -M04RhpNewFastExhaustionFailureEnabled $EnableM04RhpNewFastExhaustionFailureProbe.IsPresent `
        -M04SzArrayAllocationEnabled $EnableM04SzArrayAllocationProbe.IsPresent `
        -M04SzArrayAllocationBeforeBindEnabled $EnableM04SzArrayAllocationBeforeBindProbe.IsPresent `
        -M04SzArrayAllocationExhaustionEnabled $EnableM04SzArrayAllocationExhaustionProbe.IsPresent `
        -M04StringAllocationEnabled $EnableM04StringAllocationProbe.IsPresent `
        -M04StringAllocationBeforeBindEnabled $EnableM04StringAllocationBeforeBindProbe.IsPresent `
        -M04StringAllocationExhaustionEnabled $EnableM04StringAllocationExhaustionProbe.IsPresent `
        -M04StringAllocationInvalidLengthEnabled $EnableM04StringAllocationInvalidLengthProbe.IsPresent `
        -M03MemoryMapCaptureEnabled $EnableM03MemoryMapCaptureProbe.IsPresent `
        -M03FirmwareEntropySeedEnabled $EnableM03FirmwareEntropySeedProbe.IsPresent `
        -M04TypeManagerModuleRegistrationEnabled $EnableM04TypeManagerModuleRegistrationProbe.IsPresent `
        -M04X64UnwindMetadataEnabled $EnableM04X64UnwindMetadataProbe.IsPresent `
        -M04SimpleThrowCatchEnabled $EnableM04SimpleThrowCatchProbe.IsPresent `
        -M04GcStaticRootMaterializationEnabled $EnableM04GcStaticRootMaterializationProbe.IsPresent `
        -M04SeededCollectorChurnEnabled $EnableM04SeededCollectorChurnProbe.IsPresent `
        -M03TypedExitBootServicesCallEnabled $EnableM03TypedExitBootServicesCallProbe.IsPresent `
        -M03FirstRealExitBootServicesEnabled $EnableM03FirstRealExitBootServicesProbe.IsPresent `
        -M03FirmwareAdapterRevocationEnabled $EnableM03FirmwareAdapterRevocationProbe.IsPresent `
        -M03OwnedSubsystemSurvivalEnabled $EnableM03OwnedSubsystemSurvivalProbe.IsPresent `
        -M03OwnedSubsystemPanicEnabled $EnableM03OwnedSubsystemPanicProbe.IsPresent `
        -M04ClassConstructorExecutionEnabled $EnableM04ClassConstructorExecutionProbe.IsPresent `
        -M04InterfaceDispatchEnabled $EnableM04InterfaceDispatchProbe.IsPresent `
        -M04CheckedReferenceBarrierEnabled $EnableM04CheckedReferenceBarrierProbe.IsPresent `
        -LoadedImageStackEnabled $EnableM01LoadedImageStackProbe.IsPresent `
        -LiveAllocatorEnabled $EnableM01LiveAllocatorProbe.IsPresent `
        -OwnedPagingEnabled $EnableM01OwnedPagingProbe.IsPresent `
        -M02SafeStubEnabled $EnableM02SafeStubProbe.IsPresent `
        -M02CheckedPortIoBindingEnabled $EnableM02CheckedPortIoBindingProbe.IsPresent `
        -M02Com1OwnedPortIoEnabled $EnableM02Com1OwnedPortIoProbe.IsPresent `
        -UefiGoneSimulationEnabled $EnableUefiGoneSimulationProbe.IsPresent `
        -ExitBootServicesFreshMapEnabled $EnableExitBootServicesFreshMapProbe.IsPresent `
        -AcpiPhysicalDiscoveryEnabled $EnableAcpiPhysicalDiscoveryProbe.IsPresent
    $activePackageImportGraphSha256 = Get-M01Utf8Sha256 -Text (
        ($script:ExpectedActivePackageImportEdges -join "`n") + "`n")
    $packageTaskBindingsSha256 = Get-M01Utf8Sha256 -Text (
        ($script:ExpectedPackageTaskBindings -join "`n") + "`n")
}
catch {
    Write-Host (
        "TEST_FAIL efi.shell.publish package_closure_lock_failed detail=" +
        (Format-EvidenceValue $_.Exception.Message))
    exit 1
}
Write-Host (
    "EFI_PUBLISH_PACKAGE_CLOSURE_LOCKED status=ok packages=" +
    $packageClosureSnapshotBeforeLock.Packages.Count +
    " buildInputs=" +
    $packageBuildSnapshotBeforeLock.Inputs.Count +
    " sha256=" +
    $packageClosureSnapshotBeforeLock.AggregateSha256)
Write-Host (
    "EFI_PUBLISH_POST_RESTORE_EVALUATION status=ok compile=" +
    $postRestoreEvaluation.compileCount +
    " projectReferences=" +
    $postRestoreEvaluation.projectReferenceCount +
    " references=" +
    $postRestoreEvaluation.referenceCount +
    " analyzers=" +
    $postRestoreEvaluation.analyzerCount)

$linkerHash = (Get-FileHash -LiteralPath $linkerPath -Algorithm SHA256).Hash.ToLowerInvariant()
$dotnetHostHash = (
    Get-FileHash -LiteralPath $script:DotnetHostPath -Algorithm SHA256
).Hash.ToLowerInvariant()
$ilCompilerHash = (
    Get-FileHash -LiteralPath $actualIlCompilerPath -Algorithm SHA256
).Hash.ToLowerInvariant()
$sdkLibHash = (Get-FileHash -LiteralPath $sdkLibPath -Algorithm SHA256).Hash.ToLowerInvariant()
$runtimeListHash = (Get-FileHash -LiteralPath $runtimeListPath -Algorithm SHA256).Hash.ToLowerInvariant()
Write-Host (
    "EFI_PUBLISH_TOOLCHAIN_PIN linkerPath=" + (Format-EvidenceValue $linkerPath) +
    " linkerSha256=" + $linkerHash +
    " ilcompilerPath=" + (Format-EvidenceValue $actualIlCompilerPath) +
    " ilcompilerSha256=" + $ilCompilerHash)

$publishEnvironment = @{}
foreach ($entry in $producerMsbuildIsolation.Environment.GetEnumerator()) {
    $publishEnvironment[$entry.Key] = $entry.Value
}
$publishEnvironment["LINK"] = $null
$publishEnvironment["_LINK_"] = $null
$publishResult = Invoke-M01BoundedProcess `
    -FilePath $script:DotnetHostPath `
    -Arguments $arguments `
    -WorkingDirectory $repositoryRoot `
    -Operation "nativeaot-publish" `
    -TimeoutSeconds $BuildProcessTimeoutSeconds `
    -MaximumOutputCharacters $BuildProcessMaximumOutputCharacters `
    -EnvironmentOverrides $publishEnvironment
Write-M01BoundedProcessOutput `
    -Result $publishResult `
    -LogFullPath $logFullPath
$publishFailure = Get-M01BoundedProcessFailureReason -Result $publishResult
$exitCode = if ($publishFailure -ceq "ok") {
    0
}
else {
    1
}

if ($exitCode -eq 0) {
    try {
        Assert-M01PublishSourceLocks `
            -RepositoryRoot $repositoryRoot `
            -Inputs $sourceSnapshotBeforePublish.Inputs `
            -Locks $sourceLocks
        $sourceSnapshotAfterPublish = Get-M01PublishSourceSnapshot `
            -RepositoryRoot $repositoryRoot `
            -ProjectPath $efiProjectPath
    }
    catch {
        Write-Host (
            "TEST_FAIL efi.shell.publish source_snapshot_failed detail=" +
            (Format-EvidenceValue $_.Exception.Message))
        exit 1
    }
    if ($sourceSnapshotBeforePublish.AggregateSha256 -cne
            $sourceSnapshotAfterPublish.AggregateSha256 -or
        $sourceSnapshotBeforePublish.AuthoritySha256 -cne
            $sourceSnapshotAfterPublish.AuthoritySha256) {
        Write-Host (
            "TEST_FAIL efi.shell.publish source_changed_during_publish before=" +
            $sourceSnapshotBeforePublish.AggregateSha256 +
            " after=" +
            $sourceSnapshotAfterPublish.AggregateSha256)
        exit 1
    }
    try {
        $restoreSnapshotAfterPublish = Get-M01ExactRestoreClosureSnapshot `
            -RepositoryRoot $repositoryRoot `
            -ProjectExtensionsDirectory $projectExtensionsDirectory `
            -ExpectedRelativePaths $expectedRestoreClosureRelativePaths
    }
    catch {
        Write-Host (
            "TEST_FAIL efi.shell.publish restore_closure_snapshot_failed detail=" +
            (Format-EvidenceValue $_.Exception.Message))
        exit 1
    }
    if ($restoreSnapshotBeforeLock.AggregateSha256 -cne
        $restoreSnapshotAfterPublish.AggregateSha256) {
        Write-Host (
            "TEST_FAIL efi.shell.publish restore_closure_changed_during_publish before=" +
            $restoreSnapshotBeforeLock.AggregateSha256 +
            " after=" +
            $restoreSnapshotAfterPublish.AggregateSha256)
        exit 1
    }
    try {
        Test-M01RestoreSemanticClosure `
            -ProjectPath $efiProjectPath `
            -ProjectExtensionsDirectory $projectExtensionsDirectory `
            -PackageCacheRoot $restorePackagesDirectory
        $restoreSourceSnapshotAfterPublish = Get-M01ExactFileSnapshot `
            -RepositoryRoot $repositoryRoot `
            -RelativePaths $expectedRestoreSourceRelativePaths
        $restoreArchiveSnapshotAfterPublish =
            Get-M01PinnedRestoreArchiveSnapshot `
                -GlobalPackageCacheRoot $globalPackageCacheRoot
        $packageBuildSnapshotAfterPublish =
            Get-M01PackageBuildClosureSnapshot `
                -PackageCacheRoot $restorePackagesDirectory
        $packageClosureSnapshotAfterPublish =
            Get-M01ConsumedPackageClosureSnapshot `
                -RestorePackagesRoot $restorePackagesDirectory `
                -RuntimePackageRoot $resolvedNativeAotRuntimePackPath `
                -RestoreSourceDirectory $restoreSourceDirectory
        $postRestoreEvaluationAfterPublish =
            Invoke-M01PostRestoreEvaluation `
                -RepositoryRoot $repositoryRoot `
                -ProjectPath $efiProjectPath `
                -Configuration $Configuration `
                -Runtime $Runtime `
                -ProjectExtensionsDirectory $projectExtensionsDirectory `
                -ProjectAssetsPath $projectAssetsPath `
                -RestorePackagesRoot $restorePackagesDirectory `
                -IlCompilerTargetsPath $ilCompilerTargetsPath `
                -IlLinkTargetsPath $ilLinkTargetsPath `
                -IlLinkTasksAssembly $ilLinkTasksAssemblyPath `
                -IlcBuildTasksPath $ilcBuildTasksPath `
                -IlcHostPackagePath $ilCompilerHostPackageRoot `
                -IlcToolsPath $ilcToolsPath `
                -RuntimePackagePath $resolvedNativeAotRuntimePackPath `
                -IlcFrameworkPath $ilcFrameworkPath `
                -IlcFrameworkNativePath $ilcFrameworkNativePath `
                -IlcSdkPath $ilcSdkPath `
                -IlcMibcPath $ilcMibcPath `
                -LanguageTargetsPath $languageTargetsPath `
                -CSharpTargetsPath $csharpTargetsPath `
                -CommonTargetsPath $commonTargetsPath `
                -M02OwnedCom1ReadyEnabled $EnableM02OwnedCom1ReadyProbe.IsPresent `
                -M02PanicSafeOwnedCom1Enabled $EnableM02PanicSafeOwnedCom1Probe.IsPresent `
                -M02OwnedX64GdtEncodingEnabled $EnableM02OwnedX64GdtEncodingProbe.IsPresent `
                -M02GuardedIstStackAllocationEnabled $EnableM02GuardedIstStackAllocationProbe.IsPresent `
                -M02OwnedGdtTssTerminalEnabled $EnableM02OwnedGdtTssTerminalProbe.IsPresent `
                -M02NormalizedInterruptFramePageEnabled `
                    $EnableM02NormalizedInterruptFramePageProbe.IsPresent `
                -M02OwnedIdtGatePopulationEnabled `
                    $EnableM02OwnedIdtGatePopulationProbe.IsPresent `
                -M02IdtrContinuedExecutionEnabled `
                    $EnableM02IdtrContinuedExecutionProbe.IsPresent `
                -M02Ud2VectorStateEnabled $EnableM02Ud2VectorStateProbe.IsPresent `
                -M02PageFaultCr2ErrorCodeEnabled $EnableM02PageFaultCr2ErrorCodeProbe.IsPresent `
                -M02DoubleFaultIstEnabled $EnableM02DoubleFaultIstProbe.IsPresent `
                -M02UnexpectedVectorPanicEnabled $EnableM02UnexpectedVectorPanicProbe.IsPresent `
                -M04IndependentEndianConformanceEnabled `
                    $EnableM04IndependentEndianConformanceProbe.IsPresent `
                -M04SpanMemoryConformanceEnabled `
                    $EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe.IsPresent `
                -M04ArithmeticConformanceEnabled `
                    $EnableM04ArithmeticConformanceProbe.IsPresent `
                -M04LayoutContractEnabled $EnableM04LayoutContractProbe.IsPresent `
                -M04ManagedHeapPageRangeEnabled $EnableM04ManagedHeapPageRangeProbe.IsPresent `
                -M04RhpNewFastEnabled $EnableM04RhpNewFastProbe.IsPresent `
                -M04RhpNewFastBeforeBindFailureEnabled $EnableM04RhpNewFastBeforeBindFailureProbe.IsPresent `
                -M04RhpNewFastExhaustionFailureEnabled $EnableM04RhpNewFastExhaustionFailureProbe.IsPresent `
                -M04SzArrayAllocationEnabled $EnableM04SzArrayAllocationProbe.IsPresent `
                -M04SzArrayAllocationBeforeBindEnabled $EnableM04SzArrayAllocationBeforeBindProbe.IsPresent `
                -M04SzArrayAllocationExhaustionEnabled $EnableM04SzArrayAllocationExhaustionProbe.IsPresent `
                -M04StringAllocationEnabled $EnableM04StringAllocationProbe.IsPresent `
                -M04StringAllocationBeforeBindEnabled $EnableM04StringAllocationBeforeBindProbe.IsPresent `
                -M04StringAllocationExhaustionEnabled $EnableM04StringAllocationExhaustionProbe.IsPresent `
                -M04StringAllocationInvalidLengthEnabled $EnableM04StringAllocationInvalidLengthProbe.IsPresent `
                -M03MemoryMapCaptureEnabled $EnableM03MemoryMapCaptureProbe.IsPresent `
                -M03FirmwareEntropySeedEnabled $EnableM03FirmwareEntropySeedProbe.IsPresent `
                -M04TypeManagerModuleRegistrationEnabled $EnableM04TypeManagerModuleRegistrationProbe.IsPresent `
                -M04X64UnwindMetadataEnabled $EnableM04X64UnwindMetadataProbe.IsPresent `
                -M04SimpleThrowCatchEnabled $EnableM04SimpleThrowCatchProbe.IsPresent `
                -M04GcStaticRootMaterializationEnabled $EnableM04GcStaticRootMaterializationProbe.IsPresent `
                -M04SeededCollectorChurnEnabled $EnableM04SeededCollectorChurnProbe.IsPresent `
                -M03TypedExitBootServicesCallEnabled $EnableM03TypedExitBootServicesCallProbe.IsPresent `
                -M03FirstRealExitBootServicesEnabled $EnableM03FirstRealExitBootServicesProbe.IsPresent `
                -M03FirmwareAdapterRevocationEnabled $EnableM03FirmwareAdapterRevocationProbe.IsPresent `
                -M03OwnedSubsystemSurvivalEnabled $EnableM03OwnedSubsystemSurvivalProbe.IsPresent `
                -M03OwnedSubsystemPanicEnabled $EnableM03OwnedSubsystemPanicProbe.IsPresent `
                -M04ClassConstructorExecutionEnabled $EnableM04ClassConstructorExecutionProbe.IsPresent `
                -M04InterfaceDispatchEnabled $EnableM04InterfaceDispatchProbe.IsPresent `
                -M04CheckedReferenceBarrierEnabled $EnableM04CheckedReferenceBarrierProbe.IsPresent `
                -LoadedImageStackEnabled $EnableM01LoadedImageStackProbe.IsPresent `
                -LiveAllocatorEnabled $EnableM01LiveAllocatorProbe.IsPresent `
                -OwnedPagingEnabled $EnableM01OwnedPagingProbe.IsPresent `
                -M02SafeStubEnabled $EnableM02SafeStubProbe.IsPresent `
                -M02CheckedPortIoBindingEnabled $EnableM02CheckedPortIoBindingProbe.IsPresent `
                -M02Com1OwnedPortIoEnabled $EnableM02Com1OwnedPortIoProbe.IsPresent `
                -UefiGoneSimulationEnabled $EnableUefiGoneSimulationProbe.IsPresent `
                -ExitBootServicesFreshMapEnabled $EnableExitBootServicesFreshMapProbe.IsPresent `
                -AcpiPhysicalDiscoveryEnabled $EnableAcpiPhysicalDiscoveryProbe.IsPresent
    }
    catch {
        Write-Host (
            "TEST_FAIL efi.shell.publish package_closure_snapshot_failed detail=" +
            (Format-EvidenceValue $_.Exception.Message))
        exit 1
    }
    if ($restoreSourceSnapshotBeforeLock.AggregateSha256 -cne
            $restoreSourceSnapshotAfterPublish.AggregateSha256 -or
        $restoreArchiveSnapshotBeforeLock.AggregateSha256 -cne
            $restoreArchiveSnapshotAfterPublish.AggregateSha256 -or
        $packageBuildSnapshotBeforeLock.AggregateSha256 -cne
            $packageBuildSnapshotAfterPublish.AggregateSha256 -or
        $packageClosureSnapshotBeforeLock.AggregateSha256 -cne
            $packageClosureSnapshotAfterPublish.AggregateSha256 -or
        $postRestoreEvaluation.compileSha256 -cne
            $postRestoreEvaluationAfterPublish.compileSha256 -or
        $postRestoreEvaluation.analyzerSha256 -cne
            $postRestoreEvaluationAfterPublish.analyzerSha256) {
        Write-Host (
            "TEST_FAIL efi.shell.publish package_or_restore_input_changed_during_publish")
        exit 1
    }

    $publishOutput = Join-Path $repositoryRoot (
        "src\OS\bin\$Configuration\net10.0\$Runtime\publish\OS.exe")
    $efiFullPath = $outputPublicationPlan.EfiPath
    $linkerMapOutputPath = $outputPublicationPlan.MapPath
    $linkResponseOutputPath = $outputPublicationPlan.ResponsePath
    $linkEvidenceOutputPath = $outputPublicationPlan.EvidencePath
    $serializerObjectOutputPath =
        $outputPublicationPlan.SerializerObjectPath
    try {
        Clear-M01OutputPublicationLeaves -Plan $outputPublicationPlan
    }
    catch {
        Write-Host (
            "TEST_FAIL efi.shell.publish output_authority_failed detail=" +
            (Format-EvidenceValue $_.Exception.Message))
        exit 1
    }
    Write-Host ("EFI_PUBLISH_OUTPUT_AUTHORITY status=ok leaves=" + $outputPublicationPlan.Paths.Count)
    $dispatchObjectOutputPath = $efiFullPath + ".dispatch.obj"
    if ($EnableM04InterfaceDispatchProbe) {
        Assert-M01NoReparsePoints -RootPath $repositoryRoot -Path $dispatchObjectPath -Reason "dispatch_object_authority"
        if (-not (Test-Path -LiteralPath $dispatchObjectPath -PathType Leaf) -or
            (Get-Item -LiteralPath $dispatchObjectPath).Length -ne 828 -or
            (Get-FileHash -LiteralPath $dispatchObjectPath -Algorithm SHA256).Hash -cne
                '228630B0AD4D6AB298414FDA0FCF07E76CB4699A63339FA59BA331AFDC3AEB61') {
            throw 'interface_dispatch_object_identity_mismatch'
        }
    }
    $catchObjectOutputPath = $efiFullPath + ".catch.obj"
    if ($EnableM04SimpleThrowCatchProbe) {
        Assert-M01NoReparsePoints -RootPath $repositoryRoot -Path $catchObjectPath -Reason "catch_object_authority"
        if (-not (Test-Path -LiteralPath $catchObjectPath -PathType Leaf) -or
            (Get-Item -LiteralPath $catchObjectPath).Length -ne 1350 -or
            (Get-FileHash -LiteralPath $catchObjectPath -Algorithm SHA256).Hash -cne
                'DD32DC30808C3B6DD42981DF652B95F62236BDC8C45C1DAA3DDE32BAB1C981AB') {
            throw 'simple_catch_object_identity_mismatch'
        }
    }
    $referenceObjectOutputPath = $efiFullPath + ".references.obj"
    if ($EnableM04CheckedReferenceBarrierProbe -or $EnableM04GcStaticRootMaterializationProbe) {
        Assert-M01NoReparsePoints -RootPath $repositoryRoot -Path $referenceObjectPath -Reason "reference_object_authority"
        if (-not (Test-Path -LiteralPath $referenceObjectPath -PathType Leaf) -or
            (Get-Item -LiteralPath $referenceObjectPath).Length -ne 1645 -or
            (Get-FileHash -LiteralPath $referenceObjectPath -Algorithm SHA256).Hash -cne
                'E96ABF595503B9D1AE38DEE740F93AE9B3EB84F05D6395B931E4B5D74F65D808') {
            throw 'checked_reference_object_identity_mismatch'
        }
    }

    foreach ($requiredOutput in @(
        $publishOutput,
        $linkerMapWorkingPath,
        $linkResponseWorkingPath,
        $serializerObjectPath,
        $managedAssemblyPath
    )) {
        Assert-M01NoReparsePoints `
            -RootPath $repositoryRoot `
            -Path $requiredOutput `
            -Reason "publish_working_output_authority"
        if (-not (Test-Path -LiteralPath $requiredOutput -PathType Leaf) -or
            (Get-Item -LiteralPath $requiredOutput).Length -eq 0) {
            Write-Host (
                "TEST_FAIL efi.shell.publish linker_evidence_missing path=" +
                $requiredOutput)
            exit 1
        }
    }

    $serializerObjectBytes = (Get-Item -LiteralPath $serializerObjectPath).Length
    $serializerObjectHash = (
        Get-FileHash -LiteralPath $serializerObjectPath -Algorithm SHA256
    ).Hash.ToLowerInvariant()
    if ($serializerObjectBytes -ne $script:ExpectedSerializerObjectBytes -or
        $serializerObjectHash -cne
            $script:ExpectedSerializerObjectSha256.ToLowerInvariant()) {
        Write-Host (
            "TEST_FAIL efi.shell.publish serializer_object_mismatch bytes=" +
            $serializerObjectBytes +
            " sha256=" +
            $serializerObjectHash)
        exit 1
    }

    try {
        Copy-M01FileCreateNew `
            -SourcePath $publishOutput `
            -SourceRootPath $repositoryRoot `
            -DestinationPath $efiFullPath `
            -DestinationRootPath $outputPublicationPlan.Directory `
            -Purpose "published-efi"
        Copy-M01FileCreateNew `
            -SourcePath $linkerMapWorkingPath `
            -SourceRootPath $repositoryRoot `
            -DestinationPath $linkerMapOutputPath `
            -DestinationRootPath $outputPublicationPlan.Directory `
            -Purpose "preserved-link-map"
        Copy-M01FileCreateNew `
            -SourcePath $linkResponseWorkingPath `
            -SourceRootPath $repositoryRoot `
            -DestinationPath $linkResponseOutputPath `
            -DestinationRootPath $outputPublicationPlan.Directory `
            -Purpose "preserved-link-response"
        Copy-M01FileCreateNew `
            -SourcePath $serializerObjectPath `
            -SourceRootPath $repositoryRoot `
            -DestinationPath $serializerObjectOutputPath `
            -DestinationRootPath $outputPublicationPlan.Directory `
            -Purpose "preserved-serializer-object"
        if ($EnableM04InterfaceDispatchProbe) {
            Copy-M01FileCreateNew -SourcePath $dispatchObjectPath -SourceRootPath $repositoryRoot `
                -DestinationPath $dispatchObjectOutputPath -DestinationRootPath $outputPublicationPlan.Directory `
                -Purpose "preserved-interface-dispatch-object"
        }
        if ($EnableM04SimpleThrowCatchProbe) {
            Copy-M01FileCreateNew -SourcePath $catchObjectPath -SourceRootPath $repositoryRoot `
                -DestinationPath $catchObjectOutputPath -DestinationRootPath $outputPublicationPlan.Directory `
                -Purpose "preserved-simple-catch-object"
        }
        if ($EnableM04CheckedReferenceBarrierProbe -or $EnableM04GcStaticRootMaterializationProbe) {
            Copy-M01FileCreateNew -SourcePath $referenceObjectPath -SourceRootPath $repositoryRoot `
                -DestinationPath $referenceObjectOutputPath -DestinationRootPath $outputPublicationPlan.Directory `
                -Purpose "preserved-checked-reference-object"
        }
    }
    catch {
        Write-Host (
            "TEST_FAIL efi.shell.publish output_publication_failed detail=" +
            (Format-EvidenceValue $_.Exception.Message))
        exit 1
    }

    $artifactHash = (Get-FileHash -LiteralPath $efiFullPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $linkerMapHash = (Get-FileHash -LiteralPath $linkerMapOutputPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $linkResponseHash = (Get-FileHash -LiteralPath $linkResponseOutputPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $projectHash = (Get-FileHash -LiteralPath $efiProjectPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $solutionHash = (
        Get-FileHash -LiteralPath $solutionPath -Algorithm SHA256
    ).Hash.ToLowerInvariant()
    $entrySourcePath = Join-Path $repositoryRoot "src\OS\Boot\EfiEntry.cs"
    $entrySourceHash = (Get-FileHash -LiteralPath $entrySourcePath -Algorithm SHA256).Hash.ToLowerInvariant()
    $serializerTargetsHash = (
        Get-FileHash -LiteralPath $serializerTargetsPath -Algorithm SHA256
    ).Hash.ToLowerInvariant()
    $serializerGeneratorProjectHash = (
        Get-FileHash -LiteralPath $serializerGeneratorProjectPath -Algorithm SHA256
    ).Hash.ToLowerInvariant()
    $serializerGeneratorProgramHash = (
        Get-FileHash -LiteralPath $serializerGeneratorProgramPath -Algorithm SHA256
    ).Hash.ToLowerInvariant()
    $serializerGeneratorWriterHash = (
        Get-FileHash -LiteralPath $serializerGeneratorWriterPath -Algorithm SHA256
    ).Hash.ToLowerInvariant()
    $serializerGeneratorSourceHash = (
        Get-FileHash -LiteralPath $serializerGeneratorSourcePath -Algorithm SHA256
    ).Hash.ToLowerInvariant()
    $instructionPublicationSourceHash = (
        Get-FileHash -LiteralPath $instructionPublicationSourcePath -Algorithm SHA256
    ).Hash.ToLowerInvariant()
    $runtimeImportAttributeSourceHash = (
        Get-FileHash -LiteralPath $runtimeImportAttributeSourcePath -Algorithm SHA256
    ).Hash.ToLowerInvariant()
    $methodImplAttributeSourceHash = (
        Get-FileHash -LiteralPath $methodImplAttributeSourcePath -Algorithm SHA256
    ).Hash.ToLowerInvariant()
    $managedAssemblyHash = (
        Get-FileHash -LiteralPath $managedAssemblyPath -Algorithm SHA256
    ).Hash.ToLowerInvariant()
    $projectAssetsHash = (
        Get-FileHash -LiteralPath $projectAssetsPath -Algorithm SHA256
    ).Hash.ToLowerInvariant()
    $runtimePackEvidence = @(
        $packageClosureSnapshotBeforeLock.Packages |
            Where-Object { [string] $_.rootKind -ceq "runtime" })
    if ($runtimePackEvidence.Count -ne 1) {
        Write-Host (
            "TEST_FAIL efi.shell.publish runtime_pack_evidence_count_invalid")
        exit 1
    }
    $actualIlCompilerFileVersion =
        Get-FileVersionEvidence -Path $actualIlCompilerPath

    $linkEvidence = [ordered] @{
        schemaVersion = 3
        configuration = $Configuration
        runtimeIdentifier = $Runtime
        artifactSha256 = $artifactHash
        artifactBytes = (Get-Item -LiteralPath $efiFullPath).Length
        linkerMapSha256 = $linkerMapHash
        linkResponseSha256 = $linkResponseHash
        solutionSha256 = $solutionHash
        sourceClosureSha256 =
            $sourceSnapshotBeforePublish.AggregateSha256
        sourceClosureInputs =
            $sourceSnapshotBeforePublish.Inputs
        restoreClosureSha256 =
            $restoreSnapshotBeforeLock.AggregateSha256
        restoreClosureInputs =
            $restoreSnapshotBeforeLock.Inputs
        restoreArchiveSourceRootPath = $globalPackageCacheRoot
        restoreArchiveClosureSha256 =
            $restoreArchiveSnapshotBeforeLock.AggregateSha256
        restoreArchiveInputs =
            $restoreArchiveSnapshotBeforeLock.Inputs
        pinnedRestoreSourcePath = $restoreSourceDirectory
        pinnedRestoreSourceSha256 =
            $restoreSourceSnapshotBeforeLock.AggregateSha256
        pinnedRestoreSourceInputs =
            $restoreSourceSnapshotBeforeLock.Inputs
        restoreImportProjectExtensionProps = $false
        restoreImportProjectExtensionTargets = $false
        restoreExcludeRestorePackageImports = $true
        publishImportProjectExtensionProps = $false
        publishImportProjectExtensionTargets = $false
        publishExcludeRestorePackageImports = $true
        projectExtensionsPath = $projectExtensionsDirectory
        projectAssetsPath = $projectAssetsPath
        projectAssetsSha256 = $projectAssetsHash
        packageRootPath = $restorePackagesDirectory
        packageRootKind = "isolated"
        packageClosureSha256 =
            $packageClosureSnapshotBeforeLock.AggregateSha256
        packages = $packageClosureSnapshotBeforeLock.Packages
        packageBuildClosureSha256 =
            $packageBuildSnapshotBeforeLock.AggregateSha256
        packageBuildClosureInputs =
            $packageBuildSnapshotBeforeLock.Inputs
        activePackageImportEdges =
            $script:ExpectedActivePackageImportEdges
        activePackageImportGraphSha256 =
            $activePackageImportGraphSha256
        packageTaskBindings =
            $script:ExpectedPackageTaskBindings
        packageTaskBindingsSha256 =
            $packageTaskBindingsSha256
        postRestoreEvaluation = $postRestoreEvaluation
        sourceProjectSha256 = $projectHash
        entrySourceSha256 = $entrySourceHash
        entryPointSymbol = "EfiMain"
        managedEntrySymbol = "OS_OS_Boot_EfiEntry__EfiMain"
        serializerSymbol = $script:ExpectedSerializerSymbol
        serializerExpectedSuccessCookie =
            $script:ExpectedSerializerSuccessCookie
        serializerLinkedObjectPath = $serializerObjectPath
        serializerObjectPath = $serializerObjectOutputPath
        serializerObjectBytes = $serializerObjectBytes
        serializerObjectSha256 = $serializerObjectHash
        serializerTargetsSha256 = $serializerTargetsHash
        serializerGeneratorProjectSha256 = $serializerGeneratorProjectHash
        serializerGeneratorProgramSha256 = $serializerGeneratorProgramHash
        serializerGeneratorWriterSha256 = $serializerGeneratorWriterHash
        serializerGeneratorSourceSha256 = $serializerGeneratorSourceHash
        instructionPublicationSourceSha256 = $instructionPublicationSourceHash
        runtimeImportAttributeSourceSha256 = $runtimeImportAttributeSourceHash
        methodImplAttributeSourceSha256 = $methodImplAttributeSourceHash
        managedAssemblyPath = $managedAssemblyPath
        managedAssemblySha256 = $managedAssemblyHash
        linkerPath = $linkerPath
        linkerVersion = $linkerVersion
        linkerSha256 = $linkerHash
        dotnetHostPath = $script:DotnetHostPath
        dotnetHostSha256 = $dotnetHostHash
        dotnetSdkVersion = $dotnetSdkVersion
        windowsSdkLibraryPath = $sdkLibPath
        windowsSdkVersion = $sdkVersion
        windowsSdkLibrarySha256 = $sdkLibHash
        ilCompilerPath = $actualIlCompilerPath
        ilCompilerDiscoverySourcePath = $resolvedIlCompilerPath
        ilCompilerPackageVersion = $ilCompilerPackageVersion
        ilCompilerFileVersion = $actualIlCompilerFileVersion
        ilCompilerSha256 = $ilCompilerHash
        nativeAotRuntimePackPath = $resolvedNativeAotRuntimePackPath
        nativeAotRuntimePackVersion = $nativeAotRuntimePackVersion
        nativeAotRuntimePackSha256 =
            $runtimePackEvidence[0].aggregateSha256
        nativeAotRuntimePackFileCount =
            $runtimePackEvidence[0].fileCount
        nativeAotRuntimePackBytes =
            $runtimePackEvidence[0].totalBytes
        ilCompilerTargetsPath = $ilCompilerTargetsPath
        ilLinkTargetsPath = $ilLinkTargetsPath
        ilLinkTasksAssemblyPath = $ilLinkTasksAssemblyPath
        ilcBuildTasksPath = $ilcBuildTasksPath
        ilcHostPackagePath = $ilCompilerHostPackageRoot
        ilcToolsPath = $ilcToolsPath
        ilcFrameworkPath = $ilcFrameworkPath
        ilcFrameworkNativePath = $ilcFrameworkNativePath
        ilcSdkPath = $ilcSdkPath
        ilcMibcPath = $ilcMibcPath
        languageTargetsPath = $languageTargetsPath
        csharpTargetsPath = $csharpTargetsPath
        commonTargetsPath = $commonTargetsPath
        runtimeListSha256 = $runtimeListHash
        generatedUtc = [DateTimeOffset]::UtcNow.ToString("O")
    }
    if ($EnableM04CheckedReferenceBarrierProbe -or $EnableM04GcStaticRootMaterializationProbe) {
        $linkEvidence.checkedReferenceLinkedObjectPath = $referenceObjectPath
        $linkEvidence.checkedReferenceObjectPath = $referenceObjectOutputPath
        $linkEvidence.checkedReferenceObjectBytes = 1645
        $linkEvidence.checkedReferenceObjectSha256 = 'e96abf595503b9d1ae38dee740f93ae9b3eb84f05d6395b931e4b5d74f65d808'
    }
    if ($EnableM04InterfaceDispatchProbe) {
        $linkEvidence.interfaceDispatchLinkedObjectPath = $dispatchObjectPath
        $linkEvidence.interfaceDispatchObjectPath = $dispatchObjectOutputPath
        $linkEvidence.interfaceDispatchObjectBytes = 828
        $linkEvidence.interfaceDispatchObjectSha256 = '228630b0ad4d6ab298414fda0fcf07e76cb4699a63339fa59ba331afdc3aeb61'
    }
    if ($EnableM04SimpleThrowCatchProbe) {
        $linkEvidence.simpleCatchLinkedObjectPath = $catchObjectPath
        $linkEvidence.simpleCatchObjectPath = $catchObjectOutputPath
        $linkEvidence.simpleCatchObjectBytes = 1350
        $linkEvidence.simpleCatchObjectSha256 = 'dd32dc30808c3b6dd42981df652b95f62236bdc8c45c1daa3dde32bab1c981ab'
    }
    $linkEvidenceJson = $linkEvidence | ConvertTo-Json -Depth 10
    try {
        Write-M01Utf8CreateNew `
            -Path $linkEvidenceOutputPath `
            -RootPath $outputPublicationPlan.Directory `
            -Text $linkEvidenceJson
        $verifierInputs = @(
            [pscustomobject] @{
                Path = $efiFullPath
                RootPath = $outputPublicationPlan.Directory
                Purpose = "final-efi"
            },
            [pscustomobject] @{
                Path = $linkerMapOutputPath
                RootPath = $outputPublicationPlan.Directory
                Purpose = "final-link-map"
            },
            [pscustomobject] @{
                Path = $linkResponseOutputPath
                RootPath = $outputPublicationPlan.Directory
                Purpose = "final-link-response"
            },
            [pscustomobject] @{
                Path = $linkEvidenceOutputPath
                RootPath = $outputPublicationPlan.Directory
                Purpose = "final-link-evidence"
            },
            [pscustomobject] @{
                Path = $serializerObjectOutputPath
                RootPath = $outputPublicationPlan.Directory
                Purpose = "preserved-serializer-object"
            },
            [pscustomobject] @{
                Path = $serializerObjectPath
                RootPath = $repositoryRoot
                Purpose = "linked-serializer-object"
            },
            [pscustomobject] @{
                Path = $linkerMapWorkingPath
                RootPath = $repositoryRoot
                Purpose = "linked-map"
            },
            [pscustomobject] @{
                Path = $managedAssemblyPath
                RootPath = $repositoryRoot
                Purpose = "managed-dll"
            }
        )
        if ($EnableM04CheckedReferenceBarrierProbe -or $EnableM04GcStaticRootMaterializationProbe) {
            $verifierInputs += [pscustomobject]@{Path=$referenceObjectPath; RootPath=$repositoryRoot; Purpose="linked-reference-object"}
            $verifierInputs += [pscustomobject]@{Path=$referenceObjectOutputPath; RootPath=$outputPublicationPlan.Directory; Purpose="preserved-reference-object"}
        }
        if ($EnableM04InterfaceDispatchProbe) {
            $verifierInputs += [pscustomobject]@{Path=$dispatchObjectPath; RootPath=$repositoryRoot; Purpose="linked-dispatch-object"}
            $verifierInputs += [pscustomobject]@{Path=$dispatchObjectOutputPath; RootPath=$outputPublicationPlan.Directory; Purpose="preserved-dispatch-object"}
        }
        if ($EnableM04SimpleThrowCatchProbe) {
            $verifierInputs += [pscustomobject]@{Path=$catchObjectPath; RootPath=$repositoryRoot; Purpose="linked-catch-object"}
            $verifierInputs += [pscustomobject]@{Path=$catchObjectOutputPath; RootPath=$outputPublicationPlan.Directory; Purpose="preserved-catch-object"}
        }
        $verifierInputLocks = Open-M01VerifierInputLocks -Inputs $verifierInputs
        $expectedVerifierInputCount = if ($EnableM04CheckedReferenceBarrierProbe -or $EnableM04GcStaticRootMaterializationProbe) { 10 } else { 8 }
        if ($EnableM04InterfaceDispatchProbe) { $expectedVerifierInputCount += 2 }
        if ($EnableM04SimpleThrowCatchProbe) { $expectedVerifierInputCount += 2 }
        if ($verifierInputLocks.Count -ne $expectedVerifierInputCount) {
            throw "verifier input authority count mismatch"
        }
        Assert-M01DistinctAuthorityRecords -Records (
            @($verifierInputLocks.ToArray()) +
            @($script:M01LogAuthority))
    }
    catch {
        Write-Host (
            "TEST_FAIL efi.shell.publish verifier_input_authority_failed detail=" +
            (Format-EvidenceValue $_.Exception.Message))
        exit 1
    }
    Write-Host ("EFI_PUBLISH_VERIFIER_INPUT_AUTHORITY status=ok inputs=" + $verifierInputLocks.Count)

    $verifierScript = Join-Path $repositoryRoot "scripts\test-efi-artifact.ps1"
    $verificationArguments = @(
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        $verifierScript,
        "-EfiArtifact",
        $efiFullPath,
        "-ProjectPath",
        $efiProjectPath,
        "-EntrySourcePath",
        $entrySourcePath,
        "-SerializerObjectPath",
        $serializerObjectOutputPath,
        "-LinkerMapPath",
        $linkerMapOutputPath,
        "-LinkResponsePath",
        $linkResponseOutputPath,
        "-LinkEvidencePath",
        $linkEvidenceOutputPath
    )
    if ($EnableM04IndependentEndianConformanceProbe) {
        $verificationArguments += @(
            "-EnableM04IndependentEndianConformanceProbe",
            "true")
    }
    if ($EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe) {
        $verificationArguments += @(
            "-EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe",
            "true")
    }
    if ($EnableM04ArithmeticConformanceProbe) {
        $verificationArguments += @(
            "-EnableM04ArithmeticConformanceProbe",
            "true")
    }
    if ($EnableM04LayoutContractProbe) {
        $verificationArguments += @(
            "-EnableM04LayoutContractProbe",
            "true")
    }
    if ($EnableM04ManagedHeapPageRangeProbe) {
        $verificationArguments += @("-EnableM04ManagedHeapPageRangeProbe", "true")
    }
    $verificationArguments += @("-EnableM04RhpNewFastProbe", $EnableM04RhpNewFastProbe.IsPresent.ToString().ToLowerInvariant())
    $verificationArguments += @("-EnableM04RhpNewFastBeforeBindFailureProbe", $EnableM04RhpNewFastBeforeBindFailureProbe.IsPresent.ToString().ToLowerInvariant())
    $verificationArguments += @("-EnableM04RhpNewFastExhaustionFailureProbe", $EnableM04RhpNewFastExhaustionFailureProbe.IsPresent.ToString().ToLowerInvariant())
    $verificationArguments += @("-EnableM04SzArrayAllocationProbe", $EnableM04SzArrayAllocationProbe.IsPresent.ToString().ToLowerInvariant())
    $verificationArguments += @("-EnableM04SzArrayAllocationBeforeBindProbe", $EnableM04SzArrayAllocationBeforeBindProbe.IsPresent.ToString().ToLowerInvariant())
    $verificationArguments += @("-EnableM04SzArrayAllocationExhaustionProbe", $EnableM04SzArrayAllocationExhaustionProbe.IsPresent.ToString().ToLowerInvariant())
    $verificationArguments += @("-EnableM04StringAllocationProbe", $EnableM04StringAllocationProbe.IsPresent.ToString().ToLowerInvariant())
    $verificationArguments += @("-EnableM04StringAllocationBeforeBindProbe", $EnableM04StringAllocationBeforeBindProbe.IsPresent.ToString().ToLowerInvariant())
    $verificationArguments += @("-EnableM04StringAllocationExhaustionProbe", $EnableM04StringAllocationExhaustionProbe.IsPresent.ToString().ToLowerInvariant())
    $verificationArguments += @("-EnableM04StringAllocationInvalidLengthProbe", $EnableM04StringAllocationInvalidLengthProbe.IsPresent.ToString().ToLowerInvariant())
    $verificationArguments += @("-EnableM03MemoryMapCaptureProbe", $EnableM03MemoryMapCaptureProbe.IsPresent.ToString().ToLowerInvariant())
    $verificationArguments += @("-EnableM03FirmwareEntropySeedProbe", $EnableM03FirmwareEntropySeedProbe.IsPresent.ToString().ToLowerInvariant())
    $verificationArguments += @("-EnableM04TypeManagerModuleRegistrationProbe", $EnableM04TypeManagerModuleRegistrationProbe.IsPresent.ToString().ToLowerInvariant())
    $verificationArguments += @("-EnableM04X64UnwindMetadataProbe", $EnableM04X64UnwindMetadataProbe.IsPresent.ToString().ToLowerInvariant())
    $verificationArguments += @("-EnableM04SimpleThrowCatchProbe", $EnableM04SimpleThrowCatchProbe.IsPresent.ToString().ToLowerInvariant())
    $verificationArguments += @("-EnableM04GcStaticRootMaterializationProbe", $EnableM04GcStaticRootMaterializationProbe.IsPresent.ToString().ToLowerInvariant())
    $verificationArguments += @("-EnableM04SeededCollectorChurnProbe", $EnableM04SeededCollectorChurnProbe.IsPresent.ToString().ToLowerInvariant())
    $verificationArguments += @("-EnableM03TypedExitBootServicesCallProbe", $EnableM03TypedExitBootServicesCallProbe.IsPresent.ToString().ToLowerInvariant())
    $verificationArguments += @("-EnableM03FirstRealExitBootServicesProbe", $EnableM03FirstRealExitBootServicesProbe.IsPresent.ToString().ToLowerInvariant())
    $verificationArguments += @("-EnableM03FirmwareAdapterRevocationProbe", $EnableM03FirmwareAdapterRevocationProbe.IsPresent.ToString().ToLowerInvariant())
    $verificationArguments += @("-EnableM03OwnedSubsystemSurvivalProbe", $EnableM03OwnedSubsystemSurvivalProbe.IsPresent.ToString().ToLowerInvariant())
    $verificationArguments += @("-EnableM03OwnedSubsystemPanicProbe", $EnableM03OwnedSubsystemPanicProbe.IsPresent.ToString().ToLowerInvariant())
    $verificationArguments += @("-EnableM04ClassConstructorExecutionProbe", $EnableM04ClassConstructorExecutionProbe.IsPresent.ToString().ToLowerInvariant())
    $verificationArguments += @("-EnableM04InterfaceDispatchProbe", $EnableM04InterfaceDispatchProbe.IsPresent.ToString().ToLowerInvariant())
    $verificationArguments += @("-EnableM04CheckedReferenceBarrierProbe", $EnableM04CheckedReferenceBarrierProbe.IsPresent.ToString().ToLowerInvariant())
    $verificationArguments += @("-EnableM01LoadedImageStackProbe", $EnableM01LoadedImageStackProbe.IsPresent.ToString().ToLowerInvariant())
    $verificationArguments += @("-EnableM01LiveAllocatorProbe", $EnableM01LiveAllocatorProbe.IsPresent.ToString().ToLowerInvariant())
    $verificationArguments += @("-EnableM01OwnedPagingProbe", $EnableM01OwnedPagingProbe.IsPresent.ToString().ToLowerInvariant())
    $verificationArguments += @("-EnableM02SafeStubProbe", $EnableM02SafeStubProbe.IsPresent.ToString().ToLowerInvariant())
    $verificationArguments += @("-EnableM02CheckedPortIoBindingProbe", $EnableM02CheckedPortIoBindingProbe.IsPresent.ToString().ToLowerInvariant())
    $verificationArguments += @("-EnableM02Com1OwnedPortIoProbe", $EnableM02Com1OwnedPortIoProbe.IsPresent.ToString().ToLowerInvariant())
    $verificationArguments += @("-EnableUefiGoneSimulationProbe", $EnableUefiGoneSimulationProbe.IsPresent.ToString().ToLowerInvariant())
    $verificationArguments += @("-EnableExitBootServicesFreshMapProbe", $EnableExitBootServicesFreshMapProbe.IsPresent.ToString().ToLowerInvariant())
    $verificationArguments += @("-EnableAcpiPhysicalDiscoveryProbe", $EnableAcpiPhysicalDiscoveryProbe.IsPresent.ToString().ToLowerInvariant())

    if ($EnableM02OwnedCom1ReadyProbe) {
        $verificationArguments += @(
            "-EnableM02OwnedCom1ReadyProbe",
            "true")
    }
    if ($EnableM02PanicSafeOwnedCom1Probe) {
        $verificationArguments += @(
            "-EnableM02PanicSafeOwnedCom1Probe",
            "true")
    }
    if ($EnableM02OwnedX64GdtEncodingProbe) {
        $verificationArguments += @(
            "-EnableM02OwnedX64GdtEncodingProbe",
            "true")
    }
    if ($EnableM02GuardedIstStackAllocationProbe) {
        $verificationArguments += @(
            "-EnableM02GuardedIstStackAllocationProbe",
            "true")
    }
    if ($EnableM02OwnedGdtTssTerminalProbe) {
        $verificationArguments += @(
            "-EnableM02OwnedGdtTssTerminalProbe",
            "true")
    }
    if ($EnableM02NormalizedInterruptFramePageProbe) {
        $verificationArguments += @(
            "-EnableM02NormalizedInterruptFramePageProbe",
            "true")
    }
    if ($EnableM02OwnedIdtGatePopulationProbe) {
        $verificationArguments += @(
            "-EnableM02OwnedIdtGatePopulationProbe",
            "true")
    }
    if ($EnableM02IdtrContinuedExecutionProbe) {
        $verificationArguments += @(
            "-EnableM02IdtrContinuedExecutionProbe",
            "true")
    }
    if ($EnableM02Ud2VectorStateProbe) {
        $verificationArguments += @(
            "-EnableM02Ud2VectorStateProbe",
            "true")
    }
    if ($EnableM02PageFaultCr2ErrorCodeProbe) {
        $verificationArguments += @(
            "-EnableM02PageFaultCr2ErrorCodeProbe",
            "true")
    }

    if ($EnableM02DoubleFaultIstProbe) {
        $verificationArguments += @(
            "-EnableM02DoubleFaultIstProbe",
            "true")
    }
    if ($EnableM02UnexpectedVectorPanicProbe) {
        $verificationArguments += @(
            "-EnableM02UnexpectedVectorPanicProbe",
            "true")
    }
    $verificationStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $verificationResult = Invoke-M01ArtifactVerifier `
            -Arguments $verificationArguments `
            -TimeoutSeconds $ArtifactVerifierTimeoutSeconds
    }
    catch {
        $verificationStopwatch.Stop()
        Write-Host (
            "TEST_FAIL efi.shell.publish artifact_verifier_launch_failed detail=" +
            (Format-EvidenceValue $_.Exception.Message))
        exit 1
    }
    $verificationStopwatch.Stop()
    try {
        Assert-M01VerifierInputLocks -Locks $verifierInputLocks
        Assert-M01WritableAuthorityIdentity `
            -Record $script:M01LogAuthority
        Assert-M01DistinctAuthorityRecords -Records (
            @($verifierInputLocks.ToArray()) +
            @($script:M01LogAuthority))
    }
    catch {
        Write-Host (
            "TEST_FAIL efi.shell.publish verifier_input_changed detail=" +
            (Format-EvidenceValue $_.Exception.Message))
        exit 1
    }
    Write-Host ("EFI_PUBLISH_VERIFIER_INPUTS_UNCHANGED status=ok inputs=" + $verifierInputLocks.Count)
    foreach ($verificationLine in $verificationResult.OutputLines) {
        Write-Host $verificationLine
        Write-M01LogLines `
            -Authority $script:M01LogAuthority `
            -Lines @([string] $verificationLine)
    }
    if ($verificationResult.TimedOut) {
        Write-Host (
            "TEST_FAIL efi.shell.publish artifact_verification_timeout " +
            "timeout_seconds=" +
            $ArtifactVerifierTimeoutSeconds +
            " elapsed_ms=" +
            $verificationStopwatch.ElapsedMilliseconds +
            " termination_confirmed=" +
            $verificationResult.TerminationConfirmed.ToString().ToLowerInvariant() +
            " output_capture_succeeded=" +
            $verificationResult.OutputCaptureSucceeded.ToString().ToLowerInvariant())
        exit 1
    }
    if (-not $verificationResult.TerminationConfirmed) {
        Write-Host (
            "TEST_FAIL efi.shell.publish artifact_verification_tree_termination_failed " +
            "elapsed_ms=" +
            $verificationStopwatch.ElapsedMilliseconds)
        exit 1
    }
    if ($verificationResult.OutputDrainTimedOut -or
        -not $verificationResult.OutputCaptureSucceeded) {
        Write-Host (
            "TEST_FAIL efi.shell.publish artifact_verification_output_capture_failed " +
            "drain_timed_out=" +
            $verificationResult.OutputDrainTimedOut.ToString().ToLowerInvariant() +
            " elapsed_ms=" +
            $verificationStopwatch.ElapsedMilliseconds)
        exit 1
    }
    if ($verificationResult.ExitCode -ne 0) {
        Write-Host (
            "TEST_FAIL efi.shell.publish artifact_verification_failed exit_code=" +
            $verificationResult.ExitCode +
            " log=" +
            $LogPath)
        exit $verificationResult.ExitCode
    }
    Write-Host (
        "EFI_ARTIFACT_VERIFIER_BOUND status=ok elapsed_ms=" +
        $verificationStopwatch.ElapsedMilliseconds +
        " timeout_seconds=" +
        $ArtifactVerifierTimeoutSeconds)

    Write-Host (
        "EFI_PUBLISH_TOOLCHAIN_VERIFIED linkerPath=" +
        (Format-EvidenceValue $linkerPath) +
        " mapSha256=" +
        $linkerMapHash +
        " responseSha256=" +
        $linkResponseHash)
    Write-Host (
        "EFI_SERIALIZER_OBJECT path=" +
        (Format-EvidenceValue $serializerObjectOutputPath) +
        " bytes=" +
        $serializerObjectBytes +
        " sha256=" +
        $serializerObjectHash)
    Write-Host "TEST_PASS efi.shell.publish"
    Write-Host "EFI_ARTIFACT $EfiOutputPath"
    Write-Host "EFI_LINKER_MAP $linkerMapOutputPath"
    Write-Host "EFI_LINK_EVIDENCE $linkEvidenceOutputPath"
    exit 0
}

$logText = Read-M01LogAuthorityText -Authority $script:M01LogAuthority
if ($logText.IndexOf("Platform linker not found", [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
    Write-Host "TEST_FAIL efi.shell.publish platform_linker_missing log=$LogPath"
    exit $exitCode
}

Write-Host "TEST_FAIL efi.shell.publish exit_code=$exitCode log=$LogPath"
exit $exitCode
}
finally {
    Close-M01AuthorityRecords -Records $verifierInputLocks
    if ($null -ne $script:M01LogAuthority) {
        $script:M01LogAuthority.Stream.Dispose()
        $script:M01LogAuthority = $null
    }
    Close-M01PublishSourceLocks -Locks $packageLocks
    Close-M01PublishSourceLocks -Locks $restoreLocks
    Close-M01PublishSourceLocks -Locks $restoreSourceLocks
    Close-M01PublishSourceLocks -Locks $restoreArchiveSourceLocks
    Close-M01PublishSourceLocks -Locks $sourceLocks
    Close-M01PublishOutputLock -Lock $publishOutputLock
}
