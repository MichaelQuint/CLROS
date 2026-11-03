using System.Buffers.Binary;

namespace OS.X64TextSerializerGenerator;
public enum CoffWriteError
{
    None = 0,
    InvalidConfiguration,
    UnsupportedMachine,
    NonDeterministicTimestamp,
    InvalidSectionCount,
    InvalidSymbolCount,
    InvalidName,
    EmptySection,
    InvalidSectionCharacteristics,
    DuplicateSection,
    InvalidSymbol,
    DuplicateSymbol,
    InvalidRelocation,
    CapacityExceeded,
    ArithmeticOverflow,
    InternalInvariant,
}
