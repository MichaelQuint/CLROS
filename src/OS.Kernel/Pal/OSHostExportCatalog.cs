namespace OS.Pal;

using OS.Std.Text;

public enum OSHostExportDomain : int
{
    Memory = 0,
    Time = 1,
    ConsoleLog = 2,
    Entropy = 3,
    PackageFile = 4,
    Config = 5,
    Environment = 6,
    Socket = 7,
    Timer = 8,
    Thread = 9,
    Wait = 10,
    Event = 11,
    Semaphore = 12,
    Mutex = 13,
    Cpu = 14
}

public enum OSHostExportSignature : int
{
    StatusOnly = 0,
    StatusHandle = 1,
    StatusMemoryRequestHandleOut = 2,
    StatusMemoryRange = 3,
    StatusHandleMemoryQueryOut = 4,
    StatusTimeSnapshotOut = 5,
    StatusWriteLogBytesRequest = 6,
    StatusRandomBytesRequest = 7,
    StatusPackagePathHandleOut = 8,
    StatusHandleReadAtRequest = 9,
    StatusHandleFileInfoOut = 10,
    StatusPackageEnumerateRequest = 11,
    StatusConfigQuery = 12,
    StatusEnvironmentEnumerationQuery = 13,
    StatusSocketCreateOptionsHandleOut = 14,
    StatusSocketRequest = 15,
    StatusSocketIoRequestTransferredOut = 16,
    StatusSocketPollEntries = 17,
    StatusTimerOptionsHandleOut = 18,
    StatusHandleTimerOptions = 19,
    StatusThreadCreateOptionsHandleOut = 20,
    StatusThreadExitCode = 21,
    StatusThreadIdOut = 22,
    StatusWaitOneRequestWaitResultOut = 23,
    StatusWaitManyRequestWaitResultOut = 24,
    StatusEventOptionsHandleOut = 25,
    StatusSemaphoreCreateHandleOut = 26,
    StatusMutexCreateHandleOut = 27,
    StatusMemoryBarrier = 28
}

public enum OSHostExportCatalogStatus : int
{
    Success = 0,
    EmptyCatalog = 1,
    InvalidName = 2,
    DuplicateName = 3,
    InvalidDomain = 4,
    InvalidSignature = 5
}

public readonly struct OSHostExportCatalogEntry
{
    public readonly string Name;

    public readonly OSHostExportDomain Domain;

    public readonly OSHostExportSignature Signature;

    public readonly uint Flags;

    public OSHostExportCatalogEntry(
        string name,
        OSHostExportDomain domain,
        OSHostExportSignature signature,
        uint flags = 0)
    {
        Name = name;
        Domain = domain;
        Signature = signature;
        Flags = flags;
    }
}

public readonly struct OSHostExportCatalogValidationResult
{
    private const uint InitializedFlag = 1;

    private const uint EntryMetadataFlag = 2;

    public readonly OSHostExportCatalogStatus Status;

    public readonly int EntryIndex;

    public readonly int DuplicateEntryIndex;

    public readonly int EntryCount;

    public readonly OSHostExportDomain Domain;

    public readonly OSHostExportSignature Signature;

    public readonly uint Flags;

    public OSHostExportCatalogValidationResult(
        OSHostExportCatalogStatus status,
        int entryIndex,
        int duplicateEntryIndex,
        int entryCount,
        uint flags = InitializedFlag)
        : this(
            status,
            entryIndex,
            duplicateEntryIndex,
            entryCount,
            domain: default,
            signature: default,
            hasEntryMetadata: false,
            flags: flags)
    {
    }

    public OSHostExportCatalogValidationResult(
        OSHostExportCatalogStatus status,
        int entryIndex,
        int duplicateEntryIndex,
        int entryCount,
        OSHostExportDomain domain,
        OSHostExportSignature signature,
        bool hasEntryMetadata,
        uint flags = InitializedFlag)
    {
        Status = status;
        EntryIndex = entryIndex;
        DuplicateEntryIndex = duplicateEntryIndex;
        EntryCount = entryCount;
        Domain = domain;
        Signature = signature;
        Flags = hasEntryMetadata ? flags | EntryMetadataFlag : flags;
    }

    public bool IsInitialized => (Flags & InitializedFlag) != 0;

    public bool HasEntryMetadata => (Flags & EntryMetadataFlag) != 0;

    public bool Succeeded => IsInitialized && Status == OSHostExportCatalogStatus.Success && EntryCount > 0;
}

