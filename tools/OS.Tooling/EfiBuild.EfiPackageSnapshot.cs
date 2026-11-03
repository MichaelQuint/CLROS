using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using OS.Tooling.Native;
using static OS.Tooling.EfiEvidence;
using static OS.Tooling.EfiPeImage;

namespace OS.Tooling;

public static partial class EfiBuild
{
    public sealed record EfiPackageSnapshot(JsonArray Packages, string AggregateSha256, EfiSnapshot Runtime);
}
