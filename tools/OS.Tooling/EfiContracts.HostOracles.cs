namespace OS.Tooling;

public static partial class EfiContracts
{
    // Reviewed independent host-oracle pins retained from the original firmware
    // fixtures. These are test witnesses, not native source-admission keys.
    public const string AcpiBootstrapHostOracleSha256 = "2afb2b08c511ac8a25ce3f8818e7730961ab3f53c032ca6b846e0bb634624ae8";
    public const string FirmwareEntropyHostOracleSha256 = "b76fef791119978524f7e159f99d26087d48bd33f292abcb5422b6fe7c4000bc";

    // Reviewed descendants of the immutable raw entropy oracle above.
    // The host consumer binds logical types, checks complete source-text pins,
    // then admits only UTF-8/no-BOM LF or CRLF bytes for each pinned document.
    internal static IReadOnlyDictionary<string, IReadOnlyList<string>> FirmwareEntropyHostOracleTypes
    {
        get;
    } =
        new System.Collections.ObjectModel.ReadOnlyDictionary<string, IReadOnlyList<string>>(
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
            {
                ["OS.Boot.FirmwareEntropySeedStatus"] = Array.AsReadOnly<string>(
                    ["d8262e9ec3983c077f21ec6d2e897b4770bb25d56371d3d2d1942213cfbaf236"]),
                ["OS.Boot.FirmwareEntropySeedPublication"] = Array.AsReadOnly<string>(
                    ["ee608cf9a35034d8b299165ac7dd1d121222d18939f51501d042d366744bd4b4"]),
                ["OS.Boot.FirmwareEntropySeedResult"] = Array.AsReadOnly<string>(
                    ["1ddf2c459d0a23edb237ab2c71c7be766c5d598a2be9590fa8e357d21be5c473"]),
                ["OS.Boot.AnyFirmwareDerivedEntropySeedNeededFirmwareShutdown"] = Array.AsReadOnly<string>(
                    ["f592c6183b12d8df12c4c028d0cd1a9396ac15692e1cba1417fb7ff6737ebf61"])
            });

    // Complete token-equivalent descendants of the original raw checked-reference
    // host witness; raw build inputs remain separately recorded in provenance.
    internal const string CheckedReferenceHostOracleSha256 =
        "8f40b7416297c73ee252d25573ef84755c48b27a7c3e60f498c708d6c3c68021";

    internal static IReadOnlyDictionary<string, IReadOnlyList<string>> CheckedReferenceHostOracleTypes
    {
        get;
    } =
        new System.Collections.ObjectModel.ReadOnlyDictionary<string, IReadOnlyList<string>>(
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
            {
                ["OS.Kernel.Runtime.CheckedReferenceBulkProfile"] = Array.AsReadOnly<string>(["9966f2b0c9706ae7950cc92efefaedd2b95a440be882b2c3a58725455e887039"]),
                ["OS.Kernel.Runtime.CheckedReferenceBulkResult"] = Array.AsReadOnly<string>(["2769020a813e357ca32d05fd18cbe3f1ba51b2356edc07536f55b71f4cb4956e"]),
                ["OS.Kernel.Runtime.CheckedReferenceBulkStatus"] = Array.AsReadOnly<string>(["f2e90260cc39f7aac3424e2059f5dc46c95fae7e0785408e2cf6c99fd72a2557"]),
                ["OS.Kernel.Runtime.CheckedReferenceBulkBarriersIncludingDotNetN10Bulk"] = Array.AsReadOnly<string>(["97a5363a661370a3469893a8c77a3b494d2cd73f66d356ef3c2335b4d1d3b81f"])
            });
    // The original raw witness remains recorded; both default and NOSTDLIB_EFI
    // declaration inventories were independently compared before these split pins.
    internal const string OwnedGdtHostOracleSha256 =
        "0f8d0828da8228030276232cb5b6077513572d3d5fd76d52cd9ec27d9bcfff04";

