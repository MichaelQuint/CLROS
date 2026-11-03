using OS.Tooling;

internal static class NativeAotCurrentSourcesTests
{
    private const string Producer = "tools/OS.Tooling/NativeAotPayload.cs";
    private const string OriginalHash = "E3EC6BCE13C70C99F3D7306541D68104876FAF73593325107319719811694D2F";

    [HostTest("nativeaot current source expansion requires the reviewed original identity")]
    private static void OriginalIdentity()
    {
        var unchanged = (Path: "unmapped.cs", Hash: new string('A', 64));
        HostCheck.True(NativeAotCurrentSources.Expand([unchanged]).SequenceEqual([unchanged]));
        Reject(() => NativeAotCurrentSources.Expand([(Producer, new string('A', 64))]).ToArray(), "source_family_original_identity");
        var expanded = NativeAotCurrentSources.Expand([(Producer, OriginalHash)]).ToArray();
        HostCheck.True(expanded.Select(source => source.Path).ToHashSet(StringComparer.Ordinal).SetEquals([
            Producer, "tools/OS.Tooling/NativeAotPayload.Segment.cs", "tools/OS.Tooling/NativeAotPayload.Inspection.cs"]));
        HostCheck.True(expanded.All(source => source.Hash != OriginalHash));
        Reject(() => NativeAotCurrentSources.Expand([(Producer, OriginalHash),
            ("tools/OS.Tooling/NativeAotPayload.Segment.cs", new string('A', 64))]).ToArray(), "source_family_conflicting_identity");
    }

    [HostTest("nativeaot current producer families match compiled partial ownership without duplicate paths")]
    private static void CompleteProducerFamilies()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        var catalog = HostSourceCatalog.Project("tools/OS.Tooling/OS.Tooling.csproj");
        var socket = NativeAotSocketPackage.CurrentSources();
        var expected = new[] { typeof(NativeAotPayload), typeof(NativeAotSocketPayload), typeof(NativeAotWebPayload) }
            .SelectMany(type => catalog.TypeSources(type.FullName!))
            .Select(source => (Path: Path.GetRelativePath(root, source.Path).Replace('\\', '/'), Hash: source.Sha256))
            .ToHashSet();
        HostCheck.True(expected.SetEquals(socket.Where(source => source.Path.StartsWith("tools/OS.Tooling/", StringComparison.Ordinal))));
        var sqliteExpected = new[] { typeof(NativeAotPayload), typeof(NativeAotWebPayload),
            typeof(NativeAotSocketFixturePublisher), typeof(NativeAotSqliteContract) }
            .SelectMany(type => catalog.TypeSources(type.FullName!))
            .Select(source => (Path: Path.GetRelativePath(root, source.Path).Replace('\\', '/'), Hash: source.Sha256))
            .ToHashSet();
        HostCheck.True(sqliteExpected.SetEquals(NativeAotSqliteProgram.CurrentSources()
            .Where(source => source.Path.StartsWith("tools/OS.Tooling/", StringComparison.Ordinal))));

        var original = (Path: "tools/OS.Tooling/NativeAotWebPayload.cs",
            Hash: "129FD1AE91130FBFFC772D4A189A1FBB4EEEBF8236FF69B6B827A3A40B4C1FA3");
        var family = NativeAotCurrentSources.Expand([original]).ToArray();
        HostCheck.True(family.SequenceEqual(NativeAotCurrentSources.Expand([original, original])));
        HostCheck.Equal(family.Length, family.Select(source => source.Path).Distinct(StringComparer.Ordinal).Count());
        Reject(() => NativeAotCurrentSources.Expand([original, (original.Path, new string('A', 64))]).ToArray(),
            "source_family_original_identity");

        foreach (string app in NativeAotThreadingPayload.Applications)
            HostCheck.True(NativeAotThreadingPackage.CurrentSources(app)
                .Where(source => source.Path.StartsWith("apps/", StringComparison.Ordinal))
                .All(source => source.Path.StartsWith("apps/" + app + "/", StringComparison.Ordinal)));

