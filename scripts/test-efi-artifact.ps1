[CmdletBinding()]
param(
    [string]$EfiArtifact = "artifacts/efi/BOOTX64.EFI",
    [string]$ProjectPath = "src/OS/OS.csproj",
    [string]$EntrySourcePath = "src/OS/Boot/EfiEntry.cs",
    [string]$LinkerMapPath = "",
    [string]$LinkResponsePath = "",
    [string]$LinkEvidencePath = "",
    [string]$SerializerObjectPath = "",
    [switch]$EnableM02OwnedCom1ReadyProbe,
    [switch]$EnableM02PanicSafeOwnedCom1Probe,
    [switch]$EnableM02OwnedX64GdtEncodingProbe,
    [switch]$EnableM02GuardedIstStackAllocationProbe,
    [switch]$EnableM02OwnedGdtTssTerminalProbe,
    [switch]$EnableM02NormalizedInterruptFramePageProbe,
    [switch]$EnableM02OwnedIdtGatePopulationProbe,
    [switch]$EnableM02IdtrContinuedExecutionProbe,
    [switch]$EnableM02Ud2VectorStateProbe,
    [switch]$EnableM02PageFaultCr2ErrorCodeProbe,
    [switch]$EnableM04IndependentEndianConformanceProbe,
    [switch]$EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe,
    [switch]$EnableM04ArithmeticConformanceProbe,
    [switch]$EnableM04LayoutContractProbe,
    [switch]$EnableM04ManagedHeapPageRangeProbe,
    [switch]$EnableM04RhpNewFastProbe,
    [switch]$EnableM04RhpNewFastBeforeBindFailureProbe,
    [switch]$EnableM04RhpNewFastExhaustionFailureProbe,
    [switch]$EnableM01LoadedImageStackProbe,
    [switch]$EnableM01LiveAllocatorProbe,
    [switch]$EnableM01OwnedPagingProbe,
    [switch]$EnableM02SafeStubProbe,
    [switch]$EnableM02CheckedPortIoBindingProbe,
    [switch]$EnableM02Com1OwnedPortIoProbe,
    [switch]$EnableUefiGoneSimulationProbe,
    [switch]$EnableExitBootServicesFreshMapProbe,
    [switch]$EnableAcpiPhysicalDiscoveryProbe
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$testName = "efi.artifact.verify"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$serializerSymbol = "CLROS_X64_SerializeInstructionPublication"
$serializerSizeBytes = 20
$serializerExpectedSuccessCookie = 1397902403
$serializerObjectSizeBytes = 336
$maximumRuntimeFunctionRecords = 2048
$maximumLinkResponseTokens = 4096
$maximumManagedAssemblyBytes = 67108864L
$maximumAuthorityTextBytes = 16777216L
$maximumProjectAssetsBytes = 4194304L
$maximumPackageMetadataBytes = 65536L
$maximumPackageFileBytes = 67108864L
$maximumPackageTreeBytes = 134217728L
$maximumPackageTreeEntries = 1024
$maximumPackageTreeFiles = 512
$expectedSourceClosureInputCount = 90
$postRestoreEvaluationTimeoutSeconds = 120
$postRestoreEvaluationOutputCharacterLimit = 4194304
$minimumExternalProcessTimeoutSeconds = 5
$maximumExternalProcessTimeoutSeconds = 3600
$minimumExternalProcessOutputCharacters = 4096
$maximumExternalProcessOutputCharacters = 67108864
$script:authorityReadHandles =
    New-Object 'System.Collections.Generic.List[object]'
$script:authorityReadHandlesByPath =
    New-Object 'System.Collections.Generic.Dictionary[string,object]' (
        [System.StringComparer]::OrdinalIgnoreCase)
$msbuildWildcardPropertyNames = @(
    "ImportByWildcardBeforeMicrosoftCommonProps",
    "ImportByWildcardAfterMicrosoftCommonProps",
    "ImportUserLocationsByWildcardBeforeMicrosoftCommonProps",
    "ImportUserLocationsByWildcardAfterMicrosoftCommonProps",
    "ImportByWildcardBeforeMicrosoftCommonTargets",
    "ImportByWildcardAfterMicrosoftCommonTargets",
    "ImportUserLocationsByWildcardBeforeMicrosoftCommonTargets",
    "ImportUserLocationsByWildcardAfterMicrosoftCommonTargets",
    "ImportByWildcardBeforeMicrosoftCSharpTargets",
    "ImportByWildcardAfterMicrosoftCSharpTargets",
    "ImportUserLocationsByWildcardBeforeMicrosoftCSharpTargets",
    "ImportUserLocationsByWildcardAfterMicrosoftCSharpTargets",
    "ImportByWildcardBeforeMicrosoftCommonCrossTargetingTargets",
    "ImportByWildcardAfterMicrosoftCommonCrossTargetingTargets"
)
$serializerObjectSha256 = "da97e50c95e479541b65eb3f96d9c6ded163735857650bee9416324086d55c7b"
$serializerBytesSha256 = "df76c8e39e0768d26ea27206ff102d7eadcc35d2c9bdf547b52b1171fd47bdaf"
[byte[]]$expectedSerializerBytes = @(
    0x53,
    0xB8, 0x00, 0x00, 0x00, 0x00,
    0xB9, 0x00, 0x00, 0x00, 0x00,
    0x0F, 0xA2,
    0xB8, 0x43, 0x4C, 0x52, 0x53,
    0x5B,
    0xC3
)
[byte[]]$expectedUnwindInfoBytes = @(
    0x01, 0x01, 0x01, 0x00,
    0x01, 0x30, 0x00, 0x00
)
$expectedEfiCompileItems = @(
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
    '..\OS.Kernel\Diagnostics\InvalidOpcodeVectorAuthority.cs|Diagnostics\InvalidOpcodeVectorAuthority.cs',
    '..\OS.Kernel\Diagnostics\CpuFaultFrame.cs|Diagnostics\CpuFaultFrame.cs',
    '..\OS.Kernel\Diagnostics\DisabledDefaultUd2VectorState.cs|Diagnostics\DisabledDefaultUd2VectorState.cs',
    '..\OS.Kernel\Diagnostics\DisabledDefaultUd2VectorStateTransition.cs|Diagnostics\DisabledDefaultUd2VectorStateTransition.cs',
    '..\..\std\no-runtime\shared\IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMOSA.cs|Std\IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMOSA.cs',
    '..\..\std\no-runtime\shared\SpanOverlapMemoryCastUnsafeWidthConformanceVectors.cs|Std\SpanOverlapMemoryCastUnsafeWidthConformanceVectors.cs',
    '..\..\std\no-runtime\shared\IndependentArithmeticShiftCastN64BitDivisionRemainder.cs|Std\IndependentArithmeticShiftCastN64BitDivisionRemainder.cs',
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
    'Runtime\RhpNewFastAllocation.cs|'
)
$expectedPackageBuildClosure = @(
    [pscustomobject]@{
        Path = 'microsoft.dotnet.ilcompiler/10.0.8/build/Microsoft.DotNet.ILCompiler.SingleEntry.targets'
        Sha256 = '345c9448182507befa430e80622f68c7ab09debe9c419d98b418f7f1cb79c34e'
    },
    [pscustomobject]@{
        Path = 'microsoft.dotnet.ilcompiler/10.0.8/build/Microsoft.NETCore.Native.Publish.targets'
        Sha256 = '6fcb3f0491a79fd5a67281b44439e0685c17fe4c848a07477ab2a57a10a68c60'
    },
    [pscustomobject]@{
        Path = 'microsoft.dotnet.ilcompiler/10.0.8/build/Microsoft.NETCore.Native.Windows.targets'
        Sha256 = '547266fbf1e772518edda8194413ce99906d3deb8bef074bbcca1033c31bc54c'
    },
    [pscustomobject]@{
        Path = 'microsoft.dotnet.ilcompiler/10.0.8/build/Microsoft.NETCore.Native.targets'
        Sha256 = '22516102af0e4bdafa004795d3bbc30d617efe5bb7b809005d1ea3a00c436f6d'
    },
    [pscustomobject]@{
        Path = 'microsoft.dotnet.ilcompiler/10.0.8/tools/netstandard/ILCompiler.Build.Tasks.dll'
        Sha256 = '4c8cd85bcf8e2d51d69c0fce3eaea55c2c8b23765da747c0c6cbfc38f9cccd88'
    },
    [pscustomobject]@{
        Path = 'microsoft.dotnet.ilcompiler/10.0.8/tools/netstandard/ILCompiler.Build.Tasks.deps.json'
        Sha256 = '893364a48ebfd6ca97a440e32fc2252f53dbc4f4c64d63f62ac9a583365ec7c6'
    },
    [pscustomobject]@{
        Path = 'microsoft.net.illink.tasks/10.0.10/build/Microsoft.NET.ILLink.targets'
        Sha256 = 'f1bbe7e9e79badb900a594691fe4c6738e906d911ee3e50975434d09035a2568'
    },
    [pscustomobject]@{
        Path = 'microsoft.net.illink.tasks/10.0.10/tools/net/ILLink.Tasks.dll'
        Sha256 = '9c5ffc1e594b75c1c716173c12f501ff4a7aa0cfb7a84019c7176f2a3f4c6120'
    },
    [pscustomobject]@{
        Path = 'microsoft.net.illink.tasks/10.0.10/tools/net/ILLink.Tasks.deps.json'
        Sha256 = '4bd1d4ac7c249fa1911c53106c757fd390fb40aad0d690368bcda0941ff8bbae'
    }
)
$expectedRestoreLibraries = [ordered]@{
    'Microsoft.DotNet.ILCompiler/10.0.8' = [pscustomobject]@{
        Path = 'microsoft.dotnet.ilcompiler/10.0.8'
        Sha512 = 'RJxitcN5CCyZDcPNXKLsecwKvACzmy8C1z8hGM9+hFcnPhv1jDysJFFIeUHIPWaZ6wDAfYtZcgKEtegvL2Nz8A=='
        FileCount = 20
    }
    'Microsoft.NET.ILLink.Tasks/10.0.10' = [pscustomobject]@{
        Path = 'microsoft.net.illink.tasks/10.0.10'
        Sha512 = 'f5VCIE7AJpd5YvzNTeMGVzQIgyE9tX+AreTYwQF+REbu+DZo/2Ae+jNSwhPEYrVz6RRkd7y8ubXjk6Nn6Ka+Cg=='
        FileCount = 42
    }
    'runtime.win-x64.Microsoft.DotNet.ILCompiler/10.0.8' = [pscustomobject]@{
        Path = 'runtime.win-x64.microsoft.dotnet.ilcompiler/10.0.8'
        Sha512 = 'S+9omeowlFjRReD0VB/Q4u3DcN7+vueJSF37saFj0U2OSJIkfI7gGKQRsDxGIf0MwLha14qGrjRC3bDGhvF0Gg=='
        FileCount = 13
    }
}
$expectedConsumedPackageTrees = @(
    [pscustomobject]@{
        Id = 'microsoft.dotnet.ilcompiler'
        Version = '10.0.8'
        ContentHashSha512 = 'RJxitcN5CCyZDcPNXKLsecwKvACzmy8C1z8hGM9+hFcnPhv1jDysJFFIeUHIPWaZ6wDAfYtZcgKEtegvL2Nz8A=='
        FileCount = 20
        TotalBytes = 404859L
        AggregateSha256 = 'b9c5a195c93e515f34066c9d8b0b3827cf1a32ce681454d284c489622a5bfcde'
        RootKind = 'restore'
    },
    [pscustomobject]@{
        Id = 'microsoft.net.illink.tasks'
        Version = '10.0.10'
        ContentHashSha512 = 'f5VCIE7AJpd5YvzNTeMGVzQIgyE9tX+AreTYwQF+REbu+DZo/2Ae+jNSwhPEYrVz6RRkd7y8ubXjk6Nn6Ka+Cg=='
        FileCount = 42
        TotalBytes = 5337318L
        AggregateSha256 = '0edc00810e9462f8d6a7468835861db6d2833f970539e6d4eb82cde5eb42ffb6'
        RootKind = 'restore'
    },
    [pscustomobject]@{
        Id = 'runtime.win-x64.microsoft.dotnet.ilcompiler'
        Version = '10.0.8'
        ContentHashSha512 = 'S+9omeowlFjRReD0VB/Q4u3DcN7+vueJSF37saFj0U2OSJIkfI7gGKQRsDxGIf0MwLha14qGrjRC3bDGhvF0Gg=='
        FileCount = 13
        TotalBytes = 37461393L
        AggregateSha256 = 'c5aff04c7a4abcca691fc0e6a8539628cded3250a7777d6c407dc64c07ca858e'
        RootKind = 'restore'
    },
    [pscustomobject]@{
        Id = 'runtime.win-x64.microsoft.dotnet.ilcompiler'
        Version = '10.0.10'
        ContentHashSha512 = 'e5P00ObvdmAl49KkvGvqDLE5+EXu/IdXCDsLghgKFXKrNybyxMmH12HuGrsO7LTIGjXI58tyN3Hwxwr/MR0d8A=='
        FileCount = 13
        TotalBytes = 37473738L
        AggregateSha256 = '17a69cfcd4a3a770d27b7315313c85f68b3a956878af4b95699f4011572e63fb'
        RootKind = 'restore-only'
    },
    [pscustomobject]@{
        Id = 'microsoft.netcore.app.runtime.nativeaot.win-x64'
        Version = '10.0.10'
        ContentHashSha512 = 'NHkgRRE/Fbaw/z0eLdC7rNF7ZRoR+2KQGSPh2ZiKgXpuZOz3Cjhv0srLOaeYnvYMDtT+KzjyyZGgChXx1W18wA=='
        FileCount = 406
        TotalBytes = 109133220L
        AggregateSha256 = '389ade21d811efc6fc4d6f0f1aa492ba71f5fabfb39e4780b83c5a65e9a680c4'
        RootKind = 'restore-only'
    },
    [pscustomobject]@{
        Id = 'microsoft.netcore.app.runtime.nativeaot.win-x64'
        Version = '10.0.8'
        ContentHashSha512 = '4OPm4RaS1aAEf0GOcA/IsdD/ml9o6Hw2BaiHy/iceHxExoj+38QFAyNTxcNAz73SDPHXpUp4SL046vBq5RkuEQ=='
        FileCount = 406
        TotalBytes = 109016717L
        AggregateSha256 = '73972c0d17f29fa389de9c4a87051b372fa15afe5cdadf55bef731a3d8144fbc'
        RootKind = 'runtime'
    }
)
$expectedRestoreArchives = @(
    [pscustomobject]@{
        Id = 'microsoft.dotnet.ilcompiler'
        Version = '10.0.8'
        FileName = 'microsoft.dotnet.ilcompiler.10.0.8.nupkg'
        Sha256 = 'ccdc381bbc7672c48bc755311fdf2f5907433933c295198172d2ad6958ed10b4'
    },
    [pscustomobject]@{
        Id = 'microsoft.net.illink.tasks'
        Version = '10.0.10'
        FileName = 'microsoft.net.illink.tasks.10.0.10.nupkg'
        Sha256 = '97a0eca2030901738f070f142aaf02b3b1aead2bc984c08c428847910b4be312'
    },
    [pscustomobject]@{
        Id = 'runtime.win-x64.microsoft.dotnet.ilcompiler'
        Version = '10.0.8'
        FileName = 'runtime.win-x64.microsoft.dotnet.ilcompiler.10.0.8.nupkg'
        Sha256 = '49f15032ef981172b8c25360f97b76d8abab75348f8e16da0a3fbb970c10bec8'
    },
    [pscustomobject]@{
        Id = 'runtime.win-x64.microsoft.dotnet.ilcompiler'
        Version = '10.0.10'
        FileName = 'runtime.win-x64.microsoft.dotnet.ilcompiler.10.0.10.nupkg'
        Sha256 = '0ae1c40ef28915e63c257999d4cd860db9086878c3a6f8e30388e00056ab350a'
    },
    [pscustomobject]@{
        Id = 'microsoft.netcore.app.runtime.nativeaot.win-x64'
        Version = '10.0.10'
        FileName = 'microsoft.netcore.app.runtime.nativeaot.win-x64.10.0.10.nupkg'
        Sha256 = '5fd7129af9eed4b5a2ed04219b636f7ba0dd965569275d3379fc4d02f623e626'
    }
)
$expectedActivePackageImportEdges = @(
    'sdk|microsoft.dotnet.ilcompiler/10.0.8/build/Microsoft.DotNet.ILCompiler.SingleEntry.targets',
    'microsoft.dotnet.ilcompiler/10.0.8/build/Microsoft.DotNet.ILCompiler.SingleEntry.targets|microsoft.dotnet.ilcompiler/10.0.8/build/Microsoft.NETCore.Native.targets',
    'microsoft.dotnet.ilcompiler/10.0.8/build/Microsoft.NETCore.Native.targets|microsoft.dotnet.ilcompiler/10.0.8/build/Microsoft.NETCore.Native.Windows.targets',
    'microsoft.dotnet.ilcompiler/10.0.8/build/Microsoft.NETCore.Native.targets|microsoft.dotnet.ilcompiler/10.0.8/build/Microsoft.NETCore.Native.Publish.targets',
    'sdk|microsoft.net.illink.tasks/10.0.10/build/Microsoft.NET.ILLink.targets'
)
$expectedPackageTaskBindings = @(
    'ComputeManagedAssembliesToCompileToNative|microsoft.dotnet.ilcompiler/10.0.8/tools/netstandard/ILCompiler.Build.Tasks.dll',
    'ComputeManagedAssemblies|microsoft.net.illink.tasks/10.0.10/tools/net/ILLink.Tasks.dll'
)

function Write-Failure {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Reason,
        [string]$Detail = ""
    )

    $safeDetail = $Detail.Replace("`r", " ").Replace("`n", " ").Trim()
    if ([string]::IsNullOrWhiteSpace($safeDetail)) {
        [System.Console]::Out.WriteLine(
            "TEST_FAIL $testName $Reason")
    }
    else {
        [System.Console]::Out.WriteLine(
            "TEST_FAIL $testName $Reason $safeDetail")
    }

    exit 1
}

function Get-AbsolutePath {
    param(
        [AllowNull()]
        [AllowEmptyString()]
        [string]$Path,
        [string]$Reason = "path_invalid"
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        Write-Failure $Reason "value=empty"
    }

    try {
        if ([System.IO.Path]::IsPathRooted($Path)) {
            return [System.IO.Path]::GetFullPath($Path)
        }

        return [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $Path))
    }
    catch {
        Write-Failure $Reason "type=$($_.Exception.GetType().Name)"
    }
}

function Resolve-PinnedRepositoryContractPath {
    param(
        [AllowNull()]
        [AllowEmptyString()]
        [string]$SuppliedPath,
        [Parameter(Mandatory = $true)]
        [string]$ExpectedRelativePath,
        [Parameter(Mandatory = $true)]
        [string]$Reason
    )

    if ([string]::IsNullOrWhiteSpace($SuppliedPath)) {
        Write-Failure $Reason "value=empty"
    }

    $normalizedSuppliedPath = $SuppliedPath.Replace(
        [System.IO.Path]::AltDirectorySeparatorChar,
        [System.IO.Path]::DirectorySeparatorChar)
    $pathSegments = $normalizedSuppliedPath.Split(
        [System.IO.Path]::DirectorySeparatorChar)
    foreach ($pathSegment in $pathSegments) {
        if ($pathSegment -eq "." -or $pathSegment -eq "..") {
            Write-Failure $Reason "path=$SuppliedPath"
        }
    }

    $expectedFullPath = [System.IO.Path]::GetFullPath(
        (Join-Path $repositoryRoot $ExpectedRelativePath))
    $suppliedFullPath = Get-AbsolutePath $SuppliedPath $Reason
    if (-not [System.IO.Path]::IsPathRooted($SuppliedPath)) {
        $normalizedRelativePath = $normalizedSuppliedPath.Replace(
            [System.IO.Path]::DirectorySeparatorChar,
            "/")
        $normalizedExpectedRelativePath = $ExpectedRelativePath.Replace(
            [System.IO.Path]::DirectorySeparatorChar,
            "/")
        if (-not [string]::Equals(
                $normalizedRelativePath,
                $normalizedExpectedRelativePath,
                [System.StringComparison]::OrdinalIgnoreCase)) {
            Write-Failure $Reason "path=$SuppliedPath"
        }
    }
    if (-not [string]::Equals(
            $suppliedFullPath,
            $expectedFullPath,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        Write-Failure $Reason (
            "expected=$expectedFullPath actual=$suppliedFullPath")
    }

    return $expectedFullPath
}

function Assert-ByteRange {
    param(
        [Parameter(Mandatory = $true)]
        [long]$Offset,
        [Parameter(Mandatory = $true)]
        [long]$Count,
        [Parameter(Mandatory = $true)]
        [long]$Length,
        [Parameter(Mandatory = $true)]
        [string]$Reason
    )

    if ($Offset -lt 0 -or $Count -lt 0 -or $Offset -gt $Length -or $Count -gt ($Length - $Offset)) {
        Write-Failure $Reason
    }
}

function Get-RvaRangeEnd {
    param(
        [Parameter(Mandatory = $true)]
        [uint64]$Rva,
        [Parameter(Mandatory = $true)]
        [uint64]$Size,
        [Parameter(Mandatory = $true)]
        [string]$Reason
    )

    $addressSpaceEnd = [uint64]4294967296
    if ($Size -eq 0 -or $Rva -ge $addressSpaceEnd -or $Size -gt ($addressSpaceEnd - $Rva)) {
        Write-Failure $Reason
    }

    return [uint64]($Rva + $Size)
}

function Read-UInt16LittleEndian {
    param(
        [Parameter(Mandatory = $true)]
        [byte[]]$Bytes,
        [Parameter(Mandatory = $true)]
        [long]$Offset,
        [Parameter(Mandatory = $true)]
        [string]$Reason
    )

    Assert-ByteRange $Offset 2 $Bytes.LongLength $Reason
    return [System.BitConverter]::ToUInt16($Bytes, [int]$Offset)
}

function Read-UInt32LittleEndian {
    param(
        [Parameter(Mandatory = $true)]
        [byte[]]$Bytes,
        [Parameter(Mandatory = $true)]
        [long]$Offset,
        [Parameter(Mandatory = $true)]
        [string]$Reason
    )

    Assert-ByteRange $Offset 4 $Bytes.LongLength $Reason
    return [System.BitConverter]::ToUInt32($Bytes, [int]$Offset)
}

function Read-UInt64LittleEndian {
    param(
        [Parameter(Mandatory = $true)]
        [byte[]]$Bytes,
        [Parameter(Mandatory = $true)]
        [long]$Offset,
        [Parameter(Mandatory = $true)]
        [string]$Reason
    )

    Assert-ByteRange $Offset 8 $Bytes.LongLength $Reason
    return [System.BitConverter]::ToUInt64($Bytes, [int]$Offset)
}

function Get-Sha256 {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-StreamSha256 {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.FileStream]$Stream
    )

    $originalPosition = $Stream.Position
    $algorithm = [System.Security.Cryptography.SHA256]::Create()
    try {
        $Stream.Position = 0
        return [System.BitConverter]::ToString(
            $algorithm.ComputeHash($Stream)
        ).Replace("-", "").ToLowerInvariant()
    }
    finally {
        $Stream.Position = $originalPosition
        $algorithm.Dispose()
    }
}

function Initialize-NativeFileIdentity {
    if ($null -ne ("CLROS.Verifier.NativeFile" -as [type])) {
        return
    }

    try {
        Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace CLROS.Verifier
{
    public sealed class FileIdentity
    {
        public string Key { get; private set; }
        public uint LinkCount { get; private set; }
        public ulong Length { get; private set; }

        internal FileIdentity(string key, uint linkCount, ulong length)
        {
            Key = key;
            LinkCount = linkCount;
            Length = length;
        }
    }

    public static class NativeFile
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

        public static FileIdentity GetIdentity(SafeFileHandle handle)
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
            return new FileIdentity(key, information.NumberOfLinks, length);
        }
    }
}
'@
    }
    catch {
        Write-Failure "file_identity_interop_unavailable" (
            "type=$($_.Exception.GetType().Name)")
    }
}

function Get-AuthorityRootPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $repositoryFullPath = [System.IO.Path]::GetFullPath(
        $repositoryRoot).TrimEnd(
            [System.IO.Path]::DirectorySeparatorChar,
            [System.IO.Path]::AltDirectorySeparatorChar)
    $repositoryPrefix = $repositoryFullPath +
        [System.IO.Path]::DirectorySeparatorChar
    if ([string]::Equals(
            $fullPath,
            $repositoryFullPath,
            [System.StringComparison]::OrdinalIgnoreCase) -or
        $fullPath.StartsWith(
            $repositoryPrefix,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        return $repositoryFullPath
    }

    return [System.IO.Path]::GetPathRoot($fullPath)
}

function Open-AuthorityReadHandle {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$RootPath,
        [Parameter(Mandatory = $true)]
        [string]$Purpose,
        [Parameter(Mandatory = $true)]
        [string]$FailurePrefix,
        [long]$MaximumBytes = [long]::MaxValue,
        [switch]$AllowEmpty,
        [switch]$RequireSingleLink
    )

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    Assert-NoReparsePoints `
        -RootPath $RootPath `
        -Path $fullPath `
        -Reason ($FailurePrefix + "_reparse_point")
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        Write-Failure ($FailurePrefix + "_missing") "path=$fullPath"
    }

    if ($script:authorityReadHandlesByPath.ContainsKey($fullPath)) {
        $existing = $script:authorityReadHandlesByPath[$fullPath]
        Assert-NoReparsePoints `
            -RootPath $RootPath `
            -Path $fullPath `
            -Reason ($FailurePrefix + "_reparse_point")
        if ($RequireSingleLink) {
            $existing.RequireSingleLink = $true
            if ([uint32]$existing.InitialLinkCount -ne 1) {
                Write-Failure "authority_output_hardlink_invalid" (
                    "purpose=$Purpose path=$fullPath links=$($existing.InitialLinkCount)")
            }
        }
        return $existing
    }

    Initialize-NativeFileIdentity
    $stream = $null
    try {
        $stream = New-Object System.IO.FileStream(
            $fullPath,
            [System.IO.FileMode]::Open,
            [System.IO.FileAccess]::Read,
            [System.IO.FileShare]::Read,
            65536,
            [System.IO.FileOptions]::SequentialScan)
        Assert-NoReparsePoints `
            -RootPath $RootPath `
            -Path $fullPath `
            -Reason ($FailurePrefix + "_reparse_point")
        $identity = [CLROS.Verifier.NativeFile]::GetIdentity(
            $stream.SafeFileHandle)
        $length = $stream.Length
        if ($length -lt 0 -or
            $length -gt $MaximumBytes -or
            (-not $AllowEmpty -and $length -eq 0) -or
            [uint64]$length -ne [uint64]$identity.Length) {
            Write-Failure ($FailurePrefix + "_size_out_of_range") (
                "purpose=$Purpose path=$fullPath bytes=$length maximum=$MaximumBytes")
        }
        if ($RequireSingleLink -and [uint32]$identity.LinkCount -ne 1) {
            Write-Failure "authority_output_hardlink_invalid" (
                "purpose=$Purpose path=$fullPath links=$($identity.LinkCount)")
        }
        $initialSha256 = Get-StreamSha256 -Stream $stream
        $record = [pscustomobject]@{
            Path = $fullPath
            RootPath = [System.IO.Path]::GetFullPath($RootPath)
            Purpose = $Purpose
            FailurePrefix = $FailurePrefix
            Stream = $stream
            InitialLength = [long]$length
            InitialSha256 = $initialSha256
            InitialIdentityKey = [string]$identity.Key
            InitialLinkCount = [uint32]$identity.LinkCount
            RequireSingleLink = [bool]$RequireSingleLink
        }
        $script:authorityReadHandles.Add($record)
        $script:authorityReadHandlesByPath.Add($fullPath, $record)
        $stream = $null
        return $record
    }
    catch {
        if ($null -ne $stream) {
            $stream.Dispose()
        }
        if ($_.Exception -is [System.Management.Automation.RuntimeException] -and
            $_.Exception.ErrorRecord.FullyQualifiedErrorId -eq
                "ScriptHalted") {
            throw
        }
        Write-Failure ($FailurePrefix + "_open_failed") (
            "purpose=$Purpose path=$fullPath type=$($_.Exception.GetType().Name)")
    }
}

function Read-AuthorityBytes {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Record,
        [Parameter(Mandatory = $true)]
        [long]$MaximumBytes,
        [Parameter(Mandatory = $true)]
        [string]$Reason
    )

    $length = [long]$Record.Stream.Length
    if ($length -lt 0 -or
        $length -gt $MaximumBytes -or
        $length -gt [int]::MaxValue) {
        Write-Failure $Reason (
            "path=$($Record.Path) bytes=$length maximum=$MaximumBytes")
    }
    [byte[]]$result = New-Object byte[] ([int]$length)
    $originalPosition = $Record.Stream.Position
    try {
        $Record.Stream.Position = 0
        $offset = 0
        while ($offset -lt $result.Length) {
            $read = $Record.Stream.Read(
                $result,
                $offset,
                $result.Length - $offset)
            if ($read -le 0) {
                Write-Failure $Reason "path=$($Record.Path) prematureEof=true"
            }
            $offset += $read
        }
        return ,$result
    }
    finally {
        $Record.Stream.Position = $originalPosition
    }
}

function Read-AuthorityUtf8Text {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Record,
        [Parameter(Mandatory = $true)]
        [long]$MaximumBytes,
        [Parameter(Mandatory = $true)]
        [string]$Reason
    )

    [byte[]]$content = Read-AuthorityBytes `
        -Record $Record `
        -MaximumBytes $MaximumBytes `
        -Reason $Reason
    $offset = 0
    if ($content.Length -ge 3 -and
        $content[0] -eq 0xef -and
        $content[1] -eq 0xbb -and
        $content[2] -eq 0xbf) {
        $offset = 3
    }
    try {
        $strictUtf8 = New-Object System.Text.UTF8Encoding($false, $true)
        return $strictUtf8.GetString(
            $content,
            $offset,
            $content.Length - $offset)
    }
    catch {
        Write-Failure $Reason (
            "path=$($Record.Path) encoding=utf8 type=$($_.Exception.GetType().Name)")
    }
}

function Assert-DistinctAuthorityFiles {
    param(
        [Parameter(Mandatory = $true)]
        [object[]]$Records
    )

    $identityPaths =
        New-Object 'System.Collections.Generic.Dictionary[string,string]' (
            [System.StringComparer]::Ordinal)
    foreach ($record in $Records) {
        if ([uint32]$record.InitialLinkCount -ne 1) {
            Write-Failure "authority_output_hardlink_invalid" (
                "purpose=$($record.Purpose) path=$($record.Path) " +
                "links=$($record.InitialLinkCount)")
        }
        if ($identityPaths.ContainsKey([string]$record.InitialIdentityKey)) {
            Write-Failure "authority_output_file_alias" (
                "first=$($identityPaths[[string]$record.InitialIdentityKey]) " +
                "second=$($record.Path)")
        }
        $identityPaths.Add(
            [string]$record.InitialIdentityKey,
            [string]$record.Path)
    }
}

