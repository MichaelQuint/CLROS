using System.Collections;
using System.Globalization;
using OS.Tooling;
using Reads = OS.Tooling.NativeDiskReadObservations;

internal static partial class NativeDiskReadObservationTests
{
    [HostTest("disk multi-sector evidence preserves one physical counter and exact bounded payload chunks")]
    private static void MultiSector()
    {
        foreach (uint count in new[]
        {
            2U,
            3U,
            4U
        }

        )
        {
            var f = MultiVector(count);
            var value = Reads.Inspect(f.Rows, f.Image, f.Boundary, allowMultiSector: true);
            HostCheck.Equal(1UL, value.Counters.Copied);
            HostCheck.Equal((int)count + 2, value.Rows.Count);
            HostCheck.Equal(1, value.Rows.Count(r => r.Kind == Reads.Kind.Copied));
            HostCheck.Equal((int)count - 1, value.Rows.Count(r => r.Kind == Reads.Kind.CopyChunk));
            for (uint chunk = 0; chunk < count; chunk++)
            {
                var row = value.Rows[(int)chunk + 2];
                HostCheck.True(row.Sector == 1 && row.DataSector == 1 + chunk && row.ChunkIndex == chunk && row.Bytes == count * 512);
                HostCheck.True(Convert.FromHexString(row.DataHex).AsSpan().SequenceEqual(f.Image.AsSpan((int)(1 + chunk) * 512, 512)));
                HostCheck.True(f.Rows[(int)chunk + 2].Length <= Reads.MaximumRowChars);
            }

            Reject(f); // Existing consumers must explicitly select the new contract.
        }

        var cancelled = MultiVector(4, true);
        var result = Reads.Inspect(cancelled.Rows, cancelled.Image, cancelled.Boundary, allowMultiSector: true);
        HostCheck.True(result.Cancelled == 1 && result.Counters.Copied == 0 && result.Rows.Count == 2);
    }

    [HostTest("disk multi-sector evidence rejects incomplete reordered mismatched oversized and divergent chunks")]
    private static void MultiSectorRejected()
    {
        var f = MultiVector(4);
        void Deny(IReadOnlyList<string> rows, byte[]? image = null)
        {
            bool rejected = false;
            try
            {
                _ = Reads.Inspect(rows, image ?? f.Image, f.Boundary, allowMultiSector: true);
            }
            catch (ToolFailure)
            {
                rejected = true;
            }

            HostCheck.True(rejected);
        }

        for (int i = 0; i < f.Rows.Count; i++)
        {
            var rows = f.Rows.ToList();
            rows.RemoveAt(i);
            Deny(rows);
        }

        foreach (string key in new[]
        {
            "generation",
            "sector",
            "sequence",
            "bytes",
            "available",
            "consumed",
            "chunk"
        }

        )
            Deny(Replace(f.Rows, 3, Set(f.Rows[3], key, "0")));
        Deny(Replace(f.Rows, 3, Set(f.Rows[3], "kind", "2")));
        Deny(Replace(f.Rows, 2, Set(f.Rows[2], "kind", "3")));
        Deny([.. f.Rows, f.Rows[^1]]);
        var reordered = f.Rows.ToList();
        (reordered[3], reordered[4]) = (reordered[4], reordered[3]);
        Deny(reordered);
        Deny(Replace(f.Rows, 0, Set(f.Rows[0], "bytes", "2560")));
        Deny(Replace(f.Rows, 0, Set(f.Rows[0], "sector", "6")));
        Deny(Replace(f.Rows, 1, Set(f.Rows[1], "bytes", "512")));
        for (int sector = 1; sector <= 4; sector++)
        {
            var changed = f.Image.ToArray();
            changed[sector * 512] ^= 1;
            Deny(f.Rows, changed);
        }
    }

    [HostTest("disk copy-clock preserves first-copy capture zero and maximum clocks for each bounded group")]
    private static void CopyClockGroups()
    {
        foreach (uint count in new[]
        {
            2U,
            3U,
            4U
        }

        )
            foreach (ulong copyClock in new[]
            {
                0UL,
                100UL,
                101UL,
                ulong.MaxValue
            }

            )
            {
                var f = MultiVector(count);
                string[] rows = f.Rows.Select((row, i) => Set(row, "clock", N(i < 2 ? Math.Min(100UL, copyClock) : copyClock))).ToArray();
                var actual = Reads.Inspect(rows, f.Image, f.Boundary, allowMultiSector: true);
                HostCheck.Equal(1UL, actual.Counters.Copied);
                HostCheck.True(actual.Rows.Skip(2).All(row => row.Clock == copyClock));
            }
    }

