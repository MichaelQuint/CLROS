using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace OS.Tooling;

public sealed record HostTerminalValidation(bool Valid, string Reason, int PassCount, int FailCount);
