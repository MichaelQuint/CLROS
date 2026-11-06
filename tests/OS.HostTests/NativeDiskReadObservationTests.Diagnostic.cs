using OS.Tooling;
using Reads = OS.Tooling.NativeDiskReadObservations;

internal static partial class NativeDiskReadObservationTests
{
    [HostTest("diagnostic disk trace and summary validate independent image CRCs and one through four sector groups")]
    private static void DiagnosticGroups()
    {
        HostCheck.Equal(0xcbf43926U, DiagnosticCrc("123456789"u8));
        foreach (bool payload in new[] { false, true })
        {
            for (uint sectors = 1; sectors <= 4; sectors++)
            {
                var fixture = sectors == 1 ? Vector(0, 0, false) : MultiVector(sectors);
                string[] rows = DiagnosticRows(fixture, payload);
                var result = Reads.InspectDiagnostic(rows, fixture.Image, fixture.Boundary, payload);
                HostCheck.Equal(fixture.Boundary, result.Counters);
                HostCheck.Equal(0UL, result.Cancelled);
                HostCheck.Equal((int)sectors + 2, result.Rows);
                HostCheck.Equal((int)sectors, result.CopiedSectors);
                HostCheck.Equal(payload, result.PayloadIncluded);
                string[] originalRows = rows.ToArray();
                byte[] originalImage = fixture.Image.ToArray();
                _ = Reads.InspectDiagnostic(rows, fixture.Image, fixture.Boundary, payload);
                HostCheck.True(rows.SequenceEqual(originalRows) && fixture.Image.AsSpan().SequenceEqual(originalImage));
                Array.Fill(rows, "changed");
                Array.Clear(fixture.Image);
                HostCheck.Equal(fixture.Boundary, result.Counters);
                HostCheck.Equal((int)sectors, result.CopiedSectors);
            }
        }
    }

    [HostTest("diagnostic disk keeps cancellation ring wrap maximum counters and drained terminal checks")]
    private static void DiagnosticCancellation()
    {
        foreach (bool payload in new[] { false, true })
        {
            var fixture = Vector(65534, 65534, false, true, false);
            var result = Reads.InspectDiagnostic(DiagnosticRows(fixture, payload), fixture.Image, fixture.Boundary, payload);
            HostCheck.True(result.Cancelled == 1 && result.CopiedSectors == 2 && result.Rows == 8);
            var cancelled = MultiVector(4, true);
            result = Reads.InspectDiagnostic(DiagnosticRows(cancelled, payload), cancelled.Image, cancelled.Boundary, payload);
            HostCheck.True(result.Cancelled == 1 && result.CopiedSectors == 0 && result.Rows == 2);
            var maximum = Vector(ulong.MaxValue - 1, ulong.MaxValue - 1, false);
            result = Reads.InspectDiagnostic(DiagnosticRows(maximum, payload), maximum.Image, maximum.Boundary, payload);
            HostCheck.Equal(ulong.MaxValue, result.Counters.Copied);
        }
    }

    [HostTest("diagnostic disk rejects missing reordered foreign partial mismatched and out of bound records")]
    private static void DiagnosticRejected()
    {
        foreach (bool payload in new[] { false, true })
        {
            var fixture = MultiVector(4);
            string[] rows = DiagnosticRows(fixture, payload);
            for (int index = 0; index < rows.Length; index++)
            {
                DiagnosticDeny(fixture, rows.Where((_, i) => i != index).ToArray(), payload);
                foreach (string field in new[] { "owner", "epoch", "generation", "sequence", "bytes", "available" })
                    DiagnosticDeny(fixture, Replace(rows, index, Set(rows[index], field, "0")), payload);
                DiagnosticDeny(fixture, Replace(rows, index, rows[index] + " extra=0"), payload);
                DiagnosticDeny(fixture, Replace(rows, index, Set(rows[index], "payload", payload ? "0" : "1")), payload);
                DiagnosticDeny(fixture, Replace(rows, index, rows[index].Replace("CLROS_DISK_IO_V1", "M16_023_IO", StringComparison.Ordinal)), payload);
            }
            DiagnosticDeny(fixture, [.. rows, rows[^1]], payload);
            var reordered = rows.ToArray();
            (reordered[3], reordered[4]) = (reordered[4], reordered[3]);
            DiagnosticDeny(fixture, reordered, payload);
            DiagnosticDeny(fixture, Replace(rows, 1, Set(rows[1], "bytes", "512")), payload);
            DiagnosticDeny(fixture, Replace(rows, 3, Set(rows[3], "clock", "101")), payload);
            DiagnosticDeny(fixture, Replace(rows, 3, Set(rows[3], "chunk", "0")), payload);
            DiagnosticDeny(fixture, Replace(rows, 3, Set(rows[3], "consumed", "0")), payload);
            DiagnosticDeny(fixture, Replace(rows, 0, Set(rows[0], "bytes", "2560")), payload);
            DiagnosticDeny(fixture, [], payload);
            DiagnosticDeny(fixture, new CountOnly(Reads.MaximumRows + 1), payload);
            DiagnosticDeny(fixture, Replace(rows, 0, new string('x', Reads.MaximumRowChars + 1)), payload);
            DiagnosticDeny(fixture, Replace(rows, 0, null!), payload);
            DiagnosticDeny(fixture, rows, payload, boundary: fixture.Boundary with
            {
                Completed = 0
            });
            DiagnosticDeny(fixture, rows, payload, image: new byte[513]);
            for (int sector = 1; sector <= 4; sector++)
            {
                byte[] changed = fixture.Image.ToArray();
                changed[sector * 512] ^= 1;
                DiagnosticDeny(fixture, rows, payload, image: changed);
            }
        }
    }