    [HostTest("disk copy-clock rejects every later continuation even when the complete stream remains monotonic")]
    private static void CopyClockContinuations()
    {
        foreach (uint count in new[]
        {
            2U,
            3U,
            4U
        }

        )
            foreach (ulong copyClock in new[]
            {
                0UL,
                100UL
            }

            )
            {
                var f = MultiVector(count);
                for (int changed = 3; changed < f.Rows.Count; changed++)
                {
                    string[] rows = f.Rows.Select((row, i) => Set(row, "clock", N(i < changed ? copyClock : copyClock + 1))).ToArray();
                    bool rejected = false;
                    try
                    {
                        _ = Reads.Inspect(rows, f.Image, f.Boundary, allowMultiSector: true);
                    }
                    catch (ToolFailure failure)
                    {
                        HostCheck.Equal("disk_observation_copy_clock", failure.Message);
                        rejected = true;
                    }

                    HostCheck.True(rejected);
                }
            }
    }

    [HostTest("disk copy-clock keeps group boundaries interleaving cancellation and legacy512 contracts")]
    private static void CopyClockBoundaries()
    {
        var first = MultiVector(2);
        var second = MultiVector(3);
        string Next(string row)
        {
            string changed = Set(Set(Set(row, "generation", "2"), "sequence", "2"), "available", "2");
            changed = Set(changed, "consumed", row.StartsWith("M16_023_IO kind=0 ", StringComparison.Ordinal) ? "1" : "2");
            return Set(changed, "clock", "101");
        }

        List<string> joined = [.. first.Rows, .. second.Rows.Select(Next)];
        var final = first.Boundary with
        {
            Submitted = 2,
            Completed = 2,
            Copied = 2
        };
        var actual = Reads.Inspect(joined, first.Image, final, allowMultiSector: true);
        HostCheck.Equal(2UL, actual.Counters.Copied);
        HostCheck.Equal(9, actual.Rows.Count);
        HostCheck.True(actual.Rows.Take(4).All(row => row.Clock == 100) && actual.Rows.Skip(4).All(row => row.Clock == 101));
        void Deny(IReadOnlyList<string> rows, Reads.Boundary boundary)
        {
            bool rejected = false;
            try
            {
                _ = Reads.Inspect(rows, first.Image, boundary, allowMultiSector: true);
            }
            catch (ToolFailure)
            {
                rejected = true;
            }

            HostCheck.True(rejected);
        }

        var interleaved = joined.ToList();
        interleaved.Insert(3, interleaved[4]);
        Deny(interleaved, final);
        var cancelled = MultiVector(4, true);
        List<string> withCancellation = [.. first.Rows, .. cancelled.Rows.Select(Next)];
        var cancelledFinal = final with
        {
            Copied = 1
        };
        actual = Reads.Inspect(withCancellation, first.Image, cancelledFinal, allowMultiSector: true);
        HostCheck.True(actual.Counters.Copied == 1 && actual.Cancelled == 1);
        Deny([.. withCancellation, Next(MultiVector(4).Rows[3])], cancelledFinal);
        var legacy = Vector();
        foreach (bool multi in new[]
        {
            false,
            true
        }

        )
        {
            actual = Reads.Inspect(legacy.Rows, legacy.Image, legacy.Boundary, allowMultiSector: multi);
            HostCheck.Equal(2UL, actual.Counters.Copied);
            HostCheck.True(actual.Rows[2].Clock == 102 && actual.Rows[5].Clock == 105);
        }
    }

