using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OS.Tooling;

public static partial class NativeAotLogUploadHttpContract
{
    public sealed record PublicationValues(string ImageSha256, int Sources, int NativeInputs, int Outputs)
    {
        public bool GrantsOriginalPublicationOrExecutionAuthority => false;
    }
}