function Assert-AuthorityInputsUnchanged {
    foreach ($record in $script:authorityReadHandles) {
        Assert-NoReparsePoints `
            -RootPath $record.RootPath `
            -Path $record.Path `
            -Reason ($record.FailurePrefix + "_reparse_point")
        try {
            $identity = [CLROS.Verifier.NativeFile]::GetIdentity(
                $record.Stream.SafeFileHandle)
            $finalLength = [long]$record.Stream.Length
            $finalSha256 = Get-StreamSha256 -Stream $record.Stream
            $probe = New-Object System.IO.FileStream(
                $record.Path,
                [System.IO.FileMode]::Open,
                [System.IO.FileAccess]::Read,
                [System.IO.FileShare]::Read,
                4096,
                [System.IO.FileOptions]::SequentialScan)
            try {
                $probeIdentity = [CLROS.Verifier.NativeFile]::GetIdentity(
                    $probe.SafeFileHandle)
            }
            finally {
                $probe.Dispose()
            }
        }
        catch {
            Write-Failure "authority_input_changed" (
                "purpose=$($record.Purpose) path=$($record.Path) " +
                "type=$($_.Exception.GetType().Name)")
        }
        if ($finalLength -ne [long]$record.InitialLength -or
            $finalSha256 -cne [string]$record.InitialSha256 -or
            [string]$identity.Key -cne [string]$record.InitialIdentityKey -or
            [string]$probeIdentity.Key -cne
                [string]$record.InitialIdentityKey -or
            [uint32]$identity.LinkCount -ne
                [uint32]$record.InitialLinkCount -or
            ($record.RequireSingleLink -and
                [uint32]$identity.LinkCount -ne 1)) {
            Write-Failure "authority_input_changed" (
                "purpose=$($record.Purpose) path=$($record.Path)")
        }
    }
}

function Close-AuthorityReadHandles {
    foreach ($record in $script:authorityReadHandles) {
        try {
            $record.Stream.Dispose()
        }
        catch {
            # Process exit is the final authority-handle cleanup boundary.
        }
    }
    $script:authorityReadHandles.Clear()
    $script:authorityReadHandlesByPath.Clear()
}

function Get-BytesSha256 {
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [byte[]]$Bytes
    )

    $algorithm = [System.Security.Cryptography.SHA256]::Create()
    try {
        return [System.BitConverter]::ToString(
            $algorithm.ComputeHash($Bytes)
        ).Replace("-", "").ToLowerInvariant()
    }
    finally {
        $algorithm.Dispose()
    }
}

function Get-Utf8Sha256 {
    param(
        [AllowEmptyString()]
        [string]$Text
    )

    $utf8 = New-Object System.Text.UTF8Encoding($false)
    return Get-BytesSha256 -Bytes $utf8.GetBytes($Text)
}

function Test-ExactPropertyNameSet {
    param(
        [AllowNull()]
        [object]$Value,
        [Parameter(Mandatory = $true)]
        [string[]]$ExpectedNames
    )

    if ($null -eq $Value) {
        return $false
    }

    [string[]]$actualNames = @(
        $Value.PSObject.Properties |
            ForEach-Object { [string]$_.Name }
    )
    [string[]]$expected = @($ExpectedNames)
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

function Assert-ExactPropertyNameSet {
    param(
        [AllowNull()]
        [object]$Value,
        [Parameter(Mandatory = $true)]
        [string[]]$ExpectedNames,
        [Parameter(Mandatory = $true)]
        [string]$Reason,
        [string]$Detail = ""
    )

    if (-not (Test-ExactPropertyNameSet -Value $Value -ExpectedNames $ExpectedNames)) {
        Write-Failure $Reason $Detail
    }
}

function Read-SecureXmlDocument {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$Reason
    )

    $xmlAuthority = Open-AuthorityReadHandle `
        -Path $Path `
        -RootPath (Get-AuthorityRootPath -Path $Path) `
        -Purpose ("xml:" + $Reason) `
        -FailurePrefix $Reason `
        -MaximumBytes $maximumAuthorityTextBytes
    $reader = $null
    try {
        $settings = New-Object System.Xml.XmlReaderSettings
        $settings.DtdProcessing = [System.Xml.DtdProcessing]::Prohibit
        $settings.XmlResolver = $null
        $settings.MaxCharactersInDocument = 16777216
        $reader = [System.Xml.XmlReader]::Create($Path, $settings)
        $document = New-Object System.Xml.XmlDocument
        $document.XmlResolver = $null
        $document.Load($reader)
        return $document
    }
    catch {
        Write-Failure $Reason "type=$($_.Exception.GetType().Name)"
    }
    finally {
        if ($null -ne $reader) {
            $reader.Dispose()
        }
    }
}

function Assert-NoReparsePoints {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RootPath,
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$Reason
    )

    try {
        $rootCandidate = [System.IO.Path]::GetFullPath($RootPath)
        $rootVolume = [System.IO.Path]::GetPathRoot($rootCandidate)
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
    }
    catch {
        Write-Failure $Reason "type=$($_.Exception.GetType().Name)"
    }

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
        Write-Failure $Reason "path=$pathFullPath"
    }

    $volumeRoot = [System.IO.Path]::GetPathRoot($pathFullPath)
    if ([string]::IsNullOrWhiteSpace($volumeRoot)) {
        Write-Failure $Reason "path=$pathFullPath root=missing"
    }
    $currentPath = $volumeRoot
    $relativePath = $pathFullPath.Substring($volumeRoot.Length)
    foreach ($segment in $relativePath.Split(
            @(
                [System.IO.Path]::DirectorySeparatorChar,
                [System.IO.Path]::AltDirectorySeparatorChar
            ),
            [System.StringSplitOptions]::RemoveEmptyEntries)) {
        $currentPath = Join-Path $currentPath $segment
        try {
            $item = Get-Item `
                -LiteralPath $currentPath `
                -Force `
                -ErrorAction Stop
        }
        catch {
            Write-Failure $Reason (
                "path=$currentPath type=$($_.Exception.GetType().Name)")
        }
        if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            Write-Failure $Reason "path=$currentPath"
        }
    }
}

function Get-EvidenceInteger {
    param(
        [AllowNull()]
        [object]$Value,
        [Parameter(Mandatory = $true)]
        [string]$Property,
        [long]$Minimum = 0
    )

    if ($Value -isnot [int] -and $Value -isnot [long]) {
        Write-Failure "link_evidence_number_malformed" "property=$Property"
    }
    $result = Convert-EvidenceInt64 -Value $Value -Property $Property
    if ($result -lt $Minimum) {
        Write-Failure "link_evidence_number_out_of_range" "property=$Property value=$result"
    }
    return $result
}

function Convert-HexUInt64 {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Value,
        [Parameter(Mandatory = $true)]
        [string]$Reason
    )

    try {
        return [System.Convert]::ToUInt64($Value, 16)
    }
    catch {
        Write-Failure $Reason "value=$Value"
    }
}

function Convert-EvidenceInt64 {
    param(
        [AllowNull()]
        [object]$Value,
        [Parameter(Mandatory = $true)]
        [string]$Property
    )

    if ($null -eq $Value) {
        Write-Failure "link_evidence_number_malformed" "property=$Property"
    }

    try {
        return [System.Convert]::ToInt64(
            $Value,
            [System.Globalization.CultureInfo]::InvariantCulture)
    }
    catch {
        Write-Failure "link_evidence_number_malformed" "property=$Property"
    }
}

function Read-DataDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [byte[]]$Bytes,
        [Parameter(Mandatory = $true)]
        [long]$OptionalHeaderOffset,
        [Parameter(Mandatory = $true)]
        [long]$OptionalHeaderSize,
        [Parameter(Mandatory = $true)]
        [uint32]$DirectoryCount,
        [Parameter(Mandatory = $true)]
        [int]$Index,
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    if ($DirectoryCount -le $Index) {
        return [pscustomobject]@{
            Name = $Name
            Rva = [uint32]0
            Size = [uint32]0
        }
    }

    $directoryOffset = $OptionalHeaderOffset + 112L + ([long]$Index * 8L)
    $optionalHeaderEnd = $OptionalHeaderOffset + $OptionalHeaderSize
    if ($directoryOffset -lt $OptionalHeaderOffset -or ($directoryOffset + 8L) -gt $optionalHeaderEnd) {
        Write-Failure "invalid_data_directory" "directory=$Name"
    }

    return [pscustomobject]@{
        Name = $Name
        Rva = Read-UInt32LittleEndian $Bytes $directoryOffset "truncated_data_directory"
        Size = Read-UInt32LittleEndian $Bytes ($directoryOffset + 4L) "truncated_data_directory"
    }
}

function Resolve-FileBackedRvaRange {
    param(
        [Parameter(Mandatory = $true)]
        [object[]]$Sections,
        [Parameter(Mandatory = $true)]
        [uint64]$Rva,
        [Parameter(Mandatory = $true)]
        [uint64]$Size,
        [Parameter(Mandatory = $true)]
        [long]$FileLength,
        [Parameter(Mandatory = $true)]
        [string]$MissingReason,
        [Parameter(Mandatory = $true)]
        [string]$AmbiguousReason
    )

    $rangeEnd = Get-RvaRangeEnd $Rva $Size $MissingReason
    $matches = @()

    foreach ($section in $Sections) {
        if ([uint64]$section.VirtualSize -eq 0 -or
            [uint64]$section.RawSize -eq 0) {
            continue
        }

        $sectionStart = [uint64]$section.VirtualAddress
        $sectionVirtualEnd = $sectionStart + [uint64]$section.VirtualSize
        $sectionRawEnd = $sectionStart + [uint64]$section.RawSize
        if ($sectionVirtualEnd -gt [uint64]4294967296 -or
            $sectionRawEnd -gt [uint64]4294967296) {
            continue
        }

        if ($Rva -lt $sectionStart -or
            $rangeEnd -gt $sectionVirtualEnd -or
            $rangeEnd -gt $sectionRawEnd) {
            continue
        }

        $delta = $Rva - $sectionStart
        $fileOffset = [uint64]$section.RawOffset + $delta
        if ($fileOffset -gt [uint64]$FileLength -or $Size -gt ([uint64]$FileLength - $fileOffset)) {
            continue
        }

        $matches += [pscustomobject]@{
            Section = $section
            FileOffset = $fileOffset
            Size = $Size
        }
    }

    if ($matches.Count -eq 0) {
        Write-Failure $MissingReason (
            "rva=0x{0:x8} size={1}" -f $Rva, $Size)
    }

    if ($matches.Count -ne 1) {
        Write-Failure $AmbiguousReason (
            "rva=0x{0:x8} size={1} matches={2}" -f $Rva, $Size, $matches.Count)
    }

    return $matches[0]
}

function Assert-CodeSectionCharacteristics {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Section,
        [Parameter(Mandatory = $true)]
        [string]$Reason
    )

    $characteristics = [uint64]$Section.Characteristics
    $containsCode = ($characteristics -band [uint64]0x00000020) -ne 0
    $isDiscardable = ($characteristics -band [uint64]0x02000000) -ne 0
    $isExecutable = ($characteristics -band [uint64]0x20000000) -ne 0
    $isReadable = ($characteristics -band [uint64]0x40000000) -ne 0
    $isWritable = ($characteristics -band [uint64]2147483648) -ne 0
    if (-not $containsCode -or
        -not $isReadable -or
        -not $isExecutable -or
        $isWritable -or
        $isDiscardable) {
        Write-Failure $Reason (
            "section={0} characteristics=0x{1:x8}" -f
            $Section.Name,
            $Section.Characteristics)
    }
}

function Assert-ReadOnlyDataSectionCharacteristics {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Section,
        [Parameter(Mandatory = $true)]
        [string]$Reason
    )

    $characteristics = [uint64]$Section.Characteristics
    $containsCode = ($characteristics -band [uint64]0x00000020) -ne 0
    $containsInitializedData = ($characteristics -band [uint64]0x00000040) -ne 0
    $isDiscardable = ($characteristics -band [uint64]0x02000000) -ne 0
    $isExecutable = ($characteristics -band [uint64]0x20000000) -ne 0
    $isReadable = ($characteristics -band [uint64]0x40000000) -ne 0
    $isWritable = ($characteristics -band [uint64]2147483648) -ne 0
    if ($containsCode -or
        -not $containsInitializedData -or
        -not $isReadable -or
        $isExecutable -or
        $isWritable -or
        $isDiscardable) {
        Write-Failure $Reason (
            "section={0} characteristics=0x{1:x8}" -f
            $Section.Name,
            $Section.Characteristics)
    }
}

function Test-ByteSequenceEqual {
    param(
        [Parameter(Mandatory = $true)]
        [byte[]]$Left,
        [Parameter(Mandatory = $true)]
        [byte[]]$Right
    )

    if ($Left.Length -ne $Right.Length) {
        return $false
    }

    for ($index = 0; $index -lt $Left.Length; $index++) {
        if ($Left[$index] -ne $Right[$index]) {
            return $false
        }
    }

    return $true
}

function Copy-ByteRange {
    param(
        [Parameter(Mandatory = $true)]
        [byte[]]$Bytes,
        [Parameter(Mandatory = $true)]
        [uint64]$Offset,
        [Parameter(Mandatory = $true)]
        [int]$Count,
        [Parameter(Mandatory = $true)]
        [string]$Reason
    )

    if ($Offset -gt [uint64][int]::MaxValue) {
        Write-Failure $Reason
    }

    Assert-ByteRange ([long]$Offset) $Count $Bytes.LongLength $Reason
    [byte[]]$result = New-Object byte[] $Count
    [System.Array]::Copy($Bytes, [int]$Offset, $result, 0, $Count)
    return $result
}

function Get-LinkResponseTokens {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Text
    )

    $tokens = New-Object 'System.Collections.Generic.List[string]'
    $matches = [System.Text.RegularExpressions.Regex]::Matches(
        $Text,
        '(?:[^\s"]*"(?:[^"]|"")*"[^\s"]*|\S+)')
    foreach ($match in $matches) {
        if ($tokens.Count -ge $maximumLinkResponseTokens) {
            Write-Failure "link_response_token_count_out_of_range" (
                "maximum=$maximumLinkResponseTokens")
        }

        $value = $match.Value
        if ($value.Length -ge 2 -and $value[0] -eq '"' -and $value[$value.Length - 1] -eq '"') {
            $value = $value.Substring(1, $value.Length - 2).Replace('""', '"')
        }

        $tokens.Add($value)
    }

    return $tokens.ToArray()
}

function Convert-ToRepositoryRelativePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FullPath,
        [Parameter(Mandatory = $true)]
        [string]$Reason
    )

    try {
        $normalizedPath = [System.IO.Path]::GetFullPath($FullPath)
        $normalizedRoot = [System.IO.Path]::GetFullPath($repositoryRoot).TrimEnd(
            [System.IO.Path]::DirectorySeparatorChar,
            [System.IO.Path]::AltDirectorySeparatorChar)
        $rootPrefix = $normalizedRoot + [System.IO.Path]::DirectorySeparatorChar
        if (-not $normalizedPath.StartsWith(
                $rootPrefix,
                [System.StringComparison]::OrdinalIgnoreCase)) {
            Write-Failure $Reason "path=$normalizedPath"
        }

        return $normalizedPath.Substring($rootPrefix.Length).Replace("\", "/")
    }
    catch {
        Write-Failure $Reason "type=$($_.Exception.GetType().Name)"
    }
}

function Resolve-CanonicalSourceClosurePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RelativePath,
        [string]$FailurePrefix = "source_closure"
    )

    if ([string]::IsNullOrWhiteSpace($RelativePath) -or
        $RelativePath.Length -gt 512 -or
        $RelativePath.Contains("\") -or
        $RelativePath.Contains(":") -or
        [System.IO.Path]::IsPathRooted($RelativePath)) {
        Write-Failure ($FailurePrefix + "_path_invalid") "path=$RelativePath"
    }

    $segments = $RelativePath.Split("/")
    if ($segments.Count -eq 0) {
        Write-Failure ($FailurePrefix + "_path_invalid") "path=$RelativePath"
    }
    foreach ($segment in $segments) {
        if ([string]::IsNullOrEmpty($segment) -or
            $segment -eq "." -or
            $segment -eq "..") {
            Write-Failure ($FailurePrefix + "_path_invalid") "path=$RelativePath"
        }
    }

    try {
        $fullPath = [System.IO.Path]::GetFullPath(
            (Join-Path $repositoryRoot $RelativePath.Replace(
                "/",
                [System.IO.Path]::DirectorySeparatorChar)))
    }
    catch {
        Write-Failure ($FailurePrefix + "_path_invalid") "path=$RelativePath"
    }

    $canonicalRelativePath = Convert-ToRepositoryRelativePath `
        $fullPath `
        ($FailurePrefix + "_path_outside_repository")
    if ($canonicalRelativePath -cne $RelativePath) {
        Write-Failure ($FailurePrefix + "_path_not_canonical") "path=$RelativePath"
    }

    return $fullPath
}

function Get-ExpectedSourceClosurePaths {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectFullPath
    )

    $xmlReader = $null
    try {
        $xmlSettings = New-Object System.Xml.XmlReaderSettings
        $xmlSettings.DtdProcessing = [System.Xml.DtdProcessing]::Prohibit
        $xmlSettings.XmlResolver = $null
        $xmlSettings.MaxCharactersInDocument = 4194304
        $xmlReader = [System.Xml.XmlReader]::Create(
            $ProjectFullPath,
            $xmlSettings)
        $projectDocument = New-Object System.Xml.XmlDocument
        $projectDocument.XmlResolver = $null
        $projectDocument.Load($xmlReader)
    }
    catch {
        Write-Failure "source_closure_project_parse_failed" (
            "type=$($_.Exception.GetType().Name)")
    }
    finally {
        if ($null -ne $xmlReader) {
            $xmlReader.Dispose()
        }
    }

    $expectedPathSet = New-Object 'System.Collections.Generic.HashSet[string]' (
        [System.StringComparer]::OrdinalIgnoreCase)
    $expectedPaths = New-Object 'System.Collections.Generic.List[string]'
    $projectDirectory = Split-Path -Parent $ProjectFullPath
    $compileNodes = @(
        $projectDocument.SelectNodes("//*[local-name()='Compile']")
    )
    if ($compileNodes.Count -eq 0) {
        Write-Failure "source_closure_compile_inputs_missing"
    }

    foreach ($compileNode in $compileNodes) {
        $includeAttribute = $compileNode.Attributes["Include"]
        if ($null -eq $includeAttribute -or
            [string]::IsNullOrWhiteSpace($includeAttribute.Value) -or
            $includeAttribute.Value -match '[$*?\[\]]') {
            Write-Failure "source_closure_compile_input_invalid"
        }

        try {
            $compileFullPath = [System.IO.Path]::GetFullPath(
                (Join-Path $projectDirectory $includeAttribute.Value))
        }
        catch {
            Write-Failure "source_closure_compile_input_invalid"
        }
        $relativeCompilePath = Convert-ToRepositoryRelativePath `
            $compileFullPath `
            "source_closure_compile_input_outside_repository"
        if (-not $expectedPathSet.Add($relativeCompilePath)) {
            Write-Failure "source_closure_expected_path_duplicate" (
                "path=$relativeCompilePath")
        }
        $expectedPaths.Add($relativeCompilePath)
    }

    $explicitPaths = @(
        "CLROS.slnx",
        "global.json",
        "src/OS/OS.csproj",
        "Directory.Build.props",
        "Directory.Build.targets",
        "scripts/build-efi-shell.ps1",
        "scripts/test-efi-artifact.ps1",
        "tools/Directory.Build.props",
        "tools/OS.X64TextSerializerGenerator/OS.X64TextSerializerGenerator.targets",
        "tools/OS.X64TextSerializerGenerator/OS.X64TextSerializerGenerator.csproj",
        "tools/OS.X64TextSerializerGenerator/Program.cs",
        "tools/OS.X64TextSerializerGenerator/Amd64CoffWriter.cs",
        "tools/OS.X64TextSerializerGenerator/InstructionPublicationSerializerGenerator.cs"
    )
    foreach ($explicitPath in $explicitPaths) {
        $explicitFullPath = Get-AbsolutePath `
            $explicitPath `
            "source_closure_explicit_path_invalid"
        $relativeExplicitPath = Convert-ToRepositoryRelativePath `
            $explicitFullPath `
            "source_closure_explicit_path_outside_repository"
        if ($expectedPathSet.Add($relativeExplicitPath)) {
            $expectedPaths.Add($relativeExplicitPath)
        }
    }

    $expectedPaths.Sort([System.StringComparer]::Ordinal)
    if ($expectedPaths.Count -ne $expectedSourceClosureInputCount) {
        Write-Failure "source_closure_expected_input_count_invalid" (
            "expected=$expectedSourceClosureInputCount actual=$($expectedPaths.Count)")
    }
    return $expectedPaths.ToArray()
}

function Get-SourceClosureAggregateSha256 {
    param(
        [Parameter(Mandatory = $true)]
        [object[]]$Inputs
    )

    $builder = New-Object System.Text.StringBuilder
    foreach ($inputEntry in $Inputs) {
        [void]$builder.Append([string]$inputEntry.Path)
        [void]$builder.Append("|")
        [void]$builder.Append([string]$inputEntry.Sha256)
        [void]$builder.Append("`n")
    }

    $utf8 = New-Object System.Text.UTF8Encoding($false)
    [byte[]]$aggregateBytes = $utf8.GetBytes($builder.ToString())
    return Get-BytesSha256 $aggregateBytes
}

function Assert-ManagedPeImage {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $managedLength = (Get-Item -LiteralPath $Path).Length
    if ($managedLength -eq 0 -or
        $managedLength -gt $maximumManagedAssemblyBytes) {
        Write-Failure "managed_assembly_size_out_of_range" (
            "bytes=$managedLength maximum=$maximumManagedAssemblyBytes")
    }

    try {
        [byte[]]$managedBytes = [System.IO.File]::ReadAllBytes($Path)
    }
    catch {
        Write-Failure "managed_assembly_read_failed" (
            "type=$($_.Exception.GetType().Name)")
    }

    Assert-ByteRange 0 64 $managedBytes.LongLength "managed_pe_dos_header_truncated"
    if ($managedBytes[0] -ne 0x4d -or $managedBytes[1] -ne 0x5a) {
        Write-Failure "managed_pe_dos_signature_invalid"
    }

    $managedPeOffset = [long](
        Read-UInt32LittleEndian `
            $managedBytes `
            0x3c `
            "managed_pe_dos_header_truncated")
    if ($managedPeOffset -lt 64) {
        Write-Failure "managed_pe_offset_invalid"
    }
    Assert-ByteRange `
        $managedPeOffset `
        24 `
        $managedBytes.LongLength `
        "managed_pe_header_truncated"
    if ($managedBytes[[int]$managedPeOffset] -ne 0x50 -or
        $managedBytes[[int]$managedPeOffset + 1] -ne 0x45 -or
        $managedBytes[[int]$managedPeOffset + 2] -ne 0 -or
        $managedBytes[[int]$managedPeOffset + 3] -ne 0) {
        Write-Failure "managed_pe_signature_invalid"
    }

    $managedCoffOffset = $managedPeOffset + 4L
    $managedSectionCount = Read-UInt16LittleEndian `
        $managedBytes `
        ($managedCoffOffset + 2L) `
        "managed_pe_coff_header_truncated"
    $managedOptionalSize = Read-UInt16LittleEndian `
        $managedBytes `
        ($managedCoffOffset + 16L) `
        "managed_pe_coff_header_truncated"
    if ($managedSectionCount -eq 0 -or $managedSectionCount -gt 96) {
        Write-Failure "managed_pe_section_count_out_of_range"
    }

    $managedOptionalOffset = $managedCoffOffset + 20L
    Assert-ByteRange `
        $managedOptionalOffset `
        $managedOptionalSize `
        $managedBytes.LongLength `
        "managed_pe_optional_header_truncated"
    $managedMagic = Read-UInt16LittleEndian `
        $managedBytes `
        $managedOptionalOffset `
        "managed_pe_optional_header_truncated"
    if ($managedMagic -eq 0x10b) {
        $managedDirectoryCountOffset = $managedOptionalOffset + 92L
        $managedDirectoryBase = $managedOptionalOffset + 96L
        $managedMinimumOptionalSize = 96L + (15L * 8L)
    }
    elseif ($managedMagic -eq 0x20b) {
        $managedDirectoryCountOffset = $managedOptionalOffset + 108L
        $managedDirectoryBase = $managedOptionalOffset + 112L
        $managedMinimumOptionalSize = 112L + (15L * 8L)
    }
    else {
        Write-Failure "managed_pe_optional_magic_invalid" (
            "magic=0x{0:x4}" -f $managedMagic)
    }
    if ($managedOptionalSize -lt $managedMinimumOptionalSize) {
        Write-Failure "managed_pe_optional_header_too_small"
    }

    $managedDirectoryCount = Read-UInt32LittleEndian `
        $managedBytes `
        $managedDirectoryCountOffset `
        "managed_pe_optional_header_truncated"
    if ($managedDirectoryCount -le 14) {
        Write-Failure "managed_pe_cli_directory_missing"
    }
    $managedCliOffset = $managedDirectoryBase + (14L * 8L)
    $managedCliRva = Read-UInt32LittleEndian `
        $managedBytes `
        $managedCliOffset `
        "managed_pe_cli_directory_truncated"
    $managedCliSize = Read-UInt32LittleEndian `
        $managedBytes `
        ($managedCliOffset + 4L) `
        "managed_pe_cli_directory_truncated"
    if ($managedCliRva -eq 0 -or $managedCliSize -lt 72) {
        Write-Failure "managed_pe_cli_directory_invalid"
    }

    $managedSectionTableOffset =
        $managedOptionalOffset + [long]$managedOptionalSize
    $managedSectionTableSize = [long]$managedSectionCount * 40L
    Assert-ByteRange `
        $managedSectionTableOffset `
        $managedSectionTableSize `
        $managedBytes.LongLength `
        "managed_pe_section_table_truncated"
    $managedSections = @()
    for ($managedIndex = 0;
         $managedIndex -lt $managedSectionCount;
         $managedIndex++) {
        $managedSectionOffset =
            $managedSectionTableOffset + ([long]$managedIndex * 40L)
        $managedSections += [pscustomobject]@{
            Name = "managed-$managedIndex"
            VirtualSize = Read-UInt32LittleEndian `
                $managedBytes `
                ($managedSectionOffset + 8L) `
                "managed_pe_section_table_truncated"
            VirtualAddress = Read-UInt32LittleEndian `
                $managedBytes `
                ($managedSectionOffset + 12L) `
                "managed_pe_section_table_truncated"
            RawSize = Read-UInt32LittleEndian `
                $managedBytes `
                ($managedSectionOffset + 16L) `
                "managed_pe_section_table_truncated"
            RawOffset = Read-UInt32LittleEndian `
                $managedBytes `
                ($managedSectionOffset + 20L) `
                "managed_pe_section_table_truncated"
        }
    }

    $managedCliBacking = Resolve-FileBackedRvaRange `
        $managedSections `
        ([uint64]$managedCliRva) `
        72 `
        $managedBytes.LongLength `
        "managed_pe_cli_directory_not_file_backed" `
        "managed_pe_cli_directory_ambiguous"
    $managedCliFileOffset = [long]$managedCliBacking.FileOffset
    $managedCliHeaderSize = Read-UInt32LittleEndian `
        $managedBytes `
        $managedCliFileOffset `
        "managed_pe_cli_header_truncated"
    if ($managedCliHeaderSize -lt 72) {
        Write-Failure "managed_pe_cli_header_invalid"
    }
    $managedMetadataRva = Read-UInt32LittleEndian `
        $managedBytes `
        ($managedCliFileOffset + 8L) `
        "managed_pe_cli_header_truncated"
    $managedMetadataSize = Read-UInt32LittleEndian `
        $managedBytes `
        ($managedCliFileOffset + 12L) `
        "managed_pe_cli_header_truncated"
    if ($managedMetadataRva -eq 0 -or $managedMetadataSize -lt 4) {
        Write-Failure "managed_pe_metadata_directory_invalid"
    }
    $managedMetadataBacking = Resolve-FileBackedRvaRange `
        $managedSections `
        ([uint64]$managedMetadataRva) `
        4 `
        $managedBytes.LongLength `
        "managed_pe_metadata_not_file_backed" `
        "managed_pe_metadata_ambiguous"
    $managedMetadataOffset = [long]$managedMetadataBacking.FileOffset
    if ($managedBytes[[int]$managedMetadataOffset] -ne 0x42 -or
        $managedBytes[[int]$managedMetadataOffset + 1] -ne 0x53 -or
        $managedBytes[[int]$managedMetadataOffset + 2] -ne 0x4a -or
        $managedBytes[[int]$managedMetadataOffset + 3] -ne 0x42) {
        Write-Failure "managed_pe_metadata_signature_invalid"
    }
}

function Get-VerifiedExactFileSnapshot {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RootPath,
        [Parameter(Mandatory = $true)]
        [string[]]$ExpectedRelativePaths,
        [Parameter(Mandatory = $true)]
        [object[]]$EvidenceInputs,
        [Parameter(Mandatory = $true)]
        [string]$FailurePrefix,
        [hashtable]$ExpectedHashes = @{}
    )

    $rootFullPath = [System.IO.Path]::GetFullPath($RootPath).TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar)
    if (-not (Test-Path -LiteralPath $rootFullPath -PathType Container)) {
        Write-Failure ($FailurePrefix + "_root_missing") "path=$rootFullPath"
    }
    Assert-NoReparsePoints `
        -RootPath $rootFullPath `
        -Path $rootFullPath `
        -Reason ($FailurePrefix + "_reparse_point")

    if ($EvidenceInputs.Count -ne $ExpectedRelativePaths.Count) {
        Write-Failure ($FailurePrefix + "_input_count_mismatch") (
            "expected=$($ExpectedRelativePaths.Count) actual=$($EvidenceInputs.Count)")
    }

    $builder = New-Object System.Text.StringBuilder
    $verifiedInputs = New-Object System.Collections.Generic.List[object]
    $seenPaths = New-Object 'System.Collections.Generic.HashSet[string]' (
        [System.StringComparer]::Ordinal)
    for ($index = 0; $index -lt $ExpectedRelativePaths.Count; $index++) {
        $inputEntry = $EvidenceInputs[$index]
        Assert-ExactPropertyNameSet `
            -Value $inputEntry `
            -ExpectedNames @("path", "sha256") `
            -Reason ($FailurePrefix + "_input_shape_invalid") `
            -Detail "index=$index"
        $relativePath = [string]$inputEntry.path
        $evidenceHash = [string]$inputEntry.sha256
        $expectedRelativePath = [string]$ExpectedRelativePaths[$index]
        if ($relativePath -cne $expectedRelativePath -or
            -not $seenPaths.Add($relativePath)) {
            Write-Failure ($FailurePrefix + "_input_set_mismatch") (
                "index=$index expected=$expectedRelativePath actual=$relativePath")
        }
        if ($relativePath.Length -gt 1024 -or
            [string]::IsNullOrWhiteSpace($relativePath) -or
            [System.IO.Path]::IsPathRooted($relativePath) -or
            $relativePath.Contains("\") -or
            $relativePath.Contains(":")) {
            Write-Failure ($FailurePrefix + "_input_path_invalid") (
                "path=$relativePath")
        }
        foreach ($segment in $relativePath.Split("/")) {
            if ([string]::IsNullOrEmpty($segment) -or
                $segment -eq "." -or
                $segment -eq "..") {
                Write-Failure ($FailurePrefix + "_input_path_invalid") (
                    "path=$relativePath")
            }
        }
        if ($evidenceHash -cnotmatch '^[0-9a-f]{64}$') {
            Write-Failure ($FailurePrefix + "_input_hash_malformed") (
                "path=$relativePath")
        }

        $fullPath = [System.IO.Path]::GetFullPath(
            (Join-Path $rootFullPath $relativePath.Replace(
                "/",
                [System.IO.Path]::DirectorySeparatorChar)))
        $rootPrefix = $rootFullPath + [System.IO.Path]::DirectorySeparatorChar
        if (-not $fullPath.StartsWith(
                $rootPrefix,
                [System.StringComparison]::OrdinalIgnoreCase) -or
            -not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
            Write-Failure ($FailurePrefix + "_input_missing") (
                "path=$relativePath")
        }
        Assert-NoReparsePoints `
            -RootPath $rootFullPath `
            -Path $fullPath `
            -Reason ($FailurePrefix + "_reparse_point")
        $authorityRecord = Open-AuthorityReadHandle `
            -Path $fullPath `
            -RootPath $rootFullPath `
            -Purpose ($FailurePrefix + ":" + $relativePath) `
            -FailurePrefix $FailurePrefix `
            -MaximumBytes $maximumPackageFileBytes `
            -AllowEmpty
        $actualHash = [string]$authorityRecord.InitialSha256
        if ($actualHash -cne $evidenceHash) {
            Write-Failure ($FailurePrefix + "_input_hash_mismatch") (
                "path=$relativePath")
        }
        if ($ExpectedHashes.ContainsKey($relativePath) -and
            $actualHash -cne [string]$ExpectedHashes[$relativePath]) {
            Write-Failure ($FailurePrefix + "_pinned_hash_mismatch") (
                "path=$relativePath")
        }

        [void]$builder.Append($relativePath)
        [void]$builder.Append("|")
        [void]$builder.Append($actualHash)
        [void]$builder.Append("`n")
        $verifiedInputs.Add([pscustomobject]@{
            Path = $relativePath
            Sha256 = $actualHash
            FullPath = $fullPath
            AuthorityRecord = $authorityRecord
        })
    }

    return [pscustomobject]@{
        Inputs = $verifiedInputs.ToArray()
        AggregateSha256 = Get-Utf8Sha256 -Text $builder.ToString()
    }
}

function Assert-PinnedRestoreConfig {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ConfigPath,
        [Parameter(Mandatory = $true)]
        [string]$RestoreSourcePath
    )

    $config = Read-SecureXmlDocument `
        -Path $ConfigPath `
        -Reason "pinned_restore_config_invalid"
    $root = $config.DocumentElement
    if ($null -eq $root -or
        $root.LocalName -cne "configuration" -or
        $root.NamespaceURI -cne "" -or
        $root.Attributes.Count -ne 0) {
        Write-Failure "pinned_restore_config_root_invalid"
    }
    foreach ($element in @($config.SelectNodes("//*"))) {
        if ($element.NamespaceURI -cne "") {
            Write-Failure "pinned_restore_config_namespace_invalid" (
                "element=$($element.LocalName)")
        }
    }
    $children = @(
        $root.ChildNodes |
            Where-Object { $_.NodeType -eq [System.Xml.XmlNodeType]::Element }
    )
    $expectedChildNames = @(
        "packageSources",
        "fallbackPackageFolders",
        "auditSources"
    )
    if ($children.Count -ne $expectedChildNames.Count) {
        Write-Failure "pinned_restore_config_child_count_invalid"
    }
    for ($index = 0; $index -lt $expectedChildNames.Count; $index++) {
        if ($children[$index].LocalName -cne $expectedChildNames[$index] -or
            $children[$index].NamespaceURI -cne "" -or
            $children[$index].Attributes.Count -ne 0) {
            Write-Failure "pinned_restore_config_child_invalid" "index=$index"
        }
    }

    $packageSources = @(
        $children[0].ChildNodes |
            Where-Object { $_.NodeType -eq [System.Xml.XmlNodeType]::Element }
    )
    $configuredSourcePath = ""
    if ($packageSources.Count -eq 2) {
        try {
            $configuredSourcePath = [System.IO.Path]::GetFullPath(
                $packageSources[1].GetAttribute("value"))
        }
        catch {
            Write-Failure "pinned_restore_config_package_source_path_invalid"
        }
    }
    if ($packageSources.Count -ne 2 -or
        $packageSources[0].LocalName -cne "clear" -or
        $packageSources[0].NamespaceURI -cne "" -or
        $packageSources[0].Attributes.Count -ne 0 -or
        $packageSources[1].LocalName -cne "add" -or
        $packageSources[1].NamespaceURI -cne "" -or
        $packageSources[1].Attributes.Count -ne 2 -or
        $packageSources[1].GetAttribute("key") -cne "CLROS_PINNED" -or
        -not [string]::Equals(
            $configuredSourcePath,
            $RestoreSourcePath,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        Write-Failure "pinned_restore_config_package_sources_invalid"
    }
    foreach ($clearGroup in @($children[1], $children[2])) {
        $clearChildren = @(
            $clearGroup.ChildNodes |
                Where-Object { $_.NodeType -eq [System.Xml.XmlNodeType]::Element }
        )
        if ($clearChildren.Count -ne 1 -or
            $clearChildren[0].LocalName -cne "clear" -or
            $clearChildren[0].NamespaceURI -cne "" -or
            $clearChildren[0].Attributes.Count -ne 0) {
            Write-Failure "pinned_restore_config_clear_group_invalid" (
                "group=$($clearGroup.LocalName)")
        }
    }
}