    internal static IReadOnlyDictionary<string, IReadOnlyList<string>> OwnedGdtHostOracleTypes
    {
        get;
    } =
        new System.Collections.ObjectModel.ReadOnlyDictionary<string, IReadOnlyList<string>>(
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
            {
                ["OS.Kernel.Diagnostics.OwnedX64GdtEncodingStatus"] = Array.AsReadOnly<string>(["0a1473f9e0cd009ad6515a0157a79fc16f06bd75f5b18afccc96a58a2bde2ffe"]),
                ["OS.Kernel.Diagnostics.OwnedX64GdtVerificationStatus"] = Array.AsReadOnly<string>(["8e6a2352b9e0961ef0f0a1b753c2bb2a722452292d0a34ef9e9b3ee86dcce004"]),
                ["OS.Kernel.Diagnostics.OwnedX64GdtVerificationResult"] = Array.AsReadOnly<string>(["638c8649d71e4b446307afb58174c186256faaab52980951f31cf8546819975c"]),
                ["OS.Kernel.Diagnostics.OwnedX64GdtEncodingResult"] = Array.AsReadOnly<string>(["bcf88ddd01737df49f2afdc90b08f838a13ca19c9ad5c839f233bf9d3c5b5484"]),
                ["OS.Kernel.Diagnostics.OwnedX64GdtContainingNullN64BitCode"] = Array.AsReadOnly<string>(["28b5aa8077dc692e5807a1a3445a162eb6fb8a492fed0fe70f6323b5026f9ad3"])
            });
    // Complete descendants of the original guarded-IST host witness, reviewed
    // with and without NOSTDLIB_EFI; native admission remains separate.
    internal const string GuardedIstHostOracleSha256 =
        "cb3deb08256d85ec85cc017aa30938498705ae65956d69235993a411e4fb5703";

    internal static IReadOnlyDictionary<string, IReadOnlyList<string>> GuardedIstHostOracleTypes
    {
        get;
    } =
        new System.Collections.ObjectModel.ReadOnlyDictionary<string, IReadOnlyList<string>>(
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
            {
                ["OS.Kernel.Diagnostics.GuardedIstStackKind"] = Array.AsReadOnly<string>(["e0b2b7b6fa2d8b16eb2ed005333889060094be9ac2ef9d14e9939473bd368900"]),
                ["OS.Kernel.Diagnostics.GuardedIstStackArenaStatus"] = Array.AsReadOnly<string>(["080ac0f7a686c36e7c28f8cc86d5bd33010beab293420bb6167ebddb34310af5"]),
                ["OS.Kernel.Diagnostics.GuardedIstStackAllocationStatus"] = Array.AsReadOnly<string>(["34acf75e59d51dc5db838e9e83ec582ba70d5a7da5fb7ede353f418ec57fed33"]),
                ["OS.Kernel.Diagnostics.GuardedIstStackReleaseStatus"] = Array.AsReadOnly<string>(["ea76d06c4bbc5d506dd38fd5b31bc957c3215a523a285c4ca694610a11cca503"]),
                ["OS.Kernel.Diagnostics.GuardedIstStackArenaRequest"] = Array.AsReadOnly<string>(["de67773d6feabffb40070493b6c6b28fd0c1cb8c1f09890185cc0fb1886ba14d"]),
                ["OS.Kernel.Diagnostics.GuardedIstStackArenaInitializationResult"] = Array.AsReadOnly<string>(["dc1c703702b411bbe834f88daf91f3e1e2f210f9fd68dfaf40856c15bdddb2b0"]),
                ["OS.Kernel.Diagnostics.GuardedIstStackDescriptor"] = Array.AsReadOnly<string>(["4bc4db87d767e04c2b25f331f8394816e7b2f0e307d38fd4c39c3d86188cae78"]),
                ["OS.Kernel.Diagnostics.GuardedIstStackLease"] = Array.AsReadOnly<string>(["4472d154088ab2af72df72dcdde51b1409623d6ccb8812f63f59fd1270d44ab4"]),
                ["OS.Kernel.Diagnostics.GuardedIstStackAllocatorState"] = Array.AsReadOnly<string>(["9be2395ce03ac95b7415169258af307d8f185e315b4e7c3e533724fe1524bf3a"]),
                ["OS.Kernel.Diagnostics.GuardedIstStackAllocationResult"] = Array.AsReadOnly<string>(["4afcd4176b26fc8faf0744f9345aaa640ce1ca0c606e8084e2e9b5888b709db2"]),
                ["OS.Kernel.Diagnostics.GuardedIstStackReleaseResult"] = Array.AsReadOnly<string>(["78f5a017c73849ae2208e7c48eebc762f6461a0c39cf7a357d2cdc4e0312219c"]),
                ["OS.Kernel.Diagnostics.GuardedISTStacksDoubleFaultPageFaultNMI"] = Array.AsReadOnly<string>(["37eca25202354f7a035671c582ac783c292bc3da59c4ecd12a72ee51db578f1a"])
            });

