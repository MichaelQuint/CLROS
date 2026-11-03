using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Kernel.Build;

internal static partial class KestrelSmokePackageContractTests
{
    private readonly struct HttpResponseSummary
    {
        public HttpResponseSummary(HttpStatusCode statusCode, string contentType, string body)
        {
            this.StatusCode = statusCode;
            this.ContentType = contentType;
            this.Body = body;
        }

        public HttpStatusCode StatusCode
        {
            get;
        }
        public string ContentType
        {
            get;
        }
        public string Body
        {
            get;
        }
    }
}
