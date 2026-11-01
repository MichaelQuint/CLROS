namespace OS.Pal;
using OS.Std.Text;

public static class OSHostExportCatalogDiagnosticFormatter
{
    public static OSHostExportCatalogDiagnosticFormatResult FormatValidationResult(OSHostExportCatalogValidationResult validation, Span<byte> destination)
    {
        if (!IsFormattableValidationResult(validation))
        {
            return OSHostExportCatalogDiagnosticFormatResult.FromStatus(OSHostStatus.InvalidArgument, validation.Status, (uint)destination.Length);
        }

        int requiredBytes = GetRequiredByteCount(validation);
        if (destination.Length < requiredBytes)
        {
            return new OSHostExportCatalogDiagnosticFormatResult(OSHostStatus.BufferTooSmall, validation.Status, bytesWritten: 0, requiredBytes: (uint)requiredBytes, availableBytes: (uint)destination.Length);
        }

        int offset = 0;
        Append("PAL_EXPORT_CATALOG_VALIDATION status="u8, destination, ref offset);
        Append(GetStatusName(validation.Status), destination, ref offset);
        Append(" entryCount="u8, destination, ref offset);
        AppendDecimal(validation.EntryCount, destination, ref offset);
        Append(" firstFailingIndex="u8, destination, ref offset);
        AppendDecimal(validation.EntryIndex, destination, ref offset);
        Append(" duplicateIndex="u8, destination, ref offset);
        AppendDecimal(validation.DuplicateEntryIndex, destination, ref offset);
        Append(" domain="u8, destination, ref offset);
        Append(GetDomainName(validation), destination, ref offset);
        Append(" signature="u8, destination, ref offset);
        Append(GetSignatureName(validation), destination, ref offset);
        return new OSHostExportCatalogDiagnosticFormatResult(OSHostStatus.Ok, validation.Status, bytesWritten: (uint)offset, requiredBytes: (uint)requiredBytes, availableBytes: (uint)destination.Length);
    }

    public static OSHostExportCatalogDiagnosticFormatResult QueryRequiredBytes(OSHostExportCatalogValidationResult validation)
    {
        if (!IsFormattableValidationResult(validation))
        {
            return OSHostExportCatalogDiagnosticFormatResult.FromStatus(OSHostStatus.InvalidArgument, validation.Status);
        }

        return new OSHostExportCatalogDiagnosticFormatResult(OSHostStatus.Ok, validation.Status, bytesWritten: 0, requiredBytes: (uint)GetRequiredByteCount(validation), availableBytes: 0);
    }

    private static int GetRequiredByteCount(OSHostExportCatalogValidationResult validation)
    {
        return "PAL_EXPORT_CATALOG_VALIDATION status="u8.Length + GetStatusName(validation.Status).Length + " entryCount="u8.Length + AsciiDecimalFormatter.CountInt32Digits(validation.EntryCount) + " firstFailingIndex="u8.Length + AsciiDecimalFormatter.CountInt32Digits(validation.EntryIndex) + " duplicateIndex="u8.Length + AsciiDecimalFormatter.CountInt32Digits(validation.DuplicateEntryIndex) + " domain="u8.Length + GetDomainName(validation).Length + " signature="u8.Length + GetSignatureName(validation).Length;
    }

    private static bool IsFormattableValidationResult(OSHostExportCatalogValidationResult validation)
    {
        if (!validation.IsInitialized || !IsValidCatalogStatus(validation.Status))
        {
            return false;
        }

        if (validation.EntryCount < 0 || validation.EntryIndex < -1 || validation.DuplicateEntryIndex < -1)
        {
            return false;
        }

        return validation.Status switch
        {
            OSHostExportCatalogStatus.Success => validation.EntryCount > 0 && validation.EntryIndex == -1 && validation.DuplicateEntryIndex == -1,
            OSHostExportCatalogStatus.EmptyCatalog => validation.EntryCount == 0 && validation.EntryIndex == -1 && validation.DuplicateEntryIndex == -1,
            OSHostExportCatalogStatus.DuplicateName => validation.EntryCount > 0 && validation.EntryIndex > 0 && validation.EntryIndex < validation.EntryCount && validation.DuplicateEntryIndex >= 0 && validation.DuplicateEntryIndex < validation.EntryIndex,
            _ => validation.EntryCount > 0 && validation.EntryIndex >= 0 && validation.EntryIndex < validation.EntryCount && validation.DuplicateEntryIndex == -1
        };
    }

    private static bool IsValidCatalogStatus(OSHostExportCatalogStatus status)
    {
        return status >= OSHostExportCatalogStatus.Success && status <= OSHostExportCatalogStatus.InvalidSignature;
    }

    private static ReadOnlySpan<byte> GetStatusName(OSHostExportCatalogStatus status)
    {
        return status switch
        {
            OSHostExportCatalogStatus.Success => "Success"u8,
            OSHostExportCatalogStatus.EmptyCatalog => "EmptyCatalog"u8,
            OSHostExportCatalogStatus.InvalidName => "InvalidName"u8,
            OSHostExportCatalogStatus.DuplicateName => "DuplicateName"u8,
            OSHostExportCatalogStatus.InvalidDomain => "InvalidDomain"u8,
            OSHostExportCatalogStatus.InvalidSignature => "InvalidSignature"u8,
            _ => "Unknown"u8
        };
    }

