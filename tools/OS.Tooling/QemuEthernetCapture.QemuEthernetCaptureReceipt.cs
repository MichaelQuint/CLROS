using System.Buffers.Binary;
using OS.Std.Hash;

namespace OS.Tooling;
public readonly record struct QemuEthernetCaptureReceipt(uint Records, uint Matches, uint FrameCrc32);
