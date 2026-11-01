namespace OS.Drivers;
public static partial class EnaRegisterMap
{
    public static class DeviceStatus
    {
        public const uint Offset = 0x0058;
        public const uint Width = sizeof(uint);
        public const byte ReadyBit = 0;
        public const uint Ready = 1u << ReadyBit;
        public const byte AdminQueueRestartInProgressShift = 1;
        public const uint AdminQueueRestartInProgress = 0x00000002;
        public const byte AdminQueueRestartFinishedShift = 2;
        public const uint AdminQueueRestartFinished = 0x00000004;
        public const byte ResetInProgressShift = 3;
        public const uint ResetInProgress = 0x00000008;
        public const byte ResetFinishedShift = 4;
        public const uint ResetFinished = 0x00000010;
        public const byte FatalErrorShift = 5;
        public const uint FatalError = 0x00000020;
        public const byte QuiescentStateInProgressShift = 6;
        public const uint QuiescentStateInProgress = 0x00000040;
        public const byte QuiescentStateAchievedShift = 7;
        public const uint QuiescentStateAchieved = 0x00000080;
        public static bool IsReady(uint deviceStatus)
        {
            return (deviceStatus & Ready) != 0;
        }

        public static bool IsAdminQueueRestartInProgress(uint deviceStatus)
        {
            return (deviceStatus & AdminQueueRestartInProgress) != 0;
        }

        public static bool IsResetInProgress(uint deviceStatus)
        {
            return (deviceStatus & ResetInProgress) != 0;
        }

        public static bool HasFatalError(uint deviceStatus)
        {
            return (deviceStatus & FatalError) != 0;
        }

        public static bool IsQuiescentStateInProgress(uint deviceStatus)
        {
            return (deviceStatus & QuiescentStateInProgress) != 0;
        }
    }
}
