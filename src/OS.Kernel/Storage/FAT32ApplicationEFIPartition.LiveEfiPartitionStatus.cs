using System;
using OS.Std.Binary;

namespace OS.Storage;

public enum LiveEfiPartitionStatus : byte { Uninitialized, Success, InvalidTable, Missing, Ambiguous }
