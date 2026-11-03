using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OS.Tools.QemuRunner;

public enum EvidenceRunClassification
{
    Passed = 1,
    Failed = 2,
    TimedOut = 3
}
