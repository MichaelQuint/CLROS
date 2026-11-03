using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace OS.Tooling;
public static partial class DiskStaticHttpContract
{
    public readonly record struct Header(string Name, string Value);
}
