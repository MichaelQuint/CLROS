using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

internal static partial class M01InstructionPublicationSerializerGeneratorTests
{
}

internal static partial class M01InstructionPublicationSerializerGeneratorTests
{
    private sealed record ParsedCoff(ushort Machine, ushort SectionCount, uint TimeDateStamp, uint SymbolTablePointer, uint SymbolCount, ushort OptionalHeaderSize, ushort Characteristics, IReadOnlyList<ParsedSection> Sections, IReadOnlyList<ParsedSymbol> Symbols, byte[] StringTable);
}
