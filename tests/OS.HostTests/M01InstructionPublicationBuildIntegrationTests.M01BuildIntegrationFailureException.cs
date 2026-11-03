using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

internal static partial class M01InstructionPublicationBuildIntegrationTests
{
    private sealed class M01BuildIntegrationFailureException : Exception
    {
        internal M01BuildIntegrationFailureException(string code) : base(code)
        {
            Code = code;
        }

        internal string Code { get; }
    }
}
