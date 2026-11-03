using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace OS.Tooling;
public sealed partial class EfiPeImage
{
    public sealed record Backing(Section Section, int Offset, int Size);
}
