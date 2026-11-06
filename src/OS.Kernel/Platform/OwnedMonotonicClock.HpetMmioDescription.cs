namespace OS.Kernel.Platform;
/// <summary>Numeric ACPI description only; the adapter must prove device ownership and UC mapping.</summary>

public readonly struct HpetMmioDescription
{
    public readonly ulong Address;
    public readonly uint HardwareId;
    internal HpetMmioDescription(ulong address, uint hardwareId)
    {
        Address = address;
        HardwareId = hardwareId;
    }
}
