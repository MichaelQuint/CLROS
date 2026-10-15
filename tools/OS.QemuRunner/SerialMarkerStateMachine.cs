using System.Text;

namespace OS.Tools.QemuRunner;

public sealed class SerialMarkerStateMachine
{
    public const int DefaultLineCapacity = 4096;
    public const long DefaultByteCapacity = 16L * 1024L * 1024L;
    public const int MaximumMarkerCount = 512;
    public const int MaximumMarkerLength = 512;
    public const int MaximumLineCapacity = 1024 * 1024;
    public const string DefaultSuccessTerminalMarker = "OS_SMOKE_PASS";
    public const string DefaultFailureTerminalMarker = "OS_SMOKE_FAIL";

    private readonly string[] _requiredMarkers;
    private readonly string[] _forbiddenMarkers;
    private readonly string[] _orderedMarkers;
    private readonly string[] _uniqueMarkers;
    private readonly bool[] _requiredSeen;
    private readonly bool[] _forbiddenSeen;
    private readonly bool[] _outOfOrderSeen;
    private readonly bool[] _uniqueSeen;
    private readonly bool[] _duplicateUniqueSeen;
    private readonly string _successTerminalMarker;
    private readonly string _failureTerminalMarker;
    private readonly byte[] _lineBuffer;
    private readonly long _byteCapacity;
    private readonly SerialMarkerByteNoisePolicy _byteNoisePolicy;

    private int _lineLength;
    private int _lineByteCount;
    private bool _lineHasInvalidBytes;
    private bool _discardingInvalidLine;
    private bool _pendingCarriageReturn;
    private bool _hasFeedFailure;
    private bool _finalized;
    private bool _partialMarkerAtEndOfStream;
    private SerialMarkerFeedStatus _feedFailureStatus;
    private SerialMarkerCompletionResult _completionResult;
    private long _totalBytes;
    private long _linesProcessed;
    private long _invalidByteCount;
    private long _invalidNoiseLineCount;
    private int _requiredSeenCount;
    private int _forbiddenOccurrenceCount;
    private int _orderedNextIndex;
    private int _outOfOrderOccurrenceCount;
    private int _uniqueSeenCount;
    private int _duplicateUniqueOccurrenceCount;
    private int _successTerminalCount;
    private int _failureTerminalCount;

