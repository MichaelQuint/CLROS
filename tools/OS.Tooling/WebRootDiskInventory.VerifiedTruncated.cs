using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Std.Time;
using OS.Storage;
using Chain = OS.Storage.FATClusterChainsLoopBadClusterTruncationDetection;
using Vfat = OS.Storage.MultiClusterDirectoryVFATLongNameSupportSeparate;

namespace OS.Tooling;

public static partial class WebRootDiskInventory
{
    /// <summary>Copied authority for the fixed truncated-chain control. Creation
    /// independently verifies both distinct images and the complete byte delta;
    /// a caller-supplied receipt alone cannot construct this value. Baseline
    /// remains a healthy inventory with its original hash. Original consumers
    /// must hold both images and receipts unchanged and reverify at acceptance.
    /// This value grants no native, process, HTTP or guest authority.</summary>

    public sealed class VerifiedTruncated
    {
        public const string Target = Root + "/fixture/fragmented.bin";
        private readonly string root, positivePath, faultPath, positiveJson;
        private readonly WebRootImageFixture.FaultInspection inspected;
        public Inspection Baseline
        {
            get;
        }
        public string ActualImageSha256 => inspected.Receipt.FinalSha256;
        public WebRootImageFixture.FaultInspection Fault => inspected;

        private VerifiedTruncated(string root, string positivePath, string faultPath, string positiveJson, Inspection baseline, WebRootImageFixture.FaultInspection inspected)
        {
            this.root = root;
            this.positivePath = positivePath;
            this.faultPath = faultPath;
            this.positiveJson = positiveJson;
            Baseline = baseline;
            this.inspected = inspected;
        }

        /// <summary>Synchronously borrow plain distinct image paths and receipt
        /// values; copy receipt arrays before checking. Throw on any nonfixed
        /// mutation, forged identity, alias, malformed positive or wrong variant.
        /// Files are read only; the result retains no lease or mutable input.</summary>

        public static VerifiedTruncated Verify(string root, string positivePath, WebRootImageFixture.Receipt positive, string faultPath, WebRootImageFixture.FaultReceipt fault)
        {
            Need(positive is not null && fault is { Variant: "Truncated", TargetPath: Target }, "truncated_input");
            root = Path.GetFullPath(root);
            positivePath = Path.GetFullPath(positivePath, root);
            faultPath = Path.GetFullPath(faultPath, root);
            string json = JsonSerializer.Serialize(positive);
            var copied = JsonSerializer.Deserialize<WebRootImageFixture.Receipt>(json)!;
            var inspected = WebRootImageFixture.VerifyFaultVariant(root, positivePath, copied, faultPath, fault);
            var baseline = Inspect(positivePath, File.ReadAllBytes(positivePath), copied);
            return new(Path.GetFullPath(root), Path.GetFullPath(positivePath), Path.GetFullPath(faultPath), json, baseline, inspected);
        }

        /// <summary>Require this exact copied baseline and actual held fault
        /// bytes. A healthy inventory's image hash is never rewritten. No file
        /// lease or runtime observation is inferred from this synchronous join.</summary>

        internal void RequireValues(Inspection baseline, ReadOnlySpan<byte> image)
        {
            Need(ReferenceEquals(Baseline, baseline) && Convert.ToHexString(SHA256.HashData(image)) == ActualImageSha256, "truncated_values");
        }

        /// <summary>Recheck the complete independent positive/fault delta using
        /// original paths. The enclosing original owner keeps all inputs held
        /// through this check and final acceptance. A changed file throws.</summary>

        public void VerifyUnchanged()
        {
            var positive = JsonSerializer.Deserialize<WebRootImageFixture.Receipt>(positiveJson)!;
            Need(WebRootImageFixture.VerifyFaultVariant(root, positivePath, positive, faultPath, inspected.Receipt) == inspected, "truncated_changed");
        }
    }
}
