namespace OS.Kernel.Diagnostics;

public static unsafe partial class PanicSafeFormattingSerialReportingOwnedCom1
{
    public const int MaximumSubsystemByteCount = 32;
    public const int MaximumReasonByteCount = 64;
    public const int MaximumCpuVector = 255;
    public const int DefaultTransmitReadyPollLimit = 128;
    public const int ReadinessMarkerByteCount = 125;
    public const int ReadinessSerialByteCount = 127;
    public const byte TransmitHoldingRegisterEmptyMask = 0x20;
    public const byte CarriageReturn = 0x0d;
    public const byte LineFeed = 0x0a;
    private const int FaultPrefixLength = 24;
    private const int ReasonPrefixLength = 8;
    private const int VectorPrefixLength = 8;
    private const int ErrorCodePresentPrefixLength = 18;
    private const int ErrorCodePrefixLength = 11;
    private const int RipPrefixLength = 5;
    private const int RspPrefixLength = 5;
    private const int RbpPrefixLength = 5;
    private const int RflagsPrefixLength = 8;
    private const int Cr2PrefixLength = 5;
    private const int Cr3PrefixLength = 5;
    private const int RaxPrefixLength = 5;
    private const int RbxPrefixLength = 5;
    private const int RcxPrefixLength = 5;
    private const int RdxPrefixLength = 5;
    private const int RsiPrefixLength = 5;
    private const int RdiPrefixLength = 5;
    private const int R8PrefixLength = 4;
    private const int R9PrefixLength = 4;
    private const int R10PrefixLength = 5;
    private const int R11PrefixLength = 5;
    private const int R12PrefixLength = 5;
    private const int R13PrefixLength = 5;
    private const int R14PrefixLength = 5;
    private const int R15PrefixLength = 5;
#if !NOSTDLIB_EFI
    public static PanicSafeOwnedCom1FormatResult FormatFaultSummary(Span<byte> destination, ReadOnlySpan<byte> subsystem, ReadOnlySpan<byte> reason, in PanicSafeOwnedCom1FaultRecord fault)
    {
        fixed (byte* destinationPointer = destination)
        fixed (byte* subsystemPointer = subsystem)
        fixed (byte* reasonPointer = reason)
        {
            return FormatFaultSummary(destinationPointer, destination.Length, subsystemPointer, subsystem.Length, reasonPointer, reason.Length, fault);
        }
    }

