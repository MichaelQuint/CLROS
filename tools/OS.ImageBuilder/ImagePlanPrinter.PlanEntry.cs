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
    private sealed class PlanEntry
    {
        private PlanEntry(int order, string kind, string name, string status, long? lba, long? offsetBytes, long? sectors, long? bytes, string? guestPath, string? sourcePath, string? reason)
        {
            Order = order;
            Kind = kind;
            Name = name;
            Status = status;
            Lba = lba;
            OffsetBytes = offsetBytes;
            Sectors = sectors;
            Bytes = bytes;
            GuestPath = guestPath;
            SourcePath = sourcePath;
            Reason = reason;
        }

        public int Order
        {
            get;
        }
        public string Kind
        {
            get;
        }
        public string Name
        {
            get;
        }
        public string Status
        {
            get;
        }
        public long? Lba
        {
            get;
        }
        public long? OffsetBytes
        {
            get;
        }
        public long? Sectors
        {
            get;
        }
        public long? Bytes
        {
            get;
        }
        public string? GuestPath
        {
            get;
        }
        public string? SourcePath
        {
            get;
        }
        public string? Reason
        {
            get;
        }

        public static PlanEntry Planned(int order, string kind, string name, long lba, long sectors, long? bytes = null, string? guestPath = null, string? sourcePath = null)
        {
            return new PlanEntry(order, kind, name, "planned", lba, lba * RawEspImageBuilder.SectorSize, sectors, bytes ?? sectors * RawEspImageBuilder.SectorSize, guestPath, sourcePath, reason: null);
        }

        public static PlanEntry Placeholder(int order, string kind, string name, string guestPath, string reason)
        {
            return new PlanEntry(order, kind, name, "placeholder", lba: null, offsetBytes: null, sectors: null, bytes: null, guestPath, sourcePath: null, reason);
        }
    }
}