function Get-PackageTreeSnapshot {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PackageRoot,
        [Parameter(Mandatory = $true)]
        [string]$FailurePrefix
    )

    $packageFullPath = [System.IO.Path]::GetFullPath($PackageRoot).TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar)
    if (-not (Test-Path -LiteralPath $packageFullPath -PathType Container)) {
        Write-Failure ($FailurePrefix + "_root_missing") "path=$packageFullPath"
    }
    Assert-NoReparsePoints `
        -RootPath $packageFullPath `
        -Path $packageFullPath `
        -Reason ($FailurePrefix + "_reparse_point")

    $fileByRelativePath = New-Object `
        'System.Collections.Generic.Dictionary[string,object]' (
            [System.StringComparer]::Ordinal)
    $treeEntryCount = 0
    [long]$enumeratedBytes = 0
    Get-ChildItem `
        -LiteralPath $packageFullPath `
        -Recurse `
        -Force |
        ForEach-Object {
            $treeEntryCount++
            if ($treeEntryCount -gt $maximumPackageTreeEntries) {
                Write-Failure ($FailurePrefix + "_entry_count_out_of_range") (
                    "maximum=$maximumPackageTreeEntries")
            }
            if (($_.Attributes -band
                    [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                Write-Failure ($FailurePrefix + "_reparse_point") (
                    "path=$($_.FullName)")
            }
            if (-not $_.PSIsContainer -and
                $_.Name -cne ".nupkg.metadata") {
                if ($fileByRelativePath.Count -ge $maximumPackageTreeFiles) {
                    Write-Failure ($FailurePrefix + "_file_count_out_of_range") (
                        "maximum=$maximumPackageTreeFiles")
                }
                [long]$entryByteLength = $_.Length
                if ($entryByteLength -lt 0 -or
                    $entryByteLength -gt $maximumPackageFileBytes -or
                    $enumeratedBytes -gt
                        ($maximumPackageTreeBytes - $entryByteLength)) {
                    Write-Failure ($FailurePrefix + "_byte_count_out_of_range") (
                        "path=$($_.FullName) bytes=$entryByteLength")
                }
                $enumeratedBytes += $entryByteLength
                $relativePath = $_.FullName.Substring(
                    $packageFullPath.Length + 1).Replace("\", "/")
                if ($fileByRelativePath.ContainsKey($relativePath)) {
                    Write-Failure ($FailurePrefix + "_duplicate_path") (
                        "path=$relativePath")
                }
                $fileByRelativePath.Add($relativePath, $_)
            }
        }
    [string[]]$relativePaths = @($fileByRelativePath.Keys)
    [System.Array]::Sort($relativePaths, [System.StringComparer]::Ordinal)
    $inputs = New-Object System.Collections.Generic.List[object]
    $builder = New-Object System.Text.StringBuilder
    [long]$totalBytes = 0
    foreach ($relativePath in $relativePaths) {
        $file = $fileByRelativePath[$relativePath]
        [long]$packageFileByteLength = $file.Length
        if ($packageFileByteLength -lt 0 -or
            $totalBytes -gt ([long]::MaxValue - $packageFileByteLength)) {
            Write-Failure ($FailurePrefix + "_byte_count_overflow")
        }
        $totalBytes += $packageFileByteLength
        $authorityRecord = Open-AuthorityReadHandle `
            -Path $file.FullName `
            -RootPath $packageFullPath `
            -Purpose ($FailurePrefix + ":" + $relativePath) `
            -FailurePrefix $FailurePrefix `
            -MaximumBytes $maximumPackageFileBytes `
            -AllowEmpty
        $hash = [string]$authorityRecord.InitialSha256
        $inputs.Add([pscustomobject]@{
            path = $relativePath
            bytes = $packageFileByteLength
            sha256 = $hash
            FullPath = $file.FullName
            AuthorityRecord = $authorityRecord
        })
        [void]$builder.Append($relativePath)
        [void]$builder.Append("|")
        [void]$builder.Append($packageFileByteLength)
        [void]$builder.Append("|")
        [void]$builder.Append($hash)
        [void]$builder.Append("`n")
    }

    return [pscustomobject]@{
        Inputs = $inputs.ToArray()
        FileCount = $inputs.Count
        TotalBytes = $totalBytes
        AggregateSha256 = Get-Utf8Sha256 -Text $builder.ToString()
    }
}

function Assert-PackageEvidenceInputs {
    param(
        [Parameter(Mandatory = $true)]
        [object[]]$EvidenceInputs,
        [Parameter(Mandatory = $true)]
        [object[]]$ActualInputs,
        [Parameter(Mandatory = $true)]
        [string]$PackageIdentity
    )

    if ($EvidenceInputs.Count -ne $ActualInputs.Count) {
        Write-Failure "package_tree_input_count_mismatch" (
            "package=$PackageIdentity expected=$($ActualInputs.Count) actual=$($EvidenceInputs.Count)")
    }
    for ($index = 0; $index -lt $ActualInputs.Count; $index++) {
        $evidenceInput = $EvidenceInputs[$index]
        $actualInput = $ActualInputs[$index]
        Assert-ExactPropertyNameSet `
            -Value $evidenceInput `
            -ExpectedNames @("path", "bytes", "sha256") `
            -Reason "package_tree_input_shape_invalid" `
            -Detail "package=$PackageIdentity index=$index"
        $evidenceBytes = Get-EvidenceInteger `
            -Value $evidenceInput.bytes `
            -Property "packages[$PackageIdentity].inputs[$index].bytes"
        if ([string]$evidenceInput.path -cne [string]$actualInput.path -or
            $evidenceBytes -ne [long]$actualInput.bytes -or
            [string]$evidenceInput.sha256 -cne [string]$actualInput.sha256) {
            Write-Failure "package_tree_input_mismatch" (
                "package=$PackageIdentity index=$index")
        }
    }
}

function Assert-StringEvidenceSequence {
    param(
        [Parameter(Mandatory = $true)]
        [object[]]$Actual,
        [Parameter(Mandatory = $true)]
        [string[]]$Expected,
        [Parameter(Mandatory = $true)]
        [string]$Reason
    )

    if ($Actual.Count -ne $Expected.Count) {
        Write-Failure $Reason (
            "expected=$($Expected.Count) actual=$($Actual.Count)")
    }
    for ($index = 0; $index -lt $Expected.Count; $index++) {
        if ($Actual[$index] -isnot [string] -or
            [string]$Actual[$index] -cne [string]$Expected[$index]) {
            Write-Failure $Reason "index=$index"
        }
    }
}

