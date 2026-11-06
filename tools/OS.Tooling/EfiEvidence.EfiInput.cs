using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace OS.Tooling;

public sealed record EfiInput(string Path, string Sha256, long Bytes = -1);
