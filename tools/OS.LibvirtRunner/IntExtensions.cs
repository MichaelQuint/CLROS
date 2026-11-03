using System.Text;
using System.Xml;
using System.Xml.Linq;
using OS.Tools.QemuRunner;

static class IntExtensions
{
    public static string ToStringInvariant(this int value)
    {
        return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
