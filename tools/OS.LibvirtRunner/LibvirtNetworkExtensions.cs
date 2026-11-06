using System.Text;
using System.Xml;
using System.Xml.Linq;
using OS.Tools.QemuRunner;

static class LibvirtNetworkExtensions
{
    public static string ToDiagnosticString(this LibvirtNetwork value)
    {
        return value == LibvirtNetwork.Default ? "default" : "none";
    }
}
