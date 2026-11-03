using System.Text;
using System.Xml;
using System.Xml.Linq;
using OS.Tools.QemuRunner;

readonly struct LibvirtDomainXmlValidationResult
{
    private LibvirtDomainXmlValidationResult(bool succeeded, string status, LibvirtDomainXmlSummary summary)
    {
        Succeeded = succeeded;
        Status = status;
        Summary = summary;
    }

    public bool Succeeded
    {
        get;
    }
    public string Status
    {
        get;
    }
    public LibvirtDomainXmlSummary Summary
    {
        get;
    }

    public static LibvirtDomainXmlValidationResult Pass(LibvirtDomainXmlSummary summary)
    {
        return new LibvirtDomainXmlValidationResult(true, "valid", summary);
    }

    public static LibvirtDomainXmlValidationResult Fail(string status)
    {
        return new LibvirtDomainXmlValidationResult(false, status, default);
    }
}
