using System.Buffers.Binary;

namespace OS.X64TextSerializerGenerator;

public readonly record struct CoffRelocationDefinition(uint Offset, uint SymbolIndex, ushort Type);
