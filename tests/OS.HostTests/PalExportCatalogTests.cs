using System.Runtime.CompilerServices;
using System.Text;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Pal;
using OS.Std.Text;

internal static partial class PalExportCatalogTests
{
    [HostTest("pal.export catalog lists required symbols", HostLane.Fast, 0)]
    private static bool TestCatalogListsRequiredSymbols()
    {
        OSHostExportCatalogValidationResult validation = OSHostExportCatalog.ValidateRequiredCatalog();
        return HostCheck.Result(validation.Succeeded && validation.EntryCount == OSHostExportCatalog.RequiredExportCount && OSHostExportCatalog.RequiredExportCount == 56 && OSHostExportCatalog.ContainsRequiredExport("OSHost_AllocPages") && OSHostExportCatalog.ContainsRequiredExport("OSHost_ProtectPages") && OSHostExportCatalog.ContainsRequiredExport("OSHost_WriteLogBytes") && OSHostExportCatalog.ContainsRequiredExport("OSHost_GetRandomBytes") && OSHostExportCatalog.ContainsRequiredExport("OSHost_OpenPackageFile") && OSHostExportCatalog.ContainsRequiredExport("OSHost_ReadFileAt") && OSHostExportCatalog.ContainsRequiredExport("OSHost_GetEnvironmentValue") && OSHostExportCatalog.ContainsRequiredExport("OSHost_SocketCreate") && OSHostExportCatalog.ContainsRequiredExport("OSHost_SocketAccept") && OSHostExportCatalog.ContainsRequiredExport("OSHost_TimerCreate") && OSHostExportCatalog.ContainsRequiredExport("OSHost_ThreadCreate") && OSHostExportCatalog.ContainsRequiredExport("OSHost_WaitMany") && OSHostExportCatalog.ContainsRequiredExport("OSHost_EventCreate") && OSHostExportCatalog.ContainsRequiredExport("OSHost_SemaphoreCreate") && OSHostExportCatalog.ContainsRequiredExport("OSHost_MutexLock") && OSHostExportCatalog.ContainsRequiredExport("OSHost_MemoryBarrier") && !OSHostExportCatalog.ContainsRequiredExport("SharpOSHost_AllocPages") && !OSHostExportCatalog.ContainsRequiredExport("OSHost_"));
    }

    [HostTest("pal.export catalog rejects duplicate symbols", HostLane.Fast, 1)]
    private static bool TestCatalogRejectsDuplicateSymbols()
    {
        OSHostExportCatalogEntry[] entries = [new OSHostExportCatalogEntry("OSHost_AllocPages", OSHostExportDomain.Memory, OSHostExportSignature.StatusMemoryRequestHandleOut), new OSHostExportCatalogEntry("OSHost_AllocPages", OSHostExportDomain.Memory, OSHostExportSignature.StatusMemoryRequestHandleOut)];
        OSHostExportCatalogValidationResult validation = OSHostExportCatalog.Validate(entries);
        return HostCheck.Result(validation.IsInitialized && !validation.Succeeded && validation.Status == OSHostExportCatalogStatus.DuplicateName && validation.EntryIndex == 1 && validation.DuplicateEntryIndex == 0 && validation.EntryCount == entries.Length && validation.HasEntryMetadata && validation.Domain == OSHostExportDomain.Memory && validation.Signature == OSHostExportSignature.StatusMemoryRequestHandleOut);
    }

    [HostTest("pal.export catalog rejects invalid names", HostLane.Fast, 2)]
    private static bool TestCatalogRejectsInvalidNames()
    {
        OSHostExportCatalogEntry[] emptyName = [new OSHostExportCatalogEntry("OSHost_", OSHostExportDomain.Memory, OSHostExportSignature.StatusOnly)];
        OSHostExportCatalogEntry[] wrongPrefix = [new OSHostExportCatalogEntry("SharpOSHost_AllocPages", OSHostExportDomain.Memory, OSHostExportSignature.StatusOnly)];
        OSHostExportCatalogEntry[] badCharacter = [new OSHostExportCatalogEntry("OSHost_Alloc-Pages", OSHostExportDomain.Memory, OSHostExportSignature.StatusOnly)];
        OSHostExportCatalogValidationResult emptyNameResult = OSHostExportCatalog.Validate(emptyName);
        OSHostExportCatalogValidationResult wrongPrefixResult = OSHostExportCatalog.Validate(wrongPrefix);
        OSHostExportCatalogValidationResult badCharacterResult = OSHostExportCatalog.Validate(badCharacter);
        return HostCheck.Result(!OSHostExportCatalog.IsValidExportName(null) && !OSHostExportCatalog.IsValidExportName(string.Empty) && !emptyNameResult.Succeeded && emptyNameResult.Status == OSHostExportCatalogStatus.InvalidName && emptyNameResult.HasEntryMetadata && emptyNameResult.Domain == OSHostExportDomain.Memory && emptyNameResult.Signature == OSHostExportSignature.StatusOnly && !wrongPrefixResult.Succeeded && wrongPrefixResult.Status == OSHostExportCatalogStatus.InvalidName && !badCharacterResult.Succeeded && badCharacterResult.Status == OSHostExportCatalogStatus.InvalidName);
    }

