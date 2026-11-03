using System.Text;

namespace OS.Tools.QemuRunner;
public enum SerialMarkerTerminalState
{
    None = 0,
    Success = 1,
    Failure = 2,
    Conflicting = 3
}
