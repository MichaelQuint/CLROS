using System.Text;
using System.Text.RegularExpressions;

namespace OS.Tooling;

public sealed record EfiSourceResult(bool Succeeded, string Reason, string Detail, string ActiveText);