    [HostTest("pal.export catalog formats validation marker", HostLane.Fast, 3)]
    private static bool TestFormatsValidationMarker()
    {
        Span<byte> destination = stackalloc byte[192];
        destination.Fill(0);
        OSHostExportCatalogValidationResult successValidation = OSHostExportCatalog.ValidateRequiredCatalog();
        OSHostExportCatalogDiagnosticFormatResult successFormat = OSHostExportCatalogDiagnosticFormatter.FormatValidationResult(successValidation, destination);
        string successText = Encoding.ASCII.GetString(destination[..(int)successFormat.BytesWritten]);
        OSHostExportCatalogEntry[] entries = [new OSHostExportCatalogEntry("OSHost_AllocPages", OSHostExportDomain.Memory, OSHostExportSignature.StatusMemoryRequestHandleOut), new OSHostExportCatalogEntry("OSHost_AllocPages", OSHostExportDomain.Memory, OSHostExportSignature.StatusMemoryRequestHandleOut)];
        Span<byte> failureDestination = stackalloc byte[192];
        failureDestination.Fill(0);
        OSHostExportCatalogValidationResult failureValidation = OSHostExportCatalog.Validate(entries);
        OSHostExportCatalogDiagnosticFormatResult failureFormat = OSHostExportCatalogDiagnosticFormatter.FormatValidationResult(failureValidation, failureDestination);
        string failureText = Encoding.ASCII.GetString(failureDestination[..(int)failureFormat.BytesWritten]);
        OSHostExportCatalogDiagnosticFormatResult query = OSHostExportCatalogDiagnosticFormatter.QueryRequiredBytes(failureValidation);
        return HostCheck.Result(successFormat.Succeeded && successFormat.Status == OSHostStatus.Ok && successFormat.CatalogStatus == OSHostExportCatalogStatus.Success && successText == "PAL_EXPORT_CATALOG_VALIDATION status=Success entryCount=56 firstFailingIndex=-1 duplicateIndex=-1 domain=unavailable signature=unavailable" && failureFormat.Succeeded && failureFormat.CatalogStatus == OSHostExportCatalogStatus.DuplicateName && failureText == "PAL_EXPORT_CATALOG_VALIDATION status=DuplicateName entryCount=2 firstFailingIndex=1 duplicateIndex=0 domain=Memory signature=StatusMemoryRequestHandleOut" && query.IsInitialized && !query.Succeeded && query.Status == OSHostStatus.Ok && query.CatalogStatus == OSHostExportCatalogStatus.DuplicateName && query.BytesWritten == 0 && query.RequiredBytes == failureFormat.RequiredBytes);
    }

    [HostTest("pal.export catalog formatter reports short destination", HostLane.Fast, 5)]
    private static bool TestFormatterReportsShortDestination()
    {
        OSHostExportCatalogEntry[] entries = [new OSHostExportCatalogEntry("OSHost_AllocPages", OSHostExportDomain.Memory, OSHostExportSignature.StatusMemoryRequestHandleOut), new OSHostExportCatalogEntry("OSHost_AllocPages", OSHostExportDomain.Memory, OSHostExportSignature.StatusMemoryRequestHandleOut)];
        OSHostExportCatalogValidationResult validation = OSHostExportCatalog.Validate(entries);
        OSHostExportCatalogDiagnosticFormatResult query = OSHostExportCatalogDiagnosticFormatter.QueryRequiredBytes(validation);
        Span<byte> destination = stackalloc byte[12];
        destination.Fill(0xA5);
        OSHostExportCatalogDiagnosticFormatResult shortResult = OSHostExportCatalogDiagnosticFormatter.FormatValidationResult(validation, destination);
        return HostCheck.Result(shortResult.IsInitialized && !shortResult.Succeeded && shortResult.Status == OSHostStatus.BufferTooSmall && shortResult.CatalogStatus == OSHostExportCatalogStatus.DuplicateName && shortResult.BytesWritten == 0 && shortResult.RequiredBytes == query.RequiredBytes && shortResult.AvailableBytes == (uint)destination.Length && destination[0] == 0xA5 && destination[11] == 0xA5);
    }

