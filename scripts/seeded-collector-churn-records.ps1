# Record validation only. The caller must first validate the complete raw CRLF
# transcript, the original GC records/heap and the QEMU runner lifecycle.
function Read-M04SeededChurnRecord {
    param([string] $Line, [string] $Prefix)
    $match = [regex]::Match($Line, '\A' + [regex]::Escape($Prefix) +
        ' (0x[0-9A-F]{16}) (0x[0-9A-F]{16}) (0x[0-9A-F]{16}) (0x[0-9A-F]{16})\z')
    if (-not $match.Success) { throw 'm04_churn_record_shape' }
    $values = New-Object 'UInt64[]' 4
    for ($i = 0; $i -lt 4; $i++) {
        $values[$i] = [Convert]::ToUInt64($match.Groups[$i + 1].Value.Substring(2), 16)
    }
    return ,$values
}

function Assert-M04SeededCollectorChurnRecords {
    param([string[]] $Lines, [string[]] $GcLines)
    if ($Lines.Count -ne 12 -or $GcLines.Count -ne 17 -or
        $Lines[11] -cne 'CLROS_M04_014_READY') { throw 'm04_churn_record_count_or_ready' }

    # Bind to the preceding, already validated original GC/static-root records.
    $stack = Read-M04SeededChurnRecord $GcLines[2] 'M04_GC_ROOT 7'
    $root = Read-M04SeededChurnRecord $GcLines[3] 'M04_GC_ROOT 3'
    $plain = Read-M04SeededChurnRecord $GcLines[4] 'M04_GC_ROOT 4'
    $final = Read-M04SeededChurnRecord $GcLines[15] 'M04_COLLECT 7'
    $checked = Read-M04SeededChurnRecord $GcLines[16] 'M04_COLLECT 8'
    $tag = [Convert]::ToUInt64('C011EC7000000006', 16)
    $records = @()
    for ($i = 0; $i -lt 11; $i++) {
        $records += ,(Read-M04SeededChurnRecord $Lines[$i] ('M04_CHURN ' + $i))
    }
    if ($records[0][0] -ne $plain[1] -or $records[0][1] -ne $root[1] -or
        $records[0][2] -ne $final[0] -or $records[0][3] -ne $tag) { throw 'm04_churn_original_identity' }

    $seeds = @([UInt64]0, [UInt64]1, [Convert]::ToUInt64('DEADBEEF', 16))
    $allocations = @(161, 154, 161)
    $reuses = @(103, 103, 106)
    $fingerprints = @('6159605787538563', 'F6D01A7B41E92C30', '747C5A6156456DA2')
    $identity = [decimal]$final[2]
    $minimumWitness = $checked[3]
    for ($i = 0; $i -lt 3; $i++) {
        $counts = $records[1 + $i * 3]
        $collection = $records[2 + $i * 3]
        $retained = $records[3 + $i * 3]
        if ($counts[0] -ne $seeds[$i] -or $counts[1] -ne 64 -or
            $counts[2] -ne $allocations[$i] -or $counts[3] -ne $allocations[$i] -or
            $collection[0] -ne 2 -or $collection[1] -ne 66 -or
            $collection[2] -ne $reuses[$i] -or
            $collection[3] -ne [Convert]::ToUInt64($fingerprints[$i], 16)) { throw 'm04_churn_seed_accounting' }
        $identity += $allocations[$i]
        if ($identity -gt [decimal][UInt64]::MaxValue -or [decimal]$retained[0] -ne $identity -or
            $retained[1] -ne $final[0] -or $retained[2] -ne $tag) { throw 'm04_churn_retention' }
        if ($retained[3] % 8 -ne 0 -or $retained[3] -gt $minimumWitness -or
            [decimal]$retained[3] -lt [decimal]$stack[0] + 16 + 16384 -or
            [decimal]$retained[3] + 8 -gt [decimal]$stack[2]) { throw 'm04_churn_stack_witness' }
        $minimumWitness = $retained[3]
    }
    $last = $records[10]
    if ($last[0] -ne 4 -or $last[1] -ne $checked[1] -or $last[2] -ne $checked[2] -or
        $last[3] -ne $minimumWitness) { throw 'm04_churn_stack_canaries' }
}
