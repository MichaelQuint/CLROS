namespace OS.Tooling;

public static partial class NativeDiskReadObservations
{
    /// <summary>
    /// Synchronously borrow ordered diagnostic rows and unchanged image bytes.
    /// Apply ordinary owner/epoch/generation/LBA/ring/committed-copy/terminal
    /// checks and the existing bounds; each copy CRC must match its image sector.
    /// Trace additionally requires exact payload bytes. Summary uses CRC only,
    /// which is not collision-resistant byte proof. Return copied counters and
    /// retain no input. Invalid/mixed/incomplete observations throw ToolFailure.
    /// The enclosing timing-only protocol owns original-process/native authority.
    /// </summary>
    public static DiagnosticInspection InspectDiagnostic(IReadOnlyList<string> selectedRows, ReadOnlySpan<byte> rawImage,
        Boundary boundary, bool payloadIncluded, uint maximumReadSectors = 4)
    {
        var representation = payloadIncluded ? Representation.DiagnosticTrace : Representation.DiagnosticSummary;
        var inspection = InspectCore(selectedRows, rawImage, boundary, false, null, true, representation, maximumReadSectors);
        return new DiagnosticInspection(inspection, payloadIncluded);
    }

    private static uint SectorCrc(ReadOnlySpan<byte> bytes)
    {
        uint crc = OS.Std.Hash.Crc32.InitialState;
        foreach (byte value in bytes)
            crc = OS.Std.Hash.Crc32.UpdateByte(crc, value);
        return OS.Std.Hash.Crc32.Finish(crc);
    }
}
