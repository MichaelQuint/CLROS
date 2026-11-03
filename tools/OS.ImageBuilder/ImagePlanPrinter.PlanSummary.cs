using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OS.Boot;
using OS.Kernel.Build;
using OS.Manifest;
using OS.Storage;

internal static partial class ImagePlanPrinter
{
    private sealed class PlanSummary
    {
        public PlanSummary(string mode, string format, string workloadProfile, string outputPath, int imageSizeMiB, long imageBytes, int sectorSize, long totalSectors, bool writesSuppressed)
        {
            Mode = mode;
            Format = format;
            WorkloadProfile = workloadProfile;
            OutputPath = outputPath;
            ImageSizeMiB = imageSizeMiB;
            ImageBytes = imageBytes;
            SectorSize = sectorSize;
            TotalSectors = totalSectors;
            WritesSuppressed = writesSuppressed;
        }

        public string Mode
        {
            get;
        }
        public string Format
        {
            get;
        }
        public string WorkloadProfile
        {
            get;
        }
        public string OutputPath
        {
            get;
        }
        public int ImageSizeMiB
        {
            get;
        }
        public long ImageBytes
        {
            get;
        }
        public int SectorSize
        {
            get;
        }
        public long TotalSectors
        {
            get;
        }
        public bool WritesSuppressed
        {
            get;
        }
    }
}
