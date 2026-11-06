using System.Globalization;
using System.Text;
using OS.Tooling;

/// <summary>Synthetic serial values for host protocol/ownership tests only.</summary>
internal static class HostDiskDiagnosticFixture
{
    internal static (string Prefix, string BeforeMetrics, string AfterMetrics, int Rows, int Sectors) Create(
        byte[] image, WebRootDiskInventory.Entry asset, bool payload)
    {
        const ulong bootstrap = 9;
        const int bodyBytes = 64 * 1024;
        var sectors = asset.Sectors.Where(sector => sector.FileOffset < bodyBytes).ToArray();
        HostCheck.True(asset.Bytes >= bodyBytes && sectors.Length == bodyBytes / 512 && sectors.All(sector => sector.ValidBytes == 512));
        string mode = " payload=" + (payload ? "1" : "0");
        const string owner = " owner=4194312 epoch=1193046";
        const string initial = " submitted=9 completed=9 copied=9";
        string prefix = "M16_023_BOUND" + owner + " arena=4096 pages=1 bar=8192 capacity=" + N((ulong)image.Length / 512)
            + " bootstrapCompleted=9 bootstrapCopied=9 clock=99 pci=0:7:0 queue=0 outstanding=1 access=readonly progress=cooperative reservation=retained\r\n"
            + "M10_018_DISK_READY" + initial + " clock=100\r\n"
            + "CLROS_DISK_IO_BEGIN_V1" + mode + owner + initial + " cancelled=0 records=0 copiedRecords=0 clock=100\r\n";
        StringBuilder records = new();
        ulong clock = 100;
        for (int index = 0; index < sectors.Length; index++)
        {
            ulong sequence = bootstrap + (ulong)index + 1;
            var sector = sectors[index];
            ReadOnlySpan<byte> bytes = image.AsSpan(checked((int)(sector.Lba * 512)), 512);
            for (uint kind = 0; kind <= 2; kind++)
            {
                records.Append("CLROS_DISK_IO_V1 kind=").Append(N(kind)).Append(owner)
                    .Append(" generation=").Append(N(sequence)).Append(" sector=").Append(N(sector.Lba))
                    .Append(" sequence=").Append(N(sequence)).Append(" bytes=512 available=").Append(N(unchecked((ushort)sequence)))
                    .Append(" consumed=").Append(N(unchecked((ushort)(kind == 0 ? sequence - 1 : sequence))))
                    .Append(" clock=").Append(N(clock++)).Append(mode);
                if (kind == 2)
                {
                    records.Append(" copied=committed crc32=").Append(N(OS.Std.Hash.Crc32.Compute(bytes)));
                    if (payload)
                        records.Append(" data=").Append(Convert.ToHexString(bytes).ToLowerInvariant());
                }
                records.Append("\r\n");
            }
        }
        ulong final = bootstrap + (ulong)sectors.Length;
        int rowCount = sectors.Length * 3;
        records.Append("CLROS_DISK_IO_END_V1").Append(mode).Append(owner)
            .Append(" submitted=").Append(N(final)).Append(" completed=").Append(N(final)).Append(" copied=").Append(N(final))
            .Append(" cancelled=0 records=").Append(N((ulong)rowCount)).Append(" copiedRecords=").Append(N((ulong)sectors.Length))
            .Append(" clock=").Append(N(clock)).Append("\r\n");
        string suffix = "M10_018_FILE identity=1 ordinal=9 nr=17 fd=8 generation=2 kind=5 view=4294967295 inode=" + N(asset.Inode)
            + " bytes=" + N(asset.Bytes) + " position=0 afterPosition=0\r\n"
            + "M16_025_COMPLETE identity=1 sequence=11 request=15 outputBytes=8192 completion=1 valueBits=8192 clock=123\r\n";
        return (prefix, records.ToString(), suffix, rowCount, sectors.Length);
    }

    private static string N(ulong value) => value.ToString(CultureInfo.InvariantCulture);
}
