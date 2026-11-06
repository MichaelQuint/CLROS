namespace OS.Boot;

public static class BootMemoryMapAcceptance
{
    public static BootMemoryMapAcceptanceResult Validate(EfiMemoryMapSummary summary)
    {
        if (summary.DescriptorCount == 0 && summary.TotalPages == 0 && summary.TotalBytes == 0 && summary.ConventionalPages == 0 && summary.LowestPhysicalAddress == 0 && summary.HighestPhysicalEndExclusive == 0)
        {
            return BootMemoryMapAcceptanceResult.Fail(BootMemoryMapAcceptanceStatus.MissingSummary);
        }

        if (summary.DescriptorCount == 0)
        {
            return BootMemoryMapAcceptanceResult.Fail(BootMemoryMapAcceptanceStatus.ZeroDescriptors);
        }

        if (summary.TotalPages == 0)
        {
            return BootMemoryMapAcceptanceResult.Fail(BootMemoryMapAcceptanceStatus.ZeroPages);
        }

        if (summary.TotalBytes == 0)
        {
            return BootMemoryMapAcceptanceResult.Fail(BootMemoryMapAcceptanceStatus.ZeroBytes);
        }

        if (summary.HighestPhysicalEndExclusive <= summary.LowestPhysicalAddress)
        {
            return BootMemoryMapAcceptanceResult.Fail(BootMemoryMapAcceptanceStatus.InvalidPhysicalRange);
        }

        if (summary.ConventionalPages == 0)
        {
            return BootMemoryMapAcceptanceResult.Fail(BootMemoryMapAcceptanceStatus.NoConventionalMemory);
        }

        return BootMemoryMapAcceptanceResult.Success();
    }
}
