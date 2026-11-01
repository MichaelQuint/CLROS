using OS.Boot;

namespace OS.Kernel.Memory;
public readonly partial struct MemoryMapInfoResult
{
    private readonly struct RegionSummaryResult
    {
        private readonly string? _message;
        private RegionSummaryResult(bool succeeded, MemoryMapInfoStatus status, int invalidDescriptorIndex, ulong totalPages, ulong totalBytes, ulong conventionalMemoryPages, ulong conventionalMemoryBytes, string message)
        {
            Succeeded = succeeded;
            Status = status;
            InvalidDescriptorIndex = invalidDescriptorIndex;
            TotalPages = totalPages;
            TotalBytes = totalBytes;
            ConventionalMemoryPages = conventionalMemoryPages;
            ConventionalMemoryBytes = conventionalMemoryBytes;
            _message = message;
        }

        public bool Succeeded { get; }
        public MemoryMapInfoStatus Status { get; }
        public int InvalidDescriptorIndex { get; }
        public ulong TotalPages { get; }
        public ulong TotalBytes { get; }
        public ulong ConventionalMemoryPages { get; }
        public ulong ConventionalMemoryBytes { get; }
        public string Message => _message ?? string.Empty;

        public static RegionSummaryResult Valid(ulong totalPages, ulong totalBytes, ulong conventionalMemoryPages, ulong conventionalMemoryBytes)
        {
            return new RegionSummaryResult(true, MemoryMapInfoStatus.Success, invalidDescriptorIndex: -1, totalPages, totalBytes, conventionalMemoryPages, conventionalMemoryBytes, string.Empty);
        }

        public static RegionSummaryResult Invalid(MemoryMapInfoStatus status, int invalidDescriptorIndex, string message, ulong totalPages, ulong totalBytes, ulong conventionalMemoryPages, ulong conventionalMemoryBytes)
        {
            return new RegionSummaryResult(false, status, invalidDescriptorIndex, totalPages, totalBytes, conventionalMemoryPages, conventionalMemoryBytes, message);
        }
    }
}
