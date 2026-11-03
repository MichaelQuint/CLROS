using System.Text;
using System.Xml;
using System.Xml.Linq;
using OS.Tools.QemuRunner;

readonly struct LibvirtLifecyclePlanStep
{
    public LibvirtLifecyclePlanStep(int ordinal, string name)
    {
        Ordinal = ordinal;
        Name = name;
    }

    public int Ordinal
    {
        get;
    }
    public string Name
    {
        get;
    }
}