    public static PanicSafeOwnedCom1ReportResult ReportFaultSummary(Com1OwnedPortIoBackend backend, Span<byte> scratch, ReadOnlySpan<byte> subsystem, ReadOnlySpan<byte> reason, in PanicSafeOwnedCom1FaultRecord fault, int transmitReadyPollLimit = DefaultTransmitReadyPollLimit)
    {
        fixed (byte* scratchPointer = scratch)
        fixed (byte* subsystemPointer = subsystem)
        fixed (byte* reasonPointer = reason)
        {
            return ReportFaultSummary(backend, scratchPointer, scratch.Length, subsystemPointer, subsystem.Length, reasonPointer, reason.Length, fault, transmitReadyPollLimit);
        }
    }

#endif
    public static PanicSafeOwnedCom1FormatResult FormatFaultSummary(byte* destination, int destinationCapacity, byte* subsystem, int subsystemLength, byte* reason, int reasonLength, in PanicSafeOwnedCom1FaultRecord fault)
    {
        if (!IsValidToken(subsystem, subsystemLength, MaximumSubsystemByteCount))
        {
            return PanicSafeOwnedCom1FormatResult.Fail(PanicSafeOwnedCom1FormatStatus.InvalidSubsystem, 0);
        }

        if (!IsValidToken(reason, reasonLength, MaximumReasonByteCount))
        {
            return PanicSafeOwnedCom1FormatResult.Fail(PanicSafeOwnedCom1FormatStatus.InvalidReason, 0);
        }

        if (fault.Vector < 0 || fault.Vector > MaximumCpuVector)
        {
            return PanicSafeOwnedCom1FormatResult.Fail(PanicSafeOwnedCom1FormatStatus.InvalidVector, 0);
        }

        int requiredBytes = CountRequiredBytes(subsystemLength, reasonLength, fault);
        if (destination == null || destinationCapacity < 0)
        {
            return PanicSafeOwnedCom1FormatResult.Fail(PanicSafeOwnedCom1FormatStatus.InvalidDestination, requiredBytes);
        }

        if (destinationCapacity < requiredBytes)
        {
            return PanicSafeOwnedCom1FormatResult.Fail(PanicSafeOwnedCom1FormatStatus.DestinationTooSmall, requiredBytes);
        }

        PanicSafeAsciiPointerWriter writer = new PanicSafeAsciiPointerWriter(destination);
        WriteFaultPrefix(ref writer);
        writer.Write(subsystem, subsystemLength);
        WriteReasonPrefix(ref writer);
        writer.Write(reason, reasonLength);
        WriteVectorPrefix(ref writer);
        writer.WriteUnsignedDecimal((uint)fault.Vector);
        WriteErrorCodePresentPrefix(ref writer);
        if (fault.HasErrorCode)
        {
            writer.WriteByte((byte)'t');
            writer.WriteByte((byte)'r');
            writer.WriteByte((byte)'u');
            writer.WriteByte((byte)'e');
        }
        else
        {
            writer.WriteByte((byte)'f');
            writer.WriteByte((byte)'a');
            writer.WriteByte((byte)'l');
            writer.WriteByte((byte)'s');
            writer.WriteByte((byte)'e');
        }

        WriteErrorCodePrefix(ref writer);
        writer.WriteHex(fault.ErrorCode);
        WriteRipPrefix(ref writer);
        writer.WriteHex(fault.Rip);
        WriteRspPrefix(ref writer);
        writer.WriteHex(fault.Rsp);
        WriteRbpPrefix(ref writer);
        writer.WriteHex(fault.Rbp);
        WriteRflagsPrefix(ref writer);
        writer.WriteHex(fault.Rflags);
        WriteCr2Prefix(ref writer);
        writer.WriteHex(fault.Cr2);
        WriteCr3Prefix(ref writer);
        writer.WriteHex(fault.Cr3);
        WriteRaxPrefix(ref writer);
        writer.WriteHex(fault.Rax);
        WriteRbxPrefix(ref writer);
        writer.WriteHex(fault.Rbx);
        WriteRcxPrefix(ref writer);
        writer.WriteHex(fault.Rcx);
        WriteRdxPrefix(ref writer);
        writer.WriteHex(fault.Rdx);
        WriteRsiPrefix(ref writer);
        writer.WriteHex(fault.Rsi);
        WriteRdiPrefix(ref writer);
        writer.WriteHex(fault.Rdi);
        WriteR8Prefix(ref writer);
        writer.WriteHex(fault.R8);
        WriteR9Prefix(ref writer);
        writer.WriteHex(fault.R9);
        WriteR10Prefix(ref writer);
        writer.WriteHex(fault.R10);
        WriteR11Prefix(ref writer);
        writer.WriteHex(fault.R11);
        WriteR12Prefix(ref writer);
        writer.WriteHex(fault.R12);
        WriteR13Prefix(ref writer);
        writer.WriteHex(fault.R13);
        WriteR14Prefix(ref writer);
        writer.WriteHex(fault.R14);
        WriteR15Prefix(ref writer);
        writer.WriteHex(fault.R15);
        writer.WriteByte(CarriageReturn);
        writer.WriteByte(LineFeed);
        return PanicSafeOwnedCom1FormatResult.Pass(writer.Written);
    }

    public static PanicSafeOwnedCom1ReportResult ReportFaultSummary(Com1OwnedPortIoBackend backend, byte* scratch, int scratchCapacity, byte* subsystem, int subsystemLength, byte* reason, int reasonLength, in PanicSafeOwnedCom1FaultRecord fault, int transmitReadyPollLimit = DefaultTransmitReadyPollLimit)
    {
        PanicSafeOwnedCom1ReportResult preflight = ValidateReportPreconditions(backend, transmitReadyPollLimit);
        if (preflight.IsInitialized)
        {
            return preflight;
        }

        PanicSafeOwnedCom1FormatResult format = FormatFaultSummary(scratch, scratchCapacity, subsystem, subsystemLength, reason, reasonLength, fault);
        if (!format.Succeeded)
        {
            return PanicSafeOwnedCom1ReportResult.Fail(MapFormatFailure(format.Status), format, default, default, 0, 0, -1, false);
        }

        return ConfigureAndWrite(backend, scratch, format.BytesWritten, false, format, transmitReadyPollLimit);
    }

