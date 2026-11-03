using System.Security.Cryptography;
using System.Text;
using OS.Tooling.Native;

namespace OS.Tooling;
/// <summary>Exact local file snapshots held open with read sharing and verified identity/content.</summary>

public sealed class AuthorityFiles : IDisposable
{
    private readonly Dictionary<string, AuthorityFile> records = new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    public static void RequirePlainPath(string root, string path, string reason)
    {
        string fullRoot, fullPath;
        try
        {
            fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception error)when (error is ArgumentException or IOException or NotSupportedException)
        {
            throw new ToolFailure(reason, "type=" + error.GetType().Name);
        }

        string prefix = Path.EndsInDirectorySeparator(fullRoot) ? fullRoot : fullRoot + Path.DirectorySeparatorChar;
        StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!fullPath.Equals(fullRoot, comparison) && !fullPath.StartsWith(prefix, comparison))
            throw new ToolFailure(reason, "path=" + fullPath);
        string current = Path.GetPathRoot(fullPath) ?? throw new ToolFailure(reason, "root=missing");
        foreach (string segment in fullPath[current.Length..].Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            FileAttributes attributes;
            try
            {
                attributes = File.GetAttributes(current);
            }
            catch (Exception error)when (error is IOException or UnauthorizedAccessException)
            {
                throw new ToolFailure(reason, $"path={current} type={error.GetType().Name}");
            }

            if ((attributes & FileAttributes.ReparsePoint) != 0)
                throw new ToolFailure(reason, "path=" + current);
        }
    }

    public AuthorityFile Open(string root, string path, string purpose, string failurePrefix, long maximumBytes = long.MaxValue, bool allowEmpty = false, bool singleLink = false)
    {
        string fullPath = Path.GetFullPath(path);
        RequirePlainPath(root, fullPath, failurePrefix + "_reparse_point");
        if (!File.Exists(fullPath))
            throw new ToolFailure(failurePrefix + "_missing", "path=" + fullPath);
        if (records.TryGetValue(fullPath, out var existing))
        {
            existing.CheckSize(maximumBytes, allowEmpty);
            existing.RequireSingleLink |= singleLink;
            existing.CheckLinks();
            return existing;
        }

        FileStream? stream = null;
        try
        {
            stream = new(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.SequentialScan);
            RequirePlainPath(root, fullPath, failurePrefix + "_reparse_point");
            var identity = NativeFile.GetIdentity(stream.SafeFileHandle);
            long length = stream.Length;
            if (length < 0 || length > maximumBytes || !allowEmpty && length == 0 || (ulong)length != identity.Length)
                throw new ToolFailure(failurePrefix + "_size_out_of_range", $"purpose={purpose} path={fullPath} bytes={length} maximum={maximumBytes}");
            var record = new AuthorityFile(fullPath, Path.GetFullPath(root), purpose, failurePrefix, stream, identity, singleLink);
            record.CheckSize(maximumBytes, allowEmpty);
            record.CheckLinks();
            records.Add(fullPath, record);
            stream = null;
            return record;
        }
        catch (ToolFailure)
        {
            throw;
        }
        catch (Exception error)when (error is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            throw new ToolFailure(failurePrefix + "_open_failed", $"purpose={purpose} path={fullPath} type={error.GetType().Name}");
        }
        finally
        {
            stream?.Dispose();
        }
    }

    public static void RequireDistinct(params AuthorityFile[] files)
    {
        Dictionary<string, string> identities = new(StringComparer.Ordinal);
        foreach (var record in files)
        {
            if (record.InitialIdentity.LinkCount != 1)
                throw new ToolFailure("authority_output_hardlink_invalid", $"purpose={record.Purpose} path={record.Path} links={record.InitialIdentity.LinkCount}");
            if (!identities.TryAdd(record.InitialIdentity.Key, record.Path))
                throw new ToolFailure("authority_output_file_alias", $"first={identities[record.InitialIdentity.Key]} second={record.Path}");
        }
    }

    public void VerifyUnchanged()
    {
        foreach (AuthorityFile record in records.Values)
            record.VerifyUnchanged();
    }

    public void Dispose()
    {
        foreach (AuthorityFile record in records.Values)
            record.Dispose();
        records.Clear();
    }
}
