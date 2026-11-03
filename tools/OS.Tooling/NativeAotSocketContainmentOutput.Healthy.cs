using System.Globalization;

namespace OS.Tooling;

public static partial class NativeAotSocketContainmentOutput
{
    public readonly record struct Healthy(ulong Role, ulong ElapsedMilliseconds, ulong Worker);
}
