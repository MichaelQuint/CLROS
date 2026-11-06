using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OS.Tooling;

public static partial class NativeAotDiskStaticBrowser
{
    /// <summary>Only this owner creates a completion, after the browser and its
    /// Windows job have joined. Retained paths/hash and process result identify
    /// the original capture; deserializing a receipt cannot create this value.</summary>

    public sealed class Completion
    {
        private readonly string root;
        private readonly IReadOnlyDictionary<string, string> files;
        internal Completion(string root, string directory, int port, string hash, string inventoryHash, ProcessResult process, Dom dom, Exchange[] exchanges, IReadOnlyDictionary<string, string> files)
        {
            this.root = root;
            this.files = files;
            Directory = directory;
            Port = port;
            ReceiptSha256 = hash;
            InventorySha256 = inventoryHash;
            Process = process;
            Document = dom;
            Exchanges = Array.AsReadOnly(exchanges);
        }

        public string Directory
        {
            get;
        }
        public int Port
        {
            get;
        }
        public string ReceiptSha256
        {
            get;
        }
        public string InventorySha256
        {
            get;
        }
        public ProcessResult Process
        {
            get;
        }
        public Dom Document
        {
            get;
        }
        public IReadOnlyList<Exchange> Exchanges
        {
            get;
        }
        public IReadOnlyList<string> ArtifactPaths => Array.AsReadOnly(files.Keys.Order(StringComparer.Ordinal).ToArray());
        public string Screenshot => Path.Combine(Directory, "screenshot.png");

        /// <summary>Rehash the original joined capture. An enclosing final
        /// consumer should additionally hold ArtifactPaths through its acceptance
        /// callbacks; this synchronous check alone does not close later races.</summary>

        public void VerifyUnchanged()
        {
            QemuTranscripts.RequireCompleteProcess(Process, "disk_browser_process_incomplete");
            Need(Process.Succeeded, "process_failed");
            foreach (var file in files)
            {
                AuthorityFiles.RequirePlainPath(root, file.Key, "disk_browser_original");
                Need(NativeAotWebPayload.HashFile(file.Key) == file.Value, "original_changed");
            }

            Need(NativeAotWebPayload.HashFile(Path.Combine(Directory, "receipt.json")) == ReceiptSha256, "receipt_changed");
        }
    }
}
