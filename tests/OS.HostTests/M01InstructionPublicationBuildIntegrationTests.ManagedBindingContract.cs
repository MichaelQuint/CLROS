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
    private sealed record ManagedBindingContract(int RuntimeImportAttributeCount, string? RuntimeImportAttributeName, byte[]? RuntimeImportConstructorSignature, bool MethodImplAttributePresent, bool MethodImplConstructorPresent, bool MethodImplConstructorShapeValid, int? MethodImplInternalCallValue, bool SerializeIsStatic, bool SerializeIsExtern, bool SerializeHasPInvokeImplementation, bool SerializeHasDllImportAttribute, bool SerializeHasManagedBody, byte[] SerializeSignature);
}
