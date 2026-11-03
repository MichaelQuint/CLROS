using System.Diagnostics;
using System.Security.Cryptography;
using OS.Tooling.Native;

namespace OS.Tools.QemuRunner;

/// <summary>Holds an immutable boot image and a distinct mutable raw image.
/// One serialized runner owns both streams and the bound Process through actual
/// child join. Named/retained identity checks are boundary checks, not atomic
/// execution, GPT, guest flush or durability authority. No file is deleted.</summary>
public sealed class WritableDataImageLease : IDisposable
{
    private readonly FileStream boot, working;
    private Process? child;
    private Microsoft.Win32.SafeHandles.SafeProcessHandle? childHandle;
    private int childId;
    private bool disposed;

    private WritableDataImageLease(string bootPath, string workingPath, FileStream boot, FileStream working)
    {
        BootPath = bootPath;
        WorkingPath = workingPath;
        this.boot = boot;
        this.working = working;
        var first = NativeFile.GetIdentity(boot.SafeFileHandle);
        var second = NativeFile.GetIdentity(working.SafeFileHandle);
        Need(first.LinkCount == 1 && second.LinkCount == 1 && first.Key != second.Key, "identity");
        Need(first.Length > 0 && first.Length % 512 == 0 && second.Length > 0 && second.Length % 512 == 0,
            "sector_length");
        BootIdentity = first.Key;
        WorkingIdentity = second.Key;
        BootLength = first.Length;
        WorkingLength = second.Length;
        Need(RawCompatible(boot) && RawCompatible(working), "raw_required");
        BootSha256 = Hash(boot);
        InitialWorkingSha256 = Hash(working);
        VerifyOriginalAndWorkingIdentity();
    }

    public string BootPath
    {
        get;
    }
    public string WorkingPath
    {
        get;
    }
    public string BootIdentity
    {
        get;
    }
    public string WorkingIdentity
    {
        get;
    }
    public ulong BootLength
    {
        get;
    }
    public ulong WorkingLength
    {
        get;
    }
    public string BootSha256
    {
        get;
    }
    public string InitialWorkingSha256
    {
        get;
    }
    public bool ChildJoined
    {
        get; private set;
    }
    public bool FinalVerified
    {
        get; private set;
    }
    public bool FinalVerificationAttempted
    {
        get; private set;
    }

    /// <summary>Open existing plain paths with single-link identities. The
    /// caller excludes competing writers during initial admission; mutable
    /// bytes may subsequently change while their original extent is retained.</summary>
    public static WritableDataImageLease Open(string bootPath, string workingPath)
    {
        string first = Path.GetFullPath(bootPath), second = Path.GetFullPath(workingPath);
        Need(!first.Equals(second, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal), "path_alias");
        PlainPath(first);
        PlainPath(second);
        FileStream? boot = null, working = null;
        try
        {
            boot = new(first, FileMode.Open, FileAccess.Read, FileShare.Read, 65536);
            working = new(second, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite, 65536);
            var result = new WritableDataImageLease(first, second, boot, working);
            boot = working = null;
            return result;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            throw new ArgumentException("writable_data_open_failed type=" + error.GetType().Name, error);
        }
        finally
        {
            working?.Dispose();
            boot?.Dispose();
        }
    }

    /// <summary>Reject reparse/symlink ancestors for selected runtime inputs
    /// and outputs before publication. Missing output suffixes are allowed;
    /// the owner must recheck at its later serialized boundaries.</summary>
    public static void RequirePlainRuntimePath(string path) => PlainPath(Path.GetFullPath(path), allowMissing: true);