    [HostTest("Full parent counters join inspected one through four sector groups without changing request counts")]
    private static void FullParentSectorCounters()
    {
        var cases = new (uint[] Groups, ulong Requests, ulong FsReads)[]
        {
            ([1U], 38, 4),
            ([2U], 38, 5),
            ([3U], 38, 6),
            ([4U], 38, 7),
            ([1U, 4U], 39, 8)
        };
        foreach (var item in cases)
        {
            var f = FullParentCounterVector(37, item.Groups);
            string[] before = f.Rows.ToArray();
            byte[] image = f.Image.ToArray();
            var device = Reads.Inspect(f.Rows, f.Image, f.Boundary, allowMultiSector: true);
            HostCheck.Equal(item.Requests, device.Counters.Submitted);
            HostCheck.Equal(item.Requests, device.Counters.Completed);
            HostCheck.Equal(item.Requests, device.Counters.Copied);
            HostCheck.Equal(item.Groups.Length, device.Rows.Count(r => r.Kind == Reads.Kind.Submitted));
            HostCheck.Equal(item.Groups.Sum(n => (int)n), device.Rows.Count(r => r.Kind is Reads.Kind.Copied or Reads.Kind.CopyChunk));
            QemuTranscripts.InspectNativeAotDiskStaticEndValues(FullParentCounterEnd(f.Boundary, item.FsReads), f.Boundary, device);
            HostCheck.True(f.Rows.SequenceEqual(before) && f.Image.AsSpan().SequenceEqual(image));
            if (item.Groups.Length == 1 && item.Groups[0] == 1)
            {
                var legacy = Vector(37, 37, false);
                var legacyDevice = Reads.Inspect(legacy.Rows, legacy.Image, legacy.Boundary);
                HostCheck.Equal(item.Requests, legacyDevice.Counters.Copied);
                QemuTranscripts.InspectNativeAotDiskStaticEndValues(FullParentCounterEnd(legacy.Boundary, 4), legacy.Boundary, legacyDevice);
            }
            else
                Reject(f); // Multi-sector observations still require explicit selection.
        }
    }

    [HostTest("Full parent counters reject wrong filesystem sectors forged request totals and foreign boundaries")]
    private static void FullParentSectorCountersRejected()
    {
        var f = FullParentCounterVector(37, 1, 4);
        var device = Reads.Inspect(f.Rows, f.Image, f.Boundary, allowMultiSector: true);
        string end = FullParentCounterEnd(f.Boundary, 8);
        QemuTranscripts.InspectNativeAotDiskStaticEndValues(end, f.Boundary, device);
        HostCheck.Equal(2, device.Rows.Count(r => r.Kind == Reads.Kind.Submitted));
        HostCheck.Equal(5, device.Rows.Count(r => r.Kind is Reads.Kind.Copied or Reads.Kind.CopyChunk));
        void Deny(string row, Reads.Boundary expected, Reads.Inspection actual)
        {
            bool rejected = false;
            try
            {
                QemuTranscripts.InspectNativeAotDiskStaticEndValues(row, expected, actual);
            }
            catch (ToolFailure)
            {
                rejected = true;
            }

            HostCheck.True(rejected);
        }

        foreach (ulong reads in new[]
        {
            7UL,
            9UL,
            20UL,
            30UL,
            ulong.MaxValue
        }

        )
            Deny(FullParentCounterEnd(f.Boundary, reads), f.Boundary, device);
        // 20/30 would result from counting copied chunks/all observation stages
        // rather than only the actual Submitted request lengths.
        var forged = f.Boundary with
        {
            Submitted = 40,
            Completed = 40,
            Copied = 40
        };
        Deny(FullParentCounterEnd(forged, 8), f.Boundary, device);
        Deny(FullParentCounterEnd(forged, 8), forged, device);
        Deny(end, f.Boundary with
        {
            Owner = f.Boundary.Owner + 1
        }, device);
        Deny(end, f.Boundary with
        {
            Epoch = f.Boundary.Epoch + 1
        }, device);
        var wrongBootstrap = f.Boundary with
        {
            BootstrapCompleted = 36,
            BootstrapCopied = 36
        };
        Deny(end, wrongBootstrap, device);
        Deny(FullParentCounterEnd(wrongBootstrap, 8), f.Boundary, device);
        Deny(end, f.Boundary with
        {
            BootstrapCopied = 36
        }, device);
        var one = FullParentCounterVector(37, 4);
        var other = Reads.Inspect(one.Rows, one.Image, one.Boundary, allowMultiSector: true);
        Deny(end, f.Boundary, other);
        var invalidBootstrap = FullParentCounterVector(34, 1, 4);
        var invalid = Reads.Inspect(invalidBootstrap.Rows, invalidBootstrap.Image, invalidBootstrap.Boundary, allowMultiSector: true);
        Deny(FullParentCounterEnd(invalidBootstrap.Boundary, 5), invalidBootstrap.Boundary, invalid);
    }

