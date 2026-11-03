namespace OS.Tooling;

internal static partial class NativeAotCurrentSources
{
    // Intentional consumer behavior changes, separate from token-equivalent splits.
    // The socket publisher now includes every reviewed current app helper in its
    // admission and before/after publication snapshot. Recipe, leases, original
    // evidence joins and native output checks are unchanged. This revision grants
    // no native publication or guest qualification to the refactored sources.
    private static readonly Dictionary<string, (string OriginalHash, string CurrentHash)> ReviewedRevisions = new(StringComparer.Ordinal)
    {
        ["tools/OS.Tooling/NativeAotSocketFixturePublisher.cs"] = (
            "A9029B6AFD17828D7182B7BFC2E971857BDF9469BA38D570AC6E6EBBCD197494",
            "D56CF81EA00D5BAD4389FDEE4792EC24BE0311CCC8B497F23E02702D3F116323"),
    };
}
