namespace OS.Pal;

using OS.Std.Text;

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
                return new OSHostExportCatalogValidationResult(OSHostExportCatalogStatus.InvalidName, index, duplicateEntryIndex: -1, entryCount: RequiredExportCount, domain: entry.Domain, signature: entry.Signature, hasEntryMetadata: true);
            }

            if (!IsValidDomain(entry.Domain))
            {
                return new OSHostExportCatalogValidationResult(OSHostExportCatalogStatus.InvalidDomain, index, duplicateEntryIndex: -1, entryCount: RequiredExportCount, domain: entry.Domain, signature: entry.Signature, hasEntryMetadata: true);
            }

            if (!IsValidSignature(entry.Signature))
            {
                return new OSHostExportCatalogValidationResult(OSHostExportCatalogStatus.InvalidSignature, index, duplicateEntryIndex: -1, entryCount: RequiredExportCount, domain: entry.Domain, signature: entry.Signature, hasEntryMetadata: true);
            }

            for (int previousIndex = 0; previousIndex < index; previousIndex++)
            {
                if (StringComparer.Ordinal.Equals(entry.Name, GetRequiredExport(previousIndex).Name))
                {
                    return new OSHostExportCatalogValidationResult(OSHostExportCatalogStatus.DuplicateName, index, previousIndex, entryCount: RequiredExportCount, domain: entry.Domain, signature: entry.Signature, hasEntryMetadata: true);
                }
            }
        }

        return new OSHostExportCatalogValidationResult(OSHostExportCatalogStatus.Success, entryIndex: -1, duplicateEntryIndex: -1, RequiredExportCount);
    }

    public static OSHostExportCatalogValidationResult Validate(ReadOnlySpan<OSHostExportCatalogEntry> entries)
    {
        if (entries.Length == 0)
        {
            return new OSHostExportCatalogValidationResult(OSHostExportCatalogStatus.EmptyCatalog, entryIndex: -1, duplicateEntryIndex: -1, entryCount: 0);
        }

        for (int index = 0; index < entries.Length; index++)
        {
            OSHostExportCatalogEntry entry = entries[index];
            if (!IsValidExportName(entry.Name))
            {
                return new OSHostExportCatalogValidationResult(OSHostExportCatalogStatus.InvalidName, index, duplicateEntryIndex: -1, entryCount: entries.Length, domain: entry.Domain, signature: entry.Signature, hasEntryMetadata: true);
            }

            if (!IsValidDomain(entry.Domain))
            {
                return new OSHostExportCatalogValidationResult(OSHostExportCatalogStatus.InvalidDomain, index, duplicateEntryIndex: -1, entryCount: entries.Length, domain: entry.Domain, signature: entry.Signature, hasEntryMetadata: true);
            }

            if (!IsValidSignature(entry.Signature))
            {
                return new OSHostExportCatalogValidationResult(OSHostExportCatalogStatus.InvalidSignature, index, duplicateEntryIndex: -1, entryCount: entries.Length, domain: entry.Domain, signature: entry.Signature, hasEntryMetadata: true);
            }

            for (int previousIndex = 0; previousIndex < index; previousIndex++)
            {
                if (StringComparer.Ordinal.Equals(entry.Name, entries[previousIndex].Name))
                {
                    return new OSHostExportCatalogValidationResult(OSHostExportCatalogStatus.DuplicateName, index, previousIndex, entryCount: entries.Length, domain: entry.Domain, signature: entry.Signature, hasEntryMetadata: true);
                }
            }
        }

        return new OSHostExportCatalogValidationResult(OSHostExportCatalogStatus.Success, entryIndex: -1, duplicateEntryIndex: -1, entries.Length);
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

    private static OSHostExportCatalogEntry Entry(string name, OSHostExportDomain domain, OSHostExportSignature signature)
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
