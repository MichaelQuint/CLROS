using System.Buffers.Binary;
using static OS.Tooling.EfiPeImage;

namespace OS.Tooling;
public static partial class EfiVerify
{
    internal sealed record ConstructorFailureShape(string Name, string Hex, ConstructorFailureRelocation[] Fields);
}
