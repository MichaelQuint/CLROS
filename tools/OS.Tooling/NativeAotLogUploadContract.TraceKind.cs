using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OS.Tooling;

public static partial class NativeAotLogUploadContract
{
    public enum TraceKind
    {
        CompletedCall,
        UnfinishedCall,
        ResumedCall,
        UnreturnedCall,
        ProcessExit,
        Signal,
        Unclassified
    }
}
