using System.Buffers.Binary;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Manifest;
using OS.Tooling;
using F = OS.Tooling.LinuxDiskFileProofFixture;

internal static partial class LinuxDiskCaptureTests
{
    private sealed class Capture(string[] rows, byte[] image, string path, WebRootImageFixture.Receipt receipt)
    {
        internal readonly string Path = path;
        internal readonly string[] Rows = rows;
        internal readonly byte[] Package = F.CreatePackage(), Image = image;
        internal readonly WebRootImageFixture.Receipt Receipt = receipt;
        internal QemuTranscripts.LinuxDiskCaptureInspection Inspect() => QemuTranscripts.InspectLinuxDiskCaptureValues(Rows, Package, Image, Receipt, Path);
        internal void Reject(string[] candidate, string reason, byte[]? image = null, WebRootImageFixture.Receipt? receipt = null)
        {
            string? actual = null;
            try
            {
                _ = QemuTranscripts.InspectLinuxDiskCaptureValues(candidate, Package, image ?? Image, receipt ?? Receipt, Path);
            }
            catch (ToolFailure error)
            {
                actual = error.Reason;
            }

            HostCheck.True(actual?.Contains(reason, StringComparison.Ordinal) == true, reason + " actual=" + actual);
        }
    }
}
