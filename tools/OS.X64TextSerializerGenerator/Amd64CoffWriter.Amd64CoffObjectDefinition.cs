using System.Buffers.Binary;

namespace OS.X64TextSerializerGenerator;
public readonly record struct Amd64CoffObjectDefinition(ushort Machine, uint TimeDateStamp, CoffSectionDefinition[]? Sections, CoffSymbolDefinition[]? Symbols);
