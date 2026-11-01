namespace OS.Kernel.Memory;
public enum X64Cr3ProbeStorageProvenance : byte
{
    Uninitialized = 0,
    CallerAttestedOwnedLoaderData = 1
}
