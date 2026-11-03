using System.Buffers.Binary;

namespace OS.X64TextSerializerGenerator;
public readonly record struct CoffSectionDefinition(string? Name, byte[]? RawData, uint Characteristics, CoffRelocationDefinition[]? Relocations);
