using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using OS.Tooling.Native;

namespace OS.Tooling;
public static partial class NativeAotWebPayload
{
    internal sealed record OwnedRaceCapture(string SelectedJson, CreatedRaceReport Selected, CreatedRaceReport Attempts, CreatedRaceReport Wire) : IDisposable
    {
        public void Dispose()
        {
            Selected.Dispose();
            Attempts.Dispose();
            Wire.Dispose();
        }
    }
}
