using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

internal enum StreamDrainResult
{
    EndOfStream,
    Faulted,
    Incomplete
}
