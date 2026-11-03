using System.Globalization;
using System.Text.RegularExpressions;

namespace OS.Tooling;
public static partial class NativeAotSocketGuest
{
    /// <summary>Which serialized observation was captured from the real owner.</summary>

    public enum ObservationKind
    {
        Wake = 1,
        Repark = 2,
        Complete = 3,
        Poll = 4,
        Expire = 5
    }
}