    private static Fixture FullParentCounterVector(ulong bootstrap, params uint[] groups)
    {
        var first = MultiVector(groups[0]);
        List<string> rows = [];
        for (int group = 0; group < groups.Length; group++)
        {
            ulong generation = bootstrap + (ulong)group + 1;
            foreach (string source in MultiVector(groups[group]).Rows)
            {
                bool submitted = source.StartsWith("M16_023_IO kind=0 ", StringComparison.Ordinal);
                string row = Set(Set(source, "generation", N(generation)), "sequence", N(generation));
                if (groups[group] == 1)
                    row = row.Replace(" copied=committed chunk=0 data=", " copied=committed data=", StringComparison.Ordinal);
                row = Set(row, "available", N(unchecked((ushort)generation)));
                row = Set(row, "consumed", N(unchecked((ushort)(submitted ? generation - 1 : generation))));
                rows.Add(Set(row, "clock", N(100UL + (ulong)group)));
            }
        }

        ulong final = bootstrap + (ulong)groups.Length;
        return new(first.Image, rows, new(first.Boundary.Owner, first.Boundary.Epoch, bootstrap, bootstrap, final, final, final));
    }

    private static string FullParentCounterEnd(Reads.Boundary b, ulong fsReads) => FormattableString.Invariant($"M16_025_END submitted={b.Submitted} completed={b.Completed} copied={b.Copied} bootstrapCompleted={b.BootstrapCompleted} bootstrapCopied={b.BootstrapCopied} fsReads={fsReads} cacheHits=20 cacheMisses=1 device=retired fs=retired dma=zero descriptors=closed reservation=retained");
    private static Fixture MultiVector(uint sectors, bool cancelled = false)
    {
        byte[] image = Enumerable.Range(0, (int)Math.Max(8, sectors + 1) * 512).Select(i => unchecked((byte)(i * 31 ^ i >> 8))).ToArray();
        string Head(uint kind, uint bytes) => FormattableString.Invariant($"M16_023_IO kind={kind} owner=4194312 epoch=1193046 generation=1 sector=1 sequence=1 bytes={bytes} available=1 consumed={(kind == 0 ? 0 : 1)} clock=100");
        List<string> rows = [Head(0, sectors * 512), Head(1, cancelled ? 0 : sectors * 512)];
        if (!cancelled)
            for (uint chunk = 0; chunk < sectors; chunk++)
                rows.Add(Head(chunk == 0 ? 2U : 3U, sectors * 512) + " copied=committed chunk=" + N(chunk) + " data=" + Convert.ToHexString(image.AsSpan((int)(chunk + 1) * 512, 512)).ToLowerInvariant());
        return new(image, rows, new(4194312, 1193046, 0, 0, 1, 1, cancelled ? 0UL : 1UL));
    }

    [HostTest("disk reset retirement keeps the exact last pending submit without inventing completion or copied bytes")]
    private static void PendingReset()
    {
        foreach (ulong bootstrap in new[]
        {
            9UL,
            65534UL,
            ulong.MaxValue - 3
        }

        )
        {
            var full = Vector(bootstrap, bootstrap, false, true, false);
            var boundary = full.Boundary with
            {
                Completed = full.Boundary.Completed - 1,
                Copied = full.Boundary.Copied - 1
            };
            string[] rows = full.Rows.Take(full.Rows.Count - 2).ToArray();
            var reset = ResetFor(boundary, 3, 1);
            var actual = Reads.InspectResetRetired(rows, full.Image, boundary, reset);
            HostCheck.Equal(boundary, actual.Counters);
            HostCheck.Equal(1UL, actual.Cancelled);
            HostCheck.True(actual.ResetRetirement == reset && actual.Rows.Count == rows.Length && actual.Rows[^1].Kind == Reads.Kind.Submitted && actual.Rows[^1].Generation == boundary.Submitted && actual.Rows[^1].DataHex == "" && actual.Rows.Count(r => r.Kind == Reads.Kind.CompletionValidated) == 2);
            Reject(full, rows, boundary: boundary); // The ordinary consumer remains drained-only.
            string retained = actual.Rows[2].DataHex;
            Array.Fill(rows, "changed");
            Array.Clear(full.Image);
            HostCheck.Equal(retained, actual.Rows[2].DataHex);
        }

        HostCheck.True(Inspect(Vector()).ResetRetirement is null);
    }

