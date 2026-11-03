namespace OS.Kernel.Boot;

public static unsafe partial class X64BootIdentityMappingPlan
{
    private struct PlannerState
    {
        public PlannerState(ulong sourceCount, ulong sourceCapacity, ulong destinationCapacity, ulong availableTablePageCapacity, ulong requiredPurposeMask)
        {
            SourceCount = sourceCount;
            ValidatedSourceCount = 0;
            SourceCapacity = sourceCapacity;
            DestinationCapacity = destinationCapacity;
            RequiredDestinationCapacity = 0;
            OutputRangeCount = 0;
            AvailableTablePageCapacity = availableTablePageCapacity;
            RequiredPurposeMask = requiredPurposeMask;
            ObservedPurposeMask = 0;
            MissingPurposeMask = requiredPurposeMask;
            GuardSourceIndex = X64BootIdentityMappingPlanResult.NoIndex;
            UnionPageCount = 0;
            UnionByteLength = 0;
            Pml4PageCount = 0;
            PdptPageCount = 0;
            PdPageCount = 0;
            PtPageCount = 0;
            TotalTablePageCount = 0;
            TotalTableByteLength = 0;
        }

        public ulong SourceCount;
        public ulong ValidatedSourceCount;
        public ulong SourceCapacity;
        public ulong DestinationCapacity;
        public ulong RequiredDestinationCapacity;
        public ulong OutputRangeCount;
        public ulong AvailableTablePageCapacity;
        public ulong RequiredPurposeMask;
        public ulong ObservedPurposeMask;
        public ulong MissingPurposeMask;
        public ulong GuardSourceIndex;
        public ulong UnionPageCount;
        public ulong UnionByteLength;
        public ulong Pml4PageCount;
        public ulong PdptPageCount;
        public ulong PdPageCount;
        public ulong PtPageCount;
        public ulong TotalTablePageCount;
        public ulong TotalTableByteLength;
    }
}