    [HostTest("diagnostic disk CRC syntax mode and exact trace bytes cannot substitute for ordinary evidence")]
    private static void DiagnosticRepresentation()
    {
        var fixture = Vector(0, 0, false);
        foreach (bool payload in new[] { false, true })
        {
            string[] rows = DiagnosticRows(fixture, payload);
            foreach (string crc in new[] { "0", "00", "-1", "4294967296", "18446744073709551616", "0x1234", "1 " })
                DiagnosticDeny(fixture, Replace(rows, 2, Set(rows[2], "crc32", crc)), payload);
            DiagnosticDeny(fixture, DiagnosticRows(fixture, !payload), payload);
            DiagnosticDeny(fixture, fixture.Rows, payload);
            Reject(fixture, rows);
            if (payload)
            {
                int data = rows[2].IndexOf(" data=", StringComparison.Ordinal) + 6;
                string changed = rows[2][..data] + (rows[2][data] == '0' ? '1' : '0') + rows[2][(data + 1)..];
                DiagnosticDeny(fixture, Replace(rows, 2, changed), true);
                DiagnosticDeny(fixture, Replace(rows, 2, rows[2][..(data - 6)]), true);
            }
            else
                DiagnosticDeny(fixture, Replace(rows, 2, rows[2] + " data=" + new string('0', 1024)), false);
        }
    }

    private static string[] DiagnosticRows(Fixture fixture, bool payload)
    {
        return fixture.Rows.Select(row =>
        {
            int suffix = row.IndexOf(" copied=committed", StringComparison.Ordinal);
            string metadata = suffix < 0 ? row : row[..suffix];
            string result = metadata.Replace("M16_023_IO", "CLROS_DISK_IO_V1", StringComparison.Ordinal) + " payload=" + (payload ? "1" : "0");
            if (suffix < 0)
                return result;
            int data = row.IndexOf(" data=", StringComparison.Ordinal);
            byte[] bytes = Convert.FromHexString(row[(data + 6)..]);
            result += row[suffix..data] + " crc32=" + N(DiagnosticCrc(bytes));
            return payload ? result + row[data..] : result;
        }).ToArray();
    }

    // Independent MSB-first IEEE calculation; production uses reflected updates.
    private static uint DiagnosticCrc(ReadOnlySpan<byte> bytes)
    {
        uint crc = uint.MaxValue;
        foreach (byte value in bytes)
        {
            uint reflected = 0;
            for (int bit = 0; bit < 8; bit++)
                reflected = (reflected << 1) | ((uint)value >> bit & 1);
            crc ^= reflected << 24;
            for (int bit = 0; bit < 8; bit++)
                crc = (crc << 1) ^ ((crc & 0x80000000) == 0 ? 0 : 0x04c11db7U);
        }
        uint result = 0;
        for (int bit = 0; bit < 32; bit++)
            result = (result << 1) | (crc >> bit & 1);
        return result ^ uint.MaxValue;
    }

    private static void DiagnosticDeny(Fixture fixture, IReadOnlyList<string> rows, bool payload, byte[]? image = null, Reads.Boundary? boundary = null)
    {
        bool rejected = false;
        try
        {
            _ = Reads.InspectDiagnostic(rows, image ?? fixture.Image, boundary ?? fixture.Boundary, payload);
        }
        catch (ToolFailure)
        {
            rejected = true;
        }
        HostCheck.True(rejected);
    }
}
