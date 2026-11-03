namespace OS.Tools.QemuRunner;
public sealed class SerialMarkerSummary
{
    private static readonly string[] EmptyMarkers = [];
    private static readonly AppExitMarkerResult[] EmptyAppExitResults = [];
    private SerialMarkerSummary(string[] requiredMarkers, string[] forbiddenMarkers, string[] orderedMarkers, string[] observedMarkerLines, string[] missingMarkers, string[] outOfOrderMarkers, string[] forbiddenMarkerHits, AppExitMarkerResult[] appExitResults, string[] malformedAppExitMarkers)
    {
        RequiredMarkers = requiredMarkers;
        ForbiddenMarkers = forbiddenMarkers;
        OrderedMarkers = orderedMarkers;
        ObservedMarkerLines = observedMarkerLines;
        MissingMarkers = missingMarkers;
        OutOfOrderMarkers = outOfOrderMarkers;
        ForbiddenMarkerHits = forbiddenMarkerHits;
        AppExitResults = appExitResults;
        MalformedAppExitMarkers = malformedAppExitMarkers;
    }

    public IReadOnlyList<string> RequiredMarkers { get; }
    public IReadOnlyList<string> ForbiddenMarkers { get; }
    public IReadOnlyList<string> OrderedMarkers { get; }
    public IReadOnlyList<string> ObservedMarkerLines { get; }
    public IReadOnlyList<string> MissingMarkers { get; }
    public IReadOnlyList<string> OutOfOrderMarkers { get; }
    public IReadOnlyList<string> ForbiddenMarkerHits { get; }
    public IReadOnlyList<AppExitMarkerResult> AppExitResults { get; }
    public IReadOnlyList<string> MalformedAppExitMarkers { get; }

    public bool HasAllRequiredMarkers
    {
        get
        {
            return MissingMarkers.Count == 0 && OutOfOrderMarkers.Count == 0;
        }
    }

    public bool HasForbiddenMarkers
    {
        get
        {
            return ForbiddenMarkerHits.Count != 0;
        }
    }

    public bool Passed
    {
        get
        {
            return HasAllRequiredMarkers && !HasForbiddenMarkers;
        }
    }

    public static SerialMarkerSummary Parse(string? serialText, IReadOnlyList<string> requiredMarkers, IReadOnlyList<string> forbiddenMarkers, IReadOnlyList<string>? orderedMarkers = null)
    {
        if (requiredMarkers is null)
        {
            throw new ArgumentNullException(nameof(requiredMarkers));
        }

        if (forbiddenMarkers is null)
        {
            throw new ArgumentNullException(nameof(forbiddenMarkers));
        }

        string serial = serialText ?? string.Empty;
        string[] required = CopyMarkers(requiredMarkers);
        string[] forbidden = CopyMarkers(forbiddenMarkers);
        string[] ordered = orderedMarkers is null ? EmptyMarkers : CopyMarkers(orderedMarkers);
        FindAppExitMarkers(serial, out AppExitMarkerResult[] appExitResults, out string[] malformedAppExitMarkers);
        return new SerialMarkerSummary(required, forbidden, ordered, FindObservedMarkerLines(serial), FindMissingMarkers(serial, required), FindOutOfOrderMarkers(serial, ordered), FindForbiddenMarkerHits(serial, forbidden), appExitResults, malformedAppExitMarkers);
    }

    private static string[] CopyMarkers(IReadOnlyList<string> markers)
    {
        if (markers.Count == 0)
        {
            return EmptyMarkers;
        }

        string[] copiedMarkers = new string[markers.Count];
        for (int index = 0; index < markers.Count; index++)
        {
            string marker = markers[index];
            if (string.IsNullOrWhiteSpace(marker))
            {
                throw new ArgumentException("Marker entries cannot be empty.", nameof(markers));
            }

            copiedMarkers[index] = marker.Trim();
        }

        return copiedMarkers;
    }

    private static string[] FindMissingMarkers(string serial, IReadOnlyList<string> markers)
    {
        if (markers.Count == 0)
        {
            return EmptyMarkers;
        }

        List<string> missingMarkers = new List<string>();
        foreach (string marker in markers)
        {
            if (!ContainsMarkerLine(serial, marker))
            {
                missingMarkers.Add(marker);
            }
        }

        return missingMarkers.Count == 0 ? EmptyMarkers : missingMarkers.ToArray();
    }

    private static string[] FindOutOfOrderMarkers(string serial, IReadOnlyList<string> markers)
    {
        if (markers.Count == 0)
        {
            return EmptyMarkers;
        }

        string[] lines = ReadTrimmedLines(serial);
        int searchIndex = 0;
        List<string> outOfOrderMarkers = new List<string>();
        foreach (string marker in markers)
        {
            bool found = false;
            for (; searchIndex < lines.Length; searchIndex++)
            {
                if (LineMatchesMarker(lines[searchIndex], marker))
                {
                    found = true;
                    searchIndex++;
                    break;
                }
            }

            if (!found)
            {
                outOfOrderMarkers.Add(marker);
            }
        }

        return outOfOrderMarkers.Count == 0 ? EmptyMarkers : outOfOrderMarkers.ToArray();
    }

    private static string[] FindForbiddenMarkerHits(string serial, IReadOnlyList<string> markers)
    {
        if (markers.Count == 0)
        {
            return EmptyMarkers;
        }

        List<string> hits = new List<string>();
        foreach (string marker in markers)
        {
            if (ContainsMarkerLine(serial, marker))
            {
                hits.Add(marker);
            }
        }

        return hits.Count == 0 ? EmptyMarkers : hits.ToArray();
    }

