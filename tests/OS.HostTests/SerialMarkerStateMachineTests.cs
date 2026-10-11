using System.Runtime.CompilerServices;
using System.Text;
using OS.Tools.QemuRunner;

internal static class SerialMarkerStateMachineTests
{
    [HostFixture(HostLane.Fast)]
    internal static void Run()
    {
        Console.WriteLine("TEST_START serial.marker.state_machine");
        int failures = 0;

        RunCase("required marker every split", TestRequiredMarkersAtEverySplit, ref failures);
        RunCase("forbidden marker every split", TestForbiddenMarkerAtEverySplit, ref failures);
        RunCase("arbitrary chunks noise and line endings", TestArbitraryChunksAndNoise, ref failures);
        RunCase("line boundaries reject substring lookalikes", TestRejectsSubstringLookalikes, ref failures);
        RunCase("partial EOF and complete unterminated line", TestPartialAndCompleteEndOfStream, ref failures);
        RunCase("missing timeout and forbidden precedence", TestMissingTimeoutAndForbiddenPrecedence, ref failures);
        RunCase("duplicate and conflicting terminal markers", TestDuplicateAndConflictingTerminalMarkers, ref failures);
        RunCase("invalid byte noise policy", TestInvalidByteNoisePolicy, ref failures);
        RunCase("line and total capacity failures", TestCapacityFailures, ref failures);
        RunCase("m02.005 long marker boundary", TestM02005LongMarkerBoundary, ref failures);
        RunCase("m02.010 long marker and transcript boundary", TestM02010LongMarkerBoundary, ref failures);
        RunCase("configuration overflow and defaults", TestConfigurationAndDefaultResults, ref failures);
        RunCase("finalization is stable", TestFinalizationIsStable, ref failures);

        if (failures != 0)
        {
            Console.WriteLine("TEST_FAIL serial.marker.state_machine failures=" + failures);
            throw new InvalidOperationException(
                "Serial marker state-machine tests failed: " + failures);
        }

        Console.WriteLine("TEST_PASS serial.marker.state_machine");
    }