    [HostTest("disk reset retirement rejects stale generations sectors incomplete quiescence changed counters and ready data")]
    private static void PendingResetRejections()
    {
        var full = Vector(9, 9, false, true, false);
        var boundary = full.Boundary with
        {
            Completed = full.Boundary.Completed - 1,
            Copied = full.Boundary.Copied - 1
        };
        string[] rows = full.Rows.Take(full.Rows.Count - 2).ToArray();
        var reset = ResetFor(boundary, 3, 1);
        void Deny(Reads.PendingResetRetirement changed, IReadOnlyList<string>? selected = null, Reads.Boundary? counters = null)
        {
            bool rejected = false;
            try
            {
                _ = Reads.InspectResetRetired(selected ?? rows, full.Image, counters ?? boundary, changed);
            }
            catch (ToolFailure)
            {
                rejected = true;
            }

            HostCheck.True(rejected);
        }

        foreach (var changed in new[]
        {
            reset with
            {
                Owner = reset.Owner + 8
            },
            reset with
            {
                Epoch = reset.Epoch + 1
            },
            reset with
            {
                Generation = reset.Generation - 1
            },
            reset with
            {
                Sector = reset.Sector + 1
            },
            reset with
            {
                SubmittedBeforeStop = reset.SubmittedBeforeStop - 1
            },
            reset with
            {
                CompletedBeforeStop = reset.CompletedBeforeStop + 1
            },
            reset with
            {
                CopiedBeforeStop = reset.CopiedBeforeStop + 1
            },
            reset with
            {
                CancelledBeforeStop = 0
            },
            reset with
            {
                StopStatus = OS.Storage.ContinuingReadOnlyBlockService.Status.ResetFailed
            },
            reset with
            {
                ResetReads = 0
            },
            reset with
            {
                ResetReads = 2049
            },
            reset with
            {
                ResetComplete = false
            },
            reset with
            {
                BusMasterDisabled = false
            },
            reset with
            {
                DmaReleased = false
            },
            reset with
            {
                PoolMask = 1
            }
        }

        )
            Deny(changed);
        Deny(reset, rows[..^1]);
        Deny(reset, full.Rows);
        Deny(reset, full.Rows.Take(full.Rows.Count - 1).ToArray());
        Deny(reset, [.. rows, rows[^1]]);
        Deny(reset, counters: boundary with
        {
            Submitted = boundary.Submitted + 1
        });
        Deny(reset, counters: boundary with
        {
            Completed = boundary.Completed + 1
        });
        Deny(reset, counters: boundary with
        {
            BootstrapCopied = boundary.BootstrapCopied - 1
        });
        Deny(reset, Replace(rows, 0, Set(rows[0], "sector", "2")));
    }

    private static Reads.PendingResetRetirement ResetFor(Reads.Boundary boundary, ulong sector, ulong cancelled) => new(boundary.Owner, boundary.Epoch, boundary.Submitted, sector, boundary.Submitted, boundary.Completed, boundary.Copied, cancelled, OS.Storage.ContinuingReadOnlyBlockService.Status.Success, 1, true, true, true, 0);
    [HostTest("disk observation joins ordered copied sectors and cancellation to exact image bytes")]
    private static void Complete()
    {
        var f = Vector(9, 7, false, true, false);
        var actual = Inspect(f);
        HostCheck.Equal(f.Boundary, actual.Counters);
        HostCheck.Equal(1UL, actual.Cancelled);
        HostCheck.Equal(8, actual.Rows.Count);
        HostCheck.Equal(Reads.Kind.Submitted, actual.Rows[0].Kind);
        HostCheck.Equal(Reads.Kind.CompletionValidated, actual.Rows[1].Kind);
        HostCheck.Equal(Reads.Kind.Copied, actual.Rows[2].Kind);
        HostCheck.True(actual.Rows[2].Generation == 10 && actual.Rows[2].Sequence == 8 && actual.Rows[2].Sector == 1);
        HostCheck.True(actual.Rows[4].Bytes == 0 && actual.Rows[4].DataHex == "");
        foreach (var copied in actual.Rows.Where(row => row.Kind == Reads.Kind.Copied))
            HostCheck.True(Convert.FromHexString(copied.DataHex).AsSpan().SequenceEqual(f.Image.AsSpan((int)copied.Sector * 512, 512)));
        // A value-only sector join does not authenticate unobserved image bytes.
        f.Image[0] ^= 1;
        HostCheck.Equal(8, Inspect(f).Rows.Count);
        var cancelled = Vector(10, 5, true);
        actual = Inspect(cancelled);
        HostCheck.Equal(1UL, actual.Cancelled);
        HostCheck.Equal(2, actual.Rows.Count);
        HostCheck.Equal(5UL, actual.Counters.Copied);
    }

