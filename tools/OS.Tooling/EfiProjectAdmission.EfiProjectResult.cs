using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml;
using OS.Tooling.Native;
using static OS.Tooling.EfiEvidence;
using static OS.Tooling.EfiPeImage;

namespace OS.Tooling;
public sealed record EfiProjectResult(bool Succeeded, string Reason, string Detail, int CompileItemCount, int ReviewedSourceCount, int PrerequisiteSourceCount);
