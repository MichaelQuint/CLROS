namespace OS.Tooling;
public readonly record struct QemuArpReceiveReceipt(uint ReceiveCrc32, uint ReceiveBytes, uint RequestCrc32, ulong PeerMac);