    [HostTest("disk observation handles cumulative ushort ring wrap and final ulong generation")]
    private static void WrapAndMaximum()
    {
        var f = Vector(65534, 65530, false, false, true, false);
        var actual = Inspect(f);
        HostCheck.Equal((ushort)65535, actual.Rows[0].AvailableIndex);
        HostCheck.Equal((ushort)65534, actual.Rows[0].ConsumedIndex);
        HostCheck.Equal((ushort)0, actual.Rows[3].AvailableIndex);
        HostCheck.Equal((ushort)65535, actual.Rows[3].ConsumedIndex);
        HostCheck.Equal((ushort)0, actual.Rows[4].ConsumedIndex);
        HostCheck.Equal(65538UL, actual.Counters.Submitted);
        HostCheck.Equal(65533UL, actual.Counters.Copied);
        actual = Inspect(Vector(ulong.MaxValue - 1, ulong.MaxValue - 1, false));
        HostCheck.Equal(ulong.MaxValue, actual.Rows[0].Generation);
        HostCheck.Equal(ulong.MaxValue, actual.Counters.Copied);
        f = Vector();
        for (int i = 0; i < f.Rows.Count; i++)
            f.Rows[i] = Set(f.Rows[i], "clock", "0");
        HostCheck.True(Inspect(f).Rows.All(row => row.Clock == 0)); // Equal clocks are monotonic.
    }

    [HostTest("disk observation result is immutable and retains neither caller image nor row collection")]
    private static void Copies()
    {
        var f = Vector();
        byte[] before = f.Image.ToArray();
        string[] rows = f.Rows.ToArray();
        var actual = Inspect(f);
        HostCheck.True(f.Image.AsSpan().SequenceEqual(before) && f.Rows.SequenceEqual(rows));
        string copied = actual.Rows[2].DataHex;
        Reads.Observation first = actual.Rows[0];
        Array.Clear(f.Image);
        f.Rows[0] = "mutated";
        f.Rows.Clear();
        HostCheck.Equal(first, actual.Rows[0]);
        HostCheck.Equal(copied, actual.Rows[2].DataHex);
        HostCheck.True(Convert.FromHexString(copied).AsSpan().SequenceEqual(before.AsSpan(512, 512)));
        bool immutable = false;
        try
        {
            ((IList<Reads.Observation>)actual.Rows)[0] = default;
        }
        catch (NotSupportedException)
        {
            immutable = true;
        }

        HostCheck.True(immutable);
    }

    [HostTest("disk observation rejects malformed decimal exact fields suffix and lowercase sector payloads")]
    private static void Shapes()
    {
        var f = Vector();
        string[] badSubmit = [" " + f.Rows[0], f.Rows[0] + " ", f.Rows[0] + "\r", f.Rows[0] + "\n", f.Rows[0].Replace(" owner=", "\towner=", StringComparison.Ordinal), f.Rows[0].Replace(" owner=", "  owner=", StringComparison.Ordinal), f.Rows[0].Replace(" epoch=", " unknown=", StringComparison.Ordinal), Set(f.Rows[0], "kind", "3"), Set(f.Rows[0], "kind", "00"), Set(f.Rows[0], "owner", "+7"), Set(f.Rows[0], "owner", "-7"), Set(f.Rows[0], "owner", "\u0661"), Set(f.Rows[0], "owner", "18446744073709551616"), Set(f.Rows[0], "epoch", "01"), Set(f.Rows[0], "clock", "1.0"), Set(f.Rows[0], "clock", "18446744073709551616"), Set(f.Rows[0], "available", "65536"), f.Rows[0] + " extra=0", f.Rows[0] + " copied=committed data=" + new string('0', 1024)];
        foreach (string row in badSubmit)
            Reject(f, Replace(f.Rows, 0, row));
        string hex = Inspect(f).Rows[2].DataHex;
        foreach (string row in new[]
        {
            Set(f.Rows[2], "data", hex.ToUpperInvariant()),
            Set(f.Rows[2], "data", hex[..1023]),
            Set(f.Rows[2], "data", hex + "0"),
            Set(f.Rows[2], "data", "g" + hex[1..]),
            Set(f.Rows[2], "copied", "pending"),
            Set(f.Rows[2], "bytes", "0"),
            f.Rows[2][..f.Rows[2].IndexOf(" copied=", StringComparison.Ordinal)],
            f.Rows[2] + " unknown=1"
        }

        )
            Reject(f, Replace(f.Rows, 2, row));
    }

