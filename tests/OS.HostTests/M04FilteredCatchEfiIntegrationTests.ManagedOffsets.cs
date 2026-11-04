using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using OS.Tooling;

internal static partial class M04FilteredCatchEfiIntegrationTests
{
    [HostTest("m04.021.filters.efi managed offsets use exact constants without loading a module")]
    private static void ManagedOffsetControls()
    {
        var read = typeof(EfiVerification).GetMethod("ReadFilteredCatchOffsets", BindingFlags.Static | BindingFlags.NonPublic)!
            .CreateDelegate<Func<byte[], (uint Return, uint Continuation)>>();
        var offsets = read(OffsetImage());
        HostCheck.True(offsets == (36u, 37u));
        byte[] catcher = Convert.FromHexString(EfiVerify.FilteredCatchShapes[1].Hex);
        HostCheck.True(EfiVerify.TestFilteredCatchOffsets(catcher, offsets.Return, offsets.Continuation));
        var changed = read(OffsetImage(returnOffset: 35));
        HostCheck.True(!EfiVerify.TestFilteredCatchOffsets(catcher, changed.Return, changed.Continuation),
            "metadata cannot redefine independent native geometry");
        Reject(() => read(OffsetImage(header: 0x26)), "filters_offset_metadata_signature");
        Reject(() => read(OffsetImage(extraFlags: FieldAttributes.InitOnly)), "filters_offset_metadata_field");
        Reject(() => read(OffsetImage(signatureType: 0x08)), "filters_offset_metadata_signature");
        Reject(() => read(OffsetImage(includeContinuation: false)), "filters_offset_metadata_fields_missing");
        Reject(() => read(OffsetImage(constant: 36)), "filters_offset_metadata_constant_type");
        Reject(() => read([]), null);

        static void Reject(Action action, string? reason)
        {
            try
            {
                action();
            }
            catch (ToolFailure failure) when (failure.Reason == reason)
            {
                return;
            }
            catch (BadImageFormatException) when (reason is null)
            {
                return;
            }
            throw new InvalidOperationException("managed offset input accepted " + reason);
        }
    }

    // Synthetic metadata only: no IL methods, entrypoint, execution or native
    // acceptance. Literal signatures are independent ECMA-335 FIELD/UInt32 bytes.
    private static byte[] OffsetImage(uint returnOffset = 36, byte header = 0x06, byte signatureType = 0x09,
        FieldAttributes extraFlags = 0, bool includeContinuation = true, object? constant = null)
    {
        var metadata = new MetadataBuilder();
        metadata.AddModule(0, metadata.GetOrAddString("OffsetControl"), metadata.GetOrAddGuid(Guid.Empty), default, default);
        metadata.AddAssembly(metadata.GetOrAddString("OffsetControl"), new Version(1, 0), default, default, 0, 0);
        metadata.AddTypeDefinition(TypeAttributes.NotPublic, default, metadata.GetOrAddString("<Module>"), default,
            MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
        metadata.AddTypeDefinition(TypeAttributes.NotPublic | TypeAttributes.Abstract | TypeAttributes.Sealed,
            metadata.GetOrAddString("OS.Runtime"), metadata.GetOrAddString("RhpNewFastAllocation"), default,
            MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
        Add("FilterReturnOffset", constant ?? returnOffset);
        if (includeContinuation)
            Add("FilterContinuationOffset", 37u);
        var bytes = new BlobBuilder();
        new ManagedPEBuilder(new PEHeaderBuilder(imageCharacteristics: Characteristics.ExecutableImage | Characteristics.Dll),
            new MetadataRootBuilder(metadata), new BlobBuilder()).Serialize(bytes);
        return bytes.ToArray();

        void Add(string name, object value)
        {
            var field = metadata.AddFieldDefinition(FieldAttributes.Private | FieldAttributes.Static | FieldAttributes.Literal
                | FieldAttributes.HasDefault | extraFlags, metadata.GetOrAddString(name), metadata.GetOrAddBlob(new byte[] { header, signatureType }));
            metadata.AddConstant(field, value);
        }
    }
}
