namespace System.Runtime;
// Host projection of the no-stdlib attribute. The linked EFI adapter retains its
// real declaration and implementation; this attribute has no host behavior.
internal sealed class RuntimeExportAttribute : Attribute
{
    public RuntimeExportAttribute(string entry)
    {
    }
}