    [HostTest("disk observation rejects stale owner epoch generation sector sequence clock and ring associations")]
    private static void Associations()
    {
        var f = Vector();
        (int Row, string Field, string Value)[] mutants = [(0, "owner", "1"), (1, "epoch", "2"), (2, "owner", "1"), (0, "generation", "0"), (0, "generation", "2"), (1, "generation", "2"), (3, "generation", "1"), (2, "generation", "2"), (1, "sector", "2"), (2, "sector", "2"), (0, "sequence", "2"), (1, "sequence", "2"), (2, "sequence", "2"), (0, "bytes", "0"), (1, "bytes", "1"), (1, "bytes", "513"), (0, "available", "0"), (0, "consumed", "1"), (1, "available", "2"), (1, "consumed", "0"), (2, "available", "0"), (2, "consumed", "0"), (1, "clock", "99"), (2, "clock", "100")];
        foreach (var mutant in mutants)
            Reject(f, Replace(f.Rows, mutant.Row, Set(f.Rows[mutant.Row], mutant.Field, mutant.Value)));
        var wrap = Vector(65535, 65535, false);
        Reject(wrap, Replace(wrap.Rows, 0, Set(wrap.Rows[0], "available", "65535")));
        Reject(wrap, Replace(wrap.Rows, 1, Set(wrap.Rows[1], "consumed", "65535")));
    }

    [HostTest("disk observation rejects missing extra duplicate reordered and uncopied positive lifetimes")]
    private static void Ordering()
    {
        var f = Vector();
        for (int i = 0; i < f.Rows.Count; i++)
        {
            var rows = f.Rows.ToList();
            rows.RemoveAt(i);
            Reject(f, rows);
        }

        foreach (int duplicate in new[]
        {
            0,
            1,
            2
        }

        )
        {
            var rows = f.Rows.ToList();
            rows.Insert(duplicate + 1, rows[duplicate]);
            Reject(f, rows);
        }

        foreach (var pair in new[]
        {
            (0, 1),
            (1, 2),
            (2, 3)
        }

        )
        {
            var rows = f.Rows.ToList();
            (rows[pair.Item1], rows[pair.Item2]) = (rows[pair.Item2], rows[pair.Item1]);
            Reject(f, rows);
        }

        Reject(f, [.. f.Rows, f.Rows[0]]);
        Reject(f, [.. f.Rows, "TEST_PASS unrelated"]);
        var cancelled = Vector(0, 0, true, false);
        string falseCopy = Set(Set(f.Rows[2], "sequence", "1"), "consumed", "1");
        Reject(cancelled, [cancelled.Rows[0], cancelled.Rows[1], falseCopy, .. cancelled.Rows.Skip(2)]);
        // A positive completion cannot silently become a cancelled/omitted copy.
        Reject(f, Replace(f.Rows, 1, Set(f.Rows[1], "bytes", "0")));
    }

    [HostTest("disk observation compares actual full sector bytes and bounds unsigned64 LBA before multiplication")]
    private static void ImageBytesAndLba()
    {
        var f = Vector();
        foreach (int index in new[]
        {
            0,
            255,
            511
        }

        )
        {
            byte[] image = f.Image.ToArray();
            image[512 + index] ^= 1;
            Reject(f, image: image);
        }

        string hex = Inspect(f).Rows[2].DataHex;
        Reject(f, Replace(f.Rows, 2, Set(f.Rows[2], "data", (hex[0] == '0' ? "1" : "0") + hex[1..])));
        foreach (ulong sector in new[]
        {
            (ulong)f.Image.Length / 512,
            (ulong)long.MaxValue + 1,
            ulong.MaxValue / 512 + 1,
            ulong.MaxValue
        }

        )
            Reject(f, Replace(f.Rows, 0, Set(f.Rows[0], "sector", N(sector))));
        Reject(f, image: []);
        Reject(f, image: new byte[511]);
        Reject(f, image: new byte[513]);
        var cancel = Vector(0, 0, true);
        Reject(cancel, Replace(cancel.Rows, 0, Set(cancel.Rows[0], "sector", N(ulong.MaxValue))));
    }

