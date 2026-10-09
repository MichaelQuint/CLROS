using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using OS.Tooling;

internal static class AuthorityFilesTests
{
    private static string Scratch()
    {
        string path = Path.Combine(HostToolProcess.FindRepositoryRoot(), "artifacts", "authority-file-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
    private static void Reject(Action action, string reason)
    {
        try { action(); } catch (ToolFailure error) { HostCheck.Equal(reason, error.Reason); return; }
        throw new InvalidOperationException("expected rejection " + reason);
    }

    internal static bool ValidatePathWalk(string root)
    {
        Directory.CreateDirectory(root);
        string literal = Path.Combine(root, "literal [abc] #; space");
        Directory.CreateDirectory(literal);
        string file = Path.Combine(literal, "hidden [x].txt");
        File.WriteAllText(file, "owned fixture");
        File.SetAttributes(file, FileAttributes.Hidden);
        try
        {
            foreach (string path in new[] { file, root, literal, Path.Combine(root, ".", "literal [abc] #; space", "hidden [x].txt") })
                AuthorityFiles.RequirePlainPath(root, path, "fixture_reparse");
            AuthorityFiles.RequirePlainPath(root + Path.DirectorySeparatorChar, literal, "fixture_reparse");
            foreach (string path in new[] { root + "-outside/leaf", Path.Combine(root, "missing", "leaf"), file + "/leaf" })
                Reject(() => AuthorityFiles.RequirePlainPath(root, path, "fixture_reparse"), "fixture_reparse");
            string target = Path.Combine(root, "target"), junction = Path.Combine(root, "junction");
            Directory.CreateDirectory(target); File.WriteAllText(Path.Combine(target, "leaf"), "owned");
            TestJunction.Create(junction, target);
            try { Reject(() => AuthorityFiles.RequirePlainPath(root, Path.Combine(junction, "leaf"), "fixture_reparse"), "fixture_reparse"); }
            finally { Directory.Delete(junction); }
            // A file symlink may require local privilege; the junction above always exercises reparse rejection.
            string link = Path.Combine(root, "file-link");
            try
            {
                File.CreateSymbolicLink(link, file);
                try { Reject(() => AuthorityFiles.RequirePlainPath(root, link, "fixture_reparse"), "fixture_reparse"); }
                finally { File.Delete(link); }
            }
            catch (UnauthorizedAccessException) { Console.WriteLine("PATH_HELPER_SYMLINK_UNAVAILABLE privilege"); }
            catch (IOException error) when ((error.HResult & 0xffff) == 1314) { Console.WriteLine("PATH_HELPER_SYMLINK_UNAVAILABLE privilege"); }
            if (OperatingSystem.IsWindows())
            {
                string restricted = Path.Combine(root, "restricted");
                File.WriteAllText(restricted, "owned ACL fixture");
                var info = new FileInfo(restricted);
                var original = info.GetAccessControl();
                bool changed = false;
                try
                {
                    var denied = info.GetAccessControl();
                    using var identity = WindowsIdentity.GetCurrent();
                    denied.AddAccessRule(new FileSystemAccessRule(identity.User!, FileSystemRights.ReadAttributes, AccessControlType.Deny));
                    try { info.SetAccessControl(denied); changed = true; }
                    catch (UnauthorizedAccessException) { Console.WriteLine("PATH_HELPER_ACL_UNAVAILABLE privilege"); }
                    if (changed)
                    {
                        bool attributesDenied = false;
                        try { File.GetAttributes(restricted); } catch (UnauthorizedAccessException) { attributesDenied = true; }
                        if (attributesDenied) Reject(() => AuthorityFiles.RequirePlainPath(root, restricted, "fixture_reparse"), "fixture_reparse");
                        else Console.WriteLine("PATH_HELPER_ACL_UNAVAILABLE attributes_remain_readable");
                    }
                }
                finally { if (changed) info.SetAccessControl(original); }
            }
            Console.WriteLine("PATH_HELPER_PROBE_PASS implementation=CSharp literal_hidden_normalized_root_containment_missing_file_parent_junction=pass");
            return true;
        }
        finally { File.SetAttributes(file, FileAttributes.Normal); }
    }

    [HostTest("authority reader locks file writes and deletes through verification")]
    private static void Locks()
    {
        string root = Scratch(), path = Path.Combine(root, "input.bin");
        File.WriteAllBytes(path, [1, 2, 3]);
        using (AuthorityFiles files = new())
        {
            var record = files.Open(root, path, "input", "input", 3, singleLink: true);
            HostCheck.True(record.ReadBytes(3, "read").SequenceEqual(new byte[] { 1, 2, 3 }));
            bool writeRejected = false, deleteRejected = false;
            try { using var forbidden = File.OpenWrite(path); } catch (IOException) { writeRejected = true; }
            try { File.Delete(path); } catch (IOException) { deleteRejected = true; }
            HostCheck.True(writeRejected && deleteRejected);
            files.VerifyUnchanged();
        }
        File.WriteAllBytes(path, [4, 5, 6]);
    }

    [HostTest("authority reader bounds initial and repeated reads and rejects empty inputs")]
    private static void Bounds()
    {
        string root = Scratch(), path = Path.Combine(root, "input.bin");
        File.WriteAllBytes(path, [1, 2, 3]);
        using AuthorityFiles files = new();
        Reject(() => files.Open(root, path, "input", "input", 2), "input_size_out_of_range");
        var record = files.Open(root, path, "input", "input", 3);
        Reject(() => record.ReadBytes(2, "read_bound"), "read_bound");
        Reject(() => files.Open(root, path, "input", "input", 2), "input_size_out_of_range");
        string empty = Path.Combine(root, "empty"); File.WriteAllBytes(empty, []);
        Reject(() => files.Open(root, empty, "empty", "input"), "input_size_out_of_range");
        HostCheck.Equal(0L, files.Open(root, empty, "empty", "input", allowEmpty: true).Length);
    }

    [HostTest("authority reader accepts UTF8 BOM and rejects invalid encoding")]
    private static void Encoding()
    {
        string root = Scratch();
        using AuthorityFiles files = new();
        string good = Path.Combine(root, "utf8"), bad = Path.Combine(root, "invalid");
        File.WriteAllBytes(good, [0xef, 0xbb, 0xbf, 0x61]); File.WriteAllBytes(bad, [0xff]);
        HostCheck.Equal("a", files.Open(root, good, "good", "input").ReadUtf8(4, "utf8"));
        Reject(() => files.Open(root, bad, "bad", "input").ReadUtf8(4, "utf8"), "utf8");
    }

    [HostTest("authority reader rejects hardlinks and duplicate identities")]
    private static void Links()
    {
        string root = Scratch(), path = Path.Combine(root, "input"), alias = Path.Combine(root, "alias");
        File.WriteAllBytes(path, [1, 2, 3]);
        using (AuthorityFiles files = new())
        {
            var record = files.Open(root, path, "input", "input");
            Reject(() => AuthorityFiles.RequireDistinct(record, record), "authority_output_file_alias");
        }
        if (!CreateHardLinkW(alias, path, IntPtr.Zero)) throw new Win32Exception(Marshal.GetLastWin32Error());
        using AuthorityFiles linked = new();
        Reject(() => linked.Open(root, path, "input", "input", singleLink: true), "authority_output_hardlink_invalid");
        var permissive = linked.Open(root, path, "input", "input");
        Reject(() => linked.Open(root, alias, "alias", "input", singleLink: true), "authority_output_hardlink_invalid");
        Reject(() => AuthorityFiles.RequireDistinct(permissive), "authority_output_hardlink_invalid");
        Reject(() => linked.Open(root, path, "upgrade", "input", singleLink: true), "authority_output_hardlink_invalid");
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLinkW(string fileName, string existingFileName, IntPtr securityAttributes);
}
