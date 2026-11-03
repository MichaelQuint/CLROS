using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using OS.Boot;

internal static partial class M01InstructionPublicationEfiEntryTests
{
    private readonly record struct MutationCase(string Name, string ExpectedFailure, Func<string, string> Apply);
}
