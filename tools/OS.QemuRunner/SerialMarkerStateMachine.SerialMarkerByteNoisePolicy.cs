using System.Text;

namespace OS.Tools.QemuRunner;
public enum SerialMarkerByteNoisePolicy
{
    IgnoreInvalidLines = 0,
    RejectInvalidBytes = 1
}
