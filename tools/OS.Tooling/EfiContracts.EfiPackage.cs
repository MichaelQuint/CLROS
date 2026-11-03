namespace OS.Tooling;

public sealed record EfiPackage(string Id, string Version, string ContentHashSha512, int FileCount, long TotalBytes, string AggregateSha256, string RootKind);
