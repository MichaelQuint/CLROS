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
    internal static IReadOnlyDictionary<string, IReadOnlyList<string>> FirmwareEntropyHostOracleTypes { get; } =
        new System.Collections.ObjectModel.ReadOnlyDictionary<string, IReadOnlyList<string>>(
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
            {
                ["OS.Boot.FirmwareEntropySeedStatus"] = Array.AsReadOnly<string>(
                    ["6fd99622e8a15176f99bdd597bd2bde8df0ee5a6cbebe0b66b2532ba41e1c60b"]),
                ["OS.Boot.FirmwareEntropySeedPublication"] = Array.AsReadOnly<string>(
                    ["29d748b2a0bc475f1ab4e4b3c738ad214593159c82884b85c499b521c28d7661"]),
                ["OS.Boot.FirmwareEntropySeedResult"] = Array.AsReadOnly<string>(
                    ["e7a68440cad2eb395f15c4708099f89b198db8dd6c2881560f9a8e5ffec442dc"]),
                ["OS.Boot.AnyFirmwareDerivedEntropySeedNeededFirmwareShutdown"] = Array.AsReadOnly<string>(
                    ["f592c6183b12d8df12c4c028d0cd1a9396ac15692e1cba1417fb7ff6737ebf61"])
            });

    // Complete token-equivalent descendants of the original raw checked-reference
    // host witness; raw build inputs remain separately recorded in provenance.
    internal const string CheckedReferenceHostOracleSha256 =
        "8f40b7416297c73ee252d25573ef84755c48b27a7c3e60f498c708d6c3c68021";

    internal static IReadOnlyDictionary<string, IReadOnlyList<string>> CheckedReferenceHostOracleTypes { get; } =
        new System.Collections.ObjectModel.ReadOnlyDictionary<string, IReadOnlyList<string>>(
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
            {
                ["OS.Kernel.Runtime.CheckedReferenceBulkProfile"] = Array.AsReadOnly<string>(["9966f2b0c9706ae7950cc92efefaedd2b95a440be882b2c3a58725455e887039"]),
                ["OS.Kernel.Runtime.CheckedReferenceBulkResult"] = Array.AsReadOnly<string>(["d280bd1a186da74f4b4928353b286defddd1ee2b7b26693e2fef3f609fbf7a2a"]),
                ["OS.Kernel.Runtime.CheckedReferenceBulkStatus"] = Array.AsReadOnly<string>(["79c81ab110d2f17252a3c147d7715942e5826a2e368b84b7a86948311170b48c"]),
                ["OS.Kernel.Runtime.CheckedReferenceBulkBarriersIncludingDotNetN10Bulk"] = Array.AsReadOnly<string>(["97a5363a661370a3469893a8c77a3b494d2cd73f66d356ef3c2335b4d1d3b81f"])
            });
    // The original raw witness remains recorded; both default and NOSTDLIB_EFI
    // declaration inventories were independently compared before these split pins.
    internal const string OwnedGdtHostOracleSha256 =
        "0f8d0828da8228030276232cb5b6077513572d3d5fd76d52cd9ec27d9bcfff04";

    internal static IReadOnlyDictionary<string, IReadOnlyList<string>> OwnedGdtHostOracleTypes { get; } =
        new System.Collections.ObjectModel.ReadOnlyDictionary<string, IReadOnlyList<string>>(
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
            {
                ["OS.Kernel.Diagnostics.OwnedX64GdtEncodingStatus"] = Array.AsReadOnly<string>(["d891a5cbe1a95b91e13ad83dd6d931ef2ed9b1d476cda2c2e34cb457556c0bcb"]),
                ["OS.Kernel.Diagnostics.OwnedX64GdtVerificationStatus"] = Array.AsReadOnly<string>(["bd797218e5daeb859a19b42aed16ad0591cc76aa9225c34223669123fd058c48"]),
                ["OS.Kernel.Diagnostics.OwnedX64GdtVerificationResult"] = Array.AsReadOnly<string>(["11fe12ed958b28bd7982ebc753e90ed5cc31f7bc4e90f75c2a166ce6a3dd72a7"]),
                ["OS.Kernel.Diagnostics.OwnedX64GdtEncodingResult"] = Array.AsReadOnly<string>(["c8dcced54de5e810acd20ed5f9e55b3af5c36d2909f1b1c04be846930b1ace2f"]),
                ["OS.Kernel.Diagnostics.OwnedX64GdtContainingNullN64BitCode"] = Array.AsReadOnly<string>(["8a487dd47383d6485a9d282e60790224b9cd5c996e5c4073b1e91bf7903c619b"])
            });
    // Complete descendants of the original guarded-IST host witness, reviewed
    // with and without NOSTDLIB_EFI; native admission remains separate.
    internal const string GuardedIstHostOracleSha256 =
        "cb3deb08256d85ec85cc017aa30938498705ae65956d69235993a411e4fb5703";

    internal static IReadOnlyDictionary<string, IReadOnlyList<string>> GuardedIstHostOracleTypes { get; } =
        new System.Collections.ObjectModel.ReadOnlyDictionary<string, IReadOnlyList<string>>(
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
            {
                ["OS.Kernel.Diagnostics.GuardedIstStackKind"] = Array.AsReadOnly<string>(["918d93b8eb6eaba3cef7f675ebad54c38019f3fbc47f6d21c54cb587462bad1e"]),
                ["OS.Kernel.Diagnostics.GuardedIstStackArenaStatus"] = Array.AsReadOnly<string>(["debf873893ace088ed1d631ad5c1638be43624d0a1c6909eab5c8950e2babfa9"]),
                ["OS.Kernel.Diagnostics.GuardedIstStackAllocationStatus"] = Array.AsReadOnly<string>(["c504f87a9058b916ee0b7328095cc70334d22630189fe00cbd1f461cc50f8154"]),
                ["OS.Kernel.Diagnostics.GuardedIstStackReleaseStatus"] = Array.AsReadOnly<string>(["3341f6633451c9aacb1df9c537e469890cd1419fed74b043f074772085672944"]),
                ["OS.Kernel.Diagnostics.GuardedIstStackArenaRequest"] = Array.AsReadOnly<string>(["da1d1a8a44524904862383eafc62a6fe1aeb3e055124d8f012953207ac16d37b"]),
                ["OS.Kernel.Diagnostics.GuardedIstStackArenaInitializationResult"] = Array.AsReadOnly<string>(["0e5595b8179471bab68469cf9cb6f63cfdf81978797f2ec687c34113d45bc95b"]),
                ["OS.Kernel.Diagnostics.GuardedIstStackDescriptor"] = Array.AsReadOnly<string>(["171300ea57d7bd474792c5795b87a49f0957a9977b7234d1b19fb353d1494be0"]),
                ["OS.Kernel.Diagnostics.GuardedIstStackLease"] = Array.AsReadOnly<string>(["a11874902daee1981cc15c504937fdc71361882d209d26b13c08538352eb581b"]),
                ["OS.Kernel.Diagnostics.GuardedIstStackAllocatorState"] = Array.AsReadOnly<string>(["462a4ac9dc2895a5e7886ae7cf5dbd6f1575b0fdc4e37a867fa1d6c5de27543d"]),
                ["OS.Kernel.Diagnostics.GuardedIstStackAllocationResult"] = Array.AsReadOnly<string>(["606fd6913f19a3fdf54d403b0328bba60acf4ea32ed455af806b2165a87306b4"]),
                ["OS.Kernel.Diagnostics.GuardedIstStackReleaseResult"] = Array.AsReadOnly<string>(["5c9229d9fe571984e179ca42231346af68ed5b8677051c6dfb421d2c1abcb1aa"]),
                ["OS.Kernel.Diagnostics.GuardedISTStacksDoubleFaultPageFaultNMI"] = Array.AsReadOnly<string>(["c1113381b5a4dfb56c34e9d541d37a6eff11aa60f419085253ca4e25ec05c9b4"])
            });

    internal const string NormalizedFrameHostOracleSha256 =
        "B66CD19B45E8198897B837DF9EDA6934AFBE087DC141C468BD0DB44E62724DA1";

    internal static IReadOnlyDictionary<string, IReadOnlyList<string>> NormalizedFrameHostOracleTypes { get; } =
        new System.Collections.ObjectModel.ReadOnlyDictionary<string, IReadOnlyList<string>>(
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
            {
                ["OS.Kernel.Diagnostics.NormalizeInterruptFramePageOperation"] = Array.AsReadOnly<string>(["9e399a79907f5db3abe866d033cae9da9274c3f3ae8c9e1abc8d9843a909297c"]),
                ["OS.Kernel.Diagnostics.NormalizeInterruptFramePageStatus"] = Array.AsReadOnly<string>(["85f0a2fb02e82c71ca619d76dc980cc045f17e265c0e651e8c67c265f805c131"]),
                ["OS.Kernel.Diagnostics.NormalizeInterruptFramePageResult"] = Array.AsReadOnly<string>(["eb9c99c0bbeb87b0f9c22a88c6746d7a0276132ce87cd12016e9879269c240b7"]),
                ["OS.Kernel.Diagnostics.NormalizeErrorCodeNonErrorCodeInterruptFrame"] = Array.AsReadOnly<string>(["b99f393d82e3ce9c5ce11c494254a9b5f4985014db74208bb494f28adee9e9b4"]),
            });
    internal const string OwnedIdtHostOracleSha256 =
        "00EC277C3F6A0DB49CB54B34810202EE1D508318B29733C3F0392A7BC36CFA38";

    internal static IReadOnlyDictionary<string, IReadOnlyList<string>> OwnedIdtHostOracleTypes { get; } =
        new System.Collections.ObjectModel.ReadOnlyDictionary<string, IReadOnlyList<string>>(
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
            {
                ["OS.Kernel.Diagnostics.OwnedIdtGateTableOperation"] = Array.AsReadOnly<string>(["e6e0abbbbc2004d74f401d3422dd2a781b0c46955c77ea9ab12210592ee9da7a"]),
                ["OS.Kernel.Diagnostics.OwnedIdtGateTableStatus"] = Array.AsReadOnly<string>(["6ef4e5d3fc897b21d82ab80f54e70cc41ee8409a850d1cebc10d04be37cfa3c3"]),
                ["OS.Kernel.Diagnostics.OwnedIdtGateTableResult"] = Array.AsReadOnly<string>(["ee347e54a156dba03b41fd050d043354a1530f2833216f4079e5ad4f82ee197e"]),
                ["OS.Kernel.Diagnostics.PopulateAllN256IDTVectorsOwnedGatesNever"] = Array.AsReadOnly<string>(["e6582e6ca7da225a22f3a979a5a57f87105f7ce19e5f6d1ce53cfb68c24aa4e2"]),
            });
}
