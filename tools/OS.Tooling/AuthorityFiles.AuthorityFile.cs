using System.Security.Cryptography;
using System.Text;
using OS.Tooling.Native;

namespace OS.Tooling;
public sealed class AuthorityFile : IDisposable
{
    public string Path { get; }
    public string Root { get; }
    public string Purpose { get; }
    public string FailurePrefix { get; }
    public long Length { get; }
    public string Sha256 { get; }
    internal FileIdentity InitialIdentity { get; }
    internal bool RequireSingleLink { get; set; }

    private readonly FileStream stream;
    internal AuthorityFile(string path, string root, string purpose, string failurePrefix, FileStream stream, FileIdentity identity, bool singleLink)
    {
        Path = path;
        Root = root;
        Purpose = purpose;
        FailurePrefix = failurePrefix;
        this.stream = stream;
        InitialIdentity = identity;
        RequireSingleLink = singleLink;
        Length = stream.Length;
        Sha256 = Hash();
    }

    internal void CheckSize(long maximum, bool allowEmpty)
    {
        if (Length < 0 || Length > maximum || !allowEmpty && Length == 0 || (ulong)Length != InitialIdentity.Length)
            throw new ToolFailure(FailurePrefix + "_size_out_of_range", $"purpose={Purpose} path={Path} bytes={Length} maximum={maximum}");
    }

    internal void CheckLinks()
    {
        if (RequireSingleLink && InitialIdentity.LinkCount != 1)
            throw new ToolFailure("authority_output_hardlink_invalid", $"purpose={Purpose} path={Path} links={InitialIdentity.LinkCount}");
    }

    private string Hash()
    {
        long position = stream.Position;
        try
        {
            stream.Position = 0;
            return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }
        finally
        {
            stream.Position = position;
        }
    }

    public byte[] ReadBytes(long maximum, string reason)
    {
        long length = stream.Length;
        if (length < 0 || length > maximum || length > int.MaxValue)
            throw new ToolFailure(reason, $"path={Path} bytes={length} maximum={maximum}");
        if (length != Length)
            throw new ToolFailure("authority_input_changed", "path=" + Path + " read_length=true");
        byte[] bytes = new byte[(int)length];
        long position = stream.Position;
        try
        {
            stream.Position = 0;
            stream.ReadExactly(bytes);
            if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(Sha256, StringComparison.OrdinalIgnoreCase))
                throw new ToolFailure("authority_input_changed", "path=" + Path + " read_hash=true");
            return bytes;
        }
        catch (EndOfStreamException)
        {
            throw new ToolFailure(reason, "path=" + Path + " prematureEof=true");
        }
        finally
        {
            stream.Position = position;
        }
    }

    public string ReadUtf8(long maximum, string reason)
    {
        byte[] bytes = ReadBytes(maximum, reason);
        int skip = bytes.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf }) ? 3 : 0;
        try
        {
            return new UTF8Encoding(false, true).GetString(bytes.AsSpan(skip));
        }
        catch (DecoderFallbackException error)
        {
            throw new ToolFailure(reason, $"path={Path} encoding=utf8 type={error.GetType().Name}");
        }
    }

    public void VerifyUnchanged()
    {
        AuthorityFiles.RequirePlainPath(Root, Path, FailurePrefix + "_reparse_point");
        try
        {
            var identity = NativeFile.GetIdentity(stream.SafeFileHandle);
            using var probe = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var probeIdentity = NativeFile.GetIdentity(probe.SafeFileHandle);
            if (stream.Length != Length || Hash() != Sha256 || identity.Key != InitialIdentity.Key || probeIdentity.Key != InitialIdentity.Key || identity.LinkCount != InitialIdentity.LinkCount || RequireSingleLink && identity.LinkCount != 1)
                throw new ToolFailure("authority_input_changed", $"purpose={Purpose} path={Path}");
        }
        catch (ToolFailure)
        {
            throw;
        }
        catch (Exception error)when (error is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            throw new ToolFailure("authority_input_changed", $"purpose={Purpose} path={Path} type={error.GetType().Name}");
        }
    }

    public void Dispose() => stream.Dispose();
}
