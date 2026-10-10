namespace OS.Tooling;

public static partial class EfiContracts
{
    // Reviewed production source identities copied from the admitted contracts.
    // Normalize only CRLF to LF for this comparison; provenance always uses raw bytes.
    public static IReadOnlyDictionary<string, string> SourceIdentities { get; } =
        new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [@"..\..\std\no-runtime\shared\Memory\MemoryOrder.cs"] = "c26985ed287bda9192fb5cac309242b4da6b77866f6b0d71b518281f183b724f",
                [@"..\OS.Kernel\Drivers\VirtqueueSplitRingPlan.cs"] = "3634f2372fda144886de555a65d9395dfa339b39d9a4b4b2c9a88db785220291",
                [@"..\OS.Kernel\Drivers\MemoryOrderingPrimitivesReplacingReadinessBooleans.cs"] = "a202423035bc37a5ae1de5e0fc76898ddb0d3c510d69bd9f8f13c359d1e8e3c0",
                [@"..\OS.Kernel\Drivers\SplitVirtqueuePollingNotificationCompletion.cs"] = "9000d3da288a8ee64717337f6c57664482fe5eed753a62d6146b54d71963253b",
                [@"..\OS.Kernel\Drivers\DescriptorAllocationExhaustionInvalidIndexDoubleFreeTests.cs"] = "0c7a5ec7a24178fd075983581b70e1d9ba60b6c8843864eeb562a258aa05cb34",
                [@"..\OS.Kernel\Drivers\VirtqueuePowerTwoConstraintsIndexWidthNegotiatedMaximum.cs"] = "22022fb0a81fec7d89e71960137a909fe0998518565f98deeddb4041d55ae7e4",
                [@"..\OS.Kernel\Drivers\VirtioPciModernRegisterMap.cs"] = "4a169747f11fda8d4d58b5c3a6e8e60901858555c95e7173e6a3d8defcf145c3",
                [@"..\OS.Kernel\Drivers\VirtioPciCapabilities.cs"] = "53be92183aacc4cdaef36569dce5113e0f193ea5c77c8b4a61e31e71fba6c13a",
                [@"..\OS.Kernel\Drivers\UnsupportedDeviceFeaturesFailedFEATURESOKState.cs"] = "31f4dec656731b926cd4d8f3858b6c486f85fa2f0861180ac6933e8b948eb907",
                [@"..\OS.Kernel\Drivers\ResetACKNOWLEDGEDRIVERFEATURESOKNegotiation.cs"] = "e0a388b7720eac74138e8268b3d8c2fc652475560ddc9287eb7be35b01d09edf",
                [@"..\OS.Kernel\Drivers\CommonNotifyISRDeviceConfigurationRegions.cs"] = "3b9b0facaf00f9a4fbd5b42fa183afe8bf707c8d81919a2e926bab389f0cd7de",
                [@"..\OS.Kernel\Drivers\ModernVirtioPCICapabilitiesLiveConfigurationSpace.cs"] = "07231386b562c3032f72b087938a51b2775cdc71d5072e7b2cd2d705e2f52664",
                [@"..\OS.Kernel\Drivers\PageAlignedDMABuffersLifetimeOwnershipTracking.cs"] = "dc1cae865f61f981e8f2aaeb07b3ed44595b66d641271ae386a4168de23bbd54",
                [@"..\OS.Kernel\Platform\ExplicitCPUVirtualPhysicalDeviceAddressDMAMappings.cs"] = "682042363e88831f1b5a091da9065ea061d273887918c06f0e0d211d39073ce8",
                [@"..\OS.Kernel\Drivers\BARPrefetchTypeRegressionTestsDerivedMOSAFailures.cs"] = "58dacf1fad3b7dad998c592b23ba51b65f502a9395bb41791f3f5cecfbe35e38",
                [@"..\OS.Kernel\Drivers\N32BitN64BitBARsWhileAlwaysRestoring.cs"] = "5944877575d9e3dc980d940426a19ab56468c10bd2531fd9f722c3e3cb7b2509",
                [@"..\OS.Kernel\Boot\VerifiedExitPathOptDefaultRepeatedCleanQEMU.cs"] = "88bcd72084f8398e8dd97bcc878976af7c90e2e859b9c2268ac97215049eb9a8",
                [@"..\OS.Kernel\Runtime\ObjectArrayStringEETypeMethodTableLayoutContracts.cs"] = "ab9fd84a8010567c58111ab8c336ddac3e27c1942676c34e3ef31b51aa550225",
                [@"..\OS.Kernel\Runtime\ManagedHeapPageRangeLiveAllocator.cs"] = "43cdf5af449f7963f07b200ec494da3d8538f1ceb2f01fb3dbe9c29edebcb070",
                [@"..\OS.Kernel\Runtime\GuestTestRhpNewFast.cs"] = AllocationSourceSha256,
                [@"..\OS.Kernel\Boot\ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.cs"] = "9c42726c9c0b182dabafd06003d56ef6242469e8d412ea248f1918f9311d1434",
                [@"..\OS.Kernel\Runtime\CompilerClassConstructorExecution.cs"] = "a445fe4795291164be348c785e138b53d1648695c5e78ba93012413128269ade",
                [@"..\OS.Kernel\Runtime\CompilerInterfaceDispatch.cs"] = "fcab48599866752748409c9ef7b900b91341dc1f09620826009a789ed092f3dd",
                [@"..\OS.Kernel\Runtime\ExceptionTypeMatch.cs"] = "b51523326e1ecaa87e20fe43f1fb7ab1b587fcbb3f3e7c28a7d2830d6d32146e",
                [@"..\OS.Kernel\Runtime\X64FrameUnwind.cs"] = "b7cc8b10a5c1a0e819745d31e81b4915164645b17f172063522f32167afb3733",
                [@"..\OS.Kernel\Runtime\NativeAotEhClauseMetadata.cs"] = "b01c67219c897cce58c756b72849a90f8032f10a222f0b4fe9e37e91bacc4d03",
                [@"..\OS.Kernel\Runtime\X64PERuntimeFunctionUnwindMetadataHostileInput.cs"] = "12fac853b9dc2357d25315e4bedd0e43df0af76854079b7ae030bca36aed5253",
                [@"..\OS.Kernel\Diagnostics\Probes.cs"] = "5867d0c68245ae63557c662d7baab2b61e9f6305dfe80b81bf890d6b556ee180",
                [@"..\OS.Kernel\Boot\OwnedAllocatorDescriptorTablesACPISnapshotPanicOutput.cs"] = "88053c5a7a7c46aae39e0c68d03d3dd6ab83047640e3a22455148e53023a44fe",
                [@"..\OS.Kernel\Platform\PostFirmwarePhysicalMappingExplicitUCWCWB.cs"] = "305c825611db57ddfdb446e6dabc0dd234a91e3c4d4bd050e9aaaf9b8ea57cb5",
                [@"..\OS.Kernel\Boot\FirstOptRealExitBootServicesTransition.cs"] = "916abd5e7e68724a521a4f7b70c25b0428d0b35342b1409953457f593f5faf14",
                [@"..\..\std\no-runtime\shared\Hash\Crc32.cs"] = "228540b7c150ea26100ea9f509772ee89bbe10a45ef2c4d8e95a4b250c56333e",
                [@"..\..\std\no-runtime\shared\DeterministicRandom32.cs"] = "6ca5e3e3e839017ca27fb73884b7665315b3e2df3c1f49a5fe012a3e56c2ae33",
                [@"..\OS.Kernel\Runtime\SeededAllocationChurnCollectionRootRetentionGuestProbes.cs"] = "f708e1192e6964dc43f5cdda86caee6656dbc155c3699be71566381b9725fc5c",
                [@"Runtime\SeededCollectorChurnProbe.cs"] = "69aeb22b1c23f17f7748b07b68629698216da53a3d9b39fa6f6aac6bc17e6768",
                [@"..\OS.Kernel\Diagnostics\SeparateDisabledDefaultPageFaultPageTables.cs"] = "ae976833366ca1dae212d912ef877bc6ddf0bcc78e5eb8367dc6c67cf80e4282",
                [@"..\OS.Kernel\Runtime\TypeManagerModuleMetadataLiveNativeAOTImage.cs"] = "691f6e243d175dbc83db1446a8e1863fab3a2cfa1497c975f43608c3456165da",
                [@"..\OS.Kernel\Runtime\GCStaticRootsMalformedMetadata.cs"] = "9f9e2efbfbdec20067dc9e6402f79df4c1e0affc29b7affc92049e7ce6d1a313",
                [@"..\OS.Kernel\Runtime\BoundedNonMovingCollectorExplicitRootEnumeration.cs"] = "c97db9db55ba766c9ebb6e8979993c24268886d3da2b2f46289ea15e99841b04",
                [@"..\OS.Kernel\Boot\TypedNoStdlibExitBootServicesCallAdapter.cs"] = "7227e860b5e4f97d47c9c269b889c975e30f21c506f9ecb51424f5417e2945db",
                [@"..\OS.Kernel\Boot\NoAllocationOrUnrelatedFirmwareCallOccursBetween.cs"] = "039407d33d94265898dcd6ac9239a3d011e93594bb3631e14f76eb7529af8315",
                [@"..\OS.Kernel\Boot\BoundedRetryStaleKeyStatus.cs"] = "efe748c857a600081bb4db7f54c4823113f8d334c5cf81a1d8ff8760191e6633",
                [@"..\..\std\no-runtime\shared\Numerics\IndependentArithmeticShiftCastN64BitDivisionRemainder.cs"] = "29397b76b8f7f6437cb5a2b54643cfab824c4a403cdbfac9cf5746f6ee9a23eb",
                [@"..\..\std\no-runtime\shared\Memory\SpanOverlapMemoryCastUnsafeWidthConformanceVectors.cs"] = "0c818f89ecc761a4e5692ce3970e98d577381b98bbe540f6ec5f154130c12153",
            });
}
