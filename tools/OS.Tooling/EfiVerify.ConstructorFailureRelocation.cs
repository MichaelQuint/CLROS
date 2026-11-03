using System.Buffers.Binary;
using static OS.Tooling.EfiPeImage;

namespace OS.Tooling;

public static partial class EfiVerify
{
    internal sealed record ConstructorFailureRelocation(int Field, string Target, long Addend, string Owner);
}
