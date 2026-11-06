namespace OS.Boot;
/// <summary>
/// Reports the result of constructing or operating on a typed address value.
/// The zero-valued default is intentionally not success.
/// </summary>

public enum AddressValueStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    InvalidAddress = 2,
    ZeroAddress = 3,
    ZeroLength = 4,
    InvalidAlignment = 5,
    MisalignedAddress = 6,
    AddressOverflow = 7
}
