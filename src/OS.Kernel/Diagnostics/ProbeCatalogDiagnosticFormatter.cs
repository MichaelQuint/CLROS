using OS.Std.Text;

namespace OS.Kernel.Diagnostics;
public static class ProbeCatalogDiagnosticFormatter
{
    public const bool UsesSharedAsciiTextBuilder = true;
    public const bool UsesCallerProvidedDestination = true;
    public const bool AllocatesManagedStorage = false;
    public const bool ExecutesProbes = false;
    public const bool PerformsLiveDeviceIo = false;
    public const bool StartsKestrel = false;
    private static ReadOnlySpan<byte> MarkerPrefix => "PROBE_CATALOG name="u8;
    private static ReadOnlySpan<byte> NewLine => "\r\n"u8;

    public static ProbeCatalogDiagnosticFormatResult QueryDefinitionRequiredBytes(in ProbeDefinition definition)
    {
        ProbeCatalogDiagnosticFormatStatus validation = ValidateDefinition(in definition);
        if (validation != ProbeCatalogDiagnosticFormatStatus.Success)
        {
            return ProbeCatalogDiagnosticFormatResult.Fail(validation, requiredBytes: 0, availableBytes: 0, definitionsFormatted: 0);
        }

        return new ProbeCatalogDiagnosticFormatResult(ProbeCatalogDiagnosticFormatStatus.Success, bytesWritten: 0, requiredBytes: GetDefinitionRequiredBytes(in definition), availableBytes: 0, definitionsFormatted: 0);
    }

    public static ProbeCatalogDiagnosticFormatResult FormatDefinition(in ProbeDefinition definition, Span<byte> destination)
    {
        ProbeCatalogDiagnosticFormatStatus validation = ValidateDefinition(in definition);
        if (validation != ProbeCatalogDiagnosticFormatStatus.Success)
        {
            return ProbeCatalogDiagnosticFormatResult.Fail(validation, requiredBytes: 0, destination.Length, definitionsFormatted: 0);
        }

        int requiredBytes = GetDefinitionRequiredBytes(in definition);
        if (destination.Length < requiredBytes)
        {
            return ProbeCatalogDiagnosticFormatResult.Fail(ProbeCatalogDiagnosticFormatStatus.DestinationTooSmall, requiredBytes, destination.Length, definitionsFormatted: 0);
        }

        int offset = 0;
        if (!TryAppendDefinition(in definition, destination, ref offset))
        {
            return ProbeCatalogDiagnosticFormatResult.Fail(ProbeCatalogDiagnosticFormatStatus.DestinationTooSmall, requiredBytes, destination.Length, definitionsFormatted: 0);
        }

        return new ProbeCatalogDiagnosticFormatResult(ProbeCatalogDiagnosticFormatStatus.Success, bytesWritten: offset, requiredBytes, availableBytes: destination.Length, definitionsFormatted: 1);
    }

    public static ProbeCatalogDiagnosticFormatResult QueryCatalogRequiredBytes(ReadOnlySpan<ProbeDefinition> catalog)
    {
        ProbeCatalogDiagnosticFormatStatus validation = TryGetCatalogRequiredBytes(catalog, out int requiredBytes);
        if (validation != ProbeCatalogDiagnosticFormatStatus.Success)
        {
            return ProbeCatalogDiagnosticFormatResult.Fail(validation, requiredBytes: 0, availableBytes: 0, definitionsFormatted: 0);
        }

        return new ProbeCatalogDiagnosticFormatResult(ProbeCatalogDiagnosticFormatStatus.Success, bytesWritten: 0, requiredBytes, availableBytes: 0, definitionsFormatted: 0);
    }

    public static ProbeCatalogDiagnosticFormatResult FormatCatalog(ReadOnlySpan<ProbeDefinition> catalog, Span<byte> destination)
    {
        ProbeCatalogDiagnosticFormatStatus validation = TryGetCatalogRequiredBytes(catalog, out int requiredBytes);
        if (validation != ProbeCatalogDiagnosticFormatStatus.Success)
        {
            return ProbeCatalogDiagnosticFormatResult.Fail(validation, requiredBytes: 0, destination.Length, definitionsFormatted: 0);
        }

        if (destination.Length < requiredBytes)
        {
            return ProbeCatalogDiagnosticFormatResult.Fail(ProbeCatalogDiagnosticFormatStatus.DestinationTooSmall, requiredBytes, destination.Length, definitionsFormatted: 0);
        }

        int offset = 0;
        for (int index = 0; index < catalog.Length; index++)
        {
            ProbeDefinition definition = catalog[index];
            if (index != 0 && !TryAppendBytes(NewLine, destination, ref offset))
            {
                return ProbeCatalogDiagnosticFormatResult.Fail(ProbeCatalogDiagnosticFormatStatus.DestinationTooSmall, requiredBytes, destination.Length, index);
            }

            if (!TryAppendDefinition(in definition, destination, ref offset))
            {
                return ProbeCatalogDiagnosticFormatResult.Fail(ProbeCatalogDiagnosticFormatStatus.DestinationTooSmall, requiredBytes, destination.Length, index);
            }
        }

        return new ProbeCatalogDiagnosticFormatResult(ProbeCatalogDiagnosticFormatStatus.Success, bytesWritten: offset, requiredBytes, availableBytes: destination.Length, definitionsFormatted: catalog.Length);
    }

