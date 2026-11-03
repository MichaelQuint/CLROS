using System.Text;

namespace OS.Tools.QemuRunner;
public readonly struct SerialMarkerCompletionResult
{
    private readonly bool _isInitialized;
    private SerialMarkerCompletionResult(SerialMarkerCompletionStatus status, SerialMarkerTerminalState terminalState, string[] missingRequiredMarkers, string[] forbiddenMarkerHits, string[] outOfOrderMarkerHits, string[] duplicateUniqueMarkerHits, long totalBytes, long linesProcessed, long invalidByteCount, long invalidNoiseLineCount, int requiredSeenCount, int forbiddenOccurrenceCount, int orderedSeenCount, int outOfOrderOccurrenceCount, int uniqueSeenCount, int duplicateUniqueOccurrenceCount, int successTerminalCount, int failureTerminalCount, bool partialMarkerAtEndOfStream)
    {
        _isInitialized = true;
        Status = status;
        TerminalState = terminalState;
        MissingRequiredMarkers = missingRequiredMarkers;
        ForbiddenMarkerHits = forbiddenMarkerHits;
        OutOfOrderMarkerHits = outOfOrderMarkerHits;
        DuplicateUniqueMarkerHits = duplicateUniqueMarkerHits;
        TotalBytes = totalBytes;
        LinesProcessed = linesProcessed;
        InvalidByteCount = invalidByteCount;
        InvalidNoiseLineCount = invalidNoiseLineCount;
        RequiredSeenCount = requiredSeenCount;
        ForbiddenOccurrenceCount = forbiddenOccurrenceCount;
        OrderedSeenCount = orderedSeenCount;
        OutOfOrderOccurrenceCount = outOfOrderOccurrenceCount;
        UniqueSeenCount = uniqueSeenCount;
        DuplicateUniqueOccurrenceCount = duplicateUniqueOccurrenceCount;
        SuccessTerminalCount = successTerminalCount;
        FailureTerminalCount = failureTerminalCount;
        PartialMarkerAtEndOfStream = partialMarkerAtEndOfStream;
    }

    public SerialMarkerCompletionStatus Status { get; }
    public SerialMarkerTerminalState TerminalState { get; }
    public IReadOnlyList<string>? MissingRequiredMarkers { get; }
    public IReadOnlyList<string>? ForbiddenMarkerHits { get; }
    public IReadOnlyList<string>? OutOfOrderMarkerHits { get; }
    public IReadOnlyList<string>? DuplicateUniqueMarkerHits { get; }
    public long TotalBytes { get; }
    public long LinesProcessed { get; }
    public long InvalidByteCount { get; }
    public long InvalidNoiseLineCount { get; }
    public int RequiredSeenCount { get; }
    public int ForbiddenOccurrenceCount { get; }
    public int OrderedSeenCount { get; }
    public int OutOfOrderOccurrenceCount { get; }
    public int UniqueSeenCount { get; }
    public int DuplicateUniqueOccurrenceCount { get; }
    public int SuccessTerminalCount { get; }
    public int FailureTerminalCount { get; }
    public bool PartialMarkerAtEndOfStream { get; }
    public bool Passed => _isInitialized && Status == SerialMarkerCompletionStatus.Passed && TerminalState == SerialMarkerTerminalState.Success && SuccessTerminalCount == 1 && FailureTerminalCount == 0 && MissingRequiredMarkers is { Count: 0 } && ForbiddenMarkerHits is { Count: 0 } && OutOfOrderMarkerHits is { Count: 0 } && DuplicateUniqueMarkerHits is { Count: 0 };

    internal static SerialMarkerCompletionResult Create(SerialMarkerCompletionStatus status, SerialMarkerTerminalState terminalState, string[] missingRequiredMarkers, string[] forbiddenMarkerHits, string[] outOfOrderMarkerHits, string[] duplicateUniqueMarkerHits, long totalBytes, long linesProcessed, long invalidByteCount, long invalidNoiseLineCount, int requiredSeenCount, int forbiddenOccurrenceCount, int orderedSeenCount, int outOfOrderOccurrenceCount, int uniqueSeenCount, int duplicateUniqueOccurrenceCount, int successTerminalCount, int failureTerminalCount, bool partialMarkerAtEndOfStream)
    {
        return new SerialMarkerCompletionResult(status, terminalState, missingRequiredMarkers, forbiddenMarkerHits, outOfOrderMarkerHits, duplicateUniqueMarkerHits, totalBytes, linesProcessed, invalidByteCount, invalidNoiseLineCount, requiredSeenCount, forbiddenOccurrenceCount, orderedSeenCount, outOfOrderOccurrenceCount, uniqueSeenCount, duplicateUniqueOccurrenceCount, successTerminalCount, failureTerminalCount, partialMarkerAtEndOfStream);
    }
}
