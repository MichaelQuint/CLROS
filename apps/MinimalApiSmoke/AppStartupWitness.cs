using System.Text;
using System.Text.Json.Serialization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Server.Kestrel.Core;

// Explicit constructors give qualification an observation of real managed
// initialization. The caller retains this object until application shutdown.
internal sealed class AppStartupWitness
{
    static AppStartupWitness() => Console.Error.WriteLine("CLROS_APP_CCTOR minimal.api.smoke");
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal AppStartupWitness() => Console.Error.WriteLine("CLROS_APP_CTOR minimal.api.smoke");
}