public readonly struct OSHostExportCatalogDiagnosticFormatResult
{
    private const uint InitializedFlag = 1;

    public readonly OSHostStatus Status;

    public readonly OSHostExportCatalogStatus CatalogStatus;

    public readonly uint BytesWritten;

    public readonly uint RequiredBytes;

    public readonly uint AvailableBytes;

    public readonly uint Flags;

    public OSHostExportCatalogDiagnosticFormatResult(
        OSHostStatus status,
        OSHostExportCatalogStatus catalogStatus,
        uint bytesWritten,
        uint requiredBytes,
        uint availableBytes,
        uint flags = InitializedFlag)
    {
        Status = status;
        CatalogStatus = catalogStatus;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
        AvailableBytes = availableBytes;
        Flags = flags;
    }

    public bool IsInitialized => (Flags & InitializedFlag) != 0;

    public bool Succeeded => IsInitialized
        && Status == OSHostStatus.Ok
        && RequiredBytes != 0
        && BytesWritten == RequiredBytes;

    public static OSHostExportCatalogDiagnosticFormatResult FromStatus(
        OSHostStatus status,
        OSHostExportCatalogStatus catalogStatus,
        uint availableBytes = 0)
    {
        return new OSHostExportCatalogDiagnosticFormatResult(
            status,
            catalogStatus,
            bytesWritten: 0,
            requiredBytes: 0,
            availableBytes);
    }
}

