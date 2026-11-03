namespace OS.Kernel.Diagnostics;

public enum GuardedIstStackArenaStatus : byte
{
    Success = 0,
    NullArenaBaseAddress = 1,
    NonCanonicalArenaBaseAddress = 2,
    UnalignedArenaBaseAddress = 3,
    InvalidArenaSize = 4,
    InvalidStackSize = 5,
    InvalidGuardSize = 6,
    NullOwnerToken = 7,
    StackMustBeWritable = 8,
    ProhibitedWritableExecutable = 9,
    SizeOverflow = 10,
    ArenaEndAddressWrap = 11,
    NonCanonicalArenaEndAddress = 12,
    ArenaExhausted = 13,
    InvalidReservedRange = 14,
    ReservedRangeAddressWrap = 15,
    ReservedRangeOverlap = 16
}
