internal static class HostSourceInfrastructureTests
{
    [HostTest("host source infrastructure follows the bound entrypoint across partial files")]
    private static void SharedPartialInputs()
    {
        var catalog = HostSemanticFingerprintTests.Catalog(new Dictionary<string, string>
        {
            ["entry.cs"] = "return RenamedRunner.Run();",
            ["renamed.cs"] = "partial class RenamedRunner { internal static int Run() => More(); }",
            ["moved/part.cs"] = "partial class RenamedRunner { static int More() => 0; }",
            ["unrelated.cs"] = "class HostTestRunner {} class HostCheck {}",
            ["inactive.cs"] = "#if INACTIVE\nclass Hidden {}\n#endif"
        }, executable: true);
        HostCheck.True(HostTestFingerprint.SharedSources(catalog).Select(document => document.Path)
            .SequenceEqual(new[] { "entry.cs", "renamed.cs", "moved/part.cs" }));
    }

    [HostTest("host fingerprint tracks moved nested sources and identical basenames independently")]
    private static void MovedSourceInputs()
    {
        Dictionary<string, string> sources = new(StringComparer.Ordinal)
        {
            ["left/Fixture.cs"] = "partial class First { int Run() => Value; }",
            ["right/Fixture.cs"] = "class Second { int Run() => 9; }",
            ["nested/First.Helper.cs"] = "partial class First { const int Value = 1; }"
        };
        var before = new HostTestFingerprint(sources, "shared");
        sources["nested/First.Helper.cs"] = "partial class First { const int Value = 2; }";
        var changed = new HostTestFingerprint(sources, "shared");
        HostCheck.True(before.For("First", "all") != changed.For("First", "all"));
        HostCheck.Equal(before.For("Second", "all"), changed.For("Second", "all"));
    }

    [HostTest("host fingerprint inventory comes from the executing assembly compiler receipt")]
    private static void CompiledInventory()
    {
        var inventory = OS.Tooling.CSharpBuiltSourceInventory.Read(HostToolProcess.FindRepositoryRoot(), typeof(HostSourceInfrastructureTests).Assembly.Location);
        HostCheck.True(inventory.Sources.Any(source => source.Path.EndsWith("CSharpCompilationReceiptTests.ReceiptFixture.cs", StringComparison.Ordinal)));
        HostCheck.True(inventory.Sources.Any(source => source.IsGenerated));
        HostCheck.Equal(2, inventory.Sources.Count(source => Path.GetFileName(source.Path) == "RunnerIdentitySample.cs"));
        HostCheck.True(inventory.InputHashes.ContainsKey(inventory.Artifacts.ReceiptPath!));
        HostCheck.Equal(OS.Tooling.CSharpBuiltSourceInventory.Read(HostToolProcess.FindRepositoryRoot(), typeof(HostSourceInfrastructureTests).Assembly.Location).Artifacts.AssemblySha256,
            HostRunCheckpoint.HashFile(typeof(HostSourceInfrastructureTests).Assembly.Location));
    }

    [HostTest("host runner groups namespace identities without merging equal type or file names")]
    private static void QualifiedFixtureOwners()
    {
        var types = new[] { typeof(RunnerControls.Left.RunnerIdentitySample), typeof(RunnerControls.Right.RunnerIdentitySample) };
        var fixtures = types.Select(type =>
        {
            var method = type.GetMethod("Run", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
            return new HostTestRunner.Fixture(HostTestRunner.Owner(method) + ".Run", HostLane.Fast, method);
        }).ToArray();
        HostCheck.Equal(2, HostTestRunner.GroupByLane(fixtures).Length);
        HostCheck.Equal(2, fixtures.Select(fixture => fixture.Id).Distinct(StringComparer.Ordinal).Count());
        Dictionary<string, string> sources = new(StringComparer.Ordinal)
        {
            ["a/Any.cs"] = "namespace A; class Fixture { int Run() => 1; }",
            ["b/Any.cs"] = "namespace B; class Fixture { int Run() => 2; }",
            ["nested.cs"] = "namespace A { partial class Parent<T> { class Nested {} } }"
        };
        var before = new HostTestFingerprint(sources, "shared");
        sources["b/Any.cs"] = "namespace B; class Fixture { int Run() => 3; }";
        var after = new HostTestFingerprint(sources, "shared");
        // Fixture identity includes namespace and nested generic arity.
        foreach (string owner in new[] { "A.Fixture", "B.Fixture", "A.Parent`1+Nested" })
            HostCheck.True(!before.For(owner, "all").StartsWith("fresh:unknown_type:", StringComparison.Ordinal));
        HostCheck.True(before.For("B.Fixture", "all") != after.For("B.Fixture", "all"));
    }
}
