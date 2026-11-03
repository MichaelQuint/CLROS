using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace OS.Tooling;

public static class CommandOptions
{
    public static string Key(string text) => text.TrimStart('-').Replace("-", "", StringComparison.Ordinal).ToLowerInvariant();
}
