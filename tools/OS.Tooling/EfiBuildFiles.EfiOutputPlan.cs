using System.Text;
using OS.Tooling.Native;
using static OS.Tooling.EfiPeImage;

namespace OS.Tooling;

public sealed record EfiOutputPlan(string Directory, string EfiPath, string MapPath, string ResponsePath, string EvidencePath, string SerializerObjectPath, IReadOnlyList<string> Paths)
{
    public static EfiOutputPlan Create(string root, string efiOutputPath, string logPath, EfiProfile? profile)
    {
        string directory = EfiBuildFiles.InitializeDirectory(Path.Combine(root, "artifacts/efi"), "output_authority_ancestor_invalid");
        string efi = EfiEvidence.FullPath(root, efiOutputPath, "output_authority_path_invalid");
        Require(EfiEvidence.SamePath(Path.GetDirectoryName(efi)!, directory), "output_authority_path_invalid", "expectedDirectory=" + directory);
        List<string> paths = [efi, efi + ".link.map", efi + ".link.rsp", efi + ".link.json", efi + ".serializer.obj"];
        if (profile is null || profile.IsEnabled("M04CheckedReferenceBarrier") || profile.IsEnabled("M04GcStaticRootMaterialization"))
            paths.Add(efi + ".references.obj");
        if (profile is null || profile.IsEnabled("M04InterfaceDispatch"))
            paths.Add(efi + ".dispatch.obj");
        if (profile is null || profile.IsEnabled("M04SimpleThrowCatch") || profile.IsEnabled("M04UnhandledException"))
            paths.Add(efi + ".catch.obj");
        if (profile is null || profile.IsEnabled("M04SingleFinallyCatch"))
            paths.Add(efi + ".finally.obj");
        if (profile is null || profile.IsEnabled("M04Rethrow"))
            paths.AddRange([efi + ".rethrow-inner.obj", efi + ".rethrow-outer.obj"]);
        if (profile is null || profile.IsEnabled("M04FilteredCatch"))
        {
            paths.Add(efi + ".filter-catch.obj");
            paths.Add(efi + ".filter-invoke.obj");
        }

        if (profile is null || profile.IsEnabled("M04ConstructorFailure"))
            paths.Add(efi + ".ctor-failure.obj");
        if (profile is null || profile.IsEnabled("M04FrameDispatch"))
            paths.Add(efi + ".frames.obj");
        if (profile is null || profile.IsEnabled("M04NormalFinally"))
            paths.Add(efi + ".normal-finally.obj");
        if (profile is null || profile.IsEnabled("M04EhCatalog") || profile.IsEnabled("M04NormalFinally"))
            paths.AddRange([efi + ".eh-catalog.obj", efi + ".ilc.obj", efi + ".ilc.map.xml"]);
        if (profile is null || profile.IsEnabled("M04NestedFinallyCatch"))
            paths.Add(efi + ".nested-finally.obj");
        foreach (string path in paths)
        {
            Require(!EfiEvidence.SamePath(path, logPath), "output_authority_log_path_collision", path);
            EfiBuildFiles.RequirePlainOrMissing(directory, path, "output_authority_leaf_invalid");
        }

        return new(directory, efi, paths[1], paths[2], paths[3], paths[4], paths.AsReadOnly());
    }

    public void Clear()
    {
        using (AuthorityFiles existing = new())
        {
            List<AuthorityFile> records = [];
            foreach (string path in Paths)
                if (Path.Exists(path))
                    records.Add(existing.Open(Directory, path, "stale-output", "output_cleanup_authority", allowEmpty: true, singleLink: true));
            AuthorityFiles.RequireDistinct(records.ToArray());
        }

        foreach (string path in Paths)
        {
            EfiBuildFiles.RequirePlainOrMissing(Directory, path, "output_cleanup_authority");
            if (File.Exists(path))
                File.Delete(path);
            Require(!Path.Exists(path), "output_cleanup_failed", path);
        }
    }
}