    private static ProbeCatalogDiagnosticFormatStatus TryGetCatalogRequiredBytes(ReadOnlySpan<ProbeDefinition> catalog, out int requiredBytes)
    {
        requiredBytes = 0;
        for (int index = 0; index < catalog.Length; index++)
        {
            ProbeDefinition definition = catalog[index];
            ProbeCatalogDiagnosticFormatStatus validation = ValidateDefinition(in definition);
            if (validation != ProbeCatalogDiagnosticFormatStatus.Success)
            {
                return validation;
            }

            int entryBytes = GetDefinitionRequiredBytes(in definition);
            int separatorBytes = index == 0 ? 0 : NewLine.Length;
            if (requiredBytes > int.MaxValue - entryBytes - separatorBytes)
            {
                requiredBytes = 0;
                return ProbeCatalogDiagnosticFormatStatus.RequiredBytesOverflow;
            }

            requiredBytes += separatorBytes + entryBytes;
        }

        return ProbeCatalogDiagnosticFormatStatus.Success;
    }

    private static ProbeCatalogDiagnosticFormatStatus ValidateDefinition(in ProbeDefinition definition)
    {
        if (!IsValidAsciiToken(definition.Name) || !IsValidAsciiToken(definition.Group) || !IsKnownRisk(definition.Risk))
        {
            return ProbeCatalogDiagnosticFormatStatus.InvalidDefinition;
        }

        return ProbeCatalogDiagnosticFormatStatus.Success;
    }

    private static int GetDefinitionRequiredBytes(in ProbeDefinition definition)
    {
        return MarkerPrefix.Length + definition.Name.Length + " group="u8.Length + definition.Group.Length + " enabled="u8.Length + GetBoolName(definition.EnabledByDefault).Length + " risk="u8.Length + GetRiskName(definition.Risk).Length + " requires="u8.Length + GetRequiredCapabilityName(definition.Risk).Length + " destructive="u8.Length + GetBoolName(IsDestructive(definition.Risk)).Length;
    }

    private static bool TryAppendDefinition(in ProbeDefinition definition, Span<byte> destination, ref int offset)
    {
        return TryAppendBytes(MarkerPrefix, destination, ref offset) && TryAppendChars(definition.Name, destination, ref offset) && TryAppendBytes(" group="u8, destination, ref offset) && TryAppendChars(definition.Group, destination, ref offset) && TryAppendBytes(" enabled="u8, destination, ref offset) && TryAppendBytes(GetBoolName(definition.EnabledByDefault), destination, ref offset) && TryAppendBytes(" risk="u8, destination, ref offset) && TryAppendBytes(GetRiskName(definition.Risk), destination, ref offset) && TryAppendBytes(" requires="u8, destination, ref offset) && TryAppendBytes(GetRequiredCapabilityName(definition.Risk), destination, ref offset) && TryAppendBytes(" destructive="u8, destination, ref offset) && TryAppendBytes(GetBoolName(IsDestructive(definition.Risk)), destination, ref offset);
    }

    private static bool IsValidAsciiToken(string value)
    {
        if (value is null || value.Length == 0)
        {
            return false;
        }

        bool hasNonWhiteSpace = false;
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if (character > 0x7f)
            {
                return false;
            }

            if (!OS.Std.Ascii.IsWhiteSpace(character))
            {
                hasNonWhiteSpace = true;
            }
        }

        return hasNonWhiteSpace;
    }

    private static bool IsKnownRisk(ProbeRisk risk)
    {
        return risk >= ProbeRisk.Safe && risk <= ProbeRisk.Destructive;
    }

    private static bool IsDestructive(ProbeRisk risk)
    {
        return risk == ProbeRisk.Destructive;
    }

    private static ReadOnlySpan<byte> GetRiskName(ProbeRisk risk)
    {
        return risk switch
        {
            ProbeRisk.Safe => "safe"u8,
            ProbeRisk.BoundarySimulation => "boundary-simulation"u8,
            ProbeRisk.DeviceIo => "device-io"u8,
            ProbeRisk.Destructive => "destructive"u8,
            _ => ReadOnlySpan<byte>.Empty
        };
    }

    private static ReadOnlySpan<byte> GetRequiredCapabilityName(ProbeRisk risk)
    {
        return risk switch
        {
            ProbeRisk.Safe => "none"u8,
            ProbeRisk.BoundarySimulation => "boundary-simulation"u8,
            ProbeRisk.DeviceIo => "device-io"u8,
            ProbeRisk.Destructive => "destructive-probe"u8,
            _ => ReadOnlySpan<byte>.Empty
        };
    }

    private static ReadOnlySpan<byte> GetBoolName(bool value)
    {
        return value ? "true"u8 : "false"u8;
    }

    private static bool TryAppendBytes(ReadOnlySpan<byte> value, Span<byte> destination, ref int offset)
    {
        AsciiTextBuilderResult result = AsciiTextBuilder.AppendBytes(destination, offset, value);
        if (!result.Succeeded)
        {
            return false;
        }

        offset = result.NextOffset;
        return true;
    }

    private static bool TryAppendChars(string value, Span<byte> destination, ref int offset)
    {
        AsciiTextBuilderResult result = AsciiTextBuilder.AppendChars(destination, offset, value.AsSpan());
        if (!result.Succeeded)
        {
            return false;
        }

        offset = result.NextOffset;
        return true;
    }
}