    public SerialMarkerStateMachine(
        IReadOnlyList<string> requiredMarkers,
        IReadOnlyList<string> forbiddenMarkers,
        string successTerminalMarker = DefaultSuccessTerminalMarker,
        string failureTerminalMarker = DefaultFailureTerminalMarker,
        int lineCapacity = DefaultLineCapacity,
        long byteCapacity = DefaultByteCapacity,
        SerialMarkerByteNoisePolicy byteNoisePolicy =
            SerialMarkerByteNoisePolicy.IgnoreInvalidLines,
        IReadOnlyList<string>? orderedMarkers = null,
        IReadOnlyList<string>? uniqueMarkers = null)
    {
        ArgumentNullException.ThrowIfNull(requiredMarkers);
        ArgumentNullException.ThrowIfNull(forbiddenMarkers);

        if (lineCapacity is <= 0 or > MaximumLineCapacity)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lineCapacity),
                "Line capacity must be positive and no greater than " + MaximumLineCapacity + ".");
        }

        if (byteCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(byteCapacity),
                "Byte capacity must be positive.");
        }

        if (!Enum.IsDefined(byteNoisePolicy))
        {
            throw new ArgumentOutOfRangeException(nameof(byteNoisePolicy));
        }

        string[] required = CopyAndValidateMarkers(
            requiredMarkers,
            nameof(requiredMarkers),
            lineCapacity);
        _orderedMarkers = CopyAndValidateMarkers(
            orderedMarkers ?? [],
            nameof(orderedMarkers),
            lineCapacity);
        _uniqueMarkers = CopyAndValidateMarkers(
            uniqueMarkers ?? [],
            nameof(uniqueMarkers),
            lineCapacity);
        _requiredMarkers = MergeRequiredMarkers(
            required,
            _orderedMarkers,
            _uniqueMarkers);
        _forbiddenMarkers = CopyAndValidateMarkers(
            forbiddenMarkers,
            nameof(forbiddenMarkers),
            lineCapacity);
        RejectOverlappingMarkerSets(_requiredMarkers, _forbiddenMarkers);

        _successTerminalMarker = NormalizeMarker(
            successTerminalMarker,
            nameof(successTerminalMarker),
            lineCapacity);
        _failureTerminalMarker = NormalizeMarker(
            failureTerminalMarker,
            nameof(failureTerminalMarker),
            lineCapacity);
        if (string.Equals(
            _successTerminalMarker,
            _failureTerminalMarker,
            StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Success and failure terminal markers must be distinct.",
                nameof(failureTerminalMarker));
        }

        _requiredSeen = new bool[_requiredMarkers.Length];
        _forbiddenSeen = new bool[_forbiddenMarkers.Length];
        _outOfOrderSeen = new bool[_orderedMarkers.Length];
        _uniqueSeen = new bool[_uniqueMarkers.Length];
        _duplicateUniqueSeen = new bool[_uniqueMarkers.Length];
        _lineBuffer = new byte[lineCapacity];
        _byteCapacity = byteCapacity;
        _byteNoisePolicy = byteNoisePolicy;
    }

    public long TotalBytes => _totalBytes;

    public long LinesProcessed => _linesProcessed;

    public long InvalidByteCount => _invalidByteCount;

    public long InvalidNoiseLineCount => _invalidNoiseLineCount;

    public int RequiredSeenCount => _requiredSeenCount;

    public int RequiredMarkerCount => _requiredMarkers.Length;

    public int ForbiddenOccurrenceCount => _forbiddenOccurrenceCount;

    public int OrderedMarkerCount => _orderedMarkers.Length;

    public int OrderedSeenCount => _orderedNextIndex;

    public int OutOfOrderOccurrenceCount => _outOfOrderOccurrenceCount;

    public int UniqueMarkerCount => _uniqueMarkers.Length;

    public int UniqueSeenCount => _uniqueSeenCount;

    public int DuplicateUniqueOccurrenceCount =>
        _duplicateUniqueOccurrenceCount;

    public int SuccessTerminalCount => _successTerminalCount;

    public int FailureTerminalCount => _failureTerminalCount;

    public bool IsFinalized => _finalized;

    /// <summary>Queries an exact configured marker after a complete serial line was received.</summary>
    public bool HasSeenRequiredMarker(string marker)
    {
        int index = Array.IndexOf(_requiredMarkers, marker);
        return index >= 0 && _requiredSeen[index];
    }

    public SerialMarkerFeedResult Feed(ReadOnlySpan<byte> chunk)
    {
        if (_finalized)
        {
            return SerialMarkerFeedResult.Create(
                SerialMarkerFeedStatus.AlreadyFinalized,
                bytesConsumed: 0,
                _totalBytes);
        }

        if (_hasFeedFailure)
        {
            return SerialMarkerFeedResult.Create(
                _feedFailureStatus,
                bytesConsumed: 0,
                _totalBytes);
        }

        if (_totalBytes > _byteCapacity - chunk.Length)
        {
            _hasFeedFailure = true;
            _feedFailureStatus = SerialMarkerFeedStatus.TotalCapacityExceeded;
            return SerialMarkerFeedResult.Create(
                _feedFailureStatus,
                bytesConsumed: 0,
                _totalBytes);
        }

        for (int index = 0; index < chunk.Length; index++)
        {
            byte value = chunk[index];
            _totalBytes++;

            if (_pendingCarriageReturn)
            {
                _pendingCarriageReturn = false;
                if (value == (byte)'\n')
                {
                    ProcessBufferedLine();
                    continue;
                }

                if (!AcceptContentByte((byte)'\r'))
                {
                    return CreateFailedFeedResult(index + 1);
                }
            }

            if (value == (byte)'\r')
            {
                _pendingCarriageReturn = true;
                continue;
            }

            if (value == (byte)'\n')
            {
                ProcessBufferedLine();
                continue;
            }

            if (!AcceptContentByte(value))
            {
                return CreateFailedFeedResult(index + 1);
            }
        }

        return SerialMarkerFeedResult.Create(
            SerialMarkerFeedStatus.Accepted,
            chunk.Length,
            _totalBytes);
    }

    public SerialMarkerCompletionResult Complete(bool timedOut = false)
    {
        if (_finalized)
        {
            return _completionResult;
        }

        _finalized = true;

        if (!_hasFeedFailure)
        {
            FinalizePendingLine();
        }

        string[] missingMarkers = FindMissingMarkers();
        string[] forbiddenMarkerHits = FindForbiddenMarkerHits();
        string[] outOfOrderMarkerHits = FindMarkerHits(
            _orderedMarkers,
            _outOfOrderSeen);
        string[] duplicateUniqueMarkerHits = FindMarkerHits(
            _uniqueMarkers,
            _duplicateUniqueSeen);
        SerialMarkerTerminalState terminalState = GetTerminalState();
        SerialMarkerCompletionStatus status = SelectCompletionStatus(
            timedOut,
            missingMarkers.Length,
            forbiddenMarkerHits.Length,
            outOfOrderMarkerHits.Length,
            duplicateUniqueMarkerHits.Length,
            terminalState);

        _completionResult = SerialMarkerCompletionResult.Create(
            status,
            terminalState,
            missingMarkers,
            forbiddenMarkerHits,
            outOfOrderMarkerHits,
            duplicateUniqueMarkerHits,
            _totalBytes,
            _linesProcessed,
            _invalidByteCount,
            _invalidNoiseLineCount,
            _requiredSeenCount,
            _forbiddenOccurrenceCount,
            _orderedNextIndex,
            _outOfOrderOccurrenceCount,
            _uniqueSeenCount,
            _duplicateUniqueOccurrenceCount,
            _successTerminalCount,
            _failureTerminalCount,
            _partialMarkerAtEndOfStream);
        return _completionResult;
    }

    private bool AcceptContentByte(byte value)
    {
        if (_discardingInvalidLine)
        {
            if (!IsMarkerTextByte(value))
            {
                _invalidByteCount++;
            }

            return true;
        }

        if (_lineByteCount == _lineBuffer.Length)
        {
            if (_byteNoisePolicy == SerialMarkerByteNoisePolicy.IgnoreInvalidLines &&
                (_lineHasInvalidBytes || !IsMarkerTextByte(value)))
            {
                _lineHasInvalidBytes = true;
                _discardingInvalidLine = true;
                if (!IsMarkerTextByte(value))
                {
                    _invalidByteCount++;
                }

                return true;
            }

            _hasFeedFailure = true;
            _feedFailureStatus = SerialMarkerFeedStatus.LineCapacityExceeded;
            return false;
        }

        _lineByteCount++;
        if (IsMarkerTextByte(value))
        {
            _lineBuffer[_lineLength++] = value;
            return true;
        }

        _invalidByteCount++;
        _lineHasInvalidBytes = true;
        if (_byteNoisePolicy == SerialMarkerByteNoisePolicy.RejectInvalidBytes)
        {
            _hasFeedFailure = true;
            _feedFailureStatus = SerialMarkerFeedStatus.InvalidByteRejected;
            return false;
        }

        return true;
    }

    private SerialMarkerFeedResult CreateFailedFeedResult(int bytesConsumed)
    {
        return SerialMarkerFeedResult.Create(
            _feedFailureStatus,
            bytesConsumed,
            _totalBytes);
    }

    private void FinalizePendingLine()
    {
        if (_pendingCarriageReturn)
        {
            _pendingCarriageReturn = false;
            ProcessOrClassifyEndOfStreamLine();
            return;
        }

        if (_lineByteCount != 0)
        {
            ProcessOrClassifyEndOfStreamLine();
        }
    }

    private void ProcessOrClassifyEndOfStreamLine()
    {
        if (!_lineHasInvalidBytes)
        {
            string line = GetTrimmedBufferedLine();
            if (line.Length != 0 && IsStrictMarkerPrefix(line))
            {
                _partialMarkerAtEndOfStream = true;
                ResetLine();
                return;
            }
        }

        ProcessBufferedLine();
    }

    private void ProcessBufferedLine()
    {
        _linesProcessed++;

        if (_lineHasInvalidBytes)
        {
            _invalidNoiseLineCount++;
            ResetLine();
            return;
        }

        string line = GetTrimmedBufferedLine();
        if (line.Length == 0)
        {
            ResetLine();
            return;
        }

        for (int index = 0; index < _requiredMarkers.Length; index++)
        {
            if (!_requiredSeen[index] && LineMatchesMarker(line, _requiredMarkers[index]))
            {
                _requiredSeen[index] = true;
                _requiredSeenCount++;
            }
        }

        for (int index = 0; index < _forbiddenMarkers.Length; index++)
        {
            if (!LineMatchesMarker(line, _forbiddenMarkers[index]))
            {
                continue;
            }

            _forbiddenOccurrenceCount++;
            _forbiddenSeen[index] = true;
        }

        ProcessOrderedMarkers(line);
        ProcessUniqueMarkers(line);

        if (LineMatchesMarker(line, _successTerminalMarker))
        {
            _successTerminalCount++;
        }

        if (LineMatchesMarker(line, _failureTerminalMarker))
        {
            _failureTerminalCount++;
        }

        ResetLine();
    }

    private void ProcessOrderedMarkers(string line)
    {
        int expectedIndex = _orderedNextIndex;
        bool advanced = false;
        for (int index = 0; index < _orderedMarkers.Length; index++)
        {
            if (!LineMatchesMarker(line, _orderedMarkers[index]))
            {
                continue;
            }

            if (index > expectedIndex)
            {
                _outOfOrderOccurrenceCount++;
                _outOfOrderSeen[index] = true;
                continue;
            }

            if (index == expectedIndex && !advanced)
            {
                _orderedNextIndex++;
                advanced = true;
            }
        }
    }

    private void ProcessUniqueMarkers(string line)
    {
        for (int index = 0; index < _uniqueMarkers.Length; index++)
        {
            if (!LineMatchesMarker(line, _uniqueMarkers[index]))
            {
                continue;
            }

            if (!_uniqueSeen[index])
            {
                _uniqueSeen[index] = true;
                _uniqueSeenCount++;
                continue;
            }

            _duplicateUniqueOccurrenceCount++;
            _duplicateUniqueSeen[index] = true;
        }
    }

    private string GetTrimmedBufferedLine()
    {
        int start = 0;
        int end = _lineLength;
        while (start < end && IsTrimByte(_lineBuffer[start]))
        {
            start++;
        }

        while (end > start && IsTrimByte(_lineBuffer[end - 1]))
        {
            end--;
        }

        return end == start
            ? string.Empty
            : Encoding.ASCII.GetString(_lineBuffer, start, end - start);
    }

    private bool IsStrictMarkerPrefix(string line)
    {
        for (int index = 0; index < _requiredMarkers.Length; index++)
        {
            if (IsStrictPrefix(line, _requiredMarkers[index]))
            {
                return true;
            }
        }

        for (int index = 0; index < _forbiddenMarkers.Length; index++)
        {
            if (IsStrictPrefix(line, _forbiddenMarkers[index]))
            {
                return true;
            }
        }

        return IsStrictPrefix(line, _successTerminalMarker)
            || IsStrictPrefix(line, _failureTerminalMarker);
    }

    private SerialMarkerCompletionStatus SelectCompletionStatus(
        bool timedOut,
        int missingMarkerCount,
        int forbiddenMarkerHitCount,
        int outOfOrderMarkerHitCount,
        int duplicateUniqueMarkerHitCount,
        SerialMarkerTerminalState terminalState)
    {
        if (_hasFeedFailure)
        {
            return _feedFailureStatus == SerialMarkerFeedStatus.InvalidByteRejected
                ? SerialMarkerCompletionStatus.InvalidByteSequence
                : SerialMarkerCompletionStatus.CapacityExceeded;
        }

        if (forbiddenMarkerHitCount != 0)
        {
            return SerialMarkerCompletionStatus.ForbiddenMarker;
        }

        if (terminalState == SerialMarkerTerminalState.Conflicting)
        {
            return SerialMarkerCompletionStatus.ConflictingTerminalMarkers;
        }

        if (_successTerminalCount > 1 || _failureTerminalCount > 1)
        {
            return SerialMarkerCompletionStatus.DuplicateTerminalMarker;
        }

        if (terminalState == SerialMarkerTerminalState.Failure)
        {
            return SerialMarkerCompletionStatus.FailureTerminal;
        }

        if (outOfOrderMarkerHitCount != 0)
        {
            return SerialMarkerCompletionStatus.OutOfOrderMarker;
        }

        if (duplicateUniqueMarkerHitCount != 0)
        {
            return SerialMarkerCompletionStatus.DuplicateUniqueMarker;
        }

        if (timedOut)
        {
            return SerialMarkerCompletionStatus.Timeout;
        }

        if (_partialMarkerAtEndOfStream)
        {
            return SerialMarkerCompletionStatus.PartialMarkerAtEndOfStream;
        }

        if (missingMarkerCount != 0)
        {
            return SerialMarkerCompletionStatus.MissingRequiredMarker;
        }

        if (terminalState != SerialMarkerTerminalState.Success)
        {
            return SerialMarkerCompletionStatus.MissingSuccessTerminal;
        }

        return SerialMarkerCompletionStatus.Passed;
    }

    private SerialMarkerTerminalState GetTerminalState()
    {
        if (_successTerminalCount != 0 && _failureTerminalCount != 0)
        {
            return SerialMarkerTerminalState.Conflicting;
        }

        if (_successTerminalCount != 0)
        {
            return SerialMarkerTerminalState.Success;
        }

        return _failureTerminalCount != 0
            ? SerialMarkerTerminalState.Failure
            : SerialMarkerTerminalState.None;
    }

    private string[] FindMissingMarkers()
    {
        int count = _requiredMarkers.Length - _requiredSeenCount;
        if (count == 0)
        {
            return [];
        }

        string[] missing = new string[count];
        int writeIndex = 0;
        for (int index = 0; index < _requiredMarkers.Length; index++)
        {
            if (!_requiredSeen[index])
            {
                missing[writeIndex++] = _requiredMarkers[index];
            }
        }

        return missing;
    }

    private string[] FindForbiddenMarkerHits()
    {
        int count = 0;
        for (int index = 0; index < _forbiddenSeen.Length; index++)
        {
            if (_forbiddenSeen[index])
            {
                count++;
            }
        }

        if (count == 0)
        {
            return [];
        }

        string[] hits = new string[count];
        int writeIndex = 0;
        for (int index = 0; index < _forbiddenSeen.Length; index++)
        {
            if (_forbiddenSeen[index])
            {
                hits[writeIndex++] = _forbiddenMarkers[index];
            }
        }

        return hits;
    }

    private static string[] FindMarkerHits(
        IReadOnlyList<string> markers,
        IReadOnlyList<bool> seen)
    {
        int count = 0;
        for (int index = 0; index < seen.Count; index++)
        {
            if (seen[index])
            {
                count++;
            }
        }

        if (count == 0)
        {
            return [];
        }

        string[] hits = new string[count];
        int writeIndex = 0;
        for (int index = 0; index < seen.Count; index++)
        {
            if (seen[index])
            {
                hits[writeIndex++] = markers[index];
            }
        }

        return hits;
    }

    private void ResetLine()
    {
        _lineLength = 0;
        _lineByteCount = 0;
        _lineHasInvalidBytes = false;
        _discardingInvalidLine = false;
    }

    private static string[] CopyAndValidateMarkers(
        IReadOnlyList<string> markers,
        string parameterName,
        int lineCapacity)
    {
        if (markers.Count > MaximumMarkerCount)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Marker count cannot exceed " + MaximumMarkerCount + ".");
        }

        if (markers.Count == 0)
        {
            return [];
        }

        string[] result = new string[markers.Count];
        for (int index = 0; index < markers.Count; index++)
        {
            string marker = NormalizeMarker(markers[index], parameterName, lineCapacity);
            for (int previous = 0; previous < index; previous++)
            {
                if (string.Equals(result[previous], marker, StringComparison.Ordinal))
                {
                    throw new ArgumentException(
                        "Marker entries must be unique.",
                        parameterName);
                }
            }

            result[index] = marker;
        }

        return result;
    }

    private static string[] MergeRequiredMarkers(
        IReadOnlyList<string> requiredMarkers,
        IReadOnlyList<string> orderedMarkers,
        IReadOnlyList<string> uniqueMarkers)
    {
        List<string> merged = new List<string>(requiredMarkers.Count);
        AddDistinctMarkers(merged, requiredMarkers);
        AddDistinctMarkers(merged, orderedMarkers);
        AddDistinctMarkers(merged, uniqueMarkers);
        if (merged.Count > MaximumMarkerCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(requiredMarkers),
                "Combined required marker count cannot exceed "
                    + MaximumMarkerCount
                    + ".");
        }

        return merged.ToArray();
    }

    private static void AddDistinctMarkers(
        List<string> destination,
        IReadOnlyList<string> source)
    {
        for (int index = 0; index < source.Count; index++)
        {
            if (!destination.Contains(source[index], StringComparer.Ordinal))
            {
                destination.Add(source[index]);
            }
        }
    }

    private static string NormalizeMarker(
        string? marker,
        string parameterName,
        int lineCapacity)
    {
        if (string.IsNullOrWhiteSpace(marker))
        {
            throw new ArgumentException("Marker entries cannot be empty.", parameterName);
        }

        string normalized = marker.Trim();
        if (normalized.Length > MaximumMarkerLength || normalized.Length > lineCapacity)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Marker length exceeds the configured capacity.");
        }

        for (int index = 0; index < normalized.Length; index++)
        {
            char character = normalized[index];
            if (character is < ' ' or > '~')
            {
                throw new ArgumentException(
                    "Markers must contain printable ASCII characters only.",
                    parameterName);
            }
        }

        return normalized;
    }

    private static void RejectOverlappingMarkerSets(
        IReadOnlyList<string> requiredMarkers,
        IReadOnlyList<string> forbiddenMarkers)
    {
        for (int requiredIndex = 0; requiredIndex < requiredMarkers.Count; requiredIndex++)
        {
            for (int forbiddenIndex = 0;
                forbiddenIndex < forbiddenMarkers.Count;
                forbiddenIndex++)
            {
                if (string.Equals(
                    requiredMarkers[requiredIndex],
                    forbiddenMarkers[forbiddenIndex],
                    StringComparison.Ordinal))
                {
                    throw new ArgumentException(
                        "A marker cannot be both required and forbidden.",
                        nameof(forbiddenMarkers));
                }
            }
        }
    }

    private static bool IsMarkerTextByte(byte value)
    {
        return value == (byte)'\t' || value is >= 0x20 and <= 0x7e;
    }

    private static bool IsTrimByte(byte value)
    {
        return value is (byte)' ' or (byte)'\t';
    }

    private static bool LineMatchesMarker(string line, string marker)
    {
        return string.Equals(line, marker, StringComparison.Ordinal)
            || line.StartsWith(marker + " ", StringComparison.Ordinal);
    }

    private static bool IsStrictPrefix(string candidate, string marker)
    {
        return candidate.Length < marker.Length
            && marker.StartsWith(candidate, StringComparison.Ordinal);
    }
}

