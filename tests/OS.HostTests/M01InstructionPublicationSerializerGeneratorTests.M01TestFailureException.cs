using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

internal static partial class M01InstructionPublicationSerializerGeneratorTests
{
}

internal static partial class M01InstructionPublicationSerializerGeneratorTests
{
    private sealed class M01TestFailureException : Exception
    {
        public M01TestFailureException(string code) : base(code)
        {
            Code = code;
        }

        public string Code
        {
            get;
        }
    }
}
