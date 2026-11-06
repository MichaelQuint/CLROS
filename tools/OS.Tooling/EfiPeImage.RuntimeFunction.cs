using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace OS.Tooling;

public sealed partial class EfiPeImage
{
    public sealed record RuntimeFunction(uint Begin, uint End, uint Unwind, int UnwindOffset, int UnwindSize);
}
