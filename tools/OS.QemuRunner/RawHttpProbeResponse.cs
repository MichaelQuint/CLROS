using System.Diagnostics;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Kernel.Build;
using OS.Tools.QemuRunner;

readonly struct RawHttpProbeResponse
{
    public RawHttpProbeResponse(int statusCode, byte[] body)
    {
        StatusCode = statusCode;
        Body = body;
    }

    public int StatusCode
    {
        get;
    }
    public byte[] Body
    {
        get;
    }
}