    /// <summary>Bind the runner's exact already-started Process instance once.
    /// The caller retains it undisposed and must not reuse it to start another
    /// child. An unjoined child makes Dispose refuse without closing either file.</summary>
    public void BindStartedChild(Process process)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(process);
        Need(child is null, "child_already_bound");
        int id = process.Id;
        var handle = process.SafeHandle;
        Need(!handle.IsClosed && !handle.IsInvalid, "child_handle");
        childId = id;
        childHandle = handle;
        child = process;
    }

    /// <summary>Revalidate unchanged boot contents and both original identities,
    /// named paths, single-link counts and lengths. Mutable content is expressly
    /// excluded from unchanged-hash checks. Call only at serialized boundaries.</summary>
    public void VerifyOriginalAndWorkingIdentity()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        PlainPath(BootPath);
        PlainPath(WorkingPath);
        Check(boot, BootPath, BootIdentity, BootLength, FileShare.Read);
        Check(working, WorkingPath, WorkingIdentity, WorkingLength, FileShare.ReadWrite);
        Need(Hash(boot) == BootSha256, "boot_content_changed");
    }

    /// <summary>Complete only after this actual bound child has exited and its
    /// wait has joined. A failed file check remains a failed final verification.
    /// Process exit and this receipt alone establish no guest acceptance.</summary>
    public void CompleteAfterChildJoin()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        Need(OriginalChildJoined(), "child_unjoined");
        ChildJoined = true;
        FinalVerificationAttempted = true;
        VerifyOriginalAndWorkingIdentity();
        FinalVerified = true;
    }

    public IEnumerable<string> Summary()
    {
        yield return "WRITABLE_DATA_IMAGE " + WorkingPath;
        yield return "WRITABLE_DATA_BOOT identity=" + BootIdentity + " bytes=" + BootLength + " sha256=" + BootSha256;
        yield return "WRITABLE_DATA_WORKING identity=" + WorkingIdentity + " bytes=" + WorkingLength +
            " initialSha256=" + InitialWorkingSha256 + " content=mutable cache=writeback";
        yield return "WRITABLE_DATA_LIFETIME joined=" + ChildJoined.ToString().ToLowerInvariant() +
            " finalVerified=" + FinalVerified.ToString().ToLowerInvariant() + " acceptance=pending";
    }

    /// <summary>Release only before child binding or after actual join. Refusal
    /// leaves both handles open. The owner must keep itself alive on unresolved
    /// termination; handles do not survive a killed runner.</summary>
    public void Dispose()
    {
        if (disposed)
            return;
        if (child is not null && !ChildJoined && !OriginalChildJoined())
            throw new InvalidOperationException("writable_data_child_unjoined");
        disposed = true;
        try
        {
            working.Dispose();
        }
        finally
        {
            boot.Dispose();
        }
    }

    private bool OriginalChildJoined() => child is not null && childHandle is not null &&
        !childHandle.IsClosed && !childHandle.IsInvalid && child.Id == childId &&
        ReferenceEquals(child.SafeHandle, childHandle) && child.HasExited && child.WaitForExit(0);

    private static void Check(FileStream retained, string path, string key, ulong length, FileShare share)
    {
        var original = NativeFile.GetIdentity(retained.SafeFileHandle);
        using FileStream named = new(path, FileMode.Open, FileAccess.Read, share);
        var current = NativeFile.GetIdentity(named.SafeFileHandle);
        Need(original.Key == key && current.Key == key && original.LinkCount == 1 && current.LinkCount == 1 &&
            original.Length == length && current.Length == length && (ulong)retained.Length == length, "identity_changed");
    }

    private static string Hash(FileStream stream)
    {
        long position = stream.Position;
        try
        {
            stream.Position = 0;
            return Convert.ToHexString(SHA256.HashData(stream));
        }
        finally
        {
            stream.Position = position;
        }
    }

    private static bool RawCompatible(FileStream stream)
    {
        Span<byte> prefix = stackalloc byte[4];
        stream.Position = 0;
        int bytes = stream.Read(prefix);
        stream.Position = 0;
        return bytes == 4 && !(prefix[0] == 'Q' && prefix[1] == 'F' && prefix[2] == 'I' && prefix[3] == 0xfb);
    }

    private static void PlainPath(string path, bool allowMissing = false)
    {
        Need(path.IndexOfAny([',', '\r', '\n', '\0']) < 0, "path_separator");
        if (OperatingSystem.IsWindows())
            Need(path.IndexOf(':', 2) < 0, "alternate_stream");
        string current = Path.GetPathRoot(path) ?? throw new ArgumentException("writable_data_path_root");
        foreach (string part in path[current.Length..].Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, part);
            FileAttributes attributes;
            try
            {
                attributes = File.GetAttributes(current);
            }
            catch (Exception error) when (allowMissing && error is FileNotFoundException or DirectoryNotFoundException)
            {
                return;
            }
            Need((attributes & FileAttributes.ReparsePoint) == 0, "reparse_path");
        }
    }

    private static void Need(bool value, string reason)
    {
        if (!value)
            throw new ArgumentException("writable_data_" + reason);
    }
}