    public static PanicSafeOwnedCom1ReportResult EmitReadinessMarker(Com1OwnedPortIoBackend backend, int transmitReadyPollLimit = DefaultTransmitReadyPollLimit)
    {
        PanicSafeOwnedCom1ReportResult preflight = ValidateReportPreconditions(backend, transmitReadyPollLimit);
        if (preflight.IsInitialized)
        {
            return preflight;
        }

        PanicSafeOwnedCom1FormatResult format = PanicSafeOwnedCom1FormatResult.Pass(ReadinessSerialByteCount);
        return ConfigureAndWrite(backend, null, ReadinessSerialByteCount, true, format, transmitReadyPollLimit);
    }

    public static bool TryGetReadinessMarkerByte(int index, out byte value)
    {
        if (index < 0 || index >= ReadinessMarkerByteCount)
        {
            value = 0;
            return false;
        }

        return TryGetReadinessPayloadByte(index, out value);
    }

    private static PanicSafeOwnedCom1ReportResult ValidateReportPreconditions(Com1OwnedPortIoBackend backend, int transmitReadyPollLimit)
    {
        if (transmitReadyPollLimit <= 0)
        {
            return PanicSafeOwnedCom1ReportResult.Fail(PanicSafeOwnedCom1ReportStatus.InvalidTransmitReadyPollLimit, default, default, default, 0, 0, -1, false);
        }

        if (!backend.IsReady)
        {
            return PanicSafeOwnedCom1ReportResult.Fail(PanicSafeOwnedCom1ReportStatus.BackendNotReady, default, default, default, 0, 0, -1, false);
        }

        return default;
    }

    private static PanicSafeOwnedCom1ReportResult ConfigureAndWrite(Com1OwnedPortIoBackend backend, byte* bytes, int byteCount, bool useReadinessPayload, PanicSafeOwnedCom1FormatResult format, int transmitReadyPollLimit)
    {
        if (byteCount <= 0 || (!useReadinessPayload && bytes == null))
        {
            return PanicSafeOwnedCom1ReportResult.Fail(PanicSafeOwnedCom1ReportStatus.InvalidDestination, format, default, default, 0, 0, -1, false);
        }

        Com1OwnedPortIoConfigurationResult configuration = backend.ConfigureDefault();
        if (!configuration.Succeeded)
        {
            return PanicSafeOwnedCom1ReportResult.Fail(PanicSafeOwnedCom1ReportStatus.ConfigurationFailed, format, configuration, configuration.LastOperation, 0, 0, 0, true);
        }

        int bytesWritten = 0;
        int lineStatusPolls = 0;
        Com1OwnedPortIoOperationResult lastOperation = configuration.LastOperation;
        for (int byteIndex = 0; byteIndex < byteCount; byteIndex++)
        {
            byte value;
            if (useReadinessPayload)
            {
                if (!TryGetReadinessPayloadByte(byteIndex, out value))
                {
                    return PanicSafeOwnedCom1ReportResult.Fail(PanicSafeOwnedCom1ReportStatus.InvalidPayloadIndex, format, configuration, lastOperation, bytesWritten, lineStatusPolls, byteIndex, true);
                }
            }
            else
            {
                value = bytes[byteIndex];
            }

            bool transmitReady = false;
            for (int poll = 0; poll < transmitReadyPollLimit; poll++)
            {
                lastOperation = backend.ReadByte(Com1OwnedPortIoBackend.LineStatusRegisterPort);
                lineStatusPolls++;
                if (!lastOperation.Succeeded)
                {
                    return PanicSafeOwnedCom1ReportResult.Fail(PanicSafeOwnedCom1ReportStatus.LineStatusReadFailed, format, configuration, lastOperation, bytesWritten, lineStatusPolls, byteIndex, true);
                }

                if ((lastOperation.Value & TransmitHoldingRegisterEmptyMask) != 0)
                {
                    transmitReady = true;
                    break;
                }
            }

            if (!transmitReady)
            {
                return PanicSafeOwnedCom1ReportResult.Fail(PanicSafeOwnedCom1ReportStatus.TransmitReadyTimeout, format, configuration, lastOperation, bytesWritten, lineStatusPolls, byteIndex, true);
            }

            lastOperation = backend.WriteByte(Com1OwnedPortIoBackend.DataRegisterPort, value);
            if (!lastOperation.Succeeded)
            {
                return PanicSafeOwnedCom1ReportResult.Fail(PanicSafeOwnedCom1ReportStatus.SerialWriteFailed, format, configuration, lastOperation, bytesWritten, lineStatusPolls, byteIndex, true);
            }

            bytesWritten++;
        }

        return PanicSafeOwnedCom1ReportResult.Pass(format, configuration, lastOperation, lineStatusPolls);
    }

