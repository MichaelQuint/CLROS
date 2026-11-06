using System.Text;
using System.Text.Json.Serialization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Server.Kestrel.Core;

internal sealed record KestrelSmokeConfigResponse(string AppId, string ContractId, string ExpectedBodyKey, string ExpectedBody, int ExpectedBodyBytes, string ListenEndpoint, string HealthPath);