public static class OSHostExportCatalogDiagnosticFormatter
{
    public static OSHostExportCatalogDiagnosticFormatResult FormatValidationResult(
        OSHostExportCatalogValidationResult validation,
        Span<byte> destination)
    {
        if (!IsFormattableValidationResult(validation))
        {
            return OSHostExportCatalogDiagnosticFormatResult.FromStatus(
                OSHostStatus.InvalidArgument,
                validation.Status,
                (uint)destination.Length);
        }

        int requiredBytes = GetRequiredByteCount(validation);
        if (destination.Length < requiredBytes)
        {
            return new OSHostExportCatalogDiagnosticFormatResult(
                OSHostStatus.BufferTooSmall,
                validation.Status,
                bytesWritten: 0,
                requiredBytes: (uint)requiredBytes,
                availableBytes: (uint)destination.Length);
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

        return new OSHostExportCatalogDiagnosticFormatResult(
            OSHostStatus.Ok,
            validation.Status,
            bytesWritten: (uint)offset,
            requiredBytes: (uint)requiredBytes,
            availableBytes: (uint)destination.Length);
    }

    public static OSHostExportCatalogDiagnosticFormatResult QueryRequiredBytes(
        OSHostExportCatalogValidationResult validation)
    {
        if (!IsFormattableValidationResult(validation))
        {
            return OSHostExportCatalogDiagnosticFormatResult.FromStatus(
                OSHostStatus.InvalidArgument,
                validation.Status);
        }

        return new OSHostExportCatalogDiagnosticFormatResult(
            OSHostStatus.Ok,
            validation.Status,
            bytesWritten: 0,
            requiredBytes: (uint)GetRequiredByteCount(validation),
            availableBytes: 0);
    }

    private static int GetRequiredByteCount(OSHostExportCatalogValidationResult validation)
    {
        return "PAL_EXPORT_CATALOG_VALIDATION status="u8.Length
            + GetStatusName(validation.Status).Length
            + " entryCount="u8.Length
            + AsciiDecimalFormatter.CountInt32Digits(validation.EntryCount)
            + " firstFailingIndex="u8.Length
            + AsciiDecimalFormatter.CountInt32Digits(validation.EntryIndex)
            + " duplicateIndex="u8.Length
            + AsciiDecimalFormatter.CountInt32Digits(validation.DuplicateEntryIndex)
            + " domain="u8.Length
            + GetDomainName(validation).Length
            + " signature="u8.Length
            + GetSignatureName(validation).Length;
    }

    private static bool IsFormattableValidationResult(OSHostExportCatalogValidationResult validation)
    {
        if (!validation.IsInitialized || !IsValidCatalogStatus(validation.Status))
        {
            return false;
        }

        if (validation.EntryCount < 0 ||
            validation.EntryIndex < -1 ||
            validation.DuplicateEntryIndex < -1)
        {
            return false;
        }

        return validation.Status switch
        {
            OSHostExportCatalogStatus.Success =>
                validation.EntryCount > 0 &&
                validation.EntryIndex == -1 &&
                validation.DuplicateEntryIndex == -1,
            OSHostExportCatalogStatus.EmptyCatalog =>
                validation.EntryCount == 0 &&
                validation.EntryIndex == -1 &&
                validation.DuplicateEntryIndex == -1,
            OSHostExportCatalogStatus.DuplicateName =>
                validation.EntryCount > 0 &&
                validation.EntryIndex > 0 &&
                validation.EntryIndex < validation.EntryCount &&
                validation.DuplicateEntryIndex >= 0 &&
                validation.DuplicateEntryIndex < validation.EntryIndex,
            _ =>
                validation.EntryCount > 0 &&
                validation.EntryIndex >= 0 &&
                validation.EntryIndex < validation.EntryCount &&
                validation.DuplicateEntryIndex == -1
        };
    }

    private static bool IsValidCatalogStatus(OSHostExportCatalogStatus status)
    {
        return status >= OSHostExportCatalogStatus.Success &&
            status <= OSHostExportCatalogStatus.InvalidSignature;
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

public static class OSHostExportCatalog
{
    public const int RequiredExportCount = 56;

    public static int Count => RequiredExportCount;

    public static OSHostExportCatalogEntry GetRequiredExport(int index)
    {
        return index switch
        {
            0 => Entry("OSHost_AllocPages", OSHostExportDomain.Memory, OSHostExportSignature.StatusMemoryRequestHandleOut),
            1 => Entry("OSHost_FreePages", OSHostExportDomain.Memory, OSHostExportSignature.StatusHandle),
            2 => Entry("OSHost_ProtectPages", OSHostExportDomain.Memory, OSHostExportSignature.StatusMemoryRange),
            3 => Entry("OSHost_QueryMemory", OSHostExportDomain.Memory, OSHostExportSignature.StatusHandleMemoryQueryOut),
            4 => Entry("OSHost_TimeGetMonotonicTicks", OSHostExportDomain.Time, OSHostExportSignature.StatusTimeSnapshotOut),
            5 => Entry("OSHost_TimeGetTickFrequency", OSHostExportDomain.Time, OSHostExportSignature.StatusTimeSnapshotOut),
            6 => Entry("OSHost_TimeGetWallClockUnixNanoseconds", OSHostExportDomain.Time, OSHostExportSignature.StatusTimeSnapshotOut),
            7 => Entry("OSHost_WriteLogBytes", OSHostExportDomain.ConsoleLog, OSHostExportSignature.StatusWriteLogBytesRequest),
            8 => Entry("OSHost_GetRandomBytes", OSHostExportDomain.Entropy, OSHostExportSignature.StatusRandomBytesRequest),
            9 => Entry("OSHost_OpenPackageFile", OSHostExportDomain.PackageFile, OSHostExportSignature.StatusPackagePathHandleOut),
            10 => Entry("OSHost_ReadFileAt", OSHostExportDomain.PackageFile, OSHostExportSignature.StatusHandleReadAtRequest),
            11 => Entry("OSHost_GetFileInfo", OSHostExportDomain.PackageFile, OSHostExportSignature.StatusHandleFileInfoOut),
            12 => Entry("OSHost_CloseFile", OSHostExportDomain.PackageFile, OSHostExportSignature.StatusHandle),
            13 => Entry("OSHost_EnumeratePackage", OSHostExportDomain.PackageFile, OSHostExportSignature.StatusPackageEnumerateRequest),
            14 => Entry("OSHost_GetConfigValue", OSHostExportDomain.Config, OSHostExportSignature.StatusConfigQuery),
            15 => Entry("OSHost_EnumerateConfig", OSHostExportDomain.Config, OSHostExportSignature.StatusEnvironmentEnumerationQuery),
            16 => Entry("OSHost_GetEnvironmentValue", OSHostExportDomain.Environment, OSHostExportSignature.StatusConfigQuery),
            17 => Entry("OSHost_EnumerateEnvironment", OSHostExportDomain.Environment, OSHostExportSignature.StatusEnvironmentEnumerationQuery),
            18 => Entry("OSHost_ReadBlock", OSHostExportDomain.PackageFile, OSHostExportSignature.StatusHandleReadAtRequest),
            19 => Entry("OSHost_SocketCreate", OSHostExportDomain.Socket, OSHostExportSignature.StatusSocketCreateOptionsHandleOut),
            20 => Entry("OSHost_SocketBind", OSHostExportDomain.Socket, OSHostExportSignature.StatusSocketRequest),
            21 => Entry("OSHost_SocketListen", OSHostExportDomain.Socket, OSHostExportSignature.StatusSocketRequest),
            22 => Entry("OSHost_SocketAccept", OSHostExportDomain.Socket, OSHostExportSignature.StatusSocketRequest),
            23 => Entry("OSHost_SocketConnect", OSHostExportDomain.Socket, OSHostExportSignature.StatusSocketRequest),
            24 => Entry("OSHost_SocketSend", OSHostExportDomain.Socket, OSHostExportSignature.StatusSocketIoRequestTransferredOut),
            25 => Entry("OSHost_SocketReceive", OSHostExportDomain.Socket, OSHostExportSignature.StatusSocketIoRequestTransferredOut),
            26 => Entry("OSHost_SocketShutdown", OSHostExportDomain.Socket, OSHostExportSignature.StatusSocketRequest),
            27 => Entry("OSHost_SocketClose", OSHostExportDomain.Socket, OSHostExportSignature.StatusHandle),
            28 => Entry("OSHost_SocketSetOption", OSHostExportDomain.Socket, OSHostExportSignature.StatusSocketRequest),
            29 => Entry("OSHost_SocketGetLocalAddress", OSHostExportDomain.Socket, OSHostExportSignature.StatusSocketRequest),
            30 => Entry("OSHost_SocketGetRemoteAddress", OSHostExportDomain.Socket, OSHostExportSignature.StatusSocketRequest),
            31 => Entry("OSHost_SocketPoll", OSHostExportDomain.Socket, OSHostExportSignature.StatusSocketPollEntries),
            32 => Entry("OSHost_TimerCreate", OSHostExportDomain.Timer, OSHostExportSignature.StatusTimerOptionsHandleOut),
            33 => Entry("OSHost_TimerArm", OSHostExportDomain.Timer, OSHostExportSignature.StatusHandleTimerOptions),
            34 => Entry("OSHost_TimerCancel", OSHostExportDomain.Timer, OSHostExportSignature.StatusHandle),
            35 => Entry("OSHost_TimerClose", OSHostExportDomain.Timer, OSHostExportSignature.StatusHandle),
            36 => Entry("OSHost_ThreadSleepUntil", OSHostExportDomain.Thread, OSHostExportSignature.StatusTimeSnapshotOut),
            37 => Entry("OSHost_ThreadCreate", OSHostExportDomain.Thread, OSHostExportSignature.StatusThreadCreateOptionsHandleOut),
            38 => Entry("OSHost_ThreadExit", OSHostExportDomain.Thread, OSHostExportSignature.StatusThreadExitCode),
            39 => Entry("OSHost_ThreadJoin", OSHostExportDomain.Thread, OSHostExportSignature.StatusHandle),
            40 => Entry("OSHost_ThreadYield", OSHostExportDomain.Thread, OSHostExportSignature.StatusOnly),
            41 => Entry("OSHost_ThreadGetCurrentId", OSHostExportDomain.Thread, OSHostExportSignature.StatusThreadIdOut),
            42 => Entry("OSHost_WaitOne", OSHostExportDomain.Wait, OSHostExportSignature.StatusWaitOneRequestWaitResultOut),
            43 => Entry("OSHost_WaitMany", OSHostExportDomain.Wait, OSHostExportSignature.StatusWaitManyRequestWaitResultOut),
            44 => Entry("OSHost_EventCreate", OSHostExportDomain.Event, OSHostExportSignature.StatusEventOptionsHandleOut),
            45 => Entry("OSHost_EventSet", OSHostExportDomain.Event, OSHostExportSignature.StatusHandle),
            46 => Entry("OSHost_EventReset", OSHostExportDomain.Event, OSHostExportSignature.StatusHandle),
            47 => Entry("OSHost_EventClose", OSHostExportDomain.Event, OSHostExportSignature.StatusHandle),
            48 => Entry("OSHost_SemaphoreCreate", OSHostExportDomain.Semaphore, OSHostExportSignature.StatusSemaphoreCreateHandleOut),
            49 => Entry("OSHost_SemaphoreRelease", OSHostExportDomain.Semaphore, OSHostExportSignature.StatusHandle),
            50 => Entry("OSHost_SemaphoreClose", OSHostExportDomain.Semaphore, OSHostExportSignature.StatusHandle),
            51 => Entry("OSHost_MutexCreate", OSHostExportDomain.Mutex, OSHostExportSignature.StatusMutexCreateHandleOut),
            52 => Entry("OSHost_MutexLock", OSHostExportDomain.Mutex, OSHostExportSignature.StatusHandle),
            53 => Entry("OSHost_MutexUnlock", OSHostExportDomain.Mutex, OSHostExportSignature.StatusHandle),
            54 => Entry("OSHost_MutexClose", OSHostExportDomain.Mutex, OSHostExportSignature.StatusHandle),
            55 => Entry("OSHost_MemoryBarrier", OSHostExportDomain.Cpu, OSHostExportSignature.StatusMemoryBarrier),
            _ => default
        };
    }

    public static OSHostExportCatalogValidationResult ValidateRequiredCatalog()
    {
        for (int index = 0; index < RequiredExportCount; index++)
        {
            OSHostExportCatalogEntry entry = GetRequiredExport(index);
            if (!IsValidExportName(entry.Name))
            {
                return new OSHostExportCatalogValidationResult(
                    OSHostExportCatalogStatus.InvalidName,
                    index,
                    duplicateEntryIndex: -1,
                    entryCount: RequiredExportCount,
                    domain: entry.Domain,
                    signature: entry.Signature,
                    hasEntryMetadata: true);
            }

            if (!IsValidDomain(entry.Domain))
            {
                return new OSHostExportCatalogValidationResult(
                    OSHostExportCatalogStatus.InvalidDomain,
                    index,
                    duplicateEntryIndex: -1,
                    entryCount: RequiredExportCount,
                    domain: entry.Domain,
                    signature: entry.Signature,
                    hasEntryMetadata: true);
            }

            if (!IsValidSignature(entry.Signature))
            {
                return new OSHostExportCatalogValidationResult(
                    OSHostExportCatalogStatus.InvalidSignature,
                    index,
                    duplicateEntryIndex: -1,
                    entryCount: RequiredExportCount,
                    domain: entry.Domain,
                    signature: entry.Signature,
                    hasEntryMetadata: true);
            }

            for (int previousIndex = 0; previousIndex < index; previousIndex++)
            {
                if (StringComparer.Ordinal.Equals(entry.Name, GetRequiredExport(previousIndex).Name))
                {
                    return new OSHostExportCatalogValidationResult(
                        OSHostExportCatalogStatus.DuplicateName,
                        index,
                        previousIndex,
                        entryCount: RequiredExportCount,
                        domain: entry.Domain,
                        signature: entry.Signature,
                        hasEntryMetadata: true);
                }
            }
        }

        return new OSHostExportCatalogValidationResult(
            OSHostExportCatalogStatus.Success,
            entryIndex: -1,
            duplicateEntryIndex: -1,
            RequiredExportCount);
    }

    public static OSHostExportCatalogValidationResult Validate(ReadOnlySpan<OSHostExportCatalogEntry> entries)
    {
        if (entries.Length == 0)
        {
            return new OSHostExportCatalogValidationResult(
                OSHostExportCatalogStatus.EmptyCatalog,
                entryIndex: -1,
                duplicateEntryIndex: -1,
                entryCount: 0);
        }

        for (int index = 0; index < entries.Length; index++)
        {
            OSHostExportCatalogEntry entry = entries[index];
            if (!IsValidExportName(entry.Name))
            {
                return new OSHostExportCatalogValidationResult(
                    OSHostExportCatalogStatus.InvalidName,
                    index,
                    duplicateEntryIndex: -1,
                    entryCount: entries.Length,
                    domain: entry.Domain,
                    signature: entry.Signature,
                    hasEntryMetadata: true);
            }

            if (!IsValidDomain(entry.Domain))
            {
                return new OSHostExportCatalogValidationResult(
                    OSHostExportCatalogStatus.InvalidDomain,
                    index,
                    duplicateEntryIndex: -1,
                    entryCount: entries.Length,
                    domain: entry.Domain,
                    signature: entry.Signature,
                    hasEntryMetadata: true);
            }

            if (!IsValidSignature(entry.Signature))
            {
                return new OSHostExportCatalogValidationResult(
                    OSHostExportCatalogStatus.InvalidSignature,
                    index,
                    duplicateEntryIndex: -1,
                    entryCount: entries.Length,
                    domain: entry.Domain,
                    signature: entry.Signature,
                    hasEntryMetadata: true);
            }

            for (int previousIndex = 0; previousIndex < index; previousIndex++)
            {
                if (StringComparer.Ordinal.Equals(entry.Name, entries[previousIndex].Name))
                {
                    return new OSHostExportCatalogValidationResult(
                        OSHostExportCatalogStatus.DuplicateName,
                        index,
                        previousIndex,
                        entryCount: entries.Length,
                        domain: entry.Domain,
                        signature: entry.Signature,
                        hasEntryMetadata: true);
                }
            }
        }

        return new OSHostExportCatalogValidationResult(
            OSHostExportCatalogStatus.Success,
            entryIndex: -1,
            duplicateEntryIndex: -1,
            entries.Length);
    }

    public static bool IsValidExportName(string? name)
    {
        if (string.IsNullOrEmpty(name) || !name.StartsWith("OSHost_", StringComparison.Ordinal))
        {
            return false;
        }

        if (name.Length == "OSHost_".Length)
        {
            return false;
        }

        for (int index = 0; index < name.Length; index++)
        {
            char value = name[index];
            if (value >= 'A' && value <= 'Z')
            {
                continue;
            }

            if (value >= 'a' && value <= 'z')
            {
                continue;
            }

            if (value >= '0' && value <= '9')
            {
                continue;
            }

            if (value == '_')
            {
                continue;
            }

            return false;
        }

        return true;
    }

    public static bool ContainsRequiredExport(string name)
    {
        if (!IsValidExportName(name))
        {
            return false;
        }

        for (int index = 0; index < RequiredExportCount; index++)
        {
            if (StringComparer.Ordinal.Equals(GetRequiredExport(index).Name, name))
            {
                return true;
            }
        }

        return false;
    }

    private static OSHostExportCatalogEntry Entry(
        string name,
        OSHostExportDomain domain,
        OSHostExportSignature signature)
    {
        return new OSHostExportCatalogEntry(name, domain, signature);
    }

    private static bool IsValidDomain(OSHostExportDomain domain)
    {
        return domain >= OSHostExportDomain.Memory && domain <= OSHostExportDomain.Cpu;
    }

    private static bool IsValidSignature(OSHostExportSignature signature)
    {
        return signature >= OSHostExportSignature.StatusOnly && signature <= OSHostExportSignature.StatusMemoryBarrier;
    }
}
