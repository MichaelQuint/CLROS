using System.Text;

namespace OS.Cloud;
public readonly struct AzureWireServerHealthBodyResult
{
    private AzureWireServerHealthBodyResult(AzureWireServerHealthBodyStatus status, string body)
    {
        Status = status;
        Body = body;
    }

    public AzureWireServerHealthBodyStatus Status { get; }
    public string Body { get; }
    public bool Succeeded => Status == AzureWireServerHealthBodyStatus.Success;

    public static AzureWireServerHealthBodyResult Success(string body)
    {
        return new AzureWireServerHealthBodyResult(AzureWireServerHealthBodyStatus.Success, body);
    }

    public static AzureWireServerHealthBodyResult Fail(AzureWireServerHealthBodyStatus status)
    {
        return new AzureWireServerHealthBodyResult(status, string.Empty);
    }
}