    private static PanicSafeOwnedCom1ReportStatus MapFormatFailure(PanicSafeOwnedCom1FormatStatus status)
    {
        switch (status)
        {
            case PanicSafeOwnedCom1FormatStatus.InvalidSubsystem:
                return PanicSafeOwnedCom1ReportStatus.InvalidSubsystem;
            case PanicSafeOwnedCom1FormatStatus.InvalidReason:
                return PanicSafeOwnedCom1ReportStatus.InvalidReason;
            case PanicSafeOwnedCom1FormatStatus.InvalidVector:
                return PanicSafeOwnedCom1ReportStatus.InvalidVector;
            case PanicSafeOwnedCom1FormatStatus.InvalidDestination:
                return PanicSafeOwnedCom1ReportStatus.InvalidDestination;
            default:
                return PanicSafeOwnedCom1ReportStatus.DestinationTooSmall;
        }
    }

    private static int CountRequiredBytes(int subsystemLength, int reasonLength, in PanicSafeOwnedCom1FaultRecord fault)
    {
        int requiredBytes = FaultPrefixLength + subsystemLength + ReasonPrefixLength + reasonLength + VectorPrefixLength + CountUnsignedDecimalDigits((uint)fault.Vector) + ErrorCodePresentPrefixLength + (fault.HasErrorCode ? 4 : 5) + 2;
        requiredBytes += ErrorCodePrefixLength + 18;
        requiredBytes += RipPrefixLength + 18;
        requiredBytes += RspPrefixLength + 18;
        requiredBytes += RbpPrefixLength + 18;
        requiredBytes += RflagsPrefixLength + 18;
        requiredBytes += Cr2PrefixLength + 18;
        requiredBytes += Cr3PrefixLength + 18;
        requiredBytes += RaxPrefixLength + 18;
        requiredBytes += RbxPrefixLength + 18;
        requiredBytes += RcxPrefixLength + 18;
        requiredBytes += RdxPrefixLength + 18;
        requiredBytes += RsiPrefixLength + 18;
        requiredBytes += RdiPrefixLength + 18;
        requiredBytes += R8PrefixLength + 18;
        requiredBytes += R9PrefixLength + 18;
        requiredBytes += R10PrefixLength + 18;
        requiredBytes += R11PrefixLength + 18;
        requiredBytes += R12PrefixLength + 18;
        requiredBytes += R13PrefixLength + 18;
        requiredBytes += R14PrefixLength + 18;
        requiredBytes += R15PrefixLength + 18;
        return requiredBytes;
    }

    private static int CountUnsignedDecimalDigits(uint value)
    {
        if (value >= 100)
        {
            return 3;
        }

        return value >= 10 ? 2 : 1;
    }