    [HostTest("disk observation rejects wrong bootstrap terminal counters bounds and numeric overflow")]
    private static void BoundariesAndBounds()
    {
        var f = Vector(0, 0, false, true, false);
        var b = f.Boundary;
        foreach (var boundary in new[]
        {
            b with
            {
                Owner = 0
            },
            b with
            {
                Epoch = 0
            },
            b with
            {
                Owner = b.Owner + 8
            },
            b with
            {
                Epoch = b.Epoch + 1
            },
            b with
            {
                BootstrapCompleted = 1
            },
            b with
            {
                BootstrapCopied = 1
            },
            b with
            {
                BootstrapCopied = 4
            },
            b with
            {
                Submitted = 2
            },
            b with
            {
                Completed = 2
            },
            b with
            {
                Submitted = 4,
                Completed = 4
            },
            b with
            {
                Copied = 1
            },
            b with
            {
                Copied = 3
            },
            b with
            {
                Copied = 4
            },
            b with
            {
                BootstrapCompleted = ulong.MaxValue,
                BootstrapCopied = ulong.MaxValue,
                Submitted = ulong.MaxValue,
                Completed = ulong.MaxValue,
                Copied = ulong.MaxValue
            }
        }

        )
            Reject(f, boundary: boundary);
        Reject(f, []);
        Reject(f, new CountOnly(Reads.MaximumRows + 1));
        Reject(f, Replace(f.Rows, 0, new string('x', Reads.MaximumRowChars + 1)));
        Reject(f, Replace(f.Rows, 0, null!));
        var last = Vector(ulong.MaxValue - 1, ulong.MaxValue - 1, false);
        Reject(last, Replace(last.Rows, 0, Set(last.Rows[0], "sequence", "18446744073709551616")));
        Reject(last, [.. last.Rows, last.Rows[0]]);
    }

    private static Reads.Inspection Inspect(Fixture f) => Reads.Inspect(f.Rows, f.Image, f.Boundary);
    private static string N(ulong value) => value.ToString(CultureInfo.InvariantCulture);
    private static Fixture Vector(ulong bootstrap = 0, ulong bootstrapCopied = 0, params bool[] cancellations)
    {
        if (cancellations.Length == 0)
            cancellations = [false, false];
        byte[] image = Enumerable.Range(0, 8 * 512).Select(i => unchecked((byte)((i * 31) ^ (i >> 8) ^ 0x5a))).ToArray();
        List<string> rows = [];
        ulong submitted = bootstrap, completed = bootstrap, copied = bootstrapCopied, clock = 100;
        for (int i = 0; i < cancellations.Length; i++)
        {
            ulong generation = checked(++submitted), sector = (ulong)(i % 3 + 1);
            Row(0, generation, sector, submitted, 512);
            completed = checked(completed + 1);
            Row(1, generation, sector, completed, cancellations[i] ? 0U : 512U);
            if (!cancellations[i])
            {
                copied = checked(copied + 1);
                Row(2, generation, sector, copied, 512);
            }
        }

        return new(image, rows, new(0x400008, 0x123456, bootstrap, bootstrapCopied, submitted, completed, copied));
        void Row(uint kind, ulong generation, ulong sector, ulong sequence, uint bytes)
        {
            string row = FormattableString.Invariant($"M16_023_IO kind={kind} owner={0x400008UL} epoch={0x123456UL} generation={generation} sector={sector} sequence={sequence} bytes={bytes} available={unchecked((ushort)submitted)} consumed={unchecked((ushort)completed)} clock={clock++}");
            if (kind == 2)
                row += " copied=committed data=" + Convert.ToHexString(image.AsSpan((int)sector * 512, 512)).ToLowerInvariant();
            rows.Add(row);
        }
    }

    private static string Set(string row, string key, string value)
    {
        string prefix = " " + key + "=";
        int start = row.IndexOf(prefix, StringComparison.Ordinal) + prefix.Length;
        int end = row.IndexOf(' ', start);
        if (end < 0)
            end = row.Length;
        return row[..start] + value + row[end..];
    }

    private static string[] Replace(IReadOnlyList<string> rows, int index, string value)
    {
        string[] copy = rows.ToArray();
        copy[index] = value;
        return copy;
    }

    private static void Reject(Fixture f, IReadOnlyList<string>? rows = null, byte[]? image = null, Reads.Boundary? boundary = null)
    {
        bool rejected = false;
        try
        {
            _ = Reads.Inspect(rows ?? f.Rows, image ?? f.Image, boundary ?? f.Boundary);
        }
        catch (ToolFailure)
        {
            rejected = true;
        }

        HostCheck.True(rejected);
    }
}
