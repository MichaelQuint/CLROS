namespace OS.Tooling;

/// <summary>Small first original-owner development contract. These five GETs
/// establish neither the full middleware matrix nor a browser/framework boot.
/// The enclosing consumer checks actual bytes, metadata, packets and shutdown.</summary>
public static class DiskStaticHttpContract
{
    public const string Schema="clros-disk-http-development-v1";
    public const string Scope="five-response-development";
    public const int MaximumBodyBytes=65536,MatrixTimeoutSeconds=110;
    public static IReadOnlyList<string> Paths {get;}=Array.AsReadOnly(new[]
        {"/healthz","/api/status","/api/items","/index.html","/fixture-nonce.txt"});
}