    private static void RunCase(string name, Func<bool> test, ref int failures)
    {
        HostCheck.Run("serial.marker.state_machine" + " " + name, () =>
        {
            try
            {
                if (test())
                {
                    return true;
                }
                Console.WriteLine(
                    "TEST_FAIL serial.marker.state_machine case=" + NormalizeCase(name));
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "TEST_FAIL serial.marker.state_machine case="
                    + NormalizeCase(name)
                    + " exception="
                    + ex.GetType().Name);
                throw;
            }
        }, ref failures);
    }

    private static bool TestRequiredMarkersAtEverySplit()
    {
        byte[] serial = Encoding.ASCII.GetBytes(
            "firmware noise\r\n"
            + "  OS_BOOT_OK detail=ready  \n"
            + "OS_SMOKE_PASS\r\n");

        for (int split = 0; split <= serial.Length; split++)
        {
            SerialMarkerStateMachine parser = CreateDefaultParser();
            SerialMarkerFeedResult first = parser.Feed(serial.AsSpan(0, split));
            SerialMarkerFeedResult second = parser.Feed(serial.AsSpan(split));
            SerialMarkerCompletionResult result = parser.Complete();
            if (!first.Succeeded ||
                !second.Succeeded ||
                !result.Passed ||
                result.RequiredSeenCount != 2 ||
                result.TerminalState != SerialMarkerTerminalState.Success ||
                result.SuccessTerminalCount != 1)
            {
                return false;
            }
        }

        SerialMarkerStateMachine oneByteParser = CreateDefaultParser();
        for (int index = 0; index < serial.Length; index++)
        {
            if (!oneByteParser.Feed(serial.AsSpan(index, 1)).Succeeded)
            {
                return false;
            }
        }

        return oneByteParser.Complete().Passed;
    }

    private static bool TestForbiddenMarkerAtEverySplit()
    {
        byte[] serial = Encoding.ASCII.GetBytes(
            "OS_BOOT_OK\n"
            + "UEFI_FORBIDDEN_CALL operation=GetMemoryMap\r\n"
            + "OS_SMOKE_PASS\n");

        for (int split = 0; split <= serial.Length; split++)
        {
            SerialMarkerStateMachine parser = new SerialMarkerStateMachine(
                ["OS_BOOT_OK", "OS_SMOKE_PASS"],
                ["UEFI_FORBIDDEN_CALL"]);
            parser.Feed(serial.AsSpan(0, split));
            parser.Feed(serial.AsSpan(split));
            SerialMarkerCompletionResult result = parser.Complete();
            if (result.Status != SerialMarkerCompletionStatus.ForbiddenMarker ||
                result.ForbiddenMarkerHits is not { Count: 1 } ||
                result.ForbiddenMarkerHits[0] != "UEFI_FORBIDDEN_CALL" ||
                result.ForbiddenOccurrenceCount != 1)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TestArbitraryChunksAndNoise()
    {
        byte[] prefix = [0x1b, (byte)'[', (byte)'2', (byte)'J', 0xff, (byte)'\n'];
        byte[] text = Encoding.ASCII.GetBytes(
            "BdsDxe: firmware chatter\r\n"
            + "\tOS_BOOT_OK\t\n"
            + "OS_SMOKE_PASS");
        byte[] serial = [.. prefix, .. text];
        int[] chunkSizes = [1, 7, 2, 9, 3, 5, 11];

        SerialMarkerStateMachine parser = CreateDefaultParser();
        int offset = 0;
        int chunkIndex = 0;
        while (offset < serial.Length)
        {
            int size = Math.Min(chunkSizes[chunkIndex++ % chunkSizes.Length], serial.Length - offset);
            if (!parser.Feed(serial.AsSpan(offset, size)).Succeeded)
            {
                return false;
            }

            offset += size;
        }

        SerialMarkerCompletionResult result = parser.Complete();
        return result.Passed
            && result.InvalidByteCount == 2
            && result.InvalidNoiseLineCount == 1
            && result.LinesProcessed == 4
            && result.TotalBytes == serial.Length;
    }

    private static bool TestRejectsSubstringLookalikes()
    {
        byte[] serial = Encoding.ASCII.GetBytes(
            "prefixOS_BOOT_OK\n"
            + "OS_BOOT_OK_EXTRA\n"
            + "OS_SMOKE_PASS_EXTRA\n"
            + "OS_SMOKE_PASSX\n");

        SerialMarkerStateMachine parser = CreateDefaultParser();
        parser.Feed(serial);
        SerialMarkerCompletionResult result = parser.Complete();

        return result.Status == SerialMarkerCompletionStatus.MissingRequiredMarker
            && result.RequiredSeenCount == 0
            && result.SuccessTerminalCount == 0
            && result.MissingRequiredMarkers is { Count: 2 }
            && result.MissingRequiredMarkers[0] == "OS_BOOT_OK"
            && result.MissingRequiredMarkers[1] == "OS_SMOKE_PASS";
    }

    private static bool TestPartialAndCompleteEndOfStream()
    {
        SerialMarkerStateMachine partialParser = new SerialMarkerStateMachine(
            ["OS_SMOKE_PASS"],
            ["OS_SMOKE_FAIL"]);
        partialParser.Feed("OS_SMOKE_PA"u8);
        SerialMarkerCompletionResult partial = partialParser.Complete();

        SerialMarkerStateMachine fullParser = new SerialMarkerStateMachine(
            ["OS_SMOKE_PASS"],
            ["OS_SMOKE_FAIL"]);
        fullParser.Feed("OS_SMOKE_PASS"u8);
        SerialMarkerCompletionResult full = fullParser.Complete();

        SerialMarkerStateMachine carriageReturnParser = new SerialMarkerStateMachine(
            ["OS_SMOKE_PASS"],
            ["OS_SMOKE_FAIL"]);
        carriageReturnParser.Feed("OS_SMOKE_PASS\r"u8);
        SerialMarkerCompletionResult carriageReturn = carriageReturnParser.Complete();

        return partial.Status == SerialMarkerCompletionStatus.PartialMarkerAtEndOfStream
            && partial.PartialMarkerAtEndOfStream
            && partial.MissingRequiredMarkers is { Count: 1 }
            && full.Passed
            && full.LinesProcessed == 1
            && carriageReturn.Passed
            && carriageReturn.LinesProcessed == 1;
    }

    private static bool TestMissingTimeoutAndForbiddenPrecedence()
    {
        SerialMarkerStateMachine missingParser = CreateDefaultParser();
        missingParser.Feed("noise only\n"u8);
        SerialMarkerCompletionResult missing = missingParser.Complete();

        SerialMarkerStateMachine timeoutParser = CreateDefaultParser();
        timeoutParser.Feed("OS_BOOT_OK\n"u8);
        SerialMarkerCompletionResult timeout = timeoutParser.Complete(timedOut: true);

        SerialMarkerStateMachine forbiddenParser = new SerialMarkerStateMachine(
            ["OS_BOOT_OK", "OS_SMOKE_PASS"],
            ["UEFI_FORBIDDEN_CALL"]);
        forbiddenParser.Feed(
            "UEFI_FORBIDDEN_CALL\nOS_SMOKE_PASS\nOS_SMOKE_FAIL\n"u8);
        SerialMarkerCompletionResult forbidden = forbiddenParser.Complete(timedOut: true);

        return missing.Status == SerialMarkerCompletionStatus.MissingRequiredMarker
            && timeout.Status == SerialMarkerCompletionStatus.Timeout
            && forbidden.Status == SerialMarkerCompletionStatus.ForbiddenMarker
            && forbidden.TerminalState == SerialMarkerTerminalState.Conflicting
            && forbidden.ForbiddenMarkerHits is { Count: 1 };
    }

    private static bool TestDuplicateAndConflictingTerminalMarkers()
    {
        SerialMarkerStateMachine duplicateParser = new SerialMarkerStateMachine(
            ["OS_SMOKE_PASS"],
            []);
        duplicateParser.Feed("OS_SMOKE_PASS\nOS_SMOKE_PASS\n"u8);
        SerialMarkerCompletionResult duplicate = duplicateParser.Complete();

        SerialMarkerStateMachine conflictParser = new SerialMarkerStateMachine(
            ["OS_SMOKE_PASS"],
            []);
        conflictParser.Feed("OS_SMOKE_PASS\nOS_SMOKE_FAIL reason=panic\n"u8);
        SerialMarkerCompletionResult conflict = conflictParser.Complete();

        SerialMarkerStateMachine failureParser = new SerialMarkerStateMachine([], []);
        failureParser.Feed("OS_SMOKE_FAIL reason=panic\n"u8);
        SerialMarkerCompletionResult failure = failureParser.Complete();

        SerialMarkerStateMachine missingSuccessParser =
            new SerialMarkerStateMachine(["CUSTOM_READY"], []);
        missingSuccessParser.Feed("CUSTOM_READY\n"u8);
        SerialMarkerCompletionResult missingSuccess =
            missingSuccessParser.Complete();

        SerialMarkerStateMachine duplicateFailureParser = new SerialMarkerStateMachine(
            [],
            []);
        duplicateFailureParser.Feed(
            "OS_SMOKE_FAIL reason=first\nOS_SMOKE_FAIL reason=second\n"u8);
        SerialMarkerCompletionResult duplicateFailure = duplicateFailureParser.Complete();

        return duplicate.Status == SerialMarkerCompletionStatus.DuplicateTerminalMarker
            && duplicate.SuccessTerminalCount == 2
            && conflict.Status == SerialMarkerCompletionStatus.ConflictingTerminalMarkers
            && conflict.TerminalState == SerialMarkerTerminalState.Conflicting
            && conflict.SuccessTerminalCount == 1
            && conflict.FailureTerminalCount == 1
            && failure.Status == SerialMarkerCompletionStatus.FailureTerminal
            && failure.TerminalState == SerialMarkerTerminalState.Failure
            && missingSuccess.Status
                == SerialMarkerCompletionStatus.MissingSuccessTerminal
            && missingSuccess.TerminalState == SerialMarkerTerminalState.None
            && !missingSuccess.Passed
            && duplicateFailure.Status
                == SerialMarkerCompletionStatus.DuplicateTerminalMarker
            && duplicateFailure.TerminalState == SerialMarkerTerminalState.Failure
            && duplicateFailure.FailureTerminalCount == 2;
    }

    private static bool TestInvalidByteNoisePolicy()
    {
        byte[] ignoredNoise =
        [
            0xff,
            0xfe,
            (byte)'\n',
            .. "OS_BOOT_OK\nOS_SMOKE_PASS\n"u8
        ];
        SerialMarkerStateMachine ignoredParser = CreateDefaultParser();
        SerialMarkerFeedResult ignoredFeed = ignoredParser.Feed(ignoredNoise);
        SerialMarkerCompletionResult ignored = ignoredParser.Complete();

        byte[] corruptedMarker =
        [
            .. "OS_BOOT_"u8,
            0xff,
            .. "OK\nOS_SMOKE_PASS\n"u8
        ];
        SerialMarkerStateMachine corruptedParser = CreateDefaultParser();
        corruptedParser.Feed(corruptedMarker);
        SerialMarkerCompletionResult corrupted = corruptedParser.Complete();

        SerialMarkerStateMachine strictParser = new SerialMarkerStateMachine(
            ["OS_SMOKE_PASS"],
            ["OS_SMOKE_FAIL"],
            byteNoisePolicy: SerialMarkerByteNoisePolicy.RejectInvalidBytes);
        SerialMarkerFeedResult strictFeed = strictParser.Feed([0xff]);
        SerialMarkerCompletionResult strict = strictParser.Complete();

        return ignoredFeed.Succeeded
            && ignored.Passed
            && ignored.InvalidByteCount == 2
            && ignored.InvalidNoiseLineCount == 1
            && corrupted.Status == SerialMarkerCompletionStatus.MissingRequiredMarker
            && corrupted.InvalidByteCount == 1
            && corrupted.RequiredSeenCount == 1
            && strictFeed.Status == SerialMarkerFeedStatus.InvalidByteRejected
            && !strictFeed.Succeeded
            && strict.Status == SerialMarkerCompletionStatus.InvalidByteSequence
            && !strict.Passed;
    }

    private static bool TestCapacityFailures()
    {
        SerialMarkerStateMachine lineParser = new SerialMarkerStateMachine(
            [],
            [],
            successTerminalMarker: "PASS",
            failureTerminalMarker: "FAIL",
            lineCapacity: 8);
        SerialMarkerFeedResult lineFeed = lineParser.Feed("123456789"u8);
        SerialMarkerCompletionResult line = lineParser.Complete();

        SerialMarkerStateMachine totalParser = new SerialMarkerStateMachine(
            [],
            [],
            successTerminalMarker: "PASS",
            failureTerminalMarker: "FAIL",
            lineCapacity: 8,
            byteCapacity: 4);
        SerialMarkerFeedResult first = totalParser.Feed("1234"u8);
        SerialMarkerFeedResult totalFeed = totalParser.Feed("5"u8);
        SerialMarkerCompletionResult total = totalParser.Complete();

        SerialMarkerStateMachine exactParser = new SerialMarkerStateMachine(
            ["PASSMARK"],
            [],
            successTerminalMarker: "PASSMARK",
            failureTerminalMarker: "FAILURE",
            lineCapacity: 8,
            byteCapacity: 10);
        SerialMarkerFeedResult exactFeed = exactParser.Feed("PASSMARK\r\n"u8);
        SerialMarkerCompletionResult exact = exactParser.Complete();

        SerialMarkerStateMachine noiseParser = new SerialMarkerStateMachine(
            ["PASS"],
            [],
            successTerminalMarker: "PASS",
            failureTerminalMarker: "FAIL",
            lineCapacity: 8,
            byteCapacity: 64);
        SerialMarkerFeedResult noiseFeed = noiseParser.Feed(
        [
            0x1b,
            (byte)'1', (byte)'2', (byte)'3', (byte)'4',
            (byte)'5', (byte)'6', (byte)'7', (byte)'8',
            (byte)'9', (byte)'0', (byte)'1', (byte)'2',
            (byte)'\n',
            (byte)'P', (byte)'A', (byte)'S', (byte)'S', (byte)'\n'
        ]);
        SerialMarkerCompletionResult noise = noiseParser.Complete();

        return lineFeed.Status == SerialMarkerFeedStatus.LineCapacityExceeded
            && lineFeed.BytesConsumed == 9
            && line.Status == SerialMarkerCompletionStatus.CapacityExceeded
            && first.Succeeded
            && totalFeed.Status == SerialMarkerFeedStatus.TotalCapacityExceeded
            && totalFeed.BytesConsumed == 0
            && total.TotalBytes == 4
            && total.Status == SerialMarkerCompletionStatus.CapacityExceeded
            && exactFeed.Succeeded
            && exact.Passed
            && noiseFeed.Succeeded
            && noise.Passed
            && noise.InvalidNoiseLineCount == 1
            && noise.InvalidByteCount == 1;
    }

    private static bool TestConfigurationAndDefaultResults()
    {
        string[] tooManyMarkers = new string[SerialMarkerStateMachine.MaximumMarkerCount + 1];
        for (int index = 0; index < tooManyMarkers.Length; index++)
        {
            tooManyMarkers[index] = "MARKER_" + index;
        }

        bool nullRejected = Throws<ArgumentNullException>(
            () => new SerialMarkerStateMachine(null!, []));
        bool zeroLineRejected = Throws<ArgumentOutOfRangeException>(
            () => new SerialMarkerStateMachine([], [], lineCapacity: 0));
        bool excessiveLineRejected = Throws<ArgumentOutOfRangeException>(
            () => new SerialMarkerStateMachine(
                [],
                [],
                lineCapacity: SerialMarkerStateMachine.MaximumLineCapacity + 1));
        bool zeroTotalRejected = Throws<ArgumentOutOfRangeException>(
            () => new SerialMarkerStateMachine([], [], byteCapacity: 0));
        bool countRejected = Throws<ArgumentOutOfRangeException>(
            () => new SerialMarkerStateMachine(tooManyMarkers, []));
        _ = new SerialMarkerStateMachine(tooManyMarkers[..^1], []);
        _ = new SerialMarkerStateMachine([], [], orderedMarkers: tooManyMarkers[..^1], uniqueMarkers: tooManyMarkers[..^1]);
        countRejected &= Throws<ArgumentOutOfRangeException>(
            () => new SerialMarkerStateMachine([], [], orderedMarkers: tooManyMarkers));
        countRejected &= Throws<ArgumentOutOfRangeException>(
            () => new SerialMarkerStateMachine([], [], uniqueMarkers: tooManyMarkers));
        countRejected &= Throws<ArgumentOutOfRangeException>(
            () => new SerialMarkerStateMachine(tooManyMarkers[..^1], [], orderedMarkers: ["EXTRA_MARKER"]));
        bool duplicateRejected = Throws<ArgumentException>(
            () => new SerialMarkerStateMachine(["MARKER", "MARKER"], []));
        bool overlapRejected = Throws<ArgumentException>(
            () => new SerialMarkerStateMachine(["MARKER"], ["MARKER"]));
        bool emptyMarkerRejected = Throws<ArgumentException>(
            () => new SerialMarkerStateMachine([" \t "], []));
        bool nonAsciiRejected = Throws<ArgumentException>(
            () => new SerialMarkerStateMachine(["M\u00c4RKER"], []));
        bool excessiveMarkerRejected = Throws<ArgumentOutOfRangeException>(
            () => new SerialMarkerStateMachine(
                [new string('A', SerialMarkerStateMachine.MaximumMarkerLength + 1)],
                []));
        bool markerBeyondLineRejected = Throws<ArgumentOutOfRangeException>(
            () => new SerialMarkerStateMachine(
                ["12345"],
                [],
                successTerminalMarker: "OK",
                failureTerminalMarker: "NO",
                lineCapacity: 4));
        bool terminalOverlapRejected = Throws<ArgumentException>(
            () => new SerialMarkerStateMachine(
                [],
                [],
                successTerminalMarker: "TERMINAL",
                failureTerminalMarker: "TERMINAL"));
        bool invalidPolicyRejected = Throws<ArgumentOutOfRangeException>(
            () => new SerialMarkerStateMachine(
                [],
                [],
                byteNoisePolicy: (SerialMarkerByteNoisePolicy)99));

        SerialMarkerFeedResult defaultFeed = default;
        SerialMarkerCompletionResult defaultCompletion = default;
        return nullRejected
            && zeroLineRejected
            && excessiveLineRejected
            && zeroTotalRejected
            && countRejected
            && duplicateRejected
            && overlapRejected
            && emptyMarkerRejected
            && nonAsciiRejected
            && excessiveMarkerRejected
            && markerBeyondLineRejected
            && terminalOverlapRejected
            && invalidPolicyRejected
            && defaultFeed.Status == SerialMarkerFeedStatus.Uninitialized
            && !defaultFeed.Succeeded
            && defaultCompletion.Status == SerialMarkerCompletionStatus.Uninitialized
            && defaultCompletion.MissingRequiredMarkers is null
            && defaultCompletion.ForbiddenMarkerHits is null
            && !defaultCompletion.Passed;
    }

    private static bool TestM02005LongMarkerBoundary()
    {
        const string M02005Marker =
            "CLROS_M02_005_READY marker=OWNED_COM1_READY markerBytes=16 lineEndingBytes=2 " +
            "configurationWrites=7 lineStatusPolls=18 ownedWriter=true " +
            "ownedMarkerTransport=com1PortIo evidenceMarkerTransport=uefiSerial " +
            "guestPortIo=true guestIn8=true postExitBootServices=false";
        if (M02005Marker.Length != 259 ||
            SerialMarkerStateMachine.MaximumMarkerLength != 512)
        {
            return false;
        }

        SerialMarkerStateMachine m02005Parser = new(
            [M02005Marker, "OS_SMOKE_PASS"],
            []);
        SerialMarkerFeedResult m02005Feed = m02005Parser.Feed(
            Encoding.ASCII.GetBytes(M02005Marker + "\r\nOS_SMOKE_PASS\r\n"));
        SerialMarkerCompletionResult m02005Result = m02005Parser.Complete();

        string maximumMarker = new(
            'M',
            SerialMarkerStateMachine.MaximumMarkerLength);
        SerialMarkerStateMachine maximumParser = new(
            [maximumMarker, "OS_SMOKE_PASS"],
            []);
        SerialMarkerFeedResult maximumFeed = maximumParser.Feed(
            Encoding.ASCII.GetBytes(maximumMarker + "\nOS_SMOKE_PASS\n"));
        SerialMarkerCompletionResult maximumResult = maximumParser.Complete();

        bool overMaximumRejected = Throws<ArgumentOutOfRangeException>(
            () => new SerialMarkerStateMachine(
                [new string(
                    'M',
                    SerialMarkerStateMachine.MaximumMarkerLength + 1)],
                []));

        return m02005Feed.Succeeded
            && m02005Result.Passed
            && m02005Result.RequiredSeenCount == 2
            && maximumFeed.Succeeded
            && maximumResult.Passed
            && maximumResult.RequiredSeenCount == 2
            && overMaximumRejected;
    }

    private static bool TestM02010LongMarkerBoundary()
    {
        const string Start = "TEST_START m02.010";
        const string Ready =
            "CLROS_M02_010_READY vectors=256 hardwareErrorCodeVectors=9 " +
            "syntheticErrorCodeVectors=247 slotBytes=16 pageBytes=4096 " +
            "normalizedSlots=3 commonTargetDelta=4096 " +
            "fnv1a64=428D0353A96B7ACE readBack=true " +
            "guestMaterialization=true loaderData=true pageReleased=true " +
            "stubExecution=false idtInstalled=false faultTriggered=false " +
            "markerTransport=uefiSerial postExitBootServices=false";
        const string Pass = "TEST_PASS m02.010";
        const string Failure =
            "M02_NORMALIZED_INTERRUPT_FRAME_PAGE_FAILED";
        string[] required = ["OS_SMOKE_PASS"];
        string[] ordered =
        [
            "EFI_CONSOLE_READY",
            Start,
            Ready,
            Pass,
            "UEFI_MEMORY_MAP_READY"
        ];
        string[] unique = [Start, Ready, Pass];
        string[] forbidden = ["OS_SMOKE_FAIL", "TEST_FAIL", Failure];

        if (Ready.Length != 370 ||
            SerialMarkerStateMachine.MaximumMarkerLength != 512)
        {
            return false;
        }

        SerialMarkerCompletionResult success = ParseM02010Transcript(
            required,
            forbidden,
            ordered,
            unique,
            ordered.Concat(["OS_SMOKE_PASS"]));
        SerialMarkerCompletionResult missing = ParseM02010Transcript(
            required,
            forbidden,
            ordered,
            unique,
            ordered.Where(marker => marker != Ready).Concat(["OS_SMOKE_PASS"]));
        SerialMarkerCompletionResult reordered = ParseM02010Transcript(
            required,
            forbidden,
            ordered,
            unique,
            new[]
            {
                Start,
                "EFI_CONSOLE_READY",
                Ready,
                Pass,
                "UEFI_MEMORY_MAP_READY",
                "OS_SMOKE_PASS"
            });
        SerialMarkerCompletionResult duplicate = ParseM02010Transcript(
            required,
            forbidden,
            ordered,
            unique,
            ordered.Take(3).Concat([Ready]).Concat(ordered.Skip(3))
                .Concat(["OS_SMOKE_PASS"]));
        SerialMarkerCompletionResult injectedFailure = ParseM02010Transcript(
            required,
            forbidden,
            ordered,
            unique,
            ordered.Concat([Failure, "OS_SMOKE_PASS"]));

        string maximumMarker = new(
            'M',
            SerialMarkerStateMachine.MaximumMarkerLength);
        SerialMarkerStateMachine maximumParser = new(
            [maximumMarker, "OS_SMOKE_PASS"],
            []);
        SerialMarkerFeedResult maximumFeed = maximumParser.Feed(
            Encoding.ASCII.GetBytes(maximumMarker + "\nOS_SMOKE_PASS\n"));
        SerialMarkerCompletionResult maximum = maximumParser.Complete();
        bool overMaximumRejected = Throws<ArgumentOutOfRangeException>(
            () => new SerialMarkerStateMachine(
                [new string(
                    'M',
                    SerialMarkerStateMachine.MaximumMarkerLength + 1)],
                []));

        return success.Passed
            && success.RequiredSeenCount == 6
            && success.OrderedSeenCount == 5
            && success.UniqueSeenCount == 3
            && missing.MissingRequiredMarkers is { Count: 1 }
            && missing.MissingRequiredMarkers[0] == Ready
            && !missing.Passed
            && reordered.OutOfOrderOccurrenceCount != 0
            && !reordered.Passed
            && duplicate.DuplicateUniqueOccurrenceCount != 0
            && !duplicate.Passed
            && injectedFailure.ForbiddenOccurrenceCount != 0
            && !injectedFailure.Passed
            && maximumFeed.Succeeded
            && maximum.Passed
            && overMaximumRejected;
    }

    private static SerialMarkerCompletionResult ParseM02010Transcript(
        IReadOnlyList<string> required,
        IReadOnlyList<string> forbidden,
        IReadOnlyList<string> ordered,
        IReadOnlyList<string> unique,
        IEnumerable<string> lines)
    {
        SerialMarkerStateMachine parser = new(
            required,
            forbidden,
            orderedMarkers: ordered,
            uniqueMarkers: unique);
        string transcript = string.Join("\n", lines) + "\n";
        parser.Feed(Encoding.ASCII.GetBytes(transcript));
        return parser.Complete();
    }

    private static bool TestFinalizationIsStable()
    {
        SerialMarkerStateMachine parser = new SerialMarkerStateMachine(
            ["OS_SMOKE_PASS"],
            ["OS_SMOKE_FAIL"]);
        SerialMarkerFeedResult empty = parser.Feed([]);
        parser.Feed("OS_SMOKE_PASS\n"u8);
        SerialMarkerCompletionResult first = parser.Complete();
        SerialMarkerCompletionResult second = parser.Complete(timedOut: true);
        SerialMarkerFeedResult after = parser.Feed("OS_SMOKE_FAIL\n"u8);

        return empty.Succeeded
            && empty.BytesConsumed == 0
            && first.Passed
            && second.Passed
            && second.Status == first.Status
            && second.TotalBytes == first.TotalBytes
            && parser.IsFinalized
            && after.Status == SerialMarkerFeedStatus.AlreadyFinalized
            && after.BytesConsumed == 0
            && !after.Succeeded;
    }

    private static SerialMarkerStateMachine CreateDefaultParser()
    {
        return new SerialMarkerStateMachine(
            ["OS_BOOT_OK", "OS_SMOKE_PASS"],
            ["OS_SMOKE_FAIL"]);
    }

    private static bool Throws<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
            return false;
        }
        catch (TException)
        {
            return true;
        }
    }

    private static string NormalizeCase(string name)
    {
        return name.Replace(' ', '_');
    }
}
