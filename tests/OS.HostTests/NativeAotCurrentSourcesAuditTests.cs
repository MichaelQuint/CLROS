using OS.Tooling;

internal static class NativeAotCurrentSourcesAuditTests
{
    [HostTest("reviewed source audit derives every current registered input and rejects source drift")]
    private static void RegisteredSources()
    {
        var inputs = NativeAotCurrentSources.ReviewedSourcesForAudit();
        HostCheck.True(inputs.Count != 0);
        HostCheck.Equal(inputs.Count, inputs.Select(input => input.Path).Distinct(StringComparer.Ordinal).Count());
        var failures = NativeAotCurrentSources.InspectReviewedSources(HostToolProcess.FindRepositoryRoot());
        HostCheck.Equal(0, failures.Count, string.Join(";", failures.Select(failure => failure.Contract + "=" + failure.Actual)));
    }

    [HostTest("reviewed source audit rejects changed missing and inactive unregistered producer files")]
    private static void Rejections()
    {
        string repository = HostToolProcess.FindRepositoryRoot();
        string root = Directory.CreateTempSubdirectory("clros-reviewed-source-audit-").FullName;
        try
        {
            var inputs = NativeAotCurrentSources.ReviewedSourcesForAudit();
            foreach (var input in inputs)
            {
                string target = Path.Combine(root, input.Path);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(Path.Combine(repository, input.Path), target);
            }

            HostCheck.Equal(0, NativeAotCurrentSources.InspectReviewedSources(root).Count);
            var selected = inputs.First(input => input.Path.EndsWith(".cs", StringComparison.Ordinal));
            string path = Path.Combine(root, selected.Path);
            byte[] original = File.ReadAllBytes(path);
            File.AppendAllText(path, "\n// unreviewed raw byte change\n");
            var changed = NativeAotCurrentSources.InspectReviewedSources(root);
            HostCheck.Equal(1, changed.Count);
            HostCheck.Equal("nativeaot.reviewed.source:" + selected.Path, changed[0].Contract);
            HostCheck.Equal(selected.Hash, changed[0].Expected);
            File.WriteAllBytes(path, original);

            File.Delete(path);
            var missing = NativeAotCurrentSources.InspectReviewedSources(root);
            HostCheck.Equal(1, missing.Count);
            HostCheck.Equal(changed[0].Contract, missing[0].Contract);
            HostCheck.True(missing[0].Actual.StartsWith("<unreadable:", StringComparison.Ordinal));
            File.WriteAllBytes(path, original);

            string extra = Path.Combine(root, "tools", "OS.Tooling", "Unregistered.cs");
            File.WriteAllText(extra, "#if INACTIVE\nnamespace OS.Tooling; partial class NativeAotPayload {}\n#endif\n");
            var ownership = NativeAotCurrentSources.InspectReviewedSources(root);
            HostCheck.Equal(1, ownership.Count);
            HostCheck.Equal("nativeaot.reviewed.ownership", ownership[0].Contract);
            HostCheck.True(ownership[0].Actual.StartsWith("source_family_extra", StringComparison.Ordinal));
            File.Delete(extra);
            HostCheck.Equal(0, NativeAotCurrentSources.InspectReviewedSources(root).Count);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
