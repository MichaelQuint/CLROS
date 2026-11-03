namespace OS.Storage;

public readonly struct Fat32DirectoryEntry
{
    public Fat32DirectoryEntry(
        string shortName,
        byte attributes,
        uint firstCluster,
        uint fileSize)
    {
        ShortName = shortName;
        Attributes = attributes;
        FirstCluster = firstCluster;
        FileSize = fileSize;
    }

    public string ShortName
    {
        get;
    }

    public byte Attributes
    {
        get;
    }

    public uint FirstCluster
    {
        get;
    }

    public uint FileSize
    {
        get;
    }

    public bool IsDirectory => (Attributes & Fat32Parser.DirectoryAttribute) != 0;
}