    private static ReadOnlySpan<byte> GetDomainName(OSHostExportCatalogValidationResult validation)
    {
        if (!validation.HasEntryMetadata)
        {
            return "unavailable"u8;
        }

        return validation.Domain switch
        {
            OSHostExportDomain.Memory => "Memory"u8,
            OSHostExportDomain.Time => "Time"u8,
            OSHostExportDomain.ConsoleLog => "ConsoleLog"u8,
            OSHostExportDomain.Entropy => "Entropy"u8,
            OSHostExportDomain.PackageFile => "PackageFile"u8,
            OSHostExportDomain.Config => "Config"u8,
            OSHostExportDomain.Environment => "Environment"u8,
            OSHostExportDomain.Socket => "Socket"u8,
            OSHostExportDomain.Timer => "Timer"u8,
            OSHostExportDomain.Thread => "Thread"u8,
            OSHostExportDomain.Wait => "Wait"u8,
            OSHostExportDomain.Event => "Event"u8,
            OSHostExportDomain.Semaphore => "Semaphore"u8,
            OSHostExportDomain.Mutex => "Mutex"u8,
            OSHostExportDomain.Cpu => "Cpu"u8,
            _ => "Unknown"u8
        };
    }

    private static ReadOnlySpan<byte> GetSignatureName(OSHostExportCatalogValidationResult validation)
    {
        if (!validation.HasEntryMetadata)
        {
            return "unavailable"u8;
        }

        return validation.Signature switch
        {
            OSHostExportSignature.StatusOnly => "StatusOnly"u8,
            OSHostExportSignature.StatusHandle => "StatusHandle"u8,
            OSHostExportSignature.StatusMemoryRequestHandleOut => "StatusMemoryRequestHandleOut"u8,
            OSHostExportSignature.StatusMemoryRange => "StatusMemoryRange"u8,
            OSHostExportSignature.StatusHandleMemoryQueryOut => "StatusHandleMemoryQueryOut"u8,
            OSHostExportSignature.StatusTimeSnapshotOut => "StatusTimeSnapshotOut"u8,
            OSHostExportSignature.StatusWriteLogBytesRequest => "StatusWriteLogBytesRequest"u8,
            OSHostExportSignature.StatusRandomBytesRequest => "StatusRandomBytesRequest"u8,
            OSHostExportSignature.StatusPackagePathHandleOut => "StatusPackagePathHandleOut"u8,
            OSHostExportSignature.StatusHandleReadAtRequest => "StatusHandleReadAtRequest"u8,
            OSHostExportSignature.StatusHandleFileInfoOut => "StatusHandleFileInfoOut"u8,
            OSHostExportSignature.StatusPackageEnumerateRequest => "StatusPackageEnumerateRequest"u8,
            OSHostExportSignature.StatusConfigQuery => "StatusConfigQuery"u8,
            OSHostExportSignature.StatusEnvironmentEnumerationQuery => "StatusEnvironmentEnumerationQuery"u8,
            OSHostExportSignature.StatusSocketCreateOptionsHandleOut => "StatusSocketCreateOptionsHandleOut"u8,
            OSHostExportSignature.StatusSocketRequest => "StatusSocketRequest"u8,
            OSHostExportSignature.StatusSocketIoRequestTransferredOut => "StatusSocketIoRequestTransferredOut"u8,
            OSHostExportSignature.StatusSocketPollEntries => "StatusSocketPollEntries"u8,
            OSHostExportSignature.StatusTimerOptionsHandleOut => "StatusTimerOptionsHandleOut"u8,
            OSHostExportSignature.StatusHandleTimerOptions => "StatusHandleTimerOptions"u8,
            OSHostExportSignature.StatusThreadCreateOptionsHandleOut => "StatusThreadCreateOptionsHandleOut"u8,
            OSHostExportSignature.StatusThreadExitCode => "StatusThreadExitCode"u8,
            OSHostExportSignature.StatusThreadIdOut => "StatusThreadIdOut"u8,
            OSHostExportSignature.StatusWaitOneRequestWaitResultOut => "StatusWaitOneRequestWaitResultOut"u8,
            OSHostExportSignature.StatusWaitManyRequestWaitResultOut => "StatusWaitManyRequestWaitResultOut"u8,
            OSHostExportSignature.StatusEventOptionsHandleOut => "StatusEventOptionsHandleOut"u8,
            OSHostExportSignature.StatusSemaphoreCreateHandleOut => "StatusSemaphoreCreateHandleOut"u8,
            OSHostExportSignature.StatusMutexCreateHandleOut => "StatusMutexCreateHandleOut"u8,
            OSHostExportSignature.StatusMemoryBarrier => "StatusMemoryBarrier"u8,
            _ => "Unknown"u8
        };
    }

    private static void Append(ReadOnlySpan<byte> value, Span<byte> destination, ref int offset)
    {
        AsciiTextBuilderResult result = AsciiTextBuilder.AppendBytes(destination, offset, value);
        if (result.Succeeded)
        {
            offset = result.NextOffset;
        }
    }

    private static void AppendDecimal(int value, Span<byte> destination, ref int offset)
    {
        AsciiTextBuilderResult result = AsciiTextBuilder.AppendInt64Decimal(destination, offset, value);
        if (result.Succeeded)
        {
            offset = result.NextOffset;
        }
    }
}
