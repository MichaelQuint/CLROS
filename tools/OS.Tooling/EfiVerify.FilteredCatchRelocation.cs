using System.Buffers.Binary;
using static OS.Tooling.EfiPeImage;

namespace OS.Tooling;
public static partial class EfiVerify
{
    internal sealed record FilteredCatchRelocation(int Field, string Target, long Addend, string Owner);
}