public enum SerialMarkerByteNoisePolicy
{
    IgnoreInvalidLines = 0,
    RejectInvalidBytes = 1
}

public enum SerialMarkerFeedStatus
{
    Uninitialized = 0,
    Accepted = 1,
    AlreadyFinalized = 2,
    LineCapacityExceeded = 3,
    TotalCapacityExceeded = 4,
    InvalidByteRejected = 5
}

public enum SerialMarkerCompletionStatus
{
    Uninitialized = 0,
    Passed = 1,
    CapacityExceeded = 2,
    InvalidByteSequence = 3,
    ForbiddenMarker = 4,
    ConflictingTerminalMarkers = 5,
    DuplicateTerminalMarker = 6,
    FailureTerminal = 7,
    Timeout = 8,
    PartialMarkerAtEndOfStream = 9,
    MissingRequiredMarker = 10,
    MissingSuccessTerminal = 11,
    OutOfOrderMarker = 12,
    DuplicateUniqueMarker = 13
}

public enum SerialMarkerTerminalState
{
    None = 0,
    Success = 1,
    Failure = 2,
    Conflicting = 3
}

public readonly struct SerialMarkerFeedResult
{
    private readonly bool _isInitialized;

    private SerialMarkerFeedResult(
        SerialMarkerFeedStatus status,
        int bytesConsumed,
        long totalBytes)
    {
        _isInitialized = true;
        Status = status;
        BytesConsumed = bytesConsumed;
        TotalBytes = totalBytes;
    }

    public SerialMarkerFeedStatus Status { get; }

    public int BytesConsumed { get; }

    public long TotalBytes { get; }

    public bool Succeeded =>
        _isInitialized &&
        Status == SerialMarkerFeedStatus.Accepted &&
        BytesConsumed >= 0 &&
        TotalBytes >= BytesConsumed;

    internal static SerialMarkerFeedResult Create(
        SerialMarkerFeedStatus status,
        int bytesConsumed,
        long totalBytes)
    {
        return new SerialMarkerFeedResult(status, bytesConsumed, totalBytes);
    }
}

