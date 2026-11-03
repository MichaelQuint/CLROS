using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Server.Kestrel.Core;

internal sealed class ServiceStartupWitness
{
    static ServiceStartupWitness() => Console.Error.WriteLine("CLROS_APP_CCTOR minimal.api.service");
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal ServiceStartupWitness() => Console.Error.WriteLine("CLROS_APP_CTOR minimal.api.service");
}
