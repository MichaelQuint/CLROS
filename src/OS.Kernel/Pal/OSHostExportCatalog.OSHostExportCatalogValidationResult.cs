namespace OS.Pal;
using OS.Std.Text;

public readonly struct OSHostExportCatalogValidationResult
{
    private const uint InitializedFlag = 1;
    private const uint EntryMetadataFlag = 2;
    public readonly OSHostExportCatalogStatus Status;
    public readonly int EntryIndex;
    public readonly int DuplicateEntryIndex;
    public readonly int EntryCount;
    public readonly OSHostExportDomain Domain;
    public readonly OSHostExportSignature Signature;
    public readonly uint Flags;
    public OSHostExportCatalogValidationResult(OSHostExportCatalogStatus status, int entryIndex, int duplicateEntryIndex, int entryCount, uint flags = InitializedFlag) : this(status, entryIndex, duplicateEntryIndex, entryCount, domain: default, signature: default, hasEntryMetadata: false, flags: flags)
    {
    }

    public OSHostExportCatalogValidationResult(OSHostExportCatalogStatus status, int entryIndex, int duplicateEntryIndex, int entryCount, OSHostExportDomain domain, OSHostExportSignature signature, bool hasEntryMetadata, uint flags = InitializedFlag)
    {
        Status = status;
        EntryIndex = entryIndex;
        DuplicateEntryIndex = duplicateEntryIndex;
        EntryCount = entryCount;
        Domain = domain;
        Signature = signature;
        Flags = hasEntryMetadata ? flags | EntryMetadataFlag : flags;
    }

    public bool IsInitialized => (Flags & InitializedFlag) != 0;
    public bool HasEntryMetadata => (Flags & EntryMetadataFlag) != 0;
    public bool Succeeded => IsInitialized && Status == OSHostExportCatalogStatus.Success && EntryCount > 0;
}
