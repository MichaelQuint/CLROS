using System.Buffers.Binary;

namespace OS.X64TextSerializerGenerator;

public static partial class Amd64CoffWriter
{
    private readonly record struct CoffSectionLayout(uint RawDataPointer, uint RelocationPointer);
}
