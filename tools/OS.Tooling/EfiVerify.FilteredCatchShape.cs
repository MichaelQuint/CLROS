using System.Buffers.Binary;
using static OS.Tooling.EfiPeImage;

namespace OS.Tooling;
public static partial class EfiVerify
{
    internal sealed record FilteredCatchShape(string Name, string Hex, FilteredCatchRelocation[] Fields);
}
