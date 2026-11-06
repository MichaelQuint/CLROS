using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace OS.X64TextSerializerGenerator;

public static partial class EhContributionCatalogGenerator
{
    private readonly record struct Symbol(string Name, uint Value, short Section, ushort Type, byte Class);
}
