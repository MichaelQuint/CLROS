using System.Buffers.Binary;
using OS.Std.Hash;

namespace OS.Tooling;

public readonly record struct QemuArpCaptureReceipt(uint Records, uint Requests, uint Replies, uint RequestCrc32, uint ReplyCrc32, ulong PeerMac);
