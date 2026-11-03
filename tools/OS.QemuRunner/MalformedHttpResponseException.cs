using System.Diagnostics;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Kernel.Build;
using OS.Tools.QemuRunner;

sealed class MalformedHttpResponseException : Exception
{
    public MalformedHttpResponseException(string message) : base(message)
    {
    }
}
