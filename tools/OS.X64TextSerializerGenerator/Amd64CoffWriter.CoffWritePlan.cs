using System.Buffers.Binary;

namespace OS.X64TextSerializerGenerator;
public static partial class Amd64CoffWriter
{
    private sealed record CoffWritePlan(int TotalSize, uint StringTableSize, uint SymbolTablePointer, CoffSectionLayout[] SectionLayouts, IReadOnlyDictionary<string, uint> LongNameOffsets, IReadOnlyList<string> LongNames);
}