    internal const string NormalizedFrameHostOracleSha256 =
        "B66CD19B45E8198897B837DF9EDA6934AFBE087DC141C468BD0DB44E62724DA1";

    internal static IReadOnlyDictionary<string, IReadOnlyList<string>> NormalizedFrameHostOracleTypes
    {
        get;
    } =
        new System.Collections.ObjectModel.ReadOnlyDictionary<string, IReadOnlyList<string>>(
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
            {
                ["OS.Kernel.Diagnostics.NormalizeInterruptFramePageOperation"] = Array.AsReadOnly<string>(["f859ea5e594184686053903315161a60bb4769782ebb4a3f697acb7430187f7b"]),
                ["OS.Kernel.Diagnostics.NormalizeInterruptFramePageStatus"] = Array.AsReadOnly<string>(["77082d6f6a47bce3409464aef992d0f844b8ebbc8b76e7a46bb7ac17c8c94983"]),
                ["OS.Kernel.Diagnostics.NormalizeInterruptFramePageResult"] = Array.AsReadOnly<string>(["158a20f52c51b6b766d2e23e9af8bbb6cc36fb18c1caa2595d6b68072e405acc"]),
                ["OS.Kernel.Diagnostics.NormalizeErrorCodeNonErrorCodeInterruptFrame"] = Array.AsReadOnly<string>(["b99f393d82e3ce9c5ce11c494254a9b5f4985014db74208bb494f28adee9e9b4"]),
            });
    internal const string OwnedIdtHostOracleSha256 =
        "00EC277C3F6A0DB49CB54B34810202EE1D508318B29733C3F0392A7BC36CFA38";

    internal static IReadOnlyDictionary<string, IReadOnlyList<string>> OwnedIdtHostOracleTypes
    {
        get;
    } =
        new System.Collections.ObjectModel.ReadOnlyDictionary<string, IReadOnlyList<string>>(
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
            {
                ["OS.Kernel.Diagnostics.OwnedIdtGateTableOperation"] = Array.AsReadOnly<string>(["f7e4d94a2271dfa7c4ffe4041b3bc3e3b528796b5278abaf5deb200738911f8b"]),
                ["OS.Kernel.Diagnostics.OwnedIdtGateTableStatus"] = Array.AsReadOnly<string>(["04a01fa0e7334af2297e7dfcc1d5b378772a5fa4325845bb3bd6b6798cb4ca58"]),
                ["OS.Kernel.Diagnostics.OwnedIdtGateTableResult"] = Array.AsReadOnly<string>(["cca75708872514259d5afd0c1dd4ef18192eda4d5590acf92366cfd602f827ef"]),
                ["OS.Kernel.Diagnostics.PopulateAllN256IDTVectorsOwnedGatesNever"] = Array.AsReadOnly<string>(["e6582e6ca7da225a22f3a979a5a57f87105f7ce19e5f6d1ce53cfb68c24aa4e2"]),
            });
}