    private static bool ContainsMarkerLine(string serial, string marker)
    {
        using StringReader reader = new StringReader(serial);
        while (reader.ReadLine()is { } line)
        {
            if (LineMatchesMarker(line.Trim(), marker))
            {
                return true;
            }
        }

        return false;
    }

    private static bool LineMatchesMarker(string line, string marker)
    {
        return string.Equals(line, marker, StringComparison.Ordinal) || line.StartsWith(marker + " ", StringComparison.Ordinal);
    }

    private static string[] ReadTrimmedLines(string serial)
    {
        List<string> lines = new List<string>();
        using StringReader reader = new StringReader(serial);
        while (reader.ReadLine()is { } line)
        {
            string trimmed = line.Trim();
            if (trimmed.Length != 0)
            {
                lines.Add(trimmed);
            }
        }

        return lines.Count == 0 ? EmptyMarkers : lines.ToArray();
    }

    private static string[] FindObservedMarkerLines(string serial)
    {
        List<string> observedMarkers = new List<string>();
        using StringReader reader = new StringReader(serial);
        while (reader.ReadLine()is { } line)
        {
            string trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            if (IsStandardObservedMarkerLine(trimmed))
            {
                observedMarkers.Add(trimmed);
            }
        }

        return observedMarkers.Count == 0 ? EmptyMarkers : observedMarkers.ToArray();
    }

    /// <summary>Classify one caller-trimmed host serial line for summary retention.
    /// Optional M16 witnesses, faults and IRQ rows remain visible without becoming
    /// required, ordered or unique markers. Recording a line does not validate it.</summary>

    public static bool IsStandardObservedMarkerLine(string line)
    {
        return line.StartsWith("TEST_", StringComparison.Ordinal) || line.StartsWith("BOOT_", StringComparison.Ordinal) || line.StartsWith("OS_", StringComparison.Ordinal) || line.StartsWith("UEFI_", StringComparison.Ordinal) || line.StartsWith("ACPI_", StringComparison.Ordinal) || line.StartsWith("M16_", StringComparison.Ordinal) || IsAppExitLine(line);
    }

    private static void FindAppExitMarkers(string serial, out AppExitMarkerResult[] appExitResults, out string[] malformedAppExitMarkers)
    {
        List<AppExitMarkerResult> results = new List<AppExitMarkerResult>();
        List<string> malformed = new List<string>();
        using StringReader reader = new StringReader(serial);
        while (reader.ReadLine()is { } line)
        {
            string trimmed = line.Trim();
            if (!IsAppExitLine(trimmed))
            {
                continue;
            }

            if (TryParseAppExitLine(trimmed, out AppExitMarkerResult result, out string malformedMarker))
            {
                results.Add(result);
                continue;
            }

            malformed.Add(malformedMarker);
        }

        appExitResults = results.Count == 0 ? EmptyAppExitResults : results.ToArray();
        malformedAppExitMarkers = malformed.Count == 0 ? EmptyMarkers : malformed.ToArray();
    }

    private static bool TryParseAppExitLine(string line, out AppExitMarkerResult result, out string malformedMarker)
    {
        result = default;
        malformedMarker = string.Empty;
        string[] tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length != 3 || !string.Equals(tokens[0], "APP_EXIT", StringComparison.Ordinal))
        {
            malformedMarker = FormatMalformedAppExitMarker("invalid_format", line);
            return false;
        }

        const string codePrefix = "code=";
        const string statusPrefix = "status=";
        string codeToken = tokens[1];
        string statusToken = tokens[2];
        if (!codeToken.StartsWith(codePrefix, StringComparison.Ordinal) || !statusToken.StartsWith(statusPrefix, StringComparison.Ordinal))
        {
            malformedMarker = FormatMalformedAppExitMarker("invalid_format", line);
            return false;
        }

        string codeText = codeToken[codePrefix.Length..];
        string status = statusToken[statusPrefix.Length..];
        if (!IsUnsignedDecimal(codeText) || !int.TryParse(codeText, out int exitCode) || exitCode is < 0 or > 255)
        {
            malformedMarker = FormatMalformedAppExitMarker("invalid_code", line);
            return false;
        }

        if (!IsAppExitStatusToken(status))
        {
            malformedMarker = FormatMalformedAppExitMarker("invalid_status", line);
            return false;
        }

        result = new AppExitMarkerResult(exitCode, status);
        return true;
    }

    private static bool IsAppExitLine(string line)
    {
        return string.Equals(line, "APP_EXIT", StringComparison.Ordinal) || line.StartsWith("APP_EXIT ", StringComparison.Ordinal);
    }

    private static bool IsUnsignedDecimal(string value)
    {
        if (value.Length == 0)
        {
            return false;
        }

        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if (character is < '0' or > '9')
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAppExitStatusToken(string value)
    {
        const string prefix = "OSHOST_STATUS_";
        if (!value.StartsWith(prefix, StringComparison.Ordinal) || value.Length == prefix.Length)
        {
            return false;
        }

        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if ((character is >= 'A' and <= 'Z') || (character is >= '0' and <= '9') || character == '_')
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private static string FormatMalformedAppExitMarker(string reason, string line)
    {
        return "reason=" + reason + " line=" + line;
    }
}
