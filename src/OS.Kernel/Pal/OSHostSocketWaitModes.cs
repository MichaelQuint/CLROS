using System.Runtime.InteropServices;

namespace OS.Pal;

public static class OSHostSocketWaitModes
{
    public static bool IsDefined(uint mode)
    {
        return mode is OSHostSocketConstants.WaitModeNone or OSHostSocketConstants.WaitModeRead or OSHostSocketConstants.WaitModeWrite or OSHostSocketConstants.WaitModeAccept or OSHostSocketConstants.WaitModeConnect;
    }
}
