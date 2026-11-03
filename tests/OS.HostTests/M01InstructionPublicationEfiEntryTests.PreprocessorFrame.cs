using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using OS.Boot;

internal static partial class M01InstructionPublicationEfiEntryTests
{
    private readonly record struct PreprocessorFrame(bool IsSelected, bool ParentEmit, bool SelectedValue, bool SeenElse, bool IsOwnedSurvival = false, bool SeenOwnedRevocation = false);
}
