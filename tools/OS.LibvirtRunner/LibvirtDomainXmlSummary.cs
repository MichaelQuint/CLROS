using System.Text;
using System.Xml;
using System.Xml.Linq;
using OS.Tools.QemuRunner;

readonly struct LibvirtDomainXmlSummary
{
    public LibvirtDomainXmlSummary(int xmlByteCount, string domainName, string domainType, int memoryMiB, int currentMemoryMiB, int vcpuCount, string osType, string osArch, string osMachine, string diskFormat, string diskSource, string serialLogPath, string network, bool hasOvmfLoader, string ovmfLoaderPath)
    {
        XmlByteCount = xmlByteCount;
        DomainName = domainName;
        DomainType = domainType;
        MemoryMiB = memoryMiB;
        CurrentMemoryMiB = currentMemoryMiB;
        VCpuCount = vcpuCount;
        OsType = osType;
        OsArch = osArch;
        OsMachine = osMachine;
        DiskFormat = diskFormat;
        DiskSource = diskSource;
        SerialLogPath = serialLogPath;
        Network = network;
        HasOvmfLoader = hasOvmfLoader;
        OvmfLoaderPath = ovmfLoaderPath;
    }

    public int XmlByteCount
    {
        get;
    }
    public string DomainName
    {
        get;
    }
    public string DomainType
    {
        get;
    }
    public int MemoryMiB
    {
        get;
    }
    public int CurrentMemoryMiB
    {
        get;
    }
    public int VCpuCount
    {
        get;
    }
    public string OsType
    {
        get;
    }
    public string OsArch
    {
        get;
    }
    public string OsMachine
    {
        get;
    }
    public string DiskFormat
    {
        get;
    }
    public string DiskSource
    {
        get;
    }
    public string SerialLogPath
    {
        get;
    }
    public string Network
    {
        get;
    }
    public bool HasOvmfLoader
    {
        get;
    }
    public string OvmfLoaderPath
    {
        get;
    }
}
