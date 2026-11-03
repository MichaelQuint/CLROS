using System.Buffers.Binary;
using System.Security.Cryptography;
using OS.Storage;

namespace OS.Tooling;
public sealed record GptImageFaultReceipt(string OriginalSha256, string FinalSha256, long Offset, byte Before, byte After);