        string temporary = Directory.CreateTempSubdirectory("clros-web-owner-").FullName;
        try
        {
            string directory = Path.Combine(temporary, "tools", "OS.Tooling");
            Directory.CreateDirectory(directory);
            foreach (var source in family)
                File.Copy(Path.Combine(root, source.Path), Path.Combine(temporary, source.Path));
            NativeAotCurrentSources.RequireToolingOwnership(temporary, [original, original]);
            File.WriteAllText(Path.Combine(directory, "Unrelated.cs"), "namespace OS.Tooling; partial class NativeAotWebPayload {}");
            Reject(() => NativeAotCurrentSources.RequireToolingOwnership(temporary, [original]), "source_family_extra");
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    [HostTest("nativeaot current consumer revisions require the exact original identity and reviewed current bytes")]
    private static void ReviewedConsumerRevision()
    {
        const string path = "tools/OS.Tooling/NativeAotSocketFixturePublisher.cs";
        const string original = "A9029B6AFD17828D7182B7BFC2E971857BDF9469BA38D570AC6E6EBBCD197494";
        var current = NativeAotCurrentSources.Expand([(path, original)]).Single();
        HostCheck.Equal(path, current.Path);
        HostCheck.True(current.Hash != original);
        using var stream = File.OpenRead(Path.Combine(HostToolProcess.FindRepositoryRoot(), path));
        HostCheck.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream)), current.Hash);
        Reject(() => NativeAotCurrentSources.Expand([(path, current.Hash)]).ToArray(), "source_revision_original_identity");
    }

    [HostTest("nativeaot program source holds include every current producer member and reject byte changes")]
    private static void CurrentProgramSourceHolds()
    {
        string repository = HostToolProcess.FindRepositoryRoot();
        var scenarios = new (string Name, IReadOnlyList<(string Path, string Hash)> Sources, Action<string, AuthorityFiles> Hold)[]
        {
            ("Web", NativeAotWebProgram.CurrentSources(), NativeAotWebProgram.HoldSources),
            ("Service", NativeAotServiceProgram.CurrentSources(), NativeAotServiceProgram.HoldSources),
            ("LogUpload", NativeAotLogUploadProgram.CurrentSources(), NativeAotLogUploadProgram.HoldSources),
            ("Sqlite", NativeAotSqliteProgram.CurrentSources(), NativeAotSqliteProgram.HoldSources),
            ("StaticFile", NativeAotStaticFileProgram.CurrentSources(), NativeAotStaticFileProgram.HoldSources)
        };
        foreach (var scenario in scenarios)
        {
            string root = Directory.CreateTempSubdirectory("clros-program-holds-").FullName;
            try
            {
                var paths = scenario.Sources.Select(source => source.Path).ToList();
                if (scenario.Name == "StaticFile")
                    paths.AddRange(Directory.GetFiles(Path.Combine(repository, "apps", NativeAotStaticFilePublisher.Application))
                        .Select(path => Path.GetRelativePath(repository, path)));
                foreach (string relative in paths)
                {
                    string path = Path.Combine(root, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.Copy(Path.Combine(repository, relative), path);
                }
                using (var held = new AuthorityFiles())
                {
                    scenario.Hold(root, held);
                    foreach (string relative in paths)
                    {
                        string path = Path.Combine(root, relative);
                        byte[] bytes = File.ReadAllBytes(path);
                        bool refused = false;
                        try
                        {
                            using var writer = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
                            writer.WriteByte((byte)(bytes[0] ^ 1));
                            writer.Flush(true);
                        }
                        catch (IOException) when (OperatingSystem.IsWindows())
                        {
                            refused = true;
                        }
                        if (refused)
                            held.VerifyUnchanged();
                        else
                        {
                            bool detected = false;
                            try
                            {
                                held.VerifyUnchanged();
                            }
                            catch (ToolFailure error) when (error.Reason == "authority_input_changed")
                            {
                                detected = true;
                            }
                            HostCheck.True(detected, scenario.Name + " held " + relative);
                            File.WriteAllBytes(path, bytes);
                        }
                    }
                }
                foreach (string relative in paths)
                {
                    string path = Path.Combine(root, relative);
                    byte[] bytes = File.ReadAllBytes(path);
                    File.AppendAllText(path, "\n");
                    bool rejected = false;
                    try
                    {
                        using var held = new AuthorityFiles();
                        scenario.Hold(root, held);
                    }
                    catch (Exception error) when (error is InvalidDataException or ToolFailure)
                    {
                        rejected = true;
                    }
                    finally
                    {
                        File.WriteAllBytes(path, bytes);
                    }
                    HostCheck.True(rejected, scenario.Name + " altered " + relative);
                }
                using var restored = new AuthorityFiles();
                scenario.Hold(root, restored);
                restored.VerifyUnchanged();
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [HostTest("nativeaot source ownership rejects hidden declarations while allowing references and unrelated types")]
    private static void ProducerOwnership()
    {
        string root = Directory.CreateTempSubdirectory("clros-producer-ownership-").FullName;
        string directory = Path.Combine(root, "tools", "OS.Tooling");
        Directory.CreateDirectory(directory);
        try
        {
            string extra = Path.Combine(directory, "OtherName.cs");
            foreach (string source in new[]
            {
                "namespace OS.Tooling; partial class NativeAotPayload {}",
                "namespace @OS.@Tooling; partial class @NativeAotPayload {}",
                "namespace \\u004fS.Tooling; partial class \\u004eativeAotPayload {}",
                "#if HIDDEN\nnamespace OS.Tooling; partial class NativeAotPayload {}\n#endif",
                "#define HIDDEN\n#if HIDDEN\nnamespace OS.Tooling; partial class NativeAotPayload {}\n#endif",
                "#if A\nnamespace OS {\n#endif\n#if B\nnamespace Tooling {\n#endif\n#if C\npartial class NativeAotPayload {}\n#endif\n#if B\n}\n#endif\n#if A\n}\n#endif",
                "namespace Unrelated; class Outer { class NativeAotPayload<T> {} }",
                "namespace Unrelated; delegate void NativeAotPayload();",
                "// <auto-generated/>\nnamespace OS.Tooling; partial class NativeAotPayload {}"
            })
            {
                File.WriteAllText(extra, source);
                Reject(() => NativeAotCurrentSources.RequireToolingOwnership(root, [(Producer, OriginalHash)]), "source_family_extra");
            }

            string generated = Path.Combine(directory, "Unrelated.g.cs");
            File.Move(extra, generated);
            Reject(() => NativeAotCurrentSources.RequireToolingOwnership(root, [(Producer, OriginalHash)]), "source_family_extra");
            File.Delete(generated);

            string nested = Path.Combine(directory, "nested", "bin");
            Directory.CreateDirectory(nested);
            File.WriteAllText(Path.Combine(nested, "Extra.cs"), "namespace OS.Tooling; partial class NativeAotPayload {}");
            Reject(() => NativeAotCurrentSources.RequireToolingOwnership(root, [(Producer, OriginalHash)]), "source_family_extra");
            File.Delete(Path.Combine(nested, "Extra.cs"));

            File.WriteAllText(extra, "namespace OS.Tooling; class Other { NativeAotPayload? value; }");
            File.WriteAllText(Path.Combine(directory, "NativeAotPayload.cs"), "namespace OS.Tooling; partial class NativeAotPayload {}");
            NativeAotCurrentSources.RequireToolingOwnership(root, [(Producer, OriginalHash)]);
            string cache = Path.Combine(directory, "obj");
            Directory.CreateDirectory(cache);
            File.WriteAllText(Path.Combine(cache, "ignored.cs"), "invalid generated cache");
            NativeAotCurrentSources.RequireToolingOwnership(root, [(Producer, OriginalHash)]);

            File.WriteAllText(extra, "namespace OS.Tooling; partial class {");
            Reject(() => NativeAotCurrentSources.RequireToolingOwnership(root, [(Producer, OriginalHash)]), "source_family_syntax");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [HostTest("nativeaot current source admission rejects each missing member of the selected family")]
    private static void MissingMembers()
    {
        string repository = HostToolProcess.FindRepositoryRoot();
        var scenarios = new List<(string Name, IReadOnlyList<(string Path, string Hash)> Sources, Action<string> Check)>();
        foreach (string app in NativeAotRuntimePayload.Applications.Prepend("NativeAotRuntimeSmoke").Prepend("NativeAotHello"))
        {
            bool hello = app == "NativeAotHello";
            var sources = hello ? NativeAotHelloGuest.CurrentSources() : NativeAotRuntimePackage.CurrentSources(app);
            scenarios.Add((app, sources, root =>
            {
                if (hello)
                    NativeAotHelloGuest.RequireCurrentSources(root);
                else
                    NativeAotRuntimePackage.RequireCurrentSources(root, app);
            }
            ));
        }
        foreach (string app in NativeAotThreadingPayload.Applications)
            scenarios.Add((app, NativeAotThreadingPackage.CurrentSources(app), root => NativeAotThreadingPackage.RequireCurrentSources(root, app)));
        scenarios.Add(("Socket", NativeAotSocketPackage.CurrentSources(), NativeAotSocketPackage.RequireCurrentSources));
        scenarios.Add(("Containment", NativeAotSocketContainmentPackage.CurrentSources(), NativeAotSocketContainmentPackage.RequireCurrentSources));
        scenarios.Add(("Vectors", NativeAotSocketVectorsPackage.CurrentSources(), NativeAotSocketVectorsPackage.RequireCurrentSources));
        foreach (var scenario in scenarios)
        {
            string root = Directory.CreateTempSubdirectory("clros-current-missing-").FullName;
            void Check() => scenario.Check(root);

            try
            {
                foreach (var source in scenario.Sources)
                {
                    string path = Path.Combine(root, source.Path);
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.Copy(Path.Combine(repository, source.Path), path);
                }
                Check();
                foreach (var source in scenario.Sources)
                {
                    string path = Path.Combine(root, source.Path);
                    byte[] bytes = File.ReadAllBytes(path);
                    File.Delete(path);
                    bool rejected = false;
                    try
                    {
                        Check();
                    }
                    catch (Exception error) when (error is IOException or InvalidDataException or ToolFailure)
                    {
                        rejected = true;
                    }
                    finally
                    {
                        File.WriteAllBytes(path, bytes);
                    }
                    HostCheck.True(rejected, scenario.Name + " missing " + source.Path);
                }
                Check();
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static void Reject(Action action, string reason)
    {
        string? failure = null;
        try
        {
            action();
        }
        catch (InvalidDataException error)
        {
            failure = error.Message;
        }
        HostCheck.True(failure?.StartsWith(reason, StringComparison.Ordinal) == true, reason + " actual=" + failure);
    }
}
