using OS.Std.Text;

namespace OS.Kernel.Diagnostics;
public readonly struct ProbeCatalogDiagnosticFormatResult
{
    private const uint InitializedFlag = 1;
    public ProbeCatalogDiagnosticFormatResult(ProbeCatalogDiagnosticFormatStatus status, int bytesWritten, int requiredBytes, int availableBytes, int definitionsFormatted, uint flags = InitializedFlag)
    {
        Status = status;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
        AvailableBytes = availableBytes;
        DefinitionsFormatted = definitionsFormatted;
        Flags = flags;
    }

    public ProbeCatalogDiagnosticFormatStatus Status { get; }
    public int BytesWritten { get; }
    public int RequiredBytes { get; }
    public int AvailableBytes { get; }
    public int DefinitionsFormatted { get; }
    public uint Flags { get; }
    public bool IsInitialized => (Flags & InitializedFlag) != 0;
    public bool Succeeded => IsInitialized && Status == ProbeCatalogDiagnosticFormatStatus.Success && BytesWritten == RequiredBytes && RequiredBytes <= AvailableBytes;

    public static ProbeCatalogDiagnosticFormatResult Fail(ProbeCatalogDiagnosticFormatStatus status, int requiredBytes, int availableBytes, int definitionsFormatted)
    {
        return new ProbeCatalogDiagnosticFormatResult(status, bytesWritten: 0, requiredBytes, availableBytes, definitionsFormatted);
    }
}
