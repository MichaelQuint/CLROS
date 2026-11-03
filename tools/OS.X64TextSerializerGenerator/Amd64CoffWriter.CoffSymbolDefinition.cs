using System.Buffers.Binary;

namespace OS.X64TextSerializerGenerator;
public readonly record struct CoffSymbolDefinition(string? Name, uint Value, short SectionNumber, ushort Type, byte StorageClass);
