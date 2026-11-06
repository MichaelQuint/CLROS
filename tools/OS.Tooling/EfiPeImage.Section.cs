using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace OS.Tooling;

public sealed partial class EfiPeImage
{
    public sealed record Section(string Name, uint VirtualSize, uint VirtualAddress, uint RawSize, uint RawOffset, uint Characteristics)
    {
        public ulong MappedSize => Math.Max(VirtualSize, RawSize);

        public void RequireCode(string reason) => Require((Characteristics & 0x60000020) == 0x60000020 && (Characteristics & 0x82000000) == 0, reason, Name);
        public void RequireReadOnlyData(string reason) => Require((Characteristics & 0x40000040) == 0x40000040 && (Characteristics & 0xa2000020) == 0, reason, Name);
    }
}
