namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Expected identity of one bodyless GET on one guest-observed
    /// remote TCP port. Path is literal ASCII request-target text, without URL
    /// normalization. BodyLength/Sha256 describe the complete identity-coded
    /// Content-Length entity. The synchronous host inspector borrows this value;
    /// it grants no peer, process, source, guest or qualification authority.</summary>
    public readonly record struct FileWireTarget(uint RemotePort, string Path, int BodyLength, string Sha256);
}
