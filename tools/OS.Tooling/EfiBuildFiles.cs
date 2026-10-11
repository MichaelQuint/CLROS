using System.Text;
using OS.Tooling.Native;
using static OS.Tooling.EfiPeImage;

namespace OS.Tooling;

/// <summary>Owned build paths are checked before creation, cleanup, or publication.</summary>
public static class EfiBuildFiles
{
    public static string InitializeDirectory(string path, string reason)
    {
        string full = Path.GetFullPath(path), current = Path.GetPathRoot(full)!;
        foreach (string part in full[current.Length..].Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, part);
            if (!Path.Exists(current)) Directory.CreateDirectory(current);
            Require(Directory.Exists(current) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) == 0, reason, current);
        }
        AuthorityFiles.RequirePlainPath(Path.GetPathRoot(full)!, full, reason);
        return full;
    }

    public static void RequirePlainOrMissing(string root, string path, string reason)
    {
        string fullRoot = Path.GetFullPath(root), full = Path.GetFullPath(path);
        _ = EfiEvidence.RelativePath(fullRoot, full, reason);
        string current = full;
        while (!Path.Exists(current)) current = Path.GetDirectoryName(current) ?? throw new ToolFailure(reason);
        AuthorityFiles.RequirePlainPath(Path.GetPathRoot(fullRoot)!, current, reason);
        if (Path.Exists(full)) AuthorityFiles.RequirePlainPath(fullRoot, full, reason);
    }

    public static FileStream OpenPublishLock(string root)
    {
        string directory = InitializeDirectory(Path.Combine(root, "artifacts/.locks"), "publish_output_lock_path_invalid");
        string path = Path.Combine(directory, "efi-publish.lock");
        RequirePlainOrMissing(directory, path, "publish_output_lock_path_invalid");
        FileStream? stream = null;
        try
        {
            stream = new(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            Require(NativeFile.GetIdentity(stream.SafeFileHandle).LinkCount == 1, "publish_output_lock_hardlink_invalid");
            return stream;
        }
        catch (IOException) { stream?.Dispose(); throw new ToolFailure("publish_output_lock_failed", "another EFI publish owns the canonical output paths"); }
        catch { stream?.Dispose(); throw; }
    }

    public static void FreshDirectory(string root, string candidate, string expectedRelative)
    {
        string fullRoot = Path.GetFullPath(root), full = Path.GetFullPath(candidate);
        Require(EfiEvidence.SamePath(full, Path.Combine(fullRoot, expectedRelative)) && !EfiEvidence.SamePath(full, fullRoot), "fresh_publish_path_mismatch", full);
        _ = EfiEvidence.RelativePath(fullRoot, full, "fresh_publish_path_outside_repository");
        RequirePlainOrMissing(fullRoot, full, "fresh_publish_path_reparse_point");
        Require(!File.Exists(full), "fresh_publish_path_not_directory");
        if (Directory.Exists(full))
        {
            // Inspect every descendant before recursive deletion; a redirected child is never followed.
            Stack<string> pending = new(); pending.Push(full);
            while (pending.TryPop(out string? directory)) foreach (string item in Directory.EnumerateFileSystemEntries(directory))
            {
                FileAttributes attributes = File.GetAttributes(item);
                Require((attributes & FileAttributes.ReparsePoint) == 0, "fresh_publish_path_reparse_point", item);
                if ((attributes & FileAttributes.Directory) != 0) pending.Push(item);
            }
            Directory.Delete(full, recursive: true);
        }
        Require(!Path.Exists(full), "fresh_publish_cleanup_failed", full);
    }

    public static void WriteCreateNew(string root, string path, ReadOnlySpan<byte> bytes)
    {
        RequirePlainOrMissing(root, path, "output_destination_authority");
        using (FileStream destination = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            Require(NativeFile.GetIdentity(destination.SafeFileHandle).LinkCount == 1, "output_destination_hardlink_invalid");
            destination.Write(bytes); destination.Flush(true);
        }
        AuthorityFiles.RequirePlainPath(root, path, "output_destination_authority");
    }

    public static AuthorityFile CopyCreateNew(AuthorityFiles locks, string sourceRoot, string sourcePath, string destinationRoot, string destinationPath)
    {
        var source = locks.Open(sourceRoot, sourcePath, "publication-source", "output_source_authority", 536870912, singleLink: true);
        WriteCreateNew(destinationRoot, destinationPath, source.ReadBytes(536870912, "output_source_read_failed"));
        var copy = locks.Open(destinationRoot, destinationPath, "published-output", "output_destination_authority", 536870912, singleLink: true);
        Require(copy.Sha256 == source.Sha256 && copy.Length == source.Length, "output_publication_identity_mismatch");
        AuthorityFiles.RequireDistinct(source, copy);
        return copy;
    }
}

