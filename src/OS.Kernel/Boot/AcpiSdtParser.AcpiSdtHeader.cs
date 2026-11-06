using OS.Std.Binary;

namespace OS.Boot;

public readonly struct AcpiSdtHeader
{
    public AcpiSdtHeader(uint signature, uint length, byte revision, byte checksum, ulong oemId, ulong oemTableId, uint oemRevision, uint creatorId, uint creatorRevision)
    {
        Signature = signature;
        Length = length;
        Revision = revision;
        Checksum = checksum;
        OemId = oemId;
        OemTableId = oemTableId;
        OemRevision = oemRevision;
        CreatorId = creatorId;
        CreatorRevision = creatorRevision;
    }

    public uint Signature
    {
        get;
    }
    public uint Length
    {
        get;
    }
    public byte Revision
    {
        get;
    }
    public byte Checksum
    {
        get;
    }
    public ulong OemId
    {
        get;
    }
    public ulong OemTableId
    {
        get;
    }
    public uint OemRevision
    {
        get;
    }
    public uint CreatorId
    {
        get;
    }
    public uint CreatorRevision
    {
        get;
    }
    public AcpiSdtTableKind TableKind => AcpiSdtParser.GetTableKind(Signature);
}
