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
    private sealed record ParsedSection(string Name, uint VirtualSize, uint VirtualAddress, uint RawSize, uint RawPointer, uint RelocationPointer, uint LineNumberPointer, ushort RelocationCount, ushort LineNumberCount, uint Characteristics, byte[] RawData, IReadOnlyList<ParsedRelocation> Relocations);
}