public sealed record EfiOutputPlan(string Directory, string EfiPath, string MapPath, string ResponsePath, string EvidencePath, string SerializerObjectPath, IReadOnlyList<string> Paths)
{
    public static EfiOutputPlan Create(string root, string efiOutputPath, string logPath, EfiProfile? profile)
    {
        string directory = EfiBuildFiles.InitializeDirectory(Path.Combine(root, "artifacts/efi"), "output_authority_ancestor_invalid");
        string efi = EfiEvidence.FullPath(root, efiOutputPath, "output_authority_path_invalid");
        Require(EfiEvidence.SamePath(Path.GetDirectoryName(efi)!, directory), "output_authority_path_invalid", "expectedDirectory=" + directory);
        List<string> paths = [efi, efi + ".link.map", efi + ".link.rsp", efi + ".link.json", efi + ".serializer.obj"];
        if (profile is null || profile.IsEnabled("M04CheckedReferenceBarrier") || profile.IsEnabled("M04GcStaticRootMaterialization")) paths.Add(efi + ".references.obj");
        if (profile is null || profile.IsEnabled("M04InterfaceDispatch")) paths.Add(efi + ".dispatch.obj");
        if (profile is null || profile.IsEnabled("M04SimpleThrowCatch") || profile.IsEnabled("M04UnhandledException")) paths.Add(efi + ".catch.obj");
        if (profile is null || profile.IsEnabled("M04SingleFinallyCatch")) paths.Add(efi + ".finally.obj");
        if (profile is null || profile.IsEnabled("M04Rethrow")) paths.AddRange([efi + ".rethrow-inner.obj", efi + ".rethrow-outer.obj"]);
        if (profile is null || profile.IsEnabled("M04FilteredCatch")) { paths.Add(efi + ".filter-catch.obj"); paths.Add(efi + ".filter-invoke.obj"); }
        if (profile is null || profile.IsEnabled("M04ConstructorFailure")) paths.Add(efi + ".ctor-failure.obj");
        if (profile is null || profile.IsEnabled("M04FrameDispatch")) paths.Add(efi + ".frames.obj");
        if (profile is null || profile.IsEnabled("M04NestedFinallyCatch")) paths.Add(efi + ".nested-finally.obj");
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
            foreach (string path in Paths) if (Path.Exists(path)) records.Add(existing.Open(Directory, path, "stale-output", "output_cleanup_authority", allowEmpty: true, singleLink: true));
            AuthorityFiles.RequireDistinct(records.ToArray());
        }
        foreach (string path in Paths)
        {
            EfiBuildFiles.RequirePlainOrMissing(Directory, path, "output_cleanup_authority");
            if (File.Exists(path)) File.Delete(path);
            Require(!Path.Exists(path), "output_cleanup_failed", path);
        }
    }
}

public sealed class EfiBuildLog : IDisposable
{
    public string Path { get; }
    public string IdentityKey { get; }
    private readonly FileStream stream;
    private EfiBuildLog(string path, FileStream stream)
    {
        Path = path; this.stream = stream;
        IdentityKey = NativeFile.GetIdentity(stream.SafeFileHandle).Key;
    }
    public static EfiBuildLog Open(string path)
    {
        string full = System.IO.Path.GetFullPath(path), root = System.IO.Path.GetPathRoot(full)!;
        EfiBuildFiles.InitializeDirectory(System.IO.Path.GetDirectoryName(full)!, "log_authority_ancestor_invalid");
        EfiBuildFiles.RequirePlainOrMissing(root, full, "log_authority_leaf_invalid");
        FileStream? stream = null;
        try
        {
            stream = new(full, File.Exists(full) ? FileMode.Open : FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read);
            Require(NativeFile.GetIdentity(stream.SafeFileHandle).LinkCount == 1, "log_authority_hardlink_invalid");
            EfiBuildLog log = new(full, stream);
            log.VerifyIdentity(); stream.SetLength(0); stream.Flush(true);
            return log;
        }
        catch { stream?.Dispose(); throw; }
    }
    public void VerifyIdentity()
    {
        AuthorityFiles.RequirePlainPath(System.IO.Path.GetPathRoot(Path)!, Path, "log_authority_leaf_invalid");
        var actual = NativeFile.GetIdentity(stream.SafeFileHandle);
        using FileStream probe = new(Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        Require(actual.Key == IdentityKey && NativeFile.GetIdentity(probe.SafeFileHandle).Key == IdentityKey && actual.LinkCount == 1, "log_authority_identity_changed");
    }
    public void WriteLine(string line)
    {
        VerifyIdentity(); stream.Position = stream.Length;
        stream.Write(Encoding.UTF8.GetBytes(line + Environment.NewLine)); stream.Flush(); VerifyIdentity();
    }
    public void Dispose() { stream.Flush(true); stream.Dispose(); }
}
