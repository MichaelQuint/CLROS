using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text.Json;
using OS.Std.Hash;
using OS.Storage;
using OS.Tooling.Native;
using DataImage = OS.Tooling.WritableDataImageFixture;
using Gpt = OS.Storage.PrimaryGPTIndependentlyBoundsCRC;

namespace OS.Tooling;
public static partial class CombinedWebDataImageFixture
{
    /// <summary>A mutable working image with separately held immutable image,
    /// receipt, EFI and package inputs. One exclusive builder/actual runner
    /// owns its use. The working handle permits read/write sharing, denies
    /// delete sharing on Windows, and anchors the initial file identity until
    /// Dispose; Unix replacement is detected when explicitly checked. These
    /// boundary checks are not an atomic execution or TOCTOU proof. Joining
    /// the actual child and qualifying the eventual modified image remain the
    /// runner's responsibility. No process, device or durability authority is
    /// supplied; Dispose releases handles without deleting any file.</summary>

    public sealed class WorkingImage : IDisposable
    {
        private readonly AuthorityFiles originals;
        private readonly FileStream working;
        private readonly string root;
        private bool disposed;
        internal WorkingImage(string root, AuthorityFiles originals, FileStream working, string path, string originalImage, string originalReceipt, string efi, string package)
        {
            this.root = root;
            this.originals = originals;
            this.working = working;
            Path = path;
            OriginalImagePath = originalImage;
            OriginalReceiptPath = originalReceipt;
            EfiPath = efi;
            PackagePath = package;
            IdentityKey = NativeFile.GetIdentity(working.SafeFileHandle).Key;
            working.Position = 0;
            InitialSha256 = Convert.ToHexString(SHA256.HashData(working));
            working.Position = 0;
        }

        public string Path { get; }
        public string OriginalImagePath { get; }
        public string OriginalReceiptPath { get; }
        public string EfiPath { get; }
        public string PackagePath { get; }
        public string IdentityKey { get; }
        public string InitialSha256 { get; }
        public long Length => ImageBytes;

        /// <summary>Recheck every immutable input and the working pathname,
        /// retained handle, single link and fixed length. Intentionally does
        /// not require current working bytes to equal the initial bytes.
        /// Call only at serialized boundaries; concurrent writers and eventual
        /// modified-image inspection are external responsibilities. Throws on
        /// revocation or after Dispose and performs no child or device work.</summary>

        public void VerifyOriginalsAndWorkingIdentity()
        {
            Need(!disposed, "working_disposed");
            originals.VerifyUnchanged();
            AuthorityFiles.RequirePlainPath(root, Path, "combined_image_working_path");
            try
            {
                var retained = NativeFile.GetIdentity(working.SafeFileHandle);
                using FileStream probe = new(Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var named = NativeFile.GetIdentity(probe.SafeFileHandle);
                Need(retained.Key == IdentityKey && named.Key == IdentityKey && retained.LinkCount == 1 && named.LinkCount == 1 && retained.Length == ImageBytes && named.Length == ImageBytes, "working_identity");
            }
            catch (Exception error)when (error is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
            {
                throw new ToolFailure("combined_image_working_identity", "type=" + error.GetType().Name);
            }

            originals.VerifyUnchanged();
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            try
            {
                working.Dispose();
            }
            finally
            {
                originals.Dispose();
            }
        }
    }
}