    [HostTest("pal.export catalog formatter uses shared ascii text builder", HostLane.Fast, 4)]
    private static bool TestFormatterUsesSharedAsciiTextBuilder()
    {
        var catalog = HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj");
        string owner = typeof(OSHostExportCatalogDiagnosticFormatter).FullName!;
        return HostCheck.Result(HostFormatterSourceGuard.CountFields(catalog, owner, "GetRequiredByteCount",
                [("EntryCount", nameof(AsciiDecimalFormatter.CountInt32Digits)),
                 ("EntryIndex", nameof(AsciiDecimalFormatter.CountInt32Digits)),
                 ("DuplicateEntryIndex", nameof(AsciiDecimalFormatter.CountInt32Digits))])
            && HostFormatterSourceGuard.Append(catalog, owner, "Append", nameof(AsciiTextBuilder.AppendBytes))
            && HostFormatterSourceGuard.Append(catalog, owner, "AppendDecimal", nameof(AsciiTextBuilder.AppendInt64Decimal))
            && catalog.Type(owner).GetMembers("GetInt32DecimalByteCount").Length == 0
            && catalog.Type(owner).GetMembers("AppendInt32").Length == 0);
    }

    [HostTest("pal.export catalog formatter default result is not success", HostLane.Fast, 6)]
    private static bool TestFormatterDefaultResultIsNotSuccess()
    {
        Span<byte> destination = stackalloc byte[64];
        destination.Fill(0xCC);
        OSHostExportCatalogValidationResult defaultValidation = default;
        OSHostExportCatalogDiagnosticFormatResult invalidFormat = OSHostExportCatalogDiagnosticFormatter.FormatValidationResult(defaultValidation, destination);
        OSHostExportCatalogDiagnosticFormatResult invalidQuery = OSHostExportCatalogDiagnosticFormatter.QueryRequiredBytes(defaultValidation);
        OSHostExportCatalogDiagnosticFormatResult defaultFormat = default;
        return HostCheck.Result(invalidFormat.IsInitialized && !invalidFormat.Succeeded && invalidFormat.Status == OSHostStatus.InvalidArgument && invalidFormat.CatalogStatus == OSHostExportCatalogStatus.Success && invalidFormat.BytesWritten == 0 && invalidFormat.RequiredBytes == 0 && invalidFormat.AvailableBytes == (uint)destination.Length && invalidQuery.IsInitialized && !invalidQuery.Succeeded && invalidQuery.Status == OSHostStatus.InvalidArgument && invalidQuery.RequiredBytes == 0 && !defaultFormat.IsInitialized && !defaultFormat.Succeeded && defaultFormat.Status == OSHostStatus.Ok && defaultFormat.RequiredBytes == 0 && defaultFormat.AvailableBytes == 0 && destination[0] == 0xCC && !RuntimeHelpers.IsReferenceOrContainsReferences<OSHostExportCatalogDiagnosticFormatResult>());
    }

    [HostTest("pal.export catalog result default is not success", HostLane.Fast, 7)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        OSHostExportCatalogValidationResult result = default;
        return HostCheck.Result(!result.IsInitialized && !result.HasEntryMetadata && !result.Succeeded && result.Status == OSHostExportCatalogStatus.Success && result.EntryCount == 0 && result.EntryIndex == 0 && result.DuplicateEntryIndex == 0 && result.Domain == OSHostExportDomain.Memory && result.Signature == OSHostExportSignature.StatusOnly && !RuntimeHelpers.IsReferenceOrContainsReferences<OSHostExportCatalogValidationResult>());
    }

