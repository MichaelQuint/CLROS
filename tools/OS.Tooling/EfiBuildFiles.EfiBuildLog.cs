using System.Text;
using OS.Tooling.Native;
using static OS.Tooling.EfiPeImage;

namespace OS.Tooling;

public sealed class EfiBuildLog : IDisposable
{
    public string Path
    {
        get;
    }
    public string IdentityKey
    {
        get;
    }

    private readonly FileStream stream;
    private EfiBuildLog(string path, FileStream stream)
    {
        Path = path;
        this.stream = stream;
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
            log.VerifyIdentity();
            stream.SetLength(0);
            stream.Flush(true);
            return log;
        }
        catch
        {
            stream?.Dispose();
            throw;
        }
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
        VerifyIdentity();
        stream.Position = stream.Length;
        stream.Write(Encoding.UTF8.GetBytes(line + Environment.NewLine));
        stream.Flush();
        VerifyIdentity();
    }

    public void Dispose()
    {
        stream.Flush(true);
        stream.Dispose();
    }
}