public readonly struct SerialMarkerCompletionResult
{
    private readonly bool _isInitialized;

    private SerialMarkerCompletionResult(
        SerialMarkerCompletionStatus status,
        SerialMarkerTerminalState terminalState,
        string[] missingRequiredMarkers,
        string[] forbiddenMarkerHits,
        string[] outOfOrderMarkerHits,
        string[] duplicateUniqueMarkerHits,
        long totalBytes,
        long linesProcessed,
        long invalidByteCount,
        long invalidNoiseLineCount,
        int requiredSeenCount,
        int forbiddenOccurrenceCount,
        int orderedSeenCount,
        int outOfOrderOccurrenceCount,
        int uniqueSeenCount,
        int duplicateUniqueOccurrenceCount,
        int successTerminalCount,
        int failureTerminalCount,
        bool partialMarkerAtEndOfStream)
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

    public bool Passed =>
        _isInitialized &&
        Status == SerialMarkerCompletionStatus.Passed &&
        TerminalState == SerialMarkerTerminalState.Success &&
        SuccessTerminalCount == 1 &&
        FailureTerminalCount == 0 &&
        MissingRequiredMarkers is { Count: 0 } &&
        ForbiddenMarkerHits is { Count: 0 } &&
        OutOfOrderMarkerHits is { Count: 0 } &&
        DuplicateUniqueMarkerHits is { Count: 0 };

    internal static SerialMarkerCompletionResult Create(
        SerialMarkerCompletionStatus status,
        SerialMarkerTerminalState terminalState,
        string[] missingRequiredMarkers,
        string[] forbiddenMarkerHits,
        string[] outOfOrderMarkerHits,
        string[] duplicateUniqueMarkerHits,
        long totalBytes,
        long linesProcessed,
        long invalidByteCount,
        long invalidNoiseLineCount,
        int requiredSeenCount,
        int forbiddenOccurrenceCount,
        int orderedSeenCount,
        int outOfOrderOccurrenceCount,
        int uniqueSeenCount,
        int duplicateUniqueOccurrenceCount,
        int successTerminalCount,
        int failureTerminalCount,
        bool partialMarkerAtEndOfStream)
    {
        return new SerialMarkerCompletionResult(
            status,
            terminalState,
            missingRequiredMarkers,
            forbiddenMarkerHits,
            outOfOrderMarkerHits,
            duplicateUniqueMarkerHits,
            totalBytes,
            linesProcessed,
            invalidByteCount,
            invalidNoiseLineCount,
            requiredSeenCount,
            forbiddenOccurrenceCount,
            orderedSeenCount,
            outOfOrderOccurrenceCount,
            uniqueSeenCount,
            duplicateUniqueOccurrenceCount,
            successTerminalCount,
            failureTerminalCount,
            partialMarkerAtEndOfStream);
    }
}
