using OS.Tooling;

internal static class CSharpEfiQueryDeadlineTests
{
    [HostTest("EFI source query deadline is bounded and participates in cache identity")]
    private static async Task DeadlineControls()
    {
        var profile = EfiOptions.Parse([], false).Profile();
        var deadline = typeof(CSharpEfiProjectEvaluation).GetMethod(nameof(CSharpEfiProjectEvaluation.LoadAsync))!
            .GetParameters().Last();
        HostCheck.True(deadline.Name == "queryTimeoutSeconds" && deadline.IsOptional && Equals(deadline.DefaultValue, 120),
            "existing callers retain the original shared default deadline");
        string ordinary = HostSourceCatalog.EfiCacheKey(profile, "Release", "SingleMinimalApi", 120);
        string original = HostSourceCatalog.EfiCacheKey(profile, "Release", "SingleMinimalApi", 60);
        HostCheck.True(ordinary != original
            && original == HostSourceCatalog.EfiCacheKey(profile, "Release", "SingleMinimalApi", 60)
            && original != HostSourceCatalog.EfiCacheKey(profile, "Release", "MultiMinimalApi", 60),
            "selected deadline and workload remain distinct cache contexts");
        foreach (int timeout in new[] { 0, -1, 121, int.MaxValue })
        {
            bool keyRejected = false;
            bool queryRejected = false;
            try
            {
                _ = HostSourceCatalog.EfiCacheKey(profile, "Release", "SingleMinimalApi", timeout);
            }
            catch (ArgumentOutOfRangeException error) when (error.ParamName == "queryTimeoutSeconds")
            {
                keyRejected = true;
            }
            try
            {
                _ = await CSharpEfiProjectEvaluation.LoadAsync("\0", profile, queryTimeoutSeconds: timeout);
            }
            catch (ArgumentOutOfRangeException error) when (error.ParamName == "queryTimeoutSeconds")
            {
                queryRejected = true;
            }
            HostCheck.True(keyRejected && queryRejected, "invalid deadline rejected before path or process work");
        }
        HostCheck.True(HostSourceCatalog.EfiCacheKey(profile, "Release", "SingleMinimalApi", 1).Length != 0,
            "minimum valid deadline is representable without starting a process");
    }
}