function Assert-RestoreGeneratedSemanticClosure {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectFullPath,
        [Parameter(Mandatory = $true)]
        [string]$ProjectExtensionsFullPath,
        [Parameter(Mandatory = $true)]
        [string]$PackageRootFullPath
    )

    $propsPath = Join-Path $ProjectExtensionsFullPath "OS.csproj.nuget.g.props"
    $targetsPath = Join-Path $ProjectExtensionsFullPath "OS.csproj.nuget.g.targets"
    $assetsPath = Join-Path $ProjectExtensionsFullPath "project.assets.json"
    $propsAuthority = Open-AuthorityReadHandle `
        -Path $propsPath `
        -RootPath $ProjectExtensionsFullPath `
        -Purpose "restore-generated-props" `
        -FailurePrefix "restore_generated_props" `
        -MaximumBytes $maximumProjectAssetsBytes
    $targetsAuthority = Open-AuthorityReadHandle `
        -Path $targetsPath `
        -RootPath $ProjectExtensionsFullPath `
        -Purpose "restore-generated-targets" `
        -FailurePrefix "restore_generated_targets" `
        -MaximumBytes $maximumProjectAssetsBytes
    $assetsAuthority = Open-AuthorityReadHandle `
        -Path $assetsPath `
        -RootPath $ProjectExtensionsFullPath `
        -Purpose "project-assets" `
        -FailurePrefix "restore_project_assets" `
        -MaximumBytes $maximumProjectAssetsBytes
    $props = Read-SecureXmlDocument `
        -Path $propsPath `
        -Reason "restore_generated_props_invalid"
    $propsRoot = $props.DocumentElement
    if ($null -eq $propsRoot -or
        $propsRoot.LocalName -cne "Project" -or
        $propsRoot.NamespaceURI -cne
            "http://schemas.microsoft.com/developer/msbuild/2003" -or
        $propsRoot.Attributes.Count -ne 2 -or
        $propsRoot.GetAttribute("ToolsVersion") -cne "14.0" -or
        $propsRoot.GetAttribute("xmlns") -cne
            "http://schemas.microsoft.com/developer/msbuild/2003") {
        Write-Failure "restore_generated_props_root_invalid"
    }
    $msbuildXmlNamespace =
        "http://schemas.microsoft.com/developer/msbuild/2003"
    foreach ($element in @($props.SelectNodes("//*"))) {
        if ($element.NamespaceURI -cne $msbuildXmlNamespace) {
            Write-Failure "restore_generated_props_namespace_invalid" (
                "element=$($element.LocalName)")
        }
    }

    $propsChildren = @(
        $propsRoot.ChildNodes |
            Where-Object { $_.NodeType -eq [System.Xml.XmlNodeType]::Element }
    )
    $expectedChildNames = @(
        "PropertyGroup",
        "ItemGroup",
        "ImportGroup",
        "PropertyGroup"
    )
    if ($propsChildren.Count -ne $expectedChildNames.Count) {
        Write-Failure "restore_generated_props_child_count_invalid"
    }
    for ($index = 0; $index -lt $expectedChildNames.Count; $index++) {
        if ($propsChildren[$index].LocalName -cne $expectedChildNames[$index] -or
            $propsChildren[$index].NamespaceURI -cne $msbuildXmlNamespace -or
            $propsChildren[$index].Attributes.Count -ne 1 -or
            $propsChildren[$index].GetAttribute("Condition") -cne
                " '`$(ExcludeRestorePackageImports)' != 'true' ") {
            Write-Failure "restore_generated_props_child_contract_invalid" "index=$index"
        }
    }
    $expectedDirectElementCounts = @(7, 1, 2, 3)
    for ($index = 0; $index -lt $propsChildren.Count; $index++) {
        $directElements = @(
            $propsChildren[$index].ChildNodes |
                Where-Object {
                    $_.NodeType -eq [System.Xml.XmlNodeType]::Element
                }
        )
        if ($directElements.Count -ne $expectedDirectElementCounts[$index]) {
            Write-Failure "restore_generated_props_direct_child_count_invalid" (
                "index=$index expected=$($expectedDirectElementCounts[$index]) " +
                "actual=$($directElements.Count)")
        }
    }

    $forbiddenPropsNodes = @(
        $props.SelectNodes(
            "//*[local-name()='Target' or local-name()='UsingTask' or " +
            "local-name()='Choose' or local-name()='When' or " +
            "local-name()='Otherwise' or local-name()='ItemDefinitionGroup' or " +
            "local-name()='PropertyGroup']/*[contains(local-name(), 'Custom') or " +
            "contains(local-name(), 'Before') or contains(local-name(), 'After')]")
    )
    if ($forbiddenPropsNodes.Count -ne 0) {
        Write-Failure "restore_generated_props_executable_surface_invalid"
    }

    $imports = @($props.SelectNodes("//*[local-name()='Import']"))
    $expectedImports = [ordered]@{
        '$(NuGetPackageRoot)\microsoft.net.illink.tasks\10.0.10\build\Microsoft.NET.ILLink.Tasks.props' =
            "Exists('`$(NuGetPackageRoot)\microsoft.net.illink.tasks\10.0.10\build\Microsoft.NET.ILLink.Tasks.props')"
        '$(NuGetPackageRoot)\microsoft.dotnet.ilcompiler\10.0.8\build\Microsoft.DotNet.ILCompiler.props' =
            "Exists('`$(NuGetPackageRoot)\microsoft.dotnet.ilcompiler\10.0.8\build\Microsoft.DotNet.ILCompiler.props')"
    }
    if ($imports.Count -ne $expectedImports.Count) {
        Write-Failure "restore_generated_props_import_count_invalid"
    }
    $seenImports = New-Object 'System.Collections.Generic.HashSet[string]' (
        [System.StringComparer]::Ordinal)
    foreach ($import in $imports) {
        $importProject = $import.GetAttribute("Project")
        if (-not $expectedImports.Contains($importProject) -or
            -not $seenImports.Add($importProject) -or
            $import.GetAttribute("Condition") -cne
                [string]$expectedImports[$importProject] -or
            $import.Attributes.Count -ne 2 -or
            $import.NamespaceURI -cne $msbuildXmlNamespace -or
            $import.ParentNode.LocalName -cne "ImportGroup" -or
            $import.ParentNode.NamespaceURI -cne $msbuildXmlNamespace -or
            $import.ParentNode.GetAttribute("Condition") -cne
                " '`$(ExcludeRestorePackageImports)' != 'true' ") {
            Write-Failure "restore_generated_props_import_invalid"
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
        "PkgMicrosoft_DotNet_ILCompiler"
    )
    $propertyNodes = @(
        $props.SelectNodes("//*[local-name()='PropertyGroup']/*")
    )
    if ($propertyNodes.Count -ne $allowedPropertyNames.Count) {
        Write-Failure "restore_generated_props_property_count_invalid"
    }
    $seenPropertyNames = New-Object 'System.Collections.Generic.HashSet[string]' (
        [System.StringComparer]::Ordinal)
    foreach ($propertyNode in $propertyNodes) {
        if ($allowedPropertyNames -cnotcontains $propertyNode.LocalName -or
            $propertyNode.NamespaceURI -cne $msbuildXmlNamespace -or
            -not $seenPropertyNames.Add($propertyNode.LocalName) -or
            $propertyNode.Attributes.Count -ne 1 -or
            $propertyNode.GetAttribute("Condition") -cne
                (" '`$(" + $propertyNode.LocalName + ")' == '' ")) {
            Write-Failure "restore_generated_props_property_invalid" (
                "name=$($propertyNode.LocalName)")
        }
    }

    $userProfileFullPath = [System.IO.Path]::GetFullPath(
        [Environment]::GetFolderPath(
            [Environment+SpecialFolder]::UserProfile)).TrimEnd(
                [System.IO.Path]::DirectorySeparatorChar,
                [System.IO.Path]::AltDirectorySeparatorChar)
    $userProfilePrefix = $userProfileFullPath +
        [System.IO.Path]::DirectorySeparatorChar
    $generatedPackageRoot = if ($PackageRootFullPath.StartsWith(
            $userProfilePrefix,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        '$(UserProfile)\' +
            $PackageRootFullPath.Substring($userProfilePrefix.Length)
    }
    else {
        $PackageRootFullPath
    }
    $expectedPropertyValues = [ordered]@{
        RestoreSuccess = "True"
        RestoreTool = "NuGet"
        ProjectAssetsFile = '$(MSBuildThisFileDirectory)project.assets.json'
        NuGetPackageRoot = $generatedPackageRoot
        NuGetProjectStyle = "PackageReference"
        NuGetToolVersion = "7.0.0"
        'Pkgruntime_win-x64_Microsoft_DotNet_ILCompiler' = Join-Path (
            $PackageRootFullPath) "runtime.win-x64.microsoft.dotnet.ilcompiler\10.0.8"
        'PkgMicrosoft_NET_ILLink_Tasks' = Join-Path (
            $PackageRootFullPath) "microsoft.net.illink.tasks\10.0.10"
        'PkgMicrosoft_DotNet_ILCompiler' = Join-Path (
            $PackageRootFullPath) "microsoft.dotnet.ilcompiler\10.0.8"
    }
    foreach ($propertyName in $expectedPropertyValues.Keys) {
        $matchingPropertyNodes = @(
            $propertyNodes |
                Where-Object { $_.LocalName -ceq $propertyName }
        )
        if ($matchingPropertyNodes.Count -ne 1 -or
            $matchingPropertyNodes[0].InnerText -cne
                [string]$expectedPropertyValues[$propertyName]) {
            Write-Failure "restore_generated_props_property_value_invalid" (
                "name=$propertyName")
        }
    }

    $generatedTargets = Read-SecureXmlDocument `
        -Path $targetsPath `
        -Reason "restore_generated_targets_invalid"
    if ($null -eq $generatedTargets.DocumentElement -or
        $generatedTargets.DocumentElement.LocalName -cne "Project" -or
        $generatedTargets.DocumentElement.NamespaceURI -cne
            "http://schemas.microsoft.com/developer/msbuild/2003" -or
        $generatedTargets.DocumentElement.Attributes.Count -ne 2 -or
        $generatedTargets.DocumentElement.GetAttribute("ToolsVersion") -cne
            "14.0" -or
        $generatedTargets.DocumentElement.GetAttribute("xmlns") -cne
            "http://schemas.microsoft.com/developer/msbuild/2003" -or
        @(
            $generatedTargets.DocumentElement.ChildNodes |
                Where-Object {
                    $_.NodeType -eq [System.Xml.XmlNodeType]::Element
                }
        ).Count -ne 0) {
        Write-Failure "restore_generated_targets_not_empty"
    }
    foreach ($element in @($generatedTargets.SelectNodes("//*"))) {
        if ($element.NamespaceURI -cne $msbuildXmlNamespace) {
            Write-Failure "restore_generated_targets_namespace_invalid" (
                "element=$($element.LocalName)")
        }
    }

    try {
        $assetsText = Read-AuthorityUtf8Text `
            -Record $assetsAuthority `
            -MaximumBytes $maximumProjectAssetsBytes `
            -Reason "restore_project_assets_invalid"
        $assets = $assetsText | ConvertFrom-Json
    }
    catch {
        Write-Failure "restore_project_assets_invalid" (
            "type=$($_.Exception.GetType().Name)")
    }
    Assert-ExactPropertyNameSet `
        -Value $assets `
        -ExpectedNames @(
            "version",
            "targets",
            "libraries",
            "projectFileDependencyGroups",
            "packageFolders",
            "project"
        ) `
        -Reason "restore_project_assets_root_invalid"
    if ($assets.version -isnot [int] -and $assets.version -isnot [long]) {
        Write-Failure "restore_project_assets_version_malformed"
    }
    if ([int64]$assets.version -ne 4) {
        Write-Failure "restore_project_assets_version_invalid"
    }

    $libraryProperties = @($assets.libraries.PSObject.Properties)
    if ($libraryProperties.Count -ne $expectedRestoreLibraries.Count) {
        Write-Failure "restore_project_assets_library_count_invalid"
    }
    foreach ($libraryProperty in $libraryProperties) {
        $libraryName = [string]$libraryProperty.Name
        if (-not $expectedRestoreLibraries.Contains($libraryName)) {
            Write-Failure "restore_project_assets_library_unexpected" (
                "library=$libraryName")
        }
        $expectedLibrary = $expectedRestoreLibraries[$libraryName]
        $library = $libraryProperty.Value
        Assert-ExactPropertyNameSet `
            -Value $library `
            -ExpectedNames @("sha512", "type", "path", "hasTools", "files") `
            -Reason "restore_project_assets_library_shape_invalid" `
            -Detail "library=$libraryName"
        if ([string]$library.sha512 -cne [string]$expectedLibrary.Sha512 -or
            [string]$library.type -cne "package" -or
            [string]$library.path -cne [string]$expectedLibrary.Path -or
            $library.hasTools -isnot [bool] -or
            -not [bool]$library.hasTools -or
            @($library.files).Count -ne [int]$expectedLibrary.FileCount) {
            Write-Failure "restore_project_assets_library_contract_invalid" (
                "library=$libraryName")
        }
        $seenLibraryFiles = New-Object 'System.Collections.Generic.HashSet[string]' (
            [System.StringComparer]::Ordinal)
        foreach ($libraryFile in @($library.files)) {
            $libraryFilePath = [string]$libraryFile
            if ([string]::IsNullOrWhiteSpace($libraryFilePath) -or
                [System.IO.Path]::IsPathRooted($libraryFilePath) -or
                $libraryFilePath.IndexOf(
                    "..",
                    [System.StringComparison]::Ordinal) -ge 0 -or
                $libraryFilePath.IndexOf(
                    ":",
                    [System.StringComparison]::Ordinal) -ge 0 -or
                -not $seenLibraryFiles.Add($libraryFilePath)) {
                Write-Failure "restore_project_assets_library_file_invalid" (
                    "library=$libraryName")
            }
        }
    }

    $targetProperties = @($assets.targets.PSObject.Properties)
    $expectedTargetNames = @("net10.0", "net10.0/win-x64")
    if ($targetProperties.Count -ne $expectedTargetNames.Count) {
        Write-Failure "restore_project_assets_target_count_invalid"
    }
    foreach ($targetName in $expectedTargetNames) {
        $matchingTargets = @(
            $targetProperties |
                Where-Object { $_.Name -ceq $targetName }
        )
        if ($matchingTargets.Count -ne 1) {
            Write-Failure "restore_project_assets_target_missing" (
                "target=$targetName")
        }
        $targetLibraries = @($matchingTargets[0].Value.PSObject.Properties)
        if ($targetLibraries.Count -ne $expectedRestoreLibraries.Count) {
            Write-Failure "restore_project_assets_target_library_count_invalid" (
                "target=$targetName")
        }
        foreach ($targetLibraryProperty in $targetLibraries) {
            $libraryName = [string]$targetLibraryProperty.Name
            if (-not $expectedRestoreLibraries.Contains($libraryName)) {
                Write-Failure "restore_project_assets_target_library_unexpected" (
                    "target=$targetName library=$libraryName")
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
            Assert-ExactPropertyNameSet `
                -Value $targetLibrary `
                -ExpectedNames $expectedNames `
                -Reason "restore_project_assets_target_library_shape_invalid" `
                -Detail "target=$targetName library=$libraryName"
            if ([string]$targetLibrary.type -cne "package") {
                Write-Failure "restore_project_assets_target_library_type_invalid"
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
                    Write-Failure "restore_project_assets_build_import_invalid" (
                        "target=$targetName library=$libraryName")
                }
            }
            if ($isCompiler -and $isRidTarget) {
                $dependencies = @(
                    $targetLibrary.dependencies.PSObject.Properties
                )
                if ($dependencies.Count -ne 1 -or
                    $dependencies[0].Name -cne
                        "runtime.win-x64.Microsoft.DotNet.ILCompiler" -or
                    [string]$dependencies[0].Value -cne "10.0.8") {
                    Write-Failure "restore_project_assets_compiler_dependency_invalid"
                }
            }
        }
    }

    $packageFolderProperties = @($assets.packageFolders.PSObject.Properties)
    if ($packageFolderProperties.Count -ne 1 -or
        -not [string]::Equals(
            [string]$packageFolderProperties[0].Name,
            $PackageRootFullPath,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        Write-Failure "restore_project_assets_package_folder_invalid"
    }
    $nuGetPackageFolders = @(
        $packageFolderProperties |
            ForEach-Object { [string]$_.Name }
    ) -join ";"
    $nuGetPackageFoldersNodes = @(
        $propertyNodes |
            Where-Object { $_.LocalName -ceq "NuGetPackageFolders" }
    )
    if ($nuGetPackageFoldersNodes.Count -ne 1 -or
        $nuGetPackageFoldersNodes[0].InnerText -cne $nuGetPackageFolders) {
        Write-Failure "restore_generated_package_folder_property_invalid"
    }
    $sourceRoots = @($props.SelectNodes("//*[local-name()='SourceRoot']"))
    if ($sourceRoots.Count -ne 1 -or
        $sourceRoots[0].NamespaceURI -cne $msbuildXmlNamespace -or
        $sourceRoots[0].Attributes.Count -ne 1 -or
        $sourceRoots[0].GetAttribute("Include") -cne
            ($PackageRootFullPath + [System.IO.Path]::DirectorySeparatorChar) -or
        $sourceRoots[0].ParentNode.LocalName -cne "ItemGroup" -or
        $sourceRoots[0].ParentNode.NamespaceURI -cne
            $msbuildXmlNamespace -or
        $sourceRoots[0].ParentNode.GetAttribute("Condition") -cne
            " '`$(ExcludeRestorePackageImports)' != 'true' ") {
        Write-Failure "restore_generated_source_root_invalid"
    }

    $restore = $assets.project.restore
    if ($null -eq $restore) {
        Write-Failure "restore_project_assets_restore_contract_missing"
    }
    try {
        $assetsProjectPath = [System.IO.Path]::GetFullPath(
            [string]$restore.projectPath)
        $assetsPackagesPath = [System.IO.Path]::GetFullPath(
            [string]$restore.packagesPath)
        $assetsOutputPath = [System.IO.Path]::GetFullPath(
            [string]$restore.outputPath)
    }
    catch {
        Write-Failure "restore_project_assets_restore_path_invalid"
    }
    if (-not [string]::Equals(
            $assetsProjectPath,
            $ProjectFullPath,
            [System.StringComparison]::OrdinalIgnoreCase) -or
        -not [string]::Equals(
            $assetsPackagesPath,
            $PackageRootFullPath,
            [System.StringComparison]::OrdinalIgnoreCase) -or
        -not [string]::Equals(
            $assetsOutputPath,
            $ProjectExtensionsFullPath +
                [System.IO.Path]::DirectorySeparatorChar,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        Write-Failure "restore_project_assets_restore_path_contract_invalid"
    }
}

function Initialize-BoundedJobProcessRunner {
    if ($null -ne ("CLROS.Verifier.BoundedJobProcessRunner" -as [type])) {
        return
    }

    try {
        Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace CLROS.Verifier
{
    public sealed class BoundedJobProcessResult
    {
        public string Status { get; private set; }
        public int ExitCode { get; private set; }
        public string Output { get; private set; }
        public int OutputCharacters { get; private set; }
        public bool TerminationConfirmed { get; private set; }
        public string Detail { get; private set; }

        internal BoundedJobProcessResult(
            string status,
            int exitCode,
            string output,
            int outputCharacters,
            bool terminationConfirmed,
            string detail)
        {
            Status = status;
            ExitCode = exitCode;
            Output = output ?? String.Empty;
            OutputCharacters = outputCharacters;
            TerminationConfirmed = terminationConfirmed;
            Detail = detail ?? String.Empty;
        }
    }

    public static class BoundedJobProcessRunner
    {
        private const int MinimumTimeoutSeconds = 5;
        private const int MaximumTimeoutSeconds = 3600;
        private const int MinimumOutputCharacters = 4096;
        private const int MaximumOutputCharacters = 67108864;
        private const uint CreateSuspended = 0x00000004;
        private const uint CreateNoWindow = 0x08000000;
        private const uint StartfUseStdHandles = 0x00000100;
        private const uint HandleFlagInherit = 0x00000001;
        private const uint JobObjectLimitKillOnJobClose = 0x00002000;
        private const int JobObjectBasicAccountingInformationClass = 1;
        private const int JobObjectExtendedLimitInformationClass = 9;
        private const uint WaitObject0 = 0x00000000;
        private const uint WaitFailed = 0xffffffff;
        private const uint InfiniteThreadResumeFailure = 0xffffffff;
        private const uint GenericRead = 0x80000000;
        private const uint FileShareRead = 0x00000001;
        private const uint FileShareWrite = 0x00000002;
        private const uint OpenExisting = 3;
        private const uint TerminationExitCode = 0xc1a00001;
        private const int TerminationConfirmationMilliseconds = 5000;
        private const int ReaderCompletionMilliseconds = 5000;

        [StructLayout(LayoutKind.Sequential)]
        private struct SecurityAttributes
        {
            public int Length;
            public IntPtr SecurityDescriptor;

            [MarshalAs(UnmanagedType.Bool)]
            public bool InheritHandle;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct StartupInfo
        {
            public int Size;
            public string Reserved;
            public string Desktop;
            public string Title;
            public uint X;
            public uint Y;
            public uint XSize;
            public uint YSize;
            public uint XCountChars;
            public uint YCountChars;
            public uint FillAttribute;
            public uint Flags;
            public short ShowWindow;
            public short ReservedByteCount;
            public IntPtr ReservedBytes;
            public IntPtr StandardInput;
            public IntPtr StandardOutput;
            public IntPtr StandardError;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ProcessInformation
        {
            public IntPtr Process;
            public IntPtr Thread;
            public uint ProcessId;
            public uint ThreadId;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IoCounters
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JobObjectBasicLimitInformation
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
        private struct JobObjectExtendedLimitInformation
        {
            public JobObjectBasicLimitInformation BasicLimitInformation;
            public IoCounters IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JobObjectBasicAccountingInformation
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

        private sealed class SafeKernelHandle :
            SafeHandleZeroOrMinusOneIsInvalid
        {
            private SafeKernelHandle()
                : base(true)
            {
            }

            internal SafeKernelHandle(IntPtr value)
                : base(true)
            {
                SetHandle(value);
            }

            protected override bool ReleaseHandle()
            {
                return CloseHandle(handle);
            }
        }

        private sealed class BoundedCapture
        {
            private readonly object sync = new object();
            private readonly StringBuilder standardOutput =
                new StringBuilder();
            private readonly StringBuilder standardError =
                new StringBuilder();
            private readonly int limit;
            private int characters;
            private bool overflow;
            private string failureType = String.Empty;

            internal BoundedCapture(int limit)
            {
                this.limit = limit;
            }

            internal bool Append(bool isStandardError, char[] buffer, int count)
            {
                lock (sync)
                {
                    if (overflow || failureType.Length != 0)
                    {
                        return false;
                    }

                    int remaining = limit - characters;
                    int accepted = Math.Min(remaining, count);
                    if (accepted > 0)
                    {
                        StringBuilder target = isStandardError
                            ? standardError
                            : standardOutput;
                        target.Append(buffer, 0, accepted);
                        characters += accepted;
                    }
                    if (accepted != count)
                    {
                        overflow = true;
                        return false;
                    }

                    return true;
                }
            }

            internal void Fail(Exception exception)
            {
                lock (sync)
                {
                    if (failureType.Length == 0)
                    {
                        failureType = exception.GetType().Name;
                    }
                }
            }

            internal bool Overflow
            {
                get
                {
                    lock (sync)
                    {
                        return overflow;
                    }
                }
            }

            internal string FailureType
            {
                get
                {
                    lock (sync)
                    {
                        return failureType;
                    }
                }
            }

            internal int CharacterCount
            {
                get
                {
                    lock (sync)
                    {
                        return characters;
                    }
                }
            }

            internal string GetOutput()
            {
                lock (sync)
                {
                    return standardOutput.ToString() +
                        standardError.ToString();
                }
            }
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateJobObjectW(
            IntPtr jobAttributes,
            string name);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetInformationJobObject(
            SafeKernelHandle job,
            int informationClass,
            ref JobObjectExtendedLimitInformation information,
            uint informationLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool QueryInformationJobObject(
            SafeKernelHandle job,
            int informationClass,
            out JobObjectBasicAccountingInformation information,
            uint informationLength,
            IntPtr returnLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AssignProcessToJobObject(
            SafeKernelHandle job,
            SafeKernelHandle process);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool TerminateJobObject(
            SafeKernelHandle job,
            uint exitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CreatePipe(
            out SafeFileHandle readPipe,
            out SafeFileHandle writePipe,
            ref SecurityAttributes pipeAttributes,
            uint size);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetHandleInformation(
            SafeFileHandle handle,
            uint mask,
            uint flags);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern SafeFileHandle CreateFileW(
            string fileName,
            uint desiredAccess,
            uint shareMode,
            ref SecurityAttributes securityAttributes,
            uint creationDisposition,
            uint flagsAndAttributes,
            IntPtr templateFile);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CreateProcessW(
            string applicationName,
            StringBuilder commandLine,
            IntPtr processAttributes,
            IntPtr threadAttributes,
            [MarshalAs(UnmanagedType.Bool)] bool inheritHandles,
            uint creationFlags,
            IntPtr environment,
            string currentDirectory,
            ref StartupInfo startupInfo,
            out ProcessInformation processInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint ResumeThread(SafeKernelHandle thread);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint WaitForSingleObject(
            SafeKernelHandle handle,
            uint milliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetExitCodeProcess(
            SafeKernelHandle process,
            out uint exitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool TerminateProcess(
            SafeKernelHandle process,
            uint exitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr handle);

        public static BoundedJobProcessResult Run(
            string executablePath,
            string[] arguments,
            string workingDirectory,
            int timeoutSeconds,
            int outputCharacterLimit)
        {
            if (timeoutSeconds < MinimumTimeoutSeconds ||
                timeoutSeconds > MaximumTimeoutSeconds)
            {
                return Result(
                    "InvalidTimeout",
                    -1,
                    null,
                    true,
                    "minimum=5 maximum=3600");
            }
            if (outputCharacterLimit < MinimumOutputCharacters ||
                outputCharacterLimit > MaximumOutputCharacters)
            {
                return Result(
                    "InvalidOutputLimit",
                    -1,
                    null,
                    true,
                    "minimum=4096 maximum=67108864");
            }
            if (String.IsNullOrWhiteSpace(executablePath) ||
                String.IsNullOrWhiteSpace(workingDirectory) ||
                arguments == null)
            {
                return Result(
                    "StartFailed",
                    -1,
                    null,
                    true,
                    "invalid_process_contract");
            }

            SafeKernelHandle job = null;
            SafeKernelHandle process = null;
            SafeKernelHandle thread = null;
            SafeFileHandle standardOutputRead = null;
            SafeFileHandle standardOutputWrite = null;
            SafeFileHandle standardErrorRead = null;
            SafeFileHandle standardErrorWrite = null;
            SafeFileHandle standardInput = null;
            FileStream standardOutputStream = null;
            FileStream standardErrorStream = null;
            Task[] readerTasks = null;
            BoundedCapture capture = new BoundedCapture(outputCharacterLimit);
            bool processCreated = false;
            bool processAssigned = false;

            try
            {
                IntPtr rawJob = CreateJobObjectW(IntPtr.Zero, null);
                if (rawJob == IntPtr.Zero || rawJob == new IntPtr(-1))
                {
                    return Result(
                        "JobConfigurationFailed",
                        -1,
                        capture,
                        true,
                        Win32Detail());
                }
                job = new SafeKernelHandle(rawJob);

                JobObjectExtendedLimitInformation limitInformation =
                    new JobObjectExtendedLimitInformation();
                limitInformation.BasicLimitInformation.LimitFlags =
                    JobObjectLimitKillOnJobClose;
                if (!SetInformationJobObject(
                        job,
                        JobObjectExtendedLimitInformationClass,
                        ref limitInformation,
                        (uint)Marshal.SizeOf(
                            typeof(JobObjectExtendedLimitInformation))))
                {
                    return Result(
                        "JobConfigurationFailed",
                        -1,
                        capture,
                        true,
                        Win32Detail());
                }

                SecurityAttributes inheritable = new SecurityAttributes();
                inheritable.Length = Marshal.SizeOf(
                    typeof(SecurityAttributes));
                inheritable.InheritHandle = true;
                if (!CreatePipe(
                        out standardOutputRead,
                        out standardOutputWrite,
                        ref inheritable,
                        0) ||
                    !SetHandleInformation(
                        standardOutputRead,
                        HandleFlagInherit,
                        0) ||
                    !CreatePipe(
                        out standardErrorRead,
                        out standardErrorWrite,
                        ref inheritable,
                        0) ||
                    !SetHandleInformation(
                        standardErrorRead,
                        HandleFlagInherit,
                        0))
                {
                    return Result(
                        "StartFailed",
                        -1,
                        capture,
                        true,
                        "pipe_" + Win32Detail());
                }

                standardInput = CreateFileW(
                    "NUL",
                    GenericRead,
                    FileShareRead | FileShareWrite,
                    ref inheritable,
                    OpenExisting,
                    0,
                    IntPtr.Zero);
                if (standardInput == null || standardInput.IsInvalid)
                {
                    return Result(
                        "StartFailed",
                        -1,
                        capture,
                        true,
                        "stdin_" + Win32Detail());
                }

                StringBuilder commandLine;
                try
                {
                    commandLine = BuildCommandLine(executablePath, arguments);
                }
                catch (Exception exception)
                {
                    return Result(
                        "StartFailed",
                        -1,
                        capture,
                        true,
                        "command_line_" + exception.GetType().Name);
                }
                if (commandLine.Length > 32767)
                {
                    return Result(
                        "StartFailed",
                        -1,
                        capture,
                        true,
                        "command_line_too_long");
                }

                StartupInfo startupInfo = new StartupInfo();
                startupInfo.Size = Marshal.SizeOf(typeof(StartupInfo));
                startupInfo.Flags = StartfUseStdHandles;
                startupInfo.StandardInput =
                    standardInput.DangerousGetHandle();
                startupInfo.StandardOutput =
                    standardOutputWrite.DangerousGetHandle();
                startupInfo.StandardError =
                    standardErrorWrite.DangerousGetHandle();
                ProcessInformation processInformation;
                if (!CreateProcessW(
                        executablePath,
                        commandLine,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        true,
                        CreateSuspended | CreateNoWindow,
                        IntPtr.Zero,
                        workingDirectory,
                        ref startupInfo,
                        out processInformation))
                {
                    return Result(
                        "StartFailed",
                        -1,
                        capture,
                        true,
                        Win32Detail());
                }

                processCreated = true;
                process = new SafeKernelHandle(processInformation.Process);
                thread = new SafeKernelHandle(processInformation.Thread);
                standardOutputWrite.Dispose();
                standardOutputWrite = null;
                standardErrorWrite.Dispose();
                standardErrorWrite = null;
                standardInput.Dispose();
                standardInput = null;

                if (!AssignProcessToJobObject(job, process))
                {
                    string terminationDetail;
                    bool terminated = TerminateSingleProcess(
                        process,
                        out terminationDetail);
                    if (!terminated)
                    {
                        return Result(
                            "TerminationUnconfirmed",
                            -1,
                            capture,
                            false,
                            "trigger=JobAssignmentFailed " +
                                terminationDetail);
                    }
                    return Result(
                        "JobAssignmentFailed",
                        -1,
                        capture,
                        true,
                        Win32Detail());
                }
                processAssigned = true;

                try
                {
                    standardOutputStream = new FileStream(
                        standardOutputRead,
                        FileAccess.Read,
                        4096,
                        false);
                    standardErrorStream = new FileStream(
                        standardErrorRead,
                        FileAccess.Read,
                        4096,
                        false);
                    readerTasks = new Task[]
                    {
                        StartReader(
                            standardOutputStream,
                            false,
                            capture),
                        StartReader(
                            standardErrorStream,
                            true,
                            capture)
                    };
                }
                catch (Exception exception)
                {
                    string terminationDetail;
                    bool terminated = TerminateAssignedTree(
                        job,
                        out terminationDetail);
                    if (!terminated)
                    {
                        return Result(
                            "TerminationUnconfirmed",
                            -1,
                            capture,
                            false,
                            "trigger=CaptureFailed " +
                                terminationDetail);
                    }
                    capture.Fail(exception);
                    return Result(
                        "CaptureFailed",
                        -1,
                        capture,
                        true,
                        exception.GetType().Name);
                }

                if (ResumeThread(thread) ==
                    InfiniteThreadResumeFailure)
                {
                    string terminationDetail;
                    bool terminated = TerminateAssignedTree(
                        job,
                        out terminationDetail);
                    if (!terminated)
                    {
                        return Result(
                            "TerminationUnconfirmed",
                            -1,
                            capture,
                            false,
                            "trigger=ResumeFailed " +
                                terminationDetail);
                    }
                    WaitForReaders(readerTasks, capture);
                    return Result(
                        "ResumeFailed",
                        -1,
                        capture,
                        true,
                        Win32Detail());
                }

                Stopwatch stopwatch = Stopwatch.StartNew();
                string triggerStatus = String.Empty;
                string triggerDetail = String.Empty;
                while (true)
                {
                    if (capture.FailureType.Length != 0)
                    {
                        triggerStatus = "CaptureFailed";
                        triggerDetail = capture.FailureType;
                        break;
                    }
                    if (capture.Overflow)
                    {
                        triggerStatus = "OutputOverflow";
                        triggerDetail =
                            "limit=" + outputCharacterLimit.ToString(
                                System.Globalization.CultureInfo.InvariantCulture);
                        break;
                    }

                    uint waitResult = WaitForSingleObject(process, 0);
                    if (waitResult == WaitFailed)
                    {
                        triggerStatus = "JobQueryFailed";
                        triggerDetail = "process_wait_" + Win32Detail();
                        break;
                    }
                    uint activeProcesses;
                    if (!TryGetActiveProcessCount(
                            job,
                            out activeProcesses))
                    {
                        triggerStatus = "JobQueryFailed";
                        triggerDetail = Win32Detail();
                        break;
                    }
                    if (waitResult == WaitObject0 &&
                        activeProcesses == 0)
                    {
                        break;
                    }
                    if (stopwatch.Elapsed.TotalSeconds >= timeoutSeconds)
                    {
                        triggerStatus = "TimedOut";
                        triggerDetail =
                            "timeoutSeconds=" + timeoutSeconds.ToString(
                                System.Globalization.CultureInfo.InvariantCulture);
                        break;
                    }

                    Thread.Sleep(10);
                }

                if (triggerStatus.Length != 0)
                {
                    string terminationDetail;
                    bool terminated = TerminateAssignedTree(
                        job,
                        out terminationDetail);
                    if (!terminated)
                    {
                        return Result(
                            "TerminationUnconfirmed",
                            -1,
                            capture,
                            false,
                            "trigger=" + triggerStatus + " " +
                                terminationDetail);
                    }
                    if (!WaitForReaders(readerTasks, capture))
                    {
                        triggerStatus = "CaptureFailed";
                        triggerDetail = "reader_completion_timeout";
                    }
                    return Result(
                        triggerStatus,
                        -1,
                        capture,
                        true,
                        triggerDetail);
                }

                if (!WaitForReaders(readerTasks, capture))
                {
                    return Result(
                        "CaptureFailed",
                        -1,
                        capture,
                        true,
                        "reader_completion_timeout");
                }
                if (capture.FailureType.Length != 0)
                {
                    return Result(
                        "CaptureFailed",
                        -1,
                        capture,
                        true,
                        capture.FailureType);
                }
                if (capture.Overflow)
                {
                    return Result(
                        "OutputOverflow",
                        -1,
                        capture,
                        true,
                        "limit=" + outputCharacterLimit.ToString(
                            System.Globalization.CultureInfo.InvariantCulture));
                }

                uint unsignedExitCode;
                if (!GetExitCodeProcess(process, out unsignedExitCode))
                {
                    return Result(
                        "JobQueryFailed",
                        -1,
                        capture,
                        true,
                        "exit_code_" + Win32Detail());
                }
                int exitCode = unchecked((int)unsignedExitCode);
                return Result(
                    exitCode == 0 ? "Success" : "NonZeroExit",
                    exitCode,
                    capture,
                    true,
                    String.Empty);
            }
            catch (Exception exception)
            {
                if (processCreated)
                {
                    string ignored;
                    bool terminated = processAssigned
                        ? TerminateAssignedTree(job, out ignored)
                        : TerminateSingleProcess(process, out ignored);
                    if (!terminated)
                    {
                        return Result(
                            "TerminationUnconfirmed",
                            -1,
                            capture,
                            false,
                            "trigger=CaptureFailed " + ignored);
                    }
                }
                return Result(
                    "CaptureFailed",
                    -1,
                    capture,
                    true,
                    exception.GetType().Name);
            }
            finally
            {
                if (readerTasks != null)
                {
                    WaitForReaders(readerTasks, capture);
                }
                if (standardOutputStream != null)
                {
                    standardOutputStream.Dispose();
                }
                if (standardErrorStream != null)
                {
                    standardErrorStream.Dispose();
                }
                if (standardOutputRead != null)
                {
                    standardOutputRead.Dispose();
                }
                if (standardOutputWrite != null)
                {
                    standardOutputWrite.Dispose();
                }
                if (standardErrorRead != null)
                {
                    standardErrorRead.Dispose();
                }
                if (standardErrorWrite != null)
                {
                    standardErrorWrite.Dispose();
                }
                if (standardInput != null)
                {
                    standardInput.Dispose();
                }
                if (thread != null)
                {
                    thread.Dispose();
                }
                if (process != null)
                {
                    process.Dispose();
                }
                if (job != null)
                {
                    job.Dispose();
                }
            }
        }

        private static Task StartReader(
            Stream stream,
            bool isStandardError,
            BoundedCapture capture)
        {
            return Task.Factory.StartNew(
                delegate
                {
                    try
                    {
                        using (StreamReader reader = new StreamReader(
                            stream,
                            new UTF8Encoding(false, true),
                            false,
                            4096,
                            true))
                        {
                            char[] buffer = new char[1024];
                            while (true)
                            {
                                int read = reader.Read(
                                    buffer,
                                    0,
                                    buffer.Length);
                                if (read == 0)
                                {
                                    break;
                                }
                                if (!capture.Append(
                                        isStandardError,
                                        buffer,
                                        read))
                                {
                                    break;
                                }
                            }
                        }
                    }
                    catch (Exception exception)
                    {
                        capture.Fail(exception);
                    }
                },
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        private static bool WaitForReaders(
            Task[] tasks,
            BoundedCapture capture)
        {
            try
            {
                return Task.WaitAll(
                    tasks,
                    ReaderCompletionMilliseconds);
            }
            catch (Exception exception)
            {
                capture.Fail(exception);
                return true;
            }
        }

        private static bool TryGetActiveProcessCount(
            SafeKernelHandle job,
            out uint activeProcesses)
        {
            JobObjectBasicAccountingInformation information;
            bool success = QueryInformationJobObject(
                job,
                JobObjectBasicAccountingInformationClass,
                out information,
                (uint)Marshal.SizeOf(
                    typeof(JobObjectBasicAccountingInformation)),
                IntPtr.Zero);
            activeProcesses = success
                ? information.ActiveProcesses
                : UInt32.MaxValue;
            return success;
        }

        private static bool TerminateAssignedTree(
            SafeKernelHandle job,
            out string detail)
        {
            uint activeProcesses;
            if (!TryGetActiveProcessCount(job, out activeProcesses))
            {
                detail = "initial_query_" + Win32Detail();
                return false;
            }
            if (activeProcesses == 0)
            {
                detail = String.Empty;
                return true;
            }

            bool terminateRequested =
                TerminateJobObject(job, TerminationExitCode);
            int terminationError = terminateRequested
                ? 0
                : Marshal.GetLastWin32Error();
            Stopwatch stopwatch = Stopwatch.StartNew();
            while (stopwatch.ElapsedMilliseconds <
                TerminationConfirmationMilliseconds)
            {
                if (!TryGetActiveProcessCount(
                        job,
                        out activeProcesses))
                {
                    detail = "confirmation_query_" + Win32Detail();
                    return false;
                }
                if (activeProcesses == 0)
                {
                    detail = String.Empty;
                    return true;
                }

                Thread.Sleep(10);
            }

            detail =
                "activeProcesses=" +
                activeProcesses.ToString(
                    System.Globalization.CultureInfo.InvariantCulture) +
                " terminateWin32=" +
                terminationError.ToString(
                    System.Globalization.CultureInfo.InvariantCulture);
            return false;
        }

        private static bool TerminateSingleProcess(
            SafeKernelHandle process,
            out string detail)
        {
            uint waitResult = WaitForSingleObject(process, 0);
            if (waitResult == WaitObject0)
            {
                detail = String.Empty;
                return true;
            }
            bool terminateRequested =
                TerminateProcess(process, TerminationExitCode);
            int terminationError = terminateRequested
                ? 0
                : Marshal.GetLastWin32Error();
            waitResult = WaitForSingleObject(
                process,
                TerminationConfirmationMilliseconds);
            if (waitResult == WaitObject0)
            {
                detail = String.Empty;
                return true;
            }

            detail =
                "waitResult=" +
                waitResult.ToString(
                    System.Globalization.CultureInfo.InvariantCulture) +
                " terminateWin32=" +
                terminationError.ToString(
                    System.Globalization.CultureInfo.InvariantCulture);
            return false;
        }

        private static StringBuilder BuildCommandLine(
            string executablePath,
            string[] arguments)
        {
            var commandLine = new StringBuilder();
            AppendQuotedArgument(commandLine, executablePath);
            for (int index = 0; index < arguments.Length; index++)
            {
                if (arguments[index] == null)
                {
                    throw new ArgumentException(
                        "Arguments cannot contain null.",
                        "arguments");
                }
                commandLine.Append(' ');
                AppendQuotedArgument(commandLine, arguments[index]);
            }
            return commandLine;
        }

        private static void AppendQuotedArgument(
            StringBuilder commandLine,
            string value)
        {
            if (value.IndexOf('\0') >= 0)
            {
                throw new ArgumentException(
                    "Arguments cannot contain null characters.",
                    "value");
            }
            if (value.Length != 0 &&
                value.IndexOfAny(
                    new char[] { ' ', '\t', '\n', '\v', '"' }) < 0)
            {
                commandLine.Append(value);
                return;
            }

            commandLine.Append('"');
            int backslashes = 0;
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (character == '\\')
                {
                    backslashes++;
                    continue;
                }
                if (character == '"')
                {
                    commandLine.Append('\\', (backslashes * 2) + 1);
                    commandLine.Append('"');
                    backslashes = 0;
                    continue;
                }
                if (backslashes != 0)
                {
                    commandLine.Append('\\', backslashes);
                    backslashes = 0;
                }
                commandLine.Append(character);
            }
            if (backslashes != 0)
            {
                commandLine.Append('\\', backslashes * 2);
            }
            commandLine.Append('"');
        }

        private static string Win32Detail()
        {
            return "win32=" +
                Marshal.GetLastWin32Error().ToString(
                    System.Globalization.CultureInfo.InvariantCulture);
        }

        private static BoundedJobProcessResult Result(
            string status,
            int exitCode,
            BoundedCapture capture,
            bool terminationConfirmed,
            string detail)
        {
            string output = capture == null
                ? String.Empty
                : capture.GetOutput();
            int outputCharacters = capture == null
                ? 0
                : capture.CharacterCount;
            return new BoundedJobProcessResult(
                status,
                exitCode,
                output,
                outputCharacters,
                terminationConfirmed,
                detail);
        }
    }
}
'@
    }
    catch {
        Write-Failure "post_restore_process_runner_unavailable" (
            "type=$($_.Exception.GetType().Name)")
    }
}

function Invoke-BoundedJobProcess {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ExecutablePath,
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,
        [Parameter(Mandatory = $true)]
        [string]$WorkingDirectory,
        [Parameter(Mandatory = $true)]
        [int]$TimeoutSeconds,
        [Parameter(Mandatory = $true)]
        [int]$OutputCharacterLimit
    )

    Initialize-BoundedJobProcessRunner
    return [CLROS.Verifier.BoundedJobProcessRunner]::Run(
        $ExecutablePath,
        $Arguments,
        $WorkingDirectory,
        $TimeoutSeconds,
        $OutputCharacterLimit)
}

function Invoke-PostRestoreEvaluationSnapshot {
    param(
        [Parameter(Mandatory = $true)]
        [string]$DotnetHostPath,
        [Parameter(Mandatory = $true)]
        [string]$ProjectFullPath,
        [Parameter(Mandatory = $true)]
        [string]$Configuration,
        [Parameter(Mandatory = $true)]
        [string]$RuntimeIdentifier,
        [Parameter(Mandatory = $true)]
        [string]$ProjectExtensionsFullPath,
        [Parameter(Mandatory = $true)]
        [string]$ProjectAssetsFullPath,
        [Parameter(Mandatory = $true)]
        [string]$PackageRootFullPath,
        [Parameter(Mandatory = $true)]
        [string]$DotnetSdkDirectory,
        [Parameter(Mandatory = $true)]
        [System.Collections.IDictionary]$CanonicalPaths
    )

    $canonicalRootPropsPath = Join-Path $repositoryRoot "Directory.Build.props"
    $canonicalRootTargetsPath = Join-Path $repositoryRoot "Directory.Build.targets"
    $dotnetSdkFullPath = [System.IO.Path]::GetFullPath(
        $DotnetSdkDirectory).TrimEnd(
            [System.IO.Path]::DirectorySeparatorChar,
            [System.IO.Path]::AltDirectorySeparatorChar)
    $msbuildSdksPath = Join-Path $dotnetSdkFullPath "Sdks"
    $msbuildExtensionsPath = $dotnetSdkFullPath +
        [System.IO.Path]::DirectorySeparatorChar
    $msbuildUserExtensionsPath = Join-Path `
        $ProjectExtensionsFullPath `
        "disabled-user-extensions"
    if (-not (Test-Path -LiteralPath $msbuildSdksPath -PathType Container)) {
        Write-Failure "post_restore_sdk_path_missing" "path=$msbuildSdksPath"
    }
    Assert-NoReparsePoints `
        -RootPath (Get-AuthorityRootPath -Path $dotnetSdkFullPath) `
        -Path $dotnetSdkFullPath `
        -Reason "post_restore_sdk_path_reparse_point"
    Assert-NoReparsePoints `
        -RootPath $dotnetSdkFullPath `
        -Path $msbuildSdksPath `
        -Reason "post_restore_sdk_path_reparse_point"
    Assert-NoReparsePoints `
        -RootPath (Get-AuthorityRootPath -Path $ProjectExtensionsFullPath) `
        -Path $ProjectExtensionsFullPath `
        -Reason "post_restore_project_extensions_reparse_point"
    if (Test-Path -LiteralPath $msbuildUserExtensionsPath) {
        Write-Failure "post_restore_user_extensions_path_present" (
            "path=$msbuildUserExtensionsPath")
    }
    $wildcardPropertyList = $msbuildWildcardPropertyNames -join ","
    $arguments = @(
        "msbuild",
        $ProjectFullPath,
        "-noAutoResponse",
        "-nodeReuse:false",
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
            "EnableM04IndependentEndianConformanceProbe," +
            "EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe," +
            "EnableM04ArithmeticConformanceProbe," +
            "EnableM04LayoutContractProbe," +
            "EnableM04ManagedHeapPageRangeProbe," +
            "EnableM04RhpNewFastProbe," +
            "EnableM04RhpNewFastBeforeBindFailureProbe," +
            "EnableM04RhpNewFastExhaustionFailureProbe," +
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
            $wildcardPropertyList +
            ",UseSharedCompilation"
        ),
        ("-p:Configuration=" + $Configuration),
        ("-p:RuntimeIdentifier=" + $RuntimeIdentifier),
        ("-p:DirectoryBuildPropsPath=" + $canonicalRootPropsPath),
        ("-p:DirectoryBuildTargetsPath=" + $canonicalRootTargetsPath),
        ("-p:MSBuildProjectExtensionsPath=" +
            $ProjectExtensionsFullPath.Replace("\", "/").TrimEnd("/") + "/"),
        ("-p:ProjectAssetsFile=" + $ProjectAssetsFullPath.Replace("\", "/")),
        ("-p:RestorePackagesPath=" + $PackageRootFullPath),
        ("-p:NuGetPackageRoot=" + $PackageRootFullPath),
        ("-p:MSBuildSDKsPath=" + $msbuildSdksPath),
        ("-p:MSBuildExtensionsPath=" + $msbuildExtensionsPath),
        ("-p:MSBuildUserExtensionsPath=" + $msbuildUserExtensionsPath),
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
        "-p:ImportByWildcardBeforeMicrosoftCommonProps=false",
        "-p:ImportByWildcardAfterMicrosoftCommonProps=false",
        "-p:ImportUserLocationsByWildcardBeforeMicrosoftCommonProps=false",
        "-p:ImportUserLocationsByWildcardAfterMicrosoftCommonProps=false",
        "-p:ImportByWildcardBeforeMicrosoftCommonTargets=false",
        "-p:ImportByWildcardAfterMicrosoftCommonTargets=false",
        "-p:ImportUserLocationsByWildcardBeforeMicrosoftCommonTargets=false",
        "-p:ImportUserLocationsByWildcardAfterMicrosoftCommonTargets=false",
        "-p:ImportByWildcardBeforeMicrosoftCSharpTargets=false",
        "-p:ImportByWildcardAfterMicrosoftCSharpTargets=false",
        "-p:ImportUserLocationsByWildcardBeforeMicrosoftCSharpTargets=false",
        "-p:ImportUserLocationsByWildcardAfterMicrosoftCSharpTargets=false",
        "-p:ImportByWildcardBeforeMicrosoftCommonCrossTargetingTargets=false",
        "-p:ImportByWildcardAfterMicrosoftCommonCrossTargetingTargets=false",
        "-p:UseSharedCompilation=false",
        "-p:BeforeMicrosoftNETSdkTargets=",
        "-p:AfterMicrosoftNETSdkTargets=",
        ("-p:ILCompilerTargetsPath=" + $CanonicalPaths.ILCompilerTargetsPath),
        ("-p:ILLinkTargetsPath=" + $CanonicalPaths.ILLinkTargetsPath),
        ("-p:ILLinkTasksAssembly=" + $CanonicalPaths.ILLinkTasksAssembly),
        ("-p:IlcBuildTasksPath=" + $CanonicalPaths.IlcBuildTasksPath),
        ("-p:IlcHostPackagePath=" + $CanonicalPaths.IlcHostPackagePath),
        ("-p:ClrosIlcHostPackagePath=" + $CanonicalPaths.IlcHostPackagePath),
        ("-p:IlcToolsPath=" + $CanonicalPaths.IlcToolsPath),
        ("-p:RuntimePackagePath=" + $CanonicalPaths.RuntimePackagePath),
        ("-p:ClrosRuntimePackagePath=" + $CanonicalPaths.RuntimePackagePath),
        ("-p:IlcFrameworkPath=" + $CanonicalPaths.IlcFrameworkPath),
        ("-p:IlcFrameworkNativePath=" + $CanonicalPaths.IlcFrameworkNativePath),
        ("-p:IlcSdkPath=" + $CanonicalPaths.IlcSdkPath),
        ("-p:IlcMibcPath=" + $CanonicalPaths.IlcMibcPath),
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
        ("-p:LanguageTargets=" + $CanonicalPaths.LanguageTargets),
        ("-p:CSharpTargetsPath=" + $CanonicalPaths.CSharpTargetsPath),
        ("-p:CommonTargetsPath=" + $CanonicalPaths.CommonTargetsPath)
    )
    if ($EnableM04IndependentEndianConformanceProbe) {
        $arguments +=
            "-p:EnableM04IndependentEndianConformanceProbe=true"
    }
    if ($EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe) {
        $arguments +=
            "-p:EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe=true"
    }
    if ($EnableM04ArithmeticConformanceProbe) {
        $arguments +=
            "-p:EnableM04ArithmeticConformanceProbe=true"
    }
    if ($EnableM04LayoutContractProbe) {
        $arguments +=
            "-p:EnableM04LayoutContractProbe=true"
    }
    if ($EnableM04ManagedHeapPageRangeProbe) {
        $arguments += "-p:EnableM04ManagedHeapPageRangeProbe=true"
    }
    if ($EnableM04RhpNewFastProbe) {
        $arguments += "-p:EnableM04RhpNewFastProbe=true"
    }
    if ($EnableM04RhpNewFastBeforeBindFailureProbe) {
        $arguments += "-p:EnableM04RhpNewFastBeforeBindFailureProbe=true"
    }
    if ($EnableM04RhpNewFastExhaustionFailureProbe) {
        $arguments += "-p:EnableM04RhpNewFastExhaustionFailureProbe=true"
    }
    $arguments += ("-p:EnableM01LoadedImageStackProbe=" + $EnableM01LoadedImageStackProbe.IsPresent.ToString().ToLowerInvariant())
    $arguments += ("-p:EnableM01LiveAllocatorProbe=" + $EnableM01LiveAllocatorProbe.IsPresent.ToString().ToLowerInvariant())
    $arguments += ("-p:EnableM01OwnedPagingProbe=" + $EnableM01OwnedPagingProbe.IsPresent.ToString().ToLowerInvariant())
    $arguments += ("-p:EnableM02SafeStubProbe=" + $EnableM02SafeStubProbe.IsPresent.ToString().ToLowerInvariant())
    $arguments += ("-p:EnableM02CheckedPortIoBindingProbe=" + $EnableM02CheckedPortIoBindingProbe.IsPresent.ToString().ToLowerInvariant())
    $arguments += ("-p:EnableM02Com1OwnedPortIoProbe=" + $EnableM02Com1OwnedPortIoProbe.IsPresent.ToString().ToLowerInvariant())
    $arguments += ("-p:EnableUefiGoneSimulationProbe=" + $EnableUefiGoneSimulationProbe.IsPresent.ToString().ToLowerInvariant())
    $arguments += ("-p:EnableExitBootServicesFreshMapProbe=" + $EnableExitBootServicesFreshMapProbe.IsPresent.ToString().ToLowerInvariant())
    $arguments += ("-p:EnableAcpiPhysicalDiscoveryProbe=" + $EnableAcpiPhysicalDiscoveryProbe.IsPresent.ToString().ToLowerInvariant())

    if ($EnableM02OwnedCom1ReadyProbe) {
        $arguments += "-p:EnableM02OwnedCom1ReadyProbe=true"
    }
    if ($EnableM02PanicSafeOwnedCom1Probe) {
        $arguments += "-p:EnableM02PanicSafeOwnedCom1Probe=true"
    }
    if ($EnableM02OwnedX64GdtEncodingProbe) {
        $arguments += "-p:EnableM02OwnedX64GdtEncodingProbe=true"
    }
    if ($EnableM02GuardedIstStackAllocationProbe) {
        $arguments +=
            "-p:EnableM02GuardedIstStackAllocationProbe=true"
    }
    if ($EnableM02OwnedGdtTssTerminalProbe) {
        $arguments +=
            "-p:EnableM02OwnedGdtTssTerminalProbe=true"
    }
    if ($EnableM02NormalizedInterruptFramePageProbe) {
        $arguments +=
            "-p:EnableM02NormalizedInterruptFramePageProbe=true"
    }
    if ($EnableM02OwnedIdtGatePopulationProbe) {
        $arguments +=
            "-p:EnableM02OwnedIdtGatePopulationProbe=true"
    }
    if ($EnableM02IdtrContinuedExecutionProbe) {
        $arguments +=
            "-p:EnableM02IdtrContinuedExecutionProbe=true"
    }
    if ($EnableM02Ud2VectorStateProbe) {
        $arguments += "-p:EnableM02Ud2VectorStateProbe=true"
    }
    if ($EnableM02PageFaultCr2ErrorCodeProbe) {
        $arguments += "-p:EnableM02PageFaultCr2ErrorCodeProbe=true"
    }

    $previousLink = $env:LINK
    $previousTrailingLink = $env:_LINK_
    $previousMSBuildSdksPath = $env:MSBuildSDKsPath
    $previousMSBuildExtensionsPath = $env:MSBuildExtensionsPath
    $previousMSBuildUserExtensionsPath = $env:MSBuildUserExtensionsPath
    $previousDotnetCliUseMsbuildServer = $env:DOTNET_CLI_USE_MSBUILD_SERVER
    $previousMSBuildDisableNodeReuse = $env:MSBUILDDISABLENODEREUSE
    $previousUseSharedCompilation = $env:UseSharedCompilation
    $previousDotnetCliTelemetryOptOut = $env:DOTNET_CLI_TELEMETRY_OPTOUT
    $previousDotnetNoLogo = $env:DOTNET_NOLOGO
    try {
        $env:LINK = $null
        $env:_LINK_ = $null
        $env:MSBuildSDKsPath = $msbuildSdksPath
        $env:MSBuildExtensionsPath = $msbuildExtensionsPath
        $env:MSBuildUserExtensionsPath = $msbuildUserExtensionsPath
        $env:DOTNET_CLI_USE_MSBUILD_SERVER = "0"
        $env:MSBUILDDISABLENODEREUSE = "1"
        $env:UseSharedCompilation = "false"
        $env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
        $env:DOTNET_NOLOGO = "1"
        $processResult = Invoke-BoundedJobProcess `
            -ExecutablePath $DotnetHostPath `
            -Arguments ([string[]]$arguments) `
            -WorkingDirectory $repositoryRoot `
            -TimeoutSeconds $postRestoreEvaluationTimeoutSeconds `
            -OutputCharacterLimit $postRestoreEvaluationOutputCharacterLimit
    }
    finally {
        $env:LINK = $previousLink
        $env:_LINK_ = $previousTrailingLink
        $env:MSBuildSDKsPath = $previousMSBuildSdksPath
        $env:MSBuildExtensionsPath = $previousMSBuildExtensionsPath
        $env:MSBuildUserExtensionsPath = $previousMSBuildUserExtensionsPath
        $env:DOTNET_CLI_USE_MSBUILD_SERVER = $previousDotnetCliUseMsbuildServer
        $env:MSBUILDDISABLENODEREUSE = $previousMSBuildDisableNodeReuse
        $env:UseSharedCompilation = $previousUseSharedCompilation
        $env:DOTNET_CLI_TELEMETRY_OPTOUT = $previousDotnetCliTelemetryOptOut
        $env:DOTNET_NOLOGO = $previousDotnetNoLogo
    }
    if (Test-Path -LiteralPath $msbuildUserExtensionsPath) {
        Write-Failure "post_restore_user_extensions_path_present" (
            "path=$msbuildUserExtensionsPath")
    }
    $diagnosticOutput = [string]$processResult.Output
    if ($diagnosticOutput.Length -gt 1024) {
        $diagnosticOutput = $diagnosticOutput.Substring(0, 1024)
    }
    switch ([string]$processResult.Status) {
        "Success" {
            break
        }
        "InvalidTimeout" {
            Write-Failure "post_restore_evaluation_timeout_invalid" (
                [string]$processResult.Detail)
        }
        "InvalidOutputLimit" {
            Write-Failure "post_restore_evaluation_output_limit_invalid" (
                [string]$processResult.Detail)
        }
        "TimedOut" {
            Write-Failure "post_restore_evaluation_timeout" (
                [string]$processResult.Detail)
        }
        "OutputOverflow" {
            Write-Failure "post_restore_evaluation_output_overflow" (
                [string]$processResult.Detail)
        }
        "CaptureFailed" {
            Write-Failure "post_restore_evaluation_capture_failed" (
                [string]$processResult.Detail)
        }
        "NonZeroExit" {
            Write-Failure "post_restore_evaluation_nonzero_exit" (
                "exitCode=$($processResult.ExitCode) output=$diagnosticOutput")
        }
        "TerminationUnconfirmed" {
            Write-Failure "post_restore_evaluation_termination_unconfirmed" (
                [string]$processResult.Detail)
        }
        "StartFailed" {
            Write-Failure "post_restore_evaluation_start_failed" (
                [string]$processResult.Detail)
        }
        "JobConfigurationFailed" {
            Write-Failure "post_restore_evaluation_job_configuration_failed" (
                [string]$processResult.Detail)
        }
        "JobAssignmentFailed" {
            Write-Failure "post_restore_evaluation_job_assignment_failed" (
                [string]$processResult.Detail)
        }
        "ResumeFailed" {
            Write-Failure "post_restore_evaluation_resume_failed" (
                [string]$processResult.Detail)
        }
        "JobQueryFailed" {
            Write-Failure "post_restore_evaluation_job_query_failed" (
                [string]$processResult.Detail)
        }
        default {
            Write-Failure "post_restore_evaluation_status_invalid" (
                "status=$($processResult.Status)")
        }
    }
    if (-not [bool]$processResult.TerminationConfirmed) {
        Write-Failure "post_restore_evaluation_termination_unconfirmed"
    }
    $outputText = [string]$processResult.Output
    $jsonStart = $outputText.IndexOf(
        "{",
        [System.StringComparison]::Ordinal)
    $jsonEnd = $outputText.LastIndexOf(
        "}",
        [System.StringComparison]::Ordinal)
    if ($jsonStart -lt 0 -or $jsonEnd -le $jsonStart) {
        Write-Failure "post_restore_evaluation_output_malformed"
    }
    try {
        $evaluation = $outputText.Substring(
            $jsonStart,
            $jsonEnd - $jsonStart + 1) | ConvertFrom-Json
    }
    catch {
        Write-Failure "post_restore_evaluation_json_invalid"
    }
    if ($null -eq $evaluation.Properties -or $null -eq $evaluation.Items) {
        Write-Failure "post_restore_evaluation_shape_invalid"
    }

    $expectedProperties = [ordered]@{
        _targetOS = "win"
        _targetArchitecture = "x64"
        RuntimeIdentifier = $RuntimeIdentifier
        TargetFramework = "net10.0"
        PublishAot = "true"
        SelfContained = "true"
        PublishTrimmed = "true"
        EnableM02OwnedCom1ReadyProbe =
            $EnableM02OwnedCom1ReadyProbe.IsPresent.ToString().ToLowerInvariant()
        EnableM02PanicSafeOwnedCom1Probe =
            $EnableM02PanicSafeOwnedCom1Probe.IsPresent.ToString().ToLowerInvariant()
        EnableM02OwnedX64GdtEncodingProbe =
            $EnableM02OwnedX64GdtEncodingProbe.IsPresent.ToString().ToLowerInvariant()
        EnableM02GuardedIstStackAllocationProbe =
            $EnableM02GuardedIstStackAllocationProbe.IsPresent.ToString().ToLowerInvariant()
        EnableM02OwnedGdtTssTerminalProbe =
            $EnableM02OwnedGdtTssTerminalProbe.IsPresent.ToString().ToLowerInvariant()
        EnableM02NormalizedInterruptFramePageProbe =
            $EnableM02NormalizedInterruptFramePageProbe.IsPresent.ToString().ToLowerInvariant()
        EnableM02OwnedIdtGatePopulationProbe =
            $EnableM02OwnedIdtGatePopulationProbe.IsPresent.ToString().ToLowerInvariant()
        EnableM02IdtrContinuedExecutionProbe =
            $EnableM02IdtrContinuedExecutionProbe.IsPresent.ToString().ToLowerInvariant()
        EnableM02Ud2VectorStateProbe =
            $EnableM02Ud2VectorStateProbe.IsPresent.ToString().ToLowerInvariant()
        EnableM02PageFaultCr2ErrorCodeProbe =
            $EnableM02PageFaultCr2ErrorCodeProbe.IsPresent.ToString().ToLowerInvariant()
        EnableM04IndependentEndianConformanceProbe =
            $EnableM04IndependentEndianConformanceProbe.IsPresent.ToString().ToLowerInvariant()
        EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe =
            $EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe.IsPresent.ToString().ToLowerInvariant()
        EnableM04ArithmeticConformanceProbe =
            $EnableM04ArithmeticConformanceProbe.IsPresent.ToString().ToLowerInvariant()
        EnableM04LayoutContractProbe =
            $EnableM04LayoutContractProbe.IsPresent.ToString().ToLowerInvariant()
        EnableM04ManagedHeapPageRangeProbe =
            $EnableM04ManagedHeapPageRangeProbe.IsPresent.ToString().ToLowerInvariant()
        EnableM04RhpNewFastProbe =
            $EnableM04RhpNewFastProbe.IsPresent.ToString().ToLowerInvariant()
        EnableM04RhpNewFastBeforeBindFailureProbe =
            $EnableM04RhpNewFastBeforeBindFailureProbe.IsPresent.ToString().ToLowerInvariant()
        EnableM04RhpNewFastExhaustionFailureProbe =
            $EnableM04RhpNewFastExhaustionFailureProbe.IsPresent.ToString().ToLowerInvariant()
        ImportProjectExtensionProps = "false"
        ImportProjectExtensionTargets = "false"
        ExcludeRestorePackageImports = "true"
        ILCompilerTargetsPath = $CanonicalPaths.ILCompilerTargetsPath
        ILLinkTargetsPath = $CanonicalPaths.ILLinkTargetsPath
        ILLinkTasksAssembly = $CanonicalPaths.ILLinkTasksAssembly
        IlcBuildTasksPath = $CanonicalPaths.IlcBuildTasksPath
        IlcHostPackagePath = $CanonicalPaths.IlcHostPackagePath
        ClrosIlcHostPackagePath = $CanonicalPaths.IlcHostPackagePath
        IlcToolsPath = $CanonicalPaths.IlcToolsPath
        RuntimePackagePath = $CanonicalPaths.RuntimePackagePath
        ClrosRuntimePackagePath = $CanonicalPaths.RuntimePackagePath
        IlcFrameworkPath = $CanonicalPaths.IlcFrameworkPath
        IlcFrameworkNativePath = $CanonicalPaths.IlcFrameworkNativePath
        IlcSdkPath = $CanonicalPaths.IlcSdkPath
        IlcMibcPath = $CanonicalPaths.IlcMibcPath
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
        LanguageTargets = $CanonicalPaths.LanguageTargets
        CSharpTargetsPath = $CanonicalPaths.CSharpTargetsPath
        CommonTargetsPath = $CanonicalPaths.CommonTargetsPath
        MSBuildSDKsPath = $msbuildSdksPath
        MSBuildExtensionsPath = $msbuildExtensionsPath
        MSBuildUserExtensionsPath = $msbuildUserExtensionsPath
    }
    $expectedProperties["EnableM01LoadedImageStackProbe"] = $EnableM01LoadedImageStackProbe.IsPresent.ToString().ToLowerInvariant()
    $expectedProperties["EnableM01LiveAllocatorProbe"] = $EnableM01LiveAllocatorProbe.IsPresent.ToString().ToLowerInvariant()
    $expectedProperties["EnableM01OwnedPagingProbe"] = $EnableM01OwnedPagingProbe.IsPresent.ToString().ToLowerInvariant()
    $expectedProperties["EnableM02SafeStubProbe"] = $EnableM02SafeStubProbe.IsPresent.ToString().ToLowerInvariant()
    $expectedProperties["EnableM02CheckedPortIoBindingProbe"] = $EnableM02CheckedPortIoBindingProbe.IsPresent.ToString().ToLowerInvariant()
    $expectedProperties["EnableM02Com1OwnedPortIoProbe"] = $EnableM02Com1OwnedPortIoProbe.IsPresent.ToString().ToLowerInvariant()
    $expectedProperties["EnableUefiGoneSimulationProbe"] = $EnableUefiGoneSimulationProbe.IsPresent.ToString().ToLowerInvariant()
    $expectedProperties["EnableExitBootServicesFreshMapProbe"] = $EnableExitBootServicesFreshMapProbe.IsPresent.ToString().ToLowerInvariant()
    $expectedProperties["EnableAcpiPhysicalDiscoveryProbe"] = $EnableAcpiPhysicalDiscoveryProbe.IsPresent.ToString().ToLowerInvariant()


    foreach ($wildcardPropertyName in $msbuildWildcardPropertyNames) {
        $expectedProperties[$wildcardPropertyName] = "false"
    }
    $expectedProperties["UseSharedCompilation"] = "false"
    Assert-ExactPropertyNameSet `
        -Value $evaluation.Properties `
        -ExpectedNames ([string[]]@($expectedProperties.Keys)) `
        -Reason "post_restore_evaluation_property_set_invalid"
    foreach ($entry in $expectedProperties.GetEnumerator()) {
        if ($entry.Key.StartsWith('Enable', [StringComparison]::Ordinal) -and
            -not [string]::Equals([string]$evaluation.Properties.($entry.Key),
                [string]$entry.Value, [StringComparison]::Ordinal)) {
            Write-Failure "post_restore_evaluation_property_mismatch" ("property=$($entry.Key)")
        }
        if ([string]$evaluation.Properties.($entry.Key) -cne
            [string]$entry.Value) {
            Write-Failure "post_restore_evaluation_property_mismatch" (
                "property=$($entry.Key)")
        }
    }

    $compileItems = @($evaluation.Items.Compile)
    $projectReferences = @($evaluation.Items.ProjectReference)
    $references = @($evaluation.Items.Reference)
    $analyzers = @($evaluation.Items.Analyzer)
    if ($compileItems.Count -ne 77 -or
        $compileItems.Count -ne $expectedEfiCompileItems.Count -or
        $projectReferences.Count -ne 0 -or
        $references.Count -ne 0 -or
        $analyzers.Count -ne 2) {
        Write-Failure "post_restore_evaluation_item_count_mismatch" (
            "compile=$($compileItems.Count) project=$($projectReferences.Count) " +
            "reference=$($references.Count) analyzer=$($analyzers.Count)")
    }

    $compileBuilder = New-Object System.Text.StringBuilder
    for ($index = 0; $index -lt $compileItems.Count; $index++) {
        $item = $compileItems[$index]
        $linkProperty = $item.PSObject.Properties["Link"]
        $link = if ($null -eq $linkProperty) {
            ""
        }
        else {
            [string]$linkProperty.Value
        }
        $key = [string]$item.Identity + "|" + $link
        if ($key -cne $expectedEfiCompileItems[$index]) {
            Write-Failure "post_restore_evaluation_compile_item_mismatch" (
                "index=$index")
        }
        [void]$compileBuilder.Append($key)
        [void]$compileBuilder.Append("`n")
    }
    $expectedAnalyzerNames = @(
        "Microsoft.CodeAnalysis.CSharp.NetAnalyzers.dll",
        "Microsoft.CodeAnalysis.NetAnalyzers.dll"
    )
    $analyzerRecords = New-Object System.Collections.Generic.List[string]
    foreach ($analyzer in $analyzers) {
        $analyzerName = [System.IO.Path]::GetFileName(
            [string]$analyzer.FullPath)
        $definingProjectName = [System.IO.Path]::GetFileName(
            [string]$analyzer.DefiningProjectFullPath)
        if ($expectedAnalyzerNames -cnotcontains $analyzerName -or
            $definingProjectName -cne
                "Microsoft.NET.Sdk.Analyzers.targets") {
            Write-Failure "post_restore_evaluation_analyzer_item_mismatch"
        }
        $analyzerRecords.Add($analyzerName + "|" + $definingProjectName)
    }
    [string[]]$analyzerArray = $analyzerRecords.ToArray()
    [System.Array]::Sort($analyzerArray, [System.StringComparer]::Ordinal)
    return [pscustomobject]@{
        compileCount = $compileItems.Count
        compileSha256 = Get-Utf8Sha256 -Text $compileBuilder.ToString()
        projectReferenceCount = $projectReferences.Count
        projectReferenceSha256 = Get-Utf8Sha256 -Text ""
        referenceCount = $references.Count
        referenceSha256 = Get-Utf8Sha256 -Text ""
        analyzerCount = $analyzers.Count
        analyzerSha256 = Get-Utf8Sha256 -Text (
            ($analyzerArray -join "`n") + "`n")
        properties = $expectedProperties
    }
}

function Assert-PostRestoreEvidenceMatchesSnapshot {
    param(
        [AllowNull()]
        [object]$Evidence,
        [Parameter(Mandatory = $true)]
        [object]$Snapshot
    )

    $expectedNames = @(
        "compileCount",
        "compileSha256",
        "projectReferenceCount",
        "projectReferenceSha256",
        "referenceCount",
        "referenceSha256",
        "analyzerCount",
        "analyzerSha256",
        "properties"
    )
    Assert-ExactPropertyNameSet `
        -Value $Evidence `
        -ExpectedNames $expectedNames `
        -Reason "post_restore_evidence_shape_invalid"
    foreach ($countProperty in @(
            "compileCount",
            "projectReferenceCount",
            "referenceCount",
            "analyzerCount")) {
        $evidenceCount = Get-EvidenceInteger `
            -Value $Evidence.$countProperty `
            -Property ("postRestoreEvaluation." + $countProperty)
        if ($evidenceCount -ne [long]$Snapshot.$countProperty) {
            Write-Failure "post_restore_evidence_count_mismatch" (
                "property=$countProperty")
        }
    }
    foreach ($hashProperty in @(
            "compileSha256",
            "projectReferenceSha256",
            "referenceSha256",
            "analyzerSha256")) {
        if ([string]$Evidence.$hashProperty -cnotmatch '^[0-9a-f]{64}$' -or
            [string]$Evidence.$hashProperty -cne
                [string]$Snapshot.$hashProperty) {
            Write-Failure "post_restore_evidence_hash_mismatch" (
                "property=$hashProperty")
        }
    }

    [string[]]$expectedPropertyNames = @($Snapshot.properties.Keys)
    Assert-ExactPropertyNameSet `
        -Value $Evidence.properties `
        -ExpectedNames $expectedPropertyNames `
        -Reason "post_restore_evidence_property_set_invalid"
    foreach ($propertyName in $expectedPropertyNames) {
        if ($propertyName.StartsWith('Enable', [StringComparison]::Ordinal) -and
            -not [string]::Equals([string]$Evidence.properties.$propertyName,
                [string]$Snapshot.properties[$propertyName], [StringComparison]::Ordinal)) {
            Write-Failure "post_restore_evidence_property_mismatch" ("property=$propertyName")
        }
        if ([string]$Evidence.properties.$propertyName -cne
            [string]$Snapshot.properties[$propertyName]) {
            Write-Failure "post_restore_evidence_property_mismatch" (
                "property=$propertyName")
        }
    }
}

Write-Output "TEST_START $testName"

$rhpReason = ""
if (($EnableM04RhpNewFastBeforeBindFailureProbe -or $EnableM04RhpNewFastExhaustionFailureProbe) -and -not $EnableM04RhpNewFastProbe) { $rhpReason = "m04_rhp_new_fast_negative_requires_main" }
if ($EnableM04RhpNewFastBeforeBindFailureProbe -and $EnableM04RhpNewFastExhaustionFailureProbe) { $rhpReason = "m04_rhp_new_fast_negative_conflict" }
if ($EnableM04RhpNewFastProbe) {
    foreach ($rhpRequired in @("EnableM01LoadedImageStackProbe", "EnableM01LiveAllocatorProbe", "EnableM04IndependentEndianConformanceProbe", "EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe", "EnableM04ArithmeticConformanceProbe", "EnableM04LayoutContractProbe", "EnableM04ManagedHeapPageRangeProbe")) {
        if (-not [bool](Get-Variable -Name $rhpRequired -ValueOnly)) { $rhpReason = "m04_rhp_new_fast_requires_explicit_chain_" + $rhpRequired }
    }
    foreach ($rhpConflict in @("EnableM02SafeStubProbe", "EnableM02CheckedPortIoBindingProbe", "EnableM02Com1OwnedPortIoProbe", "EnableM02OwnedCom1ReadyProbe", "EnableM02PanicSafeOwnedCom1Probe", "EnableM02OwnedX64GdtEncodingProbe", "EnableM02GuardedIstStackAllocationProbe", "EnableM02OwnedGdtTssTerminalProbe", "EnableM02NormalizedInterruptFramePageProbe", "EnableM02OwnedIdtGatePopulationProbe", "EnableM02IdtrContinuedExecutionProbe", "EnableM02Ud2VectorStateProbe", "EnableM02PageFaultCr2ErrorCodeProbe", "EnableM01OwnedPagingProbe", "EnableUefiGoneSimulationProbe", "EnableExitBootServicesFreshMapProbe", "EnableAcpiPhysicalDiscoveryProbe")) {
        if ([bool](Get-Variable -Name $rhpConflict -ValueOnly)) { $rhpReason = "m04_rhp_new_fast_conflicting_profile_" + $rhpConflict }
    }
}
if ($rhpReason.Length -ne 0) { Write-Failure $rhpReason }

if ($EnableM04ManagedHeapPageRangeProbe -and
    (-not $EnableM01LoadedImageStackProbe -or
    -not $EnableM01LiveAllocatorProbe -or
    -not $EnableM04IndependentEndianConformanceProbe -or
    -not $EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe -or
    -not $EnableM04ArithmeticConformanceProbe -or
    -not $EnableM04LayoutContractProbe)) {
    Write-Failure "m04_managed_heap_page_range_requires_explicit_chain"
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
    Write-Failure "m04_managed_heap_page_range_forbids_optional_profiles"
}

if ($EnableM04LayoutContractProbe -and
    (-not $EnableM04ArithmeticConformanceProbe -or
    -not $EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe -or
    -not $EnableM04IndependentEndianConformanceProbe)) {
    Write-Failure "m04_layout_contract_requires_explicit_m04_chain"
}
if ($EnableM04LayoutContractProbe -and
    $EnableM02OwnedGdtTssTerminalProbe) {
    Write-Failure "m04_layout_contract_forbids_owned_gdt_tss_terminal"
}
if ($EnableM04LayoutContractProbe -and
    $EnableM02Ud2VectorStateProbe) {
    Write-Failure "m04_layout_contract_forbids_m02_ud2_vector_state"
}

if ($EnableM04ArithmeticConformanceProbe -and
    -not $EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe) {
    Write-Failure "m04_arithmetic_conformance_requires_span_memory_conformance"
}
if ($EnableM04ArithmeticConformanceProbe -and
    $EnableM02OwnedGdtTssTerminalProbe) {
    Write-Failure "m04_arithmetic_conformance_forbids_owned_gdt_tss_terminal"
}
if ($EnableM04ArithmeticConformanceProbe -and
    $EnableM02Ud2VectorStateProbe) {
    Write-Failure "m04_arithmetic_conformance_forbids_m02_ud2_vector_state"
}

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
    foreach ($prerequisiteName in $m02Ud2VectorStatePrerequisites) {
        if ($PSBoundParameters.ContainsKey($prerequisiteName) -and
            -not [bool]$PSBoundParameters[$prerequisiteName]) {
            Write-Failure `
                "m02_ud2_vector_state_probe_explicit_false_prerequisite" `
                ("property=" + $prerequisiteName)
        }
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
    Write-Output (
        "EFI_ARTIFACT_M02_UD2_VECTOR_STATE_IMPLICATION status=ok " +
        "m01LoadedImageStack=true m01LiveAllocator=true ownedPaging=true " +
        "m02SafeStub=true checkedPortIoBinding=true com1OwnedPortIo=true " +
        "ownedCom1Ready=true panicSafeOwnedCom1=true")
}
if ($EnableM02PageFaultCr2ErrorCodeProbe) {
    foreach ($conflict in @(
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
            throw ("m02_page_fault_cr2_error_code_profile_conflict " + $conflict)
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
    foreach ($prerequisiteName in $m02PageFaultCr2ErrorCodePrerequisites) {
        if ($PSBoundParameters.ContainsKey($prerequisiteName) -and
            -not [bool]$PSBoundParameters[$prerequisiteName]) {
            Write-Failure `
                "m02_page_fault_cr2_error_code_probe_explicit_false_prerequisite" `
                ("property=" + $prerequisiteName)
        }
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
    Write-Output (
        "EFI_ARTIFACT_M02_PAGE_FAULT_CR2_ERROR_CODE_IMPLICATION status=ok " +
        "m01LoadedImageStack=true m01LiveAllocator=true ownedPaging=true " +
        "m02SafeStub=true checkedPortIoBinding=true com1OwnedPortIo=true " +
        "ownedCom1Ready=true panicSafeOwnedCom1=true")
}

if ($EnableM02NormalizedInterruptFramePageProbe -and
    $EnableM02OwnedGdtTssTerminalProbe) {
    Write-Failure "m02_normalized_interrupt_frame_page_probe_forbids_owned_gdt_tss_terminal"
}

if ($EnableM02OwnedIdtGatePopulationProbe -and
    $EnableM02OwnedGdtTssTerminalProbe) {
    Write-Failure "m02_owned_idt_gate_population_probe_forbids_owned_gdt_tss_terminal"
}

if ($EnableM02OwnedIdtGatePopulationProbe -and
    $EnableM02NormalizedInterruptFramePageProbe) {
    Write-Failure "m02_owned_idt_gate_population_probe_forbids_normalized_interrupt_frame_page_probe"
}

if ($EnableM02IdtrContinuedExecutionProbe -and
    $EnableM02OwnedGdtTssTerminalProbe) {
    Write-Failure "m02_idtr_continued_execution_probe_forbids_owned_gdt_tss_terminal"
}

if ($EnableM02IdtrContinuedExecutionProbe -and
    $EnableM02NormalizedInterruptFramePageProbe) {
    Write-Failure "m02_idtr_continued_execution_probe_forbids_normalized_interrupt_frame_page_probe"
}

if ($EnableM02IdtrContinuedExecutionProbe -and
    $EnableM02OwnedIdtGatePopulationProbe) {
    Write-Failure "m02_idtr_continued_execution_probe_forbids_owned_idt_gate_population_probe"
}

if ($EnableM02Ud2VectorStateProbe -and
    -not $EnableM02PanicSafeOwnedCom1Probe) {
    Write-Failure "m02_ud2_vector_state_probe_requires_panic_safe_owned_com1"
}

if ($EnableM02Ud2VectorStateProbe -and
    $EnableM02OwnedX64GdtEncodingProbe) {
    Write-Failure "m02_ud2_vector_state_probe_forbids_owned_x64_gdt_encoding_probe"
}

if ($EnableM02Ud2VectorStateProbe -and
    $EnableM02GuardedIstStackAllocationProbe) {
    Write-Failure "m02_ud2_vector_state_probe_forbids_guarded_ist_stack_allocation_probe"
}

if ($EnableM02Ud2VectorStateProbe -and
    $EnableM02OwnedGdtTssTerminalProbe) {
    Write-Failure "m02_ud2_vector_state_probe_forbids_owned_gdt_tss_terminal"
}

if ($EnableM02Ud2VectorStateProbe -and
    $EnableM02NormalizedInterruptFramePageProbe) {
    Write-Failure "m02_ud2_vector_state_probe_forbids_normalized_interrupt_frame_page_probe"
}

if ($EnableM02Ud2VectorStateProbe -and
    $EnableM02OwnedIdtGatePopulationProbe) {
    Write-Failure "m02_ud2_vector_state_probe_forbids_owned_idt_gate_population_probe"
}

if ($EnableM02Ud2VectorStateProbe -and
    $EnableM02IdtrContinuedExecutionProbe) {
    Write-Failure "m02_ud2_vector_state_probe_forbids_idtr_continued_execution_probe"
}

if ($EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe -and
    -not $EnableM04IndependentEndianConformanceProbe) {
    Write-Failure "m04_span_memory_conformance_requires_independent_endian_conformance"
}
if ($EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe -and
    $EnableM02OwnedGdtTssTerminalProbe) {
    Write-Failure "m04_span_memory_conformance_forbids_owned_gdt_tss_terminal"
}
if ($EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe -and
    $EnableM02Ud2VectorStateProbe) {
    Write-Failure "m04_span_memory_conformance_forbids_m02_ud2_vector_state"
}

if (-not [System.BitConverter]::IsLittleEndian) {
    Write-Failure "unsupported_host_endianness"
}

$artifactPath = Get-AbsolutePath $EfiArtifact "artifact_path_invalid"
$projectFullPath = Resolve-PinnedRepositoryContractPath `
    -SuppliedPath $ProjectPath `
    -ExpectedRelativePath "src/OS/OS.csproj" `
    -Reason "project_path_not_canonical_repository_contract"
$entrySourceFullPath = Resolve-PinnedRepositoryContractPath `
    -SuppliedPath $EntrySourcePath `
    -ExpectedRelativePath "src/OS/Boot/EfiEntry.cs" `
    -Reason "entry_source_path_not_canonical_repository_contract"
if ([string]::IsNullOrWhiteSpace($LinkerMapPath)) {
    $linkerMapFullPath = $artifactPath + ".link.map"
}
else {
    $linkerMapFullPath = Get-AbsolutePath $LinkerMapPath "linker_map_path_invalid"
}
if ([string]::IsNullOrWhiteSpace($LinkResponsePath)) {
    $linkResponseFullPath = $artifactPath + ".link.rsp"
}
else {
    $linkResponseFullPath = Get-AbsolutePath $LinkResponsePath "link_response_path_invalid"
}
if ([string]::IsNullOrWhiteSpace($LinkEvidencePath)) {
    $linkEvidenceFullPath = $artifactPath + ".link.json"
}
else {
    $linkEvidenceFullPath = Get-AbsolutePath $LinkEvidencePath "link_evidence_path_invalid"
}

if (-not (Test-Path -LiteralPath $artifactPath -PathType Leaf)) {
    Write-Failure "missing_artifact" "path=$artifactPath"
}
if (-not (Test-Path -LiteralPath $projectFullPath -PathType Leaf)) {
    Write-Failure "missing_build_contract" "path=$projectFullPath"
}
if (-not (Test-Path -LiteralPath $entrySourceFullPath -PathType Leaf)) {
    Write-Failure "missing_entry_source_contract" "path=$entrySourceFullPath"
}
foreach ($linkerEvidenceInput in @(
    [pscustomobject]@{
        Path = $linkerMapFullPath
        Reason = "missing_linker_map"
    },
    [pscustomobject]@{
        Path = $linkResponseFullPath
        Reason = "missing_link_response"
    },
    [pscustomobject]@{
        Path = $linkEvidenceFullPath
        Reason = "missing_link_evidence"
    }
)) {
    if (-not (Test-Path -LiteralPath $linkerEvidenceInput.Path -PathType Leaf)) {
        Write-Failure $linkerEvidenceInput.Reason (
            "path=$($linkerEvidenceInput.Path)")
    }
}

$artifactAuthority = Open-AuthorityReadHandle `
    -Path $artifactPath `
    -RootPath (Get-AuthorityRootPath -Path $artifactPath) `
    -Purpose "artifact" `
    -FailurePrefix "artifact" `
    -MaximumBytes 536870912L `
    -AllowEmpty `
    -RequireSingleLink
$linkerMapAuthority = Open-AuthorityReadHandle `
    -Path $linkerMapFullPath `
    -RootPath (Get-AuthorityRootPath -Path $linkerMapFullPath) `
    -Purpose "linker-map" `
    -FailurePrefix "linker_map" `
    -MaximumBytes 16777216L `
    -RequireSingleLink
$linkResponseAuthority = Open-AuthorityReadHandle `
    -Path $linkResponseFullPath `
    -RootPath (Get-AuthorityRootPath -Path $linkResponseFullPath) `
    -Purpose "link-response" `
    -FailurePrefix "link_response" `
    -MaximumBytes 1048576L `
    -RequireSingleLink
$linkEvidenceAuthority = Open-AuthorityReadHandle `
    -Path $linkEvidenceFullPath `
    -RootPath (Get-AuthorityRootPath -Path $linkEvidenceFullPath) `
    -Purpose "link-evidence" `
    -FailurePrefix "link_evidence" `
    -MaximumBytes 1048576L `
    -RequireSingleLink
$projectAuthority = Open-AuthorityReadHandle `
    -Path $projectFullPath `
    -RootPath $repositoryRoot `
    -Purpose "source-project" `
    -FailurePrefix "source_project" `
    -MaximumBytes $maximumAuthorityTextBytes `
    -RequireSingleLink
$entrySourceAuthority = Open-AuthorityReadHandle `
    -Path $entrySourceFullPath `
    -RootPath $repositoryRoot `
    -Purpose "entry-source" `
    -FailurePrefix "entry_source" `
    -MaximumBytes $maximumAuthorityTextBytes `
    -RequireSingleLink
Assert-DistinctAuthorityFiles -Records @(
    $projectAuthority,
    $entrySourceAuthority
)

try {
    [byte[]]$bytes = Read-AuthorityBytes `
        -Record $artifactAuthority `
        -MaximumBytes 536870912L `
        -Reason "artifact_read_failed"
}
catch {
    Write-Failure "artifact_read_failed" "path=$artifactPath"
}
$artifactLength = [long]$artifactAuthority.InitialLength

if ($bytes.LongLength -eq 0) {
    Write-Failure "empty_artifact"
}
Assert-ByteRange 0 64 $bytes.LongLength "truncated_dos_header"
if ($bytes[0] -ne 0x4d -or $bytes[1] -ne 0x5a) {
    Write-Failure "invalid_dos_signature"
}

$peOffset = [long](Read-UInt32LittleEndian $bytes 0x3c "truncated_dos_header")
if ($peOffset -lt 64) {
    Write-Failure "invalid_pe_offset" "offset=$peOffset"
}

Assert-ByteRange $peOffset 24 $bytes.LongLength "truncated_pe_header"
if ($bytes[$peOffset] -ne 0x50 -or
    $bytes[$peOffset + 1] -ne 0x45 -or
    $bytes[$peOffset + 2] -ne 0 -or
    $bytes[$peOffset + 3] -ne 0) {
    Write-Failure "invalid_pe_signature"
}

$coffHeaderOffset = $peOffset + 4L
$machine = Read-UInt16LittleEndian $bytes $coffHeaderOffset "truncated_coff_header"
$sectionCount = Read-UInt16LittleEndian $bytes ($coffHeaderOffset + 2L) "truncated_coff_header"
$coffTimestamp = Read-UInt32LittleEndian $bytes ($coffHeaderOffset + 4L) "truncated_coff_header"
$optionalHeaderSize = Read-UInt16LittleEndian $bytes ($coffHeaderOffset + 16L) "truncated_coff_header"

if ($machine -ne 0x8664) {
    Write-Failure "machine_not_x64" ("expected=0x8664 actual=0x{0:x4}" -f $machine)
}

if ($sectionCount -eq 0 -or $sectionCount -gt 96) {
    Write-Failure "section_count_out_of_range" "count=$sectionCount"
}

if ($optionalHeaderSize -lt 112) {
    Write-Failure "optional_header_too_small" "size=$optionalHeaderSize"
}

$optionalHeaderOffset = $coffHeaderOffset + 20L
Assert-ByteRange $optionalHeaderOffset $optionalHeaderSize $bytes.LongLength "truncated_optional_header"

$optionalMagic = Read-UInt16LittleEndian $bytes $optionalHeaderOffset "truncated_optional_header"
if ($optionalMagic -ne 0x20b) {
    Write-Failure "format_not_pe32_plus" ("expected=0x020b actual=0x{0:x4}" -f $optionalMagic)
}

$entryPointRva = Read-UInt32LittleEndian $bytes ($optionalHeaderOffset + 16L) "truncated_optional_header"
if ($entryPointRva -eq 0) {
    Write-Failure "entrypoint_zero"
}
$imageBase = Read-UInt64LittleEndian $bytes ($optionalHeaderOffset + 24L) "truncated_optional_header"
if ($imageBase -eq 0) {
    Write-Failure "image_base_zero"
}
$sectionAlignment = Read-UInt32LittleEndian $bytes ($optionalHeaderOffset + 32L) "truncated_optional_header"
$fileAlignment = Read-UInt32LittleEndian $bytes ($optionalHeaderOffset + 36L) "truncated_optional_header"
$sizeOfImage = Read-UInt32LittleEndian $bytes ($optionalHeaderOffset + 56L) "truncated_optional_header"
if ($sizeOfImage -eq 0) {
    Write-Failure "size_of_image_zero"
}
$sizeOfHeaders = Read-UInt32LittleEndian $bytes ($optionalHeaderOffset + 60L) "truncated_optional_header"
if ($sectionAlignment -eq 0 -or
    $fileAlignment -eq 0 -or
    $sizeOfHeaders -eq 0 -or
    $sizeOfHeaders -gt $bytes.LongLength -or
    $sizeOfHeaders -gt $sizeOfImage) {
    Write-Failure "pe_layout_bounds_invalid" (
        "sectionAlignment={0} fileAlignment={1} sizeOfHeaders={2} sizeOfImage={3}" -f
        $sectionAlignment,
        $fileAlignment,
        $sizeOfHeaders,
        $sizeOfImage)
}
if ([uint64]$sizeOfImage -gt ([uint64]::MaxValue - $imageBase)) {
    Write-Failure "image_virtual_range_overflow"
}
if ($entryPointRva -ge $sizeOfImage) {
    Write-Failure "entrypoint_out_of_image" (
        "rva=0x{0:x8} sizeOfImage=0x{1:x8}" -f
        $entryPointRva,
        $sizeOfImage)
}

$subsystem = Read-UInt16LittleEndian $bytes ($optionalHeaderOffset + 68L) "truncated_optional_header"
if ($subsystem -ne 10) {
    Write-Failure "subsystem_not_efi_application" "expected=10 actual=$subsystem"
}

$directoryCount = Read-UInt32LittleEndian $bytes ($optionalHeaderOffset + 108L) "truncated_optional_header"
$maximumDirectoryCount = [uint32](($optionalHeaderSize - 112) / 8)
if ($directoryCount -gt $maximumDirectoryCount) {
    Write-Failure "data_directory_count_out_of_range" "declared=$directoryCount available=$maximumDirectoryCount"
}

if ($directoryCount -le 3) {
    Write-Failure "exception_directory_missing" "directoryCount=$directoryCount"
}

$exceptionDirectory = Read-DataDirectory `
    $bytes `
    $optionalHeaderOffset `
    $optionalHeaderSize `
    $directoryCount `
    3 `
    "exception"

if ($exceptionDirectory.Rva -eq 0 -or $exceptionDirectory.Size -eq 0) {
    Write-Failure "exception_directory_zero" (
        "rva=0x{0:x8} size={1}" -f $exceptionDirectory.Rva, $exceptionDirectory.Size)
}
if (($exceptionDirectory.Rva -band 3) -ne 0) {
    Write-Failure "exception_directory_rva_misaligned" (
        "rva=0x{0:x8}" -f $exceptionDirectory.Rva)
}
if (($exceptionDirectory.Size % 12) -ne 0) {
    Write-Failure "exception_directory_size_not_record_aligned" "size=$($exceptionDirectory.Size)"
}
if (($exceptionDirectory.Size / 12) -gt $maximumRuntimeFunctionRecords) {
    Write-Failure "exception_directory_record_count_out_of_range" (
        "records=$($exceptionDirectory.Size / 12) maximum=$maximumRuntimeFunctionRecords")
}
$exceptionDirectoryEnd = Get-RvaRangeEnd `
    ([uint64]$exceptionDirectory.Rva) `
    ([uint64]$exceptionDirectory.Size) `
    "exception_directory_range_invalid"
if ($exceptionDirectoryEnd -gt [uint64]$sizeOfImage) {
    Write-Failure "exception_directory_out_of_image" (
        "rva=0x{0:x8} size={1} sizeOfImage=0x{2:x8}" -f
        $exceptionDirectory.Rva,
        $exceptionDirectory.Size,
        $sizeOfImage)
}

$importDirectories = @(
    (Read-DataDirectory $bytes $optionalHeaderOffset $optionalHeaderSize $directoryCount 1 "import"),
    (Read-DataDirectory $bytes $optionalHeaderOffset $optionalHeaderSize $directoryCount 11 "bound-import"),
    (Read-DataDirectory $bytes $optionalHeaderOffset $optionalHeaderSize $directoryCount 12 "iat"),
    (Read-DataDirectory $bytes $optionalHeaderOffset $optionalHeaderSize $directoryCount 13 "delay-import")
)

foreach ($directory in $importDirectories) {
    if (($directory.Rva -eq 0) -ne ($directory.Size -eq 0)) {
        Write-Failure "malformed_import_directory" "directory=$($directory.Name)"
    }

    if ($directory.Rva -ne 0 -or $directory.Size -ne 0) {
        Write-Failure "ordinary_runtime_imports_present" (
            "directory={0} rva=0x{1:x8} size={2}" -f
            $directory.Name,
            $directory.Rva,
            $directory.Size)
    }
}

$sectionTableOffset = $optionalHeaderOffset + $optionalHeaderSize
$sectionTableSize = [long]$sectionCount * 40L
Assert-ByteRange $sectionTableOffset $sectionTableSize $bytes.LongLength "truncated_section_table"
if ($sectionTableOffset -gt [long]$sizeOfHeaders -or
    $sectionTableSize -gt ([long]$sizeOfHeaders - $sectionTableOffset)) {
    Write-Failure "section_table_outside_headers"
}

$entrySectionCount = 0
$entrySectionName = ""
$entrySectionIsExecutable = $false
$entryPointIsFileBacked = $false
$entryPointFileOffset = [uint64]0
$sections = @()

for ($index = 0; $index -lt $sectionCount; $index++) {
    $sectionOffset = $sectionTableOffset + ([long]$index * 40L)
    [byte[]]$nameBytes = New-Object byte[] 8
    [System.Array]::Copy($bytes, [int]$sectionOffset, $nameBytes, 0, 8)
    $sectionName = [System.Text.Encoding]::ASCII.GetString($nameBytes).TrimEnd([char]0)

    $virtualSize = Read-UInt32LittleEndian $bytes ($sectionOffset + 8L) "truncated_section_table"
    $virtualAddress = Read-UInt32LittleEndian $bytes ($sectionOffset + 12L) "truncated_section_table"
    $rawSize = Read-UInt32LittleEndian $bytes ($sectionOffset + 16L) "truncated_section_table"
    $rawOffset = Read-UInt32LittleEndian $bytes ($sectionOffset + 20L) "truncated_section_table"
    $characteristics = Read-UInt32LittleEndian $bytes ($sectionOffset + 36L) "truncated_section_table"

    if ($rawSize -ne 0) {
        if ($rawOffset -lt $sizeOfHeaders) {
            Write-Failure "section_raw_range_overlaps_headers" (
                "section=$sectionName rawOffset=$rawOffset sizeOfHeaders=$sizeOfHeaders")
        }
        Assert-ByteRange ([long]$rawOffset) ([long]$rawSize) $bytes.LongLength "section_raw_data_out_of_range"
    }

    $mappedSize = [uint64][System.Math]::Max([uint64]$virtualSize, [uint64]$rawSize)
    if ($mappedSize -ne 0) {
        $rangeStart = [uint64]$virtualAddress
        $rangeEnd = $rangeStart + $mappedSize
        if ($rangeEnd -gt [uint64]4294967296) {
            Write-Failure "section_virtual_range_overflow" "section=$sectionName"
        }
        if ($rangeStart -lt [uint64]$sizeOfHeaders) {
            Write-Failure "section_virtual_range_overlaps_headers" (
                "section=$sectionName virtualAddress=0x{0:x8} sizeOfHeaders=0x{1:x8}" -f
                $virtualAddress,
                $sizeOfHeaders)
        }
        if ($rangeEnd -gt [uint64]$sizeOfImage) {
            Write-Failure "section_virtual_range_out_of_image" (
                "section={0} end=0x{1:x8} sizeOfImage=0x{2:x8}" -f
                $sectionName,
                $rangeEnd,
                $sizeOfImage)
        }
    }

    $section = [pscustomobject]@{
        Index = $index
        Name = $sectionName
        VirtualSize = $virtualSize
        VirtualAddress = $virtualAddress
        RawSize = $rawSize
        RawOffset = $rawOffset
        Characteristics = $characteristics
        MappedSize = $mappedSize
    }
    $sections += $section

    if ($virtualSize -eq 0) {
        continue
    }

    $entry = [uint64]$entryPointRva
    $rangeStart = [uint64]$virtualAddress
    $rangeEnd = $rangeStart + [uint64]$virtualSize
    if ($entry -ge $rangeStart -and $entry -lt $rangeEnd) {
        $entrySectionCount++
        $entrySectionName = $sectionName
        $entrySectionIsExecutable = ($characteristics -band 0x20000000) -ne 0

        $entryDelta = $entry - $rangeStart
        if ($entryDelta -lt [uint64]$rawSize) {
            $entryFileOffset = [uint64]$rawOffset + $entryDelta
            $entryPointIsFileBacked = $entryFileOffset -lt [uint64]$bytes.LongLength
            $entryPointFileOffset = $entryFileOffset
        }
    }
}

for ($leftIndex = 0; $leftIndex -lt $sections.Count; $leftIndex++) {
    $leftSection = $sections[$leftIndex]
    for ($rightIndex = $leftIndex + 1; $rightIndex -lt $sections.Count; $rightIndex++) {
        $rightSection = $sections[$rightIndex]

        if ([uint64]$leftSection.MappedSize -ne 0 -and
            [uint64]$rightSection.MappedSize -ne 0) {
            $leftVirtualStart = [uint64]$leftSection.VirtualAddress
            $leftVirtualEnd = $leftVirtualStart + [uint64]$leftSection.MappedSize
            $rightVirtualStart = [uint64]$rightSection.VirtualAddress
            $rightVirtualEnd = $rightVirtualStart + [uint64]$rightSection.MappedSize
            if ($leftVirtualStart -lt $rightVirtualEnd -and
                $rightVirtualStart -lt $leftVirtualEnd) {
                Write-Failure "section_virtual_ranges_overlap" (
                    "left={0} right={1}" -f
                    $leftSection.Name,
                    $rightSection.Name)
            }
        }

        if ([uint64]$leftSection.RawSize -ne 0 -and
            [uint64]$rightSection.RawSize -ne 0) {
            $leftRawStart = [uint64]$leftSection.RawOffset
            $leftRawEnd = $leftRawStart + [uint64]$leftSection.RawSize
            $rightRawStart = [uint64]$rightSection.RawOffset
            $rightRawEnd = $rightRawStart + [uint64]$rightSection.RawSize
            if ($leftRawStart -lt $rightRawEnd -and
                $rightRawStart -lt $leftRawEnd) {
                Write-Failure "section_raw_ranges_overlap" (
                    "left={0} right={1}" -f
                    $leftSection.Name,
                    $rightSection.Name)
            }
        }
    }
}

if ($entrySectionCount -eq 0) {
    Write-Failure "entrypoint_outside_sections" ("rva=0x{0:x8}" -f $entryPointRva)
}
if ($entrySectionCount -ne 1) {
    Write-Failure "entrypoint_section_ambiguous" (
        "rva=0x{0:x8} matches={1}" -f $entryPointRva, $entrySectionCount)
}
if (-not $entrySectionIsExecutable) {
    Write-Failure "entrypoint_section_not_executable" "section=$entrySectionName"
}
if (-not $entryPointIsFileBacked) {
    Write-Failure "entrypoint_not_file_backed" "section=$entrySectionName"
}

$entryPointFirstByte = $bytes[[int]$entryPointFileOffset]
if ($entryPointFirstByte -in @(0x00, 0xc3, 0xcb, 0xcc)) {
    Write-Failure "entrypoint_trivial_stub" ("byte=0x{0:x2}" -f $entryPointFirstByte)
}

$exceptionBacking = Resolve-FileBackedRvaRange `
    $sections `
    ([uint64]$exceptionDirectory.Rva) `
    ([uint64]$exceptionDirectory.Size) `
    $bytes.LongLength `
    "exception_directory_not_file_backed" `
    "exception_directory_section_ambiguous"
Assert-ReadOnlyDataSectionCharacteristics `
    $exceptionBacking.Section `
    "exception_directory_section_characteristics_invalid"

if (-not (Test-Path -LiteralPath $projectFullPath -PathType Leaf)) {
    Write-Failure "missing_build_contract" "path=$projectFullPath"
}
if (-not (Test-Path -LiteralPath $entrySourceFullPath -PathType Leaf)) {
    Write-Failure "missing_entry_source_contract" "path=$entrySourceFullPath"
}

try {
    $projectText = Read-AuthorityUtf8Text `
        -Record $projectAuthority `
        -MaximumBytes $maximumAuthorityTextBytes `
        -Reason "entry_contract_read_failed"
    $entrySourceText = Read-AuthorityUtf8Text `
        -Record $entrySourceAuthority `
        -MaximumBytes $maximumAuthorityTextBytes `
        -Reason "entry_contract_read_failed"
}
catch {
    Write-Failure "entry_contract_read_failed"
}

if ($projectText -notmatch '<EntryPointSymbol>\s*EfiMain\s*</EntryPointSymbol>') {
    Write-Failure "efimain_project_contract_missing" "contract=EntryPointSymbol"
}
if ($projectText -notmatch '<LinkerSubsystem>\s*EFI_APPLICATION\s*</LinkerSubsystem>') {
    Write-Failure "efimain_project_contract_missing" "contract=LinkerSubsystem"
}
if ($projectText -notmatch '/ENTRY:\$\(EntryPointSymbol\)') {
    Write-Failure "efimain_project_contract_missing" "contract=linker-entry"
}
if ($projectText -notmatch '/NODEFAULTLIB') {
    Write-Failure "efimain_project_contract_missing" "contract=no-default-library"
}
if ($entrySourceText -notmatch 'RuntimeExport\s*\(\s*"EfiMain"\s*\)') {
    Write-Failure "efimain_source_contract_missing" "contract=RuntimeExport"
}

foreach ($linkerEvidenceInput in @(
    [pscustomobject]@{
        Record = $linkerMapAuthority
        MaximumBytes = 16777216L
    },
    [pscustomobject]@{
        Record = $linkResponseAuthority
        MaximumBytes = 1048576L
    },
    [pscustomobject]@{
        Record = $linkEvidenceAuthority
        MaximumBytes = 1048576L
    }
)) {
    $evidenceLength = [long]$linkerEvidenceInput.Record.InitialLength
    if ($evidenceLength -eq 0 -or $evidenceLength -gt $linkerEvidenceInput.MaximumBytes) {
        Write-Failure "link_evidence_size_out_of_range" (
            "path={0} bytes={1}" -f
            $linkerEvidenceInput.Record.Path,
            $evidenceLength)
    }
}

try {
    $linkerMapText = Read-AuthorityUtf8Text `
        -Record $linkerMapAuthority `
        -MaximumBytes 16777216L `
        -Reason "link_evidence_read_failed"
    $linkResponseText = Read-AuthorityUtf8Text `
        -Record $linkResponseAuthority `
        -MaximumBytes 1048576L `
        -Reason "link_evidence_read_failed"
    $linkEvidenceText = Read-AuthorityUtf8Text `
        -Record $linkEvidenceAuthority `
        -MaximumBytes 1048576L `
        -Reason "link_evidence_read_failed"
    $linkEvidence = $linkEvidenceText | ConvertFrom-Json
}
catch {
    Write-Failure "link_evidence_read_failed" "type=$($_.Exception.GetType().Name)"
}

$linkResponseValidationStopwatch =
    [System.Diagnostics.Stopwatch]::StartNew()
$linkResponseTokens = @(Get-LinkResponseTokens $linkResponseText)
$breproOptionCount = 0
$malformedBreproOptionCount = 0
foreach ($linkResponseToken in $linkResponseTokens) {
    if (-not [string]::IsNullOrEmpty($linkResponseToken) -and
        $linkResponseToken[0] -eq '@') {
        Write-Failure "nested_link_response_file_forbidden" (
            "token=$linkResponseToken")
    }
    if (-not [string]::IsNullOrEmpty($linkResponseToken) -and
        $linkResponseToken[0] -eq '-') {
        Write-Failure "noncanonical_link_option_prefix" (
            "token=$linkResponseToken")
    }
    if ($linkResponseToken -match '(?i)^/FORCE(?:$|:)') {
        Write-Failure "force_linkage_forbidden" (
            "token=$linkResponseToken")
    }
    if ($linkResponseToken -match '(?i)^/BREPRO$') {
        $breproOptionCount++
    }
    elseif ($linkResponseToken -match '(?i)^/BREPRO[:=]') {
        $malformedBreproOptionCount++
    }
}
if ($breproOptionCount -ne 1 -or $malformedBreproOptionCount -ne 0) {
    Write-Failure "brepro_link_response_contract_invalid" (
        "matches=$breproOptionCount malformed=$malformedBreproOptionCount")
}
$linkResponseValidationStopwatch.Stop()

$requiredEvidenceProperties = @(
    "schemaVersion",
    "configuration",
    "runtimeIdentifier",
    "artifactSha256",
    "artifactBytes",
    "linkerMapSha256",
    "linkResponseSha256",
    "solutionSha256",
    "sourceClosureSha256",
    "sourceClosureInputs",
    "restoreClosureSha256",
    "restoreClosureInputs",
    "restoreArchiveSourceRootPath",
    "restoreArchiveClosureSha256",
    "restoreArchiveInputs",
    "pinnedRestoreSourcePath",
    "pinnedRestoreSourceSha256",
    "pinnedRestoreSourceInputs",
    "restoreImportProjectExtensionProps",
    "restoreImportProjectExtensionTargets",
    "restoreExcludeRestorePackageImports",
    "publishImportProjectExtensionProps",
    "publishImportProjectExtensionTargets",
    "publishExcludeRestorePackageImports",
    "projectExtensionsPath",
    "projectAssetsPath",
    "projectAssetsSha256",
    "packageRootPath",
    "packageRootKind",
    "packageClosureSha256",
    "packages",
    "packageBuildClosureSha256",
    "packageBuildClosureInputs",
    "activePackageImportEdges",
    "activePackageImportGraphSha256",
    "packageTaskBindings",
    "packageTaskBindingsSha256",
    "postRestoreEvaluation",
    "sourceProjectSha256",
    "entrySourceSha256",
    "entryPointSymbol",
    "managedEntrySymbol",
    "serializerSymbol",
    "serializerExpectedSuccessCookie",
    "serializerLinkedObjectPath",
    "serializerObjectPath",
    "serializerObjectBytes",
    "serializerObjectSha256",
    "serializerTargetsSha256",
    "serializerGeneratorProjectSha256",
    "serializerGeneratorProgramSha256",
    "serializerGeneratorWriterSha256",
    "serializerGeneratorSourceSha256",
    "instructionPublicationSourceSha256",
    "runtimeImportAttributeSourceSha256",
    "methodImplAttributeSourceSha256",
    "managedAssemblyPath",
    "managedAssemblySha256",
    "linkerPath",
    "linkerVersion",
    "linkerSha256",
    "dotnetHostPath",
    "dotnetHostSha256",
    "dotnetSdkVersion",
    "windowsSdkLibraryPath",
    "windowsSdkVersion",
    "windowsSdkLibrarySha256",
    "ilCompilerPath",
    "ilCompilerDiscoverySourcePath",
    "ilCompilerPackageVersion",
    "ilCompilerFileVersion",
    "ilCompilerSha256",
    "nativeAotRuntimePackPath",
    "nativeAotRuntimePackVersion",
    "nativeAotRuntimePackSha256",
    "nativeAotRuntimePackFileCount",
    "nativeAotRuntimePackBytes",
    "ilCompilerTargetsPath",
    "ilLinkTargetsPath",
    "ilLinkTasksAssemblyPath",
    "ilcBuildTasksPath",
    "ilcHostPackagePath",
    "ilcToolsPath",
    "ilcFrameworkPath",
    "ilcFrameworkNativePath",
    "ilcSdkPath",
    "ilcMibcPath",
    "languageTargetsPath",
    "csharpTargetsPath",
    "commonTargetsPath",
    "runtimeListSha256",
    "generatedUtc"
)
foreach ($propertyName in $requiredEvidenceProperties) {
    if ($null -eq $linkEvidence.PSObject.Properties[$propertyName]) {
        Write-Failure "link_evidence_property_missing" "property=$propertyName"
    }
}
Assert-ExactPropertyNameSet `
    -Value $linkEvidence `
    -ExpectedNames $requiredEvidenceProperties `
    -Reason "link_evidence_property_set_invalid"

$schemaVersion = Convert-EvidenceInt64 $linkEvidence.schemaVersion "schemaVersion"
$evidenceSuccessCookie = Convert-EvidenceInt64 `
    $linkEvidence.serializerExpectedSuccessCookie `
    "serializerExpectedSuccessCookie"
if (($linkEvidence.schemaVersion -isnot [int] -and
     $linkEvidence.schemaVersion -isnot [long]) -or
    ($linkEvidence.serializerExpectedSuccessCookie -isnot [int] -and
     $linkEvidence.serializerExpectedSuccessCookie -isnot [long]) -or
    $schemaVersion -ne 3 -or
    [string]$linkEvidence.entryPointSymbol -cne "EfiMain" -or
    [string]$linkEvidence.managedEntrySymbol -cne "OS_OS_Boot_EfiEntry__EfiMain" -or
    [string]$linkEvidence.serializerSymbol -cne $serializerSymbol -or
    $evidenceSuccessCookie -ne $serializerExpectedSuccessCookie) {
    Write-Failure "link_evidence_contract_invalid"
}
foreach ($booleanContract in @(
        [pscustomobject]@{
            Name = "restoreImportProjectExtensionProps"
            Expected = $false
        },
        [pscustomobject]@{
            Name = "restoreImportProjectExtensionTargets"
            Expected = $false
        },
        [pscustomobject]@{
            Name = "restoreExcludeRestorePackageImports"
            Expected = $true
        },
        [pscustomobject]@{
            Name = "publishImportProjectExtensionProps"
            Expected = $false
        },
        [pscustomobject]@{
            Name = "publishImportProjectExtensionTargets"
            Expected = $false
        },
        [pscustomobject]@{
            Name = "publishExcludeRestorePackageImports"
            Expected = $true
        })) {
    $booleanValue = $linkEvidence.($booleanContract.Name)
    if ($booleanValue -isnot [bool] -or
        [bool]$booleanValue -ne [bool]$booleanContract.Expected) {
        Write-Failure "link_evidence_import_boolean_contract_invalid" (
            "property=$($booleanContract.Name)")
    }
}

$sha256 = [string]$artifactAuthority.InitialSha256
$linkerMapSha256 = [string]$linkerMapAuthority.InitialSha256
$linkResponseSha256 = [string]$linkResponseAuthority.InitialSha256
$projectSha256 = [string]$projectAuthority.InitialSha256
$entrySourceSha256 = [string]$entrySourceAuthority.InitialSha256
$solutionFullPath = Get-AbsolutePath "CLROS.slnx" "solution_path_invalid"
if (-not (Test-Path -LiteralPath $solutionFullPath -PathType Leaf)) {
    Write-Failure "solution_contract_missing" "path=$solutionFullPath"
}
$solutionAuthority = Open-AuthorityReadHandle `
    -Path $solutionFullPath `
    -RootPath $repositoryRoot `
    -Purpose "solution" `
    -FailurePrefix "solution" `
    -MaximumBytes $maximumAuthorityTextBytes
$solutionSha256 = [string]$solutionAuthority.InitialSha256

$hashProperties = @(
    "artifactSha256",
    "linkerMapSha256",
    "linkResponseSha256",
    "sourceProjectSha256",
    "entrySourceSha256",
    "solutionSha256",
    "sourceClosureSha256",
    "restoreClosureSha256",
    "restoreArchiveClosureSha256",
    "pinnedRestoreSourceSha256",
    "projectAssetsSha256",
    "packageClosureSha256",
    "packageBuildClosureSha256",
    "activePackageImportGraphSha256",
    "packageTaskBindingsSha256",
    "linkerSha256",
    "serializerObjectSha256",
    "serializerTargetsSha256",
    "serializerGeneratorProjectSha256",
    "serializerGeneratorProgramSha256",
    "serializerGeneratorWriterSha256",
    "serializerGeneratorSourceSha256",
    "instructionPublicationSourceSha256",
    "runtimeImportAttributeSourceSha256",
    "methodImplAttributeSourceSha256",
    "managedAssemblySha256",
    "dotnetHostSha256",
    "windowsSdkLibrarySha256",
    "ilCompilerSha256",
    "nativeAotRuntimePackSha256",
    "runtimeListSha256"
)
foreach ($hashProperty in $hashProperties) {
    $hashValue = [string]$linkEvidence.$hashProperty
    if ($hashValue -cnotmatch '^[0-9a-f]{64}$') {
        Write-Failure "link_evidence_hash_malformed" "property=$hashProperty"
    }
}

if ($linkEvidence.artifactBytes -isnot [int] -and
    $linkEvidence.artifactBytes -isnot [long]) {
    Write-Failure "link_evidence_number_malformed" "property=artifactBytes"
}
$artifactBytesEvidence = Convert-EvidenceInt64 $linkEvidence.artifactBytes "artifactBytes"
if ([string]$linkEvidence.artifactSha256 -cne $sha256 -or
    $artifactBytesEvidence -ne $bytes.LongLength -or
    [string]$linkEvidence.linkerMapSha256 -cne $linkerMapSha256 -or
    [string]$linkEvidence.linkResponseSha256 -cne $linkResponseSha256 -or
    [string]$linkEvidence.sourceProjectSha256 -cne $projectSha256 -or
    [string]$linkEvidence.entrySourceSha256 -cne $entrySourceSha256 -or
    [string]$linkEvidence.solutionSha256 -cne $solutionSha256) {
    Write-Failure "link_evidence_hash_mismatch"
}

$sourceClosureInputs = @($linkEvidence.sourceClosureInputs)
$expectedSourceClosurePaths = @(
    Get-ExpectedSourceClosurePaths $projectFullPath
)
if ($expectedSourceClosurePaths.Count -ne $expectedSourceClosureInputCount) {
    Write-Failure "source_closure_expected_input_count_invalid" (
        "expected=$expectedSourceClosureInputCount " +
        "actual=$($expectedSourceClosurePaths.Count)")
}
if ($sourceClosureInputs.Count -ne $expectedSourceClosureInputCount) {
    Write-Failure "source_closure_input_count_mismatch" (
        "expected={0} actual={1}" -f
        $expectedSourceClosureInputCount,
        $sourceClosureInputs.Count)
}

$sourceClosurePathSet = New-Object 'System.Collections.Generic.HashSet[string]' (
    [System.StringComparer]::OrdinalIgnoreCase)
$sourceClosureAuthorities =
    New-Object 'System.Collections.Generic.List[object]'
$verifiedSourceClosureInputs = @()
for ($index = 0; $index -lt $sourceClosureInputs.Count; $index++) {
    $sourceClosureInput = $sourceClosureInputs[$index]
    if ($null -eq $sourceClosureInput -or
        $null -eq $sourceClosureInput.PSObject.Properties["path"] -or
        $null -eq $sourceClosureInput.PSObject.Properties["sha256"] -or
        @($sourceClosureInput.PSObject.Properties).Count -ne 2) {
        Write-Failure "source_closure_input_shape_invalid" "index=$index"
    }

    $relativeInputPath = [string]$sourceClosureInput.path
    $inputSha256 = [string]$sourceClosureInput.sha256
    if ($inputSha256 -cnotmatch '^[0-9a-f]{64}$') {
        Write-Failure "source_closure_input_hash_malformed" "index=$index"
    }
    if (-not $sourceClosurePathSet.Add($relativeInputPath)) {
        Write-Failure "source_closure_input_duplicate" "path=$relativeInputPath"
    }
    if ($relativeInputPath -cne $expectedSourceClosurePaths[$index]) {
        Write-Failure "source_closure_input_set_mismatch" (
            "index={0} expected={1} actual={2}" -f
            $index,
            $expectedSourceClosurePaths[$index],
            $relativeInputPath)
    }

    $sourceClosureInputPath = Resolve-CanonicalSourceClosurePath $relativeInputPath
    if (-not (Test-Path -LiteralPath $sourceClosureInputPath -PathType Leaf)) {
        Write-Failure "source_closure_input_missing" "path=$relativeInputPath"
    }
    $sourceAuthority = Open-AuthorityReadHandle `
        -Path $sourceClosureInputPath `
        -RootPath $repositoryRoot `
        -Purpose ("source:" + $relativeInputPath) `
        -FailurePrefix "source_closure" `
        -MaximumBytes $maximumAuthorityTextBytes `
        -RequireSingleLink
    $sourceClosureAuthorities.Add($sourceAuthority)
    $actualInputSha256 = [string]$sourceAuthority.InitialSha256
    if ($actualInputSha256 -cne $inputSha256) {
        Write-Failure "source_closure_input_hash_mismatch" "path=$relativeInputPath"
    }

    $verifiedSourceClosureInputs += [pscustomobject]@{
        Path = $relativeInputPath
        Sha256 = $inputSha256
    }
}
Assert-DistinctAuthorityFiles -Records $sourceClosureAuthorities.ToArray()

$actualSourceClosureSha256 = Get-SourceClosureAggregateSha256 `
    $verifiedSourceClosureInputs
if ($actualSourceClosureSha256 -cne [string]$linkEvidence.sourceClosureSha256) {
    Write-Failure "source_closure_aggregate_mismatch" (
        "actual=$actualSourceClosureSha256")
}

$evidenceLinkerPath = Get-AbsolutePath `
    ([string]$linkEvidence.linkerPath) `
    "linker_identity_path_invalid"
$linkerAuthority = Open-AuthorityReadHandle `
    -Path $evidenceLinkerPath `
    -RootPath (Get-AuthorityRootPath -Path $evidenceLinkerPath) `
    -Purpose "linker-tool" `
    -FailurePrefix "linker_identity" `
    -MaximumBytes $maximumPackageFileBytes
if (-not (Test-Path -LiteralPath $evidenceLinkerPath -PathType Leaf) -or
    [string]::IsNullOrWhiteSpace([string]$linkEvidence.linkerVersion) -or
    [string]$linkerAuthority.InitialSha256 -cne
        [string]$linkEvidence.linkerSha256) {
    Write-Failure "linker_identity_mismatch"
}
$actualLinkerVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo(
    $evidenceLinkerPath
).FileVersion
if ([string]::IsNullOrWhiteSpace($actualLinkerVersion) -or
    $actualLinkerVersion -cne [string]$linkEvidence.linkerVersion) {
    Write-Failure "linker_version_mismatch"
}

$configuration = [string]$linkEvidence.configuration
$runtimeIdentifier = [string]$linkEvidence.runtimeIdentifier
if ($configuration -cne "Release" -and $configuration -cne "Debug") {
    Write-Failure "configuration_invalid" "value=$configuration"
}
if ($runtimeIdentifier -cne "win-x64") {
    Write-Failure "build_identity_invalid" (
        "configuration=$configuration runtimeIdentifier=$runtimeIdentifier")
}

$expectedProjectExtensionsPath = [System.IO.Path]::GetFullPath(
    (Join-Path $repositoryRoot (
        "src\OS\obj\" +
        $configuration +
        "\net10.0\" +
        $runtimeIdentifier +
        "\nuget")))
$evidenceProjectExtensionsPath = Get-AbsolutePath `
    ([string]$linkEvidence.projectExtensionsPath) `
    "project_extensions_path_invalid"
if (-not [string]::Equals(
        $evidenceProjectExtensionsPath,
        $expectedProjectExtensionsPath,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    Write-Failure "project_extensions_path_not_canonical" (
        "expected=$expectedProjectExtensionsPath actual=$evidenceProjectExtensionsPath")
}
if (-not (Test-Path -LiteralPath $evidenceProjectExtensionsPath -PathType Container)) {
    Write-Failure "project_extensions_directory_missing" (
        "path=$evidenceProjectExtensionsPath")
}
$projectExtensionsItem = Get-Item -LiteralPath $evidenceProjectExtensionsPath -Force
if (($projectExtensionsItem.Attributes -band
        [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
    Write-Failure "project_extensions_directory_reparse_point"
}
$earlyUserExtensionsPath = Join-Path `
    $evidenceProjectExtensionsPath `
    "disabled-user-extensions"
if (Test-Path -LiteralPath $earlyUserExtensionsPath) {
    Write-Failure "post_restore_user_extensions_path_present" (
        "path=$earlyUserExtensionsPath")
}

$restoreClosureRoot = (
    "src/OS/obj/" +
    $configuration +
    "/net10.0/" +
    $runtimeIdentifier +
    "/nuget")
$expectedRestoreClosurePaths = @(
    ($restoreClosureRoot + "/OS.csproj.nuget.dgspec.json")
    ($restoreClosureRoot + "/OS.csproj.nuget.g.props")
    ($restoreClosureRoot + "/OS.csproj.nuget.g.targets")
    ($restoreClosureRoot + "/project.assets.json")
    ($restoreClosureRoot + "/project.nuget.cache")
)
[System.Array]::Sort(
    $expectedRestoreClosurePaths,
    [System.StringComparer]::Ordinal)
$restoreDirectoryEntries = @(
    Get-ChildItem -LiteralPath $evidenceProjectExtensionsPath -Force)
if ($restoreDirectoryEntries.Count -ne $expectedRestoreClosurePaths.Count -or
    @($restoreDirectoryEntries | Where-Object { $_.PSIsContainer }).Count -ne 0) {
    Write-Failure "restore_closure_entry_count_mismatch" (
        "expected={0} actual={1}" -f
        $expectedRestoreClosurePaths.Count,
        $restoreDirectoryEntries.Count)
}
foreach ($restoreDirectoryEntry in $restoreDirectoryEntries) {
    if (($restoreDirectoryEntry.Attributes -band
            [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
        Write-Failure "restore_closure_reparse_point" (
            "path=$($restoreDirectoryEntry.FullName)")
    }
}

$restoreClosureInputs = @($linkEvidence.restoreClosureInputs)
if ($restoreClosureInputs.Count -ne $expectedRestoreClosurePaths.Count) {
    Write-Failure "restore_closure_input_count_mismatch" (
        "expected={0} actual={1}" -f
        $expectedRestoreClosurePaths.Count,
        $restoreClosureInputs.Count)
}
$restoreClosurePathSet = New-Object 'System.Collections.Generic.HashSet[string]' (
    [System.StringComparer]::OrdinalIgnoreCase)
$verifiedRestoreClosureInputs = @()
for ($index = 0; $index -lt $restoreClosureInputs.Count; $index++) {
    $restoreClosureInput = $restoreClosureInputs[$index]
    if ($null -eq $restoreClosureInput -or
        $null -eq $restoreClosureInput.PSObject.Properties["path"] -or
        $null -eq $restoreClosureInput.PSObject.Properties["sha256"] -or
        @($restoreClosureInput.PSObject.Properties).Count -ne 2) {
        Write-Failure "restore_closure_input_shape_invalid" "index=$index"
    }
    $relativeRestorePath = [string]$restoreClosureInput.path
    $restoreInputSha256 = [string]$restoreClosureInput.sha256
    if ($restoreInputSha256 -cnotmatch '^[0-9a-f]{64}$') {
        Write-Failure "restore_closure_input_hash_malformed" "index=$index"
    }
    if (-not $restoreClosurePathSet.Add($relativeRestorePath)) {
        Write-Failure "restore_closure_input_duplicate" "path=$relativeRestorePath"
    }
    if ($relativeRestorePath -cne $expectedRestoreClosurePaths[$index]) {
        Write-Failure "restore_closure_input_set_mismatch" (
            "index={0} expected={1} actual={2}" -f
            $index,
            $expectedRestoreClosurePaths[$index],
            $relativeRestorePath)
    }
    $restoreInputPath = Resolve-CanonicalSourceClosurePath `
        $relativeRestorePath `
        "restore_closure"
    if (-not (Test-Path -LiteralPath $restoreInputPath -PathType Leaf)) {
        Write-Failure "restore_closure_input_missing" "path=$relativeRestorePath"
    }
    if ((Get-Item -LiteralPath $restoreInputPath).Length -le 0) {
        Write-Failure "restore_closure_input_empty" "path=$relativeRestorePath"
    }
    $restoreAuthority = Open-AuthorityReadHandle `
        -Path $restoreInputPath `
        -RootPath $repositoryRoot `
        -Purpose ("restore-closure:" + $relativeRestorePath) `
        -FailurePrefix "restore_closure" `
        -MaximumBytes $maximumProjectAssetsBytes
    $actualRestoreInputSha256 = [string]$restoreAuthority.InitialSha256
    if ($actualRestoreInputSha256 -cne $restoreInputSha256) {
        Write-Failure "restore_closure_input_hash_mismatch" (
            "path=$relativeRestorePath")
    }
    $verifiedRestoreClosureInputs += [pscustomobject]@{
        Path = $relativeRestorePath
        Sha256 = $restoreInputSha256
    }
}
$actualRestoreClosureSha256 = Get-SourceClosureAggregateSha256 `
    $verifiedRestoreClosureInputs
if ($actualRestoreClosureSha256 -cne
    [string]$linkEvidence.restoreClosureSha256) {
    Write-Failure "restore_closure_aggregate_mismatch" (
        "actual=$actualRestoreClosureSha256")
}
$expectedProjectAssetsPath = Join-Path (
    $expectedProjectExtensionsPath) (
    "project.assets.json")
$evidenceProjectAssetsPath = Get-AbsolutePath `
    ([string]$linkEvidence.projectAssetsPath) `
    "project_assets_path_invalid"
if (-not [string]::Equals(
        $evidenceProjectAssetsPath,
        $expectedProjectAssetsPath,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    Write-Failure "project_assets_path_not_canonical" (
        "expected=$expectedProjectAssetsPath actual=$evidenceProjectAssetsPath")
}
if (-not (Test-Path -LiteralPath $evidenceProjectAssetsPath -PathType Leaf)) {
    Write-Failure "project_assets_missing" "path=$evidenceProjectAssetsPath"
}
$projectAssetsAuthority = Open-AuthorityReadHandle `
    -Path $evidenceProjectAssetsPath `
    -RootPath $expectedProjectExtensionsPath `
    -Purpose "project-assets" `
    -FailurePrefix "project_assets" `
    -MaximumBytes $maximumProjectAssetsBytes
$actualProjectAssetsSha256 =
    [string]$projectAssetsAuthority.InitialSha256
if ($actualProjectAssetsSha256 -cne
        [string]$linkEvidence.projectAssetsSha256) {
    Write-Failure "project_assets_hash_mismatch"
}

$expectedPackageRootPath = [System.IO.Path]::GetFullPath(
    (Join-Path $repositoryRoot (
        "src\OS\obj\" +
        $configuration +
        "\net10.0\" +
        $runtimeIdentifier +
        "\packages")))
$packageRootFullPath = Get-AbsolutePath `
    ([string]$linkEvidence.packageRootPath) `
    "package_root_path_invalid"
if ([string]$linkEvidence.packageRootKind -cne "isolated" -or
    -not [string]::Equals(
        $packageRootFullPath,
        $expectedPackageRootPath,
        [System.StringComparison]::OrdinalIgnoreCase) -or
    -not (Test-Path -LiteralPath $packageRootFullPath -PathType Container)) {
    Write-Failure "package_root_contract_invalid" (
        "expected=$expectedPackageRootPath actual=$packageRootFullPath")
}
Assert-NoReparsePoints `
    -RootPath $packageRootFullPath `
    -Path $packageRootFullPath `
    -Reason "package_root_reparse_point"

$expectedPinnedRestoreSourcePath = [System.IO.Path]::GetFullPath(
    (Join-Path $repositoryRoot (
        "src\OS\obj\" +
        $configuration +
        "\net10.0\" +
        $runtimeIdentifier +
        "\restore-source")))
$pinnedRestoreSourceFullPath = Get-AbsolutePath `
    ([string]$linkEvidence.pinnedRestoreSourcePath) `
    "pinned_restore_source_path_invalid"
if (-not [string]::Equals(
        $pinnedRestoreSourceFullPath,
        $expectedPinnedRestoreSourcePath,
        [System.StringComparison]::OrdinalIgnoreCase) -or
    -not (Test-Path -LiteralPath $pinnedRestoreSourceFullPath -PathType Container)) {
    Write-Failure "pinned_restore_source_not_canonical" (
        "expected=$expectedPinnedRestoreSourcePath actual=$pinnedRestoreSourceFullPath")
}
$pinnedRestoreSourceRelativeRoot = (
    "src/OS/obj/" +
    $configuration +
    "/net10.0/" +
    $runtimeIdentifier +
    "/restore-source")
[string[]]$expectedPinnedRestoreSourceInputs = @(
    $pinnedRestoreSourceRelativeRoot + "/NuGet.Config"
)
$pinnedRestoreExpectedHashes = @{}
foreach ($archive in $expectedRestoreArchives) {
    $sourceRelativePath = (
        $pinnedRestoreSourceRelativeRoot +
        "/" +
        [string]$archive.FileName)
    $expectedPinnedRestoreSourceInputs += $sourceRelativePath
    $pinnedRestoreExpectedHashes[$sourceRelativePath] =
        [string]$archive.Sha256
}
[System.Array]::Sort(
    $expectedPinnedRestoreSourceInputs,
    [System.StringComparer]::Ordinal)
$pinnedSourceEntries = @(
    Get-ChildItem -LiteralPath $pinnedRestoreSourceFullPath -Force
)
if ($pinnedSourceEntries.Count -ne $expectedPinnedRestoreSourceInputs.Count -or
    @($pinnedSourceEntries | Where-Object { $_.PSIsContainer }).Count -ne 0) {
    Write-Failure "pinned_restore_source_entry_count_mismatch"
}
$pinnedRestoreSourceSnapshot = Get-VerifiedExactFileSnapshot `
    -RootPath $repositoryRoot `
    -ExpectedRelativePaths $expectedPinnedRestoreSourceInputs `
    -EvidenceInputs @($linkEvidence.pinnedRestoreSourceInputs) `
    -FailurePrefix "pinned_restore_source" `
    -ExpectedHashes $pinnedRestoreExpectedHashes
if ($pinnedRestoreSourceSnapshot.AggregateSha256 -cne
    [string]$linkEvidence.pinnedRestoreSourceSha256) {
    Write-Failure "pinned_restore_source_aggregate_mismatch"
}
Assert-PinnedRestoreConfig `
    -ConfigPath (Join-Path $pinnedRestoreSourceFullPath "NuGet.Config") `
    -RestoreSourcePath $pinnedRestoreSourceFullPath

$expectedGlobalPackageCacheRoot = if (
    -not [string]::IsNullOrWhiteSpace($env:NUGET_PACKAGES)) {
    [System.IO.Path]::GetFullPath($env:NUGET_PACKAGES)
}
else {
    [System.IO.Path]::GetFullPath(
        (Join-Path (
            [Environment]::GetFolderPath(
                [Environment+SpecialFolder]::UserProfile)) ".nuget\packages"))
}
$restoreArchiveSourceRootFullPath = Get-AbsolutePath `
    ([string]$linkEvidence.restoreArchiveSourceRootPath) `
    "restore_archive_source_root_path_invalid"
if (-not [string]::Equals(
        $restoreArchiveSourceRootFullPath,
        $expectedGlobalPackageCacheRoot,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    Write-Failure "restore_archive_source_root_not_canonical" (
        "expected=$expectedGlobalPackageCacheRoot actual=$restoreArchiveSourceRootFullPath")
}
[string[]]$expectedRestoreArchiveInputPaths = @()
$restoreArchiveExpectedHashes = @{}
foreach ($archive in $expectedRestoreArchives) {
    $archiveRelativePath = (
        [string]$archive.Id +
        "/" +
        [string]$archive.Version +
        "/" +
        [string]$archive.FileName)
    $expectedRestoreArchiveInputPaths += $archiveRelativePath
    $restoreArchiveExpectedHashes[$archiveRelativePath] =
        [string]$archive.Sha256
}
$restoreArchiveSnapshot = Get-VerifiedExactFileSnapshot `
    -RootPath $restoreArchiveSourceRootFullPath `
    -ExpectedRelativePaths $expectedRestoreArchiveInputPaths `
    -EvidenceInputs @($linkEvidence.restoreArchiveInputs) `
    -FailurePrefix "restore_archive" `
    -ExpectedHashes $restoreArchiveExpectedHashes
if ($restoreArchiveSnapshot.AggregateSha256 -cne
    [string]$linkEvidence.restoreArchiveClosureSha256) {
    Write-Failure "restore_archive_aggregate_mismatch"
}

Assert-RestoreGeneratedSemanticClosure `
    -ProjectFullPath $projectFullPath `
    -ProjectExtensionsFullPath $evidenceProjectExtensionsPath `
    -PackageRootFullPath $packageRootFullPath

$dotnetHostFullPath = Get-AbsolutePath `
    ([string]$linkEvidence.dotnetHostPath) `
    "dotnet_host_path_invalid"
try {
    $currentDotnetCommand = Get-Command `
        "dotnet.exe" `
        -CommandType Application `
        -ErrorAction Stop |
        Select-Object -First 1
    $currentDotnetHostPath = [System.IO.Path]::GetFullPath(
        $currentDotnetCommand.Source)
}
catch {
    Write-Failure "dotnet_host_unavailable"
}
$dotnetHostAuthority = Open-AuthorityReadHandle `
    -Path $dotnetHostFullPath `
    -RootPath (Get-AuthorityRootPath -Path $dotnetHostFullPath) `
    -Purpose "dotnet-host" `
    -FailurePrefix "dotnet_host" `
    -MaximumBytes $maximumPackageFileBytes
if (-not (Test-Path -LiteralPath $dotnetHostFullPath -PathType Leaf) -or
    -not [string]::Equals(
        $dotnetHostFullPath,
        $currentDotnetHostPath,
        [System.StringComparison]::OrdinalIgnoreCase) -or
    [string]$dotnetHostAuthority.InitialSha256 -cne
        [string]$linkEvidence.dotnetHostSha256) {
    Write-Failure "dotnet_host_identity_mismatch"
}

$dotnetSdkVersion = [string]$linkEvidence.dotnetSdkVersion
if ($dotnetSdkVersion -cnotmatch '^[0-9]+\.[0-9]+\.[0-9]+(?:[-+][0-9A-Za-z.-]+)?$') {
    Write-Failure "dotnet_sdk_version_invalid" "value=$dotnetSdkVersion"
}
$dotnetSdkDirectory = Join-Path `
    (Split-Path -Parent $dotnetHostFullPath) `
    ("sdk\" + $dotnetSdkVersion)
if (-not (Test-Path -LiteralPath $dotnetSdkDirectory -PathType Container)) {
    Write-Failure "dotnet_sdk_identity_mismatch" (
        "version=$dotnetSdkVersion path=$dotnetSdkDirectory")
}

$globalJsonFullPath = Get-AbsolutePath "global.json" "global_json_path_invalid"
$globalJsonAuthority = Open-AuthorityReadHandle `
    -Path $globalJsonFullPath `
    -RootPath $repositoryRoot `
    -Purpose "global-json" `
    -FailurePrefix "global_json" `
    -MaximumBytes 65536L
try {
    $globalJsonText = Read-AuthorityUtf8Text `
        -Record $globalJsonAuthority `
        -MaximumBytes 65536L `
        -Reason "global_json_sdk_contract_invalid"
    $globalJson = $globalJsonText | ConvertFrom-Json
    $minimumSdkVersion = [System.Version]::Parse(
        [string]$globalJson.sdk.version)
    $actualSdkVersion = [System.Version]::Parse(
        $dotnetSdkVersion.Split("-")[0])
}
catch {
    Write-Failure "global_json_sdk_contract_invalid" (
        "type=$($_.Exception.GetType().Name)")
}
if ($actualSdkVersion.Major -ne $minimumSdkVersion.Major -or
    $actualSdkVersion.Minor -ne $minimumSdkVersion.Minor -or
    $actualSdkVersion.Build -lt $minimumSdkVersion.Build -or
    $dotnetSdkVersion.Contains("-") -or
    [string]$globalJson.sdk.rollForward -cne "latestPatch" -or
    [bool]$globalJson.sdk.allowPrerelease) {
    Write-Failure "dotnet_sdk_global_contract_mismatch" (
        "minimum=$minimumSdkVersion actual=$actualSdkVersion")
}

$windowsSdkLibraryFullPath = Get-AbsolutePath `
    ([string]$linkEvidence.windowsSdkLibraryPath) `
    "windows_sdk_library_path_invalid"
$windowsSdkVersion = [string]$linkEvidence.windowsSdkVersion
$windowsSdkExpectedFragment = (
    "\Lib\" + $windowsSdkVersion + "\um\x64\")
$windowsSdkAuthority = Open-AuthorityReadHandle `
    -Path $windowsSdkLibraryFullPath `
    -RootPath (Get-AuthorityRootPath -Path $windowsSdkLibraryFullPath) `
    -Purpose "windows-sdk-library" `
    -FailurePrefix "windows_sdk_library" `
    -MaximumBytes $maximumPackageFileBytes
if ([string]::IsNullOrWhiteSpace($windowsSdkVersion) -or
    -not (Test-Path -LiteralPath $windowsSdkLibraryFullPath -PathType Leaf) -or
    [System.IO.Path]::GetFileName($windowsSdkLibraryFullPath) -ine
        "kernel32.Lib" -or
    $windowsSdkLibraryFullPath.IndexOf(
        $windowsSdkExpectedFragment,
        [System.StringComparison]::OrdinalIgnoreCase) -lt 0 -or
    [string]$windowsSdkAuthority.InitialSha256 -cne
        [string]$linkEvidence.windowsSdkLibrarySha256) {
    Write-Failure "windows_sdk_identity_mismatch"
}

$ilCompilerFullPath = Get-AbsolutePath `
    ([string]$linkEvidence.ilCompilerPath) `
    "ilcompiler_path_invalid"
$ilCompilerPackageVersion = [string]$linkEvidence.ilCompilerPackageVersion
$ilCompilerFileVersion = [string]$linkEvidence.ilCompilerFileVersion
$expectedIlCompilerFullPath = [System.IO.Path]::GetFullPath(
    (Join-Path $packageRootFullPath (
        "runtime.win-x64.microsoft.dotnet.ilcompiler\10.0.8\tools\ilc.exe")))
$ilCompilerAuthority = Open-AuthorityReadHandle `
    -Path $ilCompilerFullPath `
    -RootPath $packageRootFullPath `
    -Purpose "ilcompiler" `
    -FailurePrefix "ilcompiler" `
    -MaximumBytes $maximumPackageFileBytes
if ($ilCompilerPackageVersion -cne "10.0.8" -or
    [string]::IsNullOrWhiteSpace($ilCompilerFileVersion) -or
    -not (Test-Path -LiteralPath $ilCompilerFullPath -PathType Leaf) -or
    [System.IO.Path]::GetFileName($ilCompilerFullPath) -ine "ilc.exe" -or
    -not [string]::Equals(
        $ilCompilerFullPath,
        $expectedIlCompilerFullPath,
        [System.StringComparison]::OrdinalIgnoreCase) -or
    [string]$ilCompilerAuthority.InitialSha256 -cne
        [string]$linkEvidence.ilCompilerSha256) {
    Write-Failure "ilcompiler_identity_mismatch"
}
Assert-NoReparsePoints `
    -RootPath $packageRootFullPath `
    -Path $ilCompilerFullPath `
    -Reason "ilcompiler_path_reparse_point"
$ilCompilerDiscoverySourceFullPath = Get-AbsolutePath `
    ([string]$linkEvidence.ilCompilerDiscoverySourcePath) `
    "ilcompiler_discovery_source_path_invalid"
$expectedIlCompilerDiscoverySourceFullPath = [System.IO.Path]::GetFullPath(
    (Join-Path $restoreArchiveSourceRootFullPath (
        "runtime.win-x64.microsoft.dotnet.ilcompiler\10.0.8\tools\ilc.exe")))
$ilCompilerDiscoveryAuthority = Open-AuthorityReadHandle `
    -Path $ilCompilerDiscoverySourceFullPath `
    -RootPath $restoreArchiveSourceRootFullPath `
    -Purpose "ilcompiler-discovery-source" `
    -FailurePrefix "ilcompiler_discovery_source" `
    -MaximumBytes $maximumPackageFileBytes
if (-not [string]::Equals(
        $ilCompilerDiscoverySourceFullPath,
        $expectedIlCompilerDiscoverySourceFullPath,
        [System.StringComparison]::OrdinalIgnoreCase) -or
    -not (Test-Path -LiteralPath $ilCompilerDiscoverySourceFullPath -PathType Leaf) -or
    [string]$ilCompilerDiscoveryAuthority.InitialSha256 -cne
        [string]$linkEvidence.ilCompilerSha256) {
    Write-Failure "ilcompiler_discovery_source_identity_mismatch"
}
Assert-NoReparsePoints `
    -RootPath $restoreArchiveSourceRootFullPath `
    -Path $ilCompilerDiscoverySourceFullPath `
    -Reason "ilcompiler_discovery_source_reparse_point"
$actualIlCompilerFileVersion = (
    [System.Diagnostics.FileVersionInfo]::GetVersionInfo(
        $ilCompilerFullPath
    ).FileVersion)
if ([string]::IsNullOrWhiteSpace($actualIlCompilerFileVersion) -or
    $actualIlCompilerFileVersion -cne $ilCompilerFileVersion) {
    Write-Failure "ilcompiler_version_mismatch"
}

$nativeAotRuntimePackFullPath = Get-AbsolutePath `
    ([string]$linkEvidence.nativeAotRuntimePackPath) `
    "nativeaot_runtime_pack_path_invalid"
$nativeAotRuntimePackVersion = [string]$linkEvidence.nativeAotRuntimePackVersion
if ($nativeAotRuntimePackVersion -cne "10.0.8" -or
    -not (Test-Path -LiteralPath $nativeAotRuntimePackFullPath -PathType Container) -or
    [System.IO.Path]::GetFileName($nativeAotRuntimePackFullPath) -cne
        $nativeAotRuntimePackVersion -or
    [System.IO.Path]::GetFileName(
        (Split-Path -Parent $nativeAotRuntimePackFullPath)) -ine
        "microsoft.netcore.app.runtime.nativeaot.win-x64") {
    Write-Failure "nativeaot_runtime_pack_identity_mismatch"
}
Assert-NoReparsePoints `
    -RootPath $nativeAotRuntimePackFullPath `
    -Path $nativeAotRuntimePackFullPath `
    -Reason "nativeaot_runtime_pack_reparse_point"
$runtimeListFullPath = Join-Path `
    $nativeAotRuntimePackFullPath `
    "data\RuntimeList.xml"
$runtimeListAuthority = Open-AuthorityReadHandle `
    -Path $runtimeListFullPath `
    -RootPath $nativeAotRuntimePackFullPath `
    -Purpose "nativeaot-runtime-list" `
    -FailurePrefix "nativeaot_runtime_list" `
    -MaximumBytes 16777216L
if (-not (Test-Path -LiteralPath $runtimeListFullPath -PathType Leaf) -or
    (Get-Item -LiteralPath $runtimeListFullPath).Length -eq 0 -or
    (Get-Item -LiteralPath $runtimeListFullPath).Length -gt 16777216L -or
    [string]$runtimeListAuthority.InitialSha256 -cne
        [string]$linkEvidence.runtimeListSha256) {
    Write-Failure "nativeaot_runtime_list_identity_mismatch"
}

[string[]]$expectedRestorePackageVersions = @(
    $expectedConsumedPackageTrees |
        Where-Object { [string]$_.RootKind -cne "runtime" } |
        ForEach-Object {
            [string]$_.Id + "/" + [string]$_.Version
        }
)
$actualRestorePackageVersions = New-Object System.Collections.Generic.List[string]
foreach ($packageIdDirectory in @(
        Get-ChildItem -LiteralPath $packageRootFullPath -Force)) {
    if (-not $packageIdDirectory.PSIsContainer -or
        ($packageIdDirectory.Attributes -band
            [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
        Write-Failure "isolated_package_root_shape_invalid" (
            "path=$($packageIdDirectory.FullName)")
    }
    foreach ($versionEntry in @(
            Get-ChildItem -LiteralPath $packageIdDirectory.FullName -Force)) {
        if (-not $versionEntry.PSIsContainer -or
            ($versionEntry.Attributes -band
                [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            Write-Failure "isolated_package_version_shape_invalid" (
                "path=$($versionEntry.FullName)")
        }
        $actualRestorePackageVersions.Add(
            $packageIdDirectory.Name + "/" + $versionEntry.Name)
    }
}
[string[]]$actualRestorePackageVersionArray =
    $actualRestorePackageVersions.ToArray()
[System.Array]::Sort(
    $expectedRestorePackageVersions,
    [System.StringComparer]::Ordinal)
[System.Array]::Sort(
    $actualRestorePackageVersionArray,
    [System.StringComparer]::Ordinal)
if ($actualRestorePackageVersionArray.Count -ne
    $expectedRestorePackageVersions.Count) {
    Write-Failure "isolated_package_version_count_invalid" (
        "expected=$($expectedRestorePackageVersions.Count) " +
        "actual=$($actualRestorePackageVersionArray.Count)")
}
for ($versionIndex = 0;
    $versionIndex -lt $expectedRestorePackageVersions.Count;
    $versionIndex++) {
    if ($actualRestorePackageVersionArray[$versionIndex] -cne
        $expectedRestorePackageVersions[$versionIndex]) {
        Write-Failure "isolated_package_version_set_invalid" (
            "index=$versionIndex expected=$($expectedRestorePackageVersions[$versionIndex]) " +
            "actual=$($actualRestorePackageVersionArray[$versionIndex])")
    }
}

$packageEvidenceEntries = @($linkEvidence.packages)
if ($packageEvidenceEntries.Count -ne $expectedConsumedPackageTrees.Count) {
    Write-Failure "package_evidence_count_mismatch" (
        "expected=$($expectedConsumedPackageTrees.Count) actual=$($packageEvidenceEntries.Count)")
}
$packageClosureBuilder = New-Object System.Text.StringBuilder
$runtimePackageSnapshot = $null
for ($packageIndex = 0;
    $packageIndex -lt $expectedConsumedPackageTrees.Count;
    $packageIndex++) {
    $expectedPackage = $expectedConsumedPackageTrees[$packageIndex]
    $packageEvidence = $packageEvidenceEntries[$packageIndex]
    $packageIdentity = [string]$expectedPackage.Id +
        "/" +
        [string]$expectedPackage.Version
    Assert-ExactPropertyNameSet `
        -Value $packageEvidence `
        -ExpectedNames @(
            "id",
            "version",
            "rootKind",
            "rootPath",
            "contentHashSha512",
            "fileCount",
            "totalBytes",
            "aggregateSha256",
            "metadataSha256",
            "inputs"
        ) `
        -Reason "package_evidence_shape_invalid" `
        -Detail "package=$packageIdentity"
    if ([string]$packageEvidence.id -cne [string]$expectedPackage.Id -or
        [string]$packageEvidence.version -cne
            [string]$expectedPackage.Version -or
        [string]$packageEvidence.rootKind -cne
            [string]$expectedPackage.RootKind -or
        [string]$packageEvidence.contentHashSha512 -cne
            [string]$expectedPackage.ContentHashSha512) {
        Write-Failure "package_evidence_identity_mismatch" (
            "package=$packageIdentity")
    }

    $expectedPackageRoot = if (
        [string]$expectedPackage.RootKind -ceq "runtime") {
        $nativeAotRuntimePackFullPath
    }
    else {
        [System.IO.Path]::GetFullPath(
            (Join-Path $packageRootFullPath (
                [string]$expectedPackage.Id +
                "\" +
                [string]$expectedPackage.Version)))
    }
    $evidencePackageRoot = Get-AbsolutePath `
        ([string]$packageEvidence.rootPath) `
        "package_evidence_root_path_invalid"
    if (-not [string]::Equals(
            $evidencePackageRoot,
            $expectedPackageRoot,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        Write-Failure "package_evidence_root_path_mismatch" (
            "package=$packageIdentity")
    }

    $metadataPath = Join-Path $expectedPackageRoot ".nupkg.metadata"
    if (-not (Test-Path -LiteralPath $metadataPath -PathType Leaf)) {
        Write-Failure "package_metadata_missing" "package=$packageIdentity"
    }
    $metadataAuthority = Open-AuthorityReadHandle `
        -Path $metadataPath `
        -RootPath $expectedPackageRoot `
        -Purpose ("package-metadata:" + $packageIdentity) `
        -FailurePrefix "package_metadata" `
        -MaximumBytes $maximumPackageMetadataBytes
    try {
        $packageMetadataText = Read-AuthorityUtf8Text `
            -Record $metadataAuthority `
            -MaximumBytes $maximumPackageMetadataBytes `
            -Reason "package_metadata_invalid"
        $packageMetadata = $packageMetadataText | ConvertFrom-Json
    }
    catch {
        Write-Failure "package_metadata_invalid" (
            "package=$packageIdentity type=$($_.Exception.GetType().Name)")
    }
    Assert-ExactPropertyNameSet `
        -Value $packageMetadata `
        -ExpectedNames @("version", "contentHash", "source") `
        -Reason "package_metadata_shape_invalid" `
        -Detail "package=$packageIdentity"
    $metadataVersion = Get-EvidenceInteger `
        -Value $packageMetadata.version `
        -Property ("metadata[" + $packageIdentity + "].version")
    if ($metadataVersion -ne 2 -or
        [string]$packageMetadata.contentHash -cne
            [string]$expectedPackage.ContentHashSha512 -or
        [string]::IsNullOrWhiteSpace([string]$packageMetadata.source)) {
        Write-Failure "package_metadata_contract_invalid" (
            "package=$packageIdentity")
    }
    if ([string]$expectedPackage.RootKind -cne "runtime") {
        try {
            $metadataSourcePath = [System.IO.Path]::GetFullPath(
                [string]$packageMetadata.source)
        }
        catch {
            Write-Failure "package_metadata_source_invalid" (
                "package=$packageIdentity")
        }
        if (-not [string]::Equals(
                $metadataSourcePath,
                $pinnedRestoreSourceFullPath,
                [System.StringComparison]::OrdinalIgnoreCase)) {
            Write-Failure "package_metadata_source_mismatch" (
                "package=$packageIdentity")
        }
    }

    $metadataHash = [string]$metadataAuthority.InitialSha256
    if ([string]$packageEvidence.metadataSha256 -cnotmatch
            '^[0-9a-f]{64}$' -or
        [string]$packageEvidence.metadataSha256 -cne $metadataHash) {
        Write-Failure "package_metadata_hash_mismatch" (
            "package=$packageIdentity")
    }
    $treeSnapshot = Get-PackageTreeSnapshot `
        -PackageRoot $expectedPackageRoot `
        -FailurePrefix "package_tree"
    $evidenceFileCount = Get-EvidenceInteger `
        -Value $packageEvidence.fileCount `
        -Property ("packages[" + $packageIndex + "].fileCount")
    $evidenceTotalBytes = Get-EvidenceInteger `
        -Value $packageEvidence.totalBytes `
        -Property ("packages[" + $packageIndex + "].totalBytes")
    if ($treeSnapshot.FileCount -ne [int]$expectedPackage.FileCount -or
        $treeSnapshot.TotalBytes -ne [long]$expectedPackage.TotalBytes -or
        $treeSnapshot.AggregateSha256 -cne
            [string]$expectedPackage.AggregateSha256 -or
        $evidenceFileCount -ne $treeSnapshot.FileCount -or
        $evidenceTotalBytes -ne $treeSnapshot.TotalBytes -or
        [string]$packageEvidence.aggregateSha256 -cne
            $treeSnapshot.AggregateSha256) {
        Write-Failure "package_tree_contract_mismatch" (
            "package=$packageIdentity count=$($treeSnapshot.FileCount) " +
            "bytes=$($treeSnapshot.TotalBytes) sha256=$($treeSnapshot.AggregateSha256)")
    }
    Assert-PackageEvidenceInputs `
        -EvidenceInputs @($packageEvidence.inputs) `
        -ActualInputs @($treeSnapshot.Inputs) `
        -PackageIdentity $packageIdentity
    [void]$packageClosureBuilder.Append([string]$expectedPackage.Id)
    [void]$packageClosureBuilder.Append("|")
    [void]$packageClosureBuilder.Append([string]$expectedPackage.Version)
    [void]$packageClosureBuilder.Append("|")
    [void]$packageClosureBuilder.Append([string]$expectedPackage.RootKind)
    [void]$packageClosureBuilder.Append("|")
    [void]$packageClosureBuilder.Append($treeSnapshot.AggregateSha256)
    [void]$packageClosureBuilder.Append("`n")
    if ([string]$expectedPackage.RootKind -ceq "runtime") {
        $runtimePackageSnapshot = $treeSnapshot
    }
}
$actualPackageClosureSha256 = Get-Utf8Sha256 `
    -Text $packageClosureBuilder.ToString()
if ($actualPackageClosureSha256 -cne
    [string]$linkEvidence.packageClosureSha256) {
    Write-Failure "package_closure_aggregate_mismatch"
}
if ($null -eq $runtimePackageSnapshot) {
    Write-Failure "nativeaot_runtime_package_snapshot_missing"
}
$nativeAotRuntimePackFileCount = Get-EvidenceInteger `
    -Value $linkEvidence.nativeAotRuntimePackFileCount `
    -Property "nativeAotRuntimePackFileCount"
$nativeAotRuntimePackBytes = Get-EvidenceInteger `
    -Value $linkEvidence.nativeAotRuntimePackBytes `
    -Property "nativeAotRuntimePackBytes"
if ($nativeAotRuntimePackFileCount -ne $runtimePackageSnapshot.FileCount -or
    $nativeAotRuntimePackBytes -ne $runtimePackageSnapshot.TotalBytes -or
    [string]$linkEvidence.nativeAotRuntimePackSha256 -cne
        $runtimePackageSnapshot.AggregateSha256) {
    Write-Failure "nativeaot_runtime_pack_tree_mismatch"
}

[string[]]$expectedPackageBuildPaths = @(
    $expectedPackageBuildClosure |
        ForEach-Object { [string]$_.Path }
)
$packageBuildExpectedHashes = @{}
foreach ($expectedBuildInput in $expectedPackageBuildClosure) {
    $packageBuildExpectedHashes[[string]$expectedBuildInput.Path] =
        [string]$expectedBuildInput.Sha256
}
$packageBuildSnapshot = Get-VerifiedExactFileSnapshot `
    -RootPath $packageRootFullPath `
    -ExpectedRelativePaths $expectedPackageBuildPaths `
    -EvidenceInputs @($linkEvidence.packageBuildClosureInputs) `
    -FailurePrefix "package_build_closure" `
    -ExpectedHashes $packageBuildExpectedHashes
if ($packageBuildSnapshot.AggregateSha256 -cne
    [string]$linkEvidence.packageBuildClosureSha256) {
    Write-Failure "package_build_closure_aggregate_mismatch"
}

Assert-StringEvidenceSequence `
    -Actual @($linkEvidence.activePackageImportEdges) `
    -Expected $expectedActivePackageImportEdges `
    -Reason "active_package_import_edges_mismatch"
$actualActivePackageImportGraphSha256 = Get-Utf8Sha256 -Text (
    ($expectedActivePackageImportEdges -join "`n") + "`n")
if ($actualActivePackageImportGraphSha256 -cne
    [string]$linkEvidence.activePackageImportGraphSha256) {
    Write-Failure "active_package_import_graph_hash_mismatch"
}
Assert-StringEvidenceSequence `
    -Actual @($linkEvidence.packageTaskBindings) `
    -Expected $expectedPackageTaskBindings `
    -Reason "package_task_bindings_mismatch"
$actualPackageTaskBindingsSha256 = Get-Utf8Sha256 -Text (
    ($expectedPackageTaskBindings -join "`n") + "`n")
if ($actualPackageTaskBindingsSha256 -cne
    [string]$linkEvidence.packageTaskBindingsSha256) {
    Write-Failure "package_task_bindings_hash_mismatch"
}

$ilCompilerBuildPackageRoot = Join-Path `
    $packageRootFullPath `
    "microsoft.dotnet.ilcompiler\10.0.8"
$ilCompilerHostPackageRoot = Join-Path `
    $packageRootFullPath `
    "runtime.win-x64.microsoft.dotnet.ilcompiler\10.0.8"
$ilLinkPackageRoot = Join-Path `
    $packageRootFullPath `
    "microsoft.net.illink.tasks\10.0.10"
$directorySeparator = [string][System.IO.Path]::DirectorySeparatorChar
$canonicalPackagePaths = [ordered]@{
    ILCompilerTargetsPath = Join-Path `
        $ilCompilerBuildPackageRoot `
        "build\Microsoft.DotNet.ILCompiler.SingleEntry.targets"
    ILLinkTargetsPath = Join-Path `
        $ilLinkPackageRoot `
        "build\Microsoft.NET.ILLink.targets"
    ILLinkTasksAssembly = Join-Path `
        $ilLinkPackageRoot `
        "tools\net\ILLink.Tasks.dll"
    IlcBuildTasksPath = Join-Path `
        $ilCompilerBuildPackageRoot `
        "tools\netstandard\ILCompiler.Build.Tasks.dll"
    IlcHostPackagePath = $ilCompilerHostPackageRoot
    IlcToolsPath = (Join-Path $ilCompilerHostPackageRoot "tools") +
        $directorySeparator
    RuntimePackagePath = $nativeAotRuntimePackFullPath
    IlcFrameworkPath = (
        Join-Path $nativeAotRuntimePackFullPath (
            "runtimes\" + $runtimeIdentifier + "\lib")) +
        $directorySeparator
    IlcFrameworkNativePath = (
        Join-Path $nativeAotRuntimePackFullPath (
            "runtimes\" + $runtimeIdentifier + "\native")) +
        $directorySeparator
    IlcSdkPath = (
        Join-Path $nativeAotRuntimePackFullPath (
            "runtimes\" + $runtimeIdentifier + "\native")) +
        $directorySeparator
    IlcMibcPath = (Join-Path $nativeAotRuntimePackFullPath "mibc") +
        $directorySeparator
    LanguageTargets = Join-Path `
        $dotnetSdkDirectory `
        "Microsoft.CSharp.targets"
    CSharpTargetsPath = Join-Path `
        $dotnetSdkDirectory `
        "Microsoft.CSharp.CurrentVersion.targets"
    CommonTargetsPath = Join-Path `
        $dotnetSdkDirectory `
        "Microsoft.Common.CurrentVersion.targets"
}
$canonicalEvidencePathBindings = @(
    [pscustomobject]@{
        EvidenceName = "ilCompilerTargetsPath"
        CanonicalName = "ILCompilerTargetsPath"
        Kind = "Leaf"
        TrailingSeparator = $false
        Root = $packageRootFullPath
    },
    [pscustomobject]@{
        EvidenceName = "ilLinkTargetsPath"
        CanonicalName = "ILLinkTargetsPath"
        Kind = "Leaf"
        TrailingSeparator = $false
        Root = $packageRootFullPath
    },
    [pscustomobject]@{
        EvidenceName = "ilLinkTasksAssemblyPath"
        CanonicalName = "ILLinkTasksAssembly"
        Kind = "Leaf"
        TrailingSeparator = $false
        Root = $packageRootFullPath
    },
    [pscustomobject]@{
        EvidenceName = "ilcBuildTasksPath"
        CanonicalName = "IlcBuildTasksPath"
        Kind = "Leaf"
        TrailingSeparator = $false
        Root = $packageRootFullPath
    },
    [pscustomobject]@{
        EvidenceName = "ilcHostPackagePath"
        CanonicalName = "IlcHostPackagePath"
        Kind = "Container"
        TrailingSeparator = $false
        Root = $packageRootFullPath
    },
    [pscustomobject]@{
        EvidenceName = "ilcToolsPath"
        CanonicalName = "IlcToolsPath"
        Kind = "Container"
        TrailingSeparator = $true
        Root = $packageRootFullPath
    },
    [pscustomobject]@{
        EvidenceName = "ilcFrameworkPath"
        CanonicalName = "IlcFrameworkPath"
        Kind = "Container"
        TrailingSeparator = $true
        Root = $nativeAotRuntimePackFullPath
    },
    [pscustomobject]@{
        EvidenceName = "ilcFrameworkNativePath"
        CanonicalName = "IlcFrameworkNativePath"
        Kind = "Container"
        TrailingSeparator = $true
        Root = $nativeAotRuntimePackFullPath
    },
    [pscustomobject]@{
        EvidenceName = "ilcSdkPath"
        CanonicalName = "IlcSdkPath"
        Kind = "Container"
        TrailingSeparator = $true
        Root = $nativeAotRuntimePackFullPath
    },
    [pscustomobject]@{
        EvidenceName = "ilcMibcPath"
        CanonicalName = "IlcMibcPath"
        Kind = "OptionalContainer"
        TrailingSeparator = $true
        Root = $nativeAotRuntimePackFullPath
    },
    [pscustomobject]@{
        EvidenceName = "languageTargetsPath"
        CanonicalName = "LanguageTargets"
        Kind = "Leaf"
        TrailingSeparator = $false
        Root = $dotnetSdkDirectory
    },
    [pscustomobject]@{
        EvidenceName = "csharpTargetsPath"
        CanonicalName = "CSharpTargetsPath"
        Kind = "Leaf"
        TrailingSeparator = $false
        Root = $dotnetSdkDirectory
    },
    [pscustomobject]@{
        EvidenceName = "commonTargetsPath"
        CanonicalName = "CommonTargetsPath"
        Kind = "Leaf"
        TrailingSeparator = $false
        Root = $dotnetSdkDirectory
    }
)
foreach ($pathBinding in $canonicalEvidencePathBindings) {
    $evidencePathText = [string]$linkEvidence.($pathBinding.EvidenceName)
    $canonicalPathText = [string]$canonicalPackagePaths[$pathBinding.CanonicalName]
    if ([bool]$pathBinding.TrailingSeparator -and
        -not $evidencePathText.EndsWith(
            $directorySeparator,
            [System.StringComparison]::Ordinal)) {
        Write-Failure "package_property_path_trailing_separator_missing" (
            "property=$($pathBinding.EvidenceName)")
    }
    $evidenceCanonicalPath = Get-AbsolutePath `
        $evidencePathText `
        "package_property_path_invalid"
    $expectedCanonicalPath = [System.IO.Path]::GetFullPath($canonicalPathText)
    $pathKindValid = if (
        [string]$pathBinding.Kind -ceq "OptionalContainer") {
        -not (Test-Path -LiteralPath $evidenceCanonicalPath) -or
            (Test-Path -LiteralPath $evidenceCanonicalPath -PathType Container)
    }
    else {
        Test-Path `
            -LiteralPath $evidenceCanonicalPath `
            -PathType ([string]$pathBinding.Kind)
    }
    if (-not [string]::Equals(
            $evidenceCanonicalPath,
            $expectedCanonicalPath,
            [System.StringComparison]::OrdinalIgnoreCase) -or
        -not $pathKindValid) {
        Write-Failure "package_property_path_not_canonical" (
            "property=$($pathBinding.EvidenceName) expected=$expectedCanonicalPath " +
            "actual=$evidenceCanonicalPath")
    }
    if (Test-Path -LiteralPath $evidenceCanonicalPath) {
        Assert-NoReparsePoints `
            -RootPath ([string]$pathBinding.Root) `
            -Path $evidenceCanonicalPath `
            -Reason "package_property_path_reparse_point"
        if ([string]$pathBinding.Kind -ceq "Leaf") {
            [void](Open-AuthorityReadHandle `
                -Path $evidenceCanonicalPath `
                -RootPath ([string]$pathBinding.Root) `
                -Purpose ("evaluated-path:" + $pathBinding.EvidenceName) `
                -FailurePrefix "package_property_path" `
                -MaximumBytes $maximumPackageFileBytes)
        }
    }
}

$postRestoreSnapshot = Invoke-PostRestoreEvaluationSnapshot `
    -DotnetHostPath $dotnetHostFullPath `
    -ProjectFullPath $projectFullPath `
    -Configuration $configuration `
    -RuntimeIdentifier $runtimeIdentifier `
    -ProjectExtensionsFullPath $evidenceProjectExtensionsPath `
    -ProjectAssetsFullPath $evidenceProjectAssetsPath `
    -PackageRootFullPath $packageRootFullPath `
    -DotnetSdkDirectory $dotnetSdkDirectory `
    -CanonicalPaths $canonicalPackagePaths
Assert-PostRestoreEvidenceMatchesSnapshot `
    -Evidence $linkEvidence.postRestoreEvaluation `
    -Snapshot $postRestoreSnapshot

try {
    $generatedUtc = [System.DateTimeOffset]::ParseExact(
        [string]$linkEvidence.generatedUtc,
        "O",
        [System.Globalization.CultureInfo]::InvariantCulture,
        [System.Globalization.DateTimeStyles]::RoundtripKind)
}
catch {
    Write-Failure "generated_utc_invalid"
}
if ($generatedUtc.Offset -ne [System.TimeSpan]::Zero) {
    Write-Failure "generated_utc_not_utc"
}

$serializerObjectEvidencePath = Get-AbsolutePath `
    ([string]$linkEvidence.serializerObjectPath) `
    "serializer_object_path_invalid"
$expectedSerializerObjectPath = $artifactPath + ".serializer.obj"
if (-not [string]::Equals(
        $serializerObjectEvidencePath,
        $expectedSerializerObjectPath,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    Write-Failure "serializer_object_not_artifact_sibling" (
        "expected=$expectedSerializerObjectPath actual=$serializerObjectEvidencePath")
}
if ([string]::IsNullOrWhiteSpace($SerializerObjectPath)) {
    $serializerObjectFullPath = $serializerObjectEvidencePath
}
else {
    $serializerObjectFullPath = Get-AbsolutePath `
        $SerializerObjectPath `
        "serializer_object_path_invalid"
    if (-not [string]::Equals(
            $serializerObjectFullPath,
            $serializerObjectEvidencePath,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        Write-Failure "serializer_object_path_mismatch"
    }
}

if (-not (Test-Path -LiteralPath $serializerObjectFullPath -PathType Leaf)) {
    Write-Failure "serializer_object_missing" "path=$serializerObjectFullPath"
}
if ($linkEvidence.serializerObjectBytes -isnot [int] -and
    $linkEvidence.serializerObjectBytes -isnot [long]) {
    Write-Failure "link_evidence_number_malformed" "property=serializerObjectBytes"
}
$serializerObjectBytesEvidence = Convert-EvidenceInt64 `
    $linkEvidence.serializerObjectBytes `
    "serializerObjectBytes"
$serializerObjectAuthority = Open-AuthorityReadHandle `
    -Path $serializerObjectFullPath `
    -RootPath (Get-AuthorityRootPath -Path $serializerObjectFullPath) `
    -Purpose "serializer-preserved-object" `
    -FailurePrefix "serializer_object" `
    -MaximumBytes $serializerObjectSizeBytes `
    -RequireSingleLink
$serializerObjectLength = [long]$serializerObjectAuthority.InitialLength
$serializerObjectActualSha256 =
    [string]$serializerObjectAuthority.InitialSha256
if ($serializerObjectBytesEvidence -ne $serializerObjectSizeBytes -or
    $serializerObjectLength -ne $serializerObjectSizeBytes -or
    [string]$linkEvidence.serializerObjectSha256 -cne $serializerObjectSha256 -or
    $serializerObjectActualSha256 -cne $serializerObjectSha256) {
    Write-Failure "serializer_object_identity_mismatch" (
        "bytes={0} sha256={1}" -f $serializerObjectLength, $serializerObjectActualSha256)
}

$serializerLinkedObjectFullPath = Get-AbsolutePath `
    ([string]$linkEvidence.serializerLinkedObjectPath) `
    "serializer_linked_object_path_invalid"
if ([string]::Equals(
        $serializerLinkedObjectFullPath,
        $serializerObjectFullPath,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    Write-Failure "serializer_linked_and_preserved_object_paths_alias"
}
if (-not (Test-Path -LiteralPath $serializerLinkedObjectFullPath -PathType Leaf)) {
    Write-Failure "serializer_linked_object_missing" (
        "path=$serializerLinkedObjectFullPath")
}
$serializerLinkedObjectAuthority = Open-AuthorityReadHandle `
    -Path $serializerLinkedObjectFullPath `
    -RootPath (Get-AuthorityRootPath -Path $serializerLinkedObjectFullPath) `
    -Purpose "serializer-linked-object" `
    -FailurePrefix "serializer_linked_object" `
    -MaximumBytes $serializerObjectSizeBytes `
    -RequireSingleLink
$serializerLinkedObjectLength =
    [long]$serializerLinkedObjectAuthority.InitialLength
$serializerLinkedObjectSha256 =
    [string]$serializerLinkedObjectAuthority.InitialSha256
if ($serializerLinkedObjectLength -ne $serializerObjectSizeBytes -or
    $serializerLinkedObjectSha256 -cne $serializerObjectSha256 -or
    $serializerLinkedObjectSha256 -cne $serializerObjectActualSha256) {
    Write-Failure "serializer_linked_object_identity_mismatch" (
        "bytes={0} sha256={1}" -f
        $serializerLinkedObjectLength,
        $serializerLinkedObjectSha256)
}

$serializerSourceBindings = @(
    [pscustomobject]@{
        Property = "serializerTargetsSha256"
        Path = "tools/OS.X64TextSerializerGenerator/OS.X64TextSerializerGenerator.targets"
    },
    [pscustomobject]@{
        Property = "serializerGeneratorProjectSha256"
        Path = "tools/OS.X64TextSerializerGenerator/OS.X64TextSerializerGenerator.csproj"
    },
    [pscustomobject]@{
        Property = "serializerGeneratorProgramSha256"
        Path = "tools/OS.X64TextSerializerGenerator/Program.cs"
    },
    [pscustomobject]@{
        Property = "serializerGeneratorWriterSha256"
        Path = "tools/OS.X64TextSerializerGenerator/Amd64CoffWriter.cs"
    },
    [pscustomobject]@{
        Property = "serializerGeneratorSourceSha256"
        Path = "tools/OS.X64TextSerializerGenerator/InstructionPublicationSerializerGenerator.cs"
    },
    [pscustomobject]@{
        Property = "instructionPublicationSourceSha256"
        Path = "src/OS/Boot/X64InstructionPublication.cs"
    },
    [pscustomobject]@{
        Property = "runtimeImportAttributeSourceSha256"
        Path = "src/OS/Runtime/RuntimeImportAttribute.cs"
    },
    [pscustomobject]@{
        Property = "methodImplAttributeSourceSha256"
        Path = "src/OS/Runtime/MethodImplAttribute.cs"
    }
)
foreach ($binding in $serializerSourceBindings) {
    $bindingPath = Get-AbsolutePath $binding.Path "serializer_source_path_invalid"
    if (-not (Test-Path -LiteralPath $bindingPath -PathType Leaf)) {
        Write-Failure "serializer_source_missing" "property=$($binding.Property)"
    }
    $bindingAuthority = Open-AuthorityReadHandle `
        -Path $bindingPath `
        -RootPath $repositoryRoot `
        -Purpose ("serializer-source:" + $binding.Path) `
        -FailurePrefix "serializer_source" `
        -MaximumBytes $maximumAuthorityTextBytes
    if ([string]$bindingAuthority.InitialSha256 -cne
        [string]$linkEvidence.($binding.Property)) {
        Write-Failure "serializer_source_hash_mismatch" "property=$($binding.Property)"
    }
}

$managedAssemblyFullPath = Get-AbsolutePath `
    ([string]$linkEvidence.managedAssemblyPath) `
    "managed_assembly_path_invalid"
$expectedManagedAssemblyPath = [System.IO.Path]::GetFullPath(
    (Join-Path $repositoryRoot (
        "src\OS\bin\" +
        $configuration +
        "\net10.0\" +
        $runtimeIdentifier +
        "\OS.dll")))
if (-not [string]::Equals(
        $managedAssemblyFullPath,
        $expectedManagedAssemblyPath,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    Write-Failure "managed_assembly_path_not_canonical" (
        "expected=$expectedManagedAssemblyPath actual=$managedAssemblyFullPath")
}
if (-not (Test-Path -LiteralPath $managedAssemblyFullPath -PathType Leaf)) {
    Write-Failure "managed_assembly_missing" "path=$managedAssemblyFullPath"
}
$managedAssemblyAuthority = Open-AuthorityReadHandle `
    -Path $managedAssemblyFullPath `
    -RootPath $repositoryRoot `
    -Purpose "managed-assembly" `
    -FailurePrefix "managed_assembly" `
    -MaximumBytes $maximumManagedAssemblyBytes `
    -RequireSingleLink
$managedAssemblyLength = [long]$managedAssemblyAuthority.InitialLength
if ($managedAssemblyLength -eq 0 -or
    $managedAssemblyLength -gt $maximumManagedAssemblyBytes -or
    [string]$managedAssemblyAuthority.InitialSha256 -cne
        [string]$linkEvidence.managedAssemblySha256) {
    Write-Failure "managed_assembly_identity_mismatch"
}
Assert-DistinctAuthorityFiles -Records @(
    $artifactAuthority,
    $linkerMapAuthority,
    $linkResponseAuthority,
    $linkEvidenceAuthority,
    $serializerObjectAuthority,
    $serializerLinkedObjectAuthority,
    $managedAssemblyAuthority
)
Assert-ManagedPeImage -Path $managedAssemblyFullPath

$linkResponseValidationStopwatch.Start()
$entryOptions = [System.Text.RegularExpressions.Regex]::Matches(
    $linkResponseText,
    '(?im)^\s*/ENTRY:(?:"([^"]+)"|([^\s"]+))\s*$')
$entryOptionValue = if ($entryOptions.Count -eq 1 -and
    -not [string]::IsNullOrEmpty($entryOptions[0].Groups[1].Value)) {
    $entryOptions[0].Groups[1].Value
}
elseif ($entryOptions.Count -eq 1) {
    $entryOptions[0].Groups[2].Value
}
else {
    ""
}
if ($entryOptions.Count -ne 1 -or $entryOptionValue -cne "EfiMain") {
    Write-Failure "efimain_link_response_contract_missing"
}

$noDefaultLibraryOptions = [System.Text.RegularExpressions.Regex]::Matches(
    $linkResponseText,
    '(?im)^\s*/NODEFAULTLIB\s*$')
if ($noDefaultLibraryOptions.Count -ne 1) {
    Write-Failure "nodefaultlib_link_response_contract_missing"
}
if ($linkResponseText -match '(?im)(?:^|\s)/DEFAULTLIB(?::|\s|$)') {
    Write-Failure "default_library_linkage_present"
}

$forceIncludeOptions = [System.Text.RegularExpressions.Regex]::Matches(
    $linkResponseText,
    '(?im)^\s*/INCLUDE:(?:"([^"]+)"|([^\s"]+))\s*$')
$forceIncludeValue = if ($forceIncludeOptions.Count -eq 1 -and
    -not [string]::IsNullOrEmpty($forceIncludeOptions[0].Groups[1].Value)) {
    $forceIncludeOptions[0].Groups[1].Value
}
elseif ($forceIncludeOptions.Count -eq 1) {
    $forceIncludeOptions[0].Groups[2].Value
}
else {
    ""
}
if ($forceIncludeOptions.Count -ne 1 -or
    $forceIncludeValue -cne $serializerSymbol) {
    Write-Failure "serializer_force_include_contract_missing" (
        "matches=$($forceIncludeOptions.Count)")
}
if ($linkResponseText -match '(?im)(?:^|\s)/ALTERNATENAME:') {
    Write-Failure "serializer_fallback_linkage_present"
}

$mapResponseOptions = [System.Text.RegularExpressions.Regex]::Matches(
    $linkResponseText,
    '(?im)^\s*/MAP:(?:"([^"]+)"|(\S+))\s*$')
if ($mapResponseOptions.Count -ne 1) {
    Write-Failure "serializer_link_map_response_contract_missing" (
        "matches=$($mapResponseOptions.Count)")
}
$mapResponseValue = $mapResponseOptions[0].Groups[1].Value
if ([string]::IsNullOrEmpty($mapResponseValue)) {
    $mapResponseValue = $mapResponseOptions[0].Groups[2].Value
}
$responseDirectory = Split-Path -Parent $linkResponseFullPath
try {
    if ([System.IO.Path]::IsPathRooted($mapResponseValue)) {
        $mapResponseFullPath = [System.IO.Path]::GetFullPath($mapResponseValue)
    }
    else {
        $mapResponseFullPath = [System.IO.Path]::GetFullPath(
            (Join-Path $responseDirectory $mapResponseValue))
    }
}
catch {
    Write-Failure "serializer_link_map_response_path_invalid"
}
$expectedLinkedMapPath = [System.IO.Path]::GetFullPath(
    (Join-Path (Split-Path -Parent $serializerLinkedObjectFullPath) "OS.link.map"))
if (-not [string]::Equals(
        $mapResponseFullPath,
        $expectedLinkedMapPath,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    Write-Failure "serializer_link_map_path_mismatch" (
        "expected=$expectedLinkedMapPath actual=$mapResponseFullPath")
}
$linkResponseValidationStopwatch.Stop()
$linkedMapAuthority = Open-AuthorityReadHandle `
    -Path $expectedLinkedMapPath `
    -RootPath $repositoryRoot `
    -Purpose "linked-map" `
    -FailurePrefix "serializer_link_map" `
    -MaximumBytes 16777216L `
    -RequireSingleLink
if (-not (Test-Path -LiteralPath $expectedLinkedMapPath -PathType Leaf) -or
    [string]$linkedMapAuthority.InitialSha256 -cne $linkerMapSha256) {
    Write-Failure "serializer_link_map_identity_mismatch"
}

$linkResponseValidationStopwatch.Start()
$mapInfoResponseOptions = [System.Text.RegularExpressions.Regex]::Matches(
    $linkResponseText,
    '(?im)^\s*/MAPINFO:(?:"([^"]+)"|([^\s"]+))\s*$')
$mapInfoValue = if ($mapInfoResponseOptions.Count -eq 1 -and
    -not [string]::IsNullOrEmpty($mapInfoResponseOptions[0].Groups[1].Value)) {
    $mapInfoResponseOptions[0].Groups[1].Value
}
elseif ($mapInfoResponseOptions.Count -eq 1) {
    $mapInfoResponseOptions[0].Groups[2].Value
}
else {
    ""
}
if ($mapInfoResponseOptions.Count -ne 1 -or $mapInfoValue -cne "EXPORTS") {
    Write-Failure "serializer_link_mapinfo_contract_missing" (
        "matches=$($mapInfoResponseOptions.Count)")
}

$objectResponseMatches = 0
foreach ($token in $linkResponseTokens) {
    if ([string]::IsNullOrWhiteSpace($token) -or
        $token[0] -eq '/' -or
        $token[0] -eq '-') {
        continue
    }

    try {
        if ([System.IO.Path]::IsPathRooted($token)) {
            $candidateObjectPath = [System.IO.Path]::GetFullPath($token)
        }
        else {
            $candidateObjectPath = [System.IO.Path]::GetFullPath(
                (Join-Path $responseDirectory $token))
        }
    }
    catch {
        continue
    }

    if ([string]::Equals(
            $candidateObjectPath,
            $serializerLinkedObjectFullPath,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        $objectResponseMatches++
    }
}
if ($objectResponseMatches -ne 1) {
    Write-Failure "serializer_object_link_response_contract_missing" (
        "matches=$objectResponseMatches")
}
$linkResponseValidationStopwatch.Stop()

$hex16 = '[0-9A-Fa-f]{16}'
$hex8 = '[0-9A-Fa-f]{8}'
$hex4 = '[0-9A-Fa-f]{4}'
$preferredBaseMatches = [System.Text.RegularExpressions.Regex]::Matches(
    $linkerMapText,
    "(?m)^\s*Preferred load address is ($hex16)\s*$")
$timestampMatches = [System.Text.RegularExpressions.Regex]::Matches(
    $linkerMapText,
    "(?m)^\s*Timestamp is ($hex8)(?:\s|\(|$)")
$entryPointMatches = [System.Text.RegularExpressions.Regex]::Matches(
    $linkerMapText,
    "(?m)^\s*entry point at\s+($hex4):($hex8)\s*$")
$efiMainMatches = [System.Text.RegularExpressions.Regex]::Matches(
    $linkerMapText,
    "(?m)^\s*($hex4):($hex8)\s+EfiMain\s+($hex16)\s+\S.*$")
$managedEntryMatches = [System.Text.RegularExpressions.Regex]::Matches(
    $linkerMapText,
    "(?m)^\s*($hex4):($hex8)\s+OS_OS_Boot_EfiEntry__EfiMain\s+($hex16)\s+\S.*$")
$serializerMatches = [System.Text.RegularExpressions.Regex]::Matches(
    $linkerMapText,
    "(?m)^\s*($hex4):($hex8)\s+CLROS_X64_SerializeInstructionPublication\s+($hex16)\s+(.+?)\s*$")

if ($preferredBaseMatches.Count -ne 1 -or
    $timestampMatches.Count -ne 1 -or
    $entryPointMatches.Count -ne 1 -or
    $efiMainMatches.Count -ne 1 -or
    $managedEntryMatches.Count -ne 1) {
    Write-Failure "efimain_linker_map_contract_missing"
}
if ($serializerMatches.Count -ne 1) {
    Write-Failure "serializer_linker_map_contract_missing" (
        "matches=$($serializerMatches.Count)")
}

$serializerObjectLeaf = [System.IO.Path]::GetFileName(
    $serializerLinkedObjectFullPath)
$mapOwner = $serializerMatches[0].Groups[4].Value
$mapOwnerPattern = '(?i)(?:^|[\s\\/()])' +
    [System.Text.RegularExpressions.Regex]::Escape($serializerObjectLeaf) +
    '(?=$|[\s()])'
if ($mapOwner -notmatch $mapOwnerPattern) {
    Write-Failure "serializer_linker_map_object_mismatch" "owner=$mapOwner"
}

$preferredBase = Convert-HexUInt64 `
    $preferredBaseMatches[0].Groups[1].Value `
    "invalid_map_preferred_base"
$mapTimestamp = Convert-HexUInt64 `
    $timestampMatches[0].Groups[1].Value `
    "invalid_map_timestamp"
$efiMainVirtualAddress = Convert-HexUInt64 `
    $efiMainMatches[0].Groups[3].Value `
    "invalid_map_efimain_address"
$managedEntryVirtualAddress = Convert-HexUInt64 `
    $managedEntryMatches[0].Groups[3].Value `
    "invalid_map_managed_entry_address"
$serializerVirtualAddress = Convert-HexUInt64 `
    $serializerMatches[0].Groups[3].Value `
    "invalid_map_serializer_address"

if ($preferredBase -ne $imageBase -or
    $mapTimestamp -ne [uint64]$coffTimestamp -or
    $efiMainVirtualAddress -ne $managedEntryVirtualAddress -or
    $efiMainVirtualAddress -lt $preferredBase -or
    ($efiMainVirtualAddress - $preferredBase) -gt [uint64][uint32]::MaxValue -or
    [uint32]($efiMainVirtualAddress - $preferredBase) -ne $entryPointRva -or
    $efiMainMatches[0].Groups[1].Value -cne $entryPointMatches[0].Groups[1].Value -or
    $efiMainMatches[0].Groups[2].Value -cne $entryPointMatches[0].Groups[2].Value -or
    $efiMainMatches[0].Groups[1].Value -cne $managedEntryMatches[0].Groups[1].Value -or
    $efiMainMatches[0].Groups[2].Value -cne $managedEntryMatches[0].Groups[2].Value) {
    Write-Failure "efimain_entrypoint_mismatch"
}

if ($serializerVirtualAddress -le $preferredBase -or
    ($serializerVirtualAddress - $preferredBase) -gt [uint64][uint32]::MaxValue) {
    Write-Failure "serializer_map_rva_invalid"
}
$serializerRva = [uint32]($serializerVirtualAddress - $preferredBase)
if (($serializerRva -band 15) -ne 0) {
    Write-Failure "serializer_rva_misaligned" (
        "rva=0x{0:x8}" -f $serializerRva)
}
$serializerEndRva = Get-RvaRangeEnd `
    ([uint64]$serializerRva) `
    ([uint64]$serializerSizeBytes) `
    "serializer_rva_range_invalid"
if ($serializerRva -ge $sizeOfImage -or $serializerEndRva -gt [uint64]$sizeOfImage) {
    Write-Failure "serializer_rva_out_of_image" (
        "rva=0x{0:x8} size={1} sizeOfImage=0x{2:x8}" -f
        $serializerRva,
        $serializerSizeBytes,
        $sizeOfImage)
}

$serializerBacking = Resolve-FileBackedRvaRange `
    $sections `
    ([uint64]$serializerRva) `
    ([uint64]$serializerSizeBytes) `
    $bytes.LongLength `
    "serializer_not_file_backed" `
    "serializer_section_ambiguous"
Assert-CodeSectionCharacteristics `
    $serializerBacking.Section `
    "serializer_section_characteristics_invalid"
[byte[]]$actualSerializerBytes = Copy-ByteRange `
    $bytes `
    $serializerBacking.FileOffset `
    $serializerSizeBytes `
    "serializer_not_file_backed"
$actualSerializerSha256 = Get-BytesSha256 $actualSerializerBytes
if (-not (Test-ByteSequenceEqual $actualSerializerBytes $expectedSerializerBytes) -or
    $actualSerializerSha256 -cne $serializerBytesSha256) {
    Write-Failure "serializer_bytes_mismatch" "sha256=$actualSerializerSha256"
}

$runtimeValidationStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
$validatedUnwindSizes = New-Object 'System.Collections.Generic.Dictionary[uint32,int]'
$validatedUnwindFileOffsets = New-Object 'System.Collections.Generic.Dictionary[uint32,uint64]'
$previousRuntimeFunctionPresent = $false
$previousBeginAddress = [uint32]0
$previousEndAddress = [uint32]0
$previousUnwindInfoAddress = [uint32]0
$selectedRuntimeFunctionCount = 0
$selectedRuntimeFunctionIndex = -1
$selectedEndAddress = [uint32]0
$selectedUnwindInfoAddress = [uint32]0
$selectedUnwindStructureSize = 0
$selectedUnwindFileOffset = [uint64]0
$overlappingRuntimeFunctionCount = 0
$runtimeFunctionCount = [int]($exceptionDirectory.Size / 12)
for ($index = 0; $index -lt $runtimeFunctionCount; $index++) {
    $recordFileOffset = [uint64]$exceptionBacking.FileOffset + ([uint64]$index * 12)
    $recordFileOffsetInt32 = [int]$recordFileOffset
    $beginAddress = [System.BitConverter]::ToUInt32(
        $bytes,
        $recordFileOffsetInt32)
    $endAddress = [System.BitConverter]::ToUInt32(
        $bytes,
        $recordFileOffsetInt32 + 4)
    $unwindInfoAddress = [System.BitConverter]::ToUInt32(
        $bytes,
        $recordFileOffsetInt32 + 8)

    if ($beginAddress -ge $endAddress) {
        Write-Failure "runtime_function_range_invalid" (
            "index=$index begin=0x{0:x8} end=0x{1:x8}" -f $beginAddress, $endAddress)
    }
    if ($beginAddress -ge $sizeOfImage -or $endAddress -gt $sizeOfImage) {
        Write-Failure "runtime_function_range_out_of_image" "index=$index"
    }
    if ($unwindInfoAddress -eq 0) {
        Write-Failure "runtime_function_unwind_zero" "index=$index"
    }
    if (($unwindInfoAddress -band 3) -ne 0) {
        Write-Failure "runtime_function_unwind_misaligned" (
            "index=$index unwindRva=0x{0:x8}" -f $unwindInfoAddress)
    }
    if ($unwindInfoAddress -ge $sizeOfImage) {
        Write-Failure "runtime_function_unwind_out_of_image" "index=$index"
    }

    if ($previousRuntimeFunctionPresent) {
        if ($beginAddress -eq $previousBeginAddress -and
            $endAddress -eq $previousEndAddress -and
            $unwindInfoAddress -eq $previousUnwindInfoAddress) {
            Write-Failure "runtime_function_duplicate" "index=$index"
        }
        if ($beginAddress -le $previousBeginAddress) {
            Write-Failure "runtime_function_not_strictly_sorted" "index=$index"
        }
        if ($beginAddress -lt $previousEndAddress) {
            Write-Failure "runtime_function_overlap" "index=$index"
        }
    }

    if ($validatedUnwindSizes.ContainsKey($unwindInfoAddress)) {
        $unwindStructureSize = $validatedUnwindSizes[$unwindInfoAddress]
        $unwindFileOffset = $validatedUnwindFileOffsets[$unwindInfoAddress]
    }
    else {
        $unwindHeaderEnd = [uint64]$unwindInfoAddress + 4
        $unwindSection = $null
        $unwindFileOffset = [uint64]0
        $unwindSectionMatchCount = 0
        foreach ($section in $sections) {
            if ([uint64]$section.VirtualSize -eq 0 -or
                [uint64]$section.RawSize -eq 0) {
                continue
            }

            $sectionStart = [uint64]$section.VirtualAddress
            $sectionVirtualEnd = $sectionStart + [uint64]$section.VirtualSize
            $sectionRawEnd = $sectionStart + [uint64]$section.RawSize
            if ([uint64]$unwindInfoAddress -lt $sectionStart -or
                $unwindHeaderEnd -gt $sectionVirtualEnd -or
                $unwindHeaderEnd -gt $sectionRawEnd) {
                continue
            }

            $candidateFileOffset =
                [uint64]$section.RawOffset +
                ([uint64]$unwindInfoAddress - $sectionStart)
            if ($candidateFileOffset -gt [uint64]$bytes.LongLength -or
                4 -gt ([uint64]$bytes.LongLength - $candidateFileOffset)) {
                continue
            }

            $unwindSectionMatchCount++
            $unwindSection = $section
            $unwindFileOffset = $candidateFileOffset
        }
        if ($unwindSectionMatchCount -eq 0) {
            Write-Failure "runtime_function_unwind_not_file_backed" (
                "rva=0x{0:x8} size=4" -f $unwindInfoAddress)
        }
        if ($unwindSectionMatchCount -ne 1) {
            Write-Failure "runtime_function_unwind_section_ambiguous" (
                "rva=0x{0:x8} size=4 matches={1}" -f
                $unwindInfoAddress,
                $unwindSectionMatchCount)
        }

        $unwindCharacteristics = [uint64]$unwindSection.Characteristics
        $unwindContainsCode =
            ($unwindCharacteristics -band [uint64]0x00000020) -ne 0
        $unwindContainsInitializedData =
            ($unwindCharacteristics -band [uint64]0x00000040) -ne 0
        $unwindIsDiscardable =
            ($unwindCharacteristics -band [uint64]0x02000000) -ne 0
        $unwindIsExecutable =
            ($unwindCharacteristics -band [uint64]0x20000000) -ne 0
        $unwindIsReadable =
            ($unwindCharacteristics -band [uint64]0x40000000) -ne 0
        $unwindIsWritable =
            ($unwindCharacteristics -band [uint64]2147483648) -ne 0
        if ($unwindContainsCode -or
            -not $unwindContainsInitializedData -or
            -not $unwindIsReadable -or
            $unwindIsExecutable -or
            $unwindIsWritable -or
            $unwindIsDiscardable) {
            Write-Failure "runtime_function_unwind_section_characteristics_invalid" (
                "section={0} characteristics=0x{1:x8}" -f
                $unwindSection.Name,
                $unwindSection.Characteristics)
        }

        $unwindHeaderOffset = [int]$unwindFileOffset
        $versionAndFlags = $bytes[$unwindHeaderOffset]
        $unwindVersion = $versionAndFlags -band 7
        $unwindFlags = $versionAndFlags -shr 3
        $unwindCodeCount = [int]$bytes[$unwindHeaderOffset + 2]
        if ($unwindVersion -ne 1 -or
            ($unwindFlags -band 0x18) -ne 0 -or
            (($unwindFlags -band 4) -ne 0 -and ($unwindFlags -band 3) -ne 0)) {
            Write-Failure "runtime_function_unwind_header_invalid" "index=$index"
        }

        $alignedUnwindCodeCount = $unwindCodeCount
        if (($alignedUnwindCodeCount -band 1) -ne 0) {
            $alignedUnwindCodeCount++
        }
        $unwindStructureSize = 4 + ($alignedUnwindCodeCount * 2)
        if (($unwindFlags -band 4) -ne 0) {
            $unwindStructureSize += 12
        }
        elseif (($unwindFlags -band 3) -ne 0) {
            $unwindStructureSize += 4
        }

        $unwindStructureEnd =
            [uint64]$unwindInfoAddress +
            [uint64]$unwindStructureSize
        $unwindSectionStart = [uint64]$unwindSection.VirtualAddress
        $unwindSectionVirtualEnd =
            $unwindSectionStart + [uint64]$unwindSection.VirtualSize
        $unwindSectionRawEnd =
            $unwindSectionStart + [uint64]$unwindSection.RawSize
        if ($unwindStructureEnd -gt [uint64]4294967296 -or
            $unwindStructureEnd -gt $unwindSectionVirtualEnd -or
            $unwindStructureEnd -gt $unwindSectionRawEnd -or
            $unwindFileOffset -gt [uint64]$bytes.LongLength -or
            [uint64]$unwindStructureSize -gt
                ([uint64]$bytes.LongLength - $unwindFileOffset)) {
            Write-Failure "runtime_function_unwind_not_file_backed" (
                "rva=0x{0:x8} size={1}" -f
                $unwindInfoAddress,
                $unwindStructureSize)
        }

        $validatedUnwindSizes.Add(
            $unwindInfoAddress,
            $unwindStructureSize)
        $validatedUnwindFileOffsets.Add(
            $unwindInfoAddress,
            $unwindFileOffset)
    }

    if ($beginAddress -eq $serializerRva) {
        $selectedRuntimeFunctionCount++
        $selectedRuntimeFunctionIndex = $index
        $selectedEndAddress = $endAddress
        $selectedUnwindInfoAddress = $unwindInfoAddress
        $selectedUnwindStructureSize = $unwindStructureSize
        $selectedUnwindFileOffset = $unwindFileOffset
    }
    elseif ([uint64]$beginAddress -lt $serializerEndRva -and
        [uint64]$endAddress -gt [uint64]$serializerRva) {
        $overlappingRuntimeFunctionCount++
    }

    $previousRuntimeFunctionPresent = $true
    $previousBeginAddress = $beginAddress
    $previousEndAddress = $endAddress
    $previousUnwindInfoAddress = $unwindInfoAddress
}

if ($selectedRuntimeFunctionCount -eq 0) {
    if ($overlappingRuntimeFunctionCount -ne 0) {
        Write-Failure "serializer_runtime_function_overlap"
    }
    Write-Failure "serializer_runtime_function_missing"
}
if ($selectedRuntimeFunctionCount -ne 1) {
    Write-Failure "serializer_runtime_function_ambiguous" (
        "matches=$selectedRuntimeFunctionCount")
}

if ([uint64]$selectedEndAddress -ne $serializerEndRva) {
    Write-Failure "serializer_runtime_function_range_mismatch" (
        "begin=0x{0:x8} end=0x{1:x8}" -f
        $serializerRva,
        $selectedEndAddress)
}

$selectedUnwindAliasCount = 0
for ($index = 0; $index -lt $runtimeFunctionCount; $index++) {
    if ($index -eq $selectedRuntimeFunctionIndex) {
        continue
    }

    $recordFileOffset =
        [int]([uint64]$exceptionBacking.FileOffset + ([uint64]$index * 12))
    $candidateUnwindInfoAddress = [System.BitConverter]::ToUInt32(
        $bytes,
        $recordFileOffset + 8)
    if ($candidateUnwindInfoAddress -eq $selectedUnwindInfoAddress) {
        $selectedUnwindAliasCount++
    }
}
if ($selectedUnwindAliasCount -ne 0) {
    Write-Failure "serializer_unwind_aliased" (
        "aliases=$selectedUnwindAliasCount")
}

if ($selectedUnwindStructureSize -ne $expectedUnwindInfoBytes.Length) {
    Write-Failure "serializer_unwind_size_mismatch" (
        "size=$selectedUnwindStructureSize")
}
[byte[]]$actualUnwindInfoBytes = Copy-ByteRange `
    $bytes `
    $selectedUnwindFileOffset `
    $expectedUnwindInfoBytes.Length `
    "serializer_unwind_not_file_backed"
if (-not (Test-ByteSequenceEqual $actualUnwindInfoBytes $expectedUnwindInfoBytes)) {
    Write-Failure "serializer_unwind_bytes_mismatch"
}
$runtimeValidationStopwatch.Stop()

Assert-DistinctAuthorityFiles -Records @(
    $artifactAuthority,
    $linkerMapAuthority,
    $linkResponseAuthority,
    $linkEvidenceAuthority,
    $serializerObjectAuthority,
    $serializerLinkedObjectAuthority,
    $managedAssemblyAuthority,
    $linkedMapAuthority
)
Assert-AuthorityInputsUnchanged

Write-Output "EFI_ARTIFACT_VERIFY_PATH $artifactPath"
Write-Output (
    "EFI_ARTIFACT_PE format=PE32+ machine=0x{0:x4} subsystem={1} entryRva=0x{2:x8} entrySection={3}" -f
    $machine,
    $subsystem,
    $entryPointRva,
    $entrySectionName)
Write-Output "EFI_ARTIFACT_IMPORTS import=absent bound-import=absent iat=absent delay-import=absent"
Write-Output (
    "EFI_ARTIFACT_LINK_RESPONSE_SCAN tokens={0} elapsedMs={1} maximumTokens={2}" -f
    $linkResponseTokens.Count,
    $linkResponseValidationStopwatch.ElapsedMilliseconds,
    $maximumLinkResponseTokens)
Write-Output (
    "EFI_ARTIFACT_ENTRY_CONTRACT symbol=EfiMain managedSymbol=OS_OS_Boot_EfiEntry__EfiMain association=same-build-linker-map rva=0x{0:x8}" -f
    $entryPointRva)
Write-Output (
    "EFI_ARTIFACT_EXCEPTION_DIRECTORY section={0} rva=0x{1:x8} size={2} records={3}" -f
    $exceptionBacking.Section.Name,
    $exceptionDirectory.Rva,
    $exceptionDirectory.Size,
    $runtimeFunctionCount)
Write-Output (
    "EFI_ARTIFACT_RUNTIME_SCAN records={0} distinctUnwind={1} elapsedMs={2} maximumRecords={3}" -f
    $runtimeFunctionCount,
    $validatedUnwindSizes.Count,
    $runtimeValidationStopwatch.ElapsedMilliseconds,
    $maximumRuntimeFunctionRecords)
Write-Output (
    "EFI_ARTIFACT_SERIALIZER symbol={0} section={1} rva=0x{2:x8} size={3} unwindRva=0x{4:x8} sha256={5} expectedCookie=0x{6:x8}" -f
    $serializerSymbol,
    $serializerBacking.Section.Name,
    $serializerRva,
    $serializerSizeBytes,
    $selectedUnwindInfoAddress,
    $actualSerializerSha256,
    $serializerExpectedSuccessCookie)
Write-Output (
    "EFI_ARTIFACT_SERIALIZER_OBJECT path={0} linkedPath={1} bytes={2} sha256={3}" -f
    $serializerObjectFullPath,
    $serializerLinkedObjectFullPath,
    $serializerObjectLength,
    $serializerObjectActualSha256)
Write-Output "EFI_ARTIFACT_LINK_MAP path=$linkerMapFullPath sha256=$linkerMapSha256"
Write-Output "EFI_ARTIFACT_LINK_RESPONSE path=$linkResponseFullPath sha256=$linkResponseSha256"
Write-Output "EFI_ARTIFACT_SHA256 $sha256"
Write-Output "TEST_PASS $testName"
Close-AuthorityReadHandles
exit 0
