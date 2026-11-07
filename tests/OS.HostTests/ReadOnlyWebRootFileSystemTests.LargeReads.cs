using System.Buffers.Binary;
using Fs = OS.Storage.ReadOnlyWebRootFileSystem;

internal static unsafe partial class ReadOnlyWebRootFileSystemTests
{
    [HostTest("webroot.fs sixteen sector reads retain contiguous and fragmented bytes with request and sector counters in distinct units")]
    private static void LargeDataReads()
    {
        foreach (bool fragmented in new[] { false, true })
        {
            using var f = new Fixture(dataReadSectors: 16, largeReads: true);
            LongIndex(f, 20);
            uint[] chain = new uint[20];
            chain[0] = 10;
            for (uint i = 1; i < chain.Length; i++)
                chain[i] = 1023 + i;
            if (fragmented)
                chain[8] = 2000;
            for (int i = 0; i < chain.Length; i++)
            {
                f.Disk.SetFat(chain[i], i == chain.Length - 1 ? 0x0fffffffU : chain[i + 1]);
                byte[] sector = f.Disk.Sector(f.Disk.Data(chain[i]));
                for (int j = 0; j < 512; j++)
                    sector[j] = Payload(i * 512 + j);
            }
            BinaryPrimitives.WriteUInt32LittleEndian(f.Disk.Sector(f.Disk.Data(4)).AsSpan(60), 20 * 512 - 37);
            f.MountReady();
            var file = f.Open("/app/wwwroot/index.html");
            foreach (var range in new[] { (Offset: 512UL, Bytes: 8192U), (Offset: 3UL, Bytes: 9500U), (Offset: 19 * 512UL, Bytes: 512U) })
            {
                ulong requests = f.Device->Submitted, sectors = f.Mount->SectorReads;
                uint expected = Math.Min(range.Bytes, (uint)(20 * 512 - 37 - range.Offset));
                byte[] output = Enumerable.Repeat((byte)0xa5, (int)range.Bytes).ToArray();
                f.Read(file.File, range.Offset, range.Bytes, output, expected);
                for (uint i = 0; i < expected; i++)
                    HostCheck.Equal(Payload((int)(range.Offset + i)), output[i]);
                HostCheck.True(output.AsSpan((int)expected).IndexOfAnyExcept((byte)0xa5) < 0);
                if (range.Offset == 512)
                {
                    HostCheck.Equal(fragmented ? 3UL : 1UL, f.Device->Submitted - requests);
                    HostCheck.Equal(16UL, f.Mount->SectorReads - sectors);
                }
            }
            // A second read must reach the device again and see changed data.
            f.Disk.Sector(f.Disk.Data(chain[1]))[0] ^= 0xff;
            byte[] changed = new byte[8192];
            ulong before = f.Device->Submitted;
            f.Read(file.File, 512, 8192, changed, 8192);
            HostCheck.Equal((byte)(Payload(512) ^ 0xff), changed[0]);
            HostCheck.True(f.Device->Submitted > before);
            HostCheck.Equal(Fs.Status.Success, Fs.Close(f.Mount, file.File));
        }
    }
}
