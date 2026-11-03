namespace OS.Std.Text;
/// <summary>
/// Bounded ASCII argument splitting without allocation or escaping. Spaces outside
/// balanced double quotes delimit tokens; quotes may occur anywhere and are removed.
/// The caller retains stable readable input and trusted writable declared output
/// extents during the call. The out parameters require separate storage.
/// </summary>

public static unsafe class AsciiArgumentSplitter
{
    public const uint MaximumInputBytes = 256, MaximumArguments = 128;
    /// <summary>
    /// Counts arguments and serialized bytes, including one NUL per argument.
    /// Null input is valid only for zero length. Failure sets both outputs to zero.
    /// </summary>

    public static AsciiArgumentStatus Measure(byte* input, uint length, out uint count, out uint outputBytes)
    {
        count = 0;
        outputBytes = 0;
        AsciiArgumentStatus status = Scan(input, length, out uint measuredCount, out uint measuredBytes);
        if (status == AsciiArgumentStatus.Success)
        {
            count = measuredCount;
            outputBytes = measuredBytes;
        }

        return status;
    }

    /// <summary>
    /// Emits NUL-terminated arguments and output-relative slices only after all
    /// syntax, extent, capacity and pairwise alias checks pass. Zero-capacity
    /// destinations may be null when no tokens are needed. Failure preserves
    /// destination bytes and slices and sets both outputs to zero.
    /// </summary>

    public static AsciiArgumentStatus Split(byte* input, uint length, byte* output, uint outputCapacity, AsciiArgumentSlice* slices, uint sliceCapacity, out uint count, out uint outputBytes)
    {
        count = 0;
        outputBytes = 0;
        AsciiArgumentStatus status = Scan(input, length, out uint measuredCount, out uint measuredBytes);
        if (status != AsciiArgumentStatus.Success)
            return status;
        ulong sliceBytes = (ulong)sliceCapacity * (ulong)sizeof(AsciiArgumentSlice);
        if (!Extent(output, outputCapacity))
            return AsciiArgumentStatus.InvalidOutput;
        if (!Extent((byte*)slices, sliceBytes))
            return AsciiArgumentStatus.InvalidSlices;
        if (outputCapacity < measuredBytes)
            return AsciiArgumentStatus.OutputTooSmall;
        if (sliceCapacity < measuredCount)
            return AsciiArgumentStatus.SlicesTooSmall;
        if (Overlaps(input, length, output, outputCapacity) || Overlaps(input, length, (byte*)slices, sliceBytes) || Overlaps(output, outputCapacity, (byte*)slices, sliceBytes))
            return AsciiArgumentStatus.OverlappingStorage;
        bool quoted = false, active = false;
        uint written = 0, argument = 0, start = 0;
        for (uint i = 0; i < length; i++)
        {
            byte value = input[i];
            if (value == ' ' && !quoted)
            {
                if (active)
                {
                    output[written] = 0;
                    slices[argument++] = new AsciiArgumentSlice
                    {
                        Offset = start,
                        Length = written - start
                    };
                    written++;
                    active = false;
                }

                continue;
            }

            if (!active)
            {
                start = written;
                active = true;
            }

            if (value == '"')
                quoted = !quoted;
            else
                output[written++] = value;
        }

        if (active)
        {
            output[written] = 0;
            slices[argument] = new AsciiArgumentSlice
            {
                Offset = start,
                Length = written - start
            };
        }

        count = measuredCount;
        outputBytes = measuredBytes;
        return AsciiArgumentStatus.Success;
    }

    private static AsciiArgumentStatus Scan(byte* input, uint length, out uint count, out uint outputBytes)
    {
        count = 0;
        outputBytes = 0;
        if (length > MaximumInputBytes)
            return AsciiArgumentStatus.InputTooLong;
        if (!Extent(input, length))
            return AsciiArgumentStatus.InvalidInput;
        bool quoted = false, active = false;
        for (uint i = 0; i < length; i++)
        {
            byte value = input[i];
            if (value < 32 || value > 126)
                return AsciiArgumentStatus.InvalidCharacter;
            if (value == '\\')
                return AsciiArgumentStatus.BackslashNotSupported;
            if (value == ' ' && !quoted)
            {
                if (active)
                {
                    outputBytes++;
                    active = false;
                }

                continue;
            }

            if (!active)
            {
                if (count == MaximumArguments)
                    return AsciiArgumentStatus.TooManyArguments;
                count++;
                active = true;
            }

            if (value == '"')
                quoted = !quoted;
            else
                outputBytes++;
        }

        if (quoted)
            return AsciiArgumentStatus.UnbalancedQuotes;
        if (active)
            outputBytes++;
        return AsciiArgumentStatus.Success;
    }

    private static bool Extent(byte* pointer, ulong bytes) => (pointer != null || bytes == 0) && (ulong)pointer <= ulong.MaxValue - bytes;
    private static bool Overlaps(byte* first, ulong firstBytes, byte* second, ulong secondBytes) => firstBytes != 0 && secondBytes != 0 && (ulong)first < (ulong)second + secondBytes && (ulong)second < (ulong)first + firstBytes;
}