    private static bool IsValidToken(byte* value, int length, int maximumLength)
    {
        if (value == null || length <= 0 || length > maximumLength)
        {
            return false;
        }

        for (int index = 0; index < length; index++)
        {
            byte current = value[index];
            bool allowed = (current >= (byte)'a' && current <= (byte)'z') || (current >= (byte)'A' && current <= (byte)'Z') || (current >= (byte)'0' && current <= (byte)'9') || current == (byte)'-' || current == (byte)'_' || current == (byte)'.' || current == (byte)':';
            if (!allowed)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryGetReadinessPayloadByte(int index, out byte value)
    {
        switch (index)
        {
            case 0:
                value = (byte)'C';
                return true;
            case 1:
                value = (byte)'L';
                return true;
            case 2:
                value = (byte)'R';
                return true;
            case 3:
                value = (byte)'O';
                return true;
            case 4:
                value = (byte)'S';
                return true;
            case 5:
                value = (byte)'_';
                return true;
            case 6:
                value = (byte)'M';
                return true;
            case 7:
                value = (byte)'0';
                return true;
            case 8:
                value = (byte)'2';
                return true;
            case 9:
                value = (byte)'_';
                return true;
            case 10:
                value = (byte)'0';
                return true;
            case 11:
                value = (byte)'0';
                return true;
            case 12:
                value = (byte)'6';
                return true;
            case 13:
                value = (byte)'_';
                return true;
            case 14:
                value = (byte)'R';
                return true;
            case 15:
                value = (byte)'E';
                return true;
            case 16:
                value = (byte)'A';
                return true;
            case 17:
                value = (byte)'D';
                return true;
            case 18:
                value = (byte)'Y';
                return true;
            case 19:
                value = (byte)' ';
                return true;
            case 20:
                value = (byte)'p';
                return true;
            case 21:
                value = (byte)'a';
                return true;
            case 22:
                value = (byte)'n';
                return true;
            case 23:
                value = (byte)'i';
                return true;
            case 24:
                value = (byte)'c';
                return true;
            case 25:
                value = (byte)'F';
                return true;
            case 26:
                value = (byte)'o';
                return true;
            case 27:
                value = (byte)'r';
                return true;
            case 28:
                value = (byte)'m';
                return true;
            case 29:
                value = (byte)'a';
                return true;
            case 30:
                value = (byte)'t';
                return true;
            case 31:
                value = (byte)'t';
                return true;
            case 32:
                value = (byte)'e';
                return true;
            case 33:
                value = (byte)'r';
                return true;
            case 34:
                value = (byte)'=';
                return true;
            case 35:
                value = (byte)'b';
                return true;
            case 36:
                value = (byte)'o';
                return true;
            case 37:
                value = (byte)'u';
                return true;
            case 38:
                value = (byte)'n';
                return true;
            case 39:
                value = (byte)'d';
                return true;
            case 40:
                value = (byte)'e';
                return true;
            case 41:
                value = (byte)'d';
                return true;
            case 42:
                value = (byte)' ';
                return true;
            case 43:
                value = (byte)'o';
                return true;
            case 44:
                value = (byte)'w';
                return true;
            case 45:
                value = (byte)'n';
                return true;
            case 46:
                value = (byte)'e';
                return true;
            case 47:
                value = (byte)'d';
                return true;
            case 48:
                value = (byte)'C';
                return true;
            case 49:
                value = (byte)'o';
                return true;
            case 50:
                value = (byte)'m';
                return true;
            case 51:
                value = (byte)'1';
                return true;
            case 52:
                value = (byte)'T';
                return true;
            case 53:
                value = (byte)'r';
                return true;
            case 54:
                value = (byte)'a';
                return true;
            case 55:
                value = (byte)'n';
                return true;
            case 56:
                value = (byte)'s';
                return true;
            case 57:
                value = (byte)'p';
                return true;
            case 58:
                value = (byte)'o';
                return true;
            case 59:
                value = (byte)'r';
                return true;
            case 60:
                value = (byte)'t';
                return true;
            case 61:
                value = (byte)'=';
                return true;
            case 62:
                value = (byte)'t';
                return true;
            case 63:
                value = (byte)'r';
                return true;
            case 64:
                value = (byte)'u';
                return true;
            case 65:
                value = (byte)'e';
                return true;
            case 66:
                value = (byte)' ';
                return true;
            case 67:
                value = (byte)'c';
                return true;
            case 68:
                value = (byte)'a';
                return true;
            case 69:
                value = (byte)'l';
                return true;
            case 70:
                value = (byte)'l';
                return true;
            case 71:
                value = (byte)'e';
                return true;
            case 72:
                value = (byte)'r';
                return true;
            case 73:
                value = (byte)'S';
                return true;
            case 74:
                value = (byte)'c';
                return true;
            case 75:
                value = (byte)'r';
                return true;
            case 76:
                value = (byte)'a';
                return true;
            case 77:
                value = (byte)'t';
                return true;
            case 78:
                value = (byte)'c';
                return true;
            case 79:
                value = (byte)'h';
                return true;
            case 80:
                value = (byte)'=';
                return true;
            case 81:
                value = (byte)'t';
                return true;
            case 82:
                value = (byte)'r';
                return true;
            case 83:
                value = (byte)'u';
                return true;
            case 84:
                value = (byte)'e';
                return true;
            case 85:
                value = (byte)' ';
                return true;
            case 86:
                value = (byte)'s';
                return true;
            case 87:
                value = (byte)'t';
                return true;
            case 88:
                value = (byte)'a';
                return true;
            case 89:
                value = (byte)'t';
                return true;
            case 90:
                value = (byte)'u';
                return true;
            case 91:
                value = (byte)'s';
                return true;
            case 92:
                value = (byte)'C';
                return true;
            case 93:
                value = (byte)'o';
                return true;
            case 94:
                value = (byte)'d';
                return true;
            case 95:
                value = (byte)'e';
                return true;
            case 96:
                value = (byte)'d';
                return true;
            case 97:
                value = (byte)'=';
                return true;
            case 98:
                value = (byte)'t';
                return true;
            case 99:
                value = (byte)'r';
                return true;
            case 100:
                value = (byte)'u';
                return true;
            case 101:
                value = (byte)'e';
                return true;
            case 102:
                value = (byte)' ';
                return true;
            case 103:
                value = (byte)'f';
                return true;
            case 104:
                value = (byte)'i';
                return true;
            case 105:
                value = (byte)'r';
                return true;
            case 106:
                value = (byte)'m';
                return true;
            case 107:
                value = (byte)'w';
                return true;
            case 108:
                value = (byte)'a';
                return true;
            case 109:
                value = (byte)'r';
                return true;
            case 110:
                value = (byte)'e';
                return true;
            case 111:
                value = (byte)'F';
                return true;
            case 112:
                value = (byte)'a';
                return true;
            case 113:
                value = (byte)'l';
                return true;
            case 114:
                value = (byte)'l';
                return true;
            case 115:
                value = (byte)'b';
                return true;
            case 116:
                value = (byte)'a';
                return true;
            case 117:
                value = (byte)'c';
                return true;
            case 118:
                value = (byte)'k';
                return true;
            case 119:
                value = (byte)'=';
                return true;
            case 120:
                value = (byte)'f';
                return true;
            case 121:
                value = (byte)'a';
                return true;
            case 122:
                value = (byte)'l';
                return true;
            case 123:
                value = (byte)'s';
                return true;
            case 124:
                value = (byte)'e';
                return true;
            case 125:
                value = CarriageReturn;
                return true;
            case 126:
                value = LineFeed;
                return true;
            default:
                value = 0;
                return false;
        }
    }

    private static void WriteFaultPrefix(ref PanicSafeAsciiPointerWriter writer)
    {
        writer.WriteByte((byte)'O');
        writer.WriteByte((byte)'S');
        writer.WriteByte((byte)'_');
        writer.WriteByte((byte)'S');
        writer.WriteByte((byte)'M');
        writer.WriteByte((byte)'O');
        writer.WriteByte((byte)'K');
        writer.WriteByte((byte)'E');
        writer.WriteByte((byte)'_');
        writer.WriteByte((byte)'F');
        writer.WriteByte((byte)'A');
        writer.WriteByte((byte)'I');
        writer.WriteByte((byte)'L');
        writer.WriteByte((byte)' ');
        writer.WriteByte((byte)'s');
        writer.WriteByte((byte)'u');
        writer.WriteByte((byte)'b');
        writer.WriteByte((byte)'s');
        writer.WriteByte((byte)'y');
        writer.WriteByte((byte)'s');
        writer.WriteByte((byte)'t');
        writer.WriteByte((byte)'e');
        writer.WriteByte((byte)'m');
        writer.WriteByte((byte)'=');
    }

    private static void WriteReasonPrefix(ref PanicSafeAsciiPointerWriter writer)
    {
        writer.WriteByte((byte)' ');
        writer.WriteByte((byte)'r');
        writer.WriteByte((byte)'e');
        writer.WriteByte((byte)'a');
        writer.WriteByte((byte)'s');
        writer.WriteByte((byte)'o');
        writer.WriteByte((byte)'n');
        writer.WriteByte((byte)'=');
    }

    private static void WriteVectorPrefix(ref PanicSafeAsciiPointerWriter writer)
    {
        writer.WriteByte((byte)' ');
        writer.WriteByte((byte)'v');
        writer.WriteByte((byte)'e');
        writer.WriteByte((byte)'c');
        writer.WriteByte((byte)'t');
        writer.WriteByte((byte)'o');
        writer.WriteByte((byte)'r');
        writer.WriteByte((byte)'=');
    }

    private static void WriteErrorCodePresentPrefix(ref PanicSafeAsciiPointerWriter writer)
    {
        writer.WriteByte((byte)' ');
        writer.WriteByte((byte)'e');
        writer.WriteByte((byte)'r');
        writer.WriteByte((byte)'r');
        writer.WriteByte((byte)'o');
        writer.WriteByte((byte)'r');
        writer.WriteByte((byte)'C');
        writer.WriteByte((byte)'o');
        writer.WriteByte((byte)'d');
        writer.WriteByte((byte)'e');
        writer.WriteByte((byte)'P');
        writer.WriteByte((byte)'r');
        writer.WriteByte((byte)'e');
        writer.WriteByte((byte)'s');
        writer.WriteByte((byte)'e');
        writer.WriteByte((byte)'n');
        writer.WriteByte((byte)'t');
        writer.WriteByte((byte)'=');
    }

    private static void WriteErrorCodePrefix(ref PanicSafeAsciiPointerWriter writer)
    {
        writer.WriteByte((byte)' ');
        writer.WriteByte((byte)'e');
        writer.WriteByte((byte)'r');
        writer.WriteByte((byte)'r');
        writer.WriteByte((byte)'o');
        writer.WriteByte((byte)'r');
        writer.WriteByte((byte)'C');
        writer.WriteByte((byte)'o');
        writer.WriteByte((byte)'d');
        writer.WriteByte((byte)'e');
        writer.WriteByte((byte)'=');
    }

    private static void WriteRipPrefix(ref PanicSafeAsciiPointerWriter writer)
    {
        writer.WriteByte((byte)' ');
        writer.WriteByte((byte)'r');
        writer.WriteByte((byte)'i');
        writer.WriteByte((byte)'p');
        writer.WriteByte((byte)'=');
    }

    private static void WriteRspPrefix(ref PanicSafeAsciiPointerWriter writer)
    {
        writer.WriteByte((byte)' ');
        writer.WriteByte((byte)'r');
        writer.WriteByte((byte)'s');
        writer.WriteByte((byte)'p');
        writer.WriteByte((byte)'=');
    }

    private static void WriteRbpPrefix(ref PanicSafeAsciiPointerWriter writer)
    {
        writer.WriteByte((byte)' ');
        writer.WriteByte((byte)'r');
        writer.WriteByte((byte)'b');
        writer.WriteByte((byte)'p');
        writer.WriteByte((byte)'=');
    }

    private static void WriteRflagsPrefix(ref PanicSafeAsciiPointerWriter writer)
    {
        writer.WriteByte((byte)' ');
        writer.WriteByte((byte)'r');
        writer.WriteByte((byte)'f');
        writer.WriteByte((byte)'l');
        writer.WriteByte((byte)'a');
        writer.WriteByte((byte)'g');
        writer.WriteByte((byte)'s');
        writer.WriteByte((byte)'=');
    }

    private static void WriteCr2Prefix(ref PanicSafeAsciiPointerWriter writer)
    {
        writer.WriteByte((byte)' ');
        writer.WriteByte((byte)'c');
        writer.WriteByte((byte)'r');
        writer.WriteByte((byte)'2');
        writer.WriteByte((byte)'=');
    }

    private static void WriteCr3Prefix(ref PanicSafeAsciiPointerWriter writer)
    {
        writer.WriteByte((byte)' ');
        writer.WriteByte((byte)'c');
        writer.WriteByte((byte)'r');
        writer.WriteByte((byte)'3');
        writer.WriteByte((byte)'=');
    }

    private static void WriteRaxPrefix(ref PanicSafeAsciiPointerWriter writer)
    {
        writer.WriteByte((byte)' ');
        writer.WriteByte((byte)'r');
        writer.WriteByte((byte)'a');
        writer.WriteByte((byte)'x');
        writer.WriteByte((byte)'=');
    }

    private static void WriteRbxPrefix(ref PanicSafeAsciiPointerWriter writer)
    {
        writer.WriteByte((byte)' ');
        writer.WriteByte((byte)'r');
        writer.WriteByte((byte)'b');
        writer.WriteByte((byte)'x');
        writer.WriteByte((byte)'=');
    }

    private static void WriteRcxPrefix(ref PanicSafeAsciiPointerWriter writer)
    {
        writer.WriteByte((byte)' ');
        writer.WriteByte((byte)'r');
        writer.WriteByte((byte)'c');
        writer.WriteByte((byte)'x');
        writer.WriteByte((byte)'=');
    }

    private static void WriteRdxPrefix(ref PanicSafeAsciiPointerWriter writer)
    {
        writer.WriteByte((byte)' ');
        writer.WriteByte((byte)'r');
        writer.WriteByte((byte)'d');
        writer.WriteByte((byte)'x');
        writer.WriteByte((byte)'=');
    }

    private static void WriteRsiPrefix(ref PanicSafeAsciiPointerWriter writer)
    {
        writer.WriteByte((byte)' ');
        writer.WriteByte((byte)'r');
        writer.WriteByte((byte)'s');
        writer.WriteByte((byte)'i');
        writer.WriteByte((byte)'=');
    }

    private static void WriteRdiPrefix(ref PanicSafeAsciiPointerWriter writer)
    {
        writer.WriteByte((byte)' ');
        writer.WriteByte((byte)'r');
        writer.WriteByte((byte)'d');
        writer.WriteByte((byte)'i');
        writer.WriteByte((byte)'=');
    }

    private static void WriteR8Prefix(ref PanicSafeAsciiPointerWriter writer)
    {
        writer.WriteByte((byte)' ');
        writer.WriteByte((byte)'r');
        writer.WriteByte((byte)'8');
        writer.WriteByte((byte)'=');
    }

    private static void WriteR9Prefix(ref PanicSafeAsciiPointerWriter writer)
    {
        writer.WriteByte((byte)' ');
        writer.WriteByte((byte)'r');
        writer.WriteByte((byte)'9');
        writer.WriteByte((byte)'=');
    }

    private static void WriteR10Prefix(ref PanicSafeAsciiPointerWriter writer)
    {
        writer.WriteByte((byte)' ');
        writer.WriteByte((byte)'r');
        writer.WriteByte((byte)'1');
        writer.WriteByte((byte)'0');
        writer.WriteByte((byte)'=');
    }

    private static void WriteR11Prefix(ref PanicSafeAsciiPointerWriter writer)
    {
        writer.WriteByte((byte)' ');
        writer.WriteByte((byte)'r');
        writer.WriteByte((byte)'1');
        writer.WriteByte((byte)'1');
        writer.WriteByte((byte)'=');
    }

    private static void WriteR12Prefix(ref PanicSafeAsciiPointerWriter writer)
    {
        writer.WriteByte((byte)' ');
        writer.WriteByte((byte)'r');
        writer.WriteByte((byte)'1');
        writer.WriteByte((byte)'2');
        writer.WriteByte((byte)'=');
    }

    private static void WriteR13Prefix(ref PanicSafeAsciiPointerWriter writer)
    {
        writer.WriteByte((byte)' ');
        writer.WriteByte((byte)'r');
        writer.WriteByte((byte)'1');
        writer.WriteByte((byte)'3');
        writer.WriteByte((byte)'=');
    }

    private static void WriteR14Prefix(ref PanicSafeAsciiPointerWriter writer)
    {
        writer.WriteByte((byte)' ');
        writer.WriteByte((byte)'r');
        writer.WriteByte((byte)'1');
        writer.WriteByte((byte)'4');
        writer.WriteByte((byte)'=');
    }

    private static void WriteR15Prefix(ref PanicSafeAsciiPointerWriter writer)
    {
        writer.WriteByte((byte)' ');
        writer.WriteByte((byte)'r');
        writer.WriteByte((byte)'1');
        writer.WriteByte((byte)'5');
        writer.WriteByte((byte)'=');
    }
}
