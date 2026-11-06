using System.Text;
using System.Xml;
using System.Xml.Linq;
using OS.Tools.QemuRunner;

static class BoolExtensions
{
    public static string ToDiagnosticString(this bool value)
    {
        return value ? "true" : "false";
    }
}