    [HostTest("boot.sequence pal export catalog ready", HostLane.Fast, 8)]
    private static bool TestBootSequencePalExportCatalogReady()
    {
        OSHostExportCatalogValidationResult validation = OSHostExportCatalog.ValidateRequiredCatalog();
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(4);
        BootResult result = BootSequence.RunPalExportCatalogReadiness(validation, serial, events);
        string expectedMarker = BootSequence.CreatePalExportCatalogReadyMarker(validation);
        return HostCheck.Result(validation.Succeeded && result.Succeeded && serial.Count == 1 && serial.LineAt(0) == expectedMarker && events.Snapshot()[0] == expectedMarker && expectedMarker == "PAL_EXPORT_CATALOG_READY status=Ready initialized=true catalog=Success entryCount=56 firstFailingIndex=-1 duplicateIndex=-1 domain=unavailable signature=unavailable");
    }

    [HostTest("boot.sequence pal export catalog rejects duplicate", HostLane.Fast, 9)]
    private static bool TestBootSequencePalExportCatalogRejectsDuplicate()
    {
        OSHostExportCatalogEntry[] entries = [new OSHostExportCatalogEntry("OSHost_AllocPages", OSHostExportDomain.Memory, OSHostExportSignature.StatusMemoryRequestHandleOut), new OSHostExportCatalogEntry("OSHost_AllocPages", OSHostExportDomain.Memory, OSHostExportSignature.StatusMemoryRequestHandleOut)];
        OSHostExportCatalogValidationResult validation = OSHostExportCatalog.Validate(entries);
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(4);
        BootResult result = BootSequence.RunPalExportCatalogReadiness(validation, serial, events);
        string expectedMarker = BootSequence.CreatePalExportCatalogNotReadyMarker(PalExportCatalogReadinessStatus.CatalogValidationFailed, validation);
        return HostCheck.Result(!validation.Succeeded && validation.Status == OSHostExportCatalogStatus.DuplicateName && !result.Succeeded && result.Reason.Contains(nameof(PalExportCatalogReadinessStatus.CatalogValidationFailed), StringComparison.Ordinal) && serial.Count == 1 && serial.LineAt(0) == expectedMarker && events.Snapshot()[0] == expectedMarker && expectedMarker == "PAL_EXPORT_CATALOG_NOT_READY status=CatalogValidationFailed initialized=true catalog=DuplicateName entryCount=2 firstFailingIndex=1 duplicateIndex=0 domain=Memory signature=StatusMemoryRequestHandleOut");
    }

    [HostTest("boot.sequence pal export catalog blocks default result", HostLane.Fast, 10)]
    private static bool TestBootSequencePalExportCatalogBlocksDefaultResult()
    {
        OSHostExportCatalogValidationResult validation = default;
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(4);
        BootResult result = BootSequence.RunPalExportCatalogReadiness(validation, serial, events);
        string expectedMarker = BootSequence.CreatePalExportCatalogNotReadyMarker(PalExportCatalogReadinessStatus.NotInitialized, validation);
        return HostCheck.Result(!validation.IsInitialized && !validation.Succeeded && !result.Succeeded && result.Reason.Contains(nameof(PalExportCatalogReadinessStatus.NotInitialized), StringComparison.Ordinal) && serial.Count == 1 && serial.LineAt(0) == expectedMarker && events.Snapshot()[0] == expectedMarker && expectedMarker == "PAL_EXPORT_CATALOG_NOT_READY status=NotInitialized initialized=false catalog=Success entryCount=0 firstFailingIndex=-1 duplicateIndex=-1 domain=unavailable signature=unavailable");
    }

    [HostTest("boot.sequence pal export catalog preserves default markers", HostLane.Fast, 11)]
    private static bool TestBootSequencePalExportCatalogPreservesDefaultMarkers()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(32);
        BootResult result = BootSequence.Run(serial, events);
        if (!result.Succeeded)
        {
            return HostCheck.Result(false);
        }

        for (int index = 0; index < serial.Count; index++)
        {
            string line = serial.LineAt(index);
            if (line.StartsWith(BootSequence.PalExportCatalogReadyPrefix, StringComparison.Ordinal) || line.StartsWith(BootSequence.PalExportCatalogNotReadyPrefix, StringComparison.Ordinal))
            {
                return HostCheck.Result(false);
            }
        }

        return HostCheck.Result(true);
    }
}
