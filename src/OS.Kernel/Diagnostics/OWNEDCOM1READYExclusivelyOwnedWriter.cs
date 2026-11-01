namespace OS.Kernel.Diagnostics;
public static class OwnedCom1ReadyExclusivelyOwnedWriter
{
    public const int MarkerByteCount = 16;
    public const int LineEndingByteCount = 2;
    public const int RequiredSerialByteCount = MarkerByteCount + LineEndingByteCount;
    public const int DefaultTransmitReadyPollLimit = 128;
    public const byte TransmitHoldingRegisterEmptyMask = 0x20;
    public const byte CarriageReturn = 0x0d;
    public const byte LineFeed = 0x0a;
    public static OwnedCom1ReadyWriterResult Emit(Com1OwnedPortIoBackend backend, int transmitReadyPollLimit = DefaultTransmitReadyPollLimit)
    {
        if (transmitReadyPollLimit <= 0)
        {
            return OwnedCom1ReadyWriterResult.Fail(OwnedCom1ReadyWriterStatus.InvalidTransmitReadyPollLimit, default, default, 0, 0, 0, -1, false);
        }

        if (!backend.IsReady)
        {
            return OwnedCom1ReadyWriterResult.Fail(OwnedCom1ReadyWriterStatus.BackendNotReady, default, default, 0, 0, 0, -1, false);
        }

        Com1OwnedPortIoConfigurationResult configuration = backend.ConfigureDefault();
        if (!configuration.Succeeded)
        {
            return OwnedCom1ReadyWriterResult.Fail(OwnedCom1ReadyWriterStatus.ConfigurationFailed, configuration, configuration.LastOperation, 0, 0, 0, -1, true);
        }

        int markerBytesWritten = 0;
        int lineEndingBytesWritten = 0;
        int lineStatusPolls = 0;
        Com1OwnedPortIoOperationResult lastOperation = configuration.LastOperation;
        for (int byteIndex = 0; byteIndex < RequiredSerialByteCount; byteIndex++)
        {
            if (!TryGetPayloadByte(byteIndex, out byte value))
            {
                return OwnedCom1ReadyWriterResult.Fail(OwnedCom1ReadyWriterStatus.InvalidPayloadIndex, configuration, lastOperation, markerBytesWritten, lineEndingBytesWritten, lineStatusPolls, byteIndex, true);
            }

            bool transmitReady = false;
            for (int poll = 0; poll < transmitReadyPollLimit; poll++)
            {
                lastOperation = backend.ReadByte(Com1OwnedPortIoBackend.LineStatusRegisterPort);
                lineStatusPolls++;
                if (!lastOperation.Succeeded)
                {
                    return OwnedCom1ReadyWriterResult.Fail(OwnedCom1ReadyWriterStatus.LineStatusReadFailed, configuration, lastOperation, markerBytesWritten, lineEndingBytesWritten, lineStatusPolls, byteIndex, true);
                }

                if ((lastOperation.Value & TransmitHoldingRegisterEmptyMask) != 0)
                {
                    transmitReady = true;
                    break;
                }
            }

            if (!transmitReady)
            {
                return OwnedCom1ReadyWriterResult.Fail(OwnedCom1ReadyWriterStatus.TransmitReadyTimeout, configuration, lastOperation, markerBytesWritten, lineEndingBytesWritten, lineStatusPolls, byteIndex, true);
            }

            lastOperation = backend.WriteByte(Com1OwnedPortIoBackend.DataRegisterPort, value);
            if (!lastOperation.Succeeded)
            {
                return OwnedCom1ReadyWriterResult.Fail(OwnedCom1ReadyWriterStatus.MarkerWriteFailed, configuration, lastOperation, markerBytesWritten, lineEndingBytesWritten, lineStatusPolls, byteIndex, true);
            }

            if (byteIndex < MarkerByteCount)
            {
                markerBytesWritten++;
            }
            else
            {
                lineEndingBytesWritten++;
            }
        }

        return OwnedCom1ReadyWriterResult.Pass(configuration, lastOperation, lineStatusPolls);
    }

    public static bool TryGetMarkerByte(int index, out byte value)
    {
        if (index < 0 || index >= MarkerByteCount)
        {
            value = 0;
            return false;
        }

        return TryGetPayloadByte(index, out value);
    }

    private static bool TryGetPayloadByte(int index, out byte value)
    {
        switch (index)
        {
            case 0:
                value = (byte)'O';
                return true;
            case 1:
                value = (byte)'W';
                return true;
            case 2:
                value = (byte)'N';
                return true;
            case 3:
                value = (byte)'E';
                return true;
            case 4:
                value = (byte)'D';
                return true;
            case 5:
                value = (byte)'_';
                return true;
            case 6:
                value = (byte)'C';
                return true;
            case 7:
                value = (byte)'O';
                return true;
            case 8:
                value = (byte)'M';
                return true;
            case 9:
                value = (byte)'1';
                return true;
            case 10:
                value = (byte)'_';
                return true;
            case 11:
                value = (byte)'R';
                return true;
            case 12:
                value = (byte)'E';
                return true;
            case 13:
                value = (byte)'A';
                return true;
            case 14:
                value = (byte)'D';
                return true;
            case 15:
                value = (byte)'Y';
                return true;
            case 16:
                value = CarriageReturn;
                return true;
            case 17:
                value = LineFeed;
                return true;
            default:
                value = 0;
                return false;
        }
    }
}
